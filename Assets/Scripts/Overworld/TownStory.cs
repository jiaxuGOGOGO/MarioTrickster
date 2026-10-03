using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// S232：小镇居民 + 故事片段（storylet）。纯逻辑，沙盒可测；只管"说什么话"，不碰玩法（H4：马里奥 AI 读不到这里）。
///
/// 为什么这样做（调研来源见 docs/step1/S232_STORIES_RANDOM_TUNING.md §2）：
///  - 故事片段 = 一句话 + 前提 + 效果；前提只看"世界现在的状态"，不看你走过的具体路线（Emily Short：避免 time cave，以后随便加新句子）。
///  - 选句规则 = Valve《Left 4 Dead》动态对白（Elan Ruskin GDC 2012）：条件全满足的句子里，条件越多越具体 → 分越高；同分随机；
///    说过的写回记忆、带冷却（防止同一个梗太密）。
///  - 三档优先级 = Hades（Kasavin）：0 刚发生的大事 &gt; 1 这个人自己的故事线 &gt; 2 闲话池。
///  - 真情要"挣"来：同一户来往几次以后才出现（MOTHER2：玩过的时间让一句普通的话活过来）；
///    一句真情 = 小小的伤口 + 一个玩笑（糸井重里「引っかき傷＋冗談」）；节奏 ≈ 几句好笑的才一句真情（Undertale 的推拉）。
///  - 输了也推进故事（Hades：死一次故事走一步）：被偷、没赶上也有专门的话。
/// 数据：Assets/Resources/TownStories.json（可直接改；读不到用 Default，sim 逐句对照两份一致）。
/// </summary>
public static class TownStory
{
    public enum When { Morning, Back, DayEnd, Witness } // S233：Witness = 大机关砸中马里奥时，旁边那户人家当场喊一句

    public sealed class Line
    {
        public string id = "", who = "door", when = "back", tone = "comic", zh = "", en = "", note = "";
        public int tier = 2, coolDays = 3;
        public bool once;
        public string[] needs = new string[0];
        public bool Sincere => tone == "sincere";
    }

    public sealed class Resident { public int door; public string name = "", trait = ""; }

    // ── 住户（门 1–9 住着谁）──────────────────────────────
    public static readonly string[] Traits = { "baker", "granny", "kid", "mayor", "painter", "guard" };
    public static string TraitZh(string t)
    {
        switch (t)
        {
            case "baker": return "面包师"; case "granny": return "老奶奶"; case "kid": return "小孩"; case "mayor": return "镇长";
            case "painter": return "画家"; case "guard": return "守夜人"; default: return "居民";
        }
    }
    private static readonly string[] DefaultNames = { "阿梅", "桂婆婆", "小豆", "老镇长", "小林", "大熊", "阿梅二号", "桂婆婆的妹妹", "小豆的哥哥" };
    /// <summary>地图没写住户时的默认住户（门 n → 第 n 个名字、性格按顺序轮流）。</summary>
    public static Resident DefaultResident(int door)
    {
        int i = Math.Max(1, Math.Min(9, door)) - 1;
        return new Resident { door = door, name = DefaultNames[i], trait = Traits[i % Traits.Length] };
    }
    public static Resident ResidentOf(OverworldMap.Map m, int door)
    {
        var r = m != null ? m.residents.FirstOrDefault(x => x.door == door) : null;
        if (r == null || r.name.Length == 0) return DefaultResident(door);
        return new Resident { door = door, name = r.name, trait = Traits.Contains(r.trait) ? r.trait : DefaultResident(door).trait };
    }

    // ── 记忆（跨天保留，重新进 Play 才清空；只记"发生过什么"，不影响玩法）────────────
    public sealed class Memory
    {
        public readonly Dictionary<string, int> lastDay = new Dictionary<string, int>();
        public readonly HashSet<string> said = new HashSet<string>();
        public readonly Dictionary<int, int> visits = new Dictionary<int, int>(), defended = new Dictionary<int, int>(), looted = new Dictionary<int, int>(), missed = new Dictionary<int, int>(), witnessed = new Dictionary<int, int>(); // S233：witnessed = 在他家门口看到过几次大动静
        public readonly Dictionary<string, int> unlockDay = new Dictionary<string, int>(); // S233：真心话第一次听到是"累计第几天"（笔记本用）
        public int totalDays; // S233：累计玩过几天（跨次存档）
        public readonly List<string> today = new List<string>();
        public int comicSinceSincere = SincereEvery, sincereDay = -1, sincereTotal;
        public readonly Dictionary<int, int> witnessDay = new Dictionary<int, int>(); // 今天这户人家喊过没有（不存档）
        // 昨天（早上的闲话用）
        public string yOutcome = "", yDeath = ""; public int yBigHits, yDefended, yLooted, yDay;
        public int Get(Dictionary<int, int> d, int door) => d.TryGetValue(door, out var v) ? v : 0;
        public void Add(Dictionary<int, int> d, int door) { if (door > 0) d[door] = Get(d, door) + 1; }
    }
    public static Memory Mem = new Memory();
    public static void Reset() { Mem = new Memory(); }

    /// <summary>真情句之间至少隔几句好笑的（Undertale 式推拉；起点值，按试玩调）。每天最多 1 句真情。</summary>
    public const int SincereEvery = 3;
    /// <summary>一户人家至少要有几句"回到小镇"的话，才不会玩几天就重复（冷却 2 天 × 守住/被偷两种结果 + 余量）。</summary>
    public const int MinBackLinesPerTrait = 5;

    // ── 选句 ─────────────────────────────────────────
    /// <summary>needs 里每一条的格式：key=value、key!=value、key&gt;=n、key&lt;=n。全部满足才算候选；满足的条数 = 分数（越具体越优先）。</summary>
    public static bool Match(string need, IDictionary<string, string> facts)
    {
        string op = need.Contains(">=") ? ">=" : need.Contains("<=") ? "<=" : need.Contains("!=") ? "!=" : "=";
        int i = need.IndexOf(op, StringComparison.Ordinal); if (i <= 0) return false;
        string k = need.Substring(0, i).Trim(), v = need.Substring(i + op.Length).Trim();
        facts.TryGetValue(k, out var f); f = f ?? "";
        if (op == "=") return f == v;
        if (op == "!=") return f != v;
        if (!int.TryParse(f, out int a) || !int.TryParse(v, out int b)) return false;
        return op == ">=" ? a >= b : a <= b;
    }

    public static uint Hash(string s) { uint h = 2166136261u; foreach (char c in s ?? "") { h ^= c; h = unchecked(h * 16777619u); } return h; }

