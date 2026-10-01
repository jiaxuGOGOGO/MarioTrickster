using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// S214：网页设计台 ↔ Unity 自动同步（不用再"导出 → 下载 → 打开 Unity → 导入 → 选文件"）。
/// 借鉴 LDtk to Unity（https://github.com/Cammin/LDtkToUnity ）："外部编辑器一保存，Unity 就自动重新导入"。
///   网页 → Unity：网页"🔗 连接项目文件夹"后，每次改动把变了的关卡/小镇写进 Assets/Levels/Inbox/*.json；
///                  Unity 回到前台时自动刷新 → 这里自动导入（和手动"导入关卡包"同一条路），打开着的工坊自动换成新版本。
///   Unity → 网页：网页直接读 Assets/Levels/Library/*.txt 和 Assets/Levels/Overworld/*.txt（切回网页时自动拉取）。
/// 安全网：覆盖关卡库/小镇文件前，旧内容先备份到 Library/MarioTricksterHistory（Unity 自己的缓存目录，不进 git），每个名字留最近 10 份。
/// 浏览器不支持文件夹访问（Firefox / Safari）时：把导出的 .levelpack.json 拖进 Assets/Levels/Inbox 也一样自动导入。
/// </summary>
public sealed class WebSync : AssetPostprocessor
{
    public const string Inbox = "Assets/Levels/Inbox";
    public const string HistoryDir = "Library/MarioTricksterHistory";
    public const int KeepHistory = 10;

    /// <summary>自动导入后通知打开着的工坊：（关卡名们，小镇名们，报告）。</summary>
    public static event Action<List<string>, List<string>, string> Imported;

