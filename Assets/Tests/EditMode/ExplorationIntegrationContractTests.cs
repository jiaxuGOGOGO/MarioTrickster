using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Editor-side integration contracts; actual scene/playmode cycling still needs Unity execution.</summary>
public class ExplorationIntegrationContractTests
{
    [TestCase("B")]
    [TestCase("C")]
    [TestCase("-")]
    [TestCase("F")]
    [TestCase("<")]
    [TestCase("X")]
    [TestCase("o")]
    [TestCase("H")]
    [TestCase(">")]
    [TestCase("^")]
    [TestCase("~")]
    [TestCase("P")]
    [TestCase("[")]
    [TestCase("]")]
    [TestCase("E")]
    [TestCase("e")]
    [TestCase("@")]
    [TestCase("f")]
    [TestCase("S")]
    public void EveryMechanismProbeBuildsRealRegisteredComponents(string symbol)
    {
        GameObject root = null;
        try
        {
            var scenario = MechanismExplorationPlan.Build(153, symbol);
            root = AsciiLevelGenerator.GenerateFromTemplate(scenario.ascii, false, false);
            Assert.IsNotNull(root);
            var entry = AsciiElementRegistry.GetDefault().GetEntry(symbol[0]);
            Assert.IsNotNull(entry);
            Assert.IsNotEmpty(entry.componentTypeNames);
            // Registry may include native components (FlyingEnemy requires Rigidbody2D).
            var components = root.GetComponentsInChildren<Component>(true);
            foreach (string expectedType in entry.componentTypeNames)
                Assert.IsTrue(System.Array.Exists(components, c => c != null && c.GetType().Name == expectedType),
                    symbol + " must build actual " + expectedType + "; ASCII alone is not evidence");
        }
        finally { if (root != null) Object.DestroyImmediate(root); }
    }

    [Test]
    public void BaiterPreparesDuringApproachButOnlyRequestsAtRealProximity()
    {
        bool armed = false; float remaining = 0f;
        Assert.IsFalse(HeuristicBotInputProvider.StepBaiterWindow(ref armed, ref remaining, 5, true, 8, 0.1f, 0.5f));
        Assert.IsTrue(armed);
        Assert.IsFalse(HeuristicBotInputProvider.StepBaiterWindow(ref armed, ref remaining, 3, true, 8, 0.6f, 0));
        Assert.AreEqual(0, remaining, "Preparation may finish without firing early");
        Assert.IsFalse(HeuristicBotInputProvider.StepBaiterWindow(ref armed, ref remaining, 2, true, 8, 0.1f, 0));
        Assert.IsTrue(HeuristicBotInputProvider.StepBaiterWindow(ref armed, ref remaining, 1.9f, true, 8, 0.1f, 0));
        Assert.IsFalse(armed, "New shot requires a fresh reaction");
    }

    [TestCase(7f, true, 8f)]
    [TestCase(4f, false, 8f)]
    [TestCase(4f, true, -8f)]
    [TestCase(4f, true, 0f)]
    public void BaiterCancelsPreparationWhenRunnerLeavesOrChangesLane(float distance, bool sameHeight, float speed)
    {
        bool armed = true; float remaining = 0f;
        Assert.IsFalse(HeuristicBotInputProvider.StepBaiterWindow(ref armed, ref remaining, distance, sameHeight, speed, 0.1f, 0.5f));
        Assert.IsFalse(armed); Assert.AreEqual(0, remaining);
        Assert.IsFalse(HeuristicBotInputProvider.StepBaiterWindow(ref armed, ref remaining, 1, true, 8, 0.1f, 0.5f));
        Assert.IsFalse(HeuristicBotInputProvider.StepBaiterWindow(ref armed, ref remaining, 1, true, 8, 0.2f, 0));
        Assert.IsTrue(HeuristicBotInputProvider.StepBaiterWindow(ref armed, ref remaining, 1, true, 8, 0.4f, 0));
    }

