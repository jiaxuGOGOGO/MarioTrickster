using System.Collections.Generic;

/// <summary>
/// S188：关卡元素说明书（唯一来源）。给"人"看的元数据：中英文名、它是干什么的、摆在哪、美术换图用的主题键。
/// 物理数据（碰撞体、组件、颜色）仍只在 AsciiElementRegistry；这里不重复。测试保证两边一一对应：
///   - Registry 每个字符都必须在这里有说明（新增元素忘了写说明 → 测试失败）；
///   - themeKey 必须等于 Registry 的 elementName（= 生成物体的名称前缀 = LevelThemeProfile 插槽键），美术拖图才能对上；
///   - 所有非地形元素在 LevelThemeProfile 里都有换图插槽。
/// 参考：LDtk 的"实体定义"做法——每种可摆放的东西有一份定义（名字、显示、约束，如 PlayerStart 只能有 1 个），
///   关卡里只放实例（https://ldtk.io/docs/general/editor-components/entities/）。这里用同样思路，但保持项目的 ASCII + Registry 架构不变。
/// </summary>
public static class ElementCatalog
{
    public enum Role
    {
        Terrain,      // 地形：地面、墙、台面
        Scenery,      // 场景摆件：只阻挡/遮挡/装饰，不可操控
        PlayerPrank,  // 玩家机关：捣蛋者伪装后按 L 触发（平时安全）
        AutoHazard,   // 自动危险：自己会伤人（第 1 步不用）
        Objective,    // 目标：宝物、出口
        Spawn,        // 出生点
        Movement,     // 移动类：弹跳、传送带、移动平台
        Enemy,        // 敌人（第 1 步不用）
        Special       // 其他：暗道、假墙、检查点、队列机关
    }

    public sealed class Info
    {
        public char ch;
        public string themeKey;   // = Registry.elementName = 物体名前缀 = 主题插槽键
        public string zh, en;
        public Role role;
        public string what;       // 它是干什么的（一句话）
        public string place;      // 摆在哪（一句话）
        public bool unique;       // 整个关卡只能有 1 个
        public bool needsSupport; // 脚下（正下方）必须是实心
        public bool step1;        // 第 1 步恶作剧房间允许使用
        public int muzzle;        // 大炮：炮口方向 +1 右 / -1 左（0 = 不是炮）
        public ArtFit fit;        // 美术换图时怎么贴（见 ArtFit）
    }

    /// <summary>
    /// S191：美术换图的贴法（接到项目原有的 SpriteAutoFit 视碰分离适配，碰撞体永远不动）：
    ///   Tile  = 平铺（地面/墙/台面/桥：连续多格拼成一条，图按原尺寸重复，不拉伸）→ SpriteAutoFit.Tiled；
    ///   Fit   = 等比缩放放进格子（箱子、大炮、火、宝物、装饰：不变形，留边）→ 按"显示框"等比缩放；
    ///   Stretch = 拉满显示框（出口光柱等可以拉伸的效果）→ SpriteAutoFit.Scaled；
    ///   None  = 没有图（出生点、空气）。
    /// 显示框 = Registry 的 visualScale 格数（白盒尺寸），所以美术只要按"建议图尺寸"画，放进去就对。
    /// </summary>
    public enum ArtFit { None, Tile, Fit, Stretch }

    /// <summary>编辑器/图例里的显示颜色（Registry 没有颜色的元素，如出生点，用这里的）。</summary>
    public static UnityEngine.Color EditorColor(char c)
    {
        switch (c)
        {
            case 'M': return new UnityEngine.Color(0.90f, 0.20f, 0.20f); // 与场景里马里奥同色
            case 'T': return new UnityEngine.Color(0.20f, 0.40f, 0.90f); // 与场景里捣蛋者同色
            case '1': case '2': case '3': return new UnityEngine.Color(0.45f, 0.40f, 0.60f);
        }
        var e = AsciiElementRegistry.GetDefault().GetEntry(c);
        return e != null ? e.visualColor : new UnityEngine.Color(0.13f, 0.15f, 0.19f);
    }

