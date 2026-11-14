// OpenZ (GPL-3.0) - version 1.4, speed-up 2 (fewer garbage-collector stutters): the counted check of the rules' part, on the editor's own
// Mono (the runtime the Mono exe ships). Headless: no scene, no Play mode, nothing saved. Leave Play mode first.
//  G1 an L02 computer match, both sides computer players, with the switches a Play start sets (set here, so a fresh editor counts the
//     same as a game): the managed heap's growth while the rule ticks run (rises only, the estimate the perf line uses in a release exe),
//     a tick on average over 3 game minutes. The kit's headless harness (CoreCLR, exact counter, L02 seed 1): about 17 900 bytes a tick
//     before this kit, about 800 with it. The bar: at most 4096 bytes a tick (Mono's heap grows in whole blocks, so this reads coarser).
//  G2 the same on L16 (the crocodile level: its flee test made a closure for every object on every tick before).
//  G3 the collections the runtime made during G1 and G2 (shown, no bar: the editor's own work adds some).
// Menu: OpenZ/V1.4/Garbage: Rules Heap Count (headless).
using System;
using System.Globalization;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    public static class OzV14GarbageCount
    {
        /// <summary>The 89 rule switches ZGame sets at a Play start with the default settings (every one on); set here by name when the build has it.</summary>
        static readonly string[] GameSwitches =
        {
            "AheadWalkDeviation", "ApcCrewDeviation", "ApcSnipeDeviation", "ApproachDeviation", "ArmourChipDeviation", "BoardNearestDeviation", "BotCommitDeviation",
            "BotFinishDeviation", "BotHeavyDeviation", "BotLaserDeviation", "BuildingReachDeviation", "CalmFollowDeviation", "CalmMoveDeviation", "CaptureRunDeviation",
            "CarParkBotDeviation", "ClearanceDeviation", "CloseGrabDeviation", "CpuDefendDeviation", "CpuGunManDeviation", "CpuRepairDeviation", "CpuShuttleDeviation",
            "CraneAnyRuinDeviation", "CraneFixDeviation", "CraneGunDeviation", "CraneSnipeDeviation", "CustomStatsDeviation", "DetailPropsDeviation", "DragonDeviation",
            "DriverFireDeviation", "DuelDeviation", "EjectLockDeviation", "ElectrobotDeviation", "EnterFallbackDeviation", "EvilZodDeviation", "ExitLanesDeviation",
            "ExposedDriverDeviation", "FireOnMoveDeviation", "FormationDeviation", "FortDefenderDeviation", "FortDoorGunDeviation", "FortGunDeviation",
            "FortMortarDeviation", "FortRushDeviation", "GrabCertainDeviation", "GrabPolicyDeviation", "GrenadeBoostDeviation", "GrenadeRuleDeviation",
            "GroundFireDeviation", "HarbourWrecksDeviation", "HeavyFallDeviation", "HeavyPairDeviation", "HeavyRockDeviation", "HelpDeviation", "HeroBoostDeviation",
            "HeroSquadDeviation", "HighGroundDeviation", "KnockHoldDeviation", "LavaDeviation", "LosDeviation", "MTankDeviation", "MechHandGunDeviation",
            "MechHpDeviation", "MechMixDeviation", "NoAutoFlagUnderFireDeviation", "NoTankDuelVsJeep", "OrderMoveFallbackDeviation", "OvertakeDeviation",
            "ParkedPathDeviation", "ProneTargetDeviation", "PyroForceBurnDeviation", "RepairBayFireDeviation", "RepairInPlaceDeviation", "RiflePairDeviation",
            "RockRoundDeviation", "RocketSpeedDeviation", "RunOverDeviation", "ShiftAppendDeviation", "ShuttlePlayableDeviation", "SideShuttlesDeviation",
            "SnipeDeviation", "SnipeSafeSpotDeviation", "SniperBalanceDeviation", "SniperHeadshotDeviation", "SniperSteadyDeviation", "StrikeGroupDeviation",
            "ToughPrepareDeviation", "ToughSteadyDeviation", "TowerAnswerDeviation", "VeteranDeviation",
        };

        public const long BarBytesATick = 4096;
        static string F(double v, string f = "0") => v.ToString(f, CultureInfo.InvariantCulture);

        /// <summary>One computer match: average heap growth a tick (bytes) while the ticks run, ticks run, collections made.</summary>
        public static (double perTick, int ticks, int gcs) Match(string settings, string map, string level, int seed, double seconds)
        {
            using (OzV11StrikeCount.Game(true))
            {
                foreach (var n in GameSwitches) OzV11Kit.TrySet(typeof(ZWorld), n, true);
                var w = ZWorld.Create(settings, map, seed, level == "L17");
                w.Bots.Add(new ZBot(w, Team.Blue, seed + 101)); w.Bots.Add(new ZBot(w, Team.Red, seed));
                w.Events.Clear();
                for (int i = 0; i < 300 && !w.GameOver; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); }   // the first 3 s (the start's own set-up) left out
                long grew = 0; int ticks = 0, gc0 = GC.CollectionCount(0);
                long last = GC.GetTotalMemory(false);
                while (w.Time < seconds && !w.GameOver)
                {
                    w.Tick(ZConst.TICK);
                    long now = GC.GetTotalMemory(false);
                    if (now > last) grew += now - last;
                    w.Events.Clear();
                    last = GC.GetTotalMemory(false);
                    ticks++;
                }
                return (ticks > 0 ? (double)grew / ticks : 0, ticks, GC.CollectionCount(0) - gc0);
            }
        }

#if UNITY_EDITOR
        public static string Check()
        {
            var sb = new StringBuilder();
            sb.AppendLine("OpenZ 1.4 speed-up 2, the rules' garbage (headless, the editor's Mono): heap growth while the rule ticks run, bar " + BarBytesATick + " bytes a tick");
            var g1 = Match(OzV11Kit.SettingsText, OzV11Kit.MapText("L02"), "L02", 1, 180);
            var g2 = Match(OzV11Kit.SettingsText, OzV11Kit.MapText("L16"), "L16", 1, 180);
            sb.AppendLine(OzV11Kit.Verdict(g1.ticks > 1000 && g1.perTick <= BarBytesATick, $"G1 L02 seed 1, 3 min computer against computer: {F(g1.perTick)} bytes a tick over {g1.ticks} ticks (= {F(g1.perTick * 100 / 60 / 1024, "0.00")} KB a frame at 60 fps and 1x; before this kit the harness measured about 17 900)"));
            sb.AppendLine(OzV11Kit.Verdict(g2.ticks > 1000 && g2.perTick <= BarBytesATick, $"G2 L16 seed 1, the same: {F(g2.perTick)} bytes a tick over {g2.ticks} ticks"));
            sb.AppendLine($"INFO  G3 collections during G1 and G2: {g1.gcs + g2.gcs} (the editor's own work adds some)");
            sb.AppendLine($"MACHINE l02 {F(g1.perTick)} l16 {F(g2.perTick)} ticks {g1.ticks + g2.ticks} gcs {g1.gcs + g2.gcs}");
            return sb.ToString();
        }

        [UnityEditor.MenuItem("OpenZ/V1.4/Garbage: Rules Heap Count (headless)")]
        static void Menu() => OzV11Kit.RunMenu("1.4 garbage: rules heap count", Check);
#endif
    }
}
