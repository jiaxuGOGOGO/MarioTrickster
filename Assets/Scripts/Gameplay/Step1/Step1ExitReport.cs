using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// S227（总方案阶段 D 的"填表"自动化）：读 PlaytestLogs/step1_rounds*.csv，算出离宪法第 1 步退出条件还差什么，
/// 并按各阶段写好的"证伪规则"给出下一步该改什么。纯逻辑、无 Unity 依赖（沙盒用用户真实的 CSV 测）。
/// - 列只在末尾追加过 → 一律按 Columns 的位置读（旧文件表头短、行长也能读对）。
/// - 比例全部带样本数 + Wilson 95% 区间（总方案永久规则 4）。
/// - 只读记录，不改任何玩法数值；建议只指向已有的调参/规则，不发明新系统。
/// </summary>
public static class Step1ExitReport
{
    /// <summary>step1_rounds.csv 的全部列（按历史追加顺序）。和 Step1PlaytestLog.CsvHeader 必须一致（sim 检查）。</summary>
    public static readonly string[] Columns =
    {
        "timestamp", "round", "winner", "reason", "seconds", "trickster_lives_left", "times_caught", "omens", "alerts", "pranks", "distinct_kinds",
        "calculated_moment", "near_miss_moment", "caught_verdict", "want_again_1to5", "note", "max_combo", "layout_seed", "stuck_rescues", "combo_score",
        "personality", "laughed", "tuning_version", "mode"
    };

    public const int ExitRounds = 20;
    public const int ExitKinds = 3;
    public const float ExitWantAgainLast5 = 3f;

    public sealed class Round
    {
        public DateTime time; public string winner = "", reason = "", pranksRaw = "", verdict = "", note = "", personality = "", mode = "";
        public float seconds; public int caught, wantAgain, stuck, version;
        public bool? calculated, nearMiss, laughed;
        public List<string> kinds = new List<string>();
        public bool TimedOut => reason.IndexOf("Time ran out", StringComparison.OrdinalIgnoreCase) >= 0 || reason.Contains("超时");
        public bool MarioWon => winner.Equals("Mario", StringComparison.OrdinalIgnoreCase);
    }

