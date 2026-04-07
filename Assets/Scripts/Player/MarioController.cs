using UnityEngine;

/// <summary>
/// Mario 玩家控制器
///
/// 架构参考: Ultimate-2D-Controller (Tarodev, MIT)
///   https://github.com/Matthew-J-Spencer/Ultimate-2D-Controller
/// 手感参考: zigurous/unity-super-mario-tutorial
///   https://github.com/zigurous/unity-super-mario-tutorial
///
/// 核心设计:
///   所有速度变化（移动、重力、跳跃）在一帧内累积到 _frameVelocity，
///   最后一次性写入 rb.velocity，避免多处赋值互相覆盖。
///   重力由代码自管，不依赖 Unity gravityScale，可精确控制手感。
///
///   平台跟随：移动平台每帧调用 SetPlatformVelocity() 注入平台速度，
///   FixedUpdate 最后将平台速度叠加到 _frameVelocity 再写入 rb。
///   不使用 SetParent（避免 Transform 层级与 Rigidbody2D 世界坐标冲突）。
///
/// Session 16 更新:
///   B023 - 添加击退 stun 机制：受伤时暂停控制器速度覆盖，让 AddForce 击退力生效
///
/// Session 20 更新:
///   - 新增 BounceStun 状态（区别于 KnockbackStun）
///   - BounceStun 不完全禁止横向控制，而是大幅降低 acceleration 和 deceleration
///   - 让物理引擎先接管弹跳抛物线轨迹，之后再恢复玩家控制
///   - 参考 Celeste 弹簧 / Sonic 弹簧最佳实践：弹跳后短暂降低操控力
///
/// Session 21 更新:
///   - 根因修复：BounceStun 期间 HandleDirection 的 MoveTowards 目标值
///     不再被 maxSpeed 截断，允许弹射速度超过 maxSpeed 自由飞行
///   - 新增 SetFrameVelocity(Vector2) 公开方法
///
/// Session 22 重构：两段式弹射状态机
///   - 废除 bounceStunTimer 浮点计时器，改用布尔状态机：
///     _isPreparingBounce（蓄力冻结期）+ _isBouncing（抛物线飞行期）
///   - PrepareBounce()：碰到平台时调用，冻结角色（忽略输入、零速度、零重力）
///   - ExecuteBounce(Vector2)：延迟结束后调用，注入绝对弹射速度，进入飞行期
///   - HandleDirection 动能保留：飞行期超速时引入微弱空气阻力自然衰减，
///     允许微弱空中转向但不突破当前超速上限
///   - 落地或碰墙自动解除 _isBouncing，恢复正常移动逻辑
///
/// Session 36: 弹跳平台 Game Feel 增强
///   参考来源:
///     - GameMaker Kitchen "10 Levels of Platformer Jumping"
///     - Dawnosaur "Improve Your Platformer Jump"
///     - "Secrets of Springs" (GDC): 阻尼简谐运动、过冲效果
///   改动:
///     - 弹射飞行期也启用半重力顶点（不需要 jumpHeld）
///     - 角色 Squash & Stretch 形变：蓄力压扁、弹射拉伸、落地压扁
///     - 视碰分离：通过 visualTransform.localScale 实现（S37 重构）
///
/// Session 39 重构（方案 C: 按键驱动大跳 Skill-Based Bounce）:
///   - 新增 IsJumpHeld 公共只读属性，供 BouncyPlatform 在 comedyDelay 结束时
///     查询玩家是否按住跳跃键，决定是否施加 superBounceMultiplier
///   - PrepareBounce() 增加 Kinematic Freeze：rb.isKinematic = true
///     彻底熔断物理引擎的穿透恢复力和重力干扰
///   - ExecuteBounce() 增加：
///     1. 恢复 rb.isKinematic = false
///     2. 微抬坐标 rb.position += Vector2.up * 0.05f 脱离碰撞重叠区
///     3. 绝对速度覆写（拒绝 AddForce）
///   - 蓄力冻结期不再吞掉 jumpHeld 状态（只吞 jumpPressedThisFrame），
///     让 InputManager 持续维护 jumpHeld 的真实状态，
///     BouncyPlatform 可在延迟结束时准确读取
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[SelectionBase] // S37 视碰分离: 确保框选时选中 Root 而非 Visual 子节点
public class MarioController : MonoBehaviour
{
    // ── S52: PhysicsConfigSO 实时手感面板 ──────────────────
    // [AI防坑警告] physicsConfig 是可选的 ScriptableObject 引用。
    // 当赋值时，所有手感参数优先从 SO 读取（支持 PlayMode 实时调参）。
    // 当为 null 时，回退到下方的本地 [SerializeField] 值（零行为变化）。
    // 绝对不要删除下方的本地字段！它们是 SO 为空时的兜底默认值。
    [Header("S52: 实时手感面板 (可选)")]
    [Tooltip("拖入 PhysicsConfigSO 资产即可启用 PlayMode 实时调参。为空时使用下方本地值。")]
    [SerializeField] private PhysicsConfigSO physicsConfig;

