using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// S244：一局的"节奏 + 暂停菜单 + 小镇存档"——纯逻辑（沙盒 sim 编译验证），Step1PauseMenu / Step1Rhythm / OverworldGame 只做接线。
/// 用户 S244："交互动画控制节奏把握以及暂停保存UI功能 继续完善一版"。自检发现：
///   ① 暂停只有一行"已暂停 Esc 继续"，没有重开 / 帮助 / 回小镇 / 设置；
///   ② 小镇的一天只在内存里（OverworldSession 注释原话"不写存档"），关掉游戏就没了；
///   ③ 开局只有左上角一行"x 秒后出发"，拿到宝 / 最后 10 秒没有醒目的提示，节奏全靠读字。
/// 做法参考：暂停菜单 = 继续 / 重开 / 设置 / 退出这一套是平台游戏的通行做法（Celeste、Hollow Knight 都是方向键选 + 确认）；
/// 存档 = 自动存（到点就存，不用玩家记得按），读档时校验版本、坏档直接丢弃不报错（宁可重新开始一天也不卡死，H9）。
/// </summary>
public static class Step1Flow
{
    // ═════════════ 暂停菜单 ═════════════
    public enum PauseItem { Resume, Restart, Help, Settings, BackToTown, Quit }

    /// <summary>暂停菜单有哪几项：从小镇进来的房间多一项"回小镇（这扇门算没守住）"；编辑器里"退出"= 停止 Play。</summary>
    public static List<PauseItem> PauseItems(bool fromTown)
    {
        var l = new List<PauseItem> { PauseItem.Resume, PauseItem.Restart, PauseItem.Help, PauseItem.Settings };
        if (fromTown) l.Add(PauseItem.BackToTown);
        l.Add(PauseItem.Quit);
        return l;
    }

    public static string PauseLabel(PauseItem i)
    {
        switch (i)
        {
            case PauseItem.Resume: return "继续  Resume";
            case PauseItem.Restart: return "重开这一局  Restart";
            case PauseItem.Help: return "怎么玩  How to play";
            case PauseItem.Settings: return "设置  Settings";
            case PauseItem.BackToTown: return "回小镇（这扇门算没守住）  Back to town";
            default: return "退出  Quit";
        }
    }

    /// <summary>上下选：到头了绕回来（菜单短，绕回比卡住顺手）。</summary>
    public static int Move(int index, int delta, int count)
    {
        if (count <= 0) return 0;
        int i = (index + delta) % count; return i < 0 ? i + count : i;
    }

    /// <summary>数字键直接选第几项（1 = 第一项）。超出范围 = -1。</summary>
    public static int DigitPick(int digit, int count) => digit >= 1 && digit <= count ? digit - 1 : -1;

    // ── 设置（暂停菜单里的子页，改的就是调参文件里同名字段，不新建配置——S238 规则：只有一个调参文件）──
    public enum Setting { GameSpeed, ScreenShake, SoundRings, ArtSkin, HelpOnStart, Countdown }

    public static readonly Setting[] Settings = { Setting.GameSpeed, Setting.ScreenShake, Setting.SoundRings, Setting.ArtSkin, Setting.HelpOnStart, Setting.Countdown };

    public static string SettingLabel(Setting s)
    {
        switch (s)
        {
            case Setting.GameSpeed: return "游戏速度  Game speed";
            case Setting.ScreenShake: return "震屏  Screen shake";
            case Setting.SoundRings: return "声音圈  Sound rings";
            case Setting.ArtSkin: return "像素美术  Pixel art";
            case Setting.HelpOnStart: return "开局显示说明  Help on start";
            default: return "开局倒计时  Start countdown";
        }
    }

    /// <summary>游戏速度一档一档调：0.5 / 0.6 / 0.75 / 0.9 / 1（和调参 roomGameSpeed 的范围一致）。</summary>
    public static readonly float[] SpeedSteps = { 0.5f, 0.6f, 0.75f, 0.9f, 1f };

