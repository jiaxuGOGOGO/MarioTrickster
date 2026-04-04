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
/// Session 10 更新：集成 EnergySystem，操控道具消耗能量
/// 
/// Session 17 更新：
///   - 添加运行时 Gizmo 可视化（GL 绘制控制范围圆 + 绑定连线）
///   - 修复隐蔽状态下绑定逻辑：融入后自动重新绑定最近道具
///   - controlRange 可在 Inspector 中调整控制范围
/// 
/// Session 18 性能优化：
///   - BindNearestProp 改用缓存的道具列表，消除 Update 中每帧 FindObjectsByType
///   - 道具列表在 ActivateAbility 时刷新一次，按L操控时再刷新一次
///   - OnRenderObject GL 绘制段数从 48 降到 32，并添加相机过滤避免重复绘制
///   - OnGUI 调试面板默认关闭，不产生任何开销
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
    [Tooltip("操控检测半径 — 在 Inspector 中调整此值可控制 Trickster 的操控范围")]
    [SerializeField] private float controlRange = 5f;

    [Tooltip("是否需要完全融入场景才能操控")]
    [SerializeField] private bool requireBlended = true;

    [Tooltip("操控模式: true=变身时绑定最近道具, false=每次操控时检测最近道具")]
    [SerializeField] private bool bindOnDisguise = true;

    [Header("=== 操控限制 ===")]
    [Tooltip("单次变身最大操控总次数（-1=无限）")]
    [SerializeField] private int maxControlsPerDisguise = -1;

    [Tooltip("操控持续时间限制（秒，0=无限）")]
    [SerializeField] private float controlTimeLimit = 0f;

    [Header("=== 运行时 Gizmo 可视化 ===")]
    [Tooltip("运行时是否显示控制范围圆和绑定连线（方便测试）")]
    [SerializeField] private bool showRuntimeGizmo = true;

    [Tooltip("Gizmo 圆的线段数（越大越圆滑，越小越省性能）")]
    [SerializeField] private int gizmoSegments = 32;

    [Header("=== 调试 ===")]
    [SerializeField] private bool showDebugInfo = false;

    // 组件
    private DisguiseSystem disguiseSystem;
    private TricksterController tricksterController;
    private EnergySystem energySystem; // 可选：能量系统

    // 状态
    private IControllableProp boundProp;          // 当前绑定的道具
    private GameObject boundPropObject;           // 绑定道具的 GameObject（用于距离检测）
    private int controlsUsedThisDisguise;         // 本次变身已使用的操控次数
    private float controlTimeUsed;                // 本次变身已使用的操控时间
    private bool isAbilityActive;                 // 能力系统是否激活（变身且融入后）
    private Vector2 lastInputDirection;           // 最近一次输入方向

    // Session 18 性能优化：缓存道具列表，避免每帧 FindObjectsByType
    private ControllablePropBase[] cachedProps;
    private bool propsCacheDirty = true;          // 标记缓存是否需要刷新

    // GL 绘制用材质
    private Material glMaterial;

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
        energySystem = GetComponent<EnergySystem>(); // 可选组件

        // 创建 GL 绘制材质
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader != null)
        {
            glMaterial = new Material(shader);
            glMaterial.hideFlags = HideFlags.HideAndDontSave;
            glMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            glMaterial.SetInt("_ZWrite", 0);
        }
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

        // Session 17: 融入后自动重新绑定最近道具（解决隐蔽状态下无法绑定的问题）
        // Session 18 优化: 只在 boundProp 为空时尝试绑定，不再每帧扫描场景
        if (isAbilityActive && boundProp == null && bindOnDisguise)
        {
            BindNearestProp();
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

        // 能量检查（如果有 EnergySystem）
        if (energySystem != null)
        {
            if (!energySystem.TryConsumeControlCost())
            {
                if (showDebugInfo) Debug.Log("[TricksterAbility] 能量不足，无法操控道具");
                return;
            }
        }

        // 触发操控
        prop.OnTricksterActivate(lastInputDirection);
        controlsUsedThisDisguise++;

        OnPropActivated?.Invoke(prop);

        if (showDebugInfo)
        {
            string energyInfo = energySystem != null ? $", 剩余能量: {energySystem.CurrentEnergy:F0}" : "";
            Debug.Log($"[TricksterAbility] 操控 {prop.PropName}! 方向: {lastInputDirection}, " +
                      $"剩余次数: {ControlsRemaining}{energyInfo}");
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

            // 变身时标记缓存需要刷新
            propsCacheDirty = true;

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

        // 激活时刷新道具缓存
        propsCacheDirty = true;

        // Session 17: 激活时也尝试绑定（解决变身时距离远、融入后距离近的场景）
        if (bindOnDisguise && boundProp == null)
        {
            BindNearestProp();
        }

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

    /// <summary>
    /// Session 18 性能优化：刷新道具缓存列表
    /// 只在必要时调用（变身、激活、按L操控时），而非每帧
    /// </summary>
    private void RefreshPropsCache()
    {
        cachedProps = FindObjectsByType<ControllablePropBase>(FindObjectsSortMode.None);
        propsCacheDirty = false;
    }

    /// <summary>绑定最近的可操控道具</summary>
    private void BindNearestProp()
    {
        // Session 18: 使用缓存的道具列表
        if (propsCacheDirty || cachedProps == null)
        {
            RefreshPropsCache();
        }

        IControllableProp nearest = null;
        float nearestDist = float.MaxValue;
        GameObject nearestObj = null;

        foreach (ControllablePropBase prop in cachedProps)
        {
            if (prop == null) continue; // 防止已销毁的对象
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
            // Session 17: 如果绑定丢失，尝试重新绑定
            // Session 18: 操控时刷新缓存，确保能找到新道具
            propsCacheDirty = true;
            BindNearestProp();
            return boundProp;
        }
        else
        {
            // 就近模式：每次操控时检测最近的道具
            propsCacheDirty = true;
            BindNearestProp();
            return boundProp;
        }
    }

    #endregion

    #region 运行时 Gizmo 可视化 (Session 17)

    /// <summary>
    /// 使用 GL 在运行时绘制控制范围圆和绑定连线
    /// OnRenderObject 在所有相机渲染后调用，确保 Gizmo 始终可见
    /// 
    /// Session 18 性能优化：
    ///   - 添加 Game 视图主相机过滤，避免 Scene 视图+Game 视图重复绘制
    ///   - 段数从 48 降到 32（视觉差异极小，减少 GL 顶点数）
    /// </summary>
    private void OnRenderObject()
    {
        if (!showRuntimeGizmo || glMaterial == null) return;
        if (!Application.isPlaying) return;

        // Session 18: 只在 Game 视图的主相机中绘制一次，避免多相机重复绘制
        Camera cam = Camera.current;
        if (cam == null || cam != Camera.main) return;

        glMaterial.SetPass(0);

        // ── 绘制控制范围圆 ──
        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);
        GL.Begin(GL.LINES);

        // 范围圆颜色：激活=品红，未激活=灰色
        Color rangeColor = isAbilityActive ? new Color(1f, 0f, 1f, 0.5f) : new Color(0.5f, 0.5f, 0.5f, 0.3f);
        GL.Color(rangeColor);

        Vector3 center = transform.position;
        float angleStep = 360f / gizmoSegments;
        for (int i = 0; i < gizmoSegments; i++)
        {
            float a1 = i * angleStep * Mathf.Deg2Rad;
            float a2 = (i + 1) * angleStep * Mathf.Deg2Rad;
            GL.Vertex3(center.x + Mathf.Cos(a1) * controlRange, center.y + Mathf.Sin(a1) * controlRange, 0);
            GL.Vertex3(center.x + Mathf.Cos(a2) * controlRange, center.y + Mathf.Sin(a2) * controlRange, 0);
        }

        // ── 绘制绑定连线 ──
        if (boundPropObject != null)
        {
            Color lineColor = isAbilityActive ? new Color(1f, 1f, 0f, 0.8f) : new Color(1f, 1f, 0f, 0.3f);
            GL.Color(lineColor);
            GL.Vertex3(center.x, center.y, 0);
            GL.Vertex3(boundPropObject.transform.position.x, boundPropObject.transform.position.y, 0);

            // 绑定目标十字标记
            Vector3 tp = boundPropObject.transform.position;
            float crossSize = 0.3f;
            GL.Vertex3(tp.x - crossSize, tp.y, 0);
            GL.Vertex3(tp.x + crossSize, tp.y, 0);
            GL.Vertex3(tp.x, tp.y - crossSize, 0);
            GL.Vertex3(tp.x, tp.y + crossSize, 0);
        }

        GL.End();
        GL.PopMatrix();
    }

    #endregion

    #region Editor Gizmo

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

    #endregion

    #region 调试 GUI

    private void OnGUI()
    {
        if (!showDebugInfo) return;
        if (Camera.main == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        if (screenPos.z < 0) return;

        float x = screenPos.x - 80;
        float y = Screen.height - screenPos.y - 60;

        GUILayout.BeginArea(new Rect(x, y, 160, 100));
        GUILayout.Label($"Ability: {(isAbilityActive ? "ON" : "OFF")}");
        if (boundProp != null)
        {
            GUILayout.Label($"Prop: {boundProp.PropName}");
            GUILayout.Label($"State: {boundProp.GetControlState()}");
            GUILayout.Label($"Uses: {ControlsRemaining}");
        }
        if (energySystem != null)
        {
            GUILayout.Label($"Energy: {energySystem.CurrentEnergy:F0}/{energySystem.MaxEnergy:F0}");
        }
        GUILayout.EndArea();
    }

    #endregion
}
