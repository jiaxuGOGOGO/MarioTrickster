using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// UIWorldSpaceLayerSetup — 自动化 Scene 视图隐藏方案（S150 修订版）。
///
/// 解决 GlobalGameUICanvas 在 Scene 视图中遮挡关卡元素导致策划无法点选的问题。
///
/// 策略（双保险）：
///   主方案：使用 SceneVisibilityManager.Hide() 在 Scene 视图中隐藏 Canvas GameObject。
///           这是 Unity 2019.1+ 官方 API，专门用于 Scene 视图可见性控制。
///           - 不影响 GameObject.activeSelf（FindObjectOfType 正常工作）
///           - 不影响运行时渲染（Play Mode 自动恢复）
///           - 不影响 Inspector 中的任何属性
///   后备：同时将 Canvas 及子物体设置到 UI_WorldSpace Layer 作为额外隔离层。
///
/// 触发时机：
///   - Editor 首次加载（InitializeOnLoad）
///   - 场景打开 / 场景保存
///   - 退出 Play Mode 回到 Edit Mode
///   - 关卡生成后（通过 HierarchyChanged 监听新创建的 Canvas）
///
/// [AI防坑警告] FindObjectOfType&lt;GlobalGameUICanvas&gt;() 在 PlayableEnvironmentBuilder、
/// GameplayLoopSceneBootstrapper、TestSceneBuilder 中用于检测是否已存在 Canvas。
/// 绝对不能用 SetActive(false)，否则会导致重复创建。本方案使用 SceneVisibilityManager
/// 隐藏，GameObject 始终保持 active。
/// </summary>
[InitializeOnLoad]
public static class UIWorldSpaceLayerSetup
{
    private const string LAYER_NAME = "UI_WorldSpace";
    private const string PREF_KEY_AUTO_HIDE = "MarioTrickster_AutoHideUIWorldSpace";

    static UIWorldSpaceLayerSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;

        // 首次加载时延迟执行一次（确保场景物体已就绪）
        EditorApplication.delayCall += () =>
        {
            ApplyEditModeHiding();
        };
    }

    /// <summary>是否启用自动隐藏（默认 true，可通过 Level Studio 面板关闭）。</summary>
    public static bool IsAutoHideEnabled
    {
        get => EditorPrefs.GetBool(PREF_KEY_AUTO_HIDE, true);
        set
        {
            EditorPrefs.SetBool(PREF_KEY_AUTO_HIDE, value);
            if (value && !EditorApplication.isPlaying)
                ApplyEditModeHiding();
            else if (!value)
                RestoreVisibility();
        }
    }

    // ─────────────────────────────────────────────────────
    // 事件回调
    // ─────────────────────────────────────────────────────

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += ApplyEditModeHiding;
    }

    private static void OnSceneSaved(UnityEngine.SceneManagement.Scene scene)
    {
        AssignLayerToAllCanvases();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.EnteredPlayMode:
                // Play Mode：恢复可见性，确保 Canvas 正常渲染
                RestoreVisibility();
                EditorApplication.delayCall += AssignLayerToAllCanvases;
                break;

            case PlayModeStateChange.EnteredEditMode:
                // 回到 Edit Mode：重新隐藏
                EditorApplication.delayCall += ApplyEditModeHiding;
                break;
        }
    }

    /// <summary>
    /// 监听 Hierarchy 变化，捕获关卡生成器新创建的 GlobalGameUICanvas。
    /// 使用节流机制避免每帧触发。
    /// </summary>
    private static double _lastHierarchyCheckTime;

    private static void OnHierarchyChanged()
    {
        if (EditorApplication.isPlaying) return;
        if (!IsAutoHideEnabled) return;

        // 节流：最多每 0.5 秒检查一次
        double now = EditorApplication.timeSinceStartup;
        if (now - _lastHierarchyCheckTime < 0.5) return;
        _lastHierarchyCheckTime = now;

        EditorApplication.delayCall += ApplyEditModeHiding;
    }

    // ─────────────────────────────────────────────────────
    // 核心逻辑
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// Edit Mode 下的完整隐藏流程：分配 Layer + SceneVisibilityManager 隐藏。
    /// </summary>
    private static void ApplyEditModeHiding()
    {
        if (EditorApplication.isPlaying) return;

        AssignLayerToAllCanvases();

        if (!IsAutoHideEnabled) return;

        GlobalGameUICanvas[] canvases = Object.FindObjectsOfType<GlobalGameUICanvas>(true);
        if (canvases.Length == 0) return;

        SceneVisibilityManager svm = SceneVisibilityManager.instance;
        foreach (GlobalGameUICanvas canvas in canvases)
        {
            if (canvas == null) continue;
            GameObject go = canvas.gameObject;

            // SceneVisibilityManager.Hide：Scene 视图不可见、不可选
            // includeDescendants = true：子物体一起隐藏
            if (!svm.IsHidden(go, false))
            {
                svm.Hide(go, true);
            }
        }
    }

    /// <summary>
    /// 恢复所有 Canvas 在 Scene 视图中的可见性。
    /// </summary>
    private static void RestoreVisibility()
    {
        GlobalGameUICanvas[] canvases = Object.FindObjectsOfType<GlobalGameUICanvas>(true);
        if (canvases.Length == 0) return;

        SceneVisibilityManager svm = SceneVisibilityManager.instance;
        foreach (GlobalGameUICanvas canvas in canvases)
        {
            if (canvas == null) continue;
            GameObject go = canvas.gameObject;

            if (svm.IsHidden(go, false))
            {
                svm.Show(go, true);
            }
        }
    }

    /// <summary>
    /// 将场景中所有 GlobalGameUICanvas 及其子物体的 Layer 设置为 UI_WorldSpace。
    /// </summary>
    private static void AssignLayerToAllCanvases()
    {
        int layer = LayerMask.NameToLayer(LAYER_NAME);
        if (layer < 0) return; // Layer 尚未注册，静默跳过

        GlobalGameUICanvas[] canvases = Object.FindObjectsOfType<GlobalGameUICanvas>(true);
        foreach (GlobalGameUICanvas canvas in canvases)
        {
            if (canvas == null) continue;
            SetLayerRecursive(canvas.gameObject, layer);
        }
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (go.layer != layer)
        {
            Undo.RecordObject(go, "Set UI_WorldSpace Layer");
            go.layer = layer;
        }

        for (int i = 0; i < go.transform.childCount; i++)
        {
            SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }
    }
}
