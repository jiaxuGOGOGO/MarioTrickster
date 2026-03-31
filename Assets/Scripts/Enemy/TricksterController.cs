using UnityEngine;

/// <summary>
/// Trickster（伪装者）控制器 - MVP核心脚本
/// 功能: 基础移动、跳跃、触发伪装、与DisguiseSystem协作
/// 伪装者可以自由移动，按下伪装键后变身为场景物体
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class TricksterController : MonoBehaviour
{
    [Header("=== 移动参数 ===")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float gravityScale = 3f;

    [Header("=== 地面检测 ===")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.1f;

    [Header("=== 伪装状态下的移动限制 ===")]
    [Tooltip("伪装状态下的移动速度倍率（0=完全不能动，1=正常速度）")]
    [SerializeField] private float disguisedMoveMultiplier = 0.15f;
    [Tooltip("伪装状态下能否跳跃")]
    [SerializeField] private bool canJumpWhileDisguised = false;

    // 组件
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;
    private DisguiseSystem disguiseSystem;

    // 输入
    private Vector2 moveInput;
    private bool jumpPressed;

    // 状态
    private bool isGrounded;
    private bool isFacingRight = true;

    // 公共属性
    public bool IsGrounded => isGrounded;
    public bool IsDisguised => disguiseSystem != null && disguiseSystem.IsDisguised;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        disguiseSystem = GetComponent<DisguiseSystem>();

        rb.gravityScale = gravityScale;
        rb.freezeRotation = true;
    }

    private void Update()
    {
        isGrounded = CheckGrounded();

        // 跳跃
        bool canJump = isGrounded && (!IsDisguised || canJumpWhileDisguised);
        if (jumpPressed && canJump)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpPressed = false;
        }

        // 朝向（仅非伪装状态）
        if (!IsDisguised)
        {
            UpdateFacing();
        }
    }

    private void FixedUpdate()
    {
        float speedMultiplier = IsDisguised ? disguisedMoveMultiplier : 1f;
        float targetSpeed = moveInput.x * moveSpeed * speedMultiplier;
        rb.linearVelocity = new Vector2(targetSpeed, rb.linearVelocity.y);
    }

    #region 输入回调（由InputManager调用）

    public void SetMoveInput(Vector2 input)
    {
        moveInput = input;
    }

    public void OnJumpPressed()
    {
        jumpPressed = true;
    }

    public void OnJumpReleased()
    {
        // Trickster暂不需要变高跳跃
    }

    /// <summary>伪装键按下</summary>
    public void OnDisguisePressed()
    {
        if (disguiseSystem != null)
        {
            disguiseSystem.ToggleDisguise();
        }
    }

    /// <summary>切换伪装形态（下一个/上一个）</summary>
    public void OnSwitchDisguise(float direction)
    {
        if (disguiseSystem != null && !disguiseSystem.IsDisguised)
        {
            if (direction > 0)
                disguiseSystem.NextDisguise();
            else
                disguiseSystem.PreviousDisguise();
        }
    }

    #endregion

    #region 内部方法

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
            isFacingRight = true;
            spriteRenderer.flipX = false;
        }
        else if (moveInput.x < -0.01f && isFacingRight)
        {
            isFacingRight = false;
            spriteRenderer.flipX = true;
        }
    }

    #endregion
}
