using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace BlendMode.Showcase.Audit
{
    // Part 4: walks BlendShowcase2D in Play mode and checks every station.
    // For each station: ON and OFF captures (saved to BlendShowcaseReport/), the ON/OFF pixel difference over the
    // demo area, the station-specific check, blendInGamma on vs off, and the average frame time.
    // Run: open BlendShowcase2D, press Play, then Tools > Blend Mode > Run Showcase Audit.
    // Keep the Editor focused while it runs: Play mode does not advance frames in the background.
    public static class ShowcaseAudit
    {
        public static string Status = "idle";
        public static bool Running;
        public static readonly List<Result> Results = new List<Result>();
        public static readonly List<string> ConsoleIssues = new List<string>();

        const int W = 1920, H = 1080;
        static readonly RectInt Demo = new RectInt(100, 110, 1720, 710);   // the demo area on screen, in pixels from bottom-left
        static string ReportDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BlendShowcaseReport"));

        public class Result
        {
            public int index;
            public string title, modes, approach;
            public float diffPct;
            public bool diffOk;
            public bool checkOk;
            public string check = "", notes = "";
            public float gammaChangePct, gammaMeanDiff;
            public float frameMs;
        }

        static readonly string[] Modes =
        {
            "All 27", "Screen", "Multiply", "Multiply, Soft light", "Add vs Normal", "Add", "Dissolve", "Difference, Exclusion",
            "Color", "Hue, Soft light", "Saturation (+Multiply)", "Overlay, Soft light", "Multiply, Overlay", "Screen, Soft light",
            "Soft light", "Screen, Multiply, Saturation, Add", "Difference"
        };
        static readonly string[] Approaches =
        {
            "Compositor", "Fixed", "Fixed", "Compositor", "Fixed", "Fixed", "Compositor", "Fixed + Compositor", "Compositor",
            "Compositor", "Compositor", "Compositor", "Fixed + Compositor", "Fixed + Compositor", "Compositor", "Fixed (UI) + Compositor", "Compositor"
        };

        [MenuItem("Tools/Blend Mode/Run Showcase Audit (Play mode)")]
        public static void RunFromMenu()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Open BlendShowcase2D and press Play first."); return; }
            _ = Run();
        }

        public static async Task Run(int only = -1)
        {
            if (Running) return;
            Running = true;
            Results.Clear();
            ConsoleIssues.Clear();
            Application.logMessageReceived += OnLog;
            bool asyncCompile = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;   // no cyan placeholder shaders in the captures (session only)
            try
            {
                Directory.CreateDirectory(ReportDir);
                var controller = UnityEngine.Object.FindAnyObjectByType<ShowcaseController>();
                if (controller == null) throw new Exception("ShowcaseController not found. Open BlendShowcase2D and press Play.");

                // Warm up: visit every station once so every shader variant and RenderTexture exists.
                for (int i = 0; i < controller.stations.Count; i++) { controller.GoTo(i, true); await Frames(3); }

                for (int i = 0; i < controller.stations.Count; i++)
                {
                    if (only >= 0 && i != only) continue;
                    Status = $"station {i + 1}";
                    var r = new Result { index = i + 1, title = controller.stations[i].title, modes = Modes[i], approach = Approaches[i] };
                    try { await AuditStation(controller, i, r); }
                    catch (Exception e) { r.checkOk = false; r.check = "EXCEPTION"; r.notes += e.GetType().Name + ": " + e.Message; }
                    Results.Add(r);
                }
                Time.timeScale = 1f;
                WriteReport();
                Status = "done";
            }
            catch (Exception e)
            {
                Status = "failed: " + e.Message;
                Debug.LogException(e);
            }
            finally
            {
                Time.timeScale = 1f;
                ShaderUtil.allowAsyncCompilation = asyncCompile;
                Application.logMessageReceived -= OnLog;
                Running = false;
            }
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Log) return;
            ConsoleIssues.Add($"{type}: {message.Split('\n')[0]}");
        }

        // ---------- stress sweep (tester): abuse every control, watch the Console and resource counts ----------

        public static string StressReport = "";

        public static async Task Stress()
        {
            if (Running) return;
            Running = true;
            ConsoleIssues.Clear();
            Application.logMessageReceived += OnLog;
            var sb = new StringBuilder();
            try
            {
                var c = UnityEngine.Object.FindAnyObjectByType<ShowcaseController>();
                int CountRTs() => Resources.FindObjectsOfTypeAll<RenderTexture>().Length;
                int CountMats() => Resources.FindObjectsOfTypeAll<Material>().Length;

                // Navigation past both ends must clamp, not throw.
                c.GoTo(-5, true); await Frames(2);
                sb.AppendLine($"GoTo(-5) -> index {c.Index}");
                c.GoTo(99, true); await Frames(2);
                sb.AppendLine($"GoTo(99) -> index {c.Index}");
                for (int k = 0; k < 5; k++) c.Next();
                await Frames(2);
                sb.AppendLine($"Next x5 at the end -> index {c.Index}");

                // First lap to let every station create its resources, then measure a second lap.
                for (int i = 0; i < c.stations.Count; i++) { c.GoTo(i, true); await Frames(3); }
                int rt0 = CountRTs(), mat0 = CountMats();

                for (int i = 0; i < c.stations.Count; i++)
                {
                    Status = $"stress {i + 1}";
                    var st = c.stations[i];
                    c.GoTo(i, true);
                    await Frames(2);
                    st.SetOpacity(0f); await Frames(2);
                    st.SetOpacity(1f); await Frames(2);
                    st.SetOpacity(-3f); st.SetOpacity(7f); await Frames(1);          // out-of-range values clamp
                    for (int k = 0; k < 10; k++) { c.ToggleBlend(); await Frames(1); }  // rapid toggling, ends ON
                    if (!st.blendOn) sb.AppendLine($"station {i + 1}: blend ended OFF after an even number of toggles");
                    if (st.opacity < 0f || st.opacity > 1f) sb.AppendLine($"station {i + 1}: opacity out of range {st.opacity}");
                    st.OnKey(KeyCode.F); st.OnKey(KeyCode.R);                          // stray keys must be harmless
                    switch (st)
                    {
                        case S04_DayNightStation dn: dn.SetNight(-1f); dn.SetNight(2f); dn.SetNight(0.6f); break;
                        case S09_TeamColorsStation team: team.SetTeam(-1); team.SetTeam(99); team.SetTeam(0); break;
                        case S10_StatusEffectsStation fx: fx.SetEffects(true, true, true); await Frames(2); fx.SetEffects(true, false, false); break;
                        case S11_DesaturateStation d: d.SetHealth(-1f); d.SetHealth(5f); d.SetPaused(true); d.SetPaused(false); d.SetHealth(0.3f); break;
                        case S17_CompareStation cmp: cmp.SetShowChanges(false); cmp.SetShowChanges(true); break;
                    }
                    await Frames(2);
                }
                int rt1 = CountRTs(), mat1 = CountMats();
                sb.AppendLine($"RenderTextures {rt0} -> {rt1}, Materials {mat0} -> {mat1} after a full abusive lap");
                c.GoTo(0, true);
            }
            catch (Exception e) { sb.AppendLine("EXCEPTION " + e); }
            finally
            {
                Application.logMessageReceived -= OnLog;
                sb.AppendLine($"Console warnings/errors during stress: {ConsoleIssues.Count}");
                foreach (var i in ConsoleIssues.Distinct()) sb.AppendLine("  " + i);
                StressReport = sb.ToString();
                Status = "stress done";
                Running = false;
            }
        }

        // ---------- one station ----------

        static async Task AuditStation(ShowcaseController c, int i, Result r)
        {
            var st = c.stations[i];
            c.GoTo(i, true);
            Time.timeScale = 1f;
            st.SetBlend(true);
            await Frames(6);

            await Prepare(st);                // put animated stations in a state where ON/OFF is meaningful
            Time.timeScale = 0f;              // freeze animation so ON vs OFF only differs by the blend
            await Frames(3);

            st.SetBlend(true); await Frames(3);
            var on = Capture();
            Save(on, $"{i + 1:00}_{Slug(st.title)}_ON.png");
            st.SetBlend(false); await Frames(3);
            var off = Capture();
            Save(off, $"{i + 1:00}_{Slug(st.title)}_OFF.png");
            st.SetBlend(true); await Frames(3);

            r.diffPct = ChangedFraction(on, off, W, Demo, 8) * 100f;
            r.diffOk = r.diffPct > 2f;
            if (!r.diffOk) r.notes += "ON and OFF look identical: the blend is not taking effect. ";

            // blendInGamma on vs off (every stack in this station).
            var stacks = st.GetComponentsInChildren<BlendLayerStack>(true);
            foreach (var s in stacks) { s.blendInGamma = false; s.MarkDirty(); }
            await Frames(3);
            var noGamma = Capture();
            foreach (var s in stacks) { s.blendInGamma = true; s.MarkDirty(); }
            await Frames(3);
            r.gammaChangePct = ChangedFraction(on, noGamma, W, Demo, 2) * 100f;
            r.gammaMeanDiff = MeanAbsDiff(on, noGamma, W, Demo) * 255f;
            if (stacks.Length == 0) r.notes += "No compositor stack, so blendInGamma does not apply. ";

            // Station-specific check (some need time to run).
            var (ok, text) = await Check(st, i, on, off);
            r.checkOk = ok;
            r.check = text;

            // Average frame time over 2 s of normal play.
            Time.timeScale = 1f;
            await Frames(5);
            float t0 = Time.realtimeSinceStartup; int f0 = Time.frameCount;
            await Seconds(2f);
            int frames = Mathf.Max(1, Time.frameCount - f0);
            r.frameMs = (Time.realtimeSinceStartup - t0) * 1000f / frames;
            await Restore(st);
        }

        static async Task Prepare(Station st)
        {
            switch (st)
            {
                case S06_HitFlashStation hit:
                    hit.Hit();                    // freeze on the first frame of a hit, flash at full strength
                    await Frames(1);
                    break;
                case S07_DissolveStation dis:
                    dis.SetOpacity(0.5f);         // stops its animation
                    break;
                case S08_GlitchStation g:
                    g.SetAnimate(false);
                    break;
                case S16_UIStation ui:
                    ui.AnimateShine = false;
                    ui.Shine.anchoredPosition = new Vector2(30f, 0f);
                    break;
            }
            await Frames(2);
        }

        static async Task Restore(Station st)
        {
            switch (st)
            {
                case S07_DissolveStation dis: dis.SetAnimate(true); dis.SetOpacity(1f); dis.SetAnimate(true); break;
                case S08_GlitchStation g: g.SetBarsVisible(true, true); g.SetAnimate(true); break;
                case S16_UIStation ui: ui.AnimateShine = true; break;
                case S17_CompareStation cmp: cmp.SetShowChanges(true); break;
                case S11_DesaturateStation d: d.SetPaused(false); d.SetHealth(0.3f); break;
                case S10_StatusEffectsStation fx: fx.SetEffects(true, false, false); break;
                case S09_TeamColorsStation team: team.SetTeam(0); break;
                case S04_DayNightStation dn: dn.SetNight(0.6f); break;
            }
            await Frames(2);
        }

        // ---------- station-specific checks ----------

        static async Task<(bool, string)> Check(Station st, int i, Color32[] on, Color32[] off)
        {
            switch (st)
            {
                case S01_GalleryStation g: return GalleryCheck(g);
                case S02_TorchGlowStation _: return BrightnessCheck(on, off, brighter: true);
                case S03_ShadowsStation _: return BrightnessCheck(on, off, brighter: false);
                case S04_DayNightStation dn: return await DayNightCheck(dn);
                case S05_ParticlesStation _: return ParticlesCheck(on);
                case S06_HitFlashStation hit: return await HitFlashCheck(hit);
                case S07_DissolveStation dis: return await DissolveCheck(dis);
                case S08_GlitchStation gl: return await GlitchCheck(gl);
                case S09_TeamColorsStation team: return await TeamCheck(team);
                case S10_StatusEffectsStation fx: return await StatusCheck(fx);
                case S11_DesaturateStation d: return await DesaturateCheck(d);
                case S12_TextureDetailStation tex: return TextureCheck(tex);
                case S13_TilemapStation tm: return TilemapCheck(tm);
                case S14_HazeStation hz: return HazeCheck(hz);
                case S15_WaterStation w: return await WaterCheck(w);
                case S16_UIStation ui: return await UiCheck(ui);
                case S17_CompareStation cmp: return await CompareCheck(cmp);
            }
            return (false, "no check");
        }

        static (bool, string) GalleryCheck(S01_GalleryStation g)
        {
            var tiles = g.gallery.tiles;
            int labelled = 0;
            for (int k = 0; k < tiles.Count; k++)
            {
                var label = tiles[k].transform.parent.GetComponentInChildren<TMP_Text>();
                if (label != null && label.text.Contains(ObjectNames.NicifyVariableName(((LayerBlendMode)k).ToString()).Split(' ')[0])) labelled++;
            }
            var pixels = tiles.Select(t => ReadRT(t.Result)).ToList();
            var identical = new List<string>();
            float minDiff = float.MaxValue;
            for (int k = 0; k < tiles.Count; k++)
            foreach (int n in new[] { k + 1, k + 9 })
            {
                if (n >= tiles.Count || (n == k + 1 && n % 9 == 0)) continue;
                float d = MeanAbsDiff(pixels[k], pixels[n], tiles[k].Result.width, new RectInt(0, 0, tiles[k].Result.width, tiles[k].Result.height)) * 255f;
                minDiff = Mathf.Min(minDiff, d);
                if (d < 0.5f) identical.Add($"{(LayerBlendMode)k}={(LayerBlendMode)n}");
            }
            bool ok = tiles.Count == 27 && labelled == 27 && identical.Count == 0;
            return (ok, $"{tiles.Count} tiles, {labelled} labelled, smallest neighbour difference {minDiff:0.0}/255" +
                        (identical.Count > 0 ? ", identical neighbours: " + string.Join(", ", identical) : ""));
        }

        static (bool, string) BrightnessCheck(Color32[] on, Color32[] off, bool brighter)
        {
            float mOn = MeanLum(on, W, Demo), mOff = MeanLum(off, W, Demo);
            int wrong = 0; float worst = 0f;
            ForEach(Demo, (x, y) =>
            {
                int k = y * W + x;
                float d = Lum(on[k]) - Lum(off[k]);
                float bad = brighter ? -d : d;
                if (bad > worst) worst = bad;
                if (bad > 2f / 255f) wrong++;
            });
            bool ok = (brighter ? mOn > mOff : mOn < mOff) && wrong == 0;
            return (ok, $"mean brightness {mOff * 255f:0.0} -> {mOn * 255f:0.0}; pixels going the wrong way by > 2/255: {wrong}; worst {worst * 255f:0.0}/255");
        }

        static async Task<(bool, string)> DayNightCheck(S04_DayNightStation dn)
        {
            var values = new List<float>();
            foreach (float n in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            {
                dn.SetNight(n);
                await Frames(4);
                values.Add(MeanLum(Capture(), W, Demo) * 255f);
            }
            bool ok = true;
            for (int k = 1; k < values.Count; k++) if (!(values[k] < values[k - 1])) ok = false;
            return (ok, "mean brightness at night 0, 25, 50, 75, 100%: " + string.Join(", ", values.Select(v => v.ToString("0.0"))));
        }

        static (bool, string) ParticlesCheck(Color32[] on)
        {
            var left = new RectInt(Demo.x, Demo.y, Demo.width / 2, Demo.height);
            var right = new RectInt(Demo.x + Demo.width / 2, Demo.y, Demo.width / 2, Demo.height);
            float pl = Percentile(on, W, left, 0.995f), pr = Percentile(on, W, right, 0.995f);
            float ml = Percentile(on, W, left, 1f), mr = Percentile(on, W, right, 1f);
            return (pr > pl, $"99.5th-percentile brightness Normal {pl * 255f:0} vs Add {pr * 255f:0}; peak {ml * 255f:0} vs {mr * 255f:0}");
        }

        static async Task<(bool, string)> HitFlashCheck(S06_HitFlashStation hit)
        {
            var region = new RectInt(780, 190, 360, 520);
            Time.timeScale = 1f;
            await Seconds(0.6f);                          // let any earlier flash finish
            var baseline = Capture();
            hit.Hit();
            float hitTime = Time.realtimeSinceStartup;
            await Frames(1);
            var flash = Capture();
            float flashUp = (MeanLum(flash, W, region) - MeanLum(baseline, W, region)) * 255f;
            float backAt = -1f;
            while (Time.realtimeSinceStartup - hitTime < 1f)
            {
                if (hit.FlashAmount <= 0f) { backAt = Time.realtimeSinceStartup - hitTime; break; }
                await Frames(1);
            }
            await Seconds(Mathf.Max(0f, 0.5f - (Time.realtimeSinceStartup - hitTime)));
            var after = Capture();
            float residual = MaxAbsDiff(after, baseline, W, region) * 255f;
            bool ok = flashUp > 10f && backAt >= 0f && backAt <= 0.5f && residual <= 2f;
            return (ok, $"flash raises the knight's brightness by {flashUp:0.0}/255, back to normal after {backAt:0.00} s, difference at 0.5 s {residual:0}/255");
        }

        static async Task<(bool, string)> DissolveCheck(S07_DissolveStation dis)
        {
            var region = new RectInt(780, 200, 360, 480);
            dis.SetOpacity(1f); await Frames(3); var full = Capture();
            dis.SetOpacity(0f); await Frames(3); var none = Capture();
            dis.SetOpacity(0.5f); await Frames(3); var half = Capture();
            int character = 0, fromSource = 0;
            ForEach(region, (x, y) =>
            {
                int k = y * W + x;
                if (Dist(full[k], none[k]) < 24) return;   // not a character pixel
                character++;
                if (Dist(half[k], full[k]) < Dist(half[k], none[k])) fromSource++;
            });
            float frac = character > 0 ? fromSource / (float)character : 0f;
            return (frac >= 0.4f && frac <= 0.6f, $"at 50%: {frac:P1} of {character} character pixels come from the source");
        }

        static async Task<(bool, string)> GlitchCheck(S08_GlitchStation g)
        {
            // Difference bar: compare the compositor result with and without it, inside the bar rows.
            g.SetBarsVisible(true, true); await Frames(3);
            var withDiff = ReadRT(g.GlitchStack.Result);
            g.SetBarsVisible(false, true); await Frames(3);
            var noDiff = ReadRT(g.GlitchStack.Result);
            int rw = g.GlitchStack.Result.width, rh = g.GlitchStack.Result.height;
            float scale = rh / 71f;
            var band = new RectInt(0, Mathf.RoundToInt(S08_GlitchStation.DifferenceBarY * scale), rw, Mathf.RoundToInt(S08_GlitchStation.DifferenceBarH * scale));
            float diffChange = ChangedFraction(withDiff, noDiff, rw, band, 8) * 100f;

            // Exclusion bar: screen capture with and without the sprite.
            g.SetBarsVisible(true, true); await Frames(3);
            var withExcl = Capture();
            g.SetBarsVisible(true, false); await Frames(3);
            var noExcl = Capture();
            var exBand = new RectInt(Demo.x + 80, Demo.y + S08_GlitchStation.ExclusionBarY * 10, Demo.width - 160, S08_GlitchStation.ExclusionBarH * 10);
            float exChange = ChangedFraction(withExcl, noExcl, W, exBand, 8) * 100f;
            g.SetBarsVisible(true, true);

            // Difference on identical content must be black.
            var tmp = new GameObject("Audit identical difference") { hideFlags = HideFlags.HideAndDontSave };
            var stack = tmp.AddComponent<BlendLayerStack>();
            stack.Shader = Shader.Find("Blend Mode/BlendCompositor");
            stack.resolution = new Vector2Int(rw, rh);
            stack.layers.Add(new BlendLayer { texture = g.CameraTexture });
            stack.layers.Add(new BlendLayer { texture = g.CameraTexture, mode = LayerBlendMode.Difference });
            stack.Composite();
            var black = ReadRT(stack.Result);
            int maxChannel = black.Max(p => Math.Max(p.r, Math.Max(p.g, p.b)));
            UnityEngine.Object.Destroy(tmp);

            bool ok = diffChange > 20f && exChange > 20f && maxChannel <= 2;
            return (ok, $"Difference bar changes {diffChange:0}% of its rows, Exclusion bar {exChange:0}%; Difference of the scene with itself: max {maxChannel}/255");
        }

        static async Task<(bool, string)> TeamCheck(S09_TeamColorsStation team)
        {
            var src = (Texture2D)team.TeamStack.layers[0].texture;
            var gray = src.GetPixels32();
            var parts = new List<string>();
            bool allOk = true;
            for (int t = 0; t < S09_TeamColorsStation.Teams.Length; t++)
            {
                team.SetTeam(t);
                await Frames(3);
                var res = ReadRT(team.TeamStack.Result);
                int rw = team.TeamStack.Result.width, sx = rw / src.width;
                Color.RGBToHSV(S09_TeamColorsStation.Teams[t], out float targetHue, out _, out _);
                var hueErrors = new List<float>();
                var lumByGray = new SortedDictionary<int, List<float>>();
                for (int y = 0; y < team.TeamStack.Result.height; y++)
                for (int x = 0; x < rw; x++)
                {
                    var g = gray[(y / sx) * src.width + x / sx];
                    if (g.a < 128) continue;
                    var p = res[y * rw + x];
                    if (!lumByGray.TryGetValue(g.r, out var list)) lumByGray[g.r] = list = new List<float>();
                    list.Add(Lum(p));
                    Color.RGBToHSV(p, out float h, out float s, out float v);
                    if (s > 0.25f && v > 0.15f) hueErrors.Add(Mathf.Abs(Mathf.DeltaAngle(h * 360f, targetHue * 360f)));
                }
                hueErrors.Sort();
                float median = hueErrors.Count > 0 ? hueErrors[hueErrors.Count / 2] : 999f;
                float within = hueErrors.Count > 0 ? hueErrors.Count(e => e <= 15f) / (float)hueErrors.Count : 0f;
                var means = lumByGray.Values.Select(l => l.Average()).ToList();
                bool ordered = true;
                for (int k = 1; k < means.Count; k++) if (!(means[k] > means[k - 1])) ordered = false;
                bool ok = median <= 15f && within >= 0.95f && ordered;
                allOk &= ok;
                parts.Add($"{S09_TeamColorsStation.TeamNames[t]}: median hue error {median:0.0} deg, {within:P0} within 15 deg, {means.Count} gray levels {(ordered ? "keep" : "LOSE")} their order");
            }
            return (allOk, string.Join("; ", parts));
        }

        static async Task<(bool, string)> StatusCheck(S10_StatusEffectsStation fx)
        {
            Color Mean(Color32[] px, Color32[] mask)
            {
                double r = 0, g = 0, b = 0; int n = 0;
                for (int k = 0; k < px.Length; k++) { if (mask[k].a < 128) continue; r += px[k].r; g += px[k].g; b += px[k].b; n++; }
                return new Color((float)(r / n / 255), (float)(g / n / 255), (float)(b / n / 255));
            }
            async Task<Color32[]> Read(bool p, bool f, bool b) { fx.SetEffects(p, f, b); await Frames(3); return ReadRT(fx.EffectStack.Result); }

            var none = await Read(false, false, false);
            var mask = none;
            var cNone = Mean(none, mask);
            var cPoison = Mean(await Read(true, false, false), mask);
            var cFrozen = Mean(await Read(false, true, false), mask);
            var cBurn = Mean(await Read(false, false, true), mask);

            float greenUp = (cPoison.g - Mathf.Max(cPoison.r, cPoison.b)) - (cNone.g - Mathf.Max(cNone.r, cNone.b));
            float blueUp = (cFrozen.b - cFrozen.r) - (cNone.b - cNone.r);
            float orangeUp = (cBurn.r - cBurn.b) - (cNone.r - cNone.b);
            Color.RGBToHSV(cPoison, out float hp, out _, out _);
            bool ok = greenUp > 0.05f && hp * 360f > 70f && hp * 360f < 170f && blueUp > 0.03f && orangeUp > 0.03f;
            return (ok, $"poisoned: green lead +{greenUp:0.00} (mean hue {hp * 360f:0} deg); frozen: blue minus red +{blueUp:0.00}; burning: red minus blue +{orangeUp:0.00}");
        }

        static async Task<(bool, string)> DesaturateCheck(S11_DesaturateStation d)
        {
            d.SetPaused(false); d.SetHealth(0f); await Frames(4);
            float satFull = MeanSaturation(ReadRT(d.SceneStack.Result));
            d.SetHealth(1f); await Frames(4);
            float satNone = MeanSaturation(ReadRT(d.SceneStack.Result));
            d.SetPaused(true); await Frames(4);
            float satPaused = MeanSaturation(ReadRT(d.SceneStack.Result));
            bool ok = satFull < 0.1f && satPaused < 0.1f && satNone > 0.3f;
            return (ok, $"average saturation: full health {satNone:0.00}, zero health {satFull:0.000}, paused {satPaused:0.000}");
        }

        static (bool, string) TextureCheck(S12_TextureDetailStation t)
        {
            var o = ReadRT(t.OverlayStack.Result);
            var s = ReadRT(t.SoftLightStack.Result);
            int rw = t.OverlayStack.Result.width, rh = t.OverlayStack.Result.height;
            var all = new RectInt(0, 0, rw, rh);
            float diff = MeanAbsDiff(o, s, rw, all) * 255f;
            float co = StdLum(o, rw, all) * 255f, cs = StdLum(s, rw, all) * 255f;
            return (diff > 5f && cs < co, $"mean difference Overlay vs Soft light {diff:0.0}/255; contrast (brightness std dev) Overlay {co:0.0}, Soft light {cs:0.0}");
        }

        static (bool, string) TilemapCheck(S13_TilemapStation tm)
        {
            var res = ReadRT(tm.TileStack.Result);
            int rw = tm.TileStack.Result.width, rh = tm.TileStack.Result.height;
            RectInt Block(Vector2Int cell, int size)
            {
                var c = tm.TileCamera.WorldToScreenPoint(tm.Ground.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0)));
                return new RectInt(Mathf.RoundToInt(c.x) - size / 2, Mathf.RoundToInt(c.y) - size / 2, size, size);
            }
            bool Inside(RectInt r) => r.xMin >= 0 && r.yMin >= 0 && r.xMax <= rw && r.yMax <= rh;
            TileBase TileAt(Vector2Int c) => tm.Ground.GetTile(new Vector3Int(c.x, c.y, 0));
            bool Plain(Vector2Int c) => !tm.WallCells.Contains(c) && !tm.ShadowCells.Contains(c);

            var shadowLum = new List<float>();
            var plainLum = new List<float>();
            for (int y = 0; y < S13_TilemapStation.Rows; y++)
            for (int x = 0; x < S13_TilemapStation.Cols; x++)
            {
                var cell = new Vector2Int(x, y);
                var r = Block(cell, 40);
                if (!Inside(r) || TileAt(cell)?.name != "Grass" || tm.WallCells.Contains(cell)) continue;
                (tm.ShadowCells.Contains(cell) ? shadowLum : plainLum).Add(MeanLum(res, rw, r));
            }

            int pairs = 0, identical = 0; float minDiff = float.MaxValue;
            for (int y = 0; y < S13_TilemapStation.Rows; y++)
            for (int x = 0; x < S13_TilemapStation.Cols; x++)
            foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(0, 1) })
            {
                var a = new Vector2Int(x, y); var b = a + d;
                if (!Plain(a) || !Plain(b) || TileAt(a) != TileAt(b) || TileAt(a) == null) continue;
                var ra = Block(a, 44); var rb = Block(b, 44);
                if (!Inside(ra) || !Inside(rb)) continue;
                double sum = 0;
                for (int j = 0; j < 44; j++)
                for (int i = 0; i < 44; i++)
                {
                    var pa = res[(ra.y + j) * rw + ra.x + i]; var pb = res[(rb.y + j) * rw + rb.x + i];
                    sum += Math.Abs(pa.r - pb.r) + Math.Abs(pa.g - pb.g) + Math.Abs(pa.b - pb.b);
                }
                float md = (float)(sum / (44 * 44 * 3));
                pairs++; minDiff = Mathf.Min(minDiff, md);
                if (md < 0.5f) identical++;
            }
            float ms = shadowLum.Count > 0 ? shadowLum.Average() * 255f : 0f, mp = plainLum.Count > 0 ? plainLum.Average() * 255f : 0f;
            float brightestShadow = shadowLum.Count > 0 ? shadowLum.Max() * 255f : 0f, darkestPlain = plainLum.Count > 0 ? plainLum.Min() * 255f : 0f;
            bool ok = shadowLum.Count > 0 && ms < mp && brightestShadow < darkestPlain && identical == 0 && pairs > 0;
            return (ok, $"shadowed grass {ms:0.0} vs open grass {mp:0.0} (brightest shadow {brightestShadow:0.0} < darkest open {darkestPlain:0.0}); " +
                        $"{pairs} adjacent identical-tile pairs, {identical} look exactly alike, smallest difference {minDiff:0.0}/255");
        }

        static (bool, string) HazeCheck(S14_HazeStation hz)
        {
            var res = ReadRT(hz.HazeStack.Result);
            int rw = hz.HazeStack.Result.width, rh = hz.HazeStack.Result.height;
            var center = (Vector2)hz.Offstage;
            float ppu = rh / 7.1f;
            var lum = new[] { new List<float>(), new List<float>(), new List<float>() };
            for (int y = 3; y < rh - 3; y += 2)
            for (int x = 3; x < rw - 3; x += 2)
            {
                Vector2 World(int px, int py) => center + new Vector2((px + 0.5f - rw * 0.5f) / ppu, (py + 0.5f - rh * 0.5f) / ppu);
                int layer = hz.VisibleLayerAt(World(x, y));
                if (layer < 0) continue;
                // skip edges: the neighbours 3 px away must be the same layer
                if (hz.VisibleLayerAt(World(x + 3, y)) != layer || hz.VisibleLayerAt(World(x - 3, y)) != layer ||
                    hz.VisibleLayerAt(World(x, y + 3)) != layer || hz.VisibleLayerAt(World(x, y - 3)) != layer) continue;
                lum[layer].Add(Lum(res[y * rw + x]));
            }
            float Mean(List<float> l) => l.Count > 0 ? l.Average() : 0f;
            float Std(List<float> l) { if (l.Count == 0) return 0f; float m = Mean(l); return Mathf.Sqrt(l.Average(v => (v - m) * (v - m))); }
            float mf = Mean(lum[0]) * 255f, mm = Mean(lum[1]) * 255f, mn = Mean(lum[2]) * 255f;
            float sf = Std(lum[0]) * 255f, sn = Std(lum[2]) * 255f;
            bool ok = lum[0].Count > 100 && lum[2].Count > 100 && mf > mm && mm > mn && sf < sn;
            return (ok, $"brightness far {mf:0.0} > mid {mm:0.0} > near {mn:0.0}; contrast (std dev) far {sf:0.0} vs near {sn:0.0}");
        }

        static async Task<(bool, string)> WaterCheck(S15_WaterStation w)
        {
            Time.timeScale = 1f;
            await Frames(2);
            var a = ReadRT(w.WaterStack.Result);
            await Seconds(1f);
            var b = ReadRT(w.WaterStack.Result);
            int rw = w.WaterStack.Result.width;
            float changed = ChangedFraction(a, b, rw, new RectInt(0, 0, rw, w.WaterStack.Result.height), 8) * 100f;
            Time.timeScale = 0f;
            return (changed > 2f, $"{changed:0.0}% of the pond's pixels change by more than 8/255 in one second");
        }

        static async Task<(bool, string)> UiCheck(S16_UIStation ui)
        {
            RectInt Button(int col) => new RectInt(960 + (int)S16_UIStation.Columns[col] - 130, 465 + 40 - 35, 260, 70);

            // Shine moves: sample the sweep at two moments.
            Time.timeScale = 1f;
            ui.AnimateShine = true;
            float x0 = float.NaN, x1 = float.NaN; Color32[] c0 = null, c1 = null;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 4f && float.IsNaN(x1))
            {
                await Frames(1);
                float x = ui.ShineX;
                if (Mathf.Abs(x) > 120f) continue;
                if (float.IsNaN(x0)) { x0 = x; c0 = Capture(); }
                else if (Mathf.Abs(x - x0) > 60f) { x1 = x; c1 = Capture(); }
            }
            float shineChange = c1 != null ? ChangedFraction(c0, c1, W, Button(0), 8) * 100f : 0f;

            // Pressed darker and disabled desaturated: freeze with the shine off the button.
            ui.AnimateShine = false;
            ui.Shine.anchoredPosition = new Vector2(-260f, 0f);
            Time.timeScale = 0f;
            await Frames(3);
            var shot = Capture();
            float lumNormal = MeanLum(shot, W, Button(0)), lumPressed = MeanLum(shot, W, Button(1));
            float satNormal = MeanSaturation(shot, W, Button(0)), satDisabled = MeanSaturation(shot, W, Button(2));
            bool ok = !float.IsNaN(x1) && shineChange > 1f && lumPressed < lumNormal && satDisabled < 0.1f && satNormal > 0.3f;
            return (ok, $"shine moved {x0:0} -> {x1:0} px and changed {shineChange:0.0}% of the button; brightness normal {lumNormal * 255f:0} vs pressed {lumPressed * 255f:0}; " +
                        $"saturation normal {satNormal:0.00} vs disabled {satDisabled:0.00}");
        }

        static async Task<(bool, string)> CompareCheck(S17_CompareStation cmp)
        {
            cmp.SetShowChanges(false); await Frames(4);
            var same = ReadRT(cmp.CompareStack.Result);
            int maxSame = same.Max(p => Math.Max(p.r, Math.Max(p.g, p.b)));
            cmp.SetShowChanges(true); await Frames(4);
            var diff = ReadRT(cmp.CompareStack.Result);
            int rw = cmp.CompareStack.Result.width, rh = cmp.CompareStack.Result.height;
            var rects = cmp.ChangePixelRects().Select(r => new RectInt(r.x - 3, r.y - 3, r.width + 6, r.height + 6)).ToList();
            var hits = new int[rects.Count];
            int outside = 0;
            for (int y = 0; y < rh; y++)
            for (int x = 0; x < rw; x++)
            {
                var p = diff[y * rw + x];
                if (Math.Max(p.r, Math.Max(p.g, p.b)) <= 24) continue;
                bool inAny = false;
                for (int k = 0; k < rects.Count; k++) if (rects[k].Contains(new Vector2Int(x, y))) { hits[k]++; inAny = true; }
                if (!inAny) outside++;
            }
            bool ok = maxSame <= 2 && outside == 0 && hits.All(h => h >= 10);
            return (ok, $"identical images: brightest pixel {maxSame}/255; with changes: bright pixels per changed area {string.Join(", ", hits)}, bright pixels elsewhere {outside}");
        }

        // ---------- capture and pixel helpers ----------

        static Color32[] Capture()
        {
            var cam = Camera.main;
            var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32);
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var req = new RenderPipeline.StandardRequest { destination = rt };
                RenderPipeline.SubmitRenderRequest(cam, req);
            }
            else
            {
                // Built-in Render Pipeline: render requests need an SRP, so render the camera into the target directly.
                var prevTarget = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = prevTarget;
            }
            var px = ReadRT(rt);
            RenderTexture.ReleaseTemporary(rt);
            return px;
        }

        static Color32[] ReadRT(RenderTexture rt)
        {
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, false);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            tex.Apply();
            RenderTexture.active = prev;
            var px = tex.GetPixels32();
            UnityEngine.Object.Destroy(tex);
            return px;
        }

        static void Save(Color32[] px, string file)
        {
            // Screenshots are opaque. The framebuffer's alpha channel holds blend leftovers (Normal blending writes
            // partial alpha), and viewers would show those pixels over white, washed out.
            var opaque = new Color32[px.Length];
            for (int k = 0; k < px.Length; k++) opaque[k] = new Color32(px[k].r, px[k].g, px[k].b, 255);
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, false);
            tex.SetPixels32(opaque);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(ReportDir, file), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        static void ForEach(RectInt r, Action<int, int> f)
        {
            for (int y = r.yMin; y < r.yMax; y++)
            for (int x = r.xMin; x < r.xMax; x++) f(x, y);
        }

        static float Lum(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
        static int Dist(Color32 a, Color32 b) => Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Abs(a.b - b.b)));

        static float ChangedFraction(Color32[] a, Color32[] b, int width, RectInt r, int threshold)
        {
            int changed = 0, total = 0;
            ForEach(r, (x, y) => { total++; if (Dist(a[y * width + x], b[y * width + x]) > threshold) changed++; });
            return total > 0 ? changed / (float)total : 0f;
        }

        static float MeanAbsDiff(Color32[] a, Color32[] b, int width, RectInt r)
        {
            double sum = 0; int n = 0;
            ForEach(r, (x, y) => { var p = a[y * width + x]; var q = b[y * width + x]; sum += Math.Abs(p.r - q.r) + Math.Abs(p.g - q.g) + Math.Abs(p.b - q.b); n += 3; });
            return n > 0 ? (float)(sum / n / 255.0) : 0f;
        }

        static float MaxAbsDiff(Color32[] a, Color32[] b, int width, RectInt r)
        {
            int max = 0;
            ForEach(r, (x, y) => max = Math.Max(max, Dist(a[y * width + x], b[y * width + x])));
            return max / 255f;
        }

        static float MeanLum(Color32[] px, int width, RectInt r)
        {
            double sum = 0; int n = 0;
            ForEach(r, (x, y) => { sum += Lum(px[y * width + x]); n++; });
            return n > 0 ? (float)(sum / n) : 0f;
        }

        static float StdLum(Color32[] px, int width, RectInt r)
        {
            float m = MeanLum(px, width, r);
            double sum = 0; int n = 0;
            ForEach(r, (x, y) => { float d = Lum(px[y * width + x]) - m; sum += d * d; n++; });
            return n > 0 ? Mathf.Sqrt((float)(sum / n)) : 0f;
        }

        static float Percentile(Color32[] px, int width, RectInt r, float p)
        {
            var values = new List<float>(r.width * r.height);
            ForEach(r, (x, y) => values.Add(Lum(px[y * width + x])));
            values.Sort();
            return values[Mathf.Clamp(Mathf.FloorToInt(p * (values.Count - 1)), 0, values.Count - 1)];
        }

        static float MeanSaturation(Color32[] px)
        {
            double sum = 0;
            foreach (var p in px) { Color.RGBToHSV(p, out _, out float s, out _); sum += s; }
            return (float)(sum / px.Length);
        }

        static float MeanSaturation(Color32[] px, int width, RectInt r)
        {
            double sum = 0; int n = 0;
            ForEach(r, (x, y) => { Color.RGBToHSV(px[y * width + x], out _, out float s, out _); sum += s; n++; });
            return n > 0 ? (float)(sum / n) : 0f;
        }

        static string Slug(string s) => new string(s.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');

        // ---------- waiting for frames in Play mode ----------

        static Task Frames(int n)
        {
            var tcs = new TaskCompletionSource<bool>();
            int target = Time.frameCount + n;
            void Tick()
            {
                if (Time.frameCount < target && Application.isPlaying) return;
                EditorApplication.update -= Tick;
                tcs.TrySetResult(true);
            }
            EditorApplication.update += Tick;
            return tcs.Task;
        }

        static Task Seconds(float seconds)
        {
            var tcs = new TaskCompletionSource<bool>();
            float end = Time.realtimeSinceStartup + seconds;
            void Tick()
            {
                if (Time.realtimeSinceStartup < end && Application.isPlaying) return;
                EditorApplication.update -= Tick;
                tcs.TrySetResult(true);
            }
            EditorApplication.update += Tick;
            return tcs.Task;
        }

        // ---------- report ----------

        static void WriteReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Blend showcase audit");
            sb.AppendLine();
            sb.AppendLine($"Unity {Application.unityVersion}, {QualitySettings.activeColorSpace} color space, {SystemInfo.graphicsDeviceType}, {DateTime.Now:yyyy-MM-dd HH:mm}.");
            sb.AppendLine();
            sb.AppendLine("| # | Station | Modes | Approach | ON/OFF diff % | Check | Gamma on vs off | Frame ms | Notes |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var r in Results)
                sb.AppendLine($"| {r.index} | {r.title} | {r.modes} | {r.approach} | {r.diffPct:0.0} {(r.diffOk ? "" : "**FLAG**")} | {(r.checkOk ? "PASS" : "**FAIL**")}: {r.check} | {r.gammaChangePct:0.0}% px, mean {r.gammaMeanDiff:0.00}/255 | {r.frameMs:0.0} | {r.notes} |");
            sb.AppendLine();
            sb.AppendLine($"Console warnings/errors during the run: {ConsoleIssues.Count}");
            foreach (var c in ConsoleIssues.Distinct()) sb.AppendLine("- " + c);
            File.WriteAllText(Path.Combine(ReportDir, "report.md"), sb.ToString());
        }

        public static string Summary()
        {
            var sb = new StringBuilder();
            foreach (var r in Results)
                sb.AppendLine($"{r.index}|{r.title}|diff {r.diffPct:0.0}% {(r.diffOk ? "ok" : "FLAG")}|{(r.checkOk ? "PASS" : "FAIL")}|{r.check}|gamma {r.gammaChangePct:0.0}% ({r.gammaMeanDiff:0.00})|{r.frameMs:0.0} ms|{r.notes}");
            sb.AppendLine($"console issues: {ConsoleIssues.Count}");
            foreach (var c in ConsoleIssues.Distinct()) sb.AppendLine("  " + c);
            return sb.ToString();
        }
    }
}
