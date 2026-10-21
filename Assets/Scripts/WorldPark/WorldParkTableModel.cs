using System;
using System.Collections.Generic;

/// <summary>
/// S252 博弈桌（纯逻辑，不依赖 Unity）：把 BorderlandGame 的状态排成“一张桌子”——
/// 座位位置、每人的命/分、别人对你说的话（气泡）、本轮亮牌顺序、结算文字、新规则宣布。
/// Unity 的 WorldParkTable 只照着它画；剧情模拟器用它检验“桌上看得见的东西”与规则记录一致。
/// 只使用规则已经公开的信息：NPC 的人格（作者的秘密）永远不会出现在这里。
///
/// 阶段：choose 等你出手 → reveal 逐个翻牌 → result 结算停留 → 下一轮 choose；最后一轮结算后进入 done。
/// revealT 是“最近一轮提交后过了多少秒”；小于 0 表示没有正在播放的亮牌（例如刚进入会场）。
/// </summary>
public static class WorldParkTableModel
{
    /// <summary>提交后第一张 NPC 牌翻开的时刻（秒）。</summary>
    public const double FlipStart = 0.5;
    /// <summary>相邻两张 NPC 牌翻开的间隔（秒）。</summary>
    public const double FlipGap = 0.45;
    /// <summary>最后一张牌翻开到出结算文字的停顿（秒）。</summary>
    public const double ResultDelay = 0.35;
    /// <summary>结算文字停留多久才进入下一轮（秒；点击可跳过）。</summary>
    public const double ResultHold = 1.6;

    public sealed class Seat
    {
        public int index;
        public string name = "", initial = "";
        public bool me, alive = true, active, spotlight, revealed, winner, wrong, eliminatedNow;
        public int lives, maxLives, points, lost, gain;
        /// <summary>0–1：桌面画布里的相对位置。你永远坐在正下方，其他人沿上半圈排开。</summary>
        public double x, y;
        /// <summary>气泡：这一轮出手前别人对你说的话。</summary>
        public string bubble = "";
        /// <summary>本轮亮出的选择（数字 / 宣告的花色 / 分或偷）。</summary>
        public string pick = "";
        /// <summary>mark 模式结算后公开的真实标记。</summary>
        public string truth = "";
    }

    public sealed class View
    {
        public string mode = "", phase = "";
        public int player = -1;
        public readonly List<Seat> seats = new List<Seat>();
        public string roundLine = "", prompt = "";
        /// <summary>没法挂到某个座位上的公开信息（存活名单、追加规则、你的分数目标等）。</summary>
        public readonly List<string> info = new List<string>();
        public bool revealing, resultShown, inputOpen, finished, won;
        /// <summary>已翻开的 NPC 牌数（Unity 按它的变化放翻牌音效）。</summary>
        public int flipped;
        public double revealLength;
        public string centerBig = "", centerSmall = "", newRule = "", finishLine = "";
    }

    /// <summary>这一轮亮牌 + 结算一共要播多久。</summary>
    public static double RevealLength(BorderlandGame.RoundRecord rec, int player)
    {
        int npc = 0;
        if (rec != null)
            for (int i = 0; i < rec.picks.Length; i++)
                if (i != player && !string.IsNullOrEmpty(rec.picks[i])) npc++;
        return FlipStart + Math.Max(0, npc - 1) * FlipGap + ResultDelay + ResultHold;
    }

