// OpenZ (GPL-3.0) - version 1.3, level 7 (LV_CONTRACT.md, kit LV3): the counted Play check of walking on lava. Editor only, never in a build.
using System.Collections.Generic;
using System.Linq;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Run in Play on L07, rule LAVA BURNS ROBOTS on. Puts one robot of each type (grunt, psycho, sniper, tough) on the bank of the deepest lake
    /// and orders it in; measures each one's lava line, leg heat and cool-down; orders them out; then 40 robots for the smoke cap; then a robot
    /// shot dead in lava. Touches the test's own robots only, on a level that is thrown away after. Every line in the
    /// Console starts [LV3]. The other menu runs on any level with no lava: nothing of the kit may exist there.
    /// </summary>
    public static class OzLavaCheckLV3
    {
        static readonly string[] Types = { "grunt", "psycho", "sniper", "tough" };
        const float LineTol = 0.08f, HeatTol = 0.05f;

        sealed class Case { public string type; public ZObject o; public UnitView v; public float inAt = -1f, outAt = -1f, heatAt15 = -1f, lineErr = 99f, h; public bool ordered, cooled; public float coolT = -1f; }
        static readonly List<Case> cases = new List<Case>();
        static int pass, fail, stage; static float t0; static int lx, ly, sx, sy; static List<ZObject> crowd = new List<ZObject>();
        static int puffs0, falls0, prints0, wake0;

        static void V(bool ok, string what) { if (ok) pass++; else fail++; Debug.Log("[LV3] " + (ok ? "PASS  " : "FAIL  ") + what); }

        [MenuItem("OpenZ/L07 Lava/Checks/LV3 walking on lava (Play, on L07)")]
        static void Run()
        {
            var g = ZGame.I;
            if (!Application.isPlaying || !g || g.World == null || !g.World.Map.HasLava) { Debug.LogWarning("[LV3] enter Play on L07 first"); return; }
            if (!ZWorld.LavaDeviation) { Debug.LogWarning("[LV3] OPTIONS: LAVA BURNS ROBOTS must be on"); return; }
            pass = fail = stage = 0; cases.Clear(); crowd.Clear();
            var w = g.World;
            var d = Deepest(w.Map); lx = d.x * 16 + 8; ly = d.y * 16 + 8;
            if (!DrySpot(w, d.x, d.y, out sx, out sy)) { V(false, "no dry start spot near the deepest lava tile"); return; }
            Debug.Log("[LV3] test lake at tile " + d.x + ", " + d.y + ", world (" + (lx / 8f).ToString("F0") + ", " + (-ly / 8f).ToString("F0") + "): keep the camera on it, game speed 1, not paused");
            puffs0 = OzLavaWade.SmokePuffs; falls0 = OzLavaWade.FootfallsIn; prints0 = OzLavaWade.Prints; wake0 = OzLavaWake.Uploads;
            foreach (var type in Types)
            {
                var o = OzV11Kit.Place(w, type, sx, sy, g.player, 1); o.MaxHealth = o.Health = 10000000; var view = g.SpawnUnitView(o);     // the measures need it alive on a live level
                OzV11Kit.PlayerMove(w, o, lx, ly, false);
                cases.Add(new Case { type = type, o = o, v = view });
            }
            var cam = Camera.main; if (cam) cam.transform.position = new Vector3(lx / 8f, cam.transform.position.y, -ly / 8f - 35f);
            t0 = Time.time; EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Tick()
        {
            var g = ZGame.I; var wade = OzLavaWade.I;
            if (!Application.isPlaying || !g || !wade) { EditorApplication.update -= Tick; return; }
            float now = Time.time; var w = g.World;
            if (stage == 0)
            {
                bool allOut = true;
                foreach (var c in cases)
                {
                    if (c.o == null || c.o.Destroyed) continue;
                    if (c.inAt < 0f && wade.LegHeatOf(c.v) > 0f) c.inAt = now;
                    if (c.inAt >= 0f && !c.ordered)
                    {
                        if (now - c.inAt >= 1.6f && c.heatAt15 < 0f) c.heatAt15 = wade.LegHeatOf(c.v);
                        if (wade.Probe(c.v, out c.h, out float sink, out float line) && now - c.inAt > 1f && c.o.InLava) c.lineErr = Mathf.Min(c.lineErr, Mathf.Abs(line - OzLava.I.lavaY));
                        if (now - c.inAt >= 1.8f) { c.ordered = true; OzV11Kit.PlayerMove(w, c.o, sx, sy, false); }
                    }
                    if (c.ordered && !c.o.InLava && c.outAt < 0f) c.outAt = now;
                    if (c.outAt >= 0f && !c.cooled && wade.LegHeatOf(c.v) <= 0f) { c.cooled = true; c.coolT = now - c.outAt; }
                    if (!c.cooled) allOut = false;
                }
                if (!allOut && now - t0 < 80f) return;
                foreach (var c in cases)
                {
                    V(c.heatAt15 >= 1f - HeatTol, c.type + ": legs full 1.6 s after stepping in: " + c.heatAt15.ToString("F2"));
                    V(c.lineErr <= LineTol, c.type + ": height " + c.h.ToString("F2") + " m, lava line within " + LineTol + " m of the surface: " + c.lineErr.ToString("F3"));
                    V(c.coolT >= 7f && c.coolT <= 9.5f, c.type + ": cooled in " + c.coolT.ToString("F1") + " s (8 s wanted)");
                }
                V(OzLavaWade.SmokePuffs > puffs0, "smoke puffs " + (OzLavaWade.SmokePuffs - puffs0));
                V(OzLavaWade.FootfallsIn > falls0, "footfalls in lava with steam " + (OzLavaWade.FootfallsIn - falls0));
                V(OzLavaWade.Prints - prints0 >= 4, "hot prints " + (OzLavaWade.Prints - prints0));
                V(OzLavaWake.Uploads > wake0, "wake uploads " + (OzLavaWake.Uploads - wake0));
                OzLavaWade.NearMax = 0;
                for (int i = 0; i < 40; i++) { var o = OzV11Kit.Place(w, Types[i % 4], sx, sy, g.player, 1); g.SpawnUnitView(o); crowd.Add(o); OzV11Kit.PlayerMove(w, o, lx, ly, false); }
                stage = 1; t0 = now; return;
            }
            if (stage == 1)
            {
                if (now - t0 < 10f) return;
                V(OzLavaWade.NearMax > 0 && OzLavaWade.NearMax <= OzLavaWade.SmokeCap, "40 robots: smoke on at most " + OzLavaWade.SmokeCap + " at once: " + OzLavaWade.NearMax + ", in lava " + wade.InLavaCount);
                var victim = crowd.FirstOrDefault(o => o != null && !o.Destroyed && o.InLava);
                if (victim == null) { V(false, "no robot in lava to shoot"); Finish(); return; }
                cases.Clear(); cases.Add(new Case { type = "shot", o = victim, v = g.Views[victim.Id] });
                victim.Health = 1; victim.DamageHealth(1); w.UpdateObjectHealth(victim);
                stage = 2; t0 = now; return;
            }
            if (stage == 2)
            {
                var c = cases[0];
                if (c.v && c.v.LavaMelting) { V(true, "a robot shot dead in lava melts"); Finish(); return; }
                if (now - t0 > 3f) { V(false, "a robot shot dead in lava melts"); Finish(); }
            }
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Debug.Log("[LV3] " + (fail == 0 ? "PASS " : "FAIL ") + pass + "/" + (pass + fail));
        }

        [MenuItem("OpenZ/L07 Lava/Checks/LV3 no trace (Play, on L02 L16 L17)")]
        static void NoTrace()
        {
            var g = ZGame.I;
            if (!Application.isPlaying || !g || g.World == null) { Debug.LogWarning("[LV3] enter Play on L02, L16 or L17 first"); return; }
            bool none = !OzLava.I && !OzLavaWade.I && Object.FindFirstObjectByType<OzLava>() == null;
            Debug.Log("[LV3] " + (none ? "PASS  " : "FAIL  ") + g.level + ": no OzLava, no OzLavaWade");
            Debug.Log("[LV3] " + (!OzLava.SurfaceAt(Vector3.zero, out _) ? "PASS  " : "FAIL  ") + g.level + ": SurfaceAt false");
        }

        static Vector2Int Deepest(ZMapData m)
        {
            int best = -1; var at = Vector2Int.zero;
            for (int y = 0; y < m.height; y++)
                for (int x = 0; x < m.width; x++)
                {
                    if (!m.LavaAt(x, y)) continue;
                    int r = 0; while (r < 12 && Ring(m, x, y, r + 1)) r++;
                    if (r > best) { best = r; at = new Vector2Int(x, y); }
                }
            return at;
        }

        static bool Ring(ZMapData m, int x, int y, int r)
        {
            for (int d = -r; d <= r; d++) if (!m.LavaAt(x + d, y - r) || !m.LavaAt(x + d, y + r) || !m.LavaAt(x - r, y + d) || !m.LavaAt(x + r, y + d)) return false;
            return true;
        }

        static bool DrySpot(ZWorld w, int tx, int ty, out int px, out int py)
        {
            px = py = 0;
            for (int r = 2; r <= 10; r++)
                for (int y = ty - r; y <= ty + r; y++)
                    for (int x = tx - r; x <= tx + r; x++)
                    {
                        if (System.Math.Max(System.Math.Abs(x - tx), System.Math.Abs(y - ty)) != r || w.Map.LavaAt(x, y) || !w.Path.TilePassable(x, y, true)) continue;
                        int cx = x * 16 + 8, cy = y * 16 + 8;
                        if (!w.LavaLineClean(cx, cy, tx * 16 + 8, ty * 16 + 8)) continue;
                        px = cx; py = cy; return true;
                    }
            return false;
        }
    }
}
