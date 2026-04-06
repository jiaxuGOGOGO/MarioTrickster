using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// Doc-as-Code 动态文档同步引擎 (Session 45)
///
/// 功能 1 — Sync Docs：一键读取 PhysicsMetrics.cs 的核心常量，
///   用正则替换刷新三个 MD 文档中的物理数值，确保与代码强一致。
///
/// 功能 2 — Copy Prompt：一键生成给大模型的关卡系统提示词，
///   包含最新物理常量和设计约束，复制到剪贴板。
///
/// 数据源：PhysicsMetrics（静态引用，唯一真相源）。
/// </summary>
public class DocsAutomatorWindow : EditorWindow
{
    // ═══════════════════════════════════════════════════
    // 文档相对路径（相对于 Application.dataPath 的父目录，即项目根目录）
    // ═══════════════════════════════════════════════════
    private const string TESTING_GUIDE_PATH = "MarioTrickster_Testing_Guide.md";
    private const string PHYSICS_GUIDE_PATH = "LevelDesign_References/PHYSICS_METRICS_GUIDE.md";
    private const string DESIGN_GUIDE_PATH  = "LevelStudio_DesignGuide.md";

    private Vector2 _scrollPos;
    private string _lastSyncResult = "";
    private string _lastPromptResult = "";

    // ── 缓存 GUIStyle（遵守 P1 规则：OnGUI 中禁止 new GUIStyle） ──
    private GUIStyle _headerStyle;   // cached
    private GUIStyle _resultStyle;   // cached
    private bool _stylesInitialized; // cached

    [MenuItem("MarioTrickster/Docs Automator", false, 200)]
    public static void ShowWindow()
    {
        var win = GetWindow<DocsAutomatorWindow>("Docs Automator");
        win.minSize = new Vector2(420, 360);
    }

    // ═══════════════════════════════════════════════════
    // GUI
    // ═══════════════════════════════════════════════════

