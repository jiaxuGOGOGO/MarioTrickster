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
/// Session 26b: 精简为纯本地三合一（Custom Template Editor: 字典速查 + 5个经典片段追加 + 文本框编辑/Build）
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

    // S26b: Custom Template Editor + Snippet Library 状态
    private bool showCustomTemplateEditor = false;
    private string customAsciiTemplate = "";
    private bool showSnippetLibrary = false;

    // Art & Effects Hub 状态
    private bool showArtEffectsHub = false;

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

        // ── 区块2.5: Art & Effects Hub（素材导入 + SEF 效果统一入口）──
        showArtEffectsHub = EditorGUILayout.Foldout(showArtEffectsHub, "★ Art & Effects Hub (素材导入 + Shader效果)", true, EditorStyles.foldoutHeader);
        if (showArtEffectsHub)
        {
            DrawArtEffectsHub();
        }

        EditorGUILayout.Space(6);

        // ── 区块3: 动态元素调色板 ──
        showElementPalette = EditorGUILayout.Foldout(showElementPalette, "Element Palette (Spawn at Camera Center)", true, EditorStyles.foldoutHeader);
        if (showElementPalette)
        {
            DrawElementPalette();
        }

        EditorGUILayout.Space(6);

        // ── 区块4: 自定义模板编辑器 + 字典速查 + 片段库 (S26b) ──
        showCustomTemplateEditor = EditorGUILayout.Foldout(showCustomTemplateEditor, "☆ Custom Template Editor (自定义模板编辑器)", true, EditorStyles.foldoutHeader);
        if (showCustomTemplateEditor)
        {
            DrawCustomTemplateSection();
        }

        EditorGUILayout.Space(6);

        // ── 区块5: 原有 TestSceneBuilder 工具 ──
        showBuilderTools = EditorGUILayout.Foldout(showBuilderTools, "TestSceneBuilder (9-Stage Test Scene)", true, EditorStyles.foldoutHeader);
        if (showBuilderTools)
        {
            DrawBuilderToolsSection();
        }

        EditorGUILayout.Space(6);

        // ── 区块9: 关卡元素集控 ────
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

    /// <summary>绘制 Art & Effects Hub —— 素材导入与 Shader 效果的统一入口</summary>
    private void DrawArtEffectsHub()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.HelpBox(
            "统一入口：按顺序操作即可完成「导入素材 → 应用到场景 → 挑选效果」全流程。\n" +
            "① 新素材从零开始：用『素材导入管线』\n" +
            "② 素材穿到已有白盒物体：用『Apply Art to Selected』\n" +
            "③ 给物体加视觉效果（闪白/描边/溶解等）：用『SEF Quick Apply』\n" +
            "④ 精细调参（颜色替换/HSV/投影等）：用『效果工厂』",
            MessageType.Info);

        EditorGUILayout.Space(4);

        // 第一步：素材导入管线
        EditorGUILayout.LabelField("① 导入新素材", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(0.5f, 0.85f, 1f);
        if (GUILayout.Button("打开素材导入管线 (Ctrl+Shift+I)", GUILayout.Height(26)))
        {
            AssetImportPipeline.ShowWindow();
        }
        GUI.color = new Color(0.7f, 0.9f, 1f);
        if (GUILayout.Button("AI 智能裁切", GUILayout.Height(26)))
        {
            AI_SmartSlicerWindow.ShowWindow();
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // 第二步：应用素材到已有物体
        EditorGUILayout.LabelField("② 应用素材到已有物体", EditorStyles.boldLabel);
        GUI.color = new Color(0.5f, 1f, 0.6f);
        if (GUILayout.Button("打开 Apply Art to Selected (Ctrl+Shift+A)", GUILayout.Height(26)))
        {
            AssetApplyToSelected.ShowWindow();
        }
        GUI.color = Color.white;
        EditorGUILayout.HelpBox(
            "先在 Scene 中选中白盒物体，再点此按钮。\n" +
            "工具会自动保留已有的行为组件（碎裂/爆炸/伤害等），只替换贴图和 Material。",
            MessageType.None);

        EditorGUILayout.Space(4);

        // 第三步：SEF 效果
        EditorGUILayout.LabelField("③ 视觉效果（Shader）", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(1f, 0.85f, 0.4f);
        if (GUILayout.Button("SEF Quick Apply (Ctrl+Shift+Q)", GUILayout.Height(26)))
        {
            SEF_QuickApply.ShowWindow();
        }
        GUI.color = new Color(0.9f, 0.7f, 1f);
        if (GUILayout.Button("效果工厂精细调参 (Ctrl+Shift+E)", GUILayout.Height(26)))
        {
            SpriteEffectFactoryWindow.ShowWindow();
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(
            "Quick Apply: 10个预设一键应用（闪白、描边、溶解、冒险描边、冰冻、像素化…）\n" +
            "效果工厂: 拖入素材 → 颜色拆解 → 逐项调参 → 实时预览",
            MessageType.None);

        EditorGUILayout.Space(4);

        // 第四步：快速修复工具
        EditorGUILayout.LabelField("⚙ 快速修复", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(1f, 0.6f, 0.3f);
        if (GUILayout.Button("选中物体补 SEF Material", GUILayout.Height(24)))
        {
            FixSEFMaterialForSelection();
        }
        GUI.color = new Color(0.8f, 0.8f, 0.8f);
        if (GUILayout.Button("全工程合规巡检", GUILayout.Height(24)))
        {
            EditorApplication.ExecuteMenuItem("MarioTrickster/Art Pipeline/一键合规巡检 (校验全工程 PPU-Filter-Pivot)");
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(
            "补 SEF Material: 选中物体后点击，自动把 Sprites/Default 换成 SEF UberSprite，让效果能生效\n" +
            "合规巡检: 扫描 Assets/Art/ 下所有贴图，确保 PPU=32 / Point / Uncompressed",
            MessageType.None);

        EditorGUILayout.Space(6);

        // Picking 模式提示
        EditorGUILayout.LabelField("ℹ Picking 模式提示", EditorStyles.miniLabel);
        EditorGUILayout.HelpBox(
            "Root 模式（默认）: 点击/框选始终选中 Root，适合移动摆放\n" +
            "Visual 模式: 点击/框选始终选中 Visual 子物体，适合调视觉大小\n" +
            "Size Sync: 调 Visual 大小时自动同步碰撞体，反之亦然\n\n" +
            "★ 应用素材/效果时建议切到 Visual 模式，确保选中的是带 SpriteRenderer 的子物体。",
            MessageType.None);

        EditorGUILayout.EndVertical();
    }

    /// <summary>为当前选中物体补上 SEF Material（解决“效果不生效”的常见问题）</summary>
    private void FixSEFMaterialForSelection()
    {
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先在场景中选中物体", "好的");
            return;
        }

        int fixedCount = 0;
        foreach (var go in selected)
        {
            SpriteRenderer[] renderers = go.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in renderers)
            {
                if (sr.sharedMaterial != null && sr.sharedMaterial.shader != null
                    && sr.sharedMaterial.shader.name == "MarioTrickster/SEF/UberSprite")
                    continue;

                var shader = Shader.Find("MarioTrickster/SEF/UberSprite");
                if (shader == null)
                {
                    Debug.LogWarning("[Art Hub] SEF UberSprite shader not found!");
                    return;
                }

                Undo.RecordObject(sr, "Fix SEF Material");
                Material mat = new Material(shader);
                mat.name = $"SEF_{sr.gameObject.name}";
                if (sr.sprite != null)
                    mat.mainTexture = sr.sprite.texture;
                sr.sharedMaterial = mat;
                fixedCount++;

                // 确保有 SpriteEffectController
                if (sr.gameObject.GetComponent<SpriteEffectController>() == null)
                {
                    Undo.AddComponent<SpriteEffectController>(sr.gameObject);
                }
            }
        }

        if (fixedCount > 0)
        {
            SceneView.RepaintAll();
            Debug.Log($"[Art Hub] 已为 {fixedCount} 个 SpriteRenderer 补上 SEF Material");
        }
        else
        {
            Debug.Log("[Art Hub] 所有选中物体已经使用 SEF Material，无需修复");
        }
    }

    /// <summary>为关卡中所有有 Sprite 的物体补上 SEF Material（换肤后自动调用）</summary>
    private void EnsureSEFMaterialForLevel(GameObject root)
    {
        if (root == null) return;
        var shader = Shader.Find("MarioTrickster/SEF/UberSprite");
        if (shader == null) return;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>();
        int count = 0;
        foreach (var sr in renderers)
        {
            // 跳过已经使用 SEF Material 的
            if (sr.sharedMaterial != null && sr.sharedMaterial.shader != null
                && sr.sharedMaterial.shader.name == "MarioTrickster/SEF/UberSprite")
                continue;

            // 跳过没有贴图的（白盒状态）
            if (sr.sprite == null) continue;
            // 跳过还是白盒 Sprite 的（未被换肤）
            if (sr.sprite.texture != null && sr.sprite.texture.width == 4 && sr.sprite.texture.height == 4)
                continue;

            Material mat = new Material(shader);
            mat.name = $"SEF_{sr.gameObject.name}";
            mat.mainTexture = sr.sprite.texture;
            sr.sharedMaterial = mat;

            // 确保有 SpriteEffectController
            if (sr.gameObject.GetComponent<SpriteEffectController>() == null)
            {
                sr.gameObject.AddComponent<SpriteEffectController>();
            }
            count++;
        }
        if (count > 0)
            Debug.Log($"[TestConsole] 换肤后自动为 {count} 个物体补上 SEF Material");
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

            // S33: 统一调用链 — 与 GenerateFromCustomTemplate 保持一致，
            // 自动补全 Mario/Trickster/Managers/Camera/KillZone，让生成的关卡直接可 Play。
            // 此方法是幂等的：如果场景中已有这些对象则跳过创建。
            EnsurePlayableEnvironment(root);

            // 聚焦到生成的关卡
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[TestConsole] Whitebox level '{templateName}' generated with playable environment.");
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

        // 换肤后自动为所有替换了 Sprite 的物体补上 SEF Material，
        // 确保后续 SEF Quick Apply 效果能直接生效，用户无需手动补 Material。
        EnsureSEFMaterialForLevel(root);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[TestConsole] Theme '{themeProfile.themeName}' applied with Undo support + SEF Material.");
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
        DrawPaletteButton("SawBlade", '@', new Color(0.7f, 0.7f, 0.7f));
        EditorGUILayout.EndHorizontal();

        // 平台类
        EditorGUILayout.LabelField("Platforms", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Bouncy", 'B', new Color(0.3f, 0.85f, 0.3f));
        DrawPaletteButton("Collapse", 'C', new Color(0.8f, 0.65f, 0.3f));
        DrawPaletteButton("OneWay", '-', new Color(0.5f, 0.75f, 0.9f));
        DrawPaletteButton("Moving", '>', new Color(0.5f, 0.5f, 0.9f));
        DrawPaletteButton("Conveyor", '<', new Color(0.6f, 0.6f, 0.4f));
        EditorGUILayout.EndHorizontal();

        // 敌人类
        EditorGUILayout.LabelField("Enemies", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Bounce Enemy", 'E', new Color(0.9f, 0.2f, 0.6f));
        DrawPaletteButton("Simple Enemy", 'e', new Color(0.9f, 0.2f, 0.6f));
        DrawPaletteButton("Flying Enemy", 'f', new Color(0.85f, 0.4f, 0.85f));
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
        DrawPaletteButton("Breakable", 'X', new Color(0.75f, 0.55f, 0.3f));
        EditorGUILayout.EndHorizontal();

        // 其他
        EditorGUILayout.LabelField("Other", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Collectible", 'o', new Color(1f, 0.85f, 0.2f));
        DrawPaletteButton("Goal Zone", 'G', new Color(0.2f, 1f, 0.4f));
        DrawPaletteButton("Checkpoint", 'S', new Color(0.2f, 0.8f, 0.9f));
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

        // ═══════════════════════════════════════════════════
        // S33: 动态锚点系统 — 自动扫描场景中的兴趣点 (POI)
        // 设计理念（参考 Celeste Debug Map）：
        //   - 不硬编码任何坐标，通过“场景自省”动态发现传送目标
        //   - 优先从 LevelElementRegistry 查询（已有 Fake Null 防御）
        //   - 补充扫描 SpawnPoint、GoalZone 等非 Registry 对象
        //   - 白名单过滤：仅保留有调试价值的 POI，剔除纯静态地形噪声
        //   - 危险对象自动叠加安全传送偏移量
        // ═══════════════════════════════════════════════════
        showDynamicAnchors = EditorGUILayout.Foldout(showDynamicAnchors,
            "Dynamic Level Anchors (动态关卡锚点)", true, EditorStyles.foldoutHeader);
        if (showDynamicAnchors)
        {
            DrawDynamicAnchorsSection();
        }

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

        // S33: 缺失组件检测 — 当 Cheats 依赖的核心组件不存在时，
        // 显示警告 + 置灰无效开关 + 提供一键修复按钮。
        // 参考审计意见第 4 点：“消极警告”升级为“一键修复”与视觉阻断。
        bool cheatsAvailable = (cachedMarioHealth != null && cachedGameManager != null &&
                                cachedEnergy != null && cachedDisguise != null);

        if (EditorApplication.isPlaying && !cheatsAvailable)
        {
            EditorGUILayout.HelpBox(
                "⚠️ 场景中缺少 Cheats 依赖的核心组件：\n" +
                (cachedMarioHealth == null ? "  · MarioController / PlayerHealth\n" : "") +
                (cachedGameManager == null ? "  · GameManager\n" : "") +
                (cachedEnergy == null ? "  · EnergySystem (Trickster)\n" : "") +
                (cachedDisguise == null ? "  · DisguiseSystem (Trickster)\n" : "") +
                "\n请先通过 Level Builder 生成关卡，或点击下方按钮自动补全环境。",
                MessageType.Warning);

            // 一键修复按钮 — EnsurePlayableEnvironment 是幂等的，绝对安全
            GameObject asciiRoot = GameObject.Find("AsciiLevel_Root");
            if (asciiRoot != null)
            {
                GUI.color = new Color(0.3f, 0.9f, 0.5f);
                if (GUILayout.Button("Auto-Fix: Inject Playable Environment", GUILayout.Height(28)))
                {
                    EnsurePlayableEnvironment(asciiRoot);
                    ClearCache();
                    EnsureCache(); // 重新获取缓存，激活置灰的 Toggle
                    cachedAnchors = null; // 刷新动态锚点
                    Debug.Log("[TestConsole] Auto-Fix: Playable environment injected for Cheats.");
                }
                GUI.color = Color.white;
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "未找到 AsciiLevel_Root，请先在 Level Builder Tab 生成关卡。",
                    MessageType.Error);
            }
        }

        // S33: 视觉阻断 — 缺少组件时置灰所有 Cheat Toggle，杜绝无效点击
        EditorGUI.BeginDisabledGroup(!cheatsAvailable && EditorApplication.isPlaying);

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

        EditorGUI.EndDisabledGroup(); // S33: cheatsAvailable 置灰组结束

        EditorGUI.EndDisabledGroup(); // 原有的 !isPlaying 置灰组结束

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
            // S37: 视碰分离 — SpriteRenderer 可能在子物体 Visual 上
            SpriteRenderer sr = cachedMario.GetComponentInChildren<SpriteRenderer>();
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

    // ═════════════════════════════════════════════════
    // S26b: Custom Template Editor (三合一: 字典速查 + 片段库追加 + 文本框编辑)
    // ═════════════════════════════════════════════════

    /// <summary>绘制自定义模板编辑器（三合一：字典速查 + 片段库追加 + 文本框 + Build）</summary>
    private void DrawCustomTemplateSection()
    {
        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.HelpBox(
            "粘贴或编写 ASCII 模板，一键生成关卡。\n" +
            "可从外部 AI 聊天框复制模板粘贴进来，也可点击下方片段按钮追加拼装。",
            MessageType.Info);

        // ── 字典速查表 ──
        EditorGUILayout.BeginVertical("box");
        GUIStyle refStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, richText = true };
        EditorGUILayout.LabelField("字符映射速查表:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "<b>#</b>=地面  <b>=</b>=平台  <b>W</b>=墙壁  <b>.</b>=空气  <b>M</b>=Mario  <b>T</b>=Trickster  <b>G</b>=终点\n" +
            "<b>^</b>=地刺  <b>~</b>=火焰  <b>P</b>=摆锤  <b>B</b>=弹跳平台  <b>C</b>=崩塔平台  <b>-</b>=单向平台\n" +
            "<b>E</b>=弹跳怪  <b>e</b>=巡逻怪  <b>></b>=移动平台  <b>F</b>=伪装墙  <b>H</b>=隐藏通道  <b>o</b>=金币\n" +
            "<b>@</b>=锯片  <b>f</b>=飞行敌人  <b><</b>=传送带  <b>S</b>=检查点  <b>X</b>=可破坏方块",
            refStyle);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // ── 片段库 (Snippet Library) ──
        showSnippetLibrary = EditorGUILayout.Foldout(showSnippetLibrary, "经典片段库 (点击追加到下方文本框)", true);
        if (showSnippetLibrary)
        {
            EditorGUILayout.BeginVertical("box");
            var allSnippets = LevelSnippetLibrary.GetAllSnippets();
            foreach (var snippet in allSnippets)
            {
                EditorGUILayout.BeginVertical("box");
                // 标题 + 尺寸
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(snippet.name, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                GUILayout.Label($"{snippet.width}×{snippet.height}", EditorStyles.miniLabel, GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();

                // 说明
                EditorGUILayout.LabelField(snippet.description, EditorStyles.wordWrappedMiniLabel);

                // 操作按钮
                EditorGUILayout.BeginHorizontal();
                GUI.color = new Color(1f, 0.85f, 0.3f);
                if (GUILayout.Button("追加到文本框", GUILayout.Height(22)))
                {
                    // 追加到文本框（如果已有内容，用空行分隔）
                    if (!string.IsNullOrEmpty(customAsciiTemplate))
                        customAsciiTemplate += "\n\n";
                    customAsciiTemplate += snippet.ascii;
                    Debug.Log($"[TestConsole] Snippet '{snippet.name}' appended to template editor.");
                }
                GUI.color = new Color(0.4f, 0.9f, 0.4f);
                if (GUILayout.Button("直接生成", GUILayout.Height(22)))
                {
                    // S43: 片段直接生成时传递 isSnippet=true，避免验证器误报缺少 M/G
                    GenerateFromCustomTemplate(snippet.ascii, snippet.name, true);
                }
                GUI.color = new Color(0.5f, 0.8f, 1f);
                if (GUILayout.Button("复制", GUILayout.Height(22), GUILayout.Width(45)))
                {
                    EditorGUIUtility.systemCopyBuffer = snippet.ascii;
                    Debug.Log($"[TestConsole] Snippet '{snippet.name}' copied to clipboard.");
                }
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(2);
            }
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(6);

        // ── 模板编辑文本框 ──
        EditorGUILayout.LabelField("模板内容 (每行一层，第一行=最高层):", EditorStyles.boldLabel);
        GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea)
        {
            font = Font.CreateDynamicFontFromOSFont("Courier New", 12),
            fontSize = 12,
            wordWrap = false
        };
        customAsciiTemplate = EditorGUILayout.TextArea(customAsciiTemplate, textAreaStyle,
            GUILayout.MinHeight(150), GUILayout.MaxHeight(400));

        // 统计信息
        if (!string.IsNullOrEmpty(customAsciiTemplate))
        {
            string[] lines = customAsciiTemplate.Split('\n');
            int maxWidth = 0;
            foreach (string line in lines)
                if (line.Length > maxWidth) maxWidth = line.Length;
            EditorGUILayout.LabelField($"尺寸: {maxWidth} × {lines.Length} 格", EditorStyles.miniLabel);
        }

        // ── 操作按钮行 ──
        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(0.4f, 0.9f, 0.4f);
        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(customAsciiTemplate));
        if (GUILayout.Button("Build From Text (生成关卡)", GUILayout.Height(30)))
        {
            GenerateFromCustomTemplate(customAsciiTemplate, "CustomTemplate");
        }
        EditorGUI.EndDisabledGroup();
        GUI.color = Color.white;

        if (GUILayout.Button("从剪贴板粘贴", GUILayout.Height(30)))
        {
            customAsciiTemplate = EditorGUIUtility.systemCopyBuffer;
            Debug.Log("[TestConsole] Template pasted from clipboard.");
        }

        if (GUILayout.Button("清空", GUILayout.Height(30), GUILayout.Width(50)))
        {
            customAsciiTemplate = "";
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUI.EndDisabledGroup();
    }

    // ═════════════════════════════════════════════════
    // S26b: 通用模板生成方法
    // ═════════════════════════════════════════════════

    /// <summary>从自定义 ASCII 模板生成关卡</summary>
    /// <param name="template">ASCII 模板字符串</param>
    /// <param name="sourceName">模板来源名称（用于日志）</param>
    /// <param name="isSnippet">是否为片段模式（片段不要求 M/G）</param>
    private void GenerateFromCustomTemplate(string template, string sourceName, bool isSnippet = false)
    {
        if (string.IsNullOrEmpty(template))
        {
            Debug.LogWarning("[TestConsole] Template is empty!");
            return;
        }

        Undo.SetCurrentGroupName($"Generate Level: {sourceName}");

        // S43: 传递 isSnippet 参数给生成器，片段模式下验证器不要求 M/G
        GameObject root = AsciiLevelGenerator.GenerateFromTemplate(template, true, isSnippet);
        if (root != null)
        {
            Undo.RegisterCreatedObjectUndo(root, $"Generate {sourceName}");

            // S31: 自动创建可玩环境 — 让 ASCII 关卡生成后直接可 Play
            EnsurePlayableEnvironment(root);

            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[TestConsole] Level '{sourceName}' generated with playable environment.");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // S31: 自动创建可玩环境
    // 当 ASCII 模板生成关卡后，自动补全 Mario / Trickster / Managers / Camera / KillZone，
    // 使关卡可以直接按 Play 运行，与 Build Test Scene 体验一致。
    // 如果场景中已有这些对象（例如在 TestScene 中追加模板），则跳过创建，避免重复。
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 确保场景中存在完整的可玩环境。
    /// 检测 Mario / Trickster / Managers / Camera / KillZone，缺什么补什么。
    /// </summary>
    private void EnsurePlayableEnvironment(GameObject asciiRoot)
    {
        // --- 查找 ASCII 关卡中的 SpawnPoint ---
        Transform marioSpawnT = null;
        Transform tricksterSpawnT = null;
        float levelWidth = 0f;
        float levelHeight = 0f;

        foreach (Transform child in asciiRoot.transform)
        {
            if (child.name.StartsWith("MarioSpawnPoint"))
                marioSpawnT = child;
            else if (child.name.StartsWith("TricksterSpawnPoint"))
                tricksterSpawnT = child;

            // 计算关卡边界
            float x = child.position.x;
            float y = child.position.y;
            if (x > levelWidth) levelWidth = x;
            if (y > levelHeight) levelHeight = y;
        }

        // 默认出生位置（如果模板中没有 M/T 字符）
        Vector3 marioSpawnPos = marioSpawnT != null ? marioSpawnT.position : new Vector3(2f, 2f, 0f);
        Vector3 tricksterSpawnPos = tricksterSpawnT != null ? tricksterSpawnT.position : marioSpawnPos + new Vector3(1f, 0f, 0f);

        // 关卡边界（留余量）
        float boundMinX = -3f;
        float boundMaxX = levelWidth + 5f;
        float boundMinY = -10f;
        float boundMaxY = levelHeight + 10f;

        // --- 确保 Ground Layer 存在 ---
        int groundLayerIndex = LayerMask.NameToLayer("Ground");
        if (groundLayerIndex == -1) groundLayerIndex = 0;
        LayerMask groundLayerMask = 1 << groundLayerIndex;

        // --- 确保 Player / Trickster Layer 存在（B028 兼容）---
        int playerLayerIndex = EnsureLayerForPlayable("Player");
        int tricksterLayerIndex = EnsureLayerForPlayable("Trickster");
        Physics2D.IgnoreLayerCollision(playerLayerIndex, tricksterLayerIndex, true);

        // ═══════════════════════════════════════════════════
        // Mario
        // ═══════════════════════════════════════════════════
        MarioController marioCtrl = Object.FindObjectOfType<MarioController>();
        PlayerHealth marioHealth = null;
        GameObject mario;

        if (marioCtrl == null)
        {
            mario = new GameObject("Mario");
            mario.tag = "Player";
            mario.layer = playerLayerIndex;
            mario.transform.position = marioSpawnPos + Vector3.up * 0.5f;

            // S37: 视碰分离 — 创建 Visual 子节点承载 SpriteRenderer
            GameObject marioVisual = new GameObject("Visual");
            marioVisual.transform.SetParent(mario.transform, false);
            marioVisual.transform.localPosition = Vector3.zero;

            SpriteRenderer marioSR = marioVisual.AddComponent<SpriteRenderer>();
            marioSR.color = new Color(0.9f, 0.2f, 0.2f);
            marioSR.sortingOrder = 10;
            AssignDefaultSpriteForPlayable(marioSR, marioSR.color);

            BoxCollider2D marioCol = mario.AddComponent<BoxCollider2D>();
            marioCol.size = new Vector2(PhysicsMetrics.MARIO_COLLIDER_WIDTH, PhysicsMetrics.MARIO_COLLIDER_HEIGHT);
            marioCol.offset = new Vector2(0f, PhysicsMetrics.MARIO_COLLIDER_OFFSET_Y);

            Rigidbody2D marioRb = mario.AddComponent<Rigidbody2D>();
            marioRb.gravityScale = 0f; // MarioController 自行管理重力
            marioRb.freezeRotation = true;
            marioRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            marioCtrl = mario.AddComponent<MarioController>();
            marioCtrl.visualTransform = marioVisual.transform; // S37: 赋值视觉代理节点
            marioHealth = mario.AddComponent<PlayerHealth>();
            mario.AddComponent<ScanAbility>();

            SetSerializedFieldForPlayable(marioCtrl, "groundLayer", groundLayerMask);

            // Session 32: 自动挂载跳跃抛物线可视化工具
            mario.AddComponent<JumpArcVisualizer>();

            Undo.RegisterCreatedObjectUndo(mario, "Create Mario");
            Debug.Log("[TestConsole] Created Mario at " + mario.transform.position);
        }
        else
        {
            mario = marioCtrl.gameObject;
            marioHealth = mario.GetComponent<PlayerHealth>();
            // 将已有 Mario 传送到新关卡的出生点
            mario.transform.position = marioSpawnPos + Vector3.up * 0.5f;
            // 修复已有 Mario 的 groundLayer 未设置问题（消除黄色警告）
            SetSerializedFieldForPlayable(marioCtrl, "groundLayer", groundLayerMask);
        }

        // ═════════════════════════════════════════════════
        // Trickster
        // ═══════════════════════════════════════════════════
        TricksterController tricksterCtrl = Object.FindObjectOfType<TricksterController>();
        GameObject trickster;

        if (tricksterCtrl == null)
        {
            trickster = new GameObject("Trickster");
            trickster.layer = tricksterLayerIndex;
            trickster.transform.position = tricksterSpawnPos + Vector3.up * 0.5f;

            // S37: 视碰分离 — 创建 Visual 子节点承载 SpriteRenderer
            GameObject tricksterVisual = new GameObject("Visual");
            tricksterVisual.transform.SetParent(trickster.transform, false);
            tricksterVisual.transform.localPosition = Vector3.zero;

            SpriteRenderer tricksterSR = tricksterVisual.AddComponent<SpriteRenderer>();
            tricksterSR.color = new Color(0.2f, 0.4f, 0.9f);
            tricksterSR.sortingOrder = 10;
            AssignDefaultSpriteForPlayable(tricksterSR, tricksterSR.color);

            BoxCollider2D tricksterCol = trickster.AddComponent<BoxCollider2D>();
            tricksterCol.size = new Vector2(PhysicsMetrics.TRICKSTER_COLLIDER_WIDTH, PhysicsMetrics.TRICKSTER_COLLIDER_HEIGHT);
            tricksterCol.offset = new Vector2(0f, PhysicsMetrics.TRICKSTER_COLLIDER_OFFSET_Y);

            Rigidbody2D tricksterRb = trickster.AddComponent<Rigidbody2D>();
            tricksterRb.gravityScale = 0f;
            tricksterRb.freezeRotation = true;
            tricksterRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            tricksterCtrl = trickster.AddComponent<TricksterController>();
            tricksterCtrl.visualTransform = tricksterVisual.transform; // S37: 赋值视觉代理节点
            trickster.AddComponent<DisguiseSystem>();
            trickster.AddComponent<TricksterAbilitySystem>();
            trickster.AddComponent<EnergySystem>();

            SetSerializedFieldForPlayable(tricksterCtrl, "groundLayer", groundLayerMask);

            Undo.RegisterCreatedObjectUndo(trickster, "Create Trickster");
            Debug.Log("[TestConsole] Created Trickster at " + trickster.transform.position);
        }
        else
        {
            trickster = tricksterCtrl.gameObject;
            trickster.transform.position = tricksterSpawnPos + Vector3.up * 0.5f;
            // 修复已有 Trickster 的 groundLayer 未设置问题（消除黄色警告）
            SetSerializedFieldForPlayable(tricksterCtrl, "groundLayer", groundLayerMask);
        }

        // ═══════════════════════════════════════════════════
        // Managers (GameManager + InputManager + LevelManager)
        // ═══════════════════════════════════════════════════
        GameManager gameManager = Object.FindObjectOfType<GameManager>();

        if (gameManager == null)
        {
            GameObject managers = new GameObject("Managers");

            gameManager = managers.AddComponent<GameManager>();
            InputManager inputManager = managers.AddComponent<InputManager>();
            LevelManager levelManager = managers.AddComponent<LevelManager>();

            // 连线 InputManager
            SetSerializedFieldForPlayable(inputManager, "marioController", marioCtrl);
            SetSerializedFieldForPlayable(inputManager, "tricksterController", tricksterCtrl);

            // 连线 GameManager
            SetSerializedFieldForPlayable(gameManager, "mario", marioCtrl);
            SetSerializedFieldForPlayable(gameManager, "trickster", tricksterCtrl);
            if (marioHealth != null)
                SetSerializedFieldForPlayable(gameManager, "marioHealth", marioHealth);
            SetSerializedFieldForPlayable(gameManager, "inputManager", inputManager);

            // SpawnPoint（使用 ASCII 模板中的位置或默认位置）
            GameObject marioSP = marioSpawnT != null ? marioSpawnT.gameObject : new GameObject("MarioSpawnPoint");
            GameObject tricksterSP = tricksterSpawnT != null ? tricksterSpawnT.gameObject : new GameObject("TricksterSpawnPoint");
            if (marioSpawnT == null)
            {
                marioSP.transform.position = marioSpawnPos;
                marioSP.transform.parent = managers.transform;
            }
            if (tricksterSpawnT == null)
            {
                tricksterSP.transform.position = tricksterSpawnPos;
                tricksterSP.transform.parent = managers.transform;
            }

            SetSerializedFieldForPlayable(gameManager, "marioSpawnPoint", marioSP.transform);
            SetSerializedFieldForPlayable(gameManager, "tricksterSpawnPoint", tricksterSP.transform);
            SetSerializedFieldForPlayable(levelManager, "marioSpawnPoint", marioSP.transform);
            SetSerializedFieldForPlayable(levelManager, "tricksterSpawnPoint", tricksterSP.transform);

            // 关卡边界
            SetSerializedFieldForPlayable(levelManager, "levelMinX", boundMinX);
            SetSerializedFieldForPlayable(levelManager, "levelMaxX", boundMaxX);
            SetSerializedFieldForPlayable(levelManager, "levelMinY", boundMinY);
            SetSerializedFieldForPlayable(levelManager, "levelMaxY", boundMaxY);

            // GameUI
            GameObject uiObject = new GameObject("GameUI");
            uiObject.transform.parent = managers.transform;
            uiObject.AddComponent<GameUI>();

            Undo.RegisterCreatedObjectUndo(managers, "Create Managers");
            Debug.Log("[TestConsole] Created Managers (GameManager + InputManager + LevelManager + GameUI)");
        }

        // ═══════════════════════════════════════════════════
        // Camera
        // ═══════════════════════════════════════════════════
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            CameraController camCtrl = mainCam.GetComponent<CameraController>();
            if (camCtrl == null)
            {
                camCtrl = mainCam.gameObject.AddComponent<CameraController>();
                Undo.RegisterCreatedObjectUndo(camCtrl, "Add CameraController");
            }

            SetSerializedFieldForPlayable(camCtrl, "target", mario.transform);
            SetSerializedFieldForPlayable(camCtrl, "useBounds", true);
            SetSerializedFieldForPlayable(camCtrl, "minX", boundMinX);
            SetSerializedFieldForPlayable(camCtrl, "maxX", boundMaxX);
            SetSerializedFieldForPlayable(camCtrl, "minY", boundMinY - 5f);
            SetSerializedFieldForPlayable(camCtrl, "maxY", boundMaxY);

            // 将相机移到 Mario 位置
            mainCam.transform.position = new Vector3(marioSpawnPos.x, marioSpawnPos.y + 2f, -10f);
            mainCam.orthographicSize = 7;
        }

        // ═══════════════════════════════════════════════════
        // KillZone（底部死亡区域）
        // ═══════════════════════════════════════════════════
        KillZone existingKillZone = Object.FindObjectOfType<KillZone>();
        if (existingKillZone == null)
        {
            GameObject killZone = new GameObject("KillZone");
            killZone.transform.position = new Vector3(levelWidth / 2f, -8f, 0);
            BoxCollider2D killCol = killZone.AddComponent<BoxCollider2D>();
            killCol.size = new Vector2(levelWidth + 30f, 2f);
            killCol.isTrigger = true;
            KillZone kz = killZone.AddComponent<KillZone>();
            kz.SetFallbackY(-13f); // S48b: Y 坐标兜底阈值（KillZone 在 y=-8，再留 5 格余量）

            Undo.RegisterCreatedObjectUndo(killZone, "Create KillZone");
            Debug.Log("[TestConsole] Created KillZone below level");
        }

        Debug.Log($"[TestConsole] ✅ Playable environment ready! Mario at {mario.transform.position}, bounds: X[{boundMinX},{boundMaxX}] Y[{boundMinY},{boundMaxY}]");

        // S41: 补全环境后同步 Picking 状态（Mario/Trickster 的 Visual 子节点在此方法中创建）
        LevelEditorPickingManager.SyncState();
    }

    // ═══════════════════════════════════════════════════
    // EnsurePlayableEnvironment 辅助方法
    // ═══════════════════════════════════════════════════

    /// <summary>确保 Layer 存在，不存在则创建</summary>
    private int EnsureLayerForPlayable(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing != -1) return existing;

        SerializedObject tagManager = new SerializedObject(
            UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < 32; i++)
        {
            SerializedProperty layerProp = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layerProp.stringValue))
            {
                layerProp.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[TestConsole] Created Layer: {layerName} (index: {i})");
                return i;
            }
        }

        Debug.LogError($"[TestConsole] Cannot create Layer '{layerName}': all custom layer slots are full!");
        return 0;
    }

    /// <summary>为可玩环境对象分配默认白盒 Sprite</summary>
    private void AssignDefaultSpriteForPlayable(SpriteRenderer sr, Color color)
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;

        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        sr.color = color;
    }

    /// <summary>通过 SerializedObject 设置字段值（与 TestSceneBuilder 同逻辑）</summary>
    private void SetSerializedFieldForPlayable(Object target, string fieldName, object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);

        if (prop == null)
        {
            Debug.LogWarning($"[TestConsole] Field not found: {target.GetType().Name}.{fieldName}");
            return;
        }

        switch (prop.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                prop.objectReferenceValue = value as Object;
                break;
            case SerializedPropertyType.Float:
                prop.floatValue = (float)value;
                break;
            case SerializedPropertyType.Integer:
                prop.intValue = (int)value;
                break;
            case SerializedPropertyType.Boolean:
                prop.boolValue = (bool)value;
                break;
            case SerializedPropertyType.String:
                prop.stringValue = (string)value;
                break;
            case SerializedPropertyType.Vector3:
                prop.vector3Value = (Vector3)value;
                break;
            case SerializedPropertyType.Vector2:
                prop.vector2Value = (Vector2)value;
                break;
            case SerializedPropertyType.LayerMask:
                prop.intValue = (int)(LayerMask)value;
                break;
            case SerializedPropertyType.Enum:
                prop.enumValueIndex = (int)value;
                break;
            default:
                Debug.LogWarning($"[TestConsole] Unsupported property type: {prop.propertyType} ({fieldName})");
                break;
        }

        so.ApplyModifiedProperties();
    }

    // ═════════════════════════════════════════════════════════
    // S33: 动态锚点系统 (Dynamic Teleport Anchors)
    //
    // 设计背景：
    //   Level Builder 生成的 ASCII 关卡布局不固定，无法像 TestSceneBuilder 的
    //   9-Stage 那样硬编码传送坐标。参考 Celeste Debug Map 的“场景自省”理念，
    //   通过运行时扫描自动发现关卡中的兴趣点 (POI)，动态生成传送按钮。
    //
    // 核心原则：
    //   1. 优先从 LevelElementRegistry 查询（已有 Fake Null 防御）
    //   2. 补充扫描 SpawnPoint、GoalZone 等非 Registry 对象
    //   3. 白名单过滤：仅保留有调试价值的 POI，剔除纯静态地形噪声
    //   4. 危险对象自动叠加安全传送偏移量 (Vector3.up * 2f)
    //   5. [System.NonSerialized] 缓存 + 懒加载，Domain Reload 安全
    //   6. Fake Null 防御：遍历时跳过已销毁对象
    //   7. ScrollView 限制最大高度，防止大量元素撑爆窗口
    // ═════════════════════════════════════════════════════════

    /// <summary>动态传送锚点数据结构</summary>
    private struct TeleportAnchor
    {
        public string Name;           // 显示名称
        public string Category;       // 分组名称
        public Vector3 RawPosition;   // 原始坐标
        public Vector3 SafePosition;  // 安全传送坐标（危险对象已叠加偏移）
        public bool IsDangerous;      // 是否为危险对象
        public Color ButtonColor;     // 按钮颜色
        public Object SourceObject;   // 源对象引用（用于 Fake Null 检测）
    }

    // ─────────────────────────────────────────────────────────
    // POI 白名单：仅保留有调试价值的分类
    // 剔除 Platform 和 Misc — 这些多为纯静态地形，传送过去没有调试意义
    // ─────────────────────────────────────────────────────────
    private static readonly HashSet<ElementCategory> POI_CATEGORIES = new HashSet<ElementCategory>
    {
        ElementCategory.Trap,
        ElementCategory.Enemy,
        ElementCategory.Hazard,
        ElementCategory.HiddenPassage,
        ElementCategory.Collectible,
        ElementCategory.Checkpoint
    };

    /// <summary>刷新动态锚点缓存</summary>
    private void RefreshTeleportAnchors()
    {
        cachedAnchors = new List<TeleportAnchor>();

        // ── 源 1: LevelElementRegistry 查询（白名单过滤） ──
        foreach (var rec in LevelElementRegistry.GetAll())
        {
            // Fake Null 防御：跳过已销毁的对象
            if (rec.Component == null || rec.Transform == null) continue;

            // 白名单过滤：仅保留 POI 分类
            if (!POI_CATEGORIES.Contains(rec.Category)) continue;

            bool isDangerous = (rec.Category == ElementCategory.Trap ||
                                rec.Category == ElementCategory.Enemy ||
                                rec.Category == ElementCategory.Hazard);

            Vector3 rawPos = rec.Transform.position;
            // 安全传送偏移：危险对象在目标上方 2 个单位，避免落地瞬间触发受击
            Vector3 safePos = isDangerous ? rawPos + Vector3.up * 2f : rawPos + Vector3.up * 0.5f;

            Color btnColor;
            switch (rec.Category)
            {
                case ElementCategory.Trap:           btnColor = new Color(1f, 0.4f, 0.4f); break;
                case ElementCategory.Enemy:          btnColor = new Color(1f, 0.5f, 0.3f); break;
                case ElementCategory.Hazard:         btnColor = new Color(1f, 0.3f, 0.5f); break;
                case ElementCategory.HiddenPassage:  btnColor = new Color(0.6f, 0.4f, 1f); break;
                case ElementCategory.Collectible:    btnColor = new Color(1f, 0.9f, 0.3f); break;
                case ElementCategory.Checkpoint:     btnColor = new Color(0.3f, 1f, 0.5f); break;
                default:                             btnColor = Color.white; break;
            }

            cachedAnchors.Add(new TeleportAnchor
            {
                Name = rec.Name,
                Category = rec.Category.ToString(),
                RawPosition = rawPos,
                SafePosition = safePos,
                IsDangerous = isDangerous,
                ButtonColor = btnColor,
                SourceObject = rec.Component
            });
        }

        // ── 源 2: SpawnPoint 标记（非 Registry 对象） ──
        GameObject asciiRoot = GameObject.Find("AsciiLevel_Root");
        if (asciiRoot != null)
        {
            foreach (Transform child in asciiRoot.transform)
            {
                if (child == null) continue;
                if (child.name.StartsWith("MarioSpawnPoint"))
                {
                    cachedAnchors.Add(new TeleportAnchor
                    {
                        Name = "Mario Spawn",
                        Category = "Spawn",
                        RawPosition = child.position,
                        SafePosition = child.position + Vector3.up * 0.5f,
                        IsDangerous = false,
                        ButtonColor = new Color(0.2f, 0.8f, 0.2f),
                        SourceObject = child.gameObject
                    });
                }
                else if (child.name.StartsWith("TricksterSpawnPoint"))
                {
                    cachedAnchors.Add(new TeleportAnchor
                    {
                        Name = "Trickster Spawn",
                        Category = "Spawn",
                        RawPosition = child.position,
                        SafePosition = child.position + Vector3.up * 0.5f,
                        IsDangerous = false,
                        ButtonColor = new Color(0.3f, 0.7f, 1f),
                        SourceObject = child.gameObject
                    });
                }
            }
        }

        // ── 源 3: GoalZone（非 Registry 对象） ──
        GoalZone[] goalZones = Object.FindObjectsOfType<GoalZone>();
        foreach (GoalZone gz in goalZones)
        {
            if (gz == null) continue;
            cachedAnchors.Add(new TeleportAnchor
            {
                Name = "GoalZone",
                Category = "Goal",
                RawPosition = gz.transform.position,
                SafePosition = gz.transform.position + Vector3.left * 2f + Vector3.up * 0.5f,
                IsDangerous = false,
                ButtonColor = new Color(0.5f, 1f, 0.5f),
                SourceObject = gz
            });
        }

        // 按分类名称 + X 坐标排序，保证 UI 稳定
        cachedAnchors.Sort((a, b) =>
        {
            int catCmp = string.Compare(a.Category, b.Category, System.StringComparison.Ordinal);
            return catCmp != 0 ? catCmp : a.RawPosition.x.CompareTo(b.RawPosition.x);
        });

        Debug.Log($"[TestConsole] Dynamic anchors refreshed: {cachedAnchors.Count} POIs found.");
    }

    /// <summary>绘制动态锚点区域 UI</summary>
    private void DrawDynamicAnchorsSection()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "动态锚点仅在 PlayMode 下可用。\n进入 PlayMode 后自动扫描场景中的兴趣点。",
                MessageType.Info);
            return;
        }

        // S33: 懒加载/状态校验拦截（审计意见第 5 点）
        // Domain Reload 后 [System.NonSerialized] 字段会被清空，
        // 在绘制入口做懒加载检查，确保从编辑态进入运行态时自动完成首次扫描。
        if (cachedAnchors == null || cachedAnchors.Count == 0)
        {
            RefreshTeleportAnchors();
        }

        // 手动刷新按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Anchors", GUILayout.Height(22)))
        {
            RefreshTeleportAnchors();
        }
        EditorGUILayout.LabelField($"{(cachedAnchors != null ? cachedAnchors.Count : 0)} POIs",
            EditorStyles.miniLabel, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        if (cachedAnchors == null || cachedAnchors.Count == 0)
        {
            EditorGUILayout.HelpBox("场景中未发现可传送的兴趣点。", MessageType.Info);
            return;
        }

        // S33: ScrollView 限制最大高度（审计意见第 2 点）
        // 防止大量元素撑爆窗口，底部的 Custom Teleport 和 Quick Actions 始终可达。
        anchorScrollPos = EditorGUILayout.BeginScrollView(anchorScrollPos,
            GUILayout.MaxHeight(300));

        // 按分类分组显示（Foldout 折叠）
        string currentCategory = "";
        for (int i = 0; i < cachedAnchors.Count; i++)
        {
            TeleportAnchor anchor = cachedAnchors[i];

            // S33: Fake Null 防御（审计意见第 1 点）
            // 游玩过程中敌人被踩死、一次性陷阱被 Destroy 后，
            // 缓存列表中的引用在 Unity 底层会变成 null。
            // 必须在访问其属性前检查，否则会抛 MissingReferenceException。
            if (anchor.SourceObject == null) continue;

            // 分类标题 + Foldout
            if (anchor.Category != currentCategory)
            {
                currentCategory = anchor.Category;
                if (!anchorCategoryFoldouts.ContainsKey(currentCategory))
                    anchorCategoryFoldouts[currentCategory] = true;
                anchorCategoryFoldouts[currentCategory] = EditorGUILayout.Foldout(
                    anchorCategoryFoldouts[currentCategory],
                    $"{currentCategory} ({CountAnchorsInCategory(currentCategory)})",
                    true, EditorStyles.foldoutHeader);
            }

            if (!anchorCategoryFoldouts.ContainsKey(currentCategory) ||
                !anchorCategoryFoldouts[currentCategory])
                continue;

            // 绘制传送按钮
            EditorGUILayout.BeginHorizontal();
            GUI.color = anchor.ButtonColor;

            string dangerTag = anchor.IsDangerous ? " [SAFE+2]" : "";
            string btnLabel = $"{anchor.Name}{dangerTag}\n({anchor.SafePosition.x:F1}, {anchor.SafePosition.y:F1})";

            if (GUILayout.Button(btnLabel, GUILayout.Height(32)))
            {
                TeleportBothPlayers(anchor.SafePosition);
                Debug.Log($"[TestConsole] Teleported to dynamic anchor: {anchor.Name} " +
                          $"at ({anchor.SafePosition.x:F1}, {anchor.SafePosition.y:F1})" +
                          (anchor.IsDangerous ? " [safe offset applied]" : ""));
            }

            GUI.color = Color.white;

            // 聚焦按钮：在 Scene View 中定位到该元素
            if (GUILayout.Button("F", GUILayout.Width(22), GUILayout.Height(32)))
            {
                if (anchor.SourceObject is Component comp && comp != null)
                {
                    Selection.activeGameObject = comp.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
                else if (anchor.SourceObject is GameObject go && go != null)
                {
                    Selection.activeGameObject = go;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    /// <summary>统计指定分类的有效锚点数量（跳过已销毁对象）</summary>
    private int CountAnchorsInCategory(string category)
    {
        if (cachedAnchors == null) return 0;
        int count = 0;
        foreach (var a in cachedAnchors)
        {
            if (a.Category == category && a.SourceObject != null)
                count++;
        }
        return count;
    }
}
