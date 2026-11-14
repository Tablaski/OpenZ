#if !OPENZ_HARNESS
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// the show: View/OzPlayableShuttle.BigCrash.cs). Counted headless (Editor/OzV13BigCrashCount.cs, menu OpenZ/V1.3/Shuttle: 7 Big Crash
    /// Check): the share over many landings (one crash landing in 3, today's crash landings unchanged), the loss and the floor, the 30 / 15 / 5 %
    /// cases, never destroyed by its own landing.</summary>
    public static partial class OpenZParity
    {
        static void AddShuttleBigCrashDeviation(List<Deviation> list)
        {
            list.Add(new Deviation
            {
                id = "DV154", change = ZWorld.ShuttleBigCrashOfAllLandings ? "About one shuttle landing in three is a BIG crash landing that costs hit points (never destroying it)"
                                                                     : "One shuttle crash landing in three is a BIG crash landing that costs hit points (never destroying it)",
                zod = "Z has no playable shuttle (OpenZ's DV118); OpenZ 1.2's crash landing (one landing in 4) cost no hit points",
                openz = $"{(ZWorld.ShuttleBigCrashOfAllLandings ? "one landing in " + ZWorld.ShuttleBigCrashEvery + " of every shuttle is the big crash, exactly one in every block of " + ZWorld.ShuttleBigCrashEvery + " landings (each crash landing is the big one)" : "today's crash landings (one landing in " + ZWorld.ShuttleRoughEvery + ", as in 1.2) stay, and one crash landing in " + ZWorld.ShuttleBigCrashEvery + " of every shuttle is the big one (about one landing in " + ZWorld.ShuttleRoughEvery * ZWorld.ShuttleBigCrashEvery + "), exactly one in every block of " + ZWorld.ShuttleBigCrashEvery + " crash landings by the ship's own crash count")}, " +
                        $"which one by the ship's id (no random draw), never on a repair station; it takes {ZWorld.ShuttleBigCrashLossPct} % of the FULL health at the touchdown " +
                        $"but never below {ZWorld.ShuttleBigCrashFloorPct} % (a ship at or under that loses nothing), so it never destroys the shuttle; the squad aboard is not hurt. The crash's timing, EXIT and the ramp as before",
                measured = "counted headless (Editor/OzV13BigCrashCount.cs): see the 1.3 results, item 7",
                toggle = "ZWorld.ShuttleBigCrashDeviation (code switch; ZGame: with DV118, off in 'Zod exact'; headless default off); ZWorld.ShuttleBigCrashOfAllLandings = the reading",
                status = DevActive,
            });
        }
    }
}
#endif
