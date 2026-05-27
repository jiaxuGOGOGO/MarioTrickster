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
/// 功能概览 (S57d 升级版 — 五大选项卡):
///
///   Tab 1 — Level Design (纯关卡设计 — 布局优先)
///   Tab 2 — Art & Theme (美术与主题 — 视觉层)
///   Tab 3 — Teleport & Reset (传送与状态管理)
///   Tab 4 — Global Cheats (全局测试外挂)
///   Tab 5 — Game Loop Tuning (对抗节奏实时调参)
///
/// UI/UX 优化原则 (v2 — 清爽化重构):
///   1. 渐进式信息披露 — 默认只展示核心操作，细节按需展开
///   2. 视觉降噪 — 用 Tooltip 替代长文 HelpBox，减少垂直空间占用
///   3. 统一色彩语义 — 绿=生成/确认，蓝=信息/导入，橙=警告/AI，红=危险/清除
///   4. 紧凑卡片布局 — 片段库等列表项用单行卡片替代多行展开
///   5. 分级标题 — SectionHeader / SubHeader / MiniHeader 三级层次
///
/// 设计原则:
///   1. 所有调试开关使用 [System.NonSerialized] + #if UNITY_EDITOR || DEVELOPMENT_BUILD 宏隔离
///   2. 所有开关默认关闭，每次 Play 自动重置，不影响 114 个自动化测试
///   3. 传送时调用 CameraController.SnapToTarget() 实现相机硬切
///   4. 不修改任何核心逻辑，仅通过公开 API 进行状态干预
/// </summary>
public partial class TestConsoleWindow : EditorWindow
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
    private readonly string[] tabNames = { "Level Design", "Art & Theme", "Teleport", "Cheats", "Game Loop Tuning" };
    private Vector2 scrollPos;
    private Vector2 elementsScrollPos;
    private GameplayLoopConfigSO gameplayLoopConfig;
    private SerializedObject gameplayLoopConfigSerialized;

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
    private bool showBuilderTools = false;

    // S26b: Custom Template Editor + Snippet Library 状态
    private bool showCustomTemplateEditor = true;
    private string customAsciiTemplate = "";
    private string _backupAsciiTemplate = ""; // AI Auto-Healer 撤销备份
    private bool showSnippetLibrary = false;
    private int selectedSnippetIndex = 0;

    // 片段拼接模式 (水平 / 垂直)
    private int snippetStitchMode = 0; // 0 = 垂直(上下), 1 = 水平(左右)

    // Art & Effects Hub 状态
    private bool showArtEffectsHub = false;

    // Gameplay Mechanics 区块状态（机制驱动关卡设计）
    private bool showGameplayMechanics = false;
    private bool showAnchorNetwork = true;
    private bool showRouteBudget = false;
    private bool showMechanicsValidation = false;

    // Teleport 状态
    private float customTeleportX = 0f;
    private float customTeleportY = 1f;

    // S33: 动态锚点系统（Teleport Tab 动态 POI 发现）
    // [System.NonSerialized] 确保序列化隔离，Domain Reload 后自动重建
    [System.NonSerialized] private List<TeleportAnchor> cachedAnchors = null;
    [System.NonSerialized] private Vector2 anchorScrollPos;
    [System.NonSerialized] private Dictionary<string, bool> anchorCategoryFoldouts = new Dictionary<string, bool>();
    [System.NonSerialized] private bool showDynamicAnchors = true;

    // ═══════════════════════════════════════════════════
    // 菜单入口
    // ═══════════════════════════════════════════════════
    [MenuItem("MarioTrickster/Level Studio %t", false, 10)]
    public static void ShowWindow()
    {
        var window = GetWindow<TestConsoleWindow>("Level Studio");
        window.minSize = new Vector2(400, 560);
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
            cachedAnchors = null; // S33: 重置动态锚点缓存，进入 PlayMode 后重新扫描
            timeScaleValue = 1f;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            CleanupAIArena();
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
        // ── 紧凑标题栏（合并标题 + 状态指示 + Cheat 计数到一行） ──
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        Color statusColor = EditorApplication.isPlaying ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.8f, 0.2f);
        GUI.color = statusColor;
        GUILayout.Label(EditorApplication.isPlaying ? "● PLAY" : "○ EDIT", EditorStyles.boldLabel, GUILayout.Width(52));
        GUI.color = Color.white;
        GUILayout.Label("MarioTrickster Level Studio", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        if (EditorApplication.isPlaying)
        {
            int activeCount = CountActiveDebugFlags();
            if (activeCount > 0)
            {
                GUI.color = LevelStudioStyles.AccentOrange;
                GUILayout.Label($"[{activeCount} CHEATS]", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
        }
        EditorGUILayout.EndHorizontal();

        // ── Picking + Size Sync 工具栏（紧凑化，Tooltip 替代长文） ──
        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        bool isRootMode = LevelEditorPickingManager.IsRootMode;
        bool isSizeSyncEnabled = LevelEditorPickingManager.IsSizeSyncEnabled;
        GUILayout.Label("Picking:", GUILayout.Width(48));
        GUI.color = isRootMode ? LevelStudioStyles.AccentGreen : Color.white;
        if (GUILayout.Toggle(isRootMode, new GUIContent("Root", "选中 Root 物体，适合移动/旋转/批量摆放"), EditorStyles.toolbarButton, GUILayout.Width(42)) && !isRootMode)
        {
            LevelEditorPickingManager.SetMode(true);
        }
        GUI.color = !isRootMode ? LevelStudioStyles.AccentBlue : Color.white;
        if (GUILayout.Toggle(!isRootMode, new GUIContent("Visual", "选中 Visual 子物体，适合单独调外观大小"), EditorStyles.toolbarButton, GUILayout.Width(48)) && isRootMode)
        {
            LevelEditorPickingManager.SetMode(false);
        }

        GUILayout.Space(4);
        GUI.color = isSizeSyncEnabled ? LevelStudioStyles.AccentYellow : Color.white;
        if (GUILayout.Toggle(isSizeSyncEnabled, new GUIContent("Size Sync", "调 Visual 大小时自动同步碰撞体，反之亦然"), EditorStyles.toolbarButton, GUILayout.Width(64)) != isSizeSyncEnabled)
        {
            LevelEditorPickingManager.SetSizeSyncEnabled(!isSizeSyncEnabled);
        }

        GUI.color = Color.white;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();

        // ── Tab 选择 ──
        EditorGUILayout.Space(2);
        selectedTab = GUILayout.Toolbar(selectedTab, tabNames, GUILayout.Height(26));
        EditorGUILayout.Space(2);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        switch (selectedTab)
        {
            case 0: DrawLevelDesignTab(); break;
            case 1: DrawArtThemeTab(); break;
            case 2: DrawTeleportTab(); break;
            case 3: DrawCheatsTab(); break;
            case 4: DrawGameLoopTuningTab(); break;
        }

        EditorGUILayout.EndScrollView();
    }
}
