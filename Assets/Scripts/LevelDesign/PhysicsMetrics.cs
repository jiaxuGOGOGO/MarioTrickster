using UnityEngine;

/// <summary>
/// 物理度量中心 — 关卡设计系统的"唯一真理源" (Single Source of Truth)
/// 
/// S53 Facade 改造：跳跃极限值从静态常量升级为动态 Facade 属性。
/// 当项目中存在 PhysicsConfigSO 资产时，所有跳跃极限值自动从 SO 的当前参数实时推导。
/// 当 SO 不存在时，回退到 S32 原始硬编码默认值（零行为变化）。
///
/// 效果：当你在 Inspector 中拖动"跳跃力滑块"时——
///   不仅手感变了，整个项目的验证器、报错红线、Scene 视图里的辅助画线，
///   会瞬间、全部自动对齐到新的跨度标准。
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
/// Session 32: 视碰分离与关卡度量转译系统
/// Session 53: PhysicsMetrics Facade — 动态读取 PhysicsConfigSO 推导值
/// </summary>
public static class PhysicsMetrics
{
    // ═══════════════════════════════════════════════════
    // S53: PhysicsConfigSO 动态绑定（Facade 核心）
    // ═══════════════════════════════════════════════════

    // [AI防坑警告] _activeConfig 是整个 Facade 的数据源。
    // 所有跳跃极限属性优先从此 SO 实时推导，为 null 时回退到硬编码默认值。
    // 加载策略与 MarioController 一致：Resources.Load<PhysicsConfigSO>("PhysicsConfig")。
    // 不要手动赋值 _activeConfig，使用 ActiveConfig 属性自动懒加载。
    private static PhysicsConfigSO _activeConfig;
    private static bool _configSearched;

    /// <summary>
    /// S53: 当前活跃的 PhysicsConfigSO 实例。
    /// 懒加载：首次访问时从 Resources 自动加载，找不到则为 null（回退默认值）。
    /// 编辑器中 SO 被修改时，推导值自动更新（因为每次访问都从 SO 实时计算）。
    /// </summary>
    public static PhysicsConfigSO ActiveConfig
    {
        get
        {
            if (!_configSearched)
            {
                _activeConfig = Resources.Load<PhysicsConfigSO>("PhysicsConfig");
                _configSearched = true;
            }
            return _activeConfig;
        }
    }

    /// <summary>
    /// S53: 手动注入 PhysicsConfigSO（供测试或特殊场景使用）。
    /// 注入后，所有跳跃极限值立即切换到该 SO 的推导值。
    /// 传入 null 可重置为自动加载模式。
    /// </summary>
    public static void SetActiveConfig(PhysicsConfigSO config)
    {
        _activeConfig = config;
        _configSearched = config != null;
    }

    /// <summary>
    /// S53: 强制重新搜索 PhysicsConfigSO（用于 SO 资产创建/删除后刷新）。
    /// </summary>
    public static void RefreshConfig()
    {
        _configSearched = false;
        _activeConfig = null;
    }

    // ═══════════════════════════════════════════════════
    // 网格与PPU标准（绝对不变）
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
    // 角色碰撞体"黄金宽容比例"（The Generous Hitbox）（绝对不变）
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
    // Visual 子节点 Y 偏移（视碰对齐 — 消除悬空）
    // ═══════════════════════════════════════════════════

    // [AI防坑警告] 当 Sprite Pivot = BottomCenter 时，Sprite 底边 = Visual.position.y。
    // 碰撞体底边 = Root.y + offset.y - size.y/2。两者差值就是 Visual 需要的 Y 偏移。
    // 如果不设置此偏移，角色会悬空 (size.y/2 - offset.y) ≈ 0.5 格。
    //
    // 公式: VISUAL_OFFSET_Y = offset.y - size.y / 2
    //   Mario:     -0.025 - 0.95/2 = -0.025 - 0.475 = -0.5
    //   Trickster:  -0.025 - 0.95/2 = -0.025 - 0.475 = -0.5
    //
    // 此偏移使 Sprite 底边精确对齐碰撞体底边，角色视觉贴地。

    /// <summary>Mario Visual 子节点 Y 偏移（使 BottomCenter Pivot 的 Sprite 底边对齐碰撞体底边）</summary>
    public const float MARIO_VISUAL_OFFSET_Y = MARIO_COLLIDER_OFFSET_Y - MARIO_COLLIDER_HEIGHT / 2f;
    /// <summary>Trickster Visual 子节点 Y 偏移</summary>
    public const float TRICKSTER_VISUAL_OFFSET_Y = TRICKSTER_COLLIDER_OFFSET_Y - TRICKSTER_COLLIDER_HEIGHT / 2f;

