using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// SpriteEffectController — 挂在任何有 SpriteRenderer 的物体上
/// 游戏代码通过这个组件的公开方法触发所有视觉效果
/// 使用 MaterialPropertyBlock 实现零 Material 实例化开销
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteEffectController : MonoBehaviour
{
    // =========================================================================
    // Inspector 可调参数（编辑器面板 + 运行时均可用）
    // =========================================================================

    [Header("=== 颜色替换 (Color Swap) ===")]
    [Tooltip("启用后可将素材中的指定颜色替换为目标颜色")]
    public bool enableColorSwap;
    public Color swapColor1From = Color.red;
    public Color swapColor1To = Color.blue;
    public Color swapColor2From = Color.green;
    public Color swapColor2To = Color.yellow;
    public Color swapColor3From = Color.blue;
    public Color swapColor3To = Color.magenta;
    public Color swapColor4From = Color.yellow;
    public Color swapColor4To = Color.cyan;
    [Range(0f, 0.5f)] public float swapTolerance = 0.1f;

    [Header("=== 受击闪白 (Hit Flash) ===")]
    [Tooltip("受击时全身闪白，由代码自动驱动")]
    public Color flashColor = Color.white;
    [Range(0f, 1f)] public float flashAmount;

    [Header("=== 描边 (Outline) ===")]
    public bool enableOutline;
    public Color outlineColor = Color.white;
    [Range(0f, 10f)] public float outlineThickness = 1f;
    [Range(0f, 5f)] public float outlineGlow;

    [Header("=== 溶解 (Dissolve) ===")]
    public bool enableDissolve;
    public Texture2D dissolveNoiseTex;
    [Range(0f, 1f)] public float dissolveAmount;
    [Range(0f, 0.2f)] public float dissolveEdgeWidth = 0.05f;
    public Color dissolveEdgeColor = new Color(1f, 0.5f, 0f, 1f);

    [Header("=== 剪影 (Silhouette) ===")]
    public bool enableSilhouette;
    public Color silhouetteColor = new Color(0f, 0f, 0f, 0.5f);

    [Header("=== 色相/饱和度/亮度 (HSV) ===")]
    public bool enableHSV;
    [Range(-1f, 1f)] public float hueShift;
    [Range(0f, 2f)] public float saturation = 1f;
    [Range(0f, 2f)] public float brightness = 1f;

    [Header("=== 像素化 (Pixelate) ===")]
    public bool enablePixelate;
    [Range(1f, 64f)] public float pixelSize = 8f;

    [Header("=== 投影 (Shadow) ===")]
    public bool enableShadow;
    public Color shadowColor = new Color(0f, 0f, 0f, 0.5f);
    public Vector2 shadowOffset = new Vector2(0.02f, -0.02f);

    [Header("=== 调色板贴图 (Palette Texture) ===")]
    public bool enablePaletteTex;
    public Texture2D paletteTexture;

    // =========================================================================
    // 内部状态
    // =========================================================================
    private SpriteRenderer _sr;
    private MaterialPropertyBlock _mpb;
    private Coroutine _flashCoroutine;
    private Coroutine _dissolveCoroutine;

    // Shader property ID 缓存（避免每帧字符串查找）
    private static readonly int ID_FlashColor = Shader.PropertyToID("_FlashColor");
    private static readonly int ID_FlashAmount = Shader.PropertyToID("_FlashAmount");
    private static readonly int ID_OutlineColor = Shader.PropertyToID("_OutlineColor");
    private static readonly int ID_OutlineThickness = Shader.PropertyToID("_OutlineThickness");
    private static readonly int ID_OutlineGlow = Shader.PropertyToID("_OutlineGlow");
    private static readonly int ID_DissolveAmount = Shader.PropertyToID("_DissolveAmount");
    private static readonly int ID_DissolveEdgeWidth = Shader.PropertyToID("_DissolveEdgeWidth");
    private static readonly int ID_DissolveEdgeColor = Shader.PropertyToID("_DissolveEdgeColor");
    private static readonly int ID_DissolveTex = Shader.PropertyToID("_DissolveTex");
    private static readonly int ID_SilhouetteColor = Shader.PropertyToID("_SilhouetteColor");
    private static readonly int ID_HueShift = Shader.PropertyToID("_HueShift");
    private static readonly int ID_Saturation = Shader.PropertyToID("_Saturation");
    private static readonly int ID_Brightness = Shader.PropertyToID("_Brightness");
    private static readonly int ID_PixelSize = Shader.PropertyToID("_PixelSize");
    private static readonly int ID_ShadowColor = Shader.PropertyToID("_ShadowColor");
    private static readonly int ID_ShadowOffset = Shader.PropertyToID("_ShadowOffset");
    private static readonly int ID_SwapColor1From = Shader.PropertyToID("_SwapColor1From");
    private static readonly int ID_SwapColor1To = Shader.PropertyToID("_SwapColor1To");
    private static readonly int ID_SwapColor2From = Shader.PropertyToID("_SwapColor2From");
    private static readonly int ID_SwapColor2To = Shader.PropertyToID("_SwapColor2To");
    private static readonly int ID_SwapColor3From = Shader.PropertyToID("_SwapColor3From");
    private static readonly int ID_SwapColor3To = Shader.PropertyToID("_SwapColor3To");
    private static readonly int ID_SwapColor4From = Shader.PropertyToID("_SwapColor4From");
    private static readonly int ID_SwapColor4To = Shader.PropertyToID("_SwapColor4To");
    private static readonly int ID_SwapTolerance = Shader.PropertyToID("_SwapTolerance");
    private static readonly int ID_PaletteTex = Shader.PropertyToID("_PaletteTex");

    // =========================================================================
    // 生命周期
    // =========================================================================
    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        ApplyAllProperties();
    }

    // =========================================================================
    // 核心：将所有参数同步到 MaterialPropertyBlock
    // =========================================================================
    private void ApplyAllProperties()
    {
        _sr.GetPropertyBlock(_mpb);

        // Keywords 通过 Material 设置（MPB 不支持 keyword）
        // 所以我们需要确保 Material 上的 keyword 与当前状态一致
        var mat = _sr.sharedMaterial;
        if (mat != null)
        {
            SetKeyword(mat, "_COLOR_SWAP", enableColorSwap);
            SetKeyword(mat, "_HIT_FLASH", flashAmount > 0.001f);
            SetKeyword(mat, "_OUTLINE", enableOutline);
            SetKeyword(mat, "_DISSOLVE", enableDissolve);
            SetKeyword(mat, "_SILHOUETTE", enableSilhouette);
            SetKeyword(mat, "_HSV_ADJUST", enableHSV);
            SetKeyword(mat, "_PIXELATE", enablePixelate);
            SetKeyword(mat, "_SHADOW", enableShadow);
            SetKeyword(mat, "_PALETTE_TEX", enablePaletteTex);
        }

        // Color Swap
        _mpb.SetColor(ID_SwapColor1From, swapColor1From);
        _mpb.SetColor(ID_SwapColor1To, swapColor1To);
        _mpb.SetColor(ID_SwapColor2From, swapColor2From);
        _mpb.SetColor(ID_SwapColor2To, swapColor2To);
        _mpb.SetColor(ID_SwapColor3From, swapColor3From);
        _mpb.SetColor(ID_SwapColor3To, swapColor3To);
        _mpb.SetColor(ID_SwapColor4From, swapColor4From);
        _mpb.SetColor(ID_SwapColor4To, swapColor4To);
        _mpb.SetFloat(ID_SwapTolerance, swapTolerance);

        // Hit Flash
        _mpb.SetColor(ID_FlashColor, flashColor);
        _mpb.SetFloat(ID_FlashAmount, flashAmount);

        // Outline
        _mpb.SetColor(ID_OutlineColor, outlineColor);
        _mpb.SetFloat(ID_OutlineThickness, outlineThickness);
        _mpb.SetFloat(ID_OutlineGlow, outlineGlow);

        // Dissolve
        _mpb.SetFloat(ID_DissolveAmount, dissolveAmount);
        _mpb.SetFloat(ID_DissolveEdgeWidth, dissolveEdgeWidth);
        _mpb.SetColor(ID_DissolveEdgeColor, dissolveEdgeColor);
        if (dissolveNoiseTex != null)
            _mpb.SetTexture(ID_DissolveTex, dissolveNoiseTex);

        // Silhouette
        _mpb.SetColor(ID_SilhouetteColor, silhouetteColor);

        // HSV
        _mpb.SetFloat(ID_HueShift, hueShift);
        _mpb.SetFloat(ID_Saturation, saturation);
        _mpb.SetFloat(ID_Brightness, brightness);

        // Pixelate
        _mpb.SetFloat(ID_PixelSize, pixelSize);

        // Shadow
        _mpb.SetColor(ID_ShadowColor, shadowColor);
        _mpb.SetVector(ID_ShadowOffset, new Vector4(shadowOffset.x, shadowOffset.y, 0, 0));

        // Palette Texture
        if (paletteTexture != null)
            _mpb.SetTexture(ID_PaletteTex, paletteTexture);

        _sr.SetPropertyBlock(_mpb);
    }

    private static void SetKeyword(Material mat, string keyword, bool enabled)
    {
        if (enabled) mat.EnableKeyword(keyword);
        else mat.DisableKeyword(keyword);
    }

    // =========================================================================
    // 公开 API — 游戏代码直接调用这些方法
    // =========================================================================

    /// <summary>
    /// 播放受击闪白（自动淡出）
    /// 用法：GetComponent&lt;SpriteEffectController&gt;().PlayHitFlash();
    /// </summary>
    public void PlayHitFlash(float duration = 0.15f, Color? color = null)
    {
        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        if (color.HasValue) flashColor = color.Value;
        _flashCoroutine = StartCoroutine(HitFlashRoutine(duration));
    }

    /// <summary>
    /// 播放溶解动画（从完整到消失）
    /// 用法：GetComponent&lt;SpriteEffectController&gt;().PlayDissolve();
    /// </summary>
    public void PlayDissolve(float duration = 1.0f, System.Action onComplete = null)
    {
        if (_dissolveCoroutine != null) StopCoroutine(_dissolveCoroutine);
        enableDissolve = true;
        _dissolveCoroutine = StartCoroutine(DissolveRoutine(duration, onComplete));
    }

    /// <summary>
    /// 反向溶解（从消失到完整出现）
    /// </summary>
    public void PlayDissolveIn(float duration = 1.0f, System.Action onComplete = null)
    {
        if (_dissolveCoroutine != null) StopCoroutine(_dissolveCoroutine);
        enableDissolve = true;
        dissolveAmount = 1f;
        _dissolveCoroutine = StartCoroutine(DissolveInRoutine(duration, onComplete));
    }

    /// <summary>
    /// 完整的受击反馈（闪白 + 可选的轻微抖动）
    /// </summary>
    public void PlayHitFeedback(float flashDuration = 0.15f)
    {
        PlayHitFlash(flashDuration);
    }

    /// <summary>
    /// 死亡序列：灰度化 → 溶解消失
    /// </summary>
    public void PlayDeathSequence(float greyDuration = 0.3f, float dissolveDuration = 0.8f, System.Action onComplete = null)
    {
        StartCoroutine(DeathSequenceRoutine(greyDuration, dissolveDuration, onComplete));
    }

    /// <summary>
    /// 重置所有效果到默认状态
    /// </summary>
    public void ResetAllEffects()
    {
        enableColorSwap = false;
        flashAmount = 0f;
        enableOutline = false;
        enableDissolve = false;
        dissolveAmount = 0f;
        enableSilhouette = false;
        enableHSV = false;
        hueShift = 0f;
        saturation = 1f;
        brightness = 1f;
        enablePixelate = false;
        enableShadow = false;
        enablePaletteTex = false;
    }

    // =========================================================================
    // 协程动画
    // =========================================================================
    private IEnumerator HitFlashRoutine(float duration)
    {
        flashAmount = 1f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            flashAmount = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }
        flashAmount = 0f;
        _flashCoroutine = null;
    }

    private IEnumerator DissolveRoutine(float duration, System.Action onComplete)
    {
        dissolveAmount = 0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            dissolveAmount = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }
        dissolveAmount = 1f;
        onComplete?.Invoke();
        _dissolveCoroutine = null;
    }

    private IEnumerator DissolveInRoutine(float duration, System.Action onComplete)
    {
        dissolveAmount = 1f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            dissolveAmount = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }
        dissolveAmount = 0f;
        enableDissolve = false;
        onComplete?.Invoke();
        _dissolveCoroutine = null;
    }

    private IEnumerator DeathSequenceRoutine(float greyDuration, float dissolveDuration, System.Action onComplete)
    {
        // Phase 1: 灰度化
        enableHSV = true;
        float elapsed = 0f;
        while (elapsed < greyDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / greyDuration;
            saturation = Mathf.Lerp(1f, 0f, t);
            brightness = Mathf.Lerp(1f, 0.6f, t);
            yield return null;
        }

        // Phase 2: 溶解
        yield return DissolveRoutine(dissolveDuration, null);

        onComplete?.Invoke();
    }
}
