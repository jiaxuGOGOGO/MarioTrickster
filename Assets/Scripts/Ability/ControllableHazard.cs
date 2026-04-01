using UnityEngine;

/// <summary>
/// 可操控危险道具 - Trickster 变身后可操控的陷阱
/// 参考: Crawl 的 Flame Trap / Floor Spike Trap
/// 
/// 操控效果:
///   - 预警阶段: 道具闪烁变红 + 震动，给 Mario 反应时间
///   - 激活阶段: 陷阱激活（尖刺伸出/火焰喷射/范围扩大）
///   - 冷却阶段: 陷阱回到休眠状态
/// 
/// 操控模式（可在 Inspector 中选择）:
///   - Spike:    地刺从地面伸出（垂直方向）
///   - Expand:   危险区域扩大（碰撞体变大）
///   - Burst:    爆发伤害（瞬间大范围）
///   - Directional: 朝 Trickster 指定方向发射（如火焰/飞镖）
/// 
/// 使用方式:
///   1. 在场景中创建陷阱 GameObject（Sprite + BoxCollider2D）
///   2. 挂载此脚本
///   3. 配置操控模式和参数
///   4. 陷阱默认处于休眠状态，只有 Trickster 操控时才会激活
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ControllableHazard : ControllablePropBase
{
    [Header("=== 陷阱配置 ===")]
    [SerializeField] private HazardControlMode hazardMode = HazardControlMode.Spike;

    [Tooltip("陷阱伤害值")]
    [SerializeField] private int damage = 1;

    [Tooltip("击退力度")]
    [SerializeField] private float knockbackForce = 5f;

    [Tooltip("击退上抛力度")]
    [SerializeField] private float knockbackUpForce = 3f;

    [Tooltip("伤害目标标签")]
    [SerializeField] private string[] targetTags = new string[] { "Player" };

    [Header("=== Spike 模式参数 ===")]
    [Tooltip("尖刺伸出距离")]
    [SerializeField] private float spikeExtendDistance = 1f;

    [Tooltip("尖刺伸出速度")]
    [SerializeField] private float spikeExtendSpeed = 15f;

    [Tooltip("尖刺伸出方向（本地坐标）")]
    [SerializeField] private Vector2 spikeDirection = Vector2.up;

    [Header("=== Expand 模式参数 ===")]
    [Tooltip("扩大后的碰撞体尺寸倍率")]
    [SerializeField] private float expandMultiplier = 2.5f;

    [Header("=== Burst 模式参数 ===")]
    [Tooltip("爆发范围半径")]
    [SerializeField] private float burstRadius = 2f;

    [Header("=== Directional 模式参数 ===")]
    [Tooltip("投射物预制体（可选，如果为空则使用范围检测）")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("投射物速度")]
    [SerializeField] private float projectileSpeed = 8f;

    // 组件
    private BoxCollider2D boxCollider;

    // 原始状态（用于恢复）
    private Vector2 originalColliderSize;
    private Vector2 originalColliderOffset;
    private Vector3 originalPosition;
    private bool originalColliderEnabled;

    // 激活状态
    private bool isDamageActive;
    private Vector3 spikeTargetPosition;
    private float damageCooldownTimer;
    private const float DAMAGE_COOLDOWN = 0.5f; // 同一目标的伤害间隔

    protected override void Awake()
    {
        base.Awake();
        propName = "陷阱";

        boxCollider = GetComponent<BoxCollider2D>();
        originalColliderSize = boxCollider.size;
        originalColliderOffset = boxCollider.offset;
        originalPosition = transform.localPosition;

        // 陷阱默认休眠：碰撞体设为触发器但不造成伤害
        boxCollider.isTrigger = true;
        isDamageActive = false;
    }

    protected override void Update()
    {
        base.Update();

        // 伤害冷却
        if (damageCooldownTimer > 0)
        {
            damageCooldownTimer -= Time.deltaTime;
        }

        // Spike 模式：平滑移动到目标位置
        if (currentState == PropControlState.Active && hazardMode == HazardControlMode.Spike)
        {
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition, spikeTargetPosition,
                spikeExtendSpeed * Time.deltaTime
            );
        }
    }

    #region ControllablePropBase 实现

    protected override void OnTelegraphStart()
    {
        // 预警音效占位
        // AudioManager.Instance?.PlaySFX("hazard_warning");
    }

    protected override void OnTelegraphEnd()
    {
        // 预警结束
    }

    protected override void OnActivate(Vector2 direction)
    {
        isDamageActive = true;

        switch (hazardMode)
        {
            case HazardControlMode.Spike:
                ActivateSpike();
                break;
            case HazardControlMode.Expand:
                ActivateExpand();
                break;
            case HazardControlMode.Burst:
                ActivateBurst();
                break;
            case HazardControlMode.Directional:
                ActivateDirectional(direction);
                break;
        }
    }

    protected override void OnActiveEnd()
    {
        isDamageActive = false;

        switch (hazardMode)
        {
            case HazardControlMode.Spike:
                DeactivateSpike();
                break;
            case HazardControlMode.Expand:
                DeactivateExpand();
                break;
            case HazardControlMode.Burst:
                // Burst 是瞬间效果，不需要恢复
                break;
            case HazardControlMode.Directional:
                // 投射物自行管理
                break;
        }
    }

    #endregion

    #region Spike 模式

    private void ActivateSpike()
    {
        Vector2 dir = spikeDirection.normalized;
        spikeTargetPosition = originalPosition + new Vector3(dir.x, dir.y, 0f) * spikeExtendDistance;
    }

    private void DeactivateSpike()
    {
        // 缩回：平滑回到原位（在 Update 中处理）
        spikeTargetPosition = originalPosition;
        StartCoroutine(RetractSpike());
    }

    private System.Collections.IEnumerator RetractSpike()
    {
        while (Vector3.Distance(transform.localPosition, originalPosition) > 0.01f)
        {
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition, originalPosition,
                spikeExtendSpeed * 0.5f * Time.deltaTime // 缩回速度较慢
            );
            yield return null;
        }
        transform.localPosition = originalPosition;
    }

    #endregion

    #region Expand 模式

    private void ActivateExpand()
    {
        boxCollider.size = originalColliderSize * expandMultiplier;
    }

    private void DeactivateExpand()
    {
        boxCollider.size = originalColliderSize;
        boxCollider.offset = originalColliderOffset;
    }

    #endregion

    #region Burst 模式

    private void ActivateBurst()
    {
        // 范围检测，对范围内所有目标造成伤害
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, burstRadius);
        foreach (Collider2D hit in hits)
        {
            if (IsValidTarget(hit.gameObject))
            {
                ApplyDamage(hit.gameObject);
            }
        }
    }

    #endregion

    #region Directional 模式

    private void ActivateDirectional(Vector2 direction)
    {
        if (direction.magnitude < 0.1f)
        {
            direction = Vector2.right; // 默认朝右
        }

        if (projectilePrefab != null)
        {
            // 发射投射物
            GameObject proj = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
            Rigidbody2D projRb = proj.GetComponent<Rigidbody2D>();
            if (projRb != null)
            {
                projRb.velocity = direction.normalized * projectileSpeed;
            }
            // 自动销毁
            Destroy(proj, 5f);
        }
        else
        {
            // 没有投射物预制体时，使用射线检测
            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction.normalized, burstRadius);
            if (hit.collider != null && IsValidTarget(hit.collider.gameObject))
            {
                ApplyDamage(hit.collider.gameObject);
            }
        }
    }

    #endregion

    #region 伤害处理

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!isDamageActive) return;
        if (damageCooldownTimer > 0) return;

        if (IsValidTarget(other.gameObject))
        {
            ApplyDamage(other.gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isDamageActive) return;

        if (IsValidTarget(other.gameObject))
        {
            ApplyDamage(other.gameObject);
        }
    }

    private bool IsValidTarget(GameObject target)
    {
        foreach (string tag in targetTags)
        {
            if (target.CompareTag(tag)) return true;
        }
        // 也检查是否有 MarioController（以防标签未设置）
        if (target.GetComponent<MarioController>() != null) return true;
        return false;
    }

    private void ApplyDamage(GameObject target)
    {
        PlayerHealth health = target.GetComponent<PlayerHealth>();
        if (health == null || health.IsInvincible) return;

        health.TakeDamage(damage);
        damageCooldownTimer = DAMAGE_COOLDOWN;

        // 击退
        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb != null && knockbackForce > 0)
        {
            Vector2 knockDir = (target.transform.position - transform.position).normalized;
            targetRb.velocity = Vector2.zero;
            targetRb.AddForce(new Vector2(knockDir.x * knockbackForce, knockbackUpForce), ForceMode2D.Impulse);
        }
    }

    #endregion

    #region 调试可视化

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        switch (hazardMode)
        {
            case HazardControlMode.Spike:
                // 显示尖刺伸出方向和距离
                Gizmos.color = Color.red;
                Vector3 spikeEnd = transform.position + new Vector3(spikeDirection.x, spikeDirection.y, 0f).normalized * spikeExtendDistance;
                Gizmos.DrawLine(transform.position, spikeEnd);
                Gizmos.DrawWireSphere(spikeEnd, 0.1f);
                break;

            case HazardControlMode.Burst:
                // 显示爆发范围
                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                Gizmos.DrawWireSphere(transform.position, burstRadius);
                break;

            case HazardControlMode.Expand:
                // 显示扩大后的碰撞体
                BoxCollider2D col = GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                    Gizmos.DrawWireCube(transform.position, (Vector3)(col.size * expandMultiplier));
                }
                break;
        }
    }

    #endregion
}

/// <summary>
/// 陷阱操控模式
/// </summary>
public enum HazardControlMode
{
    Spike,          // 地刺伸出
    Expand,         // 危险区域扩大
    Burst,          // 爆发伤害
    Directional     // 方向性发射
}
