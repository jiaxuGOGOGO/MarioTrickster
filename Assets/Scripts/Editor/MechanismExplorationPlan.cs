using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>Versioned, deterministic challenge grammar. Layout coverage is not interaction coverage.</summary>
public static class MechanismExplorationPlan
{
    public const int Version = 3;
    public const string Catalog = "BC-F<XoH>^~P[]Ee@fS";
    public static readonly string[] Profiles = { "Cautious", "Runner", "Explorer" };
    public enum Scope { Smoke, Mechanisms, Pairwise, Experience, Counterplay, TunnelDuel }
    public const int MaxConfirmationScenes = 6;

    // One bounded same-seed confirmation pass, not automatic balancing or a claim of learning.
    public static string[] SelectConfirmationScenes(IEnumerable<Trial> trials)
    {
        return trials.Where(t => t.attempt <= 1 && t.NeedsConfirmation)
            .OrderBy(t => t.outcome == "NoProgress" || t.outcome == "TimedOut" ? 0 : t.outcome == "RunnerStopped" ? 1 : 2)
            .Select(t => t.scenarioId).Distinct().Take(MaxConfirmationScenes).ToArray();
    }

    public static bool RepeatedInfrastructureFailure(IList<Trial> trials)
    {
        if (trials.Count < 2) return false;
        string key = trials[trials.Count - 1].InfrastructureKey;
        return key.Length > 0 && key == trials[trials.Count - 2].InfrastructureKey;
    }

    public static string CompareConfirmation(Trial baseline, Trial confirmation)
    {
        if (baseline == null) return "缺少首轮基线，不能判定改善。";
        Func<Trial, string> signature = t => t.outcome + ":" + t.objectivePhase + ":" +
            string.Join(",", (t.completedRoutes ?? new List<string>()).OrderBy(r => r)) + $":{t.armedNearbySeconds > 0}:{t.possessionTransfers > 0}:" +
            $"{t.tunnelEvidenceVersion}:{t.tunnelArrivals > 0}:{t.postLootTunnelArrivals > 0}:{t.controlsAfterTunnel > 0}:{t.scanEvidenceVersion}:{t.scanHits > 0}:{t.healthEvidenceVersion}:{t.runnerDamageEvents > 0}:{t.probeEvidenceVersion}:{t.probeBudgetExhausted}:{t.queueEvidenceVersion}:{t.queueEvidence.Sum(q => q.cleanCrossings)}:{t.queueEvidence.Sum(q => q.healthLost)}:{t.queueEvidence.Sum(q => q.cleanEncounters)}:{t.startTimingEvidenceVersion}:{IndependentStartObserved(t)}:" + string.Join(",", (t.routesUsed ?? new List<string>()).OrderBy(r => r)) + $":{t.telegraphRetreats > 0}:{t.recoveryCrossings > 0}:{t.runnerBounceLaunches > 0}:" + string.Join("|", t.coverage
            .OrderBy(e => e.mechanism).Select(e => $"{e.mechanism}:{e.built > 0}:{e.approached}:{e.contacts > 0}:{e.activations > 0}:{e.observationVersion}:{e.runnerContacts > 0}:{e.runnerEffects > 0}:{e.movingPartEvidenceVersion}:{e.runnerMovingPartContacts > 0}:" + string.Join(",", e.phases.OrderBy(p => p))));
        return signature(baseline) == signature(confirmation)
            ? "同条件复现：结果与覆盖层级一致；仍需检查 AI 局限与真人反制体验。"
            : "同条件结果不稳定：不是已修复；对照事件时间线再定位物理时序或 AI 决策。";
    }

    // A bounded visit plan, not a mechanism correctness oracle. One representative per type.
    public sealed class ProbeVisitBudget
    {
        private readonly string[] mechanisms;
        private readonly HashSet<string> satisfied = new HashSet<string>();
        private readonly HashSet<string> activeSeen = new HashSet<string>();
        private float targetSeconds;
        public int Cursor { get; private set; }
        public int TargetCount => mechanisms.Length;
        public int SatisfiedCount => satisfied.Count;
        public int TimedOutTargets { get; private set; }
        public int MissingTargets { get; private set; }
        public float Elapsed { get; private set; }
        public float Budget { get; }
        public bool BudgetExhausted { get; private set; }
        public bool Finished => BudgetExhausted || Cursor >= mechanisms.Length;
        public string Current => Finished ? null : mechanisms[Cursor];
        public ProbeVisitBudget(IEnumerable<string> types, float trialLimit)
        {
            mechanisms = types.Distinct().ToArray();
            Budget = Math.Min(8f, Math.Max(0f, trialLimit) * 0.5f);
        }
        public void Observe(string mechanism, string signal)
        {
            if (Finished || !mechanisms.Contains(mechanism)) return;
            bool success;
            switch (mechanism)
            {
                case "B": case "o": success = signal == "Effect"; break;
                case "F": case "[":
                    if (signal == "Active") activeSeen.Add(mechanism);
                    success = signal == "Recovery" && activeSeen.Contains(mechanism); break;
                case "H": success = false; break; // Keep ordinary S input opportunity; contact is not teleportation.
                case "P": success = signal == "MovingPartContact"; break;
                default: success = signal == "Contact"; break;
            }
            if (success) satisfied.Add(mechanism);
        }
        public void Tick(float dt)
        {
            if (Finished) return;
            while (Cursor < mechanisms.Length && satisfied.Contains(mechanisms[Cursor]))
            { Cursor++; targetSeconds = 0f; }
            if (Finished) return;
            dt = Math.Max(0f, dt);
            Elapsed = Math.Min(Budget, Elapsed + dt);
            if (Elapsed >= Budget) { BudgetExhausted = true; return; }
            targetSeconds += dt;
            if (targetSeconds >= 4f) { TimedOutTargets++; Cursor++; targetSeconds = 0f; }
            while (Cursor < mechanisms.Length && satisfied.Contains(mechanisms[Cursor]))
            { Cursor++; targetSeconds = 0f; }
        }
        public void SkipMissing()
        {
            if (Finished) return;
            MissingTargets++; Cursor++; targetSeconds = 0f;
        }
    }

    [Serializable]
    public sealed class Scenario
    {
        public int version = Version;
        public int seed;
        public string id;
        public string mechanisms;
        public string ascii;
        public bool lootEscape;
        public string intention;
        // Empty on v1 reports: replay their saved ASCII, never regenerate with the new grammar.
        public string experience;
        public int counterplayVersion;
        public float startDelaySeconds; // Ordinary neutral Mario input; the world and opponent keep running.
        public Route[] routes = Array.Empty<Route>();
        public int tunnelVersion;
        public string designQuestion;
        // Separate creative grammar; legacy TunnelDuel reports keep their saved layouts unchanged.
        public int duelVersion, duelVariant, iteration;
        public int wallTacticsVersion; // Opt-in policy; zero preserves S174 and older replay behavior.
        public string parentScenarioId, mutationReason;
        public TunnelLink[] tunnelLinks = Array.Empty<TunnelLink>();
        // Used only by explicit single-match demonstration / human rehearsal. Old plans stay unchanged.
        public Matchup[] selectedMatchups = Array.Empty<Matchup>();
    }

    [Serializable]
    public sealed class Point
    {
        public float x, y;
        public Point(float x, float y) { this.x = x; this.y = y; }
    }

    [Serializable]
    public sealed class Route
    {
        public string id;
        public Point[] points;
        public float minX, maxX, minY, maxY;
        public bool Contains(float x, float y) => x >= minX && x <= maxX && y >= minY && y <= maxY;
    }

    public enum WallAction { None, Brake, Retreat, Cross }

    [Serializable]
    public sealed class WallEpisode
    {
        public string source, leg, decision, reason, outcome;
        public float beganAt, endedAt = -1f, inputAt = -1f, crossedAt = -1f;
        public bool sawWarning, sawSolid, sawReopen, damageObserved;
        public int inputFrames;
    }

