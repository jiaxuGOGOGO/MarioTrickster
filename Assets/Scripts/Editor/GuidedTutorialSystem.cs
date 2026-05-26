using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// GuidedTutorialSystem — GBG 风格的引导式创作教程
///
/// 核心理念（来自 Game Builder Garage 的 Interactive Lessons）：
///   GBG 有 7 个引导课程，每个课程通过"做一步看一步"的方式教用户创作。
///   Alice 和 Bob 两个角色对话引导，每步都有即时反馈。
///
/// 在 MarioTrickster 中的映射：
///   提供内置的"创作引导"，新用户打开 Level Studio 时可以选择跟随引导，
///   一步步学会如何创建一个完整的关卡。
///
/// 引导课程：
///   Lesson 1: "第一个关卡" — 5 分钟创建一个可玩关卡
///   Lesson 2: "添加挑战" — 用地刺和间隙增加难度
///   Lesson 3: "Trickster 的恶作剧" — 添加可操控机关
///   Lesson 4: "完整对决" — 组合所有元素创建完整关卡
///
/// 设计原则：
///   - 每步操作不超过 3 次点击
///   - 每步完成后有即时视觉反馈
///   - 可以随时退出/跳过
///   - 不阻塞正常操作
/// </summary>
public class GuidedTutorialSystem : EditorWindow
{
    // ═══════════════════════════════════════════════════
    // 数据结构
    // ═══════════════════════════════════════════════════

    private class TutorialLesson
    {
        public string title;
        public string description;
        public TutorialStep[] steps;
        public string completionMessage;

        public TutorialLesson(string title, string desc, TutorialStep[] steps, string completion)
        {
            this.title = title;
            this.description = desc;
            this.steps = steps;
            this.completionMessage = completion;
        }
    }

    private class TutorialStep
    {
        public string instruction;      // 告诉用户做什么
        public string hint;             // 如果卡住了的提示
        public string asciiToAdd;       // 这一步要添加的 ASCII（如果有）
        public bool requiresPlayTest;   // 这一步需要按 F5 测试
        public string validation;       // 验证条件描述

        public TutorialStep(string instruction, string hint, string ascii = null,
            bool playTest = false, string validation = null)
        {
            this.instruction = instruction;
            this.hint = hint;
            this.asciiToAdd = ascii;
            this.requiresPlayTest = playTest;
            this.validation = validation;
        }
    }

    // ═══════════════════════════════════════════════════
    // 状态
    // ═══════════════════════════════════════════════════
    private List<TutorialLesson> lessons;
    private int currentLesson = 0;
    private int currentStep = 0;
    private bool isActive = false;
    private Vector2 scrollPos;

    // ═══════════════════════════════════════════════════
    // 菜单入口
    // ═══════════════════════════════════════════════════

    // [FIX UI-3] 快捷键从 %#g (Ctrl+Shift+G) 改为 %&g (Ctrl+Alt+G)
    // 避免与 Unity 内置 Create Empty Parent (Ctrl+Shift+G) 冲突
    [MenuItem("MarioTrickster/Guided Tutorial %&g", false, 12)]
    public static void ShowWindow()
    {
        var window = GetWindow<GuidedTutorialSystem>("Creation Guide");
        window.minSize = new Vector2(380, 320);
    }

    // ═══════════════════════════════════════════════════
    // 生命周期
    // ═══════════════════════════════════════════════════

    private void OnEnable()
    {
        lessons = BuildLessons();
    }

    // ═══════════════════════════════════════════════════
    // GUI
    // ═══════════════════════════════════════════════════

    private void OnGUI()
    {
        if (lessons == null) return;

        if (!isActive)
        {
            DrawLessonSelector();
        }
        else
        {
            DrawActiveLesson();
        }
    }

