using UnityEngine;

/// <summary>
/// Commit 4：热度-可疑度桥接 — 热度越高，Mario 的证据衰减越慢。
///
/// 核心规则：
///   - 每帧读取 TricksterHeatMeter.EvidenceDecaySlowdown。
///   - 将该系数传递给 MarioSuspicionTracker，影响 Suspicion 和 Residue 的衰减速度。
///   - Alert 及以上时，SilentMark 积累速度提升（Mario 更容易读到信息）。
///
/// 接入方式：
///   - 纯读取 + 写入公共 API，不修改任何红线系统。
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
/// </summary>
public class HeatSuspicionBridge : MonoBehaviour
{
    [Header("=== Commit 4 热度-可疑度桥接 ===")]
    [Tooltip("Alert 及以上时 SilentMark 积累速度倍率")]
    [SerializeField] private float alertMarkSpeedBonus = 1.5f;

    [Tooltip("Lockdown 时 SilentMark 积累速度倍率")]
    [SerializeField] private float lockdownMarkSpeedBonus = 2.0f;

    // ── 引用 ──
    private TricksterHeatMeter heatMeter;
    private MarioSuspicionTracker suspicionTracker;
    private SilentMarkSensor silentMarkSensor;

    // ── 公共属性 ──
    /// <summary>当前衰减减速系数（供 MarioSuspicionTracker 读取）</summary>
    public float CurrentDecaySlowdown { get; private set; }

    /// <summary>当前 SilentMark 速度倍率（供 SilentMarkSensor 读取）</summary>
    public float CurrentMarkSpeedMultiplier { get; private set; } = 1f;

    private void Start()
    {
        heatMeter = FindObjectOfType<TricksterHeatMeter>();
        suspicionTracker = FindObjectOfType<MarioSuspicionTracker>();
        silentMarkSensor = FindObjectOfType<SilentMarkSensor>();
    }

    private void Update()
    {
        if (heatMeter == null) return;

        // 更新衰减减速
        CurrentDecaySlowdown = heatMeter.EvidenceDecaySlowdown;

        // 更新 SilentMark 速度倍率
        var tier = heatMeter.CurrentTier;
        if (tier == TricksterHeatMeter.HeatTier.Lockdown)
        {
            CurrentMarkSpeedMultiplier = lockdownMarkSpeedBonus;
        }
        else if (tier == TricksterHeatMeter.HeatTier.Alert)
        {
            CurrentMarkSpeedMultiplier = alertMarkSpeedBonus;
        }
        else
        {
            CurrentMarkSpeedMultiplier = 1f;
        }

        // 将衰减减速传递给 Tracker（通过公共属性，Tracker 在 Update 中读取）
        if (suspicionTracker != null)
        {
            suspicionTracker.DecaySlowdownFactor = CurrentDecaySlowdown;
        }
    }
}
