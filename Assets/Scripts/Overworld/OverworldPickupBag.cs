using System.Collections.Generic;

/// <summary>
/// S232：小镇道具箱 ? 不再永远是"+1 炸弹"，改成洗牌袋（Tetris 7-bag 的规则：一袋里每种都有，抽完再洗一袋 → 不会连着好几天都是同一种，也不会一种很久不来）。
/// 输入随机（Keith Burgun）：早上 06:00 就公布今天每个箱子里是什么，你先知道再决定绕不绕路去捡；捡到的效果是确定的。
/// 同一张地图、同一天、同一个箱子 → 永远一样（地图名 + 天数做种子；网页 owPickupBag 逐字一致，verify 对照）。
/// 规则保障：只给你好处、上限不变（炸弹仍 ≤ MaxBonusBombs、心 ≤ 3、能量 ≤ 3），不影响马里奥能不能通关（H1/H10）；马里奥不读（H4）。
/// </summary>
public static class OverworldPickupBag
{
    public enum Kind { Bomb, Energy, Taunt, Heart }
    /// <summary>一袋 = 5 个：炸弹 2、能量 1、挑衅 1、补心 1（炸弹是原来唯一的效果，保留最多）。</summary>
    public static readonly Kind[] Bag = { Kind.Bomb, Kind.Bomb, Kind.Energy, Kind.Taunt, Kind.Heart };

    public static uint Step(uint x) { x ^= x << 13; x ^= x >> 17; x ^= x << 5; return x; }

    /// <summary>第 cycle 袋洗好的顺序。</summary>
    public static Kind[] Shuffled(string mapName, int cycle)
    {
        var b = (Kind[])Bag.Clone();
        uint x = OverworldEvents.Hash(mapName) ^ unchecked((uint)cycle * 2654435761u); if (x == 0) x = 1;
        for (int i = b.Length - 1; i > 0; i--) { x = Step(x); int j = (int)(x % (uint)(i + 1)); var t = b[i]; b[i] = b[j]; b[j] = t; }
        return b;
    }

    /// <summary>第 day 天第 box 个箱子（按地图从上到下、从左到右数，0 起）。第 1 天全是炸弹（先学规则，和天气第 1 天晴一样）。</summary>
    public static Kind Of(string mapName, int day, int box, int boxCount)
    {
        if (day <= 1) return Kind.Bomb;
        int k = (day - 2) * System.Math.Max(1, boxCount) + box;
        return Shuffled(mapName, k / Bag.Length)[k % Bag.Length];
    }

    public static int IndexOf(OverworldMap.Map m, int x, int y)
    {
        var l = OverworldMap.Find(m, '?');
        for (int i = 0; i < l.Count; i++) if (l[i].x == x && l[i].y == y) return i;
        return -1;
    }

    public static string Zh(Kind k)
    {
        switch (k)
        {
            case Kind.Energy: return "◆能量 +1";
            case Kind.Taunt: return "📣挑衅 +1";
            case Kind.Heart: return "❤补心 +1";
            default: return "💣炸弹 +1";
        }
    }

    /// <summary>早上公布的一行（没有箱子 = ""）。</summary>
    public static string MorningLine(OverworldMap.Map m, int day)
    {
        var l = OverworldMap.Find(m, '?'); if (l.Count == 0) return "";
        var parts = new List<string>();
        for (int i = 0; i < l.Count; i++) parts.Add($"({l[i].x},{l[i].y}) {Zh(Of(m.name, day, i, l.Count))}");
        return "\n今天的道具箱：" + string.Join("  ", parts);
    }

    /// <summary>编辑器 / 网页预览：第 from 天起 n 天，每天每个箱子。</summary>
    public static List<string> Preview(OverworldMap.Map m, int from, int n)
    {
        var res = new List<string>(); var l = OverworldMap.Find(m, '?'); if (l.Count == 0) return res;
        for (int d = from; d < from + n; d++)
        {
            var parts = new List<string>();
            for (int i = 0; i < l.Count; i++) parts.Add(Zh(Of(m.name, d, i, l.Count)));
            res.Add($"第 {d} 天 道具箱：" + string.Join(" / ", parts));
        }
        return res;
    }
}
