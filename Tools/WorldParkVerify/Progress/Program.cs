using System;
using System.Collections.Generic;

internal static class Program
{
    private static int assertions;

    private static int Main()
    {
        try
        {
            RoomRoundTrip();
            ExploreRoundTrip();
            RejectsUnsafeAndTamperedInputs();
            RetryVenueRewinds();
            Console.WriteLine("PASS WorldParkProgress pure-logic harness: " + assertions + " assertions");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL WorldParkProgress harness after " + assertions + " assertions: " + ex);
            return 1;
        }
    }

    private static void RoomRoundTrip()
    {
        var pack = RoomPack();
        string fingerprint = WorldParkProgress.Fingerprint(pack);
        Check(fingerprint == WorldParkProgress.Fingerprint(pack) && fingerprint.Length == 64, "SHA-256包指纹稳定且为64位");
        var session = FinishRoom(pack, out string error);
        Check(session != null, error);
        string save = WorldParkProgress.Save(session, out error);
        Check(save != null, error);
        Check(save == WorldParkProgress.Save(session, out error), "同会话检查点输出稳定");
        Check(!save.Contains("作者秘密") && !save.Contains("\"truth\"") && !save.Contains("\"hint\"") && !save.Contains("\"log\""), "检查点不泄漏真相、提示或日志");
        var restored = WorldParkProgress.Restore(pack, save, out error);
        Check(restored != null, error);
        Equal("venue_2", restored.venueId, "重放抵达下一会场");
        Equal("arrival", restored.nodeId, "重放抵达下一会场入口");
        Check(restored.results.Count == 2 && restored.results["venue_1"] == "clear" && restored.results["venue_2"] == "clear", "会场结果由动作推导");
        SameActions(session.actions, restored.actions);
        Check(restored.roomResults.Count == 1 && restored.roomResults[0].won, "房间事实由重放得到");

        pack.name = "已变更的包";
        Check(WorldParkProgress.Restore(pack, save, out error) == null && error.Contains("指纹"), "包变化隔离旧检查点");
    }

    private static void ExploreRoundTrip()
    {
        var pack = ExplorePack();
        var session = WorldParkPack.Start(pack, out string error);
        Check(session != null, error);
        Check(session.ObserveClue("note", out error), error);
        Check(session.ResolveExplore(true, out error), error);
        Check(session.Continue(out error), error);
        string save = WorldParkProgress.Save(session, out error);
        Check(save != null, error);
        Check(save.Contains("\"kind\":\"observe\"") && save.Contains("\"kind\":\"explore\""), "首次观察与探索结果均记录事件");
        Check(!save.Contains("\"found\"") && !save.Contains("\"revealed\""), "线索状态不直接存储");
        var restored = WorldParkProgress.Restore(pack, save, out error);
        Check(restored != null, error);
        Check(restored.found.Contains("venue/note") && restored.revealed.Contains("venue/note"), "线索由重放推导");
        Check(restored.activityResults.Count == 1 && restored.activityResults[0].kind == "explore" && restored.activityResults[0].won, "探索事实由重放得到");
        SameActions(session.actions, restored.actions);

        const string observe = "{\"kind\":\"observe\",\"value\":\"note\"}";
        string duplicate = save.Replace(observe + ",{\"kind\":\"explore\"", observe + "," + observe + ",{\"kind\":\"explore\"");
        Check(WorldParkProgress.Restore(pack, duplicate, out error) == null && error.Contains("重复记录观察"), "重复观察事件拒绝");
    }

    private static void RejectsUnsafeAndTamperedInputs()
    {
        RejectsUnsafeAndTamperedInputsCore();
    }

