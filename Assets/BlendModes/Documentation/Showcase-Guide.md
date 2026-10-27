# Blend showcase: a guide for the team

Written for a developer joining the project. Read this, then open `Scenes/BlendShowcase2D.unity` and select **README (select me)** in the Hierarchy.

## Run it

1. Open `Scenes/BlendShowcase2D.unity` and press Play.
2. Left/Right or A/D (or the on-screen buttons) move between the 17 stations.
3. Space (or the Blend button) switches Blend on and off. **Blend OFF draws the same layers with plain Normal blending**, so you see exactly what the blend mode adds.
4. The HUD slider sets the opacity of the blended layer. Some stations have their own controls on the right (sliders, buttons, toggles).

The station content is generated when Play starts. In Edit mode you see the text and controls. Station 1's gallery is visible in Edit mode too.

## Folder map

| Folder | What lives there |
|---|---|
| `Shaders/` | `BlendCompositor.shader` (all 27 modes), `BlendFunctions.cginc` (the math, shared), `FixedBlend.shader` (9 GPU modes for sprites and particles), `UIFixedBlend.shader` (the same for uGUI) |
| `Scripts/Runtime/` | `LayerBlendMode`, `BlendLayerStack`, `FixedBlend`, `UIBlendImage`. Assembly `BlendMode`. |
| `Scripts/Gallery/` | `BlendGallery` and its generated textures. Used by the gallery scene and station 1. |
| `Scripts/Editor/` | Scene builders and UI helpers (Editor only). |
| `Showcase/Runtime/` | `Station` (base class), `ShowcaseController`, `ProcArt` (code-generated art), one script per station in `Stations/`. |
| `Showcase/Editor/` | `ShowcaseBuilder` and `StationDefs` (all the station text). |
| `Showcase/Tests/Editor/` | `ShowcaseAudit`, the Part 4 checker. |
| `Tests/Editor/` | Edit Mode tests for the blend math. |


## The two ways to blend

**Fixed-function** (cheap, 9 modes): give a sprite, particle or tilemap a material using `Blend Mode/FixedBlend`, then call `FixedBlend.Apply(material, LayerBlendMode.Screen)`. The GPU blends it with whatever is already on screen. For UI, add `UIBlendImage` to an Image instead.

**Compositor** (all 27 modes): add a `BlendLayerStack`, give it layers (texture, mode, opacity), and show its `Result` RenderTexture on a quad or RawImage. Use it when the mode needs to read the picture underneath (Overlay, Soft light, Color, Hue, Difference...). To blend over "the scene", render a camera into a RenderTexture and use that as the bottom layer (stations 4, 8, 13, 14, 17).

## How a station is built

Every station is a script deriving from `Station`:

```csharp
public class S02_TorchGlowStation : Station
{
    protected override void Build()           // runs once when Play starts
    {
        Sprite("Cave wall", wallTexture, position, widthInUnits, sortingOrder);
        BlendSprite("Glow", glowTexture, position, width, order, LayerBlendMode.Screen, out _);
    }
}
```

`BlendSprite` (fixed-function) and `Control(stack, layer)` (compositor) register the blended thing with the base class. The base class then handles Blend on/off, opacity and the live label. You only write the art and any animation.

## Add a station

1. Copy a small station script (S02 is the simplest) into `Showcase/Runtime/Stations/` and rename the class.
2. Add an entry to `StationDefs.All` with the title, the "How it works" caption, and the formula line.
3. Run **Tools > Blend Mode > Build Showcase Scene**. The builder overwrites the scene, so make changes in code, not by hand in the scene.
4. Run the audit (below) and the Edit Mode tests.

## Test it

- **Edit Mode tests:** Window > General > Test Runner > EditMode > run `BlendMode.Tests.Editor`.
- **Showcase audit:** press Play in BlendShowcase2D, then **Tools > Blend Mode > Run Showcase Audit**. Keep the Editor focused, because Play mode does not advance in the background. Results and ON/OFF screenshots go to `BlendShowcaseReport/` at the project root.

## Gotchas

- `Shader.Find` alone gets stripped from builds. The shaders are referenced from serialized fields: `BlendLayerStack.shader`, `Station.fixedShader`/`compositorShader`, `UIBlendImage.shader`.
- `BlendLayerStack` only re-composites when something changes. Set `continuous` (or call `MarkDirty()`) for animated or camera-fed layers.
- Use `filterMode = Point` on a stack whose result has transparent pixels and is shown enlarged. Otherwise you get dark fringes.
- This project uses the **Linear** color space, so `blendInGamma` matters: on (the default) blends gamma-encoded values like Photoshop and Figma; off blends linear values.
