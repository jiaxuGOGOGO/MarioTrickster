using UnityEngine;

/// <summary>
/// 弹跳平台 - 关卡设计系统 · 平台类
/// 
/// 框架定位: ControllableLevelElement
/// 分类: Platform | 标签: Controllable, AffectsPhysics, Resettable
/// 
/// 功能:
///   - 玩家踩上后被弹射到高处（类似蘑菇弹簧）
///   - 可配置弹射力度和方向
///   - 弹射时有视觉压缩/回弹动画
///   - Trickster操控: 改变弹射方向或力度
/// 
/// Session 17 更新:
///   - 修复弹跳检测：改用 OnCollisionEnter2D 不再检查 contact.normal 方向限制
///     只要有碰撞就触发弹跳（宽边和长边都有效）
///   - 添加喜剧延迟（Comedy Delay）：Mario 落上平台后先"陷入"压缩一小段时间，
///     然后才弹射出去。延迟期间 Trickster 可以操控增强弹性
///   - comedyDelay 可在 Inspector 中调整延迟时长
/// 
/// 扩展/删除指南: 删除此文件不影响其他脚本
/// Session 15: 关卡设计系统新增
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BouncyPlatform : ControllableLevelElement
{
    [Header("=== 弹跳设置 ===")]
    [SerializeField] private float bounceForce = 15f;
    [SerializeField] private Vector2 bounceDirection = Vector2.up;
    [SerializeField] private bool addHorizontalMomentum = true;

    [Header("=== 喜剧延迟 (Session 17) ===")]
    [Tooltip("Mario 落上平台后的压缩停留时间（秒），期间 Trickster 可增强弹性")]
    [SerializeField] private float comedyDelay = 0.25f;

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

    // Trickster覆盖
    private bool tricksterOverride;
    private Vector2 tricksterDirection;
    private float tricksterForceMult = 1f;

    protected override void Awake()
    {
        propName = "弹跳平台";
        elementCategory = ElementCategory.Platform;
        elementTags = ElementTag.Controllable | ElementTag.AffectsPhysics | ElementTag.Resettable;
        elementDescription = "踩上后弹射到高处的平台";

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

        // Session 17: 移除 contact.normal 方向限制，宽边和长边都触发弹跳
        // 只要有碰撞就触发

        // 如果已经在等待发射，忽略
        if (isWaitingToLaunch) return;

        MarioController mario = collision.gameObject.GetComponent<MarioController>();

        if (comedyDelay > 0f)
        {
            // 喜剧延迟模式：先冻结 Mario，等待延迟后弹射
            isWaitingToLaunch = true;
            launchTimer = comedyDelay;
            pendingLaunchRb = targetRb;
            pendingLaunchMario = mario;

            // 冻结目标速度（"陷入"效果）
            targetRb.velocity = Vector2.zero;

            // 开始压缩动画（但不回弹，等发射时才回弹）
            if (!isAnimating)
            {
                StartCoroutine(ComedySquashAnimation());
            }

            Debug.Log($"[BouncyPlatform] {collision.gameObject.name} 陷入平台，{comedyDelay}秒后弹射（Trickster可增强弹性）");
        }
        else
        {
            // 无延迟：立即弹射（保留原有行为）
            LaunchTarget(targetRb, mario);
        }
    }

    /// <summary>延迟结束后执行弹射</summary>
    private void ExecuteLaunch()
    {
        isWaitingToLaunch = false;

        if (pendingLaunchRb != null)
        {
            LaunchTarget(pendingLaunchRb, pendingLaunchMario);
        }

        pendingLaunchRb = null;
        pendingLaunchMario = null;
    }

    /// <summary>执行弹射逻辑</summary>
    private void LaunchTarget(Rigidbody2D targetRb, MarioController mario)
    {
        // 计算弹射方向和力度
        Vector2 dir = tricksterOverride ? tricksterDirection : bounceDirection.normalized;
        float force = tricksterOverride ? bounceForce * tricksterForceMult : bounceForce;

        // 应用弹射
        Vector2 velocity = dir * force;
        if (addHorizontalMomentum)
        {
            velocity.x += targetRb.velocity.x * 0.5f; // 保留部分水平动量
        }
        targetRb.velocity = velocity;

        // 也尝试调用MarioController.Bounce
        if (mario != null)
        {
            mario.Bounce(force);
        }

        // 弹跳回弹动画
        if (!isAnimating)
        {
            StartCoroutine(BounceAnimation());
        }

        Debug.Log($"[BouncyPlatform] 弹射 {targetRb.gameObject.name}, 力度={force}, 方向={dir}");
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

        // 保持压缩状态直到发射（等待 ExecuteLaunch 触发 BounceAnimation）
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
        tricksterDirection = direction.magnitude > 0.1f ? direction.normalized : bounceDirection.normalized;
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
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) Gizmos.DrawWireCube(transform.position + (Vector3)col.offset, col.size);
        // 弹射方向预览
        Gizmos.color = new Color(0, 1, 0, 0.5f);
        Gizmos.DrawRay(transform.position, bounceDirection.normalized * 2f);
    }
}
