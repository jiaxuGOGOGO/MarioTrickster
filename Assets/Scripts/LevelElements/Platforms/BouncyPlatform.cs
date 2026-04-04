using UnityEngine;
using System.Collections;

/// <summary>
/// 弹跳平台 - 关卡设计系统 · 平台类
/// 
/// 框架定位: ControllableLevelElement
/// 分类: Platform | 标签: Controllable, AffectsPhysics, Resettable
/// 
/// 核心机制（两段式弹射 + 反方向弹飞 + 位置偏移）:
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
/// Session 22 重构：两段式弹射协程
///   整个弹射流程由 LaunchSequence 协程统一管理：
///   
///   阶段1 - 蓄力冻结期（Comedy Delay）:
///     碰撞瞬间 → mario.PrepareBounce() 冻结角色
///     → 挤压动画 + WaitForSeconds(comedyDelay)
///     → 期间 Trickster 可按 L 操控修改弹力
///   
///   阶段2 - 发射:
///     延迟结束 → 计算最终弹力（含 Trickster 操控加成）
///     → mario.ExecuteBounce(finalVelocity) 注入绝对速度
///     → MarioController 进入 _isBouncing 飞行期
///     → 落地或碰墙自动解除，恢复正常控制
///   
///   优势：
///     - 时序逻辑集中在一个协程中，不再分散在 OnCollisionEnter2D / Update / ExecuteLaunch
///     - 蓄力冻结和抛物线飞行是两个独立阶段，互不冲突
///     - WaitForSeconds 使用缓存实例，避免协程中 GC 分配
///   
///   Trickster操控: 在蓄力冻结期按 L 键改变弹射方向或力度
/// 
/// Inspector 可调参数:
///   - bounceForce: 基础弹射力
///   - bounceForceMultiplier: 弹射力倍率
///   - positionInfluence: 位置偏移对弹射方向的影响权重
///   - comedyDelay: 碰撞后冻结时间（蓄力期）
/// 
/// 扩展/删除指南: 删除此文件不影响其他脚本
/// Session 15: 关卡设计系统新增
/// Session 19: 反方向弹飞 + 位置偏移 + KnockbackStun防覆盖 + 相机震动
/// Session 20: 碰撞法线修正 + BounceStun 抛物线保留
/// Session 21: SetFrameVelocity 绝对速度注入 + maxSpeed 截断跳过
/// Session 22: 两段式弹射协程重构
///   - 废除 Update 手动计时器，改用 LaunchSequence 协程统一管理
///   - 碰撞瞬间调用 mario.PrepareBounce() 冻结角色
///   - 延迟结束调用 mario.ExecuteBounce(velocity) 注入绝对速度
///   - WaitForSeconds 缓存实例避免 GC
///   - Trickster 操控窗口在蓄力冻结期内
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

    [Header("=== 喜剧延迟（蓄力冻结期） ===")]
    [Tooltip("碰撞后的冻结时间（秒）。期间角色被冻结，Trickster 可操控。0=立即弹射")]
    [SerializeField] private float comedyDelay = 0.25f;

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

    // [AI防坑警告] 弹射全流程由 LaunchSequence 协程统一管理，不要拆回 Update 手动计时器！
    // 协程时序：OnCollisionEnter2D → PrepareBounce(冻结) → WaitForSeconds → ExecuteBounce(发射)
    // 不要用 AddForce 替代 ExecuteBounce — AddForce 会被 MarioController._frameVelocity 写入覆盖。
    // 不要用 rb.velocity = xxx 替代 ExecuteBounce — 同理，下一帧 FixedUpdate 会读回并截断。
    // 状态
    private Vector3 originalScale;
    private bool isAnimating;
    private bool isLaunching; // 是否正在执行弹射序列（防止重复触发）

    // 当前活跃的弹射协程（用于 OnLevelReset 中止）
    private Coroutine activeLaunchCoroutine;

    // Trickster覆盖（在蓄力冻结期内可修改）
    private bool tricksterOverride;
    private Vector2 tricksterDirection;
    private float tricksterForceMult = 1f;

    // 缓存
    private CameraController cachedCamera;

    // P1-P7: 缓存 WaitForSeconds 实例，避免协程中每次 new 产生 GC
    private WaitForSeconds cachedComedyWait;

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

        // 缓存 WaitForSeconds（P7: 避免协程中 GC 分配）
        cachedComedyWait = new WaitForSeconds(comedyDelay);
    }

    private void Start()
    {
        cachedCamera = FindObjectOfType<CameraController>();
    }

    // ─────────────────────────────────────────────────────
    #region 碰撞入口

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 防止重复触发（已有弹射序列在进行中）
        if (isLaunching) return;

        Rigidbody2D targetRb = collision.gameObject.GetComponent<Rigidbody2D>();
        if (targetRb == null) return;

        MarioController mario = collision.gameObject.GetComponent<MarioController>();
        TricksterController trickster = collision.gameObject.GetComponent<TricksterController>();

        // ── 记录碰撞信息 ──
        Vector2 incomingVelocity = collision.relativeVelocity;
        if (incomingVelocity.sqrMagnitude < 0.5f)
        {
            incomingVelocity = Vector2.up;
        }

        // 碰撞接触点位置（用于位置偏移方向）
        Vector2 contactPos = (Vector2)collision.gameObject.transform.position;
        if (collision.contactCount > 0)
        {
            contactPos = Vector2.zero;
            for (int i = 0; i < collision.contactCount; i++)
            {
                contactPos += collision.GetContact(i).point;
            }
            contactPos /= collision.contactCount;
        }

        // Session 20: 碰撞法线修正
        Vector2 correctedNormal = CalcCorrectedNormal(collision);

        // 启动弹射序列协程
        activeLaunchCoroutine = StartCoroutine(
            LaunchSequence(targetRb, mario, trickster, incomingVelocity, contactPos, correctedNormal));
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 两段式弹射协程 (Session 22)

    // [AI防坑警告] 这是弹射的核心协程，时序严格不可打乱！
    // 必须先 PrepareBounce(冻结) → 等待 comedyDelay → 再 ExecuteBounce(发射)。
    // 如果跳过冻结直接发射，角色当帧可能还有重力累积的旧速度，弹射轨道会被污染。
    // cachedComedyWait 在 Awake 中缓存，协程中禁止 new WaitForSeconds（P1-P7 GC 规范）。
    /// <summary>
    /// 两段式弹射协程：统一管理蓄力冻结 → 发射的完整时序。
    /// 
    /// 阶段1（蓄力冻结期）:
    ///   - 碰撞瞬间调用 mario.PrepareBounce() 冻结角色
    ///   - 播放挤压动画
    ///   - yield return cachedComedyWait 等待喜剧延迟
    ///   - 期间 Trickster 可按 L 操控修改 bounceForceMultiplier
    /// 
    /// 阶段2（发射）:
    ///   - 计算最终弹力（含 Trickster 操控加成）
    ///   - 调用 mario.ExecuteBounce(finalVelocity) 注入绝对速度
    ///   - 播放弹跳动画 + 相机震动
    /// 
    /// P1-P7 合规:
    ///   - WaitForSeconds 使用 Awake 中缓存的实例（避免 GC）
    ///   - 协程中无 new 分配（除 Debug.Log 字符串插值，仅调试用）
    ///   - 动画协程使用 yield return null（帧等待无 GC）
    /// </summary>
    private IEnumerator LaunchSequence(Rigidbody2D targetRb, MarioController mario,
        TricksterController trickster, Vector2 incomingVelocity, Vector2 contactPos, Vector2 correctedNormal)
    {
        isLaunching = true;

        // ══════════════════════════════════════════════════
        // 阶段1：蓄力冻结
        // ══════════════════════════════════════════════════

        // 冻结角色
        if (mario != null)
        {
            mario.PrepareBounce();
        }
        else
        {
            // 非 Mario 角色（Trickster 等）：直接冻结 rb
            targetRb.velocity = Vector2.zero;
        }

        if (trickster != null)
        {
            trickster.ApplyKnockbackStun(comedyDelay + 0.5f);
        }

        // 挤压动画（内联，避免嵌套协程）
        if (comedyDelay > 0f)
        {
            Vector3 squashedScale = new Vector3(
                originalScale.x * (1 + squashAmount),
                originalScale.y * (1 - squashAmount),
                originalScale.z);

            float t = 0f;
            while (t < squashDuration)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(originalScale, squashedScale, t / squashDuration);
                yield return null;
            }
            transform.localScale = squashedScale;

            // 等待喜剧延迟（使用缓存的 WaitForSeconds）
            yield return cachedComedyWait;
        }

        // ══════════════════════════════════════════════════
        // 阶段2：发射
        // ══════════════════════════════════════════════════

        // 计算弹射方向
        Vector2 launchDir = CalcLaunchDirection(incomingVelocity, contactPos, correctedNormal);

        // 计算弹射力度（含 Trickster 操控加成）
        float force = bounceForce * bounceForceMultiplier;
        if (tricksterOverride) force *= tricksterForceMult;
        force = Mathf.Clamp(force, minBounceForce, maxBounceForce);

        // 最终弹射速度向量
        Vector2 launchVelocity = launchDir * force;

        // 执行弹射
        if (mario != null)
        {
            mario.ExecuteBounce(launchVelocity);
        }
        else
        {
            // 非 Mario 角色：直接设置 rb.velocity
            targetRb.velocity = launchVelocity;
        }

        // 相机震动
        if (enableCameraShake && cachedCamera != null)
        {
            cachedCamera.Shake(shakeDuration, shakeMagnitude);
        }

        Debug.Log($"[BouncyPlatform] 弹飞 {targetRb.gameObject.name}, 方向={launchDir}, 速度={launchVelocity}, 力度={force}, Trickster操控={tricksterOverride}");

        // 弹跳拉伸动画（内联）
        {
            Vector3 currentScale = transform.localScale;
            Vector3 stretchedScale = new Vector3(
                originalScale.x * (1 - squashAmount * 0.5f),
                originalScale.y * (1 + squashAmount * 0.5f),
                originalScale.z);

            float t = 0f;
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
        }

        isLaunching = false;
        activeLaunchCoroutine = null;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 弹射方向计算

    /// <summary>
    /// 计算弹射方向：来向反弹 + 位置偏移混合。
    /// Session 20: 使用修正后的碰撞法线参与方向计算。
    /// </summary>
    private Vector2 CalcLaunchDirection(Vector2 incomingVelocity, Vector2 contactPos, Vector2 correctedNormal)
    {
        if (tricksterOverride)
        {
            return tricksterDirection;
        }

        // ── 方向1：来向反弹（结合修正法线） ──
        Vector2 incomingDir = -incomingVelocity.normalized;
        Vector2 reflectDir = Vector2.Reflect(incomingDir, correctedNormal);

        // 如果反射结果朝下（不合理），回退到法线方向
        if (reflectDir.y < 0.1f)
        {
            reflectDir = correctedNormal;
        }

        // ── 方向2：位置偏移 ──
        Vector2 platformCenter = (Vector2)transform.position + boxCollider.offset;
        Vector2 offsetDir = (contactPos - platformCenter);

        // 归一化偏移（相对于平台半尺寸）
        Vector2 halfSize = boxCollider.size * 0.5f * (Vector2)transform.lossyScale;
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
        Vector2 launchDir = Vector2.Lerp(reflectDir, posDir, positionInfluence).normalized;

        // 保证最小向上分量（防止贴地滑行）
        if (launchDir.y < minUpwardComponent)
        {
            launchDir.y = minUpwardComponent;
            launchDir = launchDir.normalized;
        }

        return launchDir;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 碰撞法线修正 (Session 20)

    /// <summary>
    /// 碰撞法线修正：
    /// 1. 将碰撞法线与平台自身的 Up 方向做混合
    /// 2. 过滤极端的 X 轴法线，强制使用平台 Up
    /// 3. 最终法线归一化后用于弹射方向计算
    /// </summary>
    private Vector2 CalcCorrectedNormal(Collision2D collision)
    {
        if (collision.contactCount == 0)
        {
            return transform.up;
        }

        Vector2 avgNormal = Vector2.zero;
        for (int i = 0; i < collision.contactCount; i++)
        {
            avgNormal += collision.GetContact(i).normal;
        }
        avgNormal /= collision.contactCount;

        // 极端法线过滤
        if (Mathf.Abs(avgNormal.x) > extremeNormalXThreshold)
        {
            Vector2 platformUp = transform.up;
            float preservedX = Mathf.Sign(avgNormal.x) * 0.3f;
            avgNormal = new Vector2(preservedX, platformUp.y).normalized;
        }
        else
        {
            Vector2 platformUp = transform.up;
            avgNormal = Vector2.Lerp(avgNormal, platformUp, normalBlendWithUp).normalized;
        }

        return avgNormal;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region ControllablePropBase 实现

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

        // 中止活跃的弹射协程
        if (activeLaunchCoroutine != null)
        {
            StopCoroutine(activeLaunchCoroutine);
            activeLaunchCoroutine = null;
        }

        transform.localScale = originalScale;
        tricksterOverride = false;
        isAnimating = false;
        isLaunching = false;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region Gizmos

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
            Gizmos.DrawRay(center + Vector3.up * halfSize.y, Vector3.up * 1.5f);
            Gizmos.DrawRay(center + Vector3.down * halfSize.y, Vector3.down * 1.0f);
            Gizmos.DrawRay(center + Vector3.left * halfSize.x, Vector3.left * 1.0f);
            Gizmos.DrawRay(center + Vector3.right * halfSize.x, Vector3.right * 1.0f);

            Gizmos.color = new Color(1, 0.5f, 0, 0.4f);
            Gizmos.DrawRay(center + new Vector3(-halfSize.x, halfSize.y, 0), new Vector3(-1, 1, 0).normalized * 1.2f);
            Gizmos.DrawRay(center + new Vector3(halfSize.x, halfSize.y, 0), new Vector3(1, 1, 0).normalized * 1.2f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(center, transform.up * 1.5f);
        }
    }

    #endregion
}
