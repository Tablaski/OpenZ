using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ 1.5, level 18 "Bridge Game" (kit level18_rules): three rule rows of this level only. Z has none of them; every number is OpenZ's.
    /// Each is made at map load, deterministic (fixed data of this file, whole numbers, object order, no random draw, no clock), and each has
    /// its own switch (the game: on, off in 'Zod exact'; headless default off, so every older check, bot line and parity row keeps its numbers).
    /// Nothing here reads a level id: the map file is tested (<see cref="L18MapFile"/>), so levels 2, 7, 16 and 17 never see any of it.
    /// <list type="bullet">
    /// <item>DV197 "the two river stumps" (<see cref="L18StumpsDeviation"/>): the two half-sunk skyscraper stumps in the two river pools block
    /// their tiles (<see cref="L18StumpTiles"/>: stump A 5 x 3 tiles, stump B 3 x 5, the footprints of kit level18_assets' PLACEMENT.json
    /// river_ruins R1 / R2). Every tile is shallow water; each becomes a HARD impassable (ZPathFinder.SetImpassable(x, y, true, false)): robots
    /// path round it, the amphibious Bizon too, vehicles never entered water anyway; it is not a target, not destroyable, no object (no id is
    /// taken, so every object keeps its id), it does not block a line of fire (Zod's impassable tiles never do; only rocks, huts and wrecks
    /// do, ZWorld.EngageBarrierBetween). The regions were made at map load before (Zod: never rebuilt); the stumps stand inside the pools with 2
    /// open water tiles all round, so no region is split (the counted check proves the robots' wade from fort to fort stays the same length).
    /// A footprint is made only when every tile is shallow water and passable and no object stands on it (<see cref="L18StumpSpotFree"/>);
    /// else it is left out (counted in <see cref="L18StumpsRefused"/>).</item>
    /// <item>DV198 "the big tanks in the start set" (<see cref="L18BigTanksDeviation"/>, owner addition 4): blue's medium tank's map entry (moved
    /// to the top-left by blue_start_v3) is made as a MAMMOTH, the map's RIGHT bottom neutral CRANE (83, 110) as a BIZON (bizon_far_r3, the
    /// owner's picture of 13 Nov: that crane is gone, red keeps its own crewed medium) (<see cref="L18BigTankSpots"/>), through the same
    /// creation path (ZWorld.CreateFromMap: the map entry's place, the same id, every later object's id unchanged). <see cref="L18BigTanksEmpty"/>
    /// may take them (DV180 / DV181: a psycho takes the seat when a robot boards), and the replaced tank's own driver (Zod makes an owned start
    /// vehicle crewed by a grunt, zvehicle.cpp:155-168) stands beside it as one robot of his side (<see cref="L18BigTankDrivers"/>, made after
    /// every other start object). False = the one-constant variant: each is its side's own crewed start tank (a psycho driver), no robot beside
    /// it. Only while the big tank's own switch (ZWorld.MammothDeviation / BizonDeviation) is on as well; else the map's own tank.</item>
    /// <item>DV200 "the optional shuttle on level 18" (<see cref="L18ShuttleDeviation"/>, owner addition 6): the battlefield screen's SHUTTLE
    /// box (UI/OzShuttleStart.cs) works on level 18 as on level 17: on EASY / MEDIUM with the box ON the player's crewed shuttle stands by HIS
    /// fort (<see cref="L18StartShuttle"/> = the box's answer; red: <see cref="L18RedFortShuttleSpot"/>, blue: <see cref="L18BlueFortShuttleSpot"/>),
    /// it is <see cref="ZWorld.PlayableShuttle"/> and flies by the shuttle's rules (DV118 and its friends: it lands only where the landing spot
    /// rule takes it, which refuses water, rocks, buildings (a bridge is one), units and the map edge, so it crosses the river in the air).
    /// HARD, MAYHEM or the box OFF: no shuttle. The computer gets none (as on levels 16 and 17). Each spot is checked with the start spot rule
    /// on its exact heading (ZWorld.StartShuttleSpotFree); if the map ever changed under it, the nearest tile within
    /// <see cref="SideSpotSearchTiles"/> tiles that takes the ship is used (ZWorld.SidePlace), else none is made. The spots were chosen by the
    /// kit's headless measure (kit_tools/harness: every tile and heading round each fort; the room of ZWorld.ShuttleStartRoom, the walk from
    /// the fort's gate, the drawn lamps / bollards / boulders of the level's dress, the briefing stages: not on the blue convoy's roads south of
    /// the blue fort, not on the red fort's north face where the red briefing's deck hangs).</item>
    /// </list>
    /// DV199 (the computer and the big tanks) is in Sim/ZBot.BigTanks.cs.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>The decoded level 18 map file (Resources/OpenZ/Maps/L18_grid.json "file").</summary>
        public const string L18MapFile = "p02_bb_orig18.map";
        /// <summary>This world plays level 18's map.</summary>
        public bool L18Map => Map != null && !Map.aiMap && string.Equals(Map.file, L18MapFile, StringComparison.OrdinalIgnoreCase);

        // ------------------------------------------------------------------ DV197 the two river stumps
        /// <summary>DV197 switch (off = Zod: the pools are open water everywhere).</summary>
        public static bool L18StumpsDeviation = false;
        /// <summary>
        /// stand at the EDGE of the map as view-only scenery (View/OzL18Rules.cs, menu OpenZ/L18 Bridge Game/10): they close no tile, the pools
        /// are open water as in Zod (round 4 had stump A 5 x 3 at (50, 58) and stump B 3 x 5 at (72, 57) here). The switch and the row stay.</summary>
        public static readonly int[] L18StumpTiles = { };
        public static readonly string[] L18StumpIds = { "R1", "R2" };
        /// <summary>The footprints made at map load (tile x, y, w, h; read only for the view and the checks).</summary>
        public readonly List<int[]> L18Stumps = new List<int[]>();
        public int L18StumpsRefused { get; private set; }

        /// <summary>ZWorld.Create hook, right after the harbour wrecks (DV39): no object is made, only tiles are closed.</summary>
        void AddL18Stumps()
        {
            L18Stumps.Clear(); L18StumpsRefused = 0;
            if (!L18StumpsDeviation || !L18Map) return;
            var t = L18StumpTiles;
            for (int k = 0; k + 3 < t.Length; k += 4)
            {
                int tx = t[k], ty = t[k + 1], w = t[k + 2], h = t[k + 3];
                if (!L18StumpSpotFree(tx, ty, w, h)) { L18StumpsRefused++; continue; }
                for (int i = tx; i < tx + w; i++)
                    for (int j = ty; j < ty + h; j++) Path.SetImpassable(i, j, true, false);
                L18Stumps.Add(new[] { tx, ty, w, h });
            }
        }

        /// <summary>DV197: every footprint tile is shallow water and passable, and no object stands on it.</summary>
        public bool L18StumpSpotFree(int tx, int ty, int w, int h)
        {
            for (int i = tx; i < tx + w; i++)
                for (int j = ty; j < ty + h; j++)
                    if (Map.ClassAt(i, j) != ZClass.Water || !Path.TilePassable(i, j, true)) return false;
            var probe = new ZObject { WidthPix = w * 16, HeightPix = h * 16 }; probe.SetCords(tx * 16, ty * 16);
            foreach (var o in Objects) if (!o.Destroyed && probe.IntersectsObject(o)) return false;
            return true;
        }

        /// <summary>DV197: tile (tx, ty) is under a stump made at map load (the view and the checks).</summary>
        public bool L18StumpAt(int tx, int ty)
        {
            foreach (var r in L18Stumps) if (tx >= r[0] && tx < r[0] + r[2] && ty >= r[1] && ty < r[1] + r[3]) return true;
            return false;
        }

        // ------------------------------------------------------------------ DV198 the big tanks in the start set
        /// <summary>DV198 switch (off = Zod: the map's heavy and medium tank).</summary>
        public static bool L18BigTanksDeviation = false;
        /// <summary>
        /// makes both big tanks start EMPTY and neutral (grey), to be taken like any empty vehicle, and the replaced start tank's own driver (Zod's
        /// grunt of that side) stands beside it as a normal robot of his side (<see cref="L18BigTankDrivers"/>); false = the one-constant variant:
        /// each is its side's own crewed start tank (a psycho driver, DV180 / DV181), no robot beside it. Read at map load.</summary>
        public static bool L18BigTanksEmpty = true;
        /// <summary>Tile x, tile y of the map entry, the map's type there, the big tank made in its place, and the tile its driver stands on
        /// (EMPTY start: the replaced tank's driver, 2.5 m west of the drawn hull; checked at map load, else the nearest free tile). The two map
        /// entries were moved in level 18's map file by this kit (Resources/OpenZ/Maps/L18_grid.json, owner addition 7 point I: the Mammoth stood
        /// with its drawn hull 1.9 m INTO blue's light tank at (53, 35), the Bizon 0.5 m into the red fort's wall): blue's heavy (53, 33) -> (53, 30),
        /// red's medium (56, 80) -> (53, 88), each on the west side of its fort, the Bizon where the Mammoth stands mirrored round the river
        /// (the forts mirror top to bottom; one tile nearer its fort, a night post stands on the exact mirror place).</summary>
        /// bizon_far_r3 (the owner's Bizon notes of 13 Nov: "empty bizon i said NOT near fort, it should be low bottom right ... so players can find
        /// it", then his picture: the Bizon's spot
        /// is the map's RIGHT bottom neutral crane entry (83, 110) (that crane is made as the Bizon: gone; the other three neutral cranes stay);
        /// red's medium tank (53, 88) is red's own crewed tank again. side: the side of the spot's driver when the map entry is neutral ("" = the
        /// map entry's own side): red's grunt stands on his fixed tile (51, 88) by the red fort.
        public static readonly (int tx, int ty, string from, string to, int dtx, int dty, string side)[] L18BigTankSpots =
        {
            (8, 20, "medium", ZBigTanks.Mammoth, 74, 33, ""), // blue's medium tank (map entry moved (70, 36) -> (53, 30) by kit blue_start_v2, (53, 30) -> (8, 20) by kit blue_start_v3, owner additions 43, 48: blue keeps its own crewed heavy tank at (53, 35)): the EMPTY Mammoth FAR from both forts in the top-left (north-west) of the map, west of the north-west road; its replaced driver stands by the BLUE FORT (tile 74, 33, the fort's east side), not beside it, so the far tank is really empty
            (83, 110, "crane", ZBigTanks.Bizon, 51, 88, "red"),   // bizon_far_r3 (the owner's picture of 13 Nov): the map's RIGHT bottom NEUTRAL crane (Z's file; the left one is (33, 109)) = the EMPTY Bizon FAR from both forts in the bottom-right half, on that crane's spot and turn; that crane is gone; red keeps its own crewed medium tank at (53, 88) (map entry moved from (56, 80) by level18_rules), red's grunt stays on (51, 88) by the red fort
        };
        /// <summary>
        /// kits/level18/owner_notes/owner_20261113_bizon_red_box_and_road_gaps_yellow.webp): a big tank made from map entry (tx, ty) is placed on
        /// tile (mtx, mty) instead, facing (fx, fy) (Zod px; the scene's model is turned the same way by menu OpenZ/L18 Bridge Game/31). The map
        /// entry, its id and Zod's file stay; DV198 off ('Zod exact'): nothing moves. Done in <see cref="AddL18BigTankDrivers"/>, AFTER DV66's
        /// exit-lane parking: the owner's spot is ON the bottom road in front of the robot factory (77, 103), and he wants it exactly there.</summary>
        public static readonly (int tx, int ty, int mtx, int mty, float fx, float fy)[] L18BigTankMoves =
        {
            (83, 110, 78, 110, 9f, 0f),   // the EMPTY Bizon: tile (78, 110) (Unity x 158, z -222), his red box: on the bottom road below the factory, 5 tiles left of the crossing, front to the east
        };
        /// <summary>kit l18_map_fixes_r3: the tile a big tank made from map entry (tx, ty) stands on at the start (<see cref="L18BigTankMoves"/>, else the entry's own).</summary>
        public static (int x, int y) L18BigTankTile(int tx, int ty)
        {
            foreach (var m in L18BigTankMoves) if (m.tx == tx && m.ty == ty) return (m.mtx, m.mty);
            return (tx, ty);
        }
        /// <summary>kit l18_map_fixes_r3: the big tank to its <see cref="L18BigTankMoves"/> tile and facing (as DV66's ClearExitLanes moves a start
        /// vehicle: SetCords, zone, move speed).</summary>
        void L18BigTankMove(ZObject made, ZMapData.Obj o)
        {
            foreach (var m in L18BigTankMoves)
                if (m.tx == o.x && m.ty == o.y)
                {
                    made.SetCords(m.mtx * 16, m.mty * 16);
                    made.ZoneIndex = GetZoneIndex(made.LocX, made.LocY);
                    made.InitRealMoveSpeed(Map);
                    made.FaceX = m.fx; made.FaceY = m.fy;
                }
        }
        /// <summary>The big tanks made at map load (read only for the view and the checks).</summary>
        public readonly List<ZObject> L18BigTanks = new List<ZObject>();
        /// <summary>EMPTY start: the robot standing beside each empty big tank (the replaced tank's driver: one robot of his side, no squad); read
        /// only for the view (its view is made at the start: View/OzL18Rules.cs) and the checks.</summary>
        public readonly List<ZObject> L18BigTankDrivers = new List<ZObject>();
        /// <summary>EMPTY start: drivers that found no free tile beside their tank (none made).</summary>
        public int L18BigTankDriversRefused { get; private set; }

        /// <summary>ZWorld.CreateFromMap hook: the type a map vehicle is made as (the big tank on its spot, else the map's own type).</summary>
        string L18BigTankTypeAt(ZMapData.Obj o, string type)
        {
            if (!L18BigTanksDeviation || o == null || o.Category != "vehicle" || !L18Map) return type;
            foreach (var s in L18BigTankSpots)
                if (s.tx == o.x && s.ty == o.y && s.from == type && ZBigTanks.On(s.to)) return s.to;
            return type;
        }

        /// <summary>ZWorld.CreateFromMap hook: the owner the map vehicle is made with (neutral = empty while <see cref="L18BigTanksEmpty"/>).</summary>
        Team L18BigTankOwner(ZMapData.Obj o, string type, Team owner) =>
            L18BigTanksEmpty && L18BigTanksDeviation && type != o.Type && ZBigTanks.Is(type) ? Team.Neutral : owner;

        /// <summary>ZWorld.CreateFromMap hook, after the object is made: the list for the view and the checks.</summary>
        void L18BigTankMade(ZObject made, ZMapData.Obj o)
        {
            if (made != null && L18BigTanksDeviation && made.Kind == ZKind.Vehicle && ZBigTanks.Is(made.Type) && o != null && o.Type != made.Type) L18BigTanks.Add(made);
        }

        /// <summary>ZWorld.Create hook, right after DV200's ship (every start vehicle and the ship stand where they start): EMPTY start only, the
        /// replaced tank's driver (one robot of the map entry's side, the driver type Zod gives the map's tank: the grunt) on the spot's driver tile
        /// beside the tank, else on the nearest free tile round it (<see cref="L18DriverTileFree"/>; fixed order: ring, row, column). Made after
        /// every other start object, so every older object keeps its id.</summary>
        void AddL18BigTankDrivers()
        {
            L18BigTankDrivers.Clear(); L18BigTankDriversRefused = 0;
            if (L18BigTanksDeviation && L18Map) foreach (var t in L18BigTanks) if (!t.Destroyed && t.Source != null) L18BigTankMove(t, t.Source);
            if (!L18BigTanksDeviation || !L18BigTanksEmpty || !L18Map) return;
            foreach (var t in L18BigTanks)
            {
                if (t.Destroyed || t.Owner != Team.Neutral || t.Drivers.Count > 0 || t.Source == null) continue;
                var side = ParseOwner(t.Source.owner);
                int dtx = -1, dty = -1; bool fixedTile = false;
                foreach (var s in L18BigTankSpots)
                    if (s.tx == t.Source.x && s.ty == t.Source.y)
                    {
                        if (side == Team.Neutral && !string.IsNullOrEmpty(s.side)) { side = ParseOwner(s.side); dtx = s.dtx; dty = s.dty; fixedTile = true; }   // bizon_far_r3: a NEUTRAL entry's big tank: the spot's driver on his own fixed tile by his fort
                        else { dtx = s.dtx + (t.LocX / 16 - s.tx); dty = s.dty + (t.LocY / 16 - s.ty); }   // a tank DV66 moved keeps its driver on the same side
                    }
                if (side == Team.Neutral) continue;
                if (!L18DriverTileFree(dtx, dty))
                {
                    bool found = false; int tx0 = fixedTile ? dtx : t.LocX / 16, ty0 = fixedTile ? dty : t.LocY / 16;   // bizon_far_r3: a fixed driver tile is searched round itself
                    for (int r = 2; r <= 5 && !found; r++)
                        for (int j = ty0 - r; j <= ty0 + 1 + r && !found; j++)
                            for (int i = tx0 - r; i <= tx0 + 1 + r && !found; i++)
                            {
                                if (Math.Max(Math.Max(tx0 - i, i - tx0 - 1), Math.Max(ty0 - j, j - ty0 - 1)) != r) continue;   // this ring only
                                if (L18DriverTileFree(i, j)) { dtx = i; dty = j; found = true; }
                            }
                    if (!found) { L18BigTankDriversRefused++; continue; }
                }
                L18BigTankDrivers.Add(CreateRobotGroup(ZBigTanks.FirstDriver(t.Source.Type), dtx * 16, dty * 16, side, 1));
            }
        }

        /// <summary>EMPTY start: a robot may stand on tile (tx, ty): dry ground or road a robot may walk, no object within half a tile of it (a
        /// flag excepted), out of every building's exit lane and every parking keep-out disc (lamp posts), and at least
        /// <see cref="L18DriverClearPx"/> px (middle to middle) from every vehicle and cannon, so it stands clear of the drawn hulls.</summary>
        bool L18DriverTileFree(int tx, int ty)
        {
            if (tx < 1 || ty < 1 || tx >= Map.width - 1 || ty >= Map.height - 1) return false;
            var c = Map.ClassAt(tx, ty);
            if ((c != ZClass.Ground && c != ZClass.Road) || !Path.TilePassable(tx, ty, true)) return false;
            int cx = tx * 16 + 8, cy = ty * 16 + 8;
            var probe = new ZObject { WidthPix = 32, HeightPix = 32 }; probe.SetCords(tx * 16 - 8, ty * 16 - 8);
            foreach (var o in Objects)
            {
                if (o.Destroyed || (o.Kind == ZKind.MapItem && o.Type == "flag")) continue;
                if (probe.IntersectsObject(o)) return false;
                if (o.Kind == ZKind.Vehicle || o.Kind == ZKind.Cannon)
                {
                    long dx = o.CenterX - cx, dy = o.CenterY - cy;
                    if (dx * dx + dy * dy < (long)L18DriverClearPx * L18DriverClearPx) return false;
                }
            }
            if (InExitLane(cx, cy, ZPathing.RobotRadius)) return false;
            if (ParkingKeepOut.Count > 0 && ParkingKeepOutMap == Map.file)
                foreach (var k in ParkingKeepOut) { long dx = k.x - cx, dy = k.y - cy; if (dx * dx + dy * dy <= (long)(k.r + 8) * (k.r + 8)) return false; }
            return true;
        }
        /// <summary>EMPTY start: the driver's least middle-to-middle distance to any vehicle or cannon (px; 36 px = 4.5 m).</summary>
        public const int L18DriverClearPx = 36;

        // ------------------------------------------------------------------ DV200 the optional shuttle
        /// <summary>DV200 switch (off = level 18 has no shuttle of its own).</summary>
        public static bool L18ShuttleDeviation = false;
        /// <summary>DV200: the SHUTTLE box's answer for this level start (level 18, EASY / MEDIUM, the box on).</summary>
        public static bool L18StartShuttle = false;
        /// <summary>The red player's start shuttle: tile x, tile y (its middle on that tile's middle), heading (deg; 0 = east, 90 = south).</summary>
        public static readonly int[] L18RedFortShuttleSpot = { 75, 76, 0 };
        /// <summary>The blue player's start shuttle.</summary>
        public static readonly int[] L18BlueFortShuttleSpot = { 54, 41, -90 };
        /// <summary>
        /// Level 18's HEAD START (View/ZGame.HeadStartL18Types, the player only, never the computer) is two jeeps and a
        /// light tank, crewed, as on levels 7 and 16 (neither level 16 nor level 17 gives a crane at the start). No side starts with a crane: the
        /// cranes are found EMPTY further away (the map's neutral cranes far from the forts: four in Z's file, three while DV198 is on, one or more in each half: the right bottom one
        /// (83, 110) is the EMPTY Bizon's spot, bizon_far_r3). Read by the counted check.</summary>
        public static readonly string[] L18HeadStartTypes = { "jeep", "jeep", "light" };
        /// <summary>The shuttle made at map load (null: another level, HARD, the box off, a switch off).</summary>
        public ZObject L18FortShuttle { get; private set; }
        public int L18ShuttlesMade { get; private set; }
        /// <summary>Why no ship was made or it moved ("" = as wanted).</summary>
        public string L18ShuttleWhy { get; private set; } = "";

        /// <summary>ZWorld.Create hook, right after DV144's side shuttles: the player's ship by his fort when the box asks.</summary>
        void AddL18Shuttle()
        {
            L18FortShuttle = null; L18ShuttlesMade = 0; L18ShuttleWhy = "";
            if (!L18ShuttleDeviation || !ShuttlePlayableDeviation || !ShuttleOn || !L18Map) return;
            if (!L18StartShuttle) { L18ShuttleWhy = "the box did not ask for one"; return; }
            var spot = ShuttleTeam == Team.Blue ? L18BlueFortShuttleSpot : ShuttleTeam == Team.Red ? L18RedFortShuttleSpot : null;
            if (spot == null) { L18ShuttleWhy = "no player team"; return; }
            int movedBefore = SideShuttlesMoved;
            L18FortShuttle = SidePlace(spot, ShuttleTeam, "level 18 fort");
            L18ShuttleWhy = SideShuttlesMoved != movedBefore ? "moved: its spot took no ship" : L18FortShuttle == null ? "no free spot" : "";
            if (L18FortShuttle == null) return;
            L18ShuttlesMade++;
            PlayableShuttle = L18FortShuttle;
        }

        /// <summary>The checks and the parity report: DV197, DV198 and DV200 held off until disposed (a Play session leaves them on).</summary>
        public static IDisposable L18RulesOff() => new L18RulesOffHold();
        sealed class L18RulesOffHold : IDisposable
        {
            readonly bool a = L18StumpsDeviation, b = L18BigTanksDeviation, c = L18ShuttleDeviation, d = L18StartShuttle;
            public L18RulesOffHold() { L18StumpsDeviation = false; L18BigTanksDeviation = false; L18ShuttleDeviation = false; L18StartShuttle = false; }
            public void Dispose() { L18StumpsDeviation = a; L18BigTanksDeviation = b; L18ShuttleDeviation = c; L18StartShuttle = d; }
        }
    }
}
