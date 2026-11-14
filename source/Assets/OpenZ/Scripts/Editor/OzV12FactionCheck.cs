#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// k75a, the faction screen (1.2 owner items 77, 78, 78b, 81, 88, 89, 90): the counted check in edit mode (no Play, nothing saved;
    /// menu OpenZ/V1.2/Faction Screen: 75a Counted Check) and the Play proof menus (View/OzFactionProof.cs).
    ///  A  the assets: the two light models (85 000 triangles, the import as the light models' own notes say, their bounds where the
    ///     measured cameras expect them, seen by those cameras), their URP Lit materials and maps, the print's layers (1152 x 2048, alpha
    ///     where they need it, never scaled to a power of two), the grain tile (repeats), the renders' UI shader (compiles), Zod's two
    ///     pick lines (the approved takes: lengths, form and import as his own lines), the memory they take while the screen shows (the editor's count, under 128 MB).
    ///  B  the words and the pure rules: the titles, tips and captions; the pick sets the PLAY AS BLUE box's own setting; the mood's
    ///     target and its ease (no jump, no overshoot); the sway loop (limits, period, continuous, forward and back); the pointer follow
    ///     (limits); the orbit (the pivot stays in the middle of the view, yaw + moves right, pitch + moves down and looks up); the layer
    ///     slide; the measured cameras look at their pivots with the measured up; (k75a2) the mood's soft glow and the print's aged paper.
    ///  C  the menu's wiring: View.Faction, the PLAY item, the level selection's BACK, no PLAY AS BLUE key or switch left.
    ///  D  the layout at 1920 x 1080, 1366 x 768, 1280 x 1024, 1024 x 768, 2560 x 1600 and 3440 x 1440 (canvas units).
    /// Lines in the kit's proof/counted_check.txt; one line "[OzV12FactionCheck] PASS n/n" (or FAIL).
    /// </summary>
    public static class OzV12FactionCheck
    {
        public const string CheckMenu = "OpenZ/V1.2/Faction Screen: 75a Counted Check";
        public const string ProofMenu = "OpenZ/V1.2/Faction Screen: 75a Play Proof (Play)";
        public const string PicturesMenu = "OpenZ/V1.2/Faction Screen: 75a Pictures Only (Play)";
        public const string StopMenu = "OpenZ/V1.2/Faction Screen: 75a Play Proof: stop";
        const string T = "[OzV12FactionCheck] ";
        const string Root = "Assets/OpenZ/Resources/";

        [MenuItem(ProofMenu)]
        static void Proof() { FocusGame(); OzFactionProof.Run(false); }
        [MenuItem(ProofMenu, true)] static bool ProofReady() => Application.isPlaying;
        [MenuItem(PicturesMenu)]
        static void Pictures() { FocusGame(); OzFactionProof.Run(true); }
        [MenuItem(PicturesMenu, true)] static bool PicturesReady() => Application.isPlaying;
        [MenuItem(StopMenu)]
        static void Stop() => OzFactionProof.Stop();

        static void FocusGame()
        {
            var gv = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gv != null) EditorWindow.FocusWindowIfItsOpen(gv);
        }

        static readonly StringBuilder sb = new StringBuilder();
        static int pass, fail;
        static void Line(string s) { sb.AppendLine(s); }
        static void V(bool ok, string what) { if (ok) pass++; else fail++; Line((ok ? "PASS " : "FAIL ") + what); }

        [MenuItem(CheckMenu)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning(T + "run it in edit mode (no Play)"); return; }
            string report = Check();
            Debug.Log(report);
        }

        public static string Check()
        {
            sb.Clear(); pass = fail = 0;
            Line("k75a the faction screen: the counted check (edit mode)");
            try { Assets(); } catch (Exception e) { V(false, "A the assets threw " + e.GetType().Name + ": " + e.Message); }
            try { Rules(); } catch (Exception e) { V(false, "B the rules threw " + e.GetType().Name + ": " + e.Message); }
            try { Wiring(); } catch (Exception e) { V(false, "C the wiring threw " + e.GetType().Name + ": " + e.Message); }
            try { Layout(); } catch (Exception e) { V(false, "D the layout threw " + e.GetType().Name + ": " + e.Message); }
            string head = T + (fail == 0 ? "PASS " : "FAIL ") + pass + "/" + (pass + fail);
            sb.Insert(0, head + "\n");
            try
            {
                string dir = OzFactionProof.Folder();
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "counted_check.txt"), sb.ToString());
                head += " (" + Path.Combine(dir, "counted_check.txt").Replace('\\', '/') + ")";
            }
            catch (Exception e) { head += " (the lines could not be written: " + e.Message + ")"; }
            Debug.Log(sb.ToString());
            return head;
        }

        // ================================================================================================================ A: the assets
        static string F(Vector3 v) => $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";

        static void Assets()
        {
            long bytes = 0;
            for (int side = 0; side < 2; side++)
            {
                var cam = OzFaction.Cam(side); string id = cam.id;
                string fbx = Root + OzFactionPosters.Folder + id + "_poster_light.fbx";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                var mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
                V(prefab && Resources.Load<GameObject>(OzFactionPosters.Folder + id + "_poster_light") == prefab, $"A {id}: the light model loads from Resources ({fbx})");
                if (!prefab) continue;
                if (mi != null)
                    V(mi.materialImportMode == ModelImporterMaterialImportMode.None && !mi.isReadable && !mi.importAnimation && mi.animationType == ModelImporterAnimationType.None
                      && mi.importNormals == ModelImporterNormals.Import && mi.importTangents == ModelImporterTangents.CalculateMikk && Mathf.Approximately(mi.globalScale, 1f) && mi.useFileUnits,
                      $"A {id}: imported as the notes say (no materials, not readable, no animation, normals imported, tangents MikkTSpace, scale 1, file units): "
                      + $"{mi.materialImportMode}, readable {mi.isReadable}, animation {mi.importAnimation} / {mi.animationType}, normals {mi.importNormals}, tangents {mi.importTangents}, scale {mi.globalScale}, units {mi.useFileUnits}");
                else V(false, $"A {id}: a model importer at {fbx}");
                var mfs = prefab.GetComponentsInChildren<MeshFilter>(true);
                int tris = 0; var b = new Bounds(); bool first = true; bool flat = true;
                foreach (var mf in mfs)
                {
                    var m = mf.sharedMesh; if (!m) continue;
                    for (int s = 0; s < m.subMeshCount; s++) tris += (int)(m.GetIndexCount(s) / 3);
                    var toRoot = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                    var mb = m.bounds;
                    for (int c = 0; c < 8; c++)
                    {
                        var p = toRoot.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                        if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                    }
                    if (Quaternion.Angle(mf.transform.localRotation, Quaternion.identity) > 0.1f || (mf.transform.localScale - Vector3.one).magnitude > 1e-3f) flat = false;
                }
                bool rootFlat = Quaternion.Angle(prefab.transform.localRotation, Quaternion.identity) < 0.1f && (prefab.transform.localScale - Vector3.one).magnitude < 1e-3f;
                V(mfs.Length == 1 && tris == 85000, $"A {id}: one mesh, {tris} triangles (85 000 wanted), {prefab.GetComponentsInChildren<Renderer>(true).Length} renderer(s)");
                V(rootFlat && flat, $"A {id}: the model's root and its mesh are not turned or scaled (the camera numbers are local to the root): root {prefab.transform.localEulerAngles} x{prefab.transform.localScale.x:0.###}");
                var want = OzFaction.ModelBounds[side];
                float dc = (b.center - want.center).magnitude, ds = (b.size - want.size).magnitude;
                V(dc < 0.02f && ds < 0.03f, $"A {id}: the mesh in the root's frame where the measured camera expects it: centre {F(b.center)} size {F(b.size)} (wanted {F(want.center)} / {F(want.size)}: {dc * 100f:0.0} / {ds * 100f:0.0} cm off)");
                // the measured camera sees it (a hidden camera, never saved)
                var go = new GameObject("k75a check camera") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    var c = go.AddComponent<Camera>(); c.enabled = false; c.fieldOfView = cam.fov; c.aspect = OzFaction.PosterAspect; c.nearClipPlane = OzFactionPosters.Near; c.farClipPlane = OzFactionPosters.Far;
                    go.transform.SetPositionAndRotation(cam.pos, cam.rot);
                    var vc = c.WorldToViewportPoint(b.center); float top = -1f, bottom = 2f;
                    for (int k = 0; k < 8; k++)
                    {
                        var corner = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                        var v = c.WorldToViewportPoint(corner); top = Mathf.Max(top, v.y); bottom = Mathf.Min(bottom, v.y);
                    }
                    var pv = c.WorldToViewportPoint(cam.pivot);
                    V(vc.z > 0.5f && vc.x > 0.2f && vc.x < 0.8f && vc.y > 0.15f && vc.y < 0.85f && top - bottom > 0.6f && Mathf.Abs(pv.x - 0.5f) < 0.01f && Mathf.Abs(pv.y - 0.5f) < 0.01f,
                      $"A {id}: the measured camera sees the model: its middle at viewport ({vc.x:0.00}, {vc.y:0.00}) {vc.z:0.00} m ahead, it spans {(top - bottom) * 100f:0} % of the frame's height, the pivot at ({pv.x:0.000}, {pv.y:0.000})");
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
                // its material and maps
                string matPath = Root + OzFactionPosters.Folder + id + "_poster.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                var maps = new Dictionary<string, string> { { "_BaseMap", "BaseColor" }, { "_BumpMap", "Normal" }, { "_MetallicGlossMap", "MetallicSmoothness" }, { "_EmissionMap", "Emission" } };
                bool mapsOk = mat; var mapLog = new StringBuilder();
                if (mat)
                    foreach (var kv in maps)
                    {
                        string want2 = Root + OzFactionPosters.Folder + id + "_poster_" + kv.Value + ".png";
                        string got = AssetDatabase.GetAssetPath(mat.GetTexture(kv.Key));
                        if (got != want2) { mapsOk = false; mapLog.Append(' ').Append(kv.Key).Append('=').Append(string.IsNullOrEmpty(got) ? "none" : Path.GetFileName(got)); }
                    }
                V(mat && mat.shader && mat.shader.name == "Universal Render Pipeline/Lit" && mat.IsKeywordEnabled("_NORMALMAP") && mat.IsKeywordEnabled("_METALLICSPECGLOSSMAP") && mat.IsKeywordEnabled("_EMISSION")
                  && mapsOk && Mathf.Approximately(mat.GetFloat("_Smoothness"), 1f) && Mathf.Approximately(mat.GetFloat("_SmoothnessTextureChannel"), 0f) && mat.GetColor("_EmissionColor").r > 1.5f,
                  $"A {id}: its URP Lit material: {(mat && mat.shader ? mat.shader.name : "MISSING")}, normal / metallic / emission keywords, the 4 maps in their slots{(mapLog.Length > 0 ? " (wrong:" + mapLog + ")" : "")}, smoothness from the metallic alpha, emission x{(mat ? mat.GetColor("_EmissionColor").r : 0f):0.#}");
                foreach (var kv in maps)
                {
                    string p = Root + OzFactionPosters.Folder + id + "_poster_" + kv.Value + ".png";
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p); var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                    bool srgb = kv.Value == "BaseColor" || kv.Value == "Emission", normal = kv.Value == "Normal";
                    bool ok = tex && ti != null && tex.width == 2048 && tex.height == 2048 && (normal ? ti.textureType == TextureImporterType.NormalMap : ti.textureType == TextureImporterType.Default && ti.sRGBTexture == srgb);
                    V(ok, $"A {id}: {kv.Value} map {(tex ? tex.width + " x " + tex.height : "MISSING")}, {(ti != null ? ti.textureType + (normal ? "" : ", sRGB " + ti.sRGBTexture) : "no importer")}");
                    if (tex) bytes += Profiler.GetRuntimeMemorySizeLong(tex);
                }
                // the print's layers
                for (int k = 0; k < OzFactionPosters.LayerParts.Length; k++)
                {
                    string p = Root + OzFactionPosters.LayerFolder + id + "_" + OzFactionPosters.LayerParts[k] + ".png";
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p); var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                    bool alpha = k > 0;
                    bool ok = tex && ti != null && tex.width == 1152 && tex.height == 2048 && ti.npotScale == TextureImporterNPOTScale.None && ti.textureType == TextureImporterType.Default && ti.sRGBTexture
                              && (alpha ? ti.alphaSource == TextureImporterAlphaSource.FromInput && ti.alphaIsTransparency : true) && tex.wrapMode == TextureWrapMode.Clamp
                              && Resources.Load<Texture2D>(OzFactionPosters.LayerFolder + id + "_" + OzFactionPosters.LayerParts[k]) == tex;
                    V(ok, $"A {id}: layer {OzFactionPosters.LayerParts[k]} {(tex ? tex.width + " x " + tex.height + " " + tex.format : "MISSING")}{(ti != null ? ", alpha " + ti.alphaSource + ", npot " + ti.npotScale + ", wrap " + (tex ? tex.wrapMode.ToString() : "-") : "")}");
                    if (tex) bytes += Profiler.GetRuntimeMemorySizeLong(tex);
                }
                var fbxMesh = mfs.Length > 0 ? mfs[0].sharedMesh : null;
                if (fbxMesh) bytes += Profiler.GetRuntimeMemorySizeLong(fbxMesh);
            }
            {
                string p = Root + OzFactionPosters.LayerFolder + OzFactionPosters.GrainTile + ".png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p); var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                V(tex && ti != null && tex.width == 1024 && tex.height == 1024 && tex.wrapMode == TextureWrapMode.Repeat && ti.alphaSource == TextureImporterAlphaSource.FromInput,
                  $"A the paper grain tile {(tex ? tex.width + " x " + tex.height + ", wrap " + tex.wrapMode : "MISSING")} (it repeats over the print)");
                if (tex) bytes += Profiler.GetRuntimeMemorySizeLong(tex);
            }
            {
                var mat = Resources.Load<Material>(OzFactionPosters.Folder + OzFactionPosters.FigureMaterial);
                var sh = mat ? mat.shader : null;
                bool paperProps = mat && mat.HasProperty("_Print") && mat.HasProperty("_PaperSee") && mat.HasProperty("_PaperTone") && mat.HasProperty("_PaperBehind") && mat.HasProperty("_PaperLum") && mat.HasProperty("_PaperChroma");   // k75a2
                V(sh && sh.name == "OpenZ/UI/PosterFigure" && !ShaderUtil.ShaderHasError(sh) && mat.HasProperty("_RimPx") && mat.HasProperty("_RimColor") && paperProps,
                  $"A the renders' UI material: shader {(sh ? sh.name : "MISSING")}, errors {(sh ? ShaderUtil.ShaderHasError(sh).ToString() : "-")}, paper rim properties {(mat ? mat.HasProperty("_RimPx") && mat.HasProperty("_RimColor") : false)}, the print mode's paper properties {paperProps}");
            }
            // Zod's lines
            var refImp = AssetImporter.GetAtPath(Root + "OpenZ/Zod/Voice/zod_idle_01.wav") as AudioImporter;
            float[] lens = { 1.68f, 2.508f };
            for (int side = 0; side < 2; side++)
            {
                string res = OzFaction.Voice[side], p = Root + res + ".wav";
                var clip = Resources.Load<AudioClip>(res); var ai = AssetImporter.GetAtPath(p) as AudioImporter;
                bool same = ai != null && refImp != null && ai.forceToMono == refImp.forceToMono && ai.defaultSampleSettings.loadType == refImp.defaultSampleSettings.loadType
                            && ai.defaultSampleSettings.compressionFormat == refImp.defaultSampleSettings.compressionFormat && ai.defaultSampleSettings.preloadAudioData == refImp.defaultSampleSettings.preloadAudioData;
                V(clip && Mathf.Abs(clip.length - lens[side]) < 0.03f && clip.channels == 1 && clip.frequency == 44100 && same,
                  $"A Zod's pick line {res}: {(clip ? clip.length.ToString("0.000") + " s, " + clip.channels + " ch, " + clip.frequency + " Hz" : "MISSING")} (the approved take {lens[side]:0.000} s), imported as his own lines {same}");
            }
            V(bytes > 0 && bytes < 128L * 1024 * 1024, $"A the faction screen's textures and meshes in memory while it shows: {bytes / 1048576f:0.0} MB (the editor's count, under 128 MB; none of it outside the screen: the Play proof counts that)");
        }

        // ================================================================================================================ B: the rules
        static string Rgb(Color c) => "(" + Mathf.RoundToInt(c.r * 255f) + ", " + Mathf.RoundToInt(c.g * 255f) + ", " + Mathf.RoundToInt(c.b * 255f) + ")";   // k75a2
        static void Rules()
        {
            V(OzFaction.General[0] == "GENERAL ZOD" && OzFaction.Company[0] == "ZODCORP" && OzFaction.General[1] == "GENERAL EVILZOD" && OzFaction.Company[1] == "EVIL TAKEOVER INDUSTRIES",
              $"B the posters' titles: {OzFaction.General[0]} / {OzFaction.Company[0]}, {OzFaction.General[1]} / {OzFaction.Company[1]}");
            string t0 = Plain(OzFaction.Tip(0)), t1 = Plain(OzFaction.Tip(1)), c0 = Plain(OzFaction.Caption(0)), c1 = Plain(OzFaction.Caption(1));
            V(t0 == "TIP: Wise choice!" && t1 == "TIP: You chose... poorly." && c0 == "ZOD: Wise choice!" && c1 == "ZOD: You chose... poorly.",
              $"B the tips \"{t0}\" / \"{t1}\" and the pick's words \"{c0}\" / \"{c1}\"");
            V(OzFaction.SideName(false) == "GENERAL ZOD" && OzFaction.SideName(true) == "GENERAL EVILZOD", "B the level selection's tag line names the side: " + OzFaction.SideName(false) + " / " + OzFaction.SideName(true));
            // the pick = the box's own setting (put back after)
            bool had = PlayerPrefs.HasKey(OzPlayBlue.Pref); int was = PlayerPrefs.GetInt(OzPlayBlue.Pref, 0);
            try
            {
                OzFaction.Choose(OzFaction.Blue, "counted check"); bool b1 = OzPlayBlue.Box && PlayerPrefs.GetInt(OzPlayBlue.Pref, 0) == 1 && OzFaction.Chosen == OzFaction.Blue;
                OzFaction.Choose(OzFaction.Red, "counted check"); bool r1 = !OzPlayBlue.Box && PlayerPrefs.GetInt(OzPlayBlue.Pref, 1) == 0 && OzFaction.Chosen == OzFaction.Red;
                V(b1 && r1, $"B a pick sets the PLAY AS BLUE box's own setting ({OzPlayBlue.Pref}): EVILZOD -> 1 {b1}, ZOD -> 0 {r1}; DEPLOY arms the mode from it (OzPlayBlue.Arm, unchanged)");
            }
            finally { if (had) PlayerPrefs.SetInt(OzPlayBlue.Pref, was); else PlayerPrefs.DeleteKey(OzPlayBlue.Pref); PlayerPrefs.Save(); }
            // mood
            float rx = 700f, bx = 1220f, mid = (rx + bx) * 0.5f;
            float m0 = OzFaction.MoodTarget(rx, rx, bx), m1 = OzFaction.MoodTarget(bx, rx, bx), mm = OzFaction.MoodTarget(mid, rx, bx), md = OzFaction.MoodTarget(mid + 20f, rx, bx);
            float far0 = OzFaction.MoodTarget(0f, rx, bx), far1 = OzFaction.MoodTarget(1920f, rx, bx);
            float maxStep = 0f, prev = OzFaction.MoodTarget(0f, rx, bx); bool mono = true;
            for (int i = 1; i <= 400; i++) { float v = OzFaction.MoodTarget(1920f * i / 400f, rx, bx); maxStep = Mathf.Max(maxStep, Mathf.Abs(v - prev)); if (v < prev - 1e-6f) mono = false; prev = v; }
            V(Mathf.Approximately(m0, -1f) && Mathf.Approximately(m1, 1f) && Mathf.Abs(mm) < 1e-6f && Mathf.Abs(md) < 1e-6f && Mathf.Approximately(far0, -1f) && Mathf.Approximately(far1, 1f) && mono && maxStep < 0.06f,
              $"B the mood's target: at Zod's middle {m0:0.00}, at EvilZod's {m1:0.00}, half way {mm:0.00} (20 u off {md:0.00}: the calm middle), the screen's edges {far0:0.00} / {far1:0.00}; across the screen in 400 steps it only rises, the largest step {maxStep:0.000}");
            // its ease: from red to blue at 30 and 60 frames a second
            foreach (float fps in new[] { 30f, 60f })
            {
                float v = -1f, vel = 0f, first = 0f, big = 0f, settle = -1f; bool over = false;
                for (int f = 1; f <= (int)(fps * 3f); f++)
                {
                    float nv = OzFaction.Damp(v, 1f, ref vel, OzFaction.MoodTime, 1f / fps);
                    float step = Mathf.Abs(nv - v); if (f == 1) first = step; big = Mathf.Max(big, step);
                    if (nv > 1.0001f) over = true;
                    v = nv; if (settle < 0f && Mathf.Abs(1f - v) < 0.05f) settle = f / fps;
                }
                V(first < 0.05f && big < (fps < 45f ? 0.2f : 0.1f) && !over && settle > 0f && settle < 1.5f,
                  $"B the mood eases red -> blue at {fps:0} fps: the first frame {first:0.000} (it starts gently), the largest step {big:0.000}, no overshoot {!over}, there in {settle:0.00} s");
            }
            // k75a2: the mood's look is subtle (the UI blends in linear light): the soft round glow, no wash over the posters
            {
                float glow0 = OzFaction.GlowAt(0f, 0f), glowEdgeX = OzFaction.GlowAt(OzFaction.GlowW * 0.5f, 0f), glowEdgeY = OzFaction.GlowAt(0f, OzFaction.GlowH * 0.5f);
                float glowStep = 0f, glowPrev = glow0; bool glowFalls = true;
                for (int gk = 1; gk <= 200; gk++) { float gv = OzFaction.GlowAt(OzFaction.GlowW * 0.5f * gk / 200f, 0f); glowStep = Mathf.Max(glowStep, Mathf.Abs(gv - glowPrev)); if (gv > glowPrev + 1e-6f) glowFalls = false; glowPrev = gv; }
                // the backdrop beside Zod's poster (120 u left of it, 337 u from its middle) at full red mood, as k75a drew it and as now
                Color backLin = ((Color)OzFaction.PaperBack).linear, redLin = ((Color)OzFaction.MoodRed).linear;
                float sideU = (466f - 120f) / 1920f, sideA = 1f - sideU; sideA = sideA * sideA * (3f - 2f * sideA);
                Color firstLook = Color.Lerp(Color.Lerp(Color.Lerp(backLin, redLin, 0.10f), redLin, 0.24f * sideA), redLin, 0.05f).gamma;      // k75a's first look: 10 % under, 24 % side glow, 5 % over
                Color newLook = Color.Lerp(Color.Lerp(backLin, redLin, OzFaction.WashUnder), redLin, OzFaction.Glow[OzFaction.Red] * OzFaction.GlowAt(-337f, 0f)).gamma;
                Color back0 = ((Color)OzFaction.PaperBack);
                float dFirst = Mathf.Max(Mathf.Abs(firstLook.r - back0.r), Mathf.Abs(firstLook.g - back0.g), Mathf.Abs(firstLook.b - back0.b)) * 255f, dNew = Mathf.Max(Mathf.Abs(newLook.r - back0.r), Mathf.Abs(newLook.g - back0.g), Mathf.Abs(newLook.b - back0.b)) * 255f;
                V(Mathf.Approximately(glow0, 1f) && glowEdgeX < 1e-6f && glowEdgeY < 1e-6f && glowFalls && glowStep < 0.03f && OzFaction.WashOver == 0f && OzFaction.WashUnder <= 0.01f
                  && OzFaction.Glow[0] <= 0.08f && OzFaction.Glow[1] <= 0.08f && dNew > 4f && dNew * 3f <= dFirst,
                  $"B the mood's look (k75a2): the round glow 1 at its middle, {glowEdgeX:0.000} / {glowEdgeY:0.000} at its edges, only falling, the largest step {glowStep:0.000} in 200; strengths: under {OzFaction.WashUnder:0.000}, glow {OzFaction.Glow[0]:0.000} / {OzFaction.Glow[1]:0.000}, over {OzFaction.WashOver:0.000}; "
                  + $"the backdrop {Rgb(back0)} beside Zod's poster at full red: k75a {Rgb(firstLook)} (+{dFirst:0}), now {Rgb(newLook)} (+{dNew:0}: a third of it or less, still seen)");
                Color paper0 = OzFaction.PaperBehind(0f, 0f), paperRed = OzFaction.PaperBehind(-1f, 1f), paperBlue = OzFaction.PaperBehind(1f, 1f), paperOther = OzFaction.PaperBehind(-1f, OzFaction.GlowAt(554f, 0f));
                Color printPaper = new Color(0.965f, 0.945f, 0.91f, 1f), printAged = Color.Lerp(printPaper.linear * OzFaction.PaperTone.linear, paper0.linear, OzFaction.PaperSee).gamma;
                V(OzFaction.PaperSee >= 0.1f && OzFaction.PaperSee <= 0.3f && Mathf.Abs(paper0.r - back0.r) < 0.004f && Mathf.Abs(paper0.g - back0.g) < 0.004f && Mathf.Abs(paper0.b - back0.b) < 0.004f
                  && paperRed.r > paper0.r + 0.02f && paperBlue.b > paper0.b + 0.02f && paperOther.r < paperRed.r && printAged.r < printPaper.r && printAged.r > 0.8f && printAged.b < printAged.r,
                  $"B the print's paper (k75a2): it takes {OzFaction.PaperSee:0.00} of the colour behind the poster: neutral {Rgb(paper0)} (the measured backdrop {Rgb(back0)}), at full red behind Zod's poster {Rgb(paperRed)}, at full blue behind EvilZod's {Rgb(paperBlue)}, "
                  + $"behind the other poster {Rgb(paperOther)}; the print's paper {Rgb(printPaper)} drawn as {Rgb(printAged)} on the neutral screen (aged, a little less bright)");
            }
            // the sway loop
            {
                float ymax = 0f, pmax = 0f, ystep = 0f, pstep = 0f; int turns = 0; float pdy = 0f;
                var a = OzFaction.Loop(0f, 0f);
                for (int f = 1; f <= 60 * 15; f++)
                {
                    var l = OzFaction.Loop(f / 60f, 0f);
                    ymax = Mathf.Max(ymax, Mathf.Abs(l.x)); pmax = Mathf.Max(pmax, Mathf.Abs(l.y));
                    float dy = l.x - a.x; ystep = Mathf.Max(ystep, Mathf.Abs(dy)); pstep = Mathf.Max(pstep, Mathf.Abs(l.y - a.y));
                    if (f > 1 && Mathf.Sign(dy) != Mathf.Sign(pdy) && Mathf.Abs(dy) > 1e-6f) turns++;
                    pdy = dy; a = l;
                }
                float wrap = (OzFaction.Loop(OzFaction.SwayPeriod * 3f + 1.234f, 0.37f) - OzFaction.Loop(1.234f, 0.37f)).magnitude;
                V(Mathf.Abs(ymax - OzFaction.SwayYaw) < 0.01f && Mathf.Abs(pmax - OzFaction.SwayPitch) < 0.01f && ystep < 0.06f && pstep < 0.03f && turns >= 3 && wrap < 1e-3f
                  && OzFaction.SwayPeriod >= 6f && OzFaction.SwayPeriod <= 8f,
                  $"B the sway loop: yaw +-{ymax:0.00} deg, pitch +-{pmax:0.00} deg, {OzFaction.SwayPeriod:0.0} s, the largest step at 60 fps {ystep:0.000} / {pstep:0.000} deg, {turns} turns in 15 s (forward and back), the same after 3 loops ({wrap:0.0000})");
            }
            // the pointer follow and the limits
            {
                var rnd = new System.Random(75); float ymax = 0f, pmax = 0f;
                for (int i = 0; i < 2000; i++)
                {
                    var loop = OzFaction.Loop((float)rnd.NextDouble() * 20f, (float)rnd.NextDouble());
                    var fol = new Vector2((float)rnd.NextDouble() * 2.4f - 1.2f, (float)rnd.NextDouble() * 2.4f - 1.2f);
                    var ang = OzFaction.Angles(loop, fol, (float)rnd.NextDouble());
                    ymax = Mathf.Max(ymax, Mathf.Abs(ang.x)); pmax = Mathf.Max(pmax, Mathf.Abs(ang.y));
                }
                var r = OzFaction.Angles(Vector2.zero, new Vector2(1f, 0f), 1f); var u = OzFaction.Angles(Vector2.zero, new Vector2(0f, 1f), 1f);
                V(ymax <= OzFaction.YawLimit + 1e-4f && pmax <= OzFaction.PitchLimit + 1e-4f && OzFaction.YawLimit <= 12f && OzFaction.PitchLimit <= 5f && Mathf.Abs(r.x - 8f) < 1e-3f && Mathf.Abs(u.y + 3f) < 1e-3f,
                  $"B the pointer follow: pointer right -> yaw {r.x:0.0}, pointer up -> pitch {u.y:0.0}; 2000 random states stay within yaw +-{ymax:0.00} / pitch +-{pmax:0.00} (limits {OzFaction.YawLimit} / {OzFaction.PitchLimit}; the light models are whole to 12 / 5)");
            }
            // the orbit
            for (int side = 0; side < 2; side++)
            {
                var c = OzFaction.Cam(side); float worst = 0f, dist = 0f; float d0 = (c.pivot - c.pos).magnitude;
                for (float y = -8.5f; y <= 8.51f; y += 1.7f)
                    for (float p = -3f; p <= 3.01f; p += 1f)
                    {
                        OzFaction.Orbit(c.pos, c.rot, c.pivot, y, p, out var pos, out var rot);
                        worst = Mathf.Max(worst, Vector3.Angle(rot * Vector3.forward, c.pivot - pos));
                        dist = Mathf.Max(dist, Mathf.Abs((c.pivot - pos).magnitude - d0));
                    }
                OzFaction.Orbit(c.pos, c.rot, c.pivot, 4f, 0f, out var pr, out _);
                OzFaction.Orbit(c.pos, c.rot, c.pivot, 0f, 2f, out var pd, out var rd);
                bool right = Vector3.Dot(pr - c.pos, c.rot * Vector3.right) > 0f, down = Vector3.Dot(pd - c.pos, c.rot * Vector3.up) < 0f, looksUp = (rd * Vector3.forward).y > (c.rot * Vector3.forward).y;
                V(worst < 0.01f && dist < 1e-4f && right && down && looksUp,
                  $"B {c.id}: the orbit keeps the pivot in the middle of the view (worst {worst:0.0000} deg) at its distance ({dist * 1000f:0.000} mm), yaw + moves right {right}, pitch + moves down {down} and looks up {looksUp}");
            }
            // the measured cameras: they look at their pivots with the measured up
            {
                var ups = new[] { new Vector3(-0.002526f, 0.99696f, -0.077873f), new Vector3(0.189784f, 0.981782f, -0.009256f) };
                for (int side = 0; side < 2; side++)
                {
                    var c = OzFaction.Cams[side];
                    float look = Vector3.Angle(c.rot * Vector3.forward, c.pivot - c.pos), up = Vector3.Angle(c.rot * Vector3.up, ups[side]);
                    V(look < 0.05f && up < 0.1f && Mathf.Abs(c.fov - 30.9428f) < 1e-3f, $"B {c.id}: the measured camera looks at its pivot ({look:0.000} deg) with the measured up ({up:0.000} deg), field of view {c.fov:0.00}");
                }
                V(!OzFaction.EvilUpright, "B the evil poster uses the measured camera (its -10.9 deg roll follows the owner's picture; the upright one is a switch: OzFaction.EvilUpright)");
            }
            // the layer slide
            {
                var b0 = OzFaction.LayerSlide(0f, 0f, true); var bl = OzFaction.LayerSlide(OzFaction.YawLimit, OzFaction.PitchLimit, true); var ml = OzFaction.LayerSlide(OzFaction.YawLimit, OzFaction.PitchLimit, false);
                var uv = OzFaction.SlideUv(bl);
                V(b0 == Vector2.zero && Mathf.Abs(bl.x - 8f) < 1e-3f && Mathf.Abs(ml.x - 4.5f) < 1e-3f && bl.y > ml.y && uv.x < 0f && uv.y > 0f && Mathf.Abs(uv.width - 1f) < 1e-6f,
                  $"B the print's back layers slide with the camera: back plate {bl.x:0.0} / {bl.y:0.0} px, mid layer {ml.x:0.0} / {ml.y:0.0} px at the limits (of the 1440 x 2560 grid; the frame stays), uv ({uv.x:0.0000}, {uv.y:0.0000})");
            }
        }

        static string Plain(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", "");

        // ================================================================================================================ C: the wiring
        static void Wiring()
        {
            var t = typeof(OpenZMainMenu);
            var view = t.GetNestedType("View", System.Reflection.BindingFlags.NonPublic);
            V(view != null && Enum.GetNames(view).Contains("Faction"), "C the main menu has a Faction view: " + (view != null ? string.Join(", ", Enum.GetNames(view)) : "no View type"));
            const System.Reflection.BindingFlags IF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var need = new[] { "GoFaction", "BackFromSelect", "TickFaction", "PickFaction", "FactionKeys", "TickFactionView", "BuildFaction" };
            var missing = need.Where(n => t.GetMethod(n, IF) == null).ToList();
            V(missing.Count == 0, "C the faction screen's methods: " + (missing.Count == 0 ? "all " + need.Length : "missing " + string.Join(", ", missing)));
            string src = File.ReadAllText(Path.Combine(Application.dataPath, "OpenZ/Scripts/UI/OpenZMainMenu.cs"));
            string pb = File.ReadAllText(Path.Combine(Application.dataPath, "OpenZ/Scripts/UI/OpenZMainMenu.PlayBlue.cs"));
            bool play = src.Contains("AddItem(\"PLAY\", \"SELECT FACTION\", () => GoFaction());"), back = src.Contains("AddHot(backBtn.Rt, View.Select, BackFromSelect,") && src.Contains("if (esc) BackFromSelect();");
            bool noKey = !src.Contains("TogglePlayBlue(true)") && !src.Contains("\"  PLAY AS BLUE     \""), noSwitch = !pb.Contains("Buttons/switch_track") && !pb.Contains("AddHot(pbHot");
            V(play && back && noKey && noSwitch, $"C PLAY opens the faction screen {play}; the level selection's BACK / ESC go back to it {back}; no PLAY AS BLUE key {noKey} or switch {noSwitch} left on the level selection");
        }

        // ================================================================================================================ D: the layout
        static void Layout()
        {
            var sizes = new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768), new Vector2Int(1280, 1024), new Vector2Int(1024, 768), new Vector2Int(2560, 1600), new Vector2Int(3440, 1440) };
            foreach (var s in sizes)
            {
                float cw = s.x * OzLayout.RefH / s.y, h = OzFaction.PosterH, w = Mathf.Round(h * OzFaction.PosterAspect);
                var gl = OzFactionDeck.Group(cw, OzFactionDeck.VsSign);                                          // k75a3: the posters apart, the group scaled to fit
                Rect p0 = gl.ToCanvas(gl.poster0), p1 = gl.ToCanvas(gl.poster1);
                float x0 = p0.xMin, right = p1.xMax, slotBottom = OzFaction.PosterTop + (h + OzFaction.TipGap + OzFaction.TipH) * gl.scale;
                float titleBottom = OzMenuLayout.SelTitleY + OzMenuLayout.SelTitleSize, hintBottom = OzMenuLayout.SelHintY + 14f;
                float backLeft = cw - (OzLayout.RefW - OzMenuLayout.Back.x);
                float glowTop = OzFaction.PosterTop - h * 0.0125f - OzMenuLayout.CardGlow * 0.5f;          // the focused poster grows 2.5 % about its middle
                bool ok = x0 >= 40f && right <= cw - 40f && OzFaction.PosterTop > titleBottom + 12f && OzFaction.PosterTop - h * 0.0125f > hintBottom && slotBottom <= OzLayout.RefH - 24f;
                V(ok, $"D {s.x} x {s.y} (canvas {cw:0} x 1080 u): posters {x0:0}..{right:0} u ({w * gl.scale:0} x {h * gl.scale:0} u each, {p1.xMin - p0.xMax:0} u apart, the group at {gl.scale * 100f:0} %), top {OzFaction.PosterTop:0} u under the title ({titleBottom:0} u) and the keys row ({hintBottom:0} u), the tip slots end at {slotBottom:0} u; the BACK button from {backLeft:0} u (over the posters' top: none, it ends at 144 u); the glow from {glowTop:0} u");
            }
        }
    }
}
#endif
