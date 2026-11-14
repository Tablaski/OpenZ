#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// The mech's feet measured on the DRAWN model every frame (editor only; the Play proof and the mech films use it). The mech's "tracks"
    /// standing foot on its ground spot (ZGame.GroundY) and turns its sole plate to the ground; the body (the unit's root, the drive wobble
    /// and the shot's rock) moves above them and the legs' IK takes it. Per foot and frame:
    ///  * planted (MechWalk's own flag, read by reflection) or swinging;
    ///  * the sole's lowest point against the ground under it (the sole = Mech_footL / _R's bottom face, its own vertices), against where
    ///    MechWalk stands that sole by its own design (its ankle height over the ground minus the ankle-to-sole depth: v2 0, v1's Model sits
    ///    3.5 cm up so its soles stand 3.5-4.3 cm over the ground, kept as it is): a planted foot more than 3 cm under or over that is counted;
    ///    a swinging foot more than 5 cm under the ground is counted (a long sole's toe near lift-off on a slope);
    ///  * a planted foot's drift and turn from where it landed (it must not move while it stands: the wobble never reaches it);
    ///  * the body's tilt (the unit root's up against the world's up): the body does rock.
    /// Turning in place (the drawn hull faster than 60 deg/s while the unit stands) is counted on its own.
    /// </summary>
    public sealed class MechFeetWatch
    {
        public readonly string Tag; public readonly UnitView View; public readonly int Model;
        MechWalk walk; FieldInfo legsF, plantedF, ankleHF;
        readonly float[] soleDepth = new float[2], restOver = { float.NaN, float.NaN };
        readonly Transform[] foot = new Transform[2];
        readonly Vector3[][] sole = new Vector3[2][];
        readonly bool[] was = new bool[2]; readonly Vector3[] at = new Vector3[2]; readonly Quaternion[] rot = new Quaternion[2];
        readonly bool[] spun = new bool[2]; readonly Vector3[] landAt = new Vector3[2]; FieldInfo startF;   // k86c
        /// <summary>k86c: the walk landed this foot again since the last read (a stance that began while the proof did not sample: its
        /// waits, shots and phase changes); read on every sample so the landing spot stays current.</summary>
        bool NewStance(int i)
        {
            if (!walk || legsF == null) return false;
            var arr = legsF.GetValue(walk) as Array;
            if (arr == null || arr.Length != 2) return false;
            var leg = arr.GetValue(i);
            if (startF == null) startF = leg.GetType().GetField("stanceStart");
            if (startF == null) return false;
            var s = (Vector3)startF.GetValue(leg); bool n = s != landAt[i]; landAt[i] = s;
            return n;
        }
        float lastYaw = float.NaN;
        public int Frames, PlantedFrames, SwingFrames, SinkFrames, FloatFrames, SwingSinkFrames, TurnFrames, TurnSinkFrames, Plants0 = -1;
        public float SoleLow = 99f, SoleHigh = -99f, SwingLow = 99f, DriftMaxCm, TurnMaxDeg, TiltMax, TurnSoleLow = 99f;
        public float TurnStanceDriftCm, TurnStanceYawDeg; public int TurnStances;                    // k86c: stances while the drawn hull turned faster than 60 deg/s (moving in the sim or not)
        public readonly List<string> Rows = new List<string>();

        public MechFeetWatch(string tag, UnitView v, int model)
        {
            Tag = tag; View = v; Model = model;
            if (!v) return;
            walk = v.GetComponentInChildren<MechWalk>(true);
            legsF = typeof(MechWalk).GetField("legs", BindingFlags.NonPublic | BindingFlags.Instance);
            int i = 0;
            foreach (var n in new[] { "Mech_footL", "Mech_footR" })
            {
                foreach (var t in v.GetComponentsInChildren<Transform>(true)) if (t.name == n) { foot[i] = t; break; }
                var mf = foot[i] ? foot[i].GetComponent<MeshFilter>() : null;
                var pts = new List<Vector3>();
                if (mf && mf.sharedMesh && mf.sharedMesh.isReadable)
                {
                    var vs = mf.sharedMesh.vertices; float lo = float.MaxValue;
                    foreach (var p in vs) lo = Mathf.Min(lo, p.y);
                    soleDepth[i] = -lo * Mathf.Abs(foot[i].lossyScale.y);                                 // the ankle (the foot node) to its sole (m)
                    foreach (var p in vs) if (p.y < lo + 0.02f) pts.Add(p);
                    int step = Mathf.Max(1, pts.Count / 120); var kept = new List<Vector3>();
                    for (int k = 0; k < pts.Count; k += step) kept.Add(pts[k]);
                    pts = kept;
                }
                sole[i] = pts.ToArray(); i++;
            }
        }
        public bool Ok => walk && legsF != null && foot[0] && foot[1] && sole[0].Length > 3 && sole[1].Length > 3;

        bool Planted(int i)
        {
            if (!walk || legsF == null) return true;
            var arr = legsF.GetValue(walk) as Array;
            if (arr == null || arr.Length != 2) return true;
            var leg = arr.GetValue(i);
            if (plantedF == null) { plantedF = leg.GetType().GetField("planted"); ankleHF = leg.GetType().GetField("ankleH"); }
            if (float.IsNaN(restOver[i]) && ankleHF != null) restOver[i] = (float)ankleHF.GetValue(leg) - soleDepth[i];   // MechWalk plants the ankle ankleH over the ground
            return plantedF == null || (bool)plantedF.GetValue(leg);
        }
        /// <summary>Where MechWalk stands each sole over the ground by its own design (m): v2 0, v1 about 0.035 / 0.043.</summary>
        public float RestOver(int i) => float.IsNaN(restOver[i]) ? 0f : restOver[i];

        /// <summary>Once per frame, after every LateUpdate (MechWalk posed the legs). frame / t / phase only label the CSV rows.</summary>
        public void Sample(ZGame g, int frame, float t, string phase, float dt)
        {
            if (!Ok || !View || View.Dying || g == null) return;
            if (Plants0 < 0) Plants0 = walk.Plants;
            Frames++;
            var root = View.transform;
            float tilt = Vector3.Angle(root.up, Vector3.up); TiltMax = Mathf.Max(TiltMax, tilt);
            float yaw = root.eulerAngles.y, rate = float.IsNaN(lastYaw) || dt <= 0f ? 0f : Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)) / dt; lastYaw = yaw;
            bool turning = rate > 60f && (View.Obj == null || !View.Obj.IsMoving);
            if (turning) TurnFrames++;
            for (int i = 0; i < 2; i++)
            {
                float lo = float.MaxValue;
                foreach (var p in sole[i]) { var w = foot[i].TransformPoint(p); lo = Mathf.Min(lo, w.y - g.GroundY(w.x, w.z)); }
                bool pl = Planted(i);
                float off = lo - RestOver(i);                                                             // against MechWalk's own stand of this sole
                if (pl)
                {
                    PlantedFrames++;
                    SoleLow = Mathf.Min(SoleLow, lo); SoleHigh = Mathf.Max(SoleHigh, lo);
                    if (off < -0.03f) SinkFrames++;
                    if (off > 0.03f) FloatFrames++;
                    if (turning) { TurnSoleLow = Mathf.Min(TurnSoleLow, lo); if (off < -0.03f) TurnSinkFrames++; }
                    bool fresh = NewStance(i);                                                                 // k86c: a new stance since the last sample
                    if (!was[i] || fresh) { at[i] = foot[i].position; rot[i] = foot[i].rotation; spun[i] = false; }   // k86c: + a stance begun while the proof did not sample starts its own measure
                    else
                    {
                        float dcm = (foot[i].position - at[i]).magnitude * 100f, ddeg = Quaternion.Angle(rot[i], foot[i].rotation);
                        DriftMaxCm = Mathf.Max(DriftMaxCm, dcm); TurnMaxDeg = Mathf.Max(TurnMaxDeg, ddeg);
                        if (rate > 60f) { if (!spun[i]) TurnStances++; spun[i] = true; }                         // k86c: the drawn hull turning on the spot (the sim may already move: the hull pivots first)
                        if (spun[i]) { TurnStanceDriftCm = Mathf.Max(TurnStanceDriftCm, dcm); TurnStanceYawDeg = Mathf.Max(TurnStanceYawDeg, ddeg); }
                    }
                }
                else
                {
                    SwingFrames++; SwingLow = Mathf.Min(SwingLow, lo);
                    if (lo < -0.05f) SwingSinkFrames++;
                    if (turning && lo < -0.05f) TurnSinkFrames++;
                }
                was[i] = pl;
                if (Rows.Count < 20000) Rows.Add($"{Tag},{frame},{t:0.000},{phase},{(i == 0 ? "L" : "R")},{(pl ? 1 : 0)},{lo:0.0000},{tilt:0.000},{rate:0.0},{(View.Obj != null && View.Obj.IsMoving ? 1 : 0)},{(Time.time - View.LastFireAt < 0.5f ? 1 : 0)}");
            }
        }
        public int Plants => walk && Plants0 >= 0 ? walk.Plants - Plants0 : 0;
        /// <summary>The verdict rule: the planted soles where MechWalk stands them (never more than 3 cm under or over), standing still while
        /// planted (3 cm, 2 deg), swinging feet never more than 5 cm under the ground, steps taken, and the body rocking above them.</summary>
        public bool Pass(bool turnsToo) => Ok && PlantedFrames > 30 && SinkFrames == 0 && FloatFrames == 0 && SwingSinkFrames == 0 && DriftMaxCm <= 3f && TurnMaxDeg <= 2f
                                           && (!turnsToo || TurnSinkFrames == 0);
        public string Summary() =>
            $"{Tag}: {Frames} frames, {Plants} steps; PLANTED {PlantedFrames} foot-frames: the sole's lowest point {SoleLow:0.000}..{SoleHigh:0.000} m over the ground (MechWalk stands it {RestOver(0):0.000} / {RestOver(1):0.000} m over), " +
            $"{SinkFrames} more than 3 cm under that, {FloatFrames} more than 3 cm over it; a standing foot moved at most {DriftMaxCm:0.0} cm and turned {TurnMaxDeg:0.0} deg; " +
            $"SWINGING {SwingFrames} foot-frames, lowest {SwingLow:0.000} m, {SwingSinkFrames} under the ground by more than 5 cm; " +
            $"TURNING IN PLACE {TurnFrames} frames, lowest planted sole {(TurnSoleLow < 98f ? TurnSoleLow.ToString("0.000") : "-")} m, {TurnSinkFrames} foot-frames more than 3 cm under; the body tilted up to {TiltMax:0.00} deg";
        public const string CsvHead = "unit,frame,t,phase,foot,planted,sole_lowest_over_ground_m,body_tilt_deg,hull_turn_dps,moving,fired";
    }

    /// <summary>The mech's pod rockets measured on the drawn model (editor only): every drawn rocket (OpenZFxPlayer.ProjTrace, FxWeapon.Rocket, its
    /// first frame) is matched to the nearest pod muzzle (MechRocketMuzzle_L0 / L1 / R2 / R3) of the watched mechs: how far out of the
    /// muzzle along its axis and off it it is first drawn (a rocket leaves its own tube: in front of a tube mouth, never inside the body), and
    /// the pods' kick (MechRocketPod_L / _R back from their rest).</summary>
    public sealed class MechRocketWatch
    {
        public readonly string Tag; public readonly UnitView View;
        readonly Transform[] mz = new Transform[4]; readonly Transform[] pod = new Transform[2]; readonly Vector3[] podRest = new Vector3[2];
        public int Rockets, Left, Right; public float AlongMin = 99f, AlongMax = -99f, OffMax, PodKickMax;
        public readonly List<string> RocketRows = new List<string>();
        static readonly string[] Names = { "MechRocketMuzzle_L0", "MechRocketMuzzle_L1", "MechRocketMuzzle_R2", "MechRocketMuzzle_R3" };
        public MechRocketWatch(string tag, UnitView v)
        {
            Tag = tag; View = v; if (!v) return;
            foreach (var t in v.GetComponentsInChildren<Transform>(true))
            {
                int k = Array.IndexOf(Names, t.name); if (k >= 0) mz[k] = t;
                if (t.name == "MechRocketPod_L") { pod[0] = t; podRest[0] = t.localPosition; }
                if (t.name == "MechRocketPod_R") { pod[1] = t; podRest[1] = t.localPosition; }
            }
        }
        public bool Ok => Array.TrueForAll(mz, t => t) && pod[0] && pod[1];
        public void Sample() { if (!Ok || !View || View.Dying) return; for (int i = 0; i < 2; i++) PodKickMax = Mathf.Max(PodKickMax, (pod[i].localPosition - podRest[i]).magnitude); }
        static readonly List<MechRocketWatch> live = new List<MechRocketWatch>(); static readonly HashSet<int> seen = new HashSet<int>();
        static Action<int, FxWeapon, float, float, Vector3, bool> trace;
        public static void Watch(IEnumerable<MechRocketWatch> ws)
        {
            live.Clear(); live.AddRange(ws); seen.Clear();
            if (trace == null) { trace = OnTrace; OpenZFxPlayer.ProjTrace += trace; }
        }
        public static void Stop() { if (trace != null) { OpenZFxPlayer.ProjTrace -= trace; trace = null; } live.Clear(); }
        static void OnTrace(int serial, FxWeapon w, float age, float life, Vector3 pos, bool ease)
        {
            if (w != FxWeapon.Rocket || !seen.Add(serial)) return;
            MechRocketWatch best = null; int bk = -1; float bd = 1.6f;
            foreach (var x in live)
            {
                if (!x.Ok || !x.View || x.View.Dying) continue;
                for (int k = 0; k < 4; k++) { float d = Vector3.Distance(x.mz[k].position, pos); if (d < bd) { bd = d; best = x; bk = k; } }
            }
            if (best == null) return;
            var m = best.mz[bk]; var axis = -m.forward; var off = pos - m.position;
            float along = Vector3.Dot(off, axis), perp = (off - axis * along).magnitude;
            best.Rockets++; if (bk < 2) best.Left++; else best.Right++;
            best.AlongMin = Mathf.Min(best.AlongMin, along); best.AlongMax = Mathf.Max(best.AlongMax, along); best.OffMax = Mathf.Max(best.OffMax, perp);
            if (best.RocketRows.Count < 64) best.RocketRows.Add($"{best.Tag},{serial},{Names[bk]},{age:0.0000},{along:0.0000},{perp:0.0000}");
        }
        public bool Pass => Ok && Rockets >= 4 && Left >= 2 && Right >= 2 && AlongMin > -0.05f && AlongMax < 1.2f && PodKickMax > 0.05f;
        public string Summary() => $"{Tag}: {Rockets} drawn rockets at a pod muzzle ({Left} left pod, {Right} right pod), first drawn {AlongMin:0.000}..{AlongMax:0.000} m out of the muzzle along its axis, " +
                                   $"at most {OffMax:0.000} m off it (they fly to the target); the pods kicked back up to {PodKickMax:0.000} m";
    }

    /// <summary>
    /// Mech v2 Play proof (editor only; the MLRS v2's proof, View/MlrsV2Proof.cs, for the mech): one auto-started run per level (PlayerPrefs
    /// oz_mc2proof_auto = "L02" / "L17", set by the menu OpenZ/Mech v2/3). v1 and v2 side by side (pinned as the testing panel pins them), red,
    /// blue and neutral, on the proof's FIXED open lineup (View/HeavyV2Spots.cs "proof": chosen on a scan of the whole map with the sim's own
    /// tests; a mech fits where a heavy does; every slot asked again here, a protected or blocked slot stops the proof loudly, lesson 7);
    /// nobody fights while posing. Day and night frames at the game's play zoom and close (32 m), the empty v1 and v2 measured side by side
    /// (the empty look: dark grey like v1, never near white); then the red pair is ordered to attack a blue jeep (the game's own Attack order;
    /// the jeep cannot be destroyed and has no weapon): the waist turns, the cannon aims, kicks, slides and spins, the upper body kicks back
    /// and the body rocks back with an eased wobble (lesson 6), every rocket leaves a pod muzzle and the pods kick, the shot sounds; the red
    /// pair then walks away and back (it turns in place first), the feet planted on the ground EVERY frame while the body rocks above them
    /// (item 146, the mech's tracks are its feet: MechFeetWatch), with the step sounds; the lamp and blinker data each model uses; an empty
    /// v1 and v2 boarded by a red grunt each (the hatch opens while he climbs in); damage smoke at 45 % and fire at 20 %, then both red ones
    /// destroyed (blast, the upper body tossed, the legs breaking into parts, wreck) with a frame burst at 10 fps for the MP4. Each step a
    /// counted verdict for v1 AND v2.
    /// </summary>
    public class MechV2Proof : MonoBehaviour
    {
        static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.."));
        static readonly string Dir = Path.Combine(Root, "work/staging/v1_2_20261022/v2_units/mech/proof");
        public const string AutoPref = "oz_mc2proof_auto";
        static OzUnitModels M => OzMech.Models;
        string job; float lastMark; bool done; int errors; float hot = -1f, lastSample = -1f; int peak = -1;
        readonly StringBuilder log = new StringBuilder();
        readonly List<string> verdicts = new List<string>();
        readonly List<string> emptyLook = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Auto()
        {
            string job = PlayerPrefs.GetString(AutoPref, "");
            if (string.IsNullOrEmpty(job)) return;
            PlayerPrefs.DeleteKey(AutoPref); PlayerPrefs.Save();
            var go = new GameObject("MechV2Proof"); DontDestroyOnLoad(go);
            go.AddComponent<MechV2Proof>().job = job;
        }

        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        float T => (float)clock.Elapsed.TotalSeconds;
        void Mark(string s) { lastMark = T; log.AppendLine($"[{T,7:0.00}] {s}"); Debug.Log("[mc2proof] " + s); }
        void Verdict(string step, bool ok, string what) { string v = $"{step}: {(ok ? "PASS" : "FAIL")} - {what}"; verdicts.Add(v); Mark("VERDICT " + v); }
        IEnumerator Wait(float s) { float end = T + s; while (T < end && !done) yield return null; }
        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; MechRocketWatch.Stop(); if (!done) Finish("disabled"); }
        void OnLog(string c, string st, LogType type)
        {
            if (c.StartsWith("[mc2proof]")) return;
            if (type == LogType.Error || type == LogType.Exception) { errors++; log.AppendLine($"[{T,7:0.00}] ERROR {c.Split('\n')[0]}"); }
        }

        static int GpuTemp()
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType("OpenZ.EditorTools.GpuGuard"); if (t == null) continue;
                var p = t.GetProperty("LastTemp"); return p != null ? (int)p.GetValue(null) : -1;
            }
            return -1;
        }
        IEnumerator Heat()
        {   // the same guard as the other Play proofs: stop at 90 C, or at 86 C held 30 s; a watchdog when nothing moves on for 90 s
            while (!done)
            {
                int gpu = GpuTemp(); peak = Mathf.Max(peak, gpu);
                float step = lastSample < 0f ? 0.5f : T - lastSample; lastSample = T;
                if (gpu >= 90) { Finish("GPU 90 C"); yield break; }
                if (gpu >= 86) { hot = hot < 0f ? step : hot + step; if (hot >= 30f) { Finish("GPU >= 86 C for 30 s"); yield break; } }
                else if (gpu >= 0 && gpu < 84) hot = -1f;
                if (T - lastMark > 90f) { Finish("watchdog: no progress for 90 s"); yield break; }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        ZGame g; ZWorld W; RtsCamera cam; DayNight dn; Camera ucam;
        bool Spot(string what, out int x, out int y) => HeavyV2Spots.Get(g.level, "proof", what, out x, out y);
        /// <summary>Every posed mech asked again with the sim's own tests (ZPathFinder.StepOk, ZWorld.SnipeSafeSpot): all open = true.</summary>
        bool SlotsOpen(out string why)
        {
            var bad = new List<string>();
            foreach (var (o, tag) in units)
            {
                if (o.Type != OzMech.Type) continue;
                int tx = o.LocX >> 4, ty = o.LocY >> 4;
                bool foot = W.Path.StepOk(tx, ty, tx, ty, false, false), prot = W.SnipeSafeSpot(o);
                if (!foot || prot) bad.Add($"{tag} at tile ({tx},{ty}): footprint {(foot ? "passes" : "BLOCKED")}, bridge / door spot {prot}");
            }
            why = string.Join("; ", bad);
            return bad.Count == 0;
        }
        static readonly FieldInfo RigsField = typeof(BoardingView).GetField("rigs", BindingFlags.NonPublic | BindingFlags.Instance);
        static object RigOf(UnitView v)
        {
            if (!v || !BoardingView.I || RigsField == null) return null;
            var d = RigsField.GetValue(BoardingView.I) as IDictionary;
            return d != null && d.Contains(v) ? d[v] : null;
        }
        static T RigGet<T>(object rig, string field, T fallback)
        {
            var f = rig?.GetType().GetField(field); if (f == null) return fallback;
            var o = f.GetValue(rig); return o is T t ? t : fallback;
        }
        /// <summary>The data entry ("type" of its Spec) VehicleLights or UnitBlinkers rigged this view with (read only, by reflection); "-" = none yet.</summary>
        static string SpecType(MonoBehaviour comp, UnitView v)
        {
            if (!comp || !v) return "-";
            var f = comp.GetType().GetField("rigOf", BindingFlags.NonPublic | BindingFlags.Instance);
            var d = f != null ? f.GetValue(comp) as IDictionary : null;
            if (d == null || !d.Contains(v) || d[v] == null) return "-";
            var rig = d[v]; var sf = rig.GetType().GetField("Spec"); var spec = sf != null ? sf.GetValue(rig) : null;
            var tf = spec != null ? spec.GetType().GetField("type") : null;
            return tf != null ? (string)tf.GetValue(spec) : "-";
        }
        static float LidY(Transform hinge)
        {
            if (!hinge) return 0f; float y = 0f; int n = 0;
            foreach (var r in hinge.GetComponentsInChildren<MeshRenderer>(true)) { y += r.bounds.center.y; n++; }
            return n > 0 ? y / n : hinge.position.y;
        }
        static readonly FieldInfo TossedF = typeof(UnitView).GetField("tossed", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo BreakDoneF = typeof(UnitView).GetField("breakDone", BindingFlags.NonPublic | BindingFlags.Instance);
        void Focus(int tx, int ty, float h) { var p = ZGame.PxToWorld(tx * 16 + 8, ty * 16 + 8); cam.focus = new Vector3(p.x, 0f, p.z); cam.SetHeightImmediate(h); }

        IEnumerator SkipBriefing()
        {
            float w0 = T; bool aborted = false; var pf = typeof(ZodBriefing).GetField("perf", BindingFlags.NonPublic | BindingFlags.Instance);
            double sim0 = W.Time;
            while (T - w0 < 45f && !done)
            {
                if (ZodBriefing.BriefingActive)
                {
                    if (!aborted && ZodBriefing.I && pf != null && T - w0 > 0.6f)
                    {
                        var p = pf.GetValue(ZodBriefing.I);
                        if (p != null) { p.GetType().GetMethod("Abort").Invoke(p, new object[] { 0.22f }); aborted = true; Mark("briefing: Abort(0.22 s) sent"); }
                    }
                }
                else if (!g.paused && W.Time > sim0 + 0.5) break;
                yield return null;
            }
            Mark($"briefing over after {T - w0:0.0} s");
        }

        // ---------------------------------------------------------------- units
        readonly List<(ZObject o, string tag)> units = new List<(ZObject, string)>();
        ZObject Mech(int model, int tx, int ty, Team t, string tag)
        {
            var o = W.CreateObject("vehicle", OzMech.Type, tx * 16, ty * 16, t);
            M.Pin(o, model); g.SpawnUnitView(o);
            o.MaxHealth = 100000000; o.SetHealth(o.MaxHealth); o.Damage = 0; o.AttackRadius = 0;
            units.Add((o, tag)); return o;
        }
        UnitView View(ZObject o) => o != null && g.Views.TryGetValue(o.Id, out var v) ? v : null;
        static Transform Node(UnitView v, string name)
        {
            if (!v) return null;
            foreach (var t in v.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
        string Boxes()
        {
            var sb = new StringBuilder();
            foreach (var (o, tag) in units)
            {
                var v = View(o); if (!v) continue;
                bool any = false; var b = new Bounds();
                foreach (var r in v.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                if (!any) continue;
                float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1));
                    var s = ucam.WorldToScreenPoint(c);
                    x0 = Mathf.Min(x0, s.x); x1 = Mathf.Max(x1, s.x); y0 = Mathf.Min(y0, Screen.height - s.y); y1 = Mathf.Max(y1, Screen.height - s.y);
                }
                sb.Append($"{tag}={x0:0},{y0:0},{x1:0},{y1:0} ");
            }
            return sb.ToString();
        }
        IEnumerator Shot(string name, float settle = 0.5f)
        {
            yield return Wait(settle);
            string f = Path.Combine(Dir, $"{g.level}_{name}.png");
            ScreenCapture.CaptureScreenshot(f); yield return null; yield return null;
            Mark($"frame {Path.GetFileName(f)} {Screen.width}x{Screen.height} cam {cam.height:0} m | {Boxes()}");
        }
        IEnumerator TimeOfDay(float tod) { dn.timeOfDay = tod; dn.Apply(tod); yield return Wait(1.4f); }
        int Count(float t0, params string[] keys)
        {
            int n = 0;
            foreach (var e in OzSoundLog.Since(t0)) if (Array.IndexOf(keys, e.key) >= 0 && e.outcome != null && e.outcome.StartsWith("PLAY")) n++;
            return n;
        }

        static List<Vector3> TopPoints(UnitView v)
        {
            var pts = new List<Vector3>();
            if (!v) return pts;
            foreach (var mf in v.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh; var r = mf.GetComponent<Renderer>();
                if (!m || !r || !r.enabled || !m.isReadable) continue;
                string n = mf.name;
                if (!(n == "Mech_body" || n == "Mech_pelvis" || n == "Mech_thighL" || n == "Mech_thighR" || n == "Mech_clawUpper")) continue;
                var vs = m.vertices; var ns = m.normals; if (ns == null || ns.Length != vs.Length) continue;
                int step = Mathf.Max(1, vs.Length / 300);
                for (int i = 0; i < vs.Length; i += step)
                    if (mf.transform.TransformDirection(ns[i]).y > 0.35f) pts.Add(mf.transform.TransformPoint(vs[i]));
            }
            return pts;
        }
        static float Lum(Color c)
        {
            var l = c.linear; float y = 0.2126f * l.r + 0.7152f * l.g + 0.0722f * l.b;
            return 255f * (y <= 0.0031308f ? y * 12.92f : 1.055f * Mathf.Pow(y, 1f / 2.4f) - 0.055f);
        }
        IEnumerator EmptyLook(ZObject n1, ZObject n2, string frame)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                var res = new List<(string tag, float mean, float white, int n)>();
                foreach (var (o, tag) in new[] { (n1, "v1"), (n2, "v2") })
                {
                    float sum = 0f; int n = 0, white = 0;
                    foreach (var p in TopPoints(View(o)))
                    {
                        var s = ucam.WorldToScreenPoint(p);
                        if (s.z <= 0f || s.x < 0 || s.y < 0 || s.x >= tex.width || s.y >= tex.height) continue;
                        float L = Lum(tex.GetPixel((int)s.x, (int)s.y)); sum += L; n++; if (L > 200f) white++; OzV2EmptyLook.Tally(tag, L);   // k98: the two-tone rule
                    }
                    res.Add((tag, n > 0 ? sum / n : -1f, n > 0 ? 100f * white / n : -1f, n));
                }
                bool ok = OzV2EmptyLook.Verdict(false) && res[0].n > 20 && res[1].n > 20;   // k98, item 163 (View/OzV2EmptyLook.cs)
                string line = $"{g.level} {frame}: empty v1 mean sRGB {res[0].mean:0} ({res[0].white:0.0} % near white, {res[0].n} points), empty v2 mean {res[1].mean:0} ({res[1].white:0.0} % near white, {res[1].n} points): {(ok ? "PASS" : "FAIL")} ({OzV2EmptyLook.LastRule})";
                emptyLook.Add(line); Mark("EMPTY LOOK " + line);
            }
            finally { Destroy(tex); }
        }

        // ---------------------------------------------------------------- the run
        IEnumerator Start()
        {
            Directory.CreateDirectory(Dir); lastMark = T;
            Mark($"Mech v2 Play proof {job}, scene {SceneManager.GetActiveScene().name}, screen {Screen.width}x{Screen.height}, switch OzMech.Version {OzMech.Version}, v2 prefab {(M.PrefabV2 ? "found" : "MISSING")}, MECH FOOT IK {(MechWalk.On ? "ON" : "OFF")}");
            StartCoroutine(Heat());
            float w0 = T;
            while ((!ZGame.I || ZGame.I.World == null) && T - w0 < 90f) yield return null;
            if (!ZGame.I || ZGame.I.World == null) { Finish("the level never came up within 90 s"); yield break; }
            g = ZGame.I; W = g.World; cam = FindFirstObjectByType<RtsCamera>(); dn = FindFirstObjectByType<DayNight>(); ucam = cam.GetComponent<Camera>(); if (!ucam) ucam = Camera.main;
            float h0 = cam.height;
            int bots = W.Bots.Count; W.Bots.Clear();
            yield return SkipBriefing();
            if (done) yield break;
            bool run0 = dn.running; dn.running = false;
            Mark($"level {g.level}, default RTS height {h0:0.0} m; CPU bots cleared ({bots}); day/night clock stopped");
            Verdict("v2_prefab", M.PrefabV2, "Resources/OpenZ/MechV2/Z_medium_v2 (menu OpenZ/Mech v2/1)");
            if (!M.PrefabV2) { Finish("no v2 prefab"); yield break; }
            {   // mechs placed in the level scene at the start are drawn as the switch says (ZGame.BindViews -> OzV2Units.SceneView)
                int placed = 0, asV2 = 0;
                foreach (var o in W.Objects)
                {
                    if (o.Type != OzMech.Type || o.Source == null) continue;
                    var v = View(o); if (!v) continue;
                    placed++; if (M.IsV2(v.transform)) asV2++;
                }
                Verdict("scene_mechs", asV2 == (OzMech.Version == 2 ? placed : 0),
                        $"{placed} mechs placed in the level scene, {asV2} drawn as v2 (switch {OzMech.Version}, {M.SceneSwaps} scene view(s) taken over){(placed == 0 ? "; none on this map" : "")}");
            }

            var spots = new Dictionary<string, (int x, int y)>();
            foreach (var n in new[] { "red_v1", "red_v2", "blue_v1", "blue_v2", "neutral_v1", "neutral_v2", "jeep", "centre" })
            {
                if (!Spot(n, out int sx, out int sy)) { Mark($"SPOT FAIL: no fixed tile {g.level}/proof/{n} in HeavyV2Spots"); Finish("spot check failed: " + n); yield break; }
                spots[n] = (sx, sy);
            }
            Mark("fixed lineup (View/HeavyV2Spots.cs, the proof's open lineup): " + string.Join(", ", new List<string>(spots.Keys).ConvertAll(k => $"{k} {spots[k]}")));
            var r1 = Mech(1, spots["red_v1"].x, spots["red_v1"].y, Team.Red, "red_v1"); var r2 = Mech(2, spots["red_v2"].x, spots["red_v2"].y, Team.Red, "red_v2");
            var b1 = Mech(1, spots["blue_v1"].x, spots["blue_v1"].y, Team.Blue, "blue_v1"); var b2 = Mech(2, spots["blue_v2"].x, spots["blue_v2"].y, Team.Blue, "blue_v2");
            var n1 = Mech(1, spots["neutral_v1"].x, spots["neutral_v1"].y, Team.Neutral, "neutral_v1"); var n2 = Mech(2, spots["neutral_v2"].x, spots["neutral_v2"].y, Team.Neutral, "neutral_v2");
            yield return Wait(2.0f);
            int v2Views = 0, v1Views = 0;
            foreach (var (a, b) in new[] { (r1, r2), (b1, b2), (n1, n2) })
            {
                var va = View(a); var vb = View(b);
                if (va && !M.IsV2(va.transform)) v1Views++;
                if (vb && M.IsV2(vb.transform)) v2Views++;
            }
            Verdict("models", v1Views == 3 && v2Views == 3, $"{v1Views}/3 views drawn as v1, {v2Views}/3 as v2 (pinned as the testing panel pins them)");
            Mark("lineup spots (a bridge / door spot shuts every lid at once, DV82 / DV87 B): " + string.Join(", ", units.ConvertAll(u => $"{u.tag} {(W.SnipeSafeSpot(u.o) ? "ON ONE" : "open")}")));
            if (!SlotsOpen(out string spotWhy)) { Mark("SPOT FAIL: " + spotWhy); Finish("spot check failed: " + spotWhy); yield break; }
            {
                var va = View(r1); var vb = View(r2);
                var rf = typeof(UnitView).GetField("radius", BindingFlags.NonPublic | BindingFlags.Instance);
                float ra = va && rf != null ? (float)rf.GetValue(va) : -1f, rb = vb && rf != null ? (float)rf.GetValue(vb) : -1f;
                Verdict("footprint", va && vb && Mathf.Abs(ra - rb) < 0.06f, $"UnitView radius v1 {ra:0.00} m, v2 {rb:0.00} m (spacing / no touching)");
                var wa = va ? va.GetComponentInChildren<MechWalk>() : null; var wb = vb ? vb.GetComponentInChildren<MechWalk>() : null;
                Verdict("walk_rig", wa && wb && wa.Drives == wb.Drives, $"MechWalk on v1 {(wa ? (wa.Drives ? "stepping" : "legacy / not started") : "MISSING")}, v2 {(wb ? (wb.Drives ? "stepping" : "legacy / not started") : "MISSING")} (MECH FOOT IK {(MechWalk.On ? "ON" : "OFF")})");
            }

            // day + night frames, the empty look
            yield return TimeOfDay(0.08f);
            Focus(spots["centre"].x, spots["centre"].y, h0); yield return Shot("mc2_A_day_rts");
            yield return EmptyLook(n1, n2, "rts");
            Focus((spots["red_v1"].x + spots["red_v2"].x) / 2 + 1, spots["red_v2"].y, 32f); yield return Shot("mc2_A_day_close_red");
            Focus((spots["blue_v1"].x + spots["blue_v2"].x) / 2 + 1, spots["blue_v2"].y, 32f); yield return Shot("mc2_A_day_close_blue");
            Focus((spots["neutral_v1"].x + spots["neutral_v2"].x) / 2 + 1, spots["neutral_v2"].y, 32f); yield return Shot("mc2_A_day_close_neutral");
            yield return EmptyLook(n1, n2, "close");
            yield return TimeOfDay(0.62f);
            Focus(spots["centre"].x, spots["centre"].y, h0); yield return Shot("mc2_A_night_rts", 0.8f);
            Focus((spots["red_v1"].x + spots["red_v2"].x) / 2 + 1, spots["red_v2"].y, 32f); yield return Shot("mc2_A_night_close_red");
            Focus((spots["neutral_v1"].x + spots["neutral_v2"].x) / 2 + 1, spots["neutral_v2"].y, 32f); yield return Shot("mc2_A_night_close_neutral");
            {   // the lamps and blinkers: each model on its own data entry (v2: "medium@v2", measured on v2's meshes)
                string l1 = SpecType(VehicleLights.I, View(r1)), l2 = SpecType(VehicleLights.I, View(r2)), u1 = SpecType(UnitBlinkers.I, View(r1)), u2 = SpecType(UnitBlinkers.I, View(r2));
                Verdict("lights_data", l1 == OzMech.Type && l2 == M.LightsKey && (u1 == "-" || u1 == OzMech.Type) && (u2 == "-" || u2 == M.LightsKey),
                        $"VehicleLights v1 '{l1}', v2 '{l2}'; UnitBlinkers v1 '{u1}', v2 '{u2}' (v2 must use '{M.LightsKey}'; '-' = the blinkers are off in this run)");
            }
            yield return TimeOfDay(0.08f);
            if (done) yield break;

            // fire: the red pair is ordered to attack an unkillable blue jeep (no weapon). Every frame: the waist, the cannon, the upper body's kick
            // and the body's rock (HeavyRecoil), the pod rockets (MechRocketWatch), the feet against the ground (MechFeetWatch)
            var jeep = W.CreateObject("vehicle", "jeep", spots["jeep"].x * 16, spots["jeep"].y * 16, Team.Blue); g.SpawnUnitView(jeep);
            jeep.MaxHealth = 100000000; jeep.SetHealth(jeep.MaxHealth); jeep.Damage = 0; jeep.AttackRadius = 0; jeep.MoveSpeed = 0; units.Add((jeep, "target"));   // MoveSpeed 0: Zod's DodgeMissile never moves it out of the mechs' reach (the mech fires missiles)
            foreach (var (o, _) in units) if (o != jeep && o.Owner == Team.Blue) o.AttackRadius = 0;
            var vv = new[] { View(r1), View(r2) };
            var feet = new[] { new MechFeetWatch("v1", vv[0], 1), new MechFeetWatch("v2", vv[1], 2) };
            var pods = new[] { new MechRocketWatch("v1", vv[0]), new MechRocketWatch("v2", vv[1]) };
            Verdict("mech_nodes", feet[0].Ok && feet[1].Ok && pods[0].Ok && pods[1].Ok,
                    $"MechWalk legs, both feet with their soles, the 4 pod muzzles and both pods found on v1 {feet[0].Ok && pods[0].Ok} and v2 {feet[1].Ok && pods[1].Ok}");
            MechRocketWatch.Watch(pods);
            foreach (var o in new[] { r1, r2 })
            {
                o.Damage = o.U.Damage; o.AttackRadius = o.U.attack_radius;
                W.SendWaypoints(o.Owner, o, new List<ZWaypoint> { new ZWaypoint { mode = WpMode.Attack, refId = jeep.Id, x = jeep.CenterX, y = jeep.CenterY } });
            }
            Mark($"the red pair ordered to attack the jeep at tile ({spots["jeep"].x},{spots["jeep"].y}) (the game's Attack order; mech range {r1.U.attack_radius} px)");
            Focus((spots["red_v2"].x + spots["jeep"].x) / 2, (spots["red_v2"].y + spots["jeep"].y) / 2, 30f);
            float t0 = Time.unscaledTime; float fw = T; int kick0 = HeavyRecoil.Kicks;
            var waist = new Transform[2]; var waistRest = new Quaternion[2]; var yawMax = new float[2];
            var recoil = new Transform[2]; var recoilRest = new Vector3[2]; var slideMax = new float[2];
            var spin = new Transform[2]; var spinLast = new Quaternion[2]; var spinSum = new float[2];
            var headS = new float[2]; var headQ = new float[2]; var bodyQ = new float[2]; var bodyS = new float[2]; var rocks = new int[2]; var wasRock = new bool[2];
            for (int i = 0; i < 2; i++)
            {
                waist[i] = Node(vv[i], "MechWaistYaw"); recoil[i] = Node(vv[i], "MechGunRecoil"); spin[i] = Node(vv[i], "MechBarrelSpin");
                if (waist[i]) waistRest[i] = waist[i].localRotation;
                if (recoil[i]) recoilRest[i] = recoil[i].localPosition;
                if (spin[i]) spinLast[i] = spin[i].localRotation;
            }
            int burst = 0; float nextB = T + 1.0f; int fr = 0; float last = T;
            while (T - fw < 20f && !done)
            {
                yield return new WaitForEndOfFrame();
                float dt = Mathf.Max(1e-4f, T - last); last = T; fr++;
                for (int i = 0; i < 2; i++)
                {
                    var v = vv[i]; if (!v) continue;
                    feet[i].Sample(g, fr, T - fw, "firing", dt); pods[i].Sample();
                    if (waist[i]) yawMax[i] = Mathf.Max(yawMax[i], Quaternion.Angle(waistRest[i], waist[i].localRotation));
                    if (recoil[i]) slideMax[i] = Mathf.Max(slideMax[i], (recoil[i].localPosition - recoilRest[i]).magnitude);
                    if (spin[i]) { spinSum[i] += Quaternion.Angle(spinLast[i], spin[i].localRotation); spinLast[i] = spin[i].localRotation; }
                    var hr = v.HeavyRecoilView;
                    if (hr)
                    {
                        headS[i] = Mathf.Max(headS[i], hr.HeadSlideNow); headQ[i] = Mathf.Max(headQ[i], hr.HeadRockNow);
                        bodyQ[i] = Mathf.Max(bodyQ[i], hr.BodyRockNow); bodyS[i] = Mathf.Max(bodyS[i], hr.BodyPushNow);
                        bool rk = hr.BodyRockNow > 0.15f; if (rk && !wasRock[i]) rocks[i]++; wasRock[i] = rk;
                    }
                }
                if (T >= nextB && burst < 16) { ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"{g.level}_mc2_B_fire_{burst:00}.png")); burst++; nextB = T + 0.9f; }
            }
            int rockets = Count(t0, "mech_rocket"), flights = Count(t0, "mech_flight");
            for (int i = 0; i < 2; i++)
            {
                string tag = i == 0 ? "v1" : "v2";
                Verdict("waist_turn_" + tag, yawMax[i] > 15f, $"the upper body (MechWaistYaw) turned up to {yawMax[i]:0} deg toward the jeep");
                Verdict("cannon_" + tag, slideMax[i] > 0.1f && spinSum[i] > 90f, $"the cannon slid back up to {slideMax[i]:0.000} m (MechGunRecoil, model units) and its muzzle ring spun {spinSum[i]:0} deg in all (MechBarrelSpin)");
                var hr = vv[i] ? vv[i].HeavyRecoilView : null;
                float dsl = HeavyRecoil.DesignHeadSlide(OzMech.Type, i + 1), drk = HeavyRecoil.DesignHeadRock(OzMech.Type), dbr = HeavyRecoil.DesignBodyRock(OzMech.Type);
                Verdict("upper_body_kick_" + tag, hr && headS[i] > 0.6f * dsl && headS[i] < 1.4f * dsl && headQ[i] > 0.6f * drk && headQ[i] < 1.4f * drk,
                        $"the upper body kicked back {headS[i]:0.000} (design {dsl:0.000}, the waist's parent units) and rocked its front up {headQ[i]:0.00} deg (design {drk:0.0}), eased home (HeavyRecoil, model {(hr ? hr.Model : 0)})");
                Verdict("body_rock_" + tag, bodyQ[i] > 0.6f * dbr && bodyQ[i] < 1.5f * dbr && rocks[i] >= 1,
                        $"the body rocked back {bodyQ[i]:0.00} deg (design {dbr:0.00}) and slid {bodyS[i]:0.000} m, eased out ({rocks[i]} rocks)");
                Verdict("rockets_" + tag, pods[i].Pass, pods[i].Summary());
            }
            Verdict("fire_sound", rockets >= 4 && HeavyRecoil.Kicks - kick0 >= 2, $"sound log: mech_rocket {rockets} (two per shot), mech_flight {flights} in 20 s; HeavyRecoil kicks {HeavyRecoil.Kicks - kick0}");
            foreach (var (o, _) in units) { o.Damage = 0; o.AttackRadius = 0; o.AttackObject = null; }
            MechRocketWatch.Stop();
            if (done) yield break;

            // walk: the red pair walks 7 tiles away and back (each turns in place first, as the game's mech does), stops, settles; every frame the feet
            {
                float ts = Time.unscaledTime;
                var goals = new[] { (dx: 0, dy: -7), (dx: 0, dy: 0) };
                Focus((spots["red_v1"].x + spots["red_v2"].x) / 2 + 1, spots["red_v2"].y - 3, 40f);
                foreach (var (dx, dy) in goals)
                {
                    foreach (var (o, sp) in new[] { (r1, spots["red_v1"]), (r2, spots["red_v2"]) })
                        W.SendWaypoints(o.Owner, o, new List<ZWaypoint> { ZWaypoint.Move((sp.x + dx) * 16 + 16, (sp.y + dy) * 16 + 16, false) });
                    fw = T;
                    while (T - fw < 14f && !done)
                    {
                        yield return new WaitForEndOfFrame();
                        float dt = Mathf.Max(1e-4f, T - last); last = T; fr++;
                        for (int i = 0; i < 2; i++) feet[i].Sample(g, fr, T - fw, dy < 0 ? "walk away" : "walk back", dt);
                        if (T - fw > 3f && !r1.IsMoving && !r2.IsMoving) break;
                    }
                    yield return Wait(1.5f);
                    if (dy < 0) yield return Shot("mc2_C_walked", 0f);
                }
                for (int i = 0; i < 2; i++)
                {
                    Mark("FEET " + feet[i].Summary());
                    Verdict("feet_planted_" + feet[i].Tag, feet[i].Pass(false) && feet[i].Plants >= 6 && feet[i].TiltMax > 0.3f,
                            feet[i].Summary() + " (the standing feet on the ground and still, the body rocking above them: item 146 for the mech)");
                    Verdict("feet_turning_" + feet[i].Tag, feet[i].TurnSinkFrames == 0,
                            $"turning in place: {feet[i].TurnFrames} frames, the lowest planted sole {(feet[i].TurnSoleLow < 98f ? feet[i].TurnSoleLow.ToString("0.000") : "-")} m, {feet[i].TurnSinkFrames} foot-frames more than 3 cm under the ground");
                    Verdict("feet_turn_lock_" + feet[i].Tag, feet[i].TurnStances > 0 && feet[i].TurnStanceDriftCm < 5f && feet[i].TurnStanceYawDeg < 5f,
                            $"k86c: {feet[i].TurnStances} stances while the drawn hull turned faster than 60 deg/s: a standing foot moved at most {feet[i].TurnStanceDriftCm:0.0} cm and turned {feet[i].TurnStanceYawDeg:0.0} deg (pass: under 5 cm and 5 deg); MechWalk: quick landings {MechWalk.QuickLands}, stances over {MechWalk.WarnCm:0} cm / {MechWalk.WarnDeg:0} deg {MechWalk.StanceWarnN}, largest drawn {MechWalk.StanceMoveMaxCm:0.0} cm / {MechWalk.StanceTurnMaxDeg:0.0} deg, frames not posed {MechWalk.UnposedFrames}");
                }
                Verdict("step_sounds", Count(ts, "mech_step") >= 6, $"sound log: mech_step {Count(ts, "mech_step")} while the pair walked (one per plant), mech_servo / mech_stomp {Count(ts, "mech_servo", "mech_stomp")}");
                var csv = new StringBuilder(MechFeetWatch.CsvHead + "\n");
                foreach (var f in feet) foreach (var r in f.Rows) csv.Append(r).Append('\n');
                File.WriteAllText(Path.Combine(Dir, g.level + "_mc2_feet.csv"), csv.ToString());
                var rcsv = new StringBuilder("unit,serial,muzzle,age_s,along_m,off_axis_m\n");
                foreach (var p in pods) foreach (var r in p.RocketRows) rcsv.Append(r).Append('\n');
                File.WriteAllText(Path.Combine(Dir, g.level + "_mc2_rockets.csv"), rcsv.ToString());
            }
            if (done) yield break;

            // boarding: a red grunt boards each empty (neutral) mech; the hatch opens while he climbs in (BoardingView), the mech turns red
            var hinges = new Transform[2]; var lid0 = new float[2]; var maxLid = new float[2]; var lift = new float[2]; var rigRest = new Quaternion[2]; var rigs = new object[2];
            var crew = new ZObject[2];
            for (int i = 0; i < 2; i++)
            {
                var nv = i == 0 ? n1 : n2; var v = View(nv);
                hinges[i] = Node(v, "mech_HatchHinge"); lid0[i] = LidY(hinges[i]);
                rigRest[i] = hinges[i] ? hinges[i].localRotation : Quaternion.identity;
                int gx = (i == 0 ? spots["neutral_v1"].x : spots["neutral_v2"].x), gy = spots["neutral_v2"].y + 4;
                if (!OzCheatSpawner.FreeTile(W, gx, gy, 1, true, out int fx, out int fy)) { fx = gx; fy = gy; }
                crew[i] = W.CreateRobotGroup("grunt", fx * 16, fy * 16, Team.Red, 1);
                g.SpawnUnitView(crew[i]); foreach (var m in crew[i].Minions) g.SpawnUnitView(m);
                crew[i].Damage = 0; crew[i].AttackRadius = 0;
                W.SendWaypoints(Team.Red, crew[i], new List<ZWaypoint> { new ZWaypoint { mode = WpMode.Enter, refId = nv.Id, x = nv.CenterX, y = nv.CenterY } });
            }
            Mark("boarding: a red grunt ordered to enter each empty mech (v1, v2)");
            Focus((spots["neutral_v1"].x + spots["neutral_v2"].x) / 2 + 1, spots["neutral_v2"].y + 1, 26f);
            fw = T; int shotN = 0; float nextS = T + 1f;
            while (T - fw < 25f && !done && (n1.Owner == Team.Neutral || n2.Owner == Team.Neutral || T - fw < 6f))
            {
                for (int i = 0; i < 2; i++)
                {
                    if (!hinges[i]) continue;
                    if (rigs[i] == null) { rigs[i] = RigOf(View(i == 0 ? n1 : n2)); if (rigs[i] != null) rigRest[i] = RigGet(rigs[i], "hingeRest", rigRest[i]); }
                    maxLid[i] = Mathf.Max(maxLid[i], Quaternion.Angle(rigRest[i], hinges[i].localRotation)); lift[i] = Mathf.Max(lift[i], LidY(hinges[i]) - lid0[i]);
                }
                if (T >= nextS && shotN < 8) { yield return Shot($"mc2_D_board_{shotN:00}", 0f); shotN++; nextS = T + 1.5f; }
                yield return null;
            }
            yield return Wait(3f);
            for (int i = 0; i < 2; i++)
                Verdict("board_" + (i == 0 ? "v1" : "v2"), maxLid[i] > 45f && lift[i] > 0.03f && (i == 0 ? n1 : n2).Owner == Team.Red,
                        $"the hatch opened {maxLid[i]:0} deg (its lid up {lift[i]:0.00} m, hinge sign {RigGet(rigs[i], "hingeSign", 0f):+0;-0}) while the grunt climbed in; the mech now {(i == 0 ? n1 : n2).Owner}");
            bool k92L1 = r1.LidOpen, k92L2 = r2.LidOpen; r1.LidOpen = true; r2.LidOpen = true;      // k92: the sim's lid is a momentary roll (the idle rest too): both lids open for the check, then as they were
            int k86cD1 = r1.Drivers.Count, k86cD2 = r2.Drivers.Count;                                     // k86c: the rule needs a driver aboard: a mech without one (shot out or left earlier in the take) gets one for the read only
            if (k86cD1 == 0) r1.Drivers.Add(1); if (k86cD2 == 0) r2.Drivers.Add(1);
            bool k92S1 = r1.CanBeSniped(), k92S2 = r2.CanBeSniped(); r1.LidOpen = k92L1; r2.LidOpen = k92L2;
            if (k86cD1 == 0) r1.Drivers.RemoveAt(r1.Drivers.Count - 1); if (k86cD2 == 0) r2.Drivers.RemoveAt(r2.Drivers.Count - 1);
            Verdict("snipe_rule", k92S1 == k92S2 && r1.HasLid == r2.HasLid && k92S2,
                    $"the driver rules by type (owner item 4: jeeps snipe mech drivers), both lids open for the check: v1 can be sniped {k92S1} (lid {r1.HasLid}), v2 {k92S2} (lid {r2.HasLid}); on a bridge / door spot: v1 {W.SnipeSafeSpot(r1)}, v2 {W.SnipeSafeSpot(r2)}; drivers aboard before the read v1 {k86cD1}, v2 {k86cD2} (k86c: a missing one seated for the read only)");
            if (done) yield break;

            // damage: smoke at 45 %, fire at 20 % (MaxHealth / 100 first: the posed 100 000 000 x 45 would overflow an int)
            int ign0 = OpenZDamageStates.Ignited;
            foreach (var o in new[] { r1, r2 }) { o.SetHealth(o.MaxHealth / 100 * 45); W.UpdateObjectHealth(o); }
            Focus((spots["red_v1"].x + spots["red_v2"].x) / 2 + 1, spots["red_v2"].y, 32f); yield return Shot("mc2_E_damage_45", 2.5f);
            foreach (var o in new[] { r1, r2 }) { o.SetHealth(o.MaxHealth / 100 * 20); W.UpdateObjectHealth(o); }
            yield return Shot("mc2_E_damage_20", 3.0f);
            Verdict("damage_fire", OpenZDamageStates.Ignited - ign0 >= 2, $"{OpenZDamageStates.Ignited - ign0} fires lit (both at 20 %)");
            if (done) yield break;

            // death: both destroyed at once; a 10 fps frame burst for the MP4; the upper body tossed (Zod's turret toss), the legs break into parts
            // (DV18: at the death or when the wreck cooks off after its 5-7 s burn: the proof waits up to 16 s)
            float td = Time.unscaledTime;
            string mp4 = Path.Combine(Dir, "mp4_" + g.level); Directory.CreateDirectory(mp4);
            var dv = new[] { View(r1), View(r2) };
            foreach (var o in new[] { r1, r2 }) { o.SetHealth(0); W.UpdateObjectHealth(o); }
            int burstN = 0; float nf = T; fw = T; var tossed = new bool[2]; var broke = new bool[2];
            while (T - fw < 16f && !done)
            {
                if (T >= nf && T - fw < 9f) { ScreenCapture.CaptureScreenshot(Path.Combine(mp4, $"f_{burstN:000}.png")); burstN++; nf += 0.1f; }
                for (int i = 0; i < 2; i++)
                {
                    if (!dv[i]) continue;
                    if (TossedF != null && TossedF.GetValue(dv[i]) is Transform tt && tt) tossed[i] = true;
                    if (BreakDoneF != null && BreakDoneF.GetValue(dv[i]) is bool bd && bd) broke[i] = true;
                }
                if (T - fw > 9f && broke[0] && broke[1]) break;
                yield return null;
            }
            Mark($"death burst: {burstN} frames in {mp4} (ffmpeg -framerate 10 -i f_%03d.png)");
            for (int i = 0; i < 2; i++)
                Verdict("death_" + (i == 0 ? "v1" : "v2"), tossed[i] && broke[i], $"the upper body tossed {tossed[i]} (Zod's turret toss of MechWaistYaw), the legs broke into parts {broke[i]} (WreckPieces, within {T - fw:0.0} s)");
            Verdict("death_sounds", Count(td, "vdeath", "exp_large", "explosion") >= 2 && Count(td, "dst_turret_toss") >= 2,
                    $"death blasts heard: vdeath {Count(td, "vdeath")}, exp_large {Count(td, "exp_large")}, explosion {Count(td, "explosion")}; dst_turret_toss {Count(td, "dst_turret_toss")}");
            yield return Shot("mc2_F_wrecks", 0.2f);
            dn.running = run0;
            Finish(null);
        }

        void Finish(string why)
        {
            if (done) return; done = true;
            try
            {
                MechRocketWatch.Stop();
                Mark(why == null ? "DONE" : "STOPPED: " + why);
                log.AppendLine($"errors/exceptions in the Console during the run: {errors}; GPU peak (GpuGuard) {peak} C");
                int pass = 0; foreach (var v in verdicts) if (v.Contains(": PASS")) pass++;
                log.AppendLine($"## VERDICTS {pass}/{verdicts.Count} PASS"); foreach (var v in verdicts) log.AppendLine("  " + v);
                if (emptyLook.Count > 0) { log.AppendLine("## EMPTY LOOK (items 136 / 141; not one of the verdicts above)"); foreach (var e in emptyLook) log.AppendLine("  " + e); }
                Directory.CreateDirectory(Dir);
                File.WriteAllText(Path.Combine(Dir, (job ?? "run") + "_mc2_proof_log.txt"), log.ToString());
            }
            catch (Exception e) { Debug.LogError("[mc2proof] finish: " + e); }
            UnityEditor.EditorApplication.isPlaying = false;
        }
    }
}
#endif
