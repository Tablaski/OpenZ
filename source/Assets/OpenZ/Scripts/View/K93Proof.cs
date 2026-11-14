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
    /// Kit k93 Play checks (editor menus OpenZ/k93 v2 play notes/...), test tools only (staged builds and kills, nothing saved):
    ///  * Exit: the player's vehicle factory and fort build each vehicle they can make (the tech lifted for the check and put back, a 1 s staged
    ///    build each): from its view's first frame until it is full size and out of the gate, every frame at the END of the frame, the drawn
    ///    box of every renderer (its mesh box through its transform: antennas, launchers, turrets as drawn) is read in the gate's own frame
    ///    against the gate's segments it is in: the most any part of it pokes through a side wall or a lintel. Pass: never more than 1 cm.
    ///    Four pictures per vehicle at the door (30 m).
    ///  * Wrecks: (A) six light tanks killed in view (plain wrecks) are all still lying there 5 s after their sink time; (B) the camera leaves
    ///    for 5 s and comes back: all six have sunk; (C) zoomed out to the widest view, 20 killed in view: at most the cap waits on camera, the
    ///    rest sank in view as before; the frame time with the cap's wrecks waiting against none (3 s each). Pictures of each phase.
    ///  * MLRS (items 151b / 151d): a red MLRS v1 and v2 side by side (unarmed), at the 70 m and the 35 m camera, the view turned every 45 deg:
    ///    each launcher's silhouette area, length along its hull, width across it and top over its deck on screen; v2's means within
    ///    LauncherWantLo..LauncherWantHi of v1's (the same size, not smaller and not giant); v2's launcher inside its hull's ends. A picture at each height.
    /// </summary>
    public partial class K93Proof : MonoBehaviour   // k102: + View/K93Proof.K102.cs (a vehicle factory for the exit check, the camera on the gate's open side)
    {
        public static string Report = "idle";
        public static readonly string[] FactoryTypes = { "jeep", "light", ZMTank.Type, "heavy", "apc", "missile_launcher", "medium" };
        public static readonly string[] FortTypes = { "crane", "jeep", "light", ZMTank.Type, "heavy", "apc", "missile_launcher", "medium" };   // k102: + the APC (the fort builds it too)
        public const float PassPokeM = 0.01f;

        string outDir, job; readonly StringBuilder rep = new StringBuilder();
        readonly StringBuilder csv = new StringBuilder("label,frame,t_s,scale,z_tail,z_head,side_poke_m,top_poke_m\n");
        int fails, passes; ZGame g; ZWorld W; RtsCamera cam;

        public static void Run(string outDir, string job)
        {
            if (Report == "running") { Debug.LogWarning("[OpenZ K93] a check is running already"); return; }
            Report = "running";
            var go = new GameObject("K93Proof"); var p = go.AddComponent<K93Proof>(); p.outDir = outDir; p.job = job;
        }

        void Start() { StartCoroutine(Main()); }
        void Line(string s) { rep.AppendLine(s); Debug.Log("[OpenZ K93] " + s); }

        IEnumerator Main()
        {
            g = ZGame.I;
            if (!g || g.World == null) { Report = "FAIL no level running"; Destroy(gameObject); yield break; }
            W = g.World; cam = FindFirstObjectByType<RtsCamera>();
            Directory.CreateDirectory(outDir);
            if (job == "exit") yield return Exits();
            else if (job == "wrecks") yield return Wrecks();
            else if (job == "mlrs") yield return Launchers();
            string verdict = fails == 0 && passes > 0 ? "PASS" : "FAIL";
            Line($"K93 {job.ToUpperInvariant()}: {verdict} ({passes} PASS, {fails} FAIL)");
            Line(ExitDoors.Report()); Line(ExitFitK102.Report()); Line(WreckSinkGate.Report()); Line(RecoilShow.Report());
            try { File.WriteAllText(Path.Combine(outDir, "k93_" + job + "_report.txt"), rep.ToString()); if (job == "exit") File.WriteAllText(Path.Combine(outDir, "k93_exit_frames.csv"), csv.ToString()); }
            catch (System.Exception e) { Debug.LogWarning("[OpenZ K93] write failed: " + e.Message); }
            Report = verdict;
            Destroy(gameObject);
        }

        void Focus(Vector3 p, float h) { if (!cam) return; cam.focus = new Vector3(p.x, 0f, p.z); cam.SetHeightImmediate(h); }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, name + ".png"));
            Line($"      picture {name}.png ({Screen.width}x{Screen.height}, camera {(cam ? cam.height : 0f):0} m)");
        }

        static ZObject Nearest(ZWorld W, ZObject from, System.Predicate<ZObject> f)
        {
            ZObject best = null; long bd = long.MaxValue;
            foreach (var o in W.Objects)
            {
                if (o == null || o.Destroyed || !f(o)) continue;
                long dx = o.CenterX - (from != null ? from.CenterX : 0), dy = o.CenterY - (from != null ? from.CenterY : 0), d = dx * dx + dy * dy;
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        // ------------------------------------------------------------------ exits
        IEnumerator Exits()
        {
            var me = g.player; int ti = (int)me; int tech0 = ti >= 0 && ti < W.TechBonus.Length ? W.TechBonus[ti] : 0;
            if (ti >= 0 && ti < W.TechBonus.Length) W.TechBonus[ti] = 5;
            var fort = Nearest(W, null, o => o.IsFort && o.Owner == me);
            var fac = Nearest(W, fort, o => o.Type == "vehicle_factory" && o.Owner == me);
            fac = K102Factory(me, fort, fac);                                                             // k102: none of the player's: one given to the player for the check (View/K93Proof.K102.cs)
            Line($"exit: player {me}; tech lifted for the check; vehicle factory {(fac != null ? "#" + fac.Id : "none of the player's")}, fort {(fort != null ? "#" + fort.Id + " " + fort.Type : "none")}; ExitDoors on {ExitDoors.Enabled}");
            var list = new List<(ZObject b, string type, string label)>();
            if (fac != null) foreach (var t in FactoryTypes) list.Add((fac, t, "factory_" + t));
            if (fort != null) foreach (var t in FortTypes) list.Add((fort, t, "fort_" + t));
            foreach (var (b, type, label) in list) { yield return Build(me, b, type, label); yield return new WaitForSeconds(0.5f); }
            if (ti >= 0 && ti < W.TechBonus.Length) W.TechBonus[ti] = tech0;
            K102Restore();                                                                                // k102: the staged zone back, the camera's heading released
        }

        IEnumerator Build(Team me, ZObject b, string type, string label)
        {
            var known = new HashSet<int>(); foreach (var o in W.Objects) if (o != null) known.Add(o.Id);
            if (b.Building) W.StopBuilding(me, b);
            if (!W.StartBuilding(me, b, type)) { Line($"told  {label}: the {b.Type} does not build a {type} here (refused by the rules)"); yield break; }
            b.BInit = W.Time - 20.0; b.BFinal = W.Time + 1.0;                                             // staged: done in 1 s
            var rally0 = new List<ZWaypoint>(b.RallyPoints);
            if (b.GetCreationPoint(out int ax, out int ay) && b.GetCreationMovePoint(out int bx, out int by))
            {
                float dx = bx - ax, dy = by - ay, l = Mathf.Sqrt(dx * dx + dy * dy);
                if (l > 0.1f) W.SetRallyPoints(me, b, new List<ZWaypoint> { new ZWaypoint { mode = WpMode.Move, refId = -1, x = bx + (int)(dx / l * 160f), y = by + (int)(dy / l * 160f), playerGiven = true } });
            }
            var fvs = FactoryViews.I; var fv = fvs ? fvs.For(b.Id) : null;
            K102Look(b, fv);                                                                              // k102: the camera on the gate's open side (View/K93Proof.K102.cs)
            ZObject u = null; float wait = 0f;
            while (u == null && wait < 15f)
            {
                foreach (var o in W.Objects) if (o != null && !known.Contains(o.Id) && o.Type == type && o.Kind == ZKind.Vehicle && o.Owner == me) { u = o; break; }
                wait += Time.deltaTime; yield return null;
            }
            W.StopBuilding(me, b); b.RallyPoints.Clear(); b.RallyPoints.AddRange(rally0);
            if (u == null) { Line($"FAIL  {label}: no {type} came out in 15 s"); fails++; yield break; }
            UnitView v = null; wait = 0f;
            while (!v && wait < 3f) { g.Views.TryGetValue(u.Id, out v); wait += Time.deltaTime; yield return null; }
            if (!v) { Line($"FAIL  {label}: {type} #{u.Id} has no view"); fails++; yield break; }
            yield return new WaitForEndOfFrame();
            Transform gate = v.ExitGate; var segs = v.ExitSegs; bool profiled = v.ExitProfiled;
            if (!profiled || !gate || segs == null)
            {
                Line($"told  {label}: {type} #{u.Id} not fitted by a gate profile (exit straight: {v.ExitScaleNow:0.000}); the width fit as before");
                yield break;
            }
            float t0 = Time.time, minS = 1f, worstSide = -9f, worstTop = -9f, fullAt = -1f; int frame = 0, inGate = 0; int shot = 0;
            float[] at = { 0.4f, 1.2f, 2.0f, 2.8f };
            var rs = new List<Renderer>();
            while (v && !u.Destroyed && Time.time - t0 < 16f)
            {
                yield return new WaitForEndOfFrame();
                if (!v) break;
                float t = Time.time - t0, s = v.ExitScaleNow; minS = Mathf.Min(minS, s);
                Poke(v, gate, segs, rs, out float side, out float top, out float zt, out float zh, out bool inside);
                if (inside) { inGate++; worstSide = Mathf.Max(worstSide, side); worstTop = Mathf.Max(worstTop, top); }
                csv.AppendLine($"{label},{frame},{t:0.000},{s:0.0000},{zt:0.000},{zh:0.000},{(inside ? side.ToString("0.000") : "")},{(inside ? top.ToString("0.000") : "")}");
                if (shot < at.Length && t >= at[shot] && (type == "heavy" || type == "missile_launcher" || type == "medium" || type == ZMTank.Type || type == "light")) { StartCoroutine(Shot($"k93_exit_{label}_{shot + 1}_t{at[shot]:0.0}s_s{s:0.00}")); shot++; }
                if (s >= 0.999f && fullAt < 0f) fullAt = t;
                if (fullAt >= 0f && !inside && t - fullAt > 0.5f && shot >= (type == "jeep" || type == "apc" || type == "crane" ? 0 : at.Length)) break;
                frame++;
            }
            bool ok = inGate > 0 && worstSide <= PassPokeM && worstTop <= PassPokeM;
            Line($"{(ok ? "PASS" : "FAIL")}  {label}: {type} #{u.Id} ({(OzV2Units.IsV2(v) || OzHeavyTank.IsV2(v.transform) ? "v2" : "v1")} model) out of the {b.Type} gate: start scale {minS:0.000}, full size after {(fullAt >= 0f ? fullAt.ToString("0.00") : ">16")} s; " +
                 $"{inGate} frames inside the gate: the most a drawn part reached past a side wall {worstSide:+0.000;-0.000} m, past a lintel {worstTop:+0.000;-0.000} m (pass: never more than {PassPokeM:0.00} m)");
            if (ok) passes++; else fails++;
        }

        /// <summary>The drawn parts of `v` this frame in the gate's frame: the most any mesh-box corner inside a segment's length reaches past its side
        /// walls / its lintel (m; negative = clear); the tail and head z; whether any part is inside the gate at all.</summary>
        static void Poke(UnitView v, Transform gate, ExitDoors.Seg[] segs, List<Renderer> rs, out float side, out float top, out float zTail, out float zHead, out bool inside)
        {
            side = top = -9f; zTail = 1e9f; zHead = -1e9f; inside = false;
            float k = Mathf.Max(1e-4f, Mathf.Abs(gate.lossyScale.z));
            v.GetComponentsInChildren(rs);
            foreach (var r in rs)
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                var mf = r is MeshRenderer ? r.GetComponent<MeshFilter>() : null;
                Bounds lb; Matrix4x4 m;
                if (mf && mf.sharedMesh) { lb = mf.sharedMesh.bounds; m = r.transform.localToWorldMatrix; } else { lb = r.bounds; m = Matrix4x4.identity; }
                for (int i = 0; i < 8; i++)
                {
                    var w = m.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) * 2 - 1, ((i >> 1) & 1) * 2 - 1, ((i >> 2) & 1) * 2 - 1)));
                    var p = gate.InverseTransformPoint(w);
                    zTail = Mathf.Min(zTail, p.z); zHead = Mathf.Max(zHead, p.z);
                    foreach (var sg in segs)
                    {
                        if (p.z < sg.z0 || p.z >= sg.z1) continue;
                        inside = true;
                        side = Mathf.Max(side, (Mathf.Abs(p.x) - sg.half) * k);
                        if (sg.top < ExitDoors.Open * 0.5f) top = Mathf.Max(top, (p.y - sg.top) * k);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ wrecks
        IEnumerator Wrecks()
        {
            int force0 = WreckFx.Force; WreckFx.Force = 2;                                                  // plain wrecks: no break-ups, the count stays exact
            var c0 = cam ? cam.focus : Vector3.zero; float h0 = cam ? cam.height : 70f;
            var centre = c0;                                                                                // where the player looks at the start
            Line($"wrecks: smoulder {WreckFx.SmoulderSeconds} s, then the sink ({WreckFx.FadeSeconds} s); WreckSinkGate on {WreckSinkGate.Enabled}, margin {WreckSinkGate.Margin}, cap {WreckSinkGate.CapWrecks} wrecks / {WreckSinkGate.CapRenderers} parts; staged: plain wrecks (no break-ups)");
            // A: six in view
            Focus(centre, 70f); yield return new WaitForSeconds(1f);
            var a = Spawn(centre, 6); yield return new WaitForSeconds(1.5f);
            Kill(a); var ys = new Dictionary<int, float>(); yield return new WaitForSeconds(2f);
            foreach (var o in a) { var vv = View(o); if (vv) ys[o.Id] = vv.transform.position.y; }
            yield return new WaitForSeconds(WreckFx.SmoulderSeconds + 3f);
            int up = Count(a, ys, true);
            yield return Shot("k93_wrecks_A_on_camera_after_sink_time");
            Verdict("A_on_camera", up == a.Count, $"{up} of {a.Count} wrecks still lying in view {WreckFx.SmoulderSeconds + 5f:0} s after their death (their sink time {WreckFx.SmoulderSeconds} s); waiting on camera {WreckSinkGate.OnCameraWrecks}");
            // B: the camera leaves and comes back
            Focus(centre + new Vector3(160f, 0f, 160f), 70f); yield return new WaitForSeconds(5f);
            Focus(centre, 70f); yield return new WaitForSeconds(1f);
            int gone = a.Count - Count(a, ys, true);
            yield return Shot("k93_wrecks_B_back_after_leaving");
            Verdict("B_off_camera", gone == a.Count, $"after 5 s off camera: {gone} of {a.Count} sank (off camera, out of sight)");
            // C: widest view, 20 at once
            float wide = cam ? cam.maxHeight : 165f;
            Focus(centre, wide); yield return new WaitForSeconds(1f);
            var cs = Spawn(centre, 20); yield return new WaitForSeconds(1.5f);
            Kill(cs); ys.Clear(); yield return new WaitForSeconds(2f);
            foreach (var o in cs) { var vv = View(o); if (vv) ys[o.Id] = vv.transform.position.y; }
            int cap0 = WreckSinkGate.ReleasedCap;
            yield return new WaitForSeconds(WreckFx.SmoulderSeconds + 3f);
            int left = Count(cs, ys, true);
            float msHeld = 0f; yield return FrameMs(3f, x => msHeld = x);
            yield return Shot("k93_wrecks_C_widest_capped");
            Verdict("C_cap", left <= WreckSinkGate.CapWrecks && left >= Mathf.Min(cs.Count, WreckSinkGate.CapWrecks) - 1 && WreckSinkGate.ReleasedCap > cap0,
                    $"widest view ({wide:0} m): {left} of {cs.Count} still lying (cap {WreckSinkGate.CapWrecks} wrecks / {WreckSinkGate.CapRenderers} parts; on camera now {WreckSinkGate.OnCameraWrecks} wrecks, {WreckSinkGate.OnCameraParts} parts); sank by the cap {WreckSinkGate.ReleasedCap - cap0}");
            Focus(centre + new Vector3(220f, 0f, 220f), wide); yield return new WaitForSeconds(5f);
            Focus(centre, wide); yield return new WaitForSeconds(1f);
            float msNone = 0f; yield return FrameMs(3f, x => msNone = x);
            Line($"told  frame time at the widest view: {msHeld:0.00} ms with the cap's wrecks waiting, {msNone:0.00} ms after they sank (+{msHeld - msNone:0.00} ms)");
            WreckFx.Force = force0; if (cam) { cam.focus = c0; cam.SetHeightImmediate(h0); }
        }

        // ------------------------------------------------------------------ 151b / 151d: the MLRS launchers side by side
        /// <summary>Kit k104 (item 151d, "same size as from v1"): the expected ratios of v2's launcher to v1's from the RTS camera, the mean over
        /// the view turned every 45 deg (off the engine on the models, both at v1's rest tilt, item 184: area 0.99, length 0.96, width 1.02), and the band each mean must fall in.</summary>
        public const float LauncherWantLo = 0.93f, LauncherWantHi = 1.07f, LauncherLenOneMax = 1.25f;

        IEnumerator Launchers()
        {
            var c0 = cam ? cam.focus : Vector3.zero; float h0 = cam ? cam.height : 70f;
            var px = ZGame.WorldToPx(c0); int cx = px.x / 16, cy = px.y / 16;
            var pair = new ZObject[2];
            bool scan0 = UnitAim.IdleScan; UnitAim.IdleScan = false;   // while measured the two turrets face their hull's front, as on the models (an idle turret steps round 45 deg a second); put back below
            for (int k = 0; k < 2; k++)
            {
                if (!OzCheatSpawner.FreeTile(W, cx - 3 + 6 * k, cy, 3, false, out int fx, out int fy)) continue;
                var o = W.CreateObject("vehicle", OzMlrs.Type, fx * 16, fy * 16, Team.Red);
                OzMlrs.Models.Pin(o, k + 1); g.SpawnUnitView(o); o.Damage = 0; o.AttackRadius = 0; pair[k] = o;
            }
            Line($"mlrs: staged a red MLRS v1 (left) and v2 (right), unarmed, side by side; v2's modelled turret x{OzMlrsLauncher.ScaleV2} (one uniform scale, nothing stretched), +{OzMlrsLauncher.LiftV2M} m");
            yield return new WaitForSeconds(0.5f);
            if (pair[0] == null || pair[1] == null || !View(pair[0]) || !View(pair[1])) { Verdict("launcher_size", false, "no free tiles or no views for the pair"); UnitAim.IdleScan = scan0; yield break; }
            var mid = (View(pair[0]).transform.position + View(pair[1]).transform.position) * 0.5f;
            foreach (var h in new[] { 70f, 35f })
            {
                Focus(mid, h); yield return new WaitForSeconds(2f);
                yield return Shot($"k93_mlrs_v1_left_v2_right_{h:0}m");
                float sa = 0f, sl = 0f, sw = 0f, sh = 0f, lMax = 0f, lMin = 9f; int n = 0; var each = new StringBuilder();
                bool unc0 = cam && cam.unclamped;
                if (cam) { cam.yawHeld = true; cam.unclamped = true; }   // each launcher measured at the screen's centre, seen from the same side of its own hull (the pair stands 12 m or more apart: from one view their perspective alone differs 5-25 %)
                for (int yd = 0; yd < 360; yd += 45)
                {
                    var ms = new Vector3[2]; var ar = new float[2];
                    for (int u = 0; u < 2; u++)
                    {
                        var vu = View(pair[u]); if (!vu) break;
                        Focus(vu.transform.position, h); if (cam) { cam.yaw = vu.transform.eulerAngles.y + yd; cam.Place(); }
                        yield return null; yield return null;
                        ms[u] = LauncherOnScreen(vu); ar[u] = LauncherAreaOnScreen(vu);
                    }
                    var a = ms[0]; var b = ms[1]; float aa = ar[0], ba = ar[1];
                    if (a.x <= 0f || a.y <= 0f || a.z <= 0f || aa <= 0f) continue;
                    float rl = b.x / a.x, rw = b.y / a.y, rh = b.z / a.z, ra = ba / aa;
                    sa += ra; sl += rl; sw += rw; sh += rh; n++; lMax = Mathf.Max(lMax, rl); lMin = Mathf.Min(lMin, rl);
                    each.Append($" {yd}: {ra:0.00}/{rl:0.00}/{rw:0.00}");
                }
                if (cam) { cam.yaw = 0f; cam.yawHeld = false; cam.unclamped = unc0; }
                yield return null;
                if (n == 0) { Verdict("launcher_size_" + h.ToString("0") + "m", false, "no launcher measured on screen"); continue; }
                float ma = sa / n, ml = sl / n, mw = sw / n, mh = sh / n;
                Line($"      {h:0} m camera, v2 / v1 per view heading (area / length / width):{each}");
                Verdict("launcher_size_" + h.ToString("0") + "m", In(ma) && In(ml) && In(mw) && lMax <= LauncherLenOneMax,
                        $"on screen ({h:0} m camera, the view turned every 45 deg about each launcher's own hull, each at the screen's centre, {n} headings): v2's launcher / v1's: area {ma:0.00}, length along its hull {ml:0.00} ({lMin:0.00}-{lMax:0.00}), width {mw:0.00}, its top over its deck {mh:0.00} (want area, length and width each {LauncherWantLo:0.00}-{LauncherWantHi:0.00}: the same size as v1's, not smaller and not giant; one heading's length at most {LauncherLenOneMax:0.00}; off the engine 0.99 / 0.96 (0.83-1.03) / 1.02)");
                if (h > 50f)
                {
                    float over = LauncherPastHull(View(pair[1]), out string overNote);
                    Verdict("launcher_inside_hull", over <= 0f, $"v2 at its rest aim: {overNote} (want the launcher inside the hull's front and back)");
                }
            }
            if (cam) { cam.focus = c0; cam.SetHeightImmediate(h0); }
            UnitAim.IdleScan = scan0;
        }

        static bool In(float r) => r >= LauncherWantLo && r <= LauncherWantHi;

        /// <summary>A launcher's silhouette area on screen (px2): the convex outline of every vertex under MLRSRackPitch (mesh boxes for an
        /// unreadable mesh), projected by the game camera.</summary>
        static float LauncherAreaOnScreen(UnitView v)
        {
            var c = Camera.main; if (!v || !c) return 0f;
            var pts = LauncherPoints(v); if (pts.Count < 3) return 0f;
            var s = new List<Vector2>(pts.Count);
            foreach (var p in pts) s.Add(c.WorldToScreenPoint(p));
            s.Sort((p, q) => p.x != q.x ? p.x.CompareTo(q.x) : p.y.CompareTo(q.y));
            var hull = new List<Vector2>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                for (int i = 0; i < s.Count; i++)
                {
                    var p = pass == 0 ? s[i] : s[s.Count - 1 - i];
                    while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f) hull.RemoveAt(hull.Count - 1);
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count - 1);
            }
            float area = 0f;
            for (int i = 0; i < hull.Count; i++) { var p = hull[i]; var q = hull[(i + 1) % hull.Count]; area += p.x * q.y - q.x * p.y; }
            return Mathf.Abs(area) * 0.5f;
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        static List<Vector3> LauncherPoints(UnitView v)
        {
            var pts = new List<Vector3>(); Transform pitch = null;
            foreach (var t in v.GetComponentsInChildren<Transform>(true)) if (t.name == "MLRSRackPitch") { pitch = t; break; }
            if (!pitch) return pts;
            foreach (var mf in pitch.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh; var rr = mf.GetComponent<Renderer>(); if (!m || !rr || !rr.enabled) continue;
                if (m.isReadable) foreach (var p in m.vertices) pts.Add(mf.transform.TransformPoint(p));
                else { var b = m.bounds; for (int i = 0; i < 8; i++) pts.Add(mf.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1)))); }
            }
            return pts;
        }

        /// <summary>How far (m, along the unit's own axis) the launcher reaches past its hull's front or back with its turret turned back to its
        /// rest heading (the launcher's aim as it stands now); negative = inside.</summary>
        static float LauncherPastHull(UnitView v, out string note)
        {
            note = "no launcher / hull"; if (!v) return 9f;
            var pts = LauncherPoints(v); MeshFilter hull = null; Transform yaw = null;
            foreach (var mf in v.GetComponentsInChildren<MeshFilter>()) if (mf.name == "MLRS_hull") { hull = mf; break; }
            foreach (var t in v.GetComponentsInChildren<Transform>(true)) if (t.name == "MLRSRackYaw") { yaw = t; break; }
            if (pts.Count == 0 || !hull || !hull.sharedMesh || !yaw || !yaw.parent) return 9f;
            float l0 = float.MaxValue, l1 = float.MinValue, h0 = float.MaxValue, h1 = float.MinValue;
            foreach (var p in pts)
            {
                var rest = yaw.parent.TransformPoint(yaw.localPosition + Vector3.Scale(yaw.localScale, yaw.InverseTransformPoint(p)));   // the turret's rest heading (its modelled rotation is none)
                float z = v.transform.InverseTransformPoint(rest).z; l0 = Mathf.Min(l0, z); l1 = Mathf.Max(l1, z);
            }
            var hb = hull.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var w = hull.transform.TransformPoint(hb.center + Vector3.Scale(hb.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1)));
                float z = v.transform.InverseTransformPoint(w).z; h0 = Mathf.Min(h0, z); h1 = Mathf.Max(h1, z);
            }
            float over = Mathf.Max(h0 - l0, l1 - h1);
            note = $"launcher {l0:0.00}..{l1:0.00} m along the unit, hull {h0:0.00}..{h1:0.00} m: {(over <= 0f ? $"inside by {-over:0.00} m" : $"past the hull by {over:0.00} m")}";
            return over;
        }

        /// <summary>A launcher (every mesh under MLRSRackPitch, vertices or their mesh boxes) on screen, px: (its extent along the hull's drawn axis,
        /// across it, the height of its top over the hull deck).</summary>
        static Vector3 LauncherOnScreen(UnitView v)
        {
            var c = Camera.main; if (!v || !c) return Vector3.zero;
            Transform pitch = null; float deck = float.MinValue;
            foreach (var t in v.GetComponentsInChildren<Transform>(true)) if (t.name == "MLRSRackPitch") { pitch = t; break; }
            foreach (var r in v.GetComponentsInChildren<Renderer>()) if (r.name == "MLRS_hull") deck = Mathf.Max(deck, r.bounds.max.y);
            if (!pitch) return Vector3.zero;
            var pts = new List<Vector3>();
            foreach (var mf in pitch.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh; var rr = mf.GetComponent<Renderer>(); if (!m || !rr || !rr.enabled) continue;
                if (m.isReadable) foreach (var p in m.vertices) pts.Add(mf.transform.TransformPoint(p));
                else { var b = m.bounds; for (int i = 0; i < 8; i++) pts.Add(mf.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1)))); }
            }
            if (pts.Count == 0) return Vector3.zero;
            var o = (Vector2)c.WorldToScreenPoint(v.transform.position); var e = ((Vector2)c.WorldToScreenPoint(v.transform.position + v.transform.forward) - o).normalized;
            var f = new Vector2(-e.y, e.x);
            float l0 = float.MaxValue, l1 = float.MinValue, w0 = float.MaxValue, w1 = float.MinValue; Vector3 top = pts[0];
            foreach (var p in pts)
            {
                var s = (Vector2)c.WorldToScreenPoint(p) - o;
                l0 = Mathf.Min(l0, Vector2.Dot(s, e)); l1 = Mathf.Max(l1, Vector2.Dot(s, e)); w0 = Mathf.Min(w0, Vector2.Dot(s, f)); w1 = Mathf.Max(w1, Vector2.Dot(s, f));
                if (p.y > top.y) top = p;
            }
            float hpx = deck > float.MinValue ? Vector2.Distance(c.WorldToScreenPoint(top), c.WorldToScreenPoint(new Vector3(top.x, deck, top.z))) : 0f;
            return new Vector3(l1 - l0, w1 - w0, hpx);
        }

        void Verdict(string tag, bool ok, string what) { Line($"{(ok ? "PASS" : "FAIL")}  {tag}: {what}"); if (ok) passes++; else fails++; }

        UnitView View(ZObject o) => o != null && g.Views.TryGetValue(o.Id, out var v) ? v : null;

        List<ZObject> Spawn(Vector3 at, int n)
        {
            var list = new List<ZObject>();
            var px = ZGame.WorldToPx(at); int cx = px.x / 16, cy = px.y / 16;
            for (int i = 0; i < n; i++)
            {
                int gx = cx - 6 + (i % 5) * 3, gy = cy - 4 + (i / 5) * 3;
                if (!OzCheatSpawner.FreeTile(W, gx, gy, 2, false, out int fx, out int fy)) continue;
                var o = W.CreateObject("vehicle", OzLightTank.Type, fx * 16, fy * 16, Team.Red);
                OzLightTank.Models.Pin(o, 2); g.SpawnUnitView(o); o.Damage = 0; o.AttackRadius = 0; list.Add(o);
            }
            Line($"      staged: {list.Count} red light tanks v2 (unarmed) round tile ({cx},{cy})");
            return list;
        }

        void Kill(List<ZObject> list) { foreach (var o in list) if (!o.Destroyed) { o.SetHealth(0); W.UpdateObjectHealth(o); } Line($"      killed {list.Count} at {Time.time:0.0} s"); }

        /// <summary>How many of them still lie where they died (their view drawn, within 0.15 m of the height they lay at).</summary>
        int Count(List<ZObject> list, Dictionary<int, float> ys, bool lying)
        {
            int n = 0;
            foreach (var o in list)
            {
                var v = View(o);
                bool still = v && v.gameObject.activeInHierarchy && ys.TryGetValue(o.Id, out float y) && v.transform.position.y > y - 0.15f;
                if (still == lying) n++;
            }
            return n;
        }

        static IEnumerator FrameMs(float secs, System.Action<float> done)
        {
            float t0 = Time.realtimeSinceStartup; int n = 0;
            while (Time.realtimeSinceStartup - t0 < secs) { n++; yield return null; }
            done(n > 0 ? (Time.realtimeSinceStartup - t0) * 1000f / n : 0f);
        }
    }
}
#endif
