using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// InteractionLogSink — 交互判定黑匣子。
///
/// 该组件只监听 GameplayEventBus 与既有只读事件，将短空间对抗中的命中、揭示、痕迹、拢宝等判定
/// 转换为统一 InteractionLogEntry。它不反向调用玩法对象，不写入物理状态，也不参与任何碰撞判定，
/// 因而可以作为完全旁路的调试/观测层长期挂载。
/// </summary>
[DisallowMultipleComponent]
public sealed class InteractionLogSink : MonoBehaviour
{
    public const int MaxEntries = 15;

    private static readonly InteractionLogEntry[] EmptyEntries = new InteractionLogEntry[0];
    private static InteractionLogSink instance;

    private readonly Queue<InteractionLogEntry> entries = new Queue<InteractionLogEntry>(MaxEntries);
    private InteractionLogEntry[] snapshot = EmptyEntries;
    private bool subscribed;

    /// <summary>日志队列刷新时触发；参数为最近 15 条以内的快照，旧到新排序。</summary>
    public static event Action<IReadOnlyList<InteractionLogEntry>> OnLogUpdated;

    public static IReadOnlyList<InteractionLogEntry> RecentEntries => instance != null ? instance.snapshot : EmptyEntries;

    /// <summary>
    /// 供 UI 层确保场景中存在一个轻量 Sink。若设计者已手动放置，则直接复用。
    /// </summary>
    public static InteractionLogSink EnsureInstance()
    {
        if (instance != null) return instance;

        instance = FindObjectOfType<InteractionLogSink>();
        if (instance != null) return instance;

        GameObject go = new GameObject("InteractionLogSink");
        instance = go.AddComponent<InteractionLogSink>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            Unsubscribe();
            instance = null;
        }
    }

    private void Subscribe()
    {
        if (subscribed) return;

        GameplayEventBus.OnTrapTriggered += HandleTrapTriggered;
        GameplayEventBus.OnTricksterRevealed += HandleTricksterRevealed;
        GameplayEventBus.OnResidueSpotted += HandleResidueSpotted;
        LootObjective.OnLootCollected += HandleLootCollected;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;

        GameplayEventBus.OnTrapTriggered -= HandleTrapTriggered;
        GameplayEventBus.OnTricksterRevealed -= HandleTricksterRevealed;
        GameplayEventBus.OnResidueSpotted -= HandleResidueSpotted;
        LootObjective.OnLootCollected -= HandleLootCollected;
        subscribed = false;
    }

    private void HandleTrapTriggered(GameplayEventBus.TrapTriggeredPayload payload)
    {
        if (payload == null) return;

        Push(new InteractionLogEntry(
            GetTimestamp(),
            ResolveObjectId(payload.source, "UnknownSource"),
            ResolveObjectId(payload.target, "UnknownTarget"),
            InteractionType.Hit,
            InferPhase(payload.source),
            string.IsNullOrEmpty(payload.reason) ? "Hit confirmed" : payload.reason));
    }

    private void HandleTricksterRevealed(GameplayEventBus.TricksterRevealedPayload payload)
    {
        if (payload == null) return;

        GameObject tricksterObject = payload.trickster != null ? payload.trickster.gameObject : null;
        string result = string.IsNullOrEmpty(payload.reason)
            ? $"Revealed evidence={payload.evidence:F2}"
            : $"{payload.reason} evidence={payload.evidence:F2}";

        Push(new InteractionLogEntry(
            GetTimestamp(),
            ResolveObjectId(payload.source, "ScanSource"),
            ResolveObjectId(tricksterObject, "Trickster"),
            InteractionType.Reveal,
            InteractionPhase.Active,
            result));
    }

    private void HandleResidueSpotted(GameplayEventBus.ResidueSpottedPayload payload)
    {
        if (payload == null) return;

        string anchorId = payload.anchor != null ? payload.anchor.AnchorId : ResolveObjectId(payload.target, "UnknownAnchor");
        string result = string.IsNullOrEmpty(payload.reason)
            ? $"Residue spotted r={payload.residue:F2} s={payload.suspicion:F2} tier={payload.heatTier}"
            : $"{payload.reason} r={payload.residue:F2} s={payload.suspicion:F2} tier={payload.heatTier}";

        Push(new InteractionLogEntry(
            GetTimestamp(),
            anchorId,
            ResolveObjectId(payload.target, "EvidenceTarget"),
            InteractionType.Reveal,
            InteractionPhase.Windup,
            result));
    }

    private void HandleLootCollected()
    {
        Push(new InteractionLogEntry(
            GetTimestamp(),
            "LootObjective",
            "Mario",
            InteractionType.Loot,
            InteractionPhase.Active,
            "Loot collected; escape gate armed"));
    }

    private void Push(InteractionLogEntry entry)
    {
        entries.Enqueue(entry);
        while (entries.Count > MaxEntries)
        {
            entries.Dequeue();
        }

        snapshot = entries.ToArray();
        OnLogUpdated?.Invoke(snapshot);
        Debug.Log($"[InteractionLog] {entry.ToCompactString()}");
    }

    private static float GetTimestamp()
    {
        return GameManager.Instance != null ? GameManager.Instance.GameTimer : Time.time;
    }

    private static string ResolveObjectId(GameObject obj, string fallback)
    {
        if (obj == null) return fallback;

        PossessionAnchor anchor = obj.GetComponent<PossessionAnchor>();
        if (anchor != null && !string.IsNullOrEmpty(anchor.AnchorId)) return anchor.AnchorId;

        return string.IsNullOrEmpty(obj.name) ? fallback : obj.name;
    }

    private static InteractionPhase InferPhase(GameObject source)
    {
        if (source == null) return InteractionPhase.Active;

        ControllablePropBase prop = source.GetComponent<ControllablePropBase>();
        if (prop == null)
        {
            // BaseHazard 等非 Telegraph 状态机来源，本身只在实际命中时发布事件。
            return InteractionPhase.Active;
        }

        switch (prop.GetControlState())
        {
            case PropControlState.Telegraph:
                return InteractionPhase.Windup;
            case PropControlState.Active:
                return InteractionPhase.Active;
            case PropControlState.Recovery:
            case PropControlState.Cooldown:
            case PropControlState.Exhausted:
                return InteractionPhase.Recovery;
            default:
                return InteractionPhase.Active;
        }
    }
}

public enum InteractionType
{
    Hit,
    Reveal,
    Loot
}

public enum InteractionPhase
{
    Windup,
    Active,
    Recovery
}

[Serializable]
public struct InteractionLogEntry
{
    public float Timestamp;
    public string SourceId;
    public string TargetId;
    public InteractionType InteractionType;
    public InteractionPhase Phase;
    public string Result;

    public InteractionLogEntry(float timestamp, string sourceId, string targetId, InteractionType interactionType, InteractionPhase phase, string result)
    {
        Timestamp = timestamp;
        SourceId = string.IsNullOrEmpty(sourceId) ? "UnknownSource" : sourceId;
        TargetId = string.IsNullOrEmpty(targetId) ? "UnknownTarget" : targetId;
        InteractionType = interactionType;
        Phase = phase;
        Result = string.IsNullOrEmpty(result) ? "Observed" : result;
    }

    public string ToCompactString()
    {
        return $"[{Timestamp:F1}s] {InteractionType} {Phase} Source={SourceId} Target={TargetId} Result={Result}";
    }

    public override string ToString()
    {
        return ToCompactString();
    }
}
