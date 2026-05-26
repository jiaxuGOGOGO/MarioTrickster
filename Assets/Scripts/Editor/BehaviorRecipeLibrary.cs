using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// BehaviorRecipeLibrary — GBG 风格的"行为配方"预设系统
///
/// 核心理念（来自 Game Builder Garage 的 Fancy Objects）：
///   GBG 中的 Person/Car/UFO Nodon 自带行走/跳跃/转向等内置行为，
///   用户不需要从零开始连线，只需选一个预设就能得到完整的可交互对象。
///
/// 在 MarioTrickster 中的映射：
///   "行为配方" = 一组预配置好的元素组合模式（ASCII 片段 + 参数预设）
///   用户点击一个配方，就能在场景中生成一个【带完整行为的复合结构】。
///
/// 配方分类（对应 GBG 的 Nodon 四色分类）：
///   🔴 挑战配方 (Challenge) — 考验玩家技巧的组合
///   🟢 机关配方 (Mechanism) — Trickster 可操控的交互结构
///   🔵 路线配方 (Route) — 定义通行路径的结构组合
///   🟠 场景配方 (Scene) — 完整的小型游戏场景
/// </summary>
public static class BehaviorRecipeLibrary
{
    // ═══════════════════════════════════════════════════
    // 数据结构
    // ═══════════════════════════════════════════════════

    public enum RecipeCategory
    {
        Challenge,  // 🔴 挑战
        Mechanism,  // 🟢 机关
        Route,      // 🔵 路线
        Scene       // 🟠 场景
    }

    public class BehaviorRecipe
    {
        public string name;
        public string description;
        public string tooltip;          // 一句话说明"这个配方能做什么"
        public RecipeCategory category;
        public string asciiTemplate;    // ASCII 模板
        public int width;
        public int height;

        // 可调参数（GBG 风格的 Settings 面板）
        public RecipeParam[] parameters;

        public BehaviorRecipe(string name, string desc, string tooltip,
            RecipeCategory cat, string ascii, RecipeParam[] parameters = null)
        {
            this.name = name;
            this.description = desc;
            this.tooltip = tooltip;
            this.category = cat;
            this.asciiTemplate = ascii;
            this.parameters = parameters ?? new RecipeParam[0];

            string[] lines = ascii.Split('\n');
            this.height = lines.Length;
            this.width = 0;
            foreach (string line in lines)
                if (line.Length > this.width) this.width = line.Length;
        }
    }

    /// <summary>配方可调参数（类似 GBG 的 Nodon Settings）</summary>
    public class RecipeParam
    {
        public string name;
        public string label;
        public float defaultValue;
        public float min;
        public float max;
        public string unit;

        public RecipeParam(string name, string label, float defaultVal, float min, float max, string unit = "")
        {
            this.name = name;
            this.label = label;
            this.defaultValue = defaultVal;
            this.min = min;
            this.max = max;
            this.unit = unit;
        }
    }

    // ═══════════════════════════════════════════════════
    // 配方库
    // ═══════════════════════════════════════════════════

    private static List<BehaviorRecipe> _recipes;

    public static List<BehaviorRecipe> GetAllRecipes()
    {
        if (_recipes == null)
            _recipes = BuildRecipeLibrary();
        return _recipes;
    }

    public static List<BehaviorRecipe> GetByCategory(RecipeCategory category)
    {
        List<BehaviorRecipe> result = new List<BehaviorRecipe>();
        foreach (var r in GetAllRecipes())
        {
            if (r.category == category)
                result.Add(r);
        }
        return result;
    }

    // ═══════════════════════════════════════════════════
    // 配方定义
    // ═══════════════════════════════════════════════════

