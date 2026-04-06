using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 移动平台 - 在两个点之间来回移动，站在上面的角色跟随
///
/// 跟随方案：速度注入法（不使用 SetParent）
///   根本原因：Dynamic Rigidbody2D 的 rb.velocity 是世界坐标系绝对速度，
///   SetParent 改变 Transform 层级但物理引擎不理解层级关系，
///   因此 SetParent 无法让角色跟随 Kinematic 平台移动。
///
///   正确做法：
///   1. 平台在 FixedUpdate 中用 rb.MovePosition() 移动（Kinematic）
///   2. 检测站在平台上的角色（OnCollisionStay2D）
///   3. 每帧把平台速度注入角色（调用 SetPlatformVelocity()）
///   4. 角色控制器在自身 FixedUpdate 最后叠加平台速度
///   执行顺序：平台 [DefaultExecutionOrder(-10)] 先于角色控制器执行
///
/// 使用方式：
///   1. 挂载到平台 GameObject（自动添加 Rigidbody2D 和 BoxCollider2D）
///   2. Inspector 设置 Point B（相对起点的偏移），平台在起点 ↔ B 间来回
///   3. 无需手动配置 Rigidbody2D，脚本自动设为 Kinematic
/// </summary>
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[SelectionBase] // S37 视碰分离: 确保框选时选中 Root 而非 Visual 子节点
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

    // 本帧平台实际移动的速度（用于注入角色）
    private Vector2 _currentVelocity;

    // 当前站在平台上的角色列表（用 HashSet 避免重复）
    private readonly HashSet<GameObject> _riders = new HashSet<GameObject>();

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
            _currentVelocity = Vector2.zero;
            waitTimer -= Time.fixedDeltaTime;
            if (waitTimer <= 0f) isWaiting = false;
        }
        else
        {
            // 计算本帧移动量
            Vector2 prevPos = rb.position;
            Vector2 newPos = Vector2.MoveTowards(rb.position, targetPoint, moveSpeed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);

            // 平台实际速度 = 位移 / 时间
            _currentVelocity = (newPos - prevPos) / Time.fixedDeltaTime;

            if (Vector2.Distance(newPos, targetPoint) < 0.01f)
            {
                targetPoint = targetPoint == worldPointA ? worldPointB : worldPointA;
                isWaiting = true;
                waitTimer = waitTime;
            }
        }

        // 将平台速度注入所有站在上面的角色
        InjectVelocityToRiders();
    }

    // ── 速度注入 ──────────────────────────────────────────

    private void InjectVelocityToRiders()
    {
        if (_currentVelocity.sqrMagnitude < 0.0001f) return;

        foreach (GameObject rider in _riders)
        {
            if (rider == null) continue;

            // 尝试注入 MarioController
            MarioController mario = rider.GetComponent<MarioController>();
            if (mario != null)
            {
                mario.SetPlatformVelocity(_currentVelocity);
                continue;
            }

            // 尝试注入 TricksterController
            TricksterController trickster = rider.GetComponent<TricksterController>();
            if (trickster != null)
            {
                trickster.SetPlatformVelocity(_currentVelocity);
            }
        }
    }

    // ── 角色检测：从上方落到平台时加入 _riders ──────────────

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (IsRidingFromAbove(col))
            _riders.Add(col.gameObject);
    }

    private void OnCollisionStay2D(Collision2D col)
    {
        // Stay 确保角色在平台上滑动时持续注入
        if (IsRidingFromAbove(col))
            _riders.Add(col.gameObject);
    }

    private void OnCollisionExit2D(Collision2D col)
    {
        _riders.Remove(col.gameObject);
    }

    /// <summary>角色是否从平台上方落下（避免侧面碰撞也触发跟随）</summary>
    private bool IsRidingFromAbove(Collision2D col)
    {
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
