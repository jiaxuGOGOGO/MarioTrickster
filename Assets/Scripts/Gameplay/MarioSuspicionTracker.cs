using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Commit 1：Mario 侧可疑度追踪系统。
///
/// 职责：
///   - 为场景中每个 PossessionAnchor 维护一份 AnchorSuspicionData。
///   - 监听 TricksterAbilitySystem 的 OnPropActivated 事件，自动给出手锚点叠加
///     Suspicion 和 Residue。
///   - 监听 TricksterPossessionGate 的 OnStateChanged / OnAnchorChanged，
///     在附身/复用时叠加可疑度。
///   - 提供 API 供 SilentMarkSensor 和 MarioCounterplayProbe 读写数据。
///   - 每帧推进所有锚点的衰减。
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
///   - 通过监听 TricksterAbilitySystem 事件接入，不反向控制能力系统。
/// </summary>
public class MarioSuspicionTracker : MonoBehaviour
{
    [Header("=== Commit 1 可疑度追踪 ===")]
    [Tooltip("Trickster 出手后给锚点增加的基础可疑度")]
    [SerializeField] private float suspicionPerActivation = 25f;

    [Tooltip("Trickster 复用同一锚点时的额外可疑度倍率")]
    [SerializeField] private float reuseSuspicionMultiplier = 1.8f;

    [Tooltip("出手后残留初始强度 (0–1)")]
    [SerializeField] private float residueStrength = 0.9f;

    [Tooltip("附身（Blending→Possessing）时给锚点增加的可疑度")]
    [SerializeField] private float suspicionPerPossession = 10f;

    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool showDebugInfo = false;

    // ── 数据 ──
    private Dictionary<PossessionAnchor, AnchorSuspicionData> dataMap
        = new Dictionary<PossessionAnchor, AnchorSuspicionData>();

    // ── 引用（运行时查找）──
    private TricksterAbilitySystem abilitySystem;
    private TricksterPossessionGate possessionGate;

    // ── 事件（供 UI / Probe 订阅）──
    /// <summary>某锚点的可疑度发生变化时触发</summary>
    public event Action<PossessionAnchor, AnchorSuspicionData> OnSuspicionChanged;

    // ── Commit 4 桥接属性（由 HeatSuspicionBridge 写入）──
    /// <summary>证据衰减减速系数（0=无减速, 1=完全停止衰减），由 HeatSuspicionBridge 每帧设置</summary>
    public float DecaySlowdownFactor { get; set; }

    /// <summary>某锚点达到可揭穿阈值时触发（首次达到时只触发一次）</summary>
    public event Action<PossessionAnchor, AnchorSuspicionData> OnRevealReady;

    /// <summary>Trickster 出手后残留生成时触发</summary>
    public event Action<PossessionAnchor, float> OnResidueGenerated;

    // ─────────────────────────────────────────────────────
    #region 生命周期

