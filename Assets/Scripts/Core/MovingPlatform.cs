using UnityEngine;

/// <summary>
/// 移动平台
/// 功能: 在两个点之间来回移动的平台，站在上面的角色跟随移动
/// 使用方式: 挂载到平台GameObject上，设置起点和终点
/// 
/// 修复说明 (Session 4):
///   - 改用位移跟随替代 SetParent，避免 Rigidbody2D 被 parent 影响导致物理异常
///   - 排除处于伪装状态的 Trickster，防止平台吸附变身后的 Trickster
/// </summary>
public class MovingPlatform : MonoBehaviour
{
    [Header("=== 移动设置 ===")]
    [SerializeField] private Vector3 pointA = Vector3.zero;
    [SerializeField] private Vector3 pointB = new Vector3(5f, 0f, 0f);
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float waitTime = 0.5f;

    [Header("=== 选项 ===")]
    [SerializeField] private bool useLocalSpace = true; // 使用相对坐标
    [SerializeField] private bool startFromB = false;

    private Vector3 worldPointA;
    private Vector3 worldPointB;
    private Vector3 targetPoint;
    private float waitTimer;
    private bool isWaiting;

    // 上一帧位置，用于计算平台位移
    private Vector3 lastPosition;

    // 当前站在平台上的角色（最多同时支持两个玩家）
    private Transform ridingMario;
    private Transform ridingTrickster;

    private void Start()
    {
        if (useLocalSpace)
        {
            worldPointA = transform.position + pointA;
            worldPointB = transform.position + pointB;
        }
        else
        {
            worldPointA = pointA;
            worldPointB = pointB;
        }

        if (startFromB)
        {
            transform.position = worldPointB;
            targetPoint = worldPointA;
        }
        else
        {
            transform.position = worldPointA;
            targetPoint = worldPointB;
        }

        lastPosition = transform.position;
    }

    private void FixedUpdate()
    {
        if (isWaiting)
        {
            waitTimer -= Time.fixedDeltaTime;
            if (waitTimer <= 0)
            {
                isWaiting = false;
            }
            return;
        }

        // 移动
        transform.position = Vector3.MoveTowards(transform.position, targetPoint, moveSpeed * Time.fixedDeltaTime);

        // 计算本帧位移，推动站在平台上的角色
        Vector3 delta = transform.position - lastPosition;
        if (delta.sqrMagnitude > 0.000001f)
        {
            if (ridingMario != null)
                ridingMario.position += delta;

            if (ridingTrickster != null)
                ridingTrickster.position += delta;
        }
        lastPosition = transform.position;

        // 到达目标点
        if (Vector3.Distance(transform.position, targetPoint) < 0.01f)
        {
            targetPoint = (targetPoint == worldPointA) ? worldPointB : worldPointA;
            isWaiting = true;
            waitTimer = waitTime;
        }
    }

    // 判断是否应该让该角色站上平台
    // 关键修复：Trickster 处于伪装状态时不应被平台吸附
    private bool ShouldRide(GameObject obj, out bool isMario, out bool isTrickster)
    {
        isMario = false;
        isTrickster = false;

        MarioController mario = obj.GetComponent<MarioController>();
        if (mario != null)
        {
            isMario = true;
            return true;
        }

        TricksterController trickster = obj.GetComponent<TricksterController>();
        if (trickster != null)
        {
            // 如果 Trickster 正处于伪装状态，不让它被平台吸附
            DisguiseSystem disguise = obj.GetComponent<DisguiseSystem>();
            if (disguise != null && disguise.IsDisguised)
            {
                // 伪装中的 Trickster 不应被平台携带
                return false;
            }
            isTrickster = true;
            return true;
        }

        return false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 只处理从上方站上平台的情况（碰撞法线朝上）
        bool fromAbove = false;
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y < -0.5f) // 平台法线向下 = 角色在平台上方
            {
                fromAbove = true;
                break;
            }
        }
        if (!fromAbove) return;

        bool isMario, isTrickster;
        if (ShouldRide(collision.gameObject, out isMario, out isTrickster))
        {
            if (isMario)
                ridingMario = collision.transform;
            else if (isTrickster)
                ridingTrickster = collision.transform;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.transform == ridingMario)
            ridingMario = null;
        else if (collision.transform == ridingTrickster)
            ridingTrickster = null;
    }

    #region 调试可视化

    private void OnDrawGizmos()
    {
        Vector3 a, b;
        if (Application.isPlaying)
        {
            a = worldPointA;
            b = worldPointB;
        }
        else
        {
            a = useLocalSpace ? transform.position + pointA : pointA;
            b = useLocalSpace ? transform.position + pointB : pointB;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(a, 0.2f);
        Gizmos.DrawWireSphere(b, 0.2f);
    }

    #endregion
}
