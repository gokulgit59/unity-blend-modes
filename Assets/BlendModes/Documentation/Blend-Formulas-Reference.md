# Blend formulas: reference and performance notes

For the graphics programmer. `b` = backdrop (layers below), `s` = source (this layer), all channels 0..1. The HLSL lives in `Shaders/BlendFunctions.cginc`. `Tests/Editor/BlendReference.cs` is an independent C# version written from the W3C *Compositing and Blending Level 1* spec. `BlendReferenceTests` checks every mode on the GPU against it, over a 12 x 12 grid of colors (grays, primaries, secondaries, mixed). All 26 deterministic modes match within 2/255.

## Separable modes (per channel)

| # | Mode | Formula | Neutral `s` | Fixed-function? |
|---|---|---|---|---|
| 0 | Normal | `s` | transparent | yes: `SrcAlpha, OneMinusSrcAlpha` |
| 1 | Dissolve | `hash(pixel) < opacity ? s : b` | opacity 0 | no |
| 2 | Darken | `min(b, s)` | white | yes: `BlendOp Min` |
| 3 | Multiply | `b * s` | white | yes: `DstColor, Zero` |
| 4 | Color burn | `1 - min(1, (1 - b) / s)`; b=1 gives 1, s=0 gives 0 | white | no |
| 5 | Linear burn | `b + s - 1` | white | yes: `RevSub` with output `1 - s` |
| 7 | Lighten | `max(b, s)` | black | yes: `BlendOp Max` |
| 8 | Screen | `b + s - b*s` | black | yes: `OneMinusDstColor, One` |
| 9 | Color dodge | `min(1, b / (1 - s))`; b=0 gives 0 | black | no |
| 10 | Linear dodge (Add) | `b + s` | black | yes: `One, One` |
| 12 | Overlay | `HardLight(s, b)` (roles swapped) | 50% gray | no |
| 13 | Soft light | `s <= .5 ? b - (1-2s)b(1-b) : b + (2s-1)(D(b) - b)`; `D(b) = b <= .25 ? ((16b-12)b+4)b : sqrt(b)` | 50% gray | no |
| 14 | Hard light | `s <= .5 ? 2bs : 1 - 2(1-b)(1-s)` | 50% gray | no |
| 15 | Vivid light | `s < .5 ? ColorBurn(b, 2s) : ColorDodge(b, 2s - 1)` | 50% gray | no |
| 16 | Linear light | `b + 2s - 1` | 50% gray | no (needs a 2x factor and a bias) |
| 17 | Pin light | `s < .5 ? min(b, 2s) : max(b, 2s - 1)` | 50% gray | no |
| 18 | Hard mix | `b + s >= 1 ? 1 : 0` | none | no |
| 19 | Difference | `abs(b - s)` | black | no (needs abs) |
| 20 | Exclusion | `b + s - 2bs` | black | yes: `OneMinusDstColor, OneMinusSrcColor` |
| 21 | Subtract | `b - s` | black | yes: `BlendOp RevSub` |
| 22 | Divide | `b / s`; s=0 gives 1 (0 if b=0) | white | no |

## Whole-pixel and non-separable modes

| # | Mode | Formula |
|---|---|---|
| 6 | Darker color | `Lum(s) < Lum(b) ? s : b` |
| 11 | Lighter color | `Lum(s) > Lum(b) ? s : b` |
| 23 | Hue | `SetLum(SetSat(s, Sat(b)), Lum(b))` |
| 24 | Saturation | `SetLum(SetSat(b, Sat(s)), Lum(b))` |
| 25 | Color | `SetLum(s, Lum(b))` |
| 26 | Luminosity | `SetLum(b, Lum(s))` |

`Lum(c) = 0.3r + 0.59g + 0.11b`. `SetLum` adds `l - Lum(c)` to every channel, then `ClipColor` pulls out-of-range colors back toward their own luminance, which keeps the hue. `SetSat` maps max to `s`, min to 0, and scales the middle channel proportionally.

## Compositing (straight alpha, W3C)

```
aS = sourceAlpha * opacity            (x backdropAlpha when the layer is a clipping mask)
B  = saturate(Blend(b, s))
s' = lerp(s, B, aB)                   // over a transparent backdrop the layer shows as plain Normal
aO = aS + aB (1 - aS)
cO = ((1 - aS) aB b + aS s') / aO
```

In a Linear project with `blendInGamma` on, `b` and `s` are converted to gamma before blending and the result is converted back, to match Photoshop and Figma. This project uses the Linear color space, so the conversion is active. It uses the exact piecewise sRGB curve (`BlendLinearToSRGB` / `BlendSRGBToLinear` in `BlendFunctions.cginc`), not the UnityCG approximations, so results match the reference to within 2/255. In a Gamma project the conversion compiles out (`UNITY_COLORSPACE_GAMMA`).

## Performance notes, and what was changed

- **One full-screen copy per composite removed.** The reference ping-pongs between two targets and then copies into `Result`. `BlendLayerStack.Composite` now writes the last layer straight into `Result`. For N layers that's N blits instead of N + 1, plus one clear. All tests are unchanged.
- **Mode selection stays a uniform branch (`[branch] switch`), not 27 shader variants.** `_Mode` is the same for every pixel in a draw, so the branch is coherent and costs close to nothing on desktop and modern mobile GPUs. Keyword variants would multiply build size and shader warm-up by 27 for no measurable win. If profiling on a specific low-end GPU says otherwise, use `#pragma shader_feature_local` with the few modes you ship.
- **Branch-free separable modes.** Overlay, Soft light, Hard light, Vivid light and Pin light compute both halves and `lerp(lo, hi, step(0.5, x))`. That's a few extra ALU ops, with no divergence.
- **Every divide is guarded** (`max(x, 1e-4)` in burn, dodge, divide and vivid light, `max(x, 1e-5)` in `ClipColor`). The reference `ClipColor` could divide 0 by 0 for a gray input pushed out of range by float error. This version can't.
- **Dissolve hash.** The reference used `frac(sin(dot(p, k)) * 43758.5)`. Its precision depends on the GPU's `sin` for large arguments, which gives visible patterns on some mobile GPUs. It now uses Dave Hoskins' sine-free `hash12`, seeded by the integer pixel position (`SV_POSITION`), so the pattern is stable and identical everywhere.
- **The cost that matters is bandwidth:** each layer is one full-target read, one layer-texture read and one write. Keep `continuous` off for static stacks, size `resolution` to the display size, and prefer Fixed-function (zero extra passes) whenever the mode is one of the 9.
- **8-bit intermediates.** The ping-pong targets are ARGB32, as the brief specifies, so each layer quantizes the backdrop to 8 bits. That costs at most about 1/255 per layer: 0.5 is stored as 127/255, so Multiply 0.5 x 0.5 reads back 63/255. For deep stacks (10+ layers) or HDR inputs, switch the targets to `ARGBHalf`.

## Fixed-function and opacity

The GPU blender has no opacity input for these modes. `FixedBlend.shader` fades its *output* toward the mode's neutral color: `lerp(neutral, s, alpha * opacity)`. This is exact for Multiply, Screen, Add, Subtract, Exclusion, Linear burn and Normal. For Darken and Lighten it only approximates the result, because min/max of a faded value is not a fade of min/max: at 50%, Darken over b = 0.6 with s = 0.3 gives 0.60 instead of 0.45.