    // ── 移动 ──────────────────────────────────────────────────────────────────────
    [Header("移动")]
    [Tooltip("最大水平速度")]
    [SerializeField] private float maxSpeed = 9f;
    [Tooltip("地面加速度（越大起步越快）")]
    [SerializeField] private float acceleration = 160f;
    [Tooltip("地面减速度（越大停止越果断，调大可消除打滑感）")]
    [SerializeField] private float groundDeceleration = 200f;
    [Tooltip("空中减速度（松开输入后，建议保持较小以保留空中滑行感）")]
    [SerializeField] private float airDeceleration = 30f;
    [Tooltip("落地时施加的微小向下力，防止在斜面上抖动")]
    [SerializeField] private float groundingForce = -1.5f;

    // ── 跳跃 ──────────────────────────────────────────────
    [Header("跳跃")]
    [Tooltip("跳跃初速度")]
    [SerializeField] private float jumpPower = 20f;
    [Tooltip("最大下落速度")]
    [SerializeField] private float maxFallSpeed = 40f;
    [Tooltip("下落重力加速度（越大越快坠落）")]
    [SerializeField] private float fallAcceleration = 80f;
    [Tooltip("提前松开跳跃键时的重力倍率（越大跳跃弧度越短）")]
    [SerializeField] private float jumpEndEarlyGravityModifier = 3f;
    [Tooltip("离开平台边缘后仍可跳跃的宽限时间（秒）")]
    [SerializeField] private float coyoteTime = 0.15f;
    [Tooltip("落地前提前按跳跃的缓冲时间（秒）")]
    [SerializeField] private float jumpBuffer = 0.2f;

