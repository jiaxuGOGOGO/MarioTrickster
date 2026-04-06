using UnityEngine;
using System.Collections;

/// <summary>
/// 弹跳平台 - 关卡设计系统 · 平台类
/// 
/// 框架定位: ControllableLevelElement
/// 分类: Platform | 标签: Controllable, AffectsPhysics, Resettable
/// 
/// 核心机制（两段式弹射 + 法线方向弹飞 + 持续弹跳）:
///   玩家碰撞平台后，弹射方向以碰撞面法线为主：
///   1. 法线弹射：碰撞面法线决定弹射主方向（从上方落下→向上弹，从侧面碰→斜向弹）
///   2. 位置偏移：玩家相对平台中心的偏移提供微弱水平修正
///      - 落在平台左半边 → 微弱向左偏移
///      - 落在平台右半边 → 微弱向右偏移
///      - 落在平台正中间 → 纯法线方向
///   3. 持续弹跳：角色落回平台后会自动重新弹射（OnCollisionStay2D），
///      不会出现"弹一次就停"的问题
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
///
/// Session 36: 弹跳平台 Game Feel 增强（业界最佳实践）
///   参考来源:
///     - GameMaker Kitchen "10 Levels of Platformer Jumping": 弹跳平台 squash/stretch
///     - Dawnosaur "Improve Your Platformer Jump": 视觉反馈和冲击粒子
///     - "Secrets of Springs" (GDC): 阻尼简谐运动、过冲效果、体积守恒
///     - YouTube "Unity 2D Spring Like Mario": 直接速度覆盖保证一致性
///   改动:
///     - 增强 squash/stretch 动画参数（更明显的压缩和拉伸）
///     - 拉伸动画添加三段式过冲回弹（弹簧物理感）
///     - 弹射瞬间颜色闪白反馈（冲击感）
///     - comedyDelay 0.25→0.15（更紧凑的节奏）
///
/// Session 38: 弹跳方向修正 + 持续弹跳 + 方向自然化
///   问题修复:
///     1. 弹回方向不自然 → 改为碰撞面法线为主方向，位置偏移仅做微弱水平修正
///     2. 弹一次可能跳过去弹回来 → 废除 relativeVelocity 反射，直接用法线
///     3. 弹一次落地就不动了 → 新增 OnCollisionStay2D 持续弹跳检测
///   设计理念:
///     - 弹跳平台的行为应该像蹦床：踩上去就弹，方向始终以表面法线为主
///     - 参考 Super Mario Bros 弹簧：始终向上弹，不会弹到奇怪的方向
///     - 持续弹跳是弹跳平台的核心体验，不应该弹一次就停
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

    [Header("=== 弹射方向 (S38: 法线为主) ===")]
    [Tooltip("位置偏移对弹射方向的影响权重。0=纯法线弹射，1=纯位置偏移。推荐0.15-0.3")]
    [SerializeField, Range(0f, 1f)] private float positionInfluence = 0.2f;

    [Tooltip("最小向上分量（保证弹射有弧线感，不会贴地滑行）")]
    [SerializeField] private float minUpwardComponent = 0.5f;

    [Header("=== 碰撞法线修正 (Session 20) ===")]
    [Tooltip("法线与平台 Up 方向的混合权重。0=纯碰撞法线，1=纯平台Up。推荐0.5-0.7")]
    [SerializeField, Range(0f, 1f)] private float normalBlendWithUp = 0.6f;

    [Tooltip("X 轴法线绝对值超过此阈值时视为极端侧面碰撞，强制修正")]
    [SerializeField] private float extremeNormalXThreshold = 0.85f;

    [Header("=== 喜剧延迟（蓄力冻结期） ===")]
    [Tooltip("碰撞后的冻结时间（秒）。期间角色被冻结，Trickster 可操控。0=立即弹射")]
    [SerializeField] private float comedyDelay = 0.15f;

    [Header("=== 相机震动 ===")]
    [Tooltip("弹射时是否触发相机震动")]
    [SerializeField] private bool enableCameraShake = true;
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeMagnitude = 0.25f;

    [Header("=== 动画设置 (S36: 业界最佳实践增强) ===")]
    [Tooltip("挤压幅度（0-1），越大弹跳平台压缩越明显")]
    [SerializeField] private float squashAmount = 0.5f;
    [Tooltip("挤压动画时长（秒），配合 comedyDelay 使用")]
    [SerializeField] private float squashDuration = 0.1f;
    [Tooltip("弹起拉伸+回弹动画总时长（秒）")]
    [SerializeField] private float stretchDuration = 0.25f;
    [Tooltip("弹起拉伸过冲倍率，>1 产生弹簧过冲效果（Secrets of Springs: velocity nudge）")]
    [SerializeField] private float stretchOvershoot = 1.3f;

    [Header("=== 弹射视觉反馈 (S36: Game Feel Juice) ===")]
    [Tooltip("弹射瞬间平台颜色闪白，增强冲击感")]
    [SerializeField] private bool enableFlashOnBounce = true;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.08f;

    // 组件
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;  // S36: 用于弹射闪白反馈
    private Color originalColor;             // S36: 缓存原始颜色

    // S37: 视碰分离 — 视觉代理节点
    // [AI防坑警告] 所有 squash/stretch 动画必须操作 visualTransform.localScale，
    // 绝对不要操作根物体的 transform.localScale！
    [Header("S37: 视碰分离")]
    [Tooltip("视觉子节点的 Transform。为空时自动回退到自身 Transform。")]
    public Transform visualTransform;

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

    // S38: 持续弹跳冷却 — 防止 OnCollisionStay2D 在弹射动画未完成时重复触发
    // 弹射完成后需要一小段冷却时间，等角色真正离开或稳定接触后再允许下一次弹射
    private float launchCooldownTimer;
    private const float LAUNCH_COOLDOWN = 0.05f; // 极短冷却，仅防止同帧重复

    protected override void Awake()
    {
        propName = "弹跳平台";
        elementCategory = ElementCategory.Platform;
        elementTags = ElementTag.Controllable | ElementTag.AffectsPhysics | ElementTag.Resettable;
        elementDescription = "碰撞后弹飞的平台";

        base.Awake();

        boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = false;

        // S36: 缓存 SpriteRenderer 和原始颜色（用于弹射闪白反馈）
        // S37: 视碰分离 — SpriteRenderer 可能在子物体 Visual 上
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) originalColor = spriteRenderer.color;

        // S37: visualTransform 兼容回退
        if (visualTransform == null && spriteRenderer != null)
            visualTransform = spriteRenderer.transform;
        if (visualTransform == null)
            visualTransform = transform;

        originalScale = visualTransform.localScale;

        // 缓存 WaitForSeconds（P7: 避免协程中 GC 分配）
        cachedComedyWait = new WaitForSeconds(comedyDelay);
    }

    private void Start()
    {
        cachedCamera = FindObjectOfType<CameraController>();
    }

    private void Update()
    {
        // S38: 冷却计时器递减
        if (launchCooldownTimer > 0f)
        {
            launchCooldownTimer -= Time.deltaTime;
        }
    }

    // ─────────────────────────────────────────────────────
    #region 碰撞入口

    // [AI防坑警告] S38 重构：碰撞入口拆分为 Enter + Stay 双通道。
    // OnCollisionEnter2D 处理首次碰撞弹射。
    // OnCollisionStay2D 处理角色落回平台后的持续弹射（解决"弹一次就停"的问题）。
    // 两者共用 TryLaunch() 方法，由 isLaunching + launchCooldownTimer 双重防护防止重复触发。

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryLaunch(collision);
    }

    /// <summary>
    /// S38: 持续弹跳检测。角色弹起后落回同一平台时，
    /// OnCollisionEnter2D 可能不会重新触发（Unity 物理特性），
    /// 因此需要 OnCollisionStay2D 作为补充检测通道。
    /// 
    /// 触发条件：
    ///   - 当前没有弹射序列在进行（!isLaunching）
    ///   - 冷却已结束（launchCooldownTimer <= 0）
    ///   - 碰撞对象有 Rigidbody2D
    ///   - 碰撞对象正在下落或静止（velocity.y <= 0.1f），
    ///     防止角色正在上升时误触发
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        // 正在弹射中或冷却中，跳过
        if (isLaunching || launchCooldownTimer > 0f) return;

        Rigidbody2D targetRb = collision.gameObject.GetComponent<Rigidbody2D>();
        if (targetRb == null) return;

        // 只在角色下落或静止时触发（防止上升途中误弹）
        if (targetRb.velocity.y > 0.1f) return;

        TryLaunch(collision);
    }

    /// <summary>
    /// S38: 统一弹射入口。Enter 和 Stay 共用此方法。
    /// </summary>
    private void TryLaunch(Collision2D collision)
    {
        // 防止重复触发（已有弹射序列在进行中）
        if (isLaunching) return;

        // 冷却中
        if (launchCooldownTimer > 0f) return;

        Rigidbody2D targetRb = collision.gameObject.GetComponent<Rigidbody2D>();
        if (targetRb == null) return;

        MarioController mario = collision.gameObject.GetComponent<MarioController>();
        TricksterController trickster = collision.gameObject.GetComponent<TricksterController>();

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
            LaunchSequence(targetRb, mario, trickster, contactPos, correctedNormal));
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
        TricksterController trickster, Vector2 contactPos, Vector2 correctedNormal)
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
                visualTransform.localScale = Vector3.Lerp(originalScale, squashedScale, t / squashDuration);
                yield return null;
            }
            visualTransform.localScale = squashedScale;

            // 等待喜剧延迟（使用缓存的 WaitForSeconds）
            yield return cachedComedyWait;
        }

        // ══════════════════════════════════════════════════
        // 阶段2：发射
        // ══════════════════════════════════════════════════

        // S38: 计算弹射方向（法线为主，位置偏移为辅）
        Vector2 launchDir = CalcLaunchDirection(contactPos, correctedNormal);

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

        // S36: 弹射闪白反馈（Secrets of Springs: 视觉冲击感）
        if (enableFlashOnBounce && spriteRenderer != null)
        {
            spriteRenderer.color = flashColor;
        }

        // S36: 增强弹跳拉伸动画（内联，含过冲效果）
        // 业界参考: GameMaker Kitchen — 弹跳平台被踩时 image_yscale=0 然后 lerp 回弹
        // Secrets of Springs — velocity nudge 产生过冲效果
        {
            Vector3 currentScale = visualTransform.localScale;
            // 拉伸目标（平台变窄变高，体积守恒）
            Vector3 stretchedScale = new Vector3(
                originalScale.x * (1 - squashAmount * 0.6f),
                originalScale.y * (1 + squashAmount * stretchOvershoot),
                originalScale.z);

            float t = 0f;
            while (t < stretchDuration)
            {
                t += Time.deltaTime;
                float progress = t / stretchDuration;

                if (progress < 0.3f)
                {
                    // 第一段（0-30%）：快速拉伸到过冲位置
                    float p = progress / 0.3f;
                    visualTransform.localScale = Vector3.Lerp(currentScale, stretchedScale, p);
                }
                else if (progress < 0.6f)
                {
                    // 第二段（30-60%）：从过冲回弹到原始尺寸
                    float p = (progress - 0.3f) / 0.3f;
                    visualTransform.localScale = Vector3.Lerp(stretchedScale, originalScale, p);
                }
                else
                {
                    // 第三段（60-100%）：微小二次回弹（弹簧感）
                    float p = (progress - 0.6f) / 0.4f;
                    Vector3 microBounce = new Vector3(
                        originalScale.x * (1 + squashAmount * 0.15f),
                        originalScale.y * (1 - squashAmount * 0.15f),
                        originalScale.z);
                    if (p < 0.5f)
                        visualTransform.localScale = Vector3.Lerp(originalScale, microBounce, p * 2f);
                    else
                        visualTransform.localScale = Vector3.Lerp(microBounce, originalScale, (p - 0.5f) * 2f);
                }

                // S36: 闪白淡出
                if (enableFlashOnBounce && spriteRenderer != null && t < flashDuration)
                {
                    float flashProgress = t / flashDuration;
                    spriteRenderer.color = Color.Lerp(flashColor, originalColor, flashProgress);
                }
                else if (enableFlashOnBounce && spriteRenderer != null)
                {
                    spriteRenderer.color = originalColor;
                }

                yield return null;
            }
            visualTransform.localScale = originalScale;
            if (spriteRenderer != null) spriteRenderer.color = originalColor;
        }

        isLaunching = false;
        activeLaunchCoroutine = null;

        // S38: 启动冷却，防止协程刚结束就被 Stay 立即重新触发
        launchCooldownTimer = LAUNCH_COOLDOWN;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 弹射方向计算

    /// <summary>
    /// S38: 弹射方向计算重写 — 法线为主，位置偏移为辅。
    /// 
    /// 设计理念（参考 Super Mario Bros 弹簧 + Celeste 弹射台）：
    ///   弹跳平台的行为应该像蹦床，弹射方向由碰撞面法线决定：
    ///   - 从上方落下 → 法线朝上 → 向上弹（最常见场景）
    ///   - 从侧面碰撞 → 法线朝侧 → 斜向弹飞
    ///   - 位置偏移仅提供微弱水平修正，避免纯垂直弹跳的单调感
    /// 
    /// 废除旧逻辑：
    ///   - 不再使用 collision.relativeVelocity 做反射计算
    ///     （relativeVelocity 在 Unity 2D 中方向不稳定，导致弹飞方向反直觉）
    ///   - positionInfluence 从 0.6 降至 0.2，法线权重大幅提升
    /// </summary>
    private Vector2 CalcLaunchDirection(Vector2 contactPos, Vector2 correctedNormal)
    {
        if (tricksterOverride)
        {
            return tricksterDirection;
        }

        // ── 主方向：碰撞面修正法线 ──
        Vector2 baseDir = correctedNormal;

        // ── 辅助方向：位置偏移（微弱水平修正） ──
        Vector2 platformCenter = (Vector2)transform.position + boxCollider.offset;
        Vector2 offsetDir = (contactPos - platformCenter);

        // 归一化偏移（相对于平台半尺寸）
        Vector2 halfSize = boxCollider.size * 0.5f * (Vector2)transform.lossyScale;
        if (halfSize.x > 0.01f) offsetDir.x /= halfSize.x;
        if (halfSize.y > 0.01f) offsetDir.y /= halfSize.y;

        // 位置偏移只取水平分量，垂直方向完全由法线决定
        Vector2 horizontalOffset = new Vector2(offsetDir.x, 0f);

        // 如果偏移太小（正中间），不加水平修正，纯法线方向
        if (Mathf.Abs(horizontalOffset.x) < 0.1f)
        {
            horizontalOffset = Vector2.zero;
        }
        else
        {
            horizontalOffset = horizontalOffset.normalized;
        }

        // ── 混合：法线为主 + 微弱水平偏移 ──
        Vector2 launchDir = baseDir + horizontalOffset * positionInfluence;
        launchDir = launchDir.normalized;

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
    /// 
    /// S38: normalBlendWithUp 从 0.4 提升到 0.6，
    /// 使法线更偏向平台 Up 方向，弹射更自然。
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

        visualTransform.localScale = originalScale;
        tricksterOverride = false;
        isAnimating = false;
        isLaunching = false;
        launchCooldownTimer = 0f;

        // S36: 恢复原始颜色
        if (spriteRenderer != null) spriteRenderer.color = originalColor;
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

            // S38: 只绘制向上的弹射方向指示（法线为主的设计理念）
            Gizmos.color = new Color(1, 0.5f, 0, 0.8f);
            Gizmos.DrawRay(center + Vector3.up * halfSize.y, Vector3.up * 2f);

            // 微弱侧向指示
            Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
            Gizmos.DrawRay(center + new Vector3(-halfSize.x, halfSize.y, 0), new Vector3(-0.3f, 1, 0).normalized * 1.5f);
            Gizmos.DrawRay(center + new Vector3(halfSize.x, halfSize.y, 0), new Vector3(0.3f, 1, 0).normalized * 1.5f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(center, transform.up * 1.5f);
        }
    }

    #endregion
}
