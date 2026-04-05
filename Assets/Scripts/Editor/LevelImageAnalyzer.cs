using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// AI 关卡图片分析器 — 从参考图片/截图自动生成 ASCII 关卡模板
/// 
/// 核心功能:
///   1. 拖入 Mario 风格关卡截图 → AI 识别地形、敌人、陷阱布局
///   2. 自动转换为项目的 ASCII 字符模板（可直接喂给 AsciiLevelGenerator）
///   3. 识别参考关卡中的要素，标注项目中已有 vs 缺失的元素
///   4. 生成改进建议（推荐新增哪些敌人/陷阱/机关）
///
/// 使用方式:
///   在 Test Console → Level Builder Tab → "AI Level Analyzer" 区块中操作
///   或通过代码: LevelImageAnalyzer.AnalyzeImage(texture2D, callback)
///
/// 依赖:
///   - OpenAI API Key（通过环境变量 OPENAI_API_KEY 或 Editor 手动输入）
///   - 网络连接（调用 GPT-4.1-mini Vision API）
///
/// Session 26: 新增
/// </summary>
public static class LevelImageAnalyzer
{
    // ═══════════════════════════════════════════════════
    // 数据结构
    // ═══════════════════════════════════════════════════

    /// <summary>分析结果</summary>
    [Serializable]
    public class AnalysisResult
    {
        /// <summary>生成的 ASCII 关卡模板（可直接用于 AsciiLevelGenerator）</summary>
        public string asciiTemplate;

        /// <summary>关卡设计概述</summary>
        public string designSummary;

        /// <summary>识别到的所有要素列表</summary>
        public List<RecognizedElement> recognizedElements = new List<RecognizedElement>();

        /// <summary>项目中已支持的要素</summary>
        public List<string> supportedElements = new List<string>();

        /// <summary>项目中缺失的要素（建议新增）</summary>
        public List<MissingElement> missingElements = new List<MissingElement>();

        /// <summary>关卡改进建议</summary>
        public List<string> improvementSuggestions = new List<string>();

        /// <summary>原始 AI 响应文本</summary>
        public string rawResponse;

        /// <summary>是否分析成功</summary>
        public bool success;

        /// <summary>错误信息（如有）</summary>
        public string error;
    }

    /// <summary>识别到的关卡要素</summary>
    [Serializable]
    public class RecognizedElement
    {
        public string name;           // 要素名称（如 "Goomba", "Piranha Plant"）
        public string category;       // 分类（Enemy, Trap, Platform, Terrain, Collectible, Decoration）
        public string description;    // 简要描述
        public bool isSupported;      // 项目中是否已有对应实现
        public string mappedChar;     // 映射到的 ASCII 字符（如有）
    }

    /// <summary>缺失的要素</summary>
    [Serializable]
    public class MissingElement
    {
        public string name;           // 要素名称
        public string category;       // 分类
        public string description;    // 功能描述
        public string priority;       // 优先级: High / Medium / Low
        public string suggestion;     // 实现建议
    }

    // ═══════════════════════════════════════════════════
    // 项目已支持的要素映射表
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 项目当前支持的所有关卡要素及其 ASCII 映射
    /// [AI防坑警告] 新增元素到 AsciiLevelGenerator 后，必须同步更新此表
    /// </summary>
    private static readonly Dictionary<string, string> SUPPORTED_ELEMENTS = new Dictionary<string, string>
    {
        // 地形
        { "ground",            "#" },
        { "solid block",       "#" },
        { "platform",          "=" },
        { "wall",              "W" },
        { "one-way platform",  "-" },
        { "moving platform",   ">" },

        // 陷阱
        { "spike",             "^" },
        { "spike trap",        "^" },
        { "fire trap",         "~" },
        { "fire bar",          "~" },
        { "pendulum",          "P" },
        { "pendulum trap",     "P" },

        // 平台
        { "bouncy platform",   "B" },
        { "spring",            "B" },
        { "trampoline",        "B" },
        { "collapsing platform", "C" },
        { "crumbling platform", "C" },

        // 敌人
        { "goomba",            "e" },
        { "simple enemy",      "e" },
        { "patrol enemy",      "e" },
        { "bouncing enemy",    "E" },
        { "koopa",             "E" },

        // 隐藏/欺骗
        { "fake wall",         "F" },
        { "hidden passage",    "H" },
        { "secret area",       "H" },

        // 其他
        { "coin",              "o" },
        { "collectible",       "o" },
        { "goal",              "G" },
        { "flagpole",          "G" },
        { "spawn point",       "M" },

        // 空气
        { "air",               "." },
        { "empty",             "." },
        { "gap",               "." },
        { "pit",               "." },
    };