    private static List<BehaviorRecipe> BuildRecipeLibrary()
    {
        var recipes = new List<BehaviorRecipe>();

        // ────────────────────────────────────────────────
        // 🔴 挑战配方 (Challenge)
        // ────────────────────────────────────────────────

        recipes.Add(new BehaviorRecipe(
            "Spike Gauntlet",
            "地刺走廊：连续地刺+安全岛节奏挑战",
            "放下就是一段需要精确跳跃的地刺走廊",
            RecipeCategory.Challenge,
            ".........\n" +
            ".........\n" +
            "#.#.#.#.#\n" +
            "#^#^#^#^#\n" +
            "#########",
            new RecipeParam[] {
                new RecipeParam("gap", "安全岛间距", 2f, 1f, 4f, "格"),
                new RecipeParam("count", "地刺组数", 4f, 2f, 8f, "组")
            }
        ));

        recipes.Add(new BehaviorRecipe(
            "Pendulum Alley",
            "摆锤巷道：多个摆锤的时机挑战通道",
            "需要观察摆锤节奏才能安全通过的走廊",
            RecipeCategory.Challenge,
            "..P...P...P..\n" +
            ".............\n" +
            ".............\n" +
            "#############",
            new RecipeParam[] {
                new RecipeParam("count", "摆锤数量", 3f, 2f, 6f, "个"),
                new RecipeParam("spacing", "摆锤间距", 4f, 3f, 6f, "格")
            }
        ));

        recipes.Add(new BehaviorRecipe(
            "Saw Blade Corridor",
            "锯片走廊：上下锯片交错的躲避挑战",
            "锯片从上下交错逼近，需要精确走位",
            RecipeCategory.Challenge,
            ".@.........\n" +
            "...........\n" +
            "...........\n" +
            "........@..\n" +
            "###########",
            new RecipeParam[] {
                new RecipeParam("count", "锯片数量", 2f, 2f, 6f, "个")
            }
        ));

        recipes.Add(new BehaviorRecipe(
            "Collapse Rush",
            "崩塌冲刺：连续崩塌平台的速度挑战",
            "脚下的平台会崩塌，必须不停奔跑",
            RecipeCategory.Challenge,
            "............\n" +
            "............\n" +
            "CCCCCCCCCCCC\n" +
            "............\n" +
            "############",
            new RecipeParam[] {
                new RecipeParam("length", "崩塌段长度", 12f, 6f, 20f, "格"),
                new RecipeParam("delay", "崩塌延迟", 0.5f, 0.2f, 1.5f, "秒")
            }
        ));

        // ────────────────────────────────────────────────
        // 🟢 机关配方 (Mechanism) — Trickster 可操控
        // ────────────────────────────────────────────────

        recipes.Add(new BehaviorRecipe(
            "Trap Gate",
            "陷阱门：Trickster 可操控的移动平台阻断 Mario 路线",
            "一个可以被 Trickster 附身操控来阻断 Mario 的移动门",
            RecipeCategory.Mechanism,
            ".....\n" +
            "..>..\n" +
            ".....\n" +
            "#...#\n" +
            "#####",
            new RecipeParam[] {
                new RecipeParam("travel", "移动距离", 3f, 2f, 6f, "格"),
                new RecipeParam("speed", "移动速度", 2f, 1f, 5f, "格/秒")
            }
        ));

        recipes.Add(new BehaviorRecipe(
            "Conveyor Trap",
            "传送带陷阱：Trickster 可反转方向把 Mario 推向危险",
            "传送带可以被 Trickster 操控反转，把 Mario 推向地刺",
            RecipeCategory.Mechanism,
            "...........\n" +
            "...........\n" +
            "<<<<<<<<<^.\n" +
            "###########",
            new RecipeParam[] {
                new RecipeParam("length", "传送带长度", 8f, 4f, 12f, "格"),
                new RecipeParam("speed", "传送速度", 3f, 1f, 6f, "格/秒")
            }
        ));

        recipes.Add(new BehaviorRecipe(
            "Fake Wall Ambush",
            "伪装墙伏击：看似安全的墙壁实际可被 Trickster 控制消失",
            "Mario 依赖的安全墙壁突然消失，露出后面的陷阱",
            RecipeCategory.Mechanism,
            ".....\n" +
            "..F..\n" +
            "..F..\n" +
            "#.F^#\n" +
            "#####",
            new RecipeParam[] {
                new RecipeParam("height", "伪装墙高度", 3f, 2f, 5f, "格")
            }
        ));

        recipes.Add(new BehaviorRecipe(
            "Platform Switcheroo",
            "平台换位：多个移动平台组成的立体迷宫，Trickster 可操控改变路线",
            "移动平台网络，Trickster 附身后可以改变平台运动轨迹",
            RecipeCategory.Mechanism,
            "...........\n" +
            ".....>.....\n" +
            "...........\n" +
            "..>........\n" +
            "...........\n" +
            "........>..\n" +
            "###########",
            new RecipeParam[] {
                new RecipeParam("platforms", "平台数量", 3f, 2f, 5f, "个"),
                new RecipeParam("travel", "移动范围", 4f, 2f, 6f, "格")
            }
        ));

        // ────────────────────────────────────────────────
        // 🔵 路线配方 (Route) — 定义通行结构
        // ────────────────────────────────────────────────

        recipes.Add(new BehaviorRecipe(
            "High-Low Split",
            "上下分路：经典的双路线分叉结构",
            "玩家可以选择走上路（难但快）或下路（安全但慢）",
            RecipeCategory.Route,
            "...........\n" +
            "===........\n" +
            "...........\n" +
            "...........\n" +
            "...====....\n" +
            "...........\n" +
            "###########",
            new RecipeParam[] {
                new RecipeParam("length", "分路长度", 10f, 6f, 16f, "格"),
                new RecipeParam("height_diff", "高度差", 3f, 2f, 5f, "格")
            }
        ));

        recipes.Add(new BehaviorRecipe(
            "Staircase Up",
            "阶梯上升：标准的阶梯式上升路线",
            "逐级上升的平台阶梯",
            RecipeCategory.Route,
            "..........=\n" +
            "........=..\n" +
            "......=....\n" +
            "....=......\n" +
            "..=........\n" +
            "=..........\n" +
            "###########",
            new RecipeParam[] {
                new RecipeParam("steps", "台阶数", 6f, 3f, 10f, "级"),
                new RecipeParam("step_height", "每级高度", 1f, 1f, 2f, "格")
            }
        ));

        recipes.Add(new BehaviorRecipe(
            "Bouncy Shortcut",
            "弹跳捷径：弹跳平台组成的垂直快速通道",
            "用弹跳平台快速到达高处的捷径",
            RecipeCategory.Route,
            "....G......\n" +
            "...===.....\n" +
            "...........\n" +
            "...........\n" +
            ".....B.....\n" +
            "...........\n" +
            "..B........\n" +
            "###########",
            new RecipeParam[] {
                new RecipeParam("bounces", "弹跳平台数", 2f, 1f, 4f, "个"),
                new RecipeParam("total_height", "总上升高度", 6f, 4f, 10f, "格")
            }
        ));

        recipes.Add(new BehaviorRecipe(
            "Hidden Bypass",
            "隐藏旁路：伪装墙后的秘密通道",
            "看不见的捷径，需要探索才能发现",
            RecipeCategory.Route,
            "###H##\n" +
            "#....#\n" +
            "#....#\n" +
            "###H##\n" +
            "######",
            new RecipeParam[] {
                new RecipeParam("length", "通道长度", 4f, 3f, 8f, "格")
            }
        ));

        // ────────────────────────────────────────────────
        // 🟠 场景配方 (Scene) — 完整小型游戏场景
        // ────────────────────────────────────────────────

        recipes.Add(new BehaviorRecipe(
            "Arena Duel",
            "对决竞技场：封闭空间内的 Mario vs Trickster 对决场景",
            "一个完整的小型对决场景，包含出生点、机关和目标",
            RecipeCategory.Scene,
            "W.........W\n" +
            "W.........W\n" +
            "W...>...o.W\n" +
            "W.........W\n" +
            "WM..===..TW\n" +
            "W.........W\n" +
            "W...^^^...W\n" +
            "W#########W\n" +
            "WWWWWWWWWWW",
            new RecipeParam[] {
                new RecipeParam("width", "竞技场宽度", 9f, 7f, 15f, "格"),
                new RecipeParam("height", "竞技场高度", 7f, 5f, 10f, "格")
            }
        ));

        recipes.Add(new BehaviorRecipe(
            "Escape Room",
            "逃脱房间：收集钥匙（金币）后到达出口的完整小关",
            "一个需要收集所有金币才能通过终点的完整小关卡",
            RecipeCategory.Scene,
            "W...o...o.W\n" +
            "W.===.===.W\n" +
            "W.........W\n" +
            "Wo........W\n" +
            "W==..^..==W\n" +
            "W.........W\n" +
            "WM......GTW\n" +
            "W#########W\n" +
            "WWWWWWWWWWW",
            new RecipeParam[] {
                new RecipeParam("coins", "金币数量", 3f, 2f, 6f, "个"),
                new RecipeParam("traps", "陷阱密度", 1f, 0f, 3f, "组")
            }
        ));

        recipes.Add(new BehaviorRecipe(
            "Vertical Tower",
            "垂直塔楼：自下而上攀爬的塔楼结构",
            "从底部出发向上攀爬到达顶部终点的塔",
            RecipeCategory.Scene,
            "W....G....W\n" +
            "W..====...W\n" +
            "W.........W\n" +
            "W...====..W\n" +
            "W.........W\n" +
            "W..====...W\n" +
            "W.........W\n" +
            "W...====..W\n" +
            "WM........W\n" +
            "W#########W\n" +
            "WWWWWWWWWWW",
            new RecipeParam[] {
                new RecipeParam("floors", "楼层数", 4f, 3f, 8f, "层"),
                new RecipeParam("width", "塔宽度", 9f, 7f, 13f, "格")
            }
        ));

        recipes.Add(new BehaviorRecipe(
            "Chase Sequence",
            "追逐序列：长距离奔跑+障碍的追逐关卡",
            "Mario 需要快速通过一系列障碍到达终点",
            RecipeCategory.Scene,
            "........................G\n" +
            ".........................\n" +
            "M...^..P...^..@...^..===\n" +
            "#########################",
            new RecipeParam[] {
                new RecipeParam("length", "追逐长度", 24f, 16f, 40f, "格"),
                new RecipeParam("obstacle_density", "障碍密度", 3f, 2f, 5f, "组")
            }
        ));

        return recipes;
    }
}
