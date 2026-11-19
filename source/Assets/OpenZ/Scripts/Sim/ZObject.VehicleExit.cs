using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// Zod: only an APC and an ejectable cannon eject (vapc.cpp:261, zcannon.cpp:51; EJECT_VEHICLE zserver_events.cpp:631-731); a tank keeps
    /// its driver until he is sniped or the tank dies, and an empty vehicle is auto-entered by any idle robot within auto_grab_vehicle_distance
    /// (zobject.cpp:1603-1853).
    /// OpenZ, while <see cref="ZWorld.VehicleExitDeviation"/> is on (View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off):
    /// <list type="bullet">
    /// <item>The four hatch vehicles (<see cref="IsHatchVehicle"/>: the light tank "light", the medium tank ZMTank.Type, the heavy tank "heavy"
    /// and the MECH ZMTank.Mech = Zod's "medium") take the same EJECT_VEHICLE command a gun takes (ZWorld.Eject, from PlayerInput.EjectUnit),
    /// for a HUMAN team only (the computer never uses it). The driver comes out as the same robot (type, hit points, kills and rank, hero
    /// identity) on free ground beside the hull (<see cref="ZWorld.DriverExitSpot(ZObject, out int, out int, out string)"/>); the vehicle is
    /// emptied as a gun is (neutral, stopped, no target, no order) in that same tick, its lid shut and its DV75 rest over. Refused, nothing
    /// changes (<see cref="CanExitDriver(out string)"/> says why): on a bridge; no free spot with room on its side of the hull (fix round 3:
    /// a spot is taken only when the half of the first ring's square that faces it holds no wall, cliff, rock or building: his climb down
    /// and his walk round the hull are on that side; a flag stops only a spot it overlaps); on its factory / repair-bay exit move.</item>
    /// <item>Fix round 2: the driver is GETTING OUT (rule DV132's hold, Sim/ZObject.ExitWalk.cs: no step, no shot, his orders wait) for
    /// <see cref="DriverOutHoldTicks"/> of his vehicle; vehicle_exit_jump (6 Nov 2026): until his figure, jumping off the hull with one
    /// somersault, touches the ground (View/BoardingView.VehicleExit.cs: 1.36-1.46 s; before, a climb down at a human pace for 5-8.7 s); the
    /// hold ends at once when the hull is destroyed meanwhile (<see cref="DriverOutHullGone"/>: his figure takes the same jump at once).
    /// The vehicle itself stops dead in the EXIT's own tick: no order, path or target, its tick ends before every fire step (DV79, below in
    /// ExitDriver; counted by Editor/OzV14VehicleExitStopCount.cs, vehicle_exit_jump part B).
    /// APC passengers he IS a target while held (<see cref="DriverOutTarget"/>, read by CanAttackObject in Sim/ZObject.cs): enemies pick him,
    /// shoot him and can kill him; the view draws their shots, his hits and his death on his climbing figure. Before, nobody could pick him
    /// for the whole climb (5.2-8.4 s) while splash at the emptied hull still killed him. A second EXIT on the same vehicle waits until he is
    /// down (<see cref="ZWorld.DriverStillClimbing"/>: one driver at a time on the ladder; the view never draws two of them on one hull).
    /// Final fix: while held he is on the hull, not on the ground (<see cref="DriverOutOnHull"/>): no vehicle runs him over (DV65) and no jeep
    /// charges him (DV121) until his hold ends.</item>
    /// <item>For <see cref="DriverOutLockTicks"/> sim ticks (30 s) no unit of any team climbs into that vehicle BY ITSELF: <see cref="AutoEnterLocked"/>
    /// (ZObject.BoardingRules.cs) also reads <see cref="DriverOutLocked"/>, and every automatic entry asks it (the idle auto-grab with DV55's
    /// closest grabber, DV84's grab on the way, an automatic ENTER waypoint already on its way). A direct ENTER order (the player's right-click,
    /// the computer's own capture orders: SEND_WAYPOINTS, playerGiven) gets in at once; the lock then ends (RobotEnterObject).</item>
    /// </list>
    /// Deterministic: sim ticks, the pixel grid, fixed object and spot order, no random draw (the lid is shut before the vehicle lets go of
    /// its target, so Disengage draws no lid roll). With the switch off, or with nobody giving EXIT on a tank, the sim is the previous build
    /// bit for bit: a tank's EJECT_VEHICLE was refused (CanEjectDrivers) and still is; the lock field stays -1, no hold is given.
    /// </summary>
    public partial class ZObject
    {
        /// <summary>DV171: a light, medium or heavy tank or the mech: the vehicles a driver enters through a roof hatch with a lid.</summary>
        public bool IsHatchVehicle => Kind == ZKind.Vehicle && HasLid && !IsShuttle && (Type == "light" || Type == ZMTank.Type || Type == "heavy" || Type == ZMTank.Mech || ZBigTanks.Is(Type));   // 1.5 DV180 / DV181: the big tanks' turret hatch

        /// <summary>DV171: sim tick until which nobody climbs into this vehicle by himself (-1 = none).</summary>
        public long DriverOutLockUntilTick = -1;
        /// <summary>DV171: 30 s at Zod's 100 Hz server tick.</summary>
        public const long DriverOutLockTicks = 3000;
        /// <summary>DV171: the vehicle's driver climbed out less than 30 s ago and nobody got in since: no automatic entry.</summary>
        public bool DriverOutLocked => ZWorld.VehicleExitDeviation && DriverOutLockUntilTick >= 0 && World != null && World.TickIndex < DriverOutLockUntilTick;
        /// <summary>DV171: seconds of sim time the lock still holds (0 = none). View and checks.</summary>
        public double DriverOutLockLeftSec => DriverOutLocked ? (DriverOutLockUntilTick - World.TickIndex) * ZConst.TICK : 0.0;

        /// <summary>DV171: the player may let this vehicle's driver out now (why = the reason when not).</summary>
        public bool CanExitDriver(out string why) => CanExitDriver(out why, out _, out _);

        /// <summary>DV171: as <see cref="CanExitDriver(out string)"/>, with the centre of the spot the driver would stand on (Zod px). A pure read
        /// of the sim's state (the HUD asks between ticks).</summary>
        public bool CanExitDriver(out string why, out int sx, out int sy)
        {
            sx = sy = -1; why = null;
            if (!ZWorld.VehicleExitDeviation) { why = "off in 'Zod exact'"; return false; }
            if (!IsHatchVehicle) { why = "only a tank or the mech"; return false; }
            if (World == null || Destroyed || KillAt >= 0) { why = "destroyed"; return false; }
            if (RamDoomed) { why = "destroyed"; return false; }                                           // 1.5 DV182: rammed by a Mammoth: no way out (Sim/ZObject.MammothRam.cs)
            if (Owner == Team.Neutral || Drivers.Count == 0) { why = "nobody inside"; return false; }
            if (!World.DuelAllowsUnit(DriverType)) { why = "crew type excluded by lobby rules"; return false; }
            if (!World.IsHumanTeam(Owner)) { why = "a computer team's vehicle"; return false; }
            if (!CanOverwriteWP() || PathInBuildingStage) { why = ZWorld.DriverWhyFactory; return false; }
            if (World.DriverStillClimbing(this)) { why = ZWorld.DriverWhyClimbing; return false; }               // fix round 4: the last driver let out of it is still held on its hull
            return World.DriverExitSpot(this, out sx, out sy, out why);
        }

        /// <summary>
        /// 2026: from the click until his figure's feet touch the ground beside the hull
        /// (View/BoardingView.VehicleExit.cs): the lid opens at Zod's lid timing (0.40 s), he shows crouched on the rim in its last 0.10 s and
        /// springs (0.12 s, his feet planted: the take-off 0.42 s after the click), then one
        /// ballistic arc with a somersault under 9.8 m/s2 (8 px = 1 m) from the hatch rim (about 2.75 m above the ground on the light tank, 2.65 m
        /// on the medium tank, 2.15 m on the heavy tank, 3.65 m on the mech: the prefabs' boarding anchors) to a landing toward his robot that
        /// clears the vehicle and its open lid: light tank 145 ticks (1.45 s, a 1.03 s flight), medium tank 142 (a 1.00 s flight), heavy tank 136
        /// (a 0.94 s flight), mech 146 (a 1.04 s flight; inside 1.5 s with a margin for the drawn touch; fix round 2: each a real upward jump). Before (the climb
        /// down at a human pace): light 687,
        /// medium 520, heavy 670, mech 840. Fixed per type: integer ticks, deterministic, no view input. The view lands its figure on this tick
        /// (it moves and turns the landing until the arc clears the hull; none clears: a longer flight, LATE); the Play check measures the touch (H5). A
        /// change of these numbers is a kit change (vehicle_exit_jump kit/edits.py HOLDS). 0 = not a hatch vehicle.</summary>
        public static int DriverOutHoldTicks(string type) => type == "light" ? 145 : type == ZMTank.Type ? 142 : type == "heavy" ? 136 : type == ZMTank.Mech ? 146 : type == ZBigTanks.Mammoth ? ZBigTanks.MammothExitHoldTicks : type == ZBigTanks.Bizon ? ZBigTanks.BizonExitHoldTicks : 0;   // 1.5 DV180 / DV181

        /// <summary>DV171 (fix round 2): the vehicle this robot climbed out of and the DV132 out moment it got then (-1: none). The hold ends early
        /// only for that hold (an APC's or the shuttle's later hold has another out moment).</summary>
        public int DriverOutFromId = -1; public double DriverOutAt = -1;

        /// <summary>DV171: held now by THIS rule's hold (his out moment is the one ExitDriver gave him; an APC's or the shuttle's later hold has
        /// another). A pure read.</summary>
        public bool DriverOutHeld => DriverOutFromId >= 0 && ExitOutAt >= 0 && Math.Abs(ExitOutAt - DriverOutAt) <= 1e-9;

        /// <summary>DV171 fix round 4: a driver held while he climbs out of a tank or the mech is a target all the same (CanAttackObject, Sim/ZObject.cs,
        /// this rule's hold: DV132's shuttle and APC passengers stay nobody's target. A pure read; with nobody let out by EXIT DRIVER it is never
        /// true (the computer never uses it), so a bot match is the previous build's tick for tick.</summary>
        public bool DriverOutTarget => ZWorld.VehicleExitDeviation && (DriverOutHeld || DriverThrownHeld);   // 1.5 DV179 (kit exit_leap_death): a driver thrown by his hull's blast is a target while he lies (Sim/ZObject.DriverThrown.cs)

        /// <summary>DV171 final fix (review r3: an enemy jeep or tank ran over the held driver, knocked him and squashed him while his figure was
        /// still on the hull: the rules keep him on his ground spot during the hold, and DV65's run-over did not skip him): held by this rule's
        /// hold and the hold not over yet (DriverOutHeld, ExitWalking): he is on the hull, not on the ground. DV65's run-over (Sim/ZWorld.RunOver.cs,
        /// one line) and DV121's jeep charge (Sim/ZObject.JeepCharge.cs ChargeWants, one line) skip him while it lasts, as they skip a fort
        /// defender or a robot in a building; once his hold ends he is run over like any robot. A pure read; with nobody let out by EXIT DRIVER
        /// it is never true (the computer never uses it), so a bot match is the previous build's tick for tick.</summary>
        public bool DriverOutOnHull => DriverOutHeld && World != null && ExitWalking(World.Time);

        /// <summary>DV171 (fix round 2), ExitWalkHold (Sim/ZObject.ExitWalk.cs) every tick this robot is held: true = he is held for climbing out
        /// of a hull that is now destroyed (or dying): the hold ends in this tick (his figure jumps down, the view's HatchBail). A read of the
        /// object list by id: deterministic.</summary>
        internal bool DriverOutHullGone()
        {
            if (DriverOutFromId < 0) return false;
            if (Math.Abs(ExitOutAt - DriverOutAt) > 1e-9) { DriverOutFromId = -1; return false; }               // another hold since (an APC, the shuttle): not this rule's
            var v = World != null ? World.GetAny(DriverOutFromId) : null;
            if (v != null && !v.Destroyed && v.KillAt < 0) return false;
            DriverOutFromId = -1; ZWorld.DriverHoldsCut++;
            return true;
        }

        /// <summary>DV171: the hatch shuts behind him (no pending lid close, no lid roll later) and a DV75 rest ends, as RestStep ends it for an
        /// empty hull; the next driver starts buttoned up.</summary>
        internal void DriverOutReset() { LidOpen = false; doCloseLid = false; RestEnd(-1); }

        /// <summary>DV171 fix round 5 (review: an L17 start crew already on its way walked into the vehicle 17 s after an EXIT, inside the lock):
        /// every ENTER on v in this leader's orders (an L17 start crew's, DV63: playerGiven, so the lock's own test lets it in) becomes DV59's walk
        /// to the spot next to v (FallbackGoal, the same MOVE EnterFallback makes), at any place in the list; the squad gets the list at once
        /// (CloneMinionWayPoints, as SendWaypoints does). True = one was turned. Ints and the path map only: deterministic.</summary>
        internal bool DriverOutEnterToWalk(ZObject v)
        {
            bool any = false;
            for (int i = 0; i < Waypoints.Count; i++)
            {
                var wp = Waypoints[i];
                if (wp.mode != WpMode.Enter || wp.refId != v.Id) continue;
                FallbackGoal(v, wp, out int gx, out int gy);
                Waypoints[i] = new ZWaypoint { mode = WpMode.Move, refId = -1, x = gx, y = gy, attackTo = false, playerGiven = wp.playerGiven };
                if (i == 0) { PS.ClearPending(); foreach (var m in Minions) m?.PS.ClearPending(); }        // a fresh order starts clean (as EnterFallback)
                any = true;
            }
            if (any && Minions.Count > 0) CloneMinionWayPoints();
            return any;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV171 EXIT DRIVER for tanks and the mech (Sim/ZObject.VehicleExit.cs). View/ZGame.cs: on at every start, off in 'Zod
        /// exact'; headless default off (every Zod row, every older counted check and the bot tools as before).</summary>
        public static bool VehicleExitDeviation = false;
        /// <summary>DV171 statistics (checks): drivers let out; EXIT orders on a hatch vehicle refused.</summary>
        public int DriverExits, DriverExitsRefused;
        /// <summary>DV171 + DV132 statistics (checks, all worlds): drivers given a getting-out hold; holds ended early because the hull was destroyed.</summary>
        public static int DriverHoldsSet, DriverHoldsCut;
        /// <summary>DV171: the first ring of spots is this far (px, square ring) from the hull's centre at least: the chassis spacing radius
        /// (ZPathing.VehicleRadiusOf) + a robot's 8 px half box + 2 px (light 27, medium tank and mech 28, heavy 30); the second ring 16 px
        /// further out. Fix round 3: a spot needs room on ITS side of the hull only: the half of the square of half side DriverExitRing1 round
        /// the hull's centre that faces the spot (<see cref="DriverRoomFree"/>; a corner spot: its quarter) free of robot-impassable tiles and
        /// buildings. The view climbs down on the robot's side (View/BoardingView.cs LadderSide: the ladder only from its own half; FlankPoint:
        /// the robot's flank, toward him) and walks round the hull on that side (Around, 0.45 m outside the drawn hull). Fix round 1 tested the
        /// whole square, flags included: a wall, a building or a flag beside the side the driver never uses refused the EXIT in 25-45 % of
        /// normal tank positions (every one within 40 px of a flag). A second-ring spot is taken only when the first-ring box in the same
        /// direction is free of terrain and buildings too (other units may stand there), so no wall lies between the hull and the robot (fix
        /// round 1: a spot behind a one-tile wall beside a corridor was taken).</summary>
        public const int DriverExitRingMin = 26, DriverExitRingStep = 16, DriverExitSearchPx = 96;
        /// <summary>DV171: the spot order of each ring: S, SE, SW, E, W, NE, NW, N (+y is toward the RTS camera: the side the player sees).</summary>
        public static readonly int[] DriverExitDx = { 0, 1, -1, 1, -1, 1, -1, 0 }, DriverExitDy = { 1, 1, 1, 0, 0, -1, -1, -1 };
        public static int DriverExitRing1(string type) => Math.Max(DriverExitRingMin, ZPathing.VehicleRadiusOf(type) + 10);
        /// <summary>DV171: a robot's spacing radius by type (ZPathing.Radius for a robot of that type).</summary>
        public static int DriverRadiusOf(string type) => type == ZEvilZod.Type ? ZEvilZod.BodyRadius : type == "tough" || type == ZElectro.Type ? ZPathing.ToughRadius : ZPathing.RobotRadius;
        /// <summary>DV171: CanExitDriver's reasons that the HUD's tooltip names.</summary>
        public const string DriverWhyRoom = "no free ground beside the hatch", DriverWhyBridge = "on a bridge", DriverWhyFactory = "leaving the factory or in the repair bay",
                            DriverWhyClimbing = "the last driver is still getting out";

        /// <summary>DV171 fix round 4: a robot let out of v by EXIT DRIVER is still held as getting out (his figure still climbs down its hull):
        /// v's next EXIT waits (the tooltip: "The driver cannot get out now."). Before, a driver ordered in meanwhile could be let out again at
        /// once, and the view drew two figures of one robot on one hull. A read of the object list in its order: deterministic.</summary>
        public bool DriverStillClimbing(ZObject v)
        {
            if (v == null) return false;
            foreach (var o in Objects)
                if (o.Kind == ZKind.Robot && o.DriverOutFromId == v.Id && !o.Destroyed && o.KillAt < 0 && o.DriverOutHeld && o.ExitWalking(Time)) return true;
            return false;
        }

        readonly List<ZObject> dxNear = new List<ZObject>(), dxBlocks = new List<ZObject>(), dxFlags = new List<ZObject>();
        readonly byte[] dxRoom = new byte[8];                                                       // fix round 3: per direction 0 = not asked yet, 1 = room, 2 = none

        /// <summary>DV171: the free spot beside vehicle v where its driver climbs out (centre, Zod px); see the four-argument form.</summary>
        public bool DriverExitSpot(ZObject v, out int cx, out int cy) => DriverExitSpot(v, out cx, out cy, out _);

        /// <summary>DV171: the free spot beside vehicle v where its driver climbs out (centre, Zod px). Refused (why): the hull on a bridge
        /// (<see cref="DriverWhyBridge"/>: the deck is two tiles between walls, no room to climb down beside the hull); no free spot with room
        /// on its side (<see cref="DriverWhyRoom"/>). Two square rings round the hull's centre, eight spots each in <see cref="DriverExitDx"/>
        /// order; the first spot whose side of the hull has room (fix round 3, <see cref="DriverRoomFree"/>: the half of the first ring's
        /// square facing it holds no terrain or building) and whose 16 x 16 box lies 8 px inside the map, on robot-passable tiles, in the
        /// hull's robot region (Zod's region map), off lava, overlaps no building and no flag, and keeps the spacing radius from every other
        /// live unit; a second-ring spot also needs the first-ring box in its direction free of terrain and buildings. A pure read in fixed
        /// order (no random draw).</summary>
        public bool DriverExitSpot(ZObject v, out int cx, out int cy, out string why)
        {
            cx = cy = -1; why = DriverWhyRoom;
            if (v == null || Path == null || Path.WidthPix <= 0) return false;
            int hx = v.CenterX, hy = v.CenterY, rr = DriverRadiusOf(v.DriverType);
            int fx = hx, fy = hy;                                                                   // the hull's robot region: its centre tile, else the first robot tile of its 2 x 2 box
            if (!Path.TilePassable(hx >> 4, hy >> 4, true))
                for (int k = 0; k < 4; k++)
                {
                    int tx = (v.LocX >> 4) + (k & 1), ty = (v.LocY >> 4) + (k >> 1);
                    if (Path.TilePassable(tx, ty, true)) { fx = tx * 16 + 8; fy = ty * 16 + 8; break; }
                }
            dxNear.Clear(); dxBlocks.Clear(); dxFlags.Clear();
            int reach = DriverExitSearchPx + 8;
            foreach (var o in Objects)                                                              // collected once, in object order
            {
                if (o == v || o.Destroyed || o.KillAt >= 0) continue;
                bool flag = o.Kind == ZKind.MapItem && o.Type == "flag";
                if (o.Kind == ZKind.Building || flag)
                {
                    if (o.IsBridge)
                    {   // the hull on the deck (or part of it): refused; a bridge beside the hull: its wall tiles count in the room test below
                        if (o.LocX < v.LocX + v.WidthPix && v.LocX < o.LocX + o.WidthPix && o.LocY < v.LocY + v.HeightPix && v.LocY < o.LocY + o.HeightPix) { why = DriverWhyBridge; return false; }
                        continue;
                    }
                    if (o.LocX < hx + reach && o.LocX + o.WidthPix > hx - reach && o.LocY < hy + reach && o.LocY + o.HeightPix > hy - reach) (flag ? dxFlags : dxBlocks).Add(o);   // fix round 3: a flag is walkable, it stops only a spot on it
                    continue;
                }
                if (!o.IsUnit || o.ShuttleAirborne || o.IsFortDefender) continue;
                if (Math.Abs(o.CenterX - hx) <= reach && Math.Abs(o.CenterY - hy) <= reach) dxNear.Add(o);
            }
            int r1 = DriverExitRing1(v.Type);
            Array.Clear(dxRoom, 0, dxRoom.Length);
            for (int ring = 0; ring < 2; ring++)
            {
                int r = r1 + ring * DriverExitRingStep;
                for (int k = 0; k < DriverExitDx.Length; k++)
                {
                    if (dxRoom[k] == 0) dxRoom[k] = DriverRoomFree(hx, hy, r1, DriverExitDx[k], DriverExitDy[k]) ? (byte)1 : (byte)2;
                    if (dxRoom[k] == 2) continue;                                                    // fix round 3: a wall, cliff or building on this side of the hull (his climb down needs that room)
                    int sx = hx + DriverExitDx[k] * r, sy = hy + DriverExitDy[k] * r;
                    if (ring > 0 && !DriverGroundFree(hx + DriverExitDx[k] * r1 - 8, hy + DriverExitDy[k] * r1 - 8, 16, 16)) continue;   // a second-ring spot: nothing but units between it and the hull
                    if (DriverSpotFree(sx, sy, rr, fx, fy)) { cx = sx; cy = sy; why = null; return true; }
                }
            }
            return false;
        }

        /// <summary>DV171 fix round 3: the room on the side of the hull that faces direction (dx, dy) (DriverExitDx / Dy): the half of the square
        /// of half side r1 round the hull's centre (hx, hy) on that side: x from hx to hx + r1 (dx = 1) or hx - r1 to hx (dx = -1), the full
        /// width hx - r1 .. hx + r1 when dx = 0, the same for y; a corner spot gets its quarter. Free of terrain and buildings
        /// (<see cref="DriverGroundFree"/>; flags and units do not count).</summary>
        bool DriverRoomFree(int hx, int hy, int r1, int dx, int dy)
        {
            int x0 = dx > 0 ? hx : hx - r1, x1 = dx < 0 ? hx : hx + r1, y0 = dy > 0 ? hy : hy - r1, y1 = dy < 0 ? hy : hy + r1;
            return DriverGroundFree(x0, y0, x1 - x0, y1 - y0);
        }

        /// <summary>DV171: the box (Zod px) lies on the map on robot-passable tiles only and overlaps no building near the hull (DriverExitSpot's
        /// list; bridges are not in it, their walls are tiles). Flags (fix round 3: walkable, they stop only a spot on them, DriverSpotFree)
        /// and units do not count.</summary>
        bool DriverGroundFree(int x, int y, int w, int h)
        {
            if (Path.WithinImpassable(x, y, w, h, out _, out _, true)) return false;
            foreach (var b in dxBlocks)
                if (x < b.LocX + b.WidthPix && x + w > b.LocX && y < b.LocY + b.HeightPix && y + h > b.LocY) return false;
            return true;
        }

        bool DriverSpotFree(int sx, int sy, int rr, int fx, int fy)
        {
            int x0 = sx - 8, y0 = sy - 8;
            if (x0 < 8 || y0 < 8 || sx + 8 > Path.WidthPix - 8 || sy + 8 > Path.HeightPix - 8) return false;
            if (Path.WithinImpassable(x0, y0, 16, 16, out _, out _, true)) return false;
            if (!Path.ShouldBeAbleToMoveTo(fx, fy, sx, sy, true)) return false;
            if (Map != null && (LavaAtPx(x0, y0) || LavaAtPx(x0 + 15, y0) || LavaAtPx(x0, y0 + 15) || LavaAtPx(x0 + 15, y0 + 15))) return false;
            foreach (var b in dxBlocks)
                if (x0 < b.LocX + b.WidthPix && x0 + 16 > b.LocX && y0 < b.LocY + b.HeightPix && y0 + 16 > b.LocY) return false;
            foreach (var b in dxFlags)                                                              // fix round 3: never on a flag (a driver captures none just by climbing out)
                if (x0 < b.LocX + b.WidthPix && x0 + 16 > b.LocX && y0 < b.LocY + b.HeightPix && y0 + 16 > b.LocY) return false;
            foreach (var o in dxNear)
            {
                long min = rr + ZPathing.Radius(o), dx = o.CenterX - sx, dy = o.CenterY - sy;
                if (dx * dx + dy * dy < min * min) return false;
            }
            return true;
        }

        /// <summary>DV171, EJECT_VEHICLE on a hatch vehicle (ZWorld.Eject): the gun's eject (zserver_events.cpp:631-731 as ZWorld.Eject does it)
        /// with the driver on the free spot beside the hull, then the 30 s lock and (fix round 2) DV132's getting-out hold while ZWorld.ExitWalkHoldOn. Refused (false, nothing changed): the switch off, not the
        /// team's own vehicle, a computer team, no free spot, on its factory / repair-bay exit move.</summary>
        internal bool ExitDriver(Team team, ZObject v)
        {
            if (v == null || team == Team.Neutral || v.Owner != team || !IsHumanTeam(team) || !v.CanExitDriver(out _, out int sx, out int sy)) { DriverExitsRefused++; return false; }
            var hp = new List<int>(v.Drivers);
            var leader = CreateRobotGroup(v.DriverType, sx - 8, sy - 8, team, hp.Count);
            var squad = new List<ZObject> { leader }; squad.AddRange(leader.Minions);
            foreach (var m in squad) m.SetCords(sx - (m.WidthPix >> 1), sy - (m.HeightPix >> 1));
            CrewOut(v, squad);                                                      // veterancy_v1 DV12: the rank goes with him
            HeroCrewOut(v, squad, hp, false);                                       // DV108: a hero comes out as himself (Sim/ZHeroSquad.cs)
            for (int i = 0; i < squad.Count; i++)
            {
                squad[i].Health = Math.Min(squad[i].MaxHealth, hp[i]);
                Emit(ZEventType.Created, squad[i], v);                              // the view: out through the hatch (View/BoardingView.VehicleExit.cs)
            }
            v.Drivers.Clear(); v.ResetDamageInfo(); v.Waypoints.Clear(); v.StopMove();
            v.DriverOutReset();                                                     // the lid shut first: Disengage then draws no lid roll
            v.Disengage(); v.ClearOrderModes();
            Emit(ZEventType.Ejected, v, leader);
            ResetObjectTeam(v, Team.Neutral);
            v.UnmannedStop(false);                                                  // DV79 (when on): nothing left of its order, path or target
            DropAttacksOnEmptied(v, true);                                          // 1.4 DV176: nobody keeps shooting it: every unit of every team drops it as its target and every attack order on it (Sim/ZWorld.DropEmptied.cs)
            // DV79 is on at every start (View/ZGame.cs); with it the vehicle stops dead in this tick: no order, path, target, queued path
            // answer, patrol / hold, loaded second barrel or crane rifle (UnmannedStop), and from now on its tick ends before every fire and turn
            // step (UnmannedHold: no shot of any weapon, no hand-gun arm turn, no driver weapon). A sniped or killed driver goes the same way
            // (ZObject.DamageDriverHealth). Counted: Editor/OzV14VehicleExitStopCount.cs.
            // Fix round 3, the paths outside UnmannedHold (held all the same): a stunned vehicle's StunHold (Sim/ZObject.Electro.cs:194-216,
            // called before UnmannedHold, ZObject.cs:181: it fires) ends its stun for a neutral owner (Electro.cs:198) and ResetObjectTeam
            // clears it (ZWorld.cs:691, ClearStun; counted: B3); ScuffleUnits skips neutral units (ZWorld.cs:418); the pathing layer's end of
            // tick (ZPathing.cs:135-155: Unstack, PushApart, KeepOffBuildings, Unstick, Calm) holds them (ZPathing.Static, :82; PushShare,
            // ZSeparation.cs:614); RunOverTick needs a driver (ZWorld.RunOver.cs:58); ShuttleStep is the shuttle's only (ZObject.cs:175).
            v.DriverOutLockUntilTick = TickIndex + ZObject.DriverOutLockTicks;      // nobody climbs in by himself for 30 s
            DriverOutCrewsWalk(v);                                                  // fix round 5: an L17 start crew already on its way to v walks to the spot next to it instead (DV59's walk)
            if (ExitWalkHoldOn)
            {   // fix round 2, DV132's hold: his figure is still on the hull or in the air (no step, no shot; fix round 4: a target all the same, DriverOutTarget) until it lands (vehicle_exit_jump)
                double outAt = Time + ZObject.DriverOutHoldTicks(v.Type) * ZConst.TICK;
                foreach (var m in squad) { m.ExitOutAt = outAt; m.DriverOutFromId = v.Id; m.DriverOutAt = outAt; ExitWalksSet++; }
                DriverHoldsSet++;
            }
            DriverExits++;
            return true;
        }

        // ------------------------------------------------------------------ fix round 5: level 17 Easy's start crews already on their way
        /// <summary>DV171 fix round 5: on (the review's leak fixed): EXIT DRIVER turns the ENTER of a level 17 Easy start crew (DV63) already sent to
        /// that vehicle into DV59's walk to the spot next to it. Off = fix round 4: such a crew walked in during the 30 s lock (its ENTER is
        /// playerGiven; fix round 3's line in L17CrewsTick only skips a vehicle for crews sent after the EXIT). Code switch, for the counted
        /// check's control.</summary>
        public static bool DriverOutCrewWalkOn = true;
        /// <summary>DV171 fix round 5 statistics (checks): start crews whose ENTER an EXIT turned into a walk.</summary>
        public int DriverOutCrewWalks;
        readonly HashSet<int> l17CrewIds = new HashSet<int>();

        /// <summary>DV63's L17CrewsTick (Sim/ZWorld.L17Crews.cs, one line) after it sent a start crew with its ENTER order: the crew's robots, so that
        /// an EXIT on its vehicle can tell that ENTER (playerGiven, but the fort's, not the player's) from a real order. A record only: nothing
        /// reads it until an EXIT DRIVER (the computer never uses it), so every match without one is the previous build's. Final fix (review r3:
        /// the record was never cleared, so a former start crew's own player ENTER was turned into a walk too): a robot is in it only while the
        /// ENTER L17CrewsTick sent may still be in its orders: it leaves it at its next order (L17CrewForget), when it gets into a vehicle or dies
        /// (L17CrewGone), and once an EXIT turned that ENTER into a walk.</summary>
        void L17CrewSent(ZObject crew)
        {
            if (crew == null) return;
            l17CrewIds.Add(crew.Id);
            foreach (var m in crew.Minions) if (m != null) l17CrewIds.Add(m.Id);
        }

        /// <summary>DV171 final fix, SEND_WAYPOINTS (ZWorld.SendWaypoints, one line, once the order is taken): any new order replaces the list, so the
        /// start crew's ENTER is gone: the leader and his squad leave the record. Every order the player gives goes this way (right-click, queued
        /// orders, STOP, HOLD POSITION, PATROL), and so does an automatic one that replaces the list; L17CrewsTick's own order comes before its
        /// L17CrewSent. Nothing else is touched.</summary>
        void L17CrewForget(ZObject o)
        {
            if (l17CrewIds.Count == 0 || o == null) return;
            l17CrewIds.Remove(o.Id);
            foreach (var m in o.Minions) if (m != null) l17CrewIds.Remove(m.Id);
        }

        /// <summary>DV171 final fix, DeleteObject (ZWorld.cs, one line): a robot that got into a vehicle (RobotEnterObject: its object goes) or died
        /// leaves the record.</summary>
        void L17CrewGone(ZObject o) { if (l17CrewIds.Count > 0 && o != null) l17CrewIds.Remove(o.Id); }

        /// <summary>DV171 final fix, checks (counted L6): the robot is in the start crews' record now; how many are.</summary>
        public bool L17CrewNoted(int id) => l17CrewIds.Contains(id);
        public int L17CrewsNoted => l17CrewIds.Count;

        /// <summary>ExitDriver, after the lock: every start crew leader (object order) whose orders still hold an ENTER on v walks to the spot next to
        /// v instead (ZObject.DriverOutEnterToWalk: DV59's MOVE). Before (review, level 17 seed 3): a crew sent to the mech at 0.50 s, the
        /// player's grunt in at 0.97 s, EXIT at 2.97 s: the crew's ENTER was still behind its walk out of the fort door, and it climbed in 17.06 s
        /// after the EXIT. Only the ENTER L17CrewsTick sent is turned: a robot is asked only while it is in the record (its next order, getting
        /// into a vehicle or its death take it out, and so does this turn), so an order the player gave a former start crew himself is never
        /// touched (final fix, counted L6).</summary>
        void DriverOutCrewsWalk(ZObject v)
        {
            if (!DriverOutCrewWalkOn || l17CrewIds.Count == 0 || v == null) return;
            foreach (var o in Objects)
                if (o.Kind == ZKind.Robot && !o.IsMinion && !o.Destroyed && o.KillAt < 0 && l17CrewIds.Contains(o.Id) && o.DriverOutEnterToWalk(v)) { DriverOutCrewWalks++; L17CrewForget(o); }
        }
    }
}
