Shader "Loaded/UI/Cylinder Tempo Solar Resolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _HotColor ("Hot Core", Color) = (4.5,0.45,0.12,1)
        [HDR] _EmberColor ("Ember Edge", Color) = (1.8,0.015,0.005,1)
        _Resolve ("Resolve", Range(0,1)) = 0
        _Pulse ("Pulse", Range(0,1)) = 0
        _NoiseScale ("Noise Scale", Range(1,30)) = 11
        _NoiseSpeed ("Noise Speed", Range(0,8)) = 2.6
        _EdgeWidth ("Resolve Edge Width", Range(0.005,0.25)) = 0.075

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "SolarResolve"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float4 _HotColor;
            float4 _EmberColor;
            float _Resolve;
            float _Pulse;
            float _NoiseScale;
            float _NoiseSpeed;
            float _EdgeWidth;

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float SmoothNoise(float2 value)
            {
                float2 cell = floor(value);
                float2 fraction = frac(value);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1, 0));
                float c = Hash21(cell + float2(0, 1));
                float d = Hash21(cell + float2(1, 1));
                return lerp(lerp(a, b, fraction.x),
                    lerp(c, d, fraction.x), fraction.y);
            }

            v2f vert(appdata_t input)
            {
                v2f output;
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, input.texcoord);
                float2 centered = input.texcoord - 0.5;
                float radius = length(centered) * 1.42;
                float time = _Time.y * _NoiseSpeed;
                float noise = SmoothNoise(
                    input.texcoord * _NoiseScale
                    + float2(time * 0.31, -time * 0.47));
                float fineNoise = SmoothNoise(
                    input.texcoord * (_NoiseScale * 2.17)
                    + float2(-time * 0.63, time * 0.52));
                float resolveMetric = radius
                    + (noise - 0.5) * 0.2
                    + (fineNoise - 0.5) * 0.08;
                float threshold = _Resolve * 1.18 - 0.11;
                float reveal = 1.0 - smoothstep(
                    threshold,
                    threshold + _EdgeWidth,
                    resolveMetric);
                float edge = saturate(1.0
                    - abs(resolveMetric - threshold)
                    / max(_EdgeWidth * 1.8, 0.001));
                edge *= step(0.001, _Resolve) * step(_Resolve, 0.999);

                float core = saturate(1.0 - radius * 1.3);
                core = pow(core, 1.45);
                float flame = saturate(
                    noise * 0.7 + fineNoise * 0.45 + core * 0.9);
                float3 solarColor = lerp(
                    _EmberColor.rgb,
                    _HotColor.rgb,
                    saturate(core + flame * 0.42));
                solarColor *= 1.0 + edge * 2.2 + _Pulse * 1.8;

                float3 emptyColor = sprite.rgb * input.color.rgb * 0.13;
                float3 resolvedColor = solarColor * input.color.rgb;
                float3 color = lerp(emptyColor, resolvedColor, reveal);
                float alpha = sprite.a * input.color.a;
                color *= alpha;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(
                    input.worldPosition.xy,
                    _ClipRect);
                color *= UnityGet2DClipping(
                    input.worldPosition.xy,
                    _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
