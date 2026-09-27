using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// S191：菜单 MarioTrickster → Level Design → Check Theme Art (美术检查)。
/// 选中一个 LevelThemeProfile（或用 Test Console 当前主题），逐个插槽检查拖进来的 Sprite 会不会"放进去就对"：
///   - PPU 是否为项目标准 32（PhysicsMetrics.STANDARD_PPU_32）；
///   - 平铺类（地面/墙/台面/桥）Mesh Type 是否 Full Rect（否则平铺会错）；
///   - 等比放入类的宽高比与显示框差多少（差太多会留大片空白，提示美术按建议尺寸画）；
///   - 哪些插槽还空着（空着 = 保留白盒，不会报错）。
/// 只读检查，不改任何资产。规则全部来自 ElementCatalog + Registry，新增元素自动纳入。
/// </summary>
public static class ArtReadinessCheck
{
    public const float AspectTolerance = 0.35f; // 宽高比偏差 35% 以内视为合适

    public struct Item { public string key, zh, status; public bool ok, empty; }

    [MenuItem("MarioTrickster/Level Design/Check Theme Art (美术检查)", false, 201)]
    public static void Menu()
    {
        var theme = Selection.activeObject as LevelThemeProfile;
        if (theme == null)
        {
            EditorUtility.DisplayDialog("美术检查", "请先在 Project 窗口选中一个 LevelThemeProfile 主题资产。", "OK");
            return;
        }
        var items = Check(theme);
        var sb = new StringBuilder();
        int bad = 0, empty = 0;
        foreach (var i in items) { if (i.empty) empty++; else if (!i.ok) bad++; }
        sb.AppendLine($"主题 {theme.name}：{bad} 个要改，{empty} 个空插槽（保留白盒）\n");
        foreach (var i in items) if (!i.empty) sb.AppendLine((i.ok ? "✓ " : "✗ ") + i.zh + " (" + i.key + ")：" + i.status);
        Debug.Log("[美术检查]\n" + sb);
        EditorUtility.DisplayDialog("美术检查", sb.ToString(), "OK");
    }

    public static List<Item> Check(LevelThemeProfile theme)
    {
        var list = new List<Item>();
        void One(string key, Sprite sprite)
        {
            var info = ElementCatalog.ByKey(key);
            string zh = info != null ? info.zh : key;
            if (sprite == null) { list.Add(new Item { key = key, zh = zh, empty = true, ok = true, status = "空（保留白盒）" }); return; }
            list.Add(Evaluate(key, zh, sprite, info));
        }
        One("Ground", theme.groundSprite); One("Platform", theme.platformSprite); One("Wall", theme.wallSprite);
        foreach (var m in theme.elementSprites) if (m != null) One(m.elementKey, m.sprite);
        return list;
    }

    private static Item Evaluate(string key, string zh, Sprite sprite, ElementCatalog.Info info)
    {
        var issues = new List<string>();
        float ppu = sprite.pixelsPerUnit;
        if (Mathf.Abs(ppu - PhysicsMetrics.STANDARD_PPU_32) > 0.01f) issues.Add($"PPU={ppu:0}，应为 {PhysicsMetrics.STANDARD_PPU_32}（用 Asset Import Pipeline 导入会自动设好）");
        var fit = info != null ? info.fit : ElementCatalog.ArtFit.Fit;
        string path = AssetDatabase.GetAssetPath(sprite);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (fit == ElementCatalog.ArtFit.Tile && ti != null)
        {
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect) issues.Add("平铺类需要 Mesh Type = Full Rect");
        }
        if (fit == ElementCatalog.ArtFit.Fit && info != null)
        {
            var e = AsciiElementRegistry.GetDefault().GetEntry(info.ch);
            Vector2 box = e != null && e.visualScale != Vector2.zero ? e.visualScale : Vector2.one;
            float dev = AspectDeviation(sprite.bounds.size, box);
            if (dev > AspectTolerance) issues.Add($"宽高比与显示框差 {dev:P0}，会留较多空白；建议 {ElementCatalog.SuggestedPixels(info.ch)} 像素");
        }
        return new Item { key = key, zh = zh, ok = issues.Count == 0, status = issues.Count == 0 ? "放进去就对" : string.Join("；", issues) };
    }

    /// <summary>纯计算：图与显示框的宽高比偏差（0 = 完全一致）。</summary>
    public static float AspectDeviation(Vector2 sprite, Vector2 box)
    {
        if (sprite.x <= 0f || sprite.y <= 0f || box.x <= 0f || box.y <= 0f) return 1f;
        float a = sprite.x / sprite.y, b = box.x / box.y;
        return Mathf.Abs(a - b) / b;
    }
}
