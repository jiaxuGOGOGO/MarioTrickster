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
