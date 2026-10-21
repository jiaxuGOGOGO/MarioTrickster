using System;
using System.Collections.Generic;

/// <summary>
/// S252 演出层的纯逻辑：什么时候亮灯入场、倒计时哪一秒响、谁出局、结局盖什么章。
/// Unity 演出（WorldParkStage / WorldParkSfx）与剧情带入模拟器都只调用这里，保证“演出时机”可测试、两边一致。
/// 这里不含任何画面或声音代码；也不改变规则结果（演出只读状态）。
/// </summary>
public static class WorldParkStageCues
{
    public const double EntranceSeconds = 3.4;
    public const double ResultSeconds = 2.6;
    public const double EliminatedSeconds = 1.6;
    public const int CountdownFrom = 10;
    public const int CountdownUrgent = 5;

    public sealed class Cue
    {
        /// <summary>entrance 亮灯入场 / laser 出局激光 / pass 活动通过 / eliminated 他人出局 / clear 通关章 / gameover 失败章 / end 旅程终点。</summary>
        public string kind = "";
        public string card = "";
        public string title = "";
        public string sub = "";
        public double seconds;
        /// <summary>WorldParkSfx 的音效名：neon / laser / pass / clear / gameover / end。</summary>
        public string sound = "";
        /// <summary>扑克牌颜色：red（♥♦）或 cold（♠♣与人头牌）。</summary>
        public string tone = "cold";
    }

    public static string SuitSymbol(string suit)
    {
        string s = (suit ?? "").TrimStart();
        if (s.Length == 0) return "";
        return "♠♥♦♣".IndexOf(s[0]) >= 0 ? s[0].ToString() : "";
    }

    public static string Rank(int difficulty)
    {
        if (difficulty == 1) return "A";
        if (difficulty == 11) return "J";
        if (difficulty == 12) return "Q";
        if (difficulty == 13) return "K";
        return difficulty >= 2 && difficulty <= 10 ? difficulty.ToString() : "";
    }

    /// <summary>会场的扑克牌：花色符号 + 点数（如 ♠5、K）。</summary>
    public static string Card(WorldParkPack.Venue venue)
    {
        if (venue == null) return "";
        return SuitSymbol(venue.suit) + Rank(venue.difficulty);
    }

    public static string Tone(WorldParkPack.Venue venue)
    {
        string s = SuitSymbol(venue == null ? "" : venue.suit);
        return s == "♥" || s == "♦" ? "red" : "cold";
    }

    /// <summary>原作规则：通关后签证延长的天数 = 扑克牌点数（至少 1 天）。</summary>
    public static int VisaDays(WorldParkPack.Venue venue)
    {
        return venue == null ? 1 : Math.Max(1, venue.difficulty);
    }

    /// <summary>进入一个新会场时：霓虹扑克牌逐段亮起 + 会场名 + 难度与花色。</summary>
    public static Cue Entrance(WorldParkPack.Venue venue)
    {
        if (venue == null) return null;
        return new Cue
        {
            kind = "entrance", card = Card(venue), title = venue.name ?? venue.id,
            sub = "难度 " + venue.difficulty + (string.IsNullOrEmpty(venue.suit) ? "" : " · " + venue.suit) + " · 参加登记完成 · GAME START",
            seconds = EntranceSeconds, sound = "neon", tone = Tone(venue)
        };
    }

    /// <summary>探索/博弈活动刚结束（结果已锁定、尚未确认）：输了是激光处决，赢了是短促的通过音。</summary>
    public static Cue Activity(WorldParkPack.Venue venue, bool won, string reason)
    {
        return won
            ? new Cue { kind = "pass", card = Card(venue), title = "通过", sub = reason ?? "", seconds = 1.4, sound = "pass", tone = Tone(venue) }
            : new Cue { kind = "laser", card = Card(venue), title = "出局", sub = reason ?? "", seconds = ResultSeconds, sound = "laser", tone = Tone(venue) };
    }

    /// <summary>博弈中其他参加者出局（一束激光落下）。</summary>
    public static Cue Eliminated(string name)
    {
        return new Cue { kind = "eliminated", title = (name ?? "") + " 出局", sub = "GAME OVER", seconds = EliminatedSeconds, sound = "laser", tone = "red" };
    }

    /// <summary>到达会场结局：clear → GAME CLEAR + 签证天数；dead/fail → GAME OVER；其它作者标签（离开、留下……）→ 旅程终点。</summary>
    public static Cue Ending(WorldParkPack.Venue venue, WorldParkPack.Node ending)
    {
        if (ending == null || ending.kind != "ending") return null;
        string result = ending.result ?? "";
        if (result == "clear")
            return new Cue { kind = "clear", card = Card(venue), title = "GAME CLEAR", sub = "签证 +" + VisaDays(venue) + " 天", seconds = ResultSeconds, sound = "clear", tone = Tone(venue) };
        if (result == "dead" || result == "fail")
            return new Cue { kind = "gameover", card = Card(venue), title = "GAME OVER", sub = ending.name ?? "", seconds = ResultSeconds, sound = "gameover", tone = "red" };
        return new Cue { kind = "end", card = Card(venue), title = "旅程终点", sub = string.IsNullOrEmpty(ending.name) ? result : ending.name, seconds = ResultSeconds, sound = "end", tone = Tone(venue) };
    }

    /// <summary>
    /// 倒计时提示：剩余时间从 before 变到 after 时，如果显示的整秒数跨过了 10…1 中的某一秒，返回该秒（响一声）；否则返回 0。
    /// 一次跨过多秒（卡顿）只响最后一秒，避免连响。
    /// </summary>
    public static int CountdownBeep(double before, double after)
    {
        if (!(before > after) || after < 0) return 0;
        int a = (int)Math.Ceiling(before - 1e-9), b = (int)Math.Ceiling(after - 1e-9);
        if (b < a && b >= 1 && b <= CountdownFrom) return b;
        return 0;
    }

    /// <summary>这一秒是否进入“红色急促”阶段。</summary>
    public static bool Urgent(double remaining) { return remaining > 0 && remaining <= CountdownUrgent; }

    /// <summary>两次博弈状态之间新出局的 NPC 名字（玩家自己不算，玩家出局由 Activity 激光处理）。</summary>
    public static List<string> NewlyEliminated(BorderlandGame.Def def, BorderlandGame.State before, BorderlandGame.State after)
    {
        var names = new List<string>();
        if (def == null || before == null || after == null || def.seats == null) return names;
        int n = Math.Min(def.seats.Count, Math.Min(before.alive.Length, after.alive.Length));
        for (int i = 0; i < n; i++)
            if (i != after.player && before.alive[i] && !after.alive[i] && def.seats[i] != null) names.Add(def.seats[i].name);
        return names;
    }

    /// <summary>追逐事件对应的提示（看见 → 警报；跟丢 → 松一口气；草晃 → 它起疑了）。</summary>
    public static string ChaseLine(WorldParkExplore.ChaseEvent e)
    {
        switch (e.type)
        {
            case "seen": return "「" + e.name + "」看见你了！";
            case "lost": return "「" + e.name + "」跟丢了你";
            case "rustle": return "草在晃——「" + e.name + "」起疑了";
            case "giveup": return "「" + e.name + "」放弃搜寻，回去巡逻";
            case "caught": return "被「" + e.name + "」抓住了";
        }
        return "";
    }

    /// <summary>追逐事件对应的音效（WorldParkSfx 名称）。</summary>
    public static string ChaseSound(WorldParkExplore.ChaseEvent e)
    {
        return e.type == "seen" ? "alarm" : e.type == "rustle" ? "rustle" : "";
    }
}
