using UnityEngine;
using UnityEngine.UI;

namespace BlendMode
{
    // Gives a uGUI Image, RawImage or Text a blend mode, like the layer blend mode dropdown in Photoshop or Figma.
    // It blends live against whatever the canvas has drawn beneath it, so it only offers the 9 modes the GPU
    // can do without reading the backdrop (see FixedBlend). For the other 18, flatten the layers with a
    // BlendLayerStack and show its Result in a RawImage.
    // Works with Mask, RectMask2D and sprite atlases.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public class UIBlendImage : MonoBehaviour, IMaterialModifier
    {
        public const string ShaderName = "Blend Mode/UIFixedBlend";

        [Tooltip("Assign UIFixedBlend.shader so builds keep it. Shader.Find is only a fallback.")]
        [SerializeField] Shader shader;
        [SerializeField] LayerBlendMode mode = LayerBlendMode.Screen;
        [SerializeField, Range(0f, 1f)] float opacity = 1f;

        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        static readonly int[] StencilIds =
        {
            Shader.PropertyToID("_Stencil"), Shader.PropertyToID("_StencilComp"), Shader.PropertyToID("_StencilOp"),
            Shader.PropertyToID("_StencilReadMask"), Shader.PropertyToID("_StencilWriteMask"), Shader.PropertyToID("_ColorMask")
        };

        Material _mat;
        Graphic _graphic;
        bool _warned;

        public LayerBlendMode Mode
        {
            get => mode;
            set { mode = value; _warned = false; SetDirty(); }
        }

        public float Opacity
        {
            get => opacity;
            set { opacity = Mathf.Clamp01(value); SetDirty(); }
        }

        public Shader Shader
        {
            get => shader;
            set { shader = value; Kill(); SetDirty(); }
        }

        // True when the current mode can be drawn live on UI. False means it draws as Normal.
        public bool IsSupported => FixedBlend.Supports(mode);

        // Null when the mode draws as chosen, otherwise why it falls back to Normal.
        public string StatusMessage => IsSupported ? null :
            $"{mode} needs to read the backdrop, so it can't be drawn live on UI. It draws as Normal. " +
            "Use a BlendLayerStack and show its Result in a RawImage for this mode.";

        Graphic Graphic => _graphic != null ? _graphic : (_graphic = GetComponent<Graphic>());

        void OnEnable() { SetDirty(); }
        void OnDisable() { SetDirty(); }
        void OnDestroy() { Kill(); }
        void OnValidate() { _warned = false; SetDirty(); }

        void SetDirty()
        {
            if (Graphic != null) Graphic.SetMaterialDirty();
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!isActiveAndEnabled) return baseMaterial;

            if (_mat == null)
            {
                var sh = shader != null ? shader : Shader.Find(ShaderName);
                if (sh == null) return baseMaterial;
                _mat = new Material(sh) { name = "UIBlendImage (" + name + ")", hideFlags = HideFlags.HideAndDontSave };
            }

            if (!FixedBlend.Apply(_mat, mode))
            {
                if (!_warned)
                {
                    Debug.LogWarning($"UIBlendImage on '{name}': {StatusMessage}", this);
                    _warned = true;
                }
                FixedBlend.Apply(_mat, LayerBlendMode.Normal);
            }
            _mat.SetFloat(OpacityId, opacity);

            // Keep the stencil state a parent Mask set up on the base material.
            foreach (var id in StencilIds)
                if (baseMaterial != null && baseMaterial.HasProperty(id)) _mat.SetFloat(id, baseMaterial.GetFloat(id));

            return _mat;
        }

        void Kill()
        {
            if (_mat == null) return;
            if (Application.isPlaying) Destroy(_mat); else DestroyImmediate(_mat);
            _mat = null;
        }
    }
}
