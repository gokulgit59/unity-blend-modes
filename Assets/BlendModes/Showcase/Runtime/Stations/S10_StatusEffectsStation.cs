using UnityEngine;
using UnityEngine.UI;

namespace BlendMode.Showcase
{
    // Station 10: status effects stacked on one character (Compositor, every effect clipped to the character).
    //   Poisoned: Hue with a sickly green    -> every color turns green, brightness and saturation kept
    //   Frozen:   Soft light with icy blue   -> a cool tint, darks stay dark
    //   Burning:  Soft light with orange     -> a warm tint that flickers
    public class S10_StatusEffectsStation : Station
    {
        public Toggle poisonedToggle, frozenToggle, burningToggle;
        public bool poisoned = true, frozen, burning;

        BlendLayer _poison, _frost, _fire;
        Controlled _fireControl;
        BlendLayerStack _stack;

        public BlendLayerStack EffectStack => _stack;

        public override string ExtraStatus
        {
            get
            {
                string s = (poisoned ? "Poisoned " : "") + (frozen ? "Frozen " : "") + (burning ? "Burning" : "");
                return s.Length == 0 ? "No effect" : s.Trim();
            }
        }

        protected override void Build()
        {
            var bg = new PixelCanvas(4, 32, Color.white);
            bg.VerticalGradient(new Color(0.2f, 0.22f, 0.2f), new Color(0.32f, 0.36f, 0.34f));
            Fit(Sprite("Backdrop", Tex(bg, "Backdrop", FilterMode.Bilinear), new Vector2(0f, -0.75f), 17.2f, 0), new Vector2(17.2f, 7.1f));

            var hero = ProcArt.Character(CharacterPalette.Knight);
            var heroTex = Tex(hero, "Knight");
            Sprite("Original", heroTex, new Vector2(-4f, -1.0f), 3.6f, 2);

            _stack = Stack("Status effect compositor", hero.W * 4, hero.H * 4);
            _stack.filterMode = FilterMode.Point;
            AddLayer(_stack, heroTex);
            _poison = AddLayer(_stack, Own(ProcArt.Solid(new Color(0.45f, 0.95f, 0.2f), "Poison")), LayerBlendMode.Hue, clip: true);
            _frost = AddLayer(_stack, Own(ProcArt.Solid(new Color(0.55f, 0.8f, 1f), "Frost")), LayerBlendMode.SoftLight, clip: true);
            _fire = AddLayer(_stack, Own(ProcArt.Solid(new Color(1f, 0.5f, 0.1f), "Fire")), LayerBlendMode.SoftLight, clip: true);
            Control(_stack, _poison);
            Control(_stack, _frost);
            _fireControl = Control(_stack, _fire);
            StackView("With effects", _stack, new Vector2(4f, -1.0f), new Vector2(3.6f, 3.6f * hero.H / hero.W), 2);

            Hook(poisonedToggle, poisoned, v => poisoned = v);
            Hook(frozenToggle, frozen, v => frozen = v);
            Hook(burningToggle, burning, v => burning = v);
            ApplyEffects();
        }

        void Hook(Toggle t, bool value, System.Action<bool> set)
        {
            if (t == null) return;
            t.SetIsOnWithoutNotify(value);
            t.onValueChanged.AddListener(v => { set(v); ApplyEffects(); });
        }

        public void SetEffects(bool poison, bool freeze, bool burn)
        {
            poisoned = poison; frozen = freeze; burning = burn;
            if (poisonedToggle != null) poisonedToggle.SetIsOnWithoutNotify(poison);
            if (frozenToggle != null) frozenToggle.SetIsOnWithoutNotify(freeze);
            if (burningToggle != null) burningToggle.SetIsOnWithoutNotify(burn);
            ApplyEffects();
        }

        void ApplyEffects()
        {
            _poison.visible = poisoned;
            _frost.visible = frozen;
            _fire.visible = burning;
            _stack.MarkDirty();
            UpdateLabel();
        }

        void Update()
        {
            if (!burning || _fireControl == null) return;
            // A gentle flicker on the burning tint.
            _fireControl.strength = 0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 6f, 0.3f);
            Refresh();
        }
    }
}
