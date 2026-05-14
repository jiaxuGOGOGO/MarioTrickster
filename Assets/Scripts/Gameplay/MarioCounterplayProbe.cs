using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Commit 1：Mario 反制探测系统 — 包裹现有 ScanAbility，在证据足够时升级为强扫描。
///
/// 设计原则：
///   - 主动 Probe 只负责在证据足够时揭穿，不负责让玩家反复停步找答案。
///   - 当 Mario 对某锚点有足够证据（EvidenceLevel >= 2 或 Suspicion >= RevealThreshold），
///     下一次 Scan 变为"强扫描"：半径更大、冷却更短、命中高证据锚点时直接触发 Revealed。
///   - 即使没有足够证据，普通 Scan 仍然正常工作（保持现有 ScanAbility 行为不变）。
///
/// 接入方式：
///   - 挂在 Mario GameObject 上，订阅 ScanAbility.OnScanActivated 事件。
///   - 在 Scan 触发时检查范围内是否有高证据锚点，如有则触发 Counter-Reveal。
///   - Counter-Reveal 通过 TricksterPossessionGate 的公开状态判断 Trickster 是否在该锚点。
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
///   - 不改 ScanAbility 的内部逻辑，只在其事件上叠加反制判定。
/// </summary>
public class MarioCounterplayProbe : MonoBehaviour
{
    [Header("=== Commit 1 反制探测 ===")]
    [Tooltip("强扫描额外半径加成")]
    [SerializeField] private float strongScanBonusRadius = 2f;

    [Tooltip("Counter-Reveal 成功后给 Trickster 的暴露延长时间（叠加到门禁的 revealDuration）")]
    [SerializeField] private float counterRevealBonusDuration = 1.5f;

    [Tooltip("Counter-Reveal 成功后 Mario 获得的短暂保护窗口（秒）")]
    [SerializeField] private float protectedWindowDuration = 2f;

    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool showDebugInfo = false;

    // ── 引用 ──
    private ScanAbility scanAbility;
    private MarioSuspicionTracker tracker;
    private TricksterPossessionGate tricksterGate;

    // ── 状态 ──
    private bool isStrongScanReady;
    private float protectedWindowTimer;

    // ── 缓存 ──
    private List<PossessionAnchor> revealReadyAnchors = new List<PossessionAnchor>();

    // ── 公共属性 ──
    public bool IsStrongScanReady => isStrongScanReady;
    public bool IsProtected => protectedWindowTimer > 0f;
    public float ProtectedTimeRemaining => protectedWindowTimer;

    // ── 事件 ──
    /// <summary>Counter-Reveal 成功时触发（锚点, 数据）</summary>
    public System.Action<PossessionAnchor, AnchorSuspicionData> OnCounterReveal;

    /// <summary>保护窗口开始时触发</summary>
    public System.Action<float> OnProtectedWindowStart;

    private void Start()
    {
        scanAbility = GetComponent<ScanAbility>();
        tracker = FindObjectOfType<MarioSuspicionTracker>();

        var trickster = FindObjectOfType<TricksterController>();
        if (trickster != null)
        {
            tricksterGate = trickster.GetComponent<TricksterPossessionGate>();
        }

        // 订阅扫描事件
        if (scanAbility != null)
        {
            scanAbility.OnScanActivated += HandleScanActivated;
        }

        // 订阅 Tracker 的揭穿就绪事件
        if (tracker != null)
        {
            tracker.OnRevealReady += HandleRevealReady;
        }
    }

    private void OnDestroy()
    {
        if (scanAbility != null)
        {
            scanAbility.OnScanActivated -= HandleScanActivated;
        }

        if (tracker != null)
        {
            tracker.OnRevealReady -= HandleRevealReady;
        }
    }

    private void Update()
    {
        // 保护窗口倒计时
        if (protectedWindowTimer > 0f)
        {
            protectedWindowTimer -= Time.deltaTime;
        }

        // 更新强扫描就绪状态
        UpdateStrongScanReadiness();
    }

    // ─────────────────────────────────────────────────────
    #region 核心逻辑

