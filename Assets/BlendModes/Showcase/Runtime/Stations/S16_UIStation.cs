using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlendMode.Showcase
{
    // Station 16: UI styles a designer would build in Photoshop or Figma, made with real uGUI.
    //   Shine:    a white streak sweeps across the button. UIBlendImage, Screen, masked by RectMask2D. (Fixed-function on UI)
    //   Pressed:  a gray layer on the button with UIBlendImage, Multiply. Hold the Shine button to see it live.
    //   Disabled: Saturation needs the backdrop, so the button is flattened in a BlendLayerStack and shown in a RawImage.
    //   Rare card: a glowing frame with UIBlendImage, Add, pulsing.
    public class S16_UIStation : Station
    {
        [Tooltip("UIFixedBlend.shader, assigned so builds keep it.")]
        public Shader uiShader;

        class UiEntry { public UIBlendImage blend; public LayerBlendMode mode; public float strength = 1f; }

        readonly List<UiEntry> _ui = new List<UiEntry>();
        RectTransform _shine;
        UiEntry _glow, _pressOverlay;
        bool _held;

        public RectTransform Shine => _shine;
        public BlendLayerStack DisabledStack { get; private set; }
        public float ShineX => _shine != null ? _shine.anchoredPosition.x : 0f;
        public bool AnimateShine { get; set; } = true;

        // Column centers in demo canvas pixels (the demo canvas is 1720 x 710, centered on the demo area).
        public static readonly float[] Columns = { -645f, -215f, 215f, 645f };

        protected override void Build()
        {
            var canvas = DemoCanvas();
            var bg = Image(canvas, "Background", Sprite(Tex(Background(), "UI background", FilterMode.Bilinear)), Vector2.zero, new Vector2(1720f, 710f));
            bg.raycastTarget = false;

            var buttonSprite = Sprite(Tex(ButtonArt(new Color(0.55f, 0.3f, 0.95f), new Color(0.35f, 0.15f, 0.75f)), "Button", FilterMode.Bilinear));
            var shapeSprite = Sprite(Tex(ButtonArt(Color.white, Color.white, rim: false), "Button shape", FilterMode.Bilinear));
            var pressGray = new Color(0.55f, 0.55f, 0.6f);
            var buttonSize = new Vector2(330f, 110f);

            // 1. Shine sweep (Screen), masked to the button with RectMask2D.
            var shineButton = Image(canvas, "Shine button", buttonSprite, new Vector2(Columns[0], 40f), buttonSize);
            shineButton.gameObject.AddComponent<RectMask2D>();
            var shine = Image(shineButton.rectTransform, "Shine (Screen)", Sprite(Tex(Streak(), "Shine streak", FilterMode.Bilinear)), new Vector2(-260f, 0f), new Vector2(90f, 140f));
            shine.raycastTarget = false;
            _shine = shine.rectTransform;
            AddBlend(shine, LayerBlendMode.Screen);
            _pressOverlay = AddBlend(Image(shineButton.rectTransform, "Pressed overlay (Multiply)", shapeSprite, Vector2.zero, buttonSize, pressGray), LayerBlendMode.Multiply);
            _pressOverlay.blend.enabled = false;
            _pressOverlay.blend.GetComponent<Image>().enabled = false;
            Label(shineButton.rectTransform, "PLAY", 44);
            var press = shineButton.gameObject.AddComponent<PressRelay>();
            press.onChange = held => { _held = held; ApplyPressed(); };

            // 2. Pressed (Multiply), a static copy.
            var pressed = Image(canvas, "Pressed button", buttonSprite, new Vector2(Columns[1], 40f), buttonSize);
            pressed.raycastTarget = false;
            AddBlend(Image(pressed.rectTransform, "Pressed (Multiply)", shapeSprite, Vector2.zero, buttonSize, pressGray), LayerBlendMode.Multiply);
            Label(pressed.rectTransform, "PLAY", 44).rectTransform.anchoredPosition = new Vector2(0f, -4f);

            // 3. Disabled (Saturation), flattened in a stack because Saturation needs the backdrop.
            DisabledStack = Stack("Disabled button compositor", 330, 110);
            AddLayer(DisabledStack, buttonSprite.texture);
            Control(DisabledStack, AddLayer(DisabledStack, Own(ProcArt.Solid(new Color(0.5f, 0.5f, 0.5f), "Gray")), LayerBlendMode.Saturation, clip: true));
            DisabledStack.Composite();
            var disabled = new GameObject("Disabled button (RawImage of stack)", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            Place(disabled.rectTransform, canvas, new Vector2(Columns[2], 40f), buttonSize);
            disabled.texture = DisabledStack.Result;
            disabled.raycastTarget = false;
            Label(disabled.rectTransform, "PLAY", 44).color = new Color(0.85f, 0.85f, 0.85f);

            // 4. Rare item card with a glowing frame (Add), pulsing.
            var card = Image(canvas, "Item card", Sprite(Tex(Card(), "Card", FilterMode.Bilinear)), new Vector2(Columns[3], 20f), new Vector2(220f, 290f));
            card.raycastTarget = false;
            var glow = Image(canvas, "Rarity glow (Add)", Sprite(Tex(Ring(), "Glow frame", FilterMode.Bilinear)), new Vector2(Columns[3], 20f), new Vector2(330f, 400f));
            glow.raycastTarget = false;
            _glow = AddBlend(glow, LayerBlendMode.LinearDodge);
            card.transform.SetAsLastSibling();      // the card sits on top of its glow

            string[] captions = { "Shine sweep\n<size=75%>Screen, on a UI Image</size>", "Pressed\n<size=75%>Multiply, on a UI Image</size>",
                                  "Disabled\n<size=75%>Saturation, via a layer stack</size>", "Rare item\n<size=75%>Add, pulsing glow</size>" };
            for (int i = 0; i < 4; i++)
            {
                var t = Label(canvas, captions[i], 28);
                t.rectTransform.anchoredPosition = new Vector2(Columns[i], -235f);
                t.rectTransform.sizeDelta = new Vector2(400f, 90f);
            }
            var hint = Label(canvas, "Hold the left button to see Multiply live.", 22);
            hint.rectTransform.anchoredPosition = new Vector2(Columns[0], 150f);
            hint.rectTransform.sizeDelta = new Vector2(520f, 40f);
        }

        UiEntry AddBlend(Graphic g, LayerBlendMode mode)
        {
            var b = g.gameObject.AddComponent<UIBlendImage>();
            if (uiShader != null) b.Shader = uiShader;
            b.Mode = mode;
            var e = new UiEntry { blend = b, mode = mode };
            _ui.Add(e);
            return e;
        }

        void ApplyPressed()
        {
            _pressOverlay.blend.enabled = _held;
            _pressOverlay.blend.GetComponent<Image>().enabled = _held;
        }

        protected override void OnRefresh()
        {
            foreach (var e in _ui)
            {
                e.blend.Mode = blendOn ? e.mode : LayerBlendMode.Normal;
                e.blend.Opacity = opacity * e.strength;
            }
        }

        void Update()
        {
            if (_shine == null) return;
            if (AnimateShine)
            {
                // Sweep across in 0.8 s, then wait 1.2 s.
                float t = Mathf.Repeat(Time.time, 2f) / 0.8f;
                _shine.anchoredPosition = new Vector2(Mathf.Lerp(-260f, 260f, Mathf.Clamp01(t)), 0f);
            }
            _glow.strength = 0.65f + 0.35f * Mathf.Sin(Time.time * 3f);
            _glow.blend.Opacity = opacity * _glow.strength;
        }

        // ---------- building blocks ----------

        RectTransform DemoCanvas()
        {
            var go = new GameObject("UI demo canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(Demo, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 5;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(1720f, 710f);
            rt.localScale = Vector3.one * 0.01f;
            rt.localPosition = new Vector3(0f, -0.75f, 0f);
            return rt;
        }

        Sprite Sprite(Texture2D tex) =>
            Own(UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f));

        static void Place(RectTransform rt, RectTransform parent, Vector2 pos, Vector2 size)
        {
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static Image Image(RectTransform parent, string name, Sprite sprite, Vector2 pos, Vector2 size, Color? color = null)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            Place(img.rectTransform, parent, pos, size);
            img.sprite = sprite;
            img.color = color ?? Color.white;
            return img;
        }

        static TextMeshProUGUI Label(RectTransform parent, string text, float size)
        {
            var t = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            Place(t.rectTransform, parent, Vector2.zero, parent.sizeDelta.x > 0 ? parent.sizeDelta : new Vector2(300f, 60f));
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        static PixelCanvas Background()
        {
            var c = new PixelCanvas(8, 64, Color.white);
            c.VerticalGradient(new Color(0.08f, 0.1f, 0.2f), new Color(0.16f, 0.2f, 0.36f));
            return c;
        }

        // A rounded, vertically shaded button with a light rim.
        static PixelCanvas ButtonArt(Color top, Color bottom, bool rim = true)
        {
            const int w = 330, h = 110, r = 30;
            var c = new PixelCanvas(w, h, Color.clear);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, w - r), cy = Mathf.Clamp(y + 0.5f, r, h - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                if (d > r) continue;
                var col = Color.Lerp(bottom, top, y / (float)h);
                if (rim && d > r - 4f) col = Color.Lerp(col, Color.white, 0.45f);  // rim
                col.a = Mathf.Clamp01(r - d);
                c.Px[y * w + x] = col;
            }
            return c;
        }

        static PixelCanvas Streak()
        {
            var c = new PixelCanvas(45, 70, Color.clear);
            for (int y = 0; y < c.H; y++)
            for (int x = 0; x < c.W; x++)
            {
                float u = (x - c.W * 0.5f) + (y - c.H * 0.5f) * 0.45f;
                float a = Mathf.Clamp01(1f - Mathf.Abs(u) / 9f);
                c.Px[y * c.W + x] = new Color(1f, 1f, 1f, a * a);
            }
            return c;
        }

        static PixelCanvas Card()
        {
            const int w = 110, h = 145;
            var c = new PixelCanvas(w, h, Color.clear);
            c.Rect(0, 0, w, h, new Color(0.95f, 0.75f, 0.3f));
            c.Rect(4, 4, w - 8, h - 8, new Color(0.18f, 0.12f, 0.28f));
            c.Rect(10, 40, w - 20, h - 50, new Color(0.26f, 0.18f, 0.4f));
            // a gem
            for (int y = 0; y < 50; y++)
            {
                int half = y < 20 ? y : (50 - y) * 20 / 30;
                c.Rect(w / 2 - half, 60 + y, half * 2, 1, Color.Lerp(new Color(0.9f, 0.3f, 1f), new Color(0.5f, 0.1f, 0.8f), y / 50f));
            }
            c.Rect(w / 2 - 6, 90, 4, 8, new Color(1f, 0.85f, 1f));
            c.Rect(14, 14, w - 28, 16, new Color(0.95f, 0.75f, 0.3f));
            return c;
        }

        // A soft ring for the rarity glow: bright at the card's edge, fading in and out.
        static PixelCanvas Ring()
        {
            const int w = 165, h = 200;
            var c = new PixelCanvas(w, h, Color.clear);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // distance to the card's rectangle (the card is 2/3 of this texture), in pixels
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - w * 0.5f) - 55f, 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - h * 0.5f) - 72.5f, 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Exp(-d * d / 160f);
                c.Px[y * w + x] = new Color(1f, 0.6f, 0.15f, a);
            }
            return c;
        }
    }
}