    /// <param name="before">最近一轮提交之前的状态（用于“本轮失去几条命/得几分”）；没有可传 null。</param>
    public static View Build(BorderlandGame.Def def, BorderlandGame.State before, BorderlandGame.State st, double revealT)
    {
        var v = new View();
        if (def == null || def.seats == null || st == null || !string.IsNullOrEmpty(st.error)) { v.phase = "error"; return v; }
        v.mode = def.mode ?? "";
        v.player = st.player;
        int n = Math.Min(def.seats.Count, st.alive.Length);
        BorderlandGame.RoundRecord rec = st.records.Count > 0 ? st.records[st.records.Count - 1] : null;
        if (before != null && (before.alive.Length != n || before.records.Count != st.records.Count - 1)) before = null;

        if (rec != null && revealT >= 0)
        {
            v.revealLength = RevealLength(rec, st.player);
            double resultAt = v.revealLength - ResultHold;
            if (revealT < resultAt) v.phase = "reveal";
            else if (revealT < v.revealLength) v.phase = "result";
        }
        if (v.phase == "") v.phase = st.finished ? "done" : "choose";
        v.revealing = v.phase == "reveal" || v.phase == "result";
        v.resultShown = v.phase == "result" || v.phase == "done";
        v.inputOpen = v.phase == "choose";
        v.finished = v.phase == "done";
        v.won = st.won;

        // —— 座位 ——
        BorderlandGame.State src = v.phase == "reveal" && before != null ? before : st;
        int npcCount = 0;
        for (int i = 0; i < n; i++) if (i != st.player) npcCount++;
        int k = 0;
        for (int i = 0; i < n; i++)
        {
            var s = new Seat { index = i, me = i == st.player, maxLives = def.lives };
            s.name = s.me ? "你" : (string.IsNullOrEmpty(def.seats[i].name) ? def.seats[i].id ?? "" : def.seats[i].name);
            s.initial = s.me ? "你" : FirstChar(s.name);
            s.alive = src.alive[i];
            s.lives = Math.Max(0, i < src.lives.Length ? src.lives[i] : 0);
            s.points = i < src.points.Length ? src.points[i] : 0;
            if (s.me) { s.x = 0.5; s.y = 0.86; }
            else
            {
                double a = Math.PI + Math.PI * (k + 1) / (npcCount + 1);
                s.x = 0.5 + 0.40 * Math.Cos(a);
                s.y = 0.46 + 0.34 * Math.Sin(a);
                k++;
            }
            v.seats.Add(s);
        }

        if (v.phase == "choose") BuildChoose(def, st, v, n);
        else if (rec != null) BuildReveal(def, before, st, rec, v, n, v.phase == "done" ? double.MaxValue : revealT);

        // —— 顶部轮次行 ——
        if (v.phase == "choose") v.roundLine = "第 " + (st.round + 1) + " / " + def.rounds + " 轮" + (st.round + 1 == def.rounds ? "（最后一轮）" : "");
        else if (v.phase == "done") v.roundLine = "博弈结束 · 共 " + st.round + " 轮";
        else v.roundLine = "第 " + (rec == null ? st.round : rec.round) + " 轮 · 亮牌";
        if (v.mode == "split") v.roundLine += "　你的目标：" + def.goal + " 分";

        if (v.finished)
        {
            string last = st.transcript.Count > 0 ? st.transcript[st.transcript.Count - 1] : "";
            v.finishLine = (st.won ? "你活下来了" : "失败") + (last.StartsWith("【", StringComparison.Ordinal) && last.IndexOf('】') > 0 ? "：" + last.Substring(last.IndexOf('】') + 1) : "");
        }
        return v;
    }

    private static void BuildChoose(BorderlandGame.Def def, BorderlandGame.State st, View v, int n)
    {
        v.prompt = st.prompt ?? "";
        foreach (string line in st.pending)
        {
            string said;
            int who = -1;
            if (v.mode == "mark")
            {
                who = Speaker(def, n, st.player, line, "", "说你的标记是", out said);
                if (who >= 0) said = "你的标记是 " + said;
            }
            else if (v.mode == "split")
            {
                who = Speaker(def, n, st.player, line, "本轮对手：", "　对方说：", out said);
                if (who >= 0) { said = said.Trim('“', '”', '"'); v.seats[who].spotlight = true; }
            }
            else said = "";
            if (who >= 0) v.seats[who].bubble = said;
            else v.info.Add(line);
        }
        foreach (Seat s in v.seats) s.active = s.alive && (v.mode != "split" || s.me || s.spotlight);
    }

