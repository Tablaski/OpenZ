using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit ram_first: the counted check of DV189 "ram first" (Sim/ZObject.RamFirst.cs), headless, every DV180-188 switch on as in a Play
    /// session. Rows: which targets are RAM-FIRST (both tanks, every vehicle and gun type); the Mammoth idle at gun range of a medium tank /
    /// heavy / missile launcher (stepping aside from its shells as Zod's units do) drives in and rams within
    /// <see cref="RamWithin"/> ticks, its gun firing on the way; the same on the player's ATTACK order; the jeep and the light tank: the scene field
    /// by field the same with DV189 on and off (it stands and shoots); the nearest RAM-FIRST target first (a jeep nearer beside), ties by object
    /// order; the Bizon crushes a medium tank, a heavy and a ground gun; HOLD POSITION: no chase (on = off field by field); a plain MOVE order is
    /// obeyed (no chase on the way); its own tank parked in the way: no hang (it rams or falls back and shoots); the same scene twice = the same fingerprint;
    /// DV189 off: no DV189 field of any unit ever leaves its start value, no chase, no ram from gun range.
    /// </summary>
    public static class OzV15RamFirstCount
    {
        /// <summary>The longest time from the start (gun range, the target stepping aside from the shells as Zod's units do) to the ram / crush
        /// (20 s; about 9 s of it is the Mammoth's drive at 1 m/s).</summary>
        public const int RamWithin = 2000;

        public static string Quick(string settings) => Run(settings, false);

        static readonly string[] Switches = { "MammothDeviation", "BizonDeviation", "MammothRamDeviation", "BizonCrushDeviation", "RamDuelDeviation", "CrushRollOffDeviation", "RamDebrisDeviation", "RamPolishDeviation" };

        public static string Run(string settings, bool full = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DV189 ram first, counted (Sim/ZObject.RamFirst.cs)");
            var keep = new Dictionary<string, bool>();
            foreach (var n in Switches.Concat(new[] { "BigTankRamFirstDeviation", "CalmStartDeviation" })) keep[n] = Get(n);
            try
            {
                foreach (var n in Switches) Set(n, true);
                Set("CalmStartDeviation", false);                                            // the checks' red team counts as the player's: no 20 s calm start
                Set("BigTankRamFirstDeviation", true);
                Kinds(sb, settings);
                foreach (var t in new[] { ZMTank.Type, "heavy", "missile_launcher" }) DriveIn(sb, settings, ZBigTanks.Mammoth, t, false);
                foreach (var t in new[] { ZMTank.Type, "heavy" }) DriveIn(sb, settings, ZBigTanks.Mammoth, t, true);
                foreach (var t in new[] { "jeep", "light" }) Unchanged(sb, settings, t, false);
                Nearest(sb, settings); Ties(sb, settings);
                foreach (var t in new[] { ZMTank.Type, "heavy", "gatling" }) DriveIn(sb, settings, ZBigTanks.Bizon, t, false);
                Unchanged(sb, settings, ZMTank.Type, true);
                PlainMove(sb, settings);
                Blocked(sb, settings);
                string a = Print(settings, 31), b = Print(settings, 31);
                sb.AppendLine(OzV11Kit.Verdict(a == b, "the same scene twice (DV189 on): the same fingerprint " + a.GetHashCode().ToString("x8")));
                Set("BigTankRamFirstDeviation", false);
                Off(sb, settings);
            }
            catch (Exception e) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace?.Split('\n').FirstOrDefault())); }
            finally { foreach (var kv in keep) Set(kv.Key, kv.Value); }
            return sb.ToString();
        }

        static bool Get(string n) { var f = typeof(ZWorld).GetField(n, BindingFlags.Public | BindingFlags.Static); return f != null && (bool)f.GetValue(null); }
        static void Set(string n, bool v) => OzV11Kit.TrySet(typeof(ZWorld), n, v);

        static ZObject Unit(ZWorld w, string type, int cx, int cy, Team t, float fx, float fy, bool armed)
        {
            var o = OzV11Kit.Place(w, type, cx, cy, t); o.FaceX = fx; o.FaceY = fy;
            if (!armed) { o.Damage = 0; o.MaxHealth = o.Health = 1000000000; }
            return o;
        }

        static bool Shot(ZEvent e) => e.type == ZEventType.MissileFired || e.type == ZEventType.Hitscan;

        static ZWorld NewWorld(string settings, int seed, string map = null)
        {
            var w = OzV11Kit.World(settings, map ?? OzV11Kit.Plain(64, 48), seed);
            OzV11Kit.Dummy(w, 980, 730);
            return w;
        }

        /// <summary>The tank at (320, 384) facing +x, the target ahead at gun range (turned away, unarmed); idle or on the player's ATTACK order.</summary>
        static void DriveIn(StringBuilder sb, string settings, string tank, string victim, bool order)
        {
            var w = NewWorld(settings, 41);
            var a = Unit(w, tank, 320, 384, Team.Red, 9f, 0f, true);
            int range = a.AttackRadius, d = range - 20;
            var v = Unit(w, victim, 320 + d, 384, Team.Blue, 9f, 0f, false);
            int gap0 = d - ZPathing.Radius(a) - ZPathing.Radius(v);
            if (order) OzV11Kit.PlayerAttack(w, a, v);
            long ramAt = -1; int shots = 0, x0 = a.CenterX, xAtRam = -1;
            for (int i = 0; i < RamWithin + 400 && ramAt < 0; i++)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events) if (Shot(e) && e.objId == a.Id && e.otherId == v.Id) shots++;
                w.Events.Clear();
                if (v.RamDoomBy == a.Id) { ramAt = w.TickIndex; xAtRam = a.CenterX; }
            }
            bool crush = tank == ZBigTanks.Bizon;
            bool ok = ramAt > 0 && ramAt <= RamWithin && shots >= 1 && xAtRam - x0 >= 16 && gap0 > ZObject.RamStartGapPx && (!crush || v.RamCrushed);
            sb.AppendLine(OzV11Kit.Verdict(ok, $"{tank} {(order ? "on the player's ATTACK order" : "idle")} vs {victim} at gun range ({d} px, hull gap {gap0} px > the ram's {ZObject.RamStartGapPx}): "
                + $"{(crush ? "crushed" : "rammed")} at tick {ramAt} (<= {RamWithin}), drove in {xAtRam - x0} px first, {shots} shot(s) at it on the way (chases {w.RamFirstChases}, drive ticks {w.RamFirstDriveTicks}, idle picks {w.RamFirstPicks})"));
        }

        /// <summary>Which targets are RAM-FIRST (crewed enemies, one of each type): the Mammoth every vehicle but the jeep and the light tank, no gun;
        /// the Bizon the same + every ground gun.</summary>
        static void Kinds(StringBuilder sb, string settings)
        {
            var veh = new[] { "jeep", "light", "medium", ZMTank.Type, "heavy", "apc", "missile_launcher", "crane", ZBigTanks.Bizon, ZBigTanks.Mammoth };
            var guns = new[] { "gatling", "gun", "howitzer", "missile_cannon" };
            foreach (var tank in new[] { ZBigTanks.Mammoth, ZBigTanks.Bizon })
            {
                var w = NewWorld(settings, 39);
                var a = Unit(w, tank, 160, 384, Team.Red, 9f, 0f, true);
                var got = new List<string>(); bool ok = true; int i = 0;
                foreach (var ty in veh.Concat(guns))
                {
                    var o = Unit(w, ty, 300 + 60 * (i % 10), 120 + 200 * (i / 10), Team.Blue, 9f, 0f, false); i++;
                    bool want = ty != "jeep" && ty != "light" && (tank == ZBigTanks.Bizon || !guns.Contains(ty)), have = a.RamFirstTarget(o);
                    ok &= want == have; if (have) got.Add(ty);
                }
                var own = Unit(w, "heavy", 300, 700, Team.Red, 9f, 0f, false);
                ok &= !a.RamFirstTarget(own);
                sb.AppendLine(OzV11Kit.Verdict(ok, $"{tank}: RAM-FIRST targets: {string.Join(", ", got)} (never the jeep, the light tank{(tank == ZBigTanks.Mammoth ? ", a gun" : "")}, its own team: own heavy {a.RamFirstTarget(own)})"));
            }
        }

        /// <summary>DV189 on vs off, the same scene, every field of every unit each 10 ticks: jeep / light tank (idle), or a medium tank on HOLD POSITION.</summary>
        static void Unchanged(StringBuilder sb, string settings, string victim, bool hold)
        {
            string on = Scene(settings, victim, hold, true, out int shotsOn, out bool ramOn, out int moved, out int chases);
            string off = Scene(settings, victim, hold, false, out int shotsOff, out _, out _, out _);
            bool ok = on == off && shotsOn >= 1 && !ramOn && chases == 0 && (!hold || moved <= 1);
            sb.AppendLine(OzV11Kit.Verdict(ok, $"Mammoth {(hold ? "on HOLD POSITION" : "idle")} vs {victim} at gun range: DV189 on = off field by field ({(on == off ? "same" : "DIFFERENT")}, {on.Length} chars), "
                + $"{shotsOn} shot(s) at it, rammed {ramOn}, chases {chases}{(hold ? $", moved {moved} px" : "")}"));
        }

        static string Scene(string settings, string victim, bool hold, bool dv189, out int shots, out bool rammed, out int moved, out int chases)
        {
            Set("BigTankRamFirstDeviation", dv189);
            var w = NewWorld(settings, 43);
            var a = Unit(w, ZBigTanks.Mammoth, 320, 384, Team.Red, 9f, 0f, true);
            var v = Unit(w, victim, 320 + a.AttackRadius - 20, 384, Team.Blue, 9f, 0f, false);
            if (hold) w.SendHold(Team.Red, a);
            int x0 = a.CenterX, y0 = a.CenterY; shots = 0; moved = 0;
            var dump = new StringBuilder();
            for (int i = 0; i < 700; i++)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events) if (Shot(e) && e.objId == a.Id && e.otherId == v.Id) shots++;
                w.Events.Clear();
                moved = Math.Max(moved, Math.Abs(a.CenterX - x0) + Math.Abs(a.CenterY - y0));
                if (i % 10 == 9) Dump(w, dump);
            }
            rammed = v.RamDoomBy >= 0; chases = w.RamFirstChases + w.RamFirstPicks;
            Set("BigTankRamFirstDeviation", true);
            return dump.ToString();
        }

        static readonly FieldInfo[] Fields = typeof(ZObject).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string)) && !f.Name.StartsWith("rf", StringComparison.Ordinal)).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();

        /// <summary>Every plain field of every unit (the DV189 fields left out) and its waypoints.</summary>
        static void Dump(ZWorld w, StringBuilder sb)
        {
            foreach (var o in w.Objects)
            {
                sb.Append(o.Id).Append('{');
                foreach (var f in Fields)
                {
                    var x = f.GetValue(o);
                    sb.Append(x is double dv ? BitConverter.DoubleToInt64Bits(dv).ToString() : x is float fv ? BitConverter.ToInt32(BitConverter.GetBytes(fv), 0).ToString() : Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                }
                foreach (var p in o.Waypoints) sb.Append((int)p.mode).Append(':').Append(p.refId).Append(':').Append(p.x).Append(':').Append(p.y).Append(';');
                sb.Append('}');
            }
            sb.Append(w.Rng.Next(1 << 30)).Append('|');
        }

        /// <summary>A jeep nearer (beside), a heavy farther (ahead), both in gun range: the heavy first, rammed; the jeep not rammed.</summary>
        static void Nearest(StringBuilder sb, string settings)
        {
            var w = NewWorld(settings, 45);
            var a = Unit(w, ZBigTanks.Mammoth, 320, 384, Team.Red, 9f, 0f, true);
            var j = Unit(w, "jeep", 320, 384 - 80, Team.Blue, 0f, -9f, false);
            var h = Unit(w, "heavy", 320 + a.AttackRadius - 15, 384, Team.Blue, 9f, 0f, false);
            int first = -1; long ramAt = -1;
            for (int i = 0; i < RamWithin + 400 && ramAt < 0; i++)
            {
                w.Tick(ZConst.TICK); w.Events.Clear();
                if (first < 0 && a.AttackObject != null) first = a.AttackObject.Id;
                if (h.RamDoomBy == a.Id) ramAt = w.TickIndex;
            }
            sb.AppendLine(OzV11Kit.Verdict(first == h.Id && ramAt > 0 && j.RamDoomBy < 0, $"Mammoth idle, a jeep nearer beside (80 px) and a heavy ahead ({a.AttackRadius - 15} px): first target {(first == h.Id ? "the heavy" : first == j.Id ? "the JEEP" : first.ToString())}, heavy rammed at tick {ramAt}, jeep rammed {j.RamDoomBy >= 0}"));
        }

        /// <summary>Two medium tanks at the same distance (mirrored): the one made first is taken (object order).</summary>
        static void Ties(StringBuilder sb, string settings)
        {
            var w = NewWorld(settings, 47);
            var a = Unit(w, ZBigTanks.Mammoth, 320, 384, Team.Red, 9f, 0f, true);
            int d = 100;
            var m1 = Unit(w, ZMTank.Type, 320 + d, 384 + 60, Team.Blue, 9f, 0f, false);
            var m2 = Unit(w, ZMTank.Type, 320 + d, 384 - 60, Team.Blue, 9f, 0f, false);
            int first = -1;
            for (int i = 0; i < 60 && first < 0; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); if (a.AttackObject != null) first = a.AttackObject.Id; }
            sb.AppendLine(OzV11Kit.Verdict(first == m1.Id && m1.Id < m2.Id, $"Mammoth idle, two medium tanks at the same distance: the first made is taken (picked id {first}, ids {m1.Id} / {m2.Id})"));
        }

        /// <summary>A plain MOVE order (no attack-move) away from a medium tank in gun range: obeyed, no chase on the way.</summary>
        static void PlainMove(StringBuilder sb, string settings)
        {
            var w = NewWorld(settings, 49);
            var a = Unit(w, ZBigTanks.Mammoth, 320, 384, Team.Red, 9f, 0f, true);
            var v = Unit(w, ZMTank.Type, 320 + a.AttackRadius - 20, 384, Team.Blue, 9f, 0f, false);
            int gx = 320 - 120, gy = 384;
            OzV11Kit.PlayerMove(w, a, gx, gy, false);
            long arrive = -1; int chases = 0;
            for (int i = 0; i < 3000 && arrive < 0; i++)
            {
                w.Tick(ZConst.TICK); w.Events.Clear();
                chases = w.RamFirstChases + w.RamFirstPicks;
                if (Math.Abs(a.CenterX - gx) + Math.Abs(a.CenterY - gy) <= 4) arrive = w.TickIndex;
            }
            sb.AppendLine(OzV11Kit.Verdict(arrive > 0 && chases == 0 && v.RamDoomBy < 0, $"Mammoth on a plain MOVE order away from a medium tank in gun range: arrived at tick {arrive}, chases on the way {chases}, rammed {v.RamDoomBy >= 0}"));
        }

        /// <summary>A medium tank in gun range with an own (unarmed) medium tank parked right in the way: no hang: it rams or gives up (the
        /// fallback: it stands and shoots for the cool-down), its gun on the target throughout.</summary>
        static void Blocked(StringBuilder sb, string settings)
        {
            var w = NewWorld(settings, 53);
            var a = Unit(w, ZBigTanks.Mammoth, 320, 384, Team.Red, 9f, 0f, true);
            var v = Unit(w, ZMTank.Type, 320 + a.AttackRadius - 20, 384, Team.Blue, 9f, 0f, false);
            Unit(w, ZMTank.Type, 320 + 62, 384, Team.Red, 9f, 0f, false);
            int shots = 0; long ramAt = -1;
            for (int i = 0; i < 1500; i++)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events) if (Shot(e) && e.objId == a.Id && e.otherId == v.Id) shots++;
                w.Events.Clear();
                if (ramAt < 0 && v.RamDoomBy >= 0) ramAt = w.TickIndex;
            }
            sb.AppendLine(OzV11Kit.Verdict(shots >= 1 && (ramAt > 0 || w.RamFirstGiveUps > 0), $"Mammoth vs a medium tank, its own tank parked in the way: rammed at {ramAt} or gave up: give-ups {w.RamFirstGiveUps} (held {w.RamFirstEndsHeld}, blocked {w.RamFirstEndsBlocked}, time {w.RamFirstEndsTime}, way {w.RamFirstEndsWay}), {shots} shot(s) at it"));
        }

        /// <summary>DV189 off: the drive-in scenes: no chase, no ram from gun range, every DV189 field of every unit at its start value on every tick.</summary>
        static void Off(StringBuilder sb, string settings)
        {
            foreach (var (tank, victim, order) in new[] { (ZBigTanks.Mammoth, ZMTank.Type, false), (ZBigTanks.Mammoth, "heavy", true), (ZBigTanks.Bizon, ZMTank.Type, false), (ZBigTanks.Bizon, "gatling", false) })
            {
                var w = NewWorld(settings, 41);
                var a = Unit(w, tank, 320, 384, Team.Red, 9f, 0f, true);
                var v = Unit(w, victim, 320 + a.AttackRadius - 20, 384, Team.Blue, 9f, 0f, false);
                if (order) OzV11Kit.PlayerAttack(w, a, v);
                bool start = true;
                for (int i = 0; i < 900; i++)
                {
                    w.Tick(ZConst.TICK); w.Events.Clear();
                    foreach (var o in w.Objects) if (!o.RamFirstFieldsAtStart) start = false;
                }
                int c = w.RamFirstPicks + w.RamFirstPrefers + w.RamFirstChases + w.RamFirstDriveTicks + w.RamFirstGiveUps;
                sb.AppendLine(OzV11Kit.Verdict(start && c == 0 && v.RamDoomBy < 0, $"DV189 off: {tank} {(order ? "ATTACK order" : "idle")} vs {victim} at gun range: DV189 fields at start values every tick {start}, counters {c}, rammed {v.RamDoomBy >= 0} (stands and fires: Zod's / DV9's way)"));
            }
        }

        static string Print(string settings, int seed)
        {
            var w = NewWorld(settings, seed);
            var a = Unit(w, ZBigTanks.Mammoth, 320, 384, Team.Red, 9f, 0f, true);
            var b = Unit(w, ZBigTanks.Bizon, 320, 384 + 160, Team.Red, 9f, 0f, true);
            Unit(w, "heavy", 320 + 130, 384, Team.Blue, 9f, 0f, true);
            Unit(w, "jeep", 320 + 60, 384 - 70, Team.Blue, 0f, -9f, true);
            Unit(w, "gatling", 320 + 100, 384 + 160, Team.Blue, -9f, 0f, true);
            OzV11Kit.Place(w, "grunt", 320 + 110, 384 + 60, Team.Blue, 3);
            var s = new StringBuilder();
            for (int i = 0; i < 1500; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); if (i % 50 == 49) Dump(w, s); }
            return s.ToString();
        }
    }
}
