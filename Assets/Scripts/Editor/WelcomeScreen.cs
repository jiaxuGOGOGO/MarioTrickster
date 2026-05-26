using UnityEngine;
using UnityEditor;

/// <summary>
/// WelcomeScreen — GBG 风格的首次启动引导界面
///
/// 核心理念（来自 Game Builder Garage）：
///   GBG 首次启动时不是直接扔给用户一个空白画布，
///   而是友好地介绍工具并引导用户开始第一个创作。
///
/// 在 MarioTrickster 中的映射：
///   首次打开 Level Studio 时显示欢迎界面，提供：
///   1. 快速开始选项（新建空关卡 / 从模板开始 / 跟随教程）
///   2. 快捷键速查卡
///   3. "不再显示"选项
/// </summary>
[InitializeOnLoad]
public class WelcomeScreen : EditorWindow
{
    private const string PREF_SHOWN = "MarioTrickster_Welcome_Shown_v2";
    private const string PREF_DONT_SHOW = "MarioTrickster_Welcome_DontShow";

    static WelcomeScreen()
    {
        EditorApplication.delayCall += CheckFirstLaunch;
    }

    private static void CheckFirstLaunch()
    {
        // 只在首次或版本更新时显示
        if (EditorPrefs.GetBool(PREF_DONT_SHOW, false)) return;
        if (EditorPrefs.GetBool(PREF_SHOWN, false)) return;

        EditorPrefs.SetBool(PREF_SHOWN, true);
        ShowWindow();
    }

    [MenuItem("MarioTrickster/Welcome Screen", false, 999)]
    public static void ShowWindow()
    {
        var window = GetWindow<WelcomeScreen>(true, "Welcome to MarioTrickster", true);
        window.minSize = new Vector2(480, 420);
        window.maxSize = new Vector2(480, 420);
    }

    private void OnGUI()
    {
        // 标题
        EditorGUILayout.Space(16);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("MarioTrickster Level Studio", titleStyle);

        EditorGUILayout.Space(4);
        GUIStyle subtitleStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 12 };
        EditorGUILayout.LabelField("创作你的 Mario vs Trickster 对决关卡", subtitleStyle);

        EditorGUILayout.Space(20);

        // 快速开始选项
        EditorGUILayout.LabelField("快速开始", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // 选项卡片
        GUI.color = new Color(0.3f, 0.9f, 0.3f);
        if (GUILayout.Button("🎮  新建空关卡 — 从零开始创作", GUILayout.Height(36)))
        {
            Close();
            CreationFlowToolbar.IsEnabled = true;
            TestConsoleWindow.ShowWindow();
        }

        EditorGUILayout.Space(4);

        GUI.color = new Color(0.4f, 0.7f, 1f);
        if (GUILayout.Button("📦  从配方开始 — 选择预制结构快速搭建", GUILayout.Height(36)))
        {
            Close();
            TestConsoleWindow.ShowWindow();
        }

        EditorGUILayout.Space(4);

        GUI.color = new Color(1f, 0.85f, 0.3f);
        if (GUILayout.Button("✨  智能向导 — 用滑块参数化生成关卡片段", GUILayout.Height(36)))
        {
            Close();
            SmartSnippetWizard.ShowWindow();
        }

        EditorGUILayout.Space(4);

        GUI.color = new Color(0.9f, 0.5f, 0.9f);
        if (GUILayout.Button("🎓  跟随教程 — 5 分钟学会关卡创作", GUILayout.Height(36)))
        {
            Close();
            GuidedTutorialSystem.ShowWindow();
        }

        GUI.color = Color.white;

        EditorGUILayout.Space(16);

        // 快捷键速查
        EditorGUILayout.LabelField("快捷键速查", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginVertical("box");
        DrawShortcutRow("F5", "即时测试 / 返回编辑");
        DrawShortcutRow("F6", "元素连接可视化");
        DrawShortcutRow("F7", "内嵌属性面板");
        DrawShortcutRow("F8", "创作工具栏");
        DrawShortcutRow("Ctrl+T", "打开 Level Studio");
        DrawShortcutRow("Ctrl+Shift+W", "智能片段向导");
        DrawShortcutRow("Ctrl+Alt+G", "创作引导教程");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // 不再显示
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        bool dontShow = EditorPrefs.GetBool(PREF_DONT_SHOW, false);
        bool newDontShow = EditorGUILayout.ToggleLeft("不再自动显示此窗口", dontShow);
        if (newDontShow != dontShow)
            EditorPrefs.SetBool(PREF_DONT_SHOW, newDontShow);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawShortcutRow(string key, string description)
    {
        EditorGUILayout.BeginHorizontal();
        GUIStyle keyStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.4f, 0.8f, 1f) }
        };
        EditorGUILayout.LabelField(key, keyStyle, GUILayout.Width(100));
        EditorGUILayout.LabelField(description, EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }
}
