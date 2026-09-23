using System.Reflection;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Editor-side integration contracts; actual scene/playmode cycling still needs Unity execution.</summary>
public class ExplorationIntegrationContractTests
{
    private static StudioExplorationRunner.Report DuelReportFixture(MechanismExplorationPlan.Scenario room = null)
    {
        room = room ?? MechanismExplorationPlan.BuildDuel(168);
        var report = new StudioExplorationRunner.Report { status = "Complete", controlMode = "Automated", sourceFingerprint = "same-code",
            unityVersion = "2022.3.31f1", fixedDeltaTime = 0.02f, trialLimitSeconds = 60f, physicsConfigJson = "defaults", gameplayConfigJson = "same-config" };
        report.scenarios.Add(room);
        foreach (var m in MechanismExplorationPlan.Matchups(room))
        {
            var trial = new MechanismExplorationPlan.Trial { scenarioId = room.id, marioStrategy = m.mario, tricksterStrategy = m.trickster,
                profile = m.Id, attempt = 1, outcome = "Cleared", seconds = 20, controlMode = "Automated", tunnelEvidenceVersion = 1,
                healthEvidenceVersion = 1, startTimingEvidenceVersion = 1, startDelaySeconds = room.startDelaySeconds,
                actualStartWaitSeconds = room.startDelaySeconds, startWaitFrames = 60, opponentWaitDecisionFrames = m.trickster == "Passive" ? 0 : 60,
                lootEvents = 1, escapeEvents = 1, lootAtSeconds = 10, escapeAtSeconds = 20 };
            if (m.mario == "SafeRoute") { trial.completedRoutes.Add("Out:upper"); trial.completedRoutes.Add("Return:upper"); }
            report.trials.Add(trial);
        }
        return report;
    }

    private static StudioExplorationRunner.Report DesignReviewFixture()
    {
        var r = WallComparisonFixture(); r.regressions = "Passed";
        foreach (var t in r.trials)
        {
            t.scanEvidenceVersion = 1;
            if (t.tricksterStrategy == "Passive") continue;
            t.controlAccepted = 2; t.postLootControls = 1;
            if (t.marioStrategy != "Adaptive") continue;
            bool treatment = t.wallPolicy == "VisibleWallWindowV1";
            t.wallEpisodes.Add(new MechanismExplorationPlan.WallEpisode {
                source = "visible-F", leg = "Out", beganAt = 2, endedAt = 5, crossedAt = 4,
                sawWarning = true, sawSolid = true, sawReopen = true, outcome = "CrossedAfterReopen",
                inputAt = treatment ? 2.5f : -1f, inputFrames = treatment ? 20 : 0 });
        }
        return r;
    }

    private static void AddDesignConfirmations(StudioExplorationRunner.Report r)
    {
        r.confirmationScenarioIds.AddRange(r.scenarios.Select(s => s.id));
        foreach (var t in DesignReviewFixture().trials) { t.attempt = 2; r.trials.Add(t); }
    }

    [TestCase("Navigation")]
    [TestCase("ContestedRoute")]
    [TestCase("Counterplay")]
    [TestCase("Observation")]
    [TestCase("Response")]
    [TestCase("Execution")]
    [TestCase("ReturnMemory")]
    [TestCase("ReturnPressure")]
    [TestCase("HumanReview")]
    [TestCase("HumanFollowup")]
    public void ResearchReviewChoosesOneFalsifiableExperimentWithoutClaimingFun(string stage)
    {
        var r = DesignReviewFixture(); var t = r.trials[8];
        if (stage == "Navigation") r.trials[3].completedRoutes.Clear();
        if (stage == "ContestedRoute") { t.outcome = "TimedOut"; t.completedRoutes.Clear(); }
        if (stage == "Counterplay") { t.outcome = "RunnerStopped"; t.runnerDamageEvents = t.runnerHealthLost = 1; t.completedRoutes.Clear(); }
        if (stage == "Observation") t.wallEpisodes.Clear();
        if (stage == "Response") { t.wallEpisodes[0].inputFrames = 0; t.wallEpisodes[0].inputAt = -1; }
        if (stage == "Execution") { t.wallEpisodes[0].outcome = "LostCue"; t.wallEpisodes[0].crossedAt = -1; }
        if (stage == "ReturnMemory") { t.postLootControls = 0; t.scanHits = t.scans = 1; }
        if (stage == "ReturnPressure") t.postLootControls = 0;
        if (stage == "HumanFollowup") r.playerNotes.Add("好像只是在等墙打开，需要验证");
        string outcome = t.outcome; int count = r.trials.Count, episodes = t.wallEpisodes.Count;
        var card = StudioExplorationRunner.ReviewCavernDesign(r);
        Assert.AreEqual(stage, card.stage); Assert.IsNotEmpty(card.oneChange); Assert.IsNotEmpty(card.falsifier);
        StringAssert.Contains("只读", card.Brief); StringAssert.Contains("不是因果、乐趣评分", card.Detail);
        StringAssert.Contains("冻结", card.frozen); StringAssert.Contains("判断错了", card.humanQuestion);
        Assert.AreEqual(outcome, t.outcome); Assert.AreEqual(count, r.trials.Count); Assert.AreEqual(episodes, t.wallEpisodes.Count);
    }

    [TestCase("missingFirst")]
    [TestCase("duplicateFirst")]
    [TestCase("missingConfirmation")]
    [TestCase("regressionSkipped")]
    [TestCase("regressionFailed")]
    [TestCase("human")]
    [TestCase("sourceUnknown")]
    [TestCase("changedMap")]
    [TestCase("samePolicy")]
    [TestCase("mixedNavigation")]
    [TestCase("wrongPolicy")]
    [TestCase("oldCue")]
    [TestCase("falseCross")]
    [TestCase("passiveContaminated")]
    [TestCase("unequalStart")]
    [TestCase("negativeCounter")]
    [TestCase("nanPosition")]
    [TestCase("nullTrial")]
    [TestCase("unknownConfirmation")]
    [TestCase("invalidBudget")]
    public void ResearchReviewRefusesMissingMixedOrContradictoryEvidence(string fault)
    {
        var r = DesignReviewFixture(); var t = r.trials[8];
        if (fault == "missingFirst") r.trials.RemoveAt(0);
        if (fault == "duplicateFirst") r.trials[1] = r.trials[0];
        if (fault == "missingConfirmation") r.confirmationScenarioIds.Add(r.scenarios[0].id);
        if (fault == "regressionSkipped") r.regressions = "Not requested";
        if (fault == "regressionFailed") r.regressionFailed = 1;
        if (fault == "human") r.controlMode = "HumanMario";
        if (fault == "sourceUnknown") r.sourceFingerprint = "";
        if (fault == "changedMap") r.scenarios[1].ascii += " ";
        if (fault == "samePolicy") r.scenarios[1].wallTacticsVersion = 0;
        if (fault == "mixedNavigation") t.navigationPolicy = "different-policy";
        if (fault == "wrongPolicy") t.wallPolicy = "ObserveOnly";
        if (fault == "oldCue") t.wallEvidenceVersion = 0;
        if (fault == "falseCross") t.wallEpisodes[0].sawSolid = false;
        if (fault == "passiveContaminated") r.trials[0].controlAccepted = 1;
        if (fault == "unequalStart") t.opponentWaitDecisionFrames = 0;
        if (fault == "negativeCounter") t.postLootControls = -1;
        if (fault == "nanPosition") t.endX = float.NaN;
        if (fault == "nullTrial") r.trials[0] = null;
        if (fault == "unknownConfirmation") r.confirmationScenarioIds.Add("foreign-room");
        if (fault == "invalidBudget") r.trialLimitSeconds = float.PositiveInfinity;
        Assert.AreEqual("Evidence", StudioExplorationRunner.ReviewCavernDesign(r).stage);
    }

    [Test]
    public void ResearchReviewChecksBothPoliciesAndDoesNotHidePassiveConfirmationFailure()
    {
        var r = DesignReviewFixture(); AddDesignConfirmations(r);
        var failed = r.trials.Single(t => t.scenarioId == r.scenarios[1].id && t.marioStrategy == "SafeRoute" && t.tricksterStrategy == "Passive" && t.attempt == 2);
        failed.completedRoutes.Clear(); failed.completedRoutes.Add("Return:lower"); failed.outcome = "Cleared";
        r.trials[8].wallEpisodes.Clear(); // A later-stage missing cue must not hide the baseline navigation issue.
        var card = StudioExplorationRunner.ReviewCavernDesign(r);
        Assert.AreEqual("Navigation", card.stage);
        StringAssert.Contains("相关首轮0局、确认1局", card.evidence);
        StringAssert.Contains(r.scenarios[1].id, card.evidence); StringAssert.Contains("Return:lower", card.evidence);
        Assert.AreEqual("Cleared", failed.outcome, "Do not rewrite the actual win into a loss");
    }

    [Test]
    public void ResearchReviewDoesNotAttributeScanBoolToTheWallOrRequireMoreFights()
    {
        var r = DesignReviewFixture(); var t = r.trials[8]; t.postLootControls = 0; t.scanHits = t.scans = 1;
        var card = StudioExplorationRunner.ReviewCavernDesign(r);
        Assert.AreEqual("ReturnMemory", card.stage); StringAssert.Contains("扫描bool不证明命中该墙", card.falsifier);
        t.outboundThreatRemembered = true;
        card = StudioExplorationRunner.ReviewCavernDesign(r);
        Assert.AreEqual("ReturnPressure", card.stage); StringAssert.Contains("不强迫", card.falsifier);
        t.postLootControls = 1; t.controlAccepted = 200;
        Assert.AreEqual("HumanReview", StudioExplorationRunner.ReviewCavernDesign(r).stage, "More buttons do not certify fun");
    }

    [Test]
    public void ResearchReviewTreatsSaturatedObservationAsUnknownNotComplete()
    {
        var r = DesignReviewFixture(); var t = r.trials[8]; t.wallEpisodes.Clear();
        for (int i = 0; i < 16; i++) t.wallEpisodes.Add(new MechanismExplorationPlan.WallEpisode {
            source = "F" + i, leg = "Out", beganAt = 1, endedAt = 2, outcome = "LostCue" });
        Assert.AreEqual("Observation", StudioExplorationRunner.ReviewCavernDesign(r).stage);
        StringAssert.Contains("16条也不等于完整覆盖", StudioExplorationRunner.ReviewCavernDesign(r).falsifier);
    }

    [Test]
    public void ResearchReviewReturnsEvidenceForUnsupportedOrNullReports()
    {
        Assert.AreEqual("Evidence", StudioExplorationRunner.ReviewCavernDesign(null).stage);
        Assert.AreEqual("Evidence", StudioExplorationRunner.ReviewCavernDesign(DuelReportFixture()).stage);
        var r = DesignReviewFixture(); r.scenarios = null;
        Assert.AreEqual("Evidence", StudioExplorationRunner.ReviewCavernDesign(r).stage);
    }

    [Test]
    public void ResearchReviewBoundsExamplesAndKeepsWhitespaceNotesUnknown()
    {
        var r = DesignReviewFixture(); AddDesignConfirmations(r); r.playerNotes.Add("  ");
        var card = StudioExplorationRunner.ReviewCavernDesign(r);
        Assert.AreEqual("HumanReview", card.stage); StringAssert.Contains("相关首轮4局、确认4局", card.evidence);
        Assert.AreEqual(4, card.evidence.Split(new[] { "完整路线[" }, System.StringSplitOptions.None).Length - 1);
        StringAssert.Contains("其余记录见完整明细", card.evidence);
        r.trials[0].feedback.Add("需要看捣蛋者视角");
        Assert.AreEqual("HumanFollowup", StudioExplorationRunner.ReviewCavernDesign(r).stage);
    }

    [Test]
    public void ResearchReviewIsIncludedInSummaryWithoutMutatingReportFields()
    {
        var r = DesignReviewFixture(); r.toolRevision = "S175"; r.verdict = "original-verdict";
        string plan = r.scenarios[0].ascii;
        string summary = StudioExplorationRunner.BuildSummary(r);
        StringAssert.Contains("S177只读设计复盘（不重标旧报告）", summary);
        StringAssert.Contains("如何推翻本假设", summary);
        Assert.AreEqual("S175", r.toolRevision); Assert.AreEqual("original-verdict", r.verdict);
        Assert.AreEqual(plan, r.scenarios[0].ascii); Assert.AreEqual(12, r.trials.Count);
    }

