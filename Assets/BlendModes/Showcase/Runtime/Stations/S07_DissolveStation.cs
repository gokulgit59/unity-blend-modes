using UnityEngine;
using UnityEngine.UI;

namespace BlendMode.Showcase
{
    // Station 7: a character drawn with Dissolve onto a transparent stack, shown over a dungeon wall.
    // Opacity is animated: it disintegrates, stays gone for a moment, then respawns. Moving the HUD slider stops the animation.
    public class S07_DissolveStation : Station
    {
        public Toggle animateToggle;
        public bool animate = true;

        Controlled _character;
        float _clock;

        protected override void Build()
        {
            var wall = new PixelCanvas(172, 71, new Color(0.2f, 0.18f, 0.25f));
            for (int y = 0; y < wall.H; y += 10)
            for (int x = (y / 10) % 2 * 10; x < wall.W; x += 20)
                wall.Rect(x + 1, y + 1, 18, 8, new Color(0.26f, 0.23f, 0.32f));
            wall.Ellipse(86, 14, 22, 5, new Color(0.55f, 0.35f, 0.9f, 0.6f), 0.5f);   // a glowing rune circle
            wall.Grain(5f, 0.15f, 9);
            Sprite("Dungeon wall", Tex(wall, "Dungeon wall"), new Vector2(0f, -0.75f), 17.2f, 0);

            // The stack only holds the character (24 x 32 px, scaled x4), on a transparent backdrop.
            var hero = ProcArt.Pad(ProcArt.Character(CharacterPalette.Hero), 24, 32, 2);
            var stack = Stack("Dissolve compositor", 96, 128);
            stack.filterMode = FilterMode.Point;
            _character = Control(stack, AddLayer(stack, Tex(hero, "Hero"), LayerBlendMode.Dissolve));
            StackView("Dissolving hero", stack, new Vector2(0f, -1.0f), new Vector2(3.6f, 4.8f), 2);

            if (animateToggle != null)
            {
                animateToggle.SetIsOnWithoutNotify(animate);
                animateToggle.onValueChanged.AddListener(on => animate = on);
            }
        }

        public override string ExtraStatus => animate ? "Animating" : "Paused: use the slider";

        // The HUD slider drives opacity by hand, so stop the animation.
        public override void SetOpacity(float value)
        {
            SetAnimate(false);
            base.SetOpacity(value);
        }

        public void SetAnimate(bool on)
        {
            animate = on;
            if (animateToggle != null) animateToggle.SetIsOnWithoutNotify(on);
            UpdateLabel();
        }

        void Update()
        {
            if (!animate || _character == null) return;
            // 1 s visible, 1.5 s dissolving, 0.5 s gone, 1 s coming back.
            _clock = Mathf.Repeat(_clock + Time.deltaTime, 4f);
            float t = _clock;
            float value = t < 1f ? 1f : t < 2.5f ? 1f - (t - 1f) / 1.5f : t < 3f ? 0f : (t - 3f);
            opacity = value;
            Refresh();
        }
    }
}
