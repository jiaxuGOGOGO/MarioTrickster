using UnityEngine;

/// <summary>
/// Commit 1：每个 PossessionAnchor 的可疑度、残留和标记数据。
///
/// 不挂载为 MonoBehaviour，而是作为纯数据容器由 MarioSuspicionTracker 统一管理。
/// 这样避免在每个锚点上都加组件，也避免改动 PossessionAnchor 本体。
///
/// 数据含义：
///   Suspicion (0–100)   — 该锚点的可疑度，Trickster 附身/出手/复用都会增加。
///   Residue (0–1)       — 出手后残留强度，随时间衰减。Mario 经过时可被动感知。
///   SilentMarkCount     — Mario 暗中标记次数（边跑边积累）。
///   EvidenceLevel (0–3) — 证据层数，两次同点异常后升到 2 可触发强扫描。
///   LastUsedTime        — 上次被 Trickster 使用的时间戳。
/// </summary>
[System.Serializable]
public class AnchorSuspicionData
{
    public PossessionAnchor Anchor { get; private set; }

    // ── 可疑度 ──
    public float Suspicion { get; private set; }
    public const float MaxSuspicion = 100f;

    // ── 残留 ──
    public float Residue { get; private set; }
    public float ResidueDecayRate { get; set; } = 0.3f; // 每秒衰减量

    // ── 暗中标记 ──
    public int SilentMarkCount { get; private set; }
    public bool IsSilentlyMarked => SilentMarkCount > 0;

    // ── 证据层 ──
    public int EvidenceLevel { get; private set; }
    public const int MaxEvidence = 3;

    // ── 时间戳 ──
    public float LastUsedTime { get; private set; } = -999f;
    public float LastMarkedTime { get; private set; } = -999f;

    // ── 阈值 ──
    public const float RevealThreshold = 70f; // 可疑度达到此值时 Probe 变为强扫描

    public AnchorSuspicionData(PossessionAnchor anchor)
    {
        Anchor = anchor;
    }

    // ─────────────────────────────────────────────────────
    #region 修改方法

    /// <summary>增加可疑度（由 Trickster 附身/出手/复用触发）</summary>
    public void AddSuspicion(float amount)
    {
        Suspicion = Mathf.Clamp(Suspicion + amount, 0f, MaxSuspicion);
    }

    /// <summary>设置残留强度（出手后由 Tracker 设置）</summary>
    public void SetResidue(float strength)
    {
        Residue = Mathf.Clamp01(strength);
    }

    /// <summary>增加暗中标记（Mario 边跑边积累）</summary>
    public void AddSilentMark()
    {
        SilentMarkCount++;
        LastMarkedTime = Time.time;

        // 每次标记也推进证据
        if (Suspicion > 20f || Residue > 0.1f)
        {
            AddEvidence(1);
        }
    }

    /// <summary>增加证据层数</summary>
    public void AddEvidence(int layers)
    {
        EvidenceLevel = Mathf.Min(EvidenceLevel + layers, MaxEvidence);
    }

    /// <summary>记录使用时间戳</summary>
    public void MarkUsed()
    {
        LastUsedTime = Time.time;
    }

    /// <summary>每帧衰减（由 Tracker 的 Update 调用）</summary>
    public void Tick(float deltaTime)
    {
        // 残留衰减
        if (Residue > 0f)
        {
            Residue = Mathf.Max(0f, Residue - ResidueDecayRate * deltaTime);
        }

        // 可疑度缓慢自然衰减（非常慢，鼓励 Trickster 换点）
        if (Suspicion > 0f && Time.time - LastUsedTime > 5f)
        {
            Suspicion = Mathf.Max(0f, Suspicion - 1f * deltaTime);
        }
    }

    /// <summary>回合重置</summary>
    public void Reset()
    {
        Suspicion = 0f;
        Residue = 0f;
        SilentMarkCount = 0;
        EvidenceLevel = 0;
        LastUsedTime = -999f;
        LastMarkedTime = -999f;
    }

    /// <summary>是否达到可揭穿阈值</summary>
    public bool IsRevealReady()
    {
        return Suspicion >= RevealThreshold || EvidenceLevel >= 2;
    }

    #endregion
}
