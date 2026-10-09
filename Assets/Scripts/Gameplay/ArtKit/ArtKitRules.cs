using System.Collections.Generic;

/// <summary>
/// S245：素材包（换画风 + 素材自带互动）的纯逻辑——sim 编译验证，ArtKitSO / ArtKitZone / ArtKitWindow 只做接线。
/// 用户 S245：「以后我有其他画风参考的替换美术资产是否方便 是否可以结合 unity 的功能 让我在关卡编辑的时候直接替换成美术素材；
/// 并且美术素材的资产 比如我有个美术素材是毒池 他也可以让进入范围的角色持续掉血 可以是掉半格也可以是一个格子 自己设定」。
/// 做法：① 每张图都有一个名字（key）——素材包里给这个名字拖一张 Sprite，房间 / 小镇 / 图例全部换成它（不用改代码、不用改关卡）；
/// ② 同一个名字还可以挂一份"互动"：进入范围 → 每隔几秒掉半格 / 一格心（或回血、减速、晕一下），谁会中（马里奥 / 你）自己勾；
/// ③ 4 个"素材槽" z Z a r：关卡里直接画这 4 个字符，长相和互动全由素材包决定（默认：毒池 / 荆棘 / 回血泉 / 蛛网）。
/// 参考：Unity ScriptableObject 当"数据容器"、让设计师在 Inspector 里配数据而不改代码（https://docs.unity3d.com/Manual/class-ScriptableObject.html ）；
/// 半颗心 = 伤害按"半格"记账、显示成半颗心（塞尔达式心容器 https://zelda.fandom.com/wiki/Heart_Container ）。
/// H9：持续伤害一定有间隔（≥0.3 秒）、区域宽度 ≤3 格（能走出来）；H3：掉血前区域本身一直看得见（素材就是预兆）；H4：马里奥心智不读这里。
/// </summary>
public static class ArtKitRules
{
    /// <summary>一份互动。damageHalves：每次掉几个"半格"（1 = 半颗心，2 = 一颗心，负数 = 回血）。</summary>
    [System.Serializable]
    public struct Behavior
    {
        public bool enabled;
        public int damageHalves;
        public float tickSeconds;
        public float speedScale;
        public float stunSeconds;
        public bool hitsMario, hitsYou;
        public static Behavior None => new Behavior { enabled = false, tickSeconds = 1f, speedScale = 1f, hitsMario = true, hitsYou = true };
    }

    public const float MinTick = 0.3f, MinSpeed = 0.2f, MaxStun = 1.5f;
    public const int MaxHalves = 6;

    /// <summary>夹到安全范围（H9：间隔 ≥0.3 秒、晕 ≤1.5 秒、减速不低于 0.2 倍、一次最多 3 颗心）。</summary>
    public static Behavior Clamp(Behavior b)
    {
        if (b.tickSeconds < MinTick) b.tickSeconds = MinTick;
        if (b.speedScale <= 0f) b.speedScale = 1f;
        if (b.speedScale < MinSpeed) b.speedScale = MinSpeed; if (b.speedScale > 1f) b.speedScale = 1f;
        if (b.stunSeconds < 0f) b.stunSeconds = 0f; if (b.stunSeconds > MaxStun) b.stunSeconds = MaxStun;
        if (b.damageHalves > MaxHalves) b.damageHalves = MaxHalves; if (b.damageHalves < -MaxHalves) b.damageHalves = -MaxHalves;
        return b;
    }

    /// <summary>
    /// 半格记账：pending = 这个人身上攒着的半格（0 或 1）。加上这次的 halves，返回"现在要真的扣几颗心"（负 = 回几颗），pending 留下零头。
    /// 例：每次半格 → 第 1 次 pending=1（头上显示半颗心），第 2 次扣 1 颗、pending=0。
    /// </summary>
    public static int Accumulate(ref int pending, int halves)
    {
        int total = pending + halves;
        int hearts = total / 2; // C# 向零取整：-3/2 = -1
        pending = total - hearts * 2;
        return hearts;
    }