    /// <summary>在这个底色上写字用黑还是白：按 WCAG 相对亮度算两种对比度，取对比度更高的（黄、白、浅蓝上用黑字）。</summary>
    public static UnityEngine.Color TextColorOn(UnityEngine.Color bg)
    {
        var dark = new UnityEngine.Color(0.08f, 0.08f, 0.1f);
        return ContrastRatio(bg, dark) >= ContrastRatio(bg, UnityEngine.Color.white) ? dark : UnityEngine.Color.white;
    }

    /// <summary>WCAG 2 对比度（1–21）。</summary>
    public static float ContrastRatio(UnityEngine.Color a, UnityEngine.Color b)
    {
        float la = RelLum(a), lb = RelLum(b);
        return (UnityEngine.Mathf.Max(la, lb) + 0.05f) / (UnityEngine.Mathf.Min(la, lb) + 0.05f);
    }

    private static float RelLum(UnityEngine.Color c)
    {
        float R(float v) => v <= 0.03928f ? v / 12.92f : UnityEngine.Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
        return 0.2126f * R(c.r) + 0.7152f * R(c.g) + 0.0722f * R(c.b);
    }

    /// <summary>大炮炮口前方至少空出几格（否则炮弹一出膛就撞墙/箱子）。</summary>
    public const int MuzzleClearCells = 3;
    public const int SpringHeadroomCells = 4;
    public const int MaxPoolCells = 3;

