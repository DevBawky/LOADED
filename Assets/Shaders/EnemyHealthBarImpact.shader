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
                float4 _HealthRect;
                float4 _ClipRect;
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
            CBUFFER_END

            float Hash(float2 value)
            {
                return frac(sin(dot(value, float2(127.1, 311.7)))
                    * 43758.5453);
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
