using System.Collections.Generic;
using System.Linq;

/// <summary>
/// S215：一天总览（纯逻辑，沙盒可测；网页 overworld.js 的 owLedger 逐行一致）。
/// 回答制作人最常问的全局问题：这一天按顺序玩到哪几个房间、每个房间主打什么机关、哪一扇门第一次教玩家一种新机关、有没有两扇门重复同一个主角。
/// 规则来源：Nintendo"四步关卡设计"（GMTK 分析《超级马里奥 3D 世界》：一关围绕一个机关，先安全地介绍，再加变化）——
///   所以只提醒两件事：① 连着两扇门主角机关一样（像在重复）；② 一扇门里一口气第一次出现 3 种以上机关（一次教太多）。
/// 只统计 PlayerPrank + 连锁零件（绊线 R / 油桶 U），和"策略模拟"的"途经机关"同一口径。
/// </summary>
public static class CampaignLedger
{
    public sealed class Room
    {
        public int door; public string clock = "", room = "";
        public bool missing;
        /// <summary>机关种类 → 个数（按 themeKey 合并：左右两种大炮算一种）。按目录顺序。</summary>
        public readonly List<(string key, string zh, int n)> kinds = new List<(string, string, int)>();
        public string star = "";   // 主角机关（最多的那种；一样多取目录里靠前的）
        public readonly List<string> firstTime = new List<string>(); // 这扇门第一次出现的机关（中文名）
        public int pickups, total;
    }

    public sealed class Report
    {
        public readonly List<Room> rooms = new List<Room>();
        public readonly List<string> warnings = new List<string>();
        public readonly List<string> allKinds = new List<string>();
        public int maxBombs;
    }

    public static string ShortZh(string zh) { int i = zh.IndexOf('（'); return i > 0 ? zh.Substring(0, i) : zh; }

    public static bool Counts(ElementCatalog.Info e) => e != null && (e.role == ElementCatalog.Role.PlayerPrank || ComboRouteAnalyzer.IsChainPart(e.ch));

    /// <summary>resolve(房间名) → 网格（行 0 在最上面）或 null。按门的时间顺序（和马里奥的日程一样）。</summary>
    public static Report Build(OverworldMap.Map m, System.Func<string, IList<string>> resolve, int bombsPerRound, int maxBonus)
    {
        var rep = new Report { maxBombs = bombsPerRound + maxBonus };
        var seen = new HashSet<string>();
        var order = new List<string>(); foreach (var e in ElementCatalog.All) if (Counts(e) && !order.Contains(e.themeKey)) order.Add(e.themeKey);
        foreach (var d in m.doors.OrderBy(d => d.minute).ThenBy(d => d.n))
        {
            if (OverworldMap.Find(m, (char)('0' + d.n)).Count != 1) continue;
            var r = new Room { door = d.n, clock = OverworldMap.Clock(d.minute), room = d.room };
            var g = resolve(d.room);
            if (g == null) { r.missing = true; rep.rooms.Add(r); continue; }
            var cnt = new Dictionary<string, int>(); var zh = new Dictionary<string, string>();
            foreach (var row in g) foreach (char c0 in Step1Layout.StripSlots(row))
            {
                if (c0 == '?') { r.pickups++; continue; }
                var e = ElementCatalog.Get(c0); if (!Counts(e)) continue;
                cnt[e.themeKey] = (cnt.TryGetValue(e.themeKey, out var n) ? n : 0) + 1; zh[e.themeKey] = ShortZh(e.zh); r.total++;
            }
            int best = 0;
            foreach (var k in order) if (cnt.TryGetValue(k, out var n)) { r.kinds.Add((k, zh[k], n)); if (n > best) { best = n; r.star = zh[k]; } }
            foreach (var k in r.kinds) if (seen.Add(k.key)) { r.firstTime.Add(k.zh); rep.allKinds.Add(k.zh); }
            rep.rooms.Add(r);
        }
        for (int i = 0; i < rep.rooms.Count; i++)
        {
            var r = rep.rooms[i];
            if (r.missing) { rep.warnings.Add($"门 {r.door}：找不到房间「{r.room}」"); continue; }
            if (r.total == 0) rep.warnings.Add($"门 {r.door}「{r.room}」里没有机关：进门只能躲，没有捣蛋的乐趣");
            if (r.firstTime.Count >= 3) rep.warnings.Add($"门 {r.door}「{r.room}」一口气第一次出现 {r.firstTime.Count} 种机关（{string.Join("、", r.firstTime)}）：一次教太多，玩家记不住。前面的门先放一两种");
            if (i > 0 && !rep.rooms[i - 1].missing && r.star.Length > 0 && r.star == rep.rooms[i - 1].star) rep.warnings.Add($"门 {rep.rooms[i - 1].door} 和门 {r.door} 主角都是{r.star}：连着两扇门像在重复。换一个房间，或在后一扇门加一种变化");
        }
        return rep;
    }

    /// <summary>对照 / 测试用：一行一条。</summary>
    public static List<string> Lines(Report rep)
    {
        var l = new List<string>();
        foreach (var r in rep.rooms) l.Add(r.missing ? $"门{r.door} ?" : $"门{r.door} {r.room} 主角={r.star} 共{r.total} 道具箱{r.pickups} 新={string.Join("、", r.firstTime)} [{string.Join(" ", r.kinds.Select(k => k.zh + k.n))}]");
        l.AddRange(rep.warnings);
        return l;
    }
}
