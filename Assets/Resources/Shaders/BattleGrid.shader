Shader "LOADED/BattleGrid"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _WarningEffect ("Warning Effect", Float) = 0
        _Inset ("Overlap Inset", Range(0,0.2)) = 0
        _Focus ("Owner Hover Focus", Range(0,1)) = 0
        _Urgency ("Attack Imminent", Range(0,1)) = 0
        _Afterglow ("Attack Afterglow", Range(0,1)) = 0
        _Fade ("Warning Opacity", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _WarningEffect;
                float _Inset;
                float _Focus;
                float _Urgency;
                float _Afterglow;
                float _Fade;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = input.color * _Tint;
                if (_WarningEffect > 0.5)
                {
                    float edge = min(min(input.uv.x, 1 - input.uv.x),
                        min(input.uv.y, 1 - input.uv.y));
                    clip(edge - _Inset);
                    float2 gridDistance = abs(frac(input.uv * float2(6, 3)) - 0.5);
                    float grid = smoothstep(0.43, 0.49, max(gridDistance.x, gridDistance.y));
                    float emphasis = max(_Focus, _Urgency);
                    float rim = 1 - smoothstep(_Inset + 0.015,
                        _Inset + lerp(0.045, 0.075, emphasis) + _Urgency * 0.02, edge);
                    // Slow breathing, never a fully dark/off frame.
                    float pulse = 0.5 + 0.5 * sin(_Time.y * 3.14159265);
                    pulse = lerp(pulse, 1, _Urgency);
                    color.rgb *= lerp(0.78, 1.0, max(grid, rim));
                    color.rgb *= lerp(0.88, 1.0, pulse);
                    color.a *= lerp(0.88, 1.0, pulse);
                    // A bright tinted rim stays readable at the darkest pulse.
                    half3 rimColor = lerp(_Tint.rgb, half3(1, 1, 1), lerp(0.42, 0.9, emphasis));
                    color.rgb = lerp(color.rgb, color.rgb * 1.18, emphasis);
                    color.rgb = lerp(color.rgb, rimColor, rim);
                    color.a = lerp(color.a, input.color.a * _Tint.a, rim);
                    color.a = lerp(color.a, _Tint.a, rim * emphasis);
                    // The fill drops away first, leaving a brief fading border.
                    color.a *= lerp(1, lerp(0.2 * _Fade, 1, rim), _Afterglow) * _Fade;
                }
                return color;
            }
            ENDHLSL
        }
    }
}
