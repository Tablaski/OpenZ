#if !OPENZ_HARNESS
// OpenZ (GPL-3.0) - version 1.3: level 7's border volcanoes, pillars and cones may stop drawing only outside the picture. Editor only: nothing here ships, nothing is saved but the report.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Counted Play check. Level 7 in Play, after the start flyover, Game view in front. For HIGH, MEDIUM and LOW in turn:
    ///  1 the game's own path (the GPU drawer): frame times at heights 35, 70, 200 and in the free camera's widest view, two pictures, the colours under every piece at 40 views;
    ///  2 the engine's own culling (OzGrd.KeepCpuCulling, this Play only): Renderer.isVisible is filled only there, so every piece in the view is
    ///    read every frame while the camera goes 35 to 200 and back over the north, south, east and west borders, and through both start flyover lists.
    ///    A piece in the view that is not drawn, or whose drawn mesh changes, is counted: the sum must be 0.
    ///    Two controls keep the count honest: one piece gets a real cull LODGroup for a moment and must read as gone, and pieces off the view must read as not drawn.
    ///  3 the same 40 views again: a piece that shows on the engine's path and not on the drawer's differs in colour, so the drawer's path is counted too.
    /// With today's prefabs it fails with the counts of the fault; "before" or "after" in the file names says which form ran. It puts preset, pause, clock and camera back.
    /// </summary>
    public static class OzL07RimPopCheck
    {
        const string Tag = "[OpenZ L07 rim]";
        const float Inset = 0.25f;                                              // m a piece must reach into the picture before it counts: the edge of the view is the engine's to decide
        const int ShotSamples = 31, SettleFrames = 30, MeasureFrames = 120, Samples = 4, SumDiff = 70;
        public static string Report = "not run";
        static string Dir => Path.Combine(OpenZPaths.RepoRoot, "work", "staging", "v1_3", "open_fixes_31oct", "l07_volcano_pop", "proof");
        static readonly string[] Presets = { "HIGH", "MEDIUM", "LOW" }, Border = { "north", "south", "east", "west" };
        static readonly float[] Coarse = { 35f, 50f, 70f, 90f, 110f, 130f, 150f, 170f, 185f, 200f };
        static readonly Vector3[] Timed = { new Vector3(35f, 10.5f, -10f), new Vector3(70f, 21f, -28.6f), new Vector3(200f, 78f, -88f) };   // height, focus x, focus z: the view with the most rim triangles at that zoom
        static readonly Vector3 FreeTimed = new Vector3(200f, 60f, -10f);       // the same for the free camera, turned FreeYaw at tilt FreeTilt (on by default in the options; no fixed view draws as much of the rim)
        const float FreeYaw = 165f, FreeTilt = 25f;

        struct Shot
        {
            public string name; public Vector3 a, la, b, lb; public float fa, fb;
            public Shot(string n, Vector3 pa, Vector3 lookA, float fovA, Vector3 pb, Vector3 lookB, float fovB) { name = n; a = pa; la = lookA; fa = fovA; b = pb; lb = lookB; fb = fovB; }
        }

        // both start flyover lists, start and end pose of every shot (ZodFlyover.L07.cs through the pose maths of ZodFlyover.Cine.cs and .Blue.cs). The home shots end on the start camera: the scene's focus, the same offset from the blue fort on that side, north-up on both
        static readonly Shot[] Shots =
        {
            new Shot("L07 dawn_lava", new Vector3(6.00f, 34.00f, -70.04f), new Vector3(192.58f, 6.17f, -136.47f), 40.00f, new Vector3(13.58f, 38.00f, -74.56f), new Vector3(200.80f, 6.71f, -137.57f), 40.00f),
            new Shot("L07 lake_middle", new Vector3(55.20f, 70.60f, -174.90f), new Vector3(70.00f, -0.30f, -112.00f), 38.00f, new Vector3(62.90f, 66.90f, -166.90f), new Vector3(74.00f, -0.30f, -104.00f), 38.00f),   // row 62
            new Shot("L07 lake_north", new Vector3(116.90f, 68.80f, -154.70f), new Vector3(102.00f, -0.30f, -92.00f), 40.00f, new Vector3(114.50f, 62.20f, -143.90f), new Vector3(101.00f, -0.30f, -84.00f), 40.00f),   // row 62
            new Shot("L07 enemy_fort", new Vector3(166.78f, 74.48f, 2.90f), new Vector3(91.40f, 4.00f, -40.62f), 28.00f, new Vector3(165.10f, 63.25f, -15.48f), new Vector3(87.54f, 4.00f, -40.68f), 28.00f),   // row 62
            new Shot("L07 our_base_home", new Vector3(76.59f, 58.41f, -267.53f), new Vector3(84.00f, 3.00f, -197.00f), 30.00f, new Vector3(84.00f, 70.00f, -239.61f), new Vector3(84.00f, 0.00f, -190.60f), 32.00f),   // row 62
            new Shot("B07 blue_base_dawn", new Vector3(32.34f, 24.00f, -18.02f), new Vector3(219.10f, -20.99f, -73.70f), 48.00f, new Vector3(39.30f, 21.00f, -18.83f), new Vector3(225.27f, -27.38f, -74.27f), 48.00f),
            new Shot("B07 the_world", new Vector3(84.00f, 92.00f, -70.00f), new Vector3(84.00f, 0.00f, -124.00f), 34.00f, new Vector3(84.00f, 86.00f, -86.00f), new Vector3(84.00f, 0.00f, -136.00f), 34.00f),
            new Shot("B07 red_fort", new Vector3(165.83f, 80.79f, -223.59f), new Vector3(92.86f, 6.00f, -177.99f), 28.00f, new Vector3(164.41f, 69.17f, -205.45f), new Vector3(88.43f, 6.00f, -177.80f), 28.00f),   // row 68
            new Shot("B07 blue_home", new Vector3(84.00f, 53.02f, -75.79f), new Vector3(84.00f, 3.00f, -24.00f), 30.00f, new Vector3(84.00f, 70.00f, -66.61f), new Vector3(84.00f, 0.00f, -17.60f), 32.00f),
        };

        sealed class Runner : MonoBehaviour { }
        static Runner runner;

        sealed class Piece
        {
            public Transform root; public string row; public LODGroup lod; public MeshRenderer[] rends; public MeshFilter[] filters; public Mesh[] meshes;
            public Bounds box; public Vector3[] pts; public bool ownBlock; public int last = -1;
        }

        sealed class Tally { public int gone, swaps; public float firstGone = -1f, firstSwap = -1f; }

        static readonly List<Piece> pieces = new List<Piece>();
        static readonly List<Renderer> taken = new List<Renderer>();
        static readonly Plane[] planes = new Plane[6];
        static readonly Dictionary<string, Tally> rows = new Dictionary<string, Tally>();
        static readonly StringBuilder text = new StringBuilder(), csv = new StringBuilder();
        static int ok, bad, lodGroups, renderers, level0; static long tris; static string form = "after";
        static int steps, sightings, notDrawn, pops, swaps, meshChanges, offHidden;
        static bool paused0, kept, ran0, hadDn; static float scale0, fov0, clock0, height0; static Vector3 focus0; static RtsCamera rts0;

        static void Check(bool pass, string what) { if (pass) ok++; else bad++; text.Append(pass ? "\n  [ok]   " : "\n  [FAIL] ").Append(what); }
        static void Info(string what) => text.Append("\n  [info] ").Append(what);
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        [MenuItem("OpenZ/V1.3/L07 Rim: pop check (Play, level 7, Game view in front, about 4 minutes)")]
        public static void Menu()
        {
            var rts = EditorApplication.isPlaying ? Object.FindFirstObjectByType<RtsCamera>() : null;
            if (!rts || !ZGame.I || ZGame.I.World == null || !GameObject.Find("L07_Rim")) { Debug.LogError(Tag + " start level 7 in Play first"); return; }
            if (!rts.isActiveAndEnabled) { Debug.LogError(Tag + " the start flyover holds the camera: run it after the flyover"); return; }
            if (runner) { Debug.LogWarning(Tag + " a run is still going"); return; }
            var gv = Type.GetType("UnityEditor.GameView,UnityEditor"); if (gv != null) EditorWindow.FocusWindowIfItsOpen(gv);
            runner = new GameObject("OzL07RimPopCheckRunner") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Runner>();
            EditorApplication.playModeStateChanged -= Left; EditorApplication.playModeStateChanged += Left;
            Report = "running"; runner.StartCoroutine(Run(rts));
        }

        /// <summary>Play stopped half way: only the saved preset is left to put back.</summary>
        static void Left(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.ExitingPlayMode) return;
            EditorApplication.playModeStateChanged -= Left;
            if (kept) { PlayerPrefs.SetInt(OzQuality.Pref, level0); PlayerPrefs.Save(); kept = false; }
        }

        static IEnumerator Run(RtsCamera rts)
        {
            var cam = rts.GetComponent<Camera>();
            ok = bad = 0; text.Length = 0; csv.Length = 0; Build(); Keep(rts);
            csv.Append("preset,height,tilt,yaw,focus_x,focus_z,frames,frame_ms,main_ms,render_ms,gpu_ms,present_wait_ms,batches,setpass,triangles,shadow_casters\n");
            Info($"form {form}: {pieces.Count} pieces, {renderers} renderers, {tris} triangles if every one is drawn, {lodGroups} LODGroups; game held, clock stopped");
            Info("report, frame times and pictures: " + Dir);
            Check(lodGroups == 0, $"no LODGroup under L07_Rim ({lodGroups})");
            try { for (int p = 0; p < Presets.Length; p++) yield return OnePreset(p, rts, cam); }
            finally { PutBack(); }
            Finish();
        }

        static void Build()
        {
            pieces.Clear(); lodGroups = renderers = 0; tris = 0;
            var rim = GameObject.Find("L07_Rim");
            foreach (Transform group in rim.transform)
            {
                lodGroups += group.GetComponentsInChildren<LODGroup>(true).Length;
                if (group.name == "Dress") continue;                            // the border lava and bank batches: no LODGroup, counted above
                foreach (Transform t in group)
                {
                    var rs = new List<MeshRenderer>();
                    foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true)) if (!r.name.StartsWith("VOL_Flow", StringComparison.Ordinal)) rs.Add(r);   // the eruption overlays are off until a cone erupts
                    if (rs.Count == 0) continue;
                    var q = new Piece { root = t, row = group.name, lod = t.GetComponent<LODGroup>(), rends = rs.ToArray(), filters = new MeshFilter[rs.Count], meshes = new Mesh[rs.Count], box = rs[0].bounds };
                    for (int i = 0; i < rs.Count; i++)
                    {
                        q.filters[i] = rs[i].GetComponent<MeshFilter>(); q.meshes[i] = q.filters[i] ? q.filters[i].sharedMesh : null; q.box.Encapsulate(rs[i].bounds); renderers++;
                        q.ownBlock |= rs[i].HasPropertyBlock();                 // the eruptors: on the engine's path by design
                        if (q.meshes[i]) for (int s = 0; s < q.meshes[i].subMeshCount; s++) tris += q.meshes[i].GetIndexCount(s) / 3;
                    }
                    q.pts = new Vector3[Samples];
                    for (int k = 0; k < Samples; k++) q.pts[k] = new Vector3(q.box.center.x, Mathf.Lerp(q.box.min.y, q.box.max.y, (k + 1f) / (Samples + 1f)), q.box.center.z);
                    pieces.Add(q);
                }
            }
            form = lodGroups > 0 ? "before" : "after";
        }

        static void Keep(RtsCamera rts)
        {
            var g = ZGame.I; var dn = Object.FindFirstObjectByType<DayNight>();
            level0 = OzQuality.Level; paused0 = g.paused; scale0 = Time.timeScale; hadDn = dn; ran0 = dn && dn.running; clock0 = dn ? dn.timeOfDay : 0f;
            rts0 = rts; focus0 = rts.focus; height0 = rts.height; fov0 = rts.GetComponent<Camera>().fieldOfView; kept = true;
            g.paused = true; Time.timeScale = 0f; if (dn) dn.running = false;
        }

        static void PutBack()
        {
            EnginePath(false);
            if (kept && EditorApplication.isPlaying)
            {
                var g = ZGame.I; if (g) g.paused = paused0;
                Time.timeScale = scale0;
                var dn = Object.FindFirstObjectByType<DayNight>(); if (dn && hadDn) { dn.running = ran0; dn.timeOfDay = clock0; dn.Apply(clock0); }
                if (rts0) { rts0.enabled = true; rts0.GetComponent<Camera>().fieldOfView = fov0; rts0.focus = focus0; rts0.SetHeightImmediate(height0); }
                if (OzQuality.Level != level0) OzQuality.Level = level0;
                kept = false;
            }
        }

        static void Finish()
        {
            Report = $"{Tag} {form}: {(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad}{text}";
            if (bad == 0) Debug.Log(Report); else Debug.LogError(Report);
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(Path.Combine(Dir, $"rim_pop_{form}.txt"), Report + "\n");
                File.WriteAllText(Path.Combine(Dir, $"rim_frames_{form}.csv"), csv.ToString());
            }
            catch (Exception e) { Debug.LogWarning(Tag + " could not write the report: " + e.Message); }
            if (runner) Object.Destroy(runner.gameObject);
            runner = null;
        }

        // ------------------------------------------------------------------ the camera
        static void Aim(RtsCamera rts, int side, float along, float h)
        {
            Vector2 m = rts.mapSizeMeters; float x = along * m.x, z = -along * m.y, y = rts.focus.y;
            rts.follow = null;
            rts.focus = side == 0 ? new Vector3(x, y, 0f) : side == 1 ? new Vector3(x, y, -m.y) : side == 2 ? new Vector3(m.x, y, z) : new Vector3(0f, y, z);   // the camera's own clamp brings it to the border at this zoom
            rts.SetHeightImmediate(h);
        }

        static void Frustum(Camera cam)
        {
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            for (int i = 0; i < 4; i++) planes[i].distance -= Inset;
        }

        static bool Drawn(Piece q) { for (int i = 0; i < q.rends.Length; i++) if (q.rends[i].isVisible) return true; return false; }

        // ------------------------------------------------------------------ one preset
        static IEnumerator OnePreset(int p, RtsCamera rts, Camera cam)
        {
            var eof = new WaitForEndOfFrame();
            if (OzQuality.Level != p) OzQuality.Level = p;
            yield return Frames(12);
            Settings(p, cam);
            foreach (var t in Timed) yield return Times(p, rts, t);
            rts.FreeTurnBy(new Vector2(FreeYaw, rts.Tilt - FreeTilt) / RtsCamera.FreeLookSpeed);   // the row's tilt reads 55 when the FREE CAMERA option is off: the turn is then undone by the camera
            yield return Times(p, rts, FreeTimed);
            rts.FreeLookOff();
            yield return Picture(rts, $"rim_{form}_{Presets[p]}_h110_north", 0, 0.5f, 110f);
            yield return Picture(rts, $"rim_{form}_{Presets[p]}_h200_south", 1, 0.5f, 200f);
            var onDrawer = new List<byte[]>(); int seen = 0, engineDrew = 0;
            for (int s = 0; s < Border.Length; s++)
                foreach (float h in Coarse)
                {
                    Aim(rts, s, 0.5f, h); yield return Frames(3); yield return eof;
                    onDrawer.Add(Grab(cam));
                    foreach (var q in pieces) if (!q.ownBlock && GeometryUtility.TestPlanesAABB(planes, q.box)) { seen++; if (Drawn(q)) engineDrew++; }
                }
            Info($"{Presets[p]}: of {seen} sightings on the game's own path the engine's culling drew {engineDrew}: the rest are the GPU drawer's (the eruptors, on the engine's path by design, are left out; after the first preset old values can linger)");

            EnginePath(true); yield return Frames(4);
            yield return Control(p, rts, cam);
            steps = sightings = notDrawn = pops = swaps = meshChanges = offHidden = 0; rows.Clear();
            foreach (var q in pieces) q.last = -1;
            int n = 0;
            for (int s = 0; s < Border.Length; s++)
                for (int way = 0; way < 2; way++)
                    for (int i = 0; i <= 66; i++)
                    {
                        float h = 35f + 2.5f * (way == 0 ? i : 66 - i);
                        Aim(rts, s, Mathf.PingPong(n++ * 0.07f, 1f), h); yield return null; yield return eof;
                        Read(cam, h);
                    }
            int sweepSteps = steps, sweepSeen = sightings;
            yield return Flyover(rts, cam);
            var names = new List<string>(rows.Keys); names.Sort(StringComparer.Ordinal);
            foreach (var row in names) Info($"{Presets[p]} row {row}: not drawn {rows[row].gone} frames, first at height {rows[row].firstGone:0}; swaps {rows[row].swaps}, first at height {rows[row].firstSwap:0}");
            Check(steps > 800 && sweepSeen > 40000 && sightings > sweepSeen, $"{Presets[p]}: {sweepSteps} sweep frames + {steps - sweepSteps} flyover frames read, {sightings} sightings of a piece in the view");
            Check(notDrawn == 0, $"{Presets[p]}: not drawn while in the view: {notDrawn} ({pops} pop or return events)");
            Check(swaps == 0 && meshChanges == 0, $"{Presets[p]}: mesh swaps while in the view: {swaps}; mesh references changed: {meshChanges}");
            Check(offHidden > 0, $"{Presets[p]}: control, pieces off the view read as not drawn {offHidden} times (0 = isVisible says nothing, a Scene tab in front or a second camera)");

            int compared = 0, differ = 0, at = 0; var odd = new List<string>();
            for (int s = 0; s < Border.Length; s++)
                foreach (float h in Coarse)
                {
                    Aim(rts, s, 0.5f, h); yield return Frames(3); yield return eof;
                    Compare(onDrawer[at++], Grab(cam), Border[s], h, ref compared, ref differ, odd);
                }
            Check(compared > 500 && differ == 0, $"{Presets[p]}: game's path against the engine's at {at} views: {compared} pieces compared, {differ} differ{(odd.Count > 0 ? ": " + string.Join(", ", odd.GetRange(0, Mathf.Min(12, odd.Count))) : "")}");
            EnginePath(false); yield return Frames(4);
        }

        static void Settings(int p, Camera cam)
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            bool cullDist = false; foreach (float d in cam.layerCullDistances) cullDist |= d > 0f;
            int others = 0; foreach (var c in Camera.allCameras) if (c != cam && (c.cullingMask & 1) != 0) others++;
            Info($"{Presets[p]}: {(urp ? urp.name : "?")}, lod bias {QualitySettings.lodBias:0.##}, max LOD level {QualitySettings.maximumLODLevel}, far clip {cam.farClipPlane:0}, occlusion culling {cam.useOcclusionCulling}, drawer {(urp ? urp.gpuResidentDrawerMode.ToString() : "?")}");
            Check(urp && urp.smallMeshScreenPercentage == 0f, $"{Presets[p]}: the drawer's small mesh cull is off ({(urp ? urp.smallMeshScreenPercentage : -1f):0.##} %)");
            Check(QualitySettings.maximumLODLevel == 0 && !cullDist, $"{Presets[p]}: no layer cull distance, no LOD level cap");
            Check(others == 0, $"{Presets[p]}: no second camera draws layer 0 ({others}): it would answer for isVisible");
        }

        static IEnumerator Times(int p, RtsCamera rts, Vector3 t)
        {
            rts.follow = null; rts.focus = new Vector3(t.y, rts.focus.y, t.z); rts.SetHeightImmediate(t.x);
            yield return Frames(SettleFrames);
            var rb = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count"); var rs = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            var rt = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count"); var rc = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
            double frame = 0, main = 0, render = 0, gpu = 0, wait = 0, batches = 0, setpass = 0, triangles = 0, casters = 0; int n = 0, nt = 0, nr = 0;
            while (n < MeasureFrames)
            {
                yield return null;
                frame += Time.unscaledDeltaTime * 1000.0; n++;
                OzFrameTimes.Sample();
                if (OzFrameTimes.Have) { main += OzFrameTimes.Main; render += OzFrameTimes.Render; gpu += OzFrameTimes.Gpu; wait += OzFrameTimes.PresentWait; nt++; }
                if (rb.Valid) { batches += rb.LastValue; setpass += rs.Valid ? rs.LastValue : 0; triangles += rt.Valid ? rt.LastValue : 0; casters += rc.Valid ? rc.LastValue : 0; nr++; }
            }
            rb.Dispose(); rs.Dispose(); rt.Dispose(); rc.Dispose();
            double Tn(double v) => nt > 0 ? v / nt : 0.0;
            double Rn(double v) => nr > 0 ? v / nr : 0.0;
            var ci = CultureInfo.InvariantCulture;
            csv.Append(string.Join(",", new[] { Presets[p], t.x.ToString("0", ci), rts.Tilt.ToString("0", ci), rts.yaw.ToString("0", ci), t.y.ToString("0.0", ci), t.z.ToString("0.0", ci), n.ToString(ci), (frame / n).ToString("0.00", ci), Tn(main).ToString("0.00", ci), Tn(render).ToString("0.00", ci),
                Tn(gpu).ToString("0.00", ci), Tn(wait).ToString("0.00", ci), Rn(batches).ToString("0", ci), Rn(setpass).ToString("0", ci), Rn(triangles).ToString("0", ci), Rn(casters).ToString("0", ci) })).Append('\n');
            Info($"{Presets[p]} height {t.x:0}, tilt {rts.Tilt:0}: frame {frame / n:0.0} ms, main {Tn(main):0.0}, render {Tn(render):0.0}, gpu {Tn(gpu):0.0}, present wait {Tn(wait):0.0}, batches {Rn(batches):0}, triangles {Rn(triangles):0}, shadow casters {Rn(casters):0}");
        }

        static IEnumerator Picture(RtsCamera rts, string name, int side, float along, float h)
        {
            Aim(rts, side, along, h); yield return Frames(8); yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            try { Directory.CreateDirectory(Dir); File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG()); }
            catch (Exception e) { Debug.LogWarning(Tag + " could not write a picture: " + e.Message); }
            Object.Destroy(tex);
        }

        /// <summary>The engine culls the rim itself while on: every mesh renderer that has no block gets the project's small block, and the drawer lets go of it when it is switched on again. Off gives them back.</summary>
        static void EnginePath(bool on)
        {
            if (on)
            {
                taken.Clear();
                foreach (var q in pieces) foreach (var r in q.rends) if (r && !r.HasPropertyBlock()) taken.Add(r);
                OzGrd.KeepCpuCulling(taken.ToArray());
            }
            for (int i = 0; i < taken.Count; i++)
            {
                var r = taken[i]; if (!r) continue;
                if (!on) r.SetPropertyBlock(null);
                r.enabled = false; r.enabled = true;
            }
            if (!on) taken.Clear();
        }

        /// <summary>The fault itself on one piece, for a moment: a real LODGroup whose last height culls it at this size. If the piece does not read as gone, a clean count below proves nothing.</summary>
        static IEnumerator Control(int p, RtsCamera rts, Camera cam)
        {
            var eof = new WaitForEndOfFrame();
            Aim(rts, 0, 0.5f, 200f); yield return null; yield return eof;
            Frustum(cam); Piece c = null;
            foreach (var q in pieces) if (q.lod == null && GeometryUtility.TestPlanesAABB(planes, q.box) && Drawn(q)) { c = q; break; }
            if (c == null) { Check(false, $"{Presets[p]}: control: no drawn piece without a LODGroup at height 200 north"); yield break; }
            var g = c.root.gameObject.AddComponent<LODGroup>();
            g.fadeMode = LODFadeMode.None; g.SetLODs(new[] { new LOD(0.95f, c.rends) }); g.RecalculateBounds();
            yield return null; yield return null; yield return eof;
            bool gone = !Drawn(c);
            Object.Destroy(g);
            yield return null; yield return null; yield return eof;
            bool back = Drawn(c);
            Check(gone && back, $"{Presets[p]}: control, a real cull on one piece ({c.row}): read as gone {gone}, drawn again after {back}");
        }

        static void Read(Camera cam, float h)
        {
            steps++; Frustum(cam);
            foreach (var q in pieces)
            {
                int mask = 0;
                for (int i = 0; i < q.rends.Length; i++)
                {
                    if (q.rends[i].isVisible) mask |= 1 << i;
                    if (q.filters[i] && q.filters[i].sharedMesh != q.meshes[i]) { meshChanges++; q.meshes[i] = q.filters[i].sharedMesh; }
                }
                if (!GeometryUtility.TestPlanesAABB(planes, q.box)) { q.last = -1; if (mask == 0) offHidden++; continue; }
                sightings++;
                Tally t = null;
                if (mask == 0 || (q.last > 0 && mask != q.last)) { if (!rows.TryGetValue(q.row, out t)) rows[q.row] = t = new Tally(); }
                if (mask == 0) { notDrawn++; t.gone++; if (t.firstGone < 0f || h < t.firstGone) t.firstGone = h; if (q.last > 0) pops++; }
                else if (q.last == 0) pops++;
                else if (q.last > 0 && mask != q.last) { swaps++; t.swaps++; if (t.firstSwap < 0f || h < t.firstSwap) t.firstSwap = h; }
                q.last = mask;
            }
        }

        static IEnumerator Flyover(RtsCamera rts, Camera cam)
        {
            var eof = new WaitForEndOfFrame(); rts.enabled = false;
            foreach (var s in Shots)
            {
                foreach (var q in pieces) q.last = -1;
                for (int k = 0; k < ShotSamples; k++)
                {
                    float u = k / (ShotSamples - 1f); Vector3 at = Vector3.Lerp(s.a, s.b, u), look = Vector3.Lerp(s.la, s.lb, u);
                    cam.transform.SetPositionAndRotation(at, Quaternion.LookRotation(look - at)); cam.fieldOfView = Mathf.Lerp(s.fa, s.fb, u);
                    yield return null; yield return eof;
                    Read(cam, at.y);
                }
            }
            rts.enabled = true; cam.fieldOfView = fov0;
            foreach (var q in pieces) q.last = -1;
        }

        // ------------------------------------------------------------------ colours under the pieces
        static byte[] Grab(Camera cam)
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture(); var px = tex.GetPixels32(); int w = tex.width, h = tex.height; Object.Destroy(tex);
            var o = new byte[pieces.Count * Samples * 4];                       // r, g, b, on screen
            Frustum(cam);
            for (int i = 0; i < pieces.Count; i++)
            {
                if (!GeometryUtility.TestPlanesAABB(planes, pieces[i].box)) continue;
                for (int k = 0; k < Samples; k++)
                {
                    var v = cam.WorldToViewportPoint(pieces[i].pts[k]);
                    if (v.z <= 0f || v.x < 0.01f || v.x > 0.99f || v.y < 0.01f || v.y > 0.99f) continue;
                    var c = px[(int)(v.y * (h - 1)) * w + (int)(v.x * (w - 1))]; int at = (i * Samples + k) * 4;
                    o[at] = c.r; o[at + 1] = c.g; o[at + 2] = c.b; o[at + 3] = 1;
                }
            }
            return o;
        }

        /// <summary>A piece missing on one path shows the ground behind it at its sample points on that path; one hidden behind another piece reads the same on both.</summary>
        static void Compare(byte[] a, byte[] b, string side, float h, ref int compared, ref int differ, List<string> odd)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                int on = 0, off = 0;
                for (int k = 0; k < Samples; k++)
                {
                    int at = (i * Samples + k) * 4;
                    if (a[at + 3] == 0 || b[at + 3] == 0) continue;
                    on++;
                    if (Math.Abs(a[at] - b[at]) + Math.Abs(a[at + 1] - b[at + 1]) + Math.Abs(a[at + 2] - b[at + 2]) > SumDiff) off++;
                }
                if (on < 2) continue;
                compared++;
                if (off * 2 >= on) { differ++; odd.Add($"{pieces[i].row} {side} {h:0}"); }
            }
        }
    }
}
#endif
