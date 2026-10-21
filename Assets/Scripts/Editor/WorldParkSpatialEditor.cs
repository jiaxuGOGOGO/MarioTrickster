using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 世界工坊 explore 节点的局部点位编辑器。
/// 只编辑当前节点的 points（以及“新调查点”同时产生的一条明确草稿线索），
/// 不触碰原小镇地图或任何用户素材。
/// </summary>
public static class WorldParkSpatialEditor
{
    private const float BaseCellSize = 18f;
    private const float MinZoom = 0.35f;
    private const float MaxZoom = 2.2f;
    private const int HistoryLimit = 64;

    internal enum Tool
    {
        Select = 0,
        AddClue = 1,
        AddExit = 2,
        MoveSelected = 3,
        AddChaser = 4
    }

    /// <summary>由窗口序列化的视图状态；撤销栈只在本次窗口会话有效。</summary>
    [Serializable]
    public sealed class State
    {
        [SerializeField] private int selectedPoint = -1;
        [SerializeField] private int tool;
        [SerializeField] private float zoom = 1f;
        [SerializeField] private Vector2 pan = new Vector2(10f, 10f);
        [SerializeField] private int hoverX = -1;
        [SerializeField] private int hoverY = -1;
        [SerializeField] private string message = "点选已有标记；“+ 调查点”或“+ 出口”后点击可通行格。";
        [SerializeField] private string context = "";
        [NonSerialized] private readonly List<Change> undo = new List<Change>();
        [NonSerialized] private readonly List<Change> redo = new List<Change>();

        internal int SelectedPoint { get { return selectedPoint; } set { selectedPoint = value; } }
        internal Tool CurrentTool { get { return (Tool)tool; } set { tool = (int)value; } }
        internal float Zoom { get { return zoom; } set { zoom = Mathf.Clamp(value, MinZoom, MaxZoom); } }
        internal Vector2 Pan { get { return pan; } set { pan = value; } }
        internal int HoverX { get { return hoverX; } set { hoverX = value; } }
        internal int HoverY { get { return hoverY; } set { hoverY = value; } }
        internal string Message { get { return message ?? ""; } set { message = value ?? ""; } }
        internal string Context { get { return context ?? ""; } set { context = value ?? ""; } }
        internal List<Change> Undo { get { return undo; } }
        internal List<Change> Redo { get { return redo; } }

        /// <summary>切换草稿/导入包时由窗口清理局部历史，避免跨节点撤销。</summary>
        public void Reset()
        {
            selectedPoint = -1;
            tool = (int)Tool.Select;
            zoom = 1f;
            pan = new Vector2(10f, 10f);
            hoverX = hoverY = -1;
            context = "";
            message = "点选已有标记；“+ 调查点”或“+ 出口”后点击可通行格。";
            undo.Clear();
            redo.Clear();
        }
    }

    internal sealed class Change
    {
        public List<WorldParkPack.ExplorePoint> before;
        public List<WorldParkPack.ExplorePoint> after;
        // 仅“新调查点”会创建此草稿线索；普通点位编辑绝不回滚作者已有线索文本。
        public WorldParkPack.Clue createdClue;
    }

