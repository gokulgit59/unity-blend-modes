using UnityEngine;

namespace BlendMode.Showcase
{
    // Station 14: depth haze and light shafts.
    //   Offstage, three parallax layers (far mountains, mid hills, near trees) sway at different speeds.
    //   A pale haze sprite drawn with Screen sits in front of the far layer, a thinner one in front of the mid layer
    //   (Fixed-function), so distant things get lighter and flatter.
    //   The camera picture then gets diagonal light beams with Soft light (Compositor). Soft light needs the
    //   backdrop, so it cannot be Fixed-function.
    public class S14_HazeStation : Station
    {
        Transform _far, _mid, _near;
        readonly (Transform t, PixelCanvas art, float unitsWide)[] _layers = new (Transform, PixelCanvas, float)[3];

        public BlendLayerStack HazeStack { get; private set; }

        // Rows (in the 355 px camera picture, from the bottom) that show mostly one layer, for tests.
        public static readonly RectInt FarBand = new RectInt(0, 215, 860, 45), NearBand = new RectInt(0, 10, 860, 60);

        protected override void Build()
        {
            var stage = OffstageRoot("Offstage landscape");

            var sky = new PixelCanvas(4, 32, Color.white);
            sky.VerticalGradient(new Color(0.95f, 0.85f, 0.7f), new Color(0.45f, 0.6f, 0.85f));
            Fit(Sprite("Sky", Tex(sky, "Sky", FilterMode.Bilinear), Vector2.zero, 19f, 0, stage), new Vector2(19f, 7.2f));

            _far = Layer(stage, "Far mountains", 1, 2.2f, new Color(0.3f, 0.36f, 0.5f), new Color(0.22f, 0.27f, 0.4f),
                x => 34 + 14 * Mathf.Sin(x * 0.035f) + 7 * Mathf.Sin(x * 0.11f + 2f), 0f);
            Haze(stage, "Far haze (Screen)", 2, 0.6f, 0.55f);
            _mid = Layer(stage, "Mid hills", 3, 1.0f, new Color(0.18f, 0.38f, 0.3f), new Color(0.13f, 0.3f, 0.24f),
                x => 24 + 6 * Mathf.Sin(x * 0.06f + 1f) + 3 * Mathf.Sin(x * 0.17f), -0.6f);
            Haze(stage, "Mid haze (Screen)", 4, 0.3f, 0.2f);
            _near = Trees(stage, 5);

            var cam = StationCamera("Landscape camera", stage.position, 7.1f, 860, 355, new Color(0.7f, 0.75f, 0.85f));
            HazeStack = Stack("Light shaft compositor", 860, 355, live: true);
            AddLayer(HazeStack, cam.targetTexture);
            Control(HazeStack, AddLayer(HazeStack, Tex(Beams(), "Light beams", FilterMode.Bilinear), LayerBlendMode.SoftLight));
            StackView("Landscape", HazeStack, new Vector2(0f, -0.75f), new Vector2(17.2f, 7.1f), 0);
        }

        // Which parallax layer is on top at a world point: 2 near, 1 mid, 0 far, -1 sky. For tests.
        public int VisibleLayerAt(Vector2 world)
        {
            for (int i = 2; i >= 0; i--)
            {
                var (t, art, unitsWide) = _layers[i];
                float ppu = art.W / unitsWide;
                var local = (Vector2)t.position;
                int px = Mathf.FloorToInt((world.x - local.x) * ppu + art.W * 0.5f);
                int py = Mathf.FloorToInt((world.y - local.y) * ppu + art.H * 0.5f);
                if (px < 0 || py < 0 || px >= art.W || py >= art.H) continue;
                if (art.Px[py * art.W + px].a > 0.5f) return i;
            }
            return -1;
        }