    public static float StepSpeed(float now, int dir)
    {
        int best = SpeedSteps.Length - 1; float bd = 9f;
        for (int i = 0; i < SpeedSteps.Length; i++) { float d = SpeedSteps[i] - now; if (d < 0f) d = -d; if (d < bd) { bd = d; best = i; } }
        int k = best + (dir > 0 ? 1 : dir < 0 ? -1 : 0);
        k = k < 0 ? 0 : k >= SpeedSteps.Length ? SpeedSteps.Length - 1 : k;
        return SpeedSteps[k];
    }

    public static string OnOff(bool on) => on ? "<color=#7CFC7C>开 ON</color>" : "<color=#AAAAAA>关 OFF</color>";
    public static string SpeedText(float v) => v >= 0.999f ? "正常 ×1" : "慢 ×" + v.ToString("0.##", CultureInfo.InvariantCulture);

    // ═════════════ 节奏：开局倒计时 / 横幅 ═════════════

    /// <summary>开局倒计时：马里奥还要等 waitLeft 秒才出发。最后 3 秒显示 3 · 2 · 1，出发那一下显示"开始！"0.6 秒。
    /// 返回空串 = 不显示。sinceGo = 他出发后过了几秒（还没出发 &lt; 0）。</summary>
    public static string CountdownText(float waitLeft, float sinceGo)
    {
        if (waitLeft > 0f)
        {
            if (waitLeft > 3f) return "";
            int n = (int)System.Math.Ceiling(waitLeft);
            return n < 1 ? "1" : n.ToString();
        }
        return sinceGo >= 0f && sinceGo < 0.6f ? "开始！ GO!" : "";
    }

    /// <summary>数字"砸下来"的缩放：每个数字刚出现时 1.6 倍，0.25 秒内缩回 1（像格斗游戏的 3-2-1）。frac = 这个数字已经显示了几秒。</summary>
    public static float PunchScale(float frac)
    {
        if (frac <= 0f) return 1.6f;
        if (frac >= 0.25f) return 1f;
        float u = frac / 0.25f; return 1.6f - 0.6f * (1f - (1f - u) * (1f - u));
    }

    public enum Banner { None, LootTaken, LastTen, Comboed }

    /// <summary>横幅文字（屏幕上方大字 1.5 秒）：他拿到宝了 / 最后 10 秒。</summary>
    public static string BannerText(Banner b)
    {
        switch (b)
        {
            case Banner.LootTaken: return "<color=#FFD54F>他拿到宝了！</color> 拦住他回出口  <size=26>Loot taken — stop him!</size>";
            case Banner.LastTen: return "<color=#FF8A80>最后 10 秒！</color> 再拖一下就赢  <size=26>Last 10 seconds!</size>";
            default: return "";
        }
    }

    /// <summary>最后 10 秒横幅只在"刚跨过 10 秒"那一帧触发一次（前一帧 &gt; 10、这一帧 ≤ 10）。</summary>
    public static bool CrossedLastTen(float prevTimer, float timer) => prevTimer > 10f && timer <= 10f && timer > 0f;

    /// <summary>横幅的透明度：0.15 秒淡入、停留、最后 0.35 秒淡出。</summary>
    public static float BannerAlpha(float age, float total)
    {
        if (age < 0f || age >= total) return 0f;
        if (age < 0.15f) return age / 0.15f;
        float left = total - age; return left < 0.35f ? left / 0.35f : 1f;
    }

    // ═════════════ 小镇存档（自动存 / 继续这一天）═════════════
    public const int SaveVersion = 2; // S245：+存档点类型 / 名字 / 时间 / 道具箱用过的格子 / 带进房间的炸弹（旧的 1 版照样读）