    private static float Decision(HeuristicBotInputProvider bot)
    {
        var method = typeof(HeuristicBotInputProvider).GetMethod("DecisionRange", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        return (float)method.Invoke(bot, new object[] { 0f, 1f });
    }

    [Test]
    public void SeededBotDecisionsRepeatWithoutConsumingGlobalUnityRandom()
    {
        var saved = Random.state;
        try
        {
            var a = new HeuristicBotInputProvider(); a.SetDecisionSeed(153);
            var b = new HeuristicBotInputProvider(); b.SetDecisionSeed(153);
            Random.InitState(77); float expected = Random.value;
            Random.InitState(77);
            for (int i = 0; i < 30; i++) Assert.AreEqual(Decision(a), Decision(b));
            Assert.AreEqual(expected, Random.value, "Bot seed must not perturb gameplay/VFX randomness");
        }
        finally { Random.state = saved; }
    }

    [Test]
    public void CacheInvalidationClearsExplorationOnlyGoal()
    {
        var bot = new HeuristicBotInputProvider { ExplorationTarget = Vector2.one };
        bot.InvalidateCache();
        Assert.IsFalse(bot.ExplorationTarget.HasValue);
    }

    private sealed class JumpPulseBot : HeuristicBotInputProvider
    {
        public bool RequestJump;
        protected override void UpdateMarioBrain(float dt) { }
        protected override void UpdateTricksterBrain(float dt) { p2JumpDown = RequestJump; }
    }

    [Test]
    public void BotJumpHoldReleasesAndDropModifierDoesNotLeakIntoLaterFrames()
    {
        var bot = new JumpPulseBot { RequestJump = true, p1SHeld = true };
        bot.Tick(0.1f);
        Assert.IsTrue(bot.p2JumpHeld);
        Assert.IsFalse(bot.p1SHeld);
        bot.RequestJump = false;
        for (int i = 0; i < 5; i++) bot.Tick(0.1f);
        Assert.IsFalse(bot.p2JumpHeld);
        bot.RequestJump = true; bot.Tick(0.1f);
        Assert.IsTrue(bot.p2JumpHeld, "A second jump needs a new held edge");
        bot.InvalidateCache(); bot.RequestJump = false; bot.Tick(0.1f);
        Assert.IsFalse(bot.p2JumpHeld);
    }

    [TestCase(1f)]
    [TestCase(-1f)]
    public void BodySweepFindsThinElevatedColliderButNotFloorTriggersOrOneWaySides(float direction)
    {
        var actor = new GameObject("SweepActor");
        var floor = new GameObject("SweepFloor");
        var obstacle = new GameObject("ThinObstacle");
        bool originalQueries = Physics2D.queriesStartInColliders;
        try
        {
            actor.layer = 2; floor.layer = obstacle.layer = 0;
            actor.transform.position = new Vector3(25000, 1, 0);
            floor.transform.position = new Vector3(25000, 0, 0);
            obstacle.transform.position = new Vector3(25000 + direction, 1, 0);
            var body = actor.AddComponent<BoxCollider2D>();
            body.size = new Vector2(0.8f, 0.95f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(20, 1);
            var thin = obstacle.AddComponent<BoxCollider2D>();
            thin.size = PhysicsMetrics.BOUNCY_COLLIDER_SIZE;
            Physics2D.SyncTransforms();
            var oldRay = Physics2D.Raycast(new Vector2(body.bounds.center.x, body.bounds.min.y + 0.15f), new Vector2(direction, 0), 1.5f, 1 << 0);
            Assert.IsNull(oldRay.collider, "The old single-height probe misses this body-level thin platform");
            Assert.AreEqual(thin, HeuristicBotInputProvider.FindForwardObstacle(body, direction, 1 << 0));
            thin.isTrigger = true;
            Assert.IsNull(HeuristicBotInputProvider.FindForwardObstacle(body, direction, 1 << 0));
            thin.isTrigger = false;
            var effector = obstacle.AddComponent<PlatformEffector2D>();
            thin.usedByEffector = true; effector.useOneWay = true;
            Assert.IsNull(HeuristicBotInputProvider.FindForwardObstacle(body, direction, 1 << 0));
            Assert.AreEqual(originalQueries, Physics2D.queriesStartInColliders, "Navigation cannot change global query policy");
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(floor); Object.DestroyImmediate(obstacle); }
    }

    [TestCase(0f, 0f, 0f)]
    [TestCase(0.1f, 8f, -1f)]
    [TestCase(-0.1f, -8f, 1f)]
    [TestCase(1f, 0f, 1f)]
    [TestCase(-1f, 0f, -1f)]
    public void BounceLandingInputBrakesNearCenterAndMirrorsOnReturn(float dx, float speed, float expected)
    {
        Assert.AreEqual(expected, HeuristicBotInputProvider.BounceLandingSteering(dx, speed), 0.001f);
    }

    [Test]
    public void ContactProbeDoesNotAddPhysicsOrCountUnrelatedObjects()
    {
        var source = new GameObject("PassiveProbeTest");
        var other = new GameObject("NonPlayerTest");
        int contacts = 0;
        System.Action<string, GameObject, GameObject> handler = (id, target, actor) => contacts++;
        try
        {
            var probe = source.AddComponent<ExplorationContactProbe>();
            probe.mechanism = "B";
            ExplorationContactProbe.Contact += handler;
            // SendMessage on non-ExecuteAlways behaviours in EditMode asserts in Unity.
            var callback = typeof(ExplorationContactProbe).GetMethod("OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(callback);
            callback.Invoke(probe, new object[] { other.AddComponent<BoxCollider2D>() });
            Assert.AreEqual(0, contacts);
            Assert.IsNull(source.GetComponent<Collider2D>());
            Assert.IsNull(source.GetComponent<Rigidbody2D>());
        }
        finally
        {
            ExplorationContactProbe.Contact -= handler;
            Object.DestroyImmediate(source); Object.DestroyImmediate(other);
        }
    }

    [Test]
    public void TypedReportKeepsFirstPassAndConfirmationEffectsSeparate()
    {
        var report = new StudioExplorationRunner.Report {
            scenarios = MechanismExplorationPlan.Create(153, MechanismExplorationPlan.Scope.Smoke)
        };
        foreach (int attempt in new[] { 1, 2 })
        {
            var trial = new MechanismExplorationPlan.Trial { attempt = attempt, scanEvidenceVersion = 1,
                scans = 1, scanHits = attempt == 1 ? 0 : 1, scanMisses = attempt == 1 ? 1 : 0 };
            trial.coverage.Add(new MechanismExplorationPlan.Evidence { mechanism = "B", observationVersion = 1,
                built = 1, activations = 1, runnerEffects = attempt == 1 ? 0 : 1 });
            report.trials.Add(trial);
        }
        string summary = StudioExplorationRunner.BuildSummary(report);
        StringAssert.Contains("B first-pass: built=1", summary);
        StringAssert.Contains("B confirmation: built=1", summary);
        StringAssert.Contains("runnerEffects=0, observationGaps=1", summary);
        StringAssert.Contains("runnerEffects=1, observationGaps=0", summary);
        StringAssert.Contains("scan hits=0, misses=1", summary);
        StringAssert.Contains("确认局是相关复测", summary);
        StringAssert.Contains("本批未规划的19机制目录项:", summary);
        StringAssert.Contains("不是已修复", MechanismExplorationPlan.CompareConfirmation(report.trials[0], report.trials[1]));
    }

    [Test]
    public void StartupFailuresRemainUnplayedAndConfirmationBudgetIsVisible()
    {
        var report = new StudioExplorationRunner.Report {
            scenarios = MechanismExplorationPlan.Create(153, MechanismExplorationPlan.Scope.Smoke),
            status = "Blocked", blockedReason = "Repeated font error"
        };
        report.trials.Add(new MechanismExplorationPlan.Trial {
            scenarioId = report.scenarios[0].id, outcome = "StartupFailed"
        });
        StringAssert.Contains("不算通过", StudioExplorationRunner.EvidenceVerdict(report));
        string summary = StudioExplorationRunner.BuildSummary(report);
        StringAssert.Contains("有效试玩记录: 0", summary);
        StringAssert.Contains("没有有效试玩证据的场景: " + report.scenarios[0].id, summary);
        Assert.AreEqual(18, StudioExplorationRunner.PlannedTrials(report));
        report.confirmationScenarioIds.Add(report.scenarios[0].id);
        Assert.AreEqual(21, StudioExplorationRunner.PlannedTrials(report));
    }

    [Test]
    public void ExperienceReportCountsNineMatchupsAndKeepsRequestsSeparateFromEvidence()
    {
        var report = new StudioExplorationRunner.Report {
            scope = "Experience", scenarios = MechanismExplorationPlan.Create(153, MechanismExplorationPlan.Scope.Experience)
        };
        Assert.AreEqual(27, StudioExplorationRunner.PlannedTrials(report));
        foreach (var room in report.scenarios) report.confirmationScenarioIds.Add(room.id);
        Assert.AreEqual(54, StudioExplorationRunner.PlannedTrials(report));
        report.trials.Add(new MechanismExplorationPlan.Trial { scenarioId = report.scenarios[0].id,
            marioStrategy = "SafeRoute", tricksterStrategy = "Baiter", seconds = 10, outcome = "NoProgress", routeSwitchRequests = 2 });
        var summary = StudioExplorationRunner.BuildSummary(report);
        StringAssert.Contains("1 / 54", summary);
        StringAssert.Contains("route switch requests=2, physical transitions=0", summary);
        StringAssert.Contains("不是自主学习", summary);
        Assert.IsFalse(report.trials[0].CandidateForHumanPlay);
    }

    [Test]
    public void ReportsIdentifyActualTrackAndSeparateBounceRequestsFromLaunchEvents()
    {
        var report = new StudioExplorationRunner.Report {
            scope = "Replay", scenarios = MechanismExplorationPlan.Create(153, MechanismExplorationPlan.Scope.Smoke)
        };
        Assert.AreEqual("机制回归", StudioExplorationRunner.TestTrack(report));
        report.trials.Add(new MechanismExplorationPlan.Trial { bounceLandingAttempts = 3 });
        string summary = StudioExplorationRunner.BuildSummary(report);
        StringAssert.Contains("本批没有运行三类体验房", summary);
        StringAssert.Contains("bounce landing requests=3, runner bounce launches=0", summary);
        Assert.IsNull(report.toolRevision, "Reading old data must not label it as the current tool revision");
        report.scenarios = MechanismExplorationPlan.Create(153, MechanismExplorationPlan.Scope.Experience);
        Assert.AreEqual("体验探索", StudioExplorationRunner.TestTrack(report));
        Assert.IsFalse(StudioExplorationRunner.BuildSummary(report).Contains("本批没有运行三类体验房"));
        report.scenarios.Add(MechanismExplorationPlan.Build(153, "B"));
        Assert.AreEqual("混合批次", StudioExplorationRunner.TestTrack(report));
        var baseline = new MechanismExplorationPlan.Trial { outcome = "Cleared", bounceLandingAttempts = 3 };
        var replay = new MechanismExplorationPlan.Trial { outcome = "Cleared", bounceLandingAttempts = 3, runnerBounceLaunches = 1 };
        StringAssert.Contains("不稳定", MechanismExplorationPlan.CompareConfirmation(baseline, replay));
    }

    [Test]
    public void ReportsExposeUnfinishedPlansAndNeverEquateCompletionWithPassing()
    {
        var report = new StudioExplorationRunner.Report {
            seed = 153, scope = "Smoke", status = "Aborted",
            scenarios = MechanismExplorationPlan.Create(153, MechanismExplorationPlan.Scope.Smoke)
        };
        string summary = StudioExplorationRunner.BuildSummary(report);
        StringAssert.Contains("0 / 18", summary);
        StringAssert.Contains(report.scenarios[0].id, summary);
        StringAssert.Contains("不表示每局通过", summary);
        StringAssert.Contains("activations=0", summary);
    }
    [TestCase(typeof(MarioController), true)]
    [TestCase(typeof(MarioController), false)]
    [TestCase(typeof(TricksterController), true)]
    [TestCase(typeof(TricksterController), false)]
    public void BothControllerCeilingProbesRespectOneWayButKeepSolidCeilings(System.Type type, bool oneWay)
    {
        var actor = new GameObject("ProbeActor"); var roof = new GameObject("ProbeRoof");
        bool original = Physics2D.queriesStartInColliders;
        try
        {
            actor.transform.position = new Vector3(25000, 1, 0);
            var body = actor.AddComponent<BoxCollider2D>(); body.size = new Vector2(0.8f, 0.95f);
            actor.AddComponent<Rigidbody2D>(); var controller = actor.AddComponent(type);
            roof.transform.position = new Vector3(25000, 1.62f, 0);
            var deck = roof.AddComponent<BoxCollider2D>(); deck.size = new Vector2(4, 0.25f);
            if (oneWay) { deck.usedByEffector = true; roof.AddComponent<PlatformEffector2D>().useOneWay = true; }
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            type.GetField("boxCollider", flags).SetValue(controller, body);
            type.GetField("groundLayer", flags).SetValue(controller, (LayerMask)(1 << 0));
            type.GetField("_frameVelocity", flags).SetValue(controller, new Vector2(0, 6));
            Physics2D.SyncTransforms();
            type.GetMethod("CheckCollisions", flags).Invoke(controller, null);
            var velocity = (Vector2)type.GetField("_frameVelocity", flags).GetValue(controller);
            Assert.AreEqual(oneWay ? 6f : 0f, velocity.y);
            Assert.AreEqual(original, Physics2D.queriesStartInColliders);
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(roof); }
    }

    [Test]
    public void SurfaceProbeHonorsDropThroughTriggersAndSolidBehindPassableHit()
    {
        var actor = new GameObject("SurfaceActor"); var deckObject = new GameObject("SurfaceDeck");
        var solidObject = new GameObject("SolidBehindDeck");
        try
        {
            actor.transform.position = new Vector3(26000, 1.65f, 0);
            var body = actor.AddComponent<BoxCollider2D>(); body.size = new Vector2(0.8f, 1);
            actor.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            deckObject.transform.position = new Vector3(26000, 1, 0);
            var deck = deckObject.AddComponent<BoxCollider2D>(); deck.size = new Vector2(4, 0.25f);
            deck.usedByEffector = true; deckObject.AddComponent<PlatformEffector2D>().useOneWay = true;
            Physics2D.SyncTransforms();
            Assert.IsTrue(OneWayPlatform.HasBlockingSurface(body, Vector2.down, 0.1f, 1, -1));
            Assert.IsFalse(OneWayPlatform.HasBlockingSurface(body, Vector2.down, 0.1f, 1, 1));
            Physics2D.IgnoreCollision(body, deck, true);
            Assert.IsFalse(OneWayPlatform.HasBlockingSurface(body, Vector2.down, 0.1f, 1, -1));
            Physics2D.IgnoreCollision(body, deck, false);
            actor.transform.position = new Vector3(26000, 0.35f, 0);
            solidObject.transform.position = new Vector3(26000, 1.1f, 0);
            solidObject.AddComponent<BoxCollider2D>().size = new Vector2(4, 0.25f);
            Physics2D.SyncTransforms();
            Assert.IsTrue(OneWayPlatform.HasBlockingSurface(body, Vector2.up, 0.2f, 1, 6));
            solidObject.GetComponent<BoxCollider2D>().isTrigger = true;
            Assert.IsFalse(OneWayPlatform.HasBlockingSurface(body, Vector2.up, 0.2f, 1, 6));
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(deckObject); Object.DestroyImmediate(solidObject); }
    }

    [Test]
    public void OldAllClearExperienceReportStillShowsMissingRouteEvidence()
    {
        var report = new StudioExplorationRunner.Report { scenarios = MechanismExplorationPlan.Create(153, MechanismExplorationPlan.Scope.Experience) };
        report.trials.Add(new MechanismExplorationPlan.Trial { scenarioId = report.scenarios[0].id,
            outcome = "Cleared", seconds = 10, marioStrategy = "SafeRoute", waypointsReached = 5 });
        StringAssert.Contains("体验证据有缺口", StudioExplorationRunner.EvidenceVerdict(report));
        StringAssert.Contains("旧报告未记录", StudioExplorationRunner.BuildSummary(report));
        Assert.IsNull(report.trials[0].experience, "Reading must not rewrite historical data");
    }

    [TestCase(256, 1)]
    [TestCase(0, 1)]
    [TestCase(0, 0)]
    public void FailedOrEmptyRegressionStageRestoresWithoutInventingTrials(int passed, int failed)
    {
        var report = new StudioExplorationRunner.Report {
            scenarios = MechanismExplorationPlan.Create(153, MechanismExplorationPlan.Scope.Smoke)
        };
        Assert.AreEqual("Restoring", StudioExplorationRunner.CompleteRegressionStage(report, passed, failed, false));
        Assert.AreEqual(passed, report.regressionPassed); Assert.AreEqual(failed, report.regressionFailed);
        Assert.IsNotEmpty(report.blockedReason);
        Assert.IsEmpty(report.trials, "No AI gameplay happened after a blocked regression gate");
        Assert.IsEmpty(report.confirmationScenarioIds);
        StringAssert.Contains("不算通过", StudioExplorationRunner.EvidenceVerdict(report));
        StringAssert.Contains("0 / 18", StudioExplorationRunner.BuildSummary(report));
    }

    [Test]
    public void PassedRegressionContinuesButUserCancellationStillRestores()
    {
        var report = new StudioExplorationRunner.Report();
        Assert.AreEqual("Preparing", StudioExplorationRunner.CompleteRegressionStage(report, 257, 0, false));
        Assert.AreEqual("Passed", report.regressions); Assert.IsEmpty(report.blockedReason);
        Assert.AreEqual("Restoring", StudioExplorationRunner.CompleteRegressionStage(report, 257, 0, true));
        Assert.IsEmpty(report.blockedReason, "Cancellation is not a fabricated regression failure");
    }

}
