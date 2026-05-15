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
/// S53 薄层原则：只复用 ControllableLevelElement 状态机、RouteBudgetService 和 ScanAbility 事件，
/// 不改 MarioController、TricksterAbilitySystem 或 AsciiLevelGenerator 核心流程。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class ControllableBlocker : ControllableLevelElement
{
    [Header("=== 封路设置 ===")]
    [Tooltip("关联路线 ID；留空时按 Y 坐标自动映射 route_upper / route_lower。")]
    [SerializeField] private string routeId = "";

    [Tooltip("扫描命中 Windup 后，Active 持续时间倍率。0.5 = 减半。")]
    [SerializeField, Range(0.1f, 1f)] private float scannedActiveDurationMultiplier = 0.5f;

    [Tooltip("路线预算拒绝时，是否额外扣一次操控能量作为失败代价。")]
    [SerializeField] private bool consumeEnergyOnRouteFail = true;

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
    private RouteBudgetService routeBudgetService;
    private ScanAbility marioScanAbility;
    private EnergySystem tricksterEnergySystem;

    private bool originalColliderEnabled;
    private bool originalIsTrigger;
    private Color originalColor;
    private bool scanCounteredThisWindup;
    private bool activationBlockedByRouteBudget;
    private string activeRouteId;

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

        routeBudgetService = FindObjectOfType<RouteBudgetService>();
        marioScanAbility = FindObjectOfType<ScanAbility>();
        tricksterEnergySystem = FindObjectOfType<EnergySystem>();

        SetPassableState();
    }

    private void Start()
    {
        EnsureScanSubscription();
    }

    protected override void Update()
    {
        base.Update();

        if (currentState == PropControlState.Active && !activationBlockedByRouteBudget)
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
        activationBlockedByRouteBudget = false;
        activeRouteId = ResolveRouteId();
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

        activationBlockedByRouteBudget = !TryReserveRouteBudget(effectiveActiveDuration);
        if (activationBlockedByRouteBudget)
        {
            ConsumeRouteFailEnergyPenalty();
            SetPassableState();
            stateTimer = 0f;
            Debug.Log($"[ControllableBlocker] {gameObject.name} route budget rejected, blocker failed.");
            return;
        }

        stateTimer = Mathf.Min(stateTimer, effectiveActiveDuration);
        SetActiveState();
        SqueezeMarioOutIfOverlapping();
    }

    protected override void OnActiveEnd()
    {
        SetPassableState();
        scanCounteredThisWindup = false;
        activationBlockedByRouteBudget = false;
    }

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        scanCounteredThisWindup = false;
        activationBlockedByRouteBudget = false;
        activeRouteId = "";

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

    private bool TryReserveRouteBudget(float duration)
    {
        if (routeBudgetService == null)
        {
            routeBudgetService = FindObjectOfType<RouteBudgetService>();
        }

        if (routeBudgetService == null)
        {
            // 没有路线预算服务的测试场景中，不阻断机关自身功能。
            return true;
        }

        string targetRoute = string.IsNullOrEmpty(activeRouteId) ? ResolveRouteId() : activeRouteId;
        string source = ResolveBudgetSource();

        if (IsRouteAlreadyReservedByThisBlocker(targetRoute, source))
        {
            return true;
        }

        return routeBudgetService.TryDegradeRoute(targetRoute, source, duration);
    }

    private bool IsRouteAlreadyReservedByThisBlocker(string targetRoute, string source)
    {
        var routes = routeBudgetService.GetAllRoutes();
        for (int i = 0; i < routes.Count; i++)
        {
            var route = routes[i];
            if (route.RouteId == targetRoute && route.Status != RouteBudgetService.RouteStatus.Available)
            {
                return route.DegradedBy == source || route.DegradedBy == gameObject.name;
            }
        }
        return false;
    }

    private string ResolveRouteId()
    {
        if (!string.IsNullOrEmpty(routeId)) return routeId;
        return transform.position.y > 0f ? "route_upper" : "route_lower";
    }

    private string ResolveBudgetSource()
    {
        PossessionAnchor anchor = GetComponent<PossessionAnchor>();
        if (anchor != null && !string.IsNullOrEmpty(anchor.AnchorId))
        {
            return anchor.AnchorId;
        }
        return gameObject.name;
    }

    private void ConsumeRouteFailEnergyPenalty()
    {
        if (!consumeEnergyOnRouteFail) return;

        if (tricksterEnergySystem == null)
        {
            tricksterEnergySystem = FindObjectOfType<EnergySystem>();
        }

        if (tricksterEnergySystem != null)
        {
            tricksterEnergySystem.TryConsumeControlCost();
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

        Vector2 localPush;
        float minHorizontal = Mathf.Min(pushLeft, pushRight);
        float minVertical = Mathf.Min(pushDown, pushUp);

        if (minHorizontal <= minVertical)
        {
            localPush = pushRight < pushLeft
                ? new Vector2(pushRight + squeezePadding, 0f)
                : new Vector2(-(pushLeft + squeezePadding), 0f);
        }
        else
        {
            localPush = pushUp < pushDown
                ? new Vector2(0f, pushUp + squeezePadding)
                : new Vector2(0f, -(pushDown + squeezePadding));
        }

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
