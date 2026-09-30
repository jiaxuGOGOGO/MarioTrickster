using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// S210：小镇工坊（星露谷视角大地图编辑器）。左边调色板，中间画布（格子 = 小镇俯视图），右边：门的时间和连的房间、检查、马里奥的一天。
/// 和关卡工坊一样：画好 → 看检查 → ▶ 试玩。导入网页工作室导出的小镇 / 关卡包。
/// </summary>
public sealed class OverworldWorkshopWindow : EditorWindow
{
    private enum Tool { Brush, Rect, Fill, Erase, Pick }
    private OverworldMap.Map map;
    private char brush = '=';
    private Tool tool = Tool.Brush;
    private Vector2 scroll, side;
    private float cell = 16f;
    private Vector2Int? dragStart;
    private OverworldMap.Report report;
    private bool scenesStale = true; // S211：只在检查时算（读文件），不在每次重绘算
    private string status = "";
    private readonly Stack<string> undo = new Stack<string>();
    private readonly Stack<string> redo = new Stack<string>(); // S212
    private Vector2Int hover = new Vector2Int(-1, -1);          // S212：鼠标所在格
    private Vector2Int? flash; private double flashUntil;       // S212：点问题 → 那一格闪
    private float scrub = -1f;                                  // S212：时间滑条（分钟；-1 = 关）
    private Rect canvasView;                                    // 画布可见区域（滚动定位用）
    private const string DraftKey = "MarioTrickster.Overworld.WorkshopDraft";

    [MenuItem("MarioTrickster/Town Workshop (小镇工坊) %&o", false, 2)]
    public static void Open()
    {
        var w = GetWindow<OverworldWorkshopWindow>("小镇工坊");
        w.minSize = new Vector2(900, 520);
        if (w.map == null) w.Load(DraftOrCurrent(), "打开");
    }

    /// <summary>S212：脚本重新编译后窗口不丢正在画的图（SessionState 只活到关掉 Unity）。</summary>
    private static string DraftOrCurrent() { string d = SessionState.GetString(DraftKey, ""); return string.IsNullOrEmpty(d) ? OverworldBuilder.CurrentText : d; }

    private void Load(string text, string why)
    {
        if (map != null) { undo.Push(OverworldMap.ToText(map)); redo.Clear(); }
        map = OverworldMap.Parse(text);
        if (map.W == 0) map = OverworldMap.Parse(OverworldPack.SampleText);
        Recheck(); status = why; botLines.Clear();
    }

    private void Snapshot() { redo.Clear(); undo.Push(OverworldMap.ToText(map)); if (undo.Count > 60) { var a = undo.ToArray().Take(60).Reverse(); undo.Clear(); foreach (var s in a) undo.Push(s); } }
    private void Recheck() { SyncDoors(); report = OverworldMap.Check(map, OverworldBuilder.RulesFromTuning(), OverworldBuilder.RoomProblem); string t = OverworldMap.ToText(map); scenesStale = OverworldBuilder.IsStale(t); SessionState.SetString(DraftKey, t); Repaint(); }

    private void Undo() { if (undo.Count == 0) return; redo.Push(OverworldMap.ToText(map)); map = OverworldMap.Parse(undo.Pop()); Recheck(); status = "撤销（Ctrl+Y 重做）"; }
    private void Redo() { if (redo.Count == 0) return; undo.Push(OverworldMap.ToText(map)); map = OverworldMap.Parse(redo.Pop()); Recheck(); status = "重做"; }

    private void PlayTown()
    {
        OverworldBuilder.Save(map);
        if (OverworldBuilder.BuildAll(OverworldMap.ToText(map), out string rep)) EditorApplication.isPlaying = true;
        else EditorUtility.DisplayDialog("小镇", rep, "好");
    }

