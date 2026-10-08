using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// S189：关卡工坊（菜单 MarioTrickster → Level Workshop，快捷键 Ctrl+Alt+W（S190：Ctrl+Shift+L 与 Unity 自带 Generate Lighting 冲突，已改））。
/// 参考 Super Mario Maker 2 的搭建方式：左边一排分类元素（地形 / 摆件 / 你的机关 / 目标 / 角色），点一下选中，
/// 在画布上点或拖着画；右键擦除；Alt 点吸取；Shift 拖出矩形；Ctrl+Z 撤销整笔。
/// 与项目规则的关系：
///   - 关卡源仍是 ASCII（LevelStudioDocument）。本窗口只改文字画布，不直接改场景；美术换图、验证、生成器全部照旧；
///   - 元素名称/说明/能否在第 1 步使用全部来自 ElementCatalog（唯一来源），新增元素自动出现在调色板；
///   - 每次改动自动跑：摆放规则 + L1 结构 + L2 可达 + 死局分析（拿宝往返、塌桥塌后能否出去），问题直接画在格子上；
///   - "最坏情况预览"：把所有塌桥塌掉 / 封路墙升起，死局格红色、暂时出不去的格黄色；
///   - "作为第 1 步房间试玩"：保存到 Assets/Levels/Step1CustomRoom.txt 并用恶作剧房间的全部规则（马里奥心智、问卷、随机、防卡死）开玩。
/// </summary>
public class LevelWorkshopWindow : EditorWindow
{
    private const string DraftKey = "MarioTrickster.Workshop.Draft";
    [SerializeField] private string source = "";
    [SerializeField] private char brush = '#';
    [SerializeField] private LevelWorkshopModel.Tool tool = LevelWorkshopModel.Tool.Brush;
    [SerializeField] private bool step1Mode = true;
    [SerializeField] private bool worstCase;
    [SerializeField] private float zoom = 22f;

