#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PlannerProductionAssistant — 策划高速生产助手。
///
/// 目标不是替代 AI 写代码，而是把“商业素材包整理 / Theme Profile 批量绑定 / 新机制请求”
/// 三个最容易抵消策划效率的环节压成 Unity 内的一站式入口。
/// </summary>
public class PlannerProductionAssistant : EditorWindow
{
    private DefaultAsset assetFolder;
    private string assetFolderPath = "Assets/Art/Imported";
    private LevelThemeProfile targetTheme;
    private Vector2 scrollPos;
    private string reportText = "";
    private string mechanismRequest = "";
    private string mechanismAssetHint = "";
    private bool includeAsciiHook = true;
    private bool includeTests = true;
    private bool includeArtHook = true;

    private struct ThemeSlotRule
    {
        public string slotKey;
        public string displayName;
        public string[] keywords;

        public ThemeSlotRule(string slotKey, string displayName, params string[] keywords)
        {
            this.slotKey = slotKey;
            this.displayName = displayName;
            this.keywords = keywords;
        }
    }

    private sealed class SpriteCandidate
    {
        public Sprite sprite;
        public string path;
        public string searchText;
        public ArtAssetClassifier.Classification classification;
        public string suggestedSlot;
        public string suggestedName;
    }

    private static readonly ThemeSlotRule[] ThemeSlotRules = new ThemeSlotRule[]
    {
        new ThemeSlotRule("Ground", "地面", "ground", "terrain", "floor", "grass", "dirt", "soil", "land"),
        new ThemeSlotRule("Platform", "平台", "platform", "tile", "ledge", "bridge", "wood_platform"),
        new ThemeSlotRule("Wall", "墙壁", "wall", "brick", "stone_wall", "block_wall"),
        new ThemeSlotRule("Mario", "Mario/Runner", "mario", "runner", "player", "hero"),
        new ThemeSlotRule("Trickster", "Trickster", "trickster", "ghost", "spirit", "mischief"),
        new ThemeSlotRule("SpikeTrap", "地刺", "spike", "spikes", "thorn", "needle"),
        new ThemeSlotRule("FireTrap", "火焰陷阱", "fire", "flame", "lava", "burn", "torch_trap"),
        new ThemeSlotRule("PendulumTrap", "摆锤", "pendulum", "hammer", "swing", "mace"),
        new ThemeSlotRule("BouncingEnemy", "弹跳怪", "bouncing_enemy", "bounce_enemy", "bouncer", "jump_enemy"),
        new ThemeSlotRule("BouncyPlatform", "弹跳平台", "bouncy_platform", "bounce_platform", "spring", "trampoline"),
        new ThemeSlotRule("CollapsingPlatform", "崩塌平台", "collapse", "collapsing", "crumble", "falling_platform"),
        new ThemeSlotRule("OneWayPlatform", "单向平台", "oneway", "one_way", "dropthrough", "drop_through"),
        new ThemeSlotRule("MovingPlatform", "移动平台", "moving_platform", "move_platform", "moving", "lift"),
        new ThemeSlotRule("HiddenPassage", "隐藏通道", "hidden_passage", "passage", "secret", "tunnel"),
        new ThemeSlotRule("FakeWall", "伪装墙", "fake_wall", "illusory", "disguise_wall", "secret_wall"),
        new ThemeSlotRule("GoalZone", "终点", "goal", "finish", "exit", "flag", "portal_goal"),
        new ThemeSlotRule("Collectible", "收集物", "coin", "gem", "key", "heart", "pickup", "collectible"),
        new ThemeSlotRule("SimpleEnemy", "普通敌人", "simple_enemy", "enemy", "monster", "goomba", "slime"),
        new ThemeSlotRule("SawBlade", "锯片", "saw", "sawblade", "blade", "circular_saw"),
        new ThemeSlotRule("FlyingEnemy", "飞行敌人", "flying_enemy", "fly_enemy", "bat", "bird", "flying"),
        new ThemeSlotRule("ConveyorBelt", "传送带", "conveyor", "belt", "conveyor_belt"),
        new ThemeSlotRule("Checkpoint", "检查点", "checkpoint", "save", "flag_checkpoint"),
        new ThemeSlotRule("BreakableBlock", "可破坏方块", "breakable", "break_block", "breakable_block", "crate"),
    };

