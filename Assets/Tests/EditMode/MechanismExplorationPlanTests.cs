using System;
using System.Linq;
using NUnit.Framework;

public class MechanismExplorationPlanTests
{
    [TestCase(-1f)]
    [TestCase(1f)]
    public void EncounterRetainsDamageAcrossRetreatAndSeparatesReturn(float direction)
    {
        var crossing = new MechanismExplorationPlan.QueueCrossingMemory();
        var encounter = new MechanismExplorationPlan.QueueEncounterMemory();
        Action<float, int> sample = (x, loss) => {
            int flags = crossing.Sample(x * direction, 0, 2, loss);
            encounter.Sample(x * direction, 0, 2, loss, flags);
        };
        sample(-4, 0); sample(-1, 1); sample(-5, 1);
        for (int i = 0; i < 100; i++) sample(-5, 1);
        sample(-1, 1); sample(3, 1);
        Assert.AreEqual(1, encounter.Started); Assert.AreEqual(1, encounter.Completed);
        Assert.AreEqual(0, encounter.CleanCompleted, "S163 timing2 Runner: re-entry must not erase the first injury");
        sample(2.5f, 1); sample(1, 1); sample(-3, 1);
        Assert.AreEqual(2, encounter.Completed); Assert.AreEqual(1, encounter.CleanCompleted);
    }

    [Test]
    public void EncounterBypassAndSpawnInsideDoNotInventCleanCompletion()
    {
        var crossing = new MechanismExplorationPlan.QueueCrossingMemory();
        var encounter = new MechanismExplorationPlan.QueueEncounterMemory();
        Action<float, float> sample = (x, y) => encounter.Sample(x, y, 2, 0, crossing.Sample(x, y, 2, 0));
        sample(-3, 0); sample(-1, 0); sample(0, 4); sample(3, 0);
        Assert.AreEqual(1, encounter.Bypassed); Assert.AreEqual(0, encounter.Completed);
        crossing = new MechanismExplorationPlan.QueueCrossingMemory();
        encounter = new MechanismExplorationPlan.QueueEncounterMemory();
        sample(0, 0); sample(3, 0);
        Assert.AreEqual(0, encounter.Started); Assert.AreEqual(0, encounter.CleanCompleted);
    }

    [Test]
    public void AdaptiveCannotBorrowCueFromAnotherQueueOrCleanFragmentFromDamagedEncounter()
    {
        var t = new MechanismExplorationPlan.Trial { experience = "BaitAndCounter", counterplayVersion = 1,
            experienceEvidenceVersion = 1, queueEvidenceVersion = 2, marioStrategy = "Adaptive", tricksterStrategy = "Passive" };
        t.queueEvidence.Add(new MechanismExplorationPlan.QueueEvidence { source = "A", cueSamples = 1, cleanCrossings = 1 });
        t.queueEvidence.Add(new MechanismExplorationPlan.QueueEvidence { source = "B", cleanEncounters = 1 });
        Assert.IsNotEmpty(t.ExperienceGaps);
        t.queueEvidence.RemoveAt(1);
        Assert.IsNotEmpty(t.ExperienceGaps, "A clean re-entry fragment does not certify the encounter");
        t.queueEvidence[0].cleanEncounters = 1;
        Assert.IsEmpty(t.ExperienceGaps);
    }

    [Test]
    public void DelayedOpponentRequiresEveryDecisionFrameAndLegacyEvidenceStaysMissing()
    {
        var t = new MechanismExplorationPlan.Trial { experience = "LootAndReturn", counterplayVersion = 1,
            experienceEvidenceVersion = 1, marioStrategy = "Adaptive", tricksterStrategy = "Chaser",
            armedNearbySeconds = 1, startDelaySeconds = 0.6f, expectsReturn = true,
            lootEvents = 1, escapeEvents = 1, possessionTransfers = 1, postLootTransfers = 1 };
        Assert.IsFalse(MechanismExplorationPlan.IndependentStartObserved(t));
        Assert.IsTrue(t.ExperienceGaps.Any(g => g.Contains("起步等待")));
        Assert.AreEqual(0, t.startTimingEvidenceVersion, "Reading an old report must not synthesize observations");
        t.startTimingEvidenceVersion = 1; t.startWaitFrames = 30; t.opponentWaitDecisionFrames = 29;
        Assert.IsFalse(MechanismExplorationPlan.IndependentStartObserved(t));
        t.opponentWaitDecisionFrames = 30;
        Assert.IsTrue(MechanismExplorationPlan.IndependentStartObserved(t));
        Assert.IsEmpty(t.ExperienceGaps);
        t.tricksterStrategy = "Passive"; t.opponentWaitInputFrames = 1;
        Assert.IsFalse(MechanismExplorationPlan.PassiveControlClean(t));
        t.opponentWaitInputFrames = 0;
        Assert.IsTrue(MechanismExplorationPlan.PassiveControlClean(t));
    }

