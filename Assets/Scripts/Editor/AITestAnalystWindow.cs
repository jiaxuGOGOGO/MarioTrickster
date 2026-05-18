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
///   - 在 Editor 窗口内直接展示分析结果
///
/// 配置通过 EditorPrefs 持久化，无需每次重填。
/// 仅依赖 Unity 原生库（UnityEngine.Networking / System.Threading.Tasks），零第三方依赖。
/// </summary>
public class AITestAnalystWindow : EditorWindow
{
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
            if (GUILayout.Button("Load Report", GUILayout.Height(24)))
            {
                LoadSelectedReport();
            }
        }

        EditorGUILayout.Space(8);

        // ── 操作按钮 ──
        EditorGUI.BeginDisabledGroup(isRequesting || availableFiles.Count == 0 || string.IsNullOrEmpty(apiKey));
        if (GUILayout.Button(isRequesting ? "Analyzing..." : "Analyze Selected File", GUILayout.Height(30)))
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
    // LLM 请求
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
        analysisResult = "Sending request to LLM...";
        Repaint();

        try
        {
            string result = await RequestLLMAnalysis(fileContent, filePath);
            analysisResult = result;
        }
        catch (Exception ex)
        {
            analysisResult = $"[Error] {ex.Message}";
        }
        finally
        {
            isRequesting = false;
            Repaint();
        }
    }

    private Task<string> RequestLLMAnalysis(string fileContent, string filePath)
    {
        var tcs = new TaskCompletionSource<string>();

        string systemPrompt = "You are a senior QA analyst for a Unity 2D platformer game called MarioTrickster. " +
            "Analyze the following test log / data file and provide: " +
            "1) A brief summary of what the file contains. " +
            "2) Any errors, warnings, or anomalies detected. " +
            "3) Actionable recommendations for the development team.";

        string userPrompt = $"File: {Path.GetFileName(filePath)}\n\n```\n{fileContent}\n```";

        // 构建 JSON body（手动拼接避免引入 JsonUtility 对嵌套数组的限制）
        string jsonBody = BuildRequestJson(systemPrompt, userPrompt);

        UnityWebRequest request = new UnityWebRequest(baseUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);

        var operation = request.SendWebRequest();
        operation.completed += _ =>
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                string parsed = ParseResponseContent(responseText);
                tcs.SetResult(parsed);
            }
            else
            {
                string errorDetail = string.IsNullOrEmpty(request.downloadHandler.text)
                    ? request.error
                    : request.downloadHandler.text;
                tcs.SetResult($"[Request Failed] {request.responseCode}: {errorDetail}");
            }
            request.Dispose();
        };

        return tcs.Task;
    }

    // ═══════════════════════════════════════════════════
    // JSON 构建与解析（零第三方依赖）
    // ═══════════════════════════════════════════════════
    private string BuildRequestJson(string systemPrompt, string userPrompt)
    {
        // 转义 JSON 特殊字符
        string sysEscaped = EscapeJsonString(systemPrompt);
        string usrEscaped = EscapeJsonString(userPrompt);

        return "{" +
            "\"model\":\"" + EscapeJsonString(modelName) + "\"," +
            "\"messages\":[" +
                "{\"role\":\"system\",\"content\":\"" + sysEscaped + "\"}," +
                "{\"role\":\"user\",\"content\":\"" + usrEscaped + "\"}" +
            "]," +
            "\"temperature\":0.3" +
        "}";
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

    private static string ParseResponseContent(string json)
    {
        // 简易解析：提取 "content":"..." 字段值
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
                break; // 结束
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
