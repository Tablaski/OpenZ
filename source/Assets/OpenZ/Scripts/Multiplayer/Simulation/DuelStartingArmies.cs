using System;
using System.Linq;
using OpenZ.Multiplayer;

namespace OpenZ.Sim
{
    public static class DuelMissionDefaults
    {
        public static string[] OutsideUnits() => new[] { "light", "heavy", "pyro", "laser" };

        public static DuelArmySettings Create()
        {
            var army = new DuelArmySettings { FortTurrets = new[] { "howitzer", "missile_cannon", "", "" } };
            army.StartingUnits.AddRange(OutsideUnits());
            return army;
        }

        public static bool IsOutsidePreset(string[] units) => units != null && units.Length == 4 &&
            units.All(type => type == "" || Array.IndexOf(DuelRules.AllowedExtras, type) >= 0);

        public static void ApplyOutsidePreset(DuelRules rules, string[] units)
        {
            if (!IsOutsidePreset(units)) throw new ArgumentException("Invalid four-slot starting army.");
            rules.Common.StartingUnits.Clear();
            rules.Common.StartingUnits.AddRange(units.Select(type => rules.Allows(type) ? type : ""));
        }
    }

    public sealed partial class ZWorld
    {
        public void DuelApplyStartingArmies()
        {
            if (duelRules == null || DuelIsReplica || Time != 0) throw new InvalidOperationException("Fresh authoritative duel required.");
            foreach (var o in Objects.ToArray())
                if (o.IsUnit && (o.Owner != Team.Neutral || !DuelAllowsUnit(o.IsHero ? "heroes" : o.Type))) DeleteObject(o);
            for (int seat = 0; seat < 2; seat++)
            {
                Team team = seat == 0 ? Team.Red : Team.Blue;
                var army = duelRules.EffectiveFor(seat);
                var fort = Objects.First(o => o.IsFort && o.Owner == team);
                for (int i = 0; i < 4; i++)
                {
                    string type = army.FortTurrets[i]; if (string.IsNullOrEmpty(type)) continue;
                    var gun = CreateObject("cannon", type, fort.LocX + (i % 2 == 0 ? 16 : 112), fort.LocY + (i < 2 ? 0 : 48), team);
                    gun.Ejectable = false;
                }
                int budget = army.StartingUnits.Sum(t => ZBuildLists.IsRobot(t) ? Math.Max(1, S.Unit(t)?.group_amount ?? 1) : 1);
                if (budget + army.FortTurrets.Count(t => !string.IsNullOrEmpty(t)) > MAX_UNITS_PER_TEAM)
                    throw new InvalidOperationException("Starting army exceeds the 70 unit limit.");
                foreach (string type in army.StartingUnits)
                {
                    // Reuse the game's placement search, then replace its vehicle placeholder for robots/guns.
                    var placed = AddHeadStart(team, "jeep");
                    if (placed.Count != 1) throw new InvalidOperationException("Not enough space in front of the fort.");
                    var marker = placed[0]; int x = marker.LocX, y = marker.LocY;
                    DeleteObject(marker);
                    if (ZBuildLists.IsRobot(type)) CreateRobotGroup(type, x, y, team);
                    else CreateObject(ZBuildLists.IsCannon(type) ? "cannon" : "vehicle", type, x, y, team);
                }
            }
            Events.Clear();
        }

        public void DuelResetReplica()
        {
            if (!DuelIsReplica) throw new InvalidOperationException("Replica required.");
            Objects.Clear(); Flags.Clear(); byId.Clear(); graveyard.Clear(); graveOrder.Clear(); Events.Clear();
        }

        public void DuelSurrender(int seat)
        {
            if (DuelIsReplica || duelRules == null || GameOver || (seat != 0 && seat != 1)) return;
            Team loser = seat == 0 ? Team.Red : Team.Blue;
            foreach (var fort in Objects.Where(o => o.IsFort && o.Owner == loser).ToArray())
                if (!fort.Destroyed) { fort.SetHealth(0); UpdateObjectHealth(fort); }
            GameOver = true; Winner = loser == Team.Red ? Team.Blue : Team.Red;
            Events.Add(new ZEvent { type = ZEventType.GameOver, value = (float)Winner });
            if (EndFreezeDeviation) FreezeAfterGameOver();
        }
    }
}
