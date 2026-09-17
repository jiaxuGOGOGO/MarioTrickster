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
