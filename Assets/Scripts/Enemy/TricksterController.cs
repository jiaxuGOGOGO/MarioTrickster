using UnityEngine;

/// <summary>
/// Trickster（伪装者）控制器
///
/// 架构与 MarioController 保持一致（Tarodev 帧速度累积方案）：
///   所有速度变化在一帧内累积到 _frameVelocity，最后一次性写入 rb.velocity。
///   重力由代码自管，不依赖 Unity gravityScale。
///
///   平台跟随：移动平台使用 Kinematic Rigidbody2D + MovePosition，
///   Unity 物理引擎自动处理角色跟随，本控制器无需任何平台相关代码。
///
/// 特殊逻辑：
///   - 伪装状态下移动速度受 disguisedMoveMultiplier 限制
///   - 伪装状态下默认不可跳跃（可在 Inspector 开启）
///   - 支持 Coyote Time 和跳跃缓冲，与 Mario 手感一致
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class TricksterController : MonoBehaviour
{
    // ── 移动 ──────────────────────────────────────────────
    [Header("移动")]
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private float acceleration = 100f;
    [SerializeField] private float groundDeceleration = 60f;
    [SerializeField] private float airDeceleration = 30f;
    [SerializeField] private float groundingForce = -1.5f;

    // ── 跳跃 ──────────────────────────────────────────────
    [Header("跳跃")]
    [SerializeField] private float jumpPower = 18f;
    [SerializeField] private float maxFallSpeed = 40f;
    [SerializeField] private float fallAcceleration = 80f;
    [SerializeField] private float jumpEndEarlyGravityModifier = 3f;
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private float jumpBuffer = 0.2f;

    // ── 地面检测 ──────────────────────────────────────────
    [Header("地面检测")]
    [Tooltip("地面所在的 Layer（必须设置，否则无法跳跃）")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float grounderDistance = 0.05f;

    // ── 伪装限制 ──────────────────────────────────────────
    [Header("伪装状态限制")]
    [Tooltip("伪装状态下的移动速度倍率（0=完全不能动，1=正常速度）")]
    [SerializeField] private float disguisedMoveMultiplier = 0.15f;
    [Tooltip("伪装状态下能否跳跃")]
    [SerializeField] private bool canJumpWhileDisguised = false;

    // ── 组件 ──────────────────────────────────────────────
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;
    private DisguiseSystem disguiseSystem;
    private TricksterAbilitySystem abilitySystem;

    // ── 输入（由 InputManager 每帧写入）─────────────────────
    private Vector2 moveInput;
    private bool jumpPressedThisFrame;
    private bool jumpHeld;

    // ── 帧速度 ────────────────────────────────────────────
    private Vector2 _frameVelocity;

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
    public bool IsGrounded  => _grounded;
    public bool IsDisguised => disguiseSystem != null && disguiseSystem.IsDisguised;
    public TricksterAbilitySystem AbilitySystem => abilitySystem;

    // ─────────────────────────────────────────────────────
    #region 初始化

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        disguiseSystem = GetComponent<DisguiseSystem>();
        abilitySystem = GetComponent<TricksterAbilitySystem>();

        rb.gravityScale = 0f;  // 重力由代码自管
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

        if (jumpPressedThisFrame)
        {
            _jumpToConsume = true;
            _timeJumpWasPressed = _time;
            jumpPressedThisFrame = false;
        }

        if (!IsDisguised) UpdateFacing();

        if (abilitySystem != null)
            abilitySystem.SetAbilityDirection(moveInput);
    }

    private void FixedUpdate()
    {
        _frameVelocity = rb.velocity;

        CheckCollisions();
        HandleJump();
        HandleDirection();
        HandleGravity();

        rb.velocity = _frameVelocity;
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
        }
        else if (_grounded && !groundHit)
        {
            _grounded = false;
            _timeLeftGrounded = _time;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 跳跃

    private void HandleJump()
    {
        // 伪装状态下不可跳跃（除非开启 canJumpWhileDisguised）
        if (IsDisguised && !canJumpWhileDisguised)
        {
            _jumpToConsume = false;
            return;
        }

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
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 水平移动

    private void HandleDirection()
    {
        float speedMult = IsDisguised ? disguisedMoveMultiplier : 1f;
        float target = moveInput.x * maxSpeed * speedMult;

        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            _frameVelocity.x = Mathf.MoveTowards(
                _frameVelocity.x, target, acceleration * Time.fixedDeltaTime);
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

    #endregion

    // ─────────────────────────────────────────────────────
    #region 输入回调（由 InputManager 调用）

    public void SetMoveInput(Vector2 input)  => moveInput = input;
    public void OnJumpPressed()  { jumpPressedThisFrame = true; jumpHeld = true; }
    public void OnJumpReleased() { jumpHeld = false; }

    public void OnDisguisePressed()
    {
        disguiseSystem?.ToggleDisguise();
    }

    public void OnSwitchDisguise(float direction)
    {
        if (disguiseSystem == null || disguiseSystem.IsDisguised) return;
        if (direction > 0) disguiseSystem.NextDisguise();
        else               disguiseSystem.PreviousDisguise();
    }

    public void OnAbilityPressed()
    {
        abilitySystem?.OnAbilityPressed();
    }

    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (groundLayer == 0)
            Debug.LogWarning("[TricksterController] groundLayer 未设置，跳跃将无法工作！", this);
    }
#endif
}
