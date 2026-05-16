#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

// ═══════════════════════════════════════════════════════════════════
// LevelAutoHealer — ASCII 关卡自动修复底层静态服务
//
// 职责：
//   接收【当前 ASCII 关卡矩阵】和【病灶诊断报告】，调用 LLM
//   (OpenAI ChatCompletion 格式) 生成修复后的 ASCII 矩阵。
//
// 设计原则：
//   1. 纯静态类，无状态，不依赖 MonoBehaviour
//   2. 复用项目已有的 EditorPrefs API 配置（AI_SmartSlicer_*）
//   3. 纯文本 ChatCompletion（非 Vision），剔除所有图像相关代码
//   4. System Prompt 硬编码，确保 LLM 严格遵守 ASCII 矩阵规则
//   5. 返回值强制清洗，去除 markdown 标记和多余文本
//
// 使用方式：
//   string healed = await LevelAutoHealer.HealAsciiLevelAsync(ascii, report);
//
// API 配置来源（按优先级）：
//   1. EditorPrefs: AI_SmartSlicer_APIKey / AI_SmartSlicer_BaseUrl / AI_SmartSlicer_Model
//   2. 环境变量: OPENAI_API_KEY
//   3. 默认 BaseUrl: https://api.openai.com/v1
//   4. 默认 Model: gpt-4.1-mini
//
// [AI防坑警告]
//   - 本服务仅在 Editor 模式下可用，不会打包进运行时 build
//   - System Prompt 要求 LLM 保持行列数严格不变
//   - 返回值经过 markdown 标记清洗，确保是纯净 ASCII 矩阵
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// ASCII 关卡自动修复底层静态服务。
/// 调用 LLM 根据病灶报告自动修复 ASCII 关卡矩阵。
/// </summary>
public static class LevelAutoHealer
{
    // ═══════════════════════════════════════════════════════════
    // 常量
    // ═══════════════════════════════════════════════════════════

    private const string DEFAULT_BASE_URL = "https://api.openai.com/v1";
    private const string DEFAULT_MODEL = "gpt-4.1-mini";
    private const float TEMPERATURE = 0.2f;
    private const int MAX_TOKENS = 8192;
    private const int TIMEOUT_SECONDS = 90;

    /// <summary>
    /// 硬编码的 System Prompt，严格约束 LLM 输出格式。
    /// </summary>
    private const string SYSTEM_PROMPT =
        "你是一个 2D 平台关卡专家。我会提供【当前 ASCII 关卡矩阵】和【病灶报告(卡死/致死网格坐标)】。" +
        "请在病灶附近的空气('.')处替换为平台('=')或跳板('B')来修复死路，" +
        "或将阻碍路线的过高的墙('W')替换为空气('.')。\n" +
        "绝对规则：\n" +
        "1. 必须保持原有矩阵的行数和列数严格不变！\n" +
        "2. 只允许输出纯 ASCII 文本矩阵！\n" +
        "3. 严禁输出任何 markdown 标记（如 ```text），严禁输出任何解释或前言后语！";

    // ═══════════════════════════════════════════════════════════
    // 公开 API
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 异步调用 LLM 修复 ASCII 关卡。
    /// </summary>
    /// <param name="currentAscii">当前 ASCII 关卡矩阵文本（多行字符串）。</param>
    /// <param name="diagnosticReport">病灶诊断报告（含卡死/致死网格坐标）。</param>
    /// <returns>修复后的纯净 ASCII 矩阵文本，或 null（失败时）。</returns>
    /// <exception cref="InvalidOperationException">未配置 API Key 时抛出。</exception>
    public static async Task<string> HealAsciiLevelAsync(string currentAscii, string diagnosticReport)
    {
        // ── 读取 API 配置 ──
        string apiKey = ResolveApiKey();
        string baseUrl = ResolveBaseUrl();
        string model = ResolveModel();

        if (string.IsNullOrEmpty(apiKey))
        {
            const string errMsg =
                "[LevelAutoHealer] 未配置 API Key！请在 AI Smart Slicer 窗口或环境变量 OPENAI_API_KEY 中设置。";
            Debug.LogError(errMsg);
            throw new InvalidOperationException(errMsg);
        }

        // ── 构建用户消息 ──
        string userMessage =
            "【当前 ASCII 关卡矩阵】\n" +
            currentAscii + "\n\n" +
            "【病灶报告】\n" +
            diagnosticReport;

        // ── 构建请求体（纯文本 ChatCompletion，非 Vision） ──
        string requestBody = BuildRequestBody(model, userMessage);

        Debug.Log($"[LevelAutoHealer] 正在调用 LLM 修复关卡... (Model: {model}, BaseUrl: {baseUrl})");

        // ── 发送 HTTP 请求 ──
        string rawResponse = await SendChatCompletionAsync(apiKey, baseUrl, requestBody);

        if (rawResponse == null)
        {
            Debug.LogError("[LevelAutoHealer] LLM 返回为空，修复失败。");
            return null;
        }

        // ── 提取 content 字段 ──
        string content = ExtractContentFromResponse(rawResponse);

        if (string.IsNullOrEmpty(content))
        {
            Debug.LogError("[LevelAutoHealer] 无法从 LLM 响应中提取 content 字段。");
            return null;
        }

        // ── 强制清洗返回值 ──
        string cleaned = SanitizeAsciiOutput(content);

        // ── 验证行列数 ──
        if (!ValidateDimensions(currentAscii, cleaned))
        {
            Debug.LogWarning(
                "[LevelAutoHealer] ⚠️ LLM 返回的矩阵行列数与原始不一致！" +
                "返回原始结果供人工审查。");
        }

        Debug.Log("<color=#88FF88>[LevelAutoHealer] ✅ ASCII 关卡修复完成！</color>");
        return cleaned;
    }

