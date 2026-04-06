using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ASCII 关卡模板生成器 — 基于 AsciiElementRegistry 的数据驱动字符解析系统
/// 
/// 核心设计:
///   - S46: 字符映射从 AsciiElementRegistry (ScriptableObject) 动态构建
///   - 新增元素只需在 Registry 中 Add 一条记录 + 添加对应 Spawn 方法
///   - 所有生成的元素默认使用白盒（纯白/灰色方块），方便先测试逻辑
///   - 内置 2 个经典平台跳跃关卡模板（平原关卡 + 地下关卡）
///   - 生成结果挂载在统一的 Root GameObject 下，方便一键清除
///
/// 字符映射表 (Character Map) — 由 AsciiElementRegistry 定义:
///   '#' = 实心地面方块 (Ground Block)
///   '=' = 平台方块 (Platform, 可站立)
///   '.' = 空气 (Air, 不生成任何东西)
///   'M' = Mario 出生点 (Spawn Point)
///   'T' = Trickster 出生点
///   'G' = 终点 (GoalZone)
///   '^' = 地刺 (SpikeTrap)
///   '~' = 火焰陷阱 (FireTrap)
///   'P' = 摆锤 (PendulumTrap, 锚点位置)
///   'B' = 弹跳平台 (BouncyPlatform)
///   'C' = 崩塌平台 (CollapsingPlatform)
///   '-' = 单向平台 (OneWayPlatform)
///   'E' = 弹跳怪 (BouncingEnemy)
///   'F' = 伪装墙 (FakeWall)
///   'H' = 隐藏通道入口 (HiddenPassage)
///   'o' = 收集物 (Collectible)
///   'e' = 简单敌人 (SimpleEnemy)
///   '>' = 移动平台 (MovingPlatform, 水平)
///   'W' = 墙壁方块 (Wall Block)
///
/// 扩展方式 (S46 Data-Driven):
///   1. 在 AsciiElementRegistry 资产中添加新字符条目
///   2. 在本文件 InitSpawnMap() 中注册: spawnMap["NewElement"] = SpawnNewElement;
///   3. 在本文件底部添加 SpawnNewElement(int gridX, int gridY) 方法
///   4. 在 PhysicsMetrics.cs 中定义碰撞体常量（如需要）
///   5. 在 LevelThemeProfile.cs 的 elementSprites 中添加主题插槽
///   6. 在 AI_PROMPT_WORKFLOW.md 中更新 ASCII 字符表
///
/// Session 25: 关卡工坊新增
/// Session 46: Data-Driven Registry 重构
/// </summary>
public static class AsciiLevelGenerator
{
    // ═════════════════════════════════════════════════
    // S41: Editor 侧回调事件 — 运行时脚本不能直接引用 Editor 类，
    // 通过此事件解耦，由 LevelEditorPickingManager 在 Editor 侧订阅。
    // ═════════════════════════════════════════════════
    /// <summary>关卡生成/换肤完成后触发，Editor 侧用于同步 Picking 状态</summary>
    public static System.Action OnLevelGenerated;

    // ═════════════════════════════════════════════════
    // 常量
    // ═══════════════════════════════════════════════════
    // [AI防坑警告] CELL_SIZE 必须与 PhysicsMetrics.CELL_SIZE 保持一致！
    // 这是整个关卡系统的基石，修改会导致所有关卡布局崩溃。
    private const float CELL_SIZE = PhysicsMetrics.CELL_SIZE;
    private const int GROUND_SORTING = 0;
    private const int ELEMENT_SORTING = 5;
    private const string ROOT_NAME = "AsciiLevel_Root";

    // 白盒颜色定义
    private static readonly Color COLOR_GROUND   = new Color(0.55f, 0.55f, 0.55f);   // 中灰
    private static readonly Color COLOR_PLATFORM  = new Color(0.70f, 0.70f, 0.70f);   // 浅灰
    private static readonly Color COLOR_WALL      = new Color(0.40f, 0.40f, 0.40f);   // 深灰
    private static readonly Color COLOR_SPIKE     = new Color(0.85f, 0.25f, 0.25f);   // 红色
    private static readonly Color COLOR_FIRE      = new Color(1.00f, 0.50f, 0.10f);   // 橙色
    private static readonly Color COLOR_PENDULUM  = new Color(0.70f, 0.45f, 0.20f);   // 棕色
    private static readonly Color COLOR_BOUNCY    = new Color(0.30f, 0.85f, 0.30f);   // 绿色
    private static readonly Color COLOR_COLLAPSE  = new Color(0.80f, 0.65f, 0.30f);   // 土黄
    private static readonly Color COLOR_ONEWAY    = new Color(0.50f, 0.75f, 0.90f);   // 浅蓝
    private static readonly Color COLOR_ENEMY     = new Color(0.90f, 0.20f, 0.60f);   // 粉红
    private static readonly Color COLOR_FAKEWALL  = new Color(0.55f, 0.55f, 0.65f);   // 蓝灰
    private static readonly Color COLOR_PASSAGE   = new Color(0.40f, 0.70f, 0.55f);   // 青绿
    private static readonly Color COLOR_COLLECT   = new Color(1.00f, 0.85f, 0.20f);   // 金色
    private static readonly Color COLOR_GOAL      = new Color(0.20f, 1.00f, 0.40f);   // 亮绿
    private static readonly Color COLOR_MOVING    = new Color(0.50f, 0.50f, 0.90f);   // 蓝紫
    private static readonly Color COLOR_MARIO     = new Color(0.90f, 0.20f, 0.20f);   // 红色
    private static readonly Color COLOR_TRICKSTER = new Color(0.50f, 0.20f, 0.80f);   // 紫色