    [TestCase(1f)]
    [TestCase(-1f)]
    public void SolidStepExitRequiresRealStaticSupportLandingAndClearance(float direction)
    {
        var runner = new GameObject("StepRunner"); var upper = new GameObject("StepUpper");
        var lower = new GameObject("StepLower"); var obstacle = new GameObject("StepBlocker");
        try
        {
            const float origin = 52000;
            Vector2 position = new Vector2(origin + 24.89f * direction, 8.015f);
            Vector2 target = new Vector2(origin + 25f * direction, 7f);
            runner.transform.position = position;
            var body = runner.AddComponent<BoxCollider2D>(); body.size = new Vector2(0.8f, 0.95f); body.offset = new Vector2(0, -0.025f);
            upper.transform.position = new Vector3(origin + 24f * direction, 7, 0);
            var support = upper.AddComponent<BoxCollider2D>();
            lower.transform.position = new Vector3(origin + 25f * direction, 6, 0);
            var landing = lower.AddComponent<BoxCollider2D>();
            obstacle.transform.position = new Vector3(origin + 25.45f * direction, 8, 0);
            var cover = obstacle.AddComponent<BoxCollider2D>(); cover.size = new Vector2(0.2f, 1f); cover.enabled = false;
            Physics2D.SyncTransforms();
            float aim = ExplorationTrialObserver.GuidedBot.FindSolidStepExitAim(body, position, target);
            Assert.AreEqual(origin + 24.98f * direction, aim, 0.01f, "Probe the narrow overlap at the foot edge, not just the centre");
            Assert.AreEqual(position.x, runner.transform.position.x, "The query never moves the actor");
            landing.enabled = false; Physics2D.SyncTransforms();
            Assert.IsTrue(float.IsNaN(ExplorationTrialObserver.GuidedBot.FindSolidStepExitAim(body, position, target)), "No blind descent into a gap");
            landing.enabled = true; upper.AddComponent<PlatformEffector2D>(); support.usedByEffector = true; Physics2D.SyncTransforms();
            Assert.IsTrue(float.IsNaN(ExplorationTrialObserver.GuidedBot.FindSolidStepExitAim(body, position, target)), "One-way platforms keep their own drop-through input");
            support.usedByEffector = false; cover.enabled = true; Physics2D.SyncTransforms();
            Assert.IsTrue(float.IsNaN(ExplorationTrialObserver.GuidedBot.FindSolidStepExitAim(body, position, target)), "Solid obstacle ahead blocks the walk-off");
            cover.enabled = false; landing.isTrigger = true; Physics2D.SyncTransforms();
            Assert.IsTrue(float.IsNaN(ExplorationTrialObserver.GuidedBot.FindSolidStepExitAim(body, position, target)));
            landing.isTrigger = false; lower.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic; Physics2D.SyncTransforms();
            Assert.IsTrue(float.IsNaN(ExplorationTrialObserver.GuidedBot.FindSolidStepExitAim(body, position, target)), "No assumption of future moving-platform support");
        }
        finally { Object.DestroyImmediate(runner); Object.DestroyImmediate(upper); Object.DestroyImmediate(lower); Object.DestroyImmediate(obstacle); }
    }

    [Test]
    public void FeedbackExportPrecedesOptionalDemosAndAllLongHighlights()
    {
        Assert.IsNotNull(typeof(TestConsoleWindow));
        string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Editor/TestConsoleWindow.Exploration.cs"));
        int export = source.IndexOf("导出本次完整反馈ZIP");
        Assert.Greater(export, 0);
        Assert.Less(export, source.IndexOf("可选：看地下去程与返程选择"));
        Assert.Less(export, source.IndexOf("StudioExplorationRunner.DuelReportHighlights"));
        Assert.Less(source.IndexOf("if (duelReportDetails)"), source.IndexOf("StudioExplorationRunner.DuelReportHighlights"));
        Assert.Less(export, source.IndexOf("designCard.Brief"));
        Assert.Less(source.IndexOf("if (duelReportDetails)"), source.IndexOf("designCard.Detail"));
        StringAssert.DoesNotContain("下一步：看地下去程与返程选择", source);
    }

    [TestCase("Complete")]
    [TestCase("Blocked")]
    [TestCase("Interrupted")]
    public void FeedbackHandoffKeepsFailedAndIncompleteRecordsWithoutDemandingReplay(string status)
    {
        var r = WallComparisonFixture(); r.status = status; r.trials[0].outcome = "NoProgress";
        string text = StudioExplorationRunner.DuelFeedbackHandoff(r);
        StringAssert.Contains("首轮12局、确认0局", text);
        StringAssert.Contains("一份ZIP包含A/B两组", text);
        StringAssert.Contains("不必先重跑、观战或通关", text);
        Assert.AreEqual("NoProgress", r.trials[0].outcome); Assert.AreEqual(status, r.status);
    }

