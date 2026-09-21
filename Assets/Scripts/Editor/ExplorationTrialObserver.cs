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
    private sealed class QueueWatch
    {
        public MechanismExplorationPlan.QueueEvidence evidence;
        public readonly MechanismExplorationPlan.QueueCrossingMemory crossing = new MechanismExplorationPlan.QueueCrossingMemory();
        public float lastVisibleAt = -100f;
        public readonly MechanismExplorationPlan.QueueEncounterMemory encounter = new MechanismExplorationPlan.QueueEncounterMemory();
        public bool waiting;
    }
    private readonly Dictionary<StateQueueTrap, QueueWatch> queueWatches = new Dictionary<StateQueueTrap, QueueWatch>();
    private Collider2D marioBody;
    private readonly BotPersonaConfigSO marioProfile, tricksterProfile;
    private readonly GuidedBot bot;
    private float sampleTimer, noProgressTimer, bestDistance = float.MaxValue;
    private readonly Transform goal, loot;
    private readonly MechanismExplorationPlan.Route[] routeRegions;
    private readonly Dictionary<ControllablePropBase, MechanismExplorationPlan.CounterplayMotion> motion = new Dictionary<ControllablePropBase, MechanismExplorationPlan.CounterplayMotion>();
    private string lastRoute, lastPhase;
    private PossessionAnchor lastPossessedAnchor;
    private bool disposed;
    private PossessionAnchor tunnelOrigin, lastTunnelArrival, visitAnchor;
    private MechanismExplorationPlan.TunnelVisit currentVisit;
    private int visitArrivalFrame;
    private readonly DisguiseSystem visitDisguise;
    private float tunnelArrivalAt = -100f, decisionTimer;
    private string lastDecision;
    private bool HumanTrial => result.controlMode == "HumanMario" || result.controlMode == "HumanTrickster";
    private float finishedFixedTime = -1f;
    public bool Finished => result.outcome != "Running";
    public string Intent => bot.MarioIntent + " / " + bot.TricksterIntent;

    public ExplorationTrialObserver(MechanismExplorationPlan.Scenario scenario, MechanismExplorationPlan.Trial trial, float trialLimit = 30f)
    {
        result = trial;
        result.experience = scenario.experience;
        result.tunnelVersion = scenario.tunnelVersion;
        if (scenario.tunnelVersion >= 1) { result.tunnelEvidenceVersion = 1; result.tunnelVisitEvidenceVersion = 1; }
        result.tunnelPlanningPolicy = scenario.duelVersion >= 1 ? "SpeedEnvelopeV2+GroundedLayerPreparationV1" : "LegacyLocalPrediction";
        if (scenario.duelVersion >= 1) result.tunnelDecisionEvidenceVersion = result.stairRecoveryEvidenceVersion = 1;
        result.expectsReturn = scenario.lootEscape;
        result.counterplayVersion = scenario.counterplayVersion;
        result.startDelaySeconds = scenario.startDelaySeconds;
        result.experienceEvidenceVersion = 1;
        result.scanPolicy = trial.marioStrategy == "Adaptive" || trial.marioStrategy == "SafeRoute"
            ? "LocalEvidenceOrBlockerWindup" : "LegacyPersona";
        manager = GameManager.Instance;
        input = Object.FindObjectOfType<InputManager>();
        mario = Object.FindObjectOfType<MarioController>();
        if (manager == null || input == null || mario == null) throw new InvalidOperationException("Missing game/input/runner services");
        ability = Object.FindObjectOfType<TricksterAbilitySystem>();
        scan = mario.GetComponent<ScanAbility>();
        health = mario.GetComponent<PlayerHealth>();
        if (health != null) { lastHealth = health.CurrentHealth; result.healthEvidenceVersion = 1; }
        gate = Object.FindObjectOfType<TricksterPossessionGate>();
        visitDisguise = gate != null ? gate.GetComponent<DisguiseSystem>() : null;
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
        marioBody = mario.GetComponent<Collider2D>();
        result.queueEvidenceVersion = 2;
        result.startTimingEvidenceVersion = 1;
        foreach (var target in targets.Values.SelectMany(t => t).Distinct())
        {
            var queue = target.GetComponent<StateQueueTrap>();
            if (queue == null) continue;
            var evidence = new MechanismExplorationPlan.QueueEvidence { source = queue.name + "@" + queue.transform.position };
            result.queueEvidence.Add(evidence); queueWatches.Add(queue, new QueueWatch { evidence = evidence });
        }
        string runnerName = string.IsNullOrEmpty(trial.marioStrategy) ? trial.profile : trial.marioStrategy;
        string opponentName = string.IsNullOrEmpty(trial.tricksterStrategy) ? trial.profile : trial.tricksterStrategy;
        marioProfile = MakeProfile(runnerName, false);
        tricksterProfile = MakeProfile(opponentName, true);
        bot = new GuidedBot(mario, targets, runnerName == "Explorer", routeRegions, runnerName == "SafeRoute", trialLimit) {
            marioPersona = marioProfile, tricksterPersona = tricksterProfile,
            ReadPublicQueues = runnerName == "Adaptive",
            PassiveOpponent = opponentName == "Passive",
            TunnelOpponent = opponentName == "TunnelChaser",
            GroundOnlyOpponent = opponentName == "GroundChaser",
            PlannedTunnelOpponent = scenario.duelVersion >= 1,
            RecoverRouteFalls = scenario.duelVersion >= 1,
            ControlMode = trial.controlMode,
            StartDelayRemaining = scenario.startDelaySeconds,
            RunnerStrategy = runnerName == "Scout" || runnerName == "Adaptive" ? HeuristicBotInputProvider.RunnerPolicy.Scout :
                runnerName == "SafeRoute" ? HeuristicBotInputProvider.RunnerPolicy.SafeRoute :
                runnerName == "Runner" ? HeuristicBotInputProvider.RunnerPolicy.Rush : HeuristicBotInputProvider.RunnerPolicy.Legacy,
            OpponentStrategy = opponentName == "Ambusher" ? HeuristicBotInputProvider.OpponentPolicy.Ambusher :
                opponentName == "Baiter" ? HeuristicBotInputProvider.OpponentPolicy.Baiter :
                opponentName == "Chaser" || opponentName == "TunnelChaser" || opponentName == "GroundChaser" ? HeuristicBotInputProvider.OpponentPolicy.Chaser : HeuristicBotInputProvider.OpponentPolicy.Legacy
        };
        int matchupIndex = Array.FindIndex(MechanismExplorationPlan.Matchups(scenario), m => m.mario == runnerName && m.trickster == opponentName);
        // Diagnostic comparisons share the seed, not separate random streams per treatment.
        // Opponent presence can still change subsequent decisions; pairs are not causal proof.
        bot.SetDecisionSeed(scenario.counterplayVersion >= 1 || scenario.tunnelVersion >= 1 ? scenario.seed : unchecked(scenario.seed + matchupIndex * 65537));
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
        StateQueueTrap.ActualDamage += OnQueueDamage;
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
        CaptureQueueEvidence(dt);
        CaptureStartTiming();
        CaptureTunnelArrival();
        CaptureTunnelVisit();
        decisionTimer += dt;
        if (decisionTimer >= 0.5f)
        {
            decisionTimer = 0f;
            string decision = Intent;
            if (lastDecision != decision && result.decisions.Count < 160)
                result.decisions.Add($"{result.seconds:F2}s {phase} runner=({result.endX:F2},{result.endY:F2}) {decision}");
            lastDecision = decision;
        }
        result.anchorSwitchRequests = bot.AnchorSwitchRequests;
        if (bot.RouteSwitchRequests > result.routeSwitchRequests) Event("route switch requested: " + bot.RouteId + " (not a completed traversal)");
        result.routeSwitchRequests = bot.RouteSwitchRequests;
        if (bot.StairRecoveryRequests > result.stairRecoveryRequests)
            Event("stair recovery requested: rewind to lower authored landing (not a completed traversal)");
        result.stairRecoveryRequests = bot.StairRecoveryRequests;
        result.recoveryAttempts = bot.RecoveryAttempts;
        if (bot.BounceLandingAttempts > result.bounceLandingAttempts) Event("bounce top-landing input requested (not a launch)");
        result.bounceLandingAttempts = bot.BounceLandingAttempts;
        if (distance < bestDistance - 0.5f) { bestDistance = distance; noProgressTimer = 0; }
        else noProgressTimer += dt;
        if (result.seconds >= limit) { Finish("TimedOut", "超出本次时间预算；不等于物理无解。检查 AI 寻路和关卡节奏。"); return; }
        if (!HumanTrial && noProgressTimer > 12f) { Finish("NoProgress", "12 秒未向当前目标取得净进展，也未到达新路点或获得新接近/接触/激活证据；可能是 AI 局限、机制等待或布局问题，需复测。"); return; }
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
        CaptureStartTiming();
        CaptureTunnelArrival();
        CaptureTunnelVisit(); EndTunnelVisit();
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

    private void CaptureStartTiming()
    {
        if (bot.TunnelRequests > result.tunnelRequests && !string.IsNullOrEmpty(bot.LastTunnelPlan))
            Event(bot.LastTunnelPlan); // Input-edge evidence must not disappear between 0.5s intention samples.
        result.tunnelRequests = bot.TunnelRequests;
        result.tunnelPreparationRequests = bot.TunnelPreparationRequests;
        result.actualStartWaitSeconds = bot.StartWaitSeconds;
        result.startWaitFrames = bot.StartWaitFrames;
        result.opponentWaitDecisionFrames = bot.OpponentWaitDecisionFrames;
        result.opponentWaitInputFrames = bot.OpponentWaitInputFrames;
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

    private void CaptureQueueEvidence(float dt)
    {
        if (marioBody == null) return;
        foreach (var item in queueWatches)
        {
            var queue = item.Key; var watch = item.Value; var e = watch.evidence;
            if (queue == null || !queue.isActiveAndEnabled) continue;
            if (queue.TryReadPublicCue(marioBody.bounds.center, out var visible))
            {
                e.cueSamples++; watch.lastVisibleAt = manager.RoundElapsed;
                string label = visible.current + " -> " + visible.next;
                if (e.lastCue != label)
                { e.cueChanges++; e.lastCue = label; Event("public queue visible " + e.source + " " + label); }
            }
            bool waiting = bot.WaitingQueue == queue;
            if (waiting)
            {
                e.waitSeconds += Mathf.Max(0f, dt);
                if (!watch.waiting) { e.waits++; Event("ordinary wait input " + e.source + ": " + bot.QueueDecision); }
            }
            watch.waiting = waiting;
            var actual = queue.ReadPublicCue(); // Observer measurement only; never fed to the bot without visibility.
            int flags = watch.crossing.Sample(marioBody.bounds.center.x - queue.transform.position.x,
                marioBody.bounds.center.y - queue.transform.position.y,
                actual.halfWidth + marioBody.bounds.extents.x, result.runnerHealthLost);
            if ((flags & 1) != 0) { e.entries++; Event("entered queue reach " + e.source); }
            if ((flags & 2) != 0) { e.crossings++; Event("full same-lane queue crossing " + e.source); }
            if ((flags & 4) != 0) e.cleanCrossings++;
            watch.encounter.Sample(marioBody.bounds.center.x - queue.transform.position.x,
                marioBody.bounds.center.y - queue.transform.position.y,
                actual.halfWidth + marioBody.bounds.extents.x, result.runnerHealthLost, flags);
            if (watch.encounter.Completed > e.completedEncounters)
                Event("whole queue encounter completed " + e.source + "; clean=" + (watch.encounter.CleanCompleted > e.cleanEncounters));
            e.encounters = watch.encounter.Started; e.completedEncounters = watch.encounter.Completed;
            e.cleanEncounters = watch.encounter.CleanCompleted; e.bypassedEncounters = watch.encounter.Bypassed;
        }
    }
    private void OnQueueDamage(StateQueueTrap source, MarioController actor, int lost, StateQueueTrap.PublicCue cue)
    {
        if (disposed || actor != mario || source == null || lost <= 0 || !queueWatches.TryGetValue(source, out var watch)) return;
        if (Finished && !(result.outcome == "RunnerStopped" && Time.fixedTime == finishedFixedTime)) return;
        var e = watch.evidence; e.damageEvents++; e.healthLost += lost;
        bool recent = manager.RoundElapsed - watch.lastVisibleAt <= 0.75f;
        if (recent) e.damageWithRecentCue++;
        string label = $"queue actual damage {e.source}: lost={lost}, state={cue.current}, remaining={cue.remaining:F1}s, runner={mario.transform.position}, cue age={manager.RoundElapsed - watch.lastVisibleAt:F2}s, recent local cue={recent}, decision={bot.QueueDecision ?? bot.MarioIntent}; visibility proxy, not human comprehension";
        if (Finished && result.timeline.Count < 100) result.timeline.Add($"{result.seconds:F2}s {label}");
        else Event(label);
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
            ? (HumanTrial ? "真人试玩通关；反馈单独保存，不认证自动策略或乐趣。" : "AI 已通过；仍需检查接触/激活缺口，再交给真人判断乐趣。")
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
        if (CurrentVisitStillPresent() && prop.GetTransform() == visitAnchor.transform &&
            currentVisit.RecordControl(manager.RoundElapsed, result.lootEvents > 0))
            Event("control during continuous tunnel residence at " + currentVisit.destination +
                (result.lootEvents > 0 ? " on return" : " outbound") + "; acceptance, not causal effect");
        if (lastTunnelArrival != null && gate != null && gate.CurrentAnchor == lastTunnelArrival &&
            prop.GetTransform() == lastTunnelArrival.transform && manager.RoundElapsed - tunnelArrivalAt <= 3f)
        {
            result.controlsAfterTunnel++;
            Event("control accepted at verified arrival anchor within 3s; temporal association, not causal success");
            lastTunnelArrival = null; // One opportunity per arrival, not repeated counting.
        }
        if (result.lootEvents > 0) result.postLootControls++;
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
        if (Finished) return;
        if (result.tunnelEvidenceVersion >= 1)
        {
            if (state == TricksterPossessionState.Underlining)
            {
                EndTunnelVisit();
                result.tunnelStarts++; tunnelOrigin = gate.CurrentAnchor;
                Event("native tunnel departure " + (tunnelOrigin != null ? tunnelOrigin.AnchorId : "unknown"));
            }
            else if (state == TricksterPossessionState.Revealed || state == TricksterPossessionState.Roaming || state == TricksterPossessionState.Escaping)
            { tunnelOrigin = null; if (state == TricksterPossessionState.Roaming) EndTunnelVisit(); }
            else if (state == TricksterPossessionState.Possessing) CaptureTunnelArrival();
        }
        if (state == TricksterPossessionState.Blending && bot.StartWaitingThisTick)
        { result.opponentWaitPreparations++; Event("opponent began normal blending during runner start wait"); }
        if (state != TricksterPossessionState.Possessing) return;
        result.possessions++; Event("possessing");
        OnAnchorChanged(gate != null ? gate.CurrentAnchor : null);
    }
    private void OnAnchorChanged(PossessionAnchor anchor)
    {
        // Magnetic switching can change the anchor without a state transition.
        if (!Finished && visitAnchor != null && anchor != visitAnchor) EndTunnelVisit();
        if (Finished || gate == null || !gate.IsHiddenAndArmed) return;
        if (anchor != null && lastPossessedAnchor != null && anchor != lastPossessedAnchor)
        { result.possessionTransfers++; if (result.lootEvents > 0) result.postLootTransfers++; Event("possession of a different anchor completed"); }
        if (anchor != null) lastPossessedAnchor = anchor;
    }
    private void CaptureTunnelArrival()
    {
        if (result.tunnelEvidenceVersion < 1 || tunnelOrigin == null || gate == null || !gate.IsHiddenAndArmed) return;
        var destination = gate.CurrentAnchor;
        bool arrived = destination != null && destination != tunnelOrigin &&
            tunnelOrigin.connectedUnderlineNodes.Contains(destination) &&
            Vector2.Distance(gate.transform.position, destination.transform.position) < 0.8f;
        if (!arrived) return;
        result.tunnelArrivals++;
        if (result.lootEvents > 0) result.postLootTunnelArrivals++;
        lastTunnelArrival = destination; tunnelArrivalAt = manager.RoundElapsed;
        EndTunnelVisit();
        if (result.tunnelVisits.Count < 32)
        {
            visitAnchor = destination; visitArrivalFrame = Time.frameCount;
            currentVisit = new MechanismExplorationPlan.TunnelVisit { origin = tunnelOrigin.AnchorId, destination = destination.AnchorId,
                x = destination.transform.position.x, y = destination.transform.position.y, arrivalAt = tunnelArrivalAt,
                direction = Mathf.Abs(mario.Velocity.x) >= 0.5f ? Mathf.Sign(mario.Velocity.x) : 0f };
            result.tunnelVisits.Add(currentVisit);
        }
        else result.tunnelVisitOverflow++;
        Event("verified tunnel arrival " + tunnelOrigin.AnchorId + " -> " + destination.AnchorId +
            "; physical position + linked anchor + possession, not a direction request");
        tunnelOrigin = null;
    }

    private bool CurrentVisitStillPresent() => currentVisit != null && currentVisit.endedAt < 0f &&
        visitAnchor != null && gate != null && gate.CurrentAnchor == visitAnchor && visitDisguise != null && visitDisguise.IsDisguised &&
        Vector2.Distance(gate.transform.position, visitAnchor.transform.position) < 0.8f;

    private void CaptureTunnelVisit()
    {
        if (currentVisit == null) return;
        if (!CurrentVisitStillPresent()) { EndTunnelVisit(); return; }
        if (Time.frameCount >= visitArrivalFrame + 2 && currentVisit.ObserveReady(manager.RoundElapsed,
            mario.transform.position.x, mario.transform.position.y, gate.IsHiddenAndArmed, visitDisguise.IsFullyBlended))
            Event("tunnel exit ready after re-blend: " + currentVisit.destination +
                (currentVisit.runnerPassedWhenReady ? "; runner already passed in arrival direction" : "; passage not yet observed"));
    }

    private void EndTunnelVisit()
    {
        if (currentVisit != null && currentVisit.endedAt < 0f && manager != null) currentVisit.endedAt = manager.RoundElapsed;
        currentVisit = null; visitAnchor = null;
    }

    public void MarkFeedback(string tag)
    {
        if (disposed || Finished || result.feedback.Count >= 60 || string.IsNullOrWhiteSpace(tag)) return;
        result.feedback.Add($"{manager.RoundElapsed:F2}s [{result.controlMode ?? "Automated"}] " +
            $"runner=({mario.transform.position.x:F2},{mario.transform.position.y:F2}) {tag.Substring(0, Math.Min(240, tag.Length))}");
    }

    private void OnCombo(int count, float multiplier) { if (!Finished && count > 1) { result.comboEvents++; Event("combo " + count); } }
    private void OnHeat(GameplayEventBus.HeatTierChangedPayload p) { if (!Finished) { result.heatEvents++; Event("heat " + p.newTier); } }
    private void OnLoot()
    {
        if (Finished) return;
        result.lootEvents++;
        if (result.lootAtSeconds < 0f) result.lootAtSeconds = manager.RoundElapsed;
        var e = result.coverage.Find(v => v.mechanism == "o");
        if (e != null) { e.activations++; e.runnerEffects++; }
        if (loot != null) bot.ObserveProbe("o", loot, "Effect");
        Event("loot collected");
    }
    private void OnEscape() { if (!Finished) { result.escapeEvents++; result.escapeAtSeconds = manager.RoundElapsed; Event("escape"); } }
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
        StateQueueTrap.ActualDamage -= OnQueueDamage;
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
        public bool ReadPublicQueues, PassiveOpponent, TunnelOpponent, GroundOnlyOpponent, PlannedTunnelOpponent;
        public string ControlMode;
        public int TunnelRequests { get; private set; }
        public string LastTunnelPlan { get; private set; }
        private readonly MechanismExplorationPlan.TunnelPreparationBudget preparation = new MechanismExplorationPlan.TunnelPreparationBudget();
        public int TunnelPreparationRequests => preparation.Requests;
        public bool RecoverRouteFalls;
        private float observedTunnelSpeed;
        private float tunnelInputCooldown;
        private readonly KeyboardInputProvider keyboard = new KeyboardInputProvider();
        private readonly TricksterPossessionGate opponentGate;
        private readonly DisguiseSystem opponentDisguise;
        private bool HumanMario => ControlMode == "HumanMario";
        private bool HumanTrickster => ControlMode == "HumanTrickster";
        protected override bool UsesHumanTricksterInput => HumanTrickster;
        protected override bool OwnsDirectionalTransferPlanning => PlannedTunnelOpponent && (TunnelOpponent || GroundOnlyOpponent);
        public float StartDelayRemaining;
        public float StartWaitSeconds { get; private set; }
        public bool StartWaitingThisTick { get; private set; }
        public int StartWaitFrames { get; private set; }
        public int OpponentWaitDecisionFrames { get; private set; }
        public int OpponentWaitInputFrames { get; private set; }
        public StateQueueTrap WaitingQueue { get; private set; }
        public string QueueDecision { get; private set; }
        private readonly StateQueueTrap[] queueTraps;
        private StateQueueTrap crossingQueue;
        private float crossingDirection;
        private Collider2D runnerBody;
        private readonly MechanismExplorationPlan.RouteNavigator navigation;
        public int WaypointsReached => navigation.WaypointsReached;
        public IReadOnlyList<string> CompletedRoutes => navigation.CompletedRoutes;
        public int RouteSwitchRequests => navigation.SwitchRequests;
        public int StairRecoveryRequests => navigation.StairRecoveryRequests;
        public string RouteId => navigation.RouteId;
        public GuidedBot(MarioController runner, Dictionary<string, Transform[]> targets, bool explore, MechanismExplorationPlan.Route[] routes, bool safe)
            : this(runner, targets, explore, routes, safe, 30f) { }
        public GuidedBot(MarioController runner, Dictionary<string, Transform[]> targets, bool explore, MechanismExplorationPlan.Route[] routes, bool safe, float trialLimit)
        {
            navigation = new MechanismExplorationPlan.RouteNavigator(routes, safe);
            this.runner = runner; this.explore = explore;
            opponentGate = Object.FindObjectOfType<TricksterPossessionGate>();
            opponentDisguise = opponentGate != null ? opponentGate.GetComponent<DisguiseSystem>() : null;
            hasLootObjective = Object.FindObjectOfType<LootObjective>() != null;
            points = targets.SelectMany(p => p.Value.Select(t => new KeyValuePair<string, Transform>(p.Key, t)))
                .Where(p => p.Value != null).OrderBy(p => p.Value.position.x).GroupBy(p => p.Key).Select(g => g.First()).ToArray();
            ProbeVisits = new MechanismExplorationPlan.ProbeVisitBudget(points.Select(p => p.Key), trialLimit);
            queueTraps = targets.Values.SelectMany(t => t).Where(t => t != null)
                .Select(t => t.GetComponent<StateQueueTrap>()).Where(t => t != null).Distinct().ToArray();
            runnerBody = runner != null ? runner.GetComponent<Collider2D>() : null;
        }
        public void ObserveProbe(string mechanism, Transform source, string signal)
        {
            if (explore && points.Any(p => p.Key == mechanism && p.Value == source)) ProbeVisits.Observe(mechanism, signal);
        }
        private float crossingExtent, observedRunSpeed;
        private void NeutralMario()
        {
            p1Horizontal = p1Vertical = 0f;
            p1JumpDown = p1JumpHeld = p1SHeld = p1SDown = false;
        }
        protected override void UpdateTricksterBrain(float dt)
        {
            if (HumanTrickster)
            {
                p2Horizontal = keyboard.GetP2Horizontal(); p2Vertical = keyboard.GetP2Vertical();
                p2JumpDown = keyboard.GetP2JumpDown(); p2JumpHeld = keyboard.GetP2JumpHeld();
                p2DisguiseDown = keyboard.GetP2DisguiseDown(); p2DirectionDown = keyboard.GetP2DirectionDown();
                p2SwitchDir = keyboard.GetP2SwitchDirection(); p2AbilityDown = keyboard.GetP2AbilityDown();
                TricksterIntent = "[Human input; intent not inferred]";
                return;
            }
            if (!PassiveOpponent)
            {
                preparation.Tick(dt);
                if (runner != null) observedTunnelSpeed = Mathf.Max(observedTunnelSpeed, Mathf.Abs(runner.Velocity.x));
                int before = OpponentDecisionTicks;
                base.UpdateTricksterBrain(dt);
                if (TunnelOpponent || GroundOnlyOpponent)
                {
                    // Do not let the generic Chaser direction switch silently use the tunnel network.
                    p2DirectionDown = false;
                    if (opponentGate != null && opponentGate.CurrentState != TricksterPossessionState.Roaming)
                        p2Horizontal = p2Vertical = 0f;
                    tunnelInputCooldown = Mathf.Max(0f, tunnelInputCooldown - dt);
                    if (TunnelOpponent && opponentGate != null && opponentGate.CanSwitchTarget && runner != null &&
                        tunnelInputCooldown <= 0f && !p2AbilityDown && (!PlannedTunnelOpponent || !p2DisguiseDown))
                    {
                        var from = opponentGate.CurrentAnchor;
                        var next = PlannedTunnelOpponent
                            ? FindPreparedTunnelIntercept(from, runner.transform.position, runner.Velocity, opponentDisguise != null ? opponentDisguise.BlendInSeconds : float.NaN,
                                runner.HorizontalSpeedLimit, observedTunnelSpeed)
                            : FindTunnelIntercept(from, runner.transform.position, runner.Velocity);
                        bool preparing = false;
                        if (next == null && PlannedTunnelOpponent && preparation.Available)
                        {
                            next = FindTunnelLayerPreparation(from, runner.transform.position, runner.Velocity, runner.IsGrounded,
                                opponentDisguise != null ? opponentDisguise.BlendInSeconds : float.NaN);
                            preparing = next != null && preparation.TryReserve();
                            if (!preparing) next = null;
                        }
                        if (next != null)
                        {
                            Vector2 direction = ((Vector2)(next.transform.position - from.transform.position)).normalized;
                            p2Horizontal = direction.x; p2Vertical = direction.y;
                            p2DirectionDown = true; p2DisguiseDown = false;
                            float planningVelocity = MechanismExplorationPlan.TunnelPlanningVelocity(runner.Velocity.x, runner.HorizontalSpeedLimit, observedTunnelSpeed);
                            LastTunnelPlan = $"{(preparing ? "layer preparation; intercept NOT guaranteed" : PlannedTunnelOpponent ? "strict intercept forecast" : "legacy local heuristic")} tunnel input {from.AnchorId} -> {next.AnchorId}; runner=({runner.transform.position.x:F2},{runner.transform.position.y:F2}), actualVx={runner.Velocity.x:F2}, planningVx={planningVelocity:F2}; transfer={next.underlineTransitTime:F2}, blend={(opponentDisguise != null ? opponentDisguise.BlendInSeconds : -1f):F2}, telegraph={next.ControllableProp.GetTelegraphDuration():F2}; request only";
                            TunnelRequests++; tunnelInputCooldown = 1.2f;
                            TricksterIntent = (preparing ? "[Layer preparation input -> " : "[Tunnel intercept input -> ") + next.AnchorId + "; arrival and effect not yet verified]";
                        }
                    }
                }
                if (StartWaitingThisTick)
                {
                    if (OpponentDecisionTicks > before) OpponentWaitDecisionFrames++;
                    if (p2Horizontal != 0 || p2Vertical != 0 || p2SwitchDir != 0 || p2JumpDown ||
                        p2JumpHeld || p2DisguiseDown || p2DirectionDown || p2AbilityDown) OpponentWaitInputFrames++;
                }
                return;
            }
            p2Horizontal = p2Vertical = p2SwitchDir = 0f;
            p2JumpDown = p2JumpHeld = p2DirectionDown = p2DisguiseDown = p2AbilityDown = false;
            TricksterIntent = "[Passive control: actor retained, ordinary neutral input]";
        }
        public static PossessionAnchor FindPreparedTunnelIntercept(PossessionAnchor from, Vector2 runnerPosition, Vector2 velocity, float blendSeconds,
            float movementLimit = 0f, float observedPeak = 0f)
        {
            if (from == null || from.connectedUnderlineNodes == null || Vector2.Distance(from.transform.position, runnerPosition) > 12f) return null;
            if (!MechanismExplorationPlan.IsFinite(movementLimit) || movementLimit < 0f) return null;
            float planningVelocity = MechanismExplorationPlan.TunnelPlanningVelocity(velocity.x,
                movementLimit > 0f ? movementLimit : Mathf.Abs(velocity.x), observedPeak);
            if (planningVelocity == 0f) return null;
            var current = from.ControllableProp;
            // Keep a usable current ambush instead of abandoning it merely to increase transfer counts.
            if (current != null && from.CanBePossessed() && Mathf.Abs(runnerPosition.y - from.transform.position.y) <= 1.5f &&
                Mathf.Abs(velocity.x) >= 0.5f)
            {
                float eta = (from.transform.position.x - runnerPosition.x) / planningVelocity;
                if (eta >= 0f && eta <= current.GetTelegraphDuration() + 0.7f) return null;
            }
            PossessionAnchor best = null;
            float bestWindow = float.MaxValue;
            foreach (var next in from.connectedUnderlineNodes)
            {
                if (next == null || next == from || !next.isActiveAndEnabled || !next.CanBePossessed()) continue;
                float window = MechanismExplorationPlan.TunnelAmbushWindow(runnerPosition.x, runnerPosition.y, planningVelocity,
                    next.transform.position.x, next.transform.position.y, next.underlineTransitTime, blendSeconds, next.ControllableProp.GetTelegraphDuration());
                if (window >= 0f && window < bestWindow) { best = next; bestWindow = window; }
            }
            return best;
        }

        public static PossessionAnchor FindTunnelLayerPreparation(PossessionAnchor from, Vector2 runnerPosition, Vector2 velocity,
            bool grounded, float blendSeconds)
        {
            if (from == null || !from.isActiveAndEnabled || from.connectedUnderlineNodes == null ||
                !MechanismExplorationPlan.IsFinite(blendSeconds) || blendSeconds < 0f) return null;
            PossessionAnchor best = null;
            float bestScore = float.MaxValue;
            foreach (var next in from.connectedUnderlineNodes)
            {
                if (next == null || next == from || !next.isActiveAndEnabled || !next.CanBePossessed() ||
                    !MechanismExplorationPlan.IsFinite(next.underlineTransitTime) || next.underlineTransitTime < 0f ||
                    next.underlineTransitTime + blendSeconds > 6f) continue;
                float score = MechanismExplorationPlan.TunnelLayerPreparationScore(runnerPosition.x, runnerPosition.y, velocity.x, grounded,
                    from.transform.position.x, from.transform.position.y, next.transform.position.x, next.transform.position.y);
                if (score >= 0f && score < bestScore) { best = next; bestScore = score; }
            }
            return best;
        }

        public static PossessionAnchor FindTunnelIntercept(PossessionAnchor from, Vector2 runnerPosition, Vector2 velocity)
        {
            if (from == null || from.connectedUnderlineNodes == null || Vector2.Distance(from.transform.position, runnerPosition) > 12f) return null;
            // Local-distance heuristic, not optical perception or hidden-intent inference.
            Vector2 predicted = runnerPosition + new Vector2(Mathf.Clamp(velocity.x, -12f, 12f) * 0.6f, 0);
            Func<PossessionAnchor, float> score = a => Mathf.Abs(a.transform.position.x - predicted.x) +
                2f * Mathf.Abs(a.transform.position.y - runnerPosition.y);
            float bestScore = score(from) - 1f;
            PossessionAnchor best = null;
            foreach (var next in from.connectedUnderlineNodes)
            {
                if (next == null || next == from || !next.isActiveAndEnabled || !next.CanBePossessed()) continue;
                float value = score(next);
                if (value < bestScore) { bestScore = value; best = next; }
            }
            return best;
        }

        private bool ApplyPublicQueueInput()
        {
            if (runner == null || runnerBody == null || !runner.enabled) return false;
            if (runner.IsGrounded) observedRunSpeed = Mathf.Max(observedRunSpeed, Mathf.Abs(runner.Velocity.x));
            if (crossingQueue != null)
            {
                if (!crossingQueue.isActiveAndEnabled || Mathf.Abs(runner.transform.position.y - crossingQueue.transform.position.y) > 1.5f ||
                    (runnerBody.bounds.center.x - crossingQueue.transform.position.x) * crossingDirection > crossingExtent + 0.2f)
                    crossingQueue = null;
                else
                {
                    NeutralMario(); p1Horizontal = crossingDirection;
                    QueueDecision = "committed crossing: outcome still unverified";
                    MarioIntent = "[Crossing observed public window]";
                    return true;
                }
            }
            if (!runner.IsGrounded) return false;
            float direction = ExplorationTarget.HasValue ? Mathf.Sign(ExplorationTarget.Value.x - runnerBody.bounds.center.x) : Mathf.Sign(p1Horizontal);
            if (direction == 0f) return false;
            StateQueueTrap nearest = null;
            StateQueueTrap.PublicCue visible = default;
            float closest = float.MaxValue;
            foreach (var trap in queueTraps)
            {
                if (trap == null) continue;
                float ahead = (trap.transform.position.x - runnerBody.bounds.center.x) * direction;
                if (ahead < 0f || ahead > 3.2f || ahead >= closest || !trap.TryReadPublicCue(runnerBody.bounds.center, out var cue)) continue;
                nearest = trap; visible = cue; closest = ahead;
            }
            if (nearest == null) return false;
            float extent = visible.halfWidth + runnerBody.bounds.extents.x;
            float distance = closest + extent + 0.2f;
            if (MechanismExplorationPlan.QueueWindowAllowsEntry(true, visible.safe, visible.remaining, distance, observedRunSpeed))
            {
                crossingQueue = nearest; crossingDirection = direction; crossingExtent = extent;
                NeutralMario(); p1Horizontal = direction;
                QueueDecision = "enter request: " + visible.current;
                MarioIntent = "[Public window has estimated traversal time]";
            }
            else
            {
                NeutralMario();
                // If already too close, back away with normal input rather than freeze inside the attack reach.
                if (closest < extent + 0.4f) p1Horizontal = -direction;
                WaitingQueue = nearest; QueueDecision = "wait: " + visible.current;
                MarioIntent = "[Waiting outside public queue reach]";
            }
            return true;
        }
        protected override void UpdateMarioBrain(float dt)
        {
            WaitingQueue = null; QueueDecision = null;
            if (HumanMario || HumanTrickster || ControlMode == "Demonstration")
            {
                keyboard.UpdateGamepads();
                // Stop via the batch button; restarting would destroy this trial's evidence.
                pauseDown = keyboard.GetPauseDown();
            }
            if (HumanMario)
            {
                StartWaitingThisTick = false;
                p1Horizontal = keyboard.GetP1Horizontal(); p1Vertical = keyboard.GetP1Vertical();
                p1JumpHeld = keyboard.GetP1JumpHeld(); p1JumpDown = keyboard.GetP1JumpDown();
                p1SHeld = keyboard.GetP1SHeld(); p1SDown = keyboard.GetP1SDown(); p1ScanDown = keyboard.GetP1ScanDown();
                MarioIntent = "[Human input; no authored route control]";
                return;
            }
            EvidenceDrivenScanning = ReadPublicQueues || RunnerStrategy == RunnerPolicy.SafeRoute;
            StartWaitingThisTick = StartDelayRemaining > 0f;
            if (StartDelayRemaining > 0f)
            {
                StartWaitFrames++;
                float step = Mathf.Min(Mathf.Max(0f, dt), StartDelayRemaining);
                StartDelayRemaining -= step;
                StartWaitSeconds += Mathf.Max(0f, dt); // Neutral input lasts the whole sampled frame; do not hide overshoot.
                NeutralMario(); p1ScanDown = false;
                MarioIntent = "[Matched timing: ordinary neutral input]";
                return;
            }
            interactTimer -= dt;
            ExplorationTarget = null;
            AuthoredRouteTarget = false;
            dropTimer -= dt;
            if (runner != null)
            {
                navigation.Tick(runner.transform.position.x, runner.transform.position.y, hasLootObjective && LootObjective.IsLootCarried, dt, runner.IsGrounded, RecoverRouteFalls);
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
            if (ReadPublicQueues && ApplyPublicQueueInput()) return;
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
