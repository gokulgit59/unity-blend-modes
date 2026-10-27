using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlendMode
{
    // Feeds every tile's BlendLayerStack the same two generated layers. Tile i uses LayerBlendMode i.
    [ExecuteAlways]
    public class BlendGallery : MonoBehaviour
    {
        [Tooltip("One stack per mode, in LayerBlendMode order.")]
        public List<BlendLayerStack> tiles = new List<BlendLayerStack>();
        public Slider opacitySlider;
        public Toggle gammaToggle;
        [Tooltip("Optional: shows the current opacity.")]
        public TMP_Text opacityLabel;
        [Tooltip("Optional: show the two input layers.")]
        public RawImage backdropPreview, sourcePreview;

        [Range(0f, 1f)] public float opacity = 1f;
        [Tooltip("Off draws every tile's source with plain Normal blending, for a before/after comparison.")]
        public bool blendOn = true;
        public bool blendInGamma = true;
        public Vector2Int textureSize = new Vector2Int(256, 256);

        public Texture2D Backdrop => _backdrop;
        public Texture2D Source => _source;

        Texture2D _backdrop, _source;
        bool _dirty;

        void OnEnable()
        {
            int w = Mathf.Max(8, textureSize.x), h = Mathf.Max(8, textureSize.y);
            _backdrop = GalleryTextures.ColorBars(w, h);
            _source = GalleryTextures.HueGradient(w, h);

            if (opacitySlider != null)
            {
                opacitySlider.SetValueWithoutNotify(opacity);
                opacitySlider.onValueChanged.AddListener(SetOpacity);
            }
            if (gammaToggle != null)
            {
                gammaToggle.SetIsOnWithoutNotify(blendInGamma);
                gammaToggle.onValueChanged.AddListener(SetBlendInGamma);
            }
            Apply();
        }

        void OnDisable()
        {
            if (opacitySlider != null) opacitySlider.onValueChanged.RemoveListener(SetOpacity);
            if (gammaToggle != null) gammaToggle.onValueChanged.RemoveListener(SetBlendInGamma);
            Kill(_backdrop); Kill(_source);
            _backdrop = _source = null;
        }

        void OnValidate() { _dirty = true; }

        void Update()
        {
            if (_dirty) Apply();
        }

        public void SetOpacity(float value)
        {
            opacity = Mathf.Clamp01(value);
            Apply();
        }

        public void SetBlendInGamma(bool on)
        {
            blendInGamma = on;
            Apply();
        }

        public void SetBlendOn(bool on)
        {
            blendOn = on;
            Apply();
        }

        public void Apply()
        {
            _dirty = false;
            if (_backdrop == null || _source == null) return;

            if (opacityLabel != null) opacityLabel.text = $"Opacity {Mathf.RoundToInt(opacity * 100f)}%";
            if (backdropPreview != null) backdropPreview.texture = _backdrop;
            if (sourcePreview != null) sourcePreview.texture = _source;

            for (int i = 0; i < tiles.Count; i++)
            {
                var stack = tiles[i];
                if (stack == null) continue;

                while (stack.layers.Count < 2) stack.layers.Add(new BlendLayer());
                var bottom = stack.layers[0];
                bottom.texture = _backdrop; bottom.mode = LayerBlendMode.Normal; bottom.opacity = 1f; bottom.visible = true;
                var top = stack.layers[1];
                top.texture = _source; top.mode = blendOn ? (LayerBlendMode)i : LayerBlendMode.Normal; top.opacity = opacity; top.visible = true;

                stack.blendInGamma = blendInGamma;
                stack.Composite();
            }
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
