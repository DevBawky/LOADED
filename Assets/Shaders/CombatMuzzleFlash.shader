Shader "Loaded/Combat Muzzle Flash"
{
    Properties
    {
        [HDR] _PrimaryColor("Primary Color", Color) = (1.0, 0.25, 0.05, 1.0)
        [HDR] _SecondaryColor("Secondary Color", Color) = (1.0, 0.8, 0.25, 1.0)
        _Progress("Progress", Range(0.0, 1.0)) = 0.0
        _Intensity("Intensity", Range(0.0, 8.0)) = 2.0
        _Direction("Direction", Float) = 1.0
        _RayCount("Ray Count", Range(3.0, 12.0)) = 7.0
        _EffectMode("Effect Mode", Range(0.0, 1.0)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ProceduralMuzzleFlash"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha One
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
                half4 _PrimaryColor;
                half4 _SecondaryColor;
                float _Progress;
                float _Intensity;
                float _Direction;
                float _RayCount;
                // 0 renders the muzzle burst; 1 renders the contact flare.
                float _EffectMode;
            CBUFFER_END

            float Hash(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
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
                float2 centered = input.uv * 2.0 - 1.0;
                centered.x *= 1.18;
                float distanceFromCenter = length(centered);
                float angle = atan2(centered.y, centered.x);
                float fade = pow(saturate(1.0 - _Progress), 1.7);

                float angleWave = cos(angle * _RayCount + _Progress * 1.7);
                float rays = pow(saturate(angleWave * 0.5 + 0.5), 18.0);
                rays *= 1.0 - smoothstep(0.12, 1.02, distanceFromCenter);
                rays *= smoothstep(0.015, 0.16, distanceFromCenter);

                float forward = centered.x * sign(_Direction);
                float coneWidth = 0.12 + saturate(forward) * 0.52;
                float cone = smoothstep(-0.18, 0.12, forward)
                    * (1.0 - smoothstep(
                        coneWidth * 0.55,
                        coneWidth,
                        abs(centered.y)))
                    * (1.0 - smoothstep(0.28, 1.08, forward));

                float horizontalStreak = exp(-abs(centered.y) * 32.0)
                    * (1.0 - smoothstep(0.05, 1.05, abs(centered.x)));
                float verticalStreak = exp(-abs(centered.x) * 42.0)
                    * (1.0 - smoothstep(0.04, 0.68, abs(centered.y)))
                    * 0.62;
                float core = exp(-distanceFromCenter * 12.0);

                float ringRadius = lerp(0.12, 0.74, _Progress);
                float ringWidth = lerp(0.11, 0.025, _Progress);
                float ring = 1.0 - smoothstep(
                    ringWidth,
                    ringWidth * 2.1,
                    abs(distanceFromCenter - ringRadius));
                ring *= 1.0 - smoothstep(0.0, 1.0, _Progress);

                float noise = Hash(
                    floor(centered * 18.0 + _Progress * 13.0));
                float breakup = lerp(0.72, 1.18, noise);
                float muzzleEnergy = core * 1.8
                    + horizontalStreak * 1.35
                    + verticalStreak
                    + rays * breakup * 1.2
                    + cone * breakup * 1.6
                    + ring * 0.55;
                muzzleEnergy *= fade;

                float impactRadius = lerp(0.06, 0.82, _Progress);
                float impactWidth = lerp(0.14, 0.025, _Progress);
                float impactRing = 1.0 - smoothstep(
                    impactWidth,
                    impactWidth * 2.0,
                    abs(distanceFromCenter - impactRadius));
                float arcNoise = sin(angle * 5.0 + _Progress * 5.0) * 0.62
                    + sin(angle * 11.0 - _Progress * 3.0) * 0.38;
                float brokenArcs = smoothstep(-0.22, 0.48, arcNoise);
                float forwardArc = saturate(
                    0.58 + cos(angle) * sign(_Direction) * 0.52);
                float impactCore = exp(-distanceFromCenter * 10.0)
                    * pow(saturate(1.0 - _Progress), 2.6);
                float impactSlash = exp(
                        -abs(centered.y - centered.x * sign(_Direction) * 0.08)
                        * 26.0)
                    * (1.0 - smoothstep(0.04, 0.92, abs(centered.x)))
                    * pow(saturate(1.0 - _Progress), 1.8);
                float impactFade = pow(saturate(1.0 - _Progress), 0.92);
                float impactEnergy = impactCore * 1.7
                    + impactSlash * 0.85
                    + impactRing
                        * brokenArcs
                        * lerp(0.52, 1.45, forwardArc)
                        * 1.35;
                impactEnergy *= impactFade;

                float effectMode = saturate(_EffectMode);
                float energy = lerp(
                    muzzleEnergy,
                    impactEnergy,
                    effectMode) * input.color.a;

                float hotMix = saturate(
                    core * 1.6
                    + horizontalStreak
                    + verticalStreak * 0.8);
                half3 muzzleColor = lerp(
                    _PrimaryColor.rgb,
                    _SecondaryColor.rgb,
                    hotMix);
                muzzleColor = lerp(muzzleColor, 1.0.xxx, core * 0.62);
                half3 impactColor = lerp(
                    _PrimaryColor.rgb,
                    1.0.xxx,
                    impactCore * 0.28);
                half3 flameColor = lerp(
                    muzzleColor,
                    impactColor,
                    effectMode);
                float effectFade = lerp(fade, impactFade, effectMode);
                float alpha = saturate(energy) * effectFade;
                half3 outputColor = flameColor
                    * energy
                    * _Intensity
                    * input.color.rgb;

                return half4(outputColor, alpha);
            }
            ENDHLSL
        }
    }
}
