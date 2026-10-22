using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// S237：开始页「上次做到哪」——纯逻辑（sim 能测），编辑器只负责把字符串存进 EditorPrefs、读文件。
/// 用户诉求：打开 Unity 一眼看到「最近在改哪一关 / 哪个小镇、上次体检结果、上次测试、上次试玩」，点一下接着做。
/// 记录格式：一行一条「种类|名字|yyyy-MM-dd HH:mm」，最新的在最上面，同一种类同名只留最新一条。
/// </summary>
public static class RecentWork
{
    public const int Max = 6;
    public const string Room = "关卡", Town = "小镇";

    public sealed class Item { public string kind = "", name = ""; public DateTime time; }

    public static List<Item> Parse(string stored)
    {
        var list = new List<Item>();
        foreach (var line in (stored ?? "").Replace("\r", "").Split('\n'))
        {
            var p = line.Split('|');
            if (p.Length < 3 || p[1].Trim().Length == 0) continue;
            if (!DateTime.TryParseExact(p[2].Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) continue;
            list.Add(new Item { kind = p[0].Trim(), name = p[1].Trim(), time = t });
        }
        return list;
    }

    /// <summary>记一条：同种类同名的旧记录删掉，新的放最上面，最多留 max 条。名字里的 | 和换行换成空格。</summary>
    public static string Push(string stored, string kind, string name, DateTime when, int max = Max)
    {
        name = (name ?? "").Replace('|', ' ').Replace('\n', ' ').Replace('\r', ' ').Trim();
        var list = Parse(stored);
        if (name.Length == 0) return Join(list);
        list.RemoveAll(i => i.kind == kind && i.name == name);
        list.Insert(0, new Item { kind = kind, name = name, time = new DateTime(when.Year, when.Month, when.Day, when.Hour, when.Minute, 0) });
        return Join(list.Take(Math.Max(1, max)));
    }

    public static Item Latest(string stored, string kind) => Parse(stored).FirstOrDefault(i => i.kind == kind);

    static string Join(IEnumerable<Item> items) => string.Join("\n", items.Select(i => $"{i.kind}|{i.name}|{i.time:yyyy-MM-dd HH:mm}"));

    /// <summary>「3 分钟前 / 2 小时前 / 昨天 / 10-08」——让人一眼知道多久没碰了。</summary>
    public static string Ago(DateTime then, DateTime now)
    {
        var d = now - then;
        if (d.TotalMinutes < 1) return "刚刚";
        if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes} 分钟前";
        if (d.TotalHours < 24 && then.Date == now.Date) return $"{(int)d.TotalHours} 小时前";
        if (then.Date == now.Date.AddDays(-1)) return "昨天 " + then.ToString("HH:mm");
        return then.ToString("MM-dd HH:mm");
    }

    /// <summary>体检报告（HealthCheck.md）的结论行：「✓ 没有必须改的（2 个提醒）」或「3 个必须改，1 个提醒」+ 前几条 ✗。</summary>
    public static string HealthLine(string healthMd, out bool bad)
    {
        bad = false;
        if (string.IsNullOrEmpty(healthMd)) return "还没体检过";
        var lines = healthMd.Replace("\r", "").Split('\n');
        var head = lines.FirstOrDefault(l => l.StartsWith("**")) ?? "";
        head = head.Trim('*', ' ');
        var errs = lines.Where(l => l.StartsWith("✗ ")).Take(2).Select(l => l.Substring(2)).ToList();
        bad = errs.Count > 0 || head.Contains("必须改，");
        return (head.Length > 0 ? head : "（没读到结论）") + (errs.Count > 0 ? "：" + string.Join("；", errs) : "");
    }

    /// <summary>TestReport.txt 的「通过 / 失败」数（读报告头部的统计框）。</summary>
    public static string TestLine(string report, out bool bad)
    {
        bad = false;
        if (string.IsNullOrEmpty(report)) return "还没跑过 EditMode 测试";
        int Num(string key)
        {
            var l = report.Replace("\r", "").Split('\n').FirstOrDefault(x => x.Contains(key));
            if (l == null) return -1;
            var digits = new string(l.Substring(l.IndexOf(key, StringComparison.Ordinal) + key.Length).SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out int n) ? n : -1;
        }
        int pass = Num("通过:"), fail = Num("失败:");
        if (pass < 0 && fail < 0) return "（报告格式看不懂，打开 TestReport.txt 看）";
        bad = fail > 0;
        return fail > 0 ? $"✗ {fail} 个没通过（{Math.Max(0, pass)} 个通过）—— 把 TestReport.txt 发给 AI" : $"✓ {pass} 个全部通过";
    }

    /// <summary>step1_rounds.csv 最后一局：谁赢、几秒、模式（room / quick / f9 / town）。</summary>
    public static string LastRoundLine(string csv)
    {
        var rs = Step1ExitReport.Parse(csv);
        if (rs.Count == 0) return "还没有试玩记录";
        var r = rs[rs.Count - 1];
        string who = r.MarioWon ? "马里奥赢" : "你赢";
        string mode = r.mode == "f9" ? "F9 测试" : r.mode == "quick" ? "快速测试" : r.mode == "town" ? "小镇里的房间" : "认真玩";
        return $"{r.time:MM-dd HH:mm} {who}（{r.seconds:0} 秒，{mode}），一共 {rs.Count} 局";
    }
}
