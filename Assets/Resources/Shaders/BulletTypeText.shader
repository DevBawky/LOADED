Shader "LOADED/UI/Bullet Type Text"
{
    Properties
    {
        _FaceColor("Face Color", Color) = (1, 1, 1, 1)
        _FaceDilate("Face Dilate", Range(-1, 1)) = 0
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Thickness", Range(0, 1)) = 0
        _OutlineSoftness("Outline Softness", Range(0, 1)) = 0
        _WeightNormal("Weight Normal", Float) = 0
        _WeightBold("Weight Bold", Float) = 0.5
        _ShaderFlags("Flags", Float) = 0
        _ScaleRatioA("Scale Ratio A", Float) = 1
        _MainTex("Font Atlas", 2D) = "white" {}
        _TextureWidth("Texture Width", Float) = 512
        _TextureHeight("Texture Height", Float) = 512
        _GradientScale("Gradient Scale", Float) = 5
        _ScaleX("Scale X", Float) = 1
        _ScaleY("Scale Y", Float) = 1
        _PerspectiveFilter("Perspective Correction", Range(0, 1)) = 0.875
        _Sharpness("Sharpness", Range(-1, 1)) = 0
        _VertexOffsetX("Vertex Offset X", Float) = 0
        _VertexOffsetY("Vertex Offset Y", Float) = 0

        [HideInInspector] _EffectMode("Bullet Type Effect", Float) = 0
        [HideInInspector] _MotionIntensity("Motion Intensity", Range(0, 1)) = 1

        _ClipRect("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
        _MaskSoftnessX("Mask Softness X", Float) = 0
        _MaskSoftnessY("Mask Softness Y", Float) = 0
        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _CullMode("Cull Mode", Float) = 0
        _ColorMask("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
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

        Cull [_CullMode]
        ZWrite Off
        Lighting Off
        Fog { Mode Off }
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "BulletTypeText"

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma shader_feature_local _ OUTLINE_ON
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                float4 texcoord0 : TEXCOORD0;
            };

            struct Varyings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 vertex : SV_POSITION;
                fixed4 faceColor : COLOR;
                fixed4 outlineColor : COLOR1;
                float4 texcoord : TEXCOORD0;
                half4 parameters : TEXCOORD1;
                half4 mask : TEXCOORD2;
                float2 localPosition : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _FaceColor;
            float _FaceDilate;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineSoftness;
            float _WeightNormal;
            float _WeightBold;
            float _ScaleRatioA;
            float _GradientScale;
            float _ScaleX;
            float _ScaleY;
            float _PerspectiveFilter;
            float _Sharpness;
            float _VertexOffsetX;
            float _VertexOffsetY;
            float4 _ClipRect;
            float _MaskSoftnessX;
            float _MaskSoftnessY;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;
            float _EffectMode;
            float _MotionIntensity;

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float ValueNoise(float2 value)
            {
                float2 cell = floor(value);
                float2 fraction = frac(value);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);
                float bottom = lerp(
                    Hash21(cell),
                    Hash21(cell + float2(1.0, 0.0)),
                    fraction.x);
                float top = lerp(
                    Hash21(cell + float2(0.0, 1.0)),
                    Hash21(cell + 1.0),
                    fraction.x);
                return lerp(bottom, top, fraction.y);
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float bold = step(input.texcoord0.w, 0.0);
                float4 localVertex = input.vertex;
                localVertex.x += _VertexOffsetX;
                localVertex.y += _VertexOffsetY;
                float motion = saturate(_MotionIntensity);
                float time = _Time.y * motion;

                if (_EffectMode > 0.5 && _EffectMode < 1.5)
                {
                    float drift = sin(
                        localVertex.y * 0.09 + time * 1.8);
                    localVertex.x += drift * 0.45 * motion;
                }
                else if (_EffectMode > 2.5 && _EffectMode < 3.5)
                {
                    float electricJitter = sin(
                        time * 22.0 + localVertex.y * 0.19)
                        * sin(time * 7.1);
                    localVertex.xy += float2(
                        electricJitter,
                        -electricJitter * 0.35) * 0.32 * motion;
                }
                else if (_EffectMode > 5.5)
                {
                    float glitch = sin(
                        floor(localVertex.y * 0.16) * 2.7
                        + time * 11.0);
                    localVertex.x += glitch * 0.2 * motion;
                }

                float4 clipPosition = UnityObjectToClipPos(localVertex);

                float2 pixelSize = clipPosition.w;
                pixelSize /= float2(_ScaleX, _ScaleY)
                    * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float scale = rsqrt(dot(pixelSize, pixelSize));
                scale *= abs(input.texcoord0.w) * _GradientScale
                    * (_Sharpness + 1.0);

                if (UNITY_MATRIX_P[3][3] == 0.0)
                {
                    float perspective = abs(dot(
                        UnityObjectToWorldNormal(input.normal),
                        normalize(WorldSpaceViewDir(localVertex))));
                    scale = lerp(
                        abs(scale) * (1.0 - _PerspectiveFilter),
                        scale,
                        perspective);
                }

                float weight = lerp(_WeightNormal, _WeightBold, bold) / 4.0;
                weight = (weight + _FaceDilate) * _ScaleRatioA * 0.5;
                scale /= 1.0 + _OutlineSoftness * _ScaleRatioA * scale;
                float bias = (0.5 - weight) * scale - 0.5;
                float outline = _OutlineWidth * _ScaleRatioA * 0.5 * scale;

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                {
                    input.color.rgb = UIGammaToLinear(input.color.rgb);
                }

                fixed4 faceColor = input.color * _FaceColor;
                faceColor.rgb *= faceColor.a;
                fixed4 outlineColor = _OutlineColor;
                outlineColor.a *= input.color.a;
                outlineColor.rgb *= outlineColor.a;
                outlineColor = lerp(
                    faceColor,
                    outlineColor,
                    sqrt(min(1.0, outline * 2.0)));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                output.vertex = clipPosition;
                output.faceColor = faceColor;
                output.outlineColor = outlineColor;
                output.texcoord = input.texcoord0;
                output.parameters = half4(
                    scale,
                    bias - outline,
                    bias + outline,
                    bias);
                half2 softness = half2(
                    max(_UIMaskSoftnessX, _MaskSoftnessX),
                    max(_UIMaskSoftnessY, _MaskSoftnessY));
                output.mask = half4(
                    localVertex.xy * 2.0 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * softness + pixelSize.xy));
                output.localPosition = localVertex.xy;
                return output;
            }

            fixed4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float motion = saturate(_MotionIntensity);
                float time = _Time.y * motion;
                float2 position = input.localPosition;
                float2 atlasUv = input.texcoord.xy;

                if (_EffectMode > 0.5 && _EffectMode < 1.5)
                {
                    float smoke = ValueNoise(
                        position * 0.045 + float2(time * 0.18, -time * 0.3));
                    atlasUv += (smoke - 0.5) * _MainTex_TexelSize.xy
                        * 1.2 * motion;
                }

                half distance = tex2D(_MainTex, atlasUv).a
                    * input.parameters.x;
                half face = saturate(distance - input.parameters.w);
                fixed4 color = input.faceColor * face;

                #ifdef OUTLINE_ON
                color = lerp(
                    input.outlineColor,
                    input.faceColor,
                    saturate(distance - input.parameters.z));
                color *= saturate(distance - input.parameters.y);
                #endif

                float3 effectColor = 1.0;
                float innerLightStrength = 0.0;
                float tintStrength = 0.0;
                float alphaMultiplier = 1.0;
                float baseBrightness = 0.82;

                if (_EffectMode < 0.5)
                {
                    float pulse = 0.5 + 0.5 * sin(time * 1.35);
                    float shimmer = pow(saturate(
                        1.0 - abs(frac(position.x * 0.024 - time * 0.24)
                            - 0.5) * 8.0), 3.0);
                    effectColor = float3(1.0, 1.0, 1.0);
                    innerLightStrength = 0.08 + pulse * 0.12
                        + shimmer * 0.3;
                    tintStrength = 0.06 + shimmer * 0.18;
                    baseBrightness = 0.72 + pulse * 0.18;
                }
                else if (_EffectMode < 1.5)
                {
                    float smoke = ValueNoise(
                        position * 0.04 + float2(time * 0.14, -time * 0.24));
                    float wisp = 0.5 + 0.5 * sin(
                        position.y * 0.18
                        + position.x * 0.035
                        - time * 2.1
                        + smoke * 3.0);
                    effectColor = lerp(
                        float3(0.32, 0.82, 1.0),
                        float3(0.72, 0.38, 1.0),
                        saturate(smoke * 0.7 + wisp * 0.3));
                    innerLightStrength = 0.12 + wisp * 0.3;
                    tintStrength = 0.48 + wisp * 0.18;
                    alphaMultiplier = 0.48 + smoke * 0.22 + wisp * 0.12;
                    baseBrightness = 0.68 + wisp * 0.16;
                }
                else if (_EffectMode < 2.5)
                {
                    float sweep = pow(saturate(
                        1.0 - abs(frac(position.x * 0.011 - time * 0.38)
                            - 0.5) * 15.0), 2.0);
                    float crosshair = pow(saturate(
                        1.0 - abs(sin(position.y * 0.11 - time * 0.7))
                            * 8.0), 3.0);
                    float lockFlash = pow(saturate(
                        sin(time * 2.8) * 0.5 + 0.5), 14.0);
                    effectColor = lerp(
                        float3(0.72, 0.015, 0.025),
                        float3(1.0, 0.82, 0.64),
                        saturate(sweep + crosshair * 0.65 + lockFlash));
                    innerLightStrength = 0.08 + sweep * 0.72
                        + crosshair * 0.38 + lockFlash * 0.42;
                    tintStrength = 0.62 + sweep * 0.25;
                    baseBrightness = 0.66 + sweep * 0.22;
                }
                else if (_EffectMode < 3.5)
                {
                    float cloud = ValueNoise(
                        position * 0.07 + float2(time * 0.55, time * 0.18));
                    float boltA = pow(saturate(
                        1.0 - abs(sin(
                            position.x * 0.18
                            + position.y * 0.075
                            + floor(position.y * 0.13) * 1.35
                            - time * 8.5)) * 3.2), 7.0);
                    float boltB = pow(saturate(
                        1.0 - abs(sin(
                            -position.x * 0.13
                            + position.y * 0.11
                            + floor(position.x * 0.09) * 1.8
                            + time * 6.7)) * 3.8), 8.0);
                    float lightning = saturate(boltA + boltB);
                    float thunderFlash = pow(saturate(
                        sin(time * 3.4 + cloud * 2.0) * 0.5 + 0.5),
                        18.0);
                    effectColor = lerp(
                        lerp(
                            float3(0.035, 0.16, 0.78),
                            float3(0.55, 0.08, 1.0),
                            cloud),
                        float3(0.82, 0.96, 1.0),
                        saturate(lightning * 0.9 + thunderFlash * 0.7));
                    innerLightStrength = 0.18 + lightning * 0.96
                        + thunderFlash * 0.68;
                    tintStrength = 0.7 + cloud * 0.16
                        + lightning * 0.12;
                    baseBrightness = 0.58 + lightning * 0.28
                        + thunderFlash * 0.2;
                }
                else if (_EffectMode < 4.5)
                {
                    float2 cell = floor(position * 0.075);
                    float random = Hash21(cell);
                    float burst = pow(saturate(
                        sin(time * 4.0 + random * 6.283) * 0.5 + 0.5),
                        12.0) * step(0.76, random);
                    float pelletStreak = pow(saturate(
                        1.0 - abs(sin(
                            position.x * 0.15
                            + position.y * 0.22
                            - time * 7.0)) * 4.0), 6.0);
                    float muzzleFlash = pow(saturate(
                        sin(time * 3.8) * 0.5 + 0.5), 16.0);
                    effectColor = lerp(
                        float3(0.95, 0.08, 0.01),
                        float3(1.0, 0.9, 0.24),
                        saturate(random * 0.35 + pelletStreak
                            + muzzleFlash));
                    innerLightStrength = 0.14 + burst * 0.65
                        + pelletStreak * 0.58 + muzzleFlash * 0.46;
                    tintStrength = 0.62 + burst * 0.22;
                    baseBrightness = 0.64 + muzzleFlash * 0.24;
                }
                else if (_EffectMode < 5.5)
                {
                    float streak = pow(saturate(
                        1.0 - abs(frac(position.x * 0.009 - time * 0.5)
                            - 0.5) * 16.0), 2.0);
                    float cut = pow(saturate(
                        1.0 - abs(sin(
                            position.x * 0.16
                            - position.y * 0.21
                            - time * 8.0)) * 4.5), 8.0);
                    effectColor = lerp(
                        float3(0.04, 0.46, 0.88),
                        float3(0.82, 1.0, 1.0),
                        saturate(streak + cut));
                    innerLightStrength = 0.12 + streak * 0.78 + cut * 0.55;
                    tintStrength = 0.64 + streak * 0.24;
                    baseBrightness = 0.68 + streak * 0.18;
                }
                else
                {
                    float corruption = ValueNoise(
                        position * 0.065 + float2(time * 0.16, -time * 0.21));
                    float pulse = 0.5 + 0.5 * sin(
                        time * 1.9 + corruption * 4.0);
                    float vein = pow(saturate(
                        1.0 - abs(sin(
                            position.x * 0.13
                            + position.y * 0.17
                            + corruption * 4.5
                            - time * 2.4)) * 3.5), 6.0);
                    effectColor = lerp(
                        float3(0.04, 0.9, 0.16),
                        float3(0.86, 0.04, 1.0),
                        saturate(corruption + vein * 0.35));
                    innerLightStrength = 0.12 + pulse * 0.3 + vein * 0.62;
                    tintStrength = 0.58 + corruption * 0.22;
                    alphaMultiplier = 0.82 + corruption * 0.18;
                    baseBrightness = 0.66 + pulse * 0.16;
                }

                innerLightStrength *= lerp(0.45, 1.0, motion);
                tintStrength *= lerp(0.65, 1.0, motion);
                color.rgb = lerp(
                    color.rgb,
                    effectColor * color.a,
                    saturate(tintStrength));
                color.rgb *= baseBrightness;
                // Keep every type effect inside the TMP glyph coverage. Adding
                // alpha in the surrounding SDF range makes the text quad look
                // like a shaded rectangular background.
                color.rgb += effectColor * color.a * innerLightStrength;
                color.rgb *= alphaMultiplier;
                color.a = saturate(color.a * alphaMultiplier);

                #ifdef UNITY_UI_CLIP_RECT
                half2 clipping = saturate(
                    (_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy))
                    * input.mask.zw);
                color *= clipping.x * clipping.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }

    Fallback "TextMeshPro/Mobile/Distance Field"
}