    [TestCase(float.PositiveInfinity, 5f, 9f)]
    [TestCase(1f, float.NaN, 9f)]
    [TestCase(1f, -1f, 9f)]
    [TestCase(1f, 5f, float.PositiveInfinity)]
    [TestCase(1f, 5f, float.NaN)]
    public void InvalidQueueEstimatesNeverPermitEntry(float remaining, float distance, float speed)
    {
        Assert.IsFalse(MechanismExplorationPlan.QueueWindowAllowsEntry(true, true, remaining, distance, speed));
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(-1f)]
    public void InvalidTrialClocksAreNotGameplayEvidence(float seconds)
    {
        var t = new MechanismExplorationPlan.Trial { seconds = seconds, outcome = "Cleared", startDelaySeconds = seconds };
        t.coverage.Add(new MechanismExplorationPlan.Evidence { built = 1, contacts = 1 });
        Assert.IsFalse(t.HasGameplayEvidence);
        Assert.IsFalse(t.CandidateForHumanPlay);
        Assert.IsFalse(MechanismExplorationPlan.IndependentStartObserved(t));
    }

    [Test]
    public void CounterplayScheduleUsesMatchedLayoutsAndBoundedThreeOrFourWayPairs()
    {
        var rooms = MechanismExplorationPlan.Create(154, MechanismExplorationPlan.Scope.Counterplay);
        Assert.AreEqual(6, rooms.Count); Assert.AreEqual(21, MechanismExplorationPlan.TrialCount(rooms));
        Assert.AreEqual(6, rooms.Select(r => r.id).Distinct().Count());
        foreach (var group in rooms.GroupBy(r => r.experience))
        {
            Assert.AreEqual(1, group.Select(r => r.ascii).Distinct().Count(), "Timing variants must not also change geometry");
            CollectionAssert.AreEqual(new[] { 0f, 0.6f, 1.2f }, group.Select(r => r.startDelaySeconds));
        }
        var confirmations = rooms.Select(r => r.id).ToList();
        for (int i = 0; i < 42; i++)
        {
            var slot = MechanismExplorationPlan.TrialAt(rooms, confirmations, i);
            Assert.AreEqual(i < 21 ? 1 : 2, slot.attempt);
            Assert.IsTrue(slot.matchup.trickster == "Passive" || slot.matchup.trickster == "Chaser");
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => MechanismExplorationPlan.TrialAt(rooms, confirmations, 42));
        Assert.AreEqual(27, MechanismExplorationPlan.TrialCount(MechanismExplorationPlan.Create(154, MechanismExplorationPlan.Scope.Experience)));
        Assert.AreEqual(18, MechanismExplorationPlan.TrialCount(MechanismExplorationPlan.Create(154, MechanismExplorationPlan.Scope.Smoke)));
    }

    [Test]
    public void CounterplayHundredSeedsKeepAuthoredRoutesAndStaticValidity()
    {
        for (int seed = 100; seed < 200; seed++)
        {
            var rooms = MechanismExplorationPlan.Create(seed, MechanismExplorationPlan.Scope.Counterplay);
            foreach (var room in rooms)
            {
                int kind = room.lootEscape ? 2 : 1;
                var original = MechanismExplorationPlan.BuildExperience(unchecked(seed + kind * 7919), kind);
                Assert.AreEqual(original.ascii, room.ascii);
                Assert.IsEmpty(AsciiLevelValidator.ValidateTemplate(room.ascii).errors);
                CollectionAssert.AreEqual(original.routes.SelectMany(r => r.points).Select(p => p.x + ":" + p.y),
                    room.routes.SelectMany(r => r.points).Select(p => p.x + ":" + p.y));
            }
        }
    }

    [TestCase(true, true, 0.9f, 5f, 9f, true)]
    [TestCase(false, true, 0.9f, 5f, 9f, false)]
    [TestCase(true, false, 5f, 5f, 9f, false)]
    [TestCase(true, true, 0.2f, 5f, 9f, false)]
    [TestCase(true, true, 0.9f, 5f, 0f, false)]
    [TestCase(true, true, float.NaN, 5f, 9f, false)]
    public void QueueDecisionRequiresReadableSafeAndLongEnoughWindow(bool visible, bool safe, float remaining, float distance, float speed, bool expected)
    {
        Assert.AreEqual(expected, MechanismExplorationPlan.QueueWindowAllowsEntry(visible, safe, remaining, distance, speed));
    }

    [TestCase(-1f)]
    [TestCase(1f)]
    public void QueueCrossingRequiresWholeBodyExitAndIncludesEntryFrameDamage(float direction)
    {
        var memory = new MechanismExplorationPlan.QueueCrossingMemory();
        Assert.AreEqual(0, memory.Sample(-3 * direction, 0, 2, 0));
        Assert.AreEqual(1, memory.Sample(-1 * direction, 0, 2, 1));
        Assert.AreEqual(0, memory.Sample(1 * direction, 0, 2, 1));
        Assert.AreEqual(2, memory.Sample(3 * direction, 0, 2, 1), "Entry-frame damage cannot be a clean crossing");
        memory.Sample(direction, 0, 2, 1);
        Assert.AreEqual(6, memory.Sample(-3 * direction, 0, 2, 1), "Real clean return crossing is a separate event");
    }

    [Test]
    public void QueueMemoryRejectsRetreatUpperBypassAndSpawnInsideAsFullCrossing()
    {
        var memory = new MechanismExplorationPlan.QueueCrossingMemory();
        memory.Sample(-3, 0, 2, 0); memory.Sample(-1, 0, 2, 0);
        Assert.AreEqual(0, memory.Sample(-3, 0, 2, 0), "Same-side retreat is not crossing");
        memory.Sample(-1, 0, 2, 0); memory.Sample(0, 4, 2, 0);
        Assert.AreEqual(0, memory.Sample(3, 0, 2, 0), "Upper bypass is not queue traversal");
        var spawnedInside = new MechanismExplorationPlan.QueueCrossingMemory();
        spawnedInside.Sample(0, 0, 2, 0);
        Assert.AreEqual(0, spawnedInside.Sample(3, 0, 2, 0));
    }

