using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// NodeConnectionVisualizer — GBG 风格的元素连接关系可视化
///
/// 核心理念（来自 Game Builder Garage 的连线系统）：
///   GBG 中所有 Nodon 之间的信号流通过可见的连线表达，
///   用户一眼就能看出"谁影响谁"、"信号从哪来到哪去"。
///
/// 在 MarioTrickster 中的映射：
///   在 Scene 视图中用彩色连线显示元素之间的【触发/影响关系】：
///   - 移动平台 → 显示运动轨迹线
///   - 传送带 → 显示推力方向箭头
///   - PossessionAnchor → 显示 Trickster 可附身的连接网络
///   - 检查点 → 显示重生区域
///   - 崩塌平台 → 显示掉落路径
///
/// 开关：通过 Level Studio 工具栏或快捷键 F6 切换显示
/// </summary>
[InitializeOnLoad]
public static class NodeConnectionVisualizer
{
    // ═══════════════════════════════════════════════════
    // 状态
    // ═══════════════════════════════════════════════════
    private const string PREF_ENABLED = "MarioTrickster_NodeViz_Enabled";
    private const string PREF_SHOW_MOVEMENT = "MarioTrickster_NodeViz_Movement";
    private const string PREF_SHOW_ANCHORS = "MarioTrickster_NodeViz_Anchors";
    private const string PREF_SHOW_HAZARDS = "MarioTrickster_NodeViz_Hazards";

    public static bool IsEnabled
    {
        get => EditorPrefs.GetBool(PREF_ENABLED, false);
        set => EditorPrefs.SetBool(PREF_ENABLED, value);
    }

    public static bool ShowMovement
    {
        get => EditorPrefs.GetBool(PREF_SHOW_MOVEMENT, true);
        set => EditorPrefs.SetBool(PREF_SHOW_MOVEMENT, value);
    }

    public static bool ShowAnchors
    {
        get => EditorPrefs.GetBool(PREF_SHOW_ANCHORS, true);
        set => EditorPrefs.SetBool(PREF_SHOW_ANCHORS, value);
    }

    public static bool ShowHazards
    {
        get => EditorPrefs.GetBool(PREF_SHOW_HAZARDS, true);
        set => EditorPrefs.SetBool(PREF_SHOW_HAZARDS, value);
    }

    // ═══════════════════════════════════════════════════
    // 颜色定义（GBG 四色系统映射）
    // ═══════════════════════════════════════════════════
    private static readonly Color COLOR_MOVEMENT = new Color(0.3f, 0.6f, 1f, 0.8f);      // 蓝色 - 运动轨迹
    private static readonly Color COLOR_ANCHOR = new Color(0.9f, 0.4f, 0.9f, 0.8f);       // 紫色 - 附身网络
    private static readonly Color COLOR_HAZARD = new Color(1f, 0.3f, 0.2f, 0.6f);         // 红色 - 危险区域
    private static readonly Color COLOR_CHECKPOINT = new Color(0.2f, 0.9f, 0.7f, 0.7f);   // 青色 - 检查点
    private static readonly Color COLOR_CONVEYOR = new Color(0.9f, 0.8f, 0.2f, 0.7f);     // 黄色 - 传送带方向

    // ═══════════════════════════════════════════════════
    // 初始化
    // ═══════════════════════════════════════════════════
    static NodeConnectionVisualizer()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    // ═══════════════════════════════════════════════════
    // 菜单
    // ═══════════════════════════════════════════════════

    [MenuItem("MarioTrickster/Toggle Node Connections _F6", false, 101)]
    public static void ToggleVisualization()
    {
        IsEnabled = !IsEnabled;
        SceneView.RepaintAll();
        Debug.Log($"[NodeViz] 连接可视化: {(IsEnabled ? "开启" : "关闭")}");
    }

