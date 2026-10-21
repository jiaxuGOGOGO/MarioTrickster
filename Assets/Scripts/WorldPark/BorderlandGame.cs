using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// S251 弥留之国式“博弈”内核：多人同场、隐藏信息、证词与背叛、规则追加、淘汰。
/// 纯 C#、只用整数与固定伪随机（minstd），与 Tools/LevelStudioWeb/borderland.js 逐字同语义。
/// 三种模式对应三类原作效果（只借抽象机制，文字与角色全部原创）：
///   beauty —— 数字猜心（平均值×比例，递归推理；淘汰后追加规则）
///   mark   —— 看不见的标记（自己的标记只能听别人说；有人说谎；每轮公开真相，可据此识破）
///   split  —— 分还是偷（逐轮对不同参与者，先听对方承诺再选；信任、报复、最后一轮背叛）
/// 玩家选择以字符串列表给出；可只给前 N 轮，用于运行器逐轮显示公开信息（pending）。
/// </summary>
public static class BorderlandGame
{
    public const int MinSeats = 2, MaxSeats = 8, MaxRounds = 20, MaxLives = 20, MaxParseSeats = 64;
    public static readonly string[] Modes = { "beauty", "mark", "split" };
    public static readonly string[] Personas = { "player", "naive", "reasoner", "deep", "adaptive", "liar", "loyal", "mirror" };
    public static readonly string[] Suits = { "♠", "♥", "♦", "♣" };

    [Serializable]
    public sealed class Seat
    {
        public string id = "";
        public string name = "";
        public string persona = "";
    }

    [Serializable]
    public sealed class Def
    {
        public string mode = "";
        public int rounds;
        public int lives;
        public int ratio;
        public int goal;
        public int seed;
        public int escalate;
        public List<Seat> seats = new List<Seat>();
    }

    public sealed class Issue
    {
        public string severity = "", code = "", text = "";
    }

    /// <summary>每轮事实，供模拟器统计体验；不含作者未公开的信息之外的推断。</summary>
    public sealed class RoundRecord
    {
        public int round;
        public string choice = "";
        public int livesBefore, livesAfter, pointsAfter, aliveAfter;
        public bool correct = true;
        /// <summary>mark 模式：只用已公开信息（排除被识破者后的多数证词）能得出的答案；其他模式为空。</summary>
        public string informedAnswer = "";
        /// <summary>mark 模式：轻信第一个证词会得到的答案。</summary>
        public string firstClaim = "";
        public readonly List<string> eliminated = new List<string>();
        /// <summary>S252：本轮每个座位公开亮出的选择（按座位顺序；没参加本轮的为空串）。beauty 为数字，mark 为宣告的花色，split 为“分/偷”（只有你和本轮对手）。供博弈桌逐座位翻牌。</summary>
        public string[] picks = new string[0];
        /// <summary>S252：mark 模式每个座位真实的标记（本轮结束后公开）；其他模式为空串。</summary>
        public string[] marks = new string[0];
        /// <summary>S252：beauty 模式本轮目标值（平均×比例，四舍五入）；其他模式为 -1。</summary>
        public int target = -1;
        /// <summary>S252：split 模式本轮对手的座位；其他模式为 -1。</summary>
        public int opponent = -1;
        /// <summary>S252：beauty 模式本轮不扣命的座位（最接近 / 0 对 100 获胜者）；其他模式为空。</summary>
        public readonly List<int> winners = new List<int>();
    }

    public sealed class State
    {
        public bool finished, won;
        public string error = "";
        public int round;
        public int player = -1;
        public int[] lives = new int[0];
        public int[] points = new int[0];
        public bool[] alive = new bool[0];
        public string prompt = "";
        public readonly List<string> transcript = new List<string>();
        public readonly List<string> pending = new List<string>();
        public readonly List<RoundRecord> records = new List<RoundRecord>();
    }

    public static bool IsMode(string value) { return Array.IndexOf(Modes, value) >= 0; }
    public static bool IsPersona(string value) { return Array.IndexOf(Personas, value) >= 0; }

