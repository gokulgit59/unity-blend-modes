using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlendMode
{
    [Serializable]
    public class BlendLayer
    {
        public Texture texture;
        public LayerBlendMode mode = LayerBlendMode.Normal;
        [Range(0f, 1f)] public float opacity = 1f;
        public bool visible = true;

        [Tooltip("Like a clipping mask: the layer only shows where the layers below it are opaque.")]
        public bool clipToBackdrop = false;
        [Tooltip("Texture tiling for this layer. Use a Repeat wrap mode on the texture to tile or scroll.")]
        public Vector2 uvTiling = Vector2.one;
        [Tooltip("Texture offset for this layer, in UV units. Animate it to scroll.")]
        public Vector2 uvOffset = Vector2.zero;
    }

    [ExecuteAlways]
    public class BlendLayerStack : MonoBehaviour
    {
        const string ShaderName = "Blend Mode/BlendCompositor";

        [Tooltip("Assign BlendCompositor.shader here so builds keep it. Shader.Find is only a fallback.")]
        [SerializeField] Shader shader;
        public List<BlendLayer> layers = new List<BlendLayer>();
        public Vector2Int resolution = new Vector2Int(1024, 1024);

        [Tooltip("Blend in gamma space like Photoshop, Illustrator and Figma. Only matters in a Linear color space project.")]
        public bool blendInGamma = true;

        [Tooltip("Re-composite every frame. Turn off for static layers and call Composite() yourself.")]
        public bool continuous = false;

        [Tooltip("How Result is sampled when shown bigger or smaller. Point keeps pixel art crisp and avoids dark edges around transparent pixels.")]
        public FilterMode filterMode = FilterMode.Bilinear;

        [Tooltip("Optional: a RawImage that should show the result.")]
        public RawImage preview;

        public RenderTexture Result { get; private set; }

        public Shader Shader
        {
            get => shader;
            set { shader = value; KillMaterial(); _dirty = true; }
        }

        static readonly int SourceTexId = Shader.PropertyToID("_SourceTex");
        static readonly int ModeId = Shader.PropertyToID("_Mode");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        static readonly int GammaId = Shader.PropertyToID("_GammaBlend");
        static readonly int SourceStId = Shader.PropertyToID("_SourceST");
        static readonly int ClipId = Shader.PropertyToID("_ClipToBackdrop");

        Material _mat;
        RenderTexture _a, _b;
        bool _dirty = true;

        void OnEnable()
        {
            _dirty = true;
            // Edit mode only ticks Update when the scene changes, so show something as soon as the scene opens.
            if (!Application.isPlaying) Composite();
        }
        void OnDisable() { ReleaseAll(); }
        void OnDestroy() { ReleaseAll(); }
        void OnValidate() { _dirty = true; }

        void Update()
        {
            if (continuous || _dirty) Composite();
        }

        public void MarkDirty() { _dirty = true; }

        public void Composite()
        {
            _dirty = false;
            if (!EnsureResources()) return;

            var prevActive = RenderTexture.active;

            int remaining = 0;
            foreach (var layer in layers) if (IsDrawn(layer)) remaining++;

            if (remaining == 0)
            {
                Clear(Result);
                if (preview != null) preview.texture = Result;
                return;
            }

            // Ping-pong between _a and _b, but write the last layer straight into Result.
            // That saves the full-screen copy into Result that would otherwise follow.
            Clear(_a);
            RenderTexture read = _a;

            foreach (var layer in layers)
            {
                if (!IsDrawn(layer)) continue;
                remaining--;
                var write = remaining == 0 ? Result : (read == _a ? _b : _a);

                _mat.SetTexture(SourceTexId, layer.texture);
                _mat.SetInteger(ModeId, (int)layer.mode);
                _mat.SetFloat(OpacityId, Mathf.Clamp01(layer.opacity));
                _mat.SetVector(SourceStId, new Vector4(layer.uvTiling.x, layer.uvTiling.y, layer.uvOffset.x, layer.uvOffset.y));
                _mat.SetFloat(ClipId, layer.clipToBackdrop ? 1f : 0f);
                Graphics.Blit(read, write, _mat);      // read becomes _MainTex (the backdrop)
                read = write;
            }

            RenderTexture.active = prevActive;          // Blit leaves its target active
            if (preview != null) preview.texture = Result;
        }

        static bool IsDrawn(BlendLayer layer) => layer != null && layer.visible && layer.texture != null;

        bool EnsureResources()
        {
            if (_mat == null)
            {
                var sh = shader != null ? shader : Shader.Find(ShaderName);
                if (sh == null) { Debug.LogError("BlendCompositor shader not found. Assign it on BlendLayerStack.", this); return false; }
                _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }
            _mat.SetFloat(GammaId, blendInGamma ? 1f : 0f);

            int w = Mathf.Max(1, resolution.x), h = Mathf.Max(1, resolution.y);
            if (Result == null || _a == null || _b == null || Result.width != w || Result.height != h)
            {
                ReleaseTargets();
                _a = NewTarget(w, h, "BlendLayerStack A");
                _b = NewTarget(w, h, "BlendLayerStack B");
                Result = NewTarget(w, h, "BlendLayerStack Result");
            }
            if (Result.filterMode != filterMode) Result.filterMode = filterMode;
            return true;
        }

        static RenderTexture NewTarget(int w, int h, string name)
        {
            var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            rt.Create();
            return rt;
        }

        static void Clear(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = prev;
        }

        void ReleaseTargets()
        {
            if (preview != null && Result != null && preview.texture == Result) preview.texture = null;
            Kill(_a); Kill(_b); Kill(Result);
            _a = _b = null; Result = null;
        }

        void KillMaterial()
        {
            Kill(_mat); _mat = null;
        }

        void ReleaseAll()
        {
            ReleaseTargets();
            KillMaterial();
        }

        static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (o is RenderTexture rt)
            {
                if (RenderTexture.active == rt) RenderTexture.active = null;
                rt.Release();
            }
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
