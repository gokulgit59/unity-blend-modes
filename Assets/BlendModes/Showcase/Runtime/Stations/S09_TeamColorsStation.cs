using UnityEngine;
using UnityEngine.UI;

namespace BlendMode.Showcase
{
    // Station 9: one gray character, recolored per team with Color (Compositor).
    // The team color layer is clipped to the character, like a Photoshop clipping mask.
    public class S09_TeamColorsStation : Station
    {
        public Button[] teamButtons = new Button[4];

        public static readonly Color[] Teams =
        {
            new Color(0.9f, 0.2f, 0.2f), new Color(0.2f, 0.45f, 0.95f), new Color(0.2f, 0.8f, 0.3f), new Color(0.95f, 0.75f, 0.1f)
        };
        public static readonly string[] TeamNames = { "Red", "Blue", "Green", "Gold" };

        Texture2D _teamTex;
        BlendLayerStack _stack;
        int _team;

        public int Team => _team;
        public BlendLayerStack TeamStack => _stack;
        public override string ExtraStatus => "Team: " + TeamNames[_team];

        protected override void Build()
        {
            var bg = new PixelCanvas(4, 32, Color.white);
            bg.VerticalGradient(new Color(0.18f, 0.18f, 0.22f), new Color(0.3f, 0.3f, 0.36f));
            Fit(Sprite("Backdrop", Tex(bg, "Backdrop", FilterMode.Bilinear), new Vector2(0f, -0.75f), 17.2f, 0), new Vector2(17.2f, 7.1f));

            var gray = ProcArt.Character(CharacterPalette.Gray);
            var grayTex = Tex(gray, "Gray character");
            Sprite("Original (gray)", grayTex, new Vector2(-4f, -1.0f), 3.6f, 2);

            _teamTex = Own(ProcArt.Solid(Teams[0], "Team color"));
            _stack = Stack("Team color compositor", gray.W * 4, gray.H * 4);
            _stack.filterMode = FilterMode.Point;
            AddLayer(_stack, grayTex);
            Control(_stack, AddLayer(_stack, _teamTex, LayerBlendMode.Color, clip: true));
            StackView("Recolored", _stack, new Vector2(4f, -1.0f), new Vector2(3.6f, 3.6f * gray.H / gray.W), 2);

            for (int i = 0; i < teamButtons.Length; i++)
            {
                int team = i;
                if (teamButtons[i] != null) teamButtons[i].onClick.AddListener(() => SetTeam(team));
            }
        }

        public void SetTeam(int team)
        {
            _team = Mathf.Clamp(team, 0, Teams.Length - 1);
            var px = _teamTex.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i] = Teams[_team];
            _teamTex.SetPixels32(px);
            _teamTex.Apply();
            _stack.MarkDirty();
            UpdateLabel();
        }
    }
}
