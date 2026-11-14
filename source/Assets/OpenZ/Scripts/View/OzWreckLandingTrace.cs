#if UNITY_EDITOR
// OpenZ (GPL-3.0) - 1.4, kit wreck_landing: Play trace of each thrown turret's first second on the ground, frame by frame (test only).
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// Kit wreck_landing (1.4): a per-frame trace of every thrown tank or gun turret, from its landing frame (UnitView.TossedLanded) to
    /// <see cref="Follow"/> s after it. Menus OpenZ/V1.4/Wrecks: Landing Trace ... (Editor/OzV14WreckLandingMenu.cs).
    /// <list type="bullet">
    /// <item>Staged (<see cref="Run"/>, a level in Play, after a Play start): the roughest free ground of the level (the largest rise and fall
    /// of the ground over 6 m round a free 2 x 2 tile: cliffs, ledges and hillsides first; no water or lava; spots at least
    /// <see cref="SpotGapM"/> m apart), <see cref="Spots"/> spots. On each a neutral, empty tank or gun (light, medium, medium tank, heavy, gun,
    /// howitzer, gatling, missile cannon in turn), four at a time, destroyed one after another; it waits for their turrets (tanks throw at
    /// once, guns 2-4 s later) and follows each. The rules' game end is held meanwhile.</item>
    /// <item>Recorder (<see cref="Arm"/> ... <see cref="Disarm"/>): every turret thrown meanwhile (a played level, a clip).</item>
    /// </list>
    /// Each frame three samples of the piece: at the frame's start (an Update at order -32000), right after the heavy landing (an Update at
    /// order 1; OzHeavyLanding runs at 0) and at the frame's end (a LateUpdate at 32001). A move between them that the heavy landing did not
    /// report (OzHeavyLanding.SettleMoved) is named by its phase: between frames (physics), an Update before order 1, or after order 1 (a later
    /// Update, a coroutine, the animation or a LateUpdate). With the kit's switch off (the 1.3 landing) its own moves are named as the heavy
    /// landing's. Per turret, from its landing frame: R rise (its pivot and its drawn box's middle never over their landing-frame heights;
    /// 1 mm slack), M still (its pivot's path after the first 0.3 s under 5 cm), W movers (no frame moved by anything but the heavy
    /// landing), G rest (its real lowest vertex 0.25 m in to 0.15 m over the ground at the end, OzHeavyLanding.AimGap). The first 15 frames
    /// are listed one by one. A turret that the water or lava rule took is listed, not counted. Output: report.txt in <see cref="Folder"/>.
    /// </summary>
    [DefaultExecutionOrder(32001)]
    public sealed class OzWreckLandingTrace : MonoBehaviour
    {
        public static string Report = "idle";
        public static string Folder => Path.Combine(OzRelease.Milestones("v14_wreck_landing"), SceneManager.GetActiveScene().name);
        public static int Spots = 16, FramesListed = 15;
        public static float Follow = 1.2f, StillAfter = 0.3f, RiseMax = 0.001f, StillMax = 0.05f, MoveSlack = 0.001f, TurnSlack = 0.05f, GapIn = 0.25f, GapOver = 0.15f, SpotGapM = 20f;
        static readonly (string cat, string type)[] Stage = { ("vehicle", "light"), ("vehicle", "medium"), ("vehicle", ZMTank.Type), ("vehicle", "heavy"),
                                                              ("cannon", "gun"), ("cannon", "howitzer"), ("cannon", "gatling"), ("cannon", "missile_cannon") };
        static OzWreckLandingTrace me;

        sealed class Row { public int f; public float t, dt, turn, gap = float.NaN; public Vector3 p, mid; public string who = "-"; }
        sealed class Trace
        {
            public string tag, plan = "-", ground = "-"; public UnitView unit; public Transform piece;
            public bool landed, done, wet, everDrawn; public float landT; public Vector3 p0, mid0;
            public readonly List<Row> rows = new List<Row>();
            public Vector3 aP, bP, cP; public Quaternion aR, bR, cR; public int aF = -1, bF = -1;
            public string who = ""; public int others; public string firstOther = "-";
        }
        readonly List<Trace> traces = new List<Trace>();
        readonly HashSet<int> known = new HashSet<int>();
        readonly List<Renderer> rbuf = new List<Renderer>();
        readonly StringBuilder sb = new StringBuilder();
        int pass, fail; bool staged, finished;

        public static void Arm() { if (me) { Debug.LogWarning("[OpenZ] wreck landing trace: already running"); return; } Report = "recording"; Make(false); }
        public static void Disarm() { if (me) me.Finish(); }
        public static void Run()
        {
            if (me) { Debug.LogWarning("[OpenZ] wreck landing trace: already running"); return; }
            Report = "staging"; Make(true);
        }

        static void Make(bool stage)
        {
            var go = new GameObject("OzWreckLandingTrace");
            me = go.AddComponent<OzWreckLandingTrace>(); me.staged = stage;
            go.AddComponent<OzWreckLandingTraceEarly>().main = me;
            go.AddComponent<OzWreckLandingTraceMid>().main = me;
        }

        void Line(string s) { sb.AppendLine(s); Debug.Log("[OpenZ wreck landing trace] " + s); }
        void Verdict(bool ok, string s) { if (ok) pass++; else fail++; Line((ok ? "PASS " : "FAIL ") + s); }

        // ------------------------------------------------------------------ staged: the roughest free ground of the level
        IEnumerator Start()
        {
            Line($"switch OzHeavyLanding.TurretSettleInPlace = {OzHeavyLanding.TurretSettleInPlace} (false = the 1.3 landing); scene {SceneManager.GetActiveScene().name}");
            if (!staged) yield break;
            var g = ZGame.I; var W = g ? g.World : null;
            if (W == null || W.Map == null) { Report = "no game"; Finish(); yield break; }
            var rts = FindFirstObjectByType<RtsCamera>(); var focus0 = rts ? rts.focus : Vector3.zero;
            float speed0 = g.gameSpeed; g.gameSpeed = 1f; bool hold0 = W.NoGameEnd; W.NoGameEnd = true;
            var spots = RoughSpots(g, W, Spots);
            Line($"staged: game speed {speed0:0.##} (1 during the trace), the rules' game end held; {spots.Count} spots on the roughest free ground");
            int k = 0;
            for (int r = 0; r < spots.Count; r += 4)
            {
                var made = new List<ZObject>();
                for (int i = r; i < spots.Count && i < r + 4; i++)
                {
                    var (tx, ty, range, slope) = spots[i]; var (cat, type) = Stage[k++ % Stage.Length];
                    var o = W.CreateObject(cat, type, tx * 16, ty * 16, Team.Neutral);
                    if (o == null) { Line($"spot {tx},{ty}: {type} not made"); continue; }
                    g.SpawnUnitView(o); made.Add(o);
                    Line($"spot {tx},{ty}: {type} #{o.Id} (ground over 6 m: rise and fall {range:0.00} m, slope {slope:0} deg)");
                }
                if (made.Count == 0) continue;
                if (rts) rts.FlyTo(g.CenterWorld(made[0]));
                yield return new WaitForSeconds(1.5f);
                foreach (var o in made) { if (o.Destroyed) continue; o.SetHealth(0); W.UpdateObjectHealth(o); yield return new WaitForSeconds(0.4f); }
                float until = Time.time + 16f;
                while (Time.time < until && !RoundDone(g, made)) yield return null;
                yield return new WaitForSeconds(0.5f);
            }
            if (rts) rts.FlyTo(focus0);
            g.gameSpeed = speed0; W.NoGameEnd = hold0;
            Finish();
        }

        bool RoundDone(ZGame g, List<ZObject> made)
        {
            foreach (var o in made)
            {
                if (!g.Views.TryGetValue(o.Id, out var v) || !v) continue;
                var p = v.TossedPiece; if (!p) return false;
                var x = traces.Find(t => t.piece == p);
                if (x == null || !x.done) return false;
            }
            return true;
        }

        static List<(int tx, int ty, float range, float slope)> RoughSpots(ZGame g, ZWorld W, int want)
        {
            var all = new List<(int tx, int ty, float range, float slope)>();
            for (int ty = 4; ty < W.Map.height - 6; ty += 3)
                for (int tx = 4; tx < W.Map.width - 6; tx += 3)
                {
                    bool ok = true;
                    for (int j = 0; j < 2 && ok; j++) for (int i = 0; i < 2 && ok; i++) ok = W.Path.TilePassable(tx + i, ty + j, false);
                    if (!ok) continue;
                    foreach (var o in W.Objects) if (!o.Destroyed && System.Math.Abs((o.CenterX >> 4) - tx) < 4 && System.Math.Abs((o.CenterY >> 4) - ty) < 4) { ok = false; break; }
                    if (!ok) continue;
                    var c = ZGame.PxToWorld(tx * 16 + 16, ty * 16 + 16);
                    float y0 = g.GroundY(c.x, c.z);
                    if (OzWaterLanding.OpenWaterAt(new Vector3(c.x, y0, c.z), out _)) continue;
                    float hi = float.MinValue, lo = float.MaxValue;
                    for (int j = -2; j <= 2; j++)
                        for (int i = -2; i <= 2; i++) { float h = g.GroundY(c.x + i * 1.5f, c.z + j * 1.5f); hi = Mathf.Max(hi, h); lo = Mathf.Min(lo, h); }
                    float sx = (g.GroundY(c.x + 3f, c.z) - g.GroundY(c.x - 3f, c.z)) / 6f, sz = (g.GroundY(c.x, c.z + 3f) - g.GroundY(c.x, c.z - 3f)) / 6f;
                    all.Add((tx, ty, hi - lo, Mathf.Atan(Mathf.Sqrt(sx * sx + sz * sz)) * Mathf.Rad2Deg));
                }
            all.Sort((a, b) => b.range.CompareTo(a.range));
            var pick = new List<(int tx, int ty, float range, float slope)>();
            float gapTiles = SpotGapM / 2f;
            foreach (var s in all)
            {
                bool far = true;
                foreach (var p in pick) if ((p.tx - s.tx) * (p.tx - s.tx) + (p.ty - s.ty) * (p.ty - s.ty) < gapTiles * gapTiles) { far = false; break; }
                if (far) pick.Add(s);
                if (pick.Count >= want) break;
            }
            return pick;
        }

        // ------------------------------------------------------------------ the three samples of every frame
        internal void Early()
        {
            if (finished) return;
            foreach (var x in traces)
            {
                if (!x.landed || x.done || !x.piece) continue;
                x.aP = x.piece.position; x.aR = x.piece.rotation; x.aF = Time.frameCount;
                if ((x.aP - x.cP).magnitude > MoveSlack || Quaternion.Angle(x.cR, x.aR) > TurnSlack) Other(x, "between frames (physics)");
            }
        }

        internal void Mid()
        {
            if (finished) return;
            foreach (var x in traces)
            {
                if (!x.landed || x.done || !x.piece || x.aF != Time.frameCount) continue;
                x.bP = x.piece.position; x.bR = x.piece.rotation; x.bF = Time.frameCount;
                var h = x.piece.GetComponent<OzHeavyLanding>();
                var told = h && h.SettleMovedFrame == Time.frameCount ? h.SettleMoved : Vector3.zero;
                bool moved = (x.bP - x.aP - told).magnitude > MoveSlack || Quaternion.Angle(x.aR, x.bR) > TurnSlack;
                if (told.sqrMagnitude > MoveSlack * MoveSlack * 0.01f) Note(x, "the planned settle");
                if (!moved) continue;
                if (h && !h.SettlingInPlace && h.SettleMovedFrame != Time.frameCount) Note(x, "the heavy landing (1.3)");
                else Other(x, "an Update before order 1");
            }
        }

        void LateUpdate()
        {
            if (finished) return;
            Discover();
            foreach (var x in traces)
            {
                if (x.done) continue;
                if (!x.piece) { x.done = true; continue; }
                if (!x.landed)
                {
                    if (!x.unit || !x.unit.TossedLanded) continue;
                    x.landed = true; x.landT = Time.time; x.p0 = x.piece.position; x.mid0 = BoxMid(x.piece);
                    var h = x.piece.GetComponent<OzHeavyLanding>();
                    x.plan = h ? (h.SettlingInPlace ? $"planned settle: slide {h.PlanSlide:0.00} m (ended by: {h.PlanEnd})" : "the 1.3 landing (bounce, rock, skid, the fit every frame)") : "no heavy landing on it";
                    x.ground = GroundAt(x.p0);
                    x.cP = x.p0; x.cR = x.piece.rotation;
                    AddRow(x, 0f);
                    continue;
                }
                if (x.bF == Time.frameCount && ((x.piece.position - x.bP).magnitude > MoveSlack || Quaternion.Angle(x.bR, x.piece.rotation) > TurnSlack)) Other(x, "after order 1 (a later Update, a coroutine, the animation or a LateUpdate)");
                float t = Time.time - x.landT;
                AddRow(x, t);
                x.cP = x.piece.position; x.cR = x.piece.rotation;
                x.wet |= OzWaterLanding.Watched(x.piece) || OzLavaMelt.Busy(x.piece);
                if (t >= Follow) x.done = true;
            }
        }

        void Discover()
        {
            var g = ZGame.I; if (!g) return;
            foreach (var kv in g.Views)
            {
                var v = kv.Value; if (!v) continue;
                var p = v.TossedPiece; if (!p || !known.Add(p.GetInstanceID())) continue;
                traces.Add(new Trace { unit = v, piece = p, tag = (v.Obj != null ? v.Obj.Type + " #" + v.Obj.Id : "unit") + (v.OnTower ? " on a tower" : "") + " turret" });
            }
        }

        void Note(Trace x, string who) { if (!x.who.Contains(who)) x.who += (x.who.Length > 0 ? " + " : "") + who; }
        void Other(Trace x, string who)
        {
            Note(x, "OTHER: " + who);
            x.others++; if (x.firstOther == "-") x.firstOther = $"{who}, {Time.time - x.landT:0.00} s after the landing";
        }

        void AddRow(Trace x, float t)
        {
            var p = x.piece.position; var r = x.piece.rotation;
            x.everDrawn |= Drawn(x.piece);
            float gap = OzHeavyLanding.AimGap(x.piece, p, r, out bool ok);
            x.rows.Add(new Row { f = Time.frameCount, t = t, dt = Time.deltaTime, p = p, mid = BoxMid(x.piece), turn = x.rows.Count > 0 ? Quaternion.Angle(x.cR, r) : 0f, gap = ok ? gap : float.NaN, who = x.who.Length > 0 ? x.who : "-" });
            x.who = "";
        }

        bool Drawn(Transform t)
        {
            if (!t || !t.gameObject.activeInHierarchy) return false;
            t.GetComponentsInChildren(false, rbuf);
            foreach (var r in rbuf) if (r && r.enabled && !r.forceRenderingOff && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)) return true;
            return false;
        }

        Vector3 BoxMid(Transform t)
        {
            t.GetComponentsInChildren(false, rbuf);
            bool any = false; var b = new Bounds();
            foreach (var r in rbuf)
            {
                if (!r || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; }
            }
            return any ? b.center : t.position;
        }

        static string GroundAt(Vector3 p)
        {
            var g = ZGame.I; if (!g) return "-";
            float hi = float.MinValue, lo = float.MaxValue;
            for (int j = -2; j <= 2; j++)
                for (int i = -2; i <= 2; i++) { float h = g.GroundY(p.x + i * 1.5f, p.z + j * 1.5f); hi = Mathf.Max(hi, h); lo = Mathf.Min(lo, h); }
            float sx = (g.GroundY(p.x + 3f, p.z) - g.GroundY(p.x - 3f, p.z)) / 6f, sz = (g.GroundY(p.x, p.z + 3f) - g.GroundY(p.x, p.z - 3f)) / 6f;
            return $"ground over 6 m round it: rise and fall {hi - lo:0.00} m, slope {Mathf.Atan(Mathf.Sqrt(sx * sx + sz * sz)) * Mathf.Rad2Deg:0} deg";
        }

        // ------------------------------------------------------------------ the report
        void Finish()
        {
            if (finished) return;
            finished = true;
            int counted = 0, wet = 0; float worstRise = 0f, worstStill = 0f, worstGapIn = 0f, worstGapOver = 0f; int worstOthers = 0; string worstRiseTag = "-", worstStillTag = "-";
            Line($"heavy landings planned {OzHeavyLanding.Plans}: slides cut by a rise {OzHeavyLanding.PlanCutRise}, by an edge {OzHeavyLanding.PlanCutEdge}, none on steep ground {OzHeavyLanding.PlanSteep}; last: {OzHeavyLanding.LastPlan}");
            foreach (var x in traces)
            {
                if (!x.landed || x.rows.Count < 2) { Line($"-    {x.tag}: no landing followed ({x.rows.Count} frames)"); continue; }
                if (!x.everDrawn) { Line($"-    {x.tag}: never drawn, not counted"); continue; }
                Line($"{x.tag} at {x.p0.x:0.0}, {x.p0.z:0.0}: {x.ground}; {x.plan}");
                Line("    frame  +ms   dx     dy     dz (cm, from its landing frame)  step (cm)  turn (deg)  lowest vertex over the ground (cm)  moved by");
                for (int i = 0; i < x.rows.Count && i <= FramesListed; i++)
                {
                    var r = x.rows[i]; var d = (r.p - x.p0) * 100f; float st = i > 0 ? (r.p - x.rows[i - 1].p).magnitude * 100f : 0f;
                    Line($"    {i,5} {r.t * 1000f,5:0} {d.x,6:0.0} {d.y,6:0.0} {d.z,6:0.0}   {st,6:0.0}   {r.turn,6:0.00}   {(float.IsNaN(r.gap) ? "  -" : (r.gap * 100f).ToString("0.0")),6}   {r.who}");
                }
                if (x.wet) { wet++; Line($"-    {x.tag}: taken by the water or lava rule, listed, not counted"); continue; }
                counted++;
                float riseP = 0f, riseM = 0f, still = 0f, stillStep = 0f;
                for (int i = 1; i < x.rows.Count; i++)
                {
                    var r = x.rows[i];
                    riseP = Mathf.Max(riseP, r.p.y - x.p0.y); riseM = Mathf.Max(riseM, r.mid.y - x.mid0.y);
                    if (x.rows[i - 1].t >= StillAfter) { float s = (r.p - x.rows[i - 1].p).magnitude; still += s; stillStep = Mathf.Max(stillStep, s); }
                }
                var end = x.rows[x.rows.Count - 1];
                Verdict(riseP <= RiseMax && riseM <= RiseMax, $"R {x.tag}: highest over its landing frame: pivot {riseP * 100f:0.0} cm, drawn box middle {riseM * 100f:0.0} cm (bar {RiseMax * 100f:0.0})");
                Verdict(still < StillMax, $"M {x.tag}: its path after the first {StillAfter:0.0} s {still * 100f:0.0} cm, largest step there {stillStep * 100f:0.0} cm (bar under {StillMax * 100f:0} cm; followed {end.t:0.00} s)");
                Verdict(x.others == 0, $"W {x.tag}: frames moved by anything but the heavy landing {x.others} (first: {x.firstOther})");
                Verdict(!float.IsNaN(end.gap) && end.gap >= -GapIn && end.gap <= GapOver, $"G {x.tag}: real lowest vertex {(float.IsNaN(end.gap) ? "not read" : (end.gap * 100f).ToString("+0.0;-0.0") + " cm")} over the ground at the end (bar -{GapIn * 100f:0} .. +{GapOver * 100f:0} cm)");
                if (Mathf.Max(riseP, riseM) > worstRise) { worstRise = Mathf.Max(riseP, riseM); worstRiseTag = x.tag; }
                if (still > worstStill) { worstStill = still; worstStillTag = x.tag; }
                worstOthers = Mathf.Max(worstOthers, x.others);
                if (!float.IsNaN(end.gap)) { worstGapIn = Mathf.Min(worstGapIn, end.gap); worstGapOver = Mathf.Max(worstGapOver, end.gap); }
            }
            Report = $"{pass} PASS, {fail} FAIL ({counted} turrets counted, {wet} in water or lava; worst rise {worstRise * 100f:0.0} cm ({worstRiseTag}), worst path after {StillAfter:0.0} s {worstStill * 100f:0.0} cm ({worstStillTag}), most frames moved by others {worstOthers}, rest gap {worstGapIn * 100f:+0.0;-0.0} .. {worstGapOver * 100f:+0.0;-0.0} cm)";
            Line(Report);
            try { Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "report.txt"), sb.ToString()); Line("report: " + Path.Combine(Folder, "report.txt")); }
            catch (IOException e) { Debug.LogWarning("[OpenZ wreck landing trace] report not written: " + e.Message); }
            if (me == this) me = null;
            Destroy(gameObject);
        }
    }

    /// <summary>The trace's sample at the frame's start (and nothing else).</summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class OzWreckLandingTraceEarly : MonoBehaviour
    {
        internal OzWreckLandingTrace main;
        void Update() { if (main) main.Early(); }
    }

    /// <summary>The trace's sample right after the heavy landing (order 0).</summary>
    [DefaultExecutionOrder(1)]
    public sealed class OzWreckLandingTraceMid : MonoBehaviour
    {
        internal OzWreckLandingTrace main;
        void Update() { if (main) main.Mid(); }
    }
}
#endif
