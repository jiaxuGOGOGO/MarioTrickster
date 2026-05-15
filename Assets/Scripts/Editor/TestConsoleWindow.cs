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
/// 功能概览 (S57d 升级版 — 四大选项卡，关卡设计与美术分离):
///
///   Tab 1 — Level Design (纯关卡设计 — 布局优先)
///     · Custom Template Editor (自定义模板编辑器 + 片段库 + 字典速查) — 最高频操作置顶
///     · Element Palette (动态元素调色板：点击生成到 Scene 中心)
///     · Quick Whitebox Generator (快速白盒模板生成)
///     · TestSceneBuilder (9-Stage / Validation Scene)
///     · Elements Hub (Registry Browser)
///     · Test Reports & Shortcuts
///
///   Tab 2 — Art & Theme (美术与主题 — 视觉层)
///     · Theme System (主题换肤，拖入 LevelThemeProfile，支持 Undo)
///     · Art & Effects Hub (素材导入 + SEF Shader 效果 + 合规巡检)
///
///   Tab 3 — Teleport & Reset (传送与状态管理)
///     · Stage 1~9 + GoalZone 一键传送（Mario + Trickster + Camera 硬切）
///     · Dynamic Level Anchors (动态关卡锚点)
///     · 自定义坐标传送
///     · 复活 Mario / 补满能量 / 重置关卡元素
///
///   Tab 4 — Global Cheats (全局测试外挂)
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
/// Session 26b: 精简为纯本地三合一（Custom Template Editor: 字典速查 + 5个经典片段追加 + 文本框编辑/Build）
/// Session 57d: 重构为 4-Tab 架构（Level Design / Art & Theme / Teleport / Cheats）
///             关卡设计与美术分离，Custom Template Editor 置顶，参考 LDtk/Mario Maker UX
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
    private bool showBuilderTools = true;

    // S26b: Custom Template Editor + Snippet Library 状态
    private bool showCustomTemplateEditor = true;
    private string customAsciiTemplate = "";
    private bool showSnippetLibrary = false;

    // Art & Effects Hub 状态
    private bool showArtEffectsHub = false;

    // Gameplay Mechanics 区块状态（机制驱动关卡设计）
    private bool showGameplayMechanics = true;
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

        // ── S41/S57c: Picking + Size Sync Toolbar ──
        // Root 模式(默认): 点击/框选最终只选 Root，适合移动/旋转/批量摆放
        // Visual 模式: 点击/框选最终只选 Visual，适合单独调视觉大小
        // Size Sync: 在视碰分离结构下同步 Visual.localScale ↔ Root.BoxCollider2D.size
        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        bool isRootMode = LevelEditorPickingManager.IsRootMode;
        bool isSizeSyncEnabled = LevelEditorPickingManager.IsSizeSyncEnabled;
        GUILayout.Label("Picking:", GUILayout.Width(50));
        GUI.color = isRootMode ? new Color(0.4f, 0.9f, 0.4f) : Color.white;
        if (GUILayout.Toggle(isRootMode, "Root (移动/旋转)", EditorStyles.toolbarButton) && !isRootMode)
        {
            LevelEditorPickingManager.SetMode(true);
        }
        GUI.color = !isRootMode ? new Color(0.5f, 0.8f, 1f) : Color.white;
        if (GUILayout.Toggle(!isRootMode, "Visual (调大小)", EditorStyles.toolbarButton) && isRootMode)
        {
            LevelEditorPickingManager.SetMode(false);
        }

        GUI.color = isSizeSyncEnabled ? new Color(1f, 0.85f, 0.35f) : Color.white;
        if (GUILayout.Toggle(isSizeSyncEnabled, "Size Sync (视碰同步)", EditorStyles.toolbarButton) != isSizeSyncEnabled)
        {
            LevelEditorPickingManager.SetSizeSyncEnabled(!isSizeSyncEnabled);
        }

        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(4);

        // Tab 选择
        selectedTab = GUILayout.Toolbar(selectedTab, tabNames, GUILayout.Height(28));

        EditorGUILayout.Space(4);

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
