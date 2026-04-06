using UnityEngine;

/// <summary>
/// 可操控道具抽象基类 - 封装 Telegraph→Active→Cooldown 三阶段状态机
/// 参考: Crawl 游戏的陷阱操控 + 格斗游戏的"前摇/后摇"概念
/// 
/// 子类只需实现:
///   OnTelegraphStart()  - 预警开始时的表现（变红、震动等）
///   OnTelegraphEnd()    - 预警结束，恢复预警表现
///   OnActivate()        - 实际的阻碍效果（伸出尖刺、移动平台等）
///   OnActiveEnd()       - 激活结束，恢复正常
/// 
/// 基类自动处理: 状态切换、计时、次数消耗、冷却、UI 数据
/// 
/// Session 20 更新：
///   - 新增 SetHighlight(bool) 实现目标高亮视觉反馈
///   - 新增 GetTransform() 供连线系统使用
///   - 高亮效果：被锁定时 Sprite 颜色微红 + 轻微缩放脉冲
/// </summary>
public abstract class ControllablePropBase : MonoBehaviour, IControllableProp
{
    [Header("=== 操控配置 ===")]
    [Tooltip("道具显示名称")]
    [SerializeField] protected string propName = "Unknown Prop";

    [Tooltip("预警时长（秒）- 给 Mario 的反应窗口")]
    [SerializeField] protected float telegraphDuration = 0.8f;

    [Tooltip("激活持续时长（秒）- 阻碍效果的持续时间")]
    [SerializeField] protected float activeDuration = 1.5f;

    [Tooltip("操控冷却时间（秒）")]
    [SerializeField] protected float cooldownDuration = 3f;

    [Header("=== 次数限制 ===")]
    [Tooltip("最大操控次数（-1=无限次）")]
    [SerializeField] protected int maxUses = -1;

    [Tooltip("每回合重置次数")]
    [SerializeField] protected bool resetUsesPerRound = true;

