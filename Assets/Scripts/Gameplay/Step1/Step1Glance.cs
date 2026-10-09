using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// S242：一目了然（纯逻辑，sim 也编译）。用户："按 M 或者 V 出现的一堆文字……极大增加认知负担，无法让马里奥或者捣蛋者任何一方
/// 清晰的了解场上的所有情况并制定计划策略……生成清晰易懂的美术素材让游戏干爽整洁一目了然要做什么、要避开什么"。
/// 参考与取舍：
///   · Into the Breach —— 把"敌人下一步要干什么"直接画在棋盘上（箭头 + 格子高亮），"为了清晰每次都牺牲酷点子"
///     https://www.gamedeveloper.com/design/-i-into-the-breach-i-dev-on-ui-design-sacrifice-cool-ideas-for-the-sake-of-clarity-every-time-
///     https://www.rockpapershotgun.com/into-the-breach-interface-design
///     → 这里：马里奥接下来要走的路画成虚线，路上的机关标"埋伏点"和他几秒后到。
///   · Riot《Clarity in League》+ LoL VFX 指南 —— 颜色分功能、少即是多、重要的东西先被看见
///     https://www.leagueoflegends.com/en-us/news/dev/clarity-in-league/ https://www.vfxapprentice.com/blog/10-league-of-legends-vfx-design-tips
///     → 这里：只用三种颜色：红 = 坑他、蓝 = 躲 / 钻、黄 = 目标；文字最多 2–4 个字。
///   · Untitled Goose Game —— 待办清单用图标，头顶意图气泡 https://atomicbobomb.home.blog/2019/10/12/untitled-goose-game-and-ui/
///     → 这里：马里奥头顶一个图标气泡（宝箱 / 出口 / ? / !），不写字。
///   · Mark of the Ninja —— 敌人能看到 / 听到的范围画在世界里，而不是写在面板上
///     https://markoftheninja.fandom.com/wiki/User_blog:JAlbor/GDC_2013:_Mark_of_the_Ninja_and_the_Five_Genre_Heresies
///   · 反例：侦探视觉开着不关 = 拐杖，玩家不看世界只看滤镜 https://gameluster.com/detective-vision-an-unwanted-crutch/
///     → 这里：作战图是按住 / 开关的"看一眼"，平时画面只留最少的东西（路线虚线淡淡的一条）。
/// 马里奥的路线 = 他自己的寻路（和他实际走的同一个 LevelPathPlanner），不是读你的状态（H4 无关：这是给你看的显示层）。
/// </summary>
public static class Step1Glance
{
    public enum Tint { Prank, Hide, Goal, Neutral }

    /// <summary>三色（颜色和形状双编码：色弱也能靠图标分）。</summary>
    public static Color ColorOf(Tint t)
    {
        switch (t)
        {
            case Tint.Prank: return new Color(1f, 0.32f, 0.3f);
            case Tint.Hide: return new Color(0.35f, 0.7f, 1f);
            case Tint.Goal: return new Color(1f, 0.85f, 0.25f);
            default: return new Color(0.85f, 0.85f, 0.85f);
        }
    }

    public static Tint TintOf(char ch)
    {
        switch (ch)
        {
            case '~': case '[': case 'C': case 'J': case 'n': case 'x': case 'Q': case 'K': case 'k': case 'i': case 'R': case 'U': case 'Y': return Tint.Prank;
            case 'b': case 'O': case 'v': case 'c': case '%': case '|': return Tint.Hide;
            case 'o': case 'G': case '?': return Tint.Goal;
            default: return Tint.Neutral;
        }
    }

    /// <summary>每个东西 2–4 个字的动词（"按 L 干什么" / "拿它干什么"），代替以前一整句的说明。</summary>
    public static string Verb(char ch)
    {
        switch (ch)
        {
            case '~': return "喷火";
            case '[': return "升墙";
            case 'C': return "塌桥";
            case 'J': return "弹飞";
            case 'n': return "滑倒";
            case 'x': return "踩塌";
            case 'Q': return "关笼";
            case 'K': case 'k': return "开炮";
            case 'i': return "灭灯";
            case 'R': return "绊线";
            case 'U': return "会炸";
            case 'Y': return "吊人";
            case 'b': return "躲";
            case 'O': return "钻管";
            case 'v': return "遁地";
            case 'c': return "可炸";
            case '%': return "可炸";
            case '|': return "单向";
            case '?': return "道具";
            case 'w': return "毒";
            case 'z': case 'Z': case 'a': case 'r': return "素材"; // S245：素材槽（效果看素材包）
            case 'g': return "黏";
            default: return "";
        }
    }

