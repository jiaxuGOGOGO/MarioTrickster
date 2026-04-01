using UnityEngine;

/// <summary>
/// 移动平台 - 在两个点之间来回移动，站在上面的角色跟随
/// 
/// 使用方式:
///   1. 挂载到平台 GameObject，需要 BoxCollider2D
///   2. 设置 Point B（终点偏移），平台会在 A↔B 间来回移动
///   3. 在平台的 BoxCollider2D 上挂一个 PhysicsMaterial2D（Friction=0）防止边缘卡住
/// 
/// 边缘卡住修复 (Session 4):
///   - 根因：角色 Rigidbody2D 与平台侧面碰撞时，水平速度被物理引擎清零，
///     导致角色贴着平台侧面无法移动（即"卡住"现象）
///   - 修复：通过代码在运行时自动给平台 Collider 设置零摩擦材质，
///     同时给角色 Collider 也设置零摩擦材质，彻底消除侧面摩擦力
///   - 角色跟随改用位移推动（非 SetParent），避免 Rigidbody2D 物理异常
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

    private Transform ridingMario;
    private Transform ridingTrickster;

    // 零摩擦材质（运行时自动创建）
    private static PhysicsMaterial2D s_zeroFriction;

    private static PhysicsMaterial2D ZeroFriction
    {
        get
        {
            if (s_zeroFriction == null)
            {
                s_zeroFriction = new PhysicsMaterial2D("ZeroFriction");
                s_zeroFriction.friction = 0f;
                s_zeroFriction.bounciness = 0f;
            }
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

        // 给平台自身设置零摩擦，防止角色贴着侧面卡住
        GetComponent<BoxCollider2D>().sharedMaterial = ZeroFriction;
    }

    private void FixedUpdate()
    {
        if (isWaiting)
        {
            waitTimer -= Time.fixedDeltaTime;
            if (waitTimer <= 0) isWaiting = false;
            return;
        }

        Vector3 prev = transform.position;
        transform.position = Vector3.MoveTowards(transform.position, targetPoint, moveSpeed * Time.fixedDeltaTime);

        // 推动站在平台上的角色
        Vector3 delta = transform.position - prev;
        if (delta.sqrMagnitude > 0f)
        {
            if (ridingMario != null)    ridingMario.position    += delta;
            if (ridingTrickster != null) ridingTrickster.position += delta;
        }

        if (Vector3.Distance(transform.position, targetPoint) < 0.01f)
        {
            targetPoint = targetPoint == worldPointA ? worldPointB : worldPointA;
            isWaiting = true;
            waitTimer = waitTime;
        }
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        // 只响应从上方落下的碰撞
        foreach (ContactPoint2D c in col.contacts)
        {
            if (c.normal.y >= 0.5f)
            {
                TryRegisterRider(col);
                return;
            }
        }
    }

    private void OnCollisionExit2D(Collision2D col)
    {
        if (col.transform == ridingMario)    ridingMario    = null;
        if (col.transform == ridingTrickster) ridingTrickster = null;
    }

    private void TryRegisterRider(Collision2D col)
    {
        GameObject obj = col.gameObject;

        if (obj.GetComponent<MarioController>() != null)
        {
            // 给角色 Collider 也设置零摩擦，防止贴墙卡住
            ApplyZeroFriction(obj);
            ridingMario = col.transform;
            return;
        }

        TricksterController tc = obj.GetComponent<TricksterController>();
        if (tc != null)
        {
            // 伪装中的 Trickster 不被平台携带
            DisguiseSystem ds = obj.GetComponent<DisguiseSystem>();
            if (ds != null && ds.IsDisguised) return;

            ApplyZeroFriction(obj);
            ridingTrickster = col.transform;
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