    [MenuItem("MarioTrickster/Planner Production Assistant %#p", false, 203)]
    public static void ShowWindow()
    {
        PlannerProductionAssistant window = GetWindow<PlannerProductionAssistant>("策划生产助手");
        window.minSize = new Vector2(520, 620);
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        DrawHeader();
        DrawAssetSection();
        DrawThemeSection();
        DrawMechanismSection();
        DrawReportSection();
        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(8);
        GUILayout.Label("Planner Production Assistant", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "把三个短板压成一个入口：①素材包语义巡检/改名建议 ②Theme Profile 自动填槽 ③新机制需求模板复制。\n" +
            "原则：不强行改坏原素材，不替代外部强 AI 写代码，只把 Unity 当前上下文整理成稳定、低摩擦的生产动作。",
            MessageType.Info);
    }

    private void DrawAssetSection()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("1. 素材包语义巡检", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        assetFolder = (DefaultAsset)EditorGUILayout.ObjectField("素材文件夹", assetFolder, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck() && assetFolder != null)
        {
            string selectedPath = AssetDatabase.GetAssetPath(assetFolder);
            if (AssetDatabase.IsValidFolder(selectedPath)) assetFolderPath = selectedPath;
        }

        assetFolderPath = EditorGUILayout.TextField("扫描路径", assetFolderPath);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("使用当前 Project 选择", GUILayout.Height(26)))
        {
            UseCurrentSelectionAsFolder();
        }
        if (GUILayout.Button("生成语义报告", GUILayout.Height(26)))
        {
            GenerateSemanticReport(writeFile: true);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawThemeSection()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("2. Theme Profile 自动绑定", EditorStyles.boldLabel);
        targetTheme = (LevelThemeProfile)EditorGUILayout.ObjectField("目标 Theme", targetTheme, typeof(LevelThemeProfile), false);

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = targetTheme != null;
        if (GUILayout.Button("按素材名自动填槽", GUILayout.Height(30)))
        {
            AutoFillThemeProfile();
        }
        GUI.enabled = true;
        if (GUILayout.Button("只预览匹配", GUILayout.Height(30)))
        {
            GenerateSemanticReport(writeFile: false);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "自动绑定只填能明显识别的槽位；识别不到的保持原状，不会清空你已经手动确认过的 Sprite。",
            MessageType.None);
    }

    private void DrawMechanismSection()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("3. 新机制需求模板", EditorStyles.boldLabel);
        mechanismRequest = EditorGUILayout.TextField("一句话需求", mechanismRequest);
        mechanismAssetHint = EditorGUILayout.TextField("素材提示", mechanismAssetHint);
        includeAsciiHook = EditorGUILayout.Toggle("需要 ASCII 字符/调色板入口", includeAsciiHook);
        includeArtHook = EditorGUILayout.Toggle("需要素材/动画接入", includeArtHook);
        includeTests = EditorGUILayout.Toggle("需要自动化测试", includeTests);