    [Test]
    public void CounterplayAdaptiveClearNeedsCueAndCleanCrossingButSafeRouteMayBypass()
    {
        var t = new MechanismExplorationPlan.Trial { counterplayVersion = 1, experienceEvidenceVersion = 1,
            experience = "BaitAndCounter", marioStrategy = "Adaptive", tricksterStrategy = "Passive", outcome = "Cleared", seconds = 5 };
        Assert.IsNotEmpty(MechanismExplorationPlan.ExperienceIssues(t));
        t.queueEvidenceVersion = 1;
        t.queueEvidence.Add(new MechanismExplorationPlan.QueueEvidence { cueSamples = 1, crossings = 1 });
        Assert.IsNotEmpty(MechanismExplorationPlan.ExperienceIssues(t), "A damaged crossing is not accepted for Adaptive");
        t.queueEvidence[0].cleanCrossings = 1;
        Assert.IsEmpty(MechanismExplorationPlan.ExperienceIssues(t));
        t.controlAccepted = 1; Assert.IsNotEmpty(MechanismExplorationPlan.ExperienceIssues(t), "Passive contamination must be exposed");
        t.controlAccepted = 0; t.marioStrategy = "SafeRoute"; t.queueEvidence.Clear();
        t.completedRoutes.Add("Out:upper"); Assert.IsEmpty(MechanismExplorationPlan.ExperienceIssues(t));
    }

    [Test]
    public void CounterplayReturnNeedsPostLootTransferRatherThanOnlyOutboundSwitch()
    {
        var t = new MechanismExplorationPlan.Trial { counterplayVersion = 1, experienceEvidenceVersion = 1,
            experience = "LootAndReturn", expectsReturn = true, marioStrategy = "SafeRoute", tricksterStrategy = "Chaser",
            lootEvents = 1, escapeEvents = 1, possessionTransfers = 2 };
        t.completedRoutes.Add("Out:upper"); t.completedRoutes.Add("Return:upper");
        Assert.IsNotEmpty(MechanismExplorationPlan.ExperienceIssues(t));
        t.postLootTransfers = 1;
        Assert.IsEmpty(MechanismExplorationPlan.ExperienceIssues(t));
        t.counterplayVersion = 0; t.postLootTransfers = 0;
        Assert.IsEmpty(MechanismExplorationPlan.ExperienceIssues(t), "Legacy reports must not acquire new unsupported requirements");
    }

    [Test]
    public void CounterplayPairsNeverBorrowConfirmationOrClaimUnfinishedRunsImproved()
    {
        var room = MechanismExplorationPlan.Create(154, MechanismExplorationPlan.Scope.Counterplay)[0];
        var report = new StudioExplorationRunner.Report(); report.scenarios.Add(room);
        var rush = new MechanismExplorationPlan.Trial { scenarioId = room.id, marioStrategy = "Runner", tricksterStrategy = "Passive",
            counterplayVersion = 1, healthEvidenceVersion = 1, outcome = "Cleared", seconds = 5, runnerHealthLost = 1 };
        var reader = new MechanismExplorationPlan.Trial { scenarioId = room.id, marioStrategy = "Adaptive", tricksterStrategy = "Passive",
            counterplayVersion = 1, healthEvidenceVersion = 1, outcome = "Cleared", seconds = 7, attempt = 2 };
        report.trials.Add(rush); report.trials.Add(reader);
        StringAssert.DoesNotContain("实际损血差=", StudioExplorationRunner.CounterplayPairs(report));
        reader.attempt = 1;
        StringAssert.Contains("实际损血差=-1", StudioExplorationRunner.CounterplayPairs(report));
        reader.outcome = "TimedOut";
        StringAssert.DoesNotContain("实际损血差=", StudioExplorationRunner.CounterplayPairs(report));
        reader.outcome = "Cleared"; reader.startDelaySeconds = 0.6f;
        StringAssert.DoesNotContain("实际损血差=", StudioExplorationRunner.CounterplayPairs(report), "Unexecuted timing perturbation cannot be a matched pair");
        reader.actualStartWaitSeconds = 0.6f; reader.seconds = 0;
        StringAssert.DoesNotContain("实际损血差=", StudioExplorationRunner.CounterplayPairs(report), "Zero-duration clear is not played evidence");
        Assert.AreEqual(2, report.trials.Count);
        StringAssert.Contains("未记录", StudioExplorationRunner.QueueSummary(new MechanismExplorationPlan.Trial()));
    }

    [Test]
    public void ExpiringVisitSkipsAlreadyObservedNextTargetImmediately()
    {
        var visits = new MechanismExplorationPlan.ProbeVisitBudget(new[] { "H", "-" }, 30);
        visits.Observe("-", "Contact"); visits.Tick(4f);
        Assert.IsTrue(visits.Finished);
        Assert.AreEqual(1, visits.TimedOutTargets);
        Assert.AreEqual(1, visits.SatisfiedCount);
    }

