using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class WorldParkJourneyTests
{
    private static WorldParkPack.Pack Demo()
    {
        var pack = WorldParkPack.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Resources/WorldPark/Demo.worldpark.json")), out string error);
        Assert.IsNotNull(pack, error);
        return pack;
    }

    [Test]
    public void DefaultJourneyHasThreeAuthorableVenuesAndCustomResults()
    {
        var pack = Demo();
        Assert.AreEqual(3, pack.venues.Count);
        Assert.IsFalse(WorldParkPack.Validate(pack).Any(i => i.severity == "error"));
        Assert.IsTrue(pack.venues.SelectMany(v => v.nodes).Any(n => n.result == "recorded"));
        Assert.IsTrue(pack.venues.SelectMany(v => v.nodes).Any(n => n.kind == "explore" && n.timeout > 0));
        Assert.IsFalse(pack.venues.SelectMany(v => v.nodes).Any(n => n.kind == "room"));
    }

    [Test]
    public void SoftFailureBranchNeverOverwritesTheEarlierFailure()
    {
        var session = WorldParkPack.Start(Demo(), out string error);
        Assert.IsTrue(session.Continue(out error), error);
        Assert.IsTrue(session.ResolveExplore(false, out error), error);
        Assert.IsTrue(session.Continue(out error), error);
        Assert.IsFalse(session.CanTravel("venue_gallery"));
        Assert.IsTrue(session.CanTravel("venue_archive"));
        Assert.IsTrue(session.Travel("venue_archive", out error), error);
        Assert.IsTrue(session.Continue(out error), error);
        Assert.IsTrue(session.ResolveExplore(true, out error), error);
        Assert.IsTrue(session.Continue(out error), error);
        Assert.AreEqual("fail", session.results["venue_town"]);
        Assert.AreEqual("recorded", session.results["venue_archive"]);
        Assert.AreEqual(0, session.roomResults.Count);
        Assert.AreEqual(2, session.activityResults.Count);
        Assert.IsFalse(session.activityResults[0].won);
        Assert.IsTrue(session.activityResults[1].won);
    }

    [Test]
    public void SuccessfulActionsAreRecordedButRepeatedObservationAndRejectedActionsAreNot()
    {
        var session = WorldParkPack.Start(Demo(), out string error);
        Assert.IsFalse(session.ResolveExplore(true, out error));
        Assert.AreEqual(0, session.actions.Count);
        Assert.IsTrue(session.Continue(out error), error);
        Assert.IsTrue(session.ObserveClue("notice", out error), error);
        Assert.IsTrue(session.ObserveClue("notice", out error), error);
        Assert.AreEqual(2, session.actions.Count);
        Assert.AreEqual("continue", session.actions[0].kind);
        Assert.AreEqual("observe", session.actions[1].kind);
        Assert.AreEqual("notice", session.actions[1].value);
    }

    [Test]
    public void CustomResultUnlocksMatchingLinkAndAnyIsReservedAsWildcard()
    {
        var pack = Demo();
        pack.venues[0].nodes.First(n => n.id == "clear_end").result = "pass_with_ticket";
        pack.links.First(l => l.to == "venue_gallery").requires = "pass_with_ticket";
        var session = WorldParkPack.Start(pack, out string error);
        Assert.IsNotNull(session, error);
        Assert.IsTrue(session.Continue(out error), error);
        Assert.IsTrue(session.ResolveExplore(true, out error), error);
        Assert.IsTrue(session.Continue(out error), error);
        Assert.IsTrue(session.CanTravel("venue_gallery"));
        Assert.AreEqual("pass_with_ticket", session.results["venue_town"]);
        pack.venues[0].nodes.First(n => n.id == "clear_end").result = "any";
        Assert.IsTrue(WorldParkPack.Validate(pack).Any(i => i.severity == "error" && i.code == "ending_result_invalid"));
    }

    [Test]
    public void ExploreAdapterCanFindOwnerOnTheSameGeneratedObject()
    {
        var host = new GameObject("WorldPark owner fixture");
        try
        {
            var owner = host.AddComponent<WorldParkRuntime>();
            var explore = host.AddComponent<WorldParkExploreRuntime>();
            Assert.AreSame(owner, explore.GetComponent<WorldParkRuntime>());
            string builder = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Editor/WorldParkBuilder.cs"));
            StringAssert.Contains("runtime.AddComponent<WorldParkExploreRuntime>()", builder);
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }
}
