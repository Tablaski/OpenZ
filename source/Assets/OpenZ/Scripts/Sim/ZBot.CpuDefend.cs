using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ deviation DV136 "the computer defends its frontier" (version 1.2, items 14 and 14a). The engine's computer only ever attacks:
    /// every order of its rounds (zbot.cpp:127-414) sends a unit at a foreign flag, an enemy unit or the enemy fort. With DV136, from
    /// minute 8 of the game, one in four of those orders becomes a DEFEND order: the unit goes to a frontier flag of its own, takes a spot
    /// of its own near it on the side that faces the enemy, holds there for two minutes, firing at what comes into its range, and is then
    /// released with an order of the computer's own kind. Before minute 8 every order is the engine's, as before. Everything is behind ONE
    /// switch, <see cref="ZWorld.CpuDefendDeviation"/> (View/ZGame.cs sets it at every start, off in 'Zod exact'; the static default is off,
    /// and the counted checks, the parity report, its Deviations block and the computer's own checks hold it off unless they set it
    /// themselves). With the switch off not one line below changes a state: the computer is tick for tick today's.
    /// <para>THE RULE (every number is a constant of ZWorld below).</para>
    /// <list type="bullet">
    /// <item>WHICH ORDERS. Every order the computer's own order path gives (ZBot.Command, not a finisher: its rounds, DV56's idle groups, the
    /// Electrobot's escort, the order a released guard gets) that would attack a unit, enter an enemy fort or take a foreign flag is
    /// counted, from <see cref="ZWorld.CpuDefendStartSec"/> (8:00) on the game clock. The fort finish (DV60) and a strike group's releases
    /// (DV107) are finishers: never counted, never turned. Grenade crates, empty vehicles and guns, repairs and crane jobs: never counted.</item>
    /// <item>ONE IN FOUR. The counted orders are taken in blocks of <see cref="ZWorld.CpuDefendEvery"/> (4); in each block one order is the
    /// defend one, its place in the block drawn from a hash of the game tick of the block's first order, the team and the block's number
    /// (the game's own tick, never a random stream of the game: Zod's draws stay untouched, every peer and a replay draw the same). When
    /// the drawn order cannot become a defend order (the unit cannot stand guard: an unarmed unit, the Electrobot, a shuttle, a strike
    /// group member, a unit released from guard duty less than <see cref="ZWorld.CpuDefendRestSec"/> ago; or there is no frontier flag
    /// with a free spot it reaches; or it is the order a guard gets as it is released), it stays the engine's order and the defend turn
    /// passes to the next counted order that can take it (never more than one is owed).</item>
    /// <item>THE FRONTIER. A flag of the computer's whose territory (its zone rectangle) touches, edge to edge, a territory another team
    /// holds (a flag's or a fort's: neutral ground does not count). Its enemy direction: from the flag toward the nearest point of each
    /// shared edge, the unit directions added (borders on opposite sides that cancel out: the nearest one). The flag a guard goes to:
    /// the nearest such flag it reaches, each guard already there counting <see cref="ZWorld.CpuDefendCrowdPx"/> px more, at most
    /// <see cref="ZWorld.CpuDefendPerFlag"/> guards a flag.</item>
    /// <item>THE SPOT (<see cref="ZWorld.CpuDefendSpots"/>): a footprint for the unit's kind (a robot's tile, a vehicle's 2 x 2) from
    /// <see cref="ZWorld.CpuDefendNearPx"/> to <see cref="ZWorld.CpuDefendFarPx"/> px from the flag, on the side that faces the enemy (within
    /// about 75 degrees of the enemy direction), in a territory of the computer's own, on dry passable ground, in no building, exit lane or
    /// keep-clear area, never on or beside any flag (<see cref="ZWorld.CpuDefendNearPx"/> px round it, and its 48 px box), on nothing that
    /// stands there for good (rocks, huts, crates, guns, empty vehicles).
    /// Spots near the best distance (<see cref="ZWorld.CpuDefendBestPx"/>) and straight toward the enemy go first; a spot beside cover (a
    /// tile that blocks direct fire: a rock, a hut, a wall, a cliff) with a clear line to the border, and one in the flag's own territory,
    /// are preferred; a small hash of the unit's id and the spot spreads the guards naturally. A spot is free when it is at least
    /// <see cref="ZWorld.CpuDefendPitchPx"/> px from every other guard's spot of this computer and nobody stands on it, and the unit's own
    /// path graph reaches it (the exact reachability of the pathing layer, ZReach).</item>
    /// <item>THE WALK. Plain move points through the command layer (ZWorld.SendWaypoints, like every order of the computer), not the
    /// attack-move of its other orders: a guard goes to its flag and does not chase off after what it meets on the way (it still fires
    /// on the move where its kind does, as any unit on a move order).</item>
    /// <item>FACING THE ENEMY. The order is two move points: <see cref="ZWorld.CpuDefendBackPx"/> px (a vehicle
    /// <see cref="ZWorld.CpuDefendBackVehiclePx"/>) behind the spot (toward the flag), then the spot, so the last stretch of the walk points at
    /// the enemy and the unit stands facing it (the view turns a body to where it last moved; turrets track their targets). Where the
    /// stretch from the point behind is not straight open ground or somebody stands on it, the spot alone. A guard that still did not come onto its place walking
    /// toward the enemy (its last 8 px of movement more than 60 degrees off the enemy direction) turns once the only way a body turns, by
    /// walking: the same stretch straight back and forward again, or with no room behind it a 24 px step forward.</item>
    /// <item>THE HOLD. Standing within <see cref="ZWorld.CpuDefendSettlePx"/> px of its spot it holds position at once (ZWorld.SendHold, the
    /// game's HOLD POSITION: it fires at enemies in its range, no chase, no flag or vehicle taken by itself, no help call). Stopped short,
    /// it is sent on to its spot <see cref="ZWorld.CpuDefendResends"/> times at most without coming nearer, then once to the free spot it
    /// reaches nearest where it stands; then it holds where it stands when a guard may hold there (ZBot.DefendStandOk: near the flag, on
    /// its enemy side, in its own territory, clear of every flag and of the other guards' spots), else it is released. A guard knocked more
    /// than <see cref="ZWorld.CpuDefendDriftPx"/> px off its spot while it holds (a step aside from a shell) walks back,
    /// <see cref="ZWorld.CpuDefendReturns"/> times at most. While it walks to its spot and holds, the computer's order rounds and the OpenZ
    /// rules that order idle units about leave it alone (<see cref="ZObject.CpuDefendHeld"/>, asked where a strike group member's hold is
    /// asked).</item>
    /// <item>THE RELEASE: <see cref="ZWorld.CpuDefendHoldSec"/> (2 minutes) after it first stood on its spot; at once when its flag is lost;
    /// at once, without an order of this rule, when another rule of the computer gives it an order of its own (a repair trip, a ride);
    /// when it has not reached its spot <see cref="ZWorld.CpuDefendWalkSec"/> s after the order (it holds where it is when a guard may hold
    /// there, else it is released). Released, it gets an order of the computer's own kind at once (the engine's matching for that unit, its
    /// nearest target; the Easy limit on attackers is kept); with nothing to go for it stands idle as today; it is not sent to guard again
    /// for <see cref="ZWorld.CpuDefendRestSec"/> s. A squad whose leader falls stays on guard under its next robot. Switched off, or the
    /// computer stopped: every guard is released at once, without an order.</item>
    /// </list>
    /// Normal and Easy alike (the owner's 8 and 2 minutes are game time; Easy's slower clock does not stretch them). Deterministic: game
    /// time and tick, the object and zone lists' order, whole-number positions; no random draw, no frame time, no view state. The team is
    /// the computer's own (no colour is named): every other non-neutral team is the enemy.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV136: the one switch. View/ZGame.cs sets it at every start (off in 'Zod exact'); off by default (headless).</summary>
        public static bool CpuDefendDeviation = false;

        /// <summary>No defend order before this game time.</summary>
        public const double CpuDefendStartSec = 480.0;
        /// <summary>One in this many counted orders becomes a defend order.</summary>
        public const int CpuDefendEvery = 4;
        /// <summary>A guard holds this long after it first stood on its spot.</summary>
        public const double CpuDefendHoldSec = 120.0;
        /// <summary>A guard that has not reached its spot this long after the order holds where it is (near the flag) or is released.</summary>
        public const double CpuDefendWalkSec = 90.0;
        /// <summary>At most this many guards at one flag; each guard already there makes a flag this many px farther for the choice.</summary>
        public const int CpuDefendPerFlag = 6, CpuDefendCrowdPx = 320;
        /// <summary>The spots: this far from the flag (px; 16 px = one tile = 2 m), the best distance, the least distance between two guards' spots.</summary>
        public const int CpuDefendNearPx = 56, CpuDefendFarPx = 136, CpuDefendBestPx = 88, CpuDefendPitchPx = 48;
        /// <summary>The side that faces the enemy: the cosine of the angle between (spot - flag) and the enemy direction is at least this (about 75 degrees).</summary>
        public const double CpuDefendArcCos = 0.25;
        /// <summary>Standing this close to its spot it holds; the point behind the spot for the last stretch of the walk (a robot; a vehicle twice as far,
        /// so its hull has turned onto the last stretch before the view's short-move rule takes over); resends without coming nearer.</summary>
        public const int CpuDefendSettlePx = 20, CpuDefendBackPx = 32, CpuDefendBackVehiclePx = 64, CpuDefendResends = 2;
        /// <summary>A holding guard knocked this far off its spot walks back (at most CpuDefendReturns times).</summary>
        public const int CpuDefendDriftPx = 40, CpuDefendReturns = 2;
        /// <summary>A unit released from guard duty goes back to the normal orders: it is not sent to guard again for this long.</summary>
        public const double CpuDefendRestSec = 120.0;

        /// <summary>Checks and reports only (never the game): the switch off until the result is disposed, then as it was. A Play session
        /// leaves the switch on in the editor; the parity report, the Deviations block and the computer's own checks take this hold.</summary>
        public static IDisposable CpuDefendOff() => new CpuDefendOffHold();
        sealed class CpuDefendOffHold : IDisposable
        {
            readonly bool was = CpuDefendDeviation;
            public CpuDefendOffHold() { CpuDefendDeviation = false; }
            public void Dispose() { CpuDefendDeviation = was; }
        }

        /// <summary>A frontier flag: the flag, its zone's index, the enemy direction (a unit vector), the nearest point of its borders with the other side.</summary>
        public struct CpuDefendFront { public ZObject flag; public int zone; public double dx, dy; public int cx, cy; }

        List<int>[] cpuDefendBorders;

        /// <summary>Two zone rectangles touch edge to edge (one's right / bottom edge is the other's left / top edge, with a positive overlap
        /// along that edge). Zones never change: the neighbours are built once, on first use.</summary>
        public static bool CpuDefendZonesBorder(ZZone a, ZZone b)
        {
            bool vx = a.X + a.W == b.X || b.X + b.W == a.X, hy = a.Y + a.H == b.Y || b.Y + b.H == a.Y;
            int oy = Math.Min(a.Y + a.H, b.Y + b.H) - Math.Max(a.Y, b.Y), ox = Math.Min(a.X + a.W, b.X + b.W) - Math.Max(a.X, b.X);
            return (vx && oy > 0) || (hy && ox > 0);
        }

        /// <summary>The zones that border zone i (zone list order).</summary>
        public IReadOnlyList<int> CpuDefendNeighbours(int i)
        {
            if (cpuDefendBorders == null || cpuDefendBorders.Length != Zones.Count)
            {
                cpuDefendBorders = new List<int>[Zones.Count];
                for (int a = 0; a < Zones.Count; a++)
                {
                    cpuDefendBorders[a] = new List<int>();
                    for (int b = 0; b < Zones.Count; b++) if (a != b && CpuDefendZonesBorder(Zones[a], Zones[b])) cpuDefendBorders[a].Add(b);
                }
            }
            return i >= 0 && i < cpuDefendBorders.Length ? cpuDefendBorders[i] : Array.Empty<int>();
        }

        /// <summary>The frontier flags of a team now, in zone list order (see the class notes of ZBot.CpuDefend.cs). Read only.</summary>
        public void CpuDefendFrontier(Team team, List<CpuDefendFront> res)
        {
            res.Clear();
            if (team == Team.Neutral) return;
            for (int i = 0; i < Zones.Count; i++)
            {
                var z = Zones[i]; var f = z.Flag;
                if (f == null || z.Owner != team || f.Owner != team || f.Destroyed) continue;
                double sx = 0, sy = 0; int n = 0, cx = 0, cy = 0; long best = long.MaxValue;
                foreach (int j in CpuDefendNeighbours(i))
                {
                    var o = Zones[j];
                    if (o.Owner == team || o.Owner == Team.Neutral) continue;
                    int x0 = Math.Max(z.X, o.X), x1 = Math.Min(z.X + z.W, o.X + o.W), y0 = Math.Max(z.Y, o.Y), y1 = Math.Min(z.Y + z.H, o.Y + o.H);
                    int px = Math.Max(x0, Math.Min(x1, f.CenterX)), py = Math.Max(y0, Math.Min(y1, f.CenterY));   // the shared edge's point nearest the flag
                    long ddx = px - f.CenterX, ddy = py - f.CenterY, d2 = ddx * ddx + ddy * ddy;
                    if (d2 < 1) continue;
                    double l = Math.Sqrt(d2);
                    sx += ddx / l; sy += ddy / l; n++;
                    if (d2 < best) { best = d2; cx = px; cy = py; }
                }
                if (n == 0) continue;
                double sl = Math.Sqrt(sx * sx + sy * sy);
                if (sl < 0.2)
                {   // borders on opposite sides cancel out: the nearest one
                    double l = Math.Sqrt((double)best);
                    sx = (cx - f.CenterX) / l; sy = (cy - f.CenterY) / l;
                }
                else { sx /= sl; sy /= sl; }
                res.Add(new CpuDefendFront { flag = f, zone = i, dx = sx, dy = sy, cx = cx, cy = cy });
            }
        }

        /// <summary>
        /// The guard spots at a frontier flag for a robot (a tile's centre) or a vehicle (the middle of a 2 x 2 footprint), best first (score,
        /// then y, then x). See the class notes of ZBot.CpuDefend.cs for every condition. Who stands where now and who reaches a spot is
        /// decided by the computer (ZBot.DefendSend). Read only; deterministic.
        /// </summary>
        public void CpuDefendSpots(CpuDefendFront f, Team team, bool robot, List<(int x, int y, int score)> res)
        {
            res.Clear();
            var flag = f.flag;
            if (flag == null || Pathing == null) return;
            int fx = flag.CenterX, fy = flag.CenterY, r = CpuDefendFarPx / 16 + 2, ftx = fx >> 4, fty = fy >> 4;
            long near2 = (long)CpuDefendNearPx * CpuDefendNearPx, far2 = (long)CpuDefendFarPx * CpuDefendFarPx;
            int body = robot ? 10 : 20;
            for (int ty = fty - r; ty <= fty + r; ty++)
                for (int tx = ftx - r; tx <= ftx + r; tx++)
                {
                    if (tx < 1 || ty < 1 || tx + 2 >= Map.width || ty + 2 >= Map.height) continue;
                    var (x, y) = ZReach.FootCentre(tx, ty, robot);
                    long ddx = x - fx, ddy = y - fy, d2 = ddx * ddx + ddy * ddy;
                    if (d2 < near2 || d2 > far2) continue;
                    double d = Math.Sqrt(d2), cos = (ddx * f.dx + ddy * f.dy) / d;
                    if (cos < CpuDefendArcCos) continue;
                    if (!Path.StepOk(tx, ty, tx, ty, robot, false)) continue;
                    bool ok = true;
                    int span = robot ? 0 : 1;
                    for (int j = ty; j <= ty + span && ok; j++)
                        for (int i = tx; i <= tx + span; i++)
                        {
                            var c = Map.ClassAt(i, j);
                            if (c == ZClass.Water || c == ZClass.DeepWater || c == ZClass.Impassable) { ok = false; break; }   // dry ground only (no guard waits in a swamp)
                        }
                    if (!ok) continue;
                    int zi = GetZoneIndex(x, y);
                    if (zi < 0 || Zones[zi].Owner != team) continue;                                  // on its own side of the border
                    foreach (var g in Flags)
                    {   // never on a flag (a foreign one would be taken), never right beside one
                        long gx = g.CenterX - x, gy = g.CenterY - y;
                        if ((Math.Abs(gx) < 48 && Math.Abs(gy) < 48) || gx * gx + gy * gy < (long)CpuDefendNearPx * CpuDefendNearPx) { ok = false; break; }
                    }
                    if (!ok) continue;
                    if (InExitLane(x, y, body) || InBuilding(x, y, body) || Pathing.StrikeKeepClear(x, y)) continue;
                    int half = robot ? 12 : 22;
                    foreach (var o in Objects)
                    {   // nothing that stands there for good: rocks, huts, crates, wrecks, props, guns, empty vehicles
                        if (o.Destroyed || o.KillAt >= 0 || o.Kind == ZKind.Building || o.Type == "flag") continue;
                        if (!(o.Kind == ZKind.MapItem || o.Kind == ZKind.Cannon || (o.Kind == ZKind.Vehicle && o.Owner == Team.Neutral))) continue;
                        if (o.LocX < x + half && x - half < o.LocX + o.WidthPix && o.LocY < y + half && y - half < o.LocY + o.HeightPix) { ok = false; break; }
                    }
                    if (!ok) continue;
                    // cover: a tile that blocks direct fire right beside the footprint; a clear line to the border's nearest point
                    bool cover = false;
                    for (int j = ty - 1; j <= ty + span + 1 && !cover; j++)
                        for (int i = tx - 1; i <= tx + span + 1; i++)
                        {
                            if (i >= tx && i <= tx + span && j >= ty && j <= ty + span) continue;
                            if (Sight.BlockedTile(i, j)) { cover = true; break; }
                        }
                    bool sees = Sight.Clear(x, y, 0, f.cx, f.cy, 0);
                    int score = (int)Math.Abs(d - CpuDefendBestPx) + (int)((1.0 - cos) * 96) + (zi == f.zone ? 0 : 24) - (sees ? 16 : 0) - (cover && sees ? 32 : 0);
                    res.Add((x, y, score));
                }
            res.Sort((a, b) => a.score != b.score ? a.score.CompareTo(b.score) : a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
        }

        static uint CpuDefendHash(uint a, uint b, uint c)
        {
            uint h = unchecked(a * 2654435761u ^ b * 40503u ^ c * 374761393u ^ 0x9E3779B9u);
            h ^= h >> 15; h = unchecked(h * 2246822519u); h ^= h >> 13; h = unchecked(h * 3266489917u); h ^= h >> 16;
            return h;
        }
        /// <summary>DV136's draws (the defend order's place in a block of four, a spot's small spread): a hash of game values only.</summary>
        internal static uint CpuDefendMix(long a, int b, int c) => CpuDefendHash(unchecked((uint)a ^ (uint)(a >> 32)), unchecked((uint)b), unchecked((uint)c));
    }

    public partial class ZObject
    {
        /// <summary>DV136: this squad leader or vehicle stands guard (on its way to its spot or holding it). Written by the computer's defend code only.</summary>
        internal bool cpuDefendGuard;
        /// <summary>DV136: a guard of the computer's frontier: the computer's order rounds and the rules for idle units leave it alone (asked
        /// where <see cref="StrikeHeld"/> is asked). Always false with the switch off and after the game is over.</summary>
        public bool CpuDefendHeld => cpuDefendGuard && ZWorld.CpuDefendDeviation && World != null && !World.GameOver;
    }

    public sealed partial class ZBot
    {
        public enum DefendEventKind
        {
            Ordered,    // unitId, flagId, x / y = its spot, n = the guards at that flag with it, note = the order it replaced
            Holds,      // unitId, x / y = where it stands, n = px from its spot
            Resent,     // unitId, x / y = its spot, n = px short of it (-1 = another spot near it): stopped short of its spot, sent on
            Faces,      // unitId, x / y = where it will stand, n = px of the step back (-24 = a step forward): it did not come there walking toward the enemy
            Returns,    // unitId: knocked off its spot while holding, walks back
            Released,   // unitId, n = whole seconds it held, note = why
            Lost,       // unitId: destroyed, or no longer ours
            Successor,  // unitId = the squad's next leader, n = the fallen one: the squad stays on guard
            NoPlace,    // unitId: its order was the defend one, but no frontier flag has a free spot it reaches (the turn passes on)
            OrderAfter, // unitId, n = the target: the order a released guard got
        }
        public struct DefendEvent { public double time; public DefendEventKind kind; public int unitId, flagId, x, y, n; public string note; }

        /// <summary>One guard as it is now (checks, proofs, a view): the unit, its flag, its spot, the enemy direction, when it was sent, when it first stood (-1: not yet), holding now.</summary>
        public struct DefendGuardView { public int unitId, flagId, spotX, spotY; public double dirX, dirY, ordered, arrived; public bool holding; }

        /// <summary>DV136: what happened, in order (checks, proofs). At most 4000 entries.</summary>
        public readonly List<DefendEvent> DefendEvents = new List<DefendEvent>();
        /// <summary>DV136 statistics: orders counted for the one-in-four rule (from 8:00), defend orders given, defend turns that found no spot,
        /// guards released, guards lost, the game time of the first defend order (-1: none yet).</summary>
        public int DefendCounted { get; private set; }
        public int DefendOrders { get; private set; }
        public int DefendNoPlace { get; private set; }
        public int DefendReleased { get; private set; }
        public int DefendLost { get; private set; }
        public double DefendFirstAt { get; private set; } = -1;
        /// <summary>Defend orders by the order they replaced (Attack, Move = a foreign flag, EnterFort).</summary>
        public readonly Dictionary<WpMode, int> DefendByMode = new Dictionary<WpMode, int>();
        /// <summary>Guards now (on their way or holding).</summary>
        public int DefendGuardCount => dfGuards.Count;
        public List<DefendGuardView> DefendGuards()
        {
            var l = new List<DefendGuardView>();
            foreach (var g in dfGuards) l.Add(new DefendGuardView { unitId = g.o.Id, flagId = g.flagId, spotX = g.sx, spotY = g.sy, dirX = g.dx, dirY = g.dy, ordered = g.ordered, arrived = g.arrived, holding = g.phase == DfPhase.Hold });
            return l;
        }

        enum DfPhase { Walk, Hold, Return }
        sealed class DfGuard
        {
            public ZObject o; public ZWorld.CpuDefendFront front; public int flagId, sx, sy, tries, returns, ax, ay, lx, ly;
            public double dx, dy, ordered, walkFrom, arrived = -1, bestOff = -1; public DfPhase phase; public bool fell, faced, repicked;
        }
        readonly List<DfGuard> dfGuards = new List<DfGuard>();
        readonly Dictionary<int, double> dfFreed = new Dictionary<int, double>();          // unit id -> the game time it was released (looked up only, never walked through)
        readonly List<ZWorld.CpuDefendFront> dfFront = new List<ZWorld.CpuDefendFront>();
        readonly List<(int x, int y, int score)> dfSpots = new List<(int x, int y, int score)>();
        double dfNextStep;
        int dfSlot;
        bool dfOwed, dfNoTurn;

        void DfLog(DefendEventKind kind, int unitId, int flagId = -1, int x = 0, int y = 0, int n = 0, string note = null)
        {
            if (DefendEvents.Count < 4000) DefendEvents.Add(new DefendEvent { time = W.Time, kind = kind, unitId = unitId, flagId = flagId, x = x, y = y, n = n, note = note });
            if (kind == DefendEventKind.Ordered || kind == DefendEventKind.Released || kind == DefendEventKind.Lost)
                Log.Add($"{W.Time:F0}s {Team} DV136 defend: {kind} #{unitId}{(flagId >= 0 ? " flag #" + flagId : "")}{(note != null ? " (" + note + ")" : "")}");
        }

        // ------------------------------------------------------------------ the one-in-four turn (ZBot.Command, after the Easy limit)
        /// <summary>ZBot.Command, for every order that is not a finisher, after the Easy limit on attackers: true = this order became a defend
        /// order (given here; Command gives nothing more). With the switch off, before 8:00 or for an order that is not counted: false at once.</summary>
        bool DefendTurn(ZObject u, ZObject t, WpMode mode)
        {
            if (!ZWorld.CpuDefendDeviation || W.GameOver || W.Time + TIME_EPS < ZWorld.CpuDefendStartSec) return false;
            if (mode != WpMode.Attack && mode != WpMode.EnterFort && !(mode == WpMode.Move && t.Kind == ZKind.MapItem && t.Type == "flag")) return false;
            int n = DefendCounted++;
            if (n % ZWorld.CpuDefendEvery == 0) dfSlot = (int)(ZWorld.CpuDefendMix(W.TickIndex, (int)Team, n / ZWorld.CpuDefendEvery) % (uint)ZWorld.CpuDefendEvery);
            if (n % ZWorld.CpuDefendEvery == dfSlot) dfOwed = true;                       // this block's defend turn (owed until an order can take it)
            if (!dfOwed || dfNoTurn || !DefendCanGuard(u)) return false;
            if (!DefendSend(u, mode)) { DefendNoPlace++; DfLog(DefendEventKind.NoPlace, u.Id); return false; }
            dfOwed = false;
            return true;
        }

        /// <summary>A squad leader or vehicle of ours that can stand guard: armed, can move, not a fort defender, not the Electrobot, not a
        /// shuttle or a crane, not in a strike group, not already on guard, not in the middle of an order that cannot be replaced, not just
        /// back from guard duty.</summary>
        bool DefendCanGuard(ZObject u)
        {
            if (u == null || u.Owner != Team || Gone(u) || u.Destroyed || !u.IsMobile || u.IsMinion || u.IsFortDefender || u.EvilGuarding) return false;   // DV165: EvilZod on guard has his own spot (Sim/ZEvilZod.Appear.cs)
            if (!u.CanMove || !u.CanAttack() || u.IsShuttle || u.StrikeHeld || u.cpuDefendGuard) return false;
            if (u.Kind == ZKind.Robot && u.Type == ZElectro.Type) return false;
            if (u.Kind == ZKind.Vehicle && u.Type == "crane") return false;
            if (u.Waypoints.Count > 0 && !u.CanOverwriteWP() && u.Waypoints[0].mode != WpMode.ForceMove) return false;   // in the middle of a repair, a crane job or a fort entry
            if (dfFreed.TryGetValue(u.Id, out double freed) && W.Time - freed < ZWorld.CpuDefendRestSec - TIME_EPS) return false;   // just back from guard duty: the normal orders first
            return true;
        }

        /// <summary>The defend order for u: the frontier flag (nearest it reaches, the crowd counted), the best free spot it reaches there, the two
        /// move points (behind the spot, the spot). False = none: no frontier flag, no free spot, or the command layer refused it.</summary>
        bool DefendSend(ZObject u, WpMode mode)
        {
            W.CpuDefendFrontier(Team, dfFront);
            if (dfFront.Count == 0) return false;
            bool robot = u.Kind == ZKind.Robot;
            var reach = W.Pathing?.Reach;
            int comp = reach != null ? reach.UnitComp(u, false) : -1;
            var order = new List<(double s, int i, int n)>();
            for (int i = 0; i < dfFront.Count; i++)
            {
                int n = 0;
                foreach (var g in dfGuards) if (g.flagId == dfFront[i].flag.Id) n++;
                if (n >= ZWorld.CpuDefendPerFlag) continue;
                double dx = dfFront[i].flag.CenterX - u.CenterX, dy = dfFront[i].flag.CenterY - u.CenterY;
                order.Add((Math.Sqrt(dx * dx + dy * dy) + n * ZWorld.CpuDefendCrowdPx, i, n));
            }
            order.Sort((a, b) => a.s != b.s ? a.s.CompareTo(b.s) : a.i.CompareTo(b.i));
            foreach (var (_, i, n) in order)
            {
                var f = dfFront[i];
                W.CpuDefendSpots(f, Team, robot, dfSpots);
                int best = -1, bs = int.MaxValue;
                for (int k = 0; k < dfSpots.Count; k++)
                {
                    var p = dfSpots[k];
                    int sc = p.score + (int)(ZWorld.CpuDefendMix(u.Id, p.x, p.y) % 24u);
                    if (sc >= bs || !DefendSpotFree(p.x, p.y, u)) continue;
                    if (comp >= 0 ? !reach.PointOk(p.x, p.y, robot, false, comp) : !InSameRegion(u.LocX + 8, u.LocY + 8, p.x, p.y, robot)) continue;
                    bs = sc; best = k;
                }
                if (best < 0) continue;
                int sx = dfSpots[best].x, sy = dfSpots[best].y;
                var g = new DfGuard { o = u, front = f, flagId = f.flag.Id, sx = sx, sy = sy, dx = f.dx, dy = f.dy, ordered = W.Time, walkFrom = W.Time, phase = DfPhase.Walk, ax = u.CenterX, ay = u.CenterY };
                if (!DefendRoute(g, sx, sy)) return false;                                    // refused or dropped by the command layer: the engine's order goes out instead
                u.cpuDefendGuard = true;
                dfGuards.Add(g);
                DefendOrders++;
                if (DefendFirstAt < 0) DefendFirstAt = W.Time;
                DefendByMode[mode] = DefendByMode.TryGetValue(mode, out int c) ? c + 1 : 1;
                LastRoundOrders.Add(new BotOrder { unitId = u.Id, targetId = -1, mode = WpMode.Move, time = W.Time });   // ordered in this round (the Electrobot's escort then gives it no second order; no target: nobody follows it)
                DfLog(DefendEventKind.Ordered, u.Id, f.flag.Id, sx, sy, n + 1, $"instead of {mode}; {n + 1} at flag zone {f.zone}");
                return true;
            }
            return false;
        }

        /// <summary>The walk to a spot: <see cref="DfBack"/> px behind it (toward the flag), then the spot, so the last stretch points at the
        /// enemy; the spot alone where the stretch from the point behind to the spot is not open ground (<see cref="DfStraight"/>) or somebody
        /// stands on it (<see cref="DfLineFree"/>). Plain move points (no attack-move: an attack-move's
        /// chase took guards hundreds of px off their way into the enemy's posts) through the command layer, as every order of the
        /// computer. False = refused, or every point dropped by the command layer's checks.</summary>
        bool DefendRoute(DfGuard g, int sx, int sy)
        {
            var o = g.o; bool robot = o.Kind == ZKind.Robot;
            var reach = W.Pathing?.Reach;
            int comp = reach != null ? reach.UnitComp(o, false) : -1;
            int back = DfBack(o), bx = (int)Math.Round(sx - g.dx * back), by = (int)Math.Round(sy - g.dy * back);
            var list = new List<ZWaypoint>();
            if (comp >= 0 && DfStraight(reach, comp, robot, bx, by, sx, sy) && DfLineFree(o, bx, by, sx, sy)) list.Add(ZWaypoint.Move(bx, by, false));
            list.Add(ZWaypoint.Move(sx, sy, false));
            if (!W.SendWaypoints(Team, o, list) || o.Waypoints.Count == 0) return false;
            if (ClientView) Relay(o);
            return true;
        }

        /// <summary>The straight line from (x0, y0) to (x1, y1) is open ground for the unit's kind in its own part of the path graph (a footprint
        /// every 8 px fits there): the last stretch is walked straight, not by a way round.</summary>
        static bool DfStraight(ZReach reach, int comp, bool robot, int x0, int y0, int x1, int y1)
        {
            double dx = x1 - x0, dy = y1 - y0;
            int n = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / 8));
            for (int i = 0; i <= n; i++)
                if (!reach.PointOk((int)Math.Round(x0 + dx * i / n), (int)Math.Round(y0 + dy * i / n), robot, false, comp)) return false;
            return true;
        }

        /// <summary>Nobody stands in the way of u along the line (x0, y0) - (x1, y1) (a unit that stands: the path search goes round it, so the
        /// stretch would not be walked straight): no unit but u and its squad standing within both bodies' room of the line.</summary>
        bool DfLineFree(ZObject u, int x0, int y0, int x1, int y1)
        {
            double lx = x1 - x0, ly = y1 - y0, l2 = lx * lx + ly * ly;
            int ru = ZPathing.Radius(u);
            foreach (var o in W.Objects)
            {
                if (!o.IsUnit || o == u || o.Leader == u || o.Destroyed || o.KillAt >= 0) continue;
                if (o.Waypoints.Count > 0 && o.IsMoving) continue;                              // passing through
                double s = l2 > 0 ? Math.Max(0, Math.Min(1, ((o.CenterX - x0) * lx + (o.CenterY - y0) * ly) / l2)) : 0;
                double qx = x0 + lx * s - o.CenterX, qy = y0 + ly * s - o.CenterY, lim = ru + ZPathing.Radius(o) + 4;
                if (qx * qx + qy * qy < lim * lim) return false;
            }
            return true;
        }

        /// <summary>The length of the last stretch toward the enemy: a robot <see cref="ZWorld.CpuDefendBackPx"/>, a vehicle <see cref="ZWorld.CpuDefendBackVehiclePx"/>.</summary>
        static int DfBack(ZObject o) => o.Kind == ZKind.Vehicle ? ZWorld.CpuDefendBackVehiclePx : ZWorld.CpuDefendBackPx;

        /// <summary>A spot nobody else guards (CpuDefendPitchPx from every other guard's spot) and nobody stands on.</summary>
        bool DefendSpotFree(int x, int y, ZObject u)
        {
            long pitch2 = (long)ZWorld.CpuDefendPitchPx * ZWorld.CpuDefendPitchPx;
            foreach (var g in dfGuards) if (g.o != u && (long)(g.sx - x) * (g.sx - x) + (long)(g.sy - y) * (g.sy - y) < pitch2) return false;
            foreach (var o in W.Objects)
            {
                if (!o.IsUnit || o == u || o.Leader == u || o.Destroyed || o.KillAt >= 0) continue;
                if (o.Waypoints.Count > 0 && o.IsMoving) continue;                              // passing through
                long lim = 20 + ZPathing.Radius(o), ddx = o.CenterX - x, ddy = o.CenterY - y;
                if (ddx * ddx + ddy * ddy < lim * lim) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ the tick (ZBot.Tick, every server tick, after the strike groups)
        void DefendTick()
        {
            if (!ZWorld.CpuDefendDeviation || Ignored)
            {
                if (dfGuards.Count > 0) DefendEndAll(ZWorld.CpuDefendDeviation ? "the computer was stopped" : "switched off");
                dfOwed = false;
                return;
            }
            if (dfGuards.Count == 0) return;
            DefendUpkeep();
            if (W.Time + TIME_EPS < dfNextStep) return;
            dfNextStep = W.Time + 1.0;
            DefendStep();
        }

        static bool DfAlive(ZObject o) => o != null && !o.Destroyed && o.KillAt < 0;
        /// <summary>The guard holds an order that is not of this rule: the defend orders are plain moves (refId -1) and the empty hold (a
        /// factory's way out, a force move, is kept in front by the command layer); any order given with another mode or onto an object (a
        /// repair trip, an enter, an attack, a flag) came from somewhere else.</summary>
        static bool DfForeign(ZObject o)
        {
            foreach (var w in o.Waypoints) if (w.playerGiven && w.mode != WpMode.ForceMove && (w.mode != WpMode.Move || w.refId >= 0)) return true;
            return false;
        }
        static long DfD2(ZObject o, int x, int y) { long dx = o.CenterX - x, dy = o.CenterY - y; return dx * dx + dy * dy; }

        /// <summary>Every tick: who is gone, whose squad has a new leader; a guard that stopped on its way is looked at at once (so it never
        /// stands there unheld and chases off by itself).</summary>
        void DefendUpkeep()
        {
            for (int i = 0; i < dfGuards.Count;)
            {
                var g = dfGuards[i]; var o = g.o;
                bool here = DfAlive(o) && W.Get(o.Id) == o && o.Owner == Team;
                if (!here)
                {
                    if (!g.fell && W.Get(o.Id) == o && o.Destroyed && o.Owner == Team && o.Minions.Count > 0) { g.fell = true; i++; continue; }   // a leader fell in this tick: its squad's next leader is known in the next one
                    o.cpuDefendGuard = false;
                    if (o.Owner != Team) { o.HoldPosition = false; foreach (var mn in o.Minions) if (mn != null) mn.HoldPosition = false; }   // no hold of ours left on a unit another team now has
                    var next = o.Successor;
                    if (o.Destroyed && DfAlive(next) && W.Get(next.Id) == next && next.Owner == Team && !next.IsMinion && !next.cpuDefendGuard)
                    {
                        g.o = next; g.fell = false; next.cpuDefendGuard = true;
                        DfLog(DefendEventKind.Successor, next.Id, g.flagId, 0, 0, o.Id);
                        continue;                                                                   // (looked at again: it may have fallen too)
                    }
                    DefendLost++; DfLog(DefendEventKind.Lost, o.Id, g.flagId);
                    dfGuards.RemoveAt(i);
                    continue;
                }
                if (DfForeign(o)) { DefendRelease(g, "given another order", false); continue; }   // another rule of the computer gave it a task of its own (a repair trip, a ride): that one goes first
                if (g.phase != DfPhase.Hold)
                {   // where it went in its last 8 px (the view turns a body to where it last moved)
                    int mx = o.CenterX - g.ax, my = o.CenterY - g.ay;
                    if (mx * mx + my * my >= 64) { g.lx = mx; g.ly = my; g.ax = o.CenterX; g.ay = o.CenterY; }
                    if (o.Waypoints.Count == 0 && !o.IsMoving && DefendStopped(g)) continue;   // released
                }
                i++;
            }
        }

        /// <summary>A guard on its way (or walking back) stands without an order. On its spot (<see cref="ZWorld.CpuDefendSettlePx"/>) it settles,
        /// also while it fires at an enemy in its range. Short of it and firing, it fires on (it goes on when that fight is over); else it is sent on (<see cref="ZWorld.CpuDefendResends"/> times at most without coming nearer), then once to the free spot
        /// nearest where it stands; then it settles where it stands when that is a place a guard may take (<see cref="DefendStandOk"/>), else it
        /// is released. True = it was released (removed).</summary>
        bool DefendStopped(DfGuard g)
        {
            var o = g.o;
            long d2 = DfD2(o, g.sx, g.sy);
            if (d2 <= (long)ZWorld.CpuDefendSettlePx * ZWorld.CpuDefendSettlePx) { DefendSettle(g); return false; }
            if (o.AttackObject != null) return false;                                           // short of its spot and firing at an enemy in its range: it goes on when that fight is over
            double off = Math.Sqrt(d2);
            if (g.bestOff < 0 || off < g.bestOff - 8) { g.bestOff = off; g.tries = 0; }
            if (g.tries < ZWorld.CpuDefendResends)
            {
                g.tries++;
                if (DefendRoute(g, g.sx, g.sy)) { DfLog(DefendEventKind.Resent, o.Id, g.flagId, g.sx, g.sy, (int)Math.Round(off)); return false; }
            }
            if (!g.repicked) { g.repicked = true; if (DefendRepick(g)) return false; }
            if (DefendStandOk(g, o.CenterX, o.CenterY)) { g.sx = o.CenterX; g.sy = o.CenterY; DefendSettle(g); return false; }
            DefendRelease(g, "could not reach its spot", true);
            return true;
        }

        /// <summary>Once, when it cannot reach its spot: the free spot it reaches nearest where it stands (within 96 px), the same flag.</summary>
        bool DefendRepick(DfGuard g)
        {
            var o = g.o; bool robot = o.Kind == ZKind.Robot;
            W.CpuDefendSpots(g.front, Team, robot, dfSpots);
            var reach = W.Pathing?.Reach;
            int comp = reach != null ? reach.UnitComp(o, false) : -1, best = -1;
            long bd = 96L * 96;
            for (int k = 0; k < dfSpots.Count; k++)
            {
                var p = dfSpots[k];
                long d = DfD2(o, p.x, p.y);
                if ((p.x == g.sx && p.y == g.sy) || d >= bd || !DefendSpotFree(p.x, p.y, o)) continue;
                if (comp >= 0 ? !reach.PointOk(p.x, p.y, robot, false, comp) : !InSameRegion(o.LocX + 8, o.LocY + 8, p.x, p.y, robot)) continue;
                bd = d; best = k;
            }
            if (best < 0) return false;
            int sx = dfSpots[best].x, sy = dfSpots[best].y;
            if (!DefendRoute(g, sx, sy)) return false;
            g.sx = sx; g.sy = sy; g.tries = 0; g.bestOff = -1;
            DfLog(DefendEventKind.Resent, o.Id, g.flagId, sx, sy, -1, "another spot near it");
            return true;
        }

        /// <summary>A place a guard may hold although it is not its spot: <see cref="ZWorld.CpuDefendNearPx"/> - 16 to
        /// <see cref="ZWorld.CpuDefendFarPx"/> + 24 px from its flag, on the enemy side of it, in a territory of ours, 40 px from every flag, 32 px
        /// from every other guard's spot.</summary>
        bool DefendStandOk(DfGuard g, int x, int y)
        {
            var flag = W.Get(g.flagId);
            if (flag == null) return false;
            long fx = x - flag.CenterX, fy = y - flag.CenterY;
            double d = Math.Sqrt(fx * fx + fy * fy);
            if (d < ZWorld.CpuDefendNearPx - 16 || d > ZWorld.CpuDefendFarPx + 24 || fx * g.dx + fy * g.dy < 0) return false;
            int zi = W.GetZoneIndex(x, y);
            if (zi < 0 || W.Zones[zi].Owner != Team) return false;
            foreach (var f in W.Flags) { long ex = f.CenterX - x, ey = f.CenterY - y; if (ex * ex + ey * ey < 40 * 40) return false; }
            foreach (var h in dfGuards) if (h != g && (long)(h.sx - x) * (h.sx - x) + (long)(h.sy - y) * (h.sy - y) < 32 * 32) return false;
            return true;
        }

        /// <summary>Its last 8 px of movement point toward the enemy (within 60 degrees of the enemy direction).</summary>
        static bool DefendFaces(DfGuard g)
        {
            long l2 = (long)g.lx * g.lx + (long)g.ly * g.ly;
            return l2 > 0 && g.lx * g.dx + g.ly * g.dy >= 0.5 * Math.Sqrt(l2);
        }

        /// <summary>On the place it holds: when it did not come there walking toward the enemy (<see cref="DefendFaces"/>) and is not firing at
        /// an enemy, it first turns to the enemy the only way a body turns, by walking (once, <see cref="DefendFaceStep"/>); then it holds.</summary>
        void DefendSettle(DfGuard g)
        {
            if (!g.faced && g.o.AttackObject == null && !DefendFaces(g)) { g.faced = true; if (DefendFaceStep(g)) return; }   // (firing, it faces its target already)
            DefendHold(g);
        }

        /// <summary>The turn to the enemy: from where it stands its last stretch's length (<see cref="DfBack"/>, or three quarters of it) straight back,
        /// then forward to where it stood; with no room behind it, one step of 24 px forward when a guard may stand there. Its spot is
        /// where that walk ends. False = no such walk (it holds as it stands).</summary>
        bool DefendFaceStep(DfGuard g)
        {
            var o = g.o; bool robot = o.Kind == ZKind.Robot;
            var reach = W.Pathing?.Reach;
            int comp = reach != null ? reach.UnitComp(o, false) : -1;
            if (comp < 0) return false;
            int px = o.CenterX, py = o.CenterY;
            for (int k = 0; k < 2; k++)
            {
                int l = k == 0 ? DfBack(o) : DfBack(o) * 3 / 4;
                int bx = (int)Math.Round(px - g.dx * l), by = (int)Math.Round(py - g.dy * l);
                if (!DfStraight(reach, comp, robot, px, py, bx, by) || !DfLineFree(o, px, py, bx, by)) continue;   // straight back and straight forward again, no way round
                if (!W.SendWaypoints(Team, o, new[] { ZWaypoint.Move(bx, by, false), ZWaypoint.Move(px, py, false) }) || o.Waypoints.Count == 0) return false;
                if (ClientView) Relay(o);
                g.sx = px; g.sy = py; g.tries = 0; g.bestOff = -1; g.walkFrom = W.Time;
                DfLog(DefendEventKind.Faces, o.Id, g.flagId, px, py, l);
                return true;
            }
            int fx = (int)Math.Round(px + g.dx * 24), fy = (int)Math.Round(py + g.dy * 24);
            if (!DfStraight(reach, comp, robot, px, py, fx, fy) || !DfLineFree(o, px, py, fx, fy) || !DefendStandOk(g, fx, fy)) return false;
            if (!W.SendWaypoints(Team, o, new[] { ZWaypoint.Move(fx, fy, false) }) || o.Waypoints.Count == 0) return false;
            if (ClientView) Relay(o);
            g.sx = fx; g.sy = fy; g.tries = 0; g.bestOff = -1; g.walkFrom = W.Time;
            DfLog(DefendEventKind.Faces, o.Id, g.flagId, fx, fy, -24);
            return true;
        }

        void DefendHold(DfGuard g)
        {
            var o = g.o;
            if (!o.HoldPosition && W.SendHold(Team, o) && ClientView) Relay(o);
            bool first = g.arrived < 0;
            if (first) g.arrived = W.Time;
            g.phase = DfPhase.Hold; g.tries = 0; g.bestOff = -1;
            DfLog(DefendEventKind.Holds, o.Id, g.flagId, o.CenterX, o.CenterY, (int)Math.Round(Math.Sqrt(DfD2(o, g.sx, g.sy))), first ? null : "back on its spot");
        }

        /// <summary>Once a second: the flag lost, the hold's end, the walk's limit, a guard knocked off its spot.</summary>
        void DefendStep()
        {
            for (int i = 0; i < dfGuards.Count;)
            {
                var g = dfGuards[i]; var o = g.o;
                var flag = W.Get(g.flagId);
                if (flag == null || flag.Owner != Team) { DefendRelease(g, "its flag was lost", true); continue; }
                if (g.arrived >= 0 && W.Time - g.arrived >= ZWorld.CpuDefendHoldSec - TIME_EPS) { DefendRelease(g, "held its spot " + (ZWorld.CpuDefendHoldSec / 60).ToString("0") + " minutes", true); continue; }
                if (g.phase == DfPhase.Walk && W.Time - g.walkFrom >= ZWorld.CpuDefendWalkSec - TIME_EPS)
                {   // walked too long: it settles where it is when a guard may hold there, else it is free again
                    if (DefendStandOk(g, o.CenterX, o.CenterY)) { g.sx = o.CenterX; g.sy = o.CenterY; DefendSettle(g); }
                    else { DefendRelease(g, "did not reach its spot in " + ZWorld.CpuDefendWalkSec.ToString("0") + " s", true); continue; }
                }
                if (g.phase == DfPhase.Hold && o.Waypoints.Count == 0 && !o.IsMoving && o.AttackObject == null
                    && DfD2(o, g.sx, g.sy) > (long)ZWorld.CpuDefendDriftPx * ZWorld.CpuDefendDriftPx && g.returns < ZWorld.CpuDefendReturns)
                {   // knocked off its spot (a step aside from a shell, a push): back to it, the last stretch toward the enemy again
                    g.returns++;
                    if (DefendRoute(g, g.sx, g.sy)) { g.phase = DfPhase.Return; g.ax = o.CenterX; g.ay = o.CenterY; DfLog(DefendEventKind.Returns, o.Id, g.flagId, g.sx, g.sy); }
                }
                i++;
            }
        }

        /// <summary>The guard is free: no hold of ours on it; with <paramref name="orders"/> it gets an order of the computer's own kind at once
        /// (the engine's matching for this one unit, its nearest target; never turned into a defend order again; the Easy limit is kept).</summary>
        void DefendRelease(DfGuard g, string why, bool orders)
        {
            var o = g.o;
            dfGuards.Remove(g);
            o.cpuDefendGuard = false;
            dfFreed[o.Id] = W.Time;
            bool ours = DfAlive(o) && W.Get(o.Id) == o && o.Owner == Team;
            if (ours) { o.HoldPosition = false; foreach (var mn in o.Minions) if (mn != null) mn.HoldPosition = false; }
            DefendReleased++;
            DfLog(DefendEventKind.Released, o.Id, g.flagId, o.CenterX, o.CenterY, g.arrived >= 0 ? (int)Math.Round(W.Time - g.arrived) : -1, why);
            if (!orders || !ours || o.IsMinion) return;
            bool allOut = GoAllOut_3(out _, out _);
            var targets = new List<ZObject>();
            CollectOurTargets_3(targets, allOut);
            MatchTargets_3(new List<ZObject> { o }, targets);
            ZObject best = null; double bd = double.MaxValue;
            foreach (var t in AI(o)) { double d = Dist(o, t); if (d < bd) { bd = d; best = t; } }
            if (best == null) return;                                                           // nothing to go for: it stands idle (the planner finds it as today)
            int before = Orders;
            dfNoTurn = true;
            try { Command(o, best); }
            finally { dfNoTurn = false; }
            if (Orders > before) DfLog(DefendEventKind.OrderAfter, o.Id, g.flagId, 0, 0, best.Id);
        }

        /// <summary>Switched off, or the computer stopped: every guard is free at once, without an order (the planner finds it idle).</summary>
        void DefendEndAll(string why)
        {
            while (dfGuards.Count > 0) DefendRelease(dfGuards[0], why, false);
        }
    }
}
