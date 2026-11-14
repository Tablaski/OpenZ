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
    /// Gun v2 (1.2 v2 units, owner item 140), the editor side; the mech v2's menus (Editor/OzMechV2Setup.cs) for the gun.
    ///  1 Build: the v2 view prefab Resources/OpenZ/GunV2/Z_gun_v2 from Models/Legacy/gun_v2.glb, made like Prefabs/Z_gun (root + TeamPaint
    ///    copied from v1, the GLB's root CannonRoot as "Model" at v1's place (0, 0, 0.41) x 1: the turn axis on the same prefab point, so the
    ///    plinth stands where v1's pedestal stands, on the ground and on a fort tower's pad), the GLB's node tree with v1's names, the TeamLit
    ///    material gun_v2_body on every renderer, v1's renderer settings, an empty marker node gun_v2_Frame (it marks a v2 view); then its
    ///    wreck fragments (files gunv2_* beside v1's gun_*: v1's are never touched): the game's own cut of the plinth (the turret flies whole
    ///    at the death, Zod's ECannonDeath toss), then the clipped bake. v1 stays as it is.
    ///  2 Parity check (counted, edit mode): v1's node names with v1's parents and components, identity rests, the Model at v1's place, the
    ///    plinth on the ground, the muzzle, the elevation stop, the steady aim range, the kick sizes, the chair that breaks off, materials,
    ///    wreck, the spacing radius, blinker data, pictures, the switch and the testing panel's tiles.
    ///  3 Play proof L02 / L17: GunV2Proof (v1 and v2 side by side; turret turn and kick, the barrel's slide, the base planted every frame,
    ///    the gunner sniped and the gun manned again, a v2 gun on a fort tower, damage, death with the chair breaking off and the base
    ///    breaking apart).
    ///  5 .. 15 the films and tests (HeavyV2Films for the gun, L02, 1920 x 1080 with sound).
    /// </summary>
    public static class OzGunV2Setup
    {
        public const string Glb = "Assets/OpenZ/Models/Legacy/gun_v2.glb", V1Prefab = "Assets/OpenZ/Prefabs/Z_gun.prefab",
                            Folder = "Assets/OpenZ/Resources/OpenZ/GunV2", PrefabPath = Folder + "/Z_gun_v2.prefab", Mats = "Assets/OpenZ/Materials/Units/";
        /// <summary>The wreck fragment file key: gunv2_* (OpenZWreckFragments clears "&lt;key&gt;_*" before it bakes; "gun_*" would take v1's files).</summary>
        public const string FragKey = "gunv2";
        const string M = "OpenZ/Gun v2/";
        const string L02 = "Assets/OpenZ/Scenes/L02_Desert.unity", L17 = "Assets/OpenZ/Scenes/L17_CarPark.unity";
        static readonly string ProofDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/v2_units/gun/proof"));
        static OzUnitModels U => OzGun.Models;
        /// <summary>The nodes v1's code turns or reads by name (UnitView, UnitAim, BoardingView, GunChairDebris, the death, the labels).</summary>
        static readonly string[] Roles = { "Model", "CannonPedestal", "CannonPedestal_Surface", "CannonTurretYaw", "CannonTurretYaw_Surface", "CannonGunnerSeat",
                                           "CannonBarrelPitch", "CannonBarrelPitch_Surface", "CannonBarrelRecoil", "CannonBarrelRecoil_Surface", "CannonMuzzle",
                                           "CannonLeftExit", "CannonRightExit", "LabelAnchor.003" };
        static readonly string[] Joints = { "CannonTurretYaw", "CannonBarrelPitch", "CannonBarrelRecoil", "CannonMuzzle", "CannonGunnerSeat" };

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
            Debug.Log("[OzGunV2] " + r);
            Directory.CreateDirectory(ProofDir);
            File.WriteAllText(Path.Combine(ProofDir, "build_report.txt"), r + "\n");
        }

        public static string BuildNow()
        {
            var glb = AssetDatabase.LoadAssetAtPath<GameObject>(Glb);
            var v1 = AssetDatabase.LoadAssetAtPath<GameObject>(V1Prefab);
            if (!glb) return "FAILED: no " + Glb + " (import it first)";
            if (!v1) return "FAILED: no " + V1Prefab;
            var body = AssetDatabase.LoadAssetAtPath<Material>(Mats + "gun_v2_body.mat");
            if (!body) return "FAILED: no " + Mats + "gun_v2_body.mat";
            var v1Model = v1.transform.Find("Model");
            var v1Tur = Deep(v1.transform, "CannonTurretYaw_Surface"); var v1Mr = v1Tur ? v1Tur.GetComponent<MeshRenderer>() : null;
            if (!v1Model || !v1Mr) return "FAILED: Z_gun has no Model / no CannonTurretYaw_Surface renderer";
            var sb = new StringBuilder();
            var inst = Object.Instantiate(glb);
            GameObject root = null;
            try
            {
                var model = Deep(inst.transform, "CannonRoot");
                if (!model) { var ped = Deep(inst.transform, "CannonPedestal"); if (ped) model = ped.parent; }   // the GLB's single root node CannonRoot is the imported asset's root (glTFast, one root node: no scene object), as MLRSRoot / MechRoot are found by their child
                if (!model) return "FAILED: the GLB has no CannonRoot";
                root = new GameObject(OzGun.V2Key) { layer = v1.layer };
                var tp = root.AddComponent<TeamPaint>();
                var tp1 = v1.GetComponent<TeamPaint>(); if (tp1) EditorUtility.CopySerialized(tp1, tp);
                model.SetParent(root.transform, false); model.name = "Model";
                model.localPosition = v1Model.localPosition; model.localRotation = v1Model.localRotation; model.localScale = v1Model.localScale;   // v1's place: (0, 0, 0.41) x 1
                int n = 0, byName = 0; var unknown = new List<string>();
                foreach (var mr in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var src = mr.sharedMaterials; var all = new Material[src.Length];
                    for (int i = 0; i < src.Length; i++)
                    {
                        string mn = src[i] ? src[i].name : "";
                        int k = mn.IndexOf(' '); if (k > 0) mn = mn.Substring(0, k);
                        all[i] = body;
                        if (mn == "GunV2_body") byName++; else unknown.Add(mr.name + "[" + i + "] '" + (src[i] ? src[i].name : "none") + "'");
                    }
                    mr.sharedMaterials = all;
                    mr.shadowCastingMode = v1Mr.shadowCastingMode; mr.receiveShadows = v1Mr.receiveShadows;
                    mr.lightProbeUsage = v1Mr.lightProbeUsage; mr.reflectionProbeUsage = v1Mr.reflectionProbeUsage;
                    mr.motionVectorGenerationMode = v1Mr.motionVectorGenerationMode; mr.allowOcclusionWhenDynamic = v1Mr.allowOcclusionWhenDynamic;
                    n++;
                }
                foreach (var c in model.GetComponentsInChildren<Component>(true))                           // only transforms, filters, renderers (as v1)
                    if (!(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer)) Object.DestroyImmediate(c);
                var frame = new GameObject(OzGun.FrameNode).transform;                                      // the v2 marker (OzUnitModels.IsV2), an empty node
                frame.SetParent(model, false);
                Layer(model, v1Model.gameObject.layer);
                sb.Append($"prefab: {n} renderers, {byName} material slots by the GLB's material name GunV2_body{(unknown.Count > 0 ? ", others: " + string.Join(", ", unknown) : "")}; " +
                          $"Model at {model.localPosition.ToString("F3")} x {model.localScale.x:0.000} (v1 {v1Model.localPosition.ToString("F3")}); marker {OzGun.FrameNode}; ");
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/OpenZ/Resources/OpenZ", "GunV2");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok) return "FAILED: the prefab could not be saved at " + PrefabPath;
                sb.Append("saved ").Append(PrefabPath).Append("; ");
            }
            finally
            {
                if (root) Object.DestroyImmediate(root);
                if (inst) Object.DestroyImmediate(inst);
            }
            // wreck fragments: OpenZWreckFragments' own cut of the largest parts outside the turret (its Bake is private: called by name; the
            // plinth, 1400 tris), then the clipped bake
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
            Vector3 L(GameObject g, Transform t) => g.transform.InverseTransformPoint(t.position);
            {   // v1's role nodes in v2 with v1's parents and components (v2 has no owner strips: its team colour is in its paint)
                var miss = new List<string>(); int comps = 0;
                foreach (var nm in Roles)
                {
                    var a = N(v1, nm); var b = N(v2, nm);
                    if (!a) { miss.Add(nm + " (not in v1!)"); continue; }
                    if (!b) { miss.Add(nm + " MISSING in v2"); continue; }
                    if (P(a, v1) != P(b, v2)) miss.Add($"{nm} parent {P(b, v2)} (v1 {P(a, v1)})");
                    foreach (var c in a.GetComponents<Component>())
                    {
                        if (c is Transform || c is WreckClipBaked) continue;
                        comps++; if (!b.GetComponent(c.GetType())) miss.Add($"{nm}: {c.GetType().Name}");
                    }
                }
                Check(miss.Count == 0, $"{Roles.Length} role nodes and their {comps} components in v2 with v1's parents{(miss.Count > 0 ? "; " + string.Join(", ", miss) : "")} (v1's Cannon_owner_strip / .001 are not on v2: its red block and tag stripes carry the team colour)");
                var bad = Joints.Where(j => !N(v2, j) || Quaternion.Angle(Quaternion.identity, N(v2, j).localRotation) > 0.01f).ToList();
                Check(bad.Count == 0, $"{Joints.Length} joints at an identity rest (as v1: UnitAim turns them from rest){(bad.Count > 0 ? ": NOT " + string.Join(", ", bad) : "")}");
                Check(N(v2, OzGun.FrameNode) && !N(v1, OzGun.FrameNode) && U.IsV2(v2.transform) && !U.IsV2(v1.transform), $"the v2 marker {OzGun.FrameNode} on v2 only (OzUnitModels.IsV2)");
            }
            {   // the Model at v1's place, the turn axis on v1's prefab point, the plinth on the ground
                var m1 = v1.transform.Find("Model"); var m2 = v2.transform.Find("Model");
                var y1 = N(v1, "CannonTurretYaw"); var y2 = N(v2, "CannonTurretYaw");
                Check(m1 && m2 && (m1.localPosition - m2.localPosition).sqrMagnitude < 1e-8f && Mathf.Abs(m1.localScale.x - m2.localScale.x) < 1e-5f && (L(v1, y1) - L(v2, y2)).magnitude < 0.001f,
                      $"Model at {(m2 ? m2.localPosition.ToString("F3") : "-")} x {(m2 ? m2.localScale.x : 0f):0.000} (v1 {(m1 ? m1.localPosition.ToString("F3") : "-")}); the turn axis at {L(v2, y2).ToString("F3")} (v1 {L(v1, y1).ToString("F3")}): the same pad / ground point");
                float Low(GameObject g, string node)
                {
                    var mf = N(g, node) ? N(g, node).GetComponent<MeshFilter>() : null;
                    if (!mf || !mf.sharedMesh || !mf.sharedMesh.isReadable) return float.NaN;
                    float y = 9f; foreach (var v in mf.sharedMesh.vertices) y = Mathf.Min(y, g.transform.InverseTransformPoint(mf.transform.TransformPoint(v)).y);
                    return y;
                }
                float b1 = Low(v1, "CannonPedestal_Surface"), b2 = Low(v2, "CannonPedestal_Surface");
                Check(Mathf.Abs(b2) < 0.01f, $"the plinth on the ground: its lowest point {b2:0.000} m (v1's pedestal {b1:0.000} m)");
                var tm = N(v2, "CannonTurretYaw_Surface").GetComponent<MeshFilter>(); var pm = N(v2, "CannonPedestal_Surface").GetComponent<MeshFilter>();
                Check(tm && tm.sharedMesh && tm.sharedMesh.isReadable && tm.sharedMesh.subMeshCount == 1 && pm && pm.sharedMesh && pm.sharedMesh.isReadable,
                      $"the turret mesh readable with one sub-mesh ({(tm && tm.sharedMesh ? tm.sharedMesh.subMeshCount : 0)}: GunChairDebris splits it), the plinth readable (the wreck cut)");
            }
            {   // the barrel: the muzzle on the barrel's axis at its front end; the elevation stop (v2 28, v1 40); lesson 5: the steady aim's range
                var mz = N(v2, "CannonMuzzle"); var rc = N(v2, "CannonBarrelRecoil"); var tube = N(v2, "CannonBarrelRecoil_Surface").GetComponent<MeshFilter>();
                float front = tube && tube.sharedMesh ? tube.sharedMesh.bounds.min.z : 0f;
                bool axis = mz && rc && mz.parent == rc && Mathf.Abs(mz.localPosition.x) < 0.01f && Mathf.Abs(mz.localPosition.y) < 0.01f && Mathf.Abs(mz.localPosition.z - front) < 0.02f;
                Check(axis, $"CannonMuzzle on CannonBarrelRecoil at {(mz ? mz.localPosition.ToString("F3") : "-")}: on the barrel's axis at its front end (z {front:0.000}), firing along its -z");
                var s1 = UnitAim.SlewFor(OzGun.Type, ZKind.Cannon);
                Check(OzGun.PitchMaxV2 == 28f && s1.pitchMax == 40f && s1.pitchMin == -15f,
                      $"the elevation: v2 stops at {OzGun.PitchMaxV2} deg (UnitAim.GunSlew on a v2 view: its collar never over the box's front bar), v1 {s1.pitchMin}..{s1.pitchMax} as before; turn {s1.yawSpeed} deg/s, kick {s1.kickDeg} deg, slide x {s1.recoilK} for both");
                float Reach(GameObject g) { var y = N(g, "CannonTurretYaw"); var m = N(g, "CannonMuzzle"); if (!y || !m) return -1f; var d = m.position - y.position; d.y = 0f; return d.magnitude; }
                float r2 = Reach(v2), r1 = Reach(v1);
                Check(r2 > 0f && UnitAim.SteadyRangeK * r2 <= 4f, $"the turret's steady aim (lesson 5): the muzzle {r2:0.00} m from the turn axis (v1 {r1:0.00} m) x {UnitAim.SteadyRangeK} = {UnitAim.SteadyRangeK * r2:0.00} m, inside UnitAim.Steady's 4 m minimum range; the world-heading slew serves the gun (UnitAim.CombatGun {UnitAim.CombatGun}) and it rests after a waited shot and before a turn back (UnitAim.ShotSettleTypes / TurnRestTypes, registered at Play start: {typeof(UnitAim).GetMethod("K88GunTypes", BindingFlags.NonPublic | BindingFlags.Static) != null})");
            }
            // registries: the gun in HeavyRecoil (both models: the turret's kick; no base move), a two-model unit
            Check(HeavyRecoil.Types.Contains(OzGun.Type) && OzV2Units.Of(OzGun.Type) == U && HeavyRecoil.DesignHeadSlide(OzGun.Type, 1) > 0f && HeavyRecoil.DesignHeadSlide(OzGun.Type, 2) > 0f
                  && HeavyRecoil.DesignBodyRock(OzGun.Type) == 0f && HeavyRecoil.DesignBodySlide(OzGun.Type) == 0f,
                  $"the gun's kick (HeavyRecoil.Types): turret {HeavyRecoil.DesignHeadSlide(OzGun.Type, 1):0.00} / {HeavyRecoil.DesignHeadSlide(OzGun.Type, 2):0.00} m + {HeavyRecoil.DesignHeadRock(OzGun.Type):0.0} deg (v1 / v2), base {HeavyRecoil.DesignBodyRock(OzGun.Type):0.0} deg + {HeavyRecoil.DesignBodySlide(OzGun.Type):0.000} m (a building: it never moves); a two-model unit (OzV2Units)");
            {   // the chair that breaks off at the death (GunChairDebris' own split, called by name): v1 its pan + backrest as before; v2 its pan + both arms
                var split = typeof(GunChairDebris).GetMethod("Split", BindingFlags.NonPublic | BindingFlags.Static);
                string Chair(GameObject g, out int chairTris)
                {
                    chairTris = 0;
                    var mf = N(g, "CannonTurretYaw_Surface").GetComponent<MeshFilter>(); var seat = N(g, "CannonGunnerSeat");
                    if (split == null || !mf || !mf.sharedMesh || !seat) return "not found";
                    var copy = Object.Instantiate(mf.sharedMesh);                                          // a copy: the split's cache keys on the mesh
                    try
                    {
                        var r = split.Invoke(null, new object[] { copy, "gun", (Vector3?)mf.transform.InverseTransformPoint(seat.position) }) as Mesh[];
                        if (r == null) return "no chair found";
                        chairTris = r[1].triangles.Length / 3; int rest = r[0].triangles.Length / 3;
                        var b = r[1].bounds;
                        return $"{chairTris} chair tris of {chairTris + rest}, chair box {b.min.ToString("F2")}..{b.max.ToString("F2")}";
                    }
                    finally { Object.DestroyImmediate(copy); }
                }
                string c1 = Chair(v1, out int t1), c2 = Chair(v2, out int t2);
                Check(GunV2Seat.NoChairModel(v2, out string k92Chair) && t1 == 88, $"the chair breaking off at the death (GunChairDebris, the parts within 0.7 m of CannonGunnerSeat): v1 {c1} (its pan + backrest, as before), v2 {k92Chair}");   // k92: the gun v2 has no chair (View/OzGun.K92.cs)
            }
            // wreck fragments (the death's break-apart): the game's cut of the plinth (the turret flies whole)
            int holders = 0, baked = 0; var cut = new List<string>();
            foreach (var h in v2.GetComponentsInChildren<Transform>(true).Where(t => t.name == "OZ_WreckFragments"))
            {
                holders++; if (WreckPieces.IsBaked(h)) baked++;
                cut.Add($"{(h.parent ? h.parent.name : "-")} ({h.childCount})");
            }
            Check(holders >= 1 && cut.All(c => c.StartsWith("CannonPedestal_Surface")), $"wreck fragments: {holders} part(s) cut: {string.Join(", ", cut)} (v1: its pedestal; the turret is tossed whole as v1's)");
            Check(baked == holders && holders > 0, $"clipped wreck bake: {baked} of {holders} holders baked");
            // renderers: shadows, materials (team colour on the red block and tag stripes only; neutral dark grey, item 136)
            var rs = v2.GetComponentsInChildren<MeshRenderer>(true).Where(r => !Under(r.transform, "OZ_WreckFragments")).ToList();
            Check(rs.Count == 4 && rs.All(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off), $"{rs.Count} renderers (plinth, turret, collar, barrel), all cast shadows (as v1)");
            Check(rs.All(r => r.sharedMaterials.Length == 1 && r.sharedMaterials.All(m => m && m.shader && m.shader.name.Contains("TeamLit") && m.name == "gun_v2_body")), "TeamLit gun_v2_body on every renderer, one slot each, no empty slot");
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(Mats + "gun_v2_body.mat");
                bool ok = m && m.GetTexture("_BaseMap") && m.GetTexture("_BumpMap") && m.GetFloat("_LegacyHeavy") > 0.5f && m.GetFloat("_PaintFromAlpha") > 1.5f && m.GetFloat("_TeamMode") > 0.5f
                          && Mathf.Abs(m.GetVector("_HeavyLook").y - 0.236f) < 0.005f;   // k93, item 153: the v1 gun's empty grey
                Check(ok, $"gun_v2_body: albedo {(m && m.GetTexture("_BaseMap") ? m.GetTexture("_BaseMap").name : "none")}, normal {(m && m.GetTexture("_BumpMap") ? m.GetTexture("_BumpMap").name : "none")}, _HeavyLook {(m ? m.GetVector("_HeavyLook").ToString("F4") : "-")} (y = the v1 gun's empty paint, TeamLit's own empty grey of the v1 gun 0.4005 x (0.60, 0.575, 0.545), luminance 0.2315 / the warm tint 0.983 = 0.236, the value k98 draws at run time; item 153; owned paint unchanged), _DarkColor {(m ? m.GetColor("_DarkColor").ToString("F3") : "-")}");
                var r1 = N(v1, "CannonTurretYaw_Surface").GetComponent<MeshRenderer>();
                Check(OzV2Units.NoRoofPanel("CannonTurretYaw_Surface", m) && r1 && !OzV2Units.NoRoofPanel("CannonTurretYaw_Surface", r1.sharedMaterial),
                      "no shader roof panel on v2's turret (its team colour is in its paint: the box top and the tag stripes); v1 keeps its roof panel");
            }
            Check(OzGun.NoNose(v2) && !OzGun.NoNose(v1), "GunNose (legacy muzzle collar) is built on v1 only: v2 has its own octagonal muzzle (the collar would float in front of it)");
            {   // the spacing radius: v1 as before (UnitView's formula on its renderers); v2 its own and at least its chair's sweep less the keep-clear
                float R(GameObject g, float yaw, out float keep)
                {
                    keep = 0f;
                    var i = (GameObject)PrefabUtility.InstantiatePrefab(g);
                    try
                    {
                        i.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                        var rr = i.GetComponentsInChildren<Renderer>(); var bb = rr[0].bounds;
                        foreach (var r in rr) if (!(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)) bb.Encapsulate(r.bounds);
                        float rad = Mathf.Clamp(Mathf.Max(bb.extents.x, bb.extents.z) * 0.95f, 1.5f, 3f);
                        keep = OzGun.KeepClear(i.transform);
                        return rad;
                    }
                    finally { Object.DestroyImmediate(i); }
                }
                float a1 = R(v1, 0f, out _), a2 = R(v2, 0f, out float k2), b1 = R(v1, 45f, out _), b2 = R(v2, 45f, out float k2b);
                float f2 = Mathf.Clamp(Mathf.Max(a2, k2), 1.5f, 3f), g2 = Mathf.Clamp(Mathf.Max(b2, k2b), 1.5f, 3f);
                Check(f2 >= k2 - 1e-4f && g2 >= k2b - 1e-4f && f2 - a1 >= -0.1f && g2 - b1 >= -0.1f && f2 - a1 <= 0.1f + GunV2Seat.SpacingGiveM && g2 - b1 <= 0.1f + GunV2Seat.SpacingGiveM, $"spacing radius (UnitView, view only): v1 facing north {a1:0.00} m, turned 45 deg {b1:0.00} m (as before); v2 {f2:0.00} / {g2:0.00} m (its own {a2:0.00} / {b2:0.00}, at least its turret's sweep {k2 + OzGun.SweepKeepClear:0.00} m from its place less {OzGun.SweepKeepClear} = {k2:0.00} / {k2b:0.00}: robots are never drawn inside the turret or its seated gunner; from 0.1 m under v1's to 0.1 + {GunV2Seat.SpacingGiveM:0.0} m over it: its gunner's seat reaches further back; the map's room is the rule's)");   // k93: v2 has no chair since k92 (the old rule wanted v2 >= v1, which the chair once gave)
            }
            {   // the gunner's seat: v1's height, behind the red block; the label anchor over the top
                var s1 = N(v1, "CannonGunnerSeat"); var s2 = N(v2, "CannonGunnerSeat"); var la = N(v2, "LabelAnchor.003");
                var tb = N(v2, "CannonTurretYaw_Surface").GetComponent<MeshRenderer>().bounds;
                Check(s1 && s2 && (s2.localPosition - GunV2Seat.SeatYawLocal).magnitude < 0.01f && la && la.position.y > tb.max.y,
                      $"CannonGunnerSeat {L(v2, s2).ToString("F2")} (v1 {L(v1, s1).ToString("F2")}: v2's gunner sits behind the box like v1's behind its housing, nothing of the gun within 2 cm of him); LabelAnchor {la.position.y:0.00} m over the top {tb.max.y:0.00} m");
            }
            // blinkers: v2's own data entry, every node it mounts on in v2
            {
                var ta = Resources.Load<TextAsset>("OpenZ/UnitBlinkers/unit_blinkers"); string txt = ta ? ta.text : "";
                int at = txt.IndexOf("\"" + U.LightsKey + "\"", System.StringComparison.Ordinal);
                Check(at >= 0 && N(v2, "CannonTurretYaw") && N(v2, "CannonPedestal"), $"UnitBlinkers: entry '{U.LightsKey}' {(at >= 0 ? "present" : "MISSING")} (v2's own sockets: beacon, 3 status LEDs, the team neons on the tag stripes, the base power LED)");
            }
            // pictures (lesson 3) and the switch
            foreach (var c in new[] { "red", "blue" })
            {
                foreach (var (k, v1Path) in new[] { ("icon", "OpenZ/UI/Icons/icon_gun_"), ("portrait", "OpenZ/UI/PortraitsVehicles/portrait_gun_") })
                {
                    var t = Resources.Load<Texture2D>(OzGun.Dir + k + "_gun_v2_" + c); var t1p = Resources.Load<Texture2D>(v1Path + c);
                    Check(t && t1p && t.width == t1p.width && t.height == t1p.height, $"{k} v2 {c}: {(t ? t.width + "x" + t.height : "MISSING")} (v1 {(t1p ? t1p.width + "x" + t1p.height : "-")})");
                }
                for (int tier = 1; tier <= 4; tier++)
                {
                    var f2 = Resources.Load<Texture2D>(OzBadgeArt.Root + tier + "/face_gun_v2_" + c); var f1 = Resources.Load<Texture2D>(OzBadgeArt.Root + tier + "/face_gun_" + c);
                    Check(f2 && f1 && f2.width == f1.width, $"badge face x{tier} v2 {c}: {(f2 ? f2.width + " px" : "MISSING")} (v1 {(f1 ? f1.width + " px" : "-")})");
                }
            }
            U.Forget();
            Check(OzGun.Version == 2 ? U.Drawn == 2 : U.Drawn == 1, $"the switch OzGun.Version = {OzGun.Version}: drawn model v{U.Drawn}");
            var icon = UnitArt.Icon(OzGun.Type, Team.Red);
            Check(OzGun.Version != 2 || (U.PrefabFor(OzGun.V1Key, v1) == U.PrefabV2 && icon && icon.name.Contains("v2") && OzBadgeArt.FaceFor(OzGun.Type, Team.Red).Contains("gun_v2")),
                  $"with the switch on v2: Z_gun -> Z_gun_v2 (build menu, placement ghost, guns placed in a level, tower guns), the production picture {(icon ? icon.name : "none")}, the badge face {OzBadgeArt.FaceFor(OzGun.Type, Team.Red)}");
            var tv1 = OzV2Units.TileArt(OzGun.TileV1, Team.Red); var tv2 = OzV2Units.TileArt(OzGun.TileV2, Team.Red);
            var gunsF = typeof(OzCheatSpawner).GetField("Guns", BindingFlags.NonPublic | BindingFlags.Static);
            var guns = gunsF != null ? gunsF.GetValue(null) as string[] : null;
            Check(tv1 && tv2 && tv1 != tv2 && !tv1.name.Contains("v2") && guns != null && guns.Contains(OzGun.TileV1) && guns.Contains(OzGun.TileV2) && !guns.Contains(OzGun.Type),
                  $"testing panel GUNS: {(guns != null ? string.Join(", ", guns) : "no Guns list")}; {OzV2Units.TileLabel(OzGun.TileV1)} = {(tv1 ? tv1.name : "none")}, {OzV2Units.TileLabel(OzGun.TileV2)} = {(tv2 ? tv2.name : "none")}");
            Write(lines, pass, fail);
        }

        static void Write(List<string> lines, int pass, int fail)
        {
            string head = $"[OzGunV2] parity check {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail} checks";
            Directory.CreateDirectory(ProofDir);
            File.WriteAllText(Path.Combine(ProofDir, "parity_check.txt"), head + "\n" + string.Join("\n", lines) + "\n");
            Debug.Log(head + "\n" + string.Join("\n", lines));
        }

        // ---------------------------------------------------------------- 3 Play proof, 5 .. 15 films and tests
        [MenuItem(M + "3 Play proof (L02 Desert, side by side)", false, 3)] static void ProofL02() => Proof("L02", L02);
        [MenuItem(M + "3 Play proof (L17 Car Park, side by side)", false, 4)] static void ProofL17() => Proof("L17", L17);
        static bool Ready()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[OzGunV2] stop Play first"); return false; }
            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }
        static void Proof(string job, string scene)
        {
            if (!Ready()) return;
            PlayerPrefs.SetString(GunV2Proof.AutoPref, job); PlayerPrefs.Save();
            EditorSceneManager.OpenScene(scene);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(M + "5 Test + film: v1 and v2 side by side, the turret turns and kicks at each shot, the base never moves (L02)", false, 21)] static void Side() => Film("gun_side");
        [MenuItem(M + "6 Film: a robot mans an empty v2 (L02)", false, 22)] static void Board() => Film("gun_board");
        [MenuItem(M + "6 Film: a robot mans an empty v2, close camera 32 m (L02)", false, 22)] static void BoardClose() => Film("gun_board_close");
        [MenuItem(M + "7 Film: one v2 fights to its death (L02)", false, 23)] static void Fight() => Film("gun_fight");
        [MenuItem(M + "11 Test: the turret never shakes between targets (L02)", false, 50)] static void Turret() => Film("gun_turret");
        [MenuItem(M + "15 Film: the turret and its kick close at RTS distance (v2, 32 m, L02)", false, 54)] static void Close() => Film("gun_close");
        static void Film(string job)
        {
            if (!Ready()) return;
            PlayerPrefs.SetString(HeavyV2Films.SubjectPref, OzGun.Type);
            PlayerPrefs.SetString(HeavyV2Films.AutoPref, job); PlayerPrefs.Save();
            EditorSceneManager.OpenScene(L02);
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
