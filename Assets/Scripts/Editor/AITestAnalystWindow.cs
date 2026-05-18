#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// AI Test Analyst Window — LLM 驱动的测试日志分析工具
///
/// 功能：
///   - 连接 OpenAI 兼容 API（支持自定义 Base URL / Model）
///   - 选择项目内测试日志文件并发送给 LLM 进行智能分析
///   - 选择 AI Arena 战报 JSON 并通过专业 Prompt 进行深度诊断
///   - 在 Editor 窗口内直接展示分析结果
///
/// 配置通过 EditorPrefs 持久化，无需每次重填。
/// 仅依赖 Unity 原生库（UnityEngine.Networking / System.Threading.Tasks），零第三方依赖。
/// </summary>
public class AITestAnalystWindow : EditorWindow
{
    // ═══════════════════════════════════════════════════
    // Serializable Data Classes for JsonUtility
    // ═══════════════════════════════════════════════════

    [Serializable]
    private class OpenAIMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    private class OpenAIRequest
    {
        public string model;
        public List<OpenAIMessage> messages;
        public float temperature;
    }

    [Serializable]
    private class OpenAIChoice
    {
        public OpenAIMessage message;
    }

    [Serializable]
    private class OpenAIResponse
    {
        public List<OpenAIChoice> choices;
    }

    // ═══════════════════════════════════════════════════
    // 硬编码 System Prompt — 战报诊断专用
    // ═══════════════════════════════════════════════════
    private const string BATTLE_REPORT_SYSTEM_PROMPT =
        "你是一名资深的游戏关卡设计师与 QA 专家。你正在分析《MarioTrickster》的双人非对称对抗游戏战报。\n" +
        "游戏基于 ASCII 生成关卡（'='是平台，'^'是地刺，'['是封路门）。物理红线：最大跳跃距离约4.5格，高度约2.5格。\n" +
        "请阅读以下包含玩家画像(Persona)、参数快照(ConfigSnapshot)、卡死点(StuckPoints)和死亡点(DeathPoints)的 JSON 遥测数据。\n" +
        "请用中文输出一份专业的 Markdown 诊断报告，必须包含：\n" +
        "1. 【对局评价】：结合双方 Persona 风格，评估节奏和胜率是否符合预期。\n" +
        "2. 【致命病灶分析】：找出高频死亡/卡死坐标。结合 recentInteractions 日志分析死因（如：是距离太远跳不过去，还是预警太短没反应过来？）。\n" +
        "3. 【Actionable 修改建议】：给出具体的 ASCII 关卡坐标修改建议（增加平台或垫脚石），或者给出 GameplayLoopConfigSO 的具体参数调整建议。";

    // ═══════════════════════════════════════════════════
    // EditorPrefs Keys
    // ═══════════════════════════════════════════════════
    private const string PREF_API_KEY = "AITestAnalyst_ApiKey";
    private const string PREF_BASE_URL = "AITestAnalyst_BaseUrl";
    private const string PREF_MODEL_NAME = "AITestAnalyst_ModelName";
    private const string PREF_SEARCH_DIR = "AITestAnalyst_SearchDir";

    // ═══════════════════════════════════════════════════
    // GUI 状态变量
    // ═══════════════════════════════════════════════════
    private string apiKey = "";
    private string baseUrl = "https://api.openai.com/v1/chat/completions";
    private string modelName = "gpt-4o";
    private string searchDirectory = "Assets";

    // 文件选择
    private List<string> availableFiles = new List<string>();
    private string[] availableFileNames = Array.Empty<string>();
    private int selectedFileIndex = 0;

    // 分析结果
    private Vector2 scrollPosition;
    private string analysisResult = "";
    private bool isRequesting = false;

    // ═══════════════════════════════════════════════════
    // 战报 JSON 选择
    // ═══════════════════════════════════════════════════
    private List<string> reportFiles = new List<string>();
    private string[] reportFileNames = Array.Empty<string>();
    private int selectedReportIndex = 0;
    private string reportContent = "";