    private static readonly HashSet<string> pending = new HashSet<string>();

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        bool any = false;
        foreach (var p in imported)
        {
            string path = p.Replace('\\', '/');
            if (!IsInboxFile(path)) continue;
            pending.Add(path); any = true;
        }
        if (any) EditorApplication.delayCall += Flush; // 不在导入回调里再写资源（Unity 会警告/递归）
    }

    public static bool IsInboxFile(string path) => path.StartsWith(Inbox + "/") && (path.EndsWith(".json") || path.EndsWith(".txt"));

    private static void Flush()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall += Flush; return; } // Play 时先不动，停了再导
        var list = pending.ToList(); pending.Clear();
        string play = null;
        foreach (var path in list.OrderBy(p => p.Contains("_play_") ? 1 : 0)) // 先收关卡，再处理"试玩"
        {
            if (!File.Exists(path)) continue;
            string text = File.ReadAllText(path);
            if (text.Contains("\"mariotrickster-play\"")) { play = text; AssetDatabase.DeleteAsset(path); continue; }
            ImportFile(path, text);
            AssetDatabase.DeleteAsset(path); // 收完就删：收件箱只放"还没收的"，不会被旧文件反复覆盖
        }
        OverworldBuilder.InvalidateCaches(); // S220：关卡库 / 小镇列表的缓存作废
        if (play != null) Play(play);
    }

    /// <summary>S214：网页"▶ 在 Unity 试玩"：{play:{level:名字}} → 这一关作为第 1 步房间开玩；{play:{town:名字}} → 这张小镇构建并开玩。</summary>
    private static void Play(string json)
    {
        var root = MiniJson.Parse(json, out _) as Dictionary<string, object>;
        var p = root != null && root.TryGetValue("play", out var o) ? o as Dictionary<string, object> : null;
        if (p == null) return;
        if (p.TryGetValue("town", out var tn) && tn is string town)
        {
            string path = OverworldBuilder.PathFor(town);
            if (!File.Exists(path)) { Debug.LogWarning("[WebSync] 找不到小镇 " + town); return; }
            string text = File.ReadAllText(path);
            EditorPrefs.SetString(OverworldBuilder.CurrentKey, path);
            if (OverworldBuilder.BuildAll(text, out string rep)) EditorApplication.isPlaying = true;
            else EditorUtility.DisplayDialog("网页 ▶ 试玩小镇", rep, "好");
            return;
        }
        if (p.TryGetValue("level", out var ln) && ln is string level)
        {
            var e = LevelLibrary.List().Find(l => l.name == level);
            if (e.path == null) { Debug.LogWarning("[WebSync] 找不到关卡 " + level); return; }
            var rows = File.ReadAllText(e.path).Replace("\r", "").Split('\n').Where(l => l.Length > 0 && !l.StartsWith("#")).ToArray();
            Step1PrankRoomBuilder.SaveCustomRoom(rows);
            Step1PrankRoomBuilder.UseCustomRoom = true;
            Step1PrankRoomBuilder.PlayMenu();
        }
    }

    /// <summary>导入一个收件箱文件（关卡包 JSON / 小镇 .txt）。返回报告。</summary>
    public static string ImportFile(string path, string text)
    {
        var levels = new List<string>(); var towns = new List<string>(); string report;
        try
        {
            bool town = OverworldMap.IsOverworldText(text);
            if (!town)
            {
                var reg = AsciiElementRegistry.GetDefault();
                var parsed = LevelPack.Parse(text, c => reg.GetEntry(c) != null || Step1Layout.Slots.ContainsKey(c), out _);
                if (parsed != null) levels.AddRange(parsed.Select(l => l.name));
            }
            towns.AddRange(OverworldPack.Parse(text).Select(m => m.name));
            report = town ? OverworldBuilder.Import(text).report : LevelLibrary.ImportPack(text).report;
        }
        catch (Exception e) { report = "网页同步导入失败：" + e.Message; Debug.LogError("[WebSync] " + path + "：" + e); return report; }
        Debug.Log($"[WebSync] 从网页收到 {Path.GetFileName(path)}：\n{report}");
        Imported?.Invoke(levels, towns, report);
        return report;
    }

    // ── 备份 ────────────────────────────────
    /// <summary>要覆盖的文件内容不同 → 先备份旧的。LevelLibrary.Save / OverworldBuilder.Save 调用。</summary>
    public static void BackupBeforeWrite(string assetPath, string newText)
    {
        try
        {
            if (!File.Exists(assetPath)) return;
            string old = File.ReadAllText(assetPath);
            if (old.Replace("\r", "") == (newText ?? "").Replace("\r", "")) return;
            Directory.CreateDirectory(HistoryDir);
            string stem = Path.GetFileNameWithoutExtension(assetPath);
            File.WriteAllText(Path.Combine(HistoryDir, $"{stem}__{DateTime.Now:yyyyMMdd_HHmmss}.txt"), old);
            foreach (var f in Directory.GetFiles(HistoryDir, stem + "__*.txt").OrderByDescending(f => f, StringComparer.Ordinal).Skip(KeepHistory)) File.Delete(f);
        }
        catch (Exception e) { Debug.LogWarning("[WebSync] 备份失败（不影响保存）：" + e.Message); }
    }

    /// <summary>某个名字的备份（新 → 旧）。</summary>
    public static List<string> HistoryOf(string assetPath)
    {
        if (!Directory.Exists(HistoryDir)) return new List<string>();
        string stem = Path.GetFileNameWithoutExtension(assetPath);
        return Directory.GetFiles(HistoryDir, stem + "__*.txt").OrderByDescending(f => f, StringComparer.Ordinal).ToList();
    }

    [MenuItem("MarioTrickster/网页同步/打开收件箱文件夹 (Inbox)", false, 30)]
    public static void OpenInbox() { Directory.CreateDirectory(Inbox); AssetDatabase.Refresh(); EditorUtility.RevealInFinder(Inbox); }

    [MenuItem("MarioTrickster/网页同步/打开备份文件夹 (History)", false, 31)]
    public static void OpenHistory() { Directory.CreateDirectory(HistoryDir); EditorUtility.RevealInFinder(HistoryDir); }
}