    private LevelStudioDocument doc;
    [NonSerialized] private string parsedSource;
    private string parseError;
    private LevelWorkshopModel.CheckResult check;
    [NonSerialized] private string checkedSource;
    private Vector2 paletteScroll, canvasScroll, issueScroll;
    private bool painting, rectDragging, strokeStarted;
    private Vector2Int lastCell, rectStart, hoverCell = new Vector2Int(-1, -1);
    private int undoGroup;
    // S207：移动工具（点一个东西/框一块 → 拖过去；方向键微调；Ctrl+C/V 复制粘贴；Delete 清空）
    private LevelWorkshopModel.Sel? moveSel;
    private bool moveDragging, moveBoxing;
    private Vector2Int moveFrom;
    private List<(int dx, int dy, char c)> clipboard;
    // S208：起承转合分段框 / 模式印章 / 节奏 / 转移点（与网页设计台同规则：LevelBlueprint）
    [SerializeField] private bool showBeats = true;
    [SerializeField] private int[] beats;                 // 向导建的关卡用向导的分段；为空 = 按 M → 宝物平均切
    private LevelBlueprint.Pattern stampPattern;          // 非空 = 印章模式：点画布盖一次
    [NonSerialized] private string blueprintKey;
    private List<string> rhythmWarn = new List<string>();
    private List<(char c, int x, int y)> coverHints = new List<(char, int, int)>();
    private string rhythmSummary = "";
    private GUIStyle cellLabel, tileLabel;
    // S193：连招路线（缓存：只在网格变化时重算）
    private bool comboRoutes;
    // S196：箱庭总览
    private bool overview;
    [NonSerialized] private string hakoKey;
    private HakoniwaAnalyzer.Result hako;
    private Vector2 overviewScroll;
    private HakoniwaAnalyzer.Result Hako()
    {
        if (hakoKey != doc.Grid || hako == null) { hakoKey = doc.Grid; hako = HakoniwaAnalyzer.Analyze(Rows()); }
        return hako;
    }
    // S202：策略模拟（炸弹困人 → 自动加固格、路线时间线、离路线太远的机关）+ 自动检查轨迹热力图
    private bool strategy;
    [NonSerialized] private string strategyKey;
    private StrategySim.Report strategyResult;
    private StrategySim.Report Strategy()
    {
        // 高楼（11 层）一次约 5 秒：画的过程中不重算，停笔、完整检查跑完后才算（fullCheckStale = false）
        if ((strategyKey != doc.Grid || strategyResult == null) && (!fullCheckStale || strategyResult == null))
        {
            strategyKey = doc.Grid;
            var t = AssetDatabase.LoadAssetAtPath<MarioMindTuningSO>(Step1PrankRoomBuilder.TuningAssetPath);
            float speed = StrategySim.RunSpeed(9f, t != null ? t.marioSpeedScale : 0.55f);
            EditorUtility.DisplayProgressBar("策略模拟", "模拟最坏的对手用炸弹困住马里奥…", 0.5f);
            try { strategyResult = StrategySim.Analyze(Rows(), speed, t != null ? t.startDelaySeconds : 4f, t != null ? t.bombsPerRound : 3, t != null ? t.bombRadius : 1.6f, 1.5f, 3f, t != null ? t.oilRadius : 1.8f); }
            finally { EditorUtility.ClearProgressBar(); }
        }
        return strategyResult;
    }
    private bool showTrack;
    [NonSerialized] private Dictionary<int, int> trackVisits;
    [NonSerialized] private HashSet<int> trackStuck;
    [NonSerialized] private string trackNote = "";
    private void LoadTrack()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath) ?? ".", Step1PlaytestLog.LogFolder, Step1HandsOffCheck.TrackFile);
        if (!System.IO.File.Exists(path)) { trackVisits = null; trackNote = "还没有轨迹：先跑一次 测试中心 🤖 马里奥自己跑 或 🎯 陷阱试探"; return; }
        var (room, visits, stuck) = Step1HandsOffCheck.ParseTrack(System.IO.File.ReadAllText(path));
        trackVisits = visits; trackStuck = stuck;
        trackNote = room == StrategySim.Hash(doc.Grid) ? $"轨迹：{visits.Count} 格，卡住点 {stuck.Count} 个" : "⚠ 轨迹来自另一张图（先把这张图作为第 1 步房间试玩/检查一次）";
    }
    [NonSerialized] private string comboKey;
    private ComboRouteAnalyzer.Result comboResult;
    private ComboRouteAnalyzer.Result ComboResult()
    {
        if (comboKey != doc.Grid || comboResult == null)
        {
            comboKey = doc.Grid;
            var tuning = AssetDatabase.LoadAssetAtPath<MarioMindTuningSO>(Step1PrankRoomBuilder.TuningAssetPath);
            comboResult = ComboRouteAnalyzer.Analyze(Rows(), tuning != null ? tuning.comboRouteCells : 10f);
        }
        return comboResult;
    }
    // S192 性能：
    //  - 画的时候只跑"快速检查"（摆放规则，~1ms），停笔 0.35 秒后再在后台节拍里跑完整检查（死局/结构/全部随机组合）；
    //  - 画布只在鼠标换格子时重画，不是每个像素移动都重画；
    //  - GUIStyle 全部缓存（原来每个格子每帧 new 一个）；网格行只在内容变化时拆分一次。
    private const double FullCheckDelay = 0.35;
    [NonSerialized] private double fullCheckAt = -1;
    [NonSerialized] private bool fullCheckStale;
    [NonSerialized] private string[] rowsCache = new string[0];
    [NonSerialized] private string rowsSource;
    private GUIStyle glyphDark, glyphLight, nameNormal, nameSelected;
    [NonSerialized] private IList<string> shownCache;
    [NonSerialized] private string shownKey;

    [MenuItem("MarioTrickster/Level Workshop (关卡工坊) %&w", false, 1)]
    public static void Open()
    {
        var w = GetWindow<LevelWorkshopWindow>("关卡工坊");
        w.minSize = new Vector2(900, 560);
        w.Show();
    }

    /// <summary>S212：从小镇工坊的"✎ 编辑房间"跳过来：打开这个门连的房间（关卡库优先，其次内置样板）。改完"存入关卡库"同名覆盖，回小镇 ▶ 时自动重建。</summary>
    public static bool OpenRoom(string name)
    {
        name = (name ?? "").Trim();
        string text = null;
        foreach (var (n, path, _) in LevelLibrary.List()) if (n == name) { text = File.ReadAllText(path); break; }
        if (text == null) { var rows = OverworldBuilder.ResolveRoom(name); if (rows != null) text = string.Join("\n", rows); }
        if (text == null) return false;
        Open();
        var w = GetWindow<LevelWorkshopWindow>("关卡工坊");
        w.SetSource(text, "Load room " + name);
        w.libraryName = name;
        StartHereWindow.Touch(RecentWork.Room, name);
        w.ShowNotification(new GUIContent("门连的房间：" + name + "（改完 → 关卡库 → 存入，同名覆盖）"));
        return true;
    }

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(source)) source = EditorPrefs.GetString(DraftKey, "");
        if (string.IsNullOrEmpty(source)) source = string.Join("\n", Step1PrankRoomBuilder.Current);
        Undo.undoRedoPerformed += OnUndo;
        EditorApplication.update += Tick;
        WebSync.Imported -= OnWebImported; WebSync.Imported += OnWebImported;
        wantsMouseMove = true;
    }

    private void OnDisable() { Undo.undoRedoPerformed -= OnUndo; EditorApplication.update -= Tick; WebSync.Imported -= OnWebImported; }

    // ── S214：关卡库切换 + 网页同步 ─────────────────
    /// <summary>网页改了正在打开的这一关 → 自动换成新版本（Ctrl+Z 可以退回你在 Unity 里的版本）。</summary>
    private void OnWebImported(List<string> levels, List<string> towns, string report)
    {
        if (string.IsNullOrEmpty(libraryName) || !levels.Contains(libraryName)) { ShowNotification(new GUIContent($"网页同步：收到 {levels.Count} 关 {towns.Count} 个小镇")); return; }
        var e = LevelLibrary.List().Find(l => l.name == libraryName);
        if (e.path == null) return;
        string text = File.ReadAllText(e.path);
        if (LevelWorkshopModel.SameGrid(text, source)) return;
        SetSource(text, "Web sync " + libraryName);
        ShowNotification(new GUIContent("网页刚改了「" + libraryName + "」→ 已换成新版本（Ctrl+Z 退回）"));
        Repaint();
    }

    /// <summary>◀ ▶：按关卡库顺序切到上一关 / 下一关（先把当前这一关存好，不丢改动）。</summary>
    private void StepLibrary(int dir)
    {
        var list = LevelLibrary.List();
        if (list.Count == 0) { ShowNotification(new GUIContent("关卡库还是空的：先 关卡库 ▾ → 存入")); return; }
        if (!string.IsNullOrEmpty(libraryName)) QuickSave(false);
        int i = list.FindIndex(l => l.name == libraryName);
        int n = i < 0 ? (dir > 0 ? 0 : list.Count - 1) : (i + dir + list.Count) % list.Count;
        SetSource(File.ReadAllText(list[n].path), "Open " + list[n].name);
        libraryName = list[n].name;
        StartHereWindow.Touch(RecentWork.Room, libraryName);
        ShowNotification(new GUIContent($"{n + 1}/{list.Count}  {libraryName}"));
    }

    /// <summary>Ctrl+S：已经是关卡库里的关 → 直接同名存（不弹窗）；新画的 → 问名字。</summary>
    private void QuickSave(bool notify = true)
    {
        if (string.IsNullOrEmpty(libraryName)) { SaveToLibrary(); return; }
        var e = LevelLibrary.List().Find(l => l.name == libraryName);
        if (e.path != null && LevelWorkshopModel.SameGrid(File.ReadAllText(e.path), source)) { if (notify) ShowNotification(new GUIContent("没有改动")); return; }
        SaveAs(libraryName);
        if (notify) ShowNotification(new GUIContent("已存 「" + libraryName + "」（网页连接了项目文件夹的话，切回网页就能看到）"));
    }

    /// <summary>停笔一小会儿后再跑完整检查（不在画的过程中跑）。</summary>
    private void Tick()
    {
        if (fullCheckAt < 0 || painting || rectDragging) return;
        if (EditorApplication.timeSinceStartup < fullCheckAt) return;
        fullCheckAt = -1;
        if (doc == null) return;
        checkedSource = doc.Grid + step1Mode;
        check = LevelWorkshopModel.Check(Rows(), step1Mode, AsciiElementRegistry.GetDefault().IsSolid);
        fullCheckStale = false;
        Repaint();
    }

    private string[] Rows()
    {
        string grid = doc.Grid;
        if (rowsSource != grid) { rowsSource = grid; rowsCache = grid.Split('\n'); }
        return rowsCache;
    }
    private void OnUndo() { parsedSource = null; Save(); Repaint(); }
    private void Save() => EditorPrefs.SetString(DraftKey, source ?? "");

    private void Parse()
    {
        if (parsedSource == source && doc != null) return;
        parsedSource = source;
        LevelStudioDocument.TryParse(source, out doc, out parseError);
    }

    private void RunCheck()
    {
        if (doc == null) return;
        string key = doc.Grid + step1Mode;
        if (checkedSource == key || (fullCheckStale && fullCheckAt >= 0)) return;
        if (check == null) { checkedSource = key; check = LevelWorkshopModel.Check(Rows(), step1Mode, AsciiElementRegistry.GetDefault().IsSolid); return; }
        // 有改动：先给快速结果（摆放问题立刻标红），完整检查延后
        var quick = LevelWorkshopModel.QuickCheck(Rows(), step1Mode, AsciiElementRegistry.GetDefault().IsSolid);
        quick.deadlock = check.deadlock; quick.temporary = check.temporary; // 旧的死局图先保留，避免闪烁
        check = quick;
        fullCheckStale = true;
        fullCheckAt = EditorApplication.timeSinceStartup + FullCheckDelay;
    }

    private void SetSource(string text, string op)
    {
        if (text == source) return;
        Undo.RecordObject(this, op);
        // S208：换了一张图（新建/载入/导入/监狱塔）→ 起承转合分段回到"按 M → 宝物平均切"；盖章/移动/改尺寸不动
        if (op.StartsWith("Load") || op.StartsWith("Import") || op == "New room" || op == "Prison tower" || op == "Add floor") beats = null;
        blueprintKey = null;
        source = text;
        parsedSource = null;
        Save();
    }

    private void OnGUI()
    {
        if (cellLabel == null)
        {
            cellLabel = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            tileLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 10 };
            glyphDark = new GUIStyle(cellLabel) { normal = { textColor = new Color(0.08f, 0.08f, 0.1f) } };
            glyphLight = new GUIStyle(cellLabel) { normal = { textColor = Color.white } };
            nameNormal = new GUIStyle(tileLabel) { normal = { textColor = new Color(0.92f, 0.92f, 0.92f) } };
            nameSelected = new GUIStyle(tileLabel) { normal = { textColor = new Color(0.1f, 0.1f, 0.1f) } };
        }
        Parse();
        // S207：工具快捷键（和网页设计台一致）：B 画笔 R 矩形 E 橡皮 I 吸管 M 移动
        var ke = Event.current;
        if (ke.type == EventType.KeyDown && !EditorGUIUtility.editingTextField)
        {
            if ((ke.control || ke.command) && ke.keyCode == KeyCode.S) { QuickSave(); ke.Use(); }
            else if (ke.keyCode == KeyCode.PageUp) { StepLibrary(-1); ke.Use(); }
            else if (ke.keyCode == KeyCode.PageDown) { StepLibrary(1); ke.Use(); }
        }
        if (ke.type == EventType.KeyDown && !ke.control && !ke.command && !ke.alt && !EditorGUIUtility.editingTextField)
        {
            var map = new Dictionary<KeyCode, LevelWorkshopModel.Tool> { { KeyCode.B, LevelWorkshopModel.Tool.Brush }, { KeyCode.R, LevelWorkshopModel.Tool.Rect }, { KeyCode.E, LevelWorkshopModel.Tool.Erase }, { KeyCode.I, LevelWorkshopModel.Tool.Pick }, { KeyCode.M, LevelWorkshopModel.Tool.Move } };
            if (map.TryGetValue(ke.keyCode, out var t)) { tool = t; moveSel = null; ke.Use(); }
        }
        DrawToolbar();
        if (doc == null)
        {
            EditorGUILayout.HelpBox(parseError ?? "画布为空", MessageType.Warning);
            if (GUILayout.Button("新建空房间", GUILayout.Height(30))) SetSource(LevelWorkshopModel.NewRoom(48, 12), "New room");
            return;
        }
        RunCheck();
        EditorGUILayout.BeginHorizontal();
        DrawPalette();
        EditorGUILayout.BeginVertical();
        DrawCanvas();
        DrawStatus();
        EditorGUILayout.EndVertical();
        if (overview) DrawOverviewPanel();
        EditorGUILayout.EndHorizontal();
    }

    // ── 顶部工具条 ────────────────────────────────────
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("新建", EditorStyles.toolbarButton, GUILayout.Width(44)) &&
            EditorUtility.DisplayDialog("新建房间", "替换当前画布？（Ctrl+Z 可撤销）", "新建", "取消"))
            SetSource(LevelWorkshopModel.NewRoom(48, 12), "New room");
        if (GUILayout.Button(new GUIContent("向导…", "S208 新建关卡向导：选主角机关和时长 → 自动铺好起承转合 4 段（生成后已经能玩），和网页设计台一样"), EditorStyles.toolbarDropDown, GUILayout.Width(56))) WizardMenu();
        if (GUILayout.Button(new GUIContent("印章 ▾", "S208 模式印章：选一个，再点画布（马里奥站的那格）盖上一组现成的机关；右键 / Esc 退出"), EditorStyles.toolbarDropDown, GUILayout.Width(56))) StampMenu();
        if (GUILayout.Button("载入恶作剧房间", EditorStyles.toolbarButton, GUILayout.Width(96)))
            SetSource(string.Join("\n", Step1PrankRoomBuilder.Room), "Load prank room");
        if (GUILayout.Button(new GUIContent("样板：两层监狱", "纵向逃脱示例：地下拿宝、爬回地面；裂缝地板 x + 弹簧板 J"), EditorStyles.toolbarButton, GUILayout.Width(96)))
            SetSource(string.Join("\n", LevelWorkshopModel.PrisonSample), "Load prison sample");
        if (GUILayout.Button(new GUIContent("样板：箱庭监狱", "手工设计的四层地下监狱：每层一个身份，层间多条路，有单向捷径门、秘密裂墙竖井、裂缝地板"), EditorStyles.toolbarButton, GUILayout.Width(96)))
        { SetSource(string.Join("\n", LevelWorkshopModel.HakoniwaSample), "Load hakoniwa sample"); overview = true; }
        if (GUILayout.Button(new GUIContent("样板：诱捕走廊", "练习'以身入局'：一条排好的连锁（绊线→香蕉皮→火+油桶→塌桥→弹簧→封路墙）。试玩时 Shift+F 一键编号，再按 T 挑衅把他引过来"), EditorStyles.toolbarButton, GUILayout.Width(96)))
        { SetSource(string.Join("\n", LevelWorkshopModel.LureSample), "Load lure sample"); comboRoutes = true; }
        if (GUILayout.Button(new GUIContent("样板：长廊远征", "S207 大房间示范（94×15）：两段诱捕走廊连起来。游戏里镜头跟着你走（死亡细胞式），马里奥在屏外时边缘有红箭头 + 右上角小地图"), EditorStyles.toolbarButton, GUILayout.Width(96)))
            SetSource(string.Join("\n", LevelWorkshopModel.LongHallSample), "Load long hall sample");
        if (GUILayout.Button(new GUIContent("监狱塔…", "按层数自动拼一座监狱塔：每层是手工楼层模板，楼梯口左右交替；宝物在最底层，出口在顶层。拼完自动做死局检查"), EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            var menu = new GenericMenu();
            for (int f = 2; f <= FloorStacker.MaxFloors; f++)
            {
                int floors = f;
                menu.AddItem(new GUIContent($"{floors} 层（每层不同主题 + 捷径竖井）"), false, () =>
                {
                    var grid = FloorStacker.Build(floors, UnityEngine.Random.Range(0, 100000), 32, out var names);
                    SetSource(string.Join("\n", grid), "Prison tower");
                    overview = true;
                    ShowNotification(new GUIContent(string.Join(" / ", names)));
                });
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("在当前房间上面加一层"), false, () => SetSource(string.Join("\n", FloorStacker.AddFloorOnTop(Rows())), "Add floor"));
            menu.ShowAsContext();
        }
        if (GUILayout.Button(new GUIContent("关卡库 ▾", "你存下来的所有关卡（Assets/Levels/Library）：打开 / 存入 / 导入网页关卡包"), EditorStyles.toolbarDropDown, GUILayout.Width(66))) LibraryMenu();
        // S214：关卡切换 ◀ ▶（PageUp / PageDown）+ 当前是哪一关
        if (GUILayout.Button(new GUIContent("◀", "上一关（PageUp）：先自动存好当前这一关"), EditorStyles.toolbarButton, GUILayout.Width(22))) StepLibrary(-1);
        GUILayout.Label(new GUIContent(string.IsNullOrEmpty(libraryName) ? "（未存入关卡库）" : libraryName, "Ctrl+S 存；网页连接了项目文件夹时，两边改动自动同步"), EditorStyles.toolbarButton, GUILayout.MaxWidth(120));
        if (GUILayout.Button(new GUIContent("▶", "下一关（PageDown）"), EditorStyles.toolbarButton, GUILayout.Width(22))) StepLibrary(1);
        if (GUILayout.Button(new GUIContent("🏘 小镇", "S210：打开小镇工坊（星露谷视角大地图，门连到这里做的房间）"), EditorStyles.toolbarButton, GUILayout.Width(54))) OverworldWorkshopWindow.Open();
        if (GUILayout.Button(new GUIContent("📖", "开始页（Ctrl+Alt+H）：全部功能在哪、创作时怎么用"), EditorStyles.toolbarButton, GUILayout.Width(26))) StartHereWindow.Open();
        if (GUILayout.Button("导入", EditorStyles.toolbarButton, GUILayout.Width(44))) Import();
        if (GUILayout.Button("导出", EditorStyles.toolbarButton, GUILayout.Width(44))) Export();
        GUILayout.Space(10);
        var newTool = (LevelWorkshopModel.Tool)GUILayout.Toolbar((int)tool, new[]
        {
            new GUIContent("✎ 画笔", "点/拖着画当前选中的元素（B）"), new GUIContent("▭ 矩形", "拖出一个矩形，整块填满（R）"),
            new GUIContent("⌫ 橡皮", "擦成空气（E）；任何工具下右键也是擦"),
            new GUIContent("⊙ 吸管", "点画布上的格子 = 把它设成画笔，然后自动回到画笔（I；任何时候 Alt+点 也一样）"),
            new GUIContent("✥ 移动", "点一个东西（塌桥/台面这类连成一片的会整段选中）或在空处拖出框 → 拖过去；方向键微调、Ctrl+C/V 复制粘贴、Delete 清空（M）"),
        }, EditorStyles.toolbarButton, GUILayout.Width(330));
        if (newTool != tool) { tool = newTool; moveSel = null; }
        GUILayout.Space(10);
        step1Mode = GUILayout.Toggle(step1Mode, new GUIContent("第 1 步规则", "只显示/允许第 1 步恶作剧房间能用的元素，并按第 1 步规则检查"), EditorStyles.toolbarButton, GUILayout.Width(80));
        overview = GUILayout.Toggle(overview, new GUIContent("箱庭总览", "左侧显示每层的身份（主机关/藏身处）、层间连接（楼梯/捷径/秘密）、环路与捷径省下的步数，并在画布上标出楼层分隔与连接点；同时自动缩放到能看见整张图"), EditorStyles.toolbarButton, GUILayout.Width(66));
        strategy = GUILayout.Toggle(strategy, new GUIContent("策略模拟", "① 最坏的对手用 3 颗炸弹能不能把马里奥困死（能 → 标出会被自动加固的承重格，游戏里画铆钉）② 马里奥一趟路线的时间线：几秒到哪个机关 ③ 离路线太远、只能靠引诱才用得上的机关"), EditorStyles.toolbarButton, GUILayout.Width(66));
        bool newTrack = GUILayout.Toggle(showTrack, new GUIContent("检查轨迹", "显示最近一次自动检查 / 陷阱试探里马里奥走过的格子（越亮走得越多）和卡住点（红叉）"), EditorStyles.toolbarButton, GUILayout.Width(66));
        if (newTrack && !showTrack) LoadTrack();
        showTrack = newTrack;
        comboRoutes = GUILayout.Toggle(comboRoutes, new GUIContent("连招路线", "把离得够近、能在连招窗口内依次坑到马里奥的机关连成线；一组线 = 一套连招。种类越多越好"), EditorStyles.toolbarButton, GUILayout.Width(66));
        showBeats = GUILayout.Toggle(showBeats, new GUIContent("起承转合", "金色分段 = 起（教）→ 承（加深）→ 转（意外）→ 合（收尾）；橙框 = 转移点提示（机关 5 格内没有草丛/箱子，你走过去会被看见）"), EditorStyles.toolbarButton, GUILayout.Width(62));
        worstCase = GUILayout.Toggle(worstCase, new GUIContent("最坏情况预览", "所有塌桥塌掉、裂缝地板碎掉、封路墙升起时：红 = 死局（出不去），黄 = 暂时出不去"), EditorStyles.toolbarButton, GUILayout.Width(90));
        GUILayout.FlexibleSpace();
        zoom = GUILayout.HorizontalSlider(zoom, 12f, 36f, GUILayout.Width(90));
        EditorGUILayout.EndHorizontal();
    }

    // ── 左侧调色板（Mario Maker 式分类）──────────────────
    private void DrawPalette()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(236));
        paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll);
        var reg = AsciiElementRegistry.GetDefault();
        foreach (var group in PaletteCached())
        {
            EditorGUILayout.LabelField(group.title, EditorStyles.boldLabel);
            DrawTiles(group.items.Select(i => (i.ch, i.zh, $"{i.zh} {i.en}\n{i.what}\n摆放：{i.place}\n美术：{ArtHint(i)}", ElementCatalog.EditorColor(i.ch))).ToList());
            if (group.items.Count > 0 && group.items[0].role == ElementCatalog.Role.Scenery)
                DrawTiles(LevelWorkshopModel.RandomSlots.Select(s => (s.ch, s.zh, s.zh + "\n每局随机决定（检查会把所有组合都查一遍）", ElementCatalog.EditorColor(s.ch))).ToList());
        }
        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox("左键画 · 右键擦 · Alt+点击吸取\nShift+拖 = 矩形 · Ctrl+Z 撤销整笔", MessageType.None);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private List<LevelWorkshopModel.Group> paletteCache;
    private bool paletteMode;
    private List<LevelWorkshopModel.Group> PaletteCached()
    {
        if (paletteCache == null || paletteMode != step1Mode) { paletteCache = LevelWorkshopModel.Palette(step1Mode); paletteMode = step1Mode; }
        return paletteCache;
    }

    private void DrawTiles(List<(char ch, string name, string tip, Color color)> tiles)
    {
        const int perRow = 3;
        for (int i = 0; i < tiles.Count; i += perRow)
        {
            EditorGUILayout.BeginHorizontal();
            for (int k = i; k < Math.Min(i + perRow, tiles.Count); k++)
            {
                var t = tiles[k];
                Rect r = GUILayoutUtility.GetRect(72, 46, GUILayout.Width(72), GUILayout.Height(46));
                bool selected = brush == t.ch && tool != LevelWorkshopModel.Tool.Erase;
                EditorGUI.DrawRect(r, selected ? new Color(1f, 0.85f, 0.3f) : new Color(0.18f, 0.18f, 0.2f));
                var inner = new Rect(r.x + 3, r.y + 3, r.width - 6, 20);
                var c = t.color; c.a = 1f;
                EditorGUI.DrawRect(inner, c);
                DrawOutline(inner, new Color(0f, 0f, 0f, 0.6f), 1f);
                Glyph(inner, t.ch.ToString(), c);
                // 名称永远写在深色底上（不随元素颜色变），任何颜色都看得清
                GUI.Label(new Rect(r.x, r.y + 23, r.width, 22), new GUIContent(t.name, t.tip), selected ? nameSelected : nameNormal);
                if (GUI.Button(r, new GUIContent("", t.tip), GUIStyle.none))
                { brush = t.ch; if (tool == LevelWorkshopModel.Tool.Erase || tool == LevelWorkshopModel.Tool.Pick || tool == LevelWorkshopModel.Tool.Move) { tool = LevelWorkshopModel.Tool.Brush; moveSel = null; } }
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private static Color ColorOf(AsciiElementRegistry reg, char c) => ElementCatalog.EditorColor(c);

    /// <summary>按底色亮度自动用黑字或白字（亮色块上白字看不清的问题）。</summary>
    private static readonly string[] glyphText = BuildGlyphText();
    private static string[] BuildGlyphText() { var a = new string[128]; for (int i = 0; i < 128; i++) a[i] = ((char)i).ToString(); return a; }
    private readonly Dictionary<char, bool> darkGlyph = new Dictionary<char, bool>();

    private void Glyph(Rect r, string text, Color bg)
    {
        GUI.Label(r, text, ElementCatalog.TextColorOn(bg).r < 0.5f ? glyphDark : glyphLight);
    }

    /// <summary>画布格子的字：字符串与黑白判断都缓存（原来每格每帧新建 GUIStyle + 字符串）。</summary>
    private void GlyphFor(Rect r, char ch, Color bg)
    {
        if (!darkGlyph.TryGetValue(ch, out bool dark)) darkGlyph[ch] = dark = ElementCatalog.TextColorOn(bg).r < 0.5f;
        GUI.Label(r, ch < 128 ? glyphText[ch] : ch.ToString(), dark ? glyphDark : glyphLight);
    }

    private static string ArtHint(ElementCatalog.Info i)
    {
        switch (i.fit)
        {
            case ElementCatalog.ArtFit.Tile: return "平铺（" + ElementCatalog.SuggestedPixels(i.ch) + "）";
            case ElementCatalog.ArtFit.Fit: return "等比放进格子，建议 " + ElementCatalog.SuggestedPixels(i.ch) + " 像素";
            case ElementCatalog.ArtFit.Stretch: return "拉伸填满，建议 " + ElementCatalog.SuggestedPixels(i.ch) + " 像素";
            default: return "不需要图";
        }
    }

    // ── 画布 ─────────────────────────────────────────
    private void DrawCanvas()
    {
        float size = zoom;
        // S196：箱庭总览时自动缩放到能看见整张图（纵览全局）；关掉后回到手动缩放
        if (overview)
        {
            float availW = Mathf.Max(200f, position.width - 260f - 260f), availH = Mathf.Max(150f, position.height - 190f);
            size = Mathf.Clamp(Mathf.Min(availW / doc.Width, availH / doc.Height), 8f, zoom);
        }
        var reg = AsciiElementRegistry.GetDefault();
        canvasScroll = EditorGUILayout.BeginScrollView(canvasScroll, GUILayout.ExpandHeight(true));
        Rect canvas = GUILayoutUtility.GetRect(doc.Width * size, doc.Height * size, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
        Event e = Event.current;
        int control = GUIUtility.GetControlID("LevelWorkshopGrid".GetHashCode(), FocusType.Passive);

        // 最坏情况预览的网格
        string sk = doc.Grid + worstCase;
        if (shownKey != sk || shownCache == null || shownCache.Count != doc.Height) // S197：进出 Play 模式后缓存丢失 → 重建（原来会空白 + NullReference）
        {
            shownKey = sk;
            IList<string> rows = Rows();
            shownCache = worstCase ? LevelDeadlockAnalyzer.ApplyPrankState(LevelDeadlockAnalyzer.ApplyPrankState(rows, LevelDeadlockAnalyzer.PersistentOpeners, '.'), '[', 'W') : rows;
        }
        IList<string> shown = shownCache;

        if (e.type == EventType.Repaint)
        {
            int h = doc.Height;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < doc.Width; x++)
                {
                    char ch = shown[h - 1 - y][x];
                    Rect cell = CellRect(canvas, x, y, size);
                    Color bg = CellColor(ch);
                    EditorGUI.DrawRect(cell, bg);
                    if (ch != '.' && ch != '#' && ch != 'W' && size >= 14) GlyphFor(cell, ch, bg);
                }
            if (check != null)
            {
                foreach (int key in worstCase ? check.temporary : new HashSet<int>()) Tint(canvas, key / 100000, key % 100000, size, new Color(1f, 0.85f, 0.2f, 0.45f));
                foreach (int key in worstCase ? check.deadlock : new HashSet<int>()) Tint(canvas, key / 100000, key % 100000, size, new Color(1f, 0.15f, 0.15f, 0.55f));
                foreach (var issue in check.cells)
                {
                    Rect r = CellRect(canvas, issue.x, issue.y, size);
                    DrawOutline(r, issue.error ? new Color(1f, 0.2f, 0.2f) : new Color(1f, 0.8f, 0.2f), 2f);
                }
            }
            if (comboRoutes) DrawComboRoutes(canvas, size);
            if (strategy) DrawStrategy(canvas, size);
            if (showTrack) DrawTrack(canvas, size);
            if (overview) DrawHakoniwa(canvas, size);
            if (showBeats) DrawBlueprint(canvas, size);
            if (stampPattern != null && hoverCell.x >= 0) DrawStampPreview(canvas, size);
            if (tool == LevelWorkshopModel.Tool.Move && moveSel.HasValue)
            {
                var ms = moveSel.Value; var off = moveDragging && hoverCell.x >= 0 ? MoveOffset() : Vector2Int.zero;
                DrawOutline(RectOf(canvas, new Vector2Int(ms.x0, ms.y0), new Vector2Int(ms.x1, ms.y1), size), new Color(1f, 0.85f, 0.2f), 2f);
                if (off != Vector2Int.zero) DrawOutline(RectOf(canvas, new Vector2Int(ms.x0 + off.x, ms.y0 + off.y), new Vector2Int(ms.x1 + off.x, ms.y1 + off.y), size), new Color(0.4f, 1f, 0.6f), 2f);
            }
            if (moveBoxing && hoverCell.x >= 0) DrawOutline(RectOf(canvas, moveFrom, hoverCell, size), new Color(1f, 0.85f, 0.2f, 0.9f), 1f);
            if (rectDragging) DrawOutline(RectOf(canvas, rectStart, hoverCell, size), new Color(1f, 1f, 1f, 0.9f), 2f);
            else if (hoverCell.x >= 0) DrawOutline(CellRect(canvas, hoverCell.x, hoverCell.y, size), new Color(1f, 1f, 1f, 0.5f), 1f);
        }

        var point = new Vector2Int(Mathf.FloorToInt((e.mousePosition.x - canvas.x) / size), doc.Height - 1 - Mathf.FloorToInt((e.mousePosition.y - canvas.y) / size));
        bool inside = canvas.Contains(e.mousePosition);
        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
        {
            var next = inside ? point : new Vector2Int(-1, -1);
            if (next != hoverCell) { hoverCell = next; Repaint(); } // 只在换格子时重画
        }

        if (stampPattern != null && ((e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) || (inside && e.type == EventType.MouseDown && e.button == 1))) { stampPattern = null; e.Use(); Repaint(); }
        else if (stampPattern != null && inside && e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            SetSource(string.Join("\n", LevelBlueprint.Stamp(Rows(), stampPattern, point.x, point.y)), "Stamp " + stampPattern.id);
            ShowNotification(new GUIContent($"盖上了「{stampPattern.zh}」→ 看下面检查结果，不对就 Ctrl+Z"));
            e.Use();
        }
        else if (tool == LevelWorkshopModel.Tool.Move && !e.alt && HandleMove(e, point, inside, control)) { }
        else if (inside && e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
        {
            if (e.alt || tool == LevelWorkshopModel.Tool.Pick)
            {
                char picked = doc.Cell(point.x, point.y); brush = picked == '.' ? '#' : picked; tool = LevelWorkshopModel.Tool.Brush; moveSel = null;
                ShowNotification(new GUIContent($"吸管：画笔换成 {ElementCatalog.Get(brush)?.zh ?? brush.ToString()}，已回到画笔")); e.Use();
            }
            else
            {
                Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Workshop stroke");
                GUIUtility.hotControl = control;
                if (e.shift || tool == LevelWorkshopModel.Tool.Rect) { rectDragging = true; rectStart = point; }
                else { painting = true; strokeStarted = false; lastCell = point; PaintLine(point, e.button == 1 || tool == LevelWorkshopModel.Tool.Erase); }
                e.Use();
            }
        }
        else if (painting && e.type == EventType.MouseDrag) { if (inside) PaintLine(point, e.button == 1 || tool == LevelWorkshopModel.Tool.Erase); e.Use(); }
        else if ((painting || rectDragging) && e.rawType == EventType.MouseUp)
        {
            if (rectDragging)
            {
                Undo.RecordObject(this, "Workshop rect");
                LevelWorkshopModel.FillRect(doc, rectStart.x, rectStart.y, point.x, point.y, e.button == 1 || tool == LevelWorkshopModel.Tool.Erase ? '.' : brush);
                source = doc.Text; parsedSource = source;
            }
            painting = rectDragging = false;
            fullCheckAt = EditorApplication.timeSinceStartup + FullCheckDelay;
            Undo.CollapseUndoOperations(undoGroup);
            GUIUtility.hotControl = 0;
            Save();
            e.Use();
        }
        EditorGUILayout.EndScrollView();
    }

    private Vector2Int MoveOffset()
    {
        if (!moveSel.HasValue) return Vector2Int.zero;
        var (dx, dy) = LevelWorkshopModel.ClampMove(doc.Width, doc.Height, moveSel.Value, hoverCell.x - moveFrom.x, hoverCell.y - moveFrom.y);
        return new Vector2Int(dx, dy);
    }

    private void ApplyMove(int dx, int dy)
    {
        if (!moveSel.HasValue || (dx == 0 && dy == 0)) return;
        var s0 = moveSel.Value; var (cx, cy) = LevelWorkshopModel.ClampMove(doc.Width, doc.Height, s0, dx, dy);
        if (cx == 0 && cy == 0) return;
        SetSource(string.Join("\n", LevelWorkshopModel.MoveBlock(Rows(), s0, cx, cy)), "Move");
        moveSel = new LevelWorkshopModel.Sel(s0.x0 + cx, s0.y0 + cy, s0.x1 + cx, s0.y1 + cy);
        fullCheckAt = EditorApplication.timeSinceStartup + FullCheckDelay;
    }

    /// <summary>S207：移动工具的鼠标/键盘处理。返回 true = 事件已处理。</summary>
    private bool HandleMove(Event e, Vector2Int point, bool inside, int control)
    {
        if (e.type == EventType.KeyDown && moveSel.HasValue)
        {
            int dx = e.keyCode == KeyCode.LeftArrow ? -1 : e.keyCode == KeyCode.RightArrow ? 1 : 0, dy = e.keyCode == KeyCode.DownArrow ? -1 : e.keyCode == KeyCode.UpArrow ? 1 : 0;
            if (dx != 0 || dy != 0) { ApplyMove(dx, dy); e.Use(); return true; }
            if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) { SetSource(string.Join("\n", LevelWorkshopModel.ClearBlock(Rows(), moveSel.Value)), "Clear"); fullCheckAt = EditorApplication.timeSinceStartup + FullCheckDelay; e.Use(); return true; }
            if ((e.control || e.command) && e.keyCode == KeyCode.C) { clipboard = LevelWorkshopModel.CopyBlock(Rows(), moveSel.Value); ShowNotification(new GUIContent($"已复制 {clipboard.Count} 格（M/T/G/宝物不复制）；鼠标指到落点按 Ctrl+V")); e.Use(); return true; }
            if (e.keyCode == KeyCode.Escape) { moveSel = null; Repaint(); e.Use(); return true; }
        }
        if (e.type == EventType.KeyDown && (e.control || e.command) && e.keyCode == KeyCode.V && clipboard != null && hoverCell.x >= 0)
        {
            SetSource(string.Join("\n", LevelWorkshopModel.PasteBlock(Rows(), clipboard, hoverCell.x, hoverCell.y)), "Paste");
            fullCheckAt = EditorApplication.timeSinceStartup + FullCheckDelay; e.Use(); return true;
        }
        if (inside && e.type == EventType.MouseDown && e.button == 0)
        {
            GUIUtility.hotControl = control; moveFrom = point;
            if (moveSel.HasValue && moveSel.Value.Contains(point.x, point.y)) moveDragging = true;
            else
            {
                moveSel = LevelWorkshopModel.SelectAt(Rows(), point.x, point.y);
                if (moveSel.HasValue) moveDragging = true; else moveBoxing = true;
            }
            e.Use(); Repaint(); return true;
        }
        if ((moveDragging || moveBoxing) && e.type == EventType.MouseDrag) { e.Use(); Repaint(); return true; }
        if ((moveDragging || moveBoxing) && e.rawType == EventType.MouseUp)
        {
            if (moveBoxing && hoverCell.x >= 0)
            {
                var box = new LevelWorkshopModel.Sel(moveFrom.x, moveFrom.y, hoverCell.x, hoverCell.y);
                moveSel = box.x0 == box.x1 && box.y0 == box.y1 ? (LevelWorkshopModel.Sel?)null : box;
            }
            else if (moveDragging && hoverCell.x >= 0) { var off = MoveOffset(); ApplyMove(off.x, off.y); }
            moveDragging = moveBoxing = false; GUIUtility.hotControl = 0; e.Use(); Repaint(); Save(); return true;
        }
        return false;
    }

    private void PaintLine(Vector2Int p, bool erase)
    {
        char v = erase ? '.' : brush;
        if (p == lastCell && p.x >= 0 && p.x < doc.Width && p.y >= 0 && p.y < doc.Height && doc.Cell(p.x, p.y) == v && painting && strokeStarted) return; // 同一格不重复记录
        strokeStarted = true;
        Undo.RecordObject(this, "Workshop stroke");
        int steps = Mathf.Max(Mathf.Abs(p.x - lastCell.x), Mathf.Abs(p.y - lastCell.y));
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 1f : i / (float)steps;
            doc.Paint(Mathf.RoundToInt(Mathf.Lerp(lastCell.x, p.x, t)), Mathf.RoundToInt(Mathf.Lerp(lastCell.y, p.y, t)), v);
        }
        source = doc.Text; parsedSource = source; lastCell = p;
        Repaint();
    }

    private Rect CellRect(Rect canvas, int x, int y, float size) => new Rect(canvas.x + x * size, canvas.y + (doc.Height - 1 - y) * size, size - 1, size - 1);

    private Rect RectOf(Rect canvas, Vector2Int a, Vector2Int b, float size)
    {
        int x0 = Math.Min(a.x, b.x), x1 = Math.Max(a.x, b.x), y0 = Math.Min(a.y, b.y), y1 = Math.Max(a.y, b.y);
        var top = CellRect(canvas, x0, y1, size);
        return new Rect(top.x, top.y, (x1 - x0 + 1) * size, (y1 - y0 + 1) * size);
    }

    private static readonly Color[] floorTints =
    {
        new Color(0.3f, 0.6f, 1f, 0.07f), new Color(1f, 0.6f, 0.2f, 0.07f), new Color(0.4f, 1f, 0.5f, 0.07f), new Color(1f, 0.3f, 0.7f, 0.07f)
    };

    private void DrawHakoniwa(Rect canvas, float size)
    {
        var r = Hako();
        foreach (var f in r.floors)
            for (int y = f.yMin; y <= f.yMax; y++)
                for (int x = 0; x < doc.Width; x++) Tint(canvas, x, y, size, floorTints[f.index % floorTints.Length]);
        foreach (var l in r.links)
        {
            Color c = l.kind == "楼梯口" || l.kind == "单向台面" ? new Color(0.4f, 1f, 0.5f) : l.kind == "捷径门" ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.4f, 0.9f);
            DrawOutline(CellRect(canvas, l.x, l.y, size), c, 3f);
        }
        foreach (var f in r.floors)
        {
            Rect label = CellRect(canvas, 1, f.yMax, size);
            GUI.Label(new Rect(label.x, label.y, 260, 16), $"F{f.index + 1}  {f.Identity}", EditorStyles.whiteMiniLabel);
        }
    }

    private void DrawOverviewPanel()
    {
        var r = Hako();
        EditorGUILayout.BeginVertical(GUILayout.Width(260));
        EditorGUILayout.LabelField("箱庭总览 Overview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(r.Summary, r.hasLoop ? MessageType.Info : MessageType.Warning);
        overviewScroll = EditorGUILayout.BeginScrollView(overviewScroll);
        foreach (var f in r.floors)
        {
            EditorGUILayout.LabelField($"F{f.index + 1}：{f.Identity}", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"   藏身处 {f.cover} 个；机关 {string.Join("、", f.pranks.Select(kv => kv.Key + "×" + kv.Value))}", EditorStyles.wordWrappedMiniLabel);
            foreach (var l in r.links.Where(l => l.upper == f.index))
                EditorGUILayout.LabelField(l.lower == l.upper ? $"   ↔ 同层{l.kind} ({l.x},{l.y})" : $"   ↓ 到 F{l.lower + 1}：{l.kind} ({l.x},{l.y}){(l.shortcut ? " [要打开]" : "")}", EditorStyles.wordWrappedMiniLabel);
        }
        if (r.advice.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("箱庭建议", EditorStyles.miniBoldLabel);
            foreach (var a in r.advice) EditorGUILayout.LabelField("• " + a, EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("图例：绿框=楼梯/台面  黄框=捷径门  粉框=裂墙/裂缝（要打开）", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private static readonly Color[] groupColors =
    {
        new Color(1f, 0.55f, 0.1f), new Color(0.3f, 0.9f, 1f), new Color(1f, 0.35f, 0.85f), new Color(0.5f, 1f, 0.4f), new Color(1f, 1f, 0.3f)
    };

    private void DrawComboRoutes(Rect canvas, float size)
    {
        var r = ComboResult();
        var groupOf = new Dictionary<int, int>();
        for (int g = 0; g < r.groups.Count; g++) foreach (int i in r.groups[g]) groupOf[i] = g;
        Handles.BeginGUI();
        foreach (var l in r.links)
        {
            var a = r.nodes[l.a]; var b = r.nodes[l.b];
            Color c = groupColors[groupOf[l.a] % groupColors.Length];
            Vector2 pa = CellRect(canvas, a.x, a.y, size).center, pb = CellRect(canvas, b.x, b.y, size).center;
            Vector2 mid = (pa + pb) * 0.5f + Vector2.down * Mathf.Min(40f, Vector2.Distance(pa, pb) * 0.25f); // 弧线，重叠少
            Handles.color = c;
            Handles.DrawAAPolyLine(4f, pa, mid, pb);
        }
        Handles.EndGUI();
        foreach (var n in r.nodes)
        {
            int g = groupOf.TryGetValue(r.nodes.IndexOf(n), out int gg) ? gg : 0;
            DrawOutline(CellRect(canvas, n.x, n.y, size), groupColors[g % groupColors.Length], 3f);
        }
    }

    private void DrawStrategy(Rect canvas, float size)
    {
        var r = Strategy();
        foreach (int k in r.reinforced) { var cr = CellRect(canvas, k / 1000, k % 1000, size); EditorGUI.DrawRect(new Rect(cr.x + cr.width * 0.3f, cr.y + 2, cr.width * 0.4f, cr.height * 0.25f), new Color(0.9f, 0.9f, 1f, 0.95f)); DrawOutline(cr, new Color(0.7f, 0.8f, 1f), 2f); }
        if (r.trapBeforeReinforce != null)
        {
            foreach (var b in r.trapBeforeReinforce.bombs) GUI.Label(CellRect(canvas, b.x, b.y, size), "💣", cellLabel);
            foreach (var v in r.trapBeforeReinforce.victims) DrawOutline(CellRect(canvas, v.x, v.y, size), new Color(1f, 0.3f, 0.3f), 2f);
        }
        foreach (var o in r.offRoute) DrawOutline(CellRect(canvas, o.x, o.y, size), new Color(0.6f, 0.6f, 0.6f), 2f);
        if (r.route != null)
        {
            Handles.BeginGUI(); Handles.color = new Color(0.4f, 1f, 0.6f, 0.8f);
            var pts = r.route.Select(c => (Vector3)CellRect(canvas, c.x, c.y, size).center).ToArray();
            if (pts.Length > 1) Handles.DrawAAPolyLine(3f, pts);
            Handles.EndGUI();
            foreach (var s in r.onRoute) GUI.Label(new Rect(CellRect(canvas, s.x, s.y, size).x, CellRect(canvas, s.x, s.y, size).y - size * 0.6f, size * 2f, size * 0.6f), $"{s.at:F0}s", EditorStyles.miniBoldLabel);
        }
    }

    // ── S208：向导 / 印章 / 起承转合 / 节奏 / 转移点 ────────────
    private void WizardMenu()
    {
        var menu = new GenericMenu();
        foreach (char star in LevelBlueprint.WizardStars)
            foreach (int sec in new[] { 20, 30, 40 })
            {
                char s0 = star; int t0 = sec; var rec = LevelBlueprint.Recipe(star);
                string label = $"{ElementCatalog.Get(star)?.zh ?? star.ToString()} 为主角/约 {sec} 秒（{LevelBlueprint.WidthFor(sec)} 格宽）· " + string.Join(" → ", rec.Select((r, i) => LevelBlueprint.BeatZh[i] + LevelBlueprint.Get(r.id).zh));
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    if (!EditorUtility.DisplayDialog("新建关卡向导", "用向导草稿替换当前画布？（Ctrl+Z 可撤销）\n\n生成后已经能玩：金色分段是起承转合 4 段。先试玩一局，再改最别扭的一处。", "生成", "取消")) return;
                    var d = LevelBlueprint.Wizard(s0, t0, "");
                    var text = string.Join("\n", d.grid) + "\n# Goal: " + d.goal + string.Concat(d.notes.Select(n => $"\n# Note: ({n.x},{n.y}) {n.text}"));
                    SetSource(text, "Wizard"); beats = d.beats; showBeats = true; blueprintKey = null;
                });
            }
        menu.ShowAsContext();
    }

    private void StampMenu()
    {
        var menu = new GenericMenu();
        foreach (var p in LevelBlueprint.Patterns) { var p0 = p; menu.AddItem(new GUIContent($"{p.zh}　{string.Join(" / ", p.rows).Replace('_', ' ').Replace('*', p.def)}"), stampPattern == p, () => { stampPattern = p0; ShowNotification(new GUIContent($"印章「{p0.zh}」：{p0.tip}")); }); }
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("退出印章"), false, () => stampPattern = null);
        menu.ShowAsContext();
    }

    private int[] CurrentBeats()
    {
        var rows = Rows();
        if (beats != null && beats.Length == 5 && beats[4] < doc.Width) return beats;
        return LevelBlueprint.BeatBounds(rows);
    }

    /// <summary>节奏 + 转移点（只在网格变化时重算；复用策略模拟的路线时间线）。</summary>
    private void UpdateBlueprint()
    {
        string key = doc.Grid;
        if (blueprintKey == key || fullCheckStale) return; // 画的过程中不算，停笔后再算（和策略模拟一样）
        blueprintKey = key;
        var r = Strategy();
        var tune = AssetDatabase.LoadAssetAtPath<MarioMindTuningSO>(Step1PrankRoomBuilder.TuningAssetPath);
        float start = tune != null ? tune.startDelaySeconds : 4f, speed = StrategySim.RunSpeed(9f, tune != null ? tune.marioSpeedScale : 0.55f);
        rhythmWarn.Clear(); coverHints.Clear(); rhythmSummary = "马里奥路线算不出来";
        if (r.route == null) return;
        var route = r.route.Select(c => (c.x, c.y)).ToList();
        var times = new List<float> { start }; float len = 0;
        for (int i = 1; i < route.Count; i++) { len += Mathf.Sqrt((route[i].x - route[i - 1].x) * (route[i].x - route[i - 1].x) + (route[i].y - route[i - 1].y) * (route[i].y - route[i - 1].y)); times.Add(start + len / speed); }
        var passes = LevelBlueprint.Passes(route, times, r.onRoute.Select(s => (s.x, s.y)));
        var (segs, warn) = LevelBlueprint.Rhythm(passes, r.routeSeconds);
        rhythmWarn.AddRange(warn);
        float busy = segs.Where(g => g.busy).Sum(g => g.b - g.a);
        rhythmSummary = $"紧张 {Mathf.RoundToInt(busy / Mathf.Max(0.1f, r.routeSeconds) * 100)}% · {passes.Count} 次经过机关（红绿交替最好：连续紧张 ≥8 秒太挤，连续没事 ≥10 秒太空）";
        coverHints.AddRange(LevelBlueprint.CoverHints(Rows(), r.onRoute.Select(s => (s.ch, s.x, s.y))));
    }

    private void DrawBlueprint(Rect canvas, float size)
    {
        var bb = CurrentBeats();
        if (bb != null)
        {
            var gold = new Color(1f, 0.78f, 0.24f, 0.8f);
            for (int i = 0; i < 4; i++)
            {
                float x0 = canvas.x + bb[i] * size, x1 = canvas.x + bb[i + 1] * size;
                if (i % 2 == 0) EditorGUI.DrawRect(new Rect(x0, canvas.y + size, x1 - x0, (doc.Height - 2) * size), new Color(1f, 0.78f, 0.24f, 0.06f));
                EditorGUI.DrawRect(new Rect(x0, canvas.y + size, 1.5f, (doc.Height - 2) * size), gold);
                GUI.Label(new Rect(x0 + 3, canvas.y + size, 80, 18), $"<color=#FFC83D><b>{LevelBlueprint.BeatZh[i]}</b></color> <color=#C8A040>{new[] { "教", "加深", "意外", "收尾" }[i]}</color>", new GUIStyle(EditorStyles.label) { richText = true });
            }
            EditorGUI.DrawRect(new Rect(canvas.x + bb[4] * size, canvas.y + size, 1.5f, (doc.Height - 2) * size), gold);
        }
        UpdateBlueprint();
        foreach (var c in coverHints) DrawOutline(CellRect(canvas, c.x, c.y, size), new Color(1f, 0.7f, 0.28f), 3f);
    }

    private void DrawStampPreview(Rect canvas, float size)
    {
        var rows = Rows(); var prev = LevelBlueprint.Stamp(rows, stampPattern, hoverCell.x, hoverCell.y);
        for (int r = 0; r < prev.Length; r++)
            for (int x = 0; x < prev[r].Length; x++)
                if (prev[r][x] != rows[r][x]) { var cr = CellRect(canvas, x, doc.Height - 1 - r, size); var col = CellColor(prev[r][x]); col.a = 0.75f; EditorGUI.DrawRect(cr, col); if (size >= 14 && prev[r][x] != '.') GlyphFor(cr, prev[r][x], col); }
        int top = hoverCell.y + stampPattern.stand;
        DrawOutline(RectOf(canvas, new Vector2Int(hoverCell.x, top - stampPattern.rows.Length + 1), new Vector2Int(hoverCell.x + stampPattern.Width - 1, top), size), new Color(1f, 0.78f, 0.24f), 2f);
    }

    private void DrawTrack(Rect canvas, float size)
    {
        if (trackVisits == null) return;
        int max = 1; foreach (var v in trackVisits.Values) if (v > max) max = v;
        foreach (var kv in trackVisits) Tint(canvas, kv.Key / 1000, kv.Key % 1000, size, new Color(0.3f, 0.8f, 1f, 0.15f + 0.5f * kv.Value / max));
        foreach (int k in trackStuck) GUI.Label(CellRect(canvas, k / 1000, k % 1000, size), "<color=#FF4040><b>✗</b></color>", new GUIStyle(cellLabel) { richText = true });
    }

    private void Tint(Rect canvas, int x, int y, float size, Color c) { if (x >= 0 && y >= 0 && x < doc.Width && y < doc.Height) EditorGUI.DrawRect(CellRect(canvas, x, y, size), c); }

    private static void DrawOutline(Rect r, Color c, float w)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, w), c); EditorGUI.DrawRect(new Rect(r.x, r.yMax - w, r.width, w), c);
        EditorGUI.DrawRect(new Rect(r.x, r.y, w, r.height), c); EditorGUI.DrawRect(new Rect(r.xMax - w, r.y, w, r.height), c);
    }

    private static Color Opaque(Color c) { c.a = 1f; return c; }

    private readonly Dictionary<char, Color> colorCache = new Dictionary<char, Color>();
    private Color CellColor(char ch)
    {
        if (colorCache.TryGetValue(ch, out var c)) return c;
        c = ch == '.' ? new Color(0.13f, 0.15f, 0.19f) : Opaque(ElementCatalog.EditorColor(ch));
        colorCache[ch] = c;
        return c;
    }

    // ── 底部：状态 + 问题列表 + 试玩 ──────────────────────
    private void DrawStatus()
    {
        EditorGUILayout.BeginHorizontal();
        string hover = "";
        if (hoverCell.x >= 0 && hoverCell.x < doc.Width && hoverCell.y >= 0 && hoverCell.y < doc.Height)
        {
            char ch = doc.Cell(hoverCell.x, hoverCell.y);
            var info = ElementCatalog.Get(ch);
            hover = $"({hoverCell.x},{hoverCell.y}) " + (info != null ? $"{info.zh}：{info.what}" : Step1Layout.Slots.ContainsKey(ch) ? "随机槽位" : "");
            var here = check?.cells.Where(c => c.x == hoverCell.x && c.y == hoverCell.y).Select(c => c.text).ToArray();
            if (here != null && here.Length > 0) hover += "\n⚠ " + string.Join("\n⚠ ", here);
        }
        EditorGUILayout.LabelField($"{doc.Width} × {doc.Height} 格   画笔：{ElementCatalog.Get(brush)?.zh ?? brush.ToString()}\n{LevelWorkshopModel.ToolHint(tool, moveSel.HasValue)}", EditorStyles.wordWrappedMiniLabel, GUILayout.Width(260));
        if (GUILayout.Button("右 +8", EditorStyles.miniButton, GUILayout.Width(44))) SetSource(doc.Resize(Math.Min(LevelStudioDocument.MaxWidth, doc.Width + 8), doc.Height).Text, "Expand");
        if (GUILayout.Button("右 -8", EditorStyles.miniButton, GUILayout.Width(44)) && doc.Width > 24) SetSource(doc.Resize(doc.Width - 8, doc.Height).Text, "Shrink");
        if (GUILayout.Button("上 +2", EditorStyles.miniButton, GUILayout.Width(44))) SetSource(doc.Resize(doc.Width, Math.Min(LevelStudioDocument.MaxHeight, doc.Height + 2)).Text, "Expand");
        EditorGUILayout.LabelField(hover, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndHorizontal();

        if (check != null)
        {
            string head = fullCheckStale ? "…检查中（停笔后自动完成）" : check.Headline;
            if (comboRoutes) head += "\n连招路线：" + ComboResult().Summary;
            if (strategy) { var sr = Strategy(); head += "\n策略模拟：" + sr.Summary(); foreach (var wn in sr.warnings) head += "\n  · " + wn; }
            if (showTrack) head += "\n" + trackNote;
            if (showBeats) { UpdateBlueprint(); head += "\n节奏：" + rhythmSummary; foreach (var w in rhythmWarn) head += "\n  · " + w; if (coverHints.Count > 0) head += $"\n转移点：{coverHints.Count} 个机关 5 格内没有草丛/箱子可以躲（橙框）——你走过去时会被看见。提前伪装好等他来，或旁边放一丛草 b"; }
            if (stampPattern != null) head += $"\n印章「{stampPattern.zh}」：点画布上马里奥站的那格盖章（右键 / Esc 退出）· {stampPattern.tip}";
            EditorGUILayout.HelpBox(head + (check.cells.Count > 0 ? "（鼠标停在红/黄框格子上看原因）" : ""), check.Playable ? MessageType.Info : MessageType.Error);
            if (check.general.Count + check.cells.Count > 0)
            {
                issueScroll = EditorGUILayout.BeginScrollView(issueScroll, GUILayout.Height(70));
                foreach (var g in check.general) EditorGUILayout.LabelField("• " + g, EditorStyles.wordWrappedMiniLabel);
                foreach (var c in check.cells.Take(30)) EditorGUILayout.LabelField($"• ({c.x},{c.y}) {c.text}", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndScrollView();
            }
        }

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(check == null || fullCheckStale || !check.Playable || EditorApplication.isPlayingOrWillChangePlaymode || !step1Mode))
            if (GUILayout.Button(new GUIContent("▶ 作为第 1 步房间试玩", "用恶作剧房间的全部规则（马里奥心智、问卷、随机、防卡死）玩这张图"), GUILayout.Height(34)))
                PlayAsStep1();
        if (GUILayout.Button(new GUIContent("恢复默认恶作剧房间", "以后 ▶ Play Prank Room 用回默认房间"), GUILayout.Height(34), GUILayout.Width(150)))
        {
            Step1PrankRoomBuilder.UseCustomRoom = false;
            ShowNotification(new GUIContent("已恢复默认房间"));
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(Step1PrankRoomBuilder.UseCustomRoom ? "当前 ▶ Play Prank Room 使用：你的自定义房间" : "当前 ▶ Play Prank Room 使用：默认房间", EditorStyles.miniLabel);
    }

    private void PlayAsStep1()
    {
        var rows = doc.Grid.Split('\n');
        Step1PrankRoomBuilder.SaveCustomRoom(rows);
        Step1PrankRoomBuilder.UseCustomRoom = true;
        Step1PrankRoomBuilder.PlayMenu();
    }

    private void LibraryMenu()
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("存入关卡库（当前画布）…"), false, SaveToLibrary);
        menu.AddItem(new GUIContent("导入网页关卡包（.levelpack.json，可多关）…"), false, ImportPack);
        menu.AddSeparator("");
        var list = LevelLibrary.List();
        if (list.Count == 0) menu.AddDisabledItem(new GUIContent("（关卡库还是空的）"));
        foreach (var (name, path, pending) in list)
        {
            string label = "打开/" + name.Replace("/", "_") + (pending.Count > 0 ? $"  ⏳{string.Join("", pending)}" : "");
            menu.AddItem(new GUIContent(label), false, () => { SetSource(File.ReadAllText(path), "Open " + name); libraryName = name; StartHereWindow.Touch(RecentWork.Room, name); });
        }
        menu.ShowAsContext();
    }

    [SerializeField] private string libraryName = ""; // S214：重新编译后也记得当前是关卡库里哪一关（◀ ▶ 切换、Ctrl+S 直接存）
    private void SaveToLibrary()
    {
        string name = LevelNameDialog.Ask("存入关卡库", "给这一关起个名字（同名会覆盖）：", string.IsNullOrEmpty(libraryName) ? "我的关卡" : libraryName);
        if (string.IsNullOrEmpty(name)) return;
        string path = SaveAs(name);
        ShowNotification(new GUIContent("已存入 " + path));
    }

    private string SaveAs(string name)
    {
        var level = new LevelPack.Level { name = name, rows = Rows() };
        foreach (var line in (source ?? "").Replace("\r", "").Split('\n'))
        {
            if (line.StartsWith("# Goal: ")) level.goal = line.Substring(8);
            if (line.StartsWith("# Note: ")) { var m = System.Text.RegularExpressions.Regex.Match(line, @"^# Note: \((\d+),(\d+)\) (.*)$"); if (m.Success) level.notes.Add(new LevelPack.Note { x = int.Parse(m.Groups[1].Value), y = int.Parse(m.Groups[2].Value), text = m.Groups[3].Value }); }
        }
        // S214：网页里的"还没实现的新机制"（# Pending 行）存回去时保留，不然 Unity 一存就把网页画的新机制弄丢
        var old = LevelLibrary.List().Find(l => l.name == name);
        if (old.path != null) LevelWorkshopModel.CarryPending(File.ReadAllText(old.path), level);
        string path = LevelLibrary.Save(level);
        libraryName = name;
        StartHereWindow.Touch(RecentWork.Room, name); // S237：开始页「上次做到哪」
        return path;
    }

    private void ImportPack()
    {
        string path = EditorUtility.OpenFilePanel("导入网页关卡包", Application.dataPath, "json");
        if (string.IsNullOrEmpty(path)) return;
        var (count, report) = LevelLibrary.ImportPack(File.ReadAllText(path));
        EditorUtility.DisplayDialog(count > 0 ? "导入完成" : "导入失败", report, "OK");
        var list = LevelLibrary.List();
        if (count > 0 && list.Count > 0) { var first = list.Find(l => report.Contains(l.path)); if (first.path != null) { SetSource(File.ReadAllText(first.path), "Import pack"); libraryName = first.name; } }
    }

    private void Import()
    {
        string path = EditorUtility.OpenFilePanel("导入关卡（.txt 或 网页设计台 .json）", Application.dataPath, "txt,json");
        if (string.IsNullOrEmpty(path)) return;
        string text = File.ReadAllText(path);
        // S204：网页关卡设计台导出的 .studio.json → 取出网格（批注/提案交给 AI 按设计单处理）
        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var rows = LevelWorkshopModel.GridFromStudioJson(text, out string why);
            if (rows == null) { EditorUtility.DisplayDialog("导入失败", why, "OK"); return; }
            text = string.Join("\n", rows);
            if (why.Length > 0) EditorUtility.DisplayDialog("提示", why, "OK");
        }
        if (!LevelStudioDocument.TryParse(text, out _, out string err)) { EditorUtility.DisplayDialog("导入失败", err, "OK"); return; }
        SetSource(text, "Import");
    }

    private void Export()
    {
        string path = EditorUtility.SaveFilePanel("导出关卡 .txt", Application.dataPath, "my_level", "txt");
        if (string.IsNullOrEmpty(path)) return;
        File.WriteAllText(path, doc.Text + "\n");
        ShowNotification(new GUIContent("已导出"));
    }
}
