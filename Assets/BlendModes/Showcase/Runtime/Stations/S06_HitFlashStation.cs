using UnityEngine;
using UnityEngine.UI;

namespace BlendMode.Showcase
{
    // Station 6: press F (or the Hit button) and a white copy of the character, drawn with Add, flashes and eases out.
    public class S06_HitFlashStation : Station
    {
        [Tooltip("Seconds for the flash to fade back to nothing.")]
        public float duration = 0.3f;
        public Button hitButton;

        Controlled _flash;
        float _hitTime = -10f;

        public float FlashAmount { get; private set; }

        protected override void Build()
        {
            var floor = new PixelCanvas(172, 71, new Color(0.32f, 0.3f, 0.36f));
            for (int y = 0; y < floor.H; y += 12)
            for (int x = (y / 12) % 2 * 12; x < floor.W; x += 24)
                floor.Rect(x, y, 12, 12, new Color(0.36f, 0.34f, 0.4f));
            floor.Grain(4f, 0.1f, 6);
            Sprite("Arena floor", Tex(floor, "Arena floor"), new Vector2(0f, -0.75f), 17.2f, 0);

            var knight = ProcArt.Character(CharacterPalette.Knight);
            // 18 x 26 px at 3.6 units wide: big enough to see the flash.
            Sprite("Knight", Tex(knight, "Knight"), new Vector2(0f, -0.9f), 3.6f, 2);
            BlendSprite("Hit flash (Add)", Tex(ProcArt.Silhouette(knight, Color.white), "Flash"), new Vector2(0f, -0.9f), 3.6f, 3,
                LayerBlendMode.LinearDodge, out _flash);
            _flash.strength = 0f;

            if (hitButton != null) hitButton.onClick.AddListener(Hit);
        }

        public override void OnKey(KeyCode key)
        {
            if (key == KeyCode.F) Hit();
        }

        public void Hit() { _hitTime = Time.time; }

        public override string ExtraStatus => "Press F or Hit";

        void Update()
        {
            if (_flash == null) return;
            float t = (Time.time - _hitTime) / Mathf.Max(0.01f, duration);
            float amount = t >= 1f ? 0f : 1f - t * t * (3f - 2f * t);   // ease out
            if (Mathf.Approximately(amount, FlashAmount) && amount == 0f) return;
            FlashAmount = amount;
            _flash.strength = amount;
            Refresh();
        }
    }
}