    /// <summary>
    /// 已知但项目尚未实现的经典要素（用于缺失识别）
    /// </summary>
    private static readonly Dictionary<string, string[]> KNOWN_MISSING_ELEMENTS = new Dictionary<string, string[]>
    {
        // category, name, description, priority, suggestion
        { "conveyor belt",       new[] { "Platform",    "改变玩家地面移动速度/方向的传送带",         "High",   "继承 ControllableLevelElement，Trickster 可反转方向" } },
        { "rotating saw",        new[] { "Trap",        "周期性旋转的锯片/激光障碍",               "High",   "圆形碰撞体 + 旋转动画，可沿轨道移动" } },
        { "flying enemy",        new[] { "Enemy",       "不受重力影响的空中巡逻敌人",               "High",   "类似 Paragoomba，在固定高度水平/正弦波巡逻" } },
        { "bullet bill",         new[] { "Enemy",       "从炮台发射的直线飞行敌人",                 "High",   "需要 Launcher + Projectile 两个组件" } },
        { "piranha plant",       new[] { "Enemy",       "从管道中周期性伸出的敌人",                 "High",   "需要管道地形配合，周期性上下移动" } },
        { "water",               new[] { "Terrain",     "改变跳跃物理的水域区域",                   "High",   "触发区域内降低重力、允许游泳动作" } },
        { "lava",                new[] { "Hazard",      "接触即死的熔岩区域",                       "High",   "类似地刺但覆盖整个地面区域" } },
        { "ice surface",         new[] { "Terrain",     "低摩擦力的冰面地形",                       "Medium", "修改玩家地面摩擦系数" } },
        { "key and lock",        new[] { "Puzzle",      "钥匙+锁门的探索解谜机制",                  "Medium", "收集钥匙后解锁对应门" } },
        { "checkpoint",          new[] { "Checkpoint",  "中途存档点",                               "Medium", "触发后更新重生位置" } },
        { "destructible block",  new[] { "Terrain",     "可被顶撞/攻击破坏的方块",                  "Medium", "已有 Breakable.cs 但未接入 ASCII 系统" } },
        { "ladder",              new[] { "Terrain",     "可攀爬的梯子/藤蔓",                        "Medium", "触发区域内允许垂直移动" } },
        { "wind zone",           new[] { "Hazard",      "推动玩家的风力区域",                       "Medium", "持续施加水平/垂直力" } },
        { "cannon",              new[] { "Trap",        "定时发射弹丸的炮台",                       "Medium", "固定位置 + 定时生成投射物" } },
        { "thwomp",              new[] { "Enemy",       "从上方砸落的石块敌人",                     "Medium", "检测玩家在下方时快速下落，缓慢上升" } },
        { "boss",                new[] { "Enemy",       "关底 Boss 战",                            "Low",    "需要独立的战斗系统和 AI 状态机" } },
        { "warp pipe",           new[] { "Terrain",     "传送管道",                                 "Low",    "进入后传送到另一个位置" } },
        { "gravity zone",        new[] { "Hazard",      "反转或改变重力方向的区域",                  "Low",    "触发区域内反转玩家重力" } },
    };

