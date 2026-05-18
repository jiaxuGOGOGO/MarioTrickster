#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Level Template Validator Window — 关卡模板片段 QA 校验工具
///
/// 核心职责：
///   扫描 LevelSnippetLibrary 中所有 Snippet，执行物理可达性、标签完整性、
///   冗余检测等校验，将结果分类展示（通过 / 损坏 / 冗余 / 未标记），
///   辅助关卡设计师快速定位问题片段。
///
/// 仅依赖 Unity 原生库，零第三方依赖。
/// </summary>
public class LevelTemplateValidatorWindow : EditorWindow
{
    // ═══════════════════════════════════════════════════
    // MenuItem 入口
    // ═══════════════════════════════════════════════════
    [MenuItem("MarioTrickster/AI Arena/Level Template Validator (QA)")]
    public static void ShowWindow()
    {
        var window = GetWindow<LevelTemplateValidatorWindow>("Level Template Validator");
        window.minSize = new Vector2(520, 400);
        window.Show();
    }

    // ═══════════════════════════════════════════════════
    // GUI 状态变量
    // ═══════════════════════════════════════════════════
    private Vector2 scrollPos;

    // ═══════════════════════════════════════════════════
    // 扫描结果缓存
    // ═══════════════════════════════════════════════════

    /// <summary>通过校验的片段列表</summary>
    private List<LevelSnippetLibrary.Snippet> passedSnippets = new List<LevelSnippetLibrary.Snippet>();

    /// <summary>存在物理缺陷或格式错误的片段列表</summary>
    private List<LevelSnippetLibrary.Snippet> brokenSnippets = new List<LevelSnippetLibrary.Snippet>();

    /// <summary>冗余片段（按重复特征分组）：key 为冗余特征描述，value 为该组重复片段</summary>
    private Dictionary<string, List<LevelSnippetLibrary.Snippet>> redundantSnippets =
        new Dictionary<string, List<LevelSnippetLibrary.Snippet>>();

    /// <summary>缺少关键标签（MainRoute/TrapRoles/TestGoal 等）的片段列表</summary>
    private List<LevelSnippetLibrary.Snippet> untaggedSnippets = new List<LevelSnippetLibrary.Snippet>();

    /// <summary>每个问题片段对应的详细报错信息</summary>
    private Dictionary<LevelSnippetLibrary.Snippet, string> errorPrompts =
        new Dictionary<LevelSnippetLibrary.Snippet, string>();

    /// <summary>是否已执行过扫描</summary>
    private bool hasScanned = false;

    // ═══════════════════════════════════════════════════
    // 生命周期
    // ═══════════════════════════════════════════════════
    private void OnEnable()
    {
        ClearResults();
    }

    // ═══════════════════════════════════════════════════
    // OnGUI
    // ═══════════════════════════════════════════════════
    private void OnGUI()
    {
        // ── 窗口标题 ──
        EditorGUILayout.Space(10);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("Level Template Validator (QA)", titleStyle);
        EditorGUILayout.Space(8);

        // ── 醒目大按钮 ──
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        Color originalBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
        GUIStyle bigButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 13
        };
        if (GUILayout.Button("\U0001F680 Run Full Regression & Deduplication", bigButtonStyle, GUILayout.Height(40)))
        {
            RunValidationAndDeduplication();
        }
        GUI.backgroundColor = originalBg;
        EditorGUILayout.Space(8);

        if (!hasScanned)
        {
            EditorGUILayout.HelpBox("Click the button above to scan all snippets in LevelSnippetLibrary.", MessageType.Info);
            return;
        }

        // ── 结果摘要 ──
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUIStyle summaryStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        EditorGUILayout.LabelField("Scan Summary", summaryStyle);
        EditorGUILayout.Space(4);

        int totalSnippets = LevelSnippetLibrary.GetAllSnippets().Count;
        EditorGUILayout.LabelField($"Total Snippets: {totalSnippets}");
        EditorGUILayout.LabelField(
            $"\u2705 Passed: {passedSnippets.Count}  |  " +
            $"\u274C Broken: {brokenSnippets.Count}  |  " +
            $"\u26A0\uFE0F Untagged: {untaggedSnippets.Count}  |  " +
            $"\U0001F504 Redundant Groups: {redundantSnippets.Count}");

        EditorGUILayout.Space(10);

