using UnityEngine;

// ═══════════════════════════════════════════════════════════════════
// GameplayLoopConfigSO — Gameplay Loop 对抗节奏唯一真理源
//
// [AI防坑警告] 此文件集中承载 Trickster/Mario 对抗循环中的消耗、冷却、倍率、阈值与衰减参数。
// 运行时代码必须优先通过 GameplayMetrics Facade 读取这里的实时值；当资源缺失或测试隔离运行时，
// 仍回退到各组件本地 SerializeField 默认值，确保旧场景和自动化测试零行为变化。
//
// 资源路径：Assets/Resources/GameplayLoopConfig.asset
// 使用方式：PlayMode 下通过 Level Studio 的 Game Loop Tuning 页拖动滑块即可实时调节。
// ═══════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "GameplayLoopConfig", menuName = "MarioTrickster/Gameplay Loop Config")]
public class GameplayLoopConfigSO : ScriptableObject
{
    // ═══════════════════════════════════════════════════
    // EnergySystem
    // ═══════════════════════════════════════════════════

    [Header("Energy System")]

    [Tooltip("Trickster 最大能量值")]
    [Range(1f, 300f)] public float energyMaxEnergy = 100f;

    [Tooltip("Trickster 初始能量值（-1 = 满能量）")]
    [Range(-1f, 300f)] public float energyStartEnergy = -1f;

    [Tooltip("变身消耗的固定能量")]
    [Range(0f, 100f)] public float energyDisguiseCost = 20f;

    [Tooltip("变身期间每秒消耗的能量")]
    [Range(0f, 50f)] public float energyDisguiseDrainPerSecond = 5f;

    [Tooltip("完全融入场景后，维持消耗的倍率")]
    [Range(0f, 2f)] public float energyBlendedDrainMultiplier = 0.5f;

    [Tooltip("操控道具消耗的固定能量")]
    [Range(0f, 100f)] public float energyControlCost = 15f;

    [Tooltip("未变身时每秒恢复的能量")]
    [Range(0f, 50f)] public float energyRegenPerSecond = 8f;

    [Tooltip("变身时能量恢复倍率")]
    [Range(0f, 2f)] public float energyDisguisedRegenMultiplier = 0f;

    [Tooltip("操控道具后恢复延迟（秒）")]
    [Range(0f, 10f)] public float energyRegenDelayAfterControl = 2f;

    [Tooltip("能量低于此比例时触发低能量警告")]
    [Range(0f, 1f)] public float energyLowEnergyThreshold = 0.25f;

    // ═══════════════════════════════════════════════════
    // ScanAbility
    // ═══════════════════════════════════════════════════

    [Header("Scan Ability")]

    [Tooltip("Mario Q 扫描半径")]
    [Range(0.5f, 20f)] public float scanRadius = 5f;

    [Tooltip("Mario Q 扫描冷却时间（秒）")]
    [Range(0f, 30f)] public float scanCooldown = 8f;

    [Tooltip("扫描暴露视觉持续时间（秒）")]
    [Range(0f, 10f)] public float scanRevealDuration = 2f;

    [Tooltip("Q 扫描命中后额外延长 Trickster Revealed 门禁的时间（秒）")]
    [Range(0f, 10f)] public float scanRevealGateBonusDuration = 1.2f;

    [Tooltip("扫描脉冲扩散速度")]
    [Range(1f, 40f)] public float scanPulseSpeed = 15f;

    [Tooltip("扫描脉冲线条宽度")]
    [Range(0.01f, 1f)] public float scanPulseLineWidth = 0.15f;

    [Tooltip("暴露时闪烁频率")]
    [Range(0.1f, 30f)] public float scanFlashFrequency = 6f;

    [Tooltip("暴露标记颜色")]
    public Color scanRevealColor = new Color(1f, 0.2f, 0.2f, 0.8f);

    // ═══════════════════════════════════════════════════
    // TricksterPossessionGate
    // ═══════════════════════════════════════════════════

    [Header("Trickster Possession Gate")]

    [Tooltip("出手后保持暴露状态的基础时间（秒）")]
    [Range(0f, 10f)] public float possessionRevealDuration = 0.8f;

    [Tooltip("暴露结束后的撤离缓冲时间（秒）")]
    [Range(0f, 5f)] public float possessionEscapeDuration = 0.35f;

    // ═══════════════════════════════════════════════════
    // AlarmCrisisDirector
    // ═══════════════════════════════════════════════════

    [Header("Alarm Crisis Director")]

    [Tooltip("扫描波预告时间（秒）")]
    [Range(0f, 10f)] public float alarmWarningDuration = 2f;

    [Tooltip("扫描波从左到右扫过的速度（单位/秒）")]
    [Range(0.5f, 60f)] public float alarmScanSpeed = 12f;

    [Tooltip("扫描波宽度（单位）")]
    [Range(0.1f, 10f)] public float alarmScanWidth = 2f;

    [Tooltip("扫描波经过时对已有证据的放大倍率")]
    [Range(0f, 5f)] public float alarmEvidenceAmplifyFactor = 1.5f;

    [Tooltip("扫描波经过时给有残留锚点增加的额外 Suspicion")]
    [Range(0f, 100f)] public float alarmScanSuspicionBonus = 15f;

    [Tooltip("触发扫描波的最低热度档位")]
    public TricksterHeatMeter.HeatTier alarmTriggerTier = TricksterHeatMeter.HeatTier.Alert;

    [Tooltip("两次扫描波之间的最小间隔（秒）")]
    [Range(0f, 60f)] public float alarmScanCooldown = 15f;

