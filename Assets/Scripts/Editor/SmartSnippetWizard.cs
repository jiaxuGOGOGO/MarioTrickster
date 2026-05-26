using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// SmartSnippetWizard — GBG 风格的智能片段向导
///
/// 核心理念（来自 Game Builder Garage 的 Guided Lessons）：
///   GBG 的导航课程用"一步一步引导 + 即时预览"的方式教用户创作。
///   用户不需要理解底层逻辑，只需要回答几个简单问题就能得到想要的结果。
///
/// 在 MarioTrickster 中的映射：
///   "智能片段向导" = 通过简单的参数滑块，实时生成参数化的 ASCII 片段。
///   用户调整参数时，预览区实时更新，所见即所得。
///
/// 功能：
///   1. 参数化模板：不是固定的片段，而是可以通过参数调整的"活模板"
///   2. 实时预览：参数变化时 ASCII 预览即时更新
///   3. 一键生成：满意后一键放入场景
///   4. 难度标签：每个模板标注预估难度（简单/中等/困难）
///   5. 物理验证：自动检查跳跃距离是否在玩家能力范围内
/// </summary>
public class SmartSnippetWizard : EditorWindow
{
    // ═══════════════════════════════════════════════════
    // 数据结构
    // ═══════════════════════════════════════════════════

    public enum Difficulty { Easy, Medium, Hard, Expert }

    private class WizardTemplate
    {
        public string name;
        public string description;
        public Difficulty baseDifficulty;
        public WizardParam[] parameters;
        public System.Func<float[], string> generator; // 参数 → ASCII

        public WizardTemplate(string name, string desc, Difficulty diff,
            WizardParam[] parameters, System.Func<float[], string> generator)
        {
            this.name = name;
            this.description = desc;
            this.baseDifficulty = diff;
            this.parameters = parameters;
            this.generator = generator;
        }
    }

    private class WizardParam
    {
        public string label;
        public float defaultValue;
        public float min;
        public float max;
        public bool isInt;
        public string unit;

        public WizardParam(string label, float defaultVal, float min, float max, bool isInt = false, string unit = "")
        {
            this.label = label;
            this.defaultValue = defaultVal;
            this.min = min;
            this.max = max;
            this.isInt = isInt;
            this.unit = unit;
        }
    }

    // ═══════════════════════════════════════════════════
    // 状态
    // ═══════════════════════════════════════════════════
    private List<WizardTemplate> templates;
    private int selectedTemplate = 0;
    private float[] currentParams;
    private string currentPreview = "";
    private Vector2 previewScroll;
    private Vector2 templateScroll;
    private bool showPhysicsCheck = true;

    // ═══════════════════════════════════════════════════
    // 菜单入口
    // ═══════════════════════════════════════════════════

    [MenuItem("MarioTrickster/Smart Snippet Wizard %#w", false, 11)]
    public static void ShowWindow()
    {
        var window = GetWindow<SmartSnippetWizard>("Smart Snippets");
        window.minSize = new Vector2(500, 450);
    }

    // ═══════════════════════════════════════════════════
    // 生命周期
    // ═══════════════════════════════════════════════════

    private void OnEnable()
    {
        templates = BuildTemplates();
        SelectTemplate(0);
    }

    private void SelectTemplate(int index)
    {
        if (index < 0 || index >= templates.Count) return;
        selectedTemplate = index;
        currentParams = new float[templates[index].parameters.Length];
        for (int i = 0; i < currentParams.Length; i++)
            currentParams[i] = templates[index].parameters[i].defaultValue;
        RegeneratePreview();
    }

    private void RegeneratePreview()
    {
        if (templates == null || selectedTemplate >= templates.Count) return;
        currentPreview = templates[selectedTemplate].generator(currentParams);
    }

    // ═══════════════════════════════════════════════════
    // GUI
    // ═══════════════════════════════════════════════════

