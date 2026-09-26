using System;
using System.Linq;
using NUnit.Framework;

public class LevelStudioDocumentTests
{
    private static LevelStudioDocument Parse(string text)
    {
        Assert.IsTrue(LevelStudioDocument.TryParse(text, out var document, out string error), error);
        return document;
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \r\n ")]
    public void EmptyInputIsRejectedWithoutMutation(string text)
    {
        Assert.IsFalse(LevelStudioDocument.TryParse(text, out var document, out string error));
        Assert.IsNull(document);
        Assert.IsNotEmpty(error);
    }

    [Test]
    public void RaggedWindowsInputIsPaddedAndBottomOriginIsStable()
    {
        var doc = Parse("M.TG\r\n##\r\n");
        Assert.AreEqual(4, doc.Width);
        Assert.AreEqual(2, doc.Height);
        Assert.AreEqual('M', doc.Cell(0, 1));
        Assert.AreEqual('.', doc.Cell(3, 0));
        Assert.AreEqual("M.TG\n##..", doc.Grid);
    }

    [Test]
    public void InternalSpacesBecomeAir()
    {
        Assert.AreEqual("M.T.G", Parse("M T G").Grid);
    }

    [TestCase('M')]
    [TestCase('T')]
    [TestCase('G')]
    public void PaintingUniqueMarkersMovesInsteadOfDuplicates(char marker)
    {
        var doc = Parse("MTG.\n####");
        doc.Paint(3, 1, marker);
        Assert.AreEqual(marker, doc.Cell(3, 1));
        Assert.AreEqual(1, doc.Grid.Count(c => c == marker));
    }

    [Test]
    public void EraseAndOutOfBoundsPaintAreSafe()
    {
        var doc = Parse("MTG.\n####");
        doc.Paint(0, 0, '.');
        string expected = doc.Text;
        doc.Paint(-1, 0, '#'); doc.Paint(4, 0, '#'); doc.Paint(0, 2, '#'); doc.Paint(0, -1, '#');
        Assert.AreEqual(expected, doc.Text);
        Assert.AreEqual('.', doc.Cell(0, 0));
    }

    [Test]
    public void UnknownInputIsRejectedRatherThanSilentlyDeleted()
    {
        Assert.IsFalse(LevelStudioDocument.TryParse("M?TG\n####", out _, out string error));
        StringAssert.Contains("?", error);
        var doc = Parse("MTG");
        Assert.Throws<ArgumentException>(() => doc.Paint(1, 0, '?'));
    }

    [Test]
    public void MaximumDocumentIsAcceptedButLargerInputsAreRejected()
    {
        string row = new string('.', LevelStudioDocument.MaxWidth);
        Assert.AreEqual(LevelStudioDocument.MaxHeight,
            Parse(string.Join("\n", Enumerable.Repeat(row, LevelStudioDocument.MaxHeight))).Height);
        Assert.IsFalse(LevelStudioDocument.TryParse(row + ".", out _, out _));
        Assert.IsFalse(LevelStudioDocument.TryParse(string.Join("\n", Enumerable.Repeat(".", LevelStudioDocument.MaxHeight + 1)), out _, out _));
        Assert.IsFalse(LevelStudioDocument.TryParse(new string('.', 32769), out _, out _));
    }

    [Test]
    public void ResizingKeepsFloorAndDoesNotMutateOriginal()
    {
        var doc = Parse("MTG\n###");
        var expanded = doc.Resize(5, 4);
        Assert.AreEqual('#', expanded.Cell(0, 0));
        Assert.AreEqual('M', expanded.Cell(0, 1));
        Assert.AreEqual('.', expanded.Cell(0, 3));
        Assert.AreEqual('.', expanded.Cell(4, 0));
        Assert.AreEqual(3, doc.Width);
        Assert.AreEqual(2, doc.Height);
    }

    [TestCase(0, 2)]
    [TestCase(2, 0)]
    [TestCase(129, 2)]
    [TestCase(2, 49)]
    public void UnsafeResizeIsRejected(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Parse("MTG").Resize(width, height));
    }

    [Test]
    public void MetadataSurvivesPaintAndResizeWithoutBecomingRows()
    {
        const string notes = "# MainRoute: upper\n# ShadowRoute: lower\n# TrapRoles: decoy\n# Budget: one\n# TestGoal: readable\n# Override_2_1: pointB=5,2";
        var doc = Parse("MTG\n###\n" + notes);
        Assert.AreEqual(2, doc.Height);
        doc.Paint(1, 0, '=');
        var expanded = doc.Resize(4, 5);
        StringAssert.EndsWith(notes, expanded.Text);
        Assert.IsFalse(expanded.Grid.Contains("MainRoute"));
        Assert.AreEqual(expanded.Text, Parse(expanded.Text).Text);
    }

    [TestCase("MTG\n###", true)]
    [TestCase("M.G\n###", false)]
    [TestCase("MMTG\n####", false)]
    [TestCase("MTGG\n####", false)]
    public void ReadinessRequiresOneOfEachMarker(string text, bool ready)
    {
        Assert.AreEqual(ready, string.IsNullOrEmpty(Parse(text).PlayReadiness()));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void StarterRoomsAreBoundedDeterministicAndReady(int index)
    {
        string source = LevelStudioDocument.Starter(index);
        var doc = Parse(source);
        Assert.AreEqual(source, LevelStudioDocument.Starter(index));
        Assert.AreEqual(32, doc.Width);
        Assert.AreEqual(9, doc.Height);
        Assert.IsEmpty(doc.PlayReadiness());
        StringAssert.Contains("一次只改", doc.DesignNudge());
        for (int x = 0; x < doc.Width; x++)
            if (doc.Cell(x, 1) == 'M' || doc.Cell(x, 1) == 'T') Assert.AreEqual('#', doc.Cell(x, 0));
    }

    [Test]
    public void DesignNudgesExplainSpawnSupportAndNearbyHazards()
    {
        StringAssert.Contains("支撑", Parse("MTG\n... ").DesignNudge());
        StringAssert.Contains("观察", Parse("M^TG\n####").DesignNudge());
    }
}
