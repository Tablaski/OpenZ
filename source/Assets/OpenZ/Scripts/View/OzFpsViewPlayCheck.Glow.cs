#if !OZ_RELEASE
// OpenZ (GPL-3.0) - version 1.4 kit fps_glow: the glow Play check (menu OpenZ/V1.4/FPS View: Glow + Sun Check (short)), about 1.5 minutes on
// L02, the match goes on. Report and pictures: kits/fps_glow/proof/play (GlowFolder). Rows (none passes on 0 samples):
//  (G1) night on L02 (DayNight 0.66), a selected own light tank and 3 own light tanks with their headlights on 4 - 6 m beside it, the game
//       still: the view looks at each of them: the biggest white-hot blob of the picture (measured from its pixels) at most 2 % of the screen;
//       at least one mount sized; Stop NaN on in the view. (The billboard share VehicleLights.FpsMaxDrawn is capped at 1.5 % by the code itself
//       and the HUD with its stars is hidden in the view: both are logged only; the stars are G4's);
//  (G2) the leave: every light, unit renderer, canvas, lamp rig (glow, cone, pool, real light, candidate), every veteran / HUD star image (on,
//       size, scale), the camera (pose, lens, anti-aliasing), the view's and the sky's globals and the cursor the same before the entry and
//       after the leave (the check's own noise left out), the still RTS frame before = after by pixels (with Stop NaN now on: a picture
//       without a broken pixel is the same), the lights re-picked in the leave's frame and Stop NaN kept on after the leave (1.6 s later too);
//  (G4) the owner's repro, the game RUNNING, the camera's own anti-aliasing (SMAA on L02), night: twice in the selected light tank of G1:
//       the view on, the tank and a lit tank in front of it promoted in the view (VeteranMarks.OnPromoted: the pop, the gold sweep and light),
//       2 s of fire at an enemy dummy beyond the lamps, Esc; 5 RTS pictures 0.05 - 3 s after the leave: the biggest white-hot patch and the
//       biggest veteran star drawn. Pass A with the kit's fixes off (OzFpsGlow.Fixes false: P before the kit) says REPRODUCED or NOT
//       REPRODUCED (not a pass row); pass B with the kit on must be clean: the patch at most 2 % (or the RTS frame's own + 0.5 %), at least 1
//       veteran star drawn in every picture (the selected veteran's bar star) and none over 2 %. The star guard's hits and any non-finite
//       value found after the leave are named in the row;
//  (G3) by day on L02 (the key light 8 - 50 deg up): the view turned to the sun: the disc's brightness over the sky 4 disc radii round it and
//       a quarter screen beside it by at least SunMargin, its bright core's radius 0.6 - 2.5 x the drawn disc's (no white blob swallowing it),
//       the level's look in the globals (disc radius, wide glow share); after Esc a flyby (ForceOn) has the plain look again.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    public sealed partial class OzFpsViewPlayCheck
    {
        bool glowRun;
        ZObject glowU; List<ZObject> glowOthers; int glowX, glowY;                                       // G1's tanks, used again by G4
        /// <summary>The glow check's margin: the sun disc over the sky round it and beside it (display brightness 0..1).</summary>
        public static float SunMargin = 0.06f;
        /// <summary>The glow check's limit: one halo, star or white blob of the picture (share of the screen).</summary>
        public static float GlowLimit = 0.02f;

        /// <summary>fps_glow: the glow check (see the file's head).</summary>
        public static void RunGlow() { Report = "running"; var c = new GameObject("OzFpsViewPlayCheck").AddComponent<OzFpsViewPlayCheck>(); c.glowRun = true; }

        /// <summary>kits/fps_glow/proof/play (else the persistent data folder).</summary>
        public static string GlowFolder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var k = Path.Combine(d.FullName, "work", "staging", "v1_4", "kits", "fps_glow");
                if (Directory.Exists(k)) return Path.Combine(k, "proof", "play");
            }
            return Path.Combine(Application.persistentDataPath, "fps_glow_play_check");
        }

        void SavePng(Texture2D t, string name) { try { Directory.CreateDirectory(GlowFolder()); File.WriteAllBytes(Path.Combine(GlowFolder(), name), t.EncodeToPNG()); } catch (IOException) { } }

        IEnumerator GlowAll()
        {
            g = ZGame.I; W = g ? g.World : null; pi = FindFirstObjectByType<PlayerInput>(); rts = pi ? pi.cam : null; hud = OpenZHud.I; cam = rts ? rts.GetComponent<Camera>() : null;
            if (W == null || !rts || !pi || !cam) { Log("no level in Play"); fail++; Finish(); yield break; }
            Directory.CreateDirectory(GlowFolder());
            me = g.player; foe = me == Team.Red ? Team.Blue : Team.Red;
            yield return WaitFor(() => OpenZHud.ControlAt > 0f && OzMotion.Now >= OpenZHud.ControlAt + 1f, 40f);
            W.Bots.Clear();
            startIds = new HashSet<int>(W.UnitObjects.Where(o => o != null).Select(o => o.Id));
            var cd = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            Log($"GLOW CHECK level {g.level}, RTS height {rts.height:0.0}, field of view {cam.fieldOfView:0}, the camera's Stop NaN {(cd ? cd.stopNaN.ToString() : "-")}; the view's sun look here: sky x{FlybySky.FpsLookOf(g.level).sky:0.00}, glow x{FlybySky.FpsLookOf(g.level).glow:0.00}, wide x{FlybySky.FpsLookOf(g.level).wide:0.00}, disc {FlybySky.FpsLookOf(g.level).discDeg:0.00} deg; lamp limit {VehicleLights.FpsMaxShare * 100f:0.0} % a billboard");
            yield return WaitFor(() => Application.isFocused, 10f);
            Check($"the Unity window has the focus: {Application.isFocused}", Application.isFocused);
            Devices(true); OzFpsGlow.Fixes = true;
            if (OzFpsView.On && OzFpsView.I) OzFpsView.I.Leave("the check starts");
            var dn = FindAnyObjectByType<DayNight>(); float t0 = dn ? dn.timeOfDay : 0f; bool run0 = dn && dn.running;
            Check($"VehicleLights and DayNight are in the level ({(VehicleLights.I ? "lights" : "no lights")}, {(dn ? "clock" : "no clock")})", VehicleLights.I && dn);

            yield return NightGlowRow(dn);
            yield return OwnerReproRow();
            OzFpsGlow.Fixes = true;
            yield return SunLookRow(dn);

            if (dn) { dn.timeOfDay = t0; dn.running = run0; }
            Clean();
            if (errors > 0) Check($"no error or exception logged during the run ({errors}: {string.Join(" | ", errorLines)})", false);
            Finish();
        }

        // ------------------------------------------------------------------ G1 + G2
        /// <summary>fps_view_sky's state list + every lamp rig, every veteran / HUD star image and the camera's Stop NaN and anti-aliasing.</summary>
        Dictionary<string, string> GlowStates()
        {
            var d = StateList();
            if (VehicleLights.I) VehicleLights.I.FpsStates(d);
            foreach (var im in FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!im || (im.name != "VetStar" && im.name != "Star")) continue;
                var rt = im.rectTransform;
                d["star " + im.GetInstanceID() + " " + im.transform.parent?.name] = $"{im.enabled && im.gameObject.activeInHierarchy} {rt.sizeDelta:F1} {rt.localScale:F2} {rt.anchoredPosition:F0}";
            }
            var cd = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            d["camera data"] = cd ? $"aa {cd.antialiasing} post {cd.renderPostProcessing}" : "-";        // Stop NaN stays on after the first entry (OzFpsGlow): its own rows
            return d;
        }

        /// <summary>The biggest connected patch of white-hot pixels (all channels over 0.92) as a share of the screen, on an 8 px grid.</summary>
        static float BiggestBlob(Texture2D t, out int hotCells)
        {
            hotCells = 0; if (!t) return -1f;
            int cw = t.width / 8, ch = t.height / 8; var px = t.GetPixels32(); var hot = new bool[cw * ch];
            for (int y = 0; y < ch; y++) for (int x = 0; x < cw; x++)
                {
                    int n = 0, h = 0;
                    for (int j = 1; j < 8; j += 3) for (int i = 1; i < 8; i += 3) { var c = px[(y * 8 + j) * t.width + x * 8 + i]; n++; if (c.r > 234 && c.g > 234 && c.b > 225) h++; }
                    if (h * 2 > n) { hot[y * cw + x] = true; hotCells++; }
                }
            var seen = new bool[hot.Length]; var st = new Stack<int>(); int best = 0;
            for (int s = 0; s < hot.Length; s++)
            {
                if (!hot[s] || seen[s]) continue;
                int size = 0; st.Push(s); seen[s] = true;
                while (st.Count > 0)
                {
                    int k = st.Pop(); size++; int x = k % cw, y = k / cw;
                    void Go(int q) { if (!seen[q] && hot[q]) { seen[q] = true; st.Push(q); } }
                    if (x > 0) Go(k - 1); if (x < cw - 1) Go(k + 1); if (y > 0) Go(k - cw); if (y < ch - 1) Go(k + cw);
                }
                best = Mathf.Max(best, size);
            }
            return best / (float)Mathf.Max(1, hot.Length);
        }

        /// <summary>Veteran / HUD star images drawn now (their canvas on) and the biggest one's share of the screen.</summary>
        static int StarsShown(out float biggest)
        {
            biggest = 0f; int n = 0; var corners = new Vector3[4];
            foreach (var im in FindObjectsByType<Image>(FindObjectsSortMode.None))
            {
                if (!im || (im.name != "VetStar" && im.name != "Star") || !im.enabled || !im.gameObject.activeInHierarchy || !im.canvas || !im.canvas.enabled) continue;
                im.rectTransform.GetWorldCorners(corners);
                float w = Mathf.Abs(corners[2].x - corners[0].x), h = Mathf.Abs(corners[2].y - corners[0].y);
                if (im.canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                n++; biggest = Mathf.Max(biggest, w * h / Mathf.Max(1f, Screen.width * Screen.height));
            }
            return n;
        }

        IEnumerator NightGlowRow(DayNight dn)
        {
            if (!FindSpot(out int x, out int y, true)) { Check($"glow: a free place for 4 light tanks ({LastSpotWhy})", false); yield break; }
            if (dn) { dn.running = false; dn.timeOfDay = 0.66f; }
            var u = Make("vehicle", "light", x, y, me);
            var others = new List<ZObject>();
            foreach (var (dx, dy) in new[] { (40, 0), (-32, 24), (0, -40) }) { var o = Make("vehicle", "light", x + dx, y + dy, me); if (o != null) others.Add(o); }
            glowU = u; glowOthers = others; glowX = x; glowY = y;
            yield return Real(0.3f);
            var v = ViewOf(u); if (u == null || !v || others.Count == 0) { Check($"glow: the viewed light tank and the others made ({others.Count} of 3)", false); yield break; }
            Select(u);
            rts.focus = ZGame.PxToWorld(x, y); yield return Real(2.2f);                                      // headlights on at night (0.35 s), the selection up
            {   // U2 check fix 2 (check only): the lamp pick runs every 0.2 s of game time and the row stills the game next; U2 run 3 stilled it right
                // after a pick that came while the new tanks' headlights were still under 5 % (Candidate / Effects False), so the state before the
                // entry was stale and the leave's re-pick showed as a difference (and their cones as 0.100 % of the frame). Wait (up to 3 s) for a pick
                // that has every one of the 4 tanks as a candidate with its effects before the game is stilled.
                var ids = new List<int> { u.Id }; foreach (var o in others) ids.Add(o.Id);
                yield return WaitFor(() => { var st = GlowStates(); return ids.All(id => st.TryGetValue("vl " + id, out var s) && s.Split(' ') is var t && t.Length >= 3 && t[t.Length - 3] == "True" && t[t.Length - 2] == "True"); }, 3f);
                yield return Real(0.3f);
            }
            var cd = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var aa0 = cd ? cd.antialiasing : UnityEngine.Rendering.Universal.AntialiasingMode.None; bool nan0 = cd && cd.stopNaN;
            bool paused0 = g.paused; float ts0 = Time.timeScale;
            g.paused = true; Time.timeScale = 0f; if (cd) cd.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;   // as fps_view_sky's leave row: a still picture
            yield return Frames(8);
            Vector3 focus0 = rts.focus;
            yield return new WaitForEndOfFrame(); var a = ScreenCapture.CaptureScreenshotAsTexture();
            yield return Frames(3); yield return new WaitForEndOfFrame(); var a2 = ScreenCapture.CaptureScreenshotAsTexture();
            var s0 = GlowStates();
            yield return Real(0.5f); var s0b = GlowStates();
            var own = new HashSet<string>(s0.Where(kv => !s0b.TryGetValue(kv.Key, out var w0) || w0 != kv.Value).Select(kv => kv.Key));
            int leaves0 = VehicleLights.FpsLeaves;
            yield return Enter("glow light tank (night, 3 lit tanks beside it)", u, Snap(v));
            if (OzFpsView.On && OzFpsView.I)
            {
                var fv = OzFpsView.I;
                Check($"glow: Stop NaN on in the view (the camera's own {nan0}; held {OzFpsGlow.NanHeld})", cd && cd.stopNaN && OzFpsGlow.NanHeld);
                int samples = 0, seen = 0, cores = 0, held = 0, beams = 0, bad = 0; float worstBlob = 0f, worstDrawn = 0f, worstRaw = 0f, worstStar = 0f; int stars = 0;
                int i = 0;
                foreach (var o in others)
                {
                    var ov = ViewOf(o); if (!ov) continue;
                    foreach (float h in new[] { 1.2f, 0.4f })
                    {
                        yield return Aim(fv, ov.transform.position + Vector3.up * h, 0f); yield return Frames(3);
                        yield return new WaitForEndOfFrame();
                        int fs = VehicleLights.FpsFrame == Time.frameCount ? VehicleLights.FpsSeen : 0;
                        float drawn = VehicleLights.FpsMaxDrawn, raw = VehicleLights.FpsMaxRaw;
                        seen += fs; cores += VehicleLights.FpsCores; held += VehicleLights.FpsHeld; beams += VehicleLights.FpsBeamsHeld;
                        var tex = ScreenCapture.CaptureScreenshotAsTexture();
                        float blob = BiggestBlob(tex, out int hotCells);
                        int sn = StarsShown(out float sb); stars += sn; worstStar = Mathf.Max(worstStar, sb);
                        if (i < 6) SavePng(tex, $"fps_glow_night_{i}.png");
                        Destroy(tex);
                        float dist = Vector3.Distance(cam.transform.position, ov.transform.position);
                        Log($"INFO glow sample {i}: tank #{o.Id} at {dist:0.0} m, aim +{h:0.0} m: biggest white-hot blob {blob * 100f:0.00} % ({hotCells} hot cells), mounts sized {fs} (whole {VehicleLights.FpsFull}, cores only {VehicleLights.FpsCores}, not drawn {VehicleLights.FpsHeld}), cones held {VehicleLights.FpsBeamsHeld}, biggest billboard drawn {drawn * 100f:0.00} % (before the limit {raw * 100f:0.0} %), stars drawn {sn} (biggest {sb * 100f:0.00} %)");
                        samples++; worstBlob = Mathf.Max(worstBlob, blob); worstDrawn = Mathf.Max(worstDrawn, drawn); worstRaw = Mathf.Max(worstRaw, raw);
                        if (blob > GlowLimit) bad++;                                                         // from the pixels (the drawn share is capped by the code: logged only)
                        i++;
                    }
                }
                Log($"INFO glow (night, in the view): the biggest lamp billboard drawn {worstDrawn * 100f:0.00} % by the shader's sizing (capped at {VehicleLights.FpsMaxShare * 100f:0.0} % by the code; unlimited it would be {worstRaw * 100f:0.0} %), veteran / HUD stars drawn {stars} (the HUD is hidden in the view; biggest {worstStar * 100f:0.00} %; the stars are G4's)");
                Check($"glow (night, in the view): {samples} pictures toward the 3 lit tanks: the biggest white-hot blob from the pixels {worstBlob * 100f:0.00} % (at most {GlowLimit * 100f:0} % of the screen); {seen} mounts sized ({cores} cores only, {held} not drawn), {beams} cones held; over the limit in {bad} pictures",
                      samples >= 3 && seen > 0 && bad == 0);
                yield return Tap(Key.Escape); yield return Frames(2);
                Check($"glow: the leave re-picked the lights in its own frame (leave frame {OzFpsView.LeftFrame}, re-pick frame {VehicleLights.FpsLeaveFrame}, re-picks {VehicleLights.FpsLeaves - leaves0})",
                      !OzFpsView.On && VehicleLights.FpsLeaves - leaves0 == 1 && VehicleLights.FpsLeaveFrame == OzFpsView.LeftFrame);
                Check($"glow: Stop NaN on right after the leave (held {OzFpsGlow.NanHeld}, the camera {(cd ? cd.stopNaN : false)}; its own before the first entry {OzFpsGlow.NanWas})", OzFpsGlow.NanHeld && cd && cd.stopNaN);
                yield return Real(1.6f);
                Check($"glow: Stop NaN still on 1.6 s after the leave (kept for the rest of the level: the camera {(cd ? cd.stopNaN : false)}, held {OzFpsGlow.NanHeld})", OzFpsGlow.NanHeld && cd && cd.stopNaN);
            }
            rts.focus = focus0; yield return Frames(4);
            var s1 = GlowStates();
            var diff = new List<string>();
            var repicked = new List<string>();
            foreach (var kv in s0) { if (!s1.TryGetValue(kv.Key, out var w)) diff.Add(kv.Key + ": gone"); else if (w != kv.Value && !own.Contains(kv.Key)) { if (OnlyCandidate(kv.Key, kv.Value, w)) repicked.Add(kv.Key); else diff.Add($"{kv.Key}: {kv.Value} -> {w}"); } }
            if (repicked.Count > 0) Log($"INFO glow leave: {repicked.Count} lamp rig(s) differ only in Candidate (the nearest-N pick, every 0.2 s of game time: stale while the game is still; the leave's re-pick made it fresh; their lights, glows, cones and pools the same): {string.Join(", ", repicked)}");
            foreach (var kv in s1) if (!s0.ContainsKey(kv.Key)) diff.Add(kv.Key + ": new " + kv.Value);
            if (own.Count > 0) Log($"INFO glow leave: {own.Count} state(s) change on their own with the game still (left out): {string.Join("; ", own.Take(6))}");
            int rigs = s0.Keys.Count(k => k.StartsWith("vl ")), starN = s0.Keys.Count(k => k.StartsWith("star "));
            Check($"glow leave: {s0.Count} states ({rigs} lamp rigs, {starN} star images, every light, unit renderer, canvas, the camera and its Stop NaN / anti-aliasing, the globals, the cursor) the same before the entry and after the leave; differ: {diff.Count}{(diff.Count > 0 ? " (" + string.Join("; ", diff.Take(8)) + ")" : "")}",
                  s0.Count > 10 && rigs >= 4 && diff.Count == 0 && !OzFpsView.On);
            yield return new WaitForEndOfFrame(); var bTex = ScreenCapture.CaptureScreenshotAsTexture();
            float noise = DifferShift(a, a2), ab = DifferShift(a, bTex);
            SavePng(a, "fps_glow_before.png"); SavePng(bTex, "fps_glow_after.png");
            Check($"glow leave: the still RTS frame (L02 night, 4 lit light tanks, one selected, the game still) before the entry and after the leave differ on {ab * 100f:0.000} % of the screen (its own noise {noise * 100f:0.000} %; at most noise + 0.1 %)", a && bTex && ab >= 0f && ab <= noise + 0.001f);
            Destroy(a); Destroy(a2); Destroy(bTex);
            if (cd) cd.antialiasing = aa0; Time.timeScale = ts0; g.paused = paused0;
            yield return Real(0.5f);
        }

        /// <summary>U2 check fix (check only): a lamp rig whose state differs only in its Candidate field (VehicleLights.FpsGlow.cs GlowStates:
        /// GlowOn BeamOn PoolOn [Spot on + intensity] Candidate Effects EffK). Run 1 in U2 (L02): the 4 lit tanks went Candidate False -> True
        /// across the round trip: the pick runs every 0.2 s of game time and the row stills the game, so the state before the entry was the
        /// stale one; the leave's re-pick (the kit's) refreshed it. Every drawn part the same; the still frame the same (0.001 %).</summary>
        static bool OnlyCandidate(string key, string a, string b)
        {
            if (!key.StartsWith("vl ")) return false;
            var x = a.Split(' '); var y = b.Split(' ');
            if (x.Length != y.Length || x.Length < 3) return false;
            int c = x.Length - 3;
            for (int i = 0; i < x.Length; i++) if (i != c && x[i] != y[i]) return false;
            return x[c] != y[c];
        }

        // ------------------------------------------------------------------ G4: the owner's repro (see the file's head)
        sealed class Repro { public bool entered; public int samples, starsMin = int.MaxValue, starHits; public float blob, star; public string nonFinite = "none", starLast = "-"; }

        IEnumerator OwnerReproRow()
        {
            var u = glowU; var v = ViewOf(u);
            if (u == null || u.Destroyed || !v || glowOthers == null || glowOthers.Count < 2) { Check("owner repro: G1's light tanks are there", false); yield break; }
            var cd = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var vm = FindFirstObjectByType<VeteranMarks>();
            var onProm = typeof(VeteranMarks).GetMethod("OnPromoted", BindingFlags.NonPublic | BindingFlags.Instance);
            if (!vm || onProm == null) { Check("owner repro: VeteranMarks and its OnPromoted are there", false); yield break; }
            var e = Make("robot", "grunt", glowX + 120, glowY, foe, 1);                                   // 15 m east: beyond the lit tank 5 m east
            if (e != null) { e.Damage = 0; e.MaxHealth = 10000000; e.Health = 10000000; }
            yield return Real(1.2f);
            var ev = ViewOf(e); if (e == null || !ev) { Check("owner repro: an enemy dummy made beyond the lamps", false); yield break; }
            rts.focus = ZGame.PxToWorld(glowX, glowY); Select(u); yield return Real(1f);
            yield return new WaitForEndOfFrame(); var t0 = ScreenCapture.CaptureScreenshotAsTexture(); float baseBlob = BiggestBlob(t0, out _); Destroy(t0);
            float blobLimit = Mathf.Max(GlowLimit, baseBlob + 0.005f);
            Log($"INFO owner repro: L02 at night, the game running (paused {g.paused}, time scale {Time.timeScale:0.00}), the camera's own anti-aliasing {(cd ? cd.antialiasing.ToString() : "-")}, Stop NaN {(cd && cd.stopNaN)}; the RTS frame's own biggest white-hot patch {baseBlob * 100f:0.00} % (the limit {blobLimit * 100f:0.00} %); veteran deviation {ZWorld.VeteranDeviation}");
            var a = new Repro(); yield return ReproPass(false, u, v, glowOthers[0], ev, vm, onProm, a);
            OzFpsGlow.Fixes = true;
            yield return Real(3.5f);                                                                        // pass A's pop (2.6 s) and gold sweep gone
            var b = new Repro(); yield return ReproPass(true, u, v, glowOthers[1], ev, vm, onProm, b);
            OzFpsGlow.Fixes = true;
            bool rep = a.entered && (a.blob > blobLimit || a.star > GlowLimit);
            Log($"INFO owner repro pass A (the kit's fixes off = P before the kit): {(rep ? "REPRODUCED" : "NOT REPRODUCED")}: {a.samples} pictures, the biggest white-hot patch {a.blob * 100f:0.00} %, the biggest veteran star {a.star * 100f:0.000} % (stars drawn at least {(a.starsMin == int.MaxValue ? 0 : a.starsMin)}); non-finite after the leave: {a.nonFinite}");
            if (!rep) Log("INFO owner repro: the stuck light / star did NOT show with the kit's fixes off in this run: item 1's fix is NOT proven by this run (the owner's frame stays unexplained until a repro shows it)");
            Check($"owner repro pass B (the kit on): {b.samples} RTS pictures 0.05 - 3 s after the leave (the game running, the camera's own anti-aliasing): the biggest white-hot patch {b.blob * 100f:0.00} % (at most {blobLimit * 100f:0.00} %), veteran stars drawn in every picture at least {(b.starsMin == int.MaxValue ? 0 : b.starsMin)} (at least 1), the biggest {b.star * 100f:0.000} % (at most {GlowLimit * 100f:0} %); oversized stars held {b.starHits} ({b.starLast}); non-finite after the leave: {b.nonFinite}",
                  b.entered && b.samples >= 5 && b.blob <= blobLimit && b.starsMin != int.MaxValue && b.starsMin >= 1 && b.star <= GlowLimit);
            Kill(W, e); W.SendStop(me, u); yield return Real(0.3f);
        }

        IEnumerator ReproPass(bool fixes, ZObject u, UnitView v, ZObject other, UnitView ev, VeteranMarks vm, MethodInfo onProm, Repro res)
        {
            string tag = fixes ? "B" : "A";
            OzFpsGlow.Fixes = fixes; if (!fixes) OzFpsGlow.GiveBack();                                   // A: Stop NaN back to the camera's own value, no star guard, no lamp sizing, no re-pick
            int hits0 = OzFpsGlow.StarHits, nf0 = OzFpsGlow.NonFinite.Count;
            W.SendStop(me, u); Select(u); yield return Real(0.3f);
            yield return Enter($"owner repro {tag}", u, Snap(v));
            var fv = OzFpsView.I; if (!OzFpsView.On || !fv) yield break;
            res.entered = true;
            var ov = ViewOf(other);
            if (ov) { yield return Aim(fv, ov.transform.position + Vector3.up * 0.8f, 0f); yield return Real(0.3f); }
            foreach (var o in new[] { u, other })
            {   // promoted in the view, as a crew that scores its fifth kill while the owner looks through it
                if (o == null || o.Destroyed) continue;
                o.Veteran = true; o.PromotedAt = W.Time; onProm.Invoke(vm, new object[] { o });
            }
            yield return Real(0.4f);
            yield return Aim(fv, ev.transform.position + Vector3.up * 1.1f, 0f); yield return Real(0.2f);
            yield return LeftButton(true); yield return Real(2f); yield return LeftButton(false);
            if (ov) yield return Aim(fv, ov.transform.position + Vector3.up * 0.8f, 0f);                  // the lamps in the last picture of the view
            yield return Frames(2);
            yield return Leave1();
            float tl = Time.unscaledTime; int k = 0;
            foreach (float at in new[] { 0.05f, 0.3f, 1f, 2f, 3f })
            {
                while (Time.unscaledTime - tl < at) yield return null;
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                float blob = BiggestBlob(tex, out int hot); int sn = StarsShown(out float sb);
                SavePng(tex, $"fps_glow_repro_{tag}_{k}.png"); Destroy(tex);
                Log($"INFO owner repro {tag} {Time.unscaledTime - tl:0.00} s after the leave: the biggest white-hot patch {blob * 100f:0.00} % ({hot} hot cells), veteran stars drawn {sn} (the biggest {sb * 100f:0.000} %)");
                res.samples++; res.blob = Mathf.Max(res.blob, blob); res.star = Mathf.Max(res.star, sb); res.starsMin = Mathf.Min(res.starsMin, sn); k++;
            }
            res.starHits = OzFpsGlow.StarHits - hits0; res.starLast = res.starHits > 0 ? OzFpsGlow.StarLast : "-";
            res.nonFinite = OzFpsGlow.NonFinite.Count > nf0 ? string.Join(" | ", OzFpsGlow.NonFinite.Skip(nf0).Take(6)) : "none";
            W.SendStop(me, u);
        }

        // ------------------------------------------------------------------ G3
        IEnumerator SunLookRow(DayNight dn)
        {
            var sky = FlybySky.I; if (!sky || !dn) { Check("sun: FlybySky and DayNight are there", false); yield break; }
            if (!FindSpot(out int x, out int y)) { Check($"sun: a free place ({LastSpotWhy})", false); yield break; }
            rts.focus = ZGame.PxToWorld(x, y); yield return Real(0.4f);
            var u = Make("robot", "grunt", x, y, me); yield return Real(1.2f);
            var v = ViewOf(u); if (u == null || !v) { Check("sun: a grunt made", false); yield break; }
            float day = -1f; dn.running = false;
            foreach (float tt in new[] { 0.25f, 0.2f, 0.3f, 0.15f, 0.33f, 0.1f, 0.05f })
            {
                dn.timeOfDay = tt; yield return Real(0.6f);
                var sn = RenderSettings.sun ? RenderSettings.sun : dn.sun; if (!sn) break;
                float el = Mathf.Asin(Mathf.Clamp(-sn.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
                if (el >= 8f && el <= 50f) { day = tt; break; }
            }
            if (day < 0f) { Check("sun: a time with the sun 8 - 50 deg up on DayNight's clock", false); yield break; }
            int g0 = FlybySky.FpsSkyGained;
            yield return Enter("sun grunt", u, Snap(v));
            var fv = OzFpsView.I; if (!OzFpsView.On || !fv) yield break;
            yield return WaitFor(() => sky.Weight >= 0.999f, 4f);
            var sun = RenderSettings.sun ? RenderSettings.sun : dn.sun;
            Vector3 sd = -sun.transform.forward; float elev = Mathf.Asin(Mathf.Clamp(sd.y, -1f, 1f)) * Mathf.Rad2Deg;
            yield return Aim(fv, cam.transform.position + sd * 300f, 0f); yield return Real(0.5f);
            var vp = cam.WorldToViewportPoint(cam.transform.position + sd * 300f);
            var look = FlybySky.FpsLookOf(g.level);
            var shape = Shader.GetGlobalVector("_FlybySunShape"); var glow = Shader.GetGlobalVector("_FlybySunGlow");
            bool lookOn = Mathf.Abs(shape.z - Mathf.Cos(look.discDeg * Mathf.Deg2Rad)) < 1e-5f && Mathf.Abs(glow.w - FlybySky.GlowWide * look.wide) < 1e-4f;
            yield return new WaitForEndOfFrame(); var tex = ScreenCapture.CaptureScreenshotAsTexture();
            float disc = Lum(tex, vp), beside = Lum(tex, new Vector3(vp.x + 0.25f, vp.y, 1f));
            float pxPerRad = tex.height * 0.5f / Mathf.Tan(0.5f * cam.fieldOfView * Mathf.Deg2Rad), discPx = Mathf.Tan(look.discDeg * Mathf.Deg2Rad) * pxPerRad;
            float cx = vp.x * tex.width, cy = vp.y * tex.height;
            float ring = 0f; int rn = 0;
            for (int k = 0; k < 16; k++)
            {   // 4 disc radii round it, off the crosshair's arms
                float ang = (k + 0.5f) * Mathf.PI / 8f; float l = Lum(tex, new Vector3((cx + Mathf.Cos(ang) * discPx * 4f) / tex.width, (cy + Mathf.Sin(ang) * discPx * 4f) / tex.height, 1f));
                if (l >= 0f) { ring += l; rn++; }
            }
            ring = rn > 0 ? ring / rn : -1f;
            float half = (disc + ring) * 0.5f, rad = 0f; int rays = 0;
            for (int k = 0; k < 4; k++)
            {   // the bright core's radius along the 4 diagonals (the crosshair's arms are straight)
                float ang = Mathf.PI * 0.25f + k * Mathf.PI * 0.5f;
                for (float r = 1f; r < discPx * 8f; r += 1f)
                {
                    int px = Mathf.RoundToInt(cx + Mathf.Cos(ang) * r), py = Mathf.RoundToInt(cy + Mathf.Sin(ang) * r);
                    if (px < 0 || py < 0 || px >= tex.width || py >= tex.height) break;
                    var c = tex.GetPixel(px, py); float l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                    if (l < half) { rad += r; rays++; break; }
                }
            }
            rad = rays > 0 ? rad / rays : -1f;
            SavePng(tex, "fps_glow_sun_day.png"); Destroy(tex);
            Check($"sun by day ({g.level}, DayNight {day:0.00}, the key light {elev:0.0} deg up): the view's look in the globals (disc {look.discDeg:0.00} deg, wide glow {glow.w:0.000}, the dome gain applied {FlybySky.FpsSkyGained - g0} frames) {lookOn}",
                  lookOn && (Mathf.Abs(look.sky - 1f) < 1e-4f || FlybySky.FpsSkyGained - g0 > 5));
            Check($"sun by day: the disc {disc:0.00} against the sky 4 disc radii round it {ring:0.00} ({rn} samples) and a quarter screen beside it {beside:0.00}: at least {SunMargin:0.00} brighter than both; its bright core {rad:0.0} px against the drawn disc's {discPx:0.0} px ({rays} rays; 0.6 - 2.5 x)",
                  vp.z > 0f && rn >= 8 && rays >= 3 && disc >= ring + SunMargin && disc >= beside + SunMargin && rad >= discPx * 0.6f && rad <= discPx * 2.5f);
            yield return Leave1(); yield return Real(0.4f);
            // a flyby after the round trip: the plain look (FpsMode off)
            FlybySky.ForceOn = true;
            yield return WaitFor(() => sky.Weight >= 0.999f, 6f);
            shape = Shader.GetGlobalVector("_FlybySunShape"); glow = Shader.GetGlobalVector("_FlybySunGlow");
            bool plain = !sky.FpsMode && Mathf.Abs(shape.z - Mathf.Cos(FlybySky.DiscRadiusDeg * Mathf.Deg2Rad)) < 1e-5f && Mathf.Abs(glow.w - FlybySky.GlowWide) < 1e-4f;
            Check($"sun: a flyby after the round trip (ForceOn) draws the flybys' own look (first-person sky {sky.FpsMode}, disc {FlybySky.DiscRadiusDeg:0.00} deg, wide glow {glow.w:0.000} = {FlybySky.GlowWide:0.000}, Weight {sky.Weight:0.00})", plain && sky.Weight >= 0.999f);
            FlybySky.ForceOn = false;
            yield return WaitFor(() => sky.Weight <= 0.001f, 3f);
        }
    }
}
#endif
