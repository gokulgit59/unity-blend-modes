using UnityEngine;

namespace BlendMode
{
    // The two test layers used by the gallery, generated in code so there are no external assets.
    public static class GalleryTextures
    {
        static readonly Color[] Bars =
        {
            Color.white, new Color(1f, 1f, 0f), new Color(0f, 1f, 1f), new Color(0f, 1f, 0f),
            new Color(1f, 0f, 1f), new Color(1f, 0f, 0f), new Color(0f, 0f, 1f), Color.black
        };

        // 8 vertical color bars on top, a black-to-white gray ramp across the bottom quarter.
        public static Texture2D ColorBars(int width, int height)
        {
            var px = new Color32[width * height];
            int rampHeight = Mathf.Max(1, height / 4);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Color c;
                if (y < rampHeight)
                {
                    float g = width > 1 ? x / (float)(width - 1) : 0f;
                    c = new Color(g, g, g, 1f);
                }
                else
                {
                    c = Bars[Mathf.Min(Bars.Length - 1, x * Bars.Length / width)];
                }
                px[y * width + x] = c;
            }
            return Create("Gallery Backdrop (color bars)", width, height, px);
        }

        // A horizontal hue gradient: red, yellow, green, cyan, blue, magenta, back to red.
        public static Texture2D HueGradient(int width, int height)
        {
            var px = new Color32[width * height];
            for (int x = 0; x < width; x++)
            {
                Color32 c = Color.HSVToRGB(x / (float)width, 1f, 1f);
                for (int y = 0; y < height; y++) px[y * width + x] = c;
            }
            return Create("Gallery Source (hue gradient)", width, height, px);
        }

        // Color data, so sRGB (linear: false). Not saved with the scene.
        public static Texture2D Create(string name, int width, int height, Color32[] pixels)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = name,
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }
    }
}
