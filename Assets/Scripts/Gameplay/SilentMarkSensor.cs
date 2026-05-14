using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Commit 1：Mario 被动标记传感器 — 边跑边积累 SilentMark。
///
/// 设计原则（S130 硬约束）：
///   - Mario 的侦查不能被做成频繁停下来读条。
///   - SilentMark 是边跑边发生的轻动作或被动进度。
///   - Mario 经过可疑对象、触碰残留、短暂看向异常位置、或沿原路线继续推进时，
///     都可以增加隐藏证据。
///
/// 实现方式：
///   - 挂在 Mario GameObject 上，每帧检测附近有残留或可疑度的锚点。
///   - 当 Mario 在移动中（IsMoving）经过有残留的锚点时，自动给该锚点加 SilentMark。
///   - 每个锚点有冷却时间，避免原地刷标记。
///   - 不需要额外按键，不打断跑酷节奏。
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
/// </summary>
public class SilentMarkSensor : MonoBehaviour
{
    [Header("=== Commit 1 被动标记传感器 ===")]
    [Tooltip("被动感知半径 — Mario 经过此范围内有残留的锚点时自动标记")]
    [SerializeField] private float senseRadius = 2.5f;

    [Tooltip("同一锚点的标记冷却时间（秒）")]
    [SerializeField] private float markCooldown = 3f;

    [Tooltip("Mario 必须在移动中才能被动标记（防止原地刷）")]
    [SerializeField] private bool requireMoving = true;

    [Tooltip("最低残留强度才能被被动感知")]
    [SerializeField] private float minResidueToSense = 0.15f;

    [Tooltip("最低可疑度才能被被动感知（即使无残留）")]
    [SerializeField] private float minSuspicionToSense = 30f;

    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool showDebugInfo = false;

    // ── 引用 ──
    private MarioController marioController;
    private MarioSuspicionTracker tracker;
    private HeatSuspicionBridge heatBridge;

    // ── 冷却管理 ──
    private Dictionary<PossessionAnchor, float> lastMarkTime = new Dictionary<PossessionAnchor, float>();

    // ── 缓存 ──
    private PossessionAnchor[] sceneAnchors;
    private float nextAnchorRefreshTime;
    private const float AnchorRefreshInterval = 2f;

    // ── 公共属性 ──
    public float SenseRadius => senseRadius;

    // ── 事件 ──
    public System.Action<PossessionAnchor> OnPassiveMark;

    private void Start()
    {
        marioController = GetComponent<MarioController>();
        tracker = FindObjectOfType<MarioSuspicionTracker>();
        heatBridge = FindObjectOfType<HeatSuspicionBridge>();
        RefreshAnchors();

        // 订阅回合重置
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart += HandleRoundStart;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart -= HandleRoundStart;
        }
    }

    private void HandleRoundStart()
    {
        lastMarkTime.Clear();
    }

    private void Update()
    {
        if (tracker == null || marioController == null) return;

        // 定期刷新锚点列表（场景中可能动态生成）
        if (Time.time > nextAnchorRefreshTime)
        {
            RefreshAnchors();
            nextAnchorRefreshTime = Time.time + AnchorRefreshInterval;
        }

        // 必须在移动中
        if (requireMoving && !marioController.IsMoving) return;

        Vector2 marioPos = transform.position;
        float sqrRadius = senseRadius * senseRadius;

        for (int i = 0; i < sceneAnchors.Length; i++)
        {
            PossessionAnchor anchor = sceneAnchors[i];
            if (anchor == null) continue;

            // 距离检测
            Vector2 anchorPos = anchor.AnchorTransform.position;
            float sqrDist = (marioPos - anchorPos).sqrMagnitude;
            if (sqrDist > sqrRadius) continue;

            // 检查该锚点是否有足够的残留或可疑度
            AnchorSuspicionData data = tracker.GetData(anchor);
            if (data == null) continue;

            bool hasResidue = data.Residue >= minResidueToSense;
            bool hasSuspicion = data.Suspicion >= minSuspicionToSense;
            if (!hasResidue && !hasSuspicion) continue;

            // 冷却检测：热度越高，Mario 越容易被动读到信息，表现为同一锚点标记冷却缩短。
            float markSpeedMultiplier = heatBridge != null ? Mathf.Max(0.01f, heatBridge.CurrentMarkSpeedMultiplier) : 1f;
            float effectiveMarkCooldown = markCooldown / markSpeedMultiplier;
            if (lastMarkTime.TryGetValue(anchor, out float lastTime))
            {
                if (Time.time - lastTime < effectiveMarkCooldown) continue;
            }

            // 执行被动标记
            tracker.ApplySilentMark(anchor);
            lastMarkTime[anchor] = Time.time;

            OnPassiveMark?.Invoke(anchor);

            if (showDebugInfo)
            {
                Debug.Log($"[SilentMarkSensor] Passive mark on {anchor.AnchorId} " +
                          $"(Residue={data.Residue:F2}, Suspicion={data.Suspicion:F0}, " +
                          $"MarkSpeed×{markSpeedMultiplier:F1})");
            }
        }
    }

    private void RefreshAnchors()
    {
        sceneAnchors = FindObjectsOfType<PossessionAnchor>();
    }

    // ─────────────────────────────────────────────────────
    #region 调试可视化

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, senseRadius);
    }

    #endregion
}
