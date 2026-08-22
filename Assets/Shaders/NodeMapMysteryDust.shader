Shader "Loaded/UI/Node Map Mystery Dust"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1, 1, 1, 1)

        [Header(Map Palette)]
        _PaperLight("Sun-Bleached Sand", Color) = (0.50, 0.34, 0.17, 1)
        _PaperDark("Weathered Earth", Color) = (0.105, 0.062, 0.032, 1)
        _InkColor("Contour Ink", Color) = (0.055, 0.028, 0.012, 1)
        _DustColor("Sand Fog", Color) = (0.72, 0.49, 0.24, 1)

        [Header(Map Detail)]
        _TerrainScale("Terrain Scale", Range(0.5, 12)) = 3.4
        _TerrainContrast("Terrain Contrast", Range(0, 2)) = 0.85
        _ContourFrequency("Contour Frequency", Range(1, 24)) = 9
        _ContourStrength("Contour Strength", Range(0, 1)) = 0.28
        _GrainStrength("Dry Grain", Range(0, 0.3)) = 0.075

        [Header(Sand Fog)]
        _DustScale("Dust Scale", Range(0.5, 12)) = 2.8
        _DustCoverage("Dust Coverage", Range(0, 1)) = 0.66
        _DustContrast("Dust Contrast", Range(0.25, 4)) = 1.25
        _DustOpacity("Dust Opacity", Range(0, 1)) = 0.58
        _DustSpeed("Dust Speed", Range(0, 2)) = 0.16
        _DustDirection("Dust Direction", Vector) = (1, 0.18, 0, 0)
        _DustDistortion("Dust Distortion", Range(0, 2)) = 0.72

        [Header(Atmosphere)]
        _VignetteStrength("Mysterious Vignette", Range(0, 1)) = 0.72
        _VignetteSoftness("Vignette Softness", Range(0.05, 1)) = 0.48
        _OverallAlpha("Overall Alpha", Range(0, 1)) = 1

        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip(
            "Use Alpha Clip",
            Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
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
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "NodeMapMysteryDust"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            fixed4 _PaperLight;
            fixed4 _PaperDark;
            fixed4 _InkColor;
            fixed4 _DustColor;
            float4 _ClipRect;
            float _TerrainScale;
            float _TerrainContrast;
            float _ContourFrequency;
            float _ContourStrength;
            float _GrainStrength;
            float _DustScale;
            float _DustCoverage;
            float _DustContrast;
            float _DustOpacity;
            float _DustSpeed;
            float4 _DustDirection;
            float _DustDistortion;
            float _VignetteStrength;
            float _VignetteSoftness;
            float _OverallAlpha;

            float Hash(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
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
                float result = Noise(value) * 0.54;
                result += Noise(value * 2.03 + 17.13) * 0.27;
                result += Noise(value * 4.07 - 9.41) * 0.13;
                result += Noise(value * 8.11 + 31.77) * 0.06;
                return result;
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 Fragment(Varyings input) : SV_Target
            {
                fixed4 source = (tex2D(_MainTex, input.texcoord)
                    + _TextureSampleAdd) * input.color;
                float sourceLuminance = dot(
                    source.rgb,
                    float3(0.2126, 0.7152, 0.0722));

                float uvXGradient = length(float2(
                    ddx(input.texcoord.x),
                    ddy(input.texcoord.x)));
                float uvYGradient = length(float2(
                    ddx(input.texcoord.y),
                    ddy(input.texcoord.y)));
                float aspect = uvYGradient / max(uvXGradient, 0.00001);
                float2 mapPosition = input.texcoord - 0.5;
                mapPosition.x *= aspect;

                float terrain = FractalNoise(
                    mapPosition * _TerrainScale + float2(3.7, -1.9));
                float weathering = saturate(
                    (terrain - 0.5) * _TerrainContrast + 0.5);
                float verticalAge = saturate(
                    0.62 - input.texcoord.y * 0.28 + terrain * 0.3);
                fixed3 mapColor = lerp(
                    _PaperDark.rgb,
                    _PaperLight.rgb,
                    saturate(weathering * 0.72 + verticalAge * 0.28));

                float contourPhase = frac(terrain * _ContourFrequency);
                float contourDistance = min(
                    contourPhase,
                    1.0 - contourPhase);
                float contourDerivative = max(
                    fwidth(terrain * _ContourFrequency),
                    0.0025);
                float contour = 1.0 - smoothstep(
                    contourDerivative * 0.65,
                    contourDerivative * 1.65,
                    contourDistance);
                float contourBreakup = smoothstep(
                    0.18,
                    0.72,
                    Noise(mapPosition * 18.0 + terrain * 4.0));
                contour *= contourBreakup * _ContourStrength;
                mapColor = lerp(mapColor, _InkColor.rgb, saturate(contour));

                float time = _Time.y * _DustSpeed;
                float2 windDirection = _DustDirection.xy;
                float windLength = max(length(windDirection), 0.0001);
                windDirection /= windLength;
                float2 slowWind = windDirection * time;
                float distortion = FractalNoise(
                    mapPosition * 1.35
                    + slowWind * 0.32
                    + float2(-8.2, 4.1));
                float2 dustCoordinate = mapPosition * _DustScale;
                dustCoordinate += slowWind;
                dustCoordinate.y += (distortion - 0.5) * _DustDistortion;
                dustCoordinate.x += sin(
                    mapPosition.y * 4.0 + time * 0.7) * 0.16;

                float broadDust = FractalNoise(dustCoordinate);
                float fineDust = FractalNoise(
                    dustCoordinate * 1.83
                    + float2(-time * 0.41, time * 0.17)
                    + 13.4);
                float dustField = broadDust * 0.72 + fineDust * 0.28;
                float dustThreshold = lerp(0.83, 0.17, _DustCoverage);
                float dustWidth = 0.28 / max(_DustContrast, 0.25);
                float dust = smoothstep(
                    dustThreshold,
                    dustThreshold + dustWidth,
                    dustField);
                float lowFog = (1.0 - smoothstep(
                    0.08,
                    0.95,
                    input.texcoord.y))
                    * smoothstep(0.3, 0.76, broadDust)
                    * 0.28;
                dust = saturate(dust + lowFog) * _DustOpacity;
                fixed3 dustyColor = lerp(
                    mapColor,
                    _DustColor.rgb,
                    dust * 0.72);
                dustyColor += _DustColor.rgb * dust * dust * 0.13;

                float2 edgePosition = abs(input.texcoord * 2.0 - 1.0);
                float edgeDistance = max(edgePosition.x, edgePosition.y);
                float vignetteStart = saturate(1.0 - _VignetteSoftness);
                float vignette = smoothstep(
                    vignetteStart,
                    1.0,
                    edgeDistance) * _VignetteStrength;
                dustyColor = lerp(
                    dustyColor,
                    _PaperDark.rgb * 0.34,
                    vignette);

                float grain = Hash(floor(input.vertex.xy)) - 0.5;
                dustyColor += grain * _GrainStrength;
                dustyColor *= lerp(0.72, 1.08, sourceLuminance);

                fixed4 color = fixed4(
                    saturate(dustyColor),
                    source.a * _OverallAlpha);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(
                    input.worldPosition.xy,
                    _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
