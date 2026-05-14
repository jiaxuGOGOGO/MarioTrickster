using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Level Brush Tool — 基于 SceneView.duringSceneGui 的拖拽笔刷系统。
///
/// 核心功能：
///   - 在 Scene 视图中直接用鼠标"画"关卡元素，像画笔一样刷地形/陷阱/平台。
///   - 支持单击放置和拖拽连续绘制（按住鼠标左键拖动）。
///   - 自动对齐 1x1 网格，防止重叠放置。
///   - 右键或 Escape 退出笔刷模式。
///   - 橡皮擦模式：按住 Shift 切换为擦除。
///
/// 设计原则：
///   - 与 LevelEditorPickingManager 互不干扰：笔刷激活时接管 SceneView 输入。
///   - 复用 AsciiLevelGenerator 的元素生成逻辑，保持与 Element Palette 一致。
///   - 所有操作支持 Undo。
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///
/// 使用方式：
///   - 在 Level Studio 的 Element Palette 中选择元素后点击"Brush Mode"激活。
///   - 或通过快捷键 B 在 Scene 视图中切换笔刷开关。
/// </summary>
[InitializeOnLoad]
public static class LevelBrushTool
{
    // ─────────────────────────────────────────────────────
    #region 笔刷状态

    /// <summary>笔刷是否激活</summary>
    public static bool IsActive { get; private set; }

    /// <summary>当前笔刷选中的元素名称</summary>
    public static string CurrentBrushName { get; private set; } = "";

    /// <summary>当前笔刷对应的 ASCII 字符</summary>
    public static char CurrentBrushChar { get; private set; } = '\0';

    /// <summary>当前笔刷颜色（用于 Scene 视图预览）</summary>
    public static Color CurrentBrushColor { get; private set; } = Color.white;

    /// <summary>笔刷尺寸（1=单格，2=2x2，3=3x3）</summary>
    public static int BrushSize { get; set; } = 1;

    /// <summary>是否处于橡皮擦模式</summary>
    public static bool IsErasing { get; private set; }

    // 内部状态
    private static Vector2Int lastPaintedCell = new Vector2Int(int.MinValue, int.MinValue);
    private static bool isPainting = false;
    private static HashSet<Vector2Int> paintedCellsThisStroke = new HashSet<Vector2Int>();

    #endregion

    // ─────────────────────────────────────────────────────
    #region 初始化

    static LevelBrushTool()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 公共 API

    /// <summary>激活笔刷模式</summary>
    public static void Activate(string elementName, char asciiChar, Color color)
    {
        IsActive = true;
        CurrentBrushName = elementName;
        CurrentBrushChar = asciiChar;
        CurrentBrushColor = color;
        IsErasing = false;
        lastPaintedCell = new Vector2Int(int.MinValue, int.MinValue);
        paintedCellsThisStroke.Clear();

        // 强制 SceneView 重绘以显示笔刷光标
        SceneView.RepaintAll();
        Debug.Log($"[Level Brush] 激活笔刷: {elementName} ('{asciiChar}') | 左键绘制 | Shift+左键擦除 | 右键/Esc 退出 | 滚轮调大小");
    }

    /// <summary>停用笔刷模式</summary>
    public static void Deactivate()
    {
        if (IsActive)
        {
            IsActive = false;
            CurrentBrushName = "";
            CurrentBrushChar = '\0';
            isPainting = false;
            paintedCellsThisStroke.Clear();
            SceneView.RepaintAll();
            Debug.Log("[Level Brush] 笔刷已停用");
        }
    }

    /// <summary>切换橡皮擦模式</summary>
    public static void ToggleEraser()
    {
        IsErasing = !IsErasing;
        SceneView.RepaintAll();
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region SceneView 回调

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!IsActive) return;
        if (EditorApplication.isPlaying) return;

        Event e = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        // 获取鼠标在世界坐标的位置（2D，z=0）
        Vector2 mouseWorldPos = GetMouseWorldPosition(sceneView, e);
        Vector2Int cellPos = WorldToCell(mouseWorldPos);

        // 检测 Shift 键切换擦除模式
        IsErasing = e.shift;

        // ── 绘制笔刷光标预览 ──
        DrawBrushCursor(cellPos, sceneView);

        // ── 绘制 HUD 信息 ──
        DrawBrushHUD(sceneView);

