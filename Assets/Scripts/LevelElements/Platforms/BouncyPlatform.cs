using UnityEngine;
using System.Collections;

/// <summary>
/// 弹跳平台 - 关卡设计系统 · 平台类
/// 
/// 框架定位: ControllableLevelElement
/// 分类: Platform | 标签: Controllable, AffectsPhysics, Resettable
/// 
/// 核心机制（方案 C: 按键驱动的互动式大跳 Skill-Based Bounce）:
///   玩家从上方落到平台后，弹射方向以碰撞面法线为主：
///   1. 法线弹射：碰撞面法线决定弹射主方向（从上方落下→向上弹）
///   2. 位置偏移：玩家相对平台中心的偏移提供微弱水平修正
///   3. 按键驱动大跳：
///      - 保底标准弹跳 (Normal Bounce, 1.0x)：玩家什么都不按，获得绝对固定的标准弹射高度
///      - 主动蓄力大跳 (Super Bounce, 1.4x)：玩家在 comedyDelay 冻结期内按住跳跃键(Space)
///        则获得额外弹射力，把被动等待变成炫技 QTE 微操
///   
/// Session 22 重构：两段式弹射协程
///   整个弹射流程由 LaunchSequence 协程统一管理：
///   
///   阶段1 - 蓄力冻结期（Comedy Delay）:
///     碰撞瞬间 → mario.PrepareBounce() 冻结角色（Kinematic Freeze）
///     → 挤压动画 + WaitForSeconds(comedyDelay)
///     → 期间 Trickster 可按 L 操控修改弹力
///     → 期间 Mario 可按住 Space 蓄力大跳
///   
///   阶段2 - 发射:
///     延迟结束 → 微抬角色坐标脱离碰撞重叠区
///     → 计算最终弹力（含 Trickster 操控加成 / 玩家大跳加成）
///     → mario.ExecuteBounce(finalVelocity) 注入绝对速度
///     → MarioController 进入 _isBouncing 飞行期
///     → 落地或碰墙自动解除，恢复正常控制
///   
/// Session 39 重构（方案 C）：
///   问题修复:
///     1. "先高后矮" Bug → 彻底删除 OnCollisionStay2D，改用状态机锁
///        enum State { Idle, Bouncing, Cooldown }，只有 Idle 状态下 Enter 才受理
///     2. 蓄力冻结期物理引擎偷偷施加排斥力 → Kinematic Freeze 熔断物理
///        PrepareBounce 时设置 rb.isKinematic = true，发射时恢复
///     3. 碰撞体重叠导致发射后被吸回 → 发射瞬间微抬角色坐标 0.05f
///     4. 侧面蹭到也被弹飞 → 严格法线检测，只有从上方落下才触发
///   新增:
///     - 按键驱动大跳（Super Bounce）：冻结期按住 Space 获得 1.4x 弹射力
///     - 喜剧效果纯走视觉层：所有 squash/stretch 操作 visualTransform.localScale
///       BoxCollider2D 尺寸永远锁定不变
///   
///   Trickster操控: 在蓄力冻结期按 L 键改变弹射方向或力度
/// 
/// Inspector 可调参数:
///   - bounceForce: 基础弹射力
///   - bounceForceMultiplier: 弹射力倍率
///   - superBounceMultiplier: 按住跳跃键时的大跳倍率（默认 1.4x）
///   - positionInfluence: 位置偏移对弹射方向的影响权重
///   - comedyDelay: 碰撞后冻结时间（蓄力期）
/// 
/// 扩展/删除指南: 删除此文件不影响其他脚本
/// Session 15: 关卡设计系统新增
/// Session 19: 反方向弹飞 + 位置偏移 + KnockbackStun防覆盖 + 表现层震动信号
/// Session 20: 碰撞法线修正 + BounceStun 抛物线保留
/// Session 21: SetFrameVelocity 绝对速度注入 + maxSpeed 截断跳过
/// Session 22: 两段式弹射协程重构
/// Session 36: 弹跳平台 Game Feel 增强（业界最佳实践）
/// Session 38: 弹跳方向修正 + 持续弹跳 + 方向自然化
/// Session 39: 方案 C 重构 — 按键驱动大跳 + 状态机锁 + Kinematic Freeze
///   - 彻底删除 OnCollisionStay2D，用状态机锁替代
///   - Kinematic Freeze 熔断蓄力冻结期物理干扰
///   - 微抬坐标脱离碰撞重叠区
///   - 严格法线检测（只有从上方落下才触发）
///   - 按键驱动大跳（Super Bounce, 1.4x）
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

    [Header("=== S39: 按键驱动大跳 (Super Bounce) ===")]
    [Tooltip("玩家在蓄力冻结期按住跳跃键时的弹射力倍率。1.0=无加成，1.4=推荐值")]
    [SerializeField] private float superBounceMultiplier = 1.4f;

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

    [Header("=== S39: 严格法线检测 ===")]
    [Tooltip("碰撞法线 Y 分量必须小于此阈值才视为从上方落下（注意：法线指向碰撞者，平台视角法线朝上对应 Mario 视角法线朝下）")]
    [SerializeField] private float topHitNormalThreshold = -0.5f;

    [Header("=== 喜剧延迟（蓄力冻结期） ===")]
    [Tooltip("碰撞后的冻结时间（秒）。期间角色被冻结，Trickster 可操控，Mario 可蓄力大跳。0=立即弹射")]
    [SerializeField] private float comedyDelay = 0.15f;

    [Header("=== S39: 状态机冷却 ===")]
    [Tooltip("弹射完成后的冷却时间（秒），期间不接受新的碰撞触发")]
    [SerializeField] private float bounceCooldownDuration = 0.15f;

    [Header("=== 表现层事件：震动 ===")]
    [Tooltip("弹射时是否发送震动表现信号")]
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

    [Header("=== 表现层事件：弹射闪光 (S36: Game Feel Juice) ===")]
    [Tooltip("弹射瞬间是否发送平台闪光表现信号，增强冲击感")]
    [SerializeField] private bool enableFlashOnBounce = true;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.08f;

    // 组件
    private BoxCollider2D boxCollider;
    // S38: spriteRenderer 和 originalColor 已由父类 ControllablePropBase 声明为 protected 并在 base.Awake() 中初始化。
    // 子类不再重复声明，直接使用父类字段，消除 Unity 序列化重复警告。

    // S37: 视碰分离 — 视觉代理节点
    // [AI防坑警告] 所有 squash/stretch 动画必须操作 visualTransform.localScale，
    // 绝对不要操作根物体的 transform.localScale！
    // BoxCollider2D 尺寸必须死死锁定，喜剧效果纯走视觉层。
    [Header("S37: 视碰分离")]
    [Tooltip("视觉子节点的 Transform。为空时自动回退到自身 Transform。")]
    public Transform visualTransform;

    // [AI防坑警告] 弹射全流程由 LaunchSequence 协程统一管理，不要拆回 Update 手动计时器！
    // 协程时序：OnCollisionEnter2D → PrepareBounce(冻结) → WaitForSeconds → ExecuteBounce(发射)
    // 不要用 AddForce 替代 ExecuteBounce — AddForce 会被 MarioController._frameVelocity 写入覆盖。
    // 不要用 rb.velocity = xxx 替代 ExecuteBounce — 同理，下一帧 FixedUpdate 会读回并截断。

    // ═══════════════════════════════════════════════════════
    // S39: 状态机锁 — 彻底替代 OnCollisionStay2D + hasLeftPlatform + launchCooldownTimer
    // [AI防坑警告] 只有 Idle 状态下 OnCollisionEnter2D 才受理弹射。
    // Bouncing 和 Cooldown 期间一律 return，彻底断绝连环鬼畜弹。
    // 不要重新引入 OnCollisionStay2D！那是"先高后矮" Bug 的根源。
    // ═══════════════════════════════════════════════════════
    private enum BounceState { Idle, Bouncing, Cooldown }
    private BounceState _state = BounceState.Idle;

    // 状态
    private Vector3 originalScale;

    // 当前活跃的弹射协程（用于 OnLevelReset 中止）
    private Coroutine activeLaunchCoroutine;

    // Trickster覆盖（在蓄力冻结期内可修改）
    private bool tricksterOverride;
    private Vector2 tricksterDirection;
    private float tricksterForceMult = 1f;

    // P1-P7: 缓存 WaitForSeconds 实例，避免协程中每次 new 产生 GC
    private WaitForSeconds cachedComedyWait;
    private WaitForSeconds cachedCooldownWait;

    protected override void Awake()
    {
        propName = "弹跳平台";
        elementCategory = ElementCategory.Platform;
        elementTags = ElementTag.Controllable | ElementTag.AffectsPhysics | ElementTag.Resettable;
        elementDescription = "碰撞后弹飞的平台（按住跳跃键可蓄力大跳）";

        base.Awake();

        boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = false;

        // S38: spriteRenderer 和 originalColor 已由 base.Awake() 初始化（ControllablePropBase.Awake 使用 GetComponentInChildren）
        // 此处不再重复赋值，直接使用父类已缓存的字段

        // S37: visualTransform 兼容回退
        if (visualTransform == null && spriteRenderer != null)
            visualTransform = spriteRenderer.transform;
        if (visualTransform == null)
            visualTransform = transform;

        originalScale = visualTransform.localScale;

        // 缓存 WaitForSeconds（P7: 避免协程中 GC 分配）
        cachedComedyWait = new WaitForSeconds(comedyDelay);
        cachedCooldownWait = new WaitForSeconds(bounceCooldownDuration);
    }

    // ─────────────────────────────────────────────────────
    #region 碰撞入口

    // [AI防坑警告] S39 重构：彻底删除 OnCollisionStay2D 和 OnCollisionExit2D。
    // 只保留 OnCollisionEnter2D 作为唯一碰撞入口。
    // 状态机锁（Idle/Bouncing/Cooldown）替代了之前的 isLaunching + hasLeftPlatform + launchCooldownTimer。
    // 不要重新引入 Stay2D！那是"先高后矮" Bug 和连环鬼畜弹的根源。

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // S39: 状态机锁 — 只有 Idle 状态才受理
        if (_state != BounceState.Idle) return;

        Rigidbody2D targetRb = collision.gameObject.GetComponent<Rigidbody2D>();
        if (targetRb == null) return;

        // S39: 严格法线检测 — 只有从上方落下才触发弹射
        // 项目约定（与 CollapsingPlatform、BouncingEnemy 一致）：
        //   在静态碰撞体（无 Rigidbody2D）的 OnCollisionEnter2D 中，
        //   contact.normal 指向碰撞体自身（即从 Mario 指向平台）。
        //   Mario 从上方落下时，法线朝下：normal.y < -0.5。
        //   侧面碰撞时 |normal.y| 较小，不满足阈值。
        if (collision.contactCount > 0)
        {
            Vector2 avgNormal = Vector2.zero;
            for (int i = 0; i < collision.contactCount; i++)
            {
                avgNormal += collision.GetContact(i).normal;
            }
            avgNormal /= collision.contactCount;

            // normal.y < -0.5 表示 Mario 从上方落下（与 CollapsingPlatform/BouncingEnemy 一致）
            if (avgNormal.y > -0.5f)
            {
                // 不是从上方落下，忽略（防止侧面蹭到也被弹飞）
                return;
            }
        }

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
    #region 两段式弹射协程 (Session 22 / S39 重构)

    // [AI防坑警告] 这是弹射的核心协程，时序严格不可打乱！
    // 必须先 PrepareBounce(Kinematic冻结) → 等待 comedyDelay → 微抬坐标 → ExecuteBounce(发射)。
    // 如果跳过冻结直接发射，角色当帧可能还有重力累积的旧速度，弹射轨道会被污染。
    // cachedComedyWait 在 Awake 中缓存，协程中禁止 new WaitForSeconds（P1-P7 GC 规范）。
    /// <summary>
    /// 两段式弹射协程：统一管理蓄力冻结 → 发射的完整时序。
    /// 
    /// S39 重构要点：
    ///   - 状态机锁：进入时 _state = Bouncing，发射后 _state = Cooldown，冷却结束 _state = Idle
    ///   - Kinematic Freeze：冻结期 rb.isKinematic = true，熔断物理引擎的穿透恢复力
    ///   - 微抬坐标：发射瞬间 rb.position += Vector2.up * 0.05f，脱离碰撞重叠区
    ///   - 按键驱动大跳：冻结期结束时检查 mario.IsJumpHeld，决定是否施加 superBounceMultiplier
    /// 
    /// P1-P7 合规:
    ///   - WaitForSeconds 使用 Awake 中缓存的实例（避免 GC）
    ///   - 协程中无 new 分配（除 Debug.Log 字符串插值，仅调试用）
    ///   - 动画协程使用 yield return null（帧等待无 GC）
    /// </summary>
    private IEnumerator LaunchSequence(Rigidbody2D targetRb, MarioController mario,
        TricksterController trickster, Vector2 contactPos, Vector2 correctedNormal)
    {
        _state = BounceState.Bouncing;

        // ══════════════════════════════════════════════════
        // 阶段1：蓄力冻结（Kinematic Freeze）
        // ══════════════════════════════════════════════════

        // S39: Kinematic Freeze — 熔断物理引擎的干预
        // 单单把速度设为零是不够的，重力和穿透恢复力依然在底层偷偷结算。
        // 设置 isKinematic = true 彻底冻结 Rigidbody，让物理引擎无法施加任何力。
        if (mario != null)
        {
            mario.PrepareBounce();
        }
        else
        {
            // 非 Mario 角色（Trickster 等）：直接冻结 rb
            targetRb.velocity = Vector2.zero;
            targetRb.isKinematic = true;
        }

        if (trickster != null)
        {
            trickster.ApplyKnockbackStun(comedyDelay + 0.5f);
        }

        // 挤压动画（内联，避免嵌套协程）
        // [AI防坑警告] 所有动画操作 visualTransform.localScale，不操作根物体！
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

        // S39: 按键驱动大跳 — 冻结期结束时检查玩家是否按住跳跃键
        // 如果按住了 Space，施加 superBounceMultiplier（默认 1.4x）
        bool isSuperBounce = false;
        if (mario != null && mario.IsJumpHeld && !tricksterOverride)
        {
            force *= superBounceMultiplier;
            isSuperBounce = true;
        }

        force = Mathf.Clamp(force, minBounceForce, maxBounceForce);

        // S38 修正：固定垂直分量 + 独立水平分量
        // 垂直分量始终固定为 force，水平分量由方向计算独立提供。
        // 这样无论从哪个角度碰撞，弹跳高度始终一致。
        Vector2 launchVelocity;
        if (tricksterOverride)
        {
            // Trickster 操控时保留完整方向控制
            launchVelocity = launchDir * force;
        }
        else
        {
            // 正常弹射：垂直分量固定，水平分量由方向计算提供
            float verticalForce = force;  // 固定垂直弹射力
            float horizontalForce = launchDir.x * force * 0.3f;  // 微弱水平偏移
            launchVelocity = new Vector2(horizontalForce, verticalForce);
        }

        // S39: 微抬坐标 — 发射瞬间将角色 Y 坐标微抬 0.05f
        // 瞬间脱离碰撞体重叠区，拒绝物理引擎的排斥力干扰
        targetRb.position += Vector2.up * 0.05f;

        // 执行弹射
        if (mario != null)
        {
            mario.ExecuteBounce(launchVelocity);
        }
        else
        {
            // 非 Mario 角色：恢复物理后直接设置 rb.velocity
            targetRb.isKinematic = false;
            targetRb.velocity = launchVelocity;
        }

        // 表现层信号（大跳时震动更强）；核心速度注入已完成，事件不得反向影响物理状态。
        float shakeMultiplier = isSuperBounce ? 1.5f : 1f;
        GameplayEventBus.SendBouncyPlatformLaunched(
            gameObject,
            targetRb.gameObject,
            transform.position,
            launchVelocity,
            flashDuration: enableFlashOnBounce ? flashDuration : 0f,
            flashColor: flashColor,
            shakeDuration: enableCameraShake ? shakeDuration * shakeMultiplier : 0f,
            shakeMagnitude: enableCameraShake ? shakeMagnitude * shakeMultiplier : 0f);

        Debug.Log($"[BouncyPlatform] 弹飞 {targetRb.gameObject.name}, 方向={launchDir}, 速度={launchVelocity}, 力度={force}, SuperBounce={isSuperBounce}, Trickster操控={tricksterOverride}");

        // S36: 增强弹跳拉伸动画（内联，含过冲效果）
        // [AI防坑警告] 所有动画操作 visualTransform.localScale，不操作根物体！
        // 业界参考: GameMaker Kitchen — 弹跳平台被踩时 image_yscale=0 然后 lerp 回弹
        // Secrets of Springs — velocity nudge 产生过冲效果
        {
            Vector3 currentScale = visualTransform.localScale;
            // 拉伸目标（平台变窄变高，体积守恒）
            // S39: 大跳时拉伸更夸张（视觉反馈区分普通弹跳和大跳）
            float stretchMult = isSuperBounce ? 1.3f : 1f;
            Vector3 stretchedScale = new Vector3(
                originalScale.x * (1 - squashAmount * 0.6f * stretchMult),
                originalScale.y * (1 + squashAmount * stretchOvershoot * stretchMult),
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

                yield return null;
            }
            visualTransform.localScale = originalScale;
        }

        // S39: 状态机 → Cooldown（冷却期，防止动画刚结束就被再次触发）
        _state = BounceState.Cooldown;
        activeLaunchCoroutine = null;

        yield return cachedCooldownWait;

        // S39: 冷却结束 → Idle（可以接受下一次碰撞）
        _state = BounceState.Idle;
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
    ///   - 位置偏移仅提供微弱水平修正，避免纯垂直弹跳的单调感
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
        _state = BounceState.Idle;

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
