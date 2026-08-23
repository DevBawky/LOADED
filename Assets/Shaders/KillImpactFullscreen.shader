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
            float4 _KillImpactColors[4];
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
                float2 echoOffset = 0.0;
                float echoStyle = 0.0;
                float opticalBloom = 0.0;
                float opticalTintWeight = 0.0;
                float3 opticalTint = 0.0;
                float haloLight = 0.0;
                float haloColorWeight = 0.0;
                float3 haloColor = 0.0;
                float haloShadow = 0.0;
                float snapClarity = 0.0;
                float snapContrast = 0.0;
                float exposureDip = 0.0;
                float exposureLift = 0.0;
                float edgeStyle = 0.0;
                float screenFlash = 0.0;
                float screenFlashColorWeight = 0.0;
                float3 screenFlashColor = 0.0;

                [unroll]
                for (int impactIndex = 0; impactIndex < 4; impactIndex++)
                {
                    float4 impactParams = _KillImpactParams[impactIndex];
                    float progress = saturate(impactParams.x);
                    float impactStrength = saturate(impactParams.y);
                    float tier = saturate(impactParams.z) * 3.0;
                    float tierStrength = saturate(tier / 3.0);
                    float critical = step(0.5, tier);
                    float devastating = step(1.5, tier);
                    float defeat = step(2.5, tier);
                    float finalKill = saturate(impactParams.w);
                    float wideImpact = saturate(tier / 2.0);

                    if (impactStrength <= 0.0001)
                    {
                        continue;
                    }

                    float2 center = _KillImpactCenters[impactIndex].xy;
                    float4 directionData = _KillImpactDirections[impactIndex];
                    float2 direction = directionData.xy;
                    float shotPulse = saturate(directionData.z);
                    float2 delta = uv - center;
                    float2 aspectDelta = float2(delta.x * aspect, delta.y);
                    float distanceFromImpact = length(aspectDelta);
                    float2 radialAspect = aspectDelta
                        / max(distanceFromImpact, 0.0001);
                    float2 radialUv = float2(
                        radialAspect.x / aspect,
                        radialAspect.y);
                    float early = pow(saturate(1.0 - progress), 2.25);
                    // Every projectile-driven optical wave keeps the exact
                    // authored Primary Color instead of drifting toward the
                    // shared warm fallback color.
                    float3 impactLightColor =
                        _KillImpactColors[impactIndex].rgb;

                    // The firing beat changes the photographed scene itself:
                    // a warm exposure kick, directional lens shove and quick
                    // focus pull. It deliberately skips hit-only halos.
                    if (shotPulse > 0.5)
                    {
                        float shotAttack = smoothstep(
                            0.0,
                            1.0,
                            saturate(progress / 0.065));
                        float shotRelease = 1.0 - smoothstep(
                            0.08,
                            0.72,
                            progress);
                        float shotKick = shotAttack
                            * shotRelease
                            * impactStrength;
                        float shotCenterMask = 1.0 - smoothstep(
                            0.04,
                            lerp(0.62, 0.84, critical),
                            distanceFromImpact);
                        sampleUv -= delta
                            * shotCenterMask
                            * shotKick
                            * _KillImpactRadialZoom
                            * lerp(0.008, 0.014, critical);
                        sampleUv += direction
                            * shotKick
                            * saturate(_KillImpactTear)
                            * lerp(0.002, 0.004, critical);
                        splitOffset += direction
                            * shotCenterMask
                            * shotKick
                            * _KillImpactRgbSplit
                            * lerp(0.0006, 0.0014, critical);
                        opticalBloom += shotCenterMask
                            * shotKick
                            * lerp(0.18, 0.3, critical);
                        opticalTint += impactLightColor
                            * shotCenterMask
                            * shotKick;
                        opticalTintWeight += shotCenterMask * shotKick;
                        snapClarity = max(
                            snapClarity,
                            shotCenterMask * shotKick * 0.28);
                        snapContrast = max(
                            snapContrast,
                            shotKick * lerp(0.1, 0.16, critical));
                        exposureLift = max(
                            exposureLift,
                            shotKick * lerp(0.018, 0.028, critical));
                        edgeStyle = max(
                            edgeStyle,
                            shotKick * lerp(0.1, 0.18, critical));
                        float shotFlash = shotKick
                            * lerp(0.055, 0.078, critical);
                        screenFlash += shotFlash;
                        screenFlashColor += impactLightColor * shotFlash;
                        screenFlashColorWeight += shotFlash;
                        continue;
                    }

                    // A hard optical snap occupies only the first few frames.
                    // Its minimum strength makes normal hits readable even on
                    // dark scene pixels where source-dependent bloom vanishes.
                    float snapAttack = smoothstep(
                        0.0,
                        1.0,
                        saturate(progress / 0.045));
                    float snapRelease = 1.0 - smoothstep(
                        0.055,
                        0.26,
                        progress);
                    float snapEnvelope = snapAttack * snapRelease;
                    float guaranteedStrength = max(
                        impactStrength,
                        lerp(0.38, 0.58, tierStrength));
                    float snapRadius = lerp(0.24, 0.52, tierStrength)
                        + finalKill * 0.04;
                    float snapMask = 1.0 - smoothstep(
                        0.018,
                        snapRadius,
                        distanceFromImpact);
                    float snapStrength = snapEnvelope
                        * guaranteedStrength;
                    sampleUv -= delta
                        * snapMask
                        * snapStrength
                        * _KillImpactRadialZoom
                        * lerp(0.02, 0.045, tierStrength);
                    sampleUv += direction
                        * snapMask
                        * snapStrength
                        * saturate(_KillImpactTear)
                        * lerp(0.0025, 0.009, tierStrength);
                    snapClarity = max(
                        snapClarity,
                        snapMask
                            * snapStrength
                            * lerp(0.45, 0.75, tierStrength));
                    snapContrast = max(
                        snapContrast,
                        snapMask
                            * snapStrength
                            * lerp(0.32, 0.6, tierStrength));
                    exposureLift = max(
                        exposureLift,
                        snapMask * snapStrength * 0.025);
                    float hitFlash = snapEnvelope
                        * impactStrength
                        * lerp(0.028, 0.058, tierStrength);
                    screenFlash += hitFlash;
                    screenFlashColor += impactLightColor * hitFlash;
                    screenFlashColorWeight += hitFlash;

                    // The pressure front is visible only through displaced
                    // scene pixels. No ring color or decal is drawn over it.
                    float maximumRadius = lerp(0.3, 0.84, tierStrength)
                        + finalKill * 0.07;
                    float radius = lerp(0.01, maximumRadius, progress);
                    float waveWidth = lerp(
                        lerp(0.07, 0.105, tierStrength),
                        0.02,
                        progress);
                    float ringDistance = abs(distanceFromImpact - radius);
                    float pressureFront = 1.0 - smoothstep(
                        waveWidth,
                        waveWidth * 2.2,
                        ringDistance);
                    float pressureOscillation = sin(
                        (distanceFromImpact - radius) * 82.0
                        - progress * 7.0);
                    pressureOscillation *= exp(-ringDistance * 25.0);
                    float haloAngle = atan2(
                        aspectDelta.y,
                        aspectDelta.x);
                    float haloNoise = sin(
                            haloAngle * 5.0
                            + progress * 5.4
                            + impactIndex * 1.7) * 0.62
                        + sin(
                            haloAngle * 11.0
                            - progress * 3.1
                            + impactIndex) * 0.38;
                    float brokenArcs = smoothstep(
                        -0.24,
                        0.5,
                        haloNoise);
                    float forwardArc = saturate(
                        0.56
                        + cos(haloAngle) * direction.x * 0.5);
                    float haloFade = pow(
                        saturate(1.0 - progress),
                        0.78);
                    float primaryHalo = pressureFront
                        * lerp(0.28, 1.0, brokenArcs)
                        * lerp(0.58, 1.38, forwardArc)
                        * impactStrength
                        * haloFade
                        * lerp(0.4, 0.82, tierStrength);
                    haloLight += primaryHalo;
                    haloColor += impactLightColor * primaryHalo;
                    haloColorWeight += primaryHalo;
                    haloShadow = max(
                        haloShadow,
                        pressureFront
                            * (1.0 - brokenArcs)
                            * impactStrength
                            * haloFade
                            * 0.2);

                    float compressionProgress = saturate(progress / 0.17);
                    float compressionRadius = lerp(
                        lerp(0.17, 0.27, wideImpact),
                        0.016,
                        compressionProgress);
                    float compressionFront = 1.0 - smoothstep(
                        0.03,
                        0.085,
                        abs(distanceFromImpact - compressionRadius));
                    compressionFront *= 1.0 - compressionProgress;

                    float centerReach = lerp(0.3, 0.78, tierStrength)
                        + finalKill * 0.07;
                    float centerMask = 1.0 - smoothstep(
                        0.025,
                        centerReach,
                        distanceFromImpact);
                    float zoom = early
                        * _KillImpactRadialZoom
                        * impactStrength
                        * lerp(0.014, 0.042, tierStrength)
                        * lerp(1.0, 1.15, finalKill);
                    sampleUv -= delta * zoom * centerMask;
                    sampleUv += radialUv
                        * compressionFront
                        * _KillImpactShockwave
                        * impactStrength
                        * lerp(0.007, 0.013, tierStrength);
                    sampleUv -= radialUv
                        * (pressureFront * 0.82
                            + pressureOscillation * 0.18)
                        * _KillImpactShockwave
                        * impactStrength
                        * lerp(0.008, 0.019, tierStrength);

                    // A delayed pressure return gives critical and stronger
                    // hits a second, crisp kick instead of one soft swell.
                    float secondaryProgress = saturate(
                        (progress - 0.16) / 0.68);
                    float secondaryEnvelope = step(0.16, progress)
                        * (1.0 - smoothstep(0.76, 1.0, progress));
                    float secondaryRadius = lerp(
                        0.035,
                        maximumRadius * 0.82,
                        secondaryProgress);
                    float secondaryWidth = lerp(
                        0.07,
                        0.022,
                        secondaryProgress);
                    float secondaryFront = 1.0 - smoothstep(
                        secondaryWidth,
                        secondaryWidth * 1.9,
                        abs(distanceFromImpact - secondaryRadius));
                    float secondaryTier = critical * 0.16
                        + devastating * 0.2
                        + defeat * 0.23;
                    float secondaryHalo = secondaryFront
                        * secondaryEnvelope
                        * impactStrength
                        * secondaryTier
                        * lerp(0.38, 0.65, tierStrength);
                    haloLight += secondaryHalo;
                    haloColor += impactLightColor * secondaryHalo;
                    haloColorWeight += secondaryHalo;
                    sampleUv -= radialUv
                        * secondaryFront
                        * secondaryEnvelope
                        * impactStrength
                        * secondaryTier
                        * _KillImpactShockwave
                        * 0.011;

                    // A horizontal ballistic plume carries turbulent screen
                    // refraction away from the contact point like desert heat.
                    float2 plumeDelta = aspectDelta;
                    plumeDelta.x -= direction.x
                        * progress
                        * lerp(0.025, 0.13, tierStrength);
                    plumeDelta.x *= lerp(0.82, 0.56, tierStrength);
                    float plumeDistance = length(plumeDelta);
                    float plumeRadius = lerp(0.14, 0.36, tierStrength)
                        * lerp(0.72, 1.28, progress)
                        + finalKill * 0.035;
                    float heatMask = 1.0 - smoothstep(
                        plumeRadius * 0.16,
                        plumeRadius,
                        plumeDistance);
                    float heatEnvelope = pow(
                        saturate(sin(progress * PI)),
                        0.62);
                    float phase = progress * 11.0
                        + Hash11(impactIndex + 1.0) * 6.0;
                    float heatWaveX = sin(
                            plumeDelta.y * 54.0
                            + sin(plumeDelta.x * 21.0 + phase) * 1.65
                            - phase * 1.2)
                        + sin(
                            (plumeDelta.x + plumeDelta.y) * 31.0
                            + phase * 0.7) * 0.55;
                    float heatWaveY = cos(
                            plumeDelta.x * 46.0
                            + sin(plumeDelta.y * 24.0 - phase) * 1.4
                            + phase)
                        + sin(
                            (plumeDelta.x - plumeDelta.y) * 37.0
                            - phase * 0.85) * 0.5;
                    float2 shimmerOffset = float2(
                        heatWaveX / aspect,
                        heatWaveY);
                    sampleUv += shimmerOffset
                        * heatMask
                        * heatEnvelope
                        * impactStrength
                        * _KillImpactShockwave
                        * lerp(0.004, 0.011, tierStrength);

                    // Strong hits briefly squeeze the entire photographed
                    // image before releasing it, while the camera shake remains
                    // independently capped by its existing owner.
                    float elasticEnvelope = pow(
                        saturate(sin(progress * PI)),
                        1.35)
                        * impactStrength
                        * wideImpact;
                    float elasticReach = saturate(
                        centerMask + devastating * 0.42 + defeat * 0.36);
                    sampleUv -= delta
                        * elasticEnvelope
                        * elasticReach
                        * lerp(0.003, 0.011, tierStrength);
                    sampleUv += direction
                        * elasticEnvelope
                        * lerp(0.001, 0.0045, tierStrength)
                        * saturate(_KillImpactTear);

                    float caustic = sin(
                            plumeDelta.x * 73.0
                            + phase * 1.4)
                        * sin(plumeDelta.y * 61.0 - phase);
                    caustic = pow(saturate(caustic * 0.5 + 0.5), 7.0);
                    float shimmerLight = heatMask
                        * heatEnvelope
                        * impactStrength
                        * lerp(0.3, 0.82, tierStrength)
                        * (0.18 + caustic * 0.82);
                    opticalBloom += shimmerLight;
                    opticalTint += impactLightColor * shimmerLight;
                    opticalTintWeight += shimmerLight;

                    float splitMask = saturate(
                        pressureFront * 0.62
                        + heatMask * heatEnvelope * 0.72
                        + compressionFront * 0.28);
                    float2 splitDirection = normalize(
                        radialUv + direction * 0.32 + float2(0.0001, 0.0));
                    splitOffset += splitDirection
                        * splitMask
                        * _KillImpactRgbSplit
                        * impactStrength
                        * lerp(0.0014, 0.0055, tierStrength);

                    // Directional re-sampling produces a photographic echo,
                    // not a drawn silhouette. It begins at critical and grows
                    // through devastating and defeat tiers.
                    float echoTier = critical * 0.1
                        + devastating * 0.13
                        + defeat * 0.15
                        + finalKill * 0.06;
                    float echoEnvelope = early
                        * impactStrength
                        * echoTier;
                    echoStyle = max(echoStyle, echoEnvelope);
                    echoOffset += direction
                        * echoEnvelope
                        * lerp(0.004, 0.012, tierStrength);

                    float fullFrameTier = 0.02
                        + critical * 0.015
                        + devastating * 0.025
                        + defeat * 0.03
                        + finalKill * 0.018;
                    exposureDip = max(
                        exposureDip,
                        early * impactStrength * fullFrameTier);
                    exposureLift = max(
                        exposureLift,
                        pow(saturate(sin(progress * PI)), 3.0)
                            * impactStrength
                            * (fullFrameTier * 0.72));
                    edgeStyle = max(
                        edgeStyle,
                        early * impactStrength * wideImpact);
                }

                half4 baseSample = SampleScene(sampleUv);
                half red = SampleScene(sampleUv + splitOffset).r;
                half blue = SampleScene(sampleUv - splitOffset).b;
                half3 color = half3(red, baseSample.g, blue);

                half3 echoBehind = SampleScene(sampleUv - echoOffset).rgb;
                half3 echoAhead = SampleScene(sampleUv + echoOffset * 0.45).rgb;
                half3 echoColor = color * 0.54
                    + echoBehind * 0.31
                    + echoAhead * 0.15;
                color = lerp(color, echoColor, saturate(echoStyle));

                float2 bloomStep = float2(0.0035 / aspect, 0.0035);
                half3 softScene = (
                    SampleScene(sampleUv + float2(bloomStep.x, 0.0)).rgb
                    + SampleScene(sampleUv - float2(bloomStep.x, 0.0)).rgb
                    + SampleScene(sampleUv + float2(0.0, bloomStep.y)).rgb
                    + SampleScene(sampleUv - float2(0.0, bloomStep.y)).rgb)
                    * 0.25;
                half3 brightScatter = max(softScene - color, 0.0)
                    + max(softScene - 0.56, 0.0) * 0.34;
                float resolvedClarity = saturate(snapClarity);
                color += (color - softScene)
                    * resolvedClarity
                    * 0.72;
                color = (color - 0.5)
                    * (1.0 + saturate(snapContrast) * 0.28)
                    + 0.5;
                float resolvedHalo = saturate(haloLight);
                float3 resolvedHaloColor = haloColor
                    / max(haloColorWeight, 0.0001);
                color *= 1.0 - saturate(haloShadow) * 0.08;
                color += resolvedHaloColor * resolvedHalo * 0.22;
                float resolvedBloom = saturate(opticalBloom);
                float3 resolvedTint = opticalTint
                    / max(opticalTintWeight, 0.0001);
                color += brightScatter * resolvedBloom * 1.55;
                color += resolvedTint
                    * resolvedBloom
                    * resolvedBloom
                    * 0.09;

                float resolvedFlash = saturate(screenFlash);
                float3 resolvedFlashColor = screenFlashColor
                    / max(screenFlashColorWeight, 0.0001);
                color += resolvedFlashColor
                    * resolvedFlash
                    * saturate(1.0 - color);

                color *= 1.0 - saturate(exposureDip);
                color = 1.0 - (1.0 - color)
                    * (1.0 - saturate(exposureLift));
                float edge = smoothstep(0.42, 0.96, length(uv * 2.0 - 1.0));
                color *= 1.0 - edge * edgeStyle * 0.05;

                return half4(color, baseSample.a);
            }
            ENDHLSL
        }
    }
}
