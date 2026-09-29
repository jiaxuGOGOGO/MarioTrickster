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
    private enum Tool { Brush, Rect, Fill, Erase }
    private OverworldMap.Map map;
    private char brush = '=';
    private Tool tool = Tool.Brush;
    private Vector2 scroll, side;
    private float cell = 16f;
    private Vector2Int? dragStart;
    private OverworldMap.Report report;
    private string status = "";
    private readonly Stack<string> undo = new Stack<string>();

    [MenuItem("MarioTrickster/Town Workshop (小镇工坊) %&o", false, 2)]
    public static void Open()
    {
        var w = GetWindow<OverworldWorkshopWindow>("小镇工坊");
        w.minSize = new Vector2(900, 520);
        if (w.map == null) w.Load(OverworldBuilder.CurrentText, "打开");
    }

    private void Load(string text, string why)
    {
        if (map != null) undo.Push(OverworldMap.ToText(map));
        map = OverworldMap.Parse(text);
        if (map.W == 0) map = OverworldMap.Parse(OverworldPack.SampleText);
        Recheck(); status = why;
    }

    private void Snapshot() { undo.Push(OverworldMap.ToText(map)); if (undo.Count > 60) { var a = undo.ToArray().Take(60).Reverse(); undo.Clear(); foreach (var s in a) undo.Push(s); } }
    private void Recheck() { report = OverworldMap.Check(map, OverworldBuilder.RulesFromTuning(), OverworldBuilder.RoomProblem); SyncDoors(); Repaint(); }

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
        if (map == null) Load(OverworldBuilder.CurrentText, "打开");
        Toolbar();
        EditorGUILayout.BeginHorizontal();
        Palette();
        Canvas();
        SidePanel();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
    }

    private void Toolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("新建", EditorStyles.toolbarButton, GUILayout.Width(40)))
        {
            var rows = OverworldMap.NewMap(40, 24);
            Load($"# Overworld: 新小镇\n# Goal: \n{string.Join("\n", rows)}\n", "新建 40×24（已放好家 M 和出生点 T）");
        }
        if (GUILayout.Button(new GUIContent("样板", "内置样板：星露小镇（4 户人家）"), EditorStyles.toolbarButton, GUILayout.Width(40))) Load(OverworldPack.SampleText, "载入样板 星露小镇");
        if (GUILayout.Button("打开 ▾", EditorStyles.toolbarDropDown, GUILayout.Width(54)))
        {
            var menu = new GenericMenu();
            var list = OverworldBuilder.List();
            if (list.Count == 0) menu.AddDisabledItem(new GUIContent("（还没有保存的小镇）"));
            foreach (var e in list) { var p = e.path; menu.AddItem(new GUIContent(e.name), false, () => { Load(File.ReadAllText(p), "打开 " + p); EditorPrefs.SetString(OverworldBuilder.CurrentKey, p); }); }
            menu.ShowAsContext();
        }
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
        if (GUILayout.Button("撤销", EditorStyles.toolbarButton, GUILayout.Width(40)) && undo.Count > 0) { map = OverworldMap.Parse(undo.Pop()); Recheck(); }
        GUILayout.Space(8);
        tool = (Tool)GUILayout.Toolbar((int)tool, new[] { "✎ 画笔", "▭ 矩形", "▦ 填充", "⌫ 橡皮" }, EditorStyles.toolbarButton, GUILayout.Width(240));
        GUILayout.Space(8);
        cell = GUILayout.HorizontalSlider(cell, 8f, 28f, GUILayout.Width(80));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(new GUIContent("🏠 关卡工坊", "做门里面的横版房间"), EditorStyles.toolbarButton, GUILayout.Width(80))) EditorApplication.ExecuteMenuItem("MarioTrickster/Level Workshop (关卡工坊)");
        GUI.backgroundColor = report != null && report.Playable ? new Color(0.5f, 1f, 0.5f) : Color.white;
        if (GUILayout.Button("▶ 试玩小镇", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            OverworldBuilder.Save(map);
            if (OverworldBuilder.BuildAll(OverworldMap.ToText(map), out string rep)) EditorApplication.isPlaying = true;
            else EditorUtility.DisplayDialog("小镇", rep, "好");
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
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
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        var area = GUILayoutUtility.GetRect(map.W * cell, map.H * cell);
        var errCells = new HashSet<(int, int)>(report?.issues.Where(i => i.sev == OverworldMap.Sev.Error && i.x >= 0).Select(i => (i.x, i.y)) ?? Enumerable.Empty<(int, int)>());
        var route = new HashSet<(int, int)>();
        if (report?.schedule != null) { foreach (var s in report.schedule.stops) foreach (var c in s.path) route.Add((c.x, c.y)); if (report.schedule.homePath != null) foreach (var c in report.schedule.homePath) route.Add((c.x, c.y)); }
        var label = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(cell * 0.6f) };
        for (int y = 0; y < map.H; y++)
            for (int x = 0; x < map.W; x++)
            {
                char c = map.At(x, y);
                var t = OverworldCatalog.Get(c);
                var r = new Rect(area.x + x * cell, area.y + (map.H - 1 - y) * cell, cell - 1, cell - 1);
                EditorGUI.DrawRect(r, t != null ? new Color(t.r, t.g, t.b) : Color.magenta);
                if (route.Contains((x, y)) && !OverworldCatalog.Solid(c)) EditorGUI.DrawRect(new Rect(r.x + cell * 0.35f, r.y + cell * 0.35f, cell * 0.3f, cell * 0.3f), new Color(1f, 0.2f, 0.2f, 0.8f));
                if (errCells.Contains((x, y))) { EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2), Color.red); EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2, r.width, 2), Color.red); }
                if (OverworldCatalog.IsDoor(c) || c == 'M' || c == 'T' || c == '?' || c == 'n' || c == 'i') GUI.Label(r, c.ToString(), label);
            }
        foreach (var n in map.notes)
        {
            var r = new Rect(area.x + n.x * cell, area.y + (map.H - 1 - n.y) * cell, cell, cell);
            GUI.Label(r, new GUIContent("✎", n.text), label);
        }
        HandleMouse(area);
        EditorGUILayout.EndScrollView();
    }

    private void HandleMouse(Rect area)
    {
        var e = Event.current;
        if (!area.Contains(e.mousePosition)) return;
        int x = Mathf.FloorToInt((e.mousePosition.x - area.x) / cell), y = map.H - 1 - Mathf.FloorToInt((e.mousePosition.y - area.y) / cell);
        if (x < 0 || y < 0 || x >= map.W || y >= map.H) return;
        char paint = tool == Tool.Erase || e.button == 1 ? '.' : brush;
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
            EditorGUILayout.EndHorizontal();
        }
        if (map.doors.Count == 0) EditorGUILayout.HelpBox("在画布上用 1–9 画门（画在房子最下面一排的下方一格）。", MessageType.Info);

        EditorGUILayout.Space();
        if (report != null)
        {
            EditorGUILayout.LabelField(report.Headline, EditorStyles.boldLabel);
            foreach (var i in report.issues)
                EditorGUILayout.HelpBox(i.ToString(), i.sev == OverworldMap.Sev.Error ? MessageType.Error : i.sev == OverworldMap.Sev.Warn ? MessageType.Warning : MessageType.Info);
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
            }
        }
        EditorGUILayout.EndScrollView();
    }
}
