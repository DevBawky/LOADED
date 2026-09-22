Shader "Loaded/Combat Impact Particle"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _ShapeMode("Shape Mode", Range(0, 2)) = 0
        _Softness("Edge Softness", Range(0.001, 0.5)) = 0.12
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

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _ShapeMode;
                float _Softness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _BaseColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float softness = max(0.001, _Softness);
                float diamondDistance = abs(centeredUv.x)
                    + abs(centeredUv.y);
                float sparkAlpha = 1.0 - smoothstep(
                    0.68 - softness,
                    0.68 + softness,
                    diamondDistance);
                float radialDistance = length(centeredUv);
                float dustAlpha = 1.0 - smoothstep(
                    0.55,
                    1.0,
                    radialDistance);
                float ringDistance = abs(radialDistance - 0.62);
                float ringAlpha = 1.0 - smoothstep(
                    0.07,
                    0.07 + softness,
                    ringDistance);
                float useDust = step(0.5, _ShapeMode)
                    * (1.0 - step(1.5, _ShapeMode));
                float useRing = step(1.5, _ShapeMode);
                float alpha = lerp(sparkAlpha, dustAlpha, useDust);
                alpha = lerp(alpha, ringAlpha, useRing);
                half4 color = input.color;
                color.a *= saturate(alpha);
                return color;
            }
            ENDHLSL
        }
    }
}
