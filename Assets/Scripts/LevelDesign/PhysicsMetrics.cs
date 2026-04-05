using UnityEngine;

/// <summary>
/// 物理度量常量中心 — 关卡设计系统的"物理真相源"
/// 
/// 核心理念（视碰分离 Visual-Collision Decoupling）：
///   白盒关卡的碰撞体尺寸就是物理真相，换素材时只替换视觉不调布局。
///   所有关卡元素的物理尺寸由本类统一定义，任何生成器/编辑器必须引用此处常量。
///
/// 业界参考：
///   - GDC "Building a Better Jump": 从设计师参数(高度+时间)推导重力和跳跃速度
///   - GMTK Platformer Toolkit: gravity = (-2*h)/(t²), jumpSpeed = sqrt(-2*g*h)
///   - Celeste (Maddy Thorson): 角色碰撞体 < 视觉sprite，10大容错机制
///   - Super Mario World: 1 block = 1 物理单位，d-distance/delta-height 度量体系
///   - DiGRA "You Say Jump I Say How High": 21特征跳跃模型，重力归一化
///   - Gamasutra 控制手感文章: 重力+空中控制联合调优
///
/// 度量体系（基于 MarioController 精确物理公式演算）：
///   - 重力加速度 (fallAcceleration): 80 units/s²
///   - 跳跃初速度 (jumpPower): 20 units/s
///   - 最大水平速度 (maxSpeed): 9 units/s
///   - 松开跳跃键重力倍率 (jumpEndEarlyGravityModifier): 3x
///   - Coyote Time: 0.15s
///
///   公式推导：
///     H_max = v² / (2g) = 20² / (2×80) = 2.5 格
///     t_peak = v / g = 20 / 80 = 0.25 秒
///     t_total = 2 × t_peak = 0.5 秒
///     D_max = maxSpeed × t_total = 9 × 0.5 = 4.5 格
///     H_min = v² / (2 × g × modifier) = 20² / (2×80×3) = 0.833 格
///     D_coyote = maxSpeed × coyoteTime = 9 × 0.15 = 1.35 格
///
/// Session 32: 视碰分离与关卡度量转译系统
/// </summary>
public static class PhysicsMetrics
{
    // ═══════════════════════════════════════════════════
    // 网格与PPU标准
    // ═══════════════════════════════════════════════════

    /// <summary>每个 ASCII 字符对应的世界单位大小（绝对锁死，不可修改）</summary>
    // [AI防坑警告] CELL_SIZE 是整个关卡系统的基石。
    // 所有碰撞体、所有生成坐标、所有物理计算都基于此值。
    // 修改此值会导致全部关卡布局崩溃。如需缩放关卡，请调整相机而非此值。
    public const float CELL_SIZE = 1f;

    /// <summary>
    /// 标准像素密度（Pixels Per Unit）
    /// 导入素材时必须将图片的 PPU 设为此值，确保 1 张图 = 1 个网格单位。
    /// 常见像素素材尺寸与对应 PPU：
    ///   16x16 像素 → PPU = 16
    ///   32x32 像素 → PPU = 32
    ///   48x48 像素 → PPU = 48
    /// </summary>
    public const int STANDARD_PPU_16 = 16;
    public const int STANDARD_PPU_32 = 32;

    // ═══════════════════════════════════════════════════
    // 角色碰撞体"黄金宽容比例"（The Generous Hitbox）
    // ═══════════════════════════════════════════════════

    // [AI防坑警告] 角色碰撞体必须小于视觉sprite！
    // 这是顶级平台跳跃手感的核心秘密（Celeste、Hollow Knight 均采用此设计）：
    // - 宽度 < 1：防止贴墙下落时卡在砖块接缝（浮点精度问题）
    // - 高度 < 1：防止起跳时头部微擦天花板导致跳跃被截断
    // - 视觉上"明明碰到了但物理上没判定"= 宽容感 = 爽快感
    //
    // 业界参考：
    //   Celeste: 玩家碰撞体比sprite小，尖刺碰撞体更小
    //   Reddit r/gamedesign: "In a single player game, typically you want to
    //     favour the player by having a larger Hitbox and smaller hurtbox."
    //   DiGRA论文: 碰撞体应独立于动画和视觉sprite

