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
    /// Mech v2 (1.2 v2 units), the editor side; the MLRS v2's menus (Editor/OzMlrsV2Setup.cs) for the mech.
    ///  1 Build: the v2 view prefab Resources/OpenZ/MechV2/Z_medium_v2 from Models/Legacy/mech_v2.glb, made like Prefabs/Z_medium (root +
    ///    TeamPaint copied from v1, the GLB's root MechRoot as "Model" at (0, 0, 0) with v1's scale 0.87356 and v1's MechWalk copied onto it:
    ///    v2's soles touch the ground there; v1's offset would float them 3.5 cm and shift them 12 cm), the GLB's node tree with v1's names, the
    ///    TeamLit materials mech_v2_body / _limbs and the orange side lamps' mech_v2_lamp, v1's renderer settings; then its wreck fragments
    ///    (files mechv2_* beside v1's medium_*: v1's are never touched): the game's own cut of the three largest leg parts (the upper body flies
    ///    whole at the death, the Zod turret toss), then the clipped bake. v1 stays as it is.
    ///  2 Parity check (counted, edit mode): v1's skeleton and renderer names with v1's parents, identity rests, MechWalk on the model, the
    ///    soles on the ground, the cannon and pod muzzles, the hatch, the ladder, materials, wreck, footprint, lamp / blinker data, pictures,
    ///    the switch and the testing panel's tiles.
    ///  3 Play proof L02 / L17: MechV2Proof (v1 and v2 side by side; waist turn, cannon kick and spin, the upper body's kick and the body's
    ///    rock, rockets from the pods, the feet planted on the ground every frame while it fires, walks and turns, boarding, damage, death).
    ///  4 .. 15 the films and tests (HeavyV2Films for the mech, L02, 1920 x 1080 with sound).
    /// </summary>
    public static class OzMechV2Setup
    {
        public const string Glb = "Assets/OpenZ/Models/Legacy/mech_v2.glb", V1Prefab = "Assets/OpenZ/Prefabs/Z_medium.prefab",
                            Folder = "Assets/OpenZ/Resources/OpenZ/MechV2", PrefabPath = Folder + "/Z_medium_v2.prefab", Mats = "Assets/OpenZ/Materials/Units/";
        /// <summary>The wreck fragment file key: mechv2_* (OpenZWreckFragments clears "&lt;key&gt;_*" before it bakes; "medium_*" would take v1's files).</summary>
        public const string FragKey = "mechv2";
        const string M = "OpenZ/Mech v2/";
        const string L02 = "Assets/OpenZ/Scenes/L02_Desert.unity", L17 = "Assets/OpenZ/Scenes/L17_CarPark.unity";
        static readonly string ProofDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/v2_units/mech/proof"));
        static OzUnitModels U => OzMech.Models;
        /// <summary>The joints v1's code turns or reads by name (MechWalk, UnitAim, MechRockets, BoardingView, RoundHatch, UnitView): identity rests.</summary>
        static readonly string[] Joints = { "MechPelvis", "MechHip_L", "MechHip_R", "MechKnee_L", "MechKnee_R", "MechAnkle_L", "MechAnkle_R", "MechWaistYaw",
                                            "MechGunShoulder", "MechGunElbow", "MechGunRecoil", "MechBarrelSpin", "MechMuzzle", "MechClawShoulder", "MechClawElbow",
                                            "MechClawWrist", "MechClawFinger_A", "MechClawFinger_B", "MechRocketPod_L", "MechRocketPod_R", "mech_HatchHinge" };

        static Transform Deep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Deep(c, name); if (f) return f; }
            return null;
        }
        static void Layer(Transform t, int layer) { t.gameObject.layer = layer; foreach (Transform c in t) Layer(c, layer); }
        static bool Under(Transform t, string name) { for (var p = t; p; p = p.parent) if (p.name == name) return true; return false; }

        // ---------------------------------------------------------------- 1 build
        // menu 1 is Editor/OzMechV2bSetup.cs now (the v2 = the v1 model with its own texture); this build of the first v2 models stays, off the menu
        public static void Build()
        {
            string r = BuildNow();
            Debug.Log("[OzMechV2] " + r);
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
                ["MechV2_body"] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "mech_v2_body.mat"),
                ["MechV2_limbs"] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "mech_v2_limbs.mat"),
                ["MechV2_lamp"] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "mech_v2_lamp.mat"),
            };
            foreach (var kv in mats) if (!kv.Value) return "FAILED: material for " + kv.Key + " missing in " + Mats;
            var v1Model = v1.transform.Find("Model");
            var v1Body = Deep(v1.transform, "Mech_body"); var v1Mr = v1Body ? v1Body.GetComponent<MeshRenderer>() : null;
            var v1Walk = v1Model ? v1Model.GetComponent<MechWalk>() : null;
            if (!v1Model || !v1Mr || !v1Walk) return "FAILED: Z_medium has no Model with MechWalk / no Mech_body renderer";
            var sb = new StringBuilder();
            var inst = Object.Instantiate(glb);
            GameObject root = null;
            try
            {
                var pelvis = Deep(inst.transform, "MechPelvis");
                if (!pelvis || !pelvis.parent) return "FAILED: the GLB has no MechPelvis under a root node";
                var model = pelvis.parent;                                                                // the glTF scene's root node "MechRoot"
                root = new GameObject(OzMech.V2Key) { layer = v1.layer };
                var tp = root.AddComponent<TeamPaint>();
                var tp1 = v1.GetComponent<TeamPaint>(); if (tp1) EditorUtility.CopySerialized(tp1, tp);
                model.SetParent(root.transform, false); model.name = "Model";
                model.localPosition = Vector3.zero; model.localRotation = v1Model.localRotation; model.localScale = v1Model.localScale;
                int n = 0, byName = 0; var unknown = new List<string>();
                foreach (var mr in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var src = mr.sharedMaterials; var all = new Material[src.Length];
                    for (int i = 0; i < src.Length; i++)
                    {
                        string mn = src[i] ? src[i].name : "";
                        int k = mn.IndexOf(' '); if (k > 0) mn = mn.Substring(0, k);
                        if (mats.TryGetValue(mn, out var m)) { all[i] = m; byName++; continue; }
                        // fallback by the renderer's name (the GLB's materials, v2_units/mech/V1_PARITY.md section 2)
                        string nm = mr.name;
                        all[i] = nm.StartsWith("Mech_v2_lamp") ? mats["MechV2_lamp"]
                               : nm == "Mech_body" || nm == "Mech_pelvis" || nm == "Mech_v2_gunMount" || nm == "Mech_v2_hipAxles" || nm.StartsWith("mech_v2_") ? mats["MechV2_body"] : mats["MechV2_limbs"];
                        unknown.Add(nm + "[" + i + "] '" + (src[i] ? src[i].name : "none") + "'");
                    }
                    mr.sharedMaterials = all;
                    mr.shadowCastingMode = mr.name.StartsWith("Mech_v2_lamp") ? UnityEngine.Rendering.ShadowCastingMode.Off : v1Mr.shadowCastingMode;   // the lamp glass quads on the window frame: no shadow of their own (as v1's lamps)
                    mr.receiveShadows = v1Mr.receiveShadows;
                    mr.lightProbeUsage = v1Mr.lightProbeUsage; mr.reflectionProbeUsage = v1Mr.reflectionProbeUsage;
                    mr.motionVectorGenerationMode = v1Mr.motionVectorGenerationMode; mr.allowOcclusionWhenDynamic = v1Mr.allowOcclusionWhenDynamic;
                    n++;
                }
                foreach (var c in model.GetComponentsInChildren<Component>(true))                           // only transforms, filters, renderers (as v1)
                    if (!(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer)) Object.DestroyImmediate(c);
                var walk = model.gameObject.AddComponent<MechWalk>();
                EditorUtility.CopySerialized(v1Walk, walk);                                                 // v1's walk, its sign (1) and settings
                Layer(model, v1Model.gameObject.layer);
                sb.Append($"prefab: {n} renderers, {byName} material slots by the GLB's material names{(unknown.Count > 0 ? ", by renderer name: " + string.Join(", ", unknown) : "")}; " +
                          $"Model at (0, 0, 0) x {model.localScale.x:0.00000} with MechWalk (sign {walk.sign}); ");
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/OpenZ/Resources/OpenZ", "MechV2");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok) return "FAILED: the prefab could not be saved at " + PrefabPath;
                sb.Append("saved ").Append(PrefabPath).Append("; ");
            }
            finally
            {
                if (root) Object.DestroyImmediate(root);
                if (inst) Object.DestroyImmediate(inst);
            }
            // wreck fragments: OpenZWreckFragments' own cut of the three largest parts outside the upper body (its Bake is private: called by name),
            // then the clipped bake
            var bake = typeof(OpenZWreckFragments).GetMethod("Bake", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string), typeof(string) }, null);
            sb.Append("wreck fragments: ").Append(bake != null ? (string)bake.Invoke(null, new object[] { PrefabPath, FragKey }) : "FAILED (OpenZWreckFragments.Bake not found)").Append("; ");
            AssetDatabase.SaveAssets();
            sb.Append("clipped: ").Append(OpenZWreckClipBake.ClipPrefab(PrefabPath, FragKey));
            AssetDatabase.SaveAssets();
            U.Forget();
            return sb.ToString();
        }

        // ---------------------------------------------------------------- 2 parity check
        // menu 2 is Editor/OzMechV2bSetup.cs now; this parity of the first v2 models stays, off the menu
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
            // the nodes v1's code reads by name (MechWalk, UnitView, UnitAim, MechRockets, BoardingView, RoundHatch, VehicleLights, the death),
            // in v2 with v1's parents and components (the boarding anchor mech_CabinFloor is an empty node on v2: no floor plate is drawn)
            var roles = new List<string> { "Model", "MechPelvis", "Mech_pelvis", "MechHip_L", "MechHip_R", "Mech_thighL", "Mech_thighR", "MechKnee_L", "MechKnee_R",
                                           "Mech_shinL", "Mech_shinR", "MechAnkle_L", "MechAnkle_R", "Mech_footL", "Mech_footR", "MechWaistYaw", "Mech_body",
                                           "MechGunShoulder", "Mech_gunUpper", "MechGunElbow", "MechGunRecoil", "Mech_gun", "MechBarrelSpin", "Mech_spin", "MechMuzzle",
                                           "MechClawShoulder", "Mech_clawUpper", "MechClawElbow", "Mech_clawLower", "MechClawWrist", "Mech_claw", "MechClawFinger_A",
                                           "Mech_jawA", "MechClawFinger_B", "Mech_jawB", "MechRocketPod_L", "MechRocketPod_R", "MechRocketMuzzle_L0", "MechRocketMuzzle_L1",
                                           "MechRocketMuzzle_R2", "MechRocketMuzzle_R3", "mech_HatchHinge", "mech_CabinFloor", "mech_Board_Approach", "mech_Board_LadderBottom",
                                           "mech_Board_LadderTop", "mech_Board_HatchTop", "mech_Board_Seat", "MechDriverSeat", "MechLabelAnchor", "MechLeftExit",
                                           "MechRightExit", "OZ_LadderFixed_v1" };
            var miss = new List<string>(); int comps = 0;
            foreach (var nm in roles)
            {
                var a = N(v1, nm); var b = N(v2, nm);
                if (!a) { miss.Add(nm + " (not in v1!)"); continue; }
                if (!b) { miss.Add(nm + " MISSING in v2"); continue; }
                if (P(a, v1) != P(b, v2)) miss.Add($"{nm} parent {P(b, v2)} (v1 {P(a, v1)})");
                if (nm == "mech_CabinFloor" || nm == "OZ_LadderFixed_v1") continue;                      // v2: empty nodes (the boarding anchor; the ladder fix's marker)
                foreach (var c in a.GetComponents<Component>())
                {
                    if (c is Transform || c is WreckClipBaked) continue;
                    comps++; if (!b.GetComponent(c.GetType())) miss.Add($"{nm}: {c.GetType().Name}");
                }
            }
            Check(miss.Count == 0, $"{roles.Count} role nodes (v1's skeleton, renderers, sockets and anchors) and their {comps} components in v2 with v1's parents{(miss.Count > 0 ? "; " + string.Join(", ", miss) : "")}");
            {   // every joint at an identity rest (MechWalk reads the rest pose; UnitAim, MechRockets, BoardingView turn them from rest)
                var bad = Joints.Where(j => !N(v2, j) || Quaternion.Angle(Quaternion.identity, N(v2, j).localRotation) > 0.01f).ToList();
                Check(bad.Count == 0, $"{Joints.Length} joints at an identity rest (as v1){(bad.Count > 0 ? ": NOT " + string.Join(", ", bad) : "")}");
            }
            {   // the walk: MechWalk on the model, v1's sign; the model at (0, 0, 0) x v1's scale
                var m2 = v2.transform.Find("Model"); var m1 = v1.transform.Find("Model");
                var w2 = m2 ? m2.GetComponent<MechWalk>() : null; var w1 = m1 ? m1.GetComponent<MechWalk>() : null;
                Check(w2 && w1 && Mathf.Approximately(w2.sign, w1.sign) && m2.localPosition.sqrMagnitude < 1e-8f && Mathf.Abs(m2.localScale.x - m1.localScale.x) < 1e-5f,
                      $"MechWalk on v2's Model (sign {(w2 ? w2.sign : 0f)}, v1 {(w1 ? w1.sign : 0f)}); Model at {(m2 ? m2.localPosition.ToString("F3") : "-")} x {(m2 ? m2.localScale.x : 0f):0.00000} (v1 {(m1 ? m1.localPosition.ToString("F3") : "-")} x {(m1 ? m1.localScale.x : 0f):0.00000})");
            }
            // the feet: both soles on the ground at rest (MechWalk plants the ankle at the ground + its rest height over the root), the legs within
            // MechWalk's soft-reach zone at rest
            float Low(GameObject g, string foot)
            {
                var mf = N(g, foot) ? N(g, foot).GetComponent<MeshFilter>() : null;
                if (!mf || !mf.sharedMesh || !mf.sharedMesh.isReadable) return float.NaN;
                float y = 9f; foreach (var v in mf.sharedMesh.vertices) y = Mathf.Min(y, g.transform.InverseTransformPoint(mf.transform.TransformPoint(v)).y);
                return y;
            }
            float l2 = Low(v2, "Mech_footL"), r2 = Low(v2, "Mech_footR"), l1 = Low(v1, "Mech_footL"), r1 = Low(v1, "Mech_footR");
            Check(Mathf.Abs(l2) < 0.01f && Mathf.Abs(r2) < 0.01f, $"soles at rest over the ground: v2 left {l2:0.000} m, right {r2:0.000} m (v1 {l1:0.000} / {r1:0.000}: its Model sits 3.5 cm up)");
            foreach (var (g, tag) in new[] { (v2, "v2"), (v1, "v1") })
            {
                var sides = new List<string>(); bool ok = true;
                foreach (var s in new[] { "L", "R" })
                {
                    var hp = N(g, "MechHip_" + s); var kn = N(g, "MechKnee_" + s); var an = N(g, "MechAnkle_" + s);
                    if (!hp || !kn || !an) { ok = false; continue; }
                    float A = Vector3.Distance(hp.position, kn.position), B = Vector3.Distance(kn.position, an.position), d = Vector3.Distance(hp.position, an.position);
                    float share = d / Mathf.Max(1e-4f, A + B); ok &= share < 0.96f;
                    sides.Add($"{s}: thigh {A:0.000}, shin {B:0.000}, hip to ankle {share * 100f:0} % of the reach");
                }
                Check(ok, $"{tag} legs at rest inside MechWalk's soft-reach zone (under 96 %): {string.Join("; ", sides)}");
            }
            {   // the cannon: the muzzle on the gun's axis (its -z), in front of the body; the 4 pod muzzles in front of their tubes, L on the mech's left (+x)
                var mz = N(v2, "MechMuzzle"); var rc = N(v2, "MechGunRecoil"); var body = N(v2, "Mech_body").GetComponent<MeshRenderer>();
                bool axis = mz && rc && Vector3.Dot(-mz.forward, -rc.forward) > 0.999f;
                float front = mz && body ? v2.transform.InverseTransformPoint(mz.position).z - v2.transform.InverseTransformPoint(body.bounds.min).z : 9f;
                Check(axis && mz.localPosition.z < 0f, $"MechMuzzle on the cannon's axis (local {(mz ? mz.localPosition.ToString("F3") : "-")}), {(front < 0 ? "in front of" : "behind")} the body's front by {Mathf.Abs(front):0.00} m");
                var pods = new List<string>(); bool podOk = true;
                foreach (var (mzn, pod, left) in new[] { ("MechRocketMuzzle_L0", "MechRocketPod_L", true), ("MechRocketMuzzle_L1", "MechRocketPod_L", true), ("MechRocketMuzzle_R2", "MechRocketPod_R", false), ("MechRocketMuzzle_R3", "MechRocketPod_R", false) })
                {
                    var m = N(v2, mzn); var tube = N(v2, pod.Replace("MechRocketPod", "MechRocketTubes_v2"));
                    var tm = tube ? tube.GetComponent<MeshFilter>() : null;
                    float x = m ? v2.transform.InverseTransformPoint(m.position).x : 0f;
                    float ahead = m && tm && tm.sharedMesh ? tm.sharedMesh.bounds.min.z - m.localPosition.z : -9f;   // the muzzle at or in front of the tube's front face (-z = front)
                    bool ok = m && m.parent && m.parent.name == pod && (left ? x > 0.3f : x < -0.3f) && ahead > -0.02f; podOk &= ok;
                    pods.Add($"{mzn} x {x:0.00} m, {ahead * 100f:0.0} cm in front of its tube");
                }
                Check(podOk, "the rocket pods: " + string.Join("; ", pods) + " (MechRockets: rocket 1 from the left pod, its twin from the right)");
            }
            {   // the hand gun's two empty nodes (View/MechHandGunView.cs): under MechGunRecoil (the gun body slides back on a shot; MechBarrelSpin
                // spins), its LAST children (UnitView takes the first "...Muzzle" node as the unit's muzzle: MechMuzzle must stay first), identity
                // rests; HandGunMuzzle on the barrel's axis (within 3 cm) at or in front of the barrel tip; HandGunCasingEject on the outer side (local x < 0)
                var rc = N(v2, "MechGunRecoil"); var hm = N(v2, MechHandGunView.MuzzleNode); var he = N(v2, MechHandGunView.EjectNode); var mz = N(v2, "MechMuzzle");
                bool ok = rc && hm && he && mz && hm.parent == rc && he.parent == rc && hm.GetSiblingIndex() >= rc.childCount - 2 && he.GetSiblingIndex() >= rc.childCount - 2
                          && Quaternion.Angle(Quaternion.identity, hm.localRotation) < 0.01f && Quaternion.Angle(Quaternion.identity, he.localRotation) < 0.01f
                          && hm.GetComponents<Component>().Length == 1 && he.GetComponents<Component>().Length == 1;
                float off = 9f, ahead = -9f, side = 9f; string first = "-";
                if (ok)
                {
                    Vector3 a = rc.InverseTransformPoint(mz.position), h = rc.InverseTransformPoint(hm.position);
                    off = new Vector2(h.x - a.x, h.y - a.y).magnitude * rc.lossyScale.x;
                    float tip = 0f;
                    foreach (var mf in rc.GetComponentsInChildren<MeshFilter>(true))
                        if (mf.sharedMesh) { var b = mf.sharedMesh.bounds; foreach (var c in new[] { b.min, b.max, new Vector3(b.min.x, b.min.y, b.max.z), new Vector3(b.max.x, b.max.y, b.min.z) }) tip = Mathf.Min(tip, rc.InverseTransformPoint(mf.transform.TransformPoint(c)).z); }
                    ahead = (tip - h.z) * rc.lossyScale.x; side = he.localPosition.x;
                    var fm = v2.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Contains("Muzzle") && !t.name.Contains("_")); first = fm ? fm.name : "none";
                }
                Check(ok && off <= 0.03f && ahead >= -0.001f && side < 0f && first == "MechMuzzle",
                      $"the hand gun's nodes (k86c): {MechHandGunView.MuzzleNode} and {MechHandGunView.EjectNode} {(ok ? "empty, at an identity rest, the last children of MechGunRecoil" : "MISSING or not the last empty children of MechGunRecoil")}; the muzzle {off * 100f:0.0} cm off the barrel's axis, {ahead * 100f:0.0} cm in front of the barrel tip; the casing port at local x {side:0.000} (outer side); the unit's first muzzle node {first}");
            }
            {   // lesson 5: UnitAim.Steady's minimum range (1.25 x the muzzle's flat reach from the waist's turn axis, at least 4 m) against v2's cannon
                var wy = N(v2, "MechWaistYaw"); var mz = N(v2, "MechMuzzle"); var wy1 = N(v1, "MechWaistYaw"); var mz1 = N(v1, "MechMuzzle");
                float Reach(Transform a, Transform b) { if (!a || !b) return -1f; var d = b.position - a.position; d.y = 0f; return d.magnitude; }
                float reach2 = Reach(wy, mz), reach1 = Reach(wy1, mz1);
                Check(reach2 > 0f && UnitAim.SteadyRangeK * reach2 <= 4f, $"the waist's steady aim (lesson 5): the cannon's muzzle {reach2:0.00} m from the waist axis (v1 {reach1:0.00} m) x {UnitAim.SteadyRangeK} = {UnitAim.SteadyRangeK * reach2:0.00} m, inside UnitAim.Steady's 4 m minimum range (its Newton step never overshoots past it); the world-heading combat slew serves the mech's waist (every vehicle turret but the MLRS rack)");
            }
            // registries: the mech in HeavyRecoil (both models: the upper body's kick, the body's rock), a two-model unit
            Check(HeavyRecoil.Types.Contains(OzMech.Type) && OzV2Units.Of(OzMech.Type) == U && HeavyRecoil.DesignHeadSlide(OzMech.Type, 2) > 0f && HeavyRecoil.DesignBodyRock(OzMech.Type) > 0f,
                  $"the mech kicks and rocks (HeavyRecoil.Types: upper body {HeavyRecoil.DesignHeadSlide(OzMech.Type, 1):0.00} / {HeavyRecoil.DesignHeadSlide(OzMech.Type, 2):0.00} + {HeavyRecoil.DesignHeadRock(OzMech.Type):0.0} deg, body {HeavyRecoil.DesignBodyRock(OzMech.Type):0.00} deg + {HeavyRecoil.DesignBodySlide(OzMech.Type):0.000} m), it is a two-model unit (OzV2Units)");
            // wreck fragments (the death's break-apart): the game's cut of the three largest leg parts (the upper body flies whole)
            int holders = 0, baked = 0; var cut = new List<string>();
            foreach (var h in v2.GetComponentsInChildren<Transform>(true).Where(t => t.name == "OZ_WreckFragments"))
            {
                holders++; if (WreckPieces.IsBaked(h)) baked++;
                cut.Add($"{(h.parent ? h.parent.name : "-")} ({h.childCount})");
            }
            Check(holders >= 2 && cut.All(c => !c.StartsWith("Mech_body")), $"wreck fragments: {holders} parts cut: {string.Join(", ", cut)} (v1: the two thighs; the upper body is tossed whole as v1's)");
            Check(baked == holders && holders > 0, $"clipped wreck bake: {baked} of {holders} holders baked");
            // renderers: shadows, materials (team colour on the red shoulder blocks and the red top plate only; neutral dark grey like v1)
            var rs = v2.GetComponentsInChildren<MeshRenderer>(true).Where(r => !Under(r.transform, "OZ_WreckFragments")).ToList();
            Check(rs.Where(r => !r.name.StartsWith("Mech_v2_lamp")).All(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off), $"{rs.Count} renderers cast shadows (as v1; the two lamp glass quads none)");
            Check(rs.All(r => r.sharedMaterials.All(m => m && m.shader && m.shader.name.Contains("TeamLit"))), "TeamLit materials on every renderer, no empty slot");
            foreach (var mn in new[] { "mech_v2_body", "mech_v2_limbs" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(Mats + mn + ".mat");
                bool ok = m && m.GetTexture("_BaseMap") && m.GetFloat("_LegacyHeavy") > 0.5f && m.GetFloat("_PaintFromAlpha") > 1.5f && m.GetFloat("_TeamMode") > 0.5f
                          && Mathf.Abs(m.GetVector("_HeavyLook").y - 0.0819f) < 0.01f;
                Check(ok, $"{mn}: albedo {(m && m.GetTexture("_BaseMap") ? m.GetTexture("_BaseMap").name : "none")}, _HeavyLook {(m ? m.GetVector("_HeavyLook").ToString("F4") : "-")} (y = v1 mech's measured empty grey 0.0819: dark, never white), _DarkColor {(m ? m.GetColor("_DarkColor").ToString("F3") : "-")}");
            }
            {
                // top plate, as v1), none on the limbs (legs, arms, gun, claw)
                float Share(string mn)
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(Mats + mn + ".mat"); var t = m ? m.GetTexture("_BaseMap") : null;
                    if (!t) return -1f;
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    try
                    {
                        if (!tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(t)))) return -1f;
                        var px = tex.GetPixels32(); int n = 0;
                        foreach (var c in px) if (c.a >= 224) n++;
                        return n / (float)px.Length;
                    }
                    finally { Object.DestroyImmediate(tex); }
                }
                float tb = Share("mech_v2_body"), tl = Share("mech_v2_limbs");
                Check(tb > 0.04f && tb < 0.30f && tl == 0f, $"team area (item 156): {tb * 100f:0.0} % of mech_v2_body's albedo (the red shoulder blocks and the red top plate), {tl * 100f:0.0} % of mech_v2_limbs' (none: legs, arms, gun and claw keep their own paint)");
            }
            {
                var lm = AssetDatabase.LoadAssetAtPath<Material>(Mats + "mech_v2_lamp.mat");
                string ln = lm ? lm.name : "";
                Check(lm && !ln.Contains("redGlow") && !ln.Contains("OwnerGlow") && !ln.ToLower().Contains("owner") && lm.GetFloat("_TeamMode") > 0.5f,
                      $"mech_v2_lamp: the orange side lamps keep their own colour (never an owner glow: TeamPaint tints *redGlow* / *Owner* parts), base {(lm ? lm.GetColor("_BaseColor").ToString("F2") : "-")}, emission {(lm ? lm.GetColor("_EmissionColor").ToString("F2") : "-")}");
            }
            {
                var bm = N(v2, "Mech_body").GetComponent<MeshRenderer>(); var bm1 = N(v1, "Mech_body").GetComponent<MeshRenderer>();
                Check(bm && bm1 && OzV2Units.NoRoofPanel("Mech_body", bm.sharedMaterial) && !OzV2Units.NoRoofPanel("Mech_body", bm1.sharedMaterial),
                      "no team roof panel on v2's body (its team colour is painted in its albedo: the red shoulder blocks and the red top plate); v1 keeps its roof panel; v2 has no MechRocketHousing_* (v1's pod panels)");
            }
            // footprint (UnitView radius from the renderers' bounds at the bind) = v1's, facing north and turned 45 deg
            {
                float R(GameObject g, float yaw)
                {
                    var i = (GameObject)PrefabUtility.InstantiatePrefab(g);
                    try
                    {
                        i.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                        var rr = i.GetComponentsInChildren<Renderer>(); var bb = rr[0].bounds;
                        foreach (var r in rr) if (!(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)) bb.Encapsulate(r.bounds);
                        return Mathf.Clamp(Mathf.Max(bb.extents.x, bb.extents.z) * 0.8f, 1.95f, 3f);
                    }
                    finally { Object.DestroyImmediate(i); }
                }
                float a1 = R(v1, 0f), a2 = R(v2, 0f), b1 = R(v1, 45f), b2 = R(v2, 45f);
                Check(Mathf.Abs(a1 - a2) < 0.06f && Mathf.Abs(b1 - b2) < 0.06f, $"spacing radius (UnitView): facing north v1 {a1:0.00} m, v2 {a2:0.00} m; turned 45 deg v1 {b1:0.00} m, v2 {b2:0.00} m");
            }
            // the ladder (lesson 1): the rungs flush on the body's back, the steps on the pelvis, never in the ground; the anchors behind them
            {
                var lad = N(v2, "mech_v2_Ladder") ? N(v2, "mech_v2_Ladder").GetComponent<MeshRenderer>() : null;
                var steps = N(v2, "mech_v2_PelvisSteps") ? N(v2, "mech_v2_PelvisSteps").GetComponent<MeshRenderer>() : null;
                var lbot = N(v2, "mech_Board_LadderBottom"); var ltop = N(v2, "mech_Board_LadderTop"); var body = N(v2, "Mech_body").GetComponent<MeshRenderer>();
                if (lad && steps && lbot && ltop && body)
                {
                    var lb = lad.bounds; var sbx = steps.bounds; float rear = body.bounds.max.z;
                    bool ok = lb.min.y > 0.5f && sbx.min.y > 0.5f && lb.max.z <= rear + 0.15f && ltop.position.y <= lb.max.y + 0.6f && lbot.position.y < 0.2f && lbot.position.z > lb.center.z;
                    Check(ok, $"ladder: rungs {lb.min.y:0.00}..{lb.max.y:0.00} m up, reaching {(lb.max.z - rear) * 100f:0.0} cm past the body's rear face; pelvis steps from {sbx.min.y:0.00} m; LadderTop {ltop.position.ToString("F2")}, LadderBottom {lbot.position.ToString("F2")} (behind the rungs, on the ground)");
                }
                else Check(false, "no mech_v2_Ladder / mech_v2_PelvisSteps renderer or ladder anchors");
            }
            // the hatch: the modelled round hatch lid on its rear-edge hinge, the frame under it; RoundHatch uses it (nothing built)
            {
                var fr = N(v2, OzMech.FrameNode); var hh = N(v2, "mech_HatchHinge"); var lid = N(v2, "mech_v2_HatchLid"); var top = N(v2, "mech_Board_HatchTop");
                var lr = lid ? lid.GetComponent<MeshRenderer>() : null;
                bool ok = fr && hh && lid && lr && top && lid.parent == hh && fr.parent && fr.parent.name == "MechWaistYaw" && OzV2Units.FrameNodeFor("mech_") == OzMech.FrameNode
                          && hh.position.z > lr.bounds.center.z && Vector2.Distance(new Vector2(fr.position.x, fr.position.z), new Vector2(top.position.x, top.position.z)) < 0.1f;
                Check(ok, $"the round hatch: lid mech_v2_HatchLid on mech_HatchHinge at its rear edge (hinge z {(hh ? hh.position.z : 0f):0.00} behind the lid's middle {(lr ? lr.bounds.center.z : 0f):0.00}), frame {OzMech.FrameNode} under it, Board_HatchTop over it; RoundHatch uses the modelled hatch (OzV2Units.FrameNodeFor(\"mech_\"))");
            }
            // lamps and blinkers: v2's own data entries, every node they mount on in v2
            foreach (var (file, label) in new[] { ("OpenZ/VehicleLights/vehicle_lights", "VehicleLights"), ("OpenZ/UnitBlinkers/unit_blinkers", "UnitBlinkers") })
            {
                var ta = Resources.Load<TextAsset>(file); string txt = ta ? ta.text : "";
                int at = txt.IndexOf("\"" + U.LightsKey + "\"", System.StringComparison.Ordinal);
                Check(at >= 0 && N(v2, "MechWaistYaw"), $"{label}: entry '{U.LightsKey}' {(at >= 0 ? "present" : "MISSING")} (v2's own sockets, measured on its meshes)");
            }
            // pictures (lesson 3) and the switch
            foreach (var c in new[] { "red", "blue" })
            {
                foreach (var (k, v1Path) in new[] { ("icon", "OpenZ/UI/Icons/icon_medium_tank_"), ("portrait", "OpenZ/UI/PortraitsVehicles/portrait_medium_mech_") })
                {
                    var t = Resources.Load<Texture2D>(OzMech.Dir + k + "_medium_mech_v2_" + c); var t1p = Resources.Load<Texture2D>(v1Path + c);
                    Check(t && t1p && t.width == t1p.width && t.height == t1p.height, $"{k} v2 {c}: {(t ? t.width + "x" + t.height : "MISSING")} (v1 {(t1p ? t1p.width + "x" + t1p.height : "-")})");
                }
                for (int tier = 1; tier <= 4; tier++)
                {
                    var f2 = Resources.Load<Texture2D>(OzBadgeArt.Root + tier + "/face_medium_v2_" + c); var f1 = Resources.Load<Texture2D>(OzBadgeArt.Root + tier + "/face_medium_" + c);
                    Check(f2 && f1 && f2.width == f1.width, $"badge face x{tier} v2 {c}: {(f2 ? f2.width + " px" : "MISSING")} (v1 {(f1 ? f1.width + " px" : "-")})");
                }
            }
            U.Forget();
            Check(OzMech.Version == 2 ? U.Drawn == 2 : U.Drawn == 1, $"the switch OzMech.Version = {OzMech.Version}: drawn model v{U.Drawn}");
            var icon = UnitArt.Icon(OzMech.Type, Team.Red);
            Check(OzMech.Version != 2 || (U.PrefabFor(OzMech.V1Key, v1) == U.PrefabV2 && icon && icon.name.Contains("v2") && OzBadgeArt.FaceFor(OzMech.Type, Team.Red).Contains("medium_v2")),
                  $"with the switch on v2: Z_medium -> Z_medium_v2, the production picture {(icon ? icon.name : "none")}, the badge face {OzBadgeArt.FaceFor(OzMech.Type, Team.Red)}");
            var tv1 = OzV2Units.TileArt(OzMech.TileV1, Team.Red); var tv2 = OzV2Units.TileArt(OzMech.TileV2, Team.Red);
            Check(tv1 && tv2 && tv1 != tv2 && !tv1.name.Contains("v2"), $"testing panel tiles: {OzV2Units.TileLabel(OzMech.TileV1)} = {(tv1 ? tv1.name : "none")}, {OzV2Units.TileLabel(OzMech.TileV2)} = {(tv2 ? tv2.name : "none")}");
            Write(lines, pass, fail);
        }

        static void Write(List<string> lines, int pass, int fail)
        {
            string head = $"[OzMechV2] parity check {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail} checks";
            Directory.CreateDirectory(ProofDir);
            File.WriteAllText(Path.Combine(ProofDir, "parity_check.txt"), head + "\n" + string.Join("\n", lines) + "\n");
            Debug.Log(head + "\n" + string.Join("\n", lines));
        }

        // ---------------------------------------------------------------- 3 Play proof, 4 .. 15 films and tests
        [MenuItem(M + "3 Play proof (L02 Desert, side by side)", false, 3)] static void ProofL02() => Proof("L02", L02);
        [MenuItem(M + "3 Play proof (L17 Car Park, side by side)", false, 4)] static void ProofL17() => Proof("L17", L17);
        static bool Ready()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[OzMechV2] stop Play first"); return false; }
            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }
        static void Proof(string job, string scene)
        {
            if (!Ready()) return;
            PlayerPrefs.SetString(MechV2Proof.AutoPref, job); PlayerPrefs.Save();
            EditorSceneManager.OpenScene(scene);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(M + "5 Film: a battle of v2 mechs (L02)", false, 21)] static void Battle() => Film("battle");
        [MenuItem(M + "6 Film: a robot boards an empty v2 (L02)", false, 22)] static void Board() => Film("board");
        [MenuItem(M + "6 Film: a robot boards an empty v2, close camera 32 m (L02)", false, 22)] static void BoardClose() => Film("board_close");
        [MenuItem(M + "7 Film: one v2 fights to its death (L02)", false, 23)] static void Fight() => Film("fight");
        [MenuItem(M + "8 Film: the driver up against snipers (L02)", false, 24)] static void Snipe() => Film("snipe");
        [MenuItem(M + "11 Test: the waist never shakes between targets (L02)", false, 50)] static void Turret() => Film("turret");
        [MenuItem(M + "12 Test + film: v1 and v2 side by side, the upper body's kick and the body's rock at each shot (L02)", false, 51)] static void Recoil() => Film("recoil");
        [MenuItem(M + "13 Test: the waist in a fight never shakes (L02)", false, 52)] static void Combat() => Film("combat");
        [MenuItem(M + "14 Test + film: v1 and v2 side by side, the feet stay planted on the ground, only the body rocks (L02, item 146)", false, 53)] static void Walk() => Film("walk");
        [MenuItem(M + "15 Film: the walk close at RTS distance (v2, 32 m, L02)", false, 54)] static void WalkClose() => Film("walk_close");
        static void Film(string job)
        {
            if (!Ready()) return;
            PlayerPrefs.SetString(HeavyV2Films.SubjectPref, OzMech.Type);
            PlayerPrefs.SetString(HeavyV2Films.AutoPref, job); PlayerPrefs.Save();
            EditorSceneManager.OpenScene(L02);
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