    [Header("=== 预警视觉效果 ===")]
    [Tooltip("预警时的闪烁颜色")]
    [SerializeField] protected Color telegraphColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Tooltip("预警时的闪烁频率（次/秒）")]
    [SerializeField] protected float telegraphFlashRate = 8f;

    [Tooltip("预警时是否震动")]
    [SerializeField] protected bool telegraphShake = true;

    [Tooltip("预警震动幅度")]
    [SerializeField] protected float telegraphShakeIntensity = 0.05f;

    // 状态
    protected PropControlState currentState = PropControlState.Idle;
    protected float stateTimer;
    protected int remainingUses;
    protected Vector2 activateDirection;

    // 组件缓存
    protected SpriteRenderer spriteRenderer;
    protected Color originalColor;
    private Vector3 originalLocalPosition;

    // Session 20: 高亮状态
    private bool _isHighlighted;
    private static readonly Color HighlightColor = new Color(1f, 0.5f, 0.5f, 1f); // 微红色

    // 公共属性
    public string PropName => propName;

    protected virtual void Awake()
    {
        // S37: 视碰分离 — SpriteRenderer 可能在子物体 Visual 上
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        remainingUses = maxUses;
    }

    protected virtual void Update()
    {
        switch (currentState)
        {
            case PropControlState.Telegraph:
                UpdateTelegraph();
                break;
            case PropControlState.Active:
                UpdateActive();
                break;
            case PropControlState.Cooldown:
                UpdateCooldown();
                break;
        }

        // Session 20: 高亮脉冲效果（仅在 Idle 状态下显示，避免与预警闪烁冲突）
        if (_isHighlighted && currentState == PropControlState.Idle && spriteRenderer != null)
        {
            float pulse = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f; // 0~1 脉冲
            spriteRenderer.color = Color.Lerp(originalColor, HighlightColor, 0.4f + pulse * 0.3f);
        }
    }

    #region IControllableProp 实现

    public bool CanBeControlled()
    {
        if (currentState != PropControlState.Idle) return false;
        if (maxUses >= 0 && remainingUses <= 0) return false;
        return true;
    }

    public void OnTricksterActivate(Vector2 direction)
    {
        if (!CanBeControlled()) return;

        activateDirection = direction;
        EnterTelegraph();
    }

    public int GetRemainingUses()
    {
        return maxUses < 0 ? -1 : remainingUses;
    }

    public float GetCooldownProgress()
    {
        if (currentState != PropControlState.Cooldown) return 0f;
        return cooldownDuration > 0 ? stateTimer / cooldownDuration : 0f;
    }

    public float GetTelegraphDuration()
    {
        return telegraphDuration;
    }

    public PropControlState GetControlState()
    {
        return currentState;
    }

    /// <summary>
    /// Session 20: 设置目标高亮状态
    /// 被锁定为当前操控目标时 Sprite 颜色微红脉冲
    /// </summary>
    public void SetHighlight(bool isSelected)
    {
        _isHighlighted = isSelected;
        if (!isSelected && spriteRenderer != null && currentState == PropControlState.Idle)
        {
            // 取消高亮时恢复原色
            spriteRenderer.color = originalColor;
        }
    }

    /// <summary>
    /// Session 20: 获取道具的 Transform（用于连线系统计算位置）
    /// </summary>
    public Transform GetTransform()
    {
        return transform;
    }

    #endregion

    #region 状态机

    private void EnterTelegraph()
    {
        currentState = PropControlState.Telegraph;
        stateTimer = telegraphDuration;
        originalLocalPosition = transform.localPosition;

        // 消耗次数
        if (maxUses >= 0)
        {
            remainingUses--;
        }

        OnTelegraphStart();
    }

    private void UpdateTelegraph()
    {
        stateTimer -= Time.deltaTime;

        // 预警视觉效果：闪烁
        if (spriteRenderer != null)
        {
            float flash = Mathf.Sin(Time.time * telegraphFlashRate * Mathf.PI * 2f);
            spriteRenderer.color = Color.Lerp(originalColor, telegraphColor, (flash + 1f) * 0.5f);
        }

        // 预警视觉效果：震动
        if (telegraphShake)
        {
            float shakeX = Random.Range(-telegraphShakeIntensity, telegraphShakeIntensity);
            float shakeY = Random.Range(-telegraphShakeIntensity, telegraphShakeIntensity);
            transform.localPosition = originalLocalPosition + new Vector3(shakeX, shakeY, 0f);
        }

        // 预警结束 → 进入激活
        if (stateTimer <= 0f)
        {
            // 恢复预警表现
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
            if (telegraphShake)
            {
                transform.localPosition = originalLocalPosition;
            }

            OnTelegraphEnd();
            EnterActive();
        }
    }

    private void EnterActive()
    {
        currentState = PropControlState.Active;
        stateTimer = activeDuration;

        OnActivate(activateDirection);
    }

    private void UpdateActive()
    {
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            OnActiveEnd();
            EnterCooldown();
        }
    }

    private void EnterCooldown()
    {
        currentState = PropControlState.Cooldown;
        stateTimer = cooldownDuration;
    }

    private void UpdateCooldown()
    {
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            if (maxUses >= 0 && remainingUses <= 0)
            {
                currentState = PropControlState.Exhausted;
            }
            else
            {
                currentState = PropControlState.Idle;
            }
        }
    }

    #endregion

    #region 回合重置

    /// <summary>调试用：重置冷却时间（由冷却取消开关调用）</summary>
    public void ResetCooldown()
    {
        if (currentState == PropControlState.Cooldown)
        {
            currentState = PropControlState.Idle;
            stateTimer = 0f;
        }
    }

    /// <summary>
    /// 重置操控次数（由 GameManager 在新回合开始时调用）
    /// </summary>
    public void ResetUses()
    {
        if (resetUsesPerRound)
        {
            remainingUses = maxUses;
            currentState = PropControlState.Idle;
            stateTimer = 0f;
        }
    }

    #endregion

    #region 子类必须实现的方法

    /// <summary>预警开始 - 子类可添加额外的预警表现（音效、粒子等）</summary>
    protected abstract void OnTelegraphStart();

    /// <summary>预警结束 - 子类恢复预警期间的额外表现</summary>
    protected abstract void OnTelegraphEnd();

    /// <summary>激活 - 子类执行实际的阻碍效果</summary>
    /// <param name="direction">Trickster 输入的方向</param>
    protected abstract void OnActivate(Vector2 direction);

    /// <summary>激活结束 - 子类恢复到正常状态</summary>
    protected abstract void OnActiveEnd();

    #endregion

    #region 调试可视化

    protected virtual void OnDrawGizmosSelected()
    {
        // 显示当前状态
        switch (currentState)
        {
            case PropControlState.Telegraph:
                Gizmos.color = Color.yellow;
                break;
            case PropControlState.Active:
                Gizmos.color = Color.red;
                break;
            case PropControlState.Cooldown:
                Gizmos.color = Color.gray;
                break;
            default:
                Gizmos.color = Color.green;
                break;
        }
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
    }

    #endregion
}
