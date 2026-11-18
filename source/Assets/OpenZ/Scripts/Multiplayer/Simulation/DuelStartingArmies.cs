using System;
using System.Linq;
using OpenZ.Multiplayer;

namespace OpenZ.Sim
{
    public static class DuelMissionDefaults
    {
        public static string[] OutsideUnits() => new[] { "", "", "", "" };

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
                var turrets = army.FortTurrets.Where(t => !string.IsNullOrEmpty(t)).OrderByDescending(FortTurretPower).ToArray();
                var mounts = DuelFortMounts(fort);
                for (int i = 0; i < turrets.Length; i++)
                {
                    var gun = CreateObject("cannon", turrets[i], mounts[i].x, mounts[i].y, team);
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

        static int FortTurretPower(string type) => type == "missile_cannon" ? 4 :
            type == "howitzer" ? 3 : type == "gun" ? 2 : 1;

        (int x, int y)[] DuelFortMounts(ZObject fort)
        {
            fort.GetCreationMovePoint(out _, out int gateY);
            int frontY = gateY < fort.LocY ? fort.LocY : fort.LocY + 48;
            int rearY = frontY == fort.LocY ? fort.LocY + 48 : fort.LocY;
            int left = fort.LocX + 16, right = fort.LocX + 112;
            var mounts = new[] { (x: left, y: frontY), (x: right, y: frontY), (x: left, y: rearY), (x: right, y: rearY) };
            var distances = Enumerable.Repeat(long.MaxValue, mounts.Length).ToArray();
            void Compare(int x, int y, int width, int height)
            {
                for (int i = 0; i < mounts.Length; i++)
                {
                    long cx = mounts[i].x + 16, cy = mounts[i].y + 16;
                    long dx = Math.Max(0, Math.Max(x - cx, cx - (x + width)));
                    long dy = Math.Max(0, Math.Max(y - cy, cy - (y + height)));
                    distances[i] = Math.Min(distances[i], dx * dx + dy * dy);
                }
            }
            for (int y = 0; y < Map.height; y++)
                for (int x = 0; x < Map.width; x++)
                    if (Map.ClassAt(x, y) == ZClass.Road) Compare(x * ZConst.TILE, y * ZConst.TILE, ZConst.TILE, ZConst.TILE);
            foreach (var bridge in Objects)
                if (bridge.IsBridge && !bridge.Destroyed) Compare(bridge.LocX, bridge.LocY, bridge.WidthPix, bridge.HeightPix);
            if (distances[1] < distances[0]) (mounts[0], mounts[1]) = (mounts[1], mounts[0]);
            if (distances[3] < distances[2]) (mounts[2], mounts[3]) = (mounts[3], mounts[2]);
            return mounts;
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
