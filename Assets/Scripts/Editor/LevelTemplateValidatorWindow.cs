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
        EditorGUILayout.Space(10);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("Level Template Validator (QA)", titleStyle);
        EditorGUILayout.Space(6);

        // ── 操作按钮 ──
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        if (GUILayout.Button("Run Full Validation Scan (Physical Regression)", GUILayout.Height(30)))
        {
            RunValidationAndDeduplication();
        }
        EditorGUILayout.Space(8);

        if (!hasScanned)
        {
            EditorGUILayout.HelpBox("Click the button above to scan all snippets in LevelSnippetLibrary.", MessageType.Info);
            return;
        }

        // ── 结果摘要 ──
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUIStyle sectionHeader = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        EditorGUILayout.LabelField("Scan Summary", sectionHeader);
        EditorGUILayout.Space(4);

        int totalSnippets = LevelSnippetLibrary.GetAllSnippets().Count;
        EditorGUILayout.LabelField($"Total Snippets: {totalSnippets}");
        EditorGUILayout.LabelField($"Passed: {passedSnippets.Count}  |  Broken: {brokenSnippets.Count}  |  Untagged: {untaggedSnippets.Count}  |  Redundant Groups: {redundantSnippets.Count}");

        EditorGUILayout.Space(8);

        // ── 详细结果滚动区 ──
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.LabelField("Detailed Results", sectionHeader);
        EditorGUILayout.Space(4);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));

        // Broken
        if (brokenSnippets.Count > 0)
        {
            EditorGUILayout.LabelField("BROKEN (Physics / Format Errors)", EditorStyles.miniBoldLabel);
            foreach (var snippet in brokenSnippets)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(snippet.name, EditorStyles.boldLabel);
                if (errorPrompts.ContainsKey(snippet))
                {
                    EditorGUILayout.HelpBox(errorPrompts[snippet], MessageType.Error);
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(6);
        }

        // Untagged
        if (untaggedSnippets.Count > 0)
        {
            EditorGUILayout.LabelField("UNTAGGED (Missing Metadata)", EditorStyles.miniBoldLabel);
            foreach (var snippet in untaggedSnippets)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(snippet.name, EditorStyles.boldLabel);
                if (errorPrompts.ContainsKey(snippet))
                {
                    EditorGUILayout.HelpBox(errorPrompts[snippet], MessageType.Warning);
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(6);
        }

        // Redundant
        if (redundantSnippets.Count > 0)
        {
            EditorGUILayout.LabelField("REDUNDANT (Duplicate Patterns)", EditorStyles.miniBoldLabel);
            foreach (var kvp in redundantSnippets)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Group: {kvp.Key}", EditorStyles.boldLabel);
                foreach (var snippet in kvp.Value)
                {
                    EditorGUILayout.LabelField($"  - {snippet.name}");
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(6);
        }

        // Passed
        if (passedSnippets.Count > 0)
        {
            EditorGUILayout.LabelField("PASSED", EditorStyles.miniBoldLabel);
            foreach (var snippet in passedSnippets)
            {
                EditorGUILayout.LabelField($"  \u2713 {snippet.name}");
            }
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

        // 冗余检测用的临时字典：以 ascii 内容的 hash 为 key
        Dictionary<string, List<LevelSnippetLibrary.Snippet>> asciiHashGroups =
            new Dictionary<string, List<LevelSnippetLibrary.Snippet>>();

        foreach (var snippet in allSnippets)
        {
            bool isBroken = false;
            bool isUntagged = false;
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

            // ── 语义标签完整性校验（仅在物理可达时进入）──
            if (!isBroken)
            {
                List<string> missingTags = new List<string>();
                if (string.IsNullOrEmpty(snippet.MainRoute)) missingTags.Add("MainRoute");
                if (string.IsNullOrEmpty(snippet.TrapRoles)) missingTags.Add("TrapRoles");
                if (string.IsNullOrEmpty(snippet.TestGoal)) missingTags.Add("TestGoal");

                if (missingTags.Count > 0)
                {
                    isUntagged = true;
                    errors.Add("Missing tags: " + string.Join(", ", missingTags));
                }
            }

            // ── 冗余检测（按 ASCII 内容分组）──
            string asciiKey = snippet.ascii != null ? snippet.ascii.Trim() : "";
            if (!asciiHashGroups.ContainsKey(asciiKey))
            {
                asciiHashGroups[asciiKey] = new List<LevelSnippetLibrary.Snippet>();
            }
            asciiHashGroups[asciiKey].Add(snippet);

            // ── 分类 ──
            if (isBroken)
            {
                brokenSnippets.Add(snippet);
                errorPrompts[snippet] = string.Join("\n", errors);
            }
            else if (isUntagged)
            {
                untaggedSnippets.Add(snippet);
                errorPrompts[snippet] = string.Join("\n", errors);
            }
            else
            {
                passedSnippets.Add(snippet);
            }
        }

        // ── 冗余分组：超过 1 个片段共享相同 ASCII 内容 ──
        foreach (var kvp in asciiHashGroups)
        {
            if (kvp.Value.Count > 1)
            {
                string groupLabel = $"Identical ASCII ({kvp.Value.Count} snippets, {kvp.Value[0].width}x{kvp.Value[0].height})";
                redundantSnippets[groupLabel] = kvp.Value;
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
