using System.Collections.Generic;
using NUnit.Framework;

public class WorldParkSpatialTests
{
    [Test]
    public void PlacementUsesBottomOriginAndRejectsBlockedOrUnreachableCells()
    {
        const string map =
            "# Overworld: 空间编辑测试\n" +
            "ttttttt\n" +
            "t..W..t\n" +
            "t.TW..t\n" +
            "t..W..t\n" +
            "ttttttt\n";

        Assert.IsTrue(WorldParkSpatialEditor.TryGetPlacementCell(map, 2, 2, out string spawnMessage), spawnMessage);
        Assert.IsTrue(WorldParkSpatialEditor.TryGetPlacementCell(map, 1, 1, out string lowerMessage), lowerMessage);

        Assert.IsFalse(WorldParkSpatialEditor.TryGetPlacementCell(map, 3, 2, out string wallMessage));
        StringAssert.Contains("阻挡", wallMessage);

        // (4,2) 是地图中的草地，但竖直墙隔开；编辑器不让它成为运行时不可达的现场点。
        Assert.IsFalse(WorldParkSpatialEditor.TryGetPlacementCell(map, 4, 2, out string unreachableMessage));
        StringAssert.Contains("不可达", unreachableMessage);

        Assert.IsFalse(WorldParkSpatialEditor.TryGetPlacementCell(map, 1, 5, out string outsideMessage));
        StringAssert.Contains("超出", outsideMessage);
    }

    [Test]
    public void PointHitTestPreservesExistingListOrderForSameCell()
    {
        var points = new List<WorldParkPack.ExplorePoint>
        {
            new WorldParkPack.ExplorePoint { id = "first", kind = "clue", x = 4, y = 3 },
            new WorldParkPack.ExplorePoint { id = "second", kind = "exit", x = 4, y = 3 },
            new WorldParkPack.ExplorePoint { id = "other", kind = "clue", x = 2, y = 1 }
        };

        Assert.AreEqual(0, WorldParkSpatialEditor.PointIndexAt(points, 4, 3));
        Assert.AreEqual(2, WorldParkSpatialEditor.PointIndexAt(points, 2, 1));
        Assert.AreEqual(-1, WorldParkSpatialEditor.PointIndexAt(points, 0, 0));
    }
}
