using System;
using System.Collections.Generic;
using System.IO;

internal static class Program
{
    private static int assertions;

    private static int Main()
    {
        try
        {
            ParseAndEncoding();
            ValidationAndDrafts();
            WinLoseEvidenceAndTravel();
            BudgetAndInvalidAction();
            DemoResource();
            Console.WriteLine("PASS WorldPark .NET pure-logic harness: " + assertions + " assertions");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL WorldPark .NET pure-logic harness after " + assertions + " assertions: " + ex);
            return 1;
        }
    }

    private static void ParseAndEncoding()
    {
        var pack = Build();
        pack.name = "引号 \"、换行\n、反斜杠 \\ 和控制符 \u0001";
        string json = WorldParkPack.ToJson(pack);
        var parsed = WorldParkPack.Parse(json, out string error);
        Check(parsed != null, error);
        Equal(pack.name, parsed.name, "JSON 字符串转义往返");
        Check(WorldParkPack.Parse("{\"type\":\"mariotrickster-worldpark\\q\",\"v\":1,\"venues\":[],\"links\":[]}", out error) == null && error.Contains("转义"), "非法转义被拒绝");
        Check(WorldParkPack.Parse("{\"type\":\"mariotrickster-worldpark\",\"v\":1,\"venues\":[{\"nodes\":[3],\"clues\":[]}],\"links\":[]}", out error) == null && error.Contains("nodes[0]"), "坏节点结构被拒绝");
    }

    private static void ValidationAndDrafts()
    {
        var pack = Build();
        pack.venues.Add(new WorldParkPack.Venue
        {
            id = "train", name = "列车概念", setting = "原创列车", role = "trickster", suit = "草案", difficulty = 2, rules = "待适配", entry = "concept",
            nodes = new List<WorldParkPack.Node> { new WorldParkPack.Node { id = "concept", kind = "concept", name = "车门" } }, clues = new List<WorldParkPack.Clue>()
        });
        pack.links.Add(new WorldParkPack.Link { from = "venue_1", to = "train", requires = "clear" });
        var issues = WorldParkPack.Validate(pack, room => room == "默认房间" || room == "默认恶作剧房间");
        Check(Has(issues, "warn", "unsupported_concept"), "concept 标注待适配");
        Check(!HasSeverity(issues, "error"), "概念草案不制造结构错误");
        pack.venues[0].nodes[1].onLose = "ghost";
        issues = WorldParkPack.Validate(pack, room => room == "默认房间" || room == "默认恶作剧房间");
        Check(Has(issues, "error", "room_lose_missing"), "悬空房间分支被检查");
    }

    private static void WinLoseEvidenceAndTravel()
    {
        var pack = Build();
        var win = WorldParkPack.Start(pack, out string error);
        Check(win != null, error);
        Check(win.found.Contains("venue_1/blue"), "进入线索来源即记录 found");
        Check(!win.revealed.Contains("venue_1/blue"), "未到 reveal 前不记录 truth");
        Check(!Contains(win.log, "作者秘密"), "日志不泄漏作者真相");
        Check(win.Continue(out error), error);
        Check(win.ResolveRoom(true, out error), error);
        Equal("win_reveal", win.Current.id, "胜利走胜利揭示");
        Check(win.revealed.Contains("venue_1/blue"), "进入 reveal 后记录 truth 已揭示");
        Check(win.Continue(out error), error);
        Equal("clear", win.results["venue_1"], "clear 结局记录本地结果");
        Check(win.CanTravel("venue_2"), "clear 可通过会场链接");
        Check(win.Travel("venue_2", out error), error);
        Equal("arrival", win.Current.id, "Travel 进入目标会场 entry");

        var lose = WorldParkPack.Start(pack, out error);
        Check(lose.Continue(out error), error);
        Check(lose.ResolveRoom(false, out error), error);
        Equal("lose_reveal", lose.Current.id, "失败走失败揭示");
        Check(lose.revealed.Contains("venue_1/red"), "失败路线只揭示对应线索");
        Check(lose.Continue(out error), error);
        Equal("fail", lose.results["venue_1"], "fail 结局记录本地结果");
        Check(!lose.CanTravel("venue_2"), "fail 不满足 clear 链接");
    }

