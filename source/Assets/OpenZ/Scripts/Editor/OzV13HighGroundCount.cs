using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace OpenZ.EditorTools
{
    /// <summary>
    /// DV162 "high ground shoots farther" (Sim/ZObject.HighGround.cs), counted headless: no scene, no Play, nothing saved. Menu
    /// OpenZ/V1.3/High ground range: counted (headless, DV162); off-project on plain .NET by the kit's tools/harness. Every case runs the rule on and off, but for the 4 lines of plain numbers that have no rule in them (2 in H1, the tally in H5, the table in H7).
    ///  H1 the numbers: whole pixels, rounded to the nearest, every range from 0 to 2000 against exact decimals; the field AttackRadius is never written;
    ///  H2 every robot, vehicle and gun on a medium and on a high deck: the farthest target the rule lets it engage, pixel exact, the ring, on and off;
    ///  H3 a ramp is not high ground, upper or lower half; nor the ground beside the platform;
    ///  H4 levels: same deck, two decks of one height, high to medium, medium to high, ground up to a deck, a deck down onto its own ramp;
    ///  H5 every fort tower gun of every level: ring, range at the ground, at another tower gun; a real spot in the extra band, with a clear line;
    ///  H6 the second weapons: the hatch driver, the crane's rifle, the mech's hand gun;
    ///  H7 sight: the agro reach does not move on the default numbers and stretches to a custom range that passes it; DV23's line is the same;
    ///  H8 the flying shuttle is above everything, parked it is ground; a roof defender is as high as a tower gun; the shuttle's sight rule is the same;
    ///  H9 walking up and down: the range follows the unit; the rule's spot helpers for the rings;
    ///  H10 the computer: the same test: its "really fighting" (ZBot.FinishGame), the jeep's fear of a crewed gun, a CPU side's tower gun;
    ///  H11 real shots (the menu only): each weapon on each deck fires in the extra band with the rule on, not with it off, not past it; a tower gun on each level;
    ///  H12 an attack order in the extra band: a tank, a sniper, an Electrobot stand and fire on the deck with the rule on, and walk off it with the rule off;
    ///  H13 deterministic: the same match twice, the same world, rule on and rule off;
    ///  H14 units answering the tower missile cannon (the menu only): a heavy, a sniper, an Electrobot alone at 156 px, out of their 144 px, in its 158 px: at a harmless gun, and at the real one.
    /// </summary>
    public static class OzV13HighGroundCount
    {
        static int pass, fail, secPass, secFail; static string firstBad; static StringBuilder sb, detail;
        static readonly string[] Robots = { "grunt", "psycho", "sniper", "tough", "pyro", "laser", ZElectro.Type };
        static readonly string[] Vehicles = { "jeep", "light", "medium", ZMTank.Type, "heavy", "missile_launcher" };
        static readonly string[] Guns = { "gatling", "gun", "howitzer", "missile_cannon" };
        static IEnumerable<string> AllTypes => Robots.Concat(Vehicles).Concat(Guns);
        public static readonly string[] Levels = { "L02", "L07", "L16", "L17" };

        static void Case(bool ok, string what)
        {
            if (ok) { pass++; secPass++; } else { fail++; secFail++; firstBad ??= what; }
            detail.Append(ok ? "  ok    " : "  BAD   ").AppendLine(what);
        }
        static void Section(string name)
        {
            sb.Append(secFail == 0 ? "PASS  " : "FAIL  ").Append(name).Append(": ").Append(secPass).Append(" of ").Append(secPass + secFail);
            if (firstBad != null) sb.Append("; first: ").Append(firstBad);
            sb.AppendLine().Append(detail);
            secPass = secFail = 0; firstBad = null; detail.Clear();
        }
        static void Rule(bool on) => ZWorld.HighGroundDeviation = on;
        /// <summary>The same read with the rule off, then on; the rule stays on.</summary>
        static T OnOff<T>(Func<T> read, out T off) { Rule(false); off = read(); Rule(true); return read(); }

        static ZWorld Ue(string settings, string map, int seed = 1)
        {
            var w = ZWorld.Create(settings, map, seed);
            w.NoGameEnd = true; w.FlagHoldSec = 1.0; w.Events.Clear();
            return w;
        }
        static ZObject Plat(ZWorld w, int tx, int ty, int kind, int dir)
        {
            var o = w.PlacePlatform(tx, ty, kind, dir, -1, out string why);
            if (o == null) throw new Exception("no platform: " + why);
            return o;
        }
        static (int x, int y) At(ZWorld w, ZObject plat, double across, double along) { w.PlatformOf(plat.Id).Map(across, along, out double x, out double y); return ((int)x, (int)y); }
        static ZObject Unit(ZWorld w, string type, (int x, int y) c, Team t) => OzV11Kit.Place(w, type, c.x, c.y, t, 1);
        /// <summary>An enemy that stands, never fires back and does not die, its centre at (cx, cy).</summary>
        static ZObject Dummy(ZWorld w, int cx, int cy, Team enemyOf)
        {
            var d = w.CreateRobotGroup("grunt", cx - 8, cy - 8, enemyOf == Team.Blue ? Team.Red : Team.Blue, 1);
            d.MoveSpeed = 0; d.Damage = 0; d.MaxHealth = d.Health = 1000000000;
            return d;
        }
        static void Hold(ZObject o) => typeof(ZObject).GetProperty("HoldPosition").GetSetMethod(true).Invoke(o, new object[] { true });
        static void Put(ZObject o, int cx, int cy) => o.SetCords(cx - o.WidthPix / 2, cy - o.HeightPix / 2);
        /// <summary>Farthest px straight west of the shooter at which the rule lets it engage t.</summary>
        static int Far(ZObject s, ZObject t, int from)
        {
            for (int d = from; d >= 16; d--) { Put(t, s.CenterX - d, s.CenterY); if (s.WithinAttackRadius(t)) return d; }
            return -1;
        }

        public static string Check(string settings, string ueMap, Func<string, string> level, bool full)
        {
            pass = fail = secPass = secFail = 0; firstBad = null; sb = new StringBuilder(); detail = new StringBuilder();
            bool keep = ZWorld.HighGroundDeviation, los = ZWorld.LosDeviation; ZWorld.LosDeviation = true;
            try
            {
                H1(settings, ueMap); Section("H1 the numbers");
                H2(settings, ueMap); Section("H2 every weapon on both decks");
                H3(settings, ueMap); Section("H3 a ramp and the ground are not high ground");
                H4(settings, ueMap); Section("H4 same level, higher, lower");
                H5(settings, level); Section("H5 fort tower guns, every level");
                H6(settings, ueMap); Section("H6 the second weapons");
                H7(settings, ueMap); Section("H7 sight");
                H8(settings, ueMap, level); Section("H8 the flying shuttle, the roof defender");
                H9(settings, ueMap); Section("H9 the range follows the unit, the rings' helpers");
                H10(settings, level); Section("H10 the computer's units");
                if (full)
                {
                    H11(settings, ueMap, level); Section("H11 real shots");
                    H12(settings, ueMap); Section("H12 an attack order in the extra band");
                    H13(settings, level); Section("H13 deterministic");
                    H14(settings, level); Section("H14 units answering the tower missile cannon");
                }
            }
            catch (Exception e) { Case(false, "the run stopped: " + e); Section("stopped"); }
            finally { ZWorld.HighGroundDeviation = keep; ZWorld.LosDeviation = los; }
            return "[OpenZ V1.3 high ground] " + (fail == 0 ? "PASS " : "FAIL ") + pass + " of " + (pass + fail) + (full ? "" : " (short form, no real shots)") + "\n" + sb;
        }

        static void H1(string s, string m)
        {
            int bad = 0;
            for (int r = 0; r <= 2000; r++) if (ZWorld.HighGroundRange(r) != (int)Math.Round(r * 1.1m, MidpointRounding.AwayFromZero)) bad++;
            Case(bad == 0, $"whole pixels: 0 .. 2000 px x 110 %, rounded to the nearest: {bad} differ from exact decimals");
            var w = Ue(s, m);
            var o = new StringBuilder();
            foreach (var t in AllTypes) { int r = w.S.Unit(t).attack_radius; o.Append(t).Append(' ').Append(r).Append(" -> ").Append(ZWorld.HighGroundRange(r)).Append("; "); }
            Case(ZWorld.HighGroundRangePct == 110, "the table: " + o);
            int[] field = new int[2], ring = new int[2]; int before = 0;
            foreach (bool on in new[] { false, true })
            {
                Rule(on);
                var wd = Ue(s, m); var plat = Plat(wd, 60, 30, ZWorld.PlatformHigh, 0);
                var g = Unit(wd, "gun", At(wd, plat, 5, 5), Team.Red);
                before = g.AttackRadius;
                for (int i = 0; i < 100; i++) { wd.Tick(ZConst.TICK); wd.Events.Clear(); }
                field[on ? 1 : 0] = g.AttackRadius; ring[on ? 1 : 0] = g.RingRadius;
            }
            Case(field[0] == before && field[1] == before && ring[0] == before && ring[1] == ZWorld.HighGroundRange(before), $"the field stays the plain range ({before} px) on a deck, rule off {field[0]}, on {field[1]}; the ring reads off {ring[0]} px, on {ring[1]} px");
        }

        static void H2(string s, string m)
        {
            foreach (int kind in new[] { ZWorld.PlatformMid, ZWorld.PlatformHigh })
                foreach (var type in AllTypes)
                {
                    var w = Ue(s, m);
                    var plat = Plat(w, 60, 30, kind, 0);
                    var u = Unit(w, type, At(w, plat, 5, 5), Team.Red);
                    Hold(u);
                    var t = Dummy(w, u.CenterX - 60, u.CenterY, Team.Red);
                    int r = u.AttackRadius, boost = ZWorld.HighGroundRange(r), top = boost + 24;
                    Rule(false); int off = Far(u, t, top); int ringOff = u.RingRadius;
                    Rule(true); int on = Far(u, t, top); int ringOn = u.RingRadius, level = u.StandLevel;
                    Case(off == r && on == boost && ringOff == r && ringOn == boost && level == (kind == ZWorld.PlatformHigh ? ZWorld.HighLevelTop : ZWorld.HighLevelMid),
                         $"{(kind == ZWorld.PlatformHigh ? "high" : "medium")} deck, {type}: range off {off} px, on {on} px (table {r}, wanted {boost}), ring {ringOff} / {ringOn}, level {level}");
                }
        }

        static void H3(string s, string m)
        {
            foreach (int kind in new[] { ZWorld.PlatformMid, ZWorld.PlatformHigh })
                foreach (var type in new[] { "grunt", "heavy" })
                    foreach (var (name, along) in new[] { ("upper ramp", 9.0 + ZWorld.PlatformRampRows[kind] * 0.25), ("lower ramp", 9.0 + ZWorld.PlatformRampRows[kind] * 0.75), ("ground past the foot", 9.0 + ZWorld.PlatformRampRows[kind] + 6) })
                    {
                        var w = Ue(s, m); Rule(true);
                        var plat = Plat(w, 60, 30, kind, 0);
                        var u = Unit(w, type, At(w, plat, 5, along), Team.Red);
                        var t = Dummy(w, u.CenterX - 60, u.CenterY, Team.Red);
                        int on = OnOff(() => u.RangeTo(t), out int off), ringOn = OnOff(() => u.RingRadius, out int ringOff), r = u.AttackRadius;
                        Case(u.StandLevel == 0 && on == r && off == r && ringOn == r && ringOff == r, $"{type} on the {name} (kind {kind}): level {u.StandLevel}, table {r}, range off {off}, on {on}, ring off {ringOff}, on {ringOn}");
                    }
            {
                var w = Ue(s, m); Rule(true);
                var plat = Plat(w, 60, 30, ZWorld.PlatformHigh, 0);
                var rail = At(w, plat, 0.5, 5);
                int on = OnOff(() => w.PlatformDeckLevel(rail.x >> 4, rail.y >> 4) + w.PlatformDeckLevel(50, 50), out int off);
                bool highOn = OnOff(() => w.HighGroundAt(rail.x, rail.y) || w.HighGroundAt(50 * 16, 50 * 16), out bool highOff);
                Case(on == 0 && off == 0 && !highOn && !highOff, $"a closed rail tile and the open ground are level 0, rule off and on: levels {off} / {on}, high ground {highOff} / {highOn}");
            }
        }

        /// <summary>Two platforms side by side, a grunt on each at 128 px: inside the extra band (120 .. 132).</summary>
        static (ZWorld w, ZObject a, ZObject b) TwoDecks(string s, string m, int kindA, int kindB)
        {
            var w = Ue(s, m);
            var pa = Plat(w, 40, 30, kindA, 0); var pb = Plat(w, 52, 30, kindB, 0);
            var a = Unit(w, "grunt", At(w, pa, 6.5, 5), Team.Red);
            var b = Dummy(w, a.CenterX + 128, a.CenterY, Team.Red);
            return (w, a, b);
        }

        static void H4(string s, string m)
        {
            foreach (var (ka, kb, name, wants) in new[] { (ZWorld.PlatformHigh, ZWorld.PlatformHigh, "high deck to high deck", false), (ZWorld.PlatformMid, ZWorld.PlatformMid, "medium deck to medium deck", false),
                                                         (ZWorld.PlatformHigh, ZWorld.PlatformMid, "high deck down to medium deck", true), (ZWorld.PlatformMid, ZWorld.PlatformHigh, "medium deck up to high deck", false) })
            {
                var (w, a, b) = TwoDecks(s, m, ka, kb);
                Rule(false); bool off = a.WithinAttackRadius(b);
                Rule(true); bool on = a.WithinAttackRadius(b); int r = a.RangeTo(b);
                Case(!off && on == wants && r == (wants ? 132 : 120), $"{name}, a grunt at 128 px: reach off {off}, on {on}, range {r} (wanted {(wants ? 132 : 120)})");
            }
            {
                var w = Ue(s, m); Rule(true);
                var plat = Plat(w, 60, 30, ZWorld.PlatformHigh, 0);
                var u = Unit(w, "grunt", At(w, plat, 3, 3), Team.Red);
                var same = Dummy(w, At(w, plat, 7, 7).x, At(w, plat, 7, 7).y, Team.Red);
                var ramp = Dummy(w, At(w, plat, 5, 16).x, At(w, plat, 5, 16).y, Team.Red);
                int sameOn = OnOff(() => u.RangeTo(same), out int sameOff), rampOn = OnOff(() => u.RangeTo(ramp), out int rampOff);
                Case(sameOn == 120 && sameOff == 120, $"the same deck: range off {sameOff}, on {sameOn}");
                Case(rampOn == 132 && rampOff == 120, $"a deck down onto its own ramp: range off {rampOff}, on {rampOn}");
                var low = Unit(w, "grunt", (At(w, plat, 5, 5).x - 300, At(w, plat, 5, 5).y), Team.Red);
                var top = Dummy(w, At(w, plat, 5, 5).x, At(w, plat, 5, 5).y, Team.Red);
                int upOn = OnOff(() => low.RangeTo(top), out int upOff), downOn = OnOff(() => top.RangeTo(low), out int downOff);
                Case(upOn == 120 && upOff == 120 && downOn == ZWorld.HighGroundRange(top.AttackRadius) && downOff == top.AttackRadius, $"the ground up to a deck: range off {upOff}, on {upOn}; down from it off {downOff}, on {downOn}");
            }
        }

        static void H5(string s, Func<string, string> level)
        {
            int total = 0;
            foreach (var lv in Levels)
            {
                var map = level(lv); if (map == null) { Case(false, lv + ": no map"); continue; }
                var w = ZWorld.Create(s, map, 1); w.NoGameEnd = true; w.Events.Clear();
                var guns = w.Objects.Where(o => o.IsTowerGun && !o.Destroyed).ToList();
                total += guns.Count;
                foreach (var g in guns)
                {
                    int r = g.AttackRadius, boost = ZWorld.HighGroundRange(r);
                    var other = guns.FirstOrDefault(x => x != g);
                    var d = Dummy(w, g.CenterX, g.CenterY + 2000, g.Owner == Team.Red ? Team.Red : Team.Blue);
                    Rule(false); bool offOk = g.RingRadius == r && g.RangeTo(d) == r;
                    Rule(true);
                    bool onOk = g.RingRadius == boost && g.StandLevel == ZWorld.HighLevelTop && g.RangeTo(d) == boost && (other == null || g.RangeTo(other) == r);
                    // a real spot in the extra band: the ground only, a clear line
                    int spots = 0; string first = "none";
                    for (int deg = 0; deg < 360 && spots < 12; deg += 3)
                    {
                        double a = deg * Math.PI / 180.0; int dist = r + Math.Max(1, (boost - r) / 2);
                        int px = g.CenterX + (int)(dist * Math.Cos(a)), py = g.CenterY + (int)(dist * Math.Sin(a));
                        if (px < 32 || py < 32 || px >= w.Map.width * 16 - 32 || py >= w.Map.height * 16 - 32) continue;
                        Put(d, px, py);
                        if (Math.Sqrt((double)(d.CenterX - g.CenterX) * (d.CenterX - g.CenterX) + (double)(d.CenterY - g.CenterY) * (d.CenterY - g.CenterY)) <= r) continue;
                        Rule(false); bool o0 = g.WithinAttackRadius(d);
                        Rule(true); bool o1 = g.WithinAttackRadius(d);
                        if (o0) { onOk = false; first = "reached with the rule off at " + deg + " deg"; break; }
                        if (o1) { spots++; if (spots == 1) first = deg + " deg"; }
                    }
                    Case(offOk && onOk && spots > 0, $"{lv} {g.Owner} {g.Type} #{g.Id}: table {r}, ring off {r} on {boost}, at another tower gun {(other != null ? g.RangeTo(other).ToString() : "no other")}, spots in the extra band with a clear line {spots} (first {first})");
                    w.RemoveNow(d);
                }
            }
            Case(total > 0, $"{total} tower guns on {Levels.Length} levels");
        }

        static object Priv(object on, string name, params object[] a)
        {
            var type = on is Type t0 ? t0 : on.GetType();
            var m = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).First(x => x.Name == name && x.GetParameters().Length == a.Length);
            return m.Invoke(on is Type ? null : on, a);
        }

        static void H6(string s, string m)
        {
            {
                var w = Ue(s, m); var plat = Plat(w, 60, 30, ZWorld.PlatformHigh, 0);
                var u = Unit(w, "light", At(w, plat, 5, 5), Team.Red); var t = Dummy(w, u.CenterX - 128, u.CenterY, Team.Red);
                Rule(false); bool off = (bool)Priv(u, "DriverFireValid", t, 120);
                Rule(true); bool on = (bool)Priv(u, "DriverFireValid", t, 120);
                Case(!off && on, $"the hatch driver's rifle (120 px) at 128 px from a deck: off {off}, on {on}");
                Put(t, u.CenterX - 133, u.CenterY);
                bool pastOn = OnOff(() => (bool)Priv(u, "DriverFireValid", t, 120), out bool pastOff);
                Case(!pastOn && !pastOff, $"the hatch driver's rifle at 133 px, past the extra band: off {pastOff}, on {pastOn}");
            }
            {
                var w = Ue(s, m); var plat = Plat(w, 60, 30, ZWorld.PlatformHigh, 0);
                var u = Unit(w, "crane", At(w, plat, 5, 5), Team.Red); var t = Dummy(w, u.CenterX - 128, u.CenterY, Team.Red);
                t.Engage(u);
                Rule(false); bool off = (bool)Priv(u, "CraneGunValid", t, 120);
                Rule(true); bool on = (bool)Priv(u, "CraneGunValid", t, 120);
                Case(!off && on, $"the crane's rifle (120 px) at 128 px from a deck: off {off}, on {on}");
            }
            {
                var w = Ue(s, m); var plat = Plat(w, 60, 30, ZWorld.PlatformHigh, 0);
                var u = Unit(w, "medium", At(w, plat, 5, 5), Team.Red); var t = Dummy(w, u.CenterX - 128, u.CenterY, Team.Red);
                Rule(false); bool off = (bool)Priv(u, "HandGunValid", t, 120, t.CenterX, t.CenterY);
                Rule(true); bool on = (bool)Priv(u, "HandGunValid", t, 120, t.CenterX, t.CenterY);
                Case(!off && on, $"the mech's hand gun (120 px) at 128 px from a deck: off {off}, on {on}");
            }
        }

        static void H7(string s, string m)
        {
            {
                var w = Ue(s, m); int bad = 0, agro = w.S.agro_distance;
                foreach (var t in AllTypes) { int r = w.S.Unit(t).attack_radius; if (ZWorld.HighGroundRange(r) > r + agro) bad++; }
                Case(bad == 0, $"the default table: the longer range never passes range + agro_distance ({agro} px): {bad} weapons do");
                var plat = Plat(w, 60, 30, ZWorld.PlatformHigh, 0);
                var u = Unit(w, "grunt", At(w, plat, 5, 5), Team.Red); var d = Dummy(w, u.CenterX - 160, u.CenterY, Team.Red);
                int far = 120 + agro;
                foreach (bool on in new[] { false, true })
                {
                    Rule(on);
                    Put(d, u.CenterX - far, u.CenterY); bool at = u.WithinAgroRadius(d);
                    Put(d, u.CenterX - far - 1, u.CenterY); bool past = u.WithinAgroRadius(d);
                    Case(at && !past, $"default numbers, rule {(on ? "on" : "off")}: agro reach {far} px yes, {far + 1} px no: unchanged");
                }
            }
            {
                var w = Ue(s, m); var plat = Plat(w, 60, 30, ZWorld.PlatformHigh, 0);
                var u = Unit(w, "grunt", At(w, plat, 5, 5), Team.Red); u.AttackRadius = 600;
                var d = Dummy(w, u.CenterX - 650, u.CenterY, Team.Red);
                Rule(false); bool off = u.WithinAgroRadius(d);
                Rule(true); bool on = u.WithinAgroRadius(d); bool shot = u.WithinAttackRadius(d);
                Put(d, u.CenterX - 661, u.CenterY); bool past = u.WithinAgroRadius(d);
                Case(!off && on && shot && !past, $"a custom range of 600 px (660 from the deck, agro reach 640): at 650 px agro off {off}, on {on}, in range {shot}; at 661 px {past}: stretched to the range and no further");
            }
            {
                var w = Ue(s, m); var plat = Plat(w, 60, 30, ZWorld.PlatformHigh, 0);
                var low = Unit(w, "grunt", (At(w, plat, 5, 5).x - 100, At(w, plat, 5, 5).y), Team.Red);
                var far = Dummy(w, At(w, plat, 5, 5).x + 100, At(w, plat, 5, 5).y, Team.Red);
                Rule(false); bool off = low.HasLineOfSight(far);
                Rule(true); bool on = low.HasLineOfSight(far);
                Case(off == on && !on, $"DV23: ground to ground across the platform: blocked off {!off}, on {!on}: the line does not change");
            }
        }

        static void H8(string s, string m, Func<string, string> level)
        {
            {
                var w = Ue(s, m); Rule(true);
                var plat = Plat(w, 60, 30, ZWorld.PlatformHigh, 0);
                var u = Unit(w, "gun", At(w, plat, 5, 5), Team.Red);
                var ship = w.CreatePlayableShuttle(u.CenterX - 300, u.CenterY, Team.Blue);
                ship.ShuttlePlace(u.CenterX - 300, u.CenterY);
                int parkOn = OnOff(() => u.RangeTo(ship), out int parkOff);
                Case(!ship.ShuttleAirborne && ship.TargetLevel == 0 && parkOn == ZWorld.HighGroundRange(u.AttackRadius) && parkOff == u.AttackRadius, $"a parked shuttle is ground: range off {parkOff}, on {parkOn}");
                int ex = w.Sight.ExemptOf(ship); bool lineOn = w.Sight.Clear(u, ship);
                ship.ShuttleState = ZShuttleState.Hover; ship.ShuttleLift = 1;
                int airOn = OnOff(() => u.RangeTo(ship), out int airOff);
                Case(ship.TargetLevel == ZWorld.HighLevelAir && airOn == u.AttackRadius && airOff == u.AttackRadius, $"the flying shuttle is above a high deck: range off {airOff}, on {airOn}, table {u.AttackRadius}");
                Rule(false); bool clearOff = w.Sight.Clear(u, ship); int exOff = w.Sight.ExemptOf(ship);
                Rule(true); bool clearOn = w.Sight.Clear(u, ship); int exOn = w.Sight.ExemptOf(ship);
                Case(clearOff == clearOn && exOff == exOn && clearOn, "the shuttle's sight: the same on and off, a clear line to it in the air");
            }
            {
                var w = ZWorld.Create(s, level("L02"), 1); w.NoGameEnd = true; Rule(true);
                var g = w.Objects.First(o => o.IsTowerGun);
                var ship = w.CreatePlayableShuttle(g.CenterX - 180, g.CenterY, Team.Blue == g.Owner ? Team.Red : Team.Blue);
                ship.ShuttleState = ZShuttleState.Hover; ship.ShuttleLift = 1;
                int airOn = OnOff(() => g.RangeTo(ship), out int airOff);
                Case(airOn == g.AttackRadius && airOff == g.AttackRadius, $"a tower gun at the flying shuttle: range off {airOff}, on {airOn}, table {g.AttackRadius}");
                var def = Dummy(w, g.CenterX - 600, g.CenterY, g.Owner);
                int groundOn = OnOff(() => g.RangeTo(def), out int groundOff);
                Case(groundOn == ZWorld.HighGroundRange(g.AttackRadius) && groundOff == g.AttackRadius, $"a tower gun at a robot on the ground: range off {groundOff}, on {groundOn}");
                typeof(ZObject).GetProperty("DefenderFortId").GetSetMethod(true).Invoke(def, new object[] { 1 });
                int roofOn = OnOff(() => g.RangeTo(def), out int roofOff);
                Case(roofOn == g.AttackRadius && roofOff == g.AttackRadius && def.TargetLevel == ZWorld.HighLevelTop, $"a tower gun at a roof defender: same level, range off {roofOff}, on {roofOn}");
                int ringOn = OnOff(() => def.RingRadius, out int ringOff);
                Case(def.StandLevel == 0 && ringOn == def.AttackRadius && ringOff == def.AttackRadius, $"a roof defender's own range: no change: ring off {ringOff}, on {ringOn}");
            }
        }

        static void H9(string s, string m)
        {
            bool[] walks = new bool[2]; string said = "";
            foreach (bool on in new[] { true, false })
            {
                var w = Ue(s, m); Rule(on);
                var plat = Plat(w, 60, 30, ZWorld.PlatformMid, 0);
                var ground = At(w, plat, 5, 22); var deck = At(w, plat, 5, 5);
                var u = Unit(w, "grunt", ground, Team.Red);
                int r0 = u.RingRadius;
                OzV11Kit.PlayerMove(w, u, deck.x, deck.y, false);
                double t = 0; while (t < 60.0 && (u.IsMoving || u.StandLevel == 0)) { w.Tick(ZConst.TICK); w.Events.Clear(); t += ZConst.TICK; }
                int r1 = u.RingRadius, lv1 = u.StandLevel;
                OzV11Kit.PlayerMove(w, u, ground.x, ground.y, false);
                t = 0; while (t < 60.0 && (u.IsMoving || u.StandLevel != 0)) { w.Tick(ZConst.TICK); w.Events.Clear(); t += ZConst.TICK; }
                int r2 = u.RingRadius;
                walks[on ? 1 : 0] = r0 == 120 && r1 == (on ? 132 : 120) && r2 == 120 && lv1 == ZWorld.HighLevelMid;
                said += $"rule {(on ? "on" : "off")}: ring {r0}, on the deck {r1} (level {lv1}), back {r2}; ";
            }
            Case(walks[0] && walks[1], "a grunt walks up and down: " + said);
            {
                var w = Ue(s, m); var plat = Plat(w, 60, 30, ZWorld.PlatformHigh, 0);
                var deck = At(w, plat, 5, 5); var ramp = At(w, plat, 5, 12);
                Rule(true); bool a = w.HighGroundAt(deck.x, deck.y), b = w.HighGroundAt(ramp.x, ramp.y), c = w.HighGroundAt(400, 400);
                Rule(false); bool d = w.HighGroundAt(deck.x, deck.y);
                Case(a && !b && !c && !d, $"the Unit editor's preview: deck {a}, ramp {b}, ground {c}, deck with the rule off {d}");
            }
        }

        static void H10(string s, Func<string, string> level)
        {
            var w = ZWorld.Create(s, level("L02"), 1); w.NoGameEnd = true; w.Bots.Add(new ZBot(w, Team.Red, 7));
            var g = w.Objects.First(o => o.IsTowerGun && o.Drivers.Count > 0 && o.Owner == Team.Red);
            int r = g.AttackRadius, dist = r + (ZWorld.HighGroundRange(r) - r) / 2 + 1;
            var d = Dummy(w, 0, 0, Team.Red); ZObject spot = null;
            for (int deg = 0; deg < 360; deg += 3)
            {
                double a = deg * Math.PI / 180.0; Put(d, g.CenterX + (int)(dist * Math.Cos(a)), g.CenterY + (int)(dist * Math.Sin(a)));
                Rule(true); if (g.WithinAttackRadius(d) && !g.IsMoving) { Rule(false); if (!g.WithinAttackRadius(d)) { spot = d; break; } }
            }
            Case(spot != null, $"a CPU side's tower gun has a spot in the extra band: {g.Type} #{g.Id}");
            if (spot == null) return;
            int fight = ZWorld.HighGroundRange(r) + 10; bool line = false;                        // past the old reach (range + 16 px), inside the new one
            for (int deg = 0; deg < 360 && !line; deg += 3)
            {
                double a = deg * Math.PI / 180.0; Put(d, g.CenterX + (int)(fight * Math.Cos(a)), g.CenterY + (int)(fight * Math.Sin(a)));
                line = g.HasLineOfSight(d) && OzV11Kit.Dist(g, d) > r + 16;
            }
            Rule(false); bool off = (bool)Priv(typeof(ZBot), "InReach", g, d);
            Rule(true); bool on = (bool)Priv(typeof(ZBot), "InReach", g, d);
            Case(line && on && !off, $"ZBot.FinishGame's 'really fighting' at {OzV11Kit.Dist(g, d):0} px: the longer range + 16 px: off {off}, on {on}");
            int margin = ZWorld.ChargeDangerMarginPx, past = ZWorld.HighGroundRange(r) + margin - 1;
            var jeep = w.CreateObject("vehicle", "jeep", 64, 64, Team.Blue);
            var t2 = Dummy(w, g.CenterX, g.CenterY + past, Team.Blue == g.Owner ? Team.Red : Team.Blue);
            bool fortGun = ZWorld.FortGunDeviation; ZWorld.FortGunDeviation = false;              // the fort's own gun (DV7) reaches there anyway: the tower gun alone is asked
            Rule(false); bool jOff = (bool)Priv(jeep, "ChargeUnderFire", t2);
            Rule(true); bool jOn = (bool)Priv(jeep, "ChargeUnderFire", t2);
            ZWorld.FortGunDeviation = fortGun;
            Case(!jOff && jOn, $"the jeep's fear of a crewed gun reaches the longer range: off {jOff}, on {jOn}");
        }

        static bool Fired(ZObject u, ZWorld w, double sec, ZObject t, out double at)
        {
            at = -1;
            int n = (int)(sec / ZConst.TICK);
            for (int i = 0; i < n; i++)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                    if (e.objId == u.Id && e.otherId == t.Id && (e.type == ZEventType.Hitscan || e.type == ZEventType.MissileFired || e.type == ZEventType.ElectroCharge || e.type == ZEventType.ElectroBolt)) { at = i * ZConst.TICK; return true; }
                    else if (e.objId == u.Id && e.type == ZEventType.MissileFired && u.AttackObject == t) { at = i * ZConst.TICK; return true; }
                w.Events.Clear();
            }
            return false;
        }

        static void H11(string s, string m, Func<string, string> level)
        {
            foreach (int kind in new[] { ZWorld.PlatformMid, ZWorld.PlatformHigh })
                foreach (var type in AllTypes)
                {
                    int r = -1, boost = -1; bool[] got = new bool[3];
                    for (int c = 0; c < 3; c++)
                    {
                        Rule(c != 1);
                        var w = Ue(s, m); var plat = Plat(w, 60, 30, kind, 0);
                        var u = Unit(w, type, At(w, plat, 5, 5), Team.Red); Hold(u);
                        r = u.AttackRadius; boost = ZWorld.HighGroundRange(r);
                        int d = c == 2 ? boost + 3 : boost - 1;
                        var t = Dummy(w, u.CenterX - d, u.CenterY, Team.Red);
                        got[c] = Fired(u, w, 14.0, t, out _);
                    }
                    Case(got[0] && !got[1] && !got[2], $"{(kind == 1 ? "high" : "medium")} deck, {type}: fires at {boost - 1} px rule on {got[0]}, rule off {got[1]}; at {boost + 3} px {got[2]}");
                }
            foreach (var lv in Levels)
                foreach (var side in new[] { Team.Red, Team.Blue })
                {
                    bool[] got = new bool[2]; string name = "no crewed tower gun"; bool any = false;
                    for (int c = 0; c < 2; c++)
                    {
                        Rule(c == 0);
                        var w = ZWorld.Create(s, level(lv), 1); w.NoGameEnd = true; w.Bots.Add(new ZBot(w, side, 7)); w.Events.Clear();
                        var g = w.Objects.FirstOrDefault(o => o.IsTowerGun && o.Drivers.Count > 0 && o.Owner == side);
                        if (g == null) break;
                        any = true; name = g.Type + " #" + g.Id;
                        int r = g.AttackRadius, dist = r + Math.Max(1, (ZWorld.HighGroundRange(r) - r) / 2);
                        var d = Dummy(w, 0, 0, side); bool placed = false;
                        bool keep = ZWorld.HighGroundDeviation; Rule(true);
                        for (int deg = 0; deg < 360 && !placed; deg += 3)
                        {
                            double a = deg * Math.PI / 180.0; Put(d, g.CenterX + (int)(dist * Math.Cos(a)), g.CenterY + (int)(dist * Math.Sin(a)));
                            placed = g.WithinAttackRadius(d);
                        }
                        Rule(keep);
                        got[c] = placed && Fired(g, w, 14.0, d, out _);
                    }
                    if (any) Case(got[0] && !got[1], $"{lv} {side} (CPU side) {name}: a real shot in the extra band: rule on {got[0]}, off {got[1]}");
                }
        }

        static void H12(string s, string m)
        {
            foreach (var (type, sw) in new[] { ("heavy", (string)null), ("sniper", "SniperSteadyDeviation"), (ZElectro.Type, "ElectroSteadyDeviation") })
            {
                bool[] onDeck = new bool[2], fired = new bool[2]; double[] moved = new double[2];
                for (int c = 0; c < 2; c++)
                {
                    Rule(c == 0);
                    if (sw != null) OzV11Kit.TrySet(typeof(ZWorld), sw, true);
                    var w = Ue(s, m); var plat = Plat(w, 60, 30, ZWorld.PlatformHigh, 0);
                    var u = Unit(w, type, At(w, plat, 5, 5), Team.Red);
                    int r = u.AttackRadius, d = r + (ZWorld.HighGroundRange(r) - r) / 2; int x0 = u.CenterX, y0 = u.CenterY;
                    ZObject t;
                    if (type == "heavy") { t = Unit(w, "light", (u.CenterX - d, u.CenterY), Team.Blue); t.MoveSpeed = 0; t.Damage = 0; t.MaxHealth = t.Health = 1000000000; }
                    else t = Dummy(w, u.CenterX - d, u.CenterY, Team.Red);
                    OzV11Kit.PlayerAttack(w, u, t);
                    fired[c] = Fired(u, w, 10.0, t, out _);
                    for (int i = 0; i < 300; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); }
                    onDeck[c] = u.StandLevel == ZWorld.HighLevelTop; moved[c] = Math.Sqrt((double)(u.CenterX - x0) * (u.CenterX - x0) + (double)(u.CenterY - y0) * (u.CenterY - y0));
                }
                Case(fired[0] && onDeck[0] && moved[1] > moved[0], $"{type} on an attack order in the band: rule on fires {fired[0]}, on the deck {onDeck[0]}, moved {moved[0]:0} px; rule off moved {moved[1]:0} px");
            }
        }

        static ulong Hash(ZWorld w)
        {
            ulong h = 1469598103934665603UL;
            void M(long v) { unchecked { h = (h ^ (ulong)v) * 1099511628211UL; } }
            foreach (var o in w.Objects) { M(o.Id); M(o.LocX); M(o.LocY); M(o.Health); M(o.KillAt < 0 ? 0 : 1); M(o.Waypoints.Count); }
            M(w.Objects.Count); M(w.Rng.Next(1000000));
            return h;
        }

        static void H13(string s, Func<string, string> level)
        {
            foreach (var lv in new[] { "L02", "L17" })
            {
                ulong[] h = new ulong[4];
                for (int c = 0; c < 4; c++)
                {
                    Rule(c < 2);
                    var w = ZWorld.Create(s, level(lv), 5);
                    w.Bots.Add(new ZBot(w, Team.Red, 105)); w.Bots.Add(new ZBot(w, Team.Blue, 106));
                    for (int i = 0; i < 12000; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); }
                    h[c] = Hash(w);
                }
                Case(h[0] == h[1] && h[2] == h[3], $"{lv}, CPU against CPU, 120 s, played twice: rule on {h[0]:x16} / {h[1]:x16}, rule off {h[2]:x16} / {h[3]:x16}");
            }
        }

        /// <summary>One run of H14: the level past its calm start with only the gun on it, one unit put at 156 px with a clear line. real: the game's numbers; else a gun that fires
        /// harmless shots at a unit that does not die, so the only question is whether it answers. answers: shots at the gun; gunShots: shots at the unit; life: s, -1 alive after the minute.</summary>
        static bool Answer(string s, string map, string type, bool real, bool on, out int answers, out double first, out int gunShots, out double life, out int gunHp)
        {
            answers = gunShots = 0; first = life = -1; gunHp = 0;
            var w = ZWorld.Create(s, map, 1); w.NoGameEnd = true; w.Events.Clear();
            var g = w.Objects.FirstOrDefault(o => o.IsTowerGun && o.Type == "missile_cannon" && !o.Destroyed);
            if (g == null) return false;
            foreach (var o in w.Objects.ToList()) if (o != g && o.IsUnit && !g.Drivers.Contains(o.Id)) w.RemoveNow(o);
            for (int i = 0; i < (int)(21.0 / ZConst.TICK); i++) { w.Tick(ZConst.TICK); w.Events.Clear(); }   // DV69's calm start: nothing self-started in the first 20 s
            Rule(true);                                                                                        // the same spot both ways: in the gun's longer reach, in the unit's agro reach
            int dist = ZWorld.HighGroundRange(g.AttackRadius) - 2; ZObject u = null;
            for (int deg = 0; deg < 360 && u == null; deg += 5)
            {
                double a = deg * Math.PI / 180.0; int px = g.CenterX + (int)(dist * Math.Cos(a)), py = g.CenterY + (int)(dist * Math.Sin(a));
                if (px < 64 || py < 64 || px >= w.Map.width * 16 - 64 || py >= w.Map.height * 16 - 64 || w.Map.ClassAt(px >> 4, py >> 4) == ZClass.Impassable) continue;
                var c = OzV11Kit.Place(w, type, px, py, g.Owner == Team.Red ? Team.Blue : Team.Red, 1);
                if (c == null) continue;
                if (g.WithinAttackRadius(c) && c.WithinAgroRadius(g) && !c.WithinAttackRadius(g) && c.CenterX >> 4 == px >> 4) u = c; else w.RemoveNow(c);
            }
            Rule(on);
            if (u == null) return false;
            if (!real) { u.MaxHealth = u.Health = 1000000000; g.Damage = 1; g.DamageRadius = 0; }
            for (int i = 0; i < (int)(60.0 / ZConst.TICK); i++)
            {
                if (u.Destroyed || u.KillAt >= 0) { life = i * ZConst.TICK; break; }
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                    if (e.type == ZEventType.Hitscan || e.type == ZEventType.MissileFired || e.type == ZEventType.ElectroCharge || e.type == ZEventType.ElectroBolt)
                    {
                        if (e.objId == u.Id && e.otherId == g.Id) { answers++; if (first < 0) first = i * ZConst.TICK; }
                        if (e.objId == g.Id && e.otherId == u.Id) gunShots++;
                    }
                w.Events.Clear();
            }
            gunHp = g.Destroyed ? 0 : g.Health;
            return true;
        }

        /// <summary>The door tower's missile cannon (DV73, Hard) reaches 158 px from its tower, past the 144 px of a heavy, a sniper, an Electrobot. Harmless gun: each answers it, rule on and off.
        /// Real gun: the line counts what happens; the case asks that it opens fire in the band with the rule on, and that with the rule off the unit gets its shots in.</summary>
        static void H14(string s, Func<string, string> level)
        {
            bool door = ZWorld.FortDoorGunDeviation, tower = ZWorld.TowerAnswerDeviation, sniper = ZWorld.SniperSteadyDeviation, electro = ZWorld.ElectroSteadyDeviation;
            ZWorld.FortDoorGunDeviation = ZWorld.TowerAnswerDeviation = ZWorld.SniperSteadyDeviation = ZWorld.ElectroSteadyDeviation = true;
            try
            {
                foreach (var lv in Levels)
                    foreach (var type in new[] { "heavy", "sniper", ZElectro.Type })
                    {
                        bool a = Answer(s, level(lv), type, false, true, out int n1, out double f1, out int g1, out _, out _), b = Answer(s, level(lv), type, false, false, out int n0, out double f0, out int g0, out _, out _);
                        Case(a && b && n1 > 0 && n0 > 0, $"{lv} {type} at a harmless tower missile cannon: rule on {n1} shots at it, first at {f1:0.0} s (gun fired {g1}), rule off {n0}, first at {f0:0.0} s (gun fired {g0})");
                        a = Answer(s, level(lv), type, true, true, out n1, out f1, out g1, out double l1, out int h1); b = Answer(s, level(lv), type, true, false, out n0, out f0, out g0, out double l0, out int h0);
                        Case(a && b && g1 > 0 && n0 > 0, $"{lv} {type} at the real gun: rule on {n1} shots at it, gun fired {g1}, unit dead at {l1:0.0} s, gun health left {h1}; rule off {n0} shots, gun fired {g0}, unit dead at {l0:0.0} s, gun health left {h0}");
                    }
            }
            finally { ZWorld.FortDoorGunDeviation = door; ZWorld.TowerAnswerDeviation = tower; ZWorld.SniperSteadyDeviation = sniper; ZWorld.ElectroSteadyDeviation = electro; }
        }

#if UNITY_EDITOR
        [MenuItem("OpenZ/V1.3/High ground range: counted (headless, DV162)")]
        static void Menu()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.3] high ground: leave Play mode first (the check sets the simulation's static switches)."); return; }
            using (OzV11Kit.GameSwitches())
            {
                var r = Check(OzV11Kit.SettingsText, OzV11Kit.MapText("UE"), OzV11Kit.MapText, true);
                if (r.Contains("] PASS")) Debug.Log(r); else Debug.LogError(r);
            }
        }
#endif
    }
}
