// ============================================================================
// SEF_SharedLogic.hlsl - URP pass 共享片元逻辑
// 被 URP SubShader 的 HLSLPROGRAM 引用
// ============================================================================
#ifndef SEF_SHARED_LOGIC_INCLUDED
#define SEF_SHARED_LOGIC_INCLUDED

float3 SEF_RGBtoHSV(float3 c)
{
    float4 K = float4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 SEF_HSVtoRGB(float3 c)
{
    float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
}

half4 SEF_FragmentCore(
    float2 uv, float4 vertexColor,
    TEXTURE2D_PARAM(mainTex, mainSampler), float4 mainTexelSize,
    TEXTURE2D_PARAM(dissolveTex, dissolveSampler),
    TEXTURE2D_PARAM(paletteTex, paletteSampler))
{
    // === PIXELATE ===
    #ifdef _PIXELATE
    {
        float2 texSize = mainTexelSize.zw;
        uv = floor(uv * texSize / _PixelSize) * _PixelSize / texSize;
    }
    #endif

    half4 col = SAMPLE_TEXTURE2D(mainTex, mainSampler, uv);
    col *= vertexColor;
    float originalAlpha = col.a;

    // === OUTLINE ===
    #ifdef _OUTLINE
    {
        if (originalAlpha < 0.1)
        {
            float2 texelSize = mainTexelSize.xy * _OutlineThickness;
            float aU = SAMPLE_TEXTURE2D(mainTex, mainSampler, uv + float2(0, texelSize.y)).a;
            float aD = SAMPLE_TEXTURE2D(mainTex, mainSampler, uv - float2(0, texelSize.y)).a;
            float aL = SAMPLE_TEXTURE2D(mainTex, mainSampler, uv - float2(texelSize.x, 0)).a;
            float aR = SAMPLE_TEXTURE2D(mainTex, mainSampler, uv + float2(texelSize.x, 0)).a;
            float aUL = SAMPLE_TEXTURE2D(mainTex, mainSampler, uv + float2(-texelSize.x, texelSize.y)).a;
            float aUR = SAMPLE_TEXTURE2D(mainTex, mainSampler, uv + float2(texelSize.x, texelSize.y)).a;
            float aDL = SAMPLE_TEXTURE2D(mainTex, mainSampler, uv + float2(-texelSize.x, -texelSize.y)).a;
            float aDR = SAMPLE_TEXTURE2D(mainTex, mainSampler, uv + float2(texelSize.x, -texelSize.y)).a;
            float maxA = max(max(max(aU, aD), max(aL, aR)), max(max(aUL, aUR), max(aDL, aDR)));
            if (maxA > 0.1)
            {
                col = half4(_OutlineColor.rgb * (1.0 + _OutlineGlow), maxA);
                originalAlpha = maxA;
            }
        }
    }
    #endif

    if (originalAlpha < 0.01)
        discard;

    // === PALETTE TEXTURE SWAP ===
    #ifdef _PALETTE_TEX
    {
        float gray = dot(col.rgb, float3(0.299, 0.587, 0.114));
        half4 palCol = SAMPLE_TEXTURE2D(paletteTex, paletteSampler, float2(gray, 0.5));
        col.rgb = palCol.rgb;
    }
    #endif

    // === COLOR SWAP ===
    #ifdef _COLOR_SWAP
    {
        float d1 = length(col.rgb - _SwapColor1From.rgb);
        float d2 = length(col.rgb - _SwapColor2From.rgb);
        float d3 = length(col.rgb - _SwapColor3From.rgb);
        float d4 = length(col.rgb - _SwapColor4From.rgb);
        if (d1 < _SwapTolerance)
            col.rgb = lerp(col.rgb, _SwapColor1To.rgb, 1.0 - (d1 / _SwapTolerance));
        else if (d2 < _SwapTolerance)
            col.rgb = lerp(col.rgb, _SwapColor2To.rgb, 1.0 - (d2 / _SwapTolerance));
        else if (d3 < _SwapTolerance)
            col.rgb = lerp(col.rgb, _SwapColor3To.rgb, 1.0 - (d3 / _SwapTolerance));
        else if (d4 < _SwapTolerance)
            col.rgb = lerp(col.rgb, _SwapColor4To.rgb, 1.0 - (d4 / _SwapTolerance));
    }
    #endif

    // === HSV ADJUSTMENT ===
    #ifdef _HSV_ADJUST
    {
        float3 hsv = SEF_RGBtoHSV(col.rgb);
        hsv.x = frac(hsv.x + _HueShift);
        hsv.y *= _Saturation;
        hsv.z *= _Brightness;
        col.rgb = SEF_HSVtoRGB(hsv);
    }
    #endif

    // === SILHOUETTE ===
    #ifdef _SILHOUETTE
    {
        col.rgb = _SilhouetteColor.rgb;
        col.a *= _SilhouetteColor.a;
    }
    #endif

    // === HIT FLASH ===
    #ifdef _HIT_FLASH
    {
        col.rgb = lerp(col.rgb, _FlashColor.rgb, _FlashAmount);
    }
    #endif

    // === DISSOLVE ===
    #ifdef _DISSOLVE
    {
        float noise = SAMPLE_TEXTURE2D(dissolveTex, dissolveSampler, uv).r;
        float dissolveEdge = _DissolveAmount + _DissolveEdgeWidth;
        if (noise < _DissolveAmount)
            discard;
        else if (noise < dissolveEdge)
        {
            float edgeFactor = (noise - _DissolveAmount) / _DissolveEdgeWidth;
            col.rgb = lerp(_DissolveEdgeColor.rgb, col.rgb, edgeFactor);
        }
    }
    #endif

    // Premultiply alpha
    col.rgb *= col.a;
    return col;
}

#endif // SEF_SHARED_LOGIC_INCLUDED
