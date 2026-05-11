using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 素材颜色自动拆解器 — 拖入任意 Sprite，自动提取所有颜色块
/// 用户点击某个颜色即可一键替换
/// </summary>
public static class SpriteColorAnalyzer
{
    /// <summary>
    /// 从 Sprite 中提取所有不重复的颜色（按占比排序）
    /// </summary>
    public static List<ColorInfo> ExtractColors(Sprite sprite, float tolerance = 0.05f, int maxColors = 32)
    {
        if (sprite == null) return new List<ColorInfo>();

        // 确保贴图可读
        Texture2D tex = sprite.texture;
        Texture2D readable = GetReadableTexture(tex, sprite);
        if (readable == null) return new List<ColorInfo>();

        Color[] pixels = readable.GetPixels();
        var colorBuckets = new Dictionary<Color, int>(new ColorComparer(tolerance));
        int totalOpaque = 0;

        foreach (var px in pixels)
        {
            if (px.a < 0.1f) continue; // 跳过透明像素
            totalOpaque++;

            // 量化颜色以减少噪声
            Color quantized = QuantizeColor(px, tolerance);
            if (colorBuckets.ContainsKey(quantized))
                colorBuckets[quantized]++;
            else
                colorBuckets[quantized] = 1;
        }

        if (Application.isEditor && readable != tex)
            Object.DestroyImmediate(readable);

        return colorBuckets
            .OrderByDescending(kv => kv.Value)
            .Take(maxColors)
            .Select(kv => new ColorInfo
            {
                color = kv.Key,
                pixelCount = kv.Value,
                percentage = totalOpaque > 0 ? (float)kv.Value / totalOpaque * 100f : 0f
            })
            .ToList();
    }

    /// <summary>
    /// 获取可读的贴图副本（处理不可读贴图的情况）
    /// </summary>
    private static Texture2D GetReadableTexture(Texture2D source, Sprite sprite)
    {
        if (source.isReadable)
        {
            // 如果是 Sprite 子区域，裁剪出来
            if (sprite.rect.width < source.width || sprite.rect.height < source.height)
            {
                return CropToSpriteRect(source, sprite);
            }
            return source;
        }

        // 贴图不可读 → 通过 RenderTexture 拷贝
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0);
        Graphics.Blit(source, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D copy = new Texture2D(
            (int)sprite.rect.width, (int)sprite.rect.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(sprite.rect.x, sprite.rect.y, sprite.rect.width, sprite.rect.height), 0, 0);
        copy.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return copy;
    }

    private static Texture2D CropToSpriteRect(Texture2D source, Sprite sprite)
    {
        int x = Mathf.FloorToInt(sprite.rect.x);
        int y = Mathf.FloorToInt(sprite.rect.y);
        int w = Mathf.FloorToInt(sprite.rect.width);
        int h = Mathf.FloorToInt(sprite.rect.height);
        Color[] px = source.GetPixels(x, y, w, h);
        Texture2D cropped = new Texture2D(w, h, TextureFormat.RGBA32, false);
        cropped.SetPixels(px);
        cropped.Apply();
        return cropped;
    }

    private static Color QuantizeColor(Color c, float step)
    {
        float inv = 1f / Mathf.Max(step, 0.01f);
        return new Color(
            Mathf.Round(c.r * inv) / inv,
            Mathf.Round(c.g * inv) / inv,
            Mathf.Round(c.b * inv) / inv,
            1f
        );
    }

    /// <summary>
    /// 生成一张 1xN 的调色板贴图（可直接用于 Palette Texture Swap）
    /// </summary>
    public static Texture2D GeneratePaletteTexture(List<Color> colors)
    {
        int count = Mathf.Max(colors.Count, 1);
        Texture2D tex = new Texture2D(count, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int i = 0; i < count; i++)
        {
            tex.SetPixel(i, 0, i < colors.Count ? colors[i] : Color.white);
        }
        tex.Apply();
        return tex;
    }

    // =========================================================================
    // 辅助类型
    // =========================================================================
    [System.Serializable]
    public class ColorInfo
    {
        public Color color;
        public int pixelCount;
        public float percentage;
    }

    private class ColorComparer : IEqualityComparer<Color>
    {
        private float _tolerance;
        public ColorComparer(float tolerance) { _tolerance = tolerance; }

        public bool Equals(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < _tolerance
                && Mathf.Abs(a.g - b.g) < _tolerance
                && Mathf.Abs(a.b - b.b) < _tolerance;
        }

        public int GetHashCode(Color c)
        {
            float inv = 1f / Mathf.Max(_tolerance, 0.01f);
            int r = Mathf.RoundToInt(c.r * inv);
            int g = Mathf.RoundToInt(c.g * inv);
            int b = Mathf.RoundToInt(c.b * inv);
            return (r * 73856093) ^ (g * 19349663) ^ (b * 83492791);
        }
    }
}
