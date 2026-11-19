using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// 11 Nov 2026). Z has neither; every number is OpenZ's.
    /// <para>DV192 (N12): the BIZON is the only tank that drives through WATER (the L02 river, the L16 swamp, the L17 harbour: every tile of class
    /// Water) and LAVA (the L07 lava tiles, class Impassable with lava data) as passable ground. On such a tile it drives at
    /// <see cref="ZWorld.BizonWadePace"/> of its pace (Zod's robots wade at 0.7); it keeps firing as on land; it never starts a ram / crush while
    /// it or its target stands in water or lava (ZObject.RamTargetOk + RamWet).</para>
    /// <para>
    /// LAVA BURNS THE BIZON SLOWLY. Each tick its centre is on a lava tile it loses heat damage, its own kind (o.DamageHealth only:
    /// no attacker, no fire hit, no event, no help call, no kill credit): MaxHealth / <see cref="ZWorld.BizonLavaDieTicks"/> a tick, whole HP through
    /// a fixed-point remainder, so a full-health Bizon held in lava is gone after exactly BizonLavaDieTicks ticks (27.00 s). Off the lava the burn
    /// stops at once (the remainder cleared). A death in it is the usual vehicle death (the Death event, no killer). Water and land: never.
    /// The path finder weighs a lava tile <see cref="ZPathFinder.AmphibLavaSide"/> in the Bizon's grid (x 8 a dry tile, x 4.8 a water tile), so a
    /// path crosses lava only where the way round is long; an order into the lava (or a stop in it) is the player's: it burns there.</para>
    /// <para>Its own passability: two more grids on the path finder (<see cref="ZPathFinder.GridAmphib"/>, with and without destroyable rocks, and
    /// their own 4-connected regions) where water and lava tiles are open unless an object blocks them. Every vehicle-grid read of the path finder
    /// (TilePassable, the A* and the straight-line test, WithinImpassable, the regions, ZReach's components) goes to them only while the rules work
    /// for a Bizon (<see cref="ZPathFinder.Kind"/>: its own ProcessServer, its queued path search, the pathing layer's box tests of it). The caches
    /// the pathing layer shares between units (lanes, zones, bridges) are always built on Zod's grids, ZReach keeps one component set per grid
    /// kind: no other unit's path, step or result changes.</para>
    /// <para>DV193 (N22): the HEAVY TANK (v1 and v2: one rules type), the BIZON and the MAMMOTH no longer stop at or path round a VEHICLE WRECK
    /// (DV39's harbour car wrecks, the checks' test wreck; never the Unit editor's holo obstacle, a rock, a hut or a prop): for them its tiles are
    /// open ground (<see cref="ZPathFinder.GridHeavy"/>; a wreck in water only for the amphibious Bizon), and the tick their box touches it the
    /// wreck is crushed flat: destroyed at once by the game's own death path (the Death event with the tank as the attacker: the view breaks it
    /// with sparks and the crash; no kill credit, no debris shell, no random draw), so its tiles open as for any destroyed barrier. Every other
    /// unit treats wrecks as before (round them, or blast them with explosives).</para>
    /// <para>Switches <see cref="ZWorld.BizonAmphibDeviation"/>, <see cref="ZWorld.BigTankWreckDeviation"/>: View/ZGame.cs on at every start (off in
    /// 'Zod exact'); headless default off; the parity report and the counted checks hold them off. Off: the grids are kept but never read, not one
    /// field of any unit changes. Deterministic: whole numbers and bools only, object order, no random draw, no clock.</para>
    /// </summary>
    public sealed partial class ZPathFinder
    {
        /// <summary>The grid kinds: Zod's vehicle grid, the heavy tanks' (wrecks open), the Bizon's (water, lava and wrecks open).</summary>
        public const int GridZod = 0, GridHeavy = 1, GridAmphib = 2;
        /// <summary>A water tile's weight in the Bizon's grid (one tile, before the 2x2 sum): 100 / 0.6 like Zod's 100 / 0.7 for water.</summary>
        public const int AmphibSide = 166, AmphibDiag = 235;
        /// <summary>DV192 amended (N12b): a LAVA tile's weight in the Bizon's grid (one tile, before the 2x2 sum): 8 x a dry tile's 100, so lava
        /// costs the path as it costs HP: a crossing of n lava tiles is taken only where the way round is more than about 7 n tiles longer.</summary>
        public const int AmphibLavaSide = 800, AmphibLavaDiag = 1131;

        Info[,] heavyG, heavyNR, amphG, amphNR;
        int[,] heavyRegion, amphRegion, baseSide, baseDiag;
        bool[,] waterT, lavaT, basePass, objBlocked, objDestroyable, wreckOpen;
        int gridKind, landWreckTiles;
        bool anyLava;

        /// <summary>The grid kind the vehicle reads go to now (GridZod outside every scope).</summary>
        public int GridKind => gridKind;
        /// <summary>A crushable wreck stands on land (the heavy tanks' grid differs from Zod's only then).</summary>
        public bool HeavyGridDiffers => landWreckTiles > 0;

        Info[,] VG => gridKind == GridAmphib ? amphG : gridKind == GridHeavy ? heavyG : vehicle;
        Info[,] VGN => gridKind == GridAmphib ? amphNR : gridKind == GridHeavy ? heavyNR : vehicleNoRocks;
        int[,] VR => gridKind == GridAmphib ? amphRegion : gridKind == GridHeavy ? heavyRegion : vehicleRegion;

        /// <summary>The vehicle reads go to grid <paramref name="kind"/> until Dispose (scopes nest; the old kind comes back).</summary>
        public KindScope Kind(int kind)
        {
            var s = new KindScope(this, gridKind);
            gridKind = heavyG == null || kind < GridZod || kind > GridAmphib ? GridZod : kind;
            return s;
        }

        public readonly struct KindScope : IDisposable
        {
            readonly ZPathFinder pf; readonly int was;
            internal KindScope(ZPathFinder p, int w) { pf = p; was = w; }
            public void Dispose() { if (pf != null) pf.gridKind = was; }
        }

        bool AmphT(int x, int y) => waterT[x, y] || lavaT[x, y];
        /// <summary>DirectPathPossible hook (N12b): in the Bizon's grid a lava tile near the line makes it no straight line, so the A* weighs the lava
        /// (a lava tile is <see cref="AmphibLavaSide"/>); every other grid kind: false (Zod's test as it was).</summary>
        bool LavaLine(int tx, int ty) => gridKind == GridAmphib && lavaT != null && lavaT[tx, ty];

        /// <summary>A* hook (N12b): in the Bizon's grid on a map with lava, a start / end tile whose 2 x 2 footprint is not open moves to the nearest
        /// open one (rings 1 and 2, a fixed order), so the search runs and weighs the lava; Zod's answer there (no search, the straight line) would
        /// drive the Bizon straight through it. The path still ends at the order's own point. Every other grid kind and map: Zod's as it was.</summary>
        void AmphEnds(ref int sx, ref int sy, ref int ex, ref int ey, bool isRobot, Info[,] t)
        {
            if (isRobot || gridKind != GridAmphib || !anyLava) return;
            OpenNear(ref sx, ref sy, t); OpenNear(ref ex, ref ey, t);
        }

        void OpenNear(ref int x, ref int y, Info[,] t)
        {
            if (TileOk(x, y, x, y, false, t)) return;
            for (int r = 1; r <= 2; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int nx = x + dx, ny = y + dy;
                        if (TileOk(nx, ny, nx, ny, false, t)) { x = nx; y = ny; AmphEndMoves++; return; }
                    }
        }

        /// <summary>Statistics (the counted check): A* ends the Bizon's grid moved to an open footprint.</summary>
        public static int AmphEndMoves;

        /// <summary>Reset hook.</summary>
        void KindReset(int w, int h)
        {
            heavyG = new Info[w, h]; heavyNR = new Info[w, h]; amphG = new Info[w, h]; amphNR = new Info[w, h];
            heavyRegion = new int[w, h]; amphRegion = new int[w, h]; baseSide = new int[w, h]; baseDiag = new int[w, h];
            waterT = new bool[w, h]; lavaT = new bool[w, h]; basePass = new bool[w, h]; objBlocked = new bool[w, h]; objDestroyable = new bool[w, h]; wreckOpen = new bool[w, h];
            gridKind = GridZod; landWreckTiles = 0; anyLava = false;
        }

        /// <summary>SetTileInfo hook: the tile's terrain again (no object, no open wreck), the kinds' copies from Zod's tile.</summary>
        void KindTileInfo(int x, int y, PfTile t)
        {
            if (heavyG == null) return;
            baseSide[x, y] = vehicle[x, y].side; baseDiag[x, y] = vehicle[x, y].diag; basePass[x, y] = vehicle[x, y].passable;
            waterT[x, y] = t == PfTile.Water;
            objBlocked[x, y] = objDestroyable[x, y] = false;
            if (wreckOpen[x, y]) { wreckOpen[x, y] = false; if (!AmphT(x, y)) landWreckTiles--; }
            heavyG[x, y] = vehicle[x, y]; heavyNR[x, y] = vehicleNoRocks[x, y];
            amphG[x, y] = new Info { side = BS1(x, y, vehicle[x, y].side), diag = BD1(x, y, vehicle[x, y].diag) };
            amphNR[x, y] = amphG[x, y];
            KindPass(x, y);
        }

        /// <summary>SetImpassable hook: an object blocks (or no longer blocks) the tile.</summary>
        void KindImpass(int x, int y, bool impassable, bool destroyable)
        {
            if (heavyG == null) return;
            objBlocked[x, y] = impassable; objDestroyable[x, y] = impassable && destroyable;
            KindPass(x, y);
        }

        void KindPass(int x, int y)
        {
            bool wo = wreckOpen[x, y];
            heavyG[x, y].passable = vehicle[x, y].passable || (wreckOpen[x, y] && basePass[x, y]);
            heavyNR[x, y].passable = vehicleNoRocks[x, y].passable || (wreckOpen[x, y] && basePass[x, y]);
            if (AmphT(x, y))
            {
                amphG[x, y].passable = !objBlocked[x, y] || wo;
                amphNR[x, y].passable = !objBlocked[x, y] || objDestroyable[x, y] || wo;
            }
            else
            {
                amphG[x, y].passable = heavyG[x, y].passable;
                amphNR[x, y].passable = heavyNR[x, y].passable;
            }
        }

        /// <summary>The world's map-load hook: tile (x, y) is lava (open in the Bizon's grid).</summary>
        public void KindMarkLava(int x, int y)
        {
            if (heavyG == null || !TileOnMap(x, y)) return;
            lavaT[x, y] = true; anyLava = true;
            amphG[x, y].side = amphNR[x, y].side = AmphibLavaSide; amphG[x, y].diag = amphNR[x, y].diag = AmphibLavaDiag;
            KindPass(x, y);
        }

        /// <summary>The world's map-load hook, after Zod's SetTileWideWeights: the heavy grid takes Zod's 2x2 sums, the Bizon's grid sums its own
        /// tile weights over the same 2x2 (zpath_finding.cpp:473-546).</summary>
        public void KindWide()
        {
            if (heavyG == null) return;
            for (int i = 0; i < W; i++)
                for (int j = 0; j < H; j++)
                {
                    heavyG[i, j].side = heavyNR[i, j].side = vehicle[i, j].side; heavyG[i, j].diag = heavyNR[i, j].diag = vehicle[i, j].diag;
                    int s = BS(i, j), d = BD(i, j);
                    if (i + 1 < W) { s += BS(i + 1, j); d += BD(i + 1, j); }
                    if (j + 1 < H) { s += BS(i, j + 1); d += BD(i, j + 1); }
                    if (i + 1 < W && j + 1 < H) { s += BS(i + 1, j + 1); d += BD(i + 1, j + 1); }
                    amphG[i, j].side = amphNR[i, j].side = s; amphG[i, j].diag = amphNR[i, j].diag = d;
                }
        }

        int BS(int i, int j) => BS1(i, j, baseSide[i, j]);
        int BD(int i, int j) => BD1(i, j, baseDiag[i, j]);
        /// <summary>One tile's weight in the Bizon's grid: lava (N12b), water, else the terrain's own.</summary>
        int BS1(int i, int j, int dry) => lavaT[i, j] ? AmphibLavaSide : waterT[i, j] ? AmphibSide : dry;
        int BD1(int i, int j, int dry) => lavaT[i, j] ? AmphibLavaDiag : waterT[i, j] ? AmphibDiag : dry;

        /// <summary>A crushable wreck's footprint tile: open for the heavy tanks (on land) and the Bizon (land and water) until SetTileInfo
        /// gives the tile its terrain back (the wreck destroyed).</summary>
        public void KindOpenWreckTile(int x, int y)
        {
            if (heavyG == null || !TileOnMap(x, y) || wreckOpen[x, y]) return;
            wreckOpen[x, y] = true;
            if (!AmphT(x, y)) landWreckTiles++;
            KindPass(x, y);
        }

        /// <summary>RebuildRegions hook (map load): the kinds' own 4-connected regions.</summary>
        void KindRegions()
        {
            if (heavyG == null) return;
            BuildRegions(heavyG, heavyRegion); BuildRegions(amphG, amphRegion);
        }

        /// <summary>Checks: tile passable for a vehicle on grid kind k (no scope needed).</summary>
        public bool TilePassableKind(int tx, int ty, int k)
        {
            if (!TileOnMap(tx, ty)) return false;
            var g = k == GridAmphib && amphG != null ? amphG : k == GridHeavy && heavyG != null ? heavyG : vehicle;
            return g[tx, ty].passable;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV192 the Bizon is amphibious (ZGame: on at every start, off in 'Zod exact'; headless default off).</summary>
        public static bool BizonAmphibDeviation = false;
        /// <summary>DV193 the heavy tank, the Bizon and the Mammoth crush vehicle wrecks (ZGame: on at every start, off in 'Zod exact'; headless default off).</summary>
        public static bool BigTankWreckDeviation = false;
        /// <summary>DV192: the Bizon's pace on a water or lava tile (x its move speed; Zod's robots wade at 0.7).</summary>
        public const double BizonWadePace = 0.6;
        /// <summary>DV192 amended (N12b): ticks in lava that burn a full-health Bizon to nothing: 2700 = 27.00 s at Zod's 100 Hz (MaxHealth / 2700 HP a
        /// tick, a fixed-point remainder). Chosen from L07 (README, the kit's measure of all 119 straight L07 lava runs): at its lava pace a tile takes
        /// its centre ~3.8 s; with lava weighing x 8 on its path the widest crossing it still takes is a 2 tile neck crossed on a slant (9.2 s: 34 %
        /// of its health), a straight 2 tile one 7.6 s (28 %), one tile 3.8 s (14 %); held in it: dead in 27 s.</summary>
        public const int BizonLavaDieTicks = 2700;
        /// <summary>Statistics (the counted check): HP lava took from Bizons, Bizons lava destroyed.</summary>
        public static int BizonLavaDealt, BizonLavaBurns;

        /// <summary>DV192 amended (N12b): once a tick after the robots' lava (ZWorld.Tick): every live Bizon whose centre is on a lava tile loses its heat
        /// damage; off the lava it stops at once. Whole numbers only (a long remainder per hull), object order, no random draw, no clock.</summary>
        void BizonHeatTick()
        {
            if (!BizonAmphibDeviation || !BizonDeviation || !Map.HasLava || GameOver) return;    // (like the robots' lava: nothing bites after the game is over)
            int n = Objects.Count;
            for (int i = 0; i < n && i < Objects.Count; i++)
            {
                var o = Objects[i];
                if (o.Kind != ZKind.Vehicle || o.Type != ZBigTanks.Bizon || o.Destroyed || o.KillAt >= 0 || o.MaxHealth <= 0) continue;
                if (!InLavaNow(o)) { o.BizonLavaTicks = 0; o.BizonHeatAcc = 0; continue; }      // out: the burn stops at once
                o.BizonLavaTicks++;
                o.BizonHeatAcc += o.MaxHealth;
                long d = o.BizonHeatAcc / BizonLavaDieTicks;
                if (d <= 0) continue;
                o.BizonHeatAcc -= d * BizonLavaDieTicks;
                int hp0 = o.Health;
                o.DamageHealth(d >= int.MaxValue ? int.MaxValue : (int)d);                       // heat: no attacker, no fire hit, no event of its own
                int took = hp0 - o.Health; o.BizonLavaTaken += took; BizonLavaDealt += took;
                if (o.Destroyed) { BizonLavaBurns++; UpdateObjectHealth(o); }                    // the usual vehicle death, no killer
            }
        }

        /// <summary>ZReach.Nearest hook (N12b): in the Bizon's grid on a lava map, when the point asked for is not itself in the lava, a skip that also
        /// leaves out every footprint touching a lava tile, so an unreachable goal's nearest reachable point (and a settle point) is on dry ground: it
        /// never parks in the lava unless the player's point is in it. ZReach falls back to its own answer when no dry point is found. null: no
        /// change (every other unit, grid kind and map, or a point in the lava).</summary>
        internal Func<int, int, bool> BizonDrySkip(int x, int y, Func<int, int, bool> skip)
        {
            if (Path.GridKind != ZPathFinder.GridAmphib || !Map.HasLava || x < 0 || y < 0 || Map.LavaAt(x >> 4, y >> 4)) return null;
            BizonDryGoals++;
            return (px, py) => (skip != null && skip(px, py)) || FootLava(px, py);
        }

        /// <summary>A vehicle footprint centred on (px, py) (ZReach.FootCentre: the corner of its 2 x 2 tiles) touches a lava tile.</summary>
        bool FootLava(int px, int py)
        {
            int tx = (px - 16) >> 4, ty = (py - 16) >> 4;
            return Map.LavaAt(tx, ty) || Map.LavaAt(tx + 1, ty) || Map.LavaAt(tx, ty + 1) || Map.LavaAt(tx + 1, ty + 1);
        }

        /// <summary>Statistics (the counted check): nearest-point searches held off the lava for a Bizon.</summary>
        public static int BizonDryGoals;

        /// <summary>The parity report and the older counted checks: both off while this lives.</summary>
        public static IDisposable BigTankGroundOff() => new BigTankGroundOffHold();
        sealed class BigTankGroundOffHold : IDisposable
        {
            readonly bool a = BizonAmphibDeviation, b = BigTankWreckDeviation;
            public BigTankGroundOffHold() { BizonAmphibDeviation = false; BigTankWreckDeviation = false; }
            public void Dispose() { BizonAmphibDeviation = a; BigTankWreckDeviation = b; }
        }

        /// <summary>Statistics (the counted check): ticks a Bizon drove on water / lava, rams refused for a wet tank or target, wrecks crushed.</summary>
        public static int BizonWadeTicks, BizonWetRamRefused, BigTankWreckCrushes;

        readonly List<ZObject> crushWrecks = new List<ZObject>();
        /// <summary>The vehicle wrecks the big tanks may crush (read only, checks).</summary>
        public IReadOnlyList<ZObject> CrushableWrecks => crushWrecks;

        /// <summary>The grid kind the rules read for unit o: GridAmphib for a Bizon (DV192 + DV181), GridHeavy for a heavy tank / Mammoth / Bizon
        /// while a crushable wreck stands on land (DV193), else Zod's.</summary>
        public int GridKindOf(ZObject o)
        {
            if (o == null || o.Kind != ZKind.Vehicle) return ZPathFinder.GridZod;
            if (BizonAmphibDeviation && o.Type == ZBigTanks.Bizon && BizonDeviation) return ZPathFinder.GridAmphib;
            if (BigTankWreckDeviation && Path.HeavyGridDiffers && WreckCrusherType(o.Type)) return ZPathFinder.GridHeavy;
            return ZPathFinder.GridZod;
        }

        /// <summary>DV193: the heavy tank (v1 / v2) and the two big tanks with their own switch on.</summary>
        public static bool WreckCrusherType(string type) => type == ZBigTanks.Heavy || (ZBigTanks.Is(type) && ZBigTanks.On(type));

        /// <summary>Tick hook (the objects' loop): o's ProcessServer, inside its grid kind; then a wreck its box touches is crushed.</summary>
        void ProcessServerKind(ZObject o)
        {
            int k = GridKindOf(o);
            if (k == ZPathFinder.GridZod) { o.ProcessServer(Time); return; }
            using (Path.Kind(k)) o.ProcessServer(Time);
            if (BigTankWreckDeviation && crushWrecks.Count > 0 && WreckCrusherType(o.Type)) BigTankWreckContact(o);
        }

        /// <summary>InitPathfinding hook, after Zod's wide weights: the lava tiles open in the Bizon's grid, its 2x2 weights.</summary>
        void BigTankGridsInit()
        {
            if (Map.HasLava)
                for (int j = 0; j < Map.height; j++)
                    for (int i = 0; i < Map.width; i++)
                        if (Map.LavaAt(i, j)) Path.KindMarkLava(i, j);
            Path.KindWide();
        }

        /// <summary>WreckImpassables hook: a vehicle wreck's footprint opens for the crushing tanks (DV193); cleared, it leaves the list (its tiles
        /// get their terrain back through SetTileInfo).</summary>
        void BigTankWreckTiles(ZObject o, bool set)
        {
            if (o == null || !o.VehicleWreck) return;
            if (!set) { crushWrecks.Remove(o); return; }
            if (!BigTankWreckDeviation) return;
            int tx = o.LocX / 16, ty = o.LocY / 16, w = Math.Max(1, o.WidthPix / 16), h = Math.Max(1, o.HeightPix / 16);
            for (int i = tx; i < tx + w; i++)
                for (int j = ty; j < ty + h; j++) Path.KindOpenWreckTile(i, j);
            if (!crushWrecks.Contains(o)) crushWrecks.Add(o);
        }

        /// <summary>DV193: every live crushable wreck under the tank's box (shrunk by 1 px) is crushed now: destroyed by the game's death path,
        /// the tank as the attacker (no kill credit: the wreck is neutral; no debris shell: not a map object).</summary>
        void BigTankWreckContact(ZObject o)
        {
            if (o.Destroyed || o.KillAt >= 0) return;
            int x0 = o.LocX + 1, y0 = o.LocY + 1, x1 = o.LocX + o.WidthPix - 1, y1 = o.LocY + o.HeightPix - 1;
            for (int k = 0; k < crushWrecks.Count; k++)
            {
                var w = crushWrecks[k];
                if (w.Destroyed || w.KillAt >= 0) continue;
                if (x1 <= w.LocX || y1 <= w.LocY || x0 >= w.LocX + w.WidthPix || y0 >= w.LocY + w.HeightPix) continue;
                w.SetHealth(0); BigTankWreckCrushes++;
                UpdateObjectHealth(w, o.Id);
            }
        }

        /// <summary>DV192: water (any class of it) or lava under the pixel.</summary>
        public bool WetTile(int px, int py)
        {
            if (px < 0 || py < 0) return false;
            int tx = px >> 4, ty = py >> 4;
            var c = Map.ClassAt(tx, ty);
            return c == ZClass.Water || c == ZClass.DeepWater || Map.LavaAt(tx, ty);
        }

        /// <summary>DV192: a water or lava tile under the unit's box (shrunk by 1 px).</summary>
        public bool WetBox(ZObject o)
        {
            if (o == null) return false;
            int x0 = (o.LocX + 1) >> 4, y0 = (o.LocY + 1) >> 4, x1 = (o.LocX + o.WidthPix - 2) >> 4, y1 = (o.LocY + o.HeightPix - 2) >> 4;
            for (int j = y0; j <= y1; j++)
                for (int i = x0; i <= x1; i++)
                {
                    var c = Map.ClassAt(i, j);
                    if (c == ZClass.Water || c == ZClass.DeepWater || Map.LavaAt(i, j)) return true;
                }
            return false;
        }
    }

    public partial class ZObject
    {
        /// <summary>DV193: a vehicle wreck (DV39's harbour car wrecks, the checks' test wreck) the heavy tank, the Bizon and the Mammoth crush.
        /// Never set on the Unit editor's holo obstacle.</summary>
        public bool VehicleWreck;
        /// <summary>DV192 amended (N12b): ticks this Bizon has been in lava without a break (0 off it; the view's glow), the HP lava has taken from it in
        /// all (never goes down: the view's char), the burn's fixed-point remainder (x BizonLavaDieTicks).</summary>
        public int BizonLavaTicks, BizonLavaTaken;
        internal long BizonHeatAcc;

        /// <summary>ProcessMoveCore hook (the tile pace): a Bizon in its own grid on a water / lava tile drives at ZWorld.BizonWadePace.</summary>
        internal double BizonWadePace(double tile)
        {
            if (World == null || World.Path.GridKind != ZPathFinder.GridAmphib || Type != ZBigTanks.Bizon || !World.WetTile(CenterX, CenterY)) return tile;
            ZWorld.BizonWadeTicks++;
            return ZWorld.BizonWadePace;
        }

        /// <summary>RamTargetOk hook (DV192): no ram / crush start or drive while this tank or its target stands in water or lava.</summary>
        bool RamWet(ZObject t)
        {
            if (!ZWorld.BizonAmphibDeviation || World == null || t == null) return false;
            if (!World.WetBox(this) && !World.WetBox(t)) return false;
            ZWorld.BizonWetRamRefused++;
            return true;
        }
    }
}
