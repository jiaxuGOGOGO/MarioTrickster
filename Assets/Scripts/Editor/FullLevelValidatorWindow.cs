#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Full Level Validator — 完整关卡级质检 + 模板保存/新增/替换。
///
/// 比片段级 Validator 更严格的检查：
///   1. 物理可达性（M→G 全图连通）
///   2. 起点/终点存在性（必须有且仅有 1 个 M 和 1 个 G）
///   3. 孤立平台/死路检测
///   4. Unknown Element 报警（场景中无法识别的对象）
///   5. 金币可收集性检测（所有金币是否可达）
///   6. 敌人/机关密度统计
///   7. 关卡尺寸合理性
///
/// 同时提供完整关卡模板的保存/新增/替换功能：
///   - 从场景 Bake 为 ASCII
///   - 新增为内置模板或替换已有模板
///   - 自动写入 AsciiLevelGenerator.cs
///
/// 仅依赖项目内现有类，不引入外部库。
/// </summary>
public class FullLevelValidatorWindow : EditorWindow
{
    // ═══════════════════════════════════════════════════
    // MenuItem 入口
    // ═══════════════════════════════════════════════════

    [MenuItem("MarioTrickster/AI Arena/Full Level Validator (QA)")]
    public static void ShowWindow()
    {
        FullLevelValidatorWindow window = GetWindow<FullLevelValidatorWindow>("Full Level Validator");
        window.minSize = new Vector2(520f, 500f);
        window.Show();
    }

    // ═══════════════════════════════════════════════════
    // 数据结构
    // ═══════════════════════════════════════════════════

    private class ValidationReport
    {
        public bool hasRun = false;
        public string sourceName = "";
        public string ascii = "";
        public int width;
        public int height;

        // 检查结果
        public bool hasMario = false;
        public bool hasGoal = false;
        public int marioCount = 0;
        public int goalCount = 0;
        public bool isReachable = false;
        public string reachabilityDetail = "";

        // 金币检测
        public int totalCoins = 0;
        public int reachableCoins = 0;
        public List<Vector2Int> unreachableCoinPositions = new List<Vector2Int>();

        // 密度统计
        public int enemyCount = 0;
        public int trapCount = 0;
        public int platformCount = 0;
        public float enemyDensity = 0f; // 每 10 格
        public float trapDensity = 0f;

        // 孤立平台
        public List<string> isolatedPlatforms = new List<string>();

        // Unknown Elements（场景级）
        public List<string> unknownElements = new List<string>();

        // 尺寸合理性
        public bool sizeReasonable = true;
        public string sizeWarning = "";

        // 总评
        public bool passed = false;
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
    }

    // ═══════════════════════════════════════════════════
    // GUI 状态
    // ═══════════════════════════════════════════════════

    private const string GENERATED_ROOT_NAME = "AsciiLevel_Root";

    private ValidationReport report;
    private Vector2 scrollPos;
    private bool showDetails = true;
    private bool showDensity = false;
    private bool showSaveSection = false;

    // 模板保存
    private enum SaveMode { AddNew, ReplaceExisting }
    private SaveMode saveMode = SaveMode.AddNew;
    private string newTemplateName = "";
    private int replaceIndex = 0;
    private string[] existingTemplateNames;

    // ═══════════════════════════════════════════════════
    // OnGUI
    // ═══════════════════════════════════════════════════

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("Full Level Validator (QA)", titleStyle);