    // ═══════════════════════════════════════════════════
    // MenuItem 入口
    // ═══════════════════════════════════════════════════
    [MenuItem("MarioTrickster/AI Arena/AI Test Analyst (LLM)")]
    public static void ShowWindow()
    {
        var window = GetWindow<AITestAnalystWindow>("AI Test Analyst");
        window.minSize = new Vector2(480, 360);
        window.Show();
    }

    // ═══════════════════════════════════════════════════
    // 生命周期
    // ═══════════════════════════════════════════════════
    private void OnEnable()
    {
        LoadPrefs();
        RefreshFileList();
        RefreshReportList();
    }

    private void OnDisable()
    {
        SavePrefs();
    }

    // ═══════════════════════════════════════════════════
    // EditorPrefs 读写
    // ═══════════════════════════════════════════════════
    private void LoadPrefs()
    {
        apiKey = EditorPrefs.GetString(PREF_API_KEY, "");
        baseUrl = EditorPrefs.GetString(PREF_BASE_URL, "https://api.openai.com/v1/chat/completions");
        modelName = EditorPrefs.GetString(PREF_MODEL_NAME, "gpt-4o");
        searchDirectory = EditorPrefs.GetString(PREF_SEARCH_DIR, "Assets");
    }

    private void SavePrefs()
    {
        EditorPrefs.SetString(PREF_API_KEY, apiKey);
        EditorPrefs.SetString(PREF_BASE_URL, baseUrl);
        EditorPrefs.SetString(PREF_MODEL_NAME, modelName);
        EditorPrefs.SetString(PREF_SEARCH_DIR, searchDirectory);
    }

    // ═══════════════════════════════════════════════════
    // 文件扫描
    // ═══════════════════════════════════════════════════
    private void RefreshFileList()
    {
        availableFiles.Clear();

        string fullPath = Path.GetFullPath(searchDirectory);
        if (Directory.Exists(fullPath))
        {
            string[] txtFiles = Directory.GetFiles(fullPath, "*.txt", SearchOption.AllDirectories);
            string[] logFiles = Directory.GetFiles(fullPath, "*.log", SearchOption.AllDirectories);
            string[] jsonFiles = Directory.GetFiles(fullPath, "*.json", SearchOption.AllDirectories);
            string[] mdFiles = Directory.GetFiles(fullPath, "*.md", SearchOption.AllDirectories);

            availableFiles.AddRange(txtFiles);
            availableFiles.AddRange(logFiles);
            availableFiles.AddRange(jsonFiles);
            availableFiles.AddRange(mdFiles);
        }

        // 生成显示用的短名称
        availableFileNames = new string[availableFiles.Count];
        for (int i = 0; i < availableFiles.Count; i++)
        {
            availableFileNames[i] = availableFiles[i].Replace("\\", "/");
            // 尝试转为相对路径显示
            if (availableFileNames[i].Contains(searchDirectory))
            {
                int idx = availableFileNames[i].IndexOf(searchDirectory, StringComparison.Ordinal);
                availableFileNames[i] = availableFileNames[i].Substring(idx);
            }
        }

        if (selectedFileIndex >= availableFiles.Count)
            selectedFileIndex = 0;
    }

