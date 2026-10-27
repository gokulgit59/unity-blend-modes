Shader "Blend Mode/FixedBlend"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Neutral ("Neutral color (set by FixedBlend.Apply)", Color) = (0, 0, 0, 1)
        [Toggle] _Invert ("Invert output (Linear burn)", Float) = 0
        [Toggle] _IsNormal ("Normal alpha blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src factor", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst factor", Float) = 10
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend op", Float) = 0
    }
    SubShader
    {
        // No "RenderPipeline" tag and no LightMode on the pass: the pass runs as SRPDefaultUnlit,
        // which the URP Universal Renderer, the URP 2D Renderer and the Built-in pipeline all draw.
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off ZWrite Off Lighting Off
        Blend [_SrcBlend] [_DstBlend]
        BlendOp [_BlendOp]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _Neutral;
            float _Opacity;
            float _Invert;
            float _IsNormal;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv) * _Color * i.color;
                float a = c.a * _Opacity;

                // Normal: plain alpha blending
                if (_IsNormal > 0.5) return float4(c.rgb, a);

                // Other modes: fade toward the neutral color, and let the GPU blender do the rest
                float3 x = (_Invert > 0.5) ? (1.0 - c.rgb) : c.rgb;
                return float4(lerp(_Neutral.rgb, x, a), 1.0);
            }
            ENDCG
        }
    }
}
