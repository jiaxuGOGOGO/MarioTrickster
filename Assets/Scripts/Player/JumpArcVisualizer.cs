using UnityEngine;

/// <summary>
/// 跳跃抛物线可视化器 — 关卡设计师的秘密武器
/// 
/// 挂在 Mario 身上，在 Scene 视图中用 OnDrawGizmos 绘制多条半透明抛物线：
///   1. 绿色：原地最高跳轨迹（垂直极限，含半重力顶点）
///   2. 蓝色：极限远跳轨迹（水平极限，满速平跳，含半重力顶点）
///   3. 黄色：短跳弧线（立即松开跳跃键）
///   4. 红色虚线：极限标注线（最大高度、最大距离）
///   5. 白色网格：格子刻度线
///
/// S53 改造：参数读取优先级
///   1. PhysicsConfigSO（通过 PhysicsMetrics.ActiveConfig）— 编辑器实时调参时自动同步
///   2. MarioController（通过 SerializedObject 反射）— 兼容无 SO 场景
///   3. 本地 SerializeField 默认值 — 最终兜底
///
/// 使用方法：
///   1. 在 Scene 视图里把 Mario 拖到悬崖边
///   2. 看身前的抛物线有没有够到对面平台
///   3. 够不到？回文本框删一个 '.'
///   4. 超太多？回文本框加一个 '.'
///   不需要按 Play 运行游戏，Scene 视图实时预览！
///
/// 物理公式：
///   x(t) = v_x * t
///   y(t) = v_y * t - 0.5 * g * t²
///   其中 v_y = jumpPower, g = fallAcceleration, v_x = maxSpeed
///   顶点区域（|v_y| < apexThreshold）: g *= apexGravityMultiplier
///
/// 业界参考：
///   - 任天堂关卡设计师使用类似工具在编辑器中预览跳跃弧线
///   - GDC "Building a Better Jump": 从设计参数推导物理曲线
///   - GMTK Platformer Toolkit: gravity = (-2*h)/(t²)
///   - Celeste: 半重力跳跃顶点让弧线更平缓
///
/// Session 32: 视碰分离与关卡度量转译系统
/// Session 53: PhysicsConfigSO 实时监听 + PhysicsMetrics Facade 联动
/// </summary>
[ExecuteInEditMode]
public class JumpArcVisualizer : MonoBehaviour
{
    [Header("=== 显示控制 ===")]
    [Tooltip("是否在 Scene 视图中显示跳跃弧线")]
    [SerializeField] private bool showArcs = true;

    [Tooltip("是否显示网格刻度线")]
    [SerializeField] private bool showGrid = true;

    [Tooltip("抛物线采样点数（越多越平滑）")]
    [SerializeField, Range(20, 100)] private int arcResolution = 60;

    [Header("=== 物理参数（S53: 自动从 PhysicsConfigSO 或 MarioController 读取）===")]
    [Tooltip("跳跃初速度（优先从 PhysicsConfigSO 读取）")]
    [SerializeField] private float jumpPower = 20f;

    [Tooltip("重力加速度")]
    [SerializeField] private float gravity = 80f;

    [Tooltip("最大水平速度")]
    [SerializeField] private float maxSpeed = 9f;

    [Tooltip("提前松开跳跃键的重力倍率")]
    [SerializeField] private float jumpEndEarlyGravityModifier = 3f;

    [Tooltip("半重力顶点速度阈值")]
    [SerializeField] private float apexThreshold = 2.0f;

    [Tooltip("半重力顶点重力倍率")]
    [SerializeField] private float apexGravityMultiplier = 0.5f;

    [Header("=== 颜色设置 ===")]
    [SerializeField] private Color verticalArcColor = new Color(0.2f, 0.9f, 0.3f, 0.7f);
    [SerializeField] private Color horizontalArcColor = new Color(0.3f, 0.5f, 1.0f, 0.7f);
    [SerializeField] private Color shortJumpArcColor = new Color(1.0f, 0.8f, 0.2f, 0.5f);
    [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private Color limitLineColor = new Color(1f, 0.3f, 0.3f, 0.4f);

    // 缓存
    private MarioController cachedMario;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 尝试同步参数
        SyncPhysicsParams();
    }

    private void OnDrawGizmos()
    {
        if (!showArcs) return;

        SyncPhysicsParams();

        Vector3 origin = transform.position;
        // 碰撞体底部中心作为起跳点
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            origin.y -= col.size.y * 0.5f - col.offset.y;
        }

        // 1. 原地最高跳（绿色）— 纯垂直，含半重力顶点
        DrawVerticalJumpArc(origin);

        // 2. 极限远跳-右（蓝色）— 满速向右，含半重力顶点
        DrawHorizontalJumpArc(origin, 1f);

        // 3. 极限远跳-左（蓝色，镜像）
        DrawHorizontalJumpArc(origin, -1f);

        // 4. 短跳弧线（黄色）— 立即松开跳跃键
        DrawShortJumpArc(origin, 1f);
        DrawShortJumpArc(origin, -1f);

