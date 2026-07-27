Shader "Loaded/Combat Screen Pulse"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        [HDR] _PulseColor("Pulse Color", Color) = (1.0, 0.35, 0.08, 1.0)
        _Progress("Progress", Range(0.0, 1.0)) = 1.0
        _Intensity("Intensity", Range(0.0, 5.0)) = 1.0
        _Center("Viewport Center", Vector) = (0.5, 0.5, 0.0, 0.0)
        _Aspect("Screen Aspect", Float) = 1.7778
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ScreenPulse"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

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
                half4 _PulseColor;
                float _Progress;
                float _Intensity;
                float4 _Center;
                float _Aspect;
            CBUFFER_END

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
                half spriteAlpha = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv).a;
                float2 delta = input.uv - _Center.xy;
                delta.x *= max(0.1, _Aspect);
                float distanceFromCenter = length(delta);
                float radius = lerp(0.015, 0.72, _Progress);
                float ringWidth = lerp(0.065, 0.012, _Progress);
                float ring = 1.0 - smoothstep(
                    ringWidth,
                    ringWidth * 1.9,
                    abs(distanceFromCenter - radius));

                float centerFlash = exp(-distanceFromCenter * 14.0)
                    * pow(saturate(1.0 - _Progress), 3.4);
                float horizontal = exp(-abs(delta.y) * 80.0)
                    * exp(-abs(delta.x) * 2.8)
                    * pow(saturate(1.0 - _Progress), 2.0);
                float fade = pow(saturate(1.0 - _Progress), 1.25);
                float energy = (
                    ring * 0.72
                    + centerFlash * 1.65
                    + horizontal * 0.7)
                    * fade
                    * _Intensity
                    * input.color.a
                    * spriteAlpha;
                half3 color = lerp(
                    _PulseColor.rgb,
                    1.0.xxx,
                    saturate(centerFlash + horizontal * 0.5));

                return half4(color * energy, saturate(energy));
            }
            ENDHLSL
        }
    }
}
