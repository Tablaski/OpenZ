#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// MLRS v2 = the v1 model with its own texture, the v2 road wheels and rolling tread belts (View/OzMlrs.cs), the editor side. Menus 3 .. 16
    /// stay in Editor/OzMlrsV2Setup.cs.
    ///  1 Build: Resources/OpenZ/MlrsV2/Z_missile_launcher_v2 = a copy of Prefabs/Z_missile_launcher (every node, mesh, part, wreck piece,
    ///    component and setting of v1) with: v1's toy material swapped for Materials/Units/mlrs_v2b_toy on every slot that has it; the two
    ///    belts' meshes = Models/Legacy/MlrsV2b/MLRS_v2b_belt_L / _R (one continuous belt along v1's link loop, v1's width, its UVs along the
    ///    belt) on Materials/Units/mlrs_v2b_track (it scrolls: VehicleMotion.ScrollMaterials), the left belt's baked link fragments left
    ///    out (the track break bins both belts at the death, as it does v1's right one); the hull's mesh = MLRS_v2b_hull (v1's hull without
    ///    the side skirts inside the belt loops; every vertex and the bounds kept; its wreck pieces stay v1's, the clip bake's model pointer
    ///    moved to it); a dark side plate inside each loop (MLRS_v2b_sideplate_L / _R, on its belt); the v2 road wheels: 10 spin nodes
    ///    TankWheel_1 .. 10 beside the suspension (planted with the belts, spun by VehicleMotion) with MLRS_wheel_0 .. 9 on
    ///    Materials/Units/mlrs_v2b_wheel; and the empty marker node OzMlrs.FrameNode as the root's last child. v1 is not touched.
    ///  2 Parity check (counted, edit mode).
    /// </summary>
    public static class OzMlrsV2bSetup
    {
        public const string V1Prefab = OzMlrsV2Setup.V1Prefab, PrefabPath = OzMlrsV2Setup.PrefabPath, Mats = OzMlrsV2Setup.Mats;
        public const string V1Toy = Mats + "mlrs_DV3_VehiclePBR_Masked_mlrs_001.mat", V2Toy = Mats + "mlrs_v2b_toy.mat";
        public const string V1Rubber = Mats + "mlrs_DV3_rubber.mat", V2Track = Mats + "mlrs_v2b_track.mat", V2Wheel = Mats + "mlrs_v2b_wheel.mat";
        public const string V22Track = Mats + "mlrs_v2_track.mat", V22Body = Mats + "mlrs_v2_body.mat";
        public const string V1Picture = "Assets/OpenZ/Models/Legacy/Toy/mlrs_toy.png", V2Picture = "Assets/OpenZ/Models/Legacy/Toy/mlrs_v2b_toy.png";
        public const string Gear = "Assets/OpenZ/Models/Legacy/MlrsV2b/";
        public const string FragHolder = "OZ_WreckFragments";
        const string M = "OpenZ/MLRS v2/";
        static readonly string ProofDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/v2_units/mlrs/proof"));
        static OzUnitModels U => OzMlrs.Models;

        /// <summary>The road wheels (model space, as the belts): centred on each belt's width, 2 cm over the bottom run's inner face, the v2
        /// model's 0.90 m spacing; wheel 1 .. 5 on the +x belt (MLRS_RegularTread_L), 6 .. 10 on the -x belt, front (-z) to back.</summary>
        public const float WheelX = 1.406f, WheelY = 0.55f, WheelR = 0.435f;
        public static readonly float[] WheelZ = { -1.8f, -0.9f, 0f, 0.9f, 1.8f };
        static readonly string[] Belts = { "MLRS_RegularTread_L", "MLRS_RegularTread_R" };

        static Transform Deep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Deep(c, name); if (f) return f; }
            return null;
        }
        static Mesh GearMesh(string n) => AssetDatabase.LoadAssetAtPath<Mesh>(Gear + n + ".asset");

        // ---------------------------------------------------------------- 1 build
        [MenuItem(M + "1 Build the v2 prefab (the v1 model + the v2 texture, wheels and belts)", false, 1)]
        public static void Build()
        {
            string r = BuildNow();
            Debug.Log("[OzMlrsV2b] " + r);
            Directory.CreateDirectory(ProofDir);
            File.WriteAllText(Path.Combine(ProofDir, "build_report.txt"), r + "\n");
        }

        static void AddDrawn(GameObject go, Mesh mesh, Material mat, MeshRenderer like)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            if (like) { r.shadowCastingMode = like.shadowCastingMode; r.receiveShadows = like.receiveShadows; r.lightProbeUsage = like.lightProbeUsage; r.reflectionProbeUsage = like.reflectionProbeUsage; }
        }

        public static string BuildNow()
        {
            var v1 = AssetDatabase.LoadAssetAtPath<GameObject>(V1Prefab);
            var toy1 = AssetDatabase.LoadAssetAtPath<Material>(V1Toy); var toy2 = AssetDatabase.LoadAssetAtPath<Material>(V2Toy);
            var track = AssetDatabase.LoadAssetAtPath<Material>(V2Track); var wheelMat = AssetDatabase.LoadAssetAtPath<Material>(V2Wheel);
            if (!v1) return "FAILED: no " + V1Prefab;
            if (!toy1 || !toy2 || !track || !wheelMat) return "FAILED: a material is missing (" + V1Toy + ", " + V2Toy + ", " + V2Track + ", " + V2Wheel + ")";
            var hull = GearMesh("MLRS_v2b_hull"); var wl = GearMesh("MLRS_v2b_wheel_L"); var wr = GearMesh("MLRS_v2b_wheel_R");
            if (!hull || !wl || !wr || !GearMesh("MLRS_v2b_belt_L") || !GearMesh("MLRS_v2b_belt_R")) return "FAILED: a mesh in " + Gear + " is missing";
            var inst = Object.Instantiate(v1);
            try
            {
                inst.name = OzMlrs.V2Key;
                int slots = 0;
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    var ms = r.sharedMaterials; bool changed = false;
                    for (int i = 0; i < ms.Length; i++) if (ms[i] == toy1) { ms[i] = toy2; changed = true; slots++; }
                    if (changed) r.sharedMaterials = ms;
                }
                var model = Deep(inst.transform, "Model");
                MeshRenderer like = null;
                foreach (var b in Belts)
                {
                    var t = Deep(inst.transform, b); if (!t) return "FAILED: no " + b;
                    var side = b.EndsWith("_L") ? "L" : "R";
                    t.GetComponent<MeshFilter>().sharedMesh = GearMesh("MLRS_v2b_belt_" + side);
                    var mr = t.GetComponent<MeshRenderer>(); mr.sharedMaterial = track; like = mr;
                    var holder = t.Find(FragHolder); if (holder) Object.DestroyImmediate(holder.gameObject);
                    var pl = new GameObject("MLRS_v2b_sideplate_" + side) { layer = t.gameObject.layer };
                    pl.transform.SetParent(t, false); AddDrawn(pl, GearMesh("MLRS_v2b_sideplate_" + side), wheelMat, mr);
                }
                var h = Deep(inst.transform, "MLRS_hull"); h.GetComponent<MeshFilter>().sharedMesh = hull;
                var hb = h.Find(FragHolder) ? h.Find(FragHolder).GetComponent<WreckClipBaked>() : null; if (hb) hb.model = hull;
                for (int k = 0; k < 10; k++)
                {
                    bool l = k < 5;
                    var spin = new GameObject("TankWheel_" + (k + 1)) { layer = model.gameObject.layer };
                    spin.transform.SetParent(model, false); spin.transform.localPosition = new Vector3(l ? WheelX : -WheelX, WheelY, WheelZ[k % 5]);
                    var w = new GameObject("MLRS_wheel_" + k) { layer = model.gameObject.layer };
                    w.transform.SetParent(spin.transform, false); AddDrawn(w, l ? wl : wr, wheelMat, like);
                }
                if (!Deep(inst.transform, OzMlrs.FrameNode))
                {
                    var mk = new GameObject(OzMlrs.FrameNode) { layer = inst.layer };
                    mk.transform.SetParent(inst.transform, false);
                }
                PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath, out bool ok);
                if (!ok) return "FAILED: the prefab could not be saved at " + PrefabPath;
                AssetDatabase.SaveAssets();
                U.Forget();
                return $"prefab: a copy of {V1Prefab}, {slots} toy slot(s) -> {toy2.name}, belts MLRS_v2b_belt_L / _R on {track.name} (left belt's link fragments left out), hull MLRS_v2b_hull, 2 side plates, 10 road wheels, marker {OzMlrs.FrameNode}; saved {PrefabPath}";
            }
            finally { Object.DestroyImmediate(inst); }
        }

        // ---------------------------------------------------------------- 2 parity check
        static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var p = t; p && p != root; p = p.parent) parts.Add(p.name + "#" + p.GetSiblingIndex());
            parts.Reverse();
            return string.Join("/", parts);
        }
        static bool Added(Transform t) => t.name == OzMlrs.FrameNode || t.name.StartsWith("TankWheel_") || t.name.StartsWith("MLRS_wheel_") || t.name.StartsWith("MLRS_v2b_sideplate_");
        static bool UnderLeftBeltHolder(Transform t) { for (var p = t; p; p = p.parent) if (p.name == FragHolder && p.parent && p.parent.name == "MLRS_RegularTread_L") return true; return false; }

        static string Entry(string text, string type)
        {
            int at = text.IndexOf("\"type\": \"" + type + "\"", System.StringComparison.Ordinal);
            if (at < 0) return null;
            int s = text.LastIndexOf('{', at); if (s < 0) return null;
            int depth = 0;
            for (int i = s; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}' && --depth == 0) return Regex.Replace(text.Substring(s, i - s + 1), "\"(type|prefab|label)\": \"[^\"]*\",?", "");
            }
            return null;
        }

        static List<string> MatDiff(Material a, Material b, params string[] skip)
        {
            var d = new List<string>();
            if (a.shader != b.shader) d.Add("shader");
            var sh = a.shader;
            for (int i = 0; i < sh.GetPropertyCount(); i++)
            {
                string n = sh.GetPropertyName(i); if (skip.Contains(n)) continue;
                switch (sh.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Texture: if (a.GetTexture(n) != b.GetTexture(n)) d.Add(n); break;
                    case UnityEngine.Rendering.ShaderPropertyType.Color: case UnityEngine.Rendering.ShaderPropertyType.Vector: if (a.GetVector(n) != b.GetVector(n)) d.Add(n); break;
                    default: if (!Mathf.Approximately(a.GetFloat(n), b.GetFloat(n))) d.Add(n); break;
                }
            }
            if (a.renderQueue != b.renderQueue) d.Add("renderQueue");
            return d;
        }

        static Color32[] Pixels(string assetPath, out int w)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath)));
            w = t.width; var p = t.GetPixels32(); Object.DestroyImmediate(t); return p;
        }

        [MenuItem(M + "2 Parity check (counted, no Play)", false, 2)]
        public static void Parity()
        {
            var lines = new List<string>(); int pass = 0, fail = 0;
            void Check(bool ok, string what) { lines.Add((ok ? "PASS " : "FAIL ") + what); if (ok) pass++; else fail++; }
            var v1 = AssetDatabase.LoadAssetAtPath<GameObject>(V1Prefab);
            var v2 = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var toy1 = AssetDatabase.LoadAssetAtPath<Material>(V1Toy); var toy2 = AssetDatabase.LoadAssetAtPath<Material>(V2Toy);
            var rub = AssetDatabase.LoadAssetAtPath<Material>(V1Rubber); var track = AssetDatabase.LoadAssetAtPath<Material>(V2Track); var wheel = AssetDatabase.LoadAssetAtPath<Material>(V2Wheel);
            var t22 = AssetDatabase.LoadAssetAtPath<Material>(V22Track); var b22 = AssetDatabase.LoadAssetAtPath<Material>(V22Body);
            Check(v1 && v2 && toy1 && toy2 && rub && track && wheel && t22 && b22, $"prefabs v1 {(v1 ? "found" : "MISSING")}, v2 {(v2 ? "found" : "MISSING")}; materials mlrs_v2b_toy {(toy2 ? "found" : "MISSING")}, mlrs_v2b_track {(track ? "found" : "MISSING")}, mlrs_v2b_wheel {(wheel ? "found" : "MISSING")}");
            if (!v1 || !v2 || !toy1 || !toy2 || !rub || !track || !wheel || !t22 || !b22) { Write(lines, pass, fail); return; }

            // the node tree: every v1 node (but the left belt's baked link fragments) in v2 at the same place, rest, layer, active state, components
            var t1 = new Dictionary<string, Transform>(); var t2 = new Dictionary<string, Transform>();
            foreach (var t in v1.GetComponentsInChildren<Transform>(true)) if (t != v1.transform && !UnderLeftBeltHolder(t)) t1[PathOf(t, v1.transform)] = t;
            int added = 0; var addedNames = new List<string>();
            foreach (var t in v2.GetComponentsInChildren<Transform>(true)) { if (t == v2.transform) continue; bool add = false; for (var p = t; p && p != v2.transform; p = p.parent) if (Added(p)) add = true; if (add) { added++; addedNames.Add(t.name); continue; } t2[PathOf(t, v2.transform)] = t; }
            var miss = t1.Keys.Where(k => !t2.ContainsKey(k)).ToList(); var extra = t2.Keys.Where(k => !t1.ContainsKey(k)).ToList();
            var diff = new List<string>(); int comps = 0;
            foreach (var kv in t1)
            {
                if (!t2.TryGetValue(kv.Key, out var b)) continue;
                var a = kv.Value;
                if ((a.localPosition - b.localPosition).sqrMagnitude > 1e-10f || Quaternion.Angle(a.localRotation, b.localRotation) > 0.001f || (a.localScale - b.localScale).sqrMagnitude > 1e-10f
                    || a.gameObject.layer != b.gameObject.layer || a.gameObject.activeSelf != b.gameObject.activeSelf) diff.Add(a.name + " (rest / layer / active)");
                var ca = a.GetComponents<Component>().Select(c => c ? c.GetType().Name : "missing").ToList(); var cb = b.GetComponents<Component>().Select(c => c ? c.GetType().Name : "missing").ToList();
                comps += ca.Count;
                if (!ca.SequenceEqual(cb)) diff.Add(a.name + " (components " + string.Join("+", cb) + " vs v1 " + string.Join("+", ca) + ")");
            }
            Check(miss.Count == 0 && extra.Count == 0 && diff.Count == 0 && added == 23, $"the node tree: {t1.Count} nodes of v1 (the left belt's 9 baked link-fragment nodes left out) and their {comps} components in v2 at the same places, rests, layers; {added} added nodes (want 23: 10 TankWheel_ + 10 MLRS_wheel_ + 2 side plates + the marker){(miss.Count + extra.Count + diff.Count > 0 ? "; missing " + string.Join(", ", miss.Take(8)) + "; extra " + string.Join(", ", extra.Take(8)) + "; different " + string.Join(", ", diff.Take(8)) : "")}");
            var rc1 = v1.transform.GetComponents<Component>().Select(c => c.GetType().Name); var rc2 = v2.transform.GetComponents<Component>().Select(c => c.GetType().Name);
            Check(rc1.SequenceEqual(rc2) && v1.layer == v2.layer && v2.name == OzMlrs.V2Key, $"the root: components {string.Join("+", rc2)} (v1 {string.Join("+", rc1)}), layer {v2.layer}, name {v2.name}");
            Transform N(GameObject g, string n) => g.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == n);
            // meshes: v1's everywhere but the two belts and the hull
            int same = 0; var mdiff = new List<string>();
            foreach (var kv in t1)
            {
                if (!t2.TryGetValue(kv.Key, out var b)) continue;
                var ma = kv.Value.GetComponent<MeshFilter>(); var mb = b.GetComponent<MeshFilter>();
                if (!ma) continue;
                if (mb && ma.sharedMesh == mb.sharedMesh) same++; else mdiff.Add(kv.Value.name);
            }
            Check(mdiff.Count == 3 && mdiff.Contains("MLRS_RegularTread_L") && mdiff.Contains("MLRS_RegularTread_R") && mdiff.Contains("MLRS_hull"), $"meshes: {same} the same as v1's, {mdiff.Count} different: {string.Join(", ", mdiff)} (want the two belts and the hull)");
            {
                var h1 = N(v1, "MLRS_hull").GetComponent<MeshFilter>().sharedMesh; var h2 = N(v2, "MLRS_hull").GetComponent<MeshFilter>().sharedMesh;
                var hb = N(v2, "MLRS_hull").Find(FragHolder); var clip = hb ? hb.GetComponent<WreckClipBaked>() : null;
                bool bounds = h1 && h2 && (h1.bounds.center - h2.bounds.center).magnitude < 1e-3f && (h1.bounds.size - h2.bounds.size).magnitude < 1e-3f;
                Check(h2 && h2.name == "MLRS_v2b_hull" && h2.vertexCount == h1.vertexCount && bounds && h2.triangles.Length < h1.triangles.Length && clip && clip.model == h2,
                      $"hull: MLRS_v2b_hull {(h2 ? h2.vertexCount : 0)} vertices (v1 {(h1 ? h1.vertexCount : 0)}), {(h2 ? h2.triangles.Length / 3 : 0)} triangles (v1 {(h1 ? h1.triangles.Length / 3 : 0)}: the side skirts inside the loops out), bounds = v1's {bounds}; wreck pieces v1's, clip bake model -> MLRS_v2b_hull {(clip && clip.model == h2)}");
            }
            foreach (var b in Belts)
            {
                var a = N(v1, b); var c = N(v2, b);
                var m1 = a.GetComponent<MeshFilter>().sharedMesh; var m2 = c.GetComponent<MeshFilter>().sharedMesh; var mr = c.GetComponent<MeshRenderer>();
                bool bounds = m1 && m2 && (m1.bounds.min - m2.bounds.min).magnitude < 1e-3f && (m1.bounds.max - m2.bounds.max).magnitude < 1e-3f;
                float vmin = 1e9f, vmax = -1e9f; if (m2) foreach (var uv in m2.uv) { vmin = Mathf.Min(vmin, uv.y); vmax = Mathf.Max(vmax, uv.y); }
                bool scroll = mr && mr.sharedMaterials.Length == 1 && mr.sharedMaterial == track && VehicleMotion.ScrollMaterials.Contains(track.name) && track.name == OzMlrs.BeltMaterial;
                bool noFrags = !c.Find(FragHolder) && m2 && m2.isReadable;
                Check(bounds && scroll && noFrags && vmax - vmin > 2.5f && c.parent && c.parent.name == "Model",
                      $"{b}: {(m2 ? m2.name : "none")} {(m2 ? m2.triangles.Length / 3 : 0)} triangles, bounds = v1's tread {bounds} (the planting reads them), UV V {vmin:0.00}..{vmax:0.00} (30 shoes, 2.73 tiles: 0.248 a metre, it rolls at 1.007 x the ground speed), material {(mr ? mr.sharedMaterial.name : "-")} scrolls {scroll}, readable, no baked fragments (the track break bins it) {noFrags}, beside the suspension {(c.parent ? c.parent.name : "-")}");
            }
            // materials: v1's but the toy slots, the belts and the added parts
            int toySlots = 0, other = 0;
            foreach (var kv in t1)
            {
                if (!t2.TryGetValue(kv.Key, out var b)) continue;
                var ra = kv.Value.GetComponent<Renderer>(); var rb = b.GetComponent<Renderer>(); if (!ra) continue;
                var ma = ra.sharedMaterials; var mb = rb ? rb.sharedMaterials : new Material[0];
                if (ma.Length != mb.Length) { other++; continue; }
                for (int i = 0; i < ma.Length; i++)
                {
                    if (ma[i] == toy1 && mb[i] == toy2) toySlots++;
                    else if (ma[i] == rub && mb[i] == track && Belts.Contains(b.name)) { }
                    else if (ma[i] != mb[i]) other++;
                }
            }
            Check(toySlots > 0 && other == 0, $"materials: {toySlots} slot(s) {toy1.name} -> {toy2.name}, 2 belt slots {rub.name} -> {track.name}, {other} other difference(s)");
            var dToy = MatDiff(toy1, toy2, "_BaseMap"); var dTr = MatDiff(t22, track, "_BaseMap", "_BumpMap"); var dWh = MatDiff(b22, wheel, "_BaseMap", "_BumpMap", "_UseNormalMap");
            Check(dToy.Count == 0 && toy2.GetTexture("_BaseMap") && AssetDatabase.GetAssetPath(toy2.GetTexture("_BaseMap")) == V2Picture,
                  $"{toy2.name}: v1's {toy1.name} in every setting ({dToy.Count} differences{(dToy.Count > 0 ? ": " + string.Join(", ", dToy) : "")}), its picture {V2Picture}");
            Check(dTr.Count == 0 && dWh.Count == 0, $"{track.name} = the MLRS v2 belt material's settings (the slightly dark grey calibration) but its pictures ({dTr.Count} differences); {wheel.name} = mlrs_v2_body's settings but its picture and no normal map ({dWh.Count} differences){(dTr.Count + dWh.Count > 0 ? ": " + string.Join(", ", dTr.Concat(dWh)) : "")}");
            {
                var p1 = Pixels(V1Picture, out int w1); var p2 = Pixels(V2Picture, out int w2);
                int team1 = 0, team2 = 0, moved = 0, paint1 = 0, paint2 = 0;
                if (p1.Length == p2.Length)
                    for (int i = 0; i < p1.Length; i++)
                    {
                        bool a = p1[i].a >= 224, b = p2[i].a >= 224; if (a) team1++; if (b) team2++; if (a != b) moved++;
                        if (p1[i].a > 64 && p1[i].a < 224) paint1++; if (p2[i].a > 64 && p2[i].a < 224) paint2++;
                    }
                Check(w1 == w2 && p1.Length == p2.Length && moved == 0 && team1 == team2, $"team texels: {team2} in the v2 picture, {team1} in v1's, {moved} placed differently; paint texels {paint2} (v1 {paint1}: a few edge chips to bare metal)");
            }
            // the marker and the geometry: a v2 view, the v1 geometry (the launcher limits and kick sizes are v1's: no v2_2 box)
            {
                int m2 = v2.GetComponentsInChildren<Transform>(true).Count(t => t.name == OzMlrs.FrameNode), m1 = v1.GetComponentsInChildren<Transform>(true).Count(t => t.name == OzMlrs.FrameNode);
                var mk = N(v2, OzMlrs.FrameNode);
                bool ok = m2 == 1 && m1 == 0 && mk && mk.parent == v2.transform && mk.GetSiblingIndex() == v2.transform.childCount - 1 && mk.GetComponents<Component>().Length == 1 && mk.childCount == 0;
                Check(ok && U.IsV2(v2.transform) && !U.IsV2(v1.transform) && !OzMlrs.BoxModel(v2.transform) && !OzMlrs.BoxModel(v1.transform),
                      $"the v2 marker {OzMlrs.FrameNode}: {m2} on v2 (empty, the root's last child), {m1} on v1; IsV2 v2 {U.IsV2(v2.transform)}, v1 {U.IsV2(v1.transform)}; the v2_2 launcher box ({OzMlrs.BoxNode}) on neither: launcher limits -20 .. 57 deg, kick 0.22 m / 6 deg, spacing radius as v1 on both");
            }
            // the road wheels
            {
                var wl = new List<string>(); int good = 0;
                var rx = new Regex("^TankWheel_\\d+$");
                for (int k = 0; k < 10; k++)
                {
                    var s = N(v2, "TankWheel_" + (k + 1)); var w = s ? s.Find("MLRS_wheel_" + k) : null;
                    var mf = w ? w.GetComponent<MeshFilter>() : null; var mr = w ? w.GetComponent<MeshRenderer>() : null;
                    var belt = N(v2, k < 5 ? "MLRS_RegularTread_L" : "MLRS_RegularTread_R").GetComponent<MeshFilter>().sharedMesh.bounds;
                    bool ok = s && rx.IsMatch(s.name) && s.parent && s.parent.name == "Model" && w && mf && mf.sharedMesh && mr && mr.sharedMaterial == wheel && s.localRotation == Quaternion.identity;
                    if (ok)
                    {
                        var wb = mf.sharedMesh.bounds; var c = s.localPosition + wb.center;
                        float r = Mathf.Max(wb.size.y, wb.size.z) * 0.5f;
                        ok = Mathf.Abs(r - WheelR) < 0.01f && c.x - wb.extents.x >= belt.min.x - 1e-3f && c.x + wb.extents.x <= belt.max.x + 1e-3f && c.y - r > belt.min.y + 0.1f && c.y + r < belt.max.y - 0.2f
                             && c.z - r > belt.min.z && c.z + r < belt.max.z;
                        if (ok) good++; else wl.Add($"{s.name} r {r:0.000} at {c}");
                    }
                    else wl.Add("TankWheel_" + (k + 1) + " (missing / wrong)");
                }
                Check(good == 10, $"road wheels: {good}/10 spin nodes TankWheel_1..10 (VehicleMotion spins them: radius 0.435, front = -z) beside the suspension under Model, each MLRS_wheel_k on {wheel.name} inside its belt's loop (v2's wheel, 0.90 m apart){(wl.Count > 0 ? "; " + string.Join("; ", wl) : "")}");
                int plates = 0; foreach (var b in Belts) { var p = N(v2, b).Find("MLRS_v2b_sideplate_" + (b.EndsWith("_L") ? "L" : "R")); if (p && p.GetComponent<MeshRenderer>() && p.GetComponent<MeshRenderer>().sharedMaterial == wheel && !p.name.ToLowerInvariant().Contains("track") && !p.name.Contains("Tread")) plates++; }
                Check(plates == 2, $"side plates: {plates}/2 on their belts (planted with them, never scrolled: no track / tread in their names)");
            }
            // wreck pieces: the hull's and the launcher's as v1's
            {
                int h1 = N(v1, "MLRS_hull").Find(FragHolder).childCount, h2 = N(v2, "MLRS_hull").Find(FragHolder).childCount;
                int r1 = N(v1, "MLRS_rack").Find(FragHolder).childCount, r2 = N(v2, "MLRS_rack").Find(FragHolder).childCount;
                Check(h1 == h2 && r1 == r2 && TrackGround.Types.Contains(OzMlrs.Type) && OzTreadBreak.Types.Contains(OzMlrs.Type), $"wreck: hull {h2} pieces (v1 {h1}), launcher {r2} (v1 {r1}); the belts break at the death (OzTreadBreak), the gear planted (TrackGround)");
            }
            // lamps / blinkers: the v2 entries = v1's
            foreach (var (file, what) in new[] { ("Assets/OpenZ/Resources/OpenZ/VehicleLights/vehicle_lights.json", "lamps"), ("Assets/OpenZ/Resources/OpenZ/UnitBlinkers/unit_blinkers.json", "blinkers") })
            {
                var txt = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), file));
                var e1 = Entry(txt, OzMlrs.Type); var e2 = Entry(txt, U.LightsKey);
                Check(e1 != null && e2 != null && e1 == e2, $"{what}: the '{U.LightsKey}' entry = v1's '{OzMlrs.Type}' (the same sockets) {(e1 != null && e1 == e2)}");
            }
            // pictures, the switch, the testing panel's tiles
            {
                var bad = new List<string>();
                foreach (var team in new[] { "red", "blue" })
                {
                    var ic = Resources.Load<Texture2D>(OzMlrs.Dir + "icon_missile_launcher_v2_" + team); if (!ic || ic.width != 256) bad.Add("icon " + team);
                    var po = File.ReadAllBytes(Path.Combine(Application.dataPath, "OpenZ/Resources/OpenZ/MlrsV2/portrait_missile_launcher_v2_" + team + ".png"));
                    var pv = File.ReadAllBytes(Path.Combine(Application.dataPath, "OpenZ/Resources/OpenZ/UI/PortraitsVehicles/portrait_missile_launcher_" + team + ".png"));
                    if (!po.SequenceEqual(pv)) bad.Add("portrait " + team + " is not v1's avatar");
                }
                var tile = Resources.Load<Texture2D>("OpenZ/UI/Neutral/neutral_missile_launcher_v2"); if (!tile || tile.width != 512) bad.Add("neutral tile");
                Check(bad.Count == 0 && OzMlrs.Version == 2 && U.Drawn == 2, $"pictures: icon red / blue 256, the unit card = v1's avatar, the neutral tile 512{(bad.Count > 0 ? "; " + string.Join(", ", bad) : "")}; the switch OzMlrs.Version {OzMlrs.Version}, drawn v{U.Drawn}");
            }
            Write(lines, pass, fail);
        }

        static void Write(List<string> lines, int pass, int fail)
        {
            string head = $"[OzMlrsV2b] parity check {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}";
            Directory.CreateDirectory(ProofDir);
            File.WriteAllText(Path.Combine(ProofDir, "parity_check.txt"), head + "\n" + string.Join("\n", lines) + "\n");
            Debug.Log(head + "\n" + string.Join("\n", lines));
        }
    }
}
#endif