    [Tooltip("Lockdown 触发时是否立即启动扫描波")]
    public bool alarmLockdownForcesScan = true;

    // ═══════════════════════════════════════════════════
    // RouteBudgetService
    // ═══════════════════════════════════════════════════

    [Header("Route Budget Service")]

    [Tooltip("降级路线的自动恢复时间（秒）")]
    [Range(0f, 30f)] public float routeAutoRecoveryTime = 6f;

    [Tooltip("同时允许降级的最大路线数（当总路线<=2时强制为1）")]
    [Range(1, 8)] public int routeMaxSimultaneousDegraded = 1;

    // ═══════════════════════════════════════════════════
    // TricksterHeatMeter
    // ═══════════════════════════════════════════════════

    [Header("Trickster Heat Meter")]

    [Tooltip("进入附身点（Possessing）时增加的热度")]
    [Range(0f, 50f)] public float heatPerPossession = 5f;

    [Tooltip("操控机关时增加的基础热度")]
    [Range(0f, 80f)] public float heatPerActivation = 12f;

    [Tooltip("连锁倍率对热度的加成系数")]
    [Range(0f, 3f)] public float heatComboHeatFactor = 0.4f;

    [Tooltip("连锁断裂时每段连锁的热度惩罚")]
    [Range(0f, 30f)] public float heatComboBreakHeatPerChain = 3f;

    [Tooltip("热度每秒自然衰减量")]
    [Range(0f, 20f)] public float heatDecayPerSecond = 1.5f;

    [Tooltip("Lockdown 触发后热度回落到的目标值")]
    [Range(0f, 100f)] public float heatLockdownFallbackHeat = 60f;

    [Tooltip("Lockdown 触发后的冷却时间（秒）")]
    [Range(0f, 60f)] public float heatLockdownCooldown = 10f;

    [Tooltip("热度对 Mario 证据衰减的影响系数")]
    [Range(0f, 2f)] public float heatToDecaySlowdown = 0.5f;

    [Tooltip("Suspicious 档位阈值")]
    [Range(0f, 100f)] public float heatSuspiciousThreshold = 30f;

    [Tooltip("Alert 档位阈值")]
    [Range(0f, 100f)] public float heatAlertThreshold = 60f;

    [Tooltip("Lockdown 档位阈值")]
    [Range(0f, 100f)] public float heatLockdownThreshold = 85f;

    // ═══════════════════════════════════════════════════
    // PropComboTracker
    // ═══════════════════════════════════════════════════

    [Header("Prop Combo Tracker")]

    [Tooltip("连锁窗口（秒）")]
    [Range(0f, 10f)] public float comboWindow = 2.5f;

    [Tooltip("不同附身点出手的倍率加成")]
    [Range(0f, 5f)] public float comboDifferentAnchorMultiplier = 1.5f;

    [Tooltip("不同机关类型出手的倍率加成")]
    [Range(0f, 5f)] public float comboDifferentPropTypeMultiplier = 1.3f;

    [Tooltip("重复同一附身点出手的倍率")]
    [Range(0f, 2f)] public float comboSameAnchorMultiplier = 0.7f;

    [Tooltip("重复同一机关的倍率")]
    [Range(0f, 2f)] public float comboSamePropMultiplier = 0.5f;

    [Tooltip("重复同一附身点时额外叠加的 Suspicion")]
    [Range(0f, 100f)] public float comboSameAnchorSuspicionBonus = 15f;

    [Tooltip("连锁断裂后的冷却时间（秒）")]
    [Range(0f, 10f)] public float comboBreakCooldown = 1f;

    // ═══════════════════════════════════════════════════
    // InterferenceCompensationPolicy
    // ═══════════════════════════════════════════════════

    [Header("Interference Compensation Policy")]

    [Tooltip("路线降级时给 Mario 返还的 Residue")]
    [Range(0f, 2f)] public float compensationRouteDegradeResidueBonus = 0.4f;

    [Tooltip("路线降级时给 Mario 返还的 Evidence")]
    [Range(0, 10)] public int compensationRouteDegradeEvidenceBonus = 1;

    [Tooltip("机关触发时给 Mario 返还的 Suspicion")]
    [Range(0f, 100f)] public float compensationPropActivateSuspicionBonus = 8f;

    [Tooltip("短期推进加速持续时间（秒）")]
    [Range(0f, 10f)] public float compensationProgressBoostDuration = 2f;

    [Tooltip("短期推进加速倍率")]
    [Range(1f, 3f)] public float compensationProgressBoostMultiplier = 1.15f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        energyMaxEnergy = Mathf.Max(1f, energyMaxEnergy);
        energyStartEnergy = energyStartEnergy < 0f ? -1f : Mathf.Min(energyStartEnergy, energyMaxEnergy);
        energyDisguiseCost = Mathf.Max(0f, energyDisguiseCost);
        energyControlCost = Mathf.Max(0f, energyControlCost);
        routeMaxSimultaneousDegraded = Mathf.Max(1, routeMaxSimultaneousDegraded);

        heatSuspiciousThreshold = Mathf.Clamp(heatSuspiciousThreshold, 0f, 100f);
        heatAlertThreshold = Mathf.Clamp(heatAlertThreshold, heatSuspiciousThreshold, 100f);
        heatLockdownThreshold = Mathf.Clamp(heatLockdownThreshold, heatAlertThreshold, 100f);
        heatLockdownFallbackHeat = Mathf.Clamp(heatLockdownFallbackHeat, 0f, heatLockdownThreshold);
    }
#endif
}
