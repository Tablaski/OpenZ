using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ deviation DV138 "the computer flies its shuttles" (on top of DV118's playable shuttle, K256f's EMPTY shuttle and DV125's
    /// routes). Before it the computer never used a shuttle: its order rounds leave every shuttle out, a computer team's order to one was
    /// refused and only a human team's squad could board one. With DV138 each computer player (every <see cref="ZBot"/> that is not
    /// ignored) runs one shuttle plan of its own beside its order rounds. Everything is behind ONE switch,
    /// <see cref="ZWorld.CpuShuttleDeviation"/> (the game: on at every level start, off in 'Zod exact'; the static default is off and the
    /// counted checks, the parity report and the computer's own checks keep it off unless they set it): with the switch off not one line
    /// below changes a state, and the human's own shuttle obeys exactly the rules it had.
    /// <para>THE RULE (every number is a constant of ZWorld below).</para>
    /// <list type="bullet">
    /// <item>WHOSE SHIPS. Every shuttle the computer's team owns with its pilot aboard: one from the level data or the testing panel, or one
    /// it took EMPTY. One run at a time per computer player; a second ship waits where it stands.</item>
    /// <item>EMPTY SHIPS. From <see cref="ZWorld.CpuShuttleEmptyFromSec"/> s of game time on (the player has the start for an empty ship of
    /// his side), an empty shuttle (no pilot, the null team) standing where nobody of the other side defends it (the defence measure below,
    /// 0 round the ship) is taken: the computer's nearest idle squad (as its order rounds see "idle"; not a squad that holds
    /// a position or patrols, not the Electrobot) that can walk there (the same region of the bot's map) within
    /// <see cref="ZWorld.CpuShuttleCaptureMaxPx"/> px is ordered into it, the same ENTER order the player gives (its leader becomes the pilot,
    /// the others its passengers, K256f). One such order at a time; a squad that has not got in after
    /// <see cref="ZWorld.CpuShuttleCaptureSec"/> s is stopped, and that ship is tried again only after 60 s, 120 s, 240 s, 480 s, at most
    /// <see cref="ZWorld.CpuShuttleCaptureTries"/> times.</item>
    /// <item>HOME. A ship's home is a landing spot near its own fort (out of every exit lane, off the gate faces, nobody of the other side
    /// near, its own squads can walk to it), found once and looked at again when it comes back. A ship with nobody aboard that stands
    /// farther than <see cref="ZWorld.CpuShuttleHomeNearPx"/> px from its home flies home first.</item>
    /// <item>EMBARK. At home, in the last <see cref="ZWorld.CpuShuttleBoardLeadSec"/> s of the cooldown, the nearest idle squad within
    /// <see cref="ZWorld.CpuShuttleBoardMaxPx"/> px that can walk to the ship's ramp is ordered into it (the player's ENTER order: the squad
    /// walks round the hull to the ramp and boards). Not in after <see cref="ZWorld.CpuShuttleBoardSec"/> s: it is stopped and another squad
    /// is tried (three squads, then a minute's pause).</item>
    /// <item>THE TARGET (when the cooldown is over, the squad aboard): a flag of a NEUTRAL or ENEMY territory with NO DEFENCE: none of the
    /// other side's robots or vehicles (shuttles do not count: they have no weapon) within <see cref="ZWorld.CpuShuttleSafeUnitPx"/> px,
    /// none of its manned guns whose reach + <see cref="ZWorld.CpuShuttleGunMarginPx"/> px covers it, none of its forts whose gun's reach +
    /// <see cref="ZWorld.CpuShuttleFortMarginPx"/> px reaches it from the walls; and a landing spot 3 to 10 tiles from that flag, in its
    /// territory, where the same measure is 0 too, on which the landing spot rule lets the ship down now (ZWorld.ShuttlePadFree: its outline + 0.5 m and its
    /// ramp lane, units counted), out of every exit lane, not an own repair station, from where the squad can walk to the flag. A fort's
    /// territory has no flag: no ship is ever sent into a fort, and the fort's reach keeps it off the fort's surroundings. Flags at least
    /// <see cref="ZWorld.CpuShuttleMinFlightPx"/> px away (nearer ones are the walkers'), first the flags none of its own units is on its
    /// way to, then the nearest, the lower id on a tie. The way there must keep out of every enemy fort's and gun's reach as measured and pass
    /// no vehicle and at most two robots of the other side within <see cref="ZWorld.CpuShuttlePathUnitPx"/> px; when the straight line does not, one middle point to the side is looked for (a DV125
    /// route), else that flag is left. None: it waits
    /// (looked at again every 10 s), its squad aboard.</item>
    /// <item>THE FLIGHT: the shuttle's own flight (climb, cruise, strafe under fire, the air traffic, the landing rule). Once a second on the
    /// way the target is measured again; taken by its own side, or defended now (the flag or the spot), before the descent: it turns to the
    /// next target by the same rule, or flies home with its squad. It cannot land there (it hovers): the next spot near that flag, three
    /// spots at most, then the next target or home.</item>
    /// <item>UNLOAD AND CAPTURE: on the ground the computer gives EXIT (the player's command); once the squad stands outside, its leader
    /// gets the order to that flag (the planner's own flag order) and the ship flies home at once. The run ends when the flag is the
    /// computer's, when nobody of that squad is left, or after <see cref="ZWorld.CpuShuttleCaptureWatchSec"/> s; a ship lost in a run ends it
    /// too.</item>
    /// <item>COOLDOWN: the next run takes off no sooner than <see cref="ZWorld.CpuShuttleCooldownSec"/> s after a run ended (the owner: at
    /// least two minutes, so the computer does not fly all the time), and the first one no sooner than <see cref="ZWorld.CpuShuttleFirstSec"/>
    /// s into the match. The Easy computer's clock is 1.3 x slower here too (K434), and it does nothing before its start delay.</item>
    /// <item>THE OTHER RULES: a ship of the computer stays out of its order rounds (as before), out of the idle sweep (DV70), and only this plan
    /// orders it; the squads it sends are busy to the planner while they walk to a ship or to their flag, as every unit with an order. For
    /// the other computer rules: <see cref="ZObject.CpuShuttleHeld"/> (a ship on its run, the squads of the plan) and
    /// <see cref="ZWorld.CpuShuttleRepairSend"/> (the one repair order a computer may give its ship; a ship taken for a repair ends its run).</item>
    /// </list>
    /// Deterministic (lockstep, replays): game time, the object list's order, whole-number positions, ids on ties; no random draw (neither
    /// the server's nor the bot's), no frame time, no view state. Every order is the player's own (SEND_WAYPOINTS ENTER / MOVE, EJECT_VEHICLE),
    /// the ship's flight goal is set as SEND_WAYPOINTS sets it for a human team's ship.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV138: the one switch. The game sets it at every level start, off in 'Zod exact'. Headless default off.</summary>
        public static bool CpuShuttleDeviation = false;

        /// <summary>At least this long (s) between the end of one run and the next take-off.</summary>
        public const double CpuShuttleCooldownSec = 120.0;
        /// <summary>No run takes off before this game time (s).</summary>
        public const double CpuShuttleFirstSec = 120.0;
        /// <summary>The squad is called to the ship this long (s) before the cooldown ends.</summary>
        public const double CpuShuttleBoardLeadSec = 30.0;
        /// <summary>Limits of the steps (s): boarding, the flight to a target, the unload, the flight home, the capture after the unload.</summary>
        public const double CpuShuttleBoardSec = 90.0, CpuShuttleFlightSec = 150.0, CpuShuttleUnloadSec = 30.0, CpuShuttleHomeSec = 150.0, CpuShuttleCaptureWatchSec = 150.0;
        /// <summary>An empty ship: none is taken before this game time (s); the squad ordered into it gets CpuShuttleCaptureSec; a ship is
        /// tried at most CpuShuttleCaptureTries times.</summary>
        public const double CpuShuttleEmptyFromSec = 90.0, CpuShuttleCaptureSec = 120.0;
        public const int CpuShuttleCaptureTries = 4;
        /// <summary>The defence measure (px): the other side's robots and vehicles within CpuShuttleSafeUnitPx; its manned guns whose reach +
        /// CpuShuttleGunMarginPx covers the point; its forts whose gun's reach + CpuShuttleFortMarginPx reaches the point from the walls.</summary>
        public const int CpuShuttleSafeUnitPx = 192, CpuShuttleGunMarginPx = 32, CpuShuttleFortMarginPx = 32;
        /// <summary>The landing spot near a flag: rings of tile middles this many tiles out; at most this many full landing tests per flag.</summary>
        public const int CpuShuttleSpotMinTiles = 3, CpuShuttleSpotMaxTiles = 10, CpuShuttleSpotTests = 24;
        /// <summary>A flag nearer the ship than this (px) is left to the walkers.</summary>
        public const int CpuShuttleMinFlightPx = 320;
        /// <summary>A squad is called to the ship from at most this far (px); to an empty ship from at most CpuShuttleCaptureMaxPx.</summary>
        public const int CpuShuttleBoardMaxPx = 640, CpuShuttleCaptureMaxPx = 1600;
        /// <summary>Farther than this (px) from its home, a ship with nobody aboard flies home first.</summary>
        public const int CpuShuttleHomeNearPx = 480;
        /// <summary>The home: rings of tile middles round the own fort, this many tiles out.</summary>
        public const int CpuShuttleHomeMinTiles = 6, CpuShuttleHomeMaxTiles = 24;
        /// <summary>The detour: a middle point to the side of the straight line, CpuShuttleViaStepPx apart, at most CpuShuttleViaSteps each way.</summary>
        public const int CpuShuttleViaStepPx = 96, CpuShuttleViaSteps = 8;
        /// <summary>The way: the other side's robots (1 each) and vehicles (3 each) within CpuShuttlePathUnitPx of the line (px), at most this
        /// count; a turn to another target at most CpuShuttleTurnsMax times a run, a new way to the same spot at most CpuShuttleReplansMax.</summary>
        public const int CpuShuttlePathUnitsMax = 2, CpuShuttlePathUnitPx = 160, CpuShuttleTurnsMax = 4, CpuShuttleReplansMax = 2;

        /// <summary>Checks and reports only (never the game): the switch off until the result is disposed, then as it was.</summary>
        public static IDisposable CpuShuttleOff() => new CpuShuttleOffHold();
        sealed class CpuShuttleOffHold : IDisposable
        {
            readonly bool was = CpuShuttleDeviation;
            public CpuShuttleOffHold() { CpuShuttleDeviation = false; }
            public void Dispose() { CpuShuttleDeviation = was; }
        }

        /// <summary>DV118 / DV138: the shuttle's door and an empty shuttle take squads of team t: a human team, or (DV138 on) a team a
        /// computer player drives. The switch off: exactly ZWorld.IsHumanTeam.</summary>
        internal bool ShuttleTeamCanUse(Team t) => IsHumanTeam(t) || (CpuShuttleDeviation && CpuShuttleDriven(t));

        /// <summary>A computer player that is not ignored drives team t.</summary>
        internal bool CpuShuttleDriven(Team t)
        {
            if (t == Team.Neutral) return false;
            foreach (var b in Bots) if (b.Team == t && !b.Ignored) return true;
            return false;
        }

        /// <summary>DV138: the reach (px) of a fort as the defence measure counts it: the fort gun's reach + CpuShuttleFortMarginPx.</summary>
        public int CpuShuttleFortReachPx()
        {
            var hw = S != null ? S.Unit("howitzer") : null;
            return (hw != null ? ZObject.FortGunRange(hw) : 232) + CpuShuttleFortMarginPx;
        }

        static long BoxGap2(ZObject o, long px, long py)
        {
            long ex = Math.Max(0, Math.Max(o.LocX - px, px - (o.LocX + o.WidthPix)));
            long ey = Math.Max(0, Math.Max(o.LocY - py, py - (o.LocY + o.HeightPix)));
            return ex * ex + ey * ey;
        }

        /// <summary>DV138, the defence measure of the point (px) against team me: the other side's robots (1 each) and vehicles (3 each, not
        /// shuttles) within CpuShuttleSafeUnitPx, its manned guns whose reach + CpuShuttleGunMarginPx covers it (3 each), its forts whose
        /// reach (CpuShuttleFortReachPx) gets there from the walls (100 each). 0 = undefended.</summary>
        public int CpuShuttleThreat(Team me, int px, int py) => CpuShuttleThreat(CpuShuttleFoes(me, cpuFoes), px, py);

        readonly List<ZObject> cpuFoes = new List<ZObject>();

        /// <summary>DV138: what of the other side the defence measure looks at (robots, vehicles that are not shuttles, manned guns, forts),
        /// in the object list's order, into list (cleared first).</summary>
        internal List<ZObject> CpuShuttleFoes(Team me, List<ZObject> list)
        {
            list.Clear();
            foreach (var o in Objects)
            {
                if (o.Destroyed || o.KillAt >= 0 || AreAllied(me, o.Owner) || o.Owner == Team.Neutral) continue;
                if (o.IsFort || o.Kind == ZKind.Cannon || o.Kind == ZKind.Robot || (o.Kind == ZKind.Vehicle && !o.IsShuttle)) list.Add(o);
            }
            return list;
        }

        /// <summary>DV138: the defence measure of the point (px) over a list made by CpuShuttleFoes (the same tick).</summary>
        internal int CpuShuttleThreat(List<ZObject> foes, int px, int py)
        {
            int n = 0, fort = -1;
            long u2 = (long)CpuShuttleSafeUnitPx * CpuShuttleSafeUnitPx;
            foreach (var o in foes)
            {
                if (o.IsFort)
                {
                    if (fort < 0) fort = CpuShuttleFortReachPx();
                    if (BoxGap2(o, px, py) <= (long)fort * fort) n += 100;
                    continue;
                }
                long dx = o.CenterX - px, dy = o.CenterY - py, d2 = dx * dx + dy * dy;
                if (o.Kind == ZKind.Cannon) { long r = o.AttackRadius + CpuShuttleGunMarginPx; if (d2 <= r * r) n += 3; continue; }
                if (d2 > u2) continue;
                n += o.Kind == ZKind.Robot ? 1 : 3;
            }
            return n;
        }

        /// <summary>DV138: the straight way (px) keeps out of every enemy fort's reach (CpuShuttleFortReachPx from the walls, sampled every 16 px)
        /// and every enemy manned gun's (its reach + CpuShuttleGunMarginPx), and passes the other side's robots (1 each) and vehicles (3 each, not
        /// shuttles) within CpuShuttlePathUnitPx to a count of at most CpuShuttlePathUnitsMax (two robots; no vehicle).</summary>
        public bool CpuShuttleLineSafe(Team me, int x0, int y0, int x1, int y1)
        {
            double ax = x1 - x0, ay = y1 - y0, len2 = ax * ax + ay * ay, len = Math.Sqrt(len2);
            int steps = Math.Max(1, (int)Math.Ceiling(len / 16.0)), fort = -1, units = 0;
            double SegD2(int px, int py)
            {
                double u = len2 > 0 ? ((px - x0) * ax + (py - y0) * ay) / len2 : 0.0;
                u = u < 0 ? 0 : u > 1 ? 1 : u;
                double qx = x0 + u * ax - px, qy = y0 + u * ay - py;
                return qx * qx + qy * qy;
            }
            foreach (var o in Objects)
            {
                if (o.Destroyed || o.KillAt >= 0 || AreAllied(me, o.Owner) || o.Owner == Team.Neutral) continue;
                if (o.IsFort)
                {
                    if (fort < 0) fort = CpuShuttleFortReachPx();
                    for (int i = 0; i <= steps; i++)
                        if (BoxGap2(o, x0 + (long)(x1 - x0) * i / steps, y0 + (long)(y1 - y0) * i / steps) <= (long)fort * fort) return false;
                    continue;
                }
                if (o.Kind == ZKind.Cannon) { double r = o.AttackRadius + CpuShuttleGunMarginPx; if (SegD2(o.CenterX, o.CenterY) <= r * r) return false; continue; }
                if (o.Kind != ZKind.Robot && !(o.Kind == ZKind.Vehicle && !o.IsShuttle)) continue;
                if (SegD2(o.CenterX, o.CenterY) <= (double)CpuShuttlePathUnitPx * CpuShuttlePathUnitPx && (units += o.Kind == ZKind.Robot ? 1 : 3) > CpuShuttlePathUnitsMax) return false;
            }
            return true;
        }

        /// <summary>DV138: the cheap part of a landing spot (px, a tile middle): every tile within two tiles is ground or road open for vehicles.</summary>
        internal bool CpuShuttleSpotCheap(int x, int y)
        {
            if (Map == null) return false;
            int tx = x >> 4, ty = y >> 4;
            for (int j = -2; j <= 2; j++)
                for (int i = -2; i <= 2; i++)
                {
                    int ax = tx + i, ay = ty + j;
                    if (ax < 0 || ay < 0 || ax >= Map.width || ay >= Map.height) return false;
                    var c = Map.ClassAt(ax, ay);
                    if (c != ZClass.Ground && c != ZClass.Road) return false;
                    if (!Path.TilePassable(ax, ay, false)) return false;
                }
            return true;
        }

        /// <summary>DV138: the ship s may be sent down at (x, y) (a tile middle): inside the map's edge band, no own repair station's deck, out of
        /// every exit lane (and, home: off the forts' gate faces), and the landing spot rule lets it down there now (units counted).</summary>
        internal bool CpuShuttleSpotOk(ZObject s, int x, int y, bool home)
        {
            var (cx, cy) = ShuttleClamp(x, y);
            if (cx != x || cy != y) return false;
            if (ShuttleStationAt(x, y, s) != null) return false;
            if (InExitLane(x, y, home ? 40 : 26)) return false;
            if (home && NearGateFace(x - 40, y - 40, x + 40, y + 40, 48)) return false;
            return ShuttlePadFree(x, y, s, out _);
        }

        /// <summary>DV138: the computer's ship s flies to (x, y) (px), through (vx, vy) first when vx >= 0 (a DV125 route): what SEND_WAYPOINTS
        /// does for a human team's ship given one MOVE (or two), without the click's repair-station case (a target is never one).</summary>
        internal void CpuShuttleFly(ZObject s, int x, int y, int vx, int vy)
        {
            s.ShuttleAirExitOff();
            if (vx >= 0 && ShuttleQueueOrder(s, new List<ZWaypoint> { ZWaypoint.Move(vx, vy), ZWaypoint.Move(x, y) })) return;
            s.ShuttleRepairLeave();
            var (gx, gy) = ShuttleClamp(x, y);
            s.ShuttleSetGoal(gx, gy);
        }

        /// <summary>DV138, for the computer's repairs (DV137): the computer's repair order to a ship of its own. ShuttleOrder refuses every
        /// order of a computer team (this rule keeps that); this is the one way in: the same trip a human team's UNIT_REPAIR gives (item 271:
        /// onto the station's deck, repaired, then back). False = refused: the switch off, not a ship of a team a computer player drives with
        /// its pilot aboard, or the station does not take it (ShuttleRepairOrder's own tests). Nothing of this rule calls it.</summary>
        public bool CpuShuttleRepairSend(Team team, ZObject s, int stationId)
        {
            if (!CpuShuttleDeviation || s == null || !s.IsShuttle || s.Destroyed || s.KillAt >= 0 || s.Owner != team || s.Drivers.Count == 0 || !CpuShuttleDriven(team)) return false;
            s.ShuttleAirExitOff();
            return ShuttleRepairOrder(s, stationId, true);
        }

        /// <summary>DV138: o is held by its computer's shuttle plan now (ZObject.CpuShuttleHeld).</summary>
        internal bool CpuShuttleHolds(ZObject o)
        {
            if (!CpuShuttleDeviation || o == null || GameOver) return false;
            foreach (var b in Bots) if (b.Team == o.Owner && !b.Ignored && b.CpuShuttleHolds(o)) return true;
            return false;
        }
    }

    public partial class ZObject
    {
        /// <summary>DV138: held by its computer's shuttle plan (the switch on): the ship of a run step (boarding, the flight, the unload, the
        /// flight home), the squad leader walking into a ship of the plan, the squad sent from a ship to its flag. For the other computer rules
        /// (the computer's repairs, DV137: a ship on its run is not taken). Read only; the plan's own state is its only source.</summary>
        public bool CpuShuttleHeld => ZWorld.CpuShuttleDeviation && World != null && World.CpuShuttleHolds(this);
    }

    /// <summary>What the computer's shuttle plan did (DV138): read by the checks.</summary>
    public enum CpuShuttleEventKind { Board, Boarded, BoardFailed, TakeOff, Divert, Hover, Landed, Unloaded, Captured, CaptureFailed, RunEnd, Home, HomeLanded, Abort, Lost, EmptyOrder, EmptyTaken, EmptyFailed, NoTarget }

    /// <summary>One row of the plan's log (DV138). threatFlag / threatSpot: the defence measure of the flag and of the landing spot at that moment.</summary>
    public struct CpuShuttleEvent
    {
        public double time;
        public CpuShuttleEventKind kind;
        public int ship, unit, flag, x, y, threatFlag, threatSpot, aboard, viaX, viaY;
        public string note;
    }

    public sealed partial class ZBot
    {
        enum CsPhase { Idle, Boarding, Flying, Unloading, Returning }
        CsPhase csPhase;
        int csShip = -1, csLeader = -1, csFlag = -1, csSpotX, csSpotY, csBoardTries, csSpotTries, csTurns, csReplans;
        double csNextStep, csPhaseSince, csRetryAt, csCooldownUntil = -1;
        bool csRunActive;
        readonly List<(int x, int y)> csBadSpots = new List<(int x, int y)>();
        readonly Dictionary<int, (int x, int y)> csHome = new Dictionary<int, (int x, int y)>();
        readonly List<ZObject> csFoes = new List<ZObject>();
        // the capture after an unload
        int cwFlag = -1; double cwSince; readonly List<int> cwIds = new List<int>();
        // an empty ship being taken
        int ceShip = -1; double ceSince, ceNextScan;
        readonly Dictionary<int, (int fails, double next)> ceTried = new Dictionary<int, (int fails, double next)>();

        /// <summary>DV138 log (at most 2000 rows) and counts, for the checks.</summary>
        public readonly List<CpuShuttleEvent> CpuShuttleEvents = new List<CpuShuttleEvent>();
        public int CpuShuttleRuns { get; private set; }
        public int CpuShuttleLandings { get; private set; }
        public int CpuShuttleUnloads { get; private set; }
        public int CpuShuttleCaptures { get; private set; }
        public int CpuShuttleEmptyTaken { get; private set; }
        public int CpuShuttleOrders { get; private set; }
        /// <summary>DV138 state for the checks and a debug overlay.</summary>
        public string CpuShuttlePhase => csPhase.ToString();
        public int CpuShuttleShip => csShip;
        public double CpuShuttleCooldownUntil => csCooldownUntil < 0 ? ZWorld.CpuShuttleFirstSec * EasySlowScale : csCooldownUntil;
        public bool CpuShuttleRunActive => csRunActive;

        /// <summary>DV138: o is held by this plan now: its ship in a run step, the leader it sent into a ship, the squad it let out for a flag.</summary>
        internal bool CpuShuttleHolds(ZObject o)
        {
            if (o.IsShuttle) return csPhase != CsPhase.Idle && o.Id == csShip;
            if (o.Kind != ZKind.Robot) return false;
            if (cwFlag >= 0 && cwIds.Contains(o.Id)) return true;
            if (csPhase == CsPhase.Boarding && o.Id == csLeader) return true;
            if (ceShip >= 0) foreach (var wp in o.Waypoints) if (wp.mode == WpMode.Enter && wp.refId == ceShip) return true;
            return false;
        }

        void CsLog(CpuShuttleEventKind kind, ZObject s, int unit = -1, int flag = -1, int x = 0, int y = 0, int tf = -1, int ts = -1, string note = null, int vx = -1, int vy = -1)
        {
            if (CpuShuttleEvents.Count >= 2000) return;
            CpuShuttleEvents.Add(new CpuShuttleEvent
            {
                time = W.Time, kind = kind, ship = s != null ? s.Id : -1, unit = unit, flag = flag, x = x, y = y, threatFlag = tf, threatSpot = ts,
                aboard = s != null ? s.Passengers.Count : 0, note = note, viaX = vx, viaY = vy,
            });
            if (Verbose || (kind != CpuShuttleEventKind.NoTarget && kind != CpuShuttleEventKind.Hover))
                Log.Add($"{W.Time:F0}s {Team} DV138 shuttle {(s != null ? "#" + s.Id : "-")}: {kind}{(flag >= 0 ? " flag #" + flag : "")}{(unit >= 0 ? " unit #" + unit : "")}{(note != null ? " (" + note + ")" : "")}");
        }

        // ------------------------------------------------------------------ the tick (ZBot.Tick, every server tick, next to the strike groups)
        /// <summary>DV138: the computer's shuttle plan, one step a second (Normal and Easy alike). The switch off, ignored, or no shuttle made
        /// in this match: nothing.</summary>
        void CpuShuttleTick()
        {
            if (!ZWorld.CpuShuttleDeviation || Ignored || W.GameOver) return;
            if (W.Time + TIME_EPS < csNextStep) return;
            csNextStep = W.Time + 1.0;
            if (W.ShuttlesMadeCount == 0) return;
            if (Difficulty == ZBotDifficulty.Easy && W.Time + TIME_EPS < EASY_START_DELAY * CsScale) return;   // the Easy computer sits still at the start
            CsCaptureWatch();
            CsEmptyStep();
            CsShipStep();
        }

        double CsScale => EasySlowScale;

        static bool CsAlive(ZObject o) => o != null && !o.Destroyed && o.KillAt < 0;

        /// <summary>A ship of ours with its pilot aboard (one this plan may fly).</summary>
        bool CsOurShip(ZObject s) => s != null && s.IsShuttle && CsAlive(s) && s.Owner == Team && s.Drivers.Count > 0;

        static bool CsOnRepair(ZObject s) => s.ShuttleRepairTarget >= 0 || s.ShuttleOnRepairPad;

        static long CsD2(int ax, int ay, int bx, int by) => (long)(ax - bx) * (ax - bx) + (long)(ay - by) * (ay - by);

        /// <summary>One order of the plan to a unit: SEND_WAYPOINTS as the planner sends it, relayed to the bot's client view. True = the unit kept it.</summary>
        bool CsSend(ZObject u, ZWaypoint wp)
        {
            if (!W.SendWaypoints(Team, u, new[] { wp })) return false;
            if (ClientView) Relay(u);
            CpuShuttleOrders++;
            return u.Waypoints.Exists(x => x.mode == wp.mode && x.refId == wp.refId);
        }

        /// <summary>Stops a squad this plan sent (an empty list: it stands, and the planner finds it idle).</summary>
        void CsStop(ZObject u)
        {
            if (u == null || !CsAlive(u) || u.Owner != Team) return;
            if (W.SendWaypoints(Team, u, new List<ZWaypoint>())) { if (ClientView) Relay(u); CpuShuttleOrders++; }
        }

        /// <summary>The leader (not a minion) of our team whose order list holds an ENTER on the ship id, or null.</summary>
        ZObject CsEnterer(int shipId)
        {
            foreach (var o in W.Objects)
            {
                if (!o.IsRobot || o.Owner != Team || o.IsMinion || !CsAlive(o)) continue;
                foreach (var wp in o.Waypoints) if (wp.mode == WpMode.Enter && wp.refId == shipId) return o;
            }
            return null;
        }

        /// <summary>The nearest idle squad (the planner's idle list) that can walk to (x, y) (px) within maxPx; with preferCrew the squads of
        /// two or more first. Not a squad that holds a position or patrols, not one that does not board by itself (the Electrobot, heroes), not one
        /// still getting out of a carrier.</summary>
        ZObject CsIdleSquad(int x, int y, int maxPx, bool preferCrew)
        {
            var units = new List<ZObject>(); var targeted = new List<ZObject>();
            CollectOurUnits_3(units, targeted);
            ZObject best = null; long bd = long.MaxValue; bool bestCrew = false;
            foreach (var u in units)
            {
                if (u.Kind != ZKind.Robot || u.IsMinion || !CsAlive(u) || !u.CanMove || u.Stunned) continue;
                if (u.NoAutoEnter || u.HoldPosition || u.Patrolling || u.IsFortDefender || u.ExitWalking(W.Time)) continue;
                long d = CsD2(u.CenterX, u.CenterY, x, y);
                if (d > (long)maxPx * maxPx) continue;
                if (!InSameRegion(u.LocX + 8, u.LocY + 8, x, y, true)) continue;
                int bodies = 1; foreach (var m in u.Minions) if (CsAlive(m)) bodies++;
                bool crew = !preferCrew || bodies >= 2;
                if (best == null || (crew && !bestCrew) || (crew == bestCrew && (d < bd || (d == bd && u.Id < best.Id)))) { best = u; bd = d; bestCrew = crew; }
            }
            return best;
        }

        // ------------------------------------------------------------------ the capture after an unload
        void CsCaptureWatch()
        {
            if (cwFlag < 0) return;
            var f = W.Get(cwFlag);
            if (f != null && W.AreAllied(Team, f.Owner)) { CpuShuttleCaptures++; CsLog(CpuShuttleEventKind.Captured, null, flag: cwFlag, x: f.CenterX, y: f.CenterY); CsRunEnd("captured"); return; }
            bool any = false;
            foreach (int id in cwIds) { var r = W.Get(id); if (CsAlive(r) && r.Owner == Team) { any = true; break; } }
            if (f == null || !any || W.Time - cwSince > ZWorld.CpuShuttleCaptureWatchSec)
            {
                CsLog(CpuShuttleEventKind.CaptureFailed, null, flag: cwFlag, note: f == null ? "flag gone" : !any ? "squad lost" : "time");
                CsRunEnd("capture failed");
            }
        }

        /// <summary>A run ends (once): the cooldown starts now.</summary>
        void CsRunEnd(string why)
        {
            cwFlag = -1; cwIds.Clear();
            if (!csRunActive) return;
            csRunActive = false;
            csCooldownUntil = W.Time + ZWorld.CpuShuttleCooldownSec * CsScale;
            CsLog(CpuShuttleEventKind.RunEnd, null, note: why);
        }

        // ------------------------------------------------------------------ empty ships
        void CsEmptyStep()
        {
            if (ceShip >= 0)
            {
                var v = W.Get(ceShip);
                if (v != null && CsAlive(v) && v.Owner == Team)
                {
                    CpuShuttleEmptyTaken++; CsLog(CpuShuttleEventKind.EmptyTaken, v); ceShip = -1;
                    return;
                }
                var walker = CsEnterer(ceShip);
                bool fail = v == null || !CsAlive(v) || v.Owner != Team.Neutral || walker == null || W.Time - ceSince > ZWorld.CpuShuttleCaptureSec;
                if (!fail) return;
                if (walker != null) CsStop(walker);
                ceTried.TryGetValue(ceShip, out var t0);
                int fails = t0.fails + 1;
                ceTried[ceShip] = (fails, W.Time + 60.0 * (1 << Math.Min(fails - 1, 3)));
                CsLog(CpuShuttleEventKind.EmptyFailed, v, note: v == null || !CsAlive(v) ? "ship gone" : v.Owner != Team.Neutral ? "taken by " + v.Owner : walker == null ? "squad lost or stopped" : "time");
                ceShip = -1;
                return;
            }
            if (W.Time + TIME_EPS < ceNextScan || W.Time + TIME_EPS < ZWorld.CpuShuttleEmptyFromSec * CsScale) return;   // the player has the first minute and a half for an empty ship of his side
            ceNextScan = W.Time + 5.0;
            foreach (var o in W.Objects)
            {
                if (!o.IsShuttle || !o.ShuttleIsEmpty || !CsAlive(o) || o.ShuttleState != ZShuttleState.Landed || o.ShuttleLift > 0) continue;
                if (ceTried.TryGetValue(o.Id, out var t) && (t.fails >= ZWorld.CpuShuttleCaptureTries || W.Time + TIME_EPS < t.next)) continue;
                if (W.CpuShuttleThreat(Team, o.CenterX, o.CenterY) > 0) continue;
                var (bx, by) = o.ShuttleBackPoint(0);
                var u = CsIdleSquad(bx, by, ZWorld.CpuShuttleCaptureMaxPx, true);
                if (u == null) continue;
                var wp = new ZWaypoint { mode = WpMode.Enter, refId = o.Id, x = o.CenterX, y = o.CenterY, attackTo = false, playerGiven = true };
                if (!CsSend(u, wp)) { ceTried[o.Id] = (t.fails + 1, W.Time + 60.0); continue; }
                ceShip = o.Id; ceSince = W.Time;
                CsLog(CpuShuttleEventKind.EmptyOrder, o, unit: u.Id, x: o.CenterX, y: o.CenterY, tf: 0);
                return;
            }
        }

        // ------------------------------------------------------------------ the ship
        void CsShipStep()
        {
            var s = csShip >= 0 ? W.Get(csShip) : null;
            if (csPhase != CsPhase.Idle)
            {
                bool lost = !CsOurShip(s), repair = !lost && CsOnRepair(s);
                if (lost || repair)
                {
                    if (lost) CsLog(CpuShuttleEventKind.Lost, s ?? W.GetAny(csShip), note: s == null || s.Destroyed || s.KillAt >= 0 ? "destroyed" : "no longer ours");
                    else CsLog(CpuShuttleEventKind.Abort, s, note: "taken for a repair");
                    if (csPhase == CsPhase.Boarding) { CsStop(W.Get(csLeader)); }
                    if (csRunActive && cwFlag < 0) CsRunEnd(lost ? "ship lost" : "repair");
                    CsIdle(lost ? -1 : 5.0);
                    return;
                }
            }
            switch (csPhase)
            {
                case CsPhase.Idle: CsIdleStep(); break;
                case CsPhase.Boarding: CsBoardingStep(s); break;
                case CsPhase.Flying: CsFlyingStep(s); break;
                case CsPhase.Unloading: CsUnloadingStep(s); break;
                case CsPhase.Returning: CsReturningStep(s); break;
            }
        }

        void CsIdle(double retry)
        {
            csPhase = CsPhase.Idle; csLeader = -1; csPhaseSince = W.Time;
            if (retry < 0) csShip = -1;
            if (retry > 0) csRetryAt = Math.Max(csRetryAt, W.Time + retry);
        }

        void CsIdleStep()
        {
            if (W.Time + TIME_EPS < csRetryAt) return;
            ZObject s = null;
            foreach (var o in W.Objects)
            {
                if (!CsOurShip(o) || CsOnRepair(o) || o.ShuttleExitPending) continue;
                if (s == null || (o.Passengers.Count > 0 && s.Passengers.Count == 0)) s = o;
            }
            if (s == null) { csShip = -1; return; }
            csShip = s.Id;
            if (s.ShuttleAirborne)
            {
                if (s.ShuttleState == ZShuttleState.Hover) CsGoHome(s, "it hovered with no order");
                return;
            }
            double now = W.Time, until = CpuShuttleCooldownUntil;
            if (s.Passengers.Count > 0)
            {
                if (csRunActive || now + TIME_EPS < until || !s.ShuttleCrewSettled) return;           // its squad aboard: it waits for the cooldown's end
                CsLaunch(s);
                return;
            }
            var home = CsHomeOf(s);
            if (home.x >= 0 && CsD2(s.ShuttleLineX, s.ShuttleLineY, home.x, home.y) > (long)ZWorld.CpuShuttleHomeNearPx * ZWorld.CpuShuttleHomeNearPx)
            {
                CsGoHome(s, "to its home");
                return;
            }
            if (csRunActive || now + TIME_EPS < until - ZWorld.CpuShuttleBoardLeadSec * CsScale) return;
            CsBoard(s);
        }

        void CsBoard(ZObject s)
        {
            if (!s.CanTakePassengers(Team)) { csRetryAt = W.Time + 5.0; return; }
            var (bx, by) = s.ShuttleBackPoint(0);
            var u = CsIdleSquad(bx, by, ZWorld.CpuShuttleBoardMaxPx, false);
            if (u == null) { csRetryAt = W.Time + 10.0; return; }
            var wp = new ZWaypoint { mode = WpMode.Enter, refId = s.Id, x = s.CenterX, y = s.CenterY, attackTo = false, playerGiven = true };
            if (!CsSend(u, wp)) { csRetryAt = W.Time + 10.0; return; }
            csPhase = CsPhase.Boarding; csLeader = u.Id; csPhaseSince = W.Time;
            CsLog(CpuShuttleEventKind.Board, s, unit: u.Id);
        }

        void CsBoardingStep(ZObject s)
        {
            if (s.Passengers.Count > 0)
            {
                if (!s.ShuttleCrewSettled) return;                                                      // still walking up the ramp
                CsLog(CpuShuttleEventKind.Boarded, s);
                csBoardTries = 0;
                CsIdle(0);
                return;
            }
            var walker = CsEnterer(s.Id);
            bool timeUp = W.Time - csPhaseSince > ZWorld.CpuShuttleBoardSec * CsScale;
            if (walker != null && !timeUp) return;
            if (walker != null) CsStop(walker);
            csBoardTries++;
            CsLog(CpuShuttleEventKind.BoardFailed, s, unit: csLeader, note: timeUp ? "time" : "squad lost or stopped");
            if (csBoardTries >= 3) { csBoardTries = 0; CsIdle(60.0); }
            else CsIdle(5.0);
        }

        /// <summary>The take-off of a run: the target by the rule, the flight there.</summary>
        void CsLaunch(ZObject s)
        {
            csBadSpots.Clear();
            if (!CsPickTarget(s, -1, out var f, out int sx, out int sy, out int vx, out int vy))
            {
                csRetryAt = W.Time + 10.0;
                var last = CpuShuttleEvents.Count > 0 ? CpuShuttleEvents[CpuShuttleEvents.Count - 1] : default;
                if (CpuShuttleEvents.Count == 0 || last.kind != CpuShuttleEventKind.NoTarget || last.ship != s.Id) CsLog(CpuShuttleEventKind.NoTarget, s);   // once until something else happens
                return;
            }
            int tf = W.CpuShuttleThreat(Team, f.CenterX, f.CenterY), ts = W.CpuShuttleThreat(Team, sx, sy);
            W.CpuShuttleFly(s, sx, sy, vx, vy);
            csPhase = CsPhase.Flying; csFlag = f.Id; csSpotX = sx; csSpotY = sy; csPhaseSince = W.Time; csSpotTries = 0; csTurns = 0; csReplans = 0;
            csRunActive = true; CpuShuttleRuns++;
            CsLog(CpuShuttleEventKind.TakeOff, s, flag: f.Id, x: sx, y: sy, tf: tf, ts: ts, vx: vx, vy: vy);
        }

        /// <summary>The target: an undefended foreign flag (not the one skipped), its landing spot and the way there (vx = -1: straight).</summary>
        bool CsPickTarget(ZObject s, int skipFlag, out ZObject flag, out int sx, out int sy, out int vx, out int vy)
        {
            flag = null; sx = sy = 0; vx = vy = -1;
            var cands = new List<(ZObject f, bool walked, long d)>();
            long min2 = (long)ZWorld.CpuShuttleMinFlightPx * ZWorld.CpuShuttleMinFlightPx;
            var foes = W.CpuShuttleFoes(Team, csFoes);
            foreach (var f in W.Flags)
            {
                if (f == null || Gone(f) || W.AreAllied(Team, f.Owner) || f.Id == skipFlag) continue;
                long d = CsD2(s.ShuttleLineX, s.ShuttleLineY, f.CenterX, f.CenterY);
                if (d < min2) continue;
                if (W.CpuShuttleThreat(foes, f.CenterX, f.CenterY) > 0) continue;
                cands.Add((f, CsWalkedTo(f), d));
            }
            cands.Sort((a, b) => a.walked != b.walked ? (a.walked ? 1 : -1) : a.d != b.d ? a.d.CompareTo(b.d) : a.f.Id.CompareTo(b.f.Id));
            int searched = 0;
            foreach (var c in cands)
            {
                if (searched++ >= 6) break;
                if (!CsSpotNear(s, c.f, out int x, out int y)) continue;
                if (!CsWay(s, x, y, out int ax, out int ay)) continue;
                flag = c.f; sx = x; sy = y; vx = ax; vy = ay;
                return true;
            }
            return false;
        }

        /// <summary>One of our units (not a shuttle) has an order to that flag, as the bot knows it.</summary>
        bool CsWalkedTo(ZObject f)
        {
            foreach (var o in W.Objects)
            {
                if (!o.IsMobile || o.Owner != Team || o.IsMinion || o.IsShuttle || Gone(o)) continue;
                var wps = KnownWaypoints(o);
                if (wps.Count > 0 && wps[0].refId == f.Id) return true;
            }
            return false;
        }

        /// <summary>The way from the ship to (x, y): straight when it keeps out of every enemy fort's and gun's reach, else through one middle
        /// point to the side (DV125 routes on); false = no such way.</summary>
        bool CsWay(ZObject s, int x, int y, out int vx, out int vy)
        {
            vx = vy = -1;
            int x0 = s.ShuttleLineX, y0 = s.ShuttleLineY;
            if (W.CpuShuttleLineSafe(Team, x0, y0, x, y)) return true;
            if (!ZWorld.ShuttleQueueDeviation) return false;
            double dx = x - x0, dy = y - y0, len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) return false;
            double nx = -dy / len, ny = dx / len;
            int mx = (x0 + x) / 2, my = (y0 + y) / 2;
            for (int k = 1; k <= ZWorld.CpuShuttleViaSteps; k++)
                foreach (int sg in new[] { 1, -1 })
                {
                    var (cx, cy) = W.ShuttleClamp(mx + (int)Math.Round(sg * nx * k * ZWorld.CpuShuttleViaStepPx), my + (int)Math.Round(sg * ny * k * ZWorld.CpuShuttleViaStepPx));
                    if (W.CpuShuttleLineSafe(Team, x0, y0, cx, cy) && W.CpuShuttleLineSafe(Team, cx, cy, x, y)) { vx = cx; vy = cy; return true; }
                }
            return false;
        }

        bool CsBadSpot(int x, int y)
        {
            foreach (var b in csBadSpots) if (CsD2(b.x, b.y, x, y) <= 32 * 32) return true;
            return false;
        }

        /// <summary>A landing spot for the ship 3 to 10 tiles from flag f in the flag's territory (ring by ring, in each ring the nearest to the
        /// ship first): the squad can walk from it to the flag, nobody of the other side defends it, the landing spot rule takes the ship there now.</summary>
        bool CsSpotNear(ZObject s, ZObject f, out int sx, out int sy)
        {
            sx = sy = 0;
            int ftx = f.CenterX >> 4, fty = f.CenterY >> 4, tests = 0, zone = f.ZoneIndex >= 0 ? f.ZoneIndex : W.GetZoneIndex(f.CenterX, f.CenterY);
            TargetPoint(f, out int rx, out int ry);
            var foes = W.CpuShuttleFoes(Team, csFoes);
            var ring = new List<(int x, int y, long d)>();
            for (int r = ZWorld.CpuShuttleSpotMinTiles; r <= ZWorld.CpuShuttleSpotMaxTiles; r++)
            {
                ring.Clear();
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int x = (ftx + dx) * 16 + 8, y = (fty + dy) * 16 + 8;
                        ring.Add((x, y, CsD2(x, y, s.ShuttleLineX, s.ShuttleLineY)));
                    }
                ring.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
                foreach (var p in ring)
                {
                    if (CsBadSpot(p.x, p.y) || W.GetZoneIndex(p.x, p.y) != zone || !W.CpuShuttleSpotCheap(p.x, p.y)) continue;   // in the flag's own territory
                    if (!InSameRegion(p.x, p.y, rx, ry, true)) continue;
                    if (W.CpuShuttleThreat(foes, p.x, p.y) > 0) continue;
                    if (++tests > ZWorld.CpuShuttleSpotTests) return false;
                    if (!W.CpuShuttleSpotOk(s, p.x, p.y, false)) continue;
                    sx = p.x; sy = p.y;
                    return true;
                }
            }
            return false;
        }

        /// <summary>The ship's home (px; x = -1: none): a landing spot near our fort (else our first producer), found once, looked at again
        /// when it no longer takes the ship.</summary>
        (int x, int y) CsHomeOf(ZObject s, bool recheck = false)
        {
            if (csHome.TryGetValue(s.Id, out var h) && h.x >= 0 && (!recheck || W.CpuShuttleSpotOk(s, h.x, h.y, true) || (s.ShuttleLineX == h.x && s.ShuttleLineY == h.y && !s.ShuttleAirborne)))
                return h;
            ZObject b = null; int gx = 0, gy = 0;
            foreach (var o in W.Objects) if (o.IsFort && o.Owner == Team && CsAlive(o) && ZWorld.StrikeGate(o, out gx, out gy, out _, out _, out _)) { b = o; break; }   // the fort and its gate's way out
            if (b == null)
                foreach (var o in W.Objects)
                    if (o.Kind == ZKind.Building && o.Owner == Team && CsAlive(o) && (o.IsFort || o.IsProducer)) { b = o; gx = o.CenterX; gy = o.CenterY; break; }
            if (b == null) { csHome[s.Id] = (-1, -1); return (-1, -1); }
            int bx = b.CenterX, by = b.CenterY, btx = bx >> 4, bty = by >> 4;
            var ring = new List<(int x, int y, long d)>();
            int tests = 0;
            var foes = W.CpuShuttleFoes(Team, csFoes);
            for (int r = ZWorld.CpuShuttleHomeMinTiles; r <= ZWorld.CpuShuttleHomeMaxTiles; r++)
            {
                ring.Clear();
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int x = (btx + dx) * 16 + 8, y = (bty + dy) * 16 + 8;
                        ring.Add((x, y, CsD2(x, y, bx, by)));
                    }
                ring.Sort((a, c) => a.d != c.d ? a.d.CompareTo(c.d) : a.y != c.y ? a.y.CompareTo(c.y) : a.x.CompareTo(c.x));
                foreach (var p in ring)
                {
                    if (CsBadSpot(p.x, p.y) || !W.CpuShuttleSpotCheap(p.x, p.y)) continue;
                    if (!InSameRegion(p.x, p.y, gx, gy, true)) continue;
                    if (W.CpuShuttleThreat(foes, p.x, p.y) > 0) continue;
                    if (++tests > 80) break;
                    if (!W.CpuShuttleSpotOk(s, p.x, p.y, true)) continue;
                    csHome[s.Id] = (p.x, p.y);
                    return (p.x, p.y);
                }
                if (tests > 80) break;
            }
            csHome[s.Id] = (-1, -1);
            return (-1, -1);
        }

        void CsGoHome(ZObject s, string why)
        {
            var h = CsHomeOf(s, true);
            if (h.x < 0 || CsD2(s.ShuttleLineX, s.ShuttleLineY, h.x, h.y) <= 24 * 24) { if (!s.ShuttleAirborne) { CsIdle(10.0); return; } h = (s.ShuttleLineX, s.ShuttleLineY); }
            CsWay(s, h.x, h.y, out int vx, out int vy);
            W.CpuShuttleFly(s, h.x, h.y, vx, vy);
            csPhase = CsPhase.Returning; csPhaseSince = W.Time; csSpotX = h.x; csSpotY = h.y; csSpotTries = 0;
            CsLog(CpuShuttleEventKind.Home, s, x: h.x, y: h.y, note: why, vx: vx, vy: vy);
        }

        void CsFlyingStep(ZObject s)
        {
            var f = W.Get(csFlag);
            if (s.ShuttleState == ZShuttleState.Landed && s.ShuttleLift <= 0 && W.Time > csPhaseSince + 1.0 && !s.ShuttleTakeOffHeld
                && CsD2(s.ShuttleLineX, s.ShuttleLineY, csSpotX, csSpotY) <= 64 * 64)
            {
                CpuShuttleLandings++;
                CsLog(CpuShuttleEventKind.Landed, s, flag: csFlag, x: s.ShuttleLineX, y: s.ShuttleLineY,
                      tf: f != null ? W.CpuShuttleThreat(Team, f.CenterX, f.CenterY) : -1, ts: W.CpuShuttleThreat(Team, s.ShuttleLineX, s.ShuttleLineY));
                csPhase = CsPhase.Unloading; csPhaseSince = W.Time;
                W.Eject(Team, s);
                return;
            }
            if (s.ShuttleAirborne || s.ShuttleTakeOffHeld)                                              // up there (coming down too: a new goal takes it up again) or about to take off
            {
                bool bad = f == null || Gone(f) || W.AreAllied(Team, f.Owner) || W.CpuShuttleThreat(Team, f.CenterX, f.CenterY) > 0 || W.CpuShuttleThreat(Team, csSpotX, csSpotY) > 0;
                if (bad || s.ShuttleState == ZShuttleState.Hover)
                {
                    if (!bad) { CsLog(CpuShuttleEventKind.Hover, s, flag: csFlag, x: csSpotX, y: csSpotY, note: s.ShuttleNoLand); csBadSpots.Add((csSpotX, csSpotY)); csSpotTries++; }
                    int sx = 0, sy = 0, vx = -1, vy = -1; ZObject nf = null;
                    bool found = csTurns < ZWorld.CpuShuttleTurnsMax && !bad && csSpotTries <= 3 && CsSpotNear(s, f, out sx, out sy) && CsWay(s, sx, sy, out vx, out vy);
                    if (found) nf = f;
                    else found = csTurns < ZWorld.CpuShuttleTurnsMax && CsPickTarget(s, bad ? -1 : csFlag, out nf, out sx, out sy, out vx, out vy);
                    if (found)
                    {
                        W.CpuShuttleFly(s, sx, sy, vx, vy);
                        csFlag = nf.Id; csSpotX = sx; csSpotY = sy; csPhaseSince = W.Time; csTurns++; csReplans = 0;
                        CsLog(CpuShuttleEventKind.Divert, s, flag: nf.Id, x: sx, y: sy, tf: W.CpuShuttleThreat(Team, nf.CenterX, nf.CenterY), ts: W.CpuShuttleThreat(Team, sx, sy),
                              note: bad ? "target taken or defended" : "could not land", vx: vx, vy: vy);
                        return;
                    }
                    CsRunEnd(bad ? "target taken or defended, no other" : "could not land, no other");
                    CsGoHome(s, "no target");
                    return;
                }
                // the way still to fly: the other side moved under it (a vehicle, robots, a gun): a new way to the same spot, twice a run at most
                bool flying = s.ShuttleState == ZShuttleState.Climb || (s.ShuttleState == ZShuttleState.Cruise && !s.ShuttleLandTurning && !s.ShuttleDeckTurning);
                if (flying && csReplans < ZWorld.CpuShuttleReplansMax)
                {
                    int lx = s.ShuttleLineX, ly = s.ShuttleLineY, tx = s.ShuttleSteerX, ty = s.ShuttleSteerY;
                    bool safe = W.CpuShuttleLineSafe(Team, lx, ly, tx, ty) && (s.ShuttleViaCount == 0 || W.CpuShuttleLineSafe(Team, tx, ty, s.ShuttleGoalX, s.ShuttleGoalY));
                    if (!safe && CsWay(s, csSpotX, csSpotY, out int wx, out int wy))
                    {
                        W.CpuShuttleFly(s, csSpotX, csSpotY, wx, wy);
                        csReplans++;
                        CsLog(CpuShuttleEventKind.Divert, s, flag: csFlag, x: csSpotX, y: csSpotY, tf: f != null ? W.CpuShuttleThreat(Team, f.CenterX, f.CenterY) : -1, ts: W.CpuShuttleThreat(Team, csSpotX, csSpotY),
                              note: "a new way: the other side under the old one", vx: wx, vy: wy);
                        return;
                    }
                }
            }
            if (W.Time - csPhaseSince > ZWorld.CpuShuttleFlightSec)
            {
                CsRunEnd("flight took too long");
                CsGoHome(s, "flight took too long");
            }
        }

        void CsUnloadingStep(ZObject s)
        {
            if (s.Passengers.Count == 0 && s.ShuttleLastRampUse == ZShuttleRampUse.Unloaded && s.ShuttleFootIds.Count > 0)
            {
                ZObject lead = null;
                cwIds.Clear();
                foreach (int id in s.ShuttleFootIds)
                {
                    var r = W.Get(id);
                    if (!CsAlive(r) || r.Owner != Team) continue;
                    cwIds.Add(id);
                    if (lead == null && !r.IsMinion) lead = r;
                }
                CpuShuttleUnloads++;
                var f = W.Get(csFlag);
                bool order = lead != null && f != null && !Gone(f) && !W.AreAllied(Team, f.Owner);
                if (order) CsSend(lead, new ZWaypoint { mode = WpMode.Move, refId = f.Id, x = f.CenterX, y = f.CenterY, attackTo = true, playerGiven = true });
                CsLog(CpuShuttleEventKind.Unloaded, s, unit: lead != null ? lead.Id : -1, flag: csFlag, note: order ? null : "no flag order");
                if (order) { cwFlag = f.Id; cwSince = W.Time; }
                else CsRunEnd(lead == null ? "nobody came out" : "the flag was taken already");
                CsGoHome(s, "unloaded");
                return;
            }
            if (!s.ShuttleExitPending && s.Passengers.Count > 0 && s.CanEjectCrew()) W.Eject(Team, s);
            if (W.Time - csPhaseSince > ZWorld.CpuShuttleUnloadSec)
            {
                CsLog(CpuShuttleEventKind.Abort, s, note: "unload took too long");
                CsRunEnd("unload took too long");
                CsGoHome(s, "unload took too long");
            }
        }

        void CsReturningStep(ZObject s)
        {
            if (s.ShuttleState == ZShuttleState.Landed && s.ShuttleLift <= 0 && W.Time > csPhaseSince + 1.0 && !s.ShuttleTakeOffHeld)
            {
                CsLog(CpuShuttleEventKind.HomeLanded, s, x: s.ShuttleLineX, y: s.ShuttleLineY);
                CsIdle(0);
                return;
            }
            if (s.ShuttleState == ZShuttleState.Hover && !s.ShuttleStrafeBusy)
            {
                csBadSpots.Add((csSpotX, csSpotY)); csSpotTries++;
                csHome.Remove(s.Id);
                if (csSpotTries <= 3) { int tries = csSpotTries; CsGoHome(s, "home taken: " + s.ShuttleNoLand); csSpotTries = tries; return; }
            }
            if (W.Time - csPhaseSince > ZWorld.CpuShuttleHomeSec) { CsLog(CpuShuttleEventKind.Abort, s, note: "home flight took too long"); CsIdle(10.0); }
        }
    }
}