    private void DrawLessonSelector()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("🎓 创作引导", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("选择一个课程开始学习如何创作关卡：", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(8);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        for (int i = 0; i < lessons.Count; i++)
        {
            var lesson = lessons[i];
            EditorGUILayout.BeginVertical("helpBox");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"课程 {i + 1}: {lesson.title}", EditorStyles.boldLabel);
            GUI.color = new Color(0.3f, 0.9f, 0.3f);
            if (GUILayout.Button("开始", GUILayout.Width(50), GUILayout.Height(20)))
            {
                StartLesson(i);
            }
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(lesson.description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField($"步骤数: {lesson.steps.Length}", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawActiveLesson()
    {
        var lesson = lessons[currentLesson];
        var step = lesson.steps[currentStep];

        // 顶部：课程信息 + 进度
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"📖 {lesson.title}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        GUI.color = new Color(1f, 0.5f, 0.3f);
        if (GUILayout.Button("退出课程", EditorStyles.miniButton))
        {
            isActive = false;
            Repaint();
            return;
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        // 进度条
        float progress = (float)(currentStep + 1) / lesson.steps.Length;
        Rect progressRect = EditorGUILayout.GetControlRect(GUILayout.Height(8));
        EditorGUI.DrawRect(progressRect, new Color(0.2f, 0.2f, 0.2f));
        progressRect.width *= progress;
        EditorGUI.DrawRect(progressRect, new Color(0.3f, 0.8f, 0.3f));
        EditorGUILayout.LabelField($"步骤 {currentStep + 1} / {lesson.steps.Length}", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // 当前步骤指令
        EditorGUILayout.BeginVertical("helpBox");
        GUIStyle instructionStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            fontSize = 13,
            richText = true
        };
        EditorGUILayout.LabelField(step.instruction, instructionStyle);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // 提示
        if (!string.IsNullOrEmpty(step.hint))
        {
            EditorGUILayout.HelpBox($"💡 提示: {step.hint}", MessageType.Info);
        }

        // ASCII 预览（如果这步有模板）
        if (!string.IsNullOrEmpty(step.asciiToAdd))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("这一步的模板:", EditorStyles.miniLabel);
            EditorGUILayout.BeginVertical("box");
            GUIStyle monoStyle = new GUIStyle(EditorStyles.label)
            {
                font = Font.CreateDynamicFontFromOSFont("Courier New", 12),
                fontSize = 10,
                wordWrap = false
            };
            EditorGUILayout.LabelField(step.asciiToAdd, monoStyle);
            EditorGUILayout.EndVertical();

            // 一键应用按钮
            GUI.color = new Color(0.3f, 0.8f, 1f);
            if (GUILayout.Button("📋 一键应用此模板", GUILayout.Height(28)))
            {
                ApplyStepTemplate(step.asciiToAdd);
            }
            GUI.color = Color.white;
        }

        // Play 测试提示
        if (step.requiresPlayTest)
        {
            EditorGUILayout.Space(4);
            GUI.color = new Color(1f, 0.85f, 0.2f);
            EditorGUILayout.HelpBox("▶ 按 F5 测试你的关卡！", MessageType.Warning);
            GUI.color = Color.white;
        }

        EditorGUILayout.Space(8);

        // 导航按钮
        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginDisabledGroup(currentStep == 0);
        if (GUILayout.Button("◀ 上一步", GUILayout.Height(28)))
        {
            currentStep--;
            Repaint();
        }
        EditorGUI.EndDisabledGroup();

        GUILayout.FlexibleSpace();

        if (currentStep < lesson.steps.Length - 1)
        {
            GUI.color = new Color(0.3f, 0.9f, 0.3f);
            if (GUILayout.Button("下一步 ▶", GUILayout.Height(28)))
            {
                currentStep++;
                Repaint();
            }
            GUI.color = Color.white;
        }
        else
        {
            GUI.color = new Color(1f, 0.85f, 0.2f);
            if (GUILayout.Button("🎉 完成课程!", GUILayout.Height(28)))
            {
                CompleteLesson();
            }
            GUI.color = Color.white;
        }

        EditorGUILayout.EndHorizontal();
    }

    // ═══════════════════════════════════════════════════
    // 操作
    // ═══════════════════════════════════════════════════

    private void StartLesson(int index)
    {
        currentLesson = index;
        currentStep = 0;
        isActive = true;

        // 确保 Level Studio 打开
        TestConsoleWindow.ShowWindow();

        Repaint();
        Debug.Log($"[Tutorial] 开始课程: {lessons[index].title}");
    }

    private void CompleteLesson()
    {
        var lesson = lessons[currentLesson];
        EditorUtility.DisplayDialog("🎉 课程完成!",
            lesson.completionMessage, "太棒了!");
        isActive = false;
        Repaint();
    }

    private void ApplyStepTemplate(string template)
    {
        // 将模板设置到 Level Studio 的 Custom Template Editor
        var window = EditorWindow.GetWindow<TestConsoleWindow>("Level Studio", false);
        if (window != null)
        {
            var field = typeof(TestConsoleWindow).GetField("customAsciiTemplate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(window, template);
                window.Repaint();
                Debug.Log("[Tutorial] 模板已应用到 Custom Template Editor。");
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // 课程内容
    // ═══════════════════════════════════════════════════

    private List<TutorialLesson> BuildLessons()
    {
        var list = new List<TutorialLesson>();

        // ── Lesson 1: 第一个关卡 ──
        list.Add(new TutorialLesson(
            "第一个关卡",
            "5 分钟创建你的第一个可玩关卡！学会基本的地形和目标放置。",
            new TutorialStep[] {
                new TutorialStep(
                    "欢迎来到 MarioTrickster 关卡创作！\n\n" +
                    "在这个课程中，你将学会创建一个最基本的可玩关卡。\n" +
                    "整个过程只需要 5 分钟。",
                    "准备好了就点击「下一步」。"),
                new TutorialStep(
                    "首先，我们创建一个有地面的空间。\n\n" +
                    "点击下方「一键应用此模板」按钮，\n" +
                    "然后在 Level Studio 中点击 <b>Build</b> 按钮生成。",
                    "Build 按钮在 Custom Template Editor 区域的底部。",
                    "...........\n...........\n...........\n...........\n###########"),
                new TutorialStep(
                    "太好了！现在我们有了地面。\n\n" +
                    "接下来添加 Mario 的出生点(M)和终点(G)。\n" +
                    "点击「一键应用此模板」更新模板，然后重新 Build。",
                    "M = Mario 出生点，G = 终点门",
                    "...........\n...........\n...........\nM.........G\n###########"),
                new TutorialStep(
                    "完美！现在按 <b>F5</b> 测试你的关卡！\n\n" +
                    "Mario 应该能从左边跑到右边的终点。\n" +
                    "测试完按 F5 返回编辑。",
                    "如果场景中缺少角色，系统会自动补全。",
                    null, true),
                new TutorialStep(
                    "🎉 恭喜！你创建了第一个可玩关卡！\n\n" +
                    "虽然很简单，但这就是创作的起点。\n" +
                    "接下来的课程会教你添加更多挑战。",
                    "")
            },
            "你已经学会了创建基本关卡的方法！\n\n" +
            "记住：# = 地面，M = Mario，G = 终点。\n" +
            "按 F5 随时测试，按 F5 随时返回编辑。"
        ));

        // ── Lesson 2: 添加挑战 ──
        list.Add(new TutorialLesson(
            "添加挑战",
            "学会用地刺、间隙和平台增加关卡难度。",
            new TutorialStep[] {
                new TutorialStep(
                    "现在来让关卡更有趣！\n\n" +
                    "我们将添加地刺(^)和间隙来考验玩家。",
                    ""),
                new TutorialStep(
                    "地刺用 ^ 符号表示。\n" +
                    "玩家碰到地刺会受伤。\n\n" +
                    "应用下面的模板看看效果：",
                    "注意地刺之间留了安全的落脚点。",
                    "...............\n...............\n...............\nM..#.#.#.....G\n###^#^#^#######"),
                new TutorialStep(
                    "按 F5 测试！\n\n" +
                    "试试能不能跳过所有地刺到达终点。",
                    "按空格键跳跃。",
                    null, true),
                new TutorialStep(
                    "接下来试试间隙（空气中的缺口）。\n" +
                    "玩家需要跳过间隙，掉下去就失败。\n\n" +
                    "应用这个模板：",
                    "间隙最大 4 格宽（玩家跳跃极限）。",
                    "...............\n...............\n...............\nM....G.........\n###...###...###"),
                new TutorialStep(
                    "按 F5 测试间隙跳跃！\n\n" +
                    "如果间隙太宽跳不过去，可以缩小间隙（减少 . 的数量）。",
                    "物理验证会自动检查间隙是否可通过。",
                    null, true),
                new TutorialStep(
                    "最后，试试组合地刺和间隙：",
                    "这是一个中等难度的组合。",
                    "..................\n..................\n..................\nM.................G\n###..##^##..##^####"),
                new TutorialStep(
                    "🎉 你已经掌握了基本的挑战设计！\n\n" +
                    "记住：\n" +
                    "  ^ = 地刺\n" +
                    "  . = 空气（间隙）\n" +
                    "  = = 浮空平台",
                    "")
            },
            "你已经学会了添加挑战元素！\n\n" +
            "关键原则：\n" +
            "• 间隙不超过 4 格\n" +
            "• 地刺之间留安全岛\n" +
            "• 先简单后困难的节奏"
        ));

        // ── Lesson 3: Trickster 的恶作剧 ──
        list.Add(new TutorialLesson(
            "Trickster 的恶作剧",
            "学会添加 Trickster 可操控的机关，创造对抗玩法。",
            new TutorialStep[] {
                new TutorialStep(
                    "MarioTrickster 的核心是【对抗】！\n\n" +
                    "Trickster 可以附身到场景中的物体，\n" +
                    "操控它们来阻碍 Mario。\n\n" +
                    "让我们来添加可操控的机关。",
                    ""),
                new TutorialStep(
                    "移动平台用 > 符号表示。\n" +
                    "Trickster 可以附身到移动平台上控制它。\n\n" +
                    "应用这个模板：",
                    "T = Trickster 出生点",
                    "...........\n.....>.....\n...........\nM.........G\n##.......##\n###########"),
                new TutorialStep(
                    "按 F5 测试！\n\n" +
                    "注意移动平台的运动轨迹。\n" +
                    "在完整游戏中，Trickster 玩家可以控制它的时机。",
                    "",
                    null, true),
                new TutorialStep(
                    "传送带用 < 符号表示。\n" +
                    "它会把 Mario 推向某个方向。\n\n" +
                    "Trickster 可以附身后反转方向！",
                    "传送带 + 地刺 = 经典陷阱组合",
                    "...............\n...............\nT..............\nM<<<<<<<<<<<<^.\n###############"),
                new TutorialStep(
                    "最后，试试伪装墙(F)。\n" +
                    "看起来像普通墙壁，但 Trickster 可以让它消失！\n\n" +
                    "墙壁消失后，后面的陷阱就暴露了。",
                    "F = 伪装墙",
                    "...........\nT..........\n...........\nM....F....G\n#####F^^###\n###########"),
                new TutorialStep(
                    "🎉 你已经学会了 Trickster 机关设计！\n\n" +
                    "核心思路：\n" +
                    "  给 Mario 一条看似安全的路线，\n" +
                    "  但 Trickster 可以通过操控机关来破坏它。",
                    "")
            },
            "你已经掌握了 Trickster 机关设计！\n\n" +
            "关键元素：\n" +
            "• > = 移动平台（可控时机）\n" +
            "• < = 传送带（可控方向）\n" +
            "• F = 伪装墙（可控消失）\n\n" +
            "好的对抗设计 = Mario 有机会 + Trickster 有手段"
        ));

        // ── Lesson 4: 完整对决 ──
        list.Add(new TutorialLesson(
            "完整对决关卡",
            "组合所有元素，创建一个完整的 Mario vs Trickster 对决关卡。",
            new TutorialStep[] {
                new TutorialStep(
                    "最终课程！\n\n" +
                    "我们将组合之前学到的所有元素，\n" +
                    "创建一个完整的对决关卡。\n\n" +
                    "一个好的对决关卡需要：\n" +
                    "• Mario 有明确的目标路线\n" +
                    "• Trickster 有至少 3 个附身点\n" +
                    "• 多条路线选择",
                    ""),
                new TutorialStep(
                    "应用这个完整关卡模板：\n\n" +
                    "这是一个包含多种机关的完整关卡。",
                    "仔细观察各元素的布局逻辑。",
                    "W.............W\nW.............W\nW.....o...o...W\nW...===...===.W\nW.............W\nWT............W\nW...>...>...G.W\nW.............W\nWM..^^^...^^^.W\nW#############W\nWWWWWWWWWWWWWWW"),
                new TutorialStep(
                    "Build 后按 F5 测试！\n\n" +
                    "观察：\n" +
                    "• Mario 需要避开地刺\n" +
                    "• 移动平台是通过的关键\n" +
                    "• Trickster 可以操控移动平台的时机\n" +
                    "• 金币(o)提供额外目标",
                    "",
                    null, true),
                new TutorialStep(
                    "试试用「配方面板」快速添加更多结构！\n\n" +
                    "在 Level Studio → 行为配方 中，\n" +
                    "选择一个配方点击「放置」。\n\n" +
                    "配方是预制好的复合结构，一键就能用。",
                    "试试「机关」分类中的配方。"),
                new TutorialStep(
                    "🎉 恭喜完成所有课程！\n\n" +
                    "你现在已经掌握了 MarioTrickster 关卡创作的核心技能。\n\n" +
                    "接下来可以：\n" +
                    "• 用「智能向导」(Ctrl+Shift+W) 生成参数化片段\n" +
                    "• 用「配方面板」快速拼装复合结构\n" +
                    "• 用 F6 查看元素连接关系\n" +
                    "• 用分享码与朋友交换关卡",
                    "")
            },
            "🎓 全部课程完成！\n\n" +
            "你已经是一个合格的关卡设计师了。\n\n" +
            "快捷键速查：\n" +
            "• F5 = 即时测试/返回\n" +
            "• F6 = 连接可视化\n" +
            "• F7 = 内嵌属性面板\n" +
            "• F8 = 创作工具栏\n" +
            "• Ctrl+Shift+W = 智能向导\n" +
            "• Ctrl+Alt+G = 创作引导"
        ));

        return list;
    }
}