    /// <summary>给人看的一句话："每 1 秒掉半格心 · 减速到 0.6 倍 · 马里奥和你都会中"。</summary>
    public static string Describe(Behavior b)
    {
        if (!b.enabled) return "只换长相，没有互动";
        var parts = new List<string>();
        string t = b.tickSeconds.ToString("0.##");
        if (b.damageHalves > 0) parts.Add($"每 {t} 秒掉{HalvesText(b.damageHalves)}心");
        else if (b.damageHalves < 0) parts.Add($"每 {t} 秒回{HalvesText(-b.damageHalves)}心");
        if (b.speedScale < 0.999f) parts.Add($"减速到 {b.speedScale:0.##} 倍");
        if (b.stunSeconds > 0f) parts.Add($"每次晕 {b.stunSeconds:0.##} 秒");
        if (parts.Count == 0) parts.Add("进去没有效果");
        parts.Add(b.hitsMario && b.hitsYou ? "马里奥和你都会中" : b.hitsMario ? "只有马里奥会中" : b.hitsYou ? "只有你会中" : "谁都不会中");
        return string.Join(" · ", parts);
    }

    public static string HalvesText(int h) => h == 1 ? "半格" : h % 2 == 0 ? $" {h / 2} 格" : $" {h / 2} 格半";

    // ═════ 4 个素材槽（关卡字符 z Z a r）═════
    public static readonly char[] SlotChars = { 'z', 'Z', 'a', 'r' };
    public static string SlotKey(int i) => "ArtSlot" + (i + 1);
    public static int SlotIndex(string key) { if (key == null || !key.StartsWith("ArtSlot") || !int.TryParse(key.Substring(7), out int n) || n < 1 || n > SlotChars.Length) return -1; return n - 1; }
    public static int SlotOf(char c) { for (int i = 0; i < SlotChars.Length; i++) if (SlotChars[i] == c) return i; return -1; }

    /// <summary>素材槽的默认长相（素材包没拖图时用哪张内置图）和默认名字。</summary>
    public static readonly string[] SlotDefaultArt = { "PoisonPool", "Bush", "FxSplash", "DecoWeb" };
    public static readonly string[] SlotDefaultName = { "毒池（掉半格）", "荆棘（掉一格）", "回血泉", "蛛网（减速）" };

    public static Behavior SlotDefault(int i)
    {
        switch (i)
        {
            case 0: return new Behavior { enabled = true, damageHalves = 1, tickSeconds = 1.2f, speedScale = 0.6f, hitsMario = true, hitsYou = true };
            case 1: return new Behavior { enabled = true, damageHalves = 2, tickSeconds = 1.6f, speedScale = 1f, stunSeconds = 0.25f, hitsMario = true, hitsYou = true };
            case 2: return new Behavior { enabled = true, damageHalves = -1, tickSeconds = 2f, speedScale = 1f, hitsMario = true, hitsYou = true };
            case 3: return new Behavior { enabled = true, damageHalves = 0, tickSeconds = 1f, speedScale = 0.45f, hitsMario = true, hitsYou = true };
        }
        return Behavior.None;
    }

    /// <summary>换图台里的分组：每个名字属于哪一组（房间元素 / 角色 / 地形 / 小镇 / 装饰 / 特效 / 徽章 / 素材槽）。</summary>
    public static string GroupOf(string key)
    {
        if (key == null) return "其他";
        if (key.StartsWith("ArtSlot")) return "素材槽（关卡字符 z Z a r）";
        if (key.StartsWith("Hero") || key.StartsWith("Imp")) return "角色帧";
        if (key.StartsWith("Badge")) return "徽章";
        if (key.StartsWith("Deco")) return "房间装饰";
        if (key.StartsWith("Fx")) return "天气 · 技能特效";
        if (key.Length > 1 && key[0] == 'T' && char.IsUpper(key[1]) && key != "Tripwire") return "小镇";
        if (key == "GroundTop" || key == "GroundFill" || key == "Wall" || key == "Platform" || key == "StoneTop" || key == "MossWall") return "地形图块";
        return "房间元素";
    }

    /// <summary>PNG 文件名 → 名字（批量导入：文件名 = 名字，不分大小写；允许 "PoisonPool_v2.png"、"poisonpool@2x.png"）。null = 对不上。</summary>
    public static string MatchFile(string fileName, IEnumerable<string> keys)
    {
        if (string.IsNullOrEmpty(fileName)) return null;
        string n = fileName; int dot = n.LastIndexOf('.'); if (dot > 0) n = n.Substring(0, dot);
        n = n.ToLowerInvariant();
        string best = null;
        foreach (var k in keys)
        {
            string kl = k.ToLowerInvariant();
            if (n == kl) return k;
            if ((n.StartsWith(kl + "_") || n.StartsWith(kl + "@") || n.StartsWith(kl + "-")) && (best == null || k.Length > best.Length)) best = k;
        }
        return best;
    }
}
