using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlendMode.Showcase
{
    // Station 11: a colorful scene drains to gray as health drops (Saturation with a gray layer, Compositor).
    // The pause toggle drains it fully and adds a Multiply dimmer and a PAUSED label.
    public class S11_DesaturateStation : Station
    {
        [Range(0f, 1f)] public float health = 0.3f;
        public bool paused;
        public Slider healthSlider;
        public Toggle pauseToggle;
        public TMP_Text pausedLabel;

        Controlled _drain, _dim;
        BlendLayerStack _stack;

        public BlendLayerStack SceneStack => _stack;
        public override bool HasOpacity => false;
        public override string ExtraStatus => paused ? "Paused" : $"Health {Mathf.RoundToInt(health * 100f)}%";

        protected override void Build()
        {
            var scene = Meadow();
            _stack = Stack("Desaturate compositor", scene.W * 4, scene.H * 4);
            AddLayer(_stack, Tex(scene, "Meadow"));
            _drain = Control(_stack, AddLayer(_stack, Own(ProcArt.Solid(new Color(0.5f, 0.5f, 0.5f), "Gray")), LayerBlendMode.Saturation), useOpacity: false);
            _dim = Control(_stack, AddLayer(_stack, Own(ProcArt.Solid(new Color(0.45f, 0.45f, 0.45f), "Dim")), LayerBlendMode.Multiply), useOpacity: false);
            StackView("Meadow", _stack, new Vector2(0f, -0.75f), new Vector2(17.2f, 7.1f), 0);

            if (healthSlider != null)
            {
                healthSlider.SetValueWithoutNotify(health);
                healthSlider.onValueChanged.AddListener(SetHealth);
            }
            if (pauseToggle != null)
            {
                pauseToggle.SetIsOnWithoutNotify(paused);
                pauseToggle.onValueChanged.AddListener(SetPaused);
            }
            Apply();
        }

        public void SetHealth(float value) { health = Mathf.Clamp01(value); Apply(); }

        public void SetPaused(bool on) { paused = on; Apply(); }

        void Apply()
        {
            _drain.strength = paused ? 1f : 1f - health;
            _dim.strength = paused ? 1f : 0f;
            if (pausedLabel != null) pausedLabel.gameObject.SetActive(paused);
            Refresh();
        }

        static PixelCanvas Meadow()
        {
            var c = new PixelCanvas(172, 71, Color.white);
            c.VerticalGradient(new Color(0.55f, 0.85f, 1f), new Color(0.2f, 0.5f, 0.95f));
            c.Circle(140, 56, 8, new Color(1f, 0.85f, 0.2f));
            c.Silhouette(x => 30 + 6 * Mathf.Sin(x * 0.06f), new Color(0.3f, 0.75f, 0.3f));
            c.Silhouette(x => 18 + 3 * Mathf.Sin(x * 0.11f + 1.5f), new Color(0.2f, 0.62f, 0.25f));
            var flowers = new[] { new Color(1f, 0.2f, 0.3f), new Color(1f, 0.9f, 0.1f), new Color(0.6f, 0.3f, 1f), new Color(1f, 0.5f, 0.1f) };
            for (int i = 0; i < 90; i++)
            {
                int x = (int)(ProcArt.Rand(i, 21) * 172), y = (int)(ProcArt.Rand(i, 22) * 22);
                c.Rect(x, y, 2, 2, flowers[i % flowers.Length]);
                c.Rect(x, y - 2, 1, 2, new Color(0.15f, 0.45f, 0.15f));
            }
            // A red-roofed cottage and a blue pond.
            c.Rect(30, 26, 22, 14, new Color(0.95f, 0.9f, 0.75f));
            for (int y = 0; y < 10; y++) c.Rect(28 + y, 40 + y, 26 - 2 * y, 1, new Color(0.85f, 0.15f, 0.15f));
            c.Ellipse(110, 12, 22, 6, new Color(0.2f, 0.55f, 0.95f));
            return c;
        }
    }
}