    // ═══════════════════════════════════════════════════
    // S46: 数据驱动字符映射
    // ═══════════════════════════════════════════════════
    // charMap: ASCII 字符 → 生成委托（从 Registry + spawnMap 动态构建）
    private static Dictionary<char, System.Action<int, int>> charMap;
    // spawnMap: 元素名称 → 生成委托（内部映射表，连接 Registry 条目与 Spawn 方法）
    private static Dictionary<string, System.Action<int, int>> spawnMap;
    private static Transform rootTransform;
    private static int groundLayerIndex;

    /// <summary>
    /// S46: 初始化元素名称到 Spawn 方法的映射。
    /// 新增元素时在此注册: spawnMap["NewElementName"] = SpawnNewElement;
    /// </summary>
    private static void InitSpawnMap()
    {
        spawnMap = new Dictionary<string, System.Action<int, int>>
        {
            { "Ground",             SpawnGround },
            { "Platform",           SpawnPlatform },
            { "Wall",               SpawnWall },
            { "Air",                (x, y) => { } },  // 空气，不生成
            { "Space",              (x, y) => { } },  // 空格也视为空气
            { "MarioSpawn",         SpawnMarioSpawn },
            { "TricksterSpawn",     SpawnTricksterSpawn },
            { "GoalZone",           SpawnGoalZone },
            { "SpikeTrap",          SpawnSpikeTrap },
            { "FireTrap",           SpawnFireTrap },
            { "PendulumTrap",       SpawnPendulumTrap },
            { "BouncyPlatform",     SpawnBouncyPlatform },
            { "CollapsingPlatform", SpawnCollapsingPlatform },
            // [AI防坑警告] S44c: OneWayPlatform 不再逐字符生成。
            // 连续的 '-' 会被 MergeAndSpawnOneWayPlatforms 合并为一个长条平台。
            // 如果恢复逐字符生成，会导致 S+Space 下落失效（需要同时 IgnoreCollision 多个碰撞体）、
            // 边缘掉落、物理抖动等一系列拼接问题。
            { "OneWayPlatform",     (x, y) => { } },  // 占位：实际生成由合并逻辑处理
            { "BouncingEnemy",      SpawnBouncingEnemy },
            { "FakeWall",           SpawnFakeWall },
            { "HiddenPassage",      SpawnHiddenPassage },
            { "Collectible",        SpawnCollectible },
            { "SimpleEnemy",        SpawnSimpleEnemy },
            { "MovingPlatform",     SpawnMovingPlatform },
        };
    }

