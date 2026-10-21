using System;
using System.Collections.Generic;

/// <summary>
/// S252 剧情字幕卡（纯逻辑，不依赖 Unity）：把剧情 / 揭示 / 结局 / 房间前的阶段排成一张全屏“场景卡”。
/// 包括：会场扑克牌、标题、逐字出现的正文、揭示时的反转卡（当时以为 → 其实）、错过的线索、随身线索本、
/// 结局章与“下一个会场”的门（开 / 关及原因）、旅程进度。
/// Unity 的 WorldParkScene 只照着画；剧情模拟器用它检验“每次揭示都真的给玩家看到了反转”。
/// 只读会话状态，不推进流程、不改结果。
/// </summary>
public static class WorldParkSceneModel
{
    /// <summary>正文逐字出现的速度（字/秒）。点击可立即显示全文。</summary>
    public const double CharsPerSecond = 30;
    /// <summary>正文出完到第一张反转卡翻开的停顿（秒）。</summary>
    public const double TwistDelay = 0.5;
    /// <summary>相邻两张反转卡的间隔（秒）。</summary>
    public const double TwistGap = 0.9;

    public sealed class Note
    {
        public string clue = "", name = "", before = "", after = "";
    }

    public sealed class Door
    {
        public string venue = "", name = "", card = "", reason = "";
        public bool open;
    }

    public sealed class Scene
    {
        public string kind = "", card = "", tone = "", venueName = "", title = "", text = "", rules = "";
        /// <summary>揭示：这一刻被重新解释的线索（只含你真的发现过的）。</summary>
        public readonly List<Note> twists = new List<Note>();
        /// <summary>揭示：本该在这里被重新解释、但你没发现的线索名（不剧透真相，只告诉你错过了）。</summary>
        public readonly List<string> missed = new List<string>();
        /// <summary>随身线索本：本会场已发现的线索；已揭示的带“其实”。</summary>
        public readonly List<Note> notes = new List<Note>();
        /// <summary>结局章（GAME CLEAR / GAME OVER / 旅程终点）。</summary>
        public string stamp = "", stampSub = "";
        public readonly List<Door> doors = new List<Door>();
        public readonly List<string> journey = new List<string>();
        /// <summary>主按钮文字；结局没有主按钮（用门和“结束旅程”）。</summary>
        public string action = "";
    }

    /// <param name="playable">会场是否已构建可玩的场景（Unity 用构建路由判断；模拟器传 null 表示都可玩）。</param>
    public static Scene Build(WorldParkPack.Session s, Func<WorldParkPack.Venue, bool> playable)
    {
        var sc = new Scene();
        if (s == null || s.pack == null) return sc;
        WorldParkPack.Venue v = s.Venue;
        WorldParkPack.Node n = s.Current;
        if (v == null || n == null) return sc;
        sc.kind = n.kind ?? "";
        sc.card = WorldParkStageCues.Card(v);
        sc.tone = WorldParkStageCues.Tone(v);
        sc.venueName = string.IsNullOrEmpty(v.name) ? v.id : v.name;
        sc.title = string.IsNullOrEmpty(n.name) ? n.id : n.name;
        sc.text = n.text ?? "";
        sc.rules = v.rules ?? "";
        foreach (WorldParkPack.Clue c in v.clues)
        {
            string key = v.id + "/" + c.id;
            bool found = s.found.Contains(key), revealed = s.revealed.Contains(key);
            if (sc.kind == "reveal" && c.revealAt == n.id)
            {
                if (revealed) sc.twists.Add(new Note { clue = c.id, name = c.name ?? c.id, before = c.hint ?? "", after = c.truth ?? "" });
                else if (!found) sc.missed.Add(c.name ?? c.id);
            }
            if (found) sc.notes.Add(new Note { clue = c.id, name = c.name ?? c.id, before = c.hint ?? "", after = revealed ? c.truth ?? "" : "" });
        }
        if (sc.kind == "ending")
        {
            WorldParkStageCues.Cue cue = WorldParkStageCues.Ending(v, n);
            sc.stamp = cue.title;
            sc.stampSub = cue.sub;
            foreach (WorldParkPack.Link l in s.pack.links)
            {
                if (l.from != s.venueId) continue;
                WorldParkPack.Venue target = WorldParkPack.GetVenue(s.pack, l.to);
                bool can = s.CanTravel(l.to), play = target != null && (playable == null || playable(target));
                var door = new Door
                {
                    venue = l.to ?? "",
                    name = target == null ? l.to ?? "" : (string.IsNullOrEmpty(target.name) ? target.id : target.name),
                    card = target == null ? "?" : WorldParkStageCues.Card(target),
                    open = can && play,
                    reason = !can ? "需要本场结果为“" + ResultLabel(l.requires) + "”" : !play ? "这个会场的玩法还没有构建" : ""
                };
                int existing = sc.doors.FindIndex(d => d.venue == door.venue);
                if (existing < 0) sc.doors.Add(door);
                else if (door.open && !sc.doors[existing].open) sc.doors[existing] = door;
            }
        }
        else sc.action = sc.kind == "room" ? "读完提示，进入房间" : "继续";
        foreach (WorldParkPack.Venue venue in s.pack.venues)
        {
            string state = venue.id == s.venueId ? "当前" : "未到达", result;
            if (s.results.TryGetValue(venue.id, out result)) state = ResultLabel(result);
            else if (venue.id != s.venueId && s.CanTravel(venue.id)) state = "可前往";
            // S253：会场名已经以牌面开头（如“♣3 三扇门”）时不再重复牌面；与 play-model.js 同语义
            string card = WorldParkStageCues.Card(venue), label = string.IsNullOrEmpty(venue.name) ? venue.id : venue.name;
            sc.journey.Add((card.Length == 0 || label.StartsWith(card, System.StringComparison.Ordinal) ? label : card + " " + label) + " — " + state);
        }
        return sc;
    }

    /// <summary>t 秒时正文已出现的字数（不会把一个代理对切成两半）。</summary>
    public static int Visible(string text, double t)
    {
        int len = text == null ? 0 : text.Length;
        if (t <= 0 || len == 0) return 0;
        int k = (int)Math.Min(len, Math.Floor(t * CharsPerSecond));
        if (k > 0 && k < len && char.IsHighSurrogate(text[k - 1])) k++;
        return k;
    }

    public static double TextDone(Scene sc) { return (sc == null || sc.text == null ? 0 : sc.text.Length) / CharsPerSecond; }
    public static double TwistAt(Scene sc, int i) { return TextDone(sc) + TwistDelay + i * TwistGap; }
    /// <summary>全部内容出完（主按钮从“显示全部”变为正式操作）的时刻。</summary>
    public static double ReadyAt(Scene sc)
    {
        if (sc == null) return 0;
        return sc.twists.Count == 0 ? TextDone(sc) : TwistAt(sc, sc.twists.Count - 1) + 0.4;
    }

    public static string ResultLabel(string result)
    {
        switch (result ?? "")
        {
            case "clear": return "通过";
            case "fail": return "未通过";
            case "dead": return "出局";
            case "leave": return "离开";
            case "any": return "任意";
            default: return result ?? "";
        }
    }
}