        // ── 输入处理 ──
        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0) // 左键：开始绘制
                {
                    isPainting = true;
                    paintedCellsThisStroke.Clear();
                    Undo.SetCurrentGroupName(IsErasing ? "Brush Erase" : $"Brush Paint: {CurrentBrushName}");
                    PaintOrEraseAtCell(cellPos);
                    GUIUtility.hotControl = controlId;
                    e.Use();
                }
                else if (e.button == 1) // 右键：退出笔刷
                {
                    Deactivate();
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (isPainting && e.button == 0)
                {
                    PaintOrEraseAtCell(cellPos);
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (e.button == 0 && isPainting)
                {
                    isPainting = false;
                    lastPaintedCell = new Vector2Int(int.MinValue, int.MinValue);
                    Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
                    GUIUtility.hotControl = 0;
                    e.Use();
                }
                break;

            case EventType.KeyDown:
                if (e.keyCode == KeyCode.Escape)
                {
                    Deactivate();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.LeftBracket) // [ 缩小笔刷
                {
                    BrushSize = Mathf.Max(1, BrushSize - 1);
                    e.Use();
                }
                else if (e.keyCode == KeyCode.RightBracket) // ] 放大笔刷
                {
                    BrushSize = Mathf.Min(5, BrushSize + 1);
                    e.Use();
                }
                break;

            case EventType.ScrollWheel:
                // 滚轮调整笔刷大小
                if (e.control)
                {
                    BrushSize = Mathf.Clamp(BrushSize + (e.delta.y > 0 ? -1 : 1), 1, 5);
                    sceneView.Repaint();
                    e.Use();
                }
                break;

            case EventType.Layout:
                // 确保我们能接收到事件
                HandleUtility.AddDefaultControl(controlId);
                break;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 绘制/擦除逻辑

    private static void PaintOrEraseAtCell(Vector2Int centerCell)
    {
        // 根据笔刷大小计算所有需要操作的格子
        List<Vector2Int> cells = GetBrushCells(centerCell);

        foreach (var cell in cells)
        {
            // 跳过本次笔画中已经处理过的格子（避免拖拽时重复生成）
            if (paintedCellsThisStroke.Contains(cell))
                continue;

            if (IsErasing)
            {
                EraseAtCell(cell);
            }
            else
            {
                PaintAtCell(cell);
            }

            paintedCellsThisStroke.Add(cell);
        }

        lastPaintedCell = centerCell;
    }

    private static void PaintAtCell(Vector2Int cell)
    {
        // 检查该位置是否已有物体（避免重叠）
        if (HasElementAtCell(cell))
            return;

        // 确保有 Root 节点
        GameObject root = GameObject.Find("AsciiLevel_Root");
        if (root == null)
        {
            root = new GameObject("AsciiLevel_Root");
            Undo.RegisterCreatedObjectUndo(root, "Create ASCII Root");
        }

        // 使用 AsciiLevelGenerator 生成元素
        string miniTemplate = CurrentBrushChar.ToString();
        GameObject tempRoot = AsciiLevelGenerator.GenerateFromTemplate(miniTemplate, false);

        if (tempRoot != null && tempRoot.transform.childCount > 0)
        {
            List<Transform> children = new List<Transform>();
            foreach (Transform child in tempRoot.transform)
            {
                children.Add(child);
            }

            foreach (Transform child in children)
            {
                child.position = new Vector3(cell.x, cell.y, 0);
                child.name = child.name.Replace("_0_0", $"_{cell.x}_{cell.y}");
                child.parent = root.transform;
                Undo.RegisterCreatedObjectUndo(child.gameObject, $"Brush Paint {CurrentBrushName}");
            }

            Object.DestroyImmediate(tempRoot);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        else if (tempRoot != null)
        {
            Object.DestroyImmediate(tempRoot);
        }
    }

    private static void EraseAtCell(Vector2Int cell)
    {
        // 查找该格子位置的物体并删除
        GameObject root = GameObject.Find("AsciiLevel_Root");
        if (root == null) return;

        List<Transform> toDelete = new List<Transform>();
        foreach (Transform child in root.transform)
        {
            Vector2Int childCell = WorldToCell(child.position);
            if (childCell == cell)
            {
                toDelete.Add(child);
            }
        }

        foreach (var t in toDelete)
        {
            Undo.DestroyObjectImmediate(t.gameObject);
        }

        if (toDelete.Count > 0)
        {
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }

    private static bool HasElementAtCell(Vector2Int cell)
    {
        GameObject root = GameObject.Find("AsciiLevel_Root");
        if (root == null) return false;

        foreach (Transform child in root.transform)
        {
            Vector2Int childCell = WorldToCell(child.position);
            if (childCell == cell)
                return true;
        }
        return false;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 视觉反馈

    private static void DrawBrushCursor(Vector2Int cellPos, SceneView sceneView)
    {
        List<Vector2Int> cells = GetBrushCells(cellPos);
        Color cursorColor = IsErasing
            ? new Color(1f, 0.3f, 0.3f, 0.4f)
            : new Color(CurrentBrushColor.r, CurrentBrushColor.g, CurrentBrushColor.b, 0.4f);

        Color outlineColor = IsErasing
            ? new Color(1f, 0.2f, 0.2f, 0.9f)
            : new Color(CurrentBrushColor.r, CurrentBrushColor.g, CurrentBrushColor.b, 0.9f);

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        foreach (var cell in cells)
        {
            Vector3 center = new Vector3(cell.x, cell.y, 0);

            // 填充方块
            Handles.color = cursorColor;
            Vector3[] verts = new Vector3[4]
            {
                center + new Vector3(-0.5f, -0.5f, 0),
                center + new Vector3(-0.5f, 0.5f, 0),
                center + new Vector3(0.5f, 0.5f, 0),
                center + new Vector3(0.5f, -0.5f, 0)
            };
            Handles.DrawSolidRectangleAndOutline(verts, cursorColor, outlineColor);

            // 如果该位置已有物体，显示红色 X
            if (!IsErasing && HasElementAtCell(cell))
            {
                Handles.color = new Color(1f, 0f, 0f, 0.7f);
                Handles.DrawLine(center + new Vector3(-0.3f, -0.3f, 0), center + new Vector3(0.3f, 0.3f, 0));
                Handles.DrawLine(center + new Vector3(-0.3f, 0.3f, 0), center + new Vector3(0.3f, -0.3f, 0));
            }
        }

        // 中心十字准星
        Handles.color = outlineColor;
        Vector3 cursorCenter = new Vector3(cellPos.x, cellPos.y, 0);
        float crossSize = 0.15f;
        Handles.DrawLine(cursorCenter + Vector3.left * crossSize, cursorCenter + Vector3.right * crossSize);
        Handles.DrawLine(cursorCenter + Vector3.down * crossSize, cursorCenter + Vector3.up * crossSize);
    }

    private static void DrawBrushHUD(SceneView sceneView)
    {
        Handles.BeginGUI();

        // 左上角 HUD
        GUIStyle hudStyle = new GUIStyle(EditorStyles.helpBox);
        hudStyle.fontSize = 12;
        hudStyle.fontStyle = FontStyle.Bold;
        hudStyle.normal.textColor = Color.white;
        hudStyle.padding = new RectOffset(8, 8, 4, 4);

        string modeText = IsErasing ? "ERASER" : "BRUSH";
        Color modeColor = IsErasing ? new Color(1f, 0.4f, 0.4f) : CurrentBrushColor;

        Rect hudRect = new Rect(10, 10, 280, 60);

        // 背景
        EditorGUI.DrawRect(hudRect, new Color(0.1f, 0.1f, 0.1f, 0.85f));

        // 模式标签
        GUI.color = modeColor;
        GUI.Label(new Rect(14, 12, 270, 20), $"● {modeText}: {(IsErasing ? "Delete" : CurrentBrushName)}", hudStyle);

        // 操作提示
        GUI.color = new Color(0.8f, 0.8f, 0.8f);
        GUIStyle hintStyle = new GUIStyle(EditorStyles.miniLabel);
        hintStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        GUI.Label(new Rect(14, 34, 270, 16), $"Size: {BrushSize}x{BrushSize} | [/] resize | Shift=erase | RClick/Esc=exit", hintStyle);
        GUI.Label(new Rect(14, 50, 270, 16), "Ctrl+Scroll=resize | Left-drag=paint", hintStyle);

        GUI.color = Color.white;
        Handles.EndGUI();
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 工具方法

    private static Vector2 GetMouseWorldPosition(SceneView sceneView, Event e)
    {
        // 将 GUI 坐标转换为世界坐标
        Vector2 mousePos = e.mousePosition;
        // HandleUtility.GUIPointToWorldRay 在 SceneView 回调中可用
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        // 对于 2D，取 z=0 平面的交点
        float t = -ray.origin.z / ray.direction.z;
        Vector3 worldPos = ray.origin + ray.direction * t;
        return new Vector2(worldPos.x, worldPos.y);
    }

    private static Vector2Int WorldToCell(Vector2 worldPos)
    {
        return new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y));
    }

    private static Vector2Int WorldToCell(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y));
    }

    private static List<Vector2Int> GetBrushCells(Vector2Int center)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        int halfSize = BrushSize / 2;

        for (int x = -halfSize; x <= halfSize; x++)
        {
            for (int y = -halfSize; y <= halfSize; y++)
            {
                // 对于偶数尺寸，偏移使其以中心为准
                if (BrushSize % 2 == 0)
                {
                    cells.Add(new Vector2Int(center.x + x + (x >= 0 ? 0 : 1), center.y + y + (y >= 0 ? 0 : 1)));
                }
                else
                {
                    cells.Add(new Vector2Int(center.x + x, center.y + y));
                }
            }
        }

        return cells;
    }

    #endregion
}
