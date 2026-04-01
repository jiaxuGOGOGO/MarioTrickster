using UnityEngine;

/// <summary>
/// 移动平台
/// 功能: 在两个点之间来回移动的平台，站在上面的角色跟随移动
/// 使用方式: 挂载到平台GameObject上，设置起点和终点
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

        // 到达目标点
        if (Vector3.Distance(transform.position, targetPoint) < 0.01f)
        {
            // 切换目标
            targetPoint = (targetPoint == worldPointA) ? worldPointB : worldPointA;
            isWaiting = true;
            waitTimer = waitTime;
        }
    }

    // 角色站在平台上时跟随移动
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.GetComponent<MarioController>() != null ||
            collision.gameObject.GetComponent<TricksterController>() != null)
        {
            collision.transform.SetParent(transform);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.GetComponent<MarioController>() != null ||
            collision.gameObject.GetComponent<TricksterController>() != null)
        {
            collision.transform.SetParent(null);
        }
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
