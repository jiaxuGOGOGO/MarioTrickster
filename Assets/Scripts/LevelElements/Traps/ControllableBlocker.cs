using UnityEngine;

/// <summary>
/// 可控封路机关 - Santorini 式临时封路原型 B。
///
/// 设计定位：纯物理墙，不造成伤害，不挂载任何伤害组件逻辑。
/// 生命周期：
///   - Windup/Telegraph：可通过，仅播放裂纹/虚线式视觉预警。
///   - Active：转为实心碰撞，临时封住一条路线；若 Mario 已在范围内，则平滑挤出。
///   - Recovery/Cooldown：恢复可通过半透明状态。
///
/// S53 薄层原则：只复用 ControllableLevelElement 状态机和 ScanAbility 事件（S239：旧路线预算已删，H1 由死局检查保证），
/// 不改 MarioController、TricksterAbilitySystem 或 AsciiLevelGenerator 核心流程。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class ControllableBlocker : ControllableLevelElement
{
    protected override bool RefundOnMiss => false; // S241：挡路 / 改地形 / 一次性，不按"打没打中"退还
    [Header("=== 封路设置 ===")]
    [Tooltip("扫描命中 Windup 后，Active 持续时间倍率。0.5 = 减半。")]
    [SerializeField, Range(0.1f, 1f)] private float scannedActiveDurationMultiplier = 0.5f;

    [Header("=== 视觉设置 ===")]
    [Tooltip("Idle/冷却时的半透明可通过颜色。")]
    [SerializeField] private Color passableColor = new Color(0.55f, 0.75f, 1f, 0.35f);

    [Tooltip("Windup 预警颜色，表示即将封路。")]
    [SerializeField] private Color windupHintColor = new Color(1f, 0.85f, 0.20f, 0.65f);

    [Tooltip("Active 实心墙颜色。")]
    [SerializeField] private Color activeWallColor = new Color(0.35f, 0.55f, 1f, 0.95f);

    [Header("=== Mario 挤出设置 ===")]
    [Tooltip("Active 时将重叠的 Mario 推出墙体的速度。")]
    [SerializeField] private float squeezeOutSpeed = 8f;

    [Tooltip("挤出目标额外留出的安全边距。")]
    [SerializeField] private float squeezePadding = 0.08f;

    private BoxCollider2D boxCollider;
    private SpriteRenderer sr;
    private ScanAbility marioScanAbility;

    private bool originalColliderEnabled;
    private bool originalIsTrigger;
    // 使用基类 ControllablePropBase.originalColor (protected)，不再重复声明
    private bool scanCounteredThisWindup;

    protected override void Awake()
    {
        propName = "封路机关";
        elementCategory = ElementCategory.Trap;
        elementTags = ElementTag.Controllable | ElementTag.Resettable | ElementTag.AffectsPhysics;
        elementDescription = "Trickster 操控后临时变为实心墙，封住单路线但不造成伤害";

        base.Awake();

        boxCollider = GetComponent<BoxCollider2D>();
        sr = GetComponentInChildren<SpriteRenderer>();

        originalColliderEnabled = boxCollider.enabled;
        originalIsTrigger = boxCollider.isTrigger;
        originalColor = sr != null ? sr.color : Color.white;

        marioScanAbility = FindObjectOfType<ScanAbility>();

        SetPassableState();
    }

    private void Start()
    {
        EnsureScanSubscription();
    }

    protected override void Update()
    {
        base.Update();

        if (currentState == PropControlState.Active)
        {
            SqueezeMarioOutIfOverlapping();
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        UnsubscribeScanEvent();
    }

    private void OnDestroy()
    {
        UnsubscribeScanEvent();
    }

    protected override void OnTelegraphStart()
    {
        scanCounteredThisWindup = false;
        EnsureScanSubscription();
        SetWindupState();
    }

    protected override void OnTelegraphEnd()
    {
        // base 会恢复通用闪烁颜色；这里保持可通过，真正封路只在 OnActivate 成功后发生。
        SetPassableState();
    }

    protected override void OnActivate(Vector2 direction)
    {
        float effectiveActiveDuration = scanCounteredThisWindup
            ? activeDuration * scannedActiveDurationMultiplier
            : activeDuration;

        stateTimer = Mathf.Min(stateTimer, effectiveActiveDuration);
        SetActiveState();
        SqueezeMarioOutIfOverlapping();
    }

    protected override void OnActiveEnd()
    {
        SetPassableState();
        scanCounteredThisWindup = false;
    }

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        scanCounteredThisWindup = false;

        boxCollider.enabled = originalColliderEnabled;
        boxCollider.isTrigger = originalIsTrigger;
        if (sr != null) sr.color = originalColor;

        SetPassableState();
    }

    private void EnsureScanSubscription()
    {
        if (marioScanAbility == null)
        {
            marioScanAbility = FindObjectOfType<ScanAbility>();
        }

        if (marioScanAbility != null)
        {
            marioScanAbility.OnScanActivated -= HandleMarioScanActivated;
            marioScanAbility.OnScanActivated += HandleMarioScanActivated;
        }
    }

    private void UnsubscribeScanEvent()
    {
        if (marioScanAbility != null)
        {
            marioScanAbility.OnScanActivated -= HandleMarioScanActivated;
        }
    }

    private void HandleMarioScanActivated()
    {
        if (currentState != PropControlState.Telegraph || scanCounteredThisWindup) return;
        if (marioScanAbility == null) return;

        float distance = Vector2.Distance(marioScanAbility.transform.position, transform.position);
        if (distance <= marioScanAbility.ScanRadius)
        {
            scanCounteredThisWindup = true;
            if (sr != null) sr.color = Color.Lerp(windupHintColor, Color.white, 0.45f);
            Debug.Log($"[ControllableBlocker] {gameObject.name} scanned during Windup, Active duration halved.");
        }
    }

    private void SetPassableState()
    {
        if (boxCollider == null) return;

        boxCollider.enabled = true;
        boxCollider.isTrigger = true;

        if (sr != null)
        {
            sr.color = passableColor;
        }
    }

    private void SetWindupState()
    {
        if (boxCollider != null)
        {
            boxCollider.enabled = true;
            boxCollider.isTrigger = true;
        }

        if (sr != null)
        {
            sr.color = windupHintColor;
        }
    }

    private void SetActiveState()
    {
        if (boxCollider != null)
        {
            boxCollider.enabled = true;
            boxCollider.isTrigger = false;
        }

        if (sr != null)
        {
            sr.color = activeWallColor;
        }
    }

    private void SqueezeMarioOutIfOverlapping()
    {
        if (boxCollider == null) return;

        Vector2 worldCenter = transform.TransformPoint(boxCollider.offset);
        Vector2 worldSize = Vector2.Scale(boxCollider.size, transform.lossyScale);
        Collider2D[] hits = Physics2D.OverlapBoxAll(worldCenter, worldSize, transform.eulerAngles.z);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit == boxCollider) continue;

            MarioController mario = hit.GetComponentInParent<MarioController>();
            if (mario == null) continue;

            MoveMarioOutsideBlocker(mario, hit);
        }
    }

    private void MoveMarioOutsideBlocker(MarioController mario, Collider2D marioCollider)
    {
        Vector3 marioWorld = marioCollider.bounds.center;
        Vector3 marioLocal = transform.InverseTransformPoint(marioWorld);
        Vector2 half = boxCollider.size * 0.5f;
        Vector2 localDelta = new Vector2(marioLocal.x - boxCollider.offset.x, marioLocal.y - boxCollider.offset.y);

        float pushRight = half.x - localDelta.x;
        float pushLeft = half.x + localDelta.x;
        float pushUp = half.y - localDelta.y;
        float pushDown = half.y + localDelta.y;

        // S241：只横向挤，优先挤到"那边没有墙"的一侧。以前离上边近就往上挤 → 门洞只有 1 格高时把马里奥顶进门楣，
        // 卡在墙里反复被挤（用户截图：马里奥贴在红墙上出不去、一直循环）。
        Vector2 size = marioCollider.bounds.size;
        bool rightFree = SideIsFree(marioWorld, size, +1f, pushRight + squeezePadding, marioCollider);
        bool leftFree = SideIsFree(marioWorld, size, -1f, pushLeft + squeezePadding, marioCollider);
        float dir = SqueezeDirection(pushLeft, pushRight, leftFree, rightFree);
        Vector2 localPush = new Vector2(dir > 0f ? pushRight + squeezePadding : -(pushLeft + squeezePadding), 0f);

        Vector3 targetWorld = transform.TransformPoint(marioLocal + (Vector3)localPush);
        Rigidbody2D rb = mario.GetComponent<Rigidbody2D>();
        float maxStep = squeezeOutSpeed * Time.deltaTime;

        if (rb != null)
        {
            rb.MovePosition(Vector2.MoveTowards(rb.position, targetWorld, maxStep));
        }
        else
        {
            mario.transform.position = Vector3.MoveTowards(mario.transform.position, targetWorld, maxStep);
        }
    }

    /// <summary>S241 纯函数：往哪边挤（+1 右 / -1 左）。只有一边有空位就去那边；两边都有（或都没有）就去近的那边。</summary>
    public static float SqueezeDirection(float pushLeft, float pushRight, bool leftFree, bool rightFree)
    {
        if (rightFree != leftFree) return rightFree ? 1f : -1f;
        return pushRight < pushLeft ? 1f : -1f;
    }

    private static readonly Collider2D[] s_free = new Collider2D[8];

    private bool SideIsFree(Vector2 center, Vector2 size, float dir, float dist, Collider2D self)
    {
        Vector2 at = center + Vector2.right * dir * dist;
        int n = Physics2D.OverlapBoxNonAlloc(at, size * 0.9f, 0f, s_free);
        for (int i = 0; i < n; i++)
        {
            var c = s_free[i];
            if (c == null || c == boxCollider || c == self || c.isTrigger) continue;
            if (c.GetComponentInParent<MarioController>() != null) continue;
            if (SightLine.IsOneWayPlatform(c)) continue;
            return false;
        }
        return true;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        Gizmos.color = currentState == PropControlState.Active
            ? new Color(0.2f, 0.45f, 1f, 0.55f)
            : new Color(1f, 0.85f, 0.2f, 0.35f);
        Gizmos.DrawCube(transform.TransformPoint(col.offset), col.size);
    }
}
