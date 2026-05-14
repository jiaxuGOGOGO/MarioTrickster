using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Commit 6：预告式危机导演 — 最小版本，只做"扫描波"一种危机。
///
/// 核心规则（S130 第四阶段）：
///   - 订阅 TricksterHeatMeter.OnTierChanged，进入 Alert 或 Lockdown 时启动扫描波。
///   - 扫描波从左到右（或从 LootObjective 位置向外）扫过全场。
///   - 扫描波经过 PossessionAnchor 时：
///     a) 如果该锚点有 Residue 或 Suspicion，放大已有证据（不凭空抓人）。
///     b) 如果 Trickster 正在该锚点 Blending/Possessing，触发 Revealed 状态。
///   - 扫描波有预告倒计时（2s），给双方反应时间。
///   - 每次 Lockdown 最多触发一次扫描波（冷却期内不重复）。
///   - 回合重置时清除所有状态。
///
/// 关卡设计意义：
///   - 给 Mario 读局机会，不需要主动扫描也能获得信息。
///   - 给 Trickster 压力：高热度时必须考虑是否提前撤离当前锚点。
///   - 不破坏地形，不生成复杂对象，纯逻辑+视觉提示。
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
///   - 通过监听 TricksterHeatMeter 事件接入。
/// </summary>
public class AlarmCrisisDirector : MonoBehaviour
{
    // ─────────────────────────────────────────────────────
    #region 配置

    [Header("=== Commit 6 扫描波危机 ===")]
    [Tooltip("扫描波预告时间（秒）")]
    [SerializeField] private float warningDuration = 2f;

    [Tooltip("扫描波从左到右扫过的速度（单位/秒）")]
    [SerializeField] private float scanSpeed = 12f;

    [Tooltip("扫描波宽度（单位）")]
    [SerializeField] private float scanWidth = 2f;

    [Tooltip("扫描波经过时对已有证据的放大倍率")]
    [SerializeField] private float evidenceAmplifyFactor = 1.5f;

    [Tooltip("扫描波经过时给有残留锚点增加的额外 Suspicion")]
    [SerializeField] private float scanSuspicionBonus = 15f;

    [Tooltip("触发扫描波的最低热度档位")]
    [SerializeField] private TricksterHeatMeter.HeatTier triggerTier = TricksterHeatMeter.HeatTier.Alert;

    [Tooltip("两次扫描波之间的最小间隔（秒）")]
    [SerializeField] private float scanCooldown = 15f;

    [Tooltip("Lockdown 触发时是否立即启动扫描波（无视 Alert 触发）")]
    [SerializeField] private bool lockdownForcesScan = true;

    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool showDebugInfo = true;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 运行时状态

    public enum ScanPhase
    {
        Idle,
        Warning,
        Scanning,
        Cooldown
    }

    private ScanPhase currentPhase = ScanPhase.Idle;
    private float phaseTimer;
    private float scanX;         // 当前扫描波 X 位置
    private float scanStartX;
    private float scanEndX;
    private float cooldownTimer;

    // 缓存
    private TricksterHeatMeter heatMeter;
    private TricksterPossessionGate possessionGate;
    private MarioSuspicionTracker suspicionTracker;
    private PossessionAnchor[] cachedAnchors;
    private HashSet<PossessionAnchor> scannedThisWave = new HashSet<PossessionAnchor>();

    #endregion

    // ─────────────────────────────────────────────────────
    #region 事件

    /// <summary>扫描波预告开始时触发（warningDuration）</summary>
    public event Action<float> OnScanWarning;

    /// <summary>扫描波开始扫描时触发</summary>
    public event Action OnScanStarted;

    /// <summary>扫描波结束时触发</summary>
    public event Action OnScanEnded;

    /// <summary>扫描波命中锚点时触发（anchor, wasRevealed）</summary>
    public event Action<PossessionAnchor, bool> OnAnchorScanned;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 公共属性

