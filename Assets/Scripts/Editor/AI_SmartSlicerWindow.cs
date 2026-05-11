#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// AI Smart Slicer — Unity Editor 内置 AI 视觉裁切工具
///
/// 核心职责：
///   在 Unity Editor 内直接调用 GPT-4.1 视觉模型，让 AI "看"素材图片，
///   识别每个独立物体和动画帧组的边界，然后一键裁切并自动导入项目。
///
/// 工作流（全程在 Unity 内完成）：
///   1. 拖入素材图片（或从 Project 窗口选中）
///   2. 点击"AI 分析" → AI 识别所有物体/动画组
///   3. 预览 AI 的判断结果（可取消勾选不需要的）
///   4. 点击"执行裁切" → 自动切割、保存到项目、触发导入管线
///
/// [AI防坑警告]
///   本脚本使用 HttpClient 调用外部 API，仅在 Editor 模式下运行。
///   API Key 从环境变量 OPENAI_API_KEY 或 EditorPrefs 读取。
///   不会打包进运行时 build。
/// </summary>
public class AI_SmartSlicerWindow : EditorWindow
{
    // =========================================================================
    // 菜单入口
    // =========================================================================
    [MenuItem("MarioTrickster/AI Smart Slicer (智能裁切) %#a", false, 201)]
    public static void ShowWindow()
    {
        var win = GetWindow<AI_SmartSlicerWindow>("AI 智能裁切");
        win.minSize = new Vector2(520, 700);
    }

    // =========================================================================
    // 数据结构
    // =========================================================================
    [Serializable]
    private class AIAnalysisResult
    {
        public int image_width;
        public int image_height;
        public List<DetectedObject> objects = new List<DetectedObject>();
    }

    [Serializable]
    private class DetectedObject
    {
        public string name;
        public string type; // "animation" or "static"
        public int frame_count;
        public int grid_cols;
        public int grid_rows;
        public BBox bbox;
        public string description;
        // UI state
        [NonSerialized] public bool selected = true;
    }

    [Serializable]
    private class BBox
    {
        public int x, y, w, h;
    }

    // =========================================================================
    // 状态
    // =========================================================================
    private Texture2D _sourceTexture;
    private string _apiKey = "";
    private string _baseUrl = "https://api.openai.com/v1";
    private bool _showApiSettings;
    private string _model = "gpt-4.1-mini";
    private bool _removeBackground;
    private Color _bgColor = Color.magenta;
    private int _bgTolerance = 30;

    private AIAnalysisResult _analysisResult;
    private bool _isAnalyzing;
    private bool _analysisComplete;
    private string _statusMessage = "";
    private string _outputFolder = "Assets/Art/Imported";
    private Vector2 _scrollPos;
    private bool _autoImport = true;

