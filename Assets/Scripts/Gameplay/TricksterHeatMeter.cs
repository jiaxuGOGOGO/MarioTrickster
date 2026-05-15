using System;
using UnityEngine;

/// <summary>
/// Commit 4：Trickster 热度计 — 连锁带来风险，制造"继续贪还是撤"的选择。
///
/// 核心规则（S130 第二阶段）：
///   - 进入附身点增加少量热度。
///   - 成功操控机关增加更多热度，连锁越高增加越多。
///   - 热度随时间慢慢下降但下降很慢。
///   - 到达阈值时屏幕提示"警戒升高"，并让可疑附身点更明显。
///   - 热度越高，Mario 获得的信息越多，反制越容易。
///
/// 四档位：
///   0–30  Calm      — 无惩罚，只显示轻微状态。
///   30–60 Suspicious — 附身物体的抖动/闪光/残影变得更明显。
///   60–85 Alert      — 扫描预告、出口短暂关闭或机关冷却变长（不硬锁主路）。
///   85–100 Lockdown  — 触发一次预告式危机，之后热度回落到 60。
///
/// 接入方式：
///   - 监听 PropComboTracker.OnComboHit / OnComboBreak。
///   - 监听 TricksterPossessionGate.OnStateChanged（进入 Possessing 时加热度）。
///   - 向 MarioSuspicionTracker 传递热度信息（影响证据衰减速度）。
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
/// </summary>
public class TricksterHeatMeter : MonoBehaviour
{
    // ─────────────────────────────────────────────────────
    #region 枚举

    public enum HeatTier
    {
        Calm,       // 0–30
        Suspicious, // 30–60
        Alert,      // 60–85
        Lockdown    // 85–100
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 配置

    [Header("=== Commit 4 热度计 ===")]
    [Tooltip("进入附身点（Possessing）时增加的热度")]
    [SerializeField] private float heatPerPossession = 5f;

    [Tooltip("操控机关时增加的基础热度")]
    [SerializeField] private float heatPerActivation = 12f;

    [Tooltip("连锁倍率对热度的加成系数（热度增量 = base * (1 + comboMult * factor)）")]
    [SerializeField] private float comboHeatFactor = 0.4f;

    [Tooltip("连锁断裂时的热度惩罚（高连锁断裂给更多热度）")]
    [SerializeField] private float comboBreakHeatPerChain = 3f;

    [Tooltip("热度每秒自然衰减量")]
    [SerializeField] private float heatDecayPerSecond = 1.5f;

    [Tooltip("Lockdown 触发后热度回落到的目标值")]
    [SerializeField] private float lockdownFallbackHeat = 60f;

    [Tooltip("Lockdown 触发后的冷却时间（秒）— 防止反复触发")]
    [SerializeField] private float lockdownCooldown = 10f;

    [Tooltip("热度对 Mario 证据衰减的影响系数（热度越高衰减越慢）")]
    [SerializeField] private float heatToDecaySlowdown = 0.5f;

    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool showDebugInfo = false;

    // 档位阈值
    [Header("档位阈值")]
    [SerializeField] private float suspiciousThreshold = 30f;
    [SerializeField] private float alertThreshold = 60f;
    [SerializeField] private float lockdownThreshold = 85f;

    #endregion

    private float HeatPerPossessionValue => GameplayMetrics.HeatPerPossession(heatPerPossession);
    private float HeatPerActivationValue => GameplayMetrics.HeatPerActivation(heatPerActivation);
    private float ComboHeatFactorValue => GameplayMetrics.HeatComboHeatFactor(comboHeatFactor);
    private float ComboBreakHeatPerChainValue => GameplayMetrics.HeatComboBreakHeatPerChain(comboBreakHeatPerChain);
    private float HeatDecayPerSecondValue => GameplayMetrics.HeatDecayPerSecond(heatDecayPerSecond);
    private float LockdownFallbackHeatValue => GameplayMetrics.HeatLockdownFallbackHeat(lockdownFallbackHeat);
    private float LockdownCooldownValue => GameplayMetrics.HeatLockdownCooldown(lockdownCooldown);
    private float HeatToDecaySlowdownValue => GameplayMetrics.HeatToDecaySlowdown(heatToDecaySlowdown);
    private float SuspiciousThresholdValue => GameplayMetrics.HeatSuspiciousThreshold(suspiciousThreshold);
    private float AlertThresholdValue => GameplayMetrics.HeatAlertThreshold(alertThreshold);
    private float LockdownThresholdValue => GameplayMetrics.HeatLockdownThreshold(lockdownThreshold);

