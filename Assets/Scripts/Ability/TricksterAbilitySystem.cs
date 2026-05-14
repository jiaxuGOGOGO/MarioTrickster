using UnityEngine;

/// <summary>
/// Trickster 能力系统 - 变身后操控关卡道具的核心管理器
/// 参考: Crawl 游戏的 Ghost Possess Trap 机制
/// 
/// 工作流程:
///   1. Trickster 变身为某个道具（由 DisguiseSystem 处理）
///   2. 完全融入场景后（isFullyBlended），本系统激活
///   3. 玩家按方向键 → 磁吸切换目标道具（红色连线跟随切换）
///   4. 玩家按下"操控"键 → 触发当前锁定目标的操控
///   5. 道具进入 Telegraph→Active→Cooldown 流程
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
/// Session 20 重构：Trickster 道具操控选择系统
///   - 视觉连线反馈：融入后绘制红/灰连线到范围内所有可操控道具
///   - 目标高亮反馈：当前锁定目标 Sprite 微红脉冲
///   - 方向键磁吸切换：融入状态下方向键拦截为切换目标指令
///   - 使用 LineRenderer 对象池（预实例化），严禁 Update 中 Instantiate
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

    [Header("=== 连线视觉 (Session 20) ===")]
    [Tooltip("连线池大小（最多同时显示的连线数量）")]
    [SerializeField] private int maxLineRenderers = 8;

    [Tooltip("当前锁定目标连线宽度")]
    [SerializeField] private float selectedLineWidth = 0.08f;

    [Tooltip("备选目标连线宽度")]
    [SerializeField] private float candidateLineWidth = 0.03f;

    [Tooltip("当前锁定目标连线颜色（高亮红色）")]
    [SerializeField] private Color selectedLineColor = new Color(1f, 0.15f, 0.15f, 0.9f);

    [Tooltip("备选目标连线颜色（半透明暗灰色）")]
    [SerializeField] private Color candidateLineColor = new Color(0.4f, 0.4f, 0.4f, 0.35f);

    [Header("=== 运行时 Gizmo 可视化 ===")]
    [Tooltip("运行时是否显示控制范围圆和绑定连线（方便测试）")]
    [SerializeField] private bool showRuntimeGizmo = true;

    [Tooltip("Gizmo 圆的线段数（越大越圆滑，越小越省性能）")]
    // S57: 从 32 降到 24 段，减少 GL 顶点数，降低 GFX 内存压力
    [SerializeField] private int gizmoSegments = 24;

    [Header("=== 调试 ===")]
    [SerializeField] private bool showDebugInfo = false;

    // 组件
    private DisguiseSystem disguiseSystem;
    private TricksterController tricksterController;
    private EnergySystem energySystem; // 可选组件
    private TricksterPossessionGate possessionGate; // Commit 0：附身状态门禁（可选）

    // 状态
    private IControllableProp boundProp;          // 当前绑定（锁定）的道具
    private GameObject boundPropObject;           // 绑定道具的 GameObject（用于距离检测）
    private int controlsUsedThisDisguise;         // 本次变身已使用的操控次数
    private float controlTimeUsed;                // 本次变身已使用的操控时间
    private bool isAbilityActive;                 // 能力系统是否激活（变身且融入后）
    private Vector2 lastInputDirection;           // 最近一次输入方向

    // Session 18 性能优化：缓存道具列表，避免每帧 FindObjectsByType
    private ControllablePropBase[] cachedProps;
    private bool propsCacheDirty = true;          // 标记缓存是否需要刷新

    // Session 20: 范围内备选道具列表（每次激活/切换时刷新）
    private ControllablePropBase[] propsInRange;
    private int propsInRangeCount;
    private static readonly int MaxPropsInRange = 16;

    // Session 20: LineRenderer 对象池（预实例化，严禁 Update 中 Instantiate）
    private LineRenderer[] linePool;
    private Material lineMaterial; // 缓存的连线材质，Awake 中创建一次

    // GL 绘制用材质
    private Material glMaterial;

    // 公共属性
    public bool IsAbilityActive => isAbilityActive;
    public IControllableProp BoundProp => boundProp;
    public int ControlsRemaining => maxControlsPerDisguise < 0 ? -1 : maxControlsPerDisguise - controlsUsedThisDisguise;
    public float ControlTimeRemaining => controlTimeLimit <= 0 ? -1f : controlTimeLimit - controlTimeUsed;
    public TricksterPossessionState PossessionState => possessionGate != null ? possessionGate.CurrentState : TricksterPossessionState.Roaming;
    public bool IsPossessionActionAllowed => possessionGate == null || possessionGate.CanActivatePossession;
    public string PossessionGateDebugStatus => possessionGate != null ? possessionGate.GetDebugStatus() : "No possession gate";

    // 事件
    public System.Action<IControllableProp> OnPropBound;      // 绑定了道具
    public System.Action OnPropUnbound;                        // 解绑了道具
    public System.Action<IControllableProp> OnPropActivated;  // 触发了道具操控

    private void Awake()
    {
        disguiseSystem = GetComponent<DisguiseSystem>();
        tricksterController = GetComponent<TricksterController>();
        energySystem = GetComponent<EnergySystem>(); // 可选组件
        possessionGate = GetComponent<TricksterPossessionGate>();
        if (possessionGate == null)
        {
            // Commit 0：旧场景兼容。运行时补齐门禁组件，不要求手动改预制体即可启用五态约束。
            possessionGate = gameObject.AddComponent<TricksterPossessionGate>();
        }

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

        // Session 20: 预分配范围内道具数组
        propsInRange = new ControllablePropBase[MaxPropsInRange];
        propsInRangeCount = 0;

        // Session 20: 创建 LineRenderer 对象池（P5 合规：Awake 中预实例化）
        InitLineRendererPool();
    }

    #region Session 20: LineRenderer 对象池

    /// <summary>
    /// 在 Awake 中预实例化 LineRenderer 对象池。
    /// 严格遵守 P5 规范：不在 Update 中 Instantiate。
    /// </summary>
    private void InitLineRendererPool()
    {
        // 创建共享材质（P4 合规：只创建一次）
        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader != null)
        {
            lineMaterial = new Material(lineShader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        linePool = new LineRenderer[maxLineRenderers];
        for (int i = 0; i < maxLineRenderers; i++)
        {
            GameObject lineObj = new GameObject($"AbilityLine_{i}");
            lineObj.transform.SetParent(transform);
            lineObj.transform.localPosition = Vector3.zero;

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.sortingOrder = 100; // 确保在最上层
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;

            if (lineMaterial != null)
            {
                lr.material = lineMaterial;
            }

            lr.enabled = false; // 默认隐藏
            linePool[i] = lr;
        }
    }

    /// <summary>
    /// 更新所有连线的显示状态。
    /// 融入状态下：显示到范围内所有道具的连线（红=锁定，灰=备选）。
    /// 非融入状态：隐藏所有连线。
    /// </summary>
    private void UpdateLineRenderers()
    {
        if (linePool == null) return;

        if (!isAbilityActive)
        {
            // 非激活状态：隐藏所有连线
            HideAllLines();
            return;
        }

        Vector3 origin = transform.position;
        int lineIndex = 0;

        for (int i = 0; i < propsInRangeCount && lineIndex < linePool.Length; i++)
        {
            ControllablePropBase prop = propsInRange[i];
            if (prop == null) continue;

            LineRenderer lr = linePool[lineIndex];
            lr.enabled = true;

            Vector3 targetPos = prop.transform.position;
            lr.SetPosition(0, origin);
            lr.SetPosition(1, targetPos);

            bool isSelected = (prop == (object)boundProp);
            Color lineColor = isSelected ? selectedLineColor : candidateLineColor;
            float lineWidth = isSelected ? selectedLineWidth : candidateLineWidth;

            lr.startColor = lineColor;
            lr.endColor = lineColor;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;

            lineIndex++;
        }

        // 隐藏未使用的 LineRenderer
        for (int i = lineIndex; i < linePool.Length; i++)
        {
            linePool[i].enabled = false;
        }
    }

    /// <summary>隐藏所有连线</summary>
    private void HideAllLines()
    {
        if (linePool == null) return;
        for (int i = 0; i < linePool.Length; i++)
        {
            if (linePool[i] != null)
                linePool[i].enabled = false;
        }
    }

    #endregion

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

        // Session 20: 更新连线显示
        UpdateLineRenderers();
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

        // Commit 0：附身状态门禁。只有完全融入并锁定 PossessionAnchor 时才允许出手。
        if (possessionGate != null && !possessionGate.AllowsAbilityAction("ActivateProp"))
        {
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

    /// <summary>
    /// Session 20: 方向键磁吸切换目标
    /// 遍历 controlRange 内备选目标，计算相对于当前目标的方向向量，
    /// 利用点乘（Vector2.Dot）寻找与按键方向最一致的道具，设为新目标。
    /// 由 TricksterController 在融入状态下拦截方向键后调用。
    /// </summary>
    /// <param name="inputDirection">方向键输入方向（归一化）</param>
    public void SwitchTarget(Vector2 inputDirection)
    {
        if (!isAbilityActive) return;
        if (possessionGate != null && !possessionGate.CanSwitchTarget) return;
        if (inputDirection.sqrMagnitude < 0.01f) return;

        // 刷新范围内道具列表
        RefreshPropsInRange();

        if (propsInRangeCount <= 1) return; // 只有一个或没有道具，无需切换

        Vector2 inputDir = inputDirection.normalized;
        Vector2 currentPos = boundPropObject != null
            ? (Vector2)boundPropObject.transform.position
            : (Vector2)transform.position;

        float bestDot = -2f;
        ControllablePropBase bestCandidate = null;

        for (int i = 0; i < propsInRangeCount; i++)
        {
            ControllablePropBase prop = propsInRange[i];
            if (prop == null) continue;
            if (prop == (object)boundProp) continue; // 跳过当前锁定目标

            Vector2 dirToCandidate = ((Vector2)prop.transform.position - currentPos);
            if (dirToCandidate.sqrMagnitude < 0.01f) continue; // 重叠位置跳过

            float dot = Vector2.Dot(inputDir, dirToCandidate.normalized);

            // 只考虑与输入方向大致一致的候选（点乘 > 0，即夹角 < 90°）
            if (dot > 0f && dot > bestDot)
            {
                bestDot = dot;
                bestCandidate = prop;
            }
        }

        if (bestCandidate != null)
        {
            // 取消旧目标高亮
            if (boundProp != null)
            {
                boundProp.SetHighlight(false);
            }

            // 设置新目标
            boundProp = bestCandidate;
            boundPropObject = bestCandidate.gameObject;

            // 新目标高亮
            boundProp.SetHighlight(true);

            OnPropBound?.Invoke(boundProp);

            if (showDebugInfo)
            {
                Debug.Log($"[TricksterAbility] 磁吸切换目标 → {boundProp.PropName} (dot={bestDot:F2})");
            }
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

        // Session 20: 激活时刷新范围内道具列表并设置高亮
        RefreshPropsInRange();
        if (boundProp != null)
        {
            boundProp.SetHighlight(true);
        }

        if (showDebugInfo)
        {
            string propInfo = boundProp != null ? boundProp.PropName : "无绑定道具";
            Debug.Log($"[TricksterAbility] 能力系统激活! 绑定道具: {propInfo}, 范围内道具数: {propsInRangeCount}");
        }
    }

    private void DeactivateAbility()
    {
        // Session 20: 取消所有高亮
        if (boundProp != null)
        {
            boundProp.SetHighlight(false);
        }

        isAbilityActive = false;

        // Session 20: 隐藏所有连线
        HideAllLines();
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

    /// <summary>
    /// Session 20: 刷新范围内道具列表（用于连线和磁吸切换）
    /// </summary>
    private void RefreshPropsInRange()
    {
        if (propsCacheDirty || cachedProps == null)
        {
            RefreshPropsCache();
        }

        propsInRangeCount = 0;
        Vector2 myPos = transform.position;

        for (int i = 0; i < cachedProps.Length && propsInRangeCount < MaxPropsInRange; i++)
        {
            if (cachedProps[i] == null) continue;
            float dist = Vector2.Distance(myPos, cachedProps[i].transform.position);
            if (dist <= controlRange)
            {
                propsInRange[propsInRangeCount] = cachedProps[i];
                propsInRangeCount++;
            }
        }
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
            // Session 20: 取消旧目标高亮
            if (boundProp != null && boundProp != nearest)
            {
                boundProp.SetHighlight(false);
            }

            boundProp = nearest;
            boundPropObject = nearestObj;
            OnPropBound?.Invoke(nearest);

            // Session 20: 设置新目标高亮
            if (isAbilityActive)
            {
                boundProp.SetHighlight(true);
            }

            // Session 20: 同时刷新范围内道具列表
            RefreshPropsInRange();

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
            // Session 20: 取消高亮
            boundProp.SetHighlight(false);

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
    /// 
    /// Session 20 更新：
    ///   - 连线改用 LineRenderer 对象池实现（见 UpdateLineRenderers）
    ///   - GL 仅保留控制范围圆绘制
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

        GUILayout.BeginArea(new Rect(x, y, 200, 120));
        GUILayout.Label($"Ability: {(isAbilityActive ? "ON" : "OFF")}");
        if (boundProp != null)
        {
            GUILayout.Label($"Target: {boundProp.PropName}");
            GUILayout.Label($"State: {boundProp.GetControlState()}");
            GUILayout.Label($"Uses: {ControlsRemaining}");
        }
        GUILayout.Label($"Props in range: {propsInRangeCount}");
        if (energySystem != null)
        {
            GUILayout.Label($"Energy: {energySystem.CurrentEnergy:F0}/{energySystem.MaxEnergy:F0}");
        }
        GUILayout.EndArea();
    }

    #endregion
}
