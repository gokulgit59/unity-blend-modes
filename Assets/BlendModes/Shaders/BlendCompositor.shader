Shader "Blend Mode/BlendCompositor"
{
    Properties
    {
        _MainTex ("Backdrop", 2D) = "black" {}
        _SourceTex ("Source", 2D) = "white" {}
        _Mode ("Mode index", Integer) = 0
        _Opacity ("Opacity", Range(0, 1)) = 1
        [Toggle] _GammaBlend ("Blend in gamma space", Float) = 1
        _SourceST ("Source tiling (xy) and offset (zw)", Vector) = (1, 1, 0, 0)
        [Toggle] _ClipToBackdrop ("Clip to backdrop (clipping mask)", Float) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "BlendFunctions.cginc"

            sampler2D _MainTex;
            sampler2D _SourceTex;
            int _Mode;
            float _Opacity;
            float _GammaBlend;
            float4 _SourceST;
            float _ClipToBackdrop;

            float4 frag(v2f_img i) : SV_Target
            {
                float4 bT = tex2D(_MainTex, i.uv);
                float4 sT = tex2D(_SourceTex, i.uv * _SourceST.xy + _SourceST.zw);
                float3 b = bT.rgb;
                float3 s = sT.rgb;
                float aB = bT.a;
                float aS = sT.a * _Opacity;
                // Clipping mask: the layer only shows where the layers below it are opaque.
                if (_ClipToBackdrop > 0.5) aS *= aB;

            #if !defined(UNITY_COLORSPACE_GAMMA)
                // Design tools blend gamma-encoded values. Match them in a Linear project.
                if (_GammaBlend > 0.5) { b = BlendLinearToSRGB(b); s = BlendLinearToSRGB(s); }
            #endif

                float3 blended = s;
                if (_Mode == 1)
                {
                    // Dissolve: each pixel is all source or all backdrop.
                    // Seeded by the integer pixel position so the pattern is stable.
                    float n = BlendHash12(floor(i.pos.xy));
                    aS = (n < aS) ? 1.0 : 0.0;
                }
                else
                {
                    blended = saturate(Blend(_Mode, b, s));
                }

                // Compositing with alpha (W3C formula, straight alpha in and out).
                // Where the backdrop is transparent, the source shows as plain Normal.
                float3 sMixed = lerp(s, blended, aB);
                float aO = aS + aB * (1.0 - aS);
                float3 cO = ((1.0 - aS) * aB * b + aS * sMixed) / max(aO, 1e-5);

            #if !defined(UNITY_COLORSPACE_GAMMA)
                if (_GammaBlend > 0.5) cO = BlendSRGBToLinear(cO);
            #endif
                return float4(cO, aO);
            }
            ENDCG
        }
    }
}
