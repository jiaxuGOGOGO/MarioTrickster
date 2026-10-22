using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// 世界乐园 v1 的纯逻辑数据包、结构检查和本次运行内存彩排。
/// 不依赖 Unity；运行时适配器负责把 room 节点接到现有守宝房间。
/// </summary>
public static class WorldParkPack
{
    public const string TypeTag = "mariotrickster-worldpark";
    public const int Version = 1;
    public const int MaxJsonLength = 262144;
    public const int MaxVenues = 64;
    public const int MaxNodesPerVenue = 256;
    public const int MaxCluesPerVenue = 256;
    public const int MaxLinks = 256;
    public const int MaxTotalNodes = 512;
    public const int MaxExplorePointsPerNode = 256;
    public const int MaxSessionSteps = 256;
    private static readonly Regex JsonNumber = new Regex(@"^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?$");
    private static readonly Regex JsonToken = new Regex(@"\G(?:(?<str>""(?:[^""\\\x00-\x1f]|\\(?:[""\\/bfnrt]|u[0-9A-Fa-f]{4}))*"")|(?<num>-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)|(?<lit>true|false|null)|(?<punct>[{}\[\],:]))");

    [Serializable]
    public sealed class Pack
    {
        public string id = "";
        public string name = "";
        public string premise = "";
        public string entryVenue = "";
        public int v = Version;
        public List<Venue> venues = new List<Venue>();
        public List<Link> links = new List<Link>();
    }

    [Serializable]
    public sealed class Venue
    {
        public string id = "";
        public string name = "";
        public string setting = "";
        public string role = "";
        public string suit = "";
        public string rules = "";
        public string entry = "";
        public int difficulty;
        public List<Node> nodes = new List<Node>();
        public List<Clue> clues = new List<Clue>();
    }

    [Serializable]
    public sealed class Node
    {
        public string id = "";
        public string kind = "";
        public string name = "";
        public string text = "";
        public string room = "";
        public string next = "";
        public string onWin = "";
        public string onLose = "";
        public string result = "";
        // explore 节点直接携带地图；旧 JSON 没有这些字段时仍是可读取的 v1 包。
        public string mapText = "";
        public int timeout;
        public List<ExplorePoint> points = new List<ExplorePoint>();
        // S251：game 节点携带弥留之国式博弈规则；其他节点为 null，导出时省略。
        public BorderlandGame.Def game;
    }

    [Serializable]
    public sealed class ExplorePoint
    {
        public string id = "";
        public string kind = "";
        public string label = "";
        public string clue = "";
        public string outcome = "";
        public int x, y;
        public List<string> requires = new List<string>();
        // S252 追逐者（kind=chaser）专用：speed=十分之一格/秒（0=默认 34），sight=视距格数（0=默认 7），route=巡逻格 "x,y"。
        public int speed, sight;
        public List<string> route = new List<string>();
    }

    [Serializable]
    public sealed class Clue
    {
        public string id = "";
        public string name = "";
        public string foundAt = "";
        public string revealAt = "";
        public string hint = "";
        public string truth = "";
        // stage 是旧有的进入阶段即发现行为；interact 只能在探索现场实际交互后发现。
        public string trigger = "stage";
    }

    [Serializable]
    public sealed class Link
    {
        public string from = "";
        public string to = "";
        public string requires = "";
    }

    [Serializable]
    public sealed class Issue
    {
        public string severity = "";
        public string code = "";
        public string venue = "";
        public string node = "";
        public string text = "";
    }

    [Serializable]
    public sealed class RoomResult
    {
        public string venue = "", node = "";
        public bool won;
    }

    /// <summary>现场活动事实；不得用它冒充横版 room 的结果。</summary>
    [Serializable]
    public sealed class ActivityResult
    {
        public string venue = "", node = "", kind = "";
        public bool won;
    }

    /// <summary>
    /// 读取 JSON v1。格式或容器结构错误会拒绝；内容完整性由 Validate 报告，便于作者保存草稿。
    /// </summary>
    public static Pack Parse(string json, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "世界包 JSON 为空";
            return null;
        }
        if (json.Length > MaxJsonLength)
        {
            error = "世界包 JSON 超过 262144 字符上限";
            return null;
        }
        if (!CheckJsonBudgetAndEscapes(json, out error)) return null;
        if (!CheckJsonSyntax(json)) { error = "世界包JSON语法或分隔符不正确"; return null; }

        object parsed = MiniJson.Parse(json[0] == '\ufeff' ? json.Substring(1) : json, out error);
        if (parsed == null)
        {
            if (string.IsNullOrEmpty(error)) error = "世界包 JSON 无法读取";
            return null;
        }

        var root = parsed as Dictionary<string, object>;
        if (root == null)
        {
            error = "世界包根节点必须是对象";
            return null;
        }

