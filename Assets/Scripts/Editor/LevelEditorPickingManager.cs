using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 关卡编辑器拾取管理器 — 解决 S37 视碰分离架构下框选(marquee select)同时选中 Root 和 Visual 的问题
///
/// 核心机制:
///   - 利用 Unity 内置的 SceneVisibilityManager.DisablePicking/EnablePicking 控制 Visual 子节点的可拾取性
///   - Root 模式(默认): Visual 子节点不可拾取，框选只选中 Root，适合批量移动/旋转
///   - Visual 模式: Visual 子节点可拾取，框选可选到 Visual，适合单独调整视觉大小
///   - 使用 [InitializeOnLoad] 实现自愈：每次编译/打开场景自动恢复锁定状态
///   - 使用 EditorPrefs 持久化当前模式，跨 Session 保持一致
///
/// 白名单识别规则（避免误伤）:
///   只锁定满足以下条件的 Visual 子节点:
///   1. 名称为 "Visual"
///   2. 自身带有 SpriteRenderer
///   3. 父级带有 Collider2D
///   4. 父级带有 [SelectionBase] 标记的核心脚本（LevelElementBase / ControllablePropBase /
///      MarioController / TricksterController / SimpleEnemy / Breakable / Collectible /
///      DamageDealer / GoalZone / KillZone / MovingPlatform）
///
/// 性能设计:
///   - 不监听 Hierarchy 变化（避免批量生成时卡顿）
///   - 由 AsciiLevelGenerator / TestSceneBuilder 生成结束后主动调用 SyncState() 一次
///   - Ctrl+D 复制的物体自动继承 Picking 状态，无需干预
///
/// Prefab Mode 安全:
///   - 在预制体隔离编辑模式下自动放行，不锁定任何 Visual
///
/// Session 41: 新增
/// </summary>
[InitializeOnLoad]
public static class LevelEditorPickingManager
{
    // ═══════════════════════════════════════════════════
    // 常量
    // ═══════════════════════════════════════════════════
    private const string PREF_KEY = "MarioTrickster_PickingMode";
    // 0 = Root 模式 (默认, Visual 不可拾取)
    // 1 = Visual 模式 (Visual 可拾取)

    // ═══════════════════════════════════════════════════
    // 静态构造 — [InitializeOnLoad] 入口
    // ═══════════════════════════════════════════════════
    static LevelEditorPickingManager()
    {
        // 每次打开新场景，自动恢复锁定状态，无视 Git 本地差异
        EditorSceneManager.sceneOpened += OnSceneOpened;
        // 订阅 AsciiLevelGenerator 的生成/换肤完成事件（运行时→Editor 解耦桥梁）
        AsciiLevelGenerator.OnLevelGenerated += SyncState;
        // 编译完成后也同步一次（覆盖域重载场景）
        EditorApplication.delayCall += SyncState;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        // 延迟一帧执行，确保场景物体已完全加载
        EditorApplication.delayCall += SyncState;
    }

    // ═══════════════════════════════════════════════════
    // 公共 API
    // ═══════════════════════════════════════════════════

    /// <summary>当前是否为 Root 模式（Visual 不可拾取）</summary>
    public static bool IsRootMode
    {
        get { return EditorPrefs.GetInt(PREF_KEY, 0) == 0; }
    }

    /// <summary>设置拾取模式并立即同步状态</summary>
    /// <param name="rootMode">true = Root 模式, false = Visual 模式</param>
    public static void SetMode(bool rootMode)
    {
        EditorPrefs.SetInt(PREF_KEY, rootMode ? 0 : 1);
        SyncState();
    }

    /// <summary>
    /// 同步当前模式到场景中所有符合条件的 Visual 子节点。
    /// 由 AsciiLevelGenerator / TestSceneBuilder 生成结束后主动调用。
    /// 也在场景打开/编译完成时自动调用（自愈机制）。
    /// </summary>
    public static void SyncState()
    {
        // Prefab Mode 安全检查 — 预制体隔离编辑模式下放行所有 Picking
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            return;

        // PlayMode 下不干预（避免影响运行时调试）
        if (EditorApplication.isPlaying)
            return;

        bool rootMode = IsRootMode;
        var svm = SceneVisibilityManager.instance;

        // 遍历场景中所有根物体
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.isLoaded) return;

        GameObject[] rootObjects = scene.GetRootGameObjects();
        int processedCount = 0;

        foreach (GameObject rootObj in rootObjects)
        {
            // 递归处理所有子层级
            ProcessHierarchy(rootObj.transform, svm, rootMode, ref processedCount);
        }

        if (processedCount > 0)
        {
            string modeName = rootMode ? "Root" : "Visual";
            Debug.Log($"[PickingManager] Synced {processedCount} Visual nodes to {modeName} mode.");
        }
    }

    // ═══════════════════════════════════════════════════
    // 内部实现
    // ═══════════════════════════════════════════════════

    /// <summary>递归处理层级中的所有 Visual 子节点</summary>
    private static void ProcessHierarchy(Transform current, SceneVisibilityManager svm,
        bool rootMode, ref int processedCount)
    {
        // 检查当前节点的直接子节点是否为符合条件的 Visual
        for (int i = 0; i < current.childCount; i++)
        {
            Transform child = current.GetChild(i);

            if (IsTargetVisualNode(child, current))
            {
                if (rootMode)
                {
                    // Root 模式: 锁定 Visual，不含后代（Visual 本身就是叶子节点）
                    svm.DisablePicking(child.gameObject, true);
                }
                else
                {
                    // Visual 模式: 解锁 Visual
                    svm.EnablePicking(child.gameObject, true);
                }
                processedCount++;
            }
            else
            {
                // 非 Visual 节点，继续递归
                ProcessHierarchy(child, svm, rootMode, ref processedCount);
            }
        }
    }

    /// <summary>
    /// 白名单识别：判断一个子节点是否为需要管理的 Visual 节点。
    /// 条件：
    ///   1. 名称为 "Visual"
    ///   2. 自身带有 SpriteRenderer
    ///   3. 父级带有 Collider2D
    ///   4. 父级带有 [SelectionBase] 标记的核心脚本，
    ///      或父级是纯几何方块（无任何自定义 MonoBehaviour，仅有 Collider2D）
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

        // 条件 4a: 父级有 [SelectionBase] 标记的核心脚本
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
        // 结构特征：父级只有一个子节点（Visual），且无自定义脚本。
        MonoBehaviour[] scripts = parent.GetComponents<MonoBehaviour>();
        if (scripts.Length == 0 && parent.childCount == 1)
            return true;

        return false;
    }
}
