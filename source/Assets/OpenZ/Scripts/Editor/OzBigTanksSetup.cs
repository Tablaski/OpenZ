#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The MAMMOTH and the BIZON (1.5, DV180 / DV181; View/OzBigTanks.cs), the editor side. Menus OpenZ/1.5/Big Tanks/:
    ///  1 Build the prefabs: Resources/OpenZ/BigTanks/Z_mammoth and Z_bizon from Models/BigTanks/{x}_game.glb (game metres, Model scale 1), made like the
    ///    medium tank's (root + TeamPaint copied from Prefabs/Z_light, the GLB's TankRoot as "Model"); TeamLit materials Materials/Units/{X}V2_body /
    ///    _head / _track / _well cloned from the medium tank's with the tank's own textures (Models/BigTanks/textures/{x}_{body,head,track}_{albedo,normal}.png,
    ///    imported as the medium tank's); the REAL track links baked into OzTrackLinks from Models/BigTanks/{x}_track_paths.json (the pitch line of each
    ///    side, every link's rest pose on it, the corner wheels' radii, the half gauge, the planted box); the Bizon's exhaust (OzShuttleExhaust on
    ///    MK_Exhaust_L/_R + OzBigTankExhaust); the wreck set OZ_WreckPieces kept inactive.
    ///  2 Check the prefabs (counted, no Play): every node the code reads, the links on their loop, the token rule, the wreck set, the markers, the size
    ///    against the heavy tank v2 (x1.2 / x1.3), materials, pictures, names, voice pools, the Unit editor lists.
    ///  3 Write the lamp data: their vehicle_lights.json entries from the LT_* markers (the heavy tank's entry as the pattern).
    ///  4 Rules check (counted, headless): Editor/OzV15BigTanksCount.cs.
    /// </summary>
    public static class OzBigTanksSetup
    {
        const string M = "OpenZ/1.5/Big Tanks/";
        public const string Models = "Assets/OpenZ/Models/BigTanks/", Folder = "Assets/OpenZ/Resources/OpenZ/BigTanks", Mats = "Assets/OpenZ/Materials/Units/",
                            Template = "Assets/OpenZ/Prefabs/Z_light.prefab", MediumMats = "Assets/OpenZ/Materials/Units/mtank_v2_";
        static readonly string ProofDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/kits/new_tanks/unity_proof"));
        static readonly (string type, string Name, OzUnitModels models, float size)[] Tanks =
        {
            (ZBigTanks.Mammoth, "Mammoth", OzBigTanks.MammothModels, 1.2f),
            (ZBigTanks.Bizon, "Bizon", OzBigTanks.BizonModels, 1.3f),
        };

        static Transform Deep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Deep(c, name); if (f) return f; }
            return null;
        }
        static void Layer(Transform t, int layer) { t.gameObject.layer = layer; foreach (Transform c in t) Layer(c, layer); }
        static void Report(string file, string text) { Directory.CreateDirectory(ProofDir); File.WriteAllText(Path.Combine(ProofDir, file), text + "\n"); Debug.Log("[OzBigTanks] " + file + ":\n" + text); }

        // ================================================================ 1 build
        [MenuItem(M + "1 Build the prefabs", false, 1)]
        public static void Build()
        {
            var sb = new StringBuilder();
            foreach (var t in Tanks) sb.AppendLine(t.type + ": " + BuildOne(t.type, t.Name, t.models));
            AssetDatabase.SaveAssets();
            Report("build_report.txt", sb.ToString());
        }

        public static string BuildOne(string type, string Name, OzUnitModels models)
        {
            string glbPath = Models + type + "_game.glb", jsonPath = Models + type + "_track_paths.json";
            var glb = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            var tpl = AssetDatabase.LoadAssetAtPath<GameObject>(Template);
            if (!glb) return "FAILED: no " + glbPath + " (copy the Blender worker's game GLB there and let it import)";
            if (!tpl) return "FAILED: no " + Template;
            if (!File.Exists(jsonPath)) return "FAILED: no " + jsonPath + " (the Blender worker's track paths)";
            var mats = MakeMaterials(type, Name, out string matNote);
            if (mats == null) return "FAILED: " + matNote;
            var tplModel = tpl.transform.Find("Model");
            var tplHull = Deep(tpl.transform, "Tank_hull"); var tplMr = tplHull ? tplHull.GetComponent<MeshRenderer>() : null;
            if (!tplModel || !tplMr) return "FAILED: Z_light has no Model / Tank_hull renderer";
            var sb = new StringBuilder(matNote).Append("; ");
            var inst = UnityEngine.Object.Instantiate(glb);
            GameObject root = null;
            try
            {
                var susp = Deep(inst.transform, "TankSuspension");
                if (!susp || !susp.parent) return "FAILED: the GLB has no TankSuspension under its TankRoot";
                var model = susp.parent;
                root = new GameObject(type == ZBigTanks.Mammoth ? OzBigTanks.MammothKey : OzBigTanks.BizonKey) { layer = tpl.layer };
                var tp = root.AddComponent<TeamPaint>(); var tp1 = tpl.GetComponent<TeamPaint>(); if (tp1) EditorUtility.CopySerialized(tp1, tp);
                model.SetParent(root.transform, false); model.name = "Model";
                model.localPosition = Vector3.zero; model.localRotation = tplModel.localRotation; model.localScale = Vector3.one;   // game metres in the GLB (1.2 / 1.3 x the heavy tank v2)
                int n = 0, byName = 0; var unknown = new List<string>();
                foreach (var mr in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var src = mr.sharedMaterials; var all = new Material[src.Length];
                    for (int i = 0; i < src.Length; i++)
                    {
                        string mn = src[i] ? src[i].name : ""; int k = mn.IndexOf(' '); if (k > 0) mn = mn.Substring(0, k);
                        if (mats.TryGetValue(mn, out var m)) { all[i] = m; byName++; continue; }
                        string nm = mr.name.ToLowerInvariant();
                        all[i] = nm.Contains("link") || nm.Contains("sprocket") || nm.Contains("idler") || nm.Contains("guide") || nm.Contains("wheel") || nm.Contains("sponson") ? mats[Name + "V2_track"]
                               : nm.Contains("hull") || nm.Contains("plate") || nm.Contains("pipe") || nm.Contains("ladder") ? mats[Name + "V2_body"] : mats[Name + "V2_head"];
                        unknown.Add(mr.name + "[" + i + "] '" + (src[i] ? src[i].name : "none") + "'");
                    }
                    mr.sharedMaterials = all;
                    mr.shadowCastingMode = tplMr.shadowCastingMode; mr.receiveShadows = tplMr.receiveShadows;
                    mr.lightProbeUsage = tplMr.lightProbeUsage; mr.reflectionProbeUsage = tplMr.reflectionProbeUsage;
                    mr.motionVectorGenerationMode = tplMr.motionVectorGenerationMode; mr.allowOcclusionWhenDynamic = tplMr.allowOcclusionWhenDynamic;
                    n++;
                }
                foreach (var c in model.GetComponentsInChildren<Component>(true))
                    if (!(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer)) UnityEngine.Object.DestroyImmediate(c);
                Layer(model, tplModel.gameObject.layer);
                sb.Append($"{n} renderers, {byName} material slots by the GLB's names{(unknown.Count > 0 ? " (by renderer name: " + string.Join(", ", unknown.Take(12)) + (unknown.Count > 12 ? " ..." : "") + ")" : "")}; ");
                // the wreck set stays hidden until the break
                var set = Deep(model, OzBigTankWreck.SetNode);
                if (set)
                {
                    set.gameObject.SetActive(false);
                    int renamed = 0;
                    foreach (var t in set.GetComponentsInChildren<Transform>(true))
                        if (t != set && OzBigTankWreck.LauncherName(t.name)) { t.name = OzBigTankWreck.PlainName(t.name); renamed++; }   // "Sprocket" holds "rocket": WreckPieces would throw it like a launcher
                    sb.Append($"wreck set {set.GetComponentsInChildren<MeshRenderer>(true).Length} pieces (inactive{(renamed > 0 ? ", " + renamed + " renamed off the launcher words" : "")}); ");
                }
                else sb.Append("NO wreck set " + OzBigTankWreck.SetNode + " (it breaks the old way until the model has one); ");
                int brkSets = 0;                                                                            // 1.5 big_tanks_wreck_finer: the turret's and the barrels' break sets stay hidden until the break
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                    if (t.name == OzBigTankWreck.TurretSet || t.name.StartsWith(OzBigTankWreck.BarrelSet)) { t.gameObject.SetActive(false); brkSets++; }
                sb.Append($"{brkSets} turret / barrel break sets (inactive); ");
                { var cp = OzBigTankCheekPlates.Add(model, type); if (cp.Length > 0) sb.Append(cp).Append("; "); }   // 1.5 last pass 14j R13: the Mammoth's mantlet slots closed (Editor/OzBigTankCheekPlates.cs)
                // the real links
                var links = root.AddComponent<OzTrackLinks>();
                sb.Append(BakeLinks(links, root.transform, model, File.ReadAllText(jsonPath), type)).Append("; ");                   // 1.5 track_dirt_variation: + the tank (its wear JSON)
                // the Bizon's exhaust
                var ml = Deep(model, "MK_Exhaust_L"); var mrk = Deep(model, "MK_Exhaust_R");
                if (ml && mrk)
                {
                    var ex = root.AddComponent<OzShuttleExhaust>(); ex.mouths = new[] { ml, mrk }; ex.vents = new Transform[0];
                    root.AddComponent<OzBigTankExhaust>();
                    sb.Append("exhaust on MK_Exhaust_L / _R; ");
                }
                else if (type == ZBigTanks.Bizon) sb.Append("NO MK_Exhaust_L / _R (the Bizon must smoke); ");
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/OpenZ/Resources/OpenZ", "BigTanks");
                string path = Folder + "/" + root.name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                if (!ok) return "FAILED: the prefab could not be saved at " + path;
                sb.Append("saved ").Append(path);
            }
            finally
            {
                if (root) UnityEngine.Object.DestroyImmediate(root);
                if (inst) UnityEngine.Object.DestroyImmediate(inst);
            }
            models.Forget();
            return sb.ToString();
        }

        /// <summary>{X}V2_body / _head / _track / _well: clones of the medium tank's TeamLit materials (its _HeavyLook, dark colours, paint-from-alpha
        /// and normal map switches) with this tank's own textures (imported like the medium tank's).</summary>
        /// <summary>
        /// read relative to it for the team colour and the empty grey). The clones kept the medium tank's 0.2224 while the big tanks' own paint measures
        /// ~0.127, so their neutral body drew ~13 % (Bizon) / ~28 % (Mammoth) darker than the neutral light / medium tank beside them; with their own value
        /// the Bizon matches the siblings (1.02 x) and the Mammoth is much closer (0.82 x, its olive camo is darker). UE line-up frames:
        /// owner_frames/pass5/06_*.</summary>
        public static float BizonPaintRefLum = 0.1266f, MammothPaintRefLum = 0.1255f;
        static float PaintRefLum(string type) => type == OzBigTanks.Bizon ? BizonPaintRefLum : MammothPaintRefLum;

        static Dictionary<string, Material> MakeMaterials(string type, string Name, out string note)
        {
            var d = new Dictionary<string, Material>(); var sb = new StringBuilder("materials: ");
            foreach (var part in new[] { "body", "head", "track", "well" })
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>(MediumMats + part + ".mat");
                if (!src) { note = "no " + MediumMats + part + ".mat (the pattern)"; return null; }
                string path = Mats + Name + "V2_" + part + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m) { m = new Material(src) { name = Name + "V2_" + part }; AssetDatabase.CreateAsset(m, path); }
                else m.CopyPropertiesFromMaterial(src);
                string texPart = part == "well" ? "head" : part;
                Texture2D alb = Tex(Models + "textures/" + type + "_" + texPart + "_albedo.png", false), nrm = Tex(Models + "textures/" + type + "_" + texPart + "_normal.png", true);
                int set = 0;
                foreach (var prop in m.GetTexturePropertyNames())
                {
                    var t = m.GetTexture(prop); if (!t) continue;
                    bool normal = prop.Contains("Bump") || prop.Contains("Normal") || t.name.EndsWith("_normal", StringComparison.OrdinalIgnoreCase) || t.name.Contains("nrm");
                    if (normal && nrm) { m.SetTexture(prop, nrm); set++; } else if (!normal && alb && part != "well") { m.SetTexture(prop, alb); set++; }
                }
                if (m.HasProperty("_HeavyLook")) { var hl = m.GetVector("_HeavyLook"); hl.x = PaintRefLum(type); m.SetVector("_HeavyLook", hl); }   // 1.5 pass 5: this tank's own paint (was the medium tank's 0.2224)
                if (part == "track") sb.Append(WearTiling(m, type, alb));                                       // 1.5 track_dirt_variation: the 2-tile wear atlas (old UVs read the left tile)
                EditorUtility.SetDirty(m);
                d[Name + "V2_" + part] = m;
                sb.Append($"{Name}V2_{part} ({set} textures{(alb ? "" : ", NO albedo")}{(nrm ? "" : ", NO normal")}) ");
            }
            note = sb.ToString().TrimEnd();
            return d;
        }

        /// <summary>A texture of the tank imported like the medium tank's (sRGB, alpha from input, alpha is no transparency, mips, aniso 8, high quality).</summary>
        static Texture2D Tex(string path, bool normal)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return null;
            bool dirty = false;
            if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
            if (!normal && (ti.textureType != TextureImporterType.Default || !ti.sRGBTexture || ti.alphaIsTransparency || ti.alphaSource != TextureImporterAlphaSource.FromInput))
            { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.alphaIsTransparency = false; ti.alphaSource = TextureImporterAlphaSource.FromInput; dirty = true; }
            if (!ti.mipmapEnabled || ti.anisoLevel < 8 || ti.textureCompression != TextureImporterCompression.CompressedHQ || ti.maxTextureSize < 4096)
            { ti.mipmapEnabled = true; ti.anisoLevel = 8; ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.maxTextureSize = 4096; dirty = true; }
            if (dirty) ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------------------------------------------------------------- the links
        /// <summary>
        /// links per side, half gauge, the corner wheels with their radii).</summary>
        public static string BakeLinks(OzTrackLinks k, Transform root, Transform model, string json, string wearType = null)   // 1.5 track_dirt_variation: wearType = the tank (bizon / mammoth)
        {
            var j = MiniJson.Parse(json) as Dictionary<string, object>;
            if (j == null || !(j.TryGetValue("sides", out var so) && so is Dictionary<string, object> sides)) return "links FAILED: the JSON has no sides";
            float pitch = F(j, "pitch"), rim = F(j, "wheel_rim_radius"), pitchR = F(j, "pitch_radius_corner_wheels");
            int per = (int)F(j, "links_per_side");
            k.pitch = pitch; k.source = "track_paths.json: pitch " + pitch.ToString("0.000", CultureInfo.InvariantCulture) + " m, " + per + " links a side";
            var list = new List<OzTrackLinks.Side>(); var sb = new StringBuilder("links: ");
            float gaugeSum = 0f; int gaugeN = 0;
            var wear = wearType != null && (TrackWear || TrackJitter) ? WearLoad(wearType, root) : null;                         // 1.5 track_dirt_variation (null: the old clean links)
            foreach (var key in new[] { "L", "R" })
            {
                if (!(sides.TryGetValue(key, out var sv) && sv is Dictionary<string, object> s)) { sb.Append("side " + key + " missing; "); continue; }
                var sideRoot = Deep(model, key == "L" ? "Track_Left" : "Track_Right");
                var linkNames = (s.TryGetValue("links", out var ln) ? ln as List<object> : null)?.Select(x => x as string).Where(x => x != null).ToList() ?? new List<string>();
                var linkTs = linkNames.Select(x => Deep(model, x)).Where(x => x).ToList();
                if (!sideRoot || linkTs.Count == 0) { sb.Append($"side {key}: no {(sideRoot ? "links" : "side root")}; "); continue; }
                var space = linkTs[0].parent;
                var pts = new List<Vector3>();
                foreach (var po in (s.TryGetValue("points_unity", out var pu) ? pu as List<object> : null) ?? new List<object>())
                    if (po is List<object> c && c.Count >= 3) pts.Add(space.InverseTransformPoint(model.TransformPoint(new Vector3(D(c[0]), D(c[1]), D(c[2])))));
                if (pts.Count < 8) { sb.Append($"side {key}: {pts.Count} path points; "); continue; }
                if (Vector3.Distance(pts[0], pts[pts.Count - 1]) < 1e-4f) pts.RemoveAt(pts.Count - 1);     // a closing duplicate
                // the top run must run toward the front (-Z) with growing arc: else the line is turned round
                float my = pts.Average(p => p.y), dz = 0f;
                for (int i = 0; i < pts.Count; i++) { var a = pts[i]; var b = pts[(i + 1) % pts.Count]; if (a.y > my && b.y > my) dz += root.InverseTransformDirection(space.TransformDirection(b - a)).z; }   // in the prefab root's space (front = -Z)
                bool turned = dz > 0f; if (turned) pts.Reverse();
                var side = new OzTrackLinks.Side { name = key, root = sideRoot, space = space, path = pts.ToArray() };
                side.arc = OzTrackLinks.Arcs(side.path, out side.length);
                side.sign = Mathf.Sign(root.InverseTransformPoint(space.TransformPoint(pts.Aggregate(Vector3.zero, (a, b) => a + b) / pts.Count)).x);
                gaugeSum += Mathf.Abs(root.InverseTransformPoint(space.TransformPoint(pts[0])).x); gaugeN++;
                side.links = linkTs.ToArray(); side.linkArc = new float[linkTs.Count]; side.linkOffset = new Vector3[linkTs.Count]; side.linkRot = new Quaternion[linkTs.Count];
                float worst = 0f;
                for (int i = 0; i < linkTs.Count; i++)
                {
                    var t = linkTs[i];
                    var lp = space.InverseTransformPoint(t.position);
                    float a0 = OzTrackLinks.NearestArc(side, lp);
                    OzTrackLinks.Frame(side, a0, out var p, out var q);
                    side.linkArc[i] = a0; side.linkOffset[i] = Quaternion.Inverse(q) * (lp - p); side.linkRot[i] = Quaternion.Inverse(q) * (Quaternion.Inverse(space.rotation) * t.rotation);
                    worst = Mathf.Max(worst, side.linkOffset[i].magnitude);
                    if (t.parent != space) t.SetParent(space, true);
                }
                if (wear != null) sb.Append(WearApply(wear, side, linkTs, key == "L" ? 0 : 1));                                   // 1.5 track_dirt_variation: variant + variance, then the baked place
                var gear = new List<Transform>(); var radius = new List<float>(); var rest = new List<Quaternion>();
                foreach (var wo in (s.TryGetValue("wheels", out var ws) ? ws as List<object> : null) ?? new List<object>())
                {
                    if (!(wo is Dictionary<string, object> w)) continue;
                    var node = Deep(model, w.TryGetValue("node", out var nn) ? nn as string : "");
                    if (!node) continue;
                    string kind = w.TryGetValue("kind", out var kk) ? kk as string : "";
                    // every corner wheel (sprocket, idler, guide) is WRAPPED by the loop at its pitch radius (blender/CONTRACT.md: the loop runs
                    // 0.55 / 0.50 m from each axle), so it turns by d / pitch radius like the links over it; only a wheel without one goes by its rim
                    float r = w.ContainsKey("pitch_radius") ? F(w, "pitch_radius") : pitchR > 0f ? pitchR : (w.ContainsKey("rim_radius") ? F(w, "rim_radius") : rim);
                    gear.Add(node); radius.Add(Mathf.Max(0.05f, r)); rest.Add(node.localRotation);
                }
                side.gear = gear.ToArray(); side.gearRadius = radius.ToArray(); side.gearRest = rest.ToArray();
                Vector3 lo = Vector3.one * 1e9f, hi = -Vector3.one * 1e9f;
                foreach (var t in linkTs)
                {
                    var mf = t.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                    var bb = mf.sharedMesh.bounds;
                    for (int c = 0; c < 8; c++)
                    {
                        var wp = t.TransformPoint(bb.center + Vector3.Scale(bb.extents, new Vector3((c & 1) * 2 - 1, ((c >> 1) & 1) * 2 - 1, ((c >> 2) & 1) * 2 - 1)));
                        var lp2 = sideRoot.InverseTransformPoint(wp); lo = Vector3.Min(lo, lp2); hi = Vector3.Max(hi, lp2);
                    }
                }
                side.boxLo = lo; side.boxHi = hi;
                list.Add(side);
                float jl = F(s, "loop_length"); if (jl <= 0f) jl = F(j, "loop_length");
                sb.Append($"{key}: {linkTs.Count} links (want {per}), loop {side.length:0.000} m (JSON {jl:0.000}, N x pitch {per * pitch:0.000}){(turned ? ", line turned round" : "")}, links off the line at most {worst * 100f:0.0} cm, {gear.Count} corner wheels; ");
            }
            k.sides = list.ToArray();
            float hg = F(j, "half_gauge");
            k.halfGauge = gaugeN > 0 ? gaugeSum / gaugeN : hg;
            if (wear != null) sb.Append(wear.note);                                                                                // 1.5 track_dirt_variation
            sb.Append($"half gauge {k.halfGauge:0.000} m (JSON {hg:0.000}); valid {k.Valid}");
            return sb.ToString();
        }
        /// <summary>
        /// The track atlas {x}_track_albedo / _normal.png is 2 tiles wide: left = the old atlas (every old UV reads it
        /// through the material's tiling 0.5 x 1), right = 4 subtle wear variants of the link (dust / dried mud in the groove, a few hairline cracks,
        /// chipped edges, a slight tone shift), described by Models/BigTanks/{x}_track_wear.json (per UV island its box and its shift per variant).
        /// The bake gives every link one variant by a fixed hash of (tank, side, index), never the same as the link before it: a copy of the link
        /// mesh with its UV islands moved (one asset Resources/OpenZ/BigTanks/Z_{x}_track_wear.asset, the same material, batching as before), and a
        /// fixed tiny variance that rides with the link round the whole loop (OzTrackLinks.Step rewrites only its place and turn: the scale stays,
        /// the turn is baked into linkRot, the shift into linkArc): along the loop +-WearArc m, tilt about its axle +-WearTilt deg, twist in the belt
        /// +-WearTwist deg, size +-WearSize (width, thickness, length). Proven off Unity over whole loops, forward and reverse (the kit's
        /// proof/gap_table.txt: neighbour links never touch, no new hole, the wheel wrap and the ground line as before).
        /// TrackWear false = the old clean links (the tiling is still set: the old UVs read the left tile); TrackJitter false = no variance.</summary>
        public static bool TrackWear = true, TrackJitter = true;
        public static uint WearSeed = 0x7A11u;
        public static float WearArc = 0.004f, WearTilt = 1.5f, WearTwist = 1.2f;
        public static Vector3 WearSize = new Vector3(0.03f, 0.015f, 0.02f);
        const float WearEps = 0.25f / 1024f;

        sealed class WearData
        {
            public Vector2 tiling = Vector2.one; public int n; public uint seed; public string asset, note = "";
            public readonly List<Vector4> rect = new List<Vector4>(); public readonly List<Vector2[]> shift = new List<Vector2[]>();
            public readonly Dictionary<Mesh, Mesh[]> made = new Dictionary<Mesh, Mesh[]>();
        }

        static uint WMix(uint x) { unchecked { x ^= x >> 16; x *= 0x7FEB352Du; x ^= x >> 15; x *= 0x846CA68Bu; x ^= x >> 16; return x; } }
        /// <summary>A fixed number in 0..1 for (side, link, channel).</summary>
        static float WU(uint seed, int side, int i, int ch) { unchecked { return (WMix(WMix(WMix(seed ^ (uint)ch * 0x9E3779B9u) + (uint)side * 0x85EBCA6Bu) + (uint)i) & 0xFFFFFFu) / 16777215f; } }
        /// <summary>A fixed number in -1..1 (two draws: most links near 0).</summary>
        static float WJ(uint seed, int side, int i, int ch) => WU(seed, side, i, 2 * ch) + WU(seed, side, i, 2 * ch + 1) - 1f;

        static Vector2 WearTilingOf(Dictionary<string, object> j)
            => j != null && j.TryGetValue("tiling", out var to) && to is List<object> tl && tl.Count == 2 ? new Vector2(D(tl[0]), D(tl[1])) : Vector2.one;

        /// <summary>The tank's wear data; null (the old clean links) when it has no wear JSON or its albedo is not the JSON's 2-tile atlas.</summary>
        static WearData WearLoad(string type, Transform root)
        {
            string path = Models + type + "_track_wear.json";
            if (!File.Exists(path)) return null;
            var j = MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
            if (j == null) return null;
            var w = new WearData { n = (int)F(j, "variants"), tiling = WearTilingOf(j), asset = Folder + "/" + root.name + "_track_wear.asset",
                                   seed = WearSeed ^ (type == ZBigTanks.Bizon ? 0xB1201u : 0x3A307u) };
            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>(Models + "textures/" + type + "_track_albedo.png");
            if (!alb || Mathf.Abs(alb.width * w.tiling.x - alb.height * w.tiling.y) > 0.5f) { Debug.LogWarning("[OzBigTanks] track wear: " + type + "_track_albedo.png is not the wear JSON's atlas: the old clean links"); return null; }
            foreach (var go in (j.TryGetValue("groups", out var gs) ? gs as List<object> : null) ?? new List<object>())
            {
                if (!(go is Dictionary<string, object> g) || !(g.TryGetValue("rect", out var ro) && ro is List<object> r && r.Count == 4)) continue;
                var sh = (g.TryGetValue("shift", out var so) ? so as List<object> : null) ?? new List<object>();
                if (sh.Count != w.n) return null;
                w.rect.Add(new Vector4(D(r[0]), D(r[1]), D(r[2]), D(r[3])));
                w.shift.Add(sh.Select(o => o is List<object> p && p.Count == 2 ? new Vector2(D(p[0]), D(p[1])) : Vector2.zero).ToArray());
            }
            return w.n >= 2 && w.rect.Count > 0 ? w : null;
        }

        /// <summary>MakeMaterials: the track material's tiling for the 2-tile wear atlas (the old UVs read the left tile); "" without a wear JSON.</summary>
        static string WearTiling(Material m, string type, Texture2D alb)
        {
            string path = Models + type + "_track_wear.json";
            if (!m.HasProperty("_BaseMap") || !File.Exists(path)) return "";
            var t = WearTilingOf(MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>);
            if (!alb || Mathf.Abs(alb.width * t.x - alb.height * t.y) > 0.5f) return $"[NO wear tiling: the albedo is {(alb ? alb.width + " x " + alb.height : "missing")}, the wear JSON wants tiling {t.x} x {t.y}] ";
            m.SetTextureScale("_BaseMap", t); m.SetTextureOffset("_BaseMap", Vector2.zero);
            return $"[tiling {t.x:0.##} x {t.y:0.##}: the 2-tile wear atlas] ";
        }

        /// <summary>The wear variants of a link mesh: copies whose UV island k is moved by shift[k][variant] (a vertex belongs to the island whose box
        /// holds its uv); stored in the tank's one wear asset (made again on every build; the prefab is saved right after, pointing at them).</summary>
        static Mesh[] WearMeshes(WearData w, Mesh src)
        {
            var uv = src.uv; var grp = new int[uv.Length];
            for (int j = 0; j < uv.Length; j++)
            {
                grp[j] = -1;
                for (int g = 0; g < w.rect.Count && grp[j] < 0; g++)
                {
                    var r = w.rect[g];
                    if (uv[j].x >= r.x - WearEps && uv[j].x <= r.z + WearEps && uv[j].y >= r.y - WearEps && uv[j].y <= r.w + WearEps) grp[j] = g;
                }
                if (grp[j] < 0) { w.note += $"NO wear on {src.name}: its uv {uv[j].x:0.0000}, {uv[j].y:0.0000} is in no island box of the wear JSON (the model changed?); "; return null; }
            }
            var ms = new Mesh[w.n];
            for (int v = 0; v < w.n; v++)
            {
                var m = UnityEngine.Object.Instantiate(src); m.name = src.name + "_wear" + v;
                var u2 = new Vector2[uv.Length];
                for (int j = 0; j < uv.Length; j++) u2[j] = uv[j] + w.shift[grp[j]][v];
                m.uv = u2; ms[v] = m;
            }
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/OpenZ/Resources/OpenZ", "BigTanks");
            int from = 0;
            if (w.made.Count == 0)
            {
                if (AssetDatabase.LoadMainAssetAtPath(w.asset)) AssetDatabase.DeleteAsset(w.asset);
                AssetDatabase.CreateAsset(ms[0], w.asset); from = 1;
            }
            for (int v = from; v < w.n; v++) AssetDatabase.AddObjectToAsset(ms[v], w.asset);
            w.note += $"{w.n} wear meshes of {src.name} in {w.asset}; ";
            return ms;
        }

        /// <summary>One side: every link gets its wear variant (mesh) and its fixed variance (arc, tilt, twist, size), then stands on its baked place
        /// (as OzTrackLinks.Step puts it at belt travel 0).</summary>
        static string WearApply(WearData w, OzTrackLinks.Side side, List<Transform> linkTs, int s)
        {
            int n = linkTs.Count; var v = new int[n];
            unchecked
            {
                for (int i = 0; i < n; i++)
                {
                    uint h = WMix(WMix(w.seed ^ 0x5bd1e995u) + (uint)s * 131u + (uint)i);
                    v[i] = i == 0 ? (int)(h % (uint)w.n) : (int)((v[i - 1] + 1 + h % (uint)(w.n - 1)) % w.n);       // never the variant of the link before
                }
            }
            if (n > 2 && v[n - 1] == v[0]) for (int c = 0; c < w.n; c++) if (c != v[0] && c != v[n - 2]) { v[n - 1] = c; break; }   // nor round the loop's join
            int meshed = 0;
            for (int i = 0; i < n; i++)
            {
                var t = linkTs[i]; var mf = t.GetComponent<MeshFilter>();
                if (TrackWear && mf && mf.sharedMesh)
                {
                    var src = mf.sharedMesh;                                                                                  // the GLB's link mesh (both sides share it)
                    if (!w.made.TryGetValue(src, out var ms)) { ms = WearMeshes(w, src); w.made[src] = ms; }                   // null: no wear (the note says why)
                    if (ms != null) { mf.sharedMesh = ms[v[i]]; meshed++; }
                }
                if (TrackJitter)
                {
                    side.linkArc[i] += WJ(w.seed, s, i, 0) * WearArc;
                    side.linkRot[i] = side.linkRot[i] * Quaternion.AngleAxis(WJ(w.seed, s, i, 1) * WearTilt, Vector3.right) * Quaternion.AngleAxis(WJ(w.seed, s, i, 2) * WearTwist, Vector3.up);
                    t.localScale = Vector3.Scale(t.localScale, new Vector3(1f + WJ(w.seed, s, i, 3) * WearSize.x, 1f + WJ(w.seed, s, i, 4) * WearSize.y, 1f + WJ(w.seed, s, i, 5) * WearSize.z));
                }
                OzTrackLinks.Frame(side, side.linkArc[i], out var p, out var q);
                t.SetLocalPositionAndRotation(p + q * side.linkOffset[i], q * side.linkRot[i]);
            }
            return $"wear {side.name}: {meshed} of {n} links on {w.n} variants ({string.Join("", v)}){(TrackJitter ? $", variance arc +-{WearArc * 1000f:0.#} mm, tilt +-{WearTilt:0.#} deg, twist +-{WearTwist:0.#} deg, size +-{WearSize.x * 100f:0.#} / {WearSize.y * 100f:0.#} / {WearSize.z * 100f:0.#} %" : "")}; ";
        }

        /// <summary>Check menu: the wear links of a built prefab (counted).</summary>
        static void WearCheck(Transform r, string x, Action<bool, string> C)
        {
            var links = r.GetComponent<OzTrackLinks>();
            if (!links || !links.Valid || !File.Exists(Models + x + "_track_wear.json")) return;
            var meshes = new HashSet<Mesh>(); int n = 0, same = 0; float lo = 9f, hi = 0f; Material mat = null;
            foreach (var s in links.sides)
                for (int i = 0; i < s.links.Length; i++)
                {
                    var t = s.links[i]; var nx = s.links[(i + 1) % s.links.Length]; if (!t || !nx) continue;
                    var mf = t.GetComponent<MeshFilter>(); var mf2 = nx.GetComponent<MeshFilter>();
                    var m = mf ? mf.sharedMesh : null; var m2 = mf2 ? mf2.sharedMesh : null;
                    if (m) meshes.Add(m);
                    if (m && m == m2) same++;
                    n++;
                    var sc = t.localScale; lo = Mathf.Min(lo, Mathf.Min(sc.x, Mathf.Min(sc.y, sc.z))); hi = Mathf.Max(hi, Mathf.Max(sc.x, Mathf.Max(sc.y, sc.z)));
                    var mr = t.GetComponent<MeshRenderer>(); if (!mat && mr) mat = mr.sharedMaterial;
                }
            if (TrackWear) C(meshes.Count >= 3 && meshes.All(m => m.name.Contains("_wear")) && same == 0, $"{x}: track wear: {n} links on {meshes.Count} wear meshes ({string.Join(", ", meshes.Select(m => m.name))}), {same} next to a link with the same one");
            if (TrackJitter) C(lo > 0.95f && hi < 1.05f && hi > 1.001f, $"{x}: link size variance {(lo - 1f) * 100f:0.0} .. +{(hi - 1f) * 100f:0.0} % (fixed per link)");
            var tex = mat && mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
            var st = mat && mat.HasProperty("_BaseMap") ? mat.GetTextureScale("_BaseMap") : Vector2.one;
            C(tex && tex.width == 2 * tex.height && Mathf.Abs(st.x - 0.5f) < 1e-4f && Mathf.Abs(st.y - 1f) < 1e-4f,
              $"{x}: track atlas {(tex ? tex.width + " x " + tex.height : "none")}, tiling {st.x:0.##} x {st.y:0.##} (the old UVs read its left tile, the wear links its right tile)");
        }

        static float F(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? D(v) : 0f;
        static float D(object o) => o is double x ? (float)x : o is long l ? l : o is int i ? i : 0f;

        // ================================================================ 2 check
        [MenuItem(M + "2 Check the prefabs (counted, no Play)", false, 2)]
        public static void Check()
        {
            var L = new List<string>(); int pass = 0, fail = 0;
            void C(bool ok, string what) { L.Add((ok ? "PASS  " : "FAIL  ") + what); if (ok) pass++; else fail++; }
            var heavy = Resources.Load<GameObject>("OpenZ/HeavyV2/Z_heavy_v2");
            float heavyLen = 0f, heavyWid = 0f;
            if (heavy) { var b = Bounds(heavy.transform); heavyLen = b.size.z; heavyWid = b.size.x; }
            Bounds heavyHull = heavy ? Foot(heavy.transform, false) : new Bounds();   // Unity pass 9 Nov: the size row compares bodies, not barrels
            foreach (var t in Tanks)
            {
                string X = t.Name, x = t.type;
                var pf = Resources.Load<GameObject>(OzBigTanks.Dir + (x == ZBigTanks.Mammoth ? OzBigTanks.MammothKey : OzBigTanks.BizonKey));
                C(pf, $"{x}: the prefab Resources/{OzBigTanks.Dir}Z_{x} is built");
                if (!pf) continue;
                var r = pf.transform;
                foreach (var n in new[] { "TankSuspension", X + "TurretYaw", X + "BarrelPitch", x + "_HatchHinge", x + "_v2_HatchLid", x + "_v2_HatchFrame", x + "_Board_Approach", x + "_Board_LadderBottom",
                                          x + "_Board_LadderTop", x + "_Board_HatchTop", x + "_Board_Seat", "TankDriverSeat", "TankLeftExit", "TankRightExit", "TankLabelAnchor", "Track_Left", "Track_Right" })
                    C(Deep(r, n), $"{x}: node {n}");
                var lid = Deep(r, x + "_v2_HatchLid"); var hinge = Deep(r, x + "_HatchHinge");
                C(lid && hinge && lid.IsChildOf(hinge), $"{x}: the lid hangs under its *HatchHinge (the hatch_lid kit's rule)");
                var muz = r.GetComponentsInChildren<Transform>(true).Where(q => q.name.StartsWith(X + "Muzzle")).Select(q => q.name).OrderBy(q => q, StringComparer.Ordinal).ToList();
                C(x == ZBigTanks.Mammoth ? muz.SequenceEqual(new[] { X + "Muzzle_L", X + "Muzzle_R" }) : muz.SequenceEqual(new[] { X + "Muzzle" }), $"{x}: muzzles {string.Join(", ", muz)}");
                var rec = r.GetComponentsInChildren<Transform>(true).Where(q => q.name.StartsWith(X + "Recoil")).ToList();
                C(rec.Count == muz.Count && rec.All(q => q.GetComponentInChildren<MeshRenderer>(true)), $"{x}: {rec.Count} recoil slides, each with its barrel mesh");
                var badNames = r.GetComponentsInChildren<Renderer>(true).Select(q => q.name).Where(q => q.Contains("track") || q.Contains("Track") || q.Contains("tread") || q.Contains("Tread")
                               || q.StartsWith("Wheel_") || q.StartsWith("WheelSpin_") || q.ToLowerInvariant().Contains("heavy") || q.ToLowerInvariant().Contains("medium") || q.ToLowerInvariant().Contains("light")).ToList();
                C(badNames.Count == 0, $"{x}: no renderer name breaks the token rule{(badNames.Count > 0 ? ": " + string.Join(", ", badNames.Take(10)) : "")}");
                var links = r.GetComponent<OzTrackLinks>();
                C(links && links.Valid && links.sides.Length == 2, $"{x}: OzTrackLinks baked ({(links ? links.source : "none")}; {(links ? links.LinkCount : 0)} links)");
                if (links && links.Valid)
                    foreach (var s in links.sides)
                    {
                        float worst = s.linkOffset.Max(o => o.magnitude);
                        float spacing = s.linkArc.Length > 1 ? s.length / s.linkArc.Length : 0f;
                        C(worst < 0.03f && Mathf.Abs(spacing - links.pitch) < 0.01f * links.pitch + 0.002f && s.boxHi.x > s.boxLo.x,
                          $"{x} {s.name}: {s.links.Length} links on the loop (at most {worst * 100f:0.0} cm off it), loop {s.length:0.000} m = {s.links.Length} x {spacing:0.0000} m (pitch {links.pitch:0.0000}), box {s.boxLo} .. {s.boxHi}");
                        C(s.gear.Length >= 2, $"{x} {s.name}: {s.gear.Length} corner wheels turn with the links (radii {string.Join(", ", s.gearRadius.Select(q => q.ToString("0.000")))})");
                    }
                C(links && links.halfGauge > 1.2f && links.halfGauge < 3.2f, $"{x}: half gauge {(links ? links.halfGauge : 0):0.000} m (turning on the spot runs the sides opposite ways at the links' speed)");
                WearCheck(r, x, C);                                                                                       // 1.5 track_dirt_variation
                var set = Deep(r, OzBigTankWreck.SetNode);
                int pieces = set ? set.GetComponentsInChildren<MeshRenderer>(true).Length : 0;
                C(set && !set.gameObject.activeSelf && pieces >= 16, $"{x}: the wreck set {OzBigTankWreck.SetNode}: {pieces} pieces, inactive until the break (want 16-64)");
                if (set) C(set.GetComponentsInChildren<MeshRenderer>(true).All(q => q.sharedMaterials.All(m => m && m.renderQueue < 2450) && !OzBigTankWreck.LauncherName(q.name)),
                           $"{x}: every wreck piece has opaque materials and no launcher word (arm, rocket, gun ...: WreckPieces throws those high)");
                if (set) { int brkN = OzBigTankWreck.BreakPieces(r.transform, out string brkHow);                   // 1.5 big_tanks_wreck_finer: the whole break counted
                           C(brkN <= OzBigTankWreck.Cap, $"{x}: the break's pieces ({brkN} = {brkHow}) fit the cap {OzBigTankWreck.Cap}: every piece flies on its own"); }
                if (x == ZBigTanks.Bizon) C(r.GetComponent<OzShuttleExhaust>() && r.GetComponent<OzBigTankExhaust>() && Deep(r, "MK_Exhaust_L") && Deep(r, "MK_Exhaust_R"), $"{x}: the exhaust on both pipes");
                C(Deep(r, "LT_head_L") && Deep(r, "LT_head_R") && Deep(r, "LT_tail_L") && Deep(r, "LT_tail_R"), $"{x}: lamp markers LT_head_L/_R, LT_tail_L/_R");
                var bb = Bounds(r); var gb = Foot(r, true);
                float footK = heavyHull.size.x > 0f ? Mathf.Sqrt(gb.size.z * gb.size.x / (heavyHull.size.z * heavyHull.size.x)) : 0f;
                C(heavy && Mathf.Abs(footK - t.size) < 0.06f,
                  $"{x}: size {bb.size.z:0.00} x {bb.size.x:0.00} x {bb.size.y:0.00} m with the gun; track gear {gb.size.z:0.00} x {gb.size.x:0.00} m = footprint {footK:0.00} x the heavy tank v2's hull ({heavyHull.size.z:0.00} x {heavyHull.size.x:0.00} m without turret / barrel; want {t.size:0.0})");
                var mats = r.GetComponentsInChildren<MeshRenderer>(true).SelectMany(q => q.sharedMaterials).Where(m => m).Distinct().ToList();
                C(mats.All(m => m.shader && m.shader.name.Contains("TeamLit") || m.name.Contains("lamp")) && mats.Any(m => m.name == X + "V2_body"), $"{x}: TeamLit materials ({string.Join(", ", mats.Select(m => m.name))})");
                foreach (var team in new[] { "red", "blue" })
                {
                    C(Resources.Load<Texture2D>("OpenZ/UI/Icons/icon_" + x + "_" + team), $"{x}: icon_{x}_{team}");
                    C(Resources.Load<Texture2D>("OpenZ/UI/PortraitsVehicles/portrait_" + x + "_" + team), $"{x}: portrait_{x}_{team}");
                    for (int s = 1; s <= 4; s++) C(Resources.Load<Texture2D>("OpenZ/ProductionBadge/x" + s + "/face_" + x + "_" + team), $"{x}: badge face x{s} {team}");
                }
                C(Resources.Load<Texture2D>("OpenZ/UI/Neutral/neutral_" + x), $"{x}: neutral tile neutral_{x}");
                C(UnitInfo.Name(x) == (x == ZBigTanks.Mammoth ? "MAMMOTH" : "BIZON"), $"{x}: name {UnitInfo.Name(x)}, tile {UnitInfo.Short(x)}");
                C(OzUePlayground.KindOf(x) == UeKind.Vehicle && OzUePlayground.CanBeNeutral(x) && OzUePlayground.OpenZOnly(x) && Array.IndexOf(OzUePlayground.Vehicles, x) >= 0,
                  $"{x}: in the Unit editor's vehicles, droppable empty under NEUTRAL, OPENZ ONLY");
                foreach (var ev in new[] { "vo_select_x_" + x, "vo_order_x_" + x + "_move", "vo_order_x_" + x + "_attack", "vo_order_x_" + x + "_repair", "vo_electro_stunned_" + x })
                    C(EventClips(ev, out int nClips, out int loaded) && loaded == nClips && nClips >= 3, $"{x}: voice pool {ev}: {loaded} of {nClips} clips load");
            }
            C(EventClips(OzBigTankExhaust.ChuffEvent, out int nc, out int lc) && lc == nc && nc >= 2, $"the exhaust chuff {OzBigTankExhaust.ChuffEvent}: {lc} of {nc} clips load");
            Report("check_prefabs.txt", string.Join("\n", L) + $"\n\n{pass} PASS, {fail} FAIL");
        }

        // the ground footprint: tracks = only the Track_Left / Track_Right gear; else every renderer but the turret / barrel / recoil / muzzle parts
        static Bounds Foot(Transform r, bool tracks)
        {
            var b = new Bounds(); bool any = false;
            foreach (var mr in r.GetComponentsInChildren<MeshRenderer>(false))
            {
                bool turret = false, gear = false;
                for (var q = mr.transform; q && q != r; q = q.parent)
                {
                    string n = q.name.ToLowerInvariant();
                    if (n.Contains("turret") || n.Contains("barrel") || n.Contains("recoil") || n.Contains("muzzle")) turret = true;
                    if (q.name == "Track_Left" || q.name == "Track_Right") gear = true;
                }
                if (tracks ? !gear : turret) continue;
                var mf = mr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                var mb = mf.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var p = r.InverseTransformPoint(mr.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) * 2 - 1, ((c >> 1) & 1) * 2 - 1, ((c >> 2) & 1) * 2 - 1))));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        static Bounds Bounds(Transform r)
        {
            var b = new Bounds(); bool any = false;
            foreach (var mr in r.GetComponentsInChildren<MeshRenderer>(false))
            {
                var mf = mr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                var mb = mf.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var p = r.InverseTransformPoint(mr.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) * 2 - 1, ((c >> 1) & 1) * 2 - 1, ((c >> 2) & 1) * 2 - 1))));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>A sound event of openz_sound_events.json: its clips and how many load through Resources.</summary>
        static bool EventClips(string id, out int n, out int loaded)
        {
            n = loaded = 0;
            var ta = Resources.Load<TextAsset>("OpenZ/Audio/openz_sound_events");
            if (!ta) return false;
            var j = MiniJson.Parse(ta.text) as Dictionary<string, object>;
            if (j == null || !(j.TryGetValue("events", out var eo) && eo is List<object> evs)) return false;
            foreach (var o in evs)
            {
                if (!(o is Dictionary<string, object> e) || !(e.TryGetValue("id", out var i) && (i as string) == id)) continue;
                foreach (var c in (e.TryGetValue("clips", out var cl) ? cl as List<object> : null) ?? new List<object>())
                    if (c is Dictionary<string, object> cd && cd.TryGetValue("res", out var res)) { n++; if (Resources.Load<AudioClip>(res as string)) loaded++; }
                return true;
            }
            return false;
        }

        // ================================================================ 3 lamps
        [MenuItem(M + "3 Write the lamp data (vehicle_lights.json)", false, 3)]
        public static void Lamps()
        {
            const string path = "Assets/OpenZ/Resources/OpenZ/VehicleLights/vehicle_lights.json";
            string text = File.ReadAllText(path);
            var file = JsonUtility.FromJson<VLFile>(text);
            var sb = new StringBuilder();
            var heavy = file != null && file.vehicles != null ? file.vehicles.FirstOrDefault(v => v.type == "heavy") : null;
            if (heavy == null) { Report("lamps_report.txt", "FAILED: no heavy entry in " + path); return; }
            var add = new List<string>();
            foreach (var t in Tanks)
            {
                if (file.vehicles.Any(v => v.type == t.type)) { sb.AppendLine($"{t.type}: already has an entry (left as it is)"); continue; }
                var pf = Resources.Load<GameObject>(OzBigTanks.Dir + (t.type == ZBigTanks.Mammoth ? OzBigTanks.MammothKey : OzBigTanks.BizonKey));
                if (!pf) { sb.AppendLine($"{t.type}: no prefab (menu 1 first)"); continue; }
                var e = JsonUtility.FromJson<VLVehicle>(JsonUtility.ToJson(heavy));
                e.type = t.type; e.prefab = pf.name; e.label = t.Name;
                var b = Bounds(pf.transform); e.boundsMin = new[] { b.min.x, b.min.y, b.min.z }; e.boundsMax = new[] { b.max.x, b.max.y, b.max.z };
                var socks = new List<VLSocket>();
                foreach (var mk in pf.GetComponentsInChildren<Transform>(true).Where(q => q.name.StartsWith("LT_")))
                {
                    var parts = mk.name.Split('_'); if (parts.Length < 3) continue;
                    string kind = parts[1] == "exhaust" ? "engine" : parts[1];
                    var node = mk.parent; var rp = pf.transform.InverseTransformPoint(mk.position); var rn = pf.transform.InverseTransformDirection(-mk.forward);
                    var pattern = heavy.sockets.FirstOrDefault(s => s.kind == kind) ?? heavy.sockets.FirstOrDefault();
                    var s2 = JsonUtility.FromJson<VLSocket>(JsonUtility.ToJson(pattern));
                    var lp = node.InverseTransformPoint(mk.position); var ln = node.InverseTransformDirection(-mk.forward);
                    s2.id = parts[1] + "_" + parts[2]; s2.kind = kind; s2.node = node.name; s2.pos = new[] { lp.x, lp.y, lp.z }; s2.nrm = new[] { ln.x, ln.y, ln.z };
                    s2.root = new[] { rp.x, rp.y, rp.z }; s2.rootNrm = new[] { rn.x, rn.y, rn.z }; s2.nodeScale = node.lossyScale.x;
                    var near = mk.parent.GetComponentInChildren<MeshRenderer>(); s2.surface = near ? near.name : s2.surface;
                    socks.Add(s2);
                }
                if (socks.Count == 0) { sb.AppendLine($"{t.type}: no LT_* markers in the model (no lamps written)"); continue; }
                e.sockets = socks.ToArray();
                if (e.headlight != null)
                {
                    var heads = socks.Where(s => s.kind == "head").ToList();
                    e.headlight.sockets = heads.Select(s => s.id).ToArray();
                    if (heads.Count > 0) { e.headlight.node = heads[0].node; e.headlight.root = new[] { 0f, heads.Average(s => s.root[1]), heads.Average(s => s.root[2]) }; }
                }
                e.emissive = new VLEmissive[0];
                add.Add(JsonUtility.ToJson(e, true));
                sb.AppendLine($"{t.type}: {socks.Count} sockets ({string.Join(", ", socks.Select(s => s.id))})");
            }
            if (add.Count > 0)
            {
                int at = VehiclesEnd(text);
                if (at < 0) { Report("lamps_report.txt", "FAILED: the vehicles array of " + path + " was not found"); return; }
                string nl = text.Contains("\r\n") ? "\r\n" : "\n";
                string ins = string.Join("", add.Select(a => "," + nl + a.Replace("\n", nl)));
                int back = at - 1; while (back > 0 && char.IsWhiteSpace(text[back])) back--;
                text = text.Substring(0, back + 1) + ins + text.Substring(back + 1);
                File.WriteAllText(path, text);
                AssetDatabase.ImportAsset(path);
            }
            Report("lamps_report.txt", sb.ToString());
        }

        /// <summary>The index of the closing ']' of the top-level "vehicles" array (strings skipped), -1 when not found.</summary>
        static int VehiclesEnd(string t)
        {
            int k = t.IndexOf("\"vehicles\"", StringComparison.Ordinal); if (k < 0) return -1;
            k = t.IndexOf('[', k); if (k < 0) return -1;
            int depth = 0; bool str = false;
            for (int i = k; i < t.Length; i++)
            {
                char c = t[i];
                if (str) { if (c == '\\') i++; else if (c == '"') str = false; continue; }
                if (c == '"') str = true;
                else if (c == '[' || c == '{') depth++;
                else if (c == ']' || c == '}') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        // ================================================================ 4 rules
        [MenuItem(M + "4 Rules check (counted, headless)", false, 4)]
        public static void Rules()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OzBigTanks] leave Play mode first"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15BigTanksCount.Run(OzV11Kit.SettingsText, OzV11Kit.MapText);
            Report("rules_check.txt", r);
        }
    }

    /// <summary>A small JSON reader for the menus (objects = Dictionary&lt;string, object&gt;, arrays = List&lt;object&gt;, numbers = double / long,
    /// strings, bools, null). Editor only.</summary>
    public static class MiniJson
    {
        public static object Parse(string s) { int i = 0; try { return Val(s, ref i); } catch { return null; } }
        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        static object Val(string s, ref int i)
        {
            Ws(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>(); i++; Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i); string k = Str(s, ref i); Ws(s, ref i); i++;   // ':'
                    d[k] = Val(s, ref i); Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return d;                                                 // '}'
                }
            }
            if (c == '[')
            {
                var l = new List<object>(); i++; Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Val(s, ref i)); Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return l;                                                 // ']'
                }
            }
            if (c == '"') return Str(s, ref i);
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int a = i; while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            var num = s.Substring(a, i - a);
            if (num.IndexOfAny(new[] { '.', 'e', 'E' }) < 0 && long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out long lv)) return lv;
            return double.Parse(num, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder(); i++;
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++; char e = s[i];
                    if (e == 'u') { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                    else sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e == 'b' ? '\b' : e == 'f' ? '\f' : e);
                }
                else sb.Append(s[i]);
                i++;
            }
            i++; return sb.ToString();
        }
    }
}
#endif
