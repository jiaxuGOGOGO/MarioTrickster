using UnityEngine;

/// <summary>
/// Gameplay Loop 调参 Facade — 对抗节奏参数的运行时唯一读取入口。
///
/// [AI防坑警告] 所有 Gameplay Loop 消耗、冷却、倍率、阈值与衰减参数都应优先从本类读取。
/// 本类懒加载 Assets/Resources/GameplayLoopConfig.asset；资源缺失时返回调用方传入的本地默认值，
/// 从而保证旧场景、隔离单元测试和无资源环境不崩溃、不改行为。
/// </summary>
public static class GameplayMetrics
{
    private const string RESOURCE_PATH = "GameplayLoopConfig";

    private static GameplayLoopConfigSO _activeConfig;
    private static bool _configSearched;

    /// <summary>当前活跃 GameplayLoopConfigSO。首次访问时从 Resources 懒加载。</summary>
    public static GameplayLoopConfigSO ActiveConfig
    {
        get
        {
            if (!_configSearched)
            {
                _activeConfig = Resources.Load<GameplayLoopConfigSO>(RESOURCE_PATH);
                _configSearched = true;
            }

            return _activeConfig;
        }
    }

    /// <summary>供测试、编辑器窗口或特殊场景手动注入配置。传 null 可回到自动加载模式。</summary>
    public static void SetActiveConfig(GameplayLoopConfigSO config)
    {
        _activeConfig = config;
        _configSearched = config != null;
    }

    /// <summary>强制重新搜索 Resources 中的 GameplayLoopConfigSO。</summary>
    public static void RefreshConfig()
    {
        _activeConfig = null;
        _configSearched = false;
    }

    public static bool HasActiveConfig => ActiveConfig != null;

    // EnergySystem
    public static float EnergyMaxEnergy(float fallback) => ActiveConfig != null ? ActiveConfig.energyMaxEnergy : fallback;
    public static float EnergyStartEnergy(float fallback) => ActiveConfig != null ? ActiveConfig.energyStartEnergy : fallback;
    public static float EnergyDisguiseCost(float fallback) => ActiveConfig != null ? ActiveConfig.energyDisguiseCost : fallback;
    public static float EnergyDisguiseDrainPerSecond(float fallback) => ActiveConfig != null ? ActiveConfig.energyDisguiseDrainPerSecond : fallback;
    public static float EnergyBlendedDrainMultiplier(float fallback) => ActiveConfig != null ? ActiveConfig.energyBlendedDrainMultiplier : fallback;
    public static float EnergyControlCost(float fallback) => ActiveConfig != null ? ActiveConfig.energyControlCost : fallback;
    public static float EnergyRegenPerSecond(float fallback) => ActiveConfig != null ? ActiveConfig.energyRegenPerSecond : fallback;
    public static float EnergyDisguisedRegenMultiplier(float fallback) => ActiveConfig != null ? ActiveConfig.energyDisguisedRegenMultiplier : fallback;
    public static float EnergyRegenDelayAfterControl(float fallback) => ActiveConfig != null ? ActiveConfig.energyRegenDelayAfterControl : fallback;
    public static float EnergyLowEnergyThreshold(float fallback) => ActiveConfig != null ? ActiveConfig.energyLowEnergyThreshold : fallback;

    // ScanAbility
    public static float ScanRadius(float fallback) => ActiveConfig != null ? ActiveConfig.scanRadius : fallback;
    public static float ScanCooldown(float fallback) => ActiveConfig != null ? ActiveConfig.scanCooldown : fallback;
    public static float ScanRevealDuration(float fallback) => ActiveConfig != null ? ActiveConfig.scanRevealDuration : fallback;
    public static float ScanRevealGateBonusDuration(float fallback) => ActiveConfig != null ? ActiveConfig.scanRevealGateBonusDuration : fallback;
    public static float ScanPulseSpeed(float fallback) => ActiveConfig != null ? ActiveConfig.scanPulseSpeed : fallback;
    public static float ScanPulseLineWidth(float fallback) => ActiveConfig != null ? ActiveConfig.scanPulseLineWidth : fallback;
    public static float ScanFlashFrequency(float fallback) => ActiveConfig != null ? ActiveConfig.scanFlashFrequency : fallback;
    public static Color ScanRevealColor(Color fallback) => ActiveConfig != null ? ActiveConfig.scanRevealColor : fallback;

    // TricksterPossessionGate
    public static float PossessionRevealDuration(float fallback) => ActiveConfig != null ? ActiveConfig.possessionRevealDuration : fallback;
    public static float PossessionEscapeDuration(float fallback) => ActiveConfig != null ? ActiveConfig.possessionEscapeDuration : fallback;

