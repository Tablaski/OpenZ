#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit bridge_aim_further_r6: the parity row DV210 "a shot at a bridge lands on its deck" (Sim/ZObject.BridgeDeckAim.cs, the
    /// hook in Sim/ZObject.CombatNB.cs WallAim), the menu of its counted check (OzV15BridgeAimCount) and the Play check of the view half
    /// (View/OzBridgeDeckHits.cs: where the hits at a bridge are DRAWN).</summary>
    public static partial class OpenZParity
    {
        static void AddBridgeAimDeviation(List<Deviation> list)
        {
            string measured; bool pass;
            try
            {
                string report; using (OzV11Kit.GameSwitches()) report = OzV15BridgeAimCount.Quick(SettingsText, MapText);
                measured = OzV11Kit.VerdictCell(report, out pass) + " (the whole check: menu OpenZ/Checks/V1.5/Bridge aim onto the deck (no Play))";
            }
            catch (Exception ex) { measured = "the probe threw " + ex.GetType().Name + ": " + ex.Message; pass = false; }
            list.Add(new Deviation
            {
                id = "DV210", change = "A rocket, shell or grenade at a bridge lands on its deck a few metres past the end nearer to the shooter (4-6 m, a 1 m spread from shot to shot, never past the middle), not at the entrance at the shooter's feet; units still stop before the bridge where they did",
                zod = "Zod aims a shot at a building or bridge at its centre +-16 px (zobject.cpp:2140-2160); OpenZ's DV26 (combat_nb_v1) aims it at the nearest point of the building's walls, which for a bridge (no wall table: its whole rectangle) is the middle of its near end, the entrance",
                openz = "Sim/ZObject.BridgeDeckAim.cs, called first in DV26's WallAim: at a bridge the aim point is on the deck's centre line, 40 px (5 m) +- 8 px past the near end (by an integer hash of shooter, bridge and tick: no random draw), at most half the bridge's length; a shooter beside the deck at its own depth or more, a shooter on the deck 5 m ahead of itself. Zod's scatter after it as before. " +
                        "Range (DV26: to the bridge's nearest point), damage, blast radius, DV207's hit points, no friendly fire, every other target, the heavy's second barrel and the Pyro's flame unchanged; every shot now hits inside the bridge (before 45 % landed outside it), so it falls to somewhat fewer shots (the MLRS stays at 6; the Tough squad 32 -> 28, the tanks 25-26 -> 23). View half (view only): View/OzBridgeDeckHits.cs: the hits drawn at the sim's point on the deck, and the shooter's own team gets no shrapnel / flinch from a shot at a bridge (the sim never hurts it)",
                measured = measured,
                toggle = "ZWorld.BridgeDeckAimDeviation (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off; the parity report's Zod rows and probes hold it off; off in every counted check unless the check sets it)",
                status = !pass ? DevCheck : DevActive,
            });
        }

        /// <summary>Batch: only the DV210 row (-executeMethod OpenZ.EditorTools.OpenZParity.BridgeAimRowBatch) into Logs/bridge_aim_DV210.txt;
        /// exit 0 = ACTIVE and every line of the whole check PASS.</summary>
        public static void BridgeAimRowBatch()
        {
            int code = 1;
            try
            {
                var list = new List<Deviation>();
                using (ZWorld.BridgeDeckAimOff()) AddBridgeAimDeviation(list);
                string full; using (OzV11Kit.GameSwitches()) full = OzV15BridgeAimCount.Check(SettingsText, MapText);
                int p = 0, f = 0; foreach (var l in full.Split('\n')) { if (l.StartsWith("PASS")) p++; else if (l.StartsWith("FAIL")) f++; }
                var d = list[0];
                string text = $"DV210 row: status {d.status}\nmeasured: {d.measured}\n\nthe whole check: {p} PASS, {f} FAIL\n{full}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/bridge_aim_DV210.txt"), text);
                Debug.Log("[OpenZ V1.5] " + text);
                code = d.status == DevActive && f == 0 ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[OpenZ V1.5] DV210 row batch threw " + ex); }
            EditorApplication.Exit(code);
        }
    }

    public static class OzV15BridgeAimMenu
    {
        [MenuItem("OpenZ/Checks/V1.5/Bridge aim onto the deck (no Play)")]
        static void Count()
        {
            string r; using (OzV11Kit.GameSwitches()) r = OzV15BridgeAimCount.Check(OzV11Kit.SettingsText, OzV11Kit.MapText);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/bridge_aim_DV210_check.txt"), r);
            Debug.Log("[OpenZ V1.5] Bridge aim (DV210):\n" + r);
        }

        /// <summary>The Play check's limits. MinDepthM: the sim's nearest blast is 1.0 m past the near end (a grenade's -24 px scatter from the
        /// rule's 32 px floor), so 0.9 m. MinFromShooterM: from a robot that stands right at the entrance (on the last tile before the deck) that
        /// nearest blast is about 2.0 m away, so 1.8 m (in the owner's set-up, the units 1-2 tiles before the end, 4.4 m or more: the counted
        /// check). OverDeckM: a burst within this of the DRAWN deck surface under it sits on the deck.</summary>
        const float MinDepthM = 0.9f, MinFromShooterM = 1.8f, OverDeckM = 0.6f;

        static string M(float v) => float.IsNaN(v) ? "-" : v.ToString("0.00");

        /// <summary>Play: the hits DRAWN at bridges since the last clear (View/OzBridgeDeckHits.cs): real blasts must burst MinDepthM or more past
        /// the near end, inside the bridge's length, MinFromShooterM or more from their shooter, within OverDeckM of the bridge's DRAWN deck
        /// surface straight under them (its meshes, measured at the blast; not the walk collider), never beside the drawn deck; no shell or rocket
        /// at a bridge cut short by ShotLine before the near end; the MLRS's cosmetic rockets 1.5 m or more past the near end (or the shooter's
        /// side line). Logs/bridge_aim_DV210_play.txt.</summary>
        [MenuItem("OpenZ/Checks/V1.5/Bridge aim: hits drawn on bridges (Play)")]
        static void Drawn()
        {
            var g = ZGame.I;
            var sb = new StringBuilder(); int fails = 0;
            void L(bool ok, string t) { sb.Append(ok ? "PASS  " : "FAIL  ").Append(t).Append('\n'); if (!ok) fails++; }
            if (!Application.isPlaying || !g || g.World == null) { Debug.LogWarning("[OpenZ V1.5] Bridge aim (Play): enter Play on a level first"); return; }
            var hits = OzBridgeDeckHits.Recent(); var cuts = OzBridgeDeckHits.CutsSeen();
            var real = hits.Where(h => h.real).ToArray(); var ripple = hits.Where(h => !h.real).ToArray();
            L(ZWorld.BridgeDeckAimDeviation && OzBridgeDeckHits.On, $"switches: DV210 (sim) {(ZWorld.BridgeDeckAimDeviation ? "on" : "OFF")}, the view half {(OzBridgeDeckHits.On ? "on" : "OFF")} (both on in normal play, the sim's off in 'Zod exact')");
            L(OzBridgeDeckHits.Probe, $"the deck probe and the cut watch {(OzBridgeDeckHits.Probe ? "on" : "OFF: run 'Bridge aim: clear the drawn hits (Play)' before the takes")}");
            L(real.Length > 0, $"{real.Length} blasts drawn on bridges, {ripple.Length} MLRS ripple rockets ({OzBridgeDeckHits.RippleMoved} of {OzBridgeDeckHits.RippleSeen} put back onto the deck) since the last clear");
            if (real.Length > 0)
            {
                var bad = real.Where(h => h.depthM < MinDepthM || h.depthM > h.lengthM || h.fromShooterM < MinFromShooterM).ToArray();
                L(bad.Length == 0, $"every blast inside the bridge: past the near end {real.Min(h => h.depthM):0.0}..{real.Max(h => h.depthM):0.0} m (needs {MinDepthM:0.0} m or more), from the shooter {real.Min(h => h.fromShooterM):0.0}..{real.Max(h => h.fromShooterM):0.0} m (needs {MinFromShooterM:0.0} m or more)" +
                  (bad.Length > 0 ? $"; first off: bridge #{bad[0].bridge} by #{bad[0].shooter}: {bad[0].depthM:0.0} m in of {bad[0].lengthM:0.0}, {bad[0].fromShooterM:0.0} m from it (a shooter standing ON the deck or on the entrance tile can see 1-2 m: stage the units 1-2 tiles before the end)" : ""));
                var measured = real.Where(h => h.deck == 1).ToArray(); var beside = real.Where(h => h.deck == 2).ToArray();
                int blind = real.Count(h => h.deck == 3), unprobed = real.Count(h => h.deck == 0);
                var off = measured.Where(h => Mathf.Abs(h.overDeckM) > OverDeckM).ToArray();
                L(off.Length == 0 && beside.Length == 0 && unprobed == 0, $"on the DRAWN deck: {measured.Length} blasts measured against the bridge's drawn meshes straight under them" +
                  (measured.Length > 0 ? $", {measured.Min(h => h.overDeckM):0.00}..{measured.Max(h => h.overDeckM):0.00} m over the deck surface (needs within {OverDeckM:0.0} m; under 0 = under the drawn deck)" : "") +
                  $"; {beside.Length} with no drawn deck surface under them (the drawn deck narrower than the sim's bridge); {unprobed} not probed (the probe was off)" +
                  (off.Length > 0 ? $"; first off: bridge #{off[0].bridge} by #{off[0].shooter}: {off[0].overDeckM:0.00} m over the drawn deck at {off[0].depthM:0.0} m in, {off[0].acrossM:+0.0;-0.0} m across" : "") +
                  (beside.Length > 0 ? $"; first beside: bridge #{beside[0].bridge} by #{beside[0].shooter} at {beside[0].depthM:0.0} m in, {beside[0].acrossM:+0.0;-0.0} m across" : ""));
                if (blind > 0) sb.Append($"NOTE  {blind} blasts: the bridge's meshes there have no CPU copy (not readable) or it has no drawn body: judge by eye that those bursts sit ON the deck (not under it, not floating)\n");
                L(real.Select(h => Mathf.Round(h.depthM * 2f)).Distinct().Count() * 3 >= real.Length || real.Length < 3, $"spread: {real.Select(h => Mathf.Round(h.depthM * 2f)).Distinct().Count()} different depths (0.5 m steps) among {real.Length} blasts");
            }
            var shortCuts = cuts.Where(c => c.depthM < MinDepthM).ToArray();
            L(shortCuts.Length == 0, $"shells / rockets at a bridge cut short by the walk layer (View/ShotLine.cs: a bank or deck edge {ShotLine.LowBankM:0.0} m or more over the flight): {cuts.Length} cut, {shortCuts.Length} of them before {MinDepthM:0.0} m past the near end (drawn at the entrance instead of on the deck)" +
              (shortCuts.Length > 0 ? $"; first: bridge #{shortCuts[0].bridge} by #{shortCuts[0].shooter}: cut at {shortCuts[0].depthM:0.0} m, {shortCuts[0].fromShooterM:0.0} m from it" : ""));
            if (ripple.Length > 0)
            {
                var bad = ripple.Where(h => h.depthM < OzBridgeDeckHits.MinDeckM - 0.05f).ToArray();
                L(bad.Length == 0, $"MLRS ripple rockets past the near end {ripple.Min(h => h.depthM):0.0}..{ripple.Max(h => h.depthM):0.0} m, from the shooter {ripple.Min(h => h.fromShooterM):0.0}..{ripple.Max(h => h.fromShooterM):0.0} m");
            }
            sb.Append($"NOTE  own units inside the splash of their own team's shot at a bridge, spared the shrapnel chips / flinch (View/UnitView.Deaths.cs HitReactions, the sim's no-friendly-fire filter): {OzBridgeDeckHits.OwnSkipped}\n");
            foreach (var h in hits) sb.Append($"      {(h.real ? "blast " : "ripple")} bridge #{h.bridge} shooter #{h.shooter} t {h.at:0.0}: {h.depthM:0.0} m past the near end of {h.lengthM:0.0} m, {h.acrossM:+0.0;-0.0} m across, {M(h.overDeckM)} m over the drawn deck{(h.real && h.deck == 2 ? " (NO drawn deck under it)" : h.real && h.deck == 3 ? " (meshes not readable)" : "")}, {h.fromShooterM:0.0} m from the shooter{(h.moved ? " (put back onto the deck)" : "")}\n");
            foreach (var c in cuts) sb.Append($"      cut    bridge #{c.bridge} shooter #{c.shooter} t {c.at:0.0}: cut short at {c.depthM:0.0} m past the near end of {c.lengthM:0.0} m, {c.fromShooterM:0.0} m from the shooter\n");
            sb.Insert(0, fails == 0 ? "DV210 bridge hits drawn (Play): every line PASS\n" : $"DV210 bridge hits drawn (Play): {fails} FAIL\n");
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/bridge_aim_DV210_play.txt"), sb.ToString());
            Debug.Log("[OpenZ V1.5] Bridge aim (Play):\n" + sb);
        }

        /// <summary>Play: clears the noted hits and turns the deck probe and the cut watch on for the rest of this Play session (off in normal play).</summary>
        [MenuItem("OpenZ/Checks/V1.5/Bridge aim: clear the drawn hits (Play)")]
        static void ClearDrawn() { OzBridgeDeckHits.Clear(); OzBridgeDeckHits.Watch(true); Debug.Log("[OpenZ V1.5] Bridge aim (Play): the noted hits cleared; the deck probe and the cut watch on"); }
    }
}
#endif
