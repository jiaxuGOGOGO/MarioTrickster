#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Level Template Validator Window — 第五阶段：关卡模板自动化回归与去重。
///
/// The Content Enforcer 负责从源头巡检 LevelSnippetLibrary：
///   1. 对所有 Snippet 执行可达性回归；
///   2. 使用设计标签构建语义指纹；
///   3. 将不可达、重复、未打标签与健康模板分区展示；
///   4. 为不可达模板提供一键复制 AI 修复提示词能力。
///   5. [Apply Tags] 一键将 AI 返回的标签写入 Snippet 字段并持久化到源文件。
///
/// 仅依赖现有 LevelSnippetLibrary 与 LevelReachabilityAnalyzer，不引入外部库。
/// </summary>
public class LevelTemplateValidatorWindow : EditorWindow
{
    // ═══════════════════════════════════════════════════
    // MenuItem 入口
    // ═══════════════════════════════════════════════════

    [MenuItem("MarioTrickster/AI Arena/Level Template Validator (QA)")]
    public static void ShowWindow()
    {
        LevelTemplateValidatorWindow window = GetWindow<LevelTemplateValidatorWindow>("Level Template Validator");
        window.minSize = new Vector2(560f, 420f);
        window.Show();
    }

    // ═══════════════════════════════════════════════════
    // GUI 变量与扫描结果缓存
    // ═══════════════════════════════════════════════════

    private Vector2 scrollPos;

    private List<LevelSnippetLibrary.Snippet> passedSnippets = new List<LevelSnippetLibrary.Snippet>();
    private List<LevelSnippetLibrary.Snippet> brokenSnippets = new List<LevelSnippetLibrary.Snippet>();
    private Dictionary<string, List<LevelSnippetLibrary.Snippet>> redundantSnippets =
        new Dictionary<string, List<LevelSnippetLibrary.Snippet>>();
    private List<LevelSnippetLibrary.Snippet> untaggedSnippets = new List<LevelSnippetLibrary.Snippet>();
    private Dictionary<LevelSnippetLibrary.Snippet, string> errorPrompts =
        new Dictionary<LevelSnippetLibrary.Snippet, string>();

    private bool hasScanned = false;
    private int lastScannedTotal = 0;

    // ═══════════════════════════════════════════════════
    // Apply Tags 功能状态
    // ═══════════════════════════════════════════════════

    /// <summary>每个 Untagged Snippet 对应的标签输入文本（按片段名索引）</summary>
    private Dictionary<string, string> tagInputTexts = new Dictionary<string, string>();

    /// <summary>记录已成功 Apply 的片段名，用于 UI 反馈</summary>
    private HashSet<string> appliedSnippetNames = new HashSet<string>();

    // ═══════════════════════════════════════════════════
    // Unity Editor GUI
    // ═══════════════════════════════════════════════════

    private void OnGUI()
    {
        DrawHeader();

        Color oldBackgroundColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.25f, 0.55f, 1f);
        if (GUILayout.Button("\ud83d\ude80 Run Full Regression & Deduplication", GUILayout.Height(40)))
        {
            RunValidationAndDeduplication();
        }
        GUI.backgroundColor = oldBackgroundColor;

        EditorGUILayout.Space(8f);

        if (!hasScanned)
        {
            EditorGUILayout.HelpBox(
                "点击上方按钮后，将扫描 LevelSnippetLibrary.GetAllSnippets() 返回的全部关卡模板，并按不可达、语义重复、缺少标签与健康模板四类输出 QA 报告。",
                MessageType.Info);
            return;
        }

        DrawSummary();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
        DrawBrokenLevelsBlock();
        DrawRedundantTemplatesBlock();
        DrawUntaggedTemplatesBlock();
        DrawPassedLevelsBlock();
        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(10f);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("Level Template Validator (QA)", titleStyle);

