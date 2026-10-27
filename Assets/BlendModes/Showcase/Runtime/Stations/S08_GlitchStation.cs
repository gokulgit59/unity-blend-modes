using UnityEngine;
using UnityEngine.UI;

namespace BlendMode.Showcase
{
    // Station 8: a little platformer level filmed by a station camera, with two glitch bars over it.
    //   Upper bar: Difference, a layer in the compositor (it reads the camera picture).
    //   Lower bar: Exclusion, a plain sprite drawn over the screen with Fixed-function blending.
    // Both bars jitter sideways while 'animate' is on.
    public class S08_GlitchStation : Station
    {
        public Toggle animateToggle;
        public bool animate = true;

        BlendLayer _diffLayer;
        BlendLayerStack _stack;
        Transform _exclusionBar;
        float _nextJitter;

        public BlendLayerStack GlitchStack => _stack;
        public Texture CameraTexture { get; private set; }

        // Rows (in the 71 px tall demo) covered by each bar, for tests.
        public const int DifferenceBarY = 40, DifferenceBarH = 9, ExclusionBarY = 14, ExclusionBarH = 7;

        protected override void Build()
        {
            var stage = OffstageRoot("Offstage level");
            BuildLevel(stage);
            var cam = StationCamera("Level camera", stage.position, 7.1f, 860, 355, new Color(0.45f, 0.7f, 0.95f));
            CameraTexture = cam.targetTexture;

            _stack = Stack("Glitch compositor", 860, 355, live: true);
            AddLayer(_stack, cam.targetTexture);
            _diffLayer = AddLayer(_stack, Tex(Bar(172, 71, DifferenceBarY, DifferenceBarH, 5), "Difference bar", FilterMode.Point, TextureWrapMode.Repeat), LayerBlendMode.Difference);
            Control(_stack, _diffLayer);
            StackView("Level with glitch", _stack, new Vector2(0f, -0.75f), new Vector2(17.2f, 7.1f), 0);

            var excl = BlendSprite("Exclusion bar", Tex(Bar(160, ExclusionBarH, 0, ExclusionBarH, 11), "Exclusion bar"),
                Vector2.zero, 16.0f, 3, LayerBlendMode.Exclusion, out _);
            _exclusionBar = excl.transform;
            // Place it so it covers rows ExclusionBarY.. of the 71-row demo.
            float rowsToUnits = 7.1f / 71f;
            _exclusionBar.localPosition = new Vector3(0f, -4.3f + (ExclusionBarY + ExclusionBarH * 0.5f) * rowsToUnits, 0f);

            if (animateToggle != null)
            {
                animateToggle.SetIsOnWithoutNotify(animate);
                animateToggle.onValueChanged.AddListener(SetAnimate);
            }
        }

        // Show or hide each bar (for tests: compare the picture with and without a bar).
        public void SetBarsVisible(bool difference, bool exclusion)
        {
            _diffLayer.visible = difference;
            _stack.MarkDirty();
            _exclusionBar.GetComponent<SpriteRenderer>().enabled = exclusion;
        }

        public void SetAnimate(bool on)
        {
            animate = on;
            if (animateToggle != null) animateToggle.SetIsOnWithoutNotify(on);
            if (!on)
            {
                _diffLayer.uvOffset = Vector2.zero;
                _stack.MarkDirty();
                if (_exclusionBar != null) _exclusionBar.localPosition = new Vector3(0f, _exclusionBar.localPosition.y, 0f);
            }
        }

        void Update()
        {
            if (!animate || _diffLayer == null || Time.time < _nextJitter) return;
            _nextJitter = Time.time + Random.Range(0.06f, 0.25f);
            _diffLayer.uvOffset = new Vector2(Random.Range(-0.08f, 0.08f), 0f);
            _stack.MarkDirty();
            _exclusionBar.localPosition = new Vector3(Random.Range(-0.5f, 0.5f), _exclusionBar.localPosition.y, 0f);
        }

        // A band of bright glitch blocks, transparent elsewhere.
        static PixelCanvas Bar(int w, int h, int y0, int barH, int seed)
        {
            var c = new PixelCanvas(w, h, Color.clear);
            var colors = new[] { new Color(0f, 1f, 1f), new Color(1f, 0f, 1f), Color.white, new Color(1f, 1f, 0f), new Color(0.3f, 1f, 0.3f) };
            int x = 0, i = 0;
            while (x < w)
            {
                int len = 3 + (int)(ProcArt.Rand(i, seed) * 14);
                if (ProcArt.Rand(i, seed + 1) > 0.2f)
                {
                    int rowOffset = (int)(ProcArt.Rand(i, seed + 2) * 3) - 1;
                    c.Rect(x, y0 + Mathf.Max(0, rowOffset), len, barH - Mathf.Abs(rowOffset), colors[(int)(ProcArt.Rand(i, seed + 3) * colors.Length) % colors.Length]);
                }
                x += len; i++;
            }
            return c;
        }

        void BuildLevel(Transform stage)
        {
            var sky = new PixelCanvas(4, 32, Color.white);
            sky.VerticalGradient(new Color(0.75f, 0.9f, 1f), new Color(0.35f, 0.6f, 0.95f));
            Fit(Sprite("Sky", Tex(sky, "Sky", FilterMode.Bilinear), Vector2.zero, 17.4f, 0, stage), new Vector2(17.4f, 7.2f));

            var block = new PixelCanvas(8, 8, new Color(0.55f, 0.35f, 0.2f));
            block.Rect(0, 6, 8, 2, new Color(0.35f, 0.75f, 0.3f));
            block.Rect(0, 0, 8, 1, new Color(0.4f, 0.25f, 0.15f));
            var blockTex = Tex(block, "Block");
            for (int i = 0; i <= 22; i++) Sprite("Ground", blockTex, new Vector2(-8.8f + i * 0.8f, -3.15f), 0.8f, 1, stage);
            for (int i = 0; i < 4; i++) Sprite("Platform", blockTex, new Vector2(-5f + i * 0.8f, -0.6f), 0.8f, 1, stage);
            for (int i = 0; i < 5; i++) Sprite("Platform", blockTex, new Vector2(2.4f + i * 0.8f, 0.6f), 0.8f, 1, stage);

            var coin = new PixelCanvas(8, 8, Color.clear);
            coin.Circle(4, 4, 3.5f, new Color(1f, 0.82f, 0.2f)); coin.Rect(3, 2, 2, 4, new Color(1f, 0.95f, 0.6f));
            var coinTex = Tex(coin, "Coin");
            for (int i = 0; i < 4; i++) Sprite("Coin", coinTex, new Vector2(2.8f + i * 0.8f, 1.5f), 0.5f, 2, stage);

            Sprite("Hero", Tex(ProcArt.Character(CharacterPalette.Hero), "Hero"), new Vector2(-3.8f, 0.6f), 1.4f, 3, stage);
            var cloud = new PixelCanvas(24, 10, Color.clear);
            cloud.Ellipse(8, 4, 7, 4, Color.white); cloud.Ellipse(16, 5, 8, 5, Color.white);
            Sprite("Cloud", Tex(cloud, "Cloud"), new Vector2(-5.5f, 2.6f), 2.6f, 1, stage);
            Sprite("Cloud", Tex(cloud, "Cloud"), new Vector2(6f, 2.9f), 2.0f, 1, stage);
        }
    }
}
