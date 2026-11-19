// OpenZ (GPL-3.0) - version 1.5, kit l18_perf_r3: level 18's frame rate next to level 17's. VIEW ONLY: no rule, no Sim file, no saved setting.
// What the player exe measured on level 18 (quiet day, no battle): the first look at the outer city cost 60-150 ms frames on a fresh build
// (the graphics driver compiling the outer pieces' shader states the first time they are drawn), and the outer world is drawn into the
// sun's shadow map although most of it can never be seen or shadow anything that can be seen. Three parts, level 18 only:
//  1. WARM: the level's first frame (still behind the loading screen) renders the RTS view at the widest and the normal zoom over the five
//     views of the frame rate bench into a hidden picture once: every shader state and mesh of the outer world is made ready there, so the
//     first look in play costs nothing.
//  2. TRIM (HIGH / MEDIUM / LOW, invisible by construction): the RTS camera can only stand inside one box of places (the map's camera limits,
//     heading north, at its own pitch, between its closest and widest zoom). A piece of the static outer world that no such camera can see
//     stops being drawn; a piece whose shadow (the lowest sun of the day, 5 deg, any direction, down to the sea) cannot reach anything such a
//     camera sees within the shadow distance stops casting into the sun's shadow map. Receiving is never touched. Whenever the view is not
//     the plain RTS camera (briefing, fly-over, ending orbit, free look, another camera drawing the world) every piece is put back at once.
//  3. LOW only: outer pieces more than LowOuterM outside the play area cast no sun shadow and their LOD groups show the next simpler level.
// Off: OzLevelPerfTune.On = false, or the command line -oz-perftune off (this run only). Checks: ProofUndo / ProofRedo / Changed.
// Measuring helpers (command line only, with -oz-fpsbench): -oz-perftune-pairs <dir> saves at each bench view the picture with the tune and
// without it (time stopped for the pair) and logs the pixel difference; -oz-perftune-census <file> writes per group the drawn renderers and
// triangles at each view and the graphics memory list of the level.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    [DefaultExecutionOrder(950)]
    public sealed class OzLevelPerfTune : MonoBehaviour
    {
        public const string Arg = "-oz-perftune", ArgPairs = "-oz-perftune-pairs", ArgCensus = "-oz-perftune-census";
        const string Tag = "[OpenZ PerfTune]";
        /// <summary>Every part of this file.</summary>
        public static bool On = true;
        /// <summary>The levels it runs on.</summary>
        public static readonly string[] Levels = { "L18" };
        /// <summary>The lowest sun or moon of the day (deg over the horizon; the day's lowest key is 6) and the lowest ground a shadow can land on (m).</summary>
        public static float MinSunDeg = 5f, LowestGroundY = -3f;
        /// <summary>Extra room round every test (m): camera shake, rounding.</summary>
        public static float MarginM = 8f;
        /// <summary>LOW only: pieces farther than this outside the play area (m) are the "far" pieces.</summary>
        public static float LowOuterM = 8f;
        /// <summary>The widest aspect the screen may have (wider = a safe test).</summary>
        public static float AspectMax = 3.6f;

        public static OzLevelPerfTune I { get; private set; }
        /// <summary>Renderers the tune changes right now.</summary>
        public static int Changed => I ? I.changedNow : 0;
        public static string LastLine { get; private set; } = "not run";

        sealed class Piece { public MeshRenderer r; public ShadowCastingMode cast0; public bool off0; public bool hide, noShadow, lowFar; }
        readonly List<Piece> pieces = new List<Piece>();
        readonly List<LODGroup> lowLods = new List<LODGroup>();
        ZGame game; RtsCamera rts; Camera cam;
        int state = -1, changedNow, frame0 = -1;            // state: 0 nothing, 1 trim, 2 trim + LOW
        bool built, warmed, pairsBusy;
        static bool proofHold;
        string why = "";
        Vector3 tMin, tMax; float pitch0, fov0;
        string pairsDir, censusPath, lastPhase = "";
        static bool argOff, argRead;
        readonly Camera[] camBuf = new Camera[16];

        // ------------------------------------------------------------------ boot
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { On = true; I = null; LastLine = "not run"; argRead = argOff = proofHold = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; }

        static string Val(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return null;
        }

        static void OnScene(Scene s, LoadSceneMode m)
        {
            if (m != LoadSceneMode.Single || !Application.isPlaying) return;
            if (!argRead) { argRead = true; string v = Val(Arg); argOff = v != null && (v.Equals("off", StringComparison.OrdinalIgnoreCase) || v == "0"); }
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!g || Array.IndexOf(Levels, g.level) < 0) return;
            var go = new GameObject("OzLevelPerfTune") { hideFlags = HideFlags.HideInHierarchy };
            if (s.IsValid() && go.scene != s) SceneManager.MoveGameObjectToScene(go, s);
            var t = go.AddComponent<OzLevelPerfTune>(); t.game = g;
            if (!Application.isEditor) { t.pairsDir = Val(ArgPairs); t.censusPath = Val(ArgCensus); }
            if (argOff) Debug.Log($"{Tag} {g.level}: off for this run ({Arg} off)");
        }

        void Awake() { I = this; frame0 = Time.frameCount; }
        void OnDestroy() { Restore(); if (I == this) I = null; }

        bool Active => On && !argOff;

        // ------------------------------------------------------------------ every frame
        void LateUpdate()
        {
            if (!Active) { if (state > 0) Restore(); return; }
            if (!built && !Build()) return;
            if (!warmed) { warmed = true; Warm(); }
            if (proofHold) return;
            int want = !ViewIsRts(out string w) ? 0 : OzQuality.Level == 2 ? 2 : 1;
            if (want != state)
            {
                Apply(want);
                if (w != why) { why = w; Debug.Log($"{Tag} {(want == 0 ? "every piece as the scene has it: " + w : want == 2 ? "on (LOW: far pieces simpler)" : "on")}, {changedNow} renderers changed"); }
            }
            if ((pairsDir != null || censusPath != null) && !pairsBusy) WatchBench();
        }

        // ------------------------------------------------------------------ 0. the pieces and the camera's box of places
        bool Build()
        {
            if (!game) game = ZGame.I;
            if (!game || game.World == null) return false;
            if (!rts) rts = FindFirstObjectByType<RtsCamera>();
            if (!rts) return false;
            cam = rts.GetComponent<Camera>(); if (!cam) cam = Camera.main;
            if (!cam) return false;
            built = true;
            var map = game.World.Map; float W = map.WidthPix / 8f, H = map.HeightPix / 8f;
            pitch0 = rts.pitch; fov0 = cam.fieldOfView;
            float tp = Mathf.Tan(Mathf.Clamp(pitch0, 5f, 89f) * Mathf.Deg2Rad), h0 = rts.minHeight, h1 = rts.maxHeight;
            float z0 = -H - Mathf.Max(0f, rts.southGive) - 10f, z1 = 0f;
            // the camera's places: focus (x, 0, z) + (0, h, -h / tan pitch), heading north, x in the map, z from the south limit to the north edge
            tMin = new Vector3(0f, h0, z0 - h1 / tp) - Vector3.one * MarginM;
            tMax = new Vector3(W, h1, z1 - h0 / tp) + Vector3.one * MarginM;
            var rect = new Rect(0f, -H, W, H);
            float far = cam.farClipPlane, farShadow = Mathf.Min(far, Mathf.Max(ShadowDistanceByHeight.Max, CurrentShadowDistance()) + 20f);
            var vis = Planes(far); var sh = Planes(farShadow);
            float tanSun = Mathf.Tan(MinSunDeg * Mathf.Deg2Rad);
            int cand = 0, hide = 0, nosh = 0, lowFar = 0, skipped = 0;
            var envRoots = new List<Transform>();
            foreach (var root in gameObject.scene.GetRootGameObjects()) if (root.name == "Environment") envRoots.Add(root.transform);
            foreach (var er in envRoots)
                foreach (var r in er.GetComponentsInChildren<MeshRenderer>(false))
                {
                    if (!r.enabled || r.forceRenderingOff) continue;
                    if (!r.isPartOfStaticBatch && (r.GetComponentInParent<Rigidbody>() || HasScript(r.transform, er))) { skipped++; continue; }   // anything that moves or is driven by a script stays as it is
                    cand++;
                    var b = r.bounds;
                    var p = new Piece { r = r, cast0 = r.shadowCastingMode, off0 = r.forceRenderingOff };
                    float reach = Mathf.Max(0f, b.max.y - LowestGroundY) / tanSun;
                    var bs = new Bounds(); bs.SetMinMax(new Vector3(b.min.x - reach, Mathf.Min(b.min.y, LowestGroundY), b.min.z - reach), new Vector3(b.max.x + reach, b.max.y, b.max.z + reach));
                    bool seen = Hits(vis, b), shades = p.cast0 != ShadowCastingMode.Off && Hits(sh, bs);
                    p.noShadow = p.cast0 != ShadowCastingMode.Off && !shades;
                    p.hide = !seen && !shades;
                    p.lowFar = p.cast0 != ShadowCastingMode.Off && OutsideBy(rect, b) > LowOuterM;
                    if (p.hide) hide++; else if (p.noShadow) nosh++;
                    if (p.lowFar) lowFar++;
                    if (p.hide || p.noShadow || p.lowFar) pieces.Add(p);
                }
            foreach (var er in envRoots)
                foreach (var lg in er.GetComponentsInChildren<LODGroup>(false))
                {
                    if (lg.lodCount < 3) continue;
                    var c = lg.transform.TransformPoint(lg.localReferencePoint);
                    if (OutsideBy(rect, new Bounds(c, Vector3.zero)) > LowOuterM + lg.size * 0.5f) lowLods.Add(lg);
                }
            LastLine = $"{game.level}: {cand} static outer-world renderers checked ({skipped} moving / scripted left alone): {hide} never seen and never shadowing what is seen (not drawn), " +
                       $"{nosh} more whose shadow never reaches what is seen (no shadow-map draw); LOW: {lowFar} far pieces without sun shadow, {lowLods.Count} far LOD groups one level simpler; " +
                       $"camera places x {tMin.x:0}..{tMax.x:0} y {tMin.y:0}..{tMax.y:0} z {tMin.z:0}..{tMax.z:0}, pitch {pitch0:0.#}, far {far:0} m, shadows to {farShadow:0} m, sun from {MinSunDeg:0} deg";
            Debug.Log($"{Tag} {LastLine}");
            return true;
        }

        static bool HasScript(Transform t, Transform stop)
        {
            for (var x = t; x != null && x != stop; x = x.parent)
                foreach (var mb in x.GetComponents<MonoBehaviour>()) if (mb && mb.enabled) return true;
            return false;
        }

        static float OutsideBy(Rect rect, Bounds b)
        {
            float dx = Mathf.Max(rect.xMin - b.max.x, b.min.x - rect.xMax, 0f), dz = Mathf.Max(rect.yMin - b.max.z, b.min.z - rect.yMax, 0f);
            return Mathf.Max(dx, dz);
        }

        float CurrentShadowDistance() { var u = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset; return u ? u.shadowDistance : 0f; }

        /// <summary>The frustum of the RTS camera standing at the origin (heading north, its pitch, fov, the widest aspect).</summary>
        Plane[] Planes(float far)
        {
            var proj = Matrix4x4.Perspective(fov0, AspectMax, Mathf.Max(0.01f, cam.nearClipPlane), far);
            var view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(pitch0, 0f, 0f), Vector3.one).inverse;
            return GeometryUtility.CalculateFrustumPlanes(proj * view);
        }

        /// <summary>Can a camera somewhere in the box of places see box b? b moved by every place = b grown by the box (Minkowski), against
        /// one frustum at the origin. The plane test keeps a box when unsure, so the answer errs on "seen".</summary>
        bool Hits(Plane[] planes, Bounds b)
        {
            var e = new Bounds(); e.SetMinMax(b.min - tMax, b.max - tMin);
            return GeometryUtility.TestPlanesAABB(planes, e);
        }

        /// <summary>The plain RTS view: the RTS camera alone draws the world, heading north at its own pitch and fov, inside its box of places.</summary>
        bool ViewIsRts(out string w)
        {
            w = "";
            if (!rts || !cam || !rts.isActiveAndEnabled || !cam.isActiveAndEnabled || Camera.main != cam) { w = "not the RTS camera"; return false; }
            if (ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive) { w = "briefing"; return false; }
            if (Mathf.Abs(rts.yaw) > 0.01f || rts.unclamped || Mathf.Abs(rts.pitch - pitch0) > 0.01f) { w = "camera turned"; return false; }
            if (Mathf.Abs(cam.fieldOfView - fov0) > 0.01f || cam.targetTexture != null) { w = "camera lens"; return false; }
            var e = cam.transform.eulerAngles;
            if (Mathf.Abs(Mathf.DeltaAngle(e.x, pitch0)) > 0.3f || Mathf.Abs(Mathf.DeltaAngle(e.y, 0f)) > 0.3f || Mathf.Abs(Mathf.DeltaAngle(e.z, 0f)) > 0.3f) { w = "camera tilted"; return false; }
            var p = cam.transform.position;
            if (p.x < tMin.x || p.y < tMin.y || p.z < tMin.z || p.x > tMax.x || p.y > tMax.y || p.z > tMax.z) { w = "camera outside its places"; return false; }
            if (Camera.allCamerasCount > camBuf.Length) { w = "many cameras"; return false; }
            int n = Camera.GetAllCameras(camBuf);
            for (int i = 0; i < n; i++)
            {
                var c = camBuf[i];
                if (!c || c == cam || !c.isActiveAndEnabled) continue;
                if ((c.cullingMask & 1) != 0) { w = "camera " + c.name; return false; }                // another camera that draws the Default layer (the world)
            }
            return true;
        }

        // ------------------------------------------------------------------ apply / restore
        void Apply(int want)
        {
            Restore();
            if (want <= 0) { state = 0; return; }
            int n = 0;
            foreach (var p in pieces)
            {
                if (!p.r) continue;
                bool ch = false;
                if (p.hide) { p.r.forceRenderingOff = true; ch = true; }
                else if (p.noShadow || (want == 2 && p.lowFar)) { p.r.shadowCastingMode = ShadowCastingMode.Off; ch = true; }
                if (ch) n++;
            }
            if (want == 2) foreach (var lg in lowLods) if (lg) lg.ForceLOD(1);
            changedNow = n; state = want;
        }

        void Restore()
        {
            if (state > 0)
            {
                foreach (var p in pieces) if (p.r) { p.r.forceRenderingOff = p.off0; p.r.shadowCastingMode = p.cast0; }
                if (state == 2) foreach (var lg in lowLods) if (lg) lg.ForceLOD(-1);
            }
            changedNow = 0; state = 0;
        }

        /// <summary>Checks: every piece as the scene has it (until ProofRedo).</summary>
        public static void ProofUndo() { proofHold = true; if (I) I.Restore(); }
        /// <summary>Checks: the tune as the view wants it again.</summary>
        public static void ProofRedo() { proofHold = false; if (I) I.state = -1; }

        // ------------------------------------------------------------------ 1. warm: the outer world drawn once behind the loading screen
        void Warm()
        {
            if (Time.frameCount > frame0 + 2) { Debug.Log($"{Tag} warm skipped: the level's first frames are over"); return; }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var map = game.World.Map; float W = map.WidthPix / 8f, H = map.HeightPix / 8f;
            var u = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var rt = new RenderTexture(Mathf.Max(64, Screen.width), Mathf.Max(64, Screen.height), 32, RenderTextureFormat.ARGB32)
            { antiAliasing = u ? Mathf.Max(1, u.msaaSampleCount) : 1, name = "OzPerfTuneWarm" };
            var t = cam.transform; Vector3 p0 = t.position; Quaternion q0 = t.rotation; var tt0 = cam.targetTexture;
            float tp = Mathf.Tan(Mathf.Clamp(rts.pitch, 5f, 89f) * Mathf.Deg2Rad);
            int n = 0;
            try
            {
                foreach (float h in new[] { rts.maxHeight, 70f })
                    foreach (var f in new[] { new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0f), new Vector2(0.5f, -1f), new Vector2(0f, 0f), new Vector2(1f, -1f), new Vector2(0f, -1f), new Vector2(1f, 0f) })
                    {
                        var focus = new Vector3(Mathf.Clamp(f.x * W, h * 0.3f, W - h * 0.3f), 0f, Mathf.Clamp(f.y * H, -H + h * 0.42f, -10f));
                        t.SetPositionAndRotation(focus + new Vector3(0f, h, -h / tp), Quaternion.Euler(rts.pitch, 0f, 0f));
                        cam.targetTexture = rt; cam.Render(); n++;
                    }
            }
            catch (Exception e) { Debug.LogWarning($"{Tag} warm stopped: {e.GetType().Name} {e.Message}"); }
            finally { cam.targetTexture = tt0; t.SetPositionAndRotation(p0, q0); rt.Release(); Destroy(rt); }
            Debug.Log($"{Tag} warm: {n} views of the outer world drawn behind the loading screen in {sw.ElapsedMilliseconds} ms");
        }

        // ------------------------------------------------------------------ measuring helpers (bench runs only)
        static FieldInfo phaseField;
        void WatchBench()
        {
            var b = OzFpsBench.I;
            if (!b) return;
            if (phaseField == null) phaseField = typeof(OzFpsBench).GetField("phase", BindingFlags.Instance | BindingFlags.NonPublic);
            string ph = phaseField != null ? phaseField.GetValue(b) as string : null;
            if (ph == null || ph == lastPhase) return;
            lastPhase = ph;
            if (!ph.StartsWith("measure ", StringComparison.Ordinal)) return;
            StartCoroutine(AtView(ph.Substring(8)));
        }

        IEnumerator AtView(string view)
        {
            pairsBusy = true;
            yield return null; yield return null;
            if (censusPath != null) Census(view);
            if (pairsDir != null)
            {
                float ts = Time.timeScale; Time.timeScale = 0f;
                yield return new WaitForEndOfFrame(); yield return null; yield return new WaitForEndOfFrame();
                var a1 = ScreenCapture.CaptureScreenshotAsTexture();
                yield return null; yield return new WaitForEndOfFrame();
                var a2 = ScreenCapture.CaptureScreenshotAsTexture();
                int keep = state; ProofUndo();
                yield return null; yield return new WaitForEndOfFrame();
                var bOff = ScreenCapture.CaptureScreenshotAsTexture();
                ProofRedo(); Time.timeScale = ts;
                try
                {
                    Directory.CreateDirectory(pairsDir);
                    string q = OzQuality.Name, stem = Path.Combine(pairsDir, $"{game.level}_{q}_{view}_h{rts.height:0}");
                    File.WriteAllBytes(stem + "_tune.png", a1.EncodeToPNG());
                    File.WriteAllBytes(stem + "_off.png", bOff.EncodeToPNG());
                    string line = $"PAIR {game.level} {q} view {view} height {rts.height:0} m, tune state {keep}, {changedNow} renderers changed | same picture twice: {Diff(a1, a2)} | tune vs off: {Diff(a1, bOff)}";
                    Debug.Log($"{Tag} {line}");
                    File.AppendAllText(Path.Combine(pairsDir, "pairs.txt"), line + "\n");
                }
                catch (Exception e) { Debug.LogWarning($"{Tag} pair not saved: {e.Message}"); }
                Destroy(a1); Destroy(a2); Destroy(bOff);
            }
            pairsBusy = false;
        }

        /// <summary>Pixels whose largest channel difference is over 2, 8 and 24 (of 255), the largest difference, the mean.</summary>
        static string Diff(Texture2D a, Texture2D b)
        {
            if (a.width != b.width || a.height != b.height) return "sizes differ";
            var pa = a.GetPixels32(); var pb = b.GetPixels32();
            int o2 = 0, o8 = 0, o24 = 0, mx = 0; double sum = 0;
            for (int i = 0; i < pa.Length; i++)
            {
                int d = Mathf.Max(Mathf.Abs(pa[i].r - pb[i].r), Mathf.Max(Mathf.Abs(pa[i].g - pb[i].g), Mathf.Abs(pa[i].b - pb[i].b)));
                if (d > 2) o2++; if (d > 8) o8++; if (d > 24) o24++; if (d > mx) mx = d; sum += d;
            }
            float n = pa.Length;
            return $"over 2: {o2} px ({100f * o2 / n:0.###} %), over 8: {o8} px ({100f * o8 / n:0.###} %), over 24: {o24} px, largest {mx}, mean {sum / n:0.####}";
        }

        bool memDone;
        void Census(string view)
        {
            var sb = new StringBuilder();
            sb.Append($"#### {game.level} {OzQuality.Name} view {view} height {rts.height:0} m, tune state {state}, {changedNow} renderers changed\n");
            sb.Append("group,renderers_on,drawn,drawn_tris,drawn_shadow_casters\n");
            var groups = new SortedDictionary<string, long[]>();
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<Renderer>(false))
                {
                    if (!r.enabled || r.forceRenderingOff) continue;
                    var tr = r.transform; string key = root.name;
                    if (root.transform.childCount > 0 && tr != root.transform)
                    {
                        var x = tr; while (x.parent != root.transform && x.parent != null) x = x.parent;
                        if (root.name == "Environment" || root.name == "ZodObjects") key = root.name + "/" + Short(x.name);
                    }
                    if (!groups.TryGetValue(key, out var v)) groups[key] = v = new long[4];
                    v[0]++;
                    if (!r.isVisible) continue;
                    v[1]++; v[2] += Tris(r); if (r.shadowCastingMode != ShadowCastingMode.Off) v[3]++;
                }
            foreach (var kv in groups) if (kv.Value[1] > 0) sb.Append($"\"{kv.Key}\",{kv.Value[0]},{kv.Value[1]},{kv.Value[2]},{kv.Value[3]}\n");
            if (!memDone) { memDone = true; Memory(sb); }
            try { File.AppendAllText(censusPath, sb.ToString()); } catch (Exception e) { Debug.LogWarning($"{Tag} census not saved: {e.Message}"); }
        }

        static string Short(string n) => n.Length > 40 ? n.Substring(0, 40) : n;

        static long Tris(Renderer r)
        {
            Mesh m = null; int first = 0, count = 1;
            if (r is MeshRenderer mr) { var mf = r.GetComponent<MeshFilter>(); m = mf ? mf.sharedMesh : null; first = mr.isPartOfStaticBatch ? mr.subMeshStartIndex : 0; count = Mathf.Max(1, r.sharedMaterials.Length); }
            else if (r is SkinnedMeshRenderer sk) { m = sk.sharedMesh; count = m ? m.subMeshCount : 0; }
            if (!m) return 0;
            long t = 0;
            for (int i = first; i < Mathf.Min(m.subMeshCount, first + count); i++) t += m.GetSubMesh(i).indexCount / 3;
            return t;
        }

        static void Memory(StringBuilder sb)
        {
            var rows = new List<KeyValuePair<long, string>>(); long tex = 0, rts = 0, mesh = 0;
            foreach (var t in Resources.FindObjectsOfTypeAll<Texture>())
            {
                long by; string kind;
                if (t is RenderTexture rt) { if (!rt.IsCreated()) continue; by = (long)(rt.width * (double)rt.height * Math.Max(1, rt.antiAliasing) * (Bpp(rt.graphicsFormat) + (rt.depth > 0 ? 4 : 0))); kind = "RT"; rts += by; }
                else if (t is Texture2D t2) { by = (long)(t2.width * (double)t2.height * Bpp(t2.graphicsFormat) * (t2.mipmapCount > 1 ? 1.333 : 1.0)); kind = "Texture2D"; tex += by; }
                else if (t is Cubemap cm) { by = (long)(cm.width * (double)cm.width * 6 * Bpp(cm.graphicsFormat) * (cm.mipmapCount > 1 ? 1.333 : 1.0)); kind = "Cubemap"; tex += by; }
                else if (t is Texture3D t3) { by = (long)t3.width * t3.height * t3.depth * (long)Math.Max(1, Bpp(t3.graphicsFormat)); kind = "Texture3D"; tex += by; }
                else if (t is Texture2DArray ta) { by = (long)(ta.width * (double)ta.height * ta.depth * Bpp(ta.graphicsFormat) * (ta.mipmapCount > 1 ? 1.333 : 1.0)); kind = "Texture2DArray"; tex += by; }
                else continue;
                rows.Add(new KeyValuePair<long, string>(by, $"{kind},\"{t.name}\",{t.width},{t.height},{t.graphicsFormat},{by}"));
            }
            foreach (var m in Resources.FindObjectsOfTypeAll<Mesh>())
            {
                long by = 0;
                for (int s = 0; s < m.vertexBufferCount; s++) by += (long)m.vertexCount * m.GetVertexBufferStride(s);
                long idx = 0; for (int s = 0; s < m.subMeshCount; s++) idx += m.GetSubMesh(s).indexCount;
                by += idx * (m.indexFormat == IndexFormat.UInt32 ? 4 : 2); mesh += by;
                rows.Add(new KeyValuePair<long, string>(by, $"Mesh,\"{m.name}\",{m.vertexCount},{idx},-,{by}"));
            }
            rows.Sort((a, b) => b.Key.CompareTo(a.Key));
            sb.Append($"#### graphics memory (estimated from sizes and formats): textures {tex / 1048576} MB, render textures {rts / 1048576} MB, meshes {mesh / 1048576} MB\nkind,name,w_or_verts,h_or_indices,format,bytes\n");
            for (int i = 0; i < rows.Count && i < 400; i++) sb.Append(rows[i].Value).Append('\n');
        }

        static float Bpp(UnityEngine.Experimental.Rendering.GraphicsFormat f)
        {
            string s = f.ToString();
            if (s.Contains("BC7") || s.Contains("BC3") || s.Contains("DXT5") || s.Contains("BC5") || s.Contains("BC6H")) return 1f;
            if (s.Contains("BC1") || s.Contains("DXT1") || s.Contains("BC4")) return 0.5f;
            if (s.Contains("R32G32B32A32")) return 16f;
            if (s.Contains("R16G16B16A16")) return 8f;
            if (s.StartsWith("R8_") || s == "R8_UNorm" || s.Contains("A8")) return s.Contains("R8G8B8A8") || s.Contains("B8G8R8A8") ? 4f : 1f;
            if (s.Contains("R16_") || s.Contains("R8G8_")) return 2f;
            if (s.Contains("D32") || s.Contains("D24")) return 4f;
            return 4f;
        }
    }
}