    private void InitStyles() // cached
    {
        if (_stylesInitialized) return;
        _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 }; // cached
        _resultStyle = new GUIStyle(EditorStyles.helpBox) { richText = true, wordWrap = true }; // cached
        _stylesInitialized = true;
    }

    private void OnGUI()
    {
        InitStyles(); // cached

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Doc-as-Code 动态文档同步引擎", _headerStyle);
        EditorGUILayout.LabelField("数据源: PhysicsMetrics.cs（唯一真相源）", EditorStyles.miniLabel);
        EditorGUILayout.Space(4);

        // ── 当前常量预览 ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("当前 PhysicsMetrics 核心常量:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"  MAX_JUMP_HEIGHT      = {PhysicsMetrics.MAX_JUMP_HEIGHT}");
        EditorGUILayout.LabelField($"  MAX_JUMP_DISTANCE    = {PhysicsMetrics.MAX_JUMP_DISTANCE}");
        EditorGUILayout.LabelField($"  MAX_GAP_WITH_COYOTE  = {PhysicsMetrics.MAX_GAP_WITH_COYOTE}");
        EditorGUILayout.LabelField($"  MIN_JUMP_HEIGHT      = {PhysicsMetrics.MIN_JUMP_HEIGHT}");
        EditorGUILayout.LabelField($"  COYOTE_BONUS_DISTANCE= {PhysicsMetrics.COYOTE_BONUS_DISTANCE}");
        EditorGUILayout.LabelField($"  ASCII_MAX_GAP        = {PhysicsMetrics.ASCII_MAX_GAP}");
        EditorGUILayout.LabelField($"  ASCII_MAX_HEIGHT     = {PhysicsMetrics.ASCII_MAX_HEIGHT}");
        EditorGUILayout.LabelField($"  ASCII_EXTREME_HEIGHT = {PhysicsMetrics.ASCII_EXTREME_HEIGHT}");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // ── 功能 1: Sync Docs ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("功能 1: Sync Docs — 一键同步文档数值", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("刷新以下三个文档中的物理数值:", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"  • {TESTING_GUIDE_PATH}");
        EditorGUILayout.LabelField($"  • {PHYSICS_GUIDE_PATH}");
        EditorGUILayout.LabelField($"  • {DESIGN_GUIDE_PATH}");
        EditorGUILayout.Space(4);

        if (GUILayout.Button("Sync Docs — 同步文档物理数值", GUILayout.Height(30)))
        {
            SyncAllDocs();
        }

        if (!string.IsNullOrEmpty(_lastSyncResult))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(_lastSyncResult, _resultStyle);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // ── 功能 2: Copy Prompt ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("功能 2: Copy Prompt — 一键生成关卡系统提示词", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("生成包含最新物理常量的大模型提示词并复制到剪贴板。", EditorStyles.miniLabel);
        EditorGUILayout.Space(4);

        if (GUILayout.Button("Copy Prompt — 生成并复制提示词", GUILayout.Height(30)))
        {
            CopyPromptToClipboard();
        }

        if (!string.IsNullOrEmpty(_lastPromptResult))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(_lastPromptResult, _resultStyle);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);
        EditorGUILayout.EndScrollView();
    }

    // ═══════════════════════════════════════════════════
    // 功能 1: Sync Docs
    // ═══════════════════════════════════════════════════

    private void SyncAllDocs()
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        int totalReplacements = 0;
        string report = "";

        // ── 1. MarioTrickster_Testing_Guide.md ──
        string testingPath = Path.Combine(projectRoot, TESTING_GUIDE_PATH);
        if (File.Exists(testingPath))
        {
            string content = File.ReadAllText(testingPath);
            string original = content;

            // 替换测试10中的"超过 X 格高平台" → 用 MAX_JUMP_HEIGHT
            // 匹配模式: "超过 数字 格高平台" 或 "超过数字格高平台"
            content = Regex.Replace(content,
                @"超过\s*[\d.]+\s*格高平台",
                $"超过 {PhysicsMetrics.MAX_JUMP_HEIGHT} 格高平台");

            // 替换 "超过 X 格宽间" → 用 ASCII_MAX_GAP
            content = Regex.Replace(content,
                @"超过\s*[\d.]+\s*格宽间",
                $"超过 {PhysicsMetrics.ASCII_MAX_GAP} 格宽间");

            int changes = content != original ? 1 : 0;
            if (changes > 0)
            {
                File.WriteAllText(testingPath, content);
                totalReplacements += changes;
                report += $"✅ {TESTING_GUIDE_PATH}: 已同步\n";
            }
            else
            {
                report += $"✔ {TESTING_GUIDE_PATH}: 已是最新，无需修改\n";
            }
        }
        else
        {
            report += $"⚠ {TESTING_GUIDE_PATH}: 文件不存在\n";
        }

        // ── 2. PHYSICS_METRICS_GUIDE.md ──
        string physicsPath = Path.Combine(projectRoot, PHYSICS_GUIDE_PATH);
        if (File.Exists(physicsPath))
        {
            string content = File.ReadAllText(physicsPath);
            string original = content;

            // 替换 "H_max = ... = X 格" 中的数值
            content = Regex.Replace(content,
                @"(\*\*原地最高跳跃高度 \(H_max\)\*\*.*?= \*\*)[\d.]+( 格\*\*)",
                $"${{1}}{PhysicsMetrics.MAX_JUMP_HEIGHT}${{2}}");

            // 替换 "D_max = ... = X 格" 中的数值
            content = Regex.Replace(content,
                @"(\*\*满速平跳最大水平距离 \(D_max\)\*\*.*?= \*\*)[\d.]+( 格\*\*)",
                $"${{1}}{PhysicsMetrics.MAX_JUMP_DISTANCE}${{2}}");

            // 替换 "D_coyote = ... = X 格" 中的数值
            content = Regex.Replace(content,
                @"(\*\*Coyote Time 额外水平距离 \(D_coyote\)\*\*.*?= \*\*)[\d.]+( 格\*\*)",
                $"${{1}}{PhysicsMetrics.COYOTE_BONUS_DISTANCE}${{2}}");

            // 替换 "最大可跨越间隙 = X 格" 中的数值
            content = Regex.Replace(content,
                @"(\*\*含 Coyote Time 的最大可跨越间隙\*\*.*?= \*\*)[\d.]+( 格\*\*)",
                $"${{1}}{PhysicsMetrics.MAX_GAP_WITH_COYOTE}${{2}}");

            // 替换 "安全间隙上限：X 格"
            content = Regex.Replace(content,
                @"(\*\*安全间隙上限\*\*：)[\d.]+( 格)",
                $"${{1}}{PhysicsMetrics.ASCII_MAX_GAP}${{2}}");

            // 替换 "安全高台上限：X 格"
            content = Regex.Replace(content,
                @"(\*\*安全高台上限\*\*：)[\d.]+( 格)",
                $"${{1}}{PhysicsMetrics.ASCII_MAX_HEIGHT}${{2}}");

            // 替换 "极限高台：X 格"（关键修复点）
            content = Regex.Replace(content,
                @"(\*\*极限高台\*\*：)[\d.]+( 格)",
                $"${{1}}{PhysicsMetrics.MAX_JUMP_HEIGHT}${{2}}");

            int changes = content != original ? 1 : 0;
            if (changes > 0)
            {
                File.WriteAllText(physicsPath, content);
                totalReplacements += changes;
                report += $"✅ {PHYSICS_GUIDE_PATH}: 已同步\n";
            }
            else
            {
                report += $"✔ {PHYSICS_GUIDE_PATH}: 已是最新，无需修改\n";
            }
        }
        else
        {
            report += $"⚠ {PHYSICS_GUIDE_PATH}: 文件不存在\n";
        }

        // ── 3. LevelStudio_DesignGuide.md ──
        string designPath = Path.Combine(projectRoot, DESIGN_GUIDE_PATH);
        if (File.Exists(designPath))
        {
            string content = File.ReadAllText(designPath);
            string original = content;

            // 替换物理约束表格中的数值
            // "水平安全跨越 | X 格"
            content = Regex.Replace(content,
                @"(水平安全跨越\s*\|\s*)[\d.]+( 格)",
                $"${{1}}{PhysicsMetrics.ASCII_MAX_GAP}${{2}}");

            // "水平极限跨越 | X 格（含 coyote time...物理极限 X 格）"
            content = Regex.Replace(content,
                @"(水平极限跨越\s*\|\s*)[\d.]+( 格（含 coyote time，需精确操作；物理极限 )[\d.]+ 格",
                $"${{1}}{Mathf.Floor(PhysicsMetrics.MAX_GAP_WITH_COYOTE)}${{2}}{PhysicsMetrics.MAX_GAP_WITH_COYOTE} 格");

            // "垂直安全跳高 | X 格"
            content = Regex.Replace(content,
                @"(垂直安全跳高\s*\|\s*)[\d.]+( 格)",
                $"${{1}}{PhysicsMetrics.ASCII_MAX_HEIGHT}${{2}}");

            // "垂直极限跳高 | X 格"
            content = Regex.Replace(content,
                @"(垂直极限跳高\s*\|\s*)[\d.]+( 格)",
                $"${{1}}{PhysicsMetrics.MAX_JUMP_HEIGHT}${{2}}");

            // 确保空白填充字符描述正确（绝对禁止空格，必须用点号）
            content = Regex.Replace(content,
                @"(空白填充字符\s*\|\s*).*",
                "${1}`.`（英文点号，绝对禁止空格）");

            int changes = content != original ? 1 : 0;
            if (changes > 0)
            {
                File.WriteAllText(designPath, content);
                totalReplacements += changes;
                report += $"✅ {DESIGN_GUIDE_PATH}: 已同步\n";
            }
            else
            {
                report += $"✔ {DESIGN_GUIDE_PATH}: 已是最新，无需修改\n";
            }
        }
        else
        {
            report += $"⚠ {DESIGN_GUIDE_PATH}: 文件不存在\n";
        }

        _lastSyncResult = $"同步完成！{totalReplacements} 个文档被更新。\n\n{report}";

        if (totalReplacements > 0)
            Debug.Log($"[DocsAutomator] Sync Docs 完成: {totalReplacements} 个文档已更新。\n{report}");
        else
            Debug.Log($"[DocsAutomator] Sync Docs 完成: 所有文档已是最新，无需修改。");
    }

    // ═══════════════════════════════════════════════════
    // 功能 2: Copy Prompt
    // ═══════════════════════════════════════════════════

    private void CopyPromptToClipboard()
    {
        string prompt = GenerateLevelDesignPrompt();
        GUIUtility.systemCopyBuffer = prompt;
        _lastPromptResult = "✅ 提示词已复制到剪贴板！可直接粘贴给大模型。";
        Debug.Log("[DocsAutomator] Copy Prompt 成功！关卡系统提示词已复制到剪贴板。\n" +
                  $"提示词长度: {prompt.Length} 字符");
    }

    private string GenerateLevelDesignPrompt()
    {
        return $@"# MarioTrickster 关卡设计系统提示词（自动生成 by DocsAutomator）

## 物理常量（来自 PhysicsMetrics.cs，唯一真相源）

| 常量 | 值 | 说明 |
|------|-----|------|
| MAX_JUMP_HEIGHT | {PhysicsMetrics.MAX_JUMP_HEIGHT} 格 | 原地最高跳跃高度 |
| MAX_JUMP_DISTANCE | {PhysicsMetrics.MAX_JUMP_DISTANCE} 格 | 满速平跳最大水平距离 |
| MAX_GAP_WITH_COYOTE | {PhysicsMetrics.MAX_GAP_WITH_COYOTE} 格 | 含 Coyote Time 的最大可跨越间隙 |
| MIN_JUMP_HEIGHT | {PhysicsMetrics.MIN_JUMP_HEIGHT} 格 | 短跳最低高度 |
| COYOTE_BONUS_DISTANCE | {PhysicsMetrics.COYOTE_BONUS_DISTANCE} 格 | Coyote Time 额外水平距离 |
| ASCII_MAX_GAP | {PhysicsMetrics.ASCII_MAX_GAP} 格 | ASCII 模板安全间隙上限 |
| ASCII_MAX_HEIGHT | {PhysicsMetrics.ASCII_MAX_HEIGHT} 格 | ASCII 模板安全高台上限 |
| ASCII_EXTREME_HEIGHT | {PhysicsMetrics.ASCII_EXTREME_HEIGHT} 格 | ASCII 模板极限高台（需精确操作） |

## 关卡设计安全约束

| 约束 | 数值 |
|------|------|
| 水平安全跨越 | {PhysicsMetrics.ASCII_MAX_GAP} 格 |
| 水平极限跨越 | {PhysicsMetrics.MAX_GAP_WITH_COYOTE} 格（含 Coyote Time） |
| 垂直安全跳高 | {PhysicsMetrics.ASCII_MAX_HEIGHT} 格 |
| 垂直极限跳高 | {PhysicsMetrics.MAX_JUMP_HEIGHT} 格 |

## 绝对铁律（违反任何一条将导致关卡不可玩）

1. **绝对禁止空格**：空气区域必须用英文点号 `.` 填充，绝对禁止使用空格字符。必须用点号(.)保持完美矩形网格。
2. **最小平台宽度 >= 2 格**：玩家需要至少 2 格宽的平台才能安全落脚，禁止生成 1 格宽的极限平台。
3. **间隙后落脚平台 >= 2 格宽**：跨越间隙后必须有至少 2 格宽的安全落脚平台。
4. **极限跳跃落点不可放敌人**：极限跳跃的落点处不应有敌人，否则玩家无法同时处理跳跃精度和敌人威胁。
5. **所有间隙必须物理可跨越**：水平间隙不得超过 {PhysicsMetrics.MAX_GAP_WITH_COYOTE} 格，垂直高台不得超过 {PhysicsMetrics.MAX_JUMP_HEIGHT} 格。

## ASCII 字符映射表

| 字符 | 元素 | 字符 | 元素 |
|:---:|:---|:---:|:---|
| # | 实心地面 | B | 弹跳平台 |
| = | 平台 | C | 崩塌平台 |
| W | 墙壁 | - | 单向平台 |
| M | Mario出生点 | > | 移动平台 |
| T | Trickster出生点 | E | 弹跳怪 |
| G | 终点 | e | 简单敌人 |
| ^ | 地刺 | F | 伪装墙 |
| ~ | 火焰陷阱 | H | 隐藏通道 |
| P | 摆锤 | o | 收集物 |
| . | 空气（必须用点号） | | |
";
    }
}