    /// <summary>结构检查；WorldParkPack.Validate 会把它们挂到对应会场/阶段。</summary>
    public static List<Issue> Check(Def d)
    {
        var issues = new List<Issue>();
        if (d == null) { Add(issues, "error", "game_def_required", "game 阶段需要 game 规则对象"); return issues; }
        if (!IsMode(d.mode)) Add(issues, "error", "game_mode_invalid", "博弈 mode 只能是 beauty / mark / split：" + Safe(d.mode));
        if (d.rounds < 1 || d.rounds > MaxRounds) Add(issues, "error", "game_rounds_invalid", "博弈轮数必须是 1–" + MaxRounds);
        if (d.lives < 1 || d.lives > MaxLives) Add(issues, "error", "game_lives_invalid", "每人命数必须是 1–" + MaxLives);
        if (d.mode == "beauty" && (d.ratio < 1 || d.ratio > 100)) Add(issues, "error", "game_ratio_invalid", "数字猜心的比例必须是 1–100（百分比）");
        if (d.goal < 0 || d.goal > 100) Add(issues, "error", "game_goal_invalid", "目标分数必须是 0–100");
        if (d.seed < 0) Add(issues, "error", "game_seed_invalid", "随机种子不能小于 0");
        if (d.escalate != 0 && d.escalate != 1) Add(issues, "error", "game_escalate_invalid", "escalate 只能是 0 或 1");
        List<Seat> seats = d.seats ?? new List<Seat>();
        if (seats.Count < MinSeats || seats.Count > MaxSeats) Add(issues, "error", "game_seats_invalid", "参与者必须是 " + MinSeats + "–" + MaxSeats + " 人（含玩家）");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int players = 0, deceivers = 0;
        foreach (Seat seat in seats)
        {
            if (seat == null) { Add(issues, "error", "game_seat_id_invalid", "参与者为空"); continue; }
            if (InvalidId(seat.id)) Add(issues, "error", "game_seat_id_invalid", "参与者 id 不能为空、含斜杠或控制字符");
            else if (!ids.Add(seat.id)) Add(issues, "error", "game_seat_id_duplicate", "参与者 id 重复：" + seat.id);
            if (!IsPersona(seat.persona)) Add(issues, "error", "game_seat_persona_invalid", "未知人格：" + Safe(seat.persona));
            if (seat.persona == "player") players++;
            if (d.mode == "mark" && seat.persona == "liar") deceivers++;
            if (d.mode == "split" && (seat.persona == "liar" || seat.persona == "mirror" || seat.persona == "adaptive" || seat.persona == "reasoner" || seat.persona == "deep")) deceivers++;
        }
        if (players != 1) Add(issues, "error", "game_player_seat", "必须恰好有一个 persona 为 player 的玩家席位");
        if ((d.mode == "mark" || d.mode == "split") && players == 1 && seats.Count >= MinSeats && deceivers == 0)
            Add(issues, "warn", "game_no_deceiver", "没有会说谎或会报复的参与者：这局缺少信任压力，结果几乎没有悬念");
        return issues;
    }

    /// <summary>按给定的玩家选择推进。choices 少于所需轮数时返回未结束状态与下一轮公开信息。</summary>
    public static State Run(Def d, IList<string> choices)
    {
        State result = RunCore(d, choices);
        int given = choices == null ? 0 : choices.Count;
        if (result.error == "" && result.finished && result.records.Count < given)
            result.error = "博弈已在第" + I(result.round) + "轮结束，多余的选择：" + I(given - result.records.Count) + "个";
        return result;
    }

