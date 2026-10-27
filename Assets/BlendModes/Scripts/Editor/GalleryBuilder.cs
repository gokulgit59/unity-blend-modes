using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BlendMode.EditorTools
{
    // Builds Scenes/BlendModeGallery.unity: all 27 modes in a labelled grid, plus opacity and gamma controls.
    public static class GalleryBuilder
    {
        public const string ScenePath = UiKit.Root + "/Scenes/BlendModeGallery.unity";
        public const int Columns = 9;
        public static readonly Vector2 TileSize = new Vector2(190f, 190f);
        public const float LabelHeight = 30f;
        public static readonly Vector2 Spacing = new Vector2(12f, 14f);

        [MenuItem("Tools/Blend Mode/Build Gallery Scene")]
        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = UiKit.Go("Main Camera", null, typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.09f, 0.11f);
            cam.transform.position = new Vector3(0f, 0f, -10f);

            var canvas = UiKit.OverlayCanvas("Gallery Canvas", new Vector2(1920f, 1080f));
            UiKit.EnsureEventSystem();
            var root = (RectTransform)canvas.transform;
            UiKit.Stretch((RectTransform)UiKit.Panel(root, "Background", new Color(0.09f, 0.09f, 0.11f)).transform);

            BuildGallery(root, new Vector2(0f, -40f), withHeader: true, withControls: true);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Built " + ScenePath);
        }

        public static float GridHeight => 3 * (TileSize.y + LabelHeight) + 2 * Spacing.y;

        // Builds the 27-tile grid, optionally with a header (title, input previews) and controls, under any
        // RectTransform (screen or world-space canvas). With the header the block is 1830 x 950 units,
        // without it 1830 x GridHeight. It is centred on 'offset'.
        public static BlendGallery BuildGallery(RectTransform parent, Vector2 offset, bool withHeader, bool withControls)
        {
            var block = UiKit.Rect("Blend Mode Gallery", parent);
            UiKit.Place(block, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), offset, new Vector2(1830f, withHeader ? 950f : GridHeight));
            var gallery = block.gameObject.AddComponent<BlendGallery>();

            if (withHeader) BuildHeader(block, gallery, withControls);
            BuildGrid(block, gallery);
            gallery.Apply();
            return gallery;
        }

        static void BuildHeader(RectTransform block, BlendGallery gallery, bool withControls)
        {
            // Header: title, input previews, controls.
            var title = UiKit.Text(block, "Title", "All 27 blend modes", 40, TextAlignmentOptions.TopLeft);
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(700f, 52f));
            var sub = UiKit.Text(block, "Subtitle",
                "Backdrop: 8 color bars over a gray ramp. Source: a hue gradient. Each tile blends the source onto the backdrop with one mode.",
                18, TextAlignmentOptions.TopLeft, new Color(0.75f, 0.75f, 0.8f));
            UiKit.Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -54f), new Vector2(760f, 56f));

            gallery.backdropPreview = Thumb(block, "Backdrop", new Vector2(800f, 0f));
            gallery.sourcePreview = Thumb(block, "Source", new Vector2(920f, 0f));

            if (withControls)
            {
                var controls = UiKit.Rect("Controls", block);
                UiKit.Place(controls, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(760f, 110f));
                UiKit.Stretch(UiKit.Panel(controls, "Panel", new Color(1f, 1f, 1f, 0.06f)).rectTransform);

                gallery.opacityLabel = UiKit.Text(controls, "Opacity Label", "Opacity 100%", 20, TextAlignmentOptions.MidlineLeft);
                UiKit.Place(gallery.opacityLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -12f), new Vector2(170f, 32f));
                gallery.opacitySlider = UiKit.Slider(controls, "Opacity Slider", 0f, 1f, 1f);
                UiKit.Place((RectTransform)gallery.opacitySlider.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, -18f), new Vector2(530f, 22f));

                bool linear = PlayerSettings.colorSpace == ColorSpace.Linear;
                gallery.gammaToggle = UiKit.Toggle(controls, "Gamma Toggle",
                    linear ? "Blend in gamma (match Photoshop / Figma)"
                           : "Blend in gamma (no visible change: this project uses Gamma color space)", true);
                UiKit.Place((RectTransform)gallery.gammaToggle.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -62f), new Vector2(720f, 30f));
            }

        }

        static void BuildGrid(RectTransform block, BlendGallery gallery)
        {
            var grid = UiKit.Rect("Grid", block, typeof(GridLayoutGroup));
            UiKit.Place(grid, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1830f, GridHeight));
            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(TileSize.x, TileSize.y + LabelHeight);
            layout.spacing = Spacing;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Columns;
            layout.childAlignment = TextAnchor.UpperCenter;

            var shader = UiKit.CompositorShader;
            foreach (LayerBlendMode mode in Enum.GetValues(typeof(LayerBlendMode)))
            {
                int i = (int)mode;
                var tile = UiKit.Rect($"Tile {i:00} {mode}", grid);

                var imageRt = UiKit.Rect("Image", tile, typeof(RawImage), typeof(BlendLayerStack));
                UiKit.Place(imageRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, TileSize);
                var raw = imageRt.GetComponent<RawImage>();
                raw.raycastTarget = false;
                var stack = imageRt.GetComponent<BlendLayerStack>();
                stack.Shader = shader;
                stack.resolution = new Vector2Int(256, 256);
                stack.preview = raw;
                gallery.tiles.Add(stack);

                var label = UiKit.Text(tile, "Label", $"{i}. {UiKit.ModeLabel(mode)}", 17);
                UiKit.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(TileSize.x + 8f, LabelHeight));
            }
        }

        static RawImage Thumb(RectTransform parent, string label, Vector2 pos)
        {
            var rt = UiKit.Rect(label + " Preview", parent, typeof(RawImage));
            UiKit.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(84f, 84f));
            var raw = rt.GetComponent<RawImage>();
            raw.raycastTarget = false;
            var t = UiKit.Text(parent, label + " Caption", label, 15, TextAlignmentOptions.Center, new Color(0.75f, 0.75f, 0.8f));
            UiKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos + new Vector2(-8f, -86f), new Vector2(100f, 22f));
            return raw;
        }

    }
}
