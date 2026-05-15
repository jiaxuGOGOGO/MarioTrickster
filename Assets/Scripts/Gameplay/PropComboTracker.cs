using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Commit 3：机关操控连锁追踪器。
///
/// 核心规则（S130 第一阶段）：
///   - 2.5 秒内连续操控不同机关/不同附身点 → 连锁数 +1。
///   - 从不同附身点出手比重复同一附身点收益更高（倍率更大）。
///   - 重复同一机关也算连锁但收益更低，同时给该附身点快速叠加 Suspicion。
///   - 连锁窗口到期或 Trickster 被揭穿时连锁断裂。
///
/// 接入方式：
///   - 监听 TricksterAbilitySystem.OnPropActivated 事件。
///   - 读取 TricksterPossessionGate.CurrentAnchor 判断出手锚点。
///   - 向 MarioSuspicionTracker 叠加重复同点的额外 Suspicion。
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
/// </summary>
public class PropComboTracker : MonoBehaviour
{
    // ─────────────────────────────────────────────────────
    #region 配置

    [Header("=== Commit 3 连锁系统 ===")]
    [Tooltip("连锁窗口（秒）— 在此时间内再次出手才能续连锁")]
    [SerializeField] private float comboWindow = 2.5f;

    [Tooltip("不同附身点出手的倍率加成")]
    [SerializeField] private float differentAnchorMultiplier = 1.5f;

    [Tooltip("不同机关类型出手的倍率加成")]
    [SerializeField] private float differentPropTypeMultiplier = 1.3f;

    [Tooltip("重复同一附身点出手的倍率（低于 1.0 表示收益递减）")]
    [SerializeField] private float sameAnchorMultiplier = 0.7f;

    [Tooltip("重复同一机关的倍率")]
    [SerializeField] private float samePropMultiplier = 0.5f;

    [Tooltip("重复同一附身点时额外叠加的 Suspicion")]
    [SerializeField] private float sameAnchorSuspicionBonus = 15f;

    [Tooltip("连锁断裂后的冷却时间（秒）— 防止断了立刻续")]
    [SerializeField] private float breakCooldown = 1f;

    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool showDebugInfo = false;

    #endregion

    private float ComboWindowValue => GameplayMetrics.ComboWindow(comboWindow);
    private float DifferentAnchorMultiplierValue => GameplayMetrics.ComboDifferentAnchorMultiplier(differentAnchorMultiplier);
    private float DifferentPropTypeMultiplierValue => GameplayMetrics.ComboDifferentPropTypeMultiplier(differentPropTypeMultiplier);
    private float SameAnchorMultiplierValue => GameplayMetrics.ComboSameAnchorMultiplier(sameAnchorMultiplier);
    private float SamePropMultiplierValue => GameplayMetrics.ComboSamePropMultiplier(samePropMultiplier);
    private float SameAnchorSuspicionBonusValue => GameplayMetrics.ComboSameAnchorSuspicionBonus(sameAnchorSuspicionBonus);
    private float BreakCooldownValue => GameplayMetrics.ComboBreakCooldown(breakCooldown);

    // ─────────────────────────────────────────────────────
    #region 运行时状态

    private TricksterAbilitySystem abilitySystem;
    private TricksterPossessionGate possessionGate;
    private MarioSuspicionTracker suspicionTracker;

    // 连锁状态
    private int comboCount;
    private float comboTimer;          // 剩余连锁窗口
    private float breakCooldownTimer;  // 断裂冷却
    private float currentMultiplier;   // 当前连锁倍率

    // 上一次出手记录
    private string lastAnchorId;
    private string lastPropName;

    // 连锁历史（用于 HUD 显示和统计）
    private List<ComboEntry> comboHistory = new List<ComboEntry>();

    #endregion

    // ─────────────────────────────────────────────────────
    #region 数据结构

    [System.Serializable]
    public struct ComboEntry
    {
        public string AnchorId;
        public string PropName;
        public float Multiplier;
        public float Time;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 事件

    /// <summary>连锁数变化时触发（新连锁数, 当前倍率）</summary>
    public event Action<int, float> OnComboChanged;

    /// <summary>连锁断裂时触发（最终连锁数, 最终倍率）</summary>
    public event Action<int, float> OnComboBreak;

    /// <summary>单次出手的连锁倍率确定时触发（连锁数, 本次倍率, 是否不同锚点, 是否不同机关）</summary>
    public event Action<int, float, bool, bool> OnComboHit;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 公共属性

    public int ComboCount => comboCount;
    public float CurrentMultiplier => currentMultiplier;
    public float ComboTimeRemaining => comboTimer;
    public float ComboWindowMax => ComboWindowValue;
    public bool IsComboActive => comboCount > 0 && comboTimer > 0f;
    public IReadOnlyList<ComboEntry> ComboHistory => comboHistory;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 生命周期

    private void Start()
    {
        var trickster = FindObjectOfType<TricksterController>();
        if (trickster != null)
        {
            abilitySystem = trickster.GetComponent<TricksterAbilitySystem>();
            possessionGate = trickster.GetComponent<TricksterPossessionGate>();
        }

        suspicionTracker = FindObjectOfType<MarioSuspicionTracker>();

        if (abilitySystem != null)
        {
            abilitySystem.OnPropActivated += HandlePropActivated;
        }

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

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart -= HandleRoundStart;
        }
    }

