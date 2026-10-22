using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// S206：关卡包（网页"关卡设计台"导出的多关卡文件）↔ Unity 关卡库（Assets/Levels/Library/*.txt）。纯逻辑，沙盒可测。
///
/// 关卡包格式（.levelpack.json）：{ "type":"mariotrickster-levelpack", "v":1, "rules":"S206",
///   "levels":[ { "id", "name", "goal", "grid":["WWW…",…], "notes":[{"x","y","text"}] } … ],
///   "proposals":[ { "c","key","zh","effect",… } ], "cuts":{ "C":"原因" } }
/// 也兼容单关卡的 .studio.json（顶层就有 grid）。
///
/// 关卡库文件（每关一个 .txt，工坊直接能打开）：
///   ASCII 网格 + 元数据行（工坊保留不当网格）：
///   # Name: 名字      # Goal: 设计意图      # Note: (x,y) 批注      # Source: 关卡包 id
///   # Pending: A=磁铁陷阱 (5,3) (6,3)   ← 还没实现的新机制：先当空气，实现后重新导入关卡包即可还原
/// </summary>
public static class LevelPack
{
    public sealed class Note { public int x, y; public string text; }
    public sealed class Level
    {
        public string id = "", name = "", goal = "";
        public string[] rows = new string[0];
        public readonly List<Note> notes = new List<Note>();
        /// <summary>还没登记的字符 → 在哪些格（x,y）。网格里这些格已换成 '.'。</summary>
        public readonly Dictionary<char, List<(int x, int y)>> pending = new Dictionary<char, List<(int, int)>>();
        public readonly Dictionary<char, string> pendingNames = new Dictionary<char, string>();
    }

    public const string TypeTag = "mariotrickster-levelpack";

    /// <summary>读关卡包 / 单关卡 studio.json。isRegistered(ch) 判断字符是否已是正式元素。</summary>
    public static List<Level> Parse(string json, System.Func<char, bool> isRegistered, out string error)
    {
        var result = new List<Level>();
        var root = MiniJson.Parse(json, out error) as Dictionary<string, object>;
        if (root == null) { if (error.Length == 0) error = "不是关卡包（顶层不是 { }）"; return null; }
        var names = new Dictionary<char, string>();
        if (root.TryGetValue("proposals", out var ps) && ps is List<object> pl)
            foreach (var o in pl) if (o is Dictionary<string, object> p && Str(p, "c").Length == 1) names[Str(p, "c")[0]] = Str(p, "zh");
        IEnumerable<object> levels;
        if (root.TryGetValue("levels", out var lv) && lv is List<object> ll) levels = ll;
        else if (root.ContainsKey("grid")) levels = new object[] { root };
        else { error = "文件里没有关卡（找不到 levels 或 grid）"; return null; }
        int n = 0;
        foreach (var o in levels)
        {
            n++;
            if (!(o is Dictionary<string, object> d) || !(d.TryGetValue("grid", out var g) && g is List<object> gl) || gl.Count == 0) continue;
            if (Str(d, "kind") == "overworld") continue; // S210：小镇大地图由 OverworldPack 读取
            var lvl = new Level { id = Str(d, "id"), name = Str(d, "name"), goal = Str(d, "goal") };
            if (lvl.name.Length == 0) lvl.name = "关卡" + n;
            var rows = gl.Select(x => (x as string ?? "").Replace(' ', '.')).ToArray();
            int h = rows.Length;
            for (int r = 0; r < h; r++)
            {
                var ch = rows[r].ToCharArray();
                for (int x = 0; x < ch.Length; x++)
                {
                    char c = ch[x];
                    if (c == '.' || isRegistered(c)) continue;
                    if (!lvl.pending.TryGetValue(c, out var list)) lvl.pending[c] = list = new List<(int, int)>();
                    list.Add((x, h - 1 - r));
                    lvl.pendingNames[c] = names.TryGetValue(c, out var nm) ? nm : "未知";
                    ch[x] = '.';
                }
                rows[r] = new string(ch);
            }
            lvl.rows = rows;
            if (d.TryGetValue("notes", out var ns) && ns is List<object> nl)
                foreach (var no in nl)
                    if (no is Dictionary<string, object> nd)
                        lvl.notes.Add(new Note { x = (int)Num(nd, "x"), y = (int)Num(nd, "y"), text = Str(nd, "text") });
            result.Add(lvl);
        }
        if (result.Count == 0) { error = OverworldPack.Parse(json).Count > 0 ? "" : "关卡包里没有能用的关卡"; return error.Length == 0 ? result : null; }
        return result;
    }

    /// <summary>关卡库 .txt 文本（网格 + 元数据行）。</summary>
    public static string ToText(Level l)
    {
        var sb = new StringBuilder();
        foreach (var r in l.rows) sb.AppendLine(r);
        sb.Append("# Name: ").AppendLine(OneLine(l.name));
        if (l.goal.Length > 0) sb.Append("# Goal: ").AppendLine(OneLine(l.goal));
        if (l.id.Length > 0) sb.Append("# Source: ").AppendLine(OneLine(l.id));
        foreach (var n in l.notes) sb.Append("# Note: (").Append(n.x).Append(',').Append(n.y).Append(") ").AppendLine(OneLine(n.text));
        foreach (var kv in l.pending)
        {
            sb.Append("# Pending: ").Append(kv.Key).Append('=').Append(OneLine(l.pendingNames.TryGetValue(kv.Key, out var nm) ? nm : "未知"));
            foreach (var (x, y) in kv.Value) sb.Append(" (").Append(x).Append(',').Append(y).Append(')');
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>从关卡库 .txt 读名字（没有 # Name 行 → null）。</summary>
    public static string NameOf(string text)
    {
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
            if (raw.StartsWith("# Name: ")) return raw.Substring(8).Trim();
        return null;
    }

    /// <summary>从关卡库 .txt 读"还在等机制实现"的字符。</summary>
    public static List<char> PendingOf(string text)
    {
        var l = new List<char>();
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
            if (raw.StartsWith("# Pending: ") && raw.Length > 11) l.Add(raw[11]);
        return l;
    }

    /// <summary>安全文件名（去掉 Windows 不允许的字符，最长 60）。</summary>
    public static string SafeFileName(string name)
    {
        var sb = new StringBuilder();
        foreach (char c in (name ?? "").Trim()) sb.Append("\\/:*?\"<>|\n\r\t".IndexOf(c) >= 0 ? '_' : c);
        var s = sb.ToString().Trim('.', ' ');
        if (s.Length == 0) s = "未命名关卡";
        return s.Length > 60 ? s.Substring(0, 60) : s;
    }

    private static string OneLine(string s) => (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
    private static string Str(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is string s ? s : "";
    private static double Num(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is double n ? n : 0;
}
