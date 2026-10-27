using UnityEngine;

namespace BlendMode.Showcase
{
    // Station 15: a pond with two ripple textures scrolling over it with Soft light (Compositor).
    // The ripples are clipped to the pond (clipToBackdrop) and scroll with the per-layer UV offset.
    public class S15_WaterStation : Station
    {
        BlendLayer _ripplesA, _ripplesB;

        public BlendLayerStack WaterStack { get; private set; }

        protected override void Build()
        {
            var grass = new PixelCanvas(172, 71, new Color(0.42f, 0.68f, 0.34f));
            grass.Grain(4f, 0.12f, 51);
            for (int i = 0; i < 40; i++)
                grass.Rect((int)(ProcArt.Rand(i, 52) * 172), (int)(ProcArt.Rand(i, 53) * 71), 1, 2, new Color(0.3f, 0.52f, 0.25f));
            Sprite("Grass", Tex(grass, "Grass"), new Vector2(0f, -0.75f), 17.2f, 0);

            // The pond: deep in the middle, lighter toward the shore, transparent outside.
            const int w = 300, h = 150;
            var pond = new PixelCanvas(w, h, Color.clear);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) continue;
                var c = Color.Lerp(new Color(0.12f, 0.35f, 0.55f), new Color(0.35f, 0.65f, 0.75f), Mathf.Pow(d, 3f));
                c.a = Mathf.Clamp01((1f - d) * 30f);
                pond.Px[y * w + x] = c;
            }
            var ripples = Own(ProcArt.Noise(128, 8f, 2, 91, Color.black, Color.white, true, "Ripples"));
            Caustics(ripples);

            WaterStack = Stack("Water compositor", w * 2, h * 2, live: true);
            AddLayer(WaterStack, Tex(pond, "Pond", FilterMode.Bilinear));
            _ripplesA = AddLayer(WaterStack, ripples, LayerBlendMode.SoftLight, 1f, clip: true);
            _ripplesA.uvTiling = new Vector2(3f, 1.5f);
            _ripplesB = AddLayer(WaterStack, ripples, LayerBlendMode.SoftLight, 1f, clip: true);
            _ripplesB.uvTiling = new Vector2(2.2f, 1.1f);
            Control(WaterStack, _ripplesA);
            Control(WaterStack, _ripplesB).strength = 0.6f;
            StackView("Pond", WaterStack, new Vector2(0f, -1.1f), new Vector2(11f, 5.5f), 1);

            var pad = new PixelCanvas(16, 10, Color.clear);
            pad.Ellipse(8, 5, 7.5f, 4.5f, new Color(0.25f, 0.6f, 0.25f));
            for (int x = 8; x < 16; x++) { pad.Set(x, 5, Color.clear); pad.Set(x + 1, 6, Color.clear); }   // the notch
            var padTex = Tex(pad, "Lily pad");
            Sprite("Lily pad", padTex, new Vector2(-2.5f, -0.2f), 1.2f, 3);
            Sprite("Lily pad", padTex, new Vector2(2.8f, -2.2f), 1.0f, 3);
        }

        // Turn smooth noise into bright ripple lines: high where the noise crosses 0.5.
        static void Caustics(Texture2D tex)
        {
            var px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                float v = 1f - Mathf.Abs(px[i].r - 0.5f) * 2f;
                v = Mathf.Pow(v, 6f);
                float g = 0.5f + v * 0.5f - 0.08f;           // around 50% gray, so Soft light changes little except on the lines
                px[i] = new Color(g, g, g, 1f);
            }
            tex.SetPixels(px);
            tex.Apply();
        }

        void Update()
        {
            if (_ripplesA == null) return;
            float t = Time.time;
            _ripplesA.uvOffset = new Vector2(t * 0.03f, t * 0.015f);
            _ripplesB.uvOffset = new Vector2(-t * 0.02f, t * 0.025f);
            WaterStack.MarkDirty();
        }
    }
}
