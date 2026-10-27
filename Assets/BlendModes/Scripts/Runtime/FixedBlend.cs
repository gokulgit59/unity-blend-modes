using UnityEngine;
using GpuBlend = UnityEngine.Rendering.BlendMode;
using GpuBlendOp = UnityEngine.Rendering.BlendOp;

namespace BlendMode
{
    // Sets up a Blend Mode/FixedBlend material for the 9 modes the GPU blender can do without reading the backdrop.
    public static class FixedBlend
    {
        public const string ShaderName = "Blend Mode/FixedBlend";

        static readonly int SrcId = Shader.PropertyToID("_SrcBlend");
        static readonly int DstId = Shader.PropertyToID("_DstBlend");
        static readonly int OpId = Shader.PropertyToID("_BlendOp");
        static readonly int NeutralId = Shader.PropertyToID("_Neutral");
        static readonly int InvertId = Shader.PropertyToID("_Invert");
        static readonly int IsNormalId = Shader.PropertyToID("_IsNormal");

        public static bool Supports(LayerBlendMode mode)
        {
            switch (mode)
            {
                case LayerBlendMode.Normal:
                case LayerBlendMode.Darken:
                case LayerBlendMode.Multiply:
                case LayerBlendMode.LinearBurn:
                case LayerBlendMode.Lighten:
                case LayerBlendMode.Screen:
                case LayerBlendMode.LinearDodge:
                case LayerBlendMode.Exclusion:
                case LayerBlendMode.Subtract:
                    return true;
            }
            return false;
        }

        // Returns false if the mode needs the backdrop (use the compositor for those). The material is left unchanged then.
        public static bool Apply(Material mat, LayerBlendMode mode)
        {
            GpuBlend src = GpuBlend.One, dst = GpuBlend.One;
            GpuBlendOp op = GpuBlendOp.Add;
            Color neutral = Color.black;
            bool invert = false, normal = false;

            switch (mode)
            {
                case LayerBlendMode.Normal:      src = GpuBlend.SrcAlpha; dst = GpuBlend.OneMinusSrcAlpha; normal = true; break;
                case LayerBlendMode.Darken:      op = GpuBlendOp.Min; neutral = Color.white; break;
                case LayerBlendMode.Multiply:    src = GpuBlend.DstColor; dst = GpuBlend.Zero; neutral = Color.white; break;
                case LayerBlendMode.LinearBurn:  op = GpuBlendOp.ReverseSubtract; invert = true; break;  // b - (1 - s)
                case LayerBlendMode.Lighten:     op = GpuBlendOp.Max; break;
                case LayerBlendMode.Screen:      src = GpuBlend.OneMinusDstColor; dst = GpuBlend.One; break;
                case LayerBlendMode.LinearDodge: break;                                                    // One, One, Add
                case LayerBlendMode.Exclusion:   src = GpuBlend.OneMinusDstColor; dst = GpuBlend.OneMinusSrcColor; break;
                case LayerBlendMode.Subtract:    op = GpuBlendOp.ReverseSubtract; break;                  // b - s
                default: return false;
            }

            if (mat == null) return true;

            // The blend state properties are floats in the shader, so set them as floats.
            mat.SetFloat(SrcId, (float)src);
            mat.SetFloat(DstId, (float)dst);
            mat.SetFloat(OpId, (float)op);
            mat.SetColor(NeutralId, neutral);
            mat.SetFloat(InvertId, invert ? 1f : 0f);
            mat.SetFloat(IsNormalId, normal ? 1f : 0f);
            return true;
        }
    }
}
