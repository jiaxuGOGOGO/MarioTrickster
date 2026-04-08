using UnityEngine;

/// <summary>
/// 摆锤绳索陷阱 - 关卡设计系统 · 陷阱类
/// 
/// 框架定位: ControllableLevelElement → 可被Trickster操控的关卡元素
/// 分类: Trap | 标签: Controllable, Damaging, Periodic, MovingPart
/// 
/// 功能:
///   - 像糖秋千一样周期性摆动的危险物（锤头/尖球）
///   - 绳索连接顶部锚点和底部锤头，用LineRenderer绘制
///   - 锤头碰到Mario造成伤害和击退
///   - Trickster操控: 改变摆动速度/方向，或强制停止
/// 
/// Session 17 更新:
///   - 使用 KnockbackHelper 统一击退方向计算
///   - 击退方向改为 Mario 移动方向的反方向后退，避免反复二次伤害
/// 
/// 扩展/删除指南:
///   - 删除此文件不影响任何其他脚本
///   - 可通过调整 ropeLength/maxAngle/swingSpeed 创建不同难度的摆锤
///   - 锤头子对象自动创建，无需手动配置
/// 
/// Session 15: 关卡设计系统新增
/// </summary>
public class PendulumTrap : ControllableLevelElement
{
    [Header("=== 摆锤设置 ===")]
    [SerializeField] private float ropeLength = 3f;
    [SerializeField] private float swingSpeed = 2f;
    [SerializeField] private float maxAngle = 60f;
    [SerializeField] private int damage = 1;

    [Header("=== 锤头设置 ===")]
    [SerializeField] private float hammerRadius = 0.4f;
    [SerializeField] private Color hammerColor = new Color(0.8f, 0.2f, 0.1f);

    [Header("=== 击退 (Session 17 修正) ===")]
    [SerializeField] private float knockbackForce = 8f;
    [SerializeField] private float knockbackUpForce = 3f;

    [Header("=== 绳索视觉 ===")]
    [SerializeField] private Color ropeColor = new Color(0.6f, 0.4f, 0.2f);
    [SerializeField] private float ropeWidth = 0.05f;

    // 组件
    private LineRenderer lineRenderer;
    private GameObject hammerObject;
    private CircleCollider2D hammerCollider;

    // 物理状态
    private float currentAngle;
    private float angularVelocity;
    private float damageCooldown;
    private const float DAMAGE_COOLDOWN_TIME = 0.5f;

    // S57: 静态共享绳索材质（避免每个 PendulumTrap 实例都 new Material）
    private static Material s_sharedRopeMaterial;

    // Trickster覆盖
    private bool tricksterOverride;
    private float tricksterSpeedMult = 1f;

    // 初始状态（用于重置）
    private float initialAngle;

    public float CurrentAngle => currentAngle;
    public Vector3 HammerPosition => hammerObject != null ? hammerObject.transform.position : transform.position;

    protected override void Awake()
    {
        propName = "摆锤陷阱";
        elementCategory = ElementCategory.Trap;
        elementTags = ElementTag.Controllable | ElementTag.Damaging | ElementTag.Periodic | ElementTag.MovingPart;
        elementDescription = "糖秋千式摆动危险物，需把握摆动节奏通过";

        base.Awake();

        initialAngle = maxAngle;
        currentAngle = maxAngle;

        SetupLineRenderer();
        SetupHammer();
    }

    private void SetupLineRenderer()
    {
        lineRenderer = gameObject.GetComponent<LineRenderer>();
        if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = ropeWidth;
        lineRenderer.endWidth = ropeWidth;
        // S57: 使用静态共享材质，避免每个实例都创建新 Material
        if (s_sharedRopeMaterial == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null)
            {
                s_sharedRopeMaterial = new Material(sh);
                s_sharedRopeMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
        }
        if (s_sharedRopeMaterial != null)
            lineRenderer.material = s_sharedRopeMaterial;
        lineRenderer.startColor = ropeColor;
        lineRenderer.endColor = ropeColor;
        lineRenderer.sortingOrder = 4;
    }