    public static List<Round> Parse(string csvText)
    {
        var list = new List<Round>();
        if (string.IsNullOrEmpty(csvText)) return list;
        foreach (var raw in csvText.Replace("\r", "").Split('\n'))
        {
            if (raw.Length == 0 || raw.StartsWith("timestamp")) continue;
            var f = raw.Split(',');
            string Get(string name) { int i = Array.IndexOf(Columns, name); return i >= 0 && i < f.Length ? f[i].Trim() : ""; }
            if (!DateTime.TryParse(Get("timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) continue;
            var r = new Round
            {
                time = t, winner = Get("winner"), reason = Get("reason"), pranksRaw = Get("pranks"), verdict = Get("caught_verdict"),
                note = Get("note"), personality = Get("personality"), mode = Get("mode"),
                seconds = F(Get("seconds")), caught = I(Get("times_caught")), wantAgain = I(Get("want_again_1to5")), stuck = I(Get("stuck_rescues")),
                version = I(Get("tuning_version")), calculated = YN(Get("calculated_moment")), nearMiss = YN(Get("near_miss_moment")), laughed = YN(Get("laughed")),
            };
            foreach (var p in r.pranksRaw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) { var k = p.Split(':')[0]; if (k.Length > 0) r.kinds.Add(k); }
            list.Add(r);
        }
        return list.OrderBy(r => r.time).ToList();
    }

    /// <summary>多个文件（当前 + 改名留档的旧文件）合起来，去掉重复行。</summary>
    public static List<Round> ParseAll(IEnumerable<string> csvTexts)
    {
        var seen = new HashSet<string>(); var all = new List<Round>();
        foreach (var txt in csvTexts) foreach (var r in Parse(txt)) if (seen.Add(r.time.ToString("s") + "|" + r.seconds.ToString(CultureInfo.InvariantCulture))) all.Add(r);
        return all.OrderBy(r => r.time).ToList();
    }

    static float F(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
    static int I(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
    static bool? YN(string s) => s == "yes" ? true : s == "no" ? (bool?)false : null;

    /// <summary>Wilson 95% 区间。n = 0 → (0, 1)。</summary>
    public static (double lo, double hi) Wilson(int k, int n)
    {
        if (n <= 0) return (0, 1);
        const double z = 1.96; double p = (double)k / n, d = 1 + z * z / n, c = p + z * z / (2 * n), m = z * Math.Sqrt(p * (1 - p) / n + z * z / (4.0 * n * n));
        return (Math.Max(0, (c - m) / d), Math.Min(1, (c + m) / d));
    }

    public static string Rate(int k, int n) { if (n == 0) return "—（还没有）"; var w = Wilson(k, n); return $"{k}/{n}（{(double)k / n:P0}，95% 区间 {w.lo:P0}–{w.hi:P0}）"; }

    public sealed class Result
    {
        public int rounds, sessionRounds, kinds; public float wantLast5, wantMedianLast5; public bool exitMet;
        /// <summary>S231：最新两个调参版本「还想再来」的比较（不够样本 = 空）。</summary>
        public string versionCompare = "";
        public List<string> lines = new List<string>();
        /// <summary>按已有证伪规则得出的下一步（第一条最重要）。</summary>
        public List<string> next = new List<string>();
        /// <summary>不对劲的局（卡住救援 / 超时）——属于 bug，不是平衡。</summary>
        public List<string> bugs = new List<string>();
    }

    /// <summary>
    /// 退出条件：连玩 20 局（取最近一次"连续玩"——相邻两局间隔 ≤ gapMinutes）+ 最后 5 局想再来平均 ≥3 + ≥3 种坑法。
    /// minVersion &gt; 0 时只算这个调参版本及以后的局（旧版本的局另列，不混进结论）。
    /// </summary>
    public static Result Analyze(IList<Round> all, int minVersion = 0, float gapMinutes = 30f)
    {
        var res = new Result();
        var rs = all.Where(r => minVersion <= 0 || r.version >= minVersion).ToList();
        int old = all.Count - rs.Count;
        res.rounds = rs.Count;
        if (old > 0) res.lines.Add($"另有 {old} 局是旧版本（调参 v{minVersion} 以前）玩的，下面不算进去。");
        if (rs.Count == 0) { res.lines.Add("还没有记录：在恶作剧房间玩一局、答完结算的几个问题，就会自动记下来。"); res.next.Add("先玩 5 局（快速测试模式要关掉，否则不记问卷）。"); return res; }

        // 最近一次连续玩
        var session = new List<Round> { rs[rs.Count - 1] };
        for (int i = rs.Count - 2; i >= 0; i--) { if ((session[0].time - rs[i].time).TotalMinutes > gapMinutes) break; session.Insert(0, rs[i]); }
        res.sessionRounds = session.Count;
        var kinds = new HashSet<string>(rs.SelectMany(r => r.kinds)); res.kinds = kinds.Count;
        var last5 = rs.Skip(Math.Max(0, rs.Count - 5)).Where(r => r.wantAgain > 0).ToList();
        res.wantLast5 = last5.Count == 0 ? 0 : (float)last5.Average(r => r.wantAgain);
        res.exitMet = session.Count >= ExitRounds && res.wantLast5 >= ExitWantAgainLast5 && kinds.Count >= ExitKinds;

        var last5All = rs.Skip(Math.Max(0, rs.Count - 5)).Where(r => r.wantAgain > 0).Select(r => r.wantAgain).ToList();
        if (last5All.Count > 0) res.wantMedianLast5 = Median(last5All);
        res.lines.Add($"**出口进度**：最近一次连着玩了 {session.Count}/{ExitRounds} 局｜坑法 {kinds.Count}/{ExitKinds} 种（{(kinds.Count == 0 ? "还没有" : string.Join("、", kinds.OrderBy(k => k)))}）｜最后 5 局「还想再来」平均 {res.wantLast5:0.0}（要 ≥{ExitWantAgainLast5:0}；中位数 {res.wantMedianLast5:0.#}）→ " + (res.exitMet ? "**✓ 达到第 1 步出口**" : "还没到"));
        int town = rs.Count(r => r.mode == "town"), quick = rs.Count(r => r.mode == "quick");
        if (town + quick > 0) res.lines.Add($"其中小镇里的房间 {town} 局、快速测试 {quick} 局（这些不弹问卷：局数和坑法照算，「算准了 / 想再来」只看答过问卷的局）。");
        int n = rs.Count, mario = rs.Count(r => r.MarioWon);
        var secs = rs.Select(r => r.seconds).OrderBy(x => x).ToList(); float med = secs[secs.Count / 2];
        res.lines.Add($"马里奥赢 {Rate(mario, n)}｜每局时长中位数 {med:0} 秒");
        var calc = rs.Where(r => r.calculated.HasValue).ToList(); var near = rs.Where(r => r.nearMiss.HasValue).ToList(); var laugh = rs.Where(r => r.laughed.HasValue).ToList();
        res.lines.Add($"「算准了」{Rate(calc.Count(r => r.calculated == true), calc.Count)}｜「差点被发现」{Rate(near.Count(r => r.nearMiss == true), near.Count)}｜「被逗笑」{Rate(laugh.Count(r => r.laughed == true), laugh.Count)}");
        var verdicts = rs.Where(r => r.verdict.Length > 0).GroupBy(r => r.verdict).OrderByDescending(g => g.Count()).Select(g => $"{VerdictName(g.Key)} {g.Count()}").ToList();
        if (verdicts.Count > 0) res.lines.Add("被抓后的判断：" + string.Join("、", verdicts));
        var notes = rs.Where(r => r.note.Length > 0).Select(r => $"「{r.note}」").ToList();
        if (notes.Count > 0) res.lines.Add("你写的话：" + string.Join(" ", notes.Skip(Math.Max(0, notes.Count - 6))));

        // S231：改了数值以后到底变好没有 —— 最新版本 vs 上一个版本的「还想再来」。打分是"顺序"不是"距离"（Yannakakis：Ratings are Overrated），
        // 所以用中位数 + A12（随手挑一局新版本、一局旧版本，新的分更高的概率；0.5 = 没区别），每边至少 MinPhaseRounds 局才下结论（单人实验每段 ≥5 个点）。
        res.versionCompare = CompareVersions(all);
        if (res.versionCompare.Length > 0) res.lines.Add(res.versionCompare);
        // S227：旧版本（S227 以前没记版本号）的问题大多已修过 → 单独说明，不当成现在的问题
        var cur = rs.Where(r => r.version > 0).ToList();
        bool stale = cur.Count == 0;
        if (stale) res.lines.Add($"⚠ 这 {rs.Count} 局都是旧版本玩的（{rs[0].time:MM-dd}–{rs[rs.Count - 1].time:MM-dd}，那时还没记版本号）。之后改过：太快（S183 减速 + 开局等待）、机关拦不住他（S185 连招）、跳过他就甩掉（S186 预判追）、来回卡住（S198/S209）、看不懂他（S224 视锥填充、S225 起疑说原因）。下面的建议只能当参考。");
        var basis = stale ? rs : cur;
        foreach (var r in basis)
        {
            if (r.stuck > 0) res.bugs.Add($"{r.time:MM-dd HH:mm} 马里奥卡住被救了 {r.stuck} 次（布局问题，F8 截图那一刻最好）");
            if (r.TimedOut && r.caught == 0 && r.kinds.Count == 0) res.bugs.Add($"{r.time:MM-dd HH:mm} 他 {r.seconds:0} 秒没走完、你也没坑到他 → 可能卡住（违反 H10）");
        }

        // ── 下一步：只用各阶段已经写好的证伪规则 ──
        if (stale) res.next.Add("用新版本玩 5 局（快速测试模式关掉），再点体检 → 这里会按新数据重算。");
        if (res.bugs.Count > 0) res.next.Add(stale ? "旧版本里有卡住/超时的局（同类问题 S198、S209 修过）：新版本再出现就按 F8 截图。" : "先修上面的卡住/超时（bug 优先于平衡）：把反馈包发给 AI。");
        int unread = basis.Count(r => r.verdict == "unfair:couldnt_read_him"), noWarn = basis.Count(r => r.verdict == "unfair:no_warning");
        if (unread + noWarn > 0) res.next.Add($"被抓时觉得「看不懂他 / 没预兆」{unread + noWarn} 次 → 阶段 A 规则：只改呈现（? 停留更久 / 视锥更醒目），不改数值" + (stale ? "（旧版本数据，S224/S225 已经改过呈现，先看新版本还有没有）" : "") + "。");
        if (laugh.Count >= 5 && laugh.Count(r => r.laughed == true) == 0) res.next.Add("5 局以上一次没被逗笑 → 阶段 B 规则：缩短愣住/动作时间，不加更多特效。");
        if (n >= 5 && mario * 10 >= n * 8 && med < 15f) res.next.Add($"马里奥几乎全赢、每局不到 15 秒 → 先看「算准了」是不是很少；是的话加长开局等待或减慢他（调参 startDelaySeconds / marioSpeedScale），一次只改一个。");
        if (near.Count >= 5 && near.Count(r => r.nearMiss == true) == 0) res.next.Add("「差点被发现」一次都没有 → 他起疑太慢或太快抓人：看结算里的「他起疑过几次」，0 次 = 视野太窄。");
        if (kinds.Count < ExitKinds && n >= 5) res.next.Add($"坑法只有 {kinds.Count} 种 → 试着用不同机关坑他（火、挡板、塌桥、大炮、香蕉皮、弹簧、裂缝、铁笼、甩掉追捕都算）" + (stale ? "；旧版本只记会伤人的机关，S227 起香蕉皮/弹簧等也记" : "") + "。");
        if (res.exitMet) res.next.Insert(0, "第 1 步出口达到 → 可以开始第 2 步（3 个手工房间 + 谨慎型 / 贪财型）。");
        else if (res.next.Count == 0) res.next.Add(session.Count < ExitRounds ? $"没发现要改的；继续连着玩到 {ExitRounds} 局（还差 {ExitRounds - session.Count} 局）。" : "20 局够了，但「还想再来」偏低 → 写一句最想改的事发给 AI。");
        return res;
    }

    // ═════════ S231：统计小工具（纯函数，sim 逐项验算） ═════════
    /// <summary>单人实验每段至少几局才比较（Kratochwill 等：每段 ≥5 个数据点 https://hdsr.mitpress.mit.edu/pub/nqvadq0w ）。</summary>
    public const int MinPhaseRounds = 5;

    public static float Median(IList<int> xs)
    {
        if (xs == null || xs.Count == 0) return 0f;
        var o = xs.OrderBy(x => x).ToList(); int m = o.Count / 2;
        return o.Count % 2 == 1 ? o[m] : (o[m - 1] + o[m]) / 2f;
    }

    /// <summary>Vargha–Delaney A12：随机挑一个 a、一个 b，a 比 b 大的概率（相等算一半）。= Mann–Whitney U / (n1·n2)。</summary>
    public static double A12(IList<int> a, IList<int> b)
    {
        if (a == null || b == null || a.Count == 0 || b.Count == 0) return 0.5;
        double win = 0; foreach (var x in a) foreach (var y in b) win += x > y ? 1 : x == y ? 0.5 : 0;
        return win / (a.Count * (double)b.Count);
    }

    /// <summary>A12 的大小档（Vargha & Delaney 2000：|A−0.5| ≥0.06 小、≥0.14 中、≥0.21 大）。</summary>
    public static string A12Size(double a)
    {
        double d = Math.Abs(a - 0.5);
        return d >= 0.21 ? "大" : d >= 0.14 ? "中" : d >= 0.06 ? "小" : "几乎没有";
    }

    /// <summary>最新调参版本 vs 上一个有问卷的版本。样本不够 → 说还差几局；够了 → 中位数 + A12 + 档位。</summary>
    public static string CompareVersions(IList<Round> all)
    {
        var rated = all.Where(r => r.version > 0 && r.wantAgain > 0).ToList();
        var vers = rated.Select(r => r.version).Distinct().OrderByDescending(v => v).ToList();
        if (vers.Count < 2) return "";
        var a = rated.Where(r => r.version == vers[0]).Select(r => r.wantAgain).ToList();
        var b = rated.Where(r => r.version == vers[1]).Select(r => r.wantAgain).ToList();
        string head = $"**改版前后**（v{vers[1]} → v{vers[0]}，「还想再来」）：";
        if (a.Count < MinPhaseRounds || b.Count < MinPhaseRounds)
            return head + $"新版本 {a.Count} 局、旧版本 {b.Count} 局，每边要 ≥{MinPhaseRounds} 局才比较（少了看到的多半是运气）。";
        double p = A12(a, b);
        string dir = p > 0.5 ? "新版本更想再玩" : p < 0.5 ? "旧版本更想再玩" : "一样";
        return head + $"中位数 {Median(b):0.#} → {Median(a):0.#}；新版本某局比旧版本某局分高的概率 {p:P0}（差别{A12Size(p)}：{dir}）。" +
            (A12Size(p) == "几乎没有" ? "看不出变化 → 这次改动可能不是关键，别再朝同一方向加码。" : "");
    }

    /// <summary>恶作剧种类的中文名（结算"最稀罕的一招"用）。</summary>
    public static string KindZh(string k)
    {
        switch (k)
        {
            case "Fire": return "火焰"; case "Blocker": return "挡板"; case "Collapse": return "塌桥"; case "Cannon": return "大炮";
            case "Banana": return "香蕉皮"; case "Spring": return "弹簧"; case "CrackFloor": return "裂缝"; case "Cage": return "铁笼";
            case "Tripwire": return "绊线"; case "Snare": return "绳套"; case "Pit": return "掉坑"; case "Escape": return "甩掉追捕";
            default: return k;
        }
    }

    /// <summary>
    /// S231（Select the Unexpected：越少见的事越像"故事" https://www.researchgate.net/publication/365929507_Select_the_Unexpected_A_Statistical_Heuristic_for_Story_Sifting ）：
    /// 这局用到的坑法里，以前的局用得最少的那个。history 不含这局。没有坑法 = null。
    /// 返回 (种类, 以前多少局用过, 以前一共多少局)。同样少见按名字排，结果固定。
    /// </summary>
    public static (string kind, int before, int total)? RarestKind(IList<Round> history, IEnumerable<string> thisRound)
    {
        var ks = (thisRound ?? Enumerable.Empty<string>()).Where(k => !string.IsNullOrEmpty(k)).Distinct().ToList();
        if (ks.Count == 0) return null;
        int total = history?.Count ?? 0;
        var best = ks.Select(k => (k, before: history == null ? 0 : history.Count(r => r.kinds.Contains(k))))
                     .OrderBy(x => x.before).ThenBy(x => x.k, StringComparer.Ordinal).First();
        return (best.k, best.before, total);
    }

    /// <summary>结算画面的一行（只在真的稀罕时说：以前一次没用过，或用过的局 ≤ 1/5）。</summary>
    public static string RarestLine(IList<Round> history, IEnumerable<string> thisRound)
    {
        var r = RarestKind(history, thisRound);
        if (r == null || r.Value.total < MinPhaseRounds) return "";
        var (k, before, total) = r.Value;
        if (before == 0) return $"★ 新招：第一次用「{KindZh(k)}」坑到他！  New trick!";
        if (before * 5 <= total) return $"★ 少见的一招：「{KindZh(k)}」（以前 {total} 局里只有 {before} 局用过）";
        return "";
    }

    public static string VerdictName(string v)
    {
        switch (v)
        {
            case "fair": return "服气";
            case "unfair:no_warning": return "没预兆";
            case "unfair:couldnt_read_him": return "看不懂他";
            case "unfair:slipped": return "手滑";
            case "unfair:gave_myself_away": return "自己露馅";
            default: return v;
        }
    }

    /// <summary>体检报告里的一段（Markdown）。</summary>
    public static string Markdown(IList<Round> all, int minVersion = 0)
    {
        var r = Analyze(all, minVersion);
        var sb = new StringBuilder();
        foreach (var l in r.lines) sb.AppendLine(l);
        if (r.bugs.Count > 0) { sb.AppendLine("⚠ 不对劲的局："); foreach (var b in r.bugs.Take(5)) sb.AppendLine("    • " + b); }
        sb.AppendLine("**下一步**：" + string.Join(" ", r.next.Select((x, i) => $"{i + 1}) {x}")));
        return sb.ToString();
    }

    // ═════════ S230：小镇每天的记录（PlaytestLogs/town_days.csv，S229 起写）→ 体检里的"小镇"一段 ═════════
    /// <summary>town_days.csv 的列，必须和 OverworldSession.DayCsvHeader 一致（sim 检查）。</summary>
    public static readonly string[] TownColumns =
    {
        "timestamp", "map", "day", "outcome", "death", "cause", "by_you", "end_clock", "defended", "looted", "missed", "doors", "caught",
        "your_hearts_lost", "mario_hearts_lost", "big_hits", "best_chain", "reactions", "reaction_median", "tuning_version"
    };
    /// <summary>机器人"看清新目标再动"的愣神范围（OverworldBots.humanNoise）。你的中位数落在外面 = 该校准。</summary>
    public const float BotReactMin = 0.15f, BotReactMax = 0.6f;
    /// <summary>少于这么多次反应不下结论（找问题 6 人 / 12 次以上才看得出中位数稳不稳）。</summary>
    public const int MinReactSamples = 12;

    public sealed class TownDay
    {
        public DateTime time; public string map = "", outcome = "", death = "", cause = ""; public bool byYou;
        public int day, defended, looted, doors, caught, reactions, version; public float reactMedian;
    }

    public static List<TownDay> ParseTown(IEnumerable<string> csvTexts)
    {
        var seen = new HashSet<string>(); var all = new List<TownDay>();
        foreach (var txt in csvTexts)
            foreach (var line in (txt ?? "").Split('\n'))
            {
                var l = line.Trim('\r'); if (l.Length == 0 || l.StartsWith("timestamp")) continue;
                var c = l.Split(','); if (c.Length < TownColumns.Length) continue;
                if (!DateTime.TryParse(c[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) continue;
                if (!seen.Add(l)) continue;
                all.Add(new TownDay { time = t, map = c[1], day = I(c[2]), outcome = c[3], death = c[4], cause = c[5], byYou = c[6] == "yes",
                    defended = I(c[8]), looted = I(c[9]), doors = I(c[11]), caught = I(c[12]), reactions = I(c[17]), reactMedian = F(c[18]), version = I(c[19]) });
            }
        return all.OrderBy(d => d.time).ToList();
    }

    public static string TownMarkdown(IList<TownDay> days)
    {
        var sb = new StringBuilder();
        if (days.Count == 0) { sb.AppendLine("还没有小镇记录（S229 起每天结束自动写 town_days.csv）"); return sb.ToString(); }
        int n = days.Count, won = days.Count(d => d.outcome == "won"), lost = days.Count(d => d.outcome == "lost"), draw = days.Count(d => d.outcome == "draw");
        sb.AppendLine($"· 一共 {n} 天：你赢 {Rate(won, n)}，输 {lost}，平 {draw}");
        int md = days.Count(d => d.death == "MarioDied"), yd = days.Count(d => d.death == "YouDied"), bd = days.Count(d => d.death == "Both");
        int nature = days.Count(d => d.death == "MarioDied" && !d.byYou);
        sb.AppendLine($"· 心掉光结束的天：他倒下 {md}（其中天灾替你打死 {nature}），你倒下 {yd}，同归于尽 {bd}");
        if (n >= 10 && nature * 5 > n) sb.AppendLine($"  ⚠ 天灾替你赢了 {Rate(nature, n)} 天 → 超过 1/5：调参 overworldBoltTelegraphSeconds 加长或雷区缩小（sim 门槛要求挂机 ≤6%）");
        if (n >= 10 && yd * 3 > n) sb.AppendLine($"  ⚠ 你被打死 {Rate(yd, n)} 天 → 超过 1/3：先看死因，是预警没看见（F8 截图）还是躲不开（调保护期）");
        // 反应时间：每天的中位数按次数加权展开成样本，再取总中位数（近似，只用来判断落没落在机器人范围里）
        var samples = new List<float>(); foreach (var d in days) for (int i = 0; i < d.reactions; i++) samples.Add(d.reactMedian);
        if (samples.Count == 0) sb.AppendLine("· 你的反应时间：还没量到（危险预警出现在你脚下 → 你第一次按方向键）");
        else
        {
            samples.Sort(); float med = samples[samples.Count / 2];
            string verdict = samples.Count < MinReactSamples ? $"样本 {samples.Count} 次 < {MinReactSamples}，先不校准"
                : med < BotReactMin ? $"比机器人最快的 {BotReactMin}s 还快 → 机器人偏慢，sim 偏保守（不用改）"
                : med > BotReactMax ? $"比机器人最慢的 {BotReactMax}s 还慢 → 机器人太灵，sim 的『会躲 60/60』对你偏乐观：把 OverworldBots 的愣神上限调到 {med:0.00}s 重跑门槛"
                : $"落在机器人 {BotReactMin}–{BotReactMax}s 范围里 → 机器人手抖参数和你一致";
            sb.AppendLine($"· 你的反应时间中位数 ≈ {med:0.00}s（{samples.Count} 次）：{verdict}");
        }
        return sb.ToString();
    }
}
