using System;
using System.Collections.Generic;
using NUnit.Framework;

public class WorldParkProgressTests
{
    /// <summary>S254：重来本会场回到最后一次进入本会场时（与网页 play.js retryVenue 同语义），不改原会话。</summary>
    [Test]
    public void S254_RetryVenueRewindsToVenueEntryWithoutTouchingOtherVenues()
    {
        var pack = BuildExplorePack();
        var session = WorldParkPack.Start(pack, out string error);
        Assert.IsNotNull(session, error);
        Assert.IsTrue(session.ObserveClue("note", out error), error);
        var again = WorldParkProgress.RetryVenue(session, out error);
        Assert.IsNotNull(again, error);
        Assert.AreEqual("venue", again.venueId);
        Assert.AreEqual("explore", again.nodeId, "入口会场重来回到会场入口阶段");
        Assert.AreEqual(0, again.actions.Count, "本会场的观察被撤销");
        Assert.AreEqual(0, again.found.Count);
        Assert.IsTrue(session.found.Contains("venue/note"), "返回新会话，原会话不变");

        var travel = BuildRoomTravelPack();
        var atSecond = WorldParkPack.Start(travel, out error);
        Assert.IsTrue(atSecond.Continue(out error), error);
        Assert.IsTrue(atSecond.ResolveRoom(true, out error), error);
        Assert.IsTrue(atSecond.Continue(out error), error);
        Assert.IsTrue(atSecond.Travel("venue_2", out error), error);
        var rewind = WorldParkProgress.RetryVenue(atSecond, out error);
        Assert.IsNotNull(rewind, error);
        Assert.AreEqual("venue_2", rewind.venueId);
        Assert.AreEqual("arrival", rewind.nodeId);
        Assert.AreEqual("clear", rewind.results["venue_1"], "其他会场结果保留");
        Assert.IsNull(WorldParkProgress.RetryVenue(null, out error));
    }

    /// <summary>S254：玩家界面大小只能取设置里列出的档位；菜单动作常量互不相同。</summary>
    [Test]
    public void S254_UiScaleIsClampedAndMenuActionsAreDistinct()
    {
        float before = WorldParkUi.UserScale;
        try
        {
            WorldParkUi.SetScale(9f);
            Assert.LessOrEqual(WorldParkUi.UserScale, WorldParkUi.Scales[WorldParkUi.Scales.Length - 1] + 0.001f);
            WorldParkUi.SetScale(0.1f);
            Assert.GreaterOrEqual(WorldParkUi.UserScale, WorldParkUi.Scales[0] - 0.001f);
        }
        finally { WorldParkUi.SetScale(before); }
        var actions = new[] { WorldParkUi.Resume, WorldParkUi.Save, WorldParkUi.Retry, WorldParkUi.Title, WorldParkUi.Quit, WorldParkUi.NewJourney, WorldParkUi.ContinueJourney };
        Assert.AreEqual(actions.Length, new HashSet<string>(actions).Count);
    }

    [Test]
    public void RoomTravelCheckpointRoundTripsOnlyActionHistory()
    {
        var pack = BuildRoomTravelPack();
        var session = WorldParkPack.Start(pack, out string error);
        Assert.IsNotNull(session, error);
        Assert.IsTrue(session.Continue(out error), error);
        Assert.IsTrue(session.ResolveRoom(true, out error), error);
        Assert.IsTrue(session.Continue(out error), error);
        Assert.IsTrue(session.Travel("venue_2", out error), error);
        Assert.AreEqual("ending", session.Current.kind);

        string save = WorldParkProgress.Save(session, out error);
        Assert.IsNotNull(save, error);
        Assert.AreEqual(save, WorldParkProgress.Save(session, out error), "相同会话的检查点输出必须稳定");
        StringAssert.DoesNotContain("作者秘密", save);
        StringAssert.DoesNotContain("hint", save);
        StringAssert.DoesNotContain("truth", save);
        StringAssert.DoesNotContain("log", save);
        StringAssert.Contains("\"actions\"", save);
        StringAssert.Contains("\"fingerprint\"", save);

        var restored = WorldParkProgress.Restore(pack, save, out error);
        Assert.IsNotNull(restored, error);
        Assert.AreEqual("venue_2", restored.venueId);
        Assert.AreEqual("arrival", restored.nodeId);
        Assert.AreEqual("clear", restored.results["venue_1"]);
        Assert.AreEqual("clear", restored.results["venue_2"]);
        AssertSameActions(session.actions, restored.actions);
        Assert.AreEqual(1, restored.roomResults.Count);
        Assert.IsTrue(restored.roomResults[0].won);
    }

