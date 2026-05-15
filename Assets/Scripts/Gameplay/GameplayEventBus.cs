using System;
using UnityEngine;

/// <summary>
/// GameplayEventBus — 核心玩法事件总线。
///
/// 设计目标：
///   - 逻辑脚本只发布“发生了什么”，不关心视觉、音效、震动如何表现。
///   - 表现层通过 VisualFeedbackBridge 统一监听这些信号并调用 SEF / Camera Shake。
///   - 事件只携带只读上下文，不修改物理状态、MotionState 或底层循环。
///
/// [AI防坑警告]
///   1. 不要在核心逻辑脚本里直接调用 SpriteEffectController 或 Camera.main.Shake。
///   2. 新增表现需求时优先扩展 Payload 与 VisualFeedbackBridge，禁止把表现代码写回机关逻辑。
///   3. 事件发送必须是 fire-and-forget，不得让监听者反向驱动物理结果。
/// </summary>
public static class GameplayEventBus
{
    // ─────────────────────────────────────────────────────
    #region Payloads

    public sealed class TrapTriggeredPayload
    {
        public readonly GameObject source;
        public readonly GameObject target;
        public readonly Vector3 worldPosition;
        public readonly float hitFlashDuration;
        public readonly Color hitFlashColor;
        public readonly bool playDissolve;
        public readonly float dissolveDuration;
        public readonly float shakeDuration;
        public readonly float shakeMagnitude;
        public readonly string reason;

        public TrapTriggeredPayload(
            GameObject source,
            GameObject target,
            Vector3 worldPosition,
            float hitFlashDuration,
            Color hitFlashColor,
            bool playDissolve,
            float dissolveDuration,
            float shakeDuration,
            float shakeMagnitude,
            string reason)
        {
            this.source = source;
            this.target = target;
            this.worldPosition = worldPosition;
            this.hitFlashDuration = hitFlashDuration;
            this.hitFlashColor = hitFlashColor;
            this.playDissolve = playDissolve;
            this.dissolveDuration = dissolveDuration;
            this.shakeDuration = shakeDuration;
            this.shakeMagnitude = shakeMagnitude;
            this.reason = reason;
        }
    }

    public sealed class BouncyPlatformLaunchedPayload
    {
        public readonly GameObject platform;
        public readonly GameObject target;
        public readonly Vector3 worldPosition;
        public readonly Vector2 launchVelocity;
        public readonly float flashDuration;
        public readonly Color flashColor;
        public readonly float shakeDuration;
        public readonly float shakeMagnitude;

        public BouncyPlatformLaunchedPayload(
            GameObject platform,
            GameObject target,
            Vector3 worldPosition,
            Vector2 launchVelocity,
            float flashDuration,
            Color flashColor,
            float shakeDuration,
            float shakeMagnitude)
        {
            this.platform = platform;
            this.target = target;
            this.worldPosition = worldPosition;
            this.launchVelocity = launchVelocity;
            this.flashDuration = flashDuration;
            this.flashColor = flashColor;
            this.shakeDuration = shakeDuration;
            this.shakeMagnitude = shakeMagnitude;
        }
    }

    public sealed class TricksterRevealedPayload
    {
        public readonly GameObject source;
        public readonly TricksterController trickster;
        public readonly Vector3 worldPosition;
        public readonly float evidence;
        public readonly string reason;

        public TricksterRevealedPayload(GameObject source, TricksterController trickster, Vector3 worldPosition, float evidence, string reason)
        {
            this.source = source;
            this.trickster = trickster;
            this.worldPosition = worldPosition;
            this.evidence = evidence;
            this.reason = reason;
        }
    }

    public sealed class HeatTierChangedPayload
    {
        public readonly TricksterHeatMeter heatMeter;
        public readonly TricksterHeatMeter.HeatTier newTier;
        public readonly TricksterHeatMeter.HeatTier oldTier;
        public readonly float heat;
        public readonly float normalizedHeat;

        public HeatTierChangedPayload(
            TricksterHeatMeter heatMeter,
            TricksterHeatMeter.HeatTier newTier,
            TricksterHeatMeter.HeatTier oldTier,
            float heat,
            float normalizedHeat)
        {
            this.heatMeter = heatMeter;
            this.newTier = newTier;
            this.oldTier = oldTier;
            this.heat = heat;
            this.normalizedHeat = normalizedHeat;
        }
    }

    public sealed class CrisisWarningPayload
    {
        public readonly AlarmCrisisDirector director;
        public readonly Vector3 worldPosition;
        public readonly float duration;
        public readonly float radius;
        public readonly float shakeDuration;
        public readonly float shakeMagnitude;
        public readonly string warningType;

        public CrisisWarningPayload(
            AlarmCrisisDirector director,
            Vector3 worldPosition,
            float duration,
            float radius,
            float shakeDuration,
            float shakeMagnitude,
            string warningType)
        {
            this.director = director;
            this.worldPosition = worldPosition;
            this.duration = duration;
            this.radius = radius;
            this.shakeDuration = shakeDuration;
            this.shakeMagnitude = shakeMagnitude;
            this.warningType = warningType;
        }
    }

