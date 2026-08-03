Shader "Loaded/UI/Enemy Queue Ready Flame"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        [HDR] _EmberColor("Ember Color", Color) = (1.0, 0.04, 0.0, 1.0)
        [HDR] _FlameColor("Flame Color", Color) = (1.0, 0.32, 0.015, 1.0)
        [HDR] _HotColor("Hot Core Color", Color) = (1.0, 1.0, 0.35, 1.0)
        _Intensity("Intensity", Range(0.0, 8.0)) = 2.8
        _BorderWidth("Border Width", Range(0.005, 0.3)) = 0.065
        _FlameReach("Flame Reach", Range(0.0, 0.35)) = 0.10
        _Softness("Edge Softness", Range(0.001, 0.15)) = 0.022
        _Speed("Flame Speed", Range(0.0, 8.0)) = 2.2
        _Scale("Flame Scale", Range(1.0, 40.0)) = 14.0
        _PulseAmount("Pulse Amount", Range(0.0, 1.0)) = 0.20

        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+120"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
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

        Pass
        {
            Name "EnemyQueueReadyFlame"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            ZTest [unity_GUIZTestMode]
            ColorMask [_ColorMask]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

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
                float4 positionOS : TEXCOORD1;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _EmberColor;
                half4 _FlameColor;
                half4 _HotColor;
                float4 _ClipRect;
                float _Intensity;
                float _BorderWidth;
                float _FlameReach;
                float _Softness;
                float _Speed;
                float _Scale;
                float _PulseAmount;
            CBUFFER_END

            float Hash(float2 value)
            {
                return frac(sin(dot(value, float2(127.1, 311.7)))
                    * 43758.5453);
            }

            float Noise(float2 value)
            {
                float2 cell = floor(value);
                float2 local = frac(value);
                local = local * local * (3.0 - 2.0 * local);

                float lower = lerp(
                    Hash(cell),
                    Hash(cell + float2(1.0, 0.0)),
                    local.x);
                float upper = lerp(
                    Hash(cell + float2(0.0, 1.0)),
                    Hash(cell + 1.0),
                    local.x);
                return lerp(lower, upper, local.y);
            }

            float FractalNoise(float2 value)
            {
                float result = Noise(value) * 0.58;
                result += Noise(value * 2.03 + 9.17) * 0.28;
                result += Noise(value * 4.01 + 21.53) * 0.14;
                return result;
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionHCS =
                    TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float time = _Time.y * _Speed;
                float uvXGradient = length(float2(
                    ddx(input.uv.x),
                    ddy(input.uv.x)));
                float uvYGradient = length(float2(
                    ddx(input.uv.y),
                    ddy(input.uv.y)));
                float aspect = uvYGradient
                    / max(uvXGradient, 0.00001);
                float2 edge = min(input.uv, 1.0 - input.uv);
                float edgeDistance = min(edge.x * aspect, edge.y);

                float2 flameCoordinate = float2(
                    input.uv.x * _Scale,
                    input.uv.y * _Scale / max(aspect, 0.001));
                float movingNoise = FractalNoise(
                    flameCoordinate + float2(time * 0.37, -time));
                float detailNoise = Noise(
                    flameCoordinate * 2.7 + float2(-time, time * 0.43));
                float lickLength = _FlameReach
                    * saturate(movingNoise * 1.3 - 0.22);
                float animatedWidth = _BorderWidth
                    * lerp(0.72, 1.35, detailNoise);

                float body = 1.0 - smoothstep(
                    animatedWidth + lickLength,
                    animatedWidth + lickLength + _Softness,
                    edgeDistance);
                float hotCore = 1.0 - smoothstep(
                    _BorderWidth * 0.34,
                    _BorderWidth * 0.72 + _Softness,
                    edgeDistance);
                float emberRim = 1.0 - smoothstep(
                    animatedWidth + lickLength + _Softness,
                    animatedWidth + lickLength + _Softness * 3.5,
                    edgeDistance);

                float breakup = smoothstep(0.12, 0.48, movingNoise)
                    * lerp(0.72, 1.0, detailNoise);
                float pulse = lerp(
                    1.0 - _PulseAmount,
                    1.0,
                    0.5 + 0.5 * sin(time * 3.1));
                float flameMask = saturate(
                    body * breakup + hotCore + emberRim * 0.32);

                half3 color = lerp(
                    _EmberColor.rgb,
                    _FlameColor.rgb,
                    saturate(body * 1.4));
                color = lerp(color, _HotColor.rgb, hotCore);
                color *= _Intensity * pulse * input.color.rgb;
                float alpha = flameMask * pulse * input.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                float2 inside = step(
                    _ClipRect.xy,
                    input.positionOS.xy)
                    * step(
                        input.positionOS.xy,
                        _ClipRect.zw);
                alpha *= inside.x * inside.y;
                #endif

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
