using System;
using System.Collections.Generic;
using System.Linq;
using OpenZ.Sim;

namespace OpenZ.Multiplayer
{
    public enum DuelUnitPreset { OpenZ, Z1996, Custom }

    /// <summary>Stable unit IDs. The Z 1996 subset uses OpenZ IDs for the original units.</summary>
    public static class DuelUnitRoster
    {
        public static readonly string[] All = { "grunt", "psycho", "sniper", "tough", "pyro", "laser", "electrobot",
            "jeep", "light", "medium", "mtank", "heavy", "apc", "missile_launcher", "crane",
            "gatling", "gun", "howitzer", "missile_cannon", "heroes", "evilzod", "shuttle" };
        public static readonly string[] Lobby = All.Concat(ZBigTanks.Types).ToArray();
        public static readonly string[] Z1996 = { "grunt", "psycho", "sniper", "tough", "pyro", "laser",
            "jeep", "light", "mtank", "heavy", "apc", "missile_launcher", "crane", "gatling", "gun", "howitzer", "missile_cannon" };
        public static bool Known(string type) => Array.IndexOf(All, type) >= 0;
        public static string Name(DuelUnitPreset preset) => preset == DuelUnitPreset.OpenZ ? "OpenZ" : preset == DuelUnitPreset.Z1996 ? "Z 1996" : "Custom";
        internal static bool Matches(List<string> selection, string[] reference) => selection.Count == reference.Length && Array.TrueForAll(reference, selection.Contains);
    }
}