    /// <summary>存档里有什么：哪张小镇、第几天、几点、每扇门的结果、下一站、两人的心和能量、你的位置。
    /// 只在"小镇里"存（房间打到一半不存——房间一局 1–2 分钟，从门口重来更公平）。</summary>
    public sealed class TownSave
    {
        public int version = SaveVersion;
        public string map = "", scene = "";
        public int day = 1, nextStop, marioHearts = 3, youHearts = 3, energy, caught, taunts;
        public double minute, marioX, marioY, youX, youY;
        public bool hasPositions;
        /// <summary>S245：哪种存档点（自动 / 进门前 / 打完房间 / 新的一天 / 手动）、存的时候（本地时间 yyyy-MM-dd HH:mm）、序号（越大越新）。</summary>
        public int kind; public string when = ""; public double stamp; public int bonusBombs;
        public readonly HashSet<int> used = new HashSet<int>();
        public readonly Dictionary<int, int> results = new Dictionary<int, int>();
        public readonly Dictionary<int, char> changed = new Dictionary<int, char>();
    }

    public static string ToJson(TownSave s)
    {
        var sb = new StringBuilder("{");
        void N(string k, double v) { sb.Append('"').Append(k).Append("\":").Append(v.ToString("0.###", CultureInfo.InvariantCulture)).Append(','); }
        void S(string k, string v) { sb.Append('"').Append(k).Append("\":\"").Append((v ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\","); }
        N("version", s.version); S("map", s.map); S("scene", s.scene); N("day", s.day); N("minute", s.minute); N("nextStop", s.nextStop);
        N("marioHearts", s.marioHearts); N("youHearts", s.youHearts); N("energy", s.energy); N("caught", s.caught); N("taunts", s.taunts);
        N("kind", s.kind); S("when", s.when); N("stamp", s.stamp); N("bonusBombs", s.bonusBombs);
        sb.Append("\"used\":["); bool f0 = true; foreach (var u in s.used) { if (!f0) sb.Append(','); f0 = false; sb.Append(u); } sb.Append("],");
        N("hasPositions", s.hasPositions ? 1 : 0); N("marioX", s.marioX); N("marioY", s.marioY); N("youX", s.youX); N("youY", s.youY);
        sb.Append("\"results\":{");
        bool first = true; foreach (var kv in s.results) { if (!first) sb.Append(','); first = false; sb.Append('"').Append(kv.Key).Append("\":").Append(kv.Value); }
        sb.Append("},\"changed\":{");
        first = true; foreach (var kv in s.changed) { if (!first) sb.Append(','); first = false; sb.Append('"').Append(kv.Key).Append("\":").Append((int)kv.Value); }
        sb.Append("}}");
        return sb.ToString();
    }

    /// <summary>读档：格式坏了 / 版本不对 / 数字离谱 = 返回 null（当没有存档，重新开始一天，不报错不卡死）。</summary>
    public static TownSave FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        var o = MiniJson.Parse(json, out string err) as Dictionary<string, object>;
        if (o == null) return null;
        double D(string k, double def = 0) => o.TryGetValue(k, out var v) && v is double d ? d : def;
        string Str(string k) => o.TryGetValue(k, out var v) && v is string s ? s : "";
        int ver = (int)D("version", -1);
        if (ver != SaveVersion && ver != 1) return null; // S245：1 版（S244）也能读，缺的字段按默认
        var r = new TownSave
        {
            map = Str("map"), scene = Str("scene"), day = (int)D("day", 1), minute = D("minute"), nextStop = (int)D("nextStop"),
            marioHearts = (int)D("marioHearts", 3), youHearts = (int)D("youHearts", 3), energy = (int)D("energy"), caught = (int)D("caught"), taunts = (int)D("taunts"),
            hasPositions = D("hasPositions") > 0.5, marioX = D("marioX"), marioY = D("marioY"), youX = D("youX"), youY = D("youY"),
            kind = (int)D("kind"), when = Str("when"), stamp = D("stamp"), bonusBombs = (int)D("bonusBombs"),
        };
        if (r.kind < 0 || r.kind > (int)Checkpoint.Manual) r.kind = 0;
        if (r.bonusBombs < 0 || r.bonusBombs > 9) r.bonusBombs = 0;
        if (o.TryGetValue("used", out var uv) && uv is List<object> ul)
            foreach (var x in ul) if (x is double dx && dx >= 0 && dx < 1e6) r.used.Add((int)dx);
        if (o.TryGetValue("results", out var rv) && rv is Dictionary<string, object> rd)
            foreach (var kv in rd) if (int.TryParse(kv.Key, out int door) && kv.Value is double res && res >= 0 && res <= 3) r.results[door] = (int)res;
        if (o.TryGetValue("changed", out var cv) && cv is Dictionary<string, object> cd)
            foreach (var kv in cd) if (int.TryParse(kv.Key, out int cell) && kv.Value is double ch && ch >= 32 && ch < 127) r.changed[cell] = (char)(int)ch;
        if (!Valid(r)) return null;
        return r;
    }

