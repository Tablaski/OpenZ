using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// and the testing panel; this file adds the two moments he enters a level of the campaign (L02, L07, L16, L17). His rules, row and look stay DV161's.
    /// <list type="bullet">
    /// <item><b>Row 58, the retry help.</b> A human playing BLUE who lost and pressed REPLAY or RETRY on the lose screen starts that run with EvilZod in front of
    /// the blue fort, near its doors, on a free spot (<see cref="ZWorld.EvilGuardSpot"/>), as one of his own units: he takes the player's orders like any unit.
    /// The flag is part of the match setup: View/ZGame.cs calls <see cref="ZWorld.AddEvilRetryHelp"/> right after the world is made (as the head start), from
    /// the lose screen's one-shot flag (UI/OzUi.cs OzScenes). Not on a red player's retry, a normal start, after a win or a draw, in the Unit editor.</item>
    /// <item><b></b><see cref="ZEvilAppear.LastDefenseTicks"/>
    /// checked once a second, when blue owns no sector but its fort's own, the blue fort sends EvilZod out of its doors, once a game
    /// (<see cref="ZWorld.EvilGuardUsed"/>): made at the fort's creation point, he walks the fort's normal exit (Zod's exit move, DV66's free cell when the
    /// door is taken), then to his guard spot. The fort's door opens and closes and the roll-out sounds as for any unit it builds (UnitBuilt); the view says one
    /// of his own lines (<see cref="ZEventType.EvilGuardOut"/>). A dead EvilZod is never replaced. No spot free in that second: the next second.</item>
    /// <item><b>The guard.</b> <see cref="ZObject.EvilGuarding"/>: he holds his spot with DV68's HOLD POSITION (no chase, no auto-grab of flags, vehicles or
    /// crates, no help call), fires at whatever comes into his range (the passive engage, his own DV161 shot), may step up to <see cref="ZEvilAppear.StepPx"/> px
    /// from his spot toward an enemy just out of range when that step gives him a clear shot, and walks back to it (arriving from behind, so he faces out of the door again).
    /// Walled in short of his spot by his own team's robots (they do not step aside for a robot), he takes the place he stands on when it is outside every exit lane. No command reaches him:
    /// ZWorld.SendWaypoints refuses him (the computer's orders, a strike group's, a defend order's, the fort storm's), and the computer's planners leave him out.
    /// He never captures (his spot is <see cref="ZEvilAppear.FlagPx"/> px from every flag, his steps shorter), never boards (no order, no auto-enter, DV161
    /// lets him into nothing but his own crewed shuttle by an order).</item>
    /// <item><b>The spot.</b> In front of the fort's exit point along its door axis (<see cref="ZEvilAppear.SpotOutPx"/>) and to the side of it
    /// (<see cref="ZEvilAppear.SpotSidePx"/>, the side toward the enemy fort): the nearest free tile centre to that point. Free = robot-walkable dry tiles all round,
    /// no lava, outside every building's exit lane (+ his body), in front of the gate and within <see cref="ZEvilAppear.NearDoorPx"/> px of the exit point, the
    /// exit point's robot region, <see cref="ZEvilAppear.RoomPx"/> px clear of every object's box, 3 m from a standing shuttle and off its ramp lane, off the
    /// drawn props the rules know (ParkingKeepOut). Computed when he is made, from the objects of that tick. Vehicles and robots still leave and enter the fort.</item>
    /// </list>
    /// Determinism: sim ticks only (TickIndex, the zones' owners of the rules), integer px, object order, no random draw; the retry flag comes in at the level
    /// start. Off: <see cref="ZWorld.EvilLastDefenseTeam"/> stays Neutral and AddEvilRetryHelp is not called (every bot match, headless check, the Unit editor,
    /// 'Zod exact'): no line of this file changes a world, bit for bit.
    /// </summary>
    public static class ZEvilAppear
    {
        /// <summary><see cref="CheckTicks"/></summary>
        public const long LastDefenseTicks = 60000;
        public const int CheckTicks = 100;
        /// <summary>The ideal spot: out from the fort's exit point along the door axis, and to the side toward the enemy fort (px). The other side costs <see cref="OtherSidePx"/> more.</summary>
        public const int SpotOutPx = 32, SpotSidePx = 72, OtherSidePx = 48;
        /// <summary>Tiles searched round each ideal point.</summary>
        public const int SearchTiles = 7;
        /// <summary>Room round the spot: object boxes RoomPx clear of his 16 px body box; flags FlagPx away (centre to centre); the exit lanes' keep-out + his body + LaneRoomPx;
        /// at most NearDoorPx from the exit point.</summary>
        public const int RoomPx = 16, FlagPx = 96, LaneRoomPx = 8, NearDoorPx = 144;
        /// <summary>From a standing shuttle's outline and its ramp lane (m), as every robot by a parked ship (ZWorld.ShuttleStartRoom).</summary>
        public const double ShuttleClearM = 3.0, ShuttleLaneClearM = 0.5;
        /// <summary>The guard: he thinks every ThinkTicks (the passive engage's 0.5 s); steps at most StepPx from his spot toward an enemy just out of range (only where that
        /// step gives him a clear shot), StepInPx into his range; after a step of his own he walks all the way home (ArrivePx), pushed off it he walks home when farther than
        /// HomePx; the walk home goes through the point ApproachPx behind the spot (he arrives facing out of the door); a new walk at most every RetryTicks; a goal within
        /// SameGoalPx is the same goal.</summary>
        public const int ThinkTicks = 50, StepPx = 32, StepInPx = 6, HomePx = 24, ArrivePx = 4, ApproachPx = 16, RetryTicks = 200, SameGoalPx = 12, OnApproachPx = 10;
        /// <summary>His own team's robots wall him in short of his spot (they do not step aside for a robot): after SettleTries walks home that got him nowhere he takes the place
        /// he stands on as his spot, when it is a dry walkable tile outside every exit lane (inside a lane he keeps trying: the doors stay free).</summary>
        public const int SettleTries = 3;
        /// <summary>His own walks: a helper move (refId under -1, never player-given), as the pathing layer's step aside / regroup / bay moves.</summary>
        public const int RefGuard = -11;
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV165 row 59: the team whose fort sends EvilZod out as its last defense (Neutral = never). View/ZGame.cs sets it at every start: Blue when a human
        /// plays red against the blue computer on a campaign level with DV161 on; Neutral in every other world (bot matches, headless checks, the Unit editor).</summary>
        public Team EvilLastDefenseTeam = Team.Neutral;
        /// <summary>DV165: he came out (once a game, a dead one is never replaced), the tick, his id (-1 = not yet).</summary>
        public bool EvilGuardUsed { get; private set; }
        public long EvilGuardOutTick { get; private set; } = -1;
        public int EvilGuardId { get; private set; } = -1;
        /// <summary>DV165 row 58: the retry help's id in this world (-1 = none).</summary>
        public int EvilRetryId { get; private set; } = -1;
        /// <summary>DV165 statistics (checks): seconds the last defense was due but found no free spot.</summary>
        public int EvilGuardSpotWaits { get; private set; }

        /// <summary>The team's fort (the front fort first, as the head start), standing.</summary>
        public ZObject EvilFort(Team team)
        {
            ZObject fort = null;
            foreach (var o in Objects)
                if (o.Kind == ZKind.Building && o.IsFort && o.Owner == team && !o.Destroyed && (fort == null || o.Type == "fort_front")) fort = o;
            return fort;
        }

        /// <summary>Row 59's condition: the team has a standing fort and owns no sector but the fort's own.</summary>
        public bool EvilLastDefenseDue(Team team, out ZObject fort)
        {
            fort = team == Team.Neutral ? null : EvilFort(team);
            if (fort == null) return false;
            var home = ZoneOf(fort);
            foreach (var z in Zones) if (z.Owner == team && z != home) return false;
            return true;
        }

        /// <summary></summary>
        void EvilLastDefenseTick()
        {
            if (EvilLastDefenseTeam == Team.Neutral || EvilGuardUsed || GameOver || !EvilZodDeviation) return;
            if (TickIndex < ZEvilAppear.LastDefenseTicks || TickIndex % ZEvilAppear.CheckTicks != 0) return;
            if (!EvilLastDefenseDue(EvilLastDefenseTeam, out var fort)) return;
            if (EvilGuardComesOut(fort) == null) EvilGuardSpotWaits++;
        }

        /// <summary>BuildingCreateUnit's robot branch for him (ZWorld.Rules.cs; ZWorld.L17Crews.cs CrewOut): out of the fort's door to its exit point, then to his spot;
        /// Created + UnitBuilt as for any unit the fort sends out (the door, the roll-out), and EvilGuardOut (his line). No build list, no unit cap, no computer's note.</summary>
        ZObject EvilGuardComesOut(ZObject fort)
        {
            if (!fort.GetCreationPoint(out int cx, out int cy) || !fort.GetCreationMovePoint(out int ex, out int ey)) return null;
            if (!EvilGuardSpot(fort, null, out int gx, out int gy)) return null;
            EvilDoorAxis(fort, out double ux, out double uy, out _);
            var u = CreateRobotGroup(ZEvilZod.Type, cx, cy, fort.Owner, 1);
            u.SetCords(cx - (u.WidthPix >> 1), cy - (u.HeightPix >> 1));
            AddExitWaypoints(fort, u, cx, cy, ex, ey);                                               // Zod's exit move; DV66: a taken exit -> the nearest free exit cell (ZWorld.ExitLanes.cs)
            u.EvilGuardBegin(gx, gy, (int)Math.Round(gx - ux * ZEvilAppear.ApproachPx), (int)Math.Round(gy - uy * ZEvilAppear.ApproachPx));
            EvilGuardUsed = true; EvilGuardOutTick = TickIndex; EvilGuardId = u.Id;
            Emit(ZEventType.Created, u, fort);
            Emit(ZEventType.UnitBuilt, u, fort, 0, 0, 0f);
            Emit(ZEventType.EvilGuardOut, u, fort, gx, gy);
            return u;
        }

        /// <summary>Row 58, View/ZGame.cs right after the world is made (as the head start): EvilZod on the team's spot in front of its fort, the player's own unit.
        /// Null = DV161 off, no fort, no free spot.</summary>
        public ZObject AddEvilRetryHelp(Team team)
        {
            if (!EvilZodDeviation || team == Team.Neutral || EvilRetryId >= 0) return null;
            var fort = EvilFort(team);
            if (fort == null || !EvilGuardSpot(fort, null, out int gx, out int gy)) return null;
            var u = CreateRobotGroup(ZEvilZod.Type, gx - 8, gy - 8, team, 1);
            EvilRetryId = u.Id;
            return u;
        }

        /// <summary>The fort's door axis: out of the door (creation point -> exit point), and the side toward the nearest fort of another team (+1 = the right of
        /// the way out on the map, whose y grows downward: west of a door that opens south; -1 = its left; +1 when no other fort or straight ahead).</summary>
        public void EvilDoorAxis(ZObject fort, out double ux, out double uy, out int side)
        {
            ux = 0; uy = 1; side = 1;
            if (fort == null || !fort.GetCreationPoint(out int cx, out int cy) || !fort.GetCreationMovePoint(out int ex, out int ey)) return;
            double dx = ex - cx, dy = ey - cy, l = Math.Sqrt(dx * dx + dy * dy);
            if (l < 1e-6) return;
            ux = dx / l; uy = dy / l;
            ZObject foe = null; long best = long.MaxValue;
            foreach (var o in Objects)
            {
                if (o.Kind != ZKind.Building || !o.IsFort || o.Destroyed || o.Owner == fort.Owner || o.Owner == Team.Neutral) continue;
                long fx = o.CenterX - ex, fy = o.CenterY - ey, d2 = fx * fx + fy * fy;
                if (d2 < best) { best = d2; foe = o; }
            }
            if (foe == null) return;
            double s = (foe.CenterX - ex) * -uy + (foe.CenterY - ey) * ux;                       // toward the right of the way out (map y grows downward)
            side = s < 0 ? -1 : 1;
        }

        /// <summary>DV165: the guard spot of a fort (centre px): the nearest free tile centre to the ideal point on the side toward the enemy, the other side's
        /// ideal point at <see cref="ZEvilAppear.OtherSidePx"/> more. self = the robot that will stand there (left out of the room check). False = none free.</summary>
        public bool EvilGuardSpot(ZObject fort, ZObject self, out int sx, out int sy)
        {
            sx = sy = 0;
            if (fort == null || !fort.GetCreationPoint(out _, out _) || !fort.GetCreationMovePoint(out int ex, out int ey)) return false;
            EvilDoorAxis(fort, out double ux, out double uy, out int side);
            double nx = -uy, ny = ux;
            double ix = ex + ux * ZEvilAppear.SpotOutPx + nx * side * ZEvilAppear.SpotSidePx, iy = ey + uy * ZEvilAppear.SpotOutPx + ny * side * ZEvilAppear.SpotSidePx;
            double ox = ex + ux * ZEvilAppear.SpotOutPx - nx * side * ZEvilAppear.SpotSidePx, oy = ey + uy * ZEvilAppear.SpotOutPx - ny * side * ZEvilAppear.SpotSidePx;
            double best = double.MaxValue;
            foreach (var (px, py) in new[] { (ix, iy), (ox, oy) })
            {
                int ctx = (int)Math.Floor(px / 16), cty = (int)Math.Floor(py / 16);
                for (int ty = cty - ZEvilAppear.SearchTiles; ty <= cty + ZEvilAppear.SearchTiles; ty++)
                    for (int tx = ctx - ZEvilAppear.SearchTiles; tx <= ctx + ZEvilAppear.SearchTiles; tx++)
                    {
                        int x = tx * 16 + 8, y = ty * 16 + 8;
                        double score = Math.Min(EvD(x, y, ix, iy), EvD(x, y, ox, oy) + ZEvilAppear.OtherSidePx) + (ty * (double)Map.width + tx) * 1e-7;
                        if (score >= best || !EvilSpotFree(self, x, y, ex, ey, ux, uy)) continue;
                        best = score; sx = x; sy = y;
                    }
            }
            return best < double.MaxValue;
        }

        static double EvD(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

        static bool EvDry(ZWorld w, int tx, int ty)
        {
            if (!w.Path.TilePassable(tx, ty, true) || w.Map.LavaAt(tx, ty)) return false;
            var c = w.Map.ClassAt(tx, ty);
            return c != ZClass.Water && c != ZClass.DeepWater;
        }

        /// <summary>One candidate spot (see <see cref="EvilGuardSpot"/>): the rules of the class summary, in that order.</summary>
        bool EvilSpotFree(ZObject self, int x, int y, int ex, int ey, double ux, double uy)
        {
            int tx = x >> 4, ty = y >> 4;
            if (tx < 2 || ty < 2 || tx > Map.width - 3 || ty > Map.height - 3) return false;
            for (int j = ty - 1; j <= ty + 1; j++) for (int i = tx - 1; i <= tx + 1; i++) if (!EvDry(this, i, j)) return false;
            if ((x - ex) * ux + (y - ey) * uy < 0 || EvD(x, y, ex, ey) > ZEvilAppear.NearDoorPx) return false;   // in front of the gate, near the doors
            if (!Path.InSameRegion(ex, ey, x, y, true)) return false;
            if (InExitLane(x, y, ZEvilZod.BodyRadius + ZEvilAppear.LaneRoomPx)) return false;          // vehicles and robots still leave and enter every door
            foreach (var f in Flags) if (EvD(x, y, f.CenterX, f.CenterY) < ZEvilAppear.FlagPx) return false;
            int x0 = x - 8 - ZEvilAppear.RoomPx, y0 = y - 8 - ZEvilAppear.RoomPx, x1 = x + 8 + ZEvilAppear.RoomPx, y1 = y + 8 + ZEvilAppear.RoomPx;
            foreach (var o in Objects)
            {
                if (o == self || o.KillAt >= 0 || (o.Destroyed && o.Kind != ZKind.Building) || o.Type == "flag") continue;
                if (o.LocX < x1 && x0 < o.LocX + o.WidthPix && o.LocY < y1 && y0 < o.LocY + o.HeightPix) return false;
                if (o.IsShuttle && !o.ShuttleAirborne)
                {
                    ShuttleBoxGaps(o, x - 8, y - 8, x + 8, y + 8, out double g, out double lg);
                    if (g < ZEvilAppear.ShuttleClearM || lg < ZEvilAppear.ShuttleLaneClearM) return false;
                }
            }
            if (ParkingKeepOut.Count > 0 && ParkingKeepOutMap == Map.file)
                foreach (var k in ParkingKeepOut) if (EvD(x, y, k.x, k.y) < k.r + 12) return false;
            return true;
        }

        /// <summary>A step of the guard toward an enemy may end here: dry walkable tile, no lava, outside every exit lane, his region.</summary>
        internal bool EvilGuardPointOk(int x, int y, ZObject g)
        {
            int tx = x >> 4, ty = y >> 4;
            if (tx < 1 || ty < 1 || tx > Map.width - 2 || ty > Map.height - 2 || !EvDry(this, tx, ty)) return false;
            if (InExitLane(x, y, ZEvilZod.BodyRadius)) return false;
            return Path.InSameRegion(g.CenterX, g.CenterY, x, y, true);
        }
    }

    public partial class ZObject
    {
        /// <summary>DV165 row 59: EvilZod as his fort's last defense (set once, when the fort sends him out; never on anyone else).</summary>
        public bool EvilGuarding { get; private set; }
        /// <summary>His spot (centre px) and the point behind it he walks home through.</summary>
        public int EvilGuardX { get; private set; }
        public int EvilGuardY { get; private set; }
        public int EvilGuardBackX { get; private set; }
        public int EvilGuardBackY { get; private set; }
        /// <summary>Still on his walk out of the door to the spot.</summary>
        public bool EvilGuardWalkingOut { get; private set; }
        /// <summary>Checks: steps toward an enemy, walks home.</summary>
        public int EvilGuardSteps { get; private set; }
        public int EvilGuardHomeWalks { get; private set; }
        /// <summary>Checks: times he took the place he stood on as his spot (walled in by his own team).</summary>
        public int EvilGuardSettles { get; private set; }
        long evilGuardNext, evilGuardRetry; bool evilGuardAway; int evilGuardTries, evilGuardFromX, evilGuardFromY;

        internal void EvilGuardBegin(int gx, int gy, int bx, int by)
        {
            EvilGuarding = true; EvilGuardWalkingOut = true;
            EvilGuardX = gx; EvilGuardY = gy; EvilGuardBackX = bx; EvilGuardBackY = by;
            HoldPosition = true;
            Waypoints.Add(ZWaypoint.Move(bx, by, false, ZEvilAppear.RefGuard));
            Waypoints.Add(ZWaypoint.Move(gx, gy, false, ZEvilAppear.RefGuard));
        }

        void EvilGuardDrop(int i)
        {
            Waypoints.RemoveAt(i); UpdatedWaypoints = true;
            if (i == 0) { StopMove(); lastWp = ZWaypoint.Cleared; }
        }

        /// <summary>Row 82 (3 Nov): a traffic helper's move (step aside, back off, regroup, bay) that would end farther from his spot than StepPx and farther than he
        /// stands now: he does not take it, so the gate's traffic never pushes him off his ground while he fights in range (the guard's walk home waits for the fight).
        /// Shorter steps aside, and any that bring him nearer his spot, stay. Not while he walks out of the fort.</summary>
        bool EvilGuardFar(ZWaypoint w) => !EvilGuardWalkingOut && ZPathing.BridgeHelperRef(w.refId)
            && Dist(w.x, w.y, EvilGuardX, EvilGuardY) > Math.Max(ZEvilAppear.StepPx, Dist(CenterX, CenterY, EvilGuardX, EvilGuardY));

        /// <summary>ProcessServer, before the waypoint step: the guard's hold, his steps and his walk home.</summary>
        void EvilGuardStep()
        {
            HoldPosition = true;                                                                   // DV68: no chase, no auto-grab of flags / vehicles / crates, no help call
            for (int i = Waypoints.Count - 1; i >= 0; i--)                                         // walks only: no order of any other kind ever reaches him
                if ((Waypoints[i].mode != WpMode.Move && Waypoints[i].mode != WpMode.ForceMove) || EvilGuardFar(Waypoints[i])) EvilGuardDrop(i);   // row 82: no push off his ground
            long now = World.TickIndex;
            if (now < evilGuardNext) return;
            evilGuardNext = now + ZEvilAppear.ThinkTicks;
            if (EvilGuardWalkingOut) { if (Waypoints.Count > 0 && !EvilGuardFightOnTheWay()) return; EvilGuardWalkingOut = false; }   // 1.3 final: an enemy in range on his way out ends the walk (the code below stops him; home after the fight)
            if (AttackObject != null && CanAttackObject(AttackObject) && WithinAttackRadius(AttackObject)) return;   // a fight in range: he stands and fires
            ZObject near = null; long best = long.MaxValue, reach = AttackRadius + ZEvilAppear.StepPx, look = 2L * AttackRadius; int sx = 0, sy = 0;
            foreach (var o in World.UnitObjects)
            {
                if (o.Owner == Team.Neutral || o.Owner == Owner || o.Destroyed || o.KillAt >= 0 || o.ShuttleAirborne || !CanAttackObject(o)) continue;
                long mx = o.CenterX - CenterX, my = o.CenterY - CenterY;
                if (mx * mx + my * my <= look * look && WithinAttackRadius(o))
                {   // an enemy in his range from where he stands: he stops and the passive engage takes it
                    while (Waypoints.Count > 0) EvilGuardDrop(Waypoints.Count - 1);
                    return;
                }
                long dx = o.CenterX - EvilGuardX, dy = o.CenterY - EvilGuardY, d2 = dx * dx + dy * dy;
                if (d2 > reach * reach || d2 >= best) continue;
                double d = Math.Sqrt(d2), s = Math.Min(ZEvilAppear.StepPx, d - AttackRadius + ZEvilAppear.StepInPx);
                if (s <= 0) continue;                                                              // in his range from the spot itself: the walk home serves
                int px = EvilGuardX + (int)Math.Round(dx * s / d), py = EvilGuardY + (int)Math.Round(dy * s / d);
                if (!World.EvilGuardPointOk(px, py, this) || World.EngageBarrierBetween(px, py, o.CenterX, o.CenterY) || (LosActive && !World.Sight.ClearFrom(px, py, 0, o))) continue;   // a step that gives no shot is no step
                best = d2; near = o; sx = px; sy = py;
            }
            bool step = near != null;
            int wx = step ? sx : EvilGuardX, wy = step ? sy : EvilGuardY;
            if (!step && Waypoints.Count == 0 && Dist(CenterX, CenterY, wx, wy) <= ZEvilAppear.ArrivePx) evilGuardAway = false;   // home again
            if (Dist(CenterX, CenterY, wx, wy) <= (step || evilGuardAway ? ZEvilAppear.ArrivePx : ZEvilAppear.HomePx)) { evilGuardTries = 0; return; }   // there; or, pushed a little, near enough to his spot
            if (Waypoints.Count > 0)
            {
                var g = Waypoints[Waypoints.Count - 1];
                if (g.refId == ZEvilAppear.RefGuard && Dist(g.x, g.y, wx, wy) <= ZEvilAppear.SameGoalPx) return;   // on his way there already
            }
            if (now < evilGuardRetry) return;
            evilGuardRetry = now + ZEvilAppear.RetryTicks;
            if (!step && Waypoints.Count == 0)
            {   // the last walk home ended where it began: walled in by his own team
                evilGuardTries = evilGuardTries > 0 && Dist(CenterX, CenterY, evilGuardFromX, evilGuardFromY) < ZEvilAppear.SameGoalPx ? evilGuardTries + 1 : 1;
                evilGuardFromX = CenterX; evilGuardFromY = CenterY;
                if (evilGuardTries > ZEvilAppear.SettleTries && World.EvilGuardPointOk(CenterX, CenterY, this))
                {
                    int ox = EvilGuardX - EvilGuardBackX, oy = EvilGuardY - EvilGuardBackY;
                    EvilGuardX = CenterX; EvilGuardY = CenterY; EvilGuardBackX = CenterX - ox; EvilGuardBackY = CenterY - oy;
                    evilGuardTries = 0; evilGuardAway = false; EvilGuardSettles++;
                    return;
                }
            }
            while (Waypoints.Count > 0) EvilGuardDrop(Waypoints.Count - 1);
            if (!step && !EvilGuardOnApproach()) Waypoints.Add(ZWaypoint.Move(EvilGuardBackX, EvilGuardBackY, false, ZEvilAppear.RefGuard));
            Waypoints.Add(ZWaypoint.Move(wx, wy, false, ZEvilAppear.RefGuard));
            if (step) { EvilGuardSteps++; evilGuardAway = true; } else EvilGuardHomeWalks++;
            UpdatedWaypoints = true;
            World.Pathing?.OnOrder(this);
            CloneMinionWayPoints();
        }

        /// <summary>Checks: walks out of the fort that an enemy in his range cut short (1.3 final).</summary>
        public int EvilGuardFights { get; private set; }

        /// <summary>
        /// On his walk out, once the door's exit move is
        /// behind him (only his own guard walks left) and he stands outside every exit lane (the doors stay free): an enemy he may attack, inside his range from
        /// where he stands, with no barrier and (line of sight on) a clear line, ends the walk out. The guard's own rule then stops him and the passive engage
        /// fires; when nothing is left in range he walks to his spot as before. Integer px, object order, no random draw.</summary>
        bool EvilGuardFightOnTheWay()
        {
            foreach (var w in Waypoints) if (w.refId != ZEvilAppear.RefGuard) return false;       // still on Zod's exit move out of the door (or a traffic step aside)
            if (World.InExitLane(CenterX, CenterY, ZEvilZod.BodyRadius)) return false;            // never stops in a door's lane
            foreach (var o in World.UnitObjects)
            {
                if (o.Owner == Team.Neutral || o.Owner == Owner || o.Destroyed || o.KillAt >= 0 || o.ShuttleAirborne || !CanAttackObject(o) || !WithinAttackRadius(o)) continue;
                if (World.EngageBarrierBetween(CenterX, CenterY, o.CenterX, o.CenterY) || (LosActive && !World.Sight.ClearFrom(CenterX, CenterY, 0, o))) continue;
                EvilGuardFights++;
                return true;
            }
            return false;
        }

        /// <summary>He stands on the short line from the point behind his spot to the spot: the last leg home already points out of the door.</summary>
        bool EvilGuardOnApproach()
        {
            if ((long)(CenterX - EvilGuardX) * (EvilGuardX - EvilGuardBackX) + (long)(CenterY - EvilGuardY) * (EvilGuardY - EvilGuardBackY) > 0) return false;   // 1.4 DV165 G5: in front of his spot (the outward side along the door axis): the back point first, so the last step points out
            double ax = EvilGuardX - EvilGuardBackX, ay = EvilGuardY - EvilGuardBackY, l2 = ax * ax + ay * ay;
            double t = l2 <= 0 ? 0 : Math.Max(0, Math.Min(1, ((CenterX - EvilGuardBackX) * ax + (CenterY - EvilGuardBackY) * ay) / l2));
            double qx = EvilGuardBackX + ax * t - CenterX, qy = EvilGuardBackY + ay * t - CenterY;
            return qx * qx + qy * qy <= ZEvilAppear.OnApproachPx * ZEvilAppear.OnApproachPx;
        }
    }
}