    /// <summary>
    /// 绘制当前 explore 节点的地图、点位、表单和局部撤销。所有修改直接写入传入对象。
    /// </summary>
    public static void Draw(WorldParkPack.Venue venue, WorldParkPack.Node node, State state, Action changed)
    {
        if (venue == null || node == null) return;
        if (state == null) return;
        EnsureLists(venue, node);
        ResetForDifferentMap(venue, node, state);
        ClampSelection(node, state);

        GUILayout.Space(7);
        EditorGUILayout.LabelField("探索点位画布（x 左起 0，y 下起 0）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("地图直接读取本节点 mapText，标记直接写入 points。滚轮只在按 Ctrl/Cmd 时缩放，绝不会摆放点位；中键拖动平移。", MessageType.None);
        DrawToolbar(venue, node, state, changed);

        OverworldMap.Map map = OverworldMap.Parse(node.mapText ?? "");
        if (map.W <= 0 || map.H <= 0)
        {
            EditorGUILayout.HelpBox("先在上方填写至少一行地图文本，画布才能检查可通行格。", MessageType.Warning);
            DrawSelectedForm(venue, node, state, changed);
            return;
        }

        float preferredHeight = Mathf.Clamp(map.H * CellSize(state) + 20f, 230f, 440f);
        Rect view = GUILayoutUtility.GetRect(260f, preferredHeight, GUILayout.ExpandWidth(true));
        GUI.Box(view, GUIContent.none, EditorStyles.helpBox);
        DrawCanvas(view, map, venue, node, state, changed);
        DrawCanvasFooter(map, node, state);
        DrawSelectedForm(venue, node, state, changed);
    }

    /// <summary>
    /// 与 WorldParkPack.Validate 一致：只允许放在地图边界内、可通行、且从唯一 T 出生点可达的格子。
    /// 可供 EditMode 纯逻辑测试；不依赖窗口状态或 Unity 场景。
    /// </summary>
    public static bool TryGetPlacementCell(string mapText, int x, int y, out string message)
    {
        return TryGetPlacementCell(OverworldMap.Parse(mapText ?? ""), x, y, out message);
    }

    /// <summary>与字符串重载相同，便于调用方复用已解析地图。</summary>
    public static bool TryGetPlacementCell(OverworldMap.Map map, int x, int y, out string message)
    {
        message = "";
        if (map == null || map.W <= 0 || map.H <= 0)
        {
            message = "地图为空，不能放置点位";
            return false;
        }
        if (x < 0 || y < 0 || x >= map.W || y >= map.H)
        {
            message = "坐标 (" + x + "," + y + ") 超出地图 " + map.W + "×" + map.H;
            return false;
        }
        if (!OverworldMap.Walkable(map, x, y))
        {
            message = "格 (" + x + "," + y + ") 是阻挡地形，不能放置点位";
            return false;
        }

        List<OverworldMap.Cell> spawns = OverworldMap.Find(map, 'T');
        if (spawns.Count != 1)
        {
            message = "地图需要且只能有一个可通行的 T 出生点，当前无法验证点位可达性";
            return false;
        }
        if (OverworldMap.Path(map, spawns[0], new OverworldMap.Cell(x, y)) == null)
        {
            message = "格 (" + x + "," + y + ") 虽可通行，但从 T 出生点不可达";
            return false;
        }

        message = "格 (" + x + "," + y + ") 可通行，且可从 T 出生点到达";
        return true;
    }

    /// <summary>按 points 列表稳定返回某格上的第一个点；同格旧点仍可由下方表单分别编辑。</summary>
    public static int PointIndexAt(IList<WorldParkPack.ExplorePoint> points, int x, int y)
    {
        if (points == null) return -1;
        for (int i = 0; i < points.Count; i++)
        {
            WorldParkPack.ExplorePoint point = points[i];
            if (point != null && point.x == x && point.y == y) return i;
        }
        return -1;
    }

    private static void DrawToolbar(WorldParkPack.Venue venue, WorldParkPack.Node node, State state, Action changed)
    {
        EditorGUILayout.BeginHorizontal();
        ToolButton("点选", Tool.Select, "点选画布内已有点位，或在下方表单编辑", state);
        ToolButton("+ 调查点", Tool.AddClue, "下一次左键点击可通行格：创建调查点和一条明确未写的 interact 线索草稿", state);
        ToolButton("+ 出口", Tool.AddExit, "下一次左键点击可通行格：创建出口草稿（默认 win，随后在表单修改）", state);
        ToolButton("+ 追逐者", Tool.AddChaser, "下一次左键点击可通行格：放一个“鬼”（追逐者），默认速度 3.4 格/秒、视距 7 格，原地守望；在表单里加巡逻格", state);
        bool hasSelection = Selected(node, state) != null;
        EditorGUI.BeginDisabledGroup(!hasSelection);
        if (GUILayout.Button("移动所选", EditorStyles.miniButton, GUILayout.Width(66)))
        {
            state.CurrentTool = Tool.MoveSelected;
            state.Message = "请左键点击一个可通行格来移动所选点位。";
        }
        if (GUILayout.Button("删除所选", EditorStyles.miniButton, GUILayout.Width(66)))
        {
            DeleteSelected(venue, node, state, changed);
        }
        EditorGUI.EndDisabledGroup();
        GUILayout.FlexibleSpace();
        EditorGUI.BeginDisabledGroup(state.Undo.Count == 0);
        if (GUILayout.Button("撤销", EditorStyles.miniButton, GUILayout.Width(44))) Undo(venue, node, state, changed);
        EditorGUI.EndDisabledGroup();
        EditorGUI.BeginDisabledGroup(state.Redo.Count == 0);
        if (GUILayout.Button("重做", EditorStyles.miniButton, GUILayout.Width(44))) Redo(venue, node, state, changed);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("缩小", EditorStyles.miniButton, GUILayout.Width(43))) state.Zoom = state.Zoom / 1.2f;
        EditorGUILayout.LabelField("缩放 " + Mathf.RoundToInt(state.Zoom * 100f) + "%", EditorStyles.miniLabel, GUILayout.Width(74));
        if (GUILayout.Button("放大", EditorStyles.miniButton, GUILayout.Width(43))) state.Zoom = state.Zoom * 1.2f;
        if (GUILayout.Button("复位视图", EditorStyles.miniButton, GUILayout.Width(62)))
        {
            state.Zoom = 1f;
            state.Pan = new Vector2(10f, 10f);
            state.Message = "已复位缩放和平移。";
        }
        GUILayout.FlexibleSpace();
        string tool = state.CurrentTool == Tool.AddClue ? "等待放置调查点" :
            state.CurrentTool == Tool.AddExit ? "等待放置出口" :
            state.CurrentTool == Tool.AddChaser ? "等待放置追逐者" :
            state.CurrentTool == Tool.MoveSelected ? "等待移动所选点" : "点选模式";
        EditorGUILayout.LabelField(tool, EditorStyles.miniLabel, GUILayout.Width(112));
        EditorGUILayout.EndHorizontal();
    }

    private static void ToolButton(string text, Tool tool, string help, State state)
    {
        bool was = state.CurrentTool == tool;
        if (GUILayout.Toggle(was, new GUIContent(text, help), "Button", GUILayout.Width(56)))
        {
            if (!was)
            {
                state.CurrentTool = tool;
                state.Message = tool == Tool.AddClue ? "请左键点击一个可通行且从 T 可达的格子。" :
                    tool == Tool.AddExit || tool == Tool.AddChaser ? "请左键点击一个可通行且从 T 可达的格子。" : "请点选一个已有点位。";
            }
        }
        else if (was)
        {
            state.CurrentTool = Tool.Select;
        }
    }