        if (GUILayout.Button("复制给外部 AI / Cursor 的实现请求", GUILayout.Height(30)))
        {
            CopyMechanismPrompt();
        }
    }

    private void DrawReportSection()
    {
        if (string.IsNullOrEmpty(reportText)) return;
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("执行结果", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(reportText, GUILayout.MinHeight(180));
    }

    private void UseCurrentSelectionAsFolder()
    {
        UnityEngine.Object selected = Selection.activeObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("提示", "请先在 Project 面板选中一个素材文件夹。", "好的");
            return;
        }

        string selectedPath = AssetDatabase.GetAssetPath(selected);
        if (!AssetDatabase.IsValidFolder(selectedPath))
        {
            selectedPath = Path.GetDirectoryName(selectedPath)?.Replace('\\', '/');
        }

        if (!string.IsNullOrEmpty(selectedPath) && AssetDatabase.IsValidFolder(selectedPath))
        {
            assetFolderPath = selectedPath;
            assetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(selectedPath);
        }
    }

    private void GenerateSemanticReport(bool writeFile)
    {
        List<SpriteCandidate> candidates = BuildCandidates();
        StringBuilder sb = BuildReportMarkdown(candidates);
        reportText = sb.ToString();

        if (writeFile)
        {
            string reportFolder = "Assets/Art/Imported";
            EnsureFolder(reportFolder);
            string path = $"{reportFolder}/PlannerSemanticReport_{DateTime.Now:yyyyMMdd_HHmmss}.md";
            File.WriteAllText(path, reportText, Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[PlannerProductionAssistant] 语义报告已生成: {path}");
        }
    }

    private void AutoFillThemeProfile()
    {
        if (targetTheme == null) return;

        List<SpriteCandidate> candidates = BuildCandidates();
        if (candidates.Count == 0)
        {
            reportText = "没有在扫描路径中找到 Sprite。";
            return;
        }

        Undo.RecordObject(targetTheme, "Auto Fill Level Theme Profile");
        int changed = 0;
        changed += TryAssignMainSprite("Ground", ref targetTheme.groundSprite, candidates) ? 1 : 0;
        changed += TryAssignMainSprite("Platform", ref targetTheme.platformSprite, candidates) ? 1 : 0;
        changed += TryAssignMainSprite("Wall", ref targetTheme.wallSprite, candidates) ? 1 : 0;
        changed += TryAssignMainSprite("Mario", ref targetTheme.marioSprite, candidates) ? 1 : 0;
        changed += TryAssignMainSprite("Trickster", ref targetTheme.tricksterSprite, candidates) ? 1 : 0;
        changed += FillElementMappings(targetTheme, candidates);

        EditorUtility.SetDirty(targetTheme);
        AssetDatabase.SaveAssets();

        StringBuilder sb = BuildReportMarkdown(candidates);
        sb.AppendLine();
        sb.AppendLine($"> 自动绑定完成：新增/更新 {changed} 个槽位。已存在且无法更高置信匹配的槽位保持不动。");
        reportText = sb.ToString();
        Debug.Log($"[PlannerProductionAssistant] Theme '{targetTheme.themeName}' auto-filled: {changed} slots changed.");
    }

    private bool TryAssignMainSprite(string slotKey, ref Sprite field, List<SpriteCandidate> candidates)
    {
        SpriteCandidate candidate = PickBestCandidate(slotKey, candidates);
        if (candidate == null) return false;
        if (field == candidate.sprite) return false;
        if (field != null && ScoreCandidate(slotKey, BuildSearchText(field, AssetDatabase.GetAssetPath(field))) >= ScoreCandidate(slotKey, candidate.searchText)) return false;
        field = candidate.sprite;
        return true;
    }

    private int FillElementMappings(LevelThemeProfile theme, List<SpriteCandidate> candidates)
    {
        if (theme.elementSprites == null) theme.elementSprites = new ElementSpriteMapping[0];
        List<ElementSpriteMapping> mappings = theme.elementSprites.ToList();
        int changed = 0;

        foreach (ThemeSlotRule rule in ThemeSlotRules)
        {
            if (IsMainSlot(rule.slotKey)) continue;
            SpriteCandidate candidate = PickBestCandidate(rule.slotKey, candidates);
            if (candidate == null) continue;

            ElementSpriteMapping mapping = mappings.FirstOrDefault(m => m.elementKey == rule.slotKey);
            if (mapping == null)
            {
                mapping = new ElementSpriteMapping { elementKey = rule.slotKey };
                mappings.Add(mapping);
            }

            if (mapping.sprite == candidate.sprite) continue;
            if (mapping.sprite != null && ScoreCandidate(rule.slotKey, BuildSearchText(mapping.sprite, AssetDatabase.GetAssetPath(mapping.sprite))) >= ScoreCandidate(rule.slotKey, candidate.searchText)) continue;
            mapping.sprite = candidate.sprite;
            changed++;
        }

        theme.elementSprites = mappings.ToArray();
        return changed;
    }

    private List<SpriteCandidate> BuildCandidates()
    {
        List<Sprite> sprites = FindSprites(assetFolderPath);
        List<SpriteCandidate> candidates = new List<SpriteCandidate>();
        foreach (Sprite sprite in sprites)
        {
            if (sprite == null) continue;
            string path = AssetDatabase.GetAssetPath(sprite);
            string searchText = BuildSearchText(sprite, path);
            ArtAssetClassifier.Classification classification = ArtAssetClassifier.Classify(null, new Sprite[] { sprite }, -1);
            string slot = SuggestSlot(searchText, classification);
            candidates.Add(new SpriteCandidate
            {
                sprite = sprite,
                path = path,
                searchText = searchText,
                classification = classification,
                suggestedSlot = slot,
                suggestedName = BuildSuggestedName(slot, sprite.name)
            });
        }
        return candidates.OrderBy(c => c.suggestedSlot).ThenBy(c => c.sprite.name).ToList();
    }

    private List<Sprite> FindSprites(string folderPath)
    {
        List<Sprite> sprites = new List<Sprite>();
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath)) return sprites;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new string[] { folderPath });
        HashSet<int> seen = new HashSet<int>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
            {
                if (sprite != null && seen.Add(sprite.GetInstanceID())) sprites.Add(sprite);
            }
        }
        return sprites;
    }

    private StringBuilder BuildReportMarkdown(List<SpriteCandidate> candidates)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# Planner Production Assistant 语义巡检报告");
        sb.AppendLine();
        sb.AppendLine($"扫描路径：`{assetFolderPath}`");
        sb.AppendLine($"Sprite 数量：{candidates.Count}");
        sb.AppendLine();
        sb.AppendLine("| Sprite | 分类 | 动画 | 推荐槽位 | 建议命名 | 路径 |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (SpriteCandidate c in candidates)
        {
            sb.AppendLine($"| {EscapePipe(c.sprite.name)} | {c.classification.role} | {c.classification.animationMode} | {c.suggestedSlot} | `{c.suggestedName}` | `{c.path}` |");
        }
        sb.AppendLine();
        sb.AppendLine("> 建议：优先处理推荐槽位为 Unknown 的素材；若同一槽位有多张候选图，保留最符合关卡语义的一张作为 Theme Profile 插槽，其余可作为动画帧或备用皮肤。");
        return sb;
    }

    private void CopyMechanismPrompt()
    {
        string request = string.IsNullOrWhiteSpace(mechanismRequest) ? "我需要一个新的关卡机关/角色技能" : mechanismRequest.Trim();
        string assetHint = string.IsNullOrWhiteSpace(mechanismAssetHint) ? "暂无素材，先用白盒占位，后续走 Asset Import / Apply Art 接入" : mechanismAssetHint.Trim();

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("【MarioTrickster 新机制实现请求】");
        sb.AppendLine();
        sb.AppendLine($"大白话需求：{request}");
        sb.AppendLine($"素材提示：{assetHint}");
        sb.AppendLine();
        sb.AppendLine("请按项目现有最优框架后台实现，不要让用户填写工程参数：");
        sb.AppendLine("1. 保持 Root/Visual 视碰分离，行为组件写在 Root，Sprite/动画/SEF 写在 Visual。");
        sb.AppendLine("2. 优先复用 LevelElementBase / ControllablePropBase / ImportedAssetMarker / ArtAssetClassifier / SpriteFrameAnimator / SpriteStateAnimator。 ");
        if (includeAsciiHook) sb.AppendLine("3. 如属于关卡元素，请补 ASCII 字符映射、Element Palette 入口、Theme Profile 槽位与教程说明。");
        if (includeArtHook) sb.AppendLine("4. 如需要美术，请支持商业素材通过 Asset Import Pipeline 或 Apply Art to Selected 接入，并保留白盒可用回退。");
        if (includeTests) sb.AppendLine("5. 补 EditMode/PlayMode 安全网，确保旧关卡元素和现有 135 测试不退化。");
        sb.AppendLine("6. 完成后更新 SESSION_TRACKER，并用一句大白话汇报如何使用。");

        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        reportText = "已复制新机制实现请求到剪贴板。\n\n" + sb;
    }

    private string SuggestSlot(string searchText, ArtAssetClassifier.Classification classification)
    {
        ThemeSlotRule bestRule = default;
        int bestScore = 0;
        foreach (ThemeSlotRule rule in ThemeSlotRules)
        {
            int score = ScoreCandidate(rule.slotKey, searchText);
            if (score > bestScore)
            {
                bestScore = score;
                bestRule = rule;
            }
        }

        if (bestScore > 0) return bestRule.slotKey;

        switch (classification.role)
        {
            case ArtAssetClassifier.AssetRole.Character: return "Mario";
            case ArtAssetClassifier.AssetRole.Enemy: return "SimpleEnemy";
            case ArtAssetClassifier.AssetRole.Collectible: return "Collectible";
            case ArtAssetClassifier.AssetRole.Hazard: return "SpikeTrap";
            case ArtAssetClassifier.AssetRole.Platform: return "Platform";
            case ArtAssetClassifier.AssetRole.Background: return "Background";
            default: return "Unknown";
        }
    }

    private SpriteCandidate PickBestCandidate(string slotKey, List<SpriteCandidate> candidates)
    {
        return candidates
            .Select(c => new { candidate = c, score = ScoreCandidate(slotKey, c.searchText) + (c.suggestedSlot == slotKey ? 3 : 0) })
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.candidate.sprite.name.Length)
            .Select(x => x.candidate)
            .FirstOrDefault();
    }

    private int ScoreCandidate(string slotKey, string searchText)
    {
        ThemeSlotRule rule = ThemeSlotRules.FirstOrDefault(r => r.slotKey == slotKey);
        if (string.IsNullOrEmpty(rule.slotKey)) return 0;
        int score = 0;
        foreach (string keyword in rule.keywords)
        {
            if (ContainsToken(searchText, keyword)) score += keyword.Length + 2;
        }
        if (ContainsToken(searchText, slotKey)) score += 10;
        return score;
    }

    private static bool IsMainSlot(string slotKey)
    {
        return slotKey == "Ground" || slotKey == "Platform" || slotKey == "Wall" || slotKey == "Mario" || slotKey == "Trickster";
    }

    private static string BuildSearchText(Sprite sprite, string path)
    {
        return Normalize((sprite != null ? sprite.name : "") + " " + path);
    }

    private static string BuildSuggestedName(string slot, string originalName)
    {
        string clean = Normalize(originalName).Trim('_');
        if (string.IsNullOrEmpty(slot) || slot == "Unknown") return clean;
        string prefix = Normalize(slot).Trim('_');
        if (clean.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase)) return clean;
        return prefix + "_" + clean;
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        StringBuilder builder = new StringBuilder(text.Length + 8);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsLetterOrDigit(c)) builder.Append(char.ToLowerInvariant(c));
            else if (builder.Length == 0 || builder[builder.Length - 1] != '_') builder.Append('_');
        }
        return builder.ToString();
    }

    private static bool ContainsToken(string text, string token)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token)) return false;
        string normalizedText = Normalize(text);
        string normalizedToken = Normalize(token).Trim('_');
        if (string.IsNullOrEmpty(normalizedToken)) return false;
        return ("_" + normalizedText + "_").Contains("_" + normalizedToken + "_");
    }

    private static string EscapePipe(string text)
    {
        return (text ?? string.Empty).Replace("|", "\\|");
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), folder);
        Directory.CreateDirectory(fullPath);
        AssetDatabase.Refresh();
    }
}
#endif