        GUIStyle subtitleStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true };
        EditorGUILayout.LabelField("Scene-Level Quality Gate \u2014 Reachability + Coins + Density + Isolation + Save", subtitleStyle);
        EditorGUILayout.Space(6f);

        // ── 验证按钮 ──
        Color oldBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
        if (GUILayout.Button("Validate Current Scene Level", GUILayout.Height(36f)))
        {
            RunValidation();
        }
        GUI.backgroundColor = oldBg;

        EditorGUILayout.Space(6f);

        if (report == null || !report.hasRun)
        {
            EditorGUILayout.HelpBox(
                "\u70b9\u51fb\u4e0a\u65b9\u6309\u94ae\uff0c\u5c06\u5bf9\u573a\u666f\u4e2d\u5df2\u751f\u6210\u7684\u5b8c\u6574\u5173\u5361\u8fdb\u884c\u5168\u9762\u8d28\u68c0\u3002\n\n" +
                "\u68c0\u6d4b\u9879\uff1a\n" +
                "\u2022 \u7269\u7406\u53ef\u8fbe\u6027\uff08M\u2192G \u5168\u56fe\u8fde\u901a\uff09\n" +
                "\u2022 \u8d77\u70b9/\u7ec8\u70b9\u5b58\u5728\u6027\uff08\u5fc5\u987b\u6709\u4e14\u4ec5\u6709 1 \u4e2a M \u548c 1 \u4e2a G\uff09\n" +
                "\u2022 \u91d1\u5e01\u53ef\u6536\u96c6\u6027\uff08\u6240\u6709\u91d1\u5e01\u662f\u5426\u53ef\u8fbe\uff09\n" +
                "\u2022 \u5b64\u7acb\u5e73\u53f0/\u6b7b\u8def\u68c0\u6d4b\n" +
                "\u2022 Unknown Element \u62a5\u8b66\n" +
                "\u2022 \u654c\u4eba/\u673a\u5173\u5bc6\u5ea6\u7edf\u8ba1\n" +
                "\u2022 \u5173\u5361\u5c3a\u5bf8\u5408\u7406\u6027",
                MessageType.Info);
            return;
        }

        // ── 结果显示 ──
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        DrawValidationResults();
        EditorGUILayout.Space(12f);
        DrawSaveSection();
        EditorGUILayout.EndScrollView();
    }

    // ═══════════════════════════════════════════════════
    // 验证结果绘制
    // ═══════════════════════════════════════════════════

    private void DrawValidationResults()
    {
        // 总评
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        if (report.passed)
        {
            EditorGUILayout.HelpBox(
                $"\u2705 \u5173\u5361 '{report.sourceName}' \u901a\u8fc7\u5168\u90e8\u8d28\u68c0\uff01\n" +
                $"\u5c3a\u5bf8: {report.width}\u00d7{report.height} | \u654c\u4eba: {report.enemyCount} | \u673a\u5173: {report.trapCount} | \u91d1\u5e01: {report.totalCoins}",
                MessageType.Info);
        }
        else
        {
            string errorSummary = $"\u274c \u5173\u5361 '{report.sourceName}' \u672a\u901a\u8fc7\u8d28\u68c0\u3002\n\u53d1\u73b0 {report.errors.Count} \u4e2a\u9519\u8bef\u3001{report.warnings.Count} \u4e2a\u8b66\u544a\u3002";
            EditorGUILayout.HelpBox(errorSummary, MessageType.Error);
        }

        EditorGUILayout.Space(4f);

        // 详细检查项
        showDetails = EditorGUILayout.Foldout(showDetails, "\u8be6\u7ec6\u68c0\u67e5\u7ed3\u679c", true, EditorStyles.foldoutHeader);
        if (showDetails)
        {
            EditorGUI.indentLevel++;

            // 1. 起点/终点
            DrawCheckItem("\u8d77\u70b9 (M)", report.hasMario && report.marioCount == 1,
                report.hasMario ? (report.marioCount == 1 ? "\u2705 \u627e\u5230 1 \u4e2a Mario \u8d77\u70b9" : $"\u26a0\ufe0f \u627e\u5230 {report.marioCount} \u4e2a Mario\uff08\u5e94\u4e3a 1 \u4e2a\uff09") : "\u274c \u672a\u627e\u5230 Mario \u8d77\u70b9 (M)");

            DrawCheckItem("\u7ec8\u70b9 (G)", report.hasGoal && report.goalCount == 1,
                report.hasGoal ? (report.goalCount == 1 ? "\u2705 \u627e\u5230 1 \u4e2a Goal \u7ec8\u70b9" : $"\u26a0\ufe0f \u627e\u5230 {report.goalCount} \u4e2a Goal\uff08\u5e94\u4e3a 1 \u4e2a\uff09") : "\u274c \u672a\u627e\u5230 Goal \u7ec8\u70b9 (G)");

            // 2. 物理可达性
            DrawCheckItem("\u7269\u7406\u53ef\u8fbe\u6027", report.isReachable, report.reachabilityDetail);

            // 3. 金币可收集性
            if (report.totalCoins > 0)
            {
                bool coinsOk = report.reachableCoins == report.totalCoins;
                DrawCheckItem("\u91d1\u5e01\u53ef\u8fbe\u6027", coinsOk,
                    coinsOk ? $"\u2705 \u5168\u90e8 {report.totalCoins} \u4e2a\u91d1\u5e01\u5747\u53ef\u8fbe" :
                    $"\u274c {report.totalCoins - report.reachableCoins}/{report.totalCoins} \u4e2a\u91d1\u5e01\u4e0d\u53ef\u8fbe");
            }
            else
            {
                DrawCheckItem("\u91d1\u5e01\u53ef\u8fbe\u6027", true, "\u2705 \u65e0\u91d1\u5e01\uff08\u8df3\u8fc7\uff09");
            }

            // 4. 孤立平台
            bool noIsolation = report.isolatedPlatforms.Count == 0;
            DrawCheckItem("\u5b64\u7acb\u5e73\u53f0\u68c0\u6d4b", noIsolation,
                noIsolation ? "\u2705 \u65e0\u5b64\u7acb\u5e73\u53f0" :
                $"\u26a0\ufe0f \u53d1\u73b0 {report.isolatedPlatforms.Count} \u4e2a\u7591\u4f3c\u5b64\u7acb\u5e73\u53f0");
            if (!noIsolation)
            {
                foreach (string iso in report.isolatedPlatforms)
                    EditorGUILayout.LabelField($"    \u2022 {iso}", EditorStyles.miniLabel);
            }

            // 5. Unknown Elements
            bool noUnknown = report.unknownElements.Count == 0;
            DrawCheckItem("Unknown Element", noUnknown,
                noUnknown ? "\u2705 \u65e0\u672a\u8bc6\u522b\u5143\u7d20" :
                $"\u26a0\ufe0f \u53d1\u73b0 {report.unknownElements.Count} \u4e2a\u672a\u8bc6\u522b\u5143\u7d20");
            if (!noUnknown)
            {
                int showMax = Mathf.Min(report.unknownElements.Count, 10);
                for (int i = 0; i < showMax; i++)
                    EditorGUILayout.LabelField($"    \u2022 {report.unknownElements[i]}", EditorStyles.miniLabel);
                if (report.unknownElements.Count > showMax)
                    EditorGUILayout.LabelField($"    ... \u53ca\u5176\u4ed6 {report.unknownElements.Count - showMax} \u4e2a", EditorStyles.miniLabel);
            }

            // 6. 尺寸合理性
            DrawCheckItem("\u5c3a\u5bf8\u5408\u7406\u6027", report.sizeReasonable,
                report.sizeReasonable ? $"\u2705 {report.width}\u00d7{report.height} \u5728\u5408\u7406\u8303\u56f4\u5185" : report.sizeWarning);

            EditorGUI.indentLevel--;
        }

        // 密度统计
        showDensity = EditorGUILayout.Foldout(showDensity, "\u5bc6\u5ea6\u7edf\u8ba1", true, EditorStyles.foldoutHeader);
        if (showDensity)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"\u654c\u4eba\u6570\u91cf: {report.enemyCount}  (\u5bc6\u5ea6: {report.enemyDensity:F2}/10\u683c)");
            EditorGUILayout.LabelField($"\u673a\u5173\u6570\u91cf: {report.trapCount}  (\u5bc6\u5ea6: {report.trapDensity:F2}/10\u683c)");
            EditorGUILayout.LabelField($"\u5e73\u53f0\u6570\u91cf: {report.platformCount}");
            EditorGUILayout.LabelField($"\u91d1\u5e01\u6570\u91cf: {report.totalCoins}");

            // 密度警告
            if (report.enemyDensity > 3f)
                EditorGUILayout.HelpBox("\u26a0\ufe0f \u654c\u4eba\u5bc6\u5ea6\u8f83\u9ad8\uff08>3/10\u683c\uff09\uff0c\u53ef\u80fd\u5bfc\u81f4\u96be\u5ea6\u8fc7\u5927\u3002", MessageType.Warning);
            if (report.trapDensity > 4f)
                EditorGUILayout.HelpBox("\u26a0\ufe0f \u673a\u5173\u5bc6\u5ea6\u8f83\u9ad8\uff08>4/10\u683c\uff09\uff0c\u53ef\u80fd\u5bfc\u81f4\u96be\u5ea6\u8fc7\u5927\u3002", MessageType.Warning);

            EditorGUI.indentLevel--;
        }

        // 错误和警告列表
        if (report.errors.Count > 0)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("\u274c \u9519\u8bef\u5217\u8868:", EditorStyles.boldLabel);
            foreach (string err in report.errors)
                EditorGUILayout.HelpBox(err, MessageType.Error);
        }

        if (report.warnings.Count > 0)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("\u26a0\ufe0f \u8b66\u544a\u5217\u8868:", EditorStyles.boldLabel);
            foreach (string warn in report.warnings)
                EditorGUILayout.HelpBox(warn, MessageType.Warning);
        }
    }

    private void DrawCheckItem(string label, bool passed, string detail)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(passed ? "\u2705" : "\u274c", GUILayout.Width(20f));
        EditorGUILayout.LabelField($"{label}: {detail}");
        EditorGUILayout.EndHorizontal();
    }

    // ═══════════════════════════════════════════════════
    // 模板保存区域
    // ═══════════════════════════════════════════════════

    private void DrawSaveSection()
    {
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        showSaveSection = EditorGUILayout.Foldout(showSaveSection, "\u4fdd\u5b58\u4e3a\u5b8c\u6574\u5173\u5361\u6a21\u677f", true, EditorStyles.foldoutHeader);
        if (!showSaveSection) return;

        EditorGUILayout.HelpBox(
            "\u5c06\u5f53\u524d\u573a\u666f\u5173\u5361\u4fdd\u5b58\u4e3a\u5185\u7f6e\u6a21\u677f\uff0c\u4e0b\u6b21\u53ef\u4ece Quick Whitebox Generator \u7684\u4e0b\u62c9\u5217\u8868\u4e2d\u76f4\u63a5\u9009\u62e9\u751f\u6210\u3002",
            MessageType.None);

        EditorGUILayout.Space(4f);

        saveMode = (SaveMode)EditorGUILayout.EnumPopup("\u4fdd\u5b58\u6a21\u5f0f", saveMode);

        if (saveMode == SaveMode.AddNew)
        {
            newTemplateName = EditorGUILayout.TextField("\u6a21\u677f\u540d\u79f0", newTemplateName);
        }
        else
        {
            existingTemplateNames = AsciiLevelGenerator.GetBuiltInTemplateNames();
            if (existingTemplateNames != null && existingTemplateNames.Length > 0)
            {
                replaceIndex = EditorGUILayout.Popup("\u66ff\u6362\u76ee\u6807", replaceIndex, existingTemplateNames);
            }
            else
            {
                EditorGUILayout.HelpBox("\u65e0\u53ef\u66ff\u6362\u7684\u6a21\u677f\u3002", MessageType.Warning);
            }
        }

        EditorGUILayout.Space(6f);

        EditorGUI.BeginDisabledGroup(report == null || !report.hasRun || string.IsNullOrEmpty(report.ascii));
        Color oldBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
        if (GUILayout.Button("\u2705 Save as Built-in Template", GUILayout.Height(32f)))
        {
            ExecuteTemplateSave();
        }
        GUI.backgroundColor = oldBg;
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "\u4fdd\u5b58\u540e\uff0c\u6a21\u677f\u5c06\u51fa\u73b0\u5728 Quick Whitebox Generator \u7684\u4e0b\u62c9\u83dc\u5355\u4e2d\u3002",
            MessageType.None);
    }

    // ═══════════════════════════════════════════════════
    // 核心验证逻辑
    // ═══════════════════════════════════════════════════

    private void RunValidation()
    {
        report = new ValidationReport();

        // Step 1: 从场景 Bake ASCII
        string ascii = BakeSceneToAsciiForValidation();
        if (ascii == null)
        {
            report.hasRun = false;
            return;
        }

        report.hasRun = true;
        report.ascii = ascii;
        report.sourceName = GetSceneLevelName();

        string[] lines = ascii.Split('\n');
        report.height = lines.Length;
        report.width = 0;
        foreach (string line in lines)
            if (line.Length > report.width) report.width = line.Length;

        // Step 2: 检查 M 和 G 存在性
        CheckStartAndGoal(lines);

        // Step 3: 物理可达性
        CheckReachability(ascii);

        // Step 4: 金币可达性
        CheckCoinReachability(ascii, lines);

        // Step 5: 密度统计
        CalculateDensity(lines);

        // Step 6: 孤立平台检测
        CheckIsolatedPlatforms(ascii, lines);

        // Step 7: Unknown Elements（场景级）
        CheckUnknownElements();

        // Step 8: 尺寸合理性
        CheckSizeReasonability();

        // Step 9: 汇总
        SummarizeResults();

        Repaint();
    }

    private void CheckStartAndGoal(string[] lines)
    {
        report.marioCount = 0;
        report.goalCount = 0;

        foreach (string line in lines)
        {
            foreach (char c in line)
            {
                if (c == 'M') report.marioCount++;
                else if (c == 'G') report.goalCount++;
            }
        }

        report.hasMario = report.marioCount > 0;
        report.hasGoal = report.goalCount > 0;

        if (!report.hasMario)
            report.errors.Add("\u7f3a\u5c11 Mario \u8d77\u70b9 (M)\u3002\u5b8c\u6574\u5173\u5361\u5fc5\u987b\u5305\u542b\u4e00\u4e2a\u8d77\u70b9\u3002");
        else if (report.marioCount > 1)
            report.warnings.Add($"\u627e\u5230 {report.marioCount} \u4e2a Mario \u8d77\u70b9\uff0c\u5efa\u8bae\u4ec5\u4fdd\u7559 1 \u4e2a\u3002");

        if (!report.hasGoal)
            report.errors.Add("\u7f3a\u5c11 Goal \u7ec8\u70b9 (G)\u3002\u5b8c\u6574\u5173\u5361\u5fc5\u987b\u5305\u542b\u4e00\u4e2a\u7ec8\u70b9\u3002");
        else if (report.goalCount > 1)
            report.warnings.Add($"\u627e\u5230 {report.goalCount} \u4e2a Goal \u7ec8\u70b9\uff0c\u5efa\u8bae\u4ec5\u4fdd\u7559 1 \u4e2a\u3002");
    }

    private void CheckReachability(string ascii)
    {
        if (!report.hasMario || !report.hasGoal)
        {
            report.isReachable = false;
            report.reachabilityDetail = "\u274c \u7f3a\u5c11 M \u6216 G\uff0c\u65e0\u6cd5\u68c0\u6d4b\u53ef\u8fbe\u6027";
            return;
        }

        var result = LevelReachabilityAnalyzer.Analyze(ascii, false);
        report.isReachable = result.IsReachable;
        report.reachabilityDetail = result.IsReachable
            ? $"\u2705 M({result.StartX},{result.StartY}) \u2192 G({result.GoalX},{result.GoalY}) \u53ef\u8fbe\uff0c\u63a2\u7d22 {result.ExploredCount} \u4e2a\u7ad9\u4f4d"
            : $"\u274c \u4e0d\u53ef\u8fbe\uff01\u6700\u8fdc\u5230\u8fbe ({result.ClosestReachedX},{result.ClosestReachedY})\uff0c\u8ddd\u7ec8\u70b9 {result.ClosestDistance:F1} \u683c";

        if (!result.IsReachable)
            report.errors.Add($"\u7269\u7406\u6b7b\u8def: {report.reachabilityDetail}");
    }

    private void CheckCoinReachability(string ascii, string[] lines)
    {
        // 找到所有金币位置
        List<Vector2Int> coinPositions = new List<Vector2Int>();
        for (int row = 0; row < lines.Length; row++)
        {
            for (int col = 0; col < lines[row].Length; col++)
            {
                if (lines[row][col] == 'o')
                {
                    // ASCII 坐标：row 0 = 最顶行，转为游戏坐标 y = height - 1 - row
                    coinPositions.Add(new Vector2Int(col, lines.Length - 1 - row));
                }
            }
        }

        report.totalCoins = coinPositions.Count;
        if (coinPositions.Count == 0 || !report.hasMario)
        {
            report.reachableCoins = report.totalCoins;
            return;
        }

        // 使用 BFS 可达区域判断金币是否在可达范围内
        // 复用 LevelReachabilityAnalyzer 的逻辑：如果主路径可达，则检查每个金币位置
        // 简化方案：对每个金币位置做临时 G 替换测试
        // 更高效方案：直接用 Analyze 返回的 ExploredCount 判断（但 API 不暴露 visited set）
        // 实际方案：逐个金币替换为 G 测试可达性
        report.reachableCoins = 0;
        report.unreachableCoinPositions.Clear();

        foreach (Vector2Int coinPos in coinPositions)
        {
            // 构造测试模板：将该金币位置替换为 G，移除原 G
            string testAscii = BuildCoinTestAscii(lines, coinPos);
            var result = LevelReachabilityAnalyzer.Analyze(testAscii, false);
            if (result.IsReachable)
            {
                report.reachableCoins++;
            }
            else
            {
                report.unreachableCoinPositions.Add(coinPos);
            }
        }

        if (report.unreachableCoinPositions.Count > 0)
        {
            report.warnings.Add($"{report.unreachableCoinPositions.Count} \u4e2a\u91d1\u5e01\u4e0d\u53ef\u8fbe\uff0c\u73a9\u5bb6\u65e0\u6cd5\u6536\u96c6\u3002");
        }
    }

    private string BuildCoinTestAscii(string[] lines, Vector2Int coinGamePos)
    {
        // coinGamePos.y 是游戏坐标，转回行号
        int row = lines.Length - 1 - coinGamePos.y;
        int col = coinGamePos.x;

        StringBuilder sb = new StringBuilder();
        for (int r = 0; r < lines.Length; r++)
        {
            char[] chars = lines[r].ToCharArray();
            // 移除所有 G
            for (int c = 0; c < chars.Length; c++)
            {
                if (chars[c] == 'G') chars[c] = '.';
            }
            // 在金币位置放 G
            if (r == row && col < chars.Length)
            {
                chars[col] = 'G';
            }
            sb.Append(new string(chars));
            if (r < lines.Length - 1) sb.Append('\n');
        }
        return sb.ToString();
    }

    private void CalculateDensity(string[] lines)
    {
        HashSet<char> enemyChars = new HashSet<char> { 'e', 'E', 'f' };
        HashSet<char> trapChars = new HashSet<char> { '^', '~', 'P', '@', '<' };
        HashSet<char> platformChars = new HashSet<char> { '=', '-', 'B', 'C', '>' };

        int totalCells = 0;
        report.enemyCount = 0;
        report.trapCount = 0;
        report.platformCount = 0;

        foreach (string line in lines)
        {
            totalCells += line.Length;
            foreach (char c in line)
            {
                if (enemyChars.Contains(c)) report.enemyCount++;
                else if (trapChars.Contains(c)) report.trapCount++;
                else if (platformChars.Contains(c)) report.platformCount++;
            }
        }

        float totalLength = report.width > 0 ? report.width : 1f;
        report.enemyDensity = (report.enemyCount / totalLength) * 10f;
        report.trapDensity = (report.trapCount / totalLength) * 10f;
    }

    private void CheckIsolatedPlatforms(string ascii, string[] lines)
    {
        report.isolatedPlatforms.Clear();

        // 简化检测：找到所有平台段（= 或 -），检查其下方或相邻是否有地面/连接
        // 如果一个平台段的左右两端下方都是空气且无法从主路径跳到，则标记为孤立
        // 这里用简化启发式：平台段如果周围 3 格内无实体地面且不在主路径上
        HashSet<char> solidChars = new HashSet<char> { '#', '=', '-', 'B', 'C', '>' };

        for (int row = 0; row < lines.Length; row++)
        {
            int col = 0;
            while (col < lines[row].Length)
            {
                char c = lines[row][col];
                if (c == '=' || c == '-')
                {
                    int startCol = col;
                    while (col < lines[row].Length && (lines[row][col] == '=' || lines[row][col] == '-'))
                        col++;
                    int endCol = col - 1;
                    int platformWidth = endCol - startCol + 1;

                    // 检查平台下方是否有支撑（3行内）
                    bool hasSupport = false;
                    for (int checkRow = row + 1; checkRow < Mathf.Min(row + 4, lines.Length); checkRow++)
                    {
                        for (int checkCol = startCol; checkCol <= endCol && checkCol < lines[checkRow].Length; checkCol++)
                        {
                            if (solidChars.Contains(lines[checkRow][checkCol]))
                            {
                                hasSupport = true;
                                break;
                            }
                        }
                        if (hasSupport) break;
                    }

                    // 检查平台上方是否有东西（说明有用途）
                    bool hasContentAbove = false;
                    if (row > 0)
                    {
                        for (int checkCol = startCol; checkCol <= endCol && checkCol < lines[row - 1].Length; checkCol++)
                        {
                            char above = lines[row - 1][checkCol];
                            if (above != '.' && above != ' ')
                            {
                                hasContentAbove = true;
                                break;
                            }
                        }
                    }

                    // 如果无支撑且无上方内容且宽度小（可能是装饰性孤立平台）
                    if (!hasSupport && !hasContentAbove && platformWidth <= 2)
                    {
                        int gameY = lines.Length - 1 - row;
                        report.isolatedPlatforms.Add($"\u5e73\u53f0\u6bb5 ({startCol},{gameY}) \u5bbd{platformWidth}\u683c\uff0c\u65e0\u652f\u6491\u4e14\u65e0\u5185\u5bb9");
                    }
                }
                else
                {
                    col++;
                }
            }
        }

        if (report.isolatedPlatforms.Count > 0)
        {
            report.warnings.Add($"\u53d1\u73b0 {report.isolatedPlatforms.Count} \u4e2a\u7591\u4f3c\u5b64\u7acb\u5e73\u53f0\uff0c\u53ef\u80fd\u662f\u6b7b\u8def\u6216\u65e0\u7528\u5e73\u53f0\u3002");
        }
    }

    private void CheckUnknownElements()
    {
        report.unknownElements.Clear();

        GameObject root = GameObject.Find(GENERATED_ROOT_NAME);
        if (root == null) return;

        AsciiElementRegistry registry = AsciiElementRegistry.GetDefault();
        if (registry == null) return;

        registry.BuildCache();

        // 收集所有已知元素名
        HashSet<string> knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (registry.entries != null)
        {
            foreach (AsciiElementEntry entry in registry.entries)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.elementName))
                    knownNames.Add(entry.elementName);
            }
        }

        // 检查场景中的对象
        foreach (Transform child in root.transform)
        {
            if (child == null) continue;
            string objName = child.name;

            // 提取基础名（去掉 _x_y 和 _wN 后缀）
            string baseName = ExtractBaseName(objName);

            if (!string.IsNullOrEmpty(baseName) && !knownNames.Contains(baseName))
            {
                // 检查是否有已知组件
                bool hasKnownComponent = false;
                Component[] components = child.GetComponents<Component>();
                foreach (Component comp in components)
                {
                    if (comp == null) continue;
                    Type t = comp.GetType();
                    if (t != typeof(Transform) && t != typeof(SpriteRenderer) &&
                        t != typeof(BoxCollider2D) && t != typeof(Rigidbody2D) &&
                        t != typeof(Animator) && t != typeof(AudioSource))
                    {
                        hasKnownComponent = true;
                        break;
                    }
                }

                if (!hasKnownComponent)
                {
                    report.unknownElements.Add($"{objName} (pos: {child.position.x:F0},{child.position.y:F0})");
                }
            }
        }

        if (report.unknownElements.Count > 0)
        {
            report.warnings.Add($"\u573a\u666f\u4e2d\u6709 {report.unknownElements.Count} \u4e2a\u672a\u8bc6\u522b\u5143\u7d20\uff0c\u53ef\u80fd\u662f\u624b\u52a8\u6dfb\u52a0\u7684\u5bf9\u8c61\u672a\u6ce8\u518c\u5230 Registry\u3002");
        }
    }

    private string ExtractBaseName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return "";

        // 去掉 " (1)" 类型的 Unity 副本后缀
        int parenIdx = objectName.IndexOf(" (");
        if (parenIdx > 0) objectName = objectName.Substring(0, parenIdx);

        // 去掉 _x_y 或 _x_y_wN 后缀
        Match m = Regex.Match(objectName, @"^(.+?)(_\d+_\d+(_w\d+)?)$");
        if (m.Success) return m.Groups[1].Value;

        return objectName;
    }

    private void CheckSizeReasonability()
    {
        report.sizeReasonable = true;
        report.sizeWarning = "";

        if (report.width < 10)
        {
            report.sizeReasonable = false;
            report.sizeWarning = $"\u274c \u5bbd\u5ea6\u53ea\u6709 {report.width} \u683c\uff0c\u5b8c\u6574\u5173\u5361\u5efa\u8bae\u81f3\u5c11 20 \u683c\u5bbd\u3002";
            report.errors.Add(report.sizeWarning);
        }
        else if (report.width > 200)
        {
            report.sizeReasonable = false;
            report.sizeWarning = $"\u26a0\ufe0f \u5bbd\u5ea6\u8fbe\u5230 {report.width} \u683c\uff0c\u53ef\u80fd\u8fc7\u957f\u5bfc\u81f4\u4f53\u9a8c\u62d6\u6c93\u3002";
            report.warnings.Add(report.sizeWarning);
        }

        if (report.height < 5)
        {
            report.sizeReasonable = false;
            report.sizeWarning += $"\n\u274c \u9ad8\u5ea6\u53ea\u6709 {report.height} \u683c\uff0c\u5b8c\u6574\u5173\u5361\u5efa\u8bae\u81f3\u5c11 8 \u683c\u9ad8\u3002";
            report.errors.Add($"\u9ad8\u5ea6\u53ea\u6709 {report.height} \u683c\uff0c\u5b8c\u6574\u5173\u5361\u5efa\u8bae\u81f3\u5c11 8 \u683c\u9ad8\u3002");
        }
        else if (report.height > 50)
        {
            report.sizeWarning += $"\n\u26a0\ufe0f \u9ad8\u5ea6\u8fbe\u5230 {report.height} \u683c\uff0c\u5782\u76f4\u5173\u5361\u8bf7\u786e\u4fdd\u6709\u5408\u7406\u7684\u4e0a\u5347\u8def\u5f84\u3002";
            report.warnings.Add($"\u9ad8\u5ea6\u8fbe\u5230 {report.height} \u683c\uff0c\u5782\u76f4\u5173\u5361\u8bf7\u786e\u4fdd\u6709\u5408\u7406\u7684\u4e0a\u5347\u8def\u5f84\u3002");
        }
    }

    private void SummarizeResults()
    {
        report.passed = report.errors.Count == 0;
    }

    // ═══════════════════════════════════════════════════
    // 场景 Bake（复用 AsciiLevelBaker 逻辑）
    // ═══════════════════════════════════════════════════

    private string BakeSceneToAsciiForValidation()
    {
        // 先尝试直接调用 AsciiLevelBaker 的方法（通过剪贴板中转）
        GameObject root = GameObject.Find(GENERATED_ROOT_NAME);
        if (root == null)
        {
            EditorUtility.DisplayDialog("Validation Failed",
                $"\u573a\u666f\u4e2d\u672a\u627e\u5230 '{GENERATED_ROOT_NAME}'\u3002\u8bf7\u5148\u751f\u6210\u5173\u5361\u3002", "OK");
            return null;
        }

        if (root.transform.childCount == 0)
        {
            EditorUtility.DisplayDialog("Validation Failed",
                $"'{GENERATED_ROOT_NAME}' \u4e0b\u6ca1\u6709\u5b50\u5bf9\u8c61\u3002", "OK");
            return null;
        }

        // 保存当前剪贴板内容
        string oldClipboard = EditorGUIUtility.systemCopyBuffer;

        // 调用 AsciiLevelBaker 的 Bake 方法（它会写入剪贴板）
        AsciiLevelBaker.BakeSceneToAscii();

        // 读取结果
        string result = EditorGUIUtility.systemCopyBuffer;

        // 恢复剪贴板
        EditorGUIUtility.systemCopyBuffer = oldClipboard;

        if (string.IsNullOrEmpty(result) || result == oldClipboard)
        {
            EditorUtility.DisplayDialog("Validation Failed",
                "Bake \u5931\u8d25\uff0c\u672a\u80fd\u4ece\u573a\u666f\u5bfc\u51fa ASCII\u3002\u8bf7\u68c0\u67e5 Console \u65e5\u5fd7\u3002", "OK");
            return null;
        }

        // 去掉 Override 注释行（只保留纯 ASCII 网格）
        string[] allLines = result.Split('\n');
        StringBuilder sb = new StringBuilder();
        foreach (string line in allLines)
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("# Override_")) continue;
            if (string.IsNullOrEmpty(trimmed) && sb.Length > 0) continue; // 跳过空行
            sb.Append(line.TrimEnd());
            sb.Append('\n');
        }

        return sb.ToString().TrimEnd('\n');
    }

    private string GetSceneLevelName()
    {
        GameObject root = GameObject.Find(GENERATED_ROOT_NAME);
        if (root != null && root.transform.childCount > 0)
        {
            // 尝试从第一个子对象名推断
            string firstName = root.transform.GetChild(0).name;
            Match m = Regex.Match(firstName, @"^(.+?)_\d+_\d+");
            if (m.Success) return $"Scene Level ({m.Groups[1].Value}...)";
        }
        return "Scene Level";
    }

    // ═══════════════════════════════════════════════════
    // 模板保存逻辑
    // ═══════════════════════════════════════════════════

    private void ExecuteTemplateSave()
    {
        if (report == null || string.IsNullOrEmpty(report.ascii))
        {
            EditorUtility.DisplayDialog("Save Failed", "\u8bf7\u5148\u8fd0\u884c\u9a8c\u8bc1\u4ee5\u83b7\u53d6 ASCII \u6570\u636e\u3002", "OK");
            return;
        }

        string sourcePath = GetGeneratorSourcePath();
        if (sourcePath == null) return;

        string content = System.IO.File.ReadAllText(sourcePath, Encoding.UTF8);

        if (saveMode == SaveMode.AddNew)
        {
            if (string.IsNullOrEmpty(newTemplateName) || newTemplateName.Trim().Length == 0)
            {
                EditorUtility.DisplayDialog("Save Failed", "\u8bf7\u586b\u5199\u6a21\u677f\u540d\u79f0\u3002", "OK");
                return;
            }

            string templateName = newTemplateName.Trim();

            // 检查重名
            string[] existingNames = AsciiLevelGenerator.GetBuiltInTemplateNames();
            foreach (string name in existingNames)
            {
                if (name == templateName)
                {
                    bool overwrite = EditorUtility.DisplayDialog("Name Conflict",
                        $"\u5df2\u5b58\u5728\u540c\u540d\u6a21\u677f '{templateName}'\u3002\u662f\u5426\u66ff\u6362\uff1f", "Replace", "Cancel");
                    if (!overwrite) return;
                    ReplaceTemplateInSource(content, sourcePath, templateName, report.ascii);
                    return;
                }
            }

            AddNewTemplateToSource(content, sourcePath, templateName, report.ascii);
        }
        else
        {
            if (existingTemplateNames == null || existingTemplateNames.Length == 0)
            {
                EditorUtility.DisplayDialog("Save Failed", "\u65e0\u53ef\u66ff\u6362\u7684\u6a21\u677f\u3002", "OK");
                return;
            }

            string targetName = existingTemplateNames[replaceIndex];
            bool confirmed = EditorUtility.DisplayDialog("Confirm Replace",
                $"\u5c06\u66ff\u6362\u5185\u7f6e\u6a21\u677f '{targetName}' \u7684\u5185\u5bb9\u3002\n\n\u786e\u8ba4\uff1f", "Replace", "Cancel");
            if (!confirmed) return;

            ReplaceTemplateInSource(content, sourcePath, targetName, report.ascii);
        }
    }

    private void AddNewTemplateToSource(string content, string sourcePath, string templateName, string ascii)
    {
        // 生成常量名
        string constName = "TEMPLATE_" + Regex.Replace(templateName.ToUpper(), @"[^A-Z0-9]", "_");

        // 构建常量定义
        string constDef = BuildTemplateConstant(constName, templateName, ascii);

        // 在最后一个 TEMPLATE_ 常量之后插入
        int insertPos = FindTemplateConstantInsertPosition(content);
        if (insertPos < 0)
        {
            EditorUtility.DisplayDialog("Save Failed", "\u672a\u80fd\u5728 AsciiLevelGenerator.cs \u4e2d\u5b9a\u4f4d\u63d2\u5165\u70b9\u3002", "OK");
            return;
        }

        string newContent = content.Substring(0, insertPos) + "\n\n" + constDef + content.Substring(insertPos);

        // 更新 GetBuiltInTemplateNames 和 GetBuiltInTemplate
        newContent = UpdateTemplateRegistry(newContent, templateName, constName, false);

        System.IO.File.WriteAllText(sourcePath, newContent, Encoding.UTF8);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Save Successful",
            $"\u2705 \u6a21\u677f '{templateName}' \u5df2\u6dfb\u52a0\u5230 AsciiLevelGenerator\u3002\n\n" +
            "\u91cd\u65b0\u7f16\u8bd1\u540e\u5373\u53ef\u5728 Quick Whitebox Generator \u4e2d\u9009\u62e9\u3002", "OK");
        Debug.Log($"[FullLevelValidator] \u2705 Template '{templateName}' added to AsciiLevelGenerator.cs");
    }

    private void ReplaceTemplateInSource(string content, string sourcePath, string targetName, string ascii)
    {
        // 找到对应的常量名
        string constName = FindConstNameForTemplate(content, targetName);
        if (string.IsNullOrEmpty(constName))
        {
            EditorUtility.DisplayDialog("Save Failed",
                $"\u672a\u80fd\u5728\u6e90\u6587\u4ef6\u4e2d\u5b9a\u4f4d\u6a21\u677f '{targetName}' \u7684\u5e38\u91cf\u3002", "OK");
            return;
        }

        // 替换常量内容
        string pattern = @"private\s+const\s+string\s+" + Regex.Escape(constName) + @"\s*=\s*\n?\s*""[^;]+;";
        string replacement = BuildTemplateConstantInline(constName, targetName, ascii);

        string newContent = Regex.Replace(content, pattern, replacement, RegexOptions.Singleline);

        if (newContent == content)
        {
            // 尝试另一种模式（多行字符串拼接）
            pattern = @"private\s+const\s+string\s+" + Regex.Escape(constName) + @"\s*=[\s\S]*?;";
            newContent = Regex.Replace(content, pattern, replacement, RegexOptions.Singleline);
        }

        if (newContent == content)
        {
            EditorUtility.DisplayDialog("Save Failed",
                $"\u66ff\u6362\u5931\u8d25\uff0c\u672a\u80fd\u5339\u914d\u5e38\u91cf '{constName}' \u7684\u5b9a\u4e49\u3002", "OK");
            return;
        }

        System.IO.File.WriteAllText(sourcePath, newContent, Encoding.UTF8);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Replace Successful",
            $"\u2705 \u6a21\u677f '{targetName}' \u7684\u5185\u5bb9\u5df2\u66f4\u65b0\u3002", "OK");
        Debug.Log($"[FullLevelValidator] \u2705 Template '{targetName}' replaced in AsciiLevelGenerator.cs");
    }

    // ═══════════════════════════════════════════════════
    // 源文件操作辅助
    // ═══════════════════════════════════════════════════

    private string GetGeneratorSourcePath()
    {
        string relativePath = "Assets/Scripts/LevelDesign/AsciiLevelGenerator.cs";
        string fullPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(Application.dataPath), relativePath);

        if (!System.IO.File.Exists(fullPath))
        {
            string[] guids = AssetDatabase.FindAssets("AsciiLevelGenerator t:TextAsset");
            if (guids != null && guids.Length > 0)
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith("AsciiLevelGenerator.cs"))
                    {
                        fullPath = System.IO.Path.Combine(
                            System.IO.Path.GetDirectoryName(Application.dataPath), path);
                        break;
                    }
                }
            }
        }

        if (!System.IO.File.Exists(fullPath))
        {
            EditorUtility.DisplayDialog("Save Failed",
                $"\u672a\u627e\u5230 AsciiLevelGenerator.cs\u3002\n\u8def\u5f84: {fullPath}", "OK");
            return null;
        }

        return fullPath;
    }

    private string BuildTemplateConstant(string constName, string templateName, string ascii)
    {
        string[] lines = ascii.Split('\n');
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"    /// <summary>{templateName} \u2014 \u7531 Full Level Validator \u4fdd\u5b58</summary>");
        sb.Append($"    private const string {constName} =\n");
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            string escaped = line.Replace("\\", "\\\\").Replace("\"", "\\\"");
            if (i < lines.Length - 1)
                sb.AppendLine($"        \"{escaped}\\n\" +");
            else
                sb.AppendLine($"        \"{escaped}\";");
        }
        return sb.ToString();
    }

    private string BuildTemplateConstantInline(string constName, string templateName, string ascii)
    {
        string[] lines = ascii.Split('\n');
        StringBuilder sb = new StringBuilder();
        sb.Append($"private const string {constName} =\n");
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            string escaped = line.Replace("\\", "\\\\").Replace("\"", "\\\"");
            if (i < lines.Length - 1)
                sb.AppendLine($"        \"{escaped}\\n\" +");
            else
                sb.Append($"        \"{escaped}\";");
        }
        return sb.ToString();
    }

    private int FindTemplateConstantInsertPosition(string content)
    {
        // 找到最后一个 TEMPLATE_ 常量定义的结尾（分号后）
        MatchCollection matches = Regex.Matches(content, @"private\s+const\s+string\s+TEMPLATE_\w+\s*=[\s\S]*?;");
        if (matches.Count == 0) return -1;

        Match lastMatch = matches[matches.Count - 1];
        return lastMatch.Index + lastMatch.Length;
    }

    private string FindConstNameForTemplate(string content, string templateName)
    {
        // 在 GetBuiltInTemplate 的 switch 中找到模板名对应的常量名
        // 例如 case 0: return TEMPLATE_CLASSIC_PLAINS;
        string[] names = AsciiLevelGenerator.GetBuiltInTemplateNames();
        int targetIndex = -1;
        for (int i = 0; i < names.Length; i++)
        {
            if (names[i] == templateName) { targetIndex = i; break; }
        }

        if (targetIndex < 0) return null;

        // 在 switch 中找 case N: return TEMPLATE_XXX;
        string pattern = $@"case\s+{targetIndex}\s*:\s*return\s+(\w+)\s*;";
        Match m = Regex.Match(content, pattern);
        return m.Success ? m.Groups[1].Value : null;
    }

    private string UpdateTemplateRegistry(string content, string newName, string newConstName, bool isReplace)
    {
        if (isReplace) return content; // 替换模式不需要更新注册表

        // 更新 GetBuiltInTemplateNames：在数组末尾添加新名称
        string namesPattern = @"(return\s+new\s+string\[\]\s*\{[^}]+)(\}\s*;)";
        Match namesMatch = Regex.Match(content, namesPattern);
        if (namesMatch.Success)
        {
            string before = namesMatch.Groups[1].Value;
            string after = namesMatch.Groups[2].Value;
            content = content.Substring(0, namesMatch.Index) +
                      before + $", \"{newName}\"" + " " + after +
                      content.Substring(namesMatch.Index + namesMatch.Length);
        }

        // 更新 GetBuiltInTemplate：在 switch 中添加新 case
        // 找到 default: return 行，在它之前插入新 case
        string switchPattern = @"(default:\s*return\s+\w+;)";
        Match switchMatch = Regex.Match(content, switchPattern);
        if (switchMatch.Success)
        {
            string[] existingNames = AsciiLevelGenerator.GetBuiltInTemplateNames();
            int newIndex = existingNames.Length; // 新模板的索引
            string newCase = $"            case {newIndex}: return {newConstName};\n            ";
            content = content.Substring(0, switchMatch.Index) +
                      newCase + switchMatch.Value +
                      content.Substring(switchMatch.Index + switchMatch.Length);
        }

        return content;
    }
}
#endif