    private static State RunCore(Def d, IList<string> choices)
    {
        var st = new State();
        foreach (Issue issue in Check(d))
            if (issue.severity == "error") { st.error = issue.text; return st; }
        int n = d.seats.Count;
        st.lives = new int[n]; st.points = new int[n]; st.alive = new bool[n];
        for (int i = 0; i < n; i++)
        {
            st.lives[i] = d.lives; st.alive[i] = true;
            if (d.seats[i].persona == "player") st.player = i;
        }
        long rng = d.seed % 2147483647L;
        if (rng <= 0) rng = 1;
        IList<string> input = choices ?? new List<string>();
        int used = 0;
        int eliminatedTotal = 0, prevTarget = -1, ruleStage = 0;
        bool duel = false;
        bool playerBetrayed = false;
        var exposed = new bool[n];
        var meet = new int[n];
        var lastAgainst = new string[n];
        for (int i = 0; i < n; i++) lastAgainst[i] = "split";
        st.transcript.Add(Intro(d));

        for (int r = 0; r < d.rounds; r++)
        {
            // —— 回合开始：只计算“选择前就该公开”的信息 ——
            st.pending.Clear();
            st.pending.Add("第" + I(r + 1) + "/" + I(d.rounds) + "轮" + (r == d.rounds - 1 ? "（最后一轮）" : "") + "　存活：" + AliveSummary(d, st));
            int[] marks = null;
            int opp = -1, meet0 = 0;
            if (d.mode == "beauty")
            {
                var active = new List<string>();
                if (ruleStage >= 1) active.Add("同数字无效");
                if (ruleStage >= 2) active.Add("命中目标时其他人各扣两条命");
                if (duel) active.Add("只剩两人时有人选0则选100者获胜");
                st.pending.Add("追加规则：" + (active.Count == 0 ? "无" : string.Join("；", active.ToArray())));
                st.prompt = "选择 0–100 的整数；最接近“全体平均×" + I(d.ratio) + "%”者本轮不扣命";
            }
            else if (d.mode == "mark")
            {
                marks = new int[n];
                for (int i = 0; i < n; i++) if (st.alive[i]) marks[i] = Rand(ref rng, 4);
                foreach (int k in AliveNpcs(d, st))
                    st.pending.Add(d.seats[k].name + "说你的标记是" + Suits[Claim(d, marks, k, st.player, r)]);
                st.prompt = "宣告你自己的标记：♠ ♥ ♦ ♣（答错失去一条命）";
            }
            else
            {
                List<int> npcs = AliveNpcs(d, st);
                opp = npcs[r % npcs.Count];
                meet0 = meet[opp];
                st.pending.Add("本轮对手：" + d.seats[opp].name + "　对方说：“" + Promise(d, opp, meet0, playerBetrayed) + "”");
                st.pending.Add("你：" + I(st.points[st.player]) + "分 " + I(st.lives[st.player]) + "命　目标：" + I(d.goal) + "分");
                st.prompt = "选择：分 或 偷（双分各+1；偷对分者+2且对方失去一命；双偷什么都得不到）";
            }

            if (used >= input.Count) return st; // 等待玩家这一轮的选择
            string raw = input[used++] ?? "";
            var rec = new RoundRecord { round = r + 1, choice = raw, livesBefore = st.lives[st.player] };
            rec.picks = Blank(n);
            rec.marks = Blank(n);
            st.transcript.Add("—— 第" + I(r + 1) + "轮 ——");
            foreach (string line in st.pending) st.transcript.Add(line);

            if (d.mode == "beauty")
            {
                int pick;
                if (!ParseNumber(raw, out pick)) return Bad(st, r, raw);
                prevTarget = BeautyRound(d, st, pick, ref rng, ruleStage, duel, prevTarget, rec);
            }
            else if (d.mode == "mark")
            {
                int suit = ParseSuit(raw);
                if (suit < 0) return Bad(st, r, raw);
                MarkRound(d, st, marks, suit, r, exposed, rec);
            }
            else
            {
                string move = ParseMove(raw);
                if (move == "") return Bad(st, r, raw);
                string theirs = SplitRound(d, st, opp, meet0, move, r, playerBetrayed, lastAgainst, rec);
                meet[opp]++;
                lastAgainst[opp] = move;
                if (move == "steal" && theirs == "split") playerBetrayed = true;
            }

            // —— 淘汰与规则追加 ——
            for (int i = 0; i < n; i++)
                if (st.alive[i] && st.lives[i] <= 0)
                {
                    st.alive[i] = false;
                    eliminatedTotal++;
                    rec.eliminated.Add(d.seats[i].name);
                    st.transcript.Add(d.seats[i].name + "出局");
                }
            // 每轮最多追加一条：多人同时出局也不会一次塞进几条规则；只剩两人时优先宣布决斗规则（它只在决斗里有意义）。
            if (d.mode == "beauty" && d.escalate == 1 && st.alive[st.player] && AliveNpcs(d, st).Count > 0 && r < d.rounds - 1)
            {
                if (!duel && AliveNpcs(d, st).Count == 1)
                {
                    duel = true;
                    st.transcript.Add("【新规则】从下一轮起：只剩两人时，若有人选0，选100的人获胜");
                }
                else if (eliminatedTotal > ruleStage && ruleStage < 2)
                {
                    ruleStage++;
                    st.transcript.Add(ruleStage == 1 ? "【新规则】从下一轮起：两人以上选同一个数字，该数字无效并扣命" : "【新规则】从下一轮起：有人正好命中目标时，其他人各扣两条命");
                }
            }
            rec.livesAfter = st.lives[st.player];
            rec.pointsAfter = st.points[st.player];
            rec.aliveAfter = AliveNpcs(d, st).Count + (st.alive[st.player] ? 1 : 0);
            st.records.Add(rec);
            st.round = r + 1;

            bool lastRound = r == d.rounds - 1;
            if (!st.alive[st.player]) return Finish(st, false, "你失去了全部的命");
            if (AliveNpcs(d, st).Count == 0)
            {
                if (d.mode == "split" && st.points[st.player] < d.goal) return Finish(st, false, "对手都已出局，但你的分数没有达到目标");
                return Finish(st, true, "你是最后的存活者");
            }
            if (lastRound)
            {
                if (d.mode == "split" && st.points[st.player] < d.goal) return Finish(st, false, "时间到，分数只有" + I(st.points[st.player]) + "，未达到" + I(d.goal));
                return Finish(st, true, "坚持到了最后一轮");
            }
        }
        return Finish(st, st.alive[st.player], "轮数结束");
    }

