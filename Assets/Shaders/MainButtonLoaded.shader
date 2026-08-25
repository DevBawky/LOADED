Shader "Loaded/UI/Main Button"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1, 1, 1, 1)

        [Header(Loaded Palette)]
        _PlateTop("Burnished Brass", Color) = (0.72, 0.49, 0.24, 1)
        _PlateBottom("Scorched Brass", Color) = (0.42, 0.22, 0.075, 1)
        _BorderColor("Gunmetal Border", Color) = (0.075, 0.032, 0.014, 1)
        [HDR] _HoverColor("Hover Ember", Color) = (1.0, 0.21, 0.015, 1)
        [HDR] _ClickColor("Click Flash", Color) = (1.0, 0.78, 0.27, 1)

        [Header(Shape)]
        _BorderWidth("Border Width", Range(0.01, 0.12)) = 0.038
        _Chamfer("Corner Chamfer", Range(0.01, 0.2)) = 0.075
        _GrainStrength("Static Wear", Range(0, 0.25)) = 0.075

        [HideInInspector] _Hover("Hover", Range(0, 1)) = 0
        [HideInInspector] _Press("Press", Range(0, 1)) = 0
        [HideInInspector] _Click("Click", Range(0, 1)) = 0
        [HideInInspector] _ClickProgress("Click Progress", Range(0, 1)) = 1
        [HideInInspector] _ClickOrigin("Click Origin", Vector) = (0.5, 0.5, 0, 0)
        [HideInInspector] _Aspect("Rect Aspect", Float) = 3
        [HideInInspector] _UnscaledTime("Unscaled Time", Float) = 0
        [HideInInspector] _Disabled("Disabled", Range(0, 1)) = 0
        [HideInInspector] _InstanceTint("Instance Tint", Color) = (1, 1, 1, 1)

        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
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
            Name "MainButton"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest [unity_GUIZTestMode]
            ColorMask [_ColorMask]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

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
                float4 positionOS : TEXCOORD1;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _PlateTop;
                half4 _PlateBottom;
                half4 _BorderColor;
                half4 _HoverColor;
                half4 _ClickColor;
                half4 _InstanceTint;
                float4 _ClickOrigin;
                float4 _ClipRect;
                float _BorderWidth;
                float _Chamfer;
                float _GrainStrength;
                float _Hover;
                float _Press;
                float _Click;
                float _ClickProgress;
                float _Aspect;
                float _UnscaledTime;
                float _Disabled;
            CBUFFER_END

            float Hash(float2 value)
            {
                return frac(sin(dot(value, float2(127.1, 311.7)))
                    * 43758.5453);
            }

            float ChamferedPlateDistance(
                float2 uv,
                float aspect,
                float inset,
                float chamfer)
            {
                float2 position = abs(float2(
                    (uv.x - 0.5) * aspect,
                    uv.y - 0.5));
                float2 halfSize = float2(aspect * 0.5, 0.5) - inset;
                float2 margin = halfSize - position;
                float corner = margin.x + margin.y
                    - max(0.008, chamfer - inset * 0.55);
                return min(min(margin.x, margin.y), corner);
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(
                    input.positionOS.xyz);
                output.positionOS = input.positionOS;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float aspect = max(1.0, _Aspect);
                float outerDistance = ChamferedPlateDistance(
                    input.uv,
                    aspect,
                    0.0,
                    _Chamfer);
                float innerDistance = ChamferedPlateDistance(
                    input.uv,
                    aspect,
                    _BorderWidth,
                    _Chamfer);
                float antialiasWidth = max(fwidth(outerDistance), 0.0012);
                float outerMask = smoothstep(
                    -antialiasWidth,
                    antialiasWidth,
                    outerDistance);
                float innerMask = smoothstep(
                    -antialiasWidth,
                    antialiasWidth,
                    innerDistance);
                float borderMask = saturate(outerMask - innerMask);

                float verticalShade = smoothstep(0.04, 0.96, input.uv.y);
                half3 plate = lerp(
                    _PlateBottom.rgb,
                    _PlateTop.rgb,
                    verticalShade);
                plate *= _InstanceTint.rgb;
                float grain = Hash(floor(input.uv
                    * float2(128.0, 37.0)));
                float longScratch = Hash(float2(
                    floor(input.uv.y * 43.0),
                    floor(input.uv.x * 7.0)));
                plate *= 1.0 + (grain - 0.5) * _GrainStrength;
                plate *= 1.0 - step(0.935, longScratch)
                    * _GrainStrength * 0.42;

                float insetShade = 1.0 - smoothstep(
                    0.0,
                    _BorderWidth * 3.4,
                    max(innerDistance, 0.0));
                plate *= 1.0 - insetShade * 0.17;
                plate *= lerp(1.0, 0.78, _Press);

                float scanPosition = frac(_UnscaledTime * 0.48) * 1.55
                    - 0.28;
                float scanDistance = abs(
                    input.uv.x + input.uv.y * 0.16 - scanPosition);
                float hoverScan = 1.0 - smoothstep(
                    0.025,
                    0.16,
                    scanDistance);
                float hoverPulse = 0.82 + 0.18 * sin(
                    _UnscaledTime * 7.0 + input.uv.x * 11.0);
                float emberNoise = step(
                    0.97,
                    Hash(floor(input.uv * float2(94.0, 21.0))
                        + floor(_UnscaledTime * 8.0)));

                half3 color = plate * innerMask;
                half3 border = _BorderColor.rgb;
                border += _HoverColor.rgb
                    * _Hover
                    * (0.48 + hoverPulse * 0.34 + emberNoise * 0.22);
                color += border * borderMask;
                color += _HoverColor.rgb
                    * innerMask
                    * _Hover
                    * (0.055 + hoverScan * 0.27);

                float2 clickDelta = float2(
                    (input.uv.x - _ClickOrigin.x) * aspect,
                    input.uv.y - _ClickOrigin.y);
                float clickDistance = length(clickDelta);
                float clickRadius = _ClickProgress
                    * (aspect * 0.2 + 0.48);
                float clickRing = 1.0 - smoothstep(
                    0.015,
                    0.085,
                    abs(clickDistance - clickRadius));
                float clickCore = 1.0 - smoothstep(
                    0.0,
                    0.32 + _ClickProgress * 0.2,
                    clickDistance);
                color += _ClickColor.rgb
                    * innerMask
                    * _Click
                    * (clickRing * 1.25 + clickCore * 0.52);

                float luminance = dot(color, half3(0.299, 0.587, 0.114));
                color = lerp(
                    color,
                    half3(luminance, luminance, luminance) * 0.55,
                    _Disabled);

                half4 textureColor = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv);
                float alpha = outerMask
                    * textureColor.a
                    * input.color.a
                    * _Color.a;
                color *= input.color.rgb * _Color.rgb;

                #ifdef UNITY_UI_CLIP_RECT
                float2 inside = step(
                    _ClipRect.xy,
                    input.positionOS.xy)
                    * step(
                        input.positionOS.xy,
                        _ClipRect.zw);
                alpha *= inside.x * inside.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