        GUIStyle subtitleStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            wordWrap = true
        };
        EditorGUILayout.LabelField("The Content Enforcer \u2014 Physical Regression + Semantic Deduplication + Apply Tags", subtitleStyle);
        EditorGUILayout.Space(8f);
    }

    private void DrawSummary()
    {
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.LabelField("Scan Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            $"Total: {lastScannedTotal}  |  " +
            $"\u274c Broken: {brokenSnippets.Count}  |  " +
            $"\ud83d\udd04 Redundant Groups: {redundantSnippets.Count}  |  " +
            $"\u26a0\ufe0f Untagged: {untaggedSnippets.Count}  |  " +
            $"\u2705 Passed: {passedSnippets.Count}");
        EditorGUILayout.Space(8f);
    }

    // ═══════════════════════════════════════════════════
    // Block 1: Broken Levels
    // ═══════════════════════════════════════════════════

    private void DrawBrokenLevelsBlock()
    {
        DrawSectionHeader("\u274c Broken Levels", new Color(0.9f, 0.2f, 0.2f));

        if (brokenSnippets.Count == 0)
        {
            EditorGUILayout.HelpBox("未发现物理不可达或格式异常的关卡模板。", MessageType.Info);
            EditorGUILayout.Space(8f);
            return;
        }

        foreach (LevelSnippetLibrary.Snippet snippet in brokenSnippets)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(GetSnippetName(snippet), EditorStyles.boldLabel);

            if (GUILayout.Button("[Copy AI Fix Prompt]", GUILayout.Width(160f)))
            {
                string prompt = errorPrompts.ContainsKey(snippet) ? errorPrompts[snippet] : "";
                EditorGUIUtility.systemCopyBuffer = prompt;
                Debug.Log($"[AI Arena] 已复制修复提示词：{GetSnippetName(snippet)}");
            }

            EditorGUILayout.EndHorizontal();

            string errorPrompt = errorPrompts.ContainsKey(snippet)
                ? errorPrompts[snippet]
                : "No ErrorPrompt was provided by LevelReachabilityAnalyzer.";
            EditorGUILayout.HelpBox(MakeSummary(errorPrompt, 420), MessageType.Error);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(8f);
    }

    // ═══════════════════════════════════════════════════
    // Block 2: Redundant Templates
    // ═══════════════════════════════════════════════════

    private void DrawRedundantTemplatesBlock()
    {
        DrawSectionHeader("\ud83d\udd04 Redundant Templates", new Color(0.9f, 0.7f, 0.1f));

        if (redundantSnippets.Count == 0)
        {
            EditorGUILayout.HelpBox("未发现语义指纹重复的模板组。", MessageType.Info);
            EditorGUILayout.Space(8f);
            return;
        }

        foreach (KeyValuePair<string, List<LevelSnippetLibrary.Snippet>> kvp in redundantSnippets)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Fingerprint: {kvp.Key}", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(
                $"\u26a0\ufe0f 发现 {kvp.Value.Count} 个关卡的博弈解法完全一致：{JoinSnippetNames(kvp.Value)}",
                MessageType.Warning);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(8f);
    }

    // ═══════════════════════════════════════════════════
    // Block 3: Untagged Templates (含 Apply Tags 功能)
    // ═══════════════════════════════════════════════════

    private void DrawUntaggedTemplatesBlock()
    {
        DrawSectionHeader("\u26a0\ufe0f Untagged Templates", new Color(0.6f, 0.6f, 0.6f));

        if (untaggedSnippets.Count == 0)
        {
            EditorGUILayout.HelpBox("所有物理可达模板均已具备可用于语义去重的设计标签。", MessageType.Info);
            EditorGUILayout.Space(8f);
            return;
        }

        foreach (LevelSnippetLibrary.Snippet snippet in untaggedSnippets)
        {
            string snippetKey = GetSnippetName(snippet);
            bool alreadyApplied = appliedSnippetNames.Contains(snippetKey);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ── 标题行：片段名 + [Copy Tag Prompt] ──
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(snippetKey, EditorStyles.boldLabel);
            if (GUILayout.Button("[Copy Tag Prompt]", GUILayout.Width(140f)))
            {
                CopyTagPromptToClipboard(snippet);
            }
            EditorGUILayout.EndHorizontal();

            if (alreadyApplied)
            {
                // 已成功 Apply 的反馈
                EditorGUILayout.HelpBox(
                    "\u2705 标签已成功写入！运行时字段已更新，源文件已持久化。下次 Scan 将归入 Passed。",
                    MessageType.Info);
            }
            else
            {
                // ── 提示文字 ──
                EditorGUILayout.HelpBox(
                    "将 AI 返回的 5 行标签粘贴到下方输入框，点击 [Apply Tags] 即可自动写入。\n" +
                    "格式示例：\n" +
                    "MainRoute: 从左到右，多层平台跳跃\n" +
                    "ShadowRoute: none\n" +
                    "TrapRoles: ^^ = 地刺(落地惩罚)\n" +
                    "Budget: 单路线, 中等压力\n" +
                    "TestGoal: 验证平台间距物理可达",
                    MessageType.None);

                // ── 标签输入框 ──
                if (!tagInputTexts.ContainsKey(snippetKey))
                    tagInputTexts[snippetKey] = "";

                EditorGUILayout.LabelField("粘贴 AI 标签:", EditorStyles.miniLabel);
                tagInputTexts[snippetKey] = EditorGUILayout.TextArea(
                    tagInputTexts[snippetKey],
                    GUILayout.MinHeight(80f));

                // ── [Apply Tags] 按钮 ──
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
                bool applyClicked = GUILayout.Button("\u2705 Apply Tags", GUILayout.Width(120f), GUILayout.Height(26f));
                GUI.backgroundColor = oldBg;

                EditorGUILayout.EndHorizontal();

                if (applyClicked)
                {
                    string inputText = tagInputTexts[snippetKey];
                    if (string.IsNullOrEmpty(inputText) || inputText.Trim().Length == 0)
                    {
                        EditorUtility.DisplayDialog("Apply Tags",
                            "输入框为空，请先粘贴 AI 返回的标签内容。", "OK");
                    }
                    else
                    {
                        ApplyTagsToSnippet(snippet, inputText);
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(8f);
    }

    // ═══════════════════════════════════════════════════
    // Apply Tags 核心逻辑
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 解析 AI 返回的标签文本，写入 Snippet 运行时字段，并持久化回写 LevelSnippetLibrary.cs 源文件。
    /// </summary>
    private void ApplyTagsToSnippet(LevelSnippetLibrary.Snippet snippet, string rawInput)
    {
        // ── Step 1: 解析标签 ──
        TagParseResult parsed = ParseTagsFromAIOutput(rawInput);

        if (!parsed.HasAnyTag)
        {
            EditorUtility.DisplayDialog("Apply Tags - 解析失败",
                "未能从输入文本中识别出任何标签。\n\n" +
                "请确保格式为：\n" +
                "MainRoute: ...\n" +
                "ShadowRoute: ...\n" +
                "TrapRoles: ...\n" +
                "Budget: ...\n" +
                "TestGoal: ...",
                "OK");
            return;
        }

        // ── Step 2: 确认对话框 ──
        string preview =
            $"MainRoute: {parsed.MainRoute}\n" +
            $"ShadowRoute: {parsed.ShadowRoute}\n" +
            $"TrapRoles: {parsed.TrapRoles}\n" +
            $"Budget: {parsed.Budget}\n" +
            $"TestGoal: {parsed.TestGoal}";

        bool confirmed = EditorUtility.DisplayDialog(
            $"Apply Tags - {GetSnippetName(snippet)}",
            $"即将写入以下标签：\n\n{preview}\n\n确认写入？",
            "Apply", "Cancel");

        if (!confirmed)
            return;

        // ── Step 3: 写入运行时字段 ──
        snippet.MainRoute = parsed.MainRoute;
        snippet.ShadowRoute = parsed.ShadowRoute;
        snippet.TrapRoles = parsed.TrapRoles;
        snippet.Budget = parsed.Budget;
        snippet.TestGoal = parsed.TestGoal;

        // ── Step 4: 持久化回写源文件 ──
        bool persisted = PersistTagsToSourceFile(snippet, parsed);

        // ── Step 5: UI 反馈 ──
        string snippetKey = GetSnippetName(snippet);
        appliedSnippetNames.Add(snippetKey);

        if (persisted)
        {
            Debug.Log($"[Validator] \u2705 '{snippetKey}' 标签已写入运行时字段并持久化到 LevelSnippetLibrary.cs。");
        }
        else
        {
            Debug.LogWarning($"[Validator] \u26a0\ufe0f '{snippetKey}' 标签已写入运行时字段，但源文件回写失败。" +
                             "运行时标签在本次 Editor 会话中有效，但下次重编译后可能丢失。请手动检查源文件。");
        }

        Repaint();
    }

    /// <summary>
    /// 解析 AI 返回的标签文本。支持 "Key: Value" 和 "Key：Value" 格式，
    /// 容错处理多余空行、前缀序号等。
    /// </summary>
    private TagParseResult ParseTagsFromAIOutput(string rawInput)
    {
        TagParseResult result = new TagParseResult();

        if (string.IsNullOrEmpty(rawInput))
            return result;

        string[] lines = rawInput.Split('\n');
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            // 尝试匹配 "Key: Value" 或 "Key：Value"（中英文冒号均可）
            string value;

            if (TryExtractTagValue(line, "MainRoute", out value))
                result.MainRoute = value;
            else if (TryExtractTagValue(line, "ShadowRoute", out value))
                result.ShadowRoute = value;
            else if (TryExtractTagValue(line, "TrapRoles", out value))
                result.TrapRoles = value;
            else if (TryExtractTagValue(line, "Budget", out value))
                result.Budget = value;
            else if (TryExtractTagValue(line, "TestGoal", out value))
                result.TestGoal = value;
        }

        return result;
    }

    /// <summary>
    /// 尝试从一行文本中提取指定 Key 的 Value。
    /// 支持格式：Key: Value / Key：Value / key: value（大小写不敏感匹配 Key）
    /// </summary>
    private bool TryExtractTagValue(string line, string key, out string value)
    {
        value = "";

        // 匹配模式：可选前缀（数字/符号）+ Key + 中英文冒号 + 值
        string pattern = @"(?:^[\d\.\-\*\#]*\s*)" + Regex.Escape(key) + @"\s*[:：]\s*(.+)$";
        Match match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            value = match.Groups[1].Value.Trim();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 将标签持久化写入 LevelSnippetLibrary.cs 源文件。
    /// 策略：找到目标 Snippet 的 snippets.Add(new Snippet(...)) 块，
    /// 在 ASCII 字符串之后插入或替换 mainRoute/shadowRoute/trapRoles/budget/testGoal 参数。
    /// </summary>
    private bool PersistTagsToSourceFile(LevelSnippetLibrary.Snippet snippet, TagParseResult tags)
    {
        // 定位源文件
        string[] guids = AssetDatabase.FindAssets("LevelSnippetLibrary t:TextAsset");
        string sourcePath = null;

        // 优先使用 FindAssets，若失败则使用硬编码路径
        if (guids != null && guids.Length > 0)
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("LevelSnippetLibrary.cs"))
                {
                    sourcePath = path;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(sourcePath))
        {
            // Fallback: 使用项目内已知路径
            sourcePath = "Assets/Scripts/Editor/LevelSnippetLibrary.cs";
        }

        string fullPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(Application.dataPath),
            sourcePath);

        if (!System.IO.File.Exists(fullPath))
        {
            Debug.LogError($"[Validator] 源文件不存在: {fullPath}");
            return false;
        }

        string sourceContent = System.IO.File.ReadAllText(fullPath, System.Text.Encoding.UTF8);

        // ── 定位目标 Snippet 的构造调用 ──
        // 匹配模式：new Snippet(\n    "片段名",
        string escapedName = Regex.Escape(snippet.name);
        string snippetPattern = @"new\s+Snippet\s*\(\s*\r?\n\s*""" + escapedName + @"""";
        Match snippetMatch = Regex.Match(sourceContent, snippetPattern);

        if (!snippetMatch.Success)
        {
            Debug.LogError($"[Validator] 未能在源文件中定位 Snippet '{snippet.name}' 的构造调用。");
            return false;
        }

        // 从匹配位置向后找到该 Snippet 构造的闭合 "));""
        int searchStart = snippetMatch.Index;
        int closingIndex = FindSnippetClosingIndex(sourceContent, searchStart);

        if (closingIndex < 0)
        {
            Debug.LogError($"[Validator] 未能找到 Snippet '{snippet.name}' 构造调用的结束位置。");
            return false;
        }

        // 提取该 Snippet 的完整构造块
        string originalBlock = sourceContent.Substring(searchStart, closingIndex - searchStart + 2); // 包含 "));"

        // ── 构建新的参数行 ──
        string newBlock = RebuildSnippetBlock(originalBlock, tags);

        if (newBlock == null)
        {
            Debug.LogError($"[Validator] 重建 Snippet '{snippet.name}' 构造块失败。");
            return false;
        }

        // ── 替换并写回 ──
        string newContent = sourceContent.Substring(0, searchStart) + newBlock +
                            sourceContent.Substring(searchStart + originalBlock.Length);

        System.IO.File.WriteAllText(fullPath, newContent, System.Text.Encoding.UTF8);
        AssetDatabase.Refresh();

        return true;
    }

    /// <summary>
    /// 从 Snippet 构造起始位置向后搜索，找到匹配的 "));""（考虑嵌套括号）。
    /// </summary>
    private int FindSnippetClosingIndex(string source, int startIndex)
    {
        // 找到 "new Snippet(" 中的第一个 '('
        int firstParen = source.IndexOf('(', startIndex);
        if (firstParen < 0) return -1;

        int depth = 0;
        for (int i = firstParen; i < source.Length; i++)
        {
            char c = source[i];

            // 跳过字符串字面量内的括号
            if (c == '"')
            {
                i++;
                while (i < source.Length)
                {
                    if (source[i] == '\\')
                    {
                        i++; // 跳过转义字符
                    }
                    else if (source[i] == '"')
                    {
                        break;
                    }
                    i++;
                }
                continue;
            }

            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    // 找到闭合括号，继续向后找 ");" 模式
                    // 当前 i 指向 Snippet 构造的 ')'
                    // 外层是 snippets.Add(...)，所以还需要再找一个 ')'
                    // 实际上 depth==0 时 i 指向 Add 的 ')'
                    // 向后找 ';'
                    int semiPos = i + 1;
                    while (semiPos < source.Length && source[semiPos] != ';')
                    {
                        if (!char.IsWhiteSpace(source[semiPos]))
                            break;
                        semiPos++;
                    }
                    // 返回 ')' 的位置（即 "));' 中第二个 ')' 的位置）
                    // 但我们需要包含到 ";""
                    if (semiPos < source.Length && source[semiPos] == ';')
                        return semiPos;
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// 重建 Snippet 构造块，插入或替换标签参数。
    /// 保持原有 ASCII 字符串和名称/描述不变，只修改标签参数。
    /// </summary>
    private string RebuildSnippetBlock(string originalBlock, TagParseResult tags)
    {
        // 策略：移除已有的 mainRoute/shadowRoute/trapRoles/budget/testGoal 参数，
        // 然后在构造闭合前重新插入。

        // 检测缩进（从原始块第一行推断）
        string indent = "            "; // 默认 12 空格

        // 移除已有的命名参数行
        string cleaned = originalBlock;
        cleaned = Regex.Replace(cleaned, @",?\s*\r?\n\s*mainRoute:\s*""[^""]*""", "");
        cleaned = Regex.Replace(cleaned, @",?\s*\r?\n\s*shadowRoute:\s*""[^""]*""", "");
        cleaned = Regex.Replace(cleaned, @",?\s*\r?\n\s*trapRoles:\s*""[^""]*""", "");
        cleaned = Regex.Replace(cleaned, @",?\s*\r?\n\s*budget:\s*""[^""]*""", "");
        cleaned = Regex.Replace(cleaned, @",?\s*\r?\n\s*testGoal:\s*""[^""]*""", "");

        // 找到最后一个非标签参数的结尾（ASCII 字符串的最后一个引号后面）
        // 在 cleaned 中找到 "));""
        // 我们需要在最内层 ')' 之前插入标签参数

        // 找到 Snippet 构造的内层闭合 ')' — 即 "new Snippet(...)" 的 ')'
        // 在 cleaned 中，结构是 "new Snippet(\n  name,\n  desc,\n  ascii\n)"
        // 外层是 "snippets.Add(new Snippet(...))" 所以有两层括号

        // 定位内层 Snippet 构造的闭合括号
        int innerFirstParen = cleaned.IndexOf('(');
        if (innerFirstParen < 0) return null;

        // 跳到 "new Snippet(" 的 '('
        int newSnippetParen = cleaned.IndexOf("Snippet(");
        if (newSnippetParen < 0) return null;
        newSnippetParen = cleaned.IndexOf('(', newSnippetParen);

        // 从 Snippet( 开始找匹配的 )
        int depth = 0;
        int snippetCloseIndex = -1;
        for (int i = newSnippetParen; i < cleaned.Length; i++)
        {
            char c = cleaned[i];
            if (c == '"')
            {
                i++;
                while (i < cleaned.Length)
                {
                    if (cleaned[i] == '\\') { i++; }
                    else if (cleaned[i] == '"') { break; }
                    i++;
                }
                continue;
            }
            if (c == '(') depth++;
            else if (c == ')')
            {
                depth--;
                if (depth == 0) { snippetCloseIndex = i; break; }
            }
        }

        if (snippetCloseIndex < 0) return null;

        // 在 snippetCloseIndex 之前插入标签参数
        string beforeClose = cleaned.Substring(0, snippetCloseIndex);
        string afterClose = cleaned.Substring(snippetCloseIndex);

        // 确保 beforeClose 末尾没有多余逗号/空白问题
        string trimmedBefore = beforeClose.TrimEnd();

        // 构建标签参数字符串
        string escapedMainRoute = EscapeCSharpString(tags.MainRoute);
        string escapedShadowRoute = EscapeCSharpString(tags.ShadowRoute);
        string escapedTrapRoles = EscapeCSharpString(tags.TrapRoles);
        string escapedBudget = EscapeCSharpString(tags.Budget);
        string escapedTestGoal = EscapeCSharpString(tags.TestGoal);

        string tagParams =
            $",\n{indent}mainRoute: \"{escapedMainRoute}\"," +
            $"\n{indent}shadowRoute: \"{escapedShadowRoute}\"," +
            $"\n{indent}trapRoles: \"{escapedTrapRoles}\"," +
            $"\n{indent}budget: \"{escapedBudget}\"," +
            $"\n{indent}testGoal: \"{escapedTestGoal}\"";

        // 如果 trimmedBefore 末尾已有逗号，不再加逗号
        if (trimmedBefore.EndsWith(","))
        {
            // 移除 tagParams 开头的逗号
            tagParams = tagParams.Substring(1);
        }

        // 重建：beforeClose(trimmed) + tagParams + \n + indent-1 + afterClose
        string result = trimmedBefore + tagParams + "\n        " + afterClose;

        return result;
    }

    /// <summary>转义 C# 字符串字面量中的特殊字符</summary>
    private string EscapeCSharpString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";
        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    /// <summary>标签解析结果</summary>
    private class TagParseResult
    {
        public string MainRoute = "";
        public string ShadowRoute = "";
        public string TrapRoles = "";
        public string Budget = "";
        public string TestGoal = "";

        public bool HasAnyTag
        {
            get
            {
                return !string.IsNullOrEmpty(MainRoute) ||
                       !string.IsNullOrEmpty(ShadowRoute) ||
                       !string.IsNullOrEmpty(TrapRoles) ||
                       !string.IsNullOrEmpty(Budget) ||
                       !string.IsNullOrEmpty(TestGoal);
            }
        }
    }

    /// <summary>
    /// 生成补标签的 AI Prompt 并复制到系统剪贴板。
    /// 包含片段名称、ASCII 内容和明确的标签补充指令。
    /// </summary>
    private void CopyTagPromptToClipboard(LevelSnippetLibrary.Snippet snippet)
    {
        string asciiContent = snippet != null ? snippet.ascii : "(empty)";
        string snippetName = GetSnippetName(snippet);
        string snippetDesc = snippet != null && !string.IsNullOrEmpty(snippet.description)
            ? snippet.description : "(无描述)";

        string prompt =
$@"以下是一个 2D 平台跳跃关卡片段的 ASCII 地形，请根据实际地形结构分析并补充以下 5 个设计标签。

片段名称: {snippetName}
片段说明: {snippetDesc}

ASCII 地形:
{asciiContent}

字符映射参考:
# = 实心地面(Ground)  = = 平台(Platform)  - = 单向平台(OneWay)
o = 金币(Collectible)  M = 起点(Mario)  G = 终点(GoalZone)
e = 巡逻敌人  E = 弹跳怪  f = 飞行敌人
^ = 地刺(SpikeTrap)  ~ = 火焰(FireTrap)  P = 摆锤(Pendulum)  @ = 锯片(SawBlade)
< = 传送带(Conveyor)  > = 移动平台(Moving)  B = 弹跳平台(Bouncy)
C = 崩塌平台(Collapse)  S = 检查点(Checkpoint)  X = 可破坏方块(Breakable)
[ = 封路机关(ControllableBlocker)  ] = 队列机关(StateQueueTrap)

请输出以下 5 个标签(用中文描述，必须基于实际地形而非猜测):

MainRoute: (主路线方向和特征)
ShadowRoute: (影子路线/备选路线，没有则写 none)
TrapRoles: (每个陷阱/机关符号的角色职责)
Budget: (路线数量、压力级别、容错度)
TestGoal: (这个片段的测试验证目标)

注意: 标签必须基于实际 ASCII 地形中可见的符号和结构，不要编造不存在的元素。";

        EditorGUIUtility.systemCopyBuffer = prompt;
        Debug.Log($"[Validator] 已复制 '{snippetName}' 的补标签 Prompt 到剪贴板。粘贴给 AI 获取标签后，直接在面板中粘贴并点击 [Apply Tags] 即可。");
    }

    // ═══════════════════════════════════════════════════
    // Block 4: Passed Levels
    // ═══════════════════════════════════════════════════

    private void DrawPassedLevelsBlock()
    {
        DrawSectionHeader("\u2705 Passed Levels", new Color(0.1f, 0.8f, 0.3f));
        EditorGUILayout.HelpBox($"共有 {passedSnippets.Count} 个物理可达且标签规范的健康关卡。", MessageType.Info);

        if (passedSnippets.Count > 0)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (LevelSnippetLibrary.Snippet snippet in passedSnippets)
            {
                EditorGUILayout.LabelField($"\u2713 {GetSnippetName(snippet)}");
            }
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(8f);
    }

    private void DrawSectionHeader(string title, Color color)
    {
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            normal = { textColor = color }
        };
        EditorGUILayout.LabelField(title, headerStyle);
        EditorGUILayout.Space(3f);
    }

    // ═══════════════════════════════════════════════════
    // 核心逻辑：物理回归 + 语义指纹去重
    // ═══════════════════════════════════════════════════

    private void RunValidationAndDeduplication()
    {
        ClearResults();
        hasScanned = true;

        List<LevelSnippetLibrary.Snippet> allSnippets = LevelSnippetLibrary.GetAllSnippets();
        lastScannedTotal = allSnippets != null ? allSnippets.Count : 0;

        if (allSnippets == null || allSnippets.Count == 0)
        {
            Debug.LogWarning("[AI Arena] LevelTemplateValidatorWindow 未找到任何 LevelSnippetLibrary.Snippet。");
            return;
        }

        Dictionary<string, List<LevelSnippetLibrary.Snippet>> fingerprintGroups =
            new Dictionary<string, List<LevelSnippetLibrary.Snippet>>();

        foreach (LevelSnippetLibrary.Snippet snippet in allSnippets)
        {
            if (snippet == null)
                continue;

            LevelReachabilityAnalyzer.ReachabilityResult reachResult = AnalyzeSnippetReachability(snippet);
            if (reachResult != null && !reachResult.IsReachable)
            {
                brokenSnippets.Add(snippet);
                errorPrompts[snippet] = !string.IsNullOrEmpty(reachResult.ErrorPrompt)
                    ? reachResult.ErrorPrompt
                    : reachResult.GetReport();
                continue;
            }

            string fingerprint = BuildSemanticFingerprint(snippet);
            if (IsUntaggedFingerprint(fingerprint))
            {
                untaggedSnippets.Add(snippet);
                continue;
            }

            if (!fingerprintGroups.ContainsKey(fingerprint))
            {
                fingerprintGroups[fingerprint] = new List<LevelSnippetLibrary.Snippet>();
            }
            fingerprintGroups[fingerprint].Add(snippet);
        }

        foreach (KeyValuePair<string, List<LevelSnippetLibrary.Snippet>> kvp in fingerprintGroups)
        {
            if (kvp.Value.Count > 1)
            {
                redundantSnippets[kvp.Key] = kvp.Value;
            }
            else if (kvp.Value.Count == 1)
            {
                passedSnippets.Add(kvp.Value[0]);
            }
        }

        Debug.Log(
            $"[AI Arena] Level Template Validator scan complete: total={lastScannedTotal}, " +
            $"broken={brokenSnippets.Count}, redundantGroups={redundantSnippets.Count}, " +
            $"untagged={untaggedSnippets.Count}, passed={passedSnippets.Count}.");
        Debug.Log("[AI Arena] 第五阶段：关卡模板自动化去重与回归验证（The Content Enforcer）部署完成");

        Repaint();
    }

    /// <summary>
    /// 调用现有 LevelReachabilityAnalyzer。
    /// Snippet 默认不强制要求 M/G；若模板显式同时包含 M 与 G，则启用完整 BFS，避免真正完整模板被误放行。
    /// </summary>
    private LevelReachabilityAnalyzer.ReachabilityResult AnalyzeSnippetReachability(LevelSnippetLibrary.Snippet snippet)
    {
        if (snippet == null || string.IsNullOrEmpty(snippet.ascii))
        {
            LevelReachabilityAnalyzer.ReachabilityResult emptyResult =
                new LevelReachabilityAnalyzer.ReachabilityResult();
            emptyResult.IsReachable = false;
            emptyResult.ErrorPrompt = "Snippet ASCII is empty; please provide a valid ASCII template before QA validation.";
            return emptyResult;
        }

        bool hasStart = snippet.ascii.Contains("M");
        bool hasGoal = snippet.ascii.Contains("G");
        bool isSnippetMode = !(hasStart && hasGoal);

        return LevelReachabilityAnalyzer.Analyze(snippet.ascii, isSnippetMode);
    }

    private string BuildSemanticFingerprint(LevelSnippetLibrary.Snippet snippet)
    {
        return $"{snippet.MainRoute}|{snippet.ShadowRoute}|{snippet.TrapRoles}|{snippet.Budget}".Trim().ToLower();
    }

    private bool IsUntaggedFingerprint(string fingerprint)
    {
        if (string.IsNullOrEmpty(fingerprint))
            return true;

        for (int i = 0; i < fingerprint.Length; i++)
        {
            if (char.IsLetterOrDigit(fingerprint[i]))
                return false;
        }

        return true;
    }

    private string JoinSnippetNames(List<LevelSnippetLibrary.Snippet> snippets)
    {
        if (snippets == null || snippets.Count == 0)
            return "(none)";

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < snippets.Count; i++)
        {
            if (i > 0) builder.Append(", ");
            builder.Append(GetSnippetName(snippets[i]));
        }
        return builder.ToString();
    }

    private string GetSnippetName(LevelSnippetLibrary.Snippet snippet)
    {
        if (snippet == null)
            return "(null snippet)";
        return string.IsNullOrEmpty(snippet.name) ? "(unnamed snippet)" : snippet.name;
    }

    private string MakeSummary(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text.Substring(0, maxLength - 3) + "...";
    }

    private void ClearResults()
    {
        passedSnippets.Clear();
        brokenSnippets.Clear();
        redundantSnippets.Clear();
        untaggedSnippets.Clear();
        errorPrompts.Clear();
        lastScannedTotal = 0;
        hasScanned = false;
        // 注意：不清除 tagInputTexts 和 appliedSnippetNames，保留用户输入状态
    }
}
#endif
