using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Shader后端适配器 — 自动检测并兼容多种Shader插件
/// 不管底层用什么Shader，对外暴露统一的效果控制接口
/// </summary>
public enum ShaderBackendType
{
    BuiltIn,        // 内置 SEF UberSprite
    AllIn1,         // All In 1 Sprite Shader
    SpriteUltimate, // Sprite Shaders Ultimate
    ShaderGraph,    // Unity Shader Graph 自定义
    Custom          // 任意自定义Shader（通过属性探测）
}

public enum EffectType
{
    ColorSwap, HitFlash, Outline, Dissolve, Silhouette,
    HSVAdjust, Pixelate, Shadow, PaletteTexture,
    Hologram, Glitch, Blur, Glow, ColorRamp,
    Negative, Greyscale, Distortion, Shake, Fade
}

public enum SEFPropertyType
{
    Float, Range, Color, Vector, Texture, Toggle
}

[System.Serializable]
public class DetectedProperty
{
    public string propertyName;
    public string displayName;
    public SEFPropertyType propertyType;
    public EffectType? guessedEffect;
    public float floatValue;
    public Color colorValue;
    public Vector4 vectorValue;
    public Texture textureValue;
    public float rangeMin;
    public float rangeMax;
}

/// <summary>
/// Shader后端适配器核心类
/// </summary>
public static class ShaderBackendAdapter
{
    private static readonly Dictionary<string, EffectType> KnownPropertyMappings = new Dictionary<string, EffectType>
    {
        // 内置 UberSprite
        { "_FlashAmount", EffectType.HitFlash },
        { "_FlashColor", EffectType.HitFlash },
        { "_OutlineColor", EffectType.Outline },
        { "_OutlineThickness", EffectType.Outline },
        { "_DissolveAmount", EffectType.Dissolve },
        { "_HueShift", EffectType.HSVAdjust },
        { "_Saturation", EffectType.HSVAdjust },
        { "_Brightness", EffectType.HSVAdjust },
        { "_PixelSize", EffectType.Pixelate },
        { "_SilhouetteColor", EffectType.Silhouette },
        // All In 1 Sprite Shader
        { "_HitEffectBlend", EffectType.HitFlash },
        { "_HitEffectColor", EffectType.HitFlash },
        { "_OutlineAlpha", EffectType.Outline },
        { "_OutlinePixelWidth", EffectType.Outline },
        { "_FadeAmount", EffectType.Dissolve },
        { "_GlowColor", EffectType.Glow },
        { "_GlowIntensity", EffectType.Glow },
        { "_HologramStrength", EffectType.Hologram },
        { "_GlitchAmount", EffectType.Glitch },
        { "_BlurAmount", EffectType.Blur },
        { "_GreyscaleBlend", EffectType.Greyscale },
        { "_NegativeAmount", EffectType.Negative },
        { "_ColorRampBlend", EffectType.ColorRamp },
        { "_DistortAmount", EffectType.Distortion },
        { "_ShakeAmount", EffectType.Shake },
        // Sprite Shaders Ultimate
        { "_FlashFactor", EffectType.HitFlash },
        { "_OutlineWidth", EffectType.Outline },
        { "_DissolveProgress", EffectType.Dissolve },
        { "_HueOffset", EffectType.HSVAdjust },
    };

    private static readonly Dictionary<string, EffectType> NamePatterns = new Dictionary<string, EffectType>
    {
        { "flash", EffectType.HitFlash }, { "hit", EffectType.HitFlash },
        { "outline", EffectType.Outline }, { "dissolve", EffectType.Dissolve },
        { "fade", EffectType.Fade }, { "silhouette", EffectType.Silhouette },
        { "hue", EffectType.HSVAdjust }, { "saturation", EffectType.HSVAdjust },
        { "brightness", EffectType.HSVAdjust }, { "pixel", EffectType.Pixelate },
        { "shadow", EffectType.Shadow }, { "glow", EffectType.Glow },
        { "hologram", EffectType.Hologram }, { "glitch", EffectType.Glitch },
        { "blur", EffectType.Blur }, { "grey", EffectType.Greyscale },
        { "gray", EffectType.Greyscale }, { "negative", EffectType.Negative },
        { "invert", EffectType.Negative }, { "distort", EffectType.Distortion },
        { "shake", EffectType.Shake }, { "swap", EffectType.ColorSwap },
        { "replace", EffectType.ColorSwap }, { "palette", EffectType.PaletteTexture },
        { "ramp", EffectType.ColorRamp },
    };

