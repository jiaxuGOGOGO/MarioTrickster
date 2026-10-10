using System;
using UnityEngine;

/// <summary>
/// S193：连招"手感"纯逻辑（可测试）。借格斗游戏的四条规则，只借规则不借素材：
///   1. 顿帧 hitstop：命中瞬间整个画面停 1–4 帧，段数越高停得越久 → "打中了"的重量感；
///   2. 受击硬直 hitstun + 递减 damage scaling：连招越长，每段追加的晕眩越少（防无限控，宪法 H9）；
///   3. 连招段位名：x2 连击 → x3 漂亮 → x4 疯狂 → x5+ 大闹剧，颜色逐级变暖；
///   4. 不同机关算"不同招"：同一种机关连用不加分（参考"多样性 > 重复"的连招评价），鼓励换招（宪法支柱 1 / P1）。
/// 来源：Celia Wagar《Stunning Detail: Hitstun in Depth》（hitstop / hitstun / 递减），
///       bb010g《Thoughts on combos》（variety / length / paths）。
/// 所有数字来自 MarioMindTuningSO（宪法 §6）。
/// </summary>
public static class Step1ComboFeel
{
    /// <summary>第 n 段连招的顿帧时长（秒，真实时间）。n=1 也有一点点，让单次命中也"有声音"。</summary>
    public static float HitstopSeconds(int n, float baseSeconds, float perStep, float cap)
    {
        if (n <= 0 || baseSeconds <= 0f) return 0f;
        return Mathf.Min(cap, baseSeconds + perStep * (n - 1));
    }

    /// <summary>第 n 段追加晕眩（递减）：bonus × scale^(n-2)，n≥2 才有。</summary>
    public static float BonusStun(int n, float bonus, float scale)
    {
        if (n < 2 || bonus <= 0f) return 0f;
        return bonus * Mathf.Pow(Mathf.Clamp01(scale), n - 2);
    }

    /// <summary>屏幕震动幅度（世界单位）。</summary>
    public static float ShakeAmplitude(int n, float perStep, float cap) => n <= 0 ? 0f : Mathf.Min(cap, perStep * n);

    /// <summary>连招段位名（中英）。</summary>
    public static string TierName(int n)
    {
        if (n >= 5) return "大闹剧！ SLAPSTICK!";
        if (n == 4) return "疯狂！ CRAZY!";
        if (n == 3) return "漂亮！ NICE!";
        if (n == 2) return "连击！ COMBO!";
        return "";
    }

    /// <summary>段位颜色（十六进制，富文本用）。</summary>
    public static string TierColor(int n)
    {
        if (n >= 5) return "#FF4FD8";
        if (n == 4) return "#FF5A3C";
        if (n == 3) return "#FF9A2E";
        return "#FFD24A";
    }

    /// <summary>
    /// S240：坑到他的"原因"中文名（显示用；kind 本身不改——计分、反应表、试玩记录都靠它）。
    /// cause 为空时按 kind 给默认名；hurt 又细分成 炮弹 / 爆炸 / 火（由谁先报到决定，见 Step1Combo）。
    /// </summary>
    public static string CauseName(string kind, string cause = null)
    {
        if (!string.IsNullOrEmpty(cause)) return cause;
        switch (kind)
        {
            case "hurt": return "火";
            case "trip": return "绊线";
            case "slip": return "香蕉皮";
            case "launch": return "弹簧";
            case "cage": return "铁笼";
            case "snare": return "绳套";
            case "drop": return "掉下一层";
            case "pit": return "掉坑";
            case "stop": return "被墙挡住";
            default: return string.IsNullOrEmpty(kind) ? "?" : kind;
        }
    }

    /// <summary>S240：连招弹窗的一行"怎么来的"：炮弹 → 香蕉皮 → 火（最多显示最后 maxShown 个，前面用 … 省略）。</summary>
    public static string ChainText(System.Collections.Generic.IList<string> causes, int maxShown = 4)
    {
        if (causes == null || causes.Count == 0) return "";
        int from = Math.Max(0, causes.Count - maxShown);
        var parts = new System.Collections.Generic.List<string>();
        if (from > 0) parts.Add("…");
        for (int i = from; i < causes.Count; i++) parts.Add(causes[i]);
        return string.Join(" → ", parts);
    }

    /// <summary>S240：连击窗口还剩多少（0..1）。1 = 刚坑到，0 = 窗口关了（再坑就从 1 重新数）。</summary>
    public static float WindowLeft01(float now, float lastHit, float window) =>
        window <= 0f ? 0f : Mathf.Clamp01(1f - (now - lastHit) / window);

    /// <summary>
    /// 连招分：每段基础 10 × 段数；换了一种机关（本段 kind 与上一段不同）再 +5；
    /// 同一种机关连用只算基础分的一半（鼓励换招）。
    /// </summary>
    public static int StepScore(int n, string kind, string previousKind)
    {
        int basePts = 10 * Math.Max(1, n);
        if (n <= 1 || string.IsNullOrEmpty(previousKind)) return basePts;
        return kind == previousKind ? basePts / 2 : basePts + 5;
    }
}
