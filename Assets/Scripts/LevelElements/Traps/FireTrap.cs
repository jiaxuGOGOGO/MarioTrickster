using UnityEngine;

/// <summary>
/// 火焰陷阱 - 关卡设计系统 · 陷阱类
/// 
/// 框架定位: ControllableLevelElement
/// 分类: Trap | 标签: Controllable, Damaging, Periodic
/// 
/// 功能:
///   - 周期性喷射火焰柱，火焰有预热→喷射→冷却三个阶段
///   - 火焰柱有方向性（上/下/左/右），可配置喷射距离
///   - Trickster操控: 强制喷火或强制熄灭
/// 
/// Session 17 更新:
///   - 修复击退方向：改为 Mario 移动方向的反方向后退一小段距离
///     避免被火焰柱击飞到天上或反复二次伤害
///   - 击退后通知控制器进入 knockback stun
/// 
/// 扩展/删除指南: 删除此文件不影响其他脚本
/// Session 15: 关卡设计系统新增
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class FireTrap : ControllableLevelElement
{
    [Header("=== 火焰设置 ===")]
    [SerializeField] private int damage = 1;
    [SerializeField] private Vector2 fireDirection = Vector2.up;
    [SerializeField] private float fireLength = 2f;

    [Header("=== 周期设置 ===")]
    [SerializeField] private float warmupDuration = 0.5f;
    [SerializeField] private float fireDuration = 1.5f;
    [SerializeField] private float coolOffDuration = 2f;

    [Header("=== 击退 (Session 17 修正) ===")]
    [Tooltip("击退水平力度 — 向 Mario 移动方向的反方向推")]
    [SerializeField] private float knockbackForce = 6f;
    [Tooltip("击退垂直力度 — 轻微向上，避免卡地面")]
    [SerializeField] private float knockbackUpForce = 2f;

    // 组件
    private BoxCollider2D fireCollider;
    private SpriteRenderer sr;

    // 自身周期状态
    private enum FireState { Idle, Warmup, Firing, CoolOff }
    private FireState fireState = FireState.Idle;
    private float fireTimer;
    private float damageCooldown;
    private Vector3 initialLocalPos;

    // Trickster覆盖
    private bool tricksterOverride;
    private bool tricksterForceFire;

    protected override void Awake()
    {
        propName = "火焰陷阱";
        elementCategory = ElementCategory.Trap;
        elementTags = ElementTag.Controllable | ElementTag.Damaging | ElementTag.Periodic;
        elementDescription = "周期性喷射火焰柱";

        base.Awake();

        fireCollider = GetComponent<BoxCollider2D>();
        fireCollider.isTrigger = true;
        fireCollider.enabled = false;
        // S37: 视碰分离 — SpriteRenderer 可能在子物体 Visual 上
        sr = GetComponentInChildren<SpriteRenderer>();
        initialLocalPos = transform.localPosition;

        // 设置碰撞器形状匹配火焰方向
        Vector2 dir = fireDirection.normalized;
        fireCollider.size = new Vector2(
            Mathf.Abs(dir.x) > 0.5f ? fireLength : 0.5f,
            Mathf.Abs(dir.y) > 0.5f ? fireLength : 0.5f
        );
        fireCollider.offset = dir * fireLength * 0.5f;

        fireTimer = coolOffDuration; // 从冷却开始
        fireState = FireState.CoolOff;
    }

    protected override void Update()
    {
        base.Update();

        if (tricksterOverride)
        {
            SetFiring(tricksterForceFire);
        }
        else if (currentState == PropControlState.Idle)
        {
            UpdateFireCycle();
        }
        else
        {
            SetFiring(false);
        }

        // 视觉反馈
        if (sr != null)
        {
            switch (fireState)
            {
                case FireState.Warmup:
                    sr.color = new Color(1f, 0.6f, 0f, 0.5f + Mathf.Sin(Time.time * 15f) * 0.3f);
                    break;
                case FireState.Firing:
                    sr.color = new Color(1f, 0.3f, 0f, 1f);
                    break;
                default:
                    sr.color = new Color(0.4f, 0.2f, 0.1f, 0.6f);
                    break;
            }
        }

        if (damageCooldown > 0) damageCooldown -= Time.deltaTime;
    }

    private void UpdateFireCycle()
    {
        fireTimer -= Time.deltaTime;
        if (fireTimer <= 0f)
        {
            switch (fireState)
            {
                case FireState.CoolOff:
                    fireState = FireState.Warmup;
                    fireTimer = warmupDuration;
                    break;
                case FireState.Warmup:
                    fireState = FireState.Firing;
                    fireTimer = fireDuration;
                    fireCollider.enabled = true;
                    break;
                case FireState.Firing:
                    fireState = FireState.CoolOff;
                    fireTimer = coolOffDuration;
                    fireCollider.enabled = false;
                    break;
            }
        }
    }

    private void SetFiring(bool fire)
    {
        if (fire)
        {
            fireState = FireState.Firing;
            fireCollider.enabled = true;
        }
        else
        {
            fireState = FireState.CoolOff;
            fireCollider.enabled = false;
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (currentState != PropControlState.Idle && currentState != PropControlState.Active) return;
        if (fireState != FireState.Firing || damageCooldown > 0) return;
        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health != null && !health.IsInvincible)
        {
            health.TakeDamage(damage);
            damageCooldown = 0.5f;

            Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                // Session 17: 使用 SafeKnockback 统一击退逻辑
                Vector2 knockback = KnockbackHelper.CalcSafeKnockback(
                    other.transform, transform, rb, knockbackForce, knockbackUpForce);
                rb.velocity = Vector2.zero;
                rb.AddForce(knockback, ForceMode2D.Impulse);

                // 通知控制器进入 knockback stun
                KnockbackHelper.NotifyKnockbackStun(other);
            }
        }
    }

    // ── ControllablePropBase 五段生命周期实现 ─────────────

    protected override void OnTelegraphStart() { }
    protected override void OnTelegraphEnd() { }

    protected override void OnActivate(Vector2 direction)
    {
        tricksterOverride = true;
        tricksterForceFire = direction.y >= 0;
    }

    protected override void OnActiveEnd()
    {
        tricksterOverride = false;
        SetFiring(false);
    }

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        fireState = FireState.CoolOff;
        fireTimer = coolOffDuration;
        fireCollider.enabled = false;
        tricksterOverride = false;
        transform.localPosition = initialLocalPos;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = fireState == FireState.Firing ? new Color(1, 0.3f, 0, 0.6f) : new Color(1, 0.5f, 0, 0.2f);
        Vector3 end = transform.position + (Vector3)(fireDirection.normalized * fireLength);
        Gizmos.DrawLine(transform.position, end);
        Gizmos.DrawWireSphere(end, 0.15f);
    }
}
