using UnityEngine;

/// <summary>
/// Commit 4：热度破绽提示 — 热度越高，附身点的视觉破绽越明显。
///
/// 核心规则（S130 第二阶段）：
///   - Calm：无额外表现提示。
///   - Suspicious：有 Suspicion > 20 的锚点轻微闪烁。
///   - Alert：有 Suspicion > 10 的锚点明显闪烁。
///   - Lockdown：所有有 Residue 的锚点强烈闪烁。
///
/// 实现方式：
///   - 读取 TricksterHeatMeter.CurrentTier 和 MarioSuspicionTracker 数据。
///   - 仅发送 GameplayEventBus.OnResidueSpotted 信号，由 VisualFeedbackBridge 统一改色 / SEF 表现。
///   - 不修改碰撞体或物理。
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
///   - 不直接操作 SpriteRenderer.color 或 SpriteEffectController。
/// </summary>
public class HeatBreachHint : MonoBehaviour
{
    [Header("=== Commit 4 热度破绽提示 ===")]
    [Tooltip("Suspicious 档闪烁速度")]
    [SerializeField] private float suspiciousFlickerSpeed = 3f;

    [Tooltip("Alert 档闪烁速度")]
    [SerializeField] private float alertFlickerSpeed = 6f;

    [Tooltip("Lockdown 档闪烁速度")]
    [SerializeField] private float lockdownFlickerSpeed = 10f;

    [Tooltip("Suspicious 档最小可疑度阈值")]
    [SerializeField] private float suspiciousMinSuspicion = 20f;

    [Tooltip("Alert 档最小可疑度阈值")]
    [SerializeField] private float alertMinSuspicion = 10f;

    // ── 引用 ──
    private TricksterHeatMeter heatMeter;
    private MarioSuspicionTracker suspicionTracker;

    // ── 缓存 ──
    private PossessionAnchor[] cachedAnchors;
    private float refreshTimer;

    private void Start()
    {
        heatMeter = FindObjectOfType<TricksterHeatMeter>();
        suspicionTracker = FindObjectOfType<MarioSuspicionTracker>();
        RefreshAnchors();
    }

    private void Update()
    {
        if (heatMeter == null || suspicionTracker == null) return;
        if (cachedAnchors == null || cachedAnchors.Length == 0) return;

        // 定期刷新锚点缓存（处理动态生成）
        refreshTimer += Time.deltaTime;
        if (refreshTimer > 3f)
        {
            refreshTimer = 0f;
            RefreshAnchors();
        }

        var tier = heatMeter.CurrentTier;
        if (tier == TricksterHeatMeter.HeatTier.Calm)
        {
            // Calm 档只发送清除表现信号，实际恢复颜色交给 VisualFeedbackBridge。
            SendClearHints(tier, "HeatBreachHint.Calm");
            return;
        }

        float flickerSpeed = GetFlickerSpeed(tier);
        float minSuspicion = GetMinSuspicion(tier);
        float flicker = (Mathf.Sin(Time.time * flickerSpeed) + 1f) * 0.5f;

        for (int i = 0; i < cachedAnchors.Length; i++)
        {
            PossessionAnchor anchor = cachedAnchors[i];
            if (anchor == null) continue;

            AnchorSuspicionData data = suspicionTracker.GetData(anchor);
            if (data == null)
            {
                SendResidueHint(anchor, tier, 0f, 0f, 0f, "HeatBreachHint.NoData");
                continue;
            }

            bool shouldHint;
            if (tier == TricksterHeatMeter.HeatTier.Lockdown)
            {
                // Lockdown：所有有 Residue 或 Suspicion 的锚点
                shouldHint = data.Residue > 0.05f || data.Suspicion > 5f;
            }
            else
            {
                // Suspicious / Alert：超过阈值的锚点
                shouldHint = data.Suspicion >= minSuspicion;
            }

            float intensity = shouldHint ? Mathf.Lerp(0.2f, 0.8f, flicker) : 0f;
            SendResidueHint(anchor, tier, data.Residue, data.Suspicion, intensity, "HeatBreachHint");
        }
    }

    // ─────────────────────────────────────────────────────
    #region 工具方法

    private void RefreshAnchors()
    {
        cachedAnchors = FindObjectsOfType<PossessionAnchor>();
    }

    private void SendClearHints(TricksterHeatMeter.HeatTier tier, string reason)
    {
        for (int i = 0; i < cachedAnchors.Length; i++)
        {
            PossessionAnchor anchor = cachedAnchors[i];
            if (anchor == null) continue;
            SendResidueHint(anchor, tier, 0f, 0f, 0f, reason);
        }
    }

    private void SendResidueHint(
        PossessionAnchor anchor,
        TricksterHeatMeter.HeatTier tier,
        float residue,
        float suspicion,
        float intensity,
        string reason)
    {
        GameplayEventBus.SendResidueSpotted(
            anchor,
            anchor.gameObject,
            anchor.transform.position,
            tier,
            residue,
            suspicion,
            intensity,
            reason);
    }

    private float GetFlickerSpeed(TricksterHeatMeter.HeatTier tier)
    {
        switch (tier)
        {
            case TricksterHeatMeter.HeatTier.Suspicious: return suspiciousFlickerSpeed;
            case TricksterHeatMeter.HeatTier.Alert: return alertFlickerSpeed;
            case TricksterHeatMeter.HeatTier.Lockdown: return lockdownFlickerSpeed;
            default: return 0f;
        }
    }

    private float GetMinSuspicion(TricksterHeatMeter.HeatTier tier)
    {
        switch (tier)
        {
            case TricksterHeatMeter.HeatTier.Suspicious: return suspiciousMinSuspicion;
            case TricksterHeatMeter.HeatTier.Alert: return alertMinSuspicion;
            case TricksterHeatMeter.HeatTier.Lockdown: return 0f;
            default: return 100f;
        }
    }

    #endregion
}
