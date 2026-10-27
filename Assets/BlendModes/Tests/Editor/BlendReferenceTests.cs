using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BlendMode.Tests
{
    // Checks every mode on the GPU against BlendReference over a 12 x 12 grid of backdrop/source colors
    // (grays, primaries, secondaries and mixed colors), so every formula is covered, not just the spot checks.
    public class BlendReferenceTests
    {
        const float Tolerance = 2f / 255f;
        const double Nudge = 1.0 / 1024.0;   // used only to accept either side of a hard edge (Hard mix, Overlay at 0.5...)
        const string ShaderPath = "Assets/BlendModes/Shaders/BlendCompositor.shader";

        static readonly Color32[] Palette =
        {
            new Color32(0, 0, 0, 255), new Color32(64, 64, 64, 255), new Color32(128, 128, 128, 255),
            new Color32(191, 191, 191, 255), new Color32(255, 255, 255, 255),
            new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255), new Color32(0, 0, 255, 255),
            new Color32(255, 255, 0, 255), new Color32(230, 102, 26, 255), new Color32(51, 153, 204, 255),
            new Color32(128, 26, 179, 255)
        };

        static IEnumerable<TestCaseData> Modes()
        {
            foreach (LayerBlendMode m in System.Enum.GetValues(typeof(LayerBlendMode)))
                if (m != LayerBlendMode.Dissolve)   // Dissolve is random by design; it has its own test
                    yield return new TestCaseData(m).SetName($"GpuMatchesReference_{m}");
        }

        [TestCaseSource(nameof(Modes))]
        public void GpuMatchesReference(LayerBlendMode mode)
        {
            int n = Palette.Length;
            var backPx = new Color32[n * n];
            var srcPx = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                backPx[y * n + x] = Palette[x];   // backdrop varies along x
                srcPx[y * n + x] = Palette[y];    // source varies along y
            }

            var garbage = new List<Object>();
            try
            {
                var back = Tex(n, backPx, garbage);
                var src = Tex(n, srcPx, garbage);

                var go = new GameObject("RefTest") { hideFlags = HideFlags.HideAndDontSave };
                garbage.Add(go);
                var stack = go.AddComponent<BlendLayerStack>();
                stack.Shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
                stack.resolution = new Vector2Int(n, n);
                stack.layers.Add(new BlendLayer { texture = back });
                stack.layers.Add(new BlendLayer { texture = src, mode = mode });
                stack.Composite();

                var read = new Texture2D(n, n, TextureFormat.RGBA32, false, false);
                garbage.Add(read);
                var prev = RenderTexture.active;
                RenderTexture.active = stack.Result;
                read.ReadPixels(new Rect(0, 0, n, n), 0, 0, false);
                read.Apply();
                RenderTexture.active = prev;
                var gpu = read.GetPixels32();

                int failures = 0;
                float worst = 0f;
                string firstFailure = null;
                for (int i = 0; i < gpu.Length; i++)
                {
                    float err = BestError(mode, backPx[i], srcPx[i], gpu[i]);
                    worst = Mathf.Max(worst, err);
                    if (err > Tolerance)
                    {
                        failures++;
                        if (firstFailure == null)
                            firstFailure = $"b={backPx[i]} s={srcPx[i]} gpu={gpu[i]} err={err * 255f:0.0}/255";
                    }
                }
                TestContext.Out.WriteLine($"{mode}: {gpu.Length} pixels, worst error {worst * 255f:0.00}/255");
                Assert.AreEqual(0, failures, $"{mode}: {failures} of {gpu.Length} pixels off by more than 2/255. First: {firstFailure}");
            }
            finally
            {
                foreach (var o in garbage) if (o != null) Object.DestroyImmediate(o);
            }
        }

        // Error against the reference. At a hard edge (a threshold landing exactly on an input value)
        // either side is a correct answer, so the reference is also evaluated with tiny nudges.
        static float BestError(LayerBlendMode mode, Color32 b, Color32 s, Color32 gpu)
        {
            var bc = ToC(b);
            var sc = ToC(s);
            float best = Error(mode, bc, sc, gpu);
            if (best <= Tolerance) return best;
            foreach (var (db, ds) in new[] { (Nudge, 0.0), (-Nudge, 0.0), (0.0, Nudge), (0.0, -Nudge) })
                best = Mathf.Min(best, Error(mode, Shift(bc, db), Shift(sc, ds), gpu));
            return best;
        }

        static float Error(LayerBlendMode mode, BlendReference.C b, BlendReference.C s, Color32 gpu)
        {
            var e = BlendReference.Clamp(BlendReference.Blend(mode, b, s));
            return Mathf.Max(Mathf.Abs((float)e.r - gpu.r / 255f),
                Mathf.Max(Mathf.Abs((float)e.g - gpu.g / 255f), Mathf.Abs((float)e.b - gpu.b / 255f)));
        }

        static BlendReference.C ToC(Color32 c) => new BlendReference.C(c.r / 255.0, c.g / 255.0, c.b / 255.0);
        static BlendReference.C Shift(BlendReference.C c, double d) =>
            new BlendReference.C(Clamp01(c.r + d), Clamp01(c.g + d), Clamp01(c.b + d));
        static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        static Texture2D Tex(int n, Color32[] px, List<Object> garbage)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            t.SetPixels32(px);
            t.Apply();
            garbage.Add(t);
            return t;
        }
    }
}
