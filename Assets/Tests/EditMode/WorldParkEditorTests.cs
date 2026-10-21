using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class WorldParkEditorTests
{
    [Test]
    public void DemoIsAnAuthorableRunnableExploreWorld()
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/WorldPark/Demo.worldpark.json");
        Assert.IsNotNull(asset);
        var pack = WorldParkPack.Parse(asset.text, out string error);
        Assert.IsNotNull(pack, error);
        Assert.IsNotNull(WorldParkPack.GetVenue(pack, pack.entryVenue));
        Assert.IsTrue(pack.venues.SelectMany(v => v.nodes).Any(n => n.kind == "explore"), "默认世界应包含探索阶段，而不是仅守宝流程。");
        Assert.IsNotNull(WorldParkPack.Start(pack, out error), error);
    }

    [Test]
    public void PlayerExploreReadinessNeedsNoRoomVariant()
    {
        var pack = CreatePlayerExplorePack();
        Assert.IsTrue(WorldParkBuilder.CheckRunReadiness(pack, out string error), error);
        Assert.AreEqual("@explore/venue/explore", WorldParkRuntime.ExploreRouteKey("venue", "explore"));
    }

    [Test]
    public void ReadinessRejectsUnknownRoomWithoutChangingBuildSettingsOrCustomRoomFile()
    {
        var pack = new WorldParkPack.Pack
        {
            id = "bad_room_world",
            name = "Bad room",
            premise = "test",
            entryVenue = "venue",
            venues = new List<WorldParkPack.Venue>
            {
                new WorldParkPack.Venue
                {
                    id = "venue",
                    name = "venue",
                    role = "trickster",
                    entry = "room",
                    nodes = new List<WorldParkPack.Node>
                    {
                        new WorldParkPack.Node { id = "room", kind = "room", room = "不存在的世界房间_9fa8", onWin = "win", onLose = "lose" },
                        new WorldParkPack.Node { id = "win", kind = "ending", result = "clear" },
                        new WorldParkPack.Node { id = "lose", kind = "ending", result = "fail" }
                    },
                    clues = new List<WorldParkPack.Clue>()
                }
            },
            links = new List<WorldParkPack.Link>()
        };
        var old = EditorBuildSettings.scenes;
        string path = Step1PrankRoomBuilder.CustomRoomPath;
        string before = File.Exists(path) ? File.ReadAllText(path) : null;
        Assert.IsFalse(WorldParkBuilder.CheckRunReadiness(pack, out string error));
        StringAssert.Contains("找不到房间", error);
        var after = EditorBuildSettings.scenes;
        Assert.AreEqual(old.Length, after.Length);
        for (int i = 0; i < old.Length; i++)
        {
            Assert.AreEqual(old[i].path, after[i].path);
            Assert.AreEqual(old[i].enabled, after[i].enabled);
        }
        Assert.AreEqual(before, File.Exists(path) ? File.ReadAllText(path) : null);
    }

    [Test]
    public void EmptyWorldCleanupDoesNotChangeExistingModeClock()
    {
        WorldParkRuntime.ClearSession();
        float old = Time.timeScale;
        try
        {
            Time.timeScale = 0.5f;
            WorldParkRuntime.ClearSession();
            Assert.AreEqual(0.5f, Time.timeScale);
        }
        finally { Time.timeScale = old; }
    }

    private static WorldParkPack.Pack CreatePlayerExplorePack()
    {
        const string map =
            "# Overworld: 编辑器探索验证\n" +
            "tttttttttttttttt\n" +
            "t..............t\n" +
            "t..............t\n" +
            "t..............t\n" +
            "t....T.........t\n" +
            "t..............t\n" +
            "t..............t\n" +
            "t..............t\n" +
            "t..............t\n" +
            "t..............t\n" +
            "t..............t\n" +
            "tttttttttttttttt\n";

        return new WorldParkPack.Pack
        {
            id = "player_explore_world",
            name = "Player explore",
            premise = "test",
            entryVenue = "venue",
            venues = new List<WorldParkPack.Venue>
            {
                new WorldParkPack.Venue
                {
                    id = "venue",
                    name = "venue",
                    role = "player",
                    entry = "explore",
                    nodes = new List<WorldParkPack.Node>
                    {
                        new WorldParkPack.Node
                        {
                            id = "explore",
                            kind = "explore",
                            name = "调查",
                            mapText = map,
                            timeout = 0,
                            onWin = "reveal_win",
                            onLose = "reveal_lose",
                            points = new List<WorldParkPack.ExplorePoint>
                            {
                                new WorldParkPack.ExplorePoint { id = "notice", kind = "clue", label = "告示", clue = "evidence", x = 6, y = 5, requires = new List<string>() },
                                new WorldParkPack.ExplorePoint { id = "leave", kind = "exit", label = "出口", outcome = "win", x = 9, y = 5, requires = new List<string> { "evidence" } }
                            }
                        },
                        new WorldParkPack.Node { id = "reveal_win", kind = "reveal", next = "win" },
                        new WorldParkPack.Node { id = "reveal_lose", kind = "reveal", next = "lose" },
                        new WorldParkPack.Node { id = "win", kind = "ending", result = "clear" },
                        new WorldParkPack.Node { id = "lose", kind = "ending", result = "fail" }
                    },
                    clues = new List<WorldParkPack.Clue>
                    {
                        new WorldParkPack.Clue { id = "evidence", name = "证词", trigger = "interact", foundAt = "explore", revealAt = "reveal_win", hint = "有人留下了字条", truth = "字条指向出口" }
                    }
                }
            },
            links = new List<WorldParkPack.Link>()
        };
    }
}