    // ═══════════════════════════════════════════════════════════
    // API 配置解析
    // ═══════════════════════════════════════════════════════════

    private static string ResolveApiKey()
    {
        string key = EditorPrefs.GetString("AI_SmartSlicer_APIKey", "");
        if (string.IsNullOrEmpty(key))
            key = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        return key;
    }

    private static string ResolveBaseUrl()
    {
        string url = EditorPrefs.GetString("AI_SmartSlicer_BaseUrl", "");
        if (string.IsNullOrEmpty(url))
            url = DEFAULT_BASE_URL;
        return url;
    }

    private static string ResolveModel()
    {
        string model = EditorPrefs.GetString("AI_SmartSlicer_Model", "");
        if (string.IsNullOrEmpty(model))
            model = DEFAULT_MODEL;
        return model;
    }

    // ═══════════════════════════════════════════════════════════
    // HTTP 请求
    // ═══════════════════════════════════════════════════════════

    private static string BuildRequestBody(string model, string userMessage)
    {
        // 纯文本 ChatCompletion 请求（非 Vision）
        return $@"{{
  ""model"": ""{EscapeJsonValue(model)}"",
  ""messages"": [
    {{""role"": ""system"", ""content"": {EscapeJsonString(SYSTEM_PROMPT)}}},
    {{""role"": ""user"", ""content"": {EscapeJsonString(userMessage)}}}
  ],
  ""temperature"": {TEMPERATURE.ToString(System.Globalization.CultureInfo.InvariantCulture)},
  ""max_tokens"": {MAX_TOKENS}
}}";
    }

    private static async Task<string> SendChatCompletionAsync(
        string apiKey, string baseUrl, string requestBody)
    {
        using (var client = new HttpClient())
        {
            client.Timeout = TimeSpan.FromSeconds(TIMEOUT_SECONDS);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var httpContent = new StringContent(requestBody, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{baseUrl}/chat/completions", httpContent);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                Debug.LogError($"[LevelAutoHealer] API Error {response.StatusCode}: {error}");
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 响应解析（复用 AI_SmartSlicerWindow 的 ExtractContent 逻辑）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 从 OpenAI ChatCompletion JSON 响应中提取 message.content 字段。
    /// 手动解析，不依赖第三方 JSON 库。
    /// </summary>
    private static string ExtractContentFromResponse(string responseJson)
    {
        // 找到 "message" 对象中的 "content" 字段
        int msgIdx = responseJson.IndexOf("\"message\"");
        if (msgIdx < 0) return null;

        int contentIdx = responseJson.IndexOf("\"content\"", msgIdx);
        if (contentIdx < 0) return null;

        int colonIdx = responseJson.IndexOf(':', contentIdx);
        if (colonIdx < 0) return null;

        // 跳过空白找到引号开始
        int start = colonIdx + 1;
        while (start < responseJson.Length && responseJson[start] == ' ') start++;

        if (start >= responseJson.Length) return null;

        if (responseJson[start] == '"')
        {
            // 解析 JSON 转义字符串
            StringBuilder sb = new StringBuilder();
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

        return null;
    }

    // ═══════════════════════════════════════════════════════════
    // 返回值清洗
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 强制清洗 LLM 返回的 ASCII 文本：
    /// 去除 markdown 代码块标记、前言后语、多余空行。
    /// </summary>
    private static string SanitizeAsciiOutput(string raw)
    {
        // 第一步：暴力替换 markdown 代码块标记
        string cleaned = raw
            .Replace("```text", "")
            .Replace("```ascii", "")
            .Replace("```plaintext", "")
            .Replace("```", "")
            .Trim();

        // 第二步：按行过滤，去除纯文字解释行（不含 ASCII 关卡字符的行）
        // 保留包含关卡字符的行（如 . = W B # - 等）
        var lines = cleaned.Split('\n');
        var filteredLines = new System.Collections.Generic.List<string>();
        bool matrixStarted = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');

            // 检测是否是 ASCII 矩阵行（包含典型关卡字符且长度一致）
            if (IsAsciiMatrixLine(line))
            {
                matrixStarted = true;
                filteredLines.Add(line);
            }
            else if (matrixStarted && string.IsNullOrWhiteSpace(line))
            {
                // 矩阵中间的空行可能是分隔，跳过
                continue;
            }
            else if (matrixStarted)
            {
                // 矩阵结束后的文字行，丢弃
                break;
            }
            // 矩阵开始前的文字行，丢弃
        }

        return string.Join("\n", filteredLines);
    }

    /// <summary>
    /// 判断一行是否像 ASCII 关卡矩阵行。
    /// 关卡字符集：. = W B # - ^ v S E G L X @ 等。
    /// 如果一行中超过 60% 的字符属于关卡字符集，则认为是矩阵行。
    /// </summary>
    private static bool IsAsciiMatrixLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        if (line.Length < 3) return false;

        int levelCharCount = 0;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '.' || c == '=' || c == 'W' || c == 'B' || c == '#' ||
                c == '-' || c == '^' || c == 'v' || c == 'S' || c == 'E' ||
                c == 'G' || c == 'L' || c == 'X' || c == '@' || c == 'M' ||
                c == 'T' || c == 'P' || c == 'C' || c == 'H' || c == 'D' ||
                c == 'O' || c == 'R' || c == 'F' || c == 'A' || c == 'N' ||
                c == ' ' || c == '_' || c == '|' || c == '+' || c == '*')
            {
                levelCharCount++;
            }
        }

        float ratio = (float)levelCharCount / line.Length;
        return ratio > 0.6f;
    }

    // ═══════════════════════════════════════════════════════════
    // 维度验证
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 验证修复后的 ASCII 矩阵行列数是否与原始一致。
    /// </summary>
    private static bool ValidateDimensions(string original, string healed)
    {
        if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(healed))
            return false;

        string[] origLines = original.Split('\n');
        string[] healLines = healed.Split('\n');

        // 过滤空行
        var origNonEmpty = System.Array.FindAll(origLines, l => !string.IsNullOrWhiteSpace(l));
        var healNonEmpty = System.Array.FindAll(healLines, l => !string.IsNullOrWhiteSpace(l));

        if (origNonEmpty.Length != healNonEmpty.Length)
        {
            Debug.LogWarning(
                $"[LevelAutoHealer] 行数不匹配：原始 {origNonEmpty.Length} 行 → 修复后 {healNonEmpty.Length} 行");
            return false;
        }

        // 检查每行列数
        for (int i = 0; i < origNonEmpty.Length; i++)
        {
            int origCols = origNonEmpty[i].TrimEnd('\r').Length;
            int healCols = healNonEmpty[i].TrimEnd('\r').Length;
            if (origCols != healCols)
            {
                Debug.LogWarning(
                    $"[LevelAutoHealer] 第 {i} 行列数不匹配：原始 {origCols} 列 → 修复后 {healCols} 列");
                return false;
            }
        }

        return true;
    }

    // ═══════════════════════════════════════════════════════════
    // JSON 工具方法
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 将字符串转义为 JSON 字符串值（带引号包裹）。
    /// </summary>
    private static string EscapeJsonString(string s)
    {
        return "\"" + s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t") + "\"";
    }

    /// <summary>
    /// 转义 JSON 值中的特殊字符（不带引号包裹）。
    /// 用于 model 名等已在模板中被引号包裹的字段。
    /// </summary>
    private static string EscapeJsonValue(string s)
    {
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }
}
#endif
