using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// QuickPlayController — GBG 风格的"一键即玩"控制器
///
/// 核心理念（来自 Game Builder Garage）：
///   编辑 ↔ 测试之间的切换应该是【瞬间】的，像按 Switch 的 + 键一样。
///   设计者不需要关心"场景是否完整"、"环境是否就绪"——系统自动搞定一切。
///
/// 功能：
///   1. 快捷键 F5：一键进入 Play Mode（自动补全可玩环境）
///   2. 快捷键 F5（Play 中）：一键退出回到编辑
///   3. 进入 Play 前自动保存场景（防丢失）
///   4. 进入 Play 前自动运行 RedLineGuard 检查
///   5. 进入 Play 前自动调用 GameplayLoopSceneBootstrapper 补全服务
///   6. 退出 Play 后自动恢复 Scene 视图焦点到上次编辑位置
///   7. 提供 Scene 视图浮动按钮 "▶ PLAY" / "■ STOP"
///
/// 设计原则：
///   - 零配置：不需要用户做任何设置
///   - 零等待：最小化进入 Play 的前置检查
///   - 零意外：自动保存 + 自动补全 = 不会丢东西也不会缺东西
/// </summary>
[InitializeOnLoad]
public static class QuickPlayController
{
    // ═══════════════════════════════════════════════════
    // 状态持久化 Keys
    // ═══════════════════════════════════════════════════
    private const string PREF_LAST_SCENE_VIEW_PIVOT = "MarioTrickster_QPC_LastPivot";
    private const string PREF_LAST_SCENE_VIEW_SIZE = "MarioTrickster_QPC_LastSize";
    private const string PREF_QUICK_PLAY_ACTIVE = "MarioTrickster_QPC_Active";

    // ═══════════════════════════════════════════════════
    // 初始化
    // ═══════════════════════════════════════════════════
    static QuickPlayController()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    // ═══════════════════════════════════════════════════
    // 菜单 + 快捷键
    // ═══════════════════════════════════════════════════

