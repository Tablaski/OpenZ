#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit level18_containers: the parity row DV201 (level 18's shipping containers in the water, destructible), measured by the counted
    /// check (OzV15L18ContainersCount) on level 18's own map. The row is level 18's only: the other levels, the bot matches and the parity's other
    /// probes are as before (the parity report's Zod rows and every other row's probe hold it off).</summary>
    public static partial class OpenZParity
    {
        static void AddL18ContainersDeviations(List<Deviation> list)
        {
            string map = null;
            try { map = Resources.Load<TextAsset>("OpenZ/Maps/L18_grid")?.text; } catch (Exception) { }
            string cell; bool pass;
            if (string.IsNullOrEmpty(map)) { cell = "no level 18 map (Resources/OpenZ/Maps/L18_grid)"; pass = false; }
            else
            {
                try { string r; using (OzV11Kit.GameSwitches()) r = OzV15L18ContainersCount.Run(SettingsText, map, "DV201"); cell = OzV11Kit.VerdictCell(r, out pass) + " (the whole check: menu OpenZ/Checks/V1.5/L18 containers (no Play))"; }
                catch (Exception ex) { cell = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            }
            list.Add(new Deviation
            {
                id = "DV201", change = "level 18: seven rusty shipping containers half sunk and tilted in the water, destructible",
                zod = "Z's level 18 has open shallow water in its canals and pools",
                openz = "each a neutral wreck map item exactly as the Car Park's harbour wrecks (DV39): Zod's map-object hit points, explosives only, hurt by splash, its 3 (the leaning pair's west one 6) shallow-water tiles a destroyable impassable until it is destroyed (robots and the amphibious Bizon wade round it, it bars a line of fire like a rock), then every tile open again; not crushable; made after every other object (every older id kept); places: no lane's keep-clear area, 3+ tiles off a bridge span's side and 5+ past its ends, 2+ from flags, buildings and units; no region split, the fort-to-fort wade the same, the ways between flags, gates and a 9-tile lattice at most 1 tile longer",
                measured = cell, toggle = "ZWorld.L18ContainersDeviation (View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off); the view draws a container only while the rule has it (View/OzL18Containers.cs)",
                status = pass ? DevActive : DevCheck,
            });
        }

        internal static (string text, int active) L18ContainersRowsForBatch()
        {
            var list = new List<Deviation>();
            AddL18ContainersDeviations(list);
            var sb = new System.Text.StringBuilder(); int act = 0;
            foreach (var d in list) { if (d.status == DevActive) act++; sb.AppendLine($"{d.id} row: {(d.status == DevActive ? "ACTIVE" : "CHECK")}; measured: {d.measured}"); }
            return (sb.ToString(), act);
        }
    }

    public static class OzV15L18ContainersMenu
    {
        [MenuItem("OpenZ/Checks/V1.5/L18 containers (no Play)")]
        static void Check()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] L18 containers: leave Play mode first (the check sets the simulation's static switches)"); return; }
            Debug.Log("[OpenZ V1.5] L18 containers (DV201): " + Report());
        }

        static string Report()
        {
            var st = Resources.Load<TextAsset>("OpenZ/default_settings"); var map = Resources.Load<TextAsset>("OpenZ/Maps/L18_grid");
            if (!st || !map) return "FAIL  no settings or no level 18 map in Resources";
            string r; using (OzV11Kit.GameSwitches()) r = OzV15L18ContainersCount.Run(st.text, map.text, "all");
            int p = 0, f = 0; foreach (var l in r.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
            return $"{p} PASS, {f} FAIL\n{r}";
        }

        /// <summary>The kit's imported files as the editor sees them: the four models (three LOD meshes each, the triangles), the two textures
        /// (sRGB, mipmaps), the placement file (seven entries matching the rule's table).</summary>
        static string Imports(out bool ok)
        {
            ok = true; var sb = new System.Text.StringBuilder();
            foreach (var v in new[] { "A", "B", "C", "W" })
            {
                string p = $"Assets/OpenZ/Models/L18/Ruins/L18C_Container_{v}.fbx";
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                var mfs = go ? go.GetComponentsInChildren<MeshFilter>(true) : new MeshFilter[0];
                var names = string.Join(",", mfs.Select(m => m.name + "=" + (m.sharedMesh ? m.sharedMesh.triangles.Length / 3 : 0)));
                var b = mfs.Length > 0 && mfs[0].sharedMesh ? mfs[0].sharedMesh.bounds : default;
                bool good = go && mfs.Length == 3 && go.GetComponentsInChildren<Collider>(true).Length == 0 && mfs.All(m => m.sharedMesh && m.sharedMesh.triangles.Length / 3 >= 200);
                ok &= good; sb.AppendLine($"IMPORT {(good ? "PASS" : "FAIL")} {p}: {names}; LOD0 bounds size {b.size}");
            }
            foreach (var c in new[] { "Red", "Blue" })
            {
                string p = $"Assets/OpenZ/Textures/L18/Ruins/L18C_Container_{c}_BaseColor.png";
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p); var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                bool good = t && ti && ti.sRGBTexture && ti.mipmapEnabled && t.width >= 2048;
                ok &= good; sb.AppendLine($"IMPORT {(good ? "PASS" : "FAIL")} {p}: {(t ? t.width + " x " + t.height : "missing")}, sRGB {ti?.sRGBTexture}, mips {ti?.mipmapEnabled}");
            }
            var doc = OzL18ContainersMenu.Load();
            bool same = doc?.containers != null && doc.containers.Length == ZWorld.L18ContainerIds.Length && doc.containers.All(it =>
            {
                int k = Array.IndexOf(ZWorld.L18ContainerIds, it.id); var t = ZWorld.L18ContainerTiles;
                return k >= 0 && it.tile != null && it.tile.Length == 4 && it.tile[0] == t[k * 4] && it.tile[1] == t[k * 4 + 1] && it.tile[2] == t[k * 4 + 2] && it.tile[3] == t[k * 4 + 3];
            });
            ok &= same; sb.AppendLine($"IMPORT {(same ? "PASS" : "FAIL")} the placement file: {doc?.containers?.Length ?? 0} entries, every footprint the rule's own");
            return sb.ToString();
        }

        /// <summary>Batch (the kit's copy proof): -executeMethod OpenZ.EditorTools.OzV15L18ContainersMenu.BatchProof: the imports, the counted check and
        /// the parity row into Logs/level18_containers_batch.txt; exit 0 = every line PASS and the row ACTIVE ("BATCH PROOF OK").</summary>
        public static void BatchProof()
        {
            int code = 1; var sb = new System.Text.StringBuilder();
            try
            {
                string im = Imports(out bool imOk); sb.AppendLine(im);
                string r = Report(); sb.AppendLine(r);
                var rows = OpenZParity.L18ContainersRowsForBatch(); sb.AppendLine(rows.text);
                bool ok = imOk && r.Contains(" PASS, 0 FAIL") && !r.Contains("FAIL  ") && rows.active == 1;
                sb.AppendLine(ok ? "BATCH PROOF OK" : "BATCH PROOF FAILED");
                code = ok ? 0 : 1;
            }
            catch (Exception ex) { sb.AppendLine("EXCEPTION " + ex); }
            File.WriteAllText(Path.Combine(Application.dataPath, "../Logs/level18_containers_batch.txt"), sb.ToString());
            Debug.Log("[OpenZ V1.5] L18 containers batch: " + sb);
            EditorApplication.Exit(code);
        }
    }
}
#endif
