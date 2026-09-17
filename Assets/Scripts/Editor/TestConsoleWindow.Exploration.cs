using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public partial class TestConsoleWindow
{
    [SerializeField] private bool showExploration = true;
    [SerializeField] private int explorationSeed = 153;
    [SerializeField] private MechanismExplorationPlan.Scope explorationScope;
    [SerializeField] private float explorationLimit = 30;
    [SerializeField] private bool explorationRegressions = true;
    [SerializeField] private int explorationSelection;
    private bool explorationIssuesOnly = true;

    private void DrawExplorationPanel()
    {
        showExploration = EditorGUILayout.Foldout(showExploration || StudioExplorationRunner.Active, "AI 自动测试：机制回归 / 体验探索", true);
        if (!showExploration) return;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (StudioExplorationRunner.Active)
        {
            EditorGUILayout.LabelField($"{StudioExplorationRunner.Phase} · {StudioExplorationRunner.Completed}/{StudioExplorationRunner.Total} 局", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(StudioExplorationRunner.LiveIntent, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("双方由 AI 控制；每局完整重建，不修改人物物理/冷却。回归阶段的停止请求会等待 Test Runner 安全结束。", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("停止批次并恢复原场景")) StudioExplorationRunner.Cancel();
        }
        else
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || TestReportRunner.IsRunning))
            {
                explorationSeed = EditorGUILayout.IntField("随机种子", explorationSeed);
                int path = GUILayout.Toolbar(explorationScope == MechanismExplorationPlan.Scope.Experience ? 1 : 0, new[] { "机制回归", "体验探索" });
                if (path == 1) explorationScope = MechanismExplorationPlan.Scope.Experience;
                else explorationScope = (MechanismExplorationPlan.Scope)EditorGUILayout.Popup("回归范围", explorationScope == MechanismExplorationPlan.Scope.Experience ? 0 : (int)explorationScope,
                    new[] { "快速机制验证（6 场景 × 3 画像）", "全部 19 机制 + 邻接组合（38 × 3）", "全部两两共现（190 × 3）" });
                bool experience = explorationScope == MechanismExplorationPlan.Scope.Experience;
                if (experience) EditorGUILayout.HelpBox("三类短房：短路/绕行、诱导/后摇、拿宝/返程。Mario 抢进度/侦察/绕路 × Trickster 伏击/诱敌近身/换点追击，共9种搭配。路线有作者引导，不是AI学习；真正的乐趣仍由你试玩确认。", MessageType.Info);
                explorationLimit = EditorGUILayout.Slider("单局预算（秒）", explorationLimit, 10, 120);
                explorationRegressions = EditorGUILayout.ToggleLeft("先自动运行全部 EditMode / PlayMode 回归（无中途弹窗）", explorationRegressions);
                int rooms = experience ? 3 : explorationScope == MechanismExplorationPlan.Scope.Smoke ? 6 : explorationScope == MechanismExplorationPlan.Scope.Mechanisms ? 38 : 190;
                int matchups = experience ? 9 : 3;
                int confirmations = Math.Min(rooms, MechanismExplorationPlan.MaxConfirmationScenes) * matchups;
                EditorGUILayout.LabelField($"首轮 {rooms * matchups} 局 + 最多 {confirmations} 局问题图确认；按预算上限约 {(rooms * matchups + confirmations) * explorationLimit / 60:F0} 分钟 + 重载/回归。相同基础故障连续两局会停批。可随时停止。", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("一键开始：自动回归 → 随机搭建 → 双 AI 试玩 → 报告", GUILayout.Height(34)))
                {
                    if (rooms < 100 || EditorUtility.DisplayDialog("长时间组合覆盖", "两两共现计划包含 570 局，可能运行数小时。它仍不是所有时序与参数的穷举。是否开始？", "开始", "取消"))
                        StudioExplorationRunner.Start(explorationSeed, explorationScope, explorationLimit, null, explorationRegressions);
                }
                if (GUILayout.Button("生成当前路径的一张示例到画布（可继续修改）") && ConfirmStudioReplace())
                {
                    var scenario = MechanismExplorationPlan.Create(explorationSeed, experience ? MechanismExplorationPlan.Scope.Experience : MechanismExplorationPlan.Scope.Smoke).Last();
                    SetStudioText(scenario.ascii, "Generate mechanism combination");
                    studioNotice = $"种子 {explorationSeed}，机制 {scenario.mechanisms}。{scenario.intention} 画布只保留布局；完整通道/拿宝语义通过报告的“亲自试玩”复现。";
                    explorationSeed = unchecked(explorationSeed + 1);
                }
            }
        }
        if (!string.IsNullOrEmpty(StudioExplorationRunner.Error)) EditorGUILayout.HelpBox(StudioExplorationRunner.Error, MessageType.Warning);
        var report = StudioExplorationRunner.Latest;
        if (report != null)
        {
            EditorGUILayout.LabelField($"批次：{report.status} · 回归：{report.regressions}（通过 {report.regressionPassed} / 失败 {report.regressionFailed}）", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.HelpBox(StudioExplorationRunner.EvidenceVerdict(report), MessageType.Info);
            EditorGUILayout.LabelField($"有效试玩 {report.trials.Count(t => t.HasGameplayEvidence)} / 记录 {report.trials.Count}；复测不覆盖首轮失败。", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("打开本批报告目录")) EditorUtility.RevealInFinder(StudioExplorationRunner.ReportDirectory);
            if (!StudioExplorationRunner.Active)
            {
                explorationIssuesOnly = EditorGUILayout.ToggleLeft("只看失败或交互覆盖不足的案例", explorationIssuesOnly);
                var trials = report.trials.Where(t => !explorationIssuesOnly || !t.CandidateForHumanPlay).ToArray();
                if (trials.Length > 0)
                {
                    explorationSelection = Mathf.Clamp(explorationSelection, 0, trials.Length - 1);
                    explorationSelection = EditorGUILayout.Popup("复测案例", explorationSelection,
                        trials.Select(t => $"{t.scenarioId} / {t.profile} / 第{Math.Max(1, t.attempt)}轮 / {t.outcome}").ToArray());
                    var trial = trials[explorationSelection];
                    EditorGUILayout.LabelField(trial.nextAction, EditorStyles.wordWrappedMiniLabel);
                    if (!string.IsNullOrEmpty(trial.comparison)) EditorGUILayout.LabelField(trial.comparison, EditorStyles.wordWrappedMiniLabel);
                    foreach (var e in trial.coverage)
                        EditorGUILayout.LabelField($"{e.mechanism}: {e.Status}（接触 {e.contacts} / 激活 {e.activations}）", EditorStyles.wordWrappedMiniLabel);
                    var scenario = report.scenarios.FirstOrDefault(s => s.id == trial.scenarioId);
                    if (scenario != null) EditorGUILayout.LabelField(scenario.intention, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField($"目标阶段：{trial.objectivePhase}；实际进入路线：{string.Join(", ", trial.routesUsed ?? new System.Collections.Generic.List<string>())}", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField($"换路请求 {trial.routeSwitchRequests} / 实际路线切换 {trial.routeTransitions}；预警退让 {trial.telegraphRetreats} / 后摇穿越 {trial.recoveryCrossings} / 换点附身 {trial.possessionTransfers}", EditorStyles.wordWrappedMiniLabel);
                    using (new EditorGUI.DisabledScope(scenario == null || EditorApplication.isPlayingOrWillChangePlaymode || TestReportRunner.IsRunning))
                    {
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("AI 重测此场景（原策略搭配）"))
                            StudioExplorationRunner.Start(scenario.seed, MechanismExplorationPlan.Scope.Smoke, explorationLimit, scenario, false);
                        if (GUILayout.Button("我亲自试玩此场景")) PlayExplorationScenario(scenario);
                        EditorGUILayout.EndHorizontal();
                        if (GUILayout.Button("载入此案例布局到画布") && ConfirmStudioReplace())
                            SetStudioText(scenario.ascii, "Load exploration scenario");
                    }
                }
            }
        }
        EditorGUILayout.LabelField("生成 ≠ 接触 ≠ 激活 ≠ 正确 ≠ 好玩。未覆盖项不会算通过；报告不是乐趣评分。", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
    }

    private void PlayExplorationScenario(MechanismExplorationPlan.Scenario scenario)
    {
        if (!LevelStudioPlaySession.CanStart() || !StudioExplorationRunner.SaveSourceScenes()) return;
        try
        {
            ExplorationSceneBuilder.Build(scenario, explorationLimit, false);
            LevelStudioPlaySession.Begin("exploration:" + scenario.id, studioRole);
        }
        catch (Exception ex) { studioNotice = "复现失败：" + ex.Message; Debug.LogException(ex); }
    }
}
