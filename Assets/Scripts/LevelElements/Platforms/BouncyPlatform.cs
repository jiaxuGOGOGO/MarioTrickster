using UnityEngine;

/// <summary>
/// 弹跳平台 - 关卡设计系统 · 平台类
/// 
/// 框架定位: ControllableLevelElement
/// 分类: Platform | 标签: Controllable, AffectsPhysics, Resettable
/// 
/// 核心机制（反方向弹飞）:
///   玩家碰撞平台后，根据玩家碰撞前的运动方向，给予一个**反方向的力**将玩家弹飞。
///   - 从左边跑来碰到平台 → 被弹回左边
///   - 从上方落下碰到平台 → 被弹回上方
///   - 从右下方飞来碰到平台 → 被弹回左上方
///   就像撞到弹簧墙一样，来的方向反弹回去。
/// 
///   碰撞瞬间短暂冻结（comedyDelay）+ 挤压动画 → 然后猛力反方向弹射
///   弹射时触发 ApplyKnockbackStun 让 MarioController 暂停速度覆盖
///   弹射时触发相机震动增强打击感
///   Trickster操控: 改变弹射方向或力度
/// 
/// Inspector 可调参数:
///   - bounceForce: 基础弹射力
///   - bounceForceMultiplier: 弹射力倍率
///   - comedyDelay: 碰撞后冻结时间
///   - bounceStunDuration: 弹射后 MarioController 暂停覆盖时长
///   - minUpwardBias: 最小向上偏移（防止纯水平弹射没有起飞感）
/// 
/// 扩展/删除指南: 删除此文件不影响其他脚本
/// Session 15: 关卡设计系统新增
/// Session 19: 反方向弹飞 + KnockbackStun防覆盖 + 相机震动
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BouncyPlatform : ControllableLevelElement
{
    [Header("=== 弹跳设置 ===")]
    [Tooltip("基础弹射力度")]
    [SerializeField] private float bounceForce = 22f;

    [Tooltip("弹射力度倍率（用于微调，1.0=默认）")]
    [SerializeField] private float bounceForceMultiplier = 1f;

    [Tooltip("最小弹射力")]
    [SerializeField] private float minBounceForce = 10f;

    [Tooltip("最大弹射力")]
    [SerializeField] private float maxBounceForce = 50f;

    [Tooltip("最小向上偏移（保证弹射有一定向上分量，防止纯水平贴地滑行）")]
    [SerializeField] private float minUpwardBias = 0.3f;

    [Header("=== 喜剧延迟 ===")]
    [Tooltip("碰撞后的冻结时间（秒），期间玩家被冻结在平台上。0=立即弹射")]
    [SerializeField] private float comedyDelay = 0.08f;

    [Header("=== 弹射后 Stun ===")]
    [Tooltip("弹射后 MarioController 暂停速度覆盖的时长（秒），让弹射力自然生效")]
    [SerializeField] private float bounceStunDuration = 0.35f;

    [Header("=== 相机震动 ===")]
    [Tooltip("弹射时是否触发相机震动")]
    [SerializeField] private bool enableCameraShake = true;
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeMagnitude = 0.25f;

    [Header("=== 动画设置 ===")]
    [SerializeField] private float squashAmount = 0.35f;
    [SerializeField] private float squashDuration = 0.06f;
    [SerializeField] private float stretchDuration = 0.12f;

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
    private Vector2 pendingIncomingVelocity; // 碰撞瞬间玩家的运动速度（用于计算反方向）

    // Trickster覆盖
    private bool tricksterOverride;
    private Vector2 tricksterDirection;
    private float tricksterForceMult = 1f;

    // 缓存
    private CameraController cachedCamera;

    protected override void Awake()
    {
        propName = "弹跳平台";
        elementCategory = ElementCategory.Platform;
        elementTags = ElementTag.Controllable | ElementTag.AffectsPhysics | ElementTag.Resettable;
        elementDescription = "碰撞后反方向弹飞的平台";

        base.Awake();

        boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = false;
        originalScale = transform.localScale;
    }

    private void Start()
    {
        cachedCamera = FindObjectOfType<CameraController>();
    }

    protected override void Update()
    {
        base.Update();

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
        if (isWaitingToLaunch) return;

        // ── 核心：记录碰撞瞬间玩家的运动速度 ──
        // 弹射方向 = 玩家来的方向取反（反弹）
        Vector2 incomingVelocity = collision.relativeVelocity;

        // relativeVelocity 是 A 相对于 B 的速度
        // 在平台(A)脚本中，relativeVelocity = 平台速度 - 玩家速度
        // 如果平台静止，relativeVelocity = -玩家速度
        // 所以 relativeVelocity 的方向就是"从玩家指向平台"的反方向
        // 即 relativeVelocity 本身就是弹射方向（从平台推开玩家）

        // 安全检查：如果速度太小（几乎静止碰撞），默认向上弹
        if (incomingVelocity.sqrMagnitude < 0.5f)
        {
            incomingVelocity = Vector2.up;
        }

        MarioController mario = collision.gameObject.GetComponent<MarioController>();

        if (comedyDelay > 0f)
        {
            isWaitingToLaunch = true;
            launchTimer = comedyDelay;
            pendingLaunchRb = targetRb;
            pendingLaunchMario = mario;
            pendingIncomingVelocity = incomingVelocity;

            // 冻结玩家
            targetRb.velocity = Vector2.zero;

            // 立即 stun 防止 MarioController 覆盖冻结
            if (mario != null)
            {
                mario.ApplyKnockbackStun(comedyDelay + bounceStunDuration);
            }

            if (!isAnimating)
            {
                StartCoroutine(ComedySquashAnimation());
            }
        }
        else
        {
            LaunchTarget(targetRb, mario, incomingVelocity);
        }
    }

    private void ExecuteLaunch()
    {
        isWaitingToLaunch = false;

        if (pendingLaunchRb != null)
        {
            LaunchTarget(pendingLaunchRb, pendingLaunchMario, pendingIncomingVelocity);
        }

        pendingLaunchRb = null;
        pendingLaunchMario = null;
        pendingIncomingVelocity = Vector2.zero;
    }

    /// <summary>
    /// 执行反方向弹射。
    /// incomingVelocity = collision.relativeVelocity，方向已经是"从平台推开玩家"的方向。
    /// </summary>
    private void LaunchTarget(Rigidbody2D targetRb, MarioController mario, Vector2 incomingVelocity)
    {
        // ── 计算弹射方向 ──
        Vector2 launchDir;

        if (tricksterOverride)
        {
            // Trickster 操控时使用指定方向
            launchDir = tricksterDirection;
        }
        else
        {
            // 正常模式：反方向弹飞
            // relativeVelocity 在平台静止时 = -玩家速度，方向就是弹射方向
            launchDir = incomingVelocity.normalized;

            // 保证有一定向上分量（防止纯水平贴地滑行，让弹飞有弧线感）
            if (launchDir.y < minUpwardBias)
            {
                launchDir.y = minUpwardBias;
                launchDir = launchDir.normalized;
            }
        }

        // 计算弹射力度
        float force = bounceForce * bounceForceMultiplier;
        if (tricksterOverride) force *= tricksterForceMult;
        force = Mathf.Clamp(force, minBounceForce, maxBounceForce);

        // 最终弹射速度
        Vector2 launchVelocity = launchDir * force;

        // 直接设置速度
        targetRb.velocity = launchVelocity;

        // 让 MarioController 暂停速度覆盖
        if (mario != null)
        {
            if (comedyDelay <= 0f)
            {
                mario.ApplyKnockbackStun(bounceStunDuration);
            }

            // 同步跳跃状态（有向上分量时）
            if (launchDir.y > 0.1f)
            {
                mario.Bounce(launchVelocity.y);
            }
        }

        // 相机震动
        if (enableCameraShake && cachedCamera != null)
        {
            cachedCamera.Shake(shakeDuration, shakeMagnitude);
        }

        // 弹跳动画
        if (!isAnimating)
        {
            StartCoroutine(BounceAnimation());
        }

        Debug.Log($"[BouncyPlatform] 反弹 {targetRb.gameObject.name}, 来向={incomingVelocity}, 弹向={launchDir}, 力度={force}");
    }

    private System.Collections.IEnumerator ComedySquashAnimation()
    {
        isAnimating = true;

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

        while (isWaitingToLaunch)
        {
            yield return null;
        }

        isAnimating = false;
    }

    private System.Collections.IEnumerator BounceAnimation()
    {
        isAnimating = true;

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
        tricksterForceMult = 1.8f;
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
        pendingIncomingVelocity = Vector2.zero;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) Gizmos.DrawWireCube(transform.position + (Vector3)col.offset, col.size);

        if (col != null)
        {
            Vector3 center = transform.position + (Vector3)col.offset;
            Vector2 halfSize = col.size * 0.5f;

            // 显示各面的反弹方向箭头
            Gizmos.color = new Color(1, 0.5f, 0, 0.8f); // 橙色
            Gizmos.DrawRay(center + Vector3.up * halfSize.y, Vector3.up * 1.5f);
            Gizmos.DrawRay(center + Vector3.down * halfSize.y, Vector3.down * 1.0f);
            Gizmos.DrawRay(center + Vector3.left * halfSize.x, Vector3.left * 1.0f);
            Gizmos.DrawRay(center + Vector3.right * halfSize.x, Vector3.right * 1.0f);
        }
    }
}
