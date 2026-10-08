using UnityEngine;

/// <summary>
/// Gameplay Loop 数值的读取入口（组件只通过这里读，读不到就用组件自己的默认值）。
///
/// S238：以前有第二个调参文件 Assets/Resources/GameplayLoopConfig.asset（54 个数值）。查下来第 1 步真正用到的只有
/// 扫描 8 + 能量 10 + 附身 2 = 20 个 → 搬进唯一的调参文件 RushMarioTuning（「你的技能」组），默认值一模一样。
/// 另外 34 个（热度 / 警报扫描波 / 连锁倍率 / 路线预算 / 补偿）属于设计宪法第 0 步关掉的扩展系统（CoreLoopOnly = true，
/// 第 1 步房间根本不装这些组件）→ 直接用各组件自己的默认值（和旧文件里的值相同，sim 检查过）。以后真要打开扩展系统，
/// 再把它们的数值加进 RushMarioTuning，不要再建第二个调参文件。
/// </summary>
public static class GameplayMetrics
{
    private static MarioMindTuningSO tuning;
    private static bool searched;

    /// <summary>唯一的调参文件（Resources/Step1/RushMarioTuning）；找不到 = null，组件用自己的默认值。</summary>
    public static MarioMindTuningSO Tuning
    {
        get
        {
            if (!searched) { tuning = Resources.Load<MarioMindTuningSO>(MarioMindTuningSO.ResourcePath); searched = true; }
            return tuning;
        }
    }

    /// <summary>供测试 / 编辑器手动指定（传 null = 回到自动加载）。</summary>
    public static void SetTuning(MarioMindTuningSO t) { tuning = t; searched = t != null; }

    // EnergySystem（你的能量）
    public static float EnergyMaxEnergy(float fallback) => Tuning != null ? Tuning.energyMaxEnergy : fallback;
    public static float EnergyStartEnergy(float fallback) => Tuning != null ? Tuning.energyStartEnergy : fallback;
    public static float EnergyDisguiseCost(float fallback) => Tuning != null ? Tuning.energyDisguiseCost : fallback;
    public static float EnergyDisguiseDrainPerSecond(float fallback) => Tuning != null ? Tuning.energyDisguiseDrainPerSecond : fallback;
    public static float EnergyBlendedDrainMultiplier(float fallback) => Tuning != null ? Tuning.energyBlendedDrainMultiplier : fallback;
    public static float EnergyControlCost(float fallback) => Tuning != null ? Tuning.energyControlCost : fallback;
    public static float EnergyRegenPerSecond(float fallback) => Tuning != null ? Tuning.energyRegenPerSecond : fallback;
    public static float EnergyDisguisedRegenMultiplier(float fallback) => Tuning != null ? Tuning.energyDisguisedRegenMultiplier : fallback;
    public static float EnergyRegenDelayAfterControl(float fallback) => Tuning != null ? Tuning.energyRegenDelayAfterControl : fallback;
    public static float EnergyLowEnergyThreshold(float fallback) => Tuning != null ? Tuning.energyLowEnergyThreshold : fallback;

    // ScanAbility（马里奥 Q 扫描）
    public static float ScanRadius(float fallback) => Tuning != null ? Tuning.scanRadius : fallback;
    public static float ScanCooldown(float fallback) => Tuning != null ? Tuning.scanCooldown : fallback;
    public static float ScanRevealDuration(float fallback) => Tuning != null ? Tuning.scanRevealDuration : fallback;
    public static float ScanRevealGateBonusDuration(float fallback) => Tuning != null ? Tuning.scanRevealGateBonusDuration : fallback;
    public static float ScanPulseSpeed(float fallback) => Tuning != null ? Tuning.scanPulseSpeed : fallback;
    public static float ScanPulseLineWidth(float fallback) => Tuning != null ? Tuning.scanPulseLineWidth : fallback;
    public static float ScanFlashFrequency(float fallback) => Tuning != null ? Tuning.scanFlashFrequency : fallback;
    public static Color ScanRevealColor(Color fallback) => Tuning != null ? Tuning.scanRevealColor : fallback;

    // TricksterPossessionGate（发动后暴露多久）
    public static float PossessionRevealDuration(float fallback) => Tuning != null ? Tuning.possessionRevealDuration : fallback;
    public static float PossessionEscapeDuration(float fallback) => Tuning != null ? Tuning.possessionEscapeDuration : fallback;

    // 扩展系统（第 0 步已关；用组件自己的默认值）：热度 / 警报扫描波 / 路线预算 / 连锁倍率 / 补偿
    public static float AlarmWarningDuration(float fallback) => fallback;
    public static float AlarmScanSpeed(float fallback) => fallback;
    public static float AlarmScanWidth(float fallback) => fallback;
    public static float AlarmEvidenceAmplifyFactor(float fallback) => fallback;
    public static float AlarmScanSuspicionBonus(float fallback) => fallback;
    public static TricksterHeatMeter.HeatTier AlarmTriggerTier(TricksterHeatMeter.HeatTier fallback) => fallback;
    public static float AlarmScanCooldown(float fallback) => fallback;
    public static bool AlarmLockdownForcesScan(bool fallback) => fallback;
    public static float RouteAutoRecoveryTime(float fallback) => fallback;
    public static int RouteMaxSimultaneousDegraded(int fallback) => fallback;
    public static float HeatPerPossession(float fallback) => fallback;
    public static float HeatPerActivation(float fallback) => fallback;
    public static float HeatComboHeatFactor(float fallback) => fallback;
    public static float HeatComboBreakHeatPerChain(float fallback) => fallback;
    public static float HeatDecayPerSecond(float fallback) => fallback;
    public static float HeatLockdownFallbackHeat(float fallback) => fallback;
    public static float HeatLockdownCooldown(float fallback) => fallback;
    public static float HeatToDecaySlowdown(float fallback) => fallback;
    public static float HeatSuspiciousThreshold(float fallback) => fallback;
    public static float HeatAlertThreshold(float fallback) => fallback;
    public static float HeatLockdownThreshold(float fallback) => fallback;
    public static float ComboWindow(float fallback) => fallback;
    public static float ComboDifferentAnchorMultiplier(float fallback) => fallback;
    public static float ComboDifferentPropTypeMultiplier(float fallback) => fallback;
    public static float ComboSameAnchorMultiplier(float fallback) => fallback;
    public static float ComboSamePropMultiplier(float fallback) => fallback;
    public static float ComboSameAnchorSuspicionBonus(float fallback) => fallback;
    public static float ComboBreakCooldown(float fallback) => fallback;
    public static float CompensationRouteDegradeResidueBonus(float fallback) => fallback;
    public static int CompensationRouteDegradeEvidenceBonus(int fallback) => fallback;
    public static float CompensationPropActivateSuspicionBonus(float fallback) => fallback;
    public static float CompensationProgressBoostDuration(float fallback) => fallback;
    public static float CompensationProgressBoostMultiplier(float fallback) => fallback;
}