    private void SetupHammer()
    {
        Transform existing = transform.Find("PendulumHammer");
        hammerObject = existing != null ? existing.gameObject : new GameObject("PendulumHammer");
        hammerObject.transform.parent = transform;

        hammerCollider = hammerObject.GetComponent<CircleCollider2D>();
        if (hammerCollider == null) hammerCollider = hammerObject.AddComponent<CircleCollider2D>();
        hammerCollider.radius = hammerRadius;
        hammerCollider.isTrigger = true;

        SpriteRenderer sr = hammerObject.GetComponent<SpriteRenderer>();
        if (sr == null) sr = hammerObject.AddComponent<SpriteRenderer>();
        sr.color = hammerColor;
        sr.sortingOrder = 5;

        PendulumHammerTrigger trigger = hammerObject.GetComponent<PendulumHammerTrigger>();
        if (trigger == null) trigger = hammerObject.AddComponent<PendulumHammerTrigger>();
        trigger.Initialize(this);
    }

    private void FixedUpdate()
    {
        float gravity = 9.81f;
        float speedMult = tricksterOverride ? tricksterSpeedMult : 1f;

        float angularAccel = -(gravity / ropeLength) * Mathf.Sin(currentAngle * Mathf.Deg2Rad);
        angularVelocity += angularAccel * Time.fixedDeltaTime * speedMult;
        angularVelocity *= 0.999f; // 轻微阻尼
        currentAngle += angularVelocity * swingSpeed * Time.fixedDeltaTime * Mathf.Rad2Deg;
        currentAngle = Mathf.Clamp(currentAngle, -maxAngle, maxAngle);

        // 更新锤头位置
        if (hammerObject != null)
        {
            float rad = currentAngle * Mathf.Deg2Rad;
            hammerObject.transform.position = transform.position + new Vector3(
                Mathf.Sin(rad) * ropeLength, -Mathf.Cos(rad) * ropeLength, 0);
        }

        // 更新绳索
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, hammerObject.transform.position);
        }

        if (damageCooldown > 0) damageCooldown -= Time.fixedDeltaTime;
    }

    /// <summary>锤头碰撞回调（由PendulumHammerTrigger转发）</summary>
    public void OnHammerHit(Collider2D other)
    {
        if (damageCooldown > 0) return;
        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health != null && !health.IsInvincible)
        {
            health.TakeDamage(damage);
            damageCooldown = DAMAGE_COOLDOWN_TIME;
            Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                // Session 17: 使用 KnockbackHelper 统一击退逻辑
                // 以锤头位置作为伤害源
                Vector2 knockback = KnockbackHelper.CalcSafeKnockback(
                    other.transform, hammerObject.transform, rb, knockbackForce, knockbackUpForce);
                rb.velocity = Vector2.zero;
                rb.AddForce(knockback, ForceMode2D.Impulse);

                KnockbackHelper.NotifyKnockbackStun(other);
            }
        }
    }

    // ── ControllablePropBase 四阶段实现 ──────────────────

    protected override void OnTelegraphStart() { }
    protected override void OnTelegraphEnd() { }

    protected override void OnActivate(Vector2 direction)
    {
        tricksterOverride = true;
        if (direction.y < -0.5f) tricksterSpeedMult = 0.1f;       // 减速
        else if (direction.y > 0.5f) tricksterSpeedMult = 2.5f;   // 加速
        else
        {
            angularVelocity += direction.x * 3f; // 方向推力
            tricksterSpeedMult = 1.5f;
        }
    }

    protected override void OnActiveEnd()
    {
        tricksterOverride = false;
        tricksterSpeedMult = 1f;
    }

    // ── 关卡重置 ─────────────────────────────────────────

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        currentAngle = initialAngle;
        angularVelocity = 0f;
        tricksterOverride = false;
        tricksterSpeedMult = 1f;
    }

    // ── Gizmo ────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.15f);
        Gizmos.color = new Color(1, 0, 0, 0.2f);
        float lr = -maxAngle * Mathf.Deg2Rad, rr = maxAngle * Mathf.Deg2Rad;
        Vector3 le = transform.position + new Vector3(Mathf.Sin(lr) * ropeLength, -Mathf.Cos(lr) * ropeLength, 0);
        Vector3 re = transform.position + new Vector3(Mathf.Sin(rr) * ropeLength, -Mathf.Cos(rr) * ropeLength, 0);
        Gizmos.DrawLine(transform.position, le);
        Gizmos.DrawLine(transform.position, re);
        Gizmos.DrawWireSphere(le, hammerRadius);
        Gizmos.DrawWireSphere(re, hammerRadius);
    }
}

/// <summary>摆锤锤头碰撞转发器</summary>
[RequireComponent(typeof(CircleCollider2D))]
public class PendulumHammerTrigger : MonoBehaviour
{
    private PendulumTrap parentTrap;
    public void Initialize(PendulumTrap trap) { parentTrap = trap; }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (parentTrap != null) parentTrap.OnHammerHit(other);
    }
}
