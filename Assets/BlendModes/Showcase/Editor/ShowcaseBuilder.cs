using System;
using System.Collections.Generic;
using BlendMode.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BlendMode.Showcase.EditorTools
{
    // Builds Scenes/BlendShowcase2D.unity from the station table in StationDefs.cs.
    // Run it from Tools > Blend Mode > Build Showcase Scene. It overwrites the scene.
    public static class ShowcaseBuilder
    {
        public const string ScenePath = UiKit.Root + "/Scenes/BlendShowcase2D.unity";
        public const float Spacing = 24f;            // world units between stations
        public const float CanvasScale = 0.01f;      // 100 canvas pixels = 1 world unit
        static readonly Color Background = new Color(0.07f, 0.07f, 0.09f);

        [MenuItem("Tools/Blend Mode/Build Showcase Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var readme = new GameObject("README  (select me)").AddComponent<SceneNotes>();
            readme.notes = StationDefs.Readme;

            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 50f;

            UiKit.EnsureEventSystem();
            var controller = new GameObject("Showcase Controller").AddComponent<ShowcaseController>();
            controller.cam = cam;
            BuildHud(controller);

            var stationsRoot = new GameObject("Stations").transform;
            var defs = StationDefs.All;
            for (int i = 0; i < defs.Count; i++)
            {
                var station = BuildStation(defs[i], i, defs.Count, stationsRoot, cam);
                controller.stations.Add(station);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Built {ScenePath} with {defs.Count} stations.");
        }

        static Station BuildStation(StationDef def, int index, int total, Transform parent, Camera cam)
        {
            var root = new GameObject($"Station {index + 1:00}  {def.title}");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(index * Spacing, 0f, 0f);

            var station = (Station)root.AddComponent(def.type);
            station.title = def.title;
            station.caption = def.caption;
            station.formula = def.formula;
            station.opacity = def.opacity;
            station.fixedShader = UiKit.FixedShader;
            station.compositorShader = UiKit.CompositorShader;

            var canvas = WorldCanvas(root.transform, cam);
            var c = (RectTransform)canvas.transform;

            var title = UiKit.Text(c, "Title", $"<size=60%><color=#8A8FA3>{index + 1} / {total}</color></size>  {def.title}", 50, TextAlignmentOptions.TopLeft);
            title.fontStyle = FontStyles.Bold;
            UiKit.Place(title.rectTransform, Center, TopLeft, new Vector2(-920f, 520f), new Vector2(1300f, 70f));

            var caption = UiKit.Text(c, "How it works", "<b>How it works:</b> " + def.caption, 25, TextAlignmentOptions.TopLeft, new Color(0.86f, 0.87f, 0.9f));
            UiKit.Place(caption.rectTransform, Center, TopLeft, new Vector2(-920f, 446f), new Vector2(1300f, 100f));

            var formula = UiKit.Text(c, "Mode and formula", def.formula, 21, TextAlignmentOptions.TopLeft, new Color(0.55f, 0.82f, 1f));
            UiKit.Place(formula.rectTransform, Center, TopLeft, new Vector2(-920f, 340f), new Vector2(1300f, 40f));

            var status = UiKit.Text(c, "Blend status (live)", "Blend ON", 28, TextAlignmentOptions.TopRight);
            UiKit.Place(status.rectTransform, Center, new Vector2(1f, 1f), new Vector2(920f, 520f), new Vector2(560f, 90f));
            station.statusLabel = status;

            var controls = UiKit.Rect("Station controls", c);
            UiKit.Place(controls, Center, new Vector2(1f, 1f), new Vector2(920f, 420f), new Vector2(500f, 140f));

            def.build?.Invoke(new StationContext { station = station, canvas = c, controls = controls });
            return station;
        }

        static Canvas WorldCanvas(Transform parent, Camera cam)
        {
            var go = new GameObject("Canvas (text and controls)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvas.sortingOrder = 50;          // text and controls draw over the demo sprites
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(1920f, 1080f);
            rt.localScale = Vector3.one * CanvasScale;
            rt.localPosition = Vector3.zero;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
            return canvas;
        }

        static void BuildHud(ShowcaseController controller)
        {
            var canvas = UiKit.OverlayCanvas("HUD Canvas", new Vector2(1920f, 1080f));
            canvas.sortingOrder = 100;
            var root = (RectTransform)canvas.transform;

            // Bottom-left: controls help.
            var help = UiKit.Panel(root, "Controls help", new Color(0f, 0f, 0f, 0.55f));
            UiKit.Place(help.rectTransform, Vector2.zero, Vector2.zero, new Vector2(16f, 14f), new Vector2(560f, 92f));
            var helpText = UiKit.Text(help.rectTransform, "Text",
                "<b>Left / Right</b> or <b>A / D</b> move   <b>Space</b> Blend on / off   <b>F</b> hit (station 6)\n" +
                "<size=88%><b>b</b> = the picture underneath, <b>s</b> = the layer being blended on top.\n" +
                "<b>Fixed-function</b>: the graphics card blends while drawing (fast, 9 modes).\n" +
                "<b>Compositor</b>: layers are blended into a picture first, like Photoshop (all 27).</size>",
                15, TextAlignmentOptions.MidlineLeft);
            UiKit.Stretch(helpText.rectTransform).offsetMin = new Vector2(12f, 0f);

            // Bottom-center: Prev / Blend / Next and the opacity slider.
            var bar = UiKit.Rect("Navigation", root);
            UiKit.Place(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(760f, 92f));
            UiKit.Stretch(UiKit.Panel(bar, "Panel", new Color(0f, 0f, 0f, 0.55f)).rectTransform);

            controller.prevButton = HudButton(bar, "Prev", "< Previous", new Vector2(10f, -8f), 170f);
            controller.blendButton = HudButton(bar, "Blend", "Blend: ON  (Space)", new Vector2(195f, -8f), 370f);
            controller.blendButtonLabel = controller.blendButton.GetComponentInChildren<TMP_Text>();
            controller.nextButton = HudButton(bar, "Next", "Next >", new Vector2(580f, -8f), 170f);

            var group = UiKit.Rect("Opacity", bar);
            UiKit.Place(group, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 6f), new Vector2(740f, 30f));
            controller.opacityGroup = group.gameObject;
            controller.opacityLabel = UiKit.Text(group, "Label", "Opacity 100%", 17, TextAlignmentOptions.MidlineLeft);
            UiKit.Place(controller.opacityLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(150f, 30f));
            controller.opacitySlider = UiKit.Slider(group, "Slider", 0f, 1f, 1f);
            UiKit.Place((RectTransform)controller.opacitySlider.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, 0f), new Vector2(570f, 20f));
            NoNavigation(controller.opacitySlider);

            // Bottom-right: station counter.
            var counterPanel = UiKit.Panel(root, "Counter", new Color(0f, 0f, 0f, 0.55f));
            UiKit.Place(counterPanel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 14f), new Vector2(300f, 92f));
            controller.counterLabel = UiKit.Text(counterPanel.rectTransform, "Text", "1 / 17", 34, TextAlignmentOptions.Center);
            UiKit.Stretch(controller.counterLabel.rectTransform);
        }

        static Button HudButton(RectTransform parent, string name, string label, Vector2 pos, float width)
        {
            var b = UiKit.Button(parent, name, label);
            UiKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(width, 40f));
            NoNavigation(b);
            return b;
        }

        public static void NoNavigation(Selectable s)
        {
            var nav = s.navigation;
            nav.mode = Navigation.Mode.None;
            s.navigation = nav;
        }

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        static readonly Vector2 TopLeft = new Vector2(0f, 1f);

    }

    // What a station's extra build step gets: the station, its canvas, and the control area on the right.
    public class StationContext
    {
        public Station station;
        public RectTransform canvas;
        public RectTransform controls;
    }

    public class StationDef
    {
        public Type type;
        public string title, caption, formula;
        public float opacity = 1f;
        public Action<StationContext> build;
    }
}
