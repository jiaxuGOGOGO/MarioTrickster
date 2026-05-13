using UnityEngine;

/// <summary>
/// Trickster（伪装者）控制器
///
/// 架构与 MarioController 保持一致（Tarodev 帧速度累积方案）：
///   所有速度变化在一帧内累积到 _frameVelocity，最后一次性写入 rb.velocity。
///   重力由代码自管，不依赖 Unity gravityScale。
///
///   平台跟随：移动平台每帧调用 SetPlatformVelocity() 注入平台速度，
///   FixedUpdate 最后将平台速度叠加到 _frameVelocity 再写入 rb。
///   不使用 SetParent（避免 Transform 层级与 Rigidbody2D 世界坐标冲突）。
///
/// 特殊逻辑：
///   - 伪装状态下移动速度受 disguisedMoveMultiplier 限制
///   - 伪装状态下默认不可跳跃（可在 Inspector 开启）
///   - 支持 Coyote Time 和跳跃缓冲，与 Mario 手感一致
///
/// Session 16 更新:
///   B023 - 添加击退 stun 机制：受伤时暂停控制器速度覆盖，让 AddForce 击退力生效
///
/// Session 20 更新:
///   - 融入状态下方向键输入拦截：不再作为移动输入，而是转发给 TricksterAbilitySystem.SwitchTarget()
///   - 防止方向键打破融入状态
///   - 新增 OnDirectionInput(Vector2) 回调供 InputManager 调用
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[SelectionBase] // S37 视碰分离: 确保框选时选中 Root 而非 Visual 子节点
public class TricksterController : MonoBehaviour
{
    // ── 移动 ──────────────────────────────────────────────
    [Header("移动")]
    [SerializeField] private float maxSpeed = 8f;
    [Tooltip("地面加速度（越大起步越快）")]
    [SerializeField] private float acceleration = 140f;
    [Tooltip("地面减速度（越大停止越果断，调大可消除打滑感）")]
    [SerializeField] private float groundDeceleration = 200f;
    [Tooltip("空中减速度（松开输入后，建议保持较小以保留空中滑行感）")]
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

    [Header("半重力跳跃顶点 (Session 32)")]
    [Tooltip("跳跃顶点附近的速度阈值，|velocity.y| < 此值时视为顶点区")]
    [SerializeField] private float apexThreshold = 2.0f;
    [Tooltip("顶点区域重力倍率（0.5=半重力，Celeste 风格）")]
    [SerializeField] private float apexGravityMultiplier = 0.5f;

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

    // ── 击退 (Session 16) ────────────────────────────────
    [Header("击退")]
    [Tooltip("受击后控制器暂停时长（秒），让击退力生效")]
    [SerializeField] private float knockbackStunDuration = 0.25f;

    // ── 组件 ──────────────────────────────────────────────
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;
    private DisguiseSystem disguiseSystem;
    private TricksterAbilitySystem abilitySystem;

    // ── S37: 视碰分离 — 视觉代理节点 ──────────────────────
    // [AI防坑警告] visualTransform 是视觉层的唯一操作目标。
    // 朝向翻转统一使用 spriteRenderer.flipX，绝不修改 localScale.x = -1。
    [Header("S37: 视碰分离")]
    [Tooltip("视觉子节点的 Transform。为空时自动回退到自身 Transform。")]
    public Transform visualTransform;

    // ── 输入（由 InputManager 每帧写入）─────────────────────
    private Vector2 moveInput;
    private bool jumpPressedThisFrame;
    private bool jumpHeld;

     // ── 帧速度 ────────────────────────────────────────
    private Vector2 _frameVelocity;

    // ── 平台速度注入 ──
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

    // ── 朝向 ──────────────────────────────────────────────
    private bool isFacingRight = true;

    // ── Session 20: 方向键磁吸切换防抖 ────────────────────
    private float _lastSwitchTime;
    private const float SwitchCooldown = 0.2f; // 切换冷却时间，防止连续快速切换

    // ── 公共属性 ──────────────────────────────────────────
    public bool IsGrounded  => _grounded;
    public bool IsDisguised => disguiseSystem != null && disguiseSystem.IsDisguised;

