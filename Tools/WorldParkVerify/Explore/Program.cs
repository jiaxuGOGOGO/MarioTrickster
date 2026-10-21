using System;
using System.Collections.Generic;

internal static class Program
{
    private static int assertions;

    private static int Main()
    {
        try
        {
            JsonCompatibilityAndStaticValidation();
            PhysicalInteractionAndResultSeparation();
            CollisionLineOfSightAndTimeout();
            NegativeStaticChecks();
            ChaserFormatAndValidation();
        ChaserHidingRustleAndCatch();
        ChaserSearchDoesNotJitter();
            Console.WriteLine("PASS WorldParkExplore pure .NET harness: " + assertions + " assertions");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL WorldParkExplore pure .NET harness after " + assertions + " assertions: " + ex.Message);
            return 1;
        }
    }

    private static void JsonCompatibilityAndStaticValidation()
    {
        var pack = Build("player", 0, false);
        string json = WorldParkPack.ToJson(pack);
        var parsed = WorldParkPack.Parse(json, out string error);
        Check(parsed != null, "new v1 fields parse: " + error);
        Equal("stage", parsed.venues[0].clues[0].trigger, "legacy-style default stage trigger round-trips");
        Equal("interact", parsed.venues[0].clues[1].trigger, "interact trigger round-trips");
        Equal(2, parsed.venues[0].nodes[0].points.Count, "point list round-trips");
        Equal("clue_a", parsed.venues[0].nodes[0].points[0].requires[0], "exit requirements round-trip");
        Check(!HasError(WorldParkPack.Validate(parsed)), "player explore validates without old M / doors / schedule: " + Describe(WorldParkPack.Validate(parsed)));

        const string legacy = "{\"type\":\"mariotrickster-worldpark\",\"v\":1,\"id\":\"legacy\",\"name\":\"旧包\",\"premise\":\"\",\"entryVenue\":\"v\",\"venues\":[{\"id\":\"v\",\"role\":\"player\",\"entry\":\"end\",\"nodes\":[{\"id\":\"end\",\"kind\":\"ending\",\"result\":\"clear\"}],\"clues\":[]}],\"links\":[]}";
        var old = WorldParkPack.Parse(legacy, out error);
        Check(old != null, "legacy v1 JSON with no newly added fields must parse: " + error);
        Equal("stage", new WorldParkPack.Clue().trigger, "programmatic clue default remains stage");
    }

    private static void PhysicalInteractionAndResultSeparation()
    {
        var session = WorldParkPack.Start(Build("trickster", 0, false), out string error);
        Check(session != null, error);
        var run = WorldParkExplore.Create(session, out error);
        Check(run != null, error);
        Check(!session.found.Contains("venue/clue_a"), "interact clue not auto-found on stage entry");
        Check(!run.Interact("exit", out error) && error.Contains("太远"), "remote exit cannot interact");
        Check(!run.Ended, "remote interaction does not end run");

        run.Tick(.25, 1, 0); // T -> clue; exit is near but still locked
        Check(!run.Interact("exit", out error) && error.Contains("缺少线索"), "locked exit reports missing evidence without losing");
        Check(!run.Ended, "missing evidence does not latch a loss");
        Check(run.Interact("clue", out error), error);
        Check(session.found.Contains("venue/clue_a"), "close clue interaction records found");
        Check(!session.revealed.Contains("venue/clue_a"), "found clue truth remains unrevealed");
        Check(!Contains(session.log, "作者秘密"), "author truth never enters session log");
        run.Tick(.25, 1, 0); // clue -> exit
        Check(run.Interact("exit", out error), error);
        Check(run.Ended && run.Won, "qualified exit locks the configured outcome");
        Equal("explore", session.Current.kind, "exit does not auto-resolve workflow");
        Equal(0, session.roomResults.Count, "exit writes no room fact");
        Equal(0, session.activityResults.Count, "exit latch writes no activity fact before confirmation");
        Check(session.ResolveExplore(run.Won, out error), error);
        Equal("win_reveal", session.Current.id, "ResolveExplore follows onWin");
        Equal(0, session.roomResults.Count, "ResolveExplore never writes RoomResult");
        Equal(1, session.activityResults.Count, "ResolveExplore writes one ActivityResult");
        Equal("explore", session.activityResults[0].kind, "activity kind records explore");
    }

