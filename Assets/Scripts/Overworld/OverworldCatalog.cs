using System.Collections.Generic;

/// <summary>
/// S210：大地图（星露谷式俯视小镇）的格子说明书——唯一来源。网页设计台（build.py 解析本文件）与 Unity 大地图工坊都从这里读。
/// 与横版房间的字符表是两套（不同的网格）：大地图只管"走到哪、看见谁、在哪埋伏"，门 1–9 连到横版房间（关卡库里的关卡）。
/// 规则来源：Stardew Valley 地图分层（Back 地面 / Buildings 挡路 / Front 前景遮挡）+ Warp 门（https://stardewvalleywiki.com/Modding:Maps ）；
/// 潜行俯视：视锥 + 遮挡 + 高草藏身 + 可见的"? → !"（https://gamedesignskills.com/game-design/stealth/ ）。
/// </summary>
public static class OverworldCatalog
{
    public sealed class Tile
    {
        public char c;
        public string key, zh, en, role, what, how;
        /// <summary>挡路（谁都走不过去）。</summary>
        public bool solid;
        /// <summary>挡视线（马里奥看不穿）。</summary>
        public bool blocksSight;
        /// <summary>站在里面只有贴身才看得见（高草）。</summary>
        public bool hides;
        /// <summary>马里奥寻路的走路代价（石子路 1 最喜欢走；草地 2；泥地 4）。</summary>
        public int cost;
        public float r, g, b;
    }

    public static readonly List<Tile> All = new List<Tile>();
    private static readonly Dictionary<char, Tile> byChar = new Dictionary<char, Tile>();

    // 字段顺序：字符, key, 中文, English, 角色, 挡路, 挡视线, 藏身, 代价, 颜色 r g b, 是什么, 怎么放
    private static void T(char c, string key, string zh, string en, string role, bool solid, bool sight, bool hides, int cost, float r, float g, float b, string what, string how)
    {
        var t = new Tile { c = c, key = key, zh = zh, en = en, role = role, solid = solid, blocksSight = sight, hides = hides, cost = cost, r = r, g = g, b = b, what = what, how = how };
        All.Add(t); byChar[c] = t;
    }

