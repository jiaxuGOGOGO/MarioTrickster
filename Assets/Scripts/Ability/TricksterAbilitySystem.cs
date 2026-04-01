using UnityEngine;

/// <summary>
/// Trickster 能力系统 - 变身后操控关卡道具的核心管理器
/// 参考: Crawl 游戏的 Ghost Possess Trap 机制
/// 
/// 工作流程:
///   1. Trickster 变身为某个道具（由 DisguiseSystem 处理）
///   2. 完全融入场景后（isFullyBlended），本系统激活
///   3. 玩家按下"操控"键 → 检测附近的 IControllableProp → 触发操控
///   4. 道具进入 Telegraph→Active→Cooldown 流程
/// 
/// 操控模式:
///   - 就近操控: 操控距离 Trickster 最近的可操控道具
///   - 绑定操控: 变身时自动绑定最近的同类道具（推荐）
/// 
/// 使用方式: 挂载在 Trickster GameObject 上
/// </summary>
[RequireComponent(typeof(DisguiseSystem))]
public class TricksterAbilitySystem : MonoBehaviour
{
    [Header("=== 操控配置 ===")]
    [Tooltip("操控检测半径")]
    [SerializeField] private float controlRange = 2f;

    [Tooltip("是否需要完全融入场景才能操控")]
    [SerializeField] private bool requireBlended = true;

    [Tooltip("操控模式: true=变身时绑定最近道具, false=每次操控时检测最近道具")]
    [SerializeField] private bool bindOnDisguise = true;

    [Header("=== 操控限制 ===")]
    [Tooltip("单次变身最大操控总次数（-1=无限）")]
    [SerializeField] private int maxControlsPerDisguise = -1;

    [Tooltip("操控持续时间限制（秒，0=无限）")]
    [SerializeField] private float controlTimeLimit = 0f;

    [Header("=== 调试 ===")]
    [SerializeField] private bool showDebugInfo = false;

    // 组件
    private DisguiseSystem disguiseSystem;
    private TricksterController tricksterController;

    // 状态
    private IControllableProp boundProp;          // 当前绑定的道具
    private GameObject boundPropObject;           // 绑定道具的 GameObject（用于距离检测）
    private int controlsUsedThisDisguise;         // 本次变身已使用的操控次数
    private float controlTimeUsed;                // 本次变身已使用的操控时间
    private bool isAbilityActive;                 // 能力系统是否激活（变身且融入后）
    private Vector2 lastInputDirection;           // 最近一次输入方向

    // 公共属性
    public bool IsAbilityActive => isAbilityActive;
    public IControllableProp BoundProp => boundProp;
    public int ControlsRemaining => maxControlsPerDisguise < 0 ? -1 : maxControlsPerDisguise - controlsUsedThisDisguise;
    public float ControlTimeRemaining => controlTimeLimit <= 0 ? -1f : controlTimeLimit - controlTimeUsed;

    // 事件
    public System.Action<IControllableProp> OnPropBound;      // 绑定了道具
    public System.Action OnPropUnbound;                        // 解绑了道具
    public System.Action<IControllableProp> OnPropActivated;  // 触发了道具操控

    private void Awake()
    {
        disguiseSystem = GetComponent<DisguiseSystem>();
        tricksterController = GetComponent<TricksterController>();
    }

    private void OnEnable()
    {
        if (disguiseSystem != null)
        {
            disguiseSystem.OnDisguiseChanged += HandleDisguiseChanged;
        }
    }

    private void OnDisable()
    {
        if (disguiseSystem != null)
        {
            disguiseSystem.OnDisguiseChanged -= HandleDisguiseChanged;
        }
    }

    private void Update()
    {
        // 检查能力系统是否应该激活
        bool shouldBeActive = disguiseSystem != null &&
                              disguiseSystem.IsDisguised &&
                              (!requireBlended || disguiseSystem.IsFullyBlended);

        if (shouldBeActive && !isAbilityActive)
        {
            ActivateAbility();
        }
        else if (!shouldBeActive && isAbilityActive)
        {
            DeactivateAbility();
        }

        // 更新操控时间
        if (isAbilityActive && controlTimeLimit > 0)
        {
            controlTimeUsed += Time.deltaTime;
            if (controlTimeUsed >= controlTimeLimit)
            {
                // 操控时间用尽，强制解除变身
                if (disguiseSystem != null && disguiseSystem.IsDisguised)
                {
                    disguiseSystem.Undisguise();
                }
            }
        }
    }

    #region 输入回调（由 TricksterController 调用）

