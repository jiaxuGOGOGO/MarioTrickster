using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// S206：关卡库——你搭的每一关都存成 Assets/Levels/Library/名字.txt（进 git，换电脑/换账号都在）。
/// 来源：① 工坊"存入关卡库"（当前画布 + 名字）；② 导入网页设计台导出的关卡包（.levelpack.json，一次多关）。
/// 关卡里用到还没实现的新机制 → 先当空气并记在 "# Pending:" 行；机制实现后重新导入同一个关卡包即可还原（同名覆盖）。
/// </summary>
public static class LevelLibrary
{
    public const string Folder = "Assets/Levels/Library";

    public static string PathFor(string name) => Folder + "/" + LevelPack.SafeFileName(name) + ".txt";

    public static List<(string name, string path, List<char> pending)> List()
    {
        var list = new List<(string, string, List<char>)>();
        if (!Directory.Exists(Folder)) return list;
        foreach (var f in Directory.GetFiles(Folder, "*.txt").OrderBy(p => p, StringComparer.Ordinal))
        {
            string text = File.ReadAllText(f);
            list.Add((LevelPack.NameOf(text) ?? Path.GetFileNameWithoutExtension(f), f.Replace('\\', '/'), LevelPack.PendingOf(text)));
        }
        return list;
    }

    public static string Save(LevelPack.Level level)
    {
        Directory.CreateDirectory(Folder);
        string path = PathFor(level.name);
        File.WriteAllText(path, LevelPack.ToText(level));
        AssetDatabase.ImportAsset(path);
        return path;
    }

    /// <summary>导入关卡包：返回（导入几关，报告文字）。</summary>
    public static (int count, string report) ImportPack(string json)
    {
        var reg = AsciiElementRegistry.GetDefault();
        var levels = LevelPack.Parse(json, c => reg.GetEntry(c) != null || Step1Layout.Slots.ContainsKey(c), out string err);
        if (levels == null) return (0, "导入失败：" + err);
        var lines = new List<string>();
        foreach (var l in levels)
        {
            string path = Save(l);
            var check = LevelWorkshopModel.Check(l.rows, true, reg.IsSolid);
            string pending = l.pending.Count > 0 ? "  ⏳ 等新机制：" + string.Join(" ", l.pending.Keys.Select(k => $"{k}={l.pendingNames[k]}")) : "";
            lines.Add($"{(check.Playable ? "✓" : "✗")} {l.name} → {path}{pending}");
        }
        // S210：关卡包里的小镇大地图 → Assets/Levels/Overworld
        foreach (var m in OverworldPack.Parse(json))
        {
            string p = OverworldBuilder.Save(m);
            var r = OverworldMap.Check(m, OverworldBuilder.RulesFromTuning(), OverworldBuilder.RoomProblem);
            lines.Add($"{(r.Playable ? "✓" : "✗")} 小镇 {m.name} → {p}（在小镇工坊打开）");
        }
        AssetDatabase.Refresh();
        return (levels.Count, $"导入了 {levels.Count} 关到关卡库：\n" + string.Join("\n", lines) + "\n\n✓ = 检查通过，✗ = 打开后看红格；⏳ = 用到还没实现的新机制（先当空气），把设计单交给 AI 实现后再导入同一个关卡包即可。");
    }
}
