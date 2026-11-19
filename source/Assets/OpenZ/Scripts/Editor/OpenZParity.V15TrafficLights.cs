using System;
using System.Linq;
using OpenZ.Sim;
#if !OPENZ_HARNESS
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#endif

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit traffic_lights_53: the counted check of DV209 (Sim/ZWorld.TrafficLightsKerb.cs): level 17 with DV67 on, DV209 on and off.
    /// On: C01 on (43, 98), C02 on (66, 98); off: (41, 98) / (68, 98) as before; every other object of the world the same (id, kind, place);
    /// the same hit points and rules for the two items; the same match twice with DV209 on (two bots, 600 ticks) identical. Plain C# (the
    /// harness builds it too).</summary>
    public static class OzV15TrafficLightsCount
    {
        static string Tiles(ZWorld w, string uid)
        {
            var o = w.DetailProps.FirstOrDefault(p => w.DetailPropUid.TryGetValue(p.Id, out var u) && u == uid);
            if (o == null) return "missing";
            return $"({o.LocX / 16}, {o.LocY / 16}) hp {o.MaxHealth} blocks {(o.BlockTiles == null ? 0 : o.BlockTiles.Length / 2)} explosives only {(o.AttackedByExplosives ? "yes" : "NO")}";
        }

        public static string Run(string settings, Func<string, string> map, out bool pass)
        {
            var sb = new System.Text.StringBuilder();
            bool was67 = ZWorld.DetailPropsDeviation, was209 = ZWorld.TrafficLightsKerbDeviation;
            int fail = 0;
            void Row(bool ok, string s) { sb.AppendLine((ok ? "PASS " : "FAIL ") + s); if (!ok) fail++; }
            try
            {
                ZWorld.DetailPropsDeviation = true;
                ZWorld.TrafficLightsKerbDeviation = true;
                var on = ZWorld.Create(settings, map("L17"), 1, true);
                ZWorld.TrafficLightsKerbDeviation = false;
                var off = ZWorld.Create(settings, map("L17"), 1, true);
                string c1on = Tiles(on, "C01"), c2on = Tiles(on, "C02"), c1off = Tiles(off, "C01"), c2off = Tiles(off, "C02");
                Row(c1on.StartsWith("(43, 98)") && c2on.StartsWith("(66, 98)"), $"DV209 on: C01 {c1on}; C02 {c2on} (want (43, 98) / (66, 98), the kerb)");
                Row(c1off.StartsWith("(41, 98)") && c2off.StartsWith("(68, 98)"), $"DV209 off: C01 {c1off}; C02 {c2off} (want (41, 98) / (68, 98), as before)");
                Row(c1on.Substring(c1on.IndexOf(" hp")) == c1off.Substring(c1off.IndexOf(" hp")) && c2on.Substring(c2on.IndexOf(" hp")) == c2off.Substring(c2off.IndexOf(" hp")),
                    "the same hit points, blocking count and rules on and off");
                Row(on.DetailProps.Count == 10 && off.DetailProps.Count == 10, $"level 17 detail props: {on.DetailProps.Count} on / {off.DetailProps.Count} off (want 10)");
                var moved = new System.Collections.Generic.HashSet<int>(on.DetailProps.Where(p => on.DetailPropUid.TryGetValue(p.Id, out var u) && (u == "C01" || u == "C02")).Select(p => p.Id));
                bool same = on.Objects.Count == off.Objects.Count && on.Objects.Zip(off.Objects, (a, b) => a.Id == b.Id && a.Type == b.Type && (moved.Contains(a.Id) || a.LocX == b.LocX && a.LocY == b.LocY)).All(x => x);
                Row(same, $"every other object identical on and off ({on.Objects.Count} objects, only the 2 light items moved)");
                Row(on.DetailPropBlockedTiles == 0 && off.DetailPropBlockedTiles == 0, $"tiles newly blocked by the props: {on.DetailPropBlockedTiles} on / {off.DetailPropBlockedTiles} off (want 0 / 0: the paths as before)");
                ZWorld.TrafficLightsKerbDeviation = true;
                string Match()
                {
                    var r = ZWorld.Create(settings, map("L17"), 7, true);
                    r.Bots.Add(new ZBot(r, Team.Red, 7)); r.Bots.Add(new ZBot(r, Team.Blue, 108));
                    for (int i = 0; i < 600; i++) r.Tick(ZConst.TICK);
                    return string.Join(";", r.Objects.Select(o => $"{o.Id}:{o.LocX},{o.LocY},{o.Health},{(o.Destroyed ? 1 : 0)}"));
                }
                string m1 = Match(), m2 = Match();
                Row(m1 == m2, $"determinism: level 17, DV209 on, two bots, 600 ticks, the same match twice {(m1 == m2 ? "identical" : "DIFFERENT")} ({m1.Length} chars)");
            }
            catch (Exception ex) { Row(false, "the check threw " + ex.GetType().Name + ": " + ex.Message); }
            finally { ZWorld.DetailPropsDeviation = was67; ZWorld.TrafficLightsKerbDeviation = was209; }
            pass = fail == 0;
            return $"{(pass ? "PASS" : "FAIL")} DV209 check: {fail} fault(s)\n" + sb;
        }
    }

#if !OPENZ_HARNESS
    /// <summary>1.5 kit traffic_lights_53: the parity row DV209 "level 17's traffic lights stand at the kerb" and its batch.</summary>
    public static partial class OpenZParity
    {
        static void AddTrafficLightsKerbDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15TrafficLightsCount.Run(SettingsText, MapText, out pass);
                measured = report.Replace("\n", " | ").Trim(' ', '|');
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV209", change = "Level 17's two traffic lights stand at the kerb: the lights are drawn smaller (x0.48, as tall as the street lamps), their shorter arm still reaches over the lane, so each pole and its DV67 map item move 2 tiles to the kerb",
                zod = "no such objects (OpenZ's own decor, DV67)",
                openz = "Sim/ZWorld.TrafficLightsKerb.cs + ZDetailPropsData: C01 on tile (43, 98) (was (41, 98)), C02 on (66, 98) (was (68, 98)); the same hit points and rules as every DV67 prop; they block no tile, as at their old tiles (not passable at load), so every path stays as it was; level 18's lights are view only",
                measured = measured,
                toggle = "ZWorld.TrafficLightsKerbDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact' where DV67 has no props; headless default off; off in every counted check unless the check sets it)",
                status = !pass ? DevCheck : DevActive,
            });
        }

        /// <summary>Batch: only the DV209 row (-executeMethod OpenZ.EditorTools.OpenZParity.TrafficLightsKerbRowBatch) into
        /// Logs/traffic_lights_DV209.txt; exit 0 = ACTIVE and every line PASS.</summary>
        public static void TrafficLightsKerbRowBatch()
        {
            int code = 1;
            try
            {
                var list = new List<Deviation>();
                AddTrafficLightsKerbDeviation(list);
                var d = list[0];
                string text = $"DV209 row: status {d.status}\nmeasured: {d.measured}\n";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/traffic_lights_DV209.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = d.status == DevActive ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV209 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }
#endif
}
