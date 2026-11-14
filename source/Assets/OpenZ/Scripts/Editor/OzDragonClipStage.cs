#if !OPENZ_HARNESS
// OpenZ (GPL-3.0) - version 1.3, level 7, DV163: two staged scenes for the real-time clips of the dragon rows. Nothing here runs in a game.
//   OpenZ/L07 Lava/Checks/Dragons/7 Clip stage: a squad under the flame, then the mouth close    the flame hurts the whole squad under it; the jet leaves the snout
//   OpenZ/L07 Lava/Checks/Dragons/8 Clip stage: a dragon at a flying shuttle                       the flame goes from the mouth down to the hull, the shuttle takes damage, sound
// Start the screen recorder, then the menu, in Play on level 7. A stage holds the dragons' own looks, sets the rules' chance and gaps aside for its passes (ZWorld.DragonProofForce, as the
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzDragonClipStage
    {
        static IEnumerator run; static string tag; static int pass, n; static float t0; static readonly List<string> lines = new List<string>();
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();                                    // a yielded IEnumerator (Wait, Past60, Pass) runs to its end before the step goes on
        static RtsCamera heldRts; static Camera heldCam; static float heldFov;

        static void Line(bool ok, string s) { n++; if (ok) pass++; lines.Add((ok ? "[" + tag + "] ok   " : "[" + tag + "] FAIL ") + s); }
        static int Played(string key) => OzSoundLog.Since(t0).Count(e => e.key == key && (e.outcome == null || !e.outcome.StartsWith("DROP")));
        static void Cam(Vector3 at, float h) { var rts = Object.FindFirstObjectByType<RtsCamera>(); if (rts) { rts.focus = new Vector3(at.x, 0f, at.z); rts.SetHeightImmediate(h); } }
        static string Shot(string what, int i) => Path.Combine(OpenZPaths.Records, $"l07_dragon_{tag.Substring(tag.Length - 1)}_{what}{i:00}.png");
        static IEnumerator Wait(float s) { float end = Time.unscaledTime + s; while (Time.unscaledTime < end) yield return null; }

        static IEnumerator Past60(ZGame g)
        {
            g.gameSpeed = 4f;
            while (g.World.Time < ZWorld.DragonFirstSeconds + 1) yield return null;
            g.gameSpeed = 1f;
        }

        static ZObject Robot(ZGame g, string type, int x, int y)
        {
            var o = g.World.CreateRobotGroup(type, x - 8, y - 8, g.player, 1);
            g.SpawnUnitView(o); g.World.SendHold(g.player, o);
            return o;
        }

        /// <summary>The Play checks' pass (AmbientLife.ProofPass), tried again for up to 15 s while no dragon is soaring free. got[0]: the rules' answer, -1 = no dragon free or no clear path.</summary>
        static IEnumerator Pass(AmbientLife life, ZObject target, float deg, int[] got)
        {
            got[0] = -1;
            for (float until = Time.unscaledTime + 15f; ; )
            {
                got[0] = life.ProofPass(target, deg);
                if (got[0] >= 0 || Time.unscaledTime >= until) yield break;
                yield return Wait(0.5f);
            }
        }

        static bool Ready()
        {
            var g = ZGame.I;
            if (!Application.isPlaying || !g || g.World == null || !AmbientLife.I) { Debug.LogError("[DRAGON] FAIL enter Play on level 7 first"); return false; }
            if (!g.World.Map.HasLava) { Debug.LogError("[DRAGON] FAIL this one runs on level 7"); return false; }
            if (run != null) { Debug.LogError("[DRAGON] FAIL a stage is still running"); return false; }
            return true;
        }

        static void Start(string t, IEnumerator r)
        {
            tag = t; pass = n = 0; lines.Clear(); t0 = Time.unscaledTime; run = r; stack.Clear(); stack.Push(r);
            OzDragonProof.Reset();
            EditorApplication.update -= Pump; EditorApplication.update += Pump;
        }

        static void Release()
        {
            if (heldCam) heldCam.fieldOfView = heldFov;
            if (heldRts) heldRts.enabled = true;
            heldCam = null; heldRts = null;
        }

        static void Stop()
        {
            EditorApplication.update -= Pump; run = null; stack.Clear();
            OzDragonProof.Recording = false; OzDragonProof.NoAsks = false;
            Release();
            var g = ZGame.I;
            if (g) { g.gameSpeed = 1f; if (g.World != null) g.World.DragonProofForce = false; }
        }

        static void Pump()
        {
            if (!Application.isPlaying) { Stop(); Debug.LogError("[" + tag + "] FAIL Play ended during the stage"); return; }
            bool more = false;
            try
            {
                while (stack.Count > 0)
                {
                    var top = stack.Peek();
                    if (top.MoveNext()) { if (top.Current is IEnumerator inner) { stack.Push(inner); continue; } more = true; break; }
                    stack.Pop();
                }
            }
            catch (System.Exception ex) { Stop(); Debug.LogError("[" + tag + "] FAIL the stage threw: " + ex); return; }
            if (more) return;
            Stop();
            var path = Path.Combine(OpenZPaths.Records, $"l07_dragon_{tag.Substring(tag.Length - 1)}_sound_log.txt");
            File.WriteAllText(path, OzSoundLog.Format(OzSoundLog.Since(t0)));
            lines.Add($"[{tag}] sound log {path}");
            lines.Add($"[{tag}] {(pass == n ? "PASS" : "FAIL")} {pass}/{n}");
            Debug.Log(string.Join("\n", lines));
        }

        // ------------------------------------------------------------------ 7
        [MenuItem("OpenZ/L07 Lava/Checks/Dragons/7 Clip stage: a squad under the flame, then the mouth close (Play, level 7)")]
        public static void Seven() { if (Ready()) Start("DRAGON7", SevenRun()); }

        static IEnumerator SevenRun()
        {
            var g = ZGame.I; var w = g.World; var life = AmbientLife.I;
            yield return Past60(g);
            OzDragonProof.NoAsks = true; OzDragonProof.Recording = true; w.DragonProofForce = true;
            var (x, y) = OzDragonCount.Open(w);
            int[] dx = { 0, 16, -16, 0, 0 }, dy = { 0, 0, 0, 16, -16 };                                  // five grunts, 2 m apart: all inside the 2.5 m round the flame's end
            var squad = new List<ZObject>();
            for (int i = 0; i < dx.Length; i++) squad.Add(Robot(g, "grunt", x + dx[i], y + dy[i]));
            var input = Object.FindFirstObjectByType<PlayerInput>();
            if (input) { input.Selection.Clear(); input.Selection.AddRange(squad); }                 // the player's own way to see their bars
            Cam(ZGame.PxToWorld(x, y), 42f);
            yield return Wait(2f);
            var hp0 = squad.Select(o => o.Health).ToArray();
            int dealt0 = w.DragonDealt, splash0 = w.DragonSplashHits, kills0 = squad.Sum(o => o.Kills);
            var got = new int[1];
            yield return Pass(life, squad[0], 30f, got); int ans = got[0];
            Line(ans == 0, $"a pass at the squad's middle grunt: {(ans >= 0 ? ZWorld.DragonWhyName[ans] : "no clear path")}");
            if (ans != 0) yield break;
            int frames = 0; float nextShot = Time.unscaledTime;
            while (w.DragonPass != null)
            {
                if (Time.unscaledTime >= nextShot && frames < 16) { nextShot += 0.35f; ScreenCapture.CaptureScreenshot(Shot("squad_", frames++)); }
                yield return null;
            }
            yield return Wait(1.5f);
            int hurt = Enumerable.Range(0, squad.Count).Count(i => squad[i].Health < hp0[i]);
            string took = string.Join(", ", Enumerable.Range(0, squad.Count).Select(i => $"{100f * (hp0[i] - squad[i].Health) / squad[i].MaxHealth:F0} %"));
            Line(hurt == squad.Count && w.DragonSplashHits > splash0, $"every grunt of the squad lost health: {hurt} of {squad.Count} ({took}), {w.DragonSplashHits - splash0} bites on the others, DragonDealt {w.DragonDealt - dealt0} HP");
            Line(squad.All(o => !o.Destroyed), "a healthy squad lives through one pass");
            Line(squad.Sum(o => o.Kills) == kills0, "no kill credit");
            Line(OzDragonScream.Ids.Sum(sc => Played(sc)) >= 1, $"the scream at the start (1.4 ptero_scream: one of the owner's three): {OzDragonScream.Ids.Sum(sc => Played(sc))}");
            Line(Played("wpn_pyro_flame_start") >= 1, $"the flame's sounds: start {Played("wpn_pyro_flame_start")}, stop {Played("wpn_pyro_flame_stop")}");
            Line(frames >= 8, $"{frames} squad frames at the RTS camera: {OpenZPaths.Records}");

            // the mouth, close: the squad whole again, a second pass, the camera off the dragon's side with a long lens (nearer than 6 wingspans AmbientLife shrinks a bird away from the lens)
            foreach (var o in squad) if (!o.Destroyed) o.SetHealth(o.MaxHealth);
            yield return Wait(1f);
            var cam = Camera.main; var rts = Object.FindFirstObjectByType<RtsCamera>();
            if (!cam) { Line(false, "no main camera for the close frames"); yield break; }
            yield return Pass(life, squad[0], 120f, got); int ans2 = got[0];
            Line(ans2 == 0, $"a second pass for the close frames: {(ans2 >= 0 ? ZWorld.DragonWhyName[ans2] : "no clear path")}");
            if (ans2 != 0) yield break;
            heldCam = cam; heldFov = cam.fieldOfView; heldRts = rts; if (rts) rts.enabled = false;
            int close = 0, lit = 0, ahead = 0; float nextClose = 0f;
            while (w.DragonPass != null)
            {
                if (life.ProofMouth(out var mouth, out var head, out var neck, out var fwd, out float span, out bool flame))
                {
                    float far = Mathf.Max(30f, 6.6f * span);
                    var side = Vector3.Cross(Vector3.up, fwd).normalized;
                    cam.fieldOfView = 2f * Mathf.Atan(2.4f / far) * Mathf.Rad2Deg;                          // about 5 m across at the head
                    cam.transform.position = head + side * far + Vector3.up * (far * 0.2f);
                    cam.transform.rotation = Quaternion.LookRotation(head - cam.transform.position, Vector3.up);
                    if (flame)
                    {
                        lit++; if (Vector3.Dot(mouth - neck, fwd) > 0f) ahead++;
                        if (Time.unscaledTime >= nextClose && close < 24) { nextClose = Time.unscaledTime + 0.12f; ScreenCapture.CaptureScreenshot(Shot("mouth_", close++)); }
                    }
                }
                yield return null;
            }
            Release();
            Line(lit > 0 && ahead == lit, $"the jet starts at the snout, ahead of the neck along the flight, on {ahead} of {lit} frames with the flame on (fire pose and nod)");
            Line(close >= 8, $"{close} close frames of the head with the flame on, to look at: {OpenZPaths.Records}");
            if (input) input.Selection.Clear();
        }

        // ------------------------------------------------------------------ 8
        [MenuItem("OpenZ/L07 Lava/Checks/Dragons/8 Clip stage: a dragon at a flying shuttle (Play, level 7)")]
        public static void Eight() { if (Ready()) Start("DRAGON8", EightRun()); }

        static IEnumerator EightRun()
        {
            var g = ZGame.I; var w = g.World; var life = AmbientLife.I;
            yield return Past60(g);
            OzDragonProof.NoAsks = true; OzDragonProof.Recording = true; w.DragonProofForce = true;
            var (x, y) = OzDragonCount.Open(w);
            var s = w.SpawnShuttle(x, y, g.player, 0f, out string why);
            Line(s != null, $"the testing panel's shuttle on level 7: {(s != null ? "made" : why)}");
            if (s == null) yield break;
            OzPlayableShuttle.SpawnView(g, s);
            void Follow() => Cam(ZGame.PxToWorld(s.CenterX, s.CenterY), 50f);
            Follow();
            yield return Wait(1f);
            int landed = life.ProofAskOnly(s);
            Line(landed == ZWorld.WhyTarget, $"on the ground it is no target: {ZWorld.DragonWhyName[landed]}");
            w.SendWaypoints(g.player, s, new[] { ZWaypoint.Move(x + 320, y) });
            double end = w.Time + 40;
            while (w.Time < end && !(s.ShuttleAirborne && s.ShuttleHeightM >= 11.9 && !s.IsMoving)) { Follow(); yield return null; }
            Line(s.ShuttleAirborne && s.ShuttleHeightM >= 11.9, $"it flies: {s.ShuttleHeightM:F1} m");
            int hp0 = s.Health, patches0 = OpenZPyroFire.I ? OpenZPyroFire.I.Patches : 0, frames = 0;
            var got = new int[1];
            yield return Pass(life, s, 90f, got); int ans = got[0];
            Line(ans == 0, $"a pass at the flying shuttle: {(ans >= 0 ? ZWorld.DragonWhyName[ans] : "no clear path")}");
            if (ans != 0) yield break;
            float nextShot = Time.unscaledTime;
            while (w.DragonPass != null)
            {
                Follow();
                if (Time.unscaledTime >= nextShot && frames < 16) { nextShot += 0.35f; ScreenCapture.CaptureScreenshot(Shot("ship_", frames++)); }
                yield return null;
            }
            yield return Wait(1f);
            float took = (hp0 - s.Health) / (float)s.MaxHealth;
            Line(w.DragonShipBites >= 1 && took > 0f && took <= 0.181f, $"the flame reached the hull: {w.DragonShipBites} bites, {took * 100f:F1} % of its hit points (18 at most)");
            float wide = OzDragonProof.Bites.Where(b => b.hit).Select(b => b.deg).DefaultIfEmpty(180f).Max();
            Line(wide <= 45f, $"from the mouth down onto the hull: the widest landed flame {wide:F1} degrees from straight down");
            Line(!s.Destroyed && s.Health >= ZWorld.DragonShipFloorHp(s), $"the shuttle flies on at {100f * s.Health / s.MaxHealth:F0} %: no dragon takes it under {ZWorld.DragonShipFloorPerMille / 10} %");
            Line(!OpenZPyroFire.I || OpenZPyroFire.I.Patches <= patches0, "nothing burning on the ground under it");
            Line(OzDragonScream.Ids.Sum(sc => Played(sc)) >= 1 && Played("wpn_pyro_flame_start") >= 1, $"sound: scream {OzDragonScream.Ids.Sum(sc => Played(sc))} (1.4 ptero_scream), flame start {Played("wpn_pyro_flame_start")}, stop {Played("wpn_pyro_flame_stop")}");
            lines.Add($"[{tag}] every sound since the start: {string.Join(", ", OzSoundLog.Since(t0).GroupBy(e => e.key).Select(k => k.Key + " " + k.Count()).Take(20))}");
            w.SendWaypoints(g.player, s, new[] { ZWaypoint.Move(x - 160, y) });                       // it flies on, the camera with it
            for (double end2 = w.Time + 6; w.Time < end2;) { Follow(); yield return null; }
            Line(frames >= 8, $"{frames} frames at the RTS camera: {OpenZPaths.Records}");
        }
    }
}
#endif
