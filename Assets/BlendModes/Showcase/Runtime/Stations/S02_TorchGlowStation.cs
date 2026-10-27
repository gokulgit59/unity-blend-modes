using UnityEngine;

namespace BlendMode.Showcase
{
    // Station 2: two torches on a dark cave wall, each with a soft radial glow drawn with Screen (Fixed-function).
    public class S02_TorchGlowStation : Station
    {
        protected override void Build()
        {
            // Dark stone wall: rows of bricks with a little color variation and grain.
            var wall = new PixelCanvas(172, 71, new Color(0.08f, 0.07f, 0.07f));
            for (int row = 0; row * 9 < wall.H; row++)
            {
                int offset = row % 2 == 0 ? 0 : 9;
                for (int col = -1; col * 18 < wall.W; col++)
                {
                    float v = 0.15f + ProcArt.Rand(row * 31 + col, 2) * 0.07f;
                    wall.Rect(col * 18 + offset + 1, row * 9 + 1, 16, 7, new Color(v * 1.05f, v * 0.95f, v * 0.9f));
                }
            }
            wall.Grain(6f, 0.25f, 12);
            Sprite("Cave wall", Tex(wall, "Cave wall"), new Vector2(0f, -0.75f), 17.2f, 0);

            var glowTex = Own(ProcArt.Radial(128, 128, new Color(1f, 0.78f, 0.45f, 1f), new Color(1f, 0.42f, 0.08f, 1f), 1.6f, "Torch glow"));
            var torchTex = Tex(Torch(), "Torch");

            foreach (float x in new[] { -4.2f, 4.2f })
            {
                Sprite("Torch", torchTex, new Vector2(x, -1.2f), 0.9f, 2);
                BlendSprite("Glow (Screen)", glowTex, new Vector2(x, -0.4f), 7.5f, 3, LayerBlendMode.Screen, out _);
            }
        }

        static PixelCanvas Torch()
        {
            var c = new PixelCanvas(9, 22, Color.clear);
            c.Rect(3, 0, 3, 13, new Color(0.35f, 0.22f, 0.12f));       // handle
            c.Rect(2, 12, 5, 2, new Color(0.25f, 0.25f, 0.28f));       // metal band
            c.Ellipse(4.5f, 17f, 3.5f, 5f, new Color(1f, 0.45f, 0.1f));   // flame
            c.Ellipse(4.5f, 16.5f, 2f, 3.2f, new Color(1f, 0.85f, 0.3f));
            c.Ellipse(4.5f, 16f, 1f, 1.6f, new Color(1f, 1f, 0.85f));
            return c;
        }
    }
}
