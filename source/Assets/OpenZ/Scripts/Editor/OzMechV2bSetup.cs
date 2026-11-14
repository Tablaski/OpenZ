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
    /// Mech v2 = the v1 model with its own texture (View/OzMech.cs), the editor side. Menus 3 .. 19 stay in Editor/OzMechV2Setup.cs and the
    /// hand-gun stages.
    ///  1 Build: Resources/OpenZ/MechV2/Z_medium_v2 = a copy of Prefabs/Z_medium (every node, mesh, rig, part, wreck piece, component and
    ///    setting of v1), v1's toy material swapped for Materials/Units/mech_v2b_toy on every slot that has it, plus the empty marker node
    ///    OzMech.FrameNode as the root's last child. Nothing else; v1 is not touched.
    ///  2 Parity check (counted, edit mode): the two prefabs node for node (names, parents, rests, layers, components), the same meshes, the
    ///    same materials but the toy one, the toy material = v1's but its picture, the team texels of the picture = v1's, the marker, the
    ///    hatch (built as v1's, textured on v2), the hand gun (v1's fallback points on both), the wreck pieces, the lamp / blinker entries,
    ///    the recoil sizes, the pictures, the switch and the testing panel's tiles.
    /// </summary>
    public static partial class OzMechV2bSetup
    {
        public const string V1Prefab = OzMechV2Setup.V1Prefab, PrefabPath = OzMechV2Setup.PrefabPath, Mats = OzMechV2Setup.Mats;
        public const string V1Toy = Mats + "mech_DV3_VehiclePBR_Masked_mech.mat", V2Toy = Mats + "mech_v2b_toy.mat";
        public const string V1Picture = "Assets/OpenZ/Models/Legacy/Toy/mech_toy.png", V2Picture = "Assets/OpenZ/Models/Legacy/Toy/mech_v2b_toy.png";
        const string M = "OpenZ/Mech v2/";
        static readonly string ProofDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/v2_units/mech/proof"));
        static OzUnitModels U => OzMech.Models;

        static Transform Deep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Deep(c, name); if (f) return f; }
            return null;
        }

        // ---------------------------------------------------------------- 1 build
        [MenuItem(M + "1 Build the v2 prefab (the v1 model + the v2 texture)", false, 1)]
        public static void Build()
        {
            string r = BuildNow();
            Debug.Log("[OzMechV2b] " + r);
            Directory.CreateDirectory(ProofDir);
            File.WriteAllText(Path.Combine(ProofDir, "build_report.txt"), r + "\n");
        }

        public static string BuildNow()
        {
            var v1 = AssetDatabase.LoadAssetAtPath<GameObject>(V1Prefab);
            var toy1 = AssetDatabase.LoadAssetAtPath<Material>(V1Toy); var toy2 = AssetDatabase.LoadAssetAtPath<Material>(V2Toy);
            if (!v1) return "FAILED: no " + V1Prefab;
            if (!toy1 || !toy2) return "FAILED: no " + (!toy1 ? V1Toy : V2Toy);
            var inst = Object.Instantiate(v1);
            try
            {
                inst.name = OzMech.V2Key;
                int slots = 0, renderers = 0, housings = 0;
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    var ms = r.sharedMaterials; bool changed = false;
                    for (int i = 0; i < ms.Length; i++) if (ms[i] == toy1) { ms[i] = toy2; changed = true; slots++; }
                                                        else if (HousingSwap(r, ms, i)) { changed = true; housings++; }   // the worn shoulder housings (OzMechV2bSetup.Wear.cs)
                    if (changed) { r.sharedMaterials = ms; renderers++; }
                }
                if (!Deep(inst.transform, OzMech.FrameNode))
                {
                    var mk = new GameObject(OzMech.FrameNode) { layer = inst.layer };
                    mk.transform.SetParent(inst.transform, false);
                }
                if (!AssetDatabase.IsValidFolder(OzMechV2Setup.Folder)) AssetDatabase.CreateFolder("Assets/OpenZ/Resources/OpenZ", "MechV2");
                PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath, out bool ok);
                if (!ok) return "FAILED: the prefab could not be saved at " + PrefabPath;
                AssetDatabase.SaveAssets();
                U.Forget();
                return $"prefab: a copy of {V1Prefab}, {slots} material slot(s) on {renderers} renderer(s) {toy1.name} -> {toy2.name}, {housings} housing slot(s) -> mech_v2b_housing, marker {OzMech.FrameNode}; saved {PrefabPath}";
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

        static Dictionary<string, Transform> Tree(GameObject g, string skip)
        {
            var d = new Dictionary<string, Transform>();
            foreach (var t in g.GetComponentsInChildren<Transform>(true)) if (t != g.transform && t.name != skip) d[PathOf(t, g.transform)] = t;
            return d;
        }

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

        [MenuItem(M + "2 Parity check (counted, no Play)", false, 2)]
        public static void Parity()
        {
            var lines = new List<string>(); int pass = 0, fail = 0;
            void Check(bool ok, string what) { lines.Add((ok ? "PASS " : "FAIL ") + what); if (ok) pass++; else fail++; }
            var v1 = AssetDatabase.LoadAssetAtPath<GameObject>(V1Prefab);
            var v2 = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var toy1 = AssetDatabase.LoadAssetAtPath<Material>(V1Toy); var toy2 = AssetDatabase.LoadAssetAtPath<Material>(V2Toy);
            Check(v1 && v2 && toy1 && toy2, $"prefabs v1 {(v1 ? "found" : "MISSING")}, v2 {(v2 ? "found" : "MISSING")}; materials {(toy1 ? toy1.name : "MISSING")}, {(toy2 ? toy2.name : "MISSING")}");
            if (!v1 || !v2 || !toy1 || !toy2) { Write(lines, pass, fail); return; }

            // the node tree: every v1 node in v2 at the same place (path with sibling indices), same rest, layer, active state and components
            var t1 = Tree(v1, null); var t2 = Tree(v2, OzMech.FrameNode);
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
            Check(miss.Count == 0 && extra.Count == 0 && diff.Count == 0, $"the node tree: {t1.Count} nodes of v1 and their {comps} components in v2 at the same places, rests, layers{(miss.Count + extra.Count + diff.Count > 0 ? "; missing " + string.Join(", ", miss.Take(8)) + "; extra " + string.Join(", ", extra.Take(8)) + "; different " + string.Join(", ", diff.Take(8)) : "")}");
            var r1 = v1.transform.GetComponents<Component>().Select(c => c.GetType().Name); var r2 = v2.transform.GetComponents<Component>().Select(c => c.GetType().Name);
            Check(r1.SequenceEqual(r2) && v1.layer == v2.layer, $"the root: components {string.Join("+", r2)} (v1 {string.Join("+", r1)}), layer {v2.layer} (v1 {v1.layer}), name {v2.name}");
            // meshes and materials
            int meshes = 0, meshDiff = 0, slotsSwapped = 0, slotsSame = 0, slotsOther = 0, v1ToyLeft = 0, housingSwapped = 0;
            foreach (var kv in t1)
            {
                if (!t2.TryGetValue(kv.Key, out var b)) continue;
                var fa = kv.Value.GetComponent<MeshFilter>(); var fb = b.GetComponent<MeshFilter>();
                if (fa || fb) { meshes++; if (!fa || !fb || fa.sharedMesh != fb.sharedMesh) meshDiff++; }
                var ra = kv.Value.GetComponent<Renderer>(); var rb = b.GetComponent<Renderer>();
                if (!ra || !rb) continue;
                var ma = ra.sharedMaterials; var mb = rb.sharedMaterials;
                if (ma.Length != mb.Length) { slotsOther += Mathf.Max(ma.Length, mb.Length); continue; }
                for (int i = 0; i < ma.Length; i++)
                {
                    if (mb[i] == toy1) v1ToyLeft++;
                    if (ma[i] == toy1 && mb[i] == toy2) slotsSwapped++;
                    else if (HousingPair(rb, ma[i], mb[i])) housingSwapped++;   // the worn shoulder housings (OzMechV2bSetup.Wear.cs)
                    else if (ma[i] == mb[i]) slotsSame++;
                    else slotsOther++;
                }
                if (ra.shadowCastingMode != rb.shadowCastingMode || ra.receiveShadows != rb.receiveShadows) slotsOther++;
            }
            Check(meshDiff == 0 && meshes > 0, $"meshes: {meshes} mesh filters, the same mesh as v1 on every one ({meshDiff} different): the v1 model exactly (wreck pieces included)");
            Check(slotsSwapped > 0 && slotsOther == 0 && v1ToyLeft == 0, $"materials: {slotsSwapped} slot(s) {toy1.name} -> {toy2.name}, {slotsSame} slot(s) v1's own, {slotsOther} other difference(s), {v1ToyLeft} slot(s) still {toy1.name}; shadows as v1");
            Check(HousingCheck(housingSwapped, out string housingWhat), housingWhat);
            {   // the toy material: v1's settings, its picture only
                var diffs = new List<string>(); var sh = toy1.shader;
                if (toy2.shader != sh) diffs.Add("shader");
                for (int i = 0; sh && i < sh.GetPropertyCount(); i++)
                {
                    string n = sh.GetPropertyName(i); var ty = sh.GetPropertyType(i);
                    if (ty == UnityEngine.Rendering.ShaderPropertyType.Float || ty == UnityEngine.Rendering.ShaderPropertyType.Range) { if (!Mathf.Approximately(toy1.GetFloat(n), toy2.GetFloat(n))) diffs.Add(n); }
                    else if (ty == UnityEngine.Rendering.ShaderPropertyType.Color || ty == UnityEngine.Rendering.ShaderPropertyType.Vector) { if (toy1.GetVector(n) != toy2.GetVector(n)) diffs.Add(n); }
                    else if (ty == UnityEngine.Rendering.ShaderPropertyType.Texture && n != "_BaseMap") { if (toy1.GetTexture(n) != toy2.GetTexture(n)) diffs.Add(n); }
                }
                if (!toy1.shaderKeywords.OrderBy(x => x).SequenceEqual(toy2.shaderKeywords.OrderBy(x => x))) diffs.Add("keywords");
                var tex1 = toy1.GetTexture("_BaseMap"); var tex2 = toy2.GetTexture("_BaseMap");
                bool pic = tex2 && AssetDatabase.GetAssetPath(tex2) == V2Picture && tex1 && tex1.width == tex2.width && tex1.height == tex2.height;
                Check(diffs.Count == 0 && pic, $"{toy2.name}: v1's {toy1.name} in every setting ({(diffs.Count > 0 ? "DIFFERENT: " + string.Join(", ", diffs) : "0 differences")}), its picture {(tex2 ? AssetDatabase.GetAssetPath(tex2) : "none")} {(tex2 ? tex2.width + "x" + tex2.height : "")} (v1 {(tex1 ? tex1.width + "x" + tex1.height : "-")})");
            }
            {   // the team texels (alpha 1: TeamLit's toy path paints them in the team colour) = v1's, texel for texel; the paint / metal split
                int n1 = 0, n2 = 0, mism = 0, total = 0, paint1 = 0, paint2 = 0;
                var a = new Texture2D(2, 2, TextureFormat.RGBA32, false); var b = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (a.LoadImage(File.ReadAllBytes(V1Picture)) && b.LoadImage(File.ReadAllBytes(V2Picture)) && a.width == b.width && a.height == b.height)
                    {
                        var pa = a.GetPixels32(); var pb = b.GetPixels32(); total = pa.Length;
                        for (int i = 0; i < pa.Length; i++)
                        {
                            bool ta = pa[i].a >= 224, tb = pb[i].a >= 224; if (ta) n1++; if (tb) n2++; if (ta != tb) mism++;
                            if (pa[i].a > 64 && pa[i].a < 192) paint1++; if (pb[i].a > 64 && pb[i].a < 192) paint2++;
                        }
                    }
                }
                finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
                Check(total > 0 && mism == 0 && n1 == n2 && n1 > 0, $"team texels: {n2} in the v2 picture, {n1} in v1's, {mism} placed differently (the team colour exactly where v1 has it: the top plate; the shoulder housings wear their own team picture, mech_v2b_housing); paint texels {paint2} (v1 {paint1}: the claw arm and a few edge chips are metal now)");
            }
            {   // the marker: only on v2, empty, the root's last child; IsV2 tells the two apart; RoundHatch builds v1's hatch on both
                var m2 = v2.GetComponentsInChildren<Transform>(true).Where(t => t.name == OzMech.FrameNode).ToList(); var m1 = v1.GetComponentsInChildren<Transform>(true).Count(t => t.name == OzMech.FrameNode);
                bool ok = m2.Count == 1 && m1 == 0 && m2[0].parent == v2.transform && m2[0].GetSiblingIndex() == v2.transform.childCount - 1 && m2[0].GetComponents<Component>().Length == 1 && m2[0].childCount == 0;
                Check(ok && U.IsV2(v2.transform) && !U.IsV2(v1.transform), $"the v2 marker {OzMech.FrameNode}: {m2.Count} on v2 (empty, the root's last child), {m1} on v1; IsV2 v2 {U.IsV2(v2.transform)}, v1 {U.IsV2(v1.transform)}");
                Check(OzV2Units.FrameNodeFor("mech_") == null, $"the round hatch: no modelled v2 hatch (OzV2Units.FrameNodeFor(\"mech_\") = {OzV2Units.FrameNodeFor("mech_") ?? "none"}): RoundHatch builds v1's round hatch on both models");
                var lt = Resources.Load<Texture2D>(OzMech.Dir + RoundHatch.LidPicture); var pt = Resources.Load<Texture2D>(OzMech.Dir + RoundHatch.PlatePicture);
                Check(lt && pt, $"the v2 hatch pictures: {RoundHatch.LidPicture} {(lt ? lt.width + "x" + lt.height : "MISSING")}, {RoundHatch.PlatePicture} {(pt ? pt.width + "x" + pt.height : "MISSING")} (Resources/{OzMech.Dir}; View/RoundHatch.MechV2.cs puts them on the built lid, plate and collar of a v2 only)");
            }
            {   // the hand gun: v1's fallback points on both (its muzzle node MechMuzzle under MechGunRecoil); k86f's gun loss reads MechGunRecoil
                bool own = Deep(v2.transform, MechHandGunView.MuzzleNode) || Deep(v2.transform, MechHandGunView.EjectNode) || Deep(v1.transform, MechHandGunView.MuzzleNode);
                var rc2 = Deep(v2.transform, "MechGunRecoil"); var mz2 = Deep(v2.transform, "MechMuzzle");
                Check(!own && rc2 && mz2 && mz2.IsChildOf(rc2), $"the hand gun: no {MechHandGunView.MuzzleNode} / {MechHandGunView.EjectNode} on either model (both fire from v1's points: MechMuzzle under MechGunRecoil, the casing port beside it), MechGunRecoil {(rc2 ? "found" : "MISSING")}");
            }
            {   // wreck pieces: v1's own holders and fragments
                int h1 = v1.GetComponentsInChildren<Transform>(true).Count(t => t.name == "OZ_WreckFragments"), h2 = v2.GetComponentsInChildren<Transform>(true).Count(t => t.name == "OZ_WreckFragments");
                int b1 = v1.GetComponentsInChildren<Transform>(true).Count(t => t.name == "OZ_WreckFragments" && WreckPieces.IsBaked(t)), b2 = v2.GetComponentsInChildren<Transform>(true).Count(t => t.name == "OZ_WreckFragments" && WreckPieces.IsBaked(t));
                Check(h2 == h1 && b2 == b1 && h2 > 0, $"wreck pieces: {h2} holders, {b2} baked (v1 {h1} / {b1}), the same fragment meshes as v1 (the mesh line above)");
            }
            // lamps and blinkers: the v2 entries = v1's (the same model: the same sockets)
            foreach (var (file, label) in new[] { ("OpenZ/VehicleLights/vehicle_lights", "VehicleLights"), ("OpenZ/UnitBlinkers/unit_blinkers", "UnitBlinkers") })
            {
                var ta = Resources.Load<TextAsset>(file); string txt = ta ? ta.text.Replace("\r\n", "\n") : "";
                string e1 = Entry(txt, OzMech.Type), e2 = Entry(txt, U.LightsKey);
                bool eyesOnly = label == "VehicleLights" && e1 != null && e2 != null && e1 != e2 && OzMechV2EyesCheck.SameButEyes(e1, OzMechV2EyeLightsCheck.BeamsAsV1(e1, e2));   // k86i: the v2's two eye lamps sit in its glowing eyes; k86j: its eye beams have the jeep's strength
                Check(e1 != null && e2 != null && (e1 == e2 || eyesOnly), $"{label}: entry '{U.LightsKey}' {(e2 != null ? "present" : "MISSING")}, {(e1 != null && e1 == e2 ? "the same sockets as v1's '" + OzMech.Type + "'" : eyesOnly ? "v1's sockets but the two eye lamps, " + OzMechV2Eyes.SocketDrop.ToString("0.000") + " lower (in the glowing eyes)" + OzMechV2EyeLightsCheck.Note(e2) : "NOT v1's sockets")}");   // k86j: + the eye beams
            }
            { var eyes = OzMechV2EyesCheck.Parity(v2, toy2); Check(eyes.ok, eyes.what); }   // k86i: the glowing eyes (pictures, no material emission, the visor slot)
            Check(Mathf.Approximately(HeavyRecoil.DesignHeadSlide(OzMech.Type, 1), HeavyRecoil.DesignHeadSlide(OzMech.Type, 2)) && HeavyRecoil.Types.Contains(OzMech.Type),
                  $"the kick: the upper body slides {HeavyRecoil.DesignHeadSlide(OzMech.Type, 2):0.000} on v2 = {HeavyRecoil.DesignHeadSlide(OzMech.Type, 1):0.000} on v1 (HeavyRecoil)");
            // pictures, the switch, the testing panel's tiles (as the first v2's parity)
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
            var nt = Resources.Load<Texture2D>("OpenZ/UI/Neutral/neutral_medium_v2"); var nt1 = Resources.Load<Texture2D>("OpenZ/UI/Neutral/neutral_medium_v1");
            Check(nt && nt1 && nt.width == nt1.width, $"the empty tile neutral_medium_v2: {(nt ? nt.width + "x" + nt.height : "MISSING")} (v1's {(nt1 ? nt1.width + "x" + nt1.height : "-")})");
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
            string head = $"[OzMechV2b] parity check {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail} checks (the v2 = the v1 model with its own texture)";
            Directory.CreateDirectory(ProofDir);
            File.WriteAllText(Path.Combine(ProofDir, "parity_check.txt"), head + "\n" + string.Join("\n", lines) + "\n");
            Debug.Log(head + "\n" + string.Join("\n", lines));
        }
    }
}
#endif
