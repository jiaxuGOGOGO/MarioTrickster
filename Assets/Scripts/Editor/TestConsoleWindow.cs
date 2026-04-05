using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// MarioTrickster Test Console — 统一测试配置窗口 (Level Studio)
/// 
/// 快捷键: Ctrl+T (Windows) / Cmd+T (Mac)
/// 菜单:   MarioTrickster → Test Console
///
/// 功能概览 (S25 升级版 — 三大选项卡):
///
///   Tab 1 — Level Builder &amp; Theming (模板生成与换肤)
///     · 一键生成白盒模板（Classic Plains / Underground Cavern）
///     · 动态元素调色板：点击按钮在 Scene 摄像机中心生成白盒预制体
///     · 一键应用主题（拖入 LevelThemeProfile，支持 Undo 撤销）
///     · 字符映射表参考卡
///     · 原有 TestSceneBuilder 的 Build/Clear 功能
///     · 关卡元素集控（LevelElementRegistry 浏览 + 聚焦）
///     · 测试报告快捷入口
///
///   Tab 2 — Teleport &amp; Reset (传送与状态管理)
///     · Stage 1~9 + GoalZone 一键传送（Mario + Trickster + Camera 硬切）
///     · 自定义坐标传送
///     · 复活 Mario / 补满能量 / 重置关卡元素
///
///   Tab 3 — Global Cheats (全局测试外挂)
///     · God Mode (无敌)：PlayerHealth.DebugGodMode
///     · No Cooldown：GameManager.NoCooldownMode
///     · Infinite Energy：EnergySystem.DebugInfiniteEnergy
///     · Instant Blend (秒速融入)：DisguiseSystem.DebugInstantBlend
///     · Time Scale 滑动条 (0.1x ~ 3.0x)
///     · Input Debug 显示开关
///     · 运行时状态监控面板
///
/// 设计原则:
///   1. 所有调试开关使用 [System.NonSerialized] + #if UNITY_EDITOR || DEVELOPMENT_BUILD 宏隔离
///   2. 所有开关默认关闭，每次 Play 自动重置，不影响 114 个自动化测试
///   3. 传送时调用 CameraController.SnapToTarget() 实现相机硬切
///   4. 不修改任何核心逻辑，仅通过公开 API 进行状态干预
///
/// Session 24: 初版创建
/// Session 25: 升级为 Level Studio（ASCII 关卡生成 + 主题换肤 + 元素调色板）
/// </summary>
public class TestConsoleWindow : EditorWindow
{
    // ═══════════════════════════════════════════════════
    // 常量
    // ═══════════════════════════════════════════════════
    private const float STAGE_WIDTH = 18f;
    private const float STAGE_GAP = 2f;
    private const float TOTAL_STAGE_UNIT = STAGE_WIDTH + STAGE_GAP; // 20

    // Stage 名称（与 TestSceneBuilder 保持一致）
    private static readonly string[] STAGE_NAMES = new string[]
    {
        "Stage 1: Mario Movement",
        "Stage 2: Trickster Movement",
        "Stage 3: Moving Platform",
        "Stage 4: Disguise System",
        "Stage 5: Prop Control",
        "Stage 6: Scan Ability",
        "Stage 7: Win/Lose & UI",
        "Stage 8: Pause System",
        "Stage 9: Level Elements",
        "GoalZone"
    };

    // ═══════════════════════════════════════════════════
    // 状态
    // ═══════════════════════════════════════════════════
    private int selectedTab = 0;
    private readonly string[] tabNames = { "Level Builder", "Teleport", "Cheats" };
    private Vector2 scrollPos;
    private Vector2 elementsScrollPos;

    // 缓存引用（PlayMode 下动态获取）
    private MarioController cachedMario;
    private TricksterController cachedTrickster;
    private GameManager cachedGameManager;
    private InputManager cachedInputManager;
    private CameraController cachedCamera;
    private PlayerHealth cachedMarioHealth;
    private PlayerHealth cachedTricksterHealth;
    private EnergySystem cachedEnergy;
    private DisguiseSystem cachedDisguise;

    // Debug 开关本地状态
    private float timeScaleValue = 1f;

    // Elements Hub 折叠状态
    private Dictionary<string, bool> categoryFoldouts = new Dictionary<string, bool>();

    // Level Builder 状态
    private int selectedTemplateIndex = 0;
    private LevelThemeProfile themeProfile;
    private bool showCharMapRef = false;
    private bool showElementPalette = true;
    private bool showElementsHub = false;
    private bool showTestReports = false;
    private bool showBuilderTools = true;

    // Teleport 状态
    private float customTeleportX = 0f;
    private float customTeleportY = 1f;

    // ═══════════════════════════════════════════════════
    // 菜单入口
    // ═══════════════════════════════════════════════════
    [MenuItem("MarioTrickster/Test Console %t", false, 10)]
    public static void ShowWindow()
    {
        var window = GetWindow<TestConsoleWindow>("Test Console");
        window.minSize = new Vector2(380, 520);
    }

