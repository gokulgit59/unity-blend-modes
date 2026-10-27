namespace BlendMode
{
    // Same order as the Mode index in BlendCompositor.shader. Do not reorder.
    public enum LayerBlendMode
    {
        Normal = 0, Dissolve, Darken, Multiply, ColorBurn, LinearBurn, DarkerColor,
        Lighten, Screen, ColorDodge, LinearDodge, LighterColor,
        Overlay, SoftLight, HardLight, VividLight, LinearLight, PinLight, HardMix,
        Difference, Exclusion, Subtract, Divide,
        Hue, Saturation, Color, Luminosity
    }
}
