using System;
using UnityEngine;

/// <summary>
/// Commit 2：Counter-Reveal 推进奖励 — 反制成功后给 Mario 立即收益。
///
/// 核心规则（S130）：
///   - 揭穿后 Mario 获得短期推进奖励、路径恢复或 Trickster 暴露窗口。
///   - 让反制像读赢了 Trickster，而不只是恢复原状。
///   - 奖励包括：保护窗口延长、降级路线恢复、Heat 回落、短期加速。
///
/// 接入方式：
///   - 监听 MarioCounterplayProbe.OnCounterReveal 事件。
///   - 触发时向 RouteBudgetService 恢复路线、向 RepeatInterferenceStack 回落 Heat。
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
/// </summary>
public class CounterRevealReward : MonoBehaviour
{
    [Header("=== Commit 2 Counter-Reveal 奖励 ===")]
    [Tooltip("Counter-Reveal 成功后恢复所有降级路线")]
    [SerializeField] private bool recoverAllDegradedRoutes = true;

    [Tooltip("Counter-Reveal 成功后 Heat 回落量")]
    [SerializeField] private float heatReduction = 25f;

    [Tooltip("Counter-Reveal 成功后给 Mario 的推进加速持续时间（秒）")]
    [SerializeField] private float rewardBoostDuration = 3f;

    [Tooltip("Counter-Reveal 成功后给 Mario 的推进加速倍率")]
    [SerializeField] private float rewardBoostMultiplier = 1.25f;

    [Tooltip("Counter-Reveal 成功后冻结该锚点的持续时间（秒）— Trickster 不能立刻复用")]
    [SerializeField] private float anchorFreezeTime = 8f;

    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool showDebugInfo = false;

    // ── 引用 ──
    private MarioCounterplayProbe counterplayProbe;
    private RouteBudgetService routeBudget;
    private RepeatInterferenceStack repeatStack;
    private MarioSuspicionTracker suspicionTracker;

    // ── 状态 ──
    private float rewardBoostTimer;
    private int totalCounterReveals;

    // ── 事件 ──
    /// <summary>奖励发放时触发（奖励描述）</summary>
    public event Action<string> OnRewardGranted;

    /// <summary>推进加速开始时触发（持续时间, 倍率）</summary>
    public event Action<float, float> OnRewardBoostStart;

    // ── 公共属性 ──
    public bool IsRewardBoosted => rewardBoostTimer > 0f;
    public float RewardBoostRemaining => rewardBoostTimer;
    public float CurrentRewardMultiplier => IsRewardBoosted ? rewardBoostMultiplier : 1f;
    public int TotalCounterReveals => totalCounterReveals;

    // ─────────────────────────────────────────────────────
    #region 生命周期

    private void Start()
    {
        // 查找依赖
        counterplayProbe = FindObjectOfType<MarioCounterplayProbe>();
        routeBudget = FindObjectOfType<RouteBudgetService>();
        repeatStack = FindObjectOfType<RepeatInterferenceStack>();
        suspicionTracker = FindObjectOfType<MarioSuspicionTracker>();

        // 订阅 Counter-Reveal 事件
        if (counterplayProbe != null)
        {
            counterplayProbe.OnCounterReveal += HandleCounterReveal;
        }

        // 订阅回合重置
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart += HandleRoundStart;
        }
    }

    private void OnDestroy()
    {
        if (counterplayProbe != null)
        {
            counterplayProbe.OnCounterReveal -= HandleCounterReveal;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart -= HandleRoundStart;
        }
    }

    private void Update()
    {
        if (rewardBoostTimer > 0f)
        {
            rewardBoostTimer -= Time.deltaTime;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 事件处理

    private void HandleCounterReveal(PossessionAnchor anchor, AnchorSuspicionData data)
    {
        totalCounterReveals++;

        if (showDebugInfo)
        {
            Debug.Log($"[CounterRevealReward] Counter-Reveal #{totalCounterReveals} at " +
                      $"{(anchor != null ? anchor.AnchorId : "unknown")}!");
        }

        // 奖励 1：恢复所有降级路线
        if (recoverAllDegradedRoutes && routeBudget != null)
        {
            var allRoutes = routeBudget.GetAllRoutes();
            for (int i = 0; i < allRoutes.Count; i++)
            {
                if (allRoutes[i].Status != RouteBudgetService.RouteStatus.Available)
                {
                    routeBudget.ForceRecoverRoute(allRoutes[i].RouteId);
                }
            }

            if (showDebugInfo)
            {
                Debug.Log("[CounterRevealReward] All degraded routes recovered!");
            }
            OnRewardGranted?.Invoke("RoutesRecovered");
        }

        // 奖励 2：Heat 回落
        if (repeatStack != null && heatReduction > 0f)
        {
            repeatStack.AddHeat(-heatReduction);

            if (showDebugInfo)
            {
                Debug.Log($"[CounterRevealReward] Heat reduced by {heatReduction}, " +
                          $"now={repeatStack.TotalHeat:F1}");
            }
            OnRewardGranted?.Invoke("HeatReduced");
        }

        // 奖励 3：推进加速
        if (rewardBoostDuration > 0f)
        {
            rewardBoostTimer = rewardBoostDuration;
            OnRewardBoostStart?.Invoke(rewardBoostDuration, rewardBoostMultiplier);

            if (showDebugInfo)
            {
                Debug.Log($"[CounterRevealReward] Progress boost: " +
                          $"{rewardBoostMultiplier}x for {rewardBoostDuration}s");
            }
            OnRewardGranted?.Invoke("ProgressBoost");
        }

        // 奖励 4：冻结该锚点（Trickster 短时间内不能复用）
        if (anchor != null && anchorFreezeTime > 0f)
        {
            // 通过大幅提高可疑度实现"冻结"效果
            // 后续 Commit 可以在 PossessionAnchor 上加 IsFrozen 标记
            if (suspicionTracker != null)
            {
                AnchorSuspicionData anchorData = suspicionTracker.GetOrCreateData(anchor);
                anchorData.AddSuspicion(AnchorSuspicionData.MaxSuspicion);
            }

            if (showDebugInfo)
            {
                Debug.Log($"[CounterRevealReward] Anchor {anchor.AnchorId} frozen for " +
                          $"{anchorFreezeTime}s (max suspicion applied)");
            }
            OnRewardGranted?.Invoke("AnchorFrozen");
        }
    }

    private void HandleRoundStart()
    {
        rewardBoostTimer = 0f;
        totalCounterReveals = 0;
    }

    #endregion
}
