using UnityEngine;

public enum SuspicionLevel { Calm, Curious, Alert }

/// <summary>
/// 设计宪法 H2：起疑值。纯逻辑，无场景依赖，可在 EditMode 直接测试。
/// 规则：值 ≥ curious 进入 '?'；只有 '?' 已持续 ≥ minOmenSeconds 且值 ≥ alert 才进入 '!'。
/// 因此任何来源（哪怕一次加满）都不可能跳过可见预兆直接识破。
/// </summary>
public sealed class SuspicionMeter
{
    private readonly MarioMindTuningSO t;

    public float Value { get; private set; }
    public SuspicionLevel Level { get; private set; }
    public float OmenSeconds { get; private set; }
    public int OmenCount { get; private set; }
    public float Normalized => t.alertThreshold > 0f ? Mathf.Clamp01(Value / t.alertThreshold) : 0f;

    public SuspicionMeter(MarioMindTuningSO tuning) { t = tuning; }

    public void Reset() { Value = 0f; Level = SuspicionLevel.Calm; OmenSeconds = 0f; }

    public void Set(float value) { Value = Mathf.Clamp(value, 0f, t.maxSuspicion); Evaluate(0f); }

    /// <summary>一次性事件（目击触发、受伤）。等级在下一次 Tick 结算。</summary>
    public void Add(float amount) { if (amount > 0f) Value = Mathf.Min(t.maxSuspicion, Value + amount); }

    public void Tick(float dt, float risePerSecond)
    {
        dt = Mathf.Max(0f, dt);
        if (risePerSecond > 0f) Value = Mathf.Min(t.maxSuspicion, Value + risePerSecond * dt);
        else Value = Mathf.Max(0f, Value - t.decayPerSecond * dt);
        Evaluate(dt);
    }

    private void Evaluate(float dt)
    {
        if (Value < t.curiousThreshold) { Level = SuspicionLevel.Calm; OmenSeconds = 0f; return; }
        if (Level == SuspicionLevel.Calm) { Level = SuspicionLevel.Curious; OmenSeconds = 0f; OmenCount++; }
        else OmenSeconds += dt;
        if (Level == SuspicionLevel.Curious && Value >= t.alertThreshold && OmenSeconds >= t.minOmenSeconds)
            Level = SuspicionLevel.Alert;
    }
}