        void Update()
        {
            if (_far == null) return;
            float s = Mathf.Sin(Time.time * 0.4f);
            _far.localPosition = new Vector3(s * 0.15f, _far.localPosition.y, 0f);
            _mid.localPosition = new Vector3(s * 0.5f, _mid.localPosition.y, 0f);
            _near.localPosition = new Vector3(s * 1.2f, _near.localPosition.y, 0f);
        }

        Transform Layer(Transform stage, string name, int order, float roughness, Color top, Color bottom, System.Func<int, float> height, float y)
        {
            var c = new PixelCanvas(200, 71, Color.clear);
            c.Silhouette(x => height(x), bottom);
            for (int x = 0; x < c.W; x++)                     // lighter ridges for a bit of texture
            {
                int h = Mathf.RoundToInt(height(x));
                for (int k = 0; k < 3 * roughness; k++) c.Set(x, h - 1 - k, top);
            }
            var sr = Sprite(name, Tex(c, name), new Vector2(0f, y), 20f, order, stage);
            _layers[order == 1 ? 0 : 1] = (sr.transform, c, 20f);
            return sr.transform;
        }

        void Haze(Transform stage, string name, int order, float alpha, float yFade)
        {
            // Pale blue-white, strongest at the bottom of the layer where it meets the ground.
            var c = new PixelCanvas(4, 64, Color.clear);
            for (int y = 0; y < c.H; y++)
            {
                float t = y / (float)(c.H - 1);
                float a = alpha * Mathf.Lerp(1f, yFade, t);
                for (int x = 0; x < c.W; x++) c.Px[y * c.W + x] = new Color(0.85f, 0.9f, 1f, a);
            }
            var mat = FixedMaterial(LayerBlendMode.Screen);
            Control(mat, LayerBlendMode.Screen);
            Fit(Sprite(name, Tex(c, name, FilterMode.Bilinear), new Vector2(0f, 0f), 19f, order, stage, mat), new Vector2(19f, 7.2f));
        }

        Transform Trees(Transform stage, int order)
        {
            var c = new PixelCanvas(220, 40, Color.clear);
            c.Rect(0, 0, 220, 6, new Color(0.08f, 0.16f, 0.1f));
            for (int x = 0; x < 220; x += 3) c.Rect(x, 5, 1, 2, new Color(0.2f, 0.36f, 0.16f));   // grass tufts
            for (int i = 0; i < 16; i++)
            {
                int x = 6 + i * 14 + (int)(ProcArt.Rand(i, 70) * 6);
                int h = 18 + (int)(ProcArt.Rand(i, 71) * 14);
                c.Rect(x + 3, 0, 2, 6, new Color(0.15f, 0.1f, 0.06f));          // trunk
                for (int k = 0; k < h; k++)                                     // canopy: a triangle above it
                {
                    int half = Mathf.Max(1, (h - k) / 3);
                    // lit left half, shaded right half: near things keep their full light-to-dark detail
                    c.Rect(x + 4 - half, 5 + k, half, 1, new Color(0.16f, 0.4f, 0.2f));
                    c.Rect(x + 4, 5 + k, half, 1, new Color(0.03f, 0.12f, 0.06f));
                }
            }
            var t = Sprite("Near trees", Tex(c, "Near trees"), new Vector2(0f, -2.6f), 22f, order, stage).transform;
            _layers[2] = (t, c, 22f);
            return t;
        }

        static PixelCanvas Beams()
        {
            var c = new PixelCanvas(172, 71, Color.clear);
            for (int y = 0; y < c.H; y++)
            for (int x = 0; x < c.W; x++)
            {
                float u = x + y * 0.55f;
                float band = Mathf.Repeat(u, 46f) / 46f;
                float beam = Mathf.Clamp01(1f - Mathf.Abs(band - 0.5f) * 5f);
                float fade = Mathf.Lerp(0.25f, 1f, y / (float)c.H);
                c.Px[y * c.W + x] = new Color(1f, 0.93f, 0.72f, beam * fade);
            }
            return c;
        }
    }
}
