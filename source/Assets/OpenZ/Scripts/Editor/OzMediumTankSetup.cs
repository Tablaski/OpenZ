#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The MEDIUM TANK (1.2 owner item 139, DV145), the editor side; the light tank v2's menus (Editor/OzLightV2Setup.cs) for the medium tank.
    ///  1 Build: its view prefab Resources/OpenZ/MTank/Z_mtank from Models/Legacy/mtank_v2.glb, made like the light tank's (root + TeamPaint
    ///    copied from Prefabs/Z_light, the model as "Model" at (0, 0, 0) with scale 1: the GLB is in game metres), the GLB's node tree with
    ///    the light tank's node names, the TeamLit materials mtank_v2_body / _head / _track / _well and the light tank's lamp material, the
    ///    light tank's renderer settings; the dark side plates behind the road wheels go under their belts (planted with them, item 146);
    ///    then its wreck fragments (files mtank_*) and the clipped bake.
    ///  2 Parity check (counted, edit mode): every node the code reads, the planted running gear, the barrel root, the hatch, the climb step,
    ///    materials, wreck, footprint, lamps and blinkers data, pictures, names, the lists it is in (P, Unit editor, voices, the build lists).
    ///  3 Rules check (counted, headless): Editor/OzV12MTankCount.cs.
    ///  4 Play proof L02 / L17: View/MTankProof.cs.
    ///  5 .. 16 the films (View/HeavyV2Films.cs for the medium tank, L02, 1920 x 1080 with sound, the game's play zoom).
    /// </summary>
    public static class OzMediumTankSetup
    {
        public const string Glb = "Assets/OpenZ/Models/Legacy/mtank_v2.glb", Template = "Assets/OpenZ/Prefabs/Z_light.prefab",
                            Folder = "Assets/OpenZ/Resources/OpenZ/MTank", PrefabPath = Folder + "/Z_mtank.prefab", Mats = "Assets/OpenZ/Materials/Units/";
        /// <summary>The wreck fragment file key: mtank_* (OpenZWreckFragments clears "&lt;key&gt;_*" before it bakes).</summary>
        public const string FragKey = "mtank";
        const string M = "OpenZ/Medium Tank/";
        const string L02 = "Assets/OpenZ/Scenes/L02_Desert.unity", L17 = "Assets/OpenZ/Scenes/L17_CarPark.unity";
        static readonly string ProofDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/v2_units/medium/proof"));
        static OzUnitModels U => OzMediumTank.Models;

        static Transform Deep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Deep(c, name); if (f) return f; }
            return null;
        }
        static void Layer(Transform t, int layer) { t.gameObject.layer = layer; foreach (Transform c in t) Layer(c, layer); }
        static bool Under(Transform t, string name) { for (var p = t; p; p = p.parent) if (p.name == name) return true; return false; }

        // ---------------------------------------------------------------- 1 build
        [MenuItem(M + "1 Build the prefab (+ wreck fragments)", false, 1)]
        public static void Build()
        {
            string r = BuildNow();
            Debug.Log("[OzMediumTank] " + r);
            Directory.CreateDirectory(ProofDir);
            File.WriteAllText(Path.Combine(ProofDir, "build_report.txt"), r + "\n");
        }

        public static string BuildNow()
        {
            var glb = AssetDatabase.LoadAssetAtPath<GameObject>(Glb);
            var tpl = AssetDatabase.LoadAssetAtPath<GameObject>(Template);
            if (!glb) return "FAILED: no " + Glb + " (import it first)";
            if (!tpl) return "FAILED: no " + Template;
            var mats = new Dictionary<string, Material>
            {
                ["MTankV2_body"] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "mtank_v2_body.mat"),
                ["MTankV2_head"] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "mtank_v2_head.mat"),
                ["MTankV2_track"] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "mtank_v2_track.mat"),
                ["MTankV2_well"] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "mtank_v2_well.mat"),
                ["MTankV2_lamp"] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "tank_DV3_lamp.mat"),
            };
            foreach (var kv in mats) if (!kv.Value) return "FAILED: material for " + kv.Key + " missing in " + Mats;
            var tplModel = tpl.transform.Find("Model");
            var tplHull = Deep(tpl.transform, "Tank_hull"); var tplMr = tplHull ? tplHull.GetComponent<MeshRenderer>() : null;
            if (!tplModel || !tplMr) return "FAILED: Z_light has no Model / Tank_hull renderer";
            var sb = new StringBuilder();
            var inst = Object.Instantiate(glb);
            GameObject root = null;
            try
            {
                var susp = Deep(inst.transform, "TankSuspension");
                if (!susp || !susp.parent) return "FAILED: the GLB has no TankSuspension under a root node";
                var model = susp.parent;                                                                  // the glTF scene's root node "TankRoot"
                root = new GameObject(OzMediumTank.Key) { layer = tpl.layer };
                var tp = root.AddComponent<TeamPaint>();
                var tp1 = tpl.GetComponent<TeamPaint>(); if (tp1) EditorUtility.CopySerialized(tp1, tp);
                model.SetParent(root.transform, false); model.name = "Model";
                model.localPosition = Vector3.zero; model.localRotation = tplModel.localRotation; model.localScale = Vector3.one * OzMediumTank.ModelScale;   // game metres x the medium's size (k87b: between the light and the heavy, View/OzMediumTank.cs)
                int plates = 0;                                                                           // item 146: the side plates ride on their belts (planted)
                foreach (var (plate, belt) in new[] { ("Tank_sideplate_left", "Tank_track_left"), ("Tank_sideplate_right", "Tank_track_right") })
                {
                    var p = Deep(model, plate); var b = Deep(model, belt);
                    if (p && b) { p.SetParent(b, true); plates++; }
                }
                sb.Append(OzMediumTankK87b.SteadyNodes(model));                                            // k87b: TankDriverSeat / TankLabelAnchor on the Model node (the light tank's parents), not the rocking suspension
                int n = 0, byName = 0; var unknown = new List<string>();
                foreach (var mr in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var src = mr.sharedMaterials; var all = new Material[src.Length];
                    for (int i = 0; i < src.Length; i++)
                    {
                        string mn = src[i] ? src[i].name : "";
                        int k = mn.IndexOf(' '); if (k > 0) mn = mn.Substring(0, k);
                        if (mats.TryGetValue(mn, out var m)) { all[i] = m; byName++; continue; }
                        string nm = mr.name;                                                              // fallback by the renderer's name
                        all[i] = nm.StartsWith("Tank_track_") ? mats["MTankV2_track"] : nm.StartsWith("Tank_headlamp") ? mats["MTankV2_lamp"]
                               : nm == "Tank_turret" && i == 1 ? mats["MTankV2_well"]
                               : nm == "Tank_hull" || nm.StartsWith("Tank_sideplate") || nm.StartsWith("Tank_wheel") || nm == "tank_v2_Steps" ? mats["MTankV2_body"] : mats["MTankV2_head"];
                        unknown.Add(nm + "[" + i + "] '" + (src[i] ? src[i].name : "none") + "'");
                    }
                    mr.sharedMaterials = all;
                    mr.shadowCastingMode = tplMr.shadowCastingMode; mr.receiveShadows = tplMr.receiveShadows;
                    mr.lightProbeUsage = tplMr.lightProbeUsage; mr.reflectionProbeUsage = tplMr.reflectionProbeUsage;
                    mr.motionVectorGenerationMode = tplMr.motionVectorGenerationMode; mr.allowOcclusionWhenDynamic = tplMr.allowOcclusionWhenDynamic;
                    n++;
                }
                foreach (var c in model.GetComponentsInChildren<Component>(true))                           // only transforms, filters, renderers (as the light tank)
                    if (!(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer)) Object.DestroyImmediate(c);
                Layer(model, tplModel.gameObject.layer);
                sb.Append($"prefab: {n} renderers, {byName} material slots by the GLB's material names{(unknown.Count > 0 ? ", by renderer name: " + string.Join(", ", unknown) : "")}; side plates under their belts {plates}/2; Model at (0, 0, 0) x {model.localScale.x:0.0000}; ");
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/OpenZ/Resources/OpenZ", "MTank");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok) return "FAILED: the prefab could not be saved at " + PrefabPath;
                sb.Append("saved ").Append(PrefabPath).Append("; ");
            }
            finally
            {
                if (root) Object.DestroyImmediate(root);
                if (inst) Object.DestroyImmediate(inst);
            }
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
            var lt = AssetDatabase.LoadAssetAtPath<GameObject>(Template);
            var mt = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Check(lt && mt, $"prefabs: the light tank {Template} {(lt ? "found" : "MISSING")}, the medium tank {PrefabPath} {(mt ? "found" : "MISSING (menu 1)")}");
            if (!lt || !mt) { Write(lines, pass, fail); return; }
            Transform N(GameObject g, string name) => g.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
            string P(Transform t, GameObject g) => !t || !t.parent ? "-" : t.parent == g.transform ? "<root>" : t.parent.name;
            var roles = new[] { "Model", "TankSuspension", "TankChassis", "Tank_hull", "Tank_headlamp", "Tank_headlamp.001", "TankTurretYaw", "Tank_turret", "TankBarrelPitch",
                                "TankRecoil", "Tank_barrel", "TankMuzzle", "tank_HatchHinge", "tank_Board_Approach", "tank_Board_LadderBottom", "tank_Board_LadderTop",
                                "tank_Board_HatchTop", "tank_Board_Seat", "tank_CabinFloor", "TankGunnerSeat", "TankDriverSeat", "TankLabelAnchor", "TankLeftExit",
                                "TankRightExit", "Track_Left", "Track_Right", "Tank_track_left", "Tank_track_right", "TankWheel_1", "TankWheel_6" };
            var miss = new List<string>(); int comps = 0;
            foreach (var nm in roles)
            {
                var a = N(lt, nm); var b = N(mt, nm);
                if (!a) { miss.Add(nm + " (not in the light tank!)"); continue; }
                if (!b) { miss.Add(nm + " MISSING"); continue; }
                bool climb = nm.StartsWith("tank_Board_L") || nm == "tank_Board_Approach";                          // the medium climbs by its fender step (under the suspension)
                if (P(a, lt) != P(b, mt) && nm != "Tank_track_left" && nm != "Tank_track_right" && !climb) miss.Add($"{nm} parent {P(b, mt)} (light {P(a, lt)})");
                foreach (var c in a.GetComponents<Component>())
                {
                    if (c is Transform || c is WreckClipBaked) continue;
                    comps++; if (!b.GetComponent(c.GetType()) && !OzMediumTankK87b.WellStandsIn(mt, nm, c)) miss.Add($"{nm}: {c.GetType().Name}");   // k87b: the light tank v1's drawn recess box under the hatch = the medium's modelled well (its own line below)
                }
            }
            Check(miss.Count == 0, $"{roles.Length} role nodes of the light tank and their {comps} components in the medium tank with the light tank's parents (the climb anchors: the fender step's){(miss.Count > 0 ? "; " + string.Join(", ", miss) : "")}");
            Check(OzMediumTankK87b.HatchWell(mt, out string k87bWell), k87bWell);                         // k87b: the dome's modelled hatch well (dark, never team paint, under the frame)
            Check(OzMediumTankK87b.SizeCheck(mt, out string k87bSize), k87bSize);                         // k87b, item 139: the medium between the light tank v2 and the heavy tank v2 from the RTS camera
            Check(N(mt, OzMediumTank.FrameNode), "node " + OzMediumTank.FrameNode + " (the modelled hatch frame on the dome: RoundHatch builds nothing, it marks the model)");
            var yaw = N(mt, "TankTurretYaw");
            bool UnderYaw(string nm) { var x = N(mt, nm); return yaw && x && x.IsChildOf(yaw); }
            Check(UnderYaw("Tank_turret") && UnderYaw("Tank_barrel") && UnderYaw("Tank_mantlet") && UnderYaw("tank_HatchHinge") && UnderYaw("TankV2_Antennas"),
                  "dome, mantlet, barrel, hatch and antennas turn with the turret (under TankTurretYaw: the head kick and the turret toss move them as one)");
            var mz = N(mt, "TankMuzzle");
            Check(mz && mz.parent && mz.parent.name == "TankRecoil" && mz.localPosition.z < -1f && Mathf.Abs(mz.localPosition.x) < 0.01f, $"the muzzle on the recoil slide, fires along -z: {(mz ? mz.localPosition.ToString("F3") : "-")}");
            var bm = N(mt, "Tank_barrel") ? N(mt, "Tank_barrel").GetComponent<MeshFilter>() : null; var mm = N(mt, "Tank_mantlet") ? N(mt, "Tank_mantlet").GetComponent<MeshFilter>() : null;
            var tm = N(mt, "Tank_turret") ? N(mt, "Tank_turret").GetComponent<MeshFilter>() : null;
            if (bm && mm && tm && bm.sharedMesh && mm.sharedMesh && tm.sharedMesh)
            {
                float rootIn = bm.sharedMesh.bounds.max.z - mm.sharedMesh.bounds.min.z;
                var pitch = N(mt, "TankBarrelPitch");
                float slide = 1.133f * UnitAim.SlewFor(OzMediumTank.Type, ZKind.Vehicle).recoilK;                  // UnitAim's slide peak (envelope 1.133 x recoilK)
                float back = pitch.localPosition.z + bm.sharedMesh.bounds.max.z + slide;
                Check(rootIn > 0.25f && slide <= OzMediumTank.SlideMaxM && back < tm.sharedMesh.bounds.max.z,
                      $"the barrel root {rootIn:0.00} m inside the collar's front face; the slide's peak {slide:0.000} m (at most {OzMediumTank.SlideMaxM:0.00}: the root stays in the dome); its end at z {back:0.00} (the dome's back {tm.sharedMesh.bounds.max.z:0.00})");
            }
            else Check(false, "barrel / mantlet / turret meshes missing");
            var susp = N(mt, "TankSuspension");
            var gear = mt.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("TankWheel_") || t.name.StartsWith("Track_") || t.name.StartsWith("Tank_track_") || t.name.StartsWith("Tank_sideplate_")).ToList();
            int wheels = gear.Count(t => System.Text.RegularExpressions.Regex.IsMatch(t.name, "^TankWheel_\\d+$"));
            Check(susp && gear.Count >= 16 && gear.All(t => !t.IsChildOf(susp)) && wheels == 10 && N(mt, "Tank_sideplate_left") && N(mt, "Tank_sideplate_left").parent.name == "Tank_track_left"
                  && N(mt, "Tank_sideplate_right").parent.name == "Tank_track_right" && N(mt, "Tank_hull").IsChildOf(susp) && yaw.IsChildOf(susp),
                  $"item 146: {wheels} road wheels, both belts and both side plates (on their belts) beside the suspension node, never under it (planted); hull and turret on TankSuspension");
            Check(TrackGround.Types.Contains(OzMediumTank.Type) && HeavyRecoil.Types.Contains(OzMediumTank.Type) && VehicleMotion.ScrollMaterials.Contains(OzMediumTank.BeltMaterial) && OzV2Units.Of(OzMediumTank.Type) == U,
                  "the medium tank is planted (TrackGround.Types), kicks and rocks (HeavyRecoil.Types), its belts roll on screen (VehicleMotion.ScrollMaterials) and View/OzV2Units.cs serves it");
            {
                var hull = N(mt, "Tank_hull").GetComponent<MeshFilter>(); var bl = N(mt, "Tank_track_left").GetComponent<MeshFilter>(); var br = N(mt, "Tank_track_right").GetComponent<MeshFilter>();
                if (hull && bl && br && hull.sharedMesh && hull.sharedMesh.isReadable && bl.sharedMesh.isReadable)
                {
                    var root = mt.transform;
                    Bounds BeltBox(MeshFilter f) { var b = f.sharedMesh.bounds; var w = new Bounds(root.InverseTransformPoint(f.transform.TransformPoint(b.center)), Vector3.zero); for (int i = 0; i < 8; i++) w.Encapsulate(root.InverseTransformPoint(f.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1))))); return w; }
                    var L = BeltBox(bl); var R = BeltBox(br); float lowL = 9f, lowR = 9f;
                    foreach (var vtx in hull.sharedMesh.vertices)
                    {
                        var p = root.InverseTransformPoint(hull.transform.TransformPoint(vtx));
                        if (p.x > L.min.x + 0.02f && p.x < L.max.x - 0.02f && p.z > L.min.z && p.z < L.max.z) lowL = Mathf.Min(lowL, p.y - L.max.y);
                        if (p.x > R.min.x + 0.02f && p.x < R.max.x - 0.02f && p.z > R.min.z && p.z < R.max.z) lowR = Mathf.Min(lowR, p.y - R.max.y);
                    }
                    Check(lowL > 0.015f && lowR > 0.015f && Mathf.Abs(Mathf.Min(L.min.y, R.min.y)) < 0.01f, $"at rest the hull clears its belts by {lowL:0.000} / {lowR:0.000} m (left / right; the model: 4.1-4.9 cm), the belts' bottom at {Mathf.Min(L.min.y, R.min.y):0.000} m (on the ground)");
                }
                else Check(false, "hull / belt meshes not readable for the clearance check");
            }
            int baked = 0, holders = 0;
            foreach (var nm in new[] { "Tank_hull", "Tank_track_left", "Tank_track_right" })
            {
                var p = N(mt, nm); var h = p ? p.Find("OZ_WreckFragments") : null;
                if (h) { holders++; if (WreckPieces.IsBaked(h)) baked++; }
                Check(h && h.childCount >= 2, $"wreck fragments under {nm}: {(h ? h.childCount : 0)}");
            }
            Check(baked == holders && holders == 3, $"clipped wreck bake: {baked} of {holders} holders baked");
            Check(yaw && yaw.GetComponentsInChildren<Transform>(true).Count(x => x.name == "OZ_WreckFragments") == 0, "no fragments under the turret (it flies whole: the turret toss)");
            var rs = mt.GetComponentsInChildren<MeshRenderer>(true).Where(r => !Under(r.transform, "OZ_WreckFragments")).ToList();
            Check(rs.All(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off), $"{rs.Count} renderers cast shadows (as the light tank)");
            Check(rs.All(r => r.sharedMaterials.All(m => m && m.shader && m.shader.name.Contains("TeamLit"))), "TeamLit materials on every renderer, no empty slot");
            foreach (var mn in new[] { "mtank_v2_body", "mtank_v2_head", "mtank_v2_track" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(Mats + mn + ".mat");
                bool ok = m && m.GetTexture("_BaseMap") && m.GetFloat("_LegacyHeavy") > 0.5f && m.GetFloat("_PaintFromAlpha") > 1.5f && m.GetFloat("_TeamMode") > 0.5f
                          && Mathf.Abs(m.GetVector("_HeavyLook").y - 0.0805f) < 0.01f;
                Check(ok, $"{mn}: albedo {(m && m.GetTexture("_BaseMap") ? m.GetTexture("_BaseMap").name : "none")}, _HeavyLook {(m ? m.GetVector("_HeavyLook").ToString("F4") : "-")} (y = the light tank v1's empty grey 0.0805: dark, never white), _DarkColor {(m ? m.GetColor("_DarkColor").ToString("F3") : "-")}");
            }
            var wm = AssetDatabase.LoadAssetAtPath<Material>(Mats + "mtank_v2_well.mat");
            Check(wm && wm.GetFloat("_TeamMode") < 0.5f, "the hatch well is never team paint");
            var turretMr = N(mt, "Tank_turret") ? N(mt, "Tank_turret").GetComponent<MeshRenderer>() : null;
            Check(turretMr && OzV2Units.NoRoofPanel("Tank_turret", turretMr.sharedMaterial), "no team roof panel on its dome (the team colour is the red block)");
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
            var b1 = Bx(lt); var b2 = Bx(mt);
            float r2 = Mathf.Clamp(Mathf.Max(b2.extents.x, b2.extents.z) * 0.8f, 1.95f, 3f);
            Check(r2 >= 1.9f && r2 <= 2.2f, $"spacing radius {r2:0.00} m; size light v1 {b1.size.x:0.00} x {b1.size.y:0.00} x {b1.size.z:0.00} m, medium {b2.size.x:0.00} x {b2.size.y:0.00} x {b2.size.z:0.00} m (width x height x length, gun and antennas in)");
            var steps = N(mt, "tank_v2_Steps") ? N(mt, "tank_v2_Steps").GetComponent<MeshRenderer>() : null;
            var lbot = N(mt, "tank_Board_LadderBottom"); var ltop = N(mt, "tank_Board_LadderTop");
            if (steps)
            {
                var sbx = steps.bounds;
                Check(sbx.min.y > 0.6f && ltop && lbot && lbot.position.y < 0.3f && ltop.position.y > sbx.min.y && ltop.position.y < 1.8f,
                      $"the climb: the fender step {sbx.min.y:0.00}..{sbx.max.y:0.00} m up (flush on the fender, never on the ground), LadderBottom {lbot.position.y:0.00} m, LadderTop {ltop.position.y:0.00} m (the deck)");
            }
            else Check(false, "no tank_v2_Steps renderer");
            var fr = N(mt, OzMediumTank.FrameNode); var hh = N(mt, "tank_HatchHinge"); var top = N(mt, "tank_Board_HatchTop");
            Check(fr && hh && top && Vector2.Distance(new Vector2(fr.position.x, fr.position.z), new Vector2(top.position.x, top.position.z)) < 0.12f && OzV2Units.FrameNodeFor("tank_") == OzMediumTank.FrameNode,
                  "the real hatch on the dome: frame, hinge and Board_HatchTop over it; RoundHatch uses the modelled one (OzV2Units.FrameNodeFor(\"tank_\"))");
            var trk = N(mt, "Tank_track_left") ? N(mt, "Tank_track_left").GetComponent<MeshFilter>() : null;
            float vr = 0f; if (trk && trk.sharedMesh && trk.sharedMesh.isReadable) { var uv = trk.sharedMesh.uv; if (uv.Length > 0) vr = uv.Max(u => u.y) - uv.Min(u => u.y); }
            Check(vr > 1.5f && N(mt, "Tank_track_left").GetComponent<MeshRenderer>().sharedMaterial.name == OzMediumTank.BeltMaterial, $"belt V range {vr:0.00} tiles, material {OzMediumTank.BeltMaterial} (in ScrollMaterials: the tread rolls in the built game)");
            foreach (var (file, label) in new[] { ("OpenZ/VehicleLights/vehicle_lights", "VehicleLights"), ("OpenZ/UnitBlinkers/unit_blinkers", "UnitBlinkers") })
            {
                var ta = Resources.Load<TextAsset>(file); string txt = ta ? ta.text : "";
                Check(txt.Contains("\"type\": \"" + OzMediumTank.Type + "\""), $"{label}: entry '{OzMediumTank.Type}' {(txt.Contains("\"type\": \"" + OzMediumTank.Type + "\"") ? "present" : "MISSING")} (its own sockets, measured on its meshes)");
            }
            // pictures, names, lists
            foreach (var c in new[] { Team.Red, Team.Blue })
            {
                string cn = c == Team.Red ? "red" : "blue";
                var icon = UnitArt.Icon(OzMediumTank.Type, c); var ref1 = Resources.Load<Texture2D>("OpenZ/UI/Icons/icon_light_tank_" + cn);
                Check(icon && icon.name == "icon_mtank_" + cn && ref1 && icon.width == ref1.width, $"production picture {cn}: {(icon ? icon.name + " " + icon.width + " px" : "NONE")} (the light tank's {(ref1 ? ref1.width + " px" : "-")})");
                var por = UnitArt.VehiclePortrait(OzMediumTank.Type, c); var ref2 = Resources.Load<Texture2D>("OpenZ/UI/PortraitsVehicles/portrait_light_tank_" + cn);
                Check(por && por.name == "portrait_mtank_" + cn && ref2 && por.width == ref2.width, $"unit card portrait {cn}: {(por ? por.name + " " + por.width + " px" : "NONE")} (the light tank's {(ref2 ? ref2.width + " px" : "-")})");
                Check(OzBadgeArt.FaceFor(OzMediumTank.Type, c).Contains("mtank"), $"badge face {cn}: {OzBadgeArt.FaceFor(OzMediumTank.Type, c)}");
                for (int tier = 1; tier <= 4; tier++)
                {
                    var f2 = Resources.Load<Texture2D>(OzBadgeArt.Root + tier + "/face_mtank_" + cn); var f1 = Resources.Load<Texture2D>(OzBadgeArt.Root + tier + "/face_light_" + cn);
                    Check(f2 && f1 && f2.width == f1.width, $"badge face x{tier} {cn}: {(f2 ? f2.width + " px" : "MISSING")} (the light tank's {(f1 ? f1.width + " px" : "-")})");
                }
            }
            Check(UnitInfo.Name(OzMediumTank.Type) == "MEDIUM TANK" && UnitInfo.Short(OzMediumTank.Type) == "MEDIUM" && UnitInfo.Name("medium") == "MECH" && UnitInfo.WeaponOfType(OzMediumTank.Type) == "CANNON",
                  $"names: {UnitInfo.Name(OzMediumTank.Type)} / tile {UnitInfo.Short(OzMediumTank.Type)} / code {UnitInfo.Code(OzMediumTank.Type)} / weapon {UnitInfo.WeaponOfType(OzMediumTank.Type)}; the mech stays {UnitInfo.Name("medium")}");
            var pv = typeof(OzCheatSpawner).GetField("Vehicles", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as string[];
            int pi = pv != null ? System.Array.IndexOf(pv, OzMediumTank.Type) : -1, pm = pv != null ? System.Array.FindLastIndex(pv, t => t == "medium" || t == OzMech.TileV1 || t == OzMech.TileV2) : -1;   // k92: the mech's tiles are MECH v1 / v2 (medium@v1 / @v2) since the mech v2
            Check(pi >= 0 && pi == pm + 1, $"testing panel (ALT+P) VEHICLES: {(pv != null ? string.Join(", ", pv) : "not found")} (MEDIUM TANK after the mech)");
            int ue = System.Array.IndexOf(OzUeLiveStats.Units, OzMediumTank.Type), uem = System.Array.IndexOf(OzUeLiveStats.Units, "medium");
            Check(ue == uem + 1 && OzUePlayground.Units.SequenceEqual(OzUeLiveStats.Units) && System.Array.IndexOf(OzUePlayground.Vehicles, OzMediumTank.Type) == System.Array.IndexOf(OzUePlayground.Vehicles, "medium") + 1,
                  $"Unit editor: its {OzUeLiveStats.Units.Length} units in the build-menu order, the medium tank after the mech (dock, card, Custom stats, lists)");
            Check(System.Array.IndexOf(UnitVoices.Types, OzMediumTank.Type) >= 0, $"voices: '{OzMediumTank.Type}' in UnitVoices.Types");
            {
                var ta = Resources.Load<TextAsset>(AudioDirector.TablePath);
                var table = ta ? JsonUtility.FromJson<SoundTable>(ta.text) : null;
                int clips = 0, load = 0; var have = new List<string>();
                foreach (var id in new[] { "vo_select_x_mtank", "vo_order_x_mtank_move", "vo_order_x_mtank_attack" })
                {
                    var e = table?.events?.FirstOrDefault(x => x != null && x.id == id); if (e == null) continue;
                    have.Add(id); foreach (var cr in e.clips) { clips++; if (Resources.Load<AudioClip>(cr.res)) load++; }
                }
                Check(have.Count == 3 && clips == 22 && load == 22, $"voice pools: {string.Join(", ", have)}; {load} of {clips} clips load (12 lines + 10 extra lines)");
            }
            bool dv = ZWorld.MTankDeviation; ZWorld.MTankDeviation = true;
            try
            {
                var vl = ZBuildLists.For("vehicle_factory", 2).ToList(); var fl = ZBuildLists.For("fort_front", 5).ToList();
                Check(vl.IndexOf(OzMediumTank.Type) == vl.IndexOf("medium") + 1 && fl.IndexOf(OzMediumTank.Type) == fl.IndexOf("medium") + 1 && ZBuildLists.For("vehicle_factory", 1).ToList().IndexOf(OzMediumTank.Type) < 0,
                      $"build lists with DV145: vehicle factory level 2: {string.Join(", ", vl)}; level 1 without it (the mech's tech level)");
            }
            finally { ZWorld.MTankDeviation = dv; }
            U.Forget();
            Check(U.PrefabV2 && U.PrefabFor(OzMediumTank.Key, null) == U.PrefabV2, "ZGame finds its prefab by its key Z_mtank (View/OzV2Units.cs)");
            Write(lines, pass, fail);
        }

        static void Write(List<string> lines, int pass, int fail)
        {
            string head = $"[OzMediumTank] parity check {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail} checks";
            Directory.CreateDirectory(ProofDir);
            File.WriteAllText(Path.Combine(ProofDir, "parity_check.txt"), head + "\n" + string.Join("\n", lines) + "\n");
            Debug.Log(head + "\n" + string.Join("\n", lines));
        }

        // ---------------------------------------------------------------- 3 rules (headless)
        [MenuItem(M + "3 Rules check (counted, headless)", false, 3)]
        public static void Rules()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[OzMediumTank] stop Play first (the check sets the simulation's static switches)"); return; }
            string report;
            using (OzV11Kit.GameSwitches()) report = OzV12MTankCount.Run(OzV11Kit.SettingsText, OzV11Kit.MapText);
            Directory.CreateDirectory(ProofDir);
            File.WriteAllText(Path.Combine(ProofDir, "rules_check.txt"), report);
            Debug.Log("[OzMediumTank] rules check\n" + report);
        }

        // ---------------------------------------------------------------- 4 Play proof, 5 .. 16 films and tests
        [MenuItem(M + "4 Play proof (L02 Desert)", false, 4)] static void ProofL02() => Proof("L02", L02);
        [MenuItem(M + "4 Play proof (L17 Car Park)", false, 5)] static void ProofL17() => Proof("L17", L17);
        static bool Ready()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[OzMediumTank] stop Play first"); return false; }
            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }
        static void Proof(string job, string scene)
        {
            if (!Ready()) return;
            PlayerPrefs.SetString(MTankProof.AutoPref, job); PlayerPrefs.Save();
            EditorSceneManager.OpenScene(scene);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(M + "5 Film: built in a factory after the mech, drives off (L02)", false, 20)] static void FilmFactory() => Film("factory");
        [MenuItem(M + "6 Film: light, medium and heavy side by side (L02)", false, 21)] static void FilmLineup() => Film("lineup");
        [MenuItem(M + "7 Film: one medium tank fights to its death (L02)", false, 22)] static void FilmFight() => Film("fight");
        [MenuItem(M + "8 Film: the tread at the track ends (L02, crops 0.1 s apart)", false, 23)] static void FilmTread() => Film("tread");
        [MenuItem(M + "9 Film: a robot boards an empty medium tank (L02)", false, 24)] static void FilmBoard() => Film("board");
        [MenuItem(M + "9 Film: a robot boards an empty medium tank, close camera 32 m (L02)", false, 24)] static void FilmBoardClose() => Film("board_close");
        [MenuItem(M + "10 Film: the driver up against snipers (L02)", false, 25)] static void FilmSnipe() => Film("snipe");
        [MenuItem(M + "11 Film: a battle of medium tanks (L02)", false, 26)] static void FilmBattle() => Film("battle");
        [MenuItem(M + "12 Test: the turret never shakes between targets (L02)", false, 50)] static void Turret() => Film("turret");
        [MenuItem(M + "13 Test + film: head kick and hull rock at each shot (L02)", false, 51)] static void Recoil() => Film("recoil");
        [MenuItem(M + "14 Test: the turret in a fight never shakes (L02)", false, 52)] static void Combat() => Film("combat");
        [MenuItem(M + "15 Test + film: the tracks stay on the ground, only the hull rocks (L02, item 146)", false, 53)] static void Plant() => Film("plant");
        static void Film(string job)
        {
            if (!Ready()) return;
            PlayerPrefs.SetString(HeavyV2Films.SubjectPref, OzMediumTank.Type);
            PlayerPrefs.SetString(HeavyV2Films.AutoPref, job); PlayerPrefs.Save();
            EditorSceneManager.OpenScene(L02);
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
