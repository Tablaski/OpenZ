using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ 1.5 deviation DV199 "the computer and the big tanks" (kit level18_rules, owner addition 5
    /// Level 18 is the first level with a MAMMOTH and a BIZON in its start set (DV198, Sim/ZWorld.L18Rules.cs).
    /// <para>What the computer already does with them (measured, kit proof/bot_rows_L18.txt): Zod's computer (Sim/ZBot.cs) is type-blind. It
    /// orders every idle vehicle it owns at the targets of its round (flags, the enemy fort, enemy units when it goes all out), sends its most
    /// valuable damaged vehicle to repair (DV137 ranks the big tanks first), sends robot squads into every EMPTY vehicle it sees
    /// (ZObject.CanBeEntered: a neutral vehicle, the two big tanks too), and the big tanks' own rules run by themselves for whoever drives them
    /// (the Mammoth's ram DV182 and its fight while it rams DV196, the Bizon's crush DV183 and its straight course DV181, the Bizon's wading
    /// DV192): no order is needed for them. The build lists hold neither (Sim/ZBuildLists.cs), so the computer never builds one.</para>
    /// <para>What it got WRONG: the computer decides "reachable" from Zod's regions of its own map (ZBot.InSameRegion: the tile classes at map
    /// load). On level 18 the bridge tiles count as road there, so with every bridge but one down the computer still sends a big tank at a
    /// target it cannot drive to now (a flag, the fort, its repair station across the river); the path finder then gives the bare end point
    /// and the tank drives into the bank and stands (the measured case: a Mammoth across the river sent home to repair stood 11 s, then
    /// drove off, again and again). The Bizon is the other way round: it can wade, the region map does not know it.</para>
    /// <para>DV199: for a computer's BIG TANK only, the region test of its order round (ZBot.MatchTargets_3), of its repair pick (DV137,
    /// ZBot.CrStation) and of its end-game round (DV60, ZBot.FinishGame) asks the pathing layer's exact reachability on the tank's own grid as it
    /// is NOW (ZReach on ZWorld.GridKindOf: the Mammoth the heavy tanks' grid, the Bizon its amphibious one; bridges as they stand, rocks as they
    /// are): a target is taken when a spot the tank can stand on in its own component lies within <see cref="BigTankReachTiles"/> of the
    /// target's point (a flag, a box, a repair entrance: 2 tiles; a fort: its half size + 2; a unit to attack: the tank's range). Every other
    /// unit, and every other step of the computer (the mutual-nearest pick, the wave share, the order delay, the target kinds), stays Zod's.
    /// A tank standing off the graph (inside a building exit) falls back to Zod's region test.</para>
    /// <para>And its repair trip (DV137) can no longer hold it for minutes: a computer's big tank that stands still on its way to the bay for
    /// <see cref="BigTankTripStallSec"/> s (no queue of the movement layer, no stun, no fight; measured: a crane healed before it stood on the
    /// bay's doorstep and the Mammoth waited 7 minutes) is sent back by the computer's own command and is the planner's again
    /// (<see cref="BigTankTripStall"/>).</para>
    /// <para>Switch <see cref="ZWorld.BotBigTanksDeviation"/>: View/ZGame.cs on at every start (off in 'Zod exact'); headless default off; the
    /// parity report and the counted checks hold it off. Off: not one order changes. Only the computer's own big tanks are asked, so a level
    /// without one (levels 2, 7, 16, 17) plays tick for tick as before. Deterministic: the reachability components are a pure function of the
    /// path finder's grids, the ring search scans in a fixed order; no random draw, no clock.</para>
    /// </summary>
    public sealed partial class ZBot
    {
        /// <summary>DV199: a target's point counts as reached from this many tiles (flags, boxes, repair entrances).</summary>
        public const int BigTankReachTiles = 2;
        /// <summary>DV199: this unit is a big tank the row applies to.</summary>
        public static bool BotBigTank(ZObject u) => ZWorld.BotBigTanksDeviation && u != null && u.Kind == ZKind.Vehicle && ZBigTanks.Is(u.Type);

        /// <summary>DV199 statistics (checks): pairs the live test refused that Zod's regions allowed, and pairs it allowed that they refused.</summary>
        public int BigTankPairsDropped { get; private set; }
        public int BigTankPairsAdded { get; private set; }

        /// <summary>DV199, ZBot.MatchTargets_3 / FinishGame: the region test of unit u and target t at point (tx, ty).</summary>
        bool BotPairRegion(ZObject u, ZObject t, int ux, int uy, int tx, int ty)
        {
            bool zod = InSameRegion(ux, uy, tx, ty, u.Kind == ZKind.Robot);
            if (!BotBigTank(u)) return zod;
            int tiles = BigTankReachTiles;
            if (t != null)
            {
                if (t.Kind == ZKind.Building) tiles = t.IsFort ? Math.Max(t.WidthPix, t.HeightPix) / 32 + 2 : Math.Max(t.WidthPix, t.HeightPix) / 32 + 1;
                else if (t.Kind != ZKind.MapItem) tiles = Math.Max(BigTankReachTiles, u.AttackRadius / 16);
            }
            bool live = BigTankReaches(u, tx, ty, tiles, zod);
            if (live != zod) { if (live) BigTankPairsAdded++; else BigTankPairsDropped++; }
            return live;
        }

        /// <summary>DV199, ZBot.CrStation: the repair entrance (ex, ey) for unit u.</summary>
        bool BotPairPoint(ZObject u, int ex, int ey)
        {
            bool zod = InSameRegion(u.LocX + 8, u.LocY + 8, ex, ey, false);
            if (!BotBigTank(u)) return zod;
            bool live = BigTankReaches(u, ex, ey, BigTankReachTiles, zod);
            if (live != zod) { if (live) BigTankPairsAdded++; else BigTankPairsDropped++; }
            return live;
        }

        /// <summary>DV199: a computer's big tank on its repair trip (DV137) that has not moved <see cref="BigTankTripStallPx"/> px in this many game
        /// seconds on its way to the bay (not in the bay, not on the pad, not stunned, not firing, not held in a queue of the movement layer: a
        /// bridge, a lane, the path queue) gives the trip up.</summary>
        public const double BigTankTripStallSec = 12.0;
        public const int BigTankTripStallPx = 8;
        /// <summary>DV199 statistics (checks): repair trips of a big tank given up that way.</summary>
        public int BigTankTripsGivenUp { get; private set; }
        int bbStallUnit = -1, bbStallX, bbStallY; double bbStallAt;

        /// <summary>DV199, ZBot.CrFollow while the trip's repair waypoint is still ahead (measured case: a crane healed in the bay stood on its
        /// doorstep, the Mammoth waited at its queue spot for the rest of the match and DV137's own limit, 420 s, held it out of every order
        /// round). Given up: the computer's own command sends it back to where it was sent from (the trip's MOVE back, through the command
        /// layer as every order of the computer), the station is the trip's last choice for a period (as DV137 does when an approach gives up
        /// before the bay), and the hold ends, so the next order round has it again.</summary>
        void BigTankTripStall(ZObject u, int stationId)
        {
            if (!BotBigTank(u) || u.IsShuttle) return;
            bool busy = u.OnRepairPad(stationId) || u.InRepairBay(stationId) || u.LeavingRepairPad(stationId) || u.Stunned || u.AttackObject != null
                        || (u.PathWait != ZPathWait.None && u.PathWait != ZPathWait.Arrived);
            int dx = u.CenterX - bbStallX, dy = u.CenterY - bbStallY;
            if (busy || bbStallUnit != u.Id || dx * dx + dy * dy > BigTankTripStallPx * BigTankTripStallPx)
            { bbStallUnit = u.Id; bbStallX = u.CenterX; bbStallY = u.CenterY; bbStallAt = W.Time; return; }
            if (W.Time + TIME_EPS < bbStallAt + BigTankTripStallSec) return;
            bbStallUnit = -1;
            var back = new ZWaypoint { mode = WpMode.Move, refId = -1, x = crTrip.fromX, y = crTrip.fromY, attackTo = false, playerGiven = true };
            if (W.SendWaypoints(Team, u, new[] { back }) && ClientView) Relay(u);
            var st = W.Get(stationId);
            if (st != null && st.CanRepairUnit(Team)) { crNoBayId = st.Id; crNoBayUntil = W.Time + ZWorld.CpuRepairEverySec * EasySlowScale; }
            BigTankTripsGivenUp++;
            Log.Add($"{W.Time:F0}s {Team} DV199 repair trip given up: {u.Type}#{u.Id} stood {BigTankTripStallSec:0} s at ({u.CenterX / 16},{u.CenterY / 16}) on its way to repair#{stationId}");
            CrEnd(u, CpuRepairEnd.NoRepair);
        }

        /// <summary>A spot the big tank can stand on in its own reachability component (its own grid kind, the grids as they are now) lies within
        /// `tiles` of (x, y). No pathing layer, or the tank stands off the graph: Zod's answer.</summary>
        bool BigTankReaches(ZObject u, int x, int y, int tiles, bool zod)
        {
            var reach = W.Pathing?.Reach;
            if (reach == null) return zod;
            using (W.Path.Kind(W.GridKindOf(u)))
            {
                int c = reach.UnitComp(u, false);
                if (c < 0) return zod;
                return reach.Nearest(x, y, false, false, c, out _, out _, tiles);
            }
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV199 switch (off = Zod's computer: a big tank's targets by Zod's regions of the map file).</summary>
        public static bool BotBigTanksDeviation = false;

        /// <summary>The checks and the parity report: DV199 held off until disposed.</summary>
        public static IDisposable BotBigTanksOff() => new BotBigTanksOffHold();
        sealed class BotBigTanksOffHold : IDisposable
        {
            readonly bool was = BotBigTanksDeviation, drive = BigTankDriveDeviation;   // 1.5 DV202 (kit l18_cpu_bigtank_r2) is held with it
            public BotBigTanksOffHold() { BotBigTanksDeviation = false; BigTankDriveDeviation = false; }
            public void Dispose() { BotBigTanksDeviation = was; BigTankDriveDeviation = drive; }
        }
    }
}
