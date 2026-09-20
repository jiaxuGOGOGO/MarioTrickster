using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Bounded editor batch state machine; every trial gets a fresh scene, never ResetRound.</summary>
[InitializeOnLoad]
public static class StudioExplorationRunner
{
    private const string StateKey = "MarioTrickster.Exploration.State.v1";
    private static string LastDirectoryKey => StateKey + ".Last." + Hash128.Compute(Application.dataPath);
    [Serializable]
    private sealed class SceneBookmark
    {
        public string path;
        public bool loaded, active;
    }
    [Serializable]
    private sealed class State
    {
        public string phase = "Idle";
        public int step;
        public float seconds = 30;
        public bool cancel;
        public bool runInBackground;
        public string directory;
        public SceneBookmark[] original;
        public string error = "";
    }
    [Serializable]
    public sealed class Report
    {
        public int version = MechanismExplorationPlan.Version;
        public int seed;
        public string scope;
        public string toolRevision; // Set only when starting; old reports remain unlabelled.
        public string startedUtc;
        public string finishedUtc;
        public string unityVersion;
        public float fixedDeltaTime;
        public string physicsConfigJson;
        public string gameplayConfigJson;
        public string status = "Running";
        public string verdict = "尚无有效试玩证据";
        public string blockedReason = "";
        public bool confirmationPlanned;
        public List<string> confirmationScenarioIds = new List<string>();
        public string regressions = "Not requested";
        public int regressionPassed, regressionFailed;
        public string limitation = "布局种子可复现，不保证跨机器逐帧物理相同。两两共现/接触不等于交互正确或好玩。";
        public string[] unsupportedRegistry;
        public List<MechanismExplorationPlan.Scenario> scenarios = new List<MechanismExplorationPlan.Scenario>();
        public List<MechanismExplorationPlan.Trial> trials = new List<MechanismExplorationPlan.Trial>();
    }
    private static State state;
    private static Report report;
    private static ExplorationTrialObserver observer;
    private static double lastTick, phaseStarted, trialStarted;
    private static bool busy, writing;
    public static bool Active => state != null && state.phase != "Idle" && state.phase != "Complete" && state.phase != "Aborted" && state.phase != "RestoreFailed" && state.phase != "Blocked";
    public static string Phase => state?.phase ?? "Idle";
    public static string Error => state?.error ?? "";
    public static Report Latest => report;
    public static int Completed => report?.trials.Count(t => t.outcome != "Running" && t.outcome != "Building") ?? 0;
    public static int Total => PlannedTrials(report);
    public static int PlannedTrials(Report data) => data == null ? 0 :
        MechanismExplorationPlan.TrialCount(data.scenarios) + MechanismExplorationPlan.TrialCount(
            data.scenarios.Where(s => (data.confirmationScenarioIds ?? new List<string>()).Contains(s.id)));

    public static string TestTrack(Report data)
    {
        if (data == null || data.scenarios.Count == 0) return "无场景计划";
        if (data.scenarios.All(s => s.counterplayVersion >= 1)) return "反制专项";
        int experience = data.scenarios.Count(s => !string.IsNullOrEmpty(s.experience));
        return experience == 0 ? "机制回归" : experience == data.scenarios.Count ? "体验探索" : "混合批次";
    }

    public static string[] ExperienceIssues(Report data, MechanismExplorationPlan.Trial trial) =>
        MechanismExplorationPlan.ExperienceIssues(trial, data.scenarios.FirstOrDefault(s => s.id == trial.scenarioId));

    public static string CompleteRegressionStage(Report data, int passed, int failed, bool cancelled)
    {
        data.regressionPassed = passed;
        data.regressionFailed = failed;
        data.regressions = failed > 0 ? "Failures" : passed > 0 ? "Passed" : "No tests reported";
        if (cancelled) return "Restoring";
        if (failed > 0 || passed <= 0)
        {
            data.blockedReason = failed > 0
                ? $"全量回归失败 {failed} 项，已停止后续 AI 跑图。请打开本批报告目录查看 TestReport.txt，修复后重新开始。"
                : "全量回归没有返回通过记录，已停止后续 AI 跑图。请检查 Test Runner 和本批报告，不把未执行算通过。";
            return "Restoring";
        }
        return "Preparing";
    }

    public static string EvidenceVerdict(Report data)
    {
        if (!string.IsNullOrEmpty(data.blockedReason)) return "回归或基础故障阻塞，未执行部分不算通过";
        int played = data.trials.Count(t => t.HasGameplayEvidence);
        if (played == 0) return "尚无有效试玩证据（不是玩法通过）";
        if (data.regressionFailed > 0 || data.trials.Any(t => t.errors.Count > 0)) return "有回归或运行错误，先修复再评估玩法";
        if (data.trials.Any(t => t.HasGameplayEvidence && ExperienceIssues(data, t).Length > 0))
            return "体验证据有缺口：通关不代表上路/交手机会/换点成立";
        if (UnverifiedSlots(data) > 0) return "调度证据不完整：存在缺失、未试玩或重复槽位，不能整批验收";
        if (data.trials.Any(t => t.NeedsConfirmation)) return "有受阻或覆盖缺口，查看首轮与复测对照";
        return "已有真人试玩候选，不代表正确性或乐趣已验收";
    }
    public static int UnverifiedSlots(Report data)
    {
        int missing = 0;
        for (int attempt = 1; attempt <= 2; attempt++)
        foreach (var room in data.scenarios)
        {
            if (attempt == 2 && !(data.confirmationScenarioIds ?? new List<string>()).Contains(room.id)) continue;
            foreach (var matchup in MechanismExplorationPlan.Matchups(room))
            {
                var records = data.trials.Where(t => t.scenarioId == room.id &&
                    (attempt == 1 ? t.attempt <= 1 : t.attempt == 2) &&
                    (t.marioStrategy ?? t.profile) == matchup.mario && (t.tricksterStrategy ?? t.profile) == matchup.trickster).ToArray();
                if (records.Length != 1 || !records[0].HasGameplayEvidence) missing++;
            }
        }
        return missing;
    }

