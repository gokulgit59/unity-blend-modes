using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using GpuBlend = UnityEngine.Rendering.BlendMode;

namespace BlendMode.Tests
{
    public class UIBlendImageTests
    {
        const string ShaderPath = "Assets/BlendModes/Shaders/UIFixedBlend.shader";
        GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("UIBlendImageTest", typeof(RectTransform), typeof(Image)) { hideFlags = HideFlags.HideAndDontSave };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        UIBlendImage Add(LayerBlendMode mode)
        {
            var blend = _go.AddComponent<UIBlendImage>();
            blend.Shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            blend.Mode = mode;
            return blend;
        }

        [Test]
        public void Screen_SetsUpTheGpuBlend()
        {
            var blend = Add(LayerBlendMode.Screen);
            var mat = blend.GetModifiedMaterial(Canvas.GetDefaultCanvasMaterial());
            Assert.AreEqual("Blend Mode/UIFixedBlend", mat.shader.name);
            Assert.IsTrue(blend.IsSupported);
            Assert.IsNull(blend.StatusMessage);
            Assert.AreEqual((float)GpuBlend.OneMinusDstColor, mat.GetFloat("_SrcBlend"));
            Assert.AreEqual((float)GpuBlend.One, mat.GetFloat("_DstBlend"));
            Assert.AreEqual(0f, mat.GetFloat("_IsNormal"));
        }

        [Test]
        public void Opacity_IsPassedToTheMaterial()
        {
            var blend = Add(LayerBlendMode.Multiply);
            blend.Opacity = 0.3f;
            var mat = blend.GetModifiedMaterial(Canvas.GetDefaultCanvasMaterial());
            Assert.AreEqual(0.3f, mat.GetFloat("_Opacity"), 1e-6f);
        }

        [Test]
        public void BackdropMode_FallsBackToNormal_WithAWarning()
        {
            var blend = Add(LayerBlendMode.Overlay);
            Material mat;
            // The component warns once in the Console; mute it here so test runs leave the Console clean.
            Debug.unityLogger.logEnabled = false;
            try { mat = blend.GetModifiedMaterial(Canvas.GetDefaultCanvasMaterial()); }
            finally { Debug.unityLogger.logEnabled = true; }
            Assert.IsFalse(blend.IsSupported);
            StringAssert.Contains("Overlay needs to read the backdrop", blend.StatusMessage);
            Assert.AreEqual(1f, mat.GetFloat("_IsNormal"));
            Assert.AreEqual((float)GpuBlend.SrcAlpha, mat.GetFloat("_SrcBlend"));
        }

        [Test]
        public void StencilFromAParentMask_IsKept()
        {
            var blend = Add(LayerBlendMode.Screen);
            var masked = new Material(Canvas.GetDefaultCanvasMaterial());
            masked.SetFloat("_Stencil", 1f);
            masked.SetFloat("_StencilComp", 3f);   // Equal, as a Mask child gets
            try
            {
                var mat = blend.GetModifiedMaterial(masked);
                Assert.AreEqual(1f, mat.GetFloat("_Stencil"));
                Assert.AreEqual(3f, mat.GetFloat("_StencilComp"));
            }
            finally { Object.DestroyImmediate(masked); }
        }

        [Test]
        public void Disabled_ReturnsTheBaseMaterial()
        {
            var blend = Add(LayerBlendMode.Screen);
            blend.enabled = false;
            var baseMat = Canvas.GetDefaultCanvasMaterial();
            Assert.AreSame(baseMat, blend.GetModifiedMaterial(baseMat));
        }
    }
}
