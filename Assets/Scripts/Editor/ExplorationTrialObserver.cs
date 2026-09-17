using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Read-only evidence collection plus ordinary bot input injection; no teleport, god mode or forced activation.</summary>
public sealed class ExplorationTrialObserver : IDisposable
{
    private readonly MechanismExplorationPlan.Trial result;
    private readonly GameManager manager;
    private readonly InputManager input;
    private readonly IInputProvider previousInput;
    private readonly MarioController mario;
    private readonly TricksterAbilitySystem ability;
    private readonly ScanAbility scan;
    private readonly TricksterPossessionGate gate;
    private readonly PropComboTracker combo;
    private readonly RouteBudgetService routes;
    private readonly Dictionary<string, Transform[]> targets = new Dictionary<string, Transform[]>();
    private readonly BotPersonaConfigSO marioProfile, tricksterProfile;
    private readonly GuidedBot bot;
    private float sampleTimer, noProgressTimer, bestDistance = float.MaxValue;
    private readonly float goalX;
    private bool disposed;
    public bool Finished => result.outcome != "Running";
    public string Intent => bot.MarioIntent + " / " + bot.TricksterIntent;

    public ExplorationTrialObserver(MechanismExplorationPlan.Scenario scenario, MechanismExplorationPlan.Trial trial)
    {
        result = trial;
        manager = GameManager.Instance;
        input = Object.FindObjectOfType<InputManager>();
        mario = Object.FindObjectOfType<MarioController>();
        if (manager == null || input == null || mario == null) throw new InvalidOperationException("Missing game/input/runner services");
        ability = Object.FindObjectOfType<TricksterAbilitySystem>();
        scan = mario.GetComponent<ScanAbility>();
        gate = Object.FindObjectOfType<TricksterPossessionGate>();
        combo = Object.FindObjectOfType<PropComboTracker>();
        routes = Object.FindObjectOfType<RouteBudgetService>();
        goalX = 37f;
        foreach (char c in scenario.mechanisms)
        {
            var probes = Object.FindObjectsOfType<ExplorationContactProbe>().Where(p => p.mechanism == c.ToString()).ToArray();
            targets[c.ToString()] = probes.Select(p => p.transform).ToArray();
            result.coverage.Add(new MechanismExplorationPlan.Evidence { mechanism = c.ToString(), built = probes.Length });
        }
        marioProfile = MakeProfile(trial.profile, false);
        tricksterProfile = MakeProfile(trial.profile, true);
        bot = new GuidedBot(mario, targets, trial.profile == "Explorer") { marioPersona = marioProfile, tricksterPersona = tricksterProfile };
        bot.SetDecisionSeed(unchecked(scenario.seed + Array.IndexOf(MechanismExplorationPlan.Profiles, trial.profile) * 65537));
        previousInput = input.GetCurrentProvider();
        input.SetInputProvider(bot);
        manager.OnGameOver += OnGameOver;
        if (ability != null) ability.OnPropActivated += OnActivated;
        if (scan != null) scan.OnScanPerformed += OnScan;
        if (gate != null) gate.OnStateChanged += OnPossession;
        if (combo != null) combo.OnComboChanged += OnCombo;
        GameplayEventBus.OnBouncyPlatformLaunched += OnLaunch;
        GameplayEventBus.OnHeatTierChanged += OnHeat;
        GameplayEventBus.OnTrapTriggered += OnTrap;
        GameplayEventBus.OnCrisisWarning += OnCrisis;
        GameplayEventBus.OnTricksterRevealed += OnReveal;
        if (routes != null) { routes.OnRouteDegraded += OnRouteDegraded; routes.OnRouteRecovered += OnRouteRecovered; routes.OnDegradeBlocked += OnRouteBlocked; }
        LootObjective.OnLootCollected += OnLoot;
        EscapeGate.OnEscapeSuccess += OnEscape;
        ExplorationContactProbe.Contact += OnContact;
        Application.logMessageReceived += OnLog;
        result.outcome = "Running";
        result.farthestX = mario.transform.position.x;
    }

