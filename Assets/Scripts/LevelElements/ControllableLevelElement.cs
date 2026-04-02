using UnityEngine;

/// <summary>
/// 可操控关卡元素桥接基类 - 连接关卡框架与Trickster操控系统
/// 
/// 设计理念（低耦合桥接）:
///   - 继承 ControllablePropBase 获得 Trickster 操控状态机
///   - 通过直接调用 LevelElementRegistry 静态方法实现注册/注销
///   - 使用 LevelElementData 轻量数据包装器（非MonoBehaviour）传递元数据
///   - 子类只需关注自己的具体行为，框架层面的注册/查询/重置全部自动处理
/// 
/// 使用方式:
///   新增可操控关卡元素时，继承此类并实现:
///     OnTelegraphStart() / OnTelegraphEnd() / OnActivate() / OnActiveEnd()
///   同时设置 elementCategory 和 elementTags 即可
/// 
/// 扩展指南（给未来AI/开发者）:
///   - 不需要Trickster操控 → 继承 LevelElementBase
///   - 需要Trickster操控   → 继承 ControllableLevelElement
///   两条路径互不干扰，删除任何一个子类不影响其他代码。
/// 
/// Session 15: 关卡设计系统框架新增
/// </summary>
public abstract class ControllableLevelElement : ControllablePropBase
{
    [Header("=== 关卡元素框架 ===")]
    [Tooltip("元素分类")]
    [SerializeField] protected ElementCategory elementCategory = ElementCategory.Trap;

    [Tooltip("元素特征标签")]
    [SerializeField] protected ElementTag elementTags = ElementTag.Controllable;

    [Tooltip("元素描述")]
    [SerializeField, TextArea(2, 4)] protected string elementDescription = "";

    // 注册状态
    private bool _isRegistered;

    protected override void Awake()
    {
        base.Awake();
        elementTags |= ElementTag.Controllable;
    }

    protected virtual void OnEnable()
    {
        if (!_isRegistered)
        {
            LevelElementRegistry.RegisterControllable(this, propName, elementCategory, elementTags, elementDescription);
            _isRegistered = true;
        }
    }

    protected virtual void OnDisable()
    {
        if (_isRegistered)
        {
            LevelElementRegistry.UnregisterControllable(this);
            _isRegistered = false;
        }
    }

    /// <summary>关卡重置时调用（子类可覆盖）</summary>
    public virtual void OnLevelReset()
    {
        ResetUses();
    }

    // ── 查询快捷方法 ─────────────────────────────────────

    public bool HasElementTag(ElementTag tag) => (elementTags & tag) != 0;
    public bool IsElementCategory(ElementCategory cat) => elementCategory == cat;
    public ElementCategory GetElementCategory() => elementCategory;
    public ElementTag GetElementTags() => elementTags;
}