    /// <summary>Session 20: 是否处于融入状态（已伪装且完全融入）</summary>
    public bool IsFullyBlended => disguiseSystem != null && disguiseSystem.IsDisguised && disguiseSystem.IsFullyBlended;

    public TricksterAbilitySystem AbilitySystem => abilitySystem;

    // ── 事件 ──────────────────────────────────────────────
    /// <summary>道具操控失败时触发，参数为失败原因（用于 UI 显示）</summary>
    public System.Action<string> OnAbilityFailed;

    // ─────────────────────────────────────────────────────
    #region 初始化

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        // S37: 视碰分离 — SpriteRenderer 可能在子物体 Visual 上
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        disguiseSystem = GetComponent<DisguiseSystem>();
        abilitySystem = GetComponent<TricksterAbilitySystem>();

        // S37: visualTransform 兼容回退
        if (visualTransform == null && spriteRenderer != null)
            visualTransform = spriteRenderer.transform;
        if (visualTransform == null)
            visualTransform = transform;

        // S-Fix: 视碰对齐 — 将 Visual 子节点下移使 Sprite 底边对齐碰撞体底边，消除悬空。
        if (visualTransform != transform && visualTransform.parent == transform)
        {
            if (Mathf.Approximately(visualTransform.localPosition.y, 0f))
            {
                Vector3 pos = visualTransform.localPosition;
                pos.y = PhysicsMetrics.TRICKSTER_VISUAL_OFFSET_Y;
                visualTransform.localPosition = pos;
            }
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

        if (jumpPressedThisFrame)
        {
            _jumpToConsume = true;
            _timeJumpWasPressed = _time;
            jumpPressedThisFrame = false;
        }

        if (!IsDisguised) UpdateFacing();

        // Session 20: 融入状态下不再将方向键作为 abilityDirection 传递
        // （方向键已被拦截用于磁吸切换，不影响道具操控方向）
        if (abilitySystem != null && !IsFullyBlended)
            abilitySystem.SetAbilityDirection(moveInput);
    }

