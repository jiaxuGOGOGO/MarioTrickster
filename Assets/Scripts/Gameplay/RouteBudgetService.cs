using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Commit 2：路线预算服务 — 按 Mario 当前目标链统计可用路线。
///
/// 核心规则：
///   - 维护当前关卡中 Mario 的目标链（一组有序的关键通路 ID）。
///   - 每条通路有状态：Available / Degraded / Blocked。
///   - 如果当前目标只剩两条有效路径，同一时间窗内最多一条能被降级。
///   - 当 Trickster 试图降级第二条路时，服务会拒绝或强制恢复最早被降级的路。
///   - 通过监听 TricksterAbilitySystem.OnPropActivated 事件接入。
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
///   - 不直接阻止 Trickster 操控，只提供预算信息和护栏事件。
/// </summary>
public class RouteBudgetService : MonoBehaviour
{
    // ─────────────────────────────────────────────────────
    #region 数据结构

    [System.Serializable]
    public enum RouteStatus
    {
        Available,  // 正常可通行
        Degraded,   // 被干预但仍可慢速通过
        Blocked     // 暂时不可通行（有倒计时）
    }

    [System.Serializable]
    public class RouteEntry
    {
        public string RouteId;
        public RouteStatus Status;
        public float DegradedSince;     // 被降级的时间戳
        public float RecoveryTimer;     // 自动恢复倒计时
        public string DegradedBy;       // 降级来源（锚点 ID 或机关类型）

        public RouteEntry(string id)
        {
            RouteId = id;
            Status = RouteStatus.Available;
            DegradedSince = -999f;
            RecoveryTimer = 0f;
            DegradedBy = "";
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 配置

    [Header("=== Commit 2 路线预算 ===")]
    [Tooltip("降级路线的自动恢复时间（秒）")]
    [SerializeField] private float autoRecoveryTime = 6f;

    [Tooltip("同时允许降级的最大路线数（当总路线<=2时强制为1）")]
    [SerializeField] private int maxSimultaneousDegraded = 1;

    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool showDebugInfo = false;

    #endregion

    private float AutoRecoveryTimeValue => GameplayMetrics.RouteAutoRecoveryTime(autoRecoveryTime);
    private int MaxSimultaneousDegradedValue => GameplayMetrics.RouteMaxSimultaneousDegraded(maxSimultaneousDegraded);

    // ─────────────────────────────────────────────────────
    #region 运行时状态

    private List<RouteEntry> routes = new List<RouteEntry>();
    private TricksterAbilitySystem abilitySystem;
    private TricksterPossessionGate possessionGate;

    // ── 事件 ──
    /// <summary>路线被降级时触发（routeId, 降级来源）</summary>
    public event Action<string, string> OnRouteDegraded;

    /// <summary>路线恢复时触发（routeId）</summary>
    public event Action<string> OnRouteRecovered;

    /// <summary>降级被护栏拒绝时触发（routeId, 原因）</summary>
    public event Action<string, string> OnDegradeBlocked;

    // ── 公共属性 ──
    public int TotalRoutes => routes.Count;
    public int AvailableRoutes => CountByStatus(RouteStatus.Available);
    public int DegradedRoutes => CountByStatus(RouteStatus.Degraded);

    #endregion

    // ─────────────────────────────────────────────────────
    #region 生命周期

    private void Start()
    {
        // 查找 Trickster 系统
        var trickster = FindObjectOfType<TricksterController>();
        if (trickster != null)
        {
            abilitySystem = trickster.GetComponent<TricksterAbilitySystem>();
            possessionGate = trickster.GetComponent<TricksterPossessionGate>();
        }

        // 订阅事件
        if (abilitySystem != null)
        {
            abilitySystem.OnPropActivated += HandlePropActivated;
        }

        // 订阅回合重置
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart += HandleRoundStart;
        }

        // 如果没有预设路线，自动从场景中的 LevelElementRegistry 推断
        if (routes.Count == 0)
        {
            InitializeDefaultRoutes();
        }
    }

    private void OnDestroy()
    {
        if (abilitySystem != null)
        {
            abilitySystem.OnPropActivated -= HandlePropActivated;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart -= HandleRoundStart;
        }
    }