    private static void CollisionLineOfSightAndTimeout()
    {
        var blocked = WorldParkPack.Start(Build("player", 0, true), out string error);
        Check(blocked != null, error);
        var collision = WorldParkExplore.Create(blocked, out error);
        Check(collision != null, error);
        collision.Tick(.75, 1, 0);
        Check(collision.x < 3.0, "OverworldMap.Move blocks a wall instead of crossing it");
        Check(collision.LastMessage.Contains("阻挡"), "collision reports blocked movement");
        Check(!OverworldMap.LineOfSight(collision.map, 2.5, 1.5, 4.5, 1.5), "OverworldMap LOS rejects intervening wall");
        collision.Tick(double.NaN, 0, 0);
        Check(collision.LastMessage.Contains("有限"), "non-finite tick is rejected");

        var timed = WorldParkPack.Start(Build("player", 1, false), out error);
        Check(timed != null, error);
        var clock = WorldParkExplore.Create(timed, out error);
        clock.Tick(1, 0, 0);
        Check(clock.Ended && !clock.Won, "timeout latches failure exactly at deadline");
        Equal(0.0, clock.Remaining, "timeout reaches zero remaining time");
        Equal(0, timed.roomResults.Count, "timeout does not write RoomResult");
        Equal(0, timed.activityResults.Count, "timeout awaits external confirmation");
        Check(timed.ResolveExplore(false, out error), error);
        Equal(1, timed.activityResults.Count, "confirmed timeout writes activity result");
        Equal(0, timed.roomResults.Count, "confirmed timeout still writes no room result");
    }

    private static void NegativeStaticChecks()
    {
        var invalid = Build("player", 0, false);
        invalid.venues[0].nodes[0].mapText = invalid.venues[0].nodes[0].mapText.Replace('T', '.');
        invalid.venues[0].nodes[0].points[0].x = 0;
        invalid.venues[0].nodes[0].points[0].requires.Add("missing");
        invalid.venues[0].nodes[0].points.Add(new WorldParkPack.ExplorePoint { id = "again", kind = "exit", outcome = "lose", x = 2, y = 2 });
        invalid.venues[0].nodes[0].points.Add(new WorldParkPack.ExplorePoint { id = "again", kind = "exit", outcome = "lose", x = 2, y = 3 });
        var issues = WorldParkPack.Validate(invalid);
        Check(Has(issues, "explore_spawn_unique"), "static check catches non-unique/missing T");
        Check(Has(issues, "explore_point_walkable"), "static check catches non-walkable point");
        Check(Has(issues, "explore_exit_requires_missing"), "static check catches missing exit clue requirement");
        Check(Has(issues, "explore_point_id_duplicate"), "static check catches duplicate point id");
    }

    // ── S252 追逐者 ─────────────────────────────────────
    private static WorldParkPack.Pack BuildChase(int timeout, int chaserX = 5, int chaserY = 7)
    {
        var pack = Build("player", timeout, false);
        var node = pack.venues[0].nodes[0];
        node.mapText = ChaseMap();
        node.points[0].requires.Clear();
        node.points.Add(new WorldParkPack.ExplorePoint { id = "oni", kind = "chaser", label = "鬼", x = chaserX, y = chaserY, speed = 34, sight = 4, route = new List<string> { "5,6" } });
        return pack;
    }

    private static string ChaseMap()
    {
        var rows = new List<string>();
        for (int y = 11; y >= 0; y--)
        {
            char[] row = new string('t', 16).ToCharArray();
            if (y > 0 && y < 11) for (int x = 1; x < 15; x++) row[x] = '.';
            if (y == 2 || y == 3) for (int x = 4; x <= 6; x++) row[x] = '"';
            if (y == 1) row[5] = 'T';
            rows.Add(new string(row));
        }
        return "# Overworld: 鬼抓人测试\n" + string.Join("\n", rows.ToArray());
    }

    private static void ChaserFormatAndValidation()
    {
        var pack = BuildChase(0);
        Check(!HasError(WorldParkPack.Validate(pack)), "chaser stage validates: " + Describe(WorldParkPack.Validate(pack)));
        string json = WorldParkPack.ToJson(pack);
        Equal(1, Count(json, "\"route\""), "only chaser points export route");
        Equal(1, Count(json, "\"speed\""), "only chaser points export speed");
        var parsed = WorldParkPack.Parse(json, out string error);
        Check(parsed != null, "chaser JSON parses: " + error);
        var oni = parsed.venues[0].nodes[0].points[2];
        Equal("chaser", oni.kind, "chaser kind round-trips");
        Equal(34, oni.speed, "chaser speed round-trips");
        Equal(4, oni.sight, "chaser sight round-trips");
        Equal("5,6", oni.route[0], "chaser route round-trips");

        var bad = BuildChase(0, 5, 3);
        var p = bad.venues[0].nodes[0].points[2];
        p.speed = 5; p.sight = 30; p.route = new List<string> { "5, 6" };
        var issues = WorldParkPack.Validate(bad);
        Check(Has(issues, "explore_chaser_speed"), "speed outside 10–80 is rejected");
        Check(Has(issues, "explore_chaser_sight"), "sight outside 1–20 is rejected");
        Check(Has(issues, "explore_chaser_route"), "route with spaces is rejected (strict x,y)");
        Check(Has(issues, "explore_chaser_spawn_close"), "chaser spawning within 4 cells of T warns");
        p.route = new List<string> { "0,0" };
        Check(Has(WorldParkPack.Validate(bad), "explore_chaser_route"), "route on a tree cell is rejected");
        var many = BuildChase(0);
        for (int i = 0; i < 4; i++) many.venues[0].nodes[0].points.Add(new WorldParkPack.ExplorePoint { id = "oni" + i, kind = "chaser", x = 10 + i, y = 9 });
        Check(Has(WorldParkPack.Validate(many), "explore_chaser_limit"), "more than 4 chasers is rejected");
    }

