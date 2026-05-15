using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public partial class TestConsoleWindow
{
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
                    PlayableEnvironmentBuilder.EnsurePlayableEnvironment(asciiRoot);
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
}
