using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// S206：极小的 JSON 读取器（不依赖 Unity，沙盒可测）。返回 Dictionary&lt;string,object&gt; / List&lt;object&gt; / string / double / bool / null。
/// 只用来读网页"关卡设计台"导出的关卡包；格式坏了返回 null 并给出 error。
/// </summary>
public static class MiniJson
{
    public static object Parse(string json, out string error)
    {
        error = "";
        if (string.IsNullOrEmpty(json)) { error = "空文件"; return null; }
        int i = 0;
        try
        {
            if (json.Length > 0 && json[0] == '\uFEFF') i = 1;
            var v = Value(json, ref i);
            Ws(json, ref i);
            if (i != json.Length) { error = $"第 {i} 个字符后面还有多余内容"; return null; }
            return v;
        }
        catch (System.FormatException e) { error = e.Message; return null; }
    }

    private static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
    private static System.FormatException Bad(int i, string what) => new System.FormatException($"JSON 格式错误（第 {i} 个字符）：{what}");

    private static object Value(string s, ref int i)
    {
        Ws(s, ref i);
        if (i >= s.Length) throw Bad(i, "意外结束");
        char c = s[i];
        if (c == '{') return Obj(s, ref i);
        if (c == '[') return Arr(s, ref i);
        if (c == '"') return Str(s, ref i);
        if (c == 't' && Lit(s, ref i, "true")) return true;
        if (c == 'f' && Lit(s, ref i, "false")) return false;
        if (c == 'n' && Lit(s, ref i, "null")) return null;
        return Num(s, ref i);
    }

    private static bool Lit(string s, ref int i, string word)
    {
        if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw Bad(i, "认不出的值");
        i += word.Length; return true;
    }

    private static Dictionary<string, object> Obj(string s, ref int i)
    {
        var d = new Dictionary<string, object>(); i++;
        Ws(s, ref i);
        if (i < s.Length && s[i] == '}') { i++; return d; }
        while (true)
        {
            Ws(s, ref i);
            if (i >= s.Length || s[i] != '"') throw Bad(i, "对象的键要用双引号");
            string k = Str(s, ref i);
            Ws(s, ref i);
            if (i >= s.Length || s[i] != ':') throw Bad(i, "缺少冒号");
            i++;
            d[k] = Value(s, ref i);
            Ws(s, ref i);
            if (i < s.Length && s[i] == ',') { i++; continue; }
            if (i < s.Length && s[i] == '}') { i++; return d; }
            throw Bad(i, "缺少逗号或 }");
        }
    }

    private static List<object> Arr(string s, ref int i)
    {
        var l = new List<object>(); i++;
        Ws(s, ref i);
        if (i < s.Length && s[i] == ']') { i++; return l; }
        while (true)
        {
            l.Add(Value(s, ref i));
            Ws(s, ref i);
            if (i < s.Length && s[i] == ',') { i++; continue; }
            if (i < s.Length && s[i] == ']') { i++; return l; }
            throw Bad(i, "缺少逗号或 ]");
        }
    }

    private static string Str(string s, ref int i)
    {
        var sb = new StringBuilder(); i++;
        while (i < s.Length)
        {
            char c = s[i++];
            if (c == '"') return sb.ToString();
            if (c != '\\') { sb.Append(c); continue; }
            if (i >= s.Length) break;
            char e = s[i++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u':
                    if (i + 4 > s.Length) throw Bad(i, "\\u 不完整");
                    sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber)); i += 4; break;
                default: sb.Append(e); break;
            }
        }
        throw Bad(i, "字符串没有结束的引号");
    }

    private static double Num(string s, ref int i)
    {
        int start = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        if (i == start) throw Bad(i, "认不出的值");
        if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) throw Bad(start, "数字格式不对");
        return v;
    }
}
