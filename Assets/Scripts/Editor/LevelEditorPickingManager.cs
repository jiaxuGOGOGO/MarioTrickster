using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 关卡编辑器拾取管理器 — 解决 S37 视碰分离架构下框选(marquee select)同时选中 Root 和 Visual 的问题
///
/// 核心机制 (v3 — Selection 后处理 + delayCall 安全赋值):
///   - 监听 Selection.selectionChanged 事件
///   - Root 模式(默认): 选中 Visual 时，延迟一帧替换为其父级 Root
///   - Visual 模式: 不做任何过滤，Visual 可被直接选中
///
/// 防崩溃四重保险:
///   1. _isProcessingSelection 防重入锁 — 防止 Selection.objects 赋值触发死循环
///   2. EditorApplication.delayCall 延迟赋值 — 绕过 GUI 重绘时序冲突，防 Inspector 闪烁报错
///   3. HashSet&lt;Object&gt; 天然去重 — 防止多个 Visual 指向同一 Root 导致重复
///   4. go.scene.IsValid() 过滤 — 防止拦截 Project 窗口中的预制体资产点击
///
/// 白名单识别规则（避免误伤）:
///   1. 必须在有效场景中（非 Project 窗口资产）
///   2. 自身带有 SpriteRenderer
///   3. 有父节点
///   4. 父级带有 Collider2D
///   5. 父级带有核心脚本，或父级是纯几何方块（无 MonoBehaviour + 单子节点）
///
/// Session 41: 新增 (v3)
/// </summary>
[InitializeOnLoad]
public static class LevelEditorPickingManager
{
    // ═══════════════════════════════════════════════════
    // 常量
    // ═══════════════════════════════════════════════════
    private const string PREF_KEY = "MarioTrickster_PickingMode";
    // 0 = Root 模式 (默认)
    // 1 = Visual 模式

    // 防重入锁（极度重要：防止修改 Selection 时触发死循环）
    private static bool _isProcessingSelection = false;

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
        // 1. 防重入拦截 + Visual 模式放行
        if (_isProcessingSelection || !IsRootMode)
            return;

        if (Selection.objects == null || Selection.objects.Length == 0)
            return;

        bool needRedirection = false;

        // 2. 使用 HashSet 天然去重，防止框选多个 Visual 导致重复添加同一个 Root
        HashSet<Object> newSelection = new HashSet<Object>();

        foreach (var obj in Selection.objects)
        {
            GameObject go = obj as GameObject;

            // 如果选中的是场景里的 Visual 节点，进行拦截和替换
            if (go != null && IsVisualNode(go))
            {
                newSelection.Add(go.transform.parent.gameObject);
                needRedirection = true;
            }
            else
            {
                // 不是 Visual 节点，保持原样
                newSelection.Add(obj);
            }
        }

        // 3. 只有真正发生"偷梁换柱"时，才去修改 Selection
        if (needRedirection)
        {
            var finalArray = newSelection.ToArray();

            // 4. 极其关键：延迟一帧赋值，绕过 Unity 底层的 GUI 绘制时序冲突
            EditorApplication.delayCall += () =>
            {
                _isProcessingSelection = true;   // 上锁
                Selection.objects = finalArray;   // 重新选中 Root
                _isProcessingSelection = false;   // 解锁
            };
        }
    }

    // ═══════════════════════════════════════════════════
    // 白名单识别
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 判断一个 GameObject 是否为需要拦截的 Visual 节点。
    /// </summary>
    private static bool IsVisualNode(GameObject go)
    {
        // 过滤 1: 不在当前场景里（Project 窗口中的预制体资产），放行
        if (!go.scene.IsValid())
            return false;

        // 过滤 2: 自身没有 SpriteRenderer，放行
        if (go.GetComponent<SpriteRenderer>() == null)
            return false;

        // 过滤 3: 没有父节点，放行
        Transform parent = go.transform.parent;
        if (parent == null)
            return false;

        // 过滤 4: 父级没有 Collider2D，放行
        if (parent.GetComponent<Collider2D>() == null)
            return false;

        // 过滤 5a: 父级有核心脚本 — 确认是受我们架构管辖的节点
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

        // 过滤 5b: 纯几何方块（Ground/Platform/Wall）— 由 CreateBlock 生成，
        // 没有任何自定义 MonoBehaviour，仅有 Transform + Collider2D。
        MonoBehaviour[] scripts = parent.GetComponents<MonoBehaviour>();
        if (scripts.Length == 0 && parent.childCount == 1)
            return true;

        return false;
    }
}
