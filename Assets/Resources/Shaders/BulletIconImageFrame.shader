Shader "LOADED/UI/Bullet Image Frame"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _FrameTex("Frame Mask", 2D) = "black" {}
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
            sampler2D _FrameTex;
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

            float2 RotateUv(float2 uv, float rotation)
            {
                float sine = sin(rotation);
                float cosine = cos(rotation);
                float2 centered = uv - 0.5;
                return float2(
                    cosine * centered.x - sine * centered.y,
                    sine * centered.x + cosine * centered.y) + 0.5;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 localUv = i.localUv;
                float radius = length((localUv - 0.5) * 2.0);
                float time = _Time.y * _Motion;

                float2 artLocal = (localUv - 0.14) / 0.72;
                float insideArtwork = step(0.0, artLocal.x)
                    * step(artLocal.x, 1.0)
                    * step(0.0, artLocal.y)
                    * step(artLocal.y, 1.0)
                    * step(radius, 0.73);
                float2 artUv = _SpriteUvRect.xy
                    + saturate(artLocal) * _SpriteUvRect.zw;
                fixed4 artwork = tex2D(_MainTex, artUv);
                artwork.rgb *= i.color.rgb;
                artwork.a *= insideArtwork * i.color.a;

                float2 frameUv = localUv;
                if (_TypeMode > 2.5 && _TypeMode < 3.5)
                    frameUv = RotateUv(frameUv, time * 0.10);

                float insideFrame = step(0.0, frameUv.x)
                    * step(frameUv.x, 1.0)
                    * step(0.0, frameUv.y)
                    * step(frameUv.y, 1.0);
                fixed4 frameSample = tex2D(_FrameTex, saturate(frameUv));
                float frame = frameSample.r * frameSample.a * insideFrame;
                if (_TypeMode > 10.5)
                    frame *= 0.90 + sin(time * 2.2) * 0.10;

                float frameAlpha = saturate(frame * i.color.a);
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
