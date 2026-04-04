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
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class MarioController : MonoBehaviour
{
    // ── 移动 ──────────────────────────────────────────────
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

    // ── 组件 ──────────────────────────────────────────────
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

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

    private bool HasBufferedJump => _bufferedJumpUsable && _time < _timeJumpWasPressed + jumpBuffer;
    private bool CanUseCoyote    => _coyoteUsable && !_grounded && _time < _timeLeftGrounded + coyoteTime;

    // ── 击退 stun 状态 (Session 16: B023) ─────────────────
    private bool _isKnockbackStunned;
    private float _knockbackStunTimer;

    // ── Session 20: BounceStun 状态 ──────────────────────
    // 区别于 KnockbackStun：不完全禁止横向控制，而是大幅降低操控力
    // 让弹跳的水平惯性自然保持，产生顺滑的抛物线
    private bool _isBounceStunned;
    private float _bounceStunTimer;
    private float _bounceAccelMult;   // 弹跳期间加速度倍率
    private float _bounceDecelMult;   // 弹跳期间减速度倍率

    // ── 朝向 ──────────────────────────────────────────────
    private bool isFacingRight = true;

    // ── 公共属性 ──────────────────────────────────────────
    public bool IsGrounded    => _grounded;
    public bool IsMoving      => Mathf.Abs(_frameVelocity.x) > 0.1f;
    public bool IsFacingRight => isFacingRight;
    public Vector2 Velocity   => _frameVelocity;

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
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

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

        // Session 20: BounceStun 倒计时
        if (_isBounceStunned)
        {
            _bounceStunTimer -= Time.deltaTime;
            if (_bounceStunTimer <= 0f)
            {
                _isBounceStunned = false;
            }
        }

        if (jumpPressedThisFrame)
        {
            _jumpToConsume = true;
            _timeJumpWasPressed = _time;
            jumpPressedThisFrame = false;
        }

        UpdateFacing();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        // 击退 stun 期间：不覆盖 rb.velocity，让物理引擎的 AddForce 击退力自然衰减
        if (_isKnockbackStunned)
        {
            // 仍然需要更新平台状态，避免平台速度累积
            _lastPlatformVelocity = Vector2.zero;
            _onPlatform = false;
            _platformVelocity = Vector2.zero;

            // 只做重力（让击退弧线自然）但不做水平控制
            // 读回当前速度作为基础
            _frameVelocity = rb.velocity;

            // 应用重力让角色自然下落
            if (_frameVelocity.y <= 0f)
            {
                _frameVelocity.y = Mathf.MoveTowards(
                    _frameVelocity.y, -maxFallSpeed, fallAcceleration * Time.fixedDeltaTime);
            }

            rb.velocity = _frameVelocity;
            return;
        }

        // 1. 从 rb 读回速度，并减去上一帧注入的平台速度
        _frameVelocity = rb.velocity - _lastPlatformVelocity;

        // 2. 碰撞检测
        CheckCollisions();

        // 3. 跳跃
        HandleJump();

        // 4. 水平移动（Session 20: BounceStun 期间使用降低的操控力）
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
            0f, Vector2.down, grounderDistance, groundLayer);

        bool ceilingHit = Physics2D.BoxCast(
            boxCollider.bounds.center,
            new Vector2(boxCollider.bounds.size.x * 0.9f, boxCollider.bounds.size.y),
            0f, Vector2.up, grounderDistance, groundLayer);

        Physics2D.queriesStartInColliders = prev;

        if (ceilingHit) _frameVelocity.y = Mathf.Min(0, _frameVelocity.y);

        if (!_grounded && groundHit)
        {
            _grounded = true;
            _coyoteUsable = true;
            _bufferedJumpUsable = true;
            _endedJumpEarly = false;

            // Session 20: 落地时自动解除 BounceStun（抛物线已结束）
            _isBounceStunned = false;

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
        _frameVelocity.y = jumpPower;
        OnJump?.Invoke();
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 水平移动

    private void HandleDirection()
    {
        // Session 20: BounceStun 期间使用降低的操控力
        // 这样弹跳的水平惯性不会被玩家输入瞬间抹平
        float accelMult = _isBounceStunned ? _bounceAccelMult : 1f;
        float decelMult = _isBounceStunned ? _bounceDecelMult : 1f;

        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            _frameVelocity.x = Mathf.MoveTowards(
                _frameVelocity.x, moveInput.x * maxSpeed,
                acceleration * accelMult * Time.fixedDeltaTime);
        }
        else
        {
            float decel = _grounded ? groundDeceleration : airDeceleration;
            _frameVelocity.x = Mathf.MoveTowards(
                _frameVelocity.x, 0f,
                decel * decelMult * Time.fixedDeltaTime);
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 重力

    private void HandleGravity()
    {
        if (_grounded && _frameVelocity.y <= 0f)
        {
            _frameVelocity.y = groundingForce;
        }
        else
        {
            float gravity = fallAcceleration;
            if (_endedJumpEarly && _frameVelocity.y > 0)
                gravity *= jumpEndEarlyGravityModifier;

            _frameVelocity.y = Mathf.MoveTowards(
                _frameVelocity.y, -maxFallSpeed, gravity * Time.fixedDeltaTime);
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
        _isKnockbackStunned = true;
        _knockbackStunTimer = duration > 0f ? duration : knockbackStunDuration;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region Session 20: BounceStun（弹跳后降低操控力）

    /// <summary>
    /// 弹跳平台触发：进入 BounceStun 状态。
    /// 
    /// 与 KnockbackStun 的区别：
    ///   - KnockbackStun：完全禁止横向控制（让 AddForce 击退力生效）
    ///   - BounceStun：大幅降低横向加速/减速度（让弹跳抛物线自然展开）
    /// 
    /// 参考 Celeste 弹簧最佳实践：
    ///   弹簧弹射后短暂降低玩家操控力，让物理引擎先接管轨迹，
    ///   之后再恢复正常控制。这样玩家仍有微弱操控感（不会感觉失控），
    ///   但不会瞬间抹平弹跳的水平惯性。
    /// </summary>
    /// <param name="duration">BounceStun 持续时间（秒）</param>
    /// <param name="accelMultiplier">加速度倍率（0~1，越小操控力越弱）</param>
    /// <param name="decelMultiplier">减速度倍率（0~1，越小惯性保持越久）</param>
    public void ApplyBounceStun(float duration, float accelMultiplier = 0.12f, float decelMultiplier = 0.08f)
    {
        _isBounceStunned = true;
        _bounceStunTimer = duration;
        _bounceAccelMult = Mathf.Clamp01(accelMultiplier);
        _bounceDecelMult = Mathf.Clamp01(decelMultiplier);

        // BounceStun 优先级低于 KnockbackStun
        // 如果同时处于 KnockbackStun，BounceStun 会在 KnockbackStun 结束后生效
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

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (groundLayer == 0)
            Debug.LogWarning("[MarioController] groundLayer 未设置，跳跃将无法工作！", this);
    }
#endif
}
