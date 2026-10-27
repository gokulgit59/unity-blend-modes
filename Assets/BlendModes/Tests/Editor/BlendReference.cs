using System;

namespace BlendMode.Tests
{
    // CPU reference for the blend formulas, written from the W3C Compositing and Blending spec
    // (not copied from the shader) so the GPU result can be checked against an independent implementation.
    // All values are 0..1, opaque layers, 100% opacity.
    public static class BlendReference
    {
        public struct C
        {
            public double r, g, b;
            public C(double r, double g, double b) { this.r = r; this.g = g; this.b = b; }
            public static C Map(C a, C b, Func<double, double, double> f) => new C(f(a.r, b.r), f(a.g, b.g), f(a.b, b.b));
        }

        public static C Blend(LayerBlendMode mode, C b, C s)
        {
            switch (mode)
            {
                case LayerBlendMode.Normal:
                case LayerBlendMode.Dissolve:     return s;
                case LayerBlendMode.Darken:       return C.Map(b, s, Math.Min);
                case LayerBlendMode.Multiply:     return C.Map(b, s, (x, y) => x * y);
                case LayerBlendMode.ColorBurn:    return C.Map(b, s, ColorBurn);
                case LayerBlendMode.LinearBurn:   return C.Map(b, s, (x, y) => x + y - 1);
                case LayerBlendMode.DarkerColor:  return Lum(s) < Lum(b) ? s : b;
                case LayerBlendMode.Lighten:      return C.Map(b, s, Math.Max);
                case LayerBlendMode.Screen:       return C.Map(b, s, (x, y) => x + y - x * y);
                case LayerBlendMode.ColorDodge:   return C.Map(b, s, ColorDodge);
                case LayerBlendMode.LinearDodge:  return C.Map(b, s, (x, y) => x + y);
                case LayerBlendMode.LighterColor: return Lum(s) > Lum(b) ? s : b;
                case LayerBlendMode.Overlay:      return C.Map(b, s, (x, y) => HardLight(y, x));
                case LayerBlendMode.SoftLight:    return C.Map(b, s, SoftLight);
                case LayerBlendMode.HardLight:    return C.Map(b, s, HardLight);
                case LayerBlendMode.VividLight:   return C.Map(b, s, (x, y) => y < 0.5 ? ColorBurn(x, 2 * y) : ColorDodge(x, 2 * y - 1));
                case LayerBlendMode.LinearLight:  return C.Map(b, s, (x, y) => x + 2 * y - 1);
                case LayerBlendMode.PinLight:     return C.Map(b, s, (x, y) => y < 0.5 ? Math.Min(x, 2 * y) : Math.Max(x, 2 * y - 1));
                case LayerBlendMode.HardMix:      return C.Map(b, s, (x, y) => x + y >= 1 ? 1 : 0);
                case LayerBlendMode.Difference:   return C.Map(b, s, (x, y) => Math.Abs(x - y));
                case LayerBlendMode.Exclusion:    return C.Map(b, s, (x, y) => x + y - 2 * x * y);
                case LayerBlendMode.Subtract:     return C.Map(b, s, (x, y) => x - y);
                case LayerBlendMode.Divide:       return C.Map(b, s, (x, y) => y <= 0 ? (x <= 0 ? 0 : 1) : x / y);
                case LayerBlendMode.Hue:          return SetLum(SetSat(s, Sat(b)), Lum(b));
                case LayerBlendMode.Saturation:   return SetLum(SetSat(b, Sat(s)), Lum(b));
                case LayerBlendMode.Color:        return SetLum(s, Lum(b));
                case LayerBlendMode.Luminosity:   return SetLum(b, Lum(s));
            }
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        public static C Clamp(C c) => new C(Clamp01(c.r), Clamp01(c.g), Clamp01(c.b));
        static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        // W3C: if b == 1 -> 1; else if s == 0 -> 0; else 1 - min(1, (1 - b) / s)
        static double ColorBurn(double b, double s) => b >= 1 ? 1 : s <= 0 ? 0 : 1 - Math.Min(1, (1 - b) / s);
        // W3C: if b == 0 -> 0; else if s == 1 -> 1; else min(1, b / (1 - s))
        static double ColorDodge(double b, double s) => b <= 0 ? 0 : s >= 1 ? 1 : Math.Min(1, b / (1 - s));
        static double HardLight(double b, double s) => s <= 0.5 ? b * 2 * s : 1 - (1 - b) * (1 - (2 * s - 1));
        static double SoftLight(double b, double s)
        {
            if (s <= 0.5) return b - (1 - 2 * s) * b * (1 - b);
            double d = b <= 0.25 ? ((16 * b - 12) * b + 4) * b : Math.Sqrt(b);
            return b + (2 * s - 1) * (d - b);
        }

        static double Lum(C c) => 0.3 * c.r + 0.59 * c.g + 0.11 * c.b;

        static C ClipColor(C c)
        {
            double l = Lum(c);
            double n = Math.Min(c.r, Math.Min(c.g, c.b));
            double x = Math.Max(c.r, Math.Max(c.g, c.b));
            if (n < 0) c = new C(l + (c.r - l) * l / (l - n), l + (c.g - l) * l / (l - n), l + (c.b - l) * l / (l - n));
            if (x > 1) c = new C(l + (c.r - l) * (1 - l) / (x - l), l + (c.g - l) * (1 - l) / (x - l), l + (c.b - l) * (1 - l) / (x - l));
            return c;
        }

        static C SetLum(C c, double l)
        {
            double d = l - Lum(c);
            return ClipColor(new C(c.r + d, c.g + d, c.b + d));
        }

        static double Sat(C c) => Math.Max(c.r, Math.Max(c.g, c.b)) - Math.Min(c.r, Math.Min(c.g, c.b));

        // W3C SetSat: the max channel becomes s, the min channel 0, the middle one scales in between.
        static C SetSat(C c, double s)
        {
            double mn = Math.Min(c.r, Math.Min(c.g, c.b));
            double mx = Math.Max(c.r, Math.Max(c.g, c.b));
            if (mx <= mn) return new C(0, 0, 0);
            return new C((c.r - mn) * s / (mx - mn), (c.g - mn) * s / (mx - mn), (c.b - mn) * s / (mx - mn));
        }
    }
}
