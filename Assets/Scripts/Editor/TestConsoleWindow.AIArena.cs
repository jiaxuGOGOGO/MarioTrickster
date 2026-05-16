using UnityEngine;
using UnityEditor;
using System.Collections;

// ═══════════════════════════════════════════════════════════════════
// TestConsoleWindow.AIArena — AI 自动挂机角斗场 (S60 升级版)
//
// 在 Cheats Tab 末尾绘制折叠栏，提供：
//   1. [Mario 托管 (F1)] Toggle — 切换 Mario 人机控制
//   2. [Trickster 托管 (F2)] Toggle — 切换 Trickster 人机控制
//   3. [Time Scale] 滑动条 — 1x~5x 快进对局
//   4. [Auto Restart] Toggle — 回合结束后自动延迟 1s 重开
//   5. [Print Match Report] 按钮 — 输出汇总战报
//
// S60 改动：
//   - 移除原来的单体 [Enable AI Bots]，改为两个独立 Toggle
//   - 通过 HybridInputProvider（默认 Provider）的 MarioIsAI / TricksterIsAI 控制
//   - F1/F2 热键在 HybridInputProvider.Tick() 中检测，Editor UI 仅做同步显示
//   - 关闭时 Time.timeScale 恢复 1.0f，MarioIsAI/TricksterIsAI 恢复 false
// ═══════════════════════════════════════════════════════════════════

public partial class TestConsoleWindow
{
    // ═══════════════════════════════════════════════════
    // AI Arena 状态字段
    // ═══════════════════════════════════════════════════

    private float arenaTimeScale = 1.0f;
    private bool autoRestartEnabled = false;
    private bool showAIArena = true;

    private AutoTestAnalytics _analytics;
    private AutoRestartHelper _autoRestartHelper;

    // ═══════════════════════════════════════════════════
    // 获取当前 HybridInputProvider（从 InputManager）
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 从 InputManager 获取当前的 HybridInputProvider。
    /// 如果当前 Provider 不是 Hybrid（如被测试注入了 AutomatedInputProvider），返回 null。
    /// </summary>
    private HybridInputProvider GetHybridProvider()
    {
        EnsureCache();
        if (cachedInputManager == null) return null;
        return cachedInputManager.GetCurrentProvider() as HybridInputProvider;
    }

    // ═══════════════════════════════════════════════════
    // 绘制 AI Arena 折叠栏（由 DrawCheatsTab 末尾调用）
    // ═══════════════════════════════════════════════════