    private static void BudgetAndInvalidAction()
    {
        var pack = Build();
        var session = WorldParkPack.Start(pack, out string error);
        Check(!session.ResolveRoom(true, out error), "非 room 不能结算");
        Equal("intro", session.Current.id, "无效操作不推进 stage");
        pack.venues[0].nodes[1].onWin = "room_1";
        pack.venues[0].nodes[1].onLose = "win_reveal";
        pack.venues[0].clues.RemoveAll(c => c.revealAt == "lose_reveal");
        session = WorldParkPack.Start(pack, out error);
        Check(session.Continue(out error), error);
        for (int i = 0; i < 254; i++) Check(session.ResolveRoom(true, out error), error);
        Check(!session.ResolveRoom(true, out error) && error.Contains("256"), "循环在 256 步预算停止");
    }

    private static void DemoResource()
    {
        string path = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Assets", "Resources", "WorldPark", "Demo.worldpark.json"));
        var pack = WorldParkPack.Parse(File.ReadAllText(path), out string error);
        Check(pack != null, "样例可 Parse：" + error);
        var issues = WorldParkPack.Validate(pack, room => room == "默认房间" || room == "默认恶作剧房间");
        Check(!HasSeverity(issues, "error"), "样例没有结构错误");
        var session = WorldParkPack.Start(pack, out error);
        Check(session != null, "样例 entryVenue 可开始：" + error);
    }

    private static WorldParkPack.Pack Build()
    {
        return new WorldParkPack.Pack
        {
            id = "test", name = "测试", premise = "原创", entryVenue = "venue_1",
            venues = new List<WorldParkPack.Venue>
            {
                new WorldParkPack.Venue
                {
                    id = "venue_1", name = "入口", setting = "原创广场", role = "trickster", suit = "自定义", difficulty = 1, rules = "守宝", entry = "intro",
                    nodes = new List<WorldParkPack.Node>
                    {
                        new WorldParkPack.Node { id = "intro", kind = "story", next = "room_1" },
                        new WorldParkPack.Node { id = "room_1", kind = "room", room = "默认房间", onWin = "win_reveal", onLose = "lose_reveal" },
                        new WorldParkPack.Node { id = "win_reveal", kind = "reveal", next = "clear_end" },
                        new WorldParkPack.Node { id = "lose_reveal", kind = "reveal", next = "fail_end" },
                        new WorldParkPack.Node { id = "clear_end", kind = "ending", result = "clear" },
                        new WorldParkPack.Node { id = "fail_end", kind = "ending", result = "fail" }
                    },
                    clues = new List<WorldParkPack.Clue>
                    {
                        new WorldParkPack.Clue { id = "blue", foundAt = "intro", revealAt = "win_reveal", hint = "蓝色", truth = "作者秘密蓝色" },
                        new WorldParkPack.Clue { id = "red", foundAt = "intro", revealAt = "lose_reveal", hint = "红色", truth = "作者秘密红色" }
                    }
                },
                new WorldParkPack.Venue
                {
                    id = "venue_2", name = "后续", setting = "原创休息站", role = "trickster", suit = "自定义", difficulty = 1, rules = "连接", entry = "arrival",
                    nodes = new List<WorldParkPack.Node> { new WorldParkPack.Node { id = "arrival", kind = "ending", result = "clear" } }, clues = new List<WorldParkPack.Clue>()
                }
            },
            links = new List<WorldParkPack.Link> { new WorldParkPack.Link { from = "venue_1", to = "venue_2", requires = "clear" } }
        };
    }

    private static bool Has(List<WorldParkPack.Issue> issues, string severity, string code) { foreach (var issue in issues) if (issue.severity == severity && issue.code == code) return true; return false; }
    private static bool HasSeverity(List<WorldParkPack.Issue> issues, string severity) { foreach (var issue in issues) if (issue.severity == severity) return true; return false; }
    private static bool Contains(List<string> lines, string text) { foreach (string line in lines) if (line.Contains(text)) return true; return false; }
    private static void Check(bool condition, string message) { assertions++; if (!condition) throw new InvalidOperationException(message); }
    private static void Equal(string expected, string actual, string message) { Check(expected == actual, message + "；预期=" + expected + "，实际=" + actual); }
}
