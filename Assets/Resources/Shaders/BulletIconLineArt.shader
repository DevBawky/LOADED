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

                float frameRadius = 0.84;
                float frameWidth = 0.025;
                float pattern = 1.0;

                if (_TypeMode > 0.5 && _TypeMode < 1.5)
                {
                    pattern = step(0.18, frac(angle * 0.48 + time * 0.08));
                    frameRadius += sin(angle * 7.0 - time * 1.2) * 0.015;
                }
                else if (_TypeMode > 1.5 && _TypeMode < 2.5)
                {
                    float ticks = step(0.94, abs(cos(angle * 4.0)));
                    pattern = saturate(0.75 + ticks);
                }
                else if (_TypeMode > 2.5 && _TypeMode < 3.5)
                {
                    frameRadius += sin(angle * 5.0 - time * 2.4) * 0.035;
                }
                else if (_TypeMode > 3.5 && _TypeMode < 4.5)
                {
                    pattern = step(0.7, sin(angle * 12.0 - time * 3.0) * 0.5 + 0.5);
                    frameWidth = 0.045;
                }
                else if (_TypeMode > 4.5 && _TypeMode < 5.5)
                {
                    pattern = step(0.15, frac(angle * 0.95 - time * 0.18));
                }
                else if (_TypeMode > 5.5 && _TypeMode < 6.5)
                {
                    pattern = 0.72 + 0.28 * sin(angle * 9.0 + time * 1.7);
                }
                else if (_TypeMode > 6.5 && _TypeMode < 7.5)
                {
                    pattern = step(0.35, frac(angle * 0.64 - time * 0.35));
                }
                else if (_TypeMode > 7.5 && _TypeMode < 8.5)
                {
                    pattern = step(0.55, sin(angle * 8.0 - time * 2.0) * 0.5 + 0.5);
                }
                else if (_TypeMode > 8.5 && _TypeMode < 9.5)
                {
                    pattern = 0.8 + 0.2 * sin(angle * 20.0 - time * 1.1);
                }
                else if (_TypeMode > 9.5 && _TypeMode < 10.5)
                {
                    frameRadius += max(0, p.y) * 0.025 * sin(angle * 6.0 + time);
                }
                else if (_TypeMode > 10.5)
                {
                    pattern = 0.75 + 0.25 * pow(abs(sin(time * 2.4)), 8.0);
                    frameRadius += sin(angle * 2.0) * 0.015;
                }

                float frame = 1.0 - smoothstep(frameWidth, frameWidth + 0.018,
                    abs(radius - frameRadius));
                frame *= saturate(pattern);
                float innerRing = 1.0 - smoothstep(0.012, 0.025,
                    abs(radius - 0.75));
                frame = max(frame, innerRing * 0.55);

                // Rarity is communicated by frame color only. The old random
                // Ace/Legendary sparkle and moving rarity glint are intentionally removed.
                float alpha = saturate(max(artwork.a, frame));
                float3 frameColor = _GradeColor.rgb * i.color.rgb;
                float frameOnly = frame * (1.0 - artwork.a);
                float3 color = (artwork.rgb * artwork.a
                    + frameColor * frameOnly) / max(alpha, 0.0001);
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
