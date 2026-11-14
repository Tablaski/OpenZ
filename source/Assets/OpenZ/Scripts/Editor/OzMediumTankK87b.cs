#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// k87b, the medium tank after its Play checks, editor side (menu OpenZ/Medium Tank/1 build, 2 parity check, 16 and 17 pictures):
    ///  * SIZE : the medium's Model node at OzMediumTank.ModelScale; the parity measures
    ///    the three drawn tanks from the game's RTS camera with k84b's count (OzLightV2K84b.Silhouette: pitch 55, 24 headings, every drawn
    ///    renderer) and wants the medium within 5 % of the middle of the light tank v2 (at OzLightTank.V2Size) and the heavy tank v2
    ///    (off the editor: light 12.48, heavy 19.39, middle 15.93 m2; the medium 15.94 m2 at 1.087).
    ///  * HATCH WELL: the medium's well is modelled in its dome (its own material slot mtank_v2_well: dark, never team paint, a box under the
    ///    hatch frame) and tank_CabinFloor is only the seat point there: it stands for the light tank v1's drawn recess box (k84b's rule).
    ///  * STEADY NODES: TankDriverSeat and TankLabelAnchor were on the suspension (they rocked with the hull); the light tank keeps them on the
    ///    Model node: menu 1 moves them there (their world place kept). Checked: the crew in the dome hatch sits on tank_Board_Seat
    ///    (BoardingView.SeatPose, the Roof mode) and the hatch commander on the modelled hatch (RoundHatch); TankDriverSeat is only the fallback
    ///    when a vehicle has neither, TankLabelAnchor is read by no code: the hatch pose does not change.
    ///  * 16 Picture: the blue unit card (in Play on L02 as PLAY AS BLUE): a blue MEDIUM TANK dropped through the testing panel's own Spawn at
    ///    k84b's open tile (LightV2Spots "card/blue": k87's drop beside the blue factory found no room), selected, framed at 35 m, captured.
    ///  * 17 Picture: a close fire shot with the target in reach (View/MTankStage.cs: MTankCloseFire).
    /// </summary>
    public static class OzMediumTankK87b
    {
        const string M = "OpenZ/Medium Tank/";
        static readonly string ProofDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/v2_units/medium/proof"));
        const string LightV2Path = "Assets/OpenZ/Resources/OpenZ/LightV2/Z_light_v2.prefab", LightV1Path = "Assets/OpenZ/Prefabs/Z_light.prefab",
                     HeavyV2Path = "Assets/OpenZ/Resources/OpenZ/HeavyV2/Z_heavy_v2.prefab";
        /// <summary>The largest distance of the medium's silhouette from the middle of the light and heavy tanks' (a share of the middle).</summary>
        public const float SizeTolerance = 0.05f;

        static Transform Deep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Deep(c, name); if (f) return f; }
            return null;
        }

        /// <summary>Menu 1: TankDriverSeat and TankLabelAnchor onto the Model node (their world place kept), as the light tank keeps them.</summary>
        public static string SteadyNodes(Transform model)
        {
            var moved = new List<string>();
            foreach (var nm in new[] { "TankDriverSeat", "TankLabelAnchor" })
            {
                var t = Deep(model, nm);
                if (t && t.parent != model) { string was = t.parent ? t.parent.name : "-"; t.SetParent(model, true); moved.Add($"{nm} ({was} -> Model)"); }
            }
            return moved.Count > 0 ? $"steady nodes (k87b): {string.Join(", ", moved)}; " : "steady nodes (k87b): already on Model; ";
        }

        /// <summary>The medium's hatch well: the Tank_turret slot with the mtank_v2_well material, dark and never team paint, under the hatch frame.</summary>
        public static bool HatchWell(GameObject mt, out string what)
        {
            what = "the hatch well (k87b): ";
            if (!mt) { what += "no medium tank prefab"; return false; }
            var turret = Deep(mt.transform, "Tank_turret"); var frame = Deep(mt.transform, OzMediumTank.FrameNode);
            var mr = turret ? turret.GetComponent<MeshRenderer>() : null; var mf = turret ? turret.GetComponent<MeshFilter>() : null;
            if (!mr || !mf || !mf.sharedMesh) { what += "no Tank_turret renderer"; return false; }
            int slot = -1; var mats = mr.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) if (mats[i] && mats[i].name == OzMediumTank.MatPrefix + "well") slot = i;
            if (slot < 0 || slot >= mf.sharedMesh.subMeshCount) { what += "no " + OzMediumTank.MatPrefix + "well slot on Tank_turret"; return false; }
            var mat = mats[slot];
            Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            bool team = mat.HasProperty("_TeamMode") && mat.GetFloat("_TeamMode") > 0.5f, tex = mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap");
            var b = mf.sharedMesh.GetSubMesh(slot).bounds;
            var top = turret.TransformPoint(b.center + Vector3.up * b.extents.y); var size = Vector3.Scale(b.size, turret.lossyScale);
            bool under = frame && Vector2.Distance(new Vector2(frame.position.x, frame.position.z), new Vector2(top.x, top.z)) < 0.3f;
            bool ok = lum <= 0.1f && !team && !tex && size.y > 0.1f && size.x > 0.3f && size.z > 0.3f && (under || !frame);
            what += $"the dome's Tank_turret slot {slot} '{mat.name}' (base colour luminance {lum:0.000}, {(team ? "TEAM PAINT" : "never team paint")}, {(tex ? "a picture" : "no picture")}), " +
                    $"a box {size.x:0.00} x {size.z:0.00} m, {size.y:0.00} m deep, {(frame ? (under ? "under the hatch frame" : "NOT under the hatch frame") : "no frame node")}: " +
                    "it stands for the light tank v1's drawn tank_CabinFloor recess box (the medium's tank_CabinFloor is the seat point only)";
            return ok;
        }

        /// <summary>The parity's component compare: the light tank v1's tank_CabinFloor draws a recess box (MeshFilter + MeshRenderer); the
        /// medium's modelled well stands in.</summary>
        public static bool WellStandsIn(GameObject mt, string node, Component c) =>
            node == "tank_CabinFloor" && (c is MeshFilter || c is MeshRenderer) && HatchWell(mt, out _);

        /// <summary>Parity check line: the medium's silhouette from the RTS camera within SizeTolerance of the middle of the light tank v2 (at
        /// OzLightTank.V2Size: a v2 prefab built at another size is counted at V2Size) and the heavy tank v2.</summary>
        public static bool SizeCheck(GameObject mt, out string what)
        {
            var lt = AssetDatabase.LoadAssetAtPath<GameObject>(LightV2Path); var l1 = AssetDatabase.LoadAssetAtPath<GameObject>(LightV1Path);
            var hv = AssetDatabase.LoadAssetAtPath<GameObject>(HeavyV2Path);
            what = "size (k87b, item 139 'medium between light and heavy'): ";
            if (!mt || !lt || !hv || !l1) { what += $"prefabs: medium {(mt ? "found" : "MISSING")}, light v2 {(lt ? "found" : "MISSING")}, light v1 {(l1 ? "found" : "MISSING")}, heavy v2 {(hv ? "found" : "MISSING")}"; return false; }
            float am = OzLightV2K84b.Silhouette(mt, out int um, out int sm), al = OzLightV2K84b.Silhouette(lt, out int ul, out int sl), ah = OzLightV2K84b.Silhouette(hv, out int uh, out int sh);
            var lm = lt.transform.Find("Model"); var l1m = l1.transform.Find("Model");
            float built = lm && l1m ? lm.localScale.x / Mathf.Max(1e-4f, l1m.localScale.x) : OzLightTank.V2Size, k = OzLightTank.V2Size / Mathf.Max(1e-4f, built);
            float alc = al * k * k, mid = 0.5f * (alc + ah), off = mid > 1e-4f ? am / mid - 1f : 1f;
            var mm = mt.transform.Find("Model");
            bool ok = am > 1f && alc > 1f && ah > 1f && Mathf.Abs(off) <= SizeTolerance && sm + sl + sh == 0;
            what += $"from the RTS camera (pitch {OzLightV2K84b.Pitch:0}, 24 headings): light v2 {alc:0.00} m2 (at OzLightTank.V2Size {OzLightTank.V2Size:0.00}" +
                    $"{(Mathf.Abs(k - 1f) > 0.001f ? $"; its prefab is built at {built:0.000}: {al:0.00} m2 counted x {k * k:0.000}, menu OpenZ/Light Tank v2/1 again" : "")}), " +
                    $"heavy v2 {ah:0.00} m2, their middle {mid:0.00} m2; the medium {am:0.00} m2 ({um} renderers{(sm + sl + sh > 0 ? $", {sm + sl + sh} not readable" : "")}) at Model scale " +
                    $"{(mm ? mm.localScale.x : 0f):0.000} (OzMediumTank.ModelScale {OzMediumTank.ModelScale:0.000}) = {100f * off:+0.0;-0.0} % off the middle (within {100f * SizeTolerance:0} %; the rules' footprint unchanged)";
            return ok;
        }

        // ---------------------------------------------------------------- 16 the blue unit card
        [MenuItem(M + "16 Picture: the blue unit card (in Play: L02, PLAY AS BLUE)", false, 61)]
        public static void BlueCard()
        {
            if (!EditorApplication.isPlaying || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning("[OzMediumTank] start L02 in Play as blue first (PLAY AS BLUE)"); return; }
            var g = ZGame.I;
            if (!LightV2Spots.Get(g.level, "card", "blue", out int tx, out int ty)) { Debug.LogWarning($"[OzMediumTank] no blue card tile for {g.level} (View/LightV2Stage.cs: L02 only)"); return; }
            var sp = OzCheatSpawner.I;
            var spawn = typeof(OzCheatSpawner).GetMethod("Spawn", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string), typeof(Team), typeof(Vector2Int) }, null);
            if (!sp || spawn == null) { Debug.LogWarning("[OzMediumTank] the testing panel (OzCheatSpawner) is not in the scene"); return; }
            var before = new HashSet<ZObject>(g.World.Objects);
            try { spawn.Invoke(sp, new object[] { OzMediumTank.Type, Team.Blue, new Vector2Int(tx * 16 + 16, ty * 16 + 16) }); }
            catch (System.Exception e) { Debug.LogWarning("[OzMediumTank] the panel's drop failed (open the testing panel with ALT+P once, then again): " + e.GetBaseException().Message); return; }
            ZObject o = null;
            foreach (var x in g.World.Objects) if (x != null && !before.Contains(x) && x.Type == OzMediumTank.Type && x.Owner == Team.Blue) o = x;
            if (o == null) { Debug.LogWarning($"[OzMediumTank] no blue medium tank was dropped at tile ({tx},{ty}) (the panel said no room?)"); return; }
            var pi = Object.FindFirstObjectByType<PlayerInput>();
            if (pi) { pi.Selection.Clear(); pi.Selection.Add(o); }
            var cam = Object.FindFirstObjectByType<RtsCamera>();
            if (cam) { var p = ZGame.PxToWorld(o.CenterX, o.CenterY); cam.focus = new Vector3(p.x, 0f, p.z); cam.SetHeightImmediate(35f); }
            string dir = Path.Combine(ProofDir, "pictures_k87b"); Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "f_blue_unit_card.png");
            float at = (float)EditorApplication.timeSinceStartup + 1.5f;
            void Tick()
            {
                if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
                if (EditorApplication.timeSinceStartup < at) return;
                EditorApplication.update -= Tick;
                ScreenCapture.CaptureScreenshot(file);
                Debug.Log($"[OzMediumTank] blue unit card: blue medium tank #{o.Id} dropped at tile ({o.LocX >> 4},{o.LocY >> 4}) (asked: {tx},{ty}, k84b's open-tile scan), selected {(pi ? "yes" : "NO (no PlayerInput)")}; picture {file}");
            }
            EditorApplication.update += Tick;
        }

        // ---------------------------------------------------------------- 17 a close fire shot
        [MenuItem(M + "17 Picture: a close fire shot, the target in reach (in Play: L02)", false, 62)]
        public static void CloseFire()
        {
            if (!EditorApplication.isPlaying || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning("[OzMediumTank] start L02 in Play first"); return; }
            MTankCloseFire.Begin();
        }
    }
}
#endif
