using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BlendMode.Tests
{
    // Composites solid-color layers through BlendLayerStack + BlendCompositor and compares the result in sRGB.
    // Works in either color space: inputs are sRGB textures and the result is read back as stored (sRGB) bytes.
    public class BlendCompositorTests
    {
        const float Tolerance = 2f / 255f;
        const string ShaderPath = "Assets/BlendModes/Shaders/BlendCompositor.shader";

        readonly List<Object> _garbage = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _garbage) if (o != null) Object.DestroyImmediate(o);
            _garbage.Clear();
        }

        // ---------- the cases from the brief ----------

        static IEnumerable<TestCaseData> GrayCases()
        {
            yield return Gray(LayerBlendMode.Multiply, 0.5f, 0.5f, 0.25f);
            yield return Gray(LayerBlendMode.Screen, 0.5f, 0.5f, 0.75f);
            yield return Gray(LayerBlendMode.Overlay, 0.25f, 0.5f, 0.25f);
            yield return Gray(LayerBlendMode.Overlay, 0.25f, 0.75f, 0.375f);
            yield return Gray(LayerBlendMode.HardLight, 0.25f, 0.75f, 0.625f);
            yield return Gray(LayerBlendMode.SoftLight, 0.5f, 0.75f, 0.604f);
            yield return Gray(LayerBlendMode.ColorDodge, 0.25f, 0.5f, 0.5f);
            yield return Gray(LayerBlendMode.ColorBurn, 0.75f, 0.5f, 0.5f);
            yield return Gray(LayerBlendMode.LinearBurn, 0.6f, 0.7f, 0.3f);
            yield return Gray(LayerBlendMode.LinearDodge, 0.6f, 0.7f, 1.0f);
            yield return Gray(LayerBlendMode.LinearLight, 0.5f, 0.75f, 1.0f);
            yield return Gray(LayerBlendMode.VividLight, 0.5f, 0.25f, 0.0f);
            yield return Gray(LayerBlendMode.PinLight, 0.8f, 0.25f, 0.5f);
            yield return Gray(LayerBlendMode.HardMix, 0.6f, 0.5f, 1.0f);
            yield return Gray(LayerBlendMode.HardMix, 0.4f, 0.5f, 0.0f);
            yield return Gray(LayerBlendMode.Difference, 0.8f, 0.3f, 0.5f);
            yield return Gray(LayerBlendMode.Exclusion, 0.5f, 0.5f, 0.5f);
            yield return Gray(LayerBlendMode.Subtract, 0.6f, 0.2f, 0.4f);
            yield return Gray(LayerBlendMode.Divide, 0.3f, 0.6f, 0.5f);
        }

        static TestCaseData Gray(LayerBlendMode mode, float b, float s, float expected) =>
            new TestCaseData(mode, b, s, expected).SetName($"{mode}_b{b:0.00}_s{s:0.00}_is_{expected:0.000}");

        [TestCaseSource(nameof(GrayCases))]
        public void GrayBlend(LayerBlendMode mode, float b, float s, float expected)
        {
            var c = CompositeSolid(new Color(b, b, b, 1f), new Color(s, s, s, 1f), mode, 1f);
            AssertColor(new Color(expected, expected, expected), c, $"{mode} b={b} s={s}");
        }

        [Test]
        public void Color_GrayBackdrop_RedSource_Is_1_0286_0286()
        {
            var c = CompositeSolid(new Color(0.5f, 0.5f, 0.5f, 1f), new Color(1f, 0f, 0f, 1f), LayerBlendMode.Color, 1f);
            AssertColor(new Color(1f, 0.286f, 0.286f), c, "Color");
        }

        [Test]
        public void Normal_HalfOpacity_IsMidpoint()
        {
            var c = CompositeSolid(new Color(0.2f, 0.2f, 0.2f, 1f), new Color(0.8f, 0.8f, 0.8f, 1f), LayerBlendMode.Normal, 0.5f);
            AssertColor(new Color(0.5f, 0.5f, 0.5f), c, "Normal 50%");
        }

        [Test]
        public void Difference_TextureWithItself_IsBlack()
        {
            const int size = 16;
            var px = new Color32[size * size];
            var rng = new System.Random(1234);
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color32((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256), 255);
            var tex = Texture(size, px);

            var result = Composite(tex, tex, LayerBlendMode.Difference, 1f, size);
            float max = 0f;
            foreach (var p in result) max = Mathf.Max(max, p.r, p.g, p.b);
            TestContext.Out.WriteLine($"Difference self: max channel = {max:0.000} (expected 0)");
            Assert.LessOrEqual(max, Tolerance, "Difference of a texture with itself should be black");
        }

        [Test]
        public void Dissolve_HalfOpacity_ShowsAboutHalfSourcePixels()
        {
            const int size = 64;
            var black = Texture(size, Fill(size, new Color32(0, 0, 0, 255)));
            var white = Texture(size, Fill(size, new Color32(255, 255, 255, 255)));

            var result = Composite(black, white, LayerBlendMode.Dissolve, 0.5f, size);
            int fromSource = 0;
            foreach (var p in result)
            {
                bool isSource = p.r >= 1f - Tolerance && p.g >= 1f - Tolerance && p.b >= 1f - Tolerance;
                bool isBackdrop = p.r <= Tolerance && p.g <= Tolerance && p.b <= Tolerance;
                Assert.IsTrue(isSource || isBackdrop, $"Dissolve pixel {p} is neither source nor backdrop");
                if (isSource) fromSource++;
            }
            float fraction = fromSource / (float)result.Length;
            TestContext.Out.WriteLine($"Dissolve 50%: {fraction:P1} of pixels from source (expected 40%..60%)");
            Assert.That(fraction, Is.InRange(0.4f, 0.6f));
        }

        // ---------- layer options added for the showcase (defaults keep the original behavior) ----------

        [Test]
        public void ClipToBackdrop_HidesSourceWhereBackdropIsTransparent()
        {
            var clear = Texture(1, new Color32[] { new Color32(0, 0, 0, 0) });
            var red = Texture(1, new Color32[] { new Color32(255, 0, 0, 255) });

            var unclipped = Composite(clear, red, LayerBlendMode.Multiply, 1f, 1)[0];
            TestContext.Out.WriteLine($"Unclipped over transparent: {unclipped}");
            Assert.AreEqual(1f, unclipped.a, Tolerance, "Without clipping the source shows as Normal over a transparent backdrop");

            var clipped = Composite(clear, red, LayerBlendMode.Multiply, 1f, 1, l => l.clipToBackdrop = true)[0];
            TestContext.Out.WriteLine($"Clipped over transparent: {clipped}");
            Assert.AreEqual(0f, clipped.a, Tolerance, "With clipping nothing shows over a transparent backdrop");
        }

        [Test]
        public void UvOffset_ShiftsTheSourceLayer()
        {
            // Source: left half black, right half white. Offset by half a texture with Repeat wrap swaps them.
            const int size = 8;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = x < size / 2 ? new Color32(0, 0, 0, 255) : new Color32(255, 255, 255, 255);
            var source = Texture(size, px);
            source.wrapMode = TextureWrapMode.Repeat;
            var backdrop = Texture(size, Fill(size, new Color32(128, 128, 128, 255)));

            var plain = Composite(backdrop, source, LayerBlendMode.Normal, 1f, size);
            var shifted = Composite(backdrop, source, LayerBlendMode.Normal, 1f, size, l => l.uvOffset = new Vector2(0.5f, 0f));
            Assert.AreEqual(0f, plain[0].r, Tolerance, "Unshifted: left column is black");
            Assert.AreEqual(1f, shifted[0].r, Tolerance, "Shifted by half: left column is white");
        }

        // ---------- FixedBlend ----------

        static readonly HashSet<LayerBlendMode> FixedModes = new HashSet<LayerBlendMode>
        {
            LayerBlendMode.Normal, LayerBlendMode.Darken, LayerBlendMode.Multiply, LayerBlendMode.LinearBurn,
            LayerBlendMode.Lighten, LayerBlendMode.Screen, LayerBlendMode.LinearDodge, LayerBlendMode.Exclusion,
            LayerBlendMode.Subtract
        };

        static IEnumerable<TestCaseData> AllModes()
        {
            foreach (LayerBlendMode m in System.Enum.GetValues(typeof(LayerBlendMode)))
                yield return new TestCaseData(m).SetName($"FixedBlendApply_{m}_returns_{FixedModes.Contains(m).ToString().ToLowerInvariant()}");
        }

        [TestCaseSource(nameof(AllModes))]
        public void FixedBlendApply(LayerBlendMode mode)
        {
            var mat = Track(new Material(Shader.Find(FixedBlend.ShaderName)));
            bool expected = FixedModes.Contains(mode);
            Assert.AreEqual(expected, FixedBlend.Apply(mat, mode), $"FixedBlend.Apply({mode})");
            Assert.AreEqual(expected, FixedBlend.Supports(mode), $"FixedBlend.Supports({mode})");
        }

        [Test]
        public void FixedBlend_SupportsExactlyNineModes()
        {
            int supported = 0, unsupported = 0;
            foreach (LayerBlendMode m in System.Enum.GetValues(typeof(LayerBlendMode)))
                if (FixedBlend.Apply(null, m)) supported++; else unsupported++;
            Assert.AreEqual(9, supported);
            Assert.AreEqual(18, unsupported);
        }

        // ---------- helpers ----------

        Color CompositeSolid(Color backdrop, Color source, LayerBlendMode mode, float opacity)
        {
            var b = ExactSolid(backdrop);
            var s = ExactSolid(source);
            var c = Composite(b, s, mode, opacity, 1)[0];
            TestContext.Out.WriteLine($"{mode}: actual = ({c.r:0.000}, {c.g:0.000}, {c.b:0.000}, a {c.a:0.000})");
            return c;
        }

        Color[] Composite(Texture2D backdrop, Texture2D source, LayerBlendMode mode, float opacity, int size,
            System.Action<BlendLayer> configureSource = null)
        {
            var go = Track(new GameObject("BlendTest") { hideFlags = HideFlags.HideAndDontSave });
            var stack = go.AddComponent<BlendLayerStack>();
            stack.Shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.IsNotNull(stack.Shader, "BlendCompositor.shader not found at " + ShaderPath);
            stack.resolution = new Vector2Int(size, size);
            stack.blendInGamma = true;
            stack.layers.Add(new BlendLayer { texture = backdrop, mode = LayerBlendMode.Normal, opacity = 1f });
            var top = new BlendLayer { texture = source, mode = mode, opacity = opacity };
            configureSource?.Invoke(top);
            stack.layers.Add(top);
            stack.Composite();
            Assert.IsNotNull(stack.Result, "Composite produced no Result");

            var read = Track(new Texture2D(size, size, TextureFormat.RGBA32, false, false));
            var prev = RenderTexture.active;
            RenderTexture.active = stack.Result;
            read.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
            read.Apply();
            RenderTexture.active = prev;

            var pixels = read.GetPixels();
            Object.DestroyImmediate(stack);     // releases its RenderTextures and material
            return pixels;
        }

        Texture2D Texture(int size, Color32[] pixels)
        {
            var t = Track(new Texture2D(size, size, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            });
            t.SetPixels32(pixels);
            t.Apply();
            return t;
        }

        // A 1x1 float texture holding the exact value, so the expected numbers are not skewed by 8-bit input
        // rounding (0.25 would become 64/255). Float textures are never sRGB-decoded, so in a Linear project
        // store the linear equivalent; the shader then sees the same value an sRGB texture would give it.
        Texture2D ExactSolid(Color srgb)
        {
            var t = Track(new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            });
            t.SetPixel(0, 0, QualitySettings.activeColorSpace == ColorSpace.Linear ? srgb.linear : srgb);
            t.Apply();
            return t;
        }

        static Color32[] Fill(int size, Color32 c)
        {
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            return px;
        }

        T Track<T>(T o) where T : Object { _garbage.Add(o); return o; }

        static void AssertColor(Color expected, Color actual, string what)
        {
            TestContext.Out.WriteLine($"{what}: expected ({expected.r:0.000}, {expected.g:0.000}, {expected.b:0.000})");
            Assert.AreEqual(expected.r, actual.r, Tolerance, what + " (R)");
            Assert.AreEqual(expected.g, actual.g, Tolerance, what + " (G)");
            Assert.AreEqual(expected.b, actual.b, Tolerance, what + " (B)");
        }
    }
}
