Shader "Loaded/Enemy Warning Flow"
{
    Properties
    {
        [Header(Color)]
        [HDR] _BaseColor("Base Color", Color) = (0.35, 0.02, 0.01, 0.55)
        [HDR] _GridColor("Grid Color", Color) = (1.0, 0.08, 0.01, 0.9)
        [HDR] _BeamColor("Moving Beam Color", Color) = (1.0, 0.75, 0.12, 1.0)
        _BaseIntensity("Base Intensity", Range(0.0, 5.0)) = 0.8
        _OverallAlpha("Overall Alpha", Range(0.0, 1.0)) = 0.9

        [Header(Moving Grid)]
        _GridColumns("Grid Columns", Range(1.0, 64.0)) = 14.0
        _GridRows("Grid Rows", Range(1.0, 12.0)) = 2.0
        _GridLineWidth("Grid Line Width", Range(0.005, 0.49)) = 0.08
        _GridSoftness("Grid Softness", Range(0.001, 0.25)) = 0.025
        _GridIntensity("Grid Intensity", Range(0.0, 8.0)) = 2.2
        _GridScrollSpeed("Grid Scroll Speed", Float) = 1.6
        _GridSlant("Grid Slant", Range(-4.0, 4.0)) = 0.35

        [Header(Moving Beam)]
        _BeamRepeat("Beam Repeat", Range(1.0, 16.0)) = 3.0
        _BeamWidth("Beam Width", Range(0.005, 0.95)) = 0.18
        _BeamSoftness("Beam Softness", Range(0.001, 0.5)) = 0.12
        _BeamIntensity("Beam Intensity", Range(0.0, 10.0)) = 4.0
        _BeamScrollSpeed("Beam Scroll Speed", Float) = 0.8
        _BeamSlant("Beam Slant", Range(-4.0, 4.0)) = 0.8

        [Header(Pulse)]
        _PulseFrequency("Pulse Frequency", Range(0.0, 12.0)) = 2.0
        _PulseAmount("Pulse Amount", Range(0.0, 1.0)) = 0.18

        [Header(Shape)]
        _EdgeSoftness("Side Edge Softness", Range(0.001, 1.0)) = 0.22
        _EndFade("Start End Fade", Range(0.0, 0.5)) = 0.04

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 10
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth Test", Float) = 8
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
            Name "EnemyWarningFlow"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend [_SrcBlend] [_DstBlend]
            Cull Off
            ZWrite Off
            ZTest [_ZTest]

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
                half4 _BaseColor;
                half4 _GridColor;
                half4 _BeamColor;
                float _BaseIntensity;
                float _OverallAlpha;
                float _GridColumns;
                float _GridRows;
                float _GridLineWidth;
                float _GridSoftness;
                float _GridIntensity;
                float _GridScrollSpeed;
                float _GridSlant;
                float _BeamRepeat;
                float _BeamWidth;
                float _BeamSoftness;
                float _BeamIntensity;
                float _BeamScrollSpeed;
                float _BeamSlant;
                float _PulseFrequency;
                float _PulseAmount;
                float _EdgeSoftness;
                float _EndFade;
            CBUFFER_END

            float RepeatingBorder(
                float coordinate,
                float width,
                float softness)
            {
                float localCoordinate = frac(coordinate);
                float distanceToBorder = min(
                    localCoordinate,
                    1.0 - localCoordinate);
                return 1.0 - smoothstep(
                    width,
                    width + softness,
                    distanceToBorder);
            }

            float RepeatingBeam(
                float coordinate,
                float width,
                float softness)
            {
                float distanceToCenter = abs(frac(coordinate) - 0.5) * 2.0;
                return 1.0 - smoothstep(
                    width,
                    width + softness,
                    distanceToCenter);
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
                float time = _Time.y;
                float centeredY = input.uv.y - 0.5;
                float gridXCoordinate = input.uv.x * _GridColumns
                    - time * _GridScrollSpeed
                    + centeredY * _GridSlant;
                float gridYCoordinate = input.uv.y * _GridRows;
                float verticalGrid = RepeatingBorder(
                    gridXCoordinate,
                    _GridLineWidth,
                    _GridSoftness);
                float horizontalGrid = RepeatingBorder(
                    gridYCoordinate,
                    _GridLineWidth,
                    _GridSoftness);
                float gridMask = saturate(max(verticalGrid, horizontalGrid));

                float beamCoordinate = input.uv.x * _BeamRepeat
                    - time * _BeamScrollSpeed
                    + centeredY * _BeamSlant;
                float beamMask = RepeatingBeam(
                    beamCoordinate,
                    _BeamWidth,
                    _BeamSoftness);

                float sideDistance = abs(centeredY) * 2.0;
                float sideMask = 1.0 - smoothstep(
                    1.0 - _EdgeSoftness,
                    1.0,
                    sideDistance);
                float endFadeSize = max(_EndFade, 0.0001);
                float endMask = smoothstep(
                        0.0,
                        endFadeSize,
                        input.uv.x)
                    * smoothstep(
                        0.0,
                        endFadeSize,
                        1.0 - input.uv.x);

                float pulseWave = 0.5 + 0.5 * sin(
                    time * _PulseFrequency * 6.2831853);
                float pulse = lerp(1.0 - _PulseAmount, 1.0, pulseWave);
                float bodyMask = sideMask * endMask;

                half3 color = _BaseColor.rgb * _BaseIntensity;
                color += _GridColor.rgb * gridMask * _GridIntensity;
                color += _BeamColor.rgb * beamMask * _BeamIntensity;
                color *= input.color.rgb * pulse;

                float alpha = _BaseColor.a;
                alpha += gridMask * _GridColor.a;
                alpha += beamMask * _BeamColor.a;
                alpha = saturate(alpha)
                    * bodyMask
                    * pulse
                    * _OverallAlpha
                    * input.color.a;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
