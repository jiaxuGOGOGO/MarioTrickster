using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// MarioTrickster Test Console — 统一测试配置窗口
/// 
/// 快捷键: Ctrl+T (Windows) / Cmd+T (Mac)
/// 菜单:   MarioTrickster → Test Console
///
/// 功能概览:
///   Tab 1 — 传送与生成 (Teleport & Spawn)
///     · Stage 1~9 + GoalZone 一键传送（Mario + Trickster + Camera 硬切）
///     · 复活 Mario / 补满能量 / 重置关卡元素
///
///   Tab 2 — 全局调试开关 (Global Debug)
///     · God Mode (无敌)：PlayerHealth.DebugGodMode
///     · No Cooldown：GameManager.NoCooldownMode
///     · Infinite Energy：EnergySystem.DebugInfiniteEnergy
///     · Instant Blend (秒速融入)：DisguiseSystem.DebugInstantBlend
///     · Time Scale 滑动条 (0.1x ~ 3.0x)
///     · Input Debug 显示开关
///
///   Tab 3 — 关卡元素集控 (Elements Hub)
///     · 按类别分组显示 LevelElementRegistry 中的所有元素
///     · 点击元素名称 → Scene 视图聚焦 + Inspector 选中
///     · 一键 Reset All 重置所有元素状态
///
///   Tab 4 — 场景构建辅助 (Builder Tools)
///     · 一键生成 / 清空测试场景
///     · 运行 EditMode / PlayMode 测试报告
///
/// 设计原则:
///   1. 所有调试开关使用 [System.NonSerialized] + #if UNITY_EDITOR || DEVELOPMENT_BUILD 宏隔离
///   2. 所有开关默认关闭，每次 Play 自动重置，不影响 114 个自动化测试
///   3. 传送时调用 CameraController.SnapToTarget() 实现相机硬切
///   4. 不修改任何核心逻辑，仅通过公开 API 进行状态干预
///
/// Session 23: 新增
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
    private readonly string[] tabNames = { "Teleport", "Debug", "Elements", "Builder" };
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

    // Debug 开关本地状态（用于 UI 显示，实际值存在运行时组件上）
    private float timeScaleValue = 1f;

    // Elements Hub 折叠状态
    private Dictionary<string, bool> categoryFoldouts = new Dictionary<string, bool>();

    // ═══════════════════════════════════════════════════
    // 菜单入口
    // ═══════════════════════════════════════════════════
    [MenuItem("MarioTrickster/Test Console %t", false, 10)]
    public static void ShowWindow()
    {
        var window = GetWindow<TestConsoleWindow>("Test Console");
        window.minSize = new Vector2(340, 480);
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
        // 进入 PlayMode 时清空缓存，确保重新获取
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            ClearCache();
            timeScaleValue = 1f;
        }
        // 退出 PlayMode 时恢复 TimeScale
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Time.timeScale = 1f;
        }
        Repaint();
    }

    private void Update()
    {
        // PlayMode 下定期刷新（每秒约 10 次）
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
        GUILayout.Label("MarioTrickster Test Console", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // 运行状态指示
        EditorGUILayout.BeginHorizontal();
        Color statusColor = EditorApplication.isPlaying ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.8f, 0.2f);
        GUI.color = statusColor;
        GUILayout.Label(EditorApplication.isPlaying ? "● PLAY MODE" : "○ EDIT MODE", EditorStyles.boldLabel);
        GUI.color = Color.white;
        GUILayout.FlexibleSpace();

        // PlayMode 下显示活跃的调试开关数量
        if (EditorApplication.isPlaying)
        {
            int activeCount = CountActiveDebugFlags();
            if (activeCount > 0)
            {
                GUI.color = new Color(1f, 0.6f, 0.2f);
                GUILayout.Label($"[{activeCount} DEBUG FLAGS ON]", EditorStyles.boldLabel);
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
            case 0: DrawTeleportTab(); break;
            case 1: DrawDebugTab(); break;
            case 2: DrawElementsTab(); break;
            case 3: DrawBuilderTab(); break;
        }

        EditorGUILayout.EndScrollView();
    }

    // ═══════════════════════════════════════════════════
    // Tab 1: 传送与生成
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

            // 左列
            DrawStageButton(i);

            // 右列（如果存在）
            if (i + 1 < STAGE_NAMES.Length)
            {
                DrawStageButton(i + 1);
            }

            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();

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

        // 自定义坐标传送
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Custom Teleport", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        customTeleportX = EditorGUILayout.FloatField("X", customTeleportX);
        customTeleportY = EditorGUILayout.FloatField("Y", customTeleportY);
        if (GUILayout.Button("Go", GUILayout.Width(40)))
        {
            TeleportBothPlayers(new Vector3(customTeleportX, customTeleportY, 0));
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();
    }

    private float customTeleportX = 0f;
    private float customTeleportY = 1f;

    private void DrawStageButton(int index)
    {
        // GoalZone 用绿色高亮
        bool isGoal = index == STAGE_NAMES.Length - 1;
        if (isGoal) GUI.color = new Color(0.5f, 1f, 0.5f);

        if (GUILayout.Button(STAGE_NAMES[index], GUILayout.Height(30)))
        {
            TeleportToStage(index);
        }

        if (isGoal) GUI.color = Color.white;
    }

    // ═══════════════════════════════════════════════════
    // Tab 2: 全局调试开关
    // ═══════════════════════════════════════════════════
    private void DrawDebugTab()
    {
        EditorGUILayout.LabelField("Global Debug Toggles", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "所有开关默认关闭，每次 Play 自动重置。\n不影响自动化测试。仅在 PlayMode 下可用。",
            MessageType.Info);

        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);

        EnsureCache();

        EditorGUILayout.BeginVertical("box");

        // ── Mario 调试 ──
        EditorGUILayout.LabelField("Mario", EditorStyles.boldLabel);

        // God Mode
        DrawDebugToggle(
            "God Mode (无敌)",
            "不扣血、不触发死亡",
            GetGodMode(),
            (val) => SetGodMode(val),
            new Color(1f, 0.3f, 0.3f));

        EditorGUILayout.Space(4);

        // ── Trickster 调试 ──
        EditorGUILayout.LabelField("Trickster", EditorStyles.boldLabel);

        // No Cooldown
        DrawDebugToggle(
            "No Cooldown (无冷却)",
            "伪装/扫描/道具冷却立即清零",
            GetNoCooldown(),
            (val) => SetNoCooldown(val),
            new Color(0.3f, 0.7f, 1f));

        // Infinite Energy
        DrawDebugToggle(
            "Infinite Energy (无限能量)",
            "能量不消耗，始终满值",
            GetInfiniteEnergy(),
            (val) => SetInfiniteEnergy(val),
            new Color(0.3f, 0.7f, 1f));

        // Instant Blend
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
    // Tab 3: 关卡元素集控
    // ═══════════════════════════════════════════════════
    private void DrawElementsTab()
    {
        EditorGUILayout.LabelField("Level Elements Hub", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "显示 LevelElementRegistry 中所有已注册元素。\n点击名称 → Scene 聚焦 + Inspector 选中。\n仅在 PlayMode 下可用（元素需运行时注册）。",
            MessageType.Info);

        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);

        // 统计
        int totalCount = LevelElementRegistry.TotalCount;
        EditorGUILayout.LabelField($"Registered Elements: {totalCount}");

        // 一键操作
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

        // 按类别分组显示
        elementsScrollPos = EditorGUILayout.BeginScrollView(elementsScrollPos, GUILayout.MinHeight(200));

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

                    // 元素名称按钮 → 聚焦
                    if (GUILayout.Button(elem.Name, EditorStyles.linkLabel))
                    {
                        if (elem.Component != null)
                        {
                            Selection.activeGameObject = elem.Component.gameObject;
                            SceneView.lastActiveSceneView?.FrameSelected();
                        }
                    }

                    // 标签显示
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

        EditorGUI.EndDisabledGroup();
    }

    // ═══════════════════════════════════════════════════
    // Tab 4: 场景构建辅助
    // ═══════════════════════════════════════════════════
    private void DrawBuilderTab()
    {
        EditorGUILayout.LabelField("Scene Builder Tools", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "快速生成/清空测试场景，运行测试报告。\n仅在 EditMode 下可用。",
            MessageType.Info);

        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Test Scene", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(0.5f, 1f, 0.5f);
        if (GUILayout.Button("Build Test Scene", GUILayout.Height(35)))
        {
            TestSceneBuilder.BuildTestScene();
        }
        GUI.color = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("Clear Test Scene", GUILayout.Height(35)))
        {
            TestSceneBuilder.ClearTestScene();
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Test Reports", EditorStyles.boldLabel);

        if (GUILayout.Button("Run EditMode Tests", GUILayout.Height(28)))
        {
            EditorApplication.ExecuteMenuItem("MarioTrickster/Run Tests/Export Full Report (EditMode)");
        }
        if (GUILayout.Button("Run PlayMode Tests", GUILayout.Height(28)))
        {
            EditorApplication.ExecuteMenuItem("MarioTrickster/Run Tests/Export Full Report (PlayMode)");
        }
        if (GUILayout.Button("Run All Tests + Report", GUILayout.Height(28)))
        {
            EditorApplication.ExecuteMenuItem("MarioTrickster/Run Tests/Export Full Report (All)");
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // 快捷键参考
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Keyboard Shortcuts", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Ctrl+T       Open Test Console");
        EditorGUILayout.LabelField("F5           Quick Restart Level");
        EditorGUILayout.LabelField("F9           Toggle No Cooldown");
        EditorGUILayout.LabelField("ESC          Pause/Resume");
        EditorGUILayout.LabelField("R            Restart (Round Over)");
        EditorGUILayout.LabelField("N            Next Round (Round Over)");
        EditorGUILayout.EndVertical();

        EditorGUI.EndDisabledGroup();
    }

    // ═══════════════════════════════════════════════════
    // 传送逻辑
    // ═══════════════════════════════════════════════════

    /// <summary>传送到指定 Stage（0-based index，最后一个为 GoalZone）</summary>
    private void TeleportToStage(int stageIndex)
    {
        if (!EditorApplication.isPlaying) return;
        EnsureCache();

        Vector3 targetPos;

        if (stageIndex < 9)
        {
            // Stage 1~9: 使用与 TestSceneBuilder 相同的公式
            float stageStartX = stageIndex * TOTAL_STAGE_UNIT;
            targetPos = new Vector3(stageStartX + 3f, 1f, 0f);
        }
        else
        {
            // GoalZone: Stage 9 子区域之后
            // Stage 9 从 index 8 开始，有 9 个子区域 (s9SubWidth=8)，GoalZone 在最后
            float s9 = 8 * TOTAL_STAGE_UNIT;
            float s9SubWidth = 8f;
            float goalX = s9 + 9 * s9SubWidth + 2f;
            targetPos = new Vector3(goalX - 3f, 1f, 0f);
        }

        TeleportBothPlayers(targetPos);

        Debug.Log($"[TestConsole] Teleported to {STAGE_NAMES[stageIndex]} at ({targetPos.x:F1}, {targetPos.y:F1})");
    }

    /// <summary>将 Mario 和 Trickster 传送到指定位置，相机硬切</summary>
    private void TeleportBothPlayers(Vector3 position)
    {
        if (!EditorApplication.isPlaying) return;
        EnsureCache();

        // 传送 Mario
        if (cachedMario != null)
        {
            cachedMario.transform.position = position;
            // 清零速度（通过 Rigidbody2D）
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

        // 相机硬切（关键！避免 5 秒慢飘）
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

        // 重新启用 Mario 控制器（死亡后可能被禁用）
        if (cachedMario != null)
        {
            cachedMario.enabled = true;
            // 重置 SpriteRenderer 可见性
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

        // 同时重置 GoalZone 触发状态
        GoalZone[] goalZones = Object.FindObjectsOfType<GoalZone>();
        foreach (GoalZone gz in goalZones)
        {
            gz.ResetTrigger();
        }

        // 重置可操控道具使用次数
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
        // 同时设置 Mario 和 Trickster 的 PlayerHealth
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

        // GameManager 的 NoCooldownMode 是只读属性，通过反射设置私有字段
        // 或者直接模拟 F9 按键（更安全，复用现有逻辑）
        bool current = cachedGameManager.NoCooldownMode;
        if (current != value)
        {
            // 使用反射设置私有字段 noCooldownMode
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