    private void FixedUpdate()
    {
        // 击退 stun 期间：不覆盖 rb.velocity，让物理引擎的 AddForce 击退力自然衰减
        if (_isKnockbackStunned)
        {
            _lastPlatformVelocity = Vector2.zero;
            _onPlatform = false;
            _platformVelocity = Vector2.zero;

            _frameVelocity = rb.velocity;

            if (_frameVelocity.y <= 0f)
            {
                _frameVelocity.y = Mathf.MoveTowards(
                    _frameVelocity.y, -maxFallSpeed, fallAcceleration * Time.fixedDeltaTime);
            }

            rb.velocity = _frameVelocity;
            return;
        }

        // 读回 rb.velocity 并减去上一帧平台速度
        _frameVelocity = rb.velocity - _lastPlatformVelocity;

        CheckCollisions();
        HandleJump();
        HandleDirection();
        HandleGravity();

        // 叠加平台速度
        Vector2 platformVelThisFrame = _onPlatform ? _platformVelocity : Vector2.zero;
        _frameVelocity += platformVelThisFrame;

        rb.velocity = _frameVelocity;

        _lastPlatformVelocity = platformVelThisFrame;

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
        // Session 20: 融入状态下方向键被拦截，不产生移动
        // moveInput 在融入状态下由 InputManager 设为 zero（见 DispatchP2 修改）
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

    /// <summary>
    /// Session 32 半重力跳跃顶点（Celeste 风格）：
    ///   当 |velocity.y| < apexThreshold 且正在长按跳跃键时，
    ///   重力减半，给玩家更多空中调整时间。
    /// </summary>
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
            {
                gravity *= jumpEndEarlyGravityModifier;
            }
            else if (jumpHeld && Mathf.Abs(_frameVelocity.y) < apexThreshold)
            {
                // Session 32: 半重力跳跃顶点
                // 长按跳跃键 + 接近跳跃顶点 → 重力减半
                gravity *= apexGravityMultiplier;
            }

            _frameVelocity.y = Mathf.MoveTowards(
                _frameVelocity.y, -maxFallSpeed, gravity * Time.fixedDeltaTime);
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 击退 (Session 16: B023)

    /// <summary>
    /// 外部调用：触发击退 stun，暂停控制器速度覆盖。
    /// </summary>
    public void ApplyKnockbackStun(float duration = -1f)
    {
        _isKnockbackStunned = true;
        _knockbackStunTimer = duration > 0f ? duration : knockbackStunDuration;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 辅助

    private void UpdateFacing()
    {
        if (moveInput.x > 0.01f && !isFacingRight)       { isFacingRight = true;  spriteRenderer.flipX = false; }
        else if (moveInput.x < -0.01f && isFacingRight)  { isFacingRight = false; spriteRenderer.flipX = true;  }
    }

    /// <summary>死亡处理</summary>
    public void Die()
    {
        enabled = false;
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

    /// <summary>
    /// 移动平台每帧调用此方法，将平台速度注入角色。
    /// 必须在角色 FixedUpdate 之前调用（平台使用 [DefaultExecutionOrder(-10)]）。
    /// </summary>
    public void SetPlatformVelocity(Vector2 velocity)
    {
        _platformVelocity = velocity;
        _onPlatform = true;
    }

    public void OnSwitchDisguise(float direction)
    {
        if (disguiseSystem == null || disguiseSystem.IsDisguised) return;
        if (direction > 0) disguiseSystem.NextDisguise();
        else               disguiseSystem.PreviousDisguise();
    }

    public void OnAbilityPressed()
    {
        if (abilitySystem == null) return;

        string failReason = GetAbilityFailReason();
        if (failReason != null)
        {
            OnAbilityFailed?.Invoke(failReason);
            return;
        }

        abilitySystem.OnAbilityPressed();
    }

    /// <summary>
    /// Session 20: 方向键磁吸切换目标
    /// 由 InputManager 在融入状态下拦截方向键后调用。
    /// 带防抖冷却，避免连续快速切换。
    /// </summary>
    public void OnDirectionInput(Vector2 direction)
    {
        if (abilitySystem == null) return;
        if (!IsFullyBlended) return;
        if (direction.sqrMagnitude < 0.01f) return;

        // 防抖：冷却时间内不重复切换
        if (Time.time - _lastSwitchTime < SwitchCooldown) return;
        _lastSwitchTime = Time.time;

        abilitySystem.SwitchTarget(direction);
    }

    /// <summary>
    /// 检查道具操控的失败原因，返回 null 表示可以操控
    /// </summary>
    private string GetAbilityFailReason()
    {
        if (disguiseSystem == null || !disguiseSystem.IsDisguised)
            return "Must be disguised to control props!";

        if (!abilitySystem.IsAbilityActive)
        {
            if (disguiseSystem.IsDisguised && !disguiseSystem.IsFullyBlended)
                return "Stay still to blend in first!";
            return "Ability not ready!";
        }

        if (abilitySystem.ControlsRemaining == 0)
            return "No controls remaining!";

        if (abilitySystem.BoundProp == null)
            return "No controllable prop nearby!";

        if (!abilitySystem.BoundProp.CanBeControlled())
        {
            var state = abilitySystem.BoundProp.GetControlState();
            if (state == PropControlState.Cooldown)
                return $"Prop on cooldown!";
            if (state == PropControlState.Active || state == PropControlState.Telegraph)
                return "Prop already active!";
            if (state == PropControlState.Exhausted)
                return "Prop uses exhausted!";
            return "Prop not ready!";
        }

        return null;
    }

    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (groundLayer == 0)
            Debug.LogWarning("[TricksterController] groundLayer 未设置，跳跃将无法工作！", this);
    }
#endif

    // 调试显示：在屏幕左上角偏下显示伪装系统状态
    // Session 11 修复：原来放在右上角(Screen.width-520)，Game视图窄时会被裁剪看不到
    // Session 18 性能优化：缓存 GUIStyle，消除每帧 new 分配
    private GUIStyle cachedDebugStyle;
    private void OnGUI()
    {
        if (disguiseSystem == null) return;
        if (cachedDebugStyle == null)
        {
            cachedDebugStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow }
            };
        }
        string status = disguiseSystem.GetDebugStatus();
        // 放在左上角第二行（第一行是Mario HP），确保任何分辨率都能看到
        GUI.Label(new Rect(20, 50, 600, 25), $"[Trickster] {status}", cachedDebugStyle);
    }
}
