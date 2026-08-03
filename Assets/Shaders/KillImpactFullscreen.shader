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

            float4 _KillImpactCenter;
            float4 _KillImpactDirection;
            float4 _KillImpactColor;
            float _KillImpactProgress;
            float _KillImpactIntensity;
            float _KillImpactAspect;
            float _KillImpactShockwave;
            float _KillImpactRgbSplit;
            float _KillImpactRadialZoom;
            float _KillImpactTear;
            float _KillImpactCritical;
            float _KillImpactFinal;

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

                float progress = saturate(_KillImpactProgress);
                float aspect = max(0.25, _KillImpactAspect);
                float2 center = _KillImpactCenter.xy;
                float2 delta = uv - center;
                float2 aspectDelta = float2(delta.x * aspect, delta.y);
                float distanceFromImpact = length(aspectDelta);
                float2 radialAspect = aspectDelta
                    / max(distanceFromImpact, 0.0001);
                float2 radialUv = float2(radialAspect.x / aspect, radialAspect.y);

                float radius = lerp(0.012, 0.82, progress);
                float waveWidth = lerp(0.085, 0.018, progress);
                float ringDistance = abs(distanceFromImpact - radius);
                float ring = 1.0 - smoothstep(
                    waveWidth,
                    waveWidth * 2.15,
                    ringDistance);
                float innerWave = sin(
                    (distanceFromImpact - radius) * 90.0 - progress * 8.0);
                innerWave *= exp(-ringDistance * 28.0);

                float early = pow(saturate(1.0 - progress), 2.4);
                float centerMask = 1.0 - smoothstep(
                    0.04,
                    lerp(0.52, 0.78, _KillImpactFinal),
                    distanceFromImpact);
                float zoom = early
                    * _KillImpactRadialZoom
                    * strength
                    * lerp(0.012, 0.022, _KillImpactFinal);

                float2 sampleUv = center + delta * (1.0 - zoom);
                sampleUv -= radialUv
                    * (ring * 0.82 + innerWave * 0.18)
                    * _KillImpactShockwave
                    * strength
                    * lerp(0.009, 0.018, _KillImpactFinal);

                float scanIndex = floor(uv.y * 92.0 + progress * 31.0);
                float scanNoise = Hash11(scanIndex);
                float tearBand = smoothstep(0.76, 0.98, scanNoise);
                tearBand *= sin((uv.y + progress * 0.37) * 740.0) * 0.5 + 0.5;
                float tearEnvelope = early * centerMask * _KillImpactTear * strength;
                sampleUv += _KillImpactDirection.xy
                    * tearBand
                    * tearEnvelope
                    * lerp(0.006, 0.014, _KillImpactCritical);

                float centerBurst = exp(-distanceFromImpact * 13.0) * early;
                float splitMask = saturate(ring * 0.9 + centerBurst * 0.7);
                float2 splitDirection = normalize(
                    radialUv + _KillImpactDirection.xy * 0.38 + float2(0.0001, 0.0));
                float2 splitOffset = splitDirection
                    * splitMask
                    * _KillImpactRgbSplit
                    * strength
                    * lerp(0.0035, 0.008, _KillImpactCritical);

                half4 baseSample = SampleScene(sampleUv);
                half red = SampleScene(sampleUv + splitOffset).r;
                half blue = SampleScene(sampleUv - splitOffset).b;
                half3 color = half3(red, baseSample.g, blue);

                float angle = atan2(aspectDelta.y, aspectDelta.x);
                float rayCount = lerp(10.0, 18.0, _KillImpactFinal);
                float rays = pow(
                    saturate(cos(angle * rayCount + scanNoise * 3.0) * 0.5 + 0.5),
                    22.0);
                rays *= centerMask
                    * (1.0 - smoothstep(0.06, 0.72, distanceFromImpact))
                    * early
                    * strength;

                half luminance = dot(color, half3(0.2126, 0.7152, 0.0722));
                float freezeStyle = early
                    * strength
                    * saturate(_KillImpactCritical * 0.34 + _KillImpactFinal * 0.3);
                color = lerp(color, luminance.xxx, freezeStyle);

                float highlight = ring * strength * (0.1 + _KillImpactFinal * 0.08)
                    + centerBurst * strength * 0.08
                    + rays * (0.12 + _KillImpactCritical * 0.1);
                color += _KillImpactColor.rgb * highlight;

                float edge = smoothstep(0.42, 0.95, length(uv * 2.0 - 1.0));
                color *= 1.0 - edge * early * strength * 0.055;

                return half4(color, baseSample.a);
            }
            ENDHLSL
        }
    }
}