    // ———————————————————————— 三种模式 ————————————————————————

    private static int BeautyRound(Def d, State st, int pick, ref long rng, int ruleStage, bool duel, int prevTarget, RoundRecord rec)
    {
        int n = d.seats.Count, R = d.ratio;
        var order = new List<int>();
        for (int i = 0; i < n; i++) if (st.alive[i]) order.Add(i);
        var c = new int[n];
        foreach (int i in order)
        {
            if (i == st.player) { c[i] = pick; continue; }
            int j = Rand(ref rng, 10);
            string p = d.seats[i].persona;
            int v;
            // 只剩两人时“更小的数一定赢”，除直觉型外都会写 0；第三条规则（0 对 100）正是对这一步的反制。
            if (order.Count == 2 && p != "naive") v = 0;
            else if (p == "naive") v = 40 + 2 * j;
            else if (p == "deep") v = (50 * R * R) / 10000 + j / 3;
            else if (p == "adaptive") v = prevTarget < 0 ? 30 + j : prevTarget;
            else v = (50 * R) / 100 + j - 4;
            c[i] = Math.Max(0, Math.Min(100, v));
        }
        long sum = 0;
        foreach (int i in order) sum += c[i];
        long cnt = order.Count;
        bool dupRule = ruleStage >= 1, exactRule = ruleStage >= 2, zeroRule = duel && order.Count == 2;
        var invalid = new bool[n];
        if (dupRule)
            foreach (int i in order)
                foreach (int k in order)
                    if (k != i && c[k] == c[i]) invalid[i] = true;
        long best = long.MaxValue;
        foreach (int i in order) if (!invalid[i]) best = Math.Min(best, Dist(c[i], cnt, sum, R));
        var winners = new List<int>();
        foreach (int i in order) if (!invalid[i] && Dist(c[i], cnt, sum, R) == best) winners.Add(i);
        bool zeroHit = false;
        if (zeroRule && !invalid[order[0]] && !invalid[order[1]])
        {
            int a = order[0], b = order[1];
            int hundred = c[a] == 100 && c[b] == 0 ? a : c[b] == 100 && c[a] == 0 ? b : -1;
            if (hundred >= 0) { winners.Clear(); winners.Add(hundred); zeroHit = true; }
        }
        bool exactHit = !zeroHit && exactRule && winners.Count > 0 && best == 0;
        int target = (int)((2 * sum * R + 100 * cnt) / (200 * cnt));
        foreach (int i in order) rec.picks[i] = I(c[i]);
        rec.target = target;
        rec.winners.AddRange(winners);

        var picks = new List<string>();
        foreach (int i in order) picks.Add(d.seats[i].name + "选" + I(c[i]));
        st.transcript.Add(string.Join("，", picks.ToArray()));
        st.transcript.Add("平均×" + I(R) + "% ≈ " + I(target));
        var inv = new List<string>();
        foreach (int i in order) if (invalid[i]) inv.Add(d.seats[i].name);
        if (inv.Count > 0) st.transcript.Add("重复数字无效：" + string.Join("、", inv.ToArray()));
        var win = new List<string>();
        foreach (int i in winners) win.Add(d.seats[i].name);
        st.transcript.Add("最接近：" + (win.Count > 0 ? string.Join("、", win.ToArray()) : "无人"));
        if (zeroHit) st.transcript.Add("0 对 100：选100的人获胜");
        if (exactHit) st.transcript.Add("有人正好命中！其他人各扣两条命");
        foreach (int i in order)
        {
            if (winners.Contains(i)) continue;
            st.lives[i] -= invalid[i] ? 1 : (exactHit ? 2 : 1);
        }
        var lives = new List<string>();
        foreach (int i in order) lives.Add(d.seats[i].name + I(Math.Max(0, st.lives[i])) + "命");
        st.transcript.Add("命数：" + string.Join("，", lives.ToArray()));
        rec.correct = winners.Contains(st.player);
        return target;
    }