    private static WorldParkExplore.Run ChaseScenario(out string trace)
    {
        var session = WorldParkPack.Start(BuildChase(0), out string error);
        Check(session != null, error);
        var run = WorldParkExplore.Create(session, out error);
        Check(run != null, error);
        Equal(1, run.Chasers.Count, "chaser is created from points");
        Check(run.NearbyPoint == null || run.NearbyPoint.kind != "chaser", "chaser is never an interactable point");
        run.Tick(.25, 0, 1); // 走进高草（离鬼约 4.5 格，超出 4 格视距）
        Check(run.PlayerHidden, "player stands in tall grass");
        for (int i = 0; i < 6; i++) run.Tick(.5, 0, 0);
        Check(!run.Ended && run.Events.Count == 0, "standing still in grass inside the view cone stays hidden");
        Equal("patrol", run.Chasers[0].mode, "chaser keeps patrolling while player hides");
        run.Tick(.25, 0, 1); // 在草里走动 → 草晃
        Check(HasEvent(run, "rustle"), "moving in grass inside the cone makes it rustle");
        Equal("search", run.Chasers[0].mode, "rustle sends chaser to search, not to chase");
        Check(!run.Chasers[0].SeesPlayer, "rustle does not reveal the player directly");
        for (int i = 0; i < 8 && !run.Ended; i++) run.Tick(.5, 0, 0);
        Check(HasEvent(run, "seen"), "searching chaser sees player within 1.5 cells even in grass");
        Check(run.Ended && !run.Won, "chaser contact ends exploration as a loss");
        Equal("鬼", run.CaughtBy, "caught by is recorded");
        Check(run.LastMessage.Contains("抓住"), "catch message is kept instead of being overwritten");
        var lines = new List<string>();
        foreach (var e in run.Events) lines.Add(e.type + "@" + e.time.ToString("F3"));
        trace = string.Join(",", lines.ToArray()) + "|" + run.x.ToString("F6") + "," + run.y.ToString("F6");
        return run;
    }

    private static void ChaserHidingRustleAndCatch()
    {
        ChaseScenario(out string first);
        ChaseScenario(out string second);
        Equal(first, second, "same inputs reproduce the same chase exactly");

        var timed = WorldParkPack.Start(BuildChase(3), out string error);
        var clock = WorldParkExplore.Create(timed, out error);
        for (int i = 0; i < 5 && !clock.Ended; i++) clock.Tick(1, 0, 0);
        Check(clock.Ended && !clock.Won && clock.CaughtBy == "", "unseen player still times out normally on a chase stage");
        Equal(0.0, clock.Remaining, "chase stage timeout reaches zero");
        Check(clock.LastMessage.Contains("超时"), "timeout message on chase stage");
    }

    private static string EventList(WorldParkExplore.Run run) { var l = new List<string>(); foreach (var e in run.Events) l.Add(e.type + "@" + e.time.ToString("F2")); return string.Join(",", l.ToArray()); }
    private static bool HasEvent(WorldParkExplore.Run run, string type) { foreach (var e in run.Events) if (e.type == type) return true; return false; }

