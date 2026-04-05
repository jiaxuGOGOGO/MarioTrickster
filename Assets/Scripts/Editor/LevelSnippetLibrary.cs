using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 关卡片段库 — 预设的经典关卡设计模式片段
/// 
/// 核心设计:
///   - 每个片段是一个小型 ASCII 模板（5-15行 x 10-30列）
///   - 片段按设计模式分类（跳跃挑战、陷阱走廊、敌人遭遇等）
///   - 每个片段附带设计说明、难度等级、使用建议
///   - 片段可直接生成到场景，也可拼接组合成完整关卡
///   - 参考了 GMTK 四步法和经典 Mario 关卡设计模式
///
/// 使用方式:
///   在 Test Console → Level Builder → "Snippet Library" 区块中浏览和使用
///   或通过代码: LevelSnippetLibrary.GetSnippet("name")
///
/// Session 26: 新增
/// </summary>
public static class LevelSnippetLibrary
{
    // ═══════════════════════════════════════════════════
    // 数据结构
    // ═══════════════════════════════════════════════════

    /// <summary>关卡片段</summary>
    public class Snippet
    {
        public string name;           // 片段名称
        public string category;       // 分类
        public string description;    // 设计说明
        public string designPattern;  // 使用的设计模式（如 GMTK 四步法的哪一步）
        public int difficulty;        // 难度等级 1-5
        public string ascii;          // ASCII 模板
        public string usageTip;       // 使用建议
        public int width;             // 宽度（字符数）
        public int height;            // 高度（行数）

        public Snippet(string name, string category, string description,
            string designPattern, int difficulty, string ascii, string usageTip)
        {
            this.name = name;
            this.category = category;
            this.description = description;
            this.designPattern = designPattern;
            this.difficulty = difficulty;
            this.ascii = ascii;
            this.usageTip = usageTip;

            // 计算尺寸
            string[] lines = ascii.Split('\n');
            this.height = lines.Length;
            this.width = 0;
            foreach (string line in lines)
                if (line.Length > this.width) this.width = line.Length;
        }
    }

    // ═══════════════════════════════════════════════════
    // 片段分类
    // ═══════════════════════════════════════════════════

    public static readonly string[] CATEGORIES = new string[]
    {
        "Intro & Tutorial",      // 教学引导
        "Jump Challenge",        // 跳跃挑战
        "Trap Corridor",         // 陷阱走廊
        "Enemy Encounter",       // 敌人遭遇
        "Vertical Section",      // 垂直区域
        "Puzzle & Secret",       // 解谜与秘密
        "Boss Arena",            // Boss 竞技场
        "Connector",             // 连接片段
        "Complete Mini-Level",   // 完整迷你关卡
    };

    // ═══════════════════════════════════════════════════
    // 片段库
    // ═══════════════════════════════════════════════════

    private static List<Snippet> snippets;

    /// <summary>获取所有片段</summary>
    public static List<Snippet> GetAllSnippets()
    {
        if (snippets == null) InitSnippets();
        return snippets;
    }

    /// <summary>按分类获取片段</summary>
    public static List<Snippet> GetSnippetsByCategory(string category)
    {
        if (snippets == null) InitSnippets();
        List<Snippet> result = new List<Snippet>();
        foreach (var s in snippets)
            if (s.category == category) result.Add(s);
        return result;
    }

    /// <summary>按名称获取片段</summary>
    public static Snippet GetSnippet(string name)
    {
        if (snippets == null) InitSnippets();
        foreach (var s in snippets)
            if (s.name == name) return s;
        return null;
    }

    /// <summary>按难度范围获取片段</summary>
    public static List<Snippet> GetSnippetsByDifficulty(int minDiff, int maxDiff)
    {
        if (snippets == null) InitSnippets();
        List<Snippet> result = new List<Snippet>();
        foreach (var s in snippets)
            if (s.difficulty >= minDiff && s.difficulty <= maxDiff) result.Add(s);
        return result;
    }

