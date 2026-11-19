using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit level18_rules: the counted check of level 18's rule rows (Sim/ZWorld.L18Rules.cs, Sim/ZBot.BigTanks.cs), headless, on the level's
    /// own map file. Each row's switches are set here (the rest as OzV11Kit.GameSwitches has them); nothing is left changed.
    ///  DV198  the default (ZWorld.L18BigTanksEmpty): the Mammoth on blue's heavy tank's entry and the Bizon on red's medium tank's entry EMPTY
    ///         (neutral, no driver, enterable) in the map tanks' places and ids, each replaced tank's driver (a grunt of its side) standing beside it;
    ///         off: the map's heavy and medium tank; the rest of the start set as before; the crewed variant (the constant false): each its side's
    ///         own tank with a psycho driver, no robot beside it.
    ///  gaps   every start vehicle as the game starts it (five starts: no head start; red / blue with the SHUTTLE box on and off): every pair's
    ///  DV197  30 footprint tiles closed (all shallow water), none off; the robots' wade fort to fort the same length; no region split; fire not barred.
    ///  DV199  every bridge down: the computer's Bizon (it wades) is offered a flag across the river, its Mammoth no flag it cannot drive to
    ///         (Zod's regions would offer it), a heavy tank keeps Zod's answer; neither big tank is in any build list (never built). The repair
    ///         trip give-up (ZBot.BigTankTripStall) is proven by the kit's bot rows only (proof/bot_rows_L18.txt), not here.
    ///  DV200  the SHUTTLE box on level 18: a crewed ship of the player's team by his fort for red and for blue, the room round it kept, no water or
    ///         bridge under it, the head start parked beside it; the box off or HARD: none; the switch off: none; the box's rule table.
    ///  bots   two computers 120 game seconds on level 18 with every row on: the same fingerprint twice in one process.
    ///  r3     kit bizon_far_r3 (the owner's Bizon notes and picture of 13 Nov): the EMPTY Bizon is made from the map's RIGHT bottom neutral
    ///         crane (83, 110): its tile and id, that crane gone, three cranes left (one or more in each half); far from both forts, off lanes,
    ///         bridges, flags, repair bays, container footprints and night posts, both sides' robots reach it, it drives to both gates; red keeps
    ///         its own crewed medium tank, red's grunt by the red fort.
    ///         sides' robots can walk to it and it can drive to the blue fort, its replaced driver by the blue fort (DV198); part "cranes": no crane
    ///         in either side's start set in any option combination, an EMPTY neutral crane in each half far from the forts, off the bridges and
    ///         door lanes, that its side's vehicles can reach; "gaps": the four option combinations of both sides and the robot squads.
    /// Menus: OpenZ/Checks/V1.5/L18 rules (no Play); the parity rows DV197-DV200 run <see cref="Run"/> (OpenZParity.V15L18Rules.cs).
    /// </summary>
    public static class OzV15L18RulesCount
    {
        static readonly string[] Rows = { "L18StumpsDeviation", "L18BigTanksDeviation", "BotBigTanksDeviation", "L18ShuttleDeviation" };

        static void On(bool v) { foreach (var r in Rows) OzV11Kit.TrySet(typeof(ZWorld), r, v); }
        static void Big(bool v)
        {
            foreach (var r in new[] { "MammothDeviation", "BizonDeviation", "BizonAmphibDeviation", "MammothRamDeviation", "BizonCrushDeviation" }) OzV11Kit.TrySet(typeof(ZWorld), r, v);
        }
        static ZObject At(ZWorld w, int tx, int ty) => w.Objects.FirstOrDefault(o => o.Kind == ZKind.Vehicle && o.Source != null && o.Source.x == tx && o.Source.y == ty);

        /// <summary>Shortest way in tiles by the game's own step rule (the grids as they are now) from a to b; -1 = none.</summary>
        static double Way(ZWorld w, (int x, int y) a, (int x, int y) b, bool robot)
        {
            int W = w.Map.width, H = w.Map.height; var d = new double[W, H];
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) d[x, y] = double.MaxValue;
            var open = new SortedSet<(double, int, int)>(); d[a.x, a.y] = 0; open.Add((0, a.x, a.y));
            while (open.Count > 0)
            {
                var (c, x, y) = open.Min; open.Remove(open.Min);
                if (c > d[x, y]) continue;
                if (x == b.x && y == b.y) return c;
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                    if (!w.Path.StepOk(x, y, nx, ny, robot, false)) continue;
                    double nc = c + (dx != 0 && dy != 0 ? 1.41421356 : 1.0);
                    if (nc < d[nx, ny]) { d[nx, ny] = nc; open.Add((nc, nx, ny)); }
                }
            }
            return -1;
        }

        /// <summary>The whole check: PASS / FAIL lines (part "all", "DV197", "DV198", "DV199", "DV200", "gaps"). nightJson: level 18's night posts
        /// (Resources/OpenZ/Night/L18_night.json), the parking keep-out discs the game sets (null: none).</summary>
        public static string Run(string settings, string map18, string part = "all", string nightJson = null)
        {
            var sb = new StringBuilder();
            void Check(bool ok, string what) => sb.AppendLine(OzV11Kit.Verdict(ok, what));
            bool all = part == "all";
            bool wasShOn = ZWorld.ShuttleOn, wasPl = ZWorld.ShuttlePlayableDeviation, wasSide = ZWorld.SideShuttlesDeviation, wasSt = ZWorld.L18StartShuttle, wasEmpty = ZWorld.L18BigTanksEmpty;
            var wasTeam = ZWorld.ShuttleTeam;
            try
            {
                Big(true); On(true);
                ZWorld L18(int seed = 1) => ZWorld.Create(settings, map18, seed, false);
                var w = L18();
                if (!w.L18Map) { Check(false, "the map is not level 18's (" + w.Map.file + ")"); return sb.ToString(); }
                ZWorld w0; using (ZWorld.L18RulesOff()) w0 = L18();
                if (all || part == "DV198")
                {
                    var sm = ZWorld.L18BigTankSpots[0]; var sb2 = ZWorld.L18BigTankSpots[1];
                    var m = At(w, sm.tx, sm.ty); var b = At(w, sb2.tx, sb2.ty); var m0 = At(w0, sm.tx, sm.ty); var b0 = At(w0, sb2.tx, sb2.ty);
                    Check(m != null && m.Type == ZBigTanks.Mammoth && m.Owner == Team.Neutral && m.Drivers.Count == 0 && m.CanBeEntered() && m.LocX == sm.tx * 16 && m.LocY == sm.ty * 16,
                          $"DV198 tile ({sm.tx},{sm.ty}): {m?.Type} {(m?.Owner == Team.Neutral ? "EMPTY" : "of " + m?.Owner)}, {m?.Drivers.Count} driver, enterable {m?.CanBeEntered()} (the default: an EMPTY MAMMOTH, neutral, in blue's medium tank's place: blue_start_v2, moved to the top-left by blue_start_v3)");
                    Check(b != null && b.Type == ZBigTanks.Bizon && b.Owner == Team.Neutral && b.Drivers.Count == 0 && b.CanBeEntered() && b.LocX == ZWorld.L18BigTankTile(sb2.tx, sb2.ty).x * 16 && b.LocY == ZWorld.L18BigTankTile(sb2.tx, sb2.ty).y * 16,   /* kit l18_map_fixes_r3: on its moved tile */
                          $"DV198 tile ({sb2.tx},{sb2.ty}): {b?.Type} {(b?.Owner == Team.Neutral ? "EMPTY" : "of " + b?.Owner)}, {b?.Drivers.Count} driver, enterable {b?.CanBeEntered()} (the default: an EMPTY BIZON, neutral, on the map's RIGHT bottom neutral crane's entry and place, that crane gone: bizon_far_r3)");
                    var dm = w.L18BigTankDrivers.FirstOrDefault(r => r.Owner == Team.Blue); var db = w.L18BigTankDrivers.FirstOrDefault(r => r.Owner == Team.Red);
                    bool Beside(ZObject r, ZObject t) => r != null && t != null && Math.Abs(r.CenterX - t.CenterX) <= 64 && Math.Abs(r.CenterY - t.CenterY) <= 64;
                    var bfort = w.Objects.FirstOrDefault(o => o.IsFort && o.Owner == Team.Blue);
                    double toBlueFort = dm != null && bfort != null ? TilesToBox(dm, bfort) : 99;
                    var rfort = w.Objects.FirstOrDefault(o => o.IsFort && o.Owner == Team.Red);
                    double toRedFort = db != null && rfort != null ? TilesToBox(db, rfort) : 99;   // bizon_far_r3: red's grunt stays by the red fort, the Bizon stands far away
                    Check(w.L18BigTankDrivers.Count == 2 && w.L18BigTankDriversRefused == 0 && dm?.Type == "grunt" && db?.Type == "grunt" && dm.Minions.Count == 0 && db.Minions.Count == 0 && toBlueFort <= 12 && !Beside(dm, m) && toRedFort <= 12 && !Beside(db, b) && db.LocX / 16 == ZWorld.L18BigTankSpots[1].dtx && db.LocY / 16 == ZWorld.L18BigTankSpots[1].dty,
                          $"DV198 the replaced tanks' drivers: {w.L18BigTankDrivers.Count} made, {w.L18BigTankDriversRefused} refused; blue's {dm?.Type} at tile ({dm?.LocX / 16},{dm?.LocY / 16}) by the blue fort ({toBlueFort:0.0} tiles from it; blue_start_v3: the Mammoth stands far away, really empty), red's {db?.Type} at ({db?.LocX / 16},{db?.LocY / 16}) by the red fort ({toRedFort:0.0} tiles from it, where it stood; bizon_far_r3: the Bizon stands far away, really empty) (one grunt of each side, no squad)");
                    Check(w.L18BigTanks.Count == 2 && m0?.Type == "medium" && b0?.Type == "crane" && b0.Owner == Team.Neutral && m0.Id == m?.Id && b0.Id == b?.Id && w.Objects.Count == w0.Objects.Count + 2,
                          $"DV198 the rows off: the map's {m0?.Type} / {b0?.Type}; the same ids ({m?.Id}, {b?.Id}); objects {w.Objects.Count} = {w0.Objects.Count} + the 2 drivers (made last: every other id the same)");
                    Check(w.Objects.Count(o => o.Type == "heavy") == 2 && w.Objects.Count(o => o.Type == "heavy" && o.Owner == Team.Blue && o.Drivers.Count > 0) == 1 && w.Objects.Count(o => o.Type == "medium") == 1 && w.Objects.Count(o => o.Type == "medium" && o.Owner == Team.Red && o.Drivers.Count == 1 && o.LocX == 53 * 16 && o.LocY == 88 * 16) == 1 && w.Objects.Count(o => o.Kind == ZKind.Vehicle) == w0.Objects.Count(o => o.Kind == ZKind.Vehicle),
                          "DV198 the rest of the start set unchanged (red's heavy tank, blue's own crewed heavy tank (blue_start_v2), red's OWN CREWED MEDIUM tank at (53,88) (bizon_far_r3), every other vehicle)");
                    ZWorld.L18BigTanksEmpty = false; var wc = L18(); ZWorld.L18BigTanksEmpty = wasEmpty;
                    var mc = At(wc, sm.tx, sm.ty); var bc = At(wc, sb2.tx, sb2.ty);
                    Check(mc?.Type == ZBigTanks.Mammoth && mc.Owner == Team.Blue && mc.Drivers.Count == 1 && mc.DriverType == ZBigTanks.DefaultDriver && bc?.Type == ZBigTanks.Bizon && bc.Owner == Team.Neutral && bc.Drivers.Count == 0 && bc.CanBeEntered()
                          && wc.L18BigTankDrivers.Count == 0 && wc.Objects.Count == w0.Objects.Count,
                          $"DV198 the crewed variant (ZWorld.L18BigTanksEmpty = false, not the default): the Mammoth of {mc?.Owner} crewed by a {mc?.DriverType}; the Bizon on the right bottom crane's neutral entry (bizon_far_r3) {(bc?.Owner == Team.Neutral && bc?.Drivers.Count == 0 ? "EMPTY" : "of " + bc?.Owner)}; no robot added ({wc.L18BigTankDrivers.Count})");
                    if (m != null)
                    {
                        var forts = w.Objects.Where(o => o.IsFort && !o.Destroyed).ToList();
                        double far = forts.Count == 0 ? 0 : forts.Min(f => TilesToBox(m, f));
                        bool nw = m.CenterX < w.Map.width * 16 / 3 && m.CenterY < w.Map.height * 16 / 4;
                        int road = RoadTilesNear(w, m, MammothRoadTiles);
                        double flag = NearestM(m, w.Objects.Where(o => o.Kind == ZKind.MapItem && o.Type == "flag")), bay = NearestM(m, w.Objects.Where(o => o.Type == "repair")), brg = NearestM(m, w.Objects.Where(o => o.IsBridge));
                        var mt = (m.LocX / 16, m.LocY / 16);
                        double rb = Way(w, (62, 47), mt, true), rr = Way(w, (62, 74), mt, true), vb; using (w.Path.Kind(ZPathFinder.GridHeavy)) vb = Way(w, mt, (62, 47), false);
                        Check(far >= MammothFarTiles && nw && road > 0 && flag >= 4 && bay >= 10 && brg >= 10 && rb > 0 && rr > 0 && vb > 0,
                              $"DV198 the EMPTY Mammoth at ({mt.Item1},{mt.Item2}) (owner addition 48: far from both forts, top-left): {far:0} tiles from the nearest fort ({MammothFarTiles} wanted), in the map's north-west part {nw}, road tiles within {MammothRoadTiles} tiles of it {road}; nearest flag {flag:0.0} m, repair bay {bay:0.0} m, bridge {brg:0.0} m; robots walk to it from the blue gate in {rb:0} tiles, from the red gate in {rr:0} (both sides reach it); it drives to the blue gate in {vb:0} tiles with the start's bridges");
                    }
                    if (b != null)
                    {   // bizon_far_r3 (the owner's picture of 13 Nov): the EMPTY
                        // Bizon IS the map's right bottom neutral crane entry (its tile and id; that crane gone, the other three stay, one or more in each half), far from both
                        // forts in the south (red's) half, not on a lane, bridge, flag, repair spot, container footprint or night post; both sides' robots reach it, it drives to both gates
                        var c0 = At(w0, sb2.tx, sb2.ty);
                        var left = w0.Objects.Where(o => o.Kind == ZKind.Vehicle && o.Type == "crane" && o.Source != null && o.CenterY / 16 >= 59 && o != c0).ToList();   // the other bottom crane(s) in Z's file
                        bool right = c0 != null && left.Count > 0 && left.All(o => o.CenterX < c0.CenterX);
                        int cranesOn = w.Objects.Count(o => o.Kind == ZKind.Vehicle && o.Type == "crane"), cranesOff = w0.Objects.Count(o => o.Kind == ZKind.Vehicle && o.Type == "crane");
                        int north = w.Objects.Count(o => o.Kind == ZKind.Vehicle && o.Type == "crane" && o.Owner == Team.Neutral && o.CenterY / 16 < 59), south = cranesOn - north;
                        var forts = w.Objects.Where(o => o.IsFort && !o.Destroyed).ToList();
                        double far = forts.Count == 0 ? 0 : forts.Min(f => TilesToBox(b, f));
                        bool inSouth = b.CenterY / 16 >= 59;
                        int road = RoadTilesNear(w, b, MammothRoadTiles);
                        double flag = NearestM(b, w.Objects.Where(o => o.Kind == ZKind.MapItem && o.Type == "flag")), bay = NearestM(b, w.Objects.Where(o => o.Type == "repair")), brg = NearestM(b, w.Objects.Where(o => o.IsBridge));
                        bool wasCont = ZWorld.L18ContainersDeviation; ZWorld.L18ContainersDeviation = true; var wk = L18(); ZWorld.L18ContainersDeviation = wasCont;
                        var bk = At(wk, sb2.tx, sb2.ty); double cont = bk == null ? -1 : NearestM(bk, wk.L18Containers);   // DV201's footprints as the game makes them
                        bool lane = false;
                        for (int py = b.LocY - 16; py <= b.LocY + b.HeightPix + 16; py += 8) for (int px = b.LocX - 16; px <= b.LocX + b.WidthPix + 16; px += 8) if (w.InExitLane(px, py, 0)) lane = true;
                        double lamp = 99; if (!string.IsNullOrEmpty(nightJson)) foreach (var k in KeepOut(nightJson)) lamp = Math.Min(lamp, (Math.Sqrt((double)(k.x - b.CenterX) * (k.x - b.CenterX) + (double)(k.y - b.CenterY) * (k.y - b.CenterY)) - k.r) / 8.0 - BizonHalfM);
                        var bt = (b.LocX / 16, b.LocY / 16);
                        double rb = Way(w, (62, 47), bt, true), rr = Way(w, (62, 74), bt, true), va, vh, vb;
                        using (w.Path.Kind(ZPathFinder.GridAmphib)) { va = Way(w, bt, (62, 74), false); vb = Way(w, bt, (62, 47), false); }
                        using (w.Path.Kind(ZPathFinder.GridHeavy)) vh = Way(w, bt, (62, 74), false);
                        Check(c0 != null && c0.Type == "crane" && c0.Owner == Team.Neutral && c0.Id == b.Id && ZWorld.L18BigTankTile(c0.LocX / 16, c0.LocY / 16) == (b.LocX / 16, b.LocY / 16) /* kit l18_map_fixes_r3: the crane's entry, the Bizon on its moved tile */ && right && cranesOff == 4 && cranesOn == 3 && north >= 1 && south >= 1
                              && far >= CraneFarTiles && inSouth && flag >= 4 && bay >= 10 && brg >= 10 && cont >= 6 && (!lane || ZWorld.L18BigTankTile(sb2.tx, sb2.ty) != (sb2.tx, sb2.ty)) /* kit l18_map_fixes_r3: the owner's spot is in front of the factory door; the R3.41 line counts that robots get out */ && lamp >= 0 && rb > 0 && rr > 0 && va > 0 && vh > 0 && vb > 0,
                              $"DV198 the EMPTY Bizon at ({bt.Item1},{bt.Item2}) (owner 13 Nov, his picture: on the RIGHT one of the two bottom cranes' spot, that crane gone): the map's entry there with the rows off: {c0?.Type} of {c0?.Owner} #{c0?.Id} at ({c0?.LocX / 16},{c0?.LocY / 16}) (the Bizon #{b.Id}: the same id and tile), the right one of the bottom cranes {right} (the other: {string.Join(", ", left.Select(o => $"({o.LocX / 16},{o.LocY / 16})"))}); neutral cranes {cranesOn} with the rows on ({north} north, {south} south), {cranesOff} off; {far:0} tiles from the nearest fort ({CraneFarTiles} wanted), in the south half {inSouth}; road tiles within {MammothRoadTiles} tiles {road}; nearest flag {flag:0.0} m, repair bay {bay:0.0} m, bridge {brg:0.0} m, container footprint {cont:0.0} m, night post {lamp:0.0} m clear of the hull; in a door lane {lane}; robots walk to it from the blue gate in {rb:0} tiles, from the red gate in {rr:0}; it drives to the red gate in {va:0} tiles ({vh:0} on the heavy tanks' dry grid), to the blue gate in {vb:0} (it wades the river)");
                        {
                            var r341mv = ZWorld.L18BigTankTile(sb2.tx, sb2.ty);
                            int r341side = 0, r341road = 0;
                            for (int r341x = b.LocX / 16; r341x < (b.LocX + b.WidthPix + 15) / 16; r341x++)
                            {
                                if ((w.Map.ClassAt(r341x, b.LocY / 16 - 2) == ZClass.Road && w.Map.ClassAt(r341x, b.LocY / 16 - 1) == ZClass.Road) || (w.Map.ClassAt(r341x, (b.LocY + b.HeightPix) / 16 + 1) == ZClass.Road && w.Map.ClassAt(r341x, (b.LocY + b.HeightPix) / 16) == ZClass.Road)) r341side++;   // a side road meets here = a crossing
                                for (int r341y = b.LocY / 16; r341y < (b.LocY + b.HeightPix + 15) / 16; r341y++) if (w.Map.ClassAt(r341x, r341y) == ZClass.Road) r341road++;
                            }
                            var r341fac = w.Objects.Where(o => o.Kind == ZKind.Building && (o.Type == "robot_factory" || o.Type == "vehicle_factory")).OrderBy(o => NearestM(b, new[] { o })).FirstOrDefault();
                            double r341facM = r341fac == null ? 99 : NearestM(b, new[] { r341fac });
                            var r341hb = Box(b, false); double r341len = r341hb.x1 - r341hb.x0, r341wid = r341hb.z1 - r341hb.z0;
                            Check(r341mv == (78, 110) && b.LocX == r341mv.x * 16 && b.LocY == r341mv.y * 16 && r341road > 0 && r341side == 0 && r341facM >= R341StripM && r341facM <= 8 && Math.Abs(b.FaceX) > 0.5f && Math.Abs(b.FaceY) < 0.01f && r341len > r341wid,
                                  $"R3.41 the EMPTY Bizon in the owner's red box: tile ({b.LocX / 16},{b.LocY / 16}) (made from the crane's entry ({sb2.tx},{sb2.ty})), road tiles under it {r341road} (on the road), crossing side roads at its columns {r341side} (0 = off the crossing), nearest factory {r341fac?.Type} at ({r341fac?.LocX / 16},{r341fac?.LocY / 16}) {r341facM:0.0} m (in front of it), facing ({b.FaceX:0},{b.FaceY:0}) (along the east-west road); drawn hull {r341len:0.0} m along the road x {r341wid:0.0} m; a free strip of {r341facM:0.0} m between the factory front and the hull ({R341StripM} m or more: robots from the door walk out along it, the door is not sealed)");
                        }
                    }
                    Big(false); var wb = L18(); Big(true);
                    Check(At(wb, sm.tx, sm.ty)?.Type == "medium" && At(wb, sb2.tx, sb2.ty)?.Type == "crane" && wb.L18BigTankDrivers.Count == 0, "DV198 with the big tanks' own switches off (DV180 / DV181): the map's own medium tank and crane (bizon_far_r3), no robot added");
                }
                if (all || part == "DV197")
                {
                    int closed = 0, water = 0, open0 = 0; var t4 = ZWorld.L18StumpTiles;
                    for (int k = 0; k < t4.Length; k += 4)
                        for (int i = t4[k]; i < t4[k] + t4[k + 2]; i++)
                            for (int j = t4[k + 1]; j < t4[k + 1] + t4[k + 3]; j++)
                            { if (!w.Path.TilePassable(i, j, true)) closed++; if (w.Map.ClassAt(i, j) == ZClass.Water) water++; if (w0.Path.TilePassable(i, j, true)) open0++; }
                    int oldOpen = 0, oldWater = 0; int[] old4 = { 50, 58, 5, 3, 72, 57, 3, 5 };
                    for (int k = 0; k < old4.Length; k += 4)
                        for (int i = old4[k]; i < old4[k] + old4[k + 2]; i++)
                            for (int j = old4[k + 1]; j < old4[k + 1] + old4[k + 3]; j++)
                            { if (w.Path.TilePassable(i, j, true)) oldOpen++; if (w.Map.ClassAt(i, j) == ZClass.Water) oldWater++; }
                    Check(t4.Length == 0 && w.L18Stumps.Count == 0 && w.L18StumpsRefused == 0 && closed == 0 && oldOpen == 30 && oldWater == 30, $"DV197 (fixes_r5: view only at the map edge) tile set {t4.Length / 4} footprints, {w.L18Stumps.Count} stumps in the rules, {closed} tiles closed; the round 4 footprints: {oldOpen} of 30 tiles open ({oldWater} shallow water)");
                    double a = Way(w, (62, 47), (62, 74), true), a0 = Way(w0, (62, 47), (62, 74), true);
                    Check(a > 0 && Math.Abs(a - a0) < 0.01, $"DV197 robots wade gate to gate {a:0.0} tiles with the row on, {a0:0.0} with it off (the same)");
                    double p = Way(w, (44, 59), (60, 59), true), p0 = Way(w0, (44, 59), (60, 59), true);
                    Check(p > 0 && Math.Abs(p - p0) < 0.01, $"DV197 across the west pool {p:0.0} tiles with the row on, {p0:0.0} with it off (the same: as before the stumps)");
                    Check(!w.EngageBarrierBetween(52 * 16 + 8, 55 * 16 + 8, 52 * 16 + 8, 63 * 16 + 8), "DV197 a line of fire over a stump is not barred");
                    double bz; using (w.Path.Kind(ZPathFinder.GridAmphib)) bz = Way(w, (56, 78), (56, 50), false);
                    Check(bz > 0, $"DV197 the Bizon's own grid still crosses the river ({bz:0.0} tiles from the red fort's north side)");
                }
                if (all || part == "DV199")
                {
                    // every bridge down (the north-east one blown too): no vehicle way between the two halves, the Bizon wades
                    var wd = L18();
                    foreach (var br in wd.Objects.Where(o => o.IsBridge && !o.Destroyed).ToList()) br.SetHealth(0);
                    wd.Tick(ZConst.TICK);
                    var red = new ZBot(wd, Team.Red, 1); var blue = new ZBot(wd, Team.Blue, 101);
                    var bz = At(wd, ZWorld.L18BigTankSpots[1].tx, ZWorld.L18BigTankSpots[1].ty); var mm = At(wd, ZWorld.L18BigTankSpots[0].tx, ZWorld.L18BigTankSpots[0].ty); var hv = wd.Objects.First(o => o.Type == "heavy" && o.Owner == Team.Red);
                    var fNorth = wd.Flags.First(f => f.LocX / 16 == 54 && f.LocY / 16 == 17);   // zone Z1, blue's half
                    var fSouth = wd.Flags.First(f => f.LocX / 16 == 77 && f.LocY / 16 == 98);   // zone Z12, red's half
                    bool Offer(ZBot b, ZObject u, ZObject f) { b.MatchTargets_3(new List<ZObject> { u }, new List<ZObject> { f }); return b.Candidates(u).Contains(f); }
                    bool bOn = Offer(red, bz, fNorth), mOn = Offer(blue, mm, fSouth), hOn = Offer(red, hv, fNorth), mOwn = Offer(blue, mm, wd.Flags.First(f => f.LocX / 16 == 75 && f.LocY / 16 == 18));
                    ZWorld.BotBigTanksDeviation = false;
                    bool bOff = Offer(red, bz, fNorth), mOff = Offer(blue, mm, fSouth), hOff = Offer(red, hv, fNorth);
                    ZWorld.BotBigTanksDeviation = true;
                    Check(bOn && bOff, $"DV199 every bridge down: the computer's Bizon (it wades) is offered a flag across the river (54,17): row on {bOn}, off {bOff}");
                    Check(!mOn && mOff && mOwn, $"DV199 every bridge down: the computer's Mammoth is NOT offered a flag it cannot drive to (77,98): row on {mOn} (Zod's regions: {mOff}); a flag on its own half (75,18): {mOwn}");
                    Check(hOn == hOff && hOn, $"DV199 a heavy tank keeps Zod's answer (the row is the big tanks' only): on {hOn}, off {hOff}");
                    var kinds = new[] { "fort_front", "fort_back", "robot_factory", "vehicle_factory" }; int listed = 0;
                    foreach (var k in kinds) for (int lv = 0; lv <= 6; lv++) foreach (var u in ZBigTanks.Types) if (ZBuildLists.InList(k, lv, u)) listed++;
                    Check(listed == 0, $"DV199 the Mammoth and the Bizon in a build list: {listed} times (0: the computer never builds one)");
                }
                if (all || part == "DV200")
                {
                    ZWorld.ShuttlePlayableDeviation = true; ZWorld.SideShuttlesDeviation = true; ZWorld.ShuttleOn = true;
                    foreach (var team in new[] { Team.Red, Team.Blue })
                    {
                        ZWorld.ShuttleTeam = team; ZWorld.L18StartShuttle = true;
                        var ws = L18(); var s = ws.L18FortShuttle;
                        Check(s != null && s.Owner == team && s.Drivers.Count == 1 && ws.PlayableShuttle == s && ws.L18ShuttleWhy == "",
                              $"DV200 {team}: a crewed ship of {team} by its fort, the playable one, on its own spot: {(s == null ? "none (" + ws.L18ShuttleWhy + ")" : $"#{s.Id} at tile ({s.CenterX / 16},{s.CenterY / 16}) heading {s.ShuttleHeadingDeg:0}")}");
                        if (s == null) continue;
                        string bad = ws.ShuttleStartRoom(s, out string near);
                        int wat = 0, brg = 0;
                        for (int y = s.CenterY - 40; y <= s.CenterY + 40; y += 4)
                            for (int x = s.CenterX - 40; x <= s.CenterX + 40; x += 4)
                            {
                                if (s.ShuttleOutlineGapM(x, y) > 0) continue;
                                var c = ws.Map.ClassAt(x >> 4, y >> 4); if (c == ZClass.Water || c == ZClass.DeepWater) wat++;
                                foreach (var o in ws.Objects) if (o.IsBridge && x >= o.LocX && x < o.LocX + o.WidthPix && y >= o.LocY && y < o.LocY + o.HeightPix) brg++;
                            }
                        Check(bad.Length == 0 && wat == 0 && brg == 0, $"DV200 {team}: the room round it {(bad.Length == 0 ? "kept" : bad)}; water points under it {wat}, bridge points {brg}; {near}");
                        var hs = ws.AddHeadStart(team, ZWorld.L18HeadStartTypes);
                        string bad2 = ws.ShuttleStartRoom(s, out _);
                        Check(hs.Count == 3 && bad2.Length == 0, $"DV200 {team}: the level 18 head start parks {hs.Count} of 3 beside it, the room after it {(bad2.Length == 0 ? "kept" : bad2)}");
                        ZWorld.L18StartShuttle = false; var wn = L18(); ZWorld.L18StartShuttle = true;
                        Check(wn.L18FortShuttle == null && !wn.Objects.Any(o => o.IsShuttle), $"DV200 {team}: the box off (or HARD / MAYHEM): no shuttle on level 18");
                        bool st; using (ZWorld.L18RulesOff()) { ZWorld.L18StartShuttle = true; st = L18().Objects.Any(o => o.IsShuttle); }
                        Check(!st, $"DV200 {team}: the row off ('Zod exact'): no shuttle");
                    }
                }
                if (all || part == "cranes" || part == "DV198")
                {
                    int owned = OwnedCranesAtStart(settings, map18, nightJson, out string ownedAt);
                    Check(owned == 0, $"cranes (owner addition 49): cranes a side owns at the start, the four option combinations of both sides and the computer's start (head start = {string.Join(", ", ZWorld.L18HeadStartTypes)}): {owned}{ownedAt}");
                    var forts = w.Objects.Where(o => o.IsFort && !o.Destroyed).ToList();
                    foreach (var half in new[] { Team.Blue, Team.Red })
                    {
                        var gate = half == Team.Blue ? (62, 47) : (62, 74);
                        var good = new List<string>(); var seen = new List<string>();
                        foreach (var c in w.Objects.Where(o => o.Kind == ZKind.Vehicle && o.Type == "crane" && !o.Destroyed))
                        {
                            bool north = c.CenterY / 16 < 59;                                       // the river's middle row
                            if (north != (half == Team.Blue)) continue;
                            double fd = forts.Count == 0 ? 0 : forts.Min(f => TilesToBox(c, f));
                            bool onBridge = w.Objects.Any(b => b.IsBridge && c.CenterX >= b.LocX && c.CenterX < b.LocX + b.WidthPix && c.CenterY >= b.LocY && c.CenterY < b.LocY + b.HeightPix);
                            bool lane = w.InExitLane(c.CenterX, c.CenterY, 0);
                            double way = Way(w, gate, (c.LocX / 16, c.LocY / 16), false);
                            bool ok = c.Owner == Team.Neutral && c.Drivers.Count == 0 && c.CanBeEntered() && fd >= CraneFarTiles && !onBridge && !lane && way > 0;
                            string line = $"crane#{c.Id} at ({c.LocX / 16},{c.LocY / 16}) {(c.Owner == Team.Neutral && c.Drivers.Count == 0 ? "EMPTY" : "of " + c.Owner)}, {fd:0} tiles from the nearest fort, {(onBridge ? "ON A BRIDGE" : "off the bridges")}, {(lane ? "IN A DOOR LANE" : "off the door lanes")}, {(way > 0 ? "driven from the " + half + " gate in " + way.ToString("0") + " tiles" : "NO way from the " + half + " gate")}";
                            seen.Add(line); if (ok) good.Add(line);
                        }
                        Check(good.Count >= 1, $"cranes (owner addition 49): an EMPTY neutral crane in the {(half == Team.Blue ? "blue (north)" : "red (south)")} half, {CraneFarTiles} tiles or more from both forts, off the bridges and door lanes, reachable by its side's vehicles: {good.Count} of {seen.Count}: {string.Join("; ", seen)}");
                    }
                }
                if (all || part == "gaps")
                {
                    string g = Gaps(settings, map18, nightJson, false, out bool gok, out _);
                    var worst = g.Split('\n').LastOrDefault(l => l.StartsWith("worst")) ?? "";
                    Check(gok, $"start vehicles and robot squads (owner additions 7 point I, 48: nine starts = the computer's + the four option combinations of each side, the night posts as the game's parking keep-out{(string.IsNullOrEmpty(nightJson) ? ": NONE GIVEN" : "")}): every pair's drawn hulls at least {GapHullM:0.0} m clear and whole models {GapAllM:0.0} m, every hull {ClearFortM:0.0} m from the forts and their wall tiles (mechs included), nothing in a wall, building, rock, post or door lane, the drivers clear of every hull; every robot squad {SquadGapM:0.0} m from every other and {GapHullM:0.0} m from every hull and the start ship; {worst.Trim()}");
                    if (!gok) foreach (var l in g.Split('\n')) if (l.Contains("BAD ")) sb.AppendLine("      " + l.Trim());
                }
                if (all)
                {
                    var lines = new List<string>();
                    for (int k = 0; k < 2; k++)
                    {
                        var wb = L18(3); wb.Bots.Add(new ZBot(wb, Team.Blue, ZBot.SeedFor(Team.Blue, 3))); wb.Bots.Add(new ZBot(wb, Team.Red, ZBot.SeedFor(Team.Red, 3)));
                        ulong h = 1469598103934665603UL;
                        for (int i = 0; i < 12000 && !wb.GameOver; i++) { wb.Tick(ZConst.TICK); wb.Events.Clear(); if (i % 100 == 99) h = wb.RulesFingerprint(h); }
                        var mm = At(wb, ZWorld.L18BigTankSpots[0].tx, ZWorld.L18BigTankSpots[0].ty); var bb = At(wb, ZWorld.L18BigTankSpots[1].tx, ZWorld.L18BigTankSpots[1].ty);
                        lines.Add($"{h:x16}, {wb.Objects.Count} objects, Mammoth at ({mm?.LocX / 16},{mm?.LocY / 16}) {mm?.Owner}, Bizon at ({bb?.LocX / 16},{bb?.LocY / 16}) {bb?.Owner}");
                    }
                    Check(lines[0] == lines[1], $"two computers 120 s on level 18 with every row on, twice in one process: {lines[0]} / {(lines[0] == lines[1] ? "the same" : lines[1])}");
                }
            }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message); }
            finally
            {
                ZWorld.ShuttleOn = wasShOn; ZWorld.ShuttlePlayableDeviation = wasPl; ZWorld.SideShuttlesDeviation = wasSide; ZWorld.L18StartShuttle = wasSt; ZWorld.ShuttleTeam = wasTeam; ZWorld.L18BigTanksEmpty = wasEmpty;
            }
            return sb.ToString();
        }

        /// <summary>The drawn size of every start vehicle (m, the model as the game draws it, unturned): hull x0, x1, z0, z1, then the whole model
        /// (turret and barrel) x0, x1, z0, z1. Measured 12 Nov 2026 by OzV15L18RulesMenu.DrawnSizes (menu OpenZ/L18 Bridge Game/11, batch Unity
        /// 6000.3.27f2 on the project copy: proof/copy_batch_L18_rules.txt): the union of each prefab's enabled LOD 0 mesh renderers.</summary>
        public static readonly Dictionary<string, double[]> DrawnSize = new Dictionary<string, double[]>
        {
            { "light", new[] { -1.74, 1.74, -1.64, 2.08, -1.74, 1.74, -2.07, 2.08 } },                   // Z_light_v2
            { "medium", new[] { -1.26, 2.05, -1.28, 1.10, -2.05, 2.05, -1.28, 1.10 } },                  // Z_medium_v2 (the mech)
            { "heavy", new[] { -2.12, 2.12, -2.60, 2.61, -2.12, 2.12, -3.73, 2.61 } },                   // Z_heavy_v2 (x of v1, the wider)
            { ZBigTanks.Mammoth, new[] { -2.50, 2.49, -3.86, 3.19, -2.50, 2.49, -3.86, 3.19 } },         // Z_mammoth
            { ZBigTanks.Bizon, new[] { -2.48, 2.49, -3.89, 3.59, -2.48, 2.49, -3.89, 3.59 } },           // Z_bizon
            { "crane", new[] { -1.32, 1.32, -2.40, 2.40, -1.32, 1.32, -2.40, 2.40 } },                   // Z_crane
            { "jeep", new[] { -1.56, 1.59, -2.10, 2.10, -1.56, 1.59, -2.10, 2.10 } },                    // Z_jeep
            { "apc", new[] { -1.57, 1.57, -2.40, 2.40, -1.57, 1.57, -2.40, 2.40 } },                     // Z_apc
            { "missile_launcher", new[] { -1.98, 1.98, -2.45, 2.45, -1.98, 1.98, -2.45, 2.45 } },        // Z_missile_launcher_v2 (x of v1, the wider)
        };
        /// <summary>The about 2 m clear between two drawn hulls; the whole models (barrels) at least <see cref="GapAllM"/>.</summary>
        public const double GapHullM = 2.0, GapAllM = 1.0;
        /// <summary>A drawn hull's least room to a building, cannon or map item (the fort wall), to a closed or water tile (a wall, a quay edge) and to a
        /// night post (m).</summary>
        public const double ClearBuildingM = 1.0, ClearTileM = 0.5, ClearPostM = 0.5;
        /// <summary>A drawn hull's least room to a FORT (its footprint) and to a closed tile within <see cref="FortWallTiles"/> tiles of a fort (its
        /// </summary>
        public const double ClearFortM = 2.0; public const int FortWallTiles = 3;

        /// <summary>A start vehicle's drawn box in the world (m, Unity x / z), the same both ways round (a start vehicle stands turned 0 or 180 deg:
        /// the scene's team turn, the head start unturned), so the box is the union of the two turns.</summary>
        static (double x0, double x1, double z0, double z1) Box(ZObject v, bool all)
        {
            double cx = v.CenterX / 8.0, cz = -v.CenterY / 8.0;
            if (!DrawnSize.TryGetValue(v.Type, out var s)) s = new[] { -2.0, 2.0, -2.0, 2.0, -2.0, 2.0, -2.0, 2.0 };
            int k = all ? 4 : 0;
            double hx = Math.Max(-s[k], s[k + 1]), hz = Math.Max(-s[k + 2], s[k + 3]);
            if (R341Waived(v)) { double t341 = hx; hx = hz; hz = t341; }
            return (cx - hx, cx + hx, cz - hz, cz + hz);
        }

        /// <summary>The clear distance between two boxes (m); negative = they overlap (by the smaller overlap).</summary>
        static double Clear((double x0, double x1, double z0, double z1) a, (double x0, double x1, double z0, double z1) b)
        {
            double dx = Math.Max(a.x0 - b.x1, b.x0 - a.x1), dz = Math.Max(a.z0 - b.z1, b.z0 - a.z1);
            if (dx < 0 && dz < 0) return Math.Max(dx, dz);
            return Math.Sqrt(Math.Max(dx, 0) * Math.Max(dx, 0) + Math.Max(dz, 0) * Math.Max(dz, 0));
        }

        static (double, double, double, double) PxBox(int x0, int y0, int x1, int y1) => (x0 / 8.0, x1 / 8.0, -y1 / 8.0, -y0 / 8.0);

        [Serializable] public class NightDoc { public NightLamp[] lamps; }
        [Serializable] public class NightLamp { public float x, z; }

        /// <summary>ZGame.SetParkingKeepOut's discs for level 18 (its scene has no DEC_ decor: the night posts only, 11 px each).</summary>
        public static List<(int x, int y, int r)> KeepOut(string nightJson)
        {
            var list = new List<(int, int, int)>();
            var d = string.IsNullOrEmpty(nightJson) ? null : UnityEngine.JsonUtility.FromJson<NightDoc>(nightJson);
            if (d?.lamps != null) foreach (var l in d.lamps) list.Add(((int)Math.Round((double)(l.x * 8f)), (int)Math.Round((double)(-l.z * 8f)), 11));
            return list;
        }

        /// <summary>
        /// Every start vehicle of level 18 as the game starts it (the rules on, the night posts as parking keep-out discs as ZGame sets them), five
        /// starts: no head start and no ship (HARD, MAYHEM, the computer's side), red EASY / MEDIUM with the SHUTTLE box on and off, blue the same
        /// (head start: crane, light tank, jeep). For each start every pair of vehicles within 30 m: the clear gap of the drawn hulls and of the whole
        /// models; each vehicle's clearance to buildings, map items (rocks), closed or water tiles (walls, quay edges), the night posts and every
        /// fort's door lane / gate face; the drivers of the empty big tanks clear of every hull. PASS: every hull pair at least GapHullM, every
        /// model pair at least GapAllM, every hull ClearBuildingM from buildings / cannons / items (ClearFortM = 2 m from a fort and its wall tiles, mechs included), ClearTileM from walls and water, ClearPostM from
        /// the night posts, out of every door lane and gate face; the start ship's outline GapHullM from every hull; no start robot in a hull.
        /// </summary>
        public static string Gaps(string settings, string map18, string nightJson, bool printAll, out bool pass, out List<string> pairLines)
        {
            var sb = new StringBuilder(); pairLines = new List<string>(); pass = true;
            var wasKeep = ZWorld.ParkingKeepOut; var wasKeepMap = ZWorld.ParkingKeepOutMap;
            bool wasShOn = ZWorld.ShuttleOn, wasPl = ZWorld.ShuttlePlayableDeviation, wasSide = ZWorld.SideShuttlesDeviation, wasSt = ZWorld.L18StartShuttle;
            var wasTeam = ZWorld.ShuttleTeam;
            try
            {
                ZWorld.ParkingKeepOut = KeepOut(nightJson);
                ZWorld.ParkingKeepOutMap = ZMapData.FromJson(map18).file;
                var starts = new (string name, Team player, bool box, bool head)[]
                {
                    ("no head start, no ship (HARD / MAYHEM / the computer)", Team.Neutral, false, false),
                    ("red, SHUTTLE box OFF, head start OFF", Team.Red, false, false),
                    ("red, EASY / MEDIUM, SHUTTLE box ON, head start OFF", Team.Red, true, false),
                    ("red, SHUTTLE box OFF, head start ON", Team.Red, false, true),
                    ("red, EASY / MEDIUM, SHUTTLE box ON, head start ON", Team.Red, true, true),
                    ("blue (PLAY AS BLUE), SHUTTLE box OFF, head start OFF", Team.Blue, false, false),
                    ("blue (PLAY AS BLUE), EASY / MEDIUM, SHUTTLE box ON, head start OFF", Team.Blue, true, false),
                    ("blue (PLAY AS BLUE), SHUTTLE box OFF, head start ON", Team.Blue, false, true),
                    ("blue (PLAY AS BLUE), EASY / MEDIUM, SHUTTLE box ON, head start ON", Team.Blue, true, true),
                };
                double worstHull = 99, worstAll = 99; string worstHullAt = "", worstAllAt = "";
                double worstSquad = 99, worstRobot = 99; string worstSquadAt = "", worstRobotAt = "";   // blue_start_v3: the robot squads
                foreach (var st in starts)
                {
                    ZWorld.ShuttlePlayableDeviation = true; ZWorld.SideShuttlesDeviation = true;
                    ZWorld.ShuttleOn = st.player != Team.Neutral; ZWorld.ShuttleTeam = st.player == Team.Neutral ? Team.Red : st.player; ZWorld.L18StartShuttle = st.box;
                    var w = ZWorld.Create(settings, map18, 1, false);
                    if (st.head) w.AddHeadStart(st.player, ZWorld.L18HeadStartTypes);
                    var veh = w.Objects.Where(o => o.Kind == ZKind.Vehicle && !o.Destroyed && !o.IsShuttle).ToList();
                    sb.AppendLine($"== {st.name}: {veh.Count} vehicles, {w.Objects.Count(o => o.IsShuttle)} ship, {ZWorld.ParkingKeepOut.Count} night post discs");
                    int bad = 0;
                    for (int i = 0; i < veh.Count; i++)
                        for (int j = i + 1; j < veh.Count; j++)
                        {
                            var a = veh[i]; var b = veh[j];
                            double ch = Clear(Box(a, false), Box(b, false)), ca = Clear(Box(a, true), Box(b, true));
                            if (ch > 30) continue;
                            bool ok = ch >= GapHullM - 1e-9 && ca >= GapAllM - 1e-9;
                            string line = $"{(ok ? "ok  " : "BAD ")} {Name(a)} - {Name(b)}: hulls {ch:0.00} m clear, whole models {ca:0.00} m";
                            if (!ok) { bad++; pass = false; }
                            if (ch < worstHull) { worstHull = ch; worstHullAt = st.name + ": " + Name(a) + " - " + Name(b); }
                            if (ca < worstAll) { worstAll = ca; worstAllAt = st.name + ": " + Name(a) + " - " + Name(b); }
                            if (st.player == Team.Neutral) pairLines.Add(line);
                            if (printAll || !ok || ch < 6) sb.AppendLine("  " + line);
                        }
                    foreach (var v in veh)
                    {
                        string why = Clearance(w, v, out double near, out string nearWhat);
                        if (why.Length > 0) { bad++; pass = false; }
                        if (printAll || why.Length > 0) sb.AppendLine($"  {(why.Length == 0 ? "ok  " : "BAD ")} {Name(v)}: {(why.Length == 0 ? "clear" : why)}; nearest {nearWhat} {near:0.00} m");
                    }
                    foreach (var s in w.Objects.Where(o => o.IsShuttle && !o.Destroyed))
                    {   // the start ship (DV200): its drawn outline (ZObject.ShuttleOutlineGapM) against every hull, sampled every 0.25 m round the hull
                        double smin = 99; string sn = "";
                        foreach (var v in veh)
                        {
                            var bx = Box(v, false);
                            for (double x = bx.x0; x <= bx.x1 + 1e-6; x += 0.25)
                                for (double z = bx.z0; z <= bx.z1 + 1e-6; z += 0.25)
                                {
                                    if (x > bx.x0 + 1e-6 && x < bx.x1 - 0.25 && z > bx.z0 + 1e-6 && z < bx.z1 - 0.25) continue;   // the rim only
                                    double g = s.ShuttleOutlineGapM((int)Math.Round(x * 8), (int)Math.Round(-z * 8));
                                    if (g < smin) { smin = g; sn = Name(v); }
                                }
                        }
                        bool ok = smin >= GapHullM;
                        if (!ok) { bad++; pass = false; }
                        sb.AppendLine($"  {(ok ? "ok  " : "BAD ")} the start ship #{s.Id} of {s.Owner} at tile ({s.CenterX / 16},{s.CenterY / 16}): its outline {smin:0.00} m from the nearest hull ({sn})");
                    }
                    int inHull = 0; string inHullAt = "";
                    foreach (var r in w.Objects.Where(o => o.Kind == ZKind.Robot && !o.Destroyed && !w.L18BigTankDrivers.Contains(o)))
                        foreach (var v in veh)
                        {
                            var bx = Box(v, false); double rx = r.CenterX / 8.0, rz = -r.CenterY / 8.0;
                            if (rx > bx.x0 - 0.5 && rx < bx.x1 + 0.5 && rz > bx.z0 - 0.5 && rz < bx.z1 + 0.5) { inHull++; inHullAt += $" {r.Type}#{r.Id} in {Name(v)};"; }
                        }
                    if (inHull > 0) { bad++; pass = false; }
                    if (printAll || inHull > 0) sb.AppendLine($"  {(inHull == 0 ? "ok  " : "BAD ")} start robots within half a metre of a drawn hull: {inHull}{inHullAt}");
                    foreach (var d in w.L18BigTankDrivers)
                    {
                        double dmin = 99; string dn = "";
                        foreach (var v in veh) { var bx = Box(v, false); double c = Clear(bx, (d.CenterX / 8.0, d.CenterX / 8.0, -d.CenterY / 8.0, -d.CenterY / 8.0)); if (c < dmin) { dmin = c; dn = Name(v); } }
                        bool ok = dmin >= 1.0;
                        if (!ok) { bad++; pass = false; }
                        sb.AppendLine($"  {(ok ? "ok  " : "BAD ")} the driver {d.Type}#{d.Id} of {d.Owner} at tile ({d.LocX / 16},{d.LocY / 16}): {dmin:0.00} m from the nearest hull ({dn})");
                    }
                    {
                        // squad's formation and GapHullM from every drawn hull and the start ship's outline (the formation: SquadRadiusM round a squad's place)
                        var squads = w.Objects.Where(o => o.Kind == ZKind.Robot && !o.Destroyed && o.Leader == null && !o.IsFortDefender).ToList();
                        var ships = w.Objects.Where(o => o.IsShuttle && !o.Destroyed).ToList();
                        int sqBad = 0;
                        for (int i = 0; i < squads.Count; i++)
                        {
                            var a = squads[i]; double ax = a.CenterX / 8.0, az = -a.CenterY / 8.0;
                            for (int j = i + 1; j < squads.Count; j++)
                            {
                                var b = squads[j]; double bx = b.CenterX / 8.0, bz = -b.CenterY / 8.0;
                                double d = Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz)) - Radius(a) - Radius(b);
                                if (d > 30) continue;
                                bool ok = d >= SquadGapM - 1e-9; if (!ok) sqBad++;
                                if (d < worstSquad) { worstSquad = d; worstSquadAt = st.name + ": " + Squad(a) + " - " + Squad(b); }
                                if (printAll || !ok) sb.AppendLine($"  {(ok ? "ok  " : "BAD ")} squads {Squad(a)} - {Squad(b)}: {d:0.00} m between their formations");
                            }
                            double hmin = 99; string hn = "-";
                            foreach (var v in veh) { double c = Clear(Box(v, false), (ax, ax, az, az)) - Radius(a); if (c < hmin) { hmin = c; hn = Name(v); } }
                            foreach (var s in ships) { double c = s.ShuttleOutlineGapM(a.CenterX, a.CenterY) - Radius(a); if (c < hmin) { hmin = c; hn = "the start ship #" + s.Id; } }
                            bool hok = hmin >= GapHullM - 1e-9; if (!hok) sqBad++;
                            if (hmin < worstRobot) { worstRobot = hmin; worstRobotAt = st.name + ": " + Squad(a) + " - " + hn; }
                            if (printAll || !hok) sb.AppendLine($"  {(hok ? "ok  " : "BAD ")} {Squad(a)}: its formation {hmin:0.00} m from the nearest hull ({hn})");
                        }
                        if (sqBad > 0) { bad += sqBad; pass = false; }
                        sb.AppendLine($"  {(sqBad == 0 ? "ok  " : "BAD ")} {squads.Count} robot squads: {sqBad} BAD (squads {SquadGapM:0.0} m apart, {GapHullM:0.0} m from every hull and the ship)");
                    }
                    sb.AppendLine($"  {(bad == 0 ? "every pair and every clearance kept" : bad + " BAD")}");
                }
                sb.AppendLine($"worst hull gap {worstHull:0.00} m ({worstHullAt}); worst whole-model gap {worstAll:0.00} m ({worstAllAt}); worst squad gap {worstSquad:0.00} m ({worstSquadAt}); worst squad to hull {worstRobot:0.00} m ({worstRobotAt})");
            }
            finally
            {
                ZWorld.ParkingKeepOut = wasKeep; ZWorld.ParkingKeepOutMap = wasKeepMap;
                ZWorld.ShuttleOn = wasShOn; ZWorld.ShuttlePlayableDeviation = wasPl; ZWorld.SideShuttlesDeviation = wasSide; ZWorld.L18StartShuttle = wasSt; ZWorld.ShuttleTeam = wasTeam;
            }
            return sb.ToString();
        }

        /// <summary>
        /// a disc of SquadRadiusM round its place (the scene's members stand about 0.75 m from it), a lone robot LoneRadiusM.</summary>
        public const double SquadGapM = 3.0, SquadRadiusM = 1.2, LoneRadiusM = 0.4;
        /// <summary>The EMPTY Mammoth: at least MammothFarTiles tiles from both forts, a road tile within MammothRoadTiles tiles; a neutral crane
        /// counts as "further away" CraneFarTiles tiles or more from both forts.</summary>
        public const int MammothFarTiles = 30, MammothRoadTiles = 4, CraneFarTiles = 20;
        /// <summary>bizon_far_r3: the Bizon's hull half width (m) for the night post clearance (Z_bizon's drawn hull: x -2.48 .. 2.49).</summary>
        public const double BizonHalfM = 2.5;
        /// <summary>
        /// from that door walk out along it (a robot is 1.6 m wide with its keep-clear).</summary>
        public const double R341StripM = 1.6;
        /// <summary></summary>
        static bool R341Waived(ZObject v) => v != null && v.Source != null && ZBigTanks.Is(v.Type) && ZWorld.L18BigTankTile(v.Source.x, v.Source.y) != (v.Source.x, v.Source.y) && (v.LocX / 16, v.LocY / 16) == ZWorld.L18BigTankTile(v.Source.x, v.Source.y);
        static double Radius(ZObject r) => r.Minions.Count > 0 ? SquadRadiusM : LoneRadiusM;
        static string Squad(ZObject r) => $"{r.Type}{(r.Minions.Count > 0 ? " squad of " + (r.Minions.Count + 1) : "")} {r.Owner} at ({r.LocX / 16},{r.LocY / 16})";
        /// <summary>Tiles from an object's middle to a building's footprint (0 inside).</summary>
        static double TilesToBox(ZObject o, ZObject f)
        {
            double dx = Math.Max(Math.Max(f.LocX - o.CenterX, o.CenterX - (f.LocX + f.WidthPix)), 0), dy = Math.Max(Math.Max(f.LocY - o.CenterY, o.CenterY - (f.LocY + f.HeightPix)), 0);
            return Math.Sqrt(dx * dx + dy * dy) / 16.0;
        }
        /// <summary>The least clear distance (m) from a vehicle's drawn hull to the footprints of the given objects (99: none).</summary>
        static double NearestM(ZObject v, IEnumerable<ZObject> list)
        {
            double best = 99; var hb = Box(v, false);
            foreach (var o in list) if (!o.Destroyed) best = Math.Min(best, Clear(hb, PxBox(o.LocX, o.LocY, o.LocX + o.WidthPix, o.LocY + o.HeightPix)));
            return best;
        }
        /// <summary>Road tiles within r tiles of a vehicle's drawn hull.</summary>
        static int RoadTilesNear(ZWorld w, ZObject v, int r)
        {
            var hb = Box(v, false); int n = 0;
            int tx0 = (int)Math.Floor(hb.x0 / 2) - r, tx1 = (int)Math.Floor(hb.x1 / 2) + r, ty0 = (int)Math.Floor(-hb.z1 / 2) - r, ty1 = (int)Math.Floor(-hb.z0 / 2) + r;
            for (int ty = ty0; ty <= ty1; ty++) for (int tx = tx0; tx <= tx1; tx++) if (tx >= 0 && ty >= 0 && tx < w.Map.width && ty < w.Map.height && w.Map.ClassAt(tx, ty) == ZClass.Road) n++;
            return n;
        }
        /// <summary>
        /// (the head start = ZWorld.L18HeadStartTypes, the SHUTTLE box on / off), the rules on.</summary>
        static int OwnedCranesAtStart(string settings, string map18, string nightJson, out string where)
        {
            int n = 0; var sb = new StringBuilder(); var wasKeep = ZWorld.ParkingKeepOut; var wasKeepMap = ZWorld.ParkingKeepOutMap;
            bool wasShOn = ZWorld.ShuttleOn, wasPl = ZWorld.ShuttlePlayableDeviation, wasSide = ZWorld.SideShuttlesDeviation, wasSt = ZWorld.L18StartShuttle; var wasTeam = ZWorld.ShuttleTeam;
            try
            {
                ZWorld.ParkingKeepOut = KeepOut(nightJson); ZWorld.ParkingKeepOutMap = ZMapData.FromJson(map18).file;
                foreach (var player in new[] { Team.Neutral, Team.Red, Team.Blue })
                    foreach (var (box, head) in new[] { (false, false), (true, false), (false, true), (true, true) })
                    {
                        if (player == Team.Neutral && (box || head)) continue;
                        ZWorld.ShuttlePlayableDeviation = true; ZWorld.SideShuttlesDeviation = true;
                        ZWorld.ShuttleOn = player != Team.Neutral; ZWorld.ShuttleTeam = player == Team.Neutral ? Team.Red : player; ZWorld.L18StartShuttle = box;
                        var w = ZWorld.Create(settings, map18, 1, false);
                        if (head) w.AddHeadStart(player, ZWorld.L18HeadStartTypes);
                        foreach (var o in w.Objects)
                            if (!o.Destroyed && o.Kind == ZKind.Vehicle && o.Type == "crane" && (o.Owner != Team.Neutral || o.Drivers.Count > 0)) { n++; sb.Append($"; {o.Owner} crane#{o.Id} at ({o.LocX / 16},{o.LocY / 16}) ({(player == Team.Neutral ? "the computer's start" : player + (box ? ", box ON" : ", box OFF") + (head ? ", head start ON" : ", head start OFF"))})"); }
                    }
            }
            finally
            {
                ZWorld.ParkingKeepOut = wasKeep; ZWorld.ParkingKeepOutMap = wasKeepMap;
                ZWorld.ShuttleOn = wasShOn; ZWorld.ShuttlePlayableDeviation = wasPl; ZWorld.SideShuttlesDeviation = wasSide; ZWorld.L18StartShuttle = wasSt; ZWorld.ShuttleTeam = wasTeam;
            }
            where = n == 0 ? " (none in 9 starts)" : sb.ToString();
            return n;
        }

        static string Name(ZObject v) => $"{v.Type}#{v.Id} {(v.Owner == Team.Neutral ? "empty" : v.Owner.ToString())} at ({v.LocX / 16},{v.LocY / 16}){(v.Source != null && (v.Source.x != v.LocX / 16 || v.Source.y != v.LocY / 16) ? " (map " + v.Source.x + "," + v.Source.y + ")" : "")}";

        /// <summary>When the vehicle's drawn hull stands clear of buildings, map items (a flag excepted), closed / water tiles, the night posts
        /// and every door lane / gate face; else what it touches. near / nearWhat: the nearest of these (m).</summary>
        static string Clearance(ZWorld w, ZObject v, out double near, out string nearWhat)
        {
            var hb = Box(v, false); var why = new List<string>(); double nr = 99; string nw = "-";
            void Note(double c, string what, double need) { if (c < nr) { nr = c; nw = what; } if (c < need) why.Add(what + (c < 0 ? " (" + (-c).ToString("0.00") + " m into it)" : " (only " + c.ToString("0.00") + " m clear, " + need.ToString("0.0") + " m wanted)")); }
            foreach (var o in w.Objects)
            {
                if (o.Destroyed || o == v || o.Kind == ZKind.Robot || o.Kind == ZKind.Vehicle || (o.Kind == ZKind.MapItem && o.Type == "flag")) continue;
                if (o.Kind != ZKind.Building && o.Kind != ZKind.MapItem && o.Kind != ZKind.Cannon) continue;
                double c = Clear(hb, PxBox(o.LocX, o.LocY, o.LocX + o.WidthPix, o.LocY + o.HeightPix)); if (c < 12) Note(c, o.Type + "#" + o.Id + (o.IsFort ? " (fort)" : ""), o.IsFort ? ClearFortM : ClearBuildingM);
            }
            bool NearFort(int tx, int ty)
            {
                foreach (var f in w.Objects)
                    if (f.IsFort && !f.Destroyed && tx >= f.LocX / 16 - FortWallTiles && tx <= (f.LocX + f.WidthPix) / 16 + FortWallTiles && ty >= f.LocY / 16 - FortWallTiles && ty <= (f.LocY + f.HeightPix) / 16 + FortWallTiles) return true;
                return false;
            }
            int tx0 = (int)Math.Floor(hb.x0 / 2) - 1, tx1 = (int)Math.Floor(hb.x1 / 2) + 1, ty0 = (int)Math.Floor(-hb.z1 / 2) - 1, ty1 = (int)Math.Floor(-hb.z0 / 2) + 1;
            for (int ty = ty0; ty <= ty1; ty++)
                for (int tx = tx0; tx <= tx1; tx++)
                {
                    var c = w.Map.ClassAt(tx, ty);
                    if (c == ZClass.Ground || c == ZClass.Road) continue;
                    bool fortWall = c == ZClass.Impassable && NearFort(tx, ty);
                    Note(Clear(hb, (tx * 2.0, tx * 2.0 + 2, -(ty * 2.0 + 2), -ty * 2.0)), (fortWall ? "fort wall tile" : c == ZClass.Impassable ? "closed tile" : "water tile") + " (" + tx + "," + ty + ")", fortWall ? ClearFortM : ClearTileM);
                }
            foreach (var k in ZWorld.ParkingKeepOut)
            {
                double px = k.x / 8.0, pz = -k.y / 8.0, r = k.r / 8.0;
                double dx = Math.Max(Math.Max(hb.x0 - px, px - hb.x1), 0), dz = Math.Max(Math.Max(hb.z0 - pz, pz - hb.z1), 0);
                double c = (dx == 0 && dz == 0 ? -r : Math.Sqrt(dx * dx + dz * dz) - r); if (c < 12) Note(c, "night post at (" + px.ToString("0.0") + "," + pz.ToString("0.0") + ")", ClearPostM);
            }
            for (double x = hb.x0; x <= hb.x1 + 1e-6; x += 0.25)
                for (double z = hb.z0; z <= hb.z1 + 1e-6; z += 0.25)
                    if (w.InExitLane(x * 8, -z * 8, 0) && !R341Waived(v)) { why.Add("a door lane at (" + x.ToString("0.0") + "," + z.ToString("0.0") + ")"); goto lanesDone; }   /* kit l18_map_fixes_r3: the owner's Bizon spot (R3.41) is in front of a factory door on purpose: its own line counts the free strip */
            lanesDone:
            if (w.NearGateFace((int)Math.Floor(hb.x0 * 8), (int)Math.Floor(-hb.z1 * 8), (int)Math.Ceiling(hb.x1 * 8), (int)Math.Ceiling(-hb.z0 * 8), 0)) why.Add("a fort's gate face");
            near = nr; nearWhat = nw;
            return string.Join(", ", why);
        }
    }
}