    /// <summary>从表里挑一句（null = 没有能说的）。不写记忆——调用 Say 才算"说过"。</summary>
    public static Line Pick(IList<Line> table, When when, string who, IDictionary<string, string> facts, Memory mem, int day)
    {
        string w = when.ToString().ToLowerInvariant();
        Line best = null; int bestTier = int.MaxValue, bestScore = -1, bestLast = int.MaxValue; uint bestTie = 0;
        bool sincereOk = mem.comicSinceSincere >= SincereEvery && mem.sincereDay != day;
        foreach (var l in table)
        {
            if (l.when != w || l.who != who) continue;
            if (l.once && mem.said.Contains(l.id)) continue;
            if (mem.lastDay.TryGetValue(l.id, out int ld) && day - ld < Math.Max(1, l.coolDays)) continue;
            if (l.Sincere && !sincereOk) continue;
            if (!l.needs.All(n => Match(n, facts))) continue;
            int score = l.needs.Length, last = mem.lastDay.TryGetValue(l.id, out int x) ? x : -1;
            uint tie = Hash(l.id + "|" + day + "|" + (facts.TryGetValue("door", out var d) ? d : ""));
            bool better = l.tier < bestTier || (l.tier == bestTier && (score > bestScore || (score == bestScore && (last < bestLast || (last == bestLast && tie < bestTie)))));
            if (best == null || better) { best = l; bestTier = l.tier; bestScore = score; bestLast = last; bestTie = tie; }
        }
        if (best == null) return w == "witness" ? null : PickFallback(table, w, who, facts, mem, day); // S233：当场喊的话不兜底（宁可不说，也不重复）
        return best;
    }
    /// <summary>兜底：冷却中的好笑话也可以说（挑最久没说的那句）——宁可重复，也不让人家门口没话说。真心话 / 只说一次的不兜底。</summary>
    private static Line PickFallback(IList<Line> table, string w, string who, IDictionary<string, string> facts, Memory mem, int day)
    {
        Line best = null; int bestLast = int.MaxValue, bestScore = -1;
        foreach (var l in table)
        {
            if (l.when != w || l.who != who || l.once || l.Sincere || !l.needs.All(n => Match(n, facts))) continue;
            int last = mem.lastDay.TryGetValue(l.id, out int x) ? x : -1, score = l.needs.Length;
            if (best == null || last < bestLast || (last == bestLast && score > bestScore)) { best = l; bestLast = last; bestScore = score; }
        }
        return best;
    }

    /// <summary>说出这一句：写回记忆（冷却、只说一次、真情节奏）。</summary>
    public static void Say(Line l, Memory mem, int day)
    {
        if (l == null) return;
        mem.lastDay[l.id] = day; mem.said.Add(l.id); mem.today.Add(l.id);
        if (l.Sincere) { mem.comicSinceSincere = 0; mem.sincereDay = day; mem.sincereTotal++; if (!mem.unlockDay.ContainsKey(l.id)) mem.unlockDay[l.id] = mem.totalDays + 1; }
        else mem.comicSinceSincere++;
    }

    public static string Fill(string s, Resident r, IDictionary<string, string> facts)
    {
        s = (s ?? "").Replace("{name}", r != null ? r.name : "").Replace("{trait}", r != null ? TraitZh(r.trait) : "");
        if (facts != null) foreach (var kv in facts) s = s.Replace("{" + kv.Key + "}", kv.Value);
        return s;
    }

    /// <summary>屏幕上显示的一段（谁说的 + 中文 + 英文）。</summary>
    public static string Show(Line l, Resident r, IDictionary<string, string> facts)
    {
        if (l == null) return "";
        string who = l.who == "door" && r != null ? $"{TraitZh(r.trait)}·{r.name}" : l.who == "mario" ? "马里奥" : "镇上的闲话";
        return $"{who}：{Fill(l.zh, r, facts)}\n{Fill(l.en, r, facts)}";
    }

    // ── 事实（facts）：只描述已经发生的事 ─────────────────────
    public static Dictionary<string, string> DoorFacts(Resident r, OverworldSession.DoorResult result, OverworldEvents.Day weather, Memory mem)
    {
        int n = r.door;
        return new Dictionary<string, string>
        {
            { "door", n.ToString() }, { "trait", r.trait }, { "result", result == OverworldSession.DoorResult.Defended ? "defended" : result == OverworldSession.DoorResult.Looted ? "looted" : result == OverworldSession.DoorResult.Missed ? "missed" : "" },
            { "weather", weather.kind.ToString().ToLowerInvariant() }, { "visits", mem.Get(mem.visits, n).ToString() },
            { "defended", mem.Get(mem.defended, n).ToString() }, { "looted", mem.Get(mem.looted, n).ToString() }, { "missed", mem.Get(mem.missed, n).ToString() }, { "witnessed", mem.Get(mem.witnessed, n).ToString() },
            { "day", OverworldSession.Day.ToString() }, { "n", mem.Get(mem.visits, n).ToString() },
        };
    }

    public static Dictionary<string, string> DayFacts(int doorCount, OverworldEvents.Day weather)
    {
        return new Dictionary<string, string>
        {
            { "outcome", OverworldSession.Outcome(doorCount) }, { "death", OverworldSession.Death.ToString().ToLowerInvariant() },
            { "byyou", OverworldSession.DeathByYou ? "yes" : "no" }, { "cause", OverworldSession.DeathCause == ' ' || OverworldSession.DeathCause == '\0' ? "" : OverworldSession.DeathCause.ToString() },
            { "bighits", OverworldSession.BigHits.ToString() }, { "chain", OverworldSession.BestChain.ToString() }, { "caught", OverworldSession.Caught.ToString() },
            { "defendedtoday", OverworldSession.Count(OverworldSession.DoorResult.Defended).ToString() }, { "lootedtoday", OverworldSession.Count(OverworldSession.DoorResult.Looted).ToString() },
            { "weather", weather.kind.ToString().ToLowerInvariant() }, { "day", OverworldSession.Day.ToString() }, { "bells", OverworldSession.BellRings.ToString() },
        };
    }

    public static Dictionary<string, string> MorningFacts(OverworldEvents.Day weather, Memory mem)
    {
        return new Dictionary<string, string>
        {
            { "yesterday", mem.yDay > 0 ? mem.yOutcome : "none" }, { "ydeath", mem.yDeath }, { "ybighits", mem.yBigHits.ToString() },
            { "ydefended", mem.yDefended.ToString() }, { "ylooted", mem.yLooted.ToString() },
            { "weather", weather.kind.ToString().ToLowerInvariant() }, { "day", OverworldSession.Day.ToString() },
        };
    }

    /// <summary>一天结束：把今天记成"昨天"（第二天早上的闲话用）。</summary>
    public static void CloseDay(Memory mem, int doorCount)
    {
        mem.totalDays++;
        mem.yDay = OverworldSession.Day; mem.yOutcome = OverworldSession.Outcome(doorCount); mem.yDeath = OverworldSession.Death.ToString().ToLowerInvariant();
        mem.yBigHits = OverworldSession.BigHits; mem.yDefended = OverworldSession.Count(OverworldSession.DoorResult.Defended); mem.yLooted = OverworldSession.Count(OverworldSession.DoorResult.Looted);
    }

    // ── 接线入口（OverworldSession / OverworldGame 调用）──────────────
    /// <summary>刚打完 / 错过的门（回到小镇时由这户人家说一句）。0 = 没有。</summary>
    public static int PendingDoor;
    public static OverworldSession.DoorResult PendingResult;
    /// <summary>OverworldSession.RecordRoom / RecordMissed 调用：记一次来往（真情句靠它"挣"出来）。</summary>
    public static void Record(int door, OverworldSession.DoorResult r)
    {
        if (door <= 0) return;
        Mem.Add(Mem.visits, door);
        if (r == OverworldSession.DoorResult.Defended) Mem.Add(Mem.defended, door);
        else if (r == OverworldSession.DoorResult.Looted) Mem.Add(Mem.looted, door);
        else if (r == OverworldSession.DoorResult.Missed) Mem.Add(Mem.missed, door);
        PendingDoor = door; PendingResult = r;
    }

    /// <summary>挑一句、说出来（写记忆），返回屏幕上的文字；没有能说的 = ""。tone 返回这句是不是真情（画面上停久一点）。</summary>
    public static string Next(OverworldMap.Map m, When when, int door, OverworldEvents.Day weather, int doorCount, out bool sincere)
    {
        sincere = false;
        var mem = Mem; int day = OverworldSession.Day;
        Resident r = null; Dictionary<string, string> facts; string who;
        if (when == When.Back)
        {
            r = ResidentOf(m, door); facts = DoorFacts(r, PendingDoor == door ? PendingResult : OverworldSession.ResultOf(door), weather, mem); who = "door";
            facts["bighit"] = OverworldSession.LastBigHitMinute > -9000 ? "yes" : "no";
            facts["bells"] = OverworldSession.BellRings.ToString();
        }
        else if (when == When.Morning) { facts = MorningFacts(weather, mem); who = "town"; }
        else { facts = DayFacts(doorCount, weather); who = "mario"; }
        var l = Pick(Table, when, who, facts, mem, day);
        if (when == When.Back && PendingDoor == door) PendingDoor = 0;
        if (l == null) return "";
        Say(l, mem, day); sincere = l.Sincere;
        return Show(l, r, facts);
    }