    private static readonly Info[] all =
    {
        I('#', "Ground", "地面", "Ground", Role.Terrain, "实心地面，自动合并成大块。", "放在最底层；角色站在它上面。", step1: true),
        I('=', "Platform", "平台", "Platform", Role.Terrain, "实心平台（四面都挡）。", "悬空或贴墙。", step1: true),
        I('W', "Wall", "墙", "Wall", Role.Terrain, "实心墙，挡路也挡视线。", "房间边界、分区高墙。", step1: true),
        I('-', "OneWayPlatform", "单向台面", "One-way ledge", Role.Terrain, "从下面能跳上去，站得住；不挡视线。", "悬空，给上下两层路线。", step1: true),
        I('c', "Crate", "箱子", "Crate", Role.Scenery, "实心箱子：挡路、挡视线，马里奥会跳过去。可在它后面躲。", "放在地面上（脚下要实心）。", needsSupport: true, step1: true),
        I('b', "Bush", "草丛", "Bush", Role.Scenery, "能穿过；挡视线（马里奥走进同一丛才看得见你）。会晃——有时只是风。", "放在地面上。", needsSupport: true, step1: true),
        I('d', "Decor", "装饰", "Decor", Role.Scenery, "纯装饰，不挡路不挡视线。换主题时换成旋转木马、路灯、树等。", "任意位置。", step1: true),
        I('C', "CollapsingPlatform", "塌桥", "Collapse bridge", Role.PlayerPrank, "捣蛋者按 L 让它塌；桥下有人时不会重新长出来。", "横跨在坑上；坑里要留出跳出来的路。", step1: true),
        I('[', "ControllableBlocker", "封路墙", "Blocker wall", Role.PlayerPrank, "捣蛋者按 L 升起一堵墙，挡住路 3.5 秒，不伤人。", "放在必经的门洞/窄道（脚下要实心）。", needsSupport: true, step1: true),
        I('~', "FireTrap", "火", "Fire", Role.PlayerPrank, "平时安全；捣蛋者按 L 喷火，烧到马里奥会晕。", "放在地面上（脚下要实心）。", needsSupport: true, step1: true),
        I('J', "SpringPad", "弹簧板", "Spring pad", Role.PlayerPrank, "平时是普通地面；捣蛋者按 L 把站在上面的马里奥弹上天（空中不能动）→ 在落点摆好火/塌桥 = 浮空连招。", "放在地面层；正上方至少空 4 格（弹起约 3 格高，别弹到天花板）。", step1: true),
        I('x', "CrackFloor", "裂缝地板", "Crack floor", Role.PlayerPrank, "平时是实心地面；捣蛋者按 L 打碎，站在上面的人掉到下一层（本回合不复原）。多层楼/地下监狱的'凿地板'。", "铺在楼层之间；碎后下面那层必须能走回出口（死局检查会查）。", step1: true),
        I('n', "BananaPeel", "香蕉皮", "Banana peel", Role.PlayerPrank, "平时是地上的装饰（可穿过）；捣蛋者按 L 后，踩上去的马里奥会朝前滑出约 3–4 格（打乱落点）→ 滑进火/裂缝前。", "放在地面上，前方留出滑行空间。", needsSupport: true, step1: true),
        I('|', "OneWayDoor", "捷径门", "Shortcut door", Role.Special, "只能从一侧（默认右侧）贴近推开，开了本回合一直开着。魂系'从另一边打开的门'：先绕远路，再回头打通捷径。", "放在两个区域之间的墙洞里；关着时整张图也必须能通关（死局检查按关着算）。", step1: true),
        I('%', "CrackedWall", "裂墙", "Cracked wall", Role.Special, "看起来有裂纹的墙：捣蛋者现形按 B 砸开（马里奥听得见）、炮弹打开、被弹飞的人撞开。本回合不复原。", "藏秘密通路/捷径；不破也必须能通关（死局检查按墙算）。", step1: true),
        I('O', "Vent", "通风管", "Vent", Role.Special, "捣蛋者站在管口按 ↓ 钻到配对的管口（按上→下、左→右编号 1↔2、3↔4）。马里奥进不去，但离得近听得见。", "成对摆放，放在地面上；一个在楼上一个在楼下最有用。", needsSupport: true, step1: true),
        I('w', "PoisonPool", "毒池", "Poison pool", Role.Terrain, "能走但减速，每隔一会儿让人晕一下（不扣命）。马里奥会被拖慢——你的伏击窗口。", "铺在地面上，宽不超过 3 格（保证能走出来）。", needsSupport: true, step1: true),
        I('g', "Glue", "黏胶", "Glue", Role.Terrain, "踩上去移动变慢、跳不高，离开就恢复。放在楼梯口前最狠。", "铺在地面上；别放在必须跳高的台阶前（会跳不上去）。", needsSupport: true, step1: true),
        I('Y', "SnareTrap", "绳套", "Snare", Role.PlayerPrank, "踩到就被绳子倒吊 10 秒再掉下来——马里奥中了是你的机会，你自己踩到也会中。伪装在旁按 L = 重新装好 / 放人。", "放在地面上；正上方至少空 2 格（吊起来不能卡墙）。", needsSupport: true, step1: true),
        I('?', "PickupSpot", "道具箱", "Pickup", Role.Special, "每局随机亮起几个：谁先碰到归谁，同一个箱子给你和给马里奥效果不同（反转变数）。", "放在路线附近的地面上；多放几个，每局只亮一部分。", needsSupport: true, step1: true),
        I('U', "OilBarrel", "油桶", "Oil barrel", Role.Special, "实心障碍：被喷火、炸弹、炮弹或另一个油桶爆炸点燃，0.8 秒后爆炸（炸毁周围、伤双方、引爆旁边的油桶 → 连锁）。", "放在火旁、裂墙旁、马里奥路线旁；不炸也必须能通关（死局检查按实心算）。", needsSupport: true, step1: true),
        I('Q', "IronCage", "铁笼", "Iron cage", Role.PlayerPrank, "悬在头顶的笼子：伪装在旁按 L 落下，关住正下方的人 3 秒后自动打开（每局一次）。炸弹能把笼子炸开。", "放在马里奥必经的地面上；头顶要空 2 格（笼子挂在上面）。", needsSupport: true, step1: true),
        I('R', "Tripwire", "绊线", "Tripwire", Role.Special, "地上一根细线：马里奥踩到绊一下（0.4 秒），并**启动你用 F 布置好的连锁**。每局一次；你自己踩不触发。", "放在连锁第一环前 1–3 格，马里奥必经的地面上。", needsSupport: true, step1: true),
        I('K', "Cannon", "大炮（朝右）", "Cannon (→)", Role.PlayerPrank, "伪装在旁按 L 开一炮（每局 1 发）；打完后站进炮口把自己打飞逃跑。", "地面上，炮口前方至少空 3 格。", needsSupport: true, step1: true, muzzle: 1),
        I('k', "Cannon", "大炮（朝左）", "Cannon (←)", Role.PlayerPrank, "同上，炮口朝左。", "地面上，炮口前方至少空 3 格。", needsSupport: true, step1: true, muzzle: -1),
        I('i', "RoomLamp", "灯", "Lamp", Role.PlayerPrank, "夜里的光源：一圈之内马里奥看得见。捣蛋者伪装在旁按 L 灭灯 8 秒（这一片变暗，能溜过去 / 荡过去）。白天没区别。", "放在空中或地面上（不挡路）；夜里要照到他必经的路口才有意思。", needsSupport: false, step1: true),
        I('v', "GrassGround", "草地", "Grass ground", Role.Terrain, "实心地面，上面长着草：捣蛋者在草地上遁地（U）不会拱出土包，马里奥看不见。炸弹炸不开。", "铺在地面那一层（代替 #）；草地和裸地交替 = 遁地路线有藏有露。", step1: true),
        I('z', "ArtSlot1", "素材槽 1（默认毒池·掉半格）", "Art slot 1", Role.Terrain, "进去每 1.2 秒掉半格心、减速到 0.6 倍（你和马里奥都会中）。长相和互动在「素材包」里改：拖一张图 + 选掉半格 / 一格 / 回血 / 减速 / 晕。", "铺在地面上，宽不超过 3 格（保证晕了也能走出来）。", needsSupport: true, step1: true), // S245
        I('Z', "ArtSlot2", "素材槽 2（默认荆棘·掉一格）", "Art slot 2", Role.Terrain, "进去每 1.6 秒掉一格心、晕 0.25 秒。长相和互动在「素材包」里改。", "铺在地面上，宽不超过 3 格（保证晕了也能走出来）。", needsSupport: true, step1: true), // S245
        I('a', "ArtSlot3", "素材槽 3（默认回血泉）", "Art slot 3", Role.Terrain, "站在里面每 2 秒回半格心。长相和互动在「素材包」里改。", "铺在地面上，宽不超过 3 格（保证晕了也能走出来）。", needsSupport: true, step1: true), // S245
        I('r', "ArtSlot4", "素材槽 4（默认蛛网·减速）", "Art slot 4", Role.Terrain, "进去减速到 0.45 倍，不掉血。长相和互动在「素材包」里改。", "铺在地面上，宽不超过 3 格（保证晕了也能走出来）。", needsSupport: true, step1: true), // S245
        I('o', "Collectible", "宝物", "Loot", Role.Objective, "马里奥要拿的宝物（实战房里自动变成 LootObjective）。", "放在离出口远的一端，只能有 1 个。", unique: true, step1: true),
        I('G', "GoalZone", "出口", "Exit", Role.Objective, "马里奥拿宝后要回到这里。", "放在起点附近，只能有 1 个。", unique: true, step1: true),
        I('M', "MarioSpawn", "马里奥出生点", "Mario spawn", Role.Spawn, "马里奥从这里出发。", "脚下要实心，只能有 1 个。", unique: true, needsSupport: true, step1: true),
        I('T', "TricksterSpawn", "捣蛋者出生点", "Trickster spawn", Role.Spawn, "你（捣蛋者）从这里出发。", "脚下要实心，只能有 1 个。", unique: true, needsSupport: true, step1: true),
        I('.', "Air", "空气", "Air", Role.Terrain, "什么都没有。", "—", step1: true),
        I(' ', "Space", "空格", "Space", Role.Terrain, "同空气。", "—", step1: true),
        I('^', "SpikeTrap", "地刺", "Spikes", Role.AutoHazard, "周期伸缩自动伤人。", "第 1 步房间不用（平时必须安全）。"),
        I('P', "PendulumTrap", "摆锤", "Pendulum", Role.AutoHazard, "自动摆动的锤子。", "锚点在上方，第 1 步不用。"),
        I('@', "SawBlade", "锯片", "Saw", Role.AutoHazard, "旋转锯片，碰到就伤。", "第 1 步不用。"),
        I(']', "StateQueueTrap", "队列机关", "Queue trap", Role.Special, "公开显示当前/下一状态的机关。", "第 1 步不用。"),
        I('B', "BouncyPlatform", "弹跳台", "Bouncer", Role.Movement, "踩上去弹飞。", "第 1 步不用。"),
        I('>', "MovingPlatform", "移动平台", "Moving platform", Role.Movement, "来回移动的平台。", "第 1 步不用。"),
        I('<', "ConveyorBelt", "传送带", "Conveyor", Role.Movement, "站上去会被带着走。", "第 1 步不用。"),
        I('X', "BreakableBlock", "可破坏方块", "Breakable", Role.Special, "从下面顶破。", "第 1 步不用。"),
        I('F', "FakeWall", "假墙", "Fake wall", Role.Special, "看起来是墙，其实能穿过。", "第 1 步不用。"),
        I('H', "HiddenPassage", "暗道入口", "Hidden passage", Role.Special, "按 S 进入暗道传送。", "第 1 步不用。"),
        I('S', "Checkpoint", "检查点", "Checkpoint", Role.Special, "更新重生点。", "第 1 步不用。"),
        I('E', "BouncingEnemy", "弹跳怪", "Bouncing enemy", Role.Enemy, "踩头弹起，侧碰受伤。", "第 1 步不用。"),
        I('e', "SimpleEnemy", "小怪", "Simple enemy", Role.Enemy, "来回走的小怪。", "第 1 步不用。"),
        I('f', "FlyingEnemy", "飞行怪", "Flying enemy", Role.Enemy, "空中飞行的怪。", "第 1 步不用。"),
    };

