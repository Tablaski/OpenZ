// OpenZ (GPL-3.0) - version 1.3: the counted Play checks and frames of rows 19 FLAGS (View/OzNeutralFlagWaves.cs: steady rings on level 7) and 20 FLAG CLOTH
// (View/FlagView.Face.cs, View/OzFlagFace.cs: the cloth faces the camera). Editor only, never in a build.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Menu OpenZ/V1.3/Flags. Each part runs in Play on a running level, at game speed 1, not paused, and writes one report (and its frames) under the milestones
    /// folder, v1_3/flags. The camera, the zoom and the clock are put back at the end.
    ///  * Rings, level 7: R1 to R6, frames by day and by night at the RTS camera (70 m, and 120 m). It gives one neutral flag to the player (R6): leave Play after.
    ///  * Rings, other level (L02 or L17): O1 to O3, the waves as they were (no steady waves, gaps of 6 s or more, nothing at night).
    ///  * Cloth (L02, L07, L17 in turn): C0 to C11, frames at 8 headings at the normal tilt and at the free camera's lowest tilt, on a zone flag and a fort flag,
    ///    a capture and a fort dip at a turned heading.
    ///  * Cloth, ending orbit (L02 or L17): the enemy fort falls now (OpenZVictory.DebugFortFalls), every frame of the ending's orbit is read. Ends the match.
    /// </summary>
    public static class OzFlagRingsClothCheck
    {
        const string Tag = "[Flags 1.3]", Root = "OpenZ/V1.3/Flags/";
        static string Dir => OzRelease.Milestones("v1_3/flags");

        sealed class Runner : MonoBehaviour { }
        static Runner runner; static bool busy;
        static OzFlagRingsClothCheck() { EditorApplication.playModeStateChanged += _ => busy = false; }
        static Runner R()
        {
            if (!runner) { var go = new GameObject("OzFlagRingsClothCheckRunner") { hideFlags = HideFlags.HideAndDontSave }; runner = go.AddComponent<Runner>(); }
            return runner;
        }

        static int ok, bad; static string part = ""; static readonly StringBuilder notes = new StringBuilder();
        static void Check(bool pass, string what) { if (pass) ok++; else bad++; notes.Append(pass ? "\n  [ok]   " : "\n  [FAIL] ").Append(what); Debug.Log(Tag + (pass ? " ok   " : " FAIL ") + what); }
        static void Info(string what) { notes.Append("\n  [info] ").Append(what); Debug.Log(Tag + " " + what); }
        static void End()
        {
            var text = $"{Tag} {part}: {(bad == 0 && ok > 0 ? "PASS" : "FAIL")} {ok}/{ok + bad}{notes}";
            if (bad == 0 && ok > 0) Debug.Log(text); else Debug.LogError(text);
            try { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, part + ".txt"), text + "\n"); }
            catch (Exception e) { Debug.LogWarning(Tag + " could not write the report: " + e.Message); }
        }

        // ------------------------------------------------------------------ the stage
        static ZGame g; static RtsCamera rts; static DayNight dn;
        static Vector3 camFocus; static float camHeight, dnTime; static bool dnRunning;

        static void Go(Func<IEnumerator> routine, string name)
        {
            if (busy) { Debug.LogWarning(Tag + " a run is still going"); return; }
            g = ZGame.I; rts = UnityEngine.Object.FindFirstObjectByType<RtsCamera>(); dn = UnityEngine.Object.FindFirstObjectByType<DayNight>();
            if (!Application.isPlaying || !g || g.World == null || !rts || !dn) { Debug.LogWarning(Tag + " start a level in Play first (a running level with its RTS camera and clock)"); return; }
            busy = true; ok = bad = 0; notes.Length = 0; part = name + "_" + g.level;
            camFocus = rts.focus; camHeight = rts.height; dnTime = dn.timeOfDay; dnRunning = dn.running;
            try { Directory.CreateDirectory(Dir); } catch (Exception) { }
            Info($"level {g.level}, player {g.player}, time scale {Time.timeScale:0.00} (wanted 1), {Screen.width}x{Screen.height}");
            R().StartCoroutine(Flat(routine()));
        }

        /// <summary>Runs the routine and its nested routines; an exception is a FAIL line. The report and the camera, zoom and clock are put back at the end.</summary>
        static IEnumerator Flat(IEnumerator top)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(top);
            while (stack.Count > 0 && Application.isPlaying)
            {
                var e = stack.Peek(); bool more; object cur = null;
                try { more = e.MoveNext(); if (more) cur = e.Current; }
                catch (Exception ex) { Debug.LogException(ex); Check(false, "exception: " + ex.Message); break; }
                if (!more) { stack.Pop(); continue; }
                if (cur is IEnumerator sub) { stack.Push(sub); continue; }
                yield return cur;
            }
            if (!Application.isPlaying) Check(false, "Play stopped before the end");
            End();
            Restore();
            busy = false;
        }

        static void Restore()
        {
            FlagView.FaceCamera = true;
            if (rts)
            {
                rts.FreeLookOff(); rts.yawHeld = false; rts.yaw = 0f; rts.unclamped = false;
                rts.focus = camFocus; rts.SetHeightImmediate(camHeight); rts.Place();
            }
            if (dn) { NightLights.JumpTo(dnTime); dn.running = dnRunning; }
        }

        static IEnumerator Wait(float s) { float end = Time.unscaledTime + s; while (Time.unscaledTime < end) yield return null; }

        static void Look(Vector3 at, float h) { rts.focus = new Vector3(at.x, rts.focus.y, at.z); rts.SetHeightImmediate(h); rts.Place(); }

        static IEnumerator Shot(string name, string what)
        {
            yield return null; yield return null;                                                     // the camera placed, the flags posed for it
            string file = $"{g.level}_{name}.png";
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, file));
            Info($"frame {file}: {what}");
            yield return null; yield return null;
        }

        static void Day() { dn.running = false; NightLights.JumpTo(0.08f); }
        static void Night() { dn.running = false; NightLights.JumpTo(0.62f); }

        // ================================================================== row 19 FLAGS
        static bool Running(OzNeutralFlagWaves fw, int id) { foreach (var r in fw.Rings()) if (r.id == id) return r.running; return false; }

        static ZObject FarNeutral()
        {
            ZObject best = null; float far = -1f;
            foreach (var f in g.World.Flags)
                if (f != null && !f.Destroyed && f.Owner == Team.Neutral)
                {
                    var p = g.CenterWorld(f); float d = new Vector2(p.x - rts.focus.x, p.z - rts.focus.z).sqrMagnitude;
                    if (d > far) { far = d; best = f; }
                }
            return best;
        }

        /// <summary>Watches flag id's wave for s seconds: the share of the time it runs, the longest gap between two waves, the waves started.</summary>
        sealed class Watch { public float run, all, longestGap, gapFrom = -1f, firstGap = -1f; public int starts; public List<float> gaps = new List<float>(); }
        static IEnumerator Observe(OzNeutralFlagWaves fw, int id, float s, Watch w)
        {
            bool was = Running(fw, id); float end = Time.unscaledTime + s;
            if (!was) w.gapFrom = Time.unscaledTime;
            while (Time.unscaledTime < end)
            {
                yield return null;
                bool now = Running(fw, id); float dt = Time.unscaledDeltaTime; w.all += dt; if (now) w.run += dt;
                if (now && !was)
                {
                    w.starts++;
                    if (w.gapFrom >= 0f) { float gap = Time.unscaledTime - w.gapFrom; if (w.firstGap < 0f) w.firstGap = gap; else { w.gaps.Add(gap); w.longestGap = Mathf.Max(w.longestGap, gap); } }
                }
                if (!now && was) w.gapFrom = Time.unscaledTime;
                was = now;
            }
        }

        static IEnumerator FreshWave(OzNeutralFlagWaves fw, int id, float most)
        {
            float end = Time.unscaledTime + most; bool was = Running(fw, id);
            while (Time.unscaledTime < end) { yield return null; bool now = Running(fw, id); if (now && !was) break; was = now; }
        }

        [MenuItem(Root + "Rings: counted check and frames (Play, L07)")]
        static void RingsMenu() => Go(Rings, "rings");

        static IEnumerator Rings()
        {
            var fw = OzNeutralFlagWaves.I;
            Check(g.level == OzVolcanic.Level, $"R0 this is level 7 ({g.level})");
            if (g.level != OzVolcanic.Level) yield break;
            Check(fw && fw.enabled && fw.Steady && !fw.BeaconHere, $"R1 the wave layer runs, level 7's steady waves are on, no capture beacon in their place (steady {(fw ? fw.Steady.ToString() : "no layer")})");
            if (!fw || !fw.enabled) yield break;
            var flag = FarNeutral();
            if (flag == null) { Check(false, "R2 a neutral flag on this level: none left, start the level anew"); yield break; }
            Day(); yield return Wait(1f);
            int wrong0 = OzNeutralFlagWaves.StartedWrong; var foot = g.CenterWorld(flag);
            bool before = Running(fw, flag.Id);
            Look(foot, 70f);
            float arrive = Time.unscaledTime, firstAt = -1f;
            while (Time.unscaledTime - arrive < OzNeutralFlagWaves.SteadyGap + 1f) { yield return null; if (Running(fw, flag.Id)) { firstAt = Time.unscaledTime - arrive; break; } }
            Check(firstAt >= 0f && firstAt <= OzNeutralFlagWaves.SteadyGap + 0.2f, $"R2 flag {flag.Id}, off camera, then the RTS camera on it at 70 m: its wave runs {(firstAt < 0f ? "never (1.6 s)" : firstAt.ToString("0.00") + " s")} after (wanted at once, at most the {OzNeutralFlagWaves.SteadyGap} s gap{(before ? "; one ran already" : "")})");
            OzNeutralFlagWaves.LastPeak = 0f;
            var w = new Watch(); yield return Observe(fw, flag.Id, 20f, w);
            float share = w.all > 0f ? w.run / w.all : 0f;
            Check(share >= 0.75f && w.longestGap <= OzNeutralFlagWaves.SteadyGap + 0.15f && w.starts >= 5,
                  $"R3 by day, 20 s on camera: a wave runs {share * 100f:0} % of the time (wanted 75 % or more: {OzNeutralFlagWaves.WaveSeconds} s waves, {OzNeutralFlagWaves.SteadyGap} s gaps), {w.starts} waves, longest gap {w.longestGap:0.00} s (wanted {OzNeutralFlagWaves.SteadyGap + 0.15f:0.00} or less)");
            Check(OzNeutralFlagWaves.LastPeak > 0.01f && OzNeutralFlagWaves.LastPeak <= OzNeutralFlagWaves.PeakDay * OzNeutralFlagWaves.SteadyPeakScale + 1e-4f, $"R4 drawn by day, the game's own wave strength: strongest opacity {OzNeutralFlagWaves.LastPeak:0.000} (above 0, at most {OzNeutralFlagWaves.PeakDay:0.00})");
            yield return FreshWave(fw, flag.Id, 4f); yield return Wait(1f);
            yield return Shot("rings_day_70m", $"flag {flag.Id} neutral, day (clock {dn.timeOfDay:0.00}), RTS camera 70 m, 1 s into a wave");
            Look(foot, 120f); yield return FreshWave(fw, flag.Id, 4f); yield return Wait(1f);
            yield return Shot("rings_day_120m", "the same, 120 m: the flag among the others");
            Night(); Look(foot, 70f); yield return Wait(2f);
            Info($"night: clock {dn.timeOfDay:0.00}, night grade {NightGrade.NightK:0.00}, flags' night {FlagView.NightLook:0.00}");
            OzNeutralFlagWaves.LastPeak = 0f;
            var wn = new Watch(); yield return Observe(fw, flag.Id, 10f, wn);
            float shareN = wn.all > 0f ? wn.run / wn.all : 0f;
            Check(FlagView.NightLook > 0.9f, $"R5a night reached for the night rows (flags' night {FlagView.NightLook:0.00}; NIGHT & RAIN off keeps the day: switch it on)");
            Check(shareN >= 0.75f && wn.longestGap <= OzNeutralFlagWaves.SteadyGap + 0.15f && OzNeutralFlagWaves.LastPeak > 0.01f && OzNeutralFlagWaves.LastPeak <= OzNeutralFlagWaves.PeakDay * OzNeutralFlagWaves.SteadyPeakScale + 1e-4f,
                  $"R5b by night, 10 s: a wave runs {shareN * 100f:0} % of the time, longest gap {wn.longestGap:0.00} s, drawn at opacity {OzNeutralFlagWaves.LastPeak:0.000} (above 0: level 7 keeps them at night; the night strength is {OzNeutralFlagWaves.PeakNight:0.00})");
            yield return FreshWave(fw, flag.Id, 4f); yield return Wait(1f);
            yield return Shot("rings_night_70m", $"flag {flag.Id} neutral, night (clock {dn.timeOfDay:0.00}), RTS camera 70 m, 1 s into a wave");
            Look(foot, 120f); yield return FreshWave(fw, flag.Id, 4f); yield return Wait(1f);
            yield return Shot("rings_night_120m", "the same, 120 m");
            Day(); Look(foot, 70f); yield return Wait(1.5f);
            g.World.AwardZone(flag, g.player);
            yield return Wait(0.6f);
            bool stillAfter = Running(fw, flag.Id);
            var wc = new Watch(); yield return Observe(fw, flag.Id, 6f, wc);
            Check(flag.Owner == g.player && !stillAfter && wc.starts == 0 && wc.run <= 0f, $"R6 captured (given to {g.player}): its wave is out 0.6 s later ({(stillAfter ? "STILL RUNNING" : "out")}), no wave on it in the next 6 s ({wc.starts} started)");
            Check(OzNeutralFlagWaves.StartedWrong == wrong0, $"R7 no wave started on a flag that was owned or off camera: {OzNeutralFlagWaves.StartedWrong - wrong0} (wanted 0)");
            Info("leave Play now: one flag was given to the player");
        }

        [MenuItem(Root + "Rings: other level unchanged (Play, L02 or L17)")]
        static void OtherMenu() => Go(Other, "rings_other");

        static IEnumerator Other()
        {
            var fw = OzNeutralFlagWaves.I;
            Check(g.level != OzVolcanic.Level, $"O0 not level 7 ({g.level})");
            Check(fw && fw.enabled && !fw.Steady, "O1 the wave layer runs, without level 7's steady waves");
            if (!fw || !fw.enabled || g.level == OzVolcanic.Level) yield break;
            if (fw.BeaconHere) { Info("the capture beacon draws here (the level was started straight in its scene): start it from the main menu, as the game does, to judge O2, O3"); yield break; }
            var flag = FarNeutral();
            if (flag == null) { Check(false, "O2 a neutral flag on this level: none left"); yield break; }
            Day(); Look(g.CenterWorld(flag), 70f);
            var w = new Watch(); yield return Observe(fw, flag.Id, 30f, w);
            float shortest = 99f; foreach (var gp in w.gaps) shortest = Mathf.Min(shortest, gp);
            Check(w.starts >= 1 && (w.gaps.Count == 0 || shortest >= OzNeutralFlagWaves.GapMin - 0.1f),
                  $"O2 by day, 30 s on camera: {w.starts} waves, first after {w.firstGap:0.0} s, the gaps between them {(w.gaps.Count == 0 ? "none complete" : string.Join(", ", w.gaps.ConvertAll(x => x.ToString("0.0"))) + " s")} (as before: {OzNeutralFlagWaves.GapMin} to {OzNeutralFlagWaves.GapMax} s)");
            Night(); yield return Wait(2f);
            OzNeutralFlagWaves.LastPeak = 0f;
            var wn = new Watch(); yield return Observe(fw, flag.Id, 12f, wn);
            Check(FlagView.NightLook < 0.99f || OzNeutralFlagWaves.LastPeak <= 0f, $"O3 by night (flags' night {FlagView.NightLook:0.00}): nothing drawn, the pool of light takes over as in 1.2 (strongest opacity {OzNeutralFlagWaves.LastPeak:0.000}, wanted 0)");
        }

        // ================================================================== row 20 FLAG CLOTH
        sealed class Track { public float lastYaw = float.NaN, lastSideT = -99f; public int lastSide, sideChanges; public float minGap = 99f; }
        static Track[] tracks;
        static int pops, nans, coneHits, ruleConeHits, frames, faceFrames, faceGood; static float worstPop, faceMin; static string worstPopWhat;
        static int lastSampleFrame = -10; static float lastSampleDt, fastestTurn; static string fastestWhat = "none";   // the last Sample's frame and dt; the fastest cloth turn (C7, E4)

        static void ResetTracks(FlagView fv)
        {
            tracks = new Track[fv.FlagCount]; for (int i = 0; i < tracks.Length; i++) tracks[i] = new Track();
            pops = nans = coneHits = ruleConeHits = frames = faceFrames = faceGood = 0; worstPop = 0f; faceMin = 2f; worstPopWhat = "none"; lastSampleFrame = -10; fastestTurn = 0f; fastestWhat = "none";
        }

        static void ResetFace() { faceFrames = faceGood = 0; faceMin = 2f; }

        static Vector3 Heading()
        {
            var cam = Camera.main; if (!cam) return Vector3.forward;
            var h = cam.transform.forward + cam.transform.up; h.y = 0f; return h.sqrMagnitude > 1e-6f ? h.normalized : Vector3.forward;
        }

        static float YawOf(Transform piv) { var r = piv.right; return Mathf.Atan2(-r.z, r.x) * Mathf.Rad2Deg; }

        static float FaceOf(Transform piv, Vector3 h) { var n = piv.forward; n.y = 0f; return n.sqrMagnitude > 1e-6f ? Mathf.Abs(Vector3.Dot(n.normalized, h)) : 0f; }

        /// <summary>One frame of every flag that shows: pops, NaN, side changes, the fort cone (drawn and by the rule), and, when judged, the face to the camera
        /// (a flag that changed side in the last 1.2 s is still sweeping and is left out).</summary>
        static void Sample(FlagView fv, float faceWanted)
        {
            var h = Heading(); float dt = Time.deltaTime, now = Time.unscaledTime; frames++;
            // Sample runs in Update (the coroutine) and reads the pose FlagView's LateUpdate made LAST frame, so the turn since the last Sample was made with that
            // frame's dt, not this one's (with this frame's, every short frame after a long one counted a false pop). After skipped frames (a Shot) it starts again.
            bool chain = Time.frameCount == lastSampleFrame + 1; float turnDt = lastSampleDt; lastSampleFrame = Time.frameCount; lastSampleDt = dt;
            for (int i = 0; i < fv.FlagCount && i < tracks.Length; i++)
            {
                fv.FaceProbe(i, out int id, out bool fort, out bool shown, out float fy, out int side, out Vector3 bd, out float bc);
                var T = tracks[i];
                if (!shown) { T.lastYaw = float.NaN; continue; }
                var piv = fv.PivotForCheck(i); float yaw = YawOf(piv);
                if (float.IsNaN(yaw) || float.IsNaN(fy)) { nans++; continue; }
                if (chain && !float.IsNaN(T.lastYaw))
                {
                    float d = Mathf.Abs(Mathf.DeltaAngle(T.lastYaw, yaw)), allow = (OzFlagFace.MaxSpeed * 1.25f + 30f) * turnDt + 0.5f;
                    if (d > allow) { pops++; if (d - allow > worstPop) { worstPop = d - allow; worstPopWhat = $"flag {id} turned {d:0.0} deg in one frame of {turnDt * 1000f:0} ms"; } }
                    if (turnDt > 0f && d / turnDt > fastestTurn) { fastestTurn = d / turnDt; fastestWhat = $"flag {id}, {d:0.0} deg in a {turnDt * 1000f:0} ms frame"; }
                }
                T.lastYaw = yaw;
                if (T.lastSide != 0 && side != T.lastSide) { T.sideChanges++; T.minGap = Mathf.Min(T.minGap, now - T.lastSideT); T.lastSideT = now; }
                T.lastSide = side;
                if (fort && bc < 1f)
                {
                    var r = piv.right; var fly = new Vector3(r.x, 0f, r.z).normalized; if (Vector3.Dot(fly, bd) > bc + 0.003f) coneHits++;
                    float fr = fy * Mathf.Deg2Rad; var rule = new Vector3(Mathf.Cos(fr), 0f, -Mathf.Sin(fr)); if (Vector3.Dot(rule, bd) > bc + 0.003f) ruleConeHits++;
                }
                if (faceWanted > 0f && now - T.lastSideT > 1.2f) { float face = FaceOf(piv, h); faceFrames++; if (face >= faceWanted) faceGood++; faceMin = Mathf.Min(faceMin, face); }
            }
        }

        static IEnumerator Settle(FlagView fv, float s) { float end = Time.unscaledTime + s; while (Time.unscaledTime < end) { yield return null; Sample(fv, 0f); } }

        /// <summary>Every shown flag now: the worst face to the camera, and what the 1.2 rule (downwind) would show.</summary>
        static void Faces(FlagView fv, string label, float wanted)
        {
            var h = Heading(); float worst = 2f, old = 2f; string who = "none";
            float wy = fv.WindYawForCheck * Mathf.Deg2Rad; var oldN = new Vector3(Mathf.Sin(wy), 0f, Mathf.Cos(wy));
            for (int i = 0; i < fv.FlagCount; i++)
            {
                fv.FaceProbe(i, out int id, out bool fort, out bool shown, out _, out _, out _, out _);
                if (!shown) continue;
                float f = FaceOf(fv.PivotForCheck(i), h); if (f < worst) { worst = f; who = (fort ? "fort flag " : "flag ") + id; }
                old = Mathf.Min(old, Mathf.Abs(Vector3.Dot(oldN, h)));
            }
            Check(worst >= wanted, $"{label}: every flag's face to the camera {worst:0.00} at worst ({who}; wanted {wanted:0.00}; 1 = full face). Downwind, as in 1.2, it would be {old:0.00}");
        }

        [MenuItem(Root + "Cloth: counted check and frames (Play, L02, L07, L17)")]
        static void ClothMenu() => Go(Cloth, "cloth");

        static IEnumerator Cloth()
        {
            var fv = FlagView.I;
            Check(fv && fv.isActiveAndEnabled && FlagView.FaceCamera && fv.FlagCount > 0, $"C0 FlagView runs with {(fv ? fv.FlagCount : 0)} flags, the cloth faces the camera");
            if (!fv || !fv.isActiveAndEnabled || fv.FlagCount == 0) yield break;
            ResetTracks(fv);
            int zone = -1, fort = -1, zoneIndex = -1, zi = 0; float best = float.MaxValue;
            for (int i = 0; i < fv.FlagCount; i++)
            {
                fv.FaceProbe(i, out int id, out bool isFort, out bool shown, out _, out _, out _, out _);
                if (isFort) { if (fort < 0 && shown) fort = i; continue; }
                var p = fv.PivotForCheck(i).position; float d = new Vector2(p.x - rts.focus.x, p.z - rts.focus.z).sqrMagnitude;
                if (d < best) { best = d; zone = i; zoneIndex = zi; }
                zi++;
            }
            Info($"framed: zone flag #{zone} (zone index {zoneIndex}), fort flag #{fort}; wind {fv.WindDirForCheck}, wind yaw {fv.WindYawForCheck:0.0}; {fv.Diagnostics()}");
            Vector3 zp = zone >= 0 ? fv.PivotForCheck(zone).position : rts.focus, fp = fort >= 0 ? fv.PivotForCheck(fort).position : rts.focus;
            Day();
            // C1 the normal camera
            rts.FreeLookOff(); rts.yawHeld = false; rts.yaw = 0f; Look(zp, 70f);
            yield return Settle(fv, 1.5f);
            Faces(fv, "C1 the normal camera (north up)", 0.9f);
            yield return Shot("cloth_normal_zone", "the normal camera, 70 m, a zone flag");
            if (fort >= 0) { Look(fp, 70f); yield return Shot("cloth_normal_fort", "the normal camera, 70 m, a fort flag"); }
            // C2 8 headings at the normal tilt, C3 the same at the free camera's lowest tilt
            rts.yawHeld = true;
            for (int pass = 0; pass < 2; pass++)
            {
                string tilt = pass == 0 ? "normal tilt" : "free camera, lowest tilt";
                if (pass == 1)
                {
                    rts.FreeTurnBy(new Vector2(0f, 2000f)); yield return null; yield return null;
                    if (rts.Tilt > rts.pitch - 10f) { Info($"C3 not run: the free camera's tilt did not hold (FREE CAMERA off in Options? tilt {rts.Tilt:0})"); break; }
                    Info($"C3 tilt {rts.Tilt:0} deg (the normal view's {rts.pitch:0})");
                }
                for (int k = 0; k < 8; k++)
                {
                    float H = 45f * k; rts.yaw = Mathf.DeltaAngle(0f, H); Look(zp, 35f);
                    yield return Settle(fv, 1.6f);
                    Faces(fv, $"{(pass == 0 ? "C2" : "C3")} heading {H:0}, {tilt}", 0.9f);
                    yield return Shot($"cloth_{(pass == 0 ? "h" : "low_h")}{H:000}_zone", $"heading {H:0}, {tilt}, 35 m, a zone flag");
                    if (fort >= 0) { Look(fp, 45f); yield return Shot($"cloth_{(pass == 0 ? "h" : "low_h")}{H:000}_fort", $"heading {H:0}, {tilt}, 45 m, a fort flag"); }
                }
            }
            rts.FreeLookOff(); rts.yaw = 0f; Look(zp, 70f);
            yield return Settle(fv, 1.5f);
            // C4 a full turn at 60 deg/s and back at 120 deg/s
            int[] before = new int[tracks.Length]; for (int i = 0; i < tracks.Length; i++) before[i] = tracks[i].sideChanges;
            ResetFace();
            for (int dir = 0; dir < 2; dir++)
            {
                float secs = dir == 0 ? 6f : 3f, t0 = Time.unscaledTime;
                while (Time.unscaledTime - t0 < secs)
                {
                    float u = Mathf.Clamp01((Time.unscaledTime - t0) / secs), a = dir == 0 ? 360f * u : 360f * (1f - u);
                    rts.yaw = Mathf.DeltaAngle(0f, a);
                    yield return null; Sample(fv, 0.7f);
                }
            }
            int most = 0, mostFort = 0;
            for (int i = 0; i < tracks.Length; i++) { fv.FaceProbe(i, out _, out bool isFort, out _, out _, out _, out _, out _); int c = tracks[i].sideChanges - before[i]; if (isFort) mostFort = Mathf.Max(mostFort, c); else most = Mathf.Max(most, c); }
            Check(most <= 4 && mostFort <= 8, $"C4a a full turn and back: side changes at most {most} for a zone flag (wanted 4 or fewer: the wind crosses the view twice a turn), {mostFort} for a fort flag (8 or fewer)");
            Check(faceFrames > 0 && faceGood >= faceFrames * 0.95f, $"C4b while the camera turns (60 and 120 deg/s): the face is 0.70 or more on {faceGood} of {faceFrames} flag-frames (wanted 95 %), worst {faceMin:0.00}; sweeps left out");
            // C5 the dead band: the view hovers round the wind's line
            var wd = fv.WindDirForCheck; float hw = Mathf.Atan2(wd.x, wd.z) * Mathf.Rad2Deg;
            rts.yaw = Mathf.DeltaAngle(0f, hw); yield return Settle(fv, 2f);
            for (int i = 0; i < tracks.Length; i++) before[i] = tracks[i].sideChanges;
            float t1 = Time.unscaledTime;
            while (Time.unscaledTime - t1 < 6f) { rts.yaw = Mathf.DeltaAngle(0f, hw + 12f * Mathf.Sin((Time.unscaledTime - t1) * Mathf.PI * 2f / 1.5f)); yield return null; Sample(fv, 0f); }
            int dither = 0; for (int i = 0; i < tracks.Length; i++) { fv.FaceProbe(i, out _, out bool isFort, out _, out _, out _, out _, out _); if (!isFort) dither = Mathf.Max(dither, tracks[i].sideChanges - before[i]); }
            Check(dither == 0, $"C5a the view swings 12 deg either side of the wind's line for 6 s: {dither} side changes (wanted 0: dead band of {Mathf.Asin(OzFlagFace.Band) * Mathf.Rad2Deg:0.0} deg)");
            for (int i = 0; i < tracks.Length; i++) { tracks[i].minGap = 99f; before[i] = tracks[i].sideChanges; }
            float t2 = Time.unscaledTime;
            while (Time.unscaledTime - t2 < 6f) { rts.yaw = Mathf.DeltaAngle(0f, hw + 30f * Mathf.Sin((Time.unscaledTime - t2) * Mathf.PI * 2f / 3f)); yield return null; Sample(fv, 0f); }
            float gap = 99f; int swings = 0;
            for (int i = 0; i < tracks.Length; i++) { fv.FaceProbe(i, out _, out bool isFort, out _, out _, out _, out _, out _); if (isFort) continue; swings = Mathf.Max(swings, tracks[i].sideChanges - before[i]); if (tracks[i].sideChanges - before[i] > 1) gap = Mathf.Min(gap, tracks[i].minGap); }
            Check(gap >= 1f, $"C5b 30 deg either side, every 3 s: {swings} side changes at most, never two within {(gap > 98f ? "the run" : gap.ToString("0.00") + " s")} (wanted 1 s or more: each a full sweep, no flapping)");
            rts.yaw = 0f; yield return Settle(fv, 1.5f);
            // C6 to C8 over every frame so far
            Check(coneHits == 0, $"C6a a fort's cloth never flies into the cone toward its turret, as drawn: {coneHits} flag-frames inside (wanted 0)");
            Check(ruleConeHits == 0, $"C6b nor by the new rule before the sway: {ruleConeHits} flag-frames (wanted 0; PoseCloth's old clamp is only a safety)");
            Check(pops == 0 && nans == 0, $"C7 no pop, no NaN over {frames} frames: {pops} pops ({worstPopWhat}), {nans} NaN (a pop = a turn faster than {OzFlagFace.MaxSpeed * 1.25f + 30f:0} deg/s + 0.5 deg in one frame); the fastest cloth turn {fastestTurn:0} deg/s ({fastestWhat}; the spring's top speed {OzFlagFace.MaxSpeed:0})");
            int poleBad = 0;
            for (int i = 0; i < fv.FlagCount; i++)
            {
                var piv = fv.PivotForCheck(i); var cl = fv.ClothForCheck(i);
                if (piv.localPosition.sqrMagnitude > 1e-8f || Vector3.Angle(piv.up, Vector3.up) > 0.05f || cl.localPosition.x <= 0f) poleBad++;
            }
            Check(poleBad == 0, $"C8 not through the pole: every pivot on its pole's axis, turned about the upright only, every cloth's hoist outside the pole ({poleBad} wrong)");
            // C9 a capture at a turned heading, C10 the fort dip
            rts.yaw = Mathf.DeltaAngle(0f, 135f); Look(zp, 35f); yield return Settle(fv, 1.6f);
            if (zone >= 0)
            {
                var cl = fv.ClothForCheck(zone); float y0 = cl.localPosition.y, low = y0;
                fv.ReplayCapture(zoneIndex);
                float t3 = Time.unscaledTime; bool s1 = false, s2 = false;
                while (Time.unscaledTime - t3 < 3.6f)
                {
                    yield return null; Sample(fv, 0f); low = Mathf.Min(low, cl.localPosition.y);
                    float e = Time.unscaledTime - t3;
                    if (!s1 && e >= 0.25f) { s1 = true; ScreenCapture.CaptureScreenshot(Path.Combine(Dir, g.level + "_cloth_capture_drop.png")); Info($"frame {g.level}_cloth_capture_drop.png: a capture replayed at heading 135, 0.25 s in: the old cloth goes down"); }
                    if (!s2 && e >= 1.0f) { s2 = true; ScreenCapture.CaptureScreenshot(Path.Combine(Dir, g.level + "_cloth_capture_rise.png")); Info($"frame {g.level}_cloth_capture_rise.png: 1 s in: the new cloth rises"); }
                }
                Check(low < y0 - 0.4f && Mathf.Abs(cl.localPosition.y - y0) < 0.05f, $"C9 a capture at heading 135: the cloth drops ({y0:0.00} down to {low:0.00} m) and rises back ({cl.localPosition.y:0.00} m), as before");
                Faces(fv, "C9 after the capture", 0.9f);
            }
            if (fort >= 0)
            {
                Look(fp, 45f); yield return Settle(fv, 0.5f);
                var cl = fv.ClothForCheck(fort); float y0 = cl.localPosition.y, low = y0;
                fv.ForceFortDanger(5f);
                float t4 = Time.unscaledTime; bool s3 = false;
                while (Time.unscaledTime - t4 < 3f)
                {
                    yield return null; Sample(fv, 0f); low = Mathf.Min(low, cl.localPosition.y);
                    if (!s3 && Time.unscaledTime - t4 >= 2.5f) { s3 = true; ScreenCapture.CaptureScreenshot(Path.Combine(Dir, g.level + "_cloth_fort_dip.png")); Info($"frame {g.level}_cloth_fort_dip.png: heading 135, the fort flag dipped (danger)"); }
                }
                Check(low < y0 - 0.3f, $"C10 the fort flag dips in danger at heading 135, as before ({y0:0.00} down to {low:0.00} m)");
            }
            // C11 the A/B switch
            rts.yaw = 0f; Look(zp, 70f); yield return Settle(fv, 1f);
            FlagView.FaceCamera = false; yield return null; yield return null; yield return null;
            int offBad = 0; float sway = fv.swayDegrees + 0.5f;
            for (int i = 0; i < fv.FlagCount; i++)
            {
                fv.FaceProbe(i, out _, out bool isFort, out bool shown, out float fy, out _, out _, out _);
                if (isFort || !shown) continue;
                if (!float.IsNaN(fy) || Mathf.Abs(Mathf.DeltaAngle(fv.WindYawForCheck, YawOf(fv.PivotForCheck(i)))) > sway) offBad++;
            }
            FlagView.FaceCamera = true; ResetTracks(fv); yield return Settle(fv, 1.5f);
            int onBad = 0; for (int i = 0; i < fv.FlagCount; i++) { fv.FaceProbe(i, out _, out _, out bool shown, out float fy, out _, out _, out _); if (shown && float.IsNaN(fy)) onBad++; }
            Check(offBad == 0 && onBad == 0, $"C11 FlagView.FaceCamera off: every zone flag downwind within its sway, as in 1.2 ({offBad} not); on again: all face the camera ({onBad} not)");
        }

        [MenuItem(Root + "Cloth: ending orbit (Play, L02 or L17; ends the match)")]
        static void OrbitMenu() => Go(Orbit, "cloth_orbit");

        static IEnumerator Orbit()
        {
            var fv = FlagView.I;
            Check(fv && fv.isActiveAndEnabled && FlagView.FaceCamera, "E0 FlagView runs, the cloth faces the camera");
            Check(OpenZEnding.Orbit, "E1 the ending's orbit is on");
            if (!fv || !fv.isActiveAndEnabled) yield break;
            if (g.level == "L16" || g.level == OzVolcanic.Level) Info("this level has its own ending scene: the orbit is the one of L02 and L17");
            Day(); ResetTracks(fv);
            int n = OpenZVictory.DebugFortFalls(true);
            Info($"the enemy fort falls now: {n} part(s)");
            float t0 = Time.unscaledTime, most = 0f; int shot = 0; float[] at = { 10f, 25f, 38f };
            while (Time.unscaledTime - t0 < 20f)
            {
                yield return null; Sample(fv, 0.85f);
                float y = Mathf.Abs(rts.yaw); most = Mathf.Max(most, y);
                if (shot < at.Length && y >= at[shot]) { ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"{g.level}_cloth_orbit_{shot + 1}.png")); Info($"frame {g.level}_cloth_orbit_{shot + 1}.png: the orbit at {rts.yaw:0} deg"); shot++; }
            }
            Info("orbit: " + OpenZEnding.OrbitInfo);
            Check(most >= 30f, $"E2 the orbit ran: the view turned {most:0} deg (the ending turns {OpenZEnding.OrbitDegrees:0})");
            Check(faceFrames > 0 && faceGood >= faceFrames * 0.97f, $"E3 the face to the camera is 0.85 or more on {faceGood} of {faceFrames} flag-frames (wanted 97 %), worst {faceMin:0.00}");
            Check(pops == 0 && nans == 0 && coneHits == 0, $"E4 over {frames} frames: {pops} pops ({worstPopWhat}), {nans} NaN, {coneHits} fort cone hits (all wanted 0); the fastest cloth turn {fastestTurn:0} deg/s ({fastestWhat})");
            Info("leave Play now: the match is over");
        }
    }
}
