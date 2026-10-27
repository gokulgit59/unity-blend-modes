using UnityEngine;

namespace BlendMode.Showcase
{
    // A tiny CPU "paint program" for generating every sprite and texture in the showcase.
    // Coordinates are in pixels, origin bottom-left, like Texture2D.
    public class PixelCanvas
    {
        public readonly int W, H;
        public readonly Color[] Px;

        public PixelCanvas(int w, int h, Color fill)
        {
            W = w; H = h;
            Px = new Color[w * h];
            for (int i = 0; i < Px.Length; i++) Px[i] = fill;
        }

        public Color Get(int x, int y) => Px[Mathf.Clamp(y, 0, H - 1) * W + Mathf.Clamp(x, 0, W - 1)];

        public void Set(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            Px[y * W + x] = c;
        }

        // Paint c over the pixel with normal alpha blending.
        public void Paint(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || c.a <= 0f) return;
            int i = y * W + x;
            var d = Px[i];
            float a = c.a + d.a * (1f - c.a);
            if (a <= 0f) { Px[i] = Color.clear; return; }
            var rgb = (new Vector3(c.r, c.g, c.b) * c.a + new Vector3(d.r, d.g, d.b) * d.a * (1f - c.a)) / a;
            Px[i] = new Color(rgb.x, rgb.y, rgb.z, a);
        }