    private void OnGUI()
    {
        if (templates == null) return;

        EditorGUILayout.BeginHorizontal();

        // ── 左侧：模板列表 ──
        EditorGUILayout.BeginVertical("box", GUILayout.Width(180));
        EditorGUILayout.LabelField("模板库", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        templateScroll = EditorGUILayout.BeginScrollView(templateScroll);
        for (int i = 0; i < templates.Count; i++)
        {
            var t = templates[i];
            bool isSelected = (i == selectedTemplate);

            // 难度颜色
            GUI.color = GetDifficultyColor(t.baseDifficulty);
            GUIStyle btnStyle = isSelected ?
                new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold } :
                GUI.skin.button;

            if (GUILayout.Button($"{GetDifficultyIcon(t.baseDifficulty)} {t.name}", btnStyle, GUILayout.Height(24)))
            {
                SelectTemplate(i);
            }
            GUI.color = Color.white;
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // ── 右侧：参数 + 预览 ──
        EditorGUILayout.BeginVertical();

        var template = templates[selectedTemplate];

        // 标题 + 描述
        EditorGUILayout.LabelField(template.name, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(template.description, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(4);

        // 参数滑块
        EditorGUILayout.LabelField("参数调整", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        bool changed = false;
        for (int i = 0; i < template.parameters.Length; i++)
        {
            var p = template.parameters[i];
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(p.label, GUILayout.Width(100));

            float newVal;
            if (p.isInt)
            {
                newVal = EditorGUILayout.IntSlider((int)currentParams[i], (int)p.min, (int)p.max);
            }
            else
            {
                newVal = EditorGUILayout.Slider(currentParams[i], p.min, p.max);
            }

            if (!string.IsNullOrEmpty(p.unit))
                EditorGUILayout.LabelField(p.unit, GUILayout.Width(30));

            if (!Mathf.Approximately(newVal, currentParams[i]))
            {
                currentParams[i] = newVal;
                changed = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();

        if (changed)
            RegeneratePreview();

        EditorGUILayout.Space(4);

        // 物理验证
        if (showPhysicsCheck)
        {
            DrawPhysicsValidation();
        }

        EditorGUILayout.Space(4);

        // ASCII 预览
        EditorGUILayout.LabelField("实时预览", EditorStyles.boldLabel);
        previewScroll = EditorGUILayout.BeginScrollView(previewScroll, "box", GUILayout.Height(150));
        GUIStyle monoStyle = new GUIStyle(EditorStyles.label)
        {
            font = Font.CreateDynamicFontFromOSFont("Courier New", 12),
            fontSize = 11,
            wordWrap = false,
            richText = false
        };
        EditorGUILayout.LabelField(currentPreview, monoStyle, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        // 尺寸信息
        string[] lines = currentPreview.Split('\n');
        int height = lines.Length;
        int width = 0;
        foreach (string l in lines) if (l.Length > width) width = l.Length;
        EditorGUILayout.LabelField($"尺寸: {width} x {height} 格", EditorStyles.miniLabel);

        EditorGUILayout.Space(8);

        // 操作按钮
        EditorGUILayout.BeginHorizontal();

        GUI.color = new Color(0.3f, 0.9f, 0.3f);
        if (GUILayout.Button("▶ 生成到场景", GUILayout.Height(32)))
        {
            GenerateToScene();
        }

        GUI.color = new Color(0.5f, 0.8f, 1f);
        if (GUILayout.Button("📋 复制到剪贴板", GUILayout.Height(32)))
        {
            EditorGUIUtility.systemCopyBuffer = currentPreview;
            Debug.Log("[SmartSnippet] ASCII 已复制到剪贴板。");
        }

        GUI.color = new Color(1f, 0.85f, 0.3f);
        if (GUILayout.Button("📎 追加到编辑器", GUILayout.Height(32)))
        {
            AppendToCustomEditor();
        }

        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    // ═══════════════════════════════════════════════════
    // 物理验证
    // ═══════════════════════════════════════════════════

    private void DrawPhysicsValidation()
    {
        // 分析当前预览中的跳跃需求
        string[] lines = currentPreview.Split('\n');
        int maxGap = AnalyzeMaxGap(lines);
        int maxHeight = AnalyzeMaxHeight(lines);

        bool gapOk = maxGap <= PhysicsMetrics.ASCII_MAX_GAP;
        bool heightOk = maxHeight <= PhysicsMetrics.ASCII_MAX_HEIGHT;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("物理验证", EditorStyles.boldLabel);

        GUI.color = gapOk ? Color.green : Color.red;
        EditorGUILayout.LabelField(
            $"最大间隙: {maxGap} 格 {(gapOk ? "✓" : "✗")} (上限 {PhysicsMetrics.ASCII_MAX_GAP})",
            EditorStyles.miniLabel);

        GUI.color = heightOk ? Color.green : Color.red;
        EditorGUILayout.LabelField(
            $"最大高度: {maxHeight} 格 {(heightOk ? "✓" : "✗")} (上限 {PhysicsMetrics.ASCII_MAX_HEIGHT})",
            EditorStyles.miniLabel);

        GUI.color = Color.white;

        if (!gapOk || !heightOk)
        {
            EditorGUILayout.HelpBox("⚠ 当前参数可能导致玩家无法通过！请调整参数。", MessageType.Warning);
        }

        EditorGUILayout.EndVertical();
    }

    private int AnalyzeMaxGap(string[] lines)
    {
        int maxGap = 0;
        foreach (string line in lines)
        {
            int currentGap = 0;
            bool foundSolid = false;
            foreach (char c in line)
            {
                if (c == '#' || c == '=' || c == 'W' || c == '-' || c == 'B' || c == 'C')
                {
                    if (foundSolid && currentGap > maxGap)
                        maxGap = currentGap;
                    currentGap = 0;
                    foundSolid = true;
                }
                else if (foundSolid && c == '.')
                {
                    currentGap++;
                }
            }
        }
        return maxGap;
    }

    private int AnalyzeMaxHeight(string[] lines)
    {
        // 简化分析：找到相邻平台之间的最大垂直距离
        int maxHeight = 0;
        for (int col = 0; col < GetMaxWidth(lines); col++)
        {
            int lastSolidRow = -1;
            for (int row = 0; row < lines.Length; row++)
            {
                if (col < lines[row].Length)
                {
                    char c = lines[row][col];
                    if (c == '#' || c == '=' || c == 'W' || c == '-' || c == 'B')
                    {
                        if (lastSolidRow >= 0)
                        {
                            int gap = row - lastSolidRow - 1;
                            if (gap > maxHeight) maxHeight = gap;
                        }
                        lastSolidRow = row;
                    }
                }
            }
        }
        return maxHeight;
    }

    private int GetMaxWidth(string[] lines)
    {
        int max = 0;
        foreach (string l in lines) if (l.Length > max) max = l.Length;
        return max;
    }

    // ═══════════════════════════════════════════════════
    // 生成操作
    // ═══════════════════════════════════════════════════

    private void GenerateToScene()
    {
        if (string.IsNullOrEmpty(currentPreview)) return;

        Undo.SetCurrentGroupName($"Smart Snippet: {templates[selectedTemplate].name}");

        GameObject root = AsciiLevelGenerator.GenerateFromTemplate(currentPreview, false, true);
        if (root != null)
        {
            // 放到 Scene 视图中心
            SceneView sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                Vector3 pivot = sv.pivot;
                pivot.z = 0;
                root.transform.position = new Vector3(
                    Mathf.RoundToInt(pivot.x),
                    Mathf.RoundToInt(pivot.y), 0);
            }

            // 挂到主 Root 下
            GameObject mainRoot = GameObject.Find("AsciiLevel_Root");
            if (mainRoot != null)
            {
                // 将子物体移到主 Root
                List<Transform> children = new List<Transform>();
                foreach (Transform child in root.transform)
                    children.Add(child);
                foreach (Transform child in children)
                    child.parent = mainRoot.transform;
                Object.DestroyImmediate(root);
            }
            else
            {
                root.name = "AsciiLevel_Root";
                PlayableEnvironmentBuilder.EnsurePlayableEnvironment(root);
            }

            Undo.RegisterCreatedObjectUndo(root != null ? root : mainRoot, "Smart Snippet Generate");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[SmartSnippet] 已生成: {templates[selectedTemplate].name}");
        }
    }

    private void AppendToCustomEditor()
    {
        // 将当前预览追加到 Level Studio 的 Custom Template Editor
        var window = EditorWindow.GetWindow<TestConsoleWindow>("Level Studio", false);
        if (window != null)
        {
            // 通过反射设置 customAsciiTemplate
            var field = typeof(TestConsoleWindow).GetField("customAsciiTemplate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                string existing = (string)field.GetValue(window);
                if (string.IsNullOrEmpty(existing))
                    field.SetValue(window, currentPreview);
                else
                    field.SetValue(window, existing + "\n" + currentPreview);
                window.Repaint();
                Debug.Log("[SmartSnippet] 已追加到 Custom Template Editor。");
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // 难度辅助
    // ═══════════════════════════════════════════════════

    private Color GetDifficultyColor(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Easy: return new Color(0.3f, 0.9f, 0.3f);
            case Difficulty.Medium: return new Color(1f, 0.85f, 0.2f);
            case Difficulty.Hard: return new Color(1f, 0.5f, 0.2f);
            case Difficulty.Expert: return new Color(1f, 0.2f, 0.2f);
            default: return Color.white;
        }
    }

    private string GetDifficultyIcon(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Easy: return "●";
            case Difficulty.Medium: return "●●";
            case Difficulty.Hard: return "●●●";
            case Difficulty.Expert: return "●●●●";
            default: return "?";
        }
    }

    // ═══════════════════════════════════════════════════
    // 模板库构建
    // ═══════════════════════════════════════════════════

    private List<WizardTemplate> BuildTemplates()
    {
        var list = new List<WizardTemplate>();

        // ── 平坦跑道 ──
        list.Add(new WizardTemplate(
            "平坦跑道",
            "最简单的平地通道，适合开场或休息区",
            Difficulty.Easy,
            new WizardParam[] {
                new WizardParam("长度", 12, 6, 30, true, "格"),
                new WizardParam("高度", 4, 3, 8, true, "格")
            },
            (p) => GenerateFlatRun((int)p[0], (int)p[1])
        ));

        // ── 阶梯攀爬 ──
        list.Add(new WizardTemplate(
            "阶梯攀爬",
            "逐级上升的阶梯结构",
            Difficulty.Easy,
            new WizardParam[] {
                new WizardParam("台阶数", 5, 3, 10, true, "级"),
                new WizardParam("台阶宽度", 3, 2, 5, true, "格"),
                new WizardParam("台阶高度", 1, 1, 2, true, "格")
            },
            (p) => GenerateStaircase((int)p[0], (int)p[1], (int)p[2])
        ));

        // ── 间隙跳跃 ──
        list.Add(new WizardTemplate(
            "间隙跳跃",
            "需要跳跃通过的间隙序列",
            Difficulty.Medium,
            new WizardParam[] {
                new WizardParam("间隙数", 3, 1, 6, true, "个"),
                new WizardParam("间隙宽度", 3, 2, 4, true, "格"),
                new WizardParam("平台宽度", 3, 2, 5, true, "格")
            },
            (p) => GenerateGapJumps((int)p[0], (int)p[1], (int)p[2])
        ));

        // ── 地刺走廊 ──
        list.Add(new WizardTemplate(
            "地刺走廊",
            "需要精确跳跃避开地刺的通道",
            Difficulty.Medium,
            new WizardParam[] {
                new WizardParam("长度", 12, 8, 24, true, "格"),
                new WizardParam("安全岛间距", 3, 2, 5, true, "格"),
                new WizardParam("安全岛宽度", 1, 1, 3, true, "格")
            },
            (p) => GenerateSpikeGauntlet((int)p[0], (int)p[1], (int)p[2])
        ));

        // ── 移动平台序列 ──
        list.Add(new WizardTemplate(
            "移动平台序列",
            "需要借助移动平台通过的空中路段",
            Difficulty.Medium,
            new WizardParam[] {
                new WizardParam("平台数", 3, 2, 6, true, "个"),
                new WizardParam("间距", 4, 3, 6, true, "格"),
                new WizardParam("高度", 6, 4, 10, true, "格")
            },
            (p) => GenerateMovingPlatformSequence((int)p[0], (int)p[1], (int)p[2])
        ));

        // ── 弹跳塔 ──
        list.Add(new WizardTemplate(
            "弹跳塔",
            "利用弹跳平台向上攀爬的垂直结构",
            Difficulty.Medium,
            new WizardParam[] {
                new WizardParam("层数", 4, 2, 8, true, "层"),
                new WizardParam("塔宽", 7, 5, 11, true, "格")
            },
            (p) => GenerateBounceTower((int)p[0], (int)p[1])
        ));

        // ── 传送带迷宫 ──
        list.Add(new WizardTemplate(
            "传送带迷宫",
            "传送带组成的方向迷宫",
            Difficulty.Hard,
            new WizardParam[] {
                new WizardParam("宽度", 10, 8, 16, true, "格"),
                new WizardParam("层数", 3, 2, 5, true, "层")
            },
            (p) => GenerateConveyorMaze((int)p[0], (int)p[1])
        ));

        // ── 混合挑战 ──
        list.Add(new WizardTemplate(
            "混合挑战",
            "地刺+间隙+移动平台的综合挑战段",
            Difficulty.Hard,
            new WizardParam[] {
                new WizardParam("长度", 20, 14, 36, true, "格"),
                new WizardParam("难度系数", 2, 1, 3, true, "")
            },
            (p) => GenerateMixedChallenge((int)p[0], (int)p[1])
        ));

        // ── 封闭竞技场 ──
        list.Add(new WizardTemplate(
            "封闭竞技场",
            "四面围墙的对决空间",
            Difficulty.Medium,
            new WizardParam[] {
                new WizardParam("宽度", 11, 7, 17, true, "格"),
                new WizardParam("高度", 8, 6, 12, true, "格"),
                new WizardParam("平台数", 2, 0, 4, true, "个")
            },
            (p) => GenerateArena((int)p[0], (int)p[1], (int)p[2])
        ));

        // ── 锯片地狱 ──
        list.Add(new WizardTemplate(
            "锯片地狱",
            "密集锯片的极限躲避挑战",
            Difficulty.Expert,
            new WizardParam[] {
                new WizardParam("长度", 16, 10, 24, true, "格"),
                new WizardParam("锯片密度", 3, 2, 5, true, "个/段")
            },
            (p) => GenerateSawHell((int)p[0], (int)p[1])
        ));

        return list;
    }

    // ═══════════════════════════════════════════════════
    // 生成器函数
    // ═══════════════════════════════════════════════════

    private string GenerateFlatRun(int length, int height)
    {
        StringBuilder sb = new StringBuilder();
        for (int y = 0; y < height - 1; y++)
        {
            sb.AppendLine(new string('.', length));
        }
        sb.Append(new string('#', length));
        return sb.ToString();
    }

    private string GenerateStaircase(int steps, int stepWidth, int stepHeight)
    {
        int totalWidth = steps * stepWidth + 2;
        int totalHeight = steps * stepHeight + 2;

        char[,] grid = new char[totalHeight, totalWidth];
        for (int y = 0; y < totalHeight; y++)
            for (int x = 0; x < totalWidth; x++)
                grid[y, x] = '.';

        // 底部地面
        for (int x = 0; x < totalWidth; x++)
            grid[totalHeight - 1, x] = '#';

        // 阶梯
        for (int s = 0; s < steps; s++)
        {
            int startX = s * stepWidth + 1;
            int y = totalHeight - 2 - s * stepHeight;
            if (y < 0) break;
            for (int x = startX; x < startX + stepWidth && x < totalWidth; x++)
                grid[y, x] = '=';
        }

        return GridToString(grid, totalHeight, totalWidth);
    }

    private string GenerateGapJumps(int gapCount, int gapWidth, int platformWidth)
    {
        int totalWidth = (gapCount + 1) * platformWidth + gapCount * gapWidth;
        int height = 4;

        StringBuilder sb = new StringBuilder();
        for (int y = 0; y < height - 1; y++)
            sb.AppendLine(new string('.', totalWidth));

        // 地面层（带间隙）
        StringBuilder ground = new StringBuilder();
        for (int i = 0; i <= gapCount; i++)
        {
            ground.Append(new string('#', platformWidth));
            if (i < gapCount)
                ground.Append(new string('.', gapWidth));
        }
        sb.Append(ground.ToString());
        return sb.ToString();
    }

    private string GenerateSpikeGauntlet(int length, int spacing, int safeWidth)
    {
        StringBuilder sb = new StringBuilder();
        // 空气层
        sb.AppendLine(new string('.', length));
        sb.AppendLine(new string('.', length));

        // 地刺层
        StringBuilder spikeLine = new StringBuilder();
        int x = 0;
        bool isSafe = true;
        while (x < length)
        {
            if (isSafe)
            {
                int w = Mathf.Min(safeWidth, length - x);
                spikeLine.Append(new string('#', w));
                x += w;
            }
            else
            {
                int w = Mathf.Min(spacing, length - x);
                spikeLine.Append(new string('^', w));
                x += w;
            }
            isSafe = !isSafe;
        }
        sb.AppendLine(spikeLine.ToString());

        // 地面
        sb.Append(new string('#', length));
        return sb.ToString();
    }

    private string GenerateMovingPlatformSequence(int count, int spacing, int height)
    {
        int totalWidth = count * spacing + 2;

        char[,] grid = new char[height, totalWidth];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < totalWidth; x++)
                grid[y, x] = '.';

        // 底部
        for (int x = 0; x < totalWidth; x++)
            grid[height - 1, x] = '#';

        // 起始平台
        grid[height - 1, 0] = '#';
        grid[height - 1, 1] = '#';

        // 移动平台（用 > 表示）
        for (int i = 0; i < count; i++)
        {
            int px = 2 + i * spacing + spacing / 2;
            int py = height / 2;
            if (px < totalWidth)
                grid[py, px] = '>';
        }

        // 终点平台
        for (int x = totalWidth - 3; x < totalWidth; x++)
            grid[height - 1, x] = '#';

        return GridToString(grid, height, totalWidth);
    }

    private string GenerateBounceTower(int floors, int width)
    {
        int height = floors * 3 + 2;

        char[,] grid = new char[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                grid[y, x] = '.';

        // 左右墙
        for (int y = 0; y < height; y++)
        {
            grid[y, 0] = 'W';
            grid[y, width - 1] = 'W';
        }

        // 底部
        for (int x = 0; x < width; x++)
            grid[height - 1, x] = '#';

        // 弹跳平台（交替左右放置）
        for (int f = 0; f < floors; f++)
        {
            int y = height - 3 - f * 3;
            if (y < 1) break;
            int x = (f % 2 == 0) ? 2 : width - 3;
            grid[y, x] = 'B';
        }

        // 顶部目标
        grid[1, width / 2] = 'G';

        return GridToString(grid, height, width);
    }

    private string GenerateConveyorMaze(int width, int layers)
    {
        int height = layers * 3 + 2;

        char[,] grid = new char[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                grid[y, x] = '.';

        // 底部
        for (int x = 0; x < width; x++)
            grid[height - 1, x] = '#';

        // 传送带层（交替方向）
        for (int l = 0; l < layers; l++)
        {
            int y = height - 2 - l * 3;
            if (y < 1) break;
            char dir = (l % 2 == 0) ? '<' : '<'; // 用 < 表示传送带
            int startX = (l % 2 == 0) ? 1 : 2;
            int endX = (l % 2 == 0) ? width - 2 : width - 1;
            for (int x = startX; x < endX; x++)
                grid[y, x] = dir;
        }

        return GridToString(grid, height, width);
    }

    private string GenerateMixedChallenge(int length, int difficulty)
    {
        StringBuilder sb = new StringBuilder();
        // 空气层
        sb.AppendLine(new string('.', length));
        sb.AppendLine(new string('.', length));

        // 混合层
        StringBuilder mixLine = new StringBuilder();
        int x = 0;
        int pattern = 0;
        while (x < length)
        {
            switch (pattern % (4 - difficulty + 1))
            {
                case 0: // 安全平台
                    int safeLen = Mathf.Min(3, length - x);
                    mixLine.Append(new string('#', safeLen));
                    x += safeLen;
                    break;
                case 1: // 地刺
                    int spikeLen = Mathf.Min(difficulty + 1, length - x);
                    mixLine.Append(new string('^', spikeLen));
                    x += spikeLen;
                    break;
                case 2: // 间隙
                    int gapLen = Mathf.Min(difficulty + 1, length - x);
                    mixLine.Append(new string('.', gapLen));
                    x += gapLen;
                    break;
                default:
                    mixLine.Append('#');
                    x++;
                    break;
            }
            pattern++;
        }
        sb.AppendLine(mixLine.ToString());

        // 地面
        sb.Append(new string('#', length));
        return sb.ToString();
    }

    private string GenerateArena(int width, int height, int platforms)
    {
        char[,] grid = new char[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                grid[y, x] = '.';

        // 四面墙
        for (int x = 0; x < width; x++)
        {
            grid[0, x] = 'W';
            grid[height - 1, x] = '#';
        }
        for (int y = 0; y < height; y++)
        {
            grid[y, 0] = 'W';
            grid[y, width - 1] = 'W';
        }

        // 内部平台
        for (int p = 0; p < platforms; p++)
        {
            int py = height / 2 - 1 + (p % 2 == 0 ? -1 : 1);
            int px = 2 + p * (width - 4) / Mathf.Max(platforms, 1);
            int pw = 3;
            for (int i = 0; i < pw && px + i < width - 1; i++)
                grid[py, px + i] = '=';
        }

        // 出生点
        grid[height - 2, 2] = 'M';
        grid[height - 2, width - 3] = 'T';

        return GridToString(grid, height, width);
    }

    private string GenerateSawHell(int length, int density)
    {
        int height = 6;
        char[,] grid = new char[height, length];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < length; x++)
                grid[y, x] = '.';

        // 地面和天花板
        for (int x = 0; x < length; x++)
        {
            grid[0, x] = '#';
            grid[height - 1, x] = '#';
        }

        // 锯片（交替上下放置）
        int sawSpacing = Mathf.Max(length / (density * 2), 2);
        for (int i = 0; i < density * 2; i++)
        {
            int x = 2 + i * sawSpacing;
            if (x >= length - 1) break;
            int y = (i % 2 == 0) ? 1 : height - 2;
            grid[y, x] = '@';
        }

        return GridToString(grid, height, length);
    }

    // ═══════════════════════════════════════════════════
    // 工具方法
    // ═══════════════════════════════════════════════════

    private string GridToString(char[,] grid, int height, int width)
    {
        StringBuilder sb = new StringBuilder();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                sb.Append(grid[y, x]);
            if (y < height - 1)
                sb.AppendLine();
        }
        return sb.ToString();
    }
}
