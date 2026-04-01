using UnityEngine;

/// <summary>
/// 可操控移动平台 - Trickster 变身后可操控的关卡道具
/// 
/// 操控模式:
///   Rush    - 朝指定方向猛冲
///   Drop    - 突然坠落
///   Reverse - 反向移动
///   Stop    - 突然停止（让 Mario 跳空）
/// 
/// 边缘卡住修复 (Session 4):
///   - 运行时自动给平台和角色 Collider 设置零摩擦材质
///   - 角色跟随改用位移推动，非 SetParent
///   - 排除伪装状态的 Trickster 被平台携带
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ControllablePlatform : ControllablePropBase
{
    [Header("平台移动配置")]
    [SerializeField] private Vector3 pointB = new Vector3(5f, 0f, 0f);
    [SerializeField] private float normalMoveSpeed = 2f;
    [SerializeField] private float waitTime = 0.5f;
    [SerializeField] private bool useLocalSpace = true;
    [SerializeField] private bool startFromB = false;

    [Header("操控模式")]
    [SerializeField] private PlatformControlMode controlMode = PlatformControlMode.Rush;

    [Header("Rush 参数")]
    [SerializeField] private float rushSpeedMultiplier = 5f;

    [Header("Drop 参数")]
    [SerializeField] private float dropSpeed = 10f;
    [SerializeField] private float dropRecoverTime = 2f;

    [Header("Reverse 参数")]
    [SerializeField] private float reverseSpeedMultiplier = 2f;

    private Vector3 worldPointA;
    private Vector3 worldPointB;
    private Vector3 targetPoint;
    private float waitTimer;
    private bool isWaiting;

    private bool isControlled;
    private Vector3 rushDirection;
    private Vector3 preControlPosition;

    private Transform ridingMario;
    private Transform ridingTrickster;

    // 零摩擦材质（运行时自动创建，全局共享）
    private static PhysicsMaterial2D s_zeroFriction;
    private static PhysicsMaterial2D ZeroFriction
    {
        get
        {
            if (s_zeroFriction == null)
            {
                s_zeroFriction = new PhysicsMaterial2D("ZeroFriction") { friction = 0f, bounciness = 0f };
            }
            return s_zeroFriction;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        propName = "移动平台";
    }

    private void Start()
    {
        Vector3 origin = useLocalSpace ? transform.position : Vector3.zero;
        worldPointA = useLocalSpace ? transform.position : Vector3.zero;
        worldPointB = origin + pointB;

        if (startFromB)
        {
            transform.position = worldPointB;
            targetPoint = worldPointA;
        }
        else
        {
            worldPointA = transform.position;
            targetPoint = worldPointB;
        }

        // 给平台设置零摩擦，防止角色贴侧面卡住
        GetComponent<BoxCollider2D>().sharedMaterial = ZeroFriction;
    }

    private void FixedUpdate()
    {
        Vector3 prev = transform.position;

        if (isControlled)
            UpdateControlledMovement();
        else
            UpdateNormalMovement();

        Vector3 delta = transform.position - prev;
        if (delta.sqrMagnitude > 0f)
        {
            if (ridingMario != null)     ridingMario.position     += delta;
            if (ridingTrickster != null) ridingTrickster.position += delta;
        }
    }

    private void UpdateNormalMovement()
    {
        if (isWaiting)
        {
            waitTimer -= Time.fixedDeltaTime;
            if (waitTimer <= 0) isWaiting = false;
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPoint, normalMoveSpeed * Time.fixedDeltaTime);

        if (Vector3.Distance(transform.position, targetPoint) < 0.01f)
        {
            targetPoint = targetPoint == worldPointA ? worldPointB : worldPointA;
            isWaiting = true;
            waitTimer = waitTime;
        }
    }

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
                Vector3 reverseTarget = targetPoint == worldPointA ? worldPointB : worldPointA;
                transform.position = Vector3.MoveTowards(transform.position, reverseTarget,
                    normalMoveSpeed * reverseSpeedMultiplier * Time.fixedDeltaTime);
                break;
            case PlatformControlMode.Stop:
                break;
        }
    }

    #region ControllablePropBase 实现

    protected override void OnTelegraphStart() => preControlPosition = transform.position;
    protected override void OnTelegraphEnd() { }

    protected override void OnActivate(Vector2 direction)
    {
        isControlled = true;
        switch (controlMode)
        {
            case PlatformControlMode.Rush:
                rushDirection = direction.magnitude > 0.1f
                    ? new Vector3(direction.x, direction.y, 0f).normalized
                    : (transform.position - targetPoint).normalized;
                break;
            case PlatformControlMode.Reverse:
                targetPoint = targetPoint == worldPointA ? worldPointB : worldPointA;
                break;
        }
    }

    protected override void OnActiveEnd()
    {
        isControlled = false;
        if (controlMode == PlatformControlMode.Drop)
            StartCoroutine(RecoverFromDrop());
        else if (controlMode == PlatformControlMode.Rush)
        {
            float distToA = Vector3.Distance(transform.position, worldPointA);
            float distToB = Vector3.Distance(transform.position, worldPointB);
            targetPoint = distToA < distToB ? worldPointB : worldPointA;
        }
    }

    private System.Collections.IEnumerator RecoverFromDrop()
    {
        yield return new WaitForSeconds(dropRecoverTime);
        Vector3 start = transform.position;
        float elapsed = 0f, duration = 1f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t); // smoothstep
            transform.position = Vector3.Lerp(start, preControlPosition, t);
            yield return null;
        }
        transform.position = preControlPosition;
        float dA = Vector3.Distance(transform.position, worldPointA);
        float dB = Vector3.Distance(transform.position, worldPointB);
        targetPoint = dA < dB ? worldPointB : worldPointA;
    }

    #endregion

    #region 角色跟随

    private void OnCollisionEnter2D(Collision2D col)
    {
        // 只响应从上方落下的碰撞（接触法线朝上）
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
        if (col.transform == ridingMario)     ridingMario     = null;
        if (col.transform == ridingTrickster) ridingTrickster = null;
    }

    private void TryRegisterRider(Collision2D col)
    {
        GameObject obj = col.gameObject;

        if (obj.GetComponent<MarioController>() != null)
        {
            ApplyZeroFriction(obj);
            ridingMario = col.transform;
            return;
        }

        TricksterController tc = obj.GetComponent<TricksterController>();
        if (tc != null)
        {
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

    #endregion

    #region 调试可视化

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Vector3 origin = useLocalSpace ? transform.position : Vector3.zero;
        Vector3 a = Application.isPlaying ? worldPointA : (useLocalSpace ? transform.position : Vector3.zero);
        Vector3 b = Application.isPlaying ? worldPointB : origin + pointB;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(a, 0.15f);
        Gizmos.DrawWireSphere(b, 0.15f);
    }

    #endregion
}

public enum PlatformControlMode { Rush, Drop, Reverse, Stop }
