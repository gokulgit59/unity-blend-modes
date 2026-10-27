using UnityEngine;

namespace BlendMode.Showcase
{
    // Station 3: blob shadows under two characters and a dark vignette, all Multiply (Fixed-function).
    // The shadows are a cool purple rather than black: Multiply tints the grass, Normal just paints over it.
    public class S03_ShadowsStation : Station
    {
        protected override void Build()
        {
            var ground = new PixelCanvas(172, 71, new Color(0.45f, 0.72f, 0.35f));
            ground.Ellipse(86, 20, 120, 16, new Color(0.78f, 0.68f, 0.48f), 0.2f);   // dirt path
            for (int i = 0; i < 60; i++)                                               // flowers
            {
                var col = i % 3 == 0 ? new Color(1f, 0.95f, 0.5f) : i % 3 == 1 ? new Color(1f, 0.6f, 0.7f) : Color.white;
                ground.Rect((int)(ProcArt.Rand(i, 3) * 172), (int)(36 + ProcArt.Rand(i, 4) * 34), 1, 1, col);
            }
            ground.Grain(5f, 0.12f, 31);
            Sprite("Grass", Tex(ground, "Grass"), new Vector2(0f, -0.75f), 17.2f, 0);

            var shadowTex = Own(ProcArt.Radial(64, 32, new Color(0.3f, 0.24f, 0.45f, 0.95f), new Color(0.3f, 0.24f, 0.45f, 0.95f), 0.8f, "Blob shadow"));
            var hero = Tex(ProcArt.Character(CharacterPalette.Hero), "Hero");
            var knight = Tex(ProcArt.Character(CharacterPalette.Knight), "Knight");

            Character("Hero", hero, new Vector2(-3f, -1.2f), shadowTex);
            Character("Knight", knight, new Vector2(3f, -1.2f), shadowTex);

            // Vignette: transparent in the middle, a deep purple toward the edges.
            var vig = new PixelCanvas(172, 71, Color.clear);
            for (int y = 0; y < vig.H; y++)
            for (int x = 0; x < vig.W; x++)
            {
                float dx = (x + 0.5f) / vig.W * 2f - 1f, dy = (y + 0.5f) / vig.H * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f);
                vig.Px[y * vig.W + x] = new Color(0.2f, 0.12f, 0.3f, Mathf.SmoothStep(0f, 0.85f, (d - 0.45f) / 0.65f));
            }
            BlendSprite("Vignette (Multiply)", Tex(vig, "Vignette", FilterMode.Bilinear), new Vector2(0f, -0.75f), 17.2f, 10, LayerBlendMode.Multiply, out _);
        }

        void Character(string name, Texture2D tex, Vector2 feet, Texture2D shadowTex)
        {
            BlendSprite(name + " shadow (Multiply)", shadowTex, feet + new Vector2(0.15f, 0f), 2.6f, 1, LayerBlendMode.Multiply, out _);
            // 18 x 26 px at 8 px per unit: 2.25 x 3.25 units, standing on 'feet'.
            Sprite(name, tex, feet + new Vector2(0f, 1.625f), 2.25f, 2);
        }
    }
}