    // ── S233：当场喊一句（大机关砸中马里奥，离他最近的那户人家看见了）──────────
    /// <summary>离 (x,y) 最近、且在 maxDist 格以内的门（按门格中心算）；没有 = 0。纯函数，网页 tsNearestDoor 一样。</summary>
    public static int NearestDoor(OverworldMap.Map m, double x, double y, double maxDist)
    {
        int best = 0; double bd = maxDist * maxDist + 1e-9;
        foreach (var d in m.doors.OrderBy(d => d.n))
        {
            var c = OverworldMap.Find(m, (char)('0' + d.n)); if (c.Count != 1) continue;
            double dx = c[0].x + 0.5 - x, dy = c[0].y + 0.5 - y, dd = dx * dx + dy * dy;
            if (dd < bd) { bd = dd; best = d.n; }
        }
        return best;
    }
    /// <summary>看见的范围（格）。在这以内的人家才"看得见"。</summary>
    public const double WitnessRange = 7;
    public static Dictionary<string, string> WitnessFacts(Resident r, char cause, OverworldEvents.Day weather, Memory mem, int day)
    {
        return new Dictionary<string, string>
        {
            { "door", r.door.ToString() }, { "trait", r.trait }, { "cause", cause == ' ' || cause == '\0' ? "" : cause.ToString() },
            { "weather", weather.kind.ToString().ToLowerInvariant() }, { "witnessed", mem.Get(mem.witnessed, r.door).ToString() },
            { "visits", mem.Get(mem.visits, r.door).ToString() }, { "day", day.ToString() },
        };
    }
    /// <summary>纯函数版（彩排 / sim / 网页 tsWitness 同一套）：记一次"在他家门口看见"，挑一句当场喊的。一户人家一天最多喊一次（不兜底）。</summary>
    public static Line WitnessOn(OverworldMap.Map m, IList<Line> t, Memory mem, int door, char cause, OverworldEvents.Day weather, int day)
    {
        if (door <= 0) return null;
        if (mem.witnessDay.TryGetValue(door, out int wd) && wd == day) return null;
        mem.Add(mem.witnessed, door);
        var l = Pick(t, When.Witness, "door", WitnessFacts(ResidentOf(m, door), cause, weather, mem, day), mem, day);
        mem.witnessDay[door] = day;
        if (l != null) Say(l, mem, day);
        return l;
    }
    /// <summary>OverworldGame 调用（大机关砸中马里奥那一帧）：返回屏幕上的文字；没话说 = ""。</summary>
    public static string Witness(OverworldMap.Map m, int door, char cause, OverworldEvents.Day weather, out bool sincere)
    {
        sincere = false;
        var l = WitnessOn(m, Table, Mem, door, cause, weather, OverworldSession.Day);
        if (l == null) return "";
        sincere = l.Sincere;
        return Show(l, ResidentOf(m, door), WitnessFacts(ResidentOf(m, door), cause, weather, Mem, OverworldSession.Day));
    }

    // ── S233：居民笔记本（N 键；Bombers' Notebook：每户一行 + 解锁过的真心话原文，没解锁的只给提示）──────────
    public sealed class NotePage { public int door; public string head = ""; public readonly List<string> rows = new List<string>(); }
    /// <summary>"还差什么"的提示：把 needs 里的数字条件翻成人话（只说条件，不剧透原文）。</summary>
    public static string NeedHint(Line l)
    {
        var parts = new List<string>();
        foreach (var n in l.needs)
        {
            if (n.StartsWith("trait=")) continue;
            if (n.StartsWith("visits>=")) parts.Add("来往 " + n.Substring(8) + " 次");
            else if (n.StartsWith("defended>=")) parts.Add("守住 " + n.Substring(10) + " 次");
            else if (n.StartsWith("looted>=")) parts.Add("被偷 " + n.Substring(8) + " 次");
            else if (n.StartsWith("witnessed>=")) parts.Add("在他家门口闹出 " + n.Substring(11) + " 次大动静");
            else if (n.StartsWith("day>=")) parts.Add("第 " + n.Substring(5) + " 天以后");
            else if (n == "outcome=won") parts.Add("那天你赢了");
            else if (n == "outcome=lost") parts.Add("那天你输了");
            else if (n.StartsWith("weather=")) parts.Add(n.Substring(8) == "rain" ? "下雨天" : n.Substring(8) == "fog" ? "大雾天" : n.Substring(8) == "storm" ? "雷雨天" : n.Substring(8) == "wind" ? "大风天" : "特别的天气");
            else parts.Add("某个特别的日子");
        }
        return parts.Count == 0 ? "多来几次" : string.Join("、", parts);
    }
    public static List<NotePage> Notebook(OverworldMap.Map m, IList<Line> t, Memory mem)
    {
        var pages = new List<NotePage>();
        foreach (var d in m.doors.OrderBy(d => d.n))
        {
            if (OverworldMap.Find(m, (char)('0' + d.n)).Count != 1) continue;
            var r = ResidentOf(m, d.n); int n = d.n;
            var p = new NotePage { door = n, head = $"门{n} {TraitZh(r.trait)}·{r.name}　来往 {mem.Get(mem.visits, n)}　守住 {mem.Get(mem.defended, n)}　被偷 {mem.Get(mem.looted, n)}　没赶上 {mem.Get(mem.missed, n)}　门口大动静 {mem.Get(mem.witnessed, n)}" };
            foreach (var l in t.Where(x => x.Sincere && x.who == "door" && x.needs.Contains("trait=" + r.trait)))
                p.rows.Add(mem.said.Contains(l.id) ? $"♥ {Fill(l.zh, r, null)}" + (mem.unlockDay.TryGetValue(l.id, out int ud) ? $"（第 {ud} 天）" : "") : $"？ 还没听过——{NeedHint(l)}");
            pages.Add(p);
        }
        var town = new NotePage { door = 0, head = "镇上 / 马里奥" };
        foreach (var l in t.Where(x => x.Sincere && x.who != "door"))
            town.rows.Add(mem.said.Contains(l.id) ? $"♥ {(l.who == "mario" ? "马里奥" : "镇上")}：{Fill(l.zh, null, null)}" : $"？ 还没听过——{NeedHint(l)}");
        pages.Add(town);
        return pages;
    }
    public static string NotebookText(OverworldMap.Map m, IList<Line> t, Memory mem)
    {
        var sb = new System.Text.StringBuilder(); var pr = SincereProgress(t, mem);
        sb.Append($"居民笔记本  真心话 {pr.got}/{pr.total} 段　累计 {mem.totalDays} 天\n");
        foreach (var p in Notebook(m, t, mem)) { sb.Append('\n').Append(p.head).Append('\n'); foreach (var r in p.rows) sb.Append("  ").Append(r).Append('\n'); }
        return sb.ToString();
    }

