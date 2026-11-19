using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5, fort-mounted missile cannons fire 2x slower, rule DV177 (Sim/ZObject.FortTurretRof.cs): the counted check, headless on the Sim alone,
    /// nothing saved: menu OpenZ/V1.5/Fort Turret Reload: Count (headless, DV177). One line per fact, [OpenZ V1.5 fort turret reload] PASS n/n.
    /// The check sets the rule's switch itself (on as View/ZGame.cs does at a level start, off as in 'Zod exact') on top of the counted checks'
    /// usual game switches (OzV11Kit.GameSwitches, put back after), and puts it back. Its scenes hold the fort's own big gun (DV7), the fort's
    /// roof crew (DV41) and DV73 off so only the cannon under test fires; the level part sets DV73 itself.
    ///  S  the rows: the settings parse is the same off and on (the rule changes no row: the Unit editor and the build card read the row);
    ///  F  fort vs ground: a red missile cannon on a fort's tower spot and one on the ground, each with a still, harmless heavy tank 96 px north,
    ///     60 s, both teams (the cannon red, then blue): off = both fire every G ticks (Zod's 1.124 s = 113 ticks); on = the tower one every F
    ///     ticks (2 x 1.124 s = 225 ticks), F / G about 2, about half the shots; the first shot at the same tick off and on; the cannon's damage,
    ///     range, row reload and rocket speed the same; ReloadSeconds 2.248 on the tower, 1.124 on the ground;
    ///  G  ground identical: the ground scene off and on gives the same shot ticks, every event and the random stream byte for byte; the same for
    ///     a gun, a howitzer and a gatling on the tower spot (only missile cannons are named) and for a blue ground missile cannon;
    ///  L  the levels: L02, L07, L16, L17 with DV73 off (EASY / MEDIUM, bot matches) and on (HARD; MAYHEM plays HARD's rules): exactly the
    ///     expected fort-mounted missile cannons (README table), every one of them slowed, every other cannon not; a missile cannon placed
    ///     from the fort's store on a free tower spot is slowed, one placed on the ground is not;
    ///  D  determinism: the scripted tower fight twice, the same ticks, events and random stream; computer against computer on L16 (its blue
    ///     tower missile cannon) and on L17 with DV73 forced on (one per fort, both teams), 90 s, on twice and off twice: identical tick for tick;
    ///     in every ON match no fort-mounted missile cannon fires again sooner than F - 1 ticks;
    ///  U  the Unit editor's stats code: its missile cannon reload card reads the row (1.124 s) off and on: its playground has no fort, so every
    ///     missile cannon there is a ground one and the card is right; the fort value is told in the README (no player text changed here).
    /// </summary>
    public static class OzV15FortTurretRofCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);
        static string F(double v, string f = "0.000") => v.ToString(f, CultureInfo.InvariantCulture);

        public const string Type = ZObject.FortTurretRofType;
        public const double ZodReload = 1.124;
        const int Big = 1000000000;
        const int FortTx = 20, FortTy = 20;                             // the fort_front's top-left tile on the plain map
        const int TowerTx = FortTx + 7, TowerTy = FortTy + 0;           // its tower spot (7,0) (bfort.cpp:534-548: fort-local px 112, 0)
        const int GroundTx = 60, GroundTy = 20;                         // a ground spot 31 tiles east (496 px: out of every gun's reach of the other)
        const int TargetPx = 96;                                        // the target's centre this far north of the cannon's centre

        /// <summary>Ticks from one shot to the next for a pause of `sec` seconds on the sim's clock: the first tick at or after it.</summary>
        public static int TicksOf(double sec) => (int)Math.Ceiling(sec / ZConst.TICK - 1e-6);

        static IDisposable Rule(bool on) => new Hold(on);
        sealed class Hold : IDisposable
        {
            readonly bool was = ZWorld.FortTurretRofDeviation;
            public Hold(bool on) { ZWorld.FortTurretRofDeviation = on; }
            public void Dispose() { ZWorld.FortTurretRofDeviation = was; }
        }

        /// <summary>Only the cannon under test fires: the fort's big gun (DV7), its roof crew (DV41) and DV73's door gun off; put back after.</summary>
        sealed class Quiet : IDisposable
        {
            readonly bool gun = ZWorld.FortGunDeviation, crew = ZWorld.FortDefenderDeviation, door = ZWorld.FortDoorGunDeviation;
            public Quiet() { ZWorld.FortGunDeviation = false; ZWorld.FortDefenderDeviation = false; ZWorld.FortDoorGunDeviation = false; }
            public void Dispose() { ZWorld.FortGunDeviation = gun; ZWorld.FortDefenderDeviation = crew; ZWorld.FortDoorGunDeviation = door; }
        }

        static string Owner(Team t) => t == Team.Red ? "red" : t == Team.Blue ? "blue" : "null";
        static Team Other(Team t) => t == Team.Red ? Team.Blue : Team.Red;

        // ------------------------------------------------------------------------------------------------ scenes
        public sealed class Scene
        {
            public ZWorld w; public ZObject gun, target; public readonly List<int> shotTicks = new List<int>(); public int ticks;
            public ulong hash = 1469598103934665603UL;
            public List<int> Gaps() { var g = new List<int>(); for (int i = 1; i < shotTicks.Count; i++) g.Add(shotTicks[i] - shotTicks[i - 1]); return g; }
        }

        static void Mix(Scene s, long v) { s.hash = (s.hash ^ (ulong)v) * 1099511628211UL; }

        /// <summary>A plain 100 x 100 map with a fort_front of `team` at (FortTx, FortTy) and one `type` cannon of `team`, on the fort's tower
        /// spot (7,0) or on the ground spot; a still, harmless enemy heavy tank that cannot die TargetPx north of the cannon. No game end.</summary>
        public static Scene New(string settings, string type, bool onTower, Team team, int seed)
        {
            int tx = onTower ? TowerTx : GroundTx, ty = onTower ? TowerTy : GroundTy;
            var map = OzV11Kit.Plain(100, 100, ("building:fort_front", FortTx, FortTy, Owner(team)), ("cannon:" + type, tx, ty, Owner(team)));
            var w = OzV11Kit.World(settings, map, seed);
            w.NoGameEnd = true;
            var gun = w.Objects.FirstOrDefault(o => o.Kind == ZKind.Cannon && o.LocX == tx * 16 && o.LocY == ty * 16);
            if (gun == null) throw new InvalidOperationException($"no {type} at tile {tx},{ty} on the plain map");
            var t = OzV11Kit.Place(w, "heavy", gun.CenterX, gun.CenterY - TargetPx, Other(team));
            t.MaxHealth = t.Health = Big; t.Damage = 0; t.DamageChance = 0; t.AttackRadius = 0; t.MoveSpeed = 0; t.RealMoveSpeed = 0; t.CanBeSnipedFlag = false;
            w.Events.Clear();
            return new Scene { w = w, gun = gun, target = t };
        }

        public static void Run(Scene s, double seconds)
        {
            int n = (int)Math.Round(seconds / ZConst.TICK);
            for (int i = 0; i < n; i++)
            {
                s.w.Tick(ZConst.TICK); s.ticks++;
                foreach (var e in s.w.Events)
                {
                    Mix(s, (long)e.type); Mix(s, e.objId); Mix(s, e.otherId); Mix(s, e.x); Mix(s, e.y); Mix(s, e.radius); Mix(s, e.flag ? 1 : 2); Mix(s, e.flag2 ? 1 : 2);
                    if (e.objId == s.gun.Id && (e.type == ZEventType.MissileFired || e.type == ZEventType.Hitscan)) s.shotTicks.Add(s.ticks);
                }
                s.w.Events.Clear();
            }
            Mix(s, s.w.Rng.Next(1 << 30)); Mix(s, s.target.Health); Mix(s, s.gun.Health);
        }

        static Scene Fight(string settings, string type, bool onTower, Team team, bool on, double seconds = 60, int seed = 7)
        {
            using (new Quiet())
            using (Rule(on))
            {
                var s = New(settings, type, onTower, team, seed);
                Run(s, seconds);
                return s;
            }
        }

        static string Gaps(Scene s)
        {
            var g = s.Gaps();
            if (g.Count == 0) return "no gap";
            return $"{s.shotTicks.Count} shots, first at tick {s.shotTicks[0]}, gaps {g.Min()}..{g.Max()} ticks (mean {F(g.Average(), "0.0")})";
        }

        static bool Steady(Scene s, int want) { var g = s.Gaps(); return g.Count >= 3 && g.All(x => Math.Abs(x - want) <= 1); }

        // ------------------------------------------------------------------------------------------------ S
        static readonly FieldInfo[] UnitFields = typeof(ZUnitSettings).GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
        static bool Same(object a, object b) => a is double x && b is double y ? BitConverter.DoubleToInt64Bits(x) == BitConverter.DoubleToInt64Bits(y) : Equals(a, b);

        static void S(string settings)
        {
            ZSettings off, on;
            using (Rule(false)) off = ZSettings.Parse(settings);
            using (Rule(true)) on = ZSettings.Parse(settings);
            var diff = new List<string>(); int fields = 0;
            foreach (var name in off.Units.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var a = off.Unit(name); var b = on.Unit(name);
                if (b == null) { diff.Add(name + " missing"); continue; }
                foreach (var f in UnitFields) { fields++; if (!Same(f.GetValue(a), f.GetValue(b))) diff.Add(name + "." + f.Name); }
            }
            var mc = on.Unit(Type);
            Check(diff.Count == 0 && mc != null && Same(mc.attack_speed, ZodReload),
                $"every unit row the same off and on ({off.Units.Count} types, {fields} fields): the rule changes no row; the missile cannon's attack_speed {F(mc?.attack_speed ?? -1)} s (Zod's 1.124){(diff.Count > 0 ? "; differ: " + string.Join(" ", diff.Take(12)) : "")}");
        }

        // ------------------------------------------------------------------------------------------------ F
        static void Fv(string settings)
        {
            int g = TicksOf(ZodReload), f = TicksOf(ZodReload * ZObject.FortTurretReloadFactor);
            Info($"the sim's clock: a pause of {F(ZodReload)} s = {g} ticks ({F(g * ZConst.TICK, "0.00")} s), {F(ZodReload * ZObject.FortTurretReloadFactor)} s = {f} ticks ({F(f * ZConst.TICK, "0.00")} s); ratio {F(f / (double)g)}");
            foreach (var team in new[] { Team.Red, Team.Blue })
            {
                var t0 = Fight(settings, Type, true, team, false); var t1 = Fight(settings, Type, true, team, true);
                var g0 = Fight(settings, Type, false, team, false); var g1 = Fight(settings, Type, false, team, true);
                Check(t0.gun.IsTowerGun && !t0.gun.Ejectable && !g0.gun.IsTowerGun && g0.gun.Ejectable,
                    $"{team}: the cannon on the tower spot ({TowerTx},{TowerTy}) is a tower gun (not ejectable, as ZWorld.MarkFortTurretCannons makes it); the one on ({GroundTx},{GroundTy}) is a ground gun");
                Check(Steady(t0, g) && Steady(g0, g) && Steady(g1, g),
                    $"{team}, 60 s at a still heavy tank 96 px away: off, the tower cannon {Gaps(t0)}; the ground cannon off {Gaps(g0)}, on {Gaps(g1)}: every {g} ticks (Zod's {F(ZodReload)} s)");
                Check(Steady(t1, f),
                    $"{team}: on, the tower cannon {Gaps(t1)}: every {f} ticks ({F(ZodReload * ZObject.FortTurretReloadFactor)} s)");
                double r = t1.Gaps().Count > 0 && t0.Gaps().Count > 0 ? t1.Gaps().Average() / t0.Gaps().Average() : 0;
                double k = t1.shotTicks.Count / (double)Math.Max(1, t0.shotTicks.Count);
                Check(r > 1.97 && r < 2.03 && k > 0.45 && k < 0.56,
                    $"{team}: tower on / off: the pause x {F(r)} (2 asked; the 10 ms clock gives {f} / {g} = {F(f / (double)g)}), the shots in 60 s x {F(k)} ({t1.shotTicks.Count} instead of {t0.shotTicks.Count}): half the rate of fire");
                Check(t0.shotTicks.Count > 0 && t1.shotTicks.Count > 0 && t0.shotTicks[0] == t1.shotTicks[0],
                    $"{team}: the first shot at the same tick off and on ({(t0.shotTicks.Count > 0 ? t0.shotTicks[0] : -1)} / {(t1.shotTicks.Count > 0 ? t1.shotTicks[0] : -1)}): the reload starts when it fires, the first shot is not delayed");
                var a = t0.gun; var b = t1.gun;
                Check(a.Damage == b.Damage && a.AttackRadius == b.AttackRadius && Same(a.AttackSpeed, b.AttackSpeed) && a.MissileSpeed == b.MissileSpeed && a.DamageRadius == b.DamageRadius && a.MaxHealth == b.MaxHealth,
                    $"{team}: the tower cannon's weapon the same off and on: {b.Damage} HP a hit, {b.AttackRadius} px, row reload {F(b.AttackSpeed)} s, rocket {b.MissileSpeed} px/s, blast {b.DamageRadius} px, {b.MaxHealth} HP");
                double rs1, rs0, rg1;
                using (Rule(true)) { rs1 = t1.gun.ReloadSeconds; rg1 = g1.gun.ReloadSeconds; }
                using (Rule(false)) rs0 = t1.gun.ReloadSeconds;
                Check(Same(rs1, ZodReload * 2) && Same(rg1, ZodReload) && Same(rs0, ZodReload),
                    $"{team}: ReloadSeconds: on the tower {F(rs1)} s with the switch on, {F(rs0)} s off; on the ground {F(rg1)} s");
            }
        }

        // ------------------------------------------------------------------------------------------------ G
        static void G(string settings)
        {
            foreach (var team in new[] { Team.Red, Team.Blue })
            {
                var a = Fight(settings, Type, false, team, false); var b = Fight(settings, Type, false, team, true);
                Check(a.shotTicks.SequenceEqual(b.shotTicks) && a.hash == b.hash && a.shotTicks.Count > 10,
                    $"{team}: the GROUND missile cannon off and on: the same {a.shotTicks.Count} shot ticks, every event and the random stream {a.hash:X16} / {b.hash:X16} (byte for byte)");
            }
            foreach (var other in new[] { "gun", "howitzer", "gatling" })
            {
                var a = Fight(settings, other, true, Team.Red, false); var b = Fight(settings, other, true, Team.Red, true);
                Check(a.gun.IsTowerGun && a.shotTicks.SequenceEqual(b.shotTicks) && a.hash == b.hash,
                    $"a {other} on the tower spot (not named by the owner) off and on: the same {a.shotTicks.Count} shots ({Gaps(b)}) and events {a.hash:X16} / {b.hash:X16}");
            }
        }

        // ------------------------------------------------------------------------------------------------ L
        /// <summary>The fort-mounted missile cannons at a level start (tile x, y, owner), README table: with DV73 off (EASY, MEDIUM, bot
        /// matches) only level 16's blue one (DV99); with DV73 on (HARD, MAYHEM) one on each fort's door tower, east side.</summary>
        public static readonly Dictionary<string, string> Expected = new Dictionary<string, string>
        {
            ["L02 off"] = "", ["L07 off"] = "", ["L16 off"] = "54,12 Blue", ["L17 off"] = "",
            ["L02 on"] = "21,10 Blue; 55,72 Red", ["L07 on"] = "44,9 Blue; 44,93 Red", ["L16 on"] = "54,109 Red; 54,12 Blue", ["L17 on"] = "26,23 Blue; 88,36 Red",
        };
        public static readonly string[] Levels = { "L02", "L07", "L16", "L17" };

        static string Listed(ZWorld w) => string.Join("; ", w.Objects.Where(o => o.IsTowerGun && o.Type == Type && !o.Destroyed)
            .Select(o => $"{o.LocX / 16},{o.LocY / 16} {o.Owner}").OrderBy(x => x, StringComparer.Ordinal));

        static void L(string settings, Func<string, string> map)
        {
            bool door = ZWorld.FortDoorGunDeviation;
            try
            {
                foreach (var hard in new[] { false, true })
                    foreach (var lv in Levels)
                    {
                        ZWorld.FortDoorGunDeviation = hard;
                        ZWorld w;
                        using (Rule(true)) w = ZWorld.Create(settings, map(lv), 1);
                        string key = lv + (hard ? " on" : " off"), got = Listed(w);
                        int slowedOk = 0, plainOk = 0; var wrong = new List<string>();
                        using (Rule(true))
                            foreach (var c in w.Objects.Where(o => o.Kind == ZKind.Cannon && !o.Destroyed))
                            {
                                bool fortMc = c.IsTowerGun && c.Type == Type;
                                bool ok = fortMc ? c.FortTurretRofSlow && Same(c.ReloadSeconds, c.AttackSpeed * 2) : !c.FortTurretRofSlow && Same(c.ReloadSeconds, c.AttackSpeed);
                                if (!ok) wrong.Add($"{c.Type} {c.LocX / 16},{c.LocY / 16} {c.Owner}");
                                else if (fortMc) slowedOk++; else plainOk++;
                            }
                        Check(got == Expected[key] && wrong.Count == 0,
                            $"{lv} {(hard ? "HARD / MAYHEM (DV73 on)" : "EASY / MEDIUM / bot match (DV73 off)")}: fort-mounted missile cannons [{got}] (expected [{Expected[key]}]), {slowedOk} slowed (2.248 s), "
                            + $"{plainOk} other cannons at their row's reload{(wrong.Count > 0 ? "; WRONG: " + string.Join(", ", wrong) : "")}");
                    }
                // a missile cannon placed from the fort's store: on a free tower spot and on the ground (L02, blue's fort, DV73 off)
                ZWorld.FortDoorGunDeviation = false;
                ZWorld p;
                using (Rule(true)) p = ZWorld.Create(settings, map("L02"), 1);
                var fort = p.Objects.FirstOrDefault(o => o.IsFort && o.Owner == Team.Blue);
                ZObject onTower = null, onGround = null; int gx = -1, gy = -1;
                if (fort != null)
                {
                    int fx = fort.LocX / 16, fy = fort.LocY / 16;
                    fort.StoredCannons.Add(Type); fort.StoredCannons.Add(Type);
                    onTower = p.PlaceCannon(Team.Blue, fort, fx + 7, fy + 3, fort.StoredCannons.Count - 1);
                    for (int dy = 13; dy <= 30 && onGround == null; dy++)
                        for (int dx = -6; dx <= 16 && onGround == null; dx++)
                        {
                            int tx = fx + dx, ty = fy + dy;
                            if (!p.CannonPlacable(fort, tx, ty) || p.GunSpotOnFortPad(tx, ty)) continue;
                            onGround = p.PlaceCannon(Team.Blue, fort, tx, ty, fort.StoredCannons.Count - 1); gx = tx; gy = ty;
                        }
                }
                bool tSlow, gSlow; double tRs, gRs;
                using (Rule(true)) { tSlow = onTower != null && onTower.FortTurretRofSlow; gSlow = onGround != null && onGround.FortTurretRofSlow; tRs = onTower?.ReloadSeconds ?? -1; gRs = onGround?.ReloadSeconds ?? -1; }
                Check(onTower != null && onTower.IsTowerGun && tSlow && Same(tRs, ZodReload * 2) && onGround != null && !onGround.IsTowerGun && !gSlow && Same(gRs, ZodReload),
                    $"L02, blue's fort store: a missile cannon placed on the free tower spot ({(fort != null ? fort.LocX / 16 + 7 : -1)},{(fort != null ? fort.LocY / 16 + 3 : -1)}) is a tower gun, {F(tRs)} s; "
                    + $"one placed on the ground ({gx},{gy}) is not, {F(gRs)} s");
                Info("MAYHEM is HARD for every rule (View/ZGame.cs: gameDifficulty stays Hard, the fog of war is view only), so its fort-mounted missile cannons are HARD's above; the switch line does not read the difficulty");
            }
            finally { ZWorld.FortDoorGunDeviation = door; }
        }

        // ------------------------------------------------------------------------------------------------ D
        public sealed class Match { public string print; public ulong hash = 1469598103934665603UL; public int towerShots, towerTooSoon, minGap = int.MaxValue; }

        public static Match Bots(string settings, string mapJson, bool on, bool door, int seed, int ticks)
        {
            bool was = ZWorld.FortDoorGunDeviation;
            try
            {
                ZWorld.FortDoorGunDeviation = door;
                using (Rule(on))
                {
                    var w = ZWorld.Create(settings, mapJson, seed);
                    w.Bots.Add(new ZBot(w, Team.Red, seed)); w.Bots.Add(new ZBot(w, Team.Blue, seed + 101));
                    w.Events.Clear();
                    var m = new Match(); var last = new Dictionary<int, int>(); int fTicks = TicksOf(ZodReload * ZObject.FortTurretReloadFactor);
                    for (int i = 1; i <= ticks; i++)
                    {
                        w.Tick(ZConst.TICK);
                        foreach (var e in w.Events)
                        {
                            m.hash = (m.hash ^ (ulong)(long)e.type) * 1099511628211UL; m.hash = (m.hash ^ (ulong)(long)e.objId) * 1099511628211UL;
                            m.hash = (m.hash ^ (ulong)(long)e.otherId) * 1099511628211UL; m.hash = (m.hash ^ (ulong)(long)e.x) * 1099511628211UL; m.hash = (m.hash ^ (ulong)(long)e.y) * 1099511628211UL;
                            if (e.type != ZEventType.MissileFired) continue;
                            var a = w.GetAny(e.objId);
                            if (a == null || !a.IsTowerGun || a.Type != Type) continue;
                            m.towerShots++;
                            if (last.TryGetValue(a.Id, out int prev)) { int gap = i - prev; m.minGap = Math.Min(m.minGap, gap); if (on && gap < fTicks - 1) m.towerTooSoon++; }
                            last[a.Id] = i;
                        }
                        w.Events.Clear();
                    }
                    m.print = string.Join(";", w.Objects.Select(o => $"{o.Id}:{o.LocX},{o.LocY},{o.Health},{(o.Destroyed ? 1 : 0)}")) + "|" + w.Rng.Next(1 << 30);
                    return m;
                }
            }
            finally { ZWorld.FortDoorGunDeviation = was; }
        }

        static void D(string settings, Func<string, string> map, int ticks)
        {
            Scene a, b;
            using (new Quiet())
            using (Rule(true)) { a = New(settings, Type, true, Team.Red, 4242); Run(a, 60); b = New(settings, Type, true, Team.Red, 4242); Run(b, 60); }
            Check(a.shotTicks.SequenceEqual(b.shotTicks) && a.hash == b.hash && a.shotTicks.Count > 3,
                $"the same tower fight twice (on, seed 4242): {a.shotTicks.Count} / {b.shotTicks.Count} shots at the same ticks, events and the random stream {a.hash:X16} / {b.hash:X16}");
            foreach (var (lv, door) in new[] { ("L16", false), ("L17", true) })
            {
                string m = map(lv);
                var on1 = Bots(settings, m, true, door, 7, ticks); var on2 = Bots(settings, m, true, door, 7, ticks);
                var off1 = Bots(settings, m, false, door, 7, ticks); var off2 = Bots(settings, m, false, door, 7, ticks);
                Check(on1.print == on2.print && on1.hash == on2.hash && off1.print == off2.print && off1.hash == off2.hash,
                    $"{lv}{(door ? " with DV73 (HARD's door-tower missile cannons, both forts)" : "")}, computer against computer, {ticks * ZConst.TICK:0} s: on twice identical ({on1.hash:X16}), off twice identical ({off1.hash:X16}); "
                    + $"on and off {(on1.hash == off1.hash ? "the same (no fort-mounted missile cannon fired twice)" : "differ (expected once a tower missile cannon reloads)")}");
                Check(on1.towerTooSoon == 0,
                    $"{lv}: in the ON match the fort-mounted missile cannons fired {on1.towerShots} rockets and none again sooner than {TicksOf(ZodReload * 2) - 1} ticks (shortest gap {(on1.minGap == int.MaxValue ? "-" : on1.minGap + " ticks")}); OFF match: {off1.towerShots} rockets, shortest gap {(off1.minGap == int.MaxValue ? "-" : off1.minGap + " ticks")}");
            }
        }

#if !OPENZ_HARNESS || OZ_UE
        // ------------------------------------------------------------------------------------------------ U (the Unit editor's stats code)
        static void U(string settings)
        {
            var rules = UeRules.FromGame();
            double r1, r0;
            using (Rule(true)) r1 = OzUeLiveStats.Table(ZSettings.Parse(settings), rules)[Type][(int)UeStat.Reload].value;
            using (Rule(false)) r0 = OzUeLiveStats.Table(ZSettings.Parse(settings), rules)[Type][(int)UeStat.Reload].value;
            Check(Same(r1, ZodReload) && Same(r0, ZodReload),
                $"the Unit editor's missile cannon card: reload {F(r1)} s on, {F(r0)} s off: the row (the ground value); its playground has no fort, so every missile cannon there is a ground one and the card is right there. "
                + "The fort value (2.248 s) is not on any card: README, 'Unit editor and cards'");
        }
#endif

        public static string Run(string settings, Func<string, string> map, int botTicks = 9000)
        {
            pass = fail = 0; sb = new StringBuilder();
            var t0 = DateTime.UtcNow;
            int r0 = ZWorld.FortTurretRofReloads;
            try
            {
                S(settings); Fv(settings); G(settings); L(settings, map); D(settings, map, botTicks);
#if !OPENZ_HARNESS || OZ_UE
                U(settings);
#endif
            }
            catch (Exception ex) { Check(false, "the check threw " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace); }
            Info($"reloads made longer by DV177 during the check: {ZWorld.FortTurretRofReloads - r0}; run time {(DateTime.UtcNow - t0).TotalSeconds:0.0} s (the check's own, not a rule input)");
            sb.AppendLine($"[OpenZ V1.5 fort turret reload] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

        /// <summary>The Deviations row's probe: a red missile cannon on the tower and one on the ground, 30 s, off and on.</summary>
        public static string Quick(string settings)
        {
            pass = fail = 0; sb = new StringBuilder();
            try
            {
                int g = TicksOf(ZodReload), f = TicksOf(ZodReload * ZObject.FortTurretReloadFactor);
                var t0 = Fight(settings, Type, true, Team.Red, false, 30); var t1 = Fight(settings, Type, true, Team.Red, true, 30);
                var g0 = Fight(settings, Type, false, Team.Red, false, 30); var g1 = Fight(settings, Type, false, Team.Red, true, 30);
                Check(Steady(t0, g) && Steady(t1, f), $"the tower missile cannon: off {Gaps(t0)}, on {Gaps(t1)} (every {g}, then {f} ticks)");
                Check(g0.shotTicks.SequenceEqual(g1.shotTicks) && g0.hash == g1.hash && Steady(g1, g), $"the ground missile cannon off and on: the same {g1.shotTicks.Count} shots and events (every {g} ticks)");
            }
            catch (Exception ex) { Check(false, "the probe threw " + ex.GetType().Name + ": " + ex.Message); }
            sb.AppendLine($"[OpenZ V1.5 fort turret reload quick] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
            return sb.ToString();
        }

#if UNITY_EDITOR && !OPENZ_HARNESS
        [UnityEditor.MenuItem("OpenZ/V1.5/Fort Turret Reload: Count (headless, DV177)")]
        static void Menu() => OzV11Kit.RunMenu("fort turret reload: count (DV177)", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText));
#endif
    }
}