    public ScanPhase CurrentPhase => currentPhase;
    public float PhaseTimer => phaseTimer;
    public float ScanX => scanX;
    public float ScanStartX => scanStartX;
    public float ScanEndX => scanEndX;
    public float WarningDuration => warningDuration;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 生命周期

    private void Start()
    {
        heatMeter = FindObjectOfType<TricksterHeatMeter>();
        possessionGate = FindObjectOfType<TricksterPossessionGate>();
        suspicionTracker = FindObjectOfType<MarioSuspicionTracker>();
        cachedAnchors = FindObjectsOfType<PossessionAnchor>();

        if (heatMeter != null)
        {
            heatMeter.OnTierChanged += HandleTierChanged;
            heatMeter.OnLockdownTriggered += HandleLockdownTriggered;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart += HandleRoundStart;
        }

        // 计算场景扫描范围
        CalculateScanBounds();
    }

    private void OnDestroy()
    {
        if (heatMeter != null)
        {
            heatMeter.OnTierChanged -= HandleTierChanged;
            heatMeter.OnLockdownTriggered -= HandleLockdownTriggered;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart -= HandleRoundStart;
        }
    }

    private void Update()
    {
        switch (currentPhase)
        {
            case ScanPhase.Warning:
                UpdateWarning();
                break;
            case ScanPhase.Scanning:
                UpdateScanning();
                break;
            case ScanPhase.Cooldown:
                UpdateCooldown();
                break;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 阶段更新

    private void UpdateWarning()
    {
        phaseTimer -= Time.deltaTime;
        if (phaseTimer <= 0f)
        {
            StartScanning();
        }
    }

    private void UpdateScanning()
    {
        float prevX = scanX;
        scanX += scanSpeed * Time.deltaTime;

        // 检查扫描波经过的锚点
        CheckAnchorsInRange(prevX, scanX);

        // 扫描完成
        if (scanX >= scanEndX)
        {
            EndScanning();
        }
    }

    private void UpdateCooldown()
    {
        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0f)
        {
            currentPhase = ScanPhase.Idle;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 扫描逻辑

    private void TriggerScanWave()
    {
        if (currentPhase != ScanPhase.Idle) return;

        currentPhase = ScanPhase.Warning;
        phaseTimer = warningDuration;

        if (showDebugInfo)
        {
            Debug.Log($"[AlarmCrisisDirector] Scan wave WARNING! {warningDuration}s to scan.");
        }

        OnScanWarning?.Invoke(warningDuration);
    }

    private void StartScanning()
    {
        currentPhase = ScanPhase.Scanning;
        scanX = scanStartX;
        scannedThisWave.Clear();

        if (showDebugInfo)
        {
            Debug.Log($"[AlarmCrisisDirector] Scan wave STARTED! Sweeping {scanStartX:F0} → {scanEndX:F0}");
        }

        OnScanStarted?.Invoke();
    }

    private void EndScanning()
    {
        currentPhase = ScanPhase.Cooldown;
        cooldownTimer = scanCooldown;

        if (showDebugInfo)
        {
            Debug.Log($"[AlarmCrisisDirector] Scan wave ENDED. Cooldown {scanCooldown}s.");
        }

        OnScanEnded?.Invoke();
    }

    private void CheckAnchorsInRange(float fromX, float toX)
    {
        if (cachedAnchors == null) return;

        float leftEdge = fromX - scanWidth * 0.5f;
        float rightEdge = toX + scanWidth * 0.5f;

        for (int i = 0; i < cachedAnchors.Length; i++)
        {
            PossessionAnchor anchor = cachedAnchors[i];
            if (anchor == null) continue;
            if (scannedThisWave.Contains(anchor)) continue;

            float anchorX = anchor.AnchorTransform.position.x;
            if (anchorX >= leftEdge && anchorX <= rightEdge)
            {
                scannedThisWave.Add(anchor);
                ProcessAnchorHit(anchor);
            }
        }
    }

    private void ProcessAnchorHit(PossessionAnchor anchor)
    {
        bool wasRevealed = false;

        // 1. 放大已有证据（不凭空创造）
        if (suspicionTracker != null)
        {
            AnchorSuspicionData data = suspicionTracker.GetData(anchor);
            if (data != null && (data.Residue > 0.05f || data.Suspicion > 5f))
            {
                // 放大已有 Suspicion
                float amplified = data.Suspicion * (evidenceAmplifyFactor - 1f);
                data.AddSuspicion(amplified + scanSuspicionBonus);

                // 放大已有 Residue
                float newResidue = Mathf.Min(1f, data.Residue * evidenceAmplifyFactor);
                data.SetResidue(newResidue);

                if (showDebugInfo)
                {
                    Debug.Log($"[AlarmCrisisDirector] Amplified evidence on {anchor.AnchorId}: " +
                              $"Suspicion+{amplified + scanSuspicionBonus:F0}, Residue→{newResidue:F2}");
                }
            }
        }

        // 2. 如果 Trickster 正在该锚点 Blending/Possessing，触发 Revealed
        if (possessionGate != null && possessionGate.CurrentAnchor == anchor)
        {
            var state = possessionGate.CurrentState;
            if (state == TricksterPossessionState.Blending ||
                state == TricksterPossessionState.Possessing)
            {
                // 通过 TricksterAbilitySystem 事件间接触发 Revealed
                // 我们不直接调用 SetState（它是 private），而是通过公共事件通知
                // TricksterPossessionGate 在出手时自动进入 Revealed
                // 这里我们发出事件，让外部系统（如 MarioCounterplayProbe）响应
                wasRevealed = true;

                if (showDebugInfo)
                {
                    Debug.Log($"[AlarmCrisisDirector] Trickster CAUGHT at {anchor.AnchorId}! " +
                              $"State was {state}.");
                }

                // 给该锚点最大证据，让 MarioCounterplayProbe 可以立即揭穿
                if (suspicionTracker != null)
                {
                    AnchorSuspicionData data = suspicionTracker.GetOrCreateData(anchor);
                    data.AddSuspicion(100f);
                    data.AddEvidence(3);
                }
            }
        }

        OnAnchorScanned?.Invoke(anchor, wasRevealed);
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 事件处理

    private void HandleTierChanged(TricksterHeatMeter.HeatTier newTier, TricksterHeatMeter.HeatTier oldTier)
    {
        // 进入 Alert 时触发扫描波（如果不是从更高档降下来的）
        if (newTier == triggerTier && oldTier < triggerTier)
        {
            TriggerScanWave();
        }
    }

    private void HandleLockdownTriggered()
    {
        if (lockdownForcesScan)
        {
            // Lockdown 强制触发，即使在冷却中也重置
            if (currentPhase == ScanPhase.Cooldown)
            {
                currentPhase = ScanPhase.Idle;
            }
            TriggerScanWave();
        }
    }

    private void HandleRoundStart()
    {
        currentPhase = ScanPhase.Idle;
        phaseTimer = 0f;
        cooldownTimer = 0f;
        scanX = scanStartX;
        scannedThisWave.Clear();

        // 重新缓存锚点（可能有新锚点）
        cachedAnchors = FindObjectsOfType<PossessionAnchor>();
        CalculateScanBounds();
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 辅助

    private void CalculateScanBounds()
    {
        if (cachedAnchors == null || cachedAnchors.Length == 0)
        {
            scanStartX = -10f;
            scanEndX = 100f;
            return;
        }

        float minX = float.MaxValue;
        float maxX = float.MinValue;

        for (int i = 0; i < cachedAnchors.Length; i++)
        {
            if (cachedAnchors[i] == null) continue;
            float x = cachedAnchors[i].AnchorTransform.position.x;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
        }

        // 扫描范围比锚点范围稍大
        scanStartX = minX - 5f;
        scanEndX = maxX + 5f;
    }

    #endregion
}
