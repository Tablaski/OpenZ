using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>
    /// the fort reading (View/MusicFortReader.cs) and the drama detector (View/MusicCore.cs DramaDetector) on REAL headless worlds of
    /// L02 and L17 (the real map, forts, walls and unit types; the map's own units made neutral and quiet). Two kinds of case:
    ///  - LIVE: the sim runs at its 100 Hz tick (two squads ordered at each other two screens away; on L17 two enemy squads attacking
    ///    ours by the flag 138 px off our fort's west wall, the fort area check; a light tank ordered at our fort from inside its range:
    ///    its shell really lands and sets the fort's hit record, LastEnemyHit).
    ///  - HELD: units stand where an attacker stands (a free spot at a chosen distance from the fort's walls, e.g. a missile launcher at
    ///    its full 160 px range), hold the fort as their target (AttackObject, as a unit firing at it does) and hit it on their own reload
    ///    cadence when the sim's rules let them (ZObject.CanAttackObject: a jeep or a robot without grenades cannot hurt a fort; DV26:
    ///    the fort's nearest wall within the unit's AttackRadius). Game time moves in 0.25 s steps and both forts are read each step,
    ///    as AudioDirector.MusicV3 does. (Headless, a newly made unit's walking order is dropped at once, so the held cases place the
    ///    unit where its walk would end instead of letting it drive there.)
    /// A new ZWorld per case; the rule switches are those of a default level start (put back after the run); the scene and the running
    /// game are never touched. Plain C# over the Sim types: OpenZ > Music v3 > Run fort reader scenarios runs it in the editor,
    /// kits/music_drama/tools/harness.py off Unity.
    /// </summary>
    public static class MusicFortReaderScenarios
    {
        const Team Me = Team.Red, Them = Team.Blue;                                                   // AudioDirector: game.player (Red unless the player plays Blue)
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        sealed class Case { public string name; public bool pass = true; public readonly List<string> notes = new List<string>(); }
        static void Expect(Case c, bool ok, string what) { c.notes.Add((ok ? "  ok   " : "  FAIL ") + what); if (!ok) c.pass = false; }
        static string S(double v) => v.ToString("0.0", IC);

        // ------------------------------------------------------------------------------------------------ switches
        /// <summary>The public static switches of the rules (ZWorld, ZPathing), to be put back after the run (as Editor/OzV14NeighbourGridCount).</summary>
        static List<(FieldInfo f, object v)> SaveSwitches()
        {
            var l = new List<(FieldInfo, object)>();
            foreach (var t in new[] { typeof(ZWorld), typeof(ZPathing) })
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
                    if (!f.IsLiteral && !f.IsInitOnly && (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string))) l.Add((f, f.GetValue(null)));
            return l;
        }
        static void Set(Type t, string name, object v) { var f = t.GetField(name, BindingFlags.Public | BindingFlags.Static); if (f != null && !f.IsLiteral && !f.IsInitOnly) f.SetValue(null, v); }
        /// <summary>The switches of a level started with the default options (the same set as Editor/OzV14NeighbourGridCount.PlaySwitches):
        /// every deviation on (DV26 building reach among them), then the ones a default start leaves off.</summary>
        static void PlaySwitches()
        {
            foreach (var f in typeof(ZWorld).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (f.FieldType == typeof(bool) && !f.IsLiteral && !f.IsInitOnly && f.Name.EndsWith("Deviation")) f.SetValue(null, true);
            Set(typeof(ZWorld), "ArmourChipDeviation", false); Set(typeof(ZWorld), "NoTankDuelVsJeep", true); Set(typeof(ZWorld), "ElectrobotAllLevels", false);
            Set(typeof(ZPathing), "VehicleFirst", true); Set(typeof(ZWorld), "L17CrewsDeviation", false); Set(typeof(ZWorld), "FortDoorGunDeviation", false);
            Set(typeof(ZWorld), "OvertakeDeviation", false); Set(typeof(ZWorld), "CustomStatsDeviation", false); Set(typeof(ZWorld), "HeroSquadsOffAsBlue", false);
            Set(typeof(ZWorld), "FriendsOverlap", false); Set(typeof(ZWorld), "ShuttleBigCrashProofNext", false); Set(typeof(ZWorld), "L17StartShuttle", false);
            Set(typeof(ZWorld), "NbVerify", false);
        }

        // ------------------------------------------------------------------------------------------------ world
        static ZWorld NewWorld(string settings, string mapText, int seed)
        {
            var w = ZWorld.Create(settings, mapText, seed);
            foreach (var o in w.Objects.Where(o => o.IsUnit && o.Owner != Team.Neutral).ToList()) { o.Owner = Team.Neutral; o.Drivers.Clear(); o.Waypoints.Clear(); o.Disengage(); }
            w.Events.Clear();
            return w;
        }

        static ZObject Fort(ZWorld w, Team t) => w.Objects.First(o => o.IsFort && o.Owner == t && !o.Destroyed);
        static double Wall(ZObject fort, int x, int y) => Math.Sqrt(fort.WallPoint(x, y, out _, out _));
        static double Centre(ZObject fort, int x, int y) => Math.Sqrt((double)(x - fort.CenterX) * (x - fort.CenterX) + (double)(y - fort.CenterY) * (y - fort.CenterY));

        /// <summary>A free tile box (tiles x tiles, passable for robots or vehicles, reachable on foot / wheels from the fort's gate) whose
        /// centre lies dMin..dMax px from the fort's walls, the closest one to the wanted direction (deg: 0 east, 90 south). Its centre.</summary>
        static bool Spot(ZWorld w, ZObject fort, bool robot, int tiles, double dMin, double dMax, double deg, out int cx, out int cy, out double gotDeg)
        {
            cx = cy = 0; gotDeg = 0;
            fort.GetCreationMovePoint(out int gx, out int gy);
            var seen = new HashSet<(int, int)>(); var q = new Queue<(int, int)>();
            for (int r = 0; r <= 3 && q.Count == 0; r++)
                for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
                    if (w.Path.TilePassable(gx / 16 + dx, gy / 16 + dy, robot) && seen.Add((gx / 16 + dx, gy / 16 + dy))) q.Enqueue((gx / 16 + dx, gy / 16 + dy));
            double best = double.MaxValue; bool found = false;
            while (q.Count > 0)
            {
                var (tx, ty) = q.Dequeue();
                bool box = true; for (int a = 0; a < tiles && box; a++) for (int b = 0; b < tiles && box; b++) box = w.Path.TilePassable(tx + a, ty + b, robot);
                if (box)
                {
                    int px = tx * 16 + tiles * 8, py = ty * 16 + tiles * 8; double d = Wall(fort, px, py);
                    if (d >= dMin && d <= dMax)
                    {
                        double ang = Math.Atan2(py - fort.CenterY, px - fort.CenterX) * 180.0 / Math.PI; double diff = Math.Abs(((ang - deg) % 360 + 540) % 360 - 180);
                        if (diff < best) { best = diff; cx = px; cy = py; gotDeg = (ang + 360) % 360; found = true; }
                    }
                }
                foreach (var (nx, ny) in new[] { (tx + 1, ty), (tx - 1, ty), (tx, ty + 1), (tx, ty - 1) })
                    if (seen.Count < 40000 && w.Path.TilePassable(nx, ny, robot) && seen.Add((nx, ny))) q.Enqueue((nx, ny));
            }
            return found;
        }

        static ZObject Make(ZWorld w, string cat, string type, int amount, int cx, int cy, Team t, int grenades = 0)
        {
            var o = cat == "robot" ? w.CreateRobotGroup(type, cx - 8, cy - 8, t, amount) : w.CreateObject(cat, type, cx - 16, cy - 16, t);
            foreach (var u in new[] { o }.Concat(o.Minions)) u.MaxHealth = u.Health = 1000000000;
            if (grenades > 0) o.Grenades = grenades;                                                    // a squad that picked up a crate: HasExplosives (the members through their leader)
            return o;
        }

        static bool Order(ZWorld w, ZObject o, ZObject target) =>
            w.SendWaypoints(o.Owner, o, new[] { new ZWaypoint { mode = WpMode.Attack, refId = target.Id, x = target.CenterX, y = target.CenterY } });

        // ------------------------------------------------------------------------------------------------ held run
        sealed class Held { public ZObject o, target; public double reload, next; public bool canHit; public double wall; }
        sealed class Watch { public double t0, firstHit = -1, drama = -1; public int fort, hits; public string why = ""; public float maxScore; public int maxUnits; public float maxOurs, maxTheirs; }

        /// <summary>HELD: game time in 0.25 s steps; each unit keeps its target and, when the sim's rules let it hurt the fort from where it
        /// stands, hits it on its reload cadence (LastEnemyHit = the hit's time, as Sim/ZWorld.cs ProcessMissiles / ZObject.cs hitscan set it);
        /// `shelling` = the fort also takes a hit every 2 s from a source that never counts (an enemy howitzer). Both forts are read each
        /// step and fed to a DramaDetector (sim and play seconds alike).</summary>
        static Watch HoldRun(ZWorld w, ZObject watched, List<Held> units, double seconds, MusicCoreConfig c, bool shelling = false)
        {
            var d = new DramaDetector(); var r = new Watch { t0 = w.Time }; double end = w.Time + seconds, shellNext = w.Time;
            foreach (var h in units)
            {
                h.wall = Wall(h.target, h.o.CenterX, h.o.CenterY);
                h.canHit = h.target.IsFort && h.o.CanAttackObject(h.target) && h.wall <= h.o.AttackRadius;   // DV26 InReach to a building: its nearest wall within range
                h.next = w.Time + 0.25;
            }
            while (w.Time < end - 1e-9)
            {
                w.Time += 0.25;
                foreach (var h in units)
                {
                    foreach (var m in new[] { h.o }.Concat(h.o.Minions)) m.AttackObject = h.target;         // the unit firing at its target holds it
                    if (h.canHit && w.Time >= h.next - 1e-9) { h.target.LastEnemyHit = w.Time; h.next = w.Time + h.reload; r.hits++; if (r.firstHit < 0 && h.target == watched) r.firstHit = w.Time; }
                }
                if (shelling && w.Time >= shellNext - 1e-9) { watched.LastEnemyHit = w.Time; shellNext = w.Time + 2.0; }
                var ours = MusicFortReader.Read(w, Me, c); var theirs = MusicFortReader.Read(w, Them, c); var mine = watched.Owner == Me ? ours : theirs;
                r.maxScore = Math.Max(r.maxScore, mine.score); r.maxUnits = Math.Max(r.maxUnits, mine.units); r.maxOurs = Math.Max(r.maxOurs, ours.score); r.maxTheirs = Math.Max(r.maxTheirs, theirs.score);
                if (d.Step(0.25f, ours, theirs, c) && d.Wanted && r.drama < 0) { r.drama = w.Time; r.fort = d.Fort; r.why = d.Why; }
            }
            return r;
        }

        static double Reload(ZObject o) => o.AttackSpeed > 0 ? o.AttackSpeed : 1.0;

        // ------------------------------------------------------------------------------------------------ cases
        /// <summary>HELD: the given units of the attacking side stand `dist` px off the fort's walls (or their own range when dist is 0)
        /// and attack it for 60 s.</summary>
        static Case AtFort(string settings, string map, string name, (string cat, string type, int amount, int grenades)[] spec, bool atTheirs, bool wantDrama, bool shelling, int seed, string kind = null)
        {
            var c = new Case { name = name };
            var cfg = new MusicCoreConfig();
            var w = NewWorld(settings, map, seed);
            var fort = Fort(w, atTheirs ? Them : Me); Team side = atTheirs ? Me : Them;
            var held = new List<Held>(); int k = 0;
            foreach (var (cat, type, amount, gren) in spec)
            {
                bool robot = cat == "robot"; double range = w.S.Unit(type)?.attack_radius ?? 120;
                if (!Spot(w, fort, robot, robot ? 1 : 2, range - 12, range, (atTheirs ? 90 : 270) + 30 * k, out int x, out int y, out double got))
                { Expect(c, false, $"no free spot for {type} {S(range - 12)}-{S(range)} px off the {fort.Owner} fort's walls"); return c; }
                var o = Make(w, cat, type, amount, x, y, side, gren); Order(w, o, fort); k++;
                held.Add(new Held { o = o, target = fort, reload = Reload(o) });
                c.notes.Add($"         {side} {type}{(amount > 1 ? " x" + amount : "")}{(gren > 0 ? " with grenades" : "")} {S(Wall(fort, x, y))} px off the {fort.Owner} fort's walls ({S(Centre(fort, x, y))} px from its centre, {got:0} deg), range {S(range)} px");
            }
            var r = HoldRun(w, fort, held, 60, cfg, shelling);
            foreach (var h in held) c.notes.Add($"         {h.o.Type}: {(h.canHit ? "hits the fort every " + S(h.reload) + " s" : "cannot hurt the fort (" + (h.o.CanAttackObject(h.target) ? "out of range" : "no explosives: ZObject.CanAttackObject") + ")")}");
            if (shelling) c.notes.Add("         the fort is also shelled every 2 s by a source that never counts (an enemy howitzer)");
            Expect(c, r.maxUnits == held.Sum(h => 1 + h.o.Minions.Count), $"engaged at the {fort.Owner} fort: {r.maxScore:0.#} pts / {r.maxUnits} unit(s) (every attacker counted, measured to the walls)");
            if (wantDrama)
                Expect(c, r.drama >= 0 && r.firstHit >= 0 && r.drama - r.firstHit >= cfg.enterHold - 0.3 && r.drama - r.firstHit <= cfg.enterHold + 0.3 && r.fort == (atTheirs ? 2 : 1) && (kind == null || r.why.Contains(kind)),
                       $"the drama {(r.drama >= 0 && r.firstHit >= 0 ? S(r.drama - r.firstHit) + " s after the first hit" : "never")} on {(r.fort == 1 ? "our" : r.fort == 2 ? "their" : "no")} fort ({r.why})");
            else
                Expect(c, r.drama < 0, $"no drama in 60 s ({r.hits} hits on the fort){(r.drama >= 0 ? ": it came " + S(r.drama - r.t0) + " s in (" + r.why + ")" : "")}");
            return c;
        }

        /// <summary>LIVE: a light tank inside its range ordered at our fort: the sim's own shell sets the fort's hit record.</summary>
        static Case LiveHit(string settings, string map)
        {
            var c = new Case { name = "live: a light tank's shell sets the fort's hit record" };
            var w = NewWorld(settings, map, 21); var fort = Fort(w, Me);
            if (!Spot(w, fort, false, 2, 90, 110, 270, out int x, out int y, out double got)) { Expect(c, false, "no free spot 90-110 px off the walls"); return c; }
            var t = Make(w, "vehicle", "light", 1, x, y, Them); bool sent = Order(w, t, fort); double t0 = w.Time, fired = -1;
            while (w.Time < t0 + 5)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events) if (e.type == ZEventType.MissileFired && e.objId == t.Id && fired < 0) fired = w.Time;
                w.Events.Clear();
            }
            Expect(c, sent && fired >= 0 && fort.LastEnemyHit >= t0, $"order {(sent ? "taken" : "REFUSED")}, {S(Wall(fort, x, y))} px off the walls: shell fired {(fired >= 0 ? S(fired - t0) + " s" : "never")}, fort hit record {(fort.LastEnemyHit >= t0 ? S(fort.LastEnemyHit - t0) + " s" : "never set")} after the order");
            return c;
        }

        /// <summary>LIVE: a red and a blue grunt squad ordered at each other two screens (>= 640 px) from both forts' walls, 40 s.</summary>
        static Case FarFight(string settings, string map)
        {
            var c = new Case { name = "live: two squads fighting two screens away" };
            var cfg = new MusicCoreConfig();
            var w = NewWorld(settings, map, 31);
            var ours = Fort(w, Me); var theirs = Fort(w, Them);
            int ax = 0, ay = 0; bool ok = false; double best = 0;
            for (int ty = 0; ty < w.Map.height; ty++) for (int tx = 0; tx < w.Map.width; tx++)
            {
                if (!w.Path.TilePassable(tx, ty, true) || !w.Path.TilePassable(tx + 1, ty, true) || !w.Path.TilePassable(tx, ty + 1, true)) continue;
                int px = tx * 16 + 8, py = ty * 16 + 8; double dd = Math.Min(Wall(ours, px, py), Wall(theirs, px, py));
                if (dd >= 640 && dd > best && Math.Abs(dd - 700) < 200) { best = dd; ax = px; ay = py; ok = true; }
            }
            if (!ok) { Expect(c, false, "no open spot 640 px from both forts"); return c; }
            int bx = ax + 5 * 16, by = ay;
            for (int dx = 4; dx <= 8 && !w.Path.TilePassable(bx / 16, by / 16, true); dx++) bx = ax + dx * 16;
            var red = Make(w, "robot", "grunt", 3, ax, ay, Me); var blue = Make(w, "robot", "grunt", 3, bx, by, Them);
            Order(w, red, blue); Order(w, blue, red);
            c.notes.Add($"         a red and a blue grunt squad {S(bx - ax)} px apart, {S(Math.Min(Wall(ours, ax, ay), Wall(theirs, ax, ay)))} px from the nearest fort wall, ordered at each other");
            int hits = 0; var d = new DramaDetector(); bool drama = false; float maxO = 0, maxT = 0; double next = w.Time + 0.25, end = w.Time + 40;
            while (w.Time < end)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events) if (e.type == ZEventType.Hitscan && e.flag) hits++;
                w.Events.Clear();
                if (w.Time + 1e-9 < next) continue;
                next += 0.25;
                var o = MusicFortReader.Read(w, Me, cfg); var t = MusicFortReader.Read(w, Them, cfg); maxO = Math.Max(maxO, o.score); maxT = Math.Max(maxT, t.score);
                if (d.Step(0.25f, o, t, cfg) && d.Wanted) drama = true;
            }
            Expect(c, hits > 0, $"they fight: {hits} hits in 40 s");
            Expect(c, !drama && maxO == 0f && maxT == 0f, $"no drama, engaged at our fort {maxO:0.#} pts, at theirs {maxT:0.#} pts (a fight away from a fort is never a fort attack)");
            return c;
        }

        /// <summary>LIVE (L17, the fort area measured from the walls): our grunt squad by the flag nearest our (red) fort (L17 tile 72,41: 138 px
        /// off the fort's west wall, 216 px from its centre) and two blue grunt squads further out ordered at it, 40 s; nobody can hurt the
        /// fort (no grenades). The fight lies inside rIn (256 px of the walls) but outside the fort area (rCore 96 px of the walls): nothing
        /// engaged, no drama. The same fight read with rCore = 176 px FROM THE WALLS (the old 11 tiles kept as a wall distance) counts 6
        /// pts / 6 units, a big attack: so this case tells the two apart.</summary>
        static Case FlagFight(string settings, string map)
        {
            var c = new Case { name = "live: L17, two enemy squads attack our squad by the flag 138 px off our fort's west wall" };
            var cfg = new MusicCoreConfig(); var wide = new MusicCoreConfig { rCore = 176f };
            var w = NewWorld(settings, map, 61); var fort = Fort(w, Me);
            var flag = w.Objects.Where(o => o.Type == "flag" && !o.Destroyed).OrderBy(o => Wall(fort, o.CenterX, o.CenterY)).FirstOrDefault();
            if (flag == null) { Expect(c, false, "no flag on the map"); return c; }
            double fdeg = Math.Atan2(flag.CenterY - fort.CenterY, flag.CenterX - fort.CenterX) * 180.0 / Math.PI;
            c.notes.Add($"         the flag nearest our fort: tile {flag.CenterX / 16},{flag.CenterY / 16}, {S(Wall(fort, flag.CenterX, flag.CenterY))} px off the walls, {S(Centre(fort, flag.CenterX, flag.CenterY))} px from the centre, {(fdeg + 360) % 360:0} deg");
            if (!Spot(w, fort, true, 1, 128, 150, fdeg, out int ox, out int oy, out double og)) { Expect(c, false, "no free spot 128-150 px off the walls by the flag"); return c; }
            if (!Spot(w, fort, true, 1, 176, 196, fdeg - 6, out int ax, out int ay, out double ag) || !Spot(w, fort, true, 1, 200, 224, fdeg + 6, out int bx, out int by, out double bg))
            { Expect(c, false, "no free spots 176-224 px off the walls for the blue squads"); return c; }
            var mine = Make(w, "robot", "grunt", 3, ox, oy, Me); var b1 = Make(w, "robot", "grunt", 3, ax, ay, Them); var b2 = Make(w, "robot", "grunt", 3, bx, by, Them);
            Order(w, b1, mine); Order(w, b2, mine); Order(w, mine, b1);
            double Near(ZObject sq) => new[] { sq }.Concat(sq.Minions).Min(u => Wall(fort, u.CenterX, u.CenterY));
            c.notes.Add($"         our squad {S(Wall(fort, ox, oy))} px off the walls ({S(Centre(fort, ox, oy))} px from the centre, {og:0} deg); blue squads {S(Wall(fort, ax, ay))} and {S(Wall(fort, bx, by))} px off the walls ({S(Math.Sqrt((double)(ax - ox) * (ax - ox) + (double)(ay - oy) * (ay - oy)))} / {S(Math.Sqrt((double)(bx - ox) * (bx - ox) + (double)(by - oy) * (by - oy)))} px from ours), ordered at ours");
            int hits = 0; var d = new DramaDetector(); var dw = new DramaDetector(); bool drama = false; string wideWhy = null; float maxO = 0, maxW = 0; int maxWU = 0; double minOurs = 1e9;
            double t0 = w.Time, next = w.Time + 0.25, end = w.Time + 40;
            while (w.Time < end)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events) if (e.type == ZEventType.Hitscan && e.flag) hits++;
                w.Events.Clear();
                if (w.Time + 1e-9 < next) continue;
                next += 0.25;
                minOurs = Math.Min(minOurs, Near(mine));
                var o = MusicFortReader.Read(w, Me, cfg); var t = MusicFortReader.Read(w, Them, cfg); maxO = Math.Max(maxO, o.score);
                if (d.Step(0.25f, o, t, cfg) && d.Wanted) drama = true;
                var ow = MusicFortReader.Read(w, Me, wide); var tw = MusicFortReader.Read(w, Them, wide); maxW = Math.Max(maxW, ow.score); maxWU = Math.Max(maxWU, ow.units);
                if (dw.Step(0.25f, ow, tw, wide) && dw.Wanted && wideWhy == null) wideWhy = dw.Why;
            }
            Expect(c, hits > 0 && fort.LastEnemyHit < t0, $"they fight: {hits} hits in 40 s; our fort {(fort.LastEnemyHit < t0 ? "never hit" : "HIT")}; our squad stays {S(minOurs)} px or more off the walls");
            Expect(c, !drama && maxO == 0f, $"rCore {cfg.rCore:0} px from the walls (the kit): engaged at our fort {maxO:0.#} pts, {(drama ? "a DRAMA" : "no drama")} (a fight by the flag is not a fort attack)");
            Expect(c, wideWhy != null && maxW >= 6f, $"the same fight read with rCore 176 px from the walls: {maxW:0.#} pts / {maxWU} units engaged, {(wideWhy != null ? "a drama (" + wideWhy + ")" : "no drama")} (the case tells the two apart)");
            return c;
        }

        /// <summary>HELD: a lone missile launcher at its full 160 px range off our fort's walls, from six DIFFERENT spots (a direction whose
        /// nearest free spot is one already used, or none, passes to the next direction of the list).</summary>
        static Case FullRange(string settings, string map)
        {
            var c = new Case { name = "a missile launcher shelling our fort from its full range (160 px off the walls), six different spots" };
            var cfg = new MusicCoreConfig();
            int pastOld = 0, n = 0, tries = 0; var used = new List<(int x, int y)>();
            foreach (var deg in new[] { 270.0, 225.0, 315.0, 180.0, 0.0, 250.0, 135.0, 290.0, 200.0, 90.0, 45.0 })
            {
                if (n >= 6) break;
                var w = NewWorld(settings, map, 41 + tries); tries++;
                var fort = Fort(w, Me);
                if (!Spot(w, fort, false, 2, 152, 160, deg, out int x, out int y, out double got)) { c.notes.Add($"         asked {deg:0} deg: no free spot 152-160 px off the walls (the next direction)"); continue; }
                if (used.Any(p => Math.Abs(p.x - x) + Math.Abs(p.y - y) < 32)) { c.notes.Add($"         asked {deg:0} deg: its nearest free spot ({got:0} deg) is one already used (the next direction)"); continue; }
                used.Add((x, y)); n++;
                var ml = Make(w, "vehicle", "missile_launcher", 1, x, y, Them); Order(w, ml, fort);
                var r = HoldRun(w, fort, new List<Held> { new Held { o = ml, target = fort, reload = Reload(ml) } }, 45, cfg);
                double wall = Wall(fort, x, y), centre = Centre(fort, x, y); bool oldIn = centre <= cfg.rIn; if (!oldIn) pastOld++;
                Expect(c, r.maxUnits == 1 && r.maxScore >= cfg.wMissile && r.hits > 0,
                       $"from {got:0} deg: {S(wall)} px off the walls, {S(centre)} px from the centre: engaged {r.maxScore:0.#} pts / {r.maxUnits} unit, {r.hits} hits (the old centre rule: {(oldIn ? "counted too" : "NOT counted, past " + cfg.rIn.ToString("0", IC) + " px")})");
                Expect(c, r.drama >= 0 && r.firstHit >= 0 && r.drama - r.firstHit >= cfg.enterHold - 0.3 && r.drama - r.firstHit <= cfg.enterHold + 0.3, $"from {got:0} deg: the lone launcher (4 pts) hitting: the drama {(r.drama >= 0 && r.firstHit >= 0 ? S(r.drama - r.firstHit) + " s after the first hit" : "never")}");
            }
            Expect(c, n == 6, $"{n} different spots (want 6)");
            c.notes.Add($"         the old centre rule would have dropped the launcher in {pastOld} of these {n} different spots");
            return c;
        }

        /// <summary>HELD: a lone enemy light tank 60-90 px off our fort's walls fighting our robot at the gate (engaged, 3 pts) while the fort itself
        /// is never hit: no drama, the hit is what makes a fort under fire. The same with the fort shelled every 2 s by a source that never counts (an
        /// enemy howitzer): the drama about 3 s after the first of those hits. So it tells "engaged" and "hit" apart (the other held cases have both on
        /// one step); it does not pin the 4 s window's length (any window >= 2 s passes): MusicCoreScenarios' hold case (c) does (64.0 s = 4 + 60, +-0.5 s).</summary>
        static Case TankAtGate(string settings, string map)
        {
            var c = new Case { name = "a lone light tank fighting our robot at the gate: 3 pts engaged, the fort never hit -> none; the fort shelled -> drama" };
            var cfg = new MusicCoreConfig();
            foreach (bool shelled in new[] { false, true })
            {
                var w = NewWorld(settings, map, shelled ? 72 : 71); var fort = Fort(w, Me);
                fort.GetCreationMovePoint(out int gx, out int gy);
                var guard = Make(w, "robot", "grunt", 1, gx, gy, Me);
                if (!Spot(w, fort, false, 2, 60, 90, 270, out int x, out int y, out double got)) { Expect(c, false, "no free spot 60-90 px off the walls"); return c; }
                var tank = Make(w, "vehicle", "light", 1, x, y, Them); Order(w, tank, guard);
                var r = HoldRun(w, fort, new List<Held> { new Held { o = tank, target = guard, reload = Reload(tank) } }, 60, cfg, shelled);
                string head = $"light tank {S(Wall(fort, x, y))} px off the walls attacking our robot at the gate: engaged {r.maxScore:0.#} pts / {r.maxUnits} unit";
                if (!shelled)
                    Expect(c, r.maxScore == cfg.wLight && r.maxUnits == 1 && fort.LastEnemyHit < r.t0 && r.drama < 0,
                           $"{head}, the fort {(fort.LastEnemyHit < r.t0 ? "never hit" : "HIT")}: {(r.drama < 0 ? "no drama" : "a drama (" + r.why + ")")} in 60 s");
                else
                    Expect(c, r.maxScore == cfg.wLight && r.drama >= 0 && r.drama - r.t0 >= cfg.enterHold - 0.3 && r.drama - r.t0 <= cfg.enterHold + 0.6 && r.why.Contains("fort under fire"),
                           $"{head}, the fort shelled every 2 s from 0.25 s by an uncounted howitzer: {(r.drama >= 0 ? "the drama " + S(r.drama - r.t0) + " s in (" + r.why + ")" : "no drama")}");
            }
            return c;
        }

        /// <summary>HELD: our fort's own unit at its gate and a lone enemy jeep attacking it, while the fort is shelled by a howitzer.</summary>
        static Case JeepAtGate(string settings, string map)
        {
            var c = new Case { name = "a lone jeep at our fort while the fort is shelled (2 pts engaged, fort under fire)" };
            var cfg = new MusicCoreConfig();
            var w = NewWorld(settings, map, 51); var fort = Fort(w, Me);
            fort.GetCreationMovePoint(out int gx, out int gy);
            var guard = Make(w, "robot", "grunt", 1, gx, gy, Me);
            if (!Spot(w, fort, false, 2, 60, 90, 270, out int x, out int y, out double got)) { Expect(c, false, "no free spot 60-90 px off the walls"); return c; }
            var jeep = Make(w, "vehicle", "jeep", 1, x, y, Them); Order(w, jeep, guard);
            var r = HoldRun(w, fort, new List<Held> { new Held { o = jeep, target = guard, reload = Reload(jeep) } }, 60, cfg, true);
            Expect(c, r.maxScore == cfg.wJeep && r.maxUnits == 1 && r.drama < 0, $"jeep {S(Wall(fort, x, y))} px off the walls attacking our robot at the gate: engaged {r.maxScore:0.#} pts / {r.maxUnits} unit, the fort under fire: {(r.drama < 0 ? "no drama" : "a drama (" + r.why + ")")} in 60 s");
            return c;
        }

        // ------------------------------------------------------------------------------------------------ run
        public static bool RunAll(string settings, Func<string, string> mapText, Action<string> log)
        {
            var cases = new List<Case>();
            var saved = SaveSwitches();
            PlaySwitches();
            try
            {
                string map = mapText("L02"), map17 = mapText("L17");
                if (string.IsNullOrEmpty(map) || string.IsNullOrEmpty(map17)) { log("MUSIC DRAMA READER SCENARIOS FAIL: L02 / L17 map missing"); return false; }
                void Do(Func<Case> f) { Case r; try { r = f(); } catch (Exception ex) { r = new Case { name = f.Method.Name }; r.pass = false; r.notes.Add("  FAIL exception " + ex); } cases.Add(r); }
                Do(() => LiveHit(settings, map));
                Do(() => AtFort(settings, map, "a lone light tank hitting our fort", new[] { ("vehicle", "light", 1, 0) }, false, true, false, 11, "fort under fire"));
                Do(() => AtFort(settings, map, "one grunt squad (3 robots, grenades) hitting our fort", new[] { ("robot", "grunt", 3, 6) }, false, true, false, 12, "fort under fire"));
                Do(() => AtFort(settings, map, "a Tough squad (2 robots, rockets) hitting our fort", new[] { ("robot", "tough", 2, 0) }, false, true, false, 16, "fort under fire"));
                Do(() => AtFort(settings, map, "a lone Tough robot hitting our fort", new[] { ("robot", "tough", 1, 0) }, false, false, false, 17));
                Do(() => AtFort(settings, map, "a lone jeep ordered at our fort", new[] { ("vehicle", "jeep", 1, 0) }, false, false, false, 13));
                Do(() => AtFort(settings, map, "a lone robot (grenades) hitting our fort", new[] { ("robot", "grunt", 1, 3) }, false, false, false, 14));
                Do(() => JeepAtGate(settings, map));
                Do(() => TankAtGate(settings, map));                                                   // round 2: the hit window, engaged without a hit
                Do(() => FarFight(settings, map));
                Do(() => FlagFight(settings, map17));
                Do(() => FullRange(settings, map));
                Do(() => AtFort(settings, map, "our lone light tank hitting THEIR fort", new[] { ("vehicle", "light", 1, 0) }, true, true, false, 18, "fort under fire"));
                Do(() => AtFort(settings, map, "our light tank + one grunt squad (grenades) hitting THEIR fort", new[] { ("vehicle", "light", 1, 0), ("robot", "grunt", 3, 6) }, true, true, false, 15, "big attack"));
            }
            finally { foreach (var (f, v) in saved) f.SetValue(null, v); }
            var sb = new StringBuilder("music_drama fort reader scenarios (L02 and L17, real headless worlds: View/MusicFortReader.cs + DramaDetector)\n");
            foreach (var r in cases) { sb.AppendLine($"{(r.pass ? "PASS" : "FAIL")}  {r.name}"); foreach (var x in r.notes) sb.AppendLine("    " + x); }
            int fails = cases.Count(x => !x.pass);
            sb.AppendLine(fails == 0 ? $"MUSIC DRAMA READER SCENARIOS OK ({cases.Count}/{cases.Count} PASS)" : $"MUSIC DRAMA READER SCENARIOS FAIL ({fails} of {cases.Count})");
            log(sb.ToString());
            return fails == 0;
        }
    }
}