    // ─────────────────────────────────────────────────────
    #region 运行时状态

    private float heat;
    private HeatTier currentTier = HeatTier.Calm;
    private float lockdownCooldownTimer;

    // 引用
    private PropComboTracker comboTracker;
    private TricksterPossessionGate possessionGate;
    private MarioSuspicionTracker suspicionTracker;
    private RepeatInterferenceStack repeatStack;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 事件

    /// <summary>热度变化时触发（新热度值, 归一化 0-1）</summary>
    public event Action<float, float> OnHeatChanged;

    /// <summary>档位变化时触发（新档位, 旧档位）</summary>
    public event Action<HeatTier, HeatTier> OnTierChanged;

    /// <summary>Lockdown 触发时触发</summary>
    public event Action OnLockdownTriggered;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 公共属性

    public float Heat => heat;
    public float MaxHeat => 100f;
    public float HeatNormalized => heat / MaxHeat;
    public HeatTier CurrentTier => currentTier;
    public bool IsLockdownCooling => lockdownCooldownTimer > 0f;

    /// <summary>当前热度对 Mario 证据衰减的减速系数（0=无减速, 1=完全停止衰减）</summary>
    public float EvidenceDecaySlowdown => Mathf.Clamp01((heat / MaxHeat) * HeatToDecaySlowdownValue);

    #endregion

    // ─────────────────────────────────────────────────────
    #region 生命周期

    private void Start()
    {
        // 查找依赖
        comboTracker = FindObjectOfType<PropComboTracker>();
        suspicionTracker = FindObjectOfType<MarioSuspicionTracker>();
        repeatStack = FindObjectOfType<RepeatInterferenceStack>();

        var trickster = FindObjectOfType<TricksterController>();
        if (trickster != null)
        {
            possessionGate = trickster.GetComponent<TricksterPossessionGate>();
        }

        // 订阅事件
        if (comboTracker != null)
        {
            comboTracker.OnComboHit += HandleComboHit;
            comboTracker.OnComboBreak += HandleComboBreak;
        }

        if (possessionGate != null)
        {
            possessionGate.OnStateChanged += HandlePossessionStateChanged;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart += HandleRoundStart;
        }
    }

    private void OnDestroy()
    {
        if (comboTracker != null)
        {
            comboTracker.OnComboHit -= HandleComboHit;
            comboTracker.OnComboBreak -= HandleComboBreak;
        }

        if (possessionGate != null)
        {
            possessionGate.OnStateChanged -= HandlePossessionStateChanged;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart -= HandleRoundStart;
        }
    }

