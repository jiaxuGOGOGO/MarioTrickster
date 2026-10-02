using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// S217：测试中心（Ctrl+Alt+T）。用户诉求："快速测试完整项目，不要被一堆反复操作卡住，测完能把反馈交给 AI 升级"。
/// 一个窗口放齐：① 一键体检（不用进 Play：所有关卡、所有小镇、机器人玩一天、场景新旧、测试报告）
/// ② 快速测试模式开关（不弹说明、不弹问卷）③ 试玩入口 ④ F8 反馈数量 + 打包反馈（zip：截图、说明、错误、体检、测试报告、试玩记录）。
/// 只读 / 只写 PlaytestLogs（不进 git），不改任何关卡和玩法。
/// </summary>
public sealed class TestHubWindow : EditorWindow
{
    private Vector2 scroll;
    private string report = "";
    private int errors, warns;
    private string note = "";

    public static string LogsRoot => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", Step1PlaytestLog.LogFolder);
    public static string HealthPath => Path.Combine(LogsRoot, "HealthCheck.md");

    [MenuItem("MarioTrickster/测试中心 Test Hub %&t", false, 3)]
    public static void Open()
    {
        var w = GetWindow<TestHubWindow>("测试中心");
        w.minSize = new Vector2(520, 480);
        if (w.report.Length == 0 && File.Exists(HealthPath)) w.report = File.ReadAllText(HealthPath);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("测试中心：按顺序点 ①②③ 就是一次完整测试", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("① 一键体检（几秒，不用进 Play）→ ② 试玩（小镇 / 房间，按 F8 随手截图记反馈）→ ③ 打包反馈，把 zip 发给 AI。\n" +
                                "游戏里按键没反应：先用鼠标点一下 Game 画面（现在进 Play 会自动切到 Game 窗口）。", MessageType.Info);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            bool q = Step1QuickTest.On;
            bool nq = EditorGUILayout.ToggleLeft(new GUIContent("⚡ 快速测试模式（不弹玩法说明、房间结束不弹 5 道问卷）", "反复测试时开着；想认真记每局感受时关掉"), q);
            if (nq != q) Step1QuickTest.On = nq;
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.6f, 0.9f, 1f);
                if (GUILayout.Button("① 一键体检", GUILayout.Height(34))) RunHealthCheck();
                GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
                if (GUILayout.Button("② ▶ 试玩小镇", GUILayout.Height(34))) EditorApplication.delayCall += OverworldBuilder.PlayMenu;
                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("② ▶ 试玩房间", GUILayout.Height(34))) EditorApplication.delayCall += Step1PrankRoomBuilder.PlayMenu;
                GUI.backgroundColor = new Color(1f, 0.85f, 0.4f);
                if (GUILayout.Button($"③ 打包反馈（{Step1Feedback.Count()} 张截图）", GUILayout.Height(34))) Pack();
                GUI.backgroundColor = Color.white;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("🏘 小镇工坊")) OverworldWorkshopWindow.Open();
                if (GUILayout.Button("🏠 关卡工坊")) LevelWorkshopWindow.Open();
                if (GUILayout.Button(new GUIContent("🧪 跑 EditMode 测试", "结果写进 TestReport.txt，打包反馈会带上"))) TestReportRunner.RunEditModeTests();
                if (GUILayout.Button("📂 打开记录文件夹")) { Directory.CreateDirectory(LogsRoot); EditorUtility.RevealInFinder(LogsRoot); }
            }
        }
        if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("正在试玩：按 F8 记反馈（截图 + 当时情况）。停止 Play 后再打包。", MessageType.None);

        EditorGUILayout.Space();
        note = EditorGUILayout.TextField(new GUIContent("给 AI 的一句话（可不写）", "会写进反馈包的最前面"), note);

        EditorGUILayout.Space();
        if (report.Length > 0)
        {
            EditorGUILayout.LabelField(errors > 0 ? $"体检：{errors} 个必须改、{warns} 个提醒" : $"体检：✓ 没有必须改的（{warns} 个提醒）", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }
    }

    /// <summary>不进 Play 的全项目体检。结果显示在窗口里并写 PlaytestLogs/HealthCheck.md。</summary>
    public void RunHealthCheck()
    {
        errors = warns = 0;
        var sb = new StringBuilder($"# 体检报告 {System.DateTime.Now:yyyy-MM-dd HH:mm}\n");
        void Err(string s) { errors++; sb.AppendLine("✗ " + s); }
        void Warn(string s) { warns++; sb.AppendLine("⚠ " + s); }
        void Ok(string s) => sb.AppendLine("✓ " + s);
        try
        {
            EditorUtility.DisplayProgressBar("体检", "横版房间", 0.1f);
            var t = Step1PrankRoomBuilder.EnsureTuningAsset();
            sb.AppendLine($"\n## 版本\n调参数据 v{t.dataVersion}（代码 v{MarioMindTuningSO.CurrentDataVersion}） · 房间构建器 v{Step1PrankRoomBuilder.BuilderVersion} · 快速测试模式 {(Step1QuickTest.On ? "开" : "关")}");
            if (t.dataVersion < MarioMindTuningSO.CurrentDataVersion) Warn("调参资产还是旧版本（进一次 Play 会自动升级）");

            sb.AppendLine("\n## 横版房间（默认 + 样板 + 关卡库）");
            var reg = AsciiElementRegistry.GetDefault();
            var rooms = new List<(string name, string[] rows)> { (LevelWorkshopModel.DefaultRoomName, Step1PrankRoomBuilder.Room) };
            rooms.AddRange(LevelWorkshopModel.SampleRooms);
            foreach (var e in LevelLibrary.List())
            {
                var rows = File.ReadAllText(e.path).Replace("\r", "").Split('\n').Where(l => l.Length > 0 && !l.StartsWith("#")).ToArray();
                rooms.Add((e.name + (e.pending.Count > 0 ? $"（有 {e.pending.Count} 种网页新机制还没实现）" : ""), rows));
            }
            foreach (var (name, rows) in rooms)
            {
                var c = LevelWorkshopModel.Check(rows, true, reg.IsSolid);
                if (c.Playable) Ok($"{name}：{c.Headline}"); else Err($"{name}：{c.Headline}（关卡工坊里打开看红格）");
            }

            EditorUtility.DisplayProgressBar("体检", "小镇", 0.5f);
            sb.AppendLine("\n## 小镇大地图");
            var towns = OverworldBuilder.List().Select(x => (x.name, File.ReadAllText(x.path))).ToList();
            if (!towns.Any(x => x.name == OverworldPack.SampleName)) towns.Insert(0, (OverworldPack.SampleName + "（内置样板）", OverworldPack.SampleText));
            if (!towns.Any(x => x.name == OverworldPack.BigSampleName)) towns.Insert(1, (OverworldPack.BigSampleName + "（内置样板）", OverworldPack.BigSampleText));
            if (!towns.Any(x => x.name == OverworldPack.MountainSampleName)) towns.Insert(2, (OverworldPack.MountainSampleName + "（内置样板）", OverworldPack.MountainSampleText));
            if (!towns.Any(x => x.name == OverworldPack.StormSampleName)) towns.Insert(3, (OverworldPack.StormSampleName + "（内置样板）", OverworldPack.StormSampleText));
            var rules = OverworldBuilder.RulesFromTuning();
            foreach (var (name, text) in towns)
            {
                var m = OverworldMap.Parse(text);
                var r = OverworldMap.Check(m, rules, OverworldBuilder.RoomProblem);
                if (!r.Playable) { Err($"{name}（{m.W}×{m.H}）：{r.Headline}"); foreach (var i in r.issues.Where(i => i.sev == OverworldMap.Sev.Error).Take(4)) sb.AppendLine("    • " + i); continue; }
                var day = OverworldWalker.SimulateDay(m, rules);
                if (!day.ok) { Err($"{name}：没人捣乱时一天走不完 —— {day.summary}"); continue; }
                int doors = r.schedule.stops.Count;
                var hider = OverworldBots.PlayDay(OverworldMap.Parse(text), t, OverworldBots.Kind.Hider, true, 1);
                var stand = OverworldBots.PlayDay(OverworldMap.Parse(text), t, OverworldBots.Kind.Follower, true, 1);
                OverworldSession.ResetStatics();
                Ok($"{name}（{m.W}×{m.H}，{doors} 扇门）：{r.Headline}｜一天约 {hider.realSeconds:0} 秒（快进后）｜会躲的机器人埋伏 {hider.ambush}/{doors}，站着不躲 {stand.ambush}/{doors}");
                if (hider.ambush < doors) Warn($"{name}：会躲的玩家也有门埋伏不上（缺藏身处或时间太紧）");
                if (!hider.dayEnded || !stand.dayEnded) Err($"{name}：机器人玩家的一天没结束（H9）");
            }
            string cur = OverworldBuilder.CurrentText;
            sb.AppendLine(OverworldBuilder.IsStale(cur) ? "· 小镇场景需要重建（点 ▶ 试玩小镇会自动做，不用管）" : "· 小镇场景已是最新");

            sb.AppendLine("\n## 第 1 步出口（从你的试玩记录自动算）");
            sb.Append(Step1ExitReport.Markdown(Step1ExitReport.ParseAll(Directory.Exists(LogsRoot)
                ? Directory.GetFiles(LogsRoot, "step1_rounds*.csv").Select(File.ReadAllText) : new string[0])));

            sb.AppendLine("\n## 小镇（从 town_days.csv 自动算，S230）");
            sb.Append(Step1ExitReport.TownMarkdown(Step1ExitReport.ParseTown(Directory.Exists(LogsRoot)
                ? Directory.GetFiles(LogsRoot, "town_days*.csv").Select(File.ReadAllText) : new string[0])));

            sb.AppendLine("\n## 上次测试");
            if (File.Exists(TestReportRunner.LastReportFile))
            {
                var first = File.ReadAllLines(TestReportRunner.LastReportFile).Take(12).Where(l => l.Contains("Pass") || l.Contains("Fail") || l.Contains("通过") || l.Contains("失败"));
                sb.AppendLine(string.Join("\n", first.Take(4)));
                sb.AppendLine($"（{File.GetLastWriteTime(TestReportRunner.LastReportFile):MM-dd HH:mm} 跑的；点 🧪 重跑）");
            }
            else sb.AppendLine("还没跑过：点 🧪 跑 EditMode 测试（约 1–3 分钟）");
            sb.AppendLine($"\n## 反馈\n已记 {Step1Feedback.Count()} 张 F8 截图" + (File.Exists(Path.Combine(Step1Feedback.Root, Step1Feedback.LogName)) ? "，feedback.md 里有说明/自动记下的错误" : ""));
        }
        catch (System.Exception e) { Err("体检自己出错了：" + e.Message); Debug.LogException(e); }
        finally { EditorUtility.ClearProgressBar(); }
        sb.Insert(sb.ToString().IndexOf('\n') + 1, errors > 0 ? $"\n**{errors} 个必须改，{warns} 个提醒**\n" : $"\n**✓ 没有必须改的（{warns} 个提醒）**\n");
        report = sb.ToString();
        Directory.CreateDirectory(LogsRoot);
        File.WriteAllText(HealthPath, report);
        Repaint();
    }

    /// <summary>把 Feedback 文件夹 + 体检 + TestReport + 试玩记录打成一个 zip（PlaytestLogs/反馈包_日期.zip）。</summary>
    private void Pack()
    {
        if (report.Length == 0) RunHealthCheck();
        string stage = Path.Combine(LogsRoot, "_pack");
        try
        {
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
            Directory.CreateDirectory(stage);
            File.WriteAllText(Path.Combine(stage, "00_给AI的话.md"), "# MarioTrickster 试玩反馈包\n\n" + (note.Length > 0 ? note : "（没写）") + "\n\n里面：HealthCheck.md 体检、feedback.md + 截图、TestReport.txt、step1_rounds.csv、town_days.csv（有的话）\n");
            void Copy(string src, string name) { if (File.Exists(src)) File.Copy(src, Path.Combine(stage, name), true); }
            Copy(HealthPath, "HealthCheck.md");
            Copy(TestReportRunner.LastReportFile, "TestReport.txt");
            if (Directory.Exists(LogsRoot)) foreach (var f in Directory.GetFiles(LogsRoot, "step1_rounds*.csv").Concat(Directory.GetFiles(LogsRoot, "town_days*.csv"))) Copy(f, Path.GetFileName(f)); // S227：留档的旧记录也带上；S230：小镇每天的记录（含反应时间）
            if (Directory.Exists(Step1Feedback.Root)) foreach (var f in Directory.GetFiles(Step1Feedback.Root)) Copy(f, Path.GetFileName(f));
            string zip = Path.Combine(LogsRoot, $"反馈包_{System.DateTime.Now:MMdd_HHmm}.zip");
            if (File.Exists(zip)) File.Delete(zip);
            TinyZip.Write(zip, Directory.GetFiles(stage));
            Directory.Delete(stage, true);
            EditorUtility.RevealInFinder(zip);
            if (EditorUtility.DisplayDialog("反馈包", "已打包：\n" + zip + "\n\n把它发给 AI 就行。要清空已发过的截图吗（下次从第 1 条开始）？", "清空", "保留"))
                if (Directory.Exists(Step1Feedback.Root)) Directory.Delete(Step1Feedback.Root, true);
        }
        catch (System.Exception e) { EditorUtility.DisplayDialog("反馈包", "打包失败：" + e.Message, "好"); }
    }
}