    private void Update()
    {
        // 连锁窗口倒计时
        if (comboTimer > 0f)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f)
            {
                BreakCombo("timeout");
            }
        }

        // 断裂冷却倒计时
        if (breakCooldownTimer > 0f)
        {
            breakCooldownTimer -= Time.deltaTime;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 公共 API

    /// <summary>外部强制断裂连锁（如被揭穿时）</summary>
    public void ForceBreakCombo(string reason = "external")
    {
        if (comboCount > 0)
        {
            BreakCombo(reason);
        }
    }

    /// <summary>获取指定连锁数下的累计倍率</summary>
    public float GetCumulativeMultiplier()
    {
        return currentMultiplier;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 事件处理

    private void HandlePropActivated(IControllableProp prop)
    {
        if (prop == null) return;

        // 断裂冷却中不计入连锁
        if (breakCooldownTimer > 0f)
        {
            if (showDebugInfo)
            {
                Debug.Log("[PropComboTracker] In break cooldown, ignoring.");
            }
            return;
        }

        // 获取当前锚点和机关名
        string currentAnchorId = "";
        if (possessionGate != null && possessionGate.CurrentAnchor != null)
        {
            currentAnchorId = possessionGate.CurrentAnchor.AnchorId;
        }

        string currentPropName = prop.PropName;

        // 判断是否在连锁窗口内
        bool isChaining = comboTimer > 0f && comboCount > 0;

        // 判断是否不同锚点/不同机关
        bool isDifferentAnchor = !string.IsNullOrEmpty(lastAnchorId) &&
                                 currentAnchorId != lastAnchorId;
        bool isDifferentProp = !string.IsNullOrEmpty(lastPropName) &&
                               currentPropName != lastPropName;

        // 计算本次倍率
        float hitMultiplier = 1f;

        if (isChaining)
        {
            // 续连锁
            comboCount++;

            if (isDifferentAnchor)
            {
                hitMultiplier = DifferentAnchorMultiplierValue;
            }
            else
            {
                hitMultiplier = SameAnchorMultiplierValue;

                // 重复同一附身点：额外叠加 Suspicion
                ApplySameAnchorPenalty(currentAnchorId);
            }

            if (isDifferentProp)
            {
                hitMultiplier *= DifferentPropTypeMultiplierValue;
            }
            else
            {
                hitMultiplier *= SamePropMultiplierValue;
            }
        }
        else
        {
            // 新连锁起始
            comboCount = 1;
            hitMultiplier = 1f;
        }

        // 累计倍率
        currentMultiplier = CalculateCumulativeMultiplier(hitMultiplier);

        // 重置连锁窗口
        comboTimer = ComboWindowValue;

        // 记录本次出手
        lastAnchorId = currentAnchorId;
        lastPropName = currentPropName;

        var entry = new ComboEntry
        {
            AnchorId = currentAnchorId,
            PropName = currentPropName,
            Multiplier = hitMultiplier,
            Time = Time.time
        };
        comboHistory.Add(entry);

        if (showDebugInfo)
        {
            Debug.Log($"[PropComboTracker] Chain x{comboCount}: " +
                      $"anchor={currentAnchorId}, prop={currentPropName}, " +
                      $"hitMult={hitMultiplier:F2}, cumMult={currentMultiplier:F2}, " +
                      $"diffAnchor={isDifferentAnchor}, diffProp={isDifferentProp}");
        }

        // 触发事件
        OnComboHit?.Invoke(comboCount, hitMultiplier, isDifferentAnchor, isDifferentProp);
        OnComboChanged?.Invoke(comboCount, currentMultiplier);
    }

    private void HandleRoundStart()
    {
        comboCount = 0;
        comboTimer = 0f;
        breakCooldownTimer = 0f;
        currentMultiplier = 1f;
        lastAnchorId = "";
        lastPropName = "";
        comboHistory.Clear();
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 内部逻辑

    private void BreakCombo(string reason)
    {
        if (comboCount <= 0) return;

        int finalCount = comboCount;
        float finalMult = currentMultiplier;

        if (showDebugInfo)
        {
            Debug.Log($"[PropComboTracker] Combo BREAK ({reason}): " +
                      $"finalChain={finalCount}, finalMult={finalMult:F2}");
        }

        OnComboBreak?.Invoke(finalCount, finalMult);

        // 重置
        comboCount = 0;
        comboTimer = 0f;
        currentMultiplier = 1f;
        breakCooldownTimer = BreakCooldownValue;
        lastAnchorId = "";
        lastPropName = "";
        comboHistory.Clear();
    }

    private float CalculateCumulativeMultiplier(float latestHitMultiplier)
    {
        // 累计倍率 = 基础 1.0 + 每次连锁的增量叠加
        // 简化公式：每次连锁贡献 (hitMultiplier - 1.0) * 0.5 的增量
        // 保证连锁越长倍率越高，但增速递减
        if (comboCount <= 1) return 1f;

        float bonus = 0f;
        for (int i = 0; i < comboHistory.Count; i++)
        {
            float contribution = (comboHistory[i].Multiplier - 0.5f) * 0.3f;
            bonus += Mathf.Max(0f, contribution);
        }

        return 1f + bonus;
    }

    private void ApplySameAnchorPenalty(string anchorId)
    {
        if (suspicionTracker == null) return;
        if (possessionGate == null || possessionGate.CurrentAnchor == null) return;

        AnchorSuspicionData data = suspicionTracker.GetOrCreateData(possessionGate.CurrentAnchor);
        data.AddSuspicion(SameAnchorSuspicionBonusValue);

        if (showDebugInfo)
        {
            Debug.Log($"[PropComboTracker] Same anchor penalty: +{sameAnchorSuspicionBonus} suspicion " +
                      $"to {anchorId}, total={data.Suspicion:F0}");
        }
    }

    #endregion
}
