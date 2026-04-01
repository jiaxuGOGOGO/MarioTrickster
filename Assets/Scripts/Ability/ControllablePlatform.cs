using System.Collections;
using System.Collections.Generic;
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
/// 跟随方案（速度注入法，不使用 SetParent）：
///   与 MovingPlatform 相同，平台带 Kinematic Rigidbody2D，
///   用 rb.MovePosition() 移动，每帧把平台速度注入站在上面的角色。
///   不使用 SetParent（避免 Transform 层级与 Rigidbody2D 世界坐标冲突）。
/// </summary>
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(Rigidbody2D))]
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
    private Rigidbody2D rb;
    private Vector2 worldPointA;
    private Vector2 worldPointB;
    private Vector2 targetPoint;
    private float waitTimer;
    private bool isWaiting;

    // ── 操控状态 ──────────────────────────────────────────
    private bool isControlled;
    private Vector2 rushDirection;
    private Vector2 preControlPosition;

    // ── 平台速度与乘客 ────────────────────────────────────
    private Vector2 _currentVelocity;
    private readonly HashSet<GameObject> _riders = new HashSet<GameObject>();

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

        rb = GetComponent<Rigidbody2D>();
        // Kinematic：平台不受重力/碰撞力影响，但能推动 Dynamic 物体
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        GetComponent<BoxCollider2D>().sharedMaterial = ZeroFriction;
    }

    private void Start()
    {
        worldPointA = transform.position;
        worldPointB = (Vector2)transform.position + (Vector2)pointB;
        targetPoint = startFromB ? worldPointA : worldPointB;
        if (startFromB) rb.position = worldPointB;
    }

    private void FixedUpdate()
    {
        Vector2 prevPos = rb.position;

        if (isControlled) UpdateControlledMovement();
        else              UpdateNormalMovement();

        // 计算本帧实际速度
        _currentVelocity = (rb.position - prevPos) / Time.fixedDeltaTime;

        // 将平台速度注入所有站在上面的角色
        InjectVelocityToRiders();
    }

    // ── 移动逻辑 ──────────────────────────────────────────

    private void UpdateNormalMovement()
    {
        if (isWaiting)
        {
            waitTimer -= Time.fixedDeltaTime;
            if (waitTimer <= 0f) isWaiting = false;
            _currentVelocity = Vector2.zero;
            return;
        }

        Vector2 newPos = Vector2.MoveTowards(rb.position, targetPoint, normalMoveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);

        if (Vector2.Distance(newPos, targetPoint) < 0.01f)
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
                rb.MovePosition(rb.position + rushDirection * rushSpeedMultiplier * normalMoveSpeed * Time.fixedDeltaTime);
                break;
            case PlatformControlMode.Drop:
                rb.MovePosition(rb.position + Vector2.down * dropSpeed * Time.fixedDeltaTime);
                break;
            case PlatformControlMode.Reverse:
                Vector2 reverseTarget = targetPoint == worldPointA ? worldPointB : worldPointA;
                rb.MovePosition(Vector2.MoveTowards(rb.position, reverseTarget,
                    normalMoveSpeed * reverseSpeedMultiplier * Time.fixedDeltaTime));
                break;
            case PlatformControlMode.Stop:
                break;
        }
    }

    // ── 速度注入 ────────────────────────────────────────────

    private void InjectVelocityToRiders()
    {
        if (_currentVelocity.sqrMagnitude < 0.0001f) return;

        foreach (GameObject rider in _riders)
        {
            if (rider == null) continue;

            MarioController mario = rider.GetComponent<MarioController>();
            if (mario != null) { mario.SetPlatformVelocity(_currentVelocity); continue; }

            TricksterController trickster = rider.GetComponent<TricksterController>();
            if (trickster != null) trickster.SetPlatformVelocity(_currentVelocity);
        }
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (IsRidingFromAbove(col)) _riders.Add(col.gameObject);
    }

    private void OnCollisionStay2D(Collision2D col)
    {
        if (IsRidingFromAbove(col)) _riders.Add(col.gameObject);
    }

    private void OnCollisionExit2D(Collision2D col)
    {
        _riders.Remove(col.gameObject);
    }

    private bool IsRidingFromAbove(Collision2D col)
    {
        foreach (ContactPoint2D contact in col.contacts)
        {
            if (contact.normal.y < -0.5f) return true;
        }
        return false;
    }

    // ── ControllablePropBase 实现 ─────────────────────────

    protected override void OnTelegraphStart() => preControlPosition = rb.position;
    protected override void OnTelegraphEnd()   { }

    protected override void OnActivate(Vector2 direction)
    {
        isControlled = true;
        if (controlMode == PlatformControlMode.Rush)
        {
            rushDirection = direction.magnitude > 0.1f
                ? direction.normalized
                : ((Vector2)transform.position - targetPoint).normalized;
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
            float dA = Vector2.Distance(rb.position, worldPointA);
            float dB = Vector2.Distance(rb.position, worldPointB);
            targetPoint = dA < dB ? worldPointB : worldPointA;
        }
    }

    private IEnumerator RecoverFromDrop()
    {
        yield return new WaitForSeconds(dropRecoverTime);
        Vector2 start = rb.position;
        float elapsed = 0f, duration = 1f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t); // smoothstep
            rb.MovePosition(Vector2.Lerp(start, preControlPosition, t));
            yield return new WaitForFixedUpdate();
        }
        rb.MovePosition(preControlPosition);
        float dA2 = Vector2.Distance(rb.position, worldPointA);
        float dB2 = Vector2.Distance(rb.position, worldPointB);
        targetPoint = dA2 < dB2 ? worldPointB : worldPointA;
    }

    // ── 编辑器可视化 ──────────────────────────────────────

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Vector3 a = Application.isPlaying ? (Vector3)worldPointA : transform.position;
        Vector3 b = Application.isPlaying ? (Vector3)worldPointB : transform.position + pointB;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(a, 0.15f);
        Gizmos.DrawWireSphere(b, 0.15f);
    }
}

public enum PlatformControlMode { Rush, Drop, Reverse, Stop }
