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
    /// Light tank v2 Play proof (editor only; the heavy tank v2's proof, View/HeavyV2Proof.cs, for the light tank): one auto-started run per
    /// level (PlayerPrefs oz_lt2proof_auto = "L02" / "L17", set by the menu OpenZ/Light Tank v2/3). v1 and v2 side by side (pinned as the
    /// testing panel pins them), red, blue and neutral, on the heavy proof's FIXED open lineup (View/HeavyV2Spots.cs: chosen on a scan of the
    /// whole map with the sim's own tests; a light tank fits where a heavy does; every slot asked again here, a protected or blocked slot
    /// stops the proof loudly); nobody fights while posing. Day and night frames at the level's RTS zoom and close (32 m), the empty v1 and
    /// v2 measured side by side (the empty look: dark grey like v1, never near white); then for the red pair: the turret turns toward a blue
    /// jeep and fires (the barrel's slide, the head's kick and rock, the hull's rock, the tracks planted on the ground every frame while the
    /// hull rocks, the shot sounds), the lamp and blinker data each model uses, the hatch open with the commander out and the driver open to a
    /// snipe, damage smoke at 45 % and fire at 20 %, then both destroyed (blast, turret toss, break-apart, wreck) with a frame burst at
    /// 10 fps for the MP4. Each step a counted verdict for v1 AND v2.
    /// </summary>
    public class LightV2Proof : MonoBehaviour
    {
        static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.."));
        static readonly string Dir = Path.Combine(Root, "work/staging/v1_2_20261022/v2_units/light/proof");
        public const string AutoPref = "oz_lt2proof_auto";
        static OzUnitModels M => OzLightTank.Models;
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
            var go = new GameObject("LightV2Proof"); DontDestroyOnLoad(go);
            go.AddComponent<LightV2Proof>().job = job;
        }

        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        float T => (float)clock.Elapsed.TotalSeconds;
        void Mark(string s) { lastMark = T; log.AppendLine($"[{T,7:0.00}] {s}"); Debug.Log("[lt2proof] " + s); }
        void Verdict(string step, bool ok, string what) { string v = $"{step}: {(ok ? "PASS" : "FAIL")} - {what}"; verdicts.Add(v); Mark("VERDICT " + v); }
        IEnumerator Wait(float s) { float end = T + s; while (T < end && !done) yield return null; }
        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; if (!done) Finish("disabled"); }
        void OnLog(string c, string st, LogType type)
        {
            if (c.StartsWith("[lt2proof]")) return;
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
                if (o.Type != OzLightTank.Type) continue;
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
        ZObject Light(int model, int tx, int ty, Team t, string tag)
        {
            var o = W.CreateObject("vehicle", OzLightTank.Type, tx * 16, ty * 16, t);
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
            Mark($"light tank v2 Play proof {job}, scene {SceneManager.GetActiveScene().name}, screen {Screen.width}x{Screen.height}, switch OzLightTank.Version {OzLightTank.Version}, v2 prefab {(M.PrefabV2 ? "found" : "MISSING")}");
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
            Verdict("v2_prefab", M.PrefabV2, "Resources/OpenZ/LightV2/Z_light_v2 (menu OpenZ/Light Tank v2/1)");
            if (!M.PrefabV2) { Finish("no v2 prefab"); yield break; }
            {   // light tanks placed in the level scene at the start are drawn as the switch says (ZGame.BindViews -> OzV2Units.SceneView)
                int placed = 0, asV2 = 0;
                foreach (var o in W.Objects)
                {
                    if (o.Type != OzLightTank.Type || o.Source == null) continue;
                    var v = View(o); if (!v) continue;
                    placed++; if (M.IsV2(v.transform)) asV2++;
                }
                Verdict("scene_lights", asV2 == (OzLightTank.Version == 2 ? placed : 0),
                        $"{placed} light tank(s) placed in the level scene, {asV2} drawn as v2 (switch {OzLightTank.Version}, {M.SceneSwaps} scene view(s) taken over){(placed == 0 ? "; none on this map" : "")}");
            }

            if (!FixedCentre(out int ax, out int ay)) { Mark($"SPOT FAIL: no fixed lineup centre for {g.level} in HeavyV2Spots"); Finish("spot check failed: no fixed lineup centre"); yield break; }
            Mark($"lineup window centre tile ({ax},{ay}) (fixed: View/HeavyV2Spots.cs, the heavy proof's open lineup)");
            var r1 = Light(1, ax - 6, ay - 4, Team.Red, "red_v1"); var r2 = Light(2, ax - 1, ay - 4, Team.Red, "red_v2");
            var b1 = Light(1, ax - 6, ay + 1, Team.Blue, "blue_v1"); var b2 = Light(2, ax - 1, ay + 1, Team.Blue, "blue_v2");
            var n1 = Light(1, ax + 5, ay - 4, Team.Neutral, "neutral_v1"); var n2 = Light(2, ax + 10, ay - 4, Team.Neutral, "neutral_v2");
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
                var rhA = va ? va.GetComponent<RoundHatch>() : null; var rhB = vb ? vb.GetComponent<RoundHatch>() : null;
                Mark($"hatch: v1 {(rhA ? "built" : "not yet")}, v2 {(rhB ? (rhB.Frame && rhB.Frame.name == OzLightTank.FrameNode ? "the modelled one" : "BUILT (wrong)") : "not yet")}");
            }

            // day + night frames, the empty look
            yield return TimeOfDay(0.08f);
            Focus(ax + 11, ay, h0); yield return Shot("lt2_A_day_rts");
            yield return EmptyLook(n1, n2, "rts");
            Focus(ax + 1, ay - 4, 32f); yield return Shot("lt2_A_day_close_red");
            Focus(ax + 1, ay + 1, 32f); yield return Shot("lt2_A_day_close_blue");
            Focus(ax + 11, ay - 4, 32f); yield return Shot("lt2_A_day_close_neutral");
            yield return EmptyLook(n1, n2, "close");
            yield return TimeOfDay(0.62f);
            Focus(ax + 11, ay, h0); yield return Shot("lt2_A_night_rts", 0.8f);
            Focus(ax + 1, ay - 4, 32f); yield return Shot("lt2_A_night_close_red");
            Focus(ax + 11, ay - 4, 32f); yield return Shot("lt2_A_night_close_neutral");
            {   // the lamps and blinkers: each model on its own data entry (v2: "light@v2", measured on v2's meshes)
                string l1 = SpecType(VehicleLights.I, View(r1)), l2 = SpecType(VehicleLights.I, View(r2)), u1 = SpecType(UnitBlinkers.I, View(r1)), u2 = SpecType(UnitBlinkers.I, View(r2));
                Verdict("lights_data", l1 == "light" && l2 == M.LightsKey && (u1 == "-" || u1 == "light") && (u2 == "-" || u2 == M.LightsKey),
                        $"VehicleLights v1 '{l1}', v2 '{l2}'; UnitBlinkers v1 '{u1}', v2 '{u2}' (v2 must use '{M.LightsKey}'; '-' = the blinkers are off in this run)");
            }
            yield return TimeOfDay(0.08f);
            if (done) yield break;

            // turret turn + fire: an unkillable blue jeep south-east of the red pair; the red pair gets its attack back. Every frame: the
            // turret's turn, the barrel's slide, the head's kick and rock and the hull's rock (HeavyRecoil), and the planted tracks
            // against the ground (UnitView.ProofGear: they must not move while the hull rocks)
            int jtx = ax + 3, jty = ay - 9;                                                              // k84b: the jeep in the light gun's reach from both red tanks (k84's tile: 104 / 162 px; red_v1 drove to it)
            if (LightV2Spots.Get(g.level, "proof", "jeep near", out int jnx, out int jny)) { jtx = jnx; jty = jny; }
            var jeep = W.CreateObject("vehicle", "jeep", jtx * 16, jty * 16, Team.Blue); g.SpawnUnitView(jeep);
            Mark($"the jeep (the target of the fire phase) at tile ({jtx},{jty}) (View/LightV2Stage.cs: LightV2Spots proof/jeep near)");
            jeep.MaxHealth = 100000000; jeep.SetHealth(jeep.MaxHealth); jeep.Damage = 0; jeep.AttackRadius = 0; units.Add((jeep, "target"));
            foreach (var (o, _) in units) if (o != jeep && o.Owner == Team.Blue) o.AttackRadius = 0;
            var watch = new List<(string tag, UnitView v, Transform yaw, Transform rec, Quaternion yaw0, Vector3 rec0)>();
            foreach (var (o, tag) in new[] { (r1, "v1"), (r2, "v2") })
            {
                var v = View(o); var yaw = Node(v, "TankTurretYaw"); var rc = Node(v, "TankRecoil");
                watch.Add((tag, v, yaw, rc, yaw ? yaw.localRotation : Quaternion.identity, rc ? rc.localPosition : Vector3.zero));
                o.Damage = o.U.Damage; o.AttackRadius = o.U.attack_radius;
            }
            LightV2Stage.FireStage(W, r1, r2, jeep, units.ConvertAll(u => u.o), Mark);                     // k84b: standing and firing: the jeep in reach, the level's own enemies near the pair without a weapon, both red tanks on the jeep
            Focus(ax + 1, ay - 6, 34f);
            float t0 = Time.unscaledTime; float fw = T;
            var maxYaw = new float[2]; var maxRec = new float[2]; var headS = new float[2]; var headQ = new float[2]; var bodyQ = new float[2]; var bodyS = new float[2];
            var tpMin = new[] { 99f, 99f }; var tpMax = new[] { -99f, -99f }; var trMin = new[] { 99f, 99f }; var trMax = new[] { -99f, -99f };
            var midMin = new[] { 99f, 99f }; var midMax = new[] { -99f, -99f }; var sinkMax = new[] { -9f, -9f }; var hullMax = new float[2]; var gearFrames = new int[2];
            int burst = 0; float nextB = T + 1.0f;
            while (T - fw < 16f && !done)
            {
                for (int i = 0; i < watch.Count; i++)
                {
                    var w = watch[i];
                    if (w.yaw) maxYaw[i] = Mathf.Max(maxYaw[i], Quaternion.Angle(w.yaw0, w.yaw.localRotation));
                    if (w.rec) maxRec[i] = Mathf.Max(maxRec[i], (w.rec.localPosition - w.rec0).magnitude);
                    var hr = w.v ? w.v.HeavyRecoilView : null;
                    if (hr) { headS[i] = Mathf.Max(headS[i], hr.HeadSlideNow); headQ[i] = Mathf.Max(headQ[i], hr.HeadRockNow); bodyQ[i] = Mathf.Max(bodyQ[i], hr.BodyRockNow); bodyS[i] = Mathf.Max(bodyS[i], hr.BodyPushNow); }
                    if (w.v && w.v.ProofGear(out float tp, out float trl, out float mid, out float sink, out float hp, out float hrl, out _))
                    {
                        gearFrames[i]++;
                        tpMin[i] = Mathf.Min(tpMin[i], tp); tpMax[i] = Mathf.Max(tpMax[i], tp); trMin[i] = Mathf.Min(trMin[i], trl); trMax[i] = Mathf.Max(trMax[i], trl);
                        midMin[i] = Mathf.Min(midMin[i], mid); midMax[i] = Mathf.Max(midMax[i], mid); sinkMax[i] = Mathf.Max(sinkMax[i], sink);
                        hullMax[i] = Mathf.Max(hullMax[i], Mathf.Max(Mathf.Abs(hp), Mathf.Abs(hrl)));
                    }
                }
                if (T >= nextB && burst < 12) { ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"{g.level}_lt2_B_fire_{burst:00}.png")); burst++; nextB = T + 0.9f; }
                yield return null;
            }
            int shots = Count(t0, "tankshot", "wpn_light_tank");
            for (int i = 0; i < watch.Count; i++)
                Verdict("turret_turn_" + watch[i].tag, maxYaw[i] > 20f, $"turret turned up to {maxYaw[i]:0} deg toward the jeep");
            for (int i = 0; i < watch.Count; i++)
                Verdict("recoil_" + watch[i].tag, maxRec[i] > 0.15f, $"barrel slide {maxRec[i]:0.00} m (UnitAim, as v1)");
            for (int i = 0; i < watch.Count; i++)
                Verdict("head_kick_" + watch[i].tag, headS[i] > 0.04f && headQ[i] > 0.5f, $"the head kicked back {headS[i]:0.000} m and rocked {headQ[i]:0.0} deg (HeavyRecoil, light: v1 0.10 m, v2 0.20 m, 2 deg)");
            for (int i = 0; i < watch.Count; i++)
                Verdict("hull_rock_" + watch[i].tag, bodyQ[i] > 0.3f && bodyS[i] > 0.02f, $"the hull rocked back {bodyQ[i]:0.00} deg and slid {bodyS[i]:0.000} m, eased out (HeavyRecoil)");
            for (int i = 0; i < watch.Count; i++)
                Verdict("tracks_planted_" + watch[i].tag, gearFrames[i] > 30 && tpMax[i] - tpMin[i] <= 0.3f && trMax[i] - trMin[i] <= 0.3f && midMax[i] - midMin[i] <= 0.03f && sinkMax[i] <= 0.03f && hullMax[i] >= 0.2f,
                        $"{gearFrames[i]} frames standing and firing on flat ground: the tracks' pitch {tpMin[i]:0.00}..{tpMax[i]:0.00} deg, roll {trMin[i]:0.00}..{trMax[i]:0.00} deg, their bottom over the ground {midMin[i]:0.000}..{midMax[i]:0.000} m, deepest point {sinkMax[i]:0.000} m in it; the hull over them up to {hullMax[i]:0.00} deg (it rocks, the tracks do not)");
            Verdict("fire_sound", shots >= 2, $"{shots} light tank shot plays (tankshot / wpn_light_tank) in 16 s (both tanks firing; sound log)");
            foreach (var (o, _) in units) { o.Damage = 0; o.AttackRadius = 0; o.AttackObject = null; }
            if (done) yield break;

            // hatch: the lid opens, the commander comes out, the driver can be sniped (both lids shut first, then opened in the same frame)
            LightV2Stage.HatchStage(r1, r2, Mark);                                                      // k84b: kills and rank logged; a veteran crew (DV12: stays buttoned up) reset for the check, as k81d's heavy proof
            r1.LidOpen = false; r2.LidOpen = false;
            { double s0 = W.Time; float w1 = T; while ((W.Time < s0 + 1.6 || T - w1 < 1.0f) && T - w1 < 10f && !done) yield return null; }
            var hinges = new Transform[2]; var rigs = new object[2]; var rest = new Quaternion[2]; var lidY0 = new float[2];
            int k = 0;
            foreach (var o in new[] { r1, r2 })
            {
                var v = View(o); hinges[k] = Node(v, "tank_HatchHinge"); rigs[k] = RigOf(v);
                rest[k] = RigGet(rigs[k], "hingeRest", hinges[k] ? hinges[k].localRotation : Quaternion.identity);
                lidY0[k] = LidY(hinges[k]); o.LidOpen = true; k++;
            }
            Focus(ax + 1, ay - 4, 24f);
            var maxLid = new float[2]; var lift = new float[2]; var held = new int[2]; var cmdTop = new[] { -9f, -9f }; var cmdUp = new bool[2]; int frames = 0;
            fw = T;
            while (T - fw < 4f && !done)
            {
                frames++;
                for (int i = 0; i < 2; i++)
                {
                    if ((i == 0 ? r1 : r2).LidOpen) held[i]++;
                    if (hinges[i]) { maxLid[i] = Mathf.Max(maxLid[i], Quaternion.Angle(rest[i], hinges[i].localRotation)); lift[i] = Mathf.Max(lift[i], LidY(hinges[i]) - lidY0[i]); }
                    float ct = CommanderTop(rigs[i], out bool up); if (up) { cmdUp[i] = true; cmdTop[i] = Mathf.Max(cmdTop[i], ct); }
                }
                yield return null;
            }
            yield return Shot("lt2_C_hatch_open_close", 0.1f);
            string Lid(int i) => $"lid opened {maxLid[i]:0} deg (its centre up {lift[i]:0.00} m, sign {RigGet(rigs[i], "hingeSign", 0f):+0;-0}), the sim kept it open {held[i]}/{frames} frames, the commander {(cmdUp[i] ? $"up, his top {cmdTop[i]:0.00} m over the rim" : "NOT shown")}";
            Verdict("hatch_v1", maxLid[0] > 60f && lift[0] > 0.05f && cmdUp[0] && cmdTop[0] > 0.2f, "v1 " + Lid(0));
            Verdict("hatch_v2", maxLid[1] > 60f && lift[1] > 0.05f && cmdUp[1] && cmdTop[1] > 0.2f, "v2 (the modelled square hatch) " + Lid(1));
            bool sn1 = r1.CanBeSniped(), sn2 = r2.CanBeSniped(), spot1 = W.SnipeSafeSpot(r1), spot2 = W.SnipeSafeSpot(r2);
            Verdict("snipe_rule", sn1 && sn2 && !spot1 && !spot2, $"driver open to a snipe with the lid open: v1 {sn1}, v2 {sn2} (the same rule, type light); on a bridge / door spot (DV82, DV87 B): v1 {spot1}, v2 {spot2}");
            r1.LidOpen = false; r2.LidOpen = false;
            if (done) yield break;

            // damage: smoke at 45 %, fire at 20 % (MaxHealth / 100 first: the posed tanks' 100 000 000 x 45 would overflow an int)
            int ign0 = OpenZDamageStates.Ignited;
            foreach (var o in new[] { r1, r2 }) { o.SetHealth(o.MaxHealth / 100 * 45); W.UpdateObjectHealth(o); }
            Focus(ax + 1, ay - 4, 32f); yield return Shot("lt2_D_damage_45", 2.5f);
            foreach (var o in new[] { r1, r2 }) { o.SetHealth(o.MaxHealth / 100 * 20); W.UpdateObjectHealth(o); }
            yield return Shot("lt2_D_damage_20", 3.0f);
            Verdict("damage_fire", OpenZDamageStates.Ignited - ign0 >= 2, $"{OpenZDamageStates.Ignited - ign0} fires lit (both tanks at 20 %)");
            if (done) yield break;

            // death: both destroyed at once; a 10 fps frame burst for the MP4
            int br0 = OzTankWreck.Breaks, pc0 = OzTankWreck.PiecesMade; float td = Time.unscaledTime;
            string mp4 = Path.Combine(Dir, "mp4_" + g.level); Directory.CreateDirectory(mp4);
            Mark(LightV2Stage.DeathWays(r1, "red_v1", r2, "red_v2"));                                   // k84b: each tank's seeded way to break up (DV18), told first
            foreach (var o in new[] { r1, r2 }) { o.SetHealth(0); W.UpdateObjectHealth(o); }
            int f = 0; float nf = T; fw = T; string seenBreak = OzTankWreck.LastBreak;
            while (T - fw < LightV2Stage.DeathWaitS && !done && !(T - fw >= 9f && OzTankWreck.Breaks - br0 >= 2))   // k84b: a tank that cooks off (5-7 s of burning) broke after k84's 9 s: wait until both broke
            {
                if (OzTankWreck.LastBreak != seenBreak) { seenBreak = OzTankWreck.LastBreak; Mark($"break-up {OzTankWreck.Breaks - br0} at {T - fw:0.0} s after the kill: {seenBreak}"); }
                if (T >= nf) { ScreenCapture.CaptureScreenshot(Path.Combine(mp4, $"f_{f:000}.png")); f++; nf += 0.1f; }
                yield return null;
            }
            Mark($"death burst: {f} frames in {mp4} (ffmpeg -framerate 10 -i f_%03d.png)");
            int breaks = OzTankWreck.Breaks - br0, pieces = OzTankWreck.PiecesMade - pc0;
            Verdict("death_breakup", breaks >= 2, $"{breaks} tank break-ups ({pieces} pieces), last: {OzTankWreck.LastBreak}");
            Verdict("death_sounds", Count(td, "dst_turret_toss") >= 2, $"turret toss sounds {Count(td, "dst_turret_toss")}, exp_large {Count(td, "exp_large")}");
            yield return Shot("lt2_E_wrecks", 0.2f);
            dn.running = run0;
            Finish(null);
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
                File.WriteAllText(Path.Combine(Dir, (job ?? "run") + "_lt2_proof_log.txt"), log.ToString());
            }
            catch (Exception e) { Debug.LogError("[lt2proof] finish: " + e); }
            UnityEditor.EditorApplication.isPlaying = false;
        }
    }
}
#endif