    public static ShaderBackendType DetectBackend(Material material)
    {
        if (material == null) return ShaderBackendType.Custom;
        string shaderName = material.shader.name.ToLower();
        if (shaderName.Contains("sef") || shaderName.Contains("ubersprite"))
            return ShaderBackendType.BuiltIn;
        if (shaderName.Contains("allin1") || shaderName.Contains("all in 1") || shaderName.Contains("seaside"))
            return ShaderBackendType.AllIn1;
        if (shaderName.Contains("spriteshadersultimate") || shaderName.Contains("sprite shaders ultimate"))
            return ShaderBackendType.SpriteUltimate;
        if (shaderName.Contains("shadergraph") || shaderName.Contains("shader graph"))
            return ShaderBackendType.ShaderGraph;
        return ShaderBackendType.Custom;
    }

    public static List<DetectedProperty> ScanMaterialProperties(Material material)
    {
        var results = new List<DetectedProperty>();
        if (material == null) return results;

        Shader shader = material.shader;
        int propCount = shader.GetPropertyCount();

        for (int i = 0; i < propCount; i++)
        {
            string propName = shader.GetPropertyName(i);
            string displayName = shader.GetPropertyDescription(i);
            var propType = shader.GetPropertyType(i);

            if (propName.StartsWith("_Stencil") || propName == "_MainTex" || propName == "_Color")
                continue;

            var detected = new DetectedProperty
            {
                propertyName = propName,
                displayName = string.IsNullOrEmpty(displayName) ? propName : displayName,
            };

            switch (propType)
            {
                case UnityEngine.Rendering.ShaderPropertyType.Float:
                    var flags = shader.GetPropertyFlags(i);
                    if ((flags & UnityEngine.Rendering.ShaderPropertyFlags.HideInInspector) != 0)
                        continue;
                    if (propName.StartsWith("_Enable") || propName.Contains("Toggle"))
                    {
                        detected.propertyType = SEFPropertyType.Toggle;
                        detected.floatValue = material.GetFloat(propName);
                    }
                    else
                    {
                        var range = shader.GetPropertyRangeLimits(i);
                        if (range.x != 0 || range.y != 0)
                        {
                            detected.propertyType = SEFPropertyType.Range;
                            detected.rangeMin = range.x;
                            detected.rangeMax = range.y;
                        }
                        else
                        {
                            detected.propertyType = SEFPropertyType.Float;
                        }
                        detected.floatValue = material.GetFloat(propName);
                    }
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Color:
                    detected.propertyType = SEFPropertyType.Color;
                    detected.colorValue = material.GetColor(propName);
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Vector:
                    detected.propertyType = SEFPropertyType.Vector;
                    detected.vectorValue = material.GetVector(propName);
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Texture:
                    detected.propertyType = SEFPropertyType.Texture;
                    detected.textureValue = material.GetTexture(propName);
                    break;
                default:
                    continue;
            }

            detected.guessedEffect = GuessEffectType(propName);
            results.Add(detected);
        }
        return results;
    }

    private static EffectType? GuessEffectType(string propertyName)
    {
        if (KnownPropertyMappings.TryGetValue(propertyName, out EffectType exactMatch))
            return exactMatch;
        string lower = propertyName.ToLower();
        foreach (var pattern in NamePatterns)
        {
            if (lower.Contains(pattern.Key))
                return pattern.Value;
        }
        return null;
    }

    public static string GetEffectDisplayName(EffectType type)
    {
        switch (type)
        {
            case EffectType.ColorSwap: return "颜色替换";
            case EffectType.HitFlash: return "受击闪白";
            case EffectType.Outline: return "描边/发光";
            case EffectType.Dissolve: return "溶解消散";
            case EffectType.Silhouette: return "剪影/遮挡";
            case EffectType.HSVAdjust: return "色相/饱和度/亮度";
            case EffectType.Pixelate: return "像素化";
            case EffectType.Shadow: return "投影阴影";
            case EffectType.PaletteTexture: return "调色板贴图";
            case EffectType.Hologram: return "全息效果";
            case EffectType.Glitch: return "故障效果";
            case EffectType.Blur: return "模糊";
            case EffectType.Glow: return "发光";
            case EffectType.ColorRamp: return "色彩渐变";
            case EffectType.Negative: return "反色/底片";
            case EffectType.Greyscale: return "灰度化";
            case EffectType.Distortion: return "扭曲变形";
            case EffectType.Shake: return "抖动";
            case EffectType.Fade: return "淡入淡出";
            default: return type.ToString();
        }
    }
}
