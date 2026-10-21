using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 世界工坊的唯一主创作入口。地图与横版关卡仍是子工具；本窗口只保存作者显式导入/导出的世界包草稿。
/// S254：与网页设计台通过同一个文件「我的世界」(Assets/Resources/WorldPark/MyWorld.worldpark.json) 双向同步——
/// 网页点“🔗 写进 Unity 项目”后每次改动自动写入它；本窗口在草稿没有未保存改动时自动载入它的新版本，有改动时只提示、不替换。
/// </summary>
public sealed class WorldParkWindow : EditorWindow
{
    // S251：开始世界/读取示例使用主世界“空白之城”；旧“幕间乐园”保留为格式样例与回归用例。
    private const string DemoAssetPath = "Assets/Resources/WorldPark/Borderland.worldpark.json";
    /// <summary>S254 网页设计台与 Unity 共用的作者世界文件（用户内容：只在作者点按钮时由本窗口写入）。</summary>
    public const string MyWorldAssetPath = "Assets/Resources/WorldPark/MyWorld.worldpark.json";
    private const string MyWorldBackupPath = "Assets/Resources/WorldPark/.history/MyWorld.previous.worldpark.json"; // 以“.”开头的目录 Unity 不导入、不进构建
    private static readonly string[] BuiltInWorlds = { "Resources/WorldPark/Borderland.worldpark.json", "Resources/WorldPark/Demo.worldpark.json" };
    private static readonly string[] GameModeLabels = { "beauty 数字猜心", "mark 看不见的标记", "split 分还是偷" };
    private static readonly string[] PersonaLabels = { "player 玩家本人", "naive 直觉型", "reasoner 推一层", "deep 深推", "adaptive 跟随者", "liar 说谎者", "loyal 老实人", "mirror 以牙还牙" };

    [SerializeField] private WorldParkPack.Pack pack;
    [SerializeField] private int venueIndex;
    [SerializeField] private int nodeIndex;
    [SerializeField] private int clueIndex;
    [SerializeField] private bool rehearsalWin = true;
    [SerializeField] private string savedSnapshot = "";
    [SerializeField] private WorldParkSpatialEditor.State spatialState = new WorldParkSpatialEditor.State();
    [SerializeField] private long myWorldSeenTicks; // 最近一次看到/载入/写入「我的世界」时的文件修改时间
    [SerializeField] private int templateIndex;

    private Vector2 scroll;
    private Vector2 issueScroll;
    private string status = "草稿只留在此窗口；点“保存为我的世界”写进项目（网页设计台也能读），或用“导出…”另存。";
    private string myWorldNotice = "";
    private static List<string> templateNames;
    private string rehearsal = "尚未彩排。彩排是纯逻辑流程推演，不会启动 Unity 场景。";
    private List<WorldParkPack.Issue> issues = new List<WorldParkPack.Issue>();

    [MenuItem("MarioTrickster/世界工坊 World Workshop/会场与流程 %&p", false, 40)]
    public static void Open()
    {
        var window = GetWindow<WorldParkWindow>("世界工坊");
        window.minSize = new Vector2(920f, 650f);
        window.Show();
    }

    /// <summary>
    /// 唯一的编辑器主试玩入口。没有窗口草稿时只读取内置示例到内存，绝不覆写示例或用户地图。
    /// resume=true 时从当前世界版本的检查点继续；检查点缺失或校验失败会直接报告，绝不悄悄改为新旅程。
    /// </summary>
    public static void PlayCurrent() { PlayCurrent(false); }

    public static void ContinueCurrent() { PlayCurrent(true); }

    public static void PlayCurrent(bool resume)
    {
        var window = GetWindow<WorldParkWindow>("世界工坊");
        window.minSize = new Vector2(920f, 650f);
        window.Show();
        if (!window.EnsurePackForPlay())
        {
            window.Repaint();
            return;
        }
        window.SyncMyWorld(true); // 网页刚写进来的新版本（且 Unity 草稿没有未保存改动）→ 先载入再运行

        window.Check();
        if (WorldParkBuilder.TryRun(window.pack, out string report, resume))
            window.status = "《" + (window.pack.name ?? window.pack.id) + "》" + report;
        else
            window.status = (resume ? "未继续旅程：" : "未启动世界：") + report;
        window.Repaint();
    }

    /// <summary>S254 构建 Windows 版（开始页、本窗口按钮与菜单共用）：用当前草稿；网页刚写进来的新版本会先自动载入。</summary>
    public static void BuildWindowsCurrent()
    {
        var window = GetWindow<WorldParkWindow>("世界工坊");
        window.minSize = new Vector2(920f, 650f);
        window.Show();
        if (!window.EnsurePackForPlay()) { window.Repaint(); return; }
        window.SyncMyWorld(true);
        window.Check();
        string exe = WorldParkBuilder.DefaultWindowsExe(window.pack);
        if (!EditorUtility.DisplayDialog("构建 Windows 版",
            "把《" + (window.pack.name ?? window.pack.id) + "》构建成双击就能玩的 Windows 游戏：\n" + exe +
            "\n\n启动先显示标题页；Esc 菜单里有设置（界面大小 / 音量 / 全屏）、重来本会场、回到标题。\n第一次构建可能要几分钟，期间 Unity 不响应。只打包世界需要的场景，不改 Build Settings。", "开始构建", "取消"))
            return;
        bool ok = WorldParkBuilder.BuildWindows(window.pack, exe, true, out string report);
        window.status = report;
        if (ok) EditorUtility.RevealInFinder(exe);
        window.Repaint();
    }

    private void OnEnable()
    {
        // 不在这里自动建空草稿：Play World 第一次从菜单启动时，空包应读取内置示例。
        // OnGUI 才为真正打开的作者窗口建立空白草稿。
        if (pack != null)
        {
            if (string.IsNullOrEmpty(savedSnapshot)) savedSnapshot = WorldParkPack.ToJson(pack);
            Check();
            SyncMyWorld(true);
        }
    }