    // ── 地面检测 ──────────────────────────────────────────
    [Header("地面检测")]
    [Tooltip("地面所在的 Layer（必须设置，否则无法跳跃）")]
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("地面检测射线长度")]
    [SerializeField] private float grounderDistance = 0.05f;

    // ── 击退 (Session 16) ────────────────────────────────
    [Header("击退")]
    [Tooltip("受击后控制器暂停时长（秒），让击退力生效")]
    [SerializeField] private float knockbackStunDuration = 0.25f;

    // ── 半重力跳跃顶点 (Session 32: Celeste 风格增强) ────────
    [Header("半重力跳跃顶点 (Session 32)")]
    [Tooltip("跳跃顶点附近的速度阈值，|velocity.y| < 此值时视为顶点区")]
    [SerializeField] private float apexThreshold = 2.0f;
    [Tooltip("顶点区域重力倍率（0.5=半重力，Celeste 风格）")]
    [SerializeField] private float apexGravityMultiplier = 0.5f;

    // ── 弹射动能保留 (Session 22) ────────────────────────
    [Header("弹射动能保留 (Session 22)")]
    [Tooltip("抛物线飞行期空气阻力（无输入时 X 轴速度每秒衰减量）")]
    [SerializeField] private float airFriction = 8f;
    [Tooltip("抛物线飞行期空中转向加速度（有输入时的微弱偏转力）")]
    [SerializeField] private float bounceAirAcceleration = 12f;

    // ── 组件 ──────────────────────────────────────────────
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    // ── S37: 视碰分离 — 视觉代理节点 ──────────────────────
    // [AI防坑警告] visualTransform 是所有形变动画（Squash & Stretch）的唯一操作目标。
    // 绝对不要对根物体的 transform.localScale 做形变！根物体 localScale 必须永远是 (1,1,1)。
    // 朝向翻转统一使用 spriteRenderer.flipX，绝不修改 localScale.x = -1。
    [Header("S37: 视碰分离")]
    [Tooltip("视觉子节点的 Transform（Squash/Stretch 形变动画操作此节点）。为空时自动回退到自身 Transform。")]
    public Transform visualTransform;

    // ── 输入（由 InputManager 每帧写入）─────────────────────
    private Vector2 moveInput;
    private bool jumpPressedThisFrame;
    private bool jumpHeld;

    // ── 帧速度（本帧所有速度变化的累积量）───────────────────
    private Vector2 _frameVelocity;

    // ── 平台速度注入（由 MovingPlatform / ControllablePlatform 每帧写入）──
    private Vector2 _platformVelocity;
    private Vector2 _lastPlatformVelocity;
    private bool _onPlatform;

    // ── 地面状态 ──────────────────────────────────────────
    private bool _grounded;
    private float _timeLeftGrounded = float.MinValue;
    private float _time;

    // ── 跳跃状态 ──────────────────────────────────────────
    private bool _jumpToConsume;
    private bool _bufferedJumpUsable;
    private bool _endedJumpEarly;
    private bool _coyoteUsable;
    private float _timeJumpWasPressed;

    private bool HasBufferedJump => _bufferedJumpUsable && _time < _timeJumpWasPressed + JumpBuffer;
    private bool CanUseCoyote    => _coyoteUsable && !_grounded && _time < _timeLeftGrounded + CoyoteTime;

    // ── 击退 stun 状态 (Session 16: B023) ─────────────────
    private bool _isKnockbackStunned;
    private float _knockbackStunTimer;

    // ── Session 22: 两段式弹射状态机 ──────────────────────
    // [AI防坑警告] 这是两段式弹射的核心状态机，绝对不要改回 bounceStunTimer 单计时器方案！
    // _isPreparingBounce（蓄力冻结）和 _isBouncing（抛物线飞行）必须是两个独立的 bool。
    // 如果合并成一个 timer，HandleDirection 的 maxSpeed 截断会在冻结期就生效，
    // 导致弹射后水平速度被截断至 maxSpeed，抛物线变成垂直下落的直角三角形。
    // 完整弹射流程：碰撞→PrepareBounce()冻结→comedyDelay→ExecuteBounce()飞行→落地/碰墙解除
    // 阶段1: 蓄力冻结期 — 碰到弹跳平台后，角色完全冻结（零速度、忽略输入）
    // 阶段2: 抛物线飞行期 — 延迟结束后注入弹射速度，允许超速惯性飞行
    private bool _isPreparingBounce;  // 蓄力冻结中
    private bool _isBouncing;         // 抛物线飞行中

    // ── S36: 角色弹射形变 (Squash & Stretch) ─────────────
    // 业界参考: GameMaker Kitchen —10 Levels of Platformer Jumping"
    //   弹跳时角色拉伸（Y 放大、X 缩小），落地时压扁（Y 缩小、X 放大）
    // Dawnosaur "Improve Your Platformer Jump" — visual juice
    // 实现：通过 visualTransform.localScale 实现（S37 视碰分离），不影响碰撞体
    private bool _bounceSquashActive;       // 是否正在播放形变动画
    private float _bounceSquashTimer;       // 形变动画计时器
    private const float BounceStretchDuration = 0.3f;  // 形变动画总时长
    private const float BounceStretchY = 1.25f;        // 拉伸 Y 倍率
    private const float BounceStretchX = 0.8f;         // 拉伸 X 倍率（体积守恒）
    private const float LandSquashY = 0.75f;           // 落地压扁 Y
    private const float LandSquashX = 1.2f;            // 落地压扁 X
    private const float LandSquashDuration = 0.15f;    // 落地压扁时长
    private bool _landSquashActive;
    private float _landSquashTimer;

    // ── 朝向 ──────────────────────────────────────────────────
    private bool isFacingRight = true;

    // ── 公共属性 ──────────────────────────────────────────
    public bool IsGrounded    => _grounded;
    public bool IsMoving      => Mathf.Abs(_frameVelocity.x) > 0.1f;
    public bool IsFacingRight => isFacingRight;
    public Vector2 Velocity   => _frameVelocity;

    // S39: 暴露跳跃键按住状态，供 BouncyPlatform 在 comedyDelay 结束时查询
    // 用于按键驱动大跳（Super Bounce）：冻结期按住 Space → 1.4x 弹射力
    // [AI防坑警告] 此属性只读，jumpHeld 的写入权归 InputManager（通过 OnJumpPressed/OnJumpReleased）。
    // 绝对不要在 PrepareBounce 中清除 jumpHeld！蓄力冻结期必须保留 jumpHeld 的真实状态。
    public bool IsJumpHeld => jumpHeld;

    // ── S52: SO 读取属性（每帧从 SO 读取最新值，SO 为 null 时回退到本地字段）──
    // [AI防坑警告] 这些属性是所有物理计算的唯一参数来源。
    // 不要直接读取本地字段（maxSpeed, jumpPower 等），必须通过这些属性读取。
    // 这样当 physicsConfig 被赋值时，PlayMode 实时调参才能生效。
    private float MaxSpeed => physicsConfig != null ? physicsConfig.maxSpeed : maxSpeed;
    private float Acceleration => physicsConfig != null ? physicsConfig.acceleration : acceleration;
    private float GroundDeceleration => physicsConfig != null ? physicsConfig.groundDeceleration : groundDeceleration;
    private float AirDeceleration => physicsConfig != null ? physicsConfig.airDeceleration : airDeceleration;
    private float GroundingForce => physicsConfig != null ? physicsConfig.groundingForce : groundingForce;
    private float JumpPower => physicsConfig != null ? physicsConfig.jumpPower : jumpPower;
    private float MaxFallSpeed => physicsConfig != null ? physicsConfig.maxFallSpeed : maxFallSpeed;
    private float FallAcceleration => physicsConfig != null ? physicsConfig.fallAcceleration : fallAcceleration;
    private float JumpEndEarlyGravityModifier => physicsConfig != null ? physicsConfig.jumpEndEarlyGravityModifier : jumpEndEarlyGravityModifier;
    private float CoyoteTime => physicsConfig != null ? physicsConfig.coyoteTime : coyoteTime;
    private float JumpBuffer => physicsConfig != null ? physicsConfig.jumpBuffer : jumpBuffer;
    private float ApexThreshold => physicsConfig != null ? physicsConfig.apexThreshold : apexThreshold;
    private float ApexGravityMultiplier => physicsConfig != null ? physicsConfig.apexGravityMultiplier : apexGravityMultiplier;
    private float AirFriction => physicsConfig != null ? physicsConfig.airFriction : airFriction;
    private float BounceAirAcceleration => physicsConfig != null ? physicsConfig.bounceAirAcceleration : bounceAirAcceleration;
    private float KnockbackStunDuration => physicsConfig != null ? physicsConfig.knockbackStunDuration : knockbackStunDuration;
    private float GrounderDistance => physicsConfig != null ? physicsConfig.grounderDistance : grounderDistance;

    // ── 事件 ──────────────────────────────────────────────
    public System.Action OnJump;
    public System.Action<bool, float> OnGroundedChanged;
    public System.Action OnDeath;

    // ─────────────────────────────────────────────────────
    #region 初始化

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        // S37: 视碰分离 — SpriteRenderer 可能在子物体 Visual 上
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();

        // S37: visualTransform 兼容回退 — 未赋值时使用 spriteRenderer 所在节点
        if (visualTransform == null && spriteRenderer != null)
            visualTransform = spriteRenderer.transform;
        if (visualTransform == null)
            visualTransform = transform;

        // S52: 如果 Inspector 未手动赋值 physicsConfig，尝试从 Resources 自动加载
        if (physicsConfig == null)
        {
            physicsConfig = Resources.Load<PhysicsConfigSO>("PhysicsConfig");
        }

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (boxCollider.sharedMaterial == null)
        {
            boxCollider.sharedMaterial = new PhysicsMaterial2D("ZeroFriction")
                { friction = 0f, bounciness = 0f };
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region Update / FixedUpdate

    private void Update()
    {
        _time += Time.deltaTime;

        // 击退 stun 倒计时
        if (_isKnockbackStunned)
        {
            _knockbackStunTimer -= Time.deltaTime;
            if (_knockbackStunTimer <= 0f)
            {
                _isKnockbackStunned = false;
            }
        }

        // Session 22 / S39: 蓄力冻结期忽略跳跃触发（jumpPressedThisFrame），
        // 但保留 jumpHeld 的真实状态！jumpHeld 由 InputManager 维护，
        // BouncyPlatform 在 comedyDelay 结束时通过 IsJumpHeld 查询。
        if (_isPreparingBounce)
        {
            jumpPressedThisFrame = false;
            return;
        }

        if (jumpPressedThisFrame)
        {
            _jumpToConsume = true;
            _timeJumpWasPressed = _time;
            jumpPressedThisFrame = false;
        }

        UpdateFacing();
        UpdateAnimator();

        // S36: 角色弹射形变动画更新
        UpdateBounceSquashStretch();
    }

    private void FixedUpdate()
    {
        // ── 击退 stun 分支：不覆盖 rb.velocity，让 AddForce 击退力自然衰减 ──
        if (_isKnockbackStunned)
        {
            _lastPlatformVelocity = Vector2.zero;
            _onPlatform = false;
            _platformVelocity = Vector2.zero;

            _frameVelocity = rb.velocity;

            if (_frameVelocity.y <= 0f)
            {
                _frameVelocity.y = Mathf.MoveTowards(
                    _frameVelocity.y, -MaxFallSpeed, FallAcceleration * Time.fixedDeltaTime);
            }

            rb.velocity = _frameVelocity;
            return;
        }

        // ── Session 22 / S39: 蓄力冻结期 — Kinematic Freeze ──
        // [AI防坑警告] isKinematic = true 已在 PrepareBounce() 中设置。
        // 此处仍需强制零速度作为双重保险，因为 isKinematic 的 Rigidbody
        // 仍然可以通过 rb.velocity 赋值产生位移（kinematic body 的 velocity
        // 用于 MovePosition 插值）。
        if (_isPreparingBounce)
        {
            _frameVelocity = Vector2.zero;
            rb.velocity = Vector2.zero;

            // 清除平台状态（防止平台速度在冻结期累积）
            _lastPlatformVelocity = Vector2.zero;
            _onPlatform = false;
            _platformVelocity = Vector2.zero;
            return;
        }

        // 1. 从 rb 读回速度，并减去上一帧注入的平台速度
        _frameVelocity = rb.velocity - _lastPlatformVelocity;

        // 2. 碰撞检测
        CheckCollisions();

        // 3. 跳跃（飞行期不允许跳跃，防止破坏抛物线）
        if (!_isBouncing)
        {
            HandleJump();
        }

        // 4. 水平移动（Session 22: 飞行期使用动能保留逻辑）
        HandleDirection();

        // 5. 重力
        HandleGravity();

        // 6. 叠加平台速度
        Vector2 platformVelThisFrame = _onPlatform ? _platformVelocity : Vector2.zero;
        _frameVelocity += platformVelThisFrame;

        // 7. 一次性写入 rb
        rb.velocity = _frameVelocity;

        // 8. 保存本帧平台速度
        _lastPlatformVelocity = platformVelThisFrame;

        // 9. 每帧重置平台状态
        _onPlatform = false;
        _platformVelocity = Vector2.zero;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 碰撞检测

    private void CheckCollisions()
    {
        bool prev = Physics2D.queriesStartInColliders;
        Physics2D.queriesStartInColliders = false;

        bool groundHit = Physics2D.BoxCast(
            boxCollider.bounds.center,
            new Vector2(boxCollider.bounds.size.x * 0.9f, boxCollider.bounds.size.y),
            0f, Vector2.down, GrounderDistance, groundLayer);

        bool ceilingHit = Physics2D.BoxCast(
            boxCollider.bounds.center,
            new Vector2(boxCollider.bounds.size.x * 0.9f, boxCollider.bounds.size.y),
            0f, Vector2.up, GrounderDistance, groundLayer);

        Physics2D.queriesStartInColliders = prev;

        if (ceilingHit)
        {
            _frameVelocity.y = Mathf.Min(0, _frameVelocity.y);

            // Session 22: 碰天花板解除飞行期（碰墙）
            if (_isBouncing)
            {
                _isBouncing = false;
            }
        }

        if (!_grounded && groundHit)
        {
            _grounded = true;
            _coyoteUsable = true;
            _bufferedJumpUsable = true;
            _endedJumpEarly = false;

            // Session 22: 落地时解除飞行期
            _isBouncing = false;

            // S36: 落地压扁形变（仅当下落速度超过阈值时触发，避免小跳也压扁）
            if (Mathf.Abs(_frameVelocity.y) > 3f)
            {
                _landSquashActive = true;
                _landSquashTimer = 0f;
                _bounceSquashActive = false;
                if (visualTransform != null)
                {
                    visualTransform.localScale = new Vector3(LandSquashX, LandSquashY, 1f);
                }
            }

            OnGroundedChanged?.Invoke(true, Mathf.Abs(_frameVelocity.y));
        }
        else if (_grounded && !groundHit)
        {
            _grounded = false;
            _timeLeftGrounded = _time;
            OnGroundedChanged?.Invoke(false, 0);
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 跳跃

    private void HandleJump()
    {
        if (!_endedJumpEarly && !_grounded && !jumpHeld && _frameVelocity.y > 0)
            _endedJumpEarly = true;

        if (!_jumpToConsume && !HasBufferedJump) return;

        if (_grounded || CanUseCoyote) ExecuteJump();

        _jumpToConsume = false;
    }

    private void ExecuteJump()
    {
        _endedJumpEarly = false;
        _timeJumpWasPressed = 0;
        _bufferedJumpUsable = false;
        _coyoteUsable = false;
        _frameVelocity.y = JumpPower;
        OnJump?.Invoke();
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 水平移动

    // [AI防坑警告] HandleDirection 是水平速度的唯一控制点。
    // 飞行期(_isBouncing)超速时，绝对禁止用 MoveTowards(target=maxSpeed) 截断速度！
    // 必须用 airFriction 自然衰减 + bounceAirAcceleration 微弱转向。
    // 如果直接 Clamp 到 maxSpeed，弹射抛物线会立即变成垂直下落。
    /// <summary>
    /// 水平移动处理。
    /// 
    /// Session 22 动能保留系统：
    ///   当 _isBouncing == true 且 |_frameVelocity.x| > maxSpeed 时：
    ///   - 绝对禁止 Mathf.Clamp 限制 X 轴速度
    ///   - 无输入：引入微弱空气阻力(airFriction)让速度缓慢衰减
    ///   - 有输入：允许微弱空中转向加速度(bounceAirAcceleration)，
    ///     但不可突破当前的超速上限
    ///   - 当速度自然衰减到 maxSpeed 以下时，自动恢复正常移动逻辑
    /// </summary>
    private void HandleDirection()
    {
        // ── Session 22: 抛物线飞行期动能保留 ──
        if (_isBouncing)
        {
            float absX = Mathf.Abs(_frameVelocity.x);

            if (absX > MaxSpeed)
            {
                // 超速飞行中：保留动能
                if (Mathf.Abs(moveInput.x) > 0.01f)
                {
                    // 有输入：微弱空中转向，但不突破当前超速上限
                    float currentCap = absX; // 当前速度就是上限
                    _frameVelocity.x = Mathf.MoveTowards(
                        _frameVelocity.x,
                        moveInput.x * currentCap,
                        BounceAirAcceleration * Time.fixedDeltaTime);
                    // 确保不突破上限
                    _frameVelocity.x = Mathf.Clamp(_frameVelocity.x, -currentCap, currentCap);
                }
                else
                {
                    // 无输入：微弱空气阻力自然衰减
                    _frameVelocity.x = Mathf.MoveTowards(
                        _frameVelocity.x, 0f,
                        AirFriction * Time.fixedDeltaTime);
                }
            }
            else
            {
                // 速度已衰减到 maxSpeed 以下：使用正常空中控制
                // （但仍处于飞行期，直到落地才完全解除）
                if (Mathf.Abs(moveInput.x) > 0.01f)
                {
                    _frameVelocity.x = Mathf.MoveTowards(
                        _frameVelocity.x, moveInput.x * MaxSpeed,
                        Acceleration * Time.fixedDeltaTime);
                }
                else
                {
                    _frameVelocity.x = Mathf.MoveTowards(
                        _frameVelocity.x, 0f,
                        AirDeceleration * Time.fixedDeltaTime);
                }
            }
            return;
        }

        // ── 正常状态 ──
        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            _frameVelocity.x = Mathf.MoveTowards(
                _frameVelocity.x, moveInput.x * MaxSpeed,
                Acceleration * Time.fixedDeltaTime);
        }
        else
        {
            float decel = _grounded ? GroundDeceleration : AirDeceleration;
            _frameVelocity.x = Mathf.MoveTowards(
                _frameVelocity.x, 0f,
                decel * Time.fixedDeltaTime);
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 重力

    /// <summary>
    /// 重力处理。
    /// 
    /// Session 32 半重力跳跃顶点（Celeste 风格）：
    ///   当 |velocity.y| < apexThreshold 且正在长按跳跃键时，
    ///   重力减半，给玩家更多空中调整时间。
    ///   "It's subtle, but this gives you more time to adjust for landing,
    ///    and also just looks/feels pleasant." — Maddy Thorson (Celeste)
    /// </summary>
    private void HandleGravity()
    {
        if (_grounded && _frameVelocity.y <= 0f)
        {
            _frameVelocity.y = GroundingForce;
        }
        else
        {
            float gravity = FallAcceleration;

            if (_endedJumpEarly && _frameVelocity.y > 0)
            {
                // 提前松开跳跃键：高重力快速截断跳跃
                gravity *= JumpEndEarlyGravityModifier;
            }
            else if (jumpHeld && Mathf.Abs(_frameVelocity.y) < ApexThreshold)
            {
                // Session 32: 半重力跳跃顶点
                // 长按跳跃键 + 接近跳跃顶点 → 重力减半
                // 效果：跳跃弧线顶部更平缓，给玩家更多时间调整落点
                gravity *= ApexGravityMultiplier;
            }
            else if (_isBouncing && Mathf.Abs(_frameVelocity.y) < ApexThreshold * 1.5f)
            {
                // S36: 弹射飞行期半重力顶点
                // 业界参考: Dawnosaur "Improve Your Platformer Jump" — Jump Hang
                // 弹射后不需要按住跳跃键也能获得顶点悬浮感
                // 使用 1.5x apexThreshold 让弹射弧线顶部更宽广的区域受益
                // 效果：弹射弧线顶部更平缓、更有弹性，给玩家更多空中调整时间
                gravity *= ApexGravityMultiplier;
            }

            _frameVelocity.y = Mathf.MoveTowards(
                _frameVelocity.y, -MaxFallSpeed, gravity * Time.fixedDeltaTime);
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 击退 (Session 16: B023)

    /// <summary>
    /// 外部调用：触发击退 stun，暂停控制器速度覆盖。
    /// DamageDealer 在 AddForce 之后调用此方法，确保击退力不被覆盖。
    /// </summary>
    public void ApplyKnockbackStun(float duration = -1f)
    {
        // 击退优先级最高：解除弹射状态
        _isPreparingBounce = false;
        _isBouncing = false;

        // S39: 击退时恢复 Kinematic（如果在蓄力冻结期被击退）
        if (rb.isKinematic)
        {
            rb.isKinematic = false;
        }

        _isKnockbackStunned = true;
        _knockbackStunTimer = duration > 0f ? duration : KnockbackStunDuration;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region Session 22 / S39: 两段式弹射接口

    // [AI防坑警告] PrepareBounce 和 ExecuteBounce 是成对的两段式接口。
    // PrepareBounce 必须在碰撞瞬间调用，ExecuteBounce 必须在 comedyDelay 后调用。
    // 不要试图合并成一个方法或用 AddForce 替代 — AddForce 会被下一帧的
    // _frameVelocity 写入覆盖，导致弹射力完全丢失。
    /// <summary>
    /// 阶段1：蓄力冻结（Kinematic Freeze）。碰到弹跳平台时由 BouncyPlatform 调用。
    /// 
    /// S39 重构：
    ///   - rb.isKinematic = true：彻底熔断物理引擎的穿透恢复力和重力干扰
    ///     单单把速度设为零是不够的，Unity 物理引擎在 FixedUpdate 之间
    ///     仍会对重叠的碰撞体施加分离力，导致角色在冻结期被偷偷推开
    ///   - 保留 jumpHeld 状态：不清除 jumpHeld，让 InputManager 持续维护
    ///     BouncyPlatform 在 comedyDelay 结束时通过 IsJumpHeld 查询
    /// 
    /// 效果：
    ///   - 进入 _isPreparingBounce 状态
    ///   - rb.isKinematic = true（Kinematic Freeze）
    ///   - FixedUpdate 中强制 _frameVelocity = Vector2.zero 和 rb.velocity = Vector2.zero
    ///   - Update 中忽略跳跃触发（但保留 jumpHeld）
    ///   - 角色完全冻结在平台上，不受任何物理力影响
    /// 
    /// 由 BouncyPlatform.LaunchSequence 协程在碰撞瞬间调用。
    /// </summary>
    public void PrepareBounce()
    {
        _isPreparingBounce = true;
        _isBouncing = false;

        // 立即清零速度，防止当前帧残留速度导致位移
        _frameVelocity = Vector2.zero;
        rb.velocity = Vector2.zero;

        // S39: Kinematic Freeze — 熔断物理引擎的干预
        // 极其重要：临时变成运动学刚体，物理引擎无法对其施加任何力
        // （包括重力、穿透恢复力、碰撞响应力等）
        rb.isKinematic = true;

        // 清除平台速度（弹射后不再跟随平台）
        _lastPlatformVelocity = Vector2.zero;
        _onPlatform = false;
        _platformVelocity = Vector2.zero;

        // S36: 蓄力冻结时角色压扁（视觉上"被弹簧压住"的感觉）
        if (visualTransform != null)
        {
            visualTransform.localScale = new Vector3(LandSquashX, LandSquashY, 1f);
        }
    }

    /// <summary>
    /// 阶段2：执行弹射。喜剧延迟结束后由 BouncyPlatform 调用。
    /// 
    /// S39 重构：
    ///   1. 恢复 rb.isKinematic = false（解除 Kinematic Freeze）
    ///   2. 微抬坐标 rb.position += Vector2.up * 0.05f（脱离碰撞重叠区）
    ///   3. 绝对速度覆写到 _frameVelocity 和 rb.velocity
    /// 
    /// 效果：
    ///   - 解除 _isPreparingBounce
    ///   - 进入 _isBouncing 状态（抛物线飞行期）
    ///   - 强制注入绝对弹射速度到 _frameVelocity 和 rb.velocity
    ///   - HandleDirection 在飞行期使用动能保留逻辑（超速惯性飞行）
    ///   - 落地或碰墙时自动解除 _isBouncing
    /// 
    /// 由 BouncyPlatform.LaunchSequence 协程在延迟结束后调用。
    /// </summary>
    /// <param name="launchVelocity">绝对弹射速度向量</param>
    public void ExecuteBounce(Vector2 launchVelocity)
    {
        _isPreparingBounce = false;
        _isBouncing = true;

        // S39: 恢复物理引擎（解除 Kinematic Freeze）
        rb.isKinematic = false;

        // S39: 微抬坐标 — 瞬间脱离碰撞体重叠区，拒绝排斥力干扰
        // 0.05f 足够脱离重叠但不会产生可见的位移跳跃
        rb.position += Vector2.up * 0.05f;

        // 绝对速度注入
        _frameVelocity = launchVelocity;
        rb.velocity = launchVelocity;

        // 重置跳跃状态（防止弹射后立即触发跳跃）
        _endedJumpEarly = false;
        _jumpToConsume = false;

        // S36: 弹射瞬间角色拉伸（视觉上"被弹飞"的感觉）
        _bounceSquashActive = true;
        _bounceSquashTimer = 0f;
        _landSquashActive = false;
        if (visualTransform != null)
        {
            visualTransform.localScale = new Vector3(BounceStretchX, BounceStretchY, 1f);
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 辅助

    private void UpdateFacing()
    {
        if (moveInput.x > 0.01f && !isFacingRight)       { isFacingRight = true;  spriteRenderer.flipX = false; }
        else if (moveInput.x < -0.01f && isFacingRight)  { isFacingRight = false; spriteRenderer.flipX = true;  }
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetBool("IsGrounded", _grounded);
        animator.SetFloat("Speed", Mathf.Abs(_frameVelocity.x));
        animator.SetFloat("VerticalSpeed", _frameVelocity.y);
    }

    /// <summary>
    /// S36: 角色弹射 Squash & Stretch 形变动画更新。
    /// 
    /// 业界参考:
    ///   - GameMaker Kitchen "10 Levels of Platformer Jumping": 角色形变是弹跳手感的核心
    ///   - Dawnosaur: 弹射拉伸 + 落地压扁 = "juice"
    ///   - Secrets of Springs: 过冲回弹的简谐运动
    /// 
    /// 实现原理:
    ///   通过 visualTransform.localScale 实现视觉形变（S37 视碰分离），
    ///   不影响 BoxCollider2D 的碰撞体尺寸（视碰分离）。
    ///   形变动画使用 Lerp 平滑回弹到原始尺寸。
    /// </summary>
    private void UpdateBounceSquashStretch()
    {
        if (visualTransform == null) return;

        // 弹射拉伸动画：从拉伸状态平滑回弹到原始尺寸
        if (_bounceSquashActive)
        {
            _bounceSquashTimer += Time.deltaTime;
            float progress = _bounceSquashTimer / BounceStretchDuration;

            if (progress >= 1f)
            {
                visualTransform.localScale = Vector3.one;
                _bounceSquashActive = false;
            }
            else
            {
                // 使用 SmoothStep 让回弹更自然
                float t = Mathf.SmoothStep(0f, 1f, progress);
                float scaleX = Mathf.Lerp(BounceStretchX, 1f, t);
                float scaleY = Mathf.Lerp(BounceStretchY, 1f, t);
                visualTransform.localScale = new Vector3(scaleX, scaleY, 1f);
            }
            return; // 拉伸动画优先于压扁动画
        }

        // 落地压扁动画：从压扁状态平滑回弹到原始尺寸
        if (_landSquashActive)
        {
            _landSquashTimer += Time.deltaTime;
            float progress = _landSquashTimer / LandSquashDuration;

            if (progress >= 1f)
            {
                visualTransform.localScale = Vector3.one;
                _landSquashActive = false;
            }
            else
            {
                float t = Mathf.SmoothStep(0f, 1f, progress);
                float scaleX = Mathf.Lerp(LandSquashX, 1f, t);
                float scaleY = Mathf.Lerp(LandSquashY, 1f, t);
                visualTransform.localScale = new Vector3(scaleX, scaleY, 1f);
            }
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 输入回调（由 InputManager 调用）

    public void SetMoveInput(Vector2 input)  => moveInput = input;
    public void OnJumpPressed()  { jumpPressedThisFrame = true; jumpHeld = true; }
    public void OnJumpReleased() { jumpHeld = false; }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 平台速度注入（由 MovingPlatform / ControllablePlatform 调用）

    /// <summary>
    /// 移动平台每帧调用此方法，将平台速度注入角色。
    /// 必须在角色 FixedUpdate 之前调用（平台使用 [DefaultExecutionOrder(-10)]）。
    /// </summary>
    public void SetPlatformVelocity(Vector2 velocity)
    {
        _platformVelocity = velocity;
        _onPlatform = true;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 公共方法

    /// <summary>Mario 死亡</summary>
    public void Die()
    {
        // 死亡时解除所有弹射状态
        _isPreparingBounce = false;
        _isBouncing = false;

        // S39: 死亡时恢复 Kinematic（如果在蓄力冻结期死亡）
        if (rb.isKinematic)
        {
            rb.isKinematic = false;
        }

        // S36: 重置形变状态
        _bounceSquashActive = false;
        _landSquashActive = false;
        if (visualTransform != null) visualTransform.localScale = Vector3.one;

        OnDeath?.Invoke();
        rb.velocity = Vector2.zero;
        _frameVelocity = Vector2.zero;
        enabled = false;
    }

    /// <summary>踩敌人后的弹跳</summary>
    public void Bounce(float bounceForce = 10f)
    {
        _frameVelocity.y = bounceForce;
        rb.velocity = _frameVelocity;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 碰墙检测（Session 22: 飞行期碰墙解除）

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!_isBouncing) return;

        // 检查是否碰到侧面墙壁（法线水平分量大于垂直分量）
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            if (Mathf.Abs(normal.x) > 0.5f)
            {
                // 碰墙：解除飞行期，清除水平速度
                _isBouncing = false;
                _frameVelocity.x = 0f;
                rb.velocity = new Vector2(0f, rb.velocity.y);
                return;
            }
        }
    }

    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (groundLayer == 0)
            Debug.LogWarning("[MarioController] groundLayer 未设置，跳跃将无法工作！", this);
    }
#endif
}
