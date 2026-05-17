using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// UIWorldSpaceLayerSetup — 自动化 Layer 隔离方案。
///
/// S150: 解决 GlobalGameUICanvas 在 Scene 视图中遮挡关卡元素导致策划无法点选的问题。
///
/// 三项自动化：
///   1. 场景打开/保存时，自动将 GlobalGameUICanvas 及其所有子物体设置到 UI_WorldSpace Layer。
///   2. 进入 Edit Mode 时，自动在 Scene 视图中隐藏 UI_WorldSpace Layer（SceneView.SetSceneViewFilteringForLayers 不可用时回退到 visibleLayers）。
///   3. 进入 Play Mode 时，自动恢复 Scene 视图中 UI_WorldSpace Layer 的可见性。
///
/// 安全性：
///   - GlobalGameUICanvas 使用 ScreenSpaceOverlay 渲染模式，不受 Camera.cullingMask 影响，
///     因此改 Layer 不会影响运行时 UI 的显示。
///   - 不修改任何 Camera 的 cullingMask，不影响物理射线检测（UI 不参与物理）。
///   - 只在 Editor 中生效，不会打包进 Build。
/// </summary>
[InitializeOnLoad]
public static class UIWorldSpaceLayerSetup
{
    private const string LAYER_NAME = "UI_WorldSpace";
    private const string PREF_KEY_AUTO_HIDE = "MarioTrickster_AutoHideUIWorldSpace";

    static UIWorldSpaceLayerSetup()
    {
        // 订阅场景事件
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        // 首次加载时执行一次
        EditorApplication.delayCall += () =>
        {
            AssignLayerToAllCanvases();
            if (!EditorApplication.isPlaying)
            {
                HideLayerInSceneView();
            }
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
                HideLayerInSceneView();
            else if (!value)
                ShowLayerInSceneView();
        }
    }

    // ─────────────────────────────────────────────────────
    // 事件回调
    // ─────────────────────────────────────────────────────

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        // 延迟一帧确保场景物体已完全加载
        EditorApplication.delayCall += () =>
        {
            AssignLayerToAllCanvases();
            if (!EditorApplication.isPlaying && IsAutoHideEnabled)
                HideLayerInSceneView();
        };
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
                // Play Mode：恢复可见性（方便在 Scene 视图中调试 UI）
                ShowLayerInSceneView();
                // 运行时也确保 Layer 正确
                EditorApplication.delayCall += AssignLayerToAllCanvases;
                break;

            case PlayModeStateChange.EnteredEditMode:
                // 回到 Edit Mode：重新隐藏
                EditorApplication.delayCall += () =>
                {
                    AssignLayerToAllCanvases();
                    if (IsAutoHideEnabled)
                        HideLayerInSceneView();
                };
                break;
        }
    }

    // ─────────────────────────────────────────────────────
    // 核心逻辑
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// 将场景中所有 GlobalGameUICanvas 及其子物体的 Layer 设置为 UI_WorldSpace。
    /// </summary>
    private static void AssignLayerToAllCanvases()
    {
        int layer = LayerMask.NameToLayer(LAYER_NAME);
        if (layer < 0)
        {
            // Layer 尚未注册（TagManager.asset 未同步），静默跳过
            return;
        }

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

    /// <summary>在所有 Scene 视图中隐藏 UI_WorldSpace Layer。</summary>
    private static void HideLayerInSceneView()
    {
        int layer = LayerMask.NameToLayer(LAYER_NAME);
        if (layer < 0) return;

        int layerMask = 1 << layer;

        // Tools.visibleLayers 是全局 Scene 视图可见层掩码，兼容 Unity 2021+
        Tools.visibleLayers &= ~layerMask;
        SceneView.RepaintAll();
    }

    /// <summary>在所有 Scene 视图中恢复 UI_WorldSpace Layer 的可见性。</summary>
    private static void ShowLayerInSceneView()
    {
        int layer = LayerMask.NameToLayer(LAYER_NAME);
        if (layer < 0) return;

        int layerMask = 1 << layer;
        Tools.visibleLayers |= layerMask;
        SceneView.RepaintAll();
    }
}
