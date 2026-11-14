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
    /// k84b, the light tank v2 follow-up, editor side (menu OpenZ/Light Tank v2/2 parity check lines and menu 15):
    ///  * SIZE: the drawn v1 and v2 seen from the game's RTS camera (View/RtsCamera.cs: pitch 55 deg, north up; 85 m off at the 70 m play
    ///    height, so the view is parallel to within 2 %): every drawn renderer of each prefab rasterised from 24 headings, the mean silhouette
    ///    area compared: v2 (OzLightTank.V2Size) must read the same size as v1, within 5 % (off the editor: 12.48 against 12.55 m2 at 1.21).
    ///  * HATCH WELL: v1 draws a dark recess box under its hatch (tank_CabinFloor's own mesh); v2's well is modelled in its turret mesh (its
    ///    own material slot light_tank_v2_well: dark, never team paint, a box under the hatch frame) and tank_CabinFloor is only the seat
    ///    point there. The parity counts the well in place of the box when it is that dark slot under the frame (the climb frames show it
    ///    dark with the lid open: k84b measured luma 31 in the opening against the roof's 80, no ground seen through it).
    ///  * 15 Picture: the blue unit card: in Play on L02 as PLAY AS BLUE, a blue LIGHT v2 is dropped through the testing panel's own Spawn at
    ///    the open tile of View/LightV2Stage.cs (LightV2Spots "card/blue", from the open-tile scan: k84's drop beside the blue factory found
    ///    no room), selected, framed at 35 m and captured to the proof folder (f_blue_unit_card.png).
    /// </summary>
    public static class OzLightV2K84b
    {
        const string M = "OpenZ/Light Tank v2/";
        static readonly string ProofDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_2_20261022/v2_units/light/proof"));
        /// <summary>The RTS camera's pitch (deg, View/RtsCamera.cs) and the raster's cell (m).</summary>
        public const float Pitch = 55f, Cell = 0.04f;
        /// <summary>The largest silhouette difference v2 / v1 that still reads the same size.</summary>
        public const float SizeTolerance = 0.05f;

        static bool Under(Transform t, string name) { for (var p = t; p; p = p.parent) if (p.name == name) return true; return false; }
        static bool Active(Transform t, Transform root) { for (var p = t; p; p = p.parent) { if (!p.gameObject.activeSelf) return false; if (p == root) break; } return true; }

        /// <summary>The mean silhouette area (m2) of a prefab's drawn renderers from the RTS camera over 24 headings; the renderers counted and
        /// those skipped (mesh not readable).</summary>
        public static float Silhouette(GameObject prefab, out int used, out int skipped)
        {
            used = skipped = 0;
            var tris = new List<Vector3>();
            var root = prefab.transform; var toRoot = root.worldToLocalMatrix;
            foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.enabled || !Active(r.transform, root) || Under(r.transform, "OZ_WreckFragments")) continue;
                var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                var mesh = mf.sharedMesh; if (!mesh.isReadable) { skipped++; continue; }
                var m = toRoot * r.transform.localToWorldMatrix; var v = mesh.vertices; var ix = mesh.triangles;
                for (int i = 0; i < ix.Length; i++) tris.Add(m.MultiplyPoint3x4(v[ix[i]]));
                used++;
            }
            if (tris.Count == 0) return 0f;
            int n = Mathf.CeilToInt(14f / Cell); var grid = new bool[n * n]; float half = n * Cell * 0.5f, sum = 0f;
            float cp = Mathf.Cos(Pitch * Mathf.Deg2Rad), sp = Mathf.Sin(Pitch * Mathf.Deg2Rad);
            var s = new Vector2[tris.Count];
            for (int hd = 0; hd < 360; hd += 15)
            {
                System.Array.Clear(grid, 0, grid.Length);
                var q = Quaternion.Euler(0f, hd, 0f);
                for (int i = 0; i < tris.Count; i++) { var p = q * tris[i]; s[i] = new Vector2(p.x, p.y * cp + p.z * sp); }
                for (int t = 0; t + 2 < s.Length; t += 3)
                {
                    Vector2 a = s[t], b = s[t + 1], c = s[t + 2];
                    float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x); if (Mathf.Abs(area) < 1e-9f) continue;
                    int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) + half) / Cell)), x1 = Mathf.Min(n - 1, Mathf.CeilToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + half) / Cell));
                    int y0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, Mathf.Min(b.y, c.y)) + half) / Cell)), y1 = Mathf.Min(n - 1, Mathf.CeilToInt((Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + half) / Cell));
                    float sg = Mathf.Sign(area);
                    for (int yy = y0; yy <= y1; yy++)
                        for (int xx = x0; xx <= x1; xx++)
                        {
                            float px = (xx + 0.5f) * Cell - half, py = (yy + 0.5f) * Cell - half;
                            float w0 = ((b.x - a.x) * (py - a.y) - (b.y - a.y) * (px - a.x)) * sg, w1 = ((c.x - b.x) * (py - b.y) - (c.y - b.y) * (px - b.x)) * sg,
                                  w2 = ((a.x - c.x) * (py - c.y) - (a.y - c.y) * (px - c.x)) * sg;
                            if (w0 >= 0f && w1 >= 0f && w2 >= 0f) grid[yy * n + xx] = true;
                        }
                }
                int on = 0; foreach (bool g in grid) if (g) on++;
                sum += on * Cell * Cell;
            }
            return sum / 24f;
        }

        /// <summary>Parity check line: v2 reads the same size as v1 from the RTS camera (silhouette within SizeTolerance).</summary>
        public static bool SizeCheck(GameObject v1, GameObject v2, out string what)
        {
            float a1 = Silhouette(v1, out int u1, out int s1), a2 = Silhouette(v2, out int u2, out int s2);
            float k = a1 > 1e-4f ? a2 / a1 : 0f;
            bool ok = a1 > 1f && a2 > 1f && Mathf.Abs(k - 1f) <= SizeTolerance && s2 == 0;
            what = $"size (k84b, item 135 'remains same size'): from the RTS camera (pitch {Pitch:0}, 24 headings) v1 {a1:0.00} m2 ({u1} renderers{(s1 > 0 ? $", {s1} not readable" : "")}), " +
                   $"v2 {a2:0.00} m2 ({u2} renderers{(s2 > 0 ? $", {s2} not readable" : "")}) = {100f * k:0.0} % of v1 at OzLightTank.V2Size {OzLightTank.V2Size:0.00} (within {100f * SizeTolerance:0} %; " +
                   "the rules' footprint unchanged)";
            return ok;
        }

        /// <summary>v2's hatch well: the Tank_turret slot with the light_tank_v2_well material, dark and never team paint, under the hatch frame.</summary>
        public static bool HatchWell(GameObject v2, out string what)
        {
            what = "the hatch well: ";
            if (!v2) { what += "no v2 prefab"; return false; }
            Transform turret = null, frame = null;
            foreach (var t in v2.GetComponentsInChildren<Transform>(true)) { if (t.name == "Tank_turret") turret = t; if (t.name == OzLightTank.FrameNode) frame = t; }
            var mr = turret ? turret.GetComponent<MeshRenderer>() : null; var mf = turret ? turret.GetComponent<MeshFilter>() : null;
            if (!mr || !mf || !mf.sharedMesh) { what += "no Tank_turret renderer"; return false; }
            int slot = -1; var mats = mr.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) if (mats[i] && mats[i].name == "light_tank_v2_well") slot = i;
            if (slot < 0 || slot >= mf.sharedMesh.subMeshCount) { what += "no light_tank_v2_well slot on Tank_turret"; return false; }
            var mat = mats[slot];
            Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            bool team = mat.HasProperty("_TeamMode") && mat.GetFloat("_TeamMode") > 0.5f;
            bool tex = mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap");
            var b = mf.sharedMesh.GetSubMesh(slot).bounds;
            var top = turret.TransformPoint(b.center + Vector3.up * b.extents.y); var bottom = turret.TransformPoint(b.center - Vector3.up * b.extents.y);
            var size = Vector3.Scale(b.size, turret.lossyScale);
            bool under = frame && Vector2.Distance(new Vector2(frame.position.x, frame.position.z), new Vector2(top.x, top.z)) < 0.25f;
            bool ok = lum <= 0.1f && !team && !tex && size.y > 0.1f && size.x > 0.3f && size.z > 0.3f && (under || !frame);
            what += $"v2's Tank_turret slot {slot} '{mat.name}' (base colour luminance {lum:0.000}, {(team ? "TEAM PAINT" : "never team paint")}, {(tex ? "a picture" : "no picture")}), " +
                    $"a box {size.x:0.00} x {size.z:0.00} m, {size.y:0.00} m deep, {(frame ? (under ? "under the hatch frame" : "NOT under the hatch frame") : "no frame node")} " +
                    $"(its floor {top.y - bottom.y:0.00} m below its rim): it stands for v1's drawn tank_CabinFloor recess box (v2's tank_CabinFloor is the seat point only); " +
                    "on the climb frames the open hatch shows it dark (k84b: luma 31 in the opening against the roof's 80, no ground through it)";
            return ok;
        }

        /// <summary>The parity's component compare: v1's tank_CabinFloor draws a recess box (MeshFilter + MeshRenderer); v2's well stands in.</summary>
        public static bool WellStandsIn(GameObject v2, string node, Component c) =>
            node == "tank_CabinFloor" && (c is MeshFilter || c is MeshRenderer) && HatchWell(v2, out _);

        // ---------------------------------------------------------------- 15 the blue unit card
        [MenuItem(M + "15 Picture: the blue unit card (in Play: L02, PLAY AS BLUE)", false, 60)]
        public static void BlueCard()
        {
            if (!EditorApplication.isPlaying || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning("[OzLightV2] start L02 in Play as blue first (PLAY AS BLUE)"); return; }
            var g = ZGame.I;
            if (!LightV2Spots.Get(g.level, "card", "blue", out int tx, out int ty)) { Debug.LogWarning($"[OzLightV2] no blue card tile for {g.level} (View/LightV2Stage.cs: L02 only)"); return; }
            var sp = OzCheatSpawner.I;
            var spawn = typeof(OzCheatSpawner).GetMethod("Spawn", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string), typeof(Team), typeof(Vector2Int) }, null);
            if (!sp || spawn == null) { Debug.LogWarning("[OzLightV2] the testing panel (OzCheatSpawner) is not in the scene"); return; }
            var before = new HashSet<ZObject>(g.World.Objects);
            try { spawn.Invoke(sp, new object[] { OzLightTank.TileV2, Team.Blue, new Vector2Int(tx * 16 + 16, ty * 16 + 16) }); }
            catch (System.Exception e) { Debug.LogWarning("[OzLightV2] the panel's drop failed (open the testing panel with ALT+P once, then again): " + e.GetBaseException().Message); return; }
            ZObject o = null;
            foreach (var x in g.World.Objects) if (x != null && !before.Contains(x) && x.Type == OzLightTank.Type && x.Owner == Team.Blue) o = x;
            if (o == null) { Debug.LogWarning($"[OzLightV2] no blue light tank was dropped at tile ({tx},{ty}) (the panel said no room?)"); return; }
            var pi = Object.FindFirstObjectByType<PlayerInput>();
            if (pi) { pi.Selection.Clear(); pi.Selection.Add(o); }
            var cam = Object.FindFirstObjectByType<RtsCamera>();
            if (cam) { var p = ZGame.PxToWorld(o.CenterX, o.CenterY); cam.focus = new Vector3(p.x, 0f, p.z); cam.SetHeightImmediate(35f); }
            Directory.CreateDirectory(ProofDir);
            string file = Path.Combine(ProofDir, "f_blue_unit_card.png");
            float at = (float)EditorApplication.timeSinceStartup + 1.5f;
            void Tick()
            {
                if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
                if (EditorApplication.timeSinceStartup < at) return;
                EditorApplication.update -= Tick;
                ScreenCapture.CaptureScreenshot(file);
                Debug.Log($"[OzLightV2] blue unit card: blue light tank #{o.Id} dropped at tile ({o.LocX >> 4},{o.LocY >> 4}) (asked: {tx},{ty}, the open-tile scan), selected {(pi ? "yes" : "NO (no PlayerInput)")}; picture {file}");
            }
            EditorApplication.update += Tick;
        }
    }
}
#endif