    // ═══════════════════════════════════════════════════
    // OnGUI
    // ═══════════════════════════════════════════════════
    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("AI Test Analyst (LLM)", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // ── API 配置区 ──
        EditorGUILayout.LabelField("API Configuration", EditorStyles.miniBoldLabel);
        apiKey = EditorGUILayout.PasswordField("API Key", apiKey);
        baseUrl = EditorGUILayout.TextField("Base URL", baseUrl);
        modelName = EditorGUILayout.TextField("Model Name", modelName);

        EditorGUILayout.Space(8);

        // ── 文件选择区 ──
        EditorGUILayout.LabelField("File Selection", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        searchDirectory = EditorGUILayout.TextField("Search Directory", searchDirectory);
        if (GUILayout.Button("Refresh", GUILayout.Width(60)))
        {
            RefreshFileList();
        }
        EditorGUILayout.EndHorizontal();

        if (availableFileNames.Length > 0)
        {
            selectedFileIndex = EditorGUILayout.Popup("Target File", selectedFileIndex, availableFileNames);
        }
        else
        {
            EditorGUILayout.HelpBox("No .txt / .log / .json / .md files found in the search directory.", MessageType.Info);
        }

        EditorGUILayout.Space(8);

        // ── 战报选择区 ──
        EditorGUILayout.LabelField("AI Arena Reports", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        if (reportFileNames.Length > 0)
        {
            selectedReportIndex = EditorGUILayout.Popup("Battle Report", selectedReportIndex, reportFileNames);
        }
        else
        {
            EditorGUILayout.LabelField("No reports found in reports/ai_arena_reports/");
        }
        if (GUILayout.Button("Refresh List", GUILayout.Width(90)))
        {
            RefreshReportList();
        }
        EditorGUILayout.EndHorizontal();

        if (reportFileNames.Length > 0)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load Report", GUILayout.Height(24)))
            {
                LoadSelectedReport();
            }
            EditorGUI.BeginDisabledGroup(isRequesting || reportFiles.Count == 0 || string.IsNullOrEmpty(apiKey));
            if (GUILayout.Button(isRequesting ? "Analyzing... Please wait" : "Analyze Report (LLM)", GUILayout.Height(24)))
            {
                string reportPath = reportFiles[selectedReportIndex];
                AnalyzeReportAsync(reportPath);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(8);

        // ── 通用文件分析按钮 ──
        EditorGUI.BeginDisabledGroup(isRequesting || availableFiles.Count == 0 || string.IsNullOrEmpty(apiKey));
        if (GUILayout.Button(isRequesting ? "Analyzing... Please wait" : "Analyze Selected File", GUILayout.Height(30)))
        {
            SendAnalysisRequest();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(8);

        // ── 结果显示区 ──
        EditorGUILayout.LabelField("Analysis Result", EditorStyles.miniBoldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(analysisResult, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    // ═══════════════════════════════════════════════════
    // 战报专用 LLM 分析（核心方法）
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 异步发送战报 JSON 到 LLM 进行专业诊断分析。
    /// 使用硬编码的 BATTLE_REPORT_SYSTEM_PROMPT 作为系统提示词。
    /// 通过 JsonUtility + Serializable class 构建标准 OpenAI ChatCompletion payload。
    /// </summary>
    private async void AnalyzeReportAsync(string jsonFilePath)
    {
        if (string.IsNullOrEmpty(jsonFilePath) || !File.Exists(jsonFilePath))
        {
            analysisResult = "[Error] Report file not found: " + jsonFilePath;
            Repaint();
            return;
        }

        // 读取战报 JSON 内容
        string reportJson = File.ReadAllText(jsonFilePath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(reportJson))
        {
            analysisResult = "[Error] Report file is empty.";
            Repaint();
            return;
        }

        // 截断过长内容防止超出 token 限制
        const int maxChars = 15000;
        if (reportJson.Length > maxChars)
        {
            reportJson = reportJson.Substring(0, maxChars) + "\n\n... [TRUNCATED] ...";
        }

        // 禁用按钮，显示等待状态
        isRequesting = true;
        analysisResult = "Analyzing... Please wait";
        Repaint();

        try
        {
            // 构建 OpenAI ChatCompletion 请求体
            var requestBody = new OpenAIRequest
            {
                model = modelName,
                temperature = 0.7f,
                messages = new List<OpenAIMessage>
                {
                    new OpenAIMessage { role = "system", content = BATTLE_REPORT_SYSTEM_PROMPT },
                    new OpenAIMessage { role = "user", content = reportJson }
                }
            };

            // JsonUtility 不支持直接序列化 List 内嵌对象的 messages 数组，
            // 因此使用手动 JSON 构建确保格式正确
            string jsonPayload = BuildOpenAIRequestJson(requestBody);

            // 发送 HTTP POST 请求
            UnityWebRequest webRequest = new UnityWebRequest(baseUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Authorization", "Bearer " + apiKey);

            var operation = webRequest.SendWebRequest();

            // 异步等待请求完成，避免卡死主线程
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            // 处理响应
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string responseJson = webRequest.downloadHandler.text;

                // 尝试用 JsonUtility 反序列化
                OpenAIResponse response = JsonUtility.FromJson<OpenAIResponse>(responseJson);
                if (response != null && response.choices != null && response.choices.Count > 0
                    && response.choices[0].message != null)
                {
                    analysisResult = response.choices[0].message.content;
                }
                else
                {
                    // JsonUtility 解析失败时回退到手动解析
                    analysisResult = ParseResponseContent(responseJson);
                }
            }
            else
            {
                string errorDetail = string.IsNullOrEmpty(webRequest.downloadHandler.text)
                    ? webRequest.error
                    : webRequest.downloadHandler.text;
                analysisResult = $"[Request Failed] HTTP {webRequest.responseCode}: {errorDetail}";
            }

            webRequest.Dispose();
        }
        catch (Exception ex)
        {
            analysisResult = $"[Exception] {ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
            Debug.LogError($"[AI Test Analyst] AnalyzeReportAsync exception: {ex}");
        }
        finally
        {
            isRequesting = false;
            Repaint();
        }
    }

    // ═══════════════════════════════════════════════════
    // 通用文件 LLM 请求（保留原有功能）
    // ═══════════════════════════════════════════════════
    private async void SendAnalysisRequest()
    {
        if (selectedFileIndex < 0 || selectedFileIndex >= availableFiles.Count) return;

        string filePath = availableFiles[selectedFileIndex];
        if (!File.Exists(filePath))
        {
            analysisResult = "[Error] File not found: " + filePath;
            return;
        }

        string fileContent = File.ReadAllText(filePath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(fileContent))
        {
            analysisResult = "[Error] File is empty.";
            return;
        }

        // 截断过长内容防止超出 token 限制
        const int maxChars = 12000;
        if (fileContent.Length > maxChars)
        {
            fileContent = fileContent.Substring(0, maxChars) + "\n\n... [TRUNCATED] ...";
        }

        isRequesting = true;
        analysisResult = "Analyzing... Please wait";
        Repaint();

        try
        {
            string genericSystemPrompt = "You are a senior QA analyst for a Unity 2D platformer game called MarioTrickster. " +
                "Analyze the following test log / data file and provide: " +
                "1) A brief summary of what the file contains. " +
                "2) Any errors, warnings, or anomalies detected. " +
                "3) Actionable recommendations for the development team.";

            string userContent = $"File: {Path.GetFileName(filePath)}\n\n```\n{fileContent}\n```";

            var requestBody = new OpenAIRequest
            {
                model = modelName,
                temperature = 0.3f,
                messages = new List<OpenAIMessage>
                {
                    new OpenAIMessage { role = "system", content = genericSystemPrompt },
                    new OpenAIMessage { role = "user", content = userContent }
                }
            };

            string jsonPayload = BuildOpenAIRequestJson(requestBody);

            UnityWebRequest webRequest = new UnityWebRequest(baseUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Authorization", "Bearer " + apiKey);

            var operation = webRequest.SendWebRequest();

            // 异步等待请求完成，避免卡死主线程
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string responseJson = webRequest.downloadHandler.text;
                OpenAIResponse response = JsonUtility.FromJson<OpenAIResponse>(responseJson);
                if (response != null && response.choices != null && response.choices.Count > 0
                    && response.choices[0].message != null)
                {
                    analysisResult = response.choices[0].message.content;
                }
                else
                {
                    analysisResult = ParseResponseContent(responseJson);
                }
            }
            else
            {
                string errorDetail = string.IsNullOrEmpty(webRequest.downloadHandler.text)
                    ? webRequest.error
                    : webRequest.downloadHandler.text;
                analysisResult = $"[Request Failed] HTTP {webRequest.responseCode}: {errorDetail}";
            }

            webRequest.Dispose();
        }
        catch (Exception ex)
        {
            analysisResult = $"[Exception] {ex.GetType().Name}: {ex.Message}";
            Debug.LogError($"[AI Test Analyst] SendAnalysisRequest exception: {ex}");
        }
        finally
        {
            isRequesting = false;
            Repaint();
        }
    }

    // ═══════════════════════════════════════════════════
    // JSON 构建与解析（零第三方依赖）
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 使用 Serializable class 数据构建标准 OpenAI ChatCompletion JSON payload。
    /// 由于 JsonUtility 对 List 嵌套序列化存在限制，此处手动构建确保格式正确。
    /// </summary>
    private string BuildOpenAIRequestJson(OpenAIRequest req)
    {
        var sb = new StringBuilder(512);
        sb.Append("{");
        sb.Append("\"model\":\"").Append(EscapeJsonString(req.model)).Append("\",");
        sb.Append("\"temperature\":").Append(req.temperature.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)).Append(",");
        sb.Append("\"messages\":[");

        for (int i = 0; i < req.messages.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("{\"role\":\"").Append(EscapeJsonString(req.messages[i].role)).Append("\",");
            sb.Append("\"content\":\"").Append(EscapeJsonString(req.messages[i].content)).Append("\"}");
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private static string EscapeJsonString(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";

        var sb = new StringBuilder(input.Length + 32);
        foreach (char c in input)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '\"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < 0x20)
                        sb.AppendFormat("\\u{0:X4}", (int)c);
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════
    // 战报目录扫描与加载
    // ═══════════════════════════════════════════════════
    private void RefreshReportList()
    {
        reportFiles.Clear();

        string reportsDir = Path.Combine(Application.dataPath, "..", "reports", "ai_arena_reports");
        reportsDir = Path.GetFullPath(reportsDir);

        if (Directory.Exists(reportsDir))
        {
            string[] jsonFiles = Directory.GetFiles(reportsDir, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(jsonFiles, StringComparer.OrdinalIgnoreCase);
            reportFiles.AddRange(jsonFiles);
        }

        reportFileNames = new string[reportFiles.Count];
        for (int i = 0; i < reportFiles.Count; i++)
        {
            reportFileNames[i] = Path.GetFileName(reportFiles[i]);
        }

        if (selectedReportIndex >= reportFiles.Count)
            selectedReportIndex = 0;
    }

    private void LoadSelectedReport()
    {
        if (selectedReportIndex < 0 || selectedReportIndex >= reportFiles.Count)
        {
            reportContent = "[Error] No report selected.";
            analysisResult = reportContent;
            Repaint();
            return;
        }

        string path = reportFiles[selectedReportIndex];
        if (!File.Exists(path))
        {
            reportContent = "[Error] File not found: " + path;
            analysisResult = reportContent;
            Repaint();
            return;
        }

        reportContent = File.ReadAllText(path, Encoding.UTF8);
        analysisResult = "[Report Loaded] " + Path.GetFileName(path) + "\n\n" + reportContent;
        Repaint();
    }

    /// <summary>
    /// 回退解析方法：当 JsonUtility 反序列化失败时，手动提取 content 字段。
    /// </summary>
    private static string ParseResponseContent(string json)
    {
        const string marker = "\"content\":\"";
        int startIdx = json.LastIndexOf(marker, StringComparison.Ordinal);
        if (startIdx < 0)
            return "[Parse Error] Could not find 'content' field in response.\n\nRaw:\n" + json;

        startIdx += marker.Length;

        var sb = new StringBuilder();
        for (int i = startIdx; i < json.Length; i++)
        {
            char c = json[i];
            if (c == '\\' && i + 1 < json.Length)
            {
                char next = json[i + 1];
                switch (next)
                {
                    case 'n': sb.Append('\n'); i++; break;
                    case 'r': sb.Append('\r'); i++; break;
                    case 't': sb.Append('\t'); i++; break;
                    case '\\': sb.Append('\\'); i++; break;
                    case '\"': sb.Append('\"'); i++; break;
                    case '/': sb.Append('/'); i++; break;
                    case 'u':
                        if (i + 5 < json.Length)
                        {
                            string hex = json.Substring(i + 2, 4);
                            if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int code))
                            {
                                sb.Append((char)code);
                                i += 5;
                            }
                            else
                            {
                                sb.Append(c);
                            }
                        }
                        break;
                    default: sb.Append(c); break;
                }
            }
            else if (c == '\"')
            {
                break;
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
#endif
