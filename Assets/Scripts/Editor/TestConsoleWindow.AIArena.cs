using UnityEngine;
using UnityEditor;
using System;
using System.Collections;

// ═══════════════════════════════════════════════════════════════════
// TestConsoleWindow.AIArena — AI 自动挂机角斗场 (S60 升级版)
//
// 在 Cheats Tab 末尾绘制折叠栏，提供：
//   1. [Mario 托管 (F1)] Toggle — 切换 Mario 人机控制
//   2. [Trickster 托管 (F2)] Toggle — 切换 Trickster 人机控制
//   3. [Time Scale] 滑动条 — 1x~5x 快进对局
//   4. [Auto Restart] Toggle — 回合结束后自动延迟 1s 重开
//   5. [Print Match Report] 按钮 — 输出汇总战报，并导出 JSON/Markdown 结构化报告
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

    // S149: TAS Data-Driven Testing 状态字段
    private bool _tasEnabled = false;
    private string _tasLoadedFileName = "";
    private TasReplayData _tasReplayData = null;
    private int _tasFrameCount = 0;

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
            new GUIContent("Auto Restart", "回合结束后延迟 1 秒自动重开"),
            autoRestartEnabled);

        if (newAutoRestart != autoRestartEnabled)
        {
            autoRestartEnabled = newAutoRestart;
            if (autoRestartEnabled)
                EnableAutoRestart();
            else
                DisableAutoRestart();
        }

        // ═════════════════════════════════════════════════
        // S149: TAS Data-Driven Testing 区域
        // ═════════════════════════════════════════════════
        DrawTasSection(hybrid);

        // ── Analytics 控制 ──
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Data Collection", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        bool analyticsActive = _analytics != null;
        if (!analyticsActive)
        {
            if (GUILayout.Button("Start Collecting"))
            {
                // S149: Start Collecting 时，如果 TAS 模式开启，自动重置并开始播放
                if (_tasEnabled && hybrid.TasProvider != null)
                {
                    hybrid.ResetTasPlayback();
                    Debug.Log("[AI Arena] TAS replay reset for data collection.");
                }
                _analytics = new AutoTestAnalytics();
                EnsureCache();
                if (cachedGameManager != null)
                    _analytics.StartCollecting(cachedGameManager);
                // 同步当前人格名到 Analytics
                var hybrid2 = GetHybridProvider();
                if (hybrid2 != null)
                {
                    string mName = hybrid2.marioPersona != null ? hybrid2.marioPersona.personaName : "Default";
                    string tName = hybrid2.tricksterPersona != null ? hybrid2.tricksterPersona.personaName : "Default";
                    _analytics.SetPersonaNames(mName, tName);
                }
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

        // ── Show Test Heatmap Toggle ──
        EditorGUILayout.Space(4);
        bool currentHeatmap = AnalyticsGizmoRenderer.ShowTestHeatmap;
        GUI.color = currentHeatmap ? new Color(1f, 0.5f, 0.3f) : Color.white;
        bool newHeatmap = EditorGUILayout.Toggle(
            new GUIContent("Show Test Heatmap (\u663e\u793a\u6d4b\u8bd5\u70ed\u529b\u56fe)",
                "\u5728 Scene \u89c6\u56fe\u4e2d\u663e\u793a\u6b7b\u4ea1\u70ed\u529b\u56fe\uff08\u7ea2\u7403\uff09\u548c\u5361\u6b7b\u8b66\u544a\uff08\u9ec4\u5757\uff09"),
            currentHeatmap);
        GUI.color = Color.white;

        if (newHeatmap != currentHeatmap)
        {
            AnalyticsGizmoRenderer.ShowTestHeatmap = newHeatmap;
            SceneView.RepaintAll();
            Debug.Log($"[AI Arena] Show Test Heatmap: {(newHeatmap ? "ON" : "OFF")}");
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
            {
                // 导出前同步最新人格名（支持运行中热切换人格）
                var hp = GetHybridProvider();
                if (hp != null)
                {
                    string mn = hp.marioPersona != null ? hp.marioPersona.personaName : "Default";
                    string tn = hp.tricksterPersona != null ? hp.tricksterPersona.personaName : "Default";
                    _analytics.SetPersonaNames(mn, tn);
                }
                _analytics.PrintMatchReport();
                string mdPath = AIArenaReportExporter.ExportMatchReport(_analytics);
                if (!string.IsNullOrEmpty(mdPath))
                {
                    string mdUrl = new Uri(mdPath).AbsoluteUri;
                    Debug.Log($"<color=#88FF88><b>[AI Arena] 结构化战报已导出。</b></color> <a href=\"{mdUrl}\">点击打开 auto_test_summary.md</a>\n{mdPath}");
                }
            }
            else
            {
                Debug.LogWarning("[AI Arena] No analytics data. Click 'Start Collecting' first.");
            }
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

        // S149: 挂载 TAS 循环播放回调
        _autoRestartHelper.OnBeforeRestart = () =>
        {
            var h = GetHybridProvider();
            if (h != null && h.MarioIsTAS && h.TasProvider != null)
            {
                h.ResetTasPlayback();
                Debug.Log("[AutoRestartHelper] TAS replay reset for next round.");
            }
        };

        Debug.Log("[AI Arena] Auto Restart ENABLED.");
    }

    private void DisableAutoRestart()
    {
        if (_autoRestartHelper != null)
        {
            _autoRestartHelper.Shutdown();
            if (_autoRestartHelper.gameObject != null)
                UnityEngine.Object.DestroyImmediate(_autoRestartHelper.gameObject);
            _autoRestartHelper = null;
        }
    }

    // ═══════════════════════════════════════════════════
    // PlayMode 退出时自动清理
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// PlayMode 退出时自动清理（由 OnPlayModeChanged 回调触发）。
    /// 恢复 MarioIsAI/TricksterIsAI/MarioIsTAS = false，Time.timeScale = 1.0f。
    /// </summary>
    private void CleanupAIArena()
    {
        // 恢复 HybridInputProvider 状态
        var hybrid = GetHybridProvider();
        if (hybrid != null)
        {
            hybrid.MarioIsAI = false;
            hybrid.TricksterIsAI = false;
            hybrid.MarioIsTAS = false;
            hybrid.TasProvider = null;
        }

        // S149: 清理 TAS 状态
        _tasEnabled = false;

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

    // ═════════════════════════════════════════════════
    // S149: TAS Data-Driven Testing UI
    // ═════════════════════════════════════════════════

    /// <summary>
    /// 绘制 TAS Data-Driven Testing 区域。
    /// 提供加载录像 JSON、开关 TAS 模式、状态显示。
    /// </summary>
    private void DrawTasSection(HybridInputProvider hybrid)
    {
        EditorGUILayout.Space(8);
        GUI.color = _tasEnabled ? new Color(0.2f, 0.9f, 1f) : new Color(0.7f, 0.7f, 0.7f);
        EditorGUILayout.LabelField("🎬 TAS Data-Driven Testing", EditorStyles.boldLabel);
        GUI.color = Color.white;

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.HelpBox(
            "加载录像 JSON 后，Mario 将用预录制输入替代 AI/人类操作。\n" +
            "配合 Auto Restart 可循环回放收集数据。\n" +
            "优先级：TAS > Bot > Keyboard",
            MessageType.Info);

        // ── Load Replay JSON 按钮 ──
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📁 Load Replay JSON", GUILayout.Height(24)))
        {
            string defaultDir = System.IO.Path.Combine(Application.dataPath, "Tests", "LevelReplays");
            if (!System.IO.Directory.Exists(defaultDir))
                defaultDir = Application.dataPath;

            string path = EditorUtility.OpenFilePanel("选择 TAS 录像 JSON", defaultDir, "json");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    string json = System.IO.File.ReadAllText(path);
                    _tasReplayData = InputRecorder.ImportFromJson(json);

                    if (_tasReplayData != null && _tasReplayData.frames != null && _tasReplayData.frames.Count > 0)
                    {
                        _tasFrameCount = _tasReplayData.frames.Count;
                        _tasLoadedFileName = System.IO.Path.GetFileName(path);

                        // 创建 AutomatedInputProvider 并注入到 HybridInputProvider
                        hybrid.TasProvider = new AutomatedInputProvider(_tasReplayData.frames);

                        Debug.Log($"<color=#00FFFF><b>[TAS] 录像已加载: {_tasLoadedFileName} ({_tasFrameCount} segments)</b></color>");

                        if (!string.IsNullOrEmpty(_tasReplayData.description))
                            Debug.Log($"[TAS] 备注: {_tasReplayData.description}");
                    }
                    else
                    {
                        Debug.LogWarning("[TAS] JSON 解析成功但 frames 为空。确认是 TasReplayData Wrapper 格式而非裸数组。");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[TAS] 加载失败: {ex.Message}");
                }
            }
        }

        // 显示已加载文件名
        if (!string.IsNullOrEmpty(_tasLoadedFileName))
        {
            EditorGUILayout.LabelField($"✔ {_tasLoadedFileName} ({_tasFrameCount} seg)", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField("未加载录像", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndHorizontal();

        // ── TAS 模式 Toggle ──
        EditorGUILayout.Space(2);
        bool canEnableTas = hybrid.TasProvider != null;
        EditorGUI.BeginDisabledGroup(!canEnableTas);
        GUI.color = _tasEnabled ? new Color(0.2f, 1f, 0.8f) : Color.white;
        bool newTasEnabled = EditorGUILayout.Toggle(
            new GUIContent("Mario 使用录像回放 (TAS)",
                canEnableTas ? "开启后 Mario 优先使用 TAS 录像数据" : "请先加载录像 JSON"),
            _tasEnabled);
        GUI.color = Color.white;
        EditorGUI.EndDisabledGroup();

        if (newTasEnabled != _tasEnabled)
        {
            _tasEnabled = newTasEnabled;
            hybrid.MarioIsTAS = _tasEnabled;

            if (_tasEnabled)
            {
                // 开启时重置播放头
                hybrid.ResetTasPlayback();
                Debug.Log("<color=#00FFFF><b>[TAS] Mario TAS 模式已开启。</b></color>");
            }
            else
            {
                Debug.Log("[TAS] Mario TAS 模式已关闭，回退到 Bot/Keyboard。");
            }
        }

        // ── TAS 播放状态指示 ──
        if (_tasEnabled && hybrid.TasProvider != null)
        {
            string status;
            if (hybrid.IsTasPlaying)
            {
                int seg = hybrid.TasProvider.CurrentSegmentIndex;
                status = $"▶ 播放中: segment {seg}/{_tasFrameCount}";
                GUI.color = new Color(0.2f, 1f, 0.8f);
            }
            else
            {
                status = "■ 播放完毕（等待重开或手动重置）";
                GUI.color = Color.yellow;
            }
            EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
            GUI.color = Color.white;
        }

        EditorGUILayout.EndVertical();
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

    /// <summary>
    /// S149: 自动重开时的 TAS 重置回调。
    /// 由 EnableAutoRestart 设置，在 DelayedRestart 中调用。
    /// </summary>
    public System.Action OnBeforeRestart;

    public void Initialize()
    {
        _gm = GameManager.Instance ?? UnityEngine.Object.FindObjectOfType<GameManager>();
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
            // S149: 重开前触发回调（用于 TAS 重置播放头）
            OnBeforeRestart?.Invoke();

            _gm.ResetRound();
            Debug.Log("[AutoRestartHelper] Auto restart triggered.");
        }
    }

    private void OnDestroy()
    {
        Shutdown();
    }
}
