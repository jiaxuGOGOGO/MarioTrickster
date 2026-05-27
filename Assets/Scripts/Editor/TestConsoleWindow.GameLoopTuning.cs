using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

public partial class TestConsoleWindow
{
    // ═══════════════════════════════════════════════════
    // Tab 5: Game Loop Tuning (对抗节奏实时调参)
    //
    // v2 清爽化重构要点：
    //   - 顶部 HelpBox → CompactTip（一行说明）
    //   - Source Asset 区块紧凑化（去掉多余 HelpBox）
    //   - 参数分组改为可折叠（默认折叠，按需展开）
    //   - AI 诊断区紧凑化
    //   - 所有功能 100% 保留
    // ═══════════════════════════════════════════════════

    // 参数分组折叠状态
    private Dictionary<string, bool> _loopSectionFoldouts = new Dictionary<string, bool>();

    private void DrawGameLoopTuningTab()
    {
        LevelStudioStyles.CompactTip("所有参数写入 GameplayLoopConfig.asset | PlayMode 下滑块实时生效 | 缺失时回退默认值");

        GameplayLoopConfigSO loadedConfig = EnsureGameplayLoopConfigAsset();
        if (loadedConfig != gameplayLoopConfig || gameplayLoopConfigSerialized == null)
        {
            gameplayLoopConfig = loadedConfig;
            gameplayLoopConfigSerialized = gameplayLoopConfig != null ? new SerializedObject(gameplayLoopConfig) : null;
            GameplayMetrics.SetActiveConfig(gameplayLoopConfig);
        }

        // ── Source Asset（紧凑化） ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Config:", GUILayout.Width(45));
        EditorGUI.BeginChangeCheck();
        GameplayLoopConfigSO selectedConfig = (GameplayLoopConfigSO)EditorGUILayout.ObjectField(
            gameplayLoopConfig, typeof(GameplayLoopConfigSO), false);
        if (EditorGUI.EndChangeCheck())
        {
            gameplayLoopConfig = selectedConfig;
            gameplayLoopConfigSerialized = gameplayLoopConfig != null ? new SerializedObject(gameplayLoopConfig) : null;
            GameplayMetrics.SetActiveConfig(gameplayLoopConfig);
        }
        if (GUILayout.Button("Ping", GUILayout.Width(38), GUILayout.Height(18)) && gameplayLoopConfig != null)
        {
            EditorGUIUtility.PingObject(gameplayLoopConfig);
            Selection.activeObject = gameplayLoopConfig;
        }
        if (GUILayout.Button("Refresh", GUILayout.Width(55), GUILayout.Height(18)))
        {
            GameplayMetrics.RefreshConfig();
            gameplayLoopConfig = GameplayMetrics.ActiveConfig;
            gameplayLoopConfigSerialized = gameplayLoopConfig != null ? new SerializedObject(gameplayLoopConfig) : null;
            SceneView.RepaintAll();
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        // Facade 状态（紧凑单行）
        if (GameplayMetrics.ActiveConfig != null)
        {
            EditorGUILayout.LabelField($"Facade: {GameplayMetrics.ActiveConfig.name}", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField("Facade not bound (runtime uses fallback defaults)", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();

        if (gameplayLoopConfigSerialized == null)
        {
            LevelStudioStyles.CompactTip("GameplayLoopConfigSO 不存在或未绑定，无法显示滑块");
            return;
        }

        EditorGUI.BeginChangeCheck();
        gameplayLoopConfigSerialized.Update();

        // ── 参数分组（可折叠，默认折叠以减少信息过载） ──
        DrawGameplayLoopConfigSection("Energy System", new[]
        {
            "energyMaxEnergy", "energyStartEnergy", "energyDisguiseCost", "energyDisguiseDrainPerSecond",
            "energyBlendedDrainMultiplier", "energyControlCost", "energyRegenPerSecond",
            "energyDisguisedRegenMultiplier", "energyRegenDelayAfterControl", "energyLowEnergyThreshold"
        });

        DrawGameplayLoopConfigSection("Scan Ability", new[]
        {
            "scanRadius", "scanCooldown", "scanRevealDuration", "scanRevealGateBonusDuration",
            "scanPulseSpeed", "scanPulseLineWidth", "scanFlashFrequency", "scanRevealColor"
        });

        DrawGameplayLoopConfigSection("Trickster Possession Gate", new[]
        {
            "possessionRevealDuration", "possessionEscapeDuration"
        });

        DrawGameplayLoopConfigSection("Alarm Crisis Director", new[]
        {
            "alarmWarningDuration", "alarmScanSpeed", "alarmScanWidth", "alarmEvidenceAmplifyFactor",
            "alarmScanSuspicionBonus", "alarmTriggerTier", "alarmScanCooldown", "alarmLockdownForcesScan"
        });

        DrawGameplayLoopConfigSection("Route Budget Service", new[]
        {
            "routeAutoRecoveryTime", "routeMaxSimultaneousDegraded"
        });

        DrawGameplayLoopConfigSection("Trickster Heat Meter", new[]
        {
            "heatPerPossession", "heatPerActivation", "heatComboHeatFactor", "heatComboBreakHeatPerChain",
            "heatDecayPerSecond", "heatLockdownFallbackHeat", "heatLockdownCooldown", "heatToDecaySlowdown",
            "heatSuspiciousThreshold", "heatAlertThreshold", "heatLockdownThreshold"
        });

        DrawGameplayLoopConfigSection("Prop Combo Tracker", new[]
        {
            "comboWindow", "comboDifferentAnchorMultiplier", "comboDifferentPropTypeMultiplier",
            "comboSameAnchorMultiplier", "comboSamePropMultiplier", "comboSameAnchorSuspicionBonus", "comboBreakCooldown"
        });

        DrawGameplayLoopConfigSection("Interference Compensation Policy", new[]
        {
            "compensationRouteDegradeResidueBonus", "compensationRouteDegradeEvidenceBonus",
            "compensationPropActivateSuspicionBonus", "compensationProgressBoostDuration", "compensationProgressBoostMultiplier"
        });

        bool changed = gameplayLoopConfigSerialized.ApplyModifiedProperties();
        if (EditorGUI.EndChangeCheck() || changed)
        {
            EditorUtility.SetDirty(gameplayLoopConfig);
            GameplayMetrics.SetActiveConfig(gameplayLoopConfig);
            SceneView.RepaintAll();
            if (EditorApplication.isPlaying)
            {
                Repaint();
            }
        }

        // ── AI 数值诊断顾问 ──
        DrawAIDiagnosticSection();
    }

    // ═══════════════════════════════════════════════
    // AI 数值诊断顾问
    // ═══════════════════════════════════════════════

    private string _aiDiagnosticResult;
    private bool _aiDiagnosticLoading;

    private void DrawAIDiagnosticSection()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.BeginVertical("box");
        LevelStudioStyles.SubHeader("AI Balance Advisor");
        LevelStudioStyles.CompactTip("基于 AutoTestAnalytics 对局数据 + Config 当前值，调用 LLM 分析胜率偏差");

        EditorGUI.BeginDisabledGroup(_aiDiagnosticLoading);
        if (LevelStudioStyles.ColorButton(
            _aiDiagnosticLoading ? "Analyzing..." : "Run AI Diagnostic",
            LevelStudioStyles.AccentBlue, 26f))
        {
            RunAIDiagnosticAsync();
        }
        EditorGUI.EndDisabledGroup();

        if (!string.IsNullOrEmpty(_aiDiagnosticResult))
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox(_aiDiagnosticResult, MessageType.Warning);
        }

        EditorGUILayout.EndVertical();
    }

    private async void RunAIDiagnosticAsync()
    {
        _aiDiagnosticLoading = true;
        _aiDiagnosticResult = null;
        Repaint();

        try
        {
            string matchReport = BuildMatchReportSummary();
            string configJson = BuildConfigJson();

            string userMessage =
                "【当前对局统计】\n" + matchReport + "\n\n" +
                "【当前 GameplayLoopConfigSO 数值】\n" + configJson + "\n\n" +
                "【任务】\n" +
                "分析当前非对称对抗的胜率偏差，以 50/50 胜率为目标。\n" +
                "结合数据指出是 Trickster 资源溢出还是 Mario 反制成本过高。\n" +
                "直接给出 3 个具体字段的修改建议数值（格式：字段名: 当前值 → 建议值）。\n" +
                "用中文回答，简洁明了，不超过 300 字。";

            string result = await CallLLMAsync(userMessage);

            _aiDiagnosticResult = string.IsNullOrEmpty(result)
                ? "LLM 返回为空，请检查 API Key 配置。"
                : result;
        }
        catch (Exception ex)
        {
            _aiDiagnosticResult = $"诊断失败: {ex.Message}";
            Debug.LogError($"[AI Diagnostic] {ex}");
        }
        finally
        {
            _aiDiagnosticLoading = false;
            Repaint();
        }
    }

    private string BuildMatchReportSummary()
    {
        if (_analytics == null || _analytics.TotalMatches == 0)
        {
            return "无对局数据（请先在 AI Auto-Arena 中 Start Collecting 并跑几局）";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"总对战局数: {_analytics.TotalMatches}");
        sb.AppendLine($"Mario 胜率: {_analytics.MarioWinRate:F1}% ({_analytics.MarioWins}局)");
        sb.AppendLine($"Trickster 胜率: {_analytics.TricksterWinRate:F1}% ({_analytics.TricksterWins}局)");
        sb.AppendLine($"平均单局耗时: {_analytics.AverageMatchTime:F1}s");

        if (_analytics.DeathPoints != null && _analytics.DeathPoints.Count > 0)
        {
            var causeCounts = new Dictionary<DeathCause, int>();
            foreach (var dp in _analytics.DeathPoints)
            {
                if (!causeCounts.ContainsKey(dp.cause)) causeCounts[dp.cause] = 0;
                causeCounts[dp.cause]++;
            }
            sb.AppendLine($"总死亡次数: {_analytics.DeathPoints.Count}");
            foreach (var kv in causeCounts)
            {
                string causeName = kv.Key == DeathCause.FallOffCliff ? "坠崖" : "机关击杀";
                sb.AppendLine($"  死因 [{causeName}]: {kv.Value}次");
            }
        }

        if (_analytics.StuckPoints != null && _analytics.StuckPoints.Count > 0)
            sb.AppendLine($"卡死次数: {_analytics.StuckPoints.Count}");

        return sb.ToString();
    }

    private string BuildConfigJson()
    {
        if (gameplayLoopConfig == null)
            return "{无 GameplayLoopConfigSO}";

        var c = gameplayLoopConfig;
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"energyMaxEnergy\": {c.energyMaxEnergy},");
        sb.AppendLine($"  \"energyDisguiseCost\": {c.energyDisguiseCost},");
        sb.AppendLine($"  \"energyDisguiseDrainPerSecond\": {c.energyDisguiseDrainPerSecond},");
        sb.AppendLine($"  \"energyBlendedDrainMultiplier\": {c.energyBlendedDrainMultiplier},");
        sb.AppendLine($"  \"energyControlCost\": {c.energyControlCost},");
        sb.AppendLine($"  \"energyRegenPerSecond\": {c.energyRegenPerSecond},");
        sb.AppendLine($"  \"energyDisguisedRegenMultiplier\": {c.energyDisguisedRegenMultiplier},");
        sb.AppendLine($"  \"energyRegenDelayAfterControl\": {c.energyRegenDelayAfterControl},");
        sb.AppendLine($"  \"scanRadius\": {c.scanRadius},");
        sb.AppendLine($"  \"scanCooldown\": {c.scanCooldown},");
        sb.AppendLine($"  \"scanRevealDuration\": {c.scanRevealDuration},");
        sb.AppendLine($"  \"heatPerActivation\": {c.heatPerActivation},");
        sb.AppendLine($"  \"heatDecayPerSecond\": {c.heatDecayPerSecond},");
        sb.AppendLine($"  \"heatLockdownThreshold\": {c.heatLockdownThreshold},");
        sb.AppendLine($"  \"heatLockdownCooldown\": {c.heatLockdownCooldown},");
        sb.AppendLine($"  \"possessionRevealDuration\": {c.possessionRevealDuration},");
        sb.AppendLine($"  \"possessionEscapeDuration\": {c.possessionEscapeDuration},");
        sb.AppendLine($"  \"comboWindow\": {c.comboWindow},");
        sb.AppendLine($"  \"compensationPropActivateSuspicionBonus\": {c.compensationPropActivateSuspicionBonus}");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static async Task<string> CallLLMAsync(string userMessage)
    {
        string apiKey = EditorPrefs.GetString("AI_SmartSlicer_APIKey", "");
        if (string.IsNullOrEmpty(apiKey))
            apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";

        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException(
                "未配置 API Key！请在 AI Smart Slicer 窗口或环境变量 OPENAI_API_KEY 中设置。");

        string baseUrl = EditorPrefs.GetString("AI_SmartSlicer_BaseUrl", "");
        if (string.IsNullOrEmpty(baseUrl))
            baseUrl = "https://api.openai.com/v1";

        string model = EditorPrefs.GetString("AI_SmartSlicer_Model", "");
        if (string.IsNullOrEmpty(model))
            model = "gpt-4o-mini";

        string systemPrompt =
            "你是一个 2D 非对称对抗游戏的数值平衡顾问。" +
            "游戏中 Mario(进攻方)需要完成关卡目标，Trickster(干扰方)通过伪装成场景元素并操控机关来击杀 Mario。" +
            "你的目标是分析胜率偏差并给出具体的数值调整建议。";

        string escapedSystem = EscapeJsonStr(systemPrompt);
        string escapedUser = EscapeJsonStr(userMessage);

        string requestBody = $@"{{
  ""model"": ""{EscapeJsonStr(model)}"",
  ""messages"": [
    {{""role"": ""system"", ""content"": ""{escapedSystem}""}},
    {{""role"": ""user"", ""content"": ""{escapedUser}""}}
  ],
  ""temperature"": 0.7,
  ""max_tokens"": 600
}}";

        using (var client = new HttpClient())
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var httpContent = new StringContent(requestBody, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{baseUrl}/chat/completions", httpContent);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new Exception($"API Error {response.StatusCode}: {error}");
            }

            string responseJson = await response.Content.ReadAsStringAsync();
            return ExtractLLMContent(responseJson);
        }
    }

    private static string ExtractLLMContent(string responseJson)
    {
        int msgIdx = responseJson.IndexOf("\"message\"");
        if (msgIdx < 0) return null;

        int contentIdx = responseJson.IndexOf("\"content\"", msgIdx);
        if (contentIdx < 0) return null;

        int colonIdx = responseJson.IndexOf(':', contentIdx);
        if (colonIdx < 0) return null;

        int start = colonIdx + 1;
        while (start < responseJson.Length && responseJson[start] == ' ') start++;

        if (start >= responseJson.Length || responseJson[start] != '"') return null;

        var sb = new StringBuilder();
        int i = start + 1;
        while (i < responseJson.Length)
        {
            if (responseJson[i] == '\\' && i + 1 < responseJson.Length)
            {
                char next = responseJson[i + 1];
                switch (next)
                {
                    case '"':  sb.Append('"');  i += 2; break;
                    case '\\': sb.Append('\\'); i += 2; break;
                    case 'n':  sb.Append('\n'); i += 2; break;
                    case 'r':  sb.Append('\r'); i += 2; break;
                    case 't':  sb.Append('\t'); i += 2; break;
                    default:   sb.Append(next); i += 2; break;
                }
            }
            else if (responseJson[i] == '"')
            {
                break;
            }
            else
            {
                sb.Append(responseJson[i]);
                i++;
            }
        }
        return sb.ToString();
    }

    private static string EscapeJsonStr(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private GameplayLoopConfigSO EnsureGameplayLoopConfigAsset()
    {
        GameplayLoopConfigSO config = GameplayMetrics.ActiveConfig;
        if (config != null)
        {
            return config;
        }

        const string resourcesPath = "Assets/Resources";
        const string assetPath = "Assets/Resources/GameplayLoopConfig.asset";

        config = AssetDatabase.LoadAssetAtPath<GameplayLoopConfigSO>(assetPath);
        if (config != null)
        {
            GameplayMetrics.SetActiveConfig(config);
            return config;
        }

        if (!AssetDatabase.IsValidFolder(resourcesPath))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        config = CreateInstance<GameplayLoopConfigSO>();
        AssetDatabase.CreateAsset(config, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        GameplayMetrics.SetActiveConfig(config);
        return config;
    }

    /// <summary>绘制参数分组（v2: 可折叠，减少信息过载）</summary>
    private void DrawGameplayLoopConfigSection(string title, string[] propertyNames)
    {
        EditorGUILayout.Space(2);

        if (!_loopSectionFoldouts.ContainsKey(title))
            _loopSectionFoldouts[title] = false;

        _loopSectionFoldouts[title] = EditorGUILayout.Foldout(
            _loopSectionFoldouts[title],
            $"{title} ({propertyNames.Length})", true, EditorStyles.foldoutHeader);

        if (!_loopSectionFoldouts[title]) return;

        EditorGUILayout.BeginVertical("box");
        for (int i = 0; i < propertyNames.Length; i++)
        {
            SerializedProperty property = gameplayLoopConfigSerialized.FindProperty(propertyNames[i]);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, true);
            }
            else
            {
                EditorGUILayout.HelpBox($"Missing property: {propertyNames[i]}", MessageType.Warning);
            }
        }
        EditorGUILayout.EndVertical();
    }
}
