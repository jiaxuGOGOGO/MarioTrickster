using UnityEngine;

/// <summary>
/// 简单巡逻敌人 - MVP关卡填充用
/// 功能: 在两点间巡逻，碰到墙壁或边缘自动转向，Mario踩头可消灭
/// 参考: zigurous Super Mario Tutorial 的 Goomba 实现
/// 使用方式: 挂载到敌人GameObject上，需要 Rigidbody2D + BoxCollider2D + SpriteRenderer
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class SimpleEnemy : MonoBehaviour
{
    [Header("=== 移动 ===")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private bool startMovingRight = false;

    [Header("=== 边缘检测 ===")]
    [SerializeField] private bool detectEdge = true;
    [SerializeField] private float edgeCheckDistance = 1f;
    [SerializeField] private LayerMask groundLayer;

    [Header("=== 墙壁检测 ===")]
    [SerializeField] private float wallCheckDistance = 0.3f;

    [Header("=== 被踩消灭 ===")]
    [SerializeField] private bool canBeStomped = true;
    [SerializeField] private float stompBounceForce = 10f;

    // 组件
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;

    // 状态
    private int moveDirection;
    private bool isDead;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        rb.gravityScale = 3f;
        rb.freezeRotation = true;

        moveDirection = startMovingRight ? 1 : -1;
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        // 移动
        rb.velocity = new Vector2(moveDirection * moveSpeed, rb.velocity.y);

        // 边缘检测
        if (detectEdge && IsAtEdge())
        {
            Flip();
        }

        // 墙壁检测
        if (IsAtWall())
        {
            Flip();
        }

        // 更新朝向
        spriteRenderer.flipX = moveDirection > 0;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;

        // 检查是否被Mario踩头
        if (canBeStomped)
        {
            MarioController mario = collision.gameObject.GetComponent<MarioController>();
            if (mario != null)
            {
                // 判断Mario是否从上方踩下来
                foreach (ContactPoint2D contact in collision.contacts)
                {
                    if (contact.normal.y < -0.5f) // Mario在上方
                    {
                        // 被踩死
                        Die();
                        // 给Mario一个弹跳
                        mario.Bounce(stompBounceForce);
                        return;
                    }
                }

                // 不是踩头，对Mario造成伤害
                PlayerHealth health = collision.gameObject.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    health.TakeDamage(1);
                }
            }
        }
    }

    #region 检测方法

    private bool IsAtEdge()
    {
        Vector2 origin = new Vector2(
            boxCollider.bounds.center.x + moveDirection * boxCollider.bounds.extents.x,
            boxCollider.bounds.min.y
        );

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, edgeCheckDistance, groundLayer);
        return hit.collider == null;
    }

    private bool IsAtWall()
    {
        Vector2 origin = (Vector2)boxCollider.bounds.center;
        Vector2 direction = new Vector2(moveDirection, 0);

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, boxCollider.bounds.extents.x + wallCheckDistance, groundLayer);
        return hit.collider != null;
    }

    #endregion

    #region 状态方法

    private void Flip()
    {
        moveDirection *= -1;
    }

    private void Die()
    {
        isDead = true;
        rb.velocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;

        // 压扁动画（简单缩放）
        transform.localScale = new Vector3(transform.localScale.x, 0.2f, transform.localScale.z);

        // 禁用碰撞
        boxCollider.enabled = false;

        // 延迟销毁
        Destroy(gameObject, 0.5f);

        Debug.Log($"[SimpleEnemy] {gameObject.name} 被消灭！");
    }

    #endregion

    #region 调试可视化

    private void OnDrawGizmosSelected()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        // 边缘检测射线
        if (detectEdge)
        {
            Gizmos.color = Color.red;
            Vector2 edgeOrigin = new Vector2(
                col.bounds.center.x + (startMovingRight ? 1 : -1) * col.bounds.extents.x,
                col.bounds.min.y
            );
            Gizmos.DrawLine(edgeOrigin, edgeOrigin + Vector2.down * edgeCheckDistance);
        }

        // 墙壁检测射线
        Gizmos.color = Color.blue;
        Vector2 wallOrigin = (Vector2)col.bounds.center;
        Vector2 wallDir = new Vector2(startMovingRight ? 1 : -1, 0);
        Gizmos.DrawLine(wallOrigin, wallOrigin + wallDir * (col.bounds.extents.x + wallCheckDistance));
    }

    #endregion
}
