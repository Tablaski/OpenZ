#if !OPENZ_HARNESS
// OpenZ (GPL-3.0) - version 1.3 rows 45, 46: the Play check and the frames of the soft shock ring drawn on top
// (View/FxV3/OpenZFortShockwave.Overlay.cs, Shaders/OzShockOverlay.shader). Editor only, never in a build.
using System.Collections;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Menu OpenZ/V1.3/Shockwave. Play, a level scene, the game camera. Each run fires the levels' nuke LOOK at a site, as the strike's impact
    /// does (NukeFx.Trigger + OpenZBloomDirector.Flash; view only: no unit, building or rule is touched, so it can be run again), frames it like
    /// the game's own nuke (the RtsCamera focus 9 m north of the blast, 78 m up) or wide (140 m), runs the game clock at exactly 30 frames a
    /// second while it records (Time.captureFramerate, put back after) and saves a frame every 0.1 s from the blast to +2.5 s, or every frame
    /// for 3 s (the clip), with the HUD as the player sees it.
    /// Counted rows: S1 the wave drew as the overlay (BEFORE: as the old ring); S2 its radius is RadiusAt at every frame (the timing is the
    /// old one); S3 it lies flat at the blast's floor + OverlayLift at every frame; S4 the old ring's two renderers are off; S5 the boom played.
    /// Sites: the camera focus as it is, or "hills": the point within 100 m of the focus, on flat ground, whose ring crosses the most raised
    /// ground (Level 16's hills, Level 7's volcano slopes). Frames and the report go to the milestones folder, v1_3/shockwave.
    /// <c></c>
    /// <c>OpenZ.EditorTools.OzShockOverlayCheck.Last</c> until it starts with PASS or FAIL. Every setting it changes is put back.
    /// </summary>
    public static class OzShockOverlayCheck
    {
        const string Tag = "[Shockwave]", Root = "OpenZ/V1.3/Shockwave/";
        static string Dir => OzRelease.Milestones("v1_3/shockwave");
        /// <summary>The last report (or what it is doing now).</summary>
        public static string Last { get; private set; } = "not run";
        public static bool Running { get; private set; }

        sealed class Runner : MonoBehaviour { }
        static Runner runner;
        static Runner R()
        {
            if (!runner) { var go = new GameObject("OzShockOverlayCheckRunner") { hideFlags = HideFlags.HideAndDontSave }; runner = go.AddComponent<Runner>(); }
            return runner;
        }

        static bool ring0 = true; static int cap0;

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.ExitingPlayMode || !Running) return;
                Time.captureFramerate = cap0; OpenZFortShockwave.OverlayRing = ring0; Running = false;
            };
        }

        [MenuItem(Root + "Frames at the camera focus (Play)")] static void M1() => Run(false, false, false, false);
        [MenuItem(Root + "Frames at the camera focus, wide 140 m (Play)")] static void M2() => Run(false, false, true, false);
        [MenuItem(Root + "Frames on the hills near the focus (Play)")] static void M3() => Run(true, false, false, false);
        [MenuItem(Root + "Frames on the hills near the focus, wide 140 m (Play)")] static void M4() => Run(true, false, true, false);
        [MenuItem(Root + "BEFORE - the old ring, at the camera focus (Play)")] static void M5() => Run(false, true, false, false);
        [MenuItem(Root + "BEFORE - the old ring, on the hills, wide 140 m (Play)")] static void M6() => Run(true, true, true, false);
        [MenuItem(Root + "Clip - every frame for 3 s, on the hills, wide 140 m (Play)")] static void M7() => Run(true, false, true, true);
        [MenuItem(Root + "Clip - every frame for 3 s, at the camera focus (Play)")] static void M8() => Run(false, false, false, true);

        /// <summary>Starts one run (see the class notes). Returns at once; the report lands in Last.</summary>
        public static string Run(bool hills, bool before, bool wide, bool clip)
        {
            if (!Application.isPlaying || !ZGame.I) { Debug.LogWarning(Tag + " enter Play first (a level scene)"); return Last = Tag + " enter Play first (a level scene)"; }
            if (Running) return Last = Tag + " already running";
            Running = true; Last = Tag + " running";
            R().StartCoroutine(Go(hills, before, wide, clip));
            return Last;
        }

        static IEnumerator Go(bool hills, bool before, bool wide, bool clip)
        {
            var g = ZGame.I; var rts = UnityEngine.Object.FindFirstObjectByType<RtsCamera>();
            var sb = new StringBuilder(); int ok = 0, bad = 0;
            void Row(bool pass, string what) { if (pass) ok++; else bad++; sb.Append(pass ? "\n  [ok]   " : "\n  [FAIL] ").Append(what); }
            void Info(string what) => sb.Append("\n  [info] ").Append(what);
            ring0 = OpenZFortShockwave.OverlayRing; cap0 = Time.captureFramerate;
            float h0 = rts ? rts.height : 0f; Vector3 f0 = rts ? rts.focus : Vector3.zero;
            string look = before ? "before" : "after", site = hills ? "hills" : "focus", zoom = wide ? "140m" : "78m";
            string sub = Path.Combine(Dir, $"{g.level}_{site}_{look}_{zoom}{(clip ? "_clip" : "")}");
            Directory.CreateDirectory(sub);

            var at = rts ? rts.focus - new Vector3(0f, 0f, 9f) : Vector3.zero;
            if (hills) at = HillSite(at, Info);
            if (!OzBlastGround.Y(at.x, at.z, out float gy)) gy = at.y;
            at.y = gy;
            Info($"level {g.level}, blast at ({at.x:0.0}, {at.y:0.0}, {at.z:0.0}), camera {(wide ? 140 : 78)} m, look {(before ? "BEFORE (the old ring of item 215)" : "the overlay")}, {(clip ? "every frame for 3 s" : "a frame every 0.1 s to +2.5 s")}, 30 frames a second");
            while (Time.time - OpenZFortShockwave.LastFireTime < 3.5f) yield return null;              // Fire() merges a second blast within 3 s
            if (rts) { rts.focus = at + new Vector3(0f, 0f, 9f); rts.SetHeightImmediate(wide ? 140f : 78f); }
            OpenZFortShockwave.OverlayRing = !before;
            for (int i = 0; i < 20; i++) yield return null;

            Time.captureFramerate = 30;
            int waves0 = OpenZFortShockwave.OverlayWaves; float fire0 = OpenZFortShockwave.LastFireTime;
            float tTrig = Time.time;                                                                      // NukeFx.Sequence runs to its first yield in Trigger: the wave's born time
            NukeFx.Trigger(at); OpenZBloomDirector.Flash(1f);
            int guard = 0;
            while (OpenZFortShockwave.LastFireTime <= fire0 + 1e-4f && guard++ < 60) yield return null;
            bool fired = OpenZFortShockwave.LastFireTime > fire0 + 1e-4f;
            Row(fired, "S0 the levels' nuke look fired the shockwave (OpenZFortShockwave.Fire through NukeFx)");
            float worstR = 0f, worstY = 0f; bool drew = false, oldOff = true, logged = false; int mark = -1, frame = 0, probes = 0;
            float endAt = clip ? 3f : 2.5f;
            var lines = new StringBuilder();
            while (fired)
            {
                yield return null;
                bool has = OpenZFortShockwave.OverlayProbe(out float age, out float r, out float y, out float floor, out bool off);
                float waveAge = has ? age : Time.time - tTrig;                                        // the same clock for the old ring
                if (mark < 0) mark = Mathf.CeilToInt((waveAge - 1e-3f) * 10f);                       // the first 0.1 s mark the wave has not passed yet
                if (has && r < OpenZFortShockwave.MaxRadius - 1f)
                {
                    drew = true; probes++;
                    worstR = Mathf.Max(worstR, Mathf.Abs(r - Mathf.Max(OpenZFortShockwave.RadiusAt(age), 0.5f)));
                    worstY = Mathf.Max(worstY, Mathf.Abs(y - (floor + OpenZFortShockwave.OverlayLift)));
                    oldOff &= off;
                }
                if (clip) { ScreenCapture.CaptureScreenshot(Path.Combine(sub, $"f{frame:000}.png")); frame++; }
                else if (waveAge >= mark * 0.1f - 1e-3f)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(sub, $"t{Mathf.RoundToInt(mark * 100f):0000}ms.png"));
                    lines.Append($"\n    t{Mathf.RoundToInt(mark * 100f):0000}ms.png: wave age {waveAge:0.000} s, radius {(has ? r : OpenZFortShockwave.RadiusAt(waveAge)):0.0} m");
                    mark++;
                }
                if (!logged && waveAge >= 0.5f - 1e-3f) { logged = true; Info("+0.5 s " + OpenZFortShockwave.OverlayStatus()); Info("+0.5 s " + OpenZFortShockwave.SurfaceStatus()); }
                if (waveAge >= endAt) break;
            }
            // the boom arrives with the front at the camera (at most 5 s): run on, then read it
            float until = Time.time + 3f;
            while (fired && Time.time < until && !OpenZFortShockwave.ProbeBoomed) yield return null;
            Time.captureFramerate = cap0;

            if (fired)
            {
                if (before) Row(!drew && OpenZFortShockwave.OverlayWaves == waves0, "S1 BEFORE: the wave drew the old ring (no overlay)");
                else
                {
                    Row(drew && OpenZFortShockwave.OverlayWaves == waves0 + 1, $"S1 the wave drew as the overlay ({probes} frames)");
                    Row(drew && worstR < 0.01f, $"S2 its radius is RadiusAt at every frame (the old clock): worst {worstR:0.0000} m");
                    Row(drew && worstY < 0.001f, $"S3 flat at the blast's floor + {OpenZFortShockwave.OverlayLift:0.##} m at every frame: worst {worstY:0.0000} m");
                    Row(drew && oldOff, "S4 the old ring's two renderers are off");
                }
                bool boomed = OpenZFortShockwave.ProbeBoomed; string st = OpenZFortShockwave.Status();
                Row(boomed, "S5 the boom played: " + st);
            }
            Info(clip ? $"{frame} frames: {sub} (ffmpeg -framerate 30 -i f%03d.png)" : $"frames every 0.1 s: {sub}{lines}");
            OpenZFortShockwave.OverlayRing = ring0;
            if (rts) { rts.focus = f0; rts.SetHeightImmediate(h0); }
            Last = $"{(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad} {Tag} {g.level} {site} {look} {zoom}{(clip ? " clip" : "")}{sb}";
            if (bad == 0) Debug.Log(Last); else Debug.LogError(Last);
            try { File.WriteAllText(Path.Combine(Dir, $"report_{g.level}_{site}_{look}_{zoom}{(clip ? "_clip" : "")}.txt"), Last + "\n"); }
            catch (System.Exception e) { Debug.LogWarning(Tag + " could not write the report: " + e.Message); }
            Running = false;
        }

        /// <summary>The point within 100 m of the focus, on flat dry ground inside the level's ground, whose ring (35, 50, 65 m) runs over flat
        /// ground and over raised ground (4 m or more over its floor): the best share of raised ground is about a third.</summary>
        static Vector3 HillSite(Vector3 focus, System.Action<string> info)
        {
            Vector3 best = focus; float bestScore = -1f; int bestRaised = 0, bestN = 0; float bestTop = 0f;
            var world = OzBlastGround.World; var fx = OpenZFxPlayer.I;
            for (int ix = -8; ix <= 8; ix++)
                for (int iz = -8; iz <= 8; iz++)
                {
                    if ((ix * ix + iz * iz) * 144f > 100f * 100f) continue;
                    float cx = focus.x + ix * 12f, cz = focus.z + iz * 12f;
                    if (!world.Contains(new Vector2(cx, cz)) || !OzBlastGround.Y(cx, cz, out float cy)) continue;
                    if (fx && fx.WetAt(new Vector3(cx, cy, cz))) continue;
                    int raised = 0, flat = 0, n = 0; float top = 0f;
                    foreach (float r in new[] { 35f, 50f, 65f })
                        for (int k = 0; k < 48; k++)
                        {
                            float a = k * Mathf.PI * 2f / 48f;
                            if (!OzBlastGround.Y(cx + Mathf.Cos(a) * r, cz + Mathf.Sin(a) * r, out float y)) continue;
                            n++; float rise = y - cy;
                            if (rise > 4f) raised++;
                            if (Mathf.Abs(rise) < 1.5f) flat++;
                            if (rise > top) top = rise;
                        }
                    if (n < 100 || raised < 8) continue;
                    float score = Mathf.Min(raised / (float)n, 0.35f) * 2f + Mathf.Min(flat / (float)n, 0.4f) - (ix * ix + iz * iz) * 0.0005f;
                    if (score > bestScore) { bestScore = score; best = new Vector3(cx, cy, cz); bestRaised = raised; bestN = n; bestTop = top; }
                }
            info(bestScore < 0f ? "hills: no raised ground within 100 m of the focus, the focus is used (move the camera to the hills)"
                                : $"hills: the ring crosses raised ground on {bestRaised} of {bestN} points at 35 / 50 / 65 m, up to {bestTop:0.0} m over the blast's floor");
            return best;
        }
    }
}
#endif