    /// <summary>S212：快捷键（和网页设计台一样）。B 画笔 R 矩形 F 填充 E 橡皮 I 吸管；1–9 门；Ctrl+Z/Y 撤销重做；Ctrl+S 保存；F5 试玩；Ctrl+C/V 和网页互相复制。</summary>
    private void Hotkeys()
    {
        var e = Event.current;
        if (e.type != EventType.KeyDown || EditorGUIUtility.editingTextField) return;
        bool ctrl = e.control || e.command;
        if (ctrl && e.keyCode == KeyCode.Z) { if (e.shift) Redo(); else Undo(); e.Use(); return; }
        if (ctrl && e.keyCode == KeyCode.Y) { Redo(); e.Use(); return; }
        if (ctrl && e.keyCode == KeyCode.S) { status = "已保存到 " + OverworldBuilder.Save(map); e.Use(); return; }
        if (ctrl && e.keyCode == KeyCode.C) { EditorGUIUtility.systemCopyBuffer = OverworldMap.ToText(map); status = "已复制整张小镇（网页'大地图'页 Ctrl+V 就能贴进去）"; e.Use(); return; }
        if (ctrl && e.keyCode == KeyCode.V) { PasteTown(); e.Use(); return; }
        if (e.keyCode == KeyCode.F5) { PlayTown(); e.Use(); return; }
        if (e.keyCode == KeyCode.PageUp) { StepTown(-1); e.Use(); return; }
        if (e.keyCode == KeyCode.PageDown) { StepTown(1); e.Use(); return; }
        if (ctrl || e.alt) return;
        switch (e.keyCode)
        {
            case KeyCode.B: tool = Tool.Brush; break;
            case KeyCode.R: tool = Tool.Rect; break;
            case KeyCode.F: tool = Tool.Fill; break;
            case KeyCode.E: tool = Tool.Erase; break;
            case KeyCode.I: tool = Tool.Pick; break;
            default:
                if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha9) { brush = (char)('1' + (e.keyCode - KeyCode.Alpha1)); if (tool == Tool.Erase || tool == Tool.Pick) tool = Tool.Brush; break; }
                return;
        }
        e.Use(); Repaint();
    }

    // ── S215：一天总览（全局：每扇门的房间主打什么、哪扇门第一次教新机关、重复 / 一次教太多、炸弹预算）──
    private bool ledgerOpen = true;
    private CampaignLedger.Report ledger; private string ledgerKey;

    private void LedgerPanel()
    {
        EditorGUILayout.Space();
        ledgerOpen = EditorGUILayout.Foldout(ledgerOpen, "📋 一天总览（按马里奥的顺序）", true, EditorStyles.foldoutHeader);
        if (!ledgerOpen) return;
        string key = OverworldMap.ToText(map) + "|" + string.Join(",", OverworldBuilder.RoomNames());
        var t = MarioMindTuningSO.LoadOrDefault();
        if (ledger == null || ledgerKey != key) { ledgerKey = key; ledger = CampaignLedger.Build(map, n => OverworldBuilder.ResolveRoom(n), t.bombsPerRound, OverworldTown.MaxBonusBombs); }
        foreach (var r in ledger.rooms)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{r.clock} 门{r.door}", EditorStyles.miniBoldLabel, GUILayout.Width(78));
            if (r.missing) EditorGUILayout.LabelField("找不到「" + r.room + "」", EditorStyles.miniLabel);
            else
            {
                string kinds = string.Join(" ", r.kinds.Select(k => k.zh + (k.n > 1 ? "×" + k.n : "")));
                EditorGUILayout.LabelField(new GUIContent($"{r.room} · 主角 {r.star}" + (r.firstTime.Count > 0 ? "  ✦新：" + string.Join("、", r.firstTime) : ""), kinds), EditorStyles.wordWrappedMiniLabel);
            }
            if (!r.missing && GUILayout.Button(new GUIContent("✎", "打开这个房间"), EditorStyles.miniButton, GUILayout.Width(22))) LevelWorkshopWindow.OpenRoom(r.room);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.LabelField($"💣 每个房间最多 {ledger.maxBombs} 颗（本回合 {t.bombsPerRound} + 小镇带进来最多 {OverworldTown.MaxBonusBombs}）；房间按这个数加固，炸不死马里奥", EditorStyles.wordWrappedMiniLabel);
        foreach (var w in ledger.warnings) EditorGUILayout.HelpBox(w, MessageType.Info);
    }

    private List<string> botLines = new List<string>();

    /// <summary>纯逻辑跑机器人玩家（OverworldBots）。只在编辑模式跑（会临时占用 OverworldSession，跑完清掉）。</summary>
    private List<string> RunBots()
    {
        var t = MarioMindTuningSO.LoadOrDefault(); var m = OverworldMap.Parse(OverworldMap.ToText(map)); var lines = new List<string>();
        int doors = m.doors.Count(d => OverworldMap.Find(m, (char)('0' + d.n)).Count == 1);
        var who = new[] { (OverworldBots.Kind.Hider, "会躲（伪装等他）"), (OverworldBots.Kind.Follower, "站着不躲"), (OverworldBots.Kind.Slow, "反应慢"), (OverworldBots.Kind.Greedy, "先捡道具") };
        int hide = 0, stand = 0, slowCaught = 0;
        foreach (var (k, zh) in who)
        {
            int am = 0, ca = 0; double sec = 0;
            for (int s = 1; s <= 3; s++) { var r = OverworldBots.PlayDay(m, t, k, true, s); am += r.ambush; ca += r.caught; sec += r.realSeconds; }
            lines.Add($"{zh}：埋伏 {am}/{doors * 3}，被抓 {ca}，一天约 {sec / 3:0} 秒");
            if (k == OverworldBots.Kind.Hider) hide = am; if (k == OverworldBots.Kind.Follower) stand = am; if (k == OverworldBots.Kind.Slow) slowCaught = ca;
        }
        OverworldSession.ResetStatics();
        if (hide < doors * 3) lines.Add("⚠ 会躲的玩家也有门埋伏不上：这扇门附近缺藏身处，或时间太紧（看检查里的'你能提前几秒'）");
        if (stand >= doors * 3) lines.Add("⚠ 站着不躲也全赢：躲藏没有意义——门口视野太空？把门放在他路线的拐角后面");
        if (slowCaught > 3) lines.Add("⚠ 反应慢的玩家老被抓：出门的地方太开阔");
        if (lines.Count == who.Length) lines.Add("✓ 会躲才稳赢：这张小镇的节奏是对的（房间里的战斗没有模拟）");
        return lines;
    }

    private void StepTown(int dir)
    {
        var list = OverworldBuilder.List();
        if (list.Count == 0) { status = "还没有保存的小镇：先点 保存"; return; }
        OverworldBuilder.Save(map);
        list = OverworldBuilder.List();
        int i = list.FindIndex(l => l.name == map.name);
        int n = i < 0 ? 0 : (i + dir + list.Count) % list.Count;
        Load(File.ReadAllText(list[n].path), $"{n + 1}/{list.Count}  {list[n].name}");
        EditorPrefs.SetString(OverworldBuilder.CurrentKey, list[n].path);
    }

    private void PasteTown()
    {
        string t = EditorGUIUtility.systemCopyBuffer ?? "";
        var maps = OverworldPack.Parse(t);
        if (maps.Count == 0 && OverworldMap.IsOverworldText(t)) maps.Add(OverworldMap.Parse(t));
        if (maps.Count == 0 || maps[0].W == 0) { status = "剪贴板里不是小镇（在网页'大地图'页按 Ctrl+C 复制）"; return; }
        Load(OverworldMap.ToText(maps[0]), "从剪贴板贴入 " + maps[0].name + "（Ctrl+Z 撤销）");
    }

    /// <summary>S212：点检查里的问题 → 画布滚到那一格并闪 1.5 秒。</summary>
    private void Locate(int x, int y)
    {
        if (x < 0 || y < 0) return;
        flash = new Vector2Int(x, y); flashUntil = EditorApplication.timeSinceStartup + 1.5;
        scroll = new Vector2(Mathf.Max(0, x * cell - canvasView.width / 2f), Mathf.Max(0, (map.H - 1 - y) * cell - canvasView.height / 2f));
        Repaint();
    }

    private void OnEnable() { wantsMouseMove = true; EditorApplication.update += Blink; WebSync.Imported -= OnWebImported; WebSync.Imported += OnWebImported; }
    private void OnDisable() { EditorApplication.update -= Blink; WebSync.Imported -= OnWebImported; }

    /// <summary>S214：网页改了正在打开的这张小镇 → 自动换成新版本（↶ 可以退回）。</summary>
    private void OnWebImported(List<string> levels, List<string> towns, string report)
    {
        if (map == null) return;
        if (towns.Contains(map.name))
        {
            string p = OverworldBuilder.PathFor(map.name);
            if (File.Exists(p)) { string t = File.ReadAllText(p); if (t.Replace("\r", "") != OverworldMap.ToText(map)) { Load(t, "网页刚改了这张小镇 → 已换成新版本（↶ 退回）"); ShowNotification(new GUIContent("网页同步：小镇已更新")); } }
        }
        else if (levels.Count > 0) { Recheck(); ShowNotification(new GUIContent($"网页同步：{levels.Count} 个房间已更新，▶ 时会自动重建")); }
    }
    private void Blink() { if (flash.HasValue) { if (EditorApplication.timeSinceStartup > flashUntil) flash = null; Repaint(); } }
    private void OnFocus() { if (map != null) Recheck(); } // 从关卡工坊改完房间回来 → 状态刷新

    /// <summary>画上的门数字 ↔ 门列表：新画的门自动加一行（默认时间往后排），擦掉的门移除。</summary>
    private void SyncDoors()
    {
        var present = new HashSet<int>();
        for (int y = 0; y < map.H; y++) for (int x = 0; x < map.W; x++) { char c = map.At(x, y); if (OverworldCatalog.IsDoor(c)) present.Add(c - '0'); }
        map.doors.RemoveAll(d => !present.Contains(d.n));
        foreach (int n in present.OrderBy(n => n))
            if (map.DoorOf(n) == null)
            {
                int last = map.doors.Count > 0 ? map.doors.Max(d => d.minute) : 7 * 60;
                map.doors.Add(new OverworldMap.Door { n = n, minute = Mathf.Min(OverworldMap.LatestDoor, last + 150), room = LevelWorkshopModel.DefaultRoomName });
            }
        map.doors.Sort((a, b) => a.n.CompareTo(b.n));
    }

    private void OnGUI()
    {
        if (map == null) Load(DraftOrCurrent(), "打开");
        Hotkeys();
        Toolbar();
        EditorGUILayout.BeginHorizontal();
        Palette();
        Canvas();
        SidePanel();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(HoverText() + (status.Length > 0 ? "　｜　" + status : ""), EditorStyles.miniLabel);
    }

    private string HoverText()
    {
        if (hover.x < 0) return $"{map.W}×{map.H}　B 画笔 R 矩形 F 填充 E 橡皮 I 吸管（Alt+点也行）  1–9 门  ↔ 扩展地图  Ctrl+滚轮 缩放  中键拖动  Ctrl+Z/Y  Ctrl+S  F5 试玩";
        char c = map.At(hover.x, hover.y); var t = OverworldCatalog.Get(c);
        string s = $"({hover.x},{hover.y}) {c} {(t != null ? t.zh : "?")}";
        if (OverworldCatalog.IsDoor(c)) { var d = map.DoorOf(c - '0'); if (d != null) s += $" · {OverworldMap.Clock(d.minute)} → {d.room}"; }
        return s;
    }

    private void Toolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("新建", EditorStyles.toolbarButton, GUILayout.Width(40)))
        {
            var rows = OverworldMap.NewMap(40, 24);
            Load($"# Overworld: 新小镇\n# Goal: \n{string.Join("\n", rows)}\n", "新建 40×24（已放好家 M 和出生点 T）");
        }
        if (GUILayout.Button(new GUIContent("样板 ▾", "内置样板：星露小镇（4 户人家）/ 星露大镇（72×40，巨炮 滚石 水塔 + 3 连锁）/ 星露山镇（山脉 + 山洞 + 雷雨 + 泥石流）"), EditorStyles.toolbarDropDown, GUILayout.Width(52)))
        {
            var sm = new GenericMenu();
            sm.AddItem(new GUIContent("星露小镇（44×28，入门）"), false, () => Load(OverworldPack.SampleText, "载入样板 星露小镇"));
            sm.AddItem(new GUIContent("星露大镇（72×40，大机关 + 连锁）"), false, () => Load(OverworldPack.BigSampleText, "载入样板 星露大镇：右边'大机关 · 连锁'点一行就定位"));
            sm.AddItem(new GUIContent("星露山镇（72×40，山脉 山洞 闪电 泥石流）"), false, () => Load(OverworldPack.MountainSampleText, "载入样板 星露山镇：紫虚线 = 山洞配对，棕色 = 泥石流会冲到哪"));
            sm.ShowAsContext();
        }
        // S217：往大世界扩展（四边都能加 / 裁），网页"↔ 扩展"同一套规则
        if (GUILayout.Button(new GUIContent($"↔ 扩展 {map.W}×{map.H} ▾", $"把地图往外加大（新地是草地，最外圈自动种树）或裁掉一圈。最大 {OverworldMap.MaxW}×{OverworldMap.MaxH}"), EditorStyles.toolbarDropDown, GUILayout.Width(118))) ResizeMenu();
        if (GUILayout.Button("打开 ▾", EditorStyles.toolbarDropDown, GUILayout.Width(54)))
        {
            var menu = new GenericMenu();
            var list = OverworldBuilder.List();
            if (list.Count == 0) menu.AddDisabledItem(new GUIContent("（还没有保存的小镇）"));
            foreach (var e in list) { var p = e.path; menu.AddItem(new GUIContent(e.name), false, () => { Load(File.ReadAllText(p), "打开 " + p); EditorPrefs.SetString(OverworldBuilder.CurrentKey, p); }); }
            menu.ShowAsContext();
        }
        // S214：◀ ▶ 切换小镇（PageUp / PageDown），切走前自动存
        if (GUILayout.Button(new GUIContent("◀", "上一张小镇（PageUp）"), EditorStyles.toolbarButton, GUILayout.Width(22))) StepTown(-1);
        if (GUILayout.Button(new GUIContent("▶", "下一张小镇（PageDown）"), EditorStyles.toolbarButton, GUILayout.Width(22))) StepTown(1);
        if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(40))) status = "已保存到 " + OverworldBuilder.Save(map);
        if (GUILayout.Button(new GUIContent("导入", "小镇 .txt / 网页工作室的关卡包 .json（里面的房间一起进关卡库）"), EditorStyles.toolbarButton, GUILayout.Width(40)))
        {
            string p = EditorUtility.OpenFilePanel("导入小镇或关卡包", "", "txt,json");
            if (!string.IsNullOrEmpty(p))
            {
                string text = File.ReadAllText(p);
                var (n, rep) = OverworldBuilder.Import(text);
                var maps = OverworldPack.Parse(text);
                if (maps.Count > 0) Load(OverworldMap.ToText(maps[0]), "导入 " + maps[0].name);
                EditorUtility.DisplayDialog("导入", rep, "好");
            }
        }
        if (GUILayout.Button(new GUIContent("导出", "存成小镇 .txt（网页工作室的'大地图'页可以导入）"), EditorStyles.toolbarButton, GUILayout.Width(40)))
        {
            string p = EditorUtility.SaveFilePanel("导出小镇", "", LevelPack.SafeFileName(map.name) + ".txt", "txt");
            if (!string.IsNullOrEmpty(p)) { File.WriteAllText(p, OverworldMap.ToText(map)); status = "已导出 " + p; }
        }
        using (new EditorGUI.DisabledScope(undo.Count == 0)) if (GUILayout.Button(new GUIContent("↶", "撤销 Ctrl+Z"), EditorStyles.toolbarButton, GUILayout.Width(24))) Undo();
        using (new EditorGUI.DisabledScope(redo.Count == 0)) if (GUILayout.Button(new GUIContent("↷", "重做 Ctrl+Y"), EditorStyles.toolbarButton, GUILayout.Width(24))) Redo();
        GUILayout.Space(8);
        tool = (Tool)GUILayout.Toolbar((int)tool, new[] { new GUIContent("✎ 画笔", "B"), new GUIContent("▭ 矩形", "R"), new GUIContent("▦ 填充", "F"), new GUIContent("⌫ 橡皮", "E"), new GUIContent("◉ 吸管", "I（或按住 Alt 点一下）") }, EditorStyles.toolbarButton, GUILayout.Width(300));
        showLinks = GUILayout.Toggle(showLinks, new GUIContent("⚡ 关系线", "画布上画出：巨炮 → 靶心（大风偏移虚框）、滚石滚道、水塔淹没范围、连锁（黄线）。悬停一扇门 = 右边预览门里的房间"), EditorStyles.toolbarButton, GUILayout.Width(62));
        GUILayout.Space(8);
        cell = GUILayout.HorizontalSlider(cell, 4f, 28f, GUILayout.Width(80));
        if (GUILayout.Button(new GUIContent("⤢", "缩放到整张地图都看得见"), EditorStyles.toolbarButton, GUILayout.Width(22)))
            cell = Mathf.Clamp(Mathf.Floor(Mathf.Min((canvasView.width - 4) / map.W, (canvasView.height - 4) / map.H)), 4f, 28f);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(new GUIContent("🏠 关卡工坊", "做门里面的横版房间"), EditorStyles.toolbarButton, GUILayout.Width(80))) LevelWorkshopWindow.Open();
        if (GUILayout.Button(new GUIContent("🧪 测试中心", "一键体检全项目 / 快速测试模式 / 打包反馈（Ctrl+Alt+T）"), EditorStyles.toolbarButton, GUILayout.Width(80))) TestHubWindow.Open();
        GUI.backgroundColor = report != null && report.Playable ? new Color(0.5f, 1f, 0.5f) : Color.white;
        if (GUILayout.Button(new GUIContent("▶ 试玩小镇", "F5"), EditorStyles.toolbarButton, GUILayout.Width(80))) PlayTown();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    private void ResizeMenu()
    {
        var menu = new GenericMenu();
        void Add(string label, int l, int r, int t, int b) => menu.AddItem(new GUIContent(label), false, () => DoResize(l, r, t, b));
        Add("四周各 +8 格", 8, 8, 8, 8);
        Add("四周各 +16 格", 16, 16, 16, 16);
        menu.AddSeparator("");
        Add("右边 +16（往东）", 0, 16, 0, 0); Add("左边 +16（往西）", 16, 0, 0, 0);
        Add("上边 +16（往北）", 0, 0, 16, 0); Add("下边 +16（往南）", 0, 0, 0, 16);
        menu.AddSeparator("");
        Add("加倍：宽高都 ×2（往右下长）", 0, map.W, 0, map.H);
        menu.AddSeparator("");
        Add("裁掉四周各 4 格", -4, -4, -4, -4);
        menu.ShowAsContext();
    }

    /// <summary>S217：扩展 / 裁掉。有东西会被裁掉时先问；之后滚到老镇中间，不会一扩展就"找不到自己的镇"。</summary>
    private void DoResize(int l, int r, int t, int b)
    {
        var probe = OverworldMap.Parse(OverworldMap.ToText(map));
        var res = OverworldMap.Resize(probe, l, r, t, b);
        if (!res.ok) { EditorUtility.DisplayDialog("小镇尺寸", res.why, "好"); return; }
        if (res.lost > 0 && !EditorUtility.DisplayDialog("小镇尺寸", $"会裁掉 / 盖掉 {res.lost} 个格子（房子、门、道具等）。继续吗？（Ctrl+Z 可撤销）", "继续", "取消")) return;
        Snapshot();
        OverworldMap.Resize(map, l, r, t, b);
        Recheck();
        status = $"现在 {map.W}×{map.H}（Ctrl+Z 撤销）。新地是草地，外圈已种树；接着画路 = 和房子，门 1–9 画在房子下方一格";
        scroll = new Vector2(Mathf.Max(0, (l + (map.W - l - r) / 2f) * cell - canvasView.width / 2f), Mathf.Max(0, (t + (map.H - t - b) / 2f) * cell - canvasView.height / 2f));
        Repaint();
    }

    private void Palette()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(170));
        foreach (var t in OverworldCatalog.All)
        {
            if (t.c == '1') { DoorButtons(t); continue; }
            var r = GUILayoutUtility.GetRect(160, 22);
            bool on = brush == t.c;
            EditorGUI.DrawRect(new Rect(r.x, r.y + 2, 18, 18), new Color(t.r, t.g, t.b));
            if (GUI.Toggle(new Rect(r.x + 22, r.y, 138, 22), on, new GUIContent($"{t.c}  {t.zh}", t.what + "\n\n" + t.how), "Button") && !on) brush = t.c;
        }
        var cur = OverworldCatalog.Get(brush);
        if (cur != null) EditorGUILayout.HelpBox(cur.what + "\n" + cur.how, MessageType.None);
        EditorGUILayout.EndVertical();
    }

    private void DoorButtons(OverworldCatalog.Tile t)
    {
        EditorGUILayout.LabelField("房间门（数字 = 编号）", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        for (int n = 1; n <= 9; n++)
        {
            char c = (char)('0' + n);
            GUI.backgroundColor = new Color(t.r, t.g, t.b);
            if (GUILayout.Toggle(brush == c, c.ToString(), "Button", GUILayout.Width(16)) && brush != c) brush = c;
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void Canvas()
    {
        var ev = Event.current;
        // S212：Ctrl+滚轮缩放（以鼠标为中心）、中键拖动画布
        if (ev.type == EventType.ScrollWheel && (ev.control || ev.command))
        {
            float old = cell; cell = Mathf.Clamp(cell * (ev.delta.y > 0 ? 0.9f : 1.1f), 4f, 40f);
            scroll = (scroll + ev.mousePosition - canvasView.position) * (cell / old) - (ev.mousePosition - canvasView.position);
            ev.Use(); Repaint();
        }
        if (ev.type == EventType.MouseDrag && ev.button == 2) { scroll -= ev.delta; ev.Use(); Repaint(); }
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        var area = GUILayoutUtility.GetRect(map.W * cell, map.H * cell);
        var errCells = new HashSet<(int, int)>(report?.issues.Where(i => i.sev == OverworldMap.Sev.Error && i.x >= 0).Select(i => (i.x, i.y)) ?? Enumerable.Empty<(int, int)>());
        var route = new HashSet<(int, int)>();
        if (report?.schedule != null) { foreach (var s in report.schedule.stops) foreach (var c in s.path) route.Add((c.x, c.y)); if (report.schedule.homePath != null) foreach (var c in report.schedule.homePath) route.Add((c.x, c.y)); }
        var label = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(cell * 0.6f) };
        // S217：只画看得见的格子（192×128 的大世界也不卡）
        int vx0 = Mathf.Max(0, Mathf.FloorToInt(scroll.x / cell) - 1), vx1 = Mathf.Min(map.W - 1, Mathf.CeilToInt((scroll.x + Mathf.Max(canvasView.width, 400f)) / cell) + 1);
        int vr0 = Mathf.Max(0, Mathf.FloorToInt(scroll.y / cell) - 1), vr1 = Mathf.Min(map.H - 1, Mathf.CeilToInt((scroll.y + Mathf.Max(canvasView.height, 300f)) / cell) + 1);
        for (int y = map.H - 1 - vr1; y <= map.H - 1 - vr0; y++)
            for (int x = vx0; x <= vx1; x++)
            {
                char c = map.At(x, y);
                var t = OverworldCatalog.Get(c);
                var r = new Rect(area.x + x * cell, area.y + (map.H - 1 - y) * cell, cell - 1, cell - 1);
                EditorGUI.DrawRect(r, t != null ? new Color(t.r, t.g, t.b) : Color.magenta);
                if (route.Contains((x, y)) && !OverworldCatalog.Solid(c)) EditorGUI.DrawRect(new Rect(r.x + cell * 0.35f, r.y + cell * 0.35f, cell * 0.3f, cell * 0.3f), new Color(1f, 0.2f, 0.2f, 0.8f));
                if (errCells.Contains((x, y))) { EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2), Color.red); EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2, r.width, 2), Color.red); }
                if (OverworldCatalog.IsDoor(c) || "MT?niKOUXh^A".IndexOf(c) >= 0) GUI.Label(r, c.ToString(), label);
            }
        // S212：矩形拖动预览
        if (dragStart.HasValue && hover.x >= 0)
        {
            var a = dragStart.Value;
            int x0 = Mathf.Min(a.x, hover.x), x1 = Mathf.Max(a.x, hover.x), y0 = Mathf.Min(a.y, hover.y), y1 = Mathf.Max(a.y, hover.y);
            var pr = new Rect(area.x + x0 * cell, area.y + (map.H - 1 - y1) * cell, (x1 - x0 + 1) * cell, (y1 - y0 + 1) * cell);
            var bt = OverworldCatalog.Get(brush); EditorGUI.DrawRect(pr, bt != null ? new Color(bt.r, bt.g, bt.b, 0.55f) : new Color(1, 1, 1, 0.3f));
            GUI.Label(new Rect(pr.xMax + 2, pr.yMax, 60, 16), $"{x1 - x0 + 1}×{y1 - y0 + 1}", EditorStyles.miniBoldLabel);
        }
        // 悬停格描边
        if (hover.x >= 0) Outline(new Rect(area.x + hover.x * cell, area.y + (map.H - 1 - hover.y) * cell, cell - 1, cell - 1), new Color(1, 1, 1, 0.9f), 1);
        // 点问题 → 闪
        if (flash.HasValue && ((int)(EditorApplication.timeSinceStartup * 6) % 2 == 0))
            Outline(new Rect(area.x + flash.Value.x * cell - 3, area.y + (map.H - 1 - flash.Value.y) * cell - 3, cell + 5, cell + 5), Color.yellow, 3);
        // 时间滑条：他此刻在哪
        if (scrub >= 0 && report?.schedule != null)
        {
            var rl = OverworldBuilder.RulesFromTuning();
            var wh = OverworldGuide.MarioAt(map, report.schedule, scrub, rl.marioSpeed, rl.minutesPerSecond);
            var mr = new Rect(area.x + wh.cell.x * cell - cell * 0.2f, area.y + (map.H - 1 - wh.cell.y) * cell - cell * 0.2f, cell * 1.4f, cell * 1.4f);
            EditorGUI.DrawRect(mr, new Color(0.95f, 0.1f, 0.1f, 0.85f));
            GUI.Label(mr, "M", label);
        }
        foreach (var n in map.notes)
        {
            var r = new Rect(area.x + n.x * cell, area.y + (map.H - 1 - n.y) * cell, cell, cell);
            GUI.Label(r, new GUIContent("✎", n.text), label);
        }
        if (showLinks && ev.type == EventType.Repaint) Links(area);
        HandleMouse(area);
        EditorGUILayout.EndScrollView();
        if (ev.type == EventType.Repaint) canvasView = GUILayoutUtility.GetLastRect();
    }

    // ═════ S218：总管全局——关系线 + 大机关面板 + 天气预览 + 悬停门看房间 ═════
    private bool showLinks = true, propsOpen = true;
    private int weatherFrom = 1;
    private Vector2 C(Rect area, int x, int y) => new Vector2(area.x + (x + 0.5f) * cell, area.y + (map.H - 1 - y + 0.5f) * cell);

    private void Links(Rect area)
    {
        Handles.BeginGUI();
        foreach (var c in OverworldProps.All(map))
        {
            char k = map.At(c.x, c.y);
            if (k == 'K' && OverworldProps.Aim(map, c, out var tg, out int dir, out _))
            {
                foreach (var mz in OverworldProps.MuzzleCells(map, c, dir)) EditorGUI.DrawRect(new Rect(area.x + mz.x * cell, area.y + (map.H - 1 - mz.y) * cell, cell - 1, cell - 1), new Color(1f, 0.2f, 0.1f, 0.35f));
                Handles.color = new Color(1f, 0.35f, 0.3f, 0.9f); Handles.DrawAAPolyLine(3f, C(area, c.x, c.y), C(area, tg.x, tg.y));
                for (int w = 0; w < 4; w++) { var l = OverworldProps.Landing(map, tg, w); Outline(new Rect(area.x + l.x * cell + 2, area.y + (map.H - 1 - l.y) * cell + 2, cell - 5, cell - 5), new Color(1f, 0.6f, 0.5f, 0.6f), 1); } // 大风天可能的落点
            }
            else if (k == 'O')
                for (int d = 0; d < 4; d++) { var lane = OverworldProps.Lane(map, c, d); if (lane.Count < 2) continue; var e = lane[lane.Count - 1]; Handles.color = new Color(0.85f, 0.8f, 0.7f, 0.55f); Handles.DrawAAPolyLine(4f, C(area, c.x, c.y), C(area, e.x, e.y)); }
            else if (k == 'U') foreach (var f in OverworldProps.Flood(map, c, OverworldProps.FloodRadius)) EditorGUI.DrawRect(new Rect(area.x + f.x * cell + cell * 0.3f, area.y + (map.H - 1 - f.y) * cell + cell * 0.3f, cell * 0.4f, cell * 0.4f), new Color(0.3f, 0.55f, 1f, 0.55f));
            foreach (var t in OverworldProps.Triggers(map, c)) { Handles.color = new Color(1f, 0.85f, 0.2f, 0.95f); Handles.DrawAAPolyLine(2.5f, C(area, c.x, c.y) + Vector2.one * 2, C(area, t.x, t.y) + Vector2.one * 2); }
            if (k == 'K') for (int d = 0; d < 4; d++) { var mz = OverworldProps.MuzzleCells(map, c, d); if (mz.Count == 0) continue; Handles.color = new Color(0.45f, 1f, 0.55f, 0.8f); Handles.DrawAAPolyLine(1.5f, C(area, c.x, c.y), C(area, mz[mz.Count - 1].x, mz[mz.Count - 1].y)); } // S219：坐进去能瞄的方向
        }
        // S219：山洞配对（紫线）、泥石流（棕色）、雷雨天路灯能震响谁（淡蓝）
        var caves = OverworldMap.Find(map, 'h');
        for (int i = 0; i + 1 < caves.Count; i += 2) { Handles.color = new Color(0.8f, 0.5f, 1f, 0.9f); Handles.DrawDottedLine(C(area, caves[i].x, caves[i].y), C(area, caves[i + 1].x, caves[i + 1].y), 4f); }
        foreach (var hc in OverworldMap.Find(map, '^'))
        {
            var lane = OverworldProps.MudLane(map, hc); if (lane.Count == 0) continue;
            foreach (var q in lane) EditorGUI.DrawRect(new Rect(area.x + q.x * cell + cell * 0.2f, area.y + (map.H - 1 - q.y) * cell + cell * 0.2f, cell * 0.6f, cell * 0.6f), new Color(0.55f, 0.35f, 0.15f, 0.5f));
            Handles.color = new Color(0.6f, 0.37f, 0.15f, 0.9f); Handles.DrawAAPolyLine(3f, C(area, hc.x, hc.y), C(area, lane[lane.Count - 1].x, lane[lane.Count - 1].y));
        }
        foreach (var lc in OverworldMap.Find(map, 'i'))
        {
            var ts = OverworldProps.ChainTargets(map, lc.x + 0.5, lc.y + 0.5, OverworldProps.LightningRadius); ts.AddRange(OverworldProps.MudSources(map, lc.x + 0.5, lc.y + 0.5));
            foreach (var t in ts) { Handles.color = new Color(0.7f, 0.87f, 1f, 0.95f); Handles.DrawAAPolyLine(2f, C(area, lc.x, lc.y), C(area, t.x, t.y)); }
        }
        Handles.EndGUI();
        // 悬停门 → 画布旁边弹出门里房间的缩略图（不用切到关卡工坊）
        if (hover.x >= 0 && OverworldCatalog.IsDoor(map.At(hover.x, hover.y)))
        {
            var d = map.DoorOf(map.At(hover.x, hover.y) - '0'); var rows = d != null ? OverworldBuilder.ResolveRoom(d.room) : null;
            if (rows != null && rows.Length > 0)
            {
                float px = Mathf.Max(2f, Mathf.Min(6f, 300f / rows[0].Length)); var at = C(area, hover.x, hover.y) + new Vector2(cell, cell);
                var box = new Rect(at.x, at.y, rows[0].Length * px + 8, rows.Length * px + 24);
                EditorGUI.DrawRect(box, new Color(0.08f, 0.08f, 0.1f, 0.95f));
                GUI.Label(new Rect(box.x + 4, box.y + 2, box.width, 18), $"门 {d.n} · {OverworldMap.Clock(d.minute)} · {d.room}", EditorStyles.whiteMiniLabel);
                for (int r = 0; r < rows.Length; r++) for (int x = 0; x < rows[r].Length; x++) { char ch = rows[r][x]; if (ch == '.' || ch == ' ') continue; EditorGUI.DrawRect(new Rect(box.x + 4 + x * px, box.y + 20 + r * px, px, px), ElementCatalog.EditorColor(ch)); }
            }
        }
    }

    private void PropsPanel()
    {
        EditorGUILayout.Space();
        propsOpen = EditorGUILayout.Foldout(propsOpen, "⚡ 大机关 · 连锁（点一行 = 画布定位）", true, EditorStyles.foldoutHeader);
        if (!propsOpen) return;
        foreach (var l in OverworldProps.Describe(map))
        {
            EditorGUILayout.LabelField(l.text, EditorStyles.wordWrappedMiniLabel);
            var hr = GUILayoutUtility.GetLastRect();
            if (l.x >= 0) { EditorGUIUtility.AddCursorRect(hr, MouseCursor.Link); if (Event.current.type == EventType.MouseDown && hr.Contains(Event.current.mousePosition)) { Locate(l.x, l.y); Event.current.Use(); } }
        }
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("🌦 天气（每天 06:00 公布，同一天永远一样）", EditorStyles.miniBoldLabel);
        if (GUILayout.Button("◀", EditorStyles.miniButtonLeft, GUILayout.Width(22))) weatherFrom = Mathf.Max(1, weatherFrom - 7);
        if (GUILayout.Button("▶", EditorStyles.miniButtonRight, GUILayout.Width(22))) weatherFrom += 7;
        EditorGUILayout.EndHorizontal();
        foreach (var w in OverworldEvents.Preview(map, weatherFrom, 7)) EditorGUILayout.LabelField(w, EditorStyles.wordWrappedMiniLabel);
    }

    private static void Outline(Rect r, Color c, float t)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c); EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c); EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    private void HandleMouse(Rect area)
    {
        var e = Event.current;
        if (e.button == 2) return; // 中键 = 拖动画布
        if (!area.Contains(e.mousePosition))
        {
            if (e.type == EventType.MouseMove && hover.x >= 0) { hover = new Vector2Int(-1, -1); Repaint(); }
            if (e.type == EventType.MouseUp && dragStart.HasValue) { dragStart = null; Repaint(); }
            return;
        }
        int x = Mathf.FloorToInt((e.mousePosition.x - area.x) / cell), y = map.H - 1 - Mathf.FloorToInt((e.mousePosition.y - area.y) / cell);
        if (x < 0 || y < 0 || x >= map.W || y >= map.H) return;
        if ((e.type == EventType.MouseMove || e.type == EventType.MouseDrag) && (hover.x != x || hover.y != y)) { hover = new Vector2Int(x, y); Repaint(); }
        char paint = tool == Tool.Erase || e.button == 1 ? '.' : brush;
        // S212：吸管（I 或 Alt+点）：拿起这一格当画笔，然后回到画笔
        if (e.type == EventType.MouseDown && e.button == 0 && (tool == Tool.Pick || e.alt))
        {
            brush = map.At(x, y); if (tool == Tool.Pick) tool = Tool.Brush;
            status = $"吸管：{brush} {OverworldCatalog.Get(brush)?.zh}"; e.Use(); Repaint(); return;
        }
        if (tool == Tool.Pick) return;
        if (e.type == EventType.MouseDown)
        {
            Snapshot();
            if (tool == Tool.Rect && e.button == 0) dragStart = new Vector2Int(x, y);
            else if (tool == Tool.Fill && e.button == 0) { Flood(x, y, paint); Recheck(); }
            else { Set(x, y, paint); Recheck(); }
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && (tool == Tool.Brush || tool == Tool.Erase || e.button == 1)) { Set(x, y, paint); Recheck(); e.Use(); }
        else if (e.type == EventType.MouseUp && dragStart.HasValue)
        {
            var a = dragStart.Value; dragStart = null;
            for (int xx = Mathf.Min(a.x, x); xx <= Mathf.Max(a.x, x); xx++) for (int yy = Mathf.Min(a.y, y); yy <= Mathf.Max(a.y, y); yy++) Set(xx, yy, paint);
            Recheck(); e.Use();
        }
    }

    private void Set(int x, int y, char c)
    {
        // 唯一的格子（家、出生点、每个门数字）：画新的就把旧的擦掉
        if (c == 'M' || c == 'T' || OverworldCatalog.IsDoor(c))
            foreach (var old in OverworldMap.Find(map, c)) Write(old.x, old.y, '.');
        Write(x, y, c);
    }

    private void Write(int x, int y, char c)
    {
        int r = map.H - 1 - y;
        var ch = map.rows[r].ToCharArray(); ch[x] = c; map.rows[r] = new string(ch);
    }

    private void Flood(int x, int y, char c)
    {
        char from = map.At(x, y);
        if (from == c || c == 'M' || c == 'T' || OverworldCatalog.IsDoor(c)) { Set(x, y, c); return; }
        var q = new Queue<Vector2Int>(); q.Enqueue(new Vector2Int(x, y)); int guard = 0;
        while (q.Count > 0 && guard++ < 10000)
        {
            var p = q.Dequeue();
            if (p.x < 0 || p.y < 0 || p.x >= map.W || p.y >= map.H || map.At(p.x, p.y) != from) continue;
            Write(p.x, p.y, c);
            q.Enqueue(p + Vector2Int.left); q.Enqueue(p + Vector2Int.right); q.Enqueue(p + Vector2Int.up); q.Enqueue(p + Vector2Int.down);
        }
    }

    private void SidePanel()
    {
        side = EditorGUILayout.BeginScrollView(side, GUILayout.Width(330));
        EditorGUI.BeginChangeCheck();
        map.name = EditorGUILayout.TextField("名字", map.name);
        map.goal = EditorGUILayout.TextField("玩法说明", map.goal);
        if (EditorGUI.EndChangeCheck()) Repaint();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("门（马里奥按时间顺序去）", EditorStyles.boldLabel);
        var rooms = OverworldBuilder.RoomNames();
        foreach (var d in map.doors)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("门 " + d.n, GUILayout.Width(40));
            string clock = EditorGUILayout.DelayedTextField(OverworldMap.Clock(d.minute), GUILayout.Width(52));
            if (OverworldMap.TryParseClock(clock, out int mm) && mm != d.minute) { Snapshot(); d.minute = mm; Recheck(); }
            int idx = Mathf.Max(0, rooms.IndexOf(d.room));
            if (rooms.IndexOf(d.room) < 0 && d.room.Length > 0) { rooms.Add(d.room); idx = rooms.Count - 1; }
            int ni = EditorGUILayout.Popup(idx, rooms.ToArray());
            if (ni != idx || d.room.Length == 0) { Snapshot(); d.room = rooms[ni]; Recheck(); }
            // S212：◎ 在画布上找到这扇门；✎ 直接打开门里的房间去改
            if (GUILayout.Button(new GUIContent("◎", "在画布上找到门 " + d.n), EditorStyles.miniButtonLeft, GUILayout.Width(22)))
            { var dc = OverworldMap.Find(map, (char)('0' + d.n)); if (dc.Count > 0) Locate(dc[0].x, dc[0].y); }
            if (GUILayout.Button(new GUIContent("✎", "在关卡工坊里打开「" + d.room + "」\n改完：关卡库 → 存入关卡库（同名覆盖）→ 回来 ▶ 会自动重建这个房间"), EditorStyles.miniButtonRight, GUILayout.Width(22)))
            { if (!LevelWorkshopWindow.OpenRoom(d.room)) ShowNotification(new GUIContent("找不到房间「" + d.room + "」")); }
            EditorGUILayout.EndHorizontal();
        }
        if (map.doors.Count == 0) EditorGUILayout.HelpBox("在画布上用 1–9 画门（画在房子最下面一排的下方一格）。", MessageType.Info);

        LedgerPanel();
        PropsPanel();
        EditorGUILayout.Space();
        // S211：场景状态——你只管画，▶ 时自动只重建变了的房间
        bool stale = scenesStale;
        EditorGUILayout.HelpBox(stale ? "场景：需要更新（点 ▶ 试玩小镇，或在 Town 场景直接按 Unity 的 ▶，都会自动只重建变了的房间）"
                                      : "场景：已是最新 ✓ 进门 / 回小镇都是淡出 → 后台加载 → 淡入", stale ? MessageType.Warning : MessageType.Info);
        EditorGUILayout.Space();
        if (report != null)
        {
            EditorGUILayout.LabelField(report.Headline, EditorStyles.boldLabel);
            foreach (var i in report.issues)
            {
                EditorGUILayout.HelpBox(i.ToString() + (i.x >= 0 ? "　（点我定位）" : ""), i.sev == OverworldMap.Sev.Error ? MessageType.Error : i.sev == OverworldMap.Sev.Warn ? MessageType.Warning : MessageType.Info);
                var hr = GUILayoutUtility.GetLastRect();
                if (i.x >= 0) { EditorGUIUtility.AddCursorRect(hr, MouseCursor.Link); if (Event.current.type == EventType.MouseDown && hr.Contains(Event.current.mousePosition)) { Locate(i.x, i.y); Event.current.Use(); } }
            }
            if (report.schedule != null && report.schedule.stops.Count > 0)
            {
                EditorGUILayout.LabelField("马里奥的一天（没人捣乱时；红点 = 他的路线）", EditorStyles.boldLabel);
                var sc = report.schedule; var rules = OverworldBuilder.RulesFromTuning();
                for (int k = 0; k < sc.stops.Count; k++)
                {
                    var s = sc.stops[k];
                    double lead = OverworldMap.AmbushLead(sc, k, rules.minutesPerSecond);
                    EditorGUILayout.LabelField($"门 {s.door.n}：{OverworldMap.Clock(s.depart)} 出发 → {OverworldMap.Clock(s.arrive)} 到 → {OverworldMap.Clock(s.leave)} 出来　你能提前 {lead:0} 秒", EditorStyles.wordWrappedMiniLabel);
                }
                EditorGUILayout.LabelField($"{OverworldMap.Clock(sc.homeArrive)} 到家", EditorStyles.miniLabel);
                // S214：🤖 模拟玩家——你画的这张小镇好不好玩（S213 的机器人玩家，同一份规则跑一天）
                EditorGUILayout.Space();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || !report.Playable))
                    if (GUILayout.Button(new GUIContent("🤖 模拟玩家玩一天", "4 种玩家各玩 3 天：会躲的该全赢、站着不躲不该全赢、反应慢的别老被抓。约 1 秒"))) botLines = RunBots();
                foreach (var l in botLines) EditorGUILayout.LabelField(l, EditorStyles.wordWrappedMiniLabel);
                // S212：时间滑条——拖一拖，画布上的红 M 就是他那一刻在哪
                EditorGUILayout.Space();
                bool on = EditorGUILayout.ToggleLeft("⏱ 时间滑条（看他几点在哪）", scrub >= 0);
                if (on && scrub < 0) scrub = OverworldMap.DayStart; else if (!on) scrub = -1f;
                if (scrub >= 0)
                {
                    scrub = GUILayout.HorizontalSlider(scrub, OverworldMap.DayStart, OverworldMap.DayEnd);
                    var wh = OverworldGuide.MarioAt(map, sc, scrub, rules.marioSpeed, rules.minutesPerSecond);
                    EditorGUILayout.LabelField($"{OverworldMap.Clock(scrub)}　{wh.what}", EditorStyles.wordWrappedMiniLabel);
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }
}
