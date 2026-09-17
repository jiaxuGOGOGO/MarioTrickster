using System;
using System.Linq;
using NUnit.Framework;

public class MechanismExplorationPlanTests
{
    [TestCase(MechanismExplorationPlan.Scope.Smoke, 6)]
    [TestCase(MechanismExplorationPlan.Scope.Mechanisms, 38)]
    [TestCase(MechanismExplorationPlan.Scope.Pairwise, 190)]
    public void PlansHaveExplicitBoundedBudgets(MechanismExplorationPlan.Scope scope, int count)
    {
        var plan = MechanismExplorationPlan.Create(153, scope);
        Assert.AreEqual(count, plan.Count);
        Assert.AreEqual(count, plan.Select(s => s.id).Distinct().Count());
        Assert.AreEqual(3, MechanismExplorationPlan.Profiles.Length);
    }

    [TestCase(0)]
    [TestCase(153)]
    [TestCase(-1)]
    [TestCase(int.MaxValue)]
    [TestCase(int.MinValue)]
    public void SameSeedReproducesEveryLayoutAndMetadata(int seed)
    {
        var a = MechanismExplorationPlan.Create(seed, MechanismExplorationPlan.Scope.Pairwise);
        var b = MechanismExplorationPlan.Create(seed, MechanismExplorationPlan.Scope.Pairwise);
        CollectionAssert.AreEqual(a.Select(s => s.ascii), b.Select(s => s.ascii));
        CollectionAssert.AreEqual(a.Select(s => s.id), b.Select(s => s.id));
        CollectionAssert.AreEqual(a.Select(s => s.mechanisms), b.Select(s => s.mechanisms));
    }

    [Test]
    public void PairwisePlanCoversEveryUnorderedCatalogPairExactlyOnce()
    {
        string catalog = MechanismExplorationPlan.Catalog;
        Assert.AreEqual(19, catalog.Length);
        Assert.AreEqual(catalog.Length, catalog.Distinct().Count());
        var plan = MechanismExplorationPlan.Create(8, MechanismExplorationPlan.Scope.Pairwise);
        var pairs = plan.Where(s => s.mechanisms.Length == 2).Select(s => MechanismExplorationPlan.PairKey(s.mechanisms[0], s.mechanisms[1])).ToArray();
        Assert.AreEqual(171, pairs.Length);
        Assert.AreEqual(pairs.Length, pairs.Distinct().Count());
        foreach (char a in catalog)
            foreach (char b in catalog)
                if (a != b) CollectionAssert.Contains(pairs, MechanismExplorationPlan.PairKey(a, b));
    }

    [Test]
    public void MechanismPlanContainsEverySingleMechanism()
    {
        var plan = MechanismExplorationPlan.Create(10, MechanismExplorationPlan.Scope.Mechanisms);
        CollectionAssert.AreEquivalent(MechanismExplorationPlan.Catalog.Select(c => c.ToString()),
            plan.Where(s => s.mechanisms.Length == 1).Select(s => s.mechanisms));
    }

    [Test]
    public void DifferentSeedsVaryTheRooms()
    {
        Assert.IsFalse(MechanismExplorationPlan.Create(1, MechanismExplorationPlan.Scope.Smoke).Select(s => s.ascii)
            .SequenceEqual(MechanismExplorationPlan.Create(2, MechanismExplorationPlan.Scope.Smoke).Select(s => s.ascii)));
    }

