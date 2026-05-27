using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public partial class TestConsoleWindow
{
    // ═══════════════════════════════════════════════════
    // Tab 4: Global Test Cheats
    //
    // v2 清爽化重构要点：
    //   - 顶部 HelpBox → 标题栏已有 Cheat 计数，此处用 CompactTip
    //   - Toggle 分组更紧凑（去掉组间 Space）
    //   - Time Scale 快捷按钮精简为常用 4 档
    //   - Runtime Status 改为紧凑单行格式
    //   - 所有功能 100% 保留
    // ═══════════════════════════════════════════════════
    private void DrawCheatsTab()
    {
        LevelStudioStyles.CompactTip("所有开关每次 Play 自动重置 | 不影响自动化测试 | Release 包零残留");

        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);

        EnsureCache();

        // S33: 缺失组件检测 + 一键修复
        bool cheatsAvailable = (cachedMarioHealth != null && cachedGameManager != null &&
                                cachedEnergy != null && cachedDisguise != null);

        if (EditorApplication.isPlaying && !cheatsAvailable)
        {
            EditorGUILayout.HelpBox(
                "缺少核心组件：" +
                (cachedMarioHealth == null ? "PlayerHealth " : "") +
                (cachedGameManager == null ? "GameManager " : "") +
                (cachedEnergy == null ? "EnergySystem " : "") +
                (cachedDisguise == null ? "DisguiseSystem " : "") +
                "— 请先生成关卡或点击 Auto-Fix",
                MessageType.Warning);

            GameObject asciiRoot = GameObject.Find("AsciiLevel_Root");
            if (asciiRoot != null)
            {
                if (LevelStudioStyles.ColorButton("Auto-Fix: Inject Playable Environment", LevelStudioStyles.AccentGreen, 26f))
                {
                    PlayableEnvironmentBuilder.EnsurePlayableEnvironment(asciiRoot);
                    ClearCache();
                    EnsureCache();
                    cachedAnchors = null;
                    Debug.Log("[TestConsole] Auto-Fix: Playable environment injected for Cheats.");
                }
            }
            else
            {
                LevelStudioStyles.CompactTip("未找到 AsciiLevel_Root，请先在 Level Design Tab 生成关卡");
            }
        }

        // S33: 视觉阻断
        EditorGUI.BeginDisabledGroup(!cheatsAvailable && EditorApplication.isPlaying);

        EditorGUILayout.BeginVertical("box");

        // ── Mario ──
        LevelStudioStyles.SubHeader("Mario");
        DrawDebugToggle("God Mode", "不扣血、不触发死亡", GetGodMode(), (val) => SetGodMode(val), new Color(1f, 0.3f, 0.3f));

        // ── Trickster ──
        LevelStudioStyles.SubHeader("Trickster");
        DrawDebugToggle("No Cooldown", "伪装/扫描/道具冷却清零", GetNoCooldown(), (val) => SetNoCooldown(val), new Color(0.3f, 0.7f, 1f));
        DrawDebugToggle("Infinite Energy", "能量不消耗", GetInfiniteEnergy(), (val) => SetInfiniteEnergy(val), new Color(0.3f, 0.7f, 1f));
        DrawDebugToggle("Instant Blend", "伪装后立即融入", GetInstantBlend(), (val) => SetInstantBlend(val), new Color(0.3f, 0.7f, 1f));

        // ── Global ──
        LevelStudioStyles.SubHeader("Global");

        // Time Scale（紧凑化：滑块 + 常用 4 档）
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Speed", GUILayout.Width(38));
        float newTimeScale = EditorGUILayout.Slider(timeScaleValue, 0.1f, 3.0f);
        if (!Mathf.Approximately(newTimeScale, timeScaleValue))
        {
            timeScaleValue = newTimeScale;
            Time.timeScale = timeScaleValue;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("0.25x", GUILayout.Height(18))) { timeScaleValue = 0.25f; Time.timeScale = 0.25f; }
        if (GUILayout.Button("0.5x", GUILayout.Height(18))) { timeScaleValue = 0.5f; Time.timeScale = 0.5f; }
        if (GUILayout.Button("1x", GUILayout.Height(18))) { timeScaleValue = 1f; Time.timeScale = 1f; }
        if (GUILayout.Button("2x", GUILayout.Height(18))) { timeScaleValue = 2f; Time.timeScale = 2f; }
        if (GUILayout.Button("3x", GUILayout.Height(18))) { timeScaleValue = 3f; Time.timeScale = 3f; }
        EditorGUILayout.EndHorizontal();

        DrawDebugToggle("Input Debug", "屏幕左上角显示按键状态", GetInputDebug(), (val) => SetInputDebug(val), new Color(0.8f, 0.8f, 0.3f));

        // ── Visualization ──
        LevelStudioStyles.SubHeader("Visualization");
        DrawDebugToggle("Gameplay Boxes", "Scene 视图绘制语义线框", GameplayBoxVisualizer.ShowGameplayBoxes,
            (val) => { GameplayBoxVisualizer.ShowGameplayBoxes = val; SceneView.RepaintAll(); }, new Color(0.4f, 0.9f, 0.8f));
        DrawDebugToggle("Trap Phase", "ControllableProp 上方显示阶段标签", GameplayBoxVisualizer.ShowTrapPhase,
            (val) => { GameplayBoxVisualizer.ShowTrapPhase = val; SceneView.RepaintAll(); }, new Color(0.4f, 0.9f, 0.8f));

        EditorGUILayout.EndVertical();

        // ── 一键全开/全关（紧凑化） ──
        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        if (LevelStudioStyles.ColorButton("Enable All", LevelStudioStyles.AccentOrange, 24f))
        {
            SetGodMode(true); SetNoCooldown(true); SetInfiniteEnergy(true); SetInstantBlend(true);
            GameplayBoxVisualizer.ShowGameplayBoxes = true; GameplayBoxVisualizer.ShowTrapPhase = true;
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Disable All", GUILayout.Height(24)))
        {
            SetGodMode(false); SetNoCooldown(false); SetInfiniteEnergy(false); SetInstantBlend(false);
            timeScaleValue = 1f; Time.timeScale = 1f;
            GameplayBoxVisualizer.ShowGameplayBoxes = false; GameplayBoxVisualizer.ShowTrapPhase = false;
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup(); // cheatsAvailable
        EditorGUI.EndDisabledGroup(); // !isPlaying

        // ── Runtime Status（紧凑化） ──
        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);

            EnsureCache();

            if (cachedMarioHealth != null)
                EditorGUILayout.LabelField($"Mario HP: {cachedMarioHealth.CurrentHealth}/{cachedMarioHealth.MaxHealth}", EditorStyles.miniLabel);
            if (cachedEnergy != null)
                EditorGUILayout.LabelField($"Energy: {cachedEnergy.CurrentEnergy:F0}/{cachedEnergy.MaxEnergy:F0} ({cachedEnergy.EnergyPercent * 100:F0}%)", EditorStyles.miniLabel);
            if (cachedDisguise != null)
                EditorGUILayout.LabelField($"Disguise: {(cachedDisguise.IsDisguised ? "YES" : "No")} | Blend: {(cachedDisguise.IsFullyBlended ? "YES" : "No")}", EditorStyles.miniLabel);
            if (cachedGameManager != null)
            {
                EditorGUILayout.LabelField($"State: {cachedGameManager.CurrentState} | T: {cachedGameManager.GameTimer:F1}s | R{cachedGameManager.CurrentRound}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Score: M{cachedGameManager.MarioWins} - T{cachedGameManager.TricksterWins}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();

            // ── AI Auto-Arena 折叠栏 ──
            DrawAIArenaSection();
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

    /// <summary>统计当前激活的调试开关数量（用于标题栏计数器）</summary>
    private int CountActiveDebugFlags()
    {
        int count = 0;
        if (GetGodMode()) count++;
        if (GetNoCooldown()) count++;
        if (GetInfiniteEnergy()) count++;
        if (GetInstantBlend()) count++;
        if (GetInputDebug()) count++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GameplayBoxVisualizer.ShowGameplayBoxes) count++;
        if (GameplayBoxVisualizer.ShowTrapPhase) count++;
#endif
        return count;
    }
}
