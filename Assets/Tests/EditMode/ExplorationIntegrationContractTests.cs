using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Editor-side integration contracts; actual scene/playmode cycling still needs Unity execution.</summary>
public class ExplorationIntegrationContractTests
{
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
}
