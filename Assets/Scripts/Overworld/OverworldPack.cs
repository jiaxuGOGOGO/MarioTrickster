using System.Collections.Generic;
using System.Linq;

/// <summary>
/// S210：关卡包里的小镇大地图（纯逻辑）。网页工作室导出的关卡包里：
///   "overworlds":[ {kind:"overworld", name, goal, grid, doors:[{n,time,room}], notes} ]；也兼容混在 "levels" 里、带 kind:"overworld" 的条目；
///   还能直接导入一张小镇 .txt（# Overworld: 开头）。
/// </summary>
public static class OverworldPack
{
    public static List<OverworldMap.Map> Parse(string text)
    {
        var list = new List<OverworldMap.Map>();
        if (string.IsNullOrWhiteSpace(text)) return list;
        if (OverworldMap.IsOverworldText(text)) { var m = OverworldMap.Parse(text); if (m.W > 0) list.Add(m); return list; }
        var root = MiniJson.Parse(text, out _) as Dictionary<string, object>;
        if (root == null) return list;
        void Take(object o)
        {
            if (o is Dictionary<string, object> d && d.TryGetValue("grid", out var g) && g is List<object> gl && gl.Count > 0)
            {
                var m = OverworldMap.FromJson(d);
                if (m.name.Length == 0) m.name = "小镇" + (list.Count + 1);
                list.Add(m);
            }
        }
        if (root.TryGetValue("overworlds", out var ow) && ow is List<object> owl) foreach (var o in owl) Take(o);
        if (root.TryGetValue("levels", out var lv) && lv is List<object> ll)
            foreach (var o in ll) if (o is Dictionary<string, object> d && d.TryGetValue("kind", out var k) && (k as string) == "overworld") Take(o);
        if (root.TryGetValue("kind", out var rk) && (rk as string) == "overworld") Take(root);
        return list;
    }

    /// <summary>S210：内置样板小镇。4 户人家，门 1 连默认恶作剧房间，2–4 连工坊样板房。</summary>
    public const string SampleName = "星露小镇";
    public static readonly string SampleText = string.Join("\n", new[]
    {
        "# Overworld: 星露小镇",
        "# Goal: 马里奥今天要去 4 户人家\"借宝贝\"。抄近路先到门口按 E 埋伏，躲在高草和木箱后面别被他看见。",
        "# Source: S210 sample",
        "# Door: 1 | 08:00 | 默认恶作剧房间",
        "# Door: 2 | 10:30 | 两层监狱",
        "# Door: 3 | 13:30 | 诱捕走廊",
        "# Door: 4 | 16:30 | 地下监狱·四层",
        "# Note: (22,14) 大桥：他必走这里，香蕉皮就在桥两头",
        "# Note: (21,19) 泥巴近道：他嫌慢不走，你可以抄",
        "# Note: (15,6) 菜园高草：绝佳藏身处",
        "tttttttttttttttttttttttttttttttttttttttttttt",
        "t....................ww....................t",
        "t............WWWWWW..ww....WWWWWWW.........t",
        "t..WWWWWW....WWWWWW..ww....WWWWWWW......?..t",
        "t..WWWWWW....WWWWWW..ww.tt.WWWWWWW.........t",
        "t..WWWWWW.tt.WWWWWW..ww....WWWWWWW....t....t",
        "t..WWWWWW....WWWWWW..ww....WWWWWWW.........t",
        "t..WWWWWW......1.c...ww.....c.2......wwww..t",
        "t....M.........=...gggggg.....=......wwww..t",
        "t....=......\"..=.\"\"..ww...\"\"..=...\"\".......t",
        "t....=......\"..=.....ww.......=n.......t...t",
        "t....=.........=.....ww.......=............t",
        "t....=.........=.....ww.......=...i........t",
        "t.========n===============n===============.t",
        "t...i.=.........i....ww......i...=.........t",
        "t.....=....t.........ww.....t....=.........t",
        "t.t...3.c............ww..........4..c...t..t",
        "t...WWWWWW...........ww.......WWWWWWWW.....t",
        "t...WWWWWW...ffffff..ww..tt...WWWWWWWW.....t",
        "t...WWWWWW...f\"\"\"\"f..ww.......WWWWWWWW.....t",
        "t...WWWWWW...f\"\"\"\"f..ww.......WWWWWWWW.....t",
        "t...WWWWWW...f\"\"\"\"...ww.......WWWWWWWW.....t",
        "t............f\"\"\"\"f..ww....................t",
        "t..\".........f\"\"\"\"f..ww.?..............\"...t",
        "t........\"\"..ffffff..ww.....\"\"........\"....t",
        "t.?..................ww.................T..t",
        "t....................ww....................t",
        "tttttttttttttttttttttttttttttttttttttttttttt",
    }) + "\n";