    private static void DrawCanvas(Rect view, OverworldMap.Map map, WorldParkPack.Venue venue, WorldParkPack.Node node, State state, Action changed)
    {
        float cell = CellSize(state);
        int x0 = Mathf.Clamp(Mathf.FloorToInt((-state.Pan.x) / cell), 0, map.W - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((view.width - state.Pan.x) / cell), 0, map.W - 1);
        int row0 = Mathf.Clamp(Mathf.FloorToInt((-state.Pan.y) / cell), 0, map.H - 1);
        int row1 = Mathf.Clamp(Mathf.CeilToInt((view.height - state.Pan.y) / cell), 0, map.H - 1);

        for (int row = row0; row <= row1; row++)
        {
            int y = map.H - 1 - row;
            for (int x = x0; x <= x1; x++)
            {
                Rect cellRect = CellRect(view, map, state, cell, x, y);
                char tile = map.At(x, y);
                EditorGUI.DrawRect(cellRect, TileColor(tile));
                if (cell >= 11f) GUI.Label(cellRect, tile.ToString(), TileStyle());
                if (cell >= 9f) DrawOutline(cellRect, new Color(0f, 0f, 0f, 0.16f), 1f);
            }
        }

        DrawPoints(view, map, node, state, cell);
        HandleCanvasInput(view, map, venue, node, state, changed, cell);
    }

    private static void DrawPoints(Rect view, OverworldMap.Map map, WorldParkPack.Node node, State state, float cell)
    {
        for (int i = 0; i < node.points.Count; i++)
        {
            WorldParkPack.ExplorePoint point = node.points[i];
            if (point == null || point.x < 0 || point.y < 0 || point.x >= map.W || point.y >= map.H) continue;
            Rect cellRect = CellRect(view, map, state, cell, point.x, point.y);
            if (!cellRect.Overlaps(view)) continue;
            int sameCell = 0;
            for (int j = 0; j < i; j++)
                if (node.points[j] != null && node.points[j].x == point.x && node.points[j].y == point.y) sameCell++;
            float inset = Mathf.Max(2f, cell * (0.20f + sameCell * 0.08f));
            Rect marker = new Rect(cellRect.x + inset, cellRect.y + inset, Mathf.Max(4f, cell - inset * 2f), Mathf.Max(4f, cell - inset * 2f));
            bool clue = string.Equals(point.kind, "clue", StringComparison.OrdinalIgnoreCase);
            bool chaser = point.kind == "chaser";
            Color color = chaser ? new Color(0.95f, 0.22f, 0.25f, 0.96f) : clue ? new Color(0.22f, 0.62f, 1f, 0.96f) : new Color(1f, 0.72f, 0.18f, 0.96f);
            EditorGUI.DrawRect(marker, color);
            if (i == state.SelectedPoint) DrawOutline(cellRect, Color.white, Mathf.Max(1f, cell * 0.10f));
            if (cell >= 16f) GUI.Label(cellRect, chaser ? "鬼" : clue ? "?" : "→", MarkerStyle());
            if (chaser && point.route != null)
                for (int r = 0; r < point.route.Count; r++)
                {
                    int rx, ry;
                    if (!WorldParkPack.TryChaserCell(point.route[r], out rx, out ry) || rx < 0 || ry < 0 || rx >= map.W || ry >= map.H) continue;
                    Rect rc = CellRect(view, map, state, cell, rx, ry);
                    float d = Mathf.Max(3f, cell * 0.3f);
                    EditorGUI.DrawRect(new Rect(rc.x + rc.width / 2f - d / 2f, rc.y + rc.height / 2f - d / 2f, d, d), new Color(0.95f, 0.22f, 0.25f, i == state.SelectedPoint ? 0.9f : 0.45f));
                    if (cell >= 22f) GUI.Label(rc, (r + 1).ToString(), MarkerStyle());
                }
        }
    }

