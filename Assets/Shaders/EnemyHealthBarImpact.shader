Shader "Loaded/UI/Enemy Health Bar Impact"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        [HDR] _FlowColor("Energy Flow Color", Color) = (0.2, 1.0, 0.04, 1.0)
        [HDR] _EdgeColor("Edge Light Color", Color) = (0.9, 1.0, 0.28, 1.0)
        [HDR] _HitColor("Hit Core Color", Color) = (1.0, 1.0, 1.0, 1.0)
        [HDR] _CriticalColor("Critical Color", Color) = (1.0, 0.34, 0.015, 1.0)
        [HDR] _DangerColor("Low Health Color", Color) = (1.0, 0.015, 0.0, 1.0)
        _FlowSpeed("Flow Speed", Range(0.0, 8.0)) = 1.4
        _FlowScale("Flow Scale", Range(1.0, 32.0)) = 11.0
        _FlowIntensity("Flow Intensity", Range(0.0, 4.0)) = 0.65
        _HitIntensity("Hit Intensity", Range(0.0, 8.0)) = 3.2
        _DangerThreshold("Low Health Threshold", Range(0.05, 0.75)) = 0.3
        _DangerPulseSpeed("Low Health Pulse Speed", Range(0.0, 12.0)) = 4.0

        [HideInInspector] _HealthRect("Health Rect", Vector) = (0, 0, 1, 1)
        [HideInInspector] _HealthRatio("Health Ratio", Range(0.0, 1.0)) = 1
        [HideInInspector] _HitPosition("Hit Position", Range(0.0, 1.0)) = 1
        [HideInInspector] _HitStrength("Hit Strength", Range(0.0, 2.0)) = 0
        [HideInInspector] _Critical("Critical", Range(0.0, 1.0)) = 0
        [HideInInspector] _GhostMode("Ghost Mode", Range(0.0, 1.0)) = 0
        [HideInInspector] _PreviewMode("Preview Mode", Range(0.0, 1.0)) = 0
        [HideInInspector] _PreviewSegmentCount("Preview Segment Count", Float) = 0
        [HideInInspector] _PreviewRange0("Preview Range 0", Vector) = (0, 0, 0, 0)
        [HideInInspector] _PreviewRange1("Preview Range 1", Vector) = (0, 0, 0, 0)
        [HideInInspector] _PreviewRange2("Preview Range 2", Vector) = (0, 0, 0, 0)
        [HideInInspector] _PreviewRange3("Preview Range 3", Vector) = (0, 0, 0, 0)
        [HideInInspector] _PreviewRange4("Preview Range 4", Vector) = (0, 0, 0, 0)
        [HideInInspector] _PreviewRange5("Preview Range 5", Vector) = (0, 0, 0, 0)
        [HideInInspector] _PreviewRange6("Preview Range 6", Vector) = (0, 0, 0, 0)
        [HideInInspector] _PreviewRange7("Preview Range 7", Vector) = (0, 0, 0, 0)
        [HideInInspector][HDR] _PreviewColor0("Preview Color 0", Color) = (0, 0, 0, 0)
        [HideInInspector][HDR] _PreviewColor1("Preview Color 1", Color) = (0, 0, 0, 0)
        [HideInInspector][HDR] _PreviewColor2("Preview Color 2", Color) = (0, 0, 0, 0)
        [HideInInspector][HDR] _PreviewColor3("Preview Color 3", Color) = (0, 0, 0, 0)
        [HideInInspector][HDR] _PreviewColor4("Preview Color 4", Color) = (0, 0, 0, 0)
        [HideInInspector][HDR] _PreviewColor5("Preview Color 5", Color) = (0, 0, 0, 0)
        [HideInInspector][HDR] _PreviewColor6("Preview Color 6", Color) = (0, 0, 0, 0)
        [HideInInspector][HDR] _PreviewColor7("Preview Color 7", Color) = (0, 0, 0, 0)

        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15
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
            Name "EnemyHealthBarImpact"
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
                half4 _FlowColor;
                half4 _EdgeColor;
                half4 _HitColor;
                half4 _CriticalColor;
                half4 _DangerColor;
                half4 _PreviewColor0;
                half4 _PreviewColor1;
                half4 _PreviewColor2;
                half4 _PreviewColor3;
                half4 _PreviewColor4;
                half4 _PreviewColor5;
                half4 _PreviewColor6;
                half4 _PreviewColor7;
                float4 _HealthRect;
                float4 _ClipRect;
                float4 _PreviewRange0;
                float4 _PreviewRange1;
                float4 _PreviewRange2;
                float4 _PreviewRange3;
                float4 _PreviewRange4;
                float4 _PreviewRange5;
                float4 _PreviewRange6;
                float4 _PreviewRange7;
                float _FlowSpeed;
                float _FlowScale;
                float _FlowIntensity;
                float _HitIntensity;
                float _DangerThreshold;
                float _DangerPulseSpeed;
                float _HealthRatio;
                float _HitPosition;
                float _HitStrength;
                float _Critical;
                float _GhostMode;
                float _PreviewMode;
                float _PreviewSegmentCount;
            CBUFFER_END

            float Hash(float2 value)
            {
                return frac(sin(dot(value, float2(127.1, 311.7)))
                    * 43758.5453);
            }

            void SelectPreviewSegment(
                float horizontalPosition,
                float4 rangeData,
                half4 sourceColor,
                inout half4 selectedColor,
                inout float selectedEmphasis,
                inout float selectedMask)
            {
                const float seamPadding = 0.002;
                float segmentMask = step(
                        rangeData.x - seamPadding,
                        horizontalPosition)
                    * step(
                        horizontalPosition,
                        rangeData.y + seamPadding);
                selectedColor = lerp(
                    selectedColor,
                    sourceColor,
                    segmentMask);
                selectedEmphasis = lerp(
                    selectedEmphasis,
                    rangeData.z,
                    segmentMask);
                selectedMask = max(selectedMask, segmentMask);
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionHCS =
                    TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 textureColor = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv);
                float2 localUV = saturate(
                    (input.positionOS.xy - _HealthRect.xy)
                    * _HealthRect.zw);
                float time = _Time.y;

                if (_PreviewMode > 0.5)
                {
                    half4 selectedColor = half4(0, 0, 0, 0);
                    float selectedEmphasis = 0.0;
                    float rangeMask = 0.0;

                    if (_PreviewSegmentCount > 0.5)
                        SelectPreviewSegment(localUV.x, _PreviewRange0, _PreviewColor0, selectedColor, selectedEmphasis, rangeMask);
                    if (_PreviewSegmentCount > 1.5)
                        SelectPreviewSegment(localUV.x, _PreviewRange1, _PreviewColor1, selectedColor, selectedEmphasis, rangeMask);
                    if (_PreviewSegmentCount > 2.5)
                        SelectPreviewSegment(localUV.x, _PreviewRange2, _PreviewColor2, selectedColor, selectedEmphasis, rangeMask);
                    if (_PreviewSegmentCount > 3.5)
                        SelectPreviewSegment(localUV.x, _PreviewRange3, _PreviewColor3, selectedColor, selectedEmphasis, rangeMask);
                    if (_PreviewSegmentCount > 4.5)
                        SelectPreviewSegment(localUV.x, _PreviewRange4, _PreviewColor4, selectedColor, selectedEmphasis, rangeMask);
                    if (_PreviewSegmentCount > 5.5)
                        SelectPreviewSegment(localUV.x, _PreviewRange5, _PreviewColor5, selectedColor, selectedEmphasis, rangeMask);
                    if (_PreviewSegmentCount > 6.5)
                        SelectPreviewSegment(localUV.x, _PreviewRange6, _PreviewColor6, selectedColor, selectedEmphasis, rangeMask);
                    if (_PreviewSegmentCount > 7.5)
                        SelectPreviewSegment(localUV.x, _PreviewRange7, _PreviewColor7, selectedColor, selectedEmphasis, rangeMask);

                    float stripePosition = localUV.x * 18.0
                        + localUV.y * 7.0
                        - time * lerp(0.7, 1.8, selectedEmphasis);
                    float stripe = smoothstep(
                        0.42,
                        0.58,
                        frac(stripePosition));
                    float pulse = 0.82 + 0.18 * sin(
                        time * 5.5 + localUV.x * 9.0);
                    float brightness = lerp(
                        0.72 + stripe * 0.34,
                        0.92 + stripe * 0.48 * pulse,
                        selectedEmphasis);
                    half3 previewColor = selectedColor.rgb * brightness;
                    float previewAlpha = textureColor.a
                        * input.color.a
                        * selectedColor.a
                        * rangeMask;

                    #ifdef UNITY_UI_CLIP_RECT
                    float2 previewInside = step(
                        _ClipRect.xy,
                        input.positionOS.xy)
                        * step(
                            input.positionOS.xy,
                            _ClipRect.zw);
                    previewAlpha *= previewInside.x * previewInside.y;
                    #endif

                    return half4(previewColor, previewAlpha);
                }

                float flowWave = sin(
                    (localUV.x * _FlowScale
                    + localUV.y * 2.7
                    - time * _FlowSpeed)
                    * 6.2831853);
                flowWave = pow(saturate(flowWave * 0.5 + 0.5), 7.0);

                float scanPosition = frac(time * _FlowSpeed * 0.19);
                float scanDistance = abs(localUV.x - scanPosition);
                scanDistance = min(scanDistance, 1.0 - scanDistance);
                float energyScan = 1.0 - smoothstep(
                    0.025,
                    0.11,
                    scanDistance);
                float verticalCore = 1.0 - abs(localUV.y * 2.0 - 1.0);
                verticalCore = pow(saturate(verticalCore), 1.8);

                float lowHealth = 1.0 - smoothstep(
                    0.06,
                    max(_DangerThreshold, 0.061),
                    _HealthRatio);
                float dangerPulse = 0.5 + 0.5 * sin(
                    time * _DangerPulseSpeed * 6.2831853);
                half3 flowColor = lerp(
                    _FlowColor.rgb,
                    _DangerColor.rgb,
                    lowHealth);
                float flowEnergy = (
                    flowWave * 0.72
                    + energyScan * verticalCore)
                    * _FlowIntensity;
                flowEnergy *= lerp(
                    1.0,
                    0.72 + dangerPulse * 0.75,
                    lowHealth);

                float hitDistance = abs(localUV.x - _HitPosition);
                float hitCore = exp(-hitDistance * 52.0)
                    * _HitStrength;
                float shockRadius = (1.0 - saturate(_HitStrength)) * 0.16;
                float shockRing = 1.0 - smoothstep(
                    0.012,
                    0.045,
                    abs(hitDistance - shockRadius));
                shockRing *= saturate(_HitStrength);

                float2 noiseCell = floor(
                    localUV * float2(72.0, 7.0)
                    + float2(-time * 18.0, time * 4.0));
                float impactNoise = Hash(noiseCell);
                float sparkMask = step(0.78, impactNoise)
                    * (1.0 - smoothstep(0.0, 0.2, hitDistance))
                    * saturate(_HitStrength);

                half3 hitColor = lerp(
                    _HitColor.rgb,
                    _CriticalColor.rgb,
                    _Critical);
                half3 baseColor = textureColor.rgb * input.color.rgb;
                baseColor = lerp(
                    baseColor,
                    baseColor * 0.45 + _CriticalColor.rgb * 0.7,
                    _GhostMode);
                half3 color = baseColor;
                color += flowColor
                    * flowEnergy
                    * lerp(1.0, 1.45, _GhostMode);
                color += _EdgeColor.rgb
                    * verticalCore
                    * 0.16;
                color += hitColor
                    * (hitCore + shockRing * 0.75 + sparkMask)
                    * _HitIntensity;

                float alpha = textureColor.a * input.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                float2 inside = step(
                    _ClipRect.xy,
                    input.positionOS.xy)
                    * step(
                        input.positionOS.xy,
                        _ClipRect.zw);
                alpha *= inside.x * inside.y;
                #endif

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
