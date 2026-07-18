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
        _CoreOpacity("Core Opacity", Range(0.0, 1.0)) = 0.82
        _SmokeBrightness("Smoke Brightness", Range(0.5, 3.0)) = 1.35
        _SparkDensity("Spark Density", Range(0.0, 1.0)) = 0.86
        _SparkIntensity("Spark Intensity", Range(0.0, 5.0)) = 2.7
        _OverallAlpha("Overall Alpha", Range(0.0, 1.0)) = 0.9

        [Header(Smoke Detail)]
        _SmokeBreakup("Smoke Breakup", Range(0.0, 1.0)) = 0.38
        _EndSoftness("End Softness", Range(0.001, 0.35)) = 0.08

        [Header(Color Mix)]
        [HDR] _PrimaryColor("Primary Color", Color) = (1.0, 0.2, 0.1, 1.0)
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
                float _CoreOpacity;
                float _SmokeBrightness;
                float _SparkDensity;
                float _SparkIntensity;
                float _OverallAlpha;
                float _SmokeBreakup;
                float _EndSoftness;
                half4 _PrimaryColor;
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

                float centeredY = input.uv.y * 2.0 - 1.0;
                float centerWarp = (domainNoise - 0.5) * 0.28
                    + (broadNoise - 0.5) * _EdgeDistortion;
                float warpedDistance = abs(centeredY + centerWarp);
                float smokeRadius = lerp(0.58, 0.88, broadNoise);
                float edgeFeather = max(0.035, _EdgeSoftness * 0.32);
                float smokeMask = 1.0 - smoothstep(
                    smokeRadius - edgeFeather,
                    smokeRadius + edgeFeather,
                    warpedDistance);
                float densityNoise = saturate(
                    broadNoise * 0.52
                    + fineNoise * 0.3
                    + domainNoise * 0.18);
                float breakupEnd = _SmokeBreakup
                    + max(0.02, (1.0 - _SmokeBreakup) * 0.28);
                float breakupMask = smoothstep(
                    _SmokeBreakup,
                    breakupEnd,
                    densityNoise + smokeMask * 0.32);
                float coreMask = 1.0 - smoothstep(
                    _CoreWidth,
                    min(1.0, _CoreWidth + 0.24),
                    warpedDistance);
                float tailMask = lerp(
                    1.0 - _TailTaper,
                    1.0,
                    smoothstep(0.0, 0.4, input.uv.x));
                float startJitter = (domainNoise - 0.5)
                    * _EndSoftness * 1.8;
                float endJitter = (fineNoise - 0.5)
                    * _EndSoftness * 1.8;
                float irregularEndMask = smoothstep(
                    0.0,
                    _EndSoftness,
                    input.uv.x + startJitter)
                    * smoothstep(
                        0.0,
                        _EndSoftness,
                        1.0 - input.uv.x + endJitter);
                float boundaryMask = smoothstep(0.0, 0.012, input.uv.x)
                    * smoothstep(0.0, 0.012, 1.0 - input.uv.x);
                float endMask = irregularEndMask * boundaryMask;

                float wispDistance = abs(
                    centeredY
                    + (fineNoise - 0.5) * _EdgeDistortion * 1.8);
                float wispShape = 1.0 - smoothstep(
                    0.48,
                    1.05,
                    wispDistance);
                float wispNoise = smoothstep(
                    0.44,
                    0.78,
                    densityNoise + domainNoise * 0.18);
                float wisps = wispShape
                    * wispNoise
                    * (1.0 - coreMask * 0.75);

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
                    max(1.001, _SparkDensity + 0.001),
                    sparkSeed) * sparkShape * max(smokeMask, wisps) * endMask;

                float smokeAlpha = smokeMask
                    * lerp(0.52, 1.0, breakupMask);
                float alpha = saturate(max(
                        smokeAlpha + wisps * 0.28 + sparks * 0.75,
                        coreMask * _CoreOpacity))
                    * tailMask
                    * endMask
                    * _OverallAlpha
                    * input.color.a;

                float flowingColorMix = saturate(
                    0.16
                    + broadNoise * 0.42
                    + fineNoise * 0.28)
                    * _ColorBlend
                    * _SecondaryColor.a;
                half3 smokeColor = lerp(
                    _PrimaryColor.rgb,
                    _SecondaryColor.rgb,
                    flowingColorMix);
                half3 coreColor = lerp(
                    _SecondaryColor.rgb,
                    _PrimaryColor.rgb,
                    0.76);
                half3 mixedColor = lerp(
                    smokeColor,
                    coreColor,
                    coreMask);
                mixedColor = lerp(
                    mixedColor,
                    _SecondaryColor.rgb * 1.2,
                    saturate(sparks));

                float brightness = _SmokeBrightness
                    * lerp(0.72, 1.12, densityNoise)
                    + coreMask * _CoreIntensity
                    + sparks * _SparkIntensity;

                return half4(mixedColor * brightness, alpha);
            }
            ENDHLSL
        }
    }
}
