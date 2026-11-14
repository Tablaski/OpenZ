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
    /// The gun measured on the DRAWN model every frame (editor only; the Play proof and the gun films use it): the turret's heading (its turns,
    /// reversals and flutters: two turn reversals within 8 frames with no new aim between them and less than 15 deg apart = a shake, lesson 5),
    /// the barrel's elevation and slide, the turret's kick (HeavyRecoil's head slide and rock, lesson 6) and the BASE: the unit's root and the
    /// plinth node against where they stood when the watch began (a building: they never move; item 140 "no base rock").
    /// </summary>
    public sealed class GunWatch
    {
        public readonly string Tag; public readonly UnitView View; public readonly int Model;
        readonly Transform yaw, pitch, recoil, pedestal; readonly Vector3 recoilRest; readonly Quaternion pitchRest;
        Vector3 rootPos0, pedPos0; Quaternion rootRot0, pedRot0; bool init;
        float h0 = float.NaN, lastH = float.NaN, headAtRev; int lastSign, lastRevF = -1000, lastAimF = -1000, lastTgt = -2, f;
        public int Frames, Reversals, Flutters, Jumps, Shots; float lastFire = -100f;
        public float TurnMax, PitchUp, PitchDown, SlideMax, HeadSlideMax, HeadRockMax, RootMoveMax, RootTurnMax, PedMoveMax, PedTurnMax;
        public readonly List<string> Rows = new List<string>();

        public GunWatch(string tag, UnitView v, int model)
        {
            Tag = tag; View = v; Model = model;
            if (!v) return;
            foreach (var t in v.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "CannonTurretYaw") yaw = t;
                else if (t.name == "CannonBarrelPitch") pitch = t;
                else if (t.name == "CannonBarrelRecoil") recoil = t;
                else if (t.name == "CannonPedestal") pedestal = t;
            }
            if (recoil) recoilRest = recoil.localPosition;
            if (pitch) pitchRest = pitch.localRotation;
        }
        public bool Ok => View && yaw && pitch && recoil && pedestal;

        /// <summary>Once per frame, after every LateUpdate.</summary>
        public void Sample(float t)
        {
            if (!Ok || View.Dying || View.Obj == null || View.Obj.Destroyed) return;
            var root = View.transform;
            if (!init) { init = true; rootPos0 = root.position; rootRot0 = root.rotation; pedPos0 = pedestal.position; pedRot0 = pedestal.rotation; }
            Frames++; f++;
            RootMoveMax = Mathf.Max(RootMoveMax, (root.position - rootPos0).magnitude); RootTurnMax = Mathf.Max(RootTurnMax, Quaternion.Angle(rootRot0, root.rotation));
            PedMoveMax = Mathf.Max(PedMoveMax, (pedestal.position - pedPos0).magnitude); PedTurnMax = Mathf.Max(PedTurnMax, Quaternion.Angle(pedRot0, pedestal.rotation));
            float pa = Mathf.DeltaAngle(0f, (Quaternion.Inverse(pitchRest) * pitch.localRotation).eulerAngles.x);   // + = the barrel up (about its local x axis: the front, -z, rises)
            PitchUp = Mathf.Max(PitchUp, pa); PitchDown = Mathf.Max(PitchDown, -pa);
            SlideMax = Mathf.Max(SlideMax, (recoil.localPosition - recoilRest).magnitude);
            var hr = View.HeavyRecoilView;
            if (hr) { HeadSlideMax = Mathf.Max(HeadSlideMax, hr.HeadSlideNow); HeadRockMax = Mathf.Max(HeadRockMax, hr.HeadRockNow); }
            if (View.LastFireAt > lastFire + 1e-4f) { lastFire = View.LastFireAt; Shots++; }
            var aim = View.GetComponent<UnitAim>();
            float h = aim && aim.ProofBarrelHeading(out float bh) ? bh : float.NaN;
            int tgt = View.Obj.AttackObject != null ? View.Obj.AttackObject.Id : -1;
            if (tgt != lastTgt || (aim && aim.ProofHeld > 0)) lastAimF = f;
            lastTgt = tgt;
            if (!float.IsNaN(h))
            {
                if (float.IsNaN(h0)) h0 = h;
                TurnMax = Mathf.Max(TurnMax, Mathf.Abs(Mathf.DeltaAngle(h0, h)));
                if (!float.IsNaN(lastH))
                {
                    float step = Mathf.DeltaAngle(lastH, h), w = step / Mathf.Max(1e-4f, Time.deltaTime);
                    if (Mathf.Abs(step) > 25f) Jumps++;
                    if (Mathf.Abs(w) >= 3f)
                    {
                        int sg = w > 0f ? 1 : -1;
                        if (lastSign != 0 && sg != lastSign)
                        {
                            Reversals++;
                            if (f - lastRevF <= 8 && lastAimF < lastRevF && Mathf.Abs(Mathf.DeltaAngle(headAtRev, h)) < 15f) Flutters++;
                            lastRevF = f; headAtRev = h;
                        }
                        lastSign = sg;
                    }
                }
                lastH = h;
            }
            if (Rows.Count < 20000)
                Rows.Add($"{Tag},{f},{t:0.000},{(float.IsNaN(h) ? "" : h.ToString("0.00"))},{pa:0.00},{(recoil.localPosition - recoilRest).magnitude:0.0000},{(hr ? hr.HeadSlideNow : 0f):0.0000},{(hr ? hr.HeadRockNow : 0f):0.000},{(root.position - rootPos0).magnitude:0.0000},{Quaternion.Angle(rootRot0, root.rotation):0.000},{(pedestal.position - pedPos0).magnitude:0.0000},{tgt},{(Time.time - View.LastFireAt < 0.1f ? 1 : 0)}");
        }
        /// <summary>The base never moves (1 mm, 0.01 deg).</summary>
        public bool BaseStill => Frames > 30 && RootMoveMax < 0.001f && RootTurnMax < 0.01f && PedMoveMax < 0.001f && PedTurnMax < 0.01f;
        public string BaseLine() => $"{Tag}: {Frames} frames, {Shots} shots: the unit's root moved at most {RootMoveMax * 1000f:0.0} mm and turned {RootTurnMax:0.000} deg, the plinth moved {PedMoveMax * 1000f:0.0} mm and turned {PedTurnMax:0.000} deg (a building: never)";
        public string KickLine() => $"{Tag}: the turret kicked back up to {HeadSlideMax:0.000} (its parent's units) and rocked its front up {HeadRockMax:0.00} deg (HeavyRecoil, model {(View && View.HeavyRecoilView ? View.HeavyRecoilView.Model : 0)}); the barrel slid back up to {SlideMax:0.000} and was laid {PitchUp:0.0} deg up / {PitchDown:0.0} deg down";
        public string TurnLine() => $"{Tag}: the turret turned up to {TurnMax:0} deg, turn reversals {Reversals}, flutters {Flutters}, jumps {Jumps}";
        public const string CsvHead = "unit,frame,t,heading_deg,pitch_deg_up,barrel_slide,turret_slide,turret_rock_deg,root_moved_m,root_turned_deg,plinth_moved_m,target,fired";
    }

    /// <summary>
    /// Gun v2 Play proof (editor only; the mech v2's proof, View/MechV2Proof.cs, for the gun): one auto-started run per level (PlayerPrefs
    /// oz_gn2proof_auto = "L02" / "L17", set by the menu OpenZ/Gun v2/3). v1 and v2 side by side (pinned as the testing panel pins them), red,
    /// blue and neutral, on the proof's FIXED open lineup (View/HeavyV2Spots.cs "proof": chosen on a scan of the whole map with the sim's own
    /// tests; a gun's 2 x 2 tiles fit where a heavy tank's do; every slot asked again here, a protected or blocked slot stops the proof loudly,
    /// lesson 7); nobody fights while posing. Day and night frames at the game's play zoom and close (32 m), the empty v1 and v2 measured side by
    /// side (the empty look: dark grey, never near white); then the red pair is ordered to attack a blue jeep (the game's own Attack order; the
    /// jeep cannot be destroyed and has no weapon): every frame the turret's turn (no flutter), the barrel's aim and slide, the turret's kick
    /// and the base (it never moves); the shot sound; the guns already on the level's fort towers and a v2 gun placed on a free tower pad: no
    /// part of the turret, barrel or gunner under the fort's top at any of 24 headings, no perch built on v2; blue snipers take the red pair's
    /// gunners (the gun goes neutral grey) and a red grunt mans each again; a red grunt mans each empty (neutral) gun and sits on its seat;
    /// damage smoke at 45 % and fire at 20 %; both red guns destroyed (blast, the chair breaking off, the turret tossed, the base breaking into
    /// parts, wreck) with a frame burst at 10 fps for the MP4. Each step a counted verdict for v1 AND v2.
    /// </summary>
    public class GunV2Proof : MonoBehaviour
    {
        static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.."));
        static readonly string Dir = Path.Combine(Root, "work/staging/v1_2_20261022/v2_units/gun/proof");
        public const string AutoPref = "oz_gn2proof_auto";
        static OzUnitModels M => OzGun.Models;
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
            var go = new GameObject("GunV2Proof"); DontDestroyOnLoad(go);
            go.AddComponent<GunV2Proof>().job = job;
        }

        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        float T => (float)clock.Elapsed.TotalSeconds;
        void Mark(string s) { lastMark = T; log.AppendLine($"[{T,7:0.00}] {s}"); Debug.Log("[gn2proof] " + s); }
        void Verdict(string step, bool ok, string what) { string v = $"{step}: {(ok ? "PASS" : "FAIL")} - {what}"; verdicts.Add(v); Mark("VERDICT " + v); }
        IEnumerator Wait(float s) { float end = T + s; while (T < end && !done) yield return null; }
        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; if (!done) Finish("disabled"); }
        void OnLog(string c, string st, LogType type)
        {
            if (c.StartsWith("[gn2proof]")) return;
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
        bool SlotsOpen(out string why)
        {
            var bad = new List<string>();
            foreach (var (o, tag) in units)
            {
                if (!OzGun.Is(o)) continue;
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
        static readonly FieldInfo TossedF = typeof(UnitView).GetField("tossed", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo BreakDoneF = typeof(UnitView).GetField("breakDone", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo RadiusF = typeof(UnitView).GetField("radius", BindingFlags.NonPublic | BindingFlags.Instance);
        void Focus(int tx, int ty, float h) { var p = ZGame.PxToWorld(tx * 16 + 8, ty * 16 + 8); cam.focus = new Vector3(p.x, 0f, p.z); cam.SetHeightImmediate(h); }
        void FocusWorld(Vector3 p, float h) { cam.focus = new Vector3(p.x, 0f, p.z); cam.SetHeightImmediate(h); }

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
        ZObject Gun(int model, int tx, int ty, Team t, string tag)
        {
            var o = W.CreateObject("cannon", OzGun.Type, tx * 16, ty * 16, t);
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

        // ---------------------------------------------------------------- the empty look (item 136: dark grey, never near white)
        static List<Vector3> TopPoints(UnitView v)
        {
            var pts = new List<Vector3>();
            if (!v) return pts;
            foreach (var mf in v.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh; var r = mf.GetComponent<Renderer>();
                if (!m || !r || !r.enabled || !m.isReadable) continue;
                string n = mf.name;
                if (!(n == "CannonTurretYaw_Surface" || n == "CannonPedestal_Surface")) continue;
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
                bool ok = OzV2EmptyLook.Verdict(true) && res[0].n > 20 && res[1].n > 20;   // k98, item 153 (View/OzV2EmptyLook.cs)
                string line = $"{g.level} {frame}: empty v1 mean sRGB {res[0].mean:0} ({res[0].white:0.0} % near white, {res[0].n} points), empty v2 mean {res[1].mean:0} ({res[1].white:0.0} % near white, {res[1].n} points): {(ok ? "PASS" : "FAIL")} ({OzV2EmptyLook.LastRule})";
                emptyLook.Add(line); Mark("EMPTY LOOK " + line);
            }
            finally { Destroy(tex); }
        }

        // ---------------------------------------------------------------- a gun on a fort tower: nothing under the fort's top at any heading
        /// <summary>The lowest height (m) of the turret's, the barrel's and the collar's vertices and of the seated gunner's drawn points over the
        /// fort's own top surface under them (TowerMounts.FortSurface: the tower top, its ledge ring and dome), the turret turned through 24
        /// headings about its own turn axis; under 0 = a part in the fort. The heading of the lowest point is told.
        /// gunv2_tower (3 Nov): the gunner was read as the bottom corners of his renderers' bounds. A skinned body's bounds are its import box
        /// in the hips bone's space (updateWhenOffscreen is off, so it is never fitted to the pose), 2.66 m tall at full size: at the gun v2's
        /// bigger gunner (gunner_seats) its bottom lies 0.72 m under his boots and 0.15 m under the tower plate, while his boots are 0.56 m over
        /// it. Now his skinned body is read as posed (BakeMesh), a rigid part by its vertices or, not readable in Play, by its own box (tight to
        /// the part); effects (particles, trails, lines) are left out. The old box reading is printed beside it and not judged. The caller
        /// keeps the gunner animated for the read (his pose is written only while a camera draws him).</summary>
        string TowerClearance(UnitView v, out float min)
        {
            min = 99f; float minAt = 0f; string k92What = "-";
            var fort = v ? v.TowerFort : null; var yaw = Node(v, "CannonTurretYaw");
            if (!fort || !yaw || !yaw.parent) return "no fort / turret";
            var parts = new List<(Matrix4x4 m, Vector3[] vs)>(); int partPts = 0;
            foreach (var mf in yaw.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh || !mf.sharedMesh.isReadable || mf.name.StartsWith("frag_")) continue;
                var vs = mf.sharedMesh.vertices; int step = Mathf.Max(1, vs.Length / 400); var kept = new List<Vector3>();
                for (int i = 0; i < vs.Length; i += step) kept.Add(vs[i]);
                parts.Add((yaw.worldToLocalMatrix * mf.transform.localToWorldMatrix, kept.ToArray())); partPts += kept.Count;   // mesh -> turret space, at this frame's pose
            }
            var crew = RigGet<GameObject>(RigOf(v), "crew", null);
            var crewPts = new List<Vector3>(); var crewWho = new List<string>(); var boxPts = new List<Vector3>(); int crewBoxed = 0;
            if (crew && crew.activeInHierarchy)
            {
                var bake = new Mesh(); var vl = new List<Vector3>();
                try
                {
                    foreach (var r in crew.GetComponentsInChildren<Renderer>())
                    {
                        if (!r.enabled) continue; var b = r.bounds;
                        for (int i = 0; i < 4; i++) boxPts.Add(yaw.InverseTransformPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, b.min.y, (i & 2) == 0 ? b.min.z : b.max.z)));   // the old reading
                        if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                        Matrix4x4 m; vl.Clear(); string who = r.name;
                        if (r is SkinnedMeshRenderer sk) { if (!sk.sharedMesh) continue; sk.BakeMesh(bake, true); bake.GetVertices(vl); m = sk.transform.localToWorldMatrix; }   // as posed now; useScale true: the mesh's own size, so the full matrix gives the world
                        else { var mf = r.GetComponent<MeshFilter>(); if (mf && mf.sharedMesh && mf.sharedMesh.isReadable) mf.sharedMesh.GetVertices(vl); m = r.transform.localToWorldMatrix; }
                        if (vl.Count == 0)
                        {   // not readable in Play (the robots' kit parts): its own box, the bounds of its mesh (tight to the part)
                            crewBoxed++; who += " (its box)";
                            for (int i = boxPts.Count - 4; i < boxPts.Count; i++) { crewPts.Add(boxPts[i]); crewWho.Add(who); }
                            continue;
                        }
                        int step = Mathf.Max(1, vl.Count / 4000);
                        for (int i = 0; i < vl.Count; i += step) { crewPts.Add(yaw.InverseTransformPoint(m.MultiplyPoint3x4(vl[i]))); crewWho.Add(who); }
                    }
                }
                finally { Destroy(bake); }
            }
            var pivot = yaw.position; var baseRot = yaw.rotation; float crewMin = 99f, boxMin = 99f, crewLo = 1e9f, crewHi = -1e9f;
            foreach (var p in crewPts) { float h = yaw.TransformPoint(p).y; crewLo = Mathf.Min(crewLo, h); crewHi = Mathf.Max(crewHi, h); }
            for (int k = 0; k < 24; k++)
            {
                var turn = Quaternion.AngleAxis(k * 15f, yaw.parent.up);
                var M4 = Matrix4x4.TRS(pivot, turn * baseRot, yaw.lossyScale);
                foreach (var (m, vs) in parts)
                    foreach (var p in vs)
                    {
                        var w = M4.MultiplyPoint3x4(m.MultiplyPoint3x4(p));
                        if (!TowerMounts.FortSurface(fort, w, w.y + 2f, out float y)) continue;
                        if (w.y - y < min) { min = w.y - y; minAt = k * 15f; k92What = "a turret / barrel point"; }
                    }
                for (int i = 0; i < crewPts.Count; i++)
                {
                    var w = M4.MultiplyPoint3x4(crewPts[i]);
                    if (!TowerMounts.FortSurface(fort, w, w.y + 2f, out float y)) continue;
                    crewMin = Mathf.Min(crewMin, w.y - y);
                    if (w.y - y < min) { min = w.y - y; minAt = k * 15f; k92What = "a seated gunner point, " + crewWho[i]; }
                }
                foreach (var p in boxPts)
                {
                    var w = M4.MultiplyPoint3x4(p);
                    if (TowerMounts.FortSurface(fort, w, w.y + 2f, out float y)) boxMin = Mathf.Min(boxMin, w.y - y);
                }
            }
            return $"lowest turret / barrel / gunner point {min:0.000} m over the fort's top (at +{minAt:0} deg of 24 headings: {k92What}), gunner figure {(crewPts.Count > 0 ? "in" : "not seated")}"
                 + $"; points read: turret / barrel {partPts}, gunner {crewPts.Count} drawn ({crewBoxed} part(s) by their own box); the gunner alone: lowest {(crewPts.Count > 0 ? crewMin.ToString("0.000") : "-")} m, drawn height {(crewPts.Count > 0 ? (crewHi - crewLo).ToString("0.00") : "-")} m; his renderers' boxes {(boxPts.Count > 0 ? boxMin.ToString("0.000") : "-")} m (the old reading, not judged)";
        }

        // ---------------------------------------------------------------- the run
        IEnumerator Start()
        {
            Directory.CreateDirectory(Dir); lastMark = T;
            Mark($"Gun v2 Play proof {job}, scene {SceneManager.GetActiveScene().name}, screen {Screen.width}x{Screen.height}, switch OzGun.Version {OzGun.Version}, v2 prefab {(M.PrefabV2 ? "found" : "MISSING")}");
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
            Verdict("v2_prefab", M.PrefabV2, "Resources/OpenZ/GunV2/Z_gun_v2 (menu OpenZ/Gun v2/1)");
            if (!M.PrefabV2) { Finish("no v2 prefab"); yield break; }
            {   // guns of the level (placed in the scene, or made from the level data: the fort towers) drawn as the switch says
                int placed = 0, asV2 = 0, tower = 0;
                foreach (var o in W.Objects)
                {
                    if (!OzGun.Is(o) || o.Destroyed) continue;
                    var v = View(o); if (!v) continue;
                    placed++; if (M.IsV2(v.transform)) asV2++; if (v.OnTower) tower++;
                }
                Verdict("level_guns", asV2 == (OzGun.Version == 2 ? placed : 0),
                        $"{placed} guns of the level ({tower} on fort towers), {asV2} drawn as v2 (switch {OzGun.Version}, {M.SceneSwaps} scene view(s) taken over){(placed == 0 ? "; none on this map" : "")}");
            }

            var spots = new Dictionary<string, (int x, int y)>();
            foreach (var n in new[] { "red_v1", "red_v2", "blue_v1", "blue_v2", "neutral_v1", "neutral_v2", "jeep", "centre" })
            {
                if (!Spot(n, out int sx, out int sy)) { Mark($"SPOT FAIL: no fixed tile {g.level}/proof/{n} in HeavyV2Spots"); Finish("spot check failed: " + n); yield break; }
                spots[n] = (sx, sy);
            }
            Mark("fixed lineup (View/HeavyV2Spots.cs, the proof's open lineup): " + string.Join(", ", new List<string>(spots.Keys).ConvertAll(k => $"{k} {spots[k]}")));
            var r1 = Gun(1, spots["red_v1"].x, spots["red_v1"].y, Team.Red, "red_v1"); var r2 = Gun(2, spots["red_v2"].x, spots["red_v2"].y, Team.Red, "red_v2");
            var b1 = Gun(1, spots["blue_v1"].x, spots["blue_v1"].y, Team.Blue, "blue_v1"); var b2 = Gun(2, spots["blue_v2"].x, spots["blue_v2"].y, Team.Blue, "blue_v2");
            var n1 = Gun(1, spots["neutral_v1"].x, spots["neutral_v1"].y, Team.Neutral, "neutral_v1"); var n2 = Gun(2, spots["neutral_v2"].x, spots["neutral_v2"].y, Team.Neutral, "neutral_v2");
            yield return Wait(2.0f);
            int v2Views = 0, v1Views = 0;
            foreach (var (a, b) in new[] { (r1, r2), (b1, b2), (n1, n2) })
            {
                var va = View(a); var vb = View(b);
                if (va && !M.IsV2(va.transform)) v1Views++;
                if (vb && M.IsV2(vb.transform)) v2Views++;
            }
            Verdict("models", v1Views == 3 && v2Views == 3, $"{v1Views}/3 views drawn as v1, {v2Views}/3 as v2 (pinned as the testing panel pins them)");
            Mark("lineup spots (a bridge / door spot holds the snipes, DV82 / DV87 B): " + string.Join(", ", units.ConvertAll(u => $"{u.tag} {(W.SnipeSafeSpot(u.o) ? "ON ONE" : "open")}")));
            if (!SlotsOpen(out string spotWhy)) { Mark("SPOT FAIL: " + spotWhy); Finish("spot check failed: " + spotWhy); yield break; }
            {
                var va = View(r1); var vb = View(r2);
                float ra = va && RadiusF != null ? (float)RadiusF.GetValue(va) : -1f, rb = vb && RadiusF != null ? (float)RadiusF.GetValue(vb) : -1f;
                float keep = vb ? OzGun.KeepClear(vb.transform) : 0f;
                Verdict("footprint", va && vb && ra >= 1.5f && rb >= keep - 0.01f && rb <= 3f,
                        $"UnitView spacing radius (view only) v1 {ra:0.00} m (as before), v2 {rb:0.00} m (its chair's sweep {keep + OzGun.SweepKeepClear:0.00} m from its place, less {OzGun.SweepKeepClear}: robots never drawn in the chair); the sim's footprint 2 x 2 tiles for both");
                bool nose1 = Node(va, "OZ_GunNoseCollar"), nose2 = Node(vb, "OZ_GunNoseCollar");
                var mz2 = Node(vb, "CannonMuzzle"); var tube2 = Node(vb, "CannonBarrelRecoil_Surface"); var tm = tube2 ? tube2.GetComponent<MeshFilter>() : null;
                Verdict("muzzle", nose1 && !nose2 && mz2 && tm && Mathf.Abs(mz2.localPosition.z - tm.sharedMesh.bounds.min.z) < 0.02f,
                        $"v1 wears its legacy nose collar (GunNose) {nose1}, v2 none {!nose2}; v2's muzzle socket at its own bore mouth (z {(mz2 ? mz2.localPosition.z : 0f):0.000}, the tube's front {(tm ? tm.sharedMesh.bounds.min.z : 0f):0.000})");
                var ua = va ? va.GetComponent<UnitAim>() : null; var ub = vb ? vb.GetComponent<UnitAim>() : null;
                Verdict("barrel_limits", ua && ub && Mathf.Approximately(ub.PitchMaxNow, OzGun.PitchMaxV2) && Mathf.Approximately(ua.PitchMaxNow, 40f) && Mathf.Approximately(ub.PitchMinNow, ua.PitchMinNow),
                        $"elevation v1 {(ua ? ua.PitchMinNow : 0f)}..{(ua ? ua.PitchMaxNow : 0f)} deg, v2 {(ub ? ub.PitchMinNow : 0f)}..{(ub ? ub.PitchMaxNow : 0f)} deg (v2's collar never over its box's front bar)");
            }

            // day + night frames, the empty look
            yield return TimeOfDay(0.08f);
            Focus(spots["centre"].x, spots["centre"].y, h0); yield return Shot("gn2_A_day_rts");
            yield return EmptyLook(n1, n2, "rts");
            Focus((spots["red_v1"].x + spots["red_v2"].x) / 2 + 1, spots["red_v2"].y, 32f); yield return Shot("gn2_A_day_close_red");
            Focus((spots["blue_v1"].x + spots["blue_v2"].x) / 2 + 1, spots["blue_v2"].y, 32f); yield return Shot("gn2_A_day_close_blue");
            Focus((spots["neutral_v1"].x + spots["neutral_v2"].x) / 2 + 1, spots["neutral_v2"].y, 32f); yield return Shot("gn2_A_day_close_neutral");
            yield return EmptyLook(n1, n2, "close");
            yield return TimeOfDay(0.62f);
            Focus(spots["centre"].x, spots["centre"].y, h0); yield return Shot("gn2_A_night_rts", 0.8f);
            Focus((spots["red_v1"].x + spots["red_v2"].x) / 2 + 1, spots["red_v2"].y, 32f); yield return Shot("gn2_A_night_close_red");
            Focus((spots["blue_v1"].x + spots["blue_v2"].x) / 2 + 1, spots["blue_v2"].y, 32f); yield return Shot("gn2_A_night_close_blue");
            {   // the blinkers: each model on its own data entry (v2: "gun@v2", measured on v2's meshes); cannons carry no VehicleLights
                string u1 = SpecType(UnitBlinkers.I, View(r1)), u2 = SpecType(UnitBlinkers.I, View(r2));
                Verdict("lights_data", (u1 == "-" || u1 == OzGun.Type) && (u2 == "-" || u2 == M.LightsKey) && u1 != u2 || (u1 == "-" && u2 == "-"),
                        $"UnitBlinkers v1 '{u1}', v2 '{u2}' (v2 must use '{M.LightsKey}'; '-' = the blinkers are off in this run)");
            }
            yield return TimeOfDay(0.08f);
            if (done) yield break;

            // fire: the red pair is ordered to attack an unkillable blue jeep (no weapon). Every frame: the turret's turn (no flutter), the barrel,
            // the turret's kick and the base (GunWatch)
            var jt = GunTarget(spots["red_v1"], spots["red_v2"], spots["jeep"], r1.U.attack_radius);
            var jeep = W.CreateObject("vehicle", "jeep", jt.x * 16, jt.y * 16, Team.Blue); g.SpawnUnitView(jeep);
            jeep.MaxHealth = 100000000; jeep.SetHealth(jeep.MaxHealth); jeep.Damage = 0; jeep.AttackRadius = 0; jeep.MoveSpeed = 0; units.Add((jeep, "target"));   // MoveSpeed 0: Zod's DodgeMissile never moves it out of reach
            foreach (var (o, _) in units) if (o != jeep && o.Owner == Team.Blue) o.AttackRadius = 0;
            var vv = new[] { View(r1), View(r2) };
            var watch = new[] { new GunWatch("v1", vv[0], 1), new GunWatch("v2", vv[1], 2) };
            Verdict("gun_nodes", watch[0].Ok && watch[1].Ok, $"turret, barrel pitch / recoil and plinth nodes found on v1 {watch[0].Ok} and v2 {watch[1].Ok}");
            int reach = r1.U.attack_radius;
            foreach (var o in new[] { r1, r2 })
            {
                o.Damage = o.U.Damage; o.AttackRadius = o.U.attack_radius;
                W.SendWaypoints(o.Owner, o, new List<ZWaypoint> { new ZWaypoint { mode = WpMode.Attack, refId = jeep.Id, x = jeep.CenterX, y = jeep.CenterY } });
                o.Engage(jeep);                                                                         // a gun cannot move: its target set at once (as the sim's own pick)
            }
            Mark($"the red pair ordered to attack the jeep at tile ({jt.x},{jt.y}) (the game's Attack order; the gun's range {reach} px; distances {Mathf.Sqrt(Sq(r1, jeep)):0} / {Mathf.Sqrt(Sq(r2, jeep)):0} px)");
            Focus((spots["red_v1"].x + spots["red_v2"].x + 2 * jt.x) / 4, (spots["red_v2"].y + jt.y) / 2, 30f);
            float t0 = Time.unscaledTime; float fw = T; int kick0 = HeavyRecoil.Kicks;
            int burst = 0; float nextB = T + 1.0f;
            while (T - fw < 20f && !done)
            {
                yield return new WaitForEndOfFrame();
                foreach (var w in watch) w.Sample(T - fw);
                if (T >= nextB && burst < 16) { ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"{g.level}_gn2_B_fire_{burst:00}.png")); burst++; nextB = T + 0.9f; }
            }
            int shots = Count(t0, "gunshot");
            for (int i = 0; i < 2; i++)
            {
                var w = watch[i]; string tag = w.Tag;
                Mark("GUN " + w.TurnLine() + " | " + w.KickLine() + " | " + w.BaseLine());
                Verdict("turret_turn_" + tag, w.TurnMax > 15f && w.Flutters == 0 && w.Jumps == 0, w.TurnLine() + " (eased onto the jeep, no shake: lesson 5)");
                Verdict("barrel_" + tag, w.SlideMax > 0.2f && w.Shots >= 2, $"the barrel slid back up to {w.SlideMax:0.000} (model units; v1's 0.43 peak) on {w.Shots} shots, laid {w.PitchUp:0.0} deg up / {w.PitchDown:0.0} deg down");
                float dsl = HeavyRecoil.DesignHeadSlide(OzGun.Type, w.Model), drk = HeavyRecoil.DesignHeadRock(OzGun.Type);
                Verdict("turret_kick_" + tag, w.HeadSlideMax > 0.6f * dsl && w.HeadSlideMax <= 1.25f * dsl && w.HeadRockMax > 0.6f * drk && w.HeadRockMax <= 1.25f * drk,
                        w.KickLine() + $" (design {dsl:0.00} + {drk:0.0} deg; measured clear of the base up to 0.10 + 3.0 deg at every heading, kit tools/kick_clip_gun.py)");
                Verdict("base_planted_" + tag, w.BaseStill, w.BaseLine());
            }
            Verdict("fire_sound", shots >= 4 && HeavyRecoil.Kicks - kick0 >= 4, $"sound log: gunshot {shots} in 20 s; HeavyRecoil kicks {HeavyRecoil.Kicks - kick0}");
            {
                var csv = new StringBuilder(GunWatch.CsvHead + "\n");
                foreach (var w in watch) foreach (var r in w.Rows) csv.Append(r).Append('\n');
                File.WriteAllText(Path.Combine(Dir, g.level + "_gn2_frames.csv"), csv.ToString());
            }
            foreach (var (o, _) in units) { o.Damage = 0; o.AttackRadius = 0; o.AttackObject = null; }
            if (done) yield break;

            // the fort towers: every gun of the level on a tower, and a v2 gun placed on a free tower pad (when one is free)
            yield return Tower();
            if (done) yield break;

            // snipe: two blue snipers take each red gun's gunner (the gun goes neutral, dark grey); then a red grunt mans each again
            {
                var sn = new List<ZObject>();
                foreach (var (o, sp) in new[] { (r1, spots["red_v1"]), (r2, spots["red_v2"]) })
                {
                    if (!OzCheatSpawner.FreeTile(W, sp.x, sp.y + 6, 1, true, out int fx, out int fy)) { fx = sp.x; fy = sp.y + 6; }
                    var s = W.CreateRobotGroup("sniper", fx * 16, fy * 16, Team.Blue, 2);
                    g.SpawnUnitView(s); foreach (var m in s.Minions) g.SpawnUnitView(m);
                    foreach (var m in Members(s)) { m.MaxHealth = 100000000; m.SetHealth(m.MaxHealth); W.UpdateObjectHealth(m); }
                    W.SendWaypoints(Team.Blue, s, new List<ZWaypoint> { new ZWaypoint { mode = WpMode.Attack, refId = o.Id, x = o.CenterX, y = o.CenterY } });
                    sn.Add(s);
                }
                Mark($"snipe: two blue snipers ordered to attack each red gun (can be sniped: v1 {r1.CanBeSniped()}, v2 {r2.CanBeSniped()})");
                Focus((spots["red_v1"].x + spots["red_v2"].x) / 2 + 1, spots["red_v2"].y + 3, 34f);
                float ts = T; int shotN = 0; float nextS = T + 1.5f;
                while (T - ts < 45f && !done && (r1.Owner != Team.Neutral || r2.Owner != Team.Neutral))
                {
                    if (T >= nextS && shotN < 6) { yield return Shot($"gn2_C_snipe_{shotN:00}", 0f); shotN++; nextS = T + 2.5f; }
                    yield return null;
                }
                foreach (var s in sn) foreach (var m in Members(s)) if (!m.Destroyed) { m.SetHealth(0); W.UpdateObjectHealth(m); }
                yield return Wait(1.5f);
                yield return Shot("gn2_C_sniped", 0.2f);
                foreach (var (o, tag) in new[] { (r1, "v1"), (r2, "v2") })
                {
                    var tp = View(o) ? View(o).GetComponent<TeamPaint>() : null;
                    Verdict("snipe_" + tag, o.Owner == Team.Neutral && o.Drivers.Count == 0 && tp && tp.Team == Team.Neutral,
                            $"the gunner sniped off: owner {o.Owner}, drivers {o.Drivers.Count}, paint {(tp ? tp.Team.ToString() : "-")} (after {T - ts:0.0} s)");
                }
                yield return Board(new[] { (r1, "reman_v1", spots["red_v1"]), (r2, "reman_v2", spots["red_v2"]) }, "gn2_C_reman");
            }
            if (done) yield break;

            // boarding: a red grunt mans each empty (neutral) gun: he walks in behind it and sits on its seat, the gun turns red
            yield return Board(new[] { (n1, "board_v1", spots["neutral_v1"]), (n2, "board_v2", spots["neutral_v2"]) }, "gn2_D_board");
            if (done) yield break;

            // damage: smoke at 45 %, fire at 20 % (MaxHealth / 100 first: the posed 100 000 000 x 45 would overflow an int)
            int ign0 = OpenZDamageStates.Ignited;
            foreach (var o in new[] { r1, r2 }) { o.SetHealth(o.MaxHealth / 100 * 45); W.UpdateObjectHealth(o); }
            Focus((spots["red_v1"].x + spots["red_v2"].x) / 2 + 1, spots["red_v2"].y, 32f); yield return Shot("gn2_E_damage_45", 2.5f);
            foreach (var o in new[] { r1, r2 }) { o.SetHealth(o.MaxHealth / 100 * 20); W.UpdateObjectHealth(o); }
            yield return Shot("gn2_E_damage_20", 3.0f);
            Verdict("damage_fire", OpenZDamageStates.Ignited - ign0 >= 2, $"{OpenZDamageStates.Ignited - ign0} fires lit (both at 20 %)");
            if (done) yield break;

            // death: both destroyed at once; a 10 fps frame burst for the MP4; 2-4 s later the chair breaks off and the turret is tossed (Zod's
            // ECannonDeath); the base breaks into parts (DV18: 1 in 2 by the unit's seed; WreckFx.Force 1 for this step so both are shown, then 0)
            float td = Time.unscaledTime; int chairs0 = GunChairDebris.Broken; int force0 = WreckFx.Force; WreckFx.Force = 1;
            string mp4 = Path.Combine(Dir, "mp4_" + g.level); Directory.CreateDirectory(mp4);
            var dv = new[] { View(r1), View(r2) };
            foreach (var o in new[] { r1, r2 }) { o.SetHealth(0); W.UpdateObjectHealth(o); }
            int burstN = 0; float nf = T; fw = T; var tossed = new bool[2]; var broke = new bool[2];
            while (T - fw < 14f && !done)
            {
                if (T >= nf && T - fw < 9f) { ScreenCapture.CaptureScreenshot(Path.Combine(mp4, $"f_{burstN:000}.png")); burstN++; nf += 0.1f; }
                for (int i = 0; i < 2; i++)
                {
                    if (!dv[i]) continue;
                    if (TossedF != null && TossedF.GetValue(dv[i]) is Transform tt && tt) tossed[i] = true;
                    if (BreakDoneF != null && BreakDoneF.GetValue(dv[i]) is bool bd && bd) broke[i] = true;
                }
                if (T - fw > 9f && broke[0] && broke[1] && tossed[0] && tossed[1]) break;
                yield return null;
            }
            WreckFx.Force = force0;
            Mark($"death burst: {burstN} frames in {mp4} (ffmpeg -framerate 10 -i f_%03d.png); WreckFx.Force back to {force0}");
            int chairs = GunChairDebris.Broken - chairs0;
            for (int i = 0; i < 2; i++)
                Verdict("death_" + (i == 0 ? "v1" : "v2"), tossed[i] && broke[i], $"the turret tossed {tossed[i]} (Zod's ECannonDeath toss of CannonTurretYaw), the base broke into parts {broke[i]} (WreckPieces, within {T - fw:0.0} s)");
            Verdict("chair_breaks_off", chairs >= 1, $"GunChairDebris: {chairs} chair(s) broke off as their own debris before the tosses (v1 its pan + backrest; v2 has no chair, k92)");   // k92: the gun v2 has no chair (View/OzGun.K92.cs)
            Verdict("death_sounds", Count(td, "dst_turret_toss") >= 2 && Count(td, "dst_debris_metal") >= 2,
                    $"dst_turret_toss {Count(td, "dst_turret_toss")}, dst_debris_metal {Count(td, "dst_debris_metal")}; blasts: exp_large {Count(td, "exp_large")}, explosion {Count(td, "explosion")}, vdeath {Count(td, "vdeath")}");
            yield return Shot("gn2_F_wrecks", 0.2f);
            dn.running = run0;
            Finish(null);
        }

        static float Sq(ZObject a, ZObject b) { float dx = a.CenterX - b.CenterX, dy = a.CenterY - b.CenterY; return dx * dx + dy * dy; }

        /// <summary>The target's tile for the red pair: the proof's own jeep tile when both guns reach it (centre to centre, a tile to spare: the
        /// gun's 128 px is shorter than the tanks' and the mech's reach the lineup was made for), else the nearest free vehicle tile north of the
        /// pair's middle that both reach (OzCheatSpawner.FreeTile, the testing panel's own test). Logged.</summary>
        (int x, int y) GunTarget((int x, int y) a, (int x, int y) b, (int x, int y) jeep, int range)
        {
            float reach = range - 16f;
            float Far(int x, int y)
            {
                float m = 0f;
                foreach (var (tx, ty) in new[] { a, b })
                {
                    float dx = x * 16 + 16 - (tx * 16 + 16), dy = y * 16 + 16 - (ty * 16 + 16);
                    m = Mathf.Max(m, Mathf.Sqrt(dx * dx + dy * dy));
                }
                return m;
            }
            if (Far(jeep.x, jeep.y) <= reach) { Mark($"the target: the proof's jeep tile ({jeep.x},{jeep.y}), {Far(jeep.x, jeep.y):0} px from the farther gun (reach {range} px)"); return jeep; }
            int mx = (a.x + b.x) / 2, my = Mathf.Min(a.y, b.y);
            for (int up = 5; up >= 3; up--)
                if (OzCheatSpawner.FreeTile(W, mx, my - up, 2, false, out int fx, out int fy) && Far(fx, fy) <= reach)
                {
                    Mark($"the target: the proof's jeep tile ({jeep.x},{jeep.y}) is {Far(jeep.x, jeep.y):0} px from the farther gun, beyond its {range} px: the jeep stands at the free tile ({fx},{fy}), {Far(fx, fy):0} px");
                    return (fx, fy);
                }
            Mark($"the target: no free tile within the gun's reach found north of the pair: the proof's jeep tile ({jeep.x},{jeep.y}) ({Far(jeep.x, jeep.y):0} px)");
            return jeep;
        }
        static IEnumerable<ZObject> Members(ZObject o) { yield return o; foreach (var m in o.Minions) yield return m; }

        /// <summary>A red grunt mans each gun (neutral): he walks in behind it and sits; verdict: the gun red, the crew figure on its seat.</summary>
        IEnumerator Board((ZObject o, string step, (int x, int y) sp)[] guns, string frames)
        {
            var crew = new List<ZObject>();
            ProofStageK92.HoldLevelUnits(W, System.Array.ConvertAll(guns, x => x.o), Team.Red, Mark, frames);   // k92: the level's own units near the guns stand still, unarmed (they manned them first: owner Blue)
            foreach (var (o, step, sp) in guns)
            {
                int gx = sp.x, gy = sp.y + 4;
                if (!OzCheatSpawner.FreeTile(W, gx, gy, 1, true, out int fx, out int fy)) { fx = gx; fy = gy; }
                var c = W.CreateRobotGroup("grunt", fx * 16, fy * 16, Team.Red, 1);
                g.SpawnUnitView(c); foreach (var m in c.Minions) g.SpawnUnitView(m);
                c.Damage = 0; c.AttackRadius = 0; foreach (var m in Members(c)) { m.MaxHealth = 100000000; m.SetHealth(m.MaxHealth); W.UpdateObjectHealth(m); }   // k93: staged, the crew cannot be killed on its way (the v1 reman take lost its grunt beside the proof's other guns)
                W.SendWaypoints(Team.Red, c, new List<ZWaypoint> { new ZWaypoint { mode = WpMode.Enter, refId = o.Id, x = o.CenterX, y = o.CenterY } });
                crew.Add(c);
            }
            Mark($"{frames}: a red grunt ordered to man each gun ({string.Join(", ", Array.ConvertAll(guns, x => x.step))})");
            Focus((guns[0].sp.x + guns[1].sp.x) / 2 + 1, guns[1].sp.y + 1, 26f);
            float fw = T; int shotN = 0; float nextS = T + 1f;
            while (T - fw < 30f && !done && (guns[0].o.Owner == Team.Neutral || guns[1].o.Owner == Team.Neutral || T - fw < 6f))
            {
                if (T >= nextS && shotN < 8) { yield return Shot($"{frames}_{shotN:00}", 0f); shotN++; nextS = T + 1.5f; }
                yield return null;
            }
            yield return Wait(3f);
            yield return Shot(frames + "_seated", 0f);
            foreach (var (o, step, sp) in guns)
            {
                var v = View(o); var rig = RigOf(v); var fig = RigGet<GameObject>(rig, "crew", null); var seat = Node(v, "CannonGunnerSeat");
                string mode = rig != null ? RigGet<object>(rig, "mode", null)?.ToString() ?? "-" : "-";
                float d = fig && seat ? Vector3.Distance(fig.transform.position, seat.position) : 99f;
                bool perch = Node(v, GunnerPerch.NodeName);
                Verdict(step, o.Owner == Team.Red && fig && fig.activeInHierarchy && d < 1.2f && !perch,
                        $"owner {o.Owner}, drivers {o.Drivers.Count}, rig mode {mode}, the gunner figure {(fig && fig.activeInHierarchy ? "shown" : "NOT shown")} {d:0.00} m from CannonGunnerSeat, no tower perch {!perch}");
            }
        }

        /// <summary>The fort towers: the level's own tower guns (any model, by the switch) and a v2 gun placed on a free tower pad of a fort.</summary>
        IEnumerator Tower()
        {
            var towerGuns = new List<(ZObject o, string tag)>();
            foreach (var o in W.Objects) { var v = View(o); if (OzGun.Is(o) && !o.Destroyed && v && v.OnTower) towerGuns.Add((o, "level tower gun #" + o.Id)); }
            ZObject placed = null;
            foreach (var f in new List<ZObject>(W.Objects))                                                  // a copy: a gun is made inside the loop
            {
                if (placed != null) break;
                if (!f.IsFort || f.Destroyed || !g.StaticViews.ContainsKey(f.Id)) continue;
                foreach (var (lx, ly) in TowerMounts.TurretSpots)
                {
                    int px = f.LocX + lx, py = f.LocY + ly; bool taken = false;
                    foreach (var c in W.Objects) if (c.Kind == ZKind.Cannon && !c.Destroyed && c.LocX == px && c.LocY == py) { taken = true; break; }
                    if (taken) continue;
                    placed = W.CreateObject("cannon", OzGun.Type, px, py, f.Owner == Team.Neutral ? Team.Red : f.Owner);
                    M.Pin(placed, 2); g.SpawnUnitView(placed); placed.Damage = 0; placed.AttackRadius = 0;   // staged: it never fires at the proof's units
                    Mark($"tower: a {placed.Owner} gun v2 placed on the free {f.Owner} {f.Type} tower pad at tile ({px >> 4},{py >> 4}) (fort-local px {lx},{ly})");
                    break;
                }
            }
            if (placed != null) towerGuns.Add((placed, "placed v2 tower gun #" + placed.Id));
            yield return Wait(2.5f);                                                                    // seated (UnitView.ResolveTower), crewed (BoardingView)
            { float k92W = T; while (T - k92W < 12f && !done && !towerGuns.TrueForAll(x => { var tv = View(x.o); var tf = RigGet<GameObject>(RigOf(tv), "crew", null); var ts = Node(tv, "CannonGunnerSeat"); return x.o.Drivers.Count == 0 || (tf && ts && tf.activeInHierarchy && Vector3.Distance(tf.transform.position, ts.position) < 1.2f); })) yield return null; }   // k92: measured with every gunner seated (a climbing gunner's box is no seated gunner)
            int n = 0, v2n = 0, ok = 0;
            foreach (var (o, tag) in towerGuns)
            {
                var v = View(o); if (!v || !v.OnTower) continue;
                n++; bool isV2 = M.IsV2(v.transform); if (isV2) v2n++;
                // gunv2_tower: the gunner is read as drawn, so he must be posed: his animator (CullUpdateTransforms) poses him only while a camera
                // draws him, and an unfocused editor stops drawing the Game view. Always animated for the read, then as it was; on screen for the frame.
                var crewGo = RigGet<GameObject>(RigOf(v), "crew", null); var crewAn = crewGo ? crewGo.GetComponentInChildren<Animator>() : null;
                var crewCull = crewAn ? crewAn.cullingMode : AnimatorCullingMode.CullUpdateTransforms; if (crewAn) crewAn.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                FocusWorld(v.transform.position, 24f); yield return Wait(0.8f);
                string line = TowerClearance(v, out float min);
                if (crewAn) crewAn.cullingMode = crewCull;
                bool perch = Node(v, GunnerPerch.NodeName);
                bool good = min > -0.005f && (isV2 ? !perch : true);
                if (good) ok++;
                Mark($"TOWER {tag} ({(isV2 ? "v2" : "v1")}, drawn x{v.transform.lossyScale.x:0.00}): {line}; perch {(perch ? "built" : "none")}{(isV2 && perch ? " (WRONG on v2)" : "")}");
                if (n <= 2) yield return Shot($"gn2_G_tower_{n}", 0.1f);                                    // gunv2_tower: focused and settled above
            }
            Verdict("tower", n > 0 && v2n > 0 && ok == n, $"{n} gun(s) on fort towers ({v2n} v2): {ok} with every turret / barrel / gunner point over the fort's top at all 24 headings and no perch on v2 (the TOWER lines above)");
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
                File.WriteAllText(Path.Combine(Dir, (job ?? "run") + "_gn2_proof_log.txt"), log.ToString());
            }
            catch (Exception e) { Debug.LogError("[gn2proof] finish: " + e); }
            UnityEditor.EditorApplication.isPlaying = false;
        }
    }
}
#endif
