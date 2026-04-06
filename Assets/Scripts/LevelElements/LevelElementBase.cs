using UnityEngine;

/// <summary>
/// 关卡元素统一抽象基类 - 低耦合关卡设计框架核心
/// 
/// 设计理念（低耦合 + 高内聚）:
///   - 所有关卡元素（陷阱、平台、隐藏通道、道具）统一继承此基类
///   - 基类只定义最小公共契约，不强制子类实现不需要的功能
///   - 通过 LevelElementRegistry 自动注册/注销，删除脚本不影响其他元素
///   - 新增元素只需: 1.继承此基类 2.实现抽象方法 3.挂载到GameObject
///   - 无需修改任何现有代码，Registry自动发现新元素
/// 
/// 分类标签系统:
///   ElementCategory 用于分类（陷阱/平台/通道/道具/敌人）
///   ElementTag 用于细分特征（可操控/有伤害/可伪装/周期性等）
///   两者组合实现灵活查询，避免硬编码的类型判断
/// 
/// 生命周期:
///   OnEnable  → 自动注册到 LevelElementRegistry
///   OnDisable → 自动从 LevelElementRegistry 注销
///   OnLevelReset() → 关卡重置时恢复初始状态
/// 
/// Session 15: 关卡设计系统框架新增
/// </summary>
// [SelectionBase] 确保 Scene 视图中点击/框选时优先选中 Root 母体，
// 而不是 Visual 子节点（因为 SpriteRenderer 在 Visual 上，Unity 默认选中有渲染组件的子物体）。
[SelectionBase]
public abstract class LevelElementBase : MonoBehaviour
{
    [Header("=== 关卡元素基础信息 ===")]
    [Tooltip("元素显示名称（用于调试和UI）")]
    [SerializeField] protected string elementName = "Unknown Element";

    [Tooltip("元素分类")]
    [SerializeField] protected ElementCategory category = ElementCategory.Misc;

    [Tooltip("元素特征标签（可多选）")]
    [SerializeField] protected ElementTag tags = ElementTag.None;

    [Tooltip("元素描述（用于编辑器和文档）")]
    [SerializeField, TextArea(2, 4)] protected string description = "";

    // ── 公共只读属性 ──────────────────────────────────────
    public string ElementName => elementName;
    public ElementCategory Category => category;
    public ElementTag Tags => tags;
    public string Description => description;

    /// <summary>元素的唯一实例ID（运行时自动分配）</summary>
    public int InstanceId { get; private set; }

    // ── 生命周期 ──────────────────────────────────────────

    protected virtual void OnEnable()
    {
        InstanceId = GetInstanceID();
        LevelElementRegistry.Register(this);
    }

    protected virtual void OnDisable()
    {
        LevelElementRegistry.Unregister(this);
    }

    // ── 关卡重置（子类可选覆盖）──────────────────────────

    /// <summary>
    /// 关卡重置时调用，子类覆盖以恢复初始状态。
    /// 默认实现为空，不强制子类实现。
    /// </summary>
    public virtual void OnLevelReset()
    {
        // 子类按需覆盖
    }

    // ── 标签查询快捷方法 ─────────────────────────────────

    /// <summary>检查是否包含指定标签</summary>
    public bool HasTag(ElementTag tag)
    {
        return (tags & tag) != 0;
    }

    /// <summary>检查是否属于指定分类</summary>
    public bool IsCategory(ElementCategory cat)
    {
        return category == cat;
    }

    // ── 调试 ─────────────────────────────────────────────

    protected virtual void OnDrawGizmosSelected()
    {
        // 基类默认Gizmo：显示元素名称标签
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f,
            $"[{category}] {elementName}");
        #endif
    }
}

/// <summary>
/// 关卡元素分类枚举
/// 用于粗粒度分类，方便批量查询和管理
/// </summary>
public enum ElementCategory
{
    Trap,           // 陷阱（地刺、摆锤、火焰等）
    Platform,       // 平台（弹跳、单向、崩塌等）
    HiddenPassage,  // 隐藏通道（地下通道、伪装墙壁等）
    Collectible,    // 收集物/道具（金币、钥匙、增益等）
    Enemy,          // 敌人（巡逻、弹跳等）
    Hazard,         // 环境危险（岩浆、深渊等）
    Checkpoint,     // 检查点
    Misc            // 其他
}

/// <summary>
/// 关卡元素特征标签（Flags枚举，可组合）
/// 用于细粒度特征标记，支持灵活查询
/// 
/// 示例: 一个可操控的周期性地刺 = Controllable | Damaging | Periodic
/// </summary>
[System.Flags]
public enum ElementTag
{
    None            = 0,
    Controllable    = 1 << 0,   // 可被Trickster操控
    Damaging        = 1 << 1,   // 会造成伤害
    Disguisable     = 1 << 2,   // 可被Trickster伪装
    Periodic        = 1 << 3,   // 周期性行为（伸缩、摆动等）
    OneShot         = 1 << 4,   // 一次性触发（崩塌、破坏等）
    Interactive     = 1 << 5,   // 玩家可交互（开关、传送等）
    MovingPart      = 1 << 6,   // 有移动部件
    Hidden          = 1 << 7,   // 默认隐藏
    Resettable      = 1 << 8,   // 可重置
    AffectsPhysics  = 1 << 9,   // 影响物理（弹跳、传送带等）
}
