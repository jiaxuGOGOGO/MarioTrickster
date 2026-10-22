using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// 世界乐园的安全进度检查点。存档只保存可重放的动作和冗余的末状态，
/// 不保存整个世界包、作者真相、提示文本或运行日志。
/// </summary>
public static class WorldParkProgress
{
    public const string TypeTag = "mariotrickster-worldpark-progress";
    public const int Version = 1;
    public const int MaxActions = 2048;
    public const int MaxSaveLength = 262144;

    /// <summary>一次已成功执行的流程动作；value：win/lose、目标会场或线索 id。</summary>
    [Serializable]
    public sealed class Action
    {
        public string kind = "";
        public string value = "";
    }

    private sealed class ResultEntry
    {
        public string venue = "";
        public string result = "";
    }

    private sealed class SaveData
    {
        public string fingerprint = "";
        public readonly List<Action> actions = new List<Action>();
        public string venue = "";
        public string node = "";
        public readonly List<ResultEntry> results = new List<ResultEntry>();
    }

    /// <summary>
    /// 以 WorldParkPack.ToJson 的稳定字段顺序计算当前包的 SHA-256。
    /// 该指纹用于隔离改包后的旧检查点，不是反作弊签名。
    /// </summary>
    public static string Fingerprint(WorldParkPack.Pack pack)
    {
        if (pack == null) throw new ArgumentNullException(nameof(pack));
        byte[] source = Encoding.UTF8.GetBytes(WorldParkPack.ToJson(pack));
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(source);
            var text = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return text.ToString();
        }
    }

    /// <summary>
    /// 只在 story、reveal、ending 保存检查点。room/explore 中途的活动位置、计时和未提交状态不会进入存档。
    /// 返回的 JSON 不含 Pack、truth、hint、log、found 或 revealed；这些都从动作重放推导。
    /// </summary>
    public static string Save(WorldParkPack.Session session, out string error)
    {
        error = "";
        if (session == null) { error = "世界会话不存在，不能保存进度"; return null; }
        if (session.pack == null) { error = "世界会话没有世界包，不能保存进度"; return null; }
        if (!IsSafeCheckpoint(session, out error)) return null;
        if (session.actions == null) { error = "世界会话没有动作历史，不能保存进度"; return null; }
        if (session.actions.Count > MaxActions)
        {
            error = "动作历史超过 " + MaxActions + " 条上限，不能保存进度";
            return null;
        }

        var data = new SaveData();
        try { data.fingerprint = Fingerprint(session.pack); }
        catch (Exception ex)
        {
            error = "世界包指纹无法计算：" + ex.Message;
            return null;
        }

        for (int i = 0; i < session.actions.Count; i++)
        {
            Action action = session.actions[i];
            if (!CheckActionShape(action, out string actionError))
            {
                error = "第 " + (i + 1).ToString(CultureInfo.InvariantCulture) + " 条动作无效：" + actionError;
                return null;
            }
            data.actions.Add(new Action { kind = action.kind, value = action.value });
        }

        data.venue = session.venueId ?? "";
        data.node = session.nodeId ?? "";
        AppendSortedResults(data.results, session.results);
        if (data.results.Count > WorldParkPack.MaxVenues)
        {
            error = "会场结果超过 " + WorldParkPack.MaxVenues + " 条上限，不能保存进度";
            return null;
        }

        string json = WriteSave(data);
        if (ExceedsSaveBudget(json))
        {
            error = "进度存档超过 " + MaxSaveLength + " 字符或UTF-8字节上限";
            return null;
        }

        // 不信任可变的公开 Session 字段：保存前用同一事件序列重放一次，确认当前状态确由历史推导。
        WorldParkPack.Session replayed = Restore(session.pack, json, out string replayError);
        if (replayed == null)
        {
            error = "动作历史不能重放，拒绝保存进度：" + replayError;
            return null;
        }
        if (!Equivalent(session, replayed))
        {
            error = "当前会话状态与动作历史不一致，拒绝保存进度";
            return null;
        }
        return json;
    }

    /// <summary>
    /// 先检查存档容器、长度、动作数量和包指纹，再从 WorldParkPack.Start 严格重放。
    /// 成功后只返回重放得到的新会话；绝不把输入中的末状态、结果或私有状态直接写入 Session。
    /// </summary>
    public static WorldParkPack.Session Restore(WorldParkPack.Pack pack, string json, out string error)
    {
        error = "";
        if (pack == null) { error = "世界包不存在，不能恢复进度"; return null; }
        if (string.IsNullOrWhiteSpace(json)) { error = "进度存档为空"; return null; }
        if (ExceedsSaveBudget(json))
        {
            error = "进度存档超过 " + MaxSaveLength + " 字符或UTF-8字节上限";
            return null;
        }

        SaveData data = ParseSave(json, out error);
        if (data == null) return null;
        if (data.actions.Count > MaxActions)
        {
            error = "进度动作超过 " + MaxActions + " 条上限";
            return null;
        }
        if (data.results.Count > WorldParkPack.MaxVenues)
        {
            error = "进度结果容器超过 " + WorldParkPack.MaxVenues + " 条上限";
            return null;
        }

        string fingerprint;
        try { fingerprint = Fingerprint(pack); }
        catch (Exception ex)
        {
            error = "世界包指纹无法计算：" + ex.Message;
            return null;
        }
        if (!string.Equals(data.fingerprint, fingerprint, StringComparison.Ordinal))
        {
            error = "进度存档对应的世界包指纹不一致；世界包已变化，请从当前包重新开始";
            return null;
        }

        WorldParkPack.Session session = WorldParkPack.Start(pack, out string startError);
        if (session == null)
        {
            error = "当前世界包无法开始，不能恢复进度：" + startError;
            return null;
        }

        for (int i = 0; i < data.actions.Count; i++)
        {
            Action action = data.actions[i];
            if (!CheckActionShape(action, out string actionError))
            {
                error = "第 " + (i + 1).ToString(CultureInfo.InvariantCulture) + " 条动作无效：" + actionError;
                return null;
            }
            if (!ReplayOne(session, action, out string replayError))
            {
                error = "第 " + (i + 1).ToString(CultureInfo.InvariantCulture) + " 条动作无法按当前规则重放：" + replayError;
                return null;
            }
        }

        // Session 的 actions 由流程方法在成功时记录。这里仅核验，不能再手动 Append，否则会制造双记录。
        if (!SameActions(session.actions, data.actions))
        {
            error = "恢复后的动作历史与存档不一致；动作记录接线可能缺失或重复";
            return null;
        }
        if (!string.Equals(session.venueId, data.venue, StringComparison.Ordinal) ||
            !string.Equals(session.nodeId, data.node, StringComparison.Ordinal))
        {
            error = "存档末会场或阶段与动作重放结果不一致";
            return null;
        }
        if (!SameResults(session.results, data.results))
        {
            error = "存档会场结果与动作重放结果不一致";
            return null;
        }
        if (!IsSafeCheckpoint(session, out error)) return null;
        return session;
    }

    /// <summary>
    /// S254 重来本会场：回到“刚进入当前会场”时的状态。与网页试玩 play.js 的 retryVenue 同语义——
    /// 只保留到最后一次 travel 进本会场为止的动作（入口会场则一条不留，从头开始），用同一套严格重放得到新会话；
    /// 本会场里的观察、活动与博弈结果全部撤销，其他会场的结果原样保留。绝不手改会话字段。
    /// </summary>
    public static WorldParkPack.Session RetryVenue(WorldParkPack.Session session, out string error)
    {
        error = "";
        if (session == null || session.pack == null) { error = "世界会话不存在，不能重来本会场"; return null; }
        if (session.actions == null || session.actions.Count > MaxActions) { error = "动作历史缺失或超过上限，不能重来本会场"; return null; }
        int cut = -1;
        for (int i = session.actions.Count - 1; i >= 0; i--)
        {
            Action a = session.actions[i];
            if (a != null && a.kind == "travel" && string.Equals(a.value, session.venueId, StringComparison.Ordinal)) { cut = i; break; }
        }
        WorldParkPack.Session fresh = WorldParkPack.Start(session.pack, out string startError);
        if (fresh == null) { error = "当前世界包无法开始：" + startError; return null; }
        for (int i = 0; i <= cut; i++)
        {
            if (!CheckActionShape(session.actions[i], out string shapeError) || !ReplayOne(fresh, session.actions[i], out shapeError))
            {
                error = "第 " + (i + 1).ToString(CultureInfo.InvariantCulture) + " 条动作无法重放：" + shapeError;
                return null;
            }
        }
        if (!string.Equals(fresh.venueId, session.venueId, StringComparison.Ordinal))
        {
            error = "重放后不在当前会场（" + fresh.venueId + "），已放弃重来";
            return null;
        }
        return fresh;
    }

    private static bool ReplayOne(WorldParkPack.Session session, Action action, out string error)
    {
        error = "";
        string message;
        if (action.kind == "continue")
        {
            if (session.Continue(out message)) return true;
            error = message;
            return false;
        }
        if (action.kind == "room")
        {
            if (session.ResolveRoom(action.value == "win", out message)) return true;
            error = message;
            return false;
        }
        if (action.kind == "explore")
        {
            if (session.ResolveExplore(action.value == "win", out message)) return true;
            error = message;
            return false;
        }
        if (action.kind == "game")
        {
            // 重放会用同一内核重新计算整局，存档里的选择被改动就会得到不同结局并被拒绝。
            if (session.ResolveGame(action.value.Split('|'), out message)) return true;
            error = message;
            return false;
        }
        if (action.kind == "travel")
        {
            if (session.Travel(action.value, out message)) return true;
            error = message;
            return false;
        }
        if (action.kind == "observe")
        {
            // ObserveClue 对重复交互会返回成功提示；检查点事件必须表示“首次实际观察”，因此显式拒绝重复。
            string key = (session.venueId ?? "") + "/" + action.value;
            if (session.found.Contains(key))
            {
                error = "同一线索不能重复记录观察动作：" + action.value;
                return false;
            }
            if (session.ObserveClue(action.value, out message)) return true;
            error = message;
            return false;
        }
        error = "未知动作 kind：" + (action.kind ?? "");
        return false;
    }

    private static bool CheckActionShape(Action action, out string error)
    {
        error = "";
        if (action == null) { error = "动作对象为空"; return false; }
        if (action.kind == "continue")
        {
            if (action.value == "") return true;
            error = "continue 的 value 必须为空";
            return false;
        }
        if (action.kind == "room" || action.kind == "explore")
        {
            if (action.value == "win" || action.value == "lose") return true;
            error = action.kind + " 的 value 只能是 win 或 lose";
            return false;
        }
        if (action.kind == "travel" || action.kind == "observe")
        {
            if (!string.IsNullOrWhiteSpace(action.value)) return true;
            error = action.kind + " 的 value 需要非空 id";
            return false;
        }
        if (action.kind == "game")
        {
            if (!string.IsNullOrEmpty(action.value) && action.value.Length <= 4096) return true;
            error = "game 的 value 需要 1–4096 字符的逐轮选择";
            return false;
        }
        error = "未知动作 kind：" + (action.kind ?? "");
        return false;
    }

    private static bool IsSafeCheckpoint(WorldParkPack.Session session, out string error)
    {
        error = "";
        WorldParkPack.Node current = session == null ? null : session.Current;
        if (current == null) { error = "当前会话没有可保存的阶段"; return false; }
        if (current.kind == "story" || current.kind == "reveal" || current.kind == "ending") return true;
        error = "当前阶段为 " + (current.kind ?? "") + "；只能在 story、reveal 或 ending 保存，room/explore 中途不会保存";
        return false;
    }

    /// <summary>保存与恢复共用的预算：字符数和 UTF-8 字节数都不得超过 MaxSaveLength，非 ASCII 内容不能绕过上限。</summary>
    private static bool ExceedsSaveBudget(string json)
    {
        if (json == null) return false;
        if (json.Length > MaxSaveLength) return true;
        return Encoding.UTF8.GetByteCount(json) > MaxSaveLength;
    }

    private static void AppendSortedResults(List<ResultEntry> target, Dictionary<string, string> source)
    {
        var keys = new List<string>();
        if (source != null)
            foreach (KeyValuePair<string, string> pair in source) keys.Add(pair.Key ?? "");
        keys.Sort(StringComparer.Ordinal);
        foreach (string key in keys)
        {
            string value;
            if (source.TryGetValue(key, out value)) target.Add(new ResultEntry { venue = key, result = value ?? "" });
        }
    }

    private static string WriteSave(SaveData data)
    {
        var sb = new StringBuilder(1024);
        sb.Append('{');
        StringMember(sb, "type", TypeTag, true);
        NumberMember(sb, "v", Version);
        StringMember(sb, "fingerprint", data.fingerprint, false);
        sb.Append(",\"actions\":[");
        for (int i = 0; i < data.actions.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('{');
            StringMember(sb, "kind", data.actions[i].kind, true);
            StringMember(sb, "value", data.actions[i].value, false);
            sb.Append('}');
        }
        sb.Append(']');
        StringMember(sb, "venue", data.venue, false);
        StringMember(sb, "node", data.node, false);
        sb.Append(",\"results\":[");
        for (int i = 0; i < data.results.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('{');
            StringMember(sb, "venue", data.results[i].venue, true);
            StringMember(sb, "result", data.results[i].result, false);
            sb.Append('}');
        }
        sb.Append("]}");
        return sb.ToString();
    }

    private static void StringMember(StringBuilder sb, string name, string value, bool first)
    {
        if (!first) sb.Append(',');
        WriteString(sb, name); sb.Append(':'); WriteString(sb, value ?? "");
    }

    private static void NumberMember(StringBuilder sb, string name, int value)
    {
        sb.Append(','); WriteString(sb, name); sb.Append(':'); sb.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void WriteString(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (char c in value ?? "")
        {
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
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }

    private static SaveData ParseSave(string json, out string error)
    {
        error = "";
        try
        {
            object value = new JsonReader(json).ReadDocument();
            Dictionary<string, object> root = RequireObject(value, "进度根节点");
            RequireOnly(root, "进度根节点", "type", "v", "fingerprint", "actions", "venue", "node", "results");
            if (!string.Equals(RequireString(root, "type", "进度根节点"), TypeTag, StringComparison.Ordinal))
                throw new ProgressFormatException("进度 type 必须是 " + TypeTag);
            if (RequireInt(root, "v", "进度根节点") != Version)
                throw new ProgressFormatException("进度 v 必须是 " + Version.ToString(CultureInfo.InvariantCulture));

            var data = new SaveData
            {
                fingerprint = RequireString(root, "fingerprint", "进度根节点"),
                venue = RequireString(root, "venue", "进度根节点"),
                node = RequireString(root, "node", "进度根节点")
            };
            List<object> actionValues = RequireArray(root, "actions", "进度根节点");
            if (actionValues.Count > MaxActions) throw new ProgressFormatException("进度动作超过 " + MaxActions + " 条上限");
            for (int i = 0; i < actionValues.Count; i++)
            {
                Dictionary<string, object> action = RequireObject(actionValues[i], "actions[" + i.ToString(CultureInfo.InvariantCulture) + "]");
                RequireOnly(action, "actions[" + i.ToString(CultureInfo.InvariantCulture) + "]", "kind", "value");
                data.actions.Add(new Action
                {
                    kind = RequireString(action, "kind", "actions[" + i.ToString(CultureInfo.InvariantCulture) + "]"),
                    value = RequireString(action, "value", "actions[" + i.ToString(CultureInfo.InvariantCulture) + "]")
                });
            }

            List<object> resultValues = RequireArray(root, "results", "进度根节点");
            if (resultValues.Count > WorldParkPack.MaxVenues) throw new ProgressFormatException("进度结果容器超过 " + WorldParkPack.MaxVenues + " 条上限");
            var seenVenues = new HashSet<string>(StringComparer.Ordinal);
            string previousVenue = null;
            for (int i = 0; i < resultValues.Count; i++)
            {
                Dictionary<string, object> result = RequireObject(resultValues[i], "results[" + i.ToString(CultureInfo.InvariantCulture) + "]");
                RequireOnly(result, "results[" + i.ToString(CultureInfo.InvariantCulture) + "]", "venue", "result");
                string venue = RequireString(result, "venue", "results[" + i.ToString(CultureInfo.InvariantCulture) + "]");
                string outcome = RequireString(result, "result", "results[" + i.ToString(CultureInfo.InvariantCulture) + "]");
                if (string.IsNullOrWhiteSpace(venue)) throw new ProgressFormatException("results[" + i.ToString(CultureInfo.InvariantCulture) + "].venue 不能为空");
                // S250：会场结果是作者定义的结局标签（clear/fail 兼容，recorded 等自定义）；any 只作链接通配，不能是结果。
                if (string.IsNullOrWhiteSpace(outcome) || outcome == "any") throw new ProgressFormatException("results[" + i.ToString(CultureInfo.InvariantCulture) + "].result 必须是非空结局标签且不能为 any");
                if (!seenVenues.Add(venue)) throw new ProgressFormatException("results 中的 venue 重复：" + venue);
                if (previousVenue != null && StringComparer.Ordinal.Compare(previousVenue, venue) >= 0)
                    throw new ProgressFormatException("results 必须按 venue 的稳定顺序排列");
                previousVenue = venue;
                data.results.Add(new ResultEntry { venue = venue, result = outcome });
            }
            return data;
        }
        catch (ProgressFormatException ex)
        {
            error = ex.Message;
            return null;
        }
    }

    private static Dictionary<string, object> RequireObject(object value, string path)
    {
        Dictionary<string, object> result = value as Dictionary<string, object>;
        if (result == null) throw new ProgressFormatException(path + " 必须是对象");
        return result;
    }

    private static List<object> RequireArray(Dictionary<string, object> source, string key, string path)
    {
        object value;
        if (!source.TryGetValue(key, out value)) throw new ProgressFormatException(path + " 缺少 " + key);
        List<object> result = value as List<object>;
        if (result == null) throw new ProgressFormatException(path + "." + key + " 必须是数组");
        return result;
    }

    private static string RequireString(Dictionary<string, object> source, string key, string path)
    {
        object value;
        if (!source.TryGetValue(key, out value)) throw new ProgressFormatException(path + " 缺少 " + key);
        string result = value as string;
        if (result == null) throw new ProgressFormatException(path + "." + key + " 必须是字符串");
        return result;
    }

    private static int RequireInt(Dictionary<string, object> source, string key, string path)
    {
        object value;
        if (!source.TryGetValue(key, out value)) throw new ProgressFormatException(path + " 缺少 " + key);
        JsonNumber number = value as JsonNumber;
        int result;
        if (number == null || !int.TryParse(number.text, NumberStyles.None, CultureInfo.InvariantCulture, out result))
            throw new ProgressFormatException(path + "." + key + " 必须是整数");
        return result;
    }

    private static void RequireOnly(Dictionary<string, object> source, string path, params string[] allowed)
    {
        var permitted = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (KeyValuePair<string, object> pair in source)
            if (!permitted.Contains(pair.Key)) throw new ProgressFormatException(path + " 含未知字段：" + pair.Key);
    }

    private static bool SameActions(List<Action> left, List<Action> right)
    {
        if (left == null || right == null || left.Count != right.Count) return false;
        for (int i = 0; i < left.Count; i++)
            if (left[i] == null || right[i] == null ||
                !string.Equals(left[i].kind, right[i].kind, StringComparison.Ordinal) ||
                !string.Equals(left[i].value, right[i].value, StringComparison.Ordinal)) return false;
        return true;
    }

    private static bool SameResults(Dictionary<string, string> actual, List<ResultEntry> expected)
    {
        if (actual == null || expected == null || actual.Count != expected.Count) return false;
        foreach (ResultEntry entry in expected)
        {
            string value;
            if (!actual.TryGetValue(entry.venue, out value) || !string.Equals(value, entry.result, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static bool Equivalent(WorldParkPack.Session left, WorldParkPack.Session right)
    {
        if (left == null || right == null ||
            !string.Equals(left.venueId, right.venueId, StringComparison.Ordinal) ||
            !string.Equals(left.nodeId, right.nodeId, StringComparison.Ordinal) ||
            !SameActions(left.actions, right.actions) ||
            left.results.Count != right.results.Count ||
            !SameStringSets(left.found, right.found) || !SameStringSets(left.revealed, right.revealed) ||
            !SameRoomResults(left.roomResults, right.roomResults) || !SameActivityResults(left.activityResults, right.activityResults)) return false;
        foreach (KeyValuePair<string, string> pair in left.results)
        {
            string value;
            if (!right.results.TryGetValue(pair.Key, out value) || !string.Equals(pair.Value, value, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static bool SameStringSets(HashSet<string> left, HashSet<string> right)
    {
        if (left == null || right == null || left.Count != right.Count) return false;
        foreach (string item in left) if (!right.Contains(item)) return false;
        return true;
    }

    private static bool SameRoomResults(List<WorldParkPack.RoomResult> left, List<WorldParkPack.RoomResult> right)
    {
        if (left == null || right == null || left.Count != right.Count) return false;
        for (int i = 0; i < left.Count; i++)
            if (left[i] == null || right[i] == null || left[i].won != right[i].won ||
                !string.Equals(left[i].venue, right[i].venue, StringComparison.Ordinal) ||
                !string.Equals(left[i].node, right[i].node, StringComparison.Ordinal)) return false;
        return true;
    }

    private static bool SameActivityResults(List<WorldParkPack.ActivityResult> left, List<WorldParkPack.ActivityResult> right)
    {
        if (left == null || right == null || left.Count != right.Count) return false;
        for (int i = 0; i < left.Count; i++)
            if (left[i] == null || right[i] == null || left[i].won != right[i].won ||
                !string.Equals(left[i].venue, right[i].venue, StringComparison.Ordinal) ||
                !string.Equals(left[i].node, right[i].node, StringComparison.Ordinal) ||
                !string.Equals(left[i].kind, right[i].kind, StringComparison.Ordinal)) return false;
        return true;
    }

    private sealed class ProgressFormatException : Exception
    {
        public ProgressFormatException(string message) : base(message) { }
    }

    private sealed class JsonNumber
    {
        public readonly string text;
        public JsonNumber(string source) { text = source; }
    }

    /// <summary>进度格式专用的严格小型 JSON 读取器；拒绝重复字段、超深嵌套和超量容器。</summary>
    private sealed class JsonReader
    {
        private const int MaxDepth = 32;
        private const int MaxContainers = MaxActions + WorldParkPack.MaxVenues + 32;
        private readonly string text;
        private int index;
        private int depth;
        private int containers;

        public JsonReader(string source) { text = source ?? ""; }

        public object ReadDocument()
        {
            SkipWhite();
            object value = ReadValue();
            SkipWhite();
            if (index != text.Length) Fail("JSON 根节点后有多余内容");
            return value;
        }

        private object ReadValue()
        {
            if (index >= text.Length) Fail("JSON 意外结束");
            char c = text[index];
            if (c == '{') return ReadObject();
            if (c == '[') return ReadArray();
            if (c == '"') return ReadString();
            if (c == '-' || IsDigit(c)) return ReadNumber();
            if (TakeWord("true")) return true;
            if (TakeWord("false")) return false;
            if (TakeWord("null")) return null;
            Fail("JSON 值不合法");
            return null;
        }

        private Dictionary<string, object> ReadObject()
        {
            EnterContainer(); index++;
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            SkipWhite();
            if (Take('}')) { LeaveContainer(); return result; }
            while (true)
            {
                SkipWhite();
                if (index >= text.Length || text[index] != '"') Fail("JSON 对象键必须是字符串");
                string key = ReadString();
                SkipWhite(); Require(':', "JSON 对象键后缺少冒号");
                SkipWhite();
                object value = ReadValue();
                if (result.ContainsKey(key)) Fail("JSON 对象字段重复：" + key);
                result.Add(key, value);
                SkipWhite();
                if (Take('}')) { LeaveContainer(); return result; }
                Require(',', "JSON 对象字段之间缺少逗号");
            }
        }

        private List<object> ReadArray()
        {
            EnterContainer(); index++;
            var result = new List<object>();
            SkipWhite();
            if (Take(']')) { LeaveContainer(); return result; }
            while (true)
            {
                if (result.Count >= MaxContainers) Fail("JSON 数组元素超过容器预算");
                SkipWhite(); result.Add(ReadValue()); SkipWhite();
                if (Take(']')) { LeaveContainer(); return result; }
                Require(',', "JSON 数组元素之间缺少逗号");
            }
        }

        private string ReadString()
        {
            Require('"', "JSON 字符串缺少起始引号");
            var result = new StringBuilder();
            while (index < text.Length)
            {
                char c = text[index++];
                if (c == '"') return result.ToString();
                if (c < 0x20) Fail("JSON 字符串含未转义控制字符");
                if (c != '\\') { result.Append(c); continue; }
                if (index >= text.Length) Fail("JSON 转义不完整");
                char escaped = text[index++];
                switch (escaped)
                {
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    case '/': result.Append('/'); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'u': result.Append(ReadHex()); break;
                    default: Fail("JSON 转义不合法"); break;
                }
            }
            Fail("JSON 字符串没有结束引号");
            return "";
        }

        private char ReadHex()
        {
            if (index + 4 > text.Length) Fail("JSON unicode 转义不完整");
            int value = 0;
            for (int i = 0; i < 4; i++)
            {
                int digit = Hex(text[index++]);
                if (digit < 0) Fail("JSON unicode 转义不是十六进制");
                value = value * 16 + digit;
            }
            return (char)value;
        }

        private JsonNumber ReadNumber()
        {
            int start = index;
            if (text[index] == '-') index++;
            if (index >= text.Length) Fail("JSON 数字不完整");
            if (text[index] == '0') index++;
            else
            {
                if (!IsDigit19(text[index])) Fail("JSON 数字不合法");
                while (index < text.Length && IsDigit(text[index])) index++;
            }
            if (index < text.Length && text[index] == '.')
            {
                index++; int fractional = index;
                while (index < text.Length && IsDigit(text[index])) index++;
                if (fractional == index) Fail("JSON 小数不完整");
            }
            if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
            {
                index++;
                if (index < text.Length && (text[index] == '+' || text[index] == '-')) index++;
                int exponent = index;
                while (index < text.Length && IsDigit(text[index])) index++;
                if (exponent == index) Fail("JSON 指数不完整");
            }
            return new JsonNumber(text.Substring(start, index - start));
        }

        private bool TakeWord(string word)
        {
            if (index + word.Length > text.Length || string.CompareOrdinal(text, index, word, 0, word.Length) != 0) return false;
            index += word.Length;
            return true;
        }

        private bool Take(char expected)
        {
            if (index < text.Length && text[index] == expected) { index++; return true; }
            return false;
        }

        private void Require(char expected, string message)
        {
            if (!Take(expected)) Fail(message);
        }

        private void EnterContainer()
        {
            depth++; containers++;
            if (depth > MaxDepth) Fail("JSON 嵌套超过 " + MaxDepth + " 层上限");
            if (containers > MaxContainers) Fail("JSON 容器超过 " + MaxContainers + " 个上限");
        }

        private void LeaveContainer() { depth--; }

        private void SkipWhite()
        {
            while (index < text.Length)
            {
                char c = text[index];
                if (c != ' ' && c != '\t' && c != '\r' && c != '\n') break;
                index++;
            }
        }

        private static bool IsDigit(char c) { return c >= '0' && c <= '9'; }
        private static bool IsDigit19(char c) { return c >= '1' && c <= '9'; }
        private static int Hex(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        private static void Fail(string message) { throw new ProgressFormatException(message); }
    }
}
