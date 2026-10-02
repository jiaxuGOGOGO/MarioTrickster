using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

/// <summary>
/// S232：调参"讲道理"检查——一张规则表，Unity 体检和网页设计台同时用（build.py 把 Rules 抄进网页，网页 tuAudit 用同一张表算）。
/// 为什么：数值散在 240+ 个字段里，单看每个都"合理"，放在一起可能互相打架（比如追你的速度比你还快 → 永远甩不掉，违反宪法 A2"每种优势都有反制"）。
/// 规则来源：Ian Schreiber《Game Balance Concepts》Level 2（数值之间的关系比单个数值重要）；Luban / 网易雷火策划表工具（表结构当契约，填错立刻在表上标红）。
/// 写法："左 运算 右 | 为什么"。左右可以是字段名或数字，可以带 *倍数（例：overworldChaseSpeed &lt; overworldTricksterSpeed）。
/// 只检查、不改值；不通过 = 体检里一条 ⚠ + 原因。新加数值时：如果它和别的数值有"必须大于 / 小于"的关系，就在这里加一行。
/// </summary>
public static class TuningAudit
{
    public static readonly string[] Rules =
    {
        "overworldMarioSpeed < overworldTricksterSpeed | 小镇里你要比他快，才能抄近路先到门口埋伏",
        "overworldChaseSpeed < overworldTricksterSpeed | 他追你时要比你慢一点：能甩掉，但要跑（每种优势都有反制）",
        "overworldChaseSpeed > overworldMarioSpeed | 追人时要比平时快，否则'被发现'没有压力",
        "overworldNightVisionRange < overworldVisionRange | 晚上要比白天看得近（路灯才有意义）",
        "overworldGrassSeeRadius < overworldVisionRange | 高草里要比空地难发现",
        "overworldNearSense < overworldGrassSeeRadius | 贴身察觉要比高草里更近",
        "overworldSlipStunSeconds <= maxStunSeconds | 晕眩不能超过总上限（H9：任何控制都有结束）",
        "overworldBigStunSeconds <= maxStunSeconds | 大机关晕眩不能超过总上限（H9）",
        "overworldKoSeconds <= maxStunSeconds | 心掉光的晕倒不能超过总上限（H9）",
        "overworldBoltTelegraphSeconds >= 0.8 | 闪电预警至少 0.8 秒（人看见 → 反应 0.2–0.4 秒 → 跑出 1 格）",
        "overworldBigFuseSeconds >= 0.8 | 大机关预警至少 0.8 秒（H3：先预警后发动）",
        "overworldHurtGraceSeconds >= 1 | 挨打后至少 1 秒无敌（防连控，S221）",
        "overworldCloudVolleySeconds < overworldCloudSeconds | 雷云在的时间里至少劈一轮",
        "overworldExitGraceSeconds >= 1.5 | 他出门后清点至少 1.5 秒，你有时间走开（玩家模拟：反应慢的人每次出门都被抓）",
        "overworldLateWindowSeconds >= 3 | 迟到窗口至少 3 秒，不然跟进门几乎不可能",
        "chaseSpeedScale > marioSpeedScale | 房间里追人时比平时快",
        "chaseSpeedScale < 0.9 | 房间里追人时仍比你慢（你 8 格/秒，他 9×倍数）",
        "curiousThreshold < alertThreshold | 先 '?' 后 '!'（H2：识破前必有预兆）",
        "alertThreshold <= maxSuspicion | 起疑上限要能到 '!'",
        "startDelaySeconds >= 2 | 开局至少站 2 秒，给你就位（S183 反馈）",
        "pickupEffectSeconds <= 10 | 道具效果不超过 10 秒（一局才 60 秒左右）",
    };

    public sealed class Result { public string rule = "", why = ""; public bool ok; public double left, right; public string detail = ""; }

    public static bool TryValue(object tuning, string token, out double v)
    {
        token = token.Trim(); double mul = 1;
        int star = token.IndexOf('*');
        if (star > 0) { if (!double.TryParse(token.Substring(star + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out mul)) { v = 0; return false; } token = token.Substring(0, star).Trim(); }
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) { v *= mul; return true; }
        var f = tuning?.GetType().GetField(token, BindingFlags.Public | BindingFlags.Instance);
        if (f == null) { v = 0; return false; }
        object o = f.GetValue(tuning);
        if (o is float fl) v = fl; else if (o is int i) v = i; else if (o is bool b) v = b ? 1 : 0; else { v = 0; return false; }
        v *= mul; return true;
    }

    public static List<Result> Check(object tuning)
    {
        var res = new List<Result>();
        foreach (var line in Rules)
        {
            var parts = line.Split('|'); string expr = parts[0].Trim(), why = parts.Length > 1 ? parts[1].Trim() : "";
            string op = expr.Contains("<=") ? "<=" : expr.Contains(">=") ? ">=" : expr.Contains("<") ? "<" : ">";
            int i = expr.IndexOf(op, StringComparison.Ordinal);
            var r = new Result { rule = expr, why = why };
            bool a = TryValue(tuning, expr.Substring(0, i), out r.left), b = TryValue(tuning, expr.Substring(i + op.Length), out r.right);
            if (!a || !b) { r.ok = false; r.detail = "规则里的名字找不到（字段改名了？）"; res.Add(r); continue; }
            r.ok = op == "<" ? r.left < r.right : op == "<=" ? r.left <= r.right : op == ">" ? r.left > r.right : r.left >= r.right;
            r.detail = $"{F(r.left)} {op} {F(r.right)}";
            res.Add(r);
        }
        return res;
    }

    public static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    public static string Markdown(object tuning)
    {
        var rs = Check(tuning); int bad = 0; var sb = new System.Text.StringBuilder();
        foreach (var r in rs) if (!r.ok) { bad++; sb.AppendLine($"⚠ {r.rule}（现在 {r.detail}）：{r.why}"); }
        return (bad == 0 ? $"✓ 数值之间的 {rs.Count} 条关系都对（追人比你慢、晚上看得近、预警够长……）\n" : $"{bad} / {rs.Count} 条数值关系不对：\n") + sb;
    }
}
