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
        (data.scenarios.Count + (data.confirmationScenarioIds?.Count ?? 0)) * MechanismExplorationPlan.Profiles.Length;

    public static string EvidenceVerdict(Report data)
    {
        if (!string.IsNullOrEmpty(data.blockedReason)) return "基础故障阻塞，未执行部分不算通过";
        int played = data.trials.Count(t => t.HasGameplayEvidence);
        if (played == 0) return "尚无有效试玩证据（不是玩法通过）";
        if (data.regressionFailed > 0 || data.trials.Any(t => t.errors.Count > 0)) return "有回归或运行错误，先修复再评估玩法";
        if (data.trials.Any(t => t.NeedsConfirmation)) return "有受阻或覆盖缺口，查看首轮与复测对照";
        return "已有真人试玩候选，不代表正确性或乐趣已验收";
    }
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
        report = new Report { seed = seed, scope = replay == null ? scope.ToString() : "Replay", startedUtc = DateTime.UtcNow.ToString("O"),
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
                    report.regressionPassed = TestReportRunner.LastPassed;
                    report.regressionFailed = TestReportRunner.LastFailed;
                    report.regressions = report.regressionFailed > 0 ? "Failures" : report.regressionPassed > 0 ? "Passed" : "No tests reported";
                    if (File.Exists(TestReportRunner.LastReportFile)) File.Copy(TestReportRunner.LastReportFile, Path.Combine(state.directory, "TestReport.txt"), true);
                    SetPhase(state.cancel ? "Restoring" : "Preparing"); Persist();
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
                    observer = new ExplorationTrialObserver(CurrentScenario, CurrentTrial);
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

    private static MechanismExplorationPlan.Scenario CurrentScenario
    {
        get
        {
            int index = state.step / MechanismExplorationPlan.Profiles.Length;
            if (index < report.scenarios.Count) return report.scenarios[index];
            return report.scenarios.First(s => s.id == report.confirmationScenarioIds[index - report.scenarios.Count]);
        }
    }
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
        var scenario = CurrentScenario;
        var trial = new MechanismExplorationPlan.Trial { scenarioId = scenario.id,
            profile = MechanismExplorationPlan.Profiles[state.step % MechanismExplorationPlan.Profiles.Length],
            attempt = state.step < report.scenarios.Count * MechanismExplorationPlan.Profiles.Length ? 1 : 2, outcome = "Building" };
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
        sb.AppendLine($"Status: {data.status}; seed: {data.seed}; Unity: {data.unityVersion}; scope: {data.scope}");
        sb.AppendLine($"Trials recorded: {data.trials.Count} / {PlannedTrials(data)}");
        sb.AppendLine("Evidence verdict: " + EvidenceVerdict(data));
        sb.AppendLine($"有效试玩记录: {data.trials.Count(t => t.HasGameplayEvidence)}; 首轮={data.trials.Count(t => t.attempt <= 1 && t.HasGameplayEvidence)}; 复测={data.trials.Count(t => t.attempt == 2 && t.HasGameplayEvidence)}");
        sb.AppendLine("有限反馈：最多追加 6 张问题图 × 3 画像 × 1 轮；同种子同配置，不自动改难度或删除失败记录。");
        if (!string.IsNullOrEmpty(data.blockedReason)) sb.AppendLine(data.blockedReason);
        foreach (var group in data.trials.Where(t => t.InfrastructureKey.Length > 0).GroupBy(t => t.InfrastructureKey))
            sb.AppendLine($"基础故障 ×{group.Count()}: {group.Key}");
        sb.AppendLine(data.limitation);
        sb.AppendLine($"Unity regressions: {data.regressions}; passed={data.regressionPassed}, failed={data.regressionFailed}");
        sb.AppendLine("Complete 表示调度结束，不表示每局通过，更不表示全部机制已验证。");
        sb.AppendLine("未规划的 Registry 机制: " + string.Join(", ", data.unsupportedRegistry ?? Array.Empty<string>()));
        var attempted = new HashSet<string>(data.trials.Where(t => t.outcome != "Building" && t.outcome != "Running").Select(t => t.scenarioId));
        sb.AppendLine("尚无完成记录的场景: " + string.Join(", ", data.scenarios.Where(s => !attempted.Contains(s.id)).Select(s => s.id)));
        sb.AppendLine("没有有效试玩证据的场景: " + string.Join(", ", data.scenarios
            .Where(s => !data.trials.Any(t => t.scenarioId == s.id && t.HasGameplayEvidence)).Select(s => s.id)));
        foreach (char mechanism in MechanismExplorationPlan.Catalog)
        {
            var evidence = data.trials.SelectMany(t => t.coverage).Where(e => e.mechanism == mechanism.ToString()).ToArray();
            sb.AppendLine($"{mechanism}: built={evidence.Sum(e => e.built)}, approachedTrials={evidence.Count(e => e.approached)}, contacts={evidence.Sum(e => e.contacts)} (runner={evidence.Sum(e => e.runnerContacts)}, trickster={evidence.Sum(e => e.tricksterContacts)}), activations={evidence.Sum(e => e.activations)}");
        }
        foreach (var trial in data.trials)
        {
            sb.AppendLine($"\n{trial.scenarioId} / {trial.profile} / attempt={trial.attempt}: {trial.outcome}, {trial.seconds:F1}s, end=({trial.endX:F1},{trial.endY:F1}), farthestX={trial.farthestX:F1}");
            sb.AppendLine($"pair exercised={trial.PairExercised}; human-play candidate={trial.CandidateForHumanPlay}");
            sb.AppendLine(trial.nextAction);
            sb.AppendLine("结束原因: " + trial.endReason);
            if (!string.IsNullOrEmpty(trial.comparison)) sb.AppendLine(trial.comparison);
            foreach (var item in trial.timeline) sb.AppendLine("  event: " + item);
            sb.AppendLine($"scan={trial.scans}, possession={trial.possessions}, combo={trial.comboEvents}, heat={trial.heatEvents}, loot={trial.lootEvents}, escape={trial.escapeEvents}, crisis={trial.crises}, reveal={trial.reveals}");
            sb.AppendLine($"route degraded={trial.routeDegradations}, recovered={trial.routeRecoveries}, guard={trial.routeBlocks}. Zero means not observed, NOT passed.");
            sb.AppendLine("Static hints: " + trial.validation);
            foreach (var e in trial.coverage) sb.AppendLine($"  {e.mechanism}: {e.Status}; observed phases={string.Join(",", e.phases)}");
            foreach (var error in trial.errors) sb.AppendLine(error);
        }
        return sb.ToString();
    }
}
