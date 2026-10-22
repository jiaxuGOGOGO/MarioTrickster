using System.Collections.Generic;

/// <summary>
/// S233 灵感骰子：一掷 = 一个"今天在谁家门口、什么天气、用什么机关、加一个小限制"的创作题目（Oblique Strategies / 设计卡片式"给一个约束"）。
/// 不改地图、不进玩法；只给做关卡和写台词的人一个起点。同一个种子永远同一组（网页 idRoll 逐字一致，verify 对照）。
/// </summary>
public static class IdeaDice
{
    public static readonly string[] Props = { "巨炮 K", "滚石 O", "水塔 U", "钟楼 B" };
    public static readonly string[] Smalls = { "香蕉皮 L", "高草（躲）", "雷区", "山丘 ^（挡视线）", "山洞 h（一对）", "木箱伪装 P", "挑衅 T", "道具箱 ?" };
    public static readonly string[] Weathers = { "晴天", "大风", "雨天", "雾天", "赶集日", "雷雨", "酸雨" };
    public static readonly string[] Twists =
    {
        "让他背对这户人家的门", "两个机关连成一串（冲击 1.5 格内）", "全程只许用一次挑衅", "让这户人家当场看见他被砸",
        "门口 5 格内没有草可躲", "他坐炮抄近路时你去拨歪", "你得先绕到他身后", "让他差点发现你（视锥擦过）",
        "这户人家今天第 3 次来往（准备说真心话）", "宝贝被偷也要好笑", "下一扇门只差 30 秒", "用天气替你出手（你不按 L）",
    };
    public sealed class Idea { public int door; public string trait = "", prop = "", small = "", weather = "", twist = "", text = "", lineStub = ""; }

    static uint H(string s) { uint h = 2166136261u; foreach (char c in s ?? "") { h ^= c; h = unchecked(h * 16777619u); } return h; }
    static string Pick(string[] a, string key) => a[(int)(H(key) % (uint)a.Length)];

    /// <summary>第 i 个题目（seed = 地图名 + 第几次掷）。门从地图上真的有的门里挑；没有门 = 门 1。</summary>
    public static Idea Roll(OverworldMap.Map m, int seed, int i)
    {
        var doors = new List<int>(); if (m != null) foreach (var d in m.doors) if (OverworldMap.Find(m, (char)('0' + d.n)).Count == 1) doors.Add(d.n);
        doors.Sort(); if (doors.Count == 0) doors.Add(1);
        string k = (m != null ? m.name : "") + "|" + seed + "|" + i;
        var it = new Idea { door = doors[(int)(H(k + "|door") % (uint)doors.Count)] };
        var r = TownStory.ResidentOf(m, it.door); it.trait = r.trait;
        it.prop = Pick(Props, k + "|prop"); it.small = Pick(Smalls, k + "|small"); it.weather = Pick(Weathers, k + "|w"); it.twist = Pick(Twists, k + "|t");
        it.text = $"🎲 门{it.door} {TownStory.TraitZh(r.trait)}·{r.name}家门口｜{it.weather}｜{it.prop} + {it.small}｜限制：{it.twist}";
        it.lineStub = $"{{\"id\": \"idea_{r.trait}_{seed}_{i}\", \"who\": \"door\", \"when\": \"witness\", \"tier\": 1, \"tone\": \"comic\", \"coolDays\": 2, \"needs\": [\"trait={r.trait}\"], \"zh\": \"（{TownStory.TraitZh(r.trait)}看见这一下会说什么？）\", \"en\": \"\"}}";
        return it;
    }
    public static List<string> Rolls(OverworldMap.Map m, int seed, int n)
    {
        var l = new List<string>(); for (int i = 0; i < n; i++) l.Add(Roll(m, seed, i).text); return l;
    }
}
