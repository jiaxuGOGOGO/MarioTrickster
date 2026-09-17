using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>Versioned, deterministic challenge grammar. Layout coverage is not interaction coverage.</summary>
public static class MechanismExplorationPlan
{
    public const int Version = 2;
    public const string Catalog = "BC-F<XoH>^~P[]Ee@fS";
    public static readonly string[] Profiles = { "Cautious", "Runner", "Explorer" };
    public enum Scope { Smoke, Mechanisms, Pairwise, Experience }
    public const int MaxConfirmationScenes = 6;

    // One bounded same-seed confirmation pass, not automatic balancing or a claim of learning.
    public static string[] SelectConfirmationScenes(IEnumerable<Trial> trials)
    {
        return trials.Where(t => t.attempt <= 1 && t.NeedsConfirmation)
            .OrderBy(t => t.outcome == "NoProgress" ? 0 : t.outcome == "RunnerStopped" ? 1 : 2)
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
            string.Join(",", (t.routesUsed ?? new List<string>()).OrderBy(r => r)) + $":{t.telegraphRetreats > 0}:{t.recoveryCrossings > 0}:" + string.Join("|", t.coverage
            .OrderBy(e => e.mechanism).Select(e => $"{e.mechanism}:{e.built > 0}:{e.approached}:{e.contacts > 0}:{e.activations > 0}"));
        return signature(baseline) == signature(confirmation)
            ? "同条件复现：结果与覆盖层级一致；仍需检查 AI 局限与真人反制体验。"
            : "同条件结果不稳定：不是已修复；对照事件时间线再定位物理时序或 AI 决策。";
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
        public Route[] routes = Array.Empty<Route>();
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

    // Navigation memory records reached coordinates, not claims of successful pathfinding.
    public sealed class RouteNavigator
    {
        private readonly Route[] routes;
        private readonly HashSet<string> reached = new HashSet<string>();
        private int routeIndex, cursor;
        private bool returning;
        private float stalled, best = float.MaxValue;
        public int SwitchRequests { get; private set; }
        public int WaypointsReached => reached.Count;
        public string RouteId => routes.Length == 0 ? "direct" : routes[routeIndex].id;
        public Point Target => routes.Length == 0 || cursor >= routes[routeIndex].points.Length ? null :
            routes[routeIndex].points[returning ? routes[routeIndex].points.Length - 1 - cursor : cursor];
        public RouteNavigator(Route[] routes, bool safe)
        { this.routes = routes ?? Array.Empty<Route>(); routeIndex = safe && this.routes.Length > 1 ? 1 : 0; }
        public void Tick(float x, float y, bool isReturning, float dt)
        {
            if (returning != isReturning)
            { returning = isReturning; cursor = 0; stalled = 0; best = float.MaxValue; }
            var target = Target;
            if (target == null) return;
            float dx = target.x - x, dy = target.y - y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            if (Math.Abs(dx) < 0.8f && Math.Abs(dy) < 0.8f)
            {
                reached.Add(returning + ":" + RouteId + ":" + cursor);
                cursor++; best = float.MaxValue; stalled = 0;
                return;
            }
            if (distance < best - 0.25f) { best = distance; stalled = 0; }
            else stalled += Math.Max(0, dt);
            if (stalled < 4f || SwitchRequests >= 2 || routes.Length < 2) return;
            // Try the other authored route at most twice; do not skip a failed waypoint as success.
            routeIndex = (routeIndex + 1) % routes.Length; cursor = 0; SwitchRequests++;
            best = float.MaxValue; stalled = 0;
            // Lower lane is enterable anywhere. Upper lane must climb via the actual entry stairs.
            if (RouteId == "lower")
                while (Target != null && (returning ? Target.x > x + 0.8f : Target.x < x - 0.8f)) cursor++;
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
    public static Matchup[] Matchups(Scenario scenario) => string.IsNullOrEmpty(scenario.experience) ? legacyMatchups : experienceMatchups;
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
        if (scope == Scope.Experience) return Enumerable.Range(0, 3).Select(i => BuildExperience(unchecked(seed + i * 7919), i)).ToList();
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
        put(kind == 2 ? 5 : 2, 1, 'M'); put(12 + shift, 1, 'T');
        put(kind == 2 ? 3 : 41, 1, 'G');
        // A climb costs time but bypasses the lower ambush lane. All entries/exits are one-way.
        put(7, 1, '-'); put(8, 2, '-'); put(9, 3, '-');
        for (int x = 10; x <= 31; x++)
            if (kind != 1 || x < 19 || x > 21) put(x, 4, '-');
        put(32, 3, '-'); put(33, 2, '-'); put(34, 1, '-');
        if (kind == 0) { put(16 + shift, 1, 'F'); put(26, 2, 'X'); }
        if (kind == 1)
        {
            // Two spaced ambushes and an upper gap: wait for recovery or take a jumping detour.
            put(15 + shift, 2, 'X'); put(28, 1, 'F');
            put(23, 5, 'F');
        }
        if (kind == 2) { put(16 + shift, 1, 'F'); put(28, 2, 'X'); put(38, 1, 'o'); }
        // Targets are root positions at rest, not sprite centers or cell-top guesses.
        float standing = PhysicsMetrics.MARIO_COLLIDER_HEIGHT * 0.5f - PhysicsMetrics.MARIO_COLLIDER_OFFSET_Y;
        float platformStanding = PhysicsMetrics.ONEWAY_COLLIDER_SIZE.y * 0.5f + standing;
        float groundStanding = 0.5f + standing;
        var upper = new System.Collections.Generic.List<Point> {
            new Point(7, 1 + platformStanding), new Point(8, 2 + platformStanding),
            new Point(10, 4 + platformStanding), new Point(17, 4 + platformStanding),
            new Point(23, 4 + platformStanding), new Point(30, 4 + platformStanding),
            new Point(33, 2 + platformStanding), new Point(35, groundStanding)
        };
        return new Scenario {
            seed = seed, id = $"v{Version}_experience_{kind}_{unchecked((uint)seed):x8}",
            experience = new[] { "RiskOrDetour", "BaitAndCounter", "LootAndReturn" }[kind],
            mechanisms = kind == 2 ? "FXo" : "FX", lootEscape = kind == 2,
            ascii = string.Join("\n", rows.Select(row => new string(row))),
            intention = new[] {
                "短路抢时间或爬高绕行；比较真实路线、预警退让和通关代价。",
                "两段伏击与带缺口高路：引诱出手，等后摇通过，或跳跃换路。",
                "深入右端拿宝，再返回左端撤离；对手可换点追击，去程进度不能代替返程。"
            }[kind],
            routes = new[] {
                new Route { id = "lower", points = new[] { new Point(11, groundStanding), new Point(24, groundStanding), new Point(35, groundStanding) }, minX = 14, maxX = 29, minY = 0.5f, maxY = 2.6f },
                new Route { id = "upper", points = upper.ToArray(), minX = 14, maxX = 29, minY = 4 + platformStanding - 0.3f, maxY = 7.5f }
            }
        };
    }

    public static string PairKey(char a, char b) => a < b ? $"{a}{b}" : $"{b}{a}";
    public static string[] MissingFromCatalog(IEnumerable<char> registryChars) => registryChars
        .Where(c => !" .#=WMTG".Contains(c) && !Catalog.Contains(c)).Select(c => c.ToString()).Distinct().ToArray();

    [Serializable]
    public sealed class Evidence
    {
        public string mechanism;
        public int built;
        public bool approached;
        public int contacts;
        public int runnerContacts, tricksterContacts;
        public int activations;
        public List<string> phases = new List<string>();
        public string Status => built == 0 ? "未生成" : activations > 0 ? "已激活（不等于全部行为通过）" :
            contacts > 0 ? "已接触 / 激活未证实" : approached ? "已接近 / 未接触" : "未到达";
    }

    [Serializable]
    public sealed class Trial
    {
        public string scenarioId;
        public string profile;
        public string marioStrategy, tricksterStrategy;
        public string objectivePhase;
        public List<string> routesUsed = new List<string>();
        public int routeSwitchRequests, routeTransitions, waypointsReached, recoveryAttempts;
        public int telegraphRetreats, recoveryCrossings, possessionTransfers;
        public int attempt = 1;
        public string comparison = "";
        public string endReason = "";
        public string outcome = "Pending";
        public float seconds;
        public float farthestX;
        public float endX, endY;
        public int scans, possessions, comboEvents, heatEvents, lootEvents, escapeEvents;
        public int routeDegradations, routeRecoveries, routeBlocks, crises, reveals;
        public List<Evidence> coverage = new List<Evidence>();
        public List<string> errors = new List<string>();
        public List<string> timeline = new List<string>();
        public string validation = "";
        public string nextAction = "";
        public bool HasGameplayEvidence => seconds > 0 &&
            (outcome == "Cleared" || outcome == "RunnerStopped" || outcome == "NoProgress" || outcome == "TimedOut");
        public bool NeedsConfirmation => HasGameplayEvidence && errors.Count == 0 &&
            (!CandidateForHumanPlay || coverage.Any(e => e.activations == 0));
        public string InfrastructureKey => outcome == "StartupFailed" || outcome == "BuildFailed" || outcome == "RuntimeError"
            ? outcome + ":" + (errors.Count > 0 ? errors[0].Replace("\r", "").Split('\n')[0] : nextAction)
            : "";
        public bool PairExercised => coverage.Count == 2 && coverage.All(e => e.built > 0 && (e.contacts > 0 || e.activations > 0));
        public bool CandidateForHumanPlay => outcome == "Cleared" && errors.Count == 0 && coverage.Count > 0 && coverage.All(e => e.built > 0 && (e.contacts > 0 || e.activations > 0));
    }
}