    private void Start()
    {
        // 查找 Trickster 的能力系统和门禁
        var trickster = FindObjectOfType<TricksterController>();
        if (trickster != null)
        {
            abilitySystem = trickster.GetComponent<TricksterAbilitySystem>();
            possessionGate = trickster.GetComponent<TricksterPossessionGate>();
        }

        // 订阅事件
        if (abilitySystem != null)
        {
            abilitySystem.OnPropActivated += HandlePropActivated;
        }

        if (possessionGate != null)
        {
            possessionGate.OnStateChanged += HandlePossessionStateChanged;
            possessionGate.OnAnchorChanged += HandleAnchorChanged;
        }

        // 初始化场景中已有锚点
        var anchors = FindObjectsOfType<PossessionAnchor>();
        foreach (var anchor in anchors)
        {
            GetOrCreateData(anchor);
        }

        // 订阅回合重置
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart += HandleRoundStart;
        }
    }

    private void OnDestroy()
    {
        if (abilitySystem != null)
        {
            abilitySystem.OnPropActivated -= HandlePropActivated;
        }

        if (possessionGate != null)
        {
            possessionGate.OnStateChanged -= HandlePossessionStateChanged;
            possessionGate.OnAnchorChanged -= HandleAnchorChanged;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart -= HandleRoundStart;
        }
    }

    private void Update()
    {
        // 考虑热度对衰减的影响：热度越高衰减越慢
        float decayMultiplier = 1f - Mathf.Clamp01(DecaySlowdownFactor);
        float dt = Time.deltaTime * decayMultiplier;
        foreach (var kvp in dataMap)
        {
            kvp.Value.Tick(dt);
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 事件处理

    private void HandlePropActivated(IControllableProp prop)
    {
        if (prop == null) return;

        Transform propTransform = prop.GetTransform();
        if (propTransform == null) return;

        PossessionAnchor anchor = propTransform.GetComponent<PossessionAnchor>();
        if (anchor == null) return;

        AnchorSuspicionData data = GetOrCreateData(anchor);

        // 判断是否复用（5 秒内再次使用同一锚点）
        bool isReuse = (Time.time - data.LastUsedTime) < 5f && data.LastUsedTime > 0f;
        float suspicionAmount = suspicionPerActivation;
        if (isReuse)
        {
            suspicionAmount *= reuseSuspicionMultiplier;
        }

        data.AddSuspicion(suspicionAmount);
        data.MarkUsed();

        // 生成残留
        data.SetResidue(residueStrength);

        // 出手也推进证据
        data.AddEvidence(1);

        if (showDebugInfo)
        {
            Debug.Log($"[MarioSuspicionTracker] Prop activated at {anchor.AnchorId}: " +
                      $"Suspicion={data.Suspicion:F0}, Residue={data.Residue:F2}, " +
                      $"Evidence={data.EvidenceLevel}, Reuse={isReuse}");
        }

        OnSuspicionChanged?.Invoke(anchor, data);
        OnResidueGenerated?.Invoke(anchor, data.Residue);

        // 检查是否首次达到揭穿阈值
        if (data.IsRevealReady())
        {
            OnRevealReady?.Invoke(anchor, data);
        }
    }

    private void HandlePossessionStateChanged(TricksterPossessionState newState)
    {
        // 进入 Possessing 时给当前锚点加可疑度
        if (newState == TricksterPossessionState.Possessing && possessionGate != null)
        {
            PossessionAnchor anchor = possessionGate.CurrentAnchor;
            if (anchor != null)
            {
                AnchorSuspicionData data = GetOrCreateData(anchor);
                data.AddSuspicion(suspicionPerPossession);

                if (showDebugInfo)
                {
                    Debug.Log($"[MarioSuspicionTracker] Possession at {anchor.AnchorId}: Suspicion={data.Suspicion:F0}");
                }

                OnSuspicionChanged?.Invoke(anchor, data);
            }
        }
    }

    private void HandleAnchorChanged(PossessionAnchor anchor)
    {
        // 确保新锚点有数据条目
        if (anchor != null)
        {
            GetOrCreateData(anchor);
        }
    }

    private void HandleRoundStart()
    {
        // 回合重置所有数据
        foreach (var kvp in dataMap)
        {
            kvp.Value.Reset();
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 公共 API（供 SilentMarkSensor / Probe / UI 调用）

    /// <summary>获取指定锚点的数据（只读访问）</summary>
    public AnchorSuspicionData GetData(PossessionAnchor anchor)
    {
        if (anchor == null) return null;
        return dataMap.TryGetValue(anchor, out var data) ? data : null;
    }

    /// <summary>获取或创建锚点数据</summary>
    public AnchorSuspicionData GetOrCreateData(PossessionAnchor anchor)
    {
        if (anchor == null) return null;
        if (!dataMap.TryGetValue(anchor, out var data))
        {
            data = new AnchorSuspicionData(anchor);
            dataMap[anchor] = data;
        }
        return data;
    }

    /// <summary>Mario 暗中标记某锚点（由 SilentMarkSensor 调用）</summary>
    public void ApplySilentMark(PossessionAnchor anchor)
    {
        if (anchor == null) return;
        AnchorSuspicionData data = GetOrCreateData(anchor);
        data.AddSilentMark();

        if (showDebugInfo)
        {
            Debug.Log($"[MarioSuspicionTracker] SilentMark on {anchor.AnchorId}: " +
                      $"MarkCount={data.SilentMarkCount}, Evidence={data.EvidenceLevel}");
        }

        OnSuspicionChanged?.Invoke(anchor, data);

        if (data.IsRevealReady())
        {
            OnRevealReady?.Invoke(anchor, data);
        }
    }

    /// <summary>外部高代价干预：直接给锚点追加可疑度与证据，并触发 Tracker 事件。</summary>
    public void ApplySuspicionEvidencePenalty(PossessionAnchor anchor, float suspicionAmount, int evidenceLayers, string source = "")
    {
        if (anchor == null) return;

        AnchorSuspicionData data = GetOrCreateData(anchor);
        if (data == null) return;

        data.AddSuspicion(suspicionAmount);
        data.AddEvidence(evidenceLayers);
        data.MarkUsed();

        if (showDebugInfo)
        {
            Debug.Log($"[MarioSuspicionTracker] Penalty {source} at {anchor.AnchorId}: " +
                      $"Suspicion={data.Suspicion:F0}, Evidence={data.EvidenceLevel}");
        }

        OnSuspicionChanged?.Invoke(anchor, data);

        if (data.IsRevealReady())
        {
            OnRevealReady?.Invoke(anchor, data);
        }
    }

    /// <summary>获取所有有残留的锚点（供 SilentMarkSensor 的被动感知使用）</summary>
    public void GetAnchorsWithResidue(List<PossessionAnchor> results, float minResidue = 0.1f)
    {
        results.Clear();
        foreach (var kvp in dataMap)
        {
            if (kvp.Value.Residue >= minResidue && kvp.Key != null)
            {
                results.Add(kvp.Key);
            }
        }
    }

    /// <summary>获取所有达到揭穿阈值的锚点</summary>
    public void GetRevealReadyAnchors(List<PossessionAnchor> results)
    {
        results.Clear();
        foreach (var kvp in dataMap)
        {
            if (kvp.Value.IsRevealReady() && kvp.Key != null)
            {
                results.Add(kvp.Key);
            }
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 调试

    private void OnGUI()
    {
        if (!showDebugInfo) return;
        if (Camera.main == null) return;

        foreach (var kvp in dataMap)
        {
            if (kvp.Key == null) continue;
            AnchorSuspicionData data = kvp.Value;
            if (data.Suspicion < 1f && data.Residue < 0.01f && data.SilentMarkCount == 0) continue;

            Vector3 worldPos = kvp.Key.AnchorTransform.position + Vector3.up * 1.5f;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0) continue;

            float x = screenPos.x - 60f;
            float y = Screen.height - screenPos.y;

            string label = $"S:{data.Suspicion:F0} R:{data.Residue:F2} M:{data.SilentMarkCount} E:{data.EvidenceLevel}";
            GUI.color = data.IsRevealReady() ? Color.red : Color.yellow;
            GUI.Label(new Rect(x, y, 200, 20), label);
            GUI.color = Color.white;
        }
    }

    #endregion
}
