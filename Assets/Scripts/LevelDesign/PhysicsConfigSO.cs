using UnityEngine;

// ═══════════════════════════════════════════════════════════════════
// PhysicsConfigSO — ScriptableObject 实时手感调参面板 (S52)
//
// [AI防坑警告] 此文件是 S52 手感调优基建的核心。
// 它将 MarioController 中硬编码的移动/跳跃/重力参数提取为 ScriptableObject，
// 允许在 PlayMode 运行时通过 Inspector 拖动滑块实时调优手感，无需重新编译。
//
// 使用方式：
//   1. 在 Assets/Resources/ 下创建 PhysicsConfig 资产：
//      右键 → Create → MarioTrickster → Physics Config
//   2. MarioController 的 Inspector 中拖入该资产到 physicsConfig 字段
//   3. 运行游戏，在 Inspector 中实时拖动滑块调整手感
//   4. 运行时修改会立即生效（每帧从 SO 读取最新值）
//   5. 退出 PlayMode 后修改会保留（ScriptableObject 是持久化资产）
//
// 设计原则：
//   - 所有 [Range] 区间基于业界经验和当前项目的物理公式推导
//   - 默认值与 MarioController 原有硬编码值完全一致（零行为变化）
//   - MarioController 优先使用 SO 值，SO 为 null 时回退到本地 SerializeField
//   - PhysicsMetrics.cs 中的碰撞体尺寸和关卡设计常量不在此处（它们是布局真相，不可运行时改）
//
// 架构关系：
//   PhysicsConfigSO（数据） → MarioController（消费者，每帧读取）
//   PhysicsMetrics（碰撞体/关卡常量） → 不变，与本文件无关
//
// 业界参考：
//   - Unity Official: "Separate Game Data and Logic with ScriptableObjects"
//   - Reddit r/gamedev: "put game feel params in a ScriptableObject and edit while playing"
//   - Tarodev Ultimate-2D-Controller: 所有手感参数集中在 PlayerStats SO
//   - Celeste (Maddy Thorson): 频繁迭代跳跃手感，需要快速调参工具
// ═══════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "PhysicsConfig", menuName = "MarioTrickster/Physics Config")]
public class PhysicsConfigSO : ScriptableObject
{
    // ═══════════════════════════════════════════════════
    // 移动参数
    // ═══════════════════════════════════════════════════

    [Header("移动 (Movement)")]

    [Tooltip("最大水平速度 (units/s)")]
    [Range(3f, 20f)]
    public float maxSpeed = 9f;

    [Tooltip("地面加速度 — 越大起步越快")]
    [Range(40f, 400f)]
    public float acceleration = 160f;

    [Tooltip("地面减速度 — 越大停止越果断，调大可消除打滑感")]
    [Range(40f, 400f)]
    public float groundDeceleration = 200f;

    [Tooltip("空中减速度 — 松开输入后的空中滑行阻力，建议保持较小")]
    [Range(5f, 100f)]
    public float airDeceleration = 30f;

    [Tooltip("落地时施加的微小向下力，防止在斜面上抖动")]
    [Range(-5f, 0f)]
    public float groundingForce = -1.5f;

    // ═══════════════════════════════════════════════════
    // 跳跃参数
    // ═══════════════════════════════════════════════════

    [Header("跳跃 (Jump)")]

    [Tooltip("跳跃初速度 (units/s) — H_max = v²/(2g)")]
    [Range(8f, 35f)]
    public float jumpPower = 20f;

    [Tooltip("最大下落速度 (units/s)")]
    [Range(15f, 80f)]
    public float maxFallSpeed = 40f;

    [Tooltip("下落重力加速度 (units/s²) — 越大越快坠落")]
    [Range(30f, 200f)]
    public float fallAcceleration = 80f;

    [Tooltip("提前松开跳跃键时的重力倍率 — 越大短跳弧度越短")]
    [Range(1f, 8f)]
    public float jumpEndEarlyGravityModifier = 3f;

    [Tooltip("Coyote Time: 离开平台边缘后仍可跳跃的宽限时间 (秒)")]
    [Range(0f, 0.3f)]
    public float coyoteTime = 0.15f;

    [Tooltip("Jump Buffer: 落地前提前按跳跃的缓冲时间 (秒)")]
    [Range(0f, 0.4f)]
    public float jumpBuffer = 0.2f;

    // ═══════════════════════════════════════════════════
    // 半重力跳跃顶点 (Celeste 风格)
    // ═══════════════════════════════════════════════════

    [Header("半重力跳跃顶点 (Apex, Celeste-Style)")]

    [Tooltip("跳跃顶点附近的速度阈值 — |velocity.y| < 此值时视为顶点区")]
    [Range(0.5f, 5f)]
    public float apexThreshold = 2.0f;

    [Tooltip("顶点区域重力倍率 — 0.5 = 半重力（Celeste 风格）")]
    [Range(0.1f, 1f)]
    public float apexGravityMultiplier = 0.5f;

    // ═══════════════════════════════════════════════════
    // 弹射动能保留 (Bounce)
    // ═══════════════════════════════════════════════════

    [Header("弹射动能保留 (Bounce Momentum)")]

    [Tooltip("抛物线飞行期空气阻力 — 无输入时 X 轴速度每秒衰减量")]
    [Range(0f, 30f)]
    public float airFriction = 8f;

    [Tooltip("抛物线飞行期空中转向加速度 — 有输入时的微弱偏转力")]
    [Range(0f, 30f)]
    public float bounceAirAcceleration = 12f;

    // ═══════════════════════════════════════════════════
    // 击退参数
    // ═══════════════════════════════════════════════════

    [Header("击退 (Knockback)")]

    [Tooltip("受击后控制器暂停时长 (秒) — 让击退力生效")]
    [Range(0.05f, 1f)]
    public float knockbackStunDuration = 0.25f;

    // ═══════════════════════════════════════════════════
    // 地面检测
    // ═══════════════════════════════════════════════════

    [Header("地面检测 (Ground Detection)")]

    [Tooltip("地面检测射线长度")]
    [Range(0.01f, 0.2f)]
    public float grounderDistance = 0.05f;

    // ═══════════════════════════════════════════════════
    // 实时推导值（只读，供 Inspector 参考）
    // ═══════════════════════════════════════════════════

    /// <summary>当前参数下的最大跳跃高度 (格)</summary>
    public float DerivedMaxJumpHeight => jumpPower * jumpPower / (2f * fallAcceleration);

    /// <summary>当前参数下的满速平跳最大水平距离 (格)</summary>
    public float DerivedMaxJumpDistance => maxSpeed * 2f * (jumpPower / fallAcceleration);

    /// <summary>当前参数下的短跳最低高度 (格)</summary>
    public float DerivedMinJumpHeight => jumpPower * jumpPower / (2f * fallAcceleration * jumpEndEarlyGravityModifier);

    /// <summary>当前参数下的 Coyote Time 额外水平距离 (格)</summary>
    public float DerivedCoyoteBonusDistance => maxSpeed * coyoteTime;

#if UNITY_EDITOR
    // ═══════════════════════════════════════════════════
    // Editor: 在 Inspector 底部显示推导值
    // ═══════════════════════════════════════════════════

    private void OnValidate()
    {
        // OnValidate 在 Inspector 中修改值时调用
        // 推导值通过 Property 自动计算，无需手动更新
        // 这里可以添加参数合理性检查
        if (maxSpeed < 1f) maxSpeed = 1f;
        if (jumpPower < 1f) jumpPower = 1f;
        if (fallAcceleration < 1f) fallAcceleration = 1f;
    }
#endif
}