    [Test]
    public void SolidStepReportKeepsOldEvidenceUnknownAndInputsSeparateFromLandings()
    {
        var t = new MechanismExplorationPlan.Trial();
        StringAssert.Contains("实体落阶输入未记录（旧版）", StudioExplorationRunner.DuelTrialRouteSummary(t));
        t.navigationPolicy = "AuthoredRoutes+SolidStepExitV1"; t.solidStepInputFrames = 20; t.solidStepInputTargets = 1; t.solidStepInputSeconds = 0.4f;
        string text = StudioExplorationRunner.DuelTrialRouteSummary(t);
        StringAssert.Contains("未记录完整路线", text); StringAssert.Contains("不是落地成功", text);
        Assert.IsEmpty(t.completedRoutes);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WallPerceptionRequiresLocalSameLevelUnobstructedPublicCue(bool blocked)
    {
        var wallObject = new GameObject("VisibleWall"); var cover = new GameObject("SolidCover");
        Sprite visualSprite = null;
        try
        {
            wallObject.transform.position = new Vector3(54002, 1, 0);
            var wall = wallObject.AddComponent<FakeWall>();
            visualSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
            var visual = wallObject.AddComponent<SpriteRenderer>(); visual.sprite = visualSprite;
            Assert.IsFalse(ExplorationTrialObserver.GuidedBot.CanReadWall(wall, new Vector2(54000, 1)), "AI must not read an unadvertised phase");
            wall.ShowPublicWallCue = true;
            var box = wallObject.GetComponent<BoxCollider2D>(); box.isTrigger = true;
            cover.transform.position = new Vector3(54001, 1, 0);
            var blocker = cover.AddComponent<BoxCollider2D>(); blocker.enabled = blocked;
            Physics2D.SyncTransforms();
            Assert.AreEqual(!blocked, ExplorationTrialObserver.GuidedBot.CanReadWall(wall, new Vector2(54000, 1)));
            Assert.IsFalse(ExplorationTrialObserver.GuidedBot.CanReadWall(wall, new Vector2(53990, 1)));
            Assert.IsFalse(ExplorationTrialObserver.GuidedBot.CanReadWall(wall, new Vector2(54002, 5)));
            blocker.isTrigger = true; Physics2D.SyncTransforms();
            Assert.IsTrue(ExplorationTrialObserver.GuidedBot.CanReadWall(wall, new Vector2(54000, 1)));
            blocker.isTrigger = false; blocker.enabled = true;
            cover.AddComponent<TricksterController>(); Physics2D.SyncTransforms();
            Assert.IsTrue(ExplorationTrialObserver.GuidedBot.CanReadWall(wall, new Vector2(54000, 1)), "Actor bodies are not opaque terrain");
            visual.enabled = false;
            Assert.IsFalse(ExplorationTrialObserver.GuidedBot.CanReadWall(wall, new Vector2(54000, 1)));
            visual.enabled = true; wall.enabled = false;
            Assert.IsFalse(ExplorationTrialObserver.GuidedBot.CanReadWall(wall, new Vector2(54000, 1)));
        }
        finally { Object.DestroyImmediate(wallObject); Object.DestroyImmediate(cover); if (visualSprite != null) Object.DestroyImmediate(visualSprite); }
    }

    [Test]
    public void WallPolicyOptInIsExplicitAndSurvivesOnlySupportedCavernIterations()
    {
        var old = MechanismExplorationPlan.BuildDuel(168); old.wallTacticsVersion = 1;
        Assert.IsFalse(MechanismExplorationPlan.IsGeneratedDuelLayout(old));
        var cave = MechanismExplorationPlan.BuildCavernDuel(168);
        Assert.AreEqual(0, cave.wallTacticsVersion, "Old cavern builder/replay stays baseline by default");
        cave.wallTacticsVersion = 1;
        Assert.IsTrue(MechanismExplorationPlan.IsGeneratedDuelLayout(cave));
        var child = MechanismExplorationPlan.NextDuelVariant(cave, "real evidence required by caller");
        Assert.AreEqual(1, child.wallTacticsVersion); Assert.AreEqual(cave.ascii, child.ascii);
        cave.wallTacticsVersion = 2;
        Assert.IsFalse(MechanismExplorationPlan.IsGeneratedDuelLayout(cave));
    }

    [TestCase(168)]
    [TestCase(int.MinValue)]
    public void WallComparisonUsesIdenticalIndependentGeometryAndTwelveUniqueFirstSlots(int seed)
    {
        var rooms = MechanismExplorationPlan.BuildWallTacticsComparison(seed);
        Assert.AreEqual(2, rooms.Count); Assert.AreEqual(rooms[0].ascii, rooms[1].ascii);
        Assert.AreNotEqual(rooms[0].id, rooms[1].id); Assert.AreEqual(0, rooms[0].wallTacticsVersion); Assert.AreEqual(1, rooms[1].wallTacticsVersion);
        Assert.AreNotSame(rooms[0].routes, rooms[1].routes); Assert.AreNotSame(rooms[0].tunnelLinks, rooms[1].tunnelLinks);
        Assert.IsTrue(rooms.All(MechanismExplorationPlan.IsGeneratedDuelLayout)); Assert.AreEqual(12, MechanismExplorationPlan.TrialCount(rooms));
        var keys = new HashSet<string>();
        for (int i = 0; i < 12; i++) { var slot = MechanismExplorationPlan.TrialAt(rooms, new List<string>(), i); Assert.AreEqual(1, slot.attempt); Assert.IsTrue(keys.Add(slot.scenario.id + slot.matchup.Id)); }
        rooms[1].routes[0].points[0].x += 1; Assert.AreNotEqual(rooms[0].routes[0].points[0].x, rooms[1].routes[0].points[0].x);
    }

    private static StudioExplorationRunner.Report WallComparisonFixture()
    {
        var r = DuelReportFixture(); r.scope = "WallTacticsComparison"; r.regressionPassed = 1;
        r.scenarios = MechanismExplorationPlan.BuildWallTacticsComparison(168); r.trials.Clear();
        foreach (var room in r.scenarios)
        foreach (var t in DuelReportFixture(room).trials)
        {
            t.cavernEvidenceVersion = 1; t.undergroundSeconds = t.surfaceSeconds = 5;
            if (t.marioStrategy == "Adaptive") { t.completedRoutes.Add("Out:lower"); t.completedRoutes.Add("Return:lower"); }
            t.wallEvidenceVersion = 1;
            t.wallPolicy = room.wallTacticsVersion == 1 && t.marioStrategy == "Adaptive" ? "VisibleWallWindowV1" : "ObserveOnly";
            r.trials.Add(t);
        }
        return r;
    }

    [Test]
    public void WallDiagnosisDistinguishesNoOpportunityNoOverrideAndActualCrossing()
    {
        var r = WallComparisonFixture(); var t = r.trials[8];
        StringAssert.Contains("未采到局部可见", StudioExplorationRunner.WallTrialDiagnosis(t));
        t.wallEpisodes.Add(new MechanismExplorationPlan.WallEpisode { source = "F1", leg = "Out", beganAt = 2, endedAt = 5, outcome = "BudgetExpired", sawWarning = true });
        StringAssert.Contains("未接管输入", StudioExplorationRunner.WallTrialDiagnosis(t));
        var e = t.wallEpisodes[0]; e.inputAt = 2.5f; e.inputFrames = 1;
        StringAssert.Contains("已尝试但未记录", StudioExplorationRunner.WallTrialDiagnosis(t));
        e.sawSolid = e.sawReopen = true; e.crossedAt = 4; e.outcome = "CrossedAfterReopen"; e.damageObserved = true;
        StringAssert.Contains("不证明骗出了", StudioExplorationRunner.WallTrialDiagnosis(t));
        StringAssert.Contains("期间损血1", StudioExplorationRunner.WallTrialDiagnosis(t));
        t.wallEvidenceVersion = 0;
        StringAssert.Contains("旧记录", StudioExplorationRunner.WallTrialDiagnosis(t));
    }

    [TestCase("missing")]
    [TestCase("confirmation")]
    [TestCase("geometry")]
    [TestCase("samePolicy")]
    [TestCase("wrongPolicy")]
    [TestCase("baselineInput")]
    [TestCase("falseCross")]
    [TestCase("nanTime")]
    public void WallComparisonRefusesIncompleteOrContaminatedEvidence(string fault)
    {
        var r = WallComparisonFixture(); var t = r.trials[8];
        if (fault == "missing") r.trials.RemoveAt(0);
        if (fault == "confirmation") r.confirmationScenarioIds.Add(r.scenarios[0].id);
        if (fault == "geometry") r.scenarios[1].ascii += " ";
        if (fault == "samePolicy") r.scenarios[1].wallTacticsVersion = 0;
        if (fault == "wrongPolicy") t.wallPolicy = "ObserveOnly";
        if (fault == "baselineInput") r.trials[0].wallEpisodes.Add(new MechanismExplorationPlan.WallEpisode {
            source = "F", leg = "Out", beganAt = 1, endedAt = 2, inputAt = 1, inputFrames = 1, outcome = "LostCue" });
        if (fault == "falseCross") t.wallEpisodes.Add(new MechanismExplorationPlan.WallEpisode {
            source = "F", leg = "Out", beganAt = 1, endedAt = 2, crossedAt = 2, outcome = "CrossedAfterReopen" });
        if (fault == "nanTime") t.wallEpisodes.Add(new MechanismExplorationPlan.WallEpisode {
            source = "F", leg = "Out", beganAt = float.NaN, endedAt = 2, outcome = "LostCue" });
        StringAssert.DoesNotContain("同图/同代码策略对照", StudioExplorationRunner.WallComparisonSummary(r));
    }

    [Test]
    public void WallComparisonPreservesWorseOutcomesAndDoesNotTreatSurfaceAsTreatment()
    {
        var r = WallComparisonFixture(); r.trials[8].runnerHealthLost = 1; r.trials[8].outcome = "RunnerStopped";
        string summary = StudioExplorationRunner.WallComparisonSummary(r);
        StringAssert.Contains("Cleared→RunnerStopped", summary); StringAssert.Contains("损血0→1", summary);
        StringAssert.Contains("地表固定路线检查环境波动", summary); StringAssert.Contains("单次差异不是因果证明", summary);
        Assert.AreEqual("RunnerStopped", r.trials[8].outcome);
    }

    private static StudioExplorationRunner.Report CavernReportFixture()
    {
        var r = DuelReportFixture(MechanismExplorationPlan.BuildCavernDuel(168));
        foreach (var t in r.trials)
        {
            t.cavernEvidenceVersion = 1;
            if (t.marioStrategy == "Adaptive") { t.undergroundSeconds = 5; t.completedRoutes.Add("Out:lower"); t.completedRoutes.Add("Return:lower"); }
            else t.surfaceSeconds = 5;
        }
        return r;
    }

    [Test]
    public void CavernCompleteReportCanProposeLinkOnlyChildWithoutRewritingOldMap()
    {
        var r = CavernReportFixture(); var room = r.scenarios[0];
        Assert.IsEmpty(StudioExplorationRunner.IterationBlockReason(r, room));
        var next = StudioExplorationRunner.ProposeDuelIteration(r, room);
        Assert.AreEqual(2, next.duelVersion); Assert.AreEqual(room.ascii, next.ascii);
        Assert.AreEqual(0, room.iteration); StringAssert.Contains("洞室", next.mutationReason);
        StringAssert.Contains("未形成出手", StudioExplorationRunner.CavernDesignSummary(r, room));
        r.trials[2].undergroundControls = 1;
        StringAssert.Contains("返程仍空", StudioExplorationRunner.CavernDesignSummary(r, room));
    }

    [TestCase("oldEvidence")]
    [TestCase("missingLower")]
    [TestCase("falseDetour")]
    [TestCase("unfinishedDetour")]
    [TestCase("nanResidence")]
    public void CavernIterationNeverTreatsRequestsAsCompletedAlternativeReturn(string fault)
    {
        var r = CavernReportFixture(); var t = r.trials[2];
        if (fault == "oldEvidence") t.cavernEvidenceVersion = 0;
        if (fault == "missingLower") t.completedRoutes.Clear();
        if (fault == "falseDetour") { t.returnDetourRequests = 1; t.completedRoutes.Add("Return:upper"); }
        if (fault == "unfinishedDetour") { t.returnDetourRequests = 1; t.outboundThreatRemembered = true; }
        if (fault == "nanResidence") t.undergroundSeconds = float.NaN;
        Assert.IsNotEmpty(StudioExplorationRunner.IterationBlockReason(r, r.scenarios[0]));
        StringAssert.Contains("尚未完整", StudioExplorationRunner.DuelNextAction(r, r.scenarios[0]));
    }

    [Test]
    public void CavernDetourNeedsActualReturnAndDoesNotBorrowConfirmation()
    {
        var r = CavernReportFixture(); var t = r.trials[2];
        t.returnDetourRequests = 1; t.outboundThreatRemembered = true; t.surfaceSeconds = 5;
        t.completedRoutes.Remove("Return:lower"); t.completedRoutes.Add("Return:upper");
        Assert.IsEmpty(StudioExplorationRunner.IterationBlockReason(r, r.scenarios[0]));
        var confirmation = CavernReportFixture().trials[2]; confirmation.attempt = 2; confirmation.completedRoutes.Clear();
        r.trials.Add(confirmation);
        Assert.IsNotEmpty(StudioExplorationRunner.IterationBlockReason(r, r.scenarios[0]));
        Assert.AreEqual(1, t.returnDetourRequests);
    }

    [TestCase("pendingConfirmation")]
    [TestCase("extraTrial")]
    [TestCase("humanTrial")]
    public void CavernIterationRequiresAllScheduledEvidenceNotJustSixFirstRuns(string fault)
    {
        var r = CavernReportFixture();
        if (fault == "pendingConfirmation") r.confirmationScenarioIds.Add(r.scenarios[0].id);
        if (fault == "extraTrial") { var t = CavernReportFixture().trials[0]; t.attempt = 3; r.trials.Add(t); }
        if (fault == "humanTrial") r.trials[0].controlMode = "HumanMario";
        Assert.IsNotEmpty(StudioExplorationRunner.IterationBlockReason(r, r.scenarios[0]));
        Assert.IsFalse(StudioExplorationRunner.DuelReportReadyForReview(r, r.scenarios[0]));
    }

    [Test]
    public void CavernSummaryDoesNotCertifyDemoOrInventOldEvidence()
    {
        var r = CavernReportFixture(); r.controlMode = "Demonstration";
        StringAssert.Contains("不能据此自动改图", StudioExplorationRunner.CavernDesignSummary(r, r.scenarios[0]));
        Assert.AreEqual("", StudioExplorationRunner.CavernDesignSummary(DuelReportFixture(), MechanismExplorationPlan.BuildDuel(168)));
    }

    private static StudioExplorationRunner.Report ImportReportFixture(string stamp = "2026-09-22T15:39:43Z")
    {
        var r = DuelReportFixture(); r.startedUtc = stamp; r.toolRevision = "S171";
        r.parentReport = "Z:/old-computer/not-followed";
        return r;
    }

    private static byte[] FeedbackZipFixture(params string[] entries)
    {
        using (var stream = new MemoryStream())
        {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                foreach (string entry in entries)
                    using (var writer = new StreamWriter(zip.CreateEntry(entry).Open())) writer.Write("{}");
            return stream.ToArray();
        }
    }

    [TestCase("../report.json")]
    [TestCase("a/../../report.json")]
    [TestCase("/report.json")]
    [TestCase("C:/report.json")]
    [TestCase("a\\report.json")]
    [TestCase("a//report.json")]
    [TestCase("a./report.json")]
    [TestCase("a /report.json")]
    [TestCase("a/./report.json")]
    [TestCase("a/report.json:stream")]
    public void FeedbackArchiveRejectsTraversalAndAmbiguousPaths(string entry)
    {
        Assert.IsFalse(StudioExplorationRunner.SafeFeedbackEntryName(entry));
        using (var stream = new MemoryStream(FeedbackZipFixture(entry)))
            Assert.Throws<InvalidDataException>(() => StudioExplorationRunner.ReadFeedbackArchive(stream));
    }

    [TestCase("../escaped")]
    [TestCase("CON")]
    [TestCase("nul")]
    [TestCase("COM1")]
    [TestCase("LPT9")]
    public void ReportScenarioIdsCannotEscapeReplayOrTargetWindowsDevices(string id)
    {
        var r = ImportReportFixture(); r.scenarios[0].id = id;
        Assert.IsFalse(StudioExplorationRunner.SafeFeedbackScenarioId(id));
        Assert.Throws<InvalidDataException>(() => StudioExplorationRunner.ValidateFeedbackReport(r));
    }

    [Test]
    public void ArchiveAllowlistIgnoresBackupsScriptsAndImporterMetadata()
    {
        using (var stream = new MemoryStream(FeedbackZipFixture("batch/report.json", "batch/summary.txt", "batch/TestReport.txt",
            "batch/report.json.bak", "batch/import_links.json", "Assets/Evil.cs")))
        {
            var files = StudioExplorationRunner.ReadFeedbackArchive(stream);
            CollectionAssert.AreEquivalent(new[] { "batch/report.json", "batch/summary.txt", "batch/TestReport.txt" }, files.Keys);
        }
        using (var stream = new MemoryStream(FeedbackZipFixture("report.json", "REPORT.JSON")))
            Assert.Throws<InvalidDataException>(() => StudioExplorationRunner.ReadFeedbackArchive(stream));
        using (var stream = new MemoryStream(new byte[] { 1, 2, 3 }))
            Assert.Throws<InvalidDataException>(() => StudioExplorationRunner.ReadFeedbackArchive(stream));
    }

    [TestCase("file")]
    [TestCase("total")]
    [TestCase("entries")]
    public void ArchiveBudgetsRejectCompressedBombsBeforeExtraction(string fault)
    {
        using (var stream = new MemoryStream())
        {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                int count = fault == "entries" ? 1025 : fault == "total" ? 9 : 1;
                var block = new byte[8192];
                for (int i = 0; i < count; i++)
                using (var output = zip.CreateEntry(i + "/report.json").Open())
                {
                    if (fault == "entries") continue;
                    for (int b = 0; b < 1024; b++) output.Write(block, 0, block.Length);
                    if (fault == "file") output.WriteByte(0);
                }
            }
            stream.Position = 0;
            Assert.Throws<InvalidDataException>(() => StudioExplorationRunner.ReadFeedbackArchive(stream));
        }
    }

    [Test]
    public void MultiBatchImportDeduplicatesExactSnapshotsAndKeepsLocalParentChain()
    {
        var a = Encoding.UTF8.GetBytes("{\"a\":1}"); var b = Encoding.UTF8.GetBytes("{\"b\":1}");
        var files = new Dictionary<string, byte[]> { ["first/report.json"] = a, ["second/report.json"] = b,
            ["second/parent_report.json"] = a, ["second/baseline_report.json"] = a };
        int parses = 0;
        var docs = StudioExplorationRunner.BuildFeedbackImportPlan(files, text => {
            parses++; var r = ImportReportFixture(text.Contains("b") ? "2026-09-22T15:40:00Z" : "2026-09-22T15:39:00Z");
            r.controlMode = text.Contains("b") ? "Automated" : "Demonstration"; return r;
        });
        Assert.AreEqual(2, docs.Length); Assert.AreEqual(2, parses);
        Assert.AreEqual(StudioExplorationRunner.FeedbackDocumentKey(b), docs[0].key);
        Assert.AreEqual(docs[1].key, docs[0].parentKey); Assert.AreEqual(docs[1].key, docs[0].baselineKey);
        Assert.AreEqual("Z:/old-computer/not-followed", docs[0].data.parentReport);
        CollectionAssert.AreEqual(b, docs[0].bytes);
        StringAssert.Contains("Automated", docs[0].label); StringAssert.Contains("Demonstration", docs[1].label);
        files["first/parent_report.json"] = b;
        Assert.Throws<InvalidDataException>(() => StudioExplorationRunner.BuildFeedbackImportPlan(files, text => ImportReportFixture()));
    }