    // Public, locally visible wall changes only. A reaction is NOT proof that the AI induced it.
    // One bounded episode per source/leg; later retries are left to ordinary navigation.
    public sealed class WallTactics
    {
        public readonly List<WallEpisode> Episodes = new List<WallEpisode>();
        public WallEpisode Current { get; private set; }
        private float direction, startX, trapX, extent, lastNow = -1f;
        public bool Enabled;
        public WallAction Tick(string source, string leg, float now, float runnerX, float wallX, float halfSpan,
            float travelDirection, bool visible, bool grounded, bool solid, bool warning, bool safeFooting)
        {
            if (!IsFinite(now) || now < 0 || now < lastNow || !IsFinite(runnerX) || !IsFinite(wallX) ||
                !IsFinite(halfSpan) || halfSpan <= 0 || !IsFinite(travelDirection)) return WallAction.None;
            lastNow = now;
            if (Current != null && (Current.source != source || Current.leg != leg || !visible)) End(now, "LostCue");
            if (Current == null && visible && !string.IsNullOrEmpty(source) && (warning || solid) &&
                Math.Abs(travelDirection) >= 0.5f && (wallX - runnerX) * Math.Sign(travelDirection) >= 0 &&
                Math.Abs(wallX - runnerX) <= 6f && Episodes.Count < 16 &&
                !Episodes.Any(e => e.source == source && e.leg == leg))
            {
                direction = Math.Sign(travelDirection); startX = runnerX; trapX = wallX; extent = halfSpan;
                Current = new WallEpisode { source = source, leg = leg, beganAt = now, decision = "Observe", outcome = "Open" };
                Episodes.Add(Current);
            }
            if (Current == null) return WallAction.None;
            Current.sawWarning |= warning; Current.sawSolid |= solid;
            Current.sawReopen |= Current.sawSolid && !solid && !warning;
            if ((runnerX - trapX) * direction > extent + 0.2f)
            {
                Current.crossedAt = now;
                End(now, Current.sawReopen ? "CrossedAfterReopen" : "PassedWithoutReopen");
                return WallAction.None;
            }
            if (now - Current.beganAt >= 4f) { End(now, "BudgetExpired"); return WallAction.None; }
            // Observe in both treatment and baseline; only treatment owns these ordinary inputs.
            if (!Enabled || !grounded || !safeFooting || Math.Abs(travelDirection) < 0.5f || Math.Sign(travelDirection) != direction)
            {
                Current.decision = "Observe";
                Current.reason = !Enabled ? "Baseline" : !grounded ? "Airborne" : !safeFooting ? "TerrainOrReaction" : "DirectionChanged";
                return WallAction.None;
            }
            float ahead = (trapX - runnerX) * direction;
            WallAction action;
            if (warning && ahead < extent + 1.2f && Math.Abs(runnerX - startX) < 1.5f) action = WallAction.Retreat;
            else if (warning || solid) action = WallAction.Brake;
            else if (Current.sawReopen) action = WallAction.Cross;
            else { Current.decision = "Observe"; return WallAction.None; }
            Current.decision = action.ToString();
            Current.reason = warning ? "VisibleWarning" : solid ? "ObservedSolid" : "ObservedReopen";
            return action;
        }
        public void RecordInput(float now)
        {
            if (Current == null || !IsFinite(now) || now < Current.beganAt || Current.decision == "Observe") return;
            if (Current.inputAt < 0) Current.inputAt = now;
            Current.inputFrames++;
        }
        public void ObserveDamage() { if (Current != null) Current.damageObserved = true; }
        public void End(float now, string outcome)
        {
            if (Current == null || !IsFinite(now) || now < Current.beganAt) return;
            Current.endedAt = now; Current.outcome = outcome; Current = null;
        }
    }

    // Navigation memory records reached coordinates, not claims of successful pathfinding.
    public sealed class RouteNavigator
    {
        private readonly Route[] routes;
        private readonly HashSet<string> reached = new HashSet<string>();
        private int routeIndex, cursor;
        private bool returning, partialRoute;
        public readonly List<string> CompletedRoutes = new List<string>();
        private float stalled, best = float.MaxValue;
        public int SwitchRequests { get; private set; }
        public int StairRecoveryRequests { get; private set; }
        public bool LearnReturnRoute;
        public bool SawOutboundThreat { get; private set; }
        public int ReturnDetourRequests { get; private set; }
        public void ObservePublicThreat()
        { if (LearnReturnRoute && !returning && RouteId == "lower") SawOutboundThreat = true; }
        public int WaypointsReached => reached.Count;
        public string RouteId => routes.Length == 0 ? "direct" : routes[routeIndex].id;
        public Point Target => routes.Length == 0 || cursor >= routes[routeIndex].points.Length ? null :
            routes[routeIndex].points[returning ? routes[routeIndex].points.Length - 1 - cursor : cursor];
        public RouteNavigator(Route[] routes, bool safe)
        { this.routes = routes ?? Array.Empty<Route>(); routeIndex = safe && this.routes.Length > 1 ? 1 : 0; }
        public void Tick(float x, float y, bool isReturning, float dt, bool grounded = true, bool recoverFalls = false)
        {
            if (returning != isReturning)
            {
                // Only choose at the loot-side endpoint after completing the original lower route.
                // Observed scan/retreat memory is not knowledge of the opponent's future placement.
                if (isReturning && LearnReturnRoute && SawOutboundThreat && RouteId == "lower" &&
                    CompletedRoutes.Contains("Out:lower") && routes.Length == 2 && routes[1].id == "upper" &&
                    routes[0].points.Length > 0 && x >= routes[0].points.Last().x - 0.8f && grounded && ReturnDetourRequests == 0 && SwitchRequests < 2)
                { routeIndex = 1; ReturnDetourRequests++; SwitchRequests++; }
                returning = isReturning; partialRoute = false; cursor = 0; stalled = 0; best = float.MaxValue;
            }
            var target = Target;
            if (target == null) return;
            // Only new duel navigation opts in. Rewind to a lower authored landing after a real fall;
            // never skip the failed high target, teleport, or mark the recovery request as progress.
            if (recoverFalls && grounded && RouteId == "upper" && target.y - y > 1.25f && StairRecoveryRequests < 2)
            {
                var points = routes[routeIndex].points;
                for (int earlier = cursor - 1; earlier >= 0; earlier--)
                {
                    var step = points[returning ? points.Length - 1 - earlier : earlier];
                    if (step.y > y + 1.05f) continue;
                    cursor = earlier; StairRecoveryRequests++; best = float.MaxValue; stalled = 0;
                    return;
                }
            }
            float dx = target.x - x, dy = target.y - y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            if (grounded && Math.Abs(dx) < 0.45f && Math.Abs(dy) < 0.25f)
            {
                reached.Add(returning + ":" + RouteId + ":" + cursor);
                cursor++; best = float.MaxValue; stalled = 0;
                string key = (returning ? "Return:" : "Out:") + RouteId;
                if (Target == null && !partialRoute && !CompletedRoutes.Contains(key)) CompletedRoutes.Add(key);
                return;
            }
            if (distance < best - 0.25f) { best = distance; stalled = 0; }
            else stalled += Math.Max(0, dt);
            if (stalled < 4f || SwitchRequests >= 2 || routes.Length < 2) return;
            // Try the other authored route at most twice; do not skip a failed waypoint as success.
            routeIndex = (routeIndex + 1) % routes.Length; cursor = 0; partialRoute = false; SwitchRequests++;
            best = float.MaxValue; stalled = 0;
            // Lower lane is enterable anywhere. Upper lane must climb via the actual entry stairs.
            if (RouteId == "lower")
                while (Target != null && (returning ? Target.x > x + 0.8f : Target.x < x - 0.8f)) { cursor++; partialRoute = true; }
        }
    }

    public sealed class CounterplayMotion
    {
        private string previousPhase;
        private float startDistance, previousSide;
        private bool retreatCounted, crossingCounted;
        // Flags: 1 = measured retreat during a telegraph, 2 = nearby crossing during recovery.
        // Neither flag proves a scan hit, damage avoided, or an enjoyable encounter.
        public int Sample(string phase, float side, float heightDifference, float distance)
        {
            int flags = 0;
            if (phase != previousPhase)
            { startDistance = distance; retreatCounted = crossingCounted = false; }
            else
            {
                if (phase == "Telegraph" && !retreatCounted && distance <= 6f &&
                    heightDifference < 2f && distance > startDistance + 0.35f)
                { flags |= 1; retreatCounted = true; }
                if (phase == "Recovery" && !crossingCounted && heightDifference < 2f &&
                    distance < 3f && previousSide * side < 0)
                { flags |= 2; crossingCounted = true; }
            }
            previousPhase = phase;
            if (Math.Abs(side) > 0.01f) previousSide = side;
            return flags;
        }
    }

    [Serializable]
    public sealed class Matchup
    {
        public string mario, trickster;
        public string Id => mario == trickster ? mario : mario + " vs " + trickster;
    }
    public static readonly string[] RunnerStrategies = { "Runner", "Scout", "SafeRoute" };
    public static readonly string[] TricksterStrategies = { "Ambusher", "Baiter", "Chaser" };
    private static readonly Matchup[] legacyMatchups = Profiles.Select(p => new Matchup { mario = p, trickster = p }).ToArray();
    private static readonly Matchup[] experienceMatchups = RunnerStrategies.SelectMany(m =>
        TricksterStrategies.Select(t => new Matchup { mario = m, trickster = t })).ToArray();
    public static Matchup[] Matchups(Scenario scenario)
    {
        if (scenario.selectedMatchups != null && scenario.selectedMatchups.Length > 0) return scenario.selectedMatchups;
        if (scenario.tunnelVersion >= 1)
            return new[] { "Adaptive", "SafeRoute" }.SelectMany(m => new[] { "Passive", "GroundChaser", "TunnelChaser" }
                .Select(t => new Matchup { mario = m, trickster = t })).ToArray();
        if (scenario.counterplayVersion >= 1)
            return scenario.lootEscape
                ? new[] { "Adaptive", "SafeRoute" }.SelectMany(m => new[] { "Passive", "Chaser" }
                    .Select(t => new Matchup { mario = m, trickster = t })).ToArray()
                : new[] { "Runner", "Adaptive", "SafeRoute" }.Select(m => new Matchup { mario = m, trickster = "Passive" }).ToArray();
        return string.IsNullOrEmpty(scenario.experience) ? legacyMatchups : experienceMatchups;
    }
    public static int TrialCount(IEnumerable<Scenario> scenarios) => scenarios.Sum(s => Matchups(s).Length);