    // ═══════════════════════════════════════════════════
    // 地形碰撞体标准尺寸（绝对不变）
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

    /// <summary>弹跳平台碰撞体（1x1 网格对齐，确保 ASCII 连续 B 无缝拼接）</summary>
    /// S41 校准: 宽度从 1.8 → 1.0，配合 Root.localScale=(1,1,1) 后的真实物理尺寸
    public static readonly Vector2 BOUNCY_COLLIDER_SIZE = new Vector2(1.0f, 0.3f);

    /// <summary>崩塌平台碰撞体（1x1 网格对齐，确保 ASCII 连续 C 无缝拼接）</summary>
    /// S41 校准: 宽度从 1.8 → 1.0
    public static readonly Vector2 COLLAPSE_COLLIDER_SIZE = new Vector2(1.0f, 0.4f);

    /// <summary>单向平台碰撞体基准尺寸（单格宽度）</summary>
    /// S41 校准: 宽度从 1.8 → 1.0
    /// S44c: 连续 '-' 现在会被合并为一个长条平台，实际碰撞体宽度 = width * ONEWAY_COLLIDER_SIZE.x
    public static readonly Vector2 ONEWAY_COLLIDER_SIZE = new Vector2(1.0f, 0.25f);

    /// <summary>移动平台碰撞体</summary>
    public static readonly Vector2 MOVING_COLLIDER_SIZE = new Vector2(2.5f, 0.4f);

    /// <summary>弹跳怪碰撞体</summary>
    public static readonly Vector2 BOUNCING_ENEMY_COLLIDER_SIZE = new Vector2(0.8f, 0.8f);

    /// <summary>简单敌人碰撞体</summary>
    public static readonly Vector2 SIMPLE_ENEMY_COLLIDER_SIZE = new Vector2(0.8f, 0.8f);

    /// <summary>收集物碰撞体（比视觉贴图略大一圈，磁吸手感顺滑爽快）</summary>
    /// S41 校准: 从 0.5 → 0.6，增强拾取体验
    public static readonly Vector2 COLLECTIBLE_COLLIDER_SIZE = new Vector2(0.6f, 0.6f);

    /// <summary>摆锤碰撞体（0.4 大小足够有威慑力，契合铁球视觉核心）</summary>
    /// S41 校准: 从 0.3 → 0.4，修复 S37 前双重缩放导致的 0.09 像素级杀伤区
    public static readonly Vector2 PENDULUM_COLLIDER_SIZE = new Vector2(0.4f, 0.4f);

    /// <summary>可操控危险物碰撞体</summary>
    public static readonly Vector2 CONTROLLABLE_HAZARD_COLLIDER_SIZE = new Vector2(1f, 0.5f);

    /// <summary>可破坏方块碰撞体</summary>
    public static readonly Vector2 BREAKABLE_BLOCK_COLLIDER_SIZE = Vector2.one;

    // ── S56 新增碰撞体常量 ──

    /// <summary>旋转锯片碰撞体（圆形近似，比视觉略小=宽容感）</summary>
    public static readonly Vector2 SAW_BLADE_COLLIDER_SIZE = new Vector2(0.7f, 0.7f);

    /// <summary>飞行敌人碰撞体（与弹跳怪一致）</summary>
    public static readonly Vector2 FLYING_ENEMY_COLLIDER_SIZE = new Vector2(0.8f, 0.8f);

    /// <summary>传送带碰撞体（1x1 网格对齐，确保 ASCII 连续字符无缝拼接）</summary>
    public static readonly Vector2 CONVEYOR_BELT_COLLIDER_SIZE = new Vector2(1.0f, 0.3f);

    /// <summary>检查点碰撞体（Trigger，比视觉稍大便于触发）</summary>
    public static readonly Vector2 CHECKPOINT_COLLIDER_SIZE = new Vector2(0.8f, 1.2f);

    /// <summary>终点区域碰撞体</summary>
    public static readonly Vector2 GOAL_COLLIDER_SIZE = new Vector2(1f, 3f);

    // ═══════════════════════════════════════════════════
    // 跳跃能力极限 — S53 Facade: 动态读取 PhysicsConfigSO
    // ═══════════════════════════════════════════════════