    // ═══════════════════════════════════════════════════
    // 公共 API
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 分析关卡图片并生成 ASCII 模板 + 缺失要素报告
    /// </summary>
    /// <param name="imagePath">图片文件的绝对路径</param>
    /// <param name="apiKey">OpenAI API Key</param>
    /// <param name="callback">分析完成后的回调</param>
    public static async void AnalyzeImageAsync(string imagePath, string apiKey, Action<AnalysisResult> callback)
    {
        AnalysisResult result = new AnalysisResult();

        try
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                result.error = $"Image file not found: {imagePath}";
                result.success = false;
                callback?.Invoke(result);
                return;
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                result.error = "OpenAI API Key is empty. Please set it in the analyzer settings.";
                result.success = false;
                callback?.Invoke(result);
                return;
            }

            // 读取图片并转为 base64
            byte[] imageBytes = File.ReadAllBytes(imagePath);
            string base64Image = Convert.ToBase64String(imageBytes);
            string mimeType = GetMimeType(imagePath);

            // 构建 prompt
            string systemPrompt = BuildSystemPrompt();
            string userPrompt = BuildUserPrompt();

            // 调用 API
            string response = await CallVisionAPIAsync(apiKey, systemPrompt, userPrompt, base64Image, mimeType);

            if (string.IsNullOrEmpty(response))
            {
                result.error = "API returned empty response.";
                result.success = false;
                callback?.Invoke(result);
                return;
            }

            result.rawResponse = response;

