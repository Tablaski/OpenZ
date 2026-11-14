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
    /// Light tank v2 (1.2 v2 units), the editor side; the heavy tank v2's menus (Editor/OzHeavyV2Setup.cs) for the light tank.
    ///  1 Build: the v2 view prefab Resources/OpenZ/LightV2/Z_light_v2 from Models/Legacy/light_tank_v2.glb, made like Prefabs/Z_light
    ///    (root + TeamPaint copied from v1, the model as "Model" at (0, 0, 0) with v1's scale x OzLightTank.V2Size: v2's belts touch the
    ///    ground there, v1's offset would float them 3.7 cm), the GLB's node tree with v1's names, the TeamLit materials light_tank_v2_body /
    ///    _head / _track / _well and v1's lamp material, v1's renderer settings; the dark side plates behind the road wheels go under their
    ///    belts (they stay planted on the ground with them, item 146); then its wreck fragments (the hull and both belts, files lightv2_*
    ///    beside v1's light_*: v1's are never touched) and the clipped bake. v1 stays as it is.
    ///  2 Parity check (counted, edit mode): every node the code reads, the planted running gear, the barrel root, the hatch, the ladder,
    ///    materials, wreck, footprint, lamps and blinkers data, pictures, the switch.
    ///  3 Play proof L02 / L17: LightV2Proof (v1 and v2 side by side; turret, fire, head kick, hull rock, planted tracks, hatch, damage,
    ///    death, wreck).
    ///  4 .. 8, 11 .. 14 the films and tests (HeavyV2Films for the light tank, L02, 1920 x 1080 with sound, the game's play zoom).
    /// </summary>
    public static class OzLightV2Setup
    {
        public const string Glb = "Assets/OpenZ/Models/Legacy/light_tank_v2.glb", V1Prefab = "Assets/OpenZ/Prefabs/Z_light.prefab",
                            Folder = "Assets/OpenZ/Resources/OpenZ/LightV2", PrefabPath = Folder + "/Z_light_v2.prefab", Mats = "Assets/OpenZ/Materials/Units/";
        /// <summary>The wreck fragment file key: lightv2_* (OpenZWreckFragments clears "&lt;key&gt;_*" before it bakes; "light_*" would take v1's files).</summary>
        public const string FragKey = "lightv2";
        /// <summary>How much further out (model units) the build puts the climb's ground anchors (Board_LadderBottom, Board_Approach).</summary>
        public const float LadderOut = 0.25f;
        const string M = "OpenZ/Light Tank v2/";
        const string L02 = "Assets/OpenZ/Scenes/L02_Desert.unity", L17 = "Assets/OpenZ/Scenes/L17_CarPark.unity";
        static readonly string ProofDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/v2_units/light/proof"));
        static OzUnitModels U => OzLightTank.Models;

        static Transform Deep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Deep(c, name); if (f) return f; }
            return null;
        }
        static void Layer(Transform t, int layer) { t.gameObject.layer = layer; foreach (Transform c in t) Layer(c, layer); }
        static bool Under(Transform t, string name) { for (var p = t; p; p = p.parent) if (p.name == name) return true; return false; }

        // ---------------------------------------------------------------- 1 build
        [MenuItem(M + "1 Build the v2 prefab (+ wreck fragments)", false, 1)]
        public static void Build()
        {
            string r = BuildNow();
            Debug.Log("[OzLightV2] " + r);
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
                ["LightTankV2_body"] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "light_tank_v2_body.mat"),
                ["LightTankV2_head"] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "light_tank_v2_head.mat"),
                ["LightTankV2_track"] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "light_tank_v2_track.mat"),
                ["LightTankV2_well"] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "light_tank_v2_well.mat"),
                ["LightTankV2_lamp"] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "tank_DV3_lamp.mat"),
            };
            foreach (var kv in mats) if (!kv.Value) return "FAILED: material for " + kv.Key + " missing in " + Mats;
            var v1Model = v1.transform.Find("Model");
            var v1Hull = Deep(v1.transform, "Tank_hull"); var v1Mr = v1Hull ? v1Hull.GetComponent<MeshRenderer>() : null;
            if (!v1Model || !v1Mr) return "FAILED: Z_light has no Model / Tank_hull renderer";
            var sb = new StringBuilder();
            var inst = Object.Instantiate(glb);
            GameObject root = null;
            try
            {
                var susp = Deep(inst.transform, "TankSuspension");
                if (!susp || !susp.parent) return "FAILED: the GLB has no TankSuspension under a root node";
                var model = susp.parent;                                                                  // the glTF scene's root node "TankRoot"
                root = new GameObject(OzLightTank.V2Key) { layer = v1.layer };
                var tp = root.AddComponent<TeamPaint>();
                var tp1 = v1.GetComponent<TeamPaint>(); if (tp1) EditorUtility.CopySerialized(tp1, tp);
                model.SetParent(root.transform, false); model.name = "Model";
                model.localPosition = Vector3.zero; model.localRotation = v1Model.localRotation; model.localScale = v1Model.localScale * OzLightTank.V2Size;
                // item 146: the side plates ride on their belts (planted with them; View/VehicleMotion.GearOutside.cs)
                int plates = 0;
                foreach (var (plate, belt) in new[] { ("Tank_sideplate_left", "Tank_track_left"), ("Tank_sideplate_right", "Tank_track_right") })
                {
                    var p = Deep(model, plate); var b = Deep(model, belt);
                    if (p && b) { p.SetParent(b, true); plates++; }
                }
                // the climb from the ground to the turret ladder (BoardingView: a straight line from Board_LadderBottom to Board_LadderTop):
                // the bottom and the approach 0.25 model units further out than the model step put them, so at the belt's top the climber's
                // root is 0.26 outside the belt (was 0.13: his hands reached into the belt's edge); the top stays on the ladder
                foreach (var nm in new[] { "tank_Board_LadderBottom", "tank_Board_Approach" })
                {
                    var a = Deep(model, nm); if (a) a.localPosition += new Vector3(Mathf.Sign(a.localPosition.x) * LadderOut, 0f, 0f);
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
                        all[i] = nm.StartsWith("Tank_track_") ? mats["LightTankV2_track"] : nm.StartsWith("Tank_headlamp") ? mats["LightTankV2_lamp"]
                               : nm == "Tank_turret" && i == 1 ? mats["LightTankV2_well"]
                               : nm == "Tank_hull" || nm.StartsWith("Tank_sideplate") || nm.StartsWith("Tank_wheel") ? mats["LightTankV2_body"] : mats["LightTankV2_head"];
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
                sb.Append($"prefab: {n} renderers, {byName} material slots by the GLB's material names{(unknown.Count > 0 ? ", by renderer name: " + string.Join(", ", unknown) : "")}; side plates under their belts {plates}/2; Model at (0, 0, 0) x {model.localScale.x:0.0000}; ");
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/OpenZ/Resources/OpenZ", "LightV2");
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
            U.Forget();
            return sb.ToString();
        }

        // ---------------------------------------------------------------- 2 parity check
        [MenuItem(M + "2 Parity check (counted, no Play)", false, 2)]
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
            // the nodes v1's code reads by name (UnitView, UnitAim, VehicleMotion, BoardingView, RoundHatch, VehicleLights, OzTankWreck), in v2
            // with v1's parent (the side plates are v2's own: they ride on the belts)
            var roles = new[] { "Model", "TankSuspension", "TankChassis", "Tank_hull", "Tank_headlamp", "Tank_headlamp.001", "TankTurretYaw", "Tank_turret", "TankBarrelPitch",
                                "TankRecoil", "Tank_barrel", "TankMuzzle", "tank_HatchHinge", "tank_Board_Approach", "tank_Board_LadderBottom", "tank_Board_LadderTop",
                                "tank_Board_HatchTop", "tank_Board_Seat", "tank_CabinFloor", "TankGunnerSeat", "TankDriverSeat", "TankLabelAnchor", "TankLeftExit",
                                "TankRightExit", "Track_Left", "Track_Right", "Tank_track_left", "Tank_track_right", "TankWheel_1", "TankWheel_6" };
            var miss = new List<string>(); int comps = 0;
            foreach (var nm in roles)
            {
                var a = N(v1, nm); var b = N(v2, nm);
                if (!a) { miss.Add(nm + " (not in v1!)"); continue; }
                if (!b) { miss.Add(nm + " MISSING in v2"); continue; }
                if (P(a, v1) != P(b, v2) && nm != "Tank_track_left" && nm != "Tank_track_right") miss.Add($"{nm} parent {P(b, v2)} (v1 {P(a, v1)})");
                foreach (var c in a.GetComponents<Component>())
                {
                    if (c is Transform || c is WreckClipBaked) continue;
                    comps++; if (!b.GetComponent(c.GetType()) && !OzLightV2K84b.WellStandsIn(v2, nm, c)) miss.Add($"{nm}: {c.GetType().Name}");   // k84b: v1's drawn recess box under the hatch = v2's modelled well (its own line below)
                }
            }
            Check(miss.Count == 0, $"{roles.Length} role nodes and their {comps} components in v2 with v1's parents{(miss.Count > 0 ? "; " + string.Join(", ", miss) : "")}");
            Check(OzLightV2K84b.HatchWell(v2, out string k84bWell), k84bWell);                         // k84b, item 6: v2's modelled hatch well (dark, never team paint, under the frame)
            Check(OzLightV2K84b.SizeCheck(v1, v2, out string k84bSize), k84bSize);                     // k84b, item 1: v2 reads v1's size from the RTS camera (OzLightTank.V2Size)
            Check(N(v2, OzLightTank.FrameNode), "v2 node " + OzLightTank.FrameNode + " (the modelled hatch frame: RoundHatch builds nothing on v2, it marks a v2 view)");
            var yaw = N(v2, "TankTurretYaw");
            bool UnderYaw(string nm) { var x = N(v2, nm); return yaw && x && x.IsChildOf(yaw); }
            Check(UnderYaw("Tank_turret") && UnderYaw("Tank_barrel") && UnderYaw("Tank_mantlet") && UnderYaw("tank_HatchHinge") && UnderYaw("tank_v2_Ladder") && !N(v2, "TankV2_Antennas"),
                  "turret, mantlet, barrel, hatch and ladder turn with the turret (under TankTurretYaw: the head kick and the turret toss move them as one); no antennas (item 155: its reference has none)");
            var mz = N(v2, "TankMuzzle");
            Check(mz && mz.parent && mz.parent.name == "TankRecoil" && mz.localPosition.z < -1f && Mathf.Abs(mz.localPosition.x) < 0.01f, $"the muzzle on the recoil slide, fires along -z: {(mz ? mz.localPosition.ToString("F3") : "-")}");
            // the barrel root inside the mantlet (lesson 2): the barrel's rear end deep in the mantlet at rest, never behind the turret's back at full slide
            var bm = N(v2, "Tank_barrel") ? N(v2, "Tank_barrel").GetComponent<MeshFilter>() : null; var mm = N(v2, "Tank_mantlet") ? N(v2, "Tank_mantlet").GetComponent<MeshFilter>() : null;
            var tm = N(v2, "Tank_turret") ? N(v2, "Tank_turret").GetComponent<MeshFilter>() : null;
            if (bm && mm && tm && bm.sharedMesh && mm.sharedMesh && tm.sharedMesh)
            {
                float rootIn = bm.sharedMesh.bounds.max.z - mm.sharedMesh.bounds.min.z;                     // the pitch / recoil frames share the origin (TankRecoil at 0)
                var pitch = N(v2, "TankBarrelPitch");
                float back = pitch.localPosition.z + bm.sharedMesh.bounds.max.z + 1.13f * UnitAim.LightRecoilKV2;   // UnitAim's slide peak on a v2 view (envelope 1.13 x UnitAim.LightRecoilKV2; v1 and the light type 0.66), yaw frame
                Check(rootIn > 0.3f && back < tm.sharedMesh.bounds.max.z, $"the barrel root {rootIn:0.00} m inside the mantlet's front face; at the slide's peak its end at z {back:0.00} (the turret's back {tm.sharedMesh.bounds.max.z:0.00})");
            }
            else Check(false, "barrel / mantlet / turret meshes missing");
            // item 146: the running gear planted (never under the suspension node), only the hull + turret on it
            var susp = N(v2, "TankSuspension");
            var gear = v2.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("TankWheel_") || t.name.StartsWith("Track_") || t.name.StartsWith("Tank_track_") || t.name.StartsWith("Tank_sideplate_")).ToList();
            int wheels = gear.Count(t => System.Text.RegularExpressions.Regex.IsMatch(t.name, "^TankWheel_\\d+$"));
            Check(susp && gear.Count >= OzLightTank.V2Wheels + 6 && gear.All(t => !t.IsChildOf(susp)) && wheels == OzLightTank.V2Wheels && N(v2, "Tank_sideplate_left") && N(v2, "Tank_sideplate_left").parent.name == "Tank_track_left"   // item 155: the model's road wheels + the 2 belt nodes, the 2 belts and the 2 side plates (with their skirts)
                  && N(v2, "Tank_sideplate_right").parent.name == "Tank_track_right" && N(v2, "Tank_hull").IsChildOf(susp) && yaw.IsChildOf(susp),
                  $"item 146: {wheels} road wheels, both belts and both side plates (on their belts) beside the suspension node, never under it (planted); hull and turret on TankSuspension");
            Check(TrackGround.Types.Contains(OzLightTank.Type) && HeavyRecoil.Types.Contains(OzLightTank.Type) && VehicleMotion.ScrollMaterials.Contains(OzLightTank.BeltMaterial),
                  "the light tank is planted (TrackGround.Types), kicks and rocks (HeavyRecoil.Types) and its v2 belts roll on screen (VehicleMotion.ScrollMaterials)");
            // the clearance over the belts (the hull's lowest point over each belt, at rest; the wobble keeps it, lifted by the planting)
            {
                var hull = N(v2, "Tank_hull").GetComponent<MeshFilter>(); var bl = N(v2, "Tank_track_left").GetComponent<MeshFilter>(); var br = N(v2, "Tank_track_right").GetComponent<MeshFilter>();
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
                    Check(lowL > 0.015f && lowR > 0.015f && Mathf.Abs(Mathf.Min(L.min.y, R.min.y)) < 0.01f, $"at rest the hull clears its belts by {lowL:0.000} / {lowR:0.000} m (left / right; the model: 3 cm), the belts' bottom at {Mathf.Min(L.min.y, R.min.y):0.000} m (on the ground)");
                }
                else Check(false, "hull / belt meshes not readable for the clearance check");
            }
            // wreck fragments (the death's break-apart): hull + both belts
            int baked = 0, holders = 0;
            foreach (var nm in new[] { "Tank_hull", "Tank_track_left", "Tank_track_right" })
            {
                var p = N(v2, nm); var h = p ? p.Find("OZ_WreckFragments") : null;
                if (h) { holders++; if (WreckPieces.IsBaked(h)) baked++; }
                Check(h && h.childCount >= 2, $"wreck fragments under {nm}: {(h ? h.childCount : 0)}");
            }
            Check(baked == holders && holders == 3, $"clipped wreck bake: {baked} of {holders} holders baked");
            Check(yaw && yaw.GetComponentsInChildren<Transform>(true).Count(x => x.name == "OZ_WreckFragments") == 0, "no fragments under the turret (it flies whole: the turret toss)");
            // renderers: shadows, materials (team colour on the red block only; neutral dark grey like v1)
            var rs = v2.GetComponentsInChildren<MeshRenderer>(true).Where(r => !Under(r.transform, "OZ_WreckFragments")).ToList();
            Check(rs.All(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off), $"{rs.Count} renderers cast shadows (as v1)");
            Check(rs.All(r => r.sharedMaterials.All(m => m && m.shader && m.shader.name.Contains("TeamLit"))), "TeamLit materials on every renderer, no empty slot");
            foreach (var mn in new[] { "light_tank_v2_body", "light_tank_v2_head", "light_tank_v2_track" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(Mats + mn + ".mat");
                bool ok = m && m.GetTexture("_BaseMap") && m.GetFloat("_LegacyHeavy") > 0.5f && m.GetFloat("_PaintFromAlpha") > 1.5f && m.GetFloat("_TeamMode") > 0.5f
                          && Mathf.Abs(m.GetVector("_HeavyLook").y - 0.0805f) < 0.01f;
                Check(ok, $"{mn}: albedo {(m && m.GetTexture("_BaseMap") ? m.GetTexture("_BaseMap").name : "none")}, _HeavyLook {(m ? m.GetVector("_HeavyLook").ToString("F4") : "-")} (y = v1's empty grey 0.0805: dark, never white), _DarkColor {(m ? m.GetColor("_DarkColor").ToString("F3") : "-")}");
            }
            var wm = AssetDatabase.LoadAssetAtPath<Material>(Mats + "light_tank_v2_well.mat");
            Check(wm && wm.GetFloat("_TeamMode") < 0.5f, "the hatch well is never team paint");
            var turretMr = N(v2, "Tank_turret") ? N(v2, "Tank_turret").GetComponent<MeshRenderer>() : null;
            Check(turretMr && turretMr.sharedMaterial && turretMr.sharedMaterial.GetFloat("_PaintFromAlpha") > 1.5f && OzV2Units.NoRoofPanel("Tank_turret", turretMr.sharedMaterial) && !OzV2Units.NoRoofPanel("Tank_turret", N(v1, "Tank_turret").GetComponent<MeshRenderer>().sharedMaterial),
                  "no team roof panel on v2's turret (item 155: its team colour is painted in its own pictures, on the roof, the flank tags and the rear slots, through the paint mask); v1 keeps its roof panel");
            // footprint (UnitView radius from the renderers' bounds) = v1's
            Bounds Bx(GameObject g)
            {
                Bounds b = default; bool any = false;
                foreach (var r in g.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (Under(r.transform, "OZ_WreckFragments")) continue;
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
            var b1 = Bx(v1); var b2 = Bx(v2);
            float r1 = Mathf.Clamp(Mathf.Max(b1.extents.x, b1.extents.z) * 0.8f, 1.95f, 3f), r2 = Mathf.Clamp(Mathf.Max(b2.extents.x, b2.extents.z) * 0.8f, 1.95f, 3f);
            Check(Mathf.Abs(r1 - r2) < 0.06f, $"spacing radius: v1 {r1:0.00} m, v2 {r2:0.00} m; size v1 {b1.size.x:0.00} x {b1.size.y:0.00} x {b1.size.z:0.00} m, v2 {b2.size.x:0.00} x {b2.size.y:0.00} x {b2.size.z:0.00} m (OzLightTank.V2Size {OzLightTank.V2Size})");
            // the ladder (lesson 1): on the turret side, never in the ground; its anchors on it
            var lad = N(v2, "tank_v2_Ladder") ? N(v2, "tank_v2_Ladder").GetComponent<MeshRenderer>() : null;
            var lbot = N(v2, "tank_Board_LadderBottom"); var ltop = N(v2, "tank_Board_LadderTop");
            if (lad)
            {
                var lb = lad.bounds;
                Check(lb.min.y > 0.5f && ltop && lbot && ltop.position.y <= lb.max.y + 0.15f && Mathf.Abs(ltop.position.z - lb.center.z) < 0.3f,
                      $"ladder: bottom {lb.min.y:0.00} m above the ground (on the turret side, from the deck), top {lb.max.y:0.00} m; LadderTop {(ltop ? ltop.position.ToString("F2") : "-")}, LadderBottom {(lbot ? lbot.position.ToString("F2") : "-")}");
            }
            else Check(false, "no tank_v2_Ladder renderer");
            // the real hatch: the modelled frame + the hinge + HatchTop over it
            var fr = N(v2, OzLightTank.FrameNode); var hh = N(v2, "tank_HatchHinge"); var top = N(v2, "tank_Board_HatchTop");
            Check(fr && hh && top && Vector2.Distance(new Vector2(fr.position.x, fr.position.z), new Vector2(top.position.x, top.position.z)) < 0.1f && OzV2Units.FrameNodeFor("tank_") == OzLightTank.FrameNode,
                  "the real hatch: frame, hinge and Board_HatchTop over it; RoundHatch uses the modelled one (OzV2Units.FrameNodeFor(\"tank_\"))");
            // tread: the belts' V runs along the belt (VehicleMotion scrolls _BaseMap_ST.w in each material slot's block)
            var trk = N(v2, "Tank_track_left") ? N(v2, "Tank_track_left").GetComponent<MeshFilter>() : null;
            float vr = 0f; if (trk && trk.sharedMesh && trk.sharedMesh.isReadable) { var uv = trk.sharedMesh.uv; if (uv.Length > 0) vr = uv.Max(u => u.y) - uv.Min(u => u.y); }
            Check(vr > 1.5f && N(v2, "Tank_track_left").GetComponent<MeshRenderer>().sharedMaterial.name == OzLightTank.BeltMaterial, $"belt V range {vr:0.00} tiles (the model: 2.125 per loop), material {OzLightTank.BeltMaterial}");
            // lamps and blinkers: v2's own data entries, every node they mount on in v2
            foreach (var (file, arr, label) in new[] { ("OpenZ/VehicleLights/vehicle_lights", "vehicles", "VehicleLights"), ("OpenZ/UnitBlinkers/unit_blinkers", "types", "UnitBlinkers") })
            {
                var ta = Resources.Load<TextAsset>(file); string txt = ta ? ta.text : "";
                int at = txt.IndexOf("\"" + U.LightsKey + "\"", System.StringComparison.Ordinal);
                bool nodes = true;
                if (at >= 0)
                    foreach (var nm in new[] { "TankSuspension", "TankTurretYaw" }) nodes &= N(v2, nm);
                Check(at >= 0 && nodes, $"{label}: entry '{U.LightsKey}' {(at >= 0 ? "present" : "MISSING")} (v2's own sockets, measured on its meshes)");
            }
            // pictures (lesson 3) and the switch
            foreach (var c in new[] { "red", "blue" })
            {
                foreach (var k in new[] { "icon", "portrait" })
                {
                    var t = Resources.Load<Texture2D>(OzLightTank.Dir + k + "_light_tank_v2_" + c);
                    var t1p = k == "icon" ? Resources.Load<Texture2D>("OpenZ/UI/Icons/icon_light_tank_" + c) : Resources.Load<Texture2D>("OpenZ/UI/PortraitsVehicles/portrait_light_tank_" + c);
                    Check(t && t1p && t.width == t1p.width && t.height == t1p.height, $"{k} v2 {c}: {(t ? t.width + "x" + t.height : "MISSING")} (v1 {(t1p ? t1p.width + "x" + t1p.height : "-")})");
                }
                for (int tier = 1; tier <= 4; tier++)
                {
                    var f2 = Resources.Load<Texture2D>(OzBadgeArt.Root + tier + "/face_light_v2_" + c); var f1 = Resources.Load<Texture2D>(OzBadgeArt.Root + tier + "/face_light_" + c);
                    Check(f2 && f1 && f2.width == f1.width, $"badge face x{tier} v2 {c}: {(f2 ? f2.width + " px" : "MISSING")} (v1 {(f1 ? f1.width + " px" : "-")})");
                }
            }
            U.Forget();
            Check(OzLightTank.Version == 2 ? U.Drawn == 2 : U.Drawn == 1, $"the switch OzLightTank.Version = {OzLightTank.Version}: drawn model v{U.Drawn}");
            var icon = UnitArt.Icon(OzLightTank.Type, Team.Red);
            Check(OzLightTank.Version != 2 || (U.PrefabFor(OzLightTank.V1Key, v1) == U.PrefabV2 && icon && icon.name.Contains("v2") && OzBadgeArt.FaceFor(OzLightTank.Type, Team.Red).Contains("light_v2")),
                  $"with the switch on v2: Z_light -> Z_light_v2, the production picture {(icon ? icon.name : "none")}, the badge face {OzBadgeArt.FaceFor(OzLightTank.Type, Team.Red)}");
            var tv1 = OzV2Units.TileArt(OzLightTank.TileV1, Team.Red); var tv2 = OzV2Units.TileArt(OzLightTank.TileV2, Team.Red);
            Check(tv1 && tv2 && tv1 != tv2 && !tv1.name.Contains("v2"), $"testing panel tiles: {OzV2Units.TileLabel(OzLightTank.TileV1)} = {(tv1 ? tv1.name : "none")}, {OzV2Units.TileLabel(OzLightTank.TileV2)} = {(tv2 ? tv2.name : "none")}");
            Write(lines, pass, fail);
        }

        static void Write(List<string> lines, int pass, int fail)
        {
            string head = $"[OzLightV2] parity check {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail} checks";
            Directory.CreateDirectory(ProofDir);
            File.WriteAllText(Path.Combine(ProofDir, "parity_check.txt"), head + "\n" + string.Join("\n", lines) + "\n");
            Debug.Log(head + "\n" + string.Join("\n", lines));
        }

        // ---------------------------------------------------------------- 3 Play proof, 4 .. 14 films and tests
        [MenuItem(M + "3 Play proof (L02 Desert, side by side)", false, 3)] static void ProofL02() => Proof("L02", L02);
        [MenuItem(M + "3 Play proof (L17 Car Park, side by side)", false, 4)] static void ProofL17() => Proof("L17", L17);
        static bool Ready()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[OzLightV2] stop Play first"); return false; }
            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }
        static void Proof(string job, string scene)
        {
            if (!Ready()) return;
            PlayerPrefs.SetString(LightV2Proof.AutoPref, job); PlayerPrefs.Save();
            EditorSceneManager.OpenScene(scene);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(M + "4 Film: the tread at the track ends (L02, RTS camera, crops 0.1 s apart)", false, 20)] static void Tread() => Film("tread");
        [MenuItem(M + "5 Film: a battle of v2 light tanks (L02)", false, 21)] static void Battle() => Film("battle");
        [MenuItem(M + "6 Film: a robot boards an empty v2 (L02)", false, 22)] static void Board() => Film("board");
        [MenuItem(M + "6 Film: a robot boards an empty v2, close camera 32 m (L02)", false, 22)] static void BoardClose() => Film("board_close");
        [MenuItem(M + "7 Film: one v2 fights to its death (L02)", false, 23)] static void Fight() => Film("fight");
        [MenuItem(M + "8 Film: the driver up against snipers (L02)", false, 24)] static void Snipe() => Film("snipe");
        [MenuItem(M + "11 Test: the turret never shakes between targets (L02)", false, 50)] static void Turret() => Film("turret");
        [MenuItem(M + "12 Test + film: v1 and v2 side by side, head kick and hull rock at each shot (L02)", false, 51)] static void Recoil() => Film("recoil");
        [MenuItem(M + "13 Test: the turret in a fight never shakes (L02)", false, 52)] static void Combat() => Film("combat");
        [MenuItem(M + "14 Test + film: v1 and v2 side by side, the tracks stay on the ground, only the hull rocks (L02, item 146)", false, 53)] static void Plant() => Film("plant");
        static void Film(string job)
        {
            if (!Ready()) return;
            PlayerPrefs.SetString(HeavyV2Films.SubjectPref, OzLightTank.Type);
            PlayerPrefs.SetString(HeavyV2Films.AutoPref, job); PlayerPrefs.Save();
            EditorSceneManager.OpenScene(L02);
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