    private void Update()
    {
        // 推进恢复倒计时
        for (int i = 0; i < routes.Count; i++)
        {
            RouteEntry route = routes[i];
            if (route.Status == RouteStatus.Degraded || route.Status == RouteStatus.Blocked)
            {
                route.RecoveryTimer -= Time.deltaTime;
                if (route.RecoveryTimer <= 0f)
                {
                    RecoverRoute(route);
                }
            }
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 公共 API

    /// <summary>注册一条路线（由关卡初始化或 TestSceneBuilder 调用）</summary>
    public void RegisterRoute(string routeId)
    {
        if (GetRoute(routeId) != null) return;
        routes.Add(new RouteEntry(routeId));
    }

    /// <summary>尝试降级一条路线。返回是否被允许。</summary>
    public bool TryDegradeRoute(string routeId, string source, float duration = -1f)
    {
        RouteEntry route = GetRoute(routeId);
        if (route == null) return false;
        if (route.Status != RouteStatus.Available) return false;

        // 护栏检查：当前降级数是否已达上限
        int currentDegraded = CountByStatus(RouteStatus.Degraded) + CountByStatus(RouteStatus.Blocked);
        int effectiveMax = (routes.Count <= 2) ? 1 : MaxSimultaneousDegradedValue;

        if (currentDegraded >= effectiveMax)
        {
            // 尝试强制恢复最早被降级的路线
            RouteEntry oldest = GetOldestDegraded();
            if (oldest != null)
            {
                RecoverRoute(oldest);
            }
            else
            {
                // 真的无法降级更多
                if (showDebugInfo)
                {
                    Debug.Log($"[RouteBudgetService] Degrade BLOCKED for {routeId}: " +
                              $"max simultaneous degraded reached ({effectiveMax})");
                }
                OnDegradeBlocked?.Invoke(routeId, "MaxSimultaneousDegraded");
                return false;
            }
        }

        // 执行降级
        route.Status = RouteStatus.Degraded;
        route.DegradedSince = Time.time;
        route.DegradedBy = source;
        route.RecoveryTimer = duration > 0f ? duration : AutoRecoveryTimeValue;

        if (showDebugInfo)
        {
            Debug.Log($"[RouteBudgetService] Route {routeId} DEGRADED by {source}, " +
                      $"recovery in {route.RecoveryTimer:F1}s");
        }

        OnRouteDegraded?.Invoke(routeId, source);
        return true;
    }

    /// <summary>手动恢复一条路线</summary>
    public void ForceRecoverRoute(string routeId)
    {
        RouteEntry route = GetRoute(routeId);
        if (route != null && route.Status != RouteStatus.Available)
        {
            RecoverRoute(route);
        }
    }

    /// <summary>获取所有路线的只读快照</summary>
    public IReadOnlyList<RouteEntry> GetAllRoutes() => routes.AsReadOnly();

    /// <summary>是否还有至少一条可用路线</summary>
    public bool HasFallbackRoute()
    {
        for (int i = 0; i < routes.Count; i++)
        {
            if (routes[i].Status == RouteStatus.Available) return true;
        }
        return false;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 内部逻辑

    private void HandlePropActivated(IControllableProp prop)
    {
        if (prop == null) return;

        // 当 Trickster 操控机关时，尝试降级关联路线
        // 第一版：用锚点 ID 或机关名作为路线关联
        Transform propTransform = prop.GetTransform();
        if (propTransform == null) return;

        PossessionAnchor anchor = propTransform.GetComponent<PossessionAnchor>();
        string source = anchor != null ? anchor.AnchorId : propTransform.name;

        // 查找与该机关最近的路线进行降级
        string targetRoute = FindNearestRoute(propTransform.position);
        if (!string.IsNullOrEmpty(targetRoute))
        {
            TryDegradeRoute(targetRoute, source);
        }
    }

    private void HandleRoundStart()
    {
        // 回合重置所有路线
        for (int i = 0; i < routes.Count; i++)
        {
            routes[i].Status = RouteStatus.Available;
            routes[i].RecoveryTimer = 0f;
            routes[i].DegradedBy = "";
        }
    }

    private void RecoverRoute(RouteEntry route)
    {
        string routeId = route.RouteId;
        route.Status = RouteStatus.Available;
        route.RecoveryTimer = 0f;
        route.DegradedBy = "";

        if (showDebugInfo)
        {
            Debug.Log($"[RouteBudgetService] Route {routeId} RECOVERED");
        }

        OnRouteRecovered?.Invoke(routeId);
    }

    private void InitializeDefaultRoutes()
    {
        // 第一版灰盒：默认注册两条路线（上路和下路）
        // 后续可从关卡数据中读取
        RegisterRoute("route_upper");
        RegisterRoute("route_lower");

        if (showDebugInfo)
        {
            Debug.Log($"[RouteBudgetService] Initialized {routes.Count} default routes");
        }
    }

    private string FindNearestRoute(Vector3 propPosition)
    {
        // 第一版简单逻辑：根据 Y 坐标判断上下路
        // 后续可用 LevelElementRegistry 做更精确的路线归属
        if (routes.Count == 0) return null;
        if (routes.Count == 1) return routes[0].RouteId;

        // 场景中心 Y 作为分界线
        float midY = 0f;
        if (propPosition.y > midY)
        {
            return "route_upper";
        }
        else
        {
            return "route_lower";
        }
    }

    private RouteEntry GetRoute(string routeId)
    {
        for (int i = 0; i < routes.Count; i++)
        {
            if (routes[i].RouteId == routeId) return routes[i];
        }
        return null;
    }

    private RouteEntry GetOldestDegraded()
    {
        RouteEntry oldest = null;
        float oldestTime = float.MaxValue;
        for (int i = 0; i < routes.Count; i++)
        {
            if (routes[i].Status == RouteStatus.Degraded && routes[i].DegradedSince < oldestTime)
            {
                oldest = routes[i];
                oldestTime = routes[i].DegradedSince;
            }
        }
        return oldest;
    }

    private int CountByStatus(RouteStatus status)
    {
        int count = 0;
        for (int i = 0; i < routes.Count; i++)
        {
            if (routes[i].Status == status) count++;
        }
        return count;
    }

    #endregion
}