    // ═══════════════════════════════════════════════════
    // Scene GUI 绘制
    // ═══════════════════════════════════════════════════

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!IsEnabled) return;
        if (EditorApplication.isPlaying) return; // Play 模式下由 GameplayBoxVisualizer 负责

        // 绘制控制面板（左上角小面板）
        DrawControlPanel(sceneView);

        // 绘制各类连接
        if (ShowMovement)
            DrawMovementConnections();

        if (ShowAnchors)
            DrawAnchorNetwork();

        if (ShowHazards)
            DrawHazardZones();

        DrawCheckpoints();
        DrawConveyorDirections();
    }

    // ═══════════════════════════════════════════════════
    // 控制面板（Scene 视图内嵌）
    // ═══════════════════════════════════════════════════

    private static void DrawControlPanel(SceneView sceneView)
    {
        Handles.BeginGUI();

        float panelWidth = 180f;
        float panelHeight = 100f;
        Rect panelRect = new Rect(10, 40, panelWidth, panelHeight);

        // 半透明背景
        GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        GUI.DrawTexture(panelRect, EditorGUIUtility.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(panelRect);
        GUILayout.Space(4);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } };
        GUIStyle toggleStyle = new GUIStyle(EditorStyles.toggle) { normal = { textColor = Color.white } };

        GUILayout.Label("🔗 Node Connections", titleStyle);

        GUI.color = COLOR_MOVEMENT;
        ShowMovement = GUILayout.Toggle(ShowMovement, " 运动轨迹", toggleStyle);
        GUI.color = COLOR_ANCHOR;
        ShowAnchors = GUILayout.Toggle(ShowAnchors, " 附身网络", toggleStyle);
        GUI.color = COLOR_HAZARD;
        ShowHazards = GUILayout.Toggle(ShowHazards, " 危险区域", toggleStyle);
        GUI.color = Color.white;

        GUILayout.EndArea();
        Handles.EndGUI();
    }

    // ═══════════════════════════════════════════════════
    // 运动轨迹连线
    // ═══════════════════════════════════════════════════

    private static void DrawMovementConnections()
    {
        // 移动平台：显示 A→B 轨迹
        MovingPlatform[] movingPlatforms = Object.FindObjectsOfType<MovingPlatform>(true);
        foreach (var mp in movingPlatforms)
        {
            Vector3 posA = mp.transform.position;
            // 通过反射获取 pointB（私有字段）
            var field = mp.GetType().GetField("pointB",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                Vector3 pointB = (Vector3)field.GetValue(mp);
                Vector3 posB = posA + pointB;

                // 绘制轨迹线
                Handles.color = COLOR_MOVEMENT;
                Handles.DrawDottedLine(posA, posB, 4f);

                // 绘制端点标记
                Handles.DrawSolidDisc(posA, Vector3.forward, 0.15f);
                Handles.DrawSolidDisc(posB, Vector3.forward, 0.15f);

                // 绘制方向箭头
                Vector3 dir = (posB - posA).normalized;
                Vector3 mid = (posA + posB) * 0.5f;
                DrawArrow(mid, dir, 0.3f, COLOR_MOVEMENT);

                // 标签
                Handles.Label(mid + Vector3.up * 0.3f, $"▸ {Vector3.Distance(posA, posB):F1}格",
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = COLOR_MOVEMENT } });
            }
        }

        // ControllablePlatform 同理
        ControllablePlatform[] controllables = Object.FindObjectsOfType<ControllablePlatform>(true);
        foreach (var cp in controllables)
        {
            Vector3 posA = cp.transform.position;
            var field = cp.GetType().GetField("pointB",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                Vector3 pointB = (Vector3)field.GetValue(cp);
                Vector3 posB = posA + pointB;

                Handles.color = new Color(0.9f, 0.5f, 1f, 0.8f); // 紫蓝色区分可控平台
                Handles.DrawDottedLine(posA, posB, 3f);
                Handles.DrawSolidDisc(posA, Vector3.forward, 0.12f);
                Handles.DrawSolidDisc(posB, Vector3.forward, 0.12f);

                // 可控标记
                Handles.Label(posA + Vector3.up * 0.5f, "⚡可控",
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.9f, 0.5f, 1f) } });
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // 附身网络
    // ═══════════════════════════════════════════════════

    private static void DrawAnchorNetwork()
    {
        PossessionAnchor[] anchors = Object.FindObjectsOfType<PossessionAnchor>(true);
        if (anchors.Length < 2) return;

        // 绘制附身点之间的连接网络（相邻点连线）
        List<Vector3> positions = new List<Vector3>();
        foreach (var anchor in anchors)
        {
            if (anchor.PossessionEnabled)
                positions.Add(anchor.transform.position);
        }

        // 按 X 坐标排序，连接相邻点
        positions.Sort((a, b) => a.x.CompareTo(b.x));

        for (int i = 0; i < positions.Count - 1; i++)
        {
            float dist = Vector3.Distance(positions[i], positions[i + 1]);
            if (dist < 15f) // 只连接合理距离内的点
            {
                Handles.color = COLOR_ANCHOR;
                // 用贝塞尔曲线连接（更美观）
                Vector3 start = positions[i];
                Vector3 end = positions[i + 1];
                Vector3 mid = (start + end) * 0.5f + Vector3.up * 1f;
                Handles.DrawBezier(start, end, mid, mid, COLOR_ANCHOR, null, 2f);
            }
        }

        // 绘制每个附身点的标记
        foreach (var anchor in anchors)
        {
            Vector3 pos = anchor.transform.position;
            if (anchor.PossessionEnabled)
            {
                Handles.color = COLOR_ANCHOR;
                Handles.DrawWireDisc(pos, Vector3.forward, 0.4f);
                Handles.Label(pos + Vector3.up * 0.6f, $"⊕ {anchor.AnchorId}",
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = COLOR_ANCHOR } });
            }
            else
            {
                Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
                Handles.DrawWireDisc(pos, Vector3.forward, 0.3f);
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // 危险区域
    // ═══════════════════════════════════════════════════

    private static void DrawHazardZones()
    {
        // 查找所有危险元素并绘制影响范围
        // Spike Traps
        GameObject levelRoot = GameObject.Find("AsciiLevel_Root");
        if (levelRoot == null) return;

        foreach (Transform child in levelRoot.transform)
        {
            if (child.name.Contains("Spike") || child.name.Contains("Fire") || child.name.Contains("Saw"))
            {
                Vector3 pos = child.position;
                Handles.color = COLOR_HAZARD;

                // 绘制危险范围圈
                float radius = child.name.Contains("Saw") ? 0.8f : 0.5f;
                Handles.DrawWireDisc(pos, Vector3.forward, radius);

                // 绘制警告标记
                Handles.DrawSolidDisc(pos, Vector3.forward, 0.08f);
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // 检查点
    // ═══════════════════════════════════════════════════

    private static void DrawCheckpoints()
    {
        GameObject levelRoot = GameObject.Find("AsciiLevel_Root");
        if (levelRoot == null) return;

        foreach (Transform child in levelRoot.transform)
        {
            if (child.name.Contains("Checkpoint"))
            {
                Vector3 pos = child.position;
                Handles.color = COLOR_CHECKPOINT;

                // 绘制检查点标记（旗帜形状）
                Handles.DrawSolidDisc(pos, Vector3.forward, 0.2f);
                Handles.DrawLine(pos, pos + Vector3.up * 1.5f);

                // 绘制重生区域
                Handles.DrawWireDisc(pos + Vector3.up * 0.5f, Vector3.forward, 1.5f);
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // 传送带方向
    // ═══════════════════════════════════════════════════

    private static void DrawConveyorDirections()
    {
        GameObject levelRoot = GameObject.Find("AsciiLevel_Root");
        if (levelRoot == null) return;

        foreach (Transform child in levelRoot.transform)
        {
            if (child.name.Contains("Conveyor"))
            {
                Vector3 pos = child.position;
                Handles.color = COLOR_CONVEYOR;

                // 绘制方向箭头
                ConveyorBelt belt = child.GetComponent<ConveyorBelt>();
                if (belt != null)
                {
                    var speedField = typeof(ConveyorBelt).GetField("conveyorSpeed",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    float spd = speedField != null ? (float)speedField.GetValue(belt) : -3f;
                    float dir = spd > 0 ? 1f : -1f;
                    DrawArrow(pos, Vector3.right * dir, 0.4f, COLOR_CONVEYOR);
                }
                else
                {
                    // 默认向左
                    DrawArrow(pos, Vector3.left, 0.4f, COLOR_CONVEYOR);
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // 工具方法
    // ═══════════════════════════════════════════════════

    private static void DrawArrow(Vector3 position, Vector3 direction, float size, Color color)
    {
        Handles.color = color;
        Vector3 tip = position + direction * size;
        Handles.DrawLine(position, tip);

        // 箭头头部
        Vector3 right = Vector3.Cross(direction, Vector3.forward).normalized;
        Vector3 arrowLeft = tip - direction * size * 0.4f + right * size * 0.25f;
        Vector3 arrowRight = tip - direction * size * 0.4f - right * size * 0.25f;
        Handles.DrawLine(tip, arrowLeft);
        Handles.DrawLine(tip, arrowRight);
    }
}