        try
        {
            string type = RequiredString(root, "type", "根节点");
            if (!string.Equals(type, TypeTag, StringComparison.Ordinal))
                throw new PackFormatException("type 必须是 " + TypeTag);

            int version = RequiredInt(root, "v", "根节点");
            if (version != Version)
                throw new PackFormatException("v 必须是 " + Version.ToString(CultureInfo.InvariantCulture));

            var pack = new Pack
            {
                v = version,
                id = OptionalString(root, "id", "根节点"),
                name = OptionalString(root, "name", "根节点"),
                premise = OptionalString(root, "premise", "根节点"),
                entryVenue = OptionalString(root, "entryVenue", "根节点")
            };

            List<object> venueValues = RequiredArray(root, "venues", "根节点");
            if (venueValues.Count > MaxVenues)
                throw new PackFormatException("venues 超过 " + MaxVenues + " 个会场上限");
            pack.venues = ParseVenues(venueValues);

            List<object> linkValues = RequiredArray(root, "links", "根节点");
            if (linkValues.Count > MaxLinks)
                throw new PackFormatException("links 超过 " + MaxLinks + " 条连接上限");
            pack.links = ParseLinks(linkValues);
            return pack;
        }
        catch (PackFormatException ex)
        {
            error = ex.Message;
            return null;
        }
    }

    /// <summary>
    /// 输出 JSON v1。null 列表按空列表处理；字符串会正确转义，且不会借用 MiniJson 的输出能力。
    /// </summary>
    public static string ToJson(Pack p)
    {
        if (p == null) throw new ArgumentNullException(nameof(p));

        var sb = new StringBuilder(2048);
        sb.Append('{');
        bool first = true;
        StringProperty(sb, ref first, "type", TypeTag);
        IntProperty(sb, ref first, "v", p.v);
        StringProperty(sb, ref first, "id", p.id);
        StringProperty(sb, ref first, "name", p.name);
        StringProperty(sb, ref first, "premise", p.premise);
        StringProperty(sb, ref first, "entryVenue", p.entryVenue);
        Name(sb, ref first, "venues");
        AppendVenues(sb, p.venues);
        Name(sb, ref first, "links");
        AppendLinks(sb, p.links);
        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>
    /// 检查可编辑世界包。未知玩法/角色只报 warn，作者仍可保存草稿；真实运行器应拦截其实际路径。
    /// </summary>
    public static List<Issue> Validate(Pack p, Func<string, bool> roomExists = null)
    {
        var issues = new List<Issue>();
        if (p == null)
        {
            Add(issues, "error", "pack_null", "", "", "世界包不存在");
            return issues;
        }

        if (p.v != Version)
            Add(issues, "error", "version_invalid", "", "", "世界包版本必须是 1");
        if (Blank(p.id))
            Add(issues, "error", "pack_id_required", "", "", "世界包需要 id");

        var venues = p.venues ?? new List<Venue>();
        var venuesById = new Dictionary<string, Venue>(StringComparer.Ordinal);
        for (int i = 0; i < venues.Count; i++)
        {
            Venue venue = venues[i];
            if (venue == null)
            {
                Add(issues, "error", "venue_null", "", "", "第 " + (i + 1) + " 个会场为空");
                continue;
            }
            if (Blank(venue.id))
            {
                Add(issues, "error", "venue_id_required", "", "", "第 " + (i + 1) + " 个会场需要 id");
                continue;
            }
            if (InvalidKey(venue.id)) Add(issues, "error", "id_key_invalid", venue.id, "", "ID不能含斜杠或控制字符；显示名称不受此限制");
            if (venuesById.ContainsKey(venue.id))
                Add(issues, "error", "venue_id_duplicate", venue.id, "", "会场 id 重复：" + venue.id);
            else
                venuesById.Add(venue.id, venue);
        }

        if (Blank(p.entryVenue))
            Add(issues, "error", "entry_venue_required", "", "", "世界包需要 entryVenue");
        else if (!venuesById.ContainsKey(p.entryVenue))
            Add(issues, "error", "entry_venue_missing", "", "", "entryVenue 不存在：" + p.entryVenue);

        foreach (Venue venue in venues)
            if (venue != null) ValidateVenue(venue, issues, roomExists);

        ValidateLinks(p.links, venuesById, issues);
        return issues;
    }

    public static Venue GetVenue(Pack p, string id)
    {
        if (p == null || Blank(id) || p.venues == null) return null;
        foreach (Venue venue in p.venues)
            if (venue != null && string.Equals(venue.id, id, StringComparison.Ordinal)) return venue;
        return null;
    }

    public static Node GetNode(Venue v, string id)
    {
        if (v == null || Blank(id) || v.nodes == null) return null;
        foreach (Node node in v.nodes)
            if (node != null && string.Equals(node.id, id, StringComparison.Ordinal)) return node;
        return null;
    }

    /// <summary>
    /// 纯内存的一次彩排，不是世界存档。found/revealed 使用 venue/id 键，避免会场间线索 id 冲突。
    /// </summary>
    public sealed class Session
    {
        public Pack pack;
        public string venueId = "";
        public string nodeId = "";
        public string error = "";
        public readonly HashSet<string> found = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> revealed = new HashSet<string>(StringComparer.Ordinal);
        // 房间事实与作者定义的会场总体结果独立：剧情可以改变整体目标，但不得改写守宝房实际输赢。
        public readonly List<RoomResult> roomResults = new List<RoomResult>();
        public readonly List<ActivityResult> activityResults = new List<ActivityResult>();
        public readonly Dictionary<string, string> venueResults = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, string> results { get { return venueResults; } } // v1 API兼容，含义为会场结果。
        public readonly List<string> log = new List<string>();

        public readonly List<WorldParkProgress.Action> actions = new List<WorldParkProgress.Action>();

        private int steps;
        private bool stopped;

        internal Session(Pack source)
        {
            pack = source;
        }

        public Node Current { get { return GetNode(Venue, nodeId); } }
        public Venue Venue { get { return GetVenue(pack, venueId); } }

        /// <summary>只继续 story/reveal；每次调用恰好进入一个后续 stage。</summary>
        public bool Continue(out string message)
        {
            message = "";
            if (!BeginAction(out message)) return false;
            Node current = Current;
            if (current == null) return Fail("当前节点不存在", out message);
            if (current.kind != "story" && current.kind != "reveal")
                return Fail("当前阶段不能继续；只有 story 或 reveal 可以继续", out message);
            if (Blank(current.next)) return Fail("当前阶段缺少 next", out message);
            if (!MoveWithinVenue(current.next, out message)) return false;
            actions.Add(new WorldParkProgress.Action { kind = "continue", value = "" });
            return true;
        }

        /// <summary>只结算当前 room；一次调用只进入一个胜利或失败后的 stage。</summary>
        public bool ResolveRoom(bool won, out string message)
        {
            message = "";
            if (!BeginAction(out message)) return false;
            Node current = Current;
            if (current == null) return Fail("当前节点不存在", out message);
            if (current.kind != "room")
                return Fail("当前阶段不是 room，不能结算房间结果", out message);
            string target = won ? current.onWin : current.onLose;
            if (Blank(target)) return Fail(won ? "room 缺少 onWin" : "room 缺少 onLose", out message);
            string fromVenue = venueId, fromNode = nodeId;
            if (!MoveWithinVenue(target, out message)) return false;
            roomResults.Add(new RoomResult { venue = fromVenue, node = fromNode, won = won });
            actions.Add(new WorldParkProgress.Action { kind = "room", value = won ? "win" : "lose" });
            log.Add("房间结果：" + fromVenue + "/" + fromNode + "=" + (won ? "win" : "lose"));
            return true;
        }

        /// <summary>仅当前 explore 节点中由现场点对应的 interact 线索可以被实际观察。</summary>
        public bool ObserveClue(string id, out string message)
        {
            message = "";
            if (!BeginAction(out message)) return false;
            Node current = Current;
            Venue venue = Venue;
            if (current == null || venue == null) return Fail("当前探索节点不存在", out message);
            if (current.kind != "explore") return Fail("当前阶段不是 explore，不能观察现场线索", out message);
            if (Blank(id)) return Fail("线索 id 为空", out message);

            Clue matched = null;
            List<Clue> clues = venue.clues ?? new List<Clue>();
            foreach (Clue clue in clues)
                if (clue != null && string.Equals(clue.id, id, StringComparison.Ordinal) &&
                    string.Equals(clue.trigger, "interact", StringComparison.Ordinal) &&
                    string.Equals(clue.foundAt, current.id, StringComparison.Ordinal))
                {
                    matched = clue;
                    break;
                }
            if (matched == null) return Fail("当前探索没有可交互的对应线索：" + Safe(id), out message);

            bool mappedPoint = false;
            List<ExplorePoint> points = current.points ?? new List<ExplorePoint>();
            foreach (ExplorePoint point in points)
                if (point != null && point.kind == "clue" && string.Equals(point.clue, id, StringComparison.Ordinal))
                {
                    mappedPoint = true;
                    break;
                }
            if (!mappedPoint) return Fail("当前探索没有指向该线索的现场点：" + Safe(id), out message);

            string key = ClueKey(venue.id, matched.id);
            if (found.Add(key))
            {
                log.Add("找到线索：" + key);
                actions.Add(new WorldParkProgress.Action { kind = "observe", value = id });
                message = "已观察线索：" + Safe(matched.name);
            }
            else
                message = "已观察过该线索：" + Safe(matched.name);
            return true;
        }

        /// <summary>确认探索结果并进入分支；只记录 ActivityResult，绝不写 RoomResult。</summary>
        public bool ResolveExplore(bool won, out string message)
        {
            message = "";
            if (!BeginAction(out message)) return false;
            Node current = Current;
            if (current == null) return Fail("当前节点不存在", out message);
            if (current.kind != "explore") return Fail("当前阶段不是 explore，不能结算探索结果", out message);
            string target = won ? current.onWin : current.onLose;
            if (Blank(target)) return Fail(won ? "explore 缺少 onWin" : "explore 缺少 onLose", out message);
            string fromVenue = venueId, fromNode = nodeId;
            if (!MoveWithinVenue(target, out message)) return false;
            activityResults.Add(new ActivityResult { venue = fromVenue, node = fromNode, kind = "explore", won = won });
            actions.Add(new WorldParkProgress.Action { kind = "explore", value = won ? "win" : "lose" });
            log.Add("探索结果：" + fromVenue + "/" + fromNode + "=" + (won ? "win" : "lose"));
            return true;
        }

        /// <summary>
        /// S251：用玩家的逐轮选择重新计算整局博弈（不信任外部给的输赢），必须恰好走完才可结算。
        /// </summary>
        public bool ResolveGame(IList<string> choices, out string message)
        {
            message = "";
            if (!BeginAction(out message)) return false;
            Node current = Current;
            if (current == null) return Fail("当前节点不存在", out message);
            if (current.kind != "game") return Fail("当前阶段不是 game，不能结算博弈", out message);
            if (choices == null || choices.Count == 0) return Fail("博弈需要至少一轮选择", out message);
            foreach (string choice in choices)
                if (choice == null || choice.IndexOf('|') >= 0) return Fail("博弈选择不能为空或包含 |", out message);
            BorderlandGame.State state = BorderlandGame.Run(current.game, choices);
            if (!string.IsNullOrEmpty(state.error)) return Fail(state.error, out message);
            if (!state.finished) return Fail("博弈尚未结束，还需要第 " + (state.round + 1) + " 轮的选择", out message);
            string target = state.won ? current.onWin : current.onLose;
            if (Blank(target)) return Fail(state.won ? "game 缺少 onWin" : "game 缺少 onLose", out message);
            string fromVenue = venueId, fromNode = nodeId;
            if (!MoveWithinVenue(target, out message)) return false;
            activityResults.Add(new ActivityResult { venue = fromVenue, node = fromNode, kind = "game", won = state.won });
            actions.Add(new WorldParkProgress.Action { kind = "game", value = string.Join("|", new List<string>(choices).ToArray()) });
            log.Add("博弈结果：" + fromVenue + "/" + fromNode + "=" + (state.won ? "win" : "lose"));
            return true;
        }

        /// <summary>只可在 ending 使用满足 requires 的连接前往另一会场。</summary>
        public bool Travel(string targetVenueId, out string message)
        {
            message = "";
            if (!BeginAction(out message)) return false;
            Node current = Current;
            if (current == null) return Fail("当前节点不存在", out message);
            if (current.kind != "ending")
                return Fail("当前阶段不是 ending，不能前往其他会场", out message);
            if (Blank(targetVenueId)) return Fail("目标会场 id 为空", out message);

            Link link = FindLink(venueId, targetVenueId);
            if (link == null) return Fail("当前结局没有通往目标会场的连接", out message);
            string currentResult;
            if (!results.TryGetValue(venueId, out currentResult) || (link.requires != "any" && !string.Equals(currentResult, link.requires, StringComparison.Ordinal)))
                return Fail("尚未满足会场连接的结果条件：" + Safe(link.requires), out message);

            Venue target = GetVenue(pack, targetVenueId);
            if (target == null) return Fail("目标会场不存在：" + targetVenueId, out message);
            string blocker = RunBlockerForVenue(pack, target.id);
            if (!string.IsNullOrEmpty(blocker)) return Fail(blocker, out message);
            Node entry = GetNode(target, target.entry);
            if (entry == null) return Fail("目标会场缺少可进入的 entry", out message);
            if (!Enter(target, entry, out message)) return false;
            actions.Add(new WorldParkProgress.Action { kind = "travel", value = targetVenueId });
            return true;
        }

        public bool CanTravel(string targetVenueId)
        {
            Node current = Current;
            if (current == null || current.kind != "ending" || Blank(targetVenueId)) return false;
            Link link = FindLink(venueId, targetVenueId);
            if (link == null) return false;
            string currentResult;
            if (!results.TryGetValue(venueId, out currentResult) || (link.requires != "any" && !string.Equals(currentResult, link.requires, StringComparison.Ordinal))) return false;
            Venue target = GetVenue(pack, targetVenueId);
            return target != null && string.IsNullOrEmpty(RunBlockerForVenue(pack, target.id));
        }

        private bool BeginAction(out string message)
        {
            message = "";
            if (stopped)
            {
                message = string.IsNullOrEmpty(error) ? "流程已停止" : error;
                return false;
            }
            if (pack == null) return Fail("会话没有世界包", out message);
            error = "";
            return true;
        }

        private bool MoveWithinVenue(string targetNodeId, out string message)
        {
            Venue venue = Venue;
            Node target = GetNode(venue, targetNodeId);
            if (target == null) return Fail("后续节点不存在：" + Safe(targetNodeId), out message);
            return Enter(venue, target, out message);
        }

        internal bool Enter(Venue targetVenue, Node targetNode, out string message)
        {
            message = "";
            if (steps >= MaxSessionSteps)
            {
                stopped = true;
                return Fail("流程超过 " + MaxSessionSteps + " 步预算，已停止", out message);
            }

            venueId = targetVenue.id;
            nodeId = targetNode.id;
            steps++;
            log.Add("进入阶段：" + venueId + "/" + nodeId);

            List<Clue> clues = targetVenue.clues ?? new List<Clue>();
            foreach (Clue clue in clues)
            {
                if (clue == null || Blank(clue.id)) continue;
                string key = ClueKey(targetVenue.id, clue.id);
                if (string.Equals(clue.trigger, "stage", StringComparison.Ordinal) && string.Equals(clue.foundAt, targetNode.id, StringComparison.Ordinal) && found.Add(key))
                    log.Add("找到线索：" + key);

                // 不记录 truth；只有已找到的线索到达 revealAt 才进入 revealed。
                if (string.Equals(clue.revealAt, targetNode.id, StringComparison.Ordinal) && found.Contains(key) && revealed.Add(key))
                    log.Add("揭示线索：" + key);
            }

            if (targetNode.kind == "ending")
            {
                results[targetVenue.id] = targetNode.result ?? "";
                log.Add("本地结果：" + targetVenue.id + "=" + Safe(targetNode.result));
            }
            error = "";
            return true;
        }

        private Link FindLink(string from, string to)
        {
            if (pack == null || pack.links == null) return null;
            Link first = null;
            string result;
            bool completed = results.TryGetValue(from, out result);
            foreach (Link link in pack.links)
                if (link != null && string.Equals(link.from, from, StringComparison.Ordinal) && string.Equals(link.to, to, StringComparison.Ordinal))
                {
                    if (first == null) first = link;
                    if (completed && (link.requires == "any" || link.requires == result)) return link;
                }
            return first;
        }

        private bool Fail(string reason, out string message)
        {
            error = reason;
            message = reason;
            return false;
        }
    }

    public static Session Start(Pack p, out string error)
    {
        error = "";
        if (p == null)
        {
            error = "世界包不存在";
            return null;
        }
        string blocker = RunBlockerForVenue(p, p.entryVenue);
        if (!string.IsNullOrEmpty(blocker))
        {
            error = blocker;
            return null;
        }

        Venue venue = GetVenue(p, p.entryVenue);
        Node entry = venue == null ? null : GetNode(venue, venue.entry);
        if (venue == null || entry == null)
        {
            error = "入口会场或入口节点不存在";
            return null;
        }

        var session = new Session(p);
        if (!session.Enter(venue, entry, out error)) return null;
        return session;
    }

    private static void ValidateVenue(Venue venue, List<Issue> issues, Func<string, bool> roomExists)
    {
        string venueId = venue.id ?? "";
        if (venue.difficulty < 0)
            Add(issues, "error", "venue_difficulty_invalid", venueId, "", "会场难度不能小于 0");
        if (Blank(venue.role))
            Add(issues, "error", "venue_role_required", venueId, "", "会场需要 role");
        else if (!string.Equals(venue.role, "trickster", StringComparison.Ordinal) && !string.Equals(venue.role, "player", StringComparison.Ordinal))
            Add(issues, "warn", "unsupported_role", venueId, "", "角色尚待适配：" + venue.role + "；草稿可以保存但不能运行此会场");

        var nodes = venue.nodes ?? new List<Node>();
        if (nodes.Count == 0)
            Add(issues, "error", "venue_nodes_required", venueId, "", "会场至少需要一个节点");
        var nodesById = new Dictionary<string, Node>(StringComparer.Ordinal);
        for (int i = 0; i < nodes.Count; i++)
        {
            Node node = nodes[i];
            if (node == null)
            {
                Add(issues, "error", "node_null", venueId, "", "第 " + (i + 1) + " 个节点为空");
                continue;
            }
            if (Blank(node.id))
            {
                Add(issues, "error", "node_id_required", venueId, "", "第 " + (i + 1) + " 个节点需要 id");
                continue;
            }
            if (InvalidKey(node.id)) Add(issues, "error", "id_key_invalid", venueId, node.id, "ID不能含斜杠或控制字符；显示名称不受此限制");
            if (nodesById.ContainsKey(node.id))
                Add(issues, "error", "node_id_duplicate", venueId, node.id, "节点 id 重复：" + node.id);
            else
                nodesById.Add(node.id, node);
        }

        if (Blank(venue.entry))
            Add(issues, "error", "venue_entry_required", venueId, "", "会场需要 entry 节点");
        else if (!nodesById.ContainsKey(venue.entry))
            Add(issues, "error", "venue_entry_missing", venueId, "", "会场 entry 不存在：" + venue.entry);

        foreach (KeyValuePair<string, Node> pair in nodesById)
        {
            Node node = pair.Value;
            ValidateNodeShape(venue, venueId, node, issues, roomExists);
        }
        foreach (KeyValuePair<string, Node> pair in nodesById)
            ValidateNodeTargets(venueId, pair.Value, nodesById, issues);

        HashSet<string> reachable = ReachableNodeIds(venue, nodesById);
        bool reachableUnsupported = false;
        bool reachableEnding = false;
        foreach (string id in reachable)
        {
            Node node;
            if (!nodesById.TryGetValue(id, out node)) continue;
            if (node.kind == "ending") reachableEnding = true;
            if (!IsRunnableKind(node.kind)) reachableUnsupported = true;
        }
        if (nodesById.Count > 0 && !Blank(venue.entry) && nodesById.ContainsKey(venue.entry) && !reachableEnding && !reachableUnsupported)
            Add(issues, "error", "ending_unreachable", venueId, "", "从会场 entry 无法到达 ending");
        foreach (KeyValuePair<string, Node> pair in nodesById)
            if (!reachable.Contains(pair.Key))
                Add(issues, "warn", "node_unreachable", venueId, pair.Key, "节点从会场 entry 不可达：" + pair.Key);

        ValidateClues(venue, venueId, nodesById, issues);
    }

    private static void ValidateNodeShape(Venue venue, string venueId, Node node, List<Issue> issues, Func<string, bool> roomExists)
    {
        if (Blank(node.kind))
        {
            Add(issues, "error", "node_kind_required", venueId, node.id, "节点需要 kind");
            return;
        }
        if (node.kind == "concept")
            Add(issues, "warn", "unsupported_concept", venueId, node.id, "concept 是可保存的创作草案，需要新增玩法适配器后才能运行");
        else if (!IsRunnableKind(node.kind))
            Add(issues, "warn", "unsupported_node_kind", venueId, node.id, "未知玩法 kind 尚待适配：" + node.kind + "；草稿可以保存");

        if (node.kind == "room")
        {
            if (Blank(node.room))
                Add(issues, "error", "room_required", venueId, node.id, "room 节点需要 room");
            else if (roomExists == null)
                Add(issues, "warn", "room_existence_unchecked", venueId, node.id, "未提供房间查询，无法确认 room 是否可实际运行：" + node.room);
            else
            {
                bool exists;
                try { exists = roomExists(node.room); }
                catch { exists = false; }
                if (!exists)
                    Add(issues, "error", "room_missing", venueId, node.id, "当前房间适配器找不到 room：" + node.room);
            }
        }
        else if (node.kind == "explore")
            ValidateExploreNode(venue, venueId, node, issues);
        else if (node.kind == "game")
        {
            foreach (BorderlandGame.Issue issue in BorderlandGame.Check(node.game))
                Add(issues, issue.severity, issue.code, venueId, node.id, issue.text);
        }
        else if (node.kind == "ending")
        {
            if (Blank(node.result))
                Add(issues, "error", "ending_result_required", venueId, node.id, "ending 节点需要 result");
            else if (node.result == "any")
                Add(issues, "error", "ending_result_invalid", venueId, node.id, "any 是会场链接的通配条件，请用其他自定义结局标签");
        }
    }

    private static void ValidateNodeTargets(string venueId, Node node, Dictionary<string, Node> nodesById, List<Issue> issues)
    {
        if (node.kind == "story" || node.kind == "reveal")
            ValidateTarget(venueId, node.id, node.next, "next", "node_next", nodesById, issues);
        else if (node.kind == "room" || node.kind == "explore" || node.kind == "game")
        {
            string prefix = node.kind;
            ValidateTarget(venueId, node.id, node.onWin, "onWin", prefix + "_win", nodesById, issues);
            ValidateTarget(venueId, node.id, node.onLose, "onLose", prefix + "_lose", nodesById, issues);
        }
    }

    private static void ValidateTarget(string venueId, string nodeId, string target, string label, string codePrefix, Dictionary<string, Node> nodesById, List<Issue> issues)
    {
        if (Blank(target))
            Add(issues, "error", codePrefix + "_required", venueId, nodeId, nodeId + " 缺少 " + label);
        else if (!nodesById.ContainsKey(target))
            Add(issues, "error", codePrefix + "_missing", venueId, nodeId, nodeId + " 的 " + label + " 指向不存在节点：" + target);
    }

    private static void ValidateClues(Venue venue, string venueId, Dictionary<string, Node> nodesById, List<Issue> issues)
    {
        var clues = venue.clues ?? new List<Clue>();
        var cluesById = new Dictionary<string, Clue>(StringComparer.Ordinal);
        for (int i = 0; i < clues.Count; i++)
        {
            Clue clue = clues[i];
            if (clue == null)
            {
                Add(issues, "error", "clue_null", venueId, "", "第 " + (i + 1) + " 条线索为空");
                continue;
            }
            if (Blank(clue.id))
            {
                Add(issues, "error", "clue_id_required", venueId, "", "第 " + (i + 1) + " 条线索需要 id");
                continue;
            }
            if (InvalidKey(clue.id)) Add(issues, "error", "id_key_invalid", venueId, clue.id, "ID不能含斜杠或控制字符；显示名称不受此限制");
            if (cluesById.ContainsKey(clue.id))
                Add(issues, "error", "clue_id_duplicate", venueId, clue.id, "线索 id 重复：" + clue.id);
            else
                cluesById.Add(clue.id, clue);
        }

        foreach (KeyValuePair<string, Clue> pair in cluesById)
        {
            Clue clue = pair.Value;
            if (Blank(clue.trigger))
                Add(issues, "error", "clue_trigger_required", venueId, clue.id, "线索需要 trigger（stage 或 interact）");
            else if (clue.trigger != "stage" && clue.trigger != "interact")
                Add(issues, "error", "clue_trigger_invalid", venueId, clue.id, "线索 trigger 只能是 stage 或 interact");
            if (Blank(clue.foundAt))
                Add(issues, "error", "clue_found_required", venueId, clue.id, "线索需要 foundAt 来源节点");
            else if (!nodesById.ContainsKey(clue.foundAt))
                Add(issues, "error", "clue_found_missing", venueId, clue.id, "线索 foundAt 不存在：" + clue.foundAt);
            else
            {
                Node foundNode;
                if (clue.trigger == "interact" && nodesById.TryGetValue(clue.foundAt, out foundNode) && foundNode.kind != "explore")
                    Add(issues, "error", "clue_interact_found_kind", venueId, clue.id, "interact 线索的 foundAt 必须是 explore 节点");
            }

            if (Blank(clue.revealAt))
                Add(issues, "error", "clue_reveal_required", venueId, clue.id, "线索需要 revealAt 节点");
            else if (!nodesById.ContainsKey(clue.revealAt))
                Add(issues, "error", "clue_reveal_missing", venueId, clue.id, "线索 revealAt 不存在：" + clue.revealAt);

            Node revealNode;
            if (!Blank(clue.revealAt) && nodesById.TryGetValue(clue.revealAt, out revealNode) && revealNode.kind != "reveal")
                Add(issues, "error", "clue_reveal_kind", venueId, clue.id, "线索 revealAt 必须是 reveal 节点");

            if (!Blank(clue.foundAt) && !Blank(clue.revealAt) && nodesById.ContainsKey(clue.foundAt) && nodesById.ContainsKey(clue.revealAt) && !CanReach(venue, nodesById, clue.foundAt, clue.revealAt))
                Add(issues, "error", "clue_reveal_before_found", venueId, clue.id, "线索必须先在 foundAt 找到，之后才能到 revealAt 揭示");
            if (clue.foundAt == clue.revealAt && nodesById.ContainsKey(clue.foundAt))
                Add(issues, "warn", "clue_same_stage", venueId, clue.id, "发现和解释发生在同一阶段；若要反转，应提前提供观察事实");
            else if (CanReachAvoiding(nodesById, venue.entry, clue.revealAt, clue.foundAt))
                Add(issues, "warn", "clue_discovery_bypass", venueId, clue.id, "有路线未发现此线索就进入揭示；该线索真相不会显示。若需要必达线索，请修复分支");
        }
    }

    // 此处是纯静态数据检查。刻意不调用 WorldParkExplore.Create 或 Start，避免 Validate/Create 递归。
    private static void ValidateExploreNode(Venue venue, string venueId, Node node, List<Issue> issues)
    {
        if (node.timeout < 0)
            Add(issues, "error", "explore_timeout_invalid", venueId, node.id, "explore 的 timeout 不能小于 0（0 表示无倒计时）");

        OverworldMap.Map map = null;
        OverworldMap.Cell spawn = new OverworldMap.Cell();
        bool mapUsable = false;
        if (Blank(node.mapText))
            Add(issues, "error", "explore_map_required", venueId, node.id, "explore 节点需要内嵌 mapText");
        else
        {
            List<string> rows = ExploreMapRows(node.mapText);
            if (rows.Count == 0)
                Add(issues, "error", "explore_map_empty", venueId, node.id, "explore 地图没有网格行");
            else
            {
                int width = rows[0].Length;
                for (int i = 1; i < rows.Count; i++)
                    if (rows[i].Length != width)
                    {
                        Add(issues, "error", "explore_map_rectangular", venueId, node.id, "explore 地图每一行必须同宽");
                        break;
                    }
                if (width < 5 || rows.Count < 4 || width > OverworldMap.MaxW || rows.Count > OverworldMap.MaxH)
                    Add(issues, "error", "explore_map_size", venueId, node.id, "explore 地图尺寸必须在 5×4 到 " + OverworldMap.MaxW + "×" + OverworldMap.MaxH + " 之间");
            }

            map = OverworldMap.Parse(node.mapText);
            bool known = true;
            for (int y = 0; y < map.H && known; y++)
                for (int x = 0; x < map.W; x++)
                    if (!OverworldCatalog.Known(map.At(x, y)))
                    {
                        Add(issues, "error", "explore_map_tile_unknown", venueId, node.id, "explore 地图含有未知格子字符：" + map.At(x, y));
                        known = false;
                        break;
                    }
            bool closed = map.W > 0 && map.H > 0;
            for (int x = 0; x < map.W && closed; x++)
                if (!OverworldCatalog.Solid(map.At(x, 0)) || !OverworldCatalog.Solid(map.At(x, map.H - 1))) closed = false;
            for (int y = 0; y < map.H && closed; y++)
                if (!OverworldCatalog.Solid(map.At(0, y)) || !OverworldCatalog.Solid(map.At(map.W - 1, y))) closed = false;
            if (!closed)
                Add(issues, "error", "explore_map_boundary", venueId, node.id, "explore 地图最外一圈必须封闭且挡路");

            List<OverworldMap.Cell> spawns = OverworldMap.Find(map, 'T');
            if (spawns.Count != 1)
                Add(issues, "error", "explore_spawn_unique", venueId, node.id, "explore 地图需要且只能有一个 T 出生点");
            else
            {
                spawn = spawns[0];
                mapUsable = known && closed && OverworldMap.Walkable(map, spawn.x, spawn.y);
            }
        }

        var cluesById = new Dictionary<string, Clue>(StringComparer.Ordinal);
        List<Clue> clues = venue.clues ?? new List<Clue>();
        foreach (Clue clue in clues)
            if (clue != null && !Blank(clue.id) && !cluesById.ContainsKey(clue.id)) cluesById.Add(clue.id, clue);

        var pointsById = new Dictionary<string, ExplorePoint>(StringComparer.Ordinal);
        List<ExplorePoint> points = node.points ?? new List<ExplorePoint>();
        if (points.Count > MaxExplorePointsPerNode)
            Add(issues, "error", "explore_points_limit", venueId, node.id, "explore 现场点超过 " + MaxExplorePointsPerNode + " 个上限");
        bool hasExit = false, hasWinningExit = false;
        int chaserCount = 0;
        for (int i = 0; i < points.Count; i++)
        {
            ExplorePoint point = points[i];
            if (point == null)
            {
                Add(issues, "error", "explore_point_null", venueId, node.id, "第 " + (i + 1) + " 个探索点为空");
                continue;
            }
            if (Blank(point.id))
                Add(issues, "error", "explore_point_id_required", venueId, node.id, "每个探索点需要 id");
            else if (InvalidKey(point.id))
                Add(issues, "error", "id_key_invalid", venueId, node.id, "ID不能含斜杠或控制字符；显示名称不受此限制");
            else if (pointsById.ContainsKey(point.id))
                Add(issues, "error", "explore_point_id_duplicate", venueId, node.id, "探索点 id 重复：" + point.id);
            else
                pointsById.Add(point.id, point);

            if (point.kind != "clue" && point.kind != "exit" && point.kind != "chaser")
                Add(issues, "error", "explore_point_kind", venueId, node.id, "探索点 kind 只能是 clue、exit 或 chaser");
            if (map != null)
            {
                if (point.x < 0 || point.y < 0 || point.x >= map.W || point.y >= map.H || !OverworldMap.Walkable(map, point.x, point.y))
                    Add(issues, "error", "explore_point_walkable", venueId, node.id, "探索点必须在可通行地图格内");
                else if (mapUsable && OverworldMap.Path(map, spawn, new OverworldMap.Cell(point.x, point.y)) == null)
                    Add(issues, "error", "explore_point_unreachable", venueId, node.id, "探索点必须能从 T 出生点到达");
            }

            if (point.kind == "clue")
            {
                Clue clue;
                if (Blank(point.clue) || !cluesById.TryGetValue(point.clue, out clue))
                    Add(issues, "error", "explore_point_clue_missing", venueId, node.id, "线索点必须引用本会场已有线索");
                else
                {
                    if (clue.trigger != "interact")
                        Add(issues, "error", "explore_point_clue_trigger", venueId, node.id, "线索点引用的线索 trigger 必须是 interact");
                    if (!string.Equals(clue.foundAt, node.id, StringComparison.Ordinal))
                        Add(issues, "error", "explore_point_clue_stage", venueId, node.id, "线索点引用的线索 foundAt 必须是当前 explore 节点");
                }
            }
            else if (point.kind == "chaser")
            {
                chaserCount++;
                if (point.speed != 0 && (point.speed < 10 || point.speed > 80))
                    Add(issues, "error", "explore_chaser_speed", venueId, node.id, "追逐者 speed 是十分之一格/秒：0=默认 34，否则须在 10–80（玩家是 40）");
                if (point.sight != 0 && (point.sight < 1 || point.sight > 20))
                    Add(issues, "error", "explore_chaser_sight", venueId, node.id, "追逐者 sight（视距格数）：0=默认 7，否则须在 1–20");
                List<string> route = point.route ?? new List<string>();
                bool badRoute = route.Count > MaxChaserRoute;
                foreach (string raw in route)
                {
                    if (badRoute) break;
                    int rx, ry;
                    if (!TryChaserCell(raw, out rx, out ry)) { badRoute = true; break; }
                    if (map == null) continue;
                    if (rx < 0 || ry < 0 || rx >= map.W || ry >= map.H || !OverworldMap.Walkable(map, rx, ry)) badRoute = true;
                    else if (mapUsable && OverworldMap.Path(map, spawn, new OverworldMap.Cell(rx, ry)) == null) badRoute = true;
                }
                if (badRoute)
                    Add(issues, "error", "explore_chaser_route", venueId, node.id, "追逐者 route 须是至多 " + MaxChaserRoute + " 个 \"x,y\" 巡逻格，且都可通行并能从 T 到达");
                if (mapUsable)
                {
                    int sx = point.x - spawn.x, sy = point.y - spawn.y;
                    if (sx * sx + sy * sy < 16)
                        Add(issues, "warn", "explore_chaser_spawn_close", venueId, node.id, "追逐者出生格离 T 不到 4 格，玩家一开场就可能被抓");
                }
            }
            else if (point.kind == "exit")
            {
                hasExit = true;
                if (point.outcome != "win" && point.outcome != "lose")
                    Add(issues, "error", "explore_exit_outcome", venueId, node.id, "出口 outcome 只能是 win 或 lose");
                if (point.outcome == "win") hasWinningExit = true;
                var seenRequires = new HashSet<string>(StringComparer.Ordinal);
                List<string> requires = point.requires ?? new List<string>();
                foreach (string requirement in requires)
                {
                    if (Blank(requirement) || !seenRequires.Add(requirement))
                    {
                        Add(issues, "error", "explore_exit_requires_invalid", venueId, node.id, "出口 requires 不能为空或重复");
                        continue;
                    }
                    if (!cluesById.ContainsKey(requirement))
                        Add(issues, "error", "explore_exit_requires_missing", venueId, node.id, "出口 requires 引用了不存在的线索：" + requirement);
                }
            }
        }
        if (!hasExit)
            Add(issues, "error", "explore_exit_required", venueId, node.id, "explore 至少需要一个出口点");
        else if (!hasWinningExit)
            Add(issues, "warn", "explore_exit_win_required", venueId, node.id, "该探索没有win出口；若不是作者有意设计的悲剧，请补充可达成目标");
        if (chaserCount > MaxChasers)
            Add(issues, "error", "explore_chaser_limit", venueId, node.id, "每个 explore 阶段最多 " + MaxChasers + " 个追逐者");
    }

    private static List<string> ExploreMapRows(string text)
    {
        var rows = new List<string>();
        foreach (string raw in (text ?? "").Replace("\r", "").Replace("\uFEFF", "").Split('\n'))
        {
            if (raw.StartsWith("#", StringComparison.Ordinal)) continue;
            string row = raw.TrimEnd();
            if (row.Length > 0) rows.Add(row.Replace(' ', '.'));
        }
        return rows;
    }

    private static void ValidateLinks(List<Link> links, Dictionary<string, Venue> venuesById, List<Issue> issues)
    {
        var safeLinks = links ?? new List<Link>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < safeLinks.Count; i++)
        {
            Link link = safeLinks[i];
            if (link == null)
            {
                Add(issues, "error", "link_null", "", "", "第 " + (i + 1) + " 条会场连接为空");
                continue;
            }
            string owner = link.from ?? "";
            if (Blank(link.from))
                Add(issues, "error", "link_from_required", "", "", "会场连接需要 from");
            else if (!venuesById.ContainsKey(link.from))
                Add(issues, "error", "link_from_missing", owner, "", "连接来源会场不存在：" + link.from);
            if (Blank(link.to))
                Add(issues, "error", "link_to_required", owner, "", "会场连接需要 to");
            else if (!venuesById.ContainsKey(link.to))
                Add(issues, "error", "link_to_missing", owner, "", "连接目标会场不存在：" + link.to);
            if (Blank(link.requires))
                Add(issues, "error", "link_requires_required", owner, "", "会场连接需要 requires");
            else if (link.requires != "any" && venuesById.ContainsKey(owner))
            {
                bool possible = false;
                foreach (var n in venuesById[owner].nodes ?? new List<Node>())
                    if (n != null && n.kind == "ending" && n.result == link.requires) { possible = true; break; }
                if (!possible) Add(issues, "warn", "link_result_unreachable", owner, "", "来源会场没有结局标签：" + link.requires + "；这条连接暂时无法解锁");
            }

            string key = Safe(link.from) + "\u001f" + Safe(link.to) + "\u001f" + Safe(link.requires);
            if (!seen.Add(key))
                Add(issues, "warn", "link_duplicate", owner, "", "会场连接重复：" + Safe(link.from) + " → " + Safe(link.to));
        }
    }

    private static string RunBlockerForVenue(Pack pack, string activeVenueId)
    {
        List<Issue> issues = Validate(pack);
        Venue venue = GetVenue(pack, activeVenueId);
        Dictionary<string, Node> nodesById = NodeMap(venue);
        HashSet<string> reachable = venue == null ? new HashSet<string>(StringComparer.Ordinal) : ReachableNodeIds(venue, nodesById);
        foreach (Issue issue in issues)
        {
            if (issue == null) continue;
            bool isActive = string.Equals(issue.venue, activeVenueId, StringComparison.Ordinal);
            bool isGlobal = string.IsNullOrEmpty(issue.venue);
            bool isLink = issue.code != null && issue.code.StartsWith("link_", StringComparison.Ordinal);
            bool nodeIsReachable = !string.IsNullOrEmpty(issue.node) && reachable.Contains(issue.node);

            bool isClueOrIdentity = issue.code != null && (issue.code.StartsWith("clue_", StringComparison.Ordinal) || issue.code == "id_key_invalid");
            if (issue.severity == "error" && !isLink && (isGlobal || (isActive && (string.IsNullOrEmpty(issue.node) || nodeIsReachable || isClueOrIdentity))))
                return issue.text;
            if (issue.severity == "warn" && isActive && issue.code != null && issue.code.StartsWith("unsupported_", StringComparison.Ordinal) && (string.IsNullOrEmpty(issue.node) || nodeIsReachable))
                return issue.text;
        }
        return "";
    }

    private static Dictionary<string, Node> NodeMap(Venue venue)
    {
        var map = new Dictionary<string, Node>(StringComparer.Ordinal);
        if (venue == null || venue.nodes == null) return map;
        foreach (Node node in venue.nodes)
            if (node != null && !Blank(node.id) && !map.ContainsKey(node.id)) map.Add(node.id, node);
        return map;
    }

    private static HashSet<string> ReachableNodeIds(Venue venue, Dictionary<string, Node> nodesById)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        if (venue == null || Blank(venue.entry) || !nodesById.ContainsKey(venue.entry)) return visited;
        var pending = new Queue<string>();
        pending.Enqueue(venue.entry);
        while (pending.Count > 0)
        {
            string id = pending.Dequeue();
            if (!visited.Add(id)) continue;
            Node node;
            if (!nodesById.TryGetValue(id, out node)) continue;
            foreach (string target in Outgoing(node))
                if (!Blank(target) && nodesById.ContainsKey(target) && !visited.Contains(target)) pending.Enqueue(target);
        }
        return visited;
    }

    private static bool CanReach(Venue venue, Dictionary<string, Node> nodesById, string from, string to)
    {
        if (string.Equals(from, to, StringComparison.Ordinal)) return true;
        if (!nodesById.ContainsKey(from) || !nodesById.ContainsKey(to)) return false;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        pending.Enqueue(from);
        while (pending.Count > 0)
        {
            string id = pending.Dequeue();
            if (!visited.Add(id)) continue;
            Node node;
            if (!nodesById.TryGetValue(id, out node)) continue;
            foreach (string target in Outgoing(node))
            {
                if (string.Equals(target, to, StringComparison.Ordinal)) return true;
                if (!Blank(target) && nodesById.ContainsKey(target) && !visited.Contains(target)) pending.Enqueue(target);
            }
        }
        return false;
    }

    private static IEnumerable<string> Outgoing(Node node)
    {
        if (node == null) yield break;
        if (node.kind == "story" || node.kind == "reveal")
        {
            yield return node.next;
            yield break;
        }
        if (node.kind == "room")
        {
            yield return node.onWin;
            yield return node.onLose;
        }
        if (node.kind == "explore")
        {
            yield return node.onWin;
            yield return node.onLose;
        }
        if (node.kind == "game")
        {
            yield return node.onWin;
            yield return node.onLose;
        }
    }

    private static bool IsRunnableKind(string kind)
    {
        return kind == "story" || kind == "room" || kind == "explore" || kind == "game" || kind == "reveal" || kind == "ending";
    }

    private static bool CanReachAvoiding(Dictionary<string, Node> nodes, string from, string to, string excluded)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal); var pending = new Queue<string>();
        if (!Blank(from)) pending.Enqueue(from);
        while (pending.Count > 0)
        {
            string id = pending.Dequeue();
            if (id == excluded || !seen.Add(id) || !nodes.TryGetValue(id, out Node n)) continue;
            if (id == to) return true;
            foreach (string next in Outgoing(n)) if (!Blank(next)) pending.Enqueue(next);
        }
        return false;
    }

    private static void Add(List<Issue> issues, string severity, string code, string venue, string node, string text)
    {
        issues.Add(new Issue
        {
            severity = severity,
            code = code,
            venue = venue ?? "",
            node = node ?? "",
            text = text ?? ""
        });
    }

    private static bool Blank(string value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    private static bool InvalidKey(string value)
    {
        if (value == null) return false;
        foreach (char c in value) if (c == '/' || c < 0x20) return true;
        return false;
    }

    private static string ClueKey(string venueId, string clueId)
    {
        return Safe(venueId) + "/" + Safe(clueId);
    }

    private static string Safe(string value)
    {
        return value ?? "";
    }

    private static List<Venue> ParseVenues(List<object> values)
    {
        var venues = new List<Venue>();
        int totalNodes = 0;
        for (int i = 0; i < values.Count; i++)
        {
            Dictionary<string, object> value = ObjectAt(values[i], "venues[" + i + "]");
            var venue = new Venue
            {
                id = OptionalString(value, "id", "venues[" + i + "]"),
                name = OptionalString(value, "name", "venues[" + i + "]"),
                setting = OptionalString(value, "setting", "venues[" + i + "]"),
                role = OptionalString(value, "role", "venues[" + i + "]"),
                suit = OptionalString(value, "suit", "venues[" + i + "]"),
                difficulty = OptionalInt(value, "difficulty", "venues[" + i + "]", 0),
                rules = OptionalString(value, "rules", "venues[" + i + "]"),
                entry = OptionalString(value, "entry", "venues[" + i + "]")
            };
            List<object> nodes = RequiredArray(value, "nodes", "venues[" + i + "]");
            if (nodes.Count > MaxNodesPerVenue)
                throw new PackFormatException("venues[" + i + "].nodes 超过 " + MaxNodesPerVenue + " 个节点上限");
            totalNodes += nodes.Count;
            if (totalNodes > MaxTotalNodes)
                throw new PackFormatException("世界包节点总数超过 " + MaxTotalNodes + " 上限");
            venue.nodes = ParseNodes(nodes, "venues[" + i + "].nodes");

            List<object> clues = RequiredArray(value, "clues", "venues[" + i + "]");
            if (clues.Count > MaxCluesPerVenue)
                throw new PackFormatException("venues[" + i + "].clues 超过 " + MaxCluesPerVenue + " 条线索上限");
            venue.clues = ParseClues(clues, "venues[" + i + "].clues");
            venues.Add(venue);
        }
        return venues;
    }

    private static List<Node> ParseNodes(List<object> values, string path)
    {
        var nodes = new List<Node>();
        for (int i = 0; i < values.Count; i++)
        {
            Dictionary<string, object> value = ObjectAt(values[i], path + "[" + i + "]");
            nodes.Add(new Node
            {
                id = OptionalString(value, "id", path + "[" + i + "]"),
                kind = OptionalString(value, "kind", path + "[" + i + "]"),
                name = OptionalString(value, "name", path + "[" + i + "]"),
                text = OptionalString(value, "text", path + "[" + i + "]"),
                room = OptionalString(value, "room", path + "[" + i + "]"),
                next = OptionalString(value, "next", path + "[" + i + "]"),
                onWin = OptionalString(value, "onWin", path + "[" + i + "]"),
                onLose = OptionalString(value, "onLose", path + "[" + i + "]"),
                result = OptionalString(value, "result", path + "[" + i + "]"),
                mapText = OptionalString(value, "mapText", path + "[" + i + "]"),
                timeout = OptionalInt(value, "timeout", path + "[" + i + "]", 0),
                points = ParseExplorePoints(OptionalArray(value, "points", path + "[" + i + "]"), path + "[" + i + "].points"),
                game = ParseGame(value, path + "[" + i + "]")
            });
        }
        return nodes;
    }

    private static BorderlandGame.Def ParseGame(Dictionary<string, object> source, string path)
    {
        object raw;
        if (!source.TryGetValue("game", out raw)) return null;
        Dictionary<string, object> value = ObjectAt(raw, path + ".game");
        string where = path + ".game";
        var def = new BorderlandGame.Def
        {
            mode = OptionalString(value, "mode", where),
            rounds = OptionalInt(value, "rounds", where, 0),
            lives = OptionalInt(value, "lives", where, 0),
            ratio = OptionalInt(value, "ratio", where, 0),
            goal = OptionalInt(value, "goal", where, 0),
            seed = OptionalInt(value, "seed", where, 0),
            escalate = OptionalInt(value, "escalate", where, 0)
        };
        List<object> seats = OptionalArray(value, "seats", where);
        if (seats.Count > BorderlandGame.MaxParseSeats)
            throw new PackFormatException(where + ".seats 超过 " + BorderlandGame.MaxParseSeats + " 人上限");
        for (int i = 0; i < seats.Count; i++)
        {
            Dictionary<string, object> seat = ObjectAt(seats[i], where + ".seats[" + i + "]");
            def.seats.Add(new BorderlandGame.Seat
            {
                id = OptionalString(seat, "id", where + ".seats[" + i + "]"),
                name = OptionalString(seat, "name", where + ".seats[" + i + "]"),
                persona = OptionalString(seat, "persona", where + ".seats[" + i + "]")
            });
        }
        return def;
    }

    private static List<Clue> ParseClues(List<object> values, string path)
    {
        var clues = new List<Clue>();
        for (int i = 0; i < values.Count; i++)
        {
            Dictionary<string, object> value = ObjectAt(values[i], path + "[" + i + "]");
            clues.Add(new Clue
            {
                id = OptionalString(value, "id", path + "[" + i + "]"),
                name = OptionalString(value, "name", path + "[" + i + "]"),
                foundAt = OptionalString(value, "foundAt", path + "[" + i + "]"),
                revealAt = OptionalString(value, "revealAt", path + "[" + i + "]"),
                hint = OptionalString(value, "hint", path + "[" + i + "]"),
                truth = OptionalString(value, "truth", path + "[" + i + "]"),
                trigger = value.ContainsKey("trigger") ? OptionalString(value, "trigger", path + "[" + i + "]") : "stage"
            });
        }
        return clues;
    }

    private static List<Link> ParseLinks(List<object> values)
    {
        var links = new List<Link>();
        for (int i = 0; i < values.Count; i++)
        {
            Dictionary<string, object> value = ObjectAt(values[i], "links[" + i + "]");
            links.Add(new Link
            {
                from = OptionalString(value, "from", "links[" + i + "]"),
                to = OptionalString(value, "to", "links[" + i + "]"),
                requires = OptionalString(value, "requires", "links[" + i + "]")
            });
        }
        return links;
    }

    private static Dictionary<string, object> ObjectAt(object value, string path)
    {
        var result = value as Dictionary<string, object>;
        if (result == null) throw new PackFormatException(path + " 必须是对象");
        return result;
    }

    private static List<object> RequiredArray(Dictionary<string, object> source, string key, string path)
    {
        object value;
        if (!source.TryGetValue(key, out value)) throw new PackFormatException(path + " 缺少 " + key);
        var result = value as List<object>;
        if (result == null) throw new PackFormatException(path + "." + key + " 必须是数组");
        return result;
    }

    private static List<object> OptionalArray(Dictionary<string, object> source, string key, string path)
    {
        object value;
        if (!source.TryGetValue(key, out value)) return new List<object>();
        var result = value as List<object>;
        if (result == null) throw new PackFormatException(path + "." + key + " 必须是数组");
        return result;
    }

    private static string RequiredString(Dictionary<string, object> source, string key, string path)
    {
        object value;
        if (!source.TryGetValue(key, out value)) throw new PackFormatException(path + " 缺少 " + key);
        string result = value as string;
        if (result == null) throw new PackFormatException(path + "." + key + " 必须是字符串");
        return result;
    }

    private static string OptionalString(Dictionary<string, object> source, string key, string path)
    {
        object value;
        if (!source.TryGetValue(key, out value)) return "";
        string result = value as string;
        if (result == null) throw new PackFormatException(path + "." + key + " 必须是字符串");
        return result;
    }

    private static int RequiredInt(Dictionary<string, object> source, string key, string path)
    {
        object value;
        if (!source.TryGetValue(key, out value)) throw new PackFormatException(path + " 缺少 " + key);
        return IntValue(value, path + "." + key);
    }

    private static int OptionalInt(Dictionary<string, object> source, string key, string path, int fallback)
    {
        object value;
        if (!source.TryGetValue(key, out value)) return fallback;
        return IntValue(value, path + "." + key);
    }

    /// <summary>S252 追逐者上限（与网页 MAX_CHASERS / MAX_ROUTE 同值）。</summary>
    public const int MaxChasers = 4, MaxChaserRoute = 32, DefaultChaserSpeed = 34, DefaultChaserSight = 7;

    /// <summary>解析巡逻格 "x,y"（只接受两个最多 6 字符的十进制整数，不允许空格；与网页 chaserCell 同语义）。</summary>
    public static bool TryChaserCell(string raw, out int x, out int y)
    {
        x = y = 0;
        if (string.IsNullOrEmpty(raw)) return false;
        string[] parts = raw.Split(',');
        if (parts.Length != 2) return false;
        return TryInt(parts[0], out x) && TryInt(parts[1], out y);
    }

    private static bool TryInt(string s, out int value)
    {
        value = 0;
        if (s.Length == 0 || s.Length > 6) return false;
        int start = s[0] == '-' ? 1 : 0;
        if (start == s.Length) return false;
        long n = 0;
        for (int i = start; i < s.Length; i++)
        {
            if (s[i] < '0' || s[i] > '9') return false;
            n = n * 10 + (s[i] - '0');
        }
        value = (int)(start == 1 ? -n : n);
        return true;
    }

    private static int IntValue(object value, string path)
    {
        if (!(value is double)) throw new PackFormatException(path + " 必须是整数");
        double number = (double)value;
        if (double.IsNaN(number) || double.IsInfinity(number) || number < int.MinValue || number > int.MaxValue || Math.Floor(number) != number)
            throw new PackFormatException(path + " 必须是整数");
        return (int)number;
    }

    private static List<ExplorePoint> ParseExplorePoints(List<object> values, string path)
    {
        if (values.Count > MaxExplorePointsPerNode)
            throw new PackFormatException(path + " 超过 " + MaxExplorePointsPerNode + " 个现场点上限");
        var points = new List<ExplorePoint>();
        for (int i = 0; i < values.Count; i++)
        {
            Dictionary<string, object> value = ObjectAt(values[i], path + "[" + i + "]");
            points.Add(new ExplorePoint
            {
                id = OptionalString(value, "id", path + "[" + i + "]"),
                kind = OptionalString(value, "kind", path + "[" + i + "]"),
                label = OptionalString(value, "label", path + "[" + i + "]"),
                clue = OptionalString(value, "clue", path + "[" + i + "]"),
                outcome = OptionalString(value, "outcome", path + "[" + i + "]"),
                x = OptionalInt(value, "x", path + "[" + i + "]", 0),
                y = OptionalInt(value, "y", path + "[" + i + "]", 0),
                requires = OptionalStringList(value, "requires", path + "[" + i + "]"),
                speed = OptionalInt(value, "speed", path + "[" + i + "]", 0),
                sight = OptionalInt(value, "sight", path + "[" + i + "]", 0),
                route = OptionalStringList(value, "route", path + "[" + i + "]")
            });
        }
        return points;
    }

    private static List<string> OptionalStringList(Dictionary<string, object> source, string key, string path)
    {
        List<object> values = OptionalArray(source, key, path);
        var result = new List<string>();
        for (int i = 0; i < values.Count; i++)
        {
            string value = values[i] as string;
            if (value == null) throw new PackFormatException(path + "." + key + "[" + i + "] 必须是字符串");
            result.Add(value);
        }
        return result;
    }

    private static bool CheckJsonBudgetAndEscapes(string json, out string error)
    {
        error = "";
        bool inString = false;
        int depth = 0;
        int containers = 0;
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (inString)
            {
                if (c < 0x20)
                {
                    error = "JSON 字符串包含未转义的控制字符";
                    return false;
                }
                if (c == '\\')
                {
                    if (++i >= json.Length)
                    {
                        error = "JSON 字符串转义不完整";
                        return false;
                    }
                    char escape = json[i];
                    if (escape == 'u')
                    {
                        if (i + 4 >= json.Length)
                        {
                            error = "JSON Unicode 转义不完整";
                            return false;
                        }
                        for (int digit = 1; digit <= 4; digit++)
                            if (!IsHex(json[i + digit]))
                            {
                                error = "JSON Unicode 转义不是十六进制";
                                return false;
                            }
                        i += 4;
                    }
                    else if (escape != '"' && escape != '\\' && escape != '/' && escape != 'b' && escape != 'f' && escape != 'n' && escape != 'r' && escape != 't')
                    {
                        error = "JSON 包含不支持的字符串转义";
                        return false;
                    }
                    continue;
                }
                if (c == '"') inString = false;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }
            if (char.IsWhiteSpace(c) && c != ' ' && c != '\t' && c != '\r' && c != '\n')
            { error = "JSON包含不支持的空白字符"; return false; }
            if (c == '-' || c == '+' || c == '.' || (c >= '0' && c <= '9'))
            {
                int start = i;
                while (i + 1 < json.Length && "0123456789+-.eE".IndexOf(json[i + 1]) >= 0) i++;
                if (!JsonNumber.IsMatch(json.Substring(start, i - start + 1)))
                { error = "JSON数字格式不正确"; return false; }
                continue;
            }
            if (c == '{' || c == '[')
            {
                containers++;
                depth++;
                if (containers > 2048 || depth > 64)
                {
                    error = "JSON 容器层级或数量超过安全上限";
                    return false;
                }
            }
            else if (c == '}' || c == ']')
            {
                depth--;
                if (depth < 0)
                {
                    error = "JSON 容器结构不平衡";
                    return false;
                }
            }
        }
        if (inString)
        {
            error = "JSON 字符串没有结束引号";
            return false;
        }
        if (depth != 0)
        {
            error = "JSON 容器结构不平衡";
            return false;
        }
        return true;
    }

    private static bool IsHex(char value)
    {
        return (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f') || (value >= 'A' && value <= 'F');
    }

    // 保持旧MiniJson不变；这里先验证新世界格式的严格语法，再使用它读取数据。
    private static bool CheckJsonSyntax(string json)
    {
        // 对象: 0首键/结束,1冒号,2值,3逗号/结束,6逗号后必需键；数组:4首值/结束,5逗号/结束,7逗号后必需值。
        var stack = new List<int>(); bool rootRead = false;
        int index = json.Length > 0 && json[0] == '\ufeff' ? 1 : 0;
        while (index < json.Length)
        {
            while (index < json.Length && " \t\r\n".IndexOf(json[index]) >= 0) index++;
            if (index == json.Length) break;
            Match token = JsonToken.Match(json,index);
            if (!token.Success || token.Index != index) return false;
            index += token.Length; int top = stack.Count - 1, state = top < 0 ? -1 : stack[top];
            string p = token.Groups["punct"].Value;
            if (p == "}" || p == "]")
            {
                if (top < 0 || (p == "}" ? state != 0 && state != 3 : state != 4 && state != 5)) return false;
                stack.RemoveAt(top); continue;
            }
            if (p == ",")
            {
                if (state == 3) stack[top] = 6;
                else if (state == 5) stack[top] = 7;
                else return false;
                continue;
            }
            if (p == ":") { if (state != 1) return false; stack[top] = 2; continue; }
            if ((state == 0 || state == 6) && token.Groups["str"].Success) { stack[top] = 1; continue; }
            bool valueExpected = top < 0 ? !rootRead : state == 2 || state == 4 || state == 7;
            if (!valueExpected) return false;
            if (top < 0) rootRead = true;
            else stack[top] = state == 2 ? 3 : 5;
            if (p == "{") stack.Add(0);
            else if (p == "[") stack.Add(4);
            else if (!token.Groups["str"].Success && !token.Groups["num"].Success && !token.Groups["lit"].Success) return false;
        }
        return rootRead && stack.Count == 0;
    }

    private static void Name(StringBuilder sb, ref bool first, string name)
    {
        if (!first) sb.Append(',');
        first = false;
        AppendString(sb, name);
        sb.Append(':');
    }

    private static void StringProperty(StringBuilder sb, ref bool first, string name, string value)
    {
        Name(sb, ref first, name);
        AppendString(sb, value);
    }

    private static void IntProperty(StringBuilder sb, ref bool first, string name, int value)
    {
        Name(sb, ref first, name);
        sb.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendVenues(StringBuilder sb, List<Venue> venues)
    {
        sb.Append('[');
        if (venues != null)
            for (int i = 0; i < venues.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendVenue(sb, venues[i] ?? new Venue());
            }
        sb.Append(']');
    }

    private static void AppendVenue(StringBuilder sb, Venue venue)
    {
        sb.Append('{');
        bool first = true;
        StringProperty(sb, ref first, "id", venue.id);
        StringProperty(sb, ref first, "name", venue.name);
        StringProperty(sb, ref first, "setting", venue.setting);
        StringProperty(sb, ref first, "role", venue.role);
        StringProperty(sb, ref first, "suit", venue.suit);
        IntProperty(sb, ref first, "difficulty", venue.difficulty);
        StringProperty(sb, ref first, "rules", venue.rules);
        StringProperty(sb, ref first, "entry", venue.entry);
        Name(sb, ref first, "nodes");
        AppendNodes(sb, venue.nodes);
        Name(sb, ref first, "clues");
        AppendClues(sb, venue.clues);
        sb.Append('}');
    }

    private static void AppendNodes(StringBuilder sb, List<Node> nodes)
    {
        sb.Append('[');
        if (nodes != null)
            for (int i = 0; i < nodes.Count; i++)
            {
                if (i > 0) sb.Append(',');
                Node node = nodes[i] ?? new Node();
                sb.Append('{');
                bool first = true;
                StringProperty(sb, ref first, "id", node.id);
                StringProperty(sb, ref first, "kind", node.kind);
                StringProperty(sb, ref first, "name", node.name);
                StringProperty(sb, ref first, "text", node.text);
                StringProperty(sb, ref first, "room", node.room);
                StringProperty(sb, ref first, "next", node.next);
                StringProperty(sb, ref first, "onWin", node.onWin);
                StringProperty(sb, ref first, "onLose", node.onLose);
                StringProperty(sb, ref first, "result", node.result);
                StringProperty(sb, ref first, "mapText", node.mapText);
                IntProperty(sb, ref first, "timeout", node.timeout);
                Name(sb, ref first, "points");
                AppendExplorePoints(sb, node.points);
                if (node.game != null)
                {
                    Name(sb, ref first, "game");
                    AppendGame(sb, node.game);
                }
                sb.Append('}');
            }
        sb.Append(']');
    }

    private static void AppendGame(StringBuilder sb, BorderlandGame.Def game)
    {
        sb.Append('{');
        bool first = true;
        StringProperty(sb, ref first, "mode", game.mode);
        IntProperty(sb, ref first, "rounds", game.rounds);
        IntProperty(sb, ref first, "lives", game.lives);
        IntProperty(sb, ref first, "ratio", game.ratio);
        IntProperty(sb, ref first, "goal", game.goal);
        IntProperty(sb, ref first, "seed", game.seed);
        IntProperty(sb, ref first, "escalate", game.escalate);
        Name(sb, ref first, "seats");
        sb.Append('[');
        List<BorderlandGame.Seat> seats = game.seats ?? new List<BorderlandGame.Seat>();
        for (int i = 0; i < seats.Count; i++)
        {
            if (i > 0) sb.Append(',');
            BorderlandGame.Seat seat = seats[i] ?? new BorderlandGame.Seat();
            sb.Append('{');
            bool seatFirst = true;
            StringProperty(sb, ref seatFirst, "id", seat.id);
            StringProperty(sb, ref seatFirst, "name", seat.name);
            StringProperty(sb, ref seatFirst, "persona", seat.persona);
            sb.Append('}');
        }
        sb.Append(']');
        sb.Append('}');
    }

    private static void AppendClues(StringBuilder sb, List<Clue> clues)
    {
        sb.Append('[');
        if (clues != null)
            for (int i = 0; i < clues.Count; i++)
            {
                if (i > 0) sb.Append(',');
                Clue clue = clues[i] ?? new Clue();
                sb.Append('{');
                bool first = true;
                StringProperty(sb, ref first, "id", clue.id);
                StringProperty(sb, ref first, "name", clue.name);
                StringProperty(sb, ref first, "foundAt", clue.foundAt);
                StringProperty(sb, ref first, "revealAt", clue.revealAt);
                StringProperty(sb, ref first, "hint", clue.hint);
                StringProperty(sb, ref first, "truth", clue.truth);
                StringProperty(sb, ref first, "trigger", clue.trigger);
                sb.Append('}');
            }
        sb.Append(']');
    }

    private static void AppendLinks(StringBuilder sb, List<Link> links)
    {
        sb.Append('[');
        if (links != null)
            for (int i = 0; i < links.Count; i++)
            {
                if (i > 0) sb.Append(',');
                Link link = links[i] ?? new Link();
                sb.Append('{');
                bool first = true;
                StringProperty(sb, ref first, "from", link.from);
                StringProperty(sb, ref first, "to", link.to);
                StringProperty(sb, ref first, "requires", link.requires);
                sb.Append('}');
            }
        sb.Append(']');
    }

    private static void AppendExplorePoints(StringBuilder sb, List<ExplorePoint> points)
    {
        sb.Append('[');
        if (points != null)
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0) sb.Append(',');
                ExplorePoint point = points[i] ?? new ExplorePoint();
                sb.Append('{');
                bool first = true;
                StringProperty(sb, ref first, "id", point.id);
                StringProperty(sb, ref first, "kind", point.kind);
                StringProperty(sb, ref first, "label", point.label);
                StringProperty(sb, ref first, "clue", point.clue);
                StringProperty(sb, ref first, "outcome", point.outcome);
                IntProperty(sb, ref first, "x", point.x);
                IntProperty(sb, ref first, "y", point.y);
                Name(sb, ref first, "requires");
                AppendStringList(sb, point.requires);
                if (point.kind == "chaser")
                {
                    IntProperty(sb, ref first, "speed", point.speed);
                    IntProperty(sb, ref first, "sight", point.sight);
                    Name(sb, ref first, "route");
                    AppendStringList(sb, point.route);
                }
                sb.Append('}');
            }
        sb.Append(']');
    }

    private static void AppendStringList(StringBuilder sb, List<string> values)
    {
        sb.Append('[');
        if (values != null)
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendString(sb, values[i]);
            }
        sb.Append(']');
    }

    private static void AppendString(StringBuilder sb, string value)
    {
        sb.Append('"');
        string source = value ?? "";
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20 || (char.IsHighSurrogate(c) && (i + 1 >= source.Length || !char.IsLowSurrogate(source[i + 1]))) || (char.IsLowSurrogate(c) && (i == 0 || !char.IsHighSurrogate(source[i - 1]))))
                    {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                        sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }

    private sealed class PackFormatException : Exception
    {
        public PackFormatException(string message) : base(message) { }
    }
}