    [Test]
    public void ImportOrdersRealInstantsAndDoesNotConflateConnectionWithIteration()
    {
        var files = new Dictionary<string, byte[]> { ["a/report.json"] = Encoding.UTF8.GetBytes("{\"a\":1}"),
            ["b/report.json"] = Encoding.UTF8.GetBytes("{\"b\":1}") };
        var docs = StudioExplorationRunner.BuildFeedbackImportPlan(files, text => {
            var r = ImportReportFixture(text.Contains("a") ? "2026-09-22T16:00:00+08:00" : "2026-09-22T09:00:00Z");
            r.scenarios[0].duelVariant = 2; r.scenarios[0].iteration = 0; return r;
        });
        Assert.AreEqual("2026-09-22T09:00:00Z", docs[0].data.startedUtc);
        StringAssert.Contains("连接2/进度0", docs[0].label);
        StringAssert.Contains("AI完整对照", docs[0].label);
    }

    [TestCase("parent")]
    [TestCase("baseline")]
    public void ConflictingSnapshotsCannotSilentlyRelinkTheSameReport(string relation)
    {
        var main = Encoding.UTF8.GetBytes("{}"); var a = Encoding.UTF8.GetBytes("{\"a\":1}"); var b = Encoding.UTF8.GetBytes("{\"b\":1}");
        var files = new Dictionary<string, byte[]> { ["one/report.json"] = main, ["two/report.json"] = main,
            ["one/" + relation + "_report.json"] = a, ["two/" + relation + "_report.json"] = b };
        Assert.Throws<InvalidDataException>(() => StudioExplorationRunner.BuildFeedbackImportPlan(files, text => ImportReportFixture()));
    }

    [Test]
    public void ImportRejectsMissingMainMalformedUtf8AndExcessiveReports()
    {
        var files = new Dictionary<string, byte[]> { ["parent_report.json"] = Encoding.UTF8.GetBytes("{}") };
        Assert.Throws<InvalidDataException>(() => StudioExplorationRunner.BuildFeedbackImportPlan(files, text => ImportReportFixture()));
        files.Clear(); files["report.json"] = new byte[] { 0xc3, 0x28 };
        Assert.Throws<DecoderFallbackException>(() => StudioExplorationRunner.BuildFeedbackImportPlan(files, text => ImportReportFixture()));
        files["report.json"] = Encoding.UTF8.GetBytes("[]");
        Assert.Throws<InvalidDataException>(() => StudioExplorationRunner.BuildFeedbackImportPlan(files, text => ImportReportFixture()));
        files.Clear();
        for (int i = 0; i < 129; i++) files[i + "/report.json"] = Encoding.UTF8.GetBytes("{\"id\":" + i + "}");
        Assert.Throws<InvalidDataException>(() => StudioExplorationRunner.BuildFeedbackImportPlan(files, text => ImportReportFixture()));
    }

    [TestCase("version")]
    [TestCase("date")]
    [TestCase("scenario")]
    [TestCase("unknownTrial")]
    [TestCase("mode")]
    [TestCase("confirmation")]
    [TestCase("nullEvidence")]
    public void ImportSchemaValidationRejectsMalformedDataWithoutRepairingIt(string fault)
    {
        var r = ImportReportFixture();
        if (fault == "version") r.version = 999;
        if (fault == "date") r.startedUtc = "bad";
        if (fault == "scenario") r.scenarios.Add(r.scenarios[0]);
        if (fault == "unknownTrial") r.trials[0].scenarioId = "unknown";
        if (fault == "mode") r.controlMode = "ExecuteCode";
        if (fault == "confirmation") r.confirmationScenarioIds.Add("unknown");
        if (fault == "nullEvidence") r.trials[0].coverage.Add(null);
        Assert.Throws<InvalidDataException>(() => StudioExplorationRunner.ValidateFeedbackReport(r));
    }

    [TestCase("Blocked")]
    [TestCase("Running")]
    [TestCase("Aborted")]
    public void ImportAcceptsFailureAndUnfinishedEvidenceWithoutRelabellingIt(string status)
    {
        var r = ImportReportFixture(); r.status = status; r.regressionFailed = 1; r.trials.Clear();
        var files = new Dictionary<string, byte[]> { ["report.json"] = Encoding.UTF8.GetBytes("{}") };
        var docs = StudioExplorationRunner.BuildFeedbackImportPlan(files, text => r);
        Assert.AreEqual(status, docs[0].data.status); Assert.AreEqual(1, docs[0].data.regressionFailed);
        Assert.IsEmpty(docs[0].data.trials); Assert.IsNull(docs[0].parentKey, "Never follow the remote original parent path");
    }

    [Test]
    public void CompactImportPathsFitUploadedWindowsProjectWithoutTruncatingIdentifiers()
    {
        string output = @"E:\BaiduNetdiskDownload\MarioTrickster-genspark_ai_developer (1)\MarioTrickster-genspark_ai_developer\reports\ai_exploration";
        var token = System.Guid.ParseExact("a3bf96763a394873a53a7ca59fa6c8dd", "N");
        string key = new string('a', 64);
        string legacy = Path.Combine(output, "import_20260922_162552_" + token.ToString("N"), key);
        Assert.AreEqual(257, Path.Combine(legacy, "report.json").Length);
        Assert.AreEqual(264, Path.Combine(legacy, "parent_report.json").Length);
        Assert.Throws<PathTooLongException>(() => StudioExplorationRunner.ValidateFeedbackStorageDirectory(legacy));
        foreach (bool staging in new[] { true, false })
        {
            string collection = StudioExplorationRunner.FeedbackImportCollectionName(token, staging);
            StringAssert.EndsWith(token.ToString("N"), collection);
            string directory = Path.Combine(output, collection, key);
            Assert.DoesNotThrow(() => StudioExplorationRunner.ValidateFeedbackStorageDirectory(directory));
            Assert.Less(Path.Combine(directory, "baseline_report.json").Length, 260);
            Assert.Less((directory + "_feedback_12345678.zip").Length, 260);
            Assert.AreEqual(key, Path.GetFileName(directory));
        }
        Assert.AreNotEqual(StudioExplorationRunner.FeedbackImportCollectionName(token, true),
            StudioExplorationRunner.FeedbackImportCollectionName(token, false));
    }

    [TestCase(237, true)]
    [TestCase(238, false)]
    [TestCase(260, false)]
    public void ImportPathBudgetIncludesMetadataAndLaterExport(int length, bool allowed)
    {
        string directory = "C:\\" + new string('x', length - 3);
        if (allowed) Assert.DoesNotThrow(() => StudioExplorationRunner.ValidateFeedbackStorageDirectory(directory));
        else Assert.Throws<PathTooLongException>(() => StudioExplorationRunner.ValidateFeedbackStorageDirectory(directory));
    }

    [Test]
    public void ShortStagingDoesNotCertifyLongerFinalReportPath()
    {
        StudioExplorationRunner.ValidateFeedbackStorageDirectory(new string('x', 237));
        Assert.Throws<PathTooLongException>(() => StudioExplorationRunner.ValidateFeedbackStorageDirectory(new string('x', 238)));
    }

    [Test]
    public void BlockedImportedDemoCannotInventGameplayOrCertifyCompletedReview()
    {
        var r = ImportReportFixture(); r.toolRevision = "S172"; r.status = "Blocked"; r.controlMode = "Demonstration";
        r.regressionPassed = 523; r.regressionFailed = 1; r.trials.Clear();
        r.scenarios[0].selectedMatchups = new[] { new MechanismExplorationPlan.Matchup { mario = "SafeRoute", trickster = "TunnelChaser" } };
        StudioExplorationRunner.ValidateFeedbackReport(r);
        Assert.AreEqual(1, StudioExplorationRunner.UnverifiedSlots(r));
        Assert.IsFalse(StudioExplorationRunner.DuelReportReadyForReview(r, r.scenarios[0]));
        Assert.IsNotEmpty(StudioExplorationRunner.IterationBlockReason(r, r.scenarios[0]));
        Assert.AreEqual("Blocked", r.status); Assert.AreEqual("S172", r.toolRevision); Assert.IsEmpty(r.trials);
    }