    private void HandleScanActivated()
    {
        if (tracker == null) return;

        // 获取扫描范围内达到揭穿阈值的锚点
        float effectiveRadius = scanAbility != null ? scanAbility.ScanRadius : 5f;
        if (isStrongScanReady)
        {
            effectiveRadius += strongScanBonusRadius;
        }

        Vector2 marioPos = transform.position;
        tracker.GetRevealReadyAnchors(revealReadyAnchors);

        PossessionAnchor bestTarget = null;
        float bestDistance = float.MaxValue;

        foreach (var anchor in revealReadyAnchors)
        {
            if (anchor == null) continue;

            float dist = Vector2.Distance(marioPos, (Vector2)anchor.AnchorTransform.position);
            if (dist > effectiveRadius) continue;

            if (dist < bestDistance)
            {
                bestDistance = dist;
                bestTarget = anchor;
            }
        }

        if (bestTarget == null) return;

        // 检查 Trickster 是否正在该锚点附身
        bool tricksterAtAnchor = false;
        if (tricksterGate != null && tricksterGate.CurrentAnchor == bestTarget)
        {
            var state = tricksterGate.CurrentState;
            tricksterAtAnchor = state == TricksterPossessionState.Blending ||
                                state == TricksterPossessionState.Possessing;
        }

        AnchorSuspicionData data = tracker.GetData(bestTarget);

        if (tricksterAtAnchor)
        {
            // Counter-Reveal 成功！
            ExecuteCounterReveal(bestTarget, data);
        }
        else
        {
            // Trickster 不在该锚点，但证据足够 → 给 Mario 部分信息奖励
            // 清除该锚点的标记和部分可疑度（表示"查过了，这里现在安全"）
            if (data != null)
            {
                data.AddSuspicion(-15f);
            }

            if (showDebugInfo)
            {
                Debug.Log($"[MarioCounterplayProbe] Probe at {bestTarget.AnchorId}: " +
                          $"Trickster not here. Suspicion reduced.");
            }
        }
    }

    private void ExecuteCounterReveal(PossessionAnchor anchor, AnchorSuspicionData data)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[MarioCounterplayProbe] COUNTER-REVEAL at {anchor.AnchorId}! " +
                      $"Trickster exposed!");
        }

        // 启动保护窗口
        protectedWindowTimer = protectedWindowDuration;
        OnProtectedWindowStart?.Invoke(protectedWindowDuration);

        // 触发事件
        OnCounterReveal?.Invoke(anchor, data);

        // 注意：实际的 Revealed 状态转换由 TricksterPossessionGate 自己在
        // HandlePropActivated 中处理，或者由 ScanAbility 的 reveal 效果触发。
        // 这里不直接修改门禁状态，而是通过事件通知其他系统。
        // 后续 Commit 可以在这里加入更强的暴露效果。
    }

    private void HandleRevealReady(PossessionAnchor anchor, AnchorSuspicionData data)
    {
        isStrongScanReady = true;

        if (showDebugInfo)
        {
            Debug.Log($"[MarioCounterplayProbe] Strong scan ready! " +
                      $"Anchor {anchor.AnchorId} has enough evidence.");
        }
    }

    private void UpdateStrongScanReadiness()
    {
        if (tracker == null)
        {
            isStrongScanReady = false;
            return;
        }

        tracker.GetRevealReadyAnchors(revealReadyAnchors);
        isStrongScanReady = revealReadyAnchors.Count > 0;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 调试 UI

    private void OnGUI()
    {
        if (!showDebugInfo) return;
        if (Camera.main == null) return;

        // 在 Mario 头顶显示 Probe 状态
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.5f);
        if (screenPos.z < 0) return;

        float x = screenPos.x - 60f;
        float y = Screen.height - screenPos.y;

        if (IsProtected)
        {
            GUI.color = Color.green;
            GUI.Label(new Rect(x, y, 150, 20), $"PROTECTED {protectedWindowTimer:F1}s");
        }
        else if (isStrongScanReady)
        {
            GUI.color = Color.cyan;
            GUI.Label(new Rect(x, y, 150, 20), "STRONG SCAN READY");
        }

        GUI.color = Color.white;
    }

    #endregion
}
