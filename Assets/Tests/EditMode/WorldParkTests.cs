using System;
using System.Collections.Generic;
using NUnit.Framework;

public class WorldParkTests
{
    [Test]
    public void ToJsonRoundTripsEscapedTextAndNullLists()
    {
        var pack = PlayablePack();
        pack.name = "引号 \"、换行\n、反斜杠 \\ 和控制符 \u0001";
        pack.venues[0].nodes[0].text = "不会丢失的\t文本";

        string json = WorldParkPack.ToJson(pack);
        var parsed = WorldParkPack.Parse(json, out string error);
        Assert.IsNotNull(parsed, error);
        Assert.AreEqual(pack.name, parsed.name);
        Assert.AreEqual(pack.venues[0].nodes[0].text, parsed.venues[0].nodes[0].text);

        var emptyLists = new WorldParkPack.Pack
        {
            id = "empty_lists",
            name = "草稿",
            premise = "",
            entryVenue = "",
            venues = null,
            links = null
        };
        var parsedEmptyLists = WorldParkPack.Parse(WorldParkPack.ToJson(emptyLists), out error);
        Assert.IsNotNull(parsedEmptyLists, error);
        Assert.IsNotNull(parsedEmptyLists.venues);
        Assert.IsNotNull(parsedEmptyLists.links);
        Assert.AreEqual(0, parsedEmptyLists.venues.Count);
        Assert.AreEqual(0, parsedEmptyLists.links.Count);
    }

    [Test]
    public void ParseRejectsWrongTagVersionMalformedEscapeAndNonObjectNode()
    {
        Assert.IsNull(WorldParkPack.Parse("{\"type\":\"other\",\"v\":1,\"venues\":[],\"links\":[]}", out string wrongTag));
        StringAssert.Contains("type", wrongTag);

        Assert.IsNull(WorldParkPack.Parse("{\"type\":\"mariotrickster-worldpark\",\"v\":2,\"venues\":[],\"links\":[]}", out string wrongVersion));
        StringAssert.Contains("v", wrongVersion);

        Assert.IsNull(WorldParkPack.Parse("{\"type\":\"mariotrickster-worldpark\\q\",\"v\":1,\"venues\":[],\"links\":[]}", out string badEscape));
        StringAssert.Contains("转义", badEscape);

        const string nodeIsNotObject = "{\"type\":\"mariotrickster-worldpark\",\"v\":1,\"venues\":[{\"nodes\":[3],\"clues\":[]}],\"links\":[]}";
        Assert.IsNull(WorldParkPack.Parse(nodeIsNotObject, out string badNode));
        StringAssert.Contains("nodes[0]", badNode);
    }

    [Test]
    public void ValidateSeparatesDraftAdaptationWarningsFromStructuralErrors()
    {
        var pack = PlayablePack();
        pack.venues.Add(new WorldParkPack.Venue
        {
            id = "venue_train",
            name = "列车构想",
            setting = "原创环线列车会场草案",
            role = "trickster",
            suit = "自定义",
            difficulty = 2,
            rules = "尚未实现的玩法只作创作说明",
            entry = "train_concept",
            nodes = new List<WorldParkPack.Node>
            {
                new WorldParkPack.Node { id = "train_concept", kind = "concept", name = "列车试作", text = "等待新增列车玩法适配器" }
            },
            clues = new List<WorldParkPack.Clue>()
        });
        pack.links.Add(new WorldParkPack.Link { from = "venue_1", to = "venue_train", requires = "clear" });
        pack.venues[1].role = "observer";

        List<WorldParkPack.Issue> issues = WorldParkPack.Validate(pack, room => room == "默认房间");
        Assert.IsTrue(HasIssue(issues, "warn", "unsupported_concept"));
        Assert.IsTrue(HasIssue(issues, "warn", "unsupported_role"));
        Assert.IsFalse(HasSeverity(issues, "error"), Describe(issues));

        pack.venues[0].nodes[1].onLose = "missing_node";
        pack.venues[0].clues[0].revealAt = "room_1";
        issues = WorldParkPack.Validate(pack, room => room == "默认房间");
        Assert.IsTrue(HasIssue(issues, "error", "room_lose_missing"), Describe(issues));
        Assert.IsTrue(HasIssue(issues, "error", "clue_reveal_kind"), Describe(issues));
    }

