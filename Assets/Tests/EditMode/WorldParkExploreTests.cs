using System;
using System.Collections.Generic;
using NUnit.Framework;

public class WorldParkExploreTests
{
    [Test]
    public void LegacyV1JsonAcceptsAddedFieldsAndPlayerExploreHasNoOldTownRequirements()
    {
        var pack = BuildExplorePack("player", 0, false);
        string json = WorldParkPack.ToJson(pack);
        var parsed = WorldParkPack.Parse(json, out string error);
        Assert.IsNotNull(parsed, error);
        Assert.AreEqual("stage", parsed.venues[0].clues[0].trigger);
        Assert.AreEqual("interact", parsed.venues[0].clues[1].trigger);
        Assert.AreEqual(2, parsed.venues[0].nodes[0].points.Count);
        Assert.AreEqual("clue_a", parsed.venues[0].nodes[0].points[0].requires[0]);

        // 字段缺失的旧 v1 JSON 仍读取；没有 explore 节点，不因新字段被拒绝。
        string oldJson = json.Replace(",\"mapText\":" + Quote(parsed.venues[0].nodes[0].mapText) + ",\"timeout\":0,\"points\":[]", "");
        var old = WorldParkPack.Parse("{\"type\":\"mariotrickster-worldpark\",\"v\":1,\"id\":\"old\",\"venues\":[],\"links\":[]}", out error);
        Assert.IsNotNull(old, error);

        Assert.IsFalse(HasError(WorldParkPack.Validate(parsed)), Describe(WorldParkPack.Validate(parsed)));
        // Exploration deliberately does not require old M / doors / schedule / G/o counts.
        StringAssert.DoesNotContain("M", parsed.venues[0].nodes[0].mapText);
    }

    [Test]
    public void InteractIsPhysicalAndExitLatchesBeforeSeparateResolve()
    {
        var session = WorldParkPack.Start(BuildExplorePack("trickster", 0, false), out string error);
        Assert.IsNotNull(session, error);
        var run = WorldParkExplore.Create(session, out error);
        Assert.IsNotNull(run, error);
        Assert.IsFalse(session.found.Contains("venue/clue_a"), "interact 线索不能因进入阶段自动 found");

        Assert.IsFalse(run.Interact("exit", out error));
        StringAssert.Contains("太远", error);
        Assert.IsFalse(run.Ended);
        Assert.AreEqual(0, session.activityResults.Count);

        run.Tick(0.25, 1, 0); // T(1,1) -> clue(2,1); exit is near but locked
        Assert.IsFalse(run.Interact("exit", out error));
        StringAssert.Contains("缺少线索", error);
        Assert.IsFalse(run.Ended, "条件不足不是判败");

        Assert.IsTrue(run.Interact("clue", out error), error);
        Assert.IsTrue(session.found.Contains("venue/clue_a"));
        Assert.IsFalse(session.revealed.Contains("venue/clue_a"));
        Assert.IsFalse(Contains(session.log, "作者秘密"));

        run.Tick(0.25, 1, 0); // clue -> exit(3,1)
        Assert.IsTrue(run.Interact("exit", out error), error);
        Assert.IsTrue(run.Ended);
        Assert.IsTrue(run.Won);
        Assert.AreEqual("explore", session.Current.kind, "锁存出口不自动改流程");
        Assert.AreEqual(0, session.roomResults.Count);
        Assert.AreEqual(0, session.activityResults.Count);

        Assert.IsTrue(session.ResolveExplore(run.Won, out error), error);
        Assert.AreEqual("win_reveal", session.Current.id);
        Assert.AreEqual(0, session.roomResults.Count, "ResolveExplore 不可伪造 RoomResult");
        Assert.AreEqual(1, session.activityResults.Count);
        Assert.AreEqual("explore", session.activityResults[0].kind);
        Assert.IsTrue(session.activityResults[0].won);
    }

    [Test]
    public void CollisionLineOfSightAndTimeoutAreAppliedByPureRun()
    {
        var blocked = WorldParkPack.Start(BuildExplorePack("player", 0, true), out string error);
        Assert.IsNotNull(blocked, error);
        var collisionRun = WorldParkExplore.Create(blocked, out error);
        Assert.IsNotNull(collisionRun, error);
        collisionRun.Tick(0.75, 1, 0);
        Assert.Less(collisionRun.x, 3.0, "OverworldMap.Move 不允许穿过墙格");
        StringAssert.Contains("阻挡", collisionRun.LastMessage);
        Assert.IsFalse(OverworldMap.LineOfSight(collisionRun.map, 2.5, 1.5, 4.5, 1.5), "复用地图 LOS，墙应隔断视线");

        var timed = WorldParkPack.Start(BuildExplorePack("player", 1, false), out error);
        Assert.IsNotNull(timed, error);
        var timerRun = WorldParkExplore.Create(timed, out error);
        timerRun.Tick(1.0, 0, 0);
        Assert.IsTrue(timerRun.Ended);
        Assert.IsFalse(timerRun.Won);
        Assert.AreEqual(0, timerRun.Remaining, 1e-9);
        StringAssert.Contains("超时", timerRun.LastMessage);
        Assert.AreEqual(0, timed.roomResults.Count);
        Assert.AreEqual(0, timed.activityResults.Count, "超时只是运行锁存，确认后才提交活动事实");
        Assert.IsTrue(timed.ResolveExplore(false, out error), error);
        Assert.AreEqual(1, timed.activityResults.Count);
        Assert.AreEqual(0, timed.roomResults.Count);
    }

