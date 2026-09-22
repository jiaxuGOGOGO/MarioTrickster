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
        public bool importedReadOnly;
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
        public string controlMode; // Automated / Demonstration / HumanMario / HumanTrickster
        public string parentReport;
        public string sourceFingerprint;
        public string planFingerprint;
        public string iterationComparison; // Written after real child matches; never an automatic fun ranking.
        public List<string> playerNotes = new List<string>();
        public string toolRevision; // Set only when starting; old reports remain unlabelled.
        public string startedUtc;
        public string finishedUtc;
        public string unityVersion;
        public float fixedDeltaTime;
        public float trialLimitSeconds;
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
    public static bool ImportedReadOnly => state != null && state.importedReadOnly;
    public static int Completed => report?.trials.Count(t => t.outcome != "Running" && t.outcome != "Building") ?? 0;
    public static int Total => PlannedTrials(report);
    public static int PlannedTrials(Report data) => data == null ? 0 :
        MechanismExplorationPlan.TrialCount(data.scenarios) + MechanismExplorationPlan.TrialCount(
            data.scenarios.Where(s => (data.confirmationScenarioIds ?? new List<string>()).Contains(s.id)));

    public static string TestTrack(Report data)
    {
        if (data == null || data.scenarios.Count == 0) return "无场景计划";
        if (data.scenarios.All(s => s.tunnelVersion >= 1)) return "地道博弈";
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
        if (!string.IsNullOrEmpty(data.controlMode) && data.controlMode != "Automated")
            return $"{data.controlMode} 独立试玩记录；不计入自动批次覆盖或配对改善。结果与主观反馈分别保存。";
        if (!string.IsNullOrEmpty(data.blockedReason)) return "回归或基础故障阻塞，未执行部分不算通过";
        int played = data.trials.Count(t => t.HasGameplayEvidence);
        if (played == 0) return "尚无有效试玩证据（不是玩法通过）";
        if (data.regressionFailed > 0 || data.trials.Any(t => t.errors.Count > 0)) return "有回归或运行错误，先修复再评估玩法";
        if (data.trials.Any(t => t.HasGameplayEvidence && ExperienceIssues(data, t).Length > 0))
            return "体验证据有缺口：通关不代表上路/交手机会/换点成立";
        if (UnverifiedSlots(data) > 0) return "调度证据不完整：存在缺失、未试玩或重复槽位，不能整批验收";
        if (data.trials.Any(t => t.NeedsConfirmation)) return "有受阻或覆盖缺口，查看首轮与复测对照";
        var tunnelDuels = data.trials.Where(t => t.attempt <= 1 && t.IsAutomated && t.tunnelVersion >= 1 && t.tricksterStrategy == "TunnelChaser").ToArray();
        if (tunnelDuels.Length > 0 && tunnelDuels.All(t => t.controlsAfterTunnel == 0))
            return "对战数据已完成，但缺少3秒内转移后出手；长驻留返程另看证据，不表示关卡更好玩";
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
                if (records.Length != 1 || !records[0].HasGameplayEvidence ||
                    ((string.IsNullOrEmpty(data.controlMode) || data.controlMode == "Automated") && !records[0].IsAutomated)) missing++;
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
        a != null && b != null && a.IsAutomated && b.IsAutomated && a.counterplayVersion >= 1 && b.counterplayVersion >= 1 &&
        a.scenarioId == room.id && b.scenarioId == room.id && a.counterplayVersion == b.counterplayVersion &&
        (a.marioStrategy != b.marioStrategy || a.scanPolicy == b.scanPolicy) &&
        Math.Abs(a.startDelaySeconds - room.startDelaySeconds) < 0.001f && Math.Abs(b.startDelaySeconds - room.startDelaySeconds) < 0.001f &&
        a.healthEvidenceVersion >= 1 && b.healthEvidenceVersion >= 1 && a.HasGameplayEvidence && b.HasGameplayEvidence &&
        Math.Abs(a.actualStartWaitSeconds - a.startDelaySeconds) < 0.05f && Math.Abs(b.actualStartWaitSeconds - b.startDelaySeconds) < 0.05f &&
        a.outcome == "Cleared" && b.outcome == "Cleared" &&
        a.errors.Count == 0 && b.errors.Count == 0 &&
        MechanismExplorationPlan.PassiveControlClean(a) && MechanismExplorationPlan.PassiveControlClean(b);

    public static string TunnelDesignSummary(Report data)
    {
        var sb = new StringBuilder();
        foreach (var room in data.scenarios.Where(r => r.tunnelVersion >= 1))
        {
            sb.AppendLine(room.id + " — " + room.designQuestion);
            foreach (var t in data.trials.Where(t => t.scenarioId == room.id && t.attempt <= 1))
            {
                string returnTime = t.HasGameplayEvidence && t.outcome == "Cleared" && t.errors.Count == 0 &&
                    t.lootEvents > 0 && t.escapeEvents > 0 && ValidReturnTimes(t)
                    ? (t.escapeAtSeconds - t.lootAtSeconds).ToString("F2") + "s" : "未验证";
                sb.AppendLine($"  {t.profile}: {t.outcome} / 损血{t.runnerHealthLost} / 返程{returnTime} / 暗线请求{t.tunnelRequests}、开始{t.tunnelStarts}、到达{t.tunnelArrivals}、拿宝后到达{t.postLootTunnelArrivals} / 到达后3秒同点受理{t.controlsAfterTunnel}");
                if (t.tricksterStrategy == "TunnelChaser") sb.AppendLine("  " + TunnelVisitSummary(t));
            }
            if (!string.IsNullOrEmpty(data.controlMode) && data.controlMode != "Automated")
            { sb.AppendLine("  本次为独立演示/真人试玩，不作自动策略诊断；请结合玩家反馈。"); continue; }
            var first = data.trials.Where(t => t.scenarioId == room.id && t.attempt <= 1 && t.IsAutomated && t.tricksterStrategy == "TunnelChaser").ToArray();
            if (first.Length == 0 || first.Any(t => !t.HasGameplayEvidence || t.errors.Count > 0 || t.tunnelEvidenceVersion < 1))
                sb.AppendLine("  结论：证据不足；先检查生成、运行或观察器，不调整难度来制造通过。");
            else if (first.All(t => t.tunnelArrivals == 0))
                sb.AppendLine("  下一轮：先查暗线连接、输入门禁和接近时机；普通换点不是暗线到达。");
            else if (first.All(t => t.controlsAfterTunnel == 0))
                sb.AppendLine("  下一轮：已发生转移，但尚无到达后的同点出手证据；检查出口位置和玩家经过时机，不直接加伤害。");
            else sb.AppendLine("  下一轮：已有转移后出手的时序证据；请试玩判断线索是否读得懂、是博弈还是纯等待。不是因果或乐趣通过。");
        }
        if (sb.Length > 0) sb.AppendLine("三种作者变体共享基础布局；原始结果逐策略保留，不计算乐趣排名。GroundChaser禁止方向换点，TunnelChaser使用局部距离预测；底层启发式仍读取角色位置，不代表人类有限信息。真人/演示另存，不补自动样本。");
        return sb.ToString();
    }

    public static string DuelOpponentLabel(string strategy)
    {
        switch (strategy)
        {
            case "Passive": return "无干扰基线（对手不行动）";
            case "GroundChaser": return "地面追击";
            case "TunnelChaser": return "地道换位对手";
            default: return strategy ?? "未记录";
        }
    }

    public static string DuelFeedbackCompleteness(Report data)
    {
        if (data == null) return "尚无反馈报告。";
        if (!string.IsNullOrEmpty(data.controlMode) && data.controlMode != "Automated")
            return "单局演示/真人记录：不是误操作，但不能代替完整自动对照。";
        int plannedFirst = MechanismExplorationPlan.TrialCount(data.scenarios);
        int first = data.trials.Count(t => t.attempt <= 1);
        int confirmation = data.trials.Count(t => t.attempt == 2);
        int plannedConfirmation = PlannedTrials(data) - plannedFirst;
        bool complete = plannedFirst > 0 && data.status == "Complete" && string.IsNullOrEmpty(data.blockedReason) &&
            data.regressionFailed == 0 && UnverifiedSlots(data) == 0 && first == plannedFirst && confirmation == plannedConfirmation &&
            data.trials.Count == first + confirmation && data.trials.All(t => t.IsAutomated && t.errors.Count == 0);
        bool humanNotes = (data.playerNotes != null && data.playerNotes.Count > 0) || data.trials.Any(t => t.feedback != null && t.feedback.Count > 0);
        return $"反馈完整性：首轮 {first}/{plannedFirst}，确认 {confirmation}/{plannedConfirmation}。" +
            (complete ? " 已完整，足够分析本批运行与策略，不必重复补跑。" : " 尚未完整或存在故障/重复记录，请保留失败并查看缺口，不补算通过。") +
            (data.regressionPassed > 0 && data.regressionFailed == 0 ? $" 回归 {data.regressionPassed} 通过。" : " 本批没有完整通过的回归证据。") +
            (humanNotes ? " 已有用户备注；备注不等于乐趣验收。" : " 尚无真人感受记录；不影响运行诊断，但不能判断更好玩。") +
            " 父报告与原始基线可能是同一批，不重复计样本。";
    }

    // This is report completeness, not a fun score or permission to exceed the iteration cap.
    public static bool DuelReportReadyForReview(Report data, MechanismExplorationPlan.Scenario room)
    {
        return data != null && room != null && data.scenarios.Contains(room) && room.tunnelVersion >= 1 &&
            MechanismExplorationPlan.Matchups(room).Length == 6 && data.status == "Complete" &&
            (string.IsNullOrEmpty(data.controlMode) || data.controlMode == "Automated") &&
            string.IsNullOrEmpty(data.blockedReason) && data.regressionFailed == 0 &&
            data.trials.Count == PlannedTrials(data) && UnverifiedSlots(data) == 0 &&
            data.trials.All(t => t.IsAutomated && t.errors.Count == 0 && t.attempt >= 0 && t.attempt <= 2);
    }

    public static string DuelNextAction(Report data, MechanismExplorationPlan.Scenario room)
    {
        if (data == null || room == null) return "先生成一张图，再运行完整6组对照。";
        if (!string.IsNullOrEmpty(data.controlMode) && data.controlMode != "Automated")
            return "这是单局记录，不补算自动覆盖。可保存感受并导出ZIP；继续比较前先返回完整对照报告。";
        if (!DuelReportReadyForReview(data, room))
            return "本批尚未完整或有执行故障：先查看明细并导出ZIP，不生成下一变体来绕过缺口。";
        var runs = data.trials.Where(t => t.scenarioId == room.id).ToArray();
        if (runs.Any(t => t.outcome != "Cleared" || !ValidReturnTimes(t) || t.lootEvents < 1 || t.escapeEvents < 1 ||
            (t.marioStrategy == "SafeRoute" && (t.completedRoutes == null || !t.completedRoutes.Contains("Out:upper") || !t.completedRoutes.Contains("Return:upper")))))
            return "记录已收齐，但有通路或路线未完成。先保留问题并导出ZIP，不把通关/首轮成功替代确认局。";
        if (room.iteration >= 2)
            return "本图6组与计划确认已齐，本轮两次连接试验已结束。下一步看报告原图地表对战，或亲自玩一局；不要再生成变体，也不必重复补跑同批。";
        return "本批记录已齐。先看报告原图交手；若继续连接试验，用下方按钮生成下一版，无需重新生成种子或载入草稿。";
    }

    public static string DuelEncounterLine(MechanismExplorationPlan.Trial t)
    {
        if (t == null) return "缺少唯一记录；不借另一轮补算。";
        if (!t.IsAutomated) return "单局/真人记录，不作自动对照结论。";
        if (!t.HasGameplayEvidence || t.errors.Count > 0) return "未完成有效观察或有执行错误。";
        if (t.tunnelEvidenceVersion < 1) return "暗线证据未记录，不能把零值当作没有换位。";
        string transfer;
        if (t.tunnelArrivals == 0)
            transfer = t.tunnelRequests > 0 ? "有换位请求，未观察到真实暗线到达" : "未观察到暗线换位";
        else if (t.controlsAfterTunnel > 0)
            transfer = "已有到达后3秒内同出口出手受理";
        else if (t.tunnelVisitEvidenceVersion >= 1 && (t.tunnelVisits ?? new List<MechanismExplorationPlan.TunnelVisit>()).Any(v => v.firstControlAt >= 0f))
            transfer = "已有连续驻留出手，不能仅因3秒计数为0判无效";
        else if (t.tunnelVisitEvidenceVersion >= 1 && (t.tunnelVisits ?? new List<MechanismExplorationPlan.TunnelVisit>()).Any(v => v.readyAt >= 0f && v.runnerPassedWhenReady))
            transfer = "已到达，但有出口就绪时玩家已越过的记录；未记录驻留出手";
        else transfer = "已到达，尚无转移后出手证据；不推断有效伏击";
        string returned = t.startTimingEvidenceVersion >= 1
            ? $"；返程普通操控受理{t.postLootControls}次" : "；返程操控未记录";
        string motion = $"；观察到预警退让{t.telegraphRetreats}次、恢复窗口穿越{t.recoveryCrossings}次";
        return transfer + returned + motion + "。这些是时序/动作记录，不是因果或乐趣评分。";
    }

    public static string DuelReportHighlights(Report data, MechanismExplorationPlan.Scenario room)
    {
        if (data == null || room == null) return "尚无对战报告。";
        if (!string.IsNullOrEmpty(data.controlMode) && data.controlMode != "Automated")
            return "单局观战/真人试玩单独保存，不替代自动6组。请记录你实际看见的选择或单调之处。";
        var sb = new StringBuilder();
        foreach (string runner in new[] { "SafeRoute", "Adaptive" })
        {
            var first = UniqueTrial(data.trials.Where(t => t.scenarioId == room.id && t.attempt <= 1).ToArray(), runner, "TunnelChaser");
            sb.AppendLine((runner == "SafeRoute" ? "地表首轮：" : "下层首轮：") + DuelEncounterLine(first));
            if (data.confirmationScenarioIds.Contains(room.id) || data.trials.Any(t => t.scenarioId == room.id && t.attempt == 2))
            {
                var confirm = UniqueTrial(data.trials.Where(t => t.scenarioId == room.id && t.attempt == 2).ToArray(), runner, "TunnelChaser");
                sb.AppendLine("  确认：" + DuelEncounterLine(confirm));
                if (first != null && confirm != null && !string.IsNullOrEmpty(confirm.comparison) && confirm.comparison.Contains("不稳定"))
                    sb.AppendLine("  原报告标记同条件不稳定，须保留；相同通关或出手数不能抹去其他覆盖差异。");
            }
        }
        var runs = data.trials.Where(t => t.scenarioId == room.id).ToArray();
        if (runs.Length == 0 || runs.Any(t => t.stairRecoveryEvidenceVersion < 1)) sb.Append("落阶重走未完整记录。");
        else if (runs.All(t => t.stairRecoveryRequests == 0)) sb.Append("本批未触发落阶重走，不能认证实战跌落恢复。");
        else sb.Append("本批出现落阶重走请求；是否恢复须看实际路线，不能用请求数替代成功。");
        return sb.ToString();
    }

    public static MechanismExplorationPlan.Scenario CopyDuelForFullReplay(MechanismExplorationPlan.Scenario room)
    {
        if (room == null || room.tunnelVersion < 1) throw new ArgumentException("需要报告中的地道关卡");
        var copy = JsonUtility.FromJson<MechanismExplorationPlan.Scenario>(JsonUtility.ToJson(room));
        copy.selectedMatchups = Array.Empty<MechanismExplorationPlan.Matchup>();
        return copy; // Never regenerate seed/variant or mutate the saved parent to restore all six matchups.
    }

    public static string DuelRouteSummary(Report data, MechanismExplorationPlan.Scenario room)
    {
        if (data == null || room == null) return "尚无实际路线记录。";
        var sb = new StringBuilder("实际地表完成（不是策略名称）：");
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            var runs = data.trials.Where(t => t.scenarioId == room.id && t.marioStrategy == "SafeRoute" &&
                (attempt == 1 ? t.attempt <= 1 : t.attempt == 2)).ToArray();
            int expected = attempt == 1 ? MechanismExplorationPlan.Matchups(room).Count(m => m.mario == "SafeRoute") :
                (data.confirmationScenarioIds.Contains(room.id) ? MechanismExplorationPlan.Matchups(room).Count(m => m.mario == "SafeRoute") : 0);
            int complete = new[] { "Passive", "GroundChaser", "TunnelChaser" }.Count(opponent => {
                var t = UniqueTrial(runs, "SafeRoute", opponent);
                return t != null && t.IsAutomated && t.HasGameplayEvidence && t.errors.Count == 0 &&
                    t.completedRoutes != null && t.completedRoutes.Contains("Out:upper") && t.completedRoutes.Contains("Return:upper");
            });
            sb.Append($" {(attempt == 1 ? "首轮" : "确认")} {complete}/{expected}；");
            if (runs.Any(t => t.completedRoutes == null || !t.completedRoutes.Contains("Out:upper") || !t.completedRoutes.Contains("Return:upper")))
                sb.Append("存在未完成地表/回退，不能用通关或另一轮覆盖。 ");
        }
        return sb.ToString();
    }

    public static string DuelTrialRouteSummary(MechanismExplorationPlan.Trial t)
    {
        string actual = t.completedRoutes != null && t.completedRoutes.Count > 0 ? string.Join(", ", t.completedRoutes) : "未记录完整路线";
        return $"实际完成：{actual}；换路请求{t.routeSwitchRequests}；" +
            (t.stairRecoveryEvidenceVersion >= 1 ? $"落阶重走请求{t.stairRecoveryRequests}（不是恢复成功）" : "落阶重走未记录（旧版）");
    }

    public static string TunnelVisitSummary(MechanismExplorationPlan.Trial t)
    {
        if (t.tunnelVisitEvidenceVersion < 1) return "持续驻留/重新就绪未记录（旧版）；3秒外出手不能自动补算或判无效。";
        var visits = t.tunnelVisits ?? new List<MechanismExplorationPlan.TunnelVisit>();
        string decisions = t.tunnelDecisionEvidenceVersion >= 1
            ? $"暗线输入请求{t.tunnelRequests}，其中准备换层{t.tunnelPreparationRequests}（不保证拦截）；"
            : "换位意图分类未记录（旧版）；";
        return decisions + $"出口驻留：记录{visits.Count}，重新就绪{visits.Count(v => v.readyAt >= 0)}，就绪时玩家已越过{visits.Count(v => v.runnerPassedWhenReady)}；连续同点出手{visits.Count(v => v.firstControlAt >= 0)}，其中返程出手{visits.Count(v => v.returnControlAt >= 0)}。" +
            (t.tunnelVisitOverflow > 0 ? $"超出上限未详记{t.tunnelVisitOverflow}次。" : "") + "受理/驻留只证明时序与同点关系，不证明因果或好玩。";
    }

    public static string DuelReview(Report data, MechanismExplorationPlan.Scenario room)
    {
        if (data == null || room == null) return "先生成一张关卡，再让双方对战。";
        var first = data.trials.Where(t => t.scenarioId == room.id && t.attempt <= 1).ToArray();
        if (first.Length == 0) return "还没有这张图的实际对战，不能评价交互。";
        if (!string.IsNullOrEmpty(data.controlMode) && data.controlMode != "Automated")
            return first.All(t => t.tricksterStrategy == "Passive")
                ? "你看的是无干扰基线：对手不行动，只演示拿宝折返。请选地道对手，不把空跑当玩法展示。"
                : "这是单局演示或真人试玩，不能代替完整对照。你的体验标记会保留，不推断是否好玩。";
        var duels = first.Where(t => t.IsAutomated && t.tricksterStrategy == "TunnelChaser").ToArray();
        if (duels.Length != 2 || duels.Select(t => t.marioStrategy).Distinct().Count() != 2 ||
            !duels.Any(t => t.marioStrategy == "Adaptive") || !duels.Any(t => t.marioStrategy == "SafeRoute") ||
            duels.Any(t => !t.HasGameplayEvidence || t.errors.Count > 0 || t.tunnelEvidenceVersion < 1))
            return "地道对战证据尚不完整。先处理未执行或运行问题，再调整设计。";
        string facts = $"地道到达 {duels.Sum(t => t.tunnelArrivals)} 次；到达后3秒同出口出手受理 {duels.Sum(t => t.controlsAfterTunnel)} 次；实际换路 {duels.Sum(t => t.routeTransitions)} 次。";
        return DuelRouteSummary(data, room) + "\n" + facts + (duels.All(t => t.controlsAfterTunnel == 0)
            ? " 当前仍缺少转移后的出手证据（3秒窗口）；不能据此否定更晚的返程出手，也不要把到达数当伏击成功。"
            : " 已有转移后出手的时序证据；是否读得懂、是否只是在等，仍由试玩判断。") +
            "\n" + string.Join("\n", duels.Select(t => (t.marioStrategy == "SafeRoute" ? "地表：" : "下层：") + TunnelVisitSummary(t)));
    }

    public static string IterationBlockReason(Report data, MechanismExplorationPlan.Scenario room)
    {
        if (data == null || room == null || !data.scenarios.Contains(room)) return "先选择这份报告中的关卡。";
        if (room.duelVersion != 1) return "旧样板或手工图保留原样；请在第1步生成新的种子关卡。";
        if (!MechanismExplorationPlan.IsGeneratedDuelLayout(room)) return "这张图有手工修改，不能自动覆盖连接或作者路点；请保留草稿并回传设计。";
        if (room.iteration < 0 || room.iteration >= 2) return "已完成本轮两次变体。先试玩复盘，保留喜欢的版本；不无限刷局。";
        if (data.status != "Complete" || !string.IsNullOrEmpty(data.blockedReason) || data.regressionFailed > 0)
            return "批次未完整结束或被故障阻塞，不能自动改关卡。";
        if (!string.IsNullOrEmpty(data.controlMode) && data.controlMode != "Automated") return "单局演示/真人不作为自动改图依据；先跑这张图的6组对照。";
        if (MechanismExplorationPlan.Matchups(room).Length != 6) return "需要完整6组对照，而不是挑选一场成功记录。";
        var first = data.trials.Where(t => t.scenarioId == room.id && t.attempt <= 1).ToArray();
        foreach (string mario in new[] { "Adaptive", "SafeRoute" })
        foreach (string opponent in new[] { "Passive", "GroundChaser", "TunnelChaser" })
        {
            var t = UniqueTrial(first, mario, opponent);
            if (t == null || !t.IsAutomated || !t.HasGameplayEvidence || t.errors.Count > 0 ||
                t.tunnelEvidenceVersion < 1 || t.healthEvidenceVersion < 1 || !MechanismExplorationPlan.PassiveControlClean(t) ||
                !MechanismExplorationPlan.IndependentStartObserved(t) ||
                !MechanismExplorationPlan.IsFinite(t.actualStartWaitSeconds) || Math.Abs(t.startDelaySeconds - room.startDelaySeconds) > 0.001f ||
                !MechanismExplorationPlan.IsFinite(t.startDelaySeconds) || Math.Abs(t.actualStartWaitSeconds - room.startDelaySeconds) >= 0.05f ||
                (opponent != "TunnelChaser" && t.tunnelStarts != 0)) return "首轮6组对照缺失、重复、有错误或缺少真实证据；确认局不能补齐。";
            if (t.outcome != "Cleared" || !ValidReturnTimes(t) || t.lootEvents < 1 || t.escapeEvents < 1 ||
                (mario == "SafeRoute" && (t.completedRoutes == null || !t.completedRoutes.Contains("Out:upper") || !t.completedRoutes.Contains("Return:upper"))))
                return "先解决通路/拿宝返程问题，再试连接变体；不把导航失败当作提高难度的理由。";
        }
        if (first.Length != 6) return "首轮存在额外或重复记录，不能作一致对照。";
        if (data.trials.Any(t => t.scenarioId == room.id && t.attempt == 2 && t.marioStrategy == "SafeRoute" &&
            (t.completedRoutes == null || !t.completedRoutes.Contains("Out:upper") || !t.completedRoutes.Contains("Return:upper"))))
            return "确认局地表路线未完成或回退；先复验导航，不用首轮成功覆盖不稳定。";
        return "";
    }

    public static MechanismExplorationPlan.Scenario ProposeDuelIteration(Report data, MechanismExplorationPlan.Scenario room)
    {
        string blocked = IterationBlockReason(data, room);
        if (blocked.Length > 0) throw new InvalidOperationException(blocked);
        var tunnel = data.trials.Where(t => t.scenarioId == room.id && t.attempt <= 1 && t.tricksterStrategy == "TunnelChaser").ToArray();
        string reason = tunnel.All(t => t.controlsAfterTunnel == 0)
            ? "完整对照后仍无转移后出手：只改变暗线连接，比较是否减少无效换位。"
            : "已有转移后出手记录：只改变暗线连接，检查路线与返程差异；不声称更好玩。";
        if (room.duelVariant == 1)
            reason += " 本次只把地表出口的连接入口从中继改回左侧，保留下层串联；检验减少一次中转后能否赶上上路玩家。不是保证改善。";
        return MechanismExplorationPlan.NextDuelVariant(room, reason);
    }

    public static string DuelIterationComparison(Report parent, Report child)
    {
        if (parent == null || child == null) return "缺少父子报告，不比较改善。";
        var room = child.scenarios.SingleOrDefault(s => s.duelVersion == 1 && !string.IsNullOrEmpty(s.parentScenarioId));
        if (room == null) return "";
        var previous = parent.scenarios.SingleOrDefault(s => s.id == room.parentScenarioId);
        if (previous == null || IterationBlockReason(parent, previous).Length > 0 ||
            child.status != "Complete" || child.regressionFailed > 0 || !string.IsNullOrEmpty(child.blockedReason) ||
            (!string.IsNullOrEmpty(child.controlMode) && child.controlMode != "Automated") || UnverifiedSlots(child) != 0 ||
            string.IsNullOrEmpty(parent.sourceFingerprint) || parent.sourceFingerprint != child.sourceFingerprint ||
            parent.physicsConfigJson != child.physicsConfigJson || parent.gameplayConfigJson != child.gameplayConfigJson ||
            parent.unityVersion != child.unityVersion || parent.fixedDeltaTime != child.fixedDeltaTime ||
            parent.trialLimitSeconds <= 0 || parent.trialLimitSeconds != child.trialLimitSeconds ||
            !MechanismExplorationPlan.IsGeneratedDuelLayout(room) || room.ascii != previous.ascii || room.seed != previous.seed ||
            room.iteration != previous.iteration + 1 || room.duelVariant != (previous.duelVariant + 1) % 3 || MechanismExplorationPlan.Matchups(room).Length != 6)
            return "父子对照条件不一致或证据未完成；保留两份结果，不报告改善。";
        var sb = new StringBuilder("父版 → 本版（仅首轮；差异不等于更好玩）\n");
        bool lostInteraction = false;
        foreach (string mario in new[] { "Adaptive", "SafeRoute" })
        foreach (string opponent in new[] { "Passive", "GroundChaser", "TunnelChaser" })
        {
            var a = UniqueTrial(parent.trials.Where(t => t.scenarioId == previous.id && t.attempt <= 1).ToArray(), mario, opponent);
            var b = UniqueTrial(child.trials.Where(t => t.scenarioId == room.id && t.attempt <= 1).ToArray(), mario, opponent);
            if (a == null || b == null || !b.IsAutomated || !b.HasGameplayEvidence || b.errors.Count > 0 || b.tunnelEvidenceVersion < 1 ||
                b.healthEvidenceVersion < 1 || !MechanismExplorationPlan.PassiveControlClean(b) || !MechanismExplorationPlan.IndependentStartObserved(b) ||
                !MechanismExplorationPlan.IsFinite(b.actualStartWaitSeconds) || !MechanismExplorationPlan.IsFinite(b.startDelaySeconds) ||
                Math.Abs(b.startDelaySeconds - room.startDelaySeconds) > 0.001f || Math.Abs(b.actualStartWaitSeconds - room.startDelaySeconds) >= 0.05f ||
                (opponent != "TunnelChaser" && b.tunnelStarts != 0))
            { sb.AppendLine("缺少唯一真实首轮记录；不比较。"); continue; }
            sb.AppendLine($"{(mario == "SafeRoute" ? "地表路线" : "下层路线")} / {DuelOpponentLabel(opponent)}：{a.outcome} → {b.outcome}；转移后3秒出手 {a.controlsAfterTunnel} → {b.controlsAfterTunnel}；换路 {a.routeTransitions} → {b.routeTransitions}；损血 {a.runnerHealthLost} → {b.runnerHealthLost}");
            if (a.outcome == "Cleared" && b.outcome == "Cleared" && ValidReturnTimes(a) && ValidReturnTimes(b) &&
                a.lootEvents > 0 && b.lootEvents > 0 && a.escapeEvents > 0 && b.escapeEvents > 0)
                sb.AppendLine($"  去程 {a.lootAtSeconds:F2}s → {b.lootAtSeconds:F2}s；返程 {a.escapeAtSeconds - a.lootAtSeconds:F2}s → {b.escapeAtSeconds - b.lootAtSeconds:F2}s；返程操控 {a.postLootControls} → {b.postLootControls}。耗时变化不是乐趣分。");
            if (opponent == "TunnelChaser" && a.controlsAfterTunnel > 0 && b.controlsAfterTunnel == 0) lostInteraction = true;
            if (a.tunnelVisitEvidenceVersion >= 1 && b.tunnelVisitEvidenceVersion >= 1 &&
                a.tunnelVisitOverflow == 0 && b.tunnelVisitOverflow == 0)
                sb.AppendLine($"  连续驻留返程出手 {a.tunnelVisits.Count(v => v.returnControlAt >= 0)} → {b.tunnelVisits.Count(v => v.returnControlAt >= 0)}；不同于3秒窗口。");
        }
        if (lostInteraction) sb.AppendLine("设计警示：子版失去了父版已有的3秒内转移后出手；不要因通关或到达更多就称为升级。保留父版并优先对看该路线；不据此判定总体乐趣优劣。");
        sb.AppendLine("不自动淘汰父版，不用确认局覆盖首轮；请分别试玩后标记喜欢哪版及原因。");
        return sb.ToString();
    }

    public static void StartDuelIteration(MechanismExplorationPlan.Scenario room, float seconds, bool regressions)
    {
        if (Active || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var child = ProposeDuelIteration(report, room);
        if (report.sourceFingerprint != SourceFingerprint() || report.physicsConfigJson != ConfigJson("PhysicsConfig") ||
            report.gameplayConfigJson != ConfigJson("GameplayLoopConfig") || report.fixedDeltaTime != Time.fixedDeltaTime || report.unityVersion != Application.unityVersion)
            throw new InvalidOperationException("源码/配置/Unity已变化。请先原图重测6组，避免把环境改变算作关卡改善。");
        if (report.trialLimitSeconds != Mathf.Clamp(seconds, 10, 120))
            throw new InvalidOperationException("单局预算与父版不同；先用相同预算原图重测。");
        Start(child.seed, MechanismExplorationPlan.Scope.TunnelDuel, seconds, child, regressions);
    }

    public static void AddFeedback(string text)
    {
        if (report == null || string.IsNullOrWhiteSpace(text)) return;
        if (ImportedReadOnly) throw new InvalidOperationException("导入报告只读；请先启动原图试玩，再保存新的感受。");
        text = text.Trim(); text = text.Substring(0, Math.Min(500, text.Length));
        if (report.playerNotes == null) report.playerNotes = new List<string>();
        if (report.playerNotes.Count >= 60) return;
        report.playerNotes.Add(DateTime.UtcNow.ToString("O") + " [" + (report.controlMode ?? "Automated") + "] " + text);
        observer?.MarkFeedback(text);
        Persist();
    }

    public sealed class FeedbackDocument
    {
        public string key, parentKey, baselineKey, label;
        public byte[] bytes, summary, tests;
        public Report data;
    }

    [Serializable]
    private sealed class ImportedReportLinks
    {
        public string parentKey, baselineKey;
    }

    public sealed class ImportedReportChoice
    {
        public string directory, label;
    }

    // Never extract archive paths. Only bounded, explicitly supported text payloads are read.
    public static bool SafeFeedbackEntryName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 512 || name.StartsWith("/") || name.Contains("\\") ||
            name.Any(c => char.IsControl(c) || ":<>\"|?*".Contains(c))) return false;
        var parts = name.TrimEnd('/').Split('/');
        return parts.Length <= 12 && parts.All(part => part.Length > 0 && part != "." && part != ".." &&
            !part.EndsWith(".") && !part.EndsWith(" "));
    }

    public static Dictionary<string, byte[]> ReadFeedbackArchive(Stream input)
    {
        const long perFile = 8 * 1024 * 1024, totalLimit = 64 * 1024 * 1024;
        if (!input.CanSeek || input.Length > totalLimit) throw new InvalidDataException("ZIP必须可读取且不超过64MB。");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long declared = 0, expanded = 0;
        using (var zip = new System.IO.Compression.ZipArchive(input, System.IO.Compression.ZipArchiveMode.Read, true))
        {
            if (zip.Entries.Count > 1024) throw new InvalidDataException("ZIP条目过多（最多1024）。");
            foreach (var entry in zip.Entries)
            {
                string name = entry.FullName;
                if (!SafeFeedbackEntryName(name) || !names.Add(name.TrimEnd('/')))
                    throw new InvalidDataException("ZIP包含不安全或重复路径。");
                if (entry.Length < 0 || entry.Length > perFile || (declared += entry.Length) > totalLimit)
                    throw new InvalidDataException("ZIP解压大小超限：单文件8MB、合计64MB。");
                if (name.EndsWith("/")) continue;
                string leaf = name.Substring(name.LastIndexOf('/') + 1);
                if (leaf != "report.json" && leaf != "parent_report.json" && leaf != "baseline_report.json" &&
                    leaf != "summary.txt" && leaf != "TestReport.txt") continue;
                using (var source = entry.Open())
                using (var buffer = new MemoryStream())
                {
                    var block = new byte[8192]; int read;
                    while ((read = source.Read(block, 0, block.Length)) > 0)
                    {
                        if (buffer.Length + read > perFile || (expanded += read) > totalLimit)
                            throw new InvalidDataException("ZIP实际解压大小超限。");
                        buffer.Write(block, 0, read);
                    }
                    if (buffer.Length != entry.Length) throw new InvalidDataException("ZIP条目长度不一致。");
                    files.Add(name, buffer.ToArray());
                }
            }
        }
        return files;
    }

    public static string FeedbackDocumentKey(byte[] bytes)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    public static bool SafeFeedbackScenarioId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 100 || id.Any(c => !(c >= 'a' && c <= 'z') &&
            !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '_' && c != '-')) return false;
        string upper = id.ToUpperInvariant();
        return !new[] { "CON", "PRN", "AUX", "NUL" }.Contains(upper) &&
            !(upper.Length == 4 && (upper.StartsWith("COM") || upper.StartsWith("LPT")) && upper[3] >= '1' && upper[3] <= '9');
    }

    // Schema checks protect existing UI and replay file writers; they do NOT certify test success.
    public static void ValidateFeedbackReport(Report value)
    {
        if (value == null || value.version != MechanismExplorationPlan.Version || string.IsNullOrEmpty(value.status) ||
            !DateTimeOffset.TryParse(value.startedUtc, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out _) ||
            value.scenarios == null || value.scenarios.Count == 0 || value.scenarios.Count > 512 ||
            value.trials == null || value.trials.Count > 4096 || value.confirmationScenarioIds == null)
            throw new InvalidDataException("报告格式缺失或版本不支持；未加载，原报告保持不变。");
        if (!string.IsNullOrEmpty(value.controlMode) && !new[] { "Automated", "Demonstration", "HumanMario", "HumanTrickster" }.Contains(value.controlMode))
            throw new InvalidDataException("不支持的报告控制方式。");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var room in value.scenarios)
        {
            if (room == null || !SafeFeedbackScenarioId(room.id) ||
                !ids.Add(room.id) || string.IsNullOrEmpty(room.ascii) || room.ascii.Length > 65536 || room.ascii.Contains('\0') ||
                (room.routes != null && room.routes.Any(r => r == null || r.points == null || r.points.Length > 2048 || r.points.Any(p => p == null))) ||
                (room.tunnelLinks != null && room.tunnelLinks.Any(l => l == null || l.from == null || l.to == null)) ||
                (room.selectedMatchups != null && (room.selectedMatchups.Length > 32 || room.selectedMatchups.Any(m => m == null || string.IsNullOrEmpty(m.mario) || string.IsNullOrEmpty(m.trickster)))))
                throw new InvalidDataException("报告关卡结构或关卡ID不安全；不自动修正原图。");
        }
        foreach (var trial in value.trials)
            if (trial == null || !ids.Contains(trial.scenarioId) || trial.errors == null || trial.coverage == null ||
                trial.coverage.Any(e => e == null || e.phases == null) || trial.queueEvidence == null || trial.queueEvidence.Any(q => q == null) ||
                (trial.tunnelVisits != null && trial.tunnelVisits.Any(v => v == null)))
                throw new InvalidDataException("报告对局结构损坏；不补造记录。");
        if (value.confirmationScenarioIds.Any(id => !ids.Contains(id))) throw new InvalidDataException("确认计划引用了未知关卡。");
    }

    public static FeedbackDocument[] BuildFeedbackImportPlan(Dictionary<string, byte[]> files, Func<string, Report> parse)
    {
        var documents = new Dictionary<string, FeedbackDocument>(StringComparer.Ordinal);
        var paths = new Dictionary<string, FeedbackDocument>(StringComparer.Ordinal);
        var decoder = new UTF8Encoding(false, true);
        foreach (var file in files.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            string leaf = file.Key.Substring(file.Key.LastIndexOf('/') + 1);
            if (leaf != "report.json" && leaf != "parent_report.json" && leaf != "baseline_report.json") continue;
            string key = FeedbackDocumentKey(file.Value);
            if (!documents.TryGetValue(key, out var document))
            {
                if (documents.Count >= 128) throw new InvalidDataException("一次最多导入128份不同报告。");
                string text = decoder.GetString(file.Value).TrimStart('\ufeff');
                if (!text.TrimStart().StartsWith("{")) throw new InvalidDataException("报告不是JSON对象。");
                var data = parse(text); ValidateFeedbackReport(data);
                document = new FeedbackDocument { key = key, bytes = file.Value, data = data };
                string mode = data.controlMode == "Demonstration" ? "单局观战" : data.controlMode == "HumanMario" ? "真人闯关者" :
                    data.controlMode == "HumanTrickster" ? "真人捣蛋者" : "AI完整对照";
                document.label = $"{data.startedUtc} | {data.toolRevision ?? "旧版"} | {mode} ({data.controlMode ?? "Automated"}) | " +
                    string.Join(", ", data.scenarios.Take(3).Select(r => $"种子{r.seed}/连接{r.duelVariant}/进度{r.iteration}")) + $" | {data.status} | {data.trials.Count}局";
                documents.Add(key, document);
            }
            paths.Add(file.Key, document);
        }
        if (!paths.Keys.Any(p => p == "report.json" || p.EndsWith("/report.json", StringComparison.Ordinal)))
            throw new InvalidDataException("ZIP中没有report.json；支持单批反馈ZIP或多批目录ZIP。");
        foreach (var pair in paths.Where(p => p.Key == "report.json" || p.Key.EndsWith("/report.json", StringComparison.Ordinal)))
        {
            string prefix = pair.Key.Substring(0, pair.Key.Length - "report.json".Length);
            var doc = pair.Value;
            if (paths.TryGetValue(prefix + "parent_report.json", out var parent) && parent != doc)
            {
                if (!string.IsNullOrEmpty(doc.parentKey) && doc.parentKey != parent.key) throw new InvalidDataException("相同报告携带了冲突父报告。");
                doc.parentKey = parent.key;
            }
            if (paths.TryGetValue(prefix + "baseline_report.json", out var baseline) && baseline != doc)
            {
                if (!string.IsNullOrEmpty(doc.baselineKey) && doc.baselineKey != baseline.key) throw new InvalidDataException("相同报告携带了冲突基线。");
                doc.baselineKey = baseline.key;
            }
            if (files.TryGetValue(prefix + "summary.txt", out var summary)) doc.summary = summary;
            if (files.TryGetValue(prefix + "TestReport.txt", out var tests)) doc.tests = tests;
        }
        foreach (var doc in documents.Values)
        {
            var seen = new HashSet<string>(); var cursor = doc;
            while (cursor != null)
            {
                if (!seen.Add(cursor.key)) throw new InvalidDataException("报告父链存在循环。");
                cursor = string.IsNullOrEmpty(cursor.parentKey) ? null : documents[cursor.parentKey];
            }
        }
        return documents.Values.OrderByDescending(d => DateTimeOffset.Parse(d.data.startedUtc,
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal))
            .ThenBy(d => d.key, StringComparer.Ordinal).ToArray();
    }

    private static void RequireReportNavigationIdle()
    {
        if (Active || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || TestReportRunner.IsRunning)
            throw new InvalidOperationException("请先结束正在运行的测试/试玩，再导入或切换历史报告。");
    }

    public static ImportedReportChoice[] ImportFeedbackZip(string path)
    {
        RequireReportNavigationIdle();
        FeedbackDocument[] docs;
        using (var input = File.OpenRead(path))
            docs = BuildFeedbackImportPlan(ReadFeedbackArchive(input), text => JsonUtility.FromJson<Report>(text));
        return WriteFeedbackImportSnapshots(docs);
    }

    private static ImportedReportChoice[] WriteFeedbackImportSnapshots(FeedbackDocument[] docs)
    {
        // All validation completes BEFORE touching disk or the current report. No archive filename becomes a disk path.
        string suffix = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(OutputRoot);
        string staging = Path.Combine(OutputRoot, ".import_pending_" + suffix);
        string destination = Path.Combine(OutputRoot, "import_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + suffix);
        try
        {
            Directory.CreateDirectory(staging);
            var byKey = docs.ToDictionary(d => d.key);
            foreach (var doc in docs)
            {
                string folder = Path.Combine(staging, doc.key); Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "report.json"), doc.bytes);
                if (!string.IsNullOrEmpty(doc.parentKey)) File.WriteAllBytes(Path.Combine(folder, "parent_report.json"), byKey[doc.parentKey].bytes);
                if (!string.IsNullOrEmpty(doc.baselineKey)) File.WriteAllBytes(Path.Combine(folder, "baseline_report.json"), byKey[doc.baselineKey].bytes);
                if (doc.summary != null) File.WriteAllBytes(Path.Combine(folder, "summary.txt"), doc.summary);
                if (doc.tests != null) File.WriteAllBytes(Path.Combine(folder, "TestReport.txt"), doc.tests);
                File.WriteAllText(Path.Combine(folder, "import_links.json"), JsonUtility.ToJson(new ImportedReportLinks { parentKey = doc.parentKey, baselineKey = doc.baselineKey }));
            }
            Directory.Move(staging, destination);
        }
        catch { if (Directory.Exists(staging)) Directory.Delete(staging, true); throw; }
        return docs.Select(d => new ImportedReportChoice { directory = Path.Combine(destination, d.key), label = d.label }).ToArray();
    }

    public static void LoadHistoricalReport(string directory)
    {
        RequireReportNavigationIdle();
        if (!IsReportPath(directory)) throw new InvalidOperationException("报告路径不属于本项目。");
        var value = JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(directory, "report.json")));
        ValidateFeedbackReport(value);
        report = value;
        state = new State { phase = "Complete", directory = directory, importedReadOnly = File.Exists(Path.Combine(directory, "import_links.json")) };
        EditorPrefs.SetString(LastDirectoryKey, directory); SaveState(); // Read-only navigation, never EnterPlay or Persist.
    }

    private static string LocalImportedParent(string directory)
    {
        string marker = Path.Combine(directory, "import_links.json");
        if (!File.Exists(marker)) return null;
        var links = JsonUtility.FromJson<ImportedReportLinks>(File.ReadAllText(marker));
        string key = links?.parentKey;
        if (string.IsNullOrEmpty(key)) return null;
        if (key.Length != 64 || key.Any(c => !(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')))
            throw new InvalidDataException("导入父链标识不安全。");
        return Path.Combine(Directory.GetParent(directory).FullName, key);
    }

    // Imported snapshots, not an old computer's absolute path, decide whether navigation is offered.
    public static bool HasParentReport => report != null && (ImportedReadOnly
        ? File.Exists(Path.Combine(ReportDirectory, "parent_report.json"))
        : !string.IsNullOrEmpty(report.parentReport));

    public static void LoadParentReport()
    {
        RequireReportNavigationIdle();
        if (report == null) return;
        string marker = Path.Combine(ReportDirectory, "import_links.json");
        string directory = File.Exists(marker) ? LocalImportedParent(ReportDirectory) : report.parentReport;
        if (string.IsNullOrEmpty(directory) || !IsReportPath(directory) || !File.Exists(Path.Combine(directory, "report.json")))
            throw new InvalidOperationException("父报告不在本项目。可导入包含父报告的反馈ZIP；不会访问旧电脑路径或补造父记录。");
        LoadHistoricalReport(directory);
    }

    public static string ExportFeedbackZip()
    {
        if (report == null || !IsReportPath(ReportDirectory) || !Directory.Exists(ReportDirectory))
            throw new InvalidOperationException("没有可打包的报告目录");
        if (Active) throw new InvalidOperationException("请结束本局或批次后打包，避免导出不一致记录");
        string zip = ReportDirectory + "_feedback_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zip";
        System.IO.Compression.ZipFile.CreateFromDirectory(ReportDirectory, zip);
        return zip;
    }

    private static string SourceFingerprint()
    {
        var files = Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts"), "*.cs", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            var text = new StringBuilder();
            foreach (string file in files) text.Append(file.Substring(Application.dataPath.Length).Replace('\\', '/')).Append('\n').Append(File.ReadAllText(file).Replace("\r\n", "\n"));
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }
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
            {
                report = JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(state.directory, "report.json")));
                state.importedReadOnly = File.Exists(Path.Combine(state.directory, "import_links.json"));
                if (state.importedReadOnly) { ValidateFeedbackReport(report); state.phase = "Complete"; }
            }
            if (historical && report != null)
            {
                state.phase = report.status == "Complete" ? "Complete" : report.status == "Blocked" ? "Blocked" : "Aborted";
                if (report.status == "Running" && !state.importedReadOnly) report.status = "Interrupted (editor closed)";
            }
            if (Active && report == null) { state.error = "批次状态丢失；请检查输出目录。"; state.phase = "RestoreFailed"; }
        }
        catch (Exception ex) { report = null; state = new State { error = ex.Message }; }
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += OnPlayMode;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        EditorApplication.quitting += BeforeQuit;
        Application.logMessageReceived += OnStartupLog;
        phaseStarted = lastTick = EditorApplication.timeSinceStartup;
    }

    public static void Start(int seed, MechanismExplorationPlan.Scope scope, float seconds,
        MechanismExplorationPlan.Scenario replay = null, bool withRegressions = true,
        string selectedMario = null, string selectedTrickster = null, string controlMode = "Automated", bool linkParent = true)
    {
        if (Active || !LevelStudioPlaySession.CanStart() || TestReportRunner.IsRunning) return;
        if (controlMode != "Automated" && controlMode != "Demonstration" && controlMode != "HumanMario" && controlMode != "HumanTrickster")
            throw new ArgumentException("Unknown control mode");
        if (controlMode != "Automated" && (replay == null || string.IsNullOrEmpty(selectedMario) || string.IsNullOrEmpty(selectedTrickster)))
            throw new ArgumentException("Rehearsal requires an explicit scenario and matchup");
        string parentDirectory = replay != null && linkParent ? ReportDirectory : null;
        if (replay != null)
        {
            replay = JsonUtility.FromJson<MechanismExplorationPlan.Scenario>(JsonUtility.ToJson(replay));
            if (!string.IsNullOrEmpty(selectedMario) && !string.IsNullOrEmpty(selectedTrickster))
                replay.selectedMatchups = new[] { new MechanismExplorationPlan.Matchup { mario = selectedMario, trickster = selectedTrickster } };
        }
        if (!SaveSourceScenes()) return;
        var scenarios = replay == null ? MechanismExplorationPlan.Create(seed, scope) : new List<MechanismExplorationPlan.Scenario> { replay };
        string fingerprint = SourceFingerprint(); // Fail before changing the coordinator state.
        state = new State { phase = withRegressions ? "RegressionQueued" : "Preparing", seconds = Mathf.Clamp(seconds, 10, 120), original = EditorSceneManager.GetSceneManagerSetup().Select(s => new SceneBookmark { path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray(),
            runInBackground = Application.runInBackground,
            directory = Path.Combine(OutputRoot, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8)) };
        report = new Report { toolRevision = "S172", controlMode = controlMode, parentReport = parentDirectory,
            confirmationPlanned = controlMode != "Automated", sourceFingerprint = fingerprint,
            planFingerprint = Hash128.Compute(string.Join("\n", scenarios.Select(s => JsonUtility.ToJson(s)))).ToString(), seed = seed, scope = replay == null ? scope.ToString() : "Replay", startedUtc = DateTime.UtcNow.ToString("O"),
            unityVersion = Application.unityVersion, fixedDeltaTime = Time.fixedDeltaTime, trialLimitSeconds = state.seconds, scenarios = scenarios,
            physicsConfigJson = ConfigJson("PhysicsConfig"), gameplayConfigJson = ConfigJson("GameplayLoopConfig"),
            unsupportedRegistry = MechanismExplorationPlan.MissingFromCatalog(AsciiElementRegistry.GetDefault().GetAllRegisteredChars()) };
        try
        {
            Directory.CreateDirectory(state.directory);
            if (!string.IsNullOrEmpty(parentDirectory) && IsReportPath(parentDirectory) && File.Exists(Path.Combine(parentDirectory, "report.json")))
            {
                File.Copy(Path.Combine(parentDirectory, "report.json"), Path.Combine(state.directory, "parent_report.json"));
                string baseline = Path.Combine(parentDirectory, "baseline_report.json");
                File.Copy(File.Exists(baseline) ? baseline : Path.Combine(parentDirectory, "report.json"), Path.Combine(state.directory, "baseline_report.json"));
            }
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
                    if (report.controlMode != "Automated") EditorApplication.ExecuteMenuItem("Window/General/Game");
                }
                else if (now - phaseStarted > 20) EndTrial("StartupFailed", "没有找到运行时 GameManager。");
            }
            else if (state.phase == "Running")
            {
                if (observer == null) { EndTrial("Interrupted", "代码重载中断了观察器，请复测此案例。"); return; }
                if (!Mathf.Approximately(Time.timeScale, 1f) &&
                    !(report.controlMode != "Automated" && GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)) observer.Finish("Interrupted", "时间倍率被改变；本轮基准无效。");
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
            controlMode = report.controlMode, profile = slot.matchup.Id, marioStrategy = slot.matchup.mario, tricksterStrategy = slot.matchup.trickster,
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
        if (report.scenarios.Any(s => s.duelVersion == 1 && !string.IsNullOrEmpty(s.parentScenarioId)))
        {
            try
            {
                string parentPath = Path.Combine(state.directory, "parent_report.json");
                report.iterationComparison = File.Exists(parentPath)
                    ? DuelIterationComparison(JsonUtility.FromJson<Report>(File.ReadAllText(parentPath)), report)
                    : "父报告缺失，不比较改善。";
            }
            catch (Exception ex) { report.iterationComparison = "对照读取失败，不判定改善：" + ex.Message; }
        }
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
        if (report == null || string.IsNullOrEmpty(state.directory) || writing || ImportedReadOnly) return;
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
        sb.AppendLine($"控制方式: {data.controlMode ?? "Automated (legacy)"}; 源码SHA256: {data.sourceFingerprint ?? "未记录"}; 计划指纹: {data.planFingerprint ?? "未记录"}");
        sb.AppendLine("父报告: " + (data.parentReport ?? "无"));
        if (!string.IsNullOrEmpty(data.iterationComparison)) sb.AppendLine(data.iterationComparison);
        foreach (var room in data.scenarios.Where(s => s.duelVersion >= 1))
        {
            sb.AppendLine($"创作迭代 {room.iteration}/2；父关卡 {room.parentScenarioId ?? "无"}；理由 {room.mutationReason ?? "初始种子方案"}\n{DuelReview(data, room)}");
            sb.AppendLine(DuelNextAction(data, room));
            sb.AppendLine(DuelReportHighlights(data, room));
        }
        sb.AppendLine($"Status: {data.status}; seed: {data.seed}; Unity: {data.unityVersion}; scope: {data.scope}");
        sb.AppendLine($"Trials recorded: {data.trials.Count} / {PlannedTrials(data)}");
        sb.AppendLine("Evidence verdict: " + EvidenceVerdict(data));
        if (data.scenarios.Any(s => s.duelVersion >= 1)) sb.AppendLine(DuelFeedbackCompleteness(data));
        sb.AppendLine($"缺失/未试玩/重复的计划槽位: {UnverifiedSlots(data)}（按场景、双方策略及首轮/确认逐项核对）");
        sb.AppendLine($"有效试玩记录: {data.trials.Count(t => t.HasGameplayEvidence)}; 首轮={data.trials.Count(t => t.attempt <= 1 && t.HasGameplayEvidence)}; 复测={data.trials.Count(t => t.attempt == 2 && t.HasGameplayEvidence)}");
        sb.AppendLine("有限反馈：最多追加 6 张问题图 × 原策略搭配 × 1 轮；机制回归每图3局，体验探索每图9局，反制专项每图3或4局。同种子同配置，不自动改难度或删除失败记录。");
        sb.AppendLine("当前代码复测策略（不回写旧批次调度；地道每图6局，真人/演示不追加确认）：无进展/超时 → 角色死亡 → 仅观察不足；最多6图，不递归。Explorer按机制选一个代表，单目标最多4秒，总预算最多8秒且不超过单局一半；到期返回普通目标导航，不证明剩余机制通过。");
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
        sb.AppendLine(TunnelDesignSummary(data));
        foreach (string note in data.playerNotes ?? new List<string>()) sb.AppendLine("玩家反馈（非自动结论）: " + note);
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
            if (trial.tunnelVersion >= 1) sb.AppendLine(DuelTrialRouteSummary(trial));
            sb.AppendLine(ProbeSummary(trial));
            sb.AppendLine(HealthSummary(trial));
            sb.AppendLine(StartTimingSummary(trial));
            sb.AppendLine(QueueSummary(trial));
            if (trial.counterplayVersion >= 1)
                sb.AppendLine($"反制专项版本={trial.counterplayVersion}；计划/实际起步等待={trial.startDelaySeconds:F2}/{trial.actualStartWaitSeconds:F2}s；拿宝/撤离时间={trial.lootAtSeconds:F2}/{trial.escapeAtSeconds:F2}s，拿宝后换点={trial.postLootTransfers}，操控受理={trial.postLootControls}。起步等待不是反应耗时。");
            sb.AppendLine(trial.nextAction);
            sb.AppendLine("控制方式: " + (trial.controlMode ?? "Automated (legacy)"));
            foreach (string feedback in trial.feedback ?? new List<string>()) sb.AppendLine("  feedback: " + feedback);
            foreach (string decision in trial.decisions ?? new List<string>()) sb.AppendLine("  sampled decision: " + decision);
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
