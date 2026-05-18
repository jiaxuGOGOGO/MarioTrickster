using System.Collections.Generic;

/// <summary>
/// 关卡片段库 — 15 个经典 2D 平台跳跃局部片段 (S56: 新增 3 个展示新元素的片段; S144: 新增短桥读心房原型; S145: 新增四段白盒验证灰盒)
/// 
/// 核心设计:
///   - 每个片段是一个小型 ASCII 模板，可直接生成到场景
///   - 片段可【追加】到 Custom Template Editor 的文本框中，像搭积木一样拼装长关卡
///   - 所有字符严格对应 AsciiLevelGenerator 的字符映射表
///
/// 使用方式:
///   在 Level Studio → Level Design → "Custom Template Editor" 中点击按钮追加到文本框
///
/// Session 26: 新增
/// Session 26b: 精简为 5 个核心片段，删除过度设计
/// Session 43: 修复物理缺陷 — 弹跳深渊间距缩小到物理可达范围，
///             陷阱走廊地刺间距优化，敌人混战平台间距校准
///
/// 物理约束速查（来自 PhysicsMetrics.cs）：
///   MAX_JUMP_HEIGHT = 2.5 格（原地最高跳跃）
///   MAX_JUMP_DISTANCE = 4.5 格（满速平跳最大水平距离）
///   ASCII_MAX_GAP = 4 格（安全间隙上限）
///   ASCII_MAX_HEIGHT = 2 格（安全高台上限）
/// </summary>
public static class LevelSnippetLibrary
{
    /// <summary>关卡片段数据</summary>
    public class Snippet
    {
        public string name;           // 片段名称
        public string description;    // 一句话设计说明
        public string ascii;          // ASCII 模板内容
        public string MainRoute;      // 主路线设计意图
        public string ShadowRoute;    // 影子路线 / 备选路线设计意图
        public string TrapRoles;      // 机关角色与组合职责
        public string Budget;         // 路线 / 干预 / 风险预算
        public string TestGoal;       // 片段测试目标
        public int width;             // 宽度（字符数）
        public int height;            // 高度（行数）

        public Snippet(
            string name,
            string description,
            string ascii,
            string mainRoute = "",
            string shadowRoute = "",
            string trapRoles = "",
            string budget = "",
            string testGoal = "")
        {
            this.name = name;
            this.description = description;
            this.ascii = ascii;
            this.MainRoute = mainRoute;
            this.ShadowRoute = shadowRoute;
            this.TrapRoles = trapRoles;
            this.Budget = budget;
            this.TestGoal = testGoal;

            // ── 从 ASCII 文本中解析嵌入式元数据注释行 ──
            // 格式：以 "# Key: Value" 开头的行会被提取到对应字段。
            // 仅当构造参数未显式传入（为空）时，才从 ASCII 中解析补充。
            string[] lines = ascii.Split('\n');
            foreach (string line in lines)
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("# MainRoute:") && string.IsNullOrEmpty(this.MainRoute))
                    this.MainRoute = trimmed.Substring("# MainRoute:".Length).Trim();
                else if (trimmed.StartsWith("# ShadowRoute:") && string.IsNullOrEmpty(this.ShadowRoute))
                    this.ShadowRoute = trimmed.Substring("# ShadowRoute:".Length).Trim();
                else if (trimmed.StartsWith("# TrapRoles:") && string.IsNullOrEmpty(this.TrapRoles))
                    this.TrapRoles = trimmed.Substring("# TrapRoles:".Length).Trim();
                else if (trimmed.StartsWith("# Budget:") && string.IsNullOrEmpty(this.Budget))
                    this.Budget = trimmed.Substring("# Budget:".Length).Trim();
                else if (trimmed.StartsWith("# TestGoal:") && string.IsNullOrEmpty(this.TestGoal))
                    this.TestGoal = trimmed.Substring("# TestGoal:".Length).Trim();
            }