    /// <summary>S254：重来本会场 = 回到最后一次进入本会场时（与网页 retryVenue 同语义）。</summary>
    private static void RetryVenueRewinds()
    {
        // 入口会场里调查过线索：重来后线索、活动结果、动作全部撤销，回到会场入口。
        var pack = ExplorePack();
        var session = WorldParkPack.Start(pack, out string error);
        Check(session.ObserveClue("note", out error), error);
        var again = WorldParkProgress.RetryVenue(session, out error);
        Check(again != null, error);
        Equal("venue", again.venueId, "入口会场重来仍在本会场");
        Equal("explore", again.nodeId, "入口会场重来回到会场入口阶段");
        Check(again.actions.Count == 0 && again.found.Count == 0, "入口会场重来撤销本会场的观察");
        Check(session.found.Contains("venue/note"), "重来不改原会话（返回新会话）");

        // 第二个会场：保留到 travel 为止，第一个会场的结果原样保留。
        var roomPack = RoomPack();
        var atSecond = FinishRoom(roomPack, out error);
        Check(atSecond != null, error);
        var rewind = WorldParkProgress.RetryVenue(atSecond, out error);
        Check(rewind != null, error);
        Equal("venue_2", rewind.venueId, "第二会场重来仍在第二会场");
        Equal("arrival", rewind.nodeId, "第二会场重来回到它的入口");
        Check(rewind.results.ContainsKey("venue_1") && rewind.results["venue_1"] == "clear", "其他会场结果保留");
        SameActions(atSecond.actions, rewind.actions);
        Check(WorldParkProgress.RetryVenue(null, out error) == null && error.Length > 0, "空会话拒绝重来");
    }

    private static void RejectsUnsafeAndTamperedInputsCore()
    {
        var roomPack = RoomPack();
        var atRoom = WorldParkPack.Start(roomPack, out string error);
        Check(atRoom.Continue(out error), error);
        Check(WorldParkProgress.Save(atRoom, out error) == null && error.Contains("room"), "room中途不保存");
        var atExplore = WorldParkPack.Start(ExplorePack(), out error);
        Check(WorldParkProgress.Save(atExplore, out error) == null && error.Contains("explore"), "explore中途不保存");

        var session = FinishRoom(roomPack, out error);
        string save = WorldParkProgress.Save(session, out error);
        Check(WorldParkProgress.Restore(roomPack, save.Replace("\"kind\":\"continue\"", "\"kind\":\"unknown\""), out error) == null && error.Contains("未知动作"), "未知动作拒绝");
        Check(WorldParkProgress.Restore(roomPack, save.Replace("\"value\":\"venue_2\"", "\"value\":\"missing\""), out error) == null && error.Contains("无法按当前规则重放"), "非法旅行拒绝");
        Check(WorldParkProgress.Restore(roomPack, save.Replace("\"node\":\"arrival\"", "\"node\":\"forged\""), out error) == null && error.Contains("末会场或阶段"), "伪造末阶段拒绝");
        Check(WorldParkProgress.Restore(roomPack, save.Replace("\"result\":\"clear\"", "\"result\":\"fail\""), out error) == null && error.Contains("结果"), "伪造会场结果拒绝");
        Check(WorldParkProgress.Restore(roomPack, save.Replace("\"v\":1", "\"v\":\"1\""), out error) == null && error.Contains("整数"), "字段类型错误拒绝");
        Check(WorldParkProgress.Restore(roomPack, save.Replace("{\"kind\":\"continue\",\"value\":\"\"}", "{\"kind\":\"room\",\"value\":\"win\"}"), out error) == null && error.Contains("无法按当前规则重放"), "越序动作拒绝");

        var overflow = new List<string>();
        for (int i = 0; i < WorldParkProgress.MaxActions + 1; i++) overflow.Add("{\"kind\":\"continue\",\"value\":\"\"}");
        string tooMany = save.Replace("\"actions\":[", "\"actions\":[" + string.Join(",", overflow.ToArray()) + ",");
        Check(WorldParkProgress.Restore(roomPack, tooMany, out error) == null && error.Contains("动作") && error.Contains("上限"), "动作预算拒绝");
        Check(WorldParkProgress.Restore(roomPack, new string('x', WorldParkProgress.MaxSaveLength + 1), out error) == null && error.Contains("上限"), "字节预算拒绝");
        Check(WorldParkProgress.Restore(roomPack, new string('测', WorldParkProgress.MaxSaveLength / 2 + 1), out error) == null && error.Contains("上限"), "UTF-8字节预算拒绝");
    }

    private static WorldParkPack.Session FinishRoom(WorldParkPack.Pack pack, out string error)
    {
        var session = WorldParkPack.Start(pack, out error);
        if (session == null || !session.Continue(out error) || !session.ResolveRoom(true, out error) || !session.Continue(out error) || !session.Travel("venue_2", out error)) return null;
        return session;
    }

