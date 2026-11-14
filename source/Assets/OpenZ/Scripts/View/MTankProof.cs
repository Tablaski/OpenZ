#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// MEDIUM TANK Play proof (editor only; the light tank v2's proof, View/LightV2Proof.cs, for the medium tank): one auto-started run per
    /// level (PlayerPrefs oz_mt2proof_auto = "L02" / "L17", set by the menu OpenZ/Medium Tank/4). On the heavy proof's FIXED open lineup
    /// (View/HeavyV2Spots.cs: chosen on a scan of the whole map with the sim's own tests; every slot asked again here, a protected or blocked
    /// slot stops the proof loudly; never a bridge / door spot): a red light tank v2, a red medium tank and a red heavy tank v2 side by side
    /// (the sizes measured on the drawn models), a blue medium tank, an empty medium tank beside an empty light tank v1 (the empty look: the
    /// dark grey every empty vehicle keeps, never near white); nobody fights while posing. Day and night frames at the level's RTS zoom and
    /// close (32 m); the lamp and blinker data it uses; then the red medium tank: its turret turns to a blue jeep and fires (the barrel's
    /// slide, never past its 0.65 m in the dome; the head's kick and rock; the hull's rock; the tracks planted on the ground every frame while
    /// the hull rocks; the light tank's shot sound), the hatch open with the commander out and the driver open to a snipe, damage smoke at
    /// 45 % and fire at 20 %, then destroyed (blast, turret toss, break-apart, wreck) with a frame burst at 10 fps for the MP4; then a red
    /// factory builds one right after the mech (its list, its build time) and it drives off; then one dropped from the testing panel (P).
    /// </summary>
    public class MTankProof : MonoBehaviour
    {
        static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.."));
        static readonly string Dir = Path.Combine(Root, "work/staging/v1_2_20261022/v2_units/medium/proof");
        public const string AutoPref = "oz_mt2proof_auto";
        static OzUnitModels M => OzMediumTank.Models;
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
            var go = new GameObject("MTankProof"); DontDestroyOnLoad(go);
            go.AddComponent<MTankProof>().job = job;
        }

        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        float T => (float)clock.Elapsed.TotalSeconds;
        void Mark(string s) { lastMark = T; log.AppendLine($"[{T,7:0.00}] {s}"); Debug.Log("[mt2proof] " + s); }
        void Verdict(string step, bool ok, string what) { string v = $"{step}: {(ok ? "PASS" : "FAIL")} - {what}"; verdicts.Add(v); Mark("VERDICT " + v); }
        IEnumerator Wait(float s) { float end = T + s; while (T < end && !done) yield return null; }
        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; if (!done) Finish("disabled"); }
        void OnLog(string c, string st, LogType type)
        {
            if (c.StartsWith("[mt2proof]")) return;
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
        bool FixedCentre(out int cx, out int cy) => HeavyV2Spots.Get(g.level, "proof", "centre", out cx, out cy);
        /// <summary>Every posed light tank asked again with the sim's own tests (ZPathFinder.StepOk, ZWorld.SnipeSafeSpot): all open = true.</summary>
        bool SlotsOpen(out string why)
        {
            var bad = new List<string>();
            foreach (var (o, tag) in units)
            {
                if (o.Type != OzMediumTank.Type) continue;
                int tx = o.LocX >> 4, ty = o.LocY >> 4;
                bool foot = W.Path.StepOk(tx, ty, tx, ty, false, false), prot = W.SnipeSafeSpot(o);
                if (!foot || prot) bad.Add($"{tag} at tile ({tx},{ty}): footprint {(foot ? "passes" : "BLOCKED")}, bridge / door spot {prot}");
            }
            why = string.Join("; ", bad);
            return bad.Count == 0;
        }
        // BoardingView's rig of a vehicle (read only, by reflection): the hinge it turns, the hatch commander
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
        static float CommanderTop(object rig, out bool shown)
        {
            shown = false; if (rig == null) return -9f;
            var crew = RigGet<GameObject>(rig, "crew", null);
            if (!crew || !crew.activeInHierarchy || RigGet(rig, "crewPop", 0f) < 0.99f) return -9f;
            shown = true; float top = -1e9f;
            foreach (var r in crew.GetComponentsInChildren<Renderer>()) if (r.enabled) top = Mathf.Max(top, r.bounds.max.y);
            var round = RigGet<RoundHatch>(rig, "round", null);
            return top - (round ? round.RimWorld.y : crew.transform.position.y);
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
        /// <summary>A posed unit: type, team; the light and heavy tanks drawn as the model asked (pinned as the testing panel pins them); unkillable and quiet
        /// while posing (the fire part gives the medium tank its gun back).</summary>
        ZObject Tank(string type, int model, int tx, int ty, Team t, string tag)
        {
            var o = W.CreateObject("vehicle", type, tx * 16, ty * 16, t);
            if (type == OzLightTank.Type) OzLightTank.Models.Pin(o, model);
            if (type == OzHeavyTank.Type) OzHeavyTank.Pin(o, model);
            g.SpawnUnitView(o);
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
                if (!(n.StartsWith("Tank_hull") || n.StartsWith("Tank_turret") || n.StartsWith("Tank_barrel"))) continue;
                var vs = m.vertices; var ns = m.normals; if (ns == null || ns.Length != vs.Length) continue;
                int step = Mathf.Max(1, vs.Length / 300);
                for (int i = 0; i < vs.Length; i += step)
                    if (mf.transform.TransformDirection(ns[i]).y > 0.35f) pts.Add(mf.transform.TransformPoint(vs[i]));
            }
            if (pts.Count < 30)
                foreach (var r in v.GetComponentsInChildren<Renderer>())
                {
                    if (!r.name.StartsWith("Tank_hull") || !r.enabled) continue;
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
                foreach (var (o, tag) in new[] { (n1, "reference"), (n2, "medium") })
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
                string line = $"{g.level} {frame}: the empty light tank v1 (the empty grey every vehicle keeps) mean sRGB {res[0].mean:0} ({res[0].white:0.0} % near white, {res[0].n} points), the empty medium tank mean {res[1].mean:0} ({res[1].white:0.0} % near white, {res[1].n} points): {(ok ? "PASS" : "FAIL")} ({OzV2EmptyLook.LastRule})";
                emptyLook.Add(line); Mark("EMPTY LOOK " + line);
            }
            finally { Destroy(tex); }
        }

        static Vector3 DrawnSize(UnitView v)
        {
            Bounds box = default; bool any = false;
            if (!v) return Vector3.zero;
            foreach (var r in v.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || r.name.Contains("Antenna")) continue;
                bool frag = false; for (var p = r.transform; p; p = p.parent) if (p.name == "OZ_WreckFragments") { frag = true; break; }
                if (frag) continue;
                if (!any) { box = r.bounds; any = true; } else box.Encapsulate(r.bounds);
            }
            return any ? box.size : Vector3.zero;
        }

        // ---------------------------------------------------------------- the run
        IEnumerator Start()
        {
            Directory.CreateDirectory(Dir); lastMark = T;
            Mark($"medium tank Play proof {job}, scene {SceneManager.GetActiveScene().name}, screen {Screen.width}x{Screen.height}, prefab {(M.PrefabV2 ? "found" : "MISSING")}");
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
            Mark($"level {g.level}, default RTS height {h0:0.0} m; CPU bots cleared ({bots}); day/night clock stopped; DV145 switch {ZWorld.MTankDeviation}");
            Verdict("prefab", M.PrefabV2, "Resources/OpenZ/MTank/Z_mtank (menu OpenZ/Medium Tank/1)");
            if (!M.PrefabV2) { Finish("no medium tank prefab"); yield break; }

            if (!FixedCentre(out int ax, out int ay)) { Mark($"SPOT FAIL: no fixed lineup centre for {g.level} in HeavyV2Spots"); Finish("spot check failed: no fixed lineup centre"); yield break; }
            Mark($"lineup window centre tile ({ax},{ay}) (fixed: View/HeavyV2Spots.cs, the heavy proof's open lineup)");
            var rl = Tank(OzLightTank.Type, 2, ax - 6, ay - 4, Team.Red, "red_light_v2");
            var rm = Tank(OzMediumTank.Type, 2, ax - 1, ay - 4, Team.Red, "red_medium");
            var rh = Tank(OzHeavyTank.Type, 2, ax + 5, ay - 4, Team.Red, "red_heavy_v2");
            var bm = Tank(OzMediumTank.Type, 2, ax - 1, ay + 1, Team.Blue, "blue_medium");
            var n1 = Tank(OzLightTank.Type, 1, ax - 6, ay + 1, Team.Neutral, "neutral_light_v1");
            var nm = Tank(OzMediumTank.Type, 2, ax + 10, ay - 4, Team.Neutral, "neutral_medium");
            yield return Wait(2.0f);
            {
                int views = 0; foreach (var o in new[] { rm, bm, nm }) { var v = View(o); if (v && M.IsV2(v.transform)) views++; }
                Verdict("model", views == 3, $"{views}/3 medium tanks drawn with the medium tank model (red, blue, empty)");
            }
            Mark("lineup spots (a bridge / door spot shuts every lid at once, DV82 / DV87 B): " + string.Join(", ", units.ConvertAll(u => $"{u.tag} {(W.SnipeSafeSpot(u.o) ? "ON ONE" : "open")}")));
            if (!SlotsOpen(out string spotWhy)) { Mark("SPOT FAIL: " + spotWhy); Finish("spot check failed: " + spotWhy); yield break; }
            {
                var s = new[] { rl, rm, rh }.Select(o => DrawnSize(View(o))).ToArray();
                Verdict("size", ProofStageK92.MediumSilhouette(out string k92Size),                              // k92: the silhouette rule (k87b's, the parity's), not the drawn length
                        k92Size + $" | told: drawn length x width x height (m, antennas out): light v2 {s[0].z:0.00} x {s[0].x:0.00} x {s[0].y:0.00}, MEDIUM {s[1].z:0.00} x {s[1].x:0.00} x {s[1].y:0.00}, heavy v2 {s[2].z:0.00} x {s[2].x:0.00} x {s[2].y:0.00} (the light v2's long gun makes it the longest)");
                var vm = View(rm); var rf = typeof(UnitView).GetField("radius", BindingFlags.NonPublic | BindingFlags.Instance);
                float ra = vm && rf != null ? (float)rf.GetValue(vm) : -1f;
                Verdict("footprint", ra >= 1.9f && ra <= 2.2f, $"UnitView radius {ra:0.00} m (spacing / no touching; the light tank 1.95)");
                var rh2 = vm ? vm.GetComponent<RoundHatch>() : null;
                Mark($"hatch: {(rh2 ? (rh2.Frame && rh2.Frame.name == OzMediumTank.FrameNode ? "the modelled one on the dome" : "BUILT (wrong)") : "not yet")}");
            }

            // day + night frames, the empty look
            yield return TimeOfDay(0.08f);
            Focus(ax + 2, ay - 1, h0); yield return Shot("mt2_A_day_rts");
            yield return EmptyLook(n1, nm, "rts");
            Focus(ax - 1, ay - 4, 32f); yield return Shot("mt2_A_day_close_red");
            Focus(ax - 1, ay + 1, 32f); yield return Shot("mt2_A_day_close_blue");
            Focus(ax + 10, ay - 4, 32f); yield return Shot("mt2_A_day_close_neutral");
            Focus(ax - 1, ay - 4, 46f); yield return Shot("mt2_A_day_light_medium_heavy");
            yield return EmptyLook(n1, nm, "close");
            yield return TimeOfDay(0.62f);
            Focus(ax + 2, ay - 1, h0); yield return Shot("mt2_A_night_rts", 0.8f);
            Focus(ax - 1, ay - 4, 32f); yield return Shot("mt2_A_night_close_red");
            Focus(ax + 10, ay - 4, 32f); yield return Shot("mt2_A_night_close_neutral");
            {
                string l = SpecType(VehicleLights.I, View(rm)), u = SpecType(UnitBlinkers.I, View(rm));
                Verdict("lights_data", l == OzMediumTank.Type && (u == "-" || u == OzMediumTank.Type),
                        $"VehicleLights '{l}', UnitBlinkers '{u}' (its own entries '{OzMediumTank.Type}', measured on its meshes; '-' = the blinkers are off in this run)");
            }
            yield return TimeOfDay(0.08f);
            if (done) yield break;

            // turret turn + fire: an unkillable blue jeep north of the lineup; the red medium tank gets its gun back. Every frame: the turret's turn,
            // the barrel's slide (never past 0.65 m: its root stays in the dome), the head's kick and rock and the hull's rock (HeavyRecoil, the
            // light tank's gun sizes), and the planted tracks against the ground (UnitView.ProofGear: they must not move while the hull rocks)
            int jtx = ax + 3, jty = ay - 9;                                                              // k87b: the jeep in the gun's reach of the red medium tank (k87's tile: the tank drove and fought)
            if (LightV2Spots.Get(g.level, "proof", "jeep near", out int jnx, out int jny)) { jtx = jnx; jty = jny; }
            var jeep = W.CreateObject("vehicle", "jeep", jtx * 16, jty * 16, Team.Blue); g.SpawnUnitView(jeep);
            Mark($"the jeep (the target of the fire phase) at tile ({jtx},{jty}) (View/LightV2Stage.cs: LightV2Spots proof/jeep near)");
            jeep.MaxHealth = 100000000; jeep.SetHealth(jeep.MaxHealth); jeep.Damage = 0; jeep.AttackRadius = 0; units.Add((jeep, "target"));
            var vrm = View(rm); var yaw = Node(vrm, "TankTurretYaw"); var rec = Node(vrm, "TankRecoil");
            var yaw0 = yaw ? yaw.localRotation : Quaternion.identity; var rec0 = rec ? rec.localPosition : Vector3.zero;
            rm.Damage = rm.U.Damage; rm.AttackRadius = rm.U.attack_radius;
            MTankStage.FireStage(W, rm, jeep, units.ConvertAll(u => u.o), Mark);                          // k87b: standing and firing: the jeep in reach, the level's own enemies near it unarmed, the tank on the jeep
            Focus(ax - 1, ay - 6, 34f);
            float t0 = Time.unscaledTime; float fw = T;
            float maxYaw = 0f, maxRec = 0f, headS = 0f, headQ = 0f, bodyQ = 0f, bodyS = 0f;
            float tpMin = 99f, tpMax = -99f, trMin = 99f, trMax = -99f, midMin = 99f, midMax = -99f, sinkMax = -9f, hullMax = 0f; int gearFrames = 0;
            int burst = 0; float nextB = T + 1.0f;
            while (T - fw < 16f && !done)
            {
                if (yaw) maxYaw = Mathf.Max(maxYaw, Quaternion.Angle(yaw0, yaw.localRotation));
                if (rec) maxRec = Mathf.Max(maxRec, (rec.localPosition - rec0).magnitude);
                var hr = vrm ? vrm.HeavyRecoilView : null;
                if (hr) { headS = Mathf.Max(headS, hr.HeadSlideNow); headQ = Mathf.Max(headQ, hr.HeadRockNow); bodyQ = Mathf.Max(bodyQ, hr.BodyRockNow); bodyS = Mathf.Max(bodyS, hr.BodyPushNow); }
                if (vrm && vrm.ProofGear(out float tp, out float trl, out float mid, out float sink, out float hp, out float hrl, out _))
                {
                    gearFrames++;
                    tpMin = Mathf.Min(tpMin, tp); tpMax = Mathf.Max(tpMax, tp); trMin = Mathf.Min(trMin, trl); trMax = Mathf.Max(trMax, trl);
                    midMin = Mathf.Min(midMin, mid); midMax = Mathf.Max(midMax, mid); sinkMax = Mathf.Max(sinkMax, sink);
                    hullMax = Mathf.Max(hullMax, Mathf.Max(Mathf.Abs(hp), Mathf.Abs(hrl)));
                }
                if (T >= nextB && burst < 12) { ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"{g.level}_mt2_B_fire_{burst:00}.png")); burst++; nextB = T + 0.9f; }
                yield return null;
            }
            int shots = Count(t0, "tankshot", "wpn_light_tank");
            Verdict("turret_turn", maxYaw > 20f, $"turret turned up to {maxYaw:0} deg toward the jeep (eased, UnitAim.Steady)");
            Verdict("recoil", maxRec > 0.15f && maxRec <= OzMediumTank.SlideMaxM + 0.01f, $"barrel slide {maxRec:0.000} m (UnitAim recoilK 0.55: peak 0.62; never past {OzMediumTank.SlideMaxM:0.00} m, its root stays in the dome)");
            Verdict("head_kick", headS > 0.04f && headQ > 0.5f, $"the head kicked back {headS:0.000} m and rocked {headQ:0.0} deg (HeavyRecoil, the medium tank's own kick: {HeavyRecoil.DesignHeadSlide(OzMediumTank.Type, 2):0.00} x its head's scale, 2 deg)");
            Verdict("hull_rock", bodyQ > 0.3f && bodyS > 0.02f, $"the hull rocked back {bodyQ:0.00} deg and slid {bodyS:0.000} m, eased out (HeavyRecoil)");
            Verdict("tracks_planted", gearFrames > 30 && tpMax - tpMin <= 0.3f && trMax - trMin <= 0.3f && midMax - midMin <= 0.03f && sinkMax <= 0.03f && hullMax >= 0.2f,
                    $"{gearFrames} frames standing and firing on flat ground: the tracks' pitch {tpMin:0.00}..{tpMax:0.00} deg, roll {trMin:0.00}..{trMax:0.00} deg, their bottom over the ground {midMin:0.000}..{midMax:0.000} m, deepest point {sinkMax:0.000} m in it; the hull over them up to {hullMax:0.00} deg (it rocks, the tracks do not)");
            Verdict("fire_sound", shots >= 2, $"{shots} light tank gun plays (tankshot / wpn_light_tank) in 16 s (the medium tank firing; sound log)");
            foreach (var (o, _) in units) { o.Damage = 0; o.AttackRadius = 0; o.AttackObject = null; }
            if (done) yield break;

            // hatch: the lid opens, the commander comes out, the driver can be sniped
            MTankStage.HatchStage(rm, Mark);                                                            // k87b: kills and rank logged; a veteran crew (DV12: stays buttoned up) reset for the check (k81d's staging)
            rm.LidOpen = false;
            { double s0 = W.Time; float w1 = T; while ((W.Time < s0 + 1.6 || T - w1 < 1.0f) && T - w1 < 10f && !done) yield return null; }
            var hinge = Node(vrm, "tank_HatchHinge"); var rig = RigOf(vrm);
            var rest = RigGet(rig, "hingeRest", hinge ? hinge.localRotation : Quaternion.identity); float lidY0 = LidY(hinge);
            rm.LidOpen = true;
            Focus(ax - 1, ay - 4, 24f);
            float maxLid = 0f, lift = 0f, cmdTop = -9f; int held = 0, frames = 0; bool cmdUp = false;
            fw = T;
            while (T - fw < 4f && !done)
            {
                frames++; if (rm.LidOpen) held++;
                if (hinge) { maxLid = Mathf.Max(maxLid, Quaternion.Angle(rest, hinge.localRotation)); lift = Mathf.Max(lift, LidY(hinge) - lidY0); }
                float ct = CommanderTop(rig, out bool up); if (up) { cmdUp = true; cmdTop = Mathf.Max(cmdTop, ct); }
                yield return null;
            }
            yield return Shot("mt2_C_hatch_open_close", 0.1f);
            Verdict("hatch", maxLid > 60f && lift > 0.05f && cmdUp && cmdTop > 0.2f,
                    $"the dome's hatch: lid opened {maxLid:0} deg (its centre up {lift:0.00} m, sign {RigGet(rig, "hingeSign", 0f):+0;-0}), the sim kept it open {held}/{frames} frames, the commander {(cmdUp ? $"up, his top {cmdTop:0.00} m over the rim" : "NOT shown")}");
            bool sn = rm.CanBeSniped(), spot = W.SnipeSafeSpot(rm);
            Verdict("snipe_rule", sn && !spot, $"driver open to a snipe with the lid open: {sn} (the light tank's rule: DriverHitFloor 29, the four open-seat pairs); on a bridge / door spot (DV82, DV87 B): {spot}");
            rm.LidOpen = false;
            if (done) yield break;

            // damage: smoke at 45 %, fire at 20 % (MaxHealth / 100 first: the posed tank's 100 000 000 x 45 would overflow an int)
            int ign0 = OpenZDamageStates.Ignited;
            rm.SetHealth(rm.MaxHealth / 100 * 45); W.UpdateObjectHealth(rm);
            Focus(ax - 1, ay - 4, 32f); yield return Shot("mt2_D_damage_45", 2.5f);
            rm.SetHealth(rm.MaxHealth / 100 * 20); W.UpdateObjectHealth(rm);
            yield return Shot("mt2_D_damage_20", 3.0f);
            Verdict("damage_fire", OpenZDamageStates.Ignited - ign0 >= 1, $"{OpenZDamageStates.Ignited - ign0} fire(s) lit (the medium tank at 20 %)");
            if (done) yield break;

            // death: destroyed; a 10 fps frame burst for the MP4
            int br0 = OzTankWreck.Breaks, pc0 = OzTankWreck.PiecesMade; float td = Time.unscaledTime;
            string mp4 = Path.Combine(Dir, "mp4_" + g.level); Directory.CreateDirectory(mp4);
            Mark(MTankStage.DeathWay(rm));                                                              // k87b: its seeded way to break up (DV18), told first
            rm.SetHealth(0); W.UpdateObjectHealth(rm);
            int f = 0; float nf = T; fw = T; string seenBreak = OzTankWreck.LastBreak;
            while (T - fw < LightV2Stage.DeathWaitS && !done && !(T - fw >= 9f && OzTankWreck.Breaks - br0 >= 1))   // k87b: a tank that cooks off (5-7 s of burning) can break after 9 s: wait until it broke
            {
                if (OzTankWreck.LastBreak != seenBreak) { seenBreak = OzTankWreck.LastBreak; Mark($"break-up at {T - fw:0.0} s after the kill: {seenBreak}"); }
                if (T >= nf) { ScreenCapture.CaptureScreenshot(Path.Combine(mp4, $"f_{f:000}.png")); f++; nf += 0.1f; }
                yield return null;
            }
            Mark($"death burst: {f} frames in {mp4} (ffmpeg -framerate 10 -i f_%03d.png)");
            int breaks = OzTankWreck.Breaks - br0, pieces = OzTankWreck.PiecesMade - pc0;
            Verdict("death_breakup", breaks >= 1, $"{breaks} tank break-up(s) ({pieces} pieces), last: {OzTankWreck.LastBreak}");
            Verdict("death_sounds", Count(td, "dst_turret_toss") >= 1, $"turret toss sounds {Count(td, "dst_turret_toss")}, exp_large {Count(td, "exp_large")}");
            yield return Shot("mt2_E_wreck", 0.2f);
            if (done) yield break;

            // the factory: a red vehicle factory (or the fort) builds one right after the mech; it drives off its door
            yield return Factory();
            if (done) yield break;
            // the testing panel (P): a MEDIUM TANK tile drops one (its private Spawn, by reflection: the panel's own path)
            yield return PDrop(ax + 4, ay + 4);
            dn.running = run0;
            Finish(null);
        }

        IEnumerator Factory()
        {
            var team = g.player;
            var f = W.Objects.Where(o => o.Owner == team && o.Kind == ZKind.Building && o.Type == "vehicle_factory" && !o.Destroyed).OrderBy(o => o.Id).FirstOrDefault()
                    ?? W.Objects.Where(o => o.Owner == team && o.IsFort && !o.Destroyed).OrderBy(o => o.Id).FirstOrDefault();
            if (f == null) { Verdict("factory", false, "no " + team + " vehicle factory or fort"); yield break; }
            if (f.Level < 2) { Mark($"staged: the {f.Type} at tile ({f.LocX / 16},{f.LocY / 16}) raised from level {f.Level} to 2 (the mech's tech level)"); f.Level = 2; }
            var list = ZBuildLists.For(f.Type, f.BuildLevel).ToList();
            int im = list.IndexOf(ZMTank.Mech), it = list.IndexOf(ZMTank.Type);
            Verdict("build_list", im >= 0 && it == im + 1, $"the {f.Type}'s list at level {f.BuildLevel}: {string.Join(", ", list)} (the medium tank right after the mech)");
            var before = new HashSet<int>(W.Objects.Select(o => o.Id));
            bool ok = W.StartBuilding(team, f, ZMTank.Type);
            double full = f.BFinal - f.BInit;
            Mark($"StartBuilding({ZMTank.Type}): {ok}; its build time under the rules {full:0.0} s (base {W.S.Unit(ZMTank.Type).build_time} s = the mech's {W.S.Unit(ZMTank.Mech).build_time} s, x this factory's flags and health)");
            if (!ok) { Verdict("factory", false, "StartBuilding refused"); yield break; }
            var c = ZGame.PxToWorld(f.CenterX, f.CenterY); cam.focus = new Vector3(c.x, 0f, c.z); cam.SetHeightImmediate(46f);
            f.BInit = W.Time - (full - 3.0); f.RecalcBuildTime();
            Mark("staged: the build started 3 s before its end (the proof shows the end, the door and the drive off)");
            ZObject made = null; float fw = T;
            while (!done && made == null && T - fw < 20f)
            {
                foreach (var o in W.Objects) if (!before.Contains(o.Id) && o.Type == ZMTank.Type && o.Owner == team) { made = o; break; }
                yield return null;
            }
            if (made == null) { Verdict("factory", false, "no medium tank came out within 20 s"); yield break; }
            int x0 = made.CenterX, y0 = made.CenterY;
            fw = T; int shot = 0; float nextS = T;
            while (T - fw < 8f && !done)
            {
                var p = ZGame.PxToWorld(made.CenterX, made.CenterY); cam.focus = Vector3.Lerp(cam.focus, new Vector3(p.x, 0f, p.z), 0.1f);
                if (T >= nextS && shot < 4) { ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"{g.level}_mt2_F_factory_{shot:00}.png")); shot++; nextS = T + 2f; }
                yield return null;
            }
            double moved = Math.Sqrt((made.CenterX - x0) * (made.CenterX - x0) + (made.CenterY - y0) * (made.CenterY - y0)) / 8.0;
            var mech = W.S.Unit(ZMTank.Mech);
            Verdict("factory", View(made) && M.IsV2(View(made).transform) && moved > 3.0 && made.MaxHealth == W.S.Unit(ZMTank.Type).MaxHealth,
                    $"built in the {f.Type}: {made.MaxHealth} HP (mech {mech.MaxHealth}), speed {made.MoveSpeed} (mech {mech.move_speed}), drawn as the medium tank {(View(made) ? M.IsV2(View(made).transform).ToString() : "NO VIEW")}, drove {moved:0.0} m off the door in 8 s");
        }

        IEnumerator PDrop(int tx, int ty)
        {
            var sp = OzCheatSpawner.I;
            var m = typeof(OzCheatSpawner).GetMethod("Spawn", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string), typeof(Team), typeof(Vector2Int) }, null);
            if (!sp || m == null) { Mark($"P drop: not tested in this run (testing panel {(sp ? "present" : "not in this scene")}, its Spawn {(m != null ? "found" : "not found")}): the worker drops one by hand (picture g)"); yield break; }
            var before = new HashSet<int>(W.Objects.Select(o => o.Id));
            m.Invoke(sp, new object[] { OzMediumTank.Type, Team.Red, new Vector2Int(tx * 16 + 16, ty * 16 + 16) });
            yield return Wait(1.0f);
            var made = W.Objects.FirstOrDefault(o => !before.Contains(o.Id) && o.Type == OzMediumTank.Type);
            Verdict("p_drop", made != null && View(made) && M.IsV2(View(made).transform), $"the testing panel's MEDIUM TANK tile drops a red medium tank: {(made != null ? "id " + made.Id + ", drawn as the medium tank " + (View(made) ? M.IsV2(View(made).transform).ToString() : "NO VIEW") : "NONE")}");
            if (made != null) { Focus(made.LocX >> 4, made.LocY >> 4, 32f); yield return Shot("mt2_G_p_drop", 0.6f); }
        }

        void Finish(string why)
        {
            if (done) return; done = true;
            try
            {
                Mark(why == null ? "DONE" : "STOPPED: " + why);
                log.AppendLine($"errors/exceptions in the Console during the run: {errors}; GPU peak (GpuGuard) {peak} C");
                int pass = 0; foreach (var v in verdicts) if (v.Contains(": PASS")) pass++;
                log.AppendLine($"## VERDICTS {pass}/{verdicts.Count} PASS"); foreach (var v in verdicts) log.AppendLine("  " + v);
                if (emptyLook.Count > 0) { log.AppendLine("## EMPTY LOOK (items 136 / 141; not one of the verdicts above)"); foreach (var e in emptyLook) log.AppendLine("  " + e); }
                Directory.CreateDirectory(Dir);
                File.WriteAllText(Path.Combine(Dir, (job ?? "run") + "_mt2_proof_log.txt"), log.ToString());
            }
            catch (Exception e) { Debug.LogError("[mt2proof] finish: " + e); }
            UnityEditor.EditorApplication.isPlaying = false;
        }
    }
}
#endif
