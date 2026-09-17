using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>Versioned, deterministic challenge grammar. Layout coverage is not interaction coverage.</summary>
public static class MechanismExplorationPlan
{
    public const int Version = 1;
    public const string Catalog = "BC-F<XoH>^~P[]Ee@fS";
    public static readonly string[] Profiles = { "Cautious", "Runner", "Explorer" };
    public enum Scope { Smoke, Mechanisms, Pairwise }
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
        Func<Trial, string> signature = t => t.outcome + ":" + string.Join("|", t.coverage
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
            put(8, 1, '='); put(9, 2, '-');
            for (int x = 10; x <= 25; x++) put(x, 3, '-');
            put(26, 2, '-'); put(27, 1, '=');
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
