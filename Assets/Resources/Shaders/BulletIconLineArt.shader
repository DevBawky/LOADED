Shader "LOADED/UI/Bullet Line Art"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _GradeColor("Grade Color", Color) = (1,1,1,1)
        _TypeMode("Type", Float) = 0
        _GradeMode("Grade", Float) = 0
        _SpriteUvRect("Sprite UV Rect", Vector) = (0,0,1,1)
        _Motion("Motion", Range(0,1)) = 1
        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
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
                float2 localUv : TEXCOORD1;
                float4 worldPosition : TEXCOORD2;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _GradeColor;
            float _TypeMode;
            float _GradeMode;
            float4 _SpriteUvRect;
            float _Motion;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.localUv = (v.texcoord - _SpriteUvRect.xy)
                    / max(_SpriteUvRect.zw, float2(0.000001, 0.000001));
                o.color = v.color * _Color;
                return o;
            }

            float RingMask(float radius, float target, float halfWidth)
            {
                return 1.0 - smoothstep(halfWidth, halfWidth + 0.018,
                    abs(radius - target));
            }

            float BoxMask(
                float2 samplePosition,
                float2 center,
                float2 halfSize)
            {
                float2 edgeDistance = abs(samplePosition - center) - halfSize;
                float outside = length(max(edgeDistance, 0.0));
                return 1.0 - smoothstep(0.0, 0.025, outside);
            }

            float SegmentMask(
                float2 samplePosition,
                float2 lineStart,
                float2 lineEnd,
                float width)
            {
                float2 segment = lineEnd - lineStart;
                float projection = saturate(dot(samplePosition - lineStart, segment)
                    / max(dot(segment, segment), 0.000001));
                float lineDistance = length(samplePosition
                    - (lineStart + segment * projection));
                return 1.0 - smoothstep(width, width + 0.018, lineDistance);
            }

            float EllipseMask(
                float2 samplePosition,
                float2 center,
                float2 radii,
                float rotation)
            {
                float sine = sin(rotation);
                float cosine = cos(rotation);
                float2 offset = samplePosition - center;
                float2 local = float2(
                    cosine * offset.x + sine * offset.y,
                    -sine * offset.x + cosine * offset.y);
                float ellipseDistance = length(local / max(
                    radii, float2(0.000001, 0.000001)));
                return 1.0 - smoothstep(0.84, 1.0, ellipseDistance);
            }

            float CircleMask(
                float2 samplePosition,
                float2 center,
                float circleRadius)
            {
                return 1.0 - smoothstep(circleRadius,
                    circleRadius + 0.018, length(samplePosition - center));
            }

            float TriangleMask(
                float2 samplePosition,
                float2 pointA,
                float2 pointB,
                float2 pointC)
            {
                float2 edge0 = pointB - pointA;
                float2 edge1 = pointC - pointA;
                float2 offset = samplePosition - pointA;
                float dot00 = dot(edge0, edge0);
                float dot01 = dot(edge0, edge1);
                float dot11 = dot(edge1, edge1);
                float dot20 = dot(offset, edge0);
                float dot21 = dot(offset, edge1);
                float inverse = 1.0 / max(
                    dot00 * dot11 - dot01 * dot01, 0.000001);
                float weightB = (dot11 * dot20 - dot01 * dot21) * inverse;
                float weightC = (dot00 * dot21 - dot01 * dot20) * inverse;
                float weightA = 1.0 - weightB - weightC;
                return smoothstep(-0.018, 0.012,
                    min(weightA, min(weightB, weightC)));
            }

            float ArcMask(
                float angle,
                float count,
                float start,
                float end,
                float offset)
            {
                float phase = frac((angle + UNITY_PI) / (UNITY_PI * 2.0)
                    * count + offset);
                return step(start, phase) * step(phase, end);
            }

            float2 RotatePoint(float2 samplePosition, float rotation)
            {
                float sine = sin(rotation);
                float cosine = cos(rotation);
                return float2(
                    cosine * samplePosition.x - sine * samplePosition.y,
                    sine * samplePosition.x + cosine * samplePosition.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 localUv = i.localUv;
                float2 p = (localUv - 0.5) * 2.0;
                float radius = length(p);
                float angle = atan2(p.y, p.x);
                float time = _Time.y * _Motion;

                // The artwork is a real authored illustration. It is scaled into
                // the frame instead of being converted into a procedural symbol.
                float2 artLocal = (localUv - 0.14) / 0.72;
                float insideArtwork = step(0.0, artLocal.x) * step(artLocal.x, 1.0)
                    * step(0.0, artLocal.y) * step(artLocal.y, 1.0)
                    * step(radius, 0.73);
                float2 artUv = _SpriteUvRect.xy
                    + saturate(artLocal) * _SpriteUvRect.zw;
                fixed4 artwork = tex2D(_MainTex, artUv);
                artwork.rgb *= i.color.rgb;
                artwork.a *= insideArtwork * i.color.a;

                // Normal: complete concentric rings are the neutral baseline.
                float outerRing = RingMask(radius, 0.86, 0.025);
                float innerRing = RingMask(radius, 0.75, 0.012);
                float frame = max(outerRing, innerRing * 0.68);

                if (_TypeMode > 0.5 && _TypeMode < 1.5)
                {
                    // Ghost: offset broken echoes form three static paired arc bands.
                    float ghostAngle = angle;
                    float ghostSegments = ArcMask(
                        ghostAngle, 3.0, 0.08, 0.76, 0.0);
                    float ghostRings = max(
                        RingMask(radius, 0.90, 0.026),
                        RingMask(radius, 0.78, 0.015));
                    frame = ghostRings * ghostSegments;
                }
                else if (_TypeMode > 1.5 && _TypeMode < 2.5)
                {
                    // Sniper: cardinal brackets stay outside an open scope ring.
                    float cardinalGap = 1.0 - step(
                        0.91, abs(cos(angle * 2.0)));
                    float scopeRings = max(
                        RingMask(radius, 0.87, 0.022) * cardinalGap,
                        RingMask(radius, 0.76, 0.014));

                    float topBracket = max(
                        BoxMask(p, float2(0.0, 0.95), float2(0.15, 0.018)),
                        max(
                            BoxMask(p, float2(-0.13, 0.90), float2(0.018, 0.07)),
                            BoxMask(p, float2(0.13, 0.90), float2(0.018, 0.07))));
                    float bottomBracket = max(
                        BoxMask(p, float2(0.0, -0.95), float2(0.15, 0.018)),
                        max(
                            BoxMask(p, float2(-0.13, -0.90), float2(0.018, 0.07)),
                            BoxMask(p, float2(0.13, -0.90), float2(0.018, 0.07))));
                    float leftBracket = max(
                        BoxMask(p, float2(-0.95, 0.0), float2(0.018, 0.15)),
                        max(
                            BoxMask(p, float2(-0.90, -0.13), float2(0.07, 0.018)),
                            BoxMask(p, float2(-0.90, 0.13), float2(0.07, 0.018))));
                    float rightBracket = max(
                        BoxMask(p, float2(0.95, 0.0), float2(0.018, 0.15)),
                        max(
                            BoxMask(p, float2(0.90, -0.13), float2(0.07, 0.018)),
                            BoxMask(p, float2(0.90, 0.13), float2(0.07, 0.018))));
                    float scopeBrackets = max(max(topBracket, bottomBracket),
                        max(leftBracket, rightBracket));
                    frame = max(scopeRings, scopeBrackets);
                }
                else if (_TypeMode > 2.5 && _TypeMode < 3.5)
                {
                    // Storm: five broad rotating crests ripple without arrowheads.
                    float stormWave = sin(angle * 5.0 - time * 1.35)
                        * 0.5 + 0.5;
                    float stormRadius = 0.80 + stormWave * 0.10;
                    float stormRing = RingMask(radius, stormRadius, 0.038);
                    float crestEcho = RingMask(radius, 0.93, 0.016)
                        * pow(saturate(stormWave), 5.0) * 0.72;
                    frame = max(stormRing, crestEcho);
                }
                else if (_TypeMode > 3.5 && _TypeMode < 4.5)
                {
                    // Shotgun: eight pellet clusters use three round pellets each.
                    float pelletPhase = frac((angle + UNITY_PI)
                        / (UNITY_PI * 2.0) * 8.0) - 0.5;
                    float pelletTangent = pelletPhase
                        * (UNITY_PI * 2.0 / 8.0) * max(radius, 0.001);
                    float outerPellet = CircleMask(
                        float2(radius, pelletTangent),
                        float2(0.95, 0.0), 0.040);
                    float sidePellets = max(
                        CircleMask(float2(radius, pelletTangent),
                            float2(0.89, -0.050), 0.042),
                        CircleMask(float2(radius, pelletTangent),
                            float2(0.89, 0.050), 0.042));
                    frame = max(RingMask(radius, 0.79, 0.027),
                        max(outerPellet, sidePellets));
                }
                else if (_TypeMode > 4.5 && _TypeMode < 5.5)
                {
                    // Piercing: the ring opens on one axis for paired spearheads.
                    float horizontalGap = 1.0 - step(
                        0.91, abs(cos(angle)));
                    float piercingRings = max(
                        RingMask(radius, 0.87, 0.023),
                        RingMask(radius, 0.76, 0.014)) * horizontalGap;
                    float rightInnerSpear = TriangleMask(
                        p, float2(0.68, -0.070),
                        float2(0.68, 0.070), float2(0.81, 0.0));
                    float rightOuterSpear = TriangleMask(
                        p, float2(0.84, -0.075),
                        float2(0.84, 0.075), float2(0.98, 0.0));
                    float leftInnerSpear = TriangleMask(
                        p, float2(-0.68, -0.070),
                        float2(-0.68, 0.070), float2(-0.81, 0.0));
                    float leftOuterSpear = TriangleMask(
                        p, float2(-0.84, -0.075),
                        float2(-0.84, 0.075), float2(-0.98, 0.0));
                    float pairedSpears = max(
                        max(rightInnerSpear, rightOuterSpear),
                        max(leftInnerSpear, leftOuterSpear));
                    frame = max(piercingRings, pairedSpears);
                }
                else if (_TypeMode > 5.5 && _TypeMode < 6.5)
                {
                    // Debuff: six fixed inward fangs fill the inner ring edge.
                    float debuffPhase = frac((angle + UNITY_PI)
                        / (UNITY_PI * 2.0) * 6.0);
                    float debuffRings = max(
                        RingMask(radius, 0.87, 0.023),
                        RingMask(radius, 0.76, 0.014));
                    float inwardFangs = TriangleMask(
                        float2(debuffPhase, radius),
                        float2(0.22, 0.78),
                        float2(0.78, 0.78),
                        float2(0.50, 0.56));
                    frame = max(debuffRings, inwardFangs);
                }
                else if (_TypeMode > 6.5 && _TypeMode < 7.5)
                {
                    // Kinetic: four clockwise arrow tabs remain mechanically fixed.
                    float kineticAngle = angle;
                    float kineticPhase = frac((kineticAngle + UNITY_PI)
                        / (UNITY_PI * 2.0) * 4.0);
                    float kineticRings = max(
                        RingMask(radius, 0.88, 0.023),
                        RingMask(radius, 0.76, 0.014));
                    float kineticArcs = kineticRings
                        * step(0.29, kineticPhase) * step(kineticPhase, 0.94);
                    float kineticHeads = TriangleMask(
                        float2(kineticPhase, radius),
                        float2(0.34, 0.67),
                        float2(0.34, 0.96),
                        float2(0.05, 0.84));
                    frame = max(kineticArcs, kineticHeads);
                }
                else if (_TypeMode > 7.5 && _TypeMode < 8.5)
                {
                    // Combo: three overlapping inner/outer arcs use diagonal bridges.
                    float comboAngle = angle + UNITY_PI * 0.5;
                    float comboPhase = frac((comboAngle + UNITY_PI)
                        / (UNITY_PI * 2.0) * 3.0);
                    float comboRings = max(
                        RingMask(radius, 0.88, 0.023),
                        RingMask(radius, 0.76, 0.014));
                    float comboArcs = comboRings
                        * step(0.11, comboPhase) * step(comboPhase, 0.89);
                    float comboBridges = SegmentMask(
                        p, float2(-0.075, 0.74), float2(0.075, 0.91), 0.027);
                    comboBridges = max(comboBridges, SegmentMask(
                        RotatePoint(p, UNITY_PI * 2.0 / 3.0),
                        float2(-0.075, 0.74), float2(0.075, 0.91), 0.027));
                    comboBridges = max(comboBridges, SegmentMask(
                        RotatePoint(p, -UNITY_PI * 2.0 / 3.0),
                        float2(-0.075, 0.74), float2(0.075, 0.91), 0.027));
                    frame = max(comboArcs, comboBridges);
                }
                else if (_TypeMode > 8.5 && _TypeMode < 9.5)
                {
                    // Economy: a coin-like double ring uses twelve casino-chip slots.
                    float coinRings = max(
                        RingMask(radius, 0.87, 0.023),
                        RingMask(radius, 0.76, 0.014));
                    float chipSlotCount = 12.0;
                    float chipStep = UNITY_PI * 2.0 / chipSlotCount;
                    float chipIndex = floor(
                        (angle + UNITY_PI) / chipStep + 0.5);
                    float chipSectorAngle = -UNITY_PI + chipIndex * chipStep;
                    float2 chipPoint = RotatePoint(p, -chipSectorAngle);
                    float chipSlots = BoxMask(
                        chipPoint, float2(0.86, 0.0), float2(0.105, 0.030));
                    frame = max(coinRings, chipSlots);
                }
                else if (_TypeMode > 9.5 && _TypeMode < 10.5)
                {
                    // Growth: a round seed frame sprouts leaves and rounded roots.
                    float growthAttachmentGap = 1.0 - step(
                        0.91, abs(sin(angle)));
                    float growthRings = max(
                        RingMask(radius, 0.87, 0.023),
                        RingMask(radius, 0.76, 0.014)) * growthAttachmentGap;
                    float growthStem = SegmentMask(
                        p, float2(0.0, 0.72), float2(0.0, 0.84), 0.025);
                    float centerLeaf = EllipseMask(
                        p, float2(0.0, 0.90), float2(0.070, 0.09), 0.0);
                    float sideLeaves = max(
                        EllipseMask(p, float2(-0.12, 0.86),
                            float2(0.055, 0.10), -0.75),
                        EllipseMask(p, float2(0.12, 0.86),
                            float2(0.055, 0.10), 0.75));
                    float growthRoots = max(
                        SegmentMask(p, float2(0.0, -0.73),
                            float2(-0.09, -0.84), 0.024),
                        SegmentMask(p, float2(-0.09, -0.84),
                            float2(-0.19, -0.95), 0.024));
                    growthRoots = max(growthRoots, SegmentMask(
                        p, float2(-0.09, -0.84),
                        float2(-0.21, -0.85), 0.021));
                    growthRoots = max(growthRoots, max(
                        SegmentMask(p, float2(0.0, -0.73),
                            float2(0.09, -0.84), 0.024),
                        SegmentMask(p, float2(0.09, -0.84),
                            float2(0.19, -0.95), 0.024)));
                    growthRoots = max(growthRoots, SegmentMask(
                        p, float2(0.09, -0.84),
                        float2(0.21, -0.85), 0.021));
                    frame = max(growthRings,
                        max(max(growthStem, centerLeaf),
                            max(sideLeaves, growthRoots)));
                }
                else if (_TypeMode > 10.5)
                {
                    // Blood: four diagonal thorns tear through a broken single ring.
                    float diagonalGap = 1.0 - step(0.86, cos(
                        (angle - UNITY_PI * 0.25) * 4.0));
                    float bloodRing = RingMask(radius, 0.84, 0.034)
                        * diagonalGap;
                    float bloodHooks = max(
                        SegmentMask(p, float2(0.67, 0.67),
                            float2(0.48, 0.48), 0.028),
                        SegmentMask(p, float2(0.48, 0.48),
                            float2(0.59, 0.45), 0.023));
                    float2 bloodQuarter = RotatePoint(p, UNITY_PI * 0.5);
                    bloodHooks = max(bloodHooks, max(
                        SegmentMask(bloodQuarter, float2(0.67, 0.67),
                            float2(0.48, 0.48), 0.028),
                        SegmentMask(bloodQuarter, float2(0.48, 0.48),
                            float2(0.59, 0.45), 0.023)));
                    float2 bloodHalf = RotatePoint(p, UNITY_PI);
                    bloodHooks = max(bloodHooks, max(
                        SegmentMask(bloodHalf, float2(0.67, 0.67),
                            float2(0.48, 0.48), 0.028),
                        SegmentMask(bloodHalf, float2(0.48, 0.48),
                            float2(0.59, 0.45), 0.023)));
                    float2 bloodThreeQuarters = RotatePoint(
                        p, UNITY_PI * 1.5);
                    bloodHooks = max(bloodHooks, max(
                        SegmentMask(bloodThreeQuarters, float2(0.67, 0.67),
                            float2(0.48, 0.48), 0.028),
                        SegmentMask(bloodThreeQuarters, float2(0.48, 0.48),
                            float2(0.59, 0.45), 0.023)));
                    float pulse = 0.90 + sin(time * 2.2) * 0.10;
                    frame = max(bloodRing, bloodHooks * pulse);
                }

                // Rarity is communicated by frame color only. The old random
                // Ace/Legendary sparkle and moving rarity glint are intentionally removed.
                float frameAlpha = saturate(frame);
                float3 frameColor = _GradeColor.rgb * i.color.rgb;
                float alpha = frameAlpha + artwork.a * (1.0 - frameAlpha);
                float3 color = (frameColor * frameAlpha
                    + artwork.rgb * artwork.a * (1.0 - frameAlpha))
                    / max(alpha, 0.0001);
                fixed4 result = fixed4(color, alpha);

                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