    public static string ProbeSummary(MechanismExplorationPlan.Trial trial) => trial.probeEvidenceVersion < 1
        ? "探针访问预算未记录（旧报告或非Explorer）；不补算完成。"
        : $"探针访问：{trial.probeTargets}个代表目标，满足结束条件={trial.probeSatisfied}，单目标到期={trial.probeTimedOut}，目标丢失={trial.probeMissing}；用时={trial.probeElapsedSeconds:F2}/{trial.probeBudgetSeconds:F2}s，总预算到期={trial.probeBudgetExhausted}。跳过/到期不算行为通过。";

    public static string HealthSummary(MechanismExplorationPlan.Trial trial) => trial.healthEvidenceVersion < 1
        ? "实际扣血未记录；接触次数不是伤害次数。"
        : $"Mario实际扣血事件={trial.runnerDamageEvents}，累计损失生命={trial.runnerHealthLost}；来自生命变化，未归因到具体机关。";

    public static string StartTimingSummary(MechanismExplorationPlan.Trial trial) => trial.startTimingEvidenceVersion < 1
        ? "独立启动证据未记录；旧S163延迟Chaser对照存在共享缓存耦合，不能按仅Mario延迟解释。"
        : $"起步等待输入帧={trial.startWaitFrames}，期间对手有效决策帧={trial.opponentWaitDecisionFrames}，非中立输入帧={trial.opponentWaitInputFrames}，正常融入开始={trial.opponentWaitPreparations}次。决策/输入不等于附身完成；Passive应保持中立。";

    public static string EncounterSummary(MechanismExplorationPlan.Trial trial) => trial.queueEvidenceVersion < 2
        ? "整次遭遇无伤未记录；旧报告不从无伤片段或总损血反推。"
        : $"遭遇开始={trial.queueEvidence.Sum(q => q.encounters)}，完整结束={trial.queueEvidence.Sum(q => q.completedEncounters)}，整次遭遇无伤={trial.queueEvidence.Sum(q => q.cleanEncounters)}，绕行未完成={trial.queueEvidence.Sum(q => q.bypassedEncounters)}。同层接近范围为身体扩展攻击边界外2单位；等待/同侧退回不重置全来源损血基线，完整穿越结束，返程另计。";

    public static string QueueSummary(MechanismExplorationPlan.Trial trial)
    {
        if (trial.queueEvidenceVersion < 1) return "公开队列状态/伤害来源未记录；旧报告不补算。";
        if (trial.queueEvidence.Count == 0) return "本局没有公开队列机关，不适用队列穿越验收。";
        return string.Join("\n", trial.queueEvidence.Select(q =>
            $"{q.source}: 局部可见提示采样={q.cueSamples}，状态变化={q.cueChanges}；等待输入={q.waits}次/{q.waitSeconds:F2}s；进入={q.entries}，完整同层穿越={q.crossings}，无新增损血穿越片段={q.cleanCrossings}（不等于整次遭遇无伤）；队列直接扣血={q.damageEvents}次/{q.healthLost}点，其中近期有可见提示={q.damageWithRecentCue}次。可见性是距离/遮挡代理，不证明人类读懂。")) + "\n" + EncounterSummary(trial);
    }

