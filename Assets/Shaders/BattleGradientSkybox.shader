Shader "LOADED/Battle Gradient Skybox"
{
    Properties
    {
        [HDR] _ZenithColor ("Zenith Color", Color) = (0.055, 0.11, 0.22, 1)
        [HDR] _HorizonColor ("Horizon Color", Color) = (0.38, 0.28, 0.24, 1)
        [HDR] _GroundColor ("Ground Color", Color) = (0.09, 0.07, 0.06, 1)
        _GradientPower ("Gradient Power", Range(0.1, 4)) = 0.65
        _HorizonSharpness ("Horizon Sharpness", Range(1, 32)) = 8
        _Exposure ("Exposure", Range(0, 8)) = 1.2
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Skybox"
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half _GradientPower;
                half _HorizonSharpness;
                half _Exposure;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half height = normalize(input.direction).y;
                half upperBlend = pow(saturate(height), _GradientPower);
                half lowerBlend = pow(saturate(-height), _GradientPower);
                half3 upper = lerp(
                    _HorizonColor.rgb,
                    _ZenithColor.rgb,
                    upperBlend);
                half3 lower = lerp(
                    _HorizonColor.rgb,
                    _GroundColor.rgb,
                    lowerBlend);
                half3 color = height >= 0.0h ? upper : lower;
                half horizonGlow = exp2(
                    -abs(height) * _HorizonSharpness);
                color += _HorizonColor.rgb * horizonGlow * 0.14h;
                return half4(color * _Exposure, 1.0h);
            }
            ENDHLSL
        }
    }
}