    private static void BuildReveal(BorderlandGame.Def def, BorderlandGame.State before, BorderlandGame.State st, BorderlandGame.RoundRecord rec, View v, int n, double t)
    {
        int j = 0, flips = 0;
        for (int i = 0; i < n; i++)
        {
            Seat s = v.seats[i];
            string pick = i < rec.picks.Length ? rec.picks[i] ?? "" : "";
            s.active = pick != "";
            if (v.mode == "split" && i == rec.opponent) s.spotlight = true;
            if (!s.active) continue;
            s.pick = pick;
            if (s.me) s.revealed = true;
            else
            {
                s.revealed = t >= FlipStart + j * FlipGap;
                if (s.revealed) flips++;
                j++;
            }
        }
        v.flipped = flips;
        if (!v.resultShown) return;

        var winnerNames = new List<string>();
        for (int i = 0; i < n; i++)
        {
            Seat s = v.seats[i];
            if (before != null)
            {
                s.lost = Math.Max(0, Math.Max(0, before.lives[i]) - Math.Max(0, st.lives[i]));
                s.gain = i < st.points.Length && i < before.points.Length ? st.points[i] - before.points[i] : 0;
                s.eliminatedNow = before.alive[i] && !st.alive[i];
            }
            if (!s.active) continue;
            if (v.mode == "mark") { s.truth = i < rec.marks.Length ? rec.marks[i] ?? "" : ""; s.wrong = s.truth != "" && s.truth != s.pick; }
            if (v.mode == "beauty" && rec.winners.Contains(i)) { s.winner = true; winnerNames.Add(s.name); }
        }
        int me = st.player;
        if (v.mode == "beauty")
        {
            v.centerBig = "目标 " + rec.target;
            v.centerSmall = "最接近：" + (winnerNames.Count > 0 ? string.Join("、", winnerNames.ToArray()) : "无人") + "；其余的人扣命";
        }
        else if (v.mode == "mark")
        {
            string mine = me >= 0 && me < rec.marks.Length ? rec.marks[me] : "?";
            v.centerBig = "你的标记是 " + mine;
            v.centerSmall = rec.correct ? "答对了" : "答错，失去一条命";
        }
        else
        {
            int opp = rec.opponent;
            string oppName = opp >= 0 && opp < v.seats.Count ? v.seats[opp].name : "对手";
            string mine = me >= 0 && me < rec.picks.Length ? rec.picks[me] : "", theirs = opp >= 0 && opp < rec.picks.Length ? rec.picks[opp] : "";
            v.centerBig = "你 " + mine + "　·　" + oppName + " " + theirs;
            if (mine == "分" && theirs == "分") v.centerSmall = "双方各 +1 分";
            else if (mine == "偷" && theirs == "分") v.centerSmall = "你 +2 分，" + oppName + " 失去一条命";
            else if (mine == "分" && theirs == "偷") v.centerSmall = oppName + " +2 分，你失去一条命";
            else v.centerSmall = "两人都偷，什么都没得到";
        }
        int from = before != null ? before.transcript.Count : 0;
        for (int i = from; i < st.transcript.Count; i++)
            if (st.transcript[i].StartsWith("【新规则】", StringComparison.Ordinal)) v.newRule = st.transcript[i];
    }

    /// <summary>找出这句公开信息是谁说的：prefix + 名字 + mid 开头；名字重叠时取最长的那个。</summary>
    private static int Speaker(BorderlandGame.Def def, int n, int player, string line, string prefix, string mid, out string rest)
    {
        int best = -1, bestLen = -1;
        rest = "";
        for (int i = 0; i < n; i++)
        {
            if (i == player) continue;
            string name = def.seats[i].name ?? "";
            if (name.Length == 0) continue;
            string head = prefix + name + mid;
            if (line.StartsWith(head, StringComparison.Ordinal) && name.Length > bestLen) { best = i; bestLen = name.Length; rest = line.Substring(head.Length); }
        }
        return best;
    }

    private static string FirstChar(string name)
    {
        if (string.IsNullOrEmpty(name)) return "?";
        return char.IsSurrogate(name[0]) && name.Length > 1 ? name.Substring(0, 2) : name.Substring(0, 1);
    }
}