    private static void MarkRound(Def d, State st, int[] marks, int suit, int r, bool[] exposed, RoundRecord rec)
    {
        int n = d.seats.Count, me = st.player;
        rec.informedAnswer = Suits[Majority(d, st, marks, me, r, exposed, true)];
        rec.firstClaim = Suits[FirstClaim(d, st, marks, me, r)];
        var declared = new int[n];
        for (int i = 0; i < n; i++)
        {
            if (!st.alive[i]) continue;
            if (i == me) declared[i] = suit;
            else if (d.seats[i].persona == "liar") declared[i] = marks[i];
            else if (d.seats[i].persona == "naive") declared[i] = FirstClaim(d, st, marks, i, r);
            else declared[i] = Majority(d, st, marks, i, r, exposed, true);
            rec.picks[i] = Suits[declared[i]];
            rec.marks[i] = Suits[marks[i]];
        }
        st.transcript.Add("你宣告" + Suits[suit] + "，真实是" + Suits[marks[me]] + (suit == marks[me] ? "：答对" : "：答错，失去一条命"));
        rec.correct = suit == marks[me];
        for (int i = 0; i < n; i++)
        {
            if (!st.alive[i]) continue;
            if (declared[i] != marks[i]) st.lives[i] -= 1;
            if (i != me) st.transcript.Add(d.seats[i].name + "宣告" + Suits[declared[i]] + "，真实是" + Suits[marks[i]] + (declared[i] == marks[i] ? "" : "：答错"));
        }
        // 每轮结束公开全部证词真伪：给玩家识破说谎者的公平证据。
        for (int k = 0; k < n; k++)
        {
            if (!st.alive[k] || k == me) continue;
            for (int t = 0; t < n; t++)
            {
                if (!st.alive[t] || t == k) continue;
                int claim = Claim(d, marks, k, t, r);
                if (claim != marks[t])
                {
                    st.transcript.Add("公开记录：" + d.seats[k].name + "对" + (t == me ? "你" : d.seats[t].name) + "说" + Suits[claim] + "，实际是" + Suits[marks[t]]);
                    exposed[k] = true;
                }
            }
        }
    }