    private static Dictionary<char, Info> byChar;

    private static Info I(char ch, string key, string zh, string en, Role role, string what, string place,
        bool unique = false, bool needsSupport = false, bool step1 = false, int muzzle = 0) =>
        new Info { ch = ch, themeKey = key, zh = zh, en = en, role = role, what = what, place = place,
                   unique = unique, needsSupport = needsSupport, step1 = step1, muzzle = muzzle, fit = DefaultFit(key, role) };

    /// <summary>贴法默认值：地形与桥/台面类平铺；出生点/空气无图；出口拉伸；其余等比放进格子。</summary>
    private static ArtFit DefaultFit(string key, Role role)
    {
        switch (key)
        {
            case "Air": case "Space": case "MarioSpawn": case "TricksterSpawn": return ArtFit.None;
            case "Ground": case "Platform": case "Wall": case "OneWayPlatform": case "CollapsingPlatform":
            case "ConveyorBelt": case "BouncyPlatform": case "MovingPlatform": case "BreakableBlock": case "FakeWall": case "CrackFloor": case "CrackedWall": return ArtFit.Tile;
            case "GoalZone": return ArtFit.Stretch;
            default: return ArtFit.Fit;
        }
    }

    /// <summary>建议图片像素尺寸（按项目标准 PPU 32：1 格 = 32 像素；显示框 = Registry.visualScale）。平铺类给单格尺寸。</summary>
    public static string SuggestedPixels(char c, int ppu = 32)
    {
        var info = Get(c);
        var e = AsciiElementRegistry.GetDefault().GetEntry(c);
        if (info == null || info.fit == ArtFit.None || e == null) return "—";
        var v = e.visualScale == UnityEngine.Vector2.zero ? UnityEngine.Vector2.one : e.visualScale;
        if (info.fit == ArtFit.Tile) return $"{ppu}×{UnityEngine.Mathf.RoundToInt(UnityEngine.Mathf.Max(v.y, 0.25f) * ppu)} 一格（自动平铺）";
        return $"{UnityEngine.Mathf.RoundToInt(v.x * ppu)}×{UnityEngine.Mathf.RoundToInt(v.y * ppu)}";
    }

