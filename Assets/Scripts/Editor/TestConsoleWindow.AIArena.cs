using UnityEngine;
using UnityEditor;
using System.Collections;

// ═══════════════════════════════════════════════════════════════════
// TestConsoleWindow.AIArena — AI 自动挂机角斗场
//
// 在 Cheats Tab 末尾绘制折叠栏，提供：
//   1. [Enable AI Bots] Toggle — 接管 InputManager，注入 HeuristicBotInputProvider
//   2. [Time Scale] 滑动条 — 1x~5x 快进对局
//   3. [Auto Restart] Toggle — 回合结束后自动延迟 1s 重开
//   4. [Print Match Report] 按钮 — 输出汇总战报
//
// [AI防坑警告]
//   - 关闭挂机时：Time.timeScale 强制恢复 1.0f，切回 KeyboardInputProvider
//   - 绝不影响玩家后续手操试玩体验
// ═══════════════════════════════════════════════════════════════════

public partial class TestConsoleWindow
{
    // ═══════════════════════════════════════════════════
    // AI Arena 状态字段
    // ═══════════════════════════════════════════════════

    private bool aiBotsEnabled = false;
    private float arenaTimeScale = 1.0f;
    private bool autoRestartEnabled = false;
    private bool showAIArena = true;

    private HeuristicBotInputProvider _activeBotProvider;
    private AutoTestAnalytics _analytics;
    private AutoRestartHelper _autoRestartHelper;

    // ═══════════════════════════════════════════════════
    // 绘制 AI Arena 折叠栏（由 DrawCheatsTab 末尾调用）
    // ═══════════════════════════════════════════════════

    private void DrawAIArenaSection()
    {
        EditorGUILayout.Space(8);

        // 折叠栏标题
        GUI.color = aiBotsEnabled ? new Color(0.3f, 1f, 0.5f) : Color.white;
        showAIArena = EditorGUILayout.Foldout(showAIArena,
            "\U0001f916 AI Auto-Arena (\u81ea\u52a8\u6302\u673a\u89d2\u6597\u573a)" + (aiBotsEnabled ? " [ACTIVE]" : ""),
            true, EditorStyles.foldoutHeader);
        GUI.color = Color.white;

        if (!showAIArena) return;

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.HelpBox(
            "\u5f00\u542f\u540e AI \u5c06\u63a5\u7ba1\u53cc\u65b9\u8f93\u5165\uff0c\u5feb\u8fdb\u5bf9\u5c40\u5e76\u6536\u96c6\u6570\u636e\u3002\n" +
            "\u5173\u95ed\u65f6\u81ea\u52a8\u6062\u590d Time.timeScale = 1.0 \u5e76\u5207\u56de\u952e\u76d8\u8f93\u5165\u3002",
            MessageType.Info);

        // ── Enable AI Bots Toggle ──
        bool newBotsEnabled = EditorGUILayout.Toggle(
            new GUIContent("Enable AI Bots", "\u63a5\u7ba1 InputManager\uff0c\u6ce8\u5165 HeuristicBotInputProvider"),
            aiBotsEnabled);

        if (newBotsEnabled != aiBotsEnabled)
        {
            aiBotsEnabled = newBotsEnabled;
            if (aiBotsEnabled)
                EnableAIBots();
            else
                DisableAIBots();
        }

        EditorGUI.BeginDisabledGroup(!aiBotsEnabled);

        // ── Time Scale 滑动条 ──
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Time Scale", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        float newArenaTimeScale = EditorGUILayout.Slider(arenaTimeScale, 1.0f, 5.0f);
        if (!Mathf.Approximately(newArenaTimeScale, arenaTimeScale))
        {
            arenaTimeScale = newArenaTimeScale;
            if (aiBotsEnabled)
                Time.timeScale = arenaTimeScale;
        }
        // 快捷按钮
        if (GUILayout.Button("1x", GUILayout.Width(30))) { arenaTimeScale = 1f; if (aiBotsEnabled) Time.timeScale = 1f; }
        if (GUILayout.Button("3x", GUILayout.Width(30))) { arenaTimeScale = 3f; if (aiBotsEnabled) Time.timeScale = 3f; }
        if (GUILayout.Button("5x", GUILayout.Width(30))) { arenaTimeScale = 5f; if (aiBotsEnabled) Time.timeScale = 5f; }
        EditorGUILayout.EndHorizontal();

        // ── Auto Restart Toggle ──
        EditorGUILayout.Space(4);
        bool newAutoRestart = EditorGUILayout.Toggle(
            new GUIContent("Auto Restart", "\u56de\u5408\u7ed3\u675f\u540e\u5ef6\u8fdf 1 \u79d2\u81ea\u52a8\u91cd\u5f00"),
            autoRestartEnabled);

        if (newAutoRestart != autoRestartEnabled)
        {
            autoRestartEnabled = newAutoRestart;
            if (aiBotsEnabled)
            {
                if (autoRestartEnabled)
                    EnableAutoRestart();
                else
                    DisableAutoRestart();
            }
        }

        // ── 实时统计显示 ──
        if (_analytics != null && _analytics.TotalMatches > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Live Stats", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"\u5bf9\u6218\u5c40\u6570: {_analytics.TotalMatches}");
            EditorGUILayout.LabelField($"Mario \u80dc: {_analytics.MarioWins} ({_analytics.MarioWinRate:F0}%) | Trickster \u80dc: {_analytics.TricksterWins} ({_analytics.TricksterWinRate:F0}%)");
            EditorGUILayout.LabelField($"\u5e73\u5747\u5355\u5c40\u8017\u65f6: {_analytics.AverageMatchTime:F1}s");
            EditorGUILayout.EndVertical();
        }

        // ── Print Match Report 按钮 ──
        EditorGUILayout.Space(4);
        GUI.color = new Color(1f, 0.85f, 0.2f);
        if (GUILayout.Button("Print Match Report", GUILayout.Height(28)))
        {
            if (_analytics != null)
                _analytics.PrintMatchReport();
            else
                Debug.LogWarning("[AI Arena] No analytics data. Enable AI Bots first.");
        }
        GUI.color = Color.white;

        // ── Reset Stats 按钮 ──
        if (GUILayout.Button("Reset Stats"))
        {
            _analytics?.Reset();
            Debug.Log("[AI Arena] Stats reset.");
        }

        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndVertical();
    }