    // AlarmCrisisDirector
    public static float AlarmWarningDuration(float fallback) => ActiveConfig != null ? ActiveConfig.alarmWarningDuration : fallback;
    public static float AlarmScanSpeed(float fallback) => ActiveConfig != null ? ActiveConfig.alarmScanSpeed : fallback;
    public static float AlarmScanWidth(float fallback) => ActiveConfig != null ? ActiveConfig.alarmScanWidth : fallback;
    public static float AlarmEvidenceAmplifyFactor(float fallback) => ActiveConfig != null ? ActiveConfig.alarmEvidenceAmplifyFactor : fallback;
    public static float AlarmScanSuspicionBonus(float fallback) => ActiveConfig != null ? ActiveConfig.alarmScanSuspicionBonus : fallback;
    public static TricksterHeatMeter.HeatTier AlarmTriggerTier(TricksterHeatMeter.HeatTier fallback) => ActiveConfig != null ? ActiveConfig.alarmTriggerTier : fallback;
    public static float AlarmScanCooldown(float fallback) => ActiveConfig != null ? ActiveConfig.alarmScanCooldown : fallback;
    public static bool AlarmLockdownForcesScan(bool fallback) => ActiveConfig != null ? ActiveConfig.alarmLockdownForcesScan : fallback;

    // RouteBudgetService
    public static float RouteAutoRecoveryTime(float fallback) => ActiveConfig != null ? ActiveConfig.routeAutoRecoveryTime : fallback;
    public static int RouteMaxSimultaneousDegraded(int fallback) => ActiveConfig != null ? ActiveConfig.routeMaxSimultaneousDegraded : fallback;

    // TricksterHeatMeter
    public static float HeatPerPossession(float fallback) => ActiveConfig != null ? ActiveConfig.heatPerPossession : fallback;
    public static float HeatPerActivation(float fallback) => ActiveConfig != null ? ActiveConfig.heatPerActivation : fallback;
    public static float HeatComboHeatFactor(float fallback) => ActiveConfig != null ? ActiveConfig.heatComboHeatFactor : fallback;
    public static float HeatComboBreakHeatPerChain(float fallback) => ActiveConfig != null ? ActiveConfig.heatComboBreakHeatPerChain : fallback;
    public static float HeatDecayPerSecond(float fallback) => ActiveConfig != null ? ActiveConfig.heatDecayPerSecond : fallback;
    public static float HeatLockdownFallbackHeat(float fallback) => ActiveConfig != null ? ActiveConfig.heatLockdownFallbackHeat : fallback;
    public static float HeatLockdownCooldown(float fallback) => ActiveConfig != null ? ActiveConfig.heatLockdownCooldown : fallback;
    public static float HeatToDecaySlowdown(float fallback) => ActiveConfig != null ? ActiveConfig.heatToDecaySlowdown : fallback;
    public static float HeatSuspiciousThreshold(float fallback) => ActiveConfig != null ? ActiveConfig.heatSuspiciousThreshold : fallback;
    public static float HeatAlertThreshold(float fallback) => ActiveConfig != null ? ActiveConfig.heatAlertThreshold : fallback;
    public static float HeatLockdownThreshold(float fallback) => ActiveConfig != null ? ActiveConfig.heatLockdownThreshold : fallback;

    // PropComboTracker
    public static float ComboWindow(float fallback) => ActiveConfig != null ? ActiveConfig.comboWindow : fallback;
    public static float ComboDifferentAnchorMultiplier(float fallback) => ActiveConfig != null ? ActiveConfig.comboDifferentAnchorMultiplier : fallback;
    public static float ComboDifferentPropTypeMultiplier(float fallback) => ActiveConfig != null ? ActiveConfig.comboDifferentPropTypeMultiplier : fallback;
    public static float ComboSameAnchorMultiplier(float fallback) => ActiveConfig != null ? ActiveConfig.comboSameAnchorMultiplier : fallback;
    public static float ComboSamePropMultiplier(float fallback) => ActiveConfig != null ? ActiveConfig.comboSamePropMultiplier : fallback;
    public static float ComboSameAnchorSuspicionBonus(float fallback) => ActiveConfig != null ? ActiveConfig.comboSameAnchorSuspicionBonus : fallback;
    public static float ComboBreakCooldown(float fallback) => ActiveConfig != null ? ActiveConfig.comboBreakCooldown : fallback;

    // InterferenceCompensationPolicy
    public static float CompensationRouteDegradeResidueBonus(float fallback) => ActiveConfig != null ? ActiveConfig.compensationRouteDegradeResidueBonus : fallback;
    public static int CompensationRouteDegradeEvidenceBonus(int fallback) => ActiveConfig != null ? ActiveConfig.compensationRouteDegradeEvidenceBonus : fallback;
    public static float CompensationPropActivateSuspicionBonus(float fallback) => ActiveConfig != null ? ActiveConfig.compensationPropActivateSuspicionBonus : fallback;
    public static float CompensationProgressBoostDuration(float fallback) => ActiveConfig != null ? ActiveConfig.compensationProgressBoostDuration : fallback;
    public static float CompensationProgressBoostMultiplier(float fallback) => ActiveConfig != null ? ActiveConfig.compensationProgressBoostMultiplier : fallback;
}
