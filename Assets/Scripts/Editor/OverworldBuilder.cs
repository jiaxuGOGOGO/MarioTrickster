using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// S210：一键把小镇大地图 + 它连着的每个横版房间做成场景，登记到 Build Settings，然后 Play。
/// 房间：先找关卡库（Assets/Levels/Library）里同名的关卡，找不到再用内置样板/默认房间。
/// 每个房间场景里自动加 OverworldRoomLink（打完回小镇）。房间文字没变就不重建（按哈希缓存）。
/// S211：你只管画地图——
///   · 直接在 Town 场景按 Unity 的 ▶，或改了关卡库里的房间 / 调参主题，进 Play 前自动发现"场景过期"并只重建变了的那几个；
///   · 场景用完整路径登记和加载（不怕项目里别处也有叫 Town 的场景）；门变少时多余的 Room_N 场景自动删除；
///   · 切换走 SceneTransit（淡出 → 后台加载 → 淡入）。
/// </summary>
[InitializeOnLoad]
public static class OverworldBuilder
{
    public const string Folder = "Assets/Levels/Overworld";
    public const string SceneFolder = "Assets/Scenes/Overworld";
    public const string CurrentKey = "MarioTrickster.Overworld.Current";
    public static string TownScenePath => SceneFolder + "/Town.unity";
    public static string RoomScenePath(int n) => SceneFolder + "/Room_" + n + ".unity";

    public const string TownHashKey = "MarioTrickster.Overworld.TownHash";

    static OverworldBuilder() { EditorApplication.playModeStateChanged += OnPlayMode; }

    /// <summary>S211：在小镇场景里直接按 ▶ 时，场景过期 → 先停下、静默重建、再自动开始（只重建变了的房间）。</summary>
    private static void OnPlayMode(PlayModeStateChange st)
    {
        if (st != PlayModeStateChange.ExitingEditMode) return;
        if (EditorSceneManager.GetActiveScene().path != TownScenePath) return;
        string text = CurrentText;
        if (!IsStale(text)) return;
        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () =>
        {
            if (BuildAll(text, out string rep)) { Debug.Log("[Overworld] 场景过期，已自动重建后开始试玩。"); EditorApplication.isPlaying = true; }
            else EditorUtility.DisplayDialog("小镇", rep, "好");
        };
    }

    /// <summary>这张小镇 + 它连的所有房间 + 构建器版本 + 主题 的指纹。任何一样变了，场景就要重建。</summary>
    public static string TownFingerprint(string text)
    {
        var m = OverworldMap.Parse(text);
        var sb = new System.Text.StringBuilder(OverworldMap.ToText(m));
        sb.Append('|').Append(Step1PrankRoomBuilder.BuilderVersion).Append('|').Append(Step1PrankRoomBuilder.EnsureTuningAsset().themePreset);
        foreach (var d in m.doors) { var rows = ResolveRoom(d.room); sb.Append('|').Append(d.n).Append('=').Append(rows == null ? "?" : Step1PrankRoomBuilder.RoomHash(rows)); }
        return Hash128.Compute(sb.ToString()).ToString();
    }