    [Test]
    public void WinAndLoseFollowDifferentStagesWithoutLeakingAuthorTruth()
    {
        var pack = PlayablePack();

        var win = WorldParkPack.Start(pack, out string error);
        Assert.IsNotNull(win, error);
        Assert.AreEqual("intro", win.Current.id);
        Assert.IsTrue(win.found.Contains("venue_1/blue_stamp"));
        Assert.IsFalse(win.revealed.Contains("venue_1/blue_stamp"));
        AssertLogDoesNotContain(win, "作者秘密");

        Assert.IsTrue(win.Continue(out error), error);
        Assert.AreEqual("room_1", win.Current.id);
        Assert.IsTrue(win.ResolveRoom(true, out error), error);
        Assert.AreEqual("win_reveal", win.Current.id);
        Assert.IsTrue(win.revealed.Contains("venue_1/blue_stamp"));
        Assert.IsFalse(win.revealed.Contains("venue_1/red_stamp"));
        AssertLogDoesNotContain(win, "作者秘密");
        Assert.IsTrue(win.Continue(out error), error);
        Assert.AreEqual("clear_end", win.Current.id);
        Assert.AreEqual("clear", win.results["venue_1"]);
        Assert.IsTrue(win.CanTravel("venue_2"));
        Assert.IsTrue(win.Travel("venue_2", out error), error);
        Assert.AreEqual("arrival", win.Current.id);

        var lose = WorldParkPack.Start(pack, out error);
        Assert.IsNotNull(lose, error);
        Assert.IsTrue(lose.Continue(out error), error);
        Assert.IsTrue(lose.ResolveRoom(false, out error), error);
        Assert.AreEqual("lose_reveal", lose.Current.id);
        Assert.IsTrue(lose.revealed.Contains("venue_1/red_stamp"));
        Assert.IsFalse(lose.revealed.Contains("venue_1/blue_stamp"));
        Assert.IsTrue(lose.Continue(out error), error);
        Assert.AreEqual("fail", lose.results["venue_1"]);
        Assert.IsFalse(lose.CanTravel("venue_2"));
    }

    [Test]
    public void RunnableEntryCanStartWhileUnreachableConceptDraftIsWarnedAndTravelIsBlocked()
    {
        var pack = PlayablePack();
        pack.venues.Add(new WorldParkPack.Venue
        {
            id = "venue_train",
            name = "待适配列车",
            setting = "原创列车概念",
            role = "trickster",
            suit = "创作草案",
            difficulty = 1,
            rules = "概念会场不能直接运行",
            entry = "concept",
            nodes = new List<WorldParkPack.Node>
            {
                new WorldParkPack.Node { id = "concept", kind = "concept", name = "车门", text = "等待列车玩法适配器" }
            },
            clues = new List<WorldParkPack.Clue>()
        });
        pack.links.Add(new WorldParkPack.Link { from = "venue_1", to = "venue_train", requires = "clear" });

        var session = WorldParkPack.Start(pack, out string error);
        Assert.IsNotNull(session, error);
        Assert.IsTrue(session.Continue(out error), error);
        Assert.IsTrue(session.ResolveRoom(true, out error), error);
        Assert.IsTrue(session.Continue(out error), error);
        Assert.IsFalse(session.CanTravel("venue_train"));
        Assert.IsFalse(session.Travel("venue_train", out error));
        StringAssert.Contains("适配", error);
    }