    private static void HandleCanvasInput(Rect view, OverworldMap.Map map, WorldParkPack.Venue venue, WorldParkPack.Node node, State state, Action changed, float cell)
    {
        Event e = Event.current;
        if (e == null) return;

        // ScrollWheel first and return: wheel input never reaches placement code.
        if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition))
        {
            if (e.control || e.command)
            {
                float factor = e.delta.y > 0f ? 1f / 1.15f : 1.15f;
                ZoomAt(state, factor, view, e.mousePosition);
                state.Message = "缩放 " + Mathf.RoundToInt(state.Zoom * 100f) + "%（Ctrl/Cmd + 滚轮）";
                e.Use();
            }
            return;
        }

        if (e.type == EventType.MouseDrag && e.button == 2)
        {
            state.Pan += e.delta;
            state.Message = "已平移地图；点击“复位视图”可回到左上角。";
            e.Use();
            return;
        }
        if (!view.Contains(e.mousePosition))
        {
            if (e.type == EventType.MouseMove && (state.HoverX >= 0 || state.HoverY >= 0))
            {
                state.HoverX = state.HoverY = -1;
                GUI.changed = true;
            }
            return;
        }

        int x, y;
        MouseCell(view, map, state, cell, e.mousePosition, out x, out y);
        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
        {
            if (state.HoverX != x || state.HoverY != y)
            {
                state.HoverX = x;
                state.HoverY = y;
                GUI.changed = true;
            }
        }
        if (e.type != EventType.MouseDown || e.button != 0) return;
        if (x < 0 || y < 0)
        {
            state.Message = "这里不在地图范围内。";
            e.Use();
            return;
        }

        int pointIndex = PointIndexAt(node.points, x, y);
        if (state.CurrentTool == Tool.Select)
        {
            state.SelectedPoint = pointIndex;
            state.Message = pointIndex >= 0 ? PointPreview(node.points[pointIndex], pointIndex) : "格 (" + x + "," + y + ") 没有点位；先选择“+ 调查点”或“+ 出口”。";
            e.Use();
            return;
        }

        string placement;
        if (!TryGetPlacementCell(map, x, y, out placement))
        {
            state.Message = "没有写入：" + placement;
            e.Use();
            return;
        }

        if (state.CurrentTool == Tool.AddClue)
            AddClue(venue, node, state, changed, x, y);
        else if (state.CurrentTool == Tool.AddExit)
            AddExit(venue, node, state, changed, x, y);
        else if (state.CurrentTool == Tool.AddChaser)
            AddChaser(node, state, changed, x, y);
        else if (state.CurrentTool == Tool.MoveSelected)
            MoveSelected(venue, node, state, changed, x, y);
        e.Use();
    }

    private static void DrawCanvasFooter(OverworldMap.Map map, WorldParkPack.Node node, State state)
    {
        string hover = "悬停地图可查看坐标";
        if (state.HoverX >= 0 && state.HoverY >= 0 && state.HoverX < map.W && state.HoverY < map.H)
        {
            char tile = map.At(state.HoverX, state.HoverY);
            OverworldCatalog.Tile info = OverworldCatalog.Get(tile);
            int point = PointIndexAt(node.points, state.HoverX, state.HoverY);
            hover = "格 (" + state.HoverX + "," + state.HoverY + ")  " + tile + " " + (info == null ? "未知格" : info.zh) +
                (OverworldMap.Walkable(map, state.HoverX, state.HoverY) ? " · 可通行" : " · 挡路") +
                (point >= 0 ? " · " + PointPreview(node.points[point], point) : "");
        }
        EditorGUILayout.LabelField(hover + "　｜　" + state.Message, EditorStyles.wordWrappedMiniLabel);
    }

    private static void DrawSelectedForm(WorldParkPack.Venue venue, WorldParkPack.Node node, State state, Action changed)
    {
        WorldParkPack.ExplorePoint point = Selected(node, state);
        if (point == null)
        {
            if (node.points.Count == 0) EditorGUILayout.HelpBox("还没有探索点。点击“+ 调查点”或“+ 出口”，再点击画布中的可通行格。", MessageType.Warning);
            else EditorGUILayout.HelpBox("点选画布中的标记后可编辑它；同格多个旧点可从下方列表选择。", MessageType.Info);
            DrawPointList(node, state);
            return;
        }

        GUILayout.Space(4);
        EditorGUILayout.LabelField("所选点位表单", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(PointPreview(point, state.SelectedPoint), EditorStyles.miniBoldLabel);

        string id = EditorGUILayout.TextField("点 ID", point.id ?? "");
        int kindIndex = EditorGUILayout.Popup("种类", point.kind == "chaser" ? 2 : string.Equals(point.kind, "exit", StringComparison.OrdinalIgnoreCase) ? 1 : 0, new[] { "调查点 clue", "出口 exit", "追逐者 chaser（鬼）" });
        string kind = kindIndex == 2 ? "chaser" : kindIndex == 1 ? "exit" : "clue";
        string label = EditorGUILayout.TextField("玩家标签", point.label ?? "");
        if (id != point.id || kind != point.kind || label != point.label)
        {
            List<WorldParkPack.ExplorePoint> before = ClonePoints(node.points);
            point.id = id;
            point.kind = kind;
            point.label = label;
            if (kind == "exit" && string.IsNullOrEmpty(point.outcome)) point.outcome = "win";
            if (kind == "chaser" && point.route == null) point.route = new List<string>();
            Record(node, state, before, null);
            Notify(state, changed, "已编辑所选点位表单。", true);
        }

        int wantedX = EditorGUILayout.IntField("格 X", point.x);
        int wantedY = EditorGUILayout.IntField("格 Y", point.y);
        if (wantedX != point.x || wantedY != point.y)
        {
            string placement;
            if (TryGetPlacementCell(node.mapText, wantedX, wantedY, out placement))
            {
                List<WorldParkPack.ExplorePoint> before = ClonePoints(node.points);
                point.x = wantedX;
                point.y = wantedY;
                Record(node, state, before, null);
                Notify(state, changed, "已把所选点移动到 (" + wantedX + "," + wantedY + ")。", true);
            }
            else
            {
                state.Message = "坐标没有写入：" + placement;
            }
        }

        if (string.Equals(point.kind, "clue", StringComparison.OrdinalIgnoreCase))
        {
            string clue = EditorGUILayout.TextField("对应 interact 线索 ID", point.clue ?? "");
            if (clue != point.clue)
            {
                List<WorldParkPack.ExplorePoint> before = ClonePoints(node.points);
                point.clue = clue;
                Record(node, state, before, null);
                Notify(state, changed, "已改调查点的线索引用；结构检查会提示不存在或阶段不符的 ID。", true);
            }
            EditorGUILayout.HelpBox("调查点应引用当前会场、当前 explore 阶段的 interact 线索。用画布“+ 调查点”会同时建立一条【未写草稿】线索。", MessageType.None);
        }
        else if (point.kind == "chaser")
        {
            DrawChaserForm(node, point, state, changed);
        }
        else
        {
            string outcome = EditorGUILayout.Popup("出口结果", point.outcome == "lose" ? 1 : 0, new[] { "win", "lose" }) == 0 ? "win" : "lose";
            if (outcome != point.outcome)
            {
                List<WorldParkPack.ExplorePoint> before = ClonePoints(node.points);
                point.outcome = outcome;
                Record(node, state, before, null);
                Notify(state, changed, "已改出口结果。", true);
            }
            DrawRequirements(node, point, state, changed);
        }
        EditorGUILayout.EndVertical();
        DrawPointList(node, state);
    }

    private static void DrawRequirements(WorldParkPack.Node node, WorldParkPack.ExplorePoint point, State state, Action changed)
    {
        if (point.requires == null) point.requires = new List<string>();
        EditorGUILayout.LabelField("需要已观察的线索 ID", EditorStyles.miniLabel);
        for (int i = 0; i < point.requires.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            string value = EditorGUILayout.TextField(point.requires[i] ?? "");
            if (value != point.requires[i])
            {
                List<WorldParkPack.ExplorePoint> before = ClonePoints(node.points);
                point.requires[i] = value;
                Record(node, state, before, null);
                Notify(state, changed, "已改出口所需线索；结构检查会提示空白、重复或不存在的 ID。", true);
            }
            if (GUILayout.Button("×", GUILayout.Width(25)))
            {
                List<WorldParkPack.ExplorePoint> before = ClonePoints(node.points);
                point.requires.RemoveAt(i);
                Record(node, state, before, null);
                Notify(state, changed, "已删除一个出口要求。", true);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("+ 需要线索", EditorStyles.miniButton, GUILayout.Width(88)))
        {
            List<WorldParkPack.ExplorePoint> before = ClonePoints(node.points);
            point.requires.Add("");
            Record(node, state, before, null);
            Notify(state, changed, "已增加一个待填写的出口要求。", true);
        }
    }

    private static void DrawPointList(WorldParkPack.Node node, State state)
    {
        if (node.points == null || node.points.Count == 0) return;
        EditorGUILayout.LabelField("点位预览（点一行可选择；同格多点按列表顺序）", EditorStyles.miniLabel);
        for (int i = 0; i < node.points.Count; i++)
        {
            WorldParkPack.ExplorePoint point = node.points[i];
            if (point == null) continue;
            bool selected = i == state.SelectedPoint;
            if (GUILayout.Toggle(selected, PointPreview(point, i), "Button"))
            {
                if (!selected)
                {
                    state.SelectedPoint = i;
                    state.CurrentTool = Tool.Select;
                    state.Message = PointPreview(point, i);
                }
            }
            else if (selected)
            {
                state.SelectedPoint = -1;
            }
        }
    }

    private static void AddClue(WorldParkPack.Venue venue, WorldParkPack.Node node, State state, Action changed, int x, int y)
    {
        List<WorldParkPack.ExplorePoint> before = ClonePoints(node.points);
        string clueId = UniqueId(venue.clues.Select(c => c == null ? "" : c.id), "clue");
        string pointId = UniqueId(node.points.Select(p => p == null ? "" : p.id), "point");
        WorldParkPack.Clue clue = new WorldParkPack.Clue
        {
            id = clueId,
            name = "【未写草稿】现场线索",
            trigger = "interact",
            foundAt = node.id ?? "",
            revealAt = SuggestedReveal(venue, node),
            hint = "【未写草稿】请填写玩家可见提示",
            truth = "【未写草稿】请填写作者真相；运行时只在已发现且揭示后显示"
        };
        venue.clues.Add(clue);
        node.points.Add(new WorldParkPack.ExplorePoint
        {
            id = pointId,
            kind = "clue",
            label = "【未写草稿】新调查点",
            clue = clueId,
            x = x,
            y = y,
            requires = new List<string>()
        });
        state.SelectedPoint = node.points.Count - 1;
        state.CurrentTool = Tool.Select;
        Record(node, state, before, CloneClue(clue));
        Notify(state, changed, "已在 (" + x + "," + y + ") 新建调查点，并创建“【未写草稿】”interact 线索；请在下方线索表单补写提示、真相和揭示节点。", true);
    }

    private static void AddExit(WorldParkPack.Venue venue, WorldParkPack.Node node, State state, Action changed, int x, int y)
    {
        List<WorldParkPack.ExplorePoint> before = ClonePoints(node.points);
        node.points.Add(new WorldParkPack.ExplorePoint
        {
            id = UniqueId(node.points.Select(p => p == null ? "" : p.id), "exit"),
            kind = "exit",
            label = "【未写草稿】新出口",
            outcome = "win",
            x = x,
            y = y,
            requires = new List<string>()
        });
        state.SelectedPoint = node.points.Count - 1;
        state.CurrentTool = Tool.Select;
        Record(node, state, before, null);
        Notify(state, changed, "已在 (" + x + "," + y + ") 新建出口草稿（默认 win）；请在表单确认结果和需要的线索。", true);
    }

    /// <summary>S252 追逐者表单：速度、视距、巡逻格。巡逻格按顺序循环走；没有巡逻格时原地守望、每 1.5 秒转向。</summary>
    private static void DrawChaserForm(WorldParkPack.Node node, WorldParkPack.ExplorePoint point, State state, Action changed)
    {
        if (point.route == null) point.route = new List<string>();
        int speed = EditorGUILayout.IntField("速度（十分之一格/秒）", point.speed);
        int sight = EditorGUILayout.IntField("视距（格）", point.sight);
        EditorGUILayout.LabelField("速度 0=默认 34（3.4 格/秒），允许 10–80，玩家是 40；视距 0=默认 7，允许 1–20；视野是面前 ±60° 扇形，墙会挡，1.5 格内背后也会察觉。", EditorStyles.wordWrappedMiniLabel);
        if (speed != point.speed || sight != point.sight)
        {
            List<WorldParkPack.ExplorePoint> before = ClonePoints(node.points);
            point.speed = speed;
            point.sight = sight;
            Record(node, state, before, null);
            Notify(state, changed, "已改追逐者速度 / 视距；结构检查会提示越界值。", true);
        }
        EditorGUILayout.LabelField("巡逻格（\"x,y\"，按顺序循环；空=原地守望）", EditorStyles.miniLabel);
        for (int i = 0; i < point.route.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            string value = EditorGUILayout.TextField((i + 1) + ".", point.route[i] ?? "");
            if (value != point.route[i])
            {
                List<WorldParkPack.ExplorePoint> before = ClonePoints(node.points);
                point.route[i] = value;
                Record(node, state, before, null);
                Notify(state, changed, "已改巡逻格；格式必须是 x,y 且可通行、从 T 可达。", true);
            }
            if (GUILayout.Button("×", GUILayout.Width(25)))
            {
                List<WorldParkPack.ExplorePoint> before = ClonePoints(node.points);
                point.route.RemoveAt(i);
                Record(node, state, before, null);
                Notify(state, changed, "已删除一个巡逻格。", true);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("+ 巡逻格（先填出生格，再改坐标）", EditorStyles.miniButton))
        {
            List<WorldParkPack.ExplorePoint> before = ClonePoints(node.points);
            point.route.Add(point.x + "," + point.y);
            Record(node, state, before, null);
            Notify(state, changed, "已增加一个巡逻格；请改成要巡逻到的坐标（画布上红色小方块按序号显示）。", true);
        }
        EditorGUILayout.HelpBox("追逐者不是可交互点：玩家看不见它时它也在走。被它碰到 = 本次探索失败（走 onLose）。设计建议：最短路线要穿过它的巡逻线，同时留一条有高草或墙角掩护的绕路。", MessageType.None);
    }

    private static void AddChaser(WorldParkPack.Node node, State state, Action changed, int x, int y)
    {
        List<WorldParkPack.ExplorePoint> before = ClonePoints(node.points);
        node.points.Add(new WorldParkPack.ExplorePoint
        {
            id = UniqueId(node.points.Select(p => p == null ? "" : p.id), "oni"),
            kind = "chaser",
            label = "鬼",
            x = x,
            y = y,
            requires = new List<string>(),
            route = new List<string>()
        });
        state.SelectedPoint = node.points.Count - 1;
        state.CurrentTool = Tool.Select;
        Record(node, state, before, null);
        Notify(state, changed, "已在 (" + x + "," + y + ") 放置追逐者“鬼”；请在表单里加巡逻格、调速度和视距。", true);
    }

    private static void MoveSelected(WorldParkPack.Venue venue, WorldParkPack.Node node, State state, Action changed, int x, int y)
    {
        WorldParkPack.ExplorePoint point = Selected(node, state);
        if (point == null)
        {
            state.CurrentTool = Tool.Select;
            state.Message = "没有可移动的所选点位。";
            return;
        }
        if (point.x == x && point.y == y)
        {
            state.CurrentTool = Tool.Select;
            state.Message = "所选点已经在 (" + x + "," + y + ")。";
            return;
        }
        List<WorldParkPack.ExplorePoint> before = ClonePoints(node.points);
        point.x = x;
        point.y = y;
        state.CurrentTool = Tool.Select;
        Record(node, state, before, null);
        Notify(state, changed, "已移动所选点到 (" + x + "," + y + ")。", true);
    }

    private static void DeleteSelected(WorldParkPack.Venue venue, WorldParkPack.Node node, State state, Action changed)
    {
        if (state.SelectedPoint < 0 || state.SelectedPoint >= node.points.Count) return;
        List<WorldParkPack.ExplorePoint> before = ClonePoints(node.points);
        string label = node.points[state.SelectedPoint] == null ? "点位" : (node.points[state.SelectedPoint].label ?? node.points[state.SelectedPoint].id ?? "点位");
        node.points.RemoveAt(state.SelectedPoint);
        state.SelectedPoint = Mathf.Clamp(state.SelectedPoint, 0, node.points.Count - 1);
        state.CurrentTool = Tool.Select;
        // 删除调查点绝不删除线索，以免安静地抹掉作者已经写好的内容。
        Record(node, state, before, null);
        Notify(state, changed, "已删除“" + label + "”点位；关联线索保留，需时可在会场线索表单中处理。", true);
    }

    private static void Undo(WorldParkPack.Venue venue, WorldParkPack.Node node, State state, Action changed)
    {
        if (state.Undo.Count == 0) return;
        int last = state.Undo.Count - 1;
        Change change = state.Undo[last];
        state.Undo.RemoveAt(last);
        RestorePoints(node, change.before);
        string note = "已撤销本节点最近一次点位操作。";
        if (change.createdClue != null)
        {
            // 只删除仍是原样的草稿线索；作者已改写提示/真相/名称时保留，绝不因点位撤销丢失文字。
            int removed = venue.clues.RemoveAll(c => c != null && SameClue(c, change.createdClue));
            if (removed == 0 && venue.clues.Any(c => c != null && string.Equals(c.id, change.createdClue.id, StringComparison.Ordinal)))
                note = "已撤销调查点；关联线索“" + change.createdClue.id + "”已被你改写，所以保留在线索表单中。";
        }
        state.Redo.Add(change);
        ClampSelection(node, state);
        Notify(state, changed, note, true);
    }

    private static bool SameClue(WorldParkPack.Clue a, WorldParkPack.Clue b)
    {
        return string.Equals(a.id, b.id, StringComparison.Ordinal) && string.Equals(a.name, b.name, StringComparison.Ordinal) &&
            string.Equals(a.hint, b.hint, StringComparison.Ordinal) && string.Equals(a.truth, b.truth, StringComparison.Ordinal) &&
            string.Equals(a.foundAt, b.foundAt, StringComparison.Ordinal) && string.Equals(a.revealAt, b.revealAt, StringComparison.Ordinal) &&
            string.Equals(a.trigger, b.trigger, StringComparison.Ordinal);
    }

    private static void Redo(WorldParkPack.Venue venue, WorldParkPack.Node node, State state, Action changed)
    {
        if (state.Redo.Count == 0) return;
        int last = state.Redo.Count - 1;
        Change change = state.Redo[last];
        state.Redo.RemoveAt(last);
        RestorePoints(node, change.after);
        if (change.createdClue != null && !venue.clues.Any(c => c != null && string.Equals(c.id, change.createdClue.id, StringComparison.Ordinal))) venue.clues.Add(CloneClue(change.createdClue));
        state.Undo.Add(change);
        ClampSelection(node, state);
        Notify(state, changed, "已重做本节点点位操作。", true);
    }

    private static void Record(WorldParkPack.Node node, State state, List<WorldParkPack.ExplorePoint> before, WorldParkPack.Clue createdClue)
    {
        state.Undo.Add(new Change { before = before, after = ClonePoints(node.points), createdClue = createdClue });
        if (state.Undo.Count > HistoryLimit) state.Undo.RemoveAt(0);
        state.Redo.Clear();
    }

    private static void RestorePoints(WorldParkPack.Node node, List<WorldParkPack.ExplorePoint> source)
    {
        node.points.Clear();
        node.points.AddRange(ClonePoints(source));
    }

    private static void Notify(State state, Action changed, string message, bool modified)
    {
        state.Message = message;
        if (modified)
        {
            GUI.changed = true;
            if (changed != null) changed();
        }
    }

    private static void ResetForDifferentMap(WorldParkPack.Venue venue, WorldParkPack.Node node, State state)
    {
        string mapText = node.mapText ?? "";
        string key = (venue.id ?? "") + "\n" + (node.id ?? "") + "\n" + MapSignature(mapText);
        if (string.Equals(state.Context, key, StringComparison.Ordinal)) return;
        state.SelectedPoint = -1;
        state.CurrentTool = Tool.Select;
        state.HoverX = state.HoverY = -1;
        state.Undo.Clear();
        state.Redo.Clear();
        state.Context = key;
        state.Message = "当前地图/阶段已切换；局部撤销历史已隔离。";
    }

    private static string MapSignature(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < text.Length; i++) { hash ^= text[i]; hash *= 16777619; }
            return text.Length + ":" + hash.ToString("x8");
        }
    }

    private static void EnsureLists(WorldParkPack.Venue venue, WorldParkPack.Node node)
    {
        if (venue.clues == null) venue.clues = new List<WorldParkPack.Clue>();
        if (node.points == null) node.points = new List<WorldParkPack.ExplorePoint>();
    }

    private static void ClampSelection(WorldParkPack.Node node, State state)
    {
        if (state.SelectedPoint < 0 || state.SelectedPoint >= node.points.Count) state.SelectedPoint = -1;
    }

    private static WorldParkPack.ExplorePoint Selected(WorldParkPack.Node node, State state)
    {
        return state.SelectedPoint >= 0 && state.SelectedPoint < node.points.Count ? node.points[state.SelectedPoint] : null;
    }

    private static float CellSize(State state) { return BaseCellSize * state.Zoom; }

    private static Rect CellRect(Rect view, OverworldMap.Map map, State state, float cell, int x, int y)
    {
        return new Rect(view.x + state.Pan.x + x * cell, view.y + state.Pan.y + (map.H - 1 - y) * cell, cell, cell);
    }

    private static void MouseCell(Rect view, OverworldMap.Map map, State state, float cell, Vector2 mouse, out int x, out int y)
    {
        x = Mathf.FloorToInt((mouse.x - view.x - state.Pan.x) / cell);
        int row = Mathf.FloorToInt((mouse.y - view.y - state.Pan.y) / cell);
        y = map.H - 1 - row;
        if (x < 0 || y < 0 || x >= map.W || y >= map.H) { x = y = -1; }
    }

    private static void ZoomAt(State state, float factor, Rect view, Vector2 anchor)
    {
        float oldCell = CellSize(state);
        Vector2 origin = new Vector2(view.x, view.y);
        Vector2 mapAtCursor = (anchor - origin - state.Pan) / oldCell;
        state.Zoom *= factor;
        state.Pan = anchor - origin - mapAtCursor * CellSize(state);
    }

    private static Color TileColor(char tile)
    {
        OverworldCatalog.Tile info = OverworldCatalog.Get(tile);
        if (info == null) return new Color(0.78f, 0.18f, 0.78f, 1f);
        Color source = new Color(info.r, info.g, info.b, 1f);
        return Color.Lerp(source, Color.black, info.solid ? 0.12f : 0.03f);
    }

    private static GUIStyle tileStyle, markerStyle;

    private static GUIStyle TileStyle()
    {
        if (tileStyle != null) return tileStyle;
        tileStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
        tileStyle.alignment = TextAnchor.MiddleCenter;
        tileStyle.normal.textColor = new Color(1f, 1f, 1f, 0.78f);
        return tileStyle;
    }

    private static GUIStyle MarkerStyle()
    {
        if (markerStyle != null) return markerStyle;
        markerStyle = new GUIStyle(EditorStyles.boldLabel);
        markerStyle.alignment = TextAnchor.MiddleCenter;
        markerStyle.normal.textColor = Color.white;
        return markerStyle;
    }

    private static void DrawOutline(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    private static string PointPreview(WorldParkPack.ExplorePoint point, int index)
    {
        if (point == null) return "点 " + (index + 1) + "（空）";
        string type = point.kind == "chaser" ? "追逐者" : string.Equals(point.kind, "exit", StringComparison.OrdinalIgnoreCase) ? "出口" : "调查";
        string extra = type == "追逐者" ? "速度 " + (point.speed > 0 ? point.speed / 10f : WorldParkPack.DefaultChaserSpeed / 10f) + " · 视距 " + (point.sight > 0 ? point.sight : WorldParkPack.DefaultChaserSight) + " · 巡逻 " + (point.route == null ? 0 : point.route.Count) + " 格" : type == "出口" ? (string.IsNullOrEmpty(point.outcome) ? "结果未写" : point.outcome) : (string.IsNullOrEmpty(point.clue) ? "线索未接" : point.clue);
        return "点 " + (index + 1) + " · " + type + " · (" + point.x + "," + point.y + ") · " + (point.label ?? "") + " · " + extra;
    }

    private static string SuggestedReveal(WorldParkPack.Venue venue, WorldParkPack.Node node)
    {
        string[] targets = { node.onWin, node.onLose, node.next };
        foreach (string target in targets)
        {
            WorldParkPack.Node candidate = WorldParkPack.GetNode(venue, target);
            if (candidate != null && string.Equals(candidate.kind, "reveal", StringComparison.OrdinalIgnoreCase)) return candidate.id;
        }
        WorldParkPack.Node firstReveal = venue.nodes.FirstOrDefault(n => n != null && string.Equals(n.kind, "reveal", StringComparison.OrdinalIgnoreCase));
        return firstReveal == null ? "" : firstReveal.id;
    }

    private static string UniqueId(IEnumerable<string> ids, string prefix)
    {
        var used = new HashSet<string>(ids ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
        string id = prefix;
        int suffix = 2;
        while (used.Contains(id)) id = prefix + "_" + suffix++;
        return id;
    }

    private static List<WorldParkPack.ExplorePoint> ClonePoints(IEnumerable<WorldParkPack.ExplorePoint> source)
    {
        var result = new List<WorldParkPack.ExplorePoint>();
        if (source == null) return result;
        foreach (WorldParkPack.ExplorePoint point in source)
        {
            if (point == null) { result.Add(null); continue; }
            result.Add(new WorldParkPack.ExplorePoint
            {
                id = point.id,
                kind = point.kind,
                label = point.label,
                clue = point.clue,
                outcome = point.outcome,
                x = point.x,
                y = point.y,
                requires = point.requires == null ? new List<string>() : new List<string>(point.requires),
                speed = point.speed,
                sight = point.sight,
                route = point.route == null ? new List<string>() : new List<string>(point.route)
            });
        }
        return result;
    }

    private static WorldParkPack.Clue CloneClue(WorldParkPack.Clue clue)
    {
        if (clue == null) return null;
        return new WorldParkPack.Clue
        {
            id = clue.id,
            name = clue.name,
            foundAt = clue.foundAt,
            revealAt = clue.revealAt,
            hint = clue.hint,
            truth = clue.truth,
            trigger = clue.trigger
        };
    }
}