/// <summary>S217：最小 zip 写入（只存不压缩，UTF-8 文件名）。不用 System.IO.Compression——Unity 编辑器里不一定引用了它。</summary>
public static class TinyZip
{
    private static uint[] table;
    public static uint Crc(byte[] d)
    {
        if (table == null) { table = new uint[256]; for (uint i = 0; i < 256; i++) { uint c = i; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; table[i] = c; } }
        uint crc = 0xFFFFFFFFu; foreach (byte b in d) crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8); return crc ^ 0xFFFFFFFFu;
    }

    public static void Write(string zipPath, IEnumerable<string> files)
    {
        using (var fs = File.Create(zipPath))
        using (var w = new BinaryWriter(fs))
        {
            var central = new List<(byte[] name, uint crc, int size, uint offset)>();
            foreach (var f in files)
            {
                byte[] data = File.ReadAllBytes(f), name = Encoding.UTF8.GetBytes(Path.GetFileName(f)); uint crc = Crc(data), off = (uint)fs.Position;
                w.Write(0x04034b50u); w.Write((ushort)20); w.Write((ushort)0x0800); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0x21);
                w.Write(crc); w.Write(data.Length); w.Write(data.Length); w.Write((ushort)name.Length); w.Write((ushort)0); w.Write(name); w.Write(data);
                central.Add((name, crc, data.Length, off));
            }
            uint cdStart = (uint)fs.Position;
            foreach (var c in central)
            {
                w.Write(0x02014b50u); w.Write((ushort)20); w.Write((ushort)20); w.Write((ushort)0x0800); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0x21);
                w.Write(c.crc); w.Write(c.size); w.Write(c.size); w.Write((ushort)c.name.Length); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0); w.Write(0u); w.Write(c.offset); w.Write(c.name);
            }
            uint cdSize = (uint)fs.Position - cdStart;
            w.Write(0x06054b50u); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)central.Count); w.Write((ushort)central.Count); w.Write(cdSize); w.Write(cdStart); w.Write((ushort)0);
        }
    }
}