    [Test]
    public void InvalidOperationsDoNotAdvanceAndLoopStopsAtFiniteBudget()
    {
        var pack = PlayablePack();
        var session = WorldParkPack.Start(pack, out string error);
        Assert.IsNotNull(session, error);
        Assert.IsFalse(session.ResolveRoom(true, out error));
        Assert.AreEqual("intro", session.Current.id);
        Assert.IsTrue(session.Continue(out error), error);

        // 胜利分支回到同一个 room，失败分支仍可达 ending，因此结构有效但彩排不会无限循环。
        WorldParkPack.Node room = pack.venues[0].nodes[1];
        room.onWin = "room_1";
        room.onLose = "win_reveal";
        pack.venues[0].clues.RemoveAll(c => c.revealAt == "lose_reveal");
        session = WorldParkPack.Start(pack, out error);
        Assert.IsNotNull(session, error);
        Assert.IsTrue(session.Continue(out error), error);
        for (int i = 0; i < 254; i++) Assert.IsTrue(session.ResolveRoom(true, out error), error);
        Assert.IsFalse(session.ResolveRoom(true, out error));
        StringAssert.Contains("256", error);
        Assert.AreEqual("room_1", session.Current.id);
    }

    [Test]
    public void VenueEndingCannotRewriteRecordedRoomFact()
    {
        var p = PlayablePack();
        p.venues[0].nodes[2].next = "fail_end";
        var s = WorldParkPack.Start(p, out string error);
        Assert.IsTrue(s.Continue(out error), error);
        Assert.IsTrue(s.ResolveRoom(true, out error), error);
        Assert.IsTrue(s.Continue(out error), error);
        Assert.AreEqual("fail", s.venueResults["venue_1"]);
        Assert.AreEqual(1, s.roomResults.Count);
        Assert.IsTrue(s.roomResults[0].won, "作者定义的会场失败不得改写本回合守宝成功");
    }

    [Test]
    public void FailAndAnyLinksAreSelectedEvenWhenClearLinkComesFirst()
    {
        foreach (string condition in new[] { "fail", "any" })
        {
            var p = PlayablePack(); p.links.Add(new WorldParkPack.Link { from = "venue_1", to = "venue_2", requires = condition });
            var s = WorldParkPack.Start(p, out string error);
            Assert.IsTrue(s.Continue(out error), error); Assert.IsTrue(s.ResolveRoom(false, out error), error); Assert.IsTrue(s.Continue(out error), error);
            Assert.IsTrue(s.CanTravel("venue_2")); Assert.IsTrue(s.Travel("venue_2", out error), error);
        }
    }

    [Test]
    public void OptionalClueBypassIsDiagnosedAndCannotRevealUnobservedTruth()
    {
        var p = PlayablePack(); var v = p.venues[0];
        v.nodes.Add(new WorldParkPack.Node { id = "found", kind = "story", next = "win_reveal" });
        v.nodes[1].onWin = "found"; v.nodes[1].onLose = "win_reveal";
        v.clues.Clear(); v.clues.Add(new WorldParkPack.Clue { id = "optional", foundAt = "found", revealAt = "win_reveal", hint = "可选事实", truth = "作者秘密" });
        Assert.IsTrue(HasIssue(WorldParkPack.Validate(p), "warn", "clue_discovery_bypass"));
        var s = WorldParkPack.Start(p, out string error);
        Assert.IsTrue(s.Continue(out error), error); Assert.IsTrue(s.ResolveRoom(false, out error), error);
        Assert.IsFalse(s.found.Contains("venue_1/optional")); Assert.IsFalse(s.revealed.Contains("venue_1/optional"));
        AssertLogDoesNotContain(s, "作者秘密");
    }

    [Test]
    public void InvalidJsonNumbersKeysAndClueReferencesCannotStart()
    {
        var p = PlayablePack(); string json = WorldParkPack.ToJson(p);
        Assert.IsNull(WorldParkPack.Parse(json.Replace("\"v\":1", "\"v\":+1"), out string error));
        Assert.IsNull(WorldParkPack.Parse(json.Replace("\"v\":1", "\"v\":01"), out error));
        p.venues[0].clues[0].revealAt = "missing";
        Assert.IsNull(WorldParkPack.Start(p, out error));
        p = PlayablePack(); p.venues[0].clues[0].id = "unsafe/key";
        Assert.IsTrue(HasIssue(WorldParkPack.Validate(p), "error", "id_key_invalid"));
        Assert.IsNull(WorldParkPack.Start(p, out error));
    }

