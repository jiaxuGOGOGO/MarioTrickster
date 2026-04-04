using UnityEngine;

/// <summary>
/// 弹跳平台 - 关卡设计系统 · 平台类
/// 
/// 框架定位: ControllableLevelElement
/// 分类: Platform | 标签: Controllable, AffectsPhysics, Resettable
/// 
/// 核心机制（反方向弹飞 + 位置偏移）:
///   玩家碰撞平台后，弹射方向由两部分混合决定：
///   1. 来向反弹：玩家碰撞前运动方向取反（collision.relativeVelocity）
///   2. 位置偏移：玩家相对平台中心的偏移方向
///      - 落在平台左半边 → 加入向左的水平分量
///      - 落在平台右半边 → 加入向右的水平分量
///      - 落在平台正中间 → 纯向上（但力度更大）
///   
///   这样即使正方体玩家垂直落下（来向纯向下），也会因为位置偏移
///   而被弹飞到一侧，不会原地反复弹跳。
/// 
///   碰撞瞬间短暂冻结（comedyDelay）+ 挤压动画 → 然后猛力弹射
///   弹射时触发 ApplyBounceStun 让 MarioController 暂停速度覆盖
///   弹射时触发相机震动增强打击感
///   Trickster操控: 改变弹射方向或力度
/// 
/// Inspector 可调参数:
///   - bounceForce: 基础弹射力
///   - bounceForceMultiplier: 弹射力倍率
///   - positionInfluence: 位置偏移对弹射方向的影响权重（0=纯来向反弹，1=纯位置偏移）
///   - comedyDelay: 碰撞后冻结时间
///   - bounceStunDuration: 弹射后 MarioController 暂停覆盖时长
/// 
/// 扩展/删除指南: 删除此文件不影响其他脚本
/// Session 15: 关卡设计系统新增
/// Session 19: 反方向弹飞 + 位置偏移 + KnockbackStun防覆盖 + 相机震动
/// Session 20: 物理弹跳优化
///   - 碰撞法线修正：过滤极端 X 轴法线，混合平台 Up 方向
///   - 抛物线修复：引入 BounceStun 状态（区别于 KnockbackStun），
///     弹跳期间大幅降低横向操控力而非完全禁止，让物理引擎先接管抛物线
///   - 参考 Celeste 弹簧 / Sonic 弹簧最佳实践
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

    [Header("=== 弹射方向混合 ===")]
    [Tooltip("位置偏移对弹射方向的影响权重。0=纯来向反弹，1=纯位置偏移。推荐0.5-0.7")]
    [SerializeField, Range(0f, 1f)] private float positionInfluence = 0.6f;

    [Tooltip("最小向上分量（保证弹射有弧线感，不会贴地滑行）")]
    [SerializeField] private float minUpwardComponent = 0.4f;

    [Header("=== 碰撞法线修正 (Session 20) ===")]
    [Tooltip("法线与平台 Up 方向的混合权重。0=纯碰撞法线，1=纯平台Up。推荐0.3-0.5")]
    [SerializeField, Range(0f, 1f)] private float normalBlendWithUp = 0.4f;

    [Tooltip("X 轴法线绝对值超过此阈值时视为极端侧面碰撞，强制修正")]
    [SerializeField] private float extremeNormalXThreshold = 0.85f;

    [Header("=== 喜剧延迟 ===")]
    [Tooltip("碰撞后的冻结时间（秒），期间玩家被冻结在平台上。0=立即弹射")]
    [SerializeField] private float comedyDelay = 0.08f;

    [Header("=== 弹射后 Stun (Session 20 优化) ===")]
    [Tooltip("弹射后 BounceStun 时长（秒）：期间横向操控力大幅降低，保留抛物线")]
    [SerializeField] private float bounceStunDuration = 0.35f;

    [Tooltip("BounceStun 期间横向加速度倍率（0=完全不可控，0.15=微弱操控，1=正常）")]
    [SerializeField, Range(0f, 1f)] private float bounceStunAccelMultiplier = 0.12f;

    [Tooltip("BounceStun 期间横向减速度倍率（降低减速让惯性保持更久）")]
    [SerializeField, Range(0f, 1f)] private float bounceStunDecelMultiplier = 0.08f;

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
    private TricksterController pendingLaunchTrickster;
    private Vector2 pendingIncomingVelocity;
    private Vector2 pendingContactPosition;
    private Vector2 pendingCorrectedNormal; // Session 20: 修正后的碰撞法线

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

        // ── 记录碰撞信息 ──
        // 1. 来向速度（用于反弹方向）
        Vector2 incomingVelocity = collision.relativeVelocity;
        if (incomingVelocity.sqrMagnitude < 0.5f)
        {
            incomingVelocity = Vector2.up;
        }

        // 2. 碰撞接触点位置（用于位置偏移方向）
        Vector2 contactPos = (Vector2)collision.gameObject.transform.position;
        if (collision.contactCount > 0)
        {
            // 用所有接触点的平均位置
            contactPos = Vector2.zero;
            for (int i = 0; i < collision.contactCount; i++)
            {
                contactPos += collision.GetContact(i).point;
            }
            contactPos /= collision.contactCount;
        }

        // Session 20: 碰撞法线修正
        Vector2 correctedNormal = CalcCorrectedNormal(collision);

        MarioController mario = collision.gameObject.GetComponent<MarioController>();
        TricksterController trickster = collision.gameObject.GetComponent<TricksterController>();

        if (comedyDelay > 0f)
        {
            isWaitingToLaunch = true;
            launchTimer = comedyDelay;
            pendingLaunchRb = targetRb;
            pendingLaunchMario = mario;
            pendingLaunchTrickster = trickster;
            pendingIncomingVelocity = incomingVelocity;
            pendingContactPosition = contactPos;
            pendingCorrectedNormal = correctedNormal;

            targetRb.velocity = Vector2.zero;

            // 喜剧延迟期间先施加 stun（防止帧速度覆盖冻结效果）
            float totalStunTime = comedyDelay + bounceStunDuration;
            if (mario != null)
            {
                mario.ApplyBounceStun(totalStunTime, bounceStunAccelMultiplier, bounceStunDecelMultiplier);
            }
            if (trickster != null)
            {
                trickster.ApplyKnockbackStun(totalStunTime);
            }

            if (!isAnimating)
            {
                StartCoroutine(ComedySquashAnimation());
            }
        }
        else
        {
            LaunchTarget(targetRb, mario, trickster, incomingVelocity, contactPos, correctedNormal);
        }
    }

    /// <summary>
    /// Session 20: 碰撞法线修正
    /// 
    /// 问题：BoxCollider2D 在角落碰撞时会产生极端的 X 轴法线（接近水平），
    /// 导致弹射方向不自然（贴地滑行而非弧线弹飞）。
    /// 
    /// 解决方案：
    /// 1. 将碰撞法线与平台自身的 Up 方向做混合（Blend）
    /// 2. 过滤掉极端的 X 轴法线（|normal.x| > threshold），强制使用平台 Up
    /// 3. 最终法线归一化后用于弹射方向计算
    /// </summary>
    private Vector2 CalcCorrectedNormal(Collision2D collision)
    {
        if (collision.contactCount == 0)
        {
            return transform.up;
        }

        // 计算平均碰撞法线
        Vector2 avgNormal = Vector2.zero;
        for (int i = 0; i < collision.contactCount; i++)
        {
            avgNormal += collision.GetContact(i).normal;
        }
        avgNormal /= collision.contactCount;

        // 极端法线过滤：X 轴分量过大说明是侧面碰撞，强制修正
        if (Mathf.Abs(avgNormal.x) > extremeNormalXThreshold)
        {
            // 保留少量 X 分量（给一点水平弹射感），但主要用平台 Up
            Vector2 platformUp = transform.up;
            float preservedX = Mathf.Sign(avgNormal.x) * 0.3f;
            avgNormal = new Vector2(preservedX, platformUp.y).normalized;
        }
        else
        {
            // 正常法线：与平台 Up 方向混合
            Vector2 platformUp = transform.up;
            avgNormal = Vector2.Lerp(avgNormal, platformUp, normalBlendWithUp).normalized;
        }

        return avgNormal;
    }

    private void ExecuteLaunch()
    {
        isWaitingToLaunch = false;

        if (pendingLaunchRb != null)
        {
            LaunchTarget(pendingLaunchRb, pendingLaunchMario, pendingLaunchTrickster,
                pendingIncomingVelocity, pendingContactPosition, pendingCorrectedNormal);
        }

        pendingLaunchRb = null;
        pendingLaunchMario = null;
        pendingLaunchTrickster = null;
        pendingIncomingVelocity = Vector2.zero;
        pendingContactPosition = Vector2.zero;
        pendingCorrectedNormal = Vector2.zero;
    }

    /// <summary>
    /// 执行弹射。方向由来向反弹和位置偏移混合决定。
    /// Session 20: 使用修正后的碰撞法线参与方向计算。
    /// </summary>
    private void LaunchTarget(Rigidbody2D targetRb, MarioController mario, TricksterController trickster,
        Vector2 incomingVelocity, Vector2 contactPos, Vector2 correctedNormal)
    {
        Vector2 launchDir;

        if (tricksterOverride)
        {
            launchDir = tricksterDirection;
        }
        else
        {
            // ── 方向1：来向反弹（结合修正法线） ──
            // Session 20: 使用修正后的法线做反射，而非简单取反
            // 这样侧面碰撞也能产生合理的弹射弧线
            Vector2 incomingDir = -incomingVelocity.normalized; // 玩家实际运动方向
            Vector2 reflectDir = Vector2.Reflect(incomingDir, correctedNormal);

            // 如果反射结果朝下（不合理），回退到法线方向
            if (reflectDir.y < 0.1f)
            {
                reflectDir = correctedNormal;
            }

            // ── 方向2：位置偏移 ──
            // 从平台中心指向碰撞点的方向
            Vector2 platformCenter = (Vector2)transform.position + boxCollider.offset;
            Vector2 offsetDir = (contactPos - platformCenter);

            // 归一化偏移（相对于平台半尺寸，-1到+1范围）
            Vector2 halfSize = boxCollider.size * 0.5f * (Vector2)transform.localScale;
            if (halfSize.x > 0.01f) offsetDir.x /= halfSize.x;
            if (halfSize.y > 0.01f) offsetDir.y /= halfSize.y;

            // 如果偏移太小（正中间），给一个随机水平扰动防止纯垂直弹
            if (Mathf.Abs(offsetDir.x) < 0.1f && Mathf.Abs(offsetDir.y) < 0.1f)
            {
                offsetDir.x = Random.value > 0.5f ? 0.5f : -0.5f;
                offsetDir.y = 1f;
            }

            Vector2 posDir = offsetDir.normalized;

            // ── 混合两个方向 ──
            launchDir = Vector2.Lerp(reflectDir, posDir, positionInfluence).normalized;

            // 保证最小向上分量（防止贴地滑行）
            if (launchDir.y < minUpwardComponent)
            {
                launchDir.y = minUpwardComponent;
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

        // Session 20: 使用 BounceStun 替代 KnockbackStun
        // BounceStun 不完全禁止横向控制，而是大幅降低操控力
        // 让物理引擎先接管抛物线轨迹，之后再恢复玩家控制
        if (mario != null)
        {
            if (comedyDelay <= 0f)
            {
                mario.ApplyBounceStun(bounceStunDuration, bounceStunAccelMultiplier, bounceStunDecelMultiplier);
            }

            if (launchDir.y > 0.1f)
            {
                mario.Bounce(launchVelocity.y);
            }
        }

        if (trickster != null && comedyDelay <= 0f)
        {
            trickster.ApplyKnockbackStun(bounceStunDuration);
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

        Debug.Log($"[BouncyPlatform] 弹飞 {targetRb.gameObject.name}, 修正法线={correctedNormal}, 来向反射={Vector2.Reflect(-incomingVelocity.normalized, correctedNormal)}, 混合后={launchDir}, 力度={force}");
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
        pendingLaunchTrickster = null;
        pendingIncomingVelocity = Vector2.zero;
        pendingContactPosition = Vector2.zero;
        pendingCorrectedNormal = Vector2.zero;
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

            Gizmos.color = new Color(1, 0.5f, 0, 0.8f);
            // 显示弹射方向箭头
            Gizmos.DrawRay(center + Vector3.up * halfSize.y, Vector3.up * 1.5f);
            Gizmos.DrawRay(center + Vector3.down * halfSize.y, Vector3.down * 1.0f);
            Gizmos.DrawRay(center + Vector3.left * halfSize.x, Vector3.left * 1.0f);
            Gizmos.DrawRay(center + Vector3.right * halfSize.x, Vector3.right * 1.0f);
            // 对角方向（表示位置偏移弹飞）
            Gizmos.color = new Color(1, 0.5f, 0, 0.4f);
            Gizmos.DrawRay(center + new Vector3(-halfSize.x, halfSize.y, 0), new Vector3(-1, 1, 0).normalized * 1.2f);
            Gizmos.DrawRay(center + new Vector3(halfSize.x, halfSize.y, 0), new Vector3(1, 1, 0).normalized * 1.2f);

            // Session 20: 显示平台 Up 方向（法线修正参考）
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(center, transform.up * 1.5f);
        }
    }
}