    /// <summary>Mario 碰撞体宽度（0.75~0.8 为业界黄金区间）</summary>
    public const float MARIO_COLLIDER_WIDTH = 0.8f;
    /// <summary>Mario 碰撞体高度（略小于1格，防头部擦顶）</summary>
    public const float MARIO_COLLIDER_HEIGHT = 0.95f;
    /// <summary>Mario 碰撞体 Y 轴偏移（锚点对齐底部）</summary>
    public const float MARIO_COLLIDER_OFFSET_Y = -0.025f;

    /// <summary>Trickster 碰撞体宽度</summary>
    public const float TRICKSTER_COLLIDER_WIDTH = 0.8f;
    /// <summary>Trickster 碰撞体高度</summary>
    public const float TRICKSTER_COLLIDER_HEIGHT = 0.95f;
    /// <summary>Trickster 碰撞体 Y 轴偏移</summary>
    public const float TRICKSTER_COLLIDER_OFFSET_Y = -0.025f;

    // ═══════════════════════════════════════════════════
    // 地形碰撞体标准尺寸
    // ═══════════════════════════════════════════════════

    /// <summary>地面/墙壁方块碰撞体（死死锁定 1x1，绝不修改）</summary>
    public static readonly Vector2 BLOCK_COLLIDER_SIZE = Vector2.one;

    /// <summary>地刺碰撞体（矮于1格，视觉上是尖刺，碰撞体更小=更宽容）</summary>
    // 业界参考：Celeste 尖刺碰撞体比视觉小，给玩家"差一点就碰到"的宽容感
    public static readonly Vector2 SPIKE_COLLIDER_SIZE = new Vector2(0.9f, 0.35f);
    /// <summary>地刺碰撞体 Y 偏移（底部对齐）</summary>
    public const float SPIKE_COLLIDER_OFFSET_Y = -0.325f;

    /// <summary>火焰陷阱碰撞体（比视觉小，提供容错）</summary>
    public static readonly Vector2 FIRE_COLLIDER_SIZE = new Vector2(0.6f, 0.6f);

    /// <summary>弹跳平台碰撞体（宽2格，矮，像弹簧）</summary>
    public static readonly Vector2 BOUNCY_COLLIDER_SIZE = new Vector2(1.8f, 0.3f);

    /// <summary>崩塌平台碰撞体</summary>
    public static readonly Vector2 COLLAPSE_COLLIDER_SIZE = new Vector2(1.8f, 0.4f);

    /// <summary>单向平台碰撞体</summary>
    public static readonly Vector2 ONEWAY_COLLIDER_SIZE = new Vector2(1.8f, 0.25f);

    /// <summary>移动平台碰撞体</summary>
    public static readonly Vector2 MOVING_COLLIDER_SIZE = new Vector2(2.5f, 0.4f);

    /// <summary>弹跳怪碰撞体</summary>
    public static readonly Vector2 BOUNCING_ENEMY_COLLIDER_SIZE = new Vector2(0.8f, 0.8f);

    /// <summary>简单敌人碰撞体</summary>
    public static readonly Vector2 SIMPLE_ENEMY_COLLIDER_SIZE = new Vector2(0.8f, 0.8f);

    /// <summary>收集物碰撞体</summary>
    public static readonly Vector2 COLLECTIBLE_COLLIDER_SIZE = new Vector2(0.5f, 0.5f);

    /// <summary>摆锤锚点碰撞体</summary>
    public static readonly Vector2 PENDULUM_COLLIDER_SIZE = new Vector2(0.3f, 0.3f);

