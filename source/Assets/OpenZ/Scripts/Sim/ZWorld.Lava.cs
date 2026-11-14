using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// DV152, version 1.3, level 7 "Pyro Technics": robots can walk into lava, but only when the player sends them, and it melts them.
    ///   Zod        a lava tile is an Impassable tile like a cliff (volcanic.tileinfo); nobody ever enters it, it does no harm (the Z wiki: lava
    ///              is "not actually hazardous to your units, but impassable"). That stays true for vehicles, for the computer player and
    ///              for the path finder: the map keeps every lava tile as class Impassable (Resources/OpenZ/Maps/L07_grid.json, "lava").
    ///   in         a click on a lava tile means walk in. A robot squad of a HUMAN team ordered (MOVE, also a Shift-queued one) onto a lava tile walks to the shore
    ///              spot of its own ground with the least way: its walk on dry ground (a walking map of the robot grid) + the wade to that tile (LavaShore), a
    ///              normal MOVE with no fight or grab on the way, then straight in (Zod's FORCE_MOVE, the move factories use for their exits): the first leader to the
    ///              clicked tile's centre, every other robot to its own lava tile round it (LavaOwnSpot). A squad member finishes its walk in after its
    ///              leader's has ended, round a wall if it must (LavaFollowIn). The walk never enters a rock, building or cliff tile (ZObject.ProcessMove: it ends
    ///              where the next step would). No dry spot within LavaSearchTiles tiles with a clean line (none on level 7): the order stays as it was (Zod:
    ///              the nearest reachable spot).
    ///   out        a robot in lava with an order of the player walks straight out at the shore spot with the least way to the order's goal: the wade + the walk on
    ///              dry ground, so it goes forward over the lava when that is shorter, back only when forward is blocked or it would melt on the way (LavaWadePx).
    ///              A squad leaves by its leader's spot; a member still on its way in comes in after its leader and over with it. Then it carries on with the order.
    ///              Orders of its own (agro, help calls, grabs) are dropped while it is in lava, and Zod's scuffle step stays inside it. A new order
    ///              always ends a lava walk (Zod would keep a FORCE_MOVE: CanOverwriteWP).
    ///   forgotten  a robot of a human team in lava with no order, no target and no hold position for LavaIdleSeconds walks to the closest dry shore by itself
    ///              and stops there (LavaIdle). A new order or a target starts its time again; a robot with a target or on hold stays, as ordered.
    ///   burn       a robot whose centre is on a lava tile loses LavaBiteHp() = MaxHealth / LavaMeltBites (rounded up) after each whole
    ///              second in it (the first one second after it stepped in): a full-health robot melts at the 12th bite, 12 s in, of
    ///              every type. The bite is a fire hit (LastFireHit: the death is a melt, the cry the fire cry); no killer, no kill
    ///              credit. Heat (the view's glow) = lava HP lost / (that + HP left): 0 at the first step, 1 at the melt. Out of the lava
    ///              the burning stops at once and the heat falls to 0 over LavaCoolSeconds; after that a new dip starts from cold.
    ///   speed      in lava a robot walks at LavaWalkSpeed (Zod's water speed, 0.7: wading).
    ///   events     LavaStep (radius 1 in, 0 out, 2 critical at heat LavaCriticalHeat), the usual Death event (flag = fire).
    /// Deterministic: whole pixels and HP, the sim clock, list order, fixed scan orders; no random draw (Zod's rand() is untouched).
    /// Switch: ZWorld.LavaDeviation (the game sets it at every start from OPTIONS "LAVA BURNS ROBOTS", default on; off in 'Zod exact';
    /// headless default off, so every parity row and counted check runs without it). Off = Zod exactly, tick for tick: nobody enters lava.
    /// Maps without lava: nothing runs (Map.HasLava).
    /// </summary>
    public sealed partial class ZWorld
    {
        public static bool LavaDeviation = false;
        /// <summary>Bites to melt a full-health robot (one per whole second inside), seconds between bites, walk speed factor in lava,
        /// seconds for the glow to cool, the heat of the "critical" warning, how far (tiles) a dry spot is searched.</summary>
        public const int LavaMeltBites = 12;
        public const double LavaBiteEvery = 1.0, LavaWalkSpeed = ZConst.WATER_SPEED, LavaCoolSeconds = 4.0, LavaCriticalHeat = 0.75;
        public const int LavaSearchTiles = 8;
        /// <summary>Waypoint marks (refId) of the lava walks: the MOVE to the edge, the walk in, the walk out (never a real object id).</summary>
        public const int RefLavaEdge = -6, RefLavaIn = -7, RefLavaOut = -8;
        /// <summary>Counters (checks): lava orders made into lava walks, orders left as they were (no dry spot), robots that stepped in,
        /// walks out started, walks ended at a wall, HP lava took, robots it melted.</summary>
        public int LavaOrders, LavaOrdersKept, LavaSteps, LavaWalkOuts, LavaWalkHalts, LavaDealt, LavaMelts, LavaFollowIns;
        /// <summary>Counters: lava tiles the player sent a robot to (a new click; the view's line listens to it), walks out a robot started by itself.</summary>
        public int LavaCalls, LavaIdleWalks, LavaInStalls;
        /// <summary>Seconds a robot of a human team stands in lava with no order and no target before it walks to the shore by itself (a static: the counted check of the melt sets it out of reach).</summary>
        public static double LavaIdleSeconds = 5.0;
        /// <summary>Seconds a robot on a walk in keeps trying before it gives up (a hut corner, the way full).</summary>
        public const double LavaInSeconds = 20.0;
        /// <summary>Seconds a robot already in the lava on its walk in may be held by its squad without getting closer to its own tile before its walk is done.</summary>
        public const double LavaInStall = 1.0;
        double lavaClickAt = -1; int lavaClickTile = -1, lavaClickUsed;

        public static bool IsLavaWalk(ZWaypoint w) => w.mode == WpMode.ForceMove && (w.refId == RefLavaIn || w.refId == RefLavaOut);

        /// <summary>The bite of one whole second in lava for this robot (HP).</summary>
        public static int LavaBiteHp(ZObject o) => Math.Max(1, (o.MaxHealth + LavaMeltBites - 1) / LavaMeltBites);

        /// <summary>The robot's centre is on a lava tile.</summary>
        public bool InLavaNow(ZObject o) => Map.LavaAt(o.CenterX >> 4, o.CenterY >> 4);
        public bool LavaAtPx(int x, int y) => x >= 0 && y >= 0 && Map.LavaAt(x >> 4, y >> 4);
        /// <summary>Any tile under the unit's box (shrunk by 1 px like WithinImpassable) is lava.</summary>
        public bool LavaUnderBox(ZObject o)
        {
            int x = o.LocX + 1, y = o.LocY + 1, x1 = o.LocX + o.WidthPix - 2, y1 = o.LocY + o.HeightPix - 2;
            for (int j = y >> 4; j <= y1 >> 4; j++) for (int i = x >> 4; i <= x1 >> 4; i++) if (Map.LavaAt(i, j)) return true;
            return false;
        }

        /// <summary>Tiles under a robot box at (locX, locY) (shrunk by 1 px like WithinImpassable) that are blocked for robots and are
        /// not lava: rocks, buildings, cliffs; 1000 off the map.</summary>
        public int LavaHardTiles(ZObject o, int locX, int locY)
        {
            int x = locX + 1, y = locY + 1, w = o.WidthPix - 2, h = o.HeightPix - 2;
            if (x < 0 || y < 0 || x + w >= Path.WidthPix || y + h >= Path.HeightPix) return 1000;
            int n = 0;
            for (int j = y >> 4; j <= (y + h - 1) >> 4; j++)
                for (int i = x >> 4; i <= (x + w - 1) >> 4; i++)
                    if (!Path.TilePassable(i, j, true) && !Map.LavaAt(i, j)) n++;
            return n;
        }

        /// <summary>A robot (16 x 16 px) can walk the straight line between two centres over walkable tiles and lava only.</summary>
        public bool LavaLineClean(int x0, int y0, int x1, int y1)
        {
            double dx = x1 - x0, dy = y1 - y0, len = Math.Sqrt(dx * dx + dy * dy);
            int steps = (int)(len / 2) + 1;
            for (int s = 0; s <= steps; s++)
            {
                double f = s >= steps ? 1.0 : s * 2 / Math.Max(len, 1e-9);
                int cx = (int)Math.Floor(x0 + dx * f), cy = (int)Math.Floor(y0 + dy * f);
                int bx = cx - 7, by = cy - 7, bx1 = cx + 6, by1 = cy + 6;
                if (bx < 0 || by < 0 || bx1 >= Path.WidthPix || by1 >= Path.HeightPix) return false;
                for (int j = by >> 4; j <= by1 >> 4; j++)
                    for (int i = bx >> 4; i <= bx1 >> 4; i++)
                        if (!Path.TilePassable(i, j, true) && !Map.LavaAt(i, j)) return false;
            }
            return true;
        }

        /// <summary>Path component (robot grid, rocks solid) of a point, -1 when the pathing layer is off or the point is off the graph.</summary>
        int LavaComp(int x, int y) => Pathing != null && Pathing.Enabled && x >= 0 && y >= 0 ? Pathing.Reach.Comp(x >> 4, y >> 4, true, false) : -1;

        int[] lavaMap, lavaQ; bool[] lavaQd; int lavaMapTile = -1, lavaMapVersion = int.MinValue;
        /// <summary>Walking px from every tile to the tile of (x, y): 16 a step, 23 a diagonal, the robot grid, rocks solid. null: no pathing layer.</summary>
        int[] LavaWalkMap(int x, int y, int comp)
        {
            if (Pathing == null || !Pathing.Enabled) return null;
            var reach = Pathing.Reach; int w = Path.W, h = Path.H, n = w * h;
            if (comp >= 0 && reach.Comp(x >> 4, y >> 4, true, false) != comp && !reach.Nearest(x, y, true, false, comp, out x, out y)) return null;
            int key = (y >> 4) * w + (x >> 4);
            if (lavaMap != null && key == lavaMapTile && Path.Version == lavaMapVersion) return lavaMap;
            if (lavaMap == null || lavaMap.Length != n) { lavaMap = new int[n]; lavaQ = new int[n]; lavaQd = new bool[n]; }
            for (int i = 0; i < n; i++) lavaMap[i] = int.MaxValue;
            int head = 0, tail = 0, left = 1; lavaMap[key] = 0; lavaQ[tail++] = key; lavaQd[key] = true;
            while (left > 0)
            {
                int c = lavaQ[head]; head = (head + 1) % n; left--; lavaQd[c] = false;
                int cx = c % w, cy = c / w, dc = lavaMap[c];
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h || !Path.StepOk(nx, ny, cx, cy, true, false)) continue;
                        int ni = ny * w + nx, nd = dc + (dx != 0 && dy != 0 ? 23 : 16);
                        if (nd >= lavaMap[ni]) continue;
                        lavaMap[ni] = nd;
                        if (!lavaQd[ni]) { lavaQd[ni] = true; lavaQ[tail] = ni; tail = (tail + 1) % n; left++; }
                    }
            }
            lavaMapTile = key; lavaMapVersion = Path.Version;
            return lavaMap;
        }

        /// <summary>The dry spot (a tile centre: walkable, no lava, in component comp unless -1) within LavaSearchTiles tiles of the lava point
        /// (cx, cy) with a clean straight line to it, on the way that costs least: the wade (px / 0.7) + the walk on dry ground to (gx, gy),
        /// from the walking map when there is one, else straight. A spot farther than maxWade is left out. Ring scan, rows then columns: the same answer on every machine.</summary>
        bool LavaShore(int cx, int cy, int comp, int[] map, int gx, int gy, long maxWade, out int sx, out int sy)
        {
            sx = cx; sy = cy; long best = long.MaxValue; int tx0 = cx >> 4, ty0 = cy >> 4;
            for (int r = 0; r <= LavaSearchTiles; r++)
                for (int ty = ty0 - r; ty <= ty0 + r; ty++)
                    for (int tx = tx0 - r; tx <= tx0 + r; tx++)
                    {
                        if (Math.Max(Math.Abs(tx - tx0), Math.Abs(ty - ty0)) != r) continue;
                        if (!Path.TilePassable(tx, ty, true) || Map.LavaAt(tx, ty)) continue;
                        int px = tx * 16 + 8, py = ty * 16 + 8;
                        if (comp >= 0 && LavaComp(px, py) != comp) continue;
                        long ax = px - cx, ay = py - cy, bx = gx - px, by = gy - py;
                        long walk = map != null ? map[ty * Path.W + tx] : (long)Math.Sqrt(bx * bx + by * by);
                        if (walk == int.MaxValue) continue;
                        long wade = (long)Math.Sqrt(ax * ax + ay * ay), d = wade * 10 / 7 + walk;
                        if (wade > maxWade) continue;
                        if (d >= best || !LavaLineClean(px, py, cx, cy)) continue;
                        best = d; sx = px; sy = py;
                    }
            return best != long.MaxValue;
        }

        static long LavaPx(ZObject o, int x, int y) { long dx = o.CenterX - x, dy = o.CenterY - y; return (long)Math.Sqrt(dx * dx + dy * dy); }

        /// <summary>ProcessMoveWP, a new walk in: the lava spot of this robot: the first squad's leader the clicked tile, every other robot the next lava tile
        /// round it (two rings, a clean line from the clicked tile and from the robot), nearest the shore the squad comes from first.</summary>
        internal void LavaOwnSpot(ZObject o, ZWaypoint wp, ref int x, ref int y)
        {
            if (wp.refId != RefLavaIn || !wp.playerGiven || !LavaDeviation || wp.mode != WpMode.ForceMove || !LavaAtPx(wp.x, wp.y)) return;   // a scuffle nudge inside lava (not the player's) keeps its few px
            var l = o.Leader ?? o; int k = l.LavaSpotBase + (o.Leader != null ? l.Minions.IndexOf(o) + 1 : 0);
            if (k == 0) return;
            lavaSpots.Clear();
            int tx0 = wp.x >> 4, ty0 = wp.y >> 4;
            for (int ty = ty0 - 2; ty <= ty0 + 2; ty++)
                for (int tx = tx0 - 2; tx <= tx0 + 2; tx++)
                {
                    int px = tx * 16 + 8, py = ty * 16 + 8;
                    if ((tx == tx0 && ty == ty0) || !Map.LavaAt(tx, ty) || !LavaLineClean(wp.x, wp.y, px, py)) continue;
                    long dx = px - l.LavaFromX, dy = py - l.LavaFromY;
                    lavaSpots.Add(((dx * dx + dy * dy) << 16) + (ty - ty0 + 2) * 8 + tx - tx0 + 2);
                }
            lavaSpots.Sort();
            for (int i = k - 1; i < lavaSpots.Count; i++)
            {
                int code = (int)(lavaSpots[i] & 0xFFFF), px = (tx0 + code % 8 - 2) * 16 + 8, py = (ty0 + code / 8 - 2) * 16 + 8;
                if (LavaLineClean(o.CenterX, o.CenterY, px, py)) { x = px; y = py; return; }
            }
        }
        readonly List<long> lavaSpots = new List<long>();

        /// <summary>SendWaypoints, after Zod's validation: a lava walk kept first (Zod keeps a FORCE_MOVE) is dropped, and each MOVE of a
        /// human team's robot onto a lava tile becomes MOVE to the edge + the walk in.</summary>
        void LavaOrder(Team team, ZObject o)
        {
            if (!LavaDeviation || !Map.HasLava || o == null || !o.IsRobot) return;
            o.LavaOrderSeq++;                                                             // a new order of this leader: its minions' walk in (LavaFollowIn) belongs to the old one
            o.LavaIdleAt = Time; foreach (var m in o.Minions) if (m != null) m.LavaIdleAt = Time;
            var list = o.Waypoints;
            if (list.Count > 0 && IsLavaWalk(list[0])) list.RemoveAt(0);                  // the player changes his mind: the lava walk ends now
            if (!IsHumanTeam(team)) return;                                               // the computer never sends anybody into lava
            List<ZWaypoint> outl = null;
            for (int i = 0; i < list.Count; i++)
            {
                var w = list[i];
                if (w.mode != WpMode.Move || !LavaAtPx(w.x, w.y)) { outl?.Add(w); continue; }
                outl ??= new List<ZWaypoint>(list.GetRange(0, i));
                int lx = (w.x >> 4) * 16 + 8, ly = (w.y >> 4) * 16 + 8;                   // the clicked lava tile's centre
                bool direct = i == 0 && (InLavaNow(o) || LavaUnderBox(o)) && LavaLineClean(o.CenterX, o.CenterY, lx, ly);   // in it or at its very edge (a Shift queue re-sends a running walk in): straight on in
                if (!direct)
                {
                    int comp = Pathing != null && Pathing.Enabled ? Pathing.Reach.UnitComp(o, false) : -1;
                    int ox = i == 0 ? o.CenterX : list[i - 1].x, oy = i == 0 ? o.CenterY : list[i - 1].y;
                    if (!LavaShore(lx, ly, comp, LavaWalkMap(ox, oy, comp), ox, oy, int.MaxValue, out int ex, out int ey) && !LavaShore(lx, ly, -1, null, ox, oy, int.MaxValue, out ex, out ey))
                    { outl.Add(w); LavaOrdersKept++; continue; }                          // no way in from here: Zod's own handling of the order
                    outl.Add(new ZWaypoint { mode = WpMode.Move, refId = RefLavaEdge, x = ex, y = ey, playerGiven = true });
                }
                outl.Add(new ZWaypoint { mode = WpMode.ForceMove, refId = RefLavaIn, x = lx, y = ly, playerGiven = true });
                LavaOrders++;
                if (w.refId != RefLavaIn) LavaCalls++;
                int tile = (ly >> 4) * Path.W + (lx >> 4);
                if (lavaClickAt != Time || lavaClickTile != tile) { lavaClickAt = Time; lavaClickTile = tile; lavaClickUsed = 0; }
                o.LavaSpotBase = lavaClickUsed; lavaClickUsed += 1 + o.Minions.Count;
                o.LavaFromX = outl.Count > 1 ? outl[outl.Count - 2].x : o.CenterX; o.LavaFromY = outl.Count > 1 ? outl[outl.Count - 2].y : o.CenterY;
            }
            if (outl != null) { list.Clear(); list.AddRange(outl); }
        }

        /// <summary>Once a tick (Tick, after the burning ground): every robot on lava gets its bite per whole second, a robot in lava with an
        /// order walks out first, a robot out of it cools.</summary>
        void LavaTick()
        {
            if (!LavaDeviation || !Map.HasLava || GameOver) return;                         // (like the burning ground: nothing bites after the game is over)
            int n = Objects.Count;
            for (int i = 0; i < n && i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (!o.IsRobot || o.Destroyed || o.KillAt >= 0 || o.IsFortDefender || o.MaxHealth <= 0) continue;
                LavaFollowIn(o);
                bool inLava = InLavaNow(o);
                if (inLava)
                {
                    if (!o.InLava)
                    {
                        o.InLava = true; o.LavaNext = Time + LavaBiteEvery; LavaSteps++; o.LavaIdleAt = Time;
                        if (o.LavaHeat <= 0) { o.LavaTaken = 0; o.LavaCritical = false; }      // cold: a new dip
                        Emit(ZEventType.LavaStep, o, null, o.CenterX, o.CenterY, (float)o.LavaHeat, 1);
                    }
                    LavaKeepOrder(o);
                    LavaIdle(o);
                    if (Time + 1e-9 >= o.LavaNext)
                    {
                        o.LavaNext += LavaBiteEvery;
                        int hp0 = o.Health;
                        o.DamageHealth(LavaBiteHp(o));
                        o.LastFireHit = Time;                                                // a death in it is a fire death: the melt
                        int took = hp0 - o.Health; o.LavaTaken += took; LavaDealt += took;
                        if (o.Destroyed) { o.LavaKilled = true; LavaMelts++; }
                        o.LavaHeat = o.LavaTaken <= 0 ? 0 : o.LavaTaken / (double)(o.Health + o.LavaTaken);
                        UpdateObjectHealth(o);
                        if (o.Destroyed) continue;
                    }
                    else o.LavaHeat = o.LavaTaken <= 0 ? 0 : o.LavaTaken / (double)(o.Health + o.LavaTaken);
                    if (!o.LavaCritical && o.LavaHeat >= LavaCriticalHeat) { o.LavaCritical = true; Emit(ZEventType.LavaStep, o, null, o.CenterX, o.CenterY, (float)o.LavaHeat, 2); }
                }
                else
                {
                    if (o.InLava)
                    {
                        o.InLava = false; o.LavaLeftAt = Time; o.LavaHeatOut = o.LavaHeat;
                        Emit(ZEventType.LavaStep, o, null, o.CenterX, o.CenterY, (float)o.LavaHeat, 0);
                    }
                    else if (o.LavaHeat > 0)
                    {
                        double k = 1.0 - (Time - o.LavaLeftAt) / LavaCoolSeconds;
                        if (k <= 0) { o.LavaHeat = 0; o.LavaTaken = 0; o.LavaCritical = false; }
                        else o.LavaHeat = o.LavaHeatOut * k;
                    }
                    if (o.Waypoints.Count > 0 && o.Waypoints[0].mode == WpMode.ForceMove && o.Waypoints[0].refId == RefLavaOut && !LavaUnderBox(o)) o.LavaWalkDone();   // all of it out (or a squad copy of its leader's walk out on dry ground): done
                }
            }
        }

        /// <summary>A lava walk is finished whatever happens to the squad's order list: Zod clears a minion's orders when its leader's waypoint ends
        /// (CloneMinionWayPoints), and a wall ends a walk (LavaWalkHalt). A robot on a lava walk with no new order of its leader walks on: in, to its own
        /// lava spot, round the wall to the shore if it must; out, the same spot, in front of what it has left. A new order ends that, unless the robot
        /// is still on its way in and its leader goes over the lava: then it comes in and over with it. Every other robot: untouched.</summary>
        void LavaFollowIn(ZObject o)
        {
            var l = o.Leader ?? o;
            if (o.LavaInPending && o.Waypoints.Count > 0 && (o.Waypoints[0].refId == RefLavaEdge || o.Waypoints[0].mode == WpMode.Attack)) return;   // on its own way to the shore, or blasting a rock in the way
            if (o.Waypoints.Count > 0 && IsLavaWalk(o.Waypoints[0]))
            {
                if (o.LavaInSeq != l.LavaOrderSeq || !o.LavaInPending) { o.LavaHalted = 0; o.LavaInBy = Time + LavaInSeconds; o.LavaInBest = int.MaxValue; }
                o.LavaInPending = true; o.LavaInRef = o.Waypoints[0].refId; o.LavaInGiven = o.Waypoints[0].playerGiven; o.LavaInX = o.Waypoints[0].x; o.LavaInY = o.Waypoints[0].y; o.LavaInSeq = l.LavaOrderSeq;
                if (o.LavaInRef == RefLavaIn && InLavaNow(o))
                {   // in the lava on its walk in: held by its squad a whole second without getting a pixel closer to its own tile, it is in
                    long tx = o.CenterX - o.PathTargetX, ty = o.CenterY - o.PathTargetY; int d = (int)Math.Sqrt(tx * tx + ty * ty);
                    if (d < o.LavaInBest) { o.LavaInBest = d; o.LavaInBestAt = Time; }
                    else if (Time + 1e-9 >= o.LavaInBestAt + LavaInStall) { o.LavaInPending = false; o.LavaWalkDone(); LavaInStalls++; }
                }
                return;
            }
            if (!o.LavaInPending) return;
            if (l.LavaOrderSeq != o.LavaInSeq)
            {   // a new order: it stands, unless this robot is still on its way in and its leader goes over the lava: then it comes in after it, and over with it
                if (o.Leader == null || o.LavaInRef != RefLavaIn || InLavaNow(o) || l.Waypoints.Count == 0 || l.Waypoints[0].mode != WpMode.ForceMove || l.Waypoints[0].refId != RefLavaOut || !l.Waypoints[0].playerGiven)
                { o.LavaInPending = false; return; }
                o.LavaInSeq = l.LavaOrderSeq;
            }
            bool isIn = o.LavaInRef == RefLavaIn;
            if (isIn)
            {
                int sx = o.LavaInX, sy = o.LavaInY; LavaOwnSpot(o, new ZWaypoint { mode = WpMode.ForceMove, refId = RefLavaIn, x = sx, y = sy, playerGiven = o.LavaInGiven }, ref sx, ref sy);
                if (Time > o.LavaInBy) { o.LavaInPending = false; return; }                  // it could not get in: it stands where it is
                long ddx = o.CenterX - sx, ddy = o.CenterY - sy;
                if (ddx * ddx + ddy * ddy <= (InLavaNow(o) ? 24 * 24 : 2 * 2)) { o.LavaInPending = false; return; }
                if (o.Waypoints.Count > 0 && !o.Waypoints[0].playerGiven) o.Waypoints.Clear();   // (the layer's own helper moves: a regroup next to the leader)
                if (o.LavaHalted > 0 || !LavaLineClean(o.CenterX, o.CenterY, sx, sy))
                {   // a rock between: the way round to the shore, then in
                    int lx = o.LavaInX, ly = o.LavaInY, comp = Pathing != null && Pathing.Enabled ? Pathing.Reach.UnitComp(o, false) : -1;
                    if (!LavaShore(lx, ly, comp, LavaWalkMap(o.CenterX, o.CenterY, comp), o.CenterX, o.CenterY, int.MaxValue, out int ex, out int ey)) { o.LavaInPending = false; return; }
                    o.LavaWalkInsert(new ZWaypoint { mode = WpMode.ForceMove, refId = RefLavaIn, x = lx, y = ly, playerGiven = true });
                    o.LavaWalkInsert(new ZWaypoint { mode = WpMode.Move, refId = RefLavaEdge, x = ex, y = ey, playerGiven = true });
                    LavaFollowIns++; return;
                }
            }
            else if (!LavaUnderBox(o) || !LavaLineClean(o.CenterX, o.CenterY, o.LavaInX, o.LavaInY)) { o.LavaInPending = false; return; }
            o.LavaWalkInsert(new ZWaypoint { mode = WpMode.ForceMove, refId = o.LavaInRef, x = o.LavaInX, y = o.LavaInY, playerGiven = o.LavaInGiven });
            LavaFollowIns++;
        }

        /// <summary>A robot in lava with an order: the walk in / out goes on; a MOVE to the edge before a walk in is skipped when the walk
        /// in is a clean line from here; any other order: first the walk out, by its leader's spot when it can reach that, else at the shore spot with the
        /// least way to the goal that it reaches alive, else at the closest one.</summary>
        void LavaKeepOrder(ZObject o)
        {
            if (o.Waypoints.Count == 0) return;                                              // no order: it stays (and burns)
            var w = o.Waypoints[0];
            if (w.mode == WpMode.ForceMove && w.refId == RefLavaIn) return;
            if (w.mode == WpMode.ForceMove && w.refId == RefLavaOut && LavaLineClean(o.CenterX, o.CenterY, w.x, w.y)) return;   // its own way out (a squad copy of the leader's way may not be)
            if (w.mode == WpMode.Move && w.refId == RefLavaEdge && o.Waypoints.Count > 1 && o.Waypoints[1].mode == WpMode.ForceMove && o.Waypoints[1].refId == RefLavaIn
                && LavaLineClean(o.CenterX, o.CenterY, o.Waypoints[1].x, o.Waypoints[1].y)) { o.LavaWalkDone(); return; }   // already in: straight on to the new lava spot
            if (!w.playerGiven)
            {   // nothing but the player takes a robot out of lava: Zod's own small scuffle step stays inside it, any other order of its own is dropped
                if (w.mode == WpMode.Move && LavaAtPx(w.x, w.y) && LavaLineClean(o.CenterX, o.CenterY, w.x, w.y)) o.LavaWalkReplace(new ZWaypoint { mode = WpMode.ForceMove, refId = RefLavaIn, x = w.x, y = w.y });
                else o.LavaWalkDone();
                return;
            }
            int gx = w.x, gy = w.y;
            if (w.mode == WpMode.ForceMove && w.refId == RefLavaOut && o.Waypoints.Count > 1) { gx = o.Waypoints[1].x; gy = o.Waypoints[1].y; }
            int ex, ey; long reach = (long)o.LavaWadePx();
            var l = o.Leader;
            if (l != null && l.Waypoints.Count > 0 && l.Waypoints[0].mode == WpMode.ForceMove && l.Waypoints[0].refId == RefLavaOut && LavaLineClean(o.CenterX, o.CenterY, l.Waypoints[0].x, l.Waypoints[0].y) && LavaPx(o, l.Waypoints[0].x, l.Waypoints[0].y) <= reach)
            { ex = l.Waypoints[0].x; ey = l.Waypoints[0].y; }                               // the squad's way out
            else
            {
                int comp = LavaComp(gx, gy);
                if (!LavaShore(o.CenterX, o.CenterY, comp, LavaWalkMap(gx, gy, comp), gx, gy, reach, out ex, out ey) && !LavaShore(o.CenterX, o.CenterY, -1, null, gx, gy, reach, out ex, out ey)
                    && !LavaShore(o.CenterX, o.CenterY, -1, null, o.CenterX, o.CenterY, int.MaxValue, out ex, out ey))
                    return;                                                                  // no dry spot in reach (not on level 7): Zod's own movement
            }
            var exit = new ZWaypoint { mode = WpMode.ForceMove, refId = RefLavaOut, x = ex, y = ey, playerGiven = w.playerGiven };
            if (w.mode == WpMode.ForceMove && w.refId == RefLavaOut) o.LavaWalkReplace(exit); else o.LavaWalkInsert(exit);
            LavaWalkOuts++;
        }

        /// <summary>A robot of a human team in lava: no order, no target, no hold position for LavaIdleSeconds in a row (its own clock, the sim's time): it walks to the
        /// closest dry shore by itself and stops there. The first of a squad picks the shore, the others come to the same one when their own time is up.</summary>
        void LavaIdle(ZObject o)
        {
            if (!IsHumanTeam(o.Owner)) return;
            bool busy = o.AttackObject != null || o.HoldPosition || !o.CanMove;
            foreach (var w in o.Waypoints) if (w.playerGiven || w.refId == RefLavaOut) { busy = true; break; }   // an order, or a walk out under way; a scuffle nudge is no order
            if (busy) { o.LavaIdleAt = Time; return; }
            if (Time + 1e-9 < o.LavaIdleAt + LavaIdleSeconds + ZConst.TICK) return;
            if (!LavaShore(o.CenterX, o.CenterY, -1, null, o.CenterX, o.CenterY, int.MaxValue, out int ex, out int ey)) { o.LavaIdleAt = Time; return; }   // its own closest shore
            var l = o.Leader ?? o;
            if (l.LavaShoreSeq != l.LavaOrderSeq + 1) { l.LavaShoreX = ex; l.LavaShoreY = ey; l.LavaShoreSeq = l.LavaOrderSeq + 1; }   // the first of a squad sets the squad's shore
            else if (LavaPx(o, l.LavaShoreX, l.LavaShoreY) <= LavaPx(o, ex, ey) + 16 && LavaLineClean(o.CenterX, o.CenterY, l.LavaShoreX, l.LavaShoreY)) { ex = l.LavaShoreX; ey = l.LavaShoreY; }   // the others go there when it is at most a tile farther
            o.Waypoints.Clear();                                                             // (only Zod's own nudges can be left here: the walk out replaces them)
            o.LavaWalkInsert(new ZWaypoint { mode = WpMode.ForceMove, refId = RefLavaOut, x = ex, y = ey }); LavaIdleWalks++;
        }

        /// <summary>ZObject.PathNudge (the separation pushes: unstack, push apart, give way, shove): a robot of a human team standing or walking in lava is
        /// not pushed out of it onto dry ground. Those pushes may never go deeper into lava (Path.BoxAllowed), so they only ever pushed outwards: a robot
        /// sent in stepped out at the shore and back in. Its order, a Stop or the 5 s walk out take it out; on its walk out any push is fine.</summary>
        internal bool LavaPushOut(ZObject o, int newLocX, int newLocY)
        {
            if (!LavaDeviation || !o.IsRobot || !Map.HasLava || !InLavaNow(o) || !IsHumanTeam(o.Owner)) return false;
            if (o.Waypoints.Count > 0 && o.Waypoints[0].refId == RefLavaOut) return false;
            return !LavaAtPx(newLocX + (o.WidthPix >> 1), newLocY + (o.HeightPix >> 1));
        }

        /// <summary>Turns the rule off until it is disposed, then puts back what was there (the parity report holds one while it measures).</summary>
        public sealed class LavaHold : IDisposable
        {
            readonly bool was;
            internal LavaHold() { was = LavaDeviation; LavaDeviation = false; }
            public void Dispose() { LavaDeviation = was; }
        }
        public static LavaHold LavaOff() => new LavaHold();
    }

    public partial class ZObject
    {
        /// <summary>DV152 (ZWorld.Lava.cs): the robot's centre was on lava at the last lava tick; it has had its 75 % warning; it died of lava.</summary>
        public bool InLava, LavaCritical, LavaKilled;
        /// <summary>DV152: 0 cold .. 1 melting (the view's glow); HP lava took in this dip.</summary>
        public double LavaHeat;
        public int LavaTaken;
        internal double LavaNext, LavaLeftAt, LavaHeatOut;
        /// <summary>DV152: a leader's order count (LavaOrder); a minion's walk in still to finish (LavaFollowIn): its lava spot and its
        /// leader's order count then.</summary>
        internal int LavaHalted, LavaOrderSeq, LavaInX, LavaInY, LavaInSeq, LavaInRef, LavaSpotBase, LavaFromX, LavaFromY, LavaShoreX, LavaShoreY, LavaShoreSeq;
        internal bool LavaInPending, LavaInGiven;
        internal double LavaIdleAt, LavaInBy, LavaInBestAt;
        internal int LavaInBest = int.MaxValue;

        /// <summary>DV152: how far this robot can wade before lava melts it (px, half a second to spare), at its pace now.</summary>
        internal double LavaWadePx()
        {
            int bite = ZWorld.LavaBiteHp(this), bites = Math.Max(1, (Health + bite - 1) / bite);
            double t = Math.Max(0, LavaNext - World.Time) + (bites - 1) * ZWorld.LavaBiteEvery - 0.5;
            return Math.Max(0, t) * MoveSpeed * ZWorld.LavaWalkSpeed * DamagedSpeed();
        }

        /// <summary>DV152: the current lava walk (or the MOVE to its edge) is over: Zod's own end of a waypoint.</summary>
        internal void LavaWalkDone() => KillWP();
        internal void LavaWalkInsert(ZWaypoint w) { Waypoints.Insert(0, w); lastWp = ZWaypoint.Cleared; StopMove(); }
        internal void LavaWalkReplace(ZWaypoint w) { Waypoints[0] = w; lastWp = ZWaypoint.Cleared; StopMove(); }

        /// <summary>DV152, ZObject.ProcessMove: a robot on a lava walk (unstoppable, Zod's FORCE_MOVE) whose next step would put it deeper into
        /// a rock, building or cliff tile stops there and its walk ends (true). Every other move: false, untouched.</summary>
        bool LavaWalkHalt(int inx, int iny)
        {
            if (!ZWorld.LavaDeviation || !IsRobot || Waypoints.Count == 0 || !ZWorld.IsLavaWalk(Waypoints[0])) return false;
            if (World.LavaHardTiles(this, inx, iny) <= World.LavaHardTiles(this, LocX, LocY)) return false;
            KillWP(); World.LavaWalkHalts++; LavaHalted++;                                // (a minion's walk in goes on round the wall: LavaFollowIn)
            return true;
        }
    }
}
