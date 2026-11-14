#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.3 item 15 Play check (View/OzShuttleBreakup.Tumble.cs): menu OpenZ/V1.3/Wrecks: Item 15 Shuttle Tumble Watch, started in Play BEFORE
    /// the shuttle kills (the K256e break-up proof's K run makes four: a rocket salvo, a howitzer shell, the fort's gun, rifle fire; or any
    /// shuttle shot down by hand). It lives across the level restarts. For every break-up: a strip of 10 full-screen frames 0.15 s apart from
    /// the break (the camera as it is: the normal RTS camera), then per piece its time in the air, the degrees it turned there and its fastest
    /// spin; once all rest, the rest checks of the break-up as before (pieces resting floating / sunk, pieces inside each other).
    ///  T1 every piece spins: it turned at least <see cref="MinTurnDeg"/> degrees in the air, or (a piece that was down within 0.4 s, a
    ///     ground death) its spin reached <see cref="MinPeak"/> rad/s;
    ///  T2 the rest as before: 0 bad rests (OzShuttleBreakup.BadRests), 0 pairs inside each other (ThroughEachOther), all rested in their
    ///     wreck time;
    ///  T3 (1.3 item 15b, the air hold) no late jump: no piece pushed out of another at OzShuttleBreakup.PushAt s (PushedOut +0); with it the
    ///     air hold's counts (pairs held at the break beyond the deep ones, let go at a landing, let collide while resting).
    /// After <see cref="Breaks"/> break-ups (or the menu again) it writes the report. Output: <see cref="Folder"/>. Test only.
    /// </summary>
    public sealed class OzShuttleTumbleWatch : MonoBehaviour
    {
        public static int Breaks = 3; public static float MinTurnDeg = 90f, MinPeak = 2f;
        public static string Report = "idle";
        public static string Folder => OzRelease.Milestones("v13_item15_shuttle_tumble");
        static OzShuttleTumbleWatch run;
        public static void Toggle()
        {
            if (run) { run.Finish(); return; }
            Report = "running";
            var go = new GameObject("OzShuttleTumbleWatch"); DontDestroyOnLoad(go); run = go.AddComponent<OzShuttleTumbleWatch>();
        }

        readonly StringBuilder sb = new StringBuilder();
        int seenBreaks, judged, pass, fail; bool busy, done;
        int heldMark, downMark, forcedMark, pushMark;                                                          // 1.3 item 15b: the counts just before a break
        void Mark() { heldMark = OzShuttleBreakup.TumbleHeldPairs; downMark = OzShuttleBreakup.TumbleLetGoDown; forcedMark = OzShuttleBreakup.ForcedPairs; pushMark = OzShuttleBreakup.PushedOut; }
        void Line(string t) { sb.AppendLine(t); Debug.Log("[OzShuttleTumbleWatch] " + t); }

        void Start() { seenBreaks = OzShuttleBreakup.Breaks; Mark(); Directory.CreateDirectory(Folder); Line($"watch started; tumble rule {(OzShuttleBreakup.TumbleOn ? "on" : "off")} (min spin {OzShuttleBreakup.TumbleMinSpin:0.0} rad/s, air damping {OzShuttleBreakup.AirAngularDamping:0.00}), air hold {(OzShuttleBreakup.TumbleHold ? "on" : "off")}"); }

        void Update()
        {
            if (done || busy) return;
            if (OzShuttleBreakup.Breaks == seenBreaks) { Mark(); return; }
            seenBreaks = OzShuttleBreakup.Breaks;
            StartCoroutine(Judge(seenBreaks, new List<OzShuttleBreakup>(OzShuttleBreakup.LastPieces), OzShuttleBreakup.LastHow, heldMark, downMark, forcedMark, pushMark));
        }

        IEnumerator Judge(int n, List<OzShuttleBreakup> pieces, string how, int held0, int down0, int forced0, int push0)
        {
            busy = true;
            for (int i = 0; i < 10; i++) { ScreenCapture.CaptureScreenshot(Path.Combine(Folder, $"break{n}_{how}_{i:00}.png")); yield return new WaitForSeconds(0.15f); }
            float t = 0f;
            while (t < 10f && pieces.Any(p => p && !p.Touched)) { t += Time.deltaTime; yield return null; }
            var rows = pieces.Where(p => p).Select(p => (p.PieceName, p.Kind, p.MassKg, p.AirSeconds, p.AirTurnDeg, p.AirPeakSpin)).ToList();
            int spinning = rows.Count(r => r.AirTurnDeg >= MinTurnDeg || (r.AirSeconds < 0.55f && r.AirPeakSpin >= MinPeak));
            float wait = 0f;
            while (wait < OzShuttleBreakup.WreckSeconds && !OzShuttleBreakup.LastAllRested()) { wait += Time.deltaTime; yield return null; }
            bool rested = OzShuttleBreakup.LastAllRested();
            int bad = OzShuttleBreakup.BadRests(out string badWhich), inside = OzShuttleBreakup.ThroughEachOther(out float worst, out string insideWhich);
            bool t1 = rows.Count > 0 && spinning == rows.Count, t2 = rested && bad == 0 && inside == 0;
            Line($"break-up {n} ({how}): {rows.Count} pieces, spinning {spinning}/{rows.Count}; rested {rested}, bad rests {bad} {(bad > 0 ? badWhich : "")}, inside each other {inside} {(inside > 0 ? insideWhich + " " + worst.ToString("0.000") + " m" : "")}");
            foreach (var r in rows.OrderByDescending(r => r.MassKg))
                Line($"    {r.PieceName} ({r.Kind}, {r.MassKg:0} kg): {r.AirSeconds:0.00} s in the air, turned {r.AirTurnDeg:0} deg, fastest {r.AirPeakSpin:0.0} rad/s{(r.AirTurnDeg >= MinTurnDeg || (r.AirSeconds < 0.55f && r.AirPeakSpin >= MinPeak) ? "" : "  NOT SPINNING")}");
            Line((t1 ? "PASS" : "FAIL") + $" T1 break-up {n}: every piece spins ({spinning}/{rows.Count})");
            Line((t2 ? "PASS" : "FAIL") + $" T2 break-up {n}: the rest as before (rested, 0 bad rests, 0 inside each other)");
            int pushed = OzShuttleBreakup.PushedOut - push0; bool t3 = pushed == 0;                              // 1.3 item 15b: the air hold
            Line($"    air hold {(OzShuttleBreakup.TumbleHold ? "on" : "off")}: {OzShuttleBreakup.TumbleHeldPairs - held0} pairs held at the break beyond the deep ones, {OzShuttleBreakup.TumbleLetGoDown - down0} let go at a landing, {OzShuttleBreakup.ForcedPairs - forced0} let collide while resting, {pushed} pushed out at {OzShuttleBreakup.PushAt:0} s");
            Line((t3 ? "PASS" : "FAIL") + $" T3 break-up {n}: no late jump (no piece pushed out of another at {OzShuttleBreakup.PushAt:0} s)");
            pass += (t1 ? 1 : 0) + (t2 ? 1 : 0) + (t3 ? 1 : 0); fail += (t1 ? 0 : 1) + (t2 ? 0 : 1) + (t3 ? 0 : 1);
            Mark();
            judged++; busy = false;
            if (judged >= Breaks) Finish();
        }

        void Finish()
        {
            if (done) return; done = true;
            Report = $"{pass} PASS, {fail} FAIL over {judged} break-ups (raised launches {OzShuttleBreakup.TumbleRaised}, touchdowns {OzShuttleBreakup.Touchdowns})";
            Line(Report);
            File.WriteAllText(Path.Combine(Folder, "tumble_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"), sb.ToString());
            if (run == this) run = null;
            Destroy(gameObject);
        }
    }
}
#endif
