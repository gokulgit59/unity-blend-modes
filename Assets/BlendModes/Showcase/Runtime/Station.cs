using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BlendMode.Showcase
{
    // Base class for one showcase station.
    //
    // How a station works (read this first):
    //  * The scene file only holds the station's root object and its world-space Canvas (title, captions, controls).
    //  * The art is generated in code when Play starts, in Build(). Nothing is loaded from disk.
    //  * Every blended thing is registered as a "controlled" entry. Blend ON draws it with its real mode,
    //    Blend OFF draws the very same layer with plain Normal blending, so you can compare before and after.
    //  * Fixed-function entries are sprites/particles with a Blend Mode/FixedBlend material.
    //    Compositor entries are layers inside a BlendLayerStack, shown on a quad.
    public abstract class Station : MonoBehaviour
    {
        [Header("Text (shown above the demo)")]
        public string title;
        [TextArea(2, 4)] public string caption;
        public string formula;

        [Header("Live state")]
        public bool blendOn = true;
        [Range(0f, 1f)] public float opacity = 1f;
        public TMP_Text statusLabel;

        [Header("Shaders (assigned so builds keep them)")]
        public Shader fixedShader;
        public Shader compositorShader;

        [Tooltip("Demo area in local units, used to frame screenshots and tests.")]
        public Rect demoArea = new Rect(-8.6f, -4.3f, 17.2f, 7.1f);

        // Set false in stations that have no meaningful opacity (they hide the HUD slider).
        public virtual bool HasOpacity => true;
        public virtual string ExtraStatus => null;

        public const int OffstageY = -200;
        public Vector3 Offstage => new Vector3(transform.position.x, OffstageY, 0f);

        protected class Controlled
        {
            public LayerBlendMode mode;
            public Material material;          // fixed-function entry
            public BlendLayer layer;           // compositor entry
            public BlendLayerStack stack;
            public bool useOpacity = true;
            public float strength = 1f;        // extra multiplier the station animates (flash, day/night...)
        }

        protected readonly List<Controlled> controlled = new List<Controlled>();
        readonly List<Object> _owned = new List<Object>();
        readonly List<Camera> _cameras = new List<Camera>();
        readonly List<BlendLayerStack> _liveStacks = new List<BlendLayerStack>();
        Transform _demo;
        bool _built;

        protected Transform Demo
        {
            get
            {
                if (_demo == null)
                {
                    _demo = new GameObject("Demo (generated)").transform;
                    _demo.SetParent(transform, false);
                }
                return _demo;
            }
        }

        void Start() { EnsureBuilt(); }

        public void EnsureBuilt()
        {
            if (_built || !Application.isPlaying) return;
            _built = true;
            Build();
            Refresh();
        }

        protected abstract void Build();

        // ---------- the controls ----------

        public void SetBlend(bool on) { blendOn = on; Refresh(); }

        // Called by the HUD slider. Stations that animate opacity override it to stop their animation.
        public virtual void SetOpacity(float value) { opacity = Mathf.Clamp01(value); Refresh(); }

        public virtual void OnKey(KeyCode key) { }

        // Called by the controller: only the station on screen keeps its cameras and live stacks running.
        public virtual void SetFocused(bool focused)
        {
            foreach (var cam in _cameras) if (cam != null) cam.enabled = focused;
            foreach (var s in _liveStacks) if (s != null) s.continuous = focused;
        }

        // Push blendOn / opacity / strength into every controlled entry, then update the label.
        public void Refresh()
        {
            foreach (var c in controlled)
            {
                var mode = blendOn ? c.mode : LayerBlendMode.Normal;
                float op = (c.useOpacity ? opacity : 1f) * c.strength;
                if (c.material != null)
                {
                    FixedBlend.Apply(c.material, mode);
                    c.material.SetFloat("_Opacity", op);
                }
                if (c.layer != null)
                {
                    c.layer.mode = mode;
                    c.layer.opacity = op;
                    if (c.stack != null) c.stack.MarkDirty();
                }
            }
            OnRefresh();
            UpdateLabel();
        }

        protected virtual void OnRefresh() { }

        protected void UpdateLabel()
        {
            if (statusLabel == null) return;
            string state = blendOn ? "<color=#7CFC9A>Blend ON</color>" : "<color=#FF8A80>Blend OFF</color> <size=70%>(Normal)</size>";
            string op = HasOpacity ? $"   Opacity {Mathf.RoundToInt(opacity * 100f)}%" : "";
            string extra = ExtraStatus;
            statusLabel.text = state + op + (string.IsNullOrEmpty(extra) ? "" : "\n<size=75%>" + extra + "</size>");
        }

        // ---------- helpers for building a station ----------

        protected T Own<T>(T o) where T : Object { _owned.Add(o); return o; }

        protected Texture2D Tex(PixelCanvas canvas, string name, FilterMode filter = FilterMode.Point,
            TextureWrapMode wrap = TextureWrapMode.Clamp) => Own(canvas.ToTexture(name, filter, wrap));

        protected Material FixedMaterial(LayerBlendMode mode)
        {
            var m = Own(new Material(fixedShader != null ? fixedShader : Shader.Find(FixedBlend.ShaderName)));
            FixedBlend.Apply(m, mode);
            return m;
        }

        // Register a fixed-function material so Blend on/off and opacity drive it.
        protected Controlled Control(Material mat, LayerBlendMode mode, bool useOpacity = true)
        {
            var c = new Controlled { material = mat, mode = mode, useOpacity = useOpacity };
            controlled.Add(c);
            return c;
        }

        // Register a compositor layer so Blend on/off and opacity drive it.
        protected Controlled Control(BlendLayerStack stack, BlendLayer layer, bool useOpacity = true)
        {
            var c = new Controlled { stack = stack, layer = layer, mode = layer.mode, useOpacity = useOpacity };
            controlled.Add(c);
            return c;
        }

        // A sprite. Its pivot is the center. Normal blending unless a mode is given.
        protected SpriteRenderer Sprite(string name, Texture2D tex, Vector2 localPos, float unitsWide, int order,
            Transform parent = null, Material material = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : Demo, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Own(ProcArt.ToSprite(tex, unitsWide, new Vector2(0.5f, 0.5f)));
            sr.sharedMaterial = material != null ? material : FixedMaterial(LayerBlendMode.Normal);
            sr.sortingOrder = order;
            return sr;
        }

        // Scale a sprite so it covers exactly 'size' world units (for stretched gradients and fills).
        protected static SpriteRenderer Fit(SpriteRenderer sr, Vector2 size)
        {
            var b = sr.sprite.bounds.size;
            sr.transform.localScale = new Vector3(size.x / b.x, size.y / b.y, 1f);
            return sr;
        }

        protected SpriteRenderer BlendSprite(string name, Texture2D tex, Vector2 localPos, float unitsWide, int order,
            LayerBlendMode mode, out Controlled entry, Transform parent = null, bool useOpacity = true)
        {
            var mat = FixedMaterial(mode);
            entry = Control(mat, mode, useOpacity);
            return Sprite(name, tex, localPos, unitsWide, order, parent, mat);
        }

        protected BlendLayerStack Stack(string name, int width, int height, bool live = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Demo, false);
            var stack = go.AddComponent<BlendLayerStack>();
            stack.Shader = compositorShader;
            stack.resolution = new Vector2Int(width, height);
            if (live) _liveStacks.Add(stack);
            return stack;
        }

        protected BlendLayer AddLayer(BlendLayerStack stack, Texture tex, LayerBlendMode mode = LayerBlendMode.Normal,
            float layerOpacity = 1f, bool clip = false)
        {
            var layer = new BlendLayer { texture = tex, mode = mode, opacity = layerOpacity, clipToBackdrop = clip };
            stack.layers.Add(layer);
            stack.MarkDirty();
            return layer;
        }

        // A quad that shows a texture (usually a stack's Result) in the demo.
        protected MeshRenderer View(string name, Texture tex, Vector2 localPos, Vector2 size, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : Demo, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var mr = go.AddComponent<MeshRenderer>();
            var mat = FixedMaterial(LayerBlendMode.Normal);
            mat.mainTexture = tex;
            mr.sharedMaterial = mat;
            mr.sortingOrder = order;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        // Shows a stack's Result on a quad. Result is recreated if the stack resizes, so bind after it composites.
        protected MeshRenderer StackView(string name, BlendLayerStack stack, Vector2 localPos, Vector2 size, int order)
        {
            stack.Composite();
            return View(name, stack.Result, localPos, size, order);
        }

        // An orthographic camera that renders an offstage area into a RenderTexture.
        protected Camera StationCamera(string name, Vector3 worldCenter, float worldHeight, int width, int height, Color background)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, true);
            go.transform.position = worldCenter + new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = worldHeight * 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 50f;
            cam.depth = -10;
            cam.allowHDR = false; cam.allowMSAA = false;
            var rt = Own(new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32) { name = name + " RT", filterMode = FilterMode.Bilinear });
            rt.Create();
            cam.targetTexture = rt;
            cam.enabled = false;
            _cameras.Add(cam);
            return cam;
        }

        // A parent object far below the stations, for content that only station cameras see.
        protected Transform OffstageRoot(string name, Vector2 offset = default)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, true);
            t.position = Offstage + (Vector3)offset;
            return t;
        }

        protected virtual void OnDestroy()
        {
            foreach (var cam in _cameras) if (cam != null) cam.targetTexture = null;
            foreach (var o in _owned)
            {
                if (o == null) continue;
                if (o is RenderTexture rt) { if (RenderTexture.active == rt) RenderTexture.active = null; rt.Release(); }
                Destroy(o);
            }
            _owned.Clear();
        }
    }
}
