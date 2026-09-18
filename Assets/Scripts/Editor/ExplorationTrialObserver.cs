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
    private readonly PlayerHealth health;
    private int lastHealth;
    private readonly TricksterPossessionGate gate;
    private readonly PropComboTracker combo;
    private readonly RouteBudgetService routes;
    private readonly Dictionary<string, Transform[]> targets = new Dictionary<string, Transform[]>();
    private readonly BotPersonaConfigSO marioProfile, tricksterProfile;
    private readonly GuidedBot bot;
    private float sampleTimer, noProgressTimer, bestDistance = float.MaxValue;
    private readonly Transform goal, loot;
    private readonly MechanismExplorationPlan.Route[] routeRegions;
    private readonly Dictionary<ControllablePropBase, MechanismExplorationPlan.CounterplayMotion> motion = new Dictionary<ControllablePropBase, MechanismExplorationPlan.CounterplayMotion>();
    private string lastRoute, lastPhase;
    private PossessionAnchor lastPossessedAnchor;
    private bool disposed;
    private float finishedFixedTime = -1f;
    public bool Finished => result.outcome != "Running";
    public string Intent => bot.MarioIntent + " / " + bot.TricksterIntent;

    public ExplorationTrialObserver(MechanismExplorationPlan.Scenario scenario, MechanismExplorationPlan.Trial trial, float trialLimit = 30f)
    {
        result = trial;
        result.experience = scenario.experience;
        result.expectsReturn = scenario.lootEscape;
        result.experienceEvidenceVersion = 1;
        manager = GameManager.Instance;
        input = Object.FindObjectOfType<InputManager>();
        mario = Object.FindObjectOfType<MarioController>();
        if (manager == null || input == null || mario == null) throw new InvalidOperationException("Missing game/input/runner services");
        ability = Object.FindObjectOfType<TricksterAbilitySystem>();
        scan = mario.GetComponent<ScanAbility>();
        health = mario.GetComponent<PlayerHealth>();
        if (health != null) { lastHealth = health.CurrentHealth; result.healthEvidenceVersion = 1; }
        gate = Object.FindObjectOfType<TricksterPossessionGate>();
        combo = Object.FindObjectOfType<PropComboTracker>();
        routes = Object.FindObjectOfType<RouteBudgetService>();
        var escape = Object.FindObjectOfType<EscapeGate>();
        var finish = Object.FindObjectOfType<GoalZone>();
        var treasure = Object.FindObjectOfType<LootObjective>();
        goal = escape != null ? escape.transform : finish != null ? finish.transform : null;
        loot = treasure != null ? treasure.transform : null;
        if (goal == null) throw new InvalidOperationException("Missing actual goal/escape target");
        routeRegions = scenario.routes ?? Array.Empty<MechanismExplorationPlan.Route>();
        var rootProbes = Object.FindObjectsOfType<ExplorationContactProbe>().Where(p => !p.IsMovingPart).ToArray();
        foreach (var probe in rootProbes) probe.BindMovingPart();
        foreach (char c in scenario.mechanisms)
        {
            var probes = rootProbes.Where(p => p.mechanism == c.ToString()).ToArray();
            targets[c.ToString()] = probes.Select(p => p.transform).Distinct().ToArray();
            result.coverage.Add(new MechanismExplorationPlan.Evidence { mechanism = c.ToString(), built = targets[c.ToString()].Length,
                observationVersion = 1, movingPartEvidenceVersion = c == 'P' ? 1 : 0 });
        }
        string runnerName = string.IsNullOrEmpty(trial.marioStrategy) ? trial.profile : trial.marioStrategy;
        string opponentName = string.IsNullOrEmpty(trial.tricksterStrategy) ? trial.profile : trial.tricksterStrategy;
        marioProfile = MakeProfile(runnerName, false);
        tricksterProfile = MakeProfile(opponentName, true);
        bot = new GuidedBot(mario, targets, runnerName == "Explorer", routeRegions, runnerName == "SafeRoute", trialLimit) {
            marioPersona = marioProfile, tricksterPersona = tricksterProfile,
            RunnerStrategy = runnerName == "Scout" ? HeuristicBotInputProvider.RunnerPolicy.Scout :
                runnerName == "SafeRoute" ? HeuristicBotInputProvider.RunnerPolicy.SafeRoute :
                runnerName == "Runner" ? HeuristicBotInputProvider.RunnerPolicy.Rush : HeuristicBotInputProvider.RunnerPolicy.Legacy,
            OpponentStrategy = opponentName == "Ambusher" ? HeuristicBotInputProvider.OpponentPolicy.Ambusher :
                opponentName == "Baiter" ? HeuristicBotInputProvider.OpponentPolicy.Baiter :
                opponentName == "Chaser" ? HeuristicBotInputProvider.OpponentPolicy.Chaser : HeuristicBotInputProvider.OpponentPolicy.Legacy
        };
        int matchupIndex = Array.FindIndex(MechanismExplorationPlan.Matchups(scenario), m => m.mario == runnerName && m.trickster == opponentName);
        bot.SetDecisionSeed(unchecked(scenario.seed + matchupIndex * 65537));
        previousInput = input.GetCurrentProvider();
        input.SetInputProvider(bot);
        manager.OnGameOver += OnGameOver;
        if (health != null) health.OnHealthChanged += OnHealthChanged;
        if (runnerName == "Explorer") result.probeEvidenceVersion = 1;
        if (ability != null) ability.OnPropActivated += OnActivated;
        if (scan != null)
        {
            result.scanEvidenceVersion = 1;
            scan.OnScanPerformed += OnScan;
            scan.OnScanResult += OnScanResult;
        }
        if (gate != null) { gate.OnStateChanged += OnPossession; gate.OnAnchorChanged += OnAnchorChanged; }
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
        p.reactionDelay = name == "Cautious" ? 0.4f : name == "SafeRoute" ? 0.2f : 0.1f;
        p.riskTolerance = name == "Runner" ? 0.9f : 0.25f;
        p.scanAggression = name == "Explorer" || name == "Scout" ? 0.95f : 0.4f;
        p.ambushAggression = name == "Cautious" || name == "Baiter" ? 0.35f : 0.9f;
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
        string phase = loot != null ? (LootObjective.IsLootCarried ? "Escape" : "CollectLoot") : "ReachGoal";
        if (lastPhase != phase)
        {
            lastPhase = phase; result.objectivePhase = phase; lastRoute = null;
            bestDistance = float.MaxValue; noProgressTimer = 0; Event("objective " + phase);
        }
        Transform objective = phase == "CollectLoot" ? loot : goal;
        float distance = objective != null ? Vector2.Distance(mario.transform.position, objective.position) : bestDistance;
        if (bot.WaypointsReached > result.waypointsReached) noProgressTimer = 0;
        result.waypointsReached = bot.WaypointsReached;
        result.completedRoutes = bot.CompletedRoutes.ToList();
        CaptureProbeProgress();
        result.anchorSwitchRequests = bot.AnchorSwitchRequests;
        if (bot.RouteSwitchRequests > result.routeSwitchRequests) Event("route switch requested: " + bot.RouteId + " (not a completed traversal)");
        result.routeSwitchRequests = bot.RouteSwitchRequests;
        result.recoveryAttempts = bot.RecoveryAttempts;
        if (bot.BounceLandingAttempts > result.bounceLandingAttempts) Event("bounce top-landing input requested (not a launch)");
        result.bounceLandingAttempts = bot.BounceLandingAttempts;
        if (distance < bestDistance - 0.5f) { bestDistance = distance; noProgressTimer = 0; }
        else noProgressTimer += dt;
        if (result.seconds >= limit) { Finish("TimedOut", "超出本次时间预算；不等于物理无解。检查 AI 寻路和关卡节奏。"); return; }
        if (noProgressTimer > 12f) { Finish("NoProgress", "12 秒未向当前目标取得净进展，也未到达新路点或获得新接近/接触/激活证据；可能是 AI 局限、机制等待或布局问题，需复测。"); return; }
        sampleTimer += dt;
        if (sampleTimer < 0.1f) return;
        float sampledDuration = Mathf.Min(sampleTimer, 0.25f);
        sampleTimer = 0f;
        if (gate != null && gate.IsHiddenAndArmed && gate.CurrentAnchor != null)
        {
            Vector2 delta = mario.transform.position - gate.CurrentAnchor.AnchorTransform.position;
            if (Mathf.Abs(delta.y) < 2f && delta.magnitude < 6f) result.armedNearbySeconds += sampledDuration;
        }
        foreach (var region in routeRegions)
        {
            if (!region.Contains(result.endX, result.endY)) continue;
            string key = phase + ":" + region.id;
            if (!result.routesUsed.Contains(key)) { result.routesUsed.Add(key); Event("route physically entered " + key); }
            if (lastRoute != null && lastRoute != region.id) { result.routeTransitions++; Event("observed route transition " + lastRoute + " -> " + region.id); }
            lastRoute = region.id;
        }
        foreach (var evidence in result.coverage)
            foreach (var target in targets[evidence.mechanism])
            {
                if (target == null) continue;
                var pendulum = target.GetComponent<PendulumTrap>();
                Vector3 contactPosition = pendulum != null ? pendulum.HammerPosition : target.position;
                if (!evidence.approached && Vector2.Distance(mario.transform.position, contactPosition) < 2f)
                { evidence.approached = true; noProgressTimer = 0; Event("first approach " + evidence.mechanism); }
                var prop = target.GetComponent<ControllablePropBase>();
                if (prop != null)
                {
                    string propPhase = prop.GetControlState().ToString();
                    if (!evidence.phases.Contains(propPhase)) evidence.phases.Add(propPhase);
                    bot.ObserveProbe(evidence.mechanism, target, propPhase);
                    if (!motion.TryGetValue(prop, out var sample)) { sample = new MechanismExplorationPlan.CounterplayMotion(); motion.Add(prop, sample); }
                    Vector2 delta = mario.transform.position - target.position;
                    int flags = sample.Sample(propPhase, delta.x, Mathf.Abs(delta.y), delta.magnitude);
                    if ((flags & 1) != 0) { result.telegraphRetreats++; Event("measured telegraph retreat " + evidence.mechanism); }
                    if ((flags & 2) != 0) { result.recoveryCrossings++; Event("measured recovery crossing " + evidence.mechanism); }
                }
            }
    }

    public void Finish(string outcome, string nextAction)
    {
        if (Finished) return;
        CaptureProbeProgress();
        result.outcome = outcome;
        finishedFixedTime = Time.fixedTime;
        result.nextAction = nextAction;
        if (manager != null)
        {
            result.seconds = manager.RoundElapsed;
            result.endReason = manager.LastRoundReason;
        }
        if (mario != null) { result.endX = mario.transform.position.x; result.endY = mario.transform.position.y; }
    }

    private void CaptureProbeProgress()
    {
        if (result.probeEvidenceVersion < 1) return;
        var visits = bot.ProbeVisits;
        if (visits.BudgetExhausted && !result.probeBudgetExhausted)
            Event("probe budget exhausted; ordinary goal navigation resumes, remaining observations stay missing");
        result.probeBudgetSeconds = visits.Budget; result.probeElapsedSeconds = visits.Elapsed;
        result.probeTargets = visits.TargetCount; result.probeSatisfied = visits.SatisfiedCount;
        result.probeTimedOut = visits.TimedOutTargets; result.probeMissing = visits.MissingTargets;
        result.probeBudgetExhausted = visits.BudgetExhausted;
    }

    private void OnHealthChanged(int current, int maximum)
    {
        if (Finished || disposed) return;
        if (current < lastHealth)
        {
            result.runnerDamageEvents++; result.runnerHealthLost += lastHealth - current;
            Event($"runner health lost {lastHealth - current}; remaining={current} (source not attributed)");
        }
        lastHealth = current;
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
        // Unity does not order damage and probe callbacks on the same trigger.
        // Preserve contacts from the terminal physics step, never later-step gameplay.
        bool terminalContact = result.outcome == "RunnerStopped" && Time.fixedTime == finishedFixedTime;
        if ((Finished && !terminalContact) || disposed || source == null) return;
        var probe = source.GetComponent<ExplorationContactProbe>();
        if (probe == null || probe.Root == null || !targets.TryGetValue(id, out var roots) ||
            !roots.Contains(probe.Root.transform)) return;
        var e = result.coverage.Find(v => v.mechanism == id);
        if (e != null)
        {
            if (e.contacts == 0) noProgressTimer = 0;
            e.contacts++;
            bool runnerContact = actor != null && actor.GetComponent<MarioController>() != null;
            if (runnerContact)
            {
                e.runnerContacts++;
                if (probe.IsMovingPart) e.runnerMovingPartContacts++;
                bot.ObserveProbe(id, probe.Root.transform, probe.IsMovingPart ? "MovingPartContact" : "Contact");
            }
            else e.tricksterContacts++;
            string label = (runnerContact ? "runner contact " : "trickster contact ") + id + (probe.IsMovingPart ? " moving part (not damage)" : "");
            if (terminalContact && result.timeline.Count < 100)
                result.timeline.Add($"{result.seconds:F2}s terminal-step {label}");
            else Event(label);
            if (terminalContact) CaptureProbeProgress();
        }
    }
    private void MarkActivation(GameObject source, string label)
    {
        if (Finished || source == null) return;
        var probe = source.GetComponentInParent<ExplorationContactProbe>();
        if (probe == null) return;
        var e = result.coverage.Find(v => v.mechanism == probe.mechanism);
        if (e != null) { if (e.activations == 0) noProgressTimer = 0; e.activations++; Event(label + " " + e.mechanism); }
    }
    private void OnActivated(IControllableProp prop)
    {
        if (Finished || prop == null) return;
        result.controlAccepted++;
        var source = prop.GetTransform().gameObject;
        var probe = source.GetComponentInParent<ExplorationContactProbe>();
        var evidence = probe != null ? result.coverage.Find(e => e.mechanism == probe.mechanism) : null;
        if (evidence != null) evidence.controlsAccepted++;
        MarkActivation(source, "control accepted");
    }
    private void OnLaunch(GameplayEventBus.BouncyPlatformLaunchedPayload p)
    {
        if (Finished) return;
        if (mario != null && p.target == mario.gameObject)
        {
            result.runnerBounceLaunches++;
            var probe = p.platform != null ? p.platform.GetComponentInParent<ExplorationContactProbe>() : null;
            var evidence = probe != null ? result.coverage.Find(e => e.mechanism == probe.mechanism) : null;
            if (evidence != null)
            {
                evidence.runnerEffects++;
                bot.ObserveProbe(evidence.mechanism, probe.Root.transform, "Effect");
            }
            Event("runner bounce launched velocity=" + p.launchVelocity);
        }
        MarkActivation(p.platform, "bounce launch");
    }
    private void OnTrap(GameplayEventBus.TrapTriggeredPayload p) => MarkActivation(p.source, "trap event");
    private void OnScan() { if (!Finished) { result.scans++; Event("scan performed"); } }
    private void OnScanResult(bool hit)
    {
        if (Finished || disposed) return;
        if (hit) result.scanHits++; else result.scanMisses++;
        Event(hit ? "scan hit: disguised target detected (not proof of damage prevented)" : "scan miss: no disguised target detected");
    }
    private void OnPossession(TricksterPossessionState state)
    {
        if (Finished || state != TricksterPossessionState.Possessing) return;
        result.possessions++; Event("possessing");
        OnAnchorChanged(gate != null ? gate.CurrentAnchor : null);
    }
    private void OnAnchorChanged(PossessionAnchor anchor)
    {
        // Magnetic switching can change the anchor without a state transition.
        if (Finished || gate == null || !gate.IsHiddenAndArmed) return;
        if (anchor != null && lastPossessedAnchor != null && anchor != lastPossessedAnchor)
        { result.possessionTransfers++; Event("possession of a different anchor completed"); }
        if (anchor != null) lastPossessedAnchor = anchor;
    }
    private void OnCombo(int count, float multiplier) { if (!Finished && count > 1) { result.comboEvents++; Event("combo " + count); } }
    private void OnHeat(GameplayEventBus.HeatTierChangedPayload p) { if (!Finished) { result.heatEvents++; Event("heat " + p.newTier); } }
    private void OnLoot()
    {
        if (Finished) return;
        result.lootEvents++;
        var e = result.coverage.Find(v => v.mechanism == "o");
        if (e != null) { e.activations++; e.runnerEffects++; }
        if (loot != null) bot.ObserveProbe("o", loot, "Effect");
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
        if (health != null) health.OnHealthChanged -= OnHealthChanged;
        if (ability != null) ability.OnPropActivated -= OnActivated;
        if (scan != null) { scan.OnScanPerformed -= OnScan; scan.OnScanResult -= OnScanResult; }
        if (gate != null) { gate.OnStateChanged -= OnPossession; gate.OnAnchorChanged -= OnAnchorChanged; }
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

    public sealed class GuidedBot : HeuristicBotInputProvider
    {
        private readonly MarioController runner;
        private readonly KeyValuePair<string, Transform>[] points;
        private readonly bool explore, hasLootObjective;
        private float interactTimer, dropTimer;
        public MechanismExplorationPlan.ProbeVisitBudget ProbeVisits { get; }
        private readonly MechanismExplorationPlan.RouteNavigator navigation;
        public int WaypointsReached => navigation.WaypointsReached;
        public IReadOnlyList<string> CompletedRoutes => navigation.CompletedRoutes;
        public int RouteSwitchRequests => navigation.SwitchRequests;
        public string RouteId => navigation.RouteId;
        public GuidedBot(MarioController runner, Dictionary<string, Transform[]> targets, bool explore, MechanismExplorationPlan.Route[] routes, bool safe)
            : this(runner, targets, explore, routes, safe, 30f) { }
        public GuidedBot(MarioController runner, Dictionary<string, Transform[]> targets, bool explore, MechanismExplorationPlan.Route[] routes, bool safe, float trialLimit)
        {
            navigation = new MechanismExplorationPlan.RouteNavigator(routes, safe);
            this.runner = runner; this.explore = explore;
            hasLootObjective = Object.FindObjectOfType<LootObjective>() != null;
            points = targets.SelectMany(p => p.Value.Select(t => new KeyValuePair<string, Transform>(p.Key, t)))
                .Where(p => p.Value != null).OrderBy(p => p.Value.position.x).GroupBy(p => p.Key).Select(g => g.First()).ToArray();
            ProbeVisits = new MechanismExplorationPlan.ProbeVisitBudget(points.Select(p => p.Key), trialLimit);
        }
        public void ObserveProbe(string mechanism, Transform source, string signal)
        {
            if (explore && points.Any(p => p.Key == mechanism && p.Value == source)) ProbeVisits.Observe(mechanism, signal);
        }
        protected override void UpdateMarioBrain(float dt)
        {
            interactTimer -= dt;
            ExplorationTarget = null;
            AuthoredRouteTarget = false;
            dropTimer -= dt;
            if (runner != null)
            {
                navigation.Tick(runner.transform.position.x, runner.transform.position.y, hasLootObjective && LootObjective.IsLootCarried, dt, runner.IsGrounded);
                var waypoint = navigation.Target;
                if (waypoint != null) { ExplorationTarget = new Vector2(waypoint.x, waypoint.y); AuthoredRouteTarget = true; }
            }
            if (explore && runner != null)
            {
                ProbeVisits.Tick(dt);
                if (!ProbeVisits.Finished)
                {
                    var point = points[ProbeVisits.Cursor];
                    if (point.Value == null) ProbeVisits.SkipMissing();
                    else
                    {
                        var pendulum = point.Value.GetComponent<PendulumTrap>();
                        ExplorationTarget = pendulum != null ? pendulum.HammerPosition : point.Value.position;
                        AuthoredRouteTarget = false;
                    }
                }
            }
            base.UpdateMarioBrain(dt);
            if (runner != null && ExplorationTarget.HasValue && runner.IsGrounded && dropTimer <= 0f &&
                ExplorationTarget.Value.y < runner.transform.position.y - 0.4f &&
                Mathf.Abs(ExplorationTarget.Value.x - runner.transform.position.x) < 0.8f)
            { p1SHeld = true; p1JumpHeld = true; p1JumpDown = true; dropTimer = 0.8f; }
            if (!explore || ProbeVisits.Finished || runner == null) return;
            var current = points[ProbeVisits.Cursor];
            if (current.Value != null && current.Key == "H" && interactTimer <= 0f && Vector2.Distance(runner.transform.position, current.Value.position) < 1.2f)
            { p1SDown = true; interactTimer = 1f; }
        }
    }
}
