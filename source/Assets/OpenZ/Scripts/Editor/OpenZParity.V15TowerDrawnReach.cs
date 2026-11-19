#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit tower_reach_dv208: the parity row DV208 "the reach to a fort tower gun is measured to where its tower is drawn"
    /// (Sim/ZObject.TowerDrawnReach.cs), the menu of its counted check (OzV15TowerReachCount) and the Play check against the fort models.</summary>
    public static partial class OpenZParity
    {
        static void AddTowerDrawnReachDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15TowerReachCount.Quick(SettingsText, MapText) + "\n" + OzV15TowerFairCount.Quick(SettingsText, MapText);   // 1.5 round 6 (kit tower_reach_fair_r6): + one distance both ways
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole checks: menus OpenZ/Checks/V1.5/Tower reach to the drawn tower (no Play) and OpenZ/Checks/V1.5/Tower reach both ways (no Play))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV208", change = "The reach between a unit and a fort tower gun is measured to where its tower is drawn, ONE distance both ways (every attacker and the tower gun itself, every fort, both sides)",
                zod = "a fort's tower guns stand on its four turret spots, fort-local px (16,0) (112,0) (16,48) (112,48) (bfort.cpp:534-548), and range is measured centre to centre (points_within_distance, zobject.cpp:4760-4765)",
                openz = "the view draws each tower gun on its corner tower of the fort model, 56 px east / west and 27-30 px (north row) or 74-77 px (south row) south of its rule spot; a weapon's range at a fort tower gun is measured to that drawn tower (DV204's 24 px body round it) when it is nearer than the rule centre, else to the rule centre as before; " +
                        "round 6 (kit tower_reach_fair_r6): the tower gun's own range at a unit is measured by the SAME distance (from its drawn tower when that is nearer), so it answers every unit that hits it from inside its own reach, and a weapon with a shorter table range never outreaches it through DV204's body; " +
                        "the gun's spot, aim and damage unchanged (Sim/ZObject.TowerDrawnReach.cs)",
                measured = measured,
                toggle = "ZWorld.TowerDrawnReachDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows and probes hold it off)",
                status = !pass ? DevCheck : DevActive,
            });
        }

        /// <summary>Batch: only the DV208 row (-executeMethod OpenZ.EditorTools.OpenZParity.TowerDrawnReachRowBatch) into Logs/tower_reach_DV208.txt;
        /// exit 0 = ACTIVE and every line of the whole check PASS.</summary>
        public static void TowerDrawnReachRowBatch()
        {
            int code = 1;
            try
            {
                var list = new List<Deviation>();
                using (ZWorld.TowerDrawnReachOff()) AddTowerDrawnReachDeviation(list);
                string full; using (OzV11Kit.GameSwitches()) full = OzV15TowerReachCount.Run(Resources.Load<TextAsset>("OpenZ/default_settings").text, OzV11Kit.MapText)
                    + "\n" + OzV15TowerFairCount.Run(Resources.Load<TextAsset>("OpenZ/default_settings").text, OzV11Kit.MapText);   // 1.5 round 6 (kit tower_reach_fair_r6): + one distance both ways
                int p = 0, f = 0; foreach (var l in full.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
                var d = list[0];
                string text = $"DV208 row: status {d.status}\nmeasured: {d.measured}\n\nthe whole check: {p} PASS, {f} FAIL\n{full}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/tower_reach_DV208.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = d.status == DevActive && f == 0 ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV208 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }

    public static class OzV15TowerReachMenu
    {
        [MenuItem("OpenZ/Checks/V1.5/Tower reach to the drawn tower (no Play)")]
        static void Count()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] tower reach: leave Play mode first (the check sets the simulation's static switches)"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15TowerReachCount.Run(OzV11Kit.SettingsText, OzV11Kit.MapText);
            int pass = 0, fail = 0; foreach (var l in r.Split('\n')) { if (l.StartsWith("PASS")) pass++; else if (l.StartsWith("FAIL")) fail++; }
            Debug.Log($"[OpenZ V1.5] tower reach to the drawn tower (DV208): {pass} PASS, {fail} FAIL\n{r}");
        }

        /// <summary>1.5 round 6 (kit tower_reach_fair_r6): the counted check of one distance both ways (OzV15TowerFairCount), no Play.</summary>
        [MenuItem("OpenZ/Checks/V1.5/Tower reach both ways (no Play)")]
        static void FairCount()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.5] tower reach both ways: leave Play mode first (the check sets the simulation's static switches)"); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15TowerFairCount.Run(OzV11Kit.SettingsText, OzV11Kit.MapText);
            int pass = 0, fail = 0; foreach (var l in r.Split('\n')) { if (l.StartsWith("PASS")) pass++; else if (l.StartsWith("FAIL")) fail++; }
            Debug.Log($"[OpenZ V1.5] tower reach both ways (DV208 round 6): {pass} PASS, {fail} FAIL\n{r}");
        }

        /// <summary>1.5 round 6 (kit tower_reach_fair_r6), in Play, a level open (also inside an owner-film take: f,MN,(this menu)): every
        /// living fort tower gun against every living enemy unit with a weapon within 400 px of it: the distance (to the drawn tower and the
        /// rule centre), each one's range at the other, who reaches whom, who fires now (WithinAttackRadius, the line of sight too). FAIL: a
        /// unit whose table range is shorter than the gun's reaches it while the gun does not reach the unit back. Reads only, changes nothing.</summary>
        [MenuItem("OpenZ/Checks/V1.5/Tower reach both ways: live pairs (Play)")]
        static void FairPlay()
        {
            var g = ZGame.I;
            if (!Application.isPlaying || g == null || g.World == null) { Debug.LogWarning("[OpenZ V1.5] tower reach both ways Play check: start a level in Play first"); return; }
            var sb = new StringBuilder(); int pass = 0, fail = 0, guns = 0;
            foreach (var c in g.World.Objects)
            {
                if (!c.IsTowerGun || c.Destroyed || c.KillAt >= 0) continue;
                guns++;
                bool has = c.TowerDrawnAt(out int tx, out int ty);
                sb.AppendLine($"{c.Type}#{c.Id} {c.Owner}: rule centre ({c.CenterX},{c.CenterY}), drawn tower {(has ? $"({tx},{ty})" : "none")}, ring {c.RingRadius} px, crewed {c.Drivers.Count > 0}, target {(c.AttackObject == null ? "-" : c.AttackObject.Type + "#" + c.AttackObject.Id)}; DV208 {(ZWorld.TowerDrawnReachDeviation ? "on" : "off")}");
                foreach (var u in g.World.Objects)
                {
                    if (!u.IsUnit || u.Destroyed || u.KillAt >= 0 || u.AttackRadius <= 0 || u.IsTowerGun || u.Owner == c.Owner || u.Owner == Team.Neutral) continue;
                    double dc = Math.Sqrt((double)(u.CenterX - c.CenterX) * (u.CenterX - c.CenterX) + (double)(u.CenterY - c.CenterY) * (u.CenterY - c.CenterY));
                    double dd = Math.Sqrt((double)(u.CenterX - tx) * (u.CenterX - tx) + (double)(u.CenterY - ty) * (u.CenterY - ty));
                    if (Math.Min(dc, dd) > 400) continue;
                    int ru = u.RangeTo(c), rc = c.RangeTo(u);
                    bool uIn = dc <= ru, cIn = dc <= rc, bad = u.AttackRadius < c.AttackRadius && uIn && !cIn;
                    if (bad) fail++; else pass++;
                    sb.AppendLine($"  {(bad ? "FAIL" : "PASS")}  {u.Type}#{u.Id} ({u.CenterX},{u.CenterY}): {dd:0} px from the drawn tower, {dc:0} from the rule centre; its range at the gun {ru} (table {u.AttackRadius}), the gun's at it {rc} (table {c.AttackRadius}); in reach: unit {(uIn ? "yes" : "no")}, gun {(cIn ? "yes" : "no")}; fires now: unit {u.WithinAttackRadius(c)}, gun {c.WithinAttackRadius(u)}");
                }
            }
            Debug.Log($"[OpenZ V1.5] tower reach both ways, live pairs: {guns} tower guns, {pass} PASS, {fail} FAIL\n{sb}");
        }

        /// <summary>In Play, the level open: every fort tower gun of the match, the rules' drawn tower (DV208) against the pad of the fort
        /// MODEL in the scene (TowerMounts.PadForSpot / PadWorld on the fort's view) and the gun model's root; PASS within 2 px (0.25 m).</summary>
        [MenuItem("OpenZ/Checks/V1.5/Tower reach: drawn towers of this level (Play)")]
        static void PlayCheck()
        {
            var g = ZGame.I;
            if (!Application.isPlaying || g == null || g.World == null) { Debug.LogWarning("[OpenZ V1.5] tower reach Play check: start a level in Play first"); return; }
            var sb = new StringBuilder(); int pass = 0, fail = 0;
            foreach (var c in g.World.Objects)
            {
                if (!c.IsTowerGun || c.Destroyed) continue;
                var fort = TowerMounts.FortOf(g.World, c);
                if (fort == null || !g.StaticViews.TryGetValue(fort.Id, out var go) || !go) { fail++; sb.AppendLine($"FAIL  {c.Type}#{c.Id} ({c.LocX},{c.LocY}): no fort model found"); continue; }
                int pad = TowerMounts.PadForSpot(go.transform, c.LocX - fort.LocX, c.LocY - fort.LocY);
                var pw = TowerMounts.PadWorld(go.transform, pad);
                double vx = pw.x * 8, vy = -pw.z * 8;
                bool has = c.TowerDrawnAt(out int x, out int y);
                double e = Math.Sqrt((x - vx) * (x - vx) + (y - vy) * (y - vy));
                string model = g.Views.TryGetValue(c.Id, out var v) && v ? $", gun model root ({v.transform.position.x * 8:0.0},{-v.transform.position.z * 8:0.0})" : "";
                bool ok = has && e <= 2.0; if (ok) pass++; else fail++;
                sb.AppendLine($"{(ok ? "PASS" : "FAIL")}  {fort.Type} {fort.Owner} spot ({c.LocX - fort.LocX},{c.LocY - fort.LocY}) {c.Type}#{c.Id}: rule centre ({c.CenterX},{c.CenterY}), rules' drawn tower {(has ? $"({x},{y})" : "none")}, fort model's pad ({vx:0.0},{vy:0.0}) = {e:0.0} px{model}; DV208 switch {(ZWorld.TowerDrawnReachDeviation ? "on" : "off")}");
            }
            Debug.Log($"[OpenZ V1.5] tower reach Play check: {pass} PASS, {fail} FAIL\n{sb}");
        }
    }
}
#endif
