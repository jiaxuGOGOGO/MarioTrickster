using System.Collections.Generic;

/// <summary>
/// S247：元素"长相"唯一来源 —— 三色（红 坑他 / 蓝 躲·钻 / 黄 目标 / 灰 地形）、2–4 字动词、怎么用、图例一句、图标名。
/// 以前这些散在 Step1Glance（TintOf / Verb / HowTo）、Step1MapLegend（Entries / GroupOf）两处 switch 里，
/// 图例分组和颜色还对不上（油桶、绳套是红色却列在"躲"里）；网页设计台和 Unity 关卡工坊只画色块 + 字母。
/// 现在：游戏里的作战图 / 图例 / 头顶标签、Unity 关卡工坊、网页设计台（build.py 读这个文件）全读这一张表。
/// 新增元素 = ElementCatalog 加一行说明 + 这里加一行长相（测试会提醒漏了哪一项）。
/// 参考：LDtk 实体定义（一份定义管显示和约束）https://ldtk.io/docs/general/editor-components/entities/ ；
///   Super Mario Maker 编辑器"画布就是游戏里的样子" https://www.popmatters.com/delightful-design-in-super-mario-maker-2495458717.html
/// </summary>
public static class ElementLook
{
    public sealed class Look
    {
        public char ch;
        public Step1Glance.Tint tint;
        public string verb;    // 2–4 个字（作战图格子、头顶标签）
        public string howTo;   // 说明卡第二行：怎么用
        public string legend;  // 文字图例一句（没有 = 不进图例）
        public string icon;    // 像素图标名（Step1Art / Step1Icons / OverworldArt 里的名字；null = 只画色块）
    }

    private const string PressL = "伪装在旁边按 L（早按会预约）";

    // 顺序 = 文字图例里的顺序（同组内）。
    private static readonly Look[] all =
    {
        L('#', Step1Glance.Tint.Neutral, "", "", "地面：挡路，<b>炸弹能炸</b>（最外圈/最底层除外）", null),
        L('W', Step1Glance.Tint.Neutral, "", "", "墙：挡路挡视线，<b>炸弹能炸</b>（外圈围墙除外）", null),
        L('%', Step1Glance.Tint.Hide, "可炸", "炸弹能炸开 · 能躲在后面", "裂墙：<b>炸弹能炸开</b>", "CrackedWall"),
        L('x', Step1Glance.Tint.Prank, "踩塌", PressL, "裂缝地板：按 L 踩塌 / 炸弹炸开", "CrackFloor"),
        L('|', Step1Glance.Tint.Hide, "单向", "只能从一侧推开", "捷径门：只能从一侧推开", "OneWayDoor"),
        L('-', Step1Glance.Tint.Neutral, "", "", "单向台面：能从下面跳上去", null),
        L('C', Step1Glance.Tint.Prank, "塌桥", PressL, "塌桥：按 L 让它塌", "CollapsingPlatform"),
        L('c', Step1Glance.Tint.Hide, "可炸", "炸弹能炸开 · 能躲在后面", "箱子：挡路挡视线，<b>炸弹能炸掉</b>", "Crate"),
        L('b', Step1Glance.Tint.Hide, "躲", "走进去他就看不见你", "草丛：能躲", "Bush"),
        L('O', Step1Glance.Tint.Hide, "钻管", "站在管口按 ↓ 钻到另一头", "通风管：按 ↓ 钻到配对的管口", "Vent"),
        L('w', Step1Glance.Tint.Neutral, "毒", "踩上去会变慢", "毒池：减速 + 晕", "PoisonPool"),
        L('g', Step1Glance.Tint.Neutral, "黏", "踩上去会变慢", "黏胶：减速、跳不高", "Glue"),
        L('R', Step1Glance.Tint.Prank, "绊线", PressL, "绊线：他踩到 → 启动你的连锁", "Tripwire"),
        L('U', Step1Glance.Tint.Prank, "会炸", "被火 / 炸弹 / 炮点着就炸，连锁", "油桶：被点燃会爆炸（连锁）", "OilBarrel"),
        L('Q', Step1Glance.Tint.Prank, "关笼", PressL, "铁笼：按 L 落下关人 3 秒", "IronCage"),
        L('Y', Step1Glance.Tint.Prank, "吊人", "谁踩谁被吊，你也小心", "绳套：谁踩谁被吊 10 秒", "SnareTrap"),
        L('?', Step1Glance.Tint.Goal, "道具", "谁先碰到归谁", "道具箱：谁先碰归谁", "PickupSpot"),
        L('K', Step1Glance.Tint.Prank, "开炮", PressL, "大炮：←→↑↓ 瞄准，L 开炮；没弹可钻进去", "Cannon"),
        L('k', Step1Glance.Tint.Prank, "开炮", PressL, null, "Cannon"),
        L('J', Step1Glance.Tint.Prank, "弹飞", PressL, "弹簧板：按 L 弹飞他", "SpringPad"),
        L('n', Step1Glance.Tint.Prank, "滑倒", PressL, "香蕉皮：按 L 让他滑", "BananaPeel"),
        L('~', Step1Glance.Tint.Prank, "喷火", PressL, "火：按 L 喷火", "FireTrap"),
        L('[', Step1Glance.Tint.Prank, "升墙", PressL, "封路墙：按 L 升墙", "ControllableBlocker"),
        L('i', Step1Glance.Tint.Prank, "灭灯", PressL, "灯：夜里照亮一圈；按 L 灭 8 秒", "RoomLamp"),
        L('v', Step1Glance.Tint.Hide, "遁地", "站上去按 U 遁地", "草地：能遁地，<b>土包看不见</b>", "GrassGround"),
        L('z', Step1Glance.Tint.Neutral, "素材", "", "素材槽 1：默认毒池，掉半格心", "PoisonPool"),   // S245：效果看素材包
        L('Z', Step1Glance.Tint.Neutral, "素材", "", "素材槽 2：默认荆棘，掉一格心", "SpikeTrap"),
        L('a', Step1Glance.Tint.Neutral, "素材", "", "素材槽 3：默认回血泉", null),
        L('r', Step1Glance.Tint.Neutral, "素材", "", "素材槽 4：默认蛛网，减速", null),
        L('o', Step1Glance.Tint.Goal, "", "马里奥要去的地方", null, "Collectible"),
        L('G', Step1Glance.Tint.Goal, "", "马里奥要去的地方", null, "GoalZone"),
        L('M', Step1Glance.Tint.Neutral, "", "", null, "Hero0"),
        L('T', Step1Glance.Tint.Neutral, "", "", null, "Imp0"),
        L('d', Step1Glance.Tint.Neutral, "", "", null, "Decor"),
    };