    [Test]
    public void ConfirmationDistinguishesHammerDamageAndBudgetEvidence()
    {
        var a = new MechanismExplorationPlan.Trial { outcome = "Cleared" };
        var b = new MechanismExplorationPlan.Trial { outcome = "Cleared" };
        a.coverage.Add(new MechanismExplorationPlan.Evidence { mechanism = "P", movingPartEvidenceVersion = 1 });
        b.coverage.Add(new MechanismExplorationPlan.Evidence { mechanism = "P", movingPartEvidenceVersion = 1, runnerMovingPartContacts = 1 });
        StringAssert.Contains("不稳定", MechanismExplorationPlan.CompareConfirmation(a, b));
        b.coverage[0].runnerMovingPartContacts = 0;
        b.healthEvidenceVersion = 1; b.runnerDamageEvents = 1;
        StringAssert.Contains("不稳定", MechanismExplorationPlan.CompareConfirmation(a, b));
        a.healthEvidenceVersion = 1; a.runnerDamageEvents = 1;
        b.probeEvidenceVersion = 1; b.probeBudgetExhausted = true;
        StringAssert.Contains("不稳定", MechanismExplorationPlan.CompareConfirmation(a, b));
    }

    [Test]
    public void TimeoutConfirmationsCannotBeCrowdedOutByEarlyCoverageGaps()
    {
        var trials = Enumerable.Range(0, 8).Select(i => new MechanismExplorationPlan.Trial {
            scenarioId = "coverage" + i, seconds = 4, outcome = "Cleared"
        }).ToList();
        for (int i = 0; i < 4; i++) trials.Add(new MechanismExplorationPlan.Trial {
            scenarioId = "death" + i, seconds = 5, outcome = "RunnerStopped" });
        trials.Add(new MechanismExplorationPlan.Trial { scenarioId = "S-", seconds = 30, outcome = "TimedOut" });
        trials.Add(new MechanismExplorationPlan.Trial { scenarioId = "-X", seconds = 30, outcome = "TimedOut" });
        trials.Add(new MechanismExplorationPlan.Trial { scenarioId = "boot", outcome = "RuntimeError" });
        CollectionAssert.AreEqual(new[] { "S-", "-X", "death0", "death1", "death2", "death3" },
            MechanismExplorationPlan.SelectConfirmationScenes(trials));
        foreach (var t in trials) t.attempt = 2;
        Assert.IsEmpty(MechanismExplorationPlan.SelectConfirmationScenes(trials));
    }

    [Test]
    public void ProbeVisitsDeduplicateTypesAndNeverInventEvidenceAtDeadline()
    {
        var visits = new MechanismExplorationPlan.ProbeVisitBudget(new[] { "-", "-", "-", "S", "-" }, 30);
        Assert.AreEqual(2, visits.TargetCount);
        visits.Observe("-", "Contact"); visits.Observe("-", "Contact"); visits.Tick(0.1f);
        Assert.AreEqual("S", visits.Current); Assert.AreEqual(1, visits.SatisfiedCount);
        visits.Tick(4f);
        Assert.IsTrue(visits.Finished); Assert.AreEqual(1, visits.TimedOutTargets);
        Assert.AreEqual(1, visits.SatisfiedCount, "Expiration is not observation");
    }

    [TestCase("B")]
    [TestCase("o")]
    public void ProbeEffectsCannotBeReplacedByContactOrControl(string mechanism)
    {
        var visits = new MechanismExplorationPlan.ProbeVisitBudget(new[] { mechanism }, 30);
        visits.Observe(mechanism, "Contact"); visits.Observe(mechanism, "Active"); visits.Tick(0.1f);
        Assert.IsFalse(visits.Finished); Assert.AreEqual(0, visits.SatisfiedCount);
        visits.Observe(mechanism, "Effect"); visits.Tick(0.1f);
        Assert.IsTrue(visits.Finished); Assert.AreEqual(1, visits.SatisfiedCount);
    }

    [TestCase("F")]
    [TestCase("[")]
    public void ProbeControlCycleRequiresActiveThenRecovery(string mechanism)
    {
        var visits = new MechanismExplorationPlan.ProbeVisitBudget(new[] { mechanism }, 30);
        visits.Observe(mechanism, "Recovery"); visits.Observe(mechanism, "Contact"); visits.Tick(0.1f);
        Assert.AreEqual(0, visits.SatisfiedCount);
        visits.Observe(mechanism, "Active"); visits.Tick(0.1f); Assert.IsFalse(visits.Finished);
        visits.Observe(mechanism, "Recovery"); visits.Tick(0.1f); Assert.IsTrue(visits.Finished);
    }

    [Test]
    public void PassageContactKeepsOrdinaryInputOpportunityAndMissingIsNotSuccess()
    {
        var visits = new MechanismExplorationPlan.ProbeVisitBudget(new[] { "H", "P" }, 30);
        visits.Observe("H", "Contact"); visits.Tick(0.1f);
        Assert.AreEqual("H", visits.Current);
        visits.SkipMissing(); Assert.AreEqual(1, visits.MissingTargets);
        visits.Observe("P", "Contact"); visits.Tick(0.1f); Assert.AreEqual(0, visits.SatisfiedCount);
        visits.Observe("P", "MovingPartContact"); visits.Tick(0.1f);
        Assert.IsTrue(visits.Finished); Assert.AreEqual(1, visits.SatisfiedCount);
    }

    [TestCase(10f, 5f)]
    [TestCase(30f, 8f)]
    [TestCase(120f, 8f)]
    public void ProbeTotalBudgetReservesGoalTimeAndStopsEvenWithManyTargets(float limit, float expected)
    {
        var visits = new MechanismExplorationPlan.ProbeVisitBudget(MechanismExplorationPlan.Catalog.Select(c => c.ToString()), limit);
        for (int i = 0; i < 10000; i++) visits.Tick(0.1f);
        Assert.IsTrue(visits.Finished); Assert.IsTrue(visits.BudgetExhausted);
        Assert.AreEqual(expected, visits.Budget); Assert.LessOrEqual(visits.Elapsed, expected);
        Assert.AreEqual(0, visits.SatisfiedCount);
    }