    [TestCase(153)]
    [TestCase(-97)]
    [TestCase(0)]
    public void AllGeneratedRoomsKeepSpawnsRecoveryZonesAndRequestedMechanisms(int seed)
    {
        foreach (var scenario in MechanismExplorationPlan.Create(seed, MechanismExplorationPlan.Scope.Pairwise))
        {
            Assert.IsTrue(LevelStudioDocument.TryParse(scenario.ascii, out var doc, out string error), scenario.id + " " + error);
            Assert.IsEmpty(doc.PlayReadiness());
            Assert.AreEqual(40, doc.Width);
            Assert.AreEqual(10, doc.Height);
            Assert.AreEqual('M', doc.Cell(2, 1)); Assert.AreEqual('T', doc.Cell(7, 1)); Assert.AreEqual('G', doc.Cell(37, 1));
            for (int x = 0; x < 8; x++) Assert.AreEqual('#', doc.Cell(x, 0));
            for (int x = 29; x < 37; x++) { Assert.AreEqual('#', doc.Cell(x, 0)); Assert.AreEqual('.', doc.Cell(x, 1)); }
            foreach (char mechanism in scenario.mechanisms) StringAssert.Contains(mechanism.ToString(), doc.Grid);
            Assert.AreEqual(scenario.mechanisms.Contains('o'), scenario.lootEscape);
            if (scenario.mechanisms.Contains('>')) StringAssert.Contains("# Override_", doc.Text);
            Assert.IsFalse(doc.Grid.Contains("Override"));
        }
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("?")]
    [TestCase("BB")]
    [TestCase("BCX")]
    public void UnsupportedOrAmbiguousCombinationsAreRejected(string input)
    { Assert.Throws<ArgumentException>(() => MechanismExplorationPlan.Build(1, input)); }

    [Test]
    public void UnknownScopeIsRejected()
    { Assert.Throws<ArgumentOutOfRangeException>(() => MechanismExplorationPlan.Create(1, (MechanismExplorationPlan.Scope)99)); }

    [Test]
    public void NewRegistryEntriesAreReportedAsUnsupportedNotSilentlyCounted()
    {
        CollectionAssert.AreEqual(new[] { "Z", "?" }, MechanismExplorationPlan.MissingFromCatalog(" .#=WMTGBCZZ?"));
        Assert.IsEmpty(MechanismExplorationPlan.MissingFromCatalog(AsciiElementRegistry.GetDefault().GetAllRegisteredChars()));
    }

    [Test]
    public void PhysicalContactIsNeverLabeledAsActivation()
    {
        var e = new MechanismExplorationPlan.Evidence { built = 1, approached = true, contacts = 1 };
        StringAssert.Contains("激活未证实", e.Status);
        Assert.AreEqual(0, e.activations);
        e.activations = 1;
        StringAssert.Contains("已激活", e.Status);
    }

    [Test]
    public void ClearedButUnobservedOrErrorTrialsAreNotHumanPlayCandidates()
    {
        var trial = new MechanismExplorationPlan.Trial { outcome = "Cleared" };
        Assert.IsFalse(trial.CandidateForHumanPlay, "An empty observation cannot qualify as a tested candidate");
        trial.coverage.Add(new MechanismExplorationPlan.Evidence { built = 1, approached = true });
        Assert.IsFalse(trial.CandidateForHumanPlay);
        trial.coverage[0].contacts = 1;
        Assert.IsTrue(trial.CandidateForHumanPlay);
        trial.errors.Add("runtime error"); Assert.IsFalse(trial.CandidateForHumanPlay);
    }

    [Test]
    public void ConfirmationIsBoundedDeduplicatedAndNeverRetriesInfrastructure()
    {
        var trials = Enumerable.Range(0, 30).Select(i => new MechanismExplorationPlan.Trial {
            scenarioId = "room" + (i / 3), profile = "Explorer", seconds = 20, outcome = "NoProgress"
        }).ToList();
        trials.Insert(0, new MechanismExplorationPlan.Trial { scenarioId = "boot", outcome = "StartupFailed" });
        var selected = MechanismExplorationPlan.SelectConfirmationScenes(trials);
        Assert.AreEqual(6, selected.Length);
        Assert.AreEqual(6, selected.Distinct().Count());
        Assert.IsFalse(selected.Contains("boot"));
        foreach (var trial in trials) trial.attempt = 2;
        Assert.IsEmpty(MechanismExplorationPlan.SelectConfirmationScenes(trials), "Never recursively retry the confirmation pass");
    }

