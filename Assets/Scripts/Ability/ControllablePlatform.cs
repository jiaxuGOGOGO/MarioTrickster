using UnityEngine;

/// <summary>
/// 可操控移动平台 - Trickster 变身后可操控的关卡道具
/// 参考: Crawl 的 Spike Block Trap（选择方向发射）
/// 
/// 操控效果:
///   - 预警阶段: 平台变红闪烁 + 轻微震动，给 Mario 0.8秒反应时间
///   - 激活阶段: 平台突然加速冲向 Trickster 指定的方向，或突然坠落
///   - 冷却阶段: 平台缓慢回到原始路径
/// 
/// 操控模式（可在 Inspector 中选择）:
///   - Rush:     平台朝指定方向猛冲（适合水平平台）
///   - Drop:     平台突然坠落（适合悬空平台）
///   - Reverse:  平台反向移动（适合循环平台）
///   - Stop:     平台突然停止（适合正在移动的平台，让 Mario 跳空）
/// 
/// 使用方式:
///   1. 在场景中创建平台 GameObject
///   2. 挂载此脚本（替代 MovingPlatform）
///   3. 配置移动路径和操控模式
///   4. Trickster 变身后按操控键即可触发
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ControllablePlatform : ControllablePropBase
{
    [Header("=== 平台移动配置 ===")]
    [SerializeField] private Vector3 pointA = Vector3.zero;
    [SerializeField] private Vector3 pointB = new Vector3(5f, 0f, 0f);
    [SerializeField] private float normalMoveSpeed = 2f;
    [SerializeField] private float waitTime = 0.5f;
    [SerializeField] private bool useLocalSpace = true;
    [SerializeField] private bool startFromB = false;

    [Header("=== 操控模式 ===")]
    [SerializeField] private PlatformControlMode controlMode = PlatformControlMode.Rush;

    [Header("=== Rush 模式参数 ===")]
    [Tooltip("冲刺速度倍率")]
    [SerializeField] private float rushSpeedMultiplier = 5f;
    [Tooltip("冲刺距离")]
    [SerializeField] private float rushDistance = 3f;

    [Header("=== Drop 模式参数 ===")]
    [Tooltip("坠落速度")]
    [SerializeField] private float dropSpeed = 10f;
    [Tooltip("坠落后恢复时间（秒）")]
    [SerializeField] private float dropRecoverTime = 2f;

    [Header("=== Reverse 模式参数 ===")]
    [Tooltip("反向移动速度倍率")]
    [SerializeField] private float reverseSpeedMultiplier = 2f;

    // 平台移动状态
    private Vector3 worldPointA;
    private Vector3 worldPointB;
    private Vector3 targetPoint;
    private float waitTimer;
    private bool isWaiting;

    // 操控状态
    private bool isControlled;      // 是否正在被操控（激活阶段）
    private Vector3 rushDirection;   // Rush 模式的冲刺方向
    private Vector3 preControlPosition; // 操控前的位置（用于恢复）
    private float dropOriginalY;     // Drop 模式的原始 Y 坐标

    protected override void Awake()
    {
        base.Awake();
        propName = "移动平台";
    }

    private void Start()
    {
        // 初始化路径点（与 MovingPlatform 相同逻辑）
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
        if (isControlled)
        {
            // 操控中：执行操控移动
            UpdateControlledMovement();
        }
        else
        {
            // 正常移动
            UpdateNormalMovement();
        }
    }

    #region 正常移动（与 MovingPlatform 相同）

    private void UpdateNormalMovement()
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

        transform.position = Vector3.MoveTowards(transform.position, targetPoint, normalMoveSpeed * Time.fixedDeltaTime);

        if (Vector3.Distance(transform.position, targetPoint) < 0.01f)
        {
            targetPoint = (targetPoint == worldPointA) ? worldPointB : worldPointA;
            isWaiting = true;
            waitTimer = waitTime;
        }
    }

    #endregion

    #region 操控移动

    private void UpdateControlledMovement()
    {
        switch (controlMode)
        {
            case PlatformControlMode.Rush:
                // 朝指定方向冲刺
                transform.position += rushDirection * rushSpeedMultiplier * normalMoveSpeed * Time.fixedDeltaTime;
                break;

            case PlatformControlMode.Drop:
                // 向下坠落
                transform.position += Vector3.down * dropSpeed * Time.fixedDeltaTime;
                break;

            case PlatformControlMode.Reverse:
                // 反向移动（朝远离当前目标点的方向）
                Vector3 reverseTarget = (targetPoint == worldPointA) ? worldPointB : worldPointA;
                transform.position = Vector3.MoveTowards(
                    transform.position, reverseTarget,
                    normalMoveSpeed * reverseSpeedMultiplier * Time.fixedDeltaTime
                );
                break;

            case PlatformControlMode.Stop:
                // 完全停止（什么都不做）
                break;
        }
    }

    #endregion

    #region ControllablePropBase 实现

    protected override void OnTelegraphStart()
    {
        // 预警开始：记录当前位置
        preControlPosition = transform.position;

        // 可以在这里播放预警音效
        // AudioManager.Instance?.PlaySFX("platform_warning");
    }

    protected override void OnTelegraphEnd()
    {
        // 预警结束，准备激活
    }

    protected override void OnActivate(Vector2 direction)
    {
        isControlled = true;

        switch (controlMode)
        {
            case PlatformControlMode.Rush:
                // 计算冲刺方向
                if (direction.magnitude > 0.1f)
                {
                    rushDirection = new Vector3(direction.x, direction.y, 0f).normalized;
                }
                else
                {
                    // 没有输入方向时，朝远离当前目标的方向冲
                    rushDirection = (transform.position - targetPoint).normalized;
                }
                break;

            case PlatformControlMode.Drop:
                dropOriginalY = transform.position.y;
                break;

            case PlatformControlMode.Reverse:
                // 切换目标点
                targetPoint = (targetPoint == worldPointA) ? worldPointB : worldPointA;
                break;

            case PlatformControlMode.Stop:
                // 停止模式不需要额外设置
                break;
        }
    }

    protected override void OnActiveEnd()
    {
        isControlled = false;

        // 激活结束后的恢复逻辑
        switch (controlMode)
        {
            case PlatformControlMode.Drop:
                // 坠落后回到原始高度（平滑过渡由 Update 处理）
                StartCoroutine(RecoverFromDrop());
                break;

            case PlatformControlMode.Rush:
                // 冲刺结束后，重新计算最近的路径点继续正常移动
                float distToA = Vector3.Distance(transform.position, worldPointA);
                float distToB = Vector3.Distance(transform.position, worldPointB);
                targetPoint = distToA < distToB ? worldPointB : worldPointA;
                break;
        }
    }

    private System.Collections.IEnumerator RecoverFromDrop()
    {
        // 等待恢复时间
        yield return new WaitForSeconds(dropRecoverTime);

        // 平滑回到操控前的位置
        Vector3 startPos = transform.position;
        float elapsed = 0f;
        float recoverDuration = 1f;

        while (elapsed < recoverDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / recoverDuration;
            t = t * t * (3f - 2f * t); // SmoothStep
            transform.position = Vector3.Lerp(startPos, preControlPosition, t);
            yield return null;
        }

        transform.position = preControlPosition;

        // 恢复正常移动
        float distToA = Vector3.Distance(transform.position, worldPointA);
        float distToB = Vector3.Distance(transform.position, worldPointB);
        targetPoint = distToA < distToB ? worldPointB : worldPointA;
    }

    #endregion

    #region 角色跟随（与 MovingPlatform 相同）

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

    #endregion

    #region 调试可视化

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // 显示路径
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
        Gizmos.DrawWireSphere(a, 0.15f);
        Gizmos.DrawWireSphere(b, 0.15f);
    }

    #endregion
}

/// <summary>
/// 平台操控模式
/// </summary>
public enum PlatformControlMode
{
    Rush,       // 朝指定方向猛冲
    Drop,       // 突然坠落
    Reverse,    // 反向移动
    Stop        // 突然停止
}
