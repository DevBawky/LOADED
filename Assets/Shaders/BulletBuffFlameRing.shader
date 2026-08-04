Shader "Loaded/UI/Bullet Buff Flame Ring"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        [HDR] _EmberColor("Ember Color", Color) = (0.10, 0.8, 0.65, 1)
        [HDR] _FlameColor("Flame Color", Color) = (0.0, 1.0, 0.9, 1)
        [HDR] _HotColor("Hot Core Color", Color) = (1, 1, 1, 1)
        _Intensity("Intensity", Range(0, 8)) = 2.4
        _BorderWidth("Border Width", Range(0.005, 0.2)) = 0.052
        _FlameReach("Flame Reach", Range(0, 0.15)) = 0.045
        _Softness("Edge Softness", Range(0.001, 0.08)) = 0.012
        _Speed("Flame Speed", Range(0, 8)) = 2.2
        _Scale("Flame Scale", Range(1, 40)) = 14
        _PulseAmount("Pulse Amount", Range(0, 1)) = 0.2

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
            Name "BulletBuffFlameRing"
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
                float lower = lerp(Hash(cell), Hash(cell + float2(1, 0)), local.x);
                float upper = lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), local.x);
                return lerp(lower, upper, local.y);
            }

            float FractalNoise(float2 value)
            {
                return Noise(value) * 0.58
                    + Noise(value * 2.03 + 9.17) * 0.28
                    + Noise(value * 4.01 + 21.53) * 0.14;
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float uvXGradient = length(float2(ddx(input.uv.x), ddy(input.uv.x)));
                float uvYGradient = length(float2(ddx(input.uv.y), ddy(input.uv.y)));
                centered.x *= uvYGradient / max(uvXGradient, 0.00001);

                float radius = length(centered) * 0.5;
                float angle = atan2(centered.y, centered.x) / 6.2831853 + 0.5;
                float time = _Time.y * _Speed;
                float2 flameUv = float2(angle * _Scale, radius * _Scale);
                float broadNoise = FractalNoise(flameUv + float2(-time * 0.35, -time));
                float detailNoise = Noise(flameUv * 2.7 + float2(time, -time * 0.43));

                // The cylinder icons' metal rim sits at roughly 89% of their
                // square. Keep the hot core on that rim and grow flame only
                // toward the transparent outside, so the art stays readable.
                float rimRadius = 0.445;
                float lick = _FlameReach * saturate(broadNoise * 1.35 - 0.25);
                float halfWidth = _BorderWidth * 0.5;
                float animatedOuterWidth = halfWidth
                    * lerp(0.72, 1.3, detailNoise) + lick;
                float innerEdge = smoothstep(
                    rimRadius - halfWidth - _Softness,
                    rimRadius - halfWidth,
                    radius);
                float outerEdge = 1.0 - smoothstep(
                    rimRadius + animatedOuterWidth,
                    rimRadius + animatedOuterWidth + _Softness,
                    radius);
                float body = innerEdge * outerEdge;
                float signedRim = abs(radius - rimRadius);
                float hotCore = 1.0 - smoothstep(
                    halfWidth * 0.18,
                    halfWidth * 0.62 + _Softness,
                    signedRim);
                float breakup = smoothstep(0.13, 0.48, broadNoise)
                    * lerp(0.75, 1.0, detailNoise);
                float pulse = lerp(1.0 - _PulseAmount, 1.0,
                    0.5 + 0.5 * sin(time * 3.1));
                float mask = saturate(body * breakup + hotCore);

                half3 color = lerp(_EmberColor.rgb, _FlameColor.rgb, body);
                color = lerp(color, _HotColor.rgb, hotCore);
                color *= _Intensity * pulse * input.color.rgb;
                float alpha = mask * pulse * input.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                float2 inside = step(_ClipRect.xy, input.positionOS.xy)
                    * step(input.positionOS.xy, _ClipRect.zw);
                alpha *= inside.x * inside.y;
                #endif

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
