using UnityEngine;

/// <summary>
/// 移动平台 - 在两个点之间来回移动，站在上面的角色跟随
///
/// 跟随方案：Kinematic Rigidbody2D + MovePosition 移动平台
///           角色从上方落到平台时 SetParent，离开时取消
///           SetParent 保证跟随零延迟，角色自身 rb.velocity 控制相对走动
///
/// 使用方式：
///   1. 挂载到平台 GameObject（自动添加 Rigidbody2D 和 BoxCollider2D）
///   2. Inspector 设置 Point B（相对起点的偏移），平台在起点 ↔ B 间来回
///   3. 无需手动配置 Rigidbody2D，脚本自动设为 Kinematic
/// </summary>
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class MovingPlatform : MonoBehaviour
{
    [Header("移动设置")]
    [Tooltip("终点相对于起点的偏移（如 (5,0,0) = 向右移动5格）")]
    [SerializeField] private Vector3 pointB = new Vector3(5f, 0f, 0f);
    [SerializeField] private float moveSpeed = 2f;
    [Tooltip("到达端点后的等待时间（秒）")]
    [SerializeField] private float waitTime = 0.5f;
    [Tooltip("是否从 B 点出发（默认从起点出发）")]
    [SerializeField] private bool startFromB = false;

    private Rigidbody2D rb;
    private Vector2 worldPointA;
    private Vector2 worldPointB;
    private Vector2 targetPoint;
    private float waitTimer;
    private bool isWaiting;

    // 全局共享的零摩擦材质（防止角色贴平台侧面卡住）
    private static PhysicsMaterial2D s_zeroFriction;
    private static PhysicsMaterial2D ZeroFriction
    {
        get
        {
            if (s_zeroFriction == null)
                s_zeroFriction = new PhysicsMaterial2D("ZeroFriction") { friction = 0f, bounciness = 0f };
            return s_zeroFriction;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        GetComponent<BoxCollider2D>().sharedMaterial = ZeroFriction;
    }

    private void Start()
    {
        worldPointA = transform.position;
        worldPointB = (Vector2)transform.position + (Vector2)pointB;

        if (startFromB)
        {
            rb.position = worldPointB;
            targetPoint = worldPointA;
        }
        else
        {
            targetPoint = worldPointB;
        }
    }

    private void FixedUpdate()
    {
        if (isWaiting)
        {
            waitTimer -= Time.fixedDeltaTime;
            if (waitTimer <= 0f) isWaiting = false;
            return;
        }

        Vector2 newPos = Vector2.MoveTowards(rb.position, targetPoint, moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);

        if (Vector2.Distance(newPos, targetPoint) < 0.01f)
        {
            targetPoint = targetPoint == worldPointA ? worldPointB : worldPointA;
            isWaiting = true;
            waitTimer = waitTime;
        }
    }

    // ── 角色跟随：从上方落到平台时 SetParent ──────────────────

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (!IsRidingFromAbove(col)) return;
        col.transform.SetParent(transform);
    }

    private void OnCollisionExit2D(Collision2D col)
    {
        if (col.transform.parent == transform)
            col.transform.SetParent(null);
    }

    /// <summary>角色是否从平台上方落下（避免侧面碰撞也触发跟随）</summary>
    private bool IsRidingFromAbove(Collision2D col)
    {
        // 碰撞点的法线朝上（角色在平台上方）
        foreach (ContactPoint2D contact in col.contacts)
        {
            if (contact.normal.y < -0.5f) return true;
        }
        return false;
    }

    // ── 编辑器可视化 ──────────────────────────────────────

    private void OnDrawGizmos()
    {
        Vector3 a = Application.isPlaying ? (Vector3)worldPointA : transform.position;
        Vector3 b = Application.isPlaying ? (Vector3)worldPointB : transform.position + pointB;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(a, 0.2f);
        Gizmos.DrawWireSphere(b, 0.2f);
    }
}
