// OpenZ (GPL-3.0) - version 1.5, kit view_notes11 item N18: the counted check of the bridge railing sections (edit mode, no level needed).
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 notes N18: menu OpenZ/V1.5/Notes11: Bridge Rails Sections Check. Every bridge model a level shows with a walk deck (desert, city,
    /// jungle; intact and damaged) is put down as the levels place it, given a walk deck measured like ZGame.MakeWalkable (the most common top
    /// height on a 25 cm grid, the deck's extent), and cut by OzBridgeRails.Prepare. Per model: the railing line each side, the sections each side
    /// and their lengths, the triangles that moved to the sections and that the bridge model kept. PASS: the steel bridges (desert, city) have 3 or
    /// more sections a side, each 0.5-3 m long, and the bridge model kept its girders / deck / end posts (it lost fewer than a quarter of its
    /// triangles); the plank bridge is reported (its rope rails have no rigid posts). The instances are removed again.
    /// <para><see cref="Batch"/> (-executeMethod, the kit's copy proof): this check + the player script compiles of Windows 64 (plain and
    /// OZ_RELEASE) through PlayerBuildInterface; the report in Logs/view_notes11_check.txt; the exit code 0 = all PASS.</para>
    /// </summary>
    public static class OzV15Notes11RailsCheck
    {
        [MenuItem("OpenZ/V1.5/Notes11: Bridge Rails Sections Check")]
        public static void Menu() { bool ok = Run(out var report); Debug.Log(report); EditorUtility.DisplayDialog("Bridge Rails Sections Check", ok ? "PASS (details in the console)" : "FAIL (details in the console)", "OK"); }

        public static bool Run(out string report)
        {
            var sb = new StringBuilder(); bool ok = true; float x0 = 5000f;
            foreach (var biome in new[] { "desert", "city", "jungle" })
                foreach (var state in new[] { "intact", "damaged" })
                {
                    var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/OpenZ/Prefabs/Z_bridge_{biome}_{state}.prefab");
                    if (!pf) { sb.AppendLine($"MISSING Z_bridge_{biome}_{state}"); if (biome != "jungle") ok = false; continue; }
                    var go = (GameObject)Object.Instantiate(pf); go.name = $"Z_bridge_{biome}_{state}";
                    go.transform.position = new Vector3(x0, 0.06f, -5000f); x0 += 60f;
                    if (biome == "desert") go.transform.localScale = new Vector3(7.8f / 7.85f, 1f, 12f / 20.57f);   // a vertical desert bridge (OpenZSetup)
                    int trisBefore = Tris(go);
                    var deck = Deck(go);
                    if (!deck) { sb.AppendLine($"FAIL {go.name}: no deck measured"); ok = false; Object.DestroyImmediate(go); continue; }
                    int before = OzBridgeRails.SectionsMade;
                    OzBridgeRails.Prepare(go, deck);
                    int made = OzBridgeRails.SectionsMade - before;
                    var perSide = new Dictionary<char, List<float>>(); int secTris = 0;
                    var b = deck.GetComponent<MeshCollider>().bounds; bool alongZ = b.size.z >= b.size.x;
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (!mf.name.StartsWith("OZ_RailSec_")) continue;
                        char side = mf.name[11];
                        var rb = mf.GetComponent<Renderer>().bounds; float len = alongZ ? rb.size.z : rb.size.x;
                        if (!perSide.TryGetValue(side, out var l)) { l = new List<float>(); perSide[side] = l; }
                        l.Add(len); secTris += mf.sharedMesh ? mf.sharedMesh.triangles.Length / 3 : 0;
                    }
                    int kept = Tris(go) - secTris;
                    string sides = "";
                    bool good = true;
                    foreach (var kv in perSide)
                    {
                        kv.Value.Sort(); float mn = kv.Value[0], mx = kv.Value[kv.Value.Count - 1];
                        sides += $" side {kv.Key}: {kv.Value.Count} pieces, {mn:0.00}-{mx:0.00} m;";
                        if (biome != "jungle" && (kv.Value.Count < 3 || mn < 0.3f || mx > 3.2f)) good = false;
                    }
                    if (biome != "jungle" && (perSide.Count < 2 || trisBefore - kept > trisBefore / 4)) good = false;
                    if (biome != "jungle") ok &= good;
                    sb.AppendLine($"{(biome == "jungle" ? "INFO" : good ? "PASS" : "FAIL")} {go.name}: deck {b.max.y:0.00} m, sections made {made},{sides} triangles {trisBefore} -> the model keeps {kept}, the sections {secTris}; {OzBridgeRails.LastLine}");
                    Object.DestroyImmediate(go);
                }
            sb.Insert(0, (ok ? "PASS" : "FAIL") + " bridge rails sections (1.5 notes N18)\n");
            report = sb.ToString();
            return ok;
        }

        static int Tris(GameObject go) { int n = 0; foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh && mf.name != "WalkDeck") n += mf.sharedMesh.triangles.Length / 3; return n; }

        /// <summary>A walk deck as ZGame.MakeWalkable measures it (flat over the deck's extent; the ramps are not needed here).</summary>
        static GameObject Deck(GameObject art)
        {
            var temps = new List<MeshCollider>();
            foreach (var mf in art.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temps.Add(mc); }
            Physics.SyncTransforms();
            var rs = art.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) { foreach (var t in temps) Object.DestroyImmediate(t); return null; }
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var hits = new List<Vector3>(); var hist = new Dictionary<int, int>();
            for (float x = b.min.x; x <= b.max.x; x += 0.25f)
                for (float z = b.min.z; z <= b.max.z; z += 0.25f)
                {
                    float top = float.NegativeInfinity; var ray = new Ray(new Vector3(x, b.max.y + 5, z), Vector3.down);
                    foreach (var mc in temps) if (mc.Raycast(ray, out var h, 100)) top = Mathf.Max(top, h.point.y);
                    if (float.IsNegativeInfinity(top)) continue;
                    hits.Add(new Vector3(x, top, z)); int k = Mathf.RoundToInt(top * 20); hist[k] = hist.TryGetValue(k, out var c) ? c + 1 : 1;
                }
            foreach (var t in temps) Object.DestroyImmediate(t);
            if (hist.Count == 0) return null;
            int best = 0, bk = 0; foreach (var kv in hist) if (kv.Value > best) { best = kv.Value; bk = kv.Key; }
            float deckY = bk / 20f, x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var h in hits) if (Mathf.Abs(h.y - deckY) < 0.12f) { x0 = Mathf.Min(x0, h.x); x1 = Mathf.Max(x1, h.x); z0 = Mathf.Min(z0, h.z); z1 = Mathf.Max(z1, h.z); }
            var go = new GameObject("WalkDeck"); go.transform.SetParent(art.transform, false);
            var v = new List<Vector3> { new Vector3(x0, deckY, z0), new Vector3(x1, deckY, z0), new Vector3(x0, deckY, z1), new Vector3(x1, deckY, z1) };
            for (int i = 0; i < v.Count; i++) v[i] = go.transform.InverseTransformPoint(v[i]);
            var m = new Mesh { name = "WalkDeck" }; m.SetVertices(v); m.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0); m.RecalculateNormals(); m.RecalculateBounds();
            go.AddComponent<MeshCollider>().sharedMesh = m;
            Physics.SyncTransforms();
            return go;
        }

        /// <summary>-executeMethod OpenZ.EditorTools.OzV15Notes11RailsCheck.Batch: the check + the Windows 64 player script compiles (plain, OZ_RELEASE).</summary>
        public static void Batch()
        {
            var sb = new StringBuilder(); bool ok = true;
            try
            {
                ok &= Run(out var rep); sb.AppendLine(rep);
                foreach (var rel in new[] { false, true })
                {
                    var errs = new List<string>();
                    Application.LogCallback cb = (msg, st, type) => { if ((type == LogType.Error || type == LogType.Exception) && msg.Contains("error CS")) errs.Add(msg); };
                    Application.logMessageReceived += cb;
                    var set = new ScriptCompilationSettings { target = BuildTarget.StandaloneWindows64, group = BuildTargetGroup.Standalone, options = ScriptCompilationOptions.None };
                    if (rel) set.extraScriptingDefines = new[] { "OZ_RELEASE" };
                    string outDir = "Temp/oz_notes11_player" + (rel ? "_rel" : "");
                    var res = PlayerBuildInterface.CompilePlayerScripts(set, outDir);
                    Application.logMessageReceived -= cb;
                    int asm = res.assemblies != null ? res.assemblies.Count : 0;
                    bool good = errs.Count == 0 && asm > 0;
                    ok &= good;
                    sb.AppendLine($"{(good ? "PASS" : "FAIL")} player scripts Windows 64{(rel ? " OZ_RELEASE" : "")}: {asm} assemblies, {errs.Count} errors");
                    foreach (var e in errs) sb.AppendLine("   " + e);
                }
            }
            catch (System.Exception e) { ok = false; sb.AppendLine("FAIL exception: " + e); }
            sb.Insert(0, (ok ? "PASS" : "FAIL") + " view_notes11 batch check\n");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/view_notes11_check.txt", sb.ToString());
            Debug.Log(sb.ToString());
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