    // [AI防坑警告] S53 改造：以下值从 const 升级为 static property (Facade)。
    // 当 ActiveConfig (PhysicsConfigSO) 存在时，实时从 SO 参数推导。
    // 当 ActiveConfig 为 null 时，回退到 S32 原始硬编码默认值。
    // 所有消费者（验证器、可视化器、报错信息）无需改代码，自动获得动态值。
    //
    // 回退默认值与 S32 原始 const 完全一致：
    //   MAX_JUMP_HEIGHT = 2.5f
    //   MAX_JUMP_DISTANCE = 4.5f
    //   MIN_JUMP_HEIGHT = 0.833f
    //   COYOTE_BONUS_DISTANCE = 1.35f
    //   MAX_GAP_WITH_COYOTE = 5.85f

    /// <summary>原地最高跳跃高度（格）— jumpPower²/(2*fallAcceleration)</summary>
    public static float MAX_JUMP_HEIGHT
    {
        get
        {
            var cfg = ActiveConfig;
            return cfg != null ? cfg.DerivedMaxJumpHeight : DEFAULT_MAX_JUMP_HEIGHT;
        }
    }

    /// <summary>满速平跳最大水平距离（格）— maxSpeed * 2 * (jumpPower/fallAcceleration)</summary>
    public static float MAX_JUMP_DISTANCE
    {
        get
        {
            var cfg = ActiveConfig;
            return cfg != null ? cfg.DerivedMaxJumpDistance : DEFAULT_MAX_JUMP_DISTANCE;
        }
    }

    /// <summary>短跳最低高度（格）— jumpPower²/(2*fallAcceleration*jumpEndEarlyGravityModifier)</summary>
    public static float MIN_JUMP_HEIGHT
    {
        get
        {
            var cfg = ActiveConfig;
            return cfg != null ? cfg.DerivedMinJumpHeight : DEFAULT_MIN_JUMP_HEIGHT;
        }
    }

    /// <summary>Coyote Time 额外水平距离（格）— maxSpeed * coyoteTime</summary>
    public static float COYOTE_BONUS_DISTANCE
    {
        get
        {
            var cfg = ActiveConfig;
            return cfg != null ? cfg.DerivedCoyoteBonusDistance : DEFAULT_COYOTE_BONUS_DISTANCE;
        }
    }

    /// <summary>含 Coyote Time 的最大可跨越间隙（格）— MAX_JUMP_DISTANCE + COYOTE_BONUS_DISTANCE</summary>
    public static float MAX_GAP_WITH_COYOTE
    {
        get
        {
            return MAX_JUMP_DISTANCE + COYOTE_BONUS_DISTANCE;
        }
    }

    // ── S53: 回退默认值（与 S32 原始 const 完全一致）──
    private const float DEFAULT_MAX_JUMP_HEIGHT = 2.5f;
    private const float DEFAULT_MAX_JUMP_DISTANCE = 4.5f;
    private const float DEFAULT_MIN_JUMP_HEIGHT = 0.833f;
    private const float DEFAULT_COYOTE_BONUS_DISTANCE = 1.35f;

    // ═══════════════════════════════════════════════════
    // 半重力跳跃顶点参数 — S53 Facade: 动态读取 PhysicsConfigSO
    // ═══════════════════════════════════════════════════

    // 业界参考：Celeste 容错机制 #3 "Halved-Gravity Jump Peak"
    //   "If you hold the jump button, the top of your jump has half gravity applied.
    //    It's subtle, but this gives you more time to adjust for landing,
    //    and also just looks/feels pleasant." — Maddy Thorson
    //
    // 效果：跳跃顶点附近重力减半，给玩家更多空中调整时间
    // 额外滞空时间: 0.05秒，额外水平距离: 0.45格

    /// <summary>顶点区域速度阈值：当 |velocity.y| < 此值时视为跳跃顶点</summary>
    public static float APEX_VELOCITY_THRESHOLD
    {
        get
        {
            var cfg = ActiveConfig;
            return cfg != null ? cfg.apexThreshold : 2.0f;
        }
    }

    /// <summary>顶点区域重力倍率（0.5 = 半重力，与 Celeste 一致）</summary>
    public static float APEX_GRAVITY_MULTIPLIER
    {
        get
        {
            var cfg = ActiveConfig;
            return cfg != null ? cfg.apexGravityMultiplier : 0.5f;
        }
    }

    // ═══════════════════════════════════════════════════
    // 关卡设计安全约束（供 AI 提示词和编辑器验证使用）（绝对不变）
    // ═══════════════════════════════════════════════════