    private static BotPersonaConfigSO MakeProfile(string name, bool trickster)
    {
        var p = ScriptableObject.CreateInstance<BotPersonaConfigSO>();
        p.hideFlags = HideFlags.HideAndDontSave;
        p.personaName = name;
        p.reactionDelay = name == "Cautious" ? 0.4f : 0.1f;
        p.riskTolerance = name == "Runner" ? 0.9f : 0.25f;
        p.scanAggression = name == "Explorer" ? 0.95f : 0.4f;
        p.ambushAggression = name == "Cautious" ? 0.35f : 0.9f;
        p.comboPreference = trickster && name != "Cautious" ? 0.9f : 0.3f;
        return p;
    }

    public void Tick(float dt, float limit)
    {
        if (Finished || disposed) return;
        if (manager == null || mario == null) { Finish("RuntimeError", "角色或 GameManager 意外丢失。"); return; }
        if (input == null || input.GetCurrentProvider() != bot) { Finish("Interrupted", "输入被其他工具接管，不能计为 AI 测试结果。"); return; }
        if (manager.CurrentState == GameState.Paused) return;
        result.seconds = manager.RoundElapsed;
        result.endX = mario.transform.position.x; result.endY = mario.transform.position.y;
        result.farthestX = Mathf.Max(result.farthestX, result.endX);
        float distance = Mathf.Abs(goalX - result.endX);
        if (distance < bestDistance - 0.5f) { bestDistance = distance; noProgressTimer = 0; }
        else noProgressTimer += dt;
        if (result.seconds >= limit) { Finish("TimedOut", "超出本次时间预算；不等于物理无解。检查 AI 寻路和关卡节奏。"); return; }
        if (noProgressTimer > 12f) { Finish("NoProgress", "12 秒未向终点取得净进展；可能是 AI 局限、机制等待或布局问题，需复测。"); return; }
        sampleTimer += dt;
        if (sampleTimer < 0.1f) return;
        sampleTimer = 0f;
        foreach (var evidence in result.coverage)
            foreach (var target in targets[evidence.mechanism])
            {
                if (target == null) continue;
                if (Vector2.Distance(mario.transform.position, target.position) < 2f) evidence.approached = true;
                var prop = target.GetComponent<ControllablePropBase>();
                if (prop != null)
                {
                    string phase = prop.GetControlState().ToString();
                    if (!evidence.phases.Contains(phase)) evidence.phases.Add(phase);
                }
            }
    }

    public void Finish(string outcome, string nextAction)
    {
        if (Finished) return;
        result.outcome = outcome;
        result.nextAction = nextAction;
        if (manager != null) result.seconds = manager.RoundElapsed;
        if (mario != null) { result.endX = mario.transform.position.x; result.endY = mario.transform.position.y; }
    }

