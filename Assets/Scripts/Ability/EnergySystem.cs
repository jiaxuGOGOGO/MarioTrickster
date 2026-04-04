using UnityEngine;

/// <summary>
/// Trickster 能量系统
/// 
/// 设计思路：
///   Trickster 拥有一个能量条，变身和操控道具都消耗能量。
///   能量会随时间自然恢复，但变身期间恢复速率降低。
///   能量耗尽时强制解除变身，增加博弈深度：
///   Trickster 需要权衡"何时变身"和"变身后做什么"。
///
/// 能量消耗规则：
///   - 变身：消耗固定能量（disguiseCost）
///   - 维持变身：每秒消耗能量（disguiseDrainPerSecond）
///   - 操控道具：消耗固定能量（controlCost）
///   - 完全融入场景后：维持消耗降低（blendedDrainMultiplier）
///
/// 能量恢复规则：
///   - 未变身时：按 regenPerSecond 恢复
///   - 变身时：按 regenPerSecond * disguisedRegenMultiplier 恢复（默认 0，即不恢复）
///
/// 使用方式：挂载在 Trickster GameObject 上
/// </summary>
public class EnergySystem : MonoBehaviour
{
    [Header("=== 能量池 ===")]
    [Tooltip("最大能量值")]
    [SerializeField] private float maxEnergy = 100f;
    [Tooltip("初始能量值（-1 = 满能量）")]
    [SerializeField] private float startEnergy = -1f;

    [Header("=== 能量消耗 ===")]
    [Tooltip("变身消耗的固定能量")]
    [SerializeField] private float disguiseCost = 20f;
    [Tooltip("变身期间每秒消耗的能量")]
    [SerializeField] private float disguiseDrainPerSecond = 5f;
    [Tooltip("完全融入场景后，维持消耗的倍率（0.5 = 消耗减半）")]
    [SerializeField] private float blendedDrainMultiplier = 0.5f;
    [Tooltip("操控道具消耗的固定能量")]
    [SerializeField] private float controlCost = 15f;

    [Header("=== 能量恢复 ===")]
    [Tooltip("未变身时每秒恢复的能量")]
    [SerializeField] private float regenPerSecond = 8f;
    [Tooltip("变身时能量恢复倍率（0 = 不恢复）")]
    [SerializeField] private float disguisedRegenMultiplier = 0f;
    [Tooltip("操控道具后恢复延迟（秒）")]
    [SerializeField] private float regenDelayAfterControl = 2f;

    [Header("=== 低能量警告 ===")]
    [Tooltip("能量低于此比例时触发低能量警告")]
    [SerializeField] private float lowEnergyThreshold = 0.25f;

    // 内部状态
    private float currentEnergy;
    private float regenDelayTimer;
    private DisguiseSystem disguiseSystem;
    private bool wasLowEnergy;

    // ── Test Console 调试开关（仅 Editor/Development Build 可用）──
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Infinite Energy：开启后能量不消耗，始终保持满值。
    /// 默认 false，仅由 TestConsoleWindow 在运行时设置，
    /// 每次 Play 自动重置为 false，不影响自动化测试。
    /// </summary>
    [System.NonSerialized] public bool DebugInfiniteEnergy = false;
#endif

    // 公共属性
    public float CurrentEnergy => currentEnergy;
    public float MaxEnergy => maxEnergy;
    public float EnergyPercent => maxEnergy > 0 ? currentEnergy / maxEnergy : 0f;
    public bool IsLowEnergy => EnergyPercent <= lowEnergyThreshold;
    public bool HasEnoughForDisguise => currentEnergy >= disguiseCost;
    public bool HasEnoughForControl => currentEnergy >= controlCost;

    // 事件
    public System.Action<float, float> OnEnergyChanged;     // (当前, 最大)
    public System.Action OnEnergyDepleted;                    // 能量耗尽
    public System.Action<bool> OnLowEnergyWarning;           // 进入/离开低能量状态

    private void Awake()
    {
        disguiseSystem = GetComponent<DisguiseSystem>();
        currentEnergy = startEnergy < 0 ? maxEnergy : Mathf.Clamp(startEnergy, 0, maxEnergy);
    }