    static OverworldCatalog()
    {
        T('.', "Grass", "草地", "Grass", "Terrain", false, false, false, 2, 0.42f, 0.66f, 0.33f, "普通草地，谁都能走。", "铺满空地。");
        T('=', "Path", "石子路", "Stone path", "Terrain", false, false, false, 1, 0.80f, 0.72f, 0.54f, "马里奥最爱走的路（寻路代价最低）。他的日常路线会沿着路走——你能预判他从哪来。", "连起家和各个门；路边放高草就是埋伏点。");
        T('W', "Building", "房屋", "Building", "Terrain", true, true, false, 0, 0.62f, 0.38f, 0.26f, "房子/围墙：挡路、挡视线。底边一排画成墙面，上面画成屋顶（星露谷式 3/4 视角）。", "门 1–9 画在房子的最下面一排旁边（墙面前一格）。");
        T('t', "Tree", "树", "Tree", "Terrain", true, true, false, 0, 0.16f, 0.42f, 0.2f, "树：挡路、挡视线。你躲在树后面他看不见。", "外圈可以用树围起来；路边的树 = 天然掩体。");
        T('w', "Water", "水", "Water", "Terrain", true, false, false, 0, 0.27f, 0.52f, 0.86f, "水：过不去，但挡不住视线——隔着河他也能看见你。", "河、池塘。想让他绕远路就放一条河 + 一座桥（石子路）。");
        T('f', "Fence", "栅栏", "Fence", "Terrain", true, false, false, 0, 0.76f, 0.6f, 0.38f, "栅栏：过不去，看得穿。", "围菜地、围院子。");
        T('"', "TallGrass", "高草", "Tall grass", "Scenery", false, false, true, 3, 0.26f, 0.5f, 0.2f, "站在高草里只有贴身（1.5 格内）才会被看见；但你在草里走动，草会晃——他看见晃就会起疑（?）。", "放在路边、门口 2–6 格内，当埋伏点。");
        T('c', "Crate", "木箱", "Crate", "Scenery", true, true, false, 0, 0.62f, 0.44f, 0.24f, "木箱：挡路、挡视线（掩体）。你按 P 伪装时也会变成一个木箱。", "门口、路口放一两个，给你躲。");
        T('g', "Mud", "泥地", "Mud", "Terrain", false, false, false, 4, 0.46f, 0.34f, 0.22f, "泥地：走得慢（你和马里奥都一样）。马里奥寻路会尽量绕开。", "堵住捷径，逼他绕路；或者放在你的逃跑路线外。");
        T('i', "Lamp", "路灯", "Lamp", "Special", true, false, false, 0, 1f, 0.86f, 0.42f, "路灯：晚上（19:00 后）马里奥看得近，但路灯周围 3 格照得亮——站在灯下会被远远看见。", "放在路口和门口，晚上决定哪里安全。");
        T('n', "BananaPeel", "香蕉皮", "Banana peel", "PlayerPrank", false, false, false, 2, 0.98f, 0.88f, 0.2f, "你靠近（3 格内）按 L：闪 0.5 秒预警后生效 3 秒，马里奥踩上去滑倒晕 1.5 秒。被他看见你按 → 起疑。拖住他 = 你先到门口，进房间多几秒布置时间。", "放在他的日常路线上（石子路）。");
        T('?', "PickupBox", "道具箱", "Pickup box", "Special", false, false, false, 2, 0.95f, 0.72f, 0.2f, "走过去捡：下一个房间多 1 个炸弹（每天每个箱子一次）。马里奥看得见箱子，但他不捡。", "放在绕远的地方，让你在'抢时间'和'拿道具'之间选。每张图最多 3 个。");
        // S218：小镇大机关（比房间里的夸张一个量级；规则在 OverworldProps，每天每个一次，改掉的地形只活一天、只会"打开"不会"关死"）
        T('K', "GiantCannon", "巨炮", "Giant cannon", "PlayerPrank", true, true, false, 0, 0.3f, 0.3f, 0.36f, "小镇巨炮：你在 3 格内按 L，炮口前 3 格闪 1.2 秒后开炮——站在炮口里的人被轰到同一行/列最近的靶心 X（大风天吹偏 3 格），落地晕 2 秒。轰隆声半个镇都听得见。你自己站进炮口 = 抄近路飞过去。他被轰过一次就记住：以后看见炮口在闪会先躲开。每天一次。", "同一行或同一列放一个靶心 X，炮口朝靶心那边空 3 格。靶心放在滚石/水塔/另一门巨炮旁 1 格内 = 连锁。");
        T('X', "CannonTarget", "靶心", "Cannon target", "Special", false, false, false, 2, 0.93f, 0.36f, 0.3f, "巨炮落点（地上的红圈，谁都看得见）。炮弹落地的冲击会震响 1.5 格内的滚石/水塔/巨炮（连锁，同样先预警再发动）。", "和巨炮同一行或同一列；放在开阔处——落点必须走得回马里奥的家，检查会算。");
        T('O', "Boulder", "滚石", "Boulder", "PlayerPrank", true, true, false, 0, 0.56f, 0.53f, 0.49f, "巨石：你在 3 格内按 L，晃 1.2 秒后朝离开你的方向一路滚（每秒 9 格），撞碎木箱和栅栏，碾到的人晕 2 秒，滚到头碎掉，冲击震响 1.5 格内的大机关。撞碎的东西当天不复原。他被碾过一次就会躲。每天一次。", "放在长直路的一头（他的路线上）；滚到头正好停在另一个大机关旁 = 连锁。");
        T('U', "WaterTower", "水塔", "Water tower", "PlayerPrank", true, true, false, 0, 0.36f, 0.6f, 0.84f, "水塔：你在 3 格内按 L，吱呀 1.2 秒后倒水：3 格内的草地/石子路/高草变泥地（雨天 4 格），谁走都慢；泡到的香蕉皮自己变滑。代价：高草也冲没了（你的藏身处）。每天一次。", "放在他必经的路口旁；香蕉皮放在淹没范围里 = 连锁。");
        // S219：山地（高度 0 地面 / 1 山丘 / 2 山）+ 山洞隧道。规则在 OverworldProps（Height / Caves / MudLane），视线在 OverworldMap.LineOfSight
        T('^', "Hill", "山丘", "Hill", "Terrain", false, false, false, 3, 0.58f, 0.64f, 0.38f, "山丘：能走（爬坡慢一点）。站在低处的人被山丘挡住视线——躲在山丘后面他看不见你；站上山丘看得远、L 够得远 1 格，但你也被远远看见。雨天 / 雷雨，冲击（炮弹落地 / 滚石撞停 / 闪电）落在山丘旁 = 泥石流：朝离开山的方向冲 6 格，路变泥地、冲到的人晕。", "山脚下、路边一两格厚；想要泥石流就让巨炮能打到它旁边。");
        T('A', "Mountain", "山", "Mountain", "Terrain", true, true, false, 0, 0.44f, 0.41f, 0.39f, "山：挡路、挡视线。山丘紧挨着山 = 泥石流从山上往下冲（方向一眼看得出）。", "地图边上或中间一条山脉，把镇子分成几片；山脚画山丘和山洞。");
        T('h', "Cave", "山洞", "Cave", "Special", false, false, true, 2, 0.14f, 0.11f, 0.1f, "山洞口：钻进去谁都看不见你（除非他贴身）。两个山洞一对（从上到下、从左到右数：第 1 和第 2 个、第 3 和第 4 个……），在洞里按 E = 从另一头钻出来。马里奥不走隧道（他不知道）。", "画在山 A 的脚下（紧挨着山）；一对山洞放在镇子两头 = 你的秘密近道。");
        T('1', "Door", "房间门", "Room door", "Door", false, false, false, 2, 0.88f, 0.32f, 0.62f, "门 1–9：连到一个横版房间（关卡库里的关卡）。马里奥按日程去门口；你先到门口按 E = 埋伏，进入那个房间开打。", "画在房子墙面前一格；右边面板选它连哪个房间。每个数字只能用一次。");
        T('M', "MarioHome", "马里奥的家", "Mario's home", "Spawn", false, false, false, 2, 0.9f, 0.2f, 0.2f, "马里奥每天从这里出发、最后回到这里。", "只能有一个。");
        T('T', "TricksterSpawn", "捣蛋者出生点", "Trickster spawn", "Spawn", false, false, false, 2, 0.2f, 0.4f, 0.9f, "你每天从这里出发；被马里奥抓到也回到这里。", "只能有一个，离马里奥的家远一点。");
    }

    public static Tile Get(char c)
    {
        if (c == ' ') c = '.';
        if (IsDoor(c)) c = '1';
        return byChar.TryGetValue(c, out var t) ? t : null;
    }

    public static bool IsDoor(char c) => c >= '1' && c <= '9';
    public static bool Known(char c) => Get(c) != null;
    public static bool Solid(char c) { var t = Get(c); return t == null || t.solid; }
    public static bool BlocksSight(char c) { var t = Get(c); return t == null || t.blocksSight; }
    public static bool Hides(char c) { var t = Get(c); return t != null && t.hides; }
    public static int Cost(char c) { var t = Get(c); return t == null || t.solid ? 0 : t.cost; }
}