    // ═══════════════════════════════════════════════════
    // AI Arena 控制逻辑
    // ═══════════════════════════════════════════════════

    private void EnableAIBots()
    {
        EnsureCache();

        if (cachedInputManager == null)
        {
            Debug.LogWarning("[AI Arena] InputManager not found. Cannot enable AI Bots.");
            aiBotsEnabled = false;
            return;
        }

        // 创建并注入 HeuristicBotInputProvider
        _activeBotProvider = new HeuristicBotInputProvider();
        cachedInputManager.SetInputProvider(_activeBotProvider);

        // 创建数据收集器
        _analytics = new AutoTestAnalytics();
        if (cachedGameManager != null)
            _analytics.StartCollecting(cachedGameManager);

        // 设置时间缩放
        Time.timeScale = arenaTimeScale;

        // 如果 Auto Restart 已勾选，立即启用
        if (autoRestartEnabled)
            EnableAutoRestart();

        Debug.Log($"[AI Arena] AI Bots ENABLED. TimeScale={arenaTimeScale:F1}x");
    }

    private void DisableAIBots()
    {
        // 停止数据收集
        _analytics?.StopCollecting();

        // 停止自动重启
        DisableAutoRestart();

        // 切回键盘输入
        EnsureCache();
        if (cachedInputManager != null)
            cachedInputManager.SetInputProvider(null); // null → 自动回退到 KeyboardInputProvider

        _activeBotProvider = null;

        // 强制恢复 Time.timeScale = 1.0f
        Time.timeScale = 1.0f;
        arenaTimeScale = 1.0f;

        // 同步 Cheats Tab 的 timeScaleValue
        timeScaleValue = 1.0f;

        Debug.Log("[AI Arena] AI Bots DISABLED. TimeScale restored to 1.0x, input restored to keyboard.");
    }

    private void EnableAutoRestart()
    {
        if (_autoRestartHelper != null) return;

        // 创建隐形 GameObject 挂载协程
        var go = new GameObject("[AI Arena] AutoRestart Helper");
        go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        _autoRestartHelper = go.AddComponent<AutoRestartHelper>();
        _autoRestartHelper.Initialize();

        Debug.Log("[AI Arena] Auto Restart ENABLED.");
    }

    private void DisableAutoRestart()
    {
        if (_autoRestartHelper != null)
        {
            _autoRestartHelper.Shutdown();
            if (_autoRestartHelper.gameObject != null)
                Object.DestroyImmediate(_autoRestartHelper.gameObject);
            _autoRestartHelper = null;
        }
    }

    /// <summary>
    /// PlayMode 退出时自动清理（由 OnEnable/OnDisable 中的 playModeStateChanged 回调触发）。
    /// </summary>
    private void CleanupAIArena()
    {
        if (aiBotsEnabled)
        {
            DisableAIBots();
            aiBotsEnabled = false;
            autoRestartEnabled = false;
        }
    }
}

// ═══════════════════════════════════════════════════════════════════
// AutoRestartHelper — 隐形协程脚本
//
// 挂载到隐形 GameObject 上，监听 GameManager.OnGameOver，
// 延迟 1 秒（UnscaledTime）后自动调用 ResetRound()。
// ═══════════════════════════════════════════════════════════════════

public class AutoRestartHelper : MonoBehaviour
{
    private GameManager _gm;
    private bool _initialized;

    public void Initialize()
    {
        _gm = GameManager.Instance ?? Object.FindObjectOfType<GameManager>();
        if (_gm != null)
        {
            _gm.OnGameOver += OnGameOver;
            _initialized = true;
        }
        else
        {
            Debug.LogWarning("[AutoRestartHelper] GameManager not found.");
        }
    }

    public void Shutdown()
    {
        if (_initialized && _gm != null)
        {
            _gm.OnGameOver -= OnGameOver;
        }
        StopAllCoroutines();
        _initialized = false;
    }

    private void OnGameOver(string winner)
    {
        if (_initialized && this != null && gameObject != null)
        {
            StartCoroutine(DelayedRestart());
        }
    }

    private IEnumerator DelayedRestart()
    {
        // 使用 WaitForSecondsRealtime 确保不受 Time.timeScale 影响
        yield return new WaitForSecondsRealtime(1.0f);

        if (_gm != null)
        {
            _gm.ResetRound();
            Debug.Log("[AutoRestartHelper] Auto restart triggered.");
        }
    }

    private void OnDestroy()
    {
        Shutdown();
    }
}