            // 解析响应
            ParseAnalysisResponse(response, result);
            result.success = true;
        }
        catch (Exception ex)
        {
            result.error = $"Analysis failed: {ex.Message}";
            result.success = false;
            Debug.LogError($"[LevelImageAnalyzer] {result.error}\n{ex.StackTrace}");
        }

        callback?.Invoke(result);
    }

    /// <summary>
    /// 仅执行缺失要素分析（不需要图片，基于文本描述）
    /// </summary>
    public static AnalysisResult AnalyzeElementGaps(string levelDescription)
    {
        AnalysisResult result = new AnalysisResult();
        result.success = true;

        // 将描述中提到的要素与已知缺失列表对比
        string lowerDesc = levelDescription.ToLower();

        foreach (var kvp in KNOWN_MISSING_ELEMENTS)
        {
            if (lowerDesc.Contains(kvp.Key.Replace("_", " ")))
            {
                result.missingElements.Add(new MissingElement
                {
                    name = kvp.Key,
                    category = kvp.Value[0],
                    description = kvp.Value[1],
                    priority = kvp.Value[2],
                    suggestion = kvp.Value[3]
                });
            }
        }

        return result;
    }

    /// <summary>获取所有已知缺失要素的完整列表</summary>
    public static Dictionary<string, string[]> GetAllKnownMissingElements()
    {
        return new Dictionary<string, string[]>(KNOWN_MISSING_ELEMENTS);
    }

    /// <summary>获取项目支持的要素映射表</summary>
    public static Dictionary<string, string> GetSupportedElements()
    {
        return new Dictionary<string, string>(SUPPORTED_ELEMENTS);
    }

    // ═══════════════════════════════════════════════════
    // Prompt 构建
    // ═══════════════════════════════════════════════════

    private static string BuildSystemPrompt()
    {
        return @"You are a 2D platformer level design expert specializing in Super Mario-style games. 
Your task is to analyze a screenshot/image of a 2D platformer level and convert it into an ASCII art template.

CRITICAL RULES:
1. Output MUST use ONLY these characters (our project's supported set):
   # = solid ground/block    = = platform    W = wall    . = air/empty
   M = player spawn point    G = goal/end point
   ^ = spike trap    ~ = fire trap    P = pendulum trap
   B = bouncy platform    C = collapsing platform    - = one-way platform
   E = bouncing enemy    e = simple patrol enemy    > = moving platform
   F = fake wall    H = hidden passage    o = coin/collectible

2. The ASCII template should be 40-80 characters wide and 10-20 rows tall.
3. Row 0 (bottom) is the ground level, top rows are sky/ceiling.
4. Maintain proportional layout from the original image.
5. Use '.' for empty space.

For elements you see in the image that are NOT in our supported set, list them as MISSING elements.

RESPONSE FORMAT (use these exact section headers):
===ASCII_TEMPLATE_START===
(your ASCII template here, each line is a row, top row first)
===ASCII_TEMPLATE_END===

===DESIGN_SUMMARY===
(2-3 sentence summary of the level's design philosophy and flow)

===RECOGNIZED_ELEMENTS===
(one per line, format: NAME|CATEGORY|DESCRIPTION|SUPPORTED|MAPPED_CHAR)
Example: Goomba|Enemy|Simple patrol enemy that walks back and forth|true|e

===MISSING_ELEMENTS===
(one per line, format: NAME|CATEGORY|DESCRIPTION|PRIORITY|SUGGESTION)
Example: Piranha Plant|Enemy|Emerges from pipes periodically|High|Add periodic vertical enemy

===IMPROVEMENT_SUGGESTIONS===
(one per line, each a specific actionable suggestion)";
    }

    private static string BuildUserPrompt()
    {
        return @"Please analyze this 2D platformer level image and:
1. Convert the visible layout into an ASCII template using our character set
2. Identify ALL game elements visible (enemies, traps, platforms, terrain features, decorations)
3. For each element, indicate if our project supports it (check against the character set)
4. List elements that are MISSING from our project with priority and implementation suggestions
5. Provide specific improvement suggestions for this level design

Focus on accuracy of the spatial layout and completeness of element identification.";
    }

    // ═══════════════════════════════════════════════════
    // API 调用
    // ═══════════════════════════════════════════════════

    private static async Task<string> CallVisionAPIAsync(string apiKey, string systemPrompt, string userPrompt,
        string base64Image, string mimeType)
    {
        using (var client = new HttpClient())
        {
            client.Timeout = TimeSpan.FromSeconds(120);

            // 使用项目预配置的 OpenAI 兼容 API
            string baseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL");
            if (string.IsNullOrEmpty(baseUrl))
                baseUrl = "https://api.openai.com/v1";

            string url = $"{baseUrl}/chat/completions";

            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            // 构建请求体
            string requestBody = $@"{{
    ""model"": ""gpt-4.1-mini"",
    ""max_tokens"": 4096,
    ""messages"": [
        {{
            ""role"": ""system"",
            ""content"": {EscapeJsonString(systemPrompt)}
        }},
        {{
            ""role"": ""user"",
            ""content"": [
                {{
                    ""type"": ""text"",
                    ""text"": {EscapeJsonString(userPrompt)}
                }},
                {{
                    ""type"": ""image_url"",
                    ""image_url"": {{
                        ""url"": ""data:{mimeType};base64,{base64Image}"",
                        ""detail"": ""high""
                    }}
                }}
            ]
        }}
    ]
}}";

            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError($"[LevelImageAnalyzer] API Error {response.StatusCode}: {responseBody}");
                return null;
            }

            // 简单提取 content（避免引入 JSON 库依赖）
            return ExtractContentFromResponse(responseBody);
        }
    }

    // ═══════════════════════════════════════════════════
    // 响应解析
    // ═══════════════════════════════════════════════════

    private static void ParseAnalysisResponse(string response, AnalysisResult result)
    {
        // 提取 ASCII 模板
        result.asciiTemplate = ExtractSection(response, "===ASCII_TEMPLATE_START===", "===ASCII_TEMPLATE_END===");

        // 提取设计概述
        result.designSummary = ExtractSection(response, "===DESIGN_SUMMARY===", "===RECOGNIZED_ELEMENTS===");
        if (string.IsNullOrEmpty(result.designSummary))
            result.designSummary = ExtractSection(response, "===DESIGN_SUMMARY===", "===MISSING_ELEMENTS===");

        // 提取识别到的要素
        string recognizedSection = ExtractSection(response, "===RECOGNIZED_ELEMENTS===", "===MISSING_ELEMENTS===");
        if (!string.IsNullOrEmpty(recognizedSection))
        {
            foreach (string line in recognizedSection.Split('\n'))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("===")) continue;

                string[] parts = trimmed.Split('|');
                if (parts.Length >= 4)
                {
                    var elem = new RecognizedElement
                    {
                        name = parts[0].Trim(),
                        category = parts[1].Trim(),
                        description = parts[2].Trim(),
                        isSupported = parts[3].Trim().ToLower() == "true",
                        mappedChar = parts.Length > 4 ? parts[4].Trim() : ""
                    };

                    result.recognizedElements.Add(elem);

                    if (elem.isSupported)
                        result.supportedElements.Add(elem.name);
                }
            }
        }

        // 提取缺失要素
        string missingSection = ExtractSection(response, "===MISSING_ELEMENTS===", "===IMPROVEMENT_SUGGESTIONS===");
        if (!string.IsNullOrEmpty(missingSection))
        {
            foreach (string line in missingSection.Split('\n'))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("===")) continue;

                string[] parts = trimmed.Split('|');
                if (parts.Length >= 4)
                {
                    result.missingElements.Add(new MissingElement
                    {
                        name = parts[0].Trim(),
                        category = parts[1].Trim(),
                        description = parts[2].Trim(),
                        priority = parts[3].Trim(),
                        suggestion = parts.Length > 4 ? parts[4].Trim() : ""
                    });
                }
            }
        }

        // 提取改进建议
        string suggestionsSection = ExtractSectionToEnd(response, "===IMPROVEMENT_SUGGESTIONS===");
        if (!string.IsNullOrEmpty(suggestionsSection))
        {
            foreach (string line in suggestionsSection.Split('\n'))
            {
                string trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("==="))
                {
                    result.improvementSuggestions.Add(trimmed);
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // 辅助方法
    // ═══════════════════════════════════════════════════

    private static string ExtractSection(string text, string startMarker, string endMarker)
    {
        int startIdx = text.IndexOf(startMarker);
        if (startIdx == -1) return null;
        startIdx += startMarker.Length;

        int endIdx = text.IndexOf(endMarker, startIdx);
        if (endIdx == -1) endIdx = text.Length;

        return text.Substring(startIdx, endIdx - startIdx).Trim();
    }

    private static string ExtractSectionToEnd(string text, string startMarker)
    {
        int startIdx = text.IndexOf(startMarker);
        if (startIdx == -1) return null;
        startIdx += startMarker.Length;

        return text.Substring(startIdx).Trim();
    }

    private static string ExtractContentFromResponse(string jsonResponse)
    {
        // 简单解析: 找到 "content": "..." 字段
        string marker = "\"content\":";
        int idx = jsonResponse.LastIndexOf(marker);
        if (idx == -1) return null;

        idx += marker.Length;

        // 跳过空白
        while (idx < jsonResponse.Length && (jsonResponse[idx] == ' ' || jsonResponse[idx] == '\n'))
            idx++;

        if (idx >= jsonResponse.Length || jsonResponse[idx] != '"')
            return null;

        // 提取字符串值（处理转义）
        StringBuilder sb = new StringBuilder();
        idx++; // 跳过开头引号
        while (idx < jsonResponse.Length)
        {
            char c = jsonResponse[idx];
            if (c == '\\' && idx + 1 < jsonResponse.Length)
            {
                char next = jsonResponse[idx + 1];
                switch (next)
                {
                    case '"': sb.Append('"'); idx += 2; continue;
                    case '\\': sb.Append('\\'); idx += 2; continue;
                    case 'n': sb.Append('\n'); idx += 2; continue;
                    case 'r': sb.Append('\r'); idx += 2; continue;
                    case 't': sb.Append('\t'); idx += 2; continue;
                    default: sb.Append(c); idx++; continue;
                }
            }
            if (c == '"') break;
            sb.Append(c);
            idx++;
        }

        return sb.ToString();
    }

    private static string EscapeJsonString(string s)
    {
        if (string.IsNullOrEmpty(s)) return "\"\"";

        StringBuilder sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append("\"");
        return sb.ToString();
    }

    private static string GetMimeType(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLower();
        switch (ext)
        {
            case ".png": return "image/png";
            case ".jpg":
            case ".jpeg": return "image/jpeg";
            case ".gif": return "image/gif";
            case ".webp": return "image/webp";
            case ".bmp": return "image/bmp";
            default: return "image/png";
        }
    }
}