    public static IReadOnlyList<Info> All => all;

    /// <summary>S238：登记了字符却没写说明书的（新元素忘了写说明 → 非空）。替代旧探索计划里的 MissingFromCatalog。</summary>
    public static List<string> Unexplained(IEnumerable<char> registryChars)
    {
        var o = new List<string>();
        foreach (var c in registryChars) if (c != ' ' && Get(c) == null && !o.Contains(c.ToString())) o.Add(c.ToString());
        return o;
    }

    public static Info Get(char c)
    {
        if (byChar == null)
        {
            byChar = new Dictionary<char, Info>();
            foreach (var i in all) byChar[i.ch] = i;
        }
        byChar.TryGetValue(c, out var info);
        return info;
    }

    /// <summary>按主题键找说明（大炮左右两个字符共用一个键，返回第一个）。</summary>
    public static Info ByKey(string key)
    {
        foreach (var i in all) if (i.themeKey == key) return i;
        return null;
    }

    public static string RoleName(Role r)
    {
        switch (r)
        {
            case Role.Terrain: return "地形 Terrain";
            case Role.Scenery: return "场景摆件 Scenery（不可操控）";
            case Role.PlayerPrank: return "玩家机关 Your pranks（伪装后按 L）";
            case Role.AutoHazard: return "自动危险 Auto hazards";
            case Role.Objective: return "目标 Objectives";
            case Role.Spawn: return "出生点 Spawns";
            case Role.Movement: return "移动类 Movement";
            case Role.Enemy: return "敌人 Enemies";
            default: return "其他 Special";
        }
    }

