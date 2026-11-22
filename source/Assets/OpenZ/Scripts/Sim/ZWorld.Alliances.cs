using System;
using System.Linq;
using OpenZ.Multiplayer;

namespace OpenZ.Sim
{
    public sealed partial class ZWorld
    {
        int[] skirmishAlliances;
        SkirmishRules skirmishRules;
        DuelRules skirmishProduction;
        readonly double[] alliancePower = new double[8], allianceFlags = new double[8], skirmishTerritory = new double[8], skirmishFort = new double[8];
        internal bool HasSkirmishProduction => skirmishProduction != null;
        internal bool HasIndividualDefeatSequence { get; private set; }
        internal bool SkirmishHasTeams { get; private set; }

        internal void ConfigureSkirmishRules(SkirmishRules rules, int[] closed, bool replica = false)
        {
            skirmishRules = rules.Copy();
            HasIndividualDefeatSequence = skirmishAlliances.Length - closed.Length >= 3;
            SkirmishHasTeams = Enumerable.Range(0, skirmishAlliances.Length).Where(seat => !closed.Contains(seat))
                .GroupBy(seat => skirmishAlliances[seat]).Any(alliance => alliance.Count() > 1);
            ApplyMapFactoryTech(rules.VehicleTech, rules.RobotTech);
            SetBehavior(rules.Behavior(ZBehaviorFamily.Tanks));
            SetBehavior(rules.Behavior(ZBehaviorFamily.Robots));
            if (rules.ClassicUnits || rules.CustomRoster)
                ConfigureSoloUnits(rules.ClassicUnits ? DuelUnitRoster.Z1996 : rules.CustomUnits,
                    rules.ClassicUnits || rules.ExcludeMammoth, rules.ClassicUnits || rules.ExcludeBizon);
            if (rules.TerritoryCap != 50 || rules.FortSpeedup != 0 || rules.BuildPercent.Any(percent => percent != 100))
                skirmishProduction = rules.Production;
            if (replica) return;

            if (rules.OverrideFortTurrets)
            {
                foreach (var gun in Objects.Where(unit => unit.IsTowerGun && unit.Owner != Team.Neutral).ToArray())
                    DeleteObject(gun);
                FortDoorGuns.RemoveAll(gun => Get(gun.Id) == null);
            }

            foreach (int seat in closed)
            {
                var team = (Team)(seat + 1);
                skirmishEliminated |= 1 << (int)team;
                foreach (var unit in Objects.Where(unit => unit.Owner == team).ToArray())
                {
                    if (unit.IsFort || unit.IsUnit) DeleteObject(unit);
                    else if (unit.Type == "flag") AwardZone(unit, Team.Neutral);
                    else SetOwnerZod(unit, Team.Neutral, false);
                }
            }
            for (int seat = 0; seat < skirmishAlliances.Length; seat++)
            {
                if (closed.Contains(seat)) continue;
                var team = (Team)(seat + 1);
                var fort = Objects.FirstOrDefault(unit => unit.IsFort && unit.Owner == team);
                if (rules.OverrideFortTurrets && fort != null)
                    PlaceFortTurrets(fort, rules.FortTurrets, MAX_UNITS_PER_TEAM - Objects.Count(unit => unit.IsUnit && unit.Owner == team && !unit.Destroyed));
                foreach (string type in rules.StartingUnits(seat))
                {
                    if (Objects.Count(unit => unit.IsUnit && unit.Owner == team && !unit.Destroyed) +
                        (ZBuildLists.IsRobot(type) ? Math.Max(1, S.Unit(type)?.group_amount ?? 1) : 1) > MAX_UNITS_PER_TEAM) break;
                    if (!ZBuildLists.IsRobot(type)) { AddHeadStart(team, type); continue; }
                    var marker = AddHeadStart(team, "jeep").FirstOrDefault();
                    if (marker == null) break;
                    int x = marker.LocX, y = marker.LocY;
                    DeleteObject(marker);
                    CreateRobotGroup(type, x, y, team);
                }
            }
            UpdateSkirmishProduction(true);
        }

        internal bool SkirmishFriendlyFire => skirmishRules != null && !skirmishRules.FreeForAll && skirmishRules.FriendlyFire;