    // =========================================================================
    // GUI
    // =========================================================================
    private void OnEnable()
    {
        // 窗口打开时从 EditorPrefs 加载保存的配置
        string savedKey = EditorPrefs.GetString("AI_SmartSlicer_APIKey", "");
        if (!string.IsNullOrEmpty(savedKey)) _apiKey = savedKey;
        string savedUrl = EditorPrefs.GetString("AI_SmartSlicer_BaseUrl", "");
        if (!string.IsNullOrEmpty(savedUrl)) _baseUrl = savedUrl;
        string savedModel = EditorPrefs.GetString("AI_SmartSlicer_Model", "");
        if (!string.IsNullOrEmpty(savedModel)) _model = savedModel;
    }
    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawHeader();
        DrawInputSection();
        DrawSettingsSection();
        DrawAnalyzeButton();
        DrawResultsSection();
        DrawExecuteSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(8);
        GUILayout.Label("AI Smart Slicer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "拖入素材图片 → AI 自动识别物体和动画组 → 一键裁切导入\n" +
            "AI 能区分「8帧走路动画是一组」和「旁边的树是另一个物体」",
            MessageType.Info);
        EditorGUILayout.Space(4);
    }

    private void DrawInputSection()
    {
        EditorGUILayout.LabelField("1. 选择素材", EditorStyles.boldLabel);
        
        _sourceTexture = (Texture2D)EditorGUILayout.ObjectField(
            "素材图片", _sourceTexture, typeof(Texture2D), false);

        if (_sourceTexture != null)
        {
            EditorGUILayout.LabelField($"   尺寸: {_sourceTexture.width} × {_sourceTexture.height}");
        }

        EditorGUILayout.Space(4);
    }

    private void DrawSettingsSection()
    {
        EditorGUILayout.LabelField("2. API 设置", EditorStyles.boldLabel);

        // 从 EditorPrefs 加载保存的设置
        if (string.IsNullOrEmpty(_apiKey))
        {
            _apiKey = EditorPrefs.GetString("AI_SmartSlicer_APIKey", "");
            if (string.IsNullOrEmpty(_apiKey))
                _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        }
        if (_baseUrl == "https://api.openai.com/v1" || string.IsNullOrEmpty(_baseUrl))
        {
            string saved = EditorPrefs.GetString("AI_SmartSlicer_BaseUrl", "");
            if (!string.IsNullOrEmpty(saved))
                _baseUrl = saved;
        }
        {
            string savedModel = EditorPrefs.GetString("AI_SmartSlicer_Model", "");
            if (!string.IsNullOrEmpty(savedModel))
                _model = savedModel;
        }

        // 显示当前配置状态
        bool hasConfig = !string.IsNullOrEmpty(_apiKey);
        string statusIcon = hasConfig ? "\u2705" : "\u274c";
        EditorGUILayout.LabelField($"   {statusIcon} API: {(hasConfig ? _baseUrl : "(未配置)")}");
        if (hasConfig)
        {
            string displayKey = _apiKey.Length > 10 ? _apiKey.Substring(0, 6) + "..." + _apiKey.Substring(_apiKey.Length - 4) : "***";
            EditorGUILayout.LabelField($"   Key: {displayKey}  |  Model: {_model}", EditorStyles.miniLabel);
        }

        // 展开/折叠设置面板
        _showApiSettings = EditorGUILayout.Foldout(_showApiSettings, hasConfig ? "修改 API 设置" : "配置 API（必填）");
        if (_showApiSettings)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.HelpBox(
                "支持 OpenAI 官方和所有兼容接口（如 moyu.info、one-api 等中转站）。\n" +
                "设置一次后永久保存，无需重复填写。",
                MessageType.Info);

            // API Key
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("API Key", GUILayout.Width(70));
            string newKey = EditorGUILayout.PasswordField(_apiKey);
            if (newKey != _apiKey)
            {
                _apiKey = newKey;
                EditorPrefs.SetString("AI_SmartSlicer_APIKey", _apiKey);
            }
            EditorGUILayout.EndHorizontal();

            // Base URL
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Base URL", GUILayout.Width(70));
            string newUrl = EditorGUILayout.TextField(_baseUrl);
            if (newUrl != _baseUrl)
            {
                _baseUrl = newUrl;
                EditorPrefs.SetString("AI_SmartSlicer_BaseUrl", _baseUrl);
            }
            EditorGUILayout.EndHorizontal();

            // Model
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Model", GUILayout.Width(70));
            string newModel = EditorGUILayout.TextField(_model);
            if (newModel != _model)
            {
                _model = newModel;
                EditorPrefs.SetString("AI_SmartSlicer_Model", _model);
            }
            EditorGUILayout.EndHorizontal();

            // 快捷预设按钮
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("快捷预设：", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("OpenAI 官方", EditorStyles.miniButton))
            {
                _baseUrl = "https://api.openai.com/v1";
                _model = "gpt-4.1-mini";
                EditorPrefs.SetString("AI_SmartSlicer_BaseUrl", _baseUrl);
                EditorPrefs.SetString("AI_SmartSlicer_Model", _model);
            }
            if (GUILayout.Button("Moyu 中转", EditorStyles.miniButton))
            {
                _baseUrl = "https://www.moyu.info/v1";
                _model = "gpt-4o";
                EditorPrefs.SetString("AI_SmartSlicer_BaseUrl", _baseUrl);
                EditorPrefs.SetString("AI_SmartSlicer_Model", _model);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("其他设置", EditorStyles.boldLabel);

        // 背景去除
        _removeBackground = EditorGUILayout.Toggle("去除背景色", _removeBackground);
        if (_removeBackground)
        {
            EditorGUI.indentLevel++;
            _bgColor = EditorGUILayout.ColorField("背景色", _bgColor);
            _bgTolerance = EditorGUILayout.IntSlider("容差", _bgTolerance, 0, 100);
            EditorGUI.indentLevel--;
        }
        _outputFolder = EditorGUILayout.TextField("输出目录", _outputFolder);
        _autoImport = EditorGUILayout.Toggle("裁切后自动触发导入管线", _autoImport);
        EditorGUILayout.Space(4);
    }

    private void DrawAnalyzeButton()
    {
        EditorGUILayout.LabelField("3. AI 分析", EditorStyles.boldLabel);

        GUI.enabled = _sourceTexture != null && !string.IsNullOrEmpty(_apiKey) && !_isAnalyzing;
        
        if (GUILayout.Button(_isAnalyzing ? "正在分析中..." : "🔍 AI 分析（识别物体和动画组）", GUILayout.Height(36)))
        {
            RunAnalysis();
        }

        GUI.enabled = true;

        if (!string.IsNullOrEmpty(_statusMessage))
        {
            EditorGUILayout.HelpBox(_statusMessage, 
                _statusMessage.Contains("失败") ? MessageType.Error : MessageType.Info);
        }

        EditorGUILayout.Space(4);
    }

    private void DrawResultsSection()
    {
        if (!_analysisComplete || _analysisResult == null) return;

        EditorGUILayout.LabelField("4. AI 识别结果", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"共识别到 {_analysisResult.objects.Count} 个物体/动画组：");

        EditorGUILayout.Space(4);

        for (int i = 0; i < _analysisResult.objects.Count; i++)
        {
            var obj = _analysisResult.objects[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            obj.selected = EditorGUILayout.Toggle(obj.selected, GUILayout.Width(20));
            
            string typeIcon = obj.type == "animation" ? "🎬" : "🖼️";
            string frameInfo = obj.type == "animation" ? $" ({obj.frame_count}帧, {obj.grid_cols}×{obj.grid_rows})" : "";
            EditorGUILayout.LabelField($"{typeIcon} {obj.name}{frameInfo}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"描述: {obj.description}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"区域: x={obj.bbox.x}, y={obj.bbox.y}, {obj.bbox.w}×{obj.bbox.h}", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // 全选/全不选
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全选", GUILayout.Width(60)))
            _analysisResult.objects.ForEach(o => o.selected = true);
        if (GUILayout.Button("全不选", GUILayout.Width(60)))
            _analysisResult.objects.ForEach(o => o.selected = false);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
    }

    private void DrawExecuteSection()
    {
        if (!_analysisComplete || _analysisResult == null) return;

        int selectedCount = _analysisResult.objects.Count(o => o.selected);
        
        EditorGUILayout.LabelField("5. 执行裁切", EditorStyles.boldLabel);
        
        GUI.enabled = selectedCount > 0;
        if (GUILayout.Button($"✂️ 裁切选中的 {selectedCount} 个物体并导入项目", GUILayout.Height(36)))
        {
            ExecuteSlicing();
        }
        GUI.enabled = true;
    }

    // =========================================================================
    // AI 分析逻辑
    // =========================================================================
    private async void RunAnalysis()
    {
        if (_sourceTexture == null || string.IsNullOrEmpty(_apiKey)) return;

        _isAnalyzing = true;
        _analysisComplete = false;
        _statusMessage = "正在调用 AI 视觉模型分析...";
        Repaint();

        try
        {
            // 确保贴图可读
            string texPath = AssetDatabase.GetAssetPath(_sourceTexture);
            TextureImporter ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti != null && !ti.isReadable)
            {
                ti.isReadable = true;
                ti.SaveAndReimport();
            }

            // 编码图片为 base64
            string base64 = EncodeTextureToBase64(_sourceTexture);
            if (string.IsNullOrEmpty(base64))
            {
                _statusMessage = "分析失败：无法编码贴图（格式不支持）";
                _isAnalyzing = false;
                Repaint();
                return;
            }

            // 调用 API
            string jsonResult = await CallVisionAPI(base64, _sourceTexture.width, _sourceTexture.height);

            if (string.IsNullOrEmpty(jsonResult))
            {
                _statusMessage = "分析失败：AI 未返回有效结果";
                _isAnalyzing = false;
                Repaint();
                return;
            }

            // 解析结果
            _analysisResult = JsonUtility.FromJson<AIAnalysisResult>(jsonResult);
            
            if (_analysisResult == null || _analysisResult.objects == null || _analysisResult.objects.Count == 0)
            {
                // 尝试手动解析（JsonUtility 对嵌套列表有时不可靠）
                _analysisResult = ParseAnalysisManually(jsonResult);
            }

            if (_analysisResult != null && _analysisResult.objects.Count > 0)
            {
                // 默认全选
                foreach (var obj in _analysisResult.objects)
                    obj.selected = true;

                _statusMessage = $"分析完成！识别到 {_analysisResult.objects.Count} 个物体/动画组";
                _analysisComplete = true;
            }
            else
            {
                _statusMessage = "分析完成但未检测到物体，请检查图片";
            }
        }
        catch (Exception ex)
        {
            _statusMessage = $"分析失败: {ex.Message}";
            Debug.LogError($"[AI Smart Slicer] {ex}");
        }

        _isAnalyzing = false;
        Repaint();
    }

    private string EncodeTextureToBase64(Texture2D tex)
    {
        // 始终通过 RenderTexture Blit 拷贝，确保兼容：
        //   - 压缩格式（DXT/BC/ETC/ASTC）
        //   - 不可读贴图（isReadable = false）
        // EncodeToPNG 只支持 RGBA32/RGB24 等未压缩格式。
        int maxSize = 2048;
        int targetW = tex.width;
        int targetH = tex.height;

        if (Mathf.Max(targetW, targetH) > maxSize)
        {
            float scale = (float)maxSize / Mathf.Max(targetW, targetH);
            targetW = Mathf.RoundToInt(targetW * scale);
            targetH = Mathf.RoundToInt(targetH * scale);
        }

        // 一步完成：解压 + 缩放 → 未压缩 RGBA32
        RenderTexture rt = RenderTexture.GetTemporary(targetW, targetH, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Bilinear;
        Graphics.Blit(tex, rt);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D readable = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0);
        readable.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        byte[] pngBytes = readable.EncodeToPNG();
        DestroyImmediate(readable);

        if (pngBytes == null || pngBytes.Length == 0)
        {
            Debug.LogError("[AI Smart Slicer] EncodeToPNG 返回空数据，贴图可能损坏");
            return "";
        }

        return Convert.ToBase64String(pngBytes);
    }
    private async Task<string> CallVisionAPI(string base64Image, int origW, int origH)
    {
        string systemPrompt = @"You are a 2D game art asset analyst. You will receive a sprite sheet or asset collection image.

Your job is to identify every distinct object or animation group in the image and return their bounding boxes.

Rules:
1. If multiple frames of the same character/object are arranged in a row or grid (e.g., walk cycle, attack animation), group them as ONE entry with type ""animation"" and specify the frame count.
2. If an object appears only once (static prop, single tile, etc.), mark it as type ""static"".
3. Coordinates are in pixels, origin at TOP-LEFT corner of the image.
4. Include a short English name for each object (snake_case, descriptive).
5. If frames in an animation group are evenly spaced, provide the grid info (cols, rows).
6. Be precise with bounding boxes — include the full extent of each object/group with ~2px padding but don't include unrelated objects.

Return ONLY valid JSON in this exact format (no markdown, no explanation):
{""image_width"":<int>,""image_height"":<int>,""objects"":[{""name"":""hero_walk_cycle"",""type"":""animation"",""frame_count"":8,""grid_cols"":8,""grid_rows"":1,""bbox"":{""x"":0,""y"":0,""w"":512,""h"":64},""description"":""8-frame walk cycle of the main character""},{""name"":""tree_01"",""type"":""static"",""frame_count"":1,""grid_cols"":1,""grid_rows"":1,""bbox"":{""x"":520,""y"":10,""w"":48,""h"":80},""description"":""Single decorative tree""}]}";

        string userMsg = $"This sprite sheet is {origW}x{origH} pixels. Identify all distinct objects and animation groups. Return bounding boxes in the ORIGINAL {origW}x{origH} coordinate space.";

        // 构建请求 JSON
        string requestBody = $@"{{
  ""model"": ""{_model}"",
  ""messages"": [
    {{""role"": ""system"", ""content"": {EscapeJsonString(systemPrompt)}}},
    {{""role"": ""user"", ""content"": [
      {{""type"": ""text"", ""text"": {EscapeJsonString(userMsg)}}},
      {{""type"": ""image_url"", ""image_url"": {{""url"": ""data:image/png;base64,{base64Image}"", ""detail"": ""high""}}}}
    ]}}
  ],
  ""temperature"": 0.1,
  ""max_tokens"": 4096
}}";

        using (var client = new HttpClient())
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            
            // 使用配置的 Base URL

            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{_baseUrl}/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                Debug.LogError($"[AI Smart Slicer] API Error {response.StatusCode}: {error}");
                return null;
            }

            string responseJson = await response.Content.ReadAsStringAsync();
            
            // 提取 content 字段
            string resultContent = ExtractContentFromResponse(responseJson);
            
            // 清理可能的 markdown 包裹
            if (resultContent != null && resultContent.Contains("```"))
            {
                var lines = resultContent.Split('\n')
                    .Where(l => !l.TrimStart().StartsWith("```"))
                    .ToArray();
                resultContent = string.Join("\n", lines);
            }

            return resultContent;
        }
    }

    private string ExtractContentFromResponse(string responseJson)
    {
        // 简单提取 "content":"..." 字段
        // 找到 "content" 在 choices[0].message 中的位置
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
            // 解析转义字符串
            StringBuilder sb = new StringBuilder();
            int i = start + 1;
            while (i < responseJson.Length)
            {
                if (responseJson[i] == '\\' && i + 1 < responseJson.Length)
                {
                    char next = responseJson[i + 1];
                    switch (next)
                    {
                        case '"': sb.Append('"'); i += 2; break;
                        case '\\': sb.Append('\\'); i += 2; break;
                        case 'n': sb.Append('\n'); i += 2; break;
                        case 'r': sb.Append('\r'); i += 2; break;
                        case 't': sb.Append('\t'); i += 2; break;
                        default: sb.Append(next); i += 2; break;
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

    private AIAnalysisResult ParseAnalysisManually(string json)
    {
        // 手动解析 JSON（因为 Unity 的 JsonUtility 对嵌套对象列表支持有限）
        var result = new AIAnalysisResult();
        result.objects = new List<DetectedObject>();

        try
        {
            // 提取 image_width 和 image_height
            result.image_width = ExtractInt(json, "image_width");
            result.image_height = ExtractInt(json, "image_height");

            // 找到 objects 数组
            int objsStart = json.IndexOf("\"objects\"");
            if (objsStart < 0) return result;

            int arrStart = json.IndexOf('[', objsStart);
            if (arrStart < 0) return result;

            // 逐个解析对象
            int searchFrom = arrStart + 1;
            while (true)
            {
                int objStart = json.IndexOf('{', searchFrom);
                if (objStart < 0) break;

                // 找到匹配的 } （处理嵌套的 bbox {}）
                int depth = 0;
                int objEnd = objStart;
                for (int i = objStart; i < json.Length; i++)
                {
                    if (json[i] == '{') depth++;
                    else if (json[i] == '}') { depth--; if (depth == 0) { objEnd = i; break; } }
                }

                string objJson = json.Substring(objStart, objEnd - objStart + 1);
                
                var detected = new DetectedObject();
                detected.name = ExtractString(objJson, "name") ?? $"object_{result.objects.Count:D3}";
                detected.type = ExtractString(objJson, "type") ?? "static";
                detected.frame_count = Math.Max(1, ExtractInt(objJson, "frame_count"));
                detected.grid_cols = Math.Max(1, ExtractInt(objJson, "grid_cols"));
                detected.grid_rows = Math.Max(1, ExtractInt(objJson, "grid_rows"));
                detected.description = ExtractString(objJson, "description") ?? "";
                detected.selected = true;

                // 解析 bbox
                int bboxStart = objJson.IndexOf("\"bbox\"");
                if (bboxStart >= 0)
                {
                    int bboxObjStart = objJson.IndexOf('{', bboxStart);
                    int bboxObjEnd = objJson.IndexOf('}', bboxObjStart);
                    if (bboxObjStart >= 0 && bboxObjEnd >= 0)
                    {
                        string bboxJson = objJson.Substring(bboxObjStart, bboxObjEnd - bboxObjStart + 1);
                        detected.bbox = new BBox();
                        detected.bbox.x = ExtractInt(bboxJson, "x");
                        detected.bbox.y = ExtractInt(bboxJson, "y");
                        detected.bbox.w = ExtractInt(bboxJson, "w");
                        detected.bbox.h = ExtractInt(bboxJson, "h");
                    }
                }

                if (detected.bbox == null)
                    detected.bbox = new BBox();

                result.objects.Add(detected);
                searchFrom = objEnd + 1;

                // 检查是否到了数组末尾
                int nextComma = json.IndexOf(',', searchFrom);
                int arrEnd = json.IndexOf(']', searchFrom);
                if (arrEnd >= 0 && (nextComma < 0 || arrEnd < nextComma))
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AI Smart Slicer] JSON 解析错误: {ex.Message}");
        }

        return result;
    }

    private static int ExtractInt(string json, string key)
    {
        string pattern = $"\"{key}\"";
        int idx = json.IndexOf(pattern);
        if (idx < 0) return 0;
        int colonIdx = json.IndexOf(':', idx + pattern.Length);
        if (colonIdx < 0) return 0;
        
        int start = colonIdx + 1;
        while (start < json.Length && (json[start] == ' ' || json[start] == '\n' || json[start] == '\r')) start++;
        
        int end = start;
        while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
        
        if (end > start && int.TryParse(json.Substring(start, end - start), out int val))
            return val;
        return 0;
    }

    private static string ExtractString(string json, string key)
    {
        string pattern = $"\"{key}\"";
        int idx = json.IndexOf(pattern);
        if (idx < 0) return null;
        int colonIdx = json.IndexOf(':', idx + pattern.Length);
        if (colonIdx < 0) return null;
        int quoteStart = json.IndexOf('"', colonIdx + 1);
        if (quoteStart < 0) return null;
        
        int quoteEnd = quoteStart + 1;
        while (quoteEnd < json.Length)
        {
            if (json[quoteEnd] == '\\') { quoteEnd += 2; continue; }
            if (json[quoteEnd] == '"') break;
            quoteEnd++;
        }
        
        return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
    }

    private static string EscapeJsonString(string s)
    {
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"";
    }

    // =========================================================================
    // 裁切执行
    // =========================================================================
    private void ExecuteSlicing()
    {
        if (_analysisResult == null || _sourceTexture == null) return;

        var selected = _analysisResult.objects.Where(o => o.selected).ToList();
        if (selected.Count == 0) return;

        // 确保输出目录存在
        EnsureDirectory(_outputFolder);

        // 确保贴图可读
        string texPath = AssetDatabase.GetAssetPath(_sourceTexture);
        TextureImporter ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (ti != null && !ti.isReadable)
        {
            ti.isReadable = true;
            ti.SaveAndReimport();
        }

        // 获取可读的像素数据
        Texture2D readableTex = GetReadableTexture(_sourceTexture);
        if (readableTex == null)
        {
            EditorUtility.DisplayDialog("错误", "无法读取贴图像素数据", "好的");
            return;
        }

        int successCount = 0;
        List<string> createdPaths = new List<string>();

        foreach (var obj in selected)
        {
            try
            {
                int padding = 2;
                int x = Mathf.Max(0, obj.bbox.x - padding);
                int y = Mathf.Max(0, obj.bbox.y - padding);
                int w = Mathf.Min(obj.bbox.w + padding * 2, readableTex.width - x);
                int h = Mathf.Min(obj.bbox.h + padding * 2, readableTex.height - y);

                if (w <= 0 || h <= 0) continue;

                // Unity 贴图坐标 Y 轴从底部开始，而 AI 返回的是从顶部开始
                int unityY = readableTex.height - y - h;

                Color[] pixels = readableTex.GetPixels(x, unityY, w, h);
                
                Texture2D cropTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                cropTex.SetPixels(pixels);
                cropTex.Apply();

                // 可选：去除背景色
                if (_removeBackground)
                {
                    RemoveBackgroundFromTexture(cropTex);
                }

                // 保存
                byte[] pngData = cropTex.EncodeToPNG();
                string fileName = $"{_sourceTexture.name}_{obj.name}";
                
                if (obj.type == "animation" && obj.frame_count > 1)
                    fileName += $"_strip_{obj.frame_count}f";
                
                string savePath = $"{_outputFolder}/{fileName}.png";
                savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);
                
                string fullPath = Path.Combine(Application.dataPath, "..", savePath);
                File.WriteAllBytes(fullPath, pngData);
                createdPaths.Add(savePath);

                DestroyImmediate(cropTex);
                successCount++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Smart Slicer] 裁切 {obj.name} 失败: {ex.Message}");
            }
        }

        if (readableTex != _sourceTexture)
            DestroyImmediate(readableTex);

        // 刷新 AssetDatabase
        AssetDatabase.Refresh();

        _statusMessage = $"裁切完成！{successCount}/{selected.Count} 个物体已保存到 {_outputFolder}";
        
        Debug.Log($"[AI Smart Slicer] 裁切完成: {successCount} 个文件已保存");
        foreach (var p in createdPaths)
            Debug.Log($"  → {p}");

        // 自动打开导入管线
        if (_autoImport && successCount > 0)
        {
            EditorApplication.delayCall += () =>
            {
                AssetImportPipeline.ShowWindow();
            };
        }

        Repaint();
    }

    private Texture2D GetReadableTexture(Texture2D source)
    {
        if (source.isReadable) return source;

        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height);
        Graphics.Blit(source, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        
        Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        readable.Apply();
        
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        
        return readable;
    }

    private void RemoveBackgroundFromTexture(Texture2D tex)
    {
        Color[] pixels = tex.GetPixels();
        float tolerance = _bgTolerance / 255f;
        
        for (int i = 0; i < pixels.Length; i++)
        {
            if (Mathf.Abs(pixels[i].r - _bgColor.r) < tolerance &&
                Mathf.Abs(pixels[i].g - _bgColor.g) < tolerance &&
                Mathf.Abs(pixels[i].b - _bgColor.b) < tolerance)
            {
                pixels[i] = Color.clear;
            }
        }
        
        tex.SetPixels(pixels);
        tex.Apply();
    }

    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}

// =========================================================================
// 简易输入对话框（用于输入 API Key）
// =========================================================================
public class EditorInputDialog : EditorWindow
{
    private string _value = "";
    private string _message = "";
    private bool _confirmed;
    private static string _result;

    public static string Show(string title, string message, string defaultValue = "")
    {
        _result = null;
        var win = CreateInstance<EditorInputDialog>();
        win.titleContent = new GUIContent(title);
        win._message = message;
        win._value = defaultValue ?? "";
        win.minSize = new Vector2(400, 120);
        win.maxSize = new Vector2(600, 120);
        win.ShowModalUtility();
        return _result;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(_message);
        _value = EditorGUILayout.TextField(_value);
        EditorGUILayout.Space(10);
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("确定", GUILayout.Width(80)))
        {
            _result = _value;
            Close();
        }
        if (GUILayout.Button("取消", GUILayout.Width(80)))
        {
            _result = null;
            Close();
        }
        EditorGUILayout.EndHorizontal();
    }
}
#endif