    private void Update()
    {
        // 热度自然衰减
        if (heat > 0f)
        {
            float decay = HeatDecayPerSecondValue * Time.deltaTime;
            SetHeat(heat - decay);
        }

        // Lockdown 冷却
        if (lockdownCooldownTimer > 0f)
        {
            lockdownCooldownTimer -= Time.deltaTime;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 公共 API

    /// <summary>外部直接增加热度（如 RepeatInterferenceStack 联动）</summary>
    public void AddHeat(float amount)
    {
        SetHeat(heat + amount);
    }

    /// <summary>外部直接减少热度（如 Counter-Reveal 奖励）</summary>
    public void ReduceHeat(float amount)
    {
        SetHeat(heat - amount);
    }

    /// <summary>获取当前档位的描述文字</summary>
    public string GetTierLabel()
    {
        switch (currentTier)
        {
            case HeatTier.Calm: return "Calm";
            case HeatTier.Suspicious: return "Suspicious";
            case HeatTier.Alert: return "Alert";
            case HeatTier.Lockdown: return "LOCKDOWN";
            default: return "Unknown";
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 事件处理

    private void HandleComboHit(int comboCount, float hitMult, bool diffAnchor, bool diffProp)
    {
        // 操控机关增加热度，连锁越高增加越多
        float comboBonus = 1f + (comboCount - 1) * ComboHeatFactorValue;
        float heatGain = HeatPerActivationValue * comboBonus;

        // 同一锚点出手额外加热（与 RepeatInterferenceStack 联动）
        if (!diffAnchor && comboCount > 1)
        {
            heatGain *= 1.3f;
        }

        AddHeat(heatGain);

        if (showDebugInfo)
        {
            Debug.Log($"[TricksterHeatMeter] ComboHit: chain={comboCount}, " +
                      $"heatGain={heatGain:F1}, total={heat:F1}, tier={currentTier}");
        }
    }

    private void HandleComboBreak(int finalCount, float finalMult)
    {
        // 高连锁断裂给额外热度（贪多了被抓的惩罚感）
        if (finalCount >= 3)
        {
            float breakHeat = ComboBreakHeatPerChainValue * finalCount;
            AddHeat(breakHeat);

            if (showDebugInfo)
            {
                Debug.Log($"[TricksterHeatMeter] ComboBreak: chain={finalCount}, " +
                          $"breakHeat={breakHeat:F1}, total={heat:F1}");
            }
        }
    }

    private void HandlePossessionStateChanged(TricksterPossessionState newState)
    {
        // 进入附身状态时增加少量热度
        if (newState == TricksterPossessionState.Possessing)
        {
            AddHeat(HeatPerPossessionValue);

            if (showDebugInfo)
            {
                Debug.Log($"[TricksterHeatMeter] Possession: +{heatPerPossession}, " +
                          $"total={heat:F1}");
            }
        }
    }

    private void HandleRoundStart()
    {
        heat = 0f;
        currentTier = HeatTier.Calm;
        lockdownCooldownTimer = 0f;
        OnHeatChanged?.Invoke(0f, 0f);
        OnTierChanged?.Invoke(HeatTier.Calm, HeatTier.Calm);
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 内部逻辑

    private void SetHeat(float newHeat)
    {
        float oldHeat = heat;
        heat = Mathf.Clamp(newHeat, 0f, MaxHeat);

        // 检查档位变化
        HeatTier newTier = CalculateTier(heat);
        if (newTier != currentTier)
        {
            HeatTier oldTier = currentTier;
            currentTier = newTier;

            if (showDebugInfo)
            {
                Debug.Log($"[TricksterHeatMeter] Tier change: {oldTier} → {newTier} (heat={heat:F1})");
            }

            OnTierChanged?.Invoke(newTier, oldTier);

            // Lockdown 触发
            if (newTier == HeatTier.Lockdown && lockdownCooldownTimer <= 0f)
            {
                TriggerLockdown();
            }
        }

        if (Mathf.Abs(oldHeat - heat) > 0.01f)
        {
            OnHeatChanged?.Invoke(heat, heat / MaxHeat);
        }
    }

    private HeatTier CalculateTier(float h)
    {
        if (h >= LockdownThresholdValue) return HeatTier.Lockdown;
        if (h >= AlertThresholdValue) return HeatTier.Alert;
        if (h >= SuspiciousThresholdValue) return HeatTier.Suspicious;
        return HeatTier.Calm;
    }

    private void TriggerLockdown()
    {
        lockdownCooldownTimer = LockdownCooldownValue;

        if (showDebugInfo)
        {
            Debug.Log($"[TricksterHeatMeter] LOCKDOWN triggered! Heat will fall to {LockdownFallbackHeatValue}");
        }

        OnLockdownTriggered?.Invoke();

        // 热度回落到目标值（延迟一帧避免事件处理冲突）
        heat = LockdownFallbackHeatValue;
        currentTier = CalculateTier(heat);
        OnHeatChanged?.Invoke(heat, heat / MaxHeat);
    }

    #endregion
}
