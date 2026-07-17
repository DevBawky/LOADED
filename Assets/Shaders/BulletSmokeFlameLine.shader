Shader "Loaded/Bullet Smoke Flame Line"
{
    Properties
    {
        [Header(Motion)]
        _ScrollSpeed("Scroll Speed", Float) = 2.6
        _NoiseScale("Noise Scale", Float) = 9.0
        _EdgeDistortion("Edge Distortion", Range(0.0, 0.5)) = 0.22

        [Header(Shape)]
        _EdgeSoftness("Edge Softness", Range(0.01, 1.0)) = 0.42
        _CoreWidth("Core Width", Range(0.01, 0.8)) = 0.2
        _TailTaper("Tail Taper", Range(0.0, 1.0)) = 0.35

        [Header(Energy)]
        _CoreIntensity("Core Intensity", Range(0.0, 5.0)) = 2.4
        _SparkDensity("Spark Density", Range(0.0, 1.0)) = 0.86
        _SparkIntensity("Spark Intensity", Range(0.0, 5.0)) = 2.7
        _OverallAlpha("Overall Alpha", Range(0.0, 1.0)) = 0.9

        [Header(Smoke Detail)]
        _SmokeBreakup("Smoke Breakup", Range(0.0, 1.0)) = 0.38
        _EndSoftness("End Softness", Range(0.001, 0.35)) = 0.08

        [Header(Color Mix)]
        [HDR] _SecondaryColor("Secondary Color", Color) = (1.0, 0.45, 0.08, 1.0)
        _ColorBlend("Secondary Blend", Range(0.0, 1.0)) = 0.72
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SmokeFlameLine"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float _ScrollSpeed;
                float _NoiseScale;
                float _EdgeDistortion;
                float _EdgeSoftness;
                float _CoreWidth;
                float _TailTaper;
                float _CoreIntensity;
                float _SparkDensity;
                float _SparkIntensity;
                float _OverallAlpha;
                float _SmokeBreakup;
                float _EndSoftness;
                half4 _SecondaryColor;
                float _ColorBlend;
            CBUFFER_END

            float Hash(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float ValueNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 localPosition = frac(position);
                float2 blend = localPosition * localPosition
                    * (3.0 - 2.0 * localPosition);

                float bottom = lerp(
                    Hash(cell),
                    Hash(cell + float2(1.0, 0.0)),
                    blend.x);
                float top = lerp(
                    Hash(cell + float2(0.0, 1.0)),
                    Hash(cell + float2(1.0, 1.0)),
                    blend.x);

                return lerp(bottom, top, blend.y);
            }

            float FractalNoise(float2 position)
            {
                float noise = ValueNoise(position) * 0.57;
                noise += ValueNoise(position * 2.07 + 13.17) * 0.28;
                noise += ValueNoise(position * 4.13 - 7.31) * 0.15;
                return noise;
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float time = _Time.y * _ScrollSpeed;
                float domainNoise = FractalNoise(float2(
                    input.uv.x * _NoiseScale * 0.45 - time * 0.55,
                    input.uv.y * 1.7 + time * 0.09));
                float broadNoise = FractalNoise(float2(
                    input.uv.x * _NoiseScale - time,
                    input.uv.y * 2.8 + domainNoise * 2.1 + time * 0.12));
                float fineNoise = FractalNoise(float2(
                    input.uv.x * _NoiseScale * 2.1 + time * 1.7,
                    input.uv.y * 5.3 - domainNoise * 1.4 - time * 0.2));

                float distanceFromCore = abs(input.uv.y * 2.0 - 1.0);
                float warpedDistance = saturate(
                    distanceFromCore
                    + (broadNoise - 0.5) * _EdgeDistortion);
                float smokeMask = 1.0 - smoothstep(
                    1.0 - _EdgeSoftness,
                    1.0,
                    warpedDistance);
                float densityNoise = saturate(
                    broadNoise * 0.52
                    + fineNoise * 0.3
                    + domainNoise * 0.18);
                float breakupMask = smoothstep(
                    _SmokeBreakup,
                    _SmokeBreakup + 0.35,
                    densityNoise + smokeMask * 0.48);
                float coreMask = 1.0 - smoothstep(
                    _CoreWidth,
                    min(1.0, _CoreWidth + 0.2),
                    distanceFromCore);
                float tailMask = lerp(
                    1.0 - _TailTaper,
                    1.0,
                    smoothstep(0.0, 0.4, input.uv.x));
                float endMask = smoothstep(
                    0.0,
                    _EndSoftness,
                    input.uv.x)
                    * (1.0 - smoothstep(
                        1.0 - _EndSoftness,
                        1.0,
                        input.uv.x));

                float2 sparkPosition = float2(
                    input.uv.x * 48.0 - time * 6.0,
                    input.uv.y * 6.0);
                float2 sparkCell = floor(sparkPosition);
                float2 sparkLocal = frac(sparkPosition) - 0.5;
                float sparkSeed = Hash(sparkCell);
                float sparkShape = 1.0 - smoothstep(
                    0.05,
                    0.38,
                    length(sparkLocal * float2(0.45, 1.0)));
                float sparks = smoothstep(
                    _SparkDensity,
                    1.0,
                    sparkSeed) * sparkShape * smokeMask * endMask;

                float wispMask = lerp(
                    0.42,
                    1.0,
                    saturate(broadNoise * 0.65 + fineNoise * 0.35));
                float alpha = saturate(
                    smokeMask * breakupMask * wispMask + sparks * 0.8)
                    * tailMask
                    * endMask
                    * _OverallAlpha
                    * input.color.a;
                float brightness = lerp(0.48, 0.85, broadNoise)
                    + coreMask * _CoreIntensity
                    + sparks * _SparkIntensity;
                float secondaryMix = saturate(
                    fineNoise * 0.35
                    + coreMask * 0.7
                    + sparks)
                    * _ColorBlend
                    * _SecondaryColor.a;
                half3 mixedColor = lerp(
                    input.color.rgb,
                    _SecondaryColor.rgb,
                    secondaryMix);

                return half4(mixedColor * brightness, alpha);
            }
            ENDHLSL
        }
    }
}
