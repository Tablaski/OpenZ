#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// the counted check (Editor/OzV13HighGroundCount.cs, every line but the real shots of each weapon, which the menu runs).</summary>
    public static partial class OpenZParity
    {
        static void AddHighGroundDeviation(List<Deviation> list)
        {
            try { list.Add(DevHighGround()); }
            catch (Exception ex) { list.Add(new Deviation { id = "DV162", change = "DevHighGround", zod = "-", openz = "-", measured = "probe threw " + ex.GetType().Name + ": " + ex.Message, toggle = "-", status = DevCheck }); }
        }

        static Deviation DevHighGround()
        {
            string measured; bool pass;
            string Level(string lv) { var a = UnityEngine.Resources.Load<UnityEngine.TextAsset>("OpenZ/Maps/" + lv + "_grid"); return a != null ? a.text : null; }
            string report;
            using (OzV11Kit.GameSwitches()) report = OzV13HighGroundCount.Check(SettingsText, Level("UE"), Level, false);
            measured = OzV11Kit.VerdictCell(report, out pass);
            return new Deviation
            {
                id = "DV162", change = $"High ground shoots farther: {ZWorld.HighGroundRangePct - 100} % more range at a target below, for fort tower guns and for what stands on a Unit editor platform's deck",
                zod = "range is flat, 2D, the same from anywhere (zobject.cpp:4760-4765); a tower gun has its table range",
                openz = "Sim/ZObject.HighGround.cs: a fort tower gun on every level, and a robot, vehicle or gun on the deck of a Unit editor platform, medium and high alike, reaches " +
                        $"{ZWorld.HighGroundRangePct} % of its range, whole pixels, at a target below it. A ramp gives nothing, a target on the same level or higher gets no change, the flying shuttle is above everything. " +
                        "The hatch driver, the mech's hand gun and the crane's rifle go by the same rule. The computer's units run the same test. The agro reach is range + 40 px, so it stretches only for a custom range. " +
                        "View: the range ring shows the longer range. Damage, hit chance, reload and DV23's line are unchanged. Deterministic: no random draw",
                measured = measured,
                toggle = "ZWorld.HighGroundDeviation: OPTIONS 'HIGH GROUND SHOOTS FARTHER' (oz_high_ground, default on), off in 'Zod exact' (View/ZGame.cs); off by default headless, in the parity rows and in the counted checks",
                status = pass ? DevActive : DevCheck,
            };
        }
    }
}
#endif
