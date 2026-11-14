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
    /// Heavy tank v2 (1.2 items 110, 116, 117, 118): the editor side.
    ///  1 Build: the v2 view prefab Resources/OpenZ/HeavyV2/Z_heavy_v2 from Models/Legacy/heavy_tank_v2.glb, made like Prefabs/Z_heavy
    ///    (root + TeamPaint copied from v1, the model as "Model" at v1's offset, the GLB's node tree with v1's names, the TeamLit materials
    ///    heavy_tank_v2_body / _head / _track / _well and v1's ladder material, v1's renderer settings), then its wreck fragments (the hull
    ///    and both tracks, files heavyv2_* beside v1's heavy_*: v1's are never touched) and the clipped bake. v1 stays as it is.
    ///  2 Parity check (counted, edit mode): every v1 node, component, anchor and effect hook against v2; pictures; the switch.
    ///  3 Play proof L02 / L17: HeavyV2Proof (v1 and v2 side by side; turret, fire, recoil, hatch, damage, death, wreck).
    /// </summary>
    public static class OzHeavyV2Setup
    {
        public const string Glb = "Assets/OpenZ/Models/Legacy/heavy_tank_v2.glb", V1Prefab = "Assets/OpenZ/Prefabs/Z_heavy.prefab",
                            Folder = "Assets/OpenZ/Resources/OpenZ/HeavyV2", PrefabPath = Folder + "/Z_heavy_v2.prefab", Mats = "Assets/OpenZ/Materials/Units/";
        /// <summary>The wreck fragment file key: heavyv2_* (OpenZWreckFragments clears "&lt;key&gt;_*" before it bakes; "heavy_*" would take v1's files).</summary>
        public const string FragKey = "heavyv2";
        static readonly string ProofDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/heavy_v2/proof"));

        static Transform Deep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Deep(c, name); if (f) return f; }
            return null;
        }
        static void Layer(Transform t, int layer) { t.gameObject.layer = layer; foreach (Transform c in t) Layer(c, layer); }

        // ---------------------------------------------------------------- 1 build
        [MenuItem("OpenZ/Heavy Tank v2/1 Build the v2 prefab (+ wreck fragments)", false, 1)]
        public static void Build()
        {
            string r = BuildNow();
            Debug.Log("[OzHeavyV2] " + r);
            Directory.CreateDirectory(ProofDir);
            File.WriteAllText(Path.Combine(ProofDir, "build_report.txt"), r + "\n");
        }

        public static string BuildNow()
        {
            var glb = AssetDatabase.LoadAssetAtPath<GameObject>(Glb);
            var v1 = AssetDatabase.LoadAssetAtPath<GameObject>(V1Prefab);
            if (!glb) return "FAILED: no " + Glb + " (import it first)";
            if (!v1) return "FAILED: no " + V1Prefab;
            var body = AssetDatabase.LoadAssetAtPath<Material>(Mats + "heavy_tank_v2_body.mat");
            var head = AssetDatabase.LoadAssetAtPath<Material>(Mats + "heavy_tank_v2_head.mat");
            var track = AssetDatabase.LoadAssetAtPath<Material>(Mats + "heavy_tank_v2_track.mat");
            var well = AssetDatabase.LoadAssetAtPath<Material>(Mats + "heavy_tank_v2_well.mat");
            var detail = AssetDatabase.LoadAssetAtPath<Material>(Mats + "heavy_tank_HeavyTank_detail.mat");
            if (!body || !head || !track || !well || !detail) return "FAILED: a v2 material or v1's ladder material is missing in " + Mats;
            var v1Model = v1.transform.Find("Model");
            var v1Hull = Deep(v1.transform, "HeavyTankHull"); var v1Mr = v1Hull ? v1Hull.GetComponent<MeshRenderer>() : null;
            if (!v1Model || !v1Mr) return "FAILED: Z_heavy has no Model / HeavyTankHull renderer";
            var sb = new StringBuilder();
            var inst = Object.Instantiate(glb);
            GameObject root = null;
            try
            {
                var susp = Deep(inst.transform, "TankSuspension");
                if (!susp || !susp.parent) return "FAILED: the GLB has no TankSuspension under a root node";
                var model = susp.parent;                                                                  // the glTF scene's root node "HeavyTank"
                root = new GameObject(OzHeavyTank.V2Key) { layer = v1.layer };
                var tp = root.AddComponent<TeamPaint>();
                var tp1 = v1.GetComponent<TeamPaint>(); if (tp1) EditorUtility.CopySerialized(tp1, tp);
                model.SetParent(root.transform, false); model.name = "Model";
                model.localPosition = v1Model.localPosition; model.localRotation = v1Model.localRotation; model.localScale = v1Model.localScale;
                int n = 0;
                foreach (var mr in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string nm = mr.name;
                    var mf = mr.GetComponent<MeshFilter>(); int subs = mf && mf.sharedMesh ? mf.sharedMesh.subMeshCount : 1;
                    Material[] m = nm == "HeavyTankHull" ? new[] { body } : nm.StartsWith("HeavyTankTrack_") ? new[] { track }
                                 : nm == "HeavyTankTurret" ? new[] { head, well } : nm.StartsWith("HeavyTank_ladder") ? new[] { detail } : new[] { head };
                    var all = new Material[subs]; for (int i = 0; i < subs; i++) all[i] = m[Mathf.Min(i, m.Length - 1)];
                    mr.sharedMaterials = all;
                    mr.shadowCastingMode = v1Mr.shadowCastingMode; mr.receiveShadows = v1Mr.receiveShadows;
                    mr.lightProbeUsage = v1Mr.lightProbeUsage; mr.reflectionProbeUsage = v1Mr.reflectionProbeUsage;
                    mr.motionVectorGenerationMode = v1Mr.motionVectorGenerationMode; mr.allowOcclusionWhenDynamic = v1Mr.allowOcclusionWhenDynamic;
                    n++;
                }
                foreach (var c in model.GetComponentsInChildren<Component>(true))                           // only transforms, filters, renderers (as v1)
                    if (!(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer)) Object.DestroyImmediate(c);
                Layer(model, v1Model.gameObject.layer);
                sb.Append($"prefab: {n} renderers, materials body / head+well / track / ladder (v1's); ");
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/OpenZ/Resources/OpenZ", "HeavyV2");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok) return "FAILED: the prefab could not be saved at " + PrefabPath;
                sb.Append("saved ").Append(PrefabPath).Append("; ");
            }
            finally
            {
                if (root) Object.DestroyImmediate(root);
                if (inst) Object.DestroyImmediate(inst);
            }
            // wreck fragments: OpenZWreckFragments' own cut (its Bake is private: called by name), then the clipped bake
            var bake = typeof(OpenZWreckFragments).GetMethod("Bake", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string), typeof(string) }, null);
            sb.Append("wreck fragments: ").Append(bake != null ? (string)bake.Invoke(null, new object[] { PrefabPath, FragKey }) : "FAILED (OpenZWreckFragments.Bake not found)").Append("; ");
            AssetDatabase.SaveAssets();
            sb.Append("clipped: ").Append(OpenZWreckClipBake.ClipPrefab(PrefabPath, FragKey));
            AssetDatabase.SaveAssets();
            OzHeavyTank.Forget();
            return sb.ToString();
        }

        // ---------------------------------------------------------------- 2 parity check
        [MenuItem("OpenZ/Heavy Tank v2/2 Parity check (counted, no Play)", false, 2)]
        public static void Parity()
        {
            var lines = new List<string>(); int pass = 0, fail = 0;
            void Check(bool ok, string what) { lines.Add((ok ? "PASS " : "FAIL ") + what); if (ok) pass++; else fail++; }
            var v1 = AssetDatabase.LoadAssetAtPath<GameObject>(V1Prefab);
            var v2 = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Check(v1 && v2, $"prefabs: v1 {V1Prefab} {(v1 ? "found" : "MISSING")}, v2 {PrefabPath} {(v2 ? "found" : "MISSING (menu 1)")}");
            if (!v1 || !v2) { Write(lines, pass, fail); return; }
            var t1 = v1.GetComponentsInChildren<Transform>(true).Where(t => !Under(t, "OZ_WreckFragments")).ToList();
            var t2 = new Dictionary<string, Transform>();                                                  // the first node of each name (fragments repeat theirs)
            foreach (var t in v2.GetComponentsInChildren<Transform>(true)) { string k2 = t == v2.transform ? "Z_heavy" : t.name; if (!t2.ContainsKey(k2)) t2[k2] = t; }
            // every v1 node (but its own lid, which RoundHatch replaces) in v2 with the same parent and the same components
            int nodes = 0, comps = 0; var missing = new List<string>();
            foreach (var t in t1)
            {
                if (t.name == "HeavyTank_hatch_lid") continue;
                string key = t == v1.transform ? "Z_heavy" : t.name;
                nodes++;
                if (!t2.TryGetValue(key, out var u)) { missing.Add(key); continue; }
                string p1 = t.parent ? (t.parent == v1.transform ? "Z_heavy" : t.parent.name) : "-", p2 = u.parent ? (u.parent == v2.transform ? "Z_heavy" : u.parent.name) : "-";
                if (p1 != p2) missing.Add($"{key} (parent {p2}, v1 {p1})");
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c is Transform || c is WreckClipBaked) continue;
                    comps++;
                    if (!u.GetComponent(c.GetType())) missing.Add($"{key}: {c.GetType().Name}");
                }
            }
            Check(missing.Count == 0, $"v1 nodes {nodes} and their components {comps}: all in v2 with v1's parents{(missing.Count > 0 ? "; missing / different: " + string.Join(", ", missing) : "")}");
            Transform N(GameObject g, string name) => g.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
            // the parts the code reads by name
            foreach (var nm in new[] { "TankSuspension", "HeavyTankTurretYaw", "HeavyTankBarrelPitch", "HeavyTankRecoil_L", "HeavyTankRecoil_R", "HeavyTankMuzzle_L", "HeavyTankMuzzle_R",
                                       "HeavyTankTrack_L", "HeavyTankTrack_R", "heavy_HatchHinge", OzHeavyTank.FrameNode, "heavy_Board_Approach", "heavy_Board_LadderBottom",
                                       "heavy_Board_LadderTop", "heavy_Board_HatchTop", "heavy_Board_Seat", "HeavyTankDriverSeat", "HeavyTankLeftExit", "HeavyTankRightExit" })
                Check(N(v2, nm), "v2 node " + nm);
            var yaw = N(v2, "HeavyTankTurretYaw");
            bool UnderYaw(string nm) { var x = N(v2, nm); return yaw && x && x.IsChildOf(yaw); }
            Check(UnderYaw("HeavyTankBarrel_L") && UnderYaw("HeavyTankBarrel_R") && UnderYaw("HeavyTankTurret") && UnderYaw("heavy_HatchHinge") && UnderYaw("HeavyTankV2_Antennas"),
                  "turret, barrels and hatch hinge turn with the turret (under HeavyTankTurretYaw: the head recoil and the death's turret toss move them as one)");
            var mL = N(v2, "HeavyTankMuzzle_L"); var mR = N(v2, "HeavyTankMuzzle_R");
            Check(mL && mR && mL.parent.name.Contains("Recoil") && mR.parent.name.Contains("Recoil") && mL.localPosition.x > 0f && mR.localPosition.x < 0f && mL.localPosition.z < -1f && mR.localPosition.z < -1f,
                  $"muzzles on the recoil slides, L = +x, fire along -z: L {(mL ? mL.localPosition.ToString("F3") : "-")}, R {(mR ? mR.localPosition.ToString("F3") : "-")}");
            // barrel gap (item 118): the two barrels' meshes do not meet, a clear gap between them
            var bL = N(v2, "HeavyTankBarrel_L"); var bR = N(v2, "HeavyTankBarrel_R");
            var mfL = bL ? bL.GetComponent<MeshFilter>() : null; var mfR = bR ? bR.GetComponent<MeshFilter>() : null;
            if (mfL && mfR && mfL.sharedMesh && mfR.sharedMesh)
            {
                float innerL = mfL.sharedMesh.bounds.min.x, innerR = mfR.sharedMesh.bounds.max.x;           // in the shared recoil space: L at +x, R at -x
                Check(innerL - innerR > 0.02f, $"barrel gap (item 118): {innerL - innerR:0.000} m between the barrels at their closest (root sleeves)");
            }
            // wreck fragments (the death's break-apart): hull + both tracks
            int baked = 0, holders = 0;
            foreach (var nm in new[] { "HeavyTankHull", "HeavyTankTrack_L", "HeavyTankTrack_R" })
            {
                var p = N(v2, nm); var h = p ? p.Find("OZ_WreckFragments") : null;
                if (h) { holders++; if (WreckPieces.IsBaked(h)) baked++; }
                Check(h && h.childCount >= 2, $"wreck fragments under {nm}: {(h ? h.childCount : 0)}");
            }
            Check(baked == holders && holders == 3, $"clipped wreck bake: {baked} of {holders} holders baked");
            int fragsOverTurret = yaw ? yaw.GetComponentsInChildren<Transform>(true).Count(x => x.name == "OZ_WreckFragments") : -1;
            Check(fragsOverTurret == 0, "no fragments under the turret (it flies whole: the turret toss)");
            // renderers: shadows, materials (team colour on the stripes only; neutral grey)
            var rs = v2.GetComponentsInChildren<MeshRenderer>(true).Where(r => !Under(r.transform, "OZ_WreckFragments")).ToList();
            Check(rs.All(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off), $"{rs.Count} renderers cast shadows (as v1)");
            int teamlit = rs.Count(r => r.sharedMaterials.All(m => m && m.shader && m.shader.name.Contains("TeamLit")));
            Check(teamlit == rs.Count, $"TeamLit materials on {teamlit}/{rs.Count} renderers");
            foreach (var mn in new[] { "heavy_tank_v2_body", "heavy_tank_v2_head", "heavy_tank_v2_track" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(Mats + mn + ".mat");
                bool ok = m && m.GetTexture("_BaseMap") && m.GetFloat("_LegacyHeavy") > 0.5f && m.GetFloat("_PaintFromAlpha") > 1.5f && m.GetFloat("_TeamMode") > 0.5f;
                Check(ok, $"{mn}: albedo {(m && m.GetTexture("_BaseMap") ? m.GetTexture("_BaseMap").name : "none")}, _LegacyHeavy {(m ? m.GetFloat("_LegacyHeavy") : -1)}, _HeavyLook {(m ? m.GetVector("_HeavyLook").ToString("F3") : "-")} (alpha 1 = the stripes take the team colour, 0.5 = camo greys out when empty)");
            }
            var wm = AssetDatabase.LoadAssetAtPath<Material>(Mats + "heavy_tank_v2_well.mat");
            Check(wm && wm.GetFloat("_TeamMode") < 0.5f, "the hatch well is never team paint");
            var turretMr = N(v2, "HeavyTankTurret") ? N(v2, "HeavyTankTurret").GetComponent<MeshRenderer>() : null;
            Check(turretMr && OzHeavyTank.NoRoofPanel("HeavyTankTurret", turretMr.sharedMaterial), "no team roof panel on v2's turret (team colour on its stripes only)");
            // footprint (UnitView radius) without the guns = v1's
            Bounds Bx(GameObject g, bool guns)
            {
                Bounds b = default; bool any = false;
                foreach (var r in g.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (Under(r.transform, "OZ_WreckFragments")) continue;
                    if (!guns) { bool gun = false; for (var t = r.transform; t; t = t.parent) { string n = t.name; if (n.Contains("Barrel") || n.Contains("Recoil") || n.Contains("Muzzle") || n.Contains("Antenna")) { gun = true; break; } } if (gun) continue; }
                    var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                    var mb = mf.sharedMesh.bounds; var m = r.transform.localToWorldMatrix;
                    for (int i = 0; i < 8; i++)
                    {
                        var c = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1)));
                        if (!any) { b = new Bounds(c, Vector3.zero); any = true; } else b.Encapsulate(c);
                    }
                }
                return b;
            }
            var b1 = Bx(v1, true); var b2 = Bx(v2, false);
            float r1 = Mathf.Clamp(Mathf.Max(b1.extents.x, b1.extents.z) * 0.8f, 1.95f, 3f), r2 = Mathf.Clamp(Mathf.Max(b2.extents.x, b2.extents.z) * 0.8f, 1.95f, 3f);
            Check(Mathf.Abs(r1 - r2) < 0.06f, $"spacing radius: v1 {r1:0.00} m, v2 without its guns {r2:0.00} m (UnitView: OzHeavyTank.ViewRadius); length v1 {b1.size.z:0.00}, v2 {b2.size.z:0.00} m");
            // ladder (item 116): above the ground, at the armour side, its top at the deck edge; anchors on it
            var lad = v2.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("HeavyTank_ladder")).ToList();
            var lb = default(Bounds); bool la = false;
            foreach (var r in lad) { var mf = r.GetComponent<MeshFilter>(); var mb = mf.sharedMesh.bounds; var w = r.transform.TransformPoint(mb.min); var w2 = r.transform.TransformPoint(mb.max); if (!la) { lb = new Bounds(w, Vector3.zero); la = true; } lb.Encapsulate(w); lb.Encapsulate(w2); }
            var lbot = N(v2, "heavy_Board_LadderBottom"); var ltop = N(v2, "heavy_Board_LadderTop");
            Check(lad.Count == 7 && la && lb.min.y > 0.1f, $"ladder: {lad.Count} parts (v1 7), bottom {(la ? lb.min.y : -1):0.00} m above the ground, top {(la ? lb.max.y : -1):0.00} m");
            Check(ltop && lbot && la && Mathf.Abs(ltop.position.z - lb.center.z) < 0.05f && Mathf.Abs(lbot.position.z - lb.center.z) < 0.05f && ltop.position.y <= lb.max.y + 0.05f && ltop.position.y >= lb.max.y - 0.25f,
                  $"ladder anchors on the ladder: bottom {(lbot ? lbot.position.ToString("F2") : "-")}, top {(ltop ? ltop.position.ToString("F2") : "-")}, ladder centre z {(la ? lb.center.z : 0f):0.00}");
            // the real hatch: the modelled frame + the hinge (RoundHatch builds nothing on v2)
            var fr = N(v2, OzHeavyTank.FrameNode); var hh = N(v2, "heavy_HatchHinge"); var top = N(v2, "heavy_Board_HatchTop");
            Check(fr && hh && top && Vector2.Distance(new Vector2(fr.position.x, fr.position.z), new Vector2(top.position.x, top.position.z)) < 0.1f,
                  "the real hatch: frame, hinge and Board_HatchTop over the left cupola (RoundHatch.HeavyV2.cs)");
            // tread scroll: the belts' V runs along the belt (VehicleMotion scrolls _BaseMap_ST.w)
            var trk = N(v2, "HeavyTankTrack_L") ? N(v2, "HeavyTankTrack_L").GetComponent<MeshFilter>() : null;
            float vr = 0f; if (trk && trk.sharedMesh) { var uv = trk.sharedMesh.uv; if (uv.Length > 0) vr = uv.Max(u => u.y) - uv.Min(u => u.y); }
            Check(vr > 2f, $"track belt V range {vr:0.0} tiles (the tread runs with VehicleMotion's scroll)");
            // pictures (item 117) and the switch
            foreach (var c in new[] { "red", "blue" })
            {
                foreach (var k in new[] { "icon", "portrait" })
                {
                    var t = Resources.Load<Texture2D>(OzHeavyTank.Dir + k + "_heavy_tank_v2_" + c);
                    var t1p = k == "icon" ? Resources.Load<Texture2D>("OpenZ/UI/Icons/icon_heavy_tank_" + c) : Resources.Load<Texture2D>("OpenZ/UI/PortraitsVehicles/portrait_heavy_tank_" + c);
                    Check(t && t1p && t.width == t1p.width && t.height == t1p.height, $"{k} v2 {c}: {(t ? t.width + "x" + t.height : "MISSING")} (v1 {(t1p ? t1p.width + "x" + t1p.height : "-")})");
                }
                for (int tier = 1; tier <= 4; tier++)
                {
                    var f2 = Resources.Load<Texture2D>(OzBadgeArt.Root + tier + "/face_heavy_v2_" + c); var f1 = Resources.Load<Texture2D>(OzBadgeArt.Root + tier + "/face_heavy_" + c);
                    Check(f2 && f1 && f2.width == f1.width, $"badge face x{tier} v2 {c}: {(f2 ? f2.width + " px" : "MISSING")} (v1 {(f1 ? f1.width + " px" : "-")})");
                }
            }
            OzHeavyTank.Forget();
            Check(OzHeavyTank.Version == 2 ? OzHeavyTank.Drawn == 2 : OzHeavyTank.Drawn == 1, $"the switch OzHeavyTank.Version = {OzHeavyTank.Version}: drawn model v{OzHeavyTank.Drawn}");
            Check(OzHeavyTank.Version != 2 || (OzHeavyTank.PrefabFor(OzHeavyTank.V1Key, v1) == OzHeavyTank.PrefabV2 && UnitArt.Icon("heavy", Team.Red) && UnitArt.Icon("heavy", Team.Red).name.Contains("v2")),
                  $"with the switch on v2: Z_heavy -> Z_heavy_v2, the production picture {(UnitArt.Icon("heavy", Team.Red) ? UnitArt.Icon("heavy", Team.Red).name : "none")}, the badge face {OzBadgeArt.FaceFor("heavy", Team.Red)}");
            var tv1 = OzHeavyTank.TileArt(OzHeavyTank.TileV1, Team.Red); var tv2 = OzHeavyTank.TileArt(OzHeavyTank.TileV2, Team.Red);
            Check(tv1 && tv2 && tv1 != tv2 && !tv1.name.Contains("v2"), $"testing panel tiles: {OzHeavyTank.TileLabel(OzHeavyTank.TileV1)} = {(tv1 ? tv1.name : "none")}, {OzHeavyTank.TileLabel(OzHeavyTank.TileV2)} = {(tv2 ? tv2.name : "none")}");
            Write(lines, pass, fail);
        }

        static bool Under(Transform t, string name) { for (var p = t; p; p = p.parent) if (p.name == name) return true; return false; }

        static void Write(List<string> lines, int pass, int fail)
        {
            string head = $"[OzHeavyV2] parity check {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail} checks";
            Directory.CreateDirectory(ProofDir);
            File.WriteAllText(Path.Combine(ProofDir, "parity_check.txt"), head + "\n" + string.Join("\n", lines) + "\n");
            Debug.Log(head + "\n" + string.Join("\n", lines));
        }

        // ---------------------------------------------------------------- 3 Play proof
        [MenuItem("OpenZ/Heavy Tank v2/3 Play proof (L02 Desert, side by side)", false, 3)] static void ProofL02() => Proof("L02", "Assets/OpenZ/Scenes/L02_Desert.unity");
        [MenuItem("OpenZ/Heavy Tank v2/3 Play proof (L17 Car Park, side by side)", false, 4)] static void ProofL17() => Proof("L17", "Assets/OpenZ/Scenes/L17_CarPark.unity");
        static void Proof(string job, string scene)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[OzHeavyV2] stop Play first"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            PlayerPrefs.SetString(HeavyV2Proof.AutoPref, job); PlayerPrefs.Save();
            EditorSceneManager.OpenScene(scene);
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
