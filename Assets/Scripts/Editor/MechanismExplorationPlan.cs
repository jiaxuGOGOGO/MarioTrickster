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
    public enum Scope { Smoke, Mechanisms, Pairwise, Experience, Counterplay }
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
            $"{t.scanEvidenceVersion}:{t.scanHits > 0}:{t.healthEvidenceVersion}:{t.runnerDamageEvents > 0}:{t.probeEvidenceVersion}:{t.probeBudgetExhausted}:{t.queueEvidenceVersion}:{t.queueEvidence.Sum(q => q.cleanCrossings)}:{t.queueEvidence.Sum(q => q.healthLost)}:{t.queueEvidence.Sum(q => q.cleanEncounters)}:{t.startTimingEvidenceVersion}:{IndependentStartObserved(t)}:" + string.Join(",", (t.routesUsed ?? new List<string>()).OrderBy(r => r)) + $":{t.telegraphRetreats > 0}:{t.recoveryCrossings > 0}:{t.runnerBounceLaunches > 0}:" + string.Join("|", t.coverage
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
        private bool returning, partialRoute;
        public readonly List<string> CompletedRoutes = new List<string>();
        private float stalled, best = float.MaxValue;
        public int SwitchRequests { get; private set; }
        public int WaypointsReached => reached.Count;
        public string RouteId => routes.Length == 0 ? "direct" : routes[routeIndex].id;
        public Point Target => routes.Length == 0 || cursor >= routes[routeIndex].points.Length ? null :
            routes[routeIndex].points[returning ? routes[routeIndex].points.Length - 1 - cursor : cursor];
        public RouteNavigator(Route[] routes, bool safe)
        { this.routes = routes ?? Array.Empty<Route>(); routeIndex = safe && this.routes.Length > 1 ? 1 : 0; }
        public void Tick(float x, float y, bool isReturning, float dt, bool grounded = true)
        {
            if (returning != isReturning)
            { returning = isReturning; partialRoute = false; cursor = 0; stalled = 0; best = float.MaxValue; }
            var target = Target;
            if (target == null) return;
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

    public static string PairKey(char a, char b) => a < b ? $"{a}{b}" : $"{b}{a}";
    public static string[] MissingFromCatalog(IEnumerable<char> registryChars) => registryChars
        .Where(c => !" .#=WMTG".Contains(c) && !Catalog.Contains(c)).Select(c => c.ToString()).Distinct().ToArray();

    // Missing evidence is actionable, not a fun score or a requirement that every safe detour fight.
    public static string[] ExperienceIssues(Trial t, Scenario scenario = null)
    {
        if (string.IsNullOrEmpty(scenario != null ? scenario.experience : t.experience)) return Array.Empty<string>();
        bool expectsReturn = scenario != null ? scenario.lootEscape : t.expectsReturn;
        var gaps = new List<string>();
        if (t.experienceEvidenceVersion < 1) gaps.Add("旧报告未记录完整路线/交手机会；不能补算通过");
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
        public string profile;
        public string marioStrategy, tricksterStrategy;
        public string objectivePhase;
        public List<string> routesUsed = new List<string>();
        public int routeSwitchRequests, routeTransitions, waypointsReached, recoveryAttempts;
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
        public bool NeedsConfirmation => HasGameplayEvidence && errors.Count == 0 &&
            (!CandidateForHumanPlay || (string.IsNullOrEmpty(experience) && coverage.Any(e => e.ObservationGap)));
        public string InfrastructureKey => outcome == "StartupFailed" || outcome == "BuildFailed" || outcome == "RuntimeError"
            ? outcome + ":" + (errors.Count > 0 ? errors[0].Replace("\r", "").Split('\n')[0] : nextAction)
            : "";
        public bool PairExercised => coverage.Count == 2 && coverage.All(e => e.built > 0 && (e.contacts > 0 || e.activations > 0));
        public bool CandidateForHumanPlay => HasGameplayEvidence && ExperienceGaps.Length == 0 && outcome == "Cleared" && errors.Count == 0 &&
            coverage.Count > 0 && coverage.All(e => e.built > 0 &&
                (!string.IsNullOrEmpty(experience) || (e.observationVersion >= 1 ? !e.ObservationGap : e.contacts > 0 || e.activations > 0)));
    }
}
