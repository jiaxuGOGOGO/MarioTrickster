using UnityEngine;

/// <summary>
/// 弹跳平台 - 关卡设计系统 · 平台类
/// 
/// 框架定位: ControllableLevelElement
/// 分类: Platform | 标签: Controllable, AffectsPhysics, Resettable
/// 
/// 功能:
///   - 玩家碰撞平台后，沿碰撞面法线方向夸张弹飞
///   - 弹射力度可在 Inspector 中调整（bounceForce × bounceForceMultiplier）
///   - 碰撞瞬间短暂冻结（comedyDelay）+ 挤压动画 → 然后猛力弹射
///   - 弹射时触发 ApplyKnockbackStun 让 MarioController 暂停速度覆盖
///     （这是关键！否则 MarioController.FixedUpdate 会在下一帧覆盖弹射速度）
///   - 弹射时触发相机震动增强打击感
///   - Trickster操控: 改变弹射方向或力度
/// 
/// Session 19 优化（参考 Sonic 弹簧 + 2D 平台游戏最佳实践）:
///   - 核心改进：使用碰撞接触点法线（contact.normal）确定弹射方向
///   - 关键修复：弹射时调用 ApplyKnockbackStun() 防止 MarioController 覆盖速度
///   - 弹射后调用 Bounce() 同步内部跳跃状态（向上分量时）
///   - 相机震动反馈增强弹飞的夸张感
/// 
/// 扩展/删除指南: 删除此文件不影响其他脚本
/// Session 15: 关卡设计系统新增
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BouncyPlatform : ControllableLevelElement
{
    [Header("=== 弹跳设置 ===")]
    [Tooltip("基础弹射力度（沿碰撞面法线方向）")]
    [SerializeField] private float bounceForce = 22f;

    [Tooltip("弹射力度倍率（用于微调，1.0=默认）")]
    [SerializeField] private float bounceForceMultiplier = 1f;

    [Tooltip("最小弹射力（防止力度过小无感）")]
    [SerializeField] private float minBounceForce = 10f;

    [Tooltip("最大弹射力（防止力度过大飞出屏幕）")]
    [SerializeField] private float maxBounceForce = 50f;

    [Header("=== 喜剧延迟 ===")]
    [Tooltip("碰撞后的压缩停留时间（秒），期间玩家被冻结在平台上。0=立即弹射")]
    [SerializeField] private float comedyDelay = 0.08f;

    [Header("=== 弹射后 Stun ===")]
    [Tooltip("弹射后 MarioController 暂停速度覆盖的时长（秒），让弹射力自然生效")]
    [SerializeField] private float bounceStunDuration = 0.35f;

    [Header("=== 相机震动 ===")]
    [Tooltip("弹射时是否触发相机震动")]
    [SerializeField] private bool enableCameraShake = true;
    [Tooltip("相机震动持续时间")]
    [SerializeField] private float shakeDuration = 0.15f;
    [Tooltip("相机震动强度")]
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
    private Vector2 pendingLaunchNormal;

    // Trickster覆盖
    private bool tricksterOverride;
    private Vector2 tricksterDirection;
    private float tricksterForceMult = 1f;

    // 缓存相机控制器
    private CameraController cachedCamera;

    protected override void Awake()
    {
        propName = "弹跳平台";
        elementCategory = ElementCategory.Platform;
        elementTags = ElementTag.Controllable | ElementTag.AffectsPhysics | ElementTag.Resettable;
        elementDescription = "碰撞后沿法线方向夸张弹飞的平台";

        base.Awake();

        boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = false;
        originalScale = transform.localScale;
    }

    private void Start()
    {
        // 缓存相机控制器（避免每次弹射时查找）
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

        // 如果已经在等待发射，忽略
        if (isWaitingToLaunch) return;

        // ── 计算碰撞法线方向 ──
        // Unity OnCollisionEnter2D 中，contact.normal 从碰撞体B指向碰撞体A
        // 脚本在平台(A)上，玩家是(B) → contact.normal 指向平台
        // 弹射方向 = 取反 = 从平台推向玩家
        Vector2 bounceNormal = Vector2.up;
        if (collision.contactCount > 0)
        {
            Vector2 avgNormal = Vector2.zero;
            for (int i = 0; i < collision.contactCount; i++)
            {
                avgNormal += collision.GetContact(i).normal;
            }
            bounceNormal = (-avgNormal).normalized;

            if (bounceNormal.sqrMagnitude < 0.01f)
            {
                bounceNormal = Vector2.up;
            }
        }

        MarioController mario = collision.gameObject.GetComponent<MarioController>();

        if (comedyDelay > 0f)
        {
            // ── 喜剧延迟模式 ──
            isWaitingToLaunch = true;
            launchTimer = comedyDelay;
            pendingLaunchRb = targetRb;
            pendingLaunchMario = mario;
            pendingLaunchNormal = bounceNormal;

            // 冻结目标速度（"陷入"效果）
            targetRb.velocity = Vector2.zero;

            // 如果是 Mario，立即触发 stun 防止 FixedUpdate 覆盖冻结
            if (mario != null)
            {
                mario.ApplyKnockbackStun(comedyDelay + bounceStunDuration);
            }

            // 压缩动画
            if (!isAnimating)
            {
                StartCoroutine(ComedySquashAnimation());
            }
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

    /// <summary>执行弹射逻辑（夸张弹飞效果）</summary>
    private void LaunchTarget(Rigidbody2D targetRb, MarioController mario, Vector2 normal)
    {
        // 确定弹射方向：Trickster 覆盖 > 碰撞法线
        Vector2 launchDir = tricksterOverride ? tricksterDirection : normal.normalized;

        // 计算弹射力度
        float force = bounceForce * bounceForceMultiplier;
        if (tricksterOverride) force *= tricksterForceMult;
        force = Mathf.Clamp(force, minBounceForce, maxBounceForce);

        // 最终弹射速度
        Vector2 launchVelocity = launchDir * force;

        // ── 关键：直接设置速度 ──
        targetRb.velocity = launchVelocity;

        // ── 关键：让 MarioController 暂停速度覆盖 ──
        // 没有这一步，MarioController.FixedUpdate 会在下一帧立刻覆盖弹射速度！
        if (mario != null)
        {
            // 如果之前没有在喜剧延迟中设置过 stun，现在设置
            // 如果已经设置过（喜剧延迟模式），stun 还在生效中，不需要重复设置
            if (comedyDelay <= 0f)
            {
                mario.ApplyKnockbackStun(bounceStunDuration);
            }

            // 同步 MarioController 内部跳跃状态（向上弹射时）
            if (launchDir.y > 0.1f)
            {
                mario.Bounce(launchVelocity.y);
            }
        }

        // ── 相机震动反馈 ──
        if (enableCameraShake && cachedCamera != null)
        {
            cachedCamera.Shake(shakeDuration, shakeMagnitude);
        }

        // 弹跳回弹动画
        if (!isAnimating)
        {
            StartCoroutine(BounceAnimation());
        }

        Debug.Log($"[BouncyPlatform] 弹飞 {targetRb.gameObject.name}, 方向={launchDir}, 力度={force}, 速度={launchVelocity}");
    }

    /// <summary>喜剧延迟期间的压缩动画</summary>
    private System.Collections.IEnumerator ComedySquashAnimation()
    {
        isAnimating = true;

        // 快速压缩
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

        // 保持压缩直到发射
        while (isWaitingToLaunch)
        {
            yield return null;
        }

        isAnimating = false;
    }

    /// <summary>弹射后的拉伸回弹动画</summary>
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
        tricksterForceMult = 1.8f; // 操控时大幅加强弹力
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

        // 弹射方向预览（各面法线箭头）
        if (col != null)
        {
            Vector3 center = transform.position + (Vector3)col.offset;
            Vector2 halfSize = col.size * 0.5f;

            Gizmos.color = new Color(0, 1, 0, 0.6f);
            Gizmos.DrawRay(center + Vector3.up * halfSize.y, Vector3.up * 1.5f);
            Gizmos.DrawRay(center + Vector3.down * halfSize.y, Vector3.down * 1.0f);
            Gizmos.DrawRay(center + Vector3.left * halfSize.x, Vector3.left * 1.0f);
            Gizmos.DrawRay(center + Vector3.right * halfSize.x, Vector3.right * 1.0f);
        }
    }
}