        // ── 详细结果滚动区 ──
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));

        // ════════════════════════════════════════════════
        // Block 1: ❌ Broken Levels (红色)
        // ════════════════════════════════════════════════
        if (brokenSnippets.Count > 0)
        {
            GUIStyle brokenHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.9f, 0.2f, 0.2f) }
            };
            EditorGUILayout.LabelField("\u274C Broken Levels (Physical / Format Errors)", brokenHeader);
            EditorGUILayout.Space(4);

            foreach (var snippet in brokenSnippets)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(snippet.name, EditorStyles.boldLabel);

                // Copy AI Fix Prompt 按钮
                if (errorPrompts.ContainsKey(snippet))
                {
                    if (GUILayout.Button("[Copy AI Fix Prompt]", GUILayout.Width(150)))
                    {
                        EditorGUIUtility.systemCopyBuffer = errorPrompts[snippet];
                        Debug.Log($"[Level Template Validator] \u2705 已复制修复提示词: {snippet.name}");
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (errorPrompts.ContainsKey(snippet))
                {
                    // 截取摘要显示，避免过长
                    string summary = errorPrompts[snippet];
                    if (summary.Length > 300)
                        summary = summary.Substring(0, 297) + "...";
                    EditorGUILayout.HelpBox(summary, MessageType.Error);
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.Space(10);
        }

        // ════════════════════════════════════════════════
        // Block 2: \U0001F504 Redundant Templates (黄色)
        // ════════════════════════════════════════════════
        if (redundantSnippets.Count > 0)
        {
            GUIStyle redundantHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.9f, 0.7f, 0.1f) }
            };
            EditorGUILayout.LabelField("\U0001F504 Redundant Templates (Semantic Duplicates)", redundantHeader);
            EditorGUILayout.Space(4);

            foreach (var kvp in redundantSnippets)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // 指纹信息
                EditorGUILayout.LabelField($"Fingerprint: {kvp.Key}", EditorStyles.miniLabel);

                // 列出共享该指纹的关卡名称
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append($"\u26A0\uFE0F 发现 {kvp.Value.Count} 个关卡的博弈解法完全一致\uFF1a");
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(kvp.Value[i].name);
                }
                EditorGUILayout.HelpBox(sb.ToString(), MessageType.Warning);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.Space(10);
        }

        // ════════════════════════════════════════════════
        // Block 3: \u26A0\uFE0F Untagged Templates (灰色)
        // ════════════════════════════════════════════════
        if (untaggedSnippets.Count > 0)
        {
            GUIStyle untaggedHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            EditorGUILayout.LabelField("\u26A0\uFE0F Untagged Templates (Missing Design Tags)", untaggedHeader);
            EditorGUILayout.Space(4);

            foreach (var snippet in untaggedSnippets)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(snippet.name, EditorStyles.boldLabel);
                string tagHint = "请补齐设计标签: # MainRoute, # ShadowRoute, # TrapRoles, # Budget";
                if (errorPrompts.ContainsKey(snippet))
                {
                    tagHint = errorPrompts[snippet] + "\n" + tagHint;
                }
                EditorGUILayout.HelpBox(tagHint, MessageType.Info);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.Space(10);
        }

        // ════════════════════════════════════════════════
        // Block 4: \u2705 Passed Levels (绿色)
        // ════════════════════════════════════════════════
        if (passedSnippets.Count > 0)
        {
            GUIStyle passedHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.1f, 0.8f, 0.3f) }
            };
            EditorGUILayout.LabelField($"\u2705 Passed Levels ({passedSnippets.Count} healthy)", passedHeader);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (var snippet in passedSnippets)
            {
                EditorGUILayout.LabelField($"  \u2713 {snippet.name}");
            }
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();
    }

    // ═══════════════════════════════════════════════════
    // 校验逻辑（物理回归 + 语义检查 + 冗余检测）
    // ═══════════════════════════════════════════════════

    /// <summary>旧入口保留，委托给新方法</summary>
    private void RunValidation()
    {
        RunValidationAndDeduplication();
    }

    /// <summary>
    /// 核心校验方法：清空缓存 → 物理可达性回归 → 语义标签检查 → 冗余检测。
    /// 每次点击时清空上轮结果，重新全量扫描。
    /// </summary>
    private void RunValidationAndDeduplication()
    {
        ClearResults();
        hasScanned = true;

        List<LevelSnippetLibrary.Snippet> allSnippets = LevelSnippetLibrary.GetAllSnippets();
        if (allSnippets == null || allSnippets.Count == 0)
        {
            Debug.LogWarning("[Level Template Validator] No snippets found in LevelSnippetLibrary.");
            return;
        }

        // 物理可达的片段暂存列表，等待第二阶段语义指纹去重
        List<LevelSnippetLibrary.Snippet> physicallyReachable = new List<LevelSnippetLibrary.Snippet>();

        foreach (var snippet in allSnippets)
        {
            bool isBroken = false;
            List<string> errors = new List<string>();

            // ── 基础格式校验 ──
            if (string.IsNullOrWhiteSpace(snippet.ascii))
            {
                errors.Add("ASCII content is empty or whitespace-only.");
                isBroken = true;
            }

            if (snippet.width <= 0 || snippet.height <= 0)
            {
                errors.Add($"Invalid dimensions: width={snippet.width}, height={snippet.height}.");
                isBroken = true;
            }

            // ── 物理可达性回归测试 (Physical Regression) ──
            // 传 true 声明为 Snippet 模式，避免强校验 M/G 点报错。
            // 对于包含 M 和 G 的片段，传 false 以触发完整 BFS 可达性分析。
            if (!isBroken && !string.IsNullOrWhiteSpace(snippet.ascii))
            {
                bool hasStart = snippet.ascii.Contains("M");
                bool hasGoal = snippet.ascii.Contains("G");
                bool useSnippetMode = !(hasStart && hasGoal);

                var reachResult = LevelReachabilityAnalyzer.Analyze(snippet.ascii, useSnippetMode);

                if (reachResult != null && !reachResult.IsReachable)
                {
                    isBroken = true;
                    string reachError = "[Physical Regression] BFS unreachable: ";
                    if (!string.IsNullOrEmpty(reachResult.ErrorPrompt))
                    {
                        reachError += reachResult.ErrorPrompt;
                    }
                    else
                    {
                        reachError += reachResult.GetReport();
                    }
                    errors.Add(reachError);
                }
            }

            // ── 分类（第一轮：仅处理 broken）──
            if (isBroken)
            {
                brokenSnippets.Add(snippet);
                errorPrompts[snippet] = string.Join("\n", errors);
            }
            else
            {
                // 物理可达的片段暂存，等待语义指纹去重
                physicallyReachable.Add(snippet);
            }
        }

        // ═══════════════════════════════════════════════════
        // 第二阶段：语义指纹去重 (Semantic Deduplication)
        // ═══════════════════════════════════════════════════
        Dictionary<string, List<LevelSnippetLibrary.Snippet>> fingerprintGroups =
            new Dictionary<string, List<LevelSnippetLibrary.Snippet>>();

        foreach (var snippet in physicallyReachable)
        {
            // 构建语义指纹：将标签组合为小写字符串
            string fingerprint = $"{snippet.MainRoute}|{snippet.ShadowRoute}|{snippet.TrapRoles}|{snippet.Budget}".Trim().ToLower();

            // 判断指纹是否为空/无效（全是分隔符或不含有效字母数字）
            bool isEmptyFingerprint = true;
            foreach (char c in fingerprint)
            {
                if (char.IsLetterOrDigit(c))
                {
                    isEmptyFingerprint = false;
                    break;
                }
            }

            if (isEmptyFingerprint)
            {
                // 未打标签
                untaggedSnippets.Add(snippet);
                List<string> missingTags = new List<string>();
                if (string.IsNullOrEmpty(snippet.MainRoute)) missingTags.Add("MainRoute");
                if (string.IsNullOrEmpty(snippet.ShadowRoute)) missingTags.Add("ShadowRoute");
                if (string.IsNullOrEmpty(snippet.TrapRoles)) missingTags.Add("TrapRoles");
                if (string.IsNullOrEmpty(snippet.Budget)) missingTags.Add("Budget");
                errorPrompts[snippet] = "Missing tags (empty semantic fingerprint): " + string.Join(", ", missingTags);
            }
            else
            {
                // 按指纹分组
                if (!fingerprintGroups.ContainsKey(fingerprint))
                {
                    fingerprintGroups[fingerprint] = new List<LevelSnippetLibrary.Snippet>();
                }
                fingerprintGroups[fingerprint].Add(snippet);
            }
        }

        // 遍历指纹分组：Count > 1 为冗余，Count == 1 为通过
        foreach (var kvp in fingerprintGroups)
        {
            if (kvp.Value.Count > 1)
            {
                // 语义冗余：相同设计意图的多个片段
                string groupLabel = $"Semantic Duplicate ({kvp.Value.Count} snippets): {kvp.Key}";
                // 截断过长的指纹标签用于显示
                if (groupLabel.Length > 120)
                    groupLabel = groupLabel.Substring(0, 117) + "...";
                redundantSnippets[groupLabel] = kvp.Value;
            }
            else
            {
                // 设计意图唯一且物理可达 → 通过
                passedSnippets.Add(kvp.Value[0]);
            }
        }

        Debug.Log($"[Level Template Validator] Scan complete: {passedSnippets.Count} passed, " +
            $"{brokenSnippets.Count} broken, {untaggedSnippets.Count} untagged, " +
            $"{redundantSnippets.Count} redundant groups.");

        Repaint();
    }

    private void ClearResults()
    {
        passedSnippets.Clear();
        brokenSnippets.Clear();
        redundantSnippets.Clear();
        untaggedSnippets.Clear();
        errorPrompts.Clear();
        hasScanned = false;
    }
}
#endif
