using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Shader 属性扫描器（Editor Only）
/// 扫描 Material 上所有暴露的属性，自动识别并分类为效果类型
/// 这是"万能兼容"的核心 — 不管什么 Shader，都能探测出可调参数
/// </summary>
public static class ShaderPropertyScanner
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

    /// <summary>
    /// 扫描 Material 的所有属性，自动识别并分类
    /// </summary>
    public static List<DetectedProperty> ScanMaterialProperties(Material material)
    {
        var results = new List<DetectedProperty>();
        if (material == null) return results;

        Shader shader = material.shader;
        int propCount = ShaderUtil.GetPropertyCount(shader);

        for (int i = 0; i < propCount; i++)
        {
            string propName = ShaderUtil.GetPropertyName(shader, i);
            string displayName = ShaderUtil.GetPropertyDescription(shader, i);
            var propType = ShaderUtil.GetPropertyType(shader, i);

            if (propName.StartsWith("_Stencil") || propName == "_MainTex" || propName == "_Color")
                continue;

            // 跳过 HideInInspector
            if (ShaderUtil.IsShaderPropertyHidden(shader, i))
                continue;

            var detected = new DetectedProperty
            {
                propertyName = propName,
                displayName = string.IsNullOrEmpty(displayName) ? propName : displayName,
            };

            switch (propType)
            {
                case ShaderUtil.ShaderPropertyType.Float:
                    if (propName.StartsWith("_Enable") || propName.Contains("Toggle"))
                    {
                        detected.propertyType = SEFPropertyType.Toggle;
                        detected.floatValue = material.GetFloat(propName);
                    }
                    else
                    {
                        detected.propertyType = SEFPropertyType.Float;
                        detected.floatValue = material.GetFloat(propName);
                    }
                    break;

                case ShaderUtil.ShaderPropertyType.Range:
                    detected.propertyType = SEFPropertyType.Range;
                    detected.rangeMin = ShaderUtil.GetRangeLimits(shader, i, 1);
                    detected.rangeMax = ShaderUtil.GetRangeLimits(shader, i, 2);
                    detected.floatValue = material.GetFloat(propName);
                    break;

                case ShaderUtil.ShaderPropertyType.Color:
                    detected.propertyType = SEFPropertyType.Color;
                    detected.colorValue = material.GetColor(propName);
                    break;

                case ShaderUtil.ShaderPropertyType.Vector:
                    detected.propertyType = SEFPropertyType.Vector;
                    detected.vectorValue = material.GetVector(propName);
                    break;

                case ShaderUtil.ShaderPropertyType.TexEnv:
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
}
