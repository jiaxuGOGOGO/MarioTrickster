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
    private string parsedSource;
    private string parseError;
    private LevelWorkshopModel.CheckResult check;
    private string checkedSource;
    private Vector2 paletteScroll, canvasScroll, issueScroll;
    private bool painting, rectDragging, strokeStarted;
    private Vector2Int lastCell, rectStart, hoverCell = new Vector2Int(-1, -1);
    private int undoGroup;
    private GUIStyle cellLabel, tileLabel;
    // S192 性能：
    //  - 画的时候只跑"快速检查"（摆放规则，~1ms），停笔 0.35 秒后再在后台节拍里跑完整检查（死局/结构/全部随机组合）；
    //  - 画布只在鼠标换格子时重画，不是每个像素移动都重画；
    //  - GUIStyle 全部缓存（原来每个格子每帧 new 一个）；网格行只在内容变化时拆分一次。
    private const double FullCheckDelay = 0.35;
    private double fullCheckAt = -1;
    private bool fullCheckStale;
    private string[] rowsCache = new string[0];
    private string rowsSource;
    private GUIStyle glyphDark, glyphLight, nameNormal, nameSelected;
    private IList<string> shownCache;
    private string shownKey;

    [MenuItem("MarioTrickster/Level Workshop (关卡工坊) %&w", false, 1)]
    public static void Open()
    {
        var w = GetWindow<LevelWorkshopWindow>("关卡工坊");
        w.minSize = new Vector2(900, 560);
        w.Show();
    }

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(source)) source = EditorPrefs.GetString(DraftKey, "");
        if (string.IsNullOrEmpty(source)) source = string.Join("\n", Step1PrankRoomBuilder.Current);
        Undo.undoRedoPerformed += OnUndo;
        EditorApplication.update += Tick;
        wantsMouseMove = true;
    }

    private void OnDisable() { Undo.undoRedoPerformed -= OnUndo; EditorApplication.update -= Tick; }

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
        EditorGUILayout.EndHorizontal();
    }

    // ── 顶部工具条 ────────────────────────────────────
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("新建", EditorStyles.toolbarButton, GUILayout.Width(44)) &&
            EditorUtility.DisplayDialog("新建房间", "替换当前画布？（Ctrl+Z 可撤销）", "新建", "取消"))
            SetSource(LevelWorkshopModel.NewRoom(48, 12), "New room");
        if (GUILayout.Button("载入恶作剧房间", EditorStyles.toolbarButton, GUILayout.Width(96)))
            SetSource(string.Join("\n", Step1PrankRoomBuilder.Room), "Load prank room");
        if (GUILayout.Button(new GUIContent("样板：两层监狱", "纵向逃脱示例：地下拿宝、爬回地面；裂缝地板 x + 弹簧板 J"), EditorStyles.toolbarButton, GUILayout.Width(96)))
            SetSource(string.Join("\n", LevelWorkshopModel.PrisonSample), "Load prison sample");
        if (GUILayout.Button("导入", EditorStyles.toolbarButton, GUILayout.Width(44))) Import();
        if (GUILayout.Button("导出", EditorStyles.toolbarButton, GUILayout.Width(44))) Export();
        GUILayout.Space(10);
        tool = (LevelWorkshopModel.Tool)GUILayout.Toolbar((int)tool, new[] { "✎ 画笔", "▭ 矩形", "⌫ 橡皮", "⊙ 吸管" }, EditorStyles.toolbarButton, GUILayout.Width(260));
        GUILayout.Space(10);
        step1Mode = GUILayout.Toggle(step1Mode, new GUIContent("第 1 步规则", "只显示/允许第 1 步恶作剧房间能用的元素，并按第 1 步规则检查"), EditorStyles.toolbarButton, GUILayout.Width(80));
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
                { brush = t.ch; if (tool == LevelWorkshopModel.Tool.Erase || tool == LevelWorkshopModel.Tool.Pick) tool = LevelWorkshopModel.Tool.Brush; }
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
        var reg = AsciiElementRegistry.GetDefault();
        canvasScroll = EditorGUILayout.BeginScrollView(canvasScroll, GUILayout.ExpandHeight(true));
        Rect canvas = GUILayoutUtility.GetRect(doc.Width * size, doc.Height * size, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
        Event e = Event.current;
        int control = GUIUtility.GetControlID("LevelWorkshopGrid".GetHashCode(), FocusType.Passive);

        // 最坏情况预览的网格
        string sk = doc.Grid + worstCase;
        if (shownKey != sk)
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

        if (inside && e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
        {
            if (e.alt || tool == LevelWorkshopModel.Tool.Pick) { brush = doc.Cell(point.x, point.y); if (brush == '.') brush = '#'; tool = LevelWorkshopModel.Tool.Brush; e.Use(); }
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
        EditorGUILayout.LabelField($"{doc.Width} × {doc.Height} 格   画笔：{ElementCatalog.Get(brush)?.zh ?? brush.ToString()}", GUILayout.Width(220));
        if (GUILayout.Button("右 +8", EditorStyles.miniButton, GUILayout.Width(44))) SetSource(doc.Resize(Math.Min(LevelStudioDocument.MaxWidth, doc.Width + 8), doc.Height).Text, "Expand");
        if (GUILayout.Button("右 -8", EditorStyles.miniButton, GUILayout.Width(44)) && doc.Width > 24) SetSource(doc.Resize(doc.Width - 8, doc.Height).Text, "Shrink");
        if (GUILayout.Button("上 +2", EditorStyles.miniButton, GUILayout.Width(44))) SetSource(doc.Resize(doc.Width, Math.Min(LevelStudioDocument.MaxHeight, doc.Height + 2)).Text, "Expand");
        EditorGUILayout.LabelField(hover, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndHorizontal();

        if (check != null)
        {
            string head = fullCheckStale ? "…检查中（停笔后自动完成）" : check.Headline;
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

    private void Import()
    {
        string path = EditorUtility.OpenFilePanel("导入关卡 .txt", Application.dataPath, "txt");
        if (string.IsNullOrEmpty(path)) return;
        string text = File.ReadAllText(path);
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