    private static Dictionary<char, Look> byChar;

    private static Look L(char ch, Step1Glance.Tint tint, string verb, string howTo, string legend, string icon)
        => new Look { ch = ch, tint = tint, verb = verb ?? "", howTo = howTo ?? "", legend = legend, icon = icon };

    public static IReadOnlyList<Look> All => all;

    public static Look Get(char ch)
    {
        if (byChar == null) { byChar = new Dictionary<char, Look>(); foreach (var l in all) byChar[l.ch] = l; }
        return byChar.TryGetValue(ch, out var v) ? v : null;
    }

    public static Step1Glance.Tint TintOf(char ch) => Get(ch)?.tint ?? Step1Glance.Tint.Neutral;
    public static string Verb(char ch) => Get(ch)?.verb ?? "";
    public static string HowTo(char ch) => Get(ch)?.howTo ?? "";
    public static string Icon(char ch) => Get(ch)?.icon;

    /// <summary>图例分组 = 三色（以前两套规则各写各的）：红 → 能按 L 的；蓝 / 黄 → 能躲 · 能钻 · 能捡；灰 → 地形。</summary>
    public static int GroupIndex(char ch)
    {
        var t = TintOf(ch);
        return t == Step1Glance.Tint.Prank ? 0 : t == Step1Glance.Tint.Hide || t == Step1Glance.Tint.Goal ? 1 : 2;
    }

    /// <summary>文字图例条目（顺序同表）。</summary>
    public static (char ch, string use)[] LegendEntries()
    {
        var o = new List<(char, string)>();
        foreach (var l in all) if (!string.IsNullOrEmpty(l.legend)) o.Add((l.ch, l.legend));
        return o.ToArray();
    }

    /// <summary>图标像素（RGBA，左下起）：AI 新图 Step1Art（图标 / 角色帧）优先 → 房间图标 Step1Icons → 小镇图标 OverworldArt。没有 = null。</summary>
    public static float[] IconPixels(char ch)
    {
        string n = Icon(ch);
        if (n == null) return null;
        if (Step1Art.Icons.TryGetValue(n, out var art)) return Step1Art.Rgba(art);
        if (Step1Art.Frames.TryGetValue(n, out var frame)) return Step1Art.Rgba(frame);
        if (Step1Icons.ByKey.ContainsKey(n)) { var p = Step1Icons.Pixels(n); if (p != null) return p; }
        return OverworldArt.Pixels(n);
    }

    /// <summary>纯逻辑：这张表漏了什么（给测试和新增元素时看）。step1 = 第 1 步房间能用的元素。</summary>
    public static List<string> Gaps()
    {
        var gaps = new List<string>();
        foreach (var info in ElementCatalog.All)
        {
            if (!info.step1 || info.ch == '.' || info.ch == ' ') continue;
            bool terrainBlock = info.role == ElementCatalog.Role.Terrain && info.ch != 'w' && info.ch != 'g' && info.ch != 'v' && "zZar".IndexOf(info.ch) < 0;
            var l = Get(info.ch);
            if (l == null) { if (!terrainBlock) gaps.Add($"'{info.ch}' {info.zh}：ElementLook 里没有长相（三色 / 动词 / 图标）"); continue; }
            if (l.verb.Length > 4) gaps.Add($"'{info.ch}' 动词超过 4 个字：{l.verb}");
            bool interactive = info.role == ElementCatalog.Role.PlayerPrank || (info.role == ElementCatalog.Role.Special && info.ch != 'd');
            if (interactive && l.verb.Length == 0) gaps.Add($"'{info.ch}' {info.zh}：能互动但没有 2–4 字动词");
            if (interactive && l.icon == null) gaps.Add($"'{info.ch}' {info.zh}：能互动但没有图标");
            if (l.icon != null && IconPixels(info.ch) == null) gaps.Add($"'{info.ch}' 图标名 {l.icon} 找不到像素图");
        }
        return gaps;
    }
}