    /// <summary>
    /// S46: 从 AsciiElementRegistry 动态构建字符映射字典。
    /// 替代原始的硬编码 InitCharMap()。
    ///
    /// 构建逻辑：
    ///   1. 从 Registry 获取所有元素条目
    ///   2. 通过 elementName 在 spawnMap 中查找对应的 Spawn 方法
    ///   3. 将 asciiChar → Spawn 方法 的映射写入 charMap
    ///   4. 未在 spawnMap 中注册的元素名会输出警告（提醒开发者添加 Spawn 方法）
    /// </summary>
    private static void InitCharMap()
    {
        InitSpawnMap();

        charMap = new Dictionary<char, System.Action<int, int>>();

        AsciiElementRegistry registry = AsciiElementRegistry.GetDefault();
        if (registry == null || registry.entries == null)
        {
            Debug.LogError("[AsciiLevelGen] AsciiElementRegistry is null! Using empty charMap.");
            return;
        }

        foreach (var entry in registry.entries)
        {
            if (entry == null) continue;

            if (spawnMap.TryGetValue(entry.elementName, out var spawnAction))
            {
                charMap[entry.asciiChar] = spawnAction;
            }
            else
            {
                Debug.LogWarning($"[AsciiLevelGen] No spawn method registered for " +
                                 $"'{entry.elementName}' (char '{entry.asciiChar}'). " +
                                 $"Add it to InitSpawnMap().");
                // 未注册的元素默认不生成（空气行为）
                charMap[entry.asciiChar] = (x, y) => { };
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // 公共 API
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 从 ASCII 字符串模板生成关卡
    /// 坐标系: 模板第一行 = 最高行（Y 最大），最后一行 = 最低行（Y=0）
    /// </summary>
    /// <param name="template">多行 ASCII 字符串</param>
    /// <param name="clearExisting">是否先清除已有的 ASCII 关卡</param>
    /// <returns>生成的根 GameObject</returns>
    public static GameObject GenerateFromTemplate(string template, bool clearExisting = true)
    {
        return GenerateFromTemplate(template, clearExisting, false);
    }

    /// <summary>
    /// 从 ASCII 字符串模板生成关卡（支持片段模式）
    /// S43: 新增 isSnippet 参数，片段模式下验证器不要求 M/G
    /// </summary>
    /// <param name="template">多行 ASCII 字符串</param>
    /// <param name="clearExisting">是否先清除已有的 ASCII 关卡</param>
    /// <param name="isSnippet">是否为片段模式（片段不要求 M/G）</param>
    /// <returns>生成的根 GameObject</returns>
    public static GameObject GenerateFromTemplate(string template, bool clearExisting, bool isSnippet)
    {
        if (string.IsNullOrEmpty(template))
        {
            Debug.LogWarning("[AsciiLevelGen] Template is empty!");
            return null;
        }

        // Session 32: 生成前自动验证模板的物理可行性
        // Session 43: 支持片段模式验证（片段不要求 M/G）
        // L1: 静态结构检查（间隙/高台/出生安全/弹跳净空/陷阱缓冲）
        var validationResult = AsciiLevelValidator.ValidateTemplate(template, isSnippet);
        if (validationResult.HasErrors)
        {
            Debug.LogError($"[AsciiLevelGen] ⚠️ L1 Template has physical issues!\n{validationResult.GetReport()}");
        }
        else if (validationResult.HasWarnings)
        {
            Debug.LogWarning($"[AsciiLevelGen] L1 Template has warnings:\n{validationResult.GetReport()}");
        }
        else
        {
            Debug.Log("[AsciiLevelGen] ✅ L1 Template validation passed.");
        }

        // Session 47: L2 BFS 可达性验证（图搜索：M → G 路径是否存在）
        var reachResult = LevelReachabilityAnalyzer.Analyze(template, isSnippet);
        if (!reachResult.IsReachable)
        {
            Debug.LogError($"[AsciiLevelGen] ❌ L2 BFS: {reachResult.GetReport()}");
            LevelReachabilityAnalyzer.CopyErrorToClipboard(reachResult);
        }
        else if (!isSnippet)
        {
            Debug.Log($"[AsciiLevelGen] ✅ L2 BFS: {reachResult.GetReport()}");
        }

        if (clearExisting) ClearGeneratedLevel();

        InitCharMap();

        // 创建根节点
        GameObject root = new GameObject(ROOT_NAME);
        rootTransform = root.transform;

        // 确保 Ground 层存在
        groundLayerIndex = LayerMask.NameToLayer("Ground");
        if (groundLayerIndex == -1) groundLayerIndex = 0;

        // 解析模板
        string[] lines = template.Split('\n');
        int height = lines.Length;

        for (int row = 0; row < height; row++)
        {
            string line = lines[row];
            // Y 坐标: 第一行在最上面（Y = height - 1），最后一行在最下面（Y = 0）
            int worldY = height - 1 - row;

            // S44c: 先合并连续 '-' 为长条 OneWayPlatform
            MergeAndSpawnOneWayPlatforms(line, worldY);

            for (int col = 0; col < line.Length; col++)
            {
                char c = line[col];

                if (charMap.TryGetValue(c, out System.Action<int, int> spawnAction))
                {
                    spawnAction(col, worldY);
                }
                else if (c != '\r' && c != '\n')
                {
                    Debug.LogWarning($"[AsciiLevelGen] Unknown char '{c}' at ({col}, {worldY}), skipped.");
                }
            }
        }

        Debug.Log($"[AsciiLevelGen] Level generated: {root.transform.childCount} objects from {height} rows.");

        // S41: 通知 Editor 侧同步 Picking 状态（运行时脚本不能直接引用 Editor 类，通过事件解耦）
        OnLevelGenerated?.Invoke();

        return root;
    }

    /// <summary>清除已生成的 ASCII 关卡</summary>
    public static void ClearGeneratedLevel()
    {
        // 查找并销毁所有 AsciiLevel_Root
        GameObject[] roots = GameObject.FindGameObjectsWithTag("Untagged");
        foreach (GameObject go in roots)
        {
            if (go != null && go.name == ROOT_NAME)
            {
#if UNITY_EDITOR
                Object.DestroyImmediate(go);
#else
                Object.Destroy(go);
#endif
            }
        }

        // 备用: 直接按名称查找
        GameObject existing = GameObject.Find(ROOT_NAME);
        while (existing != null)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(existing);
#else
            Object.Destroy(existing);
#endif
            existing = GameObject.Find(ROOT_NAME);
        }
    }

    /// <summary>获取所有内置模板的名称列表</summary>
    public static string[] GetBuiltInTemplateNames()
    {
        return new string[] { "Classic Plains", "Underground Cavern" };
    }

    /// <summary>根据索引获取内置模板</summary>
    public static string GetBuiltInTemplate(int index)
    {
        switch (index)
        {
            case 0: return TEMPLATE_CLASSIC_PLAINS;
            case 1: return TEMPLATE_UNDERGROUND;
            default: return TEMPLATE_CLASSIC_PLAINS;
        }
    }

    /// <summary>获取字符映射表的可读描述（用于编辑器显示）</summary>
    public static string GetCharMapReference()
    {
        return
            "# = Ground   = = Platform   W = Wall   . = Air\n" +
            "M = Mario    T = Trickster  G = Goal\n" +
            "^ = Spike    ~ = Fire       P = Pendulum\n" +
            "B = Bouncy   C = Collapse   - = OneWay\n" +
            "E = BounceEnemy  e = SimpleEnemy\n" +
            "F = FakeWall  H = HiddenPassage  o = Collectible\n" +
            "> = MovingPlatform";
    }

    // ═══════════════════════════════════════════════════
    // 内置模板
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 经典平原关卡模板
    /// 包含: 出生点、跳跃平台、沟壑、地刺区、弹跳平台、高台、敌人、收集物、终点
    /// 尺寸: 约 80x15 格
    ///
    /// S35 修复（基于业界最佳实践）：
    ///   - M/T 间距拉开至 3 格（Celeste: checkpoint 周围无危险物）
    ///   - 弹跳平台(B)上方留出 3 格安全净空（SMW: 弹跳垫上方留完整弧线空间）
    ///   - 地刺(^^)前后增加 1 格安全缓冲（Dylan Wolf: 陷阱必须指向空tile）
    ///   - 陷阱不再紧贴着陆区，确保玩家有反应空间
    /// </summary>
    // S43: 修复内置模板空格字符和行宽不一致问题
    // 所有空气位置统一使用 '.' 填充，每行统一 80 字符宽
    private const string TEMPLATE_CLASSIC_PLAINS =
        "................................................................................\n" +
        "................................................................................\n" +
        "..............o.....o...........o.o.o.................o.o.............o.........\n" +
        "..............=.....=...........-----................===..........o...===.......\n" +
        ".......o.o..............................o.............................G.........\n" +
        "......-----..........o..........o.....-----=.....===.......===.........###......\n" +
        ".....................=..........===...............=.................####........\n" +
        "..M..T.....e..............E................C..C...........B.....e...#####.......\n" +
        "..##.##..####..##....####..##...##.####.####..##..####..####..####..######......\n" +
        "..##.##..####..##.^^.####..##...##.####.####..##..####..####..####..######......\n" +
        "..##.##..####..##....####..##...##.####.####..##..####..####..####..######......\n" +
        "################################################################################\n" +
        "################################################################################";

    /// <summary>
    /// 地下洞窟关卡模板
    /// 包含: 封闭空间、隐藏通道、伪装墙、火焰陷阱、摆锤、崩塔平台
    /// 尺寸: 约 70x18 格
    ///
    /// S35 修复：
    ///   - M/T 间距拉开至 3 格
    ///   - 弹跳平台(B)上方留出 3 格安全净空
    ///   - 火焰(~~)前后增加缓冲格
    /// </summary>
    // S43: 修复地下模板行宽不一致问题，统一 70 字符宽
    private const string TEMPLATE_UNDERGROUND =
        "######################################################################\n" +
        "#....................................................................#\n" +
        "#....................................................................#\n" +
        "#..M..T.o.o.o........o..o.......o.o.o...........o.o..................#\n" +
        "#..##.##.-----........===.......------..........===..........G.......#\n" +
        "#..##.##...................o...................o...........###.......#\n" +
        "#..##.##.....F...===........P......===.C..C.....===....####..........#\n" +
        "#..##.##.....F.........e......................B........#####.........#\n" +
        "#..##.##.####..##..^^..####.~~.##.####.####..##..####..######........#\n" +
        "#..##.##.####..##..^^..####.~~.##.####.####..##..####..######........#\n" +
        "#..##.##.####..##......####....##.####.####..##..####..######........#\n" +
        "#..##H##.####..##......####....##.####.####..##..####..######........#\n" +
        "#..##.##.####..##......####....##.####.####..##..####..######........#\n" +
        "#..##.##.####..##......####....##.####.####..##..####..######........#\n" +
        "#..##.##.####..##......####....##.####.####..##..####..######........#\n" +
        "#..##.##.####..##......####....##.####.####..##..####..######........#\n" +
        "######################################################################\n" +
        "######################################################################";

    // ═══════════════════════════════════════════════════
    // 生成方法（每个字符对应一个）
    // ═══════════════════════════════════════════════════

    private static void SpawnGround(int x, int y)
    {
        CreateBlock("Ground", x, y, COLOR_GROUND, GROUND_SORTING, true, false);
    }

    private static void SpawnPlatform(int x, int y)
    {
        CreateBlock("Platform", x, y, COLOR_PLATFORM, GROUND_SORTING + 1, true, false);
    }

    private static void SpawnWall(int x, int y)
    {
        CreateBlock("Wall", x, y, COLOR_WALL, GROUND_SORTING, true, false);
    }

    private static void SpawnMarioSpawn(int x, int y)
    {
        // Mario 出生点标记（不生成实体，只放一个标记物）
        GameObject marker = CreateVisualMarker("MarioSpawn", x, y, COLOR_MARIO, "M");
        // 同时创建 MarioSpawnPoint（供 LevelManager 使用）
        GameObject spawnPoint = new GameObject("MarioSpawnPoint");
        spawnPoint.transform.position = new Vector3(x * CELL_SIZE, y * CELL_SIZE, 0);
        spawnPoint.transform.parent = rootTransform;
    }

    private static void SpawnTricksterSpawn(int x, int y)
    {
        GameObject marker = CreateVisualMarker("TricksterSpawn", x, y, COLOR_TRICKSTER, "T");
        GameObject spawnPoint = new GameObject("TricksterSpawnPoint");
        spawnPoint.transform.position = new Vector3(x * CELL_SIZE, y * CELL_SIZE, 0);
        spawnPoint.transform.parent = rootTransform;
    }

    private static void SpawnGoalZone(int x, int y)
    {
        // Session 32: 终点碰撞体使用 PhysicsMetrics 标准尺寸
        GameObject go = CreateBlock("GoalZone", x, y, COLOR_GOAL, ELEMENT_SORTING, false, true);
        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        if (col != null) col.size = PhysicsMetrics.GOAL_COLLIDER_SIZE;
        go.AddComponent<GoalZone>();
    }

    private static void SpawnSpikeTrap(int x, int y)
    {
        // Session 32: 使用 PhysicsMetrics 的宽容碰撞体
        // 地刺碰撞体比视觉小 = Celeste 风格宽容感
        GameObject go = CreateBlock("SpikeTrap", x, y, COLOR_SPIKE, ELEMENT_SORTING, false, true);
        // S37: 视觉缩放操作 Visual 子节点，根物体 localScale 保持 (1,1,1)
        go.transform.Find("Visual").localScale = new Vector3(CELL_SIZE, CELL_SIZE * 0.4f, 1f);
        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.size = PhysicsMetrics.SPIKE_COLLIDER_SIZE;
            col.offset = new Vector2(0f, PhysicsMetrics.SPIKE_COLLIDER_OFFSET_Y);
        }
        go.AddComponent<SpikeTrap>();
    }

    private static void SpawnFireTrap(int x, int y)
    {
        // Session 32: 火焰碰撞体使用 PhysicsMetrics 标准尺寸（比视觉小，提供容错）
        GameObject go = CreateBlock("FireTrap", x, y, COLOR_FIRE, ELEMENT_SORTING, false, true);
        // S37: 视觉缩放操作 Visual 子节点
        go.transform.Find("Visual").localScale = new Vector3(CELL_SIZE * 0.5f, CELL_SIZE * 0.5f, 1f);
        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        if (col != null) col.size = PhysicsMetrics.FIRE_COLLIDER_SIZE;
        go.AddComponent<FireTrap>();
    }

    private static void SpawnPendulumTrap(int x, int y)
    {
        // Session 32: 摆锤碰撞体使用 PhysicsMetrics 标准尺寸
        // 摆锤锚点在上方，实际摆锤在下面摆动
        GameObject go = CreateBlock("PendulumTrap", x, y, COLOR_PENDULUM, ELEMENT_SORTING, false, false);
        // S37: 视觉缩放操作 Visual 子节点
        go.transform.Find("Visual").localScale = new Vector3(CELL_SIZE * 0.3f, CELL_SIZE * 0.3f, 1f);
        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        if (col != null) col.size = PhysicsMetrics.PENDULUM_COLLIDER_SIZE;
        go.AddComponent<PendulumTrap>();
    }

    private static void SpawnBouncyPlatform(int x, int y)
    {
        // Session 32: 弹跳平台碰撞体使用 PhysicsMetrics 标准尺寸
        GameObject go = CreateBlock("BouncyPlatform", x, y, COLOR_BOUNCY, ELEMENT_SORTING, true, false);
        // S37: 视觉缩放操作 Visual 子节点，并赋值给 BouncyPlatform.visualTransform
        Transform bouncyVisual = go.transform.Find("Visual");
        bouncyVisual.localScale = new Vector3(CELL_SIZE * 2f, CELL_SIZE * 0.3f, 1f);
        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        if (col != null) col.size = PhysicsMetrics.BOUNCY_COLLIDER_SIZE;
        BouncyPlatform bp = go.AddComponent<BouncyPlatform>();
        bp.visualTransform = bouncyVisual;
    }

    private static void SpawnCollapsingPlatform(int x, int y)
    {
        // Session 32: 崩塌平台碰撞体使用 PhysicsMetrics 标准尺寸
        GameObject go = CreateBlock("CollapsingPlatform", x, y, COLOR_COLLAPSE, ELEMENT_SORTING, true, false);
        // S37: 视觉缩放操作 Visual 子节点
        go.transform.Find("Visual").localScale = new Vector3(CELL_SIZE * 2f, CELL_SIZE * 0.4f, 1f);
        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        if (col != null) col.size = PhysicsMetrics.COLLAPSE_COLLIDER_SIZE;
        go.AddComponent<CollapsingPlatform>();
    }

    // [AI防坑警告] S44c: OneWayPlatform 不再逐字符生成。
    // 连续的 '-' 会被 MergeAndSpawnOneWayPlatforms 合并为一个长条平台。
    // 如果恢复逐字符生成，会导致 S+Space 下落失效（需要同时 IgnoreCollision 多个碰撞体）、
    // 边缘掉落、物理抖动等一系列拼接问题。
    // 旧版 SpawnOneWayPlatform(int x, int y) 已废弃，保留注释供参考。

    /// <summary>
    /// S44c: 扫描一行中连续的 '-' 字符，合并为单个长条 OneWayPlatform。
    /// 解决多个独立小平台拼接导致的 S+Space 失效、边缘掉落、物理抖动等问题。
    /// </summary>
    private static void MergeAndSpawnOneWayPlatforms(string line, int worldY)
    {
        int col = 0;
        while (col < line.Length)
        {
            if (line[col] == '-')
            {
                // 向右扫描连续的 '-'
                int startCol = col;
                while (col < line.Length && line[col] == '-')
                {
                    col++;
                }
                int width = col - startCol;
                SpawnMergedOneWayPlatform(startCol, worldY, width);
            }
            else
            {
                col++;
            }
        }
    }

    /// <summary>
    /// S44c: 生成一个合并后的长条 OneWayPlatform。
    /// 位置 = 连续段的中心点，碰撞体和视觉宽度 = width × CELL_SIZE。
    /// </summary>
    private static void SpawnMergedOneWayPlatform(int startX, int y, int width)
    {
        // 计算中心位置：startX 是左端格子坐标，中心 = startX + (width - 1) / 2.0
        float centerX = startX * CELL_SIZE + (width - 1) * CELL_SIZE * 0.5f;
        float centerY = y * CELL_SIZE;

        // 创建根物体（命名包含起始坐标和宽度，便于调试）
        GameObject go = new GameObject($"OneWayPlatform_{startX}_{y}_w{width}");
        go.transform.position = new Vector3(centerX, centerY, 0);
        go.transform.parent = rootTransform;
        go.layer = groundLayerIndex;

        // S37: Visual 子节点
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = Vector3.zero;
        // 视觉宽度 = width 格 × CELL_SIZE，高度保持薄片
        visual.transform.localScale = new Vector3(width * CELL_SIZE, CELL_SIZE * 0.3f, 1f);

        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = CreateWhiteBoxSprite();
        sr.color = COLOR_ONEWAY;
        sr.sortingOrder = ELEMENT_SORTING;

        // 碰撞体：宽度 = width × CELL_SIZE，高度保持 PhysicsMetrics 标准
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(width * PhysicsMetrics.ONEWAY_COLLIDER_SIZE.x, PhysicsMetrics.ONEWAY_COLLIDER_SIZE.y);
        col.usedByEffector = true;

        go.AddComponent<PlatformEffector2D>();
        go.AddComponent<OneWayPlatform>();

        Debug.Log($"[AsciiLevelGen] Merged OneWayPlatform at ({startX},{y}), width={width}");
    }

    private static void SpawnBouncingEnemy(int x, int y)
    {
        // Session 32: 敌人碰撞体使用 PhysicsMetrics 标准尺寸
        // S44: isTrigger 改为 false — 敌人需要物理碰撞站在地面上，
        // isTrigger=true 会导致穿过地面掉落。BouncingEnemy 自带 OnCollisionEnter2D 处理伤害。
        GameObject go = CreateBlock("BouncingEnemy", x, y, COLOR_ENEMY, ELEMENT_SORTING, false, false);
        // S37: 视觉缩放操作 Visual 子节点
        go.transform.Find("Visual").localScale = new Vector3(CELL_SIZE * 0.8f, CELL_SIZE * 0.8f, 1f);
        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        if (col != null) col.size = PhysicsMetrics.BOUNCING_ENEMY_COLLIDER_SIZE;
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 1f;
        rb.freezeRotation = true;
        go.AddComponent<BouncingEnemy>();
    }

    private static void SpawnSimpleEnemy(int x, int y)
    {
        // Session 32: 敌人碰撞体使用 PhysicsMetrics 标准尺寸
        // S44: isTrigger 改为 false — 敌人需要物理碰撞站在地面上，
        // isTrigger=true 会导致穿过地面掉落。SimpleEnemy 自带 OnCollisionEnter2D 处理伤害+踩踏，
        // 不需要 DamageDealer（DamageDealer 依赖 OnTrigger 回调，与非 Trigger 碰撞体不兼容）。
        GameObject go = CreateBlock("SimpleEnemy", x, y, COLOR_ENEMY, ELEMENT_SORTING, false, false);
        // S37: 视觉缩放操作 Visual 子节点
        go.transform.Find("Visual").localScale = new Vector3(CELL_SIZE * 0.8f, CELL_SIZE * 0.8f, 1f);
        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        if (col != null) col.size = PhysicsMetrics.SIMPLE_ENEMY_COLLIDER_SIZE;
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 1f;
        rb.freezeRotation = true;
        go.AddComponent<SimpleEnemy>();
        // S44: 移除 DamageDealer — SimpleEnemy.OnCollisionEnter2D 已完整处理伤害+踩踏逻辑，
        // DamageDealer 的 OnTrigger 回调在非 Trigger 碰撞体上不会触发，挂载无意义。
    }

    private static void SpawnFakeWall(int x, int y)
    {
        GameObject go = CreateBlock("FakeWall", x, y, COLOR_FAKEWALL, GROUND_SORTING + 1, true, true);
        go.AddComponent<FakeWall>();
    }

    private static void SpawnHiddenPassage(int x, int y)
    {
        // 隐藏通道入口
        GameObject go = CreateBlock("HiddenPassage", x, y, COLOR_PASSAGE, ELEMENT_SORTING, true, true);
        go.AddComponent<HiddenPassage>();
    }

    private static void SpawnCollectible(int x, int y)
    {
        // Session 32: 收集物碰撞体使用 PhysicsMetrics 标准尺寸
        GameObject go = CreateBlock("Collectible", x, y, COLOR_COLLECT, ELEMENT_SORTING, false, true);
        // S37: 视觉缩放操作 Visual 子节点
        go.transform.Find("Visual").localScale = new Vector3(CELL_SIZE * 0.5f, CELL_SIZE * 0.5f, 1f);
        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        if (col != null) col.size = PhysicsMetrics.COLLECTIBLE_COLLIDER_SIZE;
        go.AddComponent<Collectible>();
    }

    private static void SpawnMovingPlatform(int x, int y)
    {
        // Session 32: 移动平台碰撞体使用 PhysicsMetrics 标准尺寸
        GameObject go = CreateBlock("MovingPlatform", x, y, COLOR_MOVING, ELEMENT_SORTING, true, false);
        // S37: 视觉缩放操作 Visual 子节点
        go.transform.Find("Visual").localScale = new Vector3(CELL_SIZE * 3f, CELL_SIZE * 0.4f, 1f);
        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        if (col != null) col.size = PhysicsMetrics.MOVING_COLLIDER_SIZE;
        MovingPlatform mp = go.AddComponent<MovingPlatform>();
    }

    // ═══════════════════════════════════════════════════
    // 通用创建辅助
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// S37: 视碰分离重构 — 创建白盒方块
    /// 结构: Root(物理层: BoxCollider2D) -> Visual(视觉层: SpriteRenderer)
    /// 根物体 localScale 永远保持 (1,1,1)，视觉缩放由 Visual 子节点承担。
    /// </summary>
    private static GameObject CreateBlock(string name, int gridX, int gridY, Color color,
        int sortingOrder, bool isSolid, bool isTrigger)
    {
        GameObject go = new GameObject($"{name}_{gridX}_{gridY}");
        go.transform.position = new Vector3(gridX * CELL_SIZE, gridY * CELL_SIZE, 0);
        go.transform.parent = rootTransform;

        // 设置 Ground 层（用于碰撞检测）
        if (isSolid && !isTrigger)
            go.layer = groundLayerIndex;

        // S37: Visual 子节点（承载 SpriteRenderer）
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = Vector3.zero;

        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = CreateWhiteBoxSprite();
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        // BoxCollider2D 保留在根物体上
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        col.isTrigger = isTrigger;

        return go;
    }

    /// <summary>创建一个可视标记（带文字标签）</summary>
    private static GameObject CreateVisualMarker(string name, int gridX, int gridY, Color color, string label)
    {
        GameObject go = new GameObject($"{name}_{gridX}_{gridY}");
        go.transform.position = new Vector3(gridX * CELL_SIZE, gridY * CELL_SIZE, 0);
        go.transform.parent = rootTransform;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateWhiteBoxSprite();
        sr.color = new Color(color.r, color.g, color.b, 0.5f); // 半透明
        sr.sortingOrder = ELEMENT_SORTING + 1;

        // 文字标签
        GameObject labelObj = new GameObject($"Label_{name}");
        labelObj.transform.parent = go.transform;
        labelObj.transform.localPosition = Vector3.zero;
        TextMesh tm = labelObj.AddComponent<TextMesh>();
        tm.text = label;
        tm.characterSize = 0.3f;
        tm.fontSize = 40;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;

        return go;
    }

    /// <summary>创建白盒 Sprite（4x4 纯白纹理）</summary>
    private static Sprite cachedWhiteSprite;
    private static Sprite CreateWhiteBoxSprite()
    {
        // 缓存避免重复创建
        if (cachedWhiteSprite != null) return cachedWhiteSprite;

        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;

        cachedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        return cachedWhiteSprite;
    }

    // ═══════════════════════════════════════════════════
    // 换肤系统 (Theme Application)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 将主题应用到已生成的关卡
    /// 遍历 AsciiLevel_Root 下所有子物体，根据名称前缀匹配主题 Sprite
    /// 
    /// 【Fallback 安全】空插槽保留白盒原样，绝不抛 NullReferenceException
    /// 【通用替换】不强耦合具体陷阱类，统一通过 SpriteRenderer 替换
    /// 
    /// Session 32 视碰分离增强：
    ///   换肤时自动为地形元素挂载 SpriteAutoFit（Tiled 模式），
    ///   确保新素材以正确的 PPU 平铺填充碰撞体区域，
    ///   不拉伸不变形，保持像素完美。
    /// </summary>
    public static void ApplyTheme(LevelThemeProfile theme)
    {
        if (theme == null)
        {
            Debug.LogWarning("[AsciiLevelGen] Theme profile is null!");
            return;
        }

        GameObject root = GameObject.Find(ROOT_NAME);
        if (root == null)
        {
            Debug.LogWarning("[AsciiLevelGen] No generated level found (AsciiLevel_Root missing).");
            return;
        }

        int replacedCount = 0;

        // 应用背景色
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.backgroundColor = theme.backgroundColor;
        }

        // 遍历所有子物体
        foreach (Transform child in root.transform)
        {
            // S37: 视碰分离 — SpriteRenderer 现在在 Visual 子节点上
            SpriteRenderer sr = child.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) continue;

            string objName = child.name;

            // 根据名称前缀匹配类型
            string elementKey = ExtractElementKey(objName);

            if (elementKey == "Ground")
            {
                if (theme.groundSprite != null)
                {
                    sr.sprite = theme.groundSprite;
                    // S37: SpriteAutoFit 挂在 Visual 子节点上
                    EnsureSpriteAutoFit(sr.gameObject, SpriteAutoFit.FitMode.Tiled);
                    replacedCount++;
                }
                sr.color = theme.groundColor;
            }
            else if (elementKey == "Platform")
            {
                if (theme.platformSprite != null)
                {
                    sr.sprite = theme.platformSprite;
                    EnsureSpriteAutoFit(sr.gameObject, SpriteAutoFit.FitMode.Tiled);
                    replacedCount++;
                }
                sr.color = theme.platformColor;
            }
            else if (elementKey == "Wall")
            {
                if (theme.wallSprite != null)
                {
                    sr.sprite = theme.wallSprite;
                    EnsureSpriteAutoFit(sr.gameObject, SpriteAutoFit.FitMode.Tiled);
                    replacedCount++;
                }
                sr.color = theme.wallColor;
            }
            else
            {
                // 动态元素: 通过 elementSprites 字典查找
                Sprite themeSprite = theme.GetElementSprite(elementKey);
                Color? themeColor = theme.GetElementColor(elementKey);

                // 【Fallback 安全】只在有值时替换
                if (themeSprite != null)
                {
                    sr.sprite = themeSprite;
                    replacedCount++;
                }
                if (themeColor.HasValue)
                {
                    sr.color = themeColor.Value;
                }
            }
        }

