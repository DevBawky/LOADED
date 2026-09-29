Shader "LOADED/Battle Gradient Skybox"
{
    Properties
    {
        [HDR] _ZenithColor ("Zenith Color", Color) = (0.055, 0.11, 0.22, 1)
        [HDR] _HorizonColor ("Horizon Color", Color) = (0.48, 0.63, 0.82, 1)
        [HDR] _GroundColor ("Ground Color", Color) = (0.28, 0.18, 0.11, 1)
        [HDR] _SunColor ("Sun Color", Color) = (2.4, 1.25, 0.55, 1)
        [HDR] _CloudColor ("Cloud Light Color", Color) = (0.9, 0.72, 0.64, 1)
        [HDR] _CloudShadowColor ("Cloud Shadow Color", Color) = (0.18, 0.23, 0.34, 1)
        _SunDirection ("Sun Direction", Vector) = (0.42, 0.67, 0.61, 0)
        _SunSize ("Sun Size", Range(0.001, 0.12)) = 0.025
        _SunHalo ("Sun Halo", Range(0, 2)) = 0.7
        _CloudScale ("Cloud Scale", Range(0.1, 4)) = 0.85
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.46
        _CloudSoftness ("Cloud Softness", Range(0.01, 0.4)) = 0.12
        _CloudOpacity ("Cloud Opacity", Range(0, 1)) = 0.60
        _CloudSpeed ("Cloud Speed", Vector) = (0.008, 0.003, 0, 0)
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
                half4 _SunColor;
                half4 _CloudColor;
                half4 _CloudShadowColor;
                float4 _SunDirection;
                half _SunSize;
                half _SunHalo;
                half _CloudScale;
                half _CloudCoverage;
                half _CloudSoftness;
                half _CloudOpacity;
                float4 _CloudSpeed;
                half _GradientPower;
                half _HorizonSharpness;
                half _Exposure;
            CBUFFER_END

            float Hash21(float2 coordinate)
            {
                coordinate = frac(coordinate * float2(123.34, 456.21));
                coordinate += dot(coordinate, coordinate + 45.32);
                return frac(coordinate.x * coordinate.y);
            }

            float ValueNoise(float2 coordinate)
            {
                float2 cell = floor(coordinate);
                float2 local = frac(coordinate);
                local = local * local * (3.0 - 2.0 * local);

                float bottom = lerp(
                    Hash21(cell),
                    Hash21(cell + float2(1.0, 0.0)),
                    local.x);
                float top = lerp(
                    Hash21(cell + float2(0.0, 1.0)),
                    Hash21(cell + 1.0),
                    local.x);
                return lerp(bottom, top, local.y);
            }

            float CloudNoise(float2 coordinate)
            {
                float noise = ValueNoise(coordinate) * 0.57;
                coordinate = coordinate * 2.03 + 7.13;
                noise += ValueNoise(coordinate) * 0.29;
                coordinate = coordinate * 2.01 - 4.71;
                noise += ValueNoise(coordinate) * 0.14;
                return noise;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                half height = direction.y;
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

                float3 sunDirection = normalize(_SunDirection.xyz);
                half sunAlignment = saturate(dot(direction, sunDirection));
                half sunDisk = smoothstep(
                    1.0h - _SunSize,
                    1.0h - _SunSize * 0.22h,
                    sunAlignment);
                half sunHalo = pow(sunAlignment, 48.0h) * _SunHalo;
                half horizonSun = pow(sunAlignment, 7.0h)
                    * exp2(-abs(height) * 4.0h)
                    * _SunHalo;
                color += _SunColor.rgb
                    * (sunDisk + sunHalo * 0.24h + horizonSun * 0.08h);

                half skyMask = smoothstep(-0.015h, 0.14h, height);
                float projectionHeight = max(height + 0.22, 0.075);
                float2 cloudCoordinates = direction.xz
                    / projectionHeight
                    * _CloudScale;
                cloudCoordinates += _Time.y * _CloudSpeed.xy;
                float cloudShape = CloudNoise(cloudCoordinates);
                float cloudDetail = CloudNoise(
                    cloudCoordinates * 2.7 + float2(19.4, -11.7));
                cloudShape = cloudShape * 0.82 + cloudDetail * 0.18;
                half cloudMask = smoothstep(
                    _CloudCoverage - _CloudSoftness,
                    _CloudCoverage + _CloudSoftness,
                    cloudShape);
                cloudMask *= skyMask * _CloudOpacity;

                half lightFacing = saturate(
                    dot(normalize(float3(direction.x, 0.35, direction.z)),
                        sunDirection));
                half cloudLight = saturate(
                    cloudShape * 0.9h + lightFacing * 0.32h);
                half3 cloudColor = lerp(
                    _CloudShadowColor.rgb,
                    _CloudColor.rgb,
                    cloudLight);
                cloudColor += _SunColor.rgb
                    * pow(lightFacing, 8.0h)
                    * 0.05h;
                color = lerp(color, cloudColor, cloudMask);

                return half4(color * _Exposure, 1.0h);
            }
            ENDHLSL
        }
    }
}