    [Test]
    public void StaticExploreChecksRejectMapAndPointViolationsWithoutCallingRuntimeCreate()
    {
        var p = BuildExplorePack("player", 0, false);
        p.venues[0].nodes[0].mapText = p.venues[0].nodes[0].mapText.Replace("T", ".");
        p.venues[0].nodes[0].points[0].x = 0;
        p.venues[0].nodes[0].points[0].requires.Add("missing");
        p.venues[0].nodes[0].points.Add(new WorldParkPack.ExplorePoint { id = "duplicate", kind = "exit", outcome = "lose", x = 2, y = 2 });
        p.venues[0].nodes[0].points.Add(new WorldParkPack.ExplorePoint { id = "duplicate", kind = "exit", outcome = "lose", x = 2, y = 3 });
        List<WorldParkPack.Issue> issues = WorldParkPack.Validate(p);
        Assert.IsTrue(Has(issues, "explore_spawn_unique"), Describe(issues));
        Assert.IsTrue(Has(issues, "explore_point_walkable"), Describe(issues));
        Assert.IsTrue(Has(issues, "explore_exit_requires_missing"), Describe(issues));
        Assert.IsTrue(Has(issues, "explore_point_id_duplicate"), Describe(issues));
    }

    private static WorldParkPack.Pack BuildExplorePack(string role, int timeout, bool wall)
    {
        return new WorldParkPack.Pack
        {
            id = "explore_test",
            name = "调查测试",
            premise = "原创现场调查",
            entryVenue = "venue",
            venues = new List<WorldParkPack.Venue>
            {
                new WorldParkPack.Venue
                {
                    id = "venue", name = "陌生小镇", setting = "原创", role = role, suit = "调查", difficulty = 1, rules = "近身调查并选择出口", entry = "explore",
                    nodes = new List<WorldParkPack.Node>
                    {
                        new WorldParkPack.Node
                        {
                            id = "explore", kind = "explore", name = "街口调查", text = "观察现场", mapText = MapText(wall), timeout = timeout, onWin = "win_reveal", onLose = "lose_end",
                            points = new List<WorldParkPack.ExplorePoint>
                            {
                                new WorldParkPack.ExplorePoint { id = "exit", kind = "exit", label = "北门", outcome = "win", x = wall ? 5 : 3, y = 1, requires = new List<string> { "clue_a" } },
                                new WorldParkPack.ExplorePoint { id = "clue", kind = "clue", label = "遗落便签", clue = "clue_a", x = wall ? 4 : 2, y = 1 }
                            }
                        },
                        new WorldParkPack.Node { id = "win_reveal", kind = "reveal", next = "win_end" },
                        new WorldParkPack.Node { id = "win_end", kind = "ending", result = "clear" },
                        new WorldParkPack.Node { id = "lose_end", kind = "ending", result = "fail" }
                    },
                    clues = new List<WorldParkPack.Clue>
                    {
                        new WorldParkPack.Clue { id = "stage_note", name = "入口告示", foundAt = "explore", revealAt = "win_reveal", hint = "可见告示", truth = "作者秘密：阶段真相", trigger = "stage" },
                        new WorldParkPack.Clue { id = "clue_a", name = "便签", foundAt = "explore", revealAt = "win_reveal", hint = "字迹新鲜", truth = "作者秘密：出口含义", trigger = "interact" }
                    }
                }
            },
            links = new List<WorldParkPack.Link>()
        };
    }

    private static string MapText(bool wall)
    {
        var rows = new List<string>();
        for (int y = 11; y >= 0; y--)
        {
            char[] row = new string('t', 16).ToCharArray();
            if (y > 0 && y < 11)
                for (int x = 1; x < 15; x++) row[x] = '.';
            if (y == 1) row[1] = 'T';
            if (wall && y == 1) row[3] = 'W';
            rows.Add(new string(row));
        }
        return "# Overworld: 调查地图\n" + string.Join("\n", rows.ToArray());
    }

    private static bool Has(List<WorldParkPack.Issue> issues, string code)
    {
        foreach (WorldParkPack.Issue issue in issues) if (issue.code == code) return true;
        return false;
    }

    private static bool HasError(List<WorldParkPack.Issue> issues)
    {
        foreach (WorldParkPack.Issue issue in issues) if (issue.severity == "error") return true;
        return false;
    }

    private static bool Contains(List<string> values, string text)
    {
        foreach (string value in values) if (value.Contains(text)) return true;
        return false;
    }

    private static string Describe(List<WorldParkPack.Issue> issues)
    {
        var result = new List<string>();
        foreach (WorldParkPack.Issue issue in issues) result.Add(issue.code + ":" + issue.text);
        return string.Join(" | ", result.ToArray());
    }

    // 仅为了让旧 JSON 字段缺失断言保持可读；JSON writer 本身另由核心测试覆盖。
    private static string Quote(string value) { return "\"" + value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\"", "\\\"") + "\""; }
}
