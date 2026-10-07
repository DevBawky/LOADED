Shader "LOADED/Battle Lit Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor("Renderer Color", Color) = (1, 1, 1, 1)
        _NormalStrength("Procedural Normal Strength", Range(0, 1)) = 0.28
        _DiffuseWrap("Diffuse Wrap", Range(0, 1)) = 0.55
        _AmbientStrength("Ambient Strength", Range(0, 2)) = 0.78
        _RimStrength("Rim Strength", Range(0, 1)) = 0.14
        _SpecularStrength("Specular Strength", Range(0, 1)) = 0.08
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

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "BattleSpriteForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _NormalStrength;
                half _DiffuseWrap;
                half _AmbientStrength;
                half _RimStrength;
                half _SpecularStrength;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half Luminance(half3 color)
            {
                return dot(color, half3(0.299h, 0.587h, 0.114h));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(
                    input.positionOS,
                    unity_SpriteProps.xy);

                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv) * input.color;
                if (sprite.a <= 0.001h)
                {
                    discard;
                }

                float2 texel = _MainTex_TexelSize.xy;
                half left = Luminance(SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv - float2(texel.x, 0)).rgb);
                half right = Luminance(SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv + float2(texel.x, 0)).rgb);
                half down = Luminance(SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv - float2(0, texel.y)).rgb);
                half up = Luminance(SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv + float2(0, texel.y)).rgb);

                half3 viewDirection = GetWorldSpaceNormalizeViewDir(
                    input.positionWS);
                half3 cameraRight = normalize(UNITY_MATRIX_I_V[0].xyz);
                half3 cameraUp = normalize(UNITY_MATRIX_I_V[1].xyz);
                half3 normalWS = normalize(
                    viewDirection
                    + cameraRight * (left - right) * _NormalStrength
                    + cameraUp * (down - up) * _NormalStrength);

                float4 shadowCoord = TransformWorldToShadowCoord(
                    input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half wrappedDiffuse = saturate(
                    (dot(normalWS, mainLight.direction) + _DiffuseWrap)
                    / (1.0h + _DiffuseWrap));
                half shadow = lerp(0.42h, 1.0h, mainLight.shadowAttenuation);
                half3 direct = mainLight.color
                    * wrappedDiffuse
                    * shadow
                    * mainLight.distanceAttenuation;

                half3 ambient = SampleSH(normalWS) * _AmbientStrength;
                half3 halfVector = normalize(
                    mainLight.direction + viewDirection);
                half specular = pow(
                    saturate(dot(normalWS, halfVector)),
                    18.0h) * _SpecularStrength * shadow;
                half rim = pow(
                    1.0h - saturate(dot(normalWS, viewDirection)),
                    2.2h) * _RimStrength;

                half3 lighting = max(ambient + direct, 0.38h);
                half3 color = sprite.rgb * lighting;
                color += mainLight.color * (specular + rim) * sprite.a;
                color = MixFog(color, input.fogFactor);
                return half4(color, sprite.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
