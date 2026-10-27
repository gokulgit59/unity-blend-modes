using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlendMode.EditorTools
{
    // Small helpers for building uGUI hierarchies from editor scripts.
    public static class UiKit
    {
        public const string Root = "Assets/BlendModes";
        public const string CompositorShaderPath = Root + "/Shaders/BlendCompositor.shader";
        public const string FixedShaderPath = Root + "/Shaders/FixedBlend.shader";

        public static Shader CompositorShader => AssetDatabase.LoadAssetAtPath<Shader>(CompositorShaderPath);
        public static Shader FixedShader => AssetDatabase.LoadAssetAtPath<Shader>(FixedShaderPath);

        static DefaultControls.Resources _res;
        public static DefaultControls.Resources Resources
        {
            get
            {
                if (_res.standard == null)
                {
                    _res.standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                    _res.background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
                    _res.knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                    _res.checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
                    _res.inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
                    _res.dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
                    _res.mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd");
                }
                return _res;
            }
        }

        public static string ModeLabel(LayerBlendMode mode)
        {
            if (mode == LayerBlendMode.LinearDodge) return "Linear Dodge (Add)";
            return ObjectNames.NicifyVariableName(mode.ToString());
        }

        public static GameObject Go(string name, Transform parent, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        public static RectTransform Rect(string name, Transform parent, params System.Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform));
            foreach (var c in components) go.AddComponent(c);
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // Anchor a rect to a point of its parent with a fixed size.
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Canvas OverlayCanvas(string name, Vector2 reference)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = reference;
            scaler.matchWidthOrHeight = 0.5f;
            return go.GetComponent<Canvas>();
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size,
            TextAlignmentOptions align = TextAlignmentOptions.Center, Color? color = null)
        {
            var rt = Rect(name, parent, typeof(TextMeshProUGUI));
            var t = rt.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? Color.white;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            var rt = Rect(name, parent, typeof(Image));
            var img = rt.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Slider Slider(Transform parent, string name, float min, float max, float value)
        {
            var go = DefaultControls.CreateSlider(Resources);
            go.name = name;
            go.transform.SetParent(parent, false);
            var s = go.GetComponent<Slider>();
            s.minValue = min; s.maxValue = max; s.value = value;
            return s;
        }

        // A toggle whose legacy Text label is replaced with TextMeshPro.
        public static Toggle Toggle(Transform parent, string name, string label, bool on)
        {
            var go = DefaultControls.CreateToggle(Resources);
            go.name = name;
            go.transform.SetParent(parent, false);
            var legacy = go.GetComponentInChildren<Text>();
            var labelRt = (RectTransform)legacy.transform;
            Object.DestroyImmediate(legacy);
            var tmp = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 18; tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft; tmp.raycastTarget = false;
            var t = go.GetComponent<Toggle>();
            t.isOn = on;
            return t;
        }

        // A button whose legacy Text label is replaced with TextMeshPro.
        public static Button Button(Transform parent, string name, string label)
        {
            var go = DefaultControls.CreateButton(Resources);
            go.name = name;
            go.transform.SetParent(parent, false);
            var legacy = go.GetComponentInChildren<Text>();
            var labelGo = legacy.gameObject;
            Object.DestroyImmediate(legacy);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 18; tmp.color = new Color(0.12f, 0.12f, 0.14f);
            tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
            return go.GetComponent<Button>();
        }

        public static void SetSerialized(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