    [Test]
    public void SameStartupFaultStopsButGameplayLossesDoNotTripCircuitBreaker()
    {
        var a = new MechanismExplorationPlan.Trial { outcome = "StartupFailed" };
        var b = new MechanismExplorationPlan.Trial { outcome = "StartupFailed" };
        a.errors.Add("ArgumentException: font\nstack A"); b.errors.Add("ArgumentException: font\nstack B");
        Assert.IsTrue(MechanismExplorationPlan.RepeatedInfrastructureFailure(new[] { a, b }));
        b.errors[0] = "Different exception";
        Assert.IsFalse(MechanismExplorationPlan.RepeatedInfrastructureFailure(new[] { a, b }));
        a.outcome = b.outcome = "RunnerStopped";
        Assert.IsFalse(MechanismExplorationPlan.RepeatedInfrastructureFailure(new[] { a, b }));
    }

    [Test]
    public void ConfirmationChangesAreLabeledUnstableNotFixed()
    {
        var a = new MechanismExplorationPlan.Trial { outcome = "NoProgress", seconds = 12 };
        var b = new MechanismExplorationPlan.Trial { outcome = "NoProgress", seconds = 13, attempt = 2 };
        StringAssert.Contains("同条件复现", MechanismExplorationPlan.CompareConfirmation(a, b));
        b.outcome = "Cleared";
        StringAssert.Contains("不是已修复", MechanismExplorationPlan.CompareConfirmation(a, b));
        Assert.IsFalse(new MechanismExplorationPlan.Trial { outcome = "StartupFailed" }.HasGameplayEvidence);
    }

    [Test]
    public void UploadedSmokeSeedDoesNotMislabelLowerFloorAsBottomlessGap()
    {
        foreach (var scenario in MechanismExplorationPlan.Create(153, MechanismExplorationPlan.Scope.Smoke))
        {
            LevelStudioDocument.TryParse(scenario.ascii, out var doc, out _);
            var result = AsciiLevelValidator.ValidateTemplate(doc.Grid);
            Assert.IsFalse(result.errors.Any(e => e.Contains("IMPASSABLE gap")), scenario.id + " " + result.GetReport());
        }
    }

    [Test]
    public void ActualWideBottomlessPitStillFailsValidation()
    {
        var result = AsciiLevelValidator.ValidateTemplate("....................\nM..................G\n####............####");
        Assert.IsTrue(result.errors.Any(e => e.Contains("IMPASSABLE gap")), "Do not silence genuine unreachable pits");
    }

    [Test]
    public void DirectReplayPreservesOpeningEdgeAndEverySegmentDuration()
    {
        var replay = new AutomatedInputProvider(new System.Collections.Generic.List<InputFrame> {
            new InputFrame { duration = 1, p1JumpDown = true, p1JumpHeld = true },
            new InputFrame { duration = 3, p1Horizontal = 1 },
            new InputFrame { duration = 1, p1ScanDown = true }
        });
        Assert.IsTrue(replay.GetP1JumpDown());
        replay.Tick();
        for (int i = 0; i < 3; i++) { Assert.AreEqual(1f, replay.GetP1Horizontal()); Assert.IsFalse(replay.GetP1JumpDown()); replay.Tick(); }
        Assert.IsTrue(replay.GetP1ScanDown());
        replay.Tick(); Assert.IsTrue(replay.IsFinished); Assert.IsFalse(replay.GetP1ScanDown());
        replay.Reset(); Assert.IsTrue(replay.GetP1JumpDown());
    }

    [Test]
    public void PairCoverageRequiresBothMechanismsToBeContactedOrActivated()
    {
        var trial = new MechanismExplorationPlan.Trial();
        trial.coverage.Add(new MechanismExplorationPlan.Evidence { built = 1, contacts = 1 });
        trial.coverage.Add(new MechanismExplorationPlan.Evidence { built = 1, approached = true });
        Assert.IsFalse(trial.PairExercised);
        trial.coverage[1].activations = 1;
        Assert.IsTrue(trial.PairExercised);
    }
}