    /// <summary>
    /// 收益：双分各+1；偷对分者+2、被偷者失去一命；双偷 0。
    /// 名声：玩家偷过“选分的人”后公开留名（playerBetrayed），理性者从此防着你。
    /// </summary>
    private static string SplitRound(Def d, State st, int opp, int meet0, string move, int r, bool playerBetrayed, string[] lastAgainst, RoundRecord rec)
    {
        string p = d.seats[opp].persona, theirs;
        if (p == "liar") theirs = "steal";
        else if (p == "mirror" || p == "adaptive") theirs = meet0 == 0 ? "split" : lastAgainst[opp];
        else if (p == "reasoner") theirs = playerBetrayed ? "steal" : "split";
        else if (p == "deep") theirs = (r == d.rounds - 1 || playerBetrayed) ? "steal" : "split";
        else theirs = "split";
        int me = st.player;
        string name = d.seats[opp].name;
        rec.opponent = opp;
        rec.picks[me] = MoveName(move);
        rec.picks[opp] = MoveName(theirs);
        st.transcript.Add("你选" + MoveName(move) + "，" + name + "选" + MoveName(theirs));
        if (move == "split" && theirs == "split") { st.points[me] += 1; st.points[opp] += 1; st.transcript.Add("双方各得1分"); }
        else if (move == "steal" && theirs == "split") { st.points[me] += 2; st.lives[opp] -= 1; st.transcript.Add("你得2分，" + name + "失去一条命"); }
        else if (move == "split" && theirs == "steal") { st.points[opp] += 2; st.lives[me] -= 1; st.transcript.Add(name + "得2分，你失去一条命"); }
        else st.transcript.Add("两人都偷，什么都没得到");
        st.transcript.Add("你：" + I(st.points[me]) + "分 " + I(Math.Max(0, st.lives[me])) + "命；" + name + "：" + I(st.points[opp]) + "分 " + I(Math.Max(0, st.lives[opp])) + "命");
        if (move == "steal" && theirs == "split" && !playerBetrayed) st.transcript.Add("所有人都看到了：你偷了一个选分的人");
        rec.correct = theirs == "split";
        return theirs;
    }

    // ———————————————————————— 人格规则 ————————————————————————

    private static int Claim(Def d, int[] marks, int speaker, int target, int r)
    {
        if (d.seats[speaker].persona != "liar") return marks[target];
        if (d.seats[target].persona == "player" && r == 0) return marks[target]; // 第一轮对玩家说真话，换取信任
        return (marks[target] + 1) % 4;
    }

    private static int FirstClaim(Def d, State st, int[] marks, int target, int r)
    {
        for (int k = 0; k < d.seats.Count; k++)
            if (st.alive[k] && k != target) return k == st.player ? marks[target] : Claim(d, marks, k, target, r);
        return 0;
    }

    /// <summary>排除已被公开记录识破的说谎者后取多数；平票取座位靠前者的说法。全员被识破时退回全部证词。</summary>
    private static int Majority(Def d, State st, int[] marks, int target, int r, bool[] exposed, bool excludeExposed)
    {
        var counts = new int[4];
        var firstSeat = new int[] { int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue };
        bool any = false;
        for (int pass = 0; pass < 2 && !any; pass++)
            for (int k = 0; k < d.seats.Count; k++)
            {
                if (!st.alive[k] || k == target) continue;
                if (pass == 0 && excludeExposed && exposed[k]) continue;
                int claim = k == st.player ? marks[target] : Claim(d, marks, k, target, r);
                counts[claim]++;
                if (k < firstSeat[claim]) firstSeat[claim] = k;
                any = true;
            }
        int bestSuit = 0;
        for (int s = 1; s < 4; s++)
            if (counts[s] > counts[bestSuit] || (counts[s] == counts[bestSuit] && firstSeat[s] < firstSeat[bestSuit])) bestSuit = s;
        return bestSuit;
    }

