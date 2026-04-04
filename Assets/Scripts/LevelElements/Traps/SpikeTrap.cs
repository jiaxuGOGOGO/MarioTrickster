using UnityEngine;

/// <summary>
/// 地刺陷阱 - 关卡设计系统 · 陷阱类
/// 
/// 框架定位: ControllableLevelElement → 可被Trickster操控的关卡元素
/// 分类: Trap | 标签: Controllable, Damaging, Periodic, Resettable
/// 
/// 功能:
///   - 静态模式: 地刺始终伸出，持续造成伤害
///   - 周期模式: 地刺按固定间隔伸出/缩回，需要玩家把握时机通过
///   - Trickster操控: 预警→强制伸出/缩回→冷却（三阶段状态机由基类管理）
/// 
/// Session 17 更新:
///   - 使用 KnockbackHelper 统一击退方向计算
///   - 击退方向改为 Mario 移动方向的反方向后退，避免反复二次伤害
/// 
/// 扩展/删除指南:
///   - 删除此文件不影响任何其他脚本（Registry自动注销）
///   - 修改参数只需在Inspector中调整，无需改代码
///   - 新增地刺变体可继承此类或直接复制修改
/// 
/// Session 15: 关卡设计系统新增
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class SpikeTrap : ControllableLevelElement
{
    public enum SpikeMode { Static, Periodic }

    [Header("=== 地刺设置 ===")]
    [SerializeField] private SpikeMode mode = SpikeMode.Periodic;
    [SerializeField] private int damage = 1;

    [Header("=== 周期模式设置 ===")]
    [SerializeField] private float extendedDuration = 2f;
    [SerializeField] private float retractedDuration = 2f;
    [SerializeField] private float transitionSpeed = 8f;

    [Header("=== 视觉设置 ===")]
    [SerializeField] private float extendedY = 0f;
    [SerializeField] private float retractedY = -0.8f;

    [Header("=== 击退 (Session 17 修正) ===")]
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float knockbackUpForce = 2f;

    // 组件
    private BoxCollider2D boxCollider;

    // 自身周期状态（独立于Trickster操控状态机）
    private bool isExtended = true;
    private float cycleTimer;
    private float targetY;
    private Vector3 initialLocalPos;

    // Trickster操控覆盖
    private bool tricksterOverride;
    private bool tricksterForceExtended;

    public bool IsExtended => isExtended;

    protected override void Awake()
    {
        // 设置框架元数据
        propName = "地刺陷阱";
        elementCategory = ElementCategory.Trap;
        elementTags = ElementTag.Controllable | ElementTag.Damaging | ElementTag.Periodic | ElementTag.Resettable;
        elementDescription = mode == SpikeMode.Static ? "静态地刺，持续伤害" : "周期伸缩地刺，需把握时机通过";

        base.Awake();

        boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = true;
        initialLocalPos = transform.localPosition;

        if (mode == SpikeMode.Static)
        {
            isExtended = true;
            targetY = extendedY;
        }
        else
        {
            cycleTimer = extendedDuration;
            targetY = extendedY;
        }
    }

    protected override void Update()
    {
        base.Update(); // ControllablePropBase 状态机

        // 自身周期逻辑（与Trickster操控独立）
        if (tricksterOverride)
        {
            isExtended = tricksterForceExtended;
            targetY = isExtended ? extendedY : retractedY;
        }
        else if (mode == SpikeMode.Periodic)
        {
            cycleTimer -= Time.deltaTime;
            if (cycleTimer <= 0f)
            {
                isExtended = !isExtended;
                cycleTimer = isExtended ? extendedDuration : retractedDuration;
                targetY = isExtended ? extendedY : retractedY;
            }
        }

        // 平滑移动
        Vector3 pos = transform.localPosition;
        pos.y = Mathf.MoveTowards(pos.y, targetY, transitionSpeed * Time.deltaTime);
        transform.localPosition = pos;

        // 碰撞器状态
        boxCollider.enabled = isExtended || Mathf.Abs(transform.localPosition.y - retractedY) > 0.1f;
    }

    private void OnTriggerEnter2D(Collider2D other) { if (isExtended) TryDamage(other); }
    private void OnTriggerStay2D(Collider2D other) { if (isExtended) TryDamage(other); }

    private void TryDamage(Collider2D other)
    {
        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health != null && !health.IsInvincible)
        {
            health.TakeDamage(damage);
            Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                // Session 17: 使用 KnockbackHelper 统一击退逻辑
                Vector2 knockback = KnockbackHelper.CalcSafeKnockback(
                    other.transform, transform, rb, knockbackForce, knockbackUpForce);
                rb.velocity = Vector2.zero;
                rb.AddForce(knockback, ForceMode2D.Impulse);

                KnockbackHelper.NotifyKnockbackStun(other);
            }
        }
    }

    // ── ControllablePropBase 四阶段实现 ──────────────────

    protected override void OnTelegraphStart()
    {
        // 基类已处理闪烁+震动，此处可加音效
    }

    protected override void OnTelegraphEnd() { }

    protected override void OnActivate(Vector2 direction)
    {
        tricksterOverride = true;
        tricksterForceExtended = direction.y >= 0;
    }

    protected override void OnActiveEnd()
    {
        tricksterOverride = false;
    }

    // ── 关卡重置 ─────────────────────────────────────────

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        isExtended = (mode == SpikeMode.Static);
        cycleTimer = extendedDuration;
        targetY = isExtended ? extendedY : retractedY;
        tricksterOverride = false;
        transform.localPosition = initialLocalPos;
    }

    // ── Gizmo ────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = isExtended ? Color.red : Color.gray;
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        Vector3 size = col != null ? (Vector3)col.size : new Vector3(1, 0.3f, 0);
        Gizmos.DrawWireCube(transform.position, size);
    }
}
