// Shared blend math for Blend Mode/BlendCompositor (and any future backdrop-read shader).
// All inputs and outputs are 0..1 per channel. b = backdrop, s = source.
// Mode indices match LayerBlendMode.cs. Do not reorder.
#ifndef BLEND_MODE_BLEND_FUNCTIONS_INCLUDED
#define BLEND_MODE_BLEND_FUNCTIONS_INCLUDED

// ---------- exact sRGB transfer functions ----------
// UnityCG's LinearToGammaSpace / GammaToLinearSpace are fast approximations that drift by a few
// 8-bit steps in dark tones. Gamma-space blending in a Linear project uses the exact piecewise curve,
// so results match design tools to the last bit.
float3 BlendLinearToSRGB(float3 c)
{
    c = max(c, 0.0);
    return (c <= 0.0031308) ? c * 12.92 : 1.055 * pow(c, 1.0 / 2.4) - 0.055;
}

float3 BlendSRGBToLinear(float3 c)
{
    return (c <= 0.04045) ? c / 12.92 : pow((c + 0.055) / 1.055, 2.4);
}

// ---------- helpers for the component modes (W3C Compositing and Blending) ----------
float BlendLum(float3 c) { return dot(c, float3(0.3, 0.59, 0.11)); }

float3 BlendClipColor(float3 c)
{
    float l = BlendLum(c);
    float n = min(c.r, min(c.g, c.b));
    float x = max(c.r, max(c.g, c.b));
    if (n < 0.0) c = l + (c - l) * l / max(l - n, 1e-5);
    if (x > 1.0) c = l + (c - l) * (1.0 - l) / max(x - l, 1e-5);
    return c;
}

float3 BlendSetLum(float3 c, float l) { return BlendClipColor(c + (l - BlendLum(c))); }

float BlendSat(float3 c) { return max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b)); }

float3 BlendSetSat(float3 c, float s)
{
    float mn = min(c.r, min(c.g, c.b));
    float mx = max(c.r, max(c.g, c.b));
    return (mx > mn) ? (c - mn) * s / (mx - mn) : float3(0.0, 0.0, 0.0);
}

// Hash without sine (Dave Hoskins), stable across GPUs for pixel-sized inputs.
float BlendHash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// ---------- the blend functions ----------
float3 Blend(int mode, float3 b, float3 s)
{
    float3 lo = 0.0, hi = 0.0, d = 0.0;
    [branch] switch (mode)
    {
        case 2:  return min(b, s);                                    // Darken
        case 3:  return b * s;                                        // Multiply
        case 4:  return 1.0 - min(1.0, (1.0 - b) / max(s, 1e-4));     // Color burn
        case 5:  return b + s - 1.0;                                  // Linear burn
        case 6:  return BlendLum(s) < BlendLum(b) ? s : b;            // Darker color
        case 7:  return max(b, s);                                    // Lighten
        case 8:  return 1.0 - (1.0 - b) * (1.0 - s);                  // Screen
        case 9:  return min(1.0, b / max(1.0 - s, 1e-4));             // Color dodge
        case 10: return b + s;                                        // Linear dodge (Add)
        case 11: return BlendLum(s) > BlendLum(b) ? s : b;            // Lighter color
        case 12: // Overlay: the backdrop decides
            lo = 2.0 * b * s;
            hi = 1.0 - 2.0 * (1.0 - b) * (1.0 - s);
            return lerp(lo, hi, step(0.5, b));
        case 13: // Soft light (W3C / Figma formula)
            d  = lerp(sqrt(b), ((16.0 * b - 12.0) * b + 4.0) * b, step(b, 0.25));
            lo = b - (1.0 - 2.0 * s) * b * (1.0 - b);
            hi = b + (2.0 * s - 1.0) * (d - b);
            return lerp(lo, hi, step(0.5, s));
        case 14: // Hard light: the source decides
            lo = 2.0 * b * s;
            hi = 1.0 - 2.0 * (1.0 - b) * (1.0 - s);
            return lerp(lo, hi, step(0.5, s));
        case 15: // Vivid light: color burn below 0.5, color dodge above
            lo = 1.0 - min(1.0, (1.0 - b) / max(2.0 * s, 1e-4));
            hi = min(1.0, b / max(2.0 * (1.0 - s), 1e-4));
            return lerp(lo, hi, step(0.5, s));
        case 16: return b + 2.0 * s - 1.0;                            // Linear light
        case 17: // Pin light
            lo = min(b, 2.0 * s);
            hi = max(b, 2.0 * s - 1.0);
            return lerp(lo, hi, step(0.5, s));
        case 18: return step(1.0 - 1e-5, b + s);                      // Hard mix (epsilon absorbs float error from the sRGB round trip)
        case 19: return abs(b - s);                                   // Difference
        case 20: return b + s - 2.0 * b * s;                          // Exclusion
        case 21: return b - s;                                        // Subtract
        case 22: return b / max(s, 1e-4);                             // Divide
        case 23: return BlendSetLum(BlendSetSat(s, BlendSat(b)), BlendLum(b)); // Hue
        case 24: return BlendSetLum(BlendSetSat(b, BlendSat(s)), BlendLum(b)); // Saturation
        case 25: return BlendSetLum(s, BlendLum(b));                  // Color
        case 26: return BlendSetLum(b, BlendLum(s));                  // Luminosity
        default: return s;   // 0 = Normal, 1 = Dissolve (handled by the caller)
    }
}

#endif
