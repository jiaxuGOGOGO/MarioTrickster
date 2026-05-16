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
    // ═══════════════════════════════════════════════════
    private void DrawGameLoopTuningTab()
    {
        EditorGUILayout.LabelField("Game Loop Tuning", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "所有对抗节奏参数统一写入 Assets/Resources/GameplayLoopConfig.asset。PlayMode 下拖动滑块会通过 GameplayMetrics Facade 实时生效；资源缺失时运行时代码仍回退到各组件默认值。",
            MessageType.Info);

        GameplayLoopConfigSO loadedConfig = EnsureGameplayLoopConfigAsset();
        if (loadedConfig != gameplayLoopConfig || gameplayLoopConfigSerialized == null)
        {
            gameplayLoopConfig = loadedConfig;
            gameplayLoopConfigSerialized = gameplayLoopConfig != null ? new SerializedObject(gameplayLoopConfig) : null;
            GameplayMetrics.SetActiveConfig(gameplayLoopConfig);
        }

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Source Asset", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        GameplayLoopConfigSO selectedConfig = (GameplayLoopConfigSO)EditorGUILayout.ObjectField(
            "Gameplay Loop Config", gameplayLoopConfig, typeof(GameplayLoopConfigSO), false);
        if (EditorGUI.EndChangeCheck())
        {
            gameplayLoopConfig = selectedConfig;
            gameplayLoopConfigSerialized = gameplayLoopConfig != null ? new SerializedObject(gameplayLoopConfig) : null;
            GameplayMetrics.SetActiveConfig(gameplayLoopConfig);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Ping Asset", GUILayout.Height(24)) && gameplayLoopConfig != null)
        {
            EditorGUIUtility.PingObject(gameplayLoopConfig);
            Selection.activeObject = gameplayLoopConfig;
        }
        if (GUILayout.Button("Refresh Facade", GUILayout.Height(24)))
        {
            GameplayMetrics.RefreshConfig();
            gameplayLoopConfig = GameplayMetrics.ActiveConfig;
            gameplayLoopConfigSerialized = gameplayLoopConfig != null ? new SerializedObject(gameplayLoopConfig) : null;
            SceneView.RepaintAll();
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        string status = GameplayMetrics.ActiveConfig != null
            ? $"Facade bound: {GameplayMetrics.ActiveConfig.name}"
            : "Facade not bound; runtime will use local fallback defaults.";
        EditorGUILayout.HelpBox(status, GameplayMetrics.ActiveConfig != null ? MessageType.None : MessageType.Warning);
        EditorGUILayout.EndVertical();

        if (gameplayLoopConfigSerialized == null)
        {
            EditorGUILayout.HelpBox("GameplayLoopConfigSO 不存在或未绑定，无法显示滑块。", MessageType.Warning);
            return;
        }

        EditorGUI.BeginChangeCheck();
        gameplayLoopConfigSerialized.Update();

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

        // ═══════════════════════════════════════════════
        // AI 数值诊断顾问
        // ═══════════════════════════════════════════════
        DrawAIDiagnosticSection();
    }

    // ═══════════════════════════════════════════════
    // AI 数值诊断顾问 —— 实现
    // ═══════════════════════════════════════════════

    private string _aiDiagnosticResult;
    private bool _aiDiagnosticLoading;

    private void DrawAIDiagnosticSection()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("🤖 AI 数值诊断顾问", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "基于 AutoTestAnalytics 对局数据 + GameplayLoopConfigSO 当前数值\n" +
            "调用 LLM 分析胜率偏差并给出 3 个具体字段的修改建议。",
            MessageType.Info);

        EditorGUI.BeginDisabledGroup(_aiDiagnosticLoading);
        GUI.color = new Color(0.4f, 0.85f, 1f);
        if (GUILayout.Button(_aiDiagnosticLoading ? "⚙️ 正在分析中..." : "🤖 呼叫 AI 数值诊断", GUILayout.Height(32)))
        {
            RunAIDiagnosticAsync();
        }
        GUI.color = Color.white;
        EditorGUI.EndDisabledGroup();

        // 显示诊断结果
        if (!string.IsNullOrEmpty(_aiDiagnosticResult))
        {
            EditorGUILayout.Space(4);
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
            // ── 1. 提取对局数据 ──
            string matchReport = BuildMatchReportSummary();

            // ── 2. 提取当前 Config 数值 ──
            string configJson = BuildConfigJson();

            // ── 3. 构建 Prompt ──
            string userMessage =
                "【当前对局统计】\n" + matchReport + "\n\n" +
                "【当前 GameplayLoopConfigSO 数值】\n" + configJson + "\n\n" +
                "【任务】\n" +
                "分析当前非对称对抗的胜率偏差，以 50/50 胜率为目标。\n" +
                "结合数据指出是 Trickster 资源溢出还是 Mario 反制成本过高。\n" +
                "直接给出 3 个具体字段的修改建议数值（格式：字段名: 当前值 → 建议值）。\n" +
                "用中文回答，简洁明了，不超过 300 字。";

            // ── 4. 调用 LLM ──
            string result = await CallLLMAsync(userMessage);

            _aiDiagnosticResult = string.IsNullOrEmpty(result)
                ? "⚠️ LLM 返回为空，请检查 API Key 配置。"
                : result;
        }
        catch (Exception ex)
        {
            _aiDiagnosticResult = $"❌ 诊断失败: {ex.Message}";
            Debug.LogError($"[AI Diagnostic] {ex}");
        }
        finally
        {
            _aiDiagnosticLoading = false;
            Repaint();
        }
    }

    /// <summary>
    /// 从 AutoTestAnalytics 提取最新对局报告摘要。
    /// </summary>
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

        // 死亡点统计
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

        // 卡死点统计
        if (_analytics.StuckPoints != null && _analytics.StuckPoints.Count > 0)
            sb.AppendLine($"卡死次数: {_analytics.StuckPoints.Count}");

        return sb.ToString();
    }

    /// <summary>
    /// 将 GameplayLoopConfigSO 核心字段序列化为 JSON 格式字符串。
    /// </summary>
    private string BuildConfigJson()
    {
        if (gameplayLoopConfig == null)
            return "{无 GameplayLoopConfigSO}";

        var c = gameplayLoopConfig;
        var sb = new StringBuilder();
        sb.AppendLine("{");
        // Energy
        sb.AppendLine($"  \"energyMaxEnergy\": {c.energyMaxEnergy},");
        sb.AppendLine($"  \"energyDisguiseCost\": {c.energyDisguiseCost},");
        sb.AppendLine($"  \"energyDisguiseDrainPerSecond\": {c.energyDisguiseDrainPerSecond},");
        sb.AppendLine($"  \"energyBlendedDrainMultiplier\": {c.energyBlendedDrainMultiplier},");
        sb.AppendLine($"  \"energyControlCost\": {c.energyControlCost},");
        sb.AppendLine($"  \"energyRegenPerSecond\": {c.energyRegenPerSecond},");
        sb.AppendLine($"  \"energyDisguisedRegenMultiplier\": {c.energyDisguisedRegenMultiplier},");
        sb.AppendLine($"  \"energyRegenDelayAfterControl\": {c.energyRegenDelayAfterControl},");
        // Scan
        sb.AppendLine($"  \"scanRadius\": {c.scanRadius},");
        sb.AppendLine($"  \"scanCooldown\": {c.scanCooldown},");
        sb.AppendLine($"  \"scanRevealDuration\": {c.scanRevealDuration},");
        // Heat
        sb.AppendLine($"  \"heatPerActivation\": {c.heatPerActivation},");
        sb.AppendLine($"  \"heatDecayPerSecond\": {c.heatDecayPerSecond},");
        sb.AppendLine($"  \"heatLockdownThreshold\": {c.heatLockdownThreshold},");
        sb.AppendLine($"  \"heatLockdownCooldown\": {c.heatLockdownCooldown},");
        // Possession
        sb.AppendLine($"  \"possessionRevealDuration\": {c.possessionRevealDuration},");
        sb.AppendLine($"  \"possessionEscapeDuration\": {c.possessionEscapeDuration},");
        // Combo
        sb.AppendLine($"  \"comboWindow\": {c.comboWindow},");
        // Compensation
        sb.AppendLine($"  \"compensationPropActivateSuspicionBonus\": {c.compensationPropActivateSuspicionBonus}");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// 复用 LevelAutoHealer 的 API 配置解析逻辑，调用 OpenAI ChatCompletion。
    /// </summary>
    private static async Task<string> CallLLMAsync(string userMessage)
    {
        // ── 解析 API 配置（复用 LevelAutoHealer 的 EditorPrefs + 环境变量）──
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

        // ── 构建请求体 ──
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

        // ── 发送请求 ──
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

    /// <summary>
    /// 从 ChatCompletion JSON 响应中提取 message.content。
    /// </summary>
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

    /// <summary>
    /// 简单 JSON 字符串转义。
    /// </summary>
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

    private void DrawGameplayLoopConfigSection(string title, string[] propertyNames)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

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
