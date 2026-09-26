using System.Collections.Generic;

/// <summary>
/// S187：受控随机布局（纯逻辑，可测试）。在固定手工房间上，按种子挑选"变体槽位"：
///   模板里用小写占位字母标记可变位置，每个占位只会变成它允许的几种元素之一（或留空）。
///   例：'1' → 箱子或草丛；'2' → 草丛或空；'3' → 火或空。
/// 规则：
///   - 必须/唯一元素（M T G o、墙、地面、桥、炮）永远不动，所以房间合法性与可达性（宪法 H1）由测试对每个种子逐一验证；
///   - 随机只改"藏身处在哪"和"某把火在不在"，让每局要重新观察、重新布局，而不是一张死图背板；
///   - 种子写入试玩记录，任何一局都能复现。
/// </summary>
public static class Step1Layout
{
    /// <summary>占位字符 → 可选结果（'.' = 留空）。</summary>
    public static readonly Dictionary<char, string> Slots = new Dictionary<char, string>
    {
        { '1', "cb" },   // 地面层藏身处：箱子 或 草丛
        { '2', "b." },   // 可选草丛
        { '3', "~." },   // 可选火
    };

    /// <summary>
    /// 唯一的随机选择算法：按顺序给每个槽位挑一个选项下标。Resolve（测试/验证用）与运行时 Step1LayoutVariants.Apply 共用，
    /// 保证"测试验证过的组合 = 游戏里出现的组合"。
    /// </summary>
    public static int[] Pick(int seed, IList<string> optionsInOrder)
    {
        var rng = new System.Random(seed);
        var picks = new int[optionsInOrder.Count];
        for (int i = 0; i < picks.Length; i++)
            picks[i] = optionsInOrder[i].Length > 0 ? rng.Next(optionsInOrder[i].Length) : 0;
        return picks;
    }

    /// <summary>模板中所有槽位的选项（行优先，从上到下、从左到右）。</summary>
    public static List<string> SlotOptions(string[] template)
    {
        var list = new List<string>();
        foreach (var row in template) foreach (char ch in row) if (Slots.TryGetValue(ch, out string o)) list.Add(o);
        return list;
    }

    public static string[] Resolve(string[] template, int seed)
    {
        int[] picks = Pick(seed, SlotOptions(template));
        int k = 0;
        var result = new string[template.Length];
        for (int r = 0; r < template.Length; r++)
        {
            var row = template[r].ToCharArray();
            for (int c = 0; c < row.Length; c++)
                if (Slots.TryGetValue(row[c], out string options)) row[c] = options[picks[k++]];
            result[r] = new string(row);
        }
        return result;
    }

    public static bool HasSlots(string[] template)
    {
        foreach (var row in template) foreach (char ch in row) if (Slots.ContainsKey(ch)) return true;
        return false;
    }
}
