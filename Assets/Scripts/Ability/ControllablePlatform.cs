using UnityEngine;

/// <summary>
/// 可操控移动平台 - Trickster 变身后可操控的关卡道具
///
/// 操控模式（在 Inspector 中选择）：
///   Rush    - 朝 Trickster 指定方向猛冲
///   Drop    - 突然坠落，一段时间后恢复原位
///   Reverse - 反向移动
///   Stop    - 突然停止（让 Mario 跳空）
///
/// 跟随方案：与 MovingPlatform 相同，使用速度注入。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ControllablePlatform : ControllablePropBase
{
    [Header("平台移动")]
    [Tooltip("终点相对于起点的偏移")]
    [SerializeField] private Vector3 pointB = new Vector3(5f, 0f, 0f);
    [SerializeField] private float normalMoveSpeed = 2f;
    [SerializeField] private float waitTime = 0.5f;
    [SerializeField] private bool startFromB = false;

    [Header("操控模式")]
    [SerializeField] private PlatformControlMode controlMode = PlatformControlMode.Rush;

    [Header("Rush")]
    [SerializeField] private float rushSpeedMultiplier = 5f;

    [Header("Drop")]
    [SerializeField] private float dropSpeed = 10f;
    [SerializeField] private float dropRecoverTime = 2f;

    [Header("Reverse")]
    [SerializeField] private float reverseSpeedMultiplier = 2f;

    // ── 路径 ──────────────────────────────────────────────
    private Vector3 worldPointA;
    private Vector3 worldPointB;
    private Vector3 targetPoint;
    private float waitTimer;
    private bool isWaiting;

    // ── 操控状态 ──────────────────────────────────────────
    private bool isControlled;
    private Vector3 rushDirection;
    private Vector3 preControlPosition;

    // ── 角色跟随 ──────────────────────────────────────────
    private MarioController ridingMario;
    private TricksterController ridingTrickster;

    // ── 零摩擦材质 ────────────────────────────────────────
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

    // ─────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        propName = "移动平台";
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
        Vector3 prev = transform.position;

        if (isControlled) UpdateControlledMovement();
        else              UpdateNormalMovement();

        Vector3 delta = transform.position - prev;
        if (delta.sqrMagnitude > 0f)
        {
            Vector2 platVel = new Vector2(delta.x, delta.y) / Time.fixedDeltaTime;
            if (ridingMario != null)     ridingMario.SetPlatformVelocity(platVel);
            if (ridingTrickster != null) ridingTrickster.SetPlatformVelocity(platVel);
        }
    }

    // ── 移动逻辑 ──────────────────────────────────────────

    private void UpdateNormalMovement()
    {
        if (isWaiting)
        {
            waitTimer -= Time.fixedDeltaTime;
            if (waitTimer <= 0) isWaiting = false;
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position, targetPoint, normalMoveSpeed * Time.fixedDeltaTime);

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
                transform.position = Vector3.MoveTowards(
                    transform.position, reverseTarget,
                    normalMoveSpeed * reverseSpeedMultiplier * Time.fixedDeltaTime);
                break;
            case PlatformControlMode.Stop:
                break;
        }
    }

    // ── ControllablePropBase 实现 ─────────────────────────

    protected override void OnTelegraphStart() => preControlPosition = transform.position;
    protected override void OnTelegraphEnd()   { }

    protected override void OnActivate(Vector2 direction)
    {
        isControlled = true;
        if (controlMode == PlatformControlMode.Rush)
        {
            rushDirection = direction.magnitude > 0.1f
                ? new Vector3(direction.x, direction.y, 0f).normalized
                : (transform.position - targetPoint).normalized;
        }
        else if (controlMode == PlatformControlMode.Reverse)
        {
            targetPoint = targetPoint == worldPointA ? worldPointB : worldPointA;
        }
    }

    protected override void OnActiveEnd()
    {
        isControlled = false;
        if (controlMode == PlatformControlMode.Drop)
        {
            StartCoroutine(RecoverFromDrop());
        }
        else if (controlMode == PlatformControlMode.Rush)
        {
            float dA = Vector3.Distance(transform.position, worldPointA);
            float dB = Vector3.Distance(transform.position, worldPointB);
            targetPoint = dA < dB ? worldPointB : worldPointA;
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
        float dA2 = Vector3.Distance(transform.position, worldPointA);
        float dB2 = Vector3.Distance(transform.position, worldPointB);
        targetPoint = dA2 < dB2 ? worldPointB : worldPointA;
    }

    // ── 角色跟随 ──────────────────────────────────────────

    private void OnCollisionEnter2D(Collision2D col) => TryRegisterRider(col);
    private void OnCollisionStay2D(Collision2D col)  => TryRegisterRider(col);

    private void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.GetComponent<MarioController>() == ridingMario)         ridingMario     = null;
        if (col.gameObject.GetComponent<TricksterController>() == ridingTrickster) ridingTrickster = null;
    }

    private void TryRegisterRider(Collision2D col)
    {
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

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Vector3 a = Application.isPlaying ? worldPointA : transform.position;
        Vector3 b = Application.isPlaying ? worldPointB : transform.position + pointB;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(a, 0.15f);
        Gizmos.DrawWireSphere(b, 0.15f);
    }
}

public enum PlatformControlMode { Rush, Drop, Reverse, Stop }