    // [AI防坑警告] 以下安全约束是设计层面的硬限制，不随物理参数变化。
    // 它们定义了"安全关卡"的标准，即使物理参数变了，
    // 关卡设计的安全间隙/高台上限仍由这些常量控制。
    // 验证器会用动态的 MAX_JUMP_HEIGHT/MAX_JUMP_DISTANCE 做物理可达性判断，
    // 但 ASCII_MAX_GAP/ASCII_MAX_HEIGHT 是设计约束，保持 const。

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
    // 关卡布局安全约束（S35 新增，供验证器和模板设计使用）（绝对不变）
    // ═══════════════════════════════════════════════════

    // 业界参考：
    //   - Dylan Wolf "Building a Procedurally Generated Platformer":
    //     陷阱只在满足空间约束时放置，spike 上方必须有空tile
    //   - sgalban/platformer-gen-2D (Rhythm-Based Level Generation):
    //     每个节奏组之间有 rest area，敌人不在 < 3格宽平台上生成
    //   - Celeste (Maddy Thorson, GDC 2017 "Designing Celeste"):
    //     危险前总有安全着陆区，第一个坑前有安全跳台学习物理
    //   - Super Mario World (Reverse Design):
    //     弹跳垫上方必须留出最大弹跳高度的安全空间

    /// <summary>
    /// 出生点安全半径（格）— M/T 之间最小间距，且半径内不得有危险物。
    /// 业界参考: Celeste 每个 checkpoint 周围无危险物，给玩家安全起步空间。
    /// </summary>
    public const int SPAWN_SAFE_RADIUS = 2;

    /// <summary>
    /// 弹跳平台上方安全净空（格）— B 字符上方此范围内不得有危险物(^~P)。
    /// 推导: 弹跳平台弹射高度 >= MAX_JUMP_HEIGHT(2.5格)，向上取整+缓冲 = 3格。
    /// 业界参考: SMW 弹跳垫上方始终留出完整弹跳弧线空间。
    /// </summary>
    public const int BOUNCE_CLEARANCE = 3;

    /// <summary>
    /// 陷阱着陆缓冲（格）— 平台边缘到相邻陷阱之间至少留此间距。
    /// 业界参考: Dylan Wolf 的程序化生成中，陷阱必须指向至少一个空tile。
    /// </summary>
    public const int HAZARD_LANDING_BUFFER = 1;

    // ═══════════════════════════════════════════════════
    // 工具方法 — S53: 已升级为动态值
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 根据难度百分比计算对应的间隙宽度（格数）
    /// S53: 现在使用动态 MAX_JUMP_DISTANCE，随 SO 参数变化。
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
    /// S53: 现在使用动态 MAX_JUMP_HEIGHT，随 SO 参数变化。
    /// </summary>
    public static float HeightForDifficulty(float difficultyPercent)
    {
        return Mathf.Lerp(1f, MAX_JUMP_HEIGHT, Mathf.Clamp01(difficultyPercent));
    }

    /// <summary>
    /// 验证一个间隙是否在物理上可跨越
    /// S53: 现在使用动态 MAX_JUMP_HEIGHT/MAX_GAP_WITH_COYOTE/MAX_JUMP_DISTANCE，随 SO 参数变化。
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
    // 步骤 2: 在 AsciiElementRegistry.cs 的默认实例中添加新元素条目 (S46)
    //   - 在 CreateDefaultInstance() 的 entries 数组中添加 AsciiElementEntry
    //   - 设置 asciiChar, elementName, isSolid, isHazard, jumpBoost
    //   - 或者在 Inspector 中编辑 AsciiElementRegistry 资产
    //
    // 步骤 3: 在 AsciiLevelGenerator.cs 中添加新的 Spawn 方法
    //   - 在 InitSpawnMap() 中注册: spawnMap["NewElement"] = SpawnNewElement;
    //   - 创建 SpawnNewElement(int gridX, int gridY) 方法
    //   - 碰撞体必须引用: col.size = PhysicsMetrics.NEW_ELEMENT_COLLIDER_SIZE;
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
    //   - 平台类碰撞体宽度应 = 1.0格（1x1 网格对齐，确保 ASCII 连续字符无缝拼接）
    //   - 移动平台等独立实体不拼接，可使用更大宽度
    //   - S53 后：修改 PhysicsConfigSO 的跳跃/重力参数，本文件的跳跃极限会自动同步
    //     不再需要手动更新 MAX_JUMP_HEIGHT 等值！
    //
}
