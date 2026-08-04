Shader "Loaded/Kill Impact Fullscreen"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "KillImpactFullscreen"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _KillImpactCenters[4];
            float4 _KillImpactDirections[4];
            float4 _KillImpactParams[4];
            float4 _KillImpactColor;
            float _KillImpactIntensity;
            float _KillImpactAspect;
            float _KillImpactShockwave;
            float _KillImpactRgbSplit;
            float _KillImpactRadialZoom;
            float _KillImpactTear;

            float Hash11(float value)
            {
                value = frac(value * 0.1031);
                value *= value + 33.33;
                value *= value + value;
                return frac(value);
            }

            half4 SampleScene(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_LinearClamp,
                    saturate(uv),
                    _BlitMipLevel);
            }

            half4 Frag(Varyings input) : SV_Target0
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                float strength = saturate(_KillImpactIntensity);

                if (strength <= 0.0001)
                {
                    return SampleScene(uv);
                }

                float aspect = max(0.25, _KillImpactAspect);
                float2 sampleUv = uv;
                float2 splitOffset = 0.0;
                float highlight = 0.0;
                float freezeStyle = 0.0;

                [unroll]
                for (int impactIndex = 0; impactIndex < 4; impactIndex++)
                {
                    float4 impactParams = _KillImpactParams[impactIndex];
                    float progress = saturate(impactParams.x);
                    float impactStrength = saturate(impactParams.y);
                    float critical = saturate(impactParams.z);
                    float finalKill = saturate(impactParams.w);

                    if (impactStrength <= 0.0001)
                    {
                        continue;
                    }

                    float2 center = _KillImpactCenters[impactIndex].xy;
                    float2 direction = _KillImpactDirections[impactIndex].xy;
                    float2 delta = uv - center;
                    float2 aspectDelta = float2(delta.x * aspect, delta.y);
                    float distanceFromImpact = length(aspectDelta);
                    float2 radialAspect = aspectDelta
                        / max(distanceFromImpact, 0.0001);
                    float2 radialUv = float2(
                        radialAspect.x / aspect,
                        radialAspect.y);

                    float radius = lerp(0.012, 0.82, progress);
                    float waveWidth = lerp(0.085, 0.018, progress);
                    float ringDistance = abs(distanceFromImpact - radius);
                    float outwardRing = 1.0 - smoothstep(
                        waveWidth,
                        waveWidth * 2.15,
                        ringDistance);

                    // The first phase pulls the image inward before the main
                    // shockwave expands, which makes even small hits feel dense.
                    float compressionProgress = saturate(progress / 0.18);
                    float compressionRadius = lerp(0.24, 0.018, compressionProgress);
                    float compressionRing = 1.0 - smoothstep(
                        0.035,
                        0.085,
                        abs(distanceFromImpact - compressionRadius));
                    compressionRing *= 1.0 - compressionProgress;

                    float innerWave = sin(
                        (distanceFromImpact - radius) * 90.0 - progress * 8.0);
                    innerWave *= exp(-ringDistance * 28.0);
                    float early = pow(saturate(1.0 - progress), 2.4);
                    float centerMask = 1.0 - smoothstep(
                        0.04,
                        lerp(0.46, 0.78, finalKill),
                        distanceFromImpact);
                    float zoom = early
                        * _KillImpactRadialZoom
                        * impactStrength
                        * lerp(0.0045, 0.022, finalKill);
                    sampleUv -= delta * zoom;
                    sampleUv += radialUv
                        * compressionRing
                        * _KillImpactShockwave
                        * impactStrength
                        * 0.008;
                    sampleUv -= radialUv
                        * (outwardRing * 0.82 + innerWave * 0.18)
                        * _KillImpactShockwave
                        * impactStrength
                        * lerp(0.0045, 0.018, finalKill);

                    float scanIndex = floor(
                        uv.y * 92.0 + progress * 31.0 + impactIndex * 17.0);
                    float scanNoise = Hash11(scanIndex);
                    float tearBand = smoothstep(0.8, 0.985, scanNoise);
                    tearBand *= sin((uv.y + progress * 0.37) * 740.0)
                        * 0.5 + 0.5;
                    float tearEnvelope = early
                        * centerMask
                        * _KillImpactTear
                        * impactStrength;
                    sampleUv += direction
                        * tearBand
                        * tearEnvelope
                        * lerp(0.0025, 0.014, critical);

                    float centerBurst = exp(-distanceFromImpact * 13.0) * early;
                    float splitMask = saturate(
                        outwardRing * 0.9 + centerBurst * 0.7);
                    float2 splitDirection = normalize(
                        radialUv + direction * 0.38 + float2(0.0001, 0.0));
                    splitOffset += splitDirection
                        * splitMask
                        * _KillImpactRgbSplit
                        * impactStrength
                        * lerp(0.0015, 0.008, critical);

                    float angle = atan2(aspectDelta.y, aspectDelta.x);
                    float rayCount = lerp(9.0, 18.0, finalKill);
                    float rays = pow(
                        saturate(cos(angle * rayCount + scanNoise * 3.0)
                            * 0.5 + 0.5),
                        22.0);
                    rays *= centerMask
                        * (1.0 - smoothstep(0.06, 0.72, distanceFromImpact))
                        * early
                        * impactStrength;
                    highlight += outwardRing
                            * impactStrength
                            * lerp(0.045, 0.18, finalKill)
                        + compressionRing * impactStrength * 0.055
                        + centerBurst * impactStrength * 0.065
                        + rays * (0.08 + critical * 0.14);
                    freezeStyle = max(
                        freezeStyle,
                        early
                            * impactStrength
                            * saturate(critical * 0.34 + finalKill * 0.3));
                }

                half4 baseSample = SampleScene(sampleUv);
                half red = SampleScene(sampleUv + splitOffset).r;
                half blue = SampleScene(sampleUv - splitOffset).b;
                half3 color = half3(red, baseSample.g, blue);
                half luminance = dot(color, half3(0.2126, 0.7152, 0.0722));
                color = lerp(color, luminance.xxx, saturate(freezeStyle));
                color += _KillImpactColor.rgb * saturate(highlight);
                float edge = smoothstep(0.42, 0.95, length(uv * 2.0 - 1.0));
                color *= 1.0 - edge * strength * 0.055;

                return half4(color, baseSample.a);
            }
            ENDHLSL
        }
    }
}