    private static WorldParkPack.Pack RoomPack()
    {
        return new WorldParkPack.Pack
        {
            id = "progress_room", name = "房间检查点", premise = "原创测试", entryVenue = "venue_1",
            venues = new List<WorldParkPack.Venue>
            {
                new WorldParkPack.Venue
                {
                    id = "venue_1", name = "入口", setting = "原创", role = "trickster", suit = "自定义", difficulty = 1, rules = "真实结果", entry = "intro",
                    nodes = new List<WorldParkPack.Node>
                    {
                        new WorldParkPack.Node { id = "intro", kind = "story", next = "room" },
                        new WorldParkPack.Node { id = "room", kind = "room", room = "默认房间", onWin = "reveal", onLose = "fail" },
                        new WorldParkPack.Node { id = "reveal", kind = "reveal", next = "done" },
                        new WorldParkPack.Node { id = "done", kind = "ending", result = "clear" },
                        new WorldParkPack.Node { id = "fail", kind = "ending", result = "fail" }
                    },
                    clues = new List<WorldParkPack.Clue> { new WorldParkPack.Clue { id = "stage", foundAt = "intro", revealAt = "reveal", hint = "公开提示", truth = "作者秘密", trigger = "stage" } }
                },
                new WorldParkPack.Venue
                {
                    id = "venue_2", name = "下一站", setting = "原创", role = "player", suit = "自定义", difficulty = 1, rules = "抵达", entry = "arrival",
                    nodes = new List<WorldParkPack.Node> { new WorldParkPack.Node { id = "arrival", kind = "ending", result = "clear" } }, clues = new List<WorldParkPack.Clue>()
                }
            },
            links = new List<WorldParkPack.Link> { new WorldParkPack.Link { from = "venue_1", to = "venue_2", requires = "clear" } }
        };
    }

    private static WorldParkPack.Pack ExplorePack()
    {
        var rows = new List<string>();
        for (int y = 11; y >= 0; y--)
        {
            char[] row = new string('t', 16).ToCharArray();
            if (y > 0 && y < 11) for (int x = 1; x < 15; x++) row[x] = '.';
            if (y == 1) row[1] = 'T';
            rows.Add(new string(row));
        }
        return new WorldParkPack.Pack
        {
            id = "progress_explore", name = "探索检查点", premise = "原创调查", entryVenue = "venue",
            venues = new List<WorldParkPack.Venue>
            {
                new WorldParkPack.Venue
                {
                    id = "venue", name = "现场", setting = "原创", role = "player", suit = "调查", difficulty = 1, rules = "先调查", entry = "explore",
                    nodes = new List<WorldParkPack.Node>
                    {
                        new WorldParkPack.Node { id = "explore", kind = "explore", mapText = "# Overworld: 检查点\n" + string.Join("\n", rows.ToArray()), onWin = "reveal", onLose = "fail", points = new List<WorldParkPack.ExplorePoint> { new WorldParkPack.ExplorePoint { id = "note_point", kind = "clue", clue = "note", x = 2, y = 1 }, new WorldParkPack.ExplorePoint { id = "exit", kind = "exit", outcome = "win", x = 3, y = 1, requires = new List<string> { "note" } } } },
                        new WorldParkPack.Node { id = "reveal", kind = "reveal", next = "done" },
                        new WorldParkPack.Node { id = "done", kind = "ending", result = "clear" },
                        new WorldParkPack.Node { id = "fail", kind = "ending", result = "fail" }
                    },
                    clues = new List<WorldParkPack.Clue> { new WorldParkPack.Clue { id = "note", foundAt = "explore", revealAt = "reveal", hint = "公开笔记", truth = "作者秘密", trigger = "interact" } }
                }
            }, links = new List<WorldParkPack.Link>()
        };
    }

    private static void SameActions(List<WorldParkProgress.Action> expected, List<WorldParkProgress.Action> actual)
    {
        Check(expected.Count == actual.Count, "动作数量一致");
        for (int i = 0; i < expected.Count; i++) Check(expected[i].kind == actual[i].kind && expected[i].value == actual[i].value, "动作#" + i + " 一致");
    }

    private static void Equal(string expected, string actual, string message) { Check(expected == actual, message + "，预期=" + expected + "，实际=" + actual); }
    private static void Check(bool condition, string message) { assertions++; if (!condition) throw new InvalidOperationException(message); }
}