    public sealed class Slot
    {
        public Scenario scenario;
        public Matchup matchup;
        public int attempt;
    }
    public static Slot TrialAt(IList<Scenario> scenarios, IList<string> confirmations, int step)
    {
        if (step < 0) throw new ArgumentOutOfRangeException(nameof(step));
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            var rooms = attempt == 1 ? scenarios : (confirmations ?? Array.Empty<string>()).Select(id => scenarios.First(s => s.id == id));
            foreach (var room in rooms)
            {
                var matchups = Matchups(room);
                if (step < matchups.Length) return new Slot { scenario = room, matchup = matchups[step], attempt = attempt };
                step -= matchups.Length;
            }
        }
        throw new ArgumentOutOfRangeException(nameof(step));
    }

    // Explicit PRNG makes layout seeds independent of Mono/.NET Random implementation.
    private sealed class Dice
    {
        private uint state;
        public Dice(int seed) { state = unchecked((uint)seed) ^ 0x9e3779b9u; if (state == 0) state = 1; }
        public int Next(int count)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (int)(state % (uint)count);
        }
    }

    public static List<Scenario> Create(int seed, Scope scope)
    {
        if (!Enum.IsDefined(typeof(Scope), scope)) throw new ArgumentOutOfRangeException(nameof(scope));
        if (scope == Scope.TunnelDuel) return Enumerable.Range(0, 3).Select(i => BuildTunnel(seed, i)).ToList();
        if (scope == Scope.Experience) return Enumerable.Range(0, 3).Select(i => BuildExperience(unchecked(seed + i * 7919), i)).ToList();
        if (scope == Scope.Counterplay)
        {
            var rooms = new List<Scenario>();
            for (int kind = 1; kind <= 2; kind++)
                for (int timing = 0; timing < 3; timing++)
                {
                    var room = BuildExperience(unchecked(seed + kind * 7919), kind);
                    room.counterplayVersion = 1; room.startDelaySeconds = timing * 0.6f;
                    room.id += "_timing" + timing;
                    room.intention += " 专项对照：Mario普通中立输入等待" + room.startDelaySeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                        "秒后出发；相同布局/参数，不是独立新地图。Passive保留对手实体但不发送行动输入。";
                    rooms.Add(room);
                }
            return rooms;
        }
        var dice = new Dice(seed);
        var order = Catalog.ToCharArray();
        for (int i = order.Length - 1; i > 0; i--)
        { int j = dice.Next(i + 1); char c = order[i]; order[i] = order[j]; order[j] = c; }
        var combinations = new List<string>();
        int singles = scope == Scope.Smoke ? 3 : order.Length;
        for (int i = 0; i < singles; i++) combinations.Add(order[i].ToString());
        if (scope == Scope.Pairwise)
            for (int i = 0; i < order.Length; i++)
                for (int j = i + 1; j < order.Length; j++) combinations.Add(new string(new[] { order[i], order[j] }));
        else
            for (int i = 0; i < (scope == Scope.Smoke ? 3 : order.Length); i++)
                combinations.Add(new string(new[] { order[i], order[(i + 1) % order.Length] }));
        var result = new List<Scenario>();
        for (int i = 0; i < combinations.Count; i++)
            result.Add(Build(unchecked(seed + i * 7919), combinations[i], i));
        return result;
    }

    public static Scenario Build(int seed, string mechanisms, int index = 0)
    {
        if (string.IsNullOrEmpty(mechanisms) || mechanisms.Length > 2 ||
            mechanisms.Distinct().Count() != mechanisms.Length || mechanisms.Any(c => !Catalog.Contains(c)))
            throw new ArgumentException("Select one or two distinct supported mechanisms", nameof(mechanisms));
        var dice = new Dice(seed);
        const int width = 40, height = 10;
        var rows = Enumerable.Range(0, height).Select(_ => new string('.', width).ToCharArray()).ToArray();
        Action<int, int, char> put = (x, y, c) => rows[height - 1 - y][x] = c;
        for (int x = 0; x < width; x++) put(x, 0, '#');
        put(2, 1, 'M'); put(7, 1, 'T'); put(37, 1, 'G');
        // Safe approach -> shared challenge chamber -> recovery -> goal.
        // Optional upper route gives both sides a choice, not a promise of physical reachability.
        bool upperRoute = mechanisms.Length == 2;
        if (upperRoute)
        {
            // One-way stairs keep the lower lane open; three clear cells above a B at y=1.
            put(8, 1, '-'); put(9, 2, '-'); put(10, 3, '-');
            for (int x = 11; x <= 25; x++) put(x, 4, '-');
            put(26, 3, '-'); put(27, 2, '-'); put(28, 1, '-');
        }
        var metadata = new StringBuilder();
        for (int i = 0; i < mechanisms.Length; i++)
        {
            char c = mechanisms[i];
            int x = (i == 0 ? 13 : 21) + dice.Next(3) - 1;
            int y = 1;
            if (c == 'P') y = 6; // pendulum anchor, never embedded in the floor
            else if (c == 'f') y = 4;
            else if (c == 'X') y = 2;
            else if (c == '-' || c == 'C')
            {
                y = 2;
                put(x - 2, 1, '=');
                put(x - 1, 2, '-'); put(x + 1, 2, '-');
            }
            else if (c == '<') y = 0;
            put(x, y, c);
            if (c == 'B' && upperRoute)
                for (int clearX = x - 1; clearX <= x + 1; clearX++) put(clearX, 4, '.');
            if (c == '>') metadata.Append($"\n# Override_{x}_{y}: pointB={x + 3},{y}");
        }
        bool loot = mechanisms.Contains('o');
        string grid = string.Join("\n", rows.Select(row => new string(row)));
        return new Scenario {
            seed = seed, id = $"v{Version}_{index:D3}_{unchecked((uint)seed):x8}",
            mechanisms = mechanisms, ascii = grid + metadata, lootEscape = loot,
            intention = upperRoute ? "观察双机制交互；上路绕行、下路接触，保留反制与喘息区。" : "单机制探针：先看懂，再接触；AI 未触发不等于机制通过。"
        };
    }

    public static Scenario BuildExperience(int seed, int kind)
    {
        if (kind < 0 || kind > 2) throw new ArgumentOutOfRangeException(nameof(kind));
        const int width = 44, height = 12;
        var rows = Enumerable.Range(0, height).Select(_ => new string('.', width).ToCharArray()).ToArray();
        Action<int, int, char> put = (x, y, c) => rows[height - 1 - y][x] = c;
        int shift = new Dice(seed).Next(3);
        for (int x = 0; x < width; x++) put(x, 0, '#');
        put(kind == 2 ? 5 : 2, 1, 'M'); put(17 + shift, 1, 'T');
        put(kind == 2 ? 3 : 41, 1, 'G');
        // Spaced steps allow a stable landing without the next underside overlapping the body.
        // The upper lane stays open: avoiding an ambush is a valid choice, not missing contact.
        put(7, 1, '-'); put(9, 2, '-'); put(11, 3, '-');
        for (int x = 13; x <= 29; x++) put(x, 4, '-');
        put(31, 3, '-'); put(33, 2, '-'); put(35, 1, '-');
        // Start beside the first usable anchor, allowing the real 1.5s blend before a rush.
        // Nearby second anchors support ordinary directional switching (no forced possession).
        put(18 + shift, 1, '[');
        if (kind == 0) put(22 + shift, 1, 'F');
        if (kind == 1) { put(22 + shift, 1, ']'); put(28, 1, 'F'); }
        if (kind == 2) { put(22 + shift, 1, 'F'); put(28, 1, '['); put(38, 1, 'o'); }
        // Targets are root positions at rest, not sprite centers or cell-top guesses.
        float standing = PhysicsMetrics.MARIO_COLLIDER_HEIGHT * 0.5f - PhysicsMetrics.MARIO_COLLIDER_OFFSET_Y;
        float platformStanding = PhysicsMetrics.ONEWAY_COLLIDER_SIZE.y * 0.5f + standing;
        float groundStanding = 0.5f + standing;
        var upper = new System.Collections.Generic.List<Point> {
            new Point(7, 1 + platformStanding), new Point(9, 2 + platformStanding),
            new Point(11, 3 + platformStanding), new Point(13, 4 + platformStanding),
            new Point(17, 4 + platformStanding), new Point(23, 4 + platformStanding),
            new Point(29, 4 + platformStanding), new Point(31, 3 + platformStanding),
            new Point(33, 2 + platformStanding), new Point(35, 1 + platformStanding),
            new Point(37, groundStanding)
        };
        return new Scenario {
            seed = seed, id = $"v{Version}_experience_{kind}_{unchecked((uint)seed):x8}",
            experience = new[] { "RiskOrDetour", "BaitAndCounter", "LootAndReturn" }[kind],
            mechanisms = kind == 2 ? "[Fo" : kind == 1 ? "[]F" : "[F", lootEscape = kind == 2,
            ascii = string.Join("\n", rows.Select(row => new string(row))),
            intention = new[] {
                "短路抢时间或爬高绕行；比较真实路线、预警退让和通关代价。",
                "封路预警可扫描缩短；公开队列显示当前/下一状态，可等安全窗或走高路。",
                "深入右端拿宝，再返回左端撤离；对手可换点追击，去程进度不能代替返程。"
            }[kind],
            routes = new[] {
                new Route { id = "lower", points = new[] { new Point(11, groundStanding), new Point(24, groundStanding), new Point(35, groundStanding) }, minX = 14, maxX = 29, minY = 0.5f, maxY = 2.6f },
                new Route { id = "upper", points = upper.ToArray(), minX = 14, maxX = 29, minY = 4 + platformStanding - 0.3f, maxY = 7.5f }
            }
        };
    }

    [Serializable]
    public sealed class TunnelLink
    {
        public Point from, to;
        public float seconds = 0.8f;
    }

    // Three authored hypotheses, not random obstacle soup or an automatic fun optimizer.
    // Adjacent seeds shift the ambush cluster; each variant keeps the ordinary upper route.
    public static Scenario BuildTunnel(int seed, int variant)
    {
        if (variant < 0 || variant > 2) throw new ArgumentOutOfRangeException(nameof(variant));
        var room = BuildExperience(seed, 2);
        var rows = room.ascii.Split('\n').Select(r => r.ToCharArray()).ToArray();
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
                if ("[FT".Contains(rows[y][x])) rows[y][x] = '.';
        int shift = new Dice(seed).Next(3);
        int left = 17 + shift, middle = 23 + shift, right = 29 + shift;
        Action<int, int, char> put = (x, y, c) => rows[rows.Length - 1 - y][x] = c;
        put(left - 1, 1, 'T'); put(left, 1, '['); put(middle, 1, 'F'); put(right, 1, '[');
        var links = new List<TunnelLink>();
        Action<Point, Point, float> connect = (a, b, seconds) => {
            links.Add(new TunnelLink { from = a, to = b, seconds = seconds });
            links.Add(new TunnelLink { from = b, to = a, seconds = seconds });
        };
        var a0 = new Point(left, 1); var a1 = new Point(middle, 1); var a2 = new Point(right, 1);
        connect(a0, a1, 0.8f); connect(a1, a2, 0.8f);
        if (variant == 1)
        {
            // A side exit on the existing upper deck, not a mandatory obstacle or new damage source.
            put(middle, 5, 'F'); connect(a1, new Point(middle, 5), 0.8f);
        }
        if (variant == 2)
        {
            // No collinear competing edge: native directional selection must reach the requested exit.
            links.Clear(); connect(a0, a2, 0.8f);
        }
        room.id = $"v{Version}_tunnel_{variant}_{unchecked((uint)seed):x8}";
        room.experience = "TunnelDuel"; room.tunnelVersion = 1; room.counterplayVersion = 0;
        room.startDelaySeconds = 1.2f; // Same ordinary preparation window for all automated matchups.
        room.ascii = string.Join("\n", rows.Select(r => new string(r)));
        room.tunnelLinks = links.ToArray();
        room.designQuestion = new[] {
            "串联暗线：转移是否带来新的出手机会，还是只在空跑？",
            "上层出口：增加一个换层机会后，上路是否仍是有代价且可用的选择？",
            "回包暗线：首尾直连替代串联后，拿宝返程是否出现真实换位与再交手？"
        }[variant];
        room.intention = room.designQuestion + " 两条明路、拿宝返程、原生暗线方向键转移；不改伤害/物理/能量。" +
            "静止/地面追击/暗线追击各对照下路Adaptive与上路SafeRoute。启发式Bot和作者路点，不是人类隐藏推理或乐趣验收。";
        return room;
    }

    /// <summary>Seeded two-lane duel room. Finite authored grammar, not arbitrary terrain or learned fun.</summary>
    public static Scenario BuildDuel(int seed, int variant = 0)
    {
        if (variant < 0 || variant > 2) throw new ArgumentOutOfRangeException(nameof(variant));
        var dice = new Dice(seed);
        int width = 48 + 2 * dice.Next(7), height = 12;
        int deckStart = 13 + 2 * dice.Next(2), deckEnd = width - 15 - 2 * dice.Next(2);
        int left = deckStart + 2 + dice.Next(2), middle = (deckStart + deckEnd) / 2 + dice.Next(3) - 1;
        int right = deckEnd - 1 - dice.Next(2);
        var rows = Enumerable.Range(0, height).Select(_ => new string('.', width).ToCharArray()).ToArray();
        Action<int, int, char> put = (x, y, c) => rows[height - 1 - y][x] = c;
        for (int x = 0; x < width; x++) put(x, 0, '#');
        for (int x = deckStart; x <= deckEnd; x++) put(x, 4, '-');
        for (int i = 1; i <= 3; i++) { put(deckStart - 2 * i, 4 - i, '-'); put(deckEnd + 2 * i, 4 - i, '-'); }
        put(5, 1, 'M'); put(3, 1, 'G'); put(deckEnd + 9, 1, 'o');
        put(left - 1, 1, 'T'); put(left, 1, '['); put(middle, 1, 'F'); put(right, 1, '[');
        put(middle, 5, 'F'); // Surface exit exists in every variant; only its link changes.
        var a = new Point(left, 1); var b = new Point(middle, 1); var c0 = new Point(right, 1); var upperExit = new Point(middle, 5);
        var links = new List<TunnelLink>();
        Action<Point, Point> connect = (p, q) => {
            links.Add(new TunnelLink { from = p, to = q, seconds = 0.8f });
            links.Add(new TunnelLink { from = q, to = p, seconds = 0.8f });
        };
        // Never place two outgoing edges on the same ray: both must be selectable by native input.
        if (variant == 0) connect(a, c0); // Long flank: leave before the runner passes, rather than chase behind.
        else { connect(a, b); connect(b, c0); }
        connect(variant == 1 ? b : a, upperExit);
        float standing = PhysicsMetrics.MARIO_COLLIDER_HEIGHT * 0.5f - PhysicsMetrics.MARIO_COLLIDER_OFFSET_Y;
        float ground = 0.5f + standing, platform = PhysicsMetrics.ONEWAY_COLLIDER_SIZE.y * 0.5f + standing;
        var upper = new List<Point>();
        for (int i = 3; i >= 1; i--) upper.Add(new Point(deckStart - 2 * i, 4 - i + platform));
        foreach (int x in new[] { deckStart, left, middle, right, deckEnd }.Distinct().OrderBy(x => x)) upper.Add(new Point(x, 4 + platform));
        for (int i = 1; i <= 3; i++) upper.Add(new Point(deckEnd + 2 * i, 4 - i + platform));
        upper.Add(new Point(deckEnd + 8, ground));
        string question = new[] { "提前回包：长暗线能否赶在返程前准备，而不是追在身后？", "分段换位：中继和地表出口是否提供有效出手机会？", "侧翼出口：连接地表的入口换到左侧，是否改变双方的选择？" }[variant];
        return new Scenario {
            seed = seed, id = $"v{Version}_duel1_{variant}_{unchecked((uint)seed):x8}",
            duelVersion = 1, duelVariant = variant, tunnelVersion = 1, experience = "TunnelDuel",
            lootEscape = true, mechanisms = "[Fo", startDelaySeconds = 1.2f,
            ascii = string.Join("\n", rows.Select(r => new string(r))), tunnelLinks = links.ToArray(),
            designQuestion = question,
            intention = question + " 地表绕行/下层短路/原生暗线。种子改变宽度、台阶及机关位置；不是挖土或地形破坏。双方用真实按键，不以损血或通关率评乐趣。",
            routes = new[] {
                new Route { id = "lower", points = new[] { new Point(deckStart - 2, ground), new Point(middle + 1, ground), new Point(deckEnd + 6, ground) }, minX = deckStart + 1, maxX = deckEnd, minY = 0.5f, maxY = 2.6f },
                new Route { id = "upper", points = upper.ToArray(), minX = deckStart + 1, maxX = deckEnd, minY = 4 + platform - 0.3f, maxY = 7.5f }
            }
        };
    }

    // New grammar is explicitly versioned: never regenerate saved duelVersion=1 maps as caves.
    public static Scenario BuildCavernDuel(int seed, int variant = 0)
    {
        if (variant < 0 || variant > 2) throw new ArgumentOutOfRangeException(nameof(variant));
        var dice = new Dice(seed);
        int width = 72 + 2 * dice.Next(5), height = 16;
        int left = 18 + dice.Next(2), right = width - 19 - dice.Next(2), shaft = (left + right) / 2;
        var rows = Enumerable.Range(0, height).Select(_ => new string('.', width).ToCharArray()).ToArray();
        Action<int, int, char> put = (x, y, c) => rows[height - 1 - y][x] = c;
        for (int x = 0; x < width; x++) put(x, 0, '#');
        // Two solid earth roofs, not another floating one-way platform. Central open shaft is a
        // physical connection; its one-way bridge can be dropped through using normal S+Jump.
        for (int x = left; x <= right; x++)
            if (Math.Abs(x - shaft) > 3) { put(x, 5, '#'); put(x, 6, '#'); }
        for (int x = shaft - 3; x <= shaft + 3; x++) put(x, 6, '-');
        for (int i = 1; i <= 5; i++)
        { put(left - 2 * i, 6 - i, '-'); put(right + 2 * i, 6 - i, '-'); }
        // Central shaft ladder: genuine grounded landings, shared by players, no teleport.
        for (int y = 1; y <= 5; y++) put(shaft + (y % 2 == 0 ? 2 : -2), y, '-');
        // Two lookout rises break up the surface silhouette; each has a one-unit approach.
        int ridgeA = left + 5, ridgeB = right - 5;
        foreach (int ridge in new[] { ridgeA, ridgeB })
        { put(ridge - 1, 7, '#'); put(ridge, 7, '#'); put(ridge, 8, '#'); put(ridge + 1, 7, '#'); }
        put(5, 1, 'M'); put(3, 1, 'G'); put(right + 12, 1, 'o');
        var a = new Point(left + 2, 1); var b = new Point(right - 2, 1);
        var c = new Point(left + 9, 7); var d = new Point(right - 9, 7);
        put((int)a.x - 1, 1, 'T');
        foreach (var p in new[] { a, b, c, d }) put((int)p.x, (int)p.y, 'F');
        var links = new List<TunnelLink>();
        Action<Point, Point> connect = (p, q) => {
            links.Add(new TunnelLink { from = p, to = q, seconds = 0.8f });
            links.Add(new TunnelLink { from = q, to = p, seconds = 0.8f });
        };
        connect(a, c); connect(b, d); connect(c, d);
        if (variant == 0) connect(a, b);
        else if (variant == 1) connect(a, d);
        else connect(b, c);
        float standing = PhysicsMetrics.MARIO_COLLIDER_HEIGHT * 0.5f - PhysicsMetrics.MARIO_COLLIDER_OFFSET_Y;
        float ground = 0.5f + standing, platform = PhysicsMetrics.ONEWAY_COLLIDER_SIZE.y * 0.5f + standing;
        var upper = new List<Point>();
        for (int i = 5; i >= 1; i--) upper.Add(new Point(left - 2 * i, 6 - i + platform));
        upper.Add(new Point(left, 6 + ground));
        foreach (int ridge in new[] { ridgeA, ridgeB })
        {
            upper.Add(new Point(ridge - 2, 6 + ground)); upper.Add(new Point(ridge - 1, 7 + ground));
            upper.Add(new Point(ridge, 8 + ground)); upper.Add(new Point(ridge + 1, 7 + ground));
            upper.Add(new Point(ridge + 2, 6 + ground));
            if (ridge == ridgeA) upper.Add(new Point(shaft, 6 + platform));
        }
        upper.Add(new Point(right, 6 + ground));
        for (int i = 1; i <= 5; i++) upper.Add(new Point(right + 2 * i, 6 - i + platform));
        upper.Add(new Point(right + 11, ground));
        string question = new[] {
            "双洞室与通风井：地下近路、地表岗台、扫描后返程改走另一层，能否形成两轮选择？",
            "只把地下长边改成左下到右上斜线：是否更易反包地表，但丢失地下返程机会？",
            "只把斜线换到右下到左上：是否改变撤离侧的准备，而不是增加陷阱数量？"
        }[variant];
        return new Scenario {
            seed = seed, id = $"v{Version}_duel2_{variant}_{unchecked((uint)seed):x8}",
            duelVersion = 2, duelVariant = variant, tunnelVersion = 1, experience = "TunnelDuel",
            lootEscape = true, mechanisms = "Fo", startDelaySeconds = 1.2f,
            ascii = string.Join("\n", rows.Select(r => new string(r))), tunnelLinks = links.ToArray(),
            designQuestion = question,
            intention = question + " 两段实体土层/洞室、两侧爬坡和中央井；四个可操控假墙，不堆伤害陷阱。下路AI只在实际扫描命中或预警退让后记住风险，并在拿宝端点选择地表返程；地表AI固定两程作对照。对手仍为已知位置启发式，暗线请求不保证出手；不是自由挖土或自主学习。",
            routes = new[] {
                new Route { id = "lower", points = new[] { new Point(left - 2, ground), new Point(shaft, ground), new Point(right + 11, ground) }, minX = left, maxX = right, minY = 0.5f, maxY = 3f },
                new Route { id = "upper", points = upper.ToArray(), minX = left, maxX = right, minY = 6 + platform - 0.3f, maxY = 11f }
            }
        };
    }

    public static List<Scenario> BuildWallTacticsComparison(int seed)
    {
        var baseline = BuildCavernDuel(seed); var treatment = BuildCavernDuel(seed);
        baseline.id += "_observe"; treatment.id += "_tactics1"; treatment.wallTacticsVersion = 1;
        baseline.designQuestion = "同图策略对照A：原输入策略，额外只观察墙窗口。";
        treatment.designQuestion = "同图策略对照B：仅地下Adaptive启用公开墙窗口退让/再穿越；地图、对手与返程记忆保持相同。";
        return new List<Scenario> { baseline, treatment };
    }

    public static string[] WallEvidenceIssues(Trial t)
    {
        if (t == null || t.wallEvidenceVersion < 1) return new[] { "未记录墙窗口观察" };
        if (!IsFinite(t.seconds) || t.seconds <= 0 || t.wallEpisodes == null || t.wallEpisodes.Count > 16 || (t.wallPolicy != "ObserveOnly" && t.wallPolicy != "VisibleWallWindowV1"))
            return new[] { "墙窗口结构或策略标记缺失" };
        var seen = new HashSet<string>();
        foreach (var e in t.wallEpisodes)
        {
            if (e == null || string.IsNullOrEmpty(e.source) || (e.leg != "Out" && e.leg != "Return") || !seen.Add(e.source + ":" + e.leg) ||
                !IsFinite(e.beganAt) || e.beganAt < 0 || !IsFinite(e.endedAt) || e.endedAt < e.beganAt || e.endedAt > t.seconds + 0.1f ||
                !IsFinite(e.inputAt) || !IsFinite(e.crossedAt) || e.inputFrames < 0 ||
                (e.inputFrames == 0 ? e.inputAt != -1f : e.inputAt < e.beganAt || e.inputAt > e.endedAt) ||
                (e.crossedAt != -1f && (e.crossedAt < e.beganAt || e.crossedAt > e.endedAt)) ||
                !new[] { "CrossedAfterReopen", "PassedWithoutReopen", "LostCue", "BudgetExpired", "TrialEnded" }.Contains(e.outcome) ||
                (e.sawReopen && !e.sawSolid) ||
                ((e.outcome == "CrossedAfterReopen" || e.outcome == "PassedWithoutReopen") != (e.crossedAt >= 0)) ||
                (e.outcome == "CrossedAfterReopen" && (!e.sawSolid || !e.sawReopen || e.crossedAt < 0)) ||
                (t.wallPolicy == "ObserveOnly" && e.inputFrames > 0))
                return new[] { "墙窗口时序/同源/输入证据不一致，不能算策略成功" };
        }
        return Array.Empty<string>();
    }

    // Called only after the runner has checked complete real-match evidence. Do not mutate the parent.
    public static Scenario NextDuelVariant(Scenario parent, string reason)
    {
        if (parent == null || (parent.duelVersion != 1 && parent.duelVersion != 2) || parent.iteration < 0 || parent.iteration >= 2)
            throw new InvalidOperationException("本轮最多两次连接变体；先由真人复盘，不无限刷关卡。");
        if (!IsGeneratedDuelLayout(parent) || string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("手工改图或缺少对战理由：不能静默覆盖为生成器布局。");
        var child = parent.duelVersion == 2 ? BuildCavernDuel(parent.seed, (parent.duelVariant + 1) % 3) : BuildDuel(parent.seed, (parent.duelVariant + 1) % 3);
        child.wallTacticsVersion = parent.wallTacticsVersion;
        if (child.wallTacticsVersion == 1) child.id += "_tactics1";
        child.iteration = parent.iteration + 1;
        child.parentScenarioId = parent.id;
        child.id += "_iteration" + child.iteration;
        child.mutationReason = reason;
        child.designQuestion = "连接变体 " + child.iteration + "（待验证）：" + child.designQuestion;
        return child;
    }

    public static bool IsGeneratedDuelLayout(Scenario room)
    {
        if (room == null || (room.duelVersion != 1 && room.duelVersion != 2) || room.duelVariant < 0 || room.duelVariant > 2) return false;
        var canonical = room.duelVersion == 2 ? BuildCavernDuel(room.seed, room.duelVariant) : BuildDuel(room.seed, room.duelVariant);
        Func<Point, Point, bool> samePoint = (a, b) => a != null && b != null && a.x == b.x && a.y == b.y;
        return room.wallTacticsVersion >= 0 && room.wallTacticsVersion <= 1 && (room.duelVersion == 2 || room.wallTacticsVersion == 0) &&
            room.ascii == canonical.ascii && room.lootEscape && room.startDelaySeconds == canonical.startDelaySeconds &&
            room.mechanisms == canonical.mechanisms && room.tunnelVersion == canonical.tunnelVersion && room.counterplayVersion == 0 &&
            room.routes != null && room.routes.Length == canonical.routes.Length &&
            room.routes.Zip(canonical.routes, (a, b) => a != null && a.id == b.id && a.minX == b.minX && a.maxX == b.maxX && a.minY == b.minY && a.maxY == b.maxY &&
                a.points != null && a.points.Length == b.points.Length && a.points.Zip(b.points, samePoint).All(same => same)).All(same => same) &&
            room.tunnelLinks != null && room.tunnelLinks.Length == canonical.tunnelLinks.Length &&
            room.tunnelLinks.Zip(canonical.tunnelLinks, (a, b) => a != null && samePoint(a.from, b.from) && samePoint(a.to, b.to) && a.seconds == b.seconds).All(same => same);
    }

    // Preparation is a different decision from a timely intercept. Observe a grounded runner on
    // another layer, not their strategy name, future waypoints or loot phase. A request may be wasted.
    public static float TunnelLayerPreparationScore(float runnerX, float runnerY, float velocityX, bool grounded,
        float fromX, float fromY, float exitX, float exitY)
    {
        if (!grounded || !IsFinite(runnerX) || !IsFinite(runnerY) || !IsFinite(velocityX) ||
            !IsFinite(fromX) || !IsFinite(fromY) || !IsFinite(exitX) || !IsFinite(exitY) || Math.Abs(velocityX) < 0.5f)
            return -1f;
        double dx = (double)runnerX - fromX, dy = (double)runnerY - fromY;
        // A landed ascent/descent is enough to prepare along that layer change; waiting until the
        // runner reaches the exit's layer would again confuse preparation with last-second interception.
        if (dx * dx + dy * dy > 144 || Math.Abs(runnerY - fromY) <= 1.5f || Math.Abs(exitY - fromY) <= 1.5f ||
            ((double)runnerY - fromY) * ((double)exitY - fromY) <= 0 || Math.Abs(runnerY - exitY) > 3f) return -1f;
        float ahead = (exitX - runnerX) * Math.Sign(velocityX);
        return ahead >= 2f && ahead <= 20f ? ahead : -1f;
    }

    public sealed class TunnelPreparationBudget
    {
        public int Requests { get; private set; }
        private float cooldown;
        public bool Available => Requests < 2 && cooldown <= 0f;
        public void Tick(float dt)
        { if (IsFinite(dt) && dt > 0f) cooldown = Math.Max(0f, cooldown - dt); }
        public bool TryReserve()
        {
            if (!Available) return false;
            Requests++; cooldown = 8f; return true;
        }
    }

    // Conservative same-lane ETA: travel + re-blending + telegraph + reaction must finish before passage.
    // This is a known-position heuristic, not hidden-intent inference or proof of a successful attack.
    public static float TunnelAmbushWindow(float runnerX, float runnerY, float velocityX, float exitX, float exitY,
        float transit, float blend, float telegraph)
    {
        if (!IsFinite(runnerX) || !IsFinite(runnerY) || !IsFinite(velocityX) || !IsFinite(exitX) || !IsFinite(exitY) ||
            !IsFinite(transit) || !IsFinite(blend) || !IsFinite(telegraph) ||
            Math.Abs(velocityX) < 0.5f || Math.Abs(runnerY - exitY) > 1.5f || transit < 0 || blend < 0 || telegraph < 0) return -1f;
        float eta = (exitX - runnerX) / velocityX;
        float spare = eta - (transit + blend + telegraph + 0.35f);
        return spare >= 0f && spare <= 4f ? spare : -1f;
    }

    // A brief brake at an authored waypoint must not create a fictitious long interception window.
    // Use the public movement limit/observed peak as a conservative speed envelope, never hidden route intent.
    public static float TunnelPlanningVelocity(float velocityX, float movementLimit, float observedPeak)
    {
        if (!IsFinite(velocityX) || !IsFinite(movementLimit) || !IsFinite(observedPeak) ||
            movementLimit <= 0f || observedPeak < 0f || Math.Abs(velocityX) < 0.5f) return 0f;
        return Math.Sign(velocityX) * Math.Max(Math.Abs(velocityX), Math.Max(movementLimit, observedPeak));
    }

    [Serializable]
    public sealed class TunnelVisit
    {
        public string origin, destination;
        public float x, y, arrivalAt, direction;
        public float readyAt = -1f, firstControlAt = -1f, returnControlAt = -1f, endedAt = -1f;
        public bool runnerPassedWhenReady;
        public bool ObserveReady(float now, float runnerX, float runnerY, bool armed, bool blended)
        {
            // Arrival callbacks may still see the old blended flag until the next engine Update.
            if (endedAt >= 0f || readyAt >= 0f || !armed || !blended || !IsFinite(now) ||
                !IsFinite(runnerX) || !IsFinite(runnerY) || now < arrivalAt + 0.05f) return false;
            readyAt = now;
            runnerPassedWhenReady = Math.Abs(runnerY - y) <= 1.5f && Math.Abs(direction) > 0.5f &&
                (runnerX - x) * direction > 0.8f;
            return true;
        }
        public bool RecordControl(float now, bool returning)
        {
            if (endedAt >= 0f || !IsFinite(now) || now < arrivalAt) return false;
            bool changed = false;
            if (firstControlAt < 0f) { firstControlAt = now; changed = true; }
            if (returning && returnControlAt < 0f) { returnControlAt = now; changed = true; }
            return changed;
        }
    }

    public static string[] TunnelPlanIssues(Scenario room)
    {
        if (room.tunnelVersion < 1) return Array.Empty<string>();
        var errors = new List<string>();
        var rows = (room.ascii ?? "").Split('\n');
        var links = room.tunnelLinks ?? Array.Empty<TunnelLink>();
        Func<Point, bool> valid = p => p != null && IsFinite(p.x) && IsFinite(p.y) &&
            p.x == (int)p.x && p.y == (int)p.y && p.y >= 0 && p.y < rows.Length &&
            p.x >= 0 && p.x < rows[rows.Length - 1 - (int)p.y].Length &&
            "[F".Contains(rows[rows.Length - 1 - (int)p.y][(int)p.x]);
        if (links.Length == 0) errors.Add("暗线计划没有连接");
        var keys = new HashSet<string>();
        foreach (var link in links)
        {
            if (link == null || !valid(link.from) || !valid(link.to) || !IsFinite(link.seconds) || link.seconds < 0.1f || link.seconds > 5f)
            { errors.Add("暗线端点/时间非法，必须对应实际锚点"); continue; }
            string key = $"{link.from.x},{link.from.y}>{link.to.x},{link.to.y}";
            if (!keys.Add(key) || (link.from.x == link.to.x && link.from.y == link.to.y)) errors.Add("暗线重复或自环");
            if (!links.Any(l => l != null && l.from != null && l.to != null && l.from.x == link.to.x && l.from.y == link.to.y &&
                l.to.x == link.from.x && l.to.y == link.from.y)) errors.Add("缺少返程连接");
            if (links.Any(l => l != null && l.to != null && l.to.x == link.to.x && l.to.y == link.to.y && l.seconds != link.seconds))
                errors.Add("原生暗线时间按目的锚点配置，不能混用边时间");
        }
        return errors.Distinct().ToArray();
    }

    public static string[] CavernRouteIssues(Trial t)
    {
        if (t == null || t.cavernEvidenceVersion < 1) return new[] { "缺少洞室观察字段" };
        var gaps = new List<string>();
        string outward = t.marioStrategy == "SafeRoute" ? "upper" : "lower";
        string home = t.returnDetourRequests > 0 ? "upper" : outward;
        if (t.completedRoutes == null || !t.completedRoutes.Contains("Out:" + outward)) gaps.Add("去程路线未完整");
        if (t.completedRoutes == null || !t.completedRoutes.Contains("Return:" + home)) gaps.Add("返程路线未完整；改道请求不算完成");
        if (!IsFinite(t.undergroundSeconds) || !IsFinite(t.surfaceSeconds) || t.undergroundSeconds < 0 || t.surfaceSeconds < 0 ||
            (outward == "lower" && t.undergroundSeconds <= 0) || ((outward == "upper" || home == "upper") && t.surfaceSeconds <= 0)) gaps.Add("地下/地表观察时长缺失或非法");
        if (t.returnDetourRequests < 0 || t.returnDetourRequests > 1 ||
            (t.returnDetourRequests > 0 && (!t.outboundThreatRemembered || t.marioStrategy != "Adaptive"))) gaps.Add("改道缺少公开风险依据");
        return gaps.ToArray();
    }

    // Requests, state transitions and verified arrivals are separate evidence layers.
    public static string[] TunnelTrialIssues(Trial t)
    {
        if (t.tunnelVersion < 1) return Array.Empty<string>();
        var gaps = new List<string>();
        if (t.tunnelEvidenceVersion < 1) gaps.Add("暗线实际到达证据未记录");
        if (t.tricksterStrategy == "TunnelChaser" && t.tunnelArrivals == 0) gaps.Add("暗线未观察到真实异点到达；请求和普通换点不充数");
        if ((t.tricksterStrategy == "GroundChaser" || t.tricksterStrategy == "Passive") && t.tunnelStarts > 0) gaps.Add("地面追击基线发生暗线转移，不能配对");
        if (!IndependentStartObserved(t)) gaps.Add("起步阶段缺少独立对手决策证据");
        if (!PassiveControlClean(t)) gaps.Add("静止对手基线受到行动污染");
        return gaps.ToArray();
    }

    public static string PairKey(char a, char b) => a < b ? $"{a}{b}" : $"{b}{a}";
    public static string[] MissingFromCatalog(IEnumerable<char> registryChars) => registryChars
        .Where(c => !" .#=WMTG".Contains(c) && !Catalog.Contains(c)).Select(c => c.ToString()).Distinct().ToArray();

    // Missing evidence is actionable, not a fun score or a requirement that every safe detour fight.
    public static string[] ExperienceIssues(Trial t, Scenario scenario = null)
    {
        if (t.controlMode == "HumanMario" || t.controlMode == "HumanTrickster") return Array.Empty<string>();
        if (string.IsNullOrEmpty(scenario != null ? scenario.experience : t.experience)) return Array.Empty<string>();
        bool expectsReturn = scenario != null ? scenario.lootEscape : t.expectsReturn;
        var gaps = new List<string>();
        if (t.experienceEvidenceVersion < 1) gaps.Add("旧报告未记录完整路线/交手机会；不能补算通过");
        if (t.cavernEvidenceVersion >= 1 || (scenario != null && scenario.duelVersion == 2)) gaps.AddRange(CavernRouteIssues(t));
        if (t.marioStrategy == "SafeRoute")
        {
            if (t.completedRoutes == null || !t.completedRoutes.Contains("Out:upper")) gaps.Add("安全上路去程未完整到达所有落地点");
            if (expectsReturn && (t.completedRoutes == null || !t.completedRoutes.Contains("Return:upper"))) gaps.Add("安全上路返程未完整到达所有落地点");
        }
        else if (t.tricksterStrategy != "Passive" && t.armedNearbySeconds <= 0f && !(t.scanEvidenceVersion >= 1 && t.scanHits > 0))
            gaps.Add(t.scanEvidenceVersion < 1 && t.scans > 0
                ? "缺少扫描结果证据：可能提前揭穿，不能仅按就绪时间判定没有对抗"
                : "未观察到近距附身就绪或扫描命中；检查接近路线与准备时机");
        if (expectsReturn && (t.lootEvents == 0 || t.escapeEvents == 0)) gaps.Add("拿宝与撤离事件未成对出现");
        if (t.tricksterStrategy == "Chaser" && expectsReturn && t.possessionTransfers == 0)
            gaps.Add("换点追击尚无不同锚点成功证据（请求不算成功）");
        int diagnosticVersion = scenario != null ? scenario.counterplayVersion : t.counterplayVersion;
        if (diagnosticVersion >= 1 && !IndependentStartObserved(t))
            gaps.Add("Mario起步等待期间对手有效决策帧不足；不能视为独立启动对照");
        if (diagnosticVersion >= 1 && expectsReturn && t.tricksterStrategy == "Chaser" && t.postLootTransfers == 0)
            gaps.Add("拿宝后的实际换点尚未出现；去程换点不证明返程追击");
        if (diagnosticVersion >= 1 && !PassiveControlClean(t))
            gaps.Add("静止对手对照出现附身/操控，不能作为干净基线");
        if (diagnosticVersion >= 1 && !expectsReturn && t.marioStrategy == "Adaptive")
        {
            if (t.queueEvidenceVersion < 1 || t.queueEvidence.Count == 0) gaps.Add("公开队列专项缺少观察器证据");
            else if (t.queueEvidenceVersion >= 2 ? t.queueEvidence.Sum(e => e.cleanEncounters) == 0 : t.queueEvidence.Sum(e => e.cleanCrossings) == 0)
                gaps.Add(t.queueEvidenceVersion >= 2 ? "尚无整次遭遇无伤完成；重入后的无伤片段不能替代" : "尚无完整无伤队列穿越；等待或通关不能替代穿越证据");
            if (t.queueEvidence.Sum(e => e.cueSamples) == 0) gaps.Add("未读取到局部可见公开状态；不能宣称读懂窗口");
            else if (!t.queueEvidence.Any(e => e.cueSamples > 0 &&
                (t.queueEvidenceVersion >= 2 ? e.cleanEncounters > 0 : e.cleanCrossings > 0)))
                gaps.Add("可见线索与无伤结果不是同一机关；不能跨实例拼接反制证据");
        }
        gaps.AddRange(TunnelTrialIssues(t));
        return gaps.ToArray();
    }

    // Minimum observation for a useful probe, never a correctness/pass contract.
    // Autonomous hazards and passive surfaces must not be forced to emit control activations.
    public static string BehaviorRequirement(string mechanism)
    {
        switch (mechanism)
        {
            case "B": return "顶部实际发射、侧碰不发射、蓄力与中断恢复";
            case "C": return "顶部触发崩塌、等待恢复、搭乘者脱离";
            case "-": return "向上穿过/落顶、定向下穿、重复请求与取消恢复";
            case "F": return "默认可通行、Active实体、Recovery恢复通行";
            case "<": return "顶面速度注入、侧碰不搭乘、离开后无残留速度";
            case "X": return "从下撞破、侧面不破、重建恢复";
            case "o": return "真实拿宝事件与对应撤离事件；普通收集物另验";
            case "H": return "普通S键入口/返回、冷却、取消传送恢复移动";
            case ">": return "真实搭乘位移、端点转向、离开与重建";
            case "^": return "伸缩周期、伤害窗口、预警/后摇无伤害、作者高度保持";
            case "~": return "预热/喷火/冷却、伤害节流、后摇安全";
            case "P": return "真实锤头接触、摆动范围、控制结束恢复速度";
            case "[": return "预警可通行、扫描缩短、预算拒绝、后摇恢复通行";
            case "]": return "公开左右/安全队列、强制跳状态代价、Recovery无伤害";
            case "E": return "顶部踩踏弹跳、侧碰伤害、死亡与重建";
            case "e": return "踩踏与侧碰分开、非重复伤害、销毁后重建";
            case "@": return "锯片接触伤害、无敌节流、离开后再入";
            case "f": return "飞行轨迹、踩踏/侧碰、死亡与重建";
            case "S": return "实际更新出生引用、重复触碰幂等、跨回合语义";
            default: return "未知机制：需补行为验收契约";
        }
    }

    [Serializable]
    public sealed class Evidence
    {
        public string mechanism;
        public int built;
        public bool approached;
        public int contacts;
        public int runnerContacts, tricksterContacts;
        public int movingPartEvidenceVersion, runnerMovingPartContacts;
        public int activations; // Legacy mixed count: accepted control, launch, trap or loot event.
        public int observationVersion, controlsAccepted, runnerEffects;
        public bool ObservationGap => built <= 0 || (observationVersion < 1 ? activations <= 0 :
            mechanism == "P" && movingPartEvidenceVersion >= 1 ? runnerMovingPartContacts <= 0 :
            mechanism == "B" || mechanism == "o" ? runnerEffects <= 0 :
            mechanism == "F" || mechanism == "[" ? !(phases.Contains("Active") && phases.Contains("Recovery")) :
            runnerContacts <= 0);
        public string ObservationTarget => mechanism == "P" && movingPartEvidenceVersion >= 1 ? "Mario真实锤头接触（不等于扣血或安全通过）" : mechanism == "B" ? "Mario真实弹射事件" : mechanism == "o" ? "真实拿宝事件" :
            mechanism == "F" || mechanism == "[" ? "Active与Recovery阶段采样（不证明碰撞恢复）" :
            "Mario接触（不证明行为/反制/恢复正确）";
        public List<string> phases = new List<string>();
        public string Status => built == 0 ? "未生成" : observationVersion >= 1 ?
            (ObservationGap ? "探针观察不足：" : "已达到最低观察层级：") + ObservationTarget + "；行为验收仍待专项测试" :
            activations > 0 ? "已激活（不等于全部行为通过）" :
            contacts > 0 ? "已接触 / 激活未证实" : approached ? "已接近 / 未接触" : "未到达";
    }

    [Serializable]
    public sealed class QueueEvidence
    {
        public string source;
        public int cueSamples, cueChanges, waits, entries, crossings, cleanCrossings;
        public int damageEvents, healthLost, damageWithRecentCue;
        public int encounters, completedEncounters, cleanEncounters, bypassedEncounters;
        public float waitSeconds;
        public string lastCue;
    }

    // A conservative estimate from publicly displayed time/reach and ordinary movement speed.
    // Not a collision oracle: the physical crossing and health deltas are measured separately.
    public static bool QueueWindowAllowsEntry(bool readable, bool safe, float remaining, float distance, float speed)
    {
        return readable && safe && IsFinite(remaining) && IsFinite(distance) && IsFinite(speed) &&
            distance >= 0f && speed > 0.1f && remaining >= distance / speed + 0.2f;
    }

    public sealed class QueueCrossingMemory
    {
        private int approachSide, entrySide, healthAtEntry, previousHealthLost;
        private bool inside;
        // Flags: 1 entry, 2 full same-lane crossing, 4 no health loss during that crossing.
        public int Sample(float side, float height, float extent, int cumulativeHealthLost)
        {
            int lossBeforeSample = previousHealthLost;
            previousHealthLost = cumulativeHealthLost;
            if (Math.Abs(height) > 1.5f) { inside = false; approachSide = entrySide = 0; return 0; }
            if (Math.Abs(side) > extent)
            {
                int now = side < 0 ? -1 : 1;
                int flags = inside && entrySide != 0 && now == -entrySide ? 2 : 0;
                if (flags == 2 && cumulativeHealthLost == healthAtEntry) flags |= 4;
                inside = false; approachSide = now;
                return flags;
            }
            if (inside) return 0;
            inside = true; entrySide = approachSide; healthAtEntry = lossBeforeSample;
            return 1;
        }
    }

    // [AI防坑警告] Encounter is a measurement interval, NOT another AI sensor.
    // Start at an observed same-lane approach within 2 units of body-expanded reach,
    // or at entry after an observed exterior sample. Retaining the baseline through
    // waiting / same-side retreat (however long) prevents damage being washed away.
    // Only a full opposite-side crossing completes it. A bypass is explicitly unfinished.
    public sealed class QueueEncounterMemory
    {
        private int originSide, healthAtApproach, previousHealthLost;
        private float previousSide, previousHeight;
        private bool sampled, active;
        public int Started { get; private set; }
        public int Completed { get; private set; }
        public int CleanCompleted { get; private set; }
        public int Bypassed { get; private set; }
        public void Sample(float side, float height, float extent, int cumulativeHealthLost, int crossingFlags)
        {
            int lossBeforeSample = previousHealthLost;
            previousHealthLost = cumulativeHealthLost;
            bool sameLane = Math.Abs(height) <= 1.5f;
            bool outside = Math.Abs(side) > extent;
            bool approaching = !sampled || Math.Abs(side) < Math.Abs(previousSide);
            bool entryFromOutside = (crossingFlags & 1) != 0 && sampled &&
                Math.Abs(previousSide) > extent && Math.Abs(previousHeight) <= 1.5f;
            if (!active && sameLane && ((outside && Math.Abs(side) <= extent + 2f && approaching) || entryFromOutside))
            {
                active = true; Started++;
                originSide = (entryFromOutside ? previousSide : side) < 0 ? -1 : 1;
                healthAtApproach = lossBeforeSample;
            }
            if (active && sameLane && outside && side * originSide < 0)
            {
                if ((crossingFlags & 2) != 0)
                {
                    Completed++;
                    if (cumulativeHealthLost == healthAtApproach) CleanCompleted++;
                }
                else Bypassed++;
                active = false;
            }
            previousSide = side; previousHeight = height; sampled = true;
        }
    }

    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static bool IndependentStartObserved(Trial t) => IsFinite(t.startDelaySeconds) && t.startDelaySeconds >= 0f &&
        (t.startDelaySeconds == 0f || t.tricksterStrategy == "Passive" ||
        (t.startTimingEvidenceVersion >= 1 && t.startWaitFrames > 0 && t.opponentWaitDecisionFrames == t.startWaitFrames));

    public static bool PassiveControlClean(Trial t) => t.tricksterStrategy != "Passive" ||
        (t.controlAccepted == 0 && t.possessions == 0 && (t.startTimingEvidenceVersion < 1 ||
        (t.opponentWaitInputFrames == 0 && t.opponentWaitPreparations == 0)));

    [Serializable]
    public sealed class Trial
    {
        public string scenarioId;
        public string controlMode; // null on old reports = automated. Human records never certify AI coverage.
        public int tunnelVersion, tunnelEvidenceVersion, tunnelRequests, tunnelStarts, tunnelArrivals, postLootTunnelArrivals;
        public int controlsAfterTunnel; // Original <=3s metric retained unchanged.
        public int tunnelVisitEvidenceVersion, tunnelVisitOverflow;
        public string tunnelPlanningPolicy;
        public List<TunnelVisit> tunnelVisits = new List<TunnelVisit>();
        public List<string> feedback = new List<string>();
        public List<string> decisions = new List<string>();
        public string profile;
        public string marioStrategy, tricksterStrategy;
        public string objectivePhase;
        public List<string> routesUsed = new List<string>();
        public int routeSwitchRequests, routeTransitions, waypointsReached, recoveryAttempts;
        public int stairRecoveryEvidenceVersion, stairRecoveryRequests;
        public int tunnelDecisionEvidenceVersion, tunnelPreparationRequests;
        public int cavernEvidenceVersion, returnDetourRequests;
        public int wallEvidenceVersion;
        public string wallPolicy;
        public List<WallEpisode> wallEpisodes = new List<WallEpisode>();
        public bool outboundThreatRemembered;
        public float undergroundSeconds, surfaceSeconds;
        public int undergroundControls, surfaceControls;
        public int telegraphRetreats, recoveryCrossings, possessionTransfers;
        public int bounceLandingAttempts, runnerBounceLaunches;
        public int experienceEvidenceVersion;
        public string experience;
        public bool expectsReturn;
        public List<string> completedRoutes = new List<string>();
        public int controlAccepted, anchorSwitchRequests;
        public float armedNearbySeconds;
        public string[] ExperienceGaps => ExperienceIssues(this);
        public int attempt = 1;
        public string comparison = "";
        public string endReason = "";
        public string outcome = "Pending";
        public float seconds;
        public float farthestX;
        public float endX, endY;
        public int probeEvidenceVersion, probeTargets, probeSatisfied, probeTimedOut, probeMissing;
        public float probeBudgetSeconds, probeElapsedSeconds;
        public bool probeBudgetExhausted;
        public int healthEvidenceVersion, runnerDamageEvents, runnerHealthLost;
        public int counterplayVersion, queueEvidenceVersion;
        public int startTimingEvidenceVersion, startWaitFrames, opponentWaitDecisionFrames, opponentWaitInputFrames, opponentWaitPreparations;
        public float startDelaySeconds, actualStartWaitSeconds;
        public float lootAtSeconds = -1f, escapeAtSeconds = -1f;
        public int postLootControls, postLootTransfers;
        public List<QueueEvidence> queueEvidence = new List<QueueEvidence>();
        public string scanPolicy; // Null in historical reports; never infer a new strategy for old results.
        public int scanEvidenceVersion, scanHits, scanMisses;
        public int scans, possessions, comboEvents, heatEvents, lootEvents, escapeEvents;
        public int routeDegradations, routeRecoveries, routeBlocks, crises, reveals;
        public List<Evidence> coverage = new List<Evidence>();
        public List<string> errors = new List<string>();
        public List<string> timeline = new List<string>();
        public string validation = "";
        public string nextAction = "";
        public bool HasGameplayEvidence => IsFinite(seconds) && seconds > 0 &&
            (outcome == "Cleared" || outcome == "RunnerStopped" || outcome == "NoProgress" || outcome == "TimedOut");
        public bool IsAutomated => string.IsNullOrEmpty(controlMode) || controlMode == "Automated";
        public bool NeedsConfirmation => IsAutomated && HasGameplayEvidence && errors.Count == 0 &&
            (!CandidateForHumanPlay || (string.IsNullOrEmpty(experience) && coverage.Any(e => e.ObservationGap)));
        public string InfrastructureKey => outcome == "StartupFailed" || outcome == "BuildFailed" || outcome == "RuntimeError"
            ? outcome + ":" + (errors.Count > 0 ? errors[0].Replace("\r", "").Split('\n')[0] : nextAction)
            : "";
        public bool PairExercised => coverage.Count == 2 && coverage.All(e => e.built > 0 && (e.contacts > 0 || e.activations > 0));
        public bool CandidateForHumanPlay => (string.IsNullOrEmpty(controlMode) || controlMode == "Automated" || controlMode == "Demonstration") && HasGameplayEvidence && ExperienceGaps.Length == 0 && outcome == "Cleared" && errors.Count == 0 &&
            coverage.Count > 0 && coverage.All(e => e.built > 0 &&
                (!string.IsNullOrEmpty(experience) || (e.observationVersion >= 1 ? !e.ObservationGap : e.contacts > 0 || e.activations > 0)));
    }
}
