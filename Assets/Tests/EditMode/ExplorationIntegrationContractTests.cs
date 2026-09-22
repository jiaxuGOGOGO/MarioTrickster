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
    public void TunnelNetworkBindsActualGeneratedAnchorsIdempotently(int variant)
    {
        GameObject root = null;
        try
        {
            var room = variant < 3 ? MechanismExplorationPlan.BuildTunnel(166, variant) : MechanismExplorationPlan.BuildDuel(168, variant - 3);
            root = AsciiLevelGenerator.GenerateFromTemplate(room.ascii, false, false);
            Assert.IsNotNull(root);
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
