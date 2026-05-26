using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// CreationFlowToolbar — GBG 风格的 Scene 视图创作工具栏
///
/// 核心理念（来自 Game Builder Garage）：
///   GBG 的编辑界面顶部有一排清晰的工具按钮，
///   用户随时知道自己在做什么、能做什么，不需要翻菜单。
///
/// 在 MarioTrickster 中的映射：
///   在 Scene 视图顶部显示一排【创作模式】按钮：
///   [放置] [连接] [测试] [配方] [向导]
///   
///   每个模式有明确的视觉反馈和操作逻辑：
///   - 放置模式：点击/拖拽放置元素（现有 Palette + Brush）
///   - 连接模式：查看/编辑元素间的关系（NodeConnectionVisualizer）
///   - 测试模式：一键 Play + 实时调试（QuickPlayController）
///   - 配方模式：打开 BehaviorRecipeLibrary 面板
///   - 向导模式：打开 SmartSnippetWizard
///
/// 额外功能：
///   - 面包屑导航：显示当前关卡结构层级
///   - 快速统计：显示元素数量、附身点数量等
///   - 一键操作：新建关卡 / 清空 / 验证
/// </summary>
[InitializeOnLoad]
public static class CreationFlowToolbar
{
    // ═══════════════════════════════════════════════════
    // 创作模式
    // ═══════════════════════════════════════════════════
    public enum CreationMode
    {
        Place,      // 放置模式
        Connect,    // 连接模式
        Test,       // 测试模式
        Recipe,     // 配方模式
        Wizard      // 向导模式
    }

    private const string PREF_MODE = "MarioTrickster_CFT_Mode";
    private const string PREF_SHOW_STATS = "MarioTrickster_CFT_ShowStats";
    private const string PREF_TOOLBAR_ENABLED = "MarioTrickster_CFT_Enabled";

    public static CreationMode CurrentMode
    {
        get => (CreationMode)EditorPrefs.GetInt(PREF_MODE, 0);
        set
        {
            EditorPrefs.SetInt(PREF_MODE, (int)value);
            OnModeChanged(value);
        }
    }

    public static bool IsEnabled
    {
        get => EditorPrefs.GetBool(PREF_TOOLBAR_ENABLED, true);
        set => EditorPrefs.SetBool(PREF_TOOLBAR_ENABLED, value);
    }

    public static bool ShowStats
    {
        get => EditorPrefs.GetBool(PREF_SHOW_STATS, true);
        set => EditorPrefs.SetBool(PREF_SHOW_STATS, value);
    }

    // ═══════════════════════════════════════════════════
    // 初始化
    // ═══════════════════════════════════════════════════
    static CreationFlowToolbar()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    // ═══════════════════════════════════════════════════
    // 菜单
    // ═══════════════════════════════════════════════════

    [MenuItem("MarioTrickster/Toggle Creation Toolbar _F8", false, 103)]
    public static void ToggleToolbar()
    {
        IsEnabled = !IsEnabled;
        SceneView.RepaintAll();
    }