    private void OnFocus()
    {
        // 从浏览器切回 Unity 时看看网页有没有写进新版本。
        if (pack == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        SyncMyWorld(true);
        Repaint();
    }

    private void OnGUI()
    {
        if (pack == null && !EnsurePackForPlay()) NewDraft();
        DrawToolbar();
        DrawMyWorldBar();
        EditorGUILayout.HelpBox("世界工坊是唯一主创作入口：在这里编排会场、流程和探索；小镇地图、横版关卡是按需打开的子工具。玩家运行仅读取本次包，线索提示与作者真相分开显示。", MessageType.Info);
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUI.BeginChangeCheck();
        DrawPackFields();
        DrawVenueFields();
        DrawLinks();
        if (EditorGUI.EndChangeCheck())
        {
            Check();
            rehearsal = "草稿已修改，请重新彩排；探索需要在运行场景中实际移动和交互。";
        }
        DrawIssues();
        DrawRehearsal();
        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("新建草稿", EditorStyles.toolbarButton, GUILayout.Width(78))) NewDraft();
        if (GUILayout.Button("读取示例副本", EditorStyles.toolbarButton, GUILayout.Width(94))) LoadDemoCopy();
        if (GUILayout.Button("导入…", EditorStyles.toolbarButton, GUILayout.Width(58))) Import();
        if (GUILayout.Button("导出…", EditorStyles.toolbarButton, GUILayout.Width(58))) Export();
        GUILayout.Space(8);
        if (GUILayout.Button("地图子工具", EditorStyles.toolbarButton, GUILayout.Width(74))) OverworldWorkshopWindow.Open();
        if (GUILayout.Button("关卡子工具", EditorStyles.toolbarButton, GUILayout.Width(74))) LevelWorkshopWindow.Open();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("检查结构", EditorStyles.toolbarButton, GUILayout.Width(72))) Check();
        if (GUILayout.Button("开始世界", EditorStyles.toolbarButton, GUILayout.Width(72))) PlayCurrent(false);
        bool hasCheckpoint = false;
        try { hasCheckpoint = pack != null && WorldParkSaveStore.Exists(pack); } catch { hasCheckpoint = false; }
        using (new EditorGUI.DisabledScope(!hasCheckpoint))
            if (GUILayout.Button(new GUIContent("继续旅程", hasCheckpoint ? "从此世界版本最近的安全检查点继续" : "此世界版本还没有检查点；修改世界内容后旧检查点不会串用"), EditorStyles.toolbarButton, GUILayout.Width(72))) PlayCurrent(true);
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>S254「我的世界」一栏：与网页设计台同步的文件、保存 / 载入、打开网页、构建 Windows 版。</summary>
    private void DrawMyWorldBar()
    {
        bool exists = File.Exists(MyWorldFullPath);
        bool dirty = pack != null && WorldParkPack.ToJson(pack) != savedSnapshot;
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label((dirty ? "● 有未保存的改动　" : "") + "我的世界：" + (exists ? MyWorldAssetPath : "还没有（点右边“保存为我的世界”，或在网页设计台点“🔗 写进 Unity 项目”）"), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!exists))
                if (GUILayout.Button(new GUIContent("载入我的世界", "读取网页设计台写进来的（或上次保存的）世界"), EditorStyles.miniButtonLeft, GUILayout.Width(92))) LoadMyWorld();
            if (GUILayout.Button(new GUIContent("保存为我的世界", "写入 " + MyWorldAssetPath + "；网页设计台和 Windows 版构建都会用它"), EditorStyles.miniButtonMid, GUILayout.Width(100))) SaveMyWorld();
            if (GUILayout.Button(new GUIContent("网页设计台", "打开网页设计台：会场体检、模板、网页试玩"), EditorStyles.miniButtonMid, GUILayout.Width(78))) StartHereWindow.OpenWeb();
            if (GUILayout.Button(new GUIContent("构建 Windows 版", "把当前世界打包成双击就能玩的 exe（Builds/Windows/）"), EditorStyles.miniButtonRight, GUILayout.Width(104))) EditorApplication.delayCall += BuildWindowsCurrent;
        }
        if (!string.IsNullOrEmpty(myWorldNotice))
        {
            EditorGUILayout.HelpBox(myWorldNotice, MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("载入网页的版本（替换当前草稿）", GUILayout.Height(22))) LoadMyWorld();
                if (GUILayout.Button("保留当前草稿，并用它覆盖「我的世界」", GUILayout.Height(22))) SaveMyWorld();
                if (GUILayout.Button("先不管", GUILayout.Width(70), GUILayout.Height(22))) { MarkMyWorldSeen(); myWorldNotice = ""; }
            }
        }
    }

    // ───────────────────────── S254「我的世界」同步 ─────────────────────────

    public static string MyWorldFullPath => Path.Combine(Path.GetDirectoryName(Application.dataPath), MyWorldAssetPath);

    private static bool TryReadMyWorld(out WorldParkPack.Pack mine, out long ticks, out string error)
    {
        mine = null; ticks = 0; error = "";
        try
        {
            var info = new FileInfo(MyWorldFullPath);
            if (!info.Exists) { error = "还没有「我的世界」文件"; return false; }
            if (info.Length > WorldParkPack.MaxJsonLength * 4L) { error = "文件过大"; return false; }
            ticks = info.LastWriteTimeUtc.Ticks;
            mine = WorldParkPack.Parse(File.ReadAllText(info.FullName, System.Text.Encoding.UTF8), out error);
            if (mine == null) { if (string.IsNullOrEmpty(error)) error = "无法解析"; return false; }
            return true;
        }
        catch (Exception ex) { mine = null; error = ex.Message; return false; }
    }

    /// <summary>
    /// 对照「我的世界」文件：内容相同 → 记为已同步；文件比上次看到的新、且 Unity 草稿没有未保存改动（auto）→ 自动载入；
    /// 其余情况只在窗口里提示，绝不悄悄替换作者正在改的草稿。
    /// </summary>
    private void SyncMyWorld(bool auto)
    {
        myWorldNotice = "";
        if (!TryReadMyWorld(out var mine, out long ticks, out string error))
        {
            if (File.Exists(MyWorldFullPath)) myWorldNotice = "「我的世界」文件读不出来：" + error + "。Unity 草稿没有变。";
            return;
        }
        if (pack != null && WorldParkPack.ToJson(mine) == WorldParkPack.ToJson(pack)) { myWorldSeenTicks = ticks; return; }
        bool unmodified = pack == null || WorldParkPack.ToJson(pack) == savedSnapshot;
        if (auto && unmodified && ticks != myWorldSeenTicks)
        {
            UseMyWorld(mine, ticks, "已自动载入「我的世界」《" + (mine.name ?? mine.id) + "》（网页设计台写进来的新版本；Unity 草稿没有未保存的改动）。");
            return;
        }
        if (ticks == myWorldSeenTicks && unmodified) return; // 作者看过、并主动换成了别的世界：不反复提醒
        myWorldNotice = "网页设计台写进来的「我的世界」《" + (mine.name ?? mine.id) + "》和当前草稿不同" + (unmodified ? "。" : "；当前草稿有未保存的改动，所以没有自动替换。");
    }

    private void UseMyWorld(WorldParkPack.Pack mine, long ticks, string message)
    {
        pack = mine;
        ResetSpatialState();
        venueIndex = nodeIndex = clueIndex = 0;
        savedSnapshot = WorldParkPack.ToJson(pack);
        myWorldSeenTicks = ticks; myWorldNotice = "";
        Check();
        status = message;
        rehearsal = "已换成「我的世界」，请重新彩排。";
    }

    /// <summary>作者主动换了别的草稿（新建 / 示例 / 导入）：把当前文件版本记为已看过，只有网页再写新版本时才自动载入。</summary>
    private void MarkMyWorldSeen()
    {
        try { var info = new FileInfo(MyWorldFullPath); myWorldSeenTicks = info.Exists ? info.LastWriteTimeUtc.Ticks : 0; }
        catch { }
        myWorldNotice = "";
    }

    private void LoadMyWorld()
    {
        if (!TryReadMyWorld(out var mine, out long ticks, out string error)) { status = "没能载入「我的世界」：" + error + "。当前草稿未改变。"; return; }
        if (!ConfirmReplace()) return;
        UseMyWorld(mine, ticks, "已载入「我的世界」：" + MyWorldAssetPath);
    }

    private void SaveMyWorld()
    {
        if (pack == null) return;
        string path = MyWorldFullPath;
        try
        {
            if (TryReadMyWorld(out var mine, out long ticks, out _) && ticks != myWorldSeenTicks && WorldParkPack.ToJson(mine) != WorldParkPack.ToJson(pack)
                && !EditorUtility.DisplayDialog("覆盖「我的世界」？", "网页设计台在你上次载入之后又写进了《" + (mine.name ?? mine.id) + "》。\n用 Unity 当前草稿覆盖它吗？覆盖前的版本会备份到 " + MyWorldBackupPath + "。", "覆盖", "取消"))
            { status = "已取消，没有写入「我的世界」。"; return; }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path))
            {
                string backup = Path.Combine(Path.GetDirectoryName(Application.dataPath), MyWorldBackupPath);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(path, backup, true);
            }
            string json = WorldParkPack.ToJson(pack);
            File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
            savedSnapshot = json;
            myWorldSeenTicks = new FileInfo(path).LastWriteTimeUtc.Ticks; myWorldNotice = "";
            AssetDatabase.ImportAsset(MyWorldAssetPath);
            Check();
            status = "已保存为「我的世界」：" + MyWorldAssetPath + "。网页设计台点“🔗 写进 Unity 项目”会提示载入它；构建 Windows 版也默认用它。";
        }
        catch (Exception ex) { status = "保存「我的世界」失败：" + ex.Message + "。原文件未改动。"; }
    }

    // ───────────────────────── S254 会场：复制 / 模板 / 只玩这个会场 ─────────────────────────

    private static List<string> TemplateNames()
    {
        if (templateNames != null) return templateNames;
        templateNames = new List<string>();
        if (TryReadDemo(out var demo, out _)) foreach (var v in demo.venues) templateNames.Add((v.name ?? v.id) + " [" + v.id + "]");
        return templateNames;
    }

    /// <summary>用主世界“空白之城”的某个会场做模板：整份拷进当前草稿（新 id、名字加“（我的改编）”，与网页设计台同名规则）。</summary>
    private void AddFromTemplate(int index)
    {
        if (!TryReadDemo(out var demo, out string error) || index < 0 || index >= demo.venues.Count) { status = "模板读取失败：" + error; return; }
        var v = demo.venues[index];
        v.id = UniqueId(pack.venues.Select(x => x.id), v.id + "_my");
        v.name = (v.name ?? "会场") + "（我的改编）";
        pack.venues.Add(v);
        SelectVenue(pack.venues.Count - 1);
        ResetSpatialState();
        Check();
        status = "已用模板新建会场“" + v.name + "”[" + v.id + "]。它还没有门连到别的会场：在下方“会场链接”里接上，或直接点“▶ 只玩这个会场”试。";
    }

    private void DuplicateVenue()
    {
        var copy = WorldParkPack.Parse(WorldParkPack.ToJson(pack), out string error);
        if (copy == null || venueIndex >= copy.venues.Count) { status = "复制失败：" + error; return; }
        var v = copy.venues[venueIndex];
        v.id = UniqueId(pack.venues.Select(x => x.id), v.id + "_copy");
        v.name = (v.name ?? "会场") + "（副本）";
        pack.venues.Add(v);
        SelectVenue(pack.venues.Count - 1);
        ResetSpatialState();
        Check();
        status = "已复制为“" + v.name + "”[" + v.id + "]：大框架不动，放心在副本上改。";
    }

    private void PlayVenue(WorldParkPack.Venue venue)
    {
        Check();
        var single = WorldParkBuilder.WithEntry(pack, venue.id);
        if (WorldParkBuilder.TryRun(single, out string report, false)) status = "只玩“" + (venue.name ?? venue.id) + "”：" + report;
        else status = "未启动“" + (venue.name ?? venue.id) + "”：" + report;
    }

    private void DrawPackFields()
    {
        EditorGUILayout.LabelField("世界包", EditorStyles.boldLabel);
        pack.id = EditorGUILayout.TextField("包 ID", pack.id ?? "");
        pack.name = EditorGUILayout.TextField("名称", pack.name ?? "");
        pack.premise = EditorGUILayout.TextField("原创世界设定", pack.premise ?? "");
        pack.entryVenue = EditorGUILayout.TextField("入口会场 ID", pack.entryVenue ?? "");
        EditorGUILayout.LabelField("格式", WorldParkPack.TypeTag + " v" + WorldParkPack.Version);
        GUILayout.Space(6);
    }

    private void DrawVenueFields()
    {
        EnsureLists();
        EditorGUILayout.LabelField("会场", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (pack.venues.Count > 0)
        {
            int selected = EditorGUILayout.Popup("选择会场", venueIndex, pack.venues.Select(x => (x.name ?? "") + " [" + x.id + "]").ToArray());
            if (selected != venueIndex) SelectVenue(selected);
        }
        if (GUILayout.Button("+ 会场", GUILayout.Width(62)))
        {
            var added = NewVenue();
            added.id = UniqueId(pack.venues.Select(v => v.id), "venue");
            pack.venues.Add(added);
            SelectVenue(pack.venues.Count - 1);
        }
        if (pack.venues.Count > 1 && GUILayout.Button("删除会场", GUILayout.Width(72)))
        {
            pack.venues.RemoveAt(venueIndex);
            SelectVenue(Mathf.Clamp(venueIndex, 0, pack.venues.Count - 1));
        }
        EditorGUILayout.EndHorizontal();
        // S254：在已有的好框架上改——复制会场、用主世界会场当模板、单独试玩一个会场。
        EditorGUILayout.BeginHorizontal();
        if (pack.venues.Count > 0 && GUILayout.Button(new GUIContent("复制此会场", "整份复制当前会场（新 id），在副本上改"), GUILayout.Width(84))) { DuplicateVenue(); GUIUtility.ExitGUI(); }
        var names = TemplateNames();
        if (names.Count > 0)
        {
            templateIndex = Mathf.Clamp(templateIndex, 0, names.Count - 1);
            templateIndex = EditorGUILayout.Popup(templateIndex, names.ToArray(), GUILayout.Width(220));
            if (GUILayout.Button(new GUIContent("用模板新建会场", "把主世界“空白之城”的这个会场整份拷进来（名字加“（我的改编）”）"), GUILayout.Width(108))) { AddFromTemplate(templateIndex); GUIUtility.ExitGUI(); }
        }
        GUILayout.FlexibleSpace();
        var current = CurrentVenue();
        if (current != null && GUILayout.Button(new GUIContent("▶ 只玩这个会场", "从这个会场的入口直接开始（不用从入口会场一路玩过来；有单独的检查点）"), GUILayout.Width(110)))
        {
            var target = current;
            EditorApplication.delayCall += () => { if (this != null) PlayVenue(target); };
        }
        EditorGUILayout.EndHorizontal();

        var venue = CurrentVenue();
        if (venue == null) return;
        venue.id = EditorGUILayout.TextField("会场 ID", venue.id ?? "");
        venue.name = EditorGUILayout.TextField("名称", venue.name ?? "");
        venue.setting = EditorGUILayout.TextField("场景文字", venue.setting ?? "");
        venue.role = EditorGUILayout.TextField("角色（可运行 player / trickster）", venue.role ?? "");
        venue.suit = EditorGUILayout.TextField("自定义分类", venue.suit ?? "");
        venue.difficulty = EditorGUILayout.IntField("难度", venue.difficulty);
        venue.rules = EditorGUILayout.TextField("公开胜负条款", venue.rules ?? "");
        venue.entry = EditorGUILayout.TextField("会场入口节点 ID", venue.entry ?? "");
        DrawNodeFields(venue);
        DrawClueFields(venue);
    }

    private void DrawNodeFields(WorldParkPack.Venue venue)
    {
        EnsureVenueLists(venue);
        GUILayout.Space(8);
        EditorGUILayout.LabelField("节点（explore 是可实际移动、调查和选择出口的阶段）", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (venue.nodes.Count > 0)
        {
            int selected = EditorGUILayout.Popup("选择阶段", nodeIndex, venue.nodes.Select(x => (x.name ?? "") + " [" + x.id + "]").ToArray());
            if (selected != nodeIndex) nodeIndex = selected;
        }
        if (GUILayout.Button("+ 节点", GUILayout.Width(58)))
        {
            venue.nodes.Add(new WorldParkPack.Node { id = UniqueId(venue.nodes.Select(n => n.id), "explore"), kind = "explore", name = "新探索", text = "" });
            nodeIndex = venue.nodes.Count - 1;
        }
        if (venue.nodes.Count > 1 && GUILayout.Button("删除节点", GUILayout.Width(72)))
        {
            venue.nodes.RemoveAt(nodeIndex);
            nodeIndex = Mathf.Clamp(nodeIndex, 0, venue.nodes.Count - 1);
        }
        EditorGUILayout.EndHorizontal();

        if (venue.nodes.Count == 0) return;
        var node = venue.nodes[Mathf.Clamp(nodeIndex, 0, venue.nodes.Count - 1)];
        node.id = EditorGUILayout.TextField("节点 ID", node.id ?? "");
        node.kind = EditorGUILayout.TextField("种类 story / room / explore / game / reveal / ending / concept", node.kind ?? "");
        node.name = EditorGUILayout.TextField("显示名", node.name ?? "");
        node.text = EditorGUILayout.TextArea(node.text ?? "", GUILayout.MinHeight(42));
        if (string.Equals(node.kind, "room", StringComparison.OrdinalIgnoreCase))
        {
            node.room = EditorGUILayout.TextField("关卡库 / 内置样板房间名", node.room ?? "");
            node.onWin = EditorGUILayout.TextField("捣蛋者守宝成功 →", node.onWin ?? "");
            node.onLose = EditorGUILayout.TextField("马里奥拿宝成功 →", node.onLose ?? "");
        }
        else if (string.Equals(node.kind, "explore", StringComparison.OrdinalIgnoreCase))
        {
            DrawExploreFields(venue, node);
        }
        else if (string.Equals(node.kind, "game", StringComparison.OrdinalIgnoreCase))
        {
            DrawGameFields(node);
        }
        else
        {
            node.next = EditorGUILayout.TextField("继续 →", node.next ?? "");
        }
        if (string.Equals(node.kind, "ending", StringComparison.OrdinalIgnoreCase))
            node.result = EditorGUILayout.TextField(new GUIContent("结局标签", "clear/fail 兼容旧包；也可写 recorded、pass_with_ticket 等自定义标签，会场链接用同名条件解锁。any 保留给链接通配，不能作结局标签。"), node.result ?? "");
    }

    private void DrawGameFields(WorldParkPack.Node node)
    {
        if (node.game == null) node.game = new BorderlandGame.Def { mode = "beauty", rounds = 6, lives = 3, ratio = 80, seed = 1, escalate = 1, seats = new List<BorderlandGame.Seat> { new BorderlandGame.Seat { id = "me", name = "你", persona = "player" }, new BorderlandGame.Seat { id = "a", name = "对手", persona = "reasoner" } } };
        var g = node.game;
        if (g.seats == null) g.seats = new List<BorderlandGame.Seat>();
        GUILayout.Space(5);
        EditorGUILayout.HelpBox("博弈阶段在剧情场景中逐轮进行：玩家只看到公开信息（存活、追加规则、证词、承诺、公开记录）。整局结束才提交真实胜负；之后走 win/lose 分支。改完请运行剧情模拟（Tools/WorldParkVerify/Story）检验体验。", MessageType.None);
        int mode = Mathf.Max(0, Array.IndexOf(BorderlandGame.Modes, g.mode));
        g.mode = BorderlandGame.Modes[EditorGUILayout.Popup("玩法", mode, GameModeLabels)];
        g.rounds = EditorGUILayout.IntSlider("轮数", g.rounds, 1, 20);
        g.lives = EditorGUILayout.IntSlider("每人命数", g.lives, 1, 20);
        if (g.mode == "beauty") { g.ratio = EditorGUILayout.IntSlider("目标比例（平均×%）", g.ratio, 1, 99); g.escalate = EditorGUILayout.Toggle("有人出局后逐轮追加规则", g.escalate == 1) ? 1 : 0; }
        if (g.mode == "split") g.goal = EditorGUILayout.IntSlider("通关所需分数", g.goal, 1, 40);
        g.seed = EditorGUILayout.IntField("随机种子（同种子=同结果）", g.seed);
        EditorGUILayout.LabelField("参加者（必须恰好一位 player）", EditorStyles.miniBoldLabel);
        for (int i = 0; i < g.seats.Count; i++)
        {
            var s = g.seats[i];
            EditorGUILayout.BeginHorizontal();
            s.id = EditorGUILayout.TextField(s.id ?? "", GUILayout.Width(70));
            s.name = EditorGUILayout.TextField(s.name ?? "", GUILayout.Width(90));
            int p = Mathf.Max(0, Array.IndexOf(BorderlandGame.Personas, s.persona));
            s.persona = BorderlandGame.Personas[EditorGUILayout.Popup(p, PersonaLabels)];
            if (g.seats.Count > 2 && GUILayout.Button("×", GUILayout.Width(22))) { g.seats.RemoveAt(i); EditorGUILayout.EndHorizontal(); break; }
            EditorGUILayout.EndHorizontal();
        }
        if (g.seats.Count < 8 && GUILayout.Button("+ 参加者", GUILayout.Width(80))) g.seats.Add(new BorderlandGame.Seat { id = UniqueId(g.seats.Select(s => s.id), "npc"), name = "新参加者", persona = "reasoner" });
        foreach (var issue in BorderlandGame.Check(g)) EditorGUILayout.HelpBox(issue.text, issue.severity == "error" ? MessageType.Error : MessageType.Warning);
        node.onWin = EditorGUILayout.TextField("通过 →", node.onWin ?? "");
        node.onLose = EditorGUILayout.TextField("失败 →", node.onLose ?? "");
    }

    private void DrawExploreFields(WorldParkPack.Venue venue, WorldParkPack.Node node)
    {
        GUILayout.Space(5);
        EditorGUILayout.HelpBox("探索地图沿用小镇文本格式，但这是当前 explore 节点的内嵌副本。复制不会改写当前小镇；进入阶段不会自动发现 interact 线索。", MessageType.None);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("画当前会场地图", GUILayout.Width(156)))
        {
            if (string.IsNullOrWhiteSpace(node.mapText)) { var preview=WorldParkMainline.MapPreview(OverworldBuilder.CurrentText);node.mapText=preview.venues[0].nodes[0].mapText; }
            var target=node;
            OverworldWorkshopWindow.EditExplore(node.mapText,value=> { target.mapText=value;Check();rehearsal="地图已修改，请重新检查点位/彩排。";Repaint(); });
        }
        if (GUILayout.Button("从当前小镇复制地图文本", GUILayout.Width(176)))
        {
            var mapWindow=Resources.FindObjectsOfTypeAll<OverworldWorkshopWindow>().FirstOrDefault(w=>w.CurrentMap!=null);
            node.mapText = mapWindow!=null ? OverworldMap.ToText(mapWindow.CurrentMap) : OverworldBuilder.CurrentText ?? "";
            status = "已把当前小镇地图复制到探索节点“" + (node.id ?? "") + "”；原小镇没有被改写。";
        }
        EditorGUILayout.EndHorizontal();
        node.mapText = EditorGUILayout.TextArea(node.mapText ?? "", GUILayout.MinHeight(130));
        node.timeout = Mathf.Max(0, EditorGUILayout.IntField("倒计时（秒，0 = 无）", node.timeout));
        node.onWin = EditorGUILayout.TextField("从 win 出口 →", node.onWin ?? "");
        node.onLose = EditorGUILayout.TextField("从 lose 出口 →", node.onLose ?? "");
        if (spatialState == null) spatialState = new WorldParkSpatialEditor.State();
        WorldParkSpatialEditor.Draw(venue, node, spatialState, OnSpatialChanged);
    }

    private void DrawClueFields(WorldParkPack.Venue venue)
    {
        EnsureVenueLists(venue);
        GUILayout.Space(8);
        EditorGUILayout.LabelField("线索（提示对玩家可见；真相只在对应 reveal 且已发现后显示）", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (venue.clues.Count > 0)
        {
            int selected = EditorGUILayout.Popup("选择线索", clueIndex, venue.clues.Select(x => (x.name ?? "") + " [" + x.id + "]").ToArray());
            if (selected != clueIndex) clueIndex = selected;
        }
        if (GUILayout.Button("+ 线索", GUILayout.Width(58)))
        {
            venue.clues.Add(new WorldParkPack.Clue { id = UniqueId(venue.clues.Select(c => c.id), "clue"), name = "新线索", trigger = "interact" });
            clueIndex = venue.clues.Count - 1;
        }
        if (venue.clues.Count > 1 && GUILayout.Button("删除线索", GUILayout.Width(72)))
        {
            venue.clues.RemoveAt(clueIndex);
            clueIndex = Mathf.Clamp(clueIndex, 0, venue.clues.Count - 1);
        }
        EditorGUILayout.EndHorizontal();

        if (venue.clues.Count == 0) return;
        var clue = venue.clues[Mathf.Clamp(clueIndex, 0, venue.clues.Count - 1)];
        clue.id = EditorGUILayout.TextField("线索 ID", clue.id ?? "");
        clue.name = EditorGUILayout.TextField("名称", clue.name ?? "");
        clue.trigger = EditorGUILayout.TextField("发现方式 stage / interact", clue.trigger ?? "");
        clue.foundAt = EditorGUILayout.TextField("发现节点（interact 必须是 explore）", clue.foundAt ?? "");
        clue.revealAt = EditorGUILayout.TextField("揭示于节点", clue.revealAt ?? "");
        clue.hint = EditorGUILayout.TextField("玩家可见提示", clue.hint ?? "");
        clue.truth = EditorGUILayout.TextField("作者真相（运行时仅已发现并揭示后可见）", clue.truth ?? "");
    }

    private void DrawLinks()
    {
        EnsureLists();
        GUILayout.Space(8);
        EditorGUILayout.LabelField("会场后续入口", EditorStyles.boldLabel);
        for (int i = 0; i < pack.links.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            var link = pack.links[i];
            if (link == null) { link = new WorldParkPack.Link(); pack.links[i] = link; }
            link.from = EditorGUILayout.TextField(link.from ?? "", GUILayout.MinWidth(100));
            GUILayout.Label("→", GUILayout.Width(16));
            link.to = EditorGUILayout.TextField(link.to ?? "", GUILayout.MinWidth(100));
            GUILayout.Label("需要", GUILayout.Width(34));
            link.requires = EditorGUILayout.TextField(link.requires ?? "", GUILayout.MinWidth(70));
            if (GUILayout.Button("×", GUILayout.Width(25))) { pack.links.RemoveAt(i); GUIUtility.ExitGUI(); }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("+ 会场链接", GUILayout.Width(90))) pack.links.Add(new WorldParkPack.Link { requires = "clear" });
    }

    private void DrawIssues()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("结构检查", EditorStyles.boldLabel);
        if (issues == null || issues.Count == 0)
        {
            EditorGUILayout.HelpBox("未发现结构问题。运行前仍会检查当前可运行会场的角色、探索地图/点以及横版房间适配。", MessageType.Info);
            return;
        }
        issueScroll = EditorGUILayout.BeginScrollView(issueScroll, GUILayout.Height(Mathf.Min(170, 38 + issues.Count * 22)));
        foreach (var issue in issues)
        {
            bool error = string.Equals(issue.severity, "error", StringComparison.OrdinalIgnoreCase);
            string where = string.IsNullOrEmpty(issue.node) ? issue.venue : issue.venue + "/" + issue.node;
            EditorGUILayout.HelpBox("[" + issue.code + "] " + (string.IsNullOrEmpty(where) ? "" : where + "：") + issue.text, error ? MessageType.Error : MessageType.Warning);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawRehearsal()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("流程彩排", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("这是可复现的纯逻辑流程推演：story/reveal 自动继续，room 按下方胜负分支走。explore 不伪造物理或自动发现线索；彩排会在探索阶段停下，实际调查与出口选择必须在探索场景操作。", MessageType.None);
        EditorGUILayout.BeginHorizontal();
        rehearsalWin = GUILayout.Toggle(rehearsalWin, "捣蛋者守宝成功", "Button", GUILayout.Width(130));
        rehearsalWin = !GUILayout.Toggle(!rehearsalWin, "马里奥拿宝成功", "Button", GUILayout.Width(130));
        if (GUILayout.Button("执行纯逻辑彩排", GUILayout.Width(120))) Rehearse();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.TextArea(rehearsal ?? "", GUILayout.MinHeight(92));
    }

    private void NewDraft()
    {
        if (!ConfirmReplace()) return;
        var v = NewVenue();
        v.id = "venue_1";
        pack = new WorldParkPack.Pack { id = "my_world", name = "我的世界", premise = "在这里定义你的世界", entryVenue = v.id, venues = new List<WorldParkPack.Venue> { v } };
        ResetSpatialState();
        venueIndex = nodeIndex = clueIndex = 0;
        savedSnapshot = WorldParkPack.ToJson(pack);
        rehearsal = "尚未彩排；这是空白创作模板，不是内置示例。";
        Check();
        MarkMyWorldSeen();
        status = "新草稿尚未写入文件；点“保存为我的世界”（网页也能读），或“导出…”另存。";
    }

    private bool EnsurePackForPlay()
    {
        if (pack != null && pack.venues != null && pack.venues.Count > 0) return true;
        // S254：没有草稿时优先用「我的世界」（网页设计台写进来的），没有才用主世界示例。
        if (TryReadMyWorld(out var mine, out long ticks, out _) && mine.venues != null && mine.venues.Count > 0)
        {
            UseMyWorld(mine, ticks, "当前没有草稿，已载入「我的世界」：" + MyWorldAssetPath);
            return true;
        }
        if (!TryReadDemo(out var imported, out string error))
        {
            status = "无法读取开始世界所需的示例包：" + error;
            return false;
        }
        pack = imported;
        ResetSpatialState();
        venueIndex = nodeIndex = clueIndex = 0;
        savedSnapshot = WorldParkPack.ToJson(pack);
        MarkMyWorldSeen();
        status = "当前没有世界包草稿，已只读加载内置示例到窗口内存；未写回任何文件。";
        return true;
    }

    private void LoadDemoCopy()
    {
        if (!ConfirmReplace()) return;
        if (!TryReadDemo(out var imported, out string error))
        {
            status = "示例包尚不可用：" + error + "。当前草稿未改变。";
            return;
        }
        pack = imported;
        ResetSpatialState();
        venueIndex = nodeIndex = clueIndex = 0;
        savedSnapshot = WorldParkPack.ToJson(pack);
        MarkMyWorldSeen();
        status = "已读取示例为窗口内副本；不会写回示例文件。";
        Check();
    }

    private static bool TryReadDemo(out WorldParkPack.Pack imported, out string error)
    {
        imported = null;
        error = "";
        try
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(DemoAssetPath);
            if (text == null) { error = DemoAssetPath + " 不存在"; return false; }
            imported = WorldParkPack.Parse(text.text, out error);
            if (imported == null || !string.IsNullOrEmpty(error))
            {
                if (string.IsNullOrEmpty(error)) error = "示例包为空";
                imported = null;
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            imported = null;
            return false;
        }
    }

    private void Import()
    {
        string path = EditorUtility.OpenFilePanel("导入世界 JSON", Application.dataPath, "json");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if (new FileInfo(path).Length > WorldParkPack.MaxJsonLength * 4L) { status = "导入被拒绝：文件过大，当前草稿未改变。"; return; }
            string json = File.ReadAllText(path);
            var imported = WorldParkPack.Parse(json, out string error);
            if (imported == null || !string.IsNullOrEmpty(error))
            {
                status = "导入失败：" + error + "。当前编辑内容未改变。";
                return;
            }
            var parsedIssues = WorldParkPack.Validate(imported, WorldParkBuilder.RoomExists);
            var errors = parsedIssues.Where(i => string.Equals(i.severity, "error", StringComparison.OrdinalIgnoreCase)).ToList();
            if (errors.Count > 0)
            {
                string first = errors[0].code + "：" + errors[0].text;
                if (!EditorUtility.DisplayDialog("以待修复草稿导入？", "包有 " + errors.Count + " 个问题（首项 " + first + "）。可以保留创意继续编辑，但不代表可运行。", "作为草稿导入", "取消"))
                { status = "已取消，当前编辑内容未改变。"; return; }
            }
            if (!ConfirmReplace()) return;
            pack = imported;
            ResetSpatialState();
            venueIndex = nodeIndex = clueIndex = 0;
            savedSnapshot = WorldParkPack.ToJson(pack);
            issues = parsedIssues;
            MarkMyWorldSeen();
            status = "已导入“" + Path.GetFileName(path) + "”。问题见检查区；待修复草稿不代表可运行。";
        }
        catch (Exception ex) { status = "导入失败：" + ex.Message + "。当前编辑内容未改变。"; }
    }

    private void Export()
    {
        string defaultName = string.IsNullOrWhiteSpace(pack.id) ? "worldpark" : pack.id.Trim();
        string path = EditorUtility.SaveFilePanel("导出世界 JSON", Application.dataPath, defaultName + ".worldpark", "json");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if (BuiltInWorlds.Any(sample => string.Equals(Path.GetFullPath(path), Path.GetFullPath(Path.Combine(Application.dataPath, sample)), StringComparison.OrdinalIgnoreCase))) { status = "请另存为你的世界包；内置世界是网页和 Unity 共享资源，未覆盖它。"; return; }
            string json = WorldParkPack.ToJson(pack);
            File.WriteAllText(path, json);
            savedSnapshot = json;
            if (path.Replace('\\', '/').StartsWith(Application.dataPath.Replace('\\', '/') + "/", StringComparison.OrdinalIgnoreCase)) AssetDatabase.Refresh();
            Check();
            status = "已显式导出到：" + path + "。未覆盖示例包。";
        }
        catch (Exception ex) { status = "导出失败：" + ex.Message; }
    }

    private void Check()
    {
        try
        {
            EnsureLists();
            issues = WorldParkPack.Validate(pack, WorldParkBuilder.RoomExists) ?? new List<WorldParkPack.Issue>();
            int errors = issues.Count(i => string.Equals(i.severity, "error", StringComparison.OrdinalIgnoreCase));
            status = "结构检查完成：错误 " + errors + "，警告 " + (issues.Count - errors) + "。";
        }
        catch (Exception ex)
        {
            issues = new List<WorldParkPack.Issue>();
            status = "结构检查无法完成：" + ex.Message;
        }
    }

    private void OnSpatialChanged()
    {
        Check();
        rehearsal = "草稿已修改，请重新彩排；探索需要在运行场景中实际移动和交互。";
        Repaint();
    }

    private void ResetSpatialState()
    {
        if (spatialState == null) spatialState = new WorldParkSpatialEditor.State();
        else spatialState.Reset();
    }

    private void Rehearse()
    {
        Check();
        try
        {
            var session = WorldParkPack.Start(pack, out string error);
            if (session == null || !string.IsNullOrEmpty(error)) { rehearsal = "彩排无法开始：" + error; return; }
            var trace = new List<string>();
            for (int step = 0; step < 256; step++)
            {
                var node = session.Current;
                if (node == null) { trace.Add("停止：没有当前节点。" + session.error); break; }
                trace.Add((step + 1) + ". " + (session.venueId ?? "") + "/" + node.id + " [" + node.kind + "]");
                trace.Add("当前玩家可见：" + (node.text ?? ""));
                foreach (var clue in session.Venue.clues)
                {
                    string key = session.venueId + "/" + clue.id;
                    if (session.found.Contains(key)) trace.Add("已观察：" + clue.hint);
                    if (session.revealed.Contains(key)) trace.Add("重新解释：" + clue.truth);
                }
                if (string.Equals(node.kind, "room", StringComparison.OrdinalIgnoreCase))
                {
                    session.ResolveRoom(rehearsalWin, out error);
                    if (!string.IsNullOrEmpty(error)) { trace.Add("房间分支失败：" + error); break; }
                }
                else if (string.Equals(node.kind, "story", StringComparison.OrdinalIgnoreCase) || string.Equals(node.kind, "reveal", StringComparison.OrdinalIgnoreCase))
                {
                    session.Continue(out error);
                    if (!string.IsNullOrEmpty(error)) { trace.Add("继续失败：" + error); break; }
                }
                else if (string.Equals(node.kind, "explore", StringComparison.OrdinalIgnoreCase))
                {
                    trace.Add("停在探索：彩排不模拟移动、视线或交互；请在探索场景实际调查线索并选择出口。");
                    break;
                }
                else if (string.Equals(node.kind, "game", StringComparison.OrdinalIgnoreCase))
                {
                    trace.Add("停在博弈：彩排不替玩家做选择；请开始世界逐轮游玩，或运行剧情模拟让多种人格各打一遍。");
                    break;
                }
                else if (string.Equals(node.kind, "ending", StringComparison.OrdinalIgnoreCase))
                {
                    trace.Add("到达结局：" + (node.result ?? "") + "；本次内存结果：" + string.Join(", ", session.results.Select(kv => kv.Key + "=" + kv.Value)));
                    break;
                }
                else { trace.Add("停止：当前种类需要玩法适配器：" + node.kind); break; }
            }
            rehearsal = "纯逻辑彩排（固定“" + (rehearsalWin ? "守宝成功" : "马里奥拿宝成功") + "”）：\n" + string.Join("\n", trace);
        }
        catch (Exception ex) { rehearsal = "彩排异常：" + ex.Message; }
    }

    private void EnsureLists()
    {
        if (pack == null) return;
        if (pack.venues == null) pack.venues = new List<WorldParkPack.Venue>();
        if (pack.links == null) pack.links = new List<WorldParkPack.Link>();
        if (pack.venues.Count == 0) pack.venues.Add(NewVenue());
        venueIndex = Mathf.Clamp(venueIndex, 0, pack.venues.Count - 1);
        EnsureVenueLists(CurrentVenue());
    }

    private static void EnsureVenueLists(WorldParkPack.Venue venue)
    {
        if (venue == null) return;
        if (venue.nodes == null) venue.nodes = new List<WorldParkPack.Node>();
        if (venue.clues == null) venue.clues = new List<WorldParkPack.Clue>();
    }

    private WorldParkPack.Venue CurrentVenue()
    {
        if (pack == null || pack.venues == null || pack.venues.Count == 0) return null;
        venueIndex = Mathf.Clamp(venueIndex, 0, pack.venues.Count - 1);
        return pack.venues[venueIndex];
    }

    private void SelectVenue(int index)
    {
        venueIndex = index;
        nodeIndex = clueIndex = 0;
    }

    private bool ConfirmReplace()
    {
        return pack == null || WorldParkPack.ToJson(pack) == savedSnapshot || EditorUtility.DisplayDialog("保留你的草稿", "当前修改还没有保存（“保存为我的世界”或“导出…”）。仍要替换吗？", "替换", "取消");
    }

    private static string UniqueId(IEnumerable<string> ids, string prefix)
    {
        var used = new HashSet<string>(ids ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
        string id = prefix;
        int i = 2;
        while (used.Contains(id)) id = prefix + "_" + i++;
        return id;
    }

    private static WorldParkPack.Venue NewVenue()
    {
        return new WorldParkPack.Venue
        {
            id = "venue_new",
            entry = "intro",
            name = "新会场",
            role = "player",
            difficulty = 1,
            nodes = new List<WorldParkPack.Node>
            {
                new WorldParkPack.Node { id = "intro", kind = "story", name = "入场", next = "done" },
                new WorldParkPack.Node { id = "done", kind = "ending", name = "结束", result = "clear" }
            },
            clues = new List<WorldParkPack.Clue>()
        };
    }
}