    private void Update()
    {
        if (disguiseSystem == null) return;

        // 变身期间持续消耗能量
        if (disguiseSystem.IsDisguised)
        {
            float drain = disguiseDrainPerSecond;
            if (disguiseSystem.IsFullyBlended)
            {
                drain *= blendedDrainMultiplier;
            }
            ConsumeEnergy(drain * Time.deltaTime, false);

            // 能量耗尽，强制解除变身
            if (currentEnergy <= 0f)
            {
                disguiseSystem.Undisguise();
                OnEnergyDepleted?.Invoke();
            }
        }

        // 能量恢复
        HandleRegen();

        // 低能量警告检测
        CheckLowEnergyWarning();
    }

    #region 能量消耗

    /// <summary>
    /// 尝试消耗变身所需能量
    /// </summary>
    /// <returns>是否有足够能量</returns>
    public bool TryConsumeDisguiseCost()
    {
        if (currentEnergy < disguiseCost) return false;
        ConsumeEnergy(disguiseCost, true);
        return true;
    }

    /// <summary>
    /// 尝试消耗操控道具所需能量
    /// </summary>
    /// <returns>是否有足够能量</returns>
    public bool TryConsumeControlCost()
    {
        if (currentEnergy < controlCost) return false;
        ConsumeEnergy(controlCost, true);
        regenDelayTimer = regenDelayAfterControl;
        return true;
    }

    /// <summary>
    /// 消耗能量
    /// </summary>
    private void ConsumeEnergy(float amount, bool notify)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // [AI防坑警告] 无限能量拦截：仅在调试开关开启时跳过消耗，默认关闭，不影响自动化测试
        if (DebugInfiniteEnergy)
        {
            currentEnergy = maxEnergy;
            return;
        }
#endif
        float prev = currentEnergy;
        currentEnergy = Mathf.Max(0f, currentEnergy - amount);
        if (notify || Mathf.Abs(prev - currentEnergy) > 0.5f)
        {
            OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
        }
    }

    #endregion

    #region 能量恢复

    private void HandleRegen()
    {
        // 恢复延迟
        if (regenDelayTimer > 0f)
        {
            regenDelayTimer -= Time.deltaTime;
            return;
        }

        if (currentEnergy >= maxEnergy) return;

        float regenRate = regenPerSecond;

        // 变身时恢复倍率
        if (disguiseSystem != null && disguiseSystem.IsDisguised)
        {
            regenRate *= disguisedRegenMultiplier;
        }

        if (regenRate <= 0f) return;

        float prev = currentEnergy;
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + regenRate * Time.deltaTime);

        // 每恢复一定量通知一次（避免每帧触发事件）
        if (Mathf.FloorToInt(currentEnergy) != Mathf.FloorToInt(prev))
        {
            OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
        }
    }

    #endregion

    #region 低能量警告

    private void CheckLowEnergyWarning()
    {
        bool isLow = IsLowEnergy && currentEnergy > 0f;
        if (isLow != wasLowEnergy)
        {
            wasLowEnergy = isLow;
            OnLowEnergyWarning?.Invoke(isLow);
        }
    }

    #endregion

    #region 公共方法

    /// <summary>重置能量（回合开始时调用）</summary>
    public void ResetEnergy()
    {
        currentEnergy = maxEnergy;
        regenDelayTimer = 0f;
        wasLowEnergy = false;
        OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }

    /// <summary>增加能量（道具/奖励）</summary>
    public void AddEnergy(float amount)
    {
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);
        OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }

    /// <summary>获取变身消耗量（供 UI 显示）</summary>
    public float GetDisguiseCost() => disguiseCost;

    /// <summary>获取操控消耗量（供 UI 显示）</summary>
    public float GetControlCost() => controlCost;

    #endregion

    #region 调试

    private void OnGUI()
    {
        // 能量条调试显示（在 Trickster 头顶）
        if (Camera.main == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
        if (screenPos.z < 0) return;

        float barWidth = 60f;
        float barHeight = 8f;
        float x = screenPos.x - barWidth / 2f;
        float y = Screen.height - screenPos.y;

        // 背景
        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.DrawTexture(new Rect(x - 1, y - 1, barWidth + 2, barHeight + 2), Texture2D.whiteTexture);

        // 能量条
        Color barColor = IsLowEnergy ? new Color(1f, 0.3f, 0.1f) : new Color(0.2f, 0.6f, 1f);
        GUI.color = barColor;
        GUI.DrawTexture(new Rect(x, y, barWidth * EnergyPercent, barHeight), Texture2D.whiteTexture);

        GUI.color = Color.white;
    }

    #endregion
}