    /// <summary>S252：剧情模拟发现——搜寻中玩家在草里左右挪一格，鬼的目标在两格间来回换，鬼原地抖动、永不放弃。</summary>
    private static void ChaserSearchDoesNotJitter()
    {
        var session = WorldParkPack.Start(BuildChase(0), out string error);
        Check(session != null, error);
        var run = WorldParkExplore.Create(session, out error);
        run.Tick(.25, 0, 1); // 进草
        run.Tick(.25, 0, 1); // 草里走动 → 草晃
        Check(HasEvent(run, "rustle"), "wiggle test starts with a rustle");
        var c = run.Chasers[0];
        double tx = c.lastX, ty = c.lastY;
        run.Tick(.25, 1, 0); // 草里横挪一格
        Check(Math.Abs(c.lastX - tx) < 1e-9 && Math.Abs(c.lastY - ty) < 1e-9, "a rustle within 1.5 cells of the current search point does not retarget the search");
        double dir = -1;
        for (int i = 0; i < 60 && !run.Ended && !HasEvent(run, "giveup"); i++) { run.Tick(.25, dir, 0); dir = -dir; }
        Check(run.Ended || HasEvent(run, "giveup"), "wiggling in grass cannot freeze a searching chaser forever: mode=" + c.mode + " chaser=" + c.x.ToString("F2") + "," + c.y.ToString("F2") + " target=" + c.lastX.ToString("F2") + "," + c.lastY.ToString("F2") + " player=" + run.x.ToString("F2") + "," + run.y.ToString("F2") + " events=" + EventList(run));
        Check(WorldParkExplore.SearchTravelSeconds > WorldParkExplore.SearchSeconds, "search travel cap is longer than the look-around time");
        Check(run.Ended && run.CaughtBy == "鬼", "wiggling left-right right in front of a chaser gets you caught (no path flip-flop freeze): events=" + EventList(run));
    }
    private static int Count(string text, string part) { int n = 0, i = 0; while ((i = text.IndexOf(part, i, StringComparison.Ordinal)) >= 0) { n++; i += part.Length; } return n; }

    private static WorldParkPack.Pack Build(string role, int timeout, bool wall)
    {
        return new WorldParkPack.Pack
        {
            id = "explore_test", name = "调查", premise = "原创", entryVenue = "venue",
            venues = new List<WorldParkPack.Venue>
            {
                new WorldParkPack.Venue
                {
                    id = "venue", name = "陌生小镇", setting = "原创", role = role, suit = "调查", difficulty = 1, rules = "近身调查", entry = "explore",
                    nodes = new List<WorldParkPack.Node>
                    {
                        new WorldParkPack.Node
                        {
                            id = "explore", kind = "explore", name = "调查", mapText = MapText(wall), timeout = timeout, onWin = "win_reveal", onLose = "lose_end",
                            points = new List<WorldParkPack.ExplorePoint>
                            {
                                new WorldParkPack.ExplorePoint { id = "exit", kind = "exit", label = "出口", outcome = "win", x = wall ? 5 : 3, y = 1, requires = new List<string> { "clue_a" } },
                                new WorldParkPack.ExplorePoint { id = "clue", kind = "clue", label = "便签", clue = "clue_a", x = wall ? 4 : 2, y = 1 }
                            }
                        },
                        new WorldParkPack.Node { id = "win_reveal", kind = "reveal", next = "win_end" },
                        new WorldParkPack.Node { id = "win_end", kind = "ending", result = "clear" },
                        new WorldParkPack.Node { id = "lose_end", kind = "ending", result = "fail" }
                    },
                    clues = new List<WorldParkPack.Clue>
                    {
                        new WorldParkPack.Clue { id = "stage_note", name = "告示", foundAt = "explore", revealAt = "win_reveal", hint = "告示", truth = "作者秘密阶段", trigger = "stage" },
                        new WorldParkPack.Clue { id = "clue_a", name = "便签", foundAt = "explore", revealAt = "win_reveal", hint = "字迹", truth = "作者秘密出口", trigger = "interact" }
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
            if (y > 0 && y < 11) for (int x = 1; x < 15; x++) row[x] = '.';
            if (y == 1) row[1] = 'T';
            if (wall && y == 1) row[3] = 'W';
            rows.Add(new string(row));
        }
        return "# Overworld: 调查地图\n" + string.Join("\n", rows.ToArray());
    }

    private static bool Has(List<WorldParkPack.Issue> issues, string code) { foreach (var issue in issues) if (issue.code == code) return true; return false; }
    private static bool HasError(List<WorldParkPack.Issue> issues) { foreach (var issue in issues) if (issue.severity == "error") return true; return false; }
    private static bool Contains(List<string> values, string text) { foreach (string value in values) if (value.Contains(text)) return true; return false; }
    private static string Describe(List<WorldParkPack.Issue> issues) { var lines = new List<string>(); foreach (var issue in issues) lines.Add(issue.code + ":" + issue.text); return string.Join(" | ", lines.ToArray()); }
    private static void Check(bool condition, string text) { assertions++; if (!condition) throw new InvalidOperationException(text); }
    private static void Equal(string expected, string actual, string text) { Check(expected == actual, text + " expected=" + expected + " actual=" + actual); }
    private static void Equal(int expected, int actual, string text) { Check(expected == actual, text + " expected=" + expected + " actual=" + actual); }
    private static void Equal(double expected, double actual, string text) { Check(Math.Abs(expected - actual) < 1e-9, text + " expected=" + expected + " actual=" + actual); }
}