    /// <summary>可操控危险物碰撞体</summary>
    public static readonly Vector2 CONTROLLABLE_HAZARD_COLLIDER_SIZE = new Vector2(1f, 0.5f);

    /// <summary>可破坏方块碰撞体</summary>
    public static readonly Vector2 BREAKABLE_BLOCK_COLLIDER_SIZE = Vector2.one;

    /// <summary>终点区域碰撞体</summary>
    public static readonly Vector2 GOAL_COLLIDER_SIZE = new Vector2(1f, 3f);

    // ═══════════════════════════════════════════════════
    // 跳跃能力极限（基于 MarioController 精确物理公式演算）
    // ═══════════════════════════════════════════════════

    // [AI防坑警告] 以下数值是从 MarioController 的物理参数用公式算出的真实极限。
    // 修改 MarioController 的 jumpPower/fallAcceleration/maxSpeed 后必须同步更新此处。
    // 公式:
    //   H_max = v² / (2g) = 20² / (2×80) = 2.5
    //   t_total = 2 × (v/g) = 2 × (20/80) = 0.5s
    //   D_max = maxSpeed × t_total = 9 × 0.5 = 4.5
    //   H_min = v² / (2 × g × modifier) = 20² / (2×80×3) = 0.833

    /// <summary>原地最高跳跃高度（格）— jumpPower²/(2*fallAcceleration) = 20²/(2*80) = 2.5</summary>
    public const float MAX_JUMP_HEIGHT = 2.5f;

    /// <summary>满速平跳最大水平距离（格）— maxSpeed * 2 * (jumpPower/fallAcceleration) = 9*0.5 = 4.5</summary>
    public const float MAX_JUMP_DISTANCE = 4.5f;

    /// <summary>短跳最低高度（格）— 立即松开跳跃键: jumpPower²/(2*fallAcceleration*modifier) = 0.833</summary>
    public const float MIN_JUMP_HEIGHT = 0.833f;

    /// <summary>Coyote Time 额外水平距离（格）— maxSpeed * coyoteTime = 9*0.15 = 1.35</summary>
    public const float COYOTE_BONUS_DISTANCE = 1.35f;

    /// <summary>含 Coyote Time 的最大可跨越间隙（格）— 4.5 + 1.35 = 5.85</summary>
    public const float MAX_GAP_WITH_COYOTE = 5.85f;

    // ═══════════════════════════════════════════════════
    // 半重力跳跃顶点参数（Celeste 风格增强）
    // ═══════════════════════════════════════════════════

    // 业界参考：Celeste 容错机制 #3 "Halved-Gravity Jump Peak"
    //   "If you hold the jump button, the top of your jump has half gravity applied.
    //    It's subtle, but this gives you more time to adjust for landing,
    //    and also just looks/feels pleasant." — Maddy Thorson
    //
    // 效果：跳跃顶点附近重力减半，给玩家更多空中调整时间
    // 额外滞空时间: 0.05秒，额外水平距离: 0.45格

    /// <summary>顶点区域速度阈值：当 |velocity.y| < 此值时视为跳跃顶点</summary>
    public const float APEX_VELOCITY_THRESHOLD = 2.0f;

    /// <summary>顶点区域重力倍率（0.5 = 半重力，与 Celeste 一致）</summary>
    public const float APEX_GRAVITY_MULTIPLIER = 0.5f;

    // ═══════════════════════════════════════════════════
    // 关卡设计安全约束（供 AI 提示词和编辑器验证使用）
    // ═══════════════════════════════════════════════════

    // 设计哲学（来自 Reverse Design: Super Mario World）：
    //   "每个间隙和高台都应该在物理上可跨越，并留有容错空间。
    //    极限跳跃只应出现在最高难度区域。"

    /// <summary>ASCII 模板中安全间隙上限（不含 Coyote，留容错）</summary>
    public const int ASCII_MAX_GAP = 4;