    public sealed class ResidueSpottedPayload
    {
        public readonly PossessionAnchor anchor;
        public readonly GameObject target;
        public readonly Vector3 worldPosition;
        public readonly TricksterHeatMeter.HeatTier heatTier;
        public readonly float residue;
        public readonly float suspicion;
        public readonly float intensity;
        public readonly string reason;

        public ResidueSpottedPayload(
            PossessionAnchor anchor,
            GameObject target,
            Vector3 worldPosition,
            TricksterHeatMeter.HeatTier heatTier,
            float residue,
            float suspicion,
            float intensity,
            string reason)
        {
            this.anchor = anchor;
            this.target = target;
            this.worldPosition = worldPosition;
            this.heatTier = heatTier;
            this.residue = residue;
            this.suspicion = suspicion;
            this.intensity = intensity;
            this.reason = reason;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region Events

    public static event Action<TrapTriggeredPayload> OnTrapTriggered;
    public static event Action<BouncyPlatformLaunchedPayload> OnBouncyPlatformLaunched;
    public static event Action<TricksterRevealedPayload> OnTricksterRevealed;
    public static event Action<HeatTierChangedPayload> OnHeatTierChanged;
    public static event Action<CrisisWarningPayload> OnCrisisWarning;
    public static event Action<ResidueSpottedPayload> OnResidueSpotted;

    #endregion

    // ─────────────────────────────────────────────────────
    #region Send API

    public static void SendTrapTriggered(
        GameObject source,
        GameObject target,
        Vector3 worldPosition,
        float hitFlashDuration = 0.15f,
        Color? hitFlashColor = null,
        bool playDissolve = false,
        float dissolveDuration = 0.8f,
        float shakeDuration = 0.18f,
        float shakeMagnitude = 0.08f,
        string reason = null)
    {
        OnTrapTriggered?.Invoke(new TrapTriggeredPayload(
            source,
            target,
            worldPosition,
            hitFlashDuration,
            hitFlashColor ?? Color.white,
            playDissolve,
            dissolveDuration,
            shakeDuration,
            shakeMagnitude,
            reason));
    }

    public static void SendBouncyPlatformLaunched(
        GameObject platform,
        GameObject target,
        Vector3 worldPosition,
        Vector2 launchVelocity,
        float flashDuration = 0.12f,
        Color? flashColor = null,
        float shakeDuration = 0.08f,
        float shakeMagnitude = 0.04f)
    {
        OnBouncyPlatformLaunched?.Invoke(new BouncyPlatformLaunchedPayload(
            platform,
            target,
            worldPosition,
            launchVelocity,
            flashDuration,
            flashColor ?? Color.white,
            shakeDuration,
            shakeMagnitude));
    }

    public static void SendTricksterRevealed(GameObject source, TricksterController trickster, Vector3 worldPosition, float evidence, string reason = null)
    {
        OnTricksterRevealed?.Invoke(new TricksterRevealedPayload(source, trickster, worldPosition, evidence, reason));
    }

    public static void SendHeatTierChanged(
        TricksterHeatMeter heatMeter,
        TricksterHeatMeter.HeatTier newTier,
        TricksterHeatMeter.HeatTier oldTier,
        float heat,
        float normalizedHeat)
    {
        OnHeatTierChanged?.Invoke(new HeatTierChangedPayload(heatMeter, newTier, oldTier, heat, normalizedHeat));
    }

    public static void SendCrisisWarning(
        AlarmCrisisDirector director,
        Vector3 worldPosition,
        float duration,
        float radius,
        float shakeDuration = 0.2f,
        float shakeMagnitude = 0.08f,
        string warningType = null)
    {
        OnCrisisWarning?.Invoke(new CrisisWarningPayload(
            director,
            worldPosition,
            duration,
            radius,
            shakeDuration,
            shakeMagnitude,
            warningType));
    }

    public static void SendResidueSpotted(
        PossessionAnchor anchor,
        GameObject target,
        Vector3 worldPosition,
        TricksterHeatMeter.HeatTier heatTier,
        float residue,
        float suspicion,
        float intensity,
        string reason = null)
    {
        OnResidueSpotted?.Invoke(new ResidueSpottedPayload(
            anchor,
            target,
            worldPosition,
            heatTier,
            residue,
            suspicion,
            Mathf.Clamp01(intensity),
            reason));
    }

    /// <summary>测试 / 退出 PlayMode 时清理静态订阅，避免跨场景残留。</summary>
    public static void ClearAllListenersForTests()
    {
        OnTrapTriggered = null;
        OnBouncyPlatformLaunched = null;
        OnTricksterRevealed = null;
        OnHeatTierChanged = null;
        OnCrisisWarning = null;
        OnResidueSpotted = null;
    }

    #endregion
}
