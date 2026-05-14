using System;
using UnityEngine;

/// <summary>
/// Commit 2：干预补偿策略 — 把 Trickster 的每次阻碍转成 Mario 的线索或补偿。
///
/// 核心规则（S130）：
///   - 每次 Mario 主推进被干预时，必须返还 Residue、SilentMark 或 Probe 成功率。
///   - 被拖慢不等于纯损失，而是让 Mario 获得反制进度。
///   - 补偿量与干预强度成正比：短暂减速给少量，路线降级给大量。
///
/// 接入方式：
///   - 监听 RouteBudgetService.OnRouteDegraded（路线被降级时补偿）。
///   - 监听 TricksterAbilitySystem.OnPropActivated（机关出手时补偿）。
///   - 向 MarioSuspicionTracker 写入 Residue/Evidence 补偿。
///   - 向 MarioCounterplayProbe 推送 Probe 进度加成。
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
/// </summary>
public class InterferenceCompensationPolicy : MonoBehaviour
{
    [Header("=== Commit 2 干预补偿 ===")]
    [Tooltip("路线降级时给 Mario 的 Residue 补偿强度")]
    [SerializeField] private float routeDegradeResidueBonus = 0.4f;

    [Tooltip("路线降级时给 Mario 的证据补偿层数")]
    [SerializeField] private int routeDegradeEvidenceBonus = 1;

    [Tooltip("机关出手时给 Mario 的额外可疑度加成（叠加到 Tracker 自身的量上）")]
    [SerializeField] private float propActivateSuspicionBonus = 8f;

    [Tooltip("每次补偿后给 Mario 的短期推进加速持续时间（秒）")]
    [SerializeField] private float progressBoostDuration = 2f;

    [Tooltip("短期推进加速倍率（1.0=无加速）")]
    [SerializeField] private float progressBoostMultiplier = 1.15f;

    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool showDebugInfo = false;

    // ── 引用 ──
    private RouteBudgetService routeBudget;
    private MarioSuspicionTracker suspicionTracker;
    private TricksterAbilitySystem abilitySystem;
    private TricksterPossessionGate possessionGate;

    // ── 状态 ──
    private float progressBoostTimer;

    // ── 事件 ──
    /// <summary>补偿发放时触发（补偿类型描述, 补偿量）</summary>
    public event Action<string, float> OnCompensationGranted;

    /// <summary>推进加速开始时触发（持续时间）</summary>
    public event Action<float> OnProgressBoostStart;

    // ── 公共属性 ──
    public bool IsProgressBoosted => progressBoostTimer > 0f;
    public float ProgressBoostRemaining => progressBoostTimer;
    public float CurrentProgressMultiplier => IsProgressBoosted ? progressBoostMultiplier : 1f;

    // ─────────────────────────────────────────────────────
    #region 生命周期

    private void Start()
    {
        // 查找依赖
        routeBudget = FindObjectOfType<RouteBudgetService>();
        suspicionTracker = FindObjectOfType<MarioSuspicionTracker>();

        var trickster = FindObjectOfType<TricksterController>();
        if (trickster != null)
        {
            abilitySystem = trickster.GetComponent<TricksterAbilitySystem>();
            possessionGate = trickster.GetComponent<TricksterPossessionGate>();
        }

        // 订阅路线降级事件
        if (routeBudget != null)
        {
            routeBudget.OnRouteDegraded += HandleRouteDegraded;
        }

        // 订阅机关出手事件
        if (abilitySystem != null)
        {
            abilitySystem.OnPropActivated += HandlePropActivated;
        }

        // 订阅回合重置
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart += HandleRoundStart;
        }
    }

    private void OnDestroy()
    {
        if (routeBudget != null)
        {
            routeBudget.OnRouteDegraded -= HandleRouteDegraded;
        }

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
        if (progressBoostTimer > 0f)
        {
            progressBoostTimer -= Time.deltaTime;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 事件处理

    private void HandleRouteDegraded(string routeId, string source)
    {
        // 路线被降级 → 给 Mario 较大补偿
        if (suspicionTracker == null) return;

        // 找到降级来源的锚点，给它加 Residue 和 Evidence
        PossessionAnchor sourceAnchor = FindAnchorBySource(source);
        if (sourceAnchor != null)
        {
            AnchorSuspicionData data = suspicionTracker.GetOrCreateData(sourceAnchor);
            data.SetResidue(Mathf.Max(data.Residue, routeDegradeResidueBonus));
            data.AddEvidence(routeDegradeEvidenceBonus);

            if (showDebugInfo)
            {
                Debug.Log($"[CompensationPolicy] Route {routeId} degraded → " +
                          $"Anchor {sourceAnchor.AnchorId} gets Residue={data.Residue:F2}, " +
                          $"Evidence={data.EvidenceLevel}");
            }
        }

        // 给 Mario 短期推进加速
        GrantProgressBoost("RouteDegraded");

        OnCompensationGranted?.Invoke("RouteDegraded", routeDegradeResidueBonus);
    }

    private void HandlePropActivated(IControllableProp prop)
    {
        if (prop == null || suspicionTracker == null) return;

        Transform propTransform = prop.GetTransform();
        if (propTransform == null) return;

        PossessionAnchor anchor = propTransform.GetComponent<PossessionAnchor>();
        if (anchor == null) return;

        // 机关出手 → 给 Mario 少量额外可疑度补偿（叠加到 Tracker 自身的量上）
        AnchorSuspicionData data = suspicionTracker.GetOrCreateData(anchor);
        data.AddSuspicion(propActivateSuspicionBonus);

        if (showDebugInfo)
        {
            Debug.Log($"[CompensationPolicy] Prop activated at {anchor.AnchorId} → " +
                      $"Extra suspicion +{propActivateSuspicionBonus}, total={data.Suspicion:F0}");
        }

        OnCompensationGranted?.Invoke("PropActivated", propActivateSuspicionBonus);
    }

    private void HandleRoundStart()
    {
        progressBoostTimer = 0f;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 内部逻辑

    private void GrantProgressBoost(string reason)
    {
        progressBoostTimer = progressBoostDuration;
        OnProgressBoostStart?.Invoke(progressBoostDuration);

        if (showDebugInfo)
        {
            Debug.Log($"[CompensationPolicy] Progress boost granted ({reason}): " +
                      $"{progressBoostMultiplier}x for {progressBoostDuration}s");
        }
    }

    private PossessionAnchor FindAnchorBySource(string source)
    {
        // 尝试通过 AnchorId 查找
        var anchors = FindObjectsOfType<PossessionAnchor>();
        for (int i = 0; i < anchors.Length; i++)
        {
            if (anchors[i].AnchorId == source) return anchors[i];
        }
        return null;
    }

    #endregion
}