    [Test]
    public void ExploreObserveAndOutcomeAreReplayedWithoutSavingExploreRuntimeState()
    {
        var pack = BuildExplorePack();
        var session = WorldParkPack.Start(pack, out string error);
        Assert.IsNotNull(session, error);
        Assert.IsTrue(session.ObserveClue("note", out error), error);
        Assert.IsTrue(session.ResolveExplore(true, out error), error);
        Assert.IsTrue(session.Continue(out error), error);
        Assert.AreEqual("ending", session.Current.kind);

        string save = WorldParkProgress.Save(session, out error);
        Assert.IsNotNull(save, error);
        StringAssert.Contains("\"kind\":\"observe\"", save);
        StringAssert.Contains("\"kind\":\"explore\"", save);
        StringAssert.DoesNotContain("\"found\"", save);
        StringAssert.DoesNotContain("\"revealed\"", save);

        var restored = WorldParkProgress.Restore(pack, save, out error);
        Assert.IsNotNull(restored, error);
        Assert.IsTrue(restored.found.Contains("venue/note"));
        Assert.IsTrue(restored.revealed.Contains("venue/note"));
        Assert.AreEqual(1, restored.activityResults.Count);
        Assert.AreEqual("explore", restored.activityResults[0].kind);
        Assert.IsTrue(restored.activityResults[0].won);
        AssertSameActions(session.actions, restored.actions);
    }

    [Test]
    public void SaveRejectsRoomAndExploreInProgress()
    {
        var room = WorldParkPack.Start(BuildRoomTravelPack(), out string error);
        Assert.IsTrue(room.Continue(out error), error);
        Assert.IsNull(WorldParkProgress.Save(room, out error));
        StringAssert.Contains("room", error);

        var explore = WorldParkPack.Start(BuildExplorePack(), out error);
        Assert.IsNull(WorldParkProgress.Save(explore, out error));
        StringAssert.Contains("explore", error);
    }

    [Test]
    public void FingerprintSeparatesChangedPacksAndIsStableForSamePack()
    {
        var pack = BuildRoomTravelPack();
        string first = WorldParkProgress.Fingerprint(pack);
        string second = WorldParkProgress.Fingerprint(pack);
        Assert.AreEqual(first, second);
        Assert.AreEqual(64, first.Length);

        var session = FinishRoomTravel(pack, out string error);
        string save = WorldParkProgress.Save(session, out error);
        Assert.IsNotNull(save, error);
        pack.name = "改过的作者标题";
        Assert.IsNull(WorldParkProgress.Restore(pack, save, out error));
        StringAssert.Contains("指纹", error);
    }

    [Test]
    public void RestoreRejectsUnknownOutOfOrderDuplicateAndMismatchedActions()
    {
        var roomPack = BuildRoomTravelPack();
        var roomSession = FinishRoomTravel(roomPack, out string error);
        string roomSave = WorldParkProgress.Save(roomSession, out error);
        Assert.IsNotNull(roomSave, error);

        Assert.IsNull(WorldParkProgress.Restore(roomPack, roomSave.Replace("\"kind\":\"continue\"", "\"kind\":\"other\""), out error));
        StringAssert.Contains("未知动作", error);
        Assert.IsNull(WorldParkProgress.Restore(roomPack, roomSave.Replace("\"value\":\"venue_2\"", "\"value\":\"missing\""), out error));
        StringAssert.Contains("无法按当前规则重放", error);
        Assert.IsNull(WorldParkProgress.Restore(roomPack, roomSave.Replace("\"node\":\"arrival\"", "\"node\":\"forged\""), out error));
        StringAssert.Contains("末会场或阶段", error);
        Assert.IsNull(WorldParkProgress.Restore(roomPack, roomSave.Replace("\"v\":1", "\"v\":\"1\""), out error));
        StringAssert.Contains("整数", error);

        var explorePack = BuildExplorePack();
        var exploreSession = WorldParkPack.Start(explorePack, out error);
        Assert.IsTrue(exploreSession.ObserveClue("note", out error), error);
        Assert.IsTrue(exploreSession.ResolveExplore(true, out error), error);
        Assert.IsTrue(exploreSession.Continue(out error), error);
        string exploreSave = WorldParkProgress.Save(exploreSession, out error);
        const string observed = "{\"kind\":\"observe\",\"value\":\"note\"}";
        string duplicateObserve = exploreSave.Replace(observed + ",{\"kind\":\"explore\"", observed + "," + observed + ",{\"kind\":\"explore\"");
        Assert.IsNull(WorldParkProgress.Restore(explorePack, duplicateObserve, out error));
        StringAssert.Contains("重复记录观察", error);
    }

    [Test]
    public void RestoreRejectsContainerActionAndLengthBudgets()
    {
        var pack = BuildRoomTravelPack();
        var session = FinishRoomTravel(pack, out string error);
        string save = WorldParkProgress.Save(session, out error);
        Assert.IsNotNull(save, error);

        var tooMany = new List<string>();
        for (int i = 0; i < WorldParkProgress.MaxActions + 1; i++) tooMany.Add("{\"kind\":\"continue\",\"value\":\"\"}");
        string actionOverflow = save.Replace("\"actions\":[", "\"actions\":[" + string.Join(",", tooMany.ToArray()) + ",");
        Assert.IsNull(WorldParkProgress.Restore(pack, actionOverflow, out error));
        StringAssert.Contains("动作", error);
        StringAssert.Contains("上限", error);

        Assert.IsNull(WorldParkProgress.Restore(pack, new string('x', WorldParkProgress.MaxSaveLength + 1), out error));
        StringAssert.Contains("上限", error);
    }