        public void Rect(int x, int y, int w, int h, Color c)
        {
            for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++) Paint(i, j, c);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color c, float soft = 0f)
        {
            int x0 = Mathf.FloorToInt(cx - rx - 1), x1 = Mathf.CeilToInt(cx + rx + 1);
            int y0 = Mathf.FloorToInt(cy - ry - 1), y1 = Mathf.CeilToInt(cy + ry + 1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = soft > 0f ? Mathf.Clamp01((1f - d) / soft) : (d <= 1f ? 1f : 0f);
                if (a > 0f) Paint(x, y, new Color(c.r, c.g, c.b, c.a * a));
            }
        }

        public void Circle(float cx, float cy, float r, Color c, float soft = 0f) => Ellipse(cx, cy, r, r, c, soft);

        // Filled triangle-ish mountain/roof: a polygon given by its outline heights per column.
        public void Silhouette(System.Func<int, float> heightAt, Color c)
        {
            for (int x = 0; x < W; x++)
            {
                int h = Mathf.RoundToInt(heightAt(x));
                for (int y = 0; y < Mathf.Min(h, H); y++) Paint(x, y, c);
            }
        }

        public void VerticalGradient(Color bottom, Color top)
        {
            for (int y = 0; y < H; y++)
            {
                var c = Color.Lerp(bottom, top, H > 1 ? y / (float)(H - 1) : 0f);
                for (int x = 0; x < W; x++) Px[y * W + x] = c;
            }
        }

        // Multiply existing colors by 1 +/- amount of value noise, for a hand-painted look.
        public void Grain(float scale, float amount, int seed)
        {
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                float n = (ProcArt.Fbm(x / scale, y / scale, 3, seed) - 0.5f) * 2f * amount;
                var c = Px[i];
                Px[i] = new Color(Mathf.Clamp01(c.r * (1 + n)), Mathf.Clamp01(c.g * (1 + n)), Mathf.Clamp01(c.b * (1 + n)), c.a);
            }
        }

        // Draw a 1-pixel outline around every opaque area.
        public void Outline(Color c)
        {
            var copy = (Color[])Px.Clone();
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (copy[y * W + x].a > 0.5f) continue;
                bool edge = false;
                for (int k = 0; k < 4 && !edge; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx >= 0 && ny >= 0 && nx < W && ny < H && copy[ny * W + nx].a > 0.5f) edge = true;
                }
                if (edge) Px[y * W + x] = c;
            }
        }

        public Texture2D ToTexture(string name, FilterMode filter = FilterMode.Point, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false, false) { name = name, filterMode = filter, wrapMode = wrap };
            t.SetPixels(Px);
            t.Apply(false, false);
            return t;
        }
    }

    public struct CharacterPalette
    {
        public Color skin, hair, shirt, shirtShade, pants, boots, outline;

        public static CharacterPalette Hero => new CharacterPalette
        {
            skin = new Color(0.96f, 0.76f, 0.6f), hair = new Color(0.45f, 0.25f, 0.12f),
            shirt = new Color(0.2f, 0.55f, 0.85f), shirtShade = new Color(0.12f, 0.36f, 0.62f),
            pants = new Color(0.35f, 0.3f, 0.25f), boots = new Color(0.22f, 0.15f, 0.1f),
            outline = new Color(0.08f, 0.06f, 0.08f)
        };

        public static CharacterPalette Knight => new CharacterPalette
        {
            skin = new Color(0.93f, 0.72f, 0.56f), hair = new Color(0.85f, 0.7f, 0.3f),
            shirt = new Color(0.8f, 0.22f, 0.2f), shirtShade = new Color(0.55f, 0.12f, 0.12f),
            pants = new Color(0.55f, 0.57f, 0.62f), boots = new Color(0.3f, 0.3f, 0.35f),
            outline = new Color(0.08f, 0.06f, 0.08f)
        };

        // Grays only, with clear light, mid and dark values so a Color blend can recolor it and keep the shading.
        public static CharacterPalette Gray => new CharacterPalette
        {
            skin = new Color(0.82f, 0.82f, 0.82f), hair = new Color(0.3f, 0.3f, 0.3f),
            shirt = new Color(0.62f, 0.62f, 0.62f), shirtShade = new Color(0.42f, 0.42f, 0.42f),
            pants = new Color(0.5f, 0.5f, 0.5f), boots = new Color(0.25f, 0.25f, 0.25f),
            outline = new Color(0.1f, 0.1f, 0.1f)
        };
    }

    public static class ProcArt
    {
        // ---------- noise ----------

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144269504);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        // Smooth value noise in 0..1. With period > 0 it tiles every 'period' units.
        public static float ValueNoise(float x, float y, int seed, int period = 0)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int x1 = x0 + 1, y1 = y0 + 1;
            if (period > 0) { x0 = Mod(x0, period); x1 = Mod(x1, period); y0 = Mod(y0, period); y1 = Mod(y1, period); }
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        public static float Fbm(float x, float y, int octaves, int seed, int period = 0)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                sum += ValueNoise(x, y, seed + o * 31, period) * amp;
                norm += amp;
                x *= 2; y *= 2; amp *= 0.5f;
                if (period > 0) period *= 2;
            }
            return sum / norm;
        }

        public static float Rand(int i, int seed) => Hash(i, 7919, seed);

        // ---------- textures ----------

        public static Texture2D Solid(Color c, string name = "Solid")
        {
            var p = new PixelCanvas(4, 4, c);
            return p.ToTexture(name);
        }

        // Radial gradient from 'center' to 'edge'. Alpha falls off as (1 - d)^power.
        public static Texture2D Radial(int w, int h, Color center, Color edge, float power, string name = "Radial")
        {
            var p = new PixelCanvas(w, h, Color.clear);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                var c = Color.Lerp(center, edge, d);
                c.a = Mathf.Pow(1f - d, power) * Mathf.Lerp(center.a, edge.a, d);
                p.Px[y * w + x] = c;
            }
            return p.ToTexture(name, FilterMode.Bilinear);
        }

        // Grayscale noise between lo and hi. Tileable when tile is true.
        public static Texture2D Noise(int size, float cells, int octaves, int seed, Color lo, Color hi, bool tile, string name = "Noise")
        {
            var p = new PixelCanvas(size, size, Color.clear);
            int period = tile ? Mathf.RoundToInt(cells) : 0;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                p.Px[y * size + x] = Color.Lerp(lo, hi, Fbm(x * cells / size, y * cells / size, octaves, seed, period));
            return p.ToTexture(name, FilterMode.Bilinear, tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp);
        }

        // A 16 x 24 pixel character built from rectangles, with a 1 px outline (18 x 26 total).
        public static PixelCanvas Character(CharacterPalette p)
        {
            var c = new PixelCanvas(18, 26, Color.clear);
            int o = 1;   // outline margin
            // legs and boots
            c.Rect(o + 5, o + 0, 2, 2, p.boots); c.Rect(o + 9, o + 0, 2, 2, p.boots);
            c.Rect(o + 5, o + 2, 2, 4, p.pants); c.Rect(o + 9, o + 2, 2, 4, p.pants);
            // body with a shaded side and a belt
            c.Rect(o + 4, o + 6, 8, 8, p.shirt);
            c.Rect(o + 9, o + 6, 3, 8, p.shirtShade);
            c.Rect(o + 4, o + 6, 8, 1, p.boots);
            // arms and hands
            c.Rect(o + 2, o + 8, 2, 6, p.shirtShade); c.Rect(o + 12, o + 8, 2, 6, p.shirtShade);
            c.Rect(o + 2, o + 7, 2, 1, p.skin); c.Rect(o + 12, o + 7, 2, 1, p.skin);
            // head, hair, eyes
            c.Rect(o + 5, o + 14, 6, 7, p.skin);
            c.Rect(o + 5, o + 19, 6, 3, p.hair); c.Rect(o + 4, o + 16, 1, 5, p.hair);
            c.Rect(o + 6, o + 17, 1, 1, p.outline); c.Rect(o + 9, o + 17, 1, 1, p.outline);
            c.Outline(p.outline);
            return c;
        }

        // Same shape, all white: for flashes and silhouettes.
        public static PixelCanvas Silhouette(PixelCanvas src, Color color)
        {
            var c = new PixelCanvas(src.W, src.H, Color.clear);
            for (int i = 0; i < src.Px.Length; i++)
                if (src.Px[i].a > 0f) c.Px[i] = new Color(color.r, color.g, color.b, src.Px[i].a * color.a);
            return c;
        }

        // Pad a canvas into a bigger transparent one, centered horizontally, at 'bottom' pixels from the bottom.
        public static PixelCanvas Pad(PixelCanvas src, int w, int h, int bottom)
        {
            var c = new PixelCanvas(w, h, Color.clear);
            int ox = (w - src.W) / 2;
            for (int y = 0; y < src.H; y++)
            for (int x = 0; x < src.W; x++) c.Set(x + ox, y + bottom, src.Px[y * src.W + x]);
            return c;
        }

        public static Sprite ToSprite(Texture2D tex, float unitsWide, Vector2 pivot)
        {
            float ppu = tex.width / unitsWide;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu, 0, SpriteMeshType.FullRect);
        }
    }
}
