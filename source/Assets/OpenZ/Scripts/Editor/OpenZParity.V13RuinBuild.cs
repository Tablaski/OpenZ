#if !OPENZ_HARNESS
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>The Deviations row DV155 "no build clock on a destroyed factory" (1.3 item 4; the rule: Sim/ZWorld.RuinProduction.cs). Counted
    /// headless by Editor/OzRuinBuildCount.cs (menu OpenZ/V1.3/Rules: Item 4 Destroyed Factory No Build Check).</summary>
    public static partial class OpenZParity
    {
        static void AddRuinBuildDeviation(List<Deviation> list)
        {
            list.Add(new Deviation
            {
                id = "DV155", change = "A destroyed factory builds nothing, and is rebuilt empty",
                zod = "SetBuildingProduction has no destroyed check (zbuilding.cpp:115-144) and the computer re-sends its pick to every building of its team (zbot.cpp:1372-1432), " +
                      "so a destroyed factory's build clock ran on and its unit came out the moment it was rebuilt 600-660 s later (zobject.cpp:4235-4257)",
                openz = "a destroyed building takes no production or queue entry; the computer sends it no orders (its pick is still made: same draws); a rebuilt factory starts " +
                        $"with nothing in progress and an empty queue. Zod's rebuild time is kept{(ZWorld.FactoryAutoRebuild ? "" : " (switched off here: only a crane rebuilds a factory)")}",
                measured = "counted headless (Editor/OzRuinBuildCount.cs): rule on 0 units out of a ruin and 0 in the 2 s after its rebuild; rule off reproduces the instant unit",
                toggle = $"ZWorld.RuinNoBuildDeviation = {(ZWorld.RuinNoBuildDeviation ? "true (default)" : "false")}, ZWorld.FactoryAutoRebuild = {ZWorld.FactoryAutoRebuild}, ZWorld.RuinReviveDefault = {ZWorld.RuinReviveDefault} (code switches)",
                status = DevActive,
            });
        }
    }
}
#endif