    private static string Promise(Def d, int opp, int meet0, bool playerBetrayed)
    {
        string p = d.seats[opp].persona;
        if (p == "mirror" || p == "adaptive") return meet0 == 0 ? "我会选分" : "你上次对我选什么，我这次就选什么";
        if (p == "liar") return "我保证选分，相信我";
        if ((p == "reasoner" || p == "deep") && playerBetrayed) return "你偷过选分的人，我不会再信你";
        return "我会选分";
    }

    // ———————————————————————— 工具 ————————————————————————

    private static long Dist(int value, long count, long sum, int ratio)
    {
        long d = value * 100L * count - sum * ratio;
        return d < 0 ? -d : d;
    }

    private static int Rand(ref long s, int range)
    {
        s = (s * 48271L) % 2147483647L;
        return (int)(s % range);
    }

    private static List<int> AliveNpcs(Def d, State st)
    {
        var list = new List<int>();
        for (int i = 0; i < d.seats.Count; i++) if (st.alive[i] && i != st.player) list.Add(i);
        return list;
    }

    private static string AliveSummary(Def d, State st)
    {
        var parts = new List<string>();
        for (int i = 0; i < d.seats.Count; i++)
            if (st.alive[i]) parts.Add((i == st.player ? "你" : d.seats[i].name) + "(" + I(st.lives[i]) + "命)");
        return string.Join("、", parts.ToArray());
    }

    private static string Intro(Def d)
    {
        string name = d.mode == "beauty" ? "数字猜心" : d.mode == "mark" ? "看不见的标记" : "分还是偷";
        return "【" + name + "】" + I(d.seats.Count) + "人参加，" + I(d.rounds) + "轮，每人" + I(d.lives) + "条命";
    }

    private static State Bad(State st, int r, string raw)
    {
        st.error = "第" + I(r + 1) + "轮选择无效：" + Safe(raw);
        return st;
    }

    private static State Finish(State st, bool won, string reason)
    {
        st.finished = true;
        st.won = won;
        st.pending.Clear();
        st.prompt = "";
        st.transcript.Add((won ? "【通关】" : "【失败】") + reason);
        return st;
    }

    private static bool ParseNumber(string raw, out int value)
    {
        value = -1;
        if (string.IsNullOrEmpty(raw) || raw.Length > 3) return false;
        foreach (char ch in raw) if (ch < '0' || ch > '9') return false;
        value = int.Parse(raw, CultureInfo.InvariantCulture);
        return value <= 100;
    }

    private static int ParseSuit(string raw)
    {
        switch (raw)
        {
            case "♠": case "S": return 0;
            case "♥": case "H": return 1;
            case "♦": case "D": return 2;
            case "♣": case "C": return 3;
        }
        return -1;
    }

    private static string ParseMove(string raw)
    {
        if (raw == "split" || raw == "分") return "split";
        if (raw == "steal" || raw == "偷") return "steal";
        return "";
    }

    private static string MoveName(string move) { return move == "steal" ? "偷" : "分"; }

    private static bool InvalidId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        foreach (char ch in value) if (ch == '/' || ch < 0x20) return true;
        return false;
    }

    private static void Add(List<Issue> issues, string severity, string code, string text)
    {
        issues.Add(new Issue { severity = severity, code = code, text = text });
    }

    private static string I(int value) { return value.ToString(CultureInfo.InvariantCulture); }
    private static string I(long value) { return value.ToString(CultureInfo.InvariantCulture); }
    private static string Safe(string value) { return value ?? ""; }
    private static string[] Blank(int n) { var a = new string[n]; for (int i = 0; i < n; i++) a[i] = ""; return a; }
}