    private void OnGameOver(string winner)
    {
        Finish(winner == "Mario" ? "Cleared" : "RunnerStopped", winner == "Mario"
            ? "AI 已通过；仍需检查接触/激活缺口，再交给真人判断乐趣。"
            : "检查结束位置、机关预警和替代路线；也要排除 AI 能力不足。");
    }
    private void Event(string label)
    {
        if (!Finished && result.timeline.Count < 100) result.timeline.Add($"{manager.RoundElapsed:F2}s {label}");
    }
    private void OnContact(string id, GameObject source, GameObject actor)
    {
        if (Finished) return;
        var e = result.coverage.Find(v => v.mechanism == id);
        if (e != null)
        {
            e.contacts++;
            bool runnerContact = actor != null && actor.GetComponent<MarioController>() != null;
            if (runnerContact) e.runnerContacts++; else e.tricksterContacts++;
            Event((runnerContact ? "runner contact " : "trickster contact ") + id);
        }
    }
    private void MarkActivation(GameObject source, string label)
    {
        if (Finished || source == null) return;
        var probe = source.GetComponentInParent<ExplorationContactProbe>();
        if (probe == null) return;
        var e = result.coverage.Find(v => v.mechanism == probe.mechanism);
        if (e != null) { e.activations++; Event(label + " " + e.mechanism); }
    }
    private void OnActivated(IControllableProp prop) => MarkActivation(prop.GetTransform().gameObject, "control accepted");
    private void OnLaunch(GameplayEventBus.BouncyPlatformLaunchedPayload p) => MarkActivation(p.platform, "bounce launch");
    private void OnTrap(GameplayEventBus.TrapTriggeredPayload p) => MarkActivation(p.source, "trap event");
    private void OnScan() { if (!Finished) { result.scans++; Event("scan performed"); } }
    private void OnPossession(TricksterPossessionState state)
    { if (!Finished && state == TricksterPossessionState.Possessing) { result.possessions++; Event("possessing"); } }
    private void OnCombo(int count, float multiplier) { if (!Finished && count > 1) { result.comboEvents++; Event("combo " + count); } }
    private void OnHeat(GameplayEventBus.HeatTierChangedPayload p) { if (!Finished) { result.heatEvents++; Event("heat " + p.newTier); } }
    private void OnLoot()
    {
        if (Finished) return;
        result.lootEvents++;
        var e = result.coverage.Find(v => v.mechanism == "o");
        if (e != null) e.activations++;
        Event("loot collected");
    }
    private void OnEscape() { if (!Finished) { result.escapeEvents++; Event("escape"); } }
    private void OnCrisis(GameplayEventBus.CrisisWarningPayload p) { if (!Finished) { result.crises++; Event("crisis " + p.warningType); } }
    private void OnReveal(GameplayEventBus.TricksterRevealedPayload p) { if (!Finished) { result.reveals++; Event("revealed " + p.reason); } }
    private void OnRouteDegraded(string id, string reason) { if (!Finished) { result.routeDegradations++; Event("route degraded " + id); } }
    private void OnRouteRecovered(string id) { if (!Finished) { result.routeRecoveries++; Event("route recovered " + id); } }
    private void OnRouteBlocked(string id, string reason) { if (!Finished) { result.routeBlocks++; Event("route guard " + id); } }
    private void OnLog(string condition, string stack, LogType type)
    {
        if (Finished || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
        if (result.errors.Count < 20) result.errors.Add((condition + "\n" + stack).Substring(0, Math.Min(4000, condition.Length + 1 + stack.Length)));
        Finish("RuntimeError", "优先处理运行时错误；本局不计为可玩性成功。");
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (manager != null) manager.OnGameOver -= OnGameOver;
        if (ability != null) ability.OnPropActivated -= OnActivated;
        if (scan != null) scan.OnScanPerformed -= OnScan;
        if (gate != null) gate.OnStateChanged -= OnPossession;
        if (combo != null) combo.OnComboChanged -= OnCombo;
        GameplayEventBus.OnBouncyPlatformLaunched -= OnLaunch;
        GameplayEventBus.OnHeatTierChanged -= OnHeat;
        GameplayEventBus.OnTrapTriggered -= OnTrap;
        GameplayEventBus.OnCrisisWarning -= OnCrisis;
        GameplayEventBus.OnTricksterRevealed -= OnReveal;
        if (routes != null) { routes.OnRouteDegraded -= OnRouteDegraded; routes.OnRouteRecovered -= OnRouteRecovered; routes.OnDegradeBlocked -= OnRouteBlocked; }
        LootObjective.OnLootCollected -= OnLoot;
        EscapeGate.OnEscapeSuccess -= OnEscape;
        ExplorationContactProbe.Contact -= OnContact;
        Application.logMessageReceived -= OnLog;
        if (input != null && input.GetCurrentProvider() == bot) input.SetInputProvider(previousInput);
        Object.DestroyImmediate(marioProfile); Object.DestroyImmediate(tricksterProfile);
    }

    private sealed class GuidedBot : HeuristicBotInputProvider
    {
        private readonly MarioController runner;
        private readonly KeyValuePair<string, Transform>[] points;
        private readonly bool explore;
        private int cursor;
        private float targetTime, interactTimer;
        public GuidedBot(MarioController runner, Dictionary<string, Transform[]> targets, bool explore)
        {
            this.runner = runner; this.explore = explore;
            points = targets.SelectMany(p => p.Value.Select(t => new KeyValuePair<string, Transform>(p.Key, t)))
                .OrderBy(p => p.Value.position.x).ToArray();
        }
        protected override void UpdateMarioBrain(float dt)
        {
            interactTimer -= dt;
            ExplorationTarget = null;
            if (explore && cursor < points.Length && runner != null)
            {
                var point = points[cursor]; targetTime += dt;
                if (point.Value == null || targetTime > 4f) { cursor++; targetTime = 0f; }
                else ExplorationTarget = point.Value.position;
            }
            base.UpdateMarioBrain(dt);
            if (!explore || cursor >= points.Length || runner == null) return;
            var current = points[cursor];
            if (current.Value != null && current.Key == "H" && interactTimer <= 0f && Vector2.Distance(runner.transform.position, current.Value.position) < 1.2f)
            { p1SDown = true; interactTimer = 1f; }
        }
    }
}
