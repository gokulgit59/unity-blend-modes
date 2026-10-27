using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BlendMode.Tests
{
    // Edge cases for BlendLayerStack: empty stacks, missing textures, hidden layers, resizing, opacity limits, cleanup.
    public class BlendLayerStackRobustnessTests
    {
        const string ShaderPath = "Assets/BlendModes/Shaders/BlendCompositor.shader";
        GameObject _go;
        BlendLayerStack _stack;
        Texture2D _red, _blue;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("RobustnessTest") { hideFlags = HideFlags.HideAndDontSave };
            _stack = _go.AddComponent<BlendLayerStack>();
            _stack.Shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            _stack.resolution = new Vector2Int(4, 4);
            _red = Solid(new Color32(255, 0, 0, 255));
            _blue = Solid(new Color32(0, 0, 255, 255));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_red);
            Object.DestroyImmediate(_blue);
        }

        static Texture2D Solid(Color32 c)
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point };
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        Color32 Read()
        {
            var tex = new Texture2D(_stack.Result.width, _stack.Result.height, TextureFormat.RGBA32, false, false);
            var prev = RenderTexture.active;
            RenderTexture.active = _stack.Result;
            tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0, false);
            tex.Apply();
            RenderTexture.active = prev;
            var c = tex.GetPixels32()[0];
            Object.DestroyImmediate(tex);
            return c;
        }

        [Test]
        public void NoLayers_GivesTransparentResult()
        {
            _stack.Composite();
            Assert.IsNotNull(_stack.Result);
            Assert.AreEqual(0, Read().a);
        }

        [Test]
        public void NullTextureAndNullLayer_AreSkipped()
        {
            _stack.layers.Add(new BlendLayer { texture = _red });
            _stack.layers.Add(null);
            _stack.layers.Add(new BlendLayer { texture = null, mode = LayerBlendMode.Multiply });
            _stack.Composite();
            var c = Read();
            Assert.AreEqual(255, c.r); Assert.AreEqual(0, c.b); Assert.AreEqual(255, c.a);
        }

        [Test]
        public void HiddenLayer_IsSkipped()
        {
            _stack.layers.Add(new BlendLayer { texture = _red });
            _stack.layers.Add(new BlendLayer { texture = _blue, visible = false });
            _stack.Composite();
            Assert.AreEqual(255, Read().r);
        }

        [Test]
        public void SingleLayer_IsCopiedExactly()
        {
            _stack.layers.Add(new BlendLayer { texture = _blue });
            _stack.Composite();
            var c = Read();
            Assert.AreEqual(0, c.r); Assert.AreEqual(255, c.b); Assert.AreEqual(255, c.a);
        }

        [Test]
        public void OpacityZero_LeavesBackdrop_OpacityAboveOne_IsClamped()
        {
            _stack.layers.Add(new BlendLayer { texture = _red });
            var top = new BlendLayer { texture = _blue, opacity = 0f };
            _stack.layers.Add(top);
            _stack.Composite();
            Assert.AreEqual(255, Read().r, "opacity 0 changes nothing");

            top.opacity = 5f;   // out of range from code: clamped to 1
            _stack.Composite();
            var c = Read();
            Assert.AreEqual(0, c.r); Assert.AreEqual(255, c.b);
        }

        [Test]
        public void ManyLayers_PingPongEndsInResult()
        {
            // Odd and even layer counts take different ping-pong paths; both must end in Result.
            for (int n = 1; n <= 6; n++)
            {
                _stack.layers.Clear();
                for (int i = 0; i < n; i++) _stack.layers.Add(new BlendLayer { texture = i % 2 == 0 ? _red : _blue });
                _stack.Composite();
                var c = Read();
                bool lastIsRed = (n - 1) % 2 == 0;
                Assert.AreEqual(lastIsRed ? 255 : 0, c.r, $"{n} layers");
                Assert.AreEqual(lastIsRed ? 0 : 255, c.b, $"{n} layers");
            }
        }

        [Test]
        public void ResolutionChange_RecreatesResult_AndZeroIsClamped()
        {
            _stack.layers.Add(new BlendLayer { texture = _red });
            _stack.Composite();
            Assert.AreEqual(4, _stack.Result.width);

            _stack.resolution = new Vector2Int(16, 8);
            _stack.Composite();
            Assert.AreEqual(16, _stack.Result.width);
            Assert.AreEqual(8, _stack.Result.height);

            _stack.resolution = Vector2Int.zero;
            _stack.Composite();
            Assert.AreEqual(1, _stack.Result.width, "a zero resolution is clamped to 1, not an error");
        }

        [Test]
        public void DisableReleases_EnableRecreates()
        {
            _stack.layers.Add(new BlendLayer { texture = _red });
            _stack.Composite();
            _stack.enabled = false;
            Assert.IsNull(_stack.Result, "disabling releases the RenderTextures");
            _stack.enabled = true;
            _stack.Composite();
            Assert.IsNotNull(_stack.Result);
            Assert.AreEqual(255, Read().r);
        }

        [Test]
        public void MissingShader_FallsBackToShaderFind()
        {
            _stack.Shader = null;
            _stack.layers.Add(new BlendLayer { texture = _red });
            _stack.Composite();
            Assert.AreEqual(255, Read().r);
        }
    }
}
