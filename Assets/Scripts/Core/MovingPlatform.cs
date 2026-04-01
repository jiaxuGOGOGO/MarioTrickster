using UnityEngine;

/// <summary>
/// 移动平台 - 在两个点之间来回移动，站在上面的角色跟随
/// 
/// 跟随方案（速度注入）:
///   每帧把平台速度通过 SetPlatformVelocity() 注入到角色控制器，
///   角色控制器在 FixedUpdate 里把平台速度叠加到自身速度上。
///   这样角色既能随平台移动，也能在平台上自由走动（两者不冲突）。
/// 
/// 使用方式:
///   1. 挂载到平台 GameObject，需要 BoxCollider2D
///   2. 设置 Point B（终点偏移），平台会在起点↔B 间来回移动
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class MovingPlatform : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private Vector3 pointB = new Vector3(5f, 0f, 0f);
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float waitTime = 0.5f;
    [SerializeField] private bool startFromB = false;

    private Vector3 worldPointA;
    private Vector3 worldPointB;
    private Vector3 targetPoint;
    private float waitTimer;
    private bool isWaiting;

    // 本帧平台速度（由 FixedUpdate 计算后注入角色）
    private Vector2 currentPlatformVelocity;

    private MarioController ridingMario;
    private TricksterController ridingTrickster;

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
        targetPoint = startFromB ? worldPointA : worldPointB;
        if (startFromB) transform.position = worldPointB;

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

        // 计算本帧实际速度，注入到站在平台上的角色
        Vector3 delta = transform.position - prev;
        currentPlatformVelocity = new Vector2(delta.x, delta.y) / Time.fixedDeltaTime;

        if (ridingMario != null)
            ridingMario.SetPlatformVelocity(currentPlatformVelocity);
        if (ridingTrickster != null)
            ridingTrickster.SetPlatformVelocity(currentPlatformVelocity);

        if (Vector3.Distance(transform.position, targetPoint) < 0.01f)
        {
            targetPoint = targetPoint == worldPointA ? worldPointB : worldPointA;
            isWaiting = true;
            waitTimer = waitTime;
        }
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        foreach (ContactPoint2D c in col.contacts)
        {
            // 接触法线朝上 = 角色在平台顶部
            if (c.normal.y >= 0.5f)
            {
                TryRegisterRider(col);
                return;
            }
        }
    }

    private void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.GetComponent<MarioController>() == ridingMario)
            ridingMario = null;
        if (col.gameObject.GetComponent<TricksterController>() == ridingTrickster)
            ridingTrickster = null;
    }

    private void TryRegisterRider(Collision2D col)
    {
        MarioController mario = col.gameObject.GetComponent<MarioController>();
        if (mario != null)
        {
            ApplyZeroFriction(col.gameObject);
            ridingMario = mario;
            return;
        }

        TricksterController tc = col.gameObject.GetComponent<TricksterController>();
        if (tc != null)
        {
            DisguiseSystem ds = col.gameObject.GetComponent<DisguiseSystem>();
            if (ds != null && ds.IsDisguised) return;
            ApplyZeroFriction(col.gameObject);
            ridingTrickster = tc;
        }
    }

    private static void ApplyZeroFriction(GameObject obj)
    {
        Collider2D col = obj.GetComponent<Collider2D>();
        if (col != null && col.sharedMaterial == null)
            col.sharedMaterial = ZeroFriction;
    }

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
