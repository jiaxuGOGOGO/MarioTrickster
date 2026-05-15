using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// ASCII 关卡模板生成器 — 基于 AsciiElementRegistry 的数据驱动字符解析系统
/// 
/// 核心设计:
///   - S46: 字符映射从 AsciiElementRegistry (ScriptableObject) 动态构建
///   - 新增元素只需新建逻辑脚本 + 在 Registry 中 Add 一条记录
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
///   '@' = 旋转锯片 (SawBlade)                    [S56 新增]
///   'f' = 飞行敌人 (FlyingEnemy)                  [S56 新增]
///   '<' = 传送带 (ConveyorBelt, 向左)            [S56 新增]
///   'S' = 检查点 (Checkpoint)                    [S56 新增]
///   'X' = 可破坏方块 (BreakableBlock)              [S56 新增]
///   '[' = 临时封路机关 (ControllableBlocker)       [S53 原型B]
///
/// 扩展方式 (S46 Data-Driven):
///   1. 在 AsciiElementRegistry 资产中添加新字符条目
///   2. 在 Registry 的 componentTypeNames 中填写逻辑脚本类名，无需修改本文件。
///   3. Generator 会自动创建 Root/Visual/Collider 并反射挂载组件
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
    // S56 新增元素颜色
    private static readonly Color COLOR_SAWBLADE   = new Color(0.70f, 0.70f, 0.70f);   // 银灰
    private static readonly Color COLOR_FLYING     = new Color(0.85f, 0.40f, 0.85f);   // 紫粉
    private static readonly Color COLOR_CONVEYOR   = new Color(0.60f, 0.60f, 0.40f);   // 橄榄绿
    private static readonly Color COLOR_CHECKPOINT = new Color(0.20f, 0.80f, 0.90f);   // 青蓝
    private static readonly Color COLOR_BREAKABLE  = new Color(0.75f, 0.55f, 0.30f);   // 土棕
    private static readonly Color COLOR_MARIO     = new Color(0.90f, 0.20f, 0.20f);   // 红色
    private static readonly Color COLOR_TRICKSTER = new Color(0.50f, 0.20f, 0.80f);   // 紫色

    // ═══════════════════════════════════════════════════
    // Registry 驱动字符映射（零代码新增机制）
    // ═══════════════════════════════════════════════════
    // elementMap: ASCII 字符 → Registry Entry。生成器不再维护 SpawnXXX 映射表。
    private static Dictionary<char, AsciiElementEntry> elementMap;
    private static Transform rootTransform;
    private static int groundLayerIndex;

    /// <summary>
    /// 从 AsciiElementRegistry 构建字符到 Entry 的运行时映射。
    /// 不再要求元素名称对应任何 Spawn 方法，新增元素只依赖 Registry 数据。
    /// </summary>
    private static void InitElementMap()
    {
        elementMap = new Dictionary<char, AsciiElementEntry>();
        AsciiElementRegistry registry = AsciiElementRegistry.GetDefault();
        if (registry == null || registry.entries == null)
        {
            Debug.LogError("[AsciiLevelGen] AsciiElementRegistry is null! Using empty elementMap.");
            return;
        }

        foreach (var entry in registry.entries)
        {
            if (entry == null) continue;
            char c = entry.AsciiChar;
            if (c == '\0') continue;
            if (elementMap.ContainsKey(c))
            {
                Debug.LogWarning($"[AsciiLevelGen] Duplicate Registry char '{c}' for '{entry.elementName}', skipped.");
                continue;
            }
            elementMap[c] = entry;
        }
    }

    private static bool TryGetEntry(char c, out AsciiElementEntry entry)
    {
        if (elementMap == null) InitElementMap();
        return elementMap != null && elementMap.TryGetValue(c, out entry);
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

        InitElementMap();

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

            // S57b/S44c: 仅保留合并生成逻辑；参数来自 Registry Entry，而非 Generator 硬编码。
            if (TryGetEntry('#', out var groundEntry))
                MergeAndSpawnSolidBlocks(line, worldY, '#', groundEntry);
            if (TryGetEntry('=', out var platformEntry))
                MergeAndSpawnSolidBlocks(line, worldY, '=', platformEntry);
            if (TryGetEntry('-', out var oneWayEntry))
                MergeAndSpawnOneWayPlatforms(line, worldY, oneWayEntry);

            for (int col = 0; col < line.Length; col++)
            {
                char c = line[col];
                if (c == '#' || c == '=' || c == '-') continue;

                if (TryGetEntry(c, out var entry))
                {
                    SpawnRegisteredElement(entry, col, worldY);
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
            "> = MovingPlatform\n" +
            "@ = SawBlade  f = FlyingEnemy  < = ConveyorBelt\n" +
            "S = Checkpoint  X = BreakableBlock\n" +
            "[ = ControllableBlocker";
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
    // 生成方法（仅保留合并地形与 M/T 特殊逻辑）
    // ═══════════════════════════════════════════════════
    /// <summary>
    /// S57b: 扫描一行中连续的指定字符，合并为单个长条实体方块。
    /// Ground('#') / Platform('=') 的颜色、排序、碰撞参数均来自 Registry Entry。
    /// </summary>
    private static void MergeAndSpawnSolidBlocks(string line, int worldY, char targetChar, AsciiElementEntry entry)
    {
        int col = 0;
        while (col < line.Length)
        {
            if (line[col] == targetChar)
            {
                int startCol = col;
                while (col < line.Length && line[col] == targetChar)
                {
                    col++;
                }
                int width = col - startCol;
                SpawnMergedSolidBlock(startCol, worldY, width, entry);
            }
            else
            {
                col++;
            }
        }
    }

    /// <summary>
    /// S57b: 生成一个合并后的长条实体方块。
    /// 命名约定仍保持 "{elementName}_{startX}_{y}_w{width}"，确保主题系统按前缀兼容。
    /// </summary>
    private static void SpawnMergedSolidBlock(int startX, int y, int width, AsciiElementEntry entry)
    {
        string blockName = string.IsNullOrEmpty(entry.elementName) ? "Ground" : entry.elementName;
        float centerX = startX * CELL_SIZE + (width - 1) * CELL_SIZE * 0.5f;
        float centerY = y * CELL_SIZE;

        GameObject go = new GameObject($"{blockName}_{startX}_{y}_w{width}");
        go.transform.position = new Vector3(centerX, centerY, 0);
        go.transform.parent = rootTransform;
        go.layer = groundLayerIndex;

        GameObject visual = CreateVisualChild(go.transform, entry.visualColor, entry.sortingOrder);
        Vector2 visualScale = entry.visualScale == Vector2.zero ? Vector2.one : entry.visualScale;
        visual.transform.localScale = new Vector3(width * visualScale.x * CELL_SIZE, visualScale.y * CELL_SIZE, 1f);

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        Vector2 colliderSize = entry.customColliderSize == Vector2.zero ? Vector2.one : entry.customColliderSize;
        col.size = new Vector2(width * colliderSize.x, colliderSize.y);
        col.offset = entry.customColliderOffset;
        col.isTrigger = entry.isTrigger;
    }

    /// <summary>
    /// S44c: 扫描一行中连续的 '-'，合并为单个长条 OneWayPlatform。
    /// 仍保留专用合并逻辑，但尺寸、颜色、组件来自 Registry。
    /// </summary>
    private static void MergeAndSpawnOneWayPlatforms(string line, int worldY, AsciiElementEntry entry)
    {
        int col = 0;
        while (col < line.Length)
        {
            if (line[col] == '-')
            {
                int startCol = col;
                while (col < line.Length && line[col] == '-')
                {
                    col++;
                }
                int width = col - startCol;
                SpawnMergedOneWayPlatform(startCol, worldY, width, entry);
            }
            else
            {
                col++;
            }
        }
    }

    /// <summary>
    /// S44c: 生成一个合并后的长条单向平台。
    /// OneWayPlatform 的组件和 PlatformEffector2D 依旧由脚本 RequireComponent 自动补齐。
    /// </summary>
    private static void SpawnMergedOneWayPlatform(int startX, int y, int width, AsciiElementEntry entry)
    {
        string blockName = string.IsNullOrEmpty(entry.elementName) ? "OneWayPlatform" : entry.elementName;
        float centerX = startX * CELL_SIZE + (width - 1) * CELL_SIZE * 0.5f;
        float centerY = y * CELL_SIZE;

        GameObject go = new GameObject($"{blockName}_{startX}_{y}_w{width}");
        go.transform.position = new Vector3(centerX, centerY, 0);
        go.transform.parent = rootTransform;
        go.layer = groundLayerIndex;

        GameObject visual = CreateVisualChild(go.transform, entry.visualColor, entry.sortingOrder);
        Vector2 visualScale = entry.visualScale == Vector2.zero ? new Vector2(1f, 0.3f) : entry.visualScale;
        visual.transform.localScale = new Vector3(width * visualScale.x * CELL_SIZE, visualScale.y * CELL_SIZE, 1f);

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        Vector2 colliderSize = entry.customColliderSize == Vector2.zero ? PhysicsMetrics.ONEWAY_COLLIDER_SIZE : entry.customColliderSize;
        col.size = new Vector2(width * colliderSize.x, colliderSize.y);
        col.offset = entry.customColliderOffset;
        col.isTrigger = entry.isTrigger;

        AttachConfiguredComponents(go, entry);
    }

    /// <summary>
    /// M 出生点特殊逻辑：只创建可视标记，不通用挂组件。
    /// </summary>
    private static void SpawnMarioSpawn(int x, int y)
    {
        CreateVisualMarker("MarioSpawn", x, y, COLOR_MARIO, "M");
    }

    /// <summary>
    /// T 出生点特殊逻辑：只创建可视标记，不通用挂组件。
    /// </summary>
    private static void SpawnTricksterSpawn(int x, int y)
    {
        CreateVisualMarker("TricksterSpawn", x, y, COLOR_TRICKSTER, "T");
    }

    // ═══════════════════════════════════════════════════
    // 通用创建辅助
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Registry 通用生成入口。除 Ground/Platform/OneWayPlatform 合并逻辑与 M/T 出生点外，
    /// 所有 ASCII 元素都通过此方法按 Entry 数据生成 Root/Visual/Collider/Components。
    /// </summary>
    private static void SpawnRegisteredElement(AsciiElementEntry entry, int gridX, int gridY)
    {
        if (entry == null || !entry.generateObject) return;

        if (entry.elementName == "MarioSpawn")
        {
            SpawnMarioSpawn(gridX, gridY);
            return;
        }
        if (entry.elementName == "TricksterSpawn")
        {
            SpawnTricksterSpawn(gridX, gridY);
            return;
        }

        GameObject go = CreateRegisteredBlock(entry, gridX, gridY);
        AttachConfiguredComponents(go, entry);
    }

    /// <summary>
    /// S37: 视碰分离重构 — 创建 Registry 驱动白盒元素。
    /// 结构: Root(物理层: BoxCollider2D + 逻辑组件) -> Visual(视觉层: SpriteRenderer)。
    /// 根物体 localScale 永远保持 (1,1,1)，视觉缩放由 Visual 子节点承担。
    /// </summary>
    private static GameObject CreateRegisteredBlock(AsciiElementEntry entry, int gridX, int gridY)
    {
        string elementName = string.IsNullOrEmpty(entry.elementName) ? "AsciiElement" : entry.elementName;
        GameObject go = new GameObject($"{elementName}_{gridX}_{gridY}");
        go.transform.position = new Vector3(gridX * CELL_SIZE, gridY * CELL_SIZE, 0);
        go.transform.parent = rootTransform;

        if (entry.isSolid && !entry.isTrigger)
            go.layer = groundLayerIndex;

        GameObject visual = CreateVisualChild(go.transform, entry.visualColor, entry.sortingOrder);
        Vector2 visualScale = entry.visualScale == Vector2.zero ? Vector2.one : entry.visualScale;
        visual.transform.localScale = new Vector3(visualScale.x * CELL_SIZE, visualScale.y * CELL_SIZE, 1f);

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = entry.customColliderSize == Vector2.zero ? Vector2.one : entry.customColliderSize;
        col.offset = entry.customColliderOffset;
        col.isTrigger = entry.isTrigger;
        return go;
    }

    private static GameObject CreateVisualChild(Transform parent, Color color, int sortingOrder)
    {
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = Vector3.zero;
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = CreateWhiteBoxSprite();
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return visual;
    }

    private static void AttachConfiguredComponents(GameObject go, AsciiElementEntry entry)
    {
        if (go == null || entry == null || entry.componentTypeNames == null) return;

        foreach (string componentTypeName in entry.componentTypeNames)
        {
            if (string.IsNullOrWhiteSpace(componentTypeName)) continue;
            Type componentType = ResolveComponentType(componentTypeName.Trim());
            if (componentType == null)
            {
                Debug.LogWarning($"[AsciiLevelGen] Component type '{componentTypeName}' for '{entry.elementName}' not found. Skipped.");
                continue;
            }
            if (!typeof(Component).IsAssignableFrom(componentType))
            {
                Debug.LogWarning($"[AsciiLevelGen] Type '{componentTypeName}' is not a Unity Component. Skipped.");
                continue;
            }
            if (go.GetComponent(componentType) == null)
            {
                go.AddComponent(componentType);
            }
        }

        // 向后兼容 FlyingEnemy：旧逻辑额外给 Rigidbody2D 设为 Kinematic 并冻结旋转。
        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        if (rb != null && entry.elementName == "FlyingEnemy")
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.freezeRotation = true;
        }
    }

    private static Type ResolveComponentType(string componentTypeName)
    {
        Type type = Type.GetType(componentTypeName);
        if (type != null) return type;

        type = Type.GetType($"UnityEngine.{componentTypeName}, UnityEngine");
        if (type != null) return type;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(componentTypeName);
            if (type != null) return type;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                types = Array.FindAll(ex.Types, t => t != null);
            }
            catch
            {
                continue;
            }

            foreach (Type candidate in types)
            {
                if (candidate.Name == componentTypeName)
                    return candidate;
            }
        }
        return null;
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