    private static WorldParkPack.Session FinishRoomTravel(WorldParkPack.Pack pack, out string error)
    {
        var session = WorldParkPack.Start(pack, out error);
        if (session == null) return null;
        if (!session.Continue(out error)) return null;
        if (!session.ResolveRoom(true, out error)) return null;
        if (!session.Continue(out error)) return null;
        if (!session.Travel("venue_2", out error)) return null;
        return session;
    }

    private static WorldParkPack.Pack BuildRoomTravelPack()
    {
        return new WorldParkPack.Pack
        {
            id = "progress_room", name = "进度测试会场", premise = "原创测试", entryVenue = "venue_1",
            venues = new List<WorldParkPack.Venue>
            {
                new WorldParkPack.Venue
                {
                    id = "venue_1", name = "入口", setting = "原创广场", role = "trickster", suit = "自定义", difficulty = 1, rules = "真实房间结果", entry = "intro",
                    nodes = new List<WorldParkPack.Node>
                    {
                        new WorldParkPack.Node { id = "intro", kind = "story", next = "room" },
                        new WorldParkPack.Node { id = "room", kind = "room", room = "默认房间", onWin = "reveal", onLose = "fail" },
                        new WorldParkPack.Node { id = "reveal", kind = "reveal", next = "done" },
                        new WorldParkPack.Node { id = "done", kind = "ending", result = "clear" },
                        new WorldParkPack.Node { id = "fail", kind = "ending", result = "fail" }
                    },
                    clues = new List<WorldParkPack.Clue>
                    {
                        new WorldParkPack.Clue { id = "stage", name = "入口记录", foundAt = "intro", revealAt = "reveal", hint = "公开提示", truth = "作者秘密", trigger = "stage" }
                    }
                },
                new WorldParkPack.Venue
                {
                    id = "venue_2", name = "下一站", setting = "原创展馆", role = "player", suit = "自定义", difficulty = 1, rules = "抵达", entry = "arrival",
                    nodes = new List<WorldParkPack.Node> { new WorldParkPack.Node { id = "arrival", kind = "ending", result = "clear" } },
                    clues = new List<WorldParkPack.Clue>()
                }
            },
            links = new List<WorldParkPack.Link> { new WorldParkPack.Link { from = "venue_1", to = "venue_2", requires = "clear" } }
        };
    }

    private static WorldParkPack.Pack BuildExplorePack()
    {
        return new WorldParkPack.Pack
        {
            id = "progress_explore", name = "调查检查点", premise = "原创调查", entryVenue = "venue",
            venues = new List<WorldParkPack.Venue>
            {
                new WorldParkPack.Venue
                {
                    id = "venue", name = "现场", setting = "原创街口", role = "player", suit = "调查", difficulty = 1, rules = "先调查再离开", entry = "explore",
                    nodes = new List<WorldParkPack.Node>
                    {
                        new WorldParkPack.Node
                        {
                            id = "explore", kind = "explore", mapText = ExploreMap(), onWin = "reveal", onLose = "fail",
                            points = new List<WorldParkPack.ExplorePoint>
                            {
                                new WorldParkPack.ExplorePoint { id = "note_point", kind = "clue", clue = "note", x = 2, y = 1 },
                                new WorldParkPack.ExplorePoint { id = "exit", kind = "exit", outcome = "win", x = 3, y = 1, requires = new List<string> { "note" } }
                            }
                        },
                        new WorldParkPack.Node { id = "reveal", kind = "reveal", next = "done" },
                        new WorldParkPack.Node { id = "done", kind = "ending", result = "clear" },
                        new WorldParkPack.Node { id = "fail", kind = "ending", result = "fail" }
                    },
                    clues = new List<WorldParkPack.Clue>
                    {
                        new WorldParkPack.Clue { id = "note", name = "遗失便签", foundAt = "explore", revealAt = "reveal", hint = "公开笔记", truth = "作者秘密", trigger = "interact" }
                    }
                }
            },
            links = new List<WorldParkPack.Link>()
        };
    }

    private static string ExploreMap()
    {
        var rows = new List<string>();
        for (int y = 11; y >= 0; y--)
        {
            char[] row = new string('t', 16).ToCharArray();
            if (y > 0 && y < 11) for (int x = 1; x < 15; x++) row[x] = '.';
            if (y == 1) row[1] = 'T';
            rows.Add(new string(row));
        }
        return "# Overworld: 检查点地图\n" + string.Join("\n", rows.ToArray());
    }

    private static void AssertSameActions(List<WorldParkProgress.Action> expected, List<WorldParkProgress.Action> actual)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.AreEqual(expected[i].kind, actual[i].kind, "动作 kind #" + i);
            Assert.AreEqual(expected[i].value, actual[i].value, "动作 value #" + i);
        }
    }
}