    /// <summary>ASCII 模板中安全高台上限（留容错）</summary>
    public const int ASCII_MAX_HEIGHT = 2;

    /// <summary>ASCII 模板中极限高台（需要精确操作）</summary>
    public const int ASCII_EXTREME_HEIGHT = 3;

    // ═══════════════════════════════════════════════════
    // 工具方法
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 根据难度百分比计算对应的间隙宽度（格数）
    /// </summary>
    /// <param name="difficultyPercent">难度百分比 (0~1)，1=极限</param>
    /// <returns>对应的间隙格数</returns>
    public static float GapForDifficulty(float difficultyPercent)
    {
        return Mathf.Lerp(1f, MAX_JUMP_DISTANCE - MARIO_COLLIDER_WIDTH, 
            Mathf.Clamp01(difficultyPercent));
    }

    /// <summary>
    /// 根据难度百分比计算对应的高台高度（格数）
    /// </summary>
    public static float HeightForDifficulty(float difficultyPercent)
    {
        return Mathf.Lerp(1f, MAX_JUMP_HEIGHT, Mathf.Clamp01(difficultyPercent));
    }

    /// <summary>
    /// 验证一个间隙是否在物理上可跨越
    /// </summary>
    /// <param name="gapWidth">间隙宽度（格数）</param>
    /// <param name="heightDifference">高度差（正=向上跳，负=向下跳）</param>
    /// <returns>是否可跨越</returns>
    public static bool IsGapTraversable(float gapWidth, float heightDifference = 0f)
    {
        if (heightDifference > MAX_JUMP_HEIGHT) return false;
        if (heightDifference <= 0)
        {
            // 向下跳或平跳：滞空时间更长，可跨越更远
            return gapWidth <= MAX_GAP_WITH_COYOTE;
        }
        // 向上跳：需要同时满足高度和距离
        float availableHeight = MAX_JUMP_HEIGHT - heightDifference;
        if (availableHeight < 0) return false;
        // 简化计算：高度占比越大，可用水平距离越短
        float heightRatio = heightDifference / MAX_JUMP_HEIGHT;
        float availableDistance = MAX_JUMP_DISTANCE * (1f - heightRatio * 0.5f);
        return gapWidth <= availableDistance;
    }

    // ═══════════════════════════════════════════════════
    // 扩展指南：如何添加新元素类型
    // ═══════════════════════════════════════════════════
    //
    // 步骤 1: 在本文件添加新元素的碰撞体常量
    //   public static readonly Vector2 NEW_ELEMENT_COLLIDER_SIZE = new Vector2(w, h);
    //
    // 步骤 2: 在 AsciiLevelGenerator.cs 中添加新的 Spawn 方法
    //   - 在 InitCharMap() 中注册新字符映射
    //   - 创建 SpawnNewElement(int gridX, int gridY) 方法
    //   - 碰撞体必须引用: col.size = PhysicsMetrics.NEW_ELEMENT_COLLIDER_SIZE;
    //
    // 步骤 3: 在 AsciiLevelValidator.cs 中更新字符分类
    //   - 实体元素加入 solidChars
    //   - 空气元素加入 airChars
    //
    // 步骤 4: 在 LevelThemeProfile.cs 中添加主题插槽
    //   - 在 elementSprites 数组中添加新的 ElementSpriteMapping
    //   - key 必须与 Spawn 方法生成的 GameObject 名称前缀一致
    //
    // 步骤 5: 在 AI_PROMPT_WORKFLOW.md 中更新 ASCII 字符表
    //   - 添加新字符的含义和使用示例
    //
    // 注意事项：
    //   - 碰撞体尺寸必须在本文件定义，禁止在 Spawn 方法中硬编码
    //   - 危险物碰撞体应小于视觉（宽容感）
    //   - 平台类碰撞体宽度应 >= 1.5格（给玩家落脚空间）
    //   - 新增的跳跃能力修改必须同步更新本文件的跳跃极限常量
    //
}
