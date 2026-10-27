using UnityEngine;
using UnityEngine.UI;

namespace BlendMode.Showcase
{
    // Station 4: a village rendered by a station camera, then graded by the compositor:
    //   layer 0  the camera's RenderTexture (Normal)
    //   layer 1  a deep blue, Multiply, strength = night amount         (darkness)
    //   layer 2  a warm orange, Soft light, strongest at dusk            (sunset color grade)
    //   layer 3  a cool blue, Soft light, strength = night amount        (moonlight color grade)
    public class S04_DayNightStation : Station
    {
        [Range(0f, 1f)] public float night = 0.6f;
        public Slider nightSlider;
        public TMPro.TMP_Text nightLabel;

        Controlled _dark, _dusk, _moon;
        Transform _cloud;
        Vector3 _cloudStart;

        public override bool HasOpacity => false;
        public override string ExtraStatus => night < 0.2f ? "Day" : night < 0.75f ? "Dusk" : "Night";

        protected override void Build()
        {
            var stage = OffstageRoot("Offstage village");
            BuildVillage(stage);

            const int w = 860, h = 355;
            var cam = StationCamera("Village camera", stage.position, 7.1f, w, h, new Color(0.55f, 0.78f, 0.98f));

            var stack = Stack("Day/night compositor", w, h, live: true);
            AddLayer(stack, cam.targetTexture);
            _dark = Control(stack, AddLayer(stack, Own(ProcArt.Solid(new Color(0.16f, 0.2f, 0.48f))), LayerBlendMode.Multiply), useOpacity: false);
            _dusk = Control(stack, AddLayer(stack, Own(ProcArt.Solid(new Color(1f, 0.55f, 0.25f))), LayerBlendMode.SoftLight), useOpacity: false);
            _moon = Control(stack, AddLayer(stack, Own(ProcArt.Solid(new Color(0.3f, 0.4f, 0.85f))), LayerBlendMode.SoftLight), useOpacity: false);
            StackView("Graded village", stack, new Vector2(0f, -0.75f), new Vector2(17.2f, 7.1f), 0);

            if (nightSlider != null)
            {
                nightSlider.SetValueWithoutNotify(night);
                nightSlider.onValueChanged.AddListener(SetNight);
            }
            SetNight(night);
        }

        public void SetNight(float value)
        {
            night = Mathf.Clamp01(value);
            _dark.strength = night * 0.85f;
            _dusk.strength = Mathf.Sin(night * Mathf.PI) * 0.7f;
            _moon.strength = night * 0.6f;
            if (nightLabel != null) nightLabel.text = $"Time of day: {ExtraStatus}  ({Mathf.RoundToInt(night * 100f)}% night)";
            Refresh();
        }

        void Update()
        {
            if (_cloud != null) _cloud.position = _cloudStart + Vector3.right * Mathf.Repeat(Time.time * 0.6f, 22f);
        }

        void BuildVillage(Transform stage)
        {
            var sky = new PixelCanvas(4, 64, Color.white);
            sky.VerticalGradient(new Color(0.85f, 0.93f, 1f), new Color(0.38f, 0.65f, 0.95f));
            Fit(Sprite("Sky", Tex(sky, "Sky", FilterMode.Bilinear), Vector2.zero, 17.4f, 0, stage), new Vector2(17.4f, 7.2f));

            var sun = new PixelCanvas(32, 32, Color.clear);
            sun.Circle(16, 16, 15, new Color(1f, 0.95f, 0.7f), 0.15f);
            Sprite("Sun", Tex(sun, "Sun", FilterMode.Bilinear), new Vector2(5.5f, 2.2f), 1.6f, 1, stage);

            var cloud = new PixelCanvas(40, 16, Color.clear);
            cloud.Ellipse(12, 7, 10, 6, Color.white); cloud.Ellipse(24, 9, 12, 7, Color.white); cloud.Ellipse(32, 6, 7, 5, Color.white);
            _cloud = Sprite("Cloud", Tex(cloud, "Cloud"), new Vector2(-11f, 2.3f), 3.5f, 2, stage).transform;
            _cloudStart = _cloud.position;

            var hills = new PixelCanvas(172, 40, Color.clear);
            hills.Silhouette(x => 22 + 7 * Mathf.Sin(x * 0.05f) + 4 * Mathf.Sin(x * 0.13f + 1f), new Color(0.42f, 0.66f, 0.38f));
            hills.Silhouette(x => 12 + 3 * Mathf.Sin(x * 0.09f + 2f), new Color(0.36f, 0.58f, 0.3f));
            Sprite("Hills", Tex(hills, "Hills"), new Vector2(0f, -1.55f), 17.2f, 3, stage);

            var houseColors = new[] { new Color(0.92f, 0.85f, 0.7f), new Color(0.85f, 0.6f, 0.45f), new Color(0.95f, 0.92f, 0.85f), new Color(0.7f, 0.75f, 0.85f) };
            var roofColors = new[] { new Color(0.75f, 0.25f, 0.2f), new Color(0.35f, 0.3f, 0.5f), new Color(0.55f, 0.3f, 0.15f), new Color(0.25f, 0.45f, 0.4f) };
            for (int i = 0; i < 4; i++)
                Sprite("House " + (i + 1), Tex(House(houseColors[i], roofColors[i]), "House"), new Vector2(-6f + i * 4f, -1.3f), 2.4f, 4 + i, stage);

            var tree = new PixelCanvas(12, 20, Color.clear);
            tree.Rect(5, 0, 2, 7, new Color(0.4f, 0.27f, 0.15f));
            tree.Ellipse(6, 12, 6, 8, new Color(0.2f, 0.5f, 0.25f));
            var treeTex = Tex(tree, "Tree");
            foreach (float x in new[] { -8f, -4f, 0f, 4.2f, 8f })
                Sprite("Tree", treeTex, new Vector2(x, -1.6f), 1.2f, 3, stage);

            var ground = new PixelCanvas(4, 4, new Color(0.4f, 0.62f, 0.32f));
            Fit(Sprite("Ground", Tex(ground, "Ground"), new Vector2(0f, -3.0f), 17.4f, 3, stage), new Vector2(17.4f, 1.3f));
        }

        static PixelCanvas House(Color wall, Color roof)
        {
            var c = new PixelCanvas(24, 26, Color.clear);
            c.Rect(2, 0, 20, 14, wall);
            for (int y = 0; y < 11; y++) c.Rect(y, 14 + y, 24 - 2 * y, 1, roof);
            c.Rect(10, 0, 4, 7, new Color(0.35f, 0.22f, 0.12f));                                   // door
            c.Rect(4, 6, 4, 4, new Color(0.45f, 0.65f, 0.85f)); c.Rect(16, 6, 4, 4, new Color(0.45f, 0.65f, 0.85f));
            return c;
        }
    }
}
