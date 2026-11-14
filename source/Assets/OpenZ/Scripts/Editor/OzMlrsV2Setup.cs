#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// MLRS v2 (1.2 v2 units), the editor side; the light tank v2's menus (Editor/OzLightV2Setup.cs) for the MLRS.
    ///  1 Build: the v2 view prefab Resources/OpenZ/MlrsV2/Z_missile_launcher_v2 from Models/Legacy/mlrs_v2.glb, made like
    ///    Prefabs/Z_missile_launcher (root + TeamPaint copied from v1, the model as "Model" at (0, 0, 0) with v1's scale 0.99598: v2's belts
    ///    touch the ground there and it is centred, v1's offset would float it 4 cm and shift it 9 cm), the GLB's node tree with v1's names
    ///    (MLRS_rack stays an empty node: the launcher kick moves the box, its rockets and their muzzles together), the TeamLit materials
    ///    mlrs_v2_body / _head / _track and the light tank's lamp material for the white head lamps, v1's renderer settings; then its wreck
    ///    fragments (files mlrsv2_* beside v1's missile_launcher_*: v1's are never touched): the game's cut of the three largest parts (hull,
    ///    launcher pedestal, launcher box) and both belts cut the same way (v1 bakes its left belt only; k72 bins the other at the death), then
    ///    the clipped bake. v1 stays as it is.
    ///  2 Parity check (counted, edit mode): every node the code reads, the launcher (box, rockets, muzzles on the tube axes, the hinge),
    ///    the planted running gear, materials, wreck, footprint, ladder, hatch, lamps and blinkers data, pictures, the switch, the limits.
    ///  3 Play proof L02 / L17: MlrsV2Proof (v1 and v2 side by side; launcher turn, rockets from their own tubes, kick, hull rock, reload,
    ///    planted tracks, boarding, damage, death, wreck).
    ///  4 .. 16 the films and tests (HeavyV2Films for the MLRS, L02, 1920 x 1080 with sound).
    /// </summary>
    public static class OzMlrsV2Setup
    {
        public const string Glb = "Assets/OpenZ/Models/Legacy/mlrs_v2.glb", V1Prefab = "Assets/OpenZ/Prefabs/Z_missile_launcher.prefab",
                            Folder = "Assets/OpenZ/Resources/OpenZ/MlrsV2", PrefabPath = Folder + "/Z_missile_launcher_v2.prefab", Mats = "Assets/OpenZ/Materials/Units/";
        /// <summary>The wreck fragment file key: mlrsv2_* (OpenZWreckFragments clears "&lt;key&gt;_*" before it bakes; "missile_launcher_*" would take v1's files).</summary>
        public const string FragKey = "mlrsv2";
        /// <summary>How far out (model units) the build moves v2's side plates (the dark plates behind the road wheels, planted on their belts):
        /// the model step left them 3 cm inside the belts and 3 cm outside the hull walls; the hull rocks a little over its planted gear and a
        /// launcher salvo to a side rolls it, which took its walls up to 1.3 cm through the plates (measured off the engine, the kit's
        /// tools/gear_sim_mlrs.py). Out by 0.027 the plates sit 3 mm inside their belts (they never move against them: both planted) and the
        /// walls have 5.7 cm: 0 through them in every frame.</summary>
        public const float PlateOut = 0.027f;
        const string FragDir = "Assets/OpenZ/Models/Generated/WreckFragments";
        const string M = "OpenZ/MLRS v2/";
        const string L02 = "Assets/OpenZ/Scenes/L02_Desert.unity", L17 = "Assets/OpenZ/Scenes/L17_CarPark.unity";
        static readonly string ProofDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/v2_units/mlrs/proof"));
        static OzUnitModels U => OzMlrs.Models;
        static readonly string[] Belts = { "MLRS_RegularTread_L", "MLRS_RegularTread_R" };

        static Transform Deep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Deep(c, name); if (f) return f; }
            return null;
        }
        static void Layer(Transform t, int layer) { t.gameObject.layer = layer; foreach (Transform c in t) Layer(c, layer); }
        static bool Under(Transform t, string name) { for (var p = t; p; p = p.parent) if (p.name == name) return true; return false; }

        // ---------------------------------------------------------------- 1 build
        // menu 1 is Editor/OzMlrsV2bSetup.cs now (the v2 = the v1 model with its own texture, wheels and belts); this build of the first v2 model stays, off the menu
        public static void Build()
        {
            string r = BuildNow();
            Debug.Log("[OzMlrsV2] " + r);
            Directory.CreateDirectory(ProofDir);
            File.WriteAllText(Path.Combine(ProofDir, "build_report.txt"), r + "\n");
        }

        public static string BuildNow()
        {
            var glb = AssetDatabase.LoadAssetAtPath<GameObject>(Glb);
            var v1 = AssetDatabase.LoadAssetAtPath<GameObject>(V1Prefab);
            if (!glb) return "FAILED: no " + Glb + " (import it first)";
            if (!v1) return "FAILED: no " + V1Prefab;
            var mats = new Dictionary<string, Material>
            {
                ["MlrsV2_body"] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "mlrs_v2_body.mat"),
                ["MlrsV2_head"] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "mlrs_v2_head.mat"),
                ["MlrsV2_track"] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "mlrs_v2_track.mat"),
                ["MlrsV2_lamp"] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "tank_DV3_lamp.mat"),
            };
            foreach (var kv in mats) if (!kv.Value) return "FAILED: material for " + kv.Key + " missing in " + Mats;
            var v1Model = v1.transform.Find("Model");
            var v1Hull = Deep(v1.transform, "MLRS_hull"); var v1Mr = v1Hull ? v1Hull.GetComponent<MeshRenderer>() : null;
            if (!v1Model || !v1Mr) return "FAILED: Z_missile_launcher has no Model / MLRS_hull renderer";
            var sb = new StringBuilder();
            var inst = Object.Instantiate(glb);
            GameObject root = null;
            try
            {
                var susp = Deep(inst.transform, "MLRSSuspension");
                if (!susp || !susp.parent) return "FAILED: the GLB has no MLRSSuspension under a root node";
                var model = susp.parent;                                                                  // the glTF scene's root node "MLRSRoot"
                root = new GameObject(OzMlrs.V2Key) { layer = v1.layer };
                var tp = root.AddComponent<TeamPaint>();
                var tp1 = v1.GetComponent<TeamPaint>(); if (tp1) EditorUtility.CopySerialized(tp1, tp);
                model.SetParent(root.transform, false); model.name = "Model";
                model.localPosition = Vector3.zero; model.localRotation = v1Model.localRotation; model.localScale = v1Model.localScale;
                { var k93Yaw = Deep(model, "MLRSRackYaw"); if (k93Yaw) { k93Yaw.localScale = Vector3.one * OzMlrsLauncher.ScaleV2; k93Yaw.localPosition += Vector3.up * OzMlrsLauncher.LiftV2M / Mathf.Max(1e-4f, Mathf.Abs(model.localScale.y)); } var k93Rack = Deep(model, "MLRS_rack"); if (k93Rack) k93Rack.localScale = Vector3.one; }   // items 151 / 151d: the modelled turret x one uniform scale, its launcher as big as v1's from the game camera, nothing stretched (View/OzMlrs.Launcher.cs)
                { var k104bPitch = Deep(model, "MLRSRackPitch"); if (k104bPitch) k104bPitch.localRotation = Quaternion.AngleAxis(OzMlrsLauncher.RestRaiseV2, Vector3.right) * k104bPitch.localRotation; }   // item 184: the launcher rests at v1's tilt, 31.92 deg (+ about x raises the box's front; View/OzMlrs.Launcher.cs)
                // item 146: the side plates ride on their belts (the GLB already hangs them there: checked)
                int plates = 0;
                foreach (var (plate, belt) in new[] { ("MLRS_sideplate_left", "MLRS_RegularTread_L"), ("MLRS_sideplate_right", "MLRS_RegularTread_R") })
                {
                    var p = Deep(model, plate); var b = Deep(model, belt);
                    if (!p || !b) continue;
                    if (p.parent != b) p.SetParent(b, true);
                    var pmf = p.GetComponent<MeshFilter>();
                    float side = pmf && pmf.sharedMesh ? Mathf.Sign(pmf.sharedMesh.bounds.center.x) : Mathf.Sign(b.localPosition.x);
                    p.localPosition += new Vector3(side * PlateOut, 0f, 0f);                                   // 3 mm inside the belt, 5.7 cm outside the hull wall
                    plates++;
                }
                int n = 0, byName = 0; var unknown = new List<string>();
                foreach (var mr in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var src = mr.sharedMaterials; var all = new Material[src.Length];
                    for (int i = 0; i < src.Length; i++)
                    {
                        string mn = src[i] ? src[i].name : "";
                        int k = mn.IndexOf(' '); if (k > 0) mn = mn.Substring(0, k);
                        if (mats.TryGetValue(mn, out var m)) { all[i] = m; byName++; continue; }
                        // fallback by the renderer's name (the GLB's materials, see V1_PARITY.md section 2)
                        string nm = mr.name;
                        all[i] = nm.StartsWith("MLRS_RegularTread") ? mats["MlrsV2_track"] : nm.StartsWith("MLRS_headlamp") ? mats["MlrsV2_lamp"]
                               : nm == "MLRS_v2_box" || nm == "MLRS_pedestal" || nm == "MLRS_v2_Antennas" || nm.StartsWith("MLRS_rocket_") ? mats["MlrsV2_head"] : mats["MlrsV2_body"];
                        unknown.Add(nm + "[" + i + "] '" + (src[i] ? src[i].name : "none") + "'");
                    }
                    mr.sharedMaterials = all;
                    mr.shadowCastingMode = v1Mr.shadowCastingMode; mr.receiveShadows = v1Mr.receiveShadows;
                    mr.lightProbeUsage = v1Mr.lightProbeUsage; mr.reflectionProbeUsage = v1Mr.reflectionProbeUsage;
                    mr.motionVectorGenerationMode = v1Mr.motionVectorGenerationMode; mr.allowOcclusionWhenDynamic = v1Mr.allowOcclusionWhenDynamic;
                    n++;
                }
                foreach (var c in model.GetComponentsInChildren<Component>(true))                           // only transforms, filters, renderers (as v1)
                    if (!(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer)) Object.DestroyImmediate(c);
                Layer(model, v1Model.gameObject.layer);
                var rack = Deep(model, "MLRS_rack");
                sb.Append($"prefab: {n} renderers, {byName} material slots by the GLB's material names{(unknown.Count > 0 ? ", by renderer name: " + string.Join(", ", unknown) : "")}; side plates under their belts and {PlateOut} out {plates}/2; " +
                          $"MLRS_rack {(rack && !rack.GetComponent<Renderer>() ? "an empty node" : "HAS A RENDERER (wrong)")}; Model at (0, 0, 0) x {model.localScale.x:0.00000}; ");
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/OpenZ/Resources/OpenZ", "MlrsV2");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok) return "FAILED: the prefab could not be saved at " + PrefabPath;
                sb.Append("saved ").Append(PrefabPath).Append("; ");
            }
            finally
            {
                if (root) Object.DestroyImmediate(root);
                if (inst) Object.DestroyImmediate(inst);
            }
            // wreck fragments: OpenZWreckFragments' own cut of the three largest parts (its Bake is private: called by name), both belts cut
            // with its own Split, then the clipped bake
            var bake = typeof(OpenZWreckFragments).GetMethod("Bake", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string), typeof(string) }, null);
            sb.Append("wreck fragments: ").Append(bake != null ? (string)bake.Invoke(null, new object[] { PrefabPath, FragKey }) : "FAILED (OpenZWreckFragments.Bake not found)").Append("; ");
            AssetDatabase.SaveAssets();
            sb.Append("belts: ").Append(CutBelts()).Append("; ");
            AssetDatabase.SaveAssets();
            sb.Append("clipped: ").Append(OpenZWreckClipBake.ClipPrefab(PrefabPath, FragKey));
            AssetDatabase.SaveAssets();
            U.Forget();
            return sb.ToString();
        }

        /// <summary>Both belts cut into fragments by OpenZWreckFragments' own Split (bins of the mesh bounds), saved as mlrsv2_&lt;belt&gt;_&lt;i&gt;
        /// assets, under an inactive OZ_WreckFragments holder on each belt (as the game's bake makes them); a belt that has fragments already is
        /// left as it is.</summary>
        static string CutBelts()
        {
            var split = typeof(OpenZWreckFragments).GetMethod("Split", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Mesh), typeof(int) }, null);
            if (split == null) return "FAILED (OpenZWreckFragments.Split not found; k72 bins them at the death instead)";
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var said = new List<string>();
            try
            {
                foreach (var bn in Belts)
                {
                    var t = Deep(root.transform, bn); var mf = t ? t.GetComponent<MeshFilter>() : null; var r = t ? t.GetComponent<MeshRenderer>() : null;
                    if (!mf || !r || !mf.sharedMesh) { said.Add(bn + " missing"); continue; }
                    if (t.Find("OZ_WreckFragments")) { said.Add(bn + " has fragments already"); continue; }
                    var frags = split.Invoke(null, new object[] { mf.sharedMesh, 12 }) as List<Mesh>;
                    if (frags == null || frags.Count < 2) { said.Add(bn + " not cut"); continue; }
                    var holder = new GameObject("OZ_WreckFragments"); holder.transform.SetParent(t, false);
                    for (int i = 0; i < frags.Count; i++)
                    {
                        var fm = frags[i]; fm.name = $"{FragKey}_{bn}_{i}";
                        AssetDatabase.CreateAsset(fm, $"{FragDir}/{FragKey}_{bn}_{i}.asset");
                        var go = new GameObject("frag_" + i); go.transform.SetParent(holder.transform, false);
                        go.AddComponent<MeshFilter>().sharedMesh = fm;
                        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = r.sharedMaterials.Take(Mathf.Max(1, fm.subMeshCount)).ToArray();
                        mr.shadowCastingMode = r.shadowCastingMode; mr.receiveShadows = r.receiveShadows;
                    }
                    holder.SetActive(false);
                    said.Add($"{bn} cut into {frags.Count}");
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return string.Join(", ", said);
        }

        // ---------------------------------------------------------------- 2 parity check
        // menu 2 is Editor/OzMlrsV2bSetup.cs now; this parity of the first v2 model stays, off the menu
        public static void Parity()
        {
            var lines = new List<string>(); int pass = 0, fail = 0;
            void Check(bool ok, string what) { lines.Add((ok ? "PASS " : "FAIL ") + what); if (ok) pass++; else fail++; }
            var v1 = AssetDatabase.LoadAssetAtPath<GameObject>(V1Prefab);
            var v2 = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Check(v1 && v2, $"prefabs: v1 {V1Prefab} {(v1 ? "found" : "MISSING")}, v2 {PrefabPath} {(v2 ? "found" : "MISSING (menu 1)")}");
            if (!v1 || !v2) { Write(lines, pass, fail); return; }
            Transform N(GameObject g, string name) => g.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
            string P(Transform t, GameObject g) => !t || !t.parent ? "-" : t.parent == g.transform ? "<root>" : t.parent.name;
            // the nodes v1's code reads by name (UnitView, UnitAim, VehicleMotion, MlrsSideKick, MlrsSalvo, BoardingView, VehicleLights,
            // WheeledBreakup, OzTreadBreak), in v2 with v1's parent; the launcher's rockets hang under the kicked node on v2 (by design)
            var roles = new List<string> { "Model", "MLRSSuspension", "MLRSChassis", "MLRS_hull", "MLRS_headlamp", "MLRS_headlamp.001", "MLRSRackYaw", "MLRS_pedestal", "MLRSRackPitch",
                                           "MLRS_rack", "mlrs_HatchHinge", "mlrs_HatchLid", "mlrs_CabinFloor", "mlrs_Board_Approach", "mlrs_Board_LadderBottom", "mlrs_Board_LadderTop",
                                           "mlrs_Board_HatchTop", "mlrs_Board_Seat", "MLRSDriverSeat", "MLRSLabelAnchor", "MLRSLeftExit", "MLRSRightExit",
                                           "MLRS_RegularTread_L", "MLRS_RegularTread_R", "MLRS_tread_L", "MLRS_tread_R", "OZ_LadderFixed_v1" };
            for (int k = 1; k <= 8; k++) { roles.Add("MLRSLoadedRocket_0" + k); roles.Add("MLRS_rocket_0" + k); roles.Add("MLRSMuzzle_0" + k); }
            var miss = new List<string>(); int comps = 0;
            foreach (var nm in roles)
            {
                var a = N(v1, nm); var b = N(v2, nm);
                if (!a) { miss.Add(nm + " (not in v1!)"); continue; }
                if (!b) { miss.Add(nm + " MISSING in v2"); continue; }
                bool byDesign = nm.StartsWith("MLRSLoadedRocket_") && P(b, v2) == "MLRS_rack";
                if (P(a, v1) != P(b, v2) && !byDesign) miss.Add($"{nm} parent {P(b, v2)} (v1 {P(a, v1)})");
                if (nm == "MLRS_rack" || nm == "mlrs_CabinFloor") continue;                                  // v2: empty nodes (the kicked node; the boarding anchor)
                foreach (var c in a.GetComponents<Component>())
                {
                    if (c is Transform || c is WreckClipBaked) continue;
                    comps++; if (!b.GetComponent(c.GetType())) miss.Add($"{nm}: {c.GetType().Name}");
                }
            }
            Check(miss.Count == 0, $"{roles.Count} role nodes and their {comps} components in v2 with v1's parents (the loaded rockets under MLRS_rack by design){(miss.Count > 0 ? "; " + string.Join(", ", miss) : "")}");
            var rack = N(v2, "MLRS_rack"); var pitch = N(v2, "MLRSRackPitch"); var yaw = N(v2, "MLRSRackYaw"); var box = N(v2, OzMlrs.FrameNode);
            Check(box && rack && !rack.GetComponent<Renderer>() && box.parent == rack, $"v2 node {OzMlrs.FrameNode} (the launcher box: it marks a v2 view) under MLRS_rack; MLRS_rack is an empty node (no team roof panel lands on the box)");
            int underRack = 0;
            for (int k = 1; k <= 8; k++)
            {
                var lr = N(v2, "MLRSLoadedRocket_0" + k); var mz = N(v2, "MLRSMuzzle_0" + k); var rk = N(v2, "MLRS_rocket_0" + k);
                if (lr && mz && rk && lr.IsChildOf(rack) && mz.parent == lr && rk.parent == lr) underRack++;
            }
            Check(underRack == 8, $"the 8 tubes (loaded rocket, its mesh, its muzzle) ride on MLRS_rack: {underRack}/8 (the launcher kick moves every rocket with its tube)");
            {   // the muzzles: on the tube axes (their -z along the rack's -z), in v1's order (01..04 the lower row from the vehicle's left, 05..08 the upper)
                var bad = new List<string>(); var lp = new List<Vector3>();
                for (int k = 1; k <= 8; k++)
                {
                    var mz = N(v2, "MLRSMuzzle_0" + k); if (!mz) { bad.Add(k + " missing"); continue; }
                    float d = Vector3.Dot(-mz.forward, -rack.forward);
                    if (d < 0.999f) bad.Add($"{k}: {Mathf.Acos(Mathf.Clamp(d, -1f, 1f)) * Mathf.Rad2Deg:0.0} deg off the tube axis");
                    lp.Add(rack.InverseTransformPoint(mz.position));
                }
                bool order = lp.Count == 8 && lp[0].x > lp[1].x && lp[1].x > lp[2].x && lp[2].x > lp[3].x && lp[4].x > lp[5].x && lp[5].x > lp[6].x && lp[6].x > lp[7].x && lp[0].y < lp[4].y - 0.3f;
                Check(bad.Count == 0 && order, $"muzzles along the tube axes, at the bore mouths, in v1's order (01..04 the lower row from the vehicle's left, +x){(bad.Count > 0 ? ": " + string.Join(", ", bad) : "")}{(order ? "" : "; ORDER WRONG")}");
            }
            {   // the hinge: the pitch joint at the box's rear-top edge, modelled raised 11.35 deg and raised OzMlrsLauncher.RestRaiseV2 more at rest (item 184: v1's tilt); the yaw axis behind the hull's middle
                float rest = Quaternion.Angle(Quaternion.identity, pitch.localRotation), k104bRest = 11.35f + OzMlrsLauncher.RestRaiseV2;
                Check(pitch && Mathf.Abs(rest - k104bRest) < 0.5f && yaw && yaw.localPosition.z > 0.9f,
                      $"the launcher hinge (MLRSRackPitch) raised {rest:0.00} deg at rest (want {k104bRest:0.00}: v1's rack tilt, item 184; the model's box 11.35 + {OzMlrsLauncher.RestRaiseV2}), the turn axis at z {(yaw ? yaw.localPosition.z : 0f):0.00} (behind the middle)");
            }
            // UnitAim / MlrsSideKick for v2 (the box inside its measured clear sweep: -7 .. +57 deg about the MODELLED rest; item 184: the drawn rest is OzMlrsLauncher.RestRaiseV2 higher)
            float top = OzMlrs.PitchMaxV2 + 8f * 1.133f + MlrsSideKick.UpDeg * 1.2f, low = OzMlrs.PitchMinV2 - MlrsSideKick.UpDeg * 0.3f;
            float k104bTop = top + OzMlrsLauncher.RestRaiseV2, k104bLow = low + OzMlrsLauncher.RestRaiseV2;
            Check(k104bTop <= 57.05f && k104bLow >= -1.51f && OzMlrs.KickBackV2 == 0f && OzMlrs.KickRollV2 <= 3f,
                  $"v2 launcher limits {OzMlrs.PitchMinV2:0.00}..{OzMlrs.PitchMaxV2:0.00} deg about its rest (raised {OzMlrsLauncher.RestRaiseV2} deg to v1's tilt): with the shot kick (8 x 1.133) and the launcher kick (5 x 1.2) at most {k104bTop:0.00} deg, at least {k104bLow:0.00} deg over the modelled rest (measured clear: -1.5..+57); kick back {OzMlrs.KickBackV2} m, roll {OzMlrs.KickRollV2} deg (measured clear: 3)");
            // item 146: the running gear planted (never under the suspension node), only the hull + launcher on it
            var susp = N(v2, "MLRSSuspension");
            var gear = v2.GetComponentsInChildren<Transform>(true).Where(t => !Under(t, "OZ_WreckFragments") && (t.name.StartsWith("TankWheel_") || t.name.StartsWith("MLRS_wheel_") || t.name.StartsWith("MLRS_RegularTread") || t.name.StartsWith("MLRS_sideplate_"))).ToList();
            int wheels = gear.Count(t => System.Text.RegularExpressions.Regex.IsMatch(t.name, "^TankWheel_\\d+$"));
            Check(susp && gear.Count >= 24 && gear.All(t => !t.IsChildOf(susp)) && wheels == 10 && N(v2, "MLRS_sideplate_left") && N(v2, "MLRS_sideplate_left").parent.name == "MLRS_RegularTread_L"
                  && N(v2, "MLRS_sideplate_right").parent.name == "MLRS_RegularTread_R" && N(v2, "MLRS_hull").IsChildOf(susp) && yaw.IsChildOf(susp),
                  $"item 146: {wheels} road wheels, both belts and both side plates (on their belts) beside the suspension node, never under it (planted); hull and launcher on MLRSSuspension");
            Check(TrackGround.Types.Contains(OzMlrs.Type) && HeavyRecoil.Types.Contains(OzMlrs.Type) && VehicleMotion.ScrollMaterials.Contains(OzMlrs.BeltMaterial) && OzV2Units.Of(OzMlrs.Type) == U,
                  "the MLRS is planted (TrackGround.Types), its hull rocks (HeavyRecoil.Types), its v2 belts roll on screen (VehicleMotion.ScrollMaterials), it is a two-model unit (OzV2Units)");
            // the clearance over the belts (the hull's lowest point over each belt, at rest; the wobble keeps it, lifted by the planting)
            {
                var hull = N(v2, "MLRS_hull").GetComponent<MeshFilter>(); var bl = N(v2, "MLRS_RegularTread_L").GetComponent<MeshFilter>(); var br = N(v2, "MLRS_RegularTread_R").GetComponent<MeshFilter>();
                if (hull && bl && br && hull.sharedMesh && hull.sharedMesh.isReadable && bl.sharedMesh.isReadable)
                {
                    var root = v2.transform;
                    Bounds BeltBox(MeshFilter f) { var b = f.sharedMesh.bounds; var w = new Bounds(root.InverseTransformPoint(f.transform.TransformPoint(b.center)), Vector3.zero); for (int i = 0; i < 8; i++) w.Encapsulate(root.InverseTransformPoint(f.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1))))); return w; }
                    var L = BeltBox(bl); var R = BeltBox(br); float lowL = 9f, lowR = 9f;
                    foreach (var vtx in hull.sharedMesh.vertices)
                    {
                        var p = root.InverseTransformPoint(hull.transform.TransformPoint(vtx));
                        if (p.x > L.min.x + 0.02f && p.x < L.max.x - 0.02f && p.z > L.min.z && p.z < L.max.z) lowL = Mathf.Min(lowL, p.y - L.max.y);
                        if (p.x > R.min.x + 0.02f && p.x < R.max.x - 0.02f && p.z > R.min.z && p.z < R.max.z) lowR = Mathf.Min(lowR, p.y - R.max.y);
                    }
                    Check(lowL > 0.015f && lowR > 0.015f && Mathf.Abs(Mathf.Min(L.min.y, R.min.y)) < 0.01f, $"at rest the hull clears its belts by {lowL:0.000} / {lowR:0.000} m (left / right; the model: 5.2-5.8 cm), the belts' bottom at {Mathf.Min(L.min.y, R.min.y):0.000} m (on the ground)");
                }
                else Check(false, "hull / belt meshes not readable for the clearance check");
            }
            // the side plates (planted with their belts): just inside the belts, well outside the hull walls (the hull rocks over them)
            {
                var hull = N(v2, "MLRS_hull").GetComponent<MeshFilter>(); var root = v2.transform; var said = new List<string>(); bool ok = true;
                foreach (var (plate, belt) in new[] { ("MLRS_sideplate_left", "MLRS_RegularTread_L"), ("MLRS_sideplate_right", "MLRS_RegularTread_R") })
                {
                    var pf = N(v2, plate) ? N(v2, plate).GetComponent<MeshFilter>() : null; var bf = N(v2, belt) ? N(v2, belt).GetComponent<MeshFilter>() : null;
                    if (!pf || !bf || !hull || !pf.sharedMesh.isReadable || !bf.sharedMesh.isReadable || !hull.sharedMesh.isReadable) { ok = false; said.Add(plate + " not readable"); continue; }
                    float px = 0f; foreach (var vtx in pf.sharedMesh.vertices) px += root.InverseTransformPoint(pf.transform.TransformPoint(vtx)).x; px /= pf.sharedMesh.vertexCount;
                    float s = Mathf.Sign(px), beltIn = 9f, wall = 0f;
                    foreach (var vtx in bf.sharedMesh.vertices) beltIn = Mathf.Min(beltIn, s * root.InverseTransformPoint(bf.transform.TransformPoint(vtx)).x);
                    var pb = pf.sharedMesh.bounds;
                    foreach (var vtx in hull.sharedMesh.vertices)
                    {
                        var q = root.InverseTransformPoint(hull.transform.TransformPoint(vtx));
                        if (q.y > 0.1f && q.y < 1.08f && Mathf.Abs(q.z) < 2.3f && s * q.x < s * px + 0.3f) wall = Mathf.Max(wall, s * q.x);
                    }
                    float toBelt = beltIn - s * px, toWall = s * px - wall;
                    ok &= toBelt > 0f && toBelt < 0.01f && toWall > 0.05f;
                    said.Add($"{plate}: {toBelt * 100f:0.0} cm inside its belt, {toWall * 100f:0.0} cm outside the hull wall");
                }
                Check(ok, "the side plates (planted on their belts): " + string.Join("; ", said) + " (the hull rocks over them: measured off the engine, never through them)");
            }
            // wreck fragments (the death's break-apart): hull, pedestal, box and both belts
            int baked = 0, holders = 0;
            foreach (var nm in new[] { "MLRS_hull", "MLRS_pedestal", OzMlrs.FrameNode, "MLRS_RegularTread_L", "MLRS_RegularTread_R" })
            {
                var p = N(v2, nm); var h = p ? p.Find("OZ_WreckFragments") : null;
                if (h) { holders++; if (WreckPieces.IsBaked(h)) baked++; }
                Check(h && h.childCount >= 2, $"wreck fragments under {nm}: {(h ? h.childCount : 0)}");
            }
            Check(baked == holders && holders == 5, $"clipped wreck bake: {baked} of {holders} holders baked");
            // renderers: shadows, materials (team colour on the red block only; neutral dark grey like v1)
            var rs = v2.GetComponentsInChildren<MeshRenderer>(true).Where(r => !Under(r.transform, "OZ_WreckFragments")).ToList();
            Check(rs.All(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off), $"{rs.Count} renderers cast shadows (as v1)");
            Check(rs.All(r => r.sharedMaterials.All(m => m && m.shader && m.shader.name.Contains("TeamLit"))), "TeamLit materials on every renderer, no empty slot");
            foreach (var mn in new[] { "mlrs_v2_body", "mlrs_v2_head", "mlrs_v2_track" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(Mats + mn + ".mat");
                bool ok = m && m.GetTexture("_BaseMap") && m.GetFloat("_LegacyHeavy") > 0.5f && m.GetFloat("_PaintFromAlpha") > 1.5f && m.GetFloat("_TeamMode") > 0.5f
                          && Mathf.Abs(m.GetVector("_HeavyLook").y - 0.0801f) < 0.01f;
                Check(ok, $"{mn}: albedo {(m && m.GetTexture("_BaseMap") ? m.GetTexture("_BaseMap").name : "none")}, _HeavyLook {(m ? m.GetVector("_HeavyLook").ToString("F4") : "-")} (y = v1's measured empty grey 0.0801: dark, never white), _DarkColor {(m ? m.GetColor("_DarkColor").ToString("F3") : "-")}");
            }
            Check(!rs.Any(r => r.name == "MLRS_rack") && N(v1, "MLRS_rack").GetComponent<MeshRenderer>(), "no renderer named MLRS_rack on v2 (TeamPaint's roof panel stays off its box: the team colour is the red block); v1 keeps its rack panel");
            // footprint (UnitView radius) = v1's
            {
                var i1 = (GameObject)PrefabUtility.InstantiatePrefab(v1); var i2 = (GameObject)PrefabUtility.InstantiatePrefab(v2);
                try
                {
                    Bounds Bx(GameObject g)
                    {
                        var rr = g.GetComponentsInChildren<Renderer>(); var bb = rr[0].bounds;
                        foreach (var r in rr) if (!(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)) bb.Encapsulate(r.bounds);
                        return bb;
                    }
                    var b1 = Bx(i1); var b2 = Bx(i2);
                    float r1 = Mathf.Clamp(Mathf.Max(b1.extents.x, b1.extents.z) * 0.8f, 1.95f, 3f), r2raw = Mathf.Clamp(Mathf.Max(b2.extents.x, b2.extents.z) * 0.8f, 1.95f, 3f);
                    // OzMlrs.ViewRadius's measure (the root frame: along the belts, across the whole width without antennas)
                    float along = 0f, across = 0f;
                    foreach (var mf in i2.GetComponentsInChildren<MeshFilter>())
                    {
                        if (!mf.GetComponent<MeshRenderer>() || !mf.sharedMesh || mf.name.Contains("Antenna")) continue;
                        bool belt = mf.name.StartsWith("MLRS_RegularTread"); var b = mf.sharedMesh.bounds;
                        for (int i = 0; i < 8; i++)
                        {
                            var c = i2.transform.InverseTransformPoint(mf.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1))));
                            across = Mathf.Max(across, Mathf.Abs(c.x)); if (belt) along = Mathf.Max(along, Mathf.Abs(c.z));
                        }
                    }
                    float r2 = Mathf.Clamp(Mathf.Max(along, across) * 0.8f, 1.95f, 3f);
                    Check(Mathf.Abs(r1 - r2) < 0.06f, $"spacing radius: v1 {r1:0.00} m, v2 {r2:0.00} m (OzMlrs.ViewRadius; by the whole box it would be {r2raw:0.00}); size v1 {b1.size.x:0.00} x {b1.size.y:0.00} x {b1.size.z:0.00} m, v2 {b2.size.x:0.00} x {b2.size.y:0.00} x {b2.size.z:0.00} m");
                }
                finally { Object.DestroyImmediate(i1); Object.DestroyImmediate(i2); }
            }
            // the ladder (lesson 1): on the hull's left side, never in the ground; its anchors on it
            var lad = N(v2, "mlrs_v2_Ladder") ? N(v2, "mlrs_v2_Ladder").GetComponent<MeshRenderer>() : null;
            var lbot = N(v2, "mlrs_Board_LadderBottom"); var ltop = N(v2, "mlrs_Board_LadderTop");
            if (lad)
            {
                var lb = lad.bounds;
                Check(lb.min.y > 0.15f && ltop && lbot && ltop.position.y <= lb.max.y + 0.15f && Mathf.Abs(ltop.position.z - lb.center.z) < 0.3f && lb.center.x > 1.5f,
                      $"ladder: bottom {lb.min.y:0.00} m above the ground, top {lb.max.y:0.00} m, on the left side (x {lb.center.x:0.00}); LadderTop {(ltop ? ltop.position.ToString("F2") : "-")}, LadderBottom {(lbot ? lbot.position.ToString("F2") : "-")}");
            }
            else Check(false, "no mlrs_v2_Ladder renderer");
            // the hatch: its lid on the hinge, outside the launcher's sweep
            {
                var hh = N(v2, "mlrs_HatchHinge"); var lid = N(v2, "mlrs_HatchLid");
                float r = hh && yaw ? Vector2.Distance(new Vector2(hh.position.x, hh.position.z), new Vector2(yaw.position.x, yaw.position.z)) : 0f;
                float sweep = 0f;
                foreach (var mf in yaw.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!mf.sharedMesh || Under(mf.transform, "OZ_WreckFragments") || !mf.sharedMesh.isReadable) continue;
                    foreach (var vtx in mf.sharedMesh.vertices) { var w = mf.transform.TransformPoint(vtx); sweep = Mathf.Max(sweep, Vector2.Distance(new Vector2(w.x, w.z), new Vector2(yaw.position.x, yaw.position.z))); }
                }
                float k93Lid = OzK93Menu.MlrsLidClear(v2, out string k93LidNote);                            // k93, item 151: the bigger turret sweeps past the hinge but high over it
                Check(hh && lid && lid.parent == hh && (r > sweep + 0.1f || k93Lid >= 0.1f), $"the hatch: lid on its hinge, the hinge {r:0.00} m from the launcher's turn axis, the launcher sweeps {sweep:0.00} m at rest pitch; {k93LidNote} (the open lid stays out of it)");
                { var k93Y = N(v2, "MLRSRackYaw"); var k93R = N(v2, "MLRS_rack");   // items 151 / 151d: the turret's one uniform scale as designed, the box not stretched (View/OzMlrs.Launcher.cs)
                  Check(k93Y && k93R && Vector3.Distance(k93Y.localScale, Vector3.one * OzMlrsLauncher.ScaleV2) < 1e-3f && Vector3.Distance(k93R.localScale, Vector3.one) < 1e-3f,
                        $"launcher size (items 151 / 151d): the modelled turret x{(k93Y ? k93Y.localScale.x : 0f):0.000} / {(k93Y ? k93Y.localScale.y : 0f):0.000} / {(k93Y ? k93Y.localScale.z : 0f):0.000} (want {OzMlrsLauncher.ScaleV2} each: one uniform scale), +{OzMlrsLauncher.LiftV2M} m, its box with the tubes x{(k93R ? k93R.localScale.x : 0f):0.000} / {(k93R ? k93R.localScale.y : 0f):0.000} / {(k93R ? k93R.localScale.z : 0f):0.000} (want 1, 1, 1: nothing stretched): as big as v1's launcher from the game camera (measured on the models at v1's rest tilt: area 0.98, length 0.97, width 1.02 of v1's)"); }
            }
            // tread: the belts' V runs along the belt (VehicleMotion scrolls _BaseMap_ST.w in each material slot's block)
            var trk = N(v2, "MLRS_RegularTread_L") ? N(v2, "MLRS_RegularTread_L").GetComponent<MeshFilter>() : null;
            float vr = 0f; if (trk && trk.sharedMesh && trk.sharedMesh.isReadable) { var uv = trk.sharedMesh.uv; if (uv.Length > 0) vr = uv.Max(u => u.y) - uv.Min(u => u.y); }
            Check(vr > 1.5f && N(v2, "MLRS_RegularTread_L").GetComponent<MeshRenderer>().sharedMaterial.name == OzMlrs.BeltMaterial, $"belt V range {vr:0.00} tiles (the model: a 4.03 m tile, about 2.6 per loop), material {OzMlrs.BeltMaterial}");
            // lamps and blinkers: v2's own data entries, every node they mount on in v2
            foreach (var (file, label) in new[] { ("OpenZ/VehicleLights/vehicle_lights", "VehicleLights"), ("OpenZ/UnitBlinkers/unit_blinkers", "UnitBlinkers") })
            {
                var ta = Resources.Load<TextAsset>(file); string txt = ta ? ta.text : "";
                int at = txt.IndexOf("\"" + U.LightsKey + "\"", System.StringComparison.Ordinal);
                bool nodes = true;
                if (at >= 0) foreach (var nm in new[] { "MLRSSuspension", "MLRSRackYaw" }) nodes &= N(v2, nm);
                Check(at >= 0 && nodes, $"{label}: entry '{U.LightsKey}' {(at >= 0 ? "present" : "MISSING")} (v2's own sockets, measured on its meshes)");
            }
            // pictures (lesson 3) and the switch
            foreach (var c in new[] { "red", "blue" })
            {
                foreach (var k in new[] { "icon", "portrait" })
                {
                    var t = Resources.Load<Texture2D>(OzMlrs.Dir + k + "_missile_launcher_v2_" + c);
                    var t1p = k == "icon" ? Resources.Load<Texture2D>("OpenZ/UI/Icons/icon_missile_launcher_" + c) : Resources.Load<Texture2D>("OpenZ/UI/PortraitsVehicles/portrait_missile_launcher_" + c);
                    Check(t && t1p && t.width == t1p.width && t.height == t1p.height, $"{k} v2 {c}: {(t ? t.width + "x" + t.height : "MISSING")} (v1 {(t1p ? t1p.width + "x" + t1p.height : "-")})");
                }
                for (int tier = 1; tier <= 4; tier++)
                {
                    var f2 = Resources.Load<Texture2D>(OzBadgeArt.Root + tier + "/face_missile_launcher_v2_" + c); var f1 = Resources.Load<Texture2D>(OzBadgeArt.Root + tier + "/face_missile_launcher_" + c);
                    Check(f2 && f1 && f2.width == f1.width, $"badge face x{tier} v2 {c}: {(f2 ? f2.width + " px" : "MISSING")} (v1 {(f1 ? f1.width + " px" : "-")})");
                }
            }
            U.Forget();
            Check(OzMlrs.Version == 2 ? U.Drawn == 2 : U.Drawn == 1, $"the switch OzMlrs.Version = {OzMlrs.Version}: drawn model v{U.Drawn}");
            var icon = UnitArt.Icon(OzMlrs.Type, Team.Red);
            Check(OzMlrs.Version != 2 || (U.PrefabFor(OzMlrs.V1Key, v1) == U.PrefabV2 && icon && icon.name.Contains("v2") && OzBadgeArt.FaceFor(OzMlrs.Type, Team.Red).Contains("missile_launcher_v2")),
                  $"with the switch on v2: Z_missile_launcher -> Z_missile_launcher_v2, the production picture {(icon ? icon.name : "none")}, the badge face {OzBadgeArt.FaceFor(OzMlrs.Type, Team.Red)}");
            var tv1 = OzV2Units.TileArt(OzMlrs.TileV1, Team.Red); var tv2 = OzV2Units.TileArt(OzMlrs.TileV2, Team.Red);
            Check(tv1 && tv2 && tv1 != tv2 && !tv1.name.Contains("v2"), $"testing panel tiles: {OzV2Units.TileLabel(OzMlrs.TileV1)} = {(tv1 ? tv1.name : "none")}, {OzV2Units.TileLabel(OzMlrs.TileV2)} = {(tv2 ? tv2.name : "none")}");
            Write(lines, pass, fail);
        }

        static void Write(List<string> lines, int pass, int fail)
        {
            string head = $"[OzMlrsV2] parity check {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail} checks";
            Directory.CreateDirectory(ProofDir);
            File.WriteAllText(Path.Combine(ProofDir, "parity_check.txt"), head + "\n" + string.Join("\n", lines) + "\n");
            Debug.Log(head + "\n" + string.Join("\n", lines));
        }

        // ---------------------------------------------------------------- 3 Play proof, 4 .. 16 films and tests
        [MenuItem(M + "3 Play proof (L02 Desert, side by side)", false, 3)] static void ProofL02() => Proof("L02", L02);
        [MenuItem(M + "3 Play proof (L17 Car Park, side by side)", false, 4)] static void ProofL17() => Proof("L17", L17);
        static bool Ready()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[OzMlrsV2] stop Play first"); return false; }
            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }
        static void Proof(string job, string scene)
        {
            if (!Ready()) return;
            PlayerPrefs.SetString(MlrsV2Proof.AutoPref, job); PlayerPrefs.Save();
            EditorSceneManager.OpenScene(scene);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(M + "4 Film: the tread at the track ends (L02, RTS camera, crops 0.1 s apart)", false, 20)] static void Tread() => Film("tread");
        [MenuItem(M + "5 Film: a battle of v2 MLRS (L02)", false, 21)] static void Battle() => Film("battle");
        [MenuItem(M + "6 Film: a robot boards an empty v2 (L02)", false, 22)] static void Board() => Film("board");
        [MenuItem(M + "6 Film: a robot boards an empty v2, close camera 32 m (L02)", false, 22)] static void BoardClose() => Film("board_close");
        [MenuItem(M + "7 Film: one v2 fights to its death (L02)", false, 23)] static void Fight() => Film("fight");
        [MenuItem(M + "11 Test: the launcher never shakes between targets (L02)", false, 50)] static void Turret() => Film("turret");
        [MenuItem(M + "14 Test + film: v1 and v2 side by side, the tracks stay on the ground, only the hull rocks (L02, item 146)", false, 53)] static void Plant() => Film("plant");
        [MenuItem(M + "15 Test + film: every rocket leaves its own tube (v1 and v2, close, crops 1/30 s apart, L02)", false, 54)] static void Rockets() => Film("rockets");
        [MenuItem(M + "16 Test + film: v1 and v2 side by side, the launcher kick and the hull rock at each salvo (L02)", false, 55)] static void Launcher() => Film("launcher");
        static void Film(string job)
        {
            if (!Ready()) return;
            PlayerPrefs.SetString(HeavyV2Films.SubjectPref, OzMlrs.Type);
            PlayerPrefs.SetString(HeavyV2Films.AutoPref, job); PlayerPrefs.Save();
            EditorSceneManager.OpenScene(L02);
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
