# From Photoshop and Figma to Unity: blend modes cheat sheet

For designers building UI and 2D art. In Photoshop or Figma you pick a blend mode in the Layers panel. In Unity it depends on the mode.

## The short version

| You want | In Unity | How |
|---|---|---|
| **Normal, Darken, Multiply, Linear Burn, Lighten, Screen, Linear Dodge (Add), Exclusion, Subtract** on a UI element | Live, on the element itself | Add the **UIBlendImage** component to the Image, Raw Image or Text and pick the mode. It works inside Masks and Rect Mask 2D. |
| The same 9 modes on a sprite or particle | Live, on the element | Use a material with the **Blend Mode/FixedBlend** shader. A programmer calls `FixedBlend.Apply` once. |
| **Any other mode** (Overlay, Soft Light, Hard Light, Color, Hue, Saturation, Luminosity, Difference, Color Dodge/Burn, Vivid/Linear/Pin Light, Hard Mix, Divide, Darker/Lighter Color, Dissolve) | Flattened, like a layer group | Build the look as layers in a **BlendLayerStack** and show its result in a **Raw Image**. This is how the "Disabled" button in station 16 works. |

Why the split: those other modes need to *read* the pixels underneath. A UI element drawing itself can't do that, but a layer stack can, because it blends the layers you give it, exactly like a Photoshop group.

If you pick one of those modes on UIBlendImage, it draws as Normal, and the Console and the component's `StatusMessage` tell you to use a layer stack instead.

## Recipes (all in station 16 or elsewhere in the showcase)

- **Button shine sweep:** a white diagonal streak Image, a child of the button, with UIBlendImage **Screen**. Put **Rect Mask 2D** on the button so the streak stays inside, then animate the streak's X position.
- **Pressed state:** a gray (about 55%) Image in the button's shape on top, with UIBlendImage **Multiply**. Enable it while the button is held. Multiply darkens without flattening the art, unlike a black overlay at 40%.
- **Disabled state:** BlendLayerStack with the button art plus a 50% gray layer in **Saturation**, with *Clip To Backdrop* on. Shown in a Raw Image.
- **Rarity glow:** a soft glow Image behind the card with UIBlendImage **Linear Dodge (Add)**, and its Opacity pulsing.
- **Team colors (station 9):** paint the character in grays, then add a flat team color in **Color** with *Clip To Backdrop* on. Shading survives, and the hue comes from the team.
- **Texture on flat art (station 12):** a 50% gray grain and crack texture in **Overlay** (punchy) or **Soft Light** (gentle).

## Things that work like Photoshop

- **Opacity** is the layer opacity, and it works in every mode.
- **Clip To Backdrop** on a stack layer is a **clipping mask** (Alt-click between layers in Photoshop).
- Layer order: in a BlendLayerStack, layer 0 is the bottom.
- Results match Photoshop and Figma math (checked by automated tests). Soft Light uses the Figma/W3C curve, which differs very slightly from Photoshop's.

## Things that are different

- **Fill vs Opacity:** Unity has one opacity. For the "special 8" modes in Photoshop (Color Burn, Linear Burn, Color Dodge, Linear Dodge, Linear Light, Vivid Light, Hard Mix, Difference), Fill and Opacity behave differently. Here everything behaves like Opacity.
- **Pass Through** groups: there are no groups. Put the layers straight into the parent stack.
- **Hard Mix** follows Photoshop at 100% Fill: the result is pure black or white per channel.
- **Darken and Lighten at partial opacity** on live UI are close, not exact. In a layer stack they are exact.
- **Premultiplied art:** export PNGs normally (straight alpha). Premultiplied textures get dark edges.
- **Color space:** this project is set to Linear. Keep *Blend In Gamma* on in the stacks so the results match design tools, which blend gamma-encoded values.
