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
/// 
/// 修复说明 (Session 4):
///   - 改用位移跟随替代 SetParent，避免 Rigidbody2D 被 parent 影响导致物理异常
///   - 排除处于伪装状态的 Trickster，防止平台吸附变身后的 Trickster
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
    private bool isControlled;
    private Vector3 rushDirection;
    private Vector3 preControlPosition;
    private float dropOriginalY;

    // 上一帧位置，用于计算平台位移
    private Vector3 lastPosition;

    // 当前站在平台上的角色
    private Transform ridingMario;
    private Transform ridingTrickster;

    protected override void Awake()
    {
        base.Awake();
        propName = "移动平台";
    }

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
        Vector3 prevPos = transform.position;

        if (isControlled)
        {
            UpdateControlledMovement();
        }
        else
        {
            UpdateNormalMovement();
        }

        // 计算本帧位移，推动站在平台上的角色
        Vector3 delta = transform.position - prevPos;
        if (delta.sqrMagnitude > 0.000001f)
        {
            if (ridingMario != null)
                ridingMario.position += delta;

            if (ridingTrickster != null)
                ridingTrickster.position += delta;
        }

        lastPosition = transform.position;
    }

    #region 正常移动

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
                transform.position += rushDirection * rushSpeedMultiplier * normalMoveSpeed * Time.fixedDeltaTime;
                break;

            case PlatformControlMode.Drop:
                transform.position += Vector3.down * dropSpeed * Time.fixedDeltaTime;
                break;

            case PlatformControlMode.Reverse:
                Vector3 reverseTarget = (targetPoint == worldPointA) ? worldPointB : worldPointA;
                transform.position = Vector3.MoveTowards(
                    transform.position, reverseTarget,
                    normalMoveSpeed * reverseSpeedMultiplier * Time.fixedDeltaTime
                );
                break;

            case PlatformControlMode.Stop:
                break;
        }
    }

    #endregion

    #region ControllablePropBase 实现

    protected override void OnTelegraphStart()
    {
        preControlPosition = transform.position;
    }

    protected override void OnTelegraphEnd()
    {
    }

    protected override void OnActivate(Vector2 direction)
    {
        isControlled = true;

        switch (controlMode)
        {
            case PlatformControlMode.Rush:
                if (direction.magnitude > 0.1f)
                    rushDirection = new Vector3(direction.x, direction.y, 0f).normalized;
                else
                    rushDirection = (transform.position - targetPoint).normalized;
                break;

            case PlatformControlMode.Drop:
                dropOriginalY = transform.position.y;
                break;

            case PlatformControlMode.Reverse:
                targetPoint = (targetPoint == worldPointA) ? worldPointB : worldPointA;
                break;

            case PlatformControlMode.Stop:
                break;
        }
    }

    protected override void OnActiveEnd()
    {
        isControlled = false;

        switch (controlMode)
        {
            case PlatformControlMode.Drop:
                StartCoroutine(RecoverFromDrop());
                break;

            case PlatformControlMode.Rush:
                float distToA = Vector3.Distance(transform.position, worldPointA);
                float distToB = Vector3.Distance(transform.position, worldPointB);
                targetPoint = distToA < distToB ? worldPointB : worldPointA;
                break;
        }
    }

    private System.Collections.IEnumerator RecoverFromDrop()
    {
        yield return new WaitForSeconds(dropRecoverTime);

        Vector3 startPos = transform.position;
        float elapsed = 0f;
        float recoverDuration = 1f;

        while (elapsed < recoverDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / recoverDuration;
            t = t * t * (3f - 2f * t);
            transform.position = Vector3.Lerp(startPos, preControlPosition, t);
            yield return null;
        }

        transform.position = preControlPosition;

        float distToA2 = Vector3.Distance(transform.position, worldPointA);
        float distToB2 = Vector3.Distance(transform.position, worldPointB);
        targetPoint = distToA2 < distToB2 ? worldPointB : worldPointA;
    }

    #endregion

    #region 角色跟随（位移跟随，替代 SetParent）

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
        // 只处理从上方站上平台的情况
        bool fromAbove = false;
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y < -0.5f)
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

    #endregion

    #region 调试可视化

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

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