    /// <summary>数字合不合理（防止手改 / 写坏的存档把人放到墙里或时间倒流）。</summary>
    public static bool Valid(TownSave s)
    {
        if (s == null || string.IsNullOrEmpty(s.map) || string.IsNullOrEmpty(s.scene)) return false;
        if (s.day < 1 || s.day > 9999 || s.nextStop < 0 || s.nextStop > 64) return false;
        if (s.minute < OverworldMap.DayStart || s.minute > OverworldMap.DayEnd) return false;
        if (s.marioHearts < 1 || s.marioHearts > OverworldSession.MaxHearts || s.youHearts < 1 || s.youHearts > OverworldSession.MaxHearts) return false;
        if (s.energy < 0 || s.energy > OverworldSession.MaxEnergy) return false;
        return true;
    }

    /// <summary>把现在的一天打包（只在小镇里、这一天还没结束时调用）。</summary>
    public static TownSave Capture()
    {
        var s = new TownSave
        {
            map = OverworldSession.MapName, scene = OverworldSession.TownScene, day = OverworldSession.Day, minute = OverworldSession.Minute, nextStop = OverworldSession.NextStop,
            marioHearts = OverworldSession.MarioHearts, youHearts = OverworldSession.YouHearts, energy = OverworldSession.Energy, caught = OverworldSession.Caught, taunts = OverworldSession.TauntsUsed,
            hasPositions = OverworldSession.HasPositions, marioX = OverworldSession.MarioX, marioY = OverworldSession.MarioY, youX = OverworldSession.TricksterX, youY = OverworldSession.TricksterY,
        };
        foreach (var kv in OverworldSession.Results) s.results[kv.Key] = (int)kv.Value;
        foreach (var kv in OverworldSession.Changed) s.changed[kv.Key] = kv.Value;
        foreach (var u in OverworldSession.UsedCells) s.used.Add(u);
        s.bonusBombs = OverworldSession.BonusBombs;
        return s;
    }

    /// <summary>读档后恢复这一天（先 NewDay 清空，再把存的值放回去）。地图名对不上 = 不恢复（返回 false）。</summary>
    public static bool Restore(TownSave s, string mapName)
    {
        if (!Valid(s) || s.map != mapName) return false;
        OverworldSession.NewDay(s.map, s.scene, s.day);
        OverworldSession.Minute = s.minute; OverworldSession.NextStop = s.nextStop;
        OverworldSession.MarioHearts = s.marioHearts; OverworldSession.YouHearts = s.youHearts; OverworldSession.Energy = s.energy;
        OverworldSession.Caught = s.caught; OverworldSession.TauntsUsed = s.taunts;
        OverworldSession.HasPositions = s.hasPositions; OverworldSession.MarioX = s.marioX; OverworldSession.MarioY = s.marioY; OverworldSession.TricksterX = s.youX; OverworldSession.TricksterY = s.youY;
        foreach (var kv in s.results) OverworldSession.Results[kv.Key] = (OverworldSession.DoorResult)kv.Value;
        foreach (var kv in s.changed) OverworldSession.Changed[kv.Key] = kv.Value;
        foreach (var u in s.used) OverworldSession.UsedCells.Add(u); // S245：捡过的道具箱 / 补心 / 踩过的香蕉皮不会读档后又出现
        OverworldSession.BonusBombs = s.bonusBombs;
        return true;
    }

