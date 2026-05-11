// ============================================================================
// SpriteEffectFactory - Uber Sprite Shader
// Built-in 管线兼容（当前项目），切换 URP 后自动走 URP SubShader
// Keyword 开关控制各效果，未启用的效果零性能开销
// ============================================================================
Shader "MarioTrickster/SEF/UberSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // === COLOR SWAP ===
        [Toggle(_COLOR_SWAP)] _EnableColorSwap ("Enable Color Swap", Float) = 0
        _SwapColor1From ("Swap Color 1 From", Color) = (1,0,0,1)
        _SwapColor1To ("Swap Color 1 To", Color) = (0,0,1,1)
        _SwapColor2From ("Swap Color 2 From", Color) = (0,1,0,1)
        _SwapColor2To ("Swap Color 2 To", Color) = (1,1,0,1)
        _SwapColor3From ("Swap Color 3 From", Color) = (0,0,1,1)
        _SwapColor3To ("Swap Color 3 To", Color) = (1,0,1,1)
        _SwapColor4From ("Swap Color 4 From", Color) = (1,1,0,1)
        _SwapColor4To ("Swap Color 4 To", Color) = (0,1,1,1)
        _SwapTolerance ("Swap Tolerance", Range(0, 0.5)) = 0.1

        // === HIT FLASH ===
        [Toggle(_HIT_FLASH)] _EnableHitFlash ("Enable Hit Flash", Float) = 0
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0

        // === OUTLINE ===
        [Toggle(_OUTLINE)] _EnableOutline ("Enable Outline", Float) = 0
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineThickness ("Outline Thickness", Range(0, 10)) = 1
        _OutlineGlow ("Outline Glow Intensity", Range(0, 5)) = 0

        // === DISSOLVE ===
        [Toggle(_DISSOLVE)] _EnableDissolve ("Enable Dissolve", Float) = 0
        _DissolveTex ("Dissolve Noise Texture", 2D) = "white" {}
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0, 0.2)) = 0.05
        _DissolveEdgeColor ("Dissolve Edge Color", Color) = (1, 0.5, 0, 1)

        // === SILHOUETTE ===
        [Toggle(_SILHOUETTE)] _EnableSilhouette ("Enable Silhouette", Float) = 0
        _SilhouetteColor ("Silhouette Color", Color) = (0, 0, 0, 0.5)

        // === HSV ADJUSTMENT ===
        [Toggle(_HSV_ADJUST)] _EnableHSV ("Enable HSV Adjustment", Float) = 0
        _HueShift ("Hue Shift", Range(-1, 1)) = 0
        _Saturation ("Saturation", Range(0, 2)) = 1
        _Brightness ("Brightness", Range(0, 2)) = 1

        // === PIXELATE ===
        [Toggle(_PIXELATE)] _EnablePixelate ("Enable Pixelate", Float) = 0
        _PixelSize ("Pixel Size", Range(1, 64)) = 8

        // === SHADOW ===
        [Toggle(_SHADOW)] _EnableShadow ("Enable Shadow", Float) = 0
        _ShadowColor ("Shadow Color", Color) = (0, 0, 0, 0.5)
        _ShadowOffset ("Shadow Offset", Vector) = (0.02, -0.02, 0, 0)

        // === PALETTE TEXTURE ===
        [Toggle(_PALETTE_TEX)] _EnablePaletteTex ("Enable Palette Texture", Float) = 0
        _PaletteTex ("Palette Texture (1xN)", 2D) = "white" {}

        // Stencil
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
    }

    // =========================================================================
    // SubShader 1: URP (激活条件: 项目已切换到 URP)
    // =========================================================================
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "UberSprite_URP"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON

            #pragma shader_feature_local _COLOR_SWAP
            #pragma shader_feature_local _HIT_FLASH
            #pragma shader_feature_local _OUTLINE
            #pragma shader_feature_local _DISSOLVE
            #pragma shader_feature_local _SILHOUETTE
            #pragma shader_feature_local _HSV_ADJUST
            #pragma shader_feature_local _PIXELATE
            #pragma shader_feature_local _SHADOW
            #pragma shader_feature_local _PALETTE_TEX

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            TEXTURE2D(_DissolveTex);
            SAMPLER(sampler_DissolveTex);
            TEXTURE2D(_PaletteTex);
            SAMPLER(sampler_PaletteTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _SwapColor1From; float4 _SwapColor1To;
                float4 _SwapColor2From; float4 _SwapColor2To;
                float4 _SwapColor3From; float4 _SwapColor3To;
                float4 _SwapColor4From; float4 _SwapColor4To;
                float _SwapTolerance;
                float4 _FlashColor; float _FlashAmount;
                float4 _OutlineColor; float _OutlineThickness; float _OutlineGlow;
                float _DissolveAmount; float _DissolveEdgeWidth; float4 _DissolveEdgeColor;
                float4 _SilhouetteColor;
                float _HueShift; float _Saturation; float _Brightness;
                float _PixelSize;
                float4 _ShadowColor; float4 _ShadowOffset;
            CBUFFER_END

            #include "SEF_SharedLogic.hlsl"

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                #ifdef PIXELSNAP_ON
                output.positionCS = UnityPixelSnap(output.positionCS);
                #endif
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return SEF_FragmentCore(input.uv, input.color, _MainTex, sampler_MainTex, _MainTex_TexelSize,
                    _DissolveTex, sampler_DissolveTex, _PaletteTex, sampler_PaletteTex);
            }
            ENDHLSL
        }
    }

    // =========================================================================
    // SubShader 2: Built-in 管线 (当前项目使用)
    // =========================================================================
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON

            #pragma shader_feature_local _COLOR_SWAP
            #pragma shader_feature_local _HIT_FLASH
            #pragma shader_feature_local _OUTLINE
            #pragma shader_feature_local _DISSOLVE
            #pragma shader_feature_local _SILHOUETTE
            #pragma shader_feature_local _HSV_ADJUST
            #pragma shader_feature_local _PIXELATE
            #pragma shader_feature_local _SHADOW
            #pragma shader_feature_local _PALETTE_TEX

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _DissolveTex;
            sampler2D _PaletteTex;

            fixed4 _Color;
            // Color Swap
            fixed4 _SwapColor1From; fixed4 _SwapColor1To;
            fixed4 _SwapColor2From; fixed4 _SwapColor2To;
            fixed4 _SwapColor3From; fixed4 _SwapColor3To;
            fixed4 _SwapColor4From; fixed4 _SwapColor4To;
            float _SwapTolerance;
            // Hit Flash
            fixed4 _FlashColor; float _FlashAmount;
            // Outline
            fixed4 _OutlineColor; float _OutlineThickness; float _OutlineGlow;
            // Dissolve
            float _DissolveAmount; float _DissolveEdgeWidth; fixed4 _DissolveEdgeColor;
            // Silhouette
            fixed4 _SilhouetteColor;
            // HSV
            float _HueShift; float _Saturation; float _Brightness;
            // Pixelate
            float _PixelSize;
            // Shadow
            fixed4 _ShadowColor; float4 _ShadowOffset;

            // === UTILITY FUNCTIONS ===
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

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                #ifdef PIXELSNAP_ON
                o.pos = UnityPixelSnap(o.pos);
                #endif
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 uv = i.uv;

                // === PIXELATE ===
                #ifdef _PIXELATE
                {
                    float2 texSize = _MainTex_TexelSize.zw;
                    uv = floor(uv * texSize / _PixelSize) * _PixelSize / texSize;
                }
                #endif

                fixed4 col = tex2D(_MainTex, uv);
                col *= i.color;
                float originalAlpha = col.a;

                // === OUTLINE ===
                #ifdef _OUTLINE
                {
                    if (originalAlpha < 0.1)
                    {
                        float2 texelSize = _MainTex_TexelSize.xy * _OutlineThickness;
                        float aU = tex2D(_MainTex, uv + float2(0, texelSize.y)).a;
                        float aD = tex2D(_MainTex, uv - float2(0, texelSize.y)).a;
                        float aL = tex2D(_MainTex, uv - float2(texelSize.x, 0)).a;
                        float aR = tex2D(_MainTex, uv + float2(texelSize.x, 0)).a;
                        float aUL = tex2D(_MainTex, uv + float2(-texelSize.x, texelSize.y)).a;
                        float aUR = tex2D(_MainTex, uv + float2(texelSize.x, texelSize.y)).a;
                        float aDL = tex2D(_MainTex, uv + float2(-texelSize.x, -texelSize.y)).a;
                        float aDR = tex2D(_MainTex, uv + float2(texelSize.x, -texelSize.y)).a;
                        float maxA = max(max(max(aU, aD), max(aL, aR)), max(max(aUL, aUR), max(aDL, aDR)));
                        if (maxA > 0.1)
                        {
                            col = _OutlineColor;
                            col.rgb *= (1.0 + _OutlineGlow);
                            col.a = maxA;
                            originalAlpha = col.a;
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
                    fixed4 palCol = tex2D(_PaletteTex, float2(gray, 0.5));
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
                    float noise = tex2D(_DissolveTex, uv).r;
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
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
