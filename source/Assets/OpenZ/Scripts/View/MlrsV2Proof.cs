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
    /// The MLRS launcher measured on the DRAWN model every frame (editor only; the Play proof and the MLRS films use it): its eight tubes
    /// (MLRSLoadedRocket_0k with their rocket meshes and MLRSMuzzle_0k), the kicked node MLRS_rack, the pitch joint MLRSRackPitch and the
    /// turn joint MLRSRackYaw.
    ///  * a launch = a tube's rocket hidden this frame (UnitAim.OnFire): its tube number, in v1's round-robin order (01 .. 08, 01 ...);
    ///  * every drawn MLRS rocket (OpenZFxPlayer.ProjTrace, the first frame it is drawn) is matched to the nearest tube mouth of the watched
    ///    launchers: its tube, and where it is against that tube's axis (along the axis out of the mouth, and off the axis): a rocket that
    ///    leaves its own tube is the tube just emptied, a little in front of its mouth and on its axis;
    ///  * a reload = the tube's rocket shown again: while it slides back in (UnitAim, 0.42 m in 0.5 s) its offset from its rest along the
    ///    tube axis (0 .. 0.42 m back, never out of the mouth) and off the axis (never: the slide follows the tube);
    ///  * the launcher kick: MLRS_rack's turn from its rest (deg) and its slide (m); the pitch joint's angle about its rest (deg);
    ///    MlrsSideKick's sizes (v1 0.22 m / 6 deg, v2 0 m / 2.5 deg) and whether it puts the launcher at rest before the aim (v2).
    /// </summary>
    public sealed class MlrsV2Watch
    {
        public readonly string Tag; public readonly UnitView View; public readonly int Model;
        public Transform Rack, Pitch, Yaw;
        readonly Transform[] tube = new Transform[8], muzzle = new Transform[8];
        readonly Renderer[][] rocketR = new Renderer[8][];
        readonly Vector3[] rest = new Vector3[8];
        readonly bool[] shown = new bool[8]; readonly float[] slideFrom = new float[8];
        Quaternion rackRest, pitchRest; Vector3 rackRestPos; bool ok;
        public readonly List<int> Launches = new List<int>();                 // the tube numbers (1 .. 8) in the order they fired
        public readonly List<float> LaunchAt = new List<float>();
        public int OrderBreaks, Reloads;
        public float SlideBackMax, SlideOutMax, SlideOffAxisMax;              // during the reload slides (m)
        public float KickDegMax, KickSlideMax, RollDegMax, PitchMin = 999f, PitchMax = -999f, YawMax;
        Quaternion yawRest;
        // the drawn rockets matched to this launcher's tubes
        public int Rockets, RocketsOwnTube, RocketsWrongTube; public float AlongMin = 99f, AlongMax = -99f, OffAxisMax;
        public readonly List<string> RocketRows = new List<string>();

        public MlrsV2Watch(string tag, UnitView v, int model) { Tag = tag; View = v; Model = model; Bind(); }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
        void Bind()
        {
            if (!View) return;
            var r = View.transform;
            Rack = Find(r, "MLRS_rack"); Pitch = Find(r, "MLRSRackPitch"); Yaw = Find(r, "MLRSRackYaw");
            for (int k = 0; k < 8; k++)
            {
                tube[k] = Find(r, "MLRSLoadedRocket_0" + (k + 1)); muzzle[k] = Find(r, "MLRSMuzzle_0" + (k + 1));
                rocketR[k] = tube[k] ? tube[k].GetComponentsInChildren<Renderer>(true) : new Renderer[0];
                rest[k] = tube[k] ? tube[k].localPosition : Vector3.zero; shown[k] = Shown(k); slideFrom[k] = -1f;
            }
            if (Rack) { rackRest = Rack.localRotation; rackRestPos = Rack.localPosition; }
            if (Pitch) pitchRest = Pitch.localRotation;
            if (Yaw) yawRest = Yaw.localRotation;
            ok = Rack && Pitch && Yaw && Array.TrueForAll(tube, t => t) && Array.TrueForAll(muzzle, t => t);
        }
        public bool Ok => ok;
        bool Shown(int k) { if (rocketR[k] == null || rocketR[k].Length == 0) return false; foreach (var x in rocketR[k]) if (x && x.enabled) return true; return false; }
        /// <summary>The tube's mouth (its muzzle) and its axis out of the mouth (the muzzle's -z), world.</summary>
        public bool Mouth(int k, out Vector3 at, out Vector3 axis)
        {
            at = axis = Vector3.zero; if (!ok || !muzzle[k]) return false;
            at = muzzle[k].position; axis = -muzzle[k].forward; return true;
        }

        /// <summary>Once per frame, after every LateUpdate.</summary>
        public void Sample(float now)
        {
            if (!ok || !View || View.Dying) return;
            for (int k = 0; k < 8; k++)
            {
                bool s = Shown(k);
                if (shown[k] && !s)
                {   // a launch from tube k + 1
                    if (Launches.Count > 0 && Launches[Launches.Count - 1] % 8 + 1 != k + 1) OrderBreaks++;
                    Launches.Add(k + 1); LaunchAt.Add(now); slideFrom[k] = -1f;
                }
                if (!shown[k] && s) { Reloads++; slideFrom[k] = now; }
                shown[k] = s;
                if (s && slideFrom[k] >= 0f && now - slideFrom[k] < 0.7f && tube[k].parent)
                {   // the slide back into the tube, in the tube's parent's space: along the tube (back = into the box), off the axis
                    var off = tube[k].localPosition - rest[k];
                    var axisL = tube[k].parent.InverseTransformDirection(-muzzle[k].forward).normalized;   // out of the mouth
                    float along = Vector3.Dot(off, axisL);
                    SlideBackMax = Mathf.Max(SlideBackMax, -along); SlideOutMax = Mathf.Max(SlideOutMax, along);
                    SlideOffAxisMax = Mathf.Max(SlideOffAxisMax, (off - axisL * along).magnitude);
                }
            }
            if (Rack)
            {
                KickDegMax = Mathf.Max(KickDegMax, Quaternion.Angle(rackRest, Rack.localRotation));
                KickSlideMax = Mathf.Max(KickSlideMax, (Rack.localPosition - rackRestPos).magnitude);
                var d = Quaternion.Inverse(rackRest) * Rack.localRotation; var e = d.eulerAngles;
                RollDegMax = Mathf.Max(RollDegMax, Mathf.Abs(Mathf.DeltaAngle(0f, e.z)));
            }
            if (Pitch)
            {
                var d = Quaternion.Inverse(pitchRest) * Pitch.localRotation;
                d.ToAngleAxis(out float a, out var ax); if (a > 180f) a -= 360f;
                float signed = a * Mathf.Sign(Vector3.Dot(ax, Vector3.right) == 0f ? 1f : Vector3.Dot(ax, Vector3.right));
                PitchMin = Mathf.Min(PitchMin, signed); PitchMax = Mathf.Max(PitchMax, signed);
            }
            if (Yaw) YawMax = Mathf.Max(YawMax, Quaternion.Angle(yawRest, Yaw.localRotation));
        }

        // ---------------------------------------------------------------- the drawn rockets (OpenZFxPlayer.ProjTrace)
        static readonly List<MlrsV2Watch> live = new List<MlrsV2Watch>();
        static readonly HashSet<int> seen = new HashSet<int>();
        static Action<int, FxWeapon, float, float, Vector3, bool> trace;
        /// <summary>Starts matching every drawn MLRS rocket to these launchers' tubes (Stop ends it).</summary>
        public static void Watch(IEnumerable<MlrsV2Watch> ws)
        {
            live.Clear(); live.AddRange(ws); seen.Clear();
            if (trace == null) { trace = OnTrace; OpenZFxPlayer.ProjTrace += trace; }
        }
        public static void Stop() { if (trace != null) { OpenZFxPlayer.ProjTrace -= trace; trace = null; } live.Clear(); }
        static void OnTrace(int serial, FxWeapon w, float age, float life, Vector3 pos, bool ease)
        {
            if (w != FxWeapon.Mlrs || !seen.Add(serial)) return;
            MlrsV2Watch best = null; int bk = -1; float bd = 1.6f;
            foreach (var x in live)
                for (int k = 0; k < 8; k++)
                    if (x.Mouth(k, out var at, out _)) { float d = Vector3.Distance(at, pos); if (d < bd) { bd = d; best = x; bk = k; } }
            if (best == null) return;
            best.Mouth(bk, out var m, out var axis);
            var off = pos - m; float along = Vector3.Dot(off, axis), perp = (off - axis * along).magnitude;
            best.Rockets++;
            // its own tube: the tube nearest the drawn rocket is the one emptied now (its rocket hidden by UnitAim.OnFire this frame, before
            // the end-of-frame sample) or the last one the sample saw fire (within 0.25 s)
            bool own = !best.Shown(bk) || (best.LaunchAt.Count > 0 && best.Launches[best.Launches.Count - 1] == bk + 1 && Time.unscaledTime - best.LaunchAt[best.LaunchAt.Count - 1] < 0.25f);
            if (own) best.RocketsOwnTube++; else best.RocketsWrongTube++;
            best.AlongMin = Mathf.Min(best.AlongMin, along); best.AlongMax = Mathf.Max(best.AlongMax, along); best.OffAxisMax = Mathf.Max(best.OffAxisMax, perp);
            if (best.RocketRows.Count < 64) best.RocketRows.Add($"{best.Tag},{serial},{bk + 1},{age:0.0000},{along:0.0000},{perp:0.0000},{(own ? 1 : 0)}");
        }

        public string Summary() =>
            $"{Tag}: {Launches.Count} launches, tubes in order {string.Join("", Launches.ConvertAll(t => t.ToString()).GetRange(0, Mathf.Min(24, Launches.Count)).ToArray())} ({OrderBreaks} breaks of v1's 1..8 round robin); " +
            $"{Rockets} drawn rockets matched to a tube mouth: {RocketsOwnTube} from the tube just emptied, {RocketsWrongTube} not; first drawn frame {AlongMin:0.000}..{AlongMax:0.000} m along the tube axis out of the mouth, " +
            $"at most {OffAxisMax:0.000} m off it; {Reloads} reloads: the rocket slid back up to {SlideBackMax:0.000} m into its tube, {SlideOutMax:0.000} m out of its rest, {SlideOffAxisMax:0.0000} m off the tube axis; " +
            $"launcher kick up to {KickDegMax:0.00} deg (roll {RollDegMax:0.00} deg), slide {KickSlideMax:0.000} m; pitch joint {PitchMin:0.0}..{PitchMax:0.0} deg about its rest; turn up to {YawMax:0} deg";
    }

    /// <summary>
    /// MLRS v2 Play proof (editor only; the light tank v2's proof, View/LightV2Proof.cs, for the MLRS): one auto-started run per level
    /// (PlayerPrefs oz_ml2proof_auto = "L02" / "L17", set by the menu OpenZ/MLRS v2/3). v1 and v2 side by side (pinned as the testing panel
    /// pins them), red, blue and neutral, on the proof's FIXED open lineup (View/HeavyV2Spots.cs "proof": chosen on a scan of the whole map
    /// with the sim's own tests; an MLRS fits where a heavy does; every slot asked again here, a protected or blocked slot stops the proof
    /// loudly); nobody fights while posing. Day and night frames at the game's play zoom and close (32 m), the empty v1 and v2 measured side
    /// by side (the empty look: dark grey like v1, never near white); then the red pair is ordered to attack a blue jeep (the game's own
    /// Attack order; the jeep cannot be destroyed and has no weapon): the launcher turns and elevates, every salvo's eight rockets leave
    /// their own tubes in v1's order, the launcher kicks, the hull rocks once per salvo, the tubes reload, the tracks stay planted on the
    /// ground every frame while the hull rocks, the launch / salvo / reload sounds; the lamp and blinker data each model uses; an empty v1
    /// and v2 boarded by a red grunt each (the hatch opens while he climbs in); damage smoke at 45 % and fire at 20 %, then both red ones
    /// destroyed (blast, the parts torn off, the tracks broken into lengths, wreck) with a frame burst at 10 fps for the MP4. Each step a
    /// counted verdict for v1 AND v2.
    /// </summary>
    public class MlrsV2Proof : MonoBehaviour
    {
        static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.."));
        static readonly string Dir = Path.Combine(Root, "work/staging/v1_2_20261022/v2_units/mlrs/proof");
        public const string AutoPref = "oz_ml2proof_auto";
        static OzUnitModels M => OzMlrs.Models;
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
            var go = new GameObject("MlrsV2Proof"); DontDestroyOnLoad(go);
            go.AddComponent<MlrsV2Proof>().job = job;
        }

        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        float T => (float)clock.Elapsed.TotalSeconds;
        void Mark(string s) { lastMark = T; log.AppendLine($"[{T,7:0.00}] {s}"); Debug.Log("[ml2proof] " + s); }
        void Verdict(string step, bool ok, string what) { string v = $"{step}: {(ok ? "PASS" : "FAIL")} - {what}"; verdicts.Add(v); Mark("VERDICT " + v); }
        IEnumerator Wait(float s) { float end = T + s; while (T < end && !done) yield return null; }
        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; MlrsV2Watch.Stop(); if (!done) Finish("disabled"); }
        void OnLog(string c, string st, LogType type)
        {
            if (c.StartsWith("[ml2proof]")) return;
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
        /// <summary>Every posed MLRS asked again with the sim's own tests (ZPathFinder.StepOk, ZWorld.SnipeSafeSpot): all open = true.</summary>
        bool SlotsOpen(out string why)
        {
            var bad = new List<string>();
            foreach (var (o, tag) in units)
            {
                if (o.Type != OzMlrs.Type) continue;
                int tx = o.LocX >> 4, ty = o.LocY >> 4;
                bool foot = W.Path.StepOk(tx, ty, tx, ty, false, false), prot = W.SnipeSafeSpot(o);
                if (!foot || prot) bad.Add($"{tag} at tile ({tx},{ty}): footprint {(foot ? "passes" : "BLOCKED")}, bridge / door spot {prot}");
            }
            why = string.Join("; ", bad);
            return bad.Count == 0;
        }
        // BoardingView's rig of a vehicle (read only, by reflection): the hinge it turns
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
        ZObject Mlrs(int model, int tx, int ty, Team t, string tag)
        {
            var o = W.CreateObject("vehicle", OzMlrs.Type, tx * 16, ty * 16, t);
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
                if (!(n == "MLRS_hull" || n == "MLRS_rack" || n == "MLRS_v2_box" || n == "MLRS_pedestal")) continue;
                var vs = m.vertices; var ns = m.normals; if (ns == null || ns.Length != vs.Length) continue;
                int step = Mathf.Max(1, vs.Length / 300);
                for (int i = 0; i < vs.Length; i += step)
                    if (mf.transform.TransformDirection(ns[i]).y > 0.35f) pts.Add(mf.transform.TransformPoint(vs[i]));
            }
            if (pts.Count < 30)
                foreach (var r in v.GetComponentsInChildren<Renderer>())
                {
                    if (r.name != "MLRS_hull" || !r.enabled) continue;
                    var b = r.bounds;
                    for (int i = 0; i < 12; i++)
                        for (int j = 0; j < 12; j++)
                            pts.Add(new Vector3(Mathf.Lerp(b.min.x, b.max.x, 0.12f + 0.76f * i / 11f), b.max.y, Mathf.Lerp(b.min.z, b.max.z, 0.12f + 0.76f * j / 11f)));
                    break;
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
            Mark($"MLRS v2 Play proof {job}, scene {SceneManager.GetActiveScene().name}, screen {Screen.width}x{Screen.height}, switch OzMlrs.Version {OzMlrs.Version}, v2 prefab {(M.PrefabV2 ? "found" : "MISSING")}");
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
            Verdict("v2_prefab", M.PrefabV2, "Resources/OpenZ/MlrsV2/Z_missile_launcher_v2 (menu OpenZ/MLRS v2/1)");
            if (!M.PrefabV2) { Finish("no v2 prefab"); yield break; }
            {   // MLRS placed in the level scene at the start are drawn as the switch says (ZGame.BindViews -> OzV2Units.SceneView)
                int placed = 0, asV2 = 0;
                foreach (var o in W.Objects)
                {
                    if (o.Type != OzMlrs.Type || o.Source == null) continue;
                    var v = View(o); if (!v) continue;
                    placed++; if (M.IsV2(v.transform)) asV2++;
                }
                Verdict("scene_mlrs", asV2 == (OzMlrs.Version == 2 ? placed : 0),
                        $"{placed} MLRS placed in the level scene, {asV2} drawn as v2 (switch {OzMlrs.Version}, {M.SceneSwaps} scene view(s) taken over){(placed == 0 ? "; none on this map" : "")}");
            }

            var spots = new Dictionary<string, (int x, int y)>();
            foreach (var n in new[] { "red_v1", "red_v2", "blue_v1", "blue_v2", "neutral_v1", "neutral_v2", "jeep", "centre" })
            {
                if (!Spot(n, out int sx, out int sy)) { Mark($"SPOT FAIL: no fixed tile {g.level}/proof/{n} in HeavyV2Spots"); Finish("spot check failed: " + n); yield break; }
                spots[n] = (sx, sy);
            }
            Mark("fixed lineup (View/HeavyV2Spots.cs, the proof's open lineup): " + string.Join(", ", new List<string>(spots.Keys).ConvertAll(k => $"{k} {spots[k]}")));
            var r1 = Mlrs(1, spots["red_v1"].x, spots["red_v1"].y, Team.Red, "red_v1"); var r2 = Mlrs(2, spots["red_v2"].x, spots["red_v2"].y, Team.Red, "red_v2");
            var b1 = Mlrs(1, spots["blue_v1"].x, spots["blue_v1"].y, Team.Blue, "blue_v1"); var b2 = Mlrs(2, spots["blue_v2"].x, spots["blue_v2"].y, Team.Blue, "blue_v2");
            var n1 = Mlrs(1, spots["neutral_v1"].x, spots["neutral_v1"].y, Team.Neutral, "neutral_v1"); var n2 = Mlrs(2, spots["neutral_v2"].x, spots["neutral_v2"].y, Team.Neutral, "neutral_v2");
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
                var aa = va ? va.GetComponent<UnitAim>() : null; var ab = vb ? vb.GetComponent<UnitAim>() : null;
                Verdict("launcher_limits", aa && ab && Mathf.Approximately(aa.PitchMinNow, -20f) && Mathf.Approximately(aa.PitchMaxNow, 57f) && Mathf.Approximately(ab.PitchMinNow, OzMlrs.BoxModel(vb.transform) ? OzMlrs.PitchMinV2 : -20f) && Mathf.Approximately(ab.PitchMaxNow, OzMlrs.BoxModel(vb.transform) ? OzMlrs.PitchMaxV2 : 57f),
                        $"elevation about the rest: v1 {(aa ? aa.PitchMinNow + ".." + aa.PitchMaxNow : "-")} deg (as before), v2 {(ab ? ab.PitchMinNow + ".." + ab.PitchMaxNow : "-")} deg (the box kept inside its measured clear sweep)");
            }

            // day + night frames, the empty look
            yield return TimeOfDay(0.08f);
            Focus(spots["centre"].x, spots["centre"].y, h0); yield return Shot("ml2_A_day_rts");
            yield return EmptyLook(n1, n2, "rts");
            Focus((spots["red_v1"].x + spots["red_v2"].x) / 2 + 1, spots["red_v2"].y, 32f); yield return Shot("ml2_A_day_close_red");
            Focus((spots["blue_v1"].x + spots["blue_v2"].x) / 2 + 1, spots["blue_v2"].y, 32f); yield return Shot("ml2_A_day_close_blue");
            Focus((spots["neutral_v1"].x + spots["neutral_v2"].x) / 2 + 1, spots["neutral_v2"].y, 32f); yield return Shot("ml2_A_day_close_neutral");
            yield return EmptyLook(n1, n2, "close");
            yield return TimeOfDay(0.62f);
            Focus(spots["centre"].x, spots["centre"].y, h0); yield return Shot("ml2_A_night_rts", 0.8f);
            Focus((spots["red_v1"].x + spots["red_v2"].x) / 2 + 1, spots["red_v2"].y, 32f); yield return Shot("ml2_A_night_close_red");
            Focus((spots["neutral_v1"].x + spots["neutral_v2"].x) / 2 + 1, spots["neutral_v2"].y, 32f); yield return Shot("ml2_A_night_close_neutral");
            {   // the lamps and blinkers: each model on its own data entry (v2: "missile_launcher@v2", measured on v2's meshes)
                string l1 = SpecType(VehicleLights.I, View(r1)), l2 = SpecType(VehicleLights.I, View(r2)), u1 = SpecType(UnitBlinkers.I, View(r1)), u2 = SpecType(UnitBlinkers.I, View(r2));
                Verdict("lights_data", l1 == OzMlrs.Type && l2 == M.LightsKey && (u1 == "-" || u1 == OzMlrs.Type) && (u2 == "-" || u2 == M.LightsKey),
                        $"VehicleLights v1 '{l1}', v2 '{l2}'; UnitBlinkers v1 '{u1}', v2 '{u2}' (v2 must use '{M.LightsKey}'; '-' = the blinkers are off in this run)");
            }
            yield return TimeOfDay(0.08f);
            if (done) yield break;

            // fire: the red pair is ordered to attack an unkillable blue jeep (no weapon). Every frame: the launcher (MlrsV2Watch), the hull's
            // rock (HeavyRecoil), the planted tracks against the ground (UnitView.ProofGear)
            if (LightV2Spots.Get(g.level, "proof", "jeep near", out int jnx, out int jny)) { Mark($"k87b: the jeep at the open tile ({jnx},{jny}) in reach of both MLRS (k85's tile ({spots["jeep"].x},{spots["jeep"].y}): red_v1 drove to it; View/LightV2Stage.cs)"); spots["jeep"] = (jnx, jny); }
            var jeep = W.CreateObject("vehicle", "jeep", spots["jeep"].x * 16, spots["jeep"].y * 16, Team.Blue); g.SpawnUnitView(jeep);
            jeep.MaxHealth = 100000000; jeep.SetHealth(jeep.MaxHealth); jeep.Damage = 0; jeep.AttackRadius = 0; units.Add((jeep, "target"));
            foreach (var (o, _) in units) if (o != jeep && o.Owner == Team.Blue) o.AttackRadius = 0;
            var wv = new[] { new MlrsV2Watch("v1", View(r1), 1), new MlrsV2Watch("v2", View(r2), View(r2) && OzMlrs.BoxModel(View(r2).transform) ? 2 : 1) };   // the v2 on v1's model is judged as v1's geometry
            Verdict("launcher_nodes", wv[0].Ok && wv[1].Ok, $"MLRS_rack, MLRSRackPitch, MLRSRackYaw, 8 tubes with their rockets and muzzles found on v1 {wv[0].Ok} and v2 {wv[1].Ok}");
            MlrsV2Watch.Watch(wv);
            foreach (var o in new[] { r1, r2 })
            {
                o.Damage = o.U.Damage; o.AttackRadius = o.U.attack_radius;
                W.SendWaypoints(o.Owner, o, new List<ZWaypoint> { new ZWaypoint { mode = WpMode.Attack, refId = jeep.Id, x = jeep.CenterX, y = jeep.CenterY } });
            }
            Mark($"the red pair ordered to attack the jeep at tile ({spots["jeep"].x},{spots["jeep"].y}) (the game's Attack order; MLRS range {r1.U.attack_radius} px)");
            LightV2Stage.FireStage(W, r1, r2, jeep, units.ConvertAll(u => u.o), Mark);                     // k87b, item 148: standing and firing: the level's own enemies near the pair unarmed, both MLRS on the jeep
            MTankStage.HoldTarget(jeep, Mark);                                                          // k87b, item 148: the jeep never steps aside for a rocket (it stays in reach: no MLRS drives after it)
            Focus((spots["red_v2"].x + spots["jeep"].x) / 2, (spots["red_v2"].y + spots["jeep"].y) / 2, 30f);
            float t0 = Time.unscaledTime; float fw = T; int kick0 = HeavyRecoil.Kicks;
            var bodyQ = new float[2]; var bodyS = new float[2]; var rocks = new int[2]; var wasRock = new bool[2];
            var tpMin = new[] { 99f, 99f }; var tpMax = new[] { -99f, -99f }; var trMin = new[] { 99f, 99f }; var trMax = new[] { -99f, -99f };
            var midMin = new[] { 99f, 99f }; var midMax = new[] { -99f, -99f }; var sinkMax = new[] { -9f, -9f }; var hullMax = new float[2]; var gearFrames = new int[2];
            var sk = new MlrsSideKick[2];
            int burst = 0; float nextB = T + 1.0f;
            while (T - fw < 20f && !done)
            {
                yield return new WaitForEndOfFrame();
                for (int i = 0; i < 2; i++)
                {
                    var w = wv[i]; w.Sample(Time.unscaledTime);
                    var v = w.View; if (!v) continue;
                    if (!sk[i]) sk[i] = v.GetComponent<MlrsSideKick>();
                    var hr = v.HeavyRecoilView;
                    if (hr)
                    {
                        bodyQ[i] = Mathf.Max(bodyQ[i], hr.BodyRockNow); bodyS[i] = Mathf.Max(bodyS[i], hr.BodyPushNow);
                        bool rk = hr.BodyRockNow > 0.15f; if (rk && !wasRock[i]) rocks[i]++; wasRock[i] = rk;
                    }
                    if (ProofStageK92.GearStill(v, 0.3f) && v.ProofGear(out float tp, out float trl, out float mid, out float sink, out float hp, out float hrl, out _))   // k92: the frames the drawn MLRS stands still (0.3 s after it last moved or turned)
                    {
                        gearFrames[i]++;
                        tpMin[i] = Mathf.Min(tpMin[i], tp); tpMax[i] = Mathf.Max(tpMax[i], tp); trMin[i] = Mathf.Min(trMin[i], trl); trMax[i] = Mathf.Max(trMax[i], trl);
                        midMin[i] = Mathf.Min(midMin[i], mid); midMax[i] = Mathf.Max(midMax[i], mid); sinkMax[i] = Mathf.Max(sinkMax[i], sink);
                        hullMax[i] = Mathf.Max(hullMax[i], Mathf.Max(Mathf.Abs(hp), Mathf.Abs(hrl)));
                    }
                }
                if (T >= nextB && burst < 16) { ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"{g.level}_ml2_B_fire_{burst:00}.png")); burst++; nextB = T + 0.9f; }
            }
            MlrsV2Watch.Stop();
            int launches = Count(t0, "launch"), ripple = Count(t0, "mlrs_salvo"), reloads = Count(t0, "mlrs_reload");
            for (int i = 0; i < 2; i++) Mark("LAUNCHER " + wv[i].Summary());
            {
                var csv = new StringBuilder("unit,serial,tube,age_s,along_m,off_axis_m,own_tube\n");
                foreach (var w in wv) foreach (var r in w.RocketRows) csv.Append(r).Append('\n');
                File.WriteAllText(Path.Combine(Dir, g.level + "_ml2_rockets.csv"), csv.ToString());
            }
            for (int i = 0; i < 2; i++)
                Verdict("launcher_turn_" + wv[i].Tag, wv[i].YawMax > 15f, $"the launcher turned up to {wv[i].YawMax:0} deg toward the jeep, pitch joint {wv[i].PitchMin:0.0}..{wv[i].PitchMax:0.0} deg about its rest");
            for (int i = 0; i < 2; i++)
            {
                var w = wv[i];
                bool order = w.Launches.Count >= 8 && w.OrderBreaks == 0;
                bool own = w.Model == 1 || (w.Rockets >= 8 && w.RocketsWrongTube == 0 && w.AlongMin > -0.05f && w.AlongMax < 1.2f && w.OffAxisMax < 0.15f);
                Verdict("rockets_" + w.Tag, order && own, $"{w.Launches.Count} launches in v1's tube order ({w.OrderBreaks} breaks); {w.Rockets} drawn rockets: {w.RocketsOwnTube} from the tube just emptied, {w.RocketsWrongTube} not; " +
                        $"first drawn frame {w.AlongMin:0.000}..{w.AlongMax:0.000} m out of the mouth along the tube, {w.OffAxisMax:0.000} m off its axis{(w.Model == 1 ? " (v1: its rockets point 33 deg above its muzzle axis: shown, not judged)" : "")}");
                Verdict("reload_" + w.Tag, w.Reloads >= 8 && w.SlideBackMax > 0.3f && w.SlideBackMax < 0.44f && w.SlideOutMax < 0.005f && w.SlideOffAxisMax < 0.01f,
                        $"{w.Reloads} rockets back in their tubes, each slid {w.SlideBackMax:0.000} m in along the tube (MlrsSalvo 0.42 m), {w.SlideOutMax:0.000} m past its rest, {w.SlideOffAxisMax:0.0000} m off the axis");
                var s = sk[i];
                bool sizes = s && (w.Model == 2 ? Mathf.Approximately(s.KickBackNow, OzMlrs.KickBackV2) && Mathf.Approximately(s.KickRollNow, OzMlrs.KickRollV2) && s.RestBeforeAim
                                                : Mathf.Approximately(s.KickBackNow, MlrsSideKick.BackM) && Mathf.Approximately(s.KickRollNow, MlrsSideKick.RollDeg) && !s.RestBeforeAim);
                Verdict("launcher_kick_" + w.Tag, sizes && w.KickDegMax > 2f && (w.Model == 1 || w.KickSlideMax < 0.005f),
                        $"MlrsSideKick back {(s ? s.KickBackNow : -1f):0.00} m, roll {(s ? s.KickRollNow : -1f):0.0} deg, at rest before the aim {(s && s.RestBeforeAim)}; the launcher kicked up to {w.KickDegMax:0.0} deg (roll {w.RollDegMax:0.0}), slid {w.KickSlideMax:0.000} m");
            }
            int salvos = 0; foreach (var w in wv) salvos += (w.Launches.Count + 7) / 8;
            int kicks = HeavyRecoil.Kicks - kick0;
            for (int i = 0; i < 2; i++)
                Verdict("hull_rock_" + wv[i].Tag, bodyQ[i] >= 0.5f * HeavyRecoil.DesignBodyRock(OzMlrs.Type) && bodyQ[i] <= Mathf.Max(2.5f, 1.25f * HeavyRecoil.DesignBodyRock(OzMlrs.Type)) && rocks[i] >= 1,   // the salvo's rock (k93, item 151): visible, 0.5-1.25 x its design
                        $"the hull rocked back {bodyQ[i]:0.00} deg and slid {bodyS[i]:0.000} m, eased out (HeavyRecoil, once per salvo: {rocks[i]} rocks; pass {0.5f * HeavyRecoil.DesignBodyRock(OzMlrs.Type):0.00}-{Mathf.Max(2.5f, 1.25f * HeavyRecoil.DesignBodyRock(OzMlrs.Type)):0.00} deg = 0.5-1.25 x the salvo's design rock)");
            Verdict("hull_rock_per_salvo", kicks <= salvos + 2, $"HeavyRecoil kicks {kicks} for about {salvos} salvos of both ({wv[0].Launches.Count + wv[1].Launches.Count} rockets): once per salvo, never once per rocket");
            for (int i = 0; i < 2; i++)
                Verdict("tracks_planted_" + wv[i].Tag, gearFrames[i] > 30 && tpMax[i] - tpMin[i] <= 0.3f && trMax[i] - trMin[i] <= 0.3f && midMax[i] - midMin[i] <= 0.03f && sinkMax[i] <= 0.03f && hullMax[i] >= 0.2f,
                        $"{gearFrames[i]} frames firing on flat ground: the tracks' pitch {tpMin[i]:0.00}..{tpMax[i]:0.00} deg, roll {trMin[i]:0.00}..{trMax[i]:0.00} deg, their bottom over the ground {midMin[i]:0.000}..{midMax[i]:0.000} m, deepest point {sinkMax[i]:0.000} m in it; the hull over them up to {hullMax[i]:0.00} deg (it rocks, the tracks do not)");
            Verdict("fire_sound", launches >= 2 && ripple >= 7 && reloads >= 2, $"sound log: launch {launches} (the sim's rocket), mlrs_salvo {ripple} (the ripple), mlrs_reload {reloads} in 20 s");
            foreach (var (o, _) in units) { o.Damage = 0; o.AttackRadius = 0; o.AttackObject = null; }
            if (done) yield break;

            // boarding: a red grunt boards each empty (neutral) MLRS; the hatch opens while he climbs in (BoardingView), the MLRS turns red
            var hinges = new Transform[2]; var lid0 = new float[2]; var maxLid = new float[2]; var lift = new float[2]; var rigRest = new Quaternion[2]; var rigs = new object[2];
            var crew = new ZObject[2];
            for (int i = 0; i < 2; i++)
            {
                var nv = i == 0 ? n1 : n2; var v = View(nv);
                hinges[i] = Node(v, "mlrs_HatchHinge"); lid0[i] = LidY(hinges[i]);
                rigRest[i] = hinges[i] ? hinges[i].localRotation : Quaternion.identity;
                int gx = (i == 0 ? spots["neutral_v1"].x : spots["neutral_v2"].x), gy = spots["neutral_v2"].y + 4;
                if (!OzCheatSpawner.FreeTile(W, gx, gy, 1, true, out int fx, out int fy)) { fx = gx; fy = gy; }
                crew[i] = W.CreateRobotGroup("grunt", fx * 16, fy * 16, Team.Red, 1);
                g.SpawnUnitView(crew[i]); foreach (var m in crew[i].Minions) g.SpawnUnitView(m);
                crew[i].Damage = 0; crew[i].AttackRadius = 0;
                W.SendWaypoints(Team.Red, crew[i], new List<ZWaypoint> { new ZWaypoint { mode = WpMode.Enter, refId = nv.Id, x = nv.CenterX, y = nv.CenterY } });
            }
            Mark("boarding: a red grunt ordered to enter each empty MLRS (v1, v2)");
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
                if (T >= nextS && shotN < 8) { yield return Shot($"ml2_C_board_{shotN:00}", 0f); shotN++; nextS = T + 1.5f; }
                yield return null;
            }
            yield return Wait(3f);
            for (int i = 0; i < 2; i++)
                Verdict("board_" + (i == 0 ? "v1" : "v2"), maxLid[i] > 45f && lift[i] > 0.03f && (i == 0 ? n1 : n2).Owner == Team.Red,
                        $"the hatch opened {maxLid[i]:0} deg (its lid up {lift[i]:0.00} m, hinge sign {RigGet(rigs[i], "hingeSign", 0f):+0;-0}) while the grunt climbed in; the MLRS now {(i == 0 ? n1 : n2).Owner}");
            Verdict("snipe_rule", r1.CanBeSniped() == r2.CanBeSniped() && r1.HasLid == r2.HasLid, $"the driver rules by type: v1 can be sniped {r1.CanBeSniped()} (lid {r1.HasLid}), v2 {r2.CanBeSniped()} (lid {r2.HasLid}); on a bridge / door spot: v1 {W.SnipeSafeSpot(r1)}, v2 {W.SnipeSafeSpot(r2)}");
            if (done) yield break;

            // damage: smoke at 45 %, fire at 20 % (MaxHealth / 100 first: the posed 100 000 000 x 45 would overflow an int)
            int ign0 = OpenZDamageStates.Ignited;
            foreach (var o in new[] { r1, r2 }) { o.SetHealth(o.MaxHealth / 100 * 45); W.UpdateObjectHealth(o); }
            Focus((spots["red_v1"].x + spots["red_v2"].x) / 2 + 1, spots["red_v2"].y, 32f); yield return Shot("ml2_D_damage_45", 2.5f);
            foreach (var o in new[] { r1, r2 }) { o.SetHealth(o.MaxHealth / 100 * 20); W.UpdateObjectHealth(o); }
            yield return Shot("ml2_D_damage_20", 3.0f);
            Verdict("damage_fire", OpenZDamageStates.Ignited - ign0 >= 2, $"{OpenZDamageStates.Ignited - ign0} fires lit (both at 20 %)");
            if (done) yield break;

            // death: both destroyed at once; a 10 fps frame burst for the MP4
            int d0 = WheeledBreakup.Deaths, tb0 = OzTreadBreak.Breaks; float td = Time.unscaledTime;
            Mark(LightV2Stage.DeathWays(r1, "red_v1", r2, "red_v2"));                                   // k87b, item 148: each MLRS's seeded way to break up (DV18; its track break comes with it), told first
            string mp4 = Path.Combine(Dir, "mp4_" + g.level); Directory.CreateDirectory(mp4);
            var lastBreaks = new List<string>(); System.Action<string> k92Broke = s => lastBreaks.Add(s); OzTreadBreak.Broke += k92Broke;   // k92: every break (two in one frame were one line)
            foreach (var o in new[] { r1, r2 })
            {
                o.SetHealth(0); W.UpdateObjectHealth(o);
            }
            int f = 0; float nf = T; fw = T; string seenLast = OzTreadBreak.Last;
            while (T - fw < LightV2Stage.DeathWaitS && !done && !(T - fw >= 9f && OzTreadBreak.Breaks - tb0 >= 2))   // k87b, item 148: the second MLRS cooks off after 5-7 s of burning: wait for its track break
            {
                if (T >= nf) { ScreenCapture.CaptureScreenshot(Path.Combine(mp4, $"f_{f:000}.png")); f++; nf += 0.1f; }
                if (OzTreadBreak.Last != seenLast) seenLast = OzTreadBreak.Last;                        // k92: the breaks are recorded by OzTreadBreak.Broke
                yield return null;
            }
            OzTreadBreak.Broke -= k92Broke;
            Mark($"death burst: {f} frames in {mp4} (ffmpeg -framerate 10 -i f_%03d.png); wheeled break-up: {WheeledBreakup.Last}");
            int deaths = WheeledBreakup.Deaths - d0, breaks = OzTreadBreak.Breaks - tb0;
            bool bothSides = lastBreaks.Count >= 2 && lastBreaks.TrueForAll(s => !s.Contains("(L 0") && !s.Contains(", R 0)"));
            Verdict("death_breakup", deaths >= 2 && breaks >= 2 && bothSides, $"{deaths} MLRS break-ups (parts torn off), {breaks} track breaks (k72), each with lengths of both tracks: {string.Join(" | ", lastBreaks)}");
            Verdict("death_sounds", Count(td, "vdeath", "exp_large", "explosion") >= 2, $"death blasts heard: vdeath {Count(td, "vdeath")}, exp_large {Count(td, "exp_large")}, explosion {Count(td, "explosion")}");
            yield return Shot("ml2_E_wrecks", 0.2f);
            dn.running = run0;
            Finish(null);
        }

        void Finish(string why)
        {
            if (done) return; done = true;
            try
            {
                MlrsV2Watch.Stop();
                Mark(why == null ? "DONE" : "STOPPED: " + why);
                log.AppendLine($"errors/exceptions in the Console during the run: {errors}; GPU peak (GpuGuard) {peak} C");
                int pass = 0; foreach (var v in verdicts) if (v.Contains(": PASS")) pass++;
                log.AppendLine($"## VERDICTS {pass}/{verdicts.Count} PASS"); foreach (var v in verdicts) log.AppendLine("  " + v);
                if (emptyLook.Count > 0) { log.AppendLine("## EMPTY LOOK (items 136 / 141; not one of the verdicts above)"); foreach (var e in emptyLook) log.AppendLine("  " + e); }
                Directory.CreateDirectory(Dir);
                File.WriteAllText(Path.Combine(Dir, (job ?? "run") + "_ml2_proof_log.txt"), log.ToString());
            }
            catch (Exception e) { Debug.LogError("[ml2proof] finish: " + e); }
            UnityEditor.EditorApplication.isPlaying = false;
        }
    }
}
#endif