    /// <summary>多久自动存一次（现实秒）。另外：从房间回到小镇、天亮那一刻也各存一次。</summary>
    public const float AutoSaveSeconds = 20f;

    /// <summary>存档一行摘要（暂停菜单 / 开始提示里显示）："第 2 天 14:30 · 守住 2 扇 · 被偷 1 扇"。</summary>
    public static string SaveSummary(TownSave s)
    {
        if (s == null) return "";
        int def = 0, lost = 0; foreach (var kv in s.results) { if (kv.Value == (int)OverworldSession.DoorResult.Defended) def++; else if (kv.Value == (int)OverworldSession.DoorResult.Looted) lost++; }
        return $"第 {s.day} 天 {OverworldMap.Clock(s.minute)} · 守住 {def} 扇 · 被偷 {lost} 扇";
    }

    // ═════════════ S245：存档进度点（多个存档位 + 关键时刻自动存）═════════════
    // 用户 S245：「增加存档进度点方便下次进入游戏可以继续上次游玩」。自检：S244 只有 1 个自动档，而且①天亮结算时把档删了（下次进来又是第 1 天）；
    // ②进门打房间时不存（关掉游戏 = 回到 20 秒前，可能已经过了那扇门）；③捡过的道具箱读档后又出现。
    // 做法参考：存档点放在"自然停顿处"（进门前、打完一关、新的一天），并且留几个手动位给玩家自己存（https://www.gamedeveloper.com/design/save-systems-in-games ；
    // 星露谷每天早上自动存 https://stardewvalleywiki.com/Saves ）。读档时列出每个档的"第几天几点 · 守住几扇 · 什么时候存的"，一眼挑。
    public enum Checkpoint { Auto, DoorEnter, RoomDone, Dawn, Manual }
    /// <summary>存档位：0 = 自动（进度点都写这里），1–3 = 手动（暂停菜单 → 存到存档位）。</summary>
    public const int ManualSlots = 3;
    public static string SlotKey(string baseKey, int slot) => slot <= 0 ? baseKey : baseKey + ".Slot" + slot;

    public static string CheckpointLabel(Checkpoint k)
    {
        switch (k)
        {
            case Checkpoint.DoorEnter: return "进门前";
            case Checkpoint.RoomDone: return "打完房间";
            case Checkpoint.Dawn: return "新的一天";
            case Checkpoint.Manual: return "手动";
        }
        return "自动";
    }

    /// <summary>存档位一行："自动 · 进门前 · 第 2 天 14:30 · 守住 1 扇 · 被偷 0 扇 · 10-09 15:20"。空位 = "（空）"。</summary>
    public static string SlotLine(int slot, TownSave s)
    {
        string name = slot <= 0 ? "自动" : "存档位 " + slot;
        if (s == null) return name + " · （空）";
        string when = string.IsNullOrEmpty(s.when) ? "" : " · " + (s.when.Length >= 16 ? s.when.Substring(5) : s.when);
        return name + " · " + CheckpointLabel((Checkpoint)s.kind) + " · " + SaveSummary(s) + when;
    }

    /// <summary>继续列表：有档的位（自动在前，手动 1–3），最新的排第一个（默认选中）。</summary>
    public static List<int> ContinueOrder(TownSave[] slots)
    {
        var l = new List<int>();
        if (slots == null) return l;
        for (int i = 0; i < slots.Length; i++) if (slots[i] != null) l.Add(i);
        l.Sort((a, b) => { int c = slots[b].stamp.CompareTo(slots[a].stamp); return c != 0 ? c : a.CompareTo(b); });
        return l;
    }

    /// <summary>天亮结算：存一个"第 N+1 天早上"的档（以前直接删档 → 下次进来又是第 1 天）。只留地图 / 场景 / 天数，其余是新的一天。</summary>
    public static TownSave DawnSave(string map, string scene, int nextDay)
    {
        return new TownSave { map = map ?? "", scene = scene ?? "", day = System.Math.Max(1, nextDay), minute = OverworldMap.DayStart, kind = (int)Checkpoint.Dawn };
    }
}
