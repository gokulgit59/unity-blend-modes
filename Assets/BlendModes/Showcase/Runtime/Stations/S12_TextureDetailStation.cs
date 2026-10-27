using UnityEngine;

namespace BlendMode.Showcase
{
    // Station 12: flat stone art with a grain-and-cracks texture on top.
    // Left: Overlay. Right: Soft light. Same two layers, two compositor stacks.
    public class S12_TextureDetailStation : Station
    {
        public BlendLayerStack OverlayStack { get; private set; }
        public BlendLayerStack SoftLightStack { get; private set; }

        protected override void Build()
        {
            var stone = Tex(FlatStone(), "Flat stone");
            var detail = Tex(GrainAndCracks(), "Grain and cracks", FilterMode.Bilinear);

            OverlayStack = Panel("Overlay", stone, detail, LayerBlendMode.Overlay, -4.3f);
            SoftLightStack = Panel("Soft light", stone, detail, LayerBlendMode.SoftLight, 4.3f);
        }

        BlendLayerStack Panel(string name, Texture2D stone, Texture2D detail, LayerBlendMode mode, float x)
        {
            var stack = Stack(name + " compositor", 480, 384);
            AddLayer(stack, stone);
            Control(stack, AddLayer(stack, detail, mode));
            StackView(name, stack, new Vector2(x, -1.0f), new Vector2(7.6f, 6.08f), 0);
            return stack;
        }

        // Flat-colored stone blocks, the kind of art that looks plain without texture.
        static PixelCanvas FlatStone()
        {
            var c = new PixelCanvas(120, 96, new Color(0.3f, 0.29f, 0.3f));
            var tones = new[] { new Color(0.55f, 0.55f, 0.6f), new Color(0.6f, 0.56f, 0.5f), new Color(0.5f, 0.52f, 0.56f), new Color(0.58f, 0.58f, 0.55f) };
            int row = 0;
            for (int y = 2; y < c.H; y += 24, row++)
            for (int x = (row % 2) * -20 + 2, i = 0; x < c.W; x += 40, i++)
                c.Rect(x, y, 38, 22, tones[(row * 3 + i) % tones.Length]);
            return c;
        }

        // Gray around 50% (neutral for both modes), with fine grain, blotches, and dark cracks.
        static PixelCanvas GrainAndCracks()
        {
            const int w = 240, h = 192;
            var c = new PixelCanvas(w, h, Color.gray);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float blotch = ProcArt.Fbm(x / 28f, y / 28f, 3, 5) - 0.5f;
                float grain = ProcArt.ValueNoise(x * 0.9f, y * 0.9f, 8) - 0.5f;
                float v = 0.5f + blotch * 0.7f + grain * 0.35f;
                c.Px[y * w + x] = new Color(v, v, v, 1f);
            }
            for (int k = 0; k < 9; k++)                       // cracks: random walks
            {
                float x = ProcArt.Rand(k, 40) * w, y = ProcArt.Rand(k, 41) * h;
                float angle = ProcArt.Rand(k, 42) * Mathf.PI * 2f;
                for (int step = 0; step < 70; step++)
                {
                    angle += (ProcArt.Rand(step + k * 100, 43) - 0.5f) * 0.9f;
                    x += Mathf.Cos(angle); y += Mathf.Sin(angle);
                    c.Set((int)x, (int)y, new Color(0.12f, 0.12f, 0.12f));
                    c.Set((int)x + 1, (int)y, new Color(0.3f, 0.3f, 0.3f));
                    c.Set((int)x - 1, (int)y + 1, new Color(0.78f, 0.78f, 0.78f));   // a light edge for depth
                }
            }
            return c;
        }
    }
}
