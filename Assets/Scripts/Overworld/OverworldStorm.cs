using System;
using System.Collections.Generic;

/// <summary>
/// S220：雷区（关卡编辑时自己画范围）——纯逻辑，网页设计台 overworld.js 有一份一模一样的（同一个哈希，同一批落点）。
/// 规则：每个雷区每 overworldStormVolleySeconds 秒（按游戏时间）劈一轮，一轮同时劈 min..max 道（地图名 + 第几天 + 第几个雷区 + 第几轮 决定，可复现）。
/// 每道闪电：地上先闪 1.2 秒（最后 0.3 秒变白），然后劈下十字形 1 格（中心 + 上下左右）：被劈到 = 掉 1 颗心 + 晕 2 秒。
/// 落点只挑走得到的格子，门 / 马里奥的家 / 你的出生点 周围 1 格不劈（进出门不会被白白劈中）。
/// 雷区的闪电不出声（不叫醒马里奥）、不连锁大机关；只在下雨的天（湿）会引发山丘泥石流。
/// 参考：Don't Starve 雷雨（随机落雷 + 避雷针）、Stardew 雷雨天、攻击预警两段式（https://gdkeys.com/keys-to-combat-design-1-anatomy-of-an-attack/ ）。
/// </summary>
public static class OverworldStorm
{
    /// <summary>每道闪电伤到的格子：十字形，中心 + 上下左右 1 格。</summary>
    public const int BoltReach = 1;
    /// <summary>最后多少秒变白（"马上劈"）。</summary>
    public const float FlashSeconds = 0.3f;

    /// <summary>这个雷区今天劈不劈：always = 每天；否则只有雷雨天。</summary>
    public static bool ActiveOn(OverworldMap.Storm st, OverworldEvents.Day d) => st.always || d.kind == OverworldEvents.Kind.Storm;

    /// <summary>雷区里能劈的格子（从下到上、从左到右）：走得到、离门 / 家 / 出生点至少 2 格（切比雪夫距离）。</summary>
    public static List<OverworldMap.Cell> Strikeable(OverworldMap.Map m, OverworldMap.Storm st)
    {
        var l = new List<OverworldMap.Cell>();
        for (int y = Math.Max(0, st.y0); y <= Math.Min(m.H - 1, st.y1); y++)
            for (int x = Math.Max(0, st.x0); x <= Math.Min(m.W - 1, st.x1); x++)
            {
                if (!OverworldMap.Walkable(m, x, y) || NearSafe(m, x, y)) continue;
                l.Add(new OverworldMap.Cell(x, y));
            }
        return l;
    }

    private static bool NearSafe(OverworldMap.Map m, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
        {
            char c = m.At(x + dx, y + dy);
            if (c == 'M' || c == 'T' || OverworldCatalog.IsDoor(c)) return true;
        }
        return false;
    }

    /// <summary>十字形伤害格（只算走得到的格子——墙后面的人不会被劈）。</summary>
    public static List<OverworldMap.Cell> Plus(OverworldMap.Map m, OverworldMap.Cell c)
    {
        var l = new List<OverworldMap.Cell> { c };
        for (int k = 0; k < 4; k++) { int x = c.x + OverworldProps.DX[k], y = c.y + OverworldProps.DY[k]; if (OverworldMap.Walkable(m, x, y)) l.Add(new OverworldMap.Cell(x, y)); }
        return l;
    }

    public static bool InPlus(OverworldMap.Cell c, double x, double y)
    {
        int px = (int)Math.Floor(x), py = (int)Math.Floor(y);
        return (px == c.x && Math.Abs(py - c.y) <= BoltReach) || (py == c.y && Math.Abs(px - c.x) <= BoltReach);
    }

    /// <summary>现在是第几轮（每个雷区错开一点，不会所有雷区同一秒劈）。</summary>
    public static int VolleyIndex(double minute, float volleySeconds, float minutesPerSecond, int zone)
    {
        double period = Math.Max(1.0, volleySeconds * minutesPerSecond);
        return (int)Math.Floor((minute - OverworldMap.DayStart) / period + zone * 0.37);
    }

