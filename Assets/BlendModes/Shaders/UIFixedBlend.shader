// Fixed-function blend modes for uGUI (Image, RawImage, Text). Same idea as Blend Mode/FixedBlend,
// plus what UI needs: stencil (Mask), RectMask2D clipping, alpha clip and the sprite atlas tint.
// Set it up with the UIBlendImage component rather than by hand.
Shader "Blend Mode/UIFixedBlend"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Neutral ("Neutral color (set by FixedBlend.Apply)", Color) = (0, 0, 0, 1)
        [Toggle] _Invert ("Invert output (Linear burn)", Float) = 0
        [Toggle] _IsNormal ("Normal alpha blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src factor", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst factor", Float) = 10
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend op", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
        Blend [_SrcBlend] [_DstBlend]
        BlendOp [_BlendOp]
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 worldPosition : TEXCOORD1; };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _Neutral;
            float _Opacity;
            float _Invert;
            float _IsNormal;

            v2f vert(appdata v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 c = (tex2D(_MainTex, i.uv) + _TextureSampleAdd) * i.color;

            #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
            #endif
            #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - 0.001);
            #endif

                float a = c.a * _Opacity;
                if (_IsNormal > 0.5) return float4(c.rgb, a);

                // Fade toward the mode's neutral color so transparent and clipped pixels change nothing.
                float3 x = (_Invert > 0.5) ? (1.0 - c.rgb) : c.rgb;
                return float4(lerp(_Neutral.rgb, x, a), 1.0);
            }
            ENDCG
        }
    }
}