    public static string CounterplayPairs(Report data)
    {
        var sb = new StringBuilder();
        foreach (var room in data.scenarios.Where(s => s.counterplayVersion >= 1))
        {
            sb.AppendLine($"对照 {room.id}: 起步等待={room.startDelaySeconds:F1}s；只用首轮，不用确认局补齐或挑选最好结果。");
            var trials = data.trials.Where(t => t.scenarioId == room.id && t.attempt <= 1).ToArray();
            if (!room.lootEscape)
            {
                var rush = UniqueTrial(trials, "Runner", "Passive");
                foreach (string strategy in new[] { "Adaptive", "SafeRoute" })
                {
                    var other = UniqueTrial(trials, strategy, "Passive");
                    if (!ComparablePair(rush, other, room)) { sb.AppendLine(strategy + ": 配对未完成/有错误/基线污染，不计算改善。保留失败记录。"); continue; }
                    sb.AppendLine($"{strategy} - Runner: 总耗时差={other.seconds - rush.seconds:F2}s，实际损血差={other.runnerHealthLost - rush.runnerHealthLost}；{strategy}无新增损血穿越片段={other.queueEvidence.Sum(q => q.cleanCrossings)}（SafeRoute绕行不要求穿越）。{EncounterSummary(other)}");
                }
            }
            else foreach (string strategy in new[] { "Adaptive", "SafeRoute" })
            {
                var passive = UniqueTrial(trials, strategy, "Passive");
                var chaser = UniqueTrial(trials, strategy, "Chaser");
                if (chaser != null && !MechanismExplorationPlan.IndependentStartObserved(chaser))
                { sb.AppendLine(strategy + ": 独立启动证据不足；旧S163延迟Chaser存在共享缓存耦合，不计算纯单边延迟返程差。保留原始结果。"); continue; }
                if (!ComparablePair(passive, chaser, room) || passive.lootEvents == 0 || passive.escapeEvents == 0 ||
                    chaser.lootEvents == 0 || chaser.escapeEvents == 0 || passive.lootAtSeconds < 0 || chaser.lootAtSeconds < 0 ||
                    !ValidReturnTimes(passive) || !ValidReturnTimes(chaser))
                { sb.AppendLine(strategy + ": 返程配对证据不全，不从去程/未撤离推断追击有效。" ); continue; }
                float delta = chaser.escapeAtSeconds - chaser.lootAtSeconds - (passive.escapeAtSeconds - passive.lootAtSeconds);
                sb.AppendLine($"{strategy} Chaser - Passive: 返程耗时差={delta:F2}s；拿宝后换点={chaser.postLootTransfers}，操控受理={chaser.postLootControls}。时间差是相关对照，不把换点次数当作压迫成功。" );
            }
        }
        if (sb.Length > 0) sb.AppendLine("时序只覆盖0/0.6/1.2秒普通起步等待，同一房间模板，不是自主学习或独立地图穷举；紧张感/自然度/重玩意愿仍未自动验证。");
        return sb.ToString();
    }
    private static MechanismExplorationPlan.Trial UniqueTrial(MechanismExplorationPlan.Trial[] trials, string mario, string trickster)
    {
        var matches = trials.Where(t => t.marioStrategy == mario && t.tricksterStrategy == trickster).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
    private static bool ValidReturnTimes(MechanismExplorationPlan.Trial t) =>
        MechanismExplorationPlan.IsFinite(t.lootAtSeconds) && MechanismExplorationPlan.IsFinite(t.escapeAtSeconds) &&
        t.lootAtSeconds >= 0f && t.escapeAtSeconds >= t.lootAtSeconds && t.escapeAtSeconds <= t.seconds;

    private static bool ComparablePair(MechanismExplorationPlan.Trial a, MechanismExplorationPlan.Trial b, MechanismExplorationPlan.Scenario room) =>
        a != null && b != null && a.counterplayVersion >= 1 && b.counterplayVersion >= 1 &&
        a.scenarioId == room.id && b.scenarioId == room.id && a.counterplayVersion == b.counterplayVersion &&
        (a.marioStrategy != b.marioStrategy || a.scanPolicy == b.scanPolicy) &&
        Math.Abs(a.startDelaySeconds - room.startDelaySeconds) < 0.001f && Math.Abs(b.startDelaySeconds - room.startDelaySeconds) < 0.001f &&
        a.healthEvidenceVersion >= 1 && b.healthEvidenceVersion >= 1 && a.HasGameplayEvidence && b.HasGameplayEvidence &&
        Math.Abs(a.actualStartWaitSeconds - a.startDelaySeconds) < 0.05f && Math.Abs(b.actualStartWaitSeconds - b.startDelaySeconds) < 0.05f &&
        a.outcome == "Cleared" && b.outcome == "Cleared" &&
        a.errors.Count == 0 && b.errors.Count == 0 &&
        MechanismExplorationPlan.PassiveControlClean(a) && MechanismExplorationPlan.PassiveControlClean(b);

    public static string ReportDirectory => state?.directory ?? "";
    public static string LiveIntent => observer?.Intent ?? "";

    static StudioExplorationRunner()
    {
        try
        {
            string saved = SessionState.GetString(StateKey, "");
            state = string.IsNullOrEmpty(saved) ? new State() : JsonUtility.FromJson<State>(saved) ?? new State();
            if (string.IsNullOrEmpty(state.phase)) state.phase = "Idle";
            bool historical = string.IsNullOrEmpty(saved);
            if (historical) state.directory = EditorPrefs.GetString(LastDirectoryKey, "");
            if (!string.IsNullOrEmpty(state.directory) && IsReportPath(state.directory) && File.Exists(Path.Combine(state.directory, "report.json")))
                report = JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(state.directory, "report.json")));
            if (historical && report != null)
            {
                state.phase = report.status == "Complete" ? "Complete" : report.status == "Blocked" ? "Blocked" : "Aborted";
                if (report.status == "Running") report.status = "Interrupted (editor closed)";
            }
            if (Active && report == null) { state.error = "批次状态丢失；请检查输出目录。"; state.phase = "RestoreFailed"; }
        }
        catch (Exception ex) { state = new State { error = ex.Message }; }
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += OnPlayMode;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        EditorApplication.quitting += BeforeQuit;
        Application.logMessageReceived += OnStartupLog;
        phaseStarted = lastTick = EditorApplication.timeSinceStartup;
    }

    public static void Start(int seed, MechanismExplorationPlan.Scope scope, float seconds,
        MechanismExplorationPlan.Scenario replay = null, bool withRegressions = true)
    {
        if (Active || !LevelStudioPlaySession.CanStart() || TestReportRunner.IsRunning) return;
        if (!SaveSourceScenes()) return;
        var scenarios = replay == null ? MechanismExplorationPlan.Create(seed, scope) : new List<MechanismExplorationPlan.Scenario> { replay };
        state = new State { phase = withRegressions && replay == null ? "RegressionQueued" : "Preparing", seconds = Mathf.Clamp(seconds, 10, 120), original = EditorSceneManager.GetSceneManagerSetup().Select(s => new SceneBookmark { path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray(),
            runInBackground = Application.runInBackground,
            directory = Path.Combine(OutputRoot, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8)) };
        report = new Report { toolRevision = "S165", seed = seed, scope = replay == null ? scope.ToString() : "Replay", startedUtc = DateTime.UtcNow.ToString("O"),
            unityVersion = Application.unityVersion, fixedDeltaTime = Time.fixedDeltaTime, scenarios = scenarios,
            physicsConfigJson = ConfigJson("PhysicsConfig"), gameplayConfigJson = ConfigJson("GameplayLoopConfig"),
            unsupportedRegistry = MechanismExplorationPlan.MissingFromCatalog(AsciiElementRegistry.GetDefault().GetAllRegisteredChars()) };
        try
        {
            Directory.CreateDirectory(state.directory);
            foreach (var scenario in report.scenarios) File.WriteAllText(Path.Combine(state.directory, scenario.id + ".txt"), scenario.ascii);
            Persist();
            EditorPrefs.SetString(LastDirectoryKey, state.directory);
            LevelBrushTool.Deactivate();
        }
        catch (Exception ex) { state.phase = "Aborted"; state.error = "无法保存测试批次：" + ex.Message; SaveState(); }
    }

    public static bool SaveSourceScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            // A declined save is cancellation, never permission to discard the user's work.
            if (scene.isDirty)
            {
                EditorUtility.DisplayDialog("请先保存场景", "为保护原场景，自动测试不会丢弃未保存的修改。请保存后再开始。", "知道了");
                return false;
            }
            if (string.IsNullOrEmpty(scene.path) && !EditorSceneManager.SaveScene(scene)) return false;
        }
        return true;
    }

    public static void Cancel()
    {
        if (!Active) return;
        state.cancel = true;
        if (state.phase == "Regressions") { SaveState(); return; } // let Unity Test Runner safely finish its current suite
        if (observer != null) observer.Finish("Cancelled", "用户停止测试，未完成部分仍列为未覆盖。");
        if (state.phase == "Entering" || state.phase == "Booting" || state.phase == "Running" || EditorApplication.isPlaying)
        {
            SetPhase("Exiting");
            EditorApplication.isPlaying = false;
        }
        else SetPhase("Restoring");
        Persist();
    }

    private static void Update()
    {
        if (!Active || busy) return;
        double now = EditorApplication.timeSinceStartup;
        float dt = (float)Math.Min(0.25, Math.Max(0, now - lastTick)); lastTick = now;
        busy = true;
        try
        {
            if (EditorApplication.isCompiling && state.phase != "Running") return;
            if (state.phase == "RegressionQueued" && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                report.regressions = "Running"; SetPhase("Regressions"); Persist();
                TestReportRunner.RunAllTestsUnattended();
            }
            else if (state.phase == "Regressions")
            {
                if (!TestReportRunner.IsRunning && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    string nextPhase = CompleteRegressionStage(report, TestReportRunner.LastPassed, TestReportRunner.LastFailed, state.cancel);
                    if (!string.IsNullOrEmpty(report.blockedReason)) state.error = report.blockedReason;
                    if (File.Exists(TestReportRunner.LastReportFile)) File.Copy(TestReportRunner.LastReportFile, Path.Combine(state.directory, "TestReport.txt"), true);
                    SetPhase(nextPhase); Persist();
                }
                else if (now - phaseStarted > 1200) state.error = "Unity 回归超过 20 分钟，请查看 Test Runner；可请求停止后等待套件退出。";
            }
            else if (state.phase == "Preparing" && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (state.cancel || !string.IsNullOrEmpty(report.blockedReason)) { SetPhase("Restoring"); return; }
                if (state.step >= Total)
                {
                    if (!report.confirmationPlanned)
                    {
                        report.confirmationPlanned = true;
                        report.confirmationScenarioIds = MechanismExplorationPlan.SelectConfirmationScenes(report.trials).ToList();
                        Persist();
                    }
                    if (state.step >= Total) { SetPhase("Restoring"); return; }
                }
                PrepareTrial();
            }
            else if (state.phase == "Booting" && EditorApplication.isPlaying)
            {
                if (CurrentTrial != null && CurrentTrial.errors.Count > 0) { EndTrial("StartupFailed", "启动时有错误，见报告堆栈。"); return; }
                if (GameManager.Instance != null)
                {
                    observer = new ExplorationTrialObserver(CurrentScenario, CurrentTrial, state.seconds);
                    Application.runInBackground = true;
                    Time.timeScale = 1f;
                    trialStarted = now;
                    SetPhase("Running");
                }
                else if (now - phaseStarted > 20) EndTrial("StartupFailed", "没有找到运行时 GameManager。");
            }
            else if (state.phase == "Running")
            {
                if (observer == null) { EndTrial("Interrupted", "代码重载中断了观察器，请复测此案例。"); return; }
                if (!Mathf.Approximately(Time.timeScale, 1f)) observer.Finish("Interrupted", "时间倍率被改变；本轮基准无效。");
                if (!EditorApplication.isPaused) observer.Tick(dt, state.seconds);
                if (now - trialStarted > state.seconds * 4 + 30) observer.Finish("WallTimeout", "真实时间预算耗尽（包括编辑器暂停），请检查卡死或性能。");
                if (observer.Finished) { Persist(); SetPhase("Exiting"); EditorApplication.isPlaying = false; }
            }
            else if (state.phase == "Entering" && now - phaseStarted > 90)
            { state.cancel = true; EndTrial("StartupFailed", "进入 Play 超时；批次停止。"); }
            else if (state.phase == "Exiting")
            {
                if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
                else if (!EditorApplication.isPlayingOrWillChangePlaymode) AdvanceAfterExit();
            }
            else if (state.phase == "Restoring" && !EditorApplication.isPlayingOrWillChangePlaymode) Restore();
        }
        catch (Exception ex)
        {
            state.error = ex.ToString(); state.cancel = true;
            if (state.phase == "Restoring" || state.phase == "Complete" || state.phase == "Aborted" || state.phase == "RestoreFailed" || state.phase == "Blocked")
            { state.phase = "RestoreFailed"; SaveState(); return; } // never loop forever on disk/restore failure
            EndTrial("InfrastructureError", "测试工具错误，停止批次并恢复原场景。" + ex.Message);
        }
        finally { busy = false; }
    }

    private static MechanismExplorationPlan.Slot CurrentSlot => MechanismExplorationPlan.TrialAt(report.scenarios, report.confirmationScenarioIds, state.step);
    private static MechanismExplorationPlan.Scenario CurrentScenario => CurrentSlot.scenario;
    private static MechanismExplorationPlan.Trial CurrentTrial => report.trials.LastOrDefault();
    private static void PrepareTrial()
    {
        // Never compare same-seed trials after the user (or a test) changes the baseline.
        if (!Mathf.Approximately(Time.fixedDeltaTime, report.fixedDeltaTime) ||
            ConfigJson("PhysicsConfig") != report.physicsConfigJson ||
            ConfigJson("GameplayLoopConfig") != report.gameplayConfigJson)
        {
            report.blockedReason = "批次期间物理或玩法配置已改变，停止以免混合不同基线。保留你的配置；重新开始会记录新基线。";
            state.error = report.blockedReason;
            SetPhase("Restoring"); Persist(); return;
        }
        var slot = CurrentSlot;
        var scenario = slot.scenario;
        var trial = new MechanismExplorationPlan.Trial { scenarioId = scenario.id,
            profile = slot.matchup.Id, marioStrategy = slot.matchup.mario, tricksterStrategy = slot.matchup.trickster,
            attempt = slot.attempt, outcome = "Building" };
        report.trials.Add(trial);
        trial.validation = ExplorationSceneBuilder.Validate(scenario, out bool invalid);
        if (invalid)
        {
            trial.outcome = "InvalidTemplate"; trial.nextAction = "源码结构不合法，未执行物理试玩。";
            state.step++; Persist(); return;
        }
        try { ExplorationSceneBuilder.Build(scenario, state.seconds, true); }
        catch (Exception ex)
        {
            trial.outcome = "BuildFailed"; trial.errors.Add(ex.ToString());
            trial.nextAction = "检查生成或机制依赖配置。"; RecordFeedback(); state.step++; Persist(); return;
        }
        SetPhase("Entering"); Persist();
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayMode(PlayModeStateChange change)
    {
        if (!Active || state.phase == "Regressions") return;
        if (change == PlayModeStateChange.EnteredPlayMode && state.phase == "Entering") SetPhase("Booting");
        else if (change == PlayModeStateChange.ExitingPlayMode)
        {
            if (state.phase != "Exiting")
            {
                state.cancel = true;
                observer?.Finish("Interrupted", "Play 被手动停止或外部工具中断。");
                if (CurrentTrial != null && CurrentTrial.outcome == "Building") CurrentTrial.outcome = "Interrupted";
                SetPhase("Exiting");
            }
            DisposeObserver(); Persist();
        }
        else if (change == PlayModeStateChange.EnteredEditMode && state.phase == "Exiting") AdvanceAfterExit();
    }

    private static void AdvanceAfterExit()
    {
        DisposeObserver();
        if (CurrentTrial != null && (CurrentTrial.outcome == "Building" || CurrentTrial.outcome == "Running"))
        { CurrentTrial.outcome = state.cancel ? "Cancelled" : "Interrupted"; CurrentTrial.nextAction = "没有完整观察结果，请复测。"; }
        state.step++;
        RecordFeedback();
        SetPhase(state.cancel || !string.IsNullOrEmpty(report.blockedReason) ? "Restoring" : "Preparing");
        Persist();
    }
    private static void RecordFeedback()
    {
        if (CurrentTrial != null && CurrentTrial.attempt == 2)
        {
            var baseline = report.trials.FirstOrDefault(t => t.attempt <= 1 &&
                t.scenarioId == CurrentTrial.scenarioId && t.profile == CurrentTrial.profile);
            CurrentTrial.comparison = MechanismExplorationPlan.CompareConfirmation(baseline, CurrentTrial);
        }
        if (MechanismExplorationPlan.RepeatedInfrastructureFailure(report.trials))
        {
            report.blockedReason = "连续两局出现相同基础故障，已停止重复空跑。修复后再开始；剩余计划仍未验证。\n" + CurrentTrial.InfrastructureKey;
            state.error = report.blockedReason;
        }
    }

    private static void EndTrial(string outcome, string reason)
    {
        observer?.Finish(outcome, reason);
        if (CurrentTrial != null && (CurrentTrial.outcome == "Running" || CurrentTrial.outcome == "Building"))
        { CurrentTrial.outcome = outcome; CurrentTrial.nextAction = reason; }
        SetPhase(EditorApplication.isPlayingOrWillChangePlaymode ? "Exiting" : "Restoring");
        EditorApplication.isPlaying = false;
        try { Persist(); } catch (Exception ex) { state.error = ex.Message; SaveState(); }
    }

    private static void Restore()
    {
        DisposeObserver();
        Time.timeScale = 1f;
        Application.runInBackground = state.runInBackground;
        try
        {
            if (state.original != null && state.original.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(state.original.Select(s => new SceneSetup { path = s.path, isLoaded = s.loaded, isActive = s.active }).ToArray());
            SetPhase(state.cancel ? "Aborted" : !string.IsNullOrEmpty(report.blockedReason) ? "Blocked" : "Complete");
        }
        catch (Exception ex) { state.error = "原场景恢复失败：" + ex.Message; SetPhase("RestoreFailed"); }
        report.status = state.phase;
        report.finishedUtc = DateTime.UtcNow.ToString("O");
        Persist();
    }

    private static void OnStartupLog(string message, string stack, LogType type)
    {
        if (!Active || (state.phase != "Entering" && state.phase != "Booting") || CurrentTrial == null) return;
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (CurrentTrial.errors.Count < 20) { string log = message + "\n" + stack; CurrentTrial.errors.Add(log.Substring(0, Math.Min(4000, log.Length))); }
    }

    private static void BeforeReload()
    {
        // Normal Enter/Exit domain reload has no live observer. Script recompilation during a trial is an interruption.
        if (!Active || observer == null) return;
        state.cancel = true;
        observer.Finish("Interrupted", "运行时脚本重编译，停止批次以避免错误计分。");
        DisposeObserver(); SetPhase("Exiting"); Persist();
    }
    private static void BeforeQuit()
    {
        if (!Active) return;
        state.cancel = true; observer?.Finish("Interrupted", "编辑器退出，批次未完成。");
        DisposeObserver(); report.status = "Interrupted"; Persist();
    }
    private static void DisposeObserver() { observer?.Dispose(); observer = null; }
    private static void SetPhase(string phase) { state.phase = phase; phaseStarted = EditorApplication.timeSinceStartup; SaveState(); }
    private static void SaveState() => SessionState.SetString(StateKey, JsonUtility.ToJson(state));
    private static string OutputRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "reports", "ai_exploration"));
    private static bool IsReportPath(string path) => Path.GetFullPath(path).StartsWith(OutputRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    private static string ConfigJson(string name)
    { var config = Resources.Load<ScriptableObject>(name); return config == null ? "defaults" : JsonUtility.ToJson(config); }

    private static void Persist()
    {
        SaveState();
        if (report == null || string.IsNullOrEmpty(state.directory) || writing) return;
        if (!IsReportPath(state.directory)) throw new InvalidOperationException("Unsafe report path");
        writing = true;
        try
        {
            report.verdict = EvidenceVerdict(report);
            string jsonPath = Path.Combine(state.directory, "report.json");
            string temp = jsonPath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(report, true));
            if (File.Exists(jsonPath)) File.Replace(temp, jsonPath, jsonPath + ".bak");
            else File.Move(temp, jsonPath);
            File.WriteAllText(Path.Combine(state.directory, "summary.txt"), BuildSummary(report));
        }
        finally { writing = false; }
    }

    public static string BuildSummary(Report data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AI MECHANISM EXPLORATION — evidence, not a fun score");
        sb.AppendLine($"测试路径: {TestTrack(data)}; tool revision: {data.toolRevision ?? "未记录"}");
        if (TestTrack(data) == "机制回归") sb.AppendLine("本批没有运行三类体验房/九种独立策略。路线区域字段为空不代表路线观察失败；请另运行体验探索。");
        sb.AppendLine($"Status: {data.status}; seed: {data.seed}; Unity: {data.unityVersion}; scope: {data.scope}");
        sb.AppendLine($"Trials recorded: {data.trials.Count} / {PlannedTrials(data)}");
        sb.AppendLine("Evidence verdict: " + EvidenceVerdict(data));
        sb.AppendLine($"缺失/未试玩/重复的计划槽位: {UnverifiedSlots(data)}（按场景、双方策略及首轮/确认逐项核对）");
        sb.AppendLine($"有效试玩记录: {data.trials.Count(t => t.HasGameplayEvidence)}; 首轮={data.trials.Count(t => t.attempt <= 1 && t.HasGameplayEvidence)}; 复测={data.trials.Count(t => t.attempt == 2 && t.HasGameplayEvidence)}");
        sb.AppendLine("有限反馈：最多追加 6 张问题图 × 原策略搭配 × 1 轮；机制回归每图3局，体验探索每图9局，反制专项每图3或4局。同种子同配置，不自动改难度或删除失败记录。");
        sb.AppendLine("当前代码复测策略（不回写旧批次调度）：无进展/超时 → 角色死亡 → 仅观察不足；最多6图，不递归。Explorer按机制选一个代表，单目标最多4秒，总预算最多8秒且不超过单局一半；到期返回普通目标导航，不证明剩余机制通过。");
        sb.AppendLine("体验房提供作者标注路线供普通按键导航，不是自主学习或未知地图寻路。旧报告按保存的ASCII重建，当前代码复测不是跨版本相同条件。");
        sb.AppendLine("路线进入/后摇穿越是位置采样证据，不等于整条路线走完、反制成功或好玩；换路请求与实际换路分开统计。");
        foreach (var scenario in data.scenarios)
            sb.AppendLine($"Room {scenario.id}: {(string.IsNullOrEmpty(scenario.experience) ? "MechanismProbe" : scenario.experience)} — {scenario.intention}");
        sb.AppendLine("完整路线 = 按顺序在所有作者路点落地；Out/Return分开记录。仍不证明反制因果或乐趣。安全绕行不强求机关接触。");
        foreach (var room in data.scenarios.Where(s => !string.IsNullOrEmpty(s.experience)))
        {
            var first = data.trials.Where(t => t.scenarioId == room.id && t.attempt <= 1 && t.HasGameplayEvidence).ToArray();
            sb.AppendLine($"Experience first-pass {room.id}: trials={first.Length}, gaps={first.Count(t => ExperienceIssues(data, t).Length > 0)}, controls accepted={first.Sum(t => t.controlAccepted)}, successful anchor changes={first.Sum(t => t.possessionTransfers)}");
            if (first.Length > 0 && MechanismExplorationPlan.Matchups(room).All(m => m.trickster == "Passive"))
                sb.AppendLine("  静止对手基线：零操控符合计划；只检验公开队列和路线选择，不检验主动对抗。");
            else if (first.Length > 0 && first.Sum(t => t.controlAccepted) == 0)
                sb.AppendLine("  尚无操控受理证据（旧报告可能未记录）；安全绕行允许零操控，不可据此宣称追击压迫成立。");
        }
        foreach (var strategy in data.trials.Where(t => t.HasGameplayEvidence && t.attempt <= 1).GroupBy(t => t.marioStrategy ?? t.profile))
            sb.AppendLine($"Runner {strategy.Key}: trials={strategy.Count()}, observed routes={string.Join(",", strategy.SelectMany(t => t.routesUsed ?? new List<string>()).Distinct())}, retreats={strategy.Sum(t => t.telegraphRetreats)}, recovery crossings={strategy.Sum(t => t.recoveryCrossings)}");
        if (!string.IsNullOrEmpty(data.blockedReason)) sb.AppendLine(data.blockedReason);
        foreach (var group in data.trials.Where(t => t.InfrastructureKey.Length > 0).GroupBy(t => t.InfrastructureKey))
            sb.AppendLine($"基础故障 ×{group.Count()}: {group.Key}");
        sb.AppendLine(data.limitation);
        sb.AppendLine(CounterplayPairs(data));
        sb.AppendLine($"Unity regressions: {data.regressions}; passed={data.regressionPassed}, failed={data.regressionFailed}");
        sb.AppendLine("Complete 表示调度结束，不表示每局通过，更不表示全部机制已验证。");
        sb.AppendLine("未规划的 Registry 机制: " + string.Join(", ", data.unsupportedRegistry ?? Array.Empty<string>()));
        var attempted = new HashSet<string>(data.trials.Where(t => t.outcome != "Building" && t.outcome != "Running").Select(t => t.scenarioId));
        sb.AppendLine("尚无完成记录的场景: " + string.Join(", ", data.scenarios.Where(s => !attempted.Contains(s.id)).Select(s => s.id)));
        sb.AppendLine("没有有效试玩证据的场景: " + string.Join(", ", data.scenarios
            .Where(s => !data.trials.Any(t => t.scenarioId == s.id && t.HasGameplayEvidence)).Select(s => s.id)));
        sb.AppendLine("本批未规划的19机制目录项: " + string.Join(", ", MechanismExplorationPlan.Catalog.Where(c => !data.scenarios.Any(s => (s.mechanisms ?? "").Contains(c))).Select(c => c.ToString())));
        sb.AppendLine("最低探针观察不等于行为通过：被动/自主机制不要求操控激活；接触后仍须验证下列行为、反制和恢复。确认局是相关复测，不计独立样本。");
        foreach (char mechanism in MechanismExplorationPlan.Catalog)
        {
            sb.AppendLine($"{mechanism} 专项验收: {MechanismExplorationPlan.BehaviorRequirement(mechanism.ToString())}");
            for (int pass = 1; pass <= 2; pass++)
            {
                var evidence = data.trials.Where(t => pass == 1 ? t.attempt <= 1 : t.attempt == 2)
                    .SelectMany(t => t.coverage).Where(e => e.mechanism == mechanism.ToString()).ToArray();
                sb.AppendLine($"{mechanism} {(pass == 1 ? "first-pass" : "confirmation")}: built={evidence.Sum(e => e.built)}, approachedTrials={evidence.Count(e => e.approached)}, contacts={evidence.Sum(e => e.contacts)} (runner={evidence.Sum(e => e.runnerContacts)}, trickster={evidence.Sum(e => e.tricksterContacts)}), activations={evidence.Sum(e => e.activations)} (legacy mixed events), controlsAccepted={evidence.Sum(e => e.controlsAccepted)}, runnerEffects={evidence.Sum(e => e.runnerEffects)}, observationGaps={evidence.Count(e => e.ObservationGap)}");
            }
        }
        foreach (var trial in data.trials)
        {
            sb.AppendLine($"\n{trial.scenarioId} / {trial.profile} / attempt={trial.attempt}: {trial.outcome}, {trial.seconds:F1}s, end=({trial.endX:F1},{trial.endY:F1}), farthestX={trial.farthestX:F1}");
            sb.AppendLine($"strategies={trial.marioStrategy} / {trial.tricksterStrategy}; objective={trial.objectivePhase}; observed routes={string.Join(",", trial.routesUsed ?? new List<string>())}");
            sb.AppendLine($"route switch requests={trial.routeSwitchRequests}, physical transitions={trial.routeTransitions}, waypoint visits={trial.waypointsReached}, recovery attempts={trial.recoveryAttempts}, telegraph retreats={trial.telegraphRetreats}, recovery crossings={trial.recoveryCrossings}, anchor transfers={trial.possessionTransfers}");
            sb.AppendLine($"bounce landing requests={trial.bounceLandingAttempts}, runner bounce launches={trial.runnerBounceLaunches} (launch event only; contact/control acceptance is not a launch)");
            sb.AppendLine($"pair exercised={trial.PairExercised}; human-play candidate={trial.CandidateForHumanPlay}");
            sb.AppendLine($"completed authored routes={string.Join(",", trial.completedRoutes ?? new List<string>())}; armed nearby seconds={trial.armedNearbySeconds:F2}; control accepted={trial.controlAccepted}; anchor switch requests={trial.anchorSwitchRequests}");
            foreach (var gap in ExperienceIssues(data, trial)) sb.AppendLine("体验缺口: " + gap);
            sb.AppendLine(ProbeSummary(trial));
            sb.AppendLine(HealthSummary(trial));
            sb.AppendLine(StartTimingSummary(trial));
            sb.AppendLine(QueueSummary(trial));
            if (trial.counterplayVersion >= 1)
                sb.AppendLine($"反制专项版本={trial.counterplayVersion}；计划/实际起步等待={trial.startDelaySeconds:F2}/{trial.actualStartWaitSeconds:F2}s；拿宝/撤离时间={trial.lootAtSeconds:F2}/{trial.escapeAtSeconds:F2}s，拿宝后换点={trial.postLootTransfers}，操控受理={trial.postLootControls}。起步等待不是反应耗时。");
            sb.AppendLine(trial.nextAction);
            sb.AppendLine("结束原因: " + trial.endReason);
            if (!string.IsNullOrEmpty(trial.comparison)) sb.AppendLine(trial.comparison);
            foreach (var item in trial.timeline) sb.AppendLine("  event: " + item);
            sb.AppendLine($"scan={trial.scans}, possession={trial.possessions}, combo={trial.comboEvents}, heat={trial.heatEvents}, loot={trial.lootEvents}, escape={trial.escapeEvents}, crisis={trial.crises}, reveal={trial.reveals}");
            sb.AppendLine("扫描策略: " + (trial.scanPolicy ?? "旧报告未记录，不按当前策略重标") +
                "；miss仅表示未发现伪装，不排除封路预警缩短，效果须另验。");
            sb.AppendLine(trial.scanEvidenceVersion >= 1
                ? $"scan hits={trial.scanHits}, misses={trial.scanMisses}; result callbacks only. Hit is a detected disguise, not proof of damage prevented."
                : "扫描结果未记录；不可从施放数或reveals总线补算命中。请在新批次采集。");
            sb.AppendLine($"route degraded={trial.routeDegradations}, recovered={trial.routeRecoveries}, guard={trial.routeBlocks}. Zero means not observed, NOT passed.");
            sb.AppendLine("Static hints: " + trial.validation);
            foreach (var e in trial.coverage) sb.AppendLine($"  {e.mechanism}: {e.Status}; observed phases={string.Join(",", e.phases)}" +
                (e.mechanism == "P" ? $"; hammer contacts={(e.movingPartEvidenceVersion >= 1 ? e.runnerMovingPartContacts.ToString() : "unrecorded")}; root built={e.built} (relay excluded)" : ""));
            foreach (var error in trial.errors) sb.AppendLine(error);
        }
        return sb.ToString();
    }
}