    /// <summary>
    /// 操控键按下 - 尝试操控当前绑定的道具
    /// </summary>
    public void OnAbilityPressed()
    {
        if (!isAbilityActive) return;

        // 检查操控次数限制
        if (maxControlsPerDisguise >= 0 && controlsUsedThisDisguise >= maxControlsPerDisguise)
        {
            if (showDebugInfo) Debug.Log("[TricksterAbility] 操控次数已用尽");
            return;
        }

        // 获取要操控的道具
        IControllableProp prop = GetTargetProp();
        if (prop == null)
        {
            if (showDebugInfo) Debug.Log("[TricksterAbility] 附近没有可操控的道具");
            return;
        }

        if (!prop.CanBeControlled())
        {
            if (showDebugInfo) Debug.Log($"[TricksterAbility] {prop.PropName} 当前不可操控 (状态: {prop.GetControlState()})");
            return;
        }

        // 触发操控
        prop.OnTricksterActivate(lastInputDirection);
        controlsUsedThisDisguise++;

        OnPropActivated?.Invoke(prop);

        if (showDebugInfo)
        {
            Debug.Log($"[TricksterAbility] 操控 {prop.PropName}! 方向: {lastInputDirection}, " +
                      $"剩余次数: {ControlsRemaining}");
        }
    }

    /// <summary>
    /// 更新输入方向（用于有方向性的道具操控）
    /// </summary>
    public void SetAbilityDirection(Vector2 direction)
    {
        if (direction.magnitude > 0.1f)
        {
            lastInputDirection = direction.normalized;
        }
    }

    #endregion

    #region 内部方法

    private void HandleDisguiseChanged(bool isDisguised)
    {
        if (isDisguised)
        {
            // 变身时重置计数
            controlsUsedThisDisguise = 0;
            controlTimeUsed = 0f;

            // 绑定模式：变身时立即绑定最近的道具
            if (bindOnDisguise)
            {
                BindNearestProp();
            }
        }
        else
        {
            // 解除变身时清理
            DeactivateAbility();
            UnbindProp();
        }
    }

    private void ActivateAbility()
    {
        isAbilityActive = true;

        if (showDebugInfo)
        {
            string propInfo = boundProp != null ? boundProp.PropName : "无绑定道具";
            Debug.Log($"[TricksterAbility] 能力系统激活! 绑定道具: {propInfo}");
        }
    }

    private void DeactivateAbility()
    {
        isAbilityActive = false;
    }

    /// <summary>绑定最近的可操控道具</summary>
    private void BindNearestProp()
    {
        IControllableProp nearest = null;
        float nearestDist = float.MaxValue;
        GameObject nearestObj = null;

        // 查找场景中所有实现了 IControllableProp 的组件
        ControllablePropBase[] allProps = FindObjectsByType<ControllablePropBase>(FindObjectsSortMode.None);

        foreach (ControllablePropBase prop in allProps)
        {
            float dist = Vector2.Distance(transform.position, prop.transform.position);
            if (dist <= controlRange && dist < nearestDist)
            {
                nearest = prop;
                nearestDist = dist;
                nearestObj = prop.gameObject;
            }
        }

        if (nearest != null)
        {
            boundProp = nearest;
            boundPropObject = nearestObj;
            OnPropBound?.Invoke(nearest);

            if (showDebugInfo)
            {
                Debug.Log($"[TricksterAbility] 绑定道具: {nearest.PropName} (距离: {nearestDist:F1})");
            }
        }
    }

    /// <summary>解绑道具</summary>
    private void UnbindProp()
    {
        if (boundProp != null)
        {
            boundProp = null;
            boundPropObject = null;
            OnPropUnbound?.Invoke();
        }
    }

    /// <summary>获取当前要操控的道具</summary>
    private IControllableProp GetTargetProp()
    {
        if (bindOnDisguise)
        {
            // 绑定模式：返回已绑定的道具
            // 检查道具是否还在范围内
            if (boundPropObject != null)
            {
                float dist = Vector2.Distance(transform.position, boundPropObject.transform.position);
                if (dist <= controlRange * 1.5f) // 绑定后允许稍微远一点
                {
                    return boundProp;
                }
            }
            return null;
        }
        else
        {
            // 就近模式：每次操控时检测最近的道具
            BindNearestProp();
            return boundProp;
        }
    }

    #endregion

    #region 调试可视化

    private void OnDrawGizmosSelected()
    {
        // 显示操控范围
        Gizmos.color = isAbilityActive ? Color.magenta : Color.gray;
        Gizmos.DrawWireSphere(transform.position, controlRange);

        // 显示绑定连线
        if (boundPropObject != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, boundPropObject.transform.position);
        }
    }

    private void OnGUI()
    {
        if (!showDebugInfo) return;

        Vector3 screenPos = Camera.main != null ? Camera.main.WorldToScreenPoint(transform.position) : Vector3.zero;
        if (screenPos.z < 0) return;

        float x = screenPos.x - 80;
        float y = Screen.height - screenPos.y - 60;

        GUILayout.BeginArea(new Rect(x, y, 160, 80));
        GUILayout.Label($"Ability: {(isAbilityActive ? "ON" : "OFF")}");
        if (boundProp != null)
        {
            GUILayout.Label($"Prop: {boundProp.PropName}");
            GUILayout.Label($"State: {boundProp.GetControlState()}");
            GUILayout.Label($"Uses: {ControlsRemaining}");
        }
        GUILayout.EndArea();
    }

    #endregion
}
