#if UNITY_EDITOR
// OpenZ (GPL-3.0) - 1.3, open_fixes_2nov wreck_toss_landing: counted Play check of every thrown wreck top's landing and of its pieces' end.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// Counted Play check of open_fixes_2nov/wreck_toss_landing (FortGunView.TossPath.cs, UnitView.TossAim.cs, OzHeavyLanding.Aim.cs,
    /// OpenZFortShockwave.Late.cs, ZodBriefing.EndingKeepCannonPieces). Menus OpenZ/V1.3/Wrecks: Toss Landing ...
    /// <list type="bullet">
    /// <item>Staged (<see cref="Run"/>, a level in Play): for each fort, four tanks (light, medium, medium tank, heavy) and four field guns
    /// (gun, howitzer, gatling, missile cannon), empty and neutral, are made beside it and destroyed one after another (tanks throw their turret
    /// at once, guns 2-4 s later), then the fort's own cannon is thrown (FortGunView.TestToss: shot off while its fort stands); 20 s on the
    /// fort, 12 s with the camera away (pieces sink only off camera), back. The rules' game end is held during the check (World.NoGameEnd).</item>
    /// <item>Recorder (<see cref="Arm"/> ... <see cref="Disarm"/>): follows every toss that happens meanwhile (a played level, a win scene
    /// recorded, the item 16 tower check) and writes the same report.</item>
    /// </list>
    /// Counted per toss, from the thrown piece's drawn pose on every frame:
    ///  T1 turn: the largest turn between two frames from 15 frames before its landing to 10 after it is at most 20 degrees or its own free
    ///     spin step x 1.25 + 1 degree, the larger (the cannon before the kit: up to 176 degrees in one frame);
    ///  T2 place: the landing frame's step at most the flight's last step x 1.3 + 5 cm; each of the 15 steps after it at most 0.2 m; no rise of
    ///     more than 0.3 m in all after it (the bounce; a turret before the kit rose up to a metre and more out of the ground in 0.08 s);
    ///  T3 ground: on its landing frame its real lowest vertex lies 0.25 m in to 0.15 m over the ground (OzHeavyLanding.AimGap; for the cannon
    ///     the break-up's pieces as they are made, less the break-up's lift; a turret that lands in lava or water is listed, not counted);
    ///  P1 the cannon's break-up lifted its pieces out of the ground by at most 3 cm (OzFortGunBreakup.LastPlan);
    ///  P2 its pieces: the fastest in its first 1.5 s at most 10 m/s (the wave's late shove before the kit: 12-15 m/s);
    ///  P3 every piece and every landed turret ends by sinking (its top under the ground), popping out (scaled down), melting in lava or in
    ///     water, never by being hidden or removed in one frame while it stands over the ground; drawn time and the end are listed.
    /// Pieces and turrets never drawn (a win scene leaves the losing side's units out of its picture from its first frame) are listed, not counted.
    /// Update 1 (the residual rows of RUN7 U9c-2): turns and steps are read per median frame (a frame longer than the median, a hitch of the
    /// whole game, moved everything further: its turn and step x median / its time; the raw largest turn, the raw landing step and the longest
    /// frame are listed); P2 reads a piece's speed at its body's centre of mass (its pivot, the drawn box's middle when it was made, circles
    /// that while it spins; the pivot's speed is listed); P3's top over the ground counts what is drawn (as Drawn). Rules and bars unchanged.
    /// Output: report.txt in <see cref="Folder"/>. Test only.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class OzTossLandingCheck : MonoBehaviour
    {
        public static string Report = "idle";
        public static string Folder => Path.Combine(OzRelease.Milestones("v13_wreck_toss_landing"), SceneManager.GetActiveScene().name);
        public static float SnapDeg = 20f, RotSlack = 1.25f, RotPad = 1f, LandSlack = 1.3f, LandPad = 0.05f, AfterStepMax = 0.2f, RiseMax = 0.3f, InMax = 0.25f, OverMax = 0.15f,
                            LiftMax = 0.03f, PieceSpeedMax = 10f, StandMin = 0.1f;
        static readonly (string cat, string type)[] Stage = { ("vehicle", "light"), ("vehicle", "medium"), ("vehicle", ZMTank.Type), ("vehicle", "heavy"),
                                                              ("cannon", "gun"), ("cannon", "howitzer"), ("cannon", "gatling"), ("cannon", "missile_cannon") };
        static OzTossLandingCheck me;

        sealed class Sample { public int f; public float t, dt; public Vector3 p; public Quaternion r; public bool landed, drawn; }   // update 1: dt = that frame's time step
        sealed class Toss
        {
            public string tag; public Transform piece; public FortGunView gun; public UnitView unit;
            public readonly List<Sample> s = new List<Sample>();
            public int landIdx = -1, after; public bool done, broke, gapOk, everDrawn; public float gap = float.NaN, lift = float.NaN, pieceGap = float.NaN;
        }
        sealed class Piece
        {
            public string tag; public Transform t; public float born, lastT, maxSpeed, maxPivotSpeed, topOver, scale0 = 1f, scale = 1f; public Vector3 prev, prevCom; public bool hasPrev, ended, everDrawn, melting, popping, water;
            public Rigidbody rb;                                                                     // update 1: the piece's body (its speed is read at its centre of mass)
            public string end = "still drawn at the end";
        }
        readonly List<Toss> tosses = new List<Toss>();
        readonly List<Piece> pieces = new List<Piece>();
        readonly HashSet<int> known = new HashSet<int>(), knownPieces = new HashSet<int>();
        readonly StringBuilder sb = new StringBuilder();
        readonly List<FortGunView> guns = new List<FortGunView>();
        readonly List<Renderer> rbuf = new List<Renderer>();
        int pass, fail, late0, fly0, nextGuns; bool staged, finished;

        public static void Arm() { if (me) { Debug.LogWarning("[OpenZ] toss landing check: already running"); return; } Report = "recording"; me = new GameObject("OzTossLandingCheck").AddComponent<OzTossLandingCheck>(); }
        public static void Disarm() { if (me) me.Finish(); }
        public static void Run()
        {
            if (me) { Debug.LogWarning("[OpenZ] toss landing check: already running"); return; }
            Report = "staging"; me = new GameObject("OzTossLandingCheck").AddComponent<OzTossLandingCheck>(); me.staged = true;
        }

        void Awake() { late0 = OpenZFortShockwave.LateSkipped; fly0 = ZodBriefing.EndingFlyParts; }

        void Line(string s) { sb.AppendLine(s); Debug.Log("[OpenZ toss check] " + s); }
        void Verdict(bool ok, string s) { if (ok) pass++; else fail++; Line((ok ? "PASS " : "FAIL ") + s); }

        IEnumerator Start()
        {
            if (!staged) yield break;
            var g = ZGame.I; var W = g ? g.World : null;
            if (W == null) { Report = "no game"; Finish(); yield break; }
            var rts = FindFirstObjectByType<RtsCamera>(); var focus0 = rts ? rts.focus : Vector3.zero;
            float speed0 = g.gameSpeed; g.gameSpeed = 1f; bool hold0 = W.NoGameEnd; W.NoGameEnd = true;
            Line($"staged on {SceneManager.GetActiveScene().name}: game speed {speed0:0.##} (1 during the check), the rules' game end held");
            var forts = W.Objects.Where(o => o.IsFort && !o.Destroyed && o.Owner != Team.Neutral).ToList();
            foreach (var fort in forts)
            {
                string ftag = fort.Owner + " fort #" + fort.Id;
                if (rts) rts.FlyTo(g.CenterWorld(fort));
                yield return new WaitForSecondsRealtime(1.5f);
                var made = new List<ZObject>();
                int tx0 = fort.CenterX / 16 - 12, ty0 = fort.CenterY / 16 + 12;
                foreach (var (cat, type) in Stage)
                {
                    if (!FreeTile(W, tx0, ty0, out int tx, out int ty)) { Line($"{ftag}: no free tile for a {type}"); continue; }
                    var o = W.CreateObject(cat, type, tx * 16, ty * 16, Team.Neutral);
                    if (o == null) { Line($"{ftag}: {type} not made"); continue; }
                    g.SpawnUnitView(o); made.Add(o); tx0 += 4;
                }
                yield return new WaitForSeconds(1.5f);
                foreach (var o in made) { if (o.Destroyed) continue; o.SetHealth(0); W.UpdateObjectHealth(o); yield return new WaitForSeconds(0.8f); }
                var gun = FindObjectsByType<FortGunView>(FindObjectsSortMode.None).FirstOrDefault(v => v && v.Bound && v.Fort == fort && !v.Tossed);
                if (gun) { gun.TestToss(); Line($"{ftag}: its cannon thrown (shot off while the fort stands)"); }
                else Line($"{ftag}: no cannon of its own to throw");
                yield return new WaitForSeconds(20f);
                if (rts) rts.FlyTo(g.CenterWorld(fort) + new Vector3(140f, 0f, 140f));                                   // away: the wrecks sink only off camera (WreckSinkGate)
                yield return new WaitForSeconds(12f);
                if (rts) rts.FlyTo(g.CenterWorld(fort));
                yield return new WaitForSeconds(2f);
            }
            if (rts) rts.FlyTo(focus0);
            g.gameSpeed = speed0; W.NoGameEnd = hold0;
            Finish();
        }

        static bool FreeTile(ZWorld W, int tx0, int ty0, out int tx, out int ty)
        {
            for (int r = 0; r < 14; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        tx = tx0 + dx; ty = ty0 + dy;
                        if (tx < 2 || ty < 2 || tx > W.Map.width - 4 || ty > W.Map.height - 4) continue;
                        bool ok = true;
                        for (int j = 0; j < 2 && ok; j++) for (int i = 0; i < 2 && ok; i++) ok = W.Path.TilePassable(tx + i, ty + j, false);
                        if (!ok) continue;
                        foreach (var o in W.Objects) if (!o.Destroyed && System.Math.Abs((o.CenterX >> 4) - tx) < 3 && System.Math.Abs((o.CenterY >> 4) - ty) < 3) { ok = false; break; }
                        if (ok) return true;
                    }
            tx = ty = 0; return false;
        }

        // ------------------------------------------------------------------ every frame, after everything else has moved
        void LateUpdate()
        {
            if (finished) return;
            Discover();
            foreach (var x in tosses) if (!x.done) SampleToss(x);
            foreach (var p in pieces) if (!p.ended) SamplePiece(p);
        }

        void Discover()
        {
            var g = ZGame.I; if (!g) return;
            if (Time.frameCount >= nextGuns) { nextGuns = Time.frameCount + 30; guns.Clear(); guns.AddRange(FindObjectsByType<FortGunView>(FindObjectsSortMode.None)); }
            foreach (var v in guns)
            {
                if (!v || !v.Tossed || !v.YawT || !known.Add(v.YawT.GetInstanceID())) continue;
                tosses.Add(new Toss { gun = v, piece = v.YawT, tag = "fort cannon of fort #" + (v.Fort != null ? v.Fort.Id : -1) });
            }
            foreach (var kv in g.Views)
            {
                var v = kv.Value; if (!v) continue;
                var p = v.TossedPiece; if (!p || !known.Add(p.GetInstanceID())) continue;
                tosses.Add(new Toss { unit = v, piece = p, tag = (v.Obj != null ? v.Obj.Type + " #" + v.Obj.Id : "unit") + (v.OnTower ? " on a tower" : "") + " turret" });
            }
        }

        bool Drawn(Transform t)
        {
            if (!t || !t.gameObject.activeInHierarchy) return false;
            t.GetComponentsInChildren(false, rbuf);
            foreach (var r in rbuf) if (r && r.enabled && !r.forceRenderingOff && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)) return true;
            return false;
        }

        float TopOver(Transform t)
        {
            var g = ZGame.I; if (!g || !t) return 0f;
            t.GetComponentsInChildren(false, rbuf);
            bool any = false; var b = new Bounds();
            foreach (var r in rbuf)
            {
                if (!r || !r.enabled || r.forceRenderingOff || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;   // update 1: what is drawn (as Drawn)
                if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; }
            }
            if (!any) return 0f;
            return b.max.y - g.GroundY(b.center.x, b.center.z);
        }

        void SampleToss(Toss x)
        {
            if (!x.piece) { x.done = true; return; }
            bool landed = x.gun ? x.gun.TossLandedNow : x.unit && x.unit.TossedLanded;
            bool drawn = Drawn(x.piece); x.everDrawn |= drawn;
            if (x.gun && landed && !x.piece.gameObject.activeInHierarchy)
            {   // the cannon broke up on its landing frame: its pieces take over
                if (!x.broke && OzFortGunBreakup.LastSource == x.piece)
                {
                    x.broke = true;
                    var m = Regex.Match(OzFortGunBreakup.LastPlan ?? "", @"lifted (\d+) cm as one");
                    x.lift = m.Success ? int.Parse(m.Groups[1].Value) / 100f : float.NaN;
                    float low = float.MaxValue; bool any = false;
                    foreach (var pc in OzFortGunBreakup.LastPieces)
                    {
                        if (!pc) continue;
                        float gp = OzHeavyLanding.AimGap(pc.transform, pc.transform.position, pc.transform.rotation, out bool ok);
                        if (ok) { any = true; low = Mathf.Min(low, gp); }                                     // its real lowest vertex over the ground, as it is made
                        AddPiece(pc.transform, x.tag + " piece " + pc.PieceName);
                    }
                    if (any) { x.pieceGap = low; x.gap = low - (float.IsNaN(x.lift) ? 0f : x.lift); x.gapOk = true; }
                    if (x.landIdx < 0) { x.landIdx = x.s.Count; x.s.Add(new Sample { f = Time.frameCount, t = Time.time, dt = Time.deltaTime, p = x.gun.TossToNow, r = x.gun.TossEndNow, landed = true, drawn = false }); }
                }
                x.done = true; return;
            }
            x.s.Add(new Sample { f = Time.frameCount, t = Time.time, dt = Time.deltaTime, p = x.piece.position, r = x.piece.rotation, landed = landed, drawn = drawn });
            if (landed && x.landIdx < 0)
            {
                x.landIdx = x.s.Count - 1;
                float gp = OzHeavyLanding.AimGap(x.piece, x.piece.position, x.piece.rotation, out bool ok);
                if (ok) { x.gap = gp; x.gapOk = true; }
            }
            if (x.landIdx >= 0 && ++x.after > 20)
            {
                x.done = true;
                if (x.unit) AddPiece(x.piece, x.tag + " (landed)");
            }
        }

        void AddPiece(Transform t, string tag)
        {
            if (!t || !knownPieces.Add(t.GetInstanceID())) return;
            var p = new Piece { t = t, tag = tag, born = Time.time, lastT = Time.time, scale0 = Mathf.Max(1e-4f, t.lossyScale.x), rb = t.GetComponent<Rigidbody>() };
            pieces.Add(p); SamplePiece(p);
        }

        void SamplePiece(Piece p)
        {
            if (!p.t) { End(p, "removed"); return; }
            bool drawn = Drawn(p.t);
            if (!drawn) { End(p, p.t.gameObject.activeInHierarchy ? "hidden" : "switched off"); return; }
            p.everDrawn = true;
            float now = Time.time;
            var com = p.rb ? p.t.TransformPoint(p.rb.centerOfMass) : p.t.position;                  // update 1: the body's centre of mass as drawn
            if (p.hasPrev && now - p.born <= 1.5f && now > p.lastT)
            {
                p.maxSpeed = Mathf.Max(p.maxSpeed, (com - p.prevCom).magnitude / (now - p.lastT));
                p.maxPivotSpeed = Mathf.Max(p.maxPivotSpeed, (p.t.position - p.prev).magnitude / (now - p.lastT));
            }
            p.prev = p.t.position; p.prevCom = com; p.hasPrev = true; p.lastT = now;
            p.topOver = TopOver(p.t); p.scale = p.t.lossyScale.x / p.scale0;
            p.melting |= OzLavaMelt.Busy(p.t); p.popping |= p.t.GetComponent<PopOut>() != null;
            p.water = OzWaterLanding.InWater(p.t);
        }

        void End(Piece p, string how)
        {
            p.ended = true;
            if (!p.everDrawn) { p.end = "never drawn (left out of the picture from its start)"; return; }
            string why = p.melting ? "melted in lava" : p.water ? "in water" : p.popping || p.scale < 0.6f ? "popped out" : p.topOver < StandMin ? "sank (its top under the ground)" : null;
            p.end = why != null ? why + ", then " + how : $"VANISHED: {how} in one frame while it stood {p.topOver:0.00} m over the ground";
        }

        /// <summary>Update 1: the median time step of a toss's frames (s).</summary>
        static float FrameMedian(List<Sample> s)
        {
            var d = new List<float>(); for (int i = 1; i < s.Count; i++) if (s[i].dt > 0f) d.Add(s[i].dt);
            if (d.Count == 0) return 1f / 30f;
            d.Sort(); return d[d.Count / 2];
        }

        /// <summary>Update 1: a frame longer than the median moved everything further (a hitch, the whole game's): its step is read per
        /// median frame (x median / its time, at most 4 x shorter). A frame at or under the median counts as it is.</summary>
        static float FrameShare(float dt, float med) => dt > med && med > 0f ? Mathf.Max(0.25f, med / dt) : 1f;

        // ------------------------------------------------------------------ the report
        void Finish()
        {
            if (finished) return;
            finished = true;
            foreach (var x in tosses) if (!x.done) { x.done = true; }
            Line($"{tosses.Count} tosses followed, {pieces.Count} pieces / landed turrets; kit switches: cannon path {FortGunView.TossPathOn}, turret aim {UnitView.TossAimGround}, turret sinks whole {UnitView.TossSinkFull}, late bodies left alone {OpenZFortShockwave.SkipLateBodies}, win scene keeps the cannon's pieces {ZodBriefing.EndingKeepCannonPieces}; update 1: turret turn over the whole flight {UnitView.TossWholePath}, turret rock by its length {OzHeavyLanding.RockByLever}, turret sinks under its whole box {UnitView.TossSinkUnderAll}, cannon pieces' top speed {OzFortGunBreakup.PieceSpeedCapOn}");
            Line($"update 1: whole-flight turret turns planned {UnitView.TossWholePlanned} (last correction {UnitView.LastTossWholeFixDeg:0} deg), turret rocks capped by their length {OzHeavyLanding.RocksCapped} (last lever {OzHeavyLanding.LastRockLever:0.00} m, rock {OzHeavyLanding.LastRockDeg:0.0} deg), cannon pieces made with the top speed {OzFortGunBreakup.SpeedCapped} ({OzFortGunBreakup.PieceSpeedCap:0.#} m/s for {OzFortGunBreakup.PieceSpeedCapSeconds:0.#} s), last turret sink {UnitView.LastTossSinkExtra:0.00} m deeper than its pivot's ground; turns and steps are read per median frame (a longer frame's x median / its time)");
            Line($"cannon arcs moved {FortGunView.TossAims} (last {FortGunView.LastAimMove:+0.00;-0.00} m, last correction {FortGunView.LastFixDeg:0} deg), turret arcs moved {UnitView.TossAimed} (last {UnitView.LastTossAimMove:+0.00;-0.00} m), bodies the wave left alone as late {OpenZFortShockwave.LateSkipped - late0} ({OpenZFortShockwave.LastLate}), thrown parts left out of a win scene {ZodBriefing.EndingFlyParts - fly0}");
            int counted = 0;
            foreach (var x in tosses)
            {
                if (x.landIdx < 1) { Line($"-    {x.tag}: no landing seen ({x.s.Count} frames followed)"); continue; }
                if (!x.everDrawn && !x.broke) { Line($"-    {x.tag}: never drawn (left out of the picture), not counted"); continue; }
                counted++;
                var s = x.s; int L = x.landIdx;
                var rot = new List<float>(); var pos = new List<float>();
                float dtMed = FrameMedian(s), rawRot = 0f, rawLand = 0f, longest = 0f;                        // update 1: a long frame (a hitch) moves everything further: read per median frame
                for (int i = 1; i < s.Count; i++)
                {
                    float w = FrameShare(s[i].dt, dtMed), a0 = Quaternion.Angle(s[i - 1].r, s[i].r), p0 = (s[i].p - s[i - 1].p).magnitude;
                    rot.Add(a0 * w); pos.Add(p0 * w);
                    if (i >= L - 14 && i <= L + 10) { rawRot = Mathf.Max(rawRot, a0); longest = Mathf.Max(longest, s[i].dt); }
                    if (i == L) rawLand = p0;
                }
                string per = $" (per {dtMed * 1000f:0} ms frame; raw {rawRot:0.0} deg / landing step {rawLand:0.00} m, longest frame here {longest * 1000f:0} ms)";
                // the free spin: the median turn a frame over the first half of the flight
                var free = rot.Take(Mathf.Max(1, L / 2)).Skip(1).OrderBy(v => v).ToList();
                float freeStep = free.Count > 0 ? free[free.Count / 2] : 0f;
                int a = Mathf.Max(0, L - 15), b = Mathf.Min(rot.Count - 1, L + 9);
                float worstRot = 0f; int worstAt = a;
                for (int i = a; i <= b; i++) if (rot[i] > worstRot) { worstRot = rot[i]; worstAt = i; }
                float rotMax = Mathf.Max(SnapDeg, freeStep * RotSlack + RotPad);                                // a smooth ease may turn fast; a snap or a flip does more
                Verdict(worstRot <= rotMax, $"T1 {x.tag}: largest turn in a frame round the landing {worstRot:0.0} deg ({worstAt + 1 - L:+0;-0} frames from it), free spin {freeStep:0.0} deg a frame, at most {rotMax:0.0}{per}");
                float landStep = pos[L - 1], lastStep = L >= 2 ? pos[L - 2] : landStep;
                float afterMax = 0f, rise = 0f; int n = 0;
                for (int i = L; i < pos.Count && n < 15; i++, n++) { afterMax = Mathf.Max(afterMax, pos[i]); rise += Mathf.Max(0f, s[i + 1].p.y - s[i].p.y); }
                bool t2 = landStep <= lastStep * LandSlack + LandPad && afterMax <= AfterStepMax && rise <= RiseMax;
                Verdict(t2, $"T2 {x.tag}: landing step {landStep:0.00} m (last flight step {lastStep:0.00}), after it: largest step {afterMax:0.00} m over {n} frames, rise {rise:0.00} m{(x.broke ? " (broke up on that frame)" : "")}{per}");
                var mine = pieces.FirstOrDefault(p => p.t == x.piece);
                string t3 = $"T3 {x.tag}: real lowest vertex {x.gap:+0.00;-0.00} m over the ground on its landing frame{(x.broke ? $" (pieces as made {x.pieceGap:+0.00;-0.00} m less the lift)" : "")}";
                if (x.gapOk && mine != null && (mine.melting || mine.water)) Line($"-    {t3} (it landed in {(mine.melting ? "lava" : "water")}: not counted)");
                else if (x.gapOk) Verdict(x.gap >= -InMax && x.gap <= OverMax, t3);
                else Line($"-    T3 {x.tag}: no ground fit read");
                if (x.broke) Verdict(!float.IsNaN(x.lift) && x.lift <= LiftMax, $"P1 {x.tag}: the break-up lifted its pieces {(float.IsNaN(x.lift) ? "(not read)" : (x.lift * 100f).ToString("0") + " cm")} out of the ground");
            }
            var cannonPieces = pieces.Where(p => p.tag.Contains(" piece ")).ToList();
            foreach (var grp in cannonPieces.GroupBy(p => p.tag.Substring(0, p.tag.IndexOf(" piece "))))
            {
                var drawn = grp.Where(p => p.everDrawn).ToList(); if (drawn.Count == 0) continue;
                float fastest = drawn.Max(p => p.maxSpeed), pivot = drawn.Max(p => p.maxPivotSpeed);
                Verdict(fastest <= PieceSpeedMax, $"P2 {grp.Key}: fastest piece in its first 1.5 s {fastest:0.0} m/s at its centre of mass (its pivot {pivot:0.0} m/s; {drawn.Count} pieces)");
            }
            foreach (var p in pieces)
            {
                if (!p.ended) { p.end = "still drawn at the end"; }
                if (!p.everDrawn) { Line($"-    P3 {p.tag}: {p.end}"); continue; }
                bool vanished = p.end.StartsWith("VANISHED");
                Verdict(!vanished, $"P3 {p.tag}: {p.end}; drawn {(p.ended ? p.lastT - p.born : Time.time - p.born):0.0} s");
            }
            Report = $"{pass} PASS, {fail} FAIL ({counted} tosses counted, {pieces.Count} pieces / turrets)";
            Line(Report);
            try { Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "report.txt"), sb.ToString()); Line("report: " + Path.Combine(Folder, "report.txt")); }
            catch (IOException e) { Debug.LogWarning("[OpenZ toss check] report not written: " + e.Message); }
            if (me == this) me = null;
            Destroy(gameObject);
        }
    }
}
#endif
