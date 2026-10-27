using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BlendMode.Showcase
{
    // Station 13: a real Tilemap, filmed by a station camera.
    //   Shadows tilemap: a baked shadow layer drawn with Multiply on its TilemapRenderer (Fixed-function).
    //   Then the camera picture gets a large, soft noise layer with Overlay (Compositor),
    //   so the same grass tile repeated 100 times no longer looks identical.
    public class S13_TilemapStation : Station
    {
        public const int Cols = 18, Rows = 8;
        public const float NoiseStrength = 0.85f;

        public Tilemap Ground { get; private set; }
        public Tilemap Walls { get; private set; }
        public Tilemap Shadows { get; private set; }
        public BlendLayerStack TileStack { get; private set; }
        public Camera TileCamera { get; private set; }
        public readonly List<Vector2Int> ShadowCells = new List<Vector2Int>();
        public readonly List<Vector2Int> WallCells = new List<Vector2Int>();

        protected override void Build()
        {
            var stage = OffstageRoot("Offstage tilemap");
            var grid = new GameObject("Grid", typeof(Grid)).transform;
            grid.SetParent(stage, false);
            grid.localPosition = new Vector3(-Cols * 0.5f, -Rows * 0.5f + 0.45f, 0f);

            var grass = MakeTile(GrassTile(), "Grass");
            var path = MakeTile(PathTile(), "Path");
            var rock = MakeTile(RockTile(), "Rock");
            var shadow = MakeTile(new PixelCanvas(16, 16, new Color(0.35f, 0.3f, 0.55f, 0.65f)), "Shadow");

            Ground = Layer(grid, "Ground", 0, FixedMaterial(LayerBlendMode.Normal));
            Walls = Layer(grid, "Walls", 2, FixedMaterial(LayerBlendMode.Normal));
            var shadowMat = FixedMaterial(LayerBlendMode.Multiply);
            Control(shadowMat, LayerBlendMode.Multiply);
            Shadows = Layer(grid, "Shadows (Multiply)", 1, shadowMat);

            for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Cols; x++)
                Ground.SetTile(new Vector3Int(x, y, 0), y == 2 || (x == 9 && y < 2) ? path : grass);

            // A few rock walls. Each casts a baked shadow to the right and below.
            var walls = new[] { (3, 5), (4, 5), (5, 5), (12, 6), (13, 6), (13, 5), (7, 4), (15, 3) };
            foreach (var (x, y) in walls) { Walls.SetTile(new Vector3Int(x, y, 0), rock); WallCells.Add(new Vector2Int(x, y)); }
            foreach (var (x, y) in walls)
            foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, -1) })
            {
                var cell = new Vector2Int(x + d.x, y + d.y);
                if (WallCells.Contains(cell) || ShadowCells.Contains(cell)) continue;
                Shadows.SetTile(new Vector3Int(cell.x, cell.y, 0), shadow);
                ShadowCells.Add(cell);
            }

            // 1 tile = 1 world unit = 50 px in the camera picture.
            TileCamera = StationCamera("Tilemap camera", stage.position, 7.1f, 860, 355, new Color(0.2f, 0.3f, 0.2f));
            TileStack = Stack("Tile variation compositor", 860, 355, live: true);
            AddLayer(TileStack, TileCamera.targetTexture);
            var noise = Own(ProcArt.Noise(256, 6f, 3, 77, new Color(0.25f, 0.25f, 0.25f), new Color(0.75f, 0.75f, 0.75f), false, "Variation noise"));
            Control(TileStack, AddLayer(TileStack, noise, LayerBlendMode.Overlay, NoiseStrength)).strength = NoiseStrength;
            StackView("Tilemap", TileStack, new Vector2(0f, -0.75f), new Vector2(17.2f, 7.1f), 0);
        }

        Tile MakeTile(PixelCanvas art, string name)
        {
            var tile = Own(ScriptableObject.CreateInstance<Tile>());
            tile.name = name;
            tile.sprite = Own(ProcArt.ToSprite(Tex(art, name), 1f, new Vector2(0.5f, 0.5f)));
            return tile;
        }

        static Tilemap Layer(Transform grid, string name, int order, Material mat)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(grid, false);
            var r = go.GetComponent<TilemapRenderer>();
            r.sortingOrder = order;
            r.sharedMaterial = mat;
            return go.GetComponent<Tilemap>();
        }

        static PixelCanvas GrassTile()
        {
            var c = new PixelCanvas(16, 16, new Color(0.38f, 0.66f, 0.32f));
            c.Rect(3, 4, 1, 2, new Color(0.3f, 0.55f, 0.25f)); c.Rect(10, 9, 1, 2, new Color(0.3f, 0.55f, 0.25f));
            c.Rect(12, 3, 1, 2, new Color(0.48f, 0.76f, 0.38f)); c.Rect(6, 12, 1, 2, new Color(0.48f, 0.76f, 0.38f));
            return c;
        }

        static PixelCanvas PathTile()
        {
            var c = new PixelCanvas(16, 16, new Color(0.76f, 0.66f, 0.46f));
            c.Rect(2, 3, 2, 1, new Color(0.65f, 0.55f, 0.38f)); c.Rect(11, 10, 2, 1, new Color(0.65f, 0.55f, 0.38f));
            return c;
        }

        static PixelCanvas RockTile()
        {
            var c = new PixelCanvas(16, 16, new Color(0.45f, 0.45f, 0.5f));
            c.Rect(0, 12, 16, 4, new Color(0.6f, 0.6f, 0.65f));
            c.Rect(0, 0, 16, 1, new Color(0.3f, 0.3f, 0.35f));
            c.Rect(7, 1, 1, 11, new Color(0.35f, 0.35f, 0.4f));
            return c;
        }
    }
}