    public static uint Seed(string mapName, int day, int zone, int volley)
    {
        uint h = OverworldEvents.Hash(mapName);
        h ^= unchecked((uint)day * 2654435761u);
        h ^= unchecked((uint)(zone + 1) * 2654435769u);
        h ^= unchecked((uint)volley * 2246822507u);
        return Next(Next(h));
    }

    public static uint Next(uint h) { h ^= h << 13; h ^= h >> 17; h ^= h << 5; return h == 0 ? 0x9E3779B9u : h; }

    /// <summary>这一轮劈哪几格（不重复）。可劈的格子比 min 少 = 有多少劈多少。</summary>
    public static List<OverworldMap.Cell> Volley(OverworldMap.Map m, int zone, int day, int volley)
    {
        var res = new List<OverworldMap.Cell>();
        if (zone < 0 || zone >= m.storms.Count) return res;
        var st = m.storms[zone];
        var pool = Strikeable(m, st); if (pool.Count == 0) return res;
        int lo = Math.Max(1, st.min), hi = Math.Max(lo, Math.Min(OverworldMap.MaxBolts, st.max));
        uint h = Seed(m.name, day, zone, volley);
        int n = lo + (int)(h % (uint)(hi - lo + 1));
        for (int k = 0; k < n && pool.Count > 0; k++)
        {
            h = Next(h); int i = (int)(h % (uint)pool.Count);
            res.Add(pool[i]); pool[i] = pool[pool.Count - 1]; pool.RemoveAt(pool.Count - 1);
        }
        return res;
    }

    /// <summary>地图检查：雷区坐标、数量、最少 / 最多、能不能劈到格子、有没有盖住门。</summary>
    public static void Check(OverworldMap.Map m, Action<string, int, int> E, Action<string, int, int> W, Action<string, int, int> I)
    {
        if (m.storms.Count == 0) return;
        if (m.storms.Count > OverworldMap.MaxStorms) E($"雷区最多 {OverworldMap.MaxStorms} 个（现在 {m.storms.Count} 个）", -1, -1);
        for (int i = 0; i < m.storms.Count; i++)
        {
            var st = m.storms[i]; string nm = $"雷区 {i + 1}";
            if (st.x0 < 1 || st.y0 < 1 || st.x1 > m.W - 2 || st.y1 > m.H - 2) { E($"{nm} 超出地图（要在最外一圈以内）", st.x0, st.y0); continue; }
            if (st.min < 1 || st.max > OverworldMap.MaxBolts || st.min > st.max) E($"{nm}：每次劈的道数要满足 1 ≤ 最少 ≤ 最多 ≤ {OverworldMap.MaxBolts}（现在 {st.min}–{st.max}）", st.x0, st.y0);
            var pool = Strikeable(m, st);
            if (pool.Count == 0) { E($"{nm} 里没有能劈的格子（全是墙 / 水，或者紧挨着门 / 家 / 出生点）", st.x0, st.y0); continue; }
            foreach (var d in m.doors)
            {
                foreach (var c in OverworldMap.Find(m, (char)('0' + d.n)))
                    if (c.x >= st.x0 - 1 && c.x <= st.x1 + 1 && c.y >= st.y0 - 1 && c.y <= st.y1 + 1) W($"{nm} 挨着门 {d.n}：门口 1 格不会劈，但他进门的路上会被劈（拖住他是好事，劈太多他会一直晕）", c.x, c.y);
            }
            if (st.min > pool.Count) W($"{nm} 只有 {pool.Count} 格能劈，少于最少 {st.min} 道 → 实际每次最多劈 {pool.Count} 道", st.x0, st.y0);
            I($"{nm}：{st.x1 - st.x0 + 1}×{st.y1 - st.y0 + 1} 格，每次同时劈 {st.min}–{st.max} 道，{(st.always ? "每天都劈" : "只在雷雨天劈")}", st.x0, st.y0);
        }
    }
}