        // 5. 网格刻度
        if (showGrid)
        {
            DrawGridOverlay(origin);
        }

        // 6. 极限标注
        DrawLimitAnnotations(origin);
    }

    /// <summary>
    /// 模拟一帧的重力，考虑半重力顶点效果。
    /// 这是精确模拟 MarioController.HandleGravity 的逻辑。
    /// </summary>
    /// <param name="vy">当前垂直速度</param>
    /// <param name="dt">时间步长</param>
    /// <param name="endedEarly">是否提前松开跳跃键</param>
    /// <returns>更新后的垂直速度</returns>
    private float SimulateGravityStep(float vy, float dt, bool endedEarly)
    {
        float g = gravity;

        if (endedEarly && vy > 0)
        {
            g *= jumpEndEarlyGravityModifier;
        }
        else if (!endedEarly && Mathf.Abs(vy) < apexThreshold)
        {
            // 半重力顶点：长按跳跃键 + 接近顶点
            g *= apexGravityMultiplier;
        }

        return Mathf.MoveTowards(vy, -100f, g * dt);
    }

    /// <summary>绘制原地最高跳弧线（含半重力顶点）</summary>
    private void DrawVerticalJumpArc(Vector3 origin)
    {
        Gizmos.color = verticalArcColor;

        float dt = 0.01f; // 高精度时间步长
        float vy = jumpPower;
        float y = 0f;
        Vector3 prev = origin;

        for (int i = 0; i < 500; i++) // 最多5秒
        {
            vy = SimulateGravityStep(vy, dt, false);
            y += vy * dt;

            if (y < 0) break;

            Vector3 point = origin + new Vector3(0, y, 0);
            Gizmos.DrawLine(prev, point);
            prev = point;
        }

        // 顶点标记
        float peakY = jumpPower * jumpPower / (2f * gravity);
        Vector3 peak = origin + new Vector3(0, peakY, 0);
        Gizmos.DrawWireSphere(peak, 0.1f);
    }

    /// <summary>绘制极限远跳弧线（含半重力顶点）</summary>
    private void DrawHorizontalJumpArc(Vector3 origin, float direction)
    {
        Gizmos.color = horizontalArcColor;

        float dt = 0.01f;
        float vy = jumpPower;
        float x = 0f;
        float y = 0f;
        Vector3 prev = origin;

        for (int i = 0; i < 500; i++)
        {
            vy = SimulateGravityStep(vy, dt, false);
            x += maxSpeed * dt * direction;
            y += vy * dt;

            if (y < -0.1f) break;

            Vector3 point = origin + new Vector3(x, y, 0);
            Gizmos.DrawLine(prev, point);
            prev = point;
        }

        // 落点标记（精确计算落地位置）
        Vector3 landPoint = prev;
        landPoint.y = origin.y;
        Gizmos.DrawWireSphere(landPoint, 0.12f);
    }

    /// <summary>绘制短跳弧线（立即松开跳跃键）</summary>
    private void DrawShortJumpArc(Vector3 origin, float direction)
    {
        Gizmos.color = shortJumpArcColor;

        float dt = 0.01f;
        float vy = jumpPower;
        float x = 0f;
        float y = 0f;
        Vector3 prev = origin;

        for (int i = 0; i < 500; i++)
        {
            vy = SimulateGravityStep(vy, dt, true); // endedEarly = true
            x += maxSpeed * dt * direction;
            y += vy * dt;

            if (y < -0.1f) break;

            Vector3 point = origin + new Vector3(x, y, 0);
            Gizmos.DrawLine(prev, point);
            prev = point;
        }
    }

    /// <summary>绘制网格刻度线 — S53: 使用 PhysicsMetrics 动态值</summary>
    private void DrawGridOverlay(Vector3 origin)
    {
        Gizmos.color = gridColor;
        // S53: 从 PhysicsMetrics Facade 读取动态值（随 SO 参数实时变化）
        float maxH = PhysicsMetrics.MAX_JUMP_HEIGHT + 1f;
        float maxD = PhysicsMetrics.MAX_JUMP_DISTANCE + 1f;

        // 水平刻度线
        for (int y = 0; y <= Mathf.CeilToInt(maxH); y++)
        {
            Vector3 left = origin + new Vector3(-maxD, y, 0);
            Vector3 right = origin + new Vector3(maxD, y, 0);
            Gizmos.DrawLine(left, right);
        }

        // 垂直刻度线
        for (int x = -(int)maxD; x <= (int)maxD; x++)
        {
            Vector3 bottom = origin + new Vector3(x, -0.5f, 0);
            Vector3 top = origin + new Vector3(x, maxH, 0);
            Gizmos.DrawLine(bottom, top);
        }
    }

    /// <summary>绘制极限标注线 — S53: 使用 PhysicsMetrics 动态值</summary>
    private void DrawLimitAnnotations(Vector3 origin)
    {
        // S53: 从 PhysicsMetrics Facade 读取动态值
        // 最大高度水平线
        Gizmos.color = limitLineColor;
        float maxH = PhysicsMetrics.MAX_JUMP_HEIGHT;
        Vector3 hLeft = origin + new Vector3(-6f, maxH, 0);
        Vector3 hRight = origin + new Vector3(6f, maxH, 0);
        Gizmos.DrawLine(hLeft, hRight);

        // 最大距离垂直线（右侧）
        float maxD = PhysicsMetrics.MAX_JUMP_DISTANCE;
        Vector3 dBottom = origin + new Vector3(maxD, -1f, 0);
        Vector3 dTop = origin + new Vector3(maxD, maxH + 1f, 0);
        Gizmos.DrawLine(dBottom, dTop);

        // 最大距离垂直线（左侧）
        Vector3 dBottomL = origin + new Vector3(-maxD, -1f, 0);
        Vector3 dTopL = origin + new Vector3(-maxD, maxH + 1f, 0);
        Gizmos.DrawLine(dBottomL, dTopL);

#if UNITY_EDITOR
        // 文字标注 — S53: 使用 PhysicsMetrics 动态值
        UnityEditor.Handles.color = verticalArcColor;
        UnityEditor.Handles.Label(origin + new Vector3(0.2f, maxH + 0.2f, 0),
            $"Max Height: {maxH:F1} units");

        UnityEditor.Handles.color = horizontalArcColor;
        UnityEditor.Handles.Label(origin + new Vector3(maxD + 0.2f, 0.2f, 0),
            $"Max Dist: {maxD:F1} units");

        UnityEditor.Handles.color = shortJumpArcColor;
        float minH = PhysicsMetrics.MIN_JUMP_HEIGHT;
        UnityEditor.Handles.Label(origin + new Vector3(0.2f, minH + 0.1f, 0),
            $"Min Height: {minH:F1} units");

        // 半重力顶点区域标注
        UnityEditor.Handles.color = new Color(0.8f, 0.4f, 1f, 0.6f);
        UnityEditor.Handles.Label(origin + new Vector3(-3f, maxH - 0.3f, 0),
            $"Apex Zone: |vy| < {apexThreshold:F1}, gravity x {apexGravityMultiplier:F1}");

        // S53: 数据源指示器 — 让设计师知道当前参数来自哪里
        string source = PhysicsMetrics.ActiveConfig != null ? "PhysicsConfigSO" : "Default";
        UnityEditor.Handles.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        UnityEditor.Handles.Label(origin + new Vector3(-3f, -0.8f, 0),
            $"[Source: {source}]");
#endif
    }

    /// <summary>
    /// S53: 统一参数同步方法。
    /// 优先级：PhysicsConfigSO > MarioController > 本地默认值。
    /// 
    /// 当 PhysicsConfigSO 存在时，直接从 SO 读取所有手感参数，
    /// 确保 Scene 视图中的弧线与运行时的实际物理完全一致。
    /// 当 SO 不存在时，回退到从 MarioController 反射读取（兼容旧流程）。
    /// </summary>
    private void SyncPhysicsParams()
    {
        // S53: 优先从 PhysicsConfigSO 读取
        PhysicsConfigSO config = PhysicsMetrics.ActiveConfig;
        if (config != null)
        {
            jumpPower = config.jumpPower;
            gravity = config.fallAcceleration;
            maxSpeed = config.maxSpeed;
            jumpEndEarlyGravityModifier = config.jumpEndEarlyGravityModifier;
            apexThreshold = config.apexThreshold;
            apexGravityMultiplier = config.apexGravityMultiplier;
            return;
        }

        // 回退：从 MarioController 同步参数
        SyncFromMarioController();
    }

    /// <summary>尝试从同物体的 MarioController 同步物理参数（兼容无 SO 场景）</summary>
    private void SyncFromMarioController()
    {
        if (cachedMario == null)
            cachedMario = GetComponent<MarioController>();

        if (cachedMario == null) return;

        // 通过 SerializedObject 读取私有字段（仅编辑器）
#if UNITY_EDITOR
        var so = new UnityEditor.SerializedObject(cachedMario);
        var jpProp = so.FindProperty("jumpPower");
        var faProp = so.FindProperty("fallAcceleration");
        var msProp = so.FindProperty("maxSpeed");
        var jeProp = so.FindProperty("jumpEndEarlyGravityModifier");
        var atProp = so.FindProperty("apexThreshold");
        var agProp = so.FindProperty("apexGravityMultiplier");

        if (jpProp != null) jumpPower = jpProp.floatValue;
        if (faProp != null) gravity = faProp.floatValue;
        if (msProp != null) maxSpeed = msProp.floatValue;
        if (jeProp != null) jumpEndEarlyGravityModifier = jeProp.floatValue;
        if (atProp != null) apexThreshold = atProp.floatValue;
        if (agProp != null) apexGravityMultiplier = agProp.floatValue;
#endif
    }
#endif
}