    // ═══════════════════════════════════════════════════
    // Scene GUI
    // ═══════════════════════════════════════════════════

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!IsEnabled) return;

        Handles.BeginGUI();
        DrawToolbar(sceneView);
        if (ShowStats)
            DrawQuickStats(sceneView);
        Handles.EndGUI();
    }

    // ═══════════════════════════════════════════════════
    // 工具栏绘制
    // ═══════════════════════════════════════════════════

    private static void DrawToolbar(SceneView sceneView)
    {
        float toolbarWidth = 460f;
        float toolbarHeight = 32f;
        float startX = (sceneView.position.width - toolbarWidth) / 2f;
        Rect toolbarRect = new Rect(startX, 4, toolbarWidth, toolbarHeight);

        // 背景
        GUI.color = new Color(0.12f, 0.12f, 0.15f, 0.92f);
        GUI.DrawTexture(toolbarRect, EditorGUIUtility.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(toolbarRect);
        GUILayout.BeginHorizontal();
        GUILayout.Space(8);

        // 模式按钮
        DrawModeButton("🧱 放置", CreationMode.Place, new Color(0.4f, 0.8f, 0.4f));
        DrawModeButton("🔗 连接", CreationMode.Connect, new Color(0.4f, 0.6f, 1f));
        DrawModeButton("▶ 测试", CreationMode.Test, new Color(1f, 0.8f, 0.2f));
        DrawModeButton("📦 配方", CreationMode.Recipe, new Color(0.9f, 0.5f, 0.9f));
        DrawModeButton("✨ 向导", CreationMode.Wizard, new Color(0.2f, 0.9f, 0.9f));

        GUILayout.Space(12);

        // 快捷操作
        GUI.color = new Color(0.7f, 0.7f, 0.7f);
        if (GUILayout.Button("新建", GUILayout.Width(40), GUILayout.Height(24)))
        {
            CreateNewLevel();
        }
        if (GUILayout.Button("验证", GUILayout.Width(40), GUILayout.Height(24)))
        {
            ValidateLevel();
        }
        GUI.color = Color.white;

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private static void DrawModeButton(string label, CreationMode mode, Color color)
    {
        bool isActive = (CurrentMode == mode);
        GUI.color = isActive ? color : new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 0.8f);

        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11,
            fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal,
            padding = new RectOffset(6, 6, 4, 4)
        };

        if (GUILayout.Button(label, style, GUILayout.Height(24)))
        {
            CurrentMode = mode;
        }

        GUI.color = Color.white;
    }

    // ═══════════════════════════════════════════════════
    // 快速统计
    // ═══════════════════════════════════════════════════

    private static void DrawQuickStats(SceneView sceneView)
    {
        float statsWidth = 300f;
        float statsHeight = 20f;
        Rect statsRect = new Rect(
            (sceneView.position.width - statsWidth) / 2f,
            38,
            statsWidth,
            statsHeight);

        // 统计信息
        GameObject root = GameObject.Find("AsciiLevel_Root");
        if (root == null) return;

        int elementCount = root.transform.childCount;
        int anchorCount = 0;
        PossessionAnchor[] anchors = Object.FindObjectsOfType<PossessionAnchor>(true);
        foreach (var a in anchors)
            if (a.PossessionEnabled) anchorCount++;

        string stats = $"元素: {elementCount} | 附身点: {anchorCount} | 模式: {GetModeName(CurrentMode)}";

        GUIStyle statsStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.7f, 0.7f, 0.7f, 0.8f) },
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10
        };

        GUI.Label(statsRect, stats, statsStyle);
    }

    // ═══════════════════════════════════════════════════
    // 模式切换回调
    // ═══════════════════════════════════════════════════

    private static void OnModeChanged(CreationMode newMode)
    {
        switch (newMode)
        {
            case CreationMode.Place:
                NodeConnectionVisualizer.IsEnabled = false;
                // 确保 Level Studio 窗口打开
                EditorWindow.GetWindow<TestConsoleWindow>("Level Studio", false);
                break;

            case CreationMode.Connect:
                NodeConnectionVisualizer.IsEnabled = true;
                break;

            case CreationMode.Test:
                QuickPlayController.ToggleQuickPlay();
                break;

            case CreationMode.Recipe:
                // 打开 Level Studio 并切换到配方 tab（通过反射）
                var studioWindow = EditorWindow.GetWindow<TestConsoleWindow>("Level Studio", false);
                var tabField = typeof(TestConsoleWindow).GetField("selectedTab",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (tabField != null)
                    tabField.SetValue(studioWindow, 0); // Level Design tab
                studioWindow.Repaint();
                break;

            case CreationMode.Wizard:
                SmartSnippetWizard.ShowWindow();
                break;
        }

        SceneView.RepaintAll();
    }

    // ═══════════════════════════════════════════════════
    // 快捷操作
    // ═══════════════════════════════════════════════════

    private static void CreateNewLevel()
    {
        if (GameObject.Find("AsciiLevel_Root") != null)
        {
            if (!EditorUtility.DisplayDialog("新建关卡",
                "当前场景已有关卡，是否清空并新建？", "确定", "取消"))
                return;

            GameObject existing = GameObject.Find("AsciiLevel_Root");
            Undo.DestroyObjectImmediate(existing);
        }

        // 创建最小可玩关卡
        string minimalTemplate =
            "...........\n" +
            "...........\n" +
            "...........\n" +
            "M.........G\n" +
            "###########";

        Undo.SetCurrentGroupName("New Level");
        GameObject root = AsciiLevelGenerator.GenerateFromTemplate(minimalTemplate, true);
        if (root != null)
        {
            PlayableEnvironmentBuilder.EnsurePlayableEnvironment(root);
            Undo.RegisterCreatedObjectUndo(root, "New Level");
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        Debug.Log("[CreationFlow] 新关卡已创建，可以开始设计了！");
    }

    private static void ValidateLevel()
    {
        GameObject root = GameObject.Find("AsciiLevel_Root");
        if (root == null)
        {
            EditorUtility.DisplayDialog("验证", "场景中没有关卡。", "OK");
            return;
        }

        // 快速验证
        List<string> issues = new List<string>();

        // 检查 Mario 出生点
        if (Object.FindObjectOfType<MarioController>() == null)
            issues.Add("缺少 Mario");

        // 检查 Trickster 出生点
        if (Object.FindObjectOfType<TricksterController>() == null)
            issues.Add("缺少 Trickster");

        // 检查目标
        var goal = Object.FindObjectOfType<GoalZone>();
        if (goal == null)
            issues.Add("缺少终点 (GoalZone)");

        // 检查附身点
        PossessionAnchor[] anchors = Object.FindObjectsOfType<PossessionAnchor>(true);
        int enabledAnchors = 0;
        foreach (var a in anchors) if (a.PossessionEnabled) enabledAnchors++;
        if (enabledAnchors < 3)
            issues.Add($"附身点不足 ({enabledAnchors}/3)");

        if (issues.Count == 0)
        {
            EditorUtility.DisplayDialog("验证通过 ✓",
                "关卡满足所有基本要求，可以开始测试！", "OK");
        }
        else
        {
            string msg = "发现以下问题：\n\n";
            foreach (var issue in issues)
                msg += $"• {issue}\n";
            msg += "\n按 F5 测试时会自动补全缺失项。";
            EditorUtility.DisplayDialog("验证结果", msg, "OK");
        }
    }

    // ═══════════════════════════════════════════════════
    // 辅助
    // ═══════════════════════════════════════════════════

    private static string GetModeName(CreationMode mode)
    {
        switch (mode)
        {
            case CreationMode.Place: return "放置";
            case CreationMode.Connect: return "连接";
            case CreationMode.Test: return "测试";
            case CreationMode.Recipe: return "配方";
            case CreationMode.Wizard: return "向导";
            default: return "未知";
        }
    }
}
