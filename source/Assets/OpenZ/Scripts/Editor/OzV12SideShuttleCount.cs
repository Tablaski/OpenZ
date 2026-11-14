using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.2 item 111 (rule DV144, Sim/ZWorld.SideShuttles.cs): the SHUTTLE box for both sides, level 17's two empty car-park shuttles and the
    /// head start that never stands on or beside a shuttle, counted headless (no scene, no Play, nothing saved; menu OpenZ/V1.2/Shuttle:
    /// 111 Both Sides Check (headless), Editor/OzSideShuttleMenu.cs; the same class runs outside the editor). Every level start is made as
    /// View/ZGame.cs makes it (the Play session's rule switches, the sides of View/OzSides.cs, the box's answer, DV32's parked start, DV63's
    /// EASY crews, DV73's HARD door guns, DV66's exit lanes, the CPU, the build-list bonus, then the HEAD START at the human's fort).
    ///  A  the box rule: offered on levels 16 and 17 on EASY / MEDIUM for both sides (the copy of UI/OzShuttleStart.cs this check uses is
    ///     compared with the game's own in the editor menu); the sides' rule (View/OzSides.cs) keeps the box's answer for blue too.
    ///  B  every combination: levels 2 / 16 / 17 x red / blue x EASY / MEDIUM / HARD x the box on / off x HEAD START on / off (72 starts):
    ///     the shuttles there are exactly the wanted ones on their own spots (the player's by his own fort when the box asks, crewed, his
    ///     team, the level's playable shuttle; level 16's pocket by the red fort empty while blue plays; level 17's two EMPTY car-park ships
    ///     on every start), no spot searched; the room round every ship (vehicles and guns 4 m, robots 3 m, buildings 2 m, flags 4 m, items
    ///     1.5 m, drawn props and plant crowns when handed in, its ramp lane, no exit lane, no gate face, the landing rule, nobody within
    ///     2 m); every ship inside the map's edge rule; its ramp foot reachable on foot from both forts' gates; every HEAD START vehicle placed,
    ///     4 m from every ship's outline, off every ramp lane, its box overlapping nothing.
    ///  C  red's own game: every red start the same object for object as with the rule off (type, team, place, size, hit points, the head
    ///     start's places), the one new object being level 17's empty ship in the blue side's car park.
    ///  D  the rule off: blue starts as before for every other object; 'Zod exact' (DV118 off): no shuttle on any level.
    ///  E  short matches with the ships (blue and red, levels 16 / 17, the computer's shuttle plan on): no exception, the same game twice.
    ///  F  the head start's own rule holds whatever options are on: with DV66 off (oz_exit_lanes 0, its 2-tile parking ring gone) every
    ///     head start vehicle of levels 16 / 17, both sides, still keeps 4 m from every ship's outline and off its ramp lane.
    /// Test only.
    /// </summary>
    public static class OzV12SideShuttleCount
    {
        public static readonly string[] Levels = { "L02", "L16", "L17" };
        static readonly GameDifficulty[] Diffs = { GameDifficulty.Easy, GameDifficulty.Medium, GameDifficulty.Hard };
        public const double MatchSec = 240.0;
        const double Tick = ZConst.TICK;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static string D(GameDifficulty d) => d == GameDifficulty.Easy ? "EASY" : d == GameDifficulty.Medium ? "MEDIUM" : "HARD";
        /// <summary>UI/OzShuttleStart.Rule and L17Start (copied: that class reads PlayerPrefs; the editor menu compares the two).</summary>
        public static bool BoxRule(string level, GameDifficulty d, bool box) => level != "L16" || (d != GameDifficulty.Hard && box);
        public static bool BoxL17(string level, GameDifficulty d, bool box) => level == "L17" && d != GameDifficulty.Hard && box;

        /// <summary>One level start as ZGame makes it. props: drawn props (ParkingKeepOut discs) for level 17, or null; plants: crown circles
        /// (x, y, r px, h m) for level 16, or null; rule: the DV144 switch.</summary>
        public static ZWorld Start(string settings, string map, string level, bool blue, GameDifficulty d, bool box, bool headStart, bool rule,
                                   IList<(int x, int y, int r)> props, IList<double> plants, int seed = 1)
        {
            var s = OzSides.For(blue, false);
            bool easy = d == GameDifficulty.Easy, hard = d == GameDifficulty.Hard;
            OzV12PlayBlueCount.PlaySession(level, easy, hard, s);
            if (noExitLanes) ZWorld.ExitLanesDeviation = false;                                       // F: DV66 off (oz_exit_lanes 0): the head start's own room rule is gone
            ZWorld.ShuttleOn = OzSides.StartShuttle(level, blue, BoxRule(level, d, box));
            ZWorld.L17StartShuttle = OzSides.FortShuttle(blue, BoxL17(level, d, box));
            ZWorld.SideShuttlesDeviation = rule;
            var m = ZMapData.FromJson(map);
            ZWorld.ParkingKeepOut = props != null && level == "L17" ? new List<(int, int, int)>(props) : new List<(int, int, int)>();
            ZWorld.ParkingKeepOutMap = m.file;
            var chosen = easy ? ZBotDifficulty.Easy : ZBotDifficulty.Normal;
            int tech = easy ? 5 : d == GameDifficulty.Medium ? OzV12PlayBlueCount.MediumTech : 0;
            var w = OzV12PlayBlueCount.NewMatch(settings, map, level, s, chosen, tech, headStart, seed);
            if (plants != null && level == "L16") w.ShuttleSetKeepOut(plants, 1);
            return w;
        }

        static string Sig(ZObject o) => o.Kind + ":" + o.Type + ":" + o.Owner + "@" + o.LocX + "," + o.LocY + ":" + o.WidthPix + "x" + o.HeightPix + ":" + o.MaxHealth;

        /// <summary>The middle (px) and heading of a spot (tile x, y, heading).</summary>
        static bool AtSpot(ZObject s, int[] spot) => s != null && s.CenterX == spot[0] * 16 + 8 && s.CenterY == spot[1] * 16 + 8 && Math.Abs(Norm(s.ShuttleHeadingDeg - spot[2])) < 0.01;
        static double Norm(double a) { a %= 360.0; if (a > 180.0) a -= 360.0; if (a <= -180.0) a += 360.0; return a; }
        static string Where(ZObject s) => s == null ? "none" : $"#{s.Id} {s.Owner} tile {s.CenterX >> 4},{s.CenterY >> 4} hd {s.ShuttleHeadingDeg:0} pilots {s.Drivers.Count}";

        /// <summary>The ramp foot (px): ShuttleBackM behind the middle.</summary>
        static (int x, int y) Foot(ZObject s)
        {
            double hd = s.ShuttleHeadingDeg * Math.PI / 180.0;
            return ((int)Math.Round(s.CenterX - Math.Cos(hd) * ZWorld.ShuttleBackM * 8.0), (int)Math.Round(s.CenterY - Math.Sin(hd) * ZWorld.ShuttleBackM * 8.0));
        }

        /// <summary>A robot's walk (m) from tile a to tile b over robot tiles (8 ways, no corner cutting), every ship's outline + 0.5 m closed;
        /// -1 = no way.</summary>
        public static double Walk(ZWorld w, int ax, int ay, int bx, int by)
        {
            int W = w.Map.width, H = w.Map.height;
            var open = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    open[y * W + x] = w.Path.TilePassable(x, y, true) && w.Map.ClassAt(x, y) != ZClass.DeepWater;
            foreach (var s in w.Objects)
            {
                if (!s.IsShuttle || s.Destroyed) continue;
                for (int y = Math.Max(0, (s.CenterY - 48) >> 4); y <= Math.Min(H - 1, (s.CenterY + 48) >> 4); y++)
                    for (int x = Math.Max(0, (s.CenterX - 48) >> 4); x <= Math.Min(W - 1, (s.CenterX + 48) >> 4); x++)
                        if (s.ShuttleOutlineGapM(x * 16 + 8, y * 16 + 8) < 0.5) open[y * W + x] = false;
            }
            if (ax < 0 || ay < 0 || ax >= W || ay >= H || !open[ay * W + ax] || bx < 0 || by < 0 || bx >= W || by >= H || !open[by * W + bx]) return -1;
            var dist = new double[W * H]; for (int i = 0; i < dist.Length; i++) dist[i] = double.MaxValue;
            var pq = new SortedSet<(double d, int i)>();
            dist[ay * W + ax] = 0; pq.Add((0, ay * W + ax));
            while (pq.Count > 0)
            {
                var cur = pq.Min; pq.Remove(cur);
                int x = cur.i % W, y = cur.i / W;
                if (x == bx && y == by) return cur.d * 2.0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= W || ny >= H || !open[ny * W + nx]) continue;
                        if (dx != 0 && dy != 0 && (!open[y * W + nx] || !open[ny * W + x])) continue;
                        double nd = cur.d + (dx != 0 && dy != 0 ? 1.41421356 : 1.0);
                        int k = ny * W + nx;
                        if (nd < dist[k]) { if (dist[k] < double.MaxValue) pq.Remove((dist[k], k)); dist[k] = nd; pq.Add((nd, k)); }
                    }
            }
            return -1;
        }

        static ZObject Fort(ZWorld w, Team t)
        {
            ZObject f = null;
            foreach (var o in w.Objects) if (o.Kind == ZKind.Building && o.IsFort && o.Owner == t && !o.Destroyed && (f == null || o.Type == "fort_front" || o.Type == "fort_back")) f = o;
            return f;
        }

        /// <summary>The walk (m) from a team's fort gate (its exit point) to a ship's ramp foot; -1 = none.</summary>
        public static double GateWalk(ZWorld w, Team t, ZObject s)
        {
            var f = Fort(w, t);
            if (f == null || !f.GetCreationMovePoint(out int ex, out int ey)) return -1;
            var p = Foot(s);
            return Walk(w, ex >> 4, ey >> 4, p.x >> 4, p.y >> 4);
        }

        /// <summary>Every point of the ship's outline inside the map, ShuttleEdgeTiles tiles in from each edge.</summary>
        static bool InsideMap(ZWorld w, ZObject s)
        {
            int e = ZWorld.ShuttleEdgeTiles * 16;
            for (int y = s.CenterY - 40; y <= s.CenterY + 40; y += 2)
                for (int x = s.CenterX - 40; x <= s.CenterX + 40; x += 2)
                    if (s.ShuttleOutlineGapM(x, y) < 0 && (x < e || y < e || x >= w.Map.WidthPix - e || y >= w.Map.HeightPix - e)) return false;
            return true;
        }

        public sealed class StartResult
        {
            public string Key, Ships = "", Bad = "", Near = "", Walks = "", Head = "";
            public bool Ok => Bad.Length == 0;
        }

        /// <summary>B: one start measured.</summary>
        public static StartResult Measure(ZWorld w, string level, bool blue, GameDifficulty d, bool box, bool headStart)
        {
            var r = new StartResult { Key = $"{level} {(blue ? "BLUE" : "RED")} {D(d)} box {(box ? "on" : "off")} head start {(headStart ? "on" : "off")}" };
            var bad = new StringBuilder();
            var ships = w.Objects.Where(o => o.IsShuttle && !o.Destroyed).ToList();
            var human = blue ? Team.Blue : Team.Red;
            bool wantStart = level != "L02" && d != GameDifficulty.Hard && box;
            // the wanted ships
            var want = new List<(string what, ZObject s, int[] spot, Team team, bool crewed)>();
            if (level == "L16" && wantStart)
            {
                if (blue) want.Add(("the player's by the blue fort", w.BlueStartShuttle, ZWorld.L16BlueFortShuttleSpot, Team.Blue, true));
                else
                {
                    var q = ZWorld.L16ShuttleSquare; var ps = w.PlayableShuttle;
                    bool atPocket = ps != null && ps.CenterX == (q[0] * 2 + q[2]) * 8 && ps.CenterY == (q[1] * 2 + q[3]) * 8 && Math.Abs(Norm(ps.ShuttleHeadingDeg - ZWorld.L16ShuttleHeadingDeg)) < 0.01;
                    want.Add(("the player's in the red fort's pocket", atPocket ? ps : null, null, Team.Red, true));
                }
            }
            if (level == "L17")
            {
                want.Add(("the empty one in the red side's car park", w.L17CarParkShuttle, ZWorld.L17CarParkShuttleSpot, Team.Neutral, false));
                want.Add(("the empty one in the blue side's car park", w.L17BlueCarParkShuttle, ZWorld.L17BlueCarParkShuttleSpot, Team.Neutral, false));
                if (wantStart) want.Add(blue ? ("the player's by the blue fort", w.BlueStartShuttle, ZWorld.L17BlueFortShuttleSpot, Team.Blue, true)
                                             : ("the player's by the red fort", w.L17FortShuttle, ZWorld.L17FortShuttleSpot, Team.Red, true));
            }
            foreach (var x in want)
            {
                bool ok = x.s != null && (x.spot == null || AtSpot(x.s, x.spot)) && x.s.Owner == x.team && (x.crewed ? x.s.Drivers.Count == 1 : x.s.ShuttleIsEmpty && x.s.Drivers.Count == 0) && x.s.Passengers.Count == 0;
                r.Ships += $" {x.what}: {Where(x.s)};";
                if (!ok) bad.Append($" {x.what} missing or not as wanted ({Where(x.s)});");
            }
            var wantedSet = new HashSet<ZObject>(want.Where(x => x.s != null).Select(x => x.s));
            foreach (var s in ships) if (!wantedSet.Contains(s)) bad.Append($" an unwanted shuttle {Where(s)};");
            if (ships.Count != want.Count) bad.Append($" {ships.Count} shuttles on the map, {want.Count} wanted;");
            var own = want.FirstOrDefault(x => x.crewed).s;
            if (wantStart && level != "L02" && w.PlayableShuttle != own) bad.Append($" the level's playable shuttle is {Where(w.PlayableShuttle)}, not the player's own;");
            if (level == "L16" && blue && w.L16Shuttle != null) bad.Append(" the decoration stands in the red fort's pocket;");
            if (w.SideShuttlesMoved > 0 || w.L17ShuttlesMoved > 0) bad.Append($" a spot was searched ({w.SideShuttleWhy}{w.L17ShuttleWhy});");
            // the room, the map edge, the walks
            foreach (var s in ships)
            {
                string rb = w.ShuttleStartRoom(s, out string near);
                if (rb.Length > 0) bad.Append($" room of {Where(s)}:{rb}");
                r.Near += $" [{Where(s)}: {near}]";
                if (!InsideMap(w, s)) bad.Append($" {Where(s)} reaches past the map's edge rule;");
                double wr = GateWalk(w, Team.Red, s), wb = GateWalk(w, Team.Blue, s);
                r.Walks += $" #{s.Id}: from the red gate {(wr < 0 ? "NO WAY" : wr.ToString("0", Inv) + " m")}, from the blue gate {(wb < 0 ? "NO WAY" : wb.ToString("0", Inv) + " m")};";
                if (wr < 0 || wb < 0) bad.Append($" {Where(s)}: its ramp foot cannot be walked to from a fort's gate;");
            }
            // the head start
            if (headStart)
            {
                var types = OzV12PlayBlueCount.HeadTypes(level);
                var head = w.Objects.Where(o => o.Kind == ZKind.Vehicle && o.Source == null && !o.IsShuttle).ToList();
                if (head.Count != types.Length) bad.Append($" head start {head.Count} of {types.Length} placed;");
                foreach (var v in head)
                {
                    foreach (var s in ships)
                    {
                        ZWorld.ShuttleBoxGaps(s, v.LocX, v.LocY, v.LocX + v.WidthPix, v.LocY + v.HeightPix, out double g, out double lg);
                        if (g < ZWorld.HeadStartShuttleClearM || lg < ZWorld.HeadStartLaneClearM) bad.Append($" head start {v.Type}#{v.Id} @{v.LocX >> 4},{v.LocY >> 4}: {g:0.0} m from {Where(s)}'s outline, {lg:0.0} m from its ramp lane;");
                    }
                    foreach (var o in w.Objects)
                    {
                        if (o == v || o.Destroyed || o.Kind == ZKind.Robot || (o.Kind == ZKind.MapItem && o.Type == "flag")) continue;
                        if (o.LocX < v.LocX + v.WidthPix && v.LocX < o.LocX + o.WidthPix && o.LocY < v.LocY + v.HeightPix && v.LocY < o.LocY + o.HeightPix) bad.Append($" head start {v.Type}#{v.Id} overlaps {o.Type}#{o.Id};");
                    }
                    if (v.LocX < 16 || v.LocY < 16 || v.LocX + v.WidthPix > w.Map.WidthPix - 16 || v.LocY + v.HeightPix > w.Map.HeightPix - 16) bad.Append($" head start {v.Type}#{v.Id} at the map's edge;");
                    r.Head += $" {v.Type}@{v.LocX >> 4},{v.LocY >> 4}";
                }
                if (w.HeadStartShuttleRefusals > 0) r.Head += $" ({w.HeadStartShuttleRefusals} places refused for a shuttle)";
            }
            r.Bad = bad.ToString();
            return r;
        }

        // ================================================================================================ the check
        static int pass, fail;
        static StringBuilder sb;
        static void V(bool ok, string what) { if (ok) pass++; else fail++; sb.AppendLine((ok ? "PASS  " : "FAIL  ") + what); }
        static void Note(string what) => sb.AppendLine("      " + what);

        /// <summary>The whole check. props: level 17's drawn props (the scene's DEC_ discs and night lamps) or null; plants16: level 16's
        /// plant crowns (x, y, r px, h m) or null; quick: no matches (the parity report's row). Every switch is put back afterwards.</summary>
        public static string Check(string settings, Func<string, string> map, IList<(int x, int y, int r)> props, IList<double> plants16, bool quick = false, Action<string> progress = null)
        {
            sb = new StringBuilder(); pass = fail = 0;
            var t0 = DateTime.Now;
            sb.AppendLine("1.2 item 111 (DV144): the SHUTTLE box for both sides, level 17's two empty car-park shuttles, the head start off the shuttles (OzV12SideShuttleCount)");
            sb.AppendLine($"drawn props of level 17: {(props != null ? props.Count + " discs handed in" : "none handed in (the Play proof measures them)")}; plant crowns of level 16: {(plants16 != null ? plants16.Count / 4 + " handed in" : "none handed in (the Play proof measures them)")}");
            Team keepTeam = ZWorld.ShuttleTeam, keepCrews = ZWorld.L17CrewsTeam; var keepProps = ZWorld.ParkingKeepOut; var keepMap = ZWorld.ParkingKeepOutMap; bool keepRule = ZWorld.SideShuttlesDeviation;
            try
            {
                using (OzV11Kit.GameSwitches())
                {
                    PartA();
                    progress?.Invoke("B");
                    PartB(settings, map, props, plants16);
                    progress?.Invoke("C");
                    PartC(settings, map, props);
                    PartD(settings, map, props);
                    PartF(settings, map, props);
                    if (!quick) { progress?.Invoke("E"); PartE(settings, map, props); }
                }
            }
            catch (Exception ex) { V(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()); }
            finally { ZWorld.ShuttleTeam = keepTeam; ZWorld.L17CrewsTeam = keepCrews; ZWorld.ParkingKeepOut = keepProps; ZWorld.ParkingKeepOutMap = keepMap; }
            V(ZWorld.SideShuttlesDeviation == keepRule && ZWorld.ParkingKeepOut == keepProps, $"after the check: the DV144 switch back as it was ({(keepRule ? "on" : "off")}), the drawn props list the editor's own");
            sb.AppendLine($"RESULT {pass} PASS, {fail} FAIL ({(DateTime.Now - t0).TotalSeconds:0} s)");
            return sb.ToString();
        }

        static void PartA()
        {
            sb.AppendLine("A  the box rule and the sides' rule");
            bool ok = true; var rows = new StringBuilder();
            foreach (var lv in Levels)
                foreach (var d in Diffs)
                    foreach (bool box in new[] { true, false })
                        foreach (bool blue in new[] { false, true })
                        {
                            bool sh = OzSides.StartShuttle(lv, blue, BoxRule(lv, d, box)), f = OzSides.FortShuttle(blue, BoxL17(lv, d, box));
                            bool wantSh = lv != "L16" || (d != GameDifficulty.Hard && box), wantF = lv == "L17" && d != GameDifficulty.Hard && box;
                            if (sh != wantSh || f != wantF) { ok = false; rows.Append($" {lv} {D(d)} box {box} blue {blue}: rule {sh} / {f};"); }
                        }
            V(ok, "the box's answer is the same for red and blue: level 16 = the box on EASY / MEDIUM, level 17's start ship = the box on EASY / MEDIUM, never on HARD, never on level 2" + rows);
        }

        static void PartB(string settings, Func<string, string> map, IList<(int x, int y, int r)> props, IList<double> plants16)
        {
            sb.AppendLine("B  every start: levels 2 / 16 / 17 x red / blue x EASY / MEDIUM / HARD x the box on / off x HEAD START on / off");
            int n = 0, good = 0;
            var lines = new List<string>();
            foreach (var lv in Levels)
                foreach (bool blue in new[] { false, true })
                    foreach (var d in Diffs)
                        foreach (bool box in new[] { true, false })
                            foreach (bool hs in new[] { true, false })
                            {
                                var w = Start(settings, map(lv), lv, blue, d, box, hs, true, props, plants16);
                                var r = Measure(w, lv, blue, d, box, hs);
                                n++; if (r.Ok) good++;
                                if (!r.Ok) lines.Add("FAIL " + r.Key + ":" + r.Bad);
                                if (d == GameDifficulty.Medium && box && hs) Note($"{r.Key}:{r.Ships}{r.Walks} head start{r.Head}; room{r.Near}");
                            }
            foreach (var l in lines.Take(30)) Note(l);
            V(good == n && n == 72, $"{good} of {n} starts as wanted (the ships on their spots, the room round each, inside the map, walkable from both gates, the head start placed and clear of every ship)");
        }

        static void PartC(string settings, Func<string, string> map, IList<(int x, int y, int r)> props)
        {
            sb.AppendLine("C  red's own game: every red start the same as with the rule off, but level 17's new empty ship in the blue side's car park");
            int n = 0, same = 0; var diff = new StringBuilder();
            foreach (var lv in Levels)
                foreach (var d in Diffs)
                    foreach (bool box in new[] { true, false })
                        foreach (bool hs in new[] { true, false })
                        {
                            var on = Start(settings, map(lv), lv, false, d, box, hs, true, props, null);
                            var off = Start(settings, map(lv), lv, false, d, box, hs, false, props, null);
                            var a = on.Objects.Where(o => o != on.L17BlueCarParkShuttle).Select(Sig).ToList();
                            var b = off.Objects.Select(Sig).ToList();
                            bool eq = a.SequenceEqual(b) && (lv != "L17" || on.L17BlueCarParkShuttle != null) && (lv == "L17" || on.L17BlueCarParkShuttle == null);
                            n++; if (eq) same++; else diff.Append($" {lv} {D(d)} box {box} hs {hs}: {a.Count} vs {b.Count} objects;");
                        }
            V(same == n, $"{same} of {n} red starts the same object for object (type, team, place, size, hit points, the head start's places){diff}");
        }

        static void PartD(string settings, Func<string, string> map, IList<(int x, int y, int r)> props)
        {
            sb.AppendLine("D  the rule off; 'Zod exact'");
            bool ok = true; var why = new StringBuilder();
            foreach (var lv in Levels)
            {
                var w = Start(settings, map(lv), lv, true, GameDifficulty.Medium, true, true, false, props, null);
                int sh = w.Objects.Count(o => o.IsShuttle && !o.Destroyed);
                bool fine = w.BlueStartShuttle == null && w.L17BlueCarParkShuttle == null && w.HeadStartShuttleRefusals == 0 && (lv != "L17" || (sh == 1 && w.L17CarParkShuttle != null));
                if (!fine) { ok = false; why.Append($" {lv}: {sh} shuttles, blue start {Where(w.BlueStartShuttle)};"); }
            }
            V(ok, "the rule off: no blue start ship by the blue fort, no second car-park ship, the head start as before (level 17: DV134's one empty ship)" + why);
            bool zok = true; var zwhy = new StringBuilder();
            foreach (var lv in Levels)
                foreach (bool blue in new[] { false, true })
                {
                    var s = OzSides.For(blue, false);
                    OzV12PlayBlueCount.PlaySession(lv, false, false, s);
                    ZWorld.ShuttlePlayableDeviation = false; ZWorld.L17ShuttlesDeviation = false; ZWorld.SideShuttlesDeviation = false; ZWorld.L16ShuttleDeviation = false;
                    ZWorld.ParkingKeepOut = new List<(int, int, int)>();
                    var w = OzV12PlayBlueCount.NewMatch(settings, map(lv), lv, s, ZBotDifficulty.Normal, 2, true, 1);
                    int sh = w.Objects.Count(o => o.IsShuttle);
                    if (sh != 0) { zok = false; zwhy.Append($" {lv} {(blue ? "blue" : "red")}: {sh};"); }
                }
            V(zok, "'Zod exact' (DV118, DV134, DV144 off): no shuttle on any level for either side" + zwhy);
        }

        static bool noExitLanes;

        static void PartF(string settings, Func<string, string> map, IList<(int x, int y, int r)> props)
        {
            sb.AppendLine("F  DV66 off (oz_exit_lanes 0): the head start keeps clear of every ship by DV144's own rule");
            bool ok = true; var why = new StringBuilder(); int refused = 0;
            try
            {
                noExitLanes = true;
                foreach (var lv in new[] { "L16", "L17" })
                    foreach (bool blue in new[] { false, true })
                    {
                        var w = Start(settings, map(lv), lv, blue, GameDifficulty.Medium, true, true, true, props, null);
                        refused += w.HeadStartShuttleRefusals;
                        var ships = w.Objects.Where(o => o.IsShuttle && !o.Destroyed).ToList();
                        var head = w.Objects.Where(o => o.Kind == ZKind.Vehicle && o.Source == null && !o.IsShuttle).ToList();
                        if (head.Count != OzV12PlayBlueCount.HeadTypes(lv).Length) { ok = false; why.Append($" {lv} {(blue ? "blue" : "red")}: {head.Count} placed;"); }
                        foreach (var v in head)
                            foreach (var s in ships)
                            {
                                ZWorld.ShuttleBoxGaps(s, v.LocX, v.LocY, v.LocX + v.WidthPix, v.LocY + v.HeightPix, out double g, out double lg);
                                if (g < ZWorld.HeadStartShuttleClearM || lg < ZWorld.HeadStartLaneClearM) { ok = false; why.Append($" {lv} {(blue ? "blue" : "red")}: {v.Type}@{v.LocX >> 4},{v.LocY >> 4} {g:0.0} m / lane {lg:0.0} m from {Where(s)};"); }
                            }
                        Note($"{lv} {(blue ? "BLUE" : "RED")}, DV66 off: head start {string.Join(" ", head.Select(v => v.Type + "@" + (v.LocX >> 4) + "," + (v.LocY >> 4)))}; {w.HeadStartShuttleRefusals} places refused for a shuttle");
                    }
            }
            finally { noExitLanes = false; }
            V(ok, $"with DV66 off every head start vehicle of levels 16 / 17 (both sides) is placed and keeps 4 m from every ship's outline, off every ramp lane ({refused} places refused for a shuttle){why}");
        }

        static void PartE(string settings, Func<string, string> map, IList<(int x, int y, int r)> props)
        {
            sb.AppendLine($"E  short matches with the ships ({MatchSec:0} s, MEDIUM, the box and HEAD START on, the computer's shuttle plan on)");
            foreach (var lv in new[] { "L16", "L17" })
                foreach (bool blue in new[] { false, true })
                {
                    long h1 = 0, h2 = 0; string end = ""; bool threw = false;
                    for (int k = 0; k < 2; k++)
                    {
                        try
                        {
                            var w = Start(settings, map(lv), lv, blue, GameDifficulty.Medium, true, true, true, props, null);
                            ZWorld.CpuShuttleDeviation = true;
                            while (w.Time < MatchSec && !w.GameOver) { w.Tick(Tick); w.Events.Clear(); }
                            long h = 17; unchecked { foreach (var o in w.Objects) { h = h * 31 + o.Id; h = h * 31 + o.Health; h = h * 31 + o.LocX; h = h * 31 + o.LocY; h = h * 31 + (int)o.Owner; } }
                            if (k == 0) h1 = h; else h2 = h;
                            var ships = w.Objects.Where(o => o.IsShuttle && !o.Destroyed).Select(o => $"#{o.Id} {o.Owner} {(o.ShuttleAirborne ? "up" : "landed")} pilots {o.Drivers.Count}");
                            end = $"at {w.Time:0} s: ships {string.Join(", ", ships)}";
                        }
                        catch (Exception ex) { threw = true; end = ex.GetType().Name + ": " + ex.Message; }
                        finally { ZWorld.CpuShuttleDeviation = false; }
                    }
                    V(!threw && h1 == h2, $"{lv} {(blue ? "BLUE" : "RED")}: no exception, the same game twice (hash {h1:x16} / {h2:x16}); {end}");
                }
        }
    }
}