    // ═══════════════════════════════════════════════════
    // 生命周期
    // ═══════════════════════════════════════════════════
    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            ClearCache();
            timeScaleValue = 1f;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Time.timeScale = 1f;
        }
        Repaint();
    }

    private void Update()
    {
        if (EditorApplication.isPlaying)
        {
            Repaint();
        }
    }

    // ═══════════════════════════════════════════════════
    // 主绘制
    // ═══════════════════════════════════════════════════
    private void OnGUI()
    {
        // 标题栏
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.FlexibleSpace();
        GUILayout.Label("MarioTrickster Level Studio", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // 运行状态指示
        EditorGUILayout.BeginHorizontal();
        Color statusColor = EditorApplication.isPlaying ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.8f, 0.2f);
        GUI.color = statusColor;
        GUILayout.Label(EditorApplication.isPlaying ? "● PLAY MODE" : "○ EDIT MODE", EditorStyles.boldLabel);
        GUI.color = Color.white;
        GUILayout.FlexibleSpace();

        if (EditorApplication.isPlaying)
        {
            int activeCount = CountActiveDebugFlags();
            if (activeCount > 0)
            {
                GUI.color = new Color(1f, 0.6f, 0.2f);
                GUILayout.Label($"[{activeCount} CHEATS ON]", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // Tab 选择
        selectedTab = GUILayout.Toolbar(selectedTab, tabNames, GUILayout.Height(28));

        EditorGUILayout.Space(4);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        switch (selectedTab)
        {
            case 0: DrawLevelBuilderTab(); break;
            case 1: DrawTeleportTab(); break;
            case 2: DrawCheatsTab(); break;
        }

        EditorGUILayout.EndScrollView();
    }

    // ═══════════════════════════════════════════════════
    // Tab 1: Level Builder & Theming
    // ═══════════════════════════════════════════════════
    private void DrawLevelBuilderTab()
    {
        // ── 区块 1: ASCII 模板生成 ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("ASCII Level Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "一键生成白盒关卡模板。所有元素使用灰色方块，先测试逻辑，后续换肤。\n仅在 EditMode 下可用。",
            MessageType.Info);

        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

        // 模板选择
        string[] templateNames = AsciiLevelGenerator.GetBuiltInTemplateNames();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Template:", GUILayout.Width(65));
        selectedTemplateIndex = EditorGUILayout.Popup(selectedTemplateIndex, templateNames);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("Generate Whitebox Level", GUILayout.Height(32)))
        {
            GenerateWhiteboxLevel();
        }
        GUI.color = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("Clear ASCII Level", GUILayout.Height(32)))
        {
            AsciiLevelGenerator.ClearGeneratedLevel();
            Debug.Log("[TestConsole] ASCII level cleared.");
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();

        // 字符映射参考
        showCharMapRef = EditorGUILayout.Foldout(showCharMapRef, "Character Map Reference", true);
        if (showCharMapRef)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(AsciiLevelGenerator.GetCharMapReference(), MessageType.None);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // ── 区块 2: 主题换肤 ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Theme System", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

        themeProfile = (LevelThemeProfile)EditorGUILayout.ObjectField(
            "Theme Profile:", themeProfile, typeof(LevelThemeProfile), false);

        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(0.5f, 0.8f, 1f);
        EditorGUI.BeginDisabledGroup(themeProfile == null);
        if (GUILayout.Button("Apply Theme (with Undo)", GUILayout.Height(28)))
        {
            ApplyThemeWithUndo();
        }
        EditorGUI.EndDisabledGroup();
        GUI.color = Color.white;

        if (GUILayout.Button("Create New Theme", GUILayout.Height(28)))
        {
            CreateNewThemeProfile();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // ── 区块 3: 动态元素调色板 ──
        showElementPalette = EditorGUILayout.Foldout(showElementPalette, "Element Palette (Spawn at Camera Center)", true, EditorStyles.foldoutHeader);
        if (showElementPalette)
        {
            DrawElementPalette();
        }

        EditorGUILayout.Space(6);

        // ── 区块 4: 原有 TestSceneBuilder 工具 ──
        showBuilderTools = EditorGUILayout.Foldout(showBuilderTools, "TestSceneBuilder (9-Stage Test Scene)", true, EditorStyles.foldoutHeader);
        if (showBuilderTools)
        {
            DrawBuilderToolsSection();
        }

        EditorGUILayout.Space(6);

        // ── 区块 5: 关卡元素集控 ──
        showElementsHub = EditorGUILayout.Foldout(showElementsHub, "Elements Hub (Registry Browser)", true, EditorStyles.foldoutHeader);
        if (showElementsHub)
        {
            DrawElementsHubSection();
        }

        EditorGUILayout.Space(6);

        // ── 区块 6: 测试报告 ──
        showTestReports = EditorGUILayout.Foldout(showTestReports, "Test Reports & Shortcuts", true, EditorStyles.foldoutHeader);
        if (showTestReports)
        {
            DrawTestReportsSection();
        }
    }

    /// <summary>生成白盒关卡</summary>
    private void GenerateWhiteboxLevel()
    {
        string template = AsciiLevelGenerator.GetBuiltInTemplate(selectedTemplateIndex);
        string[] names = AsciiLevelGenerator.GetBuiltInTemplateNames();
        string templateName = selectedTemplateIndex < names.Length ? names[selectedTemplateIndex] : "Unknown";

        // 注册 Undo
        Undo.SetCurrentGroupName($"Generate Whitebox Level: {templateName}");

        GameObject root = AsciiLevelGenerator.GenerateFromTemplate(template, true);
        if (root != null)
        {
            Undo.RegisterCreatedObjectUndo(root, $"Generate {templateName}");

            // 聚焦到生成的关卡
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[TestConsole] Whitebox level '{templateName}' generated successfully.");
        }
    }

    /// <summary>应用主题（支持 Undo）</summary>
    private void ApplyThemeWithUndo()
    {
        if (themeProfile == null) return;

        GameObject root = GameObject.Find("AsciiLevel_Root");
        if (root == null)
        {
            EditorUtility.DisplayDialog("No Level Found",
                "Please generate a whitebox level first before applying a theme.",
                "OK");
            return;
        }

        // 注册 Undo（记录所有子物体的 SpriteRenderer 状态）
        Undo.SetCurrentGroupName($"Apply Theme: {themeProfile.themeName}");

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer sr in renderers)
        {
            Undo.RecordObject(sr, "Apply Theme Sprite");
        }

        // 记录相机背景色
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Undo.RecordObject(mainCam, "Apply Theme Camera BG");
        }

        AsciiLevelGenerator.ApplyTheme(themeProfile);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[TestConsole] Theme '{themeProfile.themeName}' applied with Undo support.");
    }

    /// <summary>创建新的 Theme Profile 资产</summary>
    private void CreateNewThemeProfile()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Level Theme Profile",
            "NewLevelTheme",
            "asset",
            "Choose where to save the new theme profile");

        if (string.IsNullOrEmpty(path)) return;

        LevelThemeProfile newProfile = ScriptableObject.CreateInstance<LevelThemeProfile>();
        newProfile.themeName = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(newProfile, path);
        AssetDatabase.SaveAssets();

        themeProfile = newProfile;
        EditorGUIUtility.PingObject(newProfile);
        Selection.activeObject = newProfile;

        Debug.Log($"[TestConsole] New theme profile created at: {path}");
    }

    /// <summary>绘制动态元素调色板</summary>
    private void DrawElementPalette()
    {
        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.HelpBox(
            "点击按钮在 Scene 视图摄像机中心生成白盒元素，自动对齐网格。\n生成后可在 Scene 中手动拖拽调整位置。",
            MessageType.Info);

        // 陷阱类
        EditorGUILayout.LabelField("Traps", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Spike Trap", '^', new Color(0.85f, 0.25f, 0.25f));
        DrawPaletteButton("Fire Trap", '~', new Color(1f, 0.5f, 0.1f));
        DrawPaletteButton("Pendulum", 'P', new Color(0.7f, 0.45f, 0.2f));
        EditorGUILayout.EndHorizontal();

        // 平台类
        EditorGUILayout.LabelField("Platforms", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Bouncy", 'B', new Color(0.3f, 0.85f, 0.3f));
        DrawPaletteButton("Collapse", 'C', new Color(0.8f, 0.65f, 0.3f));
        DrawPaletteButton("OneWay", '-', new Color(0.5f, 0.75f, 0.9f));
        DrawPaletteButton("Moving", '>', new Color(0.5f, 0.5f, 0.9f));
        EditorGUILayout.EndHorizontal();

        // 敌人类
        EditorGUILayout.LabelField("Enemies", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Bounce Enemy", 'E', new Color(0.9f, 0.2f, 0.6f));
        DrawPaletteButton("Simple Enemy", 'e', new Color(0.9f, 0.2f, 0.6f));
        EditorGUILayout.EndHorizontal();

        // 通道/墙壁类
        EditorGUILayout.LabelField("Passages & Walls", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Fake Wall", 'F', new Color(0.55f, 0.55f, 0.65f));
        DrawPaletteButton("Hidden Passage", 'H', new Color(0.4f, 0.7f, 0.55f));
        EditorGUILayout.EndHorizontal();

        // 基础方块
        EditorGUILayout.LabelField("Blocks", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Ground", '#', new Color(0.55f, 0.55f, 0.55f));
        DrawPaletteButton("Platform", '=', new Color(0.7f, 0.7f, 0.7f));
        DrawPaletteButton("Wall", 'W', new Color(0.4f, 0.4f, 0.4f));
        EditorGUILayout.EndHorizontal();

        // 其他
        EditorGUILayout.LabelField("Other", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Collectible", 'o', new Color(1f, 0.85f, 0.2f));
        DrawPaletteButton("Goal Zone", 'G', new Color(0.2f, 1f, 0.4f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUI.EndDisabledGroup();
    }

    /// <summary>调色板按钮：在 Scene 摄像机中心生成元素</summary>
    private void DrawPaletteButton(string label, char charKey, Color color)
    {
        GUI.color = color;
        if (GUILayout.Button(label, GUILayout.Height(25)))
        {
            SpawnElementAtSceneCenter(charKey, label);
        }
        GUI.color = Color.white;
    }

    /// <summary>在 Scene 视图摄像机中心生成一个元素（对齐网格）</summary>
    private void SpawnElementAtSceneCenter(char charKey, string label)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            Debug.LogWarning("[TestConsole] No active Scene View found.");
            return;
        }

        // 获取 Scene 视图焦点中心的世界坐标
        // [AI防坑警告] 必须使用 sceneView.pivot 而非 camera.transform.position
        // camera.transform.position 是 Scene 摄像机的 3D 位置（含透视偏移），
        // 在 2D 模式下与画面可视中心存在较大偏差。
        // pivot 才是用户在 Scene 视图中看到的真正焦点中心。
        Vector3 camCenter = sceneView.pivot;
        camCenter.z = 0;

        // 对齐到网格（四舍五入到整数）
        int gridX = Mathf.RoundToInt(camCenter.x);
        int gridY = Mathf.RoundToInt(camCenter.y);

        // 使用 ASCII 生成器的单字符模板来生成
        // 确保有 Root 节点
        GameObject root = GameObject.Find("AsciiLevel_Root");
        if (root == null)
        {
            root = new GameObject("AsciiLevel_Root");
            Undo.RegisterCreatedObjectUndo(root, "Create ASCII Root");
        }

        // 生成单个元素（通过临时模板）
        string miniTemplate = charKey.ToString();
        // 直接调用生成器的公共 API，但不清除现有内容
        GameObject tempRoot = AsciiLevelGenerator.GenerateFromTemplate(miniTemplate, false);

        if (tempRoot != null && tempRoot.transform.childCount > 0)
        {
            // 将生成的子物体移到正确位置并挂到主 Root 下
            List<Transform> children = new List<Transform>();
            foreach (Transform child in tempRoot.transform)
            {
                children.Add(child);
            }

            foreach (Transform child in children)
            {
                // 调整位置到 Scene 摄像机中心
                child.position = new Vector3(gridX, gridY, 0);
                child.name = child.name.Replace("_0_0", $"_{gridX}_{gridY}");
                child.parent = root.transform;
                Undo.RegisterCreatedObjectUndo(child.gameObject, $"Spawn {label}");
            }

            // 删除临时 Root
            Object.DestroyImmediate(tempRoot);

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[TestConsole] Spawned '{label}' at grid ({gridX}, {gridY}).");
        }
    }

    /// <summary>绘制 TestSceneBuilder 工具区块</summary>
    private void DrawBuilderToolsSection()
    {
        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("9-Stage Test Scene", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(0.5f, 1f, 0.5f);
        if (GUILayout.Button("Build Test Scene", GUILayout.Height(32)))
        {
            TestSceneBuilder.BuildTestScene();
        }
        GUI.color = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("Clear Test Scene", GUILayout.Height(32)))
        {
            TestSceneBuilder.ClearTestScene();
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUI.EndDisabledGroup();
    }

    /// <summary>绘制关卡元素集控区块</summary>
    private void DrawElementsHubSection()
    {
        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);

        EditorGUILayout.BeginVertical("box");

        int totalCount = LevelElementRegistry.TotalCount;
        EditorGUILayout.LabelField($"Registered Elements: {totalCount}");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset All Elements", GUILayout.Height(25)))
        {
            LevelElementRegistry.ResetAll();
            Debug.Log("[TestConsole] All elements reset.");
        }
        if (GUILayout.Button("Print Summary", GUILayout.Height(25)))
        {
            LevelElementRegistry.DebugPrintSummary();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        elementsScrollPos = EditorGUILayout.BeginScrollView(elementsScrollPos, GUILayout.MinHeight(150));

        var stats = LevelElementRegistry.GetCategoryStats();
        foreach (var kvp in stats)
        {
            string catName = kvp.Key.ToString();
            int count = kvp.Value;

            if (!categoryFoldouts.ContainsKey(catName))
                categoryFoldouts[catName] = true;

            categoryFoldouts[catName] = EditorGUILayout.Foldout(categoryFoldouts[catName],
                $"{catName} ({count})", true, EditorStyles.foldoutHeader);

            if (categoryFoldouts[catName])
            {
                EditorGUI.indentLevel++;
                var elements = LevelElementRegistry.GetByCategory(kvp.Key);
                foreach (var elem in elements)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(elem.Name, EditorStyles.linkLabel))
                    {
                        if (elem.Component != null)
                        {
                            Selection.activeGameObject = elem.Component.gameObject;
                            SceneView.lastActiveSceneView?.FrameSelected();
                        }
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"[{elem.Tags}]", EditorStyles.miniLabel, GUILayout.Width(120));
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }

        if (totalCount == 0)
        {
            EditorGUILayout.HelpBox(
                "当前没有已注册的关卡元素。\n请先生成测试场景并进入 PlayMode。",
                MessageType.Warning);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUI.EndDisabledGroup();
    }

    /// <summary>绘制测试报告和快捷键区块</summary>
    private void DrawTestReportsSection()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Test Reports", EditorStyles.boldLabel);

        if (GUILayout.Button("Run EditMode Tests", GUILayout.Height(25)))
        {
            EditorApplication.ExecuteMenuItem("MarioTrickster/Run Tests/Export Full Report (EditMode)");
        }
        if (GUILayout.Button("Run PlayMode Tests", GUILayout.Height(25)))
        {
            EditorApplication.ExecuteMenuItem("MarioTrickster/Run Tests/Export Full Report (PlayMode)");
        }
        if (GUILayout.Button("Run All Tests + Report", GUILayout.Height(25)))
        {
            EditorApplication.ExecuteMenuItem("MarioTrickster/Run Tests/Export Full Report (All)");
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Keyboard Shortcuts", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Ctrl+T       Open Test Console");
        EditorGUILayout.LabelField("F5           Quick Restart Level");
        EditorGUILayout.LabelField("F9           Toggle No Cooldown");
        EditorGUILayout.LabelField("ESC          Pause/Resume");
        EditorGUILayout.LabelField("R            Restart (Round Over)");
        EditorGUILayout.LabelField("N            Next Round (Round Over)");

        EditorGUILayout.EndVertical();
    }

    // ═══════════════════════════════════════════════════
    // Tab 2: 传送与状态管理
    // ═══════════════════════════════════════════════════
    private void DrawTeleportTab()
    {
        EditorGUILayout.LabelField("Stage Quick Teleport", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "一键将 Mario + Trickster 传送到指定 Stage，相机硬切跟随。\n仅在 PlayMode 下可用。",
            MessageType.Info);

        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);

        // Stage 按钮网格（2 列布局）
        EditorGUILayout.BeginVertical("box");
        for (int i = 0; i < STAGE_NAMES.Length; i += 2)
        {
            EditorGUILayout.BeginHorizontal();
            DrawStageButton(i);
            if (i + 1 < STAGE_NAMES.Length)
            {
                DrawStageButton(i + 1);
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // 自定义坐标传送
        EditorGUILayout.LabelField("Custom Teleport", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        customTeleportX = EditorGUILayout.FloatField("X", customTeleportX);
        customTeleportY = EditorGUILayout.FloatField("Y", customTeleportY);
        if (GUILayout.Button("Go", GUILayout.Width(40)))
        {
            TeleportBothPlayers(new Vector3(customTeleportX, customTeleportY, 0));
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // 角色状态快速操作
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Revive Mario\n(满血复活)", GUILayout.Height(40)))
        {
            ReviveMario();
        }
        if (GUILayout.Button("Refill Energy\n(补满能量)", GUILayout.Height(40)))
        {
            RefillEnergy();
        }
        if (GUILayout.Button("Reset Elements\n(重置关卡)", GUILayout.Height(40)))
        {
            ResetAllElements();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();
    }

    private void DrawStageButton(int index)
    {
        bool isGoal = index == STAGE_NAMES.Length - 1;
        if (isGoal) GUI.color = new Color(0.5f, 1f, 0.5f);

        if (GUILayout.Button(STAGE_NAMES[index], GUILayout.Height(30)))
        {
            TeleportToStage(index);
        }

        if (isGoal) GUI.color = Color.white;
    }

    // ═══════════════════════════════════════════════════
    // Tab 3: 全局测试外挂
    // ═══════════════════════════════════════════════════
    private void DrawCheatsTab()
    {
        EditorGUILayout.LabelField("Global Test Cheats", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "所有开关默认关闭，每次 Play 自动重置。\n不影响自动化测试。仅在 PlayMode 下可用。\n" +
            "所有作弊代码被 #if UNITY_EDITOR || DEVELOPMENT_BUILD 宏包裹，Release 包零残留。",
            MessageType.Info);

        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);

        EnsureCache();

        EditorGUILayout.BeginVertical("box");

        // ── Mario 调试 ──
        EditorGUILayout.LabelField("Mario", EditorStyles.boldLabel);

        DrawDebugToggle(
            "God Mode (无敌)",
            "不扣血、不触发死亡",
            GetGodMode(),
            (val) => SetGodMode(val),
            new Color(1f, 0.3f, 0.3f));

        EditorGUILayout.Space(4);

        // ── Trickster 调试 ──
        EditorGUILayout.LabelField("Trickster", EditorStyles.boldLabel);

        DrawDebugToggle(
            "No Cooldown (无冷却)",
            "伪装/扫描/道具冷却立即清零",
            GetNoCooldown(),
            (val) => SetNoCooldown(val),
            new Color(0.3f, 0.7f, 1f));

        DrawDebugToggle(
            "Infinite Energy (无限能量)",
            "能量不消耗，始终满值",
            GetInfiniteEnergy(),
            (val) => SetInfiniteEnergy(val),
            new Color(0.3f, 0.7f, 1f));

        DrawDebugToggle(
            "Instant Blend (秒速融入)",
            "伪装后立即进入完全融入状态",
            GetInstantBlend(),
            (val) => SetInstantBlend(val),
            new Color(0.3f, 0.7f, 1f));

        EditorGUILayout.Space(4);

        // ── 全局设置 ──
        EditorGUILayout.LabelField("Global", EditorStyles.boldLabel);

        // Time Scale
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Time Scale", GUILayout.Width(80));
        float newTimeScale = EditorGUILayout.Slider(timeScaleValue, 0.1f, 3.0f);
        if (!Mathf.Approximately(newTimeScale, timeScaleValue))
        {
            timeScaleValue = newTimeScale;
            Time.timeScale = timeScaleValue;
        }
        if (GUILayout.Button("1x", GUILayout.Width(30)))
        {
            timeScaleValue = 1f;
            Time.timeScale = 1f;
        }
        EditorGUILayout.EndHorizontal();

        // 快捷 Time Scale 按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("0.1x")) { timeScaleValue = 0.1f; Time.timeScale = 0.1f; }
        if (GUILayout.Button("0.25x")) { timeScaleValue = 0.25f; Time.timeScale = 0.25f; }
        if (GUILayout.Button("0.5x")) { timeScaleValue = 0.5f; Time.timeScale = 0.5f; }
        if (GUILayout.Button("1x")) { timeScaleValue = 1f; Time.timeScale = 1f; }
        if (GUILayout.Button("2x")) { timeScaleValue = 2f; Time.timeScale = 2f; }
        if (GUILayout.Button("3x")) { timeScaleValue = 3f; Time.timeScale = 3f; }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // Input Debug
        DrawDebugToggle(
            "Input Debug (输入调试)",
            "在屏幕左上角显示按键状态",
            GetInputDebug(),
            (val) => SetInputDebug(val),
            new Color(0.8f, 0.8f, 0.3f));

        EditorGUILayout.EndVertical();

        // ── 一键全开 / 全关 ──
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(1f, 0.6f, 0.2f);
        if (GUILayout.Button("Enable All Cheats", GUILayout.Height(28)))
        {
            SetGodMode(true);
            SetNoCooldown(true);
            SetInfiniteEnergy(true);
            SetInstantBlend(true);
        }
        GUI.color = Color.white;
        if (GUILayout.Button("Disable All Cheats", GUILayout.Height(28)))
        {
            SetGodMode(false);
            SetNoCooldown(false);
            SetInfiniteEnergy(false);
            SetInstantBlend(false);
            timeScaleValue = 1f;
            Time.timeScale = 1f;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();

        // ── 运行时状态监控 ──
        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EnsureCache();

            if (cachedMarioHealth != null)
            {
                EditorGUILayout.LabelField($"Mario HP: {cachedMarioHealth.CurrentHealth}/{cachedMarioHealth.MaxHealth}");
            }
            if (cachedEnergy != null)
            {
                EditorGUILayout.LabelField($"Trickster Energy: {cachedEnergy.CurrentEnergy:F0}/{cachedEnergy.MaxEnergy:F0} ({cachedEnergy.EnergyPercent * 100:F0}%)");
            }
            if (cachedDisguise != null)
            {
                EditorGUILayout.LabelField($"Disguise: {(cachedDisguise.IsDisguised ? "YES" : "No")} | Blended: {(cachedDisguise.IsFullyBlended ? "YES" : "No")}");
            }
            if (cachedGameManager != null)
            {
                EditorGUILayout.LabelField($"Game State: {cachedGameManager.CurrentState} | Timer: {cachedGameManager.GameTimer:F1}s");
                EditorGUILayout.LabelField($"Score: Mario {cachedGameManager.MarioWins} - Trickster {cachedGameManager.TricksterWins} | Round {cachedGameManager.CurrentRound}");
            }

            EditorGUILayout.EndVertical();
        }
    }

    // ═══════════════════════════════════════════════════
    // UI 辅助
    // ═══════════════════════════════════════════════════

    private void DrawDebugToggle(string label, string tooltip, bool currentValue, System.Action<bool> setter, Color activeColor)
    {
        EditorGUILayout.BeginHorizontal();

        if (currentValue)
        {
            GUI.color = activeColor;
        }

        bool newValue = EditorGUILayout.Toggle(new GUIContent(label, tooltip), currentValue);
        if (newValue != currentValue)
        {
            setter(newValue);
        }

        if (currentValue)
        {
            GUILayout.Label("ON", EditorStyles.boldLabel, GUILayout.Width(25));
        }

        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    // ═══════════════════════════════════════════════════
    // 传送逻辑
    // ═══════════════════════════════════════════════════

    // [AI防坑警告] 传送后必须调用 CameraController.SnapToTarget() 实现相机硬切！
    // 绝对不能让相机花 5 秒钟缓慢滑动过去，这是核心红线。
    // SnapToTarget() 会重置 smoothDampVelocity、lookAheadVelocity、currentLookAhead、
    // smoothedSpeed、isMoving、lastTargetPosition 等所有平滑状态。

    /// <summary>传送到指定 Stage（0-based index，最后一个为 GoalZone）</summary>
    private void TeleportToStage(int stageIndex)
    {
        if (!EditorApplication.isPlaying) return;
        EnsureCache();

        Vector3 targetPos;

        if (stageIndex < 9)
        {
            float stageStartX = stageIndex * TOTAL_STAGE_UNIT;
            targetPos = new Vector3(stageStartX + 3f, 1f, 0f);
        }
        else
        {
            float s9 = 8 * TOTAL_STAGE_UNIT;
            float s9SubWidth = 8f;
            float goalX = s9 + 9 * s9SubWidth + 2f;
            targetPos = new Vector3(goalX - 3f, 1f, 0f);
        }

        TeleportBothPlayers(targetPos);

        Debug.Log($"[TestConsole] Teleported to {STAGE_NAMES[stageIndex]} at ({targetPos.x:F1}, {targetPos.y:F1})");
    }

    // [AI防坑警告] 此方法末尾的 SnapToTarget() 调用是核心红线，绝对不能删除！
    // 没有它，传送后相机会花 5 秒慢飘过去，严重浪费测试时间。
    /// <summary>将 Mario 和 Trickster 传送到指定位置，相机硬切</summary>
    private void TeleportBothPlayers(Vector3 position)
    {
        if (!EditorApplication.isPlaying) return;
        EnsureCache();

        // 传送 Mario
        if (cachedMario != null)
        {
            cachedMario.transform.position = position;
            Rigidbody2D marioRb = cachedMario.GetComponent<Rigidbody2D>();
            if (marioRb != null) marioRb.velocity = Vector2.zero;
        }

        // 传送 Trickster（偏移 2 格，避免重叠）
        if (cachedTrickster != null)
        {
            cachedTrickster.transform.position = position + Vector3.right * 2f;
            Rigidbody2D tricksterRb = cachedTrickster.GetComponent<Rigidbody2D>();
            if (tricksterRb != null) tricksterRb.velocity = Vector2.zero;
        }

        // [AI防坑警告] 相机硬切 — 核心红线，绝对不能删除或改为平滑跟随！
        if (cachedCamera != null)
        {
            cachedCamera.SnapToTarget();
        }
    }

    // ═══════════════════════════════════════════════════
    // 角色状态操作
    // ═══════════════════════════════════════════════════

    private void ReviveMario()
    {
        if (!EditorApplication.isPlaying) return;
        EnsureCache();

        if (cachedMarioHealth != null)
        {
            cachedMarioHealth.ResetHealth();
            Debug.Log("[TestConsole] Mario revived (full HP).");
        }

        if (cachedMario != null)
        {
            cachedMario.enabled = true;
            SpriteRenderer sr = cachedMario.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = 1f;
                sr.color = c;
            }
        }
    }

    private void RefillEnergy()
    {
        if (!EditorApplication.isPlaying) return;
        EnsureCache();

        if (cachedEnergy != null)
        {
            cachedEnergy.ResetEnergy();
            Debug.Log("[TestConsole] Trickster energy refilled.");
        }
    }

    private void ResetAllElements()
    {
        if (!EditorApplication.isPlaying) return;

        LevelElementRegistry.ResetAll();

        GoalZone[] goalZones = Object.FindObjectsOfType<GoalZone>();
        foreach (GoalZone gz in goalZones)
        {
            gz.ResetTrigger();
        }

        ControllablePropBase[] props = Object.FindObjectsOfType<ControllablePropBase>();
        foreach (ControllablePropBase prop in props)
        {
            prop.ResetUses();
        }

        Debug.Log("[TestConsole] All level elements reset.");
    }

    // ═══════════════════════════════════════════════════
    // Debug 开关 Getter/Setter
    // ═══════════════════════════════════════════════════

    private bool GetGodMode()
    {
        EnsureCache();
        if (cachedMarioHealth == null) return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return cachedMarioHealth.DebugGodMode;
#else
        return false;
#endif
    }

    private void SetGodMode(bool value)
    {
        EnsureCache();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (cachedMarioHealth != null) cachedMarioHealth.DebugGodMode = value;
        if (cachedTricksterHealth != null) cachedTricksterHealth.DebugGodMode = value;
#endif
        Debug.Log($"[TestConsole] God Mode: {(value ? "ON" : "OFF")}");
    }

    private bool GetNoCooldown()
    {
        EnsureCache();
        return cachedGameManager != null && cachedGameManager.NoCooldownMode;
    }

    private void SetNoCooldown(bool value)
    {
        EnsureCache();
        if (cachedGameManager == null) return;

        bool current = cachedGameManager.NoCooldownMode;
        if (current != value)
        {
            var field = typeof(GameManager).GetField("noCooldownMode",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(cachedGameManager, value);
                Debug.Log($"[TestConsole] No Cooldown: {(value ? "ON" : "OFF")}");
            }
        }
    }

    private bool GetInfiniteEnergy()
    {
        EnsureCache();
        if (cachedEnergy == null) return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return cachedEnergy.DebugInfiniteEnergy;
#else
        return false;
#endif
    }

    private void SetInfiniteEnergy(bool value)
    {
        EnsureCache();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (cachedEnergy != null)
        {
            cachedEnergy.DebugInfiniteEnergy = value;
            Debug.Log($"[TestConsole] Infinite Energy: {(value ? "ON" : "OFF")}");
        }
#endif
    }

    private bool GetInstantBlend()
    {
        EnsureCache();
        if (cachedDisguise == null) return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return cachedDisguise.DebugInstantBlend;
#else
        return false;
#endif
    }

    private void SetInstantBlend(bool value)
    {
        EnsureCache();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (cachedDisguise != null)
        {
            cachedDisguise.DebugInstantBlend = value;
            Debug.Log($"[TestConsole] Instant Blend: {(value ? "ON" : "OFF")}");
        }
#endif
    }

    private bool GetInputDebug()
    {
        EnsureCache();
        if (cachedInputManager == null) return false;

        var field = typeof(InputManager).GetField("showDebugInput",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field != null && (bool)field.GetValue(cachedInputManager);
    }

    private void SetInputDebug(bool value)
    {
        EnsureCache();
        if (cachedInputManager == null) return;

        var field = typeof(InputManager).GetField("showDebugInput",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(cachedInputManager, value);
            Debug.Log($"[TestConsole] Input Debug: {(value ? "ON" : "OFF")}");
        }
    }

    // ═══════════════════════════════════════════════════
    // 缓存管理
    // ═══════════════════════════════════════════════════

    private void EnsureCache()
    {
        if (!EditorApplication.isPlaying) return;

        if (cachedMario == null)
            cachedMario = Object.FindObjectOfType<MarioController>();

        if (cachedTrickster == null)
            cachedTrickster = Object.FindObjectOfType<TricksterController>();

        if (cachedGameManager == null)
            cachedGameManager = GameManager.Instance ?? Object.FindObjectOfType<GameManager>();

        if (cachedInputManager == null)
            cachedInputManager = Object.FindObjectOfType<InputManager>();

        if (cachedCamera == null)
            cachedCamera = Object.FindObjectOfType<CameraController>();

        if (cachedMario != null && cachedMarioHealth == null)
            cachedMarioHealth = cachedMario.GetComponent<PlayerHealth>();

        if (cachedTrickster != null)
        {
            if (cachedTricksterHealth == null)
                cachedTricksterHealth = cachedTrickster.GetComponent<PlayerHealth>();
            if (cachedEnergy == null)
                cachedEnergy = cachedTrickster.GetComponent<EnergySystem>();
            if (cachedDisguise == null)
                cachedDisguise = cachedTrickster.GetComponent<DisguiseSystem>();
        }
    }

    private void ClearCache()
    {
        cachedMario = null;
        cachedTrickster = null;
        cachedGameManager = null;
        cachedInputManager = null;
        cachedCamera = null;
        cachedMarioHealth = null;
        cachedTricksterHealth = null;
        cachedEnergy = null;
        cachedDisguise = null;
    }

    // ═══════════════════════════════════════════════════
    // 辅助方法
    // ═══════════════════════════════════════════════════

    private int CountActiveDebugFlags()
    {
        int count = 0;
        if (GetGodMode()) count++;
        if (GetNoCooldown()) count++;
        if (GetInfiniteEnergy()) count++;
        if (GetInstantBlend()) count++;
        if (!Mathf.Approximately(timeScaleValue, 1f)) count++;
        return count;
    }
}
