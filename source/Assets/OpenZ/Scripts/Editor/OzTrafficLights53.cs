// the traffic lights of the city levels (level 17: C01 / C02 of the details v2 placement; level 18: L18_TL1..4 of the dressing placement, the same
// models DV2_C01 / DV2_C02) drawn at x0.48 (as tall as the street lamps beside them), each pole at the kerb so its shorter arm still reaches over the
// lane driving into the crossing (kit l18_street_r3's rule). The numbers live in the two placement files (x, z, scale), so the levels' own menus
// (Details v2/Build and Place, L18 Bridge Game/20 Dressing) give the same result; these menus only move and scale the lights already in the open
// scene (nothing re-made: the street signs, light cycles and break-up stay as they are). Level 17's sim items follow (DV209, Sim/ZWorld.TrafficLightsKerb.cs).
// Menus: OpenZ/V1.5/Traffic lights 53/1 (writes + saves the open level 17 or 18 scene), /2 checks the open scene (no write) into Logs/traffic_lights_53_<L>.txt.
// Kit traffic_light_sign_r6 (14 Nov): level 18's pole-top street sign is the compact model (Models/DetailsV2/L18S_StreetSign.fbx: a slip fitter over the
// pole cap, the bar on it, the same blade on the bar; top 0.632 m over the cap at x1, the old collar + post sign 1.087 m), so a level 18 light with its
// sign is 3.33 m at x0.48, no taller than the street lamps (3.35 m); the check measures the sign itself and holds the height row to the lamps (no 5 cm).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzTrafficLights53
    {
        public const float Scale = 0.48f;
        const float TopL18 = 7.39f, HeadsBottom = 4.05f;              // at scale 1: an L18 light with its OLD pole-top sign (6.30 + 1.087 m, r6 measured); the heads' bottom over the pole foot
        /// <summary>Kit traffic_light_sign_r6: the compact pole-top street sign's top over the pole cap (prefab metres, measured on its FBX); the old sign's 1.087.</summary>
        public const float SignTopOverCap = 0.632f, OldSignTopOverCap = 1.087f;
        const string L17File = "Assets/OpenZ/Resources/OpenZ/DetailsV2/placement_L17.json";

        struct Target { public string level, uid; public float x, y, z, scale; public Transform t; }

        static string OpenLevel()
        {
            var n = EditorSceneManager.GetActiveScene().name;
            return n.StartsWith(OzCity.BridgeGameScene) ? "L18" : n.Contains("L17") ? "L17" : null;
        }

        static List<Target> Targets(string L, StringBuilder sb)
        {
            var list = new List<Target>();
            if (L == "L17")
            {
                var pf = JsonUtility.FromJson<OpenZDetailsV2.PFile>(File.ReadAllText(L17File));
                var root = GameObject.Find("DetailsV2_L17");
                foreach (var it in pf.items ?? new OpenZDetailsV2.PItem[0])
                {
                    if (it == null || (it.id != "C01" && it.id != "C02")) continue;
                    var t = root ? root.transform.Find(string.IsNullOrEmpty(it.uid) ? it.id : it.uid) : null;
                    if (!t) sb.AppendLine("FAIL level 17 " + it.uid + ": not in the scene under DetailsV2_L17");
                    list.Add(new Target { level = L, uid = it.uid, x = it.x, y = it.y, z = it.z, scale = it.scale, t = t });
                }
            }
            else if (L == "L18")
            {
                var doc = OzL18Dressing.Read();
                var env = GameObject.Find("Environment");
                var root = env ? env.transform.Find("L18_Dressing/TrafficLights") : null;
                foreach (var l in doc?.traffic_lights ?? new OzL18Dressing.Light[0])
                {
                    var t = root ? root.Find(l.uid) : null;
                    if (!t) sb.AppendLine("FAIL level 18 " + l.uid + ": not in the scene under Environment/L18_Dressing/TrafficLights");
                    list.Add(new Target { level = L, uid = l.uid, x = l.x, y = l.y, z = l.z, scale = l.scale > 0f ? l.scale : 1f, t = t });
                }
            }
            return list;
        }

        /// <summary>The ground under (x, z): the highest collider hit under 1.5 m that is not water and not part of the light itself; NaN if none.</summary>
        static float Ground(float x, float z, Transform self)
        {
            float y = float.NaN;
            foreach (var h in Physics.RaycastAll(new Ray(new Vector3(x, 200f, z), Vector3.down), 400f))
            {
                if (self && h.collider.transform.IsChildOf(self)) continue;
                if (h.collider.name.IndexOf("water", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (h.point.y > 1.5f) continue;
                if (float.IsNaN(y) || h.point.y > y) y = h.point.y;
            }
            return y;
        }

        [MenuItem("OpenZ/V1.5/Traffic lights 53/1 Smaller traffic lights in the open level (17 or 18), save the scene", false, 3200)]
        static void ApplyMenu() => Debug.Log("[OpenZ V1.5] traffic lights 53: " + Apply());

        public static string Apply()
        {
            if (Application.isPlaying) return "leave Play first";
            var L = OpenLevel(); if (L == null) return "open the level 17 or level 18 scene first";
            var sb = new StringBuilder();
            Physics.SyncTransforms();
            int n = 0;
            foreach (var g in Targets(L, sb))
            {
                if (!g.t) continue;
                float y = Ground(g.x, g.z, g.t); if (float.IsNaN(y)) { y = g.y; sb.Append(g.uid + ": no ground hit, file y used; "); }
                g.t.position = new Vector3(g.x, y, g.z);
                g.t.localScale = Vector3.one * g.scale;
                PrefabUtility.RecordPrefabInstancePropertyModifications(g.t);
                sb.Append($"{g.uid} at ({g.x:0.000}, {y:0.000}, {g.z:0.000}) x{g.scale:0.00}; ");
                n++;
            }
            var s = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(s); EditorSceneManager.SaveScene(s);
            return $"{L}: {n} traffic light(s) moved + scaled, scene saved. " + sb;
        }

        [MenuItem("OpenZ/V1.5/Traffic lights 53/2 Check the open level (no write)", false, 3201)]
        static void CheckMenu() => Debug.Log("[OpenZ V1.5] traffic lights 53 check:\n" + Check());

        static Bounds Box(Transform t, System.Func<Renderer, bool> keep)
        {
            var rs = t.GetComponentsInChildren<Renderer>().Where(keep).ToArray();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        public static string Check()
        {
            var L = OpenLevel(); if (L == null) return "open the level 17 or level 18 scene first";
            var sb = new StringBuilder(); int pass = 0, fail = 0;
            void Row(bool ok, string s) { sb.AppendLine((ok ? "PASS " : "FAIL ") + s); if (ok) pass++; else fail++; }
            Physics.SyncTransforms();
            var lamps = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(t => t.name.IndexOf("lamp", System.StringComparison.OrdinalIgnoreCase) >= 0 && t.GetComponentInChildren<Renderer>()).Take(400)
                .Select(t => Box(t, r => true).size.y).Where(h => h > 1f && h < 30f).OrderBy(h => h).ToList();
            float lamp = lamps.Count > 0 ? lamps[lamps.Count / 2] : 0f;
            sb.AppendLine($"     street lamps in the open scene: {lamps.Count} lamp objects, median height {lamp:0.00} m (the night posts along the roads are drawn x0.85 of it in Play: {lamp * 0.85f:0.00} m)");
            var targets = Targets(L, sb);
            Row(targets.Count == (L == "L17" ? 2 : 4), $"{L}: {targets.Count} traffic lights in the file (want {(L == "L17" ? 2 : 4)})");
            foreach (var g in targets)
            {
                if (!g.t) { fail++; continue; }
                var p = g.t.position;
                Row(Mathf.Abs(p.x - g.x) < 0.05f && Mathf.Abs(p.z - g.z) < 0.05f, $"{g.uid}: pole at ({p.x:0.000}, {p.z:0.000}), file ({g.x:0.000}, {g.z:0.000})");
                Row(Mathf.Abs(g.scale - Scale) < 0.005f && Mathf.Abs(g.t.lossyScale.x - Scale) < 0.005f, $"{g.uid}: scale {g.t.lossyScale.x:0.000} (file {g.scale:0.00}, want {Scale:0.00})");
                float gy = Ground(p.x, p.z, g.t);
                Row(!float.IsNaN(gy) && Mathf.Abs(p.y - gy) < 0.03f, $"{g.uid}: foot {p.y:0.000} on the ground {(float.IsNaN(gy) ? "(no hit)" : gy.ToString("0.000"))}");
                var all = Box(g.t, r => true);
                float top = all.max.y - p.y;
                Row(top <= Mathf.Max(lamp, 3.35f) + 0.005f && top > 2.5f, $"{g.uid}: height {top:0.000} m (was {(L == "L18" ? TopL18 : 6.88f):0.00} at scale 1; street lamp {lamp:0.000} m: never taller, no allowance)");
                var heads = Box(g.t, r => OpenZ.DetailKit.Base(r.name).StartsWith("LOOSE_Head") || r.transform.GetComponentsInParent<Transform>(true).Any(a => OpenZ.DetailKit.Base(a.name).StartsWith("LOOSE_Head")));
                float hb = heads.min.y - p.y;
                Row(heads.size.y > 0f && Mathf.Abs(hb - HeadsBottom * Scale) < 0.25f, $"{g.uid}: heads' bottom {hb:0.00} m over the foot (want about {HeadsBottom * Scale:0.00}); heads {heads.size.y:0.00} m tall");
                Row(g.t.GetComponentInChildren<OpenZ.TrafficLightCycle>(true) && g.t.GetComponentInChildren<OpenZ.DetailProp>(true), $"{g.uid}: light cycle + destructible prop kept");
                if (L == "L18")
                {
                    Row(g.t.GetComponentsInChildren<Transform>(true).Any(c => OpenZ.DetailKit.Base(c.name) == OzStreetR3.SignName), $"{g.uid}: street sign kept on the pole top");
                    var sign = g.t.GetComponentsInChildren<Transform>(true).FirstOrDefault(c => OpenZ.DetailKit.Base(c.name) == OzStreetR3.SignName);   // r6: the compact sign's own top
                    float s = Mathf.Max(1e-4f, g.t.lossyScale.y), over = sign ? (Box(sign, r => true).max.y - (p.y + OzStreetR3.PoleTop * s)) / s : 0f;
                    Row(sign && Mathf.Abs(over - SignTopOverCap) < 0.02f, $"{g.uid}: the compact pole-top street sign (r6): its top {over:0.000} m over the pole cap at x1 = {over * s:0.00} m drawn (want {SignTopOverCap:0.000}; the old sign {OldSignTopOverCap:0.000})");
                    Row(g.t.GetComponentsInChildren<Collider>(true).Length == 0, $"{g.uid}: no collider (view only)");
                }
            }
            sb.AppendLine("     units under the heads (CombatView.BodyHeight x 0.9, VEHICLE SIZE 90 %): robots / jeep / light tank 1.89 m pass under; heavy / medium 2.34 m, Mammoth 2.79 m, Bizon 3.06 m reach into the heads when they drive right under them");
            var text = $"{(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail} traffic lights 53, {L}\n" + sb;
            try { File.WriteAllText(Path.Combine(Application.dataPath, "../Logs/traffic_lights_53_" + L + ".txt"), text); } catch (System.Exception) { }
            return text;
        }
    }
}
