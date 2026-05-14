using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Commit 2：重复干预惩罚栈 — 管同区域、同目标链、同机关类型的复用惩罚。
///
/// 核心规则（S130）：
///   - 同一锚点/同一机关类型/同一路线的重复干预，收益递减、Heat 与 Suspicion 递增。
///   - 防止 Trickster 用"低风险小恶心"作为最优解。
///   - 鼓励 Trickster 做高风险、高暴露成本的骗局，而非反复骚扰。
///
/// 接入方式：
///   - 监听 TricksterAbilitySystem.OnPropActivated 事件。
///   - 每次出手记录锚点 ID、机关类型和路线 ID。
///   - 重复出手时向 MarioSuspicionTracker 额外叠加 Suspicion。
///   - 提供 Heat 值供后续 TricksterHeatMeter（Commit 4）读取。
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
/// </summary>
public class RepeatInterferenceStack : MonoBehaviour
{
    // ─────────────────────────────────────────────────────
    #region 数据结构

    [System.Serializable]
    public class InterferenceRecord
    {
        public string Key;           // 锚点ID 或 机关类型
        public int RepeatCount;      // 重复次数
        public float LastTime;       // 上次干预时间
        public float AccumulatedHeat; // 该 key 累积的 Heat

        public InterferenceRecord(string key)
        {
            Key = key;
            RepeatCount = 0;
            LastTime = -999f;
            AccumulatedHeat = 0f;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 配置

    [Header("=== Commit 2 重复干预惩罚 ===")]
    [Tooltip("重复干预的时间窗口（秒）— 超过此时间不算重复")]
    [SerializeField] private float repeatWindow = 15f;

    [Tooltip("每次重复时 Suspicion 额外加成的基础值")]
    [SerializeField] private float repeatSuspicionBase = 12f;

    [Tooltip("每次重复时 Suspicion 加成的递增倍率（第N次 = base * multiplier^(N-1)）")]
    [SerializeField] private float repeatSuspicionMultiplier = 1.5f;

    [Tooltip("每次重复时累积的 Heat 基础值")]
    [SerializeField] private float repeatHeatBase = 8f;

    [Tooltip("每次重复时 Heat 递增倍率")]
    [SerializeField] private float repeatHeatMultiplier = 1.3f;

    [Tooltip("收益递减系数（第N次重复的 Trickster 收益 = 原始收益 * diminishFactor^(N-1)）")]
    [SerializeField] private float diminishFactor = 0.6f;

    [Tooltip("Heat 每秒自然衰减量")]
    [SerializeField] private float heatDecayPerSecond = 2f;

    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool showDebugInfo = false;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 运行时状态

    private Dictionary<string, InterferenceRecord> records = new Dictionary<string, InterferenceRecord>();
    private TricksterAbilitySystem abilitySystem;
    private MarioSuspicionTracker suspicionTracker;

    // 全局 Heat 值（供后续 TricksterHeatMeter 读取）
    private float totalHeat;

    // ── 事件 ──
    /// <summary>重复干预发生时触发（key, 重复次数, 收益递减系数）</summary>
    public event Action<string, int, float> OnRepeatInterference;

    /// <summary>Heat 变化时触发（新 Heat 值）</summary>
    public event Action<float> OnHeatChanged;

    // ── 公共属性 ──
    public float TotalHeat => totalHeat;
    public float MaxHeat => 100f;
    public float HeatNormalized => totalHeat / MaxHeat;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 生命周期

    private void Start()
    {
        var trickster = FindObjectOfType<TricksterController>();
        if (trickster != null)
        {
            abilitySystem = trickster.GetComponent<TricksterAbilitySystem>();
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
        // Heat 自然衰减
        if (totalHeat > 0f)
        {
            float oldHeat = totalHeat;
            totalHeat = Mathf.Max(0f, totalHeat - heatDecayPerSecond * Time.deltaTime);
            if (Mathf.Abs(oldHeat - totalHeat) > 0.01f)
            {
                OnHeatChanged?.Invoke(totalHeat);
            }
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 公共 API

    /// <summary>获取指定 key 的当前收益递减系数（1.0=无递减）</summary>
    public float GetDiminishFactor(string key)
    {
        if (!records.TryGetValue(key, out var record)) return 1f;
        if (IsExpired(record)) return 1f;
        return Mathf.Pow(diminishFactor, record.RepeatCount);
    }

    /// <summary>获取指定 key 的重复次数</summary>
    public int GetRepeatCount(string key)
    {
        if (!records.TryGetValue(key, out var record)) return 0;
        if (IsExpired(record)) return 0;
        return record.RepeatCount;
    }

    /// <summary>手动添加 Heat（供外部系统使用）</summary>
    public void AddHeat(float amount)
    {
        totalHeat = Mathf.Clamp(totalHeat + amount, 0f, MaxHeat);
        OnHeatChanged?.Invoke(totalHeat);
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 事件处理

    private void HandlePropActivated(IControllableProp prop)
    {
        if (prop == null) return;

        Transform propTransform = prop.GetTransform();
        if (propTransform == null) return;

        // 生成 key：优先用锚点 ID，其次用机关名
        PossessionAnchor anchor = propTransform.GetComponent<PossessionAnchor>();
        string key = anchor != null ? anchor.AnchorId : propTransform.name;

        // 获取或创建记录
        if (!records.TryGetValue(key, out var record))
        {
            record = new InterferenceRecord(key);
            records[key] = record;
        }

        // 判断是否在重复窗口内
        if (IsExpired(record))
        {
            // 超出窗口，重置计数
            record.RepeatCount = 0;
            record.AccumulatedHeat = 0f;
        }

        // 记录本次干预
        record.RepeatCount++;
        record.LastTime = Time.time;

        // 如果是重复（count > 1），施加惩罚
        if (record.RepeatCount > 1)
        {
            int repeatN = record.RepeatCount - 1; // 第几次重复

            // 计算额外 Suspicion
            float extraSuspicion = repeatSuspicionBase * Mathf.Pow(repeatSuspicionMultiplier, repeatN - 1);

            // 计算额外 Heat
            float extraHeat = repeatHeatBase * Mathf.Pow(repeatHeatMultiplier, repeatN - 1);

            // 计算收益递减
            float currentDiminish = Mathf.Pow(diminishFactor, repeatN);

            // 应用 Suspicion 惩罚
            if (suspicionTracker != null && anchor != null)
            {
                AnchorSuspicionData data = suspicionTracker.GetOrCreateData(anchor);
                data.AddSuspicion(extraSuspicion);
            }

            // 应用 Heat
            record.AccumulatedHeat += extraHeat;
            totalHeat = Mathf.Clamp(totalHeat + extraHeat, 0f, MaxHeat);
            OnHeatChanged?.Invoke(totalHeat);

            if (showDebugInfo)
            {
                Debug.Log($"[RepeatInterferenceStack] Repeat #{record.RepeatCount} at {key}: " +
                          $"extraSuspicion={extraSuspicion:F1}, extraHeat={extraHeat:F1}, " +
                          $"diminish={currentDiminish:F2}, totalHeat={totalHeat:F1}");
            }

            OnRepeatInterference?.Invoke(key, record.RepeatCount, currentDiminish);
        }
        else
        {
            // 首次使用，给少量基础 Heat
            float baseHeat = repeatHeatBase * 0.5f;
            totalHeat = Mathf.Clamp(totalHeat + baseHeat, 0f, MaxHeat);
            OnHeatChanged?.Invoke(totalHeat);
        }
    }

    private void HandleRoundStart()
    {
        records.Clear();
        totalHeat = 0f;
        OnHeatChanged?.Invoke(totalHeat);
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 工具方法

    private bool IsExpired(InterferenceRecord record)
    {
        return (Time.time - record.LastTime) > repeatWindow;
    }

    #endregion
}
