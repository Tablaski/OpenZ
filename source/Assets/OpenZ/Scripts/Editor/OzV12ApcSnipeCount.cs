using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.2 item 69 counted check (rule DV143, Sim/ZObject.ApcSnipe.cs: the APC's lone driver can be sniped while he is up in the roof hatch).
    /// Headless sim, no scene, no Play mode (menu OpenZ/V1.2/Combat: 69 APC Driver Snipe Check (headless), Editor/OzApcSnipeMenu.cs; the
    /// off-editor harness runs the same code). The rule switch is set by name, so the same file runs on the rules without the kit too (its
    /// "off" rows must then be the same). Every switch is put back afterwards.
    /// <list type="bullet">
    /// <item>F fights on a plain map: one attacker unit (a full squad for robots) against one blue APC with its driver (a grunt) and nobody
    /// else aboard, which stands and fires back (DV87 part C: he comes up through the roof hatch), everything mortal, the switches of a game
    /// in Play; 40 fights per attacker, the rule off (the game until now) and on. 80 px apart (inside both ranges), and a walk-in from
    /// 300 px (the attacker stops where its own range lets it fire).</item>
    /// <item>L the same fights on level 2 (its ground, without its units), at the open spot farthest from its buildings.</item>
    /// <item>P / U unchanged: an APC with a grunt squad aboard as passengers, a veteran crew, an APC on a standing bridge: the rule on is
    /// tick for tick the rule off (the driver never comes up, the APC can never be sniped).</item>
    /// <item>R rules: when the APC can be sniped (driver inside, up, back in, 'Zod exact', empty, a squad boarding), the HP of one driver
    /// hit on it per attacker, every other target's driver hit unchanged.</item>
    /// <item>E the emptied APC: neutral, no driver, no orders, stopped, enterable; a red grunt squad takes it (its leader drives, the others
    /// ride, DV15).</item>
    /// <item>D deterministic: fights run twice give the same events.</item>
    /// </list>
    /// </summary>
    public static class OzV12ApcSnipeCount
    {
        public const int Fights = 40, SeedBase = 6900, LevelSeeds = 100;
        const int CX = 640, CY = 640, Gap = 80, FarGap = 300, Big = 1000000000;
        const double FightCap = 90;
        const string Rule = "ApcSnipeDeviation";

        /// <summary>The attackers counted: every robot, the jeep and the gatling (Zod's list of who can snipe: grunt, psycho, sniper, pyro, laser,
        /// jeep, gatling; the Tough cannot).</summary>
        public static readonly string[] Attackers = { "sniper", "jeep", "grunt", "psycho", "laser", "gatling", "pyro", "tough" };

        /// <summary>The rules of a game in Play that OzV11Kit.GameSwitches leaves off for the older checks and that act in a fight.</summary>
        public static readonly string[] PlayRules =
        {
            "DriverFireDeviation", "SniperBalanceDeviation", "SniperSteadyDeviation", "SniperHeadshotDeviation", "ProneTargetDeviation", "JeepChargeDeviation",
            "ToughSteadyDeviation", "GroundFireDeviation", "GrenadeBoostDeviation", "CalmMoveDeviation", "CalmFollowDeviation", "NoAutoFlagUnderFireDeviation",
            "VeteranHealDeviation", "FortRunDeviation", "RockRoundDeviation", "CraneSnipeDeviation",
        };

        /// <summary>The switches of a game in Play (OzV11Kit.GameSwitches + PlayRules), the rule as asked. Dispose to put everything back.</summary>
        public static OzV11Kit.Switches Game(bool rule)
        {
            var s = OzV11Kit.GameSwitches();
            foreach (var n in PlayRules) OzV11Kit.TrySet(typeof(ZWorld), n, true);
            OzV11Kit.TrySet(typeof(ZWorld), Rule, rule);
            return s;
        }

        public static bool RuleInBuild => typeof(ZWorld).GetField(Rule) != null;
        static void SetRule(bool on) => OzV11Kit.TrySet(typeof(ZWorld), Rule, on);

        static string flat, bridge;
        static string Flat => flat ??= OzV11Kit.Plain(96, 96);
        static string BridgeMap => bridge ??= OzV11Kit.Plain(96, 96, ("building:bridge_vert", 20, 20, "null"));

        /// <summary>A place of a fight: the map, where the APC stands (its centre), and where the two stand-ins stand that keep the game from
        /// being over (a blue and a red grunt that cannot move, fire or die).</summary>
        public sealed class Ground
        {
            public string name, map; public int ax, ay, bx, by, rx, ry;
        }

        static Ground plain;
        static Ground Plain => plain ??= new Ground { name = "plain map", map = Flat, ax = CX + Gap, ay = CY, bx = 1440, by = 1440, rx = 120, ry = 1440 };

        /// <summary>Level 2 without its units (OzV11Kit.NoUnits: its ground, buildings, bridges, flags and map items; the blue buildings
        /// nobody's): the APC at the open spot farthest from every object (rocks aside) with open ground for the attacker gap px west of it
        /// and around both; the stand-ins near the two forts. Null without the level's map.</summary>
        static Ground Level(string settings, string levelMap, string name, int gap)
        {
            if (string.IsNullOrEmpty(levelMap)) return null;
            string map = OzV11Kit.NoUnits(levelMap);
            var w = ZWorld.Create(settings, map, 1);
            var things = w.Objects.Where(o => !o.Destroyed && o.Type != "rock").ToList();
            int W = w.Path.W, H = w.Path.H;
            bool Open(int x0, int y0, int x1, int y1)
            {
                for (int ty = y0; ty <= y1; ty++) for (int tx = x0; tx <= x1; tx++) if (!w.Path.TilePassable(tx, ty, true) || !w.Path.TilePassable(tx, ty, false)) return false;
                return true;
            }
            long best = -1; int bx = 0, by = 0;
            for (int ty = 4; ty < H - 4; ty += 2)
                for (int tx = 4; tx < W - 4; tx += 2)
                {
                    int px = tx * 16 + 16, py = ty * 16 + 16;                                                   // the APC's centre (its 2 x 2 tiles from tx, ty)
                    if (!Open((px - gap - 48) / 16, (py - 48) / 16, (px + 48) / 16, (py + 48) / 16)) continue;
                    long near = long.MaxValue;
                    foreach (var o in things) { long dx = o.CenterX - px, dy = o.CenterY - py, d = dx * dx + dy * dy; if (d < near) near = d; }
                    if (near > best) { best = near; bx = px; by = py; }
                }
            if (best < 0) return null;
            (int, int) Near(string fort)
            {
                var f = w.Objects.FirstOrDefault(o => o.Type == fort);
                int fx = f != null ? f.CenterX / 16 : 2, fy = f != null ? f.CenterY / 16 : 2;
                for (int r = 6; r < 40; r++)
                    for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && w.Path.TilePassable(fx + dx, fy + dy, true)) return ((fx + dx) * 16 + 8, (fy + dy) * 16 + 8);
                return (fx * 16 + 8, fy * 16 + 8);
            }
            var (blx, bly) = Near("fort_front"); var (rlx, rly) = Near("fort_back");
            return new Ground { name = $"{name} (APC at {bx},{by} px, {Math.Sqrt(best) / 16:0} tiles from the nearest object)", map = map, ax = bx, ay = by, bx = blx, by = bly, rx = rlx, ry = rly };
        }

        /// <summary>A world in which blue is a CPU team that gives no orders (red = the player), with a blue and a red grunt that cannot move,
        /// fire or die: the game is never over (DV10 would stop every unit) while one side has nothing else on the map.</summary>
        static ZWorld NewWorld(string settings, Ground g, int seed)
        {
            var w = OzV11Kit.World(settings, g.map, seed);
            OzV11Kit.Dummy(w, g.bx, g.by);
            var red = w.CreateRobotGroup("grunt", g.rx - 8, g.ry - 8, Team.Red, 1);
            red.MoveSpeed = 0; red.RealMoveSpeed = 0; red.Damage = 0; red.MaxHealth = red.Health = Big;
            w.Events.Clear();
            return w;
        }

        static ulong Mix(ulong h, long v) { h ^= (ulong)v; h *= 1099511628211UL; h ^= h >> 31; return h; }

        /// <summary>The kind of APC fought: its driver alone (the bug), a grunt squad aboard as passengers, a veteran crew.</summary>
        public enum Crew { Alone, Passengers, Veteran }

        static ZObject Apc(ZWorld w, int cx, int cy, Crew crew)
        {
            var t = OzV11Kit.Place(w, "apc", cx, cy, Team.Blue);
            if (crew == Crew.Passengers) { t.PassengerType = "grunt"; for (int i = 0; i < 3; i++) t.Passengers.Add(w.S.Unit("grunt").MaxHealth); t.ResetDamageInfo(); }
            if (crew == Crew.Veteran) t.Veteran = true;
            return t;
        }

        // ================================================================================================ F: fights
        public sealed class Tally
        {
            public int fights, sniped, hull, lost, none, upFights, emptyOk, insideHits, apcShots, driverHits, hits, exposedTicks, upTicks;
            public double time, minDist;
            public ulong hash = 1469598103934665603UL;
            public double Mean => sniped > 0 ? time / sniped : 0;
            public string Key => $"{sniped}/{hull}/{lost}/{none}/{upFights}/{driverHits}/{apcShots}/{hits}/{exposedTicks}/{hash:x16}";
        }

        /// <summary>One attacker unit (a squad for robots) gap px west of one blue APC, the player's attack order, everything mortal.</summary>
        public static Tally Fight(string settings, string attacker, int gap, int fights, int seedBase, Crew crew = Crew.Alone, Ground g = null)
        {
            g ??= Plain;
            var r = new Tally { fights = fights, minDist = 1e9 };
            for (int s = 1; s <= fights; s++)
            {
                var w = NewWorld(settings, g, seedBase + s);
                var apc = Apc(w, g.ax, g.ay, crew);
                var a = OzV11Kit.Place(w, attacker, g.ax - gap, g.ay, Team.Red);
                var squad = OzV11Kit.Squad(a).ToList();
                OzV11Kit.PlayerAttack(w, a, apc);
                int outcome = 3; bool up = false;
                while (w.Time < FightCap && outcome == 3)
                {
                    bool before = apc.CanBeSniped();
                    w.Tick(ZConst.TICK);
                    bool after = apc.CanBeSniped();
                    if (apc.ApcDriverUp) { up = true; r.upTicks++; }
                    if (after) r.exposedTicks++;
                    foreach (var m in squad) if (OzV11Kit.Alive(m)) r.minDist = Math.Min(r.minDist, OzV11Kit.Dist(m, apc));
                    foreach (var e in w.Events)
                    {
                        r.hash = Mix(Mix(Mix(r.hash, (long)e.type), e.objId), e.otherId);
                        if (e.type == ZEventType.Hitscan && e.objId == apc.Id) r.apcShots++;
                        if (e.type == ZEventType.Hitscan && e.otherId == apc.Id && e.flag) r.hits++;
                        if ((e.type == ZEventType.DriverHit || e.type == ZEventType.Sniped) && e.objId == apc.Id) { r.driverHits++; if (!before && !after) r.insideHits++; }
                        if (e.objId != apc.Id || outcome != 3) continue;
                        if (e.type == ZEventType.Sniped) outcome = 0;
                        else if (e.type == ZEventType.Death) outcome = 1;
                    }
                    w.Events.Clear();
                    if (outcome == 0)
                    {
                        w.Tick(ZConst.TICK); w.Events.Clear();                                                  // the server pass of the snipe has run: look at the hull now
                        if (apc.Owner == Team.Neutral && apc.Drivers.Count == 0 && apc.Waypoints.Count == 0 && !apc.IsMoving && apc.CanBeEntered() && !apc.CanBeSniped() && !apc.ApcDriverUp) r.emptyOk++;
                        break;
                    }
                    if (outcome != 3) break;
                    bool lost = a.Kind == ZKind.Robot ? squad.All(m => m.Destroyed) : a.Destroyed || a.Owner == Team.Neutral;
                    if (lost) outcome = 2;
                }
                if (up) r.upFights++;
                if (outcome == 0) { r.sniped++; r.time += w.Time; }
                else if (outcome == 1) r.hull++;
                else if (outcome == 2) r.lost++;
                else r.none++;
                r.hash = Mix(r.hash, outcome);
            }
            return r;
        }

        static string Row(string a, string where, int gap, bool on, Tally t) =>
            $"{a} -> APC | {where} | {gap} px | {(on ? "on " : "off")} | {t.sniped}/{t.fights} | {t.Mean:0.0} | {t.hull}/{t.fights} | {t.lost}/{t.fights} | {t.none}/{t.fights} | {t.upFights}/{t.fights} | {t.driverHits} | {t.insideHits} | {t.apcShots} | {t.hits} | {t.minDist:0}";

        const string Head = "attacker -> target | ground | start gap | rule | driver sniped | mean s to the snipe | hull destroyed first | attackers lost | no result | fights with the driver up | driver hits (incl. the last) | of them while he was inside | APC shots | hits on the APC | nearest attacker px";

        // ================================================================================================ the whole check
        /// <summary>The whole check (settings = default_settings, l02 = level 2's map, or null to leave the level out). fights per row;
        /// quick = the 80 px rows of sniper, jeep and grunt on the plain map only.</summary>
        public static string Check(string settings, string l02, int fights = Fights, bool quick = false, Action<string> progress = null)
        {
            var sb = new StringBuilder();
            bool inBuild = RuleInBuild;
            using (Game(false))
            {
                sb.AppendLine($"[OpenZ 1.2 item 69] the APC's lone driver can be sniped while he is up in the roof hatch (DV143): rule switch in this build {(inBuild ? "yes" : "NO (only the 'off' rows run)")}; {fights} fights per row (seeds {SeedBase + 1}-{SeedBase + fights}), cap {FightCap:0} s, the switches of a game in Play");
                var res = new Dictionary<(string, string, int, bool), Tally>();
                var rows = quick ? new[] { "sniper", "jeep", "grunt" } : Attackers;
                var rules = inBuild ? new[] { false, true } : new[] { false };
                sb.AppendLine();
                sb.AppendLine("F fights, an APC with its driver alone: " + Head);
                foreach (int gap in quick ? new[] { Gap } : new[] { Gap, FarGap })
                    foreach (var a in rows)
                        foreach (bool on in rules)
                        {
                            if (gap == FarGap && a != "sniper" && a != "jeep" && a != "grunt" && a != "laser") continue;
                            SetRule(on);
                            var t = Fight(settings, a, gap, fights, SeedBase);
                            res[(a, "plain", gap, on)] = t;
                            sb.AppendLine(Row(a, "plain", gap, on, t));
                            progress?.Invoke($"{a} {gap} px rule {(on ? "on" : "off")}: sniped {t.sniped}/{fights}");
                        }
                Ground lv = quick ? null : Level(settings, l02, "level 2", Gap);
                if (lv != null)
                {
                    sb.AppendLine($"L the same on {lv.name}, seeds {SeedBase + LevelSeeds + 1}-{SeedBase + LevelSeeds + fights}:");
                    foreach (var a in new[] { "sniper", "jeep", "grunt" })
                        foreach (bool on in rules)
                        {
                            SetRule(on);
                            var t = Fight(settings, a, Gap, fights, SeedBase + LevelSeeds, Crew.Alone, lv);
                            res[(a, "L02", Gap, on)] = t;
                            sb.AppendLine(Row(a, "L02", Gap, on, t));
                            progress?.Invoke($"{a} on level 2 rule {(on ? "on" : "off")}: sniped {t.sniped}/{fights}");
                        }
                }
                else if (!quick) sb.AppendLine("L level 2: no map given (only the plain map ran)");
                SetRule(false);
                sb.AppendLine();
                Tally T(string a, string where, int gap, bool on) => res.TryGetValue((a, where, gap, on), out var t) ? t : null;
                int offSnipes = res.Where(kv => !kv.Key.Item4).Sum(kv => kv.Value.sniped), offRows = res.Count(kv => !kv.Key.Item4);
                int offUp = res.Where(kv => !kv.Key.Item4 && kv.Key.Item3 == Gap).Sum(kv => kv.Value.upFights), offUpOf = res.Count(kv => !kv.Key.Item4 && kv.Key.Item3 == Gap) * fights;
                sb.AppendLine(OzV11Kit.Verdict(offSnipes == 0 && offUp > 0, $"F0 before (rule off, the game until now): the lone driver comes up to fire ({offUp} of {offUpOf} fights at {Gap} px) and is never sniped ({offSnipes} in {offRows * fights} fights)"));
                if (inBuild)
                {
                    var want = new Dictionary<string, int> { ["sniper"] = 36, ["jeep"] = 32, ["grunt"] = 24, ["psycho"] = 32, ["laser"] = 32, ["gatling"] = 26 };
                    foreach (var a in rows.Where(want.ContainsKey))
                    {
                        var t = T(a, "plain", Gap, true); int need = (int)Math.Ceiling(want[a] * fights / 40.0);
                        sb.AppendLine(OzV11Kit.Verdict(t != null && t.sniped >= need, $"F1 {a} at {Gap} px, rule on: the driver sniped in {t?.sniped}/{fights} fights (at least {need}), mean {t?.Mean:0.0} s"));
                    }
                    foreach (var a in rows.Where(x => x == "pyro" || x == "tough"))
                    {
                        var t = T(a, "plain", Gap, true);
                        sb.AppendLine(OzV11Kit.Verdict(t != null && t.sniped == 0, $"F2 {a}, rule on: never snipes ({t?.sniped}/{fights}; {(a == "pyro" ? "Zod's attack_snipe_chance 0" : "Zod: cannot snipe")})"));
                    }
                    var sn = T("sniper", "plain", Gap, true);
                    var others = rows.Where(x => x != "sniper" && want.ContainsKey(x)).Select(x => (x, t: T(x, "plain", Gap, true))).Where(x => x.t != null && x.t.sniped > 0).ToList();
                    sb.AppendLine(OzV11Kit.Verdict(sn != null && sn.sniped > 0 && others.All(x => x.t.Mean > sn.Mean), $"F3 snipers are the fastest APC-driver killers: {sn?.Mean:0.0} s; " + string.Join(", ", others.Select(x => $"{x.x} {x.t.Mean:0.0} s"))));
                    int inside = res.Where(kv => kv.Key.Item4).Sum(kv => kv.Value.insideHits), driverOn = res.Where(kv => kv.Key.Item4).Sum(kv => kv.Value.driverHits);
                    sb.AppendLine(OzV11Kit.Verdict(inside == 0 && driverOn > 0, $"F4 rule on: a driver hit only while the driver is up ({driverOn} driver hits, {inside} while he was inside)"));
                    int snOn = res.Where(kv => kv.Key.Item4).Sum(kv => kv.Value.sniped), okOn = res.Where(kv => kv.Key.Item4).Sum(kv => kv.Value.emptyOk);
                    sb.AppendLine(OzV11Kit.Verdict(snOn > 0 && okOn == snOn, $"F5 every sniped APC is empty: neutral, no driver, no orders, stopped, enterable, nobody up, not snipable ({okOn} of {snOn})"));
                    if (!quick)
                    {
                        var far = T("sniper", "plain", FarGap, true);
                        sb.AppendLine(OzV11Kit.Verdict(far != null && far.insideHits == 0, $"F6 snipers sent from {FarGap} px stop where they can fire ({far?.minDist:0} px, the driver's rifle reaches 120 px): the driver up in {far?.upFights}/{fights} fights, sniped {far?.sniped}, hull first {far?.hull}, no driver hit while he was inside ({far?.insideHits})"));
                        var jf = T("jeep", "plain", FarGap, true);
                        sb.AppendLine(OzV11Kit.Verdict(jf != null && jf.sniped >= (int)Math.Ceiling(30 * fights / 40.0), $"F7 a jeep sent from {FarGap} px drives into its range and snipes the driver in {jf?.sniped}/{fights} fights, mean {jf?.Mean:0.0} s"));
                        if (lv != null)
                            foreach (var a in new[] { "sniper", "jeep", "grunt" })
                            {
                                var t = T(a, "L02", Gap, true); var p = T(a, "plain", Gap, true); int need = (int)Math.Ceiling(want[a] * fights / 40.0);
                                sb.AppendLine(OzV11Kit.Verdict(t != null && t.sniped >= need && t.insideHits == 0,
                                    $"L1 {a} on level 2, rule on: the driver sniped in {t?.sniped}/{fights} fights (at least {need}), mean {t?.Mean:0.0} s (plain map {p?.sniped}/{fights}, {p?.Mean:0.0} s), no driver hit while he was inside ({t?.insideHits})"));
                            }
                    }
                }
                if (!quick)
                {
                    sb.AppendLine();
                    Unchanged(settings, sb, inBuild, fights);
                    Rules(settings, sb, inBuild);
                    if (inBuild) { Empty(settings, sb); Twice(settings, sb); }
                }
            }
            return sb.ToString();
        }

        /// <summary>The quick form for the parity report's row: the 80 px rows of sniper, jeep and grunt on the plain map, 10 fights each, the
        /// rule lines, and the passengers row of the grunt squad (10 fights, on = off).</summary>
        public static string Quick(string settings)
        {
            var sb = new StringBuilder(Check(settings, null, 10, true));
            using (Game(false)) { Rules(settings, sb, RuleInBuild); if (RuleInBuild) PassengersQuick(settings, sb); }
            return sb.ToString();
        }

        // ================================================================================================ P / U: what must not change
        /// <summary>P: an APC with a grunt squad aboard as passengers (sniper, jeep, grunt at 80 px); U: a veteran crew, and a lone driver on
        /// the deck of a standing bridge (DV82, grunt squad). The rule on must be tick for tick the rule off: the driver never up, the APC never
        /// snipable, no driver hit.</summary>
        static void Unchanged(string settings, StringBuilder sb, bool inBuild, int fights)
        {
            sb.AppendLine("P / U the APC whose driver does not come up (must not change): " + Head);
            var rules = inBuild ? new[] { false, true } : new[] { false };
            foreach (var a in new[] { "sniper", "jeep", "grunt" })
            {
                var t = new Tally[2];
                foreach (bool on in rules) { SetRule(on); t[on ? 1 : 0] = Fight(settings, a, Gap, fights, SeedBase, Crew.Passengers); sb.AppendLine(Row(a, "plain, a grunt squad aboard", Gap, on, t[on ? 1 : 0])); }
                SetRule(false);
                var off = t[0]; var onT = inBuild ? t[1] : t[0];
                sb.AppendLine(OzV11Kit.Verdict(off.upFights == 0 && off.exposedTicks == 0 && off.driverHits == 0 && (!inBuild || onT.Key == off.Key),
                    $"P1 {a} against an APC with a grunt squad aboard: the driver up in {off.upFights}/{fights} fights, snipable {off.exposedTicks} ticks, driver hits {off.driverHits}; " +
                    (inBuild ? $"rule on tick for tick the rule off: {(onT.Key == off.Key ? "yes" : "NO")} (events {off.hash:x16} / {onT.hash:x16}; hull first {off.hull} / {onT.hull}, attackers lost {off.lost} / {onT.lost})" : "rule not in this build")));
            }
            {
                var t = new Tally[2];
                foreach (bool on in rules) { SetRule(on); t[on ? 1 : 0] = Fight(settings, "sniper", Gap, Math.Max(1, fights / 4), SeedBase, Crew.Veteran); sb.AppendLine(Row("sniper", "plain, a veteran crew", Gap, on, t[on ? 1 : 0])); }
                SetRule(false);
                var off = t[0]; var onT = inBuild ? t[1] : t[0];
                sb.AppendLine(OzV11Kit.Verdict(off.upFights == 0 && onT.exposedTicks == 0 && onT.driverHits == 0 && (!inBuild || onT.Key == off.Key),
                    $"U1 a sniper squad against a veteran crew (DV12, buttoned up): driver up {off.upFights}/{off.fights} fights, snipable {onT.exposedTicks} ticks, driver hits {onT.driverHits}; rule on = off {(onT.Key == off.Key ? "yes" : "NO")}"));
            }
            bool safeWas = ZWorld.SnipeSafeSpotDeviation;
            try
            {
                ZWorld.SnipeSafeSpotDeviation = true;
                var keys = new string[2]; int up = 0, snip = 0, dh = 0, shots = 0; bool spot = false;
                foreach (bool on in rules)
                {
                    SetRule(on);
                    var w = NewWorld(settings, Plain0(BridgeMap), 14);
                    var br = w.Objects.First(o => o.IsBridge);
                    int bx = br.LocX + br.WidthPix / 2, by = br.LocY + br.HeightPix / 2;
                    var apc = Apc(w, bx, by, Crew.Alone); apc.MaxHealth = apc.Health = Big;
                    var g = OzV11Kit.Place(w, "grunt", bx + Gap, by, Team.Red);
                    foreach (var m in OzV11Kit.Squad(g)) m.MaxHealth = m.Health = Big;
                    OzV11Kit.PlayerAttack(w, g, apc);
                    ulong h = 1469598103934665603UL;
                    while (w.Time < 20)
                    {
                        w.Tick(ZConst.TICK);
                        if (apc.ApcDriverUp) up++;
                        if (apc.CanBeSniped()) snip++;
                        foreach (var e in w.Events)
                        {
                            h = Mix(Mix(Mix(h, (long)e.type), e.objId), e.otherId);
                            if (e.type == ZEventType.Hitscan && e.objId == apc.Id) shots++;
                            if ((e.type == ZEventType.DriverHit || e.type == ZEventType.Sniped) && e.objId == apc.Id) dh++;
                        }
                        w.Events.Clear();
                    }
                    spot = w.SnipeSafeSpot(apc);
                    keys[on ? 1 : 0] = h.ToString("x16");
                }
                SetRule(false);
                bool same = !inBuild || keys[0] == keys[1];
                sb.AppendLine(OzV11Kit.Verdict(spot && up == 0 && snip == 0 && dh == 0 && shots > 0 && same,
                    $"U2 a lone driver on the deck of a standing bridge (DV82 spot {spot}) attacked by a grunt squad for 20 s (rule off, then on): driver up {up} ticks, snipable {snip} ticks, driver hits {dh}, APC shots {shots} in the two runs (from inside, as before); rule on = off {(same ? "yes" : "NO")}"));
            }
            finally { ZWorld.SnipeSafeSpotDeviation = safeWas; SetRule(false); }
        }

        static Ground Plain0(string map) => new Ground { name = "plain map", map = map, ax = CX + Gap, ay = CY, bx = 1440, by = 1440, rx = 120, ry = 1440 };

        /// <summary>The passengers row of the quick form: a grunt squad against an APC with a grunt squad aboard, 10 fights, on = off.</summary>
        static void PassengersQuick(string settings, StringBuilder sb)
        {
            SetRule(false); var off = Fight(settings, "grunt", Gap, 10, SeedBase, Crew.Passengers);
            SetRule(true); var on = Fight(settings, "grunt", Gap, 10, SeedBase, Crew.Passengers);
            SetRule(false);
            sb.AppendLine(OzV11Kit.Verdict(off.upFights == 0 && on.exposedTicks == 0 && on.driverHits == 0 && on.Key == off.Key,
                $"P1 a grunt squad against an APC with a grunt squad aboard (10 fights): driver up {on.upFights}, snipable {on.exposedTicks} ticks, driver hits {on.driverHits}; rule on tick for tick the rule off: {(on.Key == off.Key ? "yes" : "NO")}"));
        }

        // ================================================================================================ R: rules
        static void Rules(string settings, StringBuilder sb, bool inBuild)
        {
            sb.AppendLine("R rules:");
            bool snipeWas = ZWorld.SnipeDeviation;
            try
            {
                foreach (bool on in inBuild ? new[] { false, true } : new[] { false })
                {
                    SetRule(on);
                    var w = NewWorld(settings, Plain, 7);
                    var apc = Apc(w, CX + Gap, CY, Crew.Alone);
                    bool fresh = apc.CanBeSniped();
                    var g = OzV11Kit.Place(w, "grunt", CX, CY, Team.Red);
                    foreach (var m in OzV11Kit.Squad(g)) m.MaxHealth = m.Health = Big;                             // they keep attacking, nobody dies
                    apc.MaxHealth = apc.Health = Big; apc.Drivers[0] = Big;
                    OzV11Kit.PlayerAttack(w, g, apc);
                    double upAt = -1; bool snipableUp = false, snipableUpEvery = true; int shots = 0;
                    while (w.Time < 6)
                    {
                        w.Tick(ZConst.TICK);
                        foreach (var e in w.Events) if (e.type == ZEventType.Hitscan && e.objId == apc.Id) shots++;
                        w.Events.Clear();
                        if (apc.ApcDriverUp && upAt < 0) upAt = w.Time;
                        if (apc.ApcDriverUp) { snipableUp |= apc.CanBeSniped(); snipableUpEvery &= apc.CanBeSniped(); }
                    }
                    foreach (var m in OzV11Kit.Squad(g)) { m.SetHealth(0); w.UpdateObjectHealth(m); }               // the attackers are gone: he goes back in
                    double goneAt = w.Time, downAt = -1;
                    while (w.Time < 14) { w.Tick(ZConst.TICK); w.Events.Clear(); if (!apc.ApcDriverUp && downAt < 0) downAt = w.Time; }
                    bool afterDown = apc.CanBeSniped();
                    if (!on)
                        sb.AppendLine(OzV11Kit.Verdict(!fresh && upAt >= 0 && shots > 0 && !snipableUp && !afterDown, $"R1 rule off: the lone driver comes up ({upAt:0.00} s) and fires ({shots} shots), the APC is never snipable (fresh {fresh}, up {snipableUp}, back in {afterDown})"));
                    else
                    {
                        sb.AppendLine(OzV11Kit.Verdict(!fresh && upAt >= 0 && snipableUp && snipableUpEvery, $"R2 rule on: a crewed APC with the driver inside is not snipable ({fresh}); attacked from {Gap} px the driver is up at {upAt:0.00} s and snipable every tick he is up ({snipableUpEvery})"));
                        sb.AppendLine(OzV11Kit.Verdict(downAt >= 0 && downAt - goneAt <= ZWorld.CraneGunLinger + 0.1 && !afterDown, $"R3 rule on: the attackers gone, he drops back in {downAt - goneAt:0.00} s later (the linger {ZWorld.CraneGunLinger:0.0} s) and the APC is not snipable again ({afterDown})"));
                        ZWorld.SnipeDeviation = false;
                        var w2 = NewWorld(settings, Plain, 8);
                        var c2 = Apc(w2, CX + Gap, CY, Crew.Alone); c2.MaxHealth = c2.Health = Big; c2.Drivers[0] = Big;
                        var g2 = OzV11Kit.Place(w2, "grunt", CX, CY, Team.Red); foreach (var m in OzV11Kit.Squad(g2)) m.MaxHealth = m.Health = Big;
                        OzV11Kit.PlayerAttack(w2, g2, c2);
                        bool zodSnipable = false, zodUp = false;
                        while (w2.Time < 6) { w2.Tick(ZConst.TICK); w2.Events.Clear(); zodUp |= c2.ApcDriverUp; zodSnipable |= c2.CanBeSniped(); }
                        ZWorld.SnipeDeviation = snipeWas;
                        sb.AppendLine(OzV11Kit.Verdict(!zodSnipable, $"R4 the rule switch on but driver sniping as in Zod (SnipeDeviation off): never snipable (driver up {zodUp}, snipable {zodSnipable})"));
                    }
                }
                if (inBuild)
                {
                    SetRule(true);
                    var w = NewWorld(settings, Plain, 9);
                    var empty = OzV11Kit.Place(w, "apc", CX + 200, CY, Team.Neutral);
                    sb.AppendLine(OzV11Kit.Verdict(!empty.CanBeSniped() && !empty.ApcDriverUp, $"R5 an empty APC is never snipable ({empty.CanBeSniped()})"));
                    // a lone driver of another type (a psycho who took a neutral APC by himself): the same rule
                    var w3 = NewWorld(settings, Plain, 10);
                    var p = Apc(w3, CX + Gap, CY, Crew.Alone); p.DriverType = "psycho"; p.Drivers[0] = Big; p.MaxHealth = p.Health = Big; p.ResetDamageInfo();
                    var g3 = OzV11Kit.Place(w3, "grunt", CX, CY, Team.Red); foreach (var m in OzV11Kit.Squad(g3)) m.MaxHealth = m.Health = Big;
                    OzV11Kit.PlayerAttack(w3, g3, p);
                    bool pUp = false, pSn = false; int pHits = 0;
                    while (w3.Time < 6)
                    {
                        w3.Tick(ZConst.TICK);
                        foreach (var e in w3.Events) if (e.type == ZEventType.DriverHit && e.objId == p.Id) pHits++;
                        w3.Events.Clear(); pUp |= p.ApcDriverUp; pSn |= p.CanBeSniped();
                    }
                    sb.AppendLine(OzV11Kit.Verdict(pUp && pSn && pHits > 0, $"R6 a lone psycho driving an APC: up {pUp}, snipable {pSn}, driver hits in 6 s {pHits} (the same rule for every lone driver)"));
                    // a squad boards the APC while its driver is up: from that tick he is inside and the APC cannot be sniped
                    var w4 = NewWorld(settings, Plain, 11);
                    var b = Apc(w4, CX + Gap, CY, Crew.Alone); b.MaxHealth = b.Health = Big; b.Drivers[0] = Big;
                    var g4 = OzV11Kit.Place(w4, "grunt", CX, CY, Team.Red); foreach (var m in OzV11Kit.Squad(g4)) m.MaxHealth = m.Health = Big;
                    OzV11Kit.PlayerAttack(w4, g4, b);
                    var ride = OzV11Kit.Place(w4, "grunt", b.CenterX + 60, b.CenterY + 40, Team.Blue);
                    foreach (var m in OzV11Kit.Squad(ride)) m.Damage = 0;
                    int rideSize = OzV11Kit.Squad(ride).Count();
                    bool upBefore = false; double sent = -1, inAt = -1; int upAfter = 0, snipAfter = 0, hitsAfter = 0, shotsAfter = 0;
                    while (w4.Time < 20)
                    {
                        w4.Tick(ZConst.TICK);
                        foreach (var e in w4.Events)
                        {
                            if (inAt < 0 && e.type == ZEventType.Entered && e.otherId == b.Id) inAt = w4.Time;
                            if (inAt >= 0 && (e.type == ZEventType.DriverHit || e.type == ZEventType.Sniped) && e.objId == b.Id) hitsAfter++;
                            if (inAt >= 0 && e.type == ZEventType.Hitscan && e.objId == b.Id) shotsAfter++;
                        }
                        w4.Events.Clear();
                        if (sent < 0 && b.ApcDriverUp && w4.Time > 2) { upBefore = true; sent = w4.Time; OzV11Kit.PlayerEnter(w4, ride, b); }
                        if (inAt >= 0) { if (b.ApcDriverUp) upAfter++; if (b.CanBeSniped()) snipAfter++; }
                    }
                    sb.AppendLine(OzV11Kit.Verdict(upBefore && inAt >= 0 && b.Passengers.Count == rideSize && upAfter == 0 && snipAfter == 0 && hitsAfter == 0 && shotsAfter > 0,
                        $"R7 a blue grunt squad boards the APC while its lone driver is up and under fire (sent {sent:0.00} s, aboard {inAt:0.00} s, {b.Passengers.Count} passengers): from that tick driver up {upAfter} ticks, snipable {snipAfter} ticks, driver hits {hitsAfter}; the APC fires on from its ports ({shotsAfter} shots, DV87 part C as before)"));
                }
                // the HP of one driver hit on the APC, and every other target unchanged
                var dw = NewWorld(settings, Plain, 12);
                var ap = OzV11Kit.Place(dw, "apc", CX + Gap, CY, Team.Blue);
                var line = new StringBuilder();
                bool tableOk = true;
                foreach (var a in new[] { "grunt", "psycho", "sniper", "laser", "pyro", "jeep", "gatling" })
                {
                    var u = OzV11Kit.Place(dw, a, CX, CY + 200, Team.Red, 1);
                    SetRule(false); int off = dw.DriverHitDamage(u, ap);
                    SetRule(true); int on = inBuild ? dw.DriverHitDamage(u, ap) : off;
                    int expect = Math.Max(u.Damage, Floor) * (a == "sniper" ? ZWorld.SniperDriverFactor : 1);
                    if (inBuild && on != expect) tableOk = false;
                    line.Append($"{a} {off} -> {on}; ");
                }
                SetRule(false);
                if (inBuild) sb.AppendLine(OzV11Kit.Verdict(tableOk, $"R8 HP of one driver hit on the APC, rule off -> on (at least {Floor}, the crane driver's floor; snipers x{ZWorld.SniperDriverFactor}): {line}"));
                int pairs = 0, same = 0;
                var targets = new[] { "jeep", "light", "medium", "heavy", "crane", "missile_launcher", "gatling", "gun", "howitzer", "missile_cannon" };
                foreach (var a in new[] { "grunt", "psycho", "sniper", "laser", "pyro", "jeep", "gatling" })
                    foreach (var tt in targets)
                    {
                        var u = OzV11Kit.Place(dw, a, CX, CY + 300, Team.Red, 1); var t = OzV11Kit.Place(dw, tt, CX + 300, CY + 300, Team.Blue);
                        SetRule(false); int off = dw.DriverHitDamage(u, t); double coff = ZWorld.SnipeChanceOn(u, t); bool soff = t.CanBeSniped();
                        SetRule(true); int on = dw.DriverHitDamage(u, t); double con = ZWorld.SnipeChanceOn(u, t); bool son = t.CanBeSniped();
                        pairs++; if (on == off && Math.Abs(con - coff) < 1e-12 && son == soff) same++;
                    }
                SetRule(false);
                sb.AppendLine(OzV11Kit.Verdict(same == pairs, $"R9 every other target: the HP of a driver hit, the snipe chance and whether it can be sniped are the same with the rule on and off ({same} of {pairs} attacker / target pairs)"));
            }
            finally { ZWorld.SnipeDeviation = snipeWas; SetRule(false); }
        }

        // ================================================================================================ E: the emptied APC is taken
        static void Empty(string settings, StringBuilder sb)
        {
            SetRule(true);
            var w = NewWorld(settings, Plain, 13);
            var apc = Apc(w, CX + Gap, CY, Crew.Alone); apc.MaxHealth = apc.Health = Big;
            var sn = OzV11Kit.Place(w, "sniper", CX, CY, Team.Red);
            foreach (var m in OzV11Kit.Squad(sn)) m.MaxHealth = m.Health = Big;
            OzV11Kit.PlayerAttack(w, sn, apc);
            double at = -1; bool neutralEvent = false;
            while (w.Time < 30 && at < 0)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                {
                    if (e.type == ZEventType.Sniped && e.objId == apc.Id) at = w.Time;
                    if (e.type == ZEventType.OwnerChanged && e.objId == apc.Id && (Team)(int)e.value == Team.Neutral) neutralEvent = true;
                }
                w.Events.Clear();
            }
            w.Tick(ZConst.TICK); w.Events.Clear();
            bool empty = apc.Owner == Team.Neutral && apc.Drivers.Count == 0 && apc.Passengers.Count == 0 && !apc.ApcDriverUp && apc.Waypoints.Count == 0 && apc.CanBeEntered() && !apc.CanBeSniped();
            bool disengaged = OzV11Kit.Squad(sn).All(m => m.AttackObject != apc);
            foreach (var m in OzV11Kit.Squad(sn)) { m.Damage = 0; w.SendWaypoints(Team.Red, m, new ZWaypoint[0]); }
            var g = OzV11Kit.Place(w, "grunt", apc.CenterX, apc.CenterY + 90, Team.Red);
            int size = OzV11Kit.Squad(g).Count();
            OzV11Kit.PlayerEnter(w, g, apc);
            bool entered = false; double t0 = w.Time; string who = "nobody";
            while (w.Time < t0 + 30 && !entered)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                    if (e.type == ZEventType.Entered && e.otherId == apc.Id) { entered = true; who = e.objId == g.Id ? "the grunt squad sent" : "a red " + (w.GetAny(e.objId)?.Type ?? "robot") + " squad by itself"; }
                w.Events.Clear();
            }
            for (int i = 0; i < 4; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); }
            bool taken = entered && apc.Owner == Team.Red && apc.Drivers.Count == 1 && apc.Passengers.Count == size - 1;
            SetRule(false);
            sb.AppendLine(OzV11Kit.Verdict(at >= 0 && neutralEvent && empty && disengaged && taken,
                $"E1 a sniper squad empties the APC at {at:0.00} s: OwnerChanged -> neutral {neutralEvent}, empty, nobody up and enterable {empty}, every sniper let go of it {disengaged}; a red grunt squad of {size} sent to walk in: {who} got in {entered}, the APC is {apc.Owner} with {apc.Drivers.Count} driver and {apc.Passengers.Count} passengers (DV15: the leader drives, the others ride)"));
        }

        // ================================================================================================ D: deterministic
        static void Twice(string settings, StringBuilder sb)
        {
            SetRule(true);
            var a = Fight(settings, "jeep", Gap, 3, SeedBase + 500);
            var b = Fight(settings, "jeep", Gap, 3, SeedBase + 500);
            var c = Fight(settings, "sniper", Gap, 3, SeedBase + 600);
            var d = Fight(settings, "sniper", Gap, 3, SeedBase + 600);
            SetRule(false);
            sb.AppendLine(OzV11Kit.Verdict(a.hash == b.hash && a.sniped == b.sniped && Math.Abs(a.time - b.time) < 1e-9 && c.hash == d.hash && c.sniped == d.sniped && Math.Abs(c.time - d.time) < 1e-9,
                $"D1 deterministic: three jeep fights and three sniper fights run twice, the same events ({a.hash:x16} / {b.hash:x16}, {c.hash:x16} / {d.hash:x16}), the same snipes ({a.sniped} / {b.sniped}, {c.sniped} / {d.sniped}) at the same times"));
        }

        /// <summary>For the menu: the verdict in one line ("PASS 30 / 30 lines", or "FAIL" with the failed lines).</summary>
        public static string Summary(string report)
        {
            var lines = (report ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("PASS  ") || l.StartsWith("FAIL  ")).ToList();
            var failed = lines.Where(l => l.StartsWith("FAIL")).ToList();
            return lines.Count == 0 ? "FAIL no verdict line" : failed.Count == 0 ? $"PASS {lines.Count} / {lines.Count} lines" : $"FAIL {failed.Count} of {lines.Count} lines: " + string.Join("; ", failed.Select(l => l.Substring(6)));
        }

        /// <summary>ZWorld.ApcDriverHitFloor read by name (0 = the rule is not in this build).</summary>
        static int Floor => typeof(ZWorld).GetField("ApcDriverHitFloor") is System.Reflection.FieldInfo f ? Convert.ToInt32(f.GetValue(null)) : 0;
    }
}
