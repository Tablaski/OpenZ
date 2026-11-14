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
    /// Heavy tank v2 Play proof (1.2 items 110, 116, 118; editor only): one auto-started run per level (PlayerPrefs oz_hv2proof_auto = "L02" /
    /// "L17", set by the menu OpenZ/Heavy Tank v2/3). v1 and v2 side by side (pinned as the testing panel pins them), red, blue and neutral,
    /// on open ground; nobody fights while posing. Day and night frames at the level's RTS zoom and close (32 m); then, for the red pair:
    /// turret turn (an unkillable blue jeep to the side), fire (recoil of both barrels, turret shove, the heavyshot sounds), the hatch open
    /// with the commander out and the driver open to a snipe, damage smoke at 45 % and fire at 20 %, then both destroyed (blast, turret toss,
    /// break-apart, wreck) with a frame burst at 10 fps for the MP4. Each step a counted verdict for v1 AND v2.
    /// </summary>
    public partial class HeavyV2Proof : MonoBehaviour                                                 // k81c: + View/HeavyV2Proof.EmptyLook.cs (item 136)
    {
        static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.."));
        static readonly string Dir = Path.Combine(Root, "work/staging/v1_2_20261022/heavy_v2/proof");
        public const string AutoPref = "oz_hv2proof_auto";
        string job; float lastMark; bool done; int errors; float hot = -1f, lastSample = -1f; int peak = -1;
        readonly StringBuilder log = new StringBuilder();
        readonly List<string> verdicts = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Auto()
        {
            string job = PlayerPrefs.GetString(AutoPref, "");
            if (string.IsNullOrEmpty(job)) return;
            PlayerPrefs.DeleteKey(AutoPref); PlayerPrefs.Save();
            var go = new GameObject("HeavyV2Proof"); DontDestroyOnLoad(go);
            go.AddComponent<HeavyV2Proof>().job = job;
        }

        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        float T => (float)clock.Elapsed.TotalSeconds;
        void Mark(string s) { lastMark = T; log.AppendLine($"[{T,7:0.00}] {s}"); Debug.Log("[hv2proof] " + s); }
        void Verdict(string step, bool ok, string what) { string v = $"{step}: {(ok ? "PASS" : "FAIL")} - {what}"; verdicts.Add(v); Mark("VERDICT " + v); }
        IEnumerator Wait(float s) { float end = T + s; while (T < end && !done) yield return null; }
        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; if (!done) Finish("disabled"); }
        void OnLog(string c, string st, LogType type)
        {
            if (c.StartsWith("[hv2proof]")) return;
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
        {
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
        /// <summary>k81c: the lineup's centre is a FIXED tile per level (View/HeavyV2Spots.cs: chosen off the editor on a scan of the whole map
        /// with the sim's own tests; the six slots and the jeep as Start places them from it). The search that stood here found no window on
        /// L02 (25 x 15 tiles all passable does not exist there) and fell back to its start tile without a word, on a bridge spot.</summary>
        bool FixedCentre(out int cx, out int cy) => HeavyV2Spots.Get(g.level, "proof", "centre", out cx, out cy);
        /// <summary>k81c: every posed heavy asked again with the sim's own tests (ZPathFinder.StepOk, ZWorld.SnipeSafeSpot): all open = true.</summary>
        bool SlotsOpen(out string why)
        {
            var bad = new List<string>();
            foreach (var (o, tag) in units)
            {
                if (o.Type != OzHeavyTank.Type) continue;
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
        /// <summary>The world height of the lid's mesh centre (it rises when the hinge swings the lid open).</summary>
        static float LidY(Transform hinge)
        {
            if (!hinge) return 0f; float y = 0f; int n = 0;
            foreach (var r in hinge.GetComponentsInChildren<MeshRenderer>(true)) { y += r.bounds.center.y; n++; }
            return n > 0 ? y / n : hinge.position.y;
        }
        /// <summary>The hatch commander fully out (BoardingView: shown, risen) and his top over the collar rim (m).</summary>
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
        ZObject Heavy(int model, int tx, int ty, Team t, string tag)
        {
            var o = W.CreateObject("vehicle", OzHeavyTank.Type, tx * 16, ty * 16, t);
            OzHeavyTank.Pin(o, model); g.SpawnUnitView(o);
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
        {   // screen boxes (pixels from the top-left) of every posed unit, for the sheet crops
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
        int Count(float t0, string key) { int n = 0; foreach (var e in OzSoundLog.Since(t0)) if (e.key == key && e.outcome != null && e.outcome.StartsWith("PLAY")) n++; return n; }

        // ---------------------------------------------------------------- the run
        IEnumerator Start()
        {
            Directory.CreateDirectory(Dir); lastMark = T;
            Mark($"heavy tank v2 Play proof {job}, scene {SceneManager.GetActiveScene().name}, screen {Screen.width}x{Screen.height}, switch OzHeavyTank.Version {OzHeavyTank.Version}, v2 prefab {(OzHeavyTank.PrefabV2 ? "found" : "MISSING")}");
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
            Verdict("v2_prefab", OzHeavyTank.PrefabV2, "Resources/OpenZ/HeavyV2/Z_heavy_v2 (menu OpenZ/Heavy Tank v2/1)");
            if (!OzHeavyTank.PrefabV2) { Finish("no v2 prefab"); yield break; }
            {   // the heavy tanks placed in the level scene at the start (L17 and L16: two each, L02: none) are drawn as the switch says
                int placed = 0, asV2 = 0;                                                          // (ZGame.BindViews -> OzHeavyTank.SceneView)
                foreach (var o in W.Objects)
                {
                    if (o.Type != OzHeavyTank.Type || o.Source == null) continue;
                    var v = View(o); if (!v) continue;
                    placed++; if (OzHeavyTank.IsV2(v.transform)) asV2++;
                }
                Verdict("scene_heavies", asV2 == (OzHeavyTank.Version == 2 ? placed : 0),
                        $"{placed} heavy tank(s) placed in the level scene, {asV2} drawn as v2 (switch {OzHeavyTank.Version}, {OzHeavyTank.SceneSwaps} scene view(s) taken over){(placed == 0 ? "; none on this map" : "")}");
            }

            if (!FixedCentre(out int ax, out int ay)) { Mark($"SPOT FAIL: no fixed lineup centre for {g.level} in HeavyV2Spots"); Finish("spot check failed: no fixed lineup centre"); yield break; }
            Mark($"lineup window centre tile ({ax},{ay}) (fixed: View/HeavyV2Spots.cs, chosen on the whole-map scan with the sim's own tests)");
            var r1 = Heavy(1, ax - 6, ay - 4, Team.Red, "red_v1"); var r2 = Heavy(2, ax - 1, ay - 4, Team.Red, "red_v2");
            var b1 = Heavy(1, ax - 6, ay + 1, Team.Blue, "blue_v1"); var b2 = Heavy(2, ax - 1, ay + 1, Team.Blue, "blue_v2");
            var n1 = Heavy(1, ax + 5, ay - 4, Team.Neutral, "neutral_v1"); var n2 = Heavy(2, ax + 10, ay - 4, Team.Neutral, "neutral_v2");
            yield return Wait(2.0f);
            var pairs = new[] { (r1, r2, "red"), (b1, b2, "blue"), (n1, n2, "neutral") };
            int v2Views = 0, v1Views = 0;
            foreach (var (a, b, tag) in pairs)
            {
                var va = View(a); var vb = View(b);
                if (va && !OzHeavyTank.IsV2(va.transform)) v1Views++;
                if (vb && OzHeavyTank.IsV2(vb.transform)) v2Views++;
            }
            Verdict("models", v1Views == 3 && v2Views == 3, $"{v1Views}/3 views drawn as v1, {v2Views}/3 as v2 (pinned as the testing panel pins them)");
            Mark("lineup spots (a bridge / door spot shuts every lid at once, DV82 / DV87 B): " + string.Join(", ", units.ConvertAll(u => $"{u.tag} {(W.SnipeSafeSpot(u.o) ? "ON ONE" : "open")}")));
            if (!SlotsOpen(out string spotWhy)) { Mark("SPOT FAIL: " + spotWhy); Finish("spot check failed: " + spotWhy); yield break; }   // k81c: loud, never a proof on a protected spot
            {
                var va = View(r1); var vb = View(r2);
                var rf = typeof(UnitView).GetField("radius", BindingFlags.NonPublic | BindingFlags.Instance);              // UnitView's spacing radius (private): read for the check
                float ra = va && rf != null ? (float)rf.GetValue(va) : -1f, rb = vb && rf != null ? (float)rf.GetValue(vb) : -1f;
                Verdict("footprint", va && vb && Mathf.Abs(ra - rb) < 0.06f, $"UnitView radius v1 {ra:0.00} m, v2 {rb:0.00} m (spacing / no touching)");
                var rhA = va ? va.GetComponent<RoundHatch>() : null; var rhB = vb ? vb.GetComponent<RoundHatch>() : null;
                Mark($"round hatch: v1 {(rhA ? "built" : "not yet")}, v2 {(rhB ? (rhB.Frame && rhB.Frame.name == OzHeavyTank.FrameNode ? "the modelled one" : "BUILT (wrong)") : "not yet")}");
            }

            // day + night frames
            yield return TimeOfDay(0.08f);
            Focus(ax + 11, ay, h0); yield return Shot("hv2_A_day_rts");
            yield return EmptyLook(n1, n2, "rts");                                                         // k81c, item 136: the empty v1 and v2 side by side at the RTS camera, measured
            Focus(ax + 1, ay - 4, 32f); yield return Shot("hv2_A_day_close_red");
            Focus(ax + 1, ay + 1, 32f); yield return Shot("hv2_A_day_close_blue");
            Focus(ax + 11, ay - 4, 32f); yield return Shot("hv2_A_day_close_neutral");
            yield return EmptyLook(n1, n2, "close");                                                       // k81c, item 136: the same, close
            yield return TimeOfDay(0.62f);
            Focus(ax + 11, ay, h0); yield return Shot("hv2_A_night_rts", 0.8f);
            Focus(ax + 1, ay - 4, 32f); yield return Shot("hv2_A_night_close_red");
            Focus(ax + 11, ay - 4, 32f); yield return Shot("hv2_A_night_close_neutral");
            yield return TimeOfDay(0.08f);
            if (done) yield break;

            // turret turn + fire: an unkillable blue jeep 4 tiles south-east of the red pair; the red pair gets its attack back
            var jeep = W.CreateObject("vehicle", "jeep", (ax + 3) * 16, (ay - 9) * 16, Team.Blue); g.SpawnUnitView(jeep);
            jeep.MaxHealth = 100000000; jeep.SetHealth(jeep.MaxHealth); jeep.Damage = 0; jeep.AttackRadius = 0; units.Add((jeep, "target"));
            foreach (var (o, _) in units) if (o != jeep && o.Owner == Team.Blue) o.AttackRadius = 0;
            var watch = new List<(string tag, UnitView v, Transform yaw, Transform recL, Transform recR, Quaternion yaw0, Vector3 rl0, Vector3 rr0, Vector3 ty0)>();
            foreach (var (o, tag) in new[] { (r1, "v1"), (r2, "v2") })
            {
                var v = View(o); var yaw = Node(v, "HeavyTankTurretYaw"); var rl = Node(v, "HeavyTankRecoil_L"); var rr = Node(v, "HeavyTankRecoil_R");
                watch.Add((tag, v, yaw, rl, rr, yaw ? yaw.localRotation : Quaternion.identity, rl ? rl.localPosition : Vector3.zero, rr ? rr.localPosition : Vector3.zero, yaw ? yaw.localPosition : Vector3.zero));
                o.Damage = o.U.Damage; o.AttackRadius = o.U.attack_radius;
            }
            Focus(ax + 1, ay - 6, 34f);
            float t0 = Time.unscaledTime; float fw = T;
            var maxYaw = new float[2]; var maxRecL = new float[2]; var maxRecR = new float[2]; var maxShove = new float[2];
            int burst = 0; float nextB = T + 1.0f;
            while (T - fw < 16f && !done)
            {
                for (int i = 0; i < watch.Count; i++)
                {
                    var w = watch[i];
                    if (w.yaw) { maxYaw[i] = Mathf.Max(maxYaw[i], Quaternion.Angle(w.yaw0, w.yaw.localRotation)); maxShove[i] = Mathf.Max(maxShove[i], (w.yaw.localPosition - w.ty0).magnitude); }
                    if (w.recL) maxRecL[i] = Mathf.Max(maxRecL[i], (w.recL.localPosition - w.rl0).magnitude);
                    if (w.recR) maxRecR[i] = Mathf.Max(maxRecR[i], (w.recR.localPosition - w.rr0).magnitude);
                }
                if (T >= nextB && burst < 12) { ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"{g.level}_hv2_B_fire_{burst:00}.png")); burst++; nextB = T + 0.9f; }
                yield return null;
            }
            int shots = Count(t0, "heavyshot");
            for (int i = 0; i < watch.Count; i++)
                Verdict("turret_turn_" + watch[i].tag, maxYaw[i] > 20f, $"turret turned up to {maxYaw[i]:0} deg toward the jeep");
            for (int i = 0; i < watch.Count; i++)
                Verdict("recoil_" + watch[i].tag, maxRecL[i] > 0.15f && maxRecR[i] > 0.15f, $"barrel slide L {maxRecL[i]:0.00} m, R {maxRecR[i]:0.00} m (v1 ~0.33-0.38)");
            for (int i = 0; i < watch.Count; i++)
                Verdict("turret_recoil_" + watch[i].tag, maxShove[i] > 0.03f, $"turret shove {maxShove[i]:0.000} m on its ring");
            Verdict("fire_sound", shots >= 4, $"{shots} heavyshot plays in 16 s (both tanks firing; sound log)");
            foreach (var (o, _) in units) { o.Damage = 0; o.AttackRadius = 0; o.AttackObject = null; }
            if (done) yield break;

            // hatch: the lid opens, the commander comes out, the driver can be sniped. Both lids shut first, long enough for a close the
            // fire phase scheduled (at most 1.4 s of sim time, zvehicle.cpp:198-240) to land; then both open in the same frame and only the
            // sim keeps or shuts them (never opened again by this proof); the angle is read from the rest BoardingView turns the hinge from
            // k81d: the fire phase can make a crew a VETERAN (DV12: ZWorld.VeteranKills kills; on L02 the level's own blue grunts stand two
            // tiles off red_v1), and a veteran crew stays buttoned up (BoardingView.ShowHatchDriver: no commander, the drawn lid shut, v1 and
            // v2 alike): the hatch is checked on a crew that is not one (staged, logged)
            foreach (var (o, tag) in new[] { (r1, "red_v1"), (r2, "red_v2") })
            {
                Mark($"{tag} before the hatch check: kills {o.Kills}, veteran {o.Veteran} (perks on: {o.VeteranActive})");
                if (o.Veteran) { o.Veteran = false; o.Kills = 0; o.PromotedAt = -1; Mark($"staged: {tag}'s crew rank reset for the hatch check (a veteran crew stays buttoned up, DV12)"); }
            }
            r1.LidOpen = false; r2.LidOpen = false;
            { double s0 = W.Time; float w1 = T; while ((W.Time < s0 + 1.6 || T - w1 < 1.0f) && T - w1 < 10f && !done) yield return null; }
            var hinges = new Transform[2]; var rigs = new object[2]; var rest = new Quaternion[2]; var lidY0 = new float[2];
            int k = 0;
            foreach (var o in new[] { r1, r2 })
            {
                var v = View(o); hinges[k] = Node(v, "heavy_HatchHinge"); rigs[k] = RigOf(v);
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
            yield return Shot("hv2_C_hatch_open_close", 0.1f);
            string Lid(int i) => $"lid opened {maxLid[i]:0} deg (its centre up {lift[i]:0.00} m, sign {RigGet(rigs[i], "hingeSign", 0f):+0;-0}), the sim kept it open {held[i]}/{frames} frames, the commander {(cmdUp[i] ? $"up, his top {cmdTop[i]:0.00} m over the rim" : "NOT shown")}";
            Verdict("hatch_v1", maxLid[0] > 60f && lift[0] > 0.1f && cmdUp[0] && cmdTop[0] > 0.2f, "v1 " + Lid(0));
            Verdict("hatch_v2", maxLid[1] > 60f && lift[1] > 0.1f && cmdUp[1] && cmdTop[1] > 0.2f, "v2 (the left, real cupola) " + Lid(1));
            bool sn1 = r1.CanBeSniped(), sn2 = r2.CanBeSniped(), spot1 = W.SnipeSafeSpot(r1), spot2 = W.SnipeSafeSpot(r2);
            Verdict("snipe_rule", sn1 && sn2 && !spot1 && !spot2, $"driver open to a snipe with the lid open: v1 {sn1}, v2 {sn2} (the same rule, type heavy); on a bridge / door spot (DV82, DV87 B): v1 {spot1}, v2 {spot2}");
            r1.LidOpen = false; r2.LidOpen = false;
            if (done) yield break;

            // damage: smoke at 45 %, fire at 20 % (MaxHealth / 100 first: the posed tanks' 100 000 000 x 45 would overflow an int)
            int ign0 = OpenZDamageStates.Ignited;
            foreach (var o in new[] { r1, r2 }) { o.SetHealth(o.MaxHealth / 100 * 45); W.UpdateObjectHealth(o); }
            Focus(ax + 1, ay - 4, 32f); yield return Shot("hv2_D_damage_45", 2.5f);
            foreach (var o in new[] { r1, r2 }) { o.SetHealth(o.MaxHealth / 100 * 20); W.UpdateObjectHealth(o); }
            yield return Shot("hv2_D_damage_20", 3.0f);
            Verdict("damage_fire", OpenZDamageStates.Ignited - ign0 >= 2, $"{OpenZDamageStates.Ignited - ign0} fires lit (both tanks at 20 %)");
            if (done) yield break;

            // death: both destroyed at once; a 10 fps frame burst for the MP4
            int br0 = OzTankWreck.Breaks, pc0 = OzTankWreck.PiecesMade; float td = Time.unscaledTime;
            string mp4 = Path.Combine(Dir, "mp4_" + g.level); Directory.CreateDirectory(mp4);
            Mark(LightV2Stage.DeathWays(r1, "red_v1", r2, "red_v2"));                                   // k87b, item 10: each tank's seeded way to break up (DV18), told first
            foreach (var o in new[] { r1, r2 }) { o.SetHealth(0); W.UpdateObjectHealth(o); }
            int f = 0; float nf = T; fw = T; string seenBreak = OzTankWreck.LastBreak;
            while (T - fw < LightV2Stage.DeathWaitS && !done && !(T - fw >= 9f && OzTankWreck.Breaks - br0 >= 2))   // k87b, item 10: the second tank cooks off after 6-7 s of burning (after the 9 s count): wait until both broke
            {
                if (OzTankWreck.LastBreak != seenBreak) { seenBreak = OzTankWreck.LastBreak; Mark($"break-up {OzTankWreck.Breaks - br0} at {T - fw:0.0} s after the kill: {seenBreak}"); }
                if (T >= nf) { ScreenCapture.CaptureScreenshot(Path.Combine(mp4, $"f_{f:000}.png")); f++; nf += 0.1f; }
                yield return null;
            }
            Mark($"death burst: {f} frames in {mp4} (ffmpeg -framerate 10 -i f_%03d.png)");
            int breaks = OzTankWreck.Breaks - br0, pieces = OzTankWreck.PiecesMade - pc0;
            Verdict("death_breakup", breaks >= 2, $"{breaks} tank break-ups ({pieces} pieces), last: {OzTankWreck.LastBreak}");
            Verdict("death_sounds", Count(td, "dst_turret_toss") >= 2, $"turret toss sounds {Count(td, "dst_turret_toss")}, exp_large {Count(td, "exp_large")}");
            yield return Shot("hv2_E_wrecks", 0.2f);
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
                if (emptyLook.Count > 0) { log.AppendLine("## EMPTY LOOK (item 136; not one of the verdicts above)"); foreach (var e in emptyLook) log.AppendLine("  " + e); }
                Directory.CreateDirectory(Dir);
                File.WriteAllText(Path.Combine(Dir, (job ?? "run") + "_hv2_proof_log.txt"), log.ToString());
            }
            catch (Exception e) { Debug.LogError("[hv2proof] finish: " + e); }
            UnityEditor.EditorApplication.isPlaying = false;
        }
    }
}
#endif
