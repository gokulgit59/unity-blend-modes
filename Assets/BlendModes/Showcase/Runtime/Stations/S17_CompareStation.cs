using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlendMode.Showcase
{
    // Station 17: a compare tool. Two cameras film two copies of the same little scene, where copy B has a few changes.
    // Difference of A and B (Compositor) is black wherever they match, so only the changes light up.
    public class S17_CompareStation : Station
    {
        public Toggle changesToggle;
        public bool showChanges = true;

        readonly List<GameObject> _changedInB = new List<GameObject>();
        readonly List<GameObject> _originalInB = new List<GameObject>();

        public BlendLayerStack CompareStack { get; private set; }
        public Camera CameraA { get; private set; }
        public Camera CameraB { get; private set; }
        public override string ExtraStatus => showChanges ? "B has 3 changes" : "B identical to A";

        // Where the changes are in B's camera picture (pixels from bottom-left), from the sprites' real bounds. For tests.
        public List<RectInt> ChangePixelRects()
        {
            var rects = new List<RectInt>();
            foreach (var go in _changedInB) rects.Add(PixelRect(go));
            foreach (var go in _originalInB) rects.Add(PixelRect(go));
            return rects;
        }

        RectInt PixelRect(GameObject go)
        {
            // From the sprite and transform, not Renderer.bounds, which is empty while the object is inactive.
            var sprite = go.GetComponent<SpriteRenderer>().sprite;
            var b = new Bounds(go.transform.TransformPoint(sprite.bounds.center), Vector3.Scale(sprite.bounds.size, go.transform.lossyScale));
            var min = CameraB.WorldToScreenPoint(b.min);
            var max = CameraB.WorldToScreenPoint(b.max);
            return new RectInt(Mathf.FloorToInt(min.x), Mathf.FloorToInt(min.y),
                Mathf.CeilToInt(max.x - min.x), Mathf.CeilToInt(max.y - min.y));
        }

        protected override void Build()
        {
            var a = OffstageRoot("Offstage scene A", new Vector2(-6f, 0f));
            var b = OffstageRoot("Offstage scene B", new Vector2(6f, 0f));
            BuildScene(a, false);
            BuildScene(b, true);

            const int w = 544, h = 320;
            CameraA = StationCamera("Camera A", a.position, 5.6f, w, h, new Color(0.6f, 0.8f, 1f));
            CameraB = StationCamera("Camera B", b.position, 5.6f, w, h, new Color(0.6f, 0.8f, 1f));

            CompareStack = Stack("Compare compositor", w, h, live: true);
            AddLayer(CompareStack, CameraA.targetTexture);
            Control(CompareStack, AddLayer(CompareStack, CameraB.targetTexture, LayerBlendMode.Difference));

            View("Image A", CameraA.targetTexture, new Vector2(-5.6f, 1.15f), new Vector2(5.1f, 3.0f), 0);
            View("Image B", CameraB.targetTexture, new Vector2(-5.6f, -2.6f), new Vector2(5.1f, 3.0f), 0);
            StackView("Difference", CompareStack, new Vector2(3.0f, -0.85f), new Vector2(10.8f, 6.35f), 0);

            if (changesToggle != null)
            {
                changesToggle.SetIsOnWithoutNotify(showChanges);
                changesToggle.onValueChanged.AddListener(SetShowChanges);
            }
            SetShowChanges(showChanges);
        }

        public void SetShowChanges(bool on)
        {
            showChanges = on;
            foreach (var go in _changedInB) go.SetActive(on);
            foreach (var go in _originalInB) go.SetActive(!on);
            UpdateLabel();
        }

        void BuildScene(Transform root, bool isB)
        {
            var sky = new PixelCanvas(4, 32, Color.white);
            sky.VerticalGradient(new Color(0.8f, 0.92f, 1f), new Color(0.45f, 0.7f, 1f));
            Fit(Sprite("Sky", Tex(sky, "Sky", FilterMode.Bilinear), Vector2.zero, 9.6f, 0, root), new Vector2(9.6f, 5.7f));

            var ground = new PixelCanvas(96, 18, new Color(0.45f, 0.72f, 0.35f));
            ground.Grain(3f, 0.1f, 61);
            Sprite("Ground", Tex(ground, "Ground"), new Vector2(0f, -2.0f), 9.6f, 1, root);

            var house = new PixelCanvas(30, 30, Color.clear);
            house.Rect(3, 0, 24, 17, new Color(0.95f, 0.88f, 0.72f));
            for (int y = 0; y < 13; y++) house.Rect(1 + y, 17 + y, 28 - 2 * y, 1, new Color(0.7f, 0.25f, 0.2f));
            house.Rect(13, 0, 5, 9, new Color(0.4f, 0.25f, 0.12f));
            house.Rect(6, 8, 5, 5, new Color(0.45f, 0.65f, 0.9f));
            house.Rect(20, 8, 5, 5, new Color(0.45f, 0.65f, 0.9f));
            Sprite("House", Tex(house, "House"), new Vector2(-2.2f, -0.35f), 3f, 2, root);

            var tree = new PixelCanvas(14, 22, Color.clear);
            tree.Rect(6, 0, 2, 8, new Color(0.4f, 0.27f, 0.15f));
            tree.Ellipse(7, 14, 7, 8, new Color(0.2f, 0.55f, 0.25f));
            Sprite("Tree", Tex(tree, "Tree"), new Vector2(2.2f, -0.2f), 1.6f, 2, root);

            var flower = new PixelCanvas(4, 8, Color.clear);
            flower.Rect(1, 0, 1, 5, new Color(0.15f, 0.45f, 0.15f));
            flower.Rect(0, 5, 3, 3, new Color(1f, 0.3f, 0.45f));
            var flowerTex = Tex(flower, "Flower");
            Sprite("Flower", flowerTex, new Vector2(-4f, -1.8f), 0.4f, 3, root);
            Sprite("Flower", flowerTex, new Vector2(0.4f, -1.9f), 0.4f, 3, root);
            var removable = Sprite("Flower (removed in B)", flowerTex, new Vector2(3.6f, -1.85f), 0.4f, 3, root).gameObject;

            Sprite("Hero", Tex(ProcArt.Character(CharacterPalette.Hero), "Hero"), new Vector2(0f, -1.1f), 0.9f, 4, root);

            if (!isB) return;

            // The three changes in B. Each has an "original" twin shown when changes are switched off.
            var bird = new PixelCanvas(10, 6, Color.clear);
            bird.Rect(0, 3, 4, 1, new Color(0.15f, 0.15f, 0.2f)); bird.Rect(4, 2, 2, 2, new Color(0.15f, 0.15f, 0.2f)); bird.Rect(6, 3, 4, 1, new Color(0.15f, 0.15f, 0.2f));
            _changedInB.Add(Sprite("Bird (added in B)", Tex(bird, "Bird"), new Vector2(2.2f, 1.9f), 0.9f, 3, root).gameObject);

            var window = new PixelCanvas(5, 5, new Color(1f, 0.6f, 0.15f));
            _changedInB.Add(Sprite("Window (orange in B)", Tex(window, "Window"), new Vector2(-2.85f, -0.8f), 0.5f, 3, root).gameObject);

            _originalInB.Add(removable);
            removable.SetActive(false);
        }
    }
}