    [Test]
    public void NewPendulumEvidenceRequiresHammerNotPivotContact()
    {
        var evidence = new MechanismExplorationPlan.Evidence { mechanism = "P", built = 1,
            observationVersion = 1, movingPartEvidenceVersion = 1, runnerContacts = 5 };
        Assert.IsTrue(evidence.ObservationGap);
        evidence.runnerMovingPartContacts = 1; Assert.IsFalse(evidence.ObservationGap);
        Assert.AreEqual(0, evidence.runnerEffects, "Contact does not claim damage or control effect");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PendulumMountIsNotRequiredLandingButOrdinaryHighPlatformStillFails(bool snippet)
    {
        var room = MechanismExplorationPlan.Build(153, "P");
        var result = AsciiLevelValidator.ValidateTemplate(room.ascii, snippet);
        Assert.IsEmpty(result.errors, result.GetReport());
        Assert.IsTrue(result.info.Any(s => s.Contains("Hazard mount 'P'")));
        Assert.IsTrue(AsciiElementRegistry.GetDefault().GetEntry('P').isSolid, "Keep actual pivot collision metadata");
        Assert.IsTrue(AsciiElementRegistry.GetDefault().GetEntry('P').isHazard, "Keep independent danger checks");
        var ordinary = AsciiLevelValidator.ValidateTemplate(room.ascii.Replace('P', '='), snippet);
        Assert.IsTrue(ordinary.errors.Any(s => s.Contains("UNREACHABLE platform")),
            "An ordinary isolated high landing must still fail at the exact same coordinate");
    }

    [Test]
    public void PendulumNearSpawnStillProducesDangerWarning()
    {
        var result = AsciiLevelValidator.ValidateTemplate("..........\n.MP....TG.\n##########");
        Assert.IsTrue(result.warnings.Any(s => s.Contains("Hazard") && s.Contains("spawn")), result.GetReport());
    }

    [TestCase('.')]
    [TestCase('C')]
    [TestCase('X')]
    [TestCase('-')]
    [TestCase('>')]
    public void ConveyorLowerFloorIsNotAnAbyssButTemporarySupportsDoNotCertifyIt(char replacement)
    {
        var room = MechanismExplorationPlan.Build(0x2a923, "B<");
        Assert.IsEmpty(AsciiLevelValidator.ValidateTemplate(room.ascii).errors);
        var unsupported = AsciiLevelValidator.ValidateTemplate(room.ascii.Replace('<', replacement));
        Assert.IsTrue(unsupported.errors.Any(e => e.Contains("IMPASSABLE gap")),
            "Only permanent lower support suppresses abyss diagnostics; dynamic supports need separate validation");
    }

    [Test]
    public void UploadedMechanismPlanHasNoL1ErrorsBeforeAnyUnityGeneration()
    {
        foreach (var room in MechanismExplorationPlan.Create(153, MechanismExplorationPlan.Scope.Mechanisms))
        {
            Assert.IsTrue(LevelStudioDocument.TryParse(room.ascii, out var doc, out var error), error);
            var result = AsciiLevelValidator.ValidateTemplate(doc.Grid);
            Assert.IsEmpty(result.errors, room.id + " " + result.GetReport());
        }
    }

    [Test]
    public void AllSingleMechanismProbesPassL1Across100Seeds()
    {
        for (int seed = -50; seed < 50; seed++)
            foreach (char mechanism in MechanismExplorationPlan.Catalog)
            {
                var room = MechanismExplorationPlan.Build(seed, mechanism.ToString());
                var result = AsciiLevelValidator.ValidateTemplate(room.ascii);
                Assert.IsEmpty(result.errors, $"seed={seed}, mechanism={mechanism}\n{result.GetReport()}");
            }
        // Structural checks only: this does not simulate moving hammers or AI encounters.
    }

    [Test]
    public void ScanRequestAndLegacyRevealCountCannotInventSuccessfulCounterplay()
    {
        var t = new MechanismExplorationPlan.Trial { experience = "RiskOrDetour", experienceEvidenceVersion = 1,
            marioStrategy = "Scout", scans = 1, reveals = 10 };
        StringAssert.Contains("缺少扫描结果", t.ExperienceGaps.Single());
        Assert.AreEqual(0, t.scanHits);
        t.scanEvidenceVersion = 1; t.scanMisses = 1;
        Assert.AreEqual(1, t.ExperienceGaps.Length, "A miss is not a hit");
        t.scanHits = 1;
        Assert.IsEmpty(t.ExperienceGaps, "Observed reveal is interaction even before the opponent is armed");
        t.marioStrategy = "SafeRoute";
        Assert.AreEqual(1, t.ExperienceGaps.Length, "Scan cannot replace a missing safe route");
    }

    [Test]
    public void EveryMechanismHasExplicitBehaviorChecksAndHonestProbeLayers()
    {
        foreach (char c in MechanismExplorationPlan.Catalog)
        {
            string symbol = c.ToString();
            Assert.IsFalse(MechanismExplorationPlan.BehaviorRequirement(symbol).Contains("未知"), symbol);
            var e = new MechanismExplorationPlan.Evidence { mechanism = symbol, observationVersion = 1, built = 1,
                activations = 100, controlsAccepted = 100, tricksterContacts = 100, contacts = 100 };
            Assert.IsTrue(e.ObservationGap, symbol + ": accepted controls and opponent contacts cannot stand in for runner effects");
            e.runnerContacts = 1;
            if (symbol == "B" || symbol == "o")
            { Assert.IsTrue(e.ObservationGap); e.runnerEffects = 1; }
            if (symbol == "F" || symbol == "[")
            { Assert.IsTrue(e.ObservationGap); e.phases.Add("Active"); Assert.IsTrue(e.ObservationGap); e.phases.Add("Recovery"); }
            Assert.IsFalse(e.ObservationGap, symbol);
            StringAssert.Contains("行为验收仍待专项测试", e.Status);
            e.built = 0; Assert.IsTrue(e.ObservationGap);
        }
    }

    [Test]
    public void PassiveContactStopsUselessActivationRetryButDoesNotClaimBehaviorPassed()
    {
        var t = new MechanismExplorationPlan.Trial { outcome = "Cleared", seconds = 4 };
        t.coverage.Add(new MechanismExplorationPlan.Evidence { observationVersion = 1, mechanism = "<", built = 1, runnerContacts = 1 });
        Assert.IsFalse(t.NeedsConfirmation, "A conveyor has no control activation to chase");
        StringAssert.Contains("不证明行为", t.coverage[0].Status);
        t.coverage[0].observationVersion = 0;
        Assert.IsTrue(t.NeedsConfirmation, "Do not silently migrate old mixed evidence into new typed observations");
    }

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
        Assert.IsFalse(trial.CandidateForHumanPlay, "Zero-duration records are not played candidates");
        trial.seconds = 5;
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

    [TestCase(0)]
    [TestCase(153)]
    [TestCase(-1)]
    public void ExperienceRoomsHaveDistinctDecisionsAndIndependentMatchups(int seed)
    {
        var rooms = MechanismExplorationPlan.Create(seed, MechanismExplorationPlan.Scope.Experience);
        Assert.AreEqual(3, rooms.Count);
        Assert.AreEqual(3, rooms.Select(r => r.experience).Distinct().Count());
        Assert.AreEqual(27, MechanismExplorationPlan.TrialCount(rooms));
        foreach (var room in rooms)
        {
            Assert.IsTrue(LevelStudioDocument.TryParse(room.ascii, out var doc, out _));
            Assert.IsEmpty(doc.PlayReadiness());
            Assert.IsFalse(AsciiLevelValidator.ValidateTemplate(doc.Grid).errors.Any(e => e.Contains("IMPASSABLE gap")), room.id);
            Assert.AreEqual(9, MechanismExplorationPlan.Matchups(room).Select(m => m.Id).Distinct().Count());
            Assert.AreEqual(2, room.routes.Length);
            Assert.IsFalse(room.routes[0].Contains(20, 5.4f));
            Assert.IsTrue(room.routes[1].Contains(20, 5.4f));
            float standingY = 4 + PhysicsMetrics.ONEWAY_COLLIDER_SIZE.y * 0.5f +
                PhysicsMetrics.MARIO_COLLIDER_HEIGHT * 0.5f - PhysicsMetrics.MARIO_COLLIDER_OFFSET_Y;
            Assert.IsTrue(room.routes[1].Contains(20, standingY), "Actual grounded upper-route movement must be recorded");
            Assert.AreEqual(standingY, room.routes[1].points[3].y, 0.001f);
            Assert.AreEqual(room.ascii, MechanismExplorationPlan.BuildExperience(room.seed, rooms.IndexOf(room)).ascii);
        }
        LevelStudioDocument.TryParse(rooms[2].ascii, out var loot, out _);
        Assert.AreEqual('G', loot.Cell(3, 1)); Assert.AreEqual('o', loot.Cell(38, 1));
        var confirmations = rooms.Select(r => r.id).ToArray();
        for (int i = 0; i < 54; i++)
        {
            var slot = MechanismExplorationPlan.TrialAt(rooms, confirmations, i);
            Assert.AreEqual(i < 27 ? 1 : 2, slot.attempt);
            Assert.AreEqual(rooms[(i % 27) / 9], slot.scenario);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => MechanismExplorationPlan.TrialAt(rooms, confirmations, 54));
        Assert.AreEqual(3, MechanismExplorationPlan.Matchups(new MechanismExplorationPlan.Scenario { version = 1 }).Length);
    }

    [Test]
    public void CombinationExitKeepsLowerLaneOpenAndBounceHasHeadroom()
    {
        foreach (var room in MechanismExplorationPlan.Create(153, MechanismExplorationPlan.Scope.Pairwise).Where(r => r.mechanisms.Length == 2))
        {
            LevelStudioDocument.TryParse(room.ascii, out var doc, out _);
            Assert.AreEqual('.', doc.Cell(27, 1), room.id);
            if (room.mechanisms.Contains('B'))
                Assert.IsFalse(AsciiLevelValidator.ValidateTemplate(doc.Grid).warnings.Any(w => w.Contains("Bounce trajectory")), room.id);
        }
    }

    [Test]
    public void RouteMemoryNeverCountsFailedWaypointsAndStopsSwitching()
    {
        var room = MechanismExplorationPlan.BuildExperience(153, 0);
        var nav = new MechanismExplorationPlan.RouteNavigator(room.routes, true);
        Assert.AreEqual("upper", nav.RouteId);
        for (int i = 0; i < 200; i++) nav.Tick(2, 1.4f, false, 0.1f);
        Assert.AreEqual(2, nav.SwitchRequests);
        Assert.AreEqual(0, nav.WaypointsReached, "Retries must not invent physical progress");
        Assert.IsNotNull(nav.Target, "Failure cannot silently skip the remaining route");
    }

    [Test]
    public void LootPhaseReversesWaypointsOnlyAfterActualCollectionFlag()
    {
        var room = MechanismExplorationPlan.BuildExperience(153, 2);
        var nav = new MechanismExplorationPlan.RouteNavigator(room.routes, false);
        Assert.AreEqual(11, nav.Target.x);
        nav.Tick(11, 1.015f, false, 0.1f);
        Assert.AreEqual(1, nav.WaypointsReached);
        Assert.AreEqual(24, nav.Target.x);
        nav.Tick(38, 1.015f, true, 0.1f);
        Assert.AreEqual(35, nav.Target.x);
        nav.Tick(35, 1.015f, true, 0.1f);
        Assert.AreEqual(24, nav.Target.x);
        Assert.AreEqual(2, nav.WaypointsReached);
    }

    [Test]
    public void CounterplayEvidenceRequiresObservedMotionWithinTheCorrectPhase()
    {
        var motion = new MechanismExplorationPlan.CounterplayMotion();
        Assert.AreEqual(0, motion.Sample("Telegraph", -2, 0, 2));
        Assert.AreEqual(0, motion.Sample("Telegraph", -2, 0, 2));
        Assert.AreEqual(1, motion.Sample("Telegraph", -2.5f, 0, 2.5f));
        Assert.AreEqual(0, motion.Sample("Telegraph", -3, 0, 3), "One retreat per warning episode");
        Assert.AreEqual(0, motion.Sample("Active", -1, 0, 1));
        Assert.AreEqual(0, motion.Sample("Recovery", 1, 0, 1), "Do not infer a recovery crossing at a phase boundary");
        Assert.AreEqual(2, motion.Sample("Recovery", -1, 0, 1));
        Assert.AreEqual(0, motion.Sample("Recovery", 1, 0, 1), "One crossing per recovery episode");
        motion.Sample("Idle", -1, 0, 1);
        motion.Sample("Recovery", -1, 5, 5);
        Assert.AreEqual(0, motion.Sample("Recovery", 1, 5, 5), "Upper-route movement is not a lower-trap counter");
    }

    [Test]
    public void ConfirmationDetectsChangedRoutesEvenWhenOutcomeMatches()
    {
        var baseline = new MechanismExplorationPlan.Trial { outcome = "Cleared" };
        var confirmation = new MechanismExplorationPlan.Trial { outcome = "Cleared" };
        baseline.routesUsed.Add("Escape:lower"); confirmation.routesUsed.Add("Escape:upper");
        StringAssert.Contains("不稳定", MechanismExplorationPlan.CompareConfirmation(baseline, confirmation));
    }

    [Test]
    public void MixedOldAndExperienceSchedulesUseEachRoomsOwnMatchups()
    {
        var rooms = new[] { MechanismExplorationPlan.Build(153, "F"), MechanismExplorationPlan.BuildExperience(154, 2) };
        var confirmation = new[] { rooms[1].id };
        Assert.AreEqual(12, MechanismExplorationPlan.TrialCount(rooms));
        Assert.AreEqual("Cautious", MechanismExplorationPlan.TrialAt(rooms, confirmation, 0).matchup.Id);
        Assert.AreEqual("Runner vs Ambusher", MechanismExplorationPlan.TrialAt(rooms, confirmation, 3).matchup.Id);
        Assert.AreEqual(2, MechanismExplorationPlan.TrialAt(rooms, confirmation, 12).attempt);
        Assert.Throws<ArgumentOutOfRangeException>(() => MechanismExplorationPlan.TrialAt(rooms, confirmation, 21));
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
    [Test]
    public void UpperWaypointsRequireGroundedLandingNotPassingUnderOrFlyingPast()
    {
        var nav = new MechanismExplorationPlan.RouteNavigator(MechanismExplorationPlan.BuildExperience(153, 0).routes, true);
        var target = nav.Target;
        nav.Tick(target.x, 1.015f, false, 0.1f, true);
        nav.Tick(target.x, target.y, false, 0.1f, false);
        Assert.AreEqual(0, nav.WaypointsReached);
        nav.Tick(target.x, target.y + 0.015f, false, 0.1f, true);
        Assert.AreEqual(1, nav.WaypointsReached);
        Assert.IsEmpty(nav.CompletedRoutes);
    }

    [Test]
    public void SkippedLowerEntryAfterFallbackCannotClaimCompleteRoute()
    {
        var nav = new MechanismExplorationPlan.RouteNavigator(MechanismExplorationPlan.BuildExperience(153, 0).routes, true);
        for (int i = 0; i < 45; i++) nav.Tick(25, 1.015f, false, 0.1f, true);
        Assert.AreEqual("lower", nav.RouteId);
        Assert.AreEqual(0, nav.WaypointsReached);
        nav.Tick(35, 1.015f, false, 0.1f, true);
        Assert.IsEmpty(nav.CompletedRoutes, "Fallback skipped the entry: no whole-route claim");
    }

    [Test]
    public void ExperienceClearCannotHideFailedSafeRouteButSafeBypassNeedsNoContact()
    {
        var t = new MechanismExplorationPlan.Trial { experience = "RiskOrDetour", experienceEvidenceVersion = 1,
            marioStrategy = "SafeRoute", outcome = "Cleared", seconds = 10 };
        t.coverage.Add(new MechanismExplorationPlan.Evidence { built = 1 });
        t.routesUsed.Add("ReachGoal:upper");
        Assert.IsFalse(t.CandidateForHumanPlay, "Region entry alone is not a complete detour");
        t.completedRoutes.Add("Out:upper");
        Assert.IsTrue(t.CandidateForHumanPlay, "Avoiding the trap is a valid choice, not failed contact coverage");
        Assert.IsFalse(t.NeedsConfirmation);
        t.expectsReturn = true;
        Assert.IsFalse(t.CandidateForHumanPlay);
        t.completedRoutes.Add("Return:upper"); t.lootEvents = t.escapeEvents = 1;
        Assert.IsTrue(t.CandidateForHumanPlay);
    }

    [Test]
    public void ChaserRequestsAndControlsCannotInventReadyWindowOrSuccessfulTransfer()
    {
        var t = new MechanismExplorationPlan.Trial { experience = "LootAndReturn", experienceEvidenceVersion = 1,
            expectsReturn = true, marioStrategy = "Runner", tricksterStrategy = "Chaser", outcome = "Cleared",
            seconds = 10, controlAccepted = 20, anchorSwitchRequests = 100, lootEvents = 1, escapeEvents = 1 };
        Assert.AreEqual(2, t.ExperienceGaps.Length);
        t.armedNearbySeconds = 0.1f; Assert.AreEqual(1, t.ExperienceGaps.Length);
        t.possessionTransfers = 1; Assert.IsEmpty(t.ExperienceGaps);
    }

    [Test]
    public void SavedV2ExperienceIsExplicitlyMissingNewEvidenceWithoutRewritingAscii()
    {
        var old = new MechanismExplorationPlan.Scenario { version = 2, experience = "RiskOrDetour", ascii = "saved old geometry" };
        var t = new MechanismExplorationPlan.Trial { outcome = "Cleared", marioStrategy = "SafeRoute" };
        StringAssert.Contains("旧报告", MechanismExplorationPlan.ExperienceIssues(t, old)[0]);
        Assert.AreEqual("saved old geometry", old.ascii);
        Assert.AreEqual(2, old.version);
    }

    [Test]
    public void Stress1000SeedsChecksGeometrySchedulesAndCoordinateOracleNotPhysics()
    {
        int rooms = 0, slots = 0, landings = 0;
        for (int i = 0; i < 1000; i++)
        {
            int seed = i == 0 ? int.MinValue : i == 1 ? int.MaxValue : unchecked(i * 104729 - 50000000);
            var plan = MechanismExplorationPlan.Create(seed, MechanismExplorationPlan.Scope.Experience);
            var confirmations = plan.Select(r => r.id).ToArray();
            for (int slot = 0; slot < 54; slot++)
            {
                var scheduled = MechanismExplorationPlan.TrialAt(plan, confirmations, slot);
                Assert.AreEqual(slot < 27 ? 1 : 2, scheduled.attempt); slots++;
            }
            foreach (var room in plan)
            {
                rooms++;
                Assert.IsTrue(LevelStudioDocument.TryParse(room.ascii, out var doc, out _));
                Assert.IsEmpty(doc.PlayReadiness(), room.id);
                Assert.IsFalse(room.mechanisms.Contains('X'), "Breakable blocks are not attack anchors");
                var upper = room.routes[1].points;
                foreach (var point in upper.Take(upper.Length - 1))
                {
                    int cellY = (int)Math.Round(point.y - 0.625f);
                    Assert.AreEqual('-', doc.Cell((int)point.x, cellY), room.id + " unsupported waypoint");
                }
                for (int n = 1; n < upper.Length; n++) Assert.LessOrEqual(Math.Abs(upper[n].y - upper[n - 1].y), 1.01f);
                var nav = new MechanismExplorationPlan.RouteNavigator(room.routes, true);
                foreach (bool returning in new[] { false, true })
                {
                    nav.Tick(returning ? 38 : 2, 1, returning, 0.02f, true);
                    for (int n = 0; n < upper.Length; n++)
                    {
                        var target = nav.Target; Assert.IsNotNull(target);
                        nav.Tick(target.x, target.y, returning, 0.02f, false);
                        Assert.AreSame(target, nav.Target, "Airborne coordinate pass cannot consume a landing");
                        nav.Tick(target.x + 0.1f, target.y + 0.015f, returning, 0.02f, true);
                        landings++;
                    }
                    Assert.IsNull(nav.Target);
                }
                CollectionAssert.AreEquivalent(new[] { "Out:upper", "Return:upper" }, nav.CompletedRoutes);
                Assert.AreEqual(0, nav.SwitchRequests);
                var stalled = new MechanismExplorationPlan.RouteNavigator(room.routes, true);
                for (int step = 0; step < 400; step++) stalled.Tick(2, 1, false, 0.1f, true);
                Assert.AreEqual(2, stalled.SwitchRequests); Assert.AreEqual(0, stalled.WaypointsReached);
                Assert.IsEmpty(stalled.CompletedRoutes);
            }
        }
        TestContext.WriteLine($"Coordinate oracle only: rooms={rooms}, schedule slots={slots}, grounded visits={landings}; no Unity physics simulated.");
    }

}
