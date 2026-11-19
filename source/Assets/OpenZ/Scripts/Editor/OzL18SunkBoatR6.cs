// The half-sunk ship is BACK in level 18's west side canal (68, -81),
// as the dressing file's piece D1S. Not the removed part-maker barge D1 (upside down, open hull): the finished Gen wreck L18R_ShipWreck (the
// harbour's own ship, hull closed + two-sided by kit l18_map_fixes_r3), upright, deck + bridge + funnel on top, at 0.19 (8.5 m long, the
// approved half-size barge's length), the bow down and under the water, the stern + bridge out of it, a small list. VIEW ONLY: static, no
// collider, no map / rule / Sim change (units wading the canal are not stopped by it; it leaves about 2.3 m of canal free on each side).
// Menus: OpenZ/V1.5/L18 sunk boat r6/1 places it in the open level 18 scene with OzL18Dressing's own piece builder (LOD group + M_L18R_ShipWreck
// materials), measures the pose in Unity (bow end, deck line, water), writes the measured pose back to the dressing file when it differs (so
// menu 20 rebuilds it the same) and SAVES the scene; /2 checks the open scene (no write).
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL18SunkBoatR6
    {
        const string Id = "D1S", Piece = "L18R_ShipWreck", File_ = "Assets/OpenZ/Models/L18/Ruins/L18_dressing_placement.json";
        const float WaterY = -0.80f, CX = 68f, CZ = -81f, BowUnder = 0.5f;              // the level's water sheet (OzL18ContainersMenu water_y); D1's canal spot

        [MenuItem("OpenZ/V1.5/L18 sunk boat r6/1 Place the sunk ship in the side canal + save the scene", false, 7706)]
        static void PlaceMenu() => Debug.Log("[OpenZ] l18_sunk_boat_r6: " + Place());

        [MenuItem("OpenZ/V1.5/L18 sunk boat r6/2 Check the open scene (no write)", false, 7707)]
        static void CheckMenu() => Debug.Log("[OpenZ] l18_sunk_boat_r6 check: " + Check());

        static bool L18Open() { var s = EditorSceneManager.GetActiveScene(); return !Application.isPlaying && s.name.StartsWith("L18_BridgeGame"); }

        static OzL18Dressing.Piece Data() => OzL18Dressing.Read()?.pieces?.FirstOrDefault(p => p.id == Id && !p.removed);

        static Transform Host()
        {
            var env = GameObject.Find("Environment");
            return env ? env.transform.Find("L18_Dressing/Pieces/" + Id + "_" + Piece) : null;
        }

        static void Pose(Transform host, float x, float y, float z, float yaw, float tilt, float heading, float scale)
        {   // OzL18Dressing.Place()'s maths, line for line
            var d = new Vector3(Mathf.Cos(heading * Mathf.Deg2Rad), 0, Mathf.Sin(heading * Mathf.Deg2Rad));
            var t = Mathf.Abs(tilt) > 0.01f ? Quaternion.AngleAxis(tilt, Vector3.Cross(Vector3.up, d)) : Quaternion.identity;
            host.SetPositionAndRotation(new Vector3(x, y, z), t * Quaternion.Euler(0, yaw, 0));
            host.localScale = Vector3.one * (scale > 0 ? scale : 1f);
        }

        /// <summary>The LOD0 hull's points: world and host-local (the model's own frame: long axis x, y up).</summary>
        static void Points(Transform host, out Vector3[] w, out Vector3[] l)
        {
            var mf = host.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name == Piece && m.sharedMesh);
            w = mf ? mf.sharedMesh.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray() : new Vector3[0];
            l = w.Select(v => host.InverseTransformPoint(v)).ToArray();
        }

        public struct M { public bool ok, bowAtPlusX; public float deckPitch, keelPitch, roll, bowTop, sternRail, under, xMin, xMax, zMin, zMax; public Vector3 centre; }

        /// <summary>Measured on the model's own points: the bow = the end whose lowest point is higher (the raised, narrow end); the deck line =
        /// the stern rail (model y 5.0-5.7, last 3 m) to the bow's top (model top 15 %, first 2 m); keel = the flat stern keel (lowest 0.2 m).</summary>
        public static M Measure(Transform host)
        {
            Points(host, out var w, out var l); var m = new M();
            if (w.Length == 0) return m;
            float x0 = l.Min(v => v.x), x1 = l.Max(v => v.x), y0 = l.Min(v => v.y), y1 = l.Max(v => v.y);
            var idx = Enumerable.Range(0, l.Length).ToArray();
            float lowA = idx.Where(i => l[i].x > x1 - 3f).Min(i => l[i].y), lowB = idx.Where(i => l[i].x < x0 + 3f).Min(i => l[i].y);
            m.bowAtPlusX = lowA > lowB; float s = m.bowAtPlusX ? 1f : -1f, bowX = m.bowAtPlusX ? x1 : x0, sternX = m.bowAtPlusX ? x0 : x1;
            var bow = idx.Where(i => Mathf.Abs(l[i].x - bowX) < 2f && l[i].y > y0 + 0.85f * (y1 - y0)).Select(i => w[i]).ToArray();
            var rail = idx.Where(i => Mathf.Abs(l[i].x - sternX) < 3f && l[i].y > y0 + 6.5f && l[i].y < y0 + 7.2f).Select(i => w[i]).ToArray();
            var keel = idx.Where(i => l[i].y < y0 + 0.2f).ToArray();
            if (bow.Length == 0 || rail.Length == 0 || keel.Length < 4) return m;
            Vector3 B = bow.Aggregate(Vector3.zero, (a, v) => a + v) / bow.Length, S = rail.Aggregate(Vector3.zero, (a, v) => a + v) / rail.Length;
            m.deckPitch = Mathf.Atan2(S.y - B.y, new Vector2(S.x - B.x, S.z - B.z).magnitude) * Mathf.Rad2Deg;     // + = bow lower
            var kf = keel.OrderBy(i => l[i].x * s).ToArray(); Vector3 ka = w[kf.First()], kb = w[kf.Last()];
            m.keelPitch = Mathf.Atan2(ka.y - kb.y, new Vector2(ka.x - kb.x, ka.z - kb.z).magnitude) * Mathf.Rad2Deg;
            m.roll = Mathf.Asin(Mathf.Clamp(host.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            m.bowTop = B.y - WaterY; m.sternRail = S.y - WaterY;
            m.under = w.Count(v => v.y < WaterY) / (float)w.Length;
            m.xMin = w.Min(v => v.x); m.xMax = w.Max(v => v.x); m.zMin = w.Min(v => v.z); m.zMax = w.Max(v => v.z);
            m.centre = new Vector3((m.xMin + m.xMax) / 2f, 0f, (m.zMin + m.zMax) / 2f); m.ok = true;
            return m;
        }

        public static string Place()
        {
            if (!L18Open()) return "FAIL: open Assets/OpenZ/Scenes/L18_BridgeGame.unity first, out of Play";
            var it = Data(); if (it == null) return "FAIL: piece " + Id + " not in " + File_ + " (apply the kit first)";
            var env = GameObject.Find("Environment"); var root = env ? env.transform.Find("L18_Dressing") : null;
            if (!root) return "FAIL: no Environment/L18_Dressing in the scene (run OpenZ/L18 Bridge Game/20 first)";
            var pc = root.Find("Pieces"); if (!pc) { pc = new GameObject("Pieces").transform; pc.SetParent(root, false); }
            for (int i = pc.childCount - 1; i >= 0; i--) if (pc.GetChild(i).name.StartsWith(Id + "_")) Object.DestroyImmediate(pc.GetChild(i).gameObject);
            var host = new GameObject(Id + "_" + Piece).transform; host.SetParent(pc, false);
            float yaw = it.unity_yaw_deg, head = it.tilt_heading_deg, sc = it.scale > 0 ? it.scale : 1f;
            Pose(host, it.x, it.y - it.sink_extra_m, it.z, yaw, it.tilt_deg, head, sc);
            var sb = new StringBuilder(); object[] a = { Piece, host, 0, sb };
            var mi = typeof(OzL18Dressing).GetMethod("RuinPiece", BindingFlags.NonPublic | BindingFlags.Static);
            var p = mi != null ? mi.Invoke(null, a) as GameObject : null;
            if (!p) { Object.DestroyImmediate(host.gameObject); return "FAIL: the piece builder made nothing: " + sb; }
            p.transform.localPosition = Vector3.zero; p.transform.localRotation = Quaternion.identity; p.transform.localScale = Vector3.one;
            var m = Measure(host);
            if (!m.ok) return "FAIL: could not measure the hull (LOD0 mesh " + Piece + ")";
            string fixes = "";
            if (m.deckPitch < 0f)                                                                    // pass B 14 Nov: the tilt tipped the STERN under in Unity (deck line -52.7): tip towards the other end (heading only; turning yaw + heading together changed nothing)
            { head += 180f; Pose(host, it.x, it.y - it.sink_extra_m, it.z, yaw, it.tilt_deg, head, sc); fixes += "tilt heading turned 180 (bow end down); "; m = Measure(host); }
            float dx = CX - m.centre.x, dz = CZ - m.centre.z, dy = -BowUnder - m.bowTop;           // centre on the canal spot, the bow's top BowUnder under the water
            host.position += new Vector3(dx, dy, dz); m = Measure(host);
            if (Mathf.Abs(dx) > 0.02f || Mathf.Abs(dz) > 0.02f || Mathf.Abs(dy) > 0.02f) fixes += $"moved {dx:+0.00;-0.00} / {dy:+0.00;-0.00} / {dz:+0.00;-0.00} m; ";
            int cols = 0; foreach (var c in host.GetComponentsInChildren<Collider>(true)) { Object.DestroyImmediate(c); cols++; }
            foreach (var t in host.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
            if (fixes.Length > 0) fixes += WriteBack(host.position + Vector3.up * it.sink_extra_m, yaw, head);
            var sc0 = EditorSceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(sc0);
            bool saved = EditorSceneManager.SaveScene(sc0);
            return $"{Row(m)}; {(int)a[2]} triangles at LOD0, {cols} colliders removed; {(fixes.Length > 0 ? fixes : "pose = the file's; ")}scene {(saved ? "SAVED" : "NOT SAVED")}. {sb}";
        }

        static string Row(M m) => $"deck line {m.deckPitch:0.0} deg bow-down (keel {m.keelPitch:0.0}), list {m.roll:0.0}, bow top {m.bowTop:+0.00;-0.00} m / stern rail {m.sternRail:+0.00;-0.00} m vs the water, " +
                                  $"{m.under * 100f:0} % of the hull under water, x {m.xMin:0.00}..{m.xMax:0.00} (canal 64..72), z {m.zMin:0.00}..{m.zMax:0.00}";

        /// <summary>The measured pose into D1S's lines of the dressing file (x, y, z, yaw, heading), so menu 20 builds the same ship.</summary>
        static string WriteBack(Vector3 pos, float yaw, float head)
        {
            var t = System.IO.File.ReadAllText(File_); int a = t.IndexOf("\"id\": \"" + Id + "\""); if (a < 0) return "file not written (D1S missing); ";
            int b = t.IndexOf('}', a); var blk = t.Substring(a, b - a); var org = blk;
            string F(float v) => v.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
            blk = Regex.Replace(blk, "\"x\": [-0-9.]+", "\"x\": " + F(pos.x)); blk = Regex.Replace(blk, "\"y\": [-0-9.]+", "\"y\": " + F(pos.y));
            blk = Regex.Replace(blk, "\"z\": [-0-9.]+", "\"z\": " + F(pos.z)); blk = Regex.Replace(blk, "\"unity_yaw_deg\": [-0-9.]+", "\"unity_yaw_deg\": " + F(yaw));
            blk = Regex.Replace(blk, "\"tilt_heading_deg\": [-0-9.]+", "\"tilt_heading_deg\": " + F(head));
            if (blk == org) return "file unchanged; ";
            System.IO.File.WriteAllText(File_, t.Substring(0, a) + blk + t.Substring(b)); AssetDatabase.ImportAsset(File_);
            return "the measured pose written to D1S in " + File_ + "; ";
        }

        public static string Check()
        {
            if (!L18Open()) return "FAIL: open the level 18 scene first, out of Play";
            int n = 0, pass = 0; var rows = new StringBuilder();
            void R(bool ok, string what) { n++; if (ok) pass++; rows.Append((ok ? "PASS " : "FAIL ") + what + " | "); }
            var it = Data(); R(it != null, "piece D1S (L18R_ShipWreck) in the dressing file, not removed");
            var env = GameObject.Find("Environment"); var pcs = env ? env.transform.Find("L18_Dressing/Pieces") : null;
            int copies = pcs ? pcs.Cast<Transform>().Count(c => c.name.StartsWith(Id + "_")) : 0;
            R(copies == 1, $"one ship in Environment/L18_Dressing/Pieces ({copies})");
            var h = Host(); if (!h) return $"FAIL {pass}/{n}: " + rows;
            var m = Measure(h); R(m.ok, "hull measured");
            R(Vector3.Dot(h.up, Vector3.up) > 0.75f, $"upright: deck up ({Vector3.Angle(h.up, Vector3.up):0} deg off level, never upside down)");
            R(m.deckPitch >= 15f && m.deckPitch <= 25f, $"deck line {m.deckPitch:0.0} deg, bow lower (15-25)");
            R(m.bowTop < -0.2f && m.sternRail > 0.8f && m.sternRail < 3.0f, $"partly under water: bow top {m.bowTop:+0.00;-0.00} m, stern rail {m.sternRail:+0.00;-0.00} m");
            R(Mathf.Abs(m.roll) >= 3f && Mathf.Abs(m.roll) <= 12f, $"irregular list {m.roll:0.0} deg (3-12)");
            R(Mathf.Abs(m.centre.x - CX) < 0.3f && Mathf.Abs(m.centre.z - CZ) < 0.3f, $"in the side canal: centre ({m.centre.x:0.00}, {m.centre.z:0.00})");
            R(m.xMin > 65.5f && m.xMax < 70.5f, $"canal room: x {m.xMin:0.00}..{m.xMax:0.00}, {m.xMin - 64f:0.0} / {72f - m.xMax:0.0} m free each side (canal 64..72)");
            float len = Mathf.Max(m.xMax - m.xMin, m.zMax - m.zMin); R(len > 7f && len < 9.5f, $"half size: {len:0.0} m long (the approved barge 7.9 m)");
            var lg = h.GetComponentInChildren<LODGroup>(); R(lg && lg.lodCount == 3, $"LOD group {(lg ? lg.lodCount : 0)} levels");
            int other = h.GetComponentsInChildren<Renderer>(true).Sum(r => r.sharedMaterials.Count(x => !x || !x.name.StartsWith("M_L18R_ShipWreck")));
            R(other == 0, $"materials = M_L18R_ShipWreck(_LOD2) on every slot ({other} other / empty)");
            int cols = h.GetComponentsInChildren<Collider>(true).Length; R(cols == 0, $"view only: {cols} colliders");
            if (it != null)
            {
                var g = new GameObject("tmp_pose").transform; Pose(g, it.x, it.y - it.sink_extra_m, it.z, it.unity_yaw_deg, it.tilt_deg, it.tilt_heading_deg, it.scale);
                bool same = Vector3.Distance(g.position, h.position) < 0.03f && Quaternion.Angle(g.rotation, h.rotation) < 0.5f; Object.DestroyImmediate(g.gameObject);
                R(same, "menu 20 would build the same pose from the file");
            }
            R(OzL18Dressing.Check().StartsWith("PASS"), "OpenZ/L18 Bridge Game/21 dressing check PASS");
            return $"{(pass == n ? "PASS" : "FAIL")} {pass}/{n}: " + Row(m) + " | " + rows;
        }
    }
}