    /// <summary>
    /// 将多个片段水平拼接为完整关卡
    /// </summary>
    public static string CombineSnippetsHorizontal(List<Snippet> snippetList, int gapWidth = 2)
    {
        if (snippetList == null || snippetList.Count == 0) return "";

        // 找到最大高度
        int maxHeight = 0;
        foreach (var s in snippetList)
            if (s.height > maxHeight) maxHeight = s.height;

        // 构建每个片段的行数组（顶部对齐）
        List<string[]> allLines = new List<string[]>();
        foreach (var s in snippetList)
        {
            string[] lines = s.ascii.Split('\n');
            string[] padded = new string[maxHeight];

            // 底部对齐：空行在上面
            int offset = maxHeight - lines.Length;
            for (int i = 0; i < maxHeight; i++)
            {
                if (i < offset)
                    padded[i] = new string('.', s.width);
                else
                    padded[i] = lines[i - offset].PadRight(s.width, '.');
            }
            allLines.Add(padded);
        }

        // 拼接
        string gap = new string('.', gapWidth);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int row = 0; row < maxHeight; row++)
        {
            for (int si = 0; si < allLines.Count; si++)
            {
                if (si > 0) sb.Append(gap);
                sb.Append(allLines[si][row]);
            }
            if (row < maxHeight - 1) sb.Append('\n');
        }

        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════
    // 片段初始化
    // ═══════════════════════════════════════════════════

