#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The counted check of View/OzPlayEdge.cs and View/AttackRangeRings.PlayEdge.cs.
    /// Menu OpenZ/Checks/V1.3/Range ring at the map edge:
    ///  * Edit mode (no Play), every level's grid in Resources/OpenZ/Maps: E1 the tiles add up, the play area is the map's bulk, no building,
    ///    unit or flag outside; E2 all four sides and four corners end in the outside, off the map counts; E3 the distance against a brute
    ///    force count; E4 rock inside the play area is kept; E5 the line follows the grid (a tile opened or closed moves it); E6 no level
    ///    is named in the code, and only the ring reads it; E7 Build's hooks; E8 the other audit keeps its ring whole.
    ///  * Play in a level: AttackRangeRings.AuditEdge, the true ring on the true ground. Run it on L02, L07, L16, L17.
    /// </summary>
    public static class OzV13RingEdgeCheck
    {
        const string Tag = "[OpenZ V1.3 ring edge]";
        public static string Report = "not run";
        static string Dir => OzRelease.Milestones("v1_3_20261030/range_ring_edge");

        static int ok, bad; static readonly StringBuilder notes = new StringBuilder();
        static void Begin() { ok = 0; bad = 0; notes.Length = 0; }
        static void Check(bool pass, string what) { if (pass) ok++; else bad++; notes.Append(pass ? "\n  [ok]   " : "\n  [FAIL] ").Append(what); }
        static void End(string part)
        {
            Report = $"{Tag} {part}: {(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad}{notes}";
            if (bad == 0) Debug.Log(Report); else Debug.LogError(Report);
            try { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, part.Replace(' ', '_') + ".txt"), Report + "\n"); }
            catch (Exception e) { Debug.LogWarning(Tag + " could not write the report: " + e.Message); }
        }

        static string Src(string rel) { var p = Path.Combine(Application.dataPath, "OpenZ/Scripts", rel); return File.Exists(p) ? File.ReadAllText(p) : ""; }
        static int Count(string t, string w) { int n = 0; for (int i = t.IndexOf(w, StringComparison.Ordinal); i >= 0; i = t.IndexOf(w, i + w.Length, StringComparison.Ordinal)) n++; return n; }

        [MenuItem("OpenZ/Checks/V1.3/Range ring at the map edge (Edit: every level's line; Play: the ring in this level)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { EditCheck(); return; }
            Begin();
            string s = AttackRangeRings.AuditEdge(out int v);
            foreach (var line in s.Split('\n'))
            {
                if (line.StartsWith("OK   ", StringComparison.Ordinal)) Check(true, line.Substring(5));
                else if (line.StartsWith("FAIL ", StringComparison.Ordinal)) Check(false, line.Substring(5));
                else if (line.Length > 0) notes.Append("\n  [info] ").Append(line);
            }
            Check(v == 0, "the audit's own count of bad lines: " + v);
            string level = ZGame.I && ZGame.I.World != null && ZGame.I.World.Map != null ? ZGame.I.World.Map.file : "no level";
            End("play " + Path.GetFileNameWithoutExtension(level));
        }

        // ------------------------------------------------------------------ edit mode
        static float Brute(OzPlayEdge e, float x, float z)
        {
            int tx = Mathf.FloorToInt(x / 2f), ty = Mathf.FloorToInt(-z / 2f);
            if (e.OutAt(tx, ty)) return 0f;
            float best = OzPlayEdge.Reach;
            for (int b = ty - 3; b <= ty + 3; b++)
                for (int a = tx - 3; a <= tx + 3; a++)
                {
                    if (!e.OutAt(a, b)) continue;
                    float dx = Mathf.Max(Mathf.Max(a * 2f - x, 0f), x - (a * 2f + 2f)), dy = Mathf.Max(Mathf.Max(b * 2f + z, 0f), -z - (b * 2f + 2f));
                    best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dy * dy));
                }
            return best;
        }

        static void EditCheck()
        {
            Begin();
            var all = new List<(string name, ZMapData m, OzPlayEdge e)>();
            foreach (var t in Resources.LoadAll<TextAsset>("OpenZ/Maps"))
            {
                if (!t.name.EndsWith("_grid", StringComparison.Ordinal)) continue;
                var m = ZMapData.FromJson(t.text);
                all.Add((t.name, m, new OzPlayEdge(m.width, m.height, m.classes, m.lava)));
            }
            Check(all.Count >= 5, $"E0 every level's grid is read: {all.Count} grids");
            foreach (var (name, m, e) in all)
            {
                // E1
                int inOut = 0, things = 0;
                foreach (var o in m.objects)
                {
                    if (o.Category == "map_item") continue;
                    things++; if (e.OutAt(o.x, o.y)) inOut++;
                }
                Check(e.PlayTiles + e.OuterTiles + e.KeptTiles == m.width * m.height && e.PlayTiles * 2 > m.width * m.height && inOut == 0,
                      $"E1 {name}: {e.PlayTiles} play + {e.OuterTiles} outside + {e.KeptTiles} kept = {m.width} x {m.height}; {things} buildings, units and flags, {inOut} outside");
                // E2 walking in from each side: off the map is outside, then the first play tile, the distance at 0.5 m past the line
                int offBad = 0, walkBad = 0, walks = 0;
                for (int i = 0; i < 40; i++)
                {
                    int cx = (i * m.width) / 40, cy = (i * m.height) / 40;
                    if (!e.OutsideAt(-1f, -(cy * 2f + 1f)) || !e.OutsideAt(m.width * 2f + 1f, -(cy * 2f + 1f)) || !e.OutsideAt(cx * 2f + 1f, 1f) || !e.OutsideAt(cx * 2f + 1f, -(m.height * 2f + 1f))) offBad++;
                    for (int side = 0; side < 4; side++)
                    {
                        int steps = side < 2 ? m.width : m.height, x = 0, y = 0, s = 0;
                        for (; s < steps; s++)
                        {
                            x = side == 0 ? s : side == 1 ? m.width - 1 - s : cx; y = side == 2 ? s : side == 3 ? m.height - 1 - s : cy;
                            if (!e.OutAt(x, y)) break;
                        }
                        if (s == steps) continue;
                        walks++;
                        float wx = side == 0 ? x * 2f + 0.5f : side == 1 ? x * 2f + 1.5f : x * 2f + 1f, wz = -(side == 2 ? y * 2f + 0.5f : side == 3 ? y * 2f + 1.5f : y * 2f + 1f);
                        if (e.Dist(wx, wz) > 0.5f + 1e-3f) walkBad++;
                    }
                }
                bool corners = e.OutsideAt(-1f, 1f) && e.OutsideAt(m.width * 2f + 1f, 1f) && e.OutsideAt(-1f, -(m.height * 2f + 1f)) && e.OutsideAt(m.width * 2f + 1f, -(m.height * 2f + 1f));
                Check(offBad == 0 && walkBad == 0 && corners, $"E2 {name}: four sides and corners off the map are outside ({offBad} wrong); {walks} walks in from a side stop at the line, 0.5 m past it the distance is 0.5 m at most ({walkBad} wrong)");
                // E3 the distance
                var rnd = new System.Random(36); int wrong = 0; float worst = 0f;
                for (int i = 0; i < 40000; i++)
                {
                    float x = (float)(rnd.NextDouble() * (m.width * 2f + 8f) - 4f), z = -(float)(rnd.NextDouble() * (m.height * 2f + 8f) - 4f);
                    float d = Mathf.Abs(e.Dist(x, z) - Brute(e, x, z));
                    if (d > worst) worst = d;
                    if (d > 1e-3f) wrong++;
                }
                Check(wrong == 0, $"E3 {name}: 40000 points against a brute force count: {wrong} differ, worst {worst:0.00000} m");
            }
            // E4
            foreach (var want in new[] { "L02_grid", "L16_grid", "L17_grid" })
            {
                var row = all.Find(r => r.name == want);
                Check(row.e != null && row.e.KeptTiles > 0, $"E4 {want}: rock inside the play area is kept ({(row.e != null ? row.e.KeptTiles : -1)} tiles)");
            }
            // E5 the line follows the grid: the level's own tiles, on a copy
            var l7 = all.Find(r => r.name == "L07_grid");
            if (l7.m != null)
            {
                var c = (int[])l7.m.classes.Clone(); int w = l7.m.width, h = l7.m.height, opened = 0;
                for (int y = 5; y < h - 5; y++) for (int x = 1; x < w - 1; x++) if (l7.e.OutAt(x, y)) { c[y * w + x] = 0; opened++; }
                var e2 = new OzPlayEdge(w, h, c, l7.m.lava);
                Check(opened > 0 && e2.PlayTiles > l7.e.PlayTiles && e2.OuterTiles < l7.e.OuterTiles,$"E5 a level's border opened in its grid ({opened} tiles): play {l7.e.PlayTiles} to {e2.PlayTiles}, outside {l7.e.OuterTiles} to {e2.OuterTiles}, no code change");
            }
            // E6
            string core = Src("View/OzPlayEdge.cs"), ring = Src("View/AttackRangeRings.PlayEdge.cs"), main = Src("View/AttackRangeRings.cs"), place = Src("View/AttackRangeRings.Placement.cs");
            bool named = false;
            foreach (var lv in new[] { "L02", "L07", "L16", "L17" }) if (Count(core, "\"" + lv) > 0 || Count(ring, "\"" + lv) > 0) named = true;
            Check(core.Length > 0 && ring.Length > 0 && !named, "E6a no level is named in the code");
            int simRefs = 0; var readers = new List<string>();
            foreach (var f in Directory.GetFiles(Path.Combine(Application.dataPath, "OpenZ/Scripts"), "*.cs", SearchOption.AllDirectories))
            {
                var t = File.ReadAllText(f); bool ed = f.Replace('\\', '/').IndexOf("/Editor/", StringComparison.Ordinal) >= 0;
                if (ed || !(t.Contains("OzPlayEdge") || t.Contains("edgeClip"))) continue;
                readers.Add(Path.GetFileName(f));
                if (f.Replace('\\', '/').IndexOf("/Sim/", StringComparison.Ordinal) >= 0) simRefs++;
            }
            readers.Sort(StringComparer.Ordinal);
            Check(simRefs == 0 && string.Join(",", readers) == "AttackRangeRings.Placement.cs,AttackRangeRings.PlayEdge.cs,AttackRangeRings.cs,OzPlayEdge.cs",
                  $"E6b view only: no rules file reads it ({simRefs}); in the game's code only the ring: {string.Join(", ", readers)}");
            // E7
            Check(Count(main, "if (edgeClip) EdgeHeights(r);") == 1 && Count(main, "if (edgeClip && a != 0) a = EdgeAlpha(a, p.x, p.z);") == 1 && Count(main, "if (rings.Count == 0) return;") == 1,
                  "E7a Build has the two hooks; with no ring LateUpdate stops before Build, and the line is made only inside Build");
            int a0 = ring.IndexOf("byte EdgeAlpha(", StringComparison.Ordinal), a1 = ring.IndexOf("void EdgeHeights(", StringComparison.Ordinal), a2 = ring.IndexOf("// ------------------------------------------------------------------ audit", StringComparison.Ordinal);
            string perFrame = a0 >= 0 && a1 > a0 ? ring.Substring(a0, a1 - a0) : "x new ", heights = a1 >= 0 && a2 > a1 ? ring.Substring(a1, a2 - a1) : "new new";
            Check(Count(perFrame, "new ") == 0 && Count(heights, "new ") == 1 && Count(heights, "edgeHid == null || edgeHid.Length < g") == 1, "E7b the per-frame code allocates nothing: EdgeAlpha no new, EdgeHeights only its one reused array");
            // E8
            Check(Count(place, "z.edgeClip = false;") == 1 && Count(place, "z.edgeClip = clip;") == 1, "E8 AuditGuns measures the ring's shape with the rule off (a hidden dot is not a different dot)");
            End("edit");
        }
    }
}
#endif
