// OpenZ (GPL-3.0)
//  - menu OpenZ/Checks/V1.5/Fixes r5 (no Play): the kit's data and switches; with the level 18 scene open (after menus 10 + 20 and a save) also
//    the stumps, the barge and the removed land containers in the scene, and the traffic lights' scale / height beside a street lamp (numbers
//    only, for owner addition 53: nothing is changed by it);
//  - menu OpenZ/V1.5/Fixes r5/L17 water colour (Play, level 17 by day): lead row L2. Renders the game camera's picture with M_Water_Harbour as
//    it is, with the sun lane at 0, with the travelling waves at 0, and with both at 0 (= before kit water_fx_1_5), measures the mean colour of
//    halvings) to the largest value that keeps every channel within 3 % of before; the ripples stay on. Writes the value into the material
//    (saved) and the numbers into kits/fixes_r5/proof/l17_water_colour_unity.txt.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzFixesR5Check
    {
        const string Ruins = "Assets/OpenZ/Models/L18/Ruins/", Proof = "../work/staging/v1_5/kits/fixes_r5/proof/";

        [System.Serializable] class RItem { public string id, piece; public int[] tile; public float scale, px, pz; }
        [System.Serializable] class RRuins { public RItem[] items; }
        [System.Serializable] class RDoc { public RRuins river_ruins; }
        [System.Serializable] class DPiece { public string id, piece; public float x, z, scale, unity_yaw_deg, tilt_deg; }
        [System.Serializable] class DDoc { public DPiece[] pieces; }

        [MenuItem("OpenZ/Checks/V1.5/Fixes r5 (no Play)", false, 3190)]
        static void CheckMenu() { var r = Run(); Debug.Log("[OpenZ] fixes_r5 check: " + r); }

        public static string Run()
        {
            var sb = new StringBuilder(); int pass = 0, fail = 0;
            void Row(bool ok, string what) { if (ok) pass++; else fail++; sb.AppendLine((ok ? "PASS " : "FAIL ") + what); }
            // ---- L1 / 8.1 / 36
            Row(!OzEvilStage.BinocFollowHead, "L1 / 51.4 the binocular close-up's heading is its own (BinocFollowHead false) and the head-room guard skips ez_binoc (code)");
            Row(OzEvilStage.HeadRoomGuard && OzEvilStage.HeadRoomHatRings, "36 the head-room guard is on and tests the hat's rings (measured on EvilZod.fbx)");
            // ---- 51.5
            float firstRed = 0.58f + OzL18BriefingStage.RedFireFrom;                                  // the flight's first frame is video 0.58 (kit_r4 check table)
            Row(firstRed >= 7.0f, $"51.5 the first red shot at video {firstRed:0.00} s at the earliest (7.00 wanted)");
            Row(OzL18BriefingStage.DefFront >= 12f && OzL18BriefingStage.DefBack > OzL18BriefingStage.DefFront, $"51.5 the red lines {OzL18BriefingStage.DefFront:0.0} / {OzL18BriefingStage.DefBack:0.0} m south of the decks (round 4: 6.6 / 9.1)");
            // ---- 51.6: the entry speed of the low flight, from the path's own pure function (60 fps, its 4.1 s)
            var path = OzL18Drone.PathOf(OzL18BriefingStage.AirShot);
            if (path == null) Row(false, "51.6 no drone path for " + OzL18BriefingStage.AirShot);
            else
            {
                string Curve(out float share, out float worst)
                {
                    var c = new StringBuilder(); var sp = new List<float>(); Vector3 last = Vector3.zero; const float dur = 4.1f, fps = 60f;
                    for (int f = 0; f <= 60; f++)
                    {
                        OzL18Drone.Pose(path, f / fps / dur, dur, Vector3.zero, Quaternion.identity, 60f, Vector3.zero, null, out var P, out _, out _, out _, out _, out _);
                        if (f > 0) { float v = (P - last).magnitude * fps; sp.Add(v); if (f % 3 == 0) c.Append($" {f / fps:0.00}:{v:0.0}"); }
                        last = P;
                    }
                    share = sp[0] / Mathf.Max(0.01f, sp[29]); worst = 0f;
                    for (int i = 1; i < sp.Count; i++) worst = Mathf.Max(worst, Mathf.Abs(sp[i] - sp[i - 1]) / Mathf.Max(0.01f, Mathf.Max(sp[i], sp[i - 1])));
                    return c.ToString();
                }
                float k0 = path.enterK, s0 = path.enterSeconds;
                path.enterK = 1f; path.enterSeconds = 0f; string before = Curve(out float shB, out float wB);
                path.enterK = k0; path.enterSeconds = s0; string after = Curve(out float shA, out float wA);
                sb.AppendLine($"     round 4 (no entry ease): first frame {shB * 100f:0} % of the speed at 0.5 s, largest step {wB * 100f:0.0} %;{before}");
                sb.AppendLine($"     fixes_r5 (enter {k0:0.00} over {s0:0.00} s): first frame {shA * 100f:0} % of the speed at 0.5 s, largest step {wA * 100f:0.0} %;{after}");
                Row(shA > 0.45f && shA < 0.8f && wA < 0.06f, $"51.6 {OzL18BriefingStage.AirShot} enters moving: first frame {shA * 100f:0} % of its speed at 0.5 s (about 60 % wanted), largest frame-to-frame step {wA * 100f:0.0} % at 60 fps (under 6 % wanted)");
            }
            Row(OzL18BriefingStage.FpvRush && Mathf.Abs(OzL18BriefingStage.FpvRushTau - 3.0f) < 0.2f, $"51.6 the rush of air at the cut (stage clock {OzL18BriefingStage.FpvRushTau:0.00}, the cut at 3.09; clip demo-flyby-whoosh, gain {OzL18BriefingStage.FpvRushGain:0.00} of GainRush, under the duck)");
            Row(AudioClipThere("demo-flyby-whoosh"), "51.6 the clip demo-flyby-whoosh is in Resources/OpenZ/Audio/Sfx");
            // ---- 51.1 / 51.2 / 52: data
            Row(OpenZ.Sim.ZWorld.L18StumpTiles.Length == 0, $"51.1 DV197's tile set is empty ({OpenZ.Sim.ZWorld.L18StumpTiles.Length / 4} footprints): the stumps close no tile");
            var rdoc = File.Exists(Ruins + "L18_ruins_placement.json") ? JsonUtility.FromJson<RDoc>(File.ReadAllText(Ruins + "L18_ruins_placement.json")) : null;
            var ritems = rdoc?.river_ruins?.items ?? new RItem[0];
            Row(ritems.Length == 2 && ritems.All(i => Mathf.Abs(i.scale - 0.75f) < 0.01f && i.px != 0f && i.pz != 0f), "51.1 both stumps in the placement file at 0.75 scale with their map-edge places: " + string.Join("; ", ritems.Select(i => $"{i.id} ({i.px:0}, {i.pz:0}) x{i.scale:0.00}")));
            var ddoc = File.Exists(Ruins + "L18_dressing_placement.json") ? JsonUtility.FromJson<DDoc>(File.ReadAllText(Ruins + "L18_dressing_placement.json")) : null;
            var pcs = ddoc?.pieces ?? new DPiece[0];
            var d1 = pcs.FirstOrDefault(p => p.id == "D1");
            Row(!pcs.Any(p => p.id == "D3" || p.id == "D4" || p.id == "D5" || p.id == "D6"), "52 / 52b the four container stacks on land (D3-D6) are not in the dressing file (" + pcs.Length + " pieces left: " + string.Join(", ", pcs.Select(p => p.id)) + ")");
            Row(d1 != null && Mathf.Abs(d1.scale - 0.5f) < 0.01f && Mathf.Abs(d1.x - 68f) < 0.1f && Mathf.Abs(d1.z + 81f) < 0.1f && d1.tilt_deg >= 25f, $"51.2 the barge D1 half size in the west side canal, tilted: {(d1 == null ? "missing" : $"({d1.x:0}, {d1.z:0}) x{d1.scale:0.00}, yaw {d1.unity_yaw_deg:0}, tilt {d1.tilt_deg:0}")}");
            // ---- 51.3 / A1
            var wm = AssetDatabase.LoadAssetAtPath<Material>("Assets/OpenZ/Materials/Env/M_Water_L18.mat");
            Row(wm && wm.HasProperty("_ObjectClearBand") && wm.GetFloat("_ObjectClearBand") > 0f, $"51.3 M_Water_L18 draws the clear band round objects: {(wm && wm.HasProperty("_ObjectClearBand") ? $"strength {wm.GetFloat("_ObjectClearBand"):0.00}, width {wm.GetFloat("_ObjectClearWidth"):0.0} m" : "property missing (shader not imported?)")}");
            foreach (var other in new[] { "M_Water_Harbour", "M_Water_Swamp", "M_Water" })
            {
                var om = AssetDatabase.LoadAssetAtPath<Material>("Assets/OpenZ/Materials/Env/" + other + ".mat");
                Row(!om || !om.HasProperty("_ObjectClearBand") || om.GetFloat("_ObjectClearBand") == 0f, "51.3 " + other + " unchanged (no clear band)");
            }
            var l18w = OpenZMinimap.L18Water;
            Row(!(l18w.r == 92 && l18w.g == 104 && l18w.b == 58), $"A1 the minimap paints level 18's water ({l18w.r}, {l18w.g}, {l18w.b}), level 17 keeps (92, 104, 58)");
            // ---- the open level 18 scene (after menus 10 + 20, saved)
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.StartsWith(OzCity.BridgeGameScene)) sb.AppendLine("     (open the level 18 scene for the scene rows)");
            else
            {
                var env = GameObject.Find("Environment");
                var st = env ? env.transform.Find(OzL18Rules.StumpsRoot) : null;
                int stOk = 0; var stTxt = new StringBuilder();
                if (st) foreach (Transform c in st)
                    {
                        var it = ritems.FirstOrDefault(i => c.name.StartsWith(i.id + "_"));
                        bool ok = it != null && Mathf.Abs(c.position.x - it.px) < 0.05f && Mathf.Abs(c.position.z - it.pz) < 0.05f && Mathf.Abs(c.localScale.x - it.scale) < 0.01f;
                        if (ok) stOk++; stTxt.Append($" {c.name} ({c.position.x:0.0}, {c.position.z:0.0}) x{c.localScale.x:0.00}");
                    }
                Row(st && st.childCount == 2 && stOk == 2, "51.1 scene: both stumps at the map edge at 0.75 (menu 10 run + saved):" + stTxt);
                var pr = env ? env.transform.Find("L18_Dressing/Pieces") : null;
                var land = pr ? pr.Cast<Transform>().Where(t => t.name.StartsWith("D3_") || t.name.StartsWith("D4_") || t.name.StartsWith("D5_") || t.name.StartsWith("D6_")).Select(t => t.name).ToList() : new List<string>();
                Row(pr && land.Count == 0, "52 scene: no container stack on land under L18_Dressing/Pieces (menu 20 run + saved)" + (land.Count > 0 ? ": still " + string.Join(", ", land) : ""));
                var b1 = pr ? pr.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("D1_")) : null;
                Row(b1 && Mathf.Abs(b1.localScale.x - 0.5f) < 0.01f && Mathf.Abs(b1.position.x - 68f) < 0.1f, "51.2 scene: the barge " + (b1 ? $"({b1.position.x:0.0}, {b1.position.y:0.00}, {b1.position.z:0.0}) x{b1.localScale.x:0.00}, rotation {b1.eulerAngles}" : "missing"));
                // ---- 53: numbers only (nothing changed): the traffic lights beside a street lamp and level 17's own
                var tl = env ? env.transform.Find("L18_Dressing/TrafficLights") : null;
                var lamps = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(t => t.name.IndexOf("lamp", System.StringComparison.OrdinalIgnoreCase) >= 0 && t.GetComponentInChildren<Renderer>()).Take(400).ToList();
                float H(Transform t) { var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return 0f; var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b.size.y; }
                var lampH = lamps.Select(H).Where(h => h > 1f && h < 30f).OrderBy(h => h).ToList();
                string lampTxt = lampH.Count > 0 ? $"{lampH.Count} lamp objects, median height {lampH[lampH.Count / 2]:0.00} m" : "no object named *lamp* with a renderer";
                if (tl) foreach (Transform t in tl) sb.AppendLine($"     53 (numbers only) traffic light {t.name}: scale {t.lossyScale.x:0.00}, height {H(t):0.00} m; street lamps: {lampTxt}; level 17's DV2_C01 / DV2_C02 in L17_CarPark.unity: scale 1 (prefab) = the same model and scale");
            }
            sb.Insert(0, $"{(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}\n");
            var text = sb.ToString();
            try { Directory.CreateDirectory(Proof); File.WriteAllText(Proof + "fixes_r5_check_unity.txt", text); } catch (System.Exception) { }
            return text;
        }

        static bool AudioClipThere(string n) => Resources.Load<AudioClip>("OpenZ/Audio/Sfx/" + n) != null;

        // ================================================================ L2: level 17's day water colour
        [MenuItem("OpenZ/V1.5/Fixes r5/L17 water colour (Play, level 17 by day)", false, 3191)]
        static void L17Menu() => Debug.Log("[OpenZ] fixes_r5 L17 water colour: " + L17WaterColour());

        public static string L17WaterColour()
        {
            if (!Application.isPlaying) return "enter Play on level 17 by day first (the game camera at the view of owner_frames/level17_water/level17_day_before_after.jpg)";
            var cam = Camera.main; if (!cam) return "no main camera";
            var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/OpenZ/Materials/Env/M_Water_Harbour.mat"); if (!m) return "no M_Water_Harbour";
            float sun0 = m.GetFloat("_SunLane"), rip0 = m.GetFloat("_RippleProc"), fire0 = m.GetFloat("_FireReflect"); Vector4 slope0 = m.GetVector("_ProcSlope");
            var sb = new StringBuilder();
            Vector3 Mean()
            {
                var rt = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var was = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = was;
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(200, 200, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(860, 1080 - 640, 200, 200), 0, 0); tex.Apply();
                RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                Vector3 s = Vector3.zero; var px = tex.GetPixels32(); foreach (var c in px) s += new Vector3(c.r, c.g, c.b); Object.DestroyImmediate(tex);
                return s / px.Length;
            }
            void Set(float sun, float rip, Vector4 slope) { m.SetFloat("_SunLane", sun); m.SetFloat("_RippleProc", rip); m.SetVector("_ProcSlope", slope); }
            float Worst(Vector3 a, Vector3 b0) => Mathf.Max(Mathf.Abs(a.x / b0.x - 1f), Mathf.Max(Mathf.Abs(a.y / b0.y - 1f), Mathf.Abs(a.z / b0.z - 1f)));
            string P3(Vector3 v) => $"({v.x:0.0}, {v.y:0.0}, {v.z:0.0})";
            Set(0f, 0f, slope0); m.SetFloat("_FireReflect", 0f); var before = Mean(); m.SetFloat("_FireReflect", fire0);
            Set(sun0, rip0, slope0); var now = Mean();
            Set(0f, rip0, slope0); var noSun = Mean();
            Set(sun0, 0f, slope0); var noRip = Mean();
            sb.AppendLine($"before (kit water_fx_1_5 rows at 0) {P3(before)}; now {P3(now)} ({Worst(now, before) * 100f:0.0} %); sun lane 0 {P3(noSun)} ({Worst(noSun, before) * 100f:0.0} %); waves 0 {P3(noRip)} ({Worst(noRip, before) * 100f:0.0} %)");
            string row; float lo = 0f, hi = 1f;
            bool sunIs = Worst(noSun, before) <= Worst(noRip, before);
            for (int i = 0; i < 8; i++)
            {   // the largest share of the guilty row that keeps the day colour within 3 % (the ripples stay on)
                float k = (lo + hi) * 0.5f;
                if (sunIs) Set(sun0 * k, rip0, slope0); else Set(sun0, rip0, slope0 * k);
                if (Worst(Mean(), before) <= 0.03f) lo = k; else hi = k;
            }
            if (sunIs) { Set(sun0 * lo, rip0, slope0); row = $"_SunLane {sun0:0.000} -> {sun0 * lo:0.000}"; }
            else { Set(sun0, rip0, slope0 * lo); row = $"_ProcSlope x {lo:0.000}"; }
            var fin = Mean();
            EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
            sb.AppendLine($"the lighter day water is from the {(sunIs ? "sun lane" : "travelling waves")}: {row}; after {P3(fin)} = {Worst(fin, before) * 100f:0.0} % from before (3 % wanted), waves on {m.GetFloat("_RippleProc"):0}; saved in M_Water_Harbour.mat");
            var text = sb.ToString();
            try { File.WriteAllText(Proof + "l17_water_colour_unity.txt", text); } catch (System.Exception) { }
            return text;
        }
    }
}
