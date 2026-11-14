using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// 1.2 item 13, deviation DV137 "the computer repairs its strongest damaged vehicle". Zod's computer sends a damaged vehicle to its
    /// repair station only when its order round happens to pair the two (the mutual-nearest match of zbot.cpp, with attack_to set, no way
    /// back), which is rare. With DV137 a computer player that owns a repair station sends, once every <see cref="ZWorld.CpuRepairEverySec"/>
    /// game seconds (x 1.3 on Easy, K434), its strongest damaged vehicle there and brings it back, exactly as the player's R key does:
    ///  * who: its own heavy tanks first, then missile launchers, then shuttles, else a medium tank (the mech); within a kind the one
    ///    that has lost the most hit points, then the lower id. At least <see cref="ZWorld.CpuRepairMinLost"/> of its hit points must be
    ///    lost (no trip for a scratch). Left out: a unit already on a repair trip (its own planner's or the R key's), a squad member, a fort
    ///    defender, a member of a strike group that forms or marches (DV107), a vehicle whose order cannot be replaced (a force move, a
    ///    fort entry past its first stage), a vehicle in a fight (hit by an enemy in the last <see cref="ZWorld.CpuRepairCalmSec"/> game
    ///    seconds, or firing at a target now: pulled out under fire it would seldom reach the station), a guard of the defend orders (DV136),
    ///    a ship on a run of the shuttle plan or a squad of it (DV138), a shuttle whose repair order the shuttle rule refused (that rule off,
    ///    no pilot, no deck point; tried again a period later);
    ///  * where: its nearest own repair station (squared distance between the centres, then the lower id) that the vehicle can drive to
    ///    (the bot's own map regions) and whose waiting spot it can stand on (DV62 waits 40 px out of the entrance, a test of the tiles only:
    ///    a cannon nearer than the movement layer's spacing keeps every vehicle off that spot, so the trip's approach would give up there and
    ///    the vehicle drive back unrepaired; the Jungle's gun beside the bay of its north station); a shuttle flies to any own station with a
    ///    deck point. A station whose bay the last trip could not get into (the trip's own approach gave up while the station still stood:
    ///    its waiting spot blocked) is taken again only when no other one is left, for one period;
    ///  * how: the R key's own order through the command layer, the waypoint UNIT_REPAIR {station, x = its centre - 8, y = its centre,
    ///    attack_to off}: the same sim repair trip (queue spot, the bay, the pad of DV62, healed to full, out again), followed in the same
    ///    order by a MOVE back to the point where the vehicle stood when it was sent (attack_to off): the drive back the R key gets from
    ///    DV62 for a human team, here given in the order itself (DV62 computes a human vehicle's way back from where it stood when its
    ///    repair waypoint last began, which the movement layer's step-asides can move; the order's own point cannot move). Its way into the
    ///    bay blocked, the vehicle drives back all the same. A shuttle takes the UNIT_REPAIR waypoint alone (its own flight there, the deck,
    ///    the flight back, item 271);
    ///  * on the way: the vehicle is held (<see cref="ZObject.CpuRepairHeld"/>): the computer's order rounds and its idle rules (Zod's
    ///    planner, DV56, DV37, DV72, DV60, DV70) leave it alone until the trip is over: back where it was (or standing, its waypoints done,
    ///    after a fight of its own on the way back), destroyed, lost to the other side, or after <see cref="ZWorld.CpuRepairMaxTripSec"/>
    ///    game seconds (longer for a far station: <see cref="ZWorld.CpuRepairTripSlackSec"/> + <see cref="ZWorld.CpuRepairTripDriveFactor"/> x the
    ///    straight drive there and back at the vehicle's own speed). Then the planner finds it idle and gives it its next order as usual.
    /// One trip at a time per computer player; the next one at the earliest a period after the last one was sent; with no station or no
    /// damaged vehicle of those kinds the clock waits. Deterministic: sim time and the object list order only, no random draw (neither the
    /// server's rand nor the bot's own), so lockstep and replays stay in step and the switch off leaves every tick as it was.
    /// Needs DV62 (the pad repair, ZWorld.RepairInPlaceDeviation); off in 'Zod exact'. Headless default off.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV137: the computer repairs its strongest damaged vehicle (Sim/ZBot.CpuRepair.cs). The game sets it at every start, off in
        /// 'Zod exact'; headless default off (the counted checks and the parity report hold it off unless they set it themselves).</summary>
        public static bool CpuRepairDeviation = false;
        /// <summary>DV137: game seconds between two repair trips of one computer player; the first one from this time of the match on. x 1.3 on Easy (K434).</summary>
        public const double CpuRepairEverySec = 300.0;
        /// <summary>DV137: the share of its hit points a vehicle must have lost to be sent.</summary>
        public const double CpuRepairMinLost = 0.10;
        /// <summary>DV137: a trip still going after this many game seconds ends: the hold is lifted (a vehicle stuck on its way is the planner's again).</summary>
        public const double CpuRepairMaxTripSec = 420.0;
        /// <summary>DV137: a far station gives a trip longer: this many game seconds + CpuRepairTripDriveFactor x the straight drive there and back at the
        /// vehicle's own speed (a missile launcher crossing the Car Park needs about 9 minutes), when that is more than CpuRepairMaxTripSec.</summary>
        public const double CpuRepairTripSlackSec = 60.0;
        public const double CpuRepairTripDriveFactor = 2.0;
        /// <summary>DV137: a shuttle that stayed on the ground this long after its repair did not fly back (it already stood there).</summary>
        public const double CpuRepairShuttleStaySec = 5.0;
        /// <summary>DV137: a vehicle hit by an enemy less than this many game seconds ago is in a fight and is not sent (nor one firing at a target now).</summary>
        public const double CpuRepairCalmSec = 4.0;

        /// <summary>Checks and reports only (never the game): the switch off until the result is disposed, then as it was. A Play session
        /// leaves the switch on in the editor; the parity report, the Deviations block and the computer's own checks take this hold.</summary>
        public static IDisposable CpuRepairOff() => new CpuRepairOffHold();
        sealed class CpuRepairOffHold : IDisposable
        {
            readonly bool was = CpuRepairDeviation;
            public CpuRepairOffHold() { CpuRepairDeviation = false; }
            public void Dispose() { CpuRepairDeviation = was; }
        }
    }

    public partial class ZObject
    {
        /// <summary>DV137: this vehicle is on a repair trip of its computer player (written by Sim/ZBot.CpuRepair.cs only).</summary>
        internal bool cpuRepairTrip;
        /// <summary>DV137: on a repair trip of its computer player: the computer's order rounds and its idle rules leave it alone. Always
        /// false with the switch off and after the game is over.</summary>
        public bool CpuRepairHeld => cpuRepairTrip && ZWorld.CpuRepairDeviation && World != null && !World.GameOver;
    }

    public sealed partial class ZBot
    {
        /// <summary>DV137: how a repair trip ended. Back = it reached the pad and its trip is over (back where it was, or standing after a
        /// fight of its own on the way back); NoRepair = its repair waypoint ended before the pad (the station lost or gone, no way in) and
        /// the rest of the trip is over.</summary>
        public enum CpuRepairEnd { None = 0, Back = 1, NoRepair = 2, Died = 3, Lost = 4, TimedOut = 5, HandedOver = 6 }

        /// <summary>DV137: one repair trip (checks and the Play watch).</summary>
        public struct CpuRepairTrip
        {
            public int unitId, stationId, rank, hpAtSend, maxHp, hpBest, hpEnd, candidates, fromX, fromY, endX, endY;
            public string type;
            public double sentAt, padAt, leftAt, endedAt;
            /// <summary>game seconds after the send at which the trip ends at the latest (TimedOut).</summary>
            public double maxSec;
            public bool shuttle;
            public CpuRepairEnd end;
            /// <summary>px from where it was when it was sent to where the trip ended (-1 = not known: destroyed / lost).</summary>
            public int BackPx => end == CpuRepairEnd.Died || end == CpuRepairEnd.Lost ? -1 : (int)Math.Round(Math.Sqrt((double)(endX - fromX) * (endX - fromX) + (double)(endY - fromY) * (endY - fromY)));
        }

        /// <summary>DV137 statistics: trips sent, orders the command layer refused (a ship whose repair order the shuttle rule refused: that rule off, no pilot, no deck point), and every finished trip.</summary>
        public int CpuRepairTrips { get; private set; }
        public int CpuRepairRefused { get; private set; }
        public readonly List<CpuRepairTrip> CpuRepairLog = new List<CpuRepairTrip>();
        /// <summary>DV137: the vehicle on a repair trip now (-1 = none).</summary>
        public int CpuRepairUnitId => crActive ? crTrip.unitId : -1;
        /// <summary>DV137: the trip under way (valid while <see cref="CpuRepairUnitId"/> >= 0).</summary>
        public CpuRepairTrip CpuRepairNow => crTrip;
        /// <summary>DV137: the game time from which the next trip may be sent (-1 = before the first look).</summary>
        public double CpuRepairNextAt => crNextAt;

        double crNextLook, crNextAt = -1, crShuttleNoUntil = -1, crBackFrom = -1, crNoBayUntil = -1;
        int crNoBayId = -1;
        bool crActive, crAir;
        CpuRepairTrip crTrip;

        /// <summary>DV137 rank of a vehicle kind: heavy tank 0, missile launcher 1, shuttle 2, medium tank 3; -1 = not one of them.</summary>
        public static int CpuRepairRank(ZObject o) => o == null || o.Kind != ZKind.Vehicle ? -1 : o.IsShuttle ? 2 : o.Type == "heavy" ? 0 : o.Type == "missile_launcher" ? 1 : o.Type == "medium" || o.Type == ZMTank.Type ? 3 : -1;   // DV145: the medium tank ranks as the mech

        /// <summary>ZBot.Tick, every server tick right after the strike groups (Normal and Easy alike): follows the trip under way every
        /// tick, looks for a new one once a second. With the switch off it returns at once and touches nothing.</summary>
        void CpuRepairTick()
        {
            if (!ZWorld.CpuRepairDeviation) return;
            if (crActive) CrFollow();
            if (W.Time + TIME_EPS < crNextLook) return;
            crNextLook = W.Time + AI_INTERVAL;
            if (Ignored) { if (crActive) CrEnd(W.Get(crTrip.unitId), CpuRepairEnd.HandedOver); return; }
            if (crActive || W.GameOver || !ZWorld.RepairInPlaceDeviation) return;
            double every = ZWorld.CpuRepairEverySec * EasySlowScale;
            if (crNextAt < 0) crNextAt = every;
            if (W.Time + TIME_EPS < crNextAt) return;
            List<ZObject> stations = null;
            foreach (var b in W.Objects) if (b.Kind == ZKind.Building && b.CanRepairUnit(Team)) (stations ??= new List<ZObject>()).Add(b);
            if (stations == null) return;                                                       // no station of ours: no trip, the clock waits
            var cands = new List<ZObject>();
            foreach (var o in W.Objects) if (CrCandidate(o)) cands.Add(o);
            if (cands.Count == 0) return;
            cands.Sort((a, b) =>
            {
                int c = CpuRepairRank(a).CompareTo(CpuRepairRank(b));
                if (c != 0) return c;
                c = (b.MaxHealth - b.Health).CompareTo(a.MaxHealth - a.Health);                   // the most hit points lost first
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });
            foreach (var u in cands)
            {
                var st = CrStation(u, stations);
                if (st != null && CrSend(u, st, cands.Count)) { crNextAt = W.Time + every; return; }
            }
        }

        bool CrCandidate(ZObject o)
        {
            if (o.Kind != ZKind.Vehicle || o.Owner != Team || Gone(o) || o.Destroyed || o.IsMinion || o.IsFortDefender) return false;
            if (CpuRepairRank(o) < 0 || !o.CanBeRepaired()) return false;
            if (o.MaxHealth - o.Health < o.MaxHealth * ZWorld.CpuRepairMinLost) return false;
            if (o.StrikeHeld) return false;                                                     // DV107: it goes with its strike group
            if (o.CpuDefendHeld || o.CpuShuttleHeld) return false;                              // DV136: a guard holds its frontier flag (its own rule releases it after its 2 minutes); DV138: a ship on its run, a squad of the shuttle plan
            if (W.Time + TIME_EPS < o.LastEnemyHit + ZWorld.CpuRepairCalmSec) return false;    // hit in the last 4 s: a vehicle under fire is not pulled out of the fight
            if (o.AttackObject != null && !o.AttackObject.Destroyed && o.AttackObject.KillAt < 0) return false;   // firing at a target now: the same
            if (o.Waypoints.Count > 0 && o.Waypoints[0].mode == WpMode.UnitRepair) return false;  // already on a repair trip
            if (o.IsShuttle) return o.ShuttleRepairTarget < 0 && W.Time + TIME_EPS >= crShuttleNoUntil;
            return o.CanMove && o.CanOverwriteWP();
        }

        /// <summary>The nearest own station the vehicle reaches and whose waiting spot it can stand on (squared centre distance, then the lower
        /// id); a shuttle: any with a deck point. The station whose bay the last trip could not get into only when there is no other.</summary>
        ZObject CrStation(ZObject u, List<ZObject> stations)
        {
            int skip = W.Time + TIME_EPS < crNoBayUntil ? crNoBayId : -1;
            return CrStation(u, stations, skip) ?? (skip >= 0 ? CrStation(u, stations, -1) : null);
        }

        ZObject CrStation(ZObject u, List<ZObject> stations, int skip)
        {
            ZObject best = null; double bd = 0;
            foreach (var st in stations)
            {
                if (st.Id == skip) continue;
                if (u.IsShuttle) { if (!u.ShuttleDeckPoint(st, out _, out _)) continue; }
                else if (!st.GetRepairEntrance(out int ex, out int ey) || !InSameRegion(u.LocX + 8, u.LocY + 8, ex, ey, false) || CrSpotBlocked(u, ex, ey)) continue;
                double dx = st.CenterX - u.CenterX, dy = st.CenterY - u.CenterY, d = dx * dx + dy * dy;
                if (best == null || d < bd || (d == bd && st.Id < best.Id)) { best = st; bd = d; }
            }
            return best;
        }

        /// <summary>The waiting spot DV62 gives u at this entrance (ZObject.RepairQueueSpot: 40 px out when its tiles are free, else the
        /// entrance itself) has a cannon nearer to it than the movement layer lets u stand (ZPathing.MinSpacing): the trip could never stand
        /// there, its approach would give up and the vehicle drive back unrepaired. A cannon carried off or destroyed frees the spot again.</summary>
        bool CrSpotBlocked(ZObject u, int ex, int ey)
        {
            int qy = ey + 40;
            if (W.Path.WithinImpassable(ex - (u.WidthPix >> 1) + 1, qy - (u.HeightPix >> 1) + 1, u.WidthPix - 2, u.HeightPix - 2, out _, out _, false)) qy = ey;
            foreach (var c in W.Objects)
            {
                if (c.Kind != ZKind.Cannon || c.Destroyed || c.KillAt >= 0) continue;
                long dx = c.CenterX - ex, dy = c.CenterY - qy, m = ZPathing.MinSpacing(u, c);
                if (dx * dx + dy * dy < m * m) return true;
            }
            return false;
        }

        /// <summary>The R key's order (View/PlayerInput.cs SendToRepair) through the command layer, the way back in the same order (a ship:
        /// the repair alone, it flies back by itself); true = the trip is under way.</summary>
        bool CrSend(ZObject u, ZObject st, int candidates)
        {
            int hp = u.Health, fx = u.CenterX, fy = u.CenterY;
            var wp = new ZWaypoint { mode = WpMode.UnitRepair, refId = st.Id, x = st.CenterX - 8, y = st.CenterY, attackTo = false, playerGiven = true };
            var order = u.IsShuttle ? new[] { wp } : new[] { wp, new ZWaypoint { mode = WpMode.Move, refId = -1, x = fx, y = fy, attackTo = false, playerGiven = true } };
            u.cpuRepairTrip = true;
            bool sent = u.IsShuttle ? W.CpuShuttleRepairSend(Team, u, st.Id) : W.SendWaypoints(Team, u, order);   // DV138: the computer's one way to send its own ship (G1 stays closed)
            bool ok = sent && (u.IsShuttle ? u.ShuttleRepairTarget == st.Id : u.Waypoints.Count > 0 && u.Waypoints[0].mode == WpMode.UnitRepair && u.Waypoints[0].refId == st.Id);
            if (!ok)
            {
                u.cpuRepairTrip = false; CpuRepairRefused++;
                if (u.IsShuttle) crShuttleNoUntil = W.Time + ZWorld.CpuRepairEverySec * EasySlowScale;   // DV138: the ship's repair order refused (the shuttle rule off, no pilot, no deck point): no new try for a period
                return false;
            }
            if (ClientView) Relay(u);
            double gx = st.CenterX - fx, gy = st.CenterY - fy, drive = u.MoveSpeed > 0 ? 2.0 * Math.Sqrt(gx * gx + gy * gy) / u.MoveSpeed : 0;   // the straight drive there and back, game seconds
            crTrip = new CpuRepairTrip
            {
                unitId = u.Id, stationId = st.Id, rank = CpuRepairRank(u), type = u.Type, shuttle = u.IsShuttle, hpAtSend = hp, maxHp = u.MaxHealth, hpBest = hp,
                candidates = candidates, fromX = fx, fromY = fy, sentAt = W.Time, padAt = -1, leftAt = -1, endedAt = -1,
                maxSec = Math.Max(ZWorld.CpuRepairMaxTripSec, ZWorld.CpuRepairTripSlackSec + ZWorld.CpuRepairTripDriveFactor * drive),
            };
            crActive = true; crBackFrom = -1; crAir = false;
            CpuRepairTrips++;
            Log.Add($"{W.Time:F0}s {Team} DV137 repair trip: {u.Type}#{u.Id} {hp}/{u.MaxHealth} -> repair#{st.Id}");
            return true;
        }

        /// <summary>Every tick while a trip is under way: its end (back, not repaired, destroyed, lost, too long).</summary>
        void CrFollow()
        {
            var u = W.Get(crTrip.unitId);
            if (u == null || u.Destroyed || u.KillAt >= 0) { CrEnd(u, CpuRepairEnd.Died); return; }
            if (u.Owner != Team) { CrEnd(u, CpuRepairEnd.Lost); return; }
            if (u.Health > crTrip.hpBest) crTrip.hpBest = u.Health;
            if (W.Time + TIME_EPS >= crTrip.sentAt + crTrip.maxSec) { CrEnd(u, CpuRepairEnd.TimedOut); return; }
            if (u.IsShuttle)
            {
                if (u.ShuttleRepairTarget == crTrip.stationId)
                {
                    if (u.ShuttleOnRepairPad && crTrip.padAt < 0) crTrip.padAt = W.Time;
                    return;
                }
                if (crBackFrom < 0) { crBackFrom = W.Time; crTrip.leftAt = W.Time; }
                if (u.ShuttleAirborne && u.ShuttleState != ZShuttleState.Hover) { crAir = true; return; }   // flying back
                if (!crAir && W.Time + TIME_EPS < crBackFrom + ZWorld.CpuRepairShuttleStaySec) return;     // the take-off waits for its ramp and room
                CrEnd(u, crTrip.padAt >= 0 ? CpuRepairEnd.Back : CpuRepairEnd.NoRepair);
                return;
            }
            int sid = crTrip.stationId; bool toStation = false;
            foreach (var p in u.Waypoints) if (p.mode == WpMode.UnitRepair && p.refId == sid) { toStation = true; break; }
            if (toStation)                                                                      // on its way there, in the bay, on the pad (a step aside of the movement layer may stand in front)
            {
                if (crTrip.padAt < 0 && u.OnRepairPad(sid)) crTrip.padAt = W.Time;
                return;
            }
            if (crBackFrom < 0) { crBackFrom = W.Time; crTrip.leftAt = W.Time; }                 // the repair waypoint is over: the drive back
            if (u.Waypoints.Count > 0 || u.IsMoving) return;
            if (crTrip.padAt < 0)
            {   // its approach gave up before the bay (the waiting spot blocked) while the station still stands: the next trip prefers another one
                var st = W.Get(crTrip.stationId);
                if (st != null && st.CanRepairUnit(Team)) { crNoBayId = st.Id; crNoBayUntil = W.Time + ZWorld.CpuRepairEverySec * EasySlowScale; }
            }
            CrEnd(u, crTrip.padAt >= 0 ? CpuRepairEnd.Back : CpuRepairEnd.NoRepair);
        }

        void CrEnd(ZObject u, CpuRepairEnd how)
        {
            if (u != null) { u.cpuRepairTrip = false; crTrip.hpEnd = u.Health; crTrip.endX = u.CenterX; crTrip.endY = u.CenterY; }
            crTrip.end = how; crTrip.endedAt = W.Time;
            CpuRepairLog.Add(crTrip);
            crActive = false;
            Log.Add($"{W.Time:F0}s {Team} DV137 repair trip over: {crTrip.type}#{crTrip.unitId} {how}, hit points {crTrip.hpEnd}/{crTrip.maxHp}");
        }
    }
}
