using UnityEngine;

namespace BlendMode.Showcase
{
    // Station 5: the same fire and sparks twice, side by side. Left: Normal. Right: Add (Linear dodge).
    // Both sides use the same random seed, so the particles match one for one and only the blending differs.
    public class S05_ParticlesStation : Station
    {
        const uint Seed = 4242;
        readonly System.Collections.Generic.List<ParticleSystem> _systems = new System.Collections.Generic.List<ParticleSystem>();

        protected override void Build()
        {
            var bg = new PixelCanvas(4, 64, Color.white);
            bg.VerticalGradient(new Color(0.12f, 0.06f, 0.08f), new Color(0.03f, 0.03f, 0.07f));
            Fit(Sprite("Night background", Tex(bg, "Night", FilterMode.Bilinear), new Vector2(0f, -0.75f), 17.2f, 0), new Vector2(17.2f, 7.1f));

            var divider = new PixelCanvas(1, 1, new Color(1f, 1f, 1f, 0.15f));
            Fit(Sprite("Divider", Tex(divider, "Divider"), new Vector2(0f, -0.75f), 1f, 1), new Vector2(0.04f, 7.1f));

            var soft = Own(ProcArt.Radial(64, 64, Color.white, Color.white, 2f, "Soft particle"));
            var normalMat = FixedMaterial(LayerBlendMode.Normal);
            normalMat.mainTexture = soft;
            var addMat = FixedMaterial(LayerBlendMode.LinearDodge);
            addMat.mainTexture = soft;
            Control(addMat, LayerBlendMode.LinearDodge);

            foreach (var (x, mat) in new[] { (-4.3f, normalMat), (4.3f, addMat) })
            {
                var root = new GameObject(x < 0 ? "Normal side" : "Add side").transform;
                root.SetParent(Demo, false);
                root.localPosition = new Vector3(x, -3.6f, 0f);
                _systems.Add(Fire(root, mat));
                _systems.Add(Sparks(root, mat));
            }

            // Start every system on the same frame with the same seed so both sides match.
            foreach (var ps in _systems) ps.Play();
        }

        ParticleSystem Fire(Transform parent, Material mat)
        {
            var ps = NewSystem("Fire", parent, mat, Seed);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            main.startColor = new Color(1f, 0.45f, 0.12f, 0.55f);
            main.maxParticles = 400;
            var emission = ps.emission; emission.rateOverTime = 110f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 14f; shape.radius = 0.8f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Gradient(new Color(1f, 0.85f, 0.4f), new Color(0.9f, 0.2f, 0.05f), 0.55f));
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            return ps;
        }

        ParticleSystem Sparks(Transform parent, Material mat)
        {
            var ps = NewSystem("Sparks", parent, mat, Seed + 1);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startColor = new Color(1f, 0.85f, 0.45f, 1f);
            main.gravityModifier = 0.35f;
            main.maxParticles = 200;
            var emission = ps.emission; emission.rateOverTime = 30f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 30f; shape.radius = 0.3f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Gradient(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.4f, 0.1f), 1f));
            return ps;
        }

        static ParticleSystem NewSystem(string name, Transform parent, Material mat, uint seed)
        {
            var go = new GameObject(name);
            go.SetActive(false);                   // configure before it starts playing
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ps.useAutoRandomSeed = false;
            ps.randomSeed = seed;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortingOrder = 5;
            go.SetActive(true);
            return ps;
        }

        static Gradient Gradient(Color start, Color end, float startAlpha)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                      new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(startAlpha, 0.5f), new GradientAlphaKey(0f, 1f) });
            return g;
        }
    }
}
