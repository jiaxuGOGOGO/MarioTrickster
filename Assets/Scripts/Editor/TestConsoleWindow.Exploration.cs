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
    private string explorationPlayerNote = "";
    [SerializeField] private string explorationCanvasScenarioJson = "";

    [SerializeField] private int duelSeed = 168;
    [SerializeField] private string duelDraftJson = "";
    [SerializeField] private int duelReportSelection, duelRoute;
    [SerializeField] private bool duelAdvanced;
    private string duelParsedJson;
    private MechanismExplorationPlan.Scenario duelDraft;

    private void SaveDuelDraft(MechanismExplorationPlan.Scenario room)
    {
        Undo.RecordObject(this, "Choose duel draft");
        duelDraftJson = JsonUtility.ToJson(room);
        duelParsedJson = duelDraftJson; duelDraft = room;
        Repaint();
    }

    private void DuelAction(Action action)
    {
        try { action(); }
        catch (Exception ex) { studioNotice = ex.Message; }
    }

    private static string DuelStatusLabel(string value)
    {
        switch (value)
        {
            case "Automated": return "AI完整对照";
            case "Demonstration": return "单局观战";
            case "HumanMario": return "我玩闯关者";
            case "HumanTrickster": return "我玩捣蛋者";
            case "Complete": return "已结束（不等于玩法通过）";
            case "Cleared": return "拿宝撤离";
            case "RunnerStopped": return "闯关者被阻止";
            case "TimedOut": return "超时";
            case "NoProgress": return "长时间无进展";
            case "Blocked": return "检查失败，已停止";
            case "Aborted": case "Cancelled": case "Interrupted": return "已中止，记录保留";
            case "RegressionQueued": case "Regressions": return "正在检查回归";
            case "Preparing": case "Building": return "搭建关卡";
            case "Entering": case "Booting": return "启动对局";
            case "Playing": case "Running": return "正在对战";
            case "Exiting": case "Restoring": return "保存并恢复场景";
            case "RestoreFailed": return "原场景恢复失败，请查看详细报告";
            default: return value ?? "未记录";
        }
    }

    private void DrawDuelWorkshop()
    {
        EditorGUILayout.LabelField("地表与暗线 · 对战创作", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("生成一张 → 看双方交手 / 亲自玩 → 根据结果试一个变体。保留原版，不刷乐趣分。", EditorStyles.wordWrappedLabel);
        var report = StudioExplorationRunner.Latest;
        if (StudioExplorationRunner.Active)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"正在进行 · {StudioExplorationRunner.Completed}/{StudioExplorationRunner.Total} 局", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("阶段：" + DuelStatusLabel(StudioExplorationRunner.Phase) + " / " + DuelStatusLabel(report?.controlMode), EditorStyles.wordWrappedMiniLabel);
            var current = report?.trials.LastOrDefault();
            if (current != null)
            {
                EditorGUILayout.LabelField($"{(current.marioStrategy == "SafeRoute" ? "地表绕行" : "下层读线索")} 对 {StudioExplorationRunner.DuelOpponentLabel(current.tricksterStrategy)}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{(current.lootEvents > 0 ? "已拿宝，返回左侧撤离" : "向右拿宝")} · 已观察地道到达 {current.tunnelArrivals} · 到达后出手受理 {current.controlsAfterTunnel}", EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.LabelField("切到 Game 窗口观看/操作。下方只记录你的真实感受；自动6组中会包含明确标注的无干扰基线。", EditorStyles.wordWrappedLabel);
            EditorGUILayout.BeginHorizontal();
            foreach (string tag in new[] { "有来有回", "看不懂", "只是空跑", "等待太多" })
                if (GUILayout.Button(tag)) StudioExplorationRunner.AddFeedback(tag);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("停止并保存，恢复我的场景", GUILayout.Height(30))) StudioExplorationRunner.Cancel();
            duelAdvanced = EditorGUILayout.Foldout(duelAdvanced, "AI详细意图 / 技术状态");
            if (duelAdvanced) EditorGUILayout.LabelField(StudioExplorationRunner.LiveIntent, EditorStyles.wordWrappedMiniLabel);
            return;
        }
        bool unavailable = EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || TestReportRunner.IsRunning;
        if (duelParsedJson != duelDraftJson)
        {
            duelParsedJson = duelDraftJson;
            DuelAction(() => duelDraft = string.IsNullOrEmpty(duelDraftJson) ? null : JsonUtility.FromJson<MechanismExplorationPlan.Scenario>(duelDraftJson));
        }
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("1  生成关卡", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(unavailable))
        {
            duelSeed = EditorGUILayout.IntField("关卡种子（可复制）", duelSeed);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("按这个种子生成")) SaveDuelDraft(MechanismExplorationPlan.BuildDuel(duelSeed));
            if (GUILayout.Button("换个种子生成"))
            { duelSeed = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0); SaveDuelDraft(MechanismExplorationPlan.BuildDuel(duelSeed)); }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.LabelField("种子改变长度、台阶和机关位置。同种子同版本可复现；有限规则生成，不保证每个种子都不同或好玩。", EditorStyles.wordWrappedMiniLabel);
        if (duelDraft != null)
        {
            EditorGUILayout.LabelField($"当前草稿 · 种子 {duelDraft.seed} · 变体 {duelDraft.iteration}/2", EditorStyles.boldLabel);
            DrawDuelMap(duelDraft);
            EditorGUILayout.LabelField(duelDraft.designQuestion, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("地表可绕行，下层可抢近路；紫线是捣蛋者附身后的原生暗线，不是自由挖土。", EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("2  让双方交手", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("以下路线选择只影响单局演示/真人；完整6组自动包含地表和下层。", EditorStyles.wordWrappedMiniLabel);
        duelRoute = GUILayout.Toolbar(duelRoute, new[] { "下层：读线索 / 扫描", "地表：爬高绕行" });
        EditorGUILayout.LabelField("默认是主动地道对手，会按观察尝试拦截或准备换层；不保证换位成功。仍受真实门禁、能量和预警约束。", EditorStyles.wordWrappedMiniLabel);
        explorationRegressions = EditorGUILayout.ToggleLeft("运行前先检查全部回归（新版本建议保留）", explorationRegressions);
        using (new EditorGUI.DisabledScope(unavailable || duelDraft == null))
        {
            if (GUILayout.Button("看一局地道对抗（不是无对手折返）", GUILayout.Height(32)))
                DuelAction(() => StartDuelDraft("Demonstration"));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("我玩闯关者")) DuelAction(() => StartDuelDraft("HumanMario"));
            if (GUILayout.Button("我玩捣蛋者")) DuelAction(() => StartDuelDraft("HumanTrickster"));
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("让AI完整对照这张图（6组，之后才能试变体）", GUILayout.Height(30)))
                DuelAction(() => StudioExplorationRunner.Start(duelDraft.seed, MechanismExplorationPlan.Scope.TunnelDuel, 60, duelDraft, explorationRegressions, linkParent: DuelDraftHasParent));
        }
        EditorGUILayout.LabelField("完整对照：两条路线各对无干扰 / 地面追击 / 地道换位；首轮6局，最多6局同条件确认。单局演示和真人不会补算覆盖。", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("3  看结果，只改一处再比较", EditorStyles.boldLabel);
        var rooms = report?.scenarios.Where(s => s.tunnelVersion >= 1).ToArray();
        if (rooms == null || rooms.Length == 0) EditorGUILayout.LabelField("还没有地道对战报告。先生成，再试玩。", EditorStyles.wordWrappedLabel);
        else
        {
            duelReportSelection = Mathf.Clamp(duelReportSelection, 0, rooms.Length - 1);
            duelReportSelection = EditorGUILayout.Popup("上次报告的关卡", duelReportSelection, rooms.Select(s => $"种子{s.seed} · {s.designQuestion}").ToArray());
            var room = rooms[duelReportSelection];
            EditorGUILayout.LabelField($"报告：{report.toolRevision} / {DuelStatusLabel(report.controlMode)} / {DuelStatusLabel(report.status)}；回归 {report.regressionPassed} 通过 / {report.regressionFailed} 失败", EditorStyles.wordWrappedMiniLabel);
            if (duelDraft == null || duelDraft.id != room.id || duelDraft.ascii != room.ascii)
                EditorGUILayout.HelpBox("这份报告不是上方当前草稿的结果。可加载报告原图；不会把旧结果算给新种子。", MessageType.Info);
            EditorGUILayout.HelpBox(StudioExplorationRunner.DuelFeedbackCompleteness(report), MessageType.Info);
            EditorGUILayout.HelpBox(StudioExplorationRunner.DuelReview(report, room), MessageType.Info);
            foreach (var t in report.trials.Where(t => t.scenarioId == room.id))
            {
                EditorGUILayout.LabelField($"{(t.attempt <= 1 ? "首轮" : "确认")} / 计划{(t.marioStrategy == "SafeRoute" ? "地表" : "下层")} / {StudioExplorationRunner.DuelOpponentLabel(t.tricksterStrategy)}：{DuelStatusLabel(t.outcome)}，{t.seconds:F1}秒；地道到达{t.tunnelArrivals}，3秒内出手{t.controlsAfterTunnel}", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(StudioExplorationRunner.DuelTrialRouteSummary(t), EditorStyles.wordWrappedMiniLabel);
            }
            if (!string.IsNullOrEmpty(report.iterationComparison)) EditorGUILayout.HelpBox(report.iterationComparison, MessageType.Info);
            EditorGUILayout.LabelField("先看报告中的真实路线，再决定是否改图。以下演示直接用报告原图，不会误用上方草稿；按当前代码重跑，不是录像。", EditorStyles.wordWrappedLabel);
            using (new EditorGUI.DisabledScope(unavailable))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("看报告原图：下层对战")) DuelAction(() => StartReportedDuel(room, "Adaptive"));
                if (GUILayout.Button("看报告原图：地表对战")) DuelAction(() => StartReportedDuel(room, "SafeRoute"));
                EditorGUILayout.EndHorizontal();
            }
            string blocked = StudioExplorationRunner.IterationBlockReason(report, room);
            EditorGUILayout.LabelField(blocked.Length > 0 ? blocked : "下一次只换暗线连接：原图、物理、伤害和AI策略保持不变。不会自动宣称更好玩。", EditorStyles.wordWrappedLabel);
            using (new EditorGUI.DisabledScope(unavailable || blocked.Length > 0))
                if (GUILayout.Button("根据这份对战结果，试一个连接变体并再对战", GUILayout.Height(32)))
                    DuelAction(() => StudioExplorationRunner.StartDuelIteration(room, 60, explorationRegressions));
            using (new EditorGUI.DisabledScope(unavailable))
            {
                if (GUILayout.Button("把报告原图载入为当前草稿（保留原种子）"))
                {
                    var copy = JsonUtility.FromJson<MechanismExplorationPlan.Scenario>(JsonUtility.ToJson(room));
                    copy.selectedMatchups = Array.Empty<MechanismExplorationPlan.Matchup>();
                    SaveDuelDraft(copy); duelSeed = room.seed;
                }
                if (!string.IsNullOrEmpty(report.parentReport) && GUILayout.Button("查看父版本结果 / 返回上一批"))
                { DuelAction(StudioExplorationRunner.LoadParentReport); GUIUtility.ExitGUI(); }
            }
            explorationPlayerNote = EditorGUILayout.TextField("我的感受 / 新道具想法", explorationPlayerNote);
            if (GUILayout.Button("保存感受或机制提案") && !string.IsNullOrWhiteSpace(explorationPlayerNote))
            { StudioExplorationRunner.AddFeedback(explorationPlayerNote); explorationPlayerNote = ""; }
            if (report.playerNotes != null)
                foreach (string note in report.playerNotes.Skip(Math.Max(0, report.playerNotes.Count - 3)))
                    EditorGUILayout.LabelField(note, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("新道具提案与玩法实现分开：先说明它解决哪种单调局面、对手如何识别和反制，再实现与回归。当前不会自动写新机制代码。", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("打包反馈ZIP，发给AI继续设计"))
                DuelAction(() => EditorUtility.RevealInFinder(StudioExplorationRunner.ExportFeedbackZip()));
        }
        EditorGUILayout.EndVertical();
        if (!string.IsNullOrEmpty(studioNotice)) EditorGUILayout.HelpBox(studioNotice, MessageType.Info);
        if (!string.IsNullOrEmpty(StudioExplorationRunner.Error)) EditorGUILayout.HelpBox(StudioExplorationRunner.Error, MessageType.Warning);
        duelAdvanced = EditorGUILayout.Foldout(duelAdvanced, "高级：旧测试矩阵 / 完整技术报告");
        if (duelAdvanced) DrawExplorationPanel();
    }

    private void StartReportedDuel(MechanismExplorationPlan.Scenario room, string runner)
    {
        StudioExplorationRunner.Start(room.seed, MechanismExplorationPlan.Scope.TunnelDuel, 60,
            room, explorationRegressions, runner, "TunnelChaser", "Demonstration");
    }

    private bool DuelDraftHasParent => duelDraft != null && StudioExplorationRunner.Latest != null &&
        StudioExplorationRunner.Latest.scenarios.Any(s => s.id == duelDraft.id && s.ascii == duelDraft.ascii);

    private void StartDuelDraft(string mode)
    {
        if (duelDraft == null) return;
        StudioExplorationRunner.Start(duelDraft.seed, MechanismExplorationPlan.Scope.TunnelDuel, mode == "Demonstration" ? 60 : 120,
            duelDraft, explorationRegressions, duelRoute == 1 ? "SafeRoute" : "Adaptive", "TunnelChaser", mode, DuelDraftHasParent);
    }

    private void DrawDuelMap(MechanismExplorationPlan.Scenario room)
    {
        if (!LevelStudioDocument.TryParse(room.ascii, out var doc, out _)) return;
        float size = Mathf.Clamp((position.width - 54f) / doc.Width, 3f, 16f);
        Rect rect = GUILayoutUtility.GetRect(doc.Width * size, doc.Height * size, GUILayout.ExpandWidth(false));
        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.10f, 0.15f));
            for (int y = 0; y < doc.Height; y++)
            for (int x = 0; x < doc.Width; x++)
            {
                char c = doc.Cell(x, y);
                if (c == '.') continue;
                Color color = c == '#' || c == '-' ? new Color(0.32f, 0.40f, 0.45f) : c == 'M' || c == 'G' ? Color.cyan : c == 'o' ? Color.yellow : new Color(1f, 0.55f, 0.35f);
                EditorGUI.DrawRect(new Rect(rect.x + x * size, rect.y + (doc.Height - 1 - y) * size, size - 1, size - 1), color);
            }
            Handles.BeginGUI();
            Color previous = Handles.color; Handles.color = new Color(0.78f, 0.48f, 1f);
            foreach (var link in room.tunnelLinks ?? Array.Empty<MechanismExplorationPlan.TunnelLink>())
            {
                if (link?.from == null || link.to == null) continue;
                if (link.from.x > link.to.x || (link.from.x == link.to.x && link.from.y > link.to.y)) continue;
                Handles.DrawLine(new Vector3(rect.x + (link.from.x + 0.5f) * size, rect.y + (doc.Height - 0.5f - link.from.y) * size),
                    new Vector3(rect.x + (link.to.x + 0.5f) * size, rect.y + (doc.Height - 0.5f - link.to.y) * size));
            }
            Handles.color = previous; Handles.EndGUI();
        }
        EditorGUILayout.LabelField("蓝：起点/撤离　黄：宝物　橙：捣蛋者/机关　紫线：暗线连接（作者预览，不是玩家透视）", EditorStyles.wordWrappedMiniLabel);
    }

    private void DrawExplorationPanel()
    {
        showExploration = EditorGUILayout.Foldout(showExploration || StudioExplorationRunner.Active, "AI 自动测试：地道博弈 / 机制与体验", true);
        if (!showExploration) return;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (StudioExplorationRunner.Active)
        {
            EditorGUILayout.LabelField($"{StudioExplorationRunner.TestTrack(StudioExplorationRunner.Latest)} · {StudioExplorationRunner.Phase} · {StudioExplorationRunner.Completed}/{StudioExplorationRunner.Total} 局", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(StudioExplorationRunner.LiveIntent, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("控制方式：" + StudioExplorationRunner.Latest?.controlMode + "。每局完整重建；真人按原操作键，停止请用下方按钮。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.BeginHorizontal();
            foreach (string tag in new[] { "这里有意思", "看不懂线索", "只是在等待", "想换个办法" })
                if (GUILayout.Button(tag)) StudioExplorationRunner.AddFeedback(tag);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("停止批次并恢复原场景")) StudioExplorationRunner.Cancel();
        }
        else
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || TestReportRunner.IsRunning))
            {
                explorationSeed = EditorGUILayout.IntField("随机种子", explorationSeed);
                int selectedPath = explorationScope == MechanismExplorationPlan.Scope.TunnelDuel ? 3 : explorationScope == MechanismExplorationPlan.Scope.Counterplay ? 2 : explorationScope == MechanismExplorationPlan.Scope.Experience ? 1 : 0;
                int path = GUILayout.Toolbar(selectedPath, new[] { "机制回归", "体验探索", "反制专项", "地道博弈" });
                if (path == 3) { explorationScope = MechanismExplorationPlan.Scope.TunnelDuel; if (selectedPath != 3) { explorationSeed = 166; explorationLimit = 60; explorationIssuesOnly = false; } }
                else if (path == 2) { explorationScope = MechanismExplorationPlan.Scope.Counterplay; if (selectedPath != 2) { explorationSeed = 154; explorationLimit = 45; } }
                else if (path == 1) explorationScope = MechanismExplorationPlan.Scope.Experience;
                else explorationScope = (MechanismExplorationPlan.Scope)EditorGUILayout.Popup("回归范围", (int)explorationScope > 2 ? 0 : (int)explorationScope,
                    new[] { "快速机制验证（6 场景 × 3 画像）", "全部 19 机制 + 邻接组合（38 × 3）", "全部两两共现（190 × 3）" });
                bool tunnel = explorationScope == MechanismExplorationPlan.Scope.TunnelDuel;
                bool diagnostic = explorationScope == MechanismExplorationPlan.Scope.Counterplay;
                bool experience = explorationScope == MechanismExplorationPlan.Scope.Experience;
                if (tunnel) EditorGUILayout.HelpBox("3种地道样板：串联换位 / 上层出口 / 首尾回包。下路与上路各对照静止、地面追击、暗线追击，共18局，最多一轮18局确认。复用原生暗线，不改伤害或物理。报告可直接启动单局演示或你操作一方，并保存体验标记。不是自主学习或乐趣评分。", MessageType.Info);
                else if (diagnostic) EditorGUILayout.HelpBox("21局首轮：第二房冲刺/读公开窗口/上路，对照静止对手；拿宝房Adaptive/上路，各对照静止对手与Chaser。每组普通起步等待0/0.6/1.2秒。自动比较穿越、伤害线索和返程耗时，不输出乐趣评分。", MessageType.Info);
                else if (experience) EditorGUILayout.HelpBox("三类短房：短路/绕行、诱导/后摇、拿宝/返程。Mario 抢进度/侦察/绕路 × Trickster 伏击/诱敌近身/换点追击，共9种搭配。路线有作者引导，不是AI学习；真正的乐趣仍由你试玩确认。", MessageType.Info);
                else EditorGUILayout.HelpBox("当前是机制回归，不包含三类体验房与九种独立策略。验证修复后，切换上方“体验探索”再运行一批；两份报告分别保留。", MessageType.Info);
                EditorGUILayout.LabelField(tunnel ? "当前将运行：地道博弈 / TunnelDuel / 首轮18局" : diagnostic ? "当前将运行：反制专项 / Counterplay / 首轮21局" : experience ? "当前将运行：体验探索 / Experience / 首轮27局" : $"当前将运行：机制回归 / {explorationScope}", EditorStyles.boldLabel);
                explorationLimit = EditorGUILayout.Slider("单局预算（秒）", explorationLimit, 10, 120);
                explorationRegressions = EditorGUILayout.ToggleLeft("先自动运行全部 EditMode / PlayMode 回归（无中途弹窗）", explorationRegressions);
                if (explorationRegressions) EditorGUILayout.LabelField("回归失败或没有通过记录时，将保存报告、停止跑图并恢复原场景。", EditorStyles.wordWrappedMiniLabel);
                int rooms = tunnel ? 3 : diagnostic ? 6 : experience ? 3 : explorationScope == MechanismExplorationPlan.Scope.Smoke ? 6 : explorationScope == MechanismExplorationPlan.Scope.Mechanisms ? 38 : 190;
                int matchups = tunnel ? 6 : experience ? 9 : 3;
                int firstPass = diagnostic ? 21 : rooms * matchups;
                int confirmations = diagnostic ? 21 : Math.Min(rooms, MechanismExplorationPlan.MaxConfirmationScenes) * matchups;
                EditorGUILayout.LabelField($"首轮 {firstPass} 局 + 最多 {confirmations} 局问题图确认；按预算上限约 {(firstPass + confirmations) * explorationLimit / 60:F0} 分钟 + 重载/回归。相同基础故障连续两局会停批。可随时停止。", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button(tunnel ? "开始地道博弈：自动搭建、对照试玩、保留报告" : diagnostic ? "开始反制专项：公开窗口 / 上下路 / 返程追击对照" : experience ? "开始体验探索：3 类房间 × 9 种策略搭配" : "开始机制回归：验证机制与导航修复", GUILayout.Height(34)))
                {
                    if (rooms < 100 || EditorUtility.DisplayDialog("长时间组合覆盖", "两两共现计划包含 570 局，可能运行数小时。它仍不是所有时序与参数的穷举。是否开始？", "开始", "取消"))
                        StudioExplorationRunner.Start(explorationSeed, explorationScope, explorationLimit, null, explorationRegressions);
                }
                if (GUILayout.Button("生成当前路径的一张示例到画布（可继续修改）") && ConfirmStudioReplace())
                {
                    var scenario = MechanismExplorationPlan.Create(explorationSeed, tunnel ? MechanismExplorationPlan.Scope.TunnelDuel : diagnostic ? MechanismExplorationPlan.Scope.Counterplay : experience ? MechanismExplorationPlan.Scope.Experience : MechanismExplorationPlan.Scope.Smoke).Last();
                    SetStudioText(scenario.ascii, "Generate mechanism combination");
                    explorationCanvasScenarioJson = scenario.tunnelVersion >= 1 ? JsonUtility.ToJson(scenario) : "";
                    studioNotice = $"种子 {explorationSeed}，机制 {scenario.mechanisms}。{scenario.intention} 画布只保留布局；完整通道/拿宝语义通过报告的“亲自试玩”复现。";
                    explorationSeed = unchecked(explorationSeed + 1);
                }
                if (!string.IsNullOrEmpty(explorationCanvasScenarioJson))
                {
                    EditorGUILayout.LabelField("地道画布微调保留原暗线坐标与作者路点；若移动出口或台阶，请回传方案由AI重接，不会静默猜线。", EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button("把当前画布作为新变体：保留暗线并跑6组对照"))
                    {
                        var edited = JsonUtility.FromJson<MechanismExplorationPlan.Scenario>(explorationCanvasScenarioJson);
                        edited.ascii = customAsciiTemplate;
                        edited.id += "_edit_" + Hash128.Compute(edited.ascii).ToString().Substring(0, 8);
                        edited.selectedMatchups = Array.Empty<MechanismExplorationPlan.Matchup>();
                        edited.designQuestion += "（玩家画布微调，原作者路点保留）";
                        string validation = ExplorationSceneBuilder.Validate(edited, out bool invalid);
                        if (invalid) studioNotice = validation;
                        else StudioExplorationRunner.Start(edited.seed, MechanismExplorationPlan.Scope.TunnelDuel, explorationLimit, edited, explorationRegressions);
                    }
                }
            }
        }
        if (!string.IsNullOrEmpty(StudioExplorationRunner.Error)) EditorGUILayout.HelpBox(StudioExplorationRunner.Error, MessageType.Warning);
        var report = StudioExplorationRunner.Latest;
        if (report != null)
        {
            EditorGUILayout.LabelField($"报告路径：{StudioExplorationRunner.TestTrack(report)} / {report.scope} · 批次：{report.status} · 回归：{report.regressions}（通过 {report.regressionPassed} / 失败 {report.regressionFailed}）", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.HelpBox(StudioExplorationRunner.EvidenceVerdict(report), MessageType.Info);
            if (report.scenarios.Any(s => s.counterplayVersion >= 1))
                EditorGUILayout.LabelField(StudioExplorationRunner.CounterplayPairs(report), EditorStyles.wordWrappedMiniLabel);
            if (StudioExplorationRunner.TestTrack(report) == "机制回归")
                EditorGUILayout.LabelField("这份报告不包含体验房。请切换上方“体验探索”并点击“开始体验探索”。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField($"有效试玩 {report.trials.Count(t => t.HasGameplayEvidence)} / 记录 {report.trials.Count}；复测不覆盖首轮失败。", EditorStyles.wordWrappedMiniLabel);
            if (report.scenarios.Any(s => s.tunnelVersion >= 1))
                EditorGUILayout.LabelField(StudioExplorationRunner.TunnelDesignSummary(report), EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("打开本批报告目录")) EditorUtility.RevealInFinder(StudioExplorationRunner.ReportDirectory);
            explorationPlayerNote = EditorGUILayout.TextField("我的体验 / 机制想法", explorationPlayerNote);
            if (GUILayout.Button("保存这条反馈到本批报告") && !string.IsNullOrWhiteSpace(explorationPlayerNote))
            { StudioExplorationRunner.AddFeedback(explorationPlayerNote); explorationPlayerNote = ""; }
            using (new EditorGUI.DisabledScope(StudioExplorationRunner.Active))
                if (GUILayout.Button("打包反馈 ZIP（完成或停止后，直接回传）"))
                {
                    try { EditorUtility.RevealInFinder(StudioExplorationRunner.ExportFeedbackZip()); }
                    catch (Exception ex) { studioNotice = "打包失败：" + ex.Message; Debug.LogException(ex); }
                }
            if (!StudioExplorationRunner.Active)
            {
                if (!string.IsNullOrEmpty(report.parentReport) && GUILayout.Button("返回上一批报告（保留本次试玩）"))
                {
                    try { StudioExplorationRunner.LoadParentReport(); }
                    catch (Exception ex) { studioNotice = ex.Message; }
                    GUIUtility.ExitGUI();
                }
                explorationIssuesOnly = EditorGUILayout.ToggleLeft("只看失败或最低观察不足的案例（取消可查看全部专项待验项）", explorationIssuesOnly);
                var trials = report.trials.Where(t => !explorationIssuesOnly || !t.CandidateForHumanPlay || StudioExplorationRunner.ExperienceIssues(report, t).Length > 0).ToArray();
                if (trials.Length > 0)
                {
                    explorationSelection = Mathf.Clamp(explorationSelection, 0, trials.Length - 1);
                    explorationSelection = EditorGUILayout.Popup("复测案例", explorationSelection,
                        trials.Select(t => $"{t.scenarioId} / {t.profile} / 第{Math.Max(1, t.attempt)}轮 / {t.outcome}").ToArray());
                    var trial = trials[explorationSelection];
                    if (trial.tricksterStrategy == "Passive")
                        EditorGUILayout.HelpBox("当前是无干扰基线：对手不行动。它只展示拿宝/路线，不展示地道博弈。", MessageType.Warning);
                    foreach (var gap in StudioExplorationRunner.ExperienceIssues(report, trial)) EditorGUILayout.HelpBox(gap, MessageType.Warning);
                    EditorGUILayout.LabelField($"完整作者路线：{string.Join(", ", trial.completedRoutes ?? new System.Collections.Generic.List<string>())}；近距附身就绪 {trial.armedNearbySeconds:F2}s；换点请求 {trial.anchorSwitchRequests} / 成功 {trial.possessionTransfers}", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField(trial.scanEvidenceVersion >= 1
                        ? $"扫描施放 {trial.scans} / 命中 {trial.scanHits} / 未命中 {trial.scanMisses}；命中不等于已证明避免伤害。"
                        : "旧记录未采集扫描结果，不能从施放数补算命中。", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField(StudioExplorationRunner.ProbeSummary(trial), EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField(StudioExplorationRunner.HealthSummary(trial), EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField(StudioExplorationRunner.QueueSummary(trial), EditorStyles.wordWrappedMiniLabel);
                    foreach (var hammer in trial.coverage.Where(e => e.mechanism == "P"))
                        EditorGUILayout.LabelField(hammer.movingPartEvidenceVersion >= 1
                            ? $"P 根实例 {hammer.built}（锤头转发器不重复计数）；真实锤头接触 {hammer.runnerMovingPartContacts}，不等于扣血或安全通过。"
                            : "旧记录未采集独立锤头接触，不能从根接触补算。", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField(trial.nextAction, EditorStyles.wordWrappedMiniLabel);
                    if (!string.IsNullOrEmpty(trial.comparison)) EditorGUILayout.LabelField(trial.comparison, EditorStyles.wordWrappedMiniLabel);
                    foreach (var e in trial.coverage)
                        EditorGUILayout.LabelField($"{e.mechanism}: {e.Status}（Mario接触 {e.runnerContacts} / 操控受理 {e.controlsAccepted} / Mario效果事件 {e.runnerEffects} / 旧混合计数 {e.activations}）\n专项验收：{MechanismExplorationPlan.BehaviorRequirement(e.mechanism)}", EditorStyles.wordWrappedMiniLabel);
                    var scenario = report.scenarios.FirstOrDefault(s => s.id == trial.scenarioId);
                    if (scenario != null) EditorGUILayout.LabelField(scenario.intention, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField($"目标阶段：{trial.objectivePhase}；实际进入路线：{string.Join(", ", trial.routesUsed ?? new System.Collections.Generic.List<string>())}", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField($"换路请求 {trial.routeSwitchRequests} / 实际路线切换 {trial.routeTransitions}；预警退让 {trial.telegraphRetreats} / 后摇穿越 {trial.recoveryCrossings} / 换点附身 {trial.possessionTransfers}", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField($"顶部落点请求 {trial.bounceLandingAttempts} / Mario实际弹射 {trial.runnerBounceLaunches}（只计发射事件）", EditorStyles.wordWrappedMiniLabel);
                    using (new EditorGUI.DisabledScope(scenario == null || EditorApplication.isPlayingOrWillChangePlaymode || TestReportRunner.IsRunning))
                    {
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("AI 重测此场景（原策略搭配）"))
                            StudioExplorationRunner.Start(scenario.seed, MechanismExplorationPlan.Scope.Smoke, explorationLimit, scenario, false,
                                trial.marioStrategy ?? trial.profile, trial.tricksterStrategy ?? trial.profile);
                        if (GUILayout.Button("看这一局 AI 演示（独立记录）"))
                            StartExplorationRehearsal(scenario, trial, "Demonstration");
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("我玩闯关者（自动保存反馈）")) StartExplorationRehearsal(scenario, trial, "HumanMario");
                        if (GUILayout.Button("我玩捣蛋者（自动保存反馈）")) StartExplorationRehearsal(scenario, trial, "HumanTrickster");
                        EditorGUILayout.EndHorizontal();
                        if (GUILayout.Button("载入此案例布局到画布") && ConfirmStudioReplace())
                        {
                            SetStudioText(scenario.ascii, "Load exploration scenario");
                            explorationCanvasScenarioJson = scenario.tunnelVersion >= 1 ? JsonUtility.ToJson(scenario) : "";
                        }
                    }
                }
            }
        }
        EditorGUILayout.LabelField("生成 ≠ 接触 ≠ 激活 ≠ 正确 ≠ 好玩。未覆盖项不会算通过；报告不是乐趣评分。", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
    }

    private void StartExplorationRehearsal(MechanismExplorationPlan.Scenario scenario, MechanismExplorationPlan.Trial trial, string mode)
    {
        // A fresh child report includes the parent report; never overwrite the automated baseline.
        StudioExplorationRunner.Start(scenario.seed, MechanismExplorationPlan.Scope.TunnelDuel,
            mode == "Demonstration" ? explorationLimit : 120f, scenario, false,
            trial.marioStrategy ?? trial.profile, trial.tricksterStrategy ?? trial.profile, mode);
    }
}
