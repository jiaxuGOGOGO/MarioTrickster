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
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class MarioController : MonoBehaviour
{
    // ── 移动 ──────────────────────────────────────────────
    [Header("移动")]
    [Tooltip("最大水平速度")]
    [SerializeField] private float maxSpeed = 9f;
    [Tooltip("地面加速度")]
    [SerializeField] private float acceleration = 120f;
    [Tooltip("地面减速度")]
    [SerializeField] private float groundDeceleration = 60f;
    [Tooltip("空中减速度（松开输入后）")]
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
    // 方案说明：
    //   Dynamic Rigidbody2D 的 rb.velocity 是世界坐标系绝对速度，
    //   SetParent 改变 Transform 层级但物理引擎不理解层级关系，
    //   所以 SetParent 无法让角色跟随 Kinematic 平台移动。
    //   正确做法：平台每帧把自己的速度注入角色，角色在 FixedUpdate
    //   最后将平台速度叠加到 _frameVelocity，实现跟随效果。
    private Vector2 _platformVelocity;
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

    // ── 朝向 ──────────────────────────────────────────────
    private bool isFacingRight = true;

    // ── 公共属性 ──────────────────────────────────────────
    public bool IsGrounded    => _grounded;
    public bool IsMoving      => Mathf.Abs(_frameVelocity.x) > 0.1f;
    public bool IsFacingRight => isFacingRight;
    public Vector2 Velocity   => _frameVelocity;

    // ── 事件 ──────────────────────────────────────────────
    public System.Action OnJump;
    public System.Action<bool, float> OnGroundedChanged; // (isGrounded, impactSpeed)
    public System.Action OnDeath;

    // ─────────────────────────────────────────────────────
    #region 初始化

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>(); // 可选

        // 重力由代码自管，关闭 Unity 内置重力
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 零摩擦材质：防止贴墙/贴平台侧面时被摩擦力卡住
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

        // 跳跃输入缓冲
        if (jumpPressedThisFrame)
        {
            _jumpToConsume = true;
            _timeJumpWasPressed = _time;
            jumpPressedThisFrame = false;
        }

        // 朝向
        UpdateFacing();
        // 动画
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        // 1. 从 rb 同步当前速度到帧速度
        _frameVelocity = rb.velocity;

        // 2. 碰撞检测（更新 _grounded）
        CheckCollisions();

        // 3. 跳跃
        HandleJump();

        // 4. 水平移动
        HandleDirection();

        // 5. 重力
        HandleGravity();

        // 6. 叠加平台速度（在所有自身速度计算完成后叠加）
        if (_onPlatform)
        {
            _frameVelocity += _platformVelocity;
        }

        // 7. 一次性写入 rb
        rb.velocity = _frameVelocity;

        // 8. 每帧重置平台状态（平台脚本每帧重新设置，若未设置则视为不在平台上）
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

        // 地面检测（BoxCast 比 Raycast 更稳定，不会卡在边角）
        bool groundHit = Physics2D.BoxCast(
            boxCollider.bounds.center,
            new Vector2(boxCollider.bounds.size.x * 0.9f, boxCollider.bounds.size.y),
            0f, Vector2.down, grounderDistance, groundLayer);

        // 天花板检测（撞头时清除向上速度）
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
        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            _frameVelocity.x = Mathf.MoveTowards(
                _frameVelocity.x, moveInput.x * maxSpeed, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            float decel = _grounded ? groundDeceleration : airDeceleration;
            _frameVelocity.x = Mathf.MoveTowards(
                _frameVelocity.x, 0f, decel * Time.fixedDeltaTime);
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