    private void DrawAIArenaSection()
    {
        EditorGUILayout.Space(8);

        var hybrid = GetHybridProvider();
        bool anyAI = hybrid != null && (hybrid.MarioIsAI || hybrid.TricksterIsAI);

        // 折叠栏标题
        GUI.color = anyAI ? new Color(0.3f, 1f, 0.5f) : Color.white;
        showAIArena = EditorGUILayout.Foldout(showAIArena,
            "\U0001f916 AI Auto-Arena (\u81ea\u52a8\u6302\u673a\u89d2\u6597\u573a)" + (anyAI ? " [ACTIVE]" : ""),
            true, EditorStyles.foldoutHeader);
        GUI.color = Color.white;

        if (!showAIArena) return;

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.HelpBox(
            "\u72ec\u7acb\u63a7\u5236 Mario \u548c Trickster \u7684\u4eba\u673a\u5207\u6362\u3002\n" +
            "\u6e38\u73a9\u4e2d\u6309 F1/F2 \u4e00\u952e\u593a\u820d\uff0c\u6216\u5728\u6b64\u52fe\u9009\u3002\n" +
            "\u5173\u95ed\u65f6\u81ea\u52a8\u6062\u590d Time.timeScale = 1.0\u3002",
            MessageType.Info);

        if (hybrid == null)
        {
            EditorGUILayout.HelpBox(
                "\u5f53\u524d InputProvider \u4e0d\u662f HybridInputProvider\uff0c\u65e0\u6cd5\u63a7\u5236\u4eba\u673a\u5207\u6362\u3002\n" +
                "\u53ef\u80fd\u662f\u81ea\u52a8\u5316\u6d4b\u8bd5\u6b63\u5728\u8fd0\u884c\u3002",
                MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        // ── Mario 托管 (F1) Toggle ──
        EditorGUILayout.Space(4);
        GUI.color = hybrid.MarioIsAI ? new Color(0.4f, 0.9f, 1f) : Color.white;
        bool newMarioAI = EditorGUILayout.Toggle(
            new GUIContent("Mario \u6258\u7ba1 (F1)", "\u5207\u6362 Mario \u4e3a AI \u63a7\u5236\uff0c\u6216\u6309 F1 \u70ed\u952e"),
            hybrid.MarioIsAI);
        GUI.color = Color.white;

        if (newMarioAI != hybrid.MarioIsAI)
        {
            hybrid.MarioIsAI = newMarioAI;
            string who = newMarioAI ? "\U0001f916 AI" : "\U0001f464 \u4eba\u7c7b";
            Debug.Log($"<color=#00FF88><b>[\u8f93\u5165\u6d41] Mario \u5df2\u5207\u6362\u4e3a{who}\u63a7\u5236\uff01(Editor Toggle)</b></color>");
        }

        // ── Trickster 托管 (F2) Toggle ──
        GUI.color = hybrid.TricksterIsAI ? new Color(1f, 0.6f, 0.2f) : Color.white;
        bool newTricksterAI = EditorGUILayout.Toggle(
            new GUIContent("Trickster \u6258\u7ba1 (F2)", "\u5207\u6362 Trickster \u4e3a AI \u63a7\u5236\uff0c\u6216\u6309 F2 \u70ed\u952e"),
            hybrid.TricksterIsAI);
        GUI.color = Color.white;

        if (newTricksterAI != hybrid.TricksterIsAI)
        {
            hybrid.TricksterIsAI = newTricksterAI;
            string who = newTricksterAI ? "\U0001f916 AI" : "\U0001f464 \u4eba\u7c7b";
            Debug.Log($"<color=#FF8800><b>[\u8f93\u5165\u6d41] Trickster \u5df2\u5207\u6362\u4e3a{who}\u63a7\u5236\uff01(Editor Toggle)</b></color>");
        }

        // ── 当前状态指示 ──
        EditorGUILayout.BeginHorizontal();
        string marioLabel = hybrid.MarioIsAI ? "\U0001f916 AI" : "\U0001f464 Human";
        string trickLabel = hybrid.TricksterIsAI ? "\U0001f916 AI" : "\U0001f464 Human";
        EditorGUILayout.LabelField($"Mario: {marioLabel}  |  Trickster: {trickLabel}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        // ── Time Scale 滑动条 ──
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Time Scale", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        float newArenaTimeScale = EditorGUILayout.Slider(arenaTimeScale, 1.0f, 5.0f);
        if (!Mathf.Approximately(newArenaTimeScale, arenaTimeScale))
        {
            arenaTimeScale = newArenaTimeScale;
            Time.timeScale = arenaTimeScale;
        }
        // 快捷按钮
        if (GUILayout.Button("1x", GUILayout.Width(30))) { arenaTimeScale = 1f; Time.timeScale = 1f; }
        if (GUILayout.Button("3x", GUILayout.Width(30))) { arenaTimeScale = 3f; Time.timeScale = 3f; }
        if (GUILayout.Button("5x", GUILayout.Width(30))) { arenaTimeScale = 5f; Time.timeScale = 5f; }
        EditorGUILayout.EndHorizontal();

        // ── Auto Restart Toggle ──
        EditorGUILayout.Space(4);
        bool newAutoRestart = EditorGUILayout.Toggle(
            new GUIContent("Auto Restart", "\u56de\u5408\u7ed3\u675f\u540e\u5ef6\u8fdf 1 \u79d2\u81ea\u52a8\u91cd\u5f00"),
            autoRestartEnabled);

        if (newAutoRestart != autoRestartEnabled)
        {
            autoRestartEnabled = newAutoRestart;
            if (autoRestartEnabled)
                EnableAutoRestart();
            else
                DisableAutoRestart();
        }

        // ── Analytics 控制 ──
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Data Collection", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        bool analyticsActive = _analytics != null;
        if (!analyticsActive)
        {
            if (GUILayout.Button("Start Collecting"))
            {
                _analytics = new AutoTestAnalytics();
                EnsureCache();
                if (cachedGameManager != null)
                    _analytics.StartCollecting(cachedGameManager);
                Debug.Log("[AI Arena] Analytics started.");
            }
        }
        else
        {
            if (GUILayout.Button("Stop Collecting"))
            {
                _analytics.StopCollecting();
                Debug.Log("[AI Arena] Analytics stopped (data preserved).");
            }
        }
        EditorGUILayout.EndHorizontal();

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
                Debug.LogWarning("[AI Arena] No analytics data. Click 'Start Collecting' first.");
        }
        GUI.color = Color.white;

        // ── Reset Stats 按钮 ──
        if (GUILayout.Button("Reset Stats"))
        {
            _analytics?.Reset();
            Debug.Log("[AI Arena] Stats reset.");
        }

        EditorGUILayout.EndVertical();
    }

    // ═══════════════════════════════════════════════════
    // Auto Restart 控制
    // ═══════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════
    // PlayMode 退出时自动清理
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// PlayMode 退出时自动清理（由 OnPlayModeChanged 回调触发）。
    /// 恢复 MarioIsAI/TricksterIsAI = false，Time.timeScale = 1.0f。
    /// </summary>
    private void CleanupAIArena()
    {
        // 恢复 HybridInputProvider 状态
        var hybrid = GetHybridProvider();
        if (hybrid != null)
        {
            hybrid.MarioIsAI = false;
            hybrid.TricksterIsAI = false;
        }

        // 停止数据收集
        _analytics?.StopCollecting();
        _analytics = null;

        // 停止自动重启
        DisableAutoRestart();
        autoRestartEnabled = false;

        // 强制恢复 Time.timeScale
        Time.timeScale = 1.0f;
        arenaTimeScale = 1.0f;
        timeScaleValue = 1.0f;
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
