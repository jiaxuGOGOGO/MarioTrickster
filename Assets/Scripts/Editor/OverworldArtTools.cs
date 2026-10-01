using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// S220：小镇像素图标的"换图管线"。
/// 1) 菜单 导出像素图标模板 → 把内置的 16×16 图标存成 PNG 到 Assets/Resources/OverworldArt/（文件名 = 图标名）。
/// 2) 美术用 Aseprite / Procreate / Photoshop 直接改这些 PNG（保持同名；尺寸 16×16 或 32×32 都行）。
/// 3) 游戏和小镇工坊自动用你的图（同名覆盖），删掉 PNG 就回到内置图标。
/// 导入设置自动设成像素风：点采样、不压缩、不生成 mipmap。
/// </summary>
public sealed class OverworldArtImporter : AssetPostprocessor
{
    public const string Folder = "Assets/Resources/OverworldArt";
    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').StartsWith(Folder + "/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.isReadable = true;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.wrapMode = TextureWrapMode.Clamp;
    }
}

public static class OverworldArtTools
{
    [MenuItem("MarioTrickster/Overworld/导出像素图标模板（给美术换图）", false, 40)]
    public static void ExportTemplates()
    {
        Directory.CreateDirectory(OverworldArtImporter.Folder);
        int n = 0, skipped = 0;
        foreach (var key in OverworldArt.Icons.Keys)
        {
            string p = OverworldArtImporter.Folder + "/" + key + ".png";
            if (File.Exists(p)) { skipped++; continue; } // 不覆盖美术已经改过的图
            var px = OverworldArt.Pixels(key);
            var t = new Texture2D(OverworldArt.Size, OverworldArt.Size, TextureFormat.RGBA32, false);
            var cols = new Color[OverworldArt.Size * OverworldArt.Size];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color(px[i * 4], px[i * 4 + 1], px[i * 4 + 2], px[i * 4 + 3]);
            t.SetPixels(cols); t.Apply();
            File.WriteAllBytes(p, t.EncodeToPNG());
            Object.DestroyImmediate(t); n++;
        }
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("像素图标模板",
            $"导出了 {n} 个图标到 {OverworldArtImporter.Folder}（已存在的 {skipped} 个没动）。\n\n" +
            "直接用 Aseprite / Procreate / Photoshop 改这些 PNG，保持文件名不变（16×16 或 32×32）。\n" +
            "游戏和小镇工坊会自动用你的图；删掉某个 PNG 就回到内置图标。", "好");
        EditorUtility.RevealInFinder(OverworldArtImporter.Folder);
    }
}
