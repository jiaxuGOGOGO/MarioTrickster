using UnityEngine;
using UnityEditor;
using System;
using System.Collections;

// ═══════════════════════════════════════════════════════════════════
// TestConsoleWindow.AIArena — AI 自动挂机角斗场 (S60 升级版)
//
// v2 清爽化重构要点：
//   - HelpBox → CompactTip（节省垂直空间）
//   - Toggle 和状态指示合并为紧凑行
//   - TAS 区域紧凑化
//   - Live Stats 改为紧凑单行
//   - 所有功能 100% 保留
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
    // 获取当前 HybridInputProvider
    // ═══════════════════════════════════════════════════

    private HybridInputProvider GetHybridProvider()
    {
        EnsureCache();
        if (cachedInputManager == null) return null;
        return cachedInputManager.GetCurrentProvider() as HybridInputProvider;
    }

    // ═══════════════════════════════════════════════════
    // 绘制 AI Arena 折叠栏
    // ═══════════════════════════════════════════════════

    private void DrawAIArenaSection()
    {
        EditorGUILayout.Space(4);

        var hybrid = GetHybridProvider();
        bool anyAI = hybrid != null && (hybrid.MarioIsAI || hybrid.TricksterIsAI);

        // 折叠栏标题
        GUI.color = anyAI ? new Color(0.3f, 1f, 0.5f) : Color.white;
        showAIArena = EditorGUILayout.Foldout(showAIArena,
            "AI Auto-Arena" + (anyAI ? " [ACTIVE]" : ""),
            true, EditorStyles.foldoutHeader);
        GUI.color = Color.white;

        if (!showAIArena) return;

        EditorGUILayout.BeginVertical("box");

        LevelStudioStyles.CompactTip("F1/F2 切换人机 | 关闭时自动恢复 TimeScale=1");

        if (hybrid == null)
        {
            LevelStudioStyles.CompactTip("当前 InputProvider 非 Hybrid，无法控制人机切换");
            EditorGUILayout.EndVertical();
            return;
        }

        // ── Mario/Trickster 托管 Toggle（紧凑双列） ──
        EditorGUILayout.BeginHorizontal();

        GUI.color = hybrid.MarioIsAI ? new Color(0.4f, 0.9f, 1f) : Color.white;
        bool newMarioAI = EditorGUILayout.Toggle(
            new GUIContent("Mario (F1)", "切换 Mario AI 控制"),
            hybrid.MarioIsAI);
        GUI.color = Color.white;

        if (newMarioAI != hybrid.MarioIsAI)
        {
            hybrid.MarioIsAI = newMarioAI;
            Debug.Log($"<color=#00FF88><b>[输入流] Mario → {(newMarioAI ? "AI" : "Human")} (Editor)</b></color>");
        }

        GUI.color = hybrid.TricksterIsAI ? new Color(1f, 0.6f, 0.2f) : Color.white;
        bool newTricksterAI = EditorGUILayout.Toggle(
            new GUIContent("Trickster (F2)", "切换 Trickster AI 控制"),
            hybrid.TricksterIsAI);
        GUI.color = Color.white;

        if (newTricksterAI != hybrid.TricksterIsAI)
        {
            hybrid.TricksterIsAI = newTricksterAI;
            Debug.Log($"<color=#FF8800><b>[输入流] Trickster → {(newTricksterAI ? "AI" : "Human")} (Editor)</b></color>");
        }

        EditorGUILayout.EndHorizontal();

        // ── Time Scale + Auto Restart（紧凑单行） ──
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Speed", GUILayout.Width(38));
        float newArenaTimeScale = EditorGUILayout.Slider(arenaTimeScale, 1.0f, 5.0f);
        if (!Mathf.Approximately(newArenaTimeScale, arenaTimeScale))
        {
            arenaTimeScale = newArenaTimeScale;
            Time.timeScale = arenaTimeScale;
        }
        if (GUILayout.Button("1x", GUILayout.Width(25))) { arenaTimeScale = 1f; Time.timeScale = 1f; }
        if (GUILayout.Button("3x", GUILayout.Width(25))) { arenaTimeScale = 3f; Time.timeScale = 3f; }
        if (GUILayout.Button("5x", GUILayout.Width(25))) { arenaTimeScale = 5f; Time.timeScale = 5f; }
        EditorGUILayout.EndHorizontal();

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

        // ── TAS Section（紧凑化） ──
        DrawTasSection(hybrid);

        // ── Analytics 控制（紧凑化） ──
        LevelStudioStyles.Separator();
        EditorGUILayout.BeginHorizontal();
        LevelStudioStyles.SubHeader("Data");
        GUILayout.FlexibleSpace();

        bool analyticsActive = _analytics != null;
        if (!analyticsActive)
        {
            if (GUILayout.Button("Start Collecting", GUILayout.Height(20)))
            {
                if (_tasEnabled && hybrid.TasProvider != null)
                {
                    hybrid.ResetTasPlayback();
                }
                _analytics = new AutoTestAnalytics();
                EnsureCache();
                if (cachedGameManager != null)
                    _analytics.StartCollecting(cachedGameManager);
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
            if (GUILayout.Button("Stop", GUILayout.Height(20), GUILayout.Width(45)))
            {
                _analytics.StopCollecting();
                Debug.Log("[AI Arena] Analytics stopped.");
            }
        }
        EditorGUILayout.EndHorizontal();

        // ── Heatmap Toggle ──
        bool currentHeatmap = AnalyticsGizmoRenderer.ShowTestHeatmap;
        GUI.color = currentHeatmap ? new Color(1f, 0.5f, 0.3f) : Color.white;
        bool newHeatmap = EditorGUILayout.Toggle(
            new GUIContent("Test Heatmap", "Scene 视图中显示死亡热力图和卡死警告"),
            currentHeatmap);
        GUI.color = Color.white;

        if (newHeatmap != currentHeatmap)
        {
            AnalyticsGizmoRenderer.ShowTestHeatmap = newHeatmap;
            SceneView.RepaintAll();
        }

        // ── Live Stats（紧凑化） ──
        if (_analytics != null && _analytics.TotalMatches > 0)
        {
            EditorGUILayout.LabelField(
                $"Matches: {_analytics.TotalMatches} | M:{_analytics.MarioWins}({_analytics.MarioWinRate:F0}%) T:{_analytics.TricksterWins}({_analytics.TricksterWinRate:F0}%) | Avg: {_analytics.AverageMatchTime:F1}s",
                EditorStyles.miniLabel);
        }

        // ── Report + Reset（紧凑化） ──
        EditorGUILayout.BeginHorizontal();
        if (LevelStudioStyles.ColorButton("Export Report", LevelStudioStyles.AccentYellow, 22f))
        {
            if (_analytics != null)
            {
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
                    Debug.Log($"<color=#88FF88><b>[AI Arena] 战报已导出。</b></color> <a href=\"{mdUrl}\">打开</a>\n{mdPath}");
                }
            }
            else
            {
                Debug.LogWarning("[AI Arena] No analytics data. Click 'Start Collecting' first.");
            }
        }
        if (GUILayout.Button("Reset", GUILayout.Height(22), GUILayout.Width(50)))
        {
            _analytics?.Reset();
            Debug.Log("[AI Arena] Stats reset.");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // ═══════════════════════════════════════════════════
    // Auto Restart 控制
    // ═══════════════════════════════════════════════════

    private void EnableAutoRestart()
    {
        if (_autoRestartHelper != null) return;

        var go = new GameObject("[AI Arena] AutoRestart Helper");
        go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        _autoRestartHelper = go.AddComponent<AutoRestartHelper>();
        _autoRestartHelper.Initialize();

        _autoRestartHelper.OnBeforeRestart = () =>
        {
            var h = GetHybridProvider();
            if (h != null && h.MarioIsTAS && h.TasProvider != null)
            {
                h.ResetTasPlayback();
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

    private void CleanupAIArena()
    {
        var hybrid = GetHybridProvider();
        if (hybrid != null)
        {
            hybrid.MarioIsAI = false;
            hybrid.TricksterIsAI = false;
            hybrid.MarioIsTAS = false;
            hybrid.TasProvider = null;
        }

        _tasEnabled = false;
        _analytics?.StopCollecting();
        _analytics = null;
        DisableAutoRestart();
        autoRestartEnabled = false;
        Time.timeScale = 1.0f;
        arenaTimeScale = 1.0f;
        timeScaleValue = 1.0f;
    }

    // ═════════════════════════════════════════════════
    // S149: TAS Data-Driven Testing UI（紧凑化）
    // ═════════════════════════════════════════════════

    private void DrawTasSection(HybridInputProvider hybrid)
    {
        LevelStudioStyles.Separator();
        LevelStudioStyles.SubHeader("TAS Replay");
        LevelStudioStyles.CompactTip("加载录像 JSON → Mario 用预录制输入 | 优先级: TAS > Bot > Keyboard");

        // ── Load + 文件名（紧凑单行） ──
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Load JSON", GUILayout.Height(20), GUILayout.Width(80)))
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
                        hybrid.TasProvider = new AutomatedInputProvider(_tasReplayData.frames);
                        Debug.Log($"<color=#00FFFF><b>[TAS] 已加载: {_tasLoadedFileName} ({_tasFrameCount} seg)</b></color>");

                        if (!string.IsNullOrEmpty(_tasReplayData.description))
                            Debug.Log($"[TAS] 备注: {_tasReplayData.description}");
                    }
                    else
                    {
                        Debug.LogWarning("[TAS] frames 为空，确认格式为 TasReplayData Wrapper。");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[TAS] 加载失败: {ex.Message}");
                }
            }
        }

        if (!string.IsNullOrEmpty(_tasLoadedFileName))
            EditorGUILayout.LabelField($"{_tasLoadedFileName} ({_tasFrameCount})", EditorStyles.miniLabel);
        else
            EditorGUILayout.LabelField("未加载", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        // ── TAS Toggle + 状态（紧凑单行） ──
        bool canEnableTas = hybrid.TasProvider != null;
        EditorGUI.BeginDisabledGroup(!canEnableTas);
        EditorGUILayout.BeginHorizontal();
        GUI.color = _tasEnabled ? new Color(0.2f, 1f, 0.8f) : Color.white;
        bool newTasEnabled = EditorGUILayout.Toggle(
            new GUIContent("Mario TAS", canEnableTas ? "开启 TAS 录像回放" : "请先加载录像"),
            _tasEnabled);
        GUI.color = Color.white;

        if (_tasEnabled && hybrid.TasProvider != null)
        {
            if (hybrid.IsTasPlaying)
            {
                int seg = hybrid.TasProvider.CurrentSegmentIndex;
                EditorGUILayout.LabelField($"seg {seg}/{_tasFrameCount}", EditorStyles.miniLabel, GUILayout.Width(80));
            }
            else
            {
                EditorGUILayout.LabelField("Done", EditorStyles.miniLabel, GUILayout.Width(35));
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();

        if (newTasEnabled != _tasEnabled)
        {
            _tasEnabled = newTasEnabled;
            hybrid.MarioIsTAS = _tasEnabled;

            if (_tasEnabled)
            {
                hybrid.ResetTasPlayback();
                Debug.Log("<color=#00FFFF><b>[TAS] Mario TAS 模式已开启。</b></color>");
            }
            else
            {
                Debug.Log("[TAS] Mario TAS 模式已关闭。");
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════
// AutoRestartHelper — 隐形协程脚本
// ═══════════════════════════════════════════════════════════════════

public class AutoRestartHelper : MonoBehaviour
{
    private GameManager _gm;
    private bool _initialized;

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
        yield return new WaitForSecondsRealtime(1.0f);

        if (_gm != null)
        {
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
