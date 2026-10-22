using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// S242：伪装装备栏（纯逻辑，sim 也编译）。用户："捣蛋者可以选择三个（或者更多可自定义）游戏场景内的道具或者机关本身，
/// 同时捣蛋者丢出的诱饵也是选择的这三个中的任何一个 可以自由切换"。
/// 参考：
///   · Witch It —— 诱饵 = 你当前模仿的那个道具（0.5.3 起），丢出去会自己动，骗找你的人 https://witch-it.fandom.com/wiki/Decoy
///   · Prop Hunt（CoD / PUBG）—— 只能变成"这张图里真有的东西"，外形和周围一样才藏得住；切换形态有代价 https://callofduty.fandom.com/wiki/Prop_Hunt https://pubg.com/news/9889
/// 规则：
///   1. 候选 = 这个房间里真的有、单格、能"站在那儿装样子"的东西（箱子、草丛、装饰、油桶、灯、弹簧、香蕉皮、火、铁笼、大炮、道具箱…）；
///      默认挑房间里最多的 N 个（越多越不显眼——藏木于林）。
///   2. 数字键 1–N 选槽（伪装中也能换）；E = 把身边那个东西"取样"进当前槽。
///   3. 伪装中换形态 = 一瞬间"变了个样"：他正好看着你就算你动了（起疑），没看着就没事。
///   4. 诱饵 G = 丢出一个"当前槽"的假道具（伪装中也能丢），它会时不时扭一下，引他过去查看；走近会识破。
/// </summary>
public static class Step1Loadout
{
    public const int MaxSlots = 5; // 数字键 1–5

    /// <summary>能变的东西（按"常见 → 少见"排；同样多时按这个顺序）。地形、宝物、出口、通风管不算。</summary>
    public static readonly char[] Disguisable = { 'c', 'b', 'd', 'U', 'i', 'J', 'n', '~', 'Q', 'K', 'k', '?', 'Y', 'R', '[' };

    public static bool CanDisguiseAs(char ch) => System.Array.IndexOf(Disguisable, ch) >= 0;

    public static int ClampSize(int n) => Mathf.Clamp(n, 1, MaxSlots);

    /// <summary>房间里每种可变的东西有几个（已去掉随机槽位，K/k 合并成 K）。</summary>
    public static Dictionary<char, int> Census(IList<string> rows)
    {
        var d = new Dictionary<char, int>();
        if (rows == null) return d;
        foreach (var r in rows) foreach (var raw in r)
        {
            char c = raw == 'k' ? 'K' : raw;
            if (!CanDisguiseAs(c)) continue;
            d[c] = d.TryGetValue(c, out var n) ? n + 1 : 1;
        }
        return d;
    }

    /// <summary>候选（房间里有的，多的在前）。</summary>
    public static List<char> Candidates(IList<string> rows)
    {
        var census = Census(rows);
        return census.Keys.OrderByDescending(c => census[c]).ThenBy(c => System.Array.IndexOf(Disguisable, c)).ToList();
    }

    /// <summary>默认装备：房间里最多的 n 个；房间里不够 n 种 → 用箱子 / 草丛 / 装饰补齐（总有能变的）。</summary>
    public static List<char> DefaultPick(IList<string> rows, int n)
    {
        n = ClampSize(n);
        var pick = Candidates(rows).Take(n).ToList();
        foreach (var f in new[] { 'c', 'b', 'd', 'U', 'i' }) { if (pick.Count >= n) break; if (!pick.Contains(f)) pick.Add(f); }
        return pick;
    }

    /// <summary>取样：把 ch 放进第 slot 格。已经在别的格里 = 两格交换（不出现重复）。</summary>
    public static List<char> Sample(List<char> slots, int slot, char ch)
    {
        var o = new List<char>(slots ?? new List<char>());
        if (!CanDisguiseAs(ch == 'k' ? 'K' : ch) || slot < 0 || slot >= o.Count) return o;
        ch = ch == 'k' ? 'K' : ch;
        int had = o.IndexOf(ch);
        if (had == slot) return o;
        if (had >= 0) o[had] = o[slot];
        o[slot] = ch;
        return o;
    }

    public static int Cycle(int index, int dir, int count) => count <= 0 ? 0 : ((index + dir) % count + count) % count;

    /// <summary>数字键 → 槽位（超出 = -1）。</summary>
    public static int SlotOfDigit(int digit, int count) => digit >= 1 && digit <= count ? digit - 1 : -1;

    /// <summary>变成 ch 时身体碰撞体多大：照抄那个东西的大小，但夹在 0.6–1.2 格（太小钻墙缝、太大卡门洞）。</summary>
    public static Vector2 BodySize(Vector2 propVisualSize) => new Vector2(Mathf.Clamp(propVisualSize.x, 0.6f, 1.2f), Mathf.Clamp(propVisualSize.y, 0.6f, 1.2f));

    /// <summary>伪装中换形态：变后 window 秒内被他看见 = 算"在动"（他会起疑）。</summary>
    public static bool ShapeShiftVisible(float now, float changedAt, float window) => window > 0f && now - changedAt >= 0f && now - changedAt < window;

    // ── 道具诱饵 ───────────────────────────────────────────
    /// <summary>丢出去的抛物线：t 秒后的位置（水平匀速，竖直受重力；落到起点高度就停在那儿）。</summary>
    public static Vector2 ThrowArc(Vector2 start, bool right, float distance, float t, float flightSeconds = 0.45f)
    {
        if (flightSeconds <= 0f) return start + new Vector2(right ? distance : -distance, 0f);
        float k = Mathf.Clamp01(t / flightSeconds);
        float x = (right ? 1f : -1f) * distance * k;
        float y = 4f * 1.2f * k * (1f - k); // 最高 1.2 格
        return start + new Vector2(x, y);
    }

    /// <summary>道具诱饵会"时不时扭一下"（每 every 秒扭 0.4 秒）——这时他看见就会当成"会动的怪东西"过来查看。</summary>
    public static bool Wriggling(float age, float every, float landedAt)
    {
        if (every <= 0f || age < landedAt) return false;
        float local = (age - landedAt) % every;
        return local < 0.4f;
    }

    /// <summary>S242：诱饵键 G 能不能用。道具诱饵伪装中也能丢（手没被占——是"变出一个分身"）；"假你"诱饵仍要现形。</summary>
    public static bool CanDecoy(bool propDecoy, bool disguised, bool shrunk, int left, bool oneAlready) =>
        !shrunk && left > 0 && !oneAlready && (propDecoy || !disguised);
}
