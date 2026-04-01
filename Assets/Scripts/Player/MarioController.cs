using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mario 玩家控制器 - MVP核心脚本
/// 参考: Ultimate-2D-Controller (Tarodev) + zigurous Super Mario Tutorial
/// 功能: 水平移动、跳跃（含coyote time和buffered jump）、地面检测、死亡
/// 使用方式: 挂载到Mario预制体，需要 Rigidbody2D + BoxCollider2D + SpriteRenderer
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class MarioController : MonoBehaviour
{
    [Header("=== 移动参数 ===")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 50f;
    [SerializeField] private float deceleration = 50f;
    [SerializeField] private float airAcceleration = 30f;

    [Header("=== 跳跃参数 ===")]
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private float gravityScale = 3f;
    [SerializeField] private float fallGravityMultiplier = 1.5f;
    [SerializeField] private float maxFallSpeed = 20f;

    [Header("=== 高级跳跃手感 ===")]
    [Tooltip("离开地面后仍可跳跃的宽限时间")]
    [SerializeField] private float coyoteTime = 0.12f;
    [Tooltip("落地前提前按跳跃的缓冲时间")]
    [SerializeField] private float jumpBufferTime = 0.15f;
    [Tooltip("松开跳跃键时的速度衰减系数")]
    [SerializeField] private float jumpCutMultiplier = 0.4f;

    [Header("=== 地面检测 ===")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.1f;

    // 组件引用
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    // 输入
    private Vector2 moveInput;
    private bool jumpPressed;
    private bool jumpHeld;

    // 状态
    private bool isGrounded;
    private bool wasGrounded;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool isJumping;
    private bool isFacingRight = true;

    // 公共属性（供其他脚本读取）
    public bool IsGrounded => isGrounded;
    public bool IsMoving => Mathf.Abs(rb.velocity.x) > 0.1f;
    public bool IsFacingRight => isFacingRight;
    public Vector2 Velocity => rb.velocity;

    // 事件
    public System.Action OnJump;
    public System.Action OnLand;
    public System.Action OnDeath;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>(); // 可选，没有也不报错

        rb.gravityScale = gravityScale;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void Update()
    {
        // 计时器递减
        coyoteTimer -= Time.deltaTime;
        jumpBufferTimer -= Time.deltaTime;

        // 地面检测
        wasGrounded = isGrounded;
        isGrounded = CheckGrounded();

        // 刚着地
        if (isGrounded && !wasGrounded)
        {
            OnLand?.Invoke();
            isJumping = false;
        }

        // 刚离地（非跳跃导致）→ 启动coyote timer
        if (!isGrounded && wasGrounded && !isJumping)
        {
            coyoteTimer = coyoteTime;
        }

        // 跳跃缓冲
        if (jumpPressed)
        {
            jumpBufferTimer = jumpBufferTime;
            jumpPressed = false;
        }

        // 执行跳跃
        if (jumpBufferTimer > 0 && (isGrounded || coyoteTimer > 0))
        {
            ExecuteJump();
        }

        // 跳跃高度控制（松开跳跃键时削减上升速度）
        if (!jumpHeld && rb.velocity.y > 0 && isJumping)
        {
            rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * jumpCutMultiplier);
            isJumping = false;
        }

        // 翻转朝向
        UpdateFacing();

        // 更新动画
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        // 水平移动
        ApplyMovement();

        // 下落加速
        ApplyFallGravity();

        // 限制下落速度
        if (rb.velocity.y < -maxFallSpeed)
        {
            rb.velocity = new Vector2(rb.velocity.x, -maxFallSpeed);
        }
    }

    #region 输入回调（由InputManager调用）

    /// <summary>由InputManager调用，传入移动输入</summary>
    public void SetMoveInput(Vector2 input)
    {
        moveInput = input;
    }

    /// <summary>由InputManager调用，跳跃键按下</summary>
    public void OnJumpPressed()
    {
        jumpPressed = true;
        jumpHeld = true;
    }

    /// <summary>由InputManager调用，跳跃键松开</summary>
    public void OnJumpReleased()
    {
        jumpHeld = false;
    }

    #endregion

    #region 核心逻辑

    private void ApplyMovement()
    {
        float targetSpeed = moveInput.x * moveSpeed;
        float currentSpeed = rb.velocity.x;
        float accel = isGrounded ? acceleration : airAcceleration;

        // 判断加速还是减速
        if (Mathf.Abs(targetSpeed) > 0.01f)
        {
            // 加速
            float newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.fixedDeltaTime);
            rb.velocity = new Vector2(newSpeed, rb.velocity.y);
        }
        else
        {
            // 减速
            float newSpeed = Mathf.MoveTowards(currentSpeed, 0, deceleration * Time.fixedDeltaTime);
            rb.velocity = new Vector2(newSpeed, rb.velocity.y);
        }
    }

    private void ExecuteJump()
    {
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        isJumping = true;
        coyoteTimer = 0;
        jumpBufferTimer = 0;
        OnJump?.Invoke();
    }

    private void ApplyFallGravity()
    {
        if (rb.velocity.y < 0)
        {
            rb.gravityScale = gravityScale * fallGravityMultiplier;
        }
        else
        {
            rb.gravityScale = gravityScale;
        }
    }

    private bool CheckGrounded()
    {
        Vector2 boxCenter = (Vector2)boxCollider.bounds.center;
        Vector2 boxSize = new Vector2(boxCollider.bounds.size.x * 0.9f, groundCheckDistance);
        Vector2 origin = new Vector2(boxCenter.x, boxCollider.bounds.min.y);

        RaycastHit2D hit = Physics2D.BoxCast(origin, boxSize, 0f, Vector2.down, groundCheckDistance, groundLayer);
        return hit.collider != null;
    }

    private void UpdateFacing()
    {
        if (moveInput.x > 0.01f && !isFacingRight)
        {
            Flip();
        }
        else if (moveInput.x < -0.01f && isFacingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        spriteRenderer.flipX = !isFacingRight;
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetBool("IsGrounded", isGrounded);
        animator.SetFloat("Speed", Mathf.Abs(rb.velocity.x));
        animator.SetFloat("VerticalSpeed", rb.velocity.y);
    }

    #endregion

    #region 公共方法

    /// <summary>Mario死亡</summary>
    public void Die()
    {
        OnDeath?.Invoke();
        rb.velocity = Vector2.zero;
        rb.AddForce(Vector2.up * jumpForce * 0.5f, ForceMode2D.Impulse);
        enabled = false; // 禁用控制
    }

    /// <summary>被踩弹跳（踩敌人后的小跳）</summary>
    public void Bounce(float bounceForce = 10f)
    {
        rb.velocity = new Vector2(rb.velocity.x, bounceForce);
    }

    #endregion
}