        internal void UpdateSkirmishProduction(bool initialize = false)
        {
            if (skirmishProduction == null || DuelIsReplica) return;
            Array.Clear(alliancePower, 0, alliancePower.Length);
            Array.Clear(allianceFlags, 0, allianceFlags.Length);
            foreach (var unit in Objects)
            {
                int seat = (int)unit.Owner - 1;
                if ((uint)seat < (uint)skirmishAlliances.Length && IsMilitaryValueUnit(unit))
                    alliancePower[skirmishAlliances[seat]] += Math.Max(0, S.Unit(unit.Type)?.build_time ?? 0);
            }
            for (int seat = 0; seat < skirmishAlliances.Length; seat++)
                if (!SkirmishEliminated((Team)(seat + 1))) allianceFlags[skirmishAlliances[seat]] += TeamFlagFraction((Team)(seat + 1));

            for (int seat = 0; seat < skirmishAlliances.Length; seat++)
            {
                var team = (Team)(seat + 1);
                if (SkirmishEliminated(team)) continue;
                int own = skirmishAlliances[seat], rival = -1;
                for (int other = 0; other < skirmishAlliances.Length; other++)
                {
                    int candidate = skirmishAlliances[other];
                    if (candidate == own || SkirmishEliminated((Team)(other + 1))) continue;
                    if (rival < 0 || alliancePower[candidate] > alliancePower[rival] ||
                        alliancePower[candidate] == alliancePower[rival] && allianceFlags[candidate] > allianceFlags[rival]) rival = candidate;
                }
                double lead = rival < 0 ? 0 : ZForceComparison.RelativePercent(alliancePower[own], alliancePower[rival]);
                double territory = skirmishProduction.TerritoryTimeReduction(TeamFlagFraction(team), lead);
                double speed = lead >= 0 ? 1 : skirmishProduction.FortCatchupSpeed(-lead,
                    skirmishProduction.TerritoryTimeReduction(allianceFlags[rival], -lead));
                double fort = 1 - Math.Min(1 - territory, 1 / speed) / (1 - territory);
                bool territoryChanged = Math.Abs(territory - skirmishTerritory[seat]) > 1e-7;
                bool fortChanged = Math.Abs(fort - skirmishFort[seat]) > 1e-7;
                skirmishTerritory[seat] = territory;
                skirmishFort[seat] = fort;
                if (!initialize && !territoryChanged && !fortChanged) continue;
                foreach (var building in Objects)
                    if (building.Owner == team && building.IsProducer && !building.Destroyed && building.Building &&
                        building.CurUnit != null && (initialize || territoryChanged || building.IsFort))
                        building.DuelUpdateProductionTime(building.ProductionProgress);
            }
        }

        internal double SkirmishBuildSeconds(ZObject building, int seconds)
        {
            int seat = (int)building.Owner - 1;
            double health = building.MaxHealth > 0 ? Math.Max(0, Math.Min(1, (double)building.Health / building.MaxHealth)) : 1;
            if ((uint)seat >= (uint)skirmishAlliances.Length) return seconds * (1 - .5 * building.ZoneOwnage) * (1 + 1.25 * (1 - health));
            double territory = DuelIsReplica ? skirmishProduction.TerritoryTimeReduction(TeamFlagFraction(building.Owner), 0) : skirmishTerritory[seat];
            double fort = building.IsFort ? skirmishFort[seat] : 0;
            return Math.Max(1, seconds * (1 - territory) * (1 - fort) * (1 + 1.25 * (1 - health)) * skirmishRules.BuildPercent[seat] / 100);
        }

        internal void SetSkirmishAlliances(int[] alliances)
        {
            skirmishAlliances = (int[])alliances.Clone();
        }

        public bool AreAllied(Team left, Team right)
        {
            if (left == right) return true;
            int leftSeat = (int)left - 1;
            int rightSeat = (int)right - 1;
            return skirmishAlliances != null &&
                (uint)leftSeat < (uint)skirmishAlliances.Length &&
                (uint)rightSeat < (uint)skirmishAlliances.Length &&
                skirmishAlliances[leftSeat] == skirmishAlliances[rightSeat];
        }

        public bool IsWinningSeat(Team team) =>
            GameOver && Winner != Team.Neutral && AreAllied(team, Winner);
    }
}
