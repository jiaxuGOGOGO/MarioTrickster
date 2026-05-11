using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Shader后端适配器 — 自动检测并兼容多种Shader插件
/// 不管底层用什么Shader，对外暴露统一的效果控制接口
/// Runtime 部分只包含枚举定义和基础检测，高级属性扫描在 Editor 侧
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
/// Shader后端适配器核心类（Runtime 安全部分）
/// </summary>
public static class ShaderBackendAdapter
{
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