    /// <summary>场景文件缺失 / 没登记 / 指纹不同 → 过期。</summary>
    public static bool IsStale(string text)
    {
        if (!File.Exists(TownScenePath)) return true;
        var m = OverworldMap.Parse(text);
        var inBuild = new HashSet<string>(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
        if (!inBuild.Contains(TownScenePath)) return true;
        foreach (var d in m.doors) if (!File.Exists(RoomScenePath(d.n)) || !inBuild.Contains(RoomScenePath(d.n))) return true;
        return EditorPrefs.GetString(TownHashKey, "") != TownFingerprint(text);
    }

    public static string PathFor(string name) => Folder + "/" + LevelPack.SafeFileName(name) + ".txt";

    /// <summary>当前小镇（工坊里最后保存/打开的那张；没有 → 内置样板）。</summary>
    public static string CurrentText
    {
        get
        {
            string p = EditorPrefs.GetString(CurrentKey, "");
            return !string.IsNullOrEmpty(p) && File.Exists(p) ? File.ReadAllText(p) : OverworldPack.SampleText;
        }
    }

    public static List<(string name, string path)> List()
    {
        var l = new List<(string, string)>();
        if (!Directory.Exists(Folder)) return l;
        foreach (var f in Directory.GetFiles(Folder, "*.txt").OrderBy(p => p, System.StringComparer.Ordinal))
        {
            var m = OverworldMap.Parse(File.ReadAllText(f));
            l.Add((m.name.Length > 0 ? m.name : Path.GetFileNameWithoutExtension(f), f.Replace('\\', '/')));
        }
        return l;
    }

    public static string Save(OverworldMap.Map m)
    {
        Directory.CreateDirectory(Folder);
        string p = PathFor(m.name.Length > 0 ? m.name : "小镇");
        File.WriteAllText(p, OverworldMap.ToText(m));
        AssetDatabase.ImportAsset(p);
        EditorPrefs.SetString(CurrentKey, p);
        return p;
    }

    /// <summary>导入：小镇 .txt 或关卡包 JSON（overworlds / levels 里 kind=overworld）。关卡包里的横版关卡同时进关卡库（门能连上它们）。</summary>
    public static (int count, string report) Import(string text)
    {
        var maps = OverworldPack.Parse(text);
        if (!OverworldMap.IsOverworldText(text) && (text ?? "").TrimStart().StartsWith("{"))
        {
            var (_, rep) = LevelLibrary.ImportPack(text); // 横版房间进关卡库 + 小镇进 Overworld 文件夹
            return (maps.Count, maps.Count == 0 ? rep + "\n（这个关卡包里没有小镇大地图）" : rep);
        }
        var lines = new List<string>();
        foreach (var m in maps)
        {
            string p = Save(m);
            var r = OverworldMap.Check(m, RulesFromTuning(), RoomProblem);
            lines.Add($"{(r.Playable ? "✓" : "✗")} 小镇 {m.name} → {p}  {r.Headline}");
        }
        AssetDatabase.Refresh();
        if (maps.Count == 0) lines.Add("没找到小镇大地图（需要 # Overworld: 开头的 .txt，或关卡包里的 overworlds）");
        return (maps.Count, string.Join("\n", lines));
    }

    public static OverworldMap.Rules RulesFromTuning()
    {
        var t = MarioMindTuningSO.LoadOrDefault();
        return new OverworldMap.Rules { marioSpeed = t.overworldMarioSpeed, tricksterSpeed = t.overworldTricksterSpeed, minutesPerSecond = t.overworldMinutesPerSecond, visitMinutes = t.overworldVisitMinutes };
    }

    /// <summary>门能连的所有房间名：默认房间 + 内置样板 + 关卡库。</summary>
    public static List<string> RoomNames()
    {
        var l = new List<string> { LevelWorkshopModel.DefaultRoomName };
        l.AddRange(LevelWorkshopModel.SampleRooms.Select(s => s.name));
        foreach (var e in LevelLibrary.List()) if (!l.Contains(e.name)) l.Add(e.name);
        return l;
    }

    /// <summary>房间名 → 网格（关卡库优先）。找不到返回 null。</summary>
    public static string[] ResolveRoom(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return null;
        foreach (var e in LevelLibrary.List())
            if (e.name == name)
                return File.ReadAllText(e.path).Replace("\r", "").Split('\n').Where(l => l.Length > 0 && !l.StartsWith("#")).ToArray();
        if (name == LevelWorkshopModel.DefaultRoomName) return Step1PrankRoomBuilder.Room;
        foreach (var s in LevelWorkshopModel.SampleRooms) if (s.name == name) return s.rows;
        return null;
    }

    /// <summary>给 Check 用：房间有问题返回原因，没问题返回 null。</summary>
    public static string RoomProblem(string name)
    {
        var rows = ResolveRoom(name);
        if (rows == null) return "找不到这个房间（关卡库里没有，也不是内置样板）";
        Step1PrankRoomBuilder.ValidateAllVariants(rows, out bool ok);
        return ok ? null : "房间本身检查没通过（在关卡工坊里打开它看红格）";
    }

    [MenuItem("MarioTrickster/Overworld/▶ Play Town (小镇)", false, 0)]
    public static void PlayMenu()
    {
        if (!BuildAll(CurrentText, out string report)) { EditorUtility.DisplayDialog("小镇", report, "好"); return; }
        EditorApplication.isPlaying = true;
    }

    [MenuItem("MarioTrickster/Overworld/Town Workshop (小镇工坊)", false, 1)]
    public static void OpenWorkshop() => OverworldWorkshopWindow.Open();

    /// <summary>构建小镇 + 所有房间场景，登记 Build Settings，停在小镇场景。</summary>
    public static bool BuildAll(string text, out string report)
    {
        report = "";
        if (EditorApplication.isPlaying) { report = "先停止 Play。"; return false; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { report = "已取消。"; return false; }
        var m = OverworldMap.Parse(text);
        var check = OverworldMap.Check(m, RulesFromTuning(), RoomProblem);
        if (!check.Playable) { report = "小镇还不能玩：\n" + string.Join("\n", check.issues.Where(i => i.sev == OverworldMap.Sev.Error).Select(i => "• " + i)); return false; }
        Directory.CreateDirectory(SceneFolder);
        var scenes = new List<string>();
        var nums = new List<int>(); var names = new List<string>();
        try
        {
            foreach (var d in m.doors)
            {
                var rows = ResolveRoom(d.room);
                string path = RoomScenePath(d.n);
                string hash = Step1PrankRoomBuilder.RoomHash(rows) + "|" + Step1PrankRoomBuilder.BuilderVersion + "|" + d.n + "|" + Step1PrankRoomBuilder.EnsureTuningAsset().themePreset;
                if (!(File.Exists(path) && EditorPrefs.GetString("MarioTrickster.Overworld.RoomHash." + d.n, "") == hash))
                {
                    Step1PrankRoomBuilder.RoomOverride = rows;
                    Step1PrankRoomBuilder.Build();
                    var link = new GameObject("OverworldRoomLink").AddComponent<OverworldRoomLink>();
                    link.door = d.n;
                    EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);
                    EditorPrefs.SetString("MarioTrickster.Overworld.RoomHash." + d.n, hash);
                }
                scenes.Add(path); nums.Add(d.n); names.Add(path); // S211：完整路径（不怕重名）
            }
        }
        finally { Step1PrankRoomBuilder.RoomOverride = null; }

        // S211：门变少了 → 删掉多余的旧房间场景（不然 Build Settings 里越积越多）
        foreach (var f in Directory.GetFiles(SceneFolder, "Room_*.unity"))
        {
            string fp = f.Replace('\\', '/');
            if (!scenes.Contains(fp)) { AssetDatabase.DeleteAsset(fp); EditorPrefs.DeleteKey("MarioTrickster.Overworld.RoomHash." + Path.GetFileNameWithoutExtension(fp).Substring(5)); }
        }

        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var go = new GameObject("OverworldGame");
        var g = go.AddComponent<OverworldGame>();
        g.mapText = OverworldMap.ToText(m);
        g.doorNumbers = nums.ToArray();
        g.doorScenes = names.ToArray();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), TownScenePath);

        var list = EditorBuildSettings.scenes.Where(s => !s.path.StartsWith(SceneFolder + "/")).ToList();
        list.Insert(0, new EditorBuildSettingsScene(TownScenePath, true));
        list.AddRange(scenes.Select(s => new EditorBuildSettingsScene(s, true)));
        EditorBuildSettings.scenes = list.ToArray();
        AssetDatabase.SaveAssets();
        EditorPrefs.SetString(TownHashKey, TownFingerprint(text));
        report = $"小镇 {m.name}：{m.doors.Count} 个房间场景 + 小镇场景已登记到 Build Settings。";
        Debug.Log("[Overworld] " + report);
        return true;
    }
}
