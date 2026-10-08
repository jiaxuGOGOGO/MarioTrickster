using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// S188：菜单 MarioTrickster → 美术 Art → 导出元素图例。
/// 从 ElementCatalog（人看的说明）+ AsciiElementRegistry（物理数据）生成一份中文图例
/// docs/ELEMENT_LEGEND.md：每个字符是什么、干什么、摆在哪、碰撞/视线、美术换图键和建议尺寸。
/// 关卡设计和美术都照这张表，不会摆错、不会拖错图。
/// </summary>
public static class ElementLegendExporter
{
    public const string OutputPath = "docs/ELEMENT_LEGEND.md";

    [MenuItem("MarioTrickster/美术 Art/导出元素图例 Element Legend", false, 200)]
    public static void ExportMenu()
    {
        string path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, Build(), new UTF8Encoding(false));
        Debug.Log("[Legend] Written " + path);
        EditorUtility.RevealInFinder(path);
    }

    public static string Build()
    {
        var reg = AsciiElementRegistry.GetDefault();
        var sb = new StringBuilder();
        sb.AppendLine("# 关卡元素图例（自动生成，勿手改）");
        sb.AppendLine();
        sb.AppendLine("> 生成来源：`ElementCatalog`（说明）+ `AsciiElementRegistry`（物理）。菜单 `MarioTrickster → 美术 Art → 导出元素图例` 重新生成。");
        sb.AppendLine("> **美术换图**：在 LevelThemeProfile 的 elementSprites 里按\"主题键\"拖 Sprite；地面/平台/墙用 Profile 顶部的专用插槽。空插槽 = 保留白盒。");
        sb.AppendLine("> **不用手调尺寸**：换图后按\"贴法\"自动适配（平铺 = 按原尺寸重复；等比放入 = 不变形放进格子、站地上的底边贴地；拉伸 = 填满）。碰撞体永远不动。");
        sb.AppendLine("> 按\"建议像素\"画、PPU=32 导入（Asset Import Pipeline 自动设置）就是一次到位；菜单 `美术 Art → 主题美术检查` 可检查整套主题。");
        sb.AppendLine("> 游戏里按 **V** 可以在每个元素头上看到名字。");
        sb.AppendLine();
        foreach (ElementCatalog.Role role in System.Enum.GetValues(typeof(ElementCatalog.Role)))
        {
            var rows = ElementCatalog.All.Where(i => i.role == role && i.ch != '.' && i.ch != ' ').ToList();
            if (rows.Count == 0) continue;
            sb.AppendLine("## " + ElementCatalog.RoleName(role));
            sb.AppendLine();
            sb.AppendLine("| 字符 | 名称 | 干什么 | 摆在哪 | 挡路 / 挡视线 | 主题键（换图） | 贴法 | 建议像素（PPU 32） | 第1步 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var i in rows)
            {
                var e = reg.GetEntry(i.ch);
                string block = e == null ? "?" : (e.isSolid && !e.isTrigger ? "挡路" : "可穿过") + " / " + SightOf(e);
                string size = e == null ? "?" : $"{Fmt(e.visualScale.x)}×{Fmt(e.visualScale.y)} 格";
                sb.AppendLine($"| `{i.ch}` | {i.zh} {i.en} | {i.what} | {i.place} | {block} | `{i.themeKey}` | {FitName(i.fit)} | {ElementCatalog.SuggestedPixels(i.ch)} | {(i.step1 ? "✓" : "—")} |");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string FitName(ElementCatalog.ArtFit f)
    {
        switch (f)
        {
            case ElementCatalog.ArtFit.Tile: return "平铺";
            case ElementCatalog.ArtFit.Fit: return "等比放入";
            case ElementCatalog.ArtFit.Stretch: return "拉伸";
            default: return "无图";
        }
    }

    private static string SightOf(AsciiElementEntry e)
    {
        bool sightBlocker = e.componentTypeNames != null && e.componentTypeNames.Contains("SightBlocker");
        if (sightBlocker) return "挡视线";
        if (e.elementName == "OneWayPlatform") return "不挡视线";
        return e.isSolid && !e.isTrigger ? "挡视线" : "不挡视线";
    }

    private static string Fmt(float v) => (v == 0f ? 1f : v).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