        Debug.Log($"[AsciiLevelGen] Theme '{theme.themeName}' applied: {replacedCount} sprites replaced.");

        // S41: 通知 Editor 侧同步 Picking 状态
        OnLevelGenerated?.Invoke();
    }

    /// <summary>
    /// 从 GameObject 名称中提取元素键名
    /// 命名约定: "ElementKey_X_Y" (如 "SpikeTrap_5_3")
    /// </summary>
    private static string ExtractElementKey(string objectName)
    {
        int firstUnderscore = objectName.IndexOf('_');
        if (firstUnderscore > 0)
            return objectName.Substring(0, firstUnderscore);
        return objectName;
    }

    /// <summary>
    /// Session 32: 确保目标 GameObject 上有 SpriteAutoFit 组件，并设置指定的适配模式。
    /// 如果已存在则只更新模式，不重复挂载。
    /// 用于换肤时自动为地形元素启用视碰分离的 Tiled 渲染。
    /// </summary>
    private static void EnsureSpriteAutoFit(GameObject go, SpriteAutoFit.FitMode mode)
    {
        SpriteAutoFit autoFit = go.GetComponent<SpriteAutoFit>();
        if (autoFit == null)
        {
            autoFit = go.AddComponent<SpriteAutoFit>();
        }
        autoFit.SetFitMode(mode);
    }
}
