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
    // No press exists at time zero. Zero is a valid press time, not an empty buffer.
    private float _timeJumpWasPressed = float.NegativeInfinity;

    private bool HasBufferedJump => _bufferedJumpUsable && _time < _timeJumpWasPressed + jumpBuffer;
    private bool CanUseCoyote    => _coyoteUsable && !_grounded && _time < _timeLeftGrounded + coyoteTime;

    // ── 击退 stun 状态 (Session 16: B023) ─────────────────
    private bool _isKnockbackStunned;
    private float _knockbackStunTimer;
    private bool _stunUntilLanded; // S216：被发射/炸飞 → 落地才恢复控制

    // ── 朝向 ──────────────────────────────────────────────
    private bool isFacingRight = true;

    // ── Session 20: 方向键磁吸切换防抖 ────────────────────
    private float _lastSwitchTime;
    private const float SwitchCooldown = 0.2f; // 切换冷却时间，防止连续快速切换

    // ── 公共属性 ──────────────────────────────────────────
    public bool IsGrounded  => _grounded;
    public bool IsDisguised => disguiseSystem != null && disguiseSystem.IsDisguised;

    // ── S197：第 1 步技能钩子（数据驱动，默认值 = 旧行为不变）─────────
    /// <summary>移动速度倍率（缩小时变快等）。</summary>
    public float AbilitySpeedMultiplier { get; set; } = 1f;
    public bool IsFacingRightValue => isFacingRight;
    /// <summary>跳跃力（构建器按"必须跳得上 2.5 格"设定；null = Inspector 值）。</summary>
    public void SetJumpPower(float power) { if (power > 0f) jumpPower = power; }
    public float JumpPowerValue => jumpPower;
    public float FallAccelerationValue => fallAcceleration;
    /// <summary>缩小身体：碰撞体与外观按比例缩放（底边对齐，站在原地不会穿地）。</summary>
    public void SetBodyScale(float scale)
    {
        scale = Mathf.Clamp(scale, 0.3f, 1f);
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();
        if (!_baseSizeCaptured && boxCollider != null) { _baseSize = boxCollider.size; _baseOffset = boxCollider.offset; _baseScale = transform.localScale; _baseSizeCaptured = true; }
        if (!_baseSizeCaptured) return;
        float bottom = _baseOffset.y - _baseSize.y * 0.5f;
        boxCollider.size = _baseSize * scale;
        boxCollider.offset = new Vector2(_baseOffset.x, bottom + _baseSize.y * scale * 0.5f);
        var vis = visualTransform != null ? visualTransform : transform.Find("Visual");
        if (vis != null)
        {
            if (!_visualCaptured) { _visualBase = vis.localScale; _visualPos = vis.localPosition; _visualCaptured = true; }
            vis.localScale = new Vector3(_visualBase.x * scale, _visualBase.y * scale, _visualBase.z);
            // 视觉底边与碰撞体底边一起对齐（视觉锚点在脚底，见 PhysicsMetrics.TRICKSTER_VISUAL_OFFSET_Y）
            vis.localPosition = _visualPos;
        }
        BodyScale = scale;
    }
    public float BodyScale { get; private set; } = 1f;
    private bool _baseSizeCaptured, _visualCaptured; private Vector2 _baseSize, _baseOffset; private Vector3 _baseScale, _visualBase, _visualPos;

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
            if (Step1Feel.StunOver(_knockbackStunTimer, _stunUntilLanded, _grounded, LaunchFeel.landGrace))
            {
                _isKnockbackStunned = false; _stunUntilLanded = false;
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
        // Match Mario: direct fixed-step replay may press jump between rendered Updates.
        if (jumpPressedThisFrame)
        {
            _jumpToConsume = true;
            _timeJumpWasPressed = _time;
            jumpPressedThisFrame = false;
        }
        // 击退 stun 期间：不覆盖 rb.velocity，让物理引擎的 AddForce 击退力自然衰减
        if (_isKnockbackStunned)
        {
            _lastPlatformVelocity = Vector2.zero;
            _onPlatform = false;
            _platformVelocity = Vector2.zero;

            _frameVelocity = rb.velocity;
            // S216：与马里奥同一条规则——全程重力（抛物线）+ 空中阻力 + 落地摩擦
            if (!rb.isKinematic) CheckCollisions();
            _frameVelocity = Step1Feel.StunStep(_frameVelocity, _grounded, Time.fixedDeltaTime,
                LaunchFeel.gravity, maxFallSpeed, LaunchFeel.airDrag, LaunchFeel.groundFriction, false);
            if (_grounded && _frameVelocity.y <= 0f) _frameVelocity.y = groundingForce;

            rb.velocity = _frameVelocity;
            return;
        }

        // S209：身体嵌进墙里（伪装变大 / 缩小恢复 / 传送）→ 先推出来，否则脚下检测会把墙里的方块顶当地面，悬在半空（用户截图）
        if (!rb.isKinematic) BodyUnstick.Resolve(rb, boxCollider, groundLayer);

        // 读回 rb.velocity 并减去上一帧平台速度
        _frameVelocity = rb.velocity - _lastPlatformVelocity;

        CheckCollisions();
        HandleJump();
        HandleDirection();
        // S209：贴墙不粘要在"施加方向键之后"再判断（原来在之前判断，随后方向键又把朝墙速度加回去 → 仍然粘墙）
        if (!_grounded && Mathf.Abs(_frameVelocity.x) > 0.01f && HitsWall(_frameVelocity.x > 0f ? Vector2.right : Vector2.left)) _frameVelocity.x = 0f;
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

    private static readonly RaycastHit2D[] s_wallHits = new RaycastHit2D[4];

    /// <summary>S198：身体一侧紧贴实心（非单向台面）墙面。</summary>
    private bool HitsWall(Vector2 side)
    {
        if (boxCollider == null) return false;
        var b = boxCollider.bounds;
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(groundLayer);
        int n = Physics2D.BoxCast(b.center, new Vector2(b.size.x, b.size.y * 0.8f), 0f, side, filter, s_wallHits, 0.04f);
        for (int i = 0; i < n; i++)
        {
            var c = s_wallHits[i].collider;
            if (c == null || c == boxCollider || c.isTrigger) continue;
            if (MarioSuspicionTracker.IsOneWayPlatform(c)) continue;
            if (Mathf.Abs(s_wallHits[i].normal.x) > 0.5f) return true;
        }
        return false;
    }

    private void CheckCollisions()
    {
        bool groundHit = OneWayPlatform.HasBlockingSurface(boxCollider, Vector2.down,
            grounderDistance, groundLayer, _frameVelocity.y);
        bool ceilingHit = OneWayPlatform.HasBlockingSurface(boxCollider, Vector2.up,
            grounderDistance, groundLayer, _frameVelocity.y);

        if (ceilingHit) _frameVelocity.y = Mathf.Min(0, _frameVelocity.y);

        // S198：贴墙不粘。空中朝墙推时，零摩擦材质之外 Unity 的接触求解仍会让刚体"卡"在墙面上（用户反馈：跳起来能粘在墙上）。
        // 检测到正在朝实心墙移动 → 清掉朝墙的水平速度，让重力正常把人拉下来。
        if (!_grounded && Mathf.Abs(_frameVelocity.x) > 0.01f)
        {
            Vector2 side = _frameVelocity.x > 0f ? Vector2.right : Vector2.left;
            if (HitsWall(side)) _frameVelocity.x = 0f;
        }

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
        _timeJumpWasPressed = float.NegativeInfinity;
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
        float target = moveInput.x * maxSpeed * speedMult * AbilitySpeedMultiplier;

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
        _stunUntilLanded = false;
        _isKnockbackStunned = true;
        _knockbackStunTimer = duration > 0f ? duration : knockbackStunDuration;
    }

    /// <summary>
    /// S187：被外力发射（大炮人肉发射 / 以后的弹射装置）。解除伪装，设置速度，
    /// 在 stunSeconds 内不覆盖速度（复用击退 stun 通道：物理自然衰减 + 重力），之后恢复正常控制。
    /// </summary>
    public void Launch(Vector2 velocity, float stunSeconds)
    {
        if (rb == null) return;
        if (disguiseSystem != null && disguiseSystem.IsDisguised) disguiseSystem.Undisguise();
        _grounded = false;
        _frameVelocity = velocity;
        rb.velocity = velocity;
        ApplyKnockbackStun(Mathf.Max(0.05f, stunSeconds));
        _stunUntilLanded = velocity.y > 0.5f; // S216：往上飞的要等落地
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

    /// <summary>
    /// 回合重置时调用：清零物理状态、解除伪装、恢复控制器。
    /// 由 GameManager.ResetRound() 在传送位置之后调用。
    /// </summary>
    public void ResetForNewRound()
    {
        // 未初始化（对象从未激活过，Awake 未执行）时没有可重置的状态。
        if (rb == null) return;

        // 1. 清零速度
        rb.velocity = Vector2.zero;
        _frameVelocity = Vector2.zero;

        // 2. 重置击退状态
        _isKnockbackStunned = false;
        _knockbackStunTimer = 0f;

        // 3. 重置跳跃状态
        _timeJumpWasPressed = float.NegativeInfinity;
        _timeLeftGrounded = float.NegativeInfinity;
        _grounded = false;
        _jumpToConsume = false;
        _bufferedJumpUsable = false;
        _endedJumpEarly = false;
        _coyoteUsable = false;
        jumpPressedThisFrame = false;
        jumpHeld = false;

        // 4. 重置平台速度
        _platformVelocity = Vector2.zero;
        _lastPlatformVelocity = Vector2.zero;
        _onPlatform = false;

        // 5. 重置输入
        moveInput = Vector2.zero;

        // 6. 解除伪装（通过事件链级联清理 AbilitySystem 和 PossessionGate）
        if (disguiseSystem != null && disguiseSystem.IsDisguised)
        {
            disguiseSystem.Undisguise();
        }
        // 重置伪装冷却（新回合立即可用）
        if (disguiseSystem != null)
        {
            disguiseSystem.ResetCooldown();
        }

        // 7. 确保控制器启用
        enabled = true;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 输入回调（由 InputManager 调用）

    public void SetMoveInput(Vector2 input)  => moveInput = input;
    public void OnJumpPressed()  { jumpPressedThisFrame = true; jumpHeld = true; }
    public void OnJumpReleased() { jumpHeld = false; }

    public void OnDisguisePressed()
    {
        if (IsPossessionLockoutActive()) return;
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
        if (IsPossessionLockoutActive()) return;
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

        // S198：当前控制的是有炮弹的大炮 → 方向键用来瞄准（←→ 调头、↑↓ 仰角），不切换目标。
        // 想换别的机关：按住 Shift + 方向键（或先打完炮弹）。
        if (abilitySystem.BoundProp is PranksterCannon cannon && cannon.HasAmmo && !ShiftHeld())
        {
            cannon.Nudge(direction);
            return;
        }

        // 防抖：冷却时间内不重复切换
        if (Time.time - _lastSwitchTime < SwitchCooldown) return;
        _lastSwitchTime = Time.time;

        abilitySystem.SwitchTarget(direction);
    }

    private static bool ShiftHeld()
    {
        bool legacy = false;
        try { legacy = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift); } catch (System.InvalidOperationException) { }
        if (legacy) return true;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.shiftKey.isPressed;
    }

    /// <summary>
    /// Commit 0：Revealed/Escaping 期间不允许通过切换伪装绕过附身门禁。
    /// </summary>
    private bool IsPossessionLockoutActive()
    {
        if (abilitySystem == null) return false;
        return abilitySystem.PossessionState == TricksterPossessionState.Revealed ||
               abilitySystem.PossessionState == TricksterPossessionState.Escaping;
    }

    /// <summary>
    /// 检查道具操控的失败原因，返回 null 表示可以操控
    /// </summary>
    private string GetAbilityFailReason()
    {
        if (TricksterKit.BlocksPranks) return "Too small to trigger props!";
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

        if (!abilitySystem.IsPossessionActionAllowed)
            return $"Possession gate blocked: {abilitySystem.PossessionState}";

        if (abilitySystem.BoundProp is PranksterCannon cannon && !cannon.HasAmmo)
            return "No cannonballs left! Stand inside the cannon to launch yourself.";

        if (!abilitySystem.BoundProp.CanBeControlled())
        {
            var state = abilitySystem.BoundProp.GetControlState();
            if (state == PropControlState.Cooldown)
                return $"Prop on cooldown!";
            if (state == PropControlState.Active || state == PropControlState.Telegraph || state == PropControlState.Recovery)
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
    [Tooltip("S182：左上角伪装调试状态行（第 1 步房间关掉）")]
    [SerializeField] private bool showDebugStatus = true;
    public void SetShowDebugStatus(bool value) => showDebugStatus = value;
    private void OnGUI()
    {
        if (!showDebugStatus || disguiseSystem == null) return;
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