            this.height = lines.Length;
            this.width = 0;
            foreach (string line in lines)
                if (line.Length > this.width) this.width = line.Length;
        }
    }

    // ═══════════════════════════════════════════════════
    // 片段库（15 个核心片段）
    // ═══════════════════════════════════════════════════

    private static List<Snippet> snippets;

    /// <summary>获取所有片段</summary>
    public static List<Snippet> GetAllSnippets()
    {
        if (snippets == null) InitSnippets();
        return snippets;
    }

    private static void InitSnippets()
    {
        snippets = new List<Snippet>();

        // ── 1. 基础教学区 ──
        // 安全起点 + 第一个小跳跃 + 第一个敌人，教会玩家基本操作
        snippets.Add(new Snippet(
            "Tutorial Start (教学起点)",
            "安全起点 → 小跳跃 → 第一个敌人。适合放在关卡最左侧。",
            "..............................\n" +
            "..o.o.......o.........o.o.....\n" +
            "..............................\n" +
            "..............................\n" +
            "..M.................e.........\n" +
            "..##...####...##..############",
            mainRoute: "left-to-right, flat ground with single gap",
            shadowRoute: "none (单路线教学)",
            trapRoles: "e = 单个地面巡逻敌人, 无陷阱",
            budget: "单路线, 零压力, 纯教学",
            testGoal: "验证新手引导：跳跃间隙可达、第一个敌人可踩踏或跳过"
        ));

        // ── 2. 连续弹跳深渊 ──
        // 无地面，必须踩弹跳平台连续跳跃，掉落即死
        // S35 修复: 弹跳平台间距调整为物理可达，上方留出 3 格安全净空
        // S43 修复: 弹跳平台水平间距从 8 格缩小到 4 格（ASCII_MAX_GAP=4），
        //          垂直间距从 2 格缩小到 1 格，确保物理可达。
        //          原始设计间距 8 格远超 MAX_JUMP_DISTANCE=4.5 格，物理上不可能跨越。
        snippets.Add(new Snippet(
            "Bounce Abyss (弹跳深渊)",
            "无地面的弹跳平台连跳，掉落即死。高难度节奏挑战。",
            "............................\n" +
            "........................o.o.\n" +
            "........................===.\n" +
            "............................\n" +
            "............................\n" +
            "..................B.........\n" +
            "............................\n" +
            "............B...............\n" +
            "............................\n" +
            "......B.....................\n" +
            "............................\n" +
            "..B.........................\n" +
            "##..........................",
            mainRoute: "bottom-left to top-right, vertical ascending via bounce pads",
            shadowRoute: "none (单路线强制弹跳)",
            trapRoles: "B = 弹跳平台(唯一落脚点), 无地面即死亡深渊",
            budget: "单路线, 高节奏压力, 零容错",
            testGoal: "验证弹跳平台连跳物理可达性(间距≤4格), 掉落即死的节奏挑战"
        ));

        // ── 3. 多层陷阱走廊 ──
        // 地刺 + 火焰 + 摆锤组合，需要观察节奏穿越
        // S35 修复: 地刺前后增加安全缓冲格，平台边缘不再紧贴陷阱
        snippets.Add(new Snippet(
            "Trap Corridor (陷阱走廊)",
            "地刺+火焰+摆锤组合走廊。需要观察节奏，精准跳跃。",
            "....P.........P.........P.....\n" +
            "..............................\n" +
            "..............................\n" +
            "......~.........~.............\n" +
            "..............................\n" +
            "....==......==......==........\n" +
            "..^^...^^^......^^...^^.......\n" +
            "##############################",
            mainRoute: "left-to-right, mid-level platforms dodging traps",
            shadowRoute: "none (单路线穿越)",
            trapRoles: "P = 摆锤(动态节奏威胁), ~ = 火焰(定时伤害), ^^ = 地刺(静态伤害边界)",
            budget: "单路线, 中等压力, 需观察节奏窗口",
            testGoal: "验证陷阱组合走廊的节奏可穿越性, 安全缓冲格是否足够"
        ));

        // ── 4. 敌人+平台混战 ──
        // 多层平台上有巡逻敌人和弹跳怪，需要边跳边躲
        // S35 修复: 地刺与平台之间增加缓冲格
        // S43 修复: 确保所有平台间距在物理可达范围内
        snippets.Add(new Snippet(
            "Enemy Gauntlet (敌人混战)",
            "多层平台上的敌人混战。巡逻怪+弹跳怪，边跳边躲。",
            "..............................\n" +
            "..........o.o.o...............\n" +
            "..........-----...............\n" +
            ".....................E........\n" +
            ".....e..............####......\n" +
            "..######....e.........####....\n" +
            "............####...^^..####...\n" +
            "##############################",
            mainRoute: "left-to-right, multi-level platforms ascending then descending",
            shadowRoute: "none (单路线多层)",
            trapRoles: "e = 地面巡逻敌人, E = 弹跳怪(高威胁), ^^ = 地刺(平台间惩罚)",
            budget: "单路线, 高压力, 需边跳边躲",
            testGoal: "验证多层平台间距物理可达, 敌人密度下的生存空间"
        ));

        // ── 5. 锯片+传送带组合 (S56 新增) ──
        // 旋转锯片和传送带的组合挑战
        snippets.Add(new Snippet(
            "Saw & Conveyor (\u952f\u7247\u4f20\u9001\u5e26)",
            "\u65cb\u8f6c\u952f\u7247+\u4f20\u9001\u5e26\u7ec4\u5408\u3002\u9700\u8981\u8ba1\u7b97\u65f6\u673a\u548c\u901f\u5ea6\u3002S56\u65b0\u589e\u5143\u7d20\u5c55\u793a\u3002",
            "..............................\n" +
            "..............................\n" +
            "..........@.......@...........\n" +
            "..............................\n" +
            "....o.o.o.........o.o.o.......\n" +
            "..<<<<<<<<<...<<<<<<<<<.......\n" +
            "..............................\n" +
            "##############################",
            mainRoute: "left-to-right, conveyor belt platforms with overhead saws",
            shadowRoute: "none (单路线)",
            trapRoles: "@ = 旋转锯片(动态伤害), <<< = 传送带(向左推力速度干扰)",
            budget: "单路线, 中等压力, 需计算时机和速度补偿",
            testGoal: "验证传送带推力下的跳跃可达性, 锯片节奏窗口"
        ));

        // ── 6. 飞行敌人+检查点 (S56 新增) ──
        // 空中威胁和安全点的组合
        snippets.Add(new Snippet(
            "Sky Patrol (\u7a7a\u4e2d\u5de1\u903b)",
            "\u98de\u884c\u654c\u4eba\u7a7a\u4e2d\u5de1\u903b+\u68c0\u67e5\u70b9\u5b89\u5168\u533a\u3002S56\u65b0\u589e\u5143\u7d20\u5c55\u793a\u3002",
            "..............................\n" +
            "..........f.......f...........\n" +
            "..............................\n" +
            "....S.........S...........o...\n" +
            "..####...####...####...####...\n" +
            "..............................\n" +
            "..^^......^^......^^..........\n" +
            "##############################",
            mainRoute: "left-to-right, platform hopping with aerial threats",
            shadowRoute: "none (单路线)",
            trapRoles: "f = 飞行敌人(空中巡逻威胁), ^^ = 地刺(落地惩罚), S = 检查点(安全区)",
            budget: "单路线, 中等压力, 检查点提供容错",
            testGoal: "验证飞行敌人巡逻路径下的平台跳跃安全窗口"
        ));

        // ── 7. 可破坏方块迷宫 (S56 新增) ──
        // 从下方撞击开路
        snippets.Add(new Snippet(
            "Breakable Maze (\u7834\u574f\u8ff7\u5bab)",
            "\u53ef\u7834\u574f\u65b9\u5757\u5c01\u5835\u901a\u8def\uff0c\u4ece\u4e0b\u65b9\u649e\u51fb\u5f00\u8def\u3002S56\u65b0\u589e\u5143\u7d20\u5c55\u793a\u3002",
            "..............................\n" +
            "..o.o.o.......o.o.o...........\n" +
            "..XXXXXX......XXXXXX..........\n" +
            "..............................\n" +
            "..........o.o.o...............\n" +
            "..........XXXXXX..............\n" +
            "..............................\n" +
            "##############################",
            mainRoute: "left-to-right, break through X blocks to progress",
            shadowRoute: "none (单路线, 上下两层可破坏区域)",
            trapRoles: "X = 可破坏方块(封堵通路, 从下方撞击开路)",
            budget: "单路线, 低压力, 探索型",
            testGoal: "验证可破坏方块的撞击开路机制, 多层封堵的路径规划"
        ));

        // ── 8. 短桥读心房 (S144 新增) ──
        // 双路线三节点原型：上层短桥（快但危险，地刺密布）、中间单向平台过渡、
        // 下层管道绕行（安全但耗时）。5 个可附身热点供 Trickster 暗线换位。
        // 验证目标：暗线网络 (connectedUnderlineNodes) + 五段生命周期
        //          (Idle → Telegraph → Active → Recovery → Cooldown → Exhausted)
        //
        // # UnderlineLinks (生成后需在 Inspector 中连入 connectedUnderlineNodes):
        // #   BridgeBlockL (X@row2,col3) <-> BridgeBounce (B@row2,col9)
        // #   BridgeBounce (B@row2,col9) <-> BridgeBlockR (X@row2,col15)
        // #   BridgeBlockL (X@row2,col3) <-> LowerBounce  (B@row6,col4)  [跨层暗线]
        // #   LowerBounce  (B@row6,col4) <-> LowerBlock   (X@row6,col11)
        //
        // 物理校验：上层桥连续无间隙；中间层间隙 2 格；下层最大间隙 3 格；
        //          垂直层间距均为 2 格（row2→row4, row4→row6, row6→row8）。
        snippets.Add(new Snippet(
            "S2_Duel_TrapBridge_01 (短桥读心房)",
            "双路线三节点原型。上层短桥(快但危险)=连续平台+地刺+弹跳+可破坏方块；" +
            "下层管道绕行(安全但耗时)。5 个可附身热点(B×2 + X×3)供 Trickster 暗线换位。" +
            "生成后请在 Inspector 连接 UnderlineLinks: " +
            "BridgeBlockL<->BridgeBounce, BridgeBounce<->BridgeBlockR, " +
            "BridgeBlockL<->LowerBounce(跨层), LowerBounce<->LowerBlock。",
            "..............................\n" +
            ".o..o.o.o.o.o.o..o............\n" +
            ".==X==^^=B=^^==X==............\n" +
            "..............................\n" +
            "..---..---..---...........o...\n" +
            "..............................\n" +
            ".##.B.####.X.##...####.##.....\n" +
            "..............................\n" +
            "##############################",
            mainRoute: "upper bridge, left-to-right, fast but dangerous (spikes + bounce pads + breakable blocks)",
            shadowRoute: "lower pipe route, left-to-right, safe but slow (solid platforms)",
            trapRoles: "^^ = 地刺(上层桥面惩罚), B = 弹跳平台/暗线附身点, X = 可破坏方块/暗线附身点, --- = 单向平台(中间过渡层)",
            budget: "双路线预算, 上层高风险高收益, 下层安全保底; 5个暗线热点供Trickster换位",
            testGoal: "验证暗线网络(connectedUnderlineNodes)连接 + 五段生命周期状态机 + 双路线物理可达"
        ));

        // ── 9. Santorini 式临时封路机关 (S53 原型 B) ──
        // 上下双路线：上层主路更短但放置 ControllableBlocker；下层绕行更安全但更慢。
        // 验证目标：Windup 可通过 + Scan 反制减半 + Active 前路线预算 TryDegradeRoute。
        snippets.Add(new Snippet(
            "S2_Duel_Blocker_01 (临时封路机关)",
            "上下双路线封路原型。上层主路放置 [ ControllableBlocker，Trickster 可临时封路；" +
            "下层绕行保证双路预算不死锁，Mario 可用扫描在 Windup 阶段将 Active 时间减半。",
            "..............................\n" +
            ".o.o.o.........o.o.o..........\n" +
            "..====[====.....====..........\n" +
            "..............................\n" +
            "..---....---....---...........\n" +
            "..............................\n" +
            "..############..############..\n" +
            "..............................\n" +
            "##############################",
            mainRoute: "upper route, left-to-right, shorter but blocked by [ ControllableBlocker",
            shadowRoute: "lower route via solid platforms, safe bypass when upper blocked",
            trapRoles: "[ = ControllableBlocker(Trickster临时封路), --- = 单向平台(中间过渡)",
            budget: "双路线预算, Trickster可短暂降级主路但不得硬锁; Mario可用Scan在Windup阶段反制",
            testGoal: "验证封路机关Windup可通过 + Scan反制减半 + Active前路线预算TryDegradeRoute"
        ));

        // ── 10. Onitama 式公开下一状态机关 (S53 原型 C) ──
        // 上下双路线：上层主路放置 StateQueueTrap，UI 始终公开 Current / Next；
        // Trickster 可高代价强行跳过当前状态，制造规律破坏后的破绽期。
        snippets.Add(new Snippet(
            "S2_Duel_QueueTrap_01 (公开队列机关)",
            "上下双路线队列机关原型。上层主路放置 ] StateQueueTrap，机关公开显示 Current/Next；" +
            "Trickster 的 Activate 只会强行跳到下一状态，并追加高 Suspicion/Evidence/Heat 与长 Recovery。",
            "..............................\n" +
            ".o.o.o.........o.o.o..........\n" +
            "..====]====.....====..........\n" +
            "..............................\n" +
            "..---....---....---...........\n" +
            "..............................\n" +
            "..############..############..\n" +
            "..............................\n" +
            "##############################",
            mainRoute: "upper route, left-to-right, shorter but has ] StateQueueTrap",
            shadowRoute: "lower route via solid platforms, safe bypass",
            trapRoles: "] = StateQueueTrap(公开Current/Next状态, Trickster可强行跳状态但代价高)",
            budget: "双路线预算, 机关公开透明但变奏不可预测; Trickster激活追加Suspicion/Evidence/Heat",
            testGoal: "验证队列机关状态公开显示 + Trickster跳状态的高代价惩罚 + 双路线不死锁"
        ));

        // ── 11. S53 四段白盒验证 1：演示房 ──
        // 无 Trickster 压力：单一路线展示基础地刺/摆锤，供玩家读取灰盒危险节奏。
        snippets.Add(new Snippet(
            "S2_Validation_1_Demo (白盒验证1-演示房)",
            "四段白盒验证第 1 房。无 Trickster 压力，单一路线展示基础地刺与摆锤；" +
            "水平安全跨度控制在 4 格内，垂直高度不超过 2 格。",
            "..............................\n" +
            "....P...............P.........\n" +
            "..............................\n" +
            "...o.o.........o.o............\n" +
            "..=====....=====....=====.....\n" +
            ".....---....---....---........\n" +
            "......^^...........^^.........\n" +
            "..##########################..\n" +
            "##############################",
            mainRoute: "left-to-right, multi-platform with pendulums overhead",
            shadowRoute: "none (单路线演示, 无Trickster压力)",
            trapRoles: "P = 摆锤(动态节奏展示), ^^ = 地刺(静态危险标记), --- = 单向平台(垂直过渡)",
            budget: "单路线, 零对抗压力, 纯灰盒节奏展示",
            testGoal: "验证演示房作为教学样板: 玩家可安全读取危险节奏, 水平跨度≤4格, 垂直≤2格"
        ));

        // ── 12. S53 四段白盒验证 2：干扰房 ──
        // 低压干扰：上下双路线，主路放置临时封路机关 [，下路保留安全绕行。
        snippets.Add(new Snippet(
            "S2_Validation_2_Interfere (白盒验证2-干扰房)",
            "四段白盒验证第 2 房。低压双路线干扰，主路放置 [ 临时封路机关；" +
            "下层绕行保证路线预算不死锁，所有平台间距保持物理安全。",
            "..............................\n" +
            "..o.o.........o.o.............\n" +
            "..====[====....====...........\n" +
            "..............................\n" +
            "..---....---....---...........\n" +
            "..............................\n" +
            "..############..############..\n" +
            "..............................\n" +
            "##############################",
            mainRoute: "upper route, left-to-right, blocked by [ ControllableBlocker",
            shadowRoute: "lower route via solid platforms, guaranteed safe bypass",
            trapRoles: "[ = ControllableBlocker(低压干扰, Trickster可封路)",
            budget: "双路线预算, 低压干扰级别, 下层绕行保证不死锁",
            testGoal: "验证低压干扰下路线预算不死锁, 所有平台间距物理安全"
        ));

        // ── 13. S53 四段白盒验证 3：反制房 ──
        // 高压反制：公开队列机关 ] + 暗线节点，诱饵金币引导 Mario 骗触发。
        snippets.Add(new Snippet(
            "S2_Validation_3_Counter (白盒验证3-反制房)",
            "四段白盒验证第 3 房。高压反制，加入 ] 公开队列机关、[ 封路机关与 X/B 暗线节点；" +
            "诱饵金币鼓励 Mario 试探并骗出 Trickster 的高代价跳状态。",
            "..............................\n" +
            "..o.o.o....o.o.o....o.o.......\n" +
            "..==X==..]==..[==B==..........\n" +
            "..............................\n" +
            "..---....---....---...........\n" +
            "..............................\n" +
            "..##B###..##X###..##[###......\n" +
            "..............................\n" +
            "##############################",
            mainRoute: "upper route, left-to-right, multiple traps (] queue + [ blocker + X/B underline nodes)",
            shadowRoute: "lower route via solid platforms with B/X underline nodes",
            trapRoles: "] = 队列机关, [ = 封路机关, X = 可破坏暗线节点, B = 弹跳暗线节点, o = 诱饵金币",
            budget: "双路线预算, 高压反制级别, 诱饵金币鼓励试探骗出Trickster高代价操作",
            testGoal: "验证高压反制房: 多机关叠加不死锁, 暗线节点可连接, 诱饵金币引导博弈"
        ));

        // ── 14. S53 四段白盒验证 4：实战房 ──
        // 拿宝撤离：放置 o 与 G，配合场景 AlarmCrisisDirector 的扫描波形成高压返程。
        snippets.Add(new Snippet(
            "S2_Validation_4_Combat (白盒验证4-实战房)",
            "四段白盒验证第 4 房。实战拿宝撤离，放置 o LootObjective 目标与 G EscapeGate 出口；" +
            "配合场景 AlarmCrisisDirector 扫描波、] 队列机关和 [ 封路机关形成高压局面。",
            "..............................\n" +
            "..o.....P.......]......G......\n" +
            "..====....====....====........\n" +
            "..............................\n" +
            "..---....---....---....---....\n" +
            "..............................\n" +
            "..##[###..##^^##..##P###......\n" +
            "..............................\n" +
            "##############################",
            mainRoute: "上层主路线：从左侧进入后沿高台快速拿取 o / LootObjective，再向右压到 G / EscapeGate；路线短、收益高，但会暴露在 ] 队列机关与扫描压力下。",
            shadowRoute: "下层影子路线：通过 [ 封路机关、^^ 危险带与 P 摆锤形成慢速绕行 / 返程压力，确保 Mario 被干扰后仍有可走解法。",
            trapRoles: "] = 公开队列机关，制造可读但会变奏的返程节奏；[ = 临时封路机关，消耗路线预算但不得硬锁；^^ = 固定伤害边界；P = 动态摆锤压力；o/G = 拿宝撤离目标链。",
            budget: "双路线预算：任意时刻至少保留一条可通行目标链；Trickster 可短暂降级主路，但必须通过 ShadowRoute、倒计时或补偿保持 Mario 行动权。",
            testGoal: "验证 S2 实战房是否从‘看起来像实战房’升级为可解释样板房：生成 Loot/Escape 目标链、触发扫描危机、记录路线预算消耗，并供 AI Arena 固定跑局。"
        ));

        // ── 15. 终点冲刺 ──
        // 崩塌平台 + 移动平台 + 终点旗帜，紧张的最后冲刺
        snippets.Add(new Snippet(
            "Final Sprint (终点冲刺)",
            "崩塌平台+移动平台的紧张冲刺，到达终点旗帜。",
            "..............................\n" +
            "..........................o.o.\n" +
            ".........................===..\n" +
            "..............................\n" +
            "...........>..............G...\n" +
            "..............................\n" +
            "...CC...CC..........CC..###...\n" +
            "##############################",
            mainRoute: "left-to-right, collapsing platforms to moving platform to goal",
            shadowRoute: "none (单路线冲刺)",
            trapRoles: "CC = 崩塌平台(限时落脚), > = 移动平台(动态载具), G = 终点旗帜",
            budget: "单路线, 高节奏压力, 崩塌平台限时+移动平台时机",
            testGoal: "验证终点冲刺节奏: 崩塌平台存活时间足够跳跃, 移动平台可接住玩家"
        ));
    }
}
