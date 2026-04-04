using UnityEngine;

/// <summary>
/// 弹跳平台 - 关卡设计系统 · 平台类
/// 
/// 框架定位: ControllableLevelElement
/// 分类: Platform | 标签: Controllable, AffectsPhysics, Resettable
/// 
/// 功能:
///   - 玩家碰撞平台后，沿碰撞面法线方向弹射（反方向力）
///   - 从顶面（最长边）落下 → 向上弹射
///   - 从侧面碰撞 → 向侧面弹射
///   - 弹射力度可在 Inspector 中调整（bounceForce）
///   - 弹射时有视觉压缩/回弹动画 + 喜剧延迟
///   - Trickster操控: 改变弹射方向或力度
/// 
/// Session 19 优化（参考 Sonic 弹簧 + 2D 平台游戏最佳实践）:
///   - 核心改进：使用碰撞接触点法线（contact.normal）确定弹射方向
///     玩家从哪个面碰撞，就往该面的法线方向弹射（反方向力）
///   - 新增 bounceForceMultiplier 用于 Inspector 中微调弹力强度
///   - 新增 minBounceForce / maxBounceForce 限制弹射力范围
///   - 保留喜剧延迟（Comedy Delay）和 Trickster 操控
///   - 保留水平动量继承选项
///   - 直接设置 velocity 而非 AddForce，确保弹射效果一致
/// 
/// 扩展/删除指南: 删除此文件不影响其他脚本
/// Session 15: 关卡设计系统新增
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BouncyPlatform : ControllableLevelElement
{
    [Header("=== 弹跳设置 ===")]
    [Tooltip("基础弹射力度（沿碰撞面法线方向）")]
    [SerializeField] private float bounceForce = 18f;

    [Tooltip("弹射力度倍率（用于微调，1.0=默认）")]
    [SerializeField] private float bounceForceMultiplier = 1f;

    [Tooltip("最小弹射力（防止力度过小无感）")]
    [SerializeField] private float minBounceForce = 8f;

    [Tooltip("最大弹射力（防止力度过大飞出屏幕）")]
    [SerializeField] private float maxBounceForce = 35f;

    [Tooltip("是否保留碰撞时的切线方向动量（沿平台表面的速度分量）")]
    [SerializeField] private bool preserveTangentMomentum = true;

    [Tooltip("切线动量保留比例（0=完全清除，1=完全保留）")]
    [SerializeField, Range(0f, 1f)] private float tangentMomentumFactor = 0.5f;

    [Header("=== 喜剧延迟 ===")]
    [Tooltip("碰撞后的压缩停留时间（秒），期间 Trickster 可增强弹性。0=立即弹射")]
    [SerializeField] private float comedyDelay = 0.2f;

    [Header("=== 动画设置 ===")]
    [SerializeField] private float squashAmount = 0.3f;
    [SerializeField] private float squashDuration = 0.1f;
    [SerializeField] private float stretchDuration = 0.15f;

    // 组件
    private BoxCollider2D boxCollider;

    // 状态
    private Vector3 originalScale;
    private bool isAnimating;

    // 喜剧延迟状态
    private bool isWaitingToLaunch;
    private float launchTimer;
    private Rigidbody2D pendingLaunchRb;
    private MarioController pendingLaunchMario;
    private Vector2 pendingLaunchNormal; // 碰撞法线方向

    // Trickster覆盖
    private bool tricksterOverride;
    private Vector2 tricksterDirection;
    private float tricksterForceMult = 1f;

    protected override void Awake()
    {
        propName = "弹跳平台";
        elementCategory = ElementCategory.Platform;
        elementTags = ElementTag.Controllable | ElementTag.AffectsPhysics | ElementTag.Resettable;
        elementDescription = "碰撞后沿法线方向弹射的平台";

        base.Awake();

        boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = false; // 实体碰撞
        originalScale = transform.localScale;
    }

    protected override void Update()
    {
        base.Update();

        // 喜剧延迟倒计时
        if (isWaitingToLaunch)
        {
            launchTimer -= Time.deltaTime;
            if (launchTimer <= 0f)
            {
                ExecuteLaunch();
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Rigidbody2D targetRb = collision.gameObject.GetComponent<Rigidbody2D>();
        if (targetRb == null) return;

        // 如果已经在等待发射，忽略
        if (isWaitingToLaunch) return;

        // Session 19: 使用碰撞接触点法线确定弹射方向
        // Unity OnCollisionEnter2D 中 collision 是在「被碰撞方」（平台）上触发的
        // contact.normal 的方向取决于谁是 collision 的「other」：
        //   - 脚本挂在平台上 → contact.normal 指向平台表面外侧（从平台指向玩家）
        //   - 这正好就是弹射方向（反方向力 = 从接触面推开玩家）
        Vector2 bounceNormal = Vector2.up; // 默认向上
        if (collision.contactCount > 0)
        {
            // 取所有接触点法线的平均值，得到更稳定的弹射方向
            Vector2 avgNormal = Vector2.zero;
            for (int i = 0; i < collision.contactCount; i++)
            {
                avgNormal += collision.GetContact(i).normal;
            }
            avgNormal = avgNormal.normalized;

            // contact.normal 在平台脚本中指向玩家（平台表面外侧）
            // 但实际上 Unity 的 contact.normal 是从 collider B 指向 collider A
            // 在 OnCollisionEnter2D 中，A = 平台（this），B = 玩家
            // 所以 contact.normal 指向平台（从玩家指向平台）
            // 弹射方向 = 取反 = 从平台推向玩家
            bounceNormal = -avgNormal;

            // 安全检查：如果法线计算异常，回退到默认向上
            if (bounceNormal.sqrMagnitude < 0.01f)
            {
                bounceNormal = Vector2.up;
            }
            else
            {
                bounceNormal = bounceNormal.normalized;
            }
        }

        MarioController mario = collision.gameObject.GetComponent<MarioController>();

        if (comedyDelay > 0f)
        {
            // 喜剧延迟模式：先冻结目标，等待延迟后弹射
            isWaitingToLaunch = true;
            launchTimer = comedyDelay;
            pendingLaunchRb = targetRb;
            pendingLaunchMario = mario;
            pendingLaunchNormal = bounceNormal;

            // 冻结目标速度（"陷入"效果）
            targetRb.velocity = Vector2.zero;

            // 开始压缩动画
            if (!isAnimating)
            {
                StartCoroutine(ComedySquashAnimation());
            }

            Debug.Log($"[BouncyPlatform] {collision.gameObject.name} 陷入平台，{comedyDelay}秒后沿 {bounceNormal} 弹射");
        }
        else
        {
            // 无延迟：立即弹射
            LaunchTarget(targetRb, mario, bounceNormal);
        }
    }

    /// <summary>延迟结束后执行弹射</summary>
    private void ExecuteLaunch()
    {
        isWaitingToLaunch = false;

        if (pendingLaunchRb != null)
        {
            LaunchTarget(pendingLaunchRb, pendingLaunchMario, pendingLaunchNormal);
        }

        pendingLaunchRb = null;
        pendingLaunchMario = null;
        pendingLaunchNormal = Vector2.up;
    }

    /// <summary>执行弹射逻辑（基于碰撞法线方向）</summary>
    private void LaunchTarget(Rigidbody2D targetRb, MarioController mario, Vector2 normal)
    {
        // 确定弹射方向：Trickster 覆盖 > 碰撞法线
        Vector2 launchDir = tricksterOverride ? tricksterDirection : normal.normalized;

        // 计算弹射力度（基础力 × 倍率 × Trickster增强）
        float force = bounceForce * bounceForceMultiplier;
        if (tricksterOverride) force *= tricksterForceMult;

        // 限制力度范围
        force = Mathf.Clamp(force, minBounceForce, maxBounceForce);

        // 计算最终速度
        Vector2 launchVelocity = launchDir * force;

        // 保留切线方向动量（沿平台表面的速度分量）
        if (preserveTangentMomentum)
        {
            // 切线方向 = 垂直于法线的方向
            Vector2 tangent = new Vector2(-launchDir.y, launchDir.x);
            float tangentSpeed = Vector2.Dot(targetRb.velocity, tangent);
            launchVelocity += tangent * tangentSpeed * tangentMomentumFactor;
        }

        // 直接设置速度（不用 AddForce，确保弹射效果一致）
        targetRb.velocity = launchVelocity;

        // 调用 MarioController.Bounce 同步内部状态
        if (mario != null)
        {
            // 只在向上弹射时调用 Bounce（更新跳跃状态）
            if (launchDir.y > 0.1f)
            {
                mario.Bounce(launchVelocity.y);
            }
        }

        // 弹跳回弹动画
        if (!isAnimating)
        {
            StartCoroutine(BounceAnimation());
        }

        Debug.Log($"[BouncyPlatform] 弹射 {targetRb.gameObject.name}, 方向={launchDir}, 力度={force}, 最终速度={launchVelocity}");
    }

    /// <summary>喜剧延迟期间的压缩动画（只压缩，不回弹）</summary>
    private System.Collections.IEnumerator ComedySquashAnimation()
    {
        isAnimating = true;

        // 压缩（快速）
        float t = 0;
        Vector3 squashedScale = new Vector3(
            originalScale.x * (1 + squashAmount),
            originalScale.y * (1 - squashAmount),
            originalScale.z);
        while (t < squashDuration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, squashedScale, t / squashDuration);
            yield return null;
        }
        transform.localScale = squashedScale;

        // 保持压缩状态直到发射
        while (isWaitingToLaunch)
        {
            yield return null;
        }

        isAnimating = false;
    }

    private System.Collections.IEnumerator BounceAnimation()
    {
        isAnimating = true;

        // 从当前状态（可能已压缩）拉伸回弹
        Vector3 currentScale = transform.localScale;
        Vector3 stretchedScale = new Vector3(
            originalScale.x * (1 - squashAmount * 0.5f),
            originalScale.y * (1 + squashAmount * 0.5f),
            originalScale.z);

        float t = 0;
        while (t < stretchDuration)
        {
            t += Time.deltaTime;
            float progress = t / stretchDuration;
            if (progress < 0.5f)
                transform.localScale = Vector3.Lerp(currentScale, stretchedScale, progress * 2f);
            else
                transform.localScale = Vector3.Lerp(stretchedScale, originalScale, (progress - 0.5f) * 2f);
            yield return null;
        }

        transform.localScale = originalScale;
        isAnimating = false;
    }

    // ── ControllablePropBase 实现 ────────────────────────

    protected override void OnTelegraphStart() { }
    protected override void OnTelegraphEnd() { }

    protected override void OnActivate(Vector2 direction)
    {
        tricksterOverride = true;
        tricksterDirection = direction.magnitude > 0.1f ? direction.normalized : Vector2.up;
        tricksterForceMult = 1.5f; // 操控时加强弹力
    }

    protected override void OnActiveEnd()
    {
        tricksterOverride = false;
        tricksterForceMult = 1f;
    }

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        transform.localScale = originalScale;
        tricksterOverride = false;
        isAnimating = false;
        isWaitingToLaunch = false;
        pendingLaunchRb = null;
        pendingLaunchMario = null;
        pendingLaunchNormal = Vector2.up;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) Gizmos.DrawWireCube(transform.position + (Vector3)col.offset, col.size);

        // 弹射方向预览（显示各面法线方向）
        if (col != null)
        {
            Vector3 center = transform.position + (Vector3)col.offset;
            Vector2 halfSize = col.size * 0.5f;

            Gizmos.color = new Color(0, 1, 0, 0.6f);
            // 顶面法线（向上）
            Gizmos.DrawRay(center + Vector3.up * halfSize.y, Vector3.up * 1.5f);
            // 底面法线（向下）
            Gizmos.DrawRay(center + Vector3.down * halfSize.y, Vector3.down * 1.0f);
            // 左面法线（向左）
            Gizmos.DrawRay(center + Vector3.left * halfSize.x, Vector3.left * 1.0f);
            // 右面法线（向右）
            Gizmos.DrawRay(center + Vector3.right * halfSize.x, Vector3.right * 1.0f);
        }
    }
}