    /// <summary>S218：内置样板"星露大镇"（72×40）：星露小镇往东、往北扩出去，放了 2 门巨炮、2 块滚石、2 座水塔，最长 3 连锁。</summary>
    public const string BigSampleName = "星露大镇";
    public static readonly string BigSampleText = string.Join("\n", new[]
    {
        "# Overworld: 星露大镇",
        "# Goal: 小镇变大了：大路北边有巨炮、菜园南边有靶心和滚石、东边炮台山。L 发动大机关，冲击会连锁；他吃过一次亏就会躲。",
        "# Source: S218 sample",
        "# Door: 1 | 08:00 | 默认恶作剧房间",
        "# Door: 2 | 10:30 | 两层监狱",
        "# Door: 3 | 13:30 | 诱捕走廊",
        "# Door: 4 | 16:30 | 地下监狱·四层",
        "# Note: (22,14) 大桥：他必走这里，香蕉皮就在桥两头",
        "# Note: (26,16) 巨炮：炮口罩住大路 3 格，他走到桥东按 L——轰进南边菜园的靶心",
        "# Note: (27,3) 连锁：炮弹落地震响滚石 → 一路撞碎木箱滚到水塔 → 水塔淹掉那片（3 连）",
        "# Note: (32,17) 水塔：门 2 路口，淹成泥地他走得慢，旁边香蕉皮自己变滑",
        "# Note: (63,28) 炮台山：你自己站进炮口 = 飞到东路口，顺便震响滚石沿大路一路滚过去",
        "tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt",
        "t......................................................................t",
        "t......................................................................t",
        "t...........................................................t..........t",
        "t..............................................t.......................t",
        "t......................................................................t",
        "t...................................................................t..t",
        "t......................................................................t",
        "t...................................................t..................t",
        "t............................................tt........................t",
        "t...............................................................tt.....t",
        "t..............................................................K.......t",
        "t......................................................\"\"\".............t",
        "t....................ww...................======================.......t",
        "t............WWWWWW..ww....WWWWWWW........=.................\"\".........t",
        "t..WWWWWW....WWWWWW..ww....WWWWWWW......?.=.......................t....t",
        "t..WWWWWW....WWWWWW..ww.tt.WWWWWWW........=............................t",
        "t..WWWWWW.tt.WWWWWW..ww....WWWWWWW....t...=.....t......................t",
        "t..WWWWWW....WWWWWW..ww....WWWWWWW........=............................t",
        "t..WWWWWW......1.c...ww.....c.2......wwww.=.............t..............t",
        "t....M.........=...gggggg.....=......wwww.=............................t",
        "t....=......\"..=.\"\"..ww...\"\"..=...\"\"......=.\"\".........................t",
        "t....=......\"..=.....ww.......=nU......t..=............................t",
        "t....=.........=.....ww...K...=...........=............................t",
        "t....=.........=.....ww.......=...i.......=....................X.......t",
        "t.========n===============n===================================O........t",
        "t...i.=.........i....ww......i...=.....................................t",
        "t.....=....t.........ww.....t....=............................tt.......t",
        "t.t...3.c............ww..........4..c...t..............................t",
        "t...WWWWWW...........ww.......WWWWWWWW.................................t",
        "t...WWWWWW...ffffff..ww..tt...WWWWWWWW.................................t",
        "t...WWWWWW...f\"\"\"\"f..ww.......WWWWWWWW..............\"\"\"................t",
        "t...WWWWWW...f\"\"\"\"f..ww.......WWWWWWWW.................................t",
        "t...WWWWWW...f\"\"\"\"...ww.......WWWWWWWW.................................t",
        "t............f\"\"\"\"f..ww................................................t",
        "t..\".........f\"\"\"\"f..ww.?..............\"...............................t",
        "t........\"\"..ffffff..ww...XO\"\"....cc..\"...........U....................t",
        "t.?..................ww.................T..............................t",
        "t....................ww................................................t",
        "tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt",
    }) + "\n";
}