    private static WorldParkPack.Pack PlayablePack()
    {
        return new WorldParkPack.Pack
        {
            id = "test_park",
            name = "测试乐园",
            premise = "原创测试设定",
            entryVenue = "venue_1",
            venues = new List<WorldParkPack.Venue>
            {
                new WorldParkPack.Venue
                {
                    id = "venue_1",
                    name = "入口会场",
                    setting = "原创广场",
                    role = "trickster",
                    suit = "自定义",
                    difficulty = 1,
                    rules = "守宝房间的结果决定不同的文字阶段",
                    entry = "intro",
                    nodes = new List<WorldParkPack.Node>
                    {
                        new WorldParkPack.Node { id = "intro", kind = "story", name = "入场", text = "观察公告", next = "room_1" },
                        new WorldParkPack.Node { id = "room_1", kind = "room", name = "守宝回合", room = "默认房间", text = "沿用现有守宝房", onWin = "win_reveal", onLose = "lose_reveal" },
                        new WorldParkPack.Node { id = "win_reveal", kind = "reveal", name = "胜利揭示", text = "印记与公告相符", next = "clear_end" },
                        new WorldParkPack.Node { id = "lose_reveal", kind = "reveal", name = "失败揭示", text = "备用印记被发现", next = "fail_end" },
                        new WorldParkPack.Node { id = "clear_end", kind = "ending", name = "通过", text = "本会场结算完成", result = "clear" },
                        new WorldParkPack.Node { id = "fail_end", kind = "ending", name = "未通过", text = "本会场结算为失败", result = "fail" }
                    },
                    clues = new List<WorldParkPack.Clue>
                    {
                        new WorldParkPack.Clue { id = "blue_stamp", name = "蓝色印记", foundAt = "intro", revealAt = "win_reveal", hint = "公告角落有蓝色印记", truth = "作者秘密：蓝色印记对应胜利路线" },
                        new WorldParkPack.Clue { id = "red_stamp", name = "红色印记", foundAt = "intro", revealAt = "lose_reveal", hint = "公告背面有红色印记", truth = "作者秘密：红色印记对应失败路线" }
                    }
                },
                new WorldParkPack.Venue
                {
                    id = "venue_2",
                    name = "后续会场",
                    setting = "原创休息站",
                    role = "trickster",
                    suit = "自定义",
                    difficulty = 1,
                    rules = "仅用于连接测试",
                    entry = "arrival",
                    nodes = new List<WorldParkPack.Node>
                    {
                        new WorldParkPack.Node { id = "arrival", kind = "ending", name = "抵达", text = "抵达后续会场", result = "clear" }
                    },
                    clues = new List<WorldParkPack.Clue>()
                }
            },
            links = new List<WorldParkPack.Link>
            {
                new WorldParkPack.Link { from = "venue_1", to = "venue_2", requires = "clear" }
            }
        };
    }

    private static bool HasIssue(List<WorldParkPack.Issue> issues, string severity, string code)
    {
        foreach (WorldParkPack.Issue issue in issues)
            if (issue.severity == severity && issue.code == code) return true;
        return false;
    }

    private static bool HasSeverity(List<WorldParkPack.Issue> issues, string severity)
    {
        foreach (WorldParkPack.Issue issue in issues)
            if (issue.severity == severity) return true;
        return false;
    }

    private static string Describe(List<WorldParkPack.Issue> issues)
    {
        var pieces = new List<string>();
        foreach (WorldParkPack.Issue issue in issues) pieces.Add(issue.code + ":" + issue.text);
        return string.Join(" | ", pieces.ToArray());
    }

    private static void AssertLogDoesNotContain(WorldParkPack.Session session, string secret)
    {
        foreach (string line in session.log) StringAssert.DoesNotContain(secret, line);
    }
}
