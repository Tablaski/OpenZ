using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenZ.Multiplayer
{
    public static class DuelRuleChanges
    {
        public static string MountName(int mount) => "SLOT " + (mount + 1);
        public static List<string> Describe(DuelRules current, DuelRules next)
        {
            var changes = new List<string>();
            Action<string, object, object> add = (name, before, after) =>
            {
                if (!Equals(before, after)) changes.Add(name + ": " + Value(before) + " → " + Value(after));
            };
            add("Map", current.Map, next.Map);
            add("Opening orders (seconds)", current.PlanningSeconds, next.PlanningSeconds);
            add("Territory bonus", current.TerritoryBonus, next.TerritoryBonus);
            add("Minimum build seconds", current.MinimumBuildSeconds, next.MinimumBuildSeconds);
            add("Territory cap", current.TerritoryReductionCap, next.TerritoryReductionCap);
            add("Fort loser bonus", current.FortLoserMaximum, next.FortLoserMaximum);
            add("Damaged tank oil trails", current.TankDamageEffects, next.TankDamageEffects);
            add("Damaged tank shake", current.TankDamageShake, next.TankDamageShake);
            add("Radar loss", current.RadarLoss, next.RadarLoss);
            add("Shared build duration", current.Common.BuildDuration, next.Common.BuildDuration);
            add("Handicap enabled", current.HandicapEnabled, next.HandicapEnabled);
            add("Handicap team", current.HandicapSlot == 0 ? "RED" : "BLUE", next.HandicapSlot == 0 ? "RED" : "BLUE");
            add("Handicap build duration", current.Handicap.BuildDuration, next.Handicap.BuildDuration);
            add("Unit preset", current.UnitPreset, next.UnitPreset);
            foreach (string type in DuelUnitRoster.All) add("Allow " + type, current.Allows(type), next.Allows(type));
            if (current.TankDynamics != next.TankDynamics) changes.Add("Tank dynamics: profile changed");
            if (current.RobotDynamics != next.RobotDynamics) changes.Add("Robot dynamics: profile changed");
            for (int seat = 0; seat < 2; seat++)
            {
                var before = current.FortTurretsFor(seat); var after = next.FortTurretsFor(seat);
                for (int mount = 0; mount < 4; mount++) add((seat == 0 ? "RED" : "BLUE") + " fort " + MountName(mount), before[mount], after[mount]);
            }
            foreach (bool handicap in new[] { false, true })
            {
                var before = handicap ? current.Handicap.StartingUnits : current.Common.StartingUnits;
                var after = handicap ? next.Handicap.StartingUnits : next.Common.StartingUnits;
                for (int i = 0; i < 4; i++) add((handicap ? "Handicap" : "Shared") + " outside slot " + (i + 1), before.ElementAtOrDefault(i) ?? "", after.ElementAtOrDefault(i) ?? "");
            }
            return changes;
        }
        static string Value(object value) => value is string text && text.Length == 0 ? "EMPTY" : Convert.ToString(value, CultureInfo.InvariantCulture);
    }
}
