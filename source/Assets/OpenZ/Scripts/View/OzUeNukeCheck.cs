#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.3 item 2 (editor only): the counted Play check of the Unit editor's NUKE, menu OpenZ/V1.3/Unit Editor/2 NUKE Check (Play, the unit
    /// editor open) (Editor/OzUeNukeMenu.cs). Through the editor's own calls (no mouse events), the field cleared before and after, the play
    /// state and the speed put back:
    ///  N1 the palette and the dock: NUKE is the fourth extra (FLAG, OBSTACLE, GRENADES, NUKE), named NUKE / NUKE STRIKE, always neutral, one
    ///     per drop, no unit of the cap; its tile stands in the dock right of GRENADES with its 256 x 256 picture;
    ///  N2 a field with a red squad, a blue squad, a red light tank, an empty jeep, a grenade crate and a flag INSIDE the kill ring (30 m since
    ///     1.3 final, was 15 m), an empty light tank and an empty gatling in the outer band (37.5 m: damaged, not destroyed), and an empty jeep
    ///     and an empty gatling OUTSIDE it (55 m out, past the band and the reach of the crate's flying grenades);
    ///  N3 the call: accepted ("NUKE INBOUND"), the levels' strike flies (OpenZNukeStrike, its playground form), it is not the fort strike
    ///     (Active false, LastImpactTime unchanged);
    ///  N4 a second call at once: refused with "NUKE READY IN n S", n 29 or 30; the dock tile shows the curtain and the seconds;
    ///  N5 the impact (in about 3.7 s): every object with HP inside the ring destroyed (both teams, the tank, the empty jeep, the crate),
    ///     the flag still standing (immune), both outside objects untouched at full HP; no slow motion (time scale 1), no end screen, no game
    ///     over, LastImpactTime unchanged;
    ///  N6 the strike's sounds in the sound log: the launch alarm, the engine, the bomb release, the whistle, the long blast;
    ///  N7 at x4: the cooldown is 30 s of watching (game time), not 30 / 4: ready after 29.5 .. 30.5 s, one ui_unit_ready, a call accepted.
    ///     drop point (OpenZFortShockwave.Fire through NukeFx); robots inside the wave's kill ring / robots thrown: every one thrown, every
    ///     throw pointing away from the bomb (HitReactions.ImpulseChecked), 0 toward it; the hardware outside is only jolted (INFO).
    ///     the levels' ring (ZWorld.NukeWaveRadiusM / NukeWaveTimeAt = OpenZFortShockwave.RadiusAt and its inverse); a row of objects 2 to
    ///     14 m out and a jeep 19 m out, the field PAUSED at the impact: nothing dies and the wave waits while paused; then, played on at
    ///     x1: every object of the ring destroyed by the wave, each at most 2 rule ticks after the ring reached its distance, none before
    ///     it, nearest first, spread over several ticks (not all at the impact); on screen no robot thrown before
    ///     the ring reached it; the jeep outside untouched;
    ///  N10 CLEAR FIELD at the impact: the rest of the wave dropped, a squad placed right after it at 3 m untouched;
    ///  N7 also checks the wave's timing at x4 (the same ring, 4 rule seconds a second of the ring: at most 2 rule ticks behind it).
    /// Runs about 70 s. Folder (optional, set before Begin): report.txt, the sound log and frames/ (one JPG every 0.4 s from the call to
    /// 4 s after the impact: the frame sheet of one drop, from the plane to the last landing) go there.
    /// </summary>
    public sealed class OzUeNukeCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ NukeCheck]";
        public static string Report = "not run";
        public static string Folder;
        public static bool Running => run;
        static OzUeNukeCheck run;
        int pass, fail;
        readonly StringBuilder lines = new StringBuilder();

        public static void Begin()
        {
            if (run) { Debug.LogWarning(Tag + " a run is still going"); return; }
            if (!Application.isPlaying || !OzUnitEditor.UiBuilt || OzUnitEditor.I == null || OzUnitEditor.I.World == null) { Debug.LogWarning(Tag + " enter Play and open the unit editor first"); return; }
            var go = new GameObject("OzUe_NukeCheck"); DontDestroyOnLoad(go);
            run = go.AddComponent<OzUeNukeCheck>();
        }

        void Start() { StartCoroutine(Main()); }
        void OnDestroy() { if (onImpulse != null) HitReactions.ImpulseChecked -= onImpulse; onImpulse = null; OzUeNuke.AfterImpact = null; if (run == this) run = null; }
        System.Action<ZObject, string, Vector3, Vector3, Vector3, Vector3> onImpulse;
        bool framing; int framesSaved;

        /// <summary>N8's frame sheet: one full-screen JPG every `every` s while `framing` is on (Folder/frames/f_000.jpg ...).</summary>
        IEnumerator FrameSheet(string dir, float every)
        {
            bool made = true;
            try { Directory.CreateDirectory(dir); } catch (System.Exception e) { Debug.LogWarning(Tag + " frames folder: " + e.Message); made = false; }
            if (!made) yield break;
            float next = 0f;
            while (framing)
            {
                yield return new WaitForEndOfFrame();
                if (Time.realtimeSinceStartup < next) continue;
                next = Time.realtimeSinceStartup + every;
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                try { File.WriteAllBytes(Path.Combine(dir, "f_" + framesSaved.ToString("000") + ".jpg"), tex.EncodeToJPG(85)); framesSaved++; }
                catch (System.Exception e) { Debug.LogWarning(Tag + " frame not written: " + e.Message); }
                Destroy(tex);
            }
        }

        void Check(bool ok, string what) { if (ok) pass++; else fail++; lines.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        void Info(string what) { lines.Append("INFO  ").AppendLine(what); }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator Seconds(float s) { float t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < s) yield return null; }
        static ZObject Lead(UeDrop d) => d.ok && d.made != null && d.made.Count > 0 ? d.made[0] : null;

        static Vector3 Focus(OzUnitEditor ed)
        {
            Vector3 f = ed && ed.Rts ? ed.Rts.focus : new Vector3(160, 0, -100);
            var map = ed && ed.World != null ? ed.World.Map : null;
            if (map != null) { f.x = Mathf.Clamp(f.x, 40f, map.width * 2f - 40f); f.z = Mathf.Clamp(f.z, -(map.height * 2f - 40f), -40f); }
            f.y = 0f; return f;
        }

        /// <summary>Every object of a drop: the leaders and their squads' members.</summary>
        static void AddAll(List<ZObject> into, UeDrop d)
        {
            if (!d.ok || d.made == null) return;
            foreach (var o in d.made) { into.Add(o); foreach (var m in o.Minions) if (m != null) into.Add(m); }
        }

        static int Played(List<OzSoundLog.Entry> es, string name)
        {
            int n = 0;
            foreach (var e in es)
                if (e.outcome != null && !e.outcome.StartsWith("SKIP", System.StringComparison.Ordinal)
                    && ((e.clip != null && e.clip.Contains(name)) || e.key == name)) n++;
            return n;
        }

        static ZWorld.PlaygroundNukeWave Wave(ZWorld w, int number)
        {
            foreach (var wv in w.PlaygroundNukeWaves) if (wv.Number == number) return wv;
            return null;
        }

        /// <summary>N9: the time (s of the ring's clock) when the levels' ring (OpenZFortShockwave.RadiusAt, the View's own curve) reaches
        /// distM, by bisection (0 inside the 4 m it starts at).</summary>
        static double RingTimeAt(double distM)
        {
            if (OpenZFortShockwave.RadiusAt(0f) >= distM) return 0.0;
            double lo = 0.0, hi = 0.05;
            while (OpenZFortShockwave.RadiusAt((float)hi) < distM && hi < 60.0) hi *= 2.0;
            for (int i = 0; i < 60; i++) { double mid = (lo + hi) * 0.5; if (OpenZFortShockwave.RadiusAt((float)mid) >= distM) hi = mid; else lo = mid; }
            return hi;
        }

        /// <summary>N9: the rules' curve against the View's ring: the radius over 0..3 s and the time to every 1/8 m over 0..30 m.</summary>
        void CurveCheck()
        {
            double maxR = 0.0, maxT = 0.0;
            for (int i = 0; i <= 600; i++) { float t = i * 0.005f; maxR = System.Math.Max(maxR, System.Math.Abs(ZWorld.NukeWaveRadiusM(t) - OpenZFortShockwave.RadiusAt(t))); }
            for (int i = 0; i <= 240; i++) { double d = i * 0.125; maxT = System.Math.Max(maxT, System.Math.Abs(ZWorld.NukeWaveTimeAt(d) - RingTimeAt(d))); }
            Check(maxR < 0.01 && maxT < 0.0005,
                  "N9 the rules' wave runs on the levels' ring (OpenZFortShockwave.RadiusAt): the radius off by at most " + maxR.ToString("0.0000") + " m over 0..3 s, the time to a distance off by at most "
                  + (maxT * 1000.0).ToString("0.000") + " ms over 0..30 m (15 m reached " + (ZWorld.NukeWaveTimeAt(15.0) * 1000.0).ToString("0") + " ms after the blast)");
        }

        /// <summary>N9 / N7: the deaths of wave `number`: each one at most 2 rule ticks after the levels' ring reached its distance at that
        /// tick, never before it; nearest first; spread over several ticks; every object alive in the ring at the impact destroyed, nothing
        /// beyond the ring reached.</summary>
        void WaveTiming(ZWorld W, int number, int insideAtImpact, string label)
        {
            var wave = Wave(W, number);
            if (wave == null) { Check(false, label + " the rules' wave " + number + " is missing"); return; }
            var hits = new List<ZWorld.PlaygroundNukeHit>();
            foreach (var h in W.PlaygroundNukeHits) if (h.Wave == number) hits.Add(h);
            hits.Sort((a, b) => a.AfterSec != b.AfterSec ? a.AfterSec.CompareTo(b.AfterSec) : a.DistPx.CompareTo(b.DistPx));
            double tick = ZConst.TICK, pace = wave.Pace, maxLag = -1.0, minLag = double.MaxValue, lastTime = -1.0, earlierMax = -1.0, soFar = -1.0;
            int killed = 0, early = 0, late = 0, beyond = 0, disorder = 0;
            var sb = new StringBuilder();
            foreach (var h in hits)
            {
                double dM = h.DistPx / 8.0, due = RingTimeAt(dM) * pace, lag = h.AfterSec - due;                // rule seconds: the ring's time x the pace
                if (h.Killed) killed++;
                if (h.DistPx >= wave.RadiusPx) beyond++;
                if (lag < -1e-6) early++;
                if (lag > 2.0 * tick + 1e-6) late++;
                maxLag = System.Math.Max(maxLag, lag); minLag = System.Math.Min(minLag, lag);
                if (h.AfterSec > lastTime + 1e-9) { earlierMax = soFar; lastTime = h.AfterSec; }
                if (earlierMax >= 0.0 && dM < earlierMax - 0.5) disorder++;                                      // 0.5 m: a unit that stepped in the 10 ms
                soFar = System.Math.Max(soFar, dM);
                sb.Append("\n      ").Append(h.Type).Append('#').Append(h.Id).Append(" at ").Append(dM.ToString("0.00")).Append(" m: ")
                  .Append(h.Killed ? "destroyed " : "reached ").Append((h.AfterSec * 1000.0).ToString("0")).Append(" ms of rules after the impact, the ring there at ")
                  .Append((due * 1000.0).ToString("0.0")).Append(" ms: ").Append((lag / tick).ToString("0.00")).Append(" tick(s) after it");
            }
            if (hits.Count == 0) minLag = 0.0;
            Check(hits.Count > 0 && killed >= insideAtImpact && beyond == 0,
                  label + " the wave (" + wave.Steps + " rule ticks at x" + pace.ToString("0.##") + ") destroyed " + killed + " of the " + insideAtImpact + " object(s) alive in the ring at the impact (" + hits.Count + " reached, "
                  + beyond + " beyond " + (wave.RadiusPx / 8.0).ToString("0") + " m; want all, 0 beyond)");
            Check(hits.Count > 0 && early == 0, label + " no death before the ring reached its distance: " + early + " early (want 0)");
            Check(hits.Count > 0 && late == 0,
                  label + " each death at most 2 rule ticks after the ring reached its distance: " + late + " late (want 0); " + (minLag / tick).ToString("0.00") + " to " + (maxLag / tick).ToString("0.00") + " tick(s)");
            Check(hits.Count > 0 && disorder == 0, label + " the deaths go out by distance, nearest first: " + disorder + " out of order (want 0)");
            double first = hits.Count > 0 ? hits[0].AfterSec : 0.0, last = hits.Count > 0 ? hits[hits.Count - 1].AfterSec : 0.0;
            Check(hits.Count > 1 && last - first >= tick - 1e-9,
                  label + " not all at the impact: the deaths spread over " + ((last - first) / tick).ToString("0") + " rule ticks = " + ((last - first) / pace * 1000.0).ToString("0") + " ms of the ring, from "
                  + (hits.Count > 0 ? (hits[0].DistPx / 8.0).ToString("0.0") : "-") + " m to " + (hits.Count > 0 ? (hits[hits.Count - 1].DistPx / 8.0).ToString("0.0") : "-") + " m");
            Info(label + " the deaths, nearest first:" + sb);
        }

        IEnumerator Main()
        {
            Report = "running";
            var ed = OzUnitEditor.I; var g = ed.Game; var W = ed.World;
            bool playing0 = OzUeControls.Playing; float speed0 = OzUeControls.Speed;
            OzUePlace.Stop(); OzUePick.ClearInspect();
            OzUePlayground.ClearField();
            OzUeNuke.Reset();                                                                                  // the check starts with the nuke ready
            OzUeControls.SetSpeed(1f); OzUeControls.SetPlaying(true);
            var f = Focus(ed);
            OzUeControls.CentreOn(f);
            yield return Seconds(1f);

            // ---- N1 palette and dock
            string id = OzUePlayground.Nuke;
            var pic = Resources.Load<Texture2D>(OzUeArt.NukeTilePath);
            Check(OzUePlayground.KindOf(id) == UeKind.Nuke && OzUePlayground.ShortName(id) == "NUKE" && OzUePlayground.FullName(id) == "NUKE STRIKE"
                  && OzUePlayground.AlwaysNeutral(id) && !OzUePlayground.CanBeNeutral(id) && OzUePlayground.Formation(id, 5, Vector3.zero, 0f).Count == 1
                  && System.Array.IndexOf(OzUePlayground.Extras, id) == 3 && OzUePlayground.UnitsOfGroup(id, -1) == 0 && OzUePlayground.Offered(id, out _),
                  "N1 NUKE: the fourth extra (FLAG, OBSTACLE, GRENADES, NUKE), named NUKE / NUKE STRIKE, always neutral, one per drop, no unit of the cap");
            var plate = GameObject.Find("UE_Dock");
            var tileN = plate ? plate.transform.Find("Tile_" + id) as RectTransform : null;
            var tileG = plate ? plate.transform.Find("Tile_" + OzUePlayground.Grenades) as RectTransform : null;
            Check(tileN && tileG && tileN.gameObject.activeInHierarchy && tileN.anchoredPosition.x > tileG.anchoredPosition.x,
                  "N1 the NUKE tile stands in the dock right of GRENADES (" + (tileN ? "x " + tileN.anchoredPosition.x.ToString("0.0") : "missing") + ", GRENADES " + (tileG ? "x " + tileG.anchoredPosition.x.ToString("0.0") : "missing") + ")");
            Check(pic && pic.width == 256 && pic.height == 256 && OzUeArt.Picture(id, Team.Neutral) == pic,
                  "N1 the NUKE tile picture Resources/" + OzUeArt.NukeTilePath + ": " + (pic ? pic.width + " x " + pic.height : "missing"));

            // ---- N2 the field
            var inside = new List<ZObject>(); var outside = new List<ZObject>();
            AddAll(inside, OzUePlayground.Drop("grunt", Team.Red, f + new Vector3(6f, 0f, 0f), 1));
            AddAll(inside, OzUePlayground.Drop("grunt", Team.Blue, f + new Vector3(-6f, 0f, 0f), 1));
            AddAll(inside, OzUePlayground.Drop("light", Team.Red, f + new Vector3(0f, 0f, 7f), 1));
            AddAll(inside, OzUePlayground.Drop("jeep", Team.Neutral, f + new Vector3(0f, 0f, -7f), 1));
            AddAll(inside, OzUePlayground.Drop(OzUePlayground.Grenades, Team.Neutral, f + new Vector3(4f, 0f, -4f), 1));
            var flag = Lead(OzUePlayground.Drop(OzUePlayground.Flag, Team.Neutral, f + new Vector3(-4f, 0f, 4f), 1));
            float outM = OzUeNuke.DamageRadiusM + 10f, bandM = (OzUeNuke.KillRadiusM + OzUeNuke.DamageRadiusM) * 0.5f;   // 1.3 final: 30 m kill ring, band to 45 m
            AddAll(outside, OzUePlayground.Drop("jeep", Team.Neutral, f + new Vector3(outM, 0f, 0f), 1));
            AddAll(outside, OzUePlayground.Drop("gatling", Team.Neutral, f + new Vector3(-outM, 0f, 0f), 1));
            var band = new List<ZObject>();
            AddAll(band, OzUePlayground.Drop("light", Team.Neutral, f + new Vector3(0f, 0f, bandM), 1));
            AddAll(band, OzUePlayground.Drop("gatling", Team.Neutral, f + new Vector3(0f, 0f, -bandM), 1));
            int inUnits = 0; bool red = false, blue = false;
            foreach (var o in inside) { if (o.IsUnit) inUnits++; red |= o.Owner == Team.Red; blue |= o.Owner == Team.Blue; }
            Check(inside.Count >= 8 && red && blue && flag != null && outside.Count == 2 && band.Count == 2,
                  "N2 the field: " + inside.Count + " object(s) inside the ring (" + inUnits + " units, red and blue, an empty jeep, a crate), a flag inside, " + band.Count + " empty hardware in the band ("
                  + bandM.ToString("0.0") + " m), " + outside.Count + " empty hardware outside (" + outM.ToString("0") + " m: past the band and the crate's flying grenades)");
            yield return Seconds(0.5f);

            // ---- N8 set-up: the robots inside the ring, their throws (direction) and the frame sheet
            var robotsIn = new HashSet<int>(); foreach (var o in inside) if (o.Kind == ZKind.Robot) robotsIn.Add(o.Id);
            var thrownIds = new HashSet<int>(); int away = 0, toward = 0;
            onImpulse = (victim, what, pos, rawFrom, safeFrom, shooter) =>
            {
                if (victim == null || what == null || !what.StartsWith("death", System.StringComparison.Ordinal) || !robotsIn.Contains(victim.Id) || !thrownIds.Add(victim.Id)) return;
                var push = pos - safeFrom; push.y = 0f; var outward = pos - OzUeNuke.LastImpactPoint; outward.y = 0f;
                if (Vector3.Dot(push, outward) > 0f) away++; else toward++;
            };
            HitReactions.ImpulseChecked += onImpulse;
            float wave0 = OpenZFortShockwave.LastFireTime; int thrown0 = OzUeNuke.Thrown, rag0 = RobotRagdoll.Launched;
            if (!string.IsNullOrEmpty(Folder)) { framing = true; StartCoroutine(FrameSheet(Path.Combine(Folder, "frames"), 0.4f)); }

            // ---- N3 the call
            float imp0 = OpenZNukeStrike.LastImpactTime, tCall = Time.unscaledTime;
            int impacts0 = OzUeNuke.Impacts, begun0 = OpenZNukeStrike.PlaygroundBegun;
            var d1 = OzUePlayground.Drop(id, Team.Neutral, f, 1);
            Check(d1.ok && d1.why == OzUeNuke.InboundWords && OpenZNukeStrike.PlaygroundBegun == begun0 + 1 && OpenZNukeStrike.LastPlayground && OpenZNukeStrike.LastPlayground.Playground
                  && !OpenZNukeStrike.Active,
                  "N3 the call: accepted (" + (d1.why ?? "no words") + "), the strike flies in its playground form, not the fort strike (Active " + OpenZNukeStrike.Active + ")");

            // ---- N4 a second call at once + the tile's curtain
            yield return Frames(2);
            var d2 = OzUePlayground.Drop(id, Team.Neutral, f + new Vector3(40f, 0f, 0f), 1);
            bool canNow = OzUePlayground.CanDrop(id, Team.Neutral, f, 1, out string whyNow);
            Check(!d2.ok && d2.why != null && d2.why.StartsWith("NUKE READY IN ", System.StringComparison.Ordinal) && (OzUeNuke.SecondsLeft == 30 || OzUeNuke.SecondsLeft == 29) && !canNow && whyNow == d2.why,
                  "N4 a second call at once refused: " + (d2.why ?? "accepted") + " (the preview says: " + (whyNow ?? "nothing") + ")");
            Check(OzUeDock.NukeCurtain > 0.9f && OzUeDock.NukeCurtain <= 1f && (OzUeDock.NukeSecondsShown == 30 || OzUeDock.NukeSecondsShown == 29),
                  "N4 the NUKE tile shows the cooldown: curtain " + OzUeDock.NukeCurtain.ToString("0.00") + " of the picture, " + OzUeDock.NukeSecondsShown + " s");

            // ---- N5 the impact
            float wait0 = Time.realtimeSinceStartup;
            while (OzUeNuke.Impacts == impacts0 && Time.realtimeSinceStartup - wait0 < 12f) yield return null;
            float flight = Time.realtimeSinceStartup - wait0;
            // red tank shot the blue squad and a red grunt climbed into the empty jeep, so those were thrown by the tank's shells or never thrown
            int robotsAtCall = robotsIn.Count;
            robotsIn.RemoveWhere(rid => { var rob = W.Get(rid); return rob == null || rob.Destroyed || rob.KillAt >= 0; });
            thrownIds.Clear(); away = 0; toward = 0;
            Info("N8 " + (robotsAtCall - robotsIn.Count) + " of " + robotsAtCall + " robots were gone when the bomb hit (shot by the other side, or in a vehicle): not counted");
            yield return Seconds(0.6f);                                                                         // the blast goes through the next ticks
            int insideLeft = 0, outsideHurt = 0; var left = new StringBuilder();
            foreach (var o in inside) { var now = W.Get(o.Id); if (now != null && !now.Destroyed && now.KillAt < 0) { insideLeft++; left.Append(' ').Append(o.Type).Append('#').Append(o.Id); } }
            foreach (var o in outside) { var now = W.Get(o.Id); if (now == null || now.Destroyed || now.Health < now.MaxHealth) outsideHurt++; }
            bool flagStands = flag != null && W.Get(flag.Id) != null && !W.Get(flag.Id).Destroyed;
            Check(OzUeNuke.Impacts == impacts0 + 1, "N5 the bomb hit " + flight.ToString("0.0") + " s after the call (the strike's flight " + OpenZNukeStrike.PlaygroundImpactDelay.ToString("0.0") + " s)");
            Check(insideLeft == 0, "N5 inside the ring: " + insideLeft + " of " + inside.Count + " left standing (want 0)" + left);
            Check(outsideHurt == 0, "N5 outside the ring: " + outsideHurt + " of " + outside.Count + " hurt or gone (want 0)");
            int bandHurt = 0, bandGone = 0; var bandHp = new StringBuilder();
            foreach (var o in band)
            {
                var now = W.Get(o.Id);
                if (now == null || now.Destroyed || now.KillAt >= 0) { bandGone++; continue; }
                if (now.Health < now.MaxHealth) bandHurt++;
                bandHp.Append(' ').Append(o.Type).Append(' ').Append(now.Health).Append('/').Append(now.MaxHealth);
            }
            Check(bandHurt == band.Count && bandGone == 0, "N5 1.3 final, the outer band (" + OzUeNuke.KillRadiusM.ToString("0") + " to " + OzUeNuke.DamageRadiusM.ToString("0") + " m): "
                  + bandHurt + " of " + band.Count + " damaged and still standing, " + bandGone + " gone (want all damaged, 0 gone; about half HP at the middle):" + bandHp);
            Check(flagStands, "N5 the flag inside the ring still stands (flags are immune to every blast)");

            // ---- N8 the shockwave and the robots flying, as at the end of a level
            yield return Seconds(1f);                                                                           // a held throw (RobotDeathLater) has flown
            HitReactions.ImpulseChecked -= onImpulse; onImpulse = null;
            Check(OpenZFortShockwave.LastFireTime > wave0 && (OpenZFortShockwave.LastCentre - OzUeNuke.LastImpactPoint).sqrMagnitude < 4f,
                  "N8 the levels' shockwave (ring, dust wall, shove) fired at the drop point (" + (OpenZFortShockwave.LastFireTime > wave0 ? "yes" : "no") + ")");
            Check(robotsIn.Count > 0 && thrownIds.Count == robotsIn.Count && away == thrownIds.Count && toward == 0,
                  "N8 robots inside the wave's kill ring " + robotsIn.Count + ", thrown " + thrownIds.Count + ": away from the bomb " + away + ", toward it " + toward + " (want all away, 0 toward)");
            Info("N8 thrown from the bomb's point (OzUeNuke.ThrowFrom) " + (OzUeNuke.Thrown - thrown0) + ", ragdolls launched " + (RobotRagdoll.Launched - rag0)
                 + "; " + outside.Count + " empty hardware outside the ring: jolted by the wave, not killed (as the level ending's survivors)");
            yield return Seconds(3f);                                                                           // the frame sheet runs to the last landing
            framing = false;
            if (!string.IsNullOrEmpty(Folder)) Info("N8 frame sheet: " + framesSaved + " frame(s) in " + Path.Combine(Folder, "frames"));
            Check(Mathf.Approximately(Time.timeScale, 1f) && Mathf.Approximately(OpenZNukeStrike.LastImpactTime, imp0) && !W.GameOver && OzUnitEditor.I == ed
                  && !OpenZEndScreen.AnyShown,
                  "N5 no ending: time scale " + Time.timeScale.ToString("0.00") + ", LastImpactTime unchanged " + Mathf.Approximately(OpenZNukeStrike.LastImpactTime, imp0) + ", game over " + W.GameOver + ", the editor open");

            // ---- N6 the sounds
            yield return Seconds(1.5f);
            var es = OzSoundLog.Since(tCall);
            string[] want = { "legacy-nuclear-launch", "v3-aircraft-engine-a", "v3-bomb-release-a", "nuke-descent-20260909", "legacy-nuke-long" };
            var miss = new List<string>();
            foreach (var w in want) if (Played(es, w) == 0) miss.Add(w);
            Check(miss.Count == 0, "N6 the strike's sounds in the sound log since the call: " + (miss.Count == 0 ? "all " + want.Length : "missing " + string.Join(", ", miss)));
            Check(Played(es, "ui_confirm") >= 1 && Played(es, "ui_error") >= 1, "N6 the call's ui_confirm and the refusal's ui_error played");

            CurveCheck();
            OzUePlayground.ClearField();
            OzUeNuke.Reset();                                                                                  // ready again (a check run only)
            OzUeControls.SetSpeed(1f); OzUeControls.SetPlaying(true);
            yield return Frames(3);
            var row = new List<ZObject>(); var beyond9 = new List<ZObject>();
            AddAll(row, OzUePlayground.Drop("grunt", Team.Red, f + new Vector3(2.5f, 0f, 0f), 1));            // one team only: nobody fights before the bomb
            AddAll(row, OzUePlayground.Drop("grunt", Team.Red, f + new Vector3(-7.5f, 0f, 0f), 1));
            AddAll(row, OzUePlayground.Drop("light", Team.Red, f + new Vector3(0f, 0f, 10.5f), 1));
            AddAll(row, OzUePlayground.Drop("jeep", Team.Neutral, f + new Vector3(0f, 0f, -13.5f), 1));
            AddAll(beyond9, OzUePlayground.Drop("jeep", Team.Neutral, f + new Vector3(OzUeNuke.DamageRadiusM + 4f, 0f, 0f), 1));   // 1.3 final: past the band (was 19 m, past the old 15 m ring)
            yield return Seconds(0.5f);
            bool pausedAtImpact = false; double ruleAtPause = 0.0; float timeAtPause = 0f; int hitsAtPause = -1;
            OzUeNuke.AfterImpact = () => { OzUeControls.SetPlaying(false); pausedAtImpact = true; ruleAtPause = W.Time; timeAtPause = Time.time; hitsAtPause = W.PlaygroundNukeHits.Count; };
            var screen = new List<Vector2>();                                                                  // (the ring's age, the robot's distance) at each death throw
            onImpulse = (victim, what, pos, rawFrom, safeFrom, shooter) =>
            {
                if (victim == null || what == null || !what.StartsWith("death", System.StringComparison.Ordinal)) return;
                var dd = pos - OzUeNuke.LastImpactPoint; dd.y = 0f;
                screen.Add(new Vector2(Time.time - OzUeNuke.LastImpactAt, dd.magnitude));
            };
            HitReactions.ImpulseChecked += onImpulse;
            int wave9 = W.PlaygroundNukes + 1, imp9 = OzUeNuke.Impacts;
            var d9 = OzUePlayground.Drop(id, Team.Neutral, f, 1);
            float w9 = Time.realtimeSinceStartup;
            while (OzUeNuke.Impacts == imp9 && Time.realtimeSinceStartup - w9 < 12f) yield return null;
            int inside9 = OzUeNuke.LastInside;
            yield return Seconds(0.5f);                                                                         // paused: the wave must wait
            int waiting9 = 0; foreach (var m in W.PendingMissiles) if (m.nuke) waiting9++;
            var wv9 = Wave(W, wave9);
            Check(d9.ok && pausedAtImpact && wv9 != null && W.Time == ruleAtPause && Time.time == timeAtPause && W.PlaygroundNukeHits.Count == hitsAtPause && waiting9 == wv9.Steps,
                  "N9 PAUSE at the impact holds the wave: the rule clock " + (W.Time == ruleAtPause ? "still" : "moved") + ", the ring's clock " + (Time.time == timeAtPause ? "still" : "moved") + ", "
                  + (hitsAtPause >= 0 ? W.PlaygroundNukeHits.Count - hitsAtPause : -1) + " reached while paused (want 0), " + waiting9 + " of " + (wv9 != null ? wv9.Steps : 0) + " wave steps waiting");
            OzUeNuke.AfterImpact = null;
            OzUeControls.SetPlaying(true);
            yield return Seconds(1f);                                                                           // the wave (0.08 s) and the throws
            HitReactions.ImpulseChecked -= onImpulse; onImpulse = null;
            WaveTiming(W, wave9, inside9, "N9 x1:");
            int beyondHurt = 0; foreach (var o in beyond9) { var now = W.Get(o.Id); if (now == null || now.Destroyed || now.Health < now.MaxHealth) beyondHurt++; }
            Check(beyond9.Count > 0 && beyondHurt == 0, "N9 outside the ring (" + (OzUeNuke.DamageRadiusM + 4f).ToString("0") + " m): " + beyondHurt + " of " + beyond9.Count + " hurt (want 0: only rocked by the wave)");
            int scrEarly = 0; float lead = 99f;
            foreach (var s in screen) { float ring = OpenZFortShockwave.RadiusAt(s.x); lead = Mathf.Min(lead, ring - s.y); if (ring < s.y - 0.5f) scrEarly++; }
            Check(screen.Count > 0 && scrEarly == 0,
                  "N9 on screen: " + screen.Count + " robot death throw(s), " + scrEarly + " before the ring reached the robot (want 0; the ring at least " + (screen.Count > 0 ? lead.ToString("0.0") : "-") + " m past the robot at its throw, 0.5 m allowed for the view's position)");

            // ---- N10 CLEAR FIELD at the impact: the rest of the wave goes with the field
            OzUePlayground.ClearField();
            OzUeNuke.Reset();
            yield return Frames(3);
            AddAll(new List<ZObject>(), OzUePlayground.Drop("grunt", Team.Red, f + new Vector3(6f, 0f, 0f), 1));
            yield return Seconds(0.5f);
            var fresh = new List<ZObject>();
            OzUeNuke.AfterImpact = () => { OzUePlayground.ClearField(); AddAll(fresh, OzUePlayground.Drop("grunt", Team.Blue, f + new Vector3(3f, 0f, 0f), 1)); };
            int wave10 = W.PlaygroundNukes + 1, imp10 = OzUeNuke.Impacts;
            var d10 = OzUePlayground.Drop(id, Team.Neutral, f, 1);
            float w10 = Time.realtimeSinceStartup;
            while (OzUeNuke.Impacts == imp10 && Time.realtimeSinceStartup - w10 < 12f) yield return null;
            OzUeNuke.AfterImpact = null;
            yield return Seconds(1f);
            int left10 = 0; foreach (var m in W.PendingMissiles) if (m.nuke) left10++;
            var wv10 = Wave(W, wave10); int hits10 = 0; foreach (var h in W.PlaygroundNukeHits) if (h.Wave == wave10) hits10++;
            int freshHurt = 0; foreach (var o in fresh) { var now = W.Get(o.Id); if (now == null || now.Destroyed || now.Health < now.MaxHealth) freshHurt++; }
            Check(d10.ok && wv10 != null && wv10.Cancelled && left10 == 0 && hits10 == 0 && fresh.Count > 0 && freshHurt == 0,
                  "N10 CLEAR FIELD at the impact: the wave's " + (wv10 != null ? wv10.Steps : 0) + " steps dropped (" + left10 + " left, " + hits10 + " reached), the squad placed right after it 3 m from the bomb: "
                  + freshHurt + " of " + fresh.Count + " hurt (want 0)");

            // ---- N7 the cooldown at x4 = 30 s of watching
            OzUePlayground.ClearField();
            OzUeNuke.Reset();
            OzUeControls.SetSpeed(4f);
            yield return Frames(3);
            AddAll(new List<ZObject>(), OzUePlayground.Drop("grunt", Team.Red, f + new Vector3(3f, 0f, 0f), 1));   // N7 also times the wave at x4
            AddAll(new List<ZObject>(), OzUePlayground.Drop("light", Team.Red, f + new Vector3(0f, 0f, 8f), 1));
            AddAll(new List<ZObject>(), OzUePlayground.Drop("jeep", Team.Neutral, f + new Vector3(-13f, 0f, 0f), 1));
            yield return Frames(3);
            int wave7 = W.PlaygroundNukes + 1, imp7 = OzUeNuke.Impacts;
            int chimes0 = OzUeNuke.ReadyChimes; float tc = Time.time;
            var d3 = OzUePlayground.Drop(id, Team.Neutral, f, 1);
            int inside7 = -1;
            while (!OzUeNuke.Ready && Time.time - tc < 40f) { if (inside7 < 0 && OzUeNuke.Impacts > imp7) inside7 = OzUeNuke.LastInside; yield return null; }
            float cool = Time.time - tc;
            var wv7 = Wave(W, wave7);
            Check(wv7 != null && Mathf.Abs((float)wv7.Pace - 4f) < 0.001f, "N7 the x4 wave runs 4 rule seconds a second of the ring (pace " + (wv7 != null ? wv7.Pace.ToString("0.##") : "none") + ")");
            WaveTiming(W, wave7, System.Math.Max(0, inside7), "N7 x4:");
            yield return Frames(3);
            var es2 = OzSoundLog.Since(tCall);
            Check(d3.ok && cool >= 29.5f && cool <= 30.5f && OzUeNuke.ReadyChimes == chimes0 + 1 && Played(es2, "ui_unit_ready") >= 1,
                  "N7 at x4 the cooldown ran " + cool.ToString("0.00") + " s of game time (want 30, not 7.5), the ready chime rang " + (OzUeNuke.ReadyChimes - chimes0) + " time(s)");
            yield return Seconds(OpenZNukeStrike.PlaygroundImpactDelay + 1f);                                  // the x4 strike lands before the field is cleared
            var d4 = OzUePlayground.Drop(id, Team.Neutral, f, 1);
            Check(d4.ok, "N7 ready again: the next call accepted");
            yield return Seconds(OpenZNukeStrike.PlaygroundImpactDelay + 1f);

            // ---- end
            OzUePlayground.ClearField();
            OzUeControls.SetSpeed(speed0); OzUeControls.SetPlaying(playing0);
            Report = Tag + " " + (fail == 0 ? "PASS" : "FAIL") + " " + pass + " PASS " + fail + " FAIL";
            string all = Report + "\n" + lines;
            Debug.Log(all);
            if (!string.IsNullOrEmpty(Folder))
            {
                try
                {
                    Directory.CreateDirectory(Folder);
                    File.WriteAllText(Path.Combine(Folder, "report.txt"), all);
                    File.WriteAllText(Path.Combine(Folder, "sound_log.txt"), OzSoundLog.Format(OzSoundLog.Since(tCall)));
                }
                catch (System.Exception e) { Debug.LogWarning(Tag + " report not written: " + e.Message); }
            }
            Destroy(gameObject);
        }
    }
}
#endif
