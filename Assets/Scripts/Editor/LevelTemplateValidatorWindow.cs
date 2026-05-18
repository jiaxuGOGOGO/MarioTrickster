#if UNITY_EDITOR
using System.Collections.Generic;
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
    // Unity Editor GUI
    // ═══════════════════════════════════════════════════

    private void OnGUI()
    {
        DrawHeader();

        Color oldBackgroundColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.25f, 0.55f, 1f);
        if (GUILayout.Button("🚀 Run Full Regression & Deduplication", GUILayout.Height(40)))
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
        EditorGUILayout.LabelField("The Content Enforcer — Physical Regression + Semantic Deduplication", subtitleStyle);
        EditorGUILayout.Space(8f);
    }

    private void DrawSummary()
    {
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.LabelField("Scan Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            $"Total: {lastScannedTotal}  |  " +
            $"❌ Broken: {brokenSnippets.Count}  |  " +
            $"🔄 Redundant Groups: {redundantSnippets.Count}  |  " +
            $"⚠️ Untagged: {untaggedSnippets.Count}  |  " +
            $"✅ Passed: {passedSnippets.Count}");
        EditorGUILayout.Space(8f);
    }

    // ═══════════════════════════════════════════════════
    // Block 1: Broken Levels
    // ═══════════════════════════════════════════════════

    private void DrawBrokenLevelsBlock()
    {
        DrawSectionHeader("❌ Broken Levels", new Color(0.9f, 0.2f, 0.2f));

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
        DrawSectionHeader("🔄 Redundant Templates", new Color(0.9f, 0.7f, 0.1f));

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
                $"⚠️ 发现 {kvp.Value.Count} 个关卡的博弈解法完全一致：{JoinSnippetNames(kvp.Value)}",
                MessageType.Warning);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(8f);
    }

    // ═══════════════════════════════════════════════════
    // Block 3: Untagged Templates
    // ═══════════════════════════════════════════════════

    private void DrawUntaggedTemplatesBlock()
    {
        DrawSectionHeader("⚠️ Untagged Templates", new Color(0.6f, 0.6f, 0.6f));

        if (untaggedSnippets.Count == 0)
        {
            EditorGUILayout.HelpBox("所有物理可达模板均已具备可用于语义去重的设计标签。", MessageType.Info);
            EditorGUILayout.Space(8f);
            return;
        }

        foreach (LevelSnippetLibrary.Snippet snippet in untaggedSnippets)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(GetSnippetName(snippet), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "缺少可用设计标签。请策划补齐 # MainRoute、# ShadowRoute、# TrapRoles、# Budget 等元数据，以便语义指纹去重。",
                MessageType.None);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(8f);
    }

    // ═══════════════════════════════════════════════════
    // Block 4: Passed Levels
    // ═══════════════════════════════════════════════════

    private void DrawPassedLevelsBlock()
    {
        DrawSectionHeader("✅ Passed Levels", new Color(0.1f, 0.8f, 0.3f));
        EditorGUILayout.HelpBox($"共有 {passedSnippets.Count} 个物理可达且标签规范的健康关卡。", MessageType.Info);

        if (passedSnippets.Count > 0)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (LevelSnippetLibrary.Snippet snippet in passedSnippets)
            {
                EditorGUILayout.LabelField($"✓ {GetSnippetName(snippet)}");
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
    }
}
#endif
