#if !OZ_RELEASE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit shuttle_view_thrusters_r7 (FAILS A23): the Play check of
    /// playing (L02 or L18), the normal RTS camera, about a minute; no keyboard or mouse is used (the view is entered, turned and left by code, the
    /// ships fly by the view's own move order, PlayerInput.FpsMove). Two own shuttles: A (the viewed one) and B (another one, about 60 m behind A on
    /// the same way), both flying.
    ///  E  the entry with the glide off, one frame: the step of the frame the view begins holds A's jets; B's not
    ///     or flame streaks drawn while A's mouths are in the picture (at least 30 steps); B never held and drawn in every step its mouths are
    ///     in the picture
    ///  R  reproduction: the hold off (OzShuttleJets.EyeHold false) for 20 steps at the same look: A's jets drawn in every step with the mouths
    ///     in the picture (the owner's two cones: the cause is the one found); on again: held and 0 drawn from the next step
    ///  X  an instant leave, one frame: the step of the leave's frame not held, and drawn (the RTS camera back on A)
    ///  G  the glide in and the glide out (Esc): not held at the start, held exactly when the glide's last share passed 85 % (OzFpsView.Holds:
    ///     the previous frame's share), never let go before the end; at the eye 0 drawn; held on the leave's frame (the camera still at the
    ///     eye), let go once the share fell under 85 % and drawn from that step, never held again; after the view drawn
    /// No PASS without its samples; errors and exceptions logged during the run fail it. Report: <see cref="Folder"/>/report_shuttle_view_jets.txt.
    /// Test only: the two ships are taken out again, the switches it set (the hold, the glide) are put back; the level's bots are cleared for the
    /// run (as the other view checks do), the match goes on without them.
    /// </summary>
    public sealed class OzShuttleViewJetsPlayCheck : MonoBehaviour
    {
        public static string Report = "idle";

        public static string Folder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var k = Path.Combine(d.FullName, "work", "staging", "v1_5", "kits", "shuttle_view_thrusters_r7");
                if (Directory.Exists(k)) return Path.Combine(k, "proof", "play");
            }
            return Path.Combine(Application.persistentDataPath, "shuttle_view_thrusters_play_check");
        }

        public static void Run() { Report = "running"; new GameObject("OzShuttleViewJetsPlayCheck").AddComponent<OzShuttleViewJetsPlayCheck>(); }

        /// <summary>The far point a ship is sent to (px) and the place of B behind A (px).</summary>
        const int LeadPx = 3200, BehindPx = 480;
        const float Hold = OzFpsFeel.LampHoldFrom;

        ZGame g; ZWorld W; RtsCamera rts; PlayerInput pi; Camera cam; Team me; int pass, fail, errors;
        readonly List<string> errorLines = new List<string>();
        readonly StringBuilder log = new StringBuilder();
        readonly List<ZObject> made = new List<ZObject>();
        bool glide0 = true;

        struct Rec { public int frame, draws; public bool held, on; public float glide; }

        void Start() { glide0 = OzFpsFeel.GlideOn; Application.logMessageReceived += OnLog; StartCoroutine(All()); }
        void OnDestroy() { Application.logMessageReceived -= OnLog; OzShuttleJets.EyeHold = true; OzFpsFeel.GlideOn = glide0; }
        void OnLog(string msg, string st, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) { errors++; if (errorLines.Count < 6) errorLines.Add(msg.Split('\n')[0]); } }

        void Log(string s) { log.AppendLine(s); Debug.Log("[OpenZ shuttle view jets check] " + s); }
        void Check(string what, bool ok) { if (ok) pass++; else fail++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < s) yield return null; }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator WaitFor(System.Func<bool> c, float max) { float t0 = Time.unscaledTime; while (Time.unscaledTime - t0 < max && !c()) yield return null; }
        static Rec Read(OzShuttleJets j) => new Rec { frame = j.LastStepFrame, draws = j.LastDraws, held = j.Held, on = j.LastOnScreen, glide = OzFpsFeel.GlideS };

        // ------------------------------------------------------------------ places and ships
        UnitView ViewOf(ZObject u) => u != null && g.Views.TryGetValue(u.Id, out var v) ? v : null;
        OzShuttleJets JetsOf(ZObject u) { var ps = OzPlayableShuttle.Of(u); return ps ? ps.Jets : null; }
        void Select(ZObject u) { pi.Selection.Clear(); if (u != null) pi.Selection.Add(u); }

        ZObject SpawnAt(int x, int y, float heading)
        {
            var q = W.ShuttleClamp(x, y);
            var s = W.SpawnShuttle(q.x, q.y, me, heading, out _);
            if (s != null) { made.Add(s); OzPlayableShuttle.SpawnView(g, s); }
            return s;
        }

        ZObject SpawnNear(int cx, int cy, int r0, int r1, float heading)
        {
            for (int r = r0; r <= r1; r += 48)
                for (int a = 0; a < 360; a += 30)
                {
                    var s = SpawnAt(cx + (int)(Mathf.Cos(a * Mathf.Deg2Rad) * r), cy + (int)(Mathf.Sin(a * Mathf.Deg2Rad) * r), heading);
                    if (s != null) return s;
                }
            return null;
        }

        /// <summary>The way on the map (px, unit length): from (x, y) toward the map's middle (along +x when it stands there).</summary>
        Vector2 Inward(int x, int y)
        {
            var d = new Vector2(rts.mapSizeMeters.x * 4f - x, rts.mapSizeMeters.y * 4f - y);
            return d.sqrMagnitude > 1f ? d.normalized : Vector2.right;
        }

        /// <summary>The ship's move order to the far point along d (the view's own order, PlayerInput.FpsMove); shorter leads when refused.</summary>
        bool Send(ZObject u, Vector2 d)
        {
            foreach (int lead in new[] { LeadPx, 2400, 1600 })
            {
                var q = W.ShuttleClamp(u.CenterX + Mathf.RoundToInt(d.x * lead), u.CenterY + Mathf.RoundToInt(d.y * lead));
                if (pi.FpsMove(u, q.x, q.y)) return true;
            }
            return false;
        }

        void Drop(ZObject o)
        {
            if (o == null) return;
            pi.Selection.RemoveAll(s => s != null && s.Id == o.Id);
            W.RemoveNow(o);
            if (g.Views.TryGetValue(o.Id, out var v)) { g.Views.Remove(o.Id); if (v) { v.gameObject.SetActive(false); Destroy(v.gameObject); } }
        }

        // ------------------------------------------------------------------ the run
        IEnumerator All()
        {
            g = ZGame.I; W = g ? g.World : null; pi = FindFirstObjectByType<PlayerInput>(); rts = pi ? pi.cam : null; cam = rts ? rts.GetComponent<Camera>() : null;
            if (W == null || !rts || !pi || !cam) { Log("no level in Play"); fail++; Finish(); yield break; }
            Directory.CreateDirectory(Folder());
            me = g.player;
            yield return WaitFor(() => OpenZHud.ControlAt > 0f && Time.unscaledTime >= OpenZHud.ControlAt + 1f, 40f);
            Log($"SHUTTLE VIEW JETS CHECK level {g.level}, shuttle rule {ZWorld.ShuttlePlayableDeviation}, hold switch {OzShuttleJets.EyeHold}, glide {OzFpsFeel.GlideOn}, graphics level {OzQuality.Level}");
            if (!ZWorld.ShuttlePlayableDeviation) { Check("the shuttle rule is on ('Zod exact' is off): no shuttle without it", false); Finish(); yield break; }
            W.Bots.Clear();
            if (OzFpsView.On && OzFpsView.I) { OzFpsView.I.Leave("the check starts"); yield return WaitFor(() => !OzFpsView.On, 3f); }
            yield return Frames(3);
            yield return Rows();
            if (OzFpsView.On && OzFpsView.I) { OzFpsView.I.Leave("the check ends"); yield return WaitFor(() => !OzFpsView.On, 3f); }
            foreach (var o in made.ToList()) Drop(o);
            made.Clear();
            OzShuttleJets.EyeHold = true; OzFpsFeel.GlideOn = glide0;
            Log($"counters: held steps {OzShuttleJets.EyeHeldSteps}, of them on screen {OzShuttleJets.EyeHeldOnScreen}");
            if (errors > 0) Check($"no error or exception logged during the run ({errors}: {string.Join(" | ", errorLines)})", false);
            Finish();
        }

        IEnumerator Rows()
        {
            // ---- set-up: A near the camera, B behind it on the same way, both sent far
            var c = rts.focus; int cx = Mathf.RoundToInt(c.x * 8f), cy = Mathf.RoundToInt(-c.z * 8f);
            float heading = rts.yaw - 90f;
            var A = SpawnNear(cx, cy, 120, 900, heading);
            if (A == null) { Check("set-up: shuttle A placed on open ground near the camera", false); yield break; }
            var way = Inward(A.CenterX, A.CenterY);
            ZObject B = null;
            for (int k = 0; k < 4 && B == null; k++) B = SpawnAt(A.CenterX - Mathf.RoundToInt(way.x * (BehindPx + 96 * k)), A.CenterY - Mathf.RoundToInt(way.y * (BehindPx + 96 * k)), heading);
            if (B == null) B = SpawnNear(A.CenterX, A.CenterY, 400, 1000, heading);
            yield return Real(1f);
            var vA = ViewOf(A); var jA = JetsOf(A); var jB = JetsOf(B);
            Check($"set-up: shuttle A #{A.Id} (pilot {A.Drivers.Count}) and B #{(B != null ? B.Id : -1)} with their views and jets", vA && jA != null && A.Drivers.Count > 0 && B != null && jB != null);
            if (!vA || jA == null || B == null || jB == null) yield break;
            Check("set-up: both ordered far along the map (the view's own move order)", Send(A, way) & Send(B, way));
            yield return WaitFor(() => A.ShuttleState == ZShuttleState.Cruise && A.ShuttleHeightM >= 8.0 && B.ShuttleAirborne, 14f);
            yield return Real(1f);
            Check($"set-up: A cruises ({A.ShuttleState}, {A.ShuttleHeightM:0.0} m, throttle {jA.Throttle:0.00}), B up ({B.ShuttleState}, {B.ShuttleHeightM:0.0} m)", A.ShuttleState == ZShuttleState.Cruise && jA.Throttle >= 0.7f && B.ShuttleAirborne);

            // ---- E: the entry with the glide off, one frame
            OzFpsFeel.GlideOn = false;
            Select(A);
            int fe = Time.frameCount;
            bool inOk = OzFpsView.TryEnter(pi);
            yield return null;
            OzFpsFeel.GlideOn = glide0;
            Check($"E: the view entered on A ({(inOk ? "in" : "refused: " + OzFpsView.LastRefusal)})", inOk && OzFpsView.On && OzFpsView.I && OzFpsView.I.Unit == A);
            if (!inOk || !OzFpsView.I) yield break;
            Check($"E: the step of the entry's frame {fe} holds A's jets (step {jA.LastStepFrame}, held {jA.Held}, drawn {jA.LastDraws}), not B's (held {jB.Held})", jA.LastStepFrame == fe && jA.Held && jA.LastDraws == 0 && !jB.Held);

            // ---- H: at the eye, the look back over A's tail (the owner's angle)
            float k0 = Mathf.Max(1e-4f, RtsCamera.FreeLookSpeed);
            OzFpsView.I.LookBy(new Vector2(180f / k0, -14f / k0));
            yield return Frames(3);
            int steps = 0, held = 0, on = 0, drawnHeld = 0, bSteps = 0, bHeld = 0, bOn = 0, bDrawn = 0, lastA = jA.LastStepFrame, lastB = jB.LastStepFrame;
            for (int i = 0; i < 120; i++)
            {
                yield return null;
                if (jA.LastStepFrame != lastA) { lastA = jA.LastStepFrame; steps++; if (jA.Held) held++; if (jA.LastOnScreen) on++; if (jA.Held && jA.LastDraws > 0) drawnHeld++; }
                if (jB.LastStepFrame != lastB) { lastB = jB.LastStepFrame; bSteps++; if (jB.Held) bHeld++; if (jB.LastOnScreen) { bOn++; if (jB.LastDraws > 0) bDrawn++; } }
            }
            Check($"H: looking back over A's tail, A flying (throttle {jA.Throttle:0.00}): held in {held} of {steps} steps, its mouths in the picture in {on}, flames / halos / heat / fire drawn in {drawnHeld} (want held every step, in the picture in 30 or more, 0 drawn)",
                  steps >= 60 && held == steps && on >= 30 && drawnHeld == 0);
            if (bOn > 0) Check($"H: B (another shuttle) never held ({bHeld} of {bSteps} steps), drawn in {bDrawn} of the {bOn} steps its mouths were in the picture", bHeld == 0 && bDrawn == bOn);
            else { Check($"H: B (another shuttle) never held ({bHeld} of {bSteps} steps)", bSteps > 0 && bHeld == 0); Log("INFO H: B's mouths were not in the picture (its draws not counted here)"); }

            // ---- R: the hold off at the same look (the cause), then on again
            OzShuttleJets.EyeHold = false;
            int rSteps = 0, rOn = 0, rDrawn = 0; lastA = jA.LastStepFrame;
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                if (jA.LastStepFrame == lastA) continue;
                lastA = jA.LastStepFrame; rSteps++;
                if (jA.LastOnScreen) { rOn++; if (!jA.Held && jA.LastDraws > 0) rDrawn++; }
            }
            int fr = Time.frameCount; OzShuttleJets.EyeHold = true;
            yield return null;
            Check($"R: the hold off: A's flames drawn in {rDrawn} of the {rOn} steps its mouths were in the picture ({rSteps} steps): REPRODUCED, the owner's two cones (want all, 10 or more)", rOn >= 10 && rDrawn == rOn);
            Check($"R: the hold on again: the next step (frame {fr}) held, 0 drawn (step {jA.LastStepFrame}, held {jA.Held}, drawn {jA.LastDraws})", jA.LastStepFrame == fr && jA.Held && jA.LastDraws == 0);

            // ---- X: an instant leave, one frame
            int fx0 = Time.frameCount;
            OzFpsView.I.Leave("the jets check: an instant leave");
            yield return null;
            var x = Read(jA);
            if (x.on) Check($"X: the step of the leave's frame {fx0} lets go and draws A's jets (step {x.frame}, held {x.held}, drawn {x.draws})", x.frame == fx0 && !x.held && x.draws > 0 && !OzFpsView.On);
            else { Check($"X: the step of the leave's frame {fx0} lets go (step {x.frame}, held {x.held})", x.frame == fx0 && !x.held && !OzFpsView.On); Log("INFO X: A's mouths not in the RTS camera's picture on that frame (drawn checked in G)"); }
            yield return Frames(5);

            // ---- G: the glide in and the glide out
            if (A.ShuttleState != ZShuttleState.Cruise) { Send(A, Inward(A.CenterX, A.CenterY)); yield return WaitFor(() => A.ShuttleState == ZShuttleState.Cruise, 12f); }
            Select(A);
            int fg = Time.frameCount;
            if (!OzFpsView.TryEnter(pi)) { Check($"G: the view entered with the glide ({OzFpsView.LastRefusal})", false); yield break; }
            var gin = new List<Rec>(); lastA = -1;
            float t0 = Time.unscaledTime; int after = 0;
            while (Time.unscaledTime - t0 < 3f && after < 3)
            {
                yield return null;
                if (jA.LastStepFrame != lastA && jA.LastStepFrame >= fg) { lastA = jA.LastStepFrame; gin.Add(Read(jA)); }
                if (!OzFpsFeel.Gliding) after++;
            }
            yield return GlideRows("G in", gin, true);
            OzFpsView.I.LookBy(new Vector2(180f / k0, -14f / k0));
            yield return Frames(3);
            steps = held = on = drawnHeld = 0; lastA = jA.LastStepFrame;
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                if (jA.LastStepFrame == lastA) continue;
                lastA = jA.LastStepFrame; steps++; if (jA.Held) held++; if (jA.LastOnScreen) on++; if (jA.Held && jA.LastDraws > 0) drawnHeld++;
            }
            Check($"G: at the eye after the glide in, looking back: held {held} of {steps} steps, in the picture {on}, drawn {drawnHeld}", steps >= 15 && held == steps && drawnHeld == 0);
            int fo = Time.frameCount;
            OzFpsView.I.Leave("Esc", true);
            var gout = new List<Rec>(); lastA = -1; t0 = Time.unscaledTime; after = 0;
            while (Time.unscaledTime - t0 < 3f && after < 4)
            {
                yield return null;
                if (jA.LastStepFrame != lastA && jA.LastStepFrame >= fo) { lastA = jA.LastStepFrame; gout.Add(Read(jA)); }
                if (!OzFpsView.On) after++;
            }
            Check($"G out: the step of the leave's frame {fo} still held (the camera at the eye; step {(gout.Count > 0 ? gout[0].frame : -1)}, held {(gout.Count > 0 && gout[0].held)})", gout.Count > 0 && gout[0].frame == fo && gout[0].held);
            yield return GlideRows("G out", gout, false);
            int back = gout.Count(r => r.on), backDrawn = gout.Count(r => r.on && !r.held && r.draws > 0), backNot = gout.Count(r => !r.held);
            var lastRec = gout.Count > 0 ? gout[gout.Count - 1] : default;
            Check($"G out: after the view not held and drawn (last step held {lastRec.held}, in the picture {lastRec.on}, drawn {lastRec.draws}); steps let go {backNot}, of them in the picture {gout.Count(r => !r.held && r.on)}, drawn {backDrawn}",
                  gout.Count > 0 && !lastRec.held && (!lastRec.on || lastRec.draws > 0) && gout.Count(r => !r.held && r.on) == backDrawn && backDrawn > 0);
            Log($"INFO G out: {back} of {gout.Count} steps with A's mouths in the picture");
        }

        /// <summary>The glide rows: the hold follows the view's test exactly (held = the glide's share at the end of the frame before >= 85 %),
        /// one switch only (in: off to on; out: on to off), the first step after the switch drawn (out, in the picture), none drawn while held.</summary>
        IEnumerator GlideRows(string tag, List<Rec> r, bool inward)
        {
            int bad = 0, switches = 0, firstChange = -1, drawnHeld = 0;
            for (int i = 0; i < r.Count; i++)
            {
                if (r[i].held && r[i].draws > 0) drawnHeld++;
                if (i == 0) continue;
                if (r[i].held != r[i - 1].held) { switches++; if (firstChange < 0) firstChange = i; }
                if (r[i].frame == r[i - 1].frame + 1 && r[i - 1].glide > 0f && r[i - 1].glide < 1f)
                {   // a step during the glide: its hold came from the share the frame before ended with
                    bool want = r[i - 1].glide >= Hold;
                    if (r[i].held != want) bad++;
                }
            }
            var sb = new StringBuilder();
            for (int i = 0; i < r.Count && i < 60; i++) sb.Append($"{(i > 0 ? " " : "")}{r[i].frame}:{r[i].glide:0.00}{(r[i].held ? "H" : "-")}{(r[i].on ? r[i].draws.ToString() : "x")}");
            Log($"INFO {tag}: frame:share(end of frame) H=held, then draws (x = not in the picture): {sb}");
            if (inward)
                Check($"{tag}: not held at the start (the camera at the RTS pose), held once the share passed {Hold:0.00}, one switch ({switches}), the view's test kept in every glide step ({bad} off), held at the end, 0 drawn while held ({drawnHeld})",
                      r.Count > 3 && !r[0].held && r[r.Count - 1].held && switches == 1 && bad == 0 && drawnHeld == 0);
            else
            {
                bool firstDrawn = firstChange > 0 && (!r[firstChange].on || r[firstChange].draws > 0);
                Check($"{tag}: let go once the share fell under {Hold:0.00}, one switch ({switches}), the view's test kept in every glide step ({bad} off), the first step let go drawn ({(firstChange > 0 ? (r[firstChange].on ? r[firstChange].draws + " draws" : "not in the picture") : "none")}), 0 drawn while held ({drawnHeld})",
                      r.Count > 3 && r[0].held && !r[r.Count - 1].held && switches == 1 && bad == 0 && firstDrawn && drawnHeld == 0);
            }
            yield break;
        }

        void Finish()
        {
            string head = $"{(fail == 0 && pass > 0 ? "PASS" : "FAIL")}: {pass} passed, {fail} failed";
            Report = head;
            log.Insert(0, head + "\n");
            try { Directory.CreateDirectory(Folder()); File.WriteAllText(Path.Combine(Folder(), "report_shuttle_view_jets.txt"), log.ToString()); } catch (IOException) { }
            Debug.Log("[OpenZ shuttle view jets check] " + head);
            Destroy(gameObject);
        }
    }
}
#endif