    // ── S233：存档（跨次进 Play 保留"来往了几次、听过哪些真心话"；冷却按天数，读档时清空）──────────
    public static string MemToJson(Memory mem)
    {
        string D(Dictionary<int, int> d) => "{" + string.Join(",", d.OrderBy(k => k.Key).Select(k => $"\"{k.Key}\":{k.Value}")) + "}";
        return "{\"v\":1,\"totalDays\":" + mem.totalDays + ",\"comic\":" + mem.comicSinceSincere + ",\"visits\":" + D(mem.visits) + ",\"defended\":" + D(mem.defended) + ",\"looted\":" + D(mem.looted) +
               ",\"missed\":" + D(mem.missed) + ",\"witnessed\":" + D(mem.witnessed) + ",\"said\":[" + string.Join(",", mem.said.OrderBy(x => x, StringComparer.Ordinal).Select(J)) + "],\"unlock\":{" +
               string.Join(",", mem.unlockDay.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => J(k.Key) + ":" + k.Value)) + "}}";
    }
    public static Memory MemFromJson(string json)
    {
        var mem = new Memory();
        if (!(MiniJson.Parse(json ?? "", out _) is Dictionary<string, object> d)) return mem;
        void Ints(string k, Dictionary<int, int> to) { if (d.TryGetValue(k, out var o) && o is Dictionary<string, object> m) foreach (var kv in m) if (int.TryParse(kv.Key, out int n) && kv.Value is double v) to[n] = (int)v; }
        Ints("visits", mem.visits); Ints("defended", mem.defended); Ints("looted", mem.looted); Ints("missed", mem.missed); Ints("witnessed", mem.witnessed);
        if (d.TryGetValue("said", out var so) && so is List<object> sl) foreach (var x in sl.OfType<string>()) mem.said.Add(x);
        if (d.TryGetValue("unlock", out var uo) && uo is Dictionary<string, object> um) foreach (var kv in um) if (kv.Value is double v) mem.unlockDay[kv.Key] = (int)v;
        if (d.TryGetValue("totalDays", out var td) && td is double tdv) mem.totalDays = (int)tdv;
        if (d.TryGetValue("comic", out var co) && co is double cv) mem.comicSinceSincere = (int)cv;
        return mem;
    }

    /// <summary>收集进度：解锁过几段真心话 / 一共几段（结算显示，给"再来一天看看还有什么"一个理由）。</summary>
    public static (int got, int total) SincereProgress(IList<Line> t, Memory mem) => (t.Count(l => l.Sincere && mem.said.Contains(l.id)), t.Count(l => l.Sincere));
    public static string ProgressLine(IList<Line> t, Memory mem) { var p = SincereProgress(t, mem); return p.total == 0 ? "" : $"\n镇上的真心话 {p.got}/{p.total} 段（常来往的人家才会说）"; }

    // ── 彩排（编辑器 / 网页 / sim 同一套）：不用进游戏，模拟 N 天会听到哪些话 ──────────
    public sealed class Rehearsal
    {
        public readonly List<string> lines = new List<string>();
        public int said, sincere, repeats3, distinct; public readonly Dictionary<string, int> firstSincereDay = new Dictionary<string, int>();
        public Memory mem; // S233：彩排结束时的记忆（笔记本预览用）
    }
    /// <summary>每扇门每天的结果按哈希定（守 40% / 被偷 40% / 没赶上 20%），不走玩法、不用 OverworldSession。网页 tsRehearse 逐字一致。</summary>
    public static OverworldSession.DoorResult RehearseResult(string map, int day, int door)
    {
        uint h = Hash("rehearse|" + map + "|" + day + "|" + door) % 5;
        return h < 2 ? OverworldSession.DoorResult.Defended : h < 4 ? OverworldSession.DoorResult.Looted : OverworldSession.DoorResult.Missed;
    }
    public static Rehearsal Rehearse(OverworldMap.Map m, IList<Line> t, int days)
    {
        var rep = new Rehearsal(); var mem = new Memory(); var heard = new Dictionary<string, int>();
        var doors = m.doors.OrderBy2().Where(d => OverworldMap.Find(m, (char)('0' + d.n)).Count == 1).ToList();
        void Hear(Line l, int day, string where)
        {
            if (l == null) { rep.lines.Add($"第{day}天 {where} —"); return; }
            Say(l, mem, day); rep.said++;
            if (l.Sincere) rep.sincere++;
            if (heard.TryGetValue(l.id, out int last) && day - last < 3) rep.repeats3++;
            heard[l.id] = day;
            rep.lines.Add($"第{day}天 {where} {(l.Sincere ? "♥" : "·")}{l.id}");
        }
        for (int day = 1; day <= days; day++)
        {
            var w = OverworldEvents.Of(m, day);
            var mf = new Dictionary<string, string> { { "yesterday", mem.yDay > 0 ? mem.yOutcome : "none" }, { "ydeath", "none" }, { "ybighits", "0" }, { "ydefended", mem.yDefended.ToString() }, { "ylooted", mem.yLooted.ToString() }, { "weather", w.kind.ToString().ToLowerInvariant() }, { "day", day.ToString() } };
            Hear(Pick(t, When.Morning, "town", mf, mem, day), day, "早上");
            int def = 0, loot = 0;
            foreach (var d in doors)
            {
                var res = RehearseResult(m.name, day, d.n); var r = ResidentOf(m, d.n);
                mem.Add(mem.visits, d.n);
                if (res == OverworldSession.DoorResult.Defended) { mem.Add(mem.defended, d.n); def++; } else if (res == OverworldSession.DoorResult.Looted) { mem.Add(mem.looted, d.n); loot++; } else mem.Add(mem.missed, d.n);
                var f = DoorFacts(r, res, w, mem); f["day"] = day.ToString(); f["bighit"] = "no"; f["bells"] = "0";
                var l = Pick(t, When.Back, "door", f, mem, day);
                if (l != null && l.Sincere && !rep.firstSincereDay.ContainsKey(r.trait)) rep.firstSincereDay[r.trait] = day;
                Hear(l, day, "门" + d.n + TraitZh(r.trait));
            }
            string outcome = doors.Count > 0 && def * 2 >= doors.Count ? "won" : "lost";
            var ef = new Dictionary<string, string> { { "outcome", outcome }, { "death", "none" }, { "byyou", "no" }, { "cause", "" }, { "bighits", "0" }, { "chain", "0" }, { "caught", "0" }, { "defendedtoday", def.ToString() }, { "lootedtoday", loot.ToString() }, { "weather", w.kind.ToString().ToLowerInvariant() }, { "day", day.ToString() }, { "bells", "0" } };
            Hear(Pick(t, When.DayEnd, "mario", ef, mem, day), day, "结束");
            mem.yDay = day; mem.yOutcome = outcome; mem.yDefended = def; mem.yLooted = loot; mem.totalDays = day;
        }
        rep.distinct = heard.Count; rep.mem = mem;
        return rep;
    }
    public static string RehearsalSummary(Rehearsal r, int days) =>
        $"彩排 {days} 天：说了 {r.said} 句（不同的 {r.distinct} 句），真心话 {r.sincere} 句；3 天内重复 {r.repeats3} 次" +
        (r.firstSincereDay.Count > 0 ? "；第一次真心话：" + string.Join("、", r.firstSincereDay.OrderBy(k => k.Value).Select(k => $"{TraitZh(k.Key)} 第{k.Value}天")) : "；还没有人说真心话");

    // ── 覆盖率（编辑器 / 网页 / 体检同一套话）────────────────────
    /// <summary>每种性格有几句"回到小镇"的话（好笑 / 真情）；不够 MinBackLinesPerTrait → 提醒会重复。网页 tsCoverage 逐字一致。</summary>
    public static List<string> Coverage(IList<Line> table)
    {
        var l = new List<string>();
        foreach (var t in Traits)
        {
            var mine = table.Where(x => x.who == "door" && x.when == "back" && (x.needs.Length == 0 || x.needs.Contains("trait=" + t) || !x.needs.Any(n => n.StartsWith("trait=")))).ToList();
            int own = mine.Count(x => x.needs.Contains("trait=" + t)), sincere = mine.Count(x => x.Sincere);
            l.Add($"{TraitZh(t)}：回小镇 {mine.Count} 句（专属 {own}、真情 {sincere}）" + (own < MinBackLinesPerTrait ? $" ⚠ 专属少于 {MinBackLinesPerTrait} 句，玩几天就会听到重复" : ""));
        }
        l.Add($"当场喊：{table.Count(x => x.when == "witness")} 句（大机关在他家门口砸中马里奥时）");
        foreach (var w in new[] { "morning", "dayend" })
        {
            int c = table.Count(x => x.when == w);
            l.Add($"{(w == "morning" ? "早上闲话" : "一天结束")}：{c} 句");
        }
        return l;
    }

    // ── S233：写台词时的检查（Unity 体检 / 小镇工坊 / 网页"台词本"同一套话）──────────
    /// <summary>每个时机能用哪些条件（写错名字 = 这句永远不会说）。</summary>
    public static readonly Dictionary<string, string[]> FactKeys = new Dictionary<string, string[]>
    {
        { "back", new[] { "door", "trait", "result", "weather", "visits", "defended", "looted", "missed", "witnessed", "day", "n", "bighit", "bells" } },
        { "witness", new[] { "door", "trait", "cause", "weather", "witnessed", "visits", "day" } },
        { "morning", new[] { "yesterday", "ydeath", "ybighits", "ydefended", "ylooted", "weather", "day" } },
        { "dayend", new[] { "outcome", "death", "byyou", "cause", "bighits", "chain", "caught", "defendedtoday", "lootedtoday", "weather", "day", "bells" } },
    };
    public static string NeedKey(string need) { foreach (var op in new[] { ">=", "<=", "!=", "=" }) { int i = need.IndexOf(op, StringComparison.Ordinal); if (i > 0) return need.Substring(0, i).Trim(); } return ""; }
    public static List<string> Validate(IList<Line> t)
    {
        var o = new List<string>(); var ids = new HashSet<string>();
        foreach (var l in t)
        {
            if (!ids.Add(l.id)) o.Add($"✗ {l.id}：id 重复");
            if (!FactKeys.TryGetValue(l.when, out var keys)) { o.Add($"✗ {l.id}：时机 {l.when} 不认识（morning / back / dayend / witness）"); continue; }
            string who = l.when == "morning" ? "town" : l.when == "dayend" ? "mario" : "door";
            if (l.who != who) o.Add($"✗ {l.id}：{l.when} 的话只能由 {who} 说（现在写的是 {l.who}）");
            foreach (var n in l.needs) { string k = NeedKey(n); if (k.Length == 0 || !keys.Contains(k)) o.Add($"✗ {l.id}：条件 {n} 用不了（{l.when} 能用：{string.Join(" ", keys)}）"); }
            if (l.Sincere && !l.once) o.Add($"⚠ {l.id}：真心话没勾\"只说一次\"（说两遍就不真心了）");
            if (l.Sincere && !l.needs.Any(n => n.Contains(">="))) o.Add($"⚠ {l.id}：真心话没有\"要挣\"的条件（例如 visits>=3）——第一天就会说");
            if (l.en.Length == 0) o.Add($"· {l.id}：还没写英文（可以先空着）");
            if (l.zh.Length > 70) o.Add($"· {l.id}：中文 {l.zh.Length} 字，屏幕上 5 秒读不完（建议 ≤ 70）");
        }
        return o;
    }

    // ── 数据文件 ─────────────────────────────────────────
    public const string ResourceName = "TownStories";
    public static IList<Line> table;
    /// <summary>运行时用的表（Unity 里由 OverworldGame 从 Resources 读进来；读不到 / sim 里 = Default）。</summary>
    public static IList<Line> Table => table ?? Default;

    public static Line[] Parse(string json, out string error)
    {
        error = "";
        var root = MiniJson.Parse(json, out string je) as Dictionary<string, object>;
        if (root == null || !root.TryGetValue("lines", out var lo) || !(lo is List<object> list)) { error = string.IsNullOrEmpty(je) ? "没有 lines 列表" : je; return Default.ToArray(); }
        var res = new List<Line>(); var ids = new HashSet<string>();
        foreach (var o in list)
        {
            if (!(o is Dictionary<string, object> d)) continue;
            var l = new Line { id = S(d, "id"), who = S(d, "who", "door"), when = S(d, "when", "back"), tone = S(d, "tone", "comic"), zh = S(d, "zh"), en = S(d, "en"), note = S(d, "note"),
                tier = (int)N(d, "tier", 2), coolDays = (int)N(d, "coolDays", 3), once = d.TryGetValue("once", out var b) && b is bool bb && bb };
            if (d.TryGetValue("needs", out var nn) && nn is List<object> nl) l.needs = nl.OfType<string>().ToArray();
            if (l.id.Length == 0 || l.zh.Length == 0) { error = "有一句没写 id 或 zh"; continue; }
            if (!ids.Add(l.id)) { error = "id 重复：" + l.id; continue; }
            if (l.when != "morning" && l.when != "back" && l.when != "dayend" && l.when != "witness") { error = $"{l.id}：when 只能是 morning / back / dayend / witness"; continue; }
            res.Add(l);
        }
        return res.ToArray();
    }
    private static string S(Dictionary<string, object> d, string k, string def = "") => d.TryGetValue(k, out var v) && v is string s ? s : def;
    private static double N(Dictionary<string, object> d, string k, double def) => d.TryGetValue(k, out var v) && v is double x ? x : def;

    public static string ToJson(IList<Line> t)
    {
        var sb = new System.Text.StringBuilder("{\n  \"version\": 1,\n  \"lines\": [\n");
        for (int i = 0; i < t.Count; i++)
        {
            var l = t[i];
            sb.Append("    {\"id\": ").Append(J(l.id)).Append(", \"who\": ").Append(J(l.who)).Append(", \"when\": ").Append(J(l.when)).Append(", \"tier\": ").Append(l.tier)
              .Append(", \"tone\": ").Append(J(l.tone)).Append(", \"coolDays\": ").Append(l.coolDays).Append(l.once ? ", \"once\": true" : "")
              .Append(", \"needs\": [").Append(string.Join(", ", l.needs.Select(J))).Append("], \"zh\": ").Append(J(l.zh)).Append(", \"en\": ").Append(J(l.en))
              .Append(l.note.Length > 0 ? ", \"note\": " + J(l.note) : "").Append('}').Append(i < t.Count - 1 ? ",\n" : "\n");
        }
        return sb.Append("  ]\n}\n").ToString();
    }
    private static string J(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";

    private static Line L(string id, string who, string when, int tier, string tone, string needs, string zh, string en, int cool = 3, bool once = false, string note = "") =>
        new Line { id = id, who = who, when = when, tier = tier, tone = tone, needs = needs.Length == 0 ? new string[0] : needs.Split(';'), zh = zh, en = en, coolDays = cool, once = once, note = note };

    /// <summary>默认表（= Resources/TownStories.json；sim 逐句对照）。写法：好笑的多、真情的少；真情一句 = 小伤口 + 一个玩笑。</summary>
    public static readonly Line[] Default =
    {
        // ── 回到小镇：通用（任何住户；兜底，保证永远有话说）──
        L("any_def_1", "door", "back", 2, "comic", "result=defended", "他在我家门口转了三圈，最后对着门鞠了个躬走了。你干的吧？", "He bowed to my door and left. Your doing?"),
        L("any_def_2", "door", "back", 2, "comic", "result=defended", "宝贝还在！我决定把它藏进……算了，不告诉你。", "Treasure's safe! I'll hide it in... no, not telling you."),
        L("any_loot_1", "door", "back", 2, "comic", "result=looted", "他拿走了我的宝贝，还顺手把我的拖鞋摆整齐了。可恶，挺有礼貌。", "He took it — and tidied my slippers. Rude. Polite, but rude."),
        L("any_loot_2", "door", "back", 2, "comic", "result=looted", "没关系没关系，明天他还会来，你还有机会。", "It's fine. He'll be back tomorrow. So will you."),
        L("any_miss_1", "door", "back", 2, "comic", "result=missed", "我等你等到茶都凉了。他倒是准时。", "My tea went cold waiting for you. He was on time."),
        L("any_miss_2", "door", "back", 2, "comic", "result=missed", "他进门的时候还回头找了你一下。好像有点失落？", "He looked back for you on the way in. A bit disappointed?"),
        L("any_miss_3", "door", "back", 2, "comic", "result=missed", "没赶上也没关系，我家门口的花替你看了全程。", "Missed it? My flowers watched the whole thing for you."),
        L("any_def_3", "door", "back", 2, "comic", "result=defended", "我听见外面'咚'的一声，然后是一句'妈妈咪呀'。干得好。", "I heard a thud, then 'mamma mia'. Good job."),
        L("any_loot_3", "door", "back", 2, "comic", "result=looted", "他走的时候哼着歌。今天的歌有点难听。", "He hummed on his way out. Bad song today."),
        // ── 面包师 ──
        L("baker_def_1", "door", "back", 1, "comic", "trait=baker;result=defended", "他被挡在门外，闻着烤箱的味道流口水。今天的面包算你一半功劳。", "He drooled outside my oven. Half of today's bread is yours."),
        L("baker_def_2", "door", "back", 1, "comic", "trait=baker;result=defended;weather=rain", "下雨天他滑得像块黄油。我笑到把面粉撒了一地。", "In the rain he slid like butter. I spilled flour laughing."),
        L("baker_loot_1", "door", "back", 1, "comic", "trait=baker;result=looted", "他偷走的不是宝贝，是我刚出炉的那根法棍！……好吧，两样都偷了。", "He didn't just take the treasure. He took my baguette too."),
        L("baker_loot_2", "door", "back", 1, "comic", "trait=baker;result=looted;looted>=2", "第 {looted} 次了。我开始考虑在门口放一盘饼干当买路钱。", "Time {looted}. I'm thinking of leaving cookies as a toll."),
        L("baker_miss_1", "door", "back", 1, "comic", "trait=baker;result=missed", "你没来，他也没吃我留的早饭。两个人都让我操心。", "You didn't come. He skipped my breakfast. You both worry me."),
        L("baker_miss_2", "door", "back", 1, "comic", "trait=baker;result=missed", "我给他留了个面包。他吃完才想起来要偷东西，结果来晚了。", "I left him a bun. He remembered to steal only after eating it."),
        L("baker_arc_1", "door", "back", 1, "sincere", "trait=baker;defended>=2;visits>=3", "……那个宝贝，是我女儿走之前烤坏的第一块饼干。硬得能砸核桃。谢谢你守着它。——别告诉别人我哭了。", "...It's the first cookie my daughter burnt before she left. Hard as rock. Thank you. Don't tell anyone I cried.", 7, true),
        L("baker_arc_2", "door", "back", 1, "sincere", "trait=baker;visits>=6", "她今天来信了，说城里的面包没我烤的好吃。我想多烤一炉——给你和那个冒失鬼各一个。", "She wrote today. Says city bread isn't as good. I'll bake extra — one for you, one for that clumsy guy.", 7, true),
        // ── 老奶奶 ──
        L("granny_def_1", "door", "back", 1, "comic", "trait=granny;result=defended", "年轻人，你那个香蕉皮的角度，我年轻时候也是这么放的。", "Young one, I used to place banana peels at that exact angle."),
        L("granny_def_2", "door", "back", 1, "comic", "trait=granny;result=defended;defended>=2", "我在窗边看了一整场，比电视剧好看。明天几点开演？", "Watched the whole thing from my window. Better than TV. Same time tomorrow?"),
        L("granny_loot_1", "door", "back", 1, "comic", "trait=granny;result=looted", "他拿走的时候说了声'谢谢奶奶'。我一时不知道该骂他还是给他糖。", "He said 'thanks, Granny' as he took it. Scold him or give him candy?"),
        L("granny_loot_2", "door", "back", 1, "comic", "trait=granny;result=looted;weather=fog", "雾这么大，他怎么还找得到我家？你倒是迷路了吧。", "In this fog he still found my house. You got lost, didn't you?"),
        L("granny_miss_1", "door", "back", 1, "comic", "trait=granny;result=missed", "我把拐杖横在门口替你守了一会儿。他跨过去了。", "I laid my cane across the door for you. He stepped over it."),
        L("granny_miss_2", "door", "back", 1, "comic", "trait=granny;result=missed", "没人来，我就跟猫说了一下午你们的事。猫睡着了。", "Nobody came, so I told my cat all about you two. It fell asleep."),
        L("granny_arc_1", "door", "back", 1, "sincere", "trait=granny;visits>=3", "我老伴以前也爱捣蛋，把邮差的帽子挂到钟楼上。……好久没人在我门口这么热闹了。下次带上那个红帽子小伙子，我煮汤。", "My late husband loved pranks too — hung the mailman's hat on the bell tower. It's been ages since my door was this lively. I'll make soup.", 7, true),
        L("granny_arc_2", "door", "back", 1, "sincere", "trait=granny;defended>=3", "宝贝是他送我的戒指盒，里面早就空了。可我还是想有人替我守着。谢谢。——你鞋带开了。", "The treasure's an empty ring box he gave me. Still, I wanted someone to guard it. Thank you. Your shoelace is untied.", 7, true),
        // ── 小孩 ──
        L("kid_def_1", "door", "back", 1, "comic", "trait=kid;result=defended", "哇！你能教我吗？我想把我哥也弹到天上去！", "Whoa! Teach me? I wanna launch my brother too!"),
        L("kid_def_2", "door", "back", 1, "comic", "trait=kid;result=defended;bighit=yes", "那个大石头滚过去的时候我录下来了！……没录上，我太激动了。", "I filmed the boulder! ...I didn't. Too excited."),
        L("kid_loot_1", "door", "back", 1, "comic", "trait=kid;result=looted", "他把我的弹珠拿走了！……但是他教了我一个后空翻。扯平？", "He took my marbles! ...but he taught me a backflip. Even?"),
        L("kid_loot_2", "door", "back", 1, "comic", "trait=kid;result=looted;looted>=2", "我决定了，长大当马里奥。他每天都赢。", "Decided. I'm gonna be Mario when I grow up. He always wins."),
        L("kid_miss_1", "door", "back", 1, "comic", "trait=kid;result=missed", "你迟到了！我已经自己布置了三个陷阱，全踩中我自己了。", "You're late! I set three traps myself. Stepped in all of them."),
        L("kid_miss_2", "door", "back", 1, "comic", "trait=kid;result=missed", "你不来，我只好自己当捣蛋鬼。我把自己关在门外了。", "You didn't come, so I played prankster. Locked myself out."),
        L("kid_arc_1", "door", "back", 1, "sincere", "trait=kid;visits>=3", "学校里没人跟我玩。但是你和那个马里奥每天都来我家门口打架。……这算朋友吗？算吧。", "Nobody plays with me at school. But you two fight at my door every day... that counts as friends, right? Right.", 7, true),
        L("kid_arc_2", "door", "back", 1, "sincere", "trait=kid;defended>=2;visits>=5", "我今天在学校讲了你们的事，大家都围过来听。我第一次被这么多人看着。谢谢你。——明天我能当陷阱吗？", "I told your story at school. Everyone listened. First time so many people looked at me. Thanks. Can I be a trap tomorrow?", 7, true),
        // ── 镇长 ──
        L("mayor_def_1", "door", "back", 1, "comic", "trait=mayor;result=defended", "镇公所正式宣布：今天的治安由一位匿名的捣蛋鬼负责。别声张。", "Town Hall officially announces: today's security was provided by an anonymous prankster."),
        L("mayor_def_2", "door", "back", 1, "comic", "trait=mayor;result=defended;bells>=1", "钟楼又响了。我已经第五次在会议上假装那是我安排的。", "The bell rang again. Fifth time I've pretended I scheduled it."),
        L("mayor_loot_1", "door", "back", 1, "comic", "trait=mayor;result=looted", "他偷走了镇长印章。今天镇上所有文件都盖着蘑菇图案。", "He stole the town seal. Every document today has a mushroom stamp."),
        L("mayor_loot_2", "door", "back", 1, "comic", "trait=mayor;result=looted;looted>=2", "我准备给他发个'年度最勤劳访客'奖。他应得的。", "Planning a 'Most Diligent Visitor' award for him. He's earned it."),
        L("mayor_miss_1", "door", "back", 1, "comic", "trait=mayor;result=missed", "你没来，我只好亲自堵门。我的腰……", "You didn't show, so I blocked the door myself. My back..."),
        L("mayor_miss_2", "door", "back", 1, "comic", "trait=mayor;result=missed", "我在会议记录里写了：'今日，捣蛋鬼缺席。'", "I wrote in the minutes: 'Today, the prankster was absent.'"),
        L("mayor_arc_1", "door", "back", 1, "sincere", "trait=mayor;visits>=3", "我当了二十年镇长，镇上从来没这么吵过。说实话——我喜欢。好像大家又醒过来了。——修路的钱你们出。", "Twenty years as mayor, and the town's never been this loud. Honestly? I like it. Like everyone woke up. You're paying for the roads.", 7, true),
        L("mayor_arc_2", "door", "back", 1, "sincere", "trait=mayor;defended>=3", "我想在广场立块牌子：'这里有人每天守着别人的宝贝'。不写名字。你会知道是你。", "I want a plaque in the square: 'Someone here guards other people's treasures every day.' No name. You'll know.", 7, true),
        // ── 画家 ──
        L("painter_def_1", "door", "back", 1, "comic", "trait=painter;result=defended", "他摔倒的姿势太完美了，我画下来了。题目叫《重力》。", "His fall was perfect. I painted it. Title: 'Gravity'."),
        L("painter_def_2", "door", "back", 1, "comic", "trait=painter;result=defended;weather=wind", "风把他吹得像一面旗。下一幅画就叫《不屈》。", "The wind made him flap like a flag. Next painting: 'Unbowed'."),
        L("painter_loot_1", "door", "back", 1, "comic", "trait=painter;result=looted", "他偷走了我的画！……他居然觉得它值钱。我有点感动。", "He stole my painting! ...He thinks it's worth something. I'm touched."),
        L("painter_loot_2", "door", "back", 1, "comic", "trait=painter;result=looted;looted>=2", "我开始画他偷东西的样子，拿去卖，赚得比被偷的还多。", "I paint him stealing now and sell those. I'm making a profit."),
        L("painter_miss_1", "door", "back", 1, "comic", "trait=painter;result=missed", "你没来，画面里少了一个人。构图都歪了。", "You didn't come. The composition's off without you."),
        L("painter_miss_2", "door", "back", 1, "comic", "trait=painter;result=missed", "空的门口也挺好看的。……不，还是你们俩在比较好看。", "An empty doorway is nice too. ...No, it's better with you two."),
        L("painter_arc_1", "door", "back", 1, "sincere", "trait=painter;visits>=3", "我已经三年画不出东西了。这几天我画了十二张，全是你们俩。……谢谢你们没让我闲着。——颜料钱算谁的？", "I hadn't painted in three years. This week: twelve, all of you two. Thanks for not letting me sit still. Who pays for the paint?", 7, true),
        L("painter_arc_2", "door", "back", 1, "sincere", "trait=painter;defended>=2;visits>=5", "我要办个小画展，题目叫《每天都在输的马里奥》。他看了会笑的吧？会吧。", "I'm holding a little show: 'Mario, Who Loses Every Day.' He'll laugh, right? Right.", 7, true),
        // ── 守夜人 ──
        L("guard_def_1", "door", "back", 1, "comic", "trait=guard;result=defended", "我值夜班的，白天的事本来不归我管。但这次……干得漂亮。", "I'm night shift. Daytime's not my job. But... nice work."),
        L("guard_def_2", "door", "back", 1, "comic", "trait=guard;result=defended;weather=storm", "雷劈下来那一下，我以为是我的退休通知。原来是你。", "When the lightning struck I thought it was my retirement notice. It was you."),
        L("guard_loot_1", "door", "back", 1, "comic", "trait=guard;result=looted", "我睡着了。别跟镇长说。", "I fell asleep. Don't tell the mayor."),
        L("guard_loot_2", "door", "back", 1, "comic", "trait=guard;result=looted;looted>=2", "我在门口挂了个'内有恶犬'的牌子。我没有狗。他也知道。", "I hung a 'Beware of Dog' sign. I don't have a dog. He knows."),
        L("guard_miss_1", "door", "back", 1, "comic", "trait=guard;result=missed", "你没赶上？我也经常没赶上。人生就是这样，伙计。", "Missed it? Me too, often. That's life, pal."),
        L("guard_miss_2", "door", "back", 1, "comic", "trait=guard;result=missed", "我替你站了一会儿岗。他冲我敬了个礼就进去了。", "I stood guard for you. He saluted me and went right in."),
        L("guard_arc_1", "door", "back", 1, "sincere", "trait=guard;visits>=3", "我守了一辈子门，从来没人谢过我。所以——谢谢你，替我守了一次。——现在我能去睡觉了吗？", "I've guarded doors all my life. Nobody ever thanked me. So — thank you for guarding mine. Can I sleep now?", 7, true),
        L("guard_arc_2", "door", "back", 1, "sincere", "trait=guard;defended>=3", "其实我年轻时就是那个偷东西的。后来有人在门口守着我，我就不偷了。……他会明白的。慢慢来。", "Truth is, I was the thief when I was young. Then someone guarded a door against me, and I stopped. He'll get it. Give it time.", 7, true),
        // ── 早上的闲话（镇上的人互相说，主角不在场：Night in the Woods 的背景对话）──
        L("m_any_1", "town", "morning", 2, "comic", "", "听说今天马里奥换了一双新鞋，跑得更响了。", "Word is Mario got new shoes. Louder now.", 3),
        L("m_any_2", "town", "morning", 2, "comic", "", "面包店今天的特价：防滑面包。不知道为什么卖得特别好。", "Bakery special: non-slip bread. Selling oddly well.", 3),
        L("m_any_3", "town", "morning", 2, "comic", "", "有人在钟楼上留了一张纸条：'别信钟声'。字很丑。", "Someone left a note on the bell tower: 'don't trust the bell.' Bad handwriting.", 3),
        L("m_won", "town", "morning", 0, "comic", "yesterday=won", "昨天全镇的宝贝都还在！镇长说要放半天假，然后自己去上班了。", "Every treasure survived yesterday! The mayor declared a half-holiday, then went to work."),
        L("m_lost", "town", "morning", 0, "comic", "yesterday=lost", "昨天马里奥满载而归。今天他走路有点飘，肯定是骄傲了。", "Mario cleaned up yesterday. He's strutting today. Pride comes before a banana peel."),
        L("m_draw", "town", "morning", 0, "comic", "yesterday=draw", "昨天你们俩同时倒下，全镇的人都在争到底算谁赢。", "You both went down at once yesterday. The whole town's still arguing who won."),
        L("m_mariodied", "town", "morning", 0, "comic", "ydeath=mariodied", "马里奥昨天被抬回家了。今早他已经在门口做热身操，还冲每户人家眨眼。", "Mario was carried home yesterday. This morning he's stretching outside, winking at every house."),
        L("m_youdied", "town", "morning", 0, "comic", "ydeath=youdied", "昨天那个捣蛋鬼被自己的机关放倒了。大家都假装没看见。……大部分人。", "The prankster got flattened by their own trap yesterday. Everyone pretends they didn't see. Mostly."),
        L("m_bighits", "town", "morning", 1, "comic", "ybighits>=3", "昨天镇上响了 {ybighits} 次大动静。王大爷说他的假牙都震松了。", "{ybighits} big crashes yesterday. Old Wang says his dentures came loose."),
        L("m_rain", "town", "morning", 1, "comic", "weather=rain", "下雨了。今天的香蕉皮会格外滑，请大家注意脚下——尤其是你，马里奥。", "Rain today. Banana peels will be extra slick. Watch your step. Especially you, Mario."),
        L("m_fog", "town", "morning", 1, "comic", "weather=fog", "大雾。今天谁跟谁撞上都别吵架，可能是捣蛋鬼。", "Thick fog. If you bump into someone, don't argue. Could be the prankster."),
        L("m_sincere", "town", "morning", 1, "sincere", "day>=4", "你发现没有？自从他们俩开始闹，镇上的人见面会打招呼了。……以前大家都只低头走路的。", "Notice? Since those two started, people say hello again. We used to walk with our heads down.", 30, true),
        // ── 一天结束：马里奥的一句（他也是这个镇的人）──
        L("e_won", "mario", "dayend", 2, "comic", "outcome=won", "今天……算你厉害。明天我起得更早！", "Today... you got me. I'm waking up earlier tomorrow!"),
        L("e_won_2", "mario", "dayend", 2, "comic", "outcome=won", "我的鞋里全是香蕉。你知道这有多难洗吗？", "My shoes are full of banana. Do you know how hard that is to wash?"),
        L("e_lost", "mario", "dayend", 2, "comic", "outcome=lost", "嘿嘿，全拿到了！明天你得更努力才行哦。", "Got 'em all! Try harder tomorrow."),
        L("e_lost_2", "mario", "dayend", 2, "comic", "outcome=lost", "你今天是不是故意放水？……不像。好吧，我就是太强了。", "Were you going easy on me? ...Nah. I'm just that good."),
        L("e_won_3", "mario", "dayend", 2, "comic", "outcome=won", "我决定了，明天我要倒着走。看你怎么办。", "Decided. Tomorrow I walk backwards. Let's see you handle that."),
        L("e_lost_3", "mario", "dayend", 2, "comic", "outcome=lost", "今天的宝贝我会好好收着的。……收在哪来着？", "I'll keep today's treasures safe. ...Where'd I put them?"),
        L("e_any_1", "mario", "dayend", 2, "comic", "", "回家洗澡。明天见。", "Going home for a bath. See you tomorrow.", 4),
        L("e_any_2", "mario", "dayend", 2, "comic", "", "今天走了好多路。我的胡子都累了。", "Walked so much today. Even my mustache is tired.", 4),
        L("e_died_you", "mario", "dayend", 0, "comic", "death=mariodied;byyou=yes", "我……我只是躺一会儿。不是输了。是在看云。", "I'm... just lying down. Not losing. Watching clouds."),
        L("e_died_storm", "mario", "dayend", 0, "comic", "death=mariodied;byyou=no", "被雷劈了。这不算你赢的吧？……算吗？", "Struck by lightning. That doesn't count as your win, right? ...Right?"),
        L("e_you_died", "mario", "dayend", 0, "comic", "death=youdied", "喂，你没事吧？下次别站在自己的炮口前面了。……我是说真的。", "Hey, you okay? Don't stand in front of your own cannon. ...I mean it."),
        L("e_draw", "mario", "dayend", 0, "comic", "death=both", "我们俩一起躺在这儿。说真的，今天天气不错。", "So we're both lying here. Honestly, nice weather today."),
        L("e_chain", "mario", "dayend", 1, "comic", "chain>=3", "今天那一串连锁……我要承认，挺好看的。", "That chain today... I'll admit, it was beautiful."),
        L("e_sincere_1", "mario", "dayend", 1, "sincere", "day>=3", "你知道吗，以前我每天都是一个人跑来跑去。现在至少有人在等我。……明天见，捣蛋鬼。", "You know, I used to run around alone every day. Now someone's waiting for me. See you tomorrow, prankster.", 30, true),
        L("e_sincere_2", "mario", "dayend", 1, "sincere", "day>=6;outcome=won", "我不是真的想要那些宝贝。我只是想让大家记得有我这个人。……你好像也一样？", "I don't really want the treasures. I just want people to remember I'm here. ...You too, maybe?", 30, true),

        // S233：当场喊（大机关在他家门口砸中马里奥）。一户一天最多一次；不兜底。真心话要"门口闹过 ≥3 次"才有。
        L("w_any_1", "door", "witness", 2, "comic", "", "我的窗户！……啊，没碎。那就——再来一次！", "My window! ...Oh, it's fine. Then — again!", 2),
        L("w_any_2", "door", "witness", 2, "comic", "", "刚才那一下我在屋里都感觉到了。谁家的锅掉了？哦，是马里奥。", "Felt that from inside. Whose pot fell? Oh. Mario.", 2),
        L("w_any_storm", "door", "witness", 1, "comic", "cause=i", "老天爷都站你这边了。我去给老天爷倒杯茶。", "Even the sky's on your side. I'll make the sky some tea.", 2),
        L("w_baker", "door", "witness", 1, "comic", "trait=baker", "你砸他的时候小心点！我的面团正在发——好吧，已经塌了。", "Careful! My dough was rising — okay, it collapsed.", 2),
        L("w_granny", "door", "witness", 1, "comic", "trait=granny", "哎哟哎哟——再砸一下，奶奶刚才没看清。", "Oh my — do it again, Granny didn't see properly.", 2),
        L("w_kid", "door", "witness", 1, "comic", "trait=kid", "哇啊啊啊！！！我要告诉全班！！！", "WHOAAA!!! I'm telling the whole class!!!", 2),
        L("w_mayor", "door", "witness", 1, "comic", "trait=mayor", "这条路是上个月刚修的……算了，记在马里奥账上。", "This road was just repaved... Fine. Bill it to Mario.", 2),
        L("w_painter", "door", "witness", 1, "comic", "trait=painter", "别动！保持这个姿势！……他晕着呢，正好。", "Don't move! Hold that pose! ...He's dazed. Perfect.", 2),
        L("w_guard", "door", "witness", 1, "comic", "trait=guard", "我什么都没看见。我在打盹。（其实看见了，挺爽的。）", "Saw nothing. Was napping. (I saw. It was great.)", 2),
        L("w_kid_arc", "door", "witness", 0, "sincere", "trait=kid;witnessed>=3", "我以前以为大人都很无聊。你们俩让我想快点长大——然后每天在别人家门口闹。", "I thought grown-ups were boring. You two make me want to grow up fast — and cause trouble at doors.", 3, true),
        L("w_granny_arc", "door", "witness", 0, "sincere", "trait=granny;witnessed>=3", "他倒下的时候，我差点想去扶。……我老伴摔倒那次，也是这么不服气地爬起来的。", "When he fell I nearly ran to help. ...My husband got up just as stubbornly, once.", 3, true),
        L("w_guard_arc", "door", "witness", 0, "sincere", "trait=guard;witnessed>=3", "你知道吗，被砸了还能笑着爬起来，是本事。他有，你也有。别把对方弄丢了。", "Getting knocked down and getting up laughing — that's a gift. He has it. So do you. Don't lose each other.", 3, true),
    };
}
