using UnityEngine;
using UnityEditor;

/// <summary>
/// 关卡编辑器拾取管理器 — 解决 S37 视碰分离架构下框选(marquee select)同时选中 Root 和 Visual 的问题
///
/// 核心机制:
///   - 监听 Selection.selectionChanged 事件，在选中后自动过滤
///   - Root 模式(默认): 框选/点选到 Visual 时，自动替换为其父级 Root
///   - Visual 模式: 不做任何过滤，Visual 可被直接选中
///   - 使用 [InitializeOnLoad] 实现自愈：每次编译自动注册事件
///   - 使用 EditorPrefs 持久化当前模式，跨 Session 保持一致
///
/// 为什么不用 SceneVisibilityManager.DisablePicking:
///   Root 节点只有 BoxCollider2D 没有 Renderer，当 Visual 被 DisablePicking 后，
///   框选的矩形检测找不到任何可拾取的 Renderer，导致整个物体框选不到。
///   因此改用 Selection 后处理：让 Visual 保持可拾取，选中后再替换为 Root。
///
/// 白名单识别规则（避免误伤）:
///   只过滤满足以下条件的 Visual 子节点:
///   1. 名称为 "Visual"
///   2. 自身带有 SpriteRenderer
///   3. 父级带有 Collider2D
///   4. 父级带有核心脚本，或父级是纯几何方块（无 MonoBehaviour + 单子节点）
///
/// Session 41: 新增 (v2 — Selection 后处理策略)
/// </summary>
[InitializeOnLoad]
public static class LevelEditorPickingManager
{
    // ═══════════════════════════════════════════════════
    // 常量
    // ═══════════════════════════════════════════════════
    private const string PREF_KEY = "MarioTrickster_PickingMode";
    // 0 = Root 模式 (默认, 框选 Visual 自动替换为 Root)
    // 1 = Visual 模式 (不做过滤)

    // 防重入标记 — 避免 SetSelection 触发 selectionChanged 导致无限递归
    private static bool _isProcessing;

    // ═══════════════════════════════════════════════════
    // 静态构造 — [InitializeOnLoad] 入口
    // ═══════════════════════════════════════════════════
    static LevelEditorPickingManager()
    {
        Selection.selectionChanged += OnSelectionChanged;
    }

    // ═══════════════════════════════════════════════════
    // 公共 API
    // ═══════════════════════════════════════════════════

    /// <summary>当前是否为 Root 模式（框选 Visual 自动替换为 Root）</summary>
    public static bool IsRootMode
    {
        get { return EditorPrefs.GetInt(PREF_KEY, 0) == 0; }
    }

    /// <summary>设置拾取模式</summary>
    /// <param name="rootMode">true = Root 模式, false = Visual 模式</param>
    public static void SetMode(bool rootMode)
    {
        EditorPrefs.SetInt(PREF_KEY, rootMode ? 0 : 1);
    }

    /// <summary>
    /// 保留公共 SyncState 接口以兼容已注入的钩子调用（AsciiLevelGenerator 事件等）。
    /// Selection 后处理策略下此方法为空操作。
    /// </summary>
    public static void SyncState()
    {
        // Selection 后处理策略不需要预扫描，保留空方法避免编译错误
    }

    // ═══════════════════════════════════════════════════
    // Selection 后处理
    // ═══════════════════════════════════════════════════

    private static void OnSelectionChanged()
    {
        // Visual 模式下不做任何过滤
        if (!IsRootMode)
            return;

        // 防重入
        if (_isProcessing)
            return;

        // PlayMode 下不干预
        if (EditorApplication.isPlaying)
            return;

        // Prefab Mode 下放行
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
            return;

        GameObject[] selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
            return;

        bool changed = false;
        GameObject[] filtered = new GameObject[selected.Length];

        for (int i = 0; i < selected.Length; i++)
        {
            GameObject go = selected[i];
            Transform parent = go.transform.parent;

            // 检查是否为需要过滤的 Visual 节点
            if (parent != null && IsTargetVisualNode(go.transform, parent))
            {
                // 替换为父级 Root
                filtered[i] = parent.gameObject;
                changed = true;
            }
            else
            {
                filtered[i] = go;
            }
        }

        if (changed)
        {
            // 去重：多个 Visual 可能指向同一个 Root
            var uniqueSet = new System.Collections.Generic.HashSet<GameObject>(filtered);
            var uniqueArray = new GameObject[uniqueSet.Count];
            uniqueSet.CopyTo(uniqueArray);

            _isProcessing = true;
            Selection.objects = uniqueArray;
            _isProcessing = false;
        }
    }

    // ═══════════════════════════════════════════════════
    // 白名单识别
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 白名单识别：判断一个子节点是否为需要过滤的 Visual 节点。
    /// 条件：
    ///   1. 名称为 "Visual"
    ///   2. 自身带有 SpriteRenderer
    ///   3. 父级带有 Collider2D
    ///   4. 父级带有核心脚本，或父级是纯几何方块
    /// </summary>
    private static bool IsTargetVisualNode(Transform child, Transform parent)
    {
        // 条件 1: 名称必须是 "Visual"
        if (child.name != "Visual")
            return false;

        // 条件 2: 自身必须有 SpriteRenderer
        if (child.GetComponent<SpriteRenderer>() == null)
            return false;

        // 条件 3: 父级必须有 Collider2D
        if (parent.GetComponent<Collider2D>() == null)
            return false;

        // 条件 4a: 父级有核心脚本
        if (parent.GetComponent<LevelElementBase>() != null) return true;
        if (parent.GetComponent<ControllablePropBase>() != null) return true;
        if (parent.GetComponent<MarioController>() != null) return true;
        if (parent.GetComponent<TricksterController>() != null) return true;
        if (parent.GetComponent<SimpleEnemy>() != null) return true;
        if (parent.GetComponent<Breakable>() != null) return true;
        if (parent.GetComponent<Collectible>() != null) return true;
        if (parent.GetComponent<DamageDealer>() != null) return true;
        if (parent.GetComponent<GoalZone>() != null) return true;
        if (parent.GetComponent<KillZone>() != null) return true;
        if (parent.GetComponent<MovingPlatform>() != null) return true;

        // 条件 4b: 纯几何方块（Ground/Platform/Wall）— 由 CreateBlock 生成，
        // 没有任何自定义 MonoBehaviour，仅有 Transform + Collider2D。
        MonoBehaviour[] scripts = parent.GetComponents<MonoBehaviour>();
        if (scripts.Length == 0 && parent.childCount == 1)
            return true;

        return false;
    }
}