    private static void InitSnippets()
    {
        snippets = new List<Snippet>();

        // ────────────────────────────────────────
        // 1. 教学引导 (Intro & Tutorial)
        // ────────────────────────────────────────

        snippets.Add(new Snippet(
            "Safe Start",
            "Intro & Tutorial",
            "安全的起始区域，玩家有足够空间熟悉操作。平坦地面 + 少量收集物引导方向。",
            "GMTK: Introduce",
            1,
            "................\n" +
            "..o...o...o.....\n" +
            "................\n" +
            "..M.............\n" +
            "################",
            "放在关卡最左侧作为起点。收集物引导玩家向右移动。"
        ));

        snippets.Add(new Snippet(
            "First Jump",
            "Intro & Tutorial",
            "第一个小跳跃挑战。短间隙 + 安全着陆区，教会玩家跳跃。",
            "GMTK: Introduce",
            1,
            "...............\n" +
            "......o........\n" +
            "...............\n" +
            "...............\n" +
            "#####...#######",
            "间隙宽度 3 格，任何跳跃都能通过。失败后可以重试。"
        ));

        snippets.Add(new Snippet(
            "First Enemy",
            "Intro & Tutorial",
            "第一次遇到敌人。单个巡逻敌人在平坦地面上，给玩家充足反应时间。",
            "GMTK: Introduce",
            1,
            "................\n" +
            "......o.o.......\n" +
            "................\n" +
            "..........e.....\n" +
            "################",
            "敌人前方有足够距离让玩家观察和准备。上方收集物暗示踩踏。"
        ));

        // ────────────────────────────────────────
        // 2. 跳跃挑战 (Jump Challenge)
        // ────────────────────────────────────────

        snippets.Add(new Snippet(
            "Staircase Platforms",
            "Jump Challenge",
            "阶梯式平台序列，逐步增加高度。经典的垂直攀升设计。",
            "GMTK: Develop",
            2,
            "....................o..\n" +
            "....................==.\n" +
            "...............o......\n" +
            "...............==.....\n" +
            "..........o...........\n" +
            "..........==..........\n" +
            ".....o................\n" +
            ".....==...............\n" +
            "......................\n" +
            "######################",
            "平台间距逐渐增大，难度递增。顶部放置奖励。"
        ));

        snippets.Add(new Snippet(
            "Gap Run",
            "Jump Challenge",
            "连续间隙跳跃。间隙宽度递增，考验连续跳跃节奏感。",
            "GMTK: Develop",
            3,
            "...........................\n" +
            "...o...o....o.....o........\n" +
            "...........................\n" +
            "...........................\n" +
            "###..###..##...##....######",
            "间隙从 2 格递增到 4 格。收集物标记最佳跳跃点。"
        ));

        snippets.Add(new Snippet(
            "Moving Platform Bridge",
            "Jump Challenge",
            "使用移动平台跨越大间隙。需要观察平台节奏后跳跃。",
            "GMTK: Develop",
            3,
            "...........................\n" +
            ".........o.....o...........\n" +
            "...........................\n" +
            ".....>........>............\n" +
            "...........................\n" +
            "####.................######",
            "移动平台间隔设计需要玩家等待时机。可叠加收集物增加风险回报。"
        ));

        snippets.Add(new Snippet(
            "Bounce Chain",
            "Jump Challenge",
            "连续弹跳平台序列。利用弹跳平台到达高处。",
            "GMTK: Twist",
            3,
            "....................o.o..\n" +
            "....................===..\n" +
            "........................\n" +
            "..............B.........\n" +
            "........................\n" +
            "........B...............\n" +
            "........................\n" +
            "..B.....................\n" +
            "########################",
            "弹跳平台形成上升路径。最后一个弹跳需要精确落点。"
        ));

        // ────────────────────────────────────────
        // 3. 陷阱走廊 (Trap Corridor)
        // ────────────────────────────────────────

        snippets.Add(new Snippet(
            "Spike Alley",
            "Trap Corridor",
            "地刺走廊。地面布满地刺，需要跳跃通过平台安全区。",
            "GMTK: Develop",
            2,
            "...........................\n" +
            "...........................\n" +
            "...........................\n" +
            "....==....==....==.........\n" +
            "...........................\n" +
            "...........................\n" +
            ".^^..^^..^^..^^..^^..^^..^.\n" +
            "###########################",
            "平台间距均匀，初次引入地刺概念。安全平台足够宽。"
        ));

        snippets.Add(new Snippet(
            "Fire Gauntlet",
            "Trap Corridor",
            "火焰陷阱走廊。需要观察火焰节奏，在间隙中通过。",
            "GMTK: Twist",
            3,
            "...........................\n" +
            "...........................\n" +
            "...........................\n" +
            "...........................\n" +
            "...~.....~.....~.....~....\n" +
            "...........................\n" +
            "...........................\n" +
            "###########################",
            "火焰陷阱有周期性开关。玩家需要观察节奏后快速通过。"
        ));

        snippets.Add(new Snippet(
            "Pendulum Passage",
            "Trap Corridor",
            "摆锤走廊。头顶悬挂摆锤，需要观察摆动节奏穿越。",
            "GMTK: Twist",
            4,
            "..P.....P.....P.....P....\n" +
            ".........................\n" +
            ".........................\n" +
            ".........................\n" +
            ".........................\n" +
            ".........................\n" +
            "#########################",
            "摆锤间距决定难度。可以叠加地面地刺增加压力。"
        ));

        snippets.Add(new Snippet(
            "Trap Combo",
            "Trap Corridor",
            "多种陷阱组合。地刺 + 火焰 + 摆锤的综合挑战。",
            "GMTK: Conclude",
            4,
            "......P.........P........\n" +
            ".........................\n" +
            ".........................\n" +
            "........~.....~..........\n" +
            ".........................\n" +
            "....==.......==....==....\n" +
            ".^^....^^^.......^^..^^..\n" +
            "#########################",
            "这是陷阱系列的终极考验。组合之前单独学过的所有陷阱。"
        ));

        // ────────────────────────────────────────
        // 4. 敌人遭遇 (Enemy Encounter)
        // ────────────────────────────────────────

        snippets.Add(new Snippet(
            "Patrol Gauntlet",
            "Enemy Encounter",
            "多个巡逻敌人的走廊。需要观察巡逻路线，找准时机通过。",
            "GMTK: Develop",
            2,
            "...........................\n" +
            "...........................\n" +
            "...........................\n" +
            "....e.......e.......e.....\n" +
            "###########################",
            "敌人间距足够大，可以逐个应对。平坦地形降低额外难度。"
        ));

        snippets.Add(new Snippet(
            "Bouncer Arena",
            "Enemy Encounter",
            "弹跳敌人封锁区域。需要观察弹跳节奏，从间隙中穿过。",
            "GMTK: Twist",
            3,
            "...........................\n" +
            "...........................\n" +
            ".......E.......E.........\n" +
            "...........................\n" +
            "...........................\n" +
            "###########################",
            "弹跳敌人的跳跃高度和频率决定安全窗口。"
        ));

        snippets.Add(new Snippet(
            "Enemy + Trap Mix",
            "Enemy Encounter",
            "敌人与陷阱的组合挑战。需要同时应对两种威胁。",
            "GMTK: Conclude",
            4,
            "...........................\n" +
            "...........................\n" +
            ".........E...............\n" +
            "...........................\n" +
            "....e.........e..........\n" +
            ".^^...####..^^...####.^^.\n" +
            "###########################",
            "地面敌人在安全平台上巡逻，弹跳敌人封锁跳跃路线。"
        ));

        // ────────────────────────────────────────
        // 5. 垂直区域 (Vertical Section)
        // ────────────────────────────────────────

        snippets.Add(new Snippet(
            "Vertical Climb",
            "Vertical Section",
            "垂直攀升塔。交替的平台形成之字形上升路径。",
            "GMTK: Develop",
            3,
            "W...........o..W\n" +
            "W..........==..W\n" +
            "W..............W\n" +
            "W..==..........W\n" +
            "W..............W\n" +
            "W..........==..W\n" +
            "W..............W\n" +
            "W..==..........W\n" +
            "W..............W\n" +
            "W..........==..W\n" +
            "W..............W\n" +
            "W####..........W\n" +
            "W##############W",
            "墙壁限制水平空间，迫使玩家垂直移动。平台交替左右放置。"
        ));

        snippets.Add(new Snippet(
            "Collapse Tower",
            "Vertical Section",
            "崩塌平台塔。平台在踩踏后崩塌，必须快速向上攀爬。",
            "GMTK: Twist",
            4,
            "W..........o.o.W\n" +
            "W..........==..W\n" +
            "W..............W\n" +
            "W..CC..........W\n" +
            "W..............W\n" +
            "W..........CC..W\n" +
            "W..............W\n" +
            "W..CC..........W\n" +
            "W..............W\n" +
            "W..........CC..W\n" +
            "W..............W\n" +
            "W####..........W\n" +
            "W##############W",
            "崩塌平台迫使玩家快速决策。错过时机需要重新开始。"
        ));

        // ────────────────────────────────────────
        // 6. 解谜与秘密 (Puzzle & Secret)
        // ────────────────────────────────────────

        snippets.Add(new Snippet(
            "Hidden Room",
            "Puzzle & Secret",
            "隐藏房间入口。伪装墙后面藏有收集物奖励。",
            "GMTK: Twist",
            2,
            "################\n" +
            "#..o.o.o.o..#...\n" +
            "#...........#...\n" +
            "#...........F...\n" +
            "################",
            "伪装墙与普通墙壁外观相同。好奇的玩家会发现秘密。"
        ));

        snippets.Add(new Snippet(
            "Secret Passage",
            "Puzzle & Secret",
            "隐藏通道连接两个区域。提供捷径或通往秘密区域。",
            "GMTK: Twist",
            2,
            "##########..........\n" +
            "#.o.o.o..#..........\n" +
            "#........#..........\n" +
            "#........H..........\n" +
            "##########..........\n" +
            "....................\n" +
            "....................\n" +
            "####################",
            "隐藏通道入口可以放在不显眼的位置。通道另一端放置奖励。"
        ));

        // ────────────────────────────────────────
        // 7. Boss 竞技场 (Boss Arena)
        // ────────────────────────────────────────

        snippets.Add(new Snippet(
            "Simple Arena",
            "Boss Arena",
            "简单的封闭竞技场。平坦地面 + 两侧墙壁，适合 Boss 战。",
            "GMTK: Conclude",
            3,
            "W....................W\n" +
            "W....................W\n" +
            "W....................W\n" +
            "W....................W\n" +
            "W....................W\n" +
            "W....................W\n" +
            "W....................W\n" +
            "W..E...........E...W\n" +
            "W####################W",
            "封闭空间限制逃跑。可以添加平台提供垂直移动选项。"
        ));

        snippets.Add(new Snippet(
            "Multi-Level Arena",
            "Boss Arena",
            "多层竞技场。多个平台层提供垂直战斗空间。",
            "GMTK: Conclude",
            4,
            "W......................W\n" +
            "W..====......====.....W\n" +
            "W......................W\n" +
            "W........====.........W\n" +
            "W......................W\n" +
            "W..====......====.....W\n" +
            "W......................W\n" +
            "W......................W\n" +
            "W######################W",
            "多层平台让战斗更有策略性。Boss 可以在不同层追逐玩家。"
        ));

        // ────────────────────────────────────────
        // 8. 连接片段 (Connector)
        // ────────────────────────────────────────

        snippets.Add(new Snippet(
            "Flat Bridge",
            "Connector",
            "平坦的连接通道。用于连接两个挑战区域，给玩家喘息空间。",
            "Rest Zone",
            1,
            "............\n" +
            "..o...o.....\n" +
            "............\n" +
            "............\n" +
            "############",
            "放在两个高难度区域之间。收集物提供正反馈。"
        ));

        snippets.Add(new Snippet(
            "Downhill Slide",
            "Connector",
            "下坡滑行段。阶梯式下降，自然过渡到下一区域。",
            "Transition",
            1,
            "##....................\n" +
            "###...................\n" +
            ".####.................\n" +
            "..#####...............\n" +
            "...######.............\n" +
            "....#######...........\n" +
            ".....################.",
            "下坡给玩家速度感和成就感。底部可以接跳跃挑战。"
        ));

        snippets.Add(new Snippet(
            "Checkpoint Rest",
            "Connector",
            "安全的检查点区域。宽阔平台 + 收集物，暗示这是安全区。",
            "Rest Zone",
            1,
            "...............\n" +
            "...o.o.o.o.....\n" +
            "...............\n" +
            "...............\n" +
            "###############",
            "在长关卡中每 3-4 个挑战后放置一个。宽度至少 15 格。"
        ));

        // ────────────────────────────────────────
        // 9. 完整迷你关卡 (Complete Mini-Level)
        // ────────────────────────────────────────

        snippets.Add(new Snippet(
            "GMTK 4-Step Demo",
            "Complete Mini-Level",
            "完整展示 GMTK 四步法的迷你关卡：引入地刺 → 发展跳跃技巧 → 转折加入敌人 → 总结组合挑战。",
            "GMTK: Full Cycle",
            3,
            "........................................................................\n" +
            "..o.o.......o...o.......o.o.o...........o...o...o.....o.o.o.............\n" +
            "..............==..........----.........===.......===.............o.......\n" +
            "...............................E.............................E...===.....\n" +
            "..M..........................................................e.......G..\n" +
            "..##...####..^^..####..####..####..^^..####..^^..####.####..^^..####.##.\n" +
            "..##...####..^^..####..####..####..^^..####..^^..####.####..^^..####.##.\n" +
            "########################################################################",
            "完整的四步法示范：\n1. 引入：安全起点，第一个小跳跃\n2. 发展：地刺间隙跳跃\n3. 转折：加入弹跳敌人\n4. 总结：地刺+敌人组合"
        ));

        snippets.Add(new Snippet(
            "Underground Adventure",
            "Complete Mini-Level",
            "地下洞窟迷你关卡。封闭空间 + 隐藏通道 + 陷阱组合。",
            "Exploration",
            4,
            "##############################\n" +
            "#............................#\n" +
            "#..M.......o...o.............#\n" +
            "#..##......----.......o.o....#\n" +
            "#..##...........F..........G.#\n" +
            "#..##..e....P...F..o.o.o..###\n" +
            "#..##..####..^^.####..##..####\n" +
            "#..##H.####..^^.####..##..####\n" +
            "#..##..####.....####..##..####\n" +
            "##############################",
            "封闭空间增加压迫感。隐藏通道提供捷径。伪装墙后有收集物奖励。"
        ));

        snippets.Add(new Snippet(
            "Sky Fortress",
            "Complete Mini-Level",
            "天空要塞迷你关卡。高空平台 + 移动平台 + 弹跳平台的空中冒险。",
            "Vertical + Horizontal",
            4,
            "............................................\n" +
            ".........o.o.o..........o.o.o..........G...\n" +
            ".........-----..........-----.........###...\n" +
            "............................................\n" +
            "....>...............>..............===.....\n" +
            "............................................\n" +
            "...........B.............B.................\n" +
            "............................................\n" +
            "..M.......====.......====..................\n" +
            "..##......====.......====..................\n" +
            "..##........................................",
            "无地面的空中关卡。掉落即死。弹跳平台和移动平台是唯一的路径。"
        ));
    }
}