    /// <summary>
    /// 摆放检查（纯逻辑）：grid[row] 第 0 行在最上面。返回中文问题列表（带坐标，x 从左 0 开始，y 从下 0 开始）。
    /// step1Only = true 时，额外检查"第 1 步房间不用"的元素。
    /// </summary>
    public static List<string> PlacementIssues(IList<string> grid, bool step1Only, System.Func<char, bool> isSolid)
    {
        var issues = new List<string>();
        int h = grid.Count;
        var counts = new Dictionary<char, int>();
        for (int row = 0; row < h; row++)
        {
            string line = grid[row];
            int y = h - 1 - row;
            for (int x = 0; x < line.Length; x++)
            {
                char c = line[x];
                var info = Get(c);
                if (info == null) { issues.Add($"({x},{y}) '{c}' 不在元素说明书里（先在 AsciiElementRegistry 登记并在 ElementCatalog 写说明）"); continue; }
                counts.TryGetValue(c, out int n); counts[c] = n + 1;
                if (step1Only && !info.step1) issues.Add($"({x},{y}) {info.zh} '{c}'：第 1 步房间不用这个元素");
                if (info.needsSupport)
                {
                    char below = row + 1 < h && x < grid[row + 1].Length ? grid[row + 1][x] : '.';
                    if (!isSolid(below)) issues.Add($"({x},{y}) {info.zh} '{c}'：脚下不是实心（会悬空或掉下去）");
                }
                if ((c == 'w' || ArtKitRules.SlotOf(c) >= 0) && (x == 0 || line[x - 1] != c)) // S245：素材槽和毒池一样最多 3 格宽
                {
                    int run = 0; while (x + run < line.Length && line[x + run] == c) run++;
                    if (run > MaxPoolCells) issues.Add($"({x},{y}) 毒池连续 {run} 格太宽：最多 {MaxPoolCells} 格（保证晕了也能走出来）");
                }
                if (c == 'Y' || c == 'Q')
                    for (int d = 1; d <= 2; d++)
                    {
                        if (row - d < 0) break;
                        string above = grid[row - d];
                        if (x < above.Length && isSolid(above[x]) && above[x] != '-') { issues.Add($"({x},{y}) {(c == 'Y' ? "绳套：正上方第 {d} 格是实心，吊起来会卡进墙" : "铁笼：正上方第 {d} 格是实心，笼子挂不上去")}；上方至少空 2 格"); break; }
                    }
                if (c == 'J')
                    for (int d = 1; d <= SpringHeadroomCells; d++)
                    {
                        if (row - d < 0) break;
                        string above = grid[row - d];
                        if (x < above.Length && isSolid(above[x])) { issues.Add($"({x},{y}) 弹簧板：正上方第 {d} 格是实心，马里奥会撞天花板；上方至少空 {SpringHeadroomCells} 格"); break; }
                    }
                if (info.muzzle != 0)
                    for (int d = 1; d <= MuzzleClearCells; d++)
                    {
                        int fx = x + info.muzzle * d;
                        if (fx < 0 || fx >= line.Length) break;
                        if (isSolid(line[fx])) { issues.Add($"({x},{y}) {info.zh}：炮口前第 {d} 格是实心（'{line[fx]}'），炮弹一出膛就会撞碎；前方至少空 {MuzzleClearCells} 格"); break; }
                    }
            }
        }
        if (counts.TryGetValue('O', out int vents) && vents % 2 == 1) issues.Add($"通风管 'O' 有 {vents} 个：要成对摆放（按上→下、左→右编号 1↔2、3↔4），现在有一个没有配对");
        foreach (var i in all)
            if (i.unique && counts.TryGetValue(i.ch, out int n) && n > 1) issues.Add($"{i.zh} '{i.ch}' 只能有 1 个（现在有 {n} 个）");
        return issues;
    }
}
