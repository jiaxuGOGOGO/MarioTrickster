using UnityEngine;

/// <summary>
/// 移动平台 - 在两个点之间来回移动，站在上面的角色跟随
///
/// 跟随方案（速度注入）：
///   每帧把平台速度通过 SetPlatformVelocity() 注入到角色控制器，
///   角色控制器在 FixedUpdate 里把平台速度叠加到自身速度上。
///   角色既能随平台移动，也能在平台上自由走动，两者不冲突。
///
/// 使用方式：
///   1. 挂载到平台 GameObject，需要 BoxCollider2D
///   2. 在 Inspector 设置 Point B（相对于起点的偏移），平台在起点 ↔ B 间来回移动
/// </summary>
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

    private Vector3 worldPointA;
    private Vector3 worldPointB;
    private Vector3 targetPoint;
    private float waitTimer;
    private bool isWaiting;

    private Vector2 currentPlatformVelocity;

    private MarioController ridingMario;
    private TricksterController ridingTrickster;

    // 全局共享的零摩擦材质
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

    private void Start()
    {
        worldPointA = transform.position;
        worldPointB = transform.position + pointB;

        if (startFromB)
        {
            transform.position = worldPointB;
            targetPoint = worldPointA;
        }
        else
        {
            targetPoint = worldPointB;
        }

        GetComponent<BoxCollider2D>().sharedMaterial = ZeroFriction;
    }

    private void FixedUpdate()
    {
        if (isWaiting)
        {
            waitTimer -= Time.fixedDeltaTime;
            if (waitTimer <= 0) isWaiting = false;
            currentPlatformVelocity = Vector2.zero;
            return;
        }

        Vector3 prev = transform.position;
        transform.position = Vector3.MoveTowards(transform.position, targetPoint, moveSpeed * Time.fixedDeltaTime);

        Vector3 delta = transform.position - prev;
        currentPlatformVelocity = new Vector2(delta.x, delta.y) / Time.fixedDeltaTime;

        // 注入平台速度到站在上面的角色
        if (ridingMario != null)     ridingMario.SetPlatformVelocity(currentPlatformVelocity);
        if (ridingTrickster != null) ridingTrickster.SetPlatformVelocity(currentPlatformVelocity);

        if (Vector3.Distance(transform.position, targetPoint) < 0.01f)
        {
            targetPoint = targetPoint == worldPointA ? worldPointB : worldPointA;
            isWaiting = true;
            waitTimer = waitTime;
        }
    }

    // ── 碰撞注册 ──────────────────────────────────────────
    // 同时监听 Enter 和 Stay，防止物理帧对齐不好时漏掉注册

    private void OnCollisionEnter2D(Collision2D col) => TryRegisterRider(col);
    private void OnCollisionStay2D(Collision2D col)  => TryRegisterRider(col);

    private void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.GetComponent<MarioController>() == ridingMario)
            ridingMario = null;
        if (col.gameObject.GetComponent<TricksterController>() == ridingTrickster)
            ridingTrickster = null;
    }

    private void TryRegisterRider(Collision2D col)
    {
        // 只响应从上方落下的碰撞（接触法线朝上）
        bool fromAbove = false;
        foreach (ContactPoint2D c in col.contacts)
        {
            if (c.normal.y >= 0.5f) { fromAbove = true; break; }
        }
        if (!fromAbove) return;

        GameObject obj = col.gameObject;

        MarioController mario = obj.GetComponent<MarioController>();
        if (mario != null)
        {
            EnsureZeroFriction(obj);
            ridingMario = mario;
            return;
        }

        TricksterController tc = obj.GetComponent<TricksterController>();
        if (tc != null)
        {
            // 伪装状态下的 Trickster 不被平台携带
            DisguiseSystem ds = obj.GetComponent<DisguiseSystem>();
            if (ds != null && ds.IsDisguised) return;
            EnsureZeroFriction(obj);
            ridingTrickster = tc;
        }
    }

    private static void EnsureZeroFriction(GameObject obj)
    {
        Collider2D col = obj.GetComponent<Collider2D>();
        if (col != null && col.sharedMaterial == null)
            col.sharedMaterial = ZeroFriction;
    }

    // ── 编辑器可视化 ──────────────────────────────────────

    private void OnDrawGizmos()
    {
        Vector3 a = Application.isPlaying ? worldPointA : transform.position;
        Vector3 b = Application.isPlaying ? worldPointB : transform.position + pointB;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(a, 0.2f);
        Gizmos.DrawWireSphere(b, 0.2f);
    }
}