    /// <summary>说明卡第二行：怎么用（按什么键），一句话。</summary>
    public static string HowTo(char ch)
    {
        switch (TintOf(ch))
        {
            case Tint.Prank: return ch == 'U' ? "被火 / 炸弹 / 炮点着就炸，连锁" : ch == 'Y' ? "谁踩谁被吊，你也小心" : "伪装在旁边按 L（早按会预约）";
            case Tint.Hide: return ch == 'O' ? "站在管口按 ↓ 钻到另一头" : ch == 'v' ? "站上去按 U 遁地" : ch == 'b' ? "走进去他就看不见你" : ch == '|' ? "只能从一侧推开" : "炸弹能炸开 · 能躲在后面";
            case Tint.Goal: return ch == '?' ? "谁先碰到归谁" : "马里奥要去的地方";
            default: return ch == 'w' || ch == 'g' ? "踩上去会变慢" : "";
        }
    }

    /// <summary>马里奥头顶的意图图标（只来自他的心智状态 + 是否拿着宝物）。</summary>
    public enum Intent { Loot, Exit, Curious, Chase, Search, Stunned }

    public static Intent IntentOf(string mindState, bool carryingLoot, bool stunned)
    {
        if (stunned) return Intent.Stunned;
        switch (mindState)
        {
            case "Chasing": return Intent.Chase;
            case "Curious": case "Investigating": return Intent.Curious;
            case "Searching": return Intent.Search;
            default: return carryingLoot ? Intent.Exit : Intent.Loot;
        }
    }

    /// <summary>意图图标名（Step1Icons 里的徽章）和 2 个字的提示。</summary>
    public static string IntentIcon(Intent i)
    {
        switch (i)
        {
            case Intent.Loot: return "BadgeLoot";
            case Intent.Exit: return "BadgeExit";
            case Intent.Curious: return "BadgeQuestion";
            case Intent.Chase: return "BadgeAlert";
            case Intent.Search: return "BadgeEye";
            default: return "BadgeStar";
        }
    }

    public static string IntentZh(Intent i)
    {
        switch (i)
        {
            case Intent.Loot: return "去拿宝";
            case Intent.Exit: return "要逃了";
            case Intent.Curious: return "起疑";
            case Intent.Chase: return "追你";
            case Intent.Search: return "找你";
            default: return "晕了";
        }
    }

    /// <summary>路线上的埋伏点：离路线 ≤ reach 格的机关，按他先到哪个排序，附"几秒后到"。</summary>
    public struct Ambush { public Vector2 pos; public char ch; public float eta; public int pathIndex; }

    public static List<Ambush> AmbushesOnRoute(IList<Vector2> route, IList<(Vector2 pos, char ch, bool usable)> props, float marioSpeed, float reach = 1.2f, int max = 3)
    {
        var o = new List<Ambush>();
        if (route == null || route.Count == 0 || props == null) return o;
        var cum = new float[route.Count];
        for (int i = 1; i < route.Count; i++) cum[i] = cum[i - 1] + Vector2.Distance(route[i - 1], route[i]);
        foreach (var p in props)
        {
            if (!p.usable || TintOf(p.ch) != Tint.Prank) continue;
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < route.Count; i++) { float d = Vector2.Distance(route[i], p.pos); if (d < bd) { bd = d; best = i; } }
            if (best < 0 || bd > reach) continue;
            o.Add(new Ambush { pos = p.pos, ch = p.ch, pathIndex = best, eta = cum[best] / Mathf.Max(0.5f, marioSpeed) });
        }
        return o.OrderBy(a => a.pathIndex).Take(Mathf.Max(0, max)).ToList();
    }

    /// <summary>路线降采样成虚线点（每 spacing 格一个点）。</summary>
    public static List<Vector2> Dashes(IList<Vector2> route, float spacing)
    {
        var o = new List<Vector2>();
        if (route == null || route.Count == 0 || spacing <= 0f) return o;
        o.Add(route[0]); float carry = 0f;
        for (int i = 1; i < route.Count; i++)
        {
            Vector2 a = route[i - 1], b = route[i]; float seg = Vector2.Distance(a, b); float d = spacing - carry;
            while (d <= seg) { o.Add(a + (b - a) * (d / seg)); d += spacing; }
            carry = seg - (d - spacing);
        }
        return o;
    }

    /// <summary>说明卡：只给离你最近的一个东西（≤ radius 格）。没有 = -1。</summary>
    public static int Nearest(IList<Vector2> things, Vector2 you, float radius)
    {
        int best = -1; float bd = radius;
        for (int i = 0; i < (things?.Count ?? 0); i++) { float d = Vector2.Distance(things[i], you); if (d <= bd) { bd = d; best = i; } }
        return best;
    }

    /// <summary>ETA 文字（"3 秒" / "马上"）。</summary>
    public static string EtaText(float eta) => eta < 0.8f ? "马上" : Mathf.RoundToInt(eta) + " 秒";
}