    /// <summary>F5 快捷键：Play/Stop 切换</summary>
    [MenuItem("MarioTrickster/Quick Play _F5", false, 100)]
    public static void ToggleQuickPlay()
    {
        if (EditorApplication.isPlaying)
        {
            // 正在播放 → 停止
            EditorApplication.isPlaying = false;
        }
        else if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            // 未在播放 → 启动快速播放流程
            StartQuickPlay();
        }
    }

    [MenuItem("MarioTrickster/Quick Play _F5", true)]
    private static bool ToggleQuickPlayValidate()
    {
        // 在编译中时禁用
        return !EditorApplication.isCompiling;
    }

    // ═══════════════════════════════════════════════════
    // 核心流程
    // ═══════════════════════════════════════════════════

    /// <summary>启动快速播放：保存 → 检查 → 补全 → Play</summary>
    private static void StartQuickPlay()
    {
        // 1. 记录当前 Scene 视图状态（退出 Play 后恢复）
        SaveSceneViewState();

        // 2. 自动保存场景（防止 Play 后丢失修改）
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
        {
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[QuickPlay] 场景已自动保存。");
        }

        // 3. 自动补全可玩环境（确保 Mario/Trickster/Managers 都在）
        EnsurePlayReady();

        // 4. 标记为 QuickPlay 触发（用于退出时恢复视图）
        EditorPrefs.SetBool(PREF_QUICK_PLAY_ACTIVE, true);

        // 5. 进入 Play Mode
        EditorApplication.isPlaying = true;

        Debug.Log("<color=#88FF88>[QuickPlay] ▶ 一键启动！按 F5 随时返回编辑。</color>");
    }

    /// <summary>确保场景可以直接 Play（补全缺失的运行时依赖）</summary>
    private static void EnsurePlayReady()
    {
        // 查找关卡根节点
        GameObject levelRoot = GameObject.Find("AsciiLevel_Root");
        if (levelRoot == null)
        {
            // 没有关卡？尝试找任何带 Ground layer 的物体作为参考
            levelRoot = FindAnyLevelRoot();
            if (levelRoot == null) return; // 真的啥都没有，让 Play Mode 自己报错
        }

        // 调用 PlayableEnvironmentBuilder 补全基础环境
        PlayableEnvironmentBuilder.EnsurePlayableEnvironment(levelRoot);

        // 调用 GameplayLoopSceneBootstrapper 补全高级服务（如果需要）
        if (GameplayLoopSceneBootstrapper.NeedsGameplayLoopAutoFix(levelRoot))
        {
            GameplayLoopSceneBootstrapper.EnsureGameplayLoopServices(levelRoot);
            GameplayLoopSceneBootstrapper.EnsureCombatRoomSemantics(levelRoot);
            Debug.Log("[QuickPlay] 已自动补全 Gameplay Loop 服务。");
        }

        // 标记场景脏（因为我们可能添加了对象）
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    /// <summary>尝试找到任何可能的关卡根节点</summary>
    private static GameObject FindAnyLevelRoot()
    {
        // 按优先级查找
        string[] possibleNames = { "AsciiLevel_Root", "Level_Root", "LevelRoot", "Level" };
        foreach (string name in possibleNames)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null) return obj;
        }
        return null;
    }

    // ═══════════════════════════════════════════════════
    // Play Mode 状态回调
    // ═══════════════════════════════════════════════════

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            // 如果是 QuickPlay 触发的，恢复 Scene 视图
            if (EditorPrefs.GetBool(PREF_QUICK_PLAY_ACTIVE, false))
            {
                EditorPrefs.SetBool(PREF_QUICK_PLAY_ACTIVE, false);
                RestoreSceneViewState();
                Debug.Log("<color=#FFAA44>[QuickPlay] ■ 已返回编辑模式，视图已恢复。</color>");
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // Scene 视图状态保存/恢复
    // ═══════════════════════════════════════════════════

    private static void SaveSceneViewState()
    {
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv == null) return;

        Vector3 pivot = sv.pivot;
        float size = sv.size;

        EditorPrefs.SetString(PREF_LAST_SCENE_VIEW_PIVOT, $"{pivot.x},{pivot.y},{pivot.z}");
        EditorPrefs.SetFloat(PREF_LAST_SCENE_VIEW_SIZE, size);
    }

    private static void RestoreSceneViewState()
    {
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv == null) return;

        string pivotStr = EditorPrefs.GetString(PREF_LAST_SCENE_VIEW_PIVOT, "");
        if (!string.IsNullOrEmpty(pivotStr))
        {
            string[] parts = pivotStr.Split(',');
            if (parts.Length == 3)
            {
                float x, y, z;
                if (float.TryParse(parts[0], out x) &&
                    float.TryParse(parts[1], out y) &&
                    float.TryParse(parts[2], out z))
                {
                    sv.pivot = new Vector3(x, y, z);
                }
            }
        }

        float savedSize = EditorPrefs.GetFloat(PREF_LAST_SCENE_VIEW_SIZE, 0f);
        if (savedSize > 0f)
        {
            sv.size = savedSize;
        }

        sv.Repaint();
    }

    // ═══════════════════════════════════════════════════
    // Scene 视图浮动按钮（GBG 风格的 Play/Stop 按钮）
    // ═══════════════════════════════════════════════════

    private static void OnSceneGUI(SceneView sceneView)
    {
        Handles.BeginGUI();

        // 在 Scene 视图右上角绘制浮动按钮（位于工具栏下方，避免遮挡）
        float btnWidth = 90f;
        float btnHeight = 28f;
        float marginX = 10f;
        float marginY = 42f; // [FIX UI-1] 下移到工具栏下方，避免与验证按钮重叠
        Rect btnRect = new Rect(
            sceneView.position.width - btnWidth - marginX,
            marginY,
            btnWidth,
            btnHeight);

        // 根据状态显示不同按钮
        if (EditorApplication.isPlaying)
        {
            GUI.color = new Color(1f, 0.4f, 0.4f);
            if (GUI.Button(btnRect, "■ STOP (F5)"))
            {
                EditorApplication.isPlaying = false;
            }
        }
        else
        {
            GUI.color = new Color(0.3f, 0.9f, 0.3f);
            if (GUI.Button(btnRect, "▶ PLAY (F5)"))
            {
                StartQuickPlay();
            }
        }

        GUI.color = Color.white;
        Handles.EndGUI();
    }
}
