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
    }

    /// <summary>大炮炮口前方至少空出几格（否则炮弹一出膛就撞墙/箱子）。</summary>
    public const int MuzzleClearCells = 3;

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
        I('K', "Cannon", "大炮（朝右）", "Cannon (→)", Role.PlayerPrank, "伪装在旁按 L 开一炮（每局 1 发）；打完后站进炮口把自己打飞逃跑。", "地面上，炮口前方至少空 3 格。", needsSupport: true, step1: true, muzzle: 1),
        I('k', "Cannon", "大炮（朝左）", "Cannon (←)", Role.PlayerPrank, "同上，炮口朝左。", "地面上，炮口前方至少空 3 格。", needsSupport: true, step1: true, muzzle: -1),
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
                   unique = unique, needsSupport = needsSupport, step1 = step1, muzzle = muzzle };

    public static IReadOnlyList<Info> All => all;

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
                if (info.muzzle != 0)
                    for (int d = 1; d <= MuzzleClearCells; d++)
                    {
                        int fx = x + info.muzzle * d;
                        if (fx < 0 || fx >= line.Length) break;
                        if (isSolid(line[fx])) { issues.Add($"({x},{y}) {info.zh}：炮口前第 {d} 格是实心（'{line[fx]}'），炮弹一出膛就会撞碎；前方至少空 {MuzzleClearCells} 格"); break; }
                    }
            }
        }
        foreach (var i in all)
            if (i.unique && counts.TryGetValue(i.ch, out int n) && n > 1) issues.Add($"{i.zh} '{i.ch}' 只能有 1 个（现在有 {n} 个）");
        return issues;
    }
}