    [Test]
    public void UnityImportWritesExactSnapshotsAndResolvesOnlyGeneratedLocalLinks()
    {
        var parent = ImportReportFixture("2026-09-22T15:39:00Z"); parent.status = "Blocked"; parent.trials.Clear();
        var child = ImportReportFixture("2026-09-22T15:40:00Z"); child.status = "Running";
        child.parentReport = ""; // A local snapshot is sufficient even when the original path is absent.
        byte[] p = Encoding.UTF8.GetBytes(JsonUtility.ToJson(parent)), c = Encoding.UTF8.GetBytes(JsonUtility.ToJson(child));
        var files = new Dictionary<string, byte[]> { ["report.json"] = c, ["parent_report.json"] = p, ["baseline_report.json"] = p };
        var docs = StudioExplorationRunner.BuildFeedbackImportPlan(files, text => JsonUtility.FromJson<StudioExplorationRunner.Report>(text));
        string root = null;
        try
        {
            var writer = typeof(StudioExplorationRunner).GetMethod("WriteFeedbackImportSnapshots", BindingFlags.NonPublic | BindingFlags.Static);
            var resolver = typeof(StudioExplorationRunner).GetMethod("LocalImportedParent", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(writer); Assert.IsNotNull(resolver);
            var choices = (StudioExplorationRunner.ImportedReportChoice[])writer.Invoke(null, new object[] { docs });
            root = Directory.GetParent(choices[0].directory).FullName;
            CollectionAssert.AreEqual(c, File.ReadAllBytes(Path.Combine(choices[0].directory, "report.json")));
            CollectionAssert.AreEqual(p, File.ReadAllBytes(Path.Combine(choices[0].directory, "parent_report.json")));
            CollectionAssert.AreEqual(p, File.ReadAllBytes(Path.Combine(choices[0].directory, "baseline_report.json")));
            Assert.IsNotEmpty(File.ReadAllText(Path.Combine(choices[0].directory, "import_links.json")));
            foreach (var choice in choices)
            {
                Assert.DoesNotThrow(() => StudioExplorationRunner.ValidateFeedbackStorageDirectory(choice.directory));
                Assert.AreEqual(64, Path.GetFileName(choice.directory).Length);
            }
            Assert.AreEqual(choices[1].directory, resolver.Invoke(null, new object[] { choices[0].directory }));
            Assert.AreEqual("Running", docs[0].data.status); Assert.AreEqual("Blocked", docs[1].data.status);
            Assert.AreEqual("", docs[0].data.parentReport);
            Assert.AreEqual("Z:/old-computer/not-followed", docs[1].data.parentReport);
            Assert.IsNull(resolver.Invoke(null, new object[] { choices[1].directory }));
        }
        finally { if (root != null && Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Test]
    public void CompleteFinalVariantDirectsToPlayInsteadOfAnotherBatch()
    {
        var room = MechanismExplorationPlan.BuildDuel(168, 2); room.iteration = 2;
        var report = DuelReportFixture(room);
        Assert.IsTrue(StudioExplorationRunner.DuelReportReadyForReview(report, room));
        StringAssert.Contains("不要再生成变体", StudioExplorationRunner.DuelNextAction(report, room));
        StringAssert.Contains("不必重复补跑", StudioExplorationRunner.DuelNextAction(report, room));
        Assert.AreEqual(2, room.iteration);
        StringAssert.Contains("两次变体", StudioExplorationRunner.IterationBlockReason(report, room));
    }

    [TestCase("missing")]
    [TestCase("duplicate")]
    [TestCase("pendingConfirmation")]
    [TestCase("human")]
    [TestCase("error")]
    [TestCase("regression")]
    [TestCase("extraAttempt")]
    [TestCase("blocked")]
    public void FinalVariantNeverHidesIncompleteEvidenceBehindItsCap(string fault)
    {
        var room = MechanismExplorationPlan.BuildDuel(168, 2); room.iteration = 2;
        var report = DuelReportFixture(room);
        if (fault == "missing") report.trials.RemoveAt(0);
        if (fault == "duplicate") report.trials.Add(report.trials[0]);
        if (fault == "pendingConfirmation") report.confirmationScenarioIds.Add(room.id);
        if (fault == "human") report.trials[0].controlMode = "HumanMario";
        if (fault == "error") report.trials[0].errors.Add("error");
        if (fault == "regression") report.regressionFailed = 1;
        if (fault == "extraAttempt") report.trials[0].attempt = 3;
        if (fault == "blocked") report.status = "Blocked";
        Assert.IsFalse(StudioExplorationRunner.DuelReportReadyForReview(report, room));
        StringAssert.Contains("故障", StudioExplorationRunner.DuelNextAction(report, room));
        StringAssert.DoesNotContain("不必重复补跑", StudioExplorationRunner.DuelNextAction(report, room));
    }

    [Test]
    public void FinalVariantRetainsFailedConfirmationRouteInsteadOfOfferingCleanReview()
    {
        var room = MechanismExplorationPlan.BuildDuel(168, 2); room.iteration = 2;
        var report = DuelReportFixture(room); report.confirmationScenarioIds.Add(room.id);
        foreach (var run in DuelReportFixture(room).trials) { run.attempt = 2; report.trials.Add(run); }
        report.trials[11].completedRoutes.Clear();
        StringAssert.Contains("路线未完成", StudioExplorationRunner.DuelNextAction(report, room));
        StringAssert.DoesNotContain("不必重复补跑", StudioExplorationRunner.DuelNextAction(report, room));
    }

    [TestCase("Demonstration")]
    [TestCase("HumanMario")]
    [TestCase("HumanTrickster")]
    public void SinglePlayInstructionsNeverCertifyAutomatedCoverage(string mode)
    {
        var report = DuelReportFixture(); report.controlMode = mode;
        Assert.IsFalse(StudioExplorationRunner.DuelReportReadyForReview(report, report.scenarios[0]));
        StringAssert.Contains("单局记录", StudioExplorationRunner.DuelNextAction(report, report.scenarios[0]));
        StringAssert.Contains("不替代自动6组", StudioExplorationRunner.DuelReportHighlights(report, report.scenarios[0]));
    }

    [Test]
    public void EncounterHighlightsDistinguishNoTransferLateArrivalAndLateControl()
    {
        var t = new MechanismExplorationPlan.Trial { outcome = "Cleared", seconds = 20, tunnelEvidenceVersion = 1,
            startTimingEvidenceVersion = 1, tunnelVisitEvidenceVersion = 1 };
        StringAssert.Contains("未观察到暗线换位", StudioExplorationRunner.DuelEncounterLine(t));
        t.tunnelRequests = 1;
        StringAssert.Contains("有换位请求", StudioExplorationRunner.DuelEncounterLine(t));
        t.tunnelArrivals = 1;
        t.tunnelVisits.Add(new MechanismExplorationPlan.TunnelVisit { arrivalAt = 13.49f, readyAt = 15.09f, runnerPassedWhenReady = true });
        StringAssert.Contains("就绪时玩家已越过", StudioExplorationRunner.DuelEncounterLine(t));
        t.tunnelVisits[0].firstControlAt = 18;
        StringAssert.Contains("已有连续驻留出手", StudioExplorationRunner.DuelEncounterLine(t));
        Assert.AreEqual(0, t.controlsAfterTunnel);
        t.controlsAfterTunnel = 1;
        StringAssert.Contains("3秒内", StudioExplorationRunner.DuelEncounterLine(t));
    }

    [Test]
    public void HighlightsKeepFirstFailureAndConfirmationSuccessSeparate()
    {
        var report = DuelReportFixture(); var room = report.scenarios[0];
        report.confirmationScenarioIds.Add(room.id);
        var confirm = DuelReportFixture(room).trials[5]; confirm.attempt = 2;
        confirm.tunnelArrivals = 1; confirm.controlsAfterTunnel = 1;
        confirm.comparison = "同条件结果不稳定"; report.trials.Add(confirm);
        string text = StudioExplorationRunner.DuelReportHighlights(report, room);
        StringAssert.Contains("地表首轮：未观察到暗线换位", text);
        StringAssert.Contains("确认：已有到达后3秒内", text);
        StringAssert.Contains("不稳定", text);
        StringAssert.Contains("缺少唯一记录", text);
        report.trials.Add(report.trials[5]);
        StringAssert.Contains("地表首轮：缺少唯一记录", StudioExplorationRunner.DuelReportHighlights(report, room));
    }

    [Test]
    public void ZeroRecoveryRequestsAndUnknownOldMetricsCannotCertifyPhysicalSuccess()
    {
        var report = DuelReportFixture(); var room = report.scenarios[0];
        StringAssert.Contains("未完整记录", StudioExplorationRunner.DuelReportHighlights(report, room));
        foreach (var t in report.trials) t.stairRecoveryEvidenceVersion = 1;
        StringAssert.Contains("未触发落阶重走", StudioExplorationRunner.DuelReportHighlights(report, room));
        report.trials[5].stairRecoveryRequests = 1;
        StringAssert.Contains("不能用请求数替代成功", StudioExplorationRunner.DuelReportHighlights(report, room));
        var old = report.trials[5]; old.tunnelEvidenceVersion = 0; old.tunnelArrivals = 5;
        StringAssert.Contains("暗线证据未记录", StudioExplorationRunner.DuelEncounterLine(old));
    }

    [Test]
    public void FullReplayCopiesReportGeometryWithoutRegeneratingOrMutatingParent()
    {
        var parent = MechanismExplorationPlan.BuildDuel(168, 2); parent.iteration = 2;
        parent.parentScenarioId = "keep-parent"; parent.mutationReason = "keep-reason";
        parent.ascii += "\n"; // Manual content must be retained, even if later validation rejects it.
        parent.selectedMatchups = new[] { new MechanismExplorationPlan.Matchup { mario = "SafeRoute", trickster = "TunnelChaser" } };
        string saved = JsonUtility.ToJson(parent);
        var copy = StudioExplorationRunner.CopyDuelForFullReplay(parent);
        Assert.AreEqual(saved, JsonUtility.ToJson(parent));
        Assert.AreEqual(parent.id, copy.id); Assert.AreEqual(parent.ascii, copy.ascii);
        Assert.AreEqual(2, copy.duelVariant); Assert.AreEqual(2, copy.iteration);
        Assert.AreEqual("keep-parent", copy.parentScenarioId); Assert.AreEqual("keep-reason", copy.mutationReason);
        Assert.AreEqual(6, MechanismExplorationPlan.Matchups(copy).Length);
        Assert.AreEqual(1, MechanismExplorationPlan.Matchups(parent).Length);
        copy.tunnelLinks[0].seconds = 4;
        Assert.AreEqual(0.8f, parent.tunnelLinks[0].seconds);
        Assert.Throws<System.ArgumentException>(() => StudioExplorationRunner.CopyDuelForFullReplay(null));
    }

    [Test]
    public void CompleteFeedbackDoesNotRequireHumanNotesOrDuplicateBaselineRuns()
    {
        var report = DuelReportFixture(); report.regressionPassed = 429;
        string summary = StudioExplorationRunner.DuelFeedbackCompleteness(report);
        StringAssert.Contains("已完整", summary);
        StringAssert.Contains("首轮 6/6", summary);
        StringAssert.Contains("尚无真人感受", summary);
        StringAssert.Contains("不重复计样本", summary);
        report.confirmationScenarioIds.Add(report.scenarios[0].id);
        StringAssert.Contains("尚未完整", StudioExplorationRunner.DuelFeedbackCompleteness(report));
    }

    [TestCase("missing")]
    [TestCase("duplicate")]
    [TestCase("error")]
    [TestCase("human")]
    [TestCase("regression")]
    [TestCase("extraAttempt")]
    public void FeedbackCompletenessDoesNotHideMissingOrContaminatedRecords(string fault)
    {
        var report = DuelReportFixture();
        if (fault == "missing") report.trials.RemoveAt(0);
        if (fault == "duplicate") report.trials.Add(report.trials[0]);
        if (fault == "error") report.trials[0].errors.Add("error");
        if (fault == "human") report.trials[0].controlMode = "HumanMario";
        if (fault == "regression") report.regressionFailed = 1;
        if (fault == "extraAttempt") report.trials[0].attempt = 3;
        StringAssert.Contains("尚未完整", StudioExplorationRunner.DuelFeedbackCompleteness(report));
    }

    [Test]
    public void LosingSurfaceInteractionIsNotMaskedByMoreTunnelArrivals()
    {
        var parent = DuelReportFixture(); parent.trials[5].controlsAfterTunnel = 1;
        var child = DuelReportFixture(StudioExplorationRunner.ProposeDuelIteration(parent, parent.scenarios[0]));
        child.trials[5].tunnelArrivals = 2;
        string summary = StudioExplorationRunner.DuelIterationComparison(parent, child);
        StringAssert.Contains("子版失去了", summary);
        StringAssert.Contains("去程", summary); StringAssert.Contains("返程", summary);
        StringAssert.Contains("保留父版", summary);
        var next = StudioExplorationRunner.ProposeDuelIteration(child, child.scenarios[0]);
        StringAssert.Contains("地表出口", next.mutationReason);
        Assert.AreEqual(2, next.duelVariant);
    }

    [Test]
    public void OldVisitsStayUnknownAndNewLateControlsDoNotRewriteThreeSecondMetric()
    {
        var t = new MechanismExplorationPlan.Trial { tunnelArrivals = 1, controlsAfterTunnel = 0 };
        StringAssert.Contains("未记录", StudioExplorationRunner.TunnelVisitSummary(t));
        t.tunnelVisitEvidenceVersion = 1;
        t.tunnelVisits.Add(new MechanismExplorationPlan.TunnelVisit { arrivalAt = 2.67f, readyAt = 4.22f,
            runnerPassedWhenReady = true, firstControlAt = 8.07f, returnControlAt = 8.07f });
        string summary = StudioExplorationRunner.TunnelVisitSummary(t);
        StringAssert.Contains("玩家已越过1", summary);
        StringAssert.Contains("返程出手1", summary);
        Assert.AreEqual(0, t.controlsAfterTunnel, "Do not expand the historical 3-second metric to manufacture success");
    }

    [Test]
    public void DuelReviewCannotUseDuplicatedLowerTrialsAsBothRouteEvidence()
    {
        var report = DuelReportFixture();
        report.trials[5].marioStrategy = "Adaptive";
        StringAssert.Contains("尚不完整", StudioExplorationRunner.DuelReview(report, report.scenarios[0]));
    }

    [Test]
    public void CompleteDuelsWithoutFollowupDoNotReceiveUnqualifiedCandidateVerdict()
    {
        var report = DuelReportFixture();
        foreach (var t in report.trials)
        {
            t.tunnelVersion = 1; t.experienceEvidenceVersion = 1; t.experience = "TunnelDuel"; t.expectsReturn = true;
            t.armedNearbySeconds = t.tricksterStrategy == "Passive" ? 0 : 1;
            t.tunnelArrivals = t.tricksterStrategy == "TunnelChaser" ? 1 : 0;
            t.coverage.Add(new MechanismExplorationPlan.Evidence { mechanism = "F", built = 1 });
        }
        StringAssert.Contains("缺少3秒内转移后出手", StudioExplorationRunner.EvidenceVerdict(report));
    }

    [Test]
    public void DuelIterationRequiresActualFirstPassAndPreservesParent()
    {
        var report = DuelReportFixture(); var parent = report.scenarios[0];
        Assert.IsEmpty(StudioExplorationRunner.IterationBlockReason(report, parent));
        var child = StudioExplorationRunner.ProposeDuelIteration(report, parent);
        Assert.AreEqual(parent.id, child.parentScenarioId);
        Assert.AreEqual(0, parent.iteration); Assert.AreEqual(6, report.trials.Count);
        StringAssert.Contains("无转移后出手", child.mutationReason);
        var nextReport = DuelReportFixture(child);
        StringAssert.Contains("父版 → 本版", StudioExplorationRunner.DuelIterationComparison(report, nextReport));
        nextReport.trials[2].outcome = "TimedOut";
        StringAssert.Contains("Cleared → TimedOut", StudioExplorationRunner.DuelIterationComparison(report, nextReport), "Never hide the child's worse outcome");
    }

    [TestCase("missing")]
    [TestCase("duplicate")]
    [TestCase("confirmationOnly")]
    [TestCase("human")]
    [TestCase("demo")]
    [TestCase("blocked")]
    [TestCase("runtimeError")]
    [TestCase("failedRegression")]
    [TestCase("noEvidence")]
    [TestCase("contaminatedGround")]
    [TestCase("nanWait")]
    [TestCase("changedDelay")]
    [TestCase("manualRoute")]
    [TestCase("incompleteReturn")]
    public void DuelIterationRejectsUntrustworthyOrUnfinishedInputs(string fault)
    {
        var report = DuelReportFixture(); var room = report.scenarios[0];
        if (fault == "missing") report.trials.RemoveAt(0);
        if (fault == "duplicate") report.trials.Add(report.trials[0]);
        if (fault == "confirmationOnly") report.trials[0].attempt = 2;
        if (fault == "human") report.trials[0].controlMode = "HumanMario";
        if (fault == "demo") report.controlMode = "Demonstration";
        if (fault == "blocked") report.status = "Blocked";
        if (fault == "runtimeError") report.trials[0].errors.Add("real error");
        if (fault == "failedRegression") report.regressionFailed = 1;
        if (fault == "noEvidence") report.trials[0].tunnelEvidenceVersion = 0;
        if (fault == "contaminatedGround") report.trials[1].tunnelStarts = 1;
        if (fault == "nanWait") report.trials[0].actualStartWaitSeconds = float.NaN;
        if (fault == "changedDelay") report.trials[0].startDelaySeconds = 0;
        if (fault == "manualRoute") room.routes[0].points[0].x += 1;
        if (fault == "incompleteReturn") report.trials[0].escapeEvents = 0;
        Assert.IsNotEmpty(StudioExplorationRunner.IterationBlockReason(report, room));
        Assert.Throws<System.InvalidOperationException>(() => StudioExplorationRunner.ProposeDuelIteration(report, room));
    }

    [TestCase("code")]
    [TestCase("config")]
    [TestCase("budget")]
    [TestCase("wrongVariant")]
    public void DuelComparisonRejectsChangedConditions(string fault)
    {
        var parent = DuelReportFixture();
        var child = DuelReportFixture(StudioExplorationRunner.ProposeDuelIteration(parent, parent.scenarios[0]));
        if (fault == "code") child.sourceFingerprint = "changed";
        if (fault == "config") child.gameplayConfigJson = "changed";
        if (fault == "budget") child.trialLimitSeconds = 30;
        if (fault == "wrongVariant") child.scenarios[0].duelVariant = 2;
        StringAssert.Contains("不报告改善", StudioExplorationRunner.DuelIterationComparison(parent, child));
    }

    [Test]
    public void PassiveDemoIsClearlyIdentifiedAndCannotAuthorizeIteration()
    {
        var report = DuelReportFixture();
        report.controlMode = "Demonstration";
        report.trials.RemoveRange(1, report.trials.Count - 1);
        StringAssert.Contains("无干扰基线", StudioExplorationRunner.DuelReview(report, report.scenarios[0]));
        Assert.IsNotEmpty(StudioExplorationRunner.IterationBlockReason(report, report.scenarios[0]));
        StringAssert.Contains("对手不行动", StudioExplorationRunner.DuelOpponentLabel("Passive"));
    }

    [Test]
    public void LayerPreparationUsesOnlyActualUsableExplicitLinks()
    {
        var origin = new GameObject("PreparationOrigin"); var exit = new GameObject("PreparationExit");
        var invalid = new GameObject("NoProp");
        try
        {
            origin.transform.position = new Vector3(36016, 1, 0); exit.transform.position = new Vector3(36024, 5, 0);
            invalid.transform.position = new Vector3(36020, 5, 0);
            origin.AddComponent<FakeWall>(); exit.AddComponent<FakeWall>();
            var from = origin.AddComponent<PossessionAnchor>(); var to = exit.AddComponent<PossessionAnchor>();
            var cache = typeof(PossessionAnchor).GetMethod("CacheControllableProp", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(cache); cache.Invoke(from, null); cache.Invoke(to, null);
            to.underlineTransitTime = 0.8f;
            var pos = new Vector2(36011, 3.625f); var velocity = new Vector2(4, 0);
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindTunnelLayerPreparation(from, pos, velocity, true, 1.5f));
            from.connectedUnderlineNodes.Add(from); from.connectedUnderlineNodes.Add(null);
            from.connectedUnderlineNodes.Add(invalid.AddComponent<PossessionAnchor>()); from.connectedUnderlineNodes.Add(to);
            Assert.AreSame(to, ExplorationTrialObserver.GuidedBot.FindTunnelLayerPreparation(from, pos, velocity, true, 1.5f));
            Assert.AreSame(to, ExplorationTrialObserver.GuidedBot.FindTunnelLayerPreparation(from, new Vector2(36009, 2.625f), velocity, true, 1.5f),
                "A real landed ascent can prepare before reaching the exit layer; it is not a timely-intercept claim");
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindPreparedTunnelIntercept(from, pos, velocity, 1.5f, 9, 8));
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindTunnelLayerPreparation(from, pos, velocity, false, 1.5f));
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindTunnelLayerPreparation(from, pos, velocity, true, float.NaN));
            to.enabled = false;
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindTunnelLayerPreparation(from, pos, velocity, true, 1.5f));
            to.enabled = true; to.underlineTransitTime = 5;
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindTunnelLayerPreparation(from, pos, velocity, true, 1.5f));
            to.underlineTransitTime = 0.8f; exit.SetActive(false);
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindTunnelLayerPreparation(from, pos, velocity, true, 1.5f));
        }
        finally { Object.DestroyImmediate(origin); Object.DestroyImmediate(exit); Object.DestroyImmediate(invalid); }
    }

    [Test]
    public void ConfirmationFallbackIsVisibleAndCannotBeReplacedByFirstPassClears()
    {
        var report = DuelReportFixture(); var room = report.scenarios[0];
        report.confirmationScenarioIds.Add(room.id);
        foreach (var t in DuelReportFixture(room).trials) { t.attempt = 2; report.trials.Add(t); }
        var failed = report.trials[11]; failed.completedRoutes.Clear();
        failed.completedRoutes.Add("Out:lower"); failed.completedRoutes.Add("Return:lower");
        failed.routeSwitchRequests = 1;
        string summary = StudioExplorationRunner.DuelRouteSummary(report, room);
        StringAssert.Contains("首轮 3/3", summary); StringAssert.Contains("确认 2/3", summary);
        StringAssert.Contains("回退", summary);
        StringAssert.Contains("确认局地表", StudioExplorationRunner.IterationBlockReason(report, room));
        StringAssert.Contains("Out:lower", StudioExplorationRunner.DuelTrialRouteSummary(failed));
        StringAssert.Contains("未记录（旧版）", StudioExplorationRunner.DuelTrialRouteSummary(failed));
        Assert.AreEqual("Cleared", failed.outcome);
    }

    [Test]
    public void RouteSummaryDoesNotCountDuplicateOrHumanRecordsAsAutomatedSurfaceCoverage()
    {
        var report = DuelReportFixture(); var room = report.scenarios[0];
        report.trials.Add(report.trials[5]);
        StringAssert.Contains("首轮 2/3", StudioExplorationRunner.DuelRouteSummary(report, room));
        report.trials[3].controlMode = "HumanMario";
        StringAssert.Contains("首轮 1/3", StudioExplorationRunner.DuelRouteSummary(report, room));
    }

    [Test]
    public void PreparationAndRecoveryRequestsDoNotInventArrivalReadinessOrCompletion()
    {
        var t = new MechanismExplorationPlan.Trial { tunnelVisitEvidenceVersion = 1, tunnelDecisionEvidenceVersion = 1,
            tunnelPreparationRequests = 2, tunnelRequests = 2, stairRecoveryEvidenceVersion = 1, stairRecoveryRequests = 1 };
        StringAssert.Contains("准备换层2", StudioExplorationRunner.TunnelVisitSummary(t));
        StringAssert.Contains("重新就绪0", StudioExplorationRunner.TunnelVisitSummary(t));
        StringAssert.Contains("不是恢复成功", StudioExplorationRunner.DuelTrialRouteSummary(t));
        Assert.AreEqual(0, t.tunnelArrivals); Assert.AreEqual(0, t.controlsAfterTunnel); Assert.IsEmpty(t.completedRoutes);
    }

    [Test]
    public void PreparedInterceptRejectsBrakingFrameOpportunityWithActualLinkedProps()
    {
        var origin = new GameObject("SpeedEnvelopeOrigin"); var exit = new GameObject("SpeedEnvelopeExit");
        try
        {
            origin.transform.position = new Vector3(36012, 1, 0); exit.transform.position = new Vector3(36018, 1, 0);
            origin.AddComponent<FakeWall>(); exit.AddComponent<FakeWall>();
            var from = origin.AddComponent<PossessionAnchor>(); var to = exit.AddComponent<PossessionAnchor>();
            var cache = typeof(PossessionAnchor).GetMethod("CacheControllableProp", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(cache); cache.Invoke(from, null); cache.Invoke(to, null);
            to.underlineTransitTime = 0.8f; from.connectedUnderlineNodes.Add(to);
            var position = new Vector2(36000, 1); var braking = new Vector2(3, 0);
            Assert.AreSame(to, ExplorationTrialObserver.GuidedBot.FindPreparedTunnelIntercept(from, position, braking, 1.5f));
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindPreparedTunnelIntercept(from, position, braking, 1.5f, 9f, 8f));
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindPreparedTunnelIntercept(from, position, braking, 1.5f, float.NaN, 8f));
        }
        finally { Object.DestroyImmediate(origin); Object.DestroyImmediate(exit); }
    }

    [Test]
    public void PreparedInterceptUsesRealLinkedPropsAndLeavesAnImminentAmbushInPlace()
    {
        var origin = new GameObject("PreparedOrigin"); var exit = new GameObject("PreparedExit");
        try
        {
            origin.transform.position = new Vector3(36012, 1, 0); exit.transform.position = new Vector3(36036, 1, 0);
            origin.AddComponent<FakeWall>(); exit.AddComponent<FakeWall>();
            var from = origin.AddComponent<PossessionAnchor>(); var to = exit.AddComponent<PossessionAnchor>();
            var cache = typeof(PossessionAnchor).GetMethod("CacheControllableProp", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(cache); cache.Invoke(from, null); cache.Invoke(to, null);
            to.underlineTransitTime = 0.8f;
            var position = new Vector2(36000, 1); var velocity = new Vector2(7, 0);
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindPreparedTunnelIntercept(from, position, velocity, 1.5f));
            from.connectedUnderlineNodes.Add(to);
            Assert.AreSame(to, ExplorationTrialObserver.GuidedBot.FindPreparedTunnelIntercept(from, position, velocity, 1.5f));
            to.enabled = false;
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindPreparedTunnelIntercept(from, position, velocity, 1.5f));
            to.enabled = true;
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindPreparedTunnelIntercept(from, new Vector2(36010, 1), velocity, 1.5f));
            exit.transform.position = new Vector3(36008, 1, 0);
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindPreparedTunnelIntercept(from, position, velocity, 1.5f));
        }
        finally { Object.DestroyImmediate(origin); Object.DestroyImmediate(exit); }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    public void TunnelNetworkBindsActualGeneratedAnchorsIdempotently(int variant)
    {
        GameObject root = null;
        try
        {
            var room = variant < 3 ? MechanismExplorationPlan.BuildTunnel(166, variant) : variant < 6 ? MechanismExplorationPlan.BuildDuel(168, variant - 3) : MechanismExplorationPlan.BuildCavernDuel(168, variant - 6);
            root = AsciiLevelGenerator.GenerateFromTemplate(room.ascii, false, false);
            Assert.IsNotNull(root);
            if (room.duelVersion == 2)
            {
                int count = root.GetComponentsInChildren<Collider2D>(true).Length;
                ExplorationSceneBuilder.AddCavernPresentation(root, room);
                Assert.AreEqual(count, root.GetComponentsInChildren<Collider2D>(true).Length, "Presentation never adds gameplay colliders");
                Assert.IsTrue(root.GetComponentsInChildren<FakeWall>(true).All(w => w.ShowPublicWallCue), "Baseline and treatment both expose public wall cues");
                Physics2D.SyncTransforms();
                var solids = root.GetComponentsInChildren<BoxCollider2D>(true).Where(c => c.enabled && !c.isTrigger).ToArray();
                foreach (var point in room.routes.SelectMany(r => r.points))
                {
                    var foot = new Vector3(point.x, point.y - PhysicsMetrics.MARIO_COLLIDER_HEIGHT * 0.5f + PhysicsMetrics.MARIO_COLLIDER_OFFSET_Y - 0.05f, 0);
                    Assert.IsTrue(solids.Any(c => c.bounds.Contains(foot)), "Authored landing has no real support: " + point.x + "," + point.y);
                }
            }
            foreach (var prop in root.GetComponentsInChildren<ControllablePropBase>(true))
                if (prop.GetComponent<PossessionAnchor>() == null) prop.gameObject.AddComponent<PossessionAnchor>();
            ExplorationSceneBuilder.ConfigureTunnelNetwork(root, room);
            ExplorationSceneBuilder.ConfigureTunnelNetwork(root, room);
            var anchors = root.GetComponentsInChildren<PossessionAnchor>(true);
            int total = 0;
            foreach (var anchor in anchors)
            {
                total += anchor.connectedUnderlineNodes.Count;
                foreach (var next in anchor.connectedUnderlineNodes)
                {
                    Assert.Contains(anchor, next.connectedUnderlineNodes);
                    Assert.AreEqual(0.8f, next.underlineTransitTime);
                }
            }
            Assert.AreEqual(room.tunnelLinks.Length, total, "No duplicate edges or runtime-only placeholder links");
            var broken = room.tunnelLinks[0].from;
            broken.x += 1000;
            Assert.Throws<System.InvalidOperationException>(() => ExplorationSceneBuilder.ConfigureTunnelNetwork(root, room));
        }
        finally { if (root != null) Object.DestroyImmediate(root); }
    }

    [Test]
    public void TunnelInterceptUsesLinkedUsableExitsAndLocalDistanceBound()
    {
        var origin = new GameObject("TunnelOrigin"); var exit = new GameObject("TunnelExit");
        try
        {
            origin.transform.position = new Vector3(36000, 1, 0); exit.transform.position = new Vector3(36006, 1, 0);
            var originProp = origin.AddComponent<FakeWall>(); var exitProp = exit.AddComponent<FakeWall>();
            var from = origin.AddComponent<PossessionAnchor>(); var to = exit.AddComponent<PossessionAnchor>();
            // [AI防坑警告] EditMode下不要用SendMessage派发生命周期，会触发ShouldRunBehaviour断言。
            // Only initialize the actual component cache; this fixture does not test runtime Awake.
            var cache = typeof(PossessionAnchor).GetMethod("CacheControllableProp", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(cache);
            cache.Invoke(from, null); cache.Invoke(to, null);
            Assert.AreSame(originProp, from.ControllableProp);
            Assert.AreSame(exitProp, to.ControllableProp);
            Assert.IsTrue(to.CanBePossessed(), "Use the real prop's availability, not a fabricated cache value");
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindTunnelIntercept(null, new Vector2(36003, 1), new Vector2(8, 0)));
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindTunnelIntercept(from, new Vector2(36003, 1), new Vector2(8, 0)));
            from.connectedUnderlineNodes.Add(null);
            from.connectedUnderlineNodes.Add(from);
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindTunnelIntercept(from, new Vector2(36003, 1), new Vector2(8, 0)));
            from.connectedUnderlineNodes.Add(to);
            Assert.AreSame(to, ExplorationTrialObserver.GuidedBot.FindTunnelIntercept(from, new Vector2(36003, 1), new Vector2(8, 0)));
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindTunnelIntercept(from, new Vector2(36003, 1), new Vector2(-8, 0)), "Do not transfer to a worse predicted intercept");
            Assert.AreSame(to, ExplorationTrialObserver.GuidedBot.FindTunnelIntercept(from, new Vector2(36012, 1), Vector2.zero), "The local radius includes its boundary");
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindTunnelIntercept(from, new Vector2(36012.25f, 1), Vector2.zero));
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindTunnelIntercept(from, new Vector2(36100, 1), new Vector2(8, 0)));
            to.enabled = false;
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindTunnelIntercept(from, new Vector2(36003, 1), new Vector2(8, 0)));
            to.enabled = true;
            exit.SetActive(false);
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindTunnelIntercept(from, new Vector2(36003, 1), new Vector2(8, 0)));
            exit.SetActive(true);
            Assert.AreSame(to, ExplorationTrialObserver.GuidedBot.FindTunnelIntercept(from, new Vector2(36003, 1), new Vector2(8, 0)), "Restored exit must be selectable again");
            Object.DestroyImmediate(exitProp);
            cache.Invoke(to, null);
            Assert.IsNull(to.ControllableProp);
            Assert.IsFalse(to.CanBePossessed());
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindTunnelIntercept(from, new Vector2(36003, 1), new Vector2(8, 0)), "A linked anchor without a prop is not usable");
            from.connectedUnderlineNodes = null;
            Assert.IsNull(ExplorationTrialObserver.GuidedBot.FindTunnelIntercept(from, new Vector2(36003, 1), new Vector2(8, 0)));
        }
        finally { Object.DestroyImmediate(origin); Object.DestroyImmediate(exit); }
    }

    [Test]
    public void TunnelRegressionFailureLeavesAllGameplaySlotsUnverified()
    {
        var report = new StudioExplorationRunner.Report {
            toolRevision = "S166", seed = 166, scope = "TunnelDuel", controlMode = "Automated",
            scenarios = MechanismExplorationPlan.Create(166, MechanismExplorationPlan.Scope.TunnelDuel)
        };
        Assert.AreEqual("Restoring", StudioExplorationRunner.CompleteRegressionStage(report, 385, 1, false));
        report.status = "Blocked";
        Assert.AreEqual(18, StudioExplorationRunner.PlannedTrials(report));
        Assert.AreEqual(18, StudioExplorationRunner.UnverifiedSlots(report));
        StringAssert.Contains("未执行部分不算通过", StudioExplorationRunner.EvidenceVerdict(report));
        StringAssert.Contains("证据不足", StudioExplorationRunner.TunnelDesignSummary(report));
        StringAssert.Contains("Trials recorded: 0 / 18", StudioExplorationRunner.BuildSummary(report));
        Assert.IsEmpty(report.trials);
        Assert.IsFalse(report.confirmationPlanned);
        Assert.IsEmpty(report.confirmationScenarioIds);
        Assert.AreEqual("S166", report.toolRevision, "Reviewing a failed historical report must not relabel its evidence");
        Assert.AreEqual(385, report.regressionPassed);
        Assert.AreEqual(1, report.regressionFailed);
    }

    [Test]
    public void TunnelReportDoesNotRankFunOrBorrowHumanAndPartialRecords()
    {
        var room = MechanismExplorationPlan.BuildTunnel(166, 0);
        var report = new StudioExplorationRunner.Report { controlMode = "Automated" };
        report.scenarios.Add(room);
        Assert.AreEqual("地道博弈", StudioExplorationRunner.TestTrack(report));
        Assert.AreEqual(6, StudioExplorationRunner.UnverifiedSlots(report));
        var t = new MechanismExplorationPlan.Trial { scenarioId = room.id, profile = "Adaptive vs TunnelChaser",
            marioStrategy = "Adaptive", tricksterStrategy = "TunnelChaser", controlMode = "HumanMario",
            tunnelVersion = 1, tunnelEvidenceVersion = 1, seconds = 20, outcome = "Cleared", tunnelArrivals = 3,
            controlsAfterTunnel = 1, lootEvents = 1, escapeEvents = 1, lootAtSeconds = 10, escapeAtSeconds = 20 };
        report.trials.Add(t);
        Assert.AreEqual(6, StudioExplorationRunner.UnverifiedSlots(report));
        StringAssert.Contains("证据不足", StudioExplorationRunner.TunnelDesignSummary(report));
        t.controlMode = "Automated";
        Assert.AreEqual(5, StudioExplorationRunner.UnverifiedSlots(report));
        t.outcome = "TimedOut";
        StringAssert.Contains("返程未验证", StudioExplorationRunner.TunnelDesignSummary(report));
        report.controlMode = "HumanMario";
        StringAssert.Contains("不计入自动批次", StudioExplorationRunner.EvidenceVerdict(report));
        StringAssert.Contains("不作自动策略诊断", StudioExplorationRunner.TunnelDesignSummary(report));
    }

    [Test]
    public void PartialOrDuplicatedSchedulesCannotReceiveWholeBatchCandidateVerdict()
    {
        var report = new StudioExplorationRunner.Report {
            status = "Complete", scenarios = MechanismExplorationPlan.Create(154, MechanismExplorationPlan.Scope.Counterplay)
        };
        foreach (var room in report.scenarios)
        foreach (var matchup in MechanismExplorationPlan.Matchups(room))
            report.trials.Add(new MechanismExplorationPlan.Trial { scenarioId = room.id, marioStrategy = matchup.mario,
                tricksterStrategy = matchup.trickster, outcome = "Cleared", seconds = 10 });
        Assert.AreEqual(0, StudioExplorationRunner.UnverifiedSlots(report));
        report.trials.RemoveAt(0);
        Assert.AreEqual(1, StudioExplorationRunner.UnverifiedSlots(report));
        report.trials.Add(report.trials[0]);
        Assert.AreEqual(2, StudioExplorationRunner.UnverifiedSlots(report), "Equal row count does not mean equal matchup coverage");
        report.confirmationScenarioIds.Add(report.scenarios[0].id);
        Assert.AreEqual(5, StudioExplorationRunner.UnverifiedSlots(report));
        var smoke = new StudioExplorationRunner.Report { scenarios = MechanismExplorationPlan.Create(154, MechanismExplorationPlan.Scope.Smoke) };
        smoke.trials.Add(new MechanismExplorationPlan.Trial { scenarioId = smoke.scenarios[0].id,
            profile = "Cautious", outcome = "Cleared", seconds = 3 });
        Assert.AreEqual(17, StudioExplorationRunner.UnverifiedSlots(smoke));
        StringAssert.Contains("调度证据不完整", StudioExplorationRunner.EvidenceVerdict(smoke));
    }

    [Test]
    public void CounterplayPairsRejectWrongPlanDuplicatesMissingStartsAndNonFiniteReturnTimes()
    {
        var room = MechanismExplorationPlan.Create(154, MechanismExplorationPlan.Scope.Counterplay)[4];
        var report = new StudioExplorationRunner.Report(); report.scenarios.Add(room);
        var a = new MechanismExplorationPlan.Trial { scenarioId = room.id, marioStrategy = "Adaptive", tricksterStrategy = "Passive",
            counterplayVersion = 1, healthEvidenceVersion = 1, outcome = "Cleared", seconds = 8,
            lootAtSeconds = 2, escapeAtSeconds = 8, lootEvents = 1, escapeEvents = 1,
            startDelaySeconds = 0.6f, actualStartWaitSeconds = 0.6f };
        var b = new MechanismExplorationPlan.Trial { scenarioId = room.id, marioStrategy = "Adaptive", tricksterStrategy = "Chaser",
            counterplayVersion = 1, healthEvidenceVersion = 1, outcome = "Cleared", seconds = 12,
            lootAtSeconds = 3, escapeAtSeconds = 12, lootEvents = 1, escapeEvents = 1,
            startDelaySeconds = 0.6f, actualStartWaitSeconds = 0.6f };
        report.trials.Add(a); report.trials.Add(b);
        StringAssert.DoesNotContain("返程耗时差=", StudioExplorationRunner.CounterplayPairs(report));
        b.startTimingEvidenceVersion = 1; b.startWaitFrames = b.opponentWaitDecisionFrames = 30;
        StringAssert.Contains("返程耗时差=3.00s", StudioExplorationRunner.CounterplayPairs(report));
        report.trials.Add(b);
        StringAssert.DoesNotContain("返程耗时差=", StudioExplorationRunner.CounterplayPairs(report));
        report.trials.RemoveAt(2);
        a.startDelaySeconds = a.actualStartWaitSeconds = 0;
        StringAssert.DoesNotContain("返程耗时差=", StudioExplorationRunner.CounterplayPairs(report));
        a.startDelaySeconds = a.actualStartWaitSeconds = 0.6f;
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, 13f, -1f })
        {
            b.escapeAtSeconds = invalid;
            StringAssert.DoesNotContain("返程耗时差=", StudioExplorationRunner.CounterplayPairs(report));
        }
        b.escapeAtSeconds = 12;
        a.startTimingEvidenceVersion = 1; a.opponentWaitInputFrames = 1;
        StringAssert.DoesNotContain("返程耗时差=", StudioExplorationRunner.CounterplayPairs(report));
    }

    [Test]
    public void PassiveOnlyRoomDoesNotDemandActiveOpponentAndOldScanPolicyRemainsUnknown()
    {
        var room = MechanismExplorationPlan.Create(154, MechanismExplorationPlan.Scope.Counterplay)[0];
        var report = new StudioExplorationRunner.Report(); report.scenarios.Add(room);
        report.trials.Add(new MechanismExplorationPlan.Trial { scenarioId = room.id, marioStrategy = "Runner",
            tricksterStrategy = "Passive", outcome = "Cleared", seconds = 5 });
        string summary = StudioExplorationRunner.BuildSummary(report);
        StringAssert.Contains("零操控符合计划", summary);
        StringAssert.DoesNotContain("尚无操控受理证据", summary);
        Assert.IsNull(report.trials[0].scanPolicy);
    }

    [TestCase(0.6f)]
    [TestCase(1.2f)]
    public void StartWaitTicksPrepareOpponentWithoutRunningMarioDecisions(float wait)
    {
        var runnerObject = new GameObject("IndependentRunner"); var opponentObject = new GameObject("IndependentOpponent");
        try
        {
            runnerObject.transform.position = new Vector3(30000, 1, 0);
            opponentObject.transform.position = new Vector3(30010, 1, 0);
            var runner = runnerObject.AddComponent<MarioController>();
            opponentObject.AddComponent<TricksterController>();
            var bot = new ExplorationTrialObserver.GuidedBot(runner, new System.Collections.Generic.Dictionary<string, Transform[]>(), false, null, false) {
                StartDelayRemaining = wait, ReadPublicQueues = true
            };
            bot.SetDecisionSeed(154);
            for (int i = 0; i < 2; i++) bot.Tick(0.2f);
            Assert.AreEqual(2, bot.StartWaitFrames); Assert.AreEqual(2, bot.OpponentWaitDecisionFrames);
            Assert.AreEqual(0, bot.p1Horizontal); Assert.IsFalse(bot.p1JumpDown || bot.p1ScanDown);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.AreEqual(0f, typeof(HeuristicBotInputProvider).GetField("_randomScanTimer", flags).GetValue(bot));
            Assert.AreEqual(0f, typeof(HeuristicBotInputProvider).GetField("_marioReactionTimer", flags).GetValue(bot));
            Assert.AreEqual(0, bot.WaypointsReached);
            bot.InvalidateCache(); bot.Tick(0.1f);
            Assert.AreEqual(1, bot.OpponentDecisionTicks, "Invalidation must reacquire shared references during the wait");
        }
        finally { Object.DestroyImmediate(runnerObject); Object.DestroyImmediate(opponentObject); }
    }

    [Test]
    public void EvidenceScanRequiresLocalCueReadyCooldownAndClearLaneWithoutTargetOmniscience()
    {
        var actor = new GameObject("EvidenceRunner"); var target = new GameObject("EvidenceAnchor");
        var wall = new GameObject("EvidenceOccluder"); var service = new GameObject("EvidenceTracker");
        try
        {
            actor.transform.position = new Vector3(31000, 1, 0); target.transform.position = new Vector3(31003, 1, 0);
            wall.transform.position = new Vector3(31001.5f, 1, 0);
            var body = actor.AddComponent<BoxCollider2D>(); var scan = actor.AddComponent<ScanAbility>();
            var anchor = target.AddComponent<PossessionAnchor>();
            var tracker = service.AddComponent<MarioSuspicionTracker>();
            var bot = new HeuristicBotInputProvider { EvidenceDrivenScanning = true };
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(HeuristicBotInputProvider);
            type.GetField("_scanAbility", flags).SetValue(bot, scan);
            type.GetField("_suspicionTracker", flags).SetValue(bot, tracker);
            type.GetField("_marioCollider", flags).SetValue(bot, body);
            type.GetField("_solidMaskReady", flags).SetValue(bot, true);
            type.GetField("_solidMask", flags).SetValue(bot, (LayerMask)1);
            var method = type.GetMethod("HasActionableScanCue", flags);
            System.Func<bool> cue = () => (bool)method.Invoke(bot, new object[] { (Vector2)actor.transform.position });
            Physics2D.SyncTransforms();
            Assert.IsFalse(cue(), "An empty anchor must not consume a scan");
            tracker.GetOrCreateData(anchor).AddEvidence(2);
            Assert.IsTrue(cue(), "Known evidence is useful without reading whether the opponent is actually hidden there");
            typeof(ScanAbility).GetField("cooldownTimer", flags).SetValue(scan, 1f);
            Assert.IsFalse(cue()); scan.ResetCooldown();
            var occluder = wall.AddComponent<BoxCollider2D>(); occluder.size = new Vector2(0.2f, 3f);
            Physics2D.SyncTransforms(); Assert.IsFalse(cue());
            occluder.isTrigger = true; Physics2D.SyncTransforms(); Assert.IsTrue(cue());
            target.transform.position += Vector3.up * 4; Physics2D.SyncTransforms(); Assert.IsFalse(cue());
            target.transform.position = actor.transform.position + Vector3.right * (scan.ScanRadius + 1);
            Physics2D.SyncTransforms(); Assert.IsFalse(cue());
            bot.InvalidateCache(); Assert.IsFalse(cue());
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(target); Object.DestroyImmediate(wall); Object.DestroyImmediate(service); }
    }

    [Test]
    public void CounterplayPassiveAndStartDelayUseOnlyNeutralOrdinaryInputs()
    {
        var bot = new ExplorationTrialObserver.GuidedBot(null, new System.Collections.Generic.Dictionary<string, Transform[]>(), false, null, false) {
            PassiveOpponent = true, StartDelayRemaining = 0.6f,
            p1Horizontal = 1, p1JumpDown = true, p1ScanDown = true,
            p2Horizontal = 1, p2Vertical = 1, p2SwitchDir = 1,
            p2JumpHeld = true, p2JumpDown = true, p2DirectionDown = true, p2DisguiseDown = true, p2AbilityDown = true
        };
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var opponent = typeof(ExplorationTrialObserver.GuidedBot).GetMethod("UpdateTricksterBrain", flags);
        opponent.Invoke(bot, new object[] { 0.2f });
        Assert.AreEqual(0, bot.p2Horizontal); Assert.AreEqual(0, bot.p2Vertical); Assert.AreEqual(0, bot.p2SwitchDir);
        Assert.IsFalse(bot.p2JumpHeld || bot.p2JumpDown || bot.p2DirectionDown || bot.p2DisguiseDown || bot.p2AbilityDown);
        var runner = typeof(ExplorationTrialObserver.GuidedBot).GetMethod("UpdateMarioBrain", flags);
        for (int i = 0; i < 3; i++) runner.Invoke(bot, new object[] { 0.25f });
        Assert.AreEqual(0, bot.StartDelayRemaining);
        Assert.That(bot.StartWaitSeconds, Is.EqualTo(0.75f).Within(0.0001f), "Full-frame neutral input overshoot must be reported, not clamped away");
        Assert.AreEqual(0, bot.p1Horizontal); Assert.IsFalse(bot.p1JumpDown || bot.p1JumpHeld || bot.p1ScanDown);
    }

    [Test]
    public void CounterplayReturnPairUsesActualLootToEscapeNotTotalDuration()
    {
        var rooms = MechanismExplorationPlan.Create(154, MechanismExplorationPlan.Scope.Counterplay);
        var room = rooms[3];
        var report = new StudioExplorationRunner.Report(); report.scenarios.Add(room);
        var a = new MechanismExplorationPlan.Trial { scenarioId = room.id, marioStrategy = "Adaptive", tricksterStrategy = "Passive",
            counterplayVersion = 1, healthEvidenceVersion = 1, outcome = "Cleared", seconds = 8, lootAtSeconds = 2, escapeAtSeconds = 8,
            lootEvents = 1, escapeEvents = 1 };
        var b = new MechanismExplorationPlan.Trial { scenarioId = room.id, marioStrategy = "Adaptive", tricksterStrategy = "Chaser",
            counterplayVersion = 1, healthEvidenceVersion = 1, outcome = "Cleared", seconds = 12, lootAtSeconds = 3, escapeAtSeconds = 12,
            postLootTransfers = 2, postLootControls = 1, lootEvents = 1, escapeEvents = 1 };
        report.trials.Add(a); report.trials.Add(b);
        StringAssert.Contains("返程耗时差=3.00s", StudioExplorationRunner.CounterplayPairs(report));
        StringAssert.DoesNotContain("返程耗时差=4.00s", StudioExplorationRunner.CounterplayPairs(report));
        b.escapeAtSeconds = -1;
        StringAssert.DoesNotContain("返程耗时差=", StudioExplorationRunner.CounterplayPairs(report));
    }

    [Test]
    public void GuidedExplorerUsesOneRepresentativeAndRejectsOtherInstanceEvidence()
    {
        var a = new GameObject("FirstDeck"); var b = new GameObject("SecondDeck"); var checkpoint = new GameObject("CheckpointProbe");
        try
        {
            a.transform.position = new Vector3(1, 0, 0); b.transform.position = new Vector3(2, 0, 0);
            checkpoint.transform.position = new Vector3(3, 0, 0);
            var targets = new System.Collections.Generic.Dictionary<string, Transform[]> {
                { "-", new[] { b.transform, a.transform } }, { "S", new[] { checkpoint.transform } }
            };
            var bot = new ExplorationTrialObserver.GuidedBot(null, targets, true, null, false, 10);
            Assert.AreEqual(2, bot.ProbeVisits.TargetCount); Assert.AreEqual(5, bot.ProbeVisits.Budget);
            bot.ObserveProbe("-", b.transform, "Contact");
            Assert.AreEqual(0, bot.ProbeVisits.SatisfiedCount);
            bot.ObserveProbe("-", a.transform, "Contact"); bot.ProbeVisits.Tick(0.1f);
            Assert.AreEqual("S", bot.ProbeVisits.Current);
            var normal = new ExplorationTrialObserver.GuidedBot(null, targets, false, null, false);
            normal.ObserveProbe("-", a.transform, "Contact");
            Assert.AreEqual(0, normal.ProbeVisits.SatisfiedCount, "Non-exploration strategies must not acquire probe detours");
        }
        finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(checkpoint); }
    }

    [Test]
    public void ReportDoesNotInventVisitOrDamageEvidenceForOldTrials()
    {
        var trial = new MechanismExplorationPlan.Trial { scans = 1 };
        StringAssert.Contains("未记录", StudioExplorationRunner.ProbeSummary(trial));
        StringAssert.Contains("未记录", StudioExplorationRunner.HealthSummary(trial));
        trial.probeEvidenceVersion = 1; trial.probeTargets = 2; trial.probeTimedOut = 1;
        trial.probeBudgetExhausted = true; trial.probeElapsedSeconds = trial.probeBudgetSeconds = 8;
        StringAssert.Contains("满足结束条件=0", StudioExplorationRunner.ProbeSummary(trial));
        StringAssert.Contains("不算行为通过", StudioExplorationRunner.ProbeSummary(trial));
        trial.healthEvidenceVersion = 1; trial.runnerDamageEvents = 2; trial.runnerHealthLost = 3;
        StringAssert.Contains("实际扣血事件=2", StudioExplorationRunner.HealthSummary(trial));
        StringAssert.Contains("未归因", StudioExplorationRunner.HealthSummary(trial));
    }

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
