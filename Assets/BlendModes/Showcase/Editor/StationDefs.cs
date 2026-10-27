using System.Collections.Generic;
using BlendMode.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BlendMode.Showcase.EditorTools
{
    // The station table: one entry per station, in order. Text lives here so it is easy to review in one place.
    // 'build' adds anything a station needs in its canvas (sliders, buttons, labels) and wires it to the station.
    public static class StationDefs
    {
        public const string Readme =
@"BLEND SHOWCASE 2D - how this scene works

PLAY IT
  Press Play. Left/Right or A/D (or the on-screen buttons) move between stations.
  Space toggles Blend on/off for the station on screen. Blend OFF draws the very same layers with plain
  Normal blending, so you see what the blend mode adds. The HUD slider sets the opacity of the blended layer.

HIERARCHY
  Main Camera ........... orthographic, moved by the Showcase Controller.
  Showcase Controller ... keyboard, HUD buttons, camera glide. Holds the list of stations.
  HUD Canvas ............ screen-space overlay: help, Prev/Blend/Next, opacity slider, station counter.
  Stations/Station NN ... one per use case, 24 units apart along X.
      Canvas (text and controls) ... world-space UI: title, 'How it works', mode/formula line, live status,
                                     plus station-specific controls on the right.
      Demo (generated) ............. created when Play starts. All sprites and textures are made in code.
      Offstage ...................... (some stations) content 200 units below, seen only by a station camera
                                     that renders it into a RenderTexture for the compositor.

TWO WAYS TO BLEND
  Fixed-function: a normal material using Blend Mode/FixedBlend. The GPU blends it with what is already
    on screen. Only 9 modes work this way (Normal, Darken, Multiply, Linear burn, Lighten, Screen, Add,
    Exclusion, Subtract), but it is the cheapest option and fine for sprites, particles and UI.
  Compositor: BlendLayerStack blends texture layers with Blend Mode/BlendCompositor, like Photoshop layers.
    All 27 modes. The result is a RenderTexture shown on a quad.

WHERE THE CODE IS
  Assets/BlendModes/Showcase/Runtime/Station.cs ........ base class, read it first.
  Assets/BlendModes/Showcase/Runtime/Stations/ ......... one small script per station.
  Assets/BlendModes/Showcase/Runtime/ProcArt.cs ........ code-generated art (pixel canvas, noise).
  Assets/BlendModes/Showcase/Editor/StationDefs.cs ..... the text on every station.
  Rebuild this scene: Tools > Blend Mode > Build Showcase Scene.";

        public static List<StationDef> All => new List<StationDef>
        {
            new StationDef
            {
                type = typeof(S01_GalleryStation),
                title = "All 27 blend modes",
                caption = "Every tile stacks the same two pictures: color bars at the bottom, a rainbow on top. " +
                          "Only the blend mode changes, so you can compare all of them side by side.",
                formula = "Every mode, from Normal to Luminosity. b = bottom layer, s = top layer. Compositor.",
                opacity = 0.8f,   // at 100% Dissolve is identical to Normal; 80% shows its speckle
                build = ctx =>
                {
                    var gallery = GalleryBuilder.BuildGallery(ctx.canvas, new Vector2(0f, -78f), withHeader: false, withControls: false);
                    gallery.transform.localScale = Vector3.one * 0.92f;
                    ((S01_GalleryStation)ctx.station).gallery = gallery;
                }
            },
            new StationDef
            {
                type = typeof(S02_TorchGlowStation),
                title = "Torch glow",
                caption = "Screen flips both layers, multiplies them, and flips the result back. " +
                          "Light only ever adds, and it never goes past white, so it is ideal for glows.",
                formula = "Screen: 1 - (1 - b)(1 - s). Fixed-function.",
            },
            new StationDef
            {
                type = typeof(S03_ShadowsStation),
                title = "Shadows and darkness",
                caption = "Multiply works like stacking tinted glass: it can only make things darker. " +
                          "White changes nothing, so soft shadows and a dark vignette sit on the art without hiding it.",
                formula = "Multiply: b x s. Fixed-function.",
            },
            new StationDef
            {
                type = typeof(S04_DayNightStation),
                title = "Day to night",
                caption = "A camera films the village, then two color layers go on top. Multiply with deep blue darkens it, " +
                          "and Soft light with orange or blue tints it like a gentle lamp. Drag the slider to change the time.",
                formula = "Multiply: b x s.  Soft light: gentle contrast toward s (W3C curve). Compositor over a camera RenderTexture.",
                build = ctx =>
                {
                    var st = (S04_DayNightStation)ctx.station;
                    st.nightSlider = Slider(ctx, "Day  -  Night", st.night, new Vector2(0f, 0f), out var label);
                    st.nightLabel = label;
                }
            },
            new StationDef
            {
                type = typeof(S05_ParticlesStation),
                title = "Particles: Normal vs Add",
                caption = "The same fire twice. With Normal, each particle covers the ones behind it. " +
                          "With Add, overlapping particles add their light together, so the core of the fire glows hot.",
                formula = "Linear dodge (Add): b + s.  Left side Normal, right side Add. Fixed-function.",
                build = ctx =>
                {
                    DemoLabel(ctx, "Normal", new Vector2(-430f, 240f), 30f);
                    DemoLabel(ctx, "Add", new Vector2(430f, 240f), 30f);
                }
            },
            new StationDef
            {
                type = typeof(S06_HitFlashStation),
                title = "Hit flash",
                caption = "When the knight is hit, a white copy of it is added on top for a split second. " +
                          "Add brightens every color toward white, so the knight lights up, then fades back.",
                formula = "Linear dodge (Add): b + s, with s fading from white to black in 0.3 s. Fixed-function.",
                opacity = 0.6f,
                build = ctx => ((S06_HitFlashStation)ctx.station).hitButton = Button(ctx, "Hit  (F)", new Vector2(0f, 0f), 200f)
            },
            new StationDef
            {
                type = typeof(S07_DissolveStation),
                title = "Dissolve",
                caption = "Dissolve does not mix colors. Each pixel is either fully the character or fully the background, " +
                          "picked at random, and the opacity sets the odds. Lower it and the character breaks apart into specks.",
                formula = "Dissolve: noise(pixel) < opacity ? s : b. Compositor.",
                build = ctx => ((S07_DissolveStation)ctx.station).animateToggle = Toggle(ctx, "Animate (disintegrate and respawn)", true, new Vector2(0f, 0f), 480f)
            },
            new StationDef
            {
                type = typeof(S08_GlitchStation),
                title = "Glitch",
                caption = "Both bars subtract colors from what is under them, so bright areas flip to dark and dark ones light up. " +
                          "Difference gives pure black where the two match. Exclusion is a softer, grayer version of the same idea.",
                formula = "Difference: |b - s|, Compositor (upper bar).   Exclusion: b + s - 2bs, Fixed-function (lower bar).",
                build = ctx =>
                {
                    ((S08_GlitchStation)ctx.station).animateToggle = Toggle(ctx, "Animate (jitter)", true, new Vector2(0f, 0f));
                    DemoLabel(ctx, "Difference", new Vector2(-630f, -430f + (S08_GlitchStation.DifferenceBarY + 4.5f) * 10f), 20f).alignment = TextAlignmentOptions.Left;
                    DemoLabel(ctx, "Exclusion", new Vector2(-630f, -430f + (S08_GlitchStation.ExclusionBarY + 12f) * 10f), 20f).alignment = TextAlignmentOptions.Left;
                }
            },
            new StationDef
            {
                type = typeof(S09_TeamColorsStation),
                title = "Team colors",
                caption = "The character is painted in grays only. Color keeps how light or dark each pixel is " +
                          "and takes the actual color (red, blue...) from the team layer, so one sprite works for every team and the shading survives.",
                formula = "Color: SetLum(s, Lum(b)), the hue and saturation of s with the brightness of b. Compositor, clipped to the character.",
                build = ctx =>
                {
                    var st = (S09_TeamColorsStation)ctx.station;
                    for (int i = 0; i < 4; i++)
                        st.teamButtons[i] = Button(ctx, S09_TeamColorsStation.TeamNames[i], new Vector2((i % 2) * 250f, -(i / 2) * 50f), 230f);
                    DemoLabel(ctx, "Original (grays)", new Vector2(-400f, 230f));
                    DemoLabel(ctx, "Recolored with Color", new Vector2(400f, 230f));
                }
            },
            new StationDef
            {
                type = typeof(S10_StatusEffectsStation),
                title = "Status effects",
                caption = "Hue swaps every color for green but keeps how bright and how vivid it was, so the poisoned knight stays readable. " +
                          "Soft light is a gentle tint: it pushes colors toward icy blue or fire orange without flattening the shading.",
                formula = "Hue: SetLum(SetSat(s, Sat(b)), Lum(b)).  Soft light: W3C soft light curve. Compositor, clipped to the character.",
                build = ctx =>
                {
                    var st = (S10_StatusEffectsStation)ctx.station;
                    st.poisonedToggle = Toggle(ctx, "Poisoned (Hue, green)", true, new Vector2(0f, 0f), 480f);
                    st.frozenToggle = Toggle(ctx, "Frozen (Soft light, blue)", false, new Vector2(0f, -40f), 480f);
                    st.burningToggle = Toggle(ctx, "Burning (Soft light, orange)", false, new Vector2(0f, -80f), 480f);
                    DemoLabel(ctx, "Original", new Vector2(-400f, 230f));
                    DemoLabel(ctx, "With status effects", new Vector2(400f, 230f));
                }
            },
            new StationDef
            {
                type = typeof(S11_DesaturateStation),
                title = "Desaturate on damage",
                caption = "Saturation takes how vivid the top layer is and gives it to the picture below, keeping its colors and brightness. " +
                          "The top layer is plain gray, so as health drops the world drains toward black and white.",
                formula = "Saturation: SetLum(SetSat(b, Sat(s)), Lum(b)), with s = gray. Pause adds Multiply to dim. Compositor.",
                build = ctx =>
                {
                    var st = (S11_DesaturateStation)ctx.station;
                    st.healthSlider = Slider(ctx, "Health", st.health, new Vector2(0f, 0f), out _);
                    st.pauseToggle = Toggle(ctx, "Pause screen", false, new Vector2(0f, -80f));
                    var paused = UiKit.Text(ctx.canvas, "PAUSED", "PAUSED", 120, TextAlignmentOptions.Center);
                    paused.fontStyle = FontStyles.Bold;
                    UiKit.Place(paused.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -75f), new Vector2(900f, 160f));
                    paused.gameObject.SetActive(false);
                    st.pausedLabel = paused;
                }
            },
            new StationDef
            {
                type = typeof(S12_TextureDetailStation),
                title = "Texture detail",
                caption = "The texture is gray around 50%, which both modes treat as 'no change'. Lighter spots brighten the stone and darker cracks darken it. " +
                          "Overlay is punchy and contrasty; Soft light does the same thing more gently.",
                formula = "Overlay: b <= 0.5 ? 2bs : 1 - 2(1 - b)(1 - s).  Soft light: W3C soft light curve. Compositor.",
                build = ctx =>
                {
                    DemoLabel(ctx, "Overlay", new Vector2(-430f, 245f), 30f);
                    DemoLabel(ctx, "Soft light", new Vector2(430f, 245f), 30f);
                }
            },
            new StationDef
            {
                type = typeof(S13_TilemapStation),
                title = "Tilemap: baked shadows and variation",
                caption = "Shadows are their own tile layer drawn with Multiply, so they darken any tile underneath. " +
                          "A big soft noise layer then goes on top with Overlay, so repeated grass tiles stop looking identical.",
                formula = "Shadow layer: Multiply b x s, Fixed-function on the TilemapRenderer.  Variation: Overlay, Compositor over a camera RenderTexture.",
            },
            new StationDef
            {
                type = typeof(S14_HazeStation),
                title = "Haze and light shafts",
                caption = "Pale haze drawn with Screen sits in front of the far hills, so they turn lighter and flatter, like real distance. " +
                          "Warm beams drawn with Soft light then brighten the scene in stripes without washing it out.",
                formula = "Haze: Screen 1 - (1 - b)(1 - s), Fixed-function.  Beams: Soft light, Compositor (it needs the backdrop).",
            },
            new StationDef
            {
                type = typeof(S15_WaterStation),
                title = "Water shimmer",
                caption = "Two ripple textures slide over the pond in different directions. They are mostly 50% gray, which Soft light ignores, " +
                          "so only their bright lines show, as moving glints. They are clipped to the pond so the grass stays still.",
                formula = "Soft light with a scrolling texture (per-layer UV offset), clipped to the water. Compositor.",
            },
            new StationDef
            {
                type = typeof(S16_UIStation),
                title = "UI shine and states",
                caption = "Real UI buttons with blend modes, set per Image like in Figma. Screen makes the sweeping shine, Multiply darkens a pressed button, " +
                          "Saturation grays out a disabled one, and Add makes a rare item glow.",
                formula = "Screen, Multiply, Add: UIBlendImage on a UI Image (Fixed-function).  Saturation: BlendLayerStack into a RawImage (Compositor).",
                build = ctx => ((S16_UIStation)ctx.station).uiShader = AssetDatabase.LoadAssetAtPath<Shader>(UiKit.Root + "/Shaders/UIFixedBlend.shader")
            },
            new StationDef
            {
                type = typeof(S17_CompareStation),
                title = "Compare tool",
                caption = "Difference subtracts one picture from the other. Where they match, the result is black. " +
                          "Anything that changed lights up, so even a tiny edit is easy to spot. Switch the changes off and the whole result goes black.",
                formula = "Difference: |b - s|, with b = image A and s = image B (two camera RenderTextures). Compositor.",
                build = ctx =>
                {
                    ((S17_CompareStation)ctx.station).changesToggle = Toggle(ctx, "B has changes", true, new Vector2(0f, 0f));
                    DemoLabel(ctx, "Image A", new Vector2(-560f, 285f), 22f);
                    DemoLabel(ctx, "Image B", new Vector2(-560f, -70f), 22f);
                    DemoLabel(ctx, "Difference of A and B", new Vector2(300f, 270f), 22f);
                }
            },
        };

        // ---------- small helpers for station controls ----------

        public static Slider Slider(StationContext ctx, string label, float value, Vector2 pos, out TMP_Text text)
        {
            text = UiKit.Text(ctx.controls, label + " Label", label, 22, TextAlignmentOptions.MidlineLeft);
            UiKit.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(500f, 30f));
            var s = UiKit.Slider(ctx.controls, label + " Slider", 0f, 1f, value);
            UiKit.Place((RectTransform)s.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos + new Vector2(0f, -36f), new Vector2(480f, 22f));
            ShowcaseBuilder.NoNavigation(s);
            return s;
        }

        public static Button Button(StationContext ctx, string label, Vector2 pos, float width = 150f)
        {
            var b = UiKit.Button(ctx.controls, label + " Button", label);
            UiKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(width, 40f));
            ShowcaseBuilder.NoNavigation(b);
            return b;
        }

        public static Toggle Toggle(StationContext ctx, string label, bool on, Vector2 pos, float width = 240f)
        {
            var t = UiKit.Toggle(ctx.controls, label + " Toggle", label, on);
            UiKit.Place((RectTransform)t.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(width, 30f));
            ShowcaseBuilder.NoNavigation(t);
            return t;
        }

        // A label inside the demo area, in canvas pixels (the canvas center is the station center).
        public static TMP_Text DemoLabel(StationContext ctx, string text, Vector2 pos, float size = 24f)
        {
            var t = UiKit.Text(ctx.canvas, "Label " + text, text, size, TextAlignmentOptions.Center);
            UiKit.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(420f, 40f));
            return t;
        }
    }
}
