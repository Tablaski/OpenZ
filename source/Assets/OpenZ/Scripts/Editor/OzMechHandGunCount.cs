using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Counted check of DV146, the mech's hand gun (Sim/ZObject.MechHandGun.cs). Headless simulation on plain ground (no scene, no Play
    /// mode, nothing saved; only the public members of the simulation, so the off-engine harness compiles it as it is):
    ///  1 the numbers: a blue mech that cannot move or die (its rockets without splash) against a red grunt squad that cannot die, 100 px
    ///    away: the shots' damage, hit share, cycle, range against the Psycho's row; every shot inside a rocket reload window;
    ///  2 robots only: the mech's rocket target a jeep with a light tank, a gun and a red robot factory near it: 0 hand-gun shots;
    ///  3 the arc: the rocket target a jeep, one red grunt squad at a time at 0 / +-10 / +20 / +30 / +45 / -20 / -30 / +90 / 180 deg from
    ///    the gun's line to it: shot only inside -15 .. +35 deg (outward +), every shot's own angle measured again here;
    ///  4 range: a squad 128 px from the mech (inside its rocket range, outside the Psycho's 120): never shot by the hand gun;
    ///  5 the rule off ('Zod exact'): no hand-gun shot; 6 the computer's mech with no order (passive engage): it fires by itself;
    ///  7 the same seed twice: the same shots (deterministic);
    ///  8 balance (a report, no verdict): one mech against 2 grunt squads / 2 psycho squads / 1 of each, everybody real, rule off vs on.
    /// </summary>
    public static class OzMechHandGunCount
    {
        const int Big = 1000000000;
        const int FX = 700, FY = 700;
        static string Map(bool factory = false) => factory ? OzV11Kit.Plain(96, 96, ("building:robot_factory", 46, 47, "red")) : OzV11Kit.Plain(96, 96);

        static ZWorld World(string settings, int seed, bool factory = false)
        {
            var w = OzV11Kit.World(settings, Map(factory), seed);
            var d = w.CreateRobotGroup("grunt", 40, 1480, Team.Red, 1);
            d.MoveSpeed = 0; d.Damage = 0; d.MaxHealth = d.Health = Big;
            OzV11Kit.Dummy(w, 80, 1480);
            return w;
        }

        static ZObject Mech(ZWorld w, int cx, int cy, bool solid = true)
        {
            var m = OzV11Kit.Place(w, "medium", cx, cy, Team.Blue);
            if (solid) { m.Drivers[0] = Big; m.MaxHealth = m.Health = Big; m.MoveSpeed = 0; m.RealMoveSpeed = 0; m.DamageRadius = 0; }
            m.ResetDamageInfo();
            return m;
        }

        static ZObject Still(ZWorld w, string type, int cx, int cy, Team team = Team.Red)
        {
            var o = OzV11Kit.Place(w, type, cx, cy, team);
            foreach (var m in OzV11Kit.Squad(o)) { m.MaxHealth = m.Health = Big; m.Damage = 0; m.MoveSpeed = 0; m.RealMoveSpeed = 0; if (m.Drivers.Count > 0) m.Drivers[0] = Big; }
            return o;
        }

        sealed class Take
        {
            public readonly List<double> rockets = new List<double>();
            public readonly List<(double t, int target, bool hit, int hp, bool killed)> shots = new List<(double, int, bool, int, bool)>();
            public long hash = 17;
        }

        /// <summary>Ticks the world for `seconds`, collects the mech's rocket shots and hand-gun shots (and a hash of the shots).</summary>
        static Take Tick(ZWorld w, ZObject mech, double seconds, Action<ZWorld> perTick = null)
        {
            var r = new Take();
            while (w.Time < seconds)
            {
                w.Tick(ZConst.TICK);
                perTick?.Invoke(w);
                foreach (var e in w.Events)
                {
                    if (e.objId != mech.Id) continue;
                    if (e.type == ZEventType.MissileFired) r.rockets.Add(w.Time);
                    if (e.type != ZEventType.MechHandGun) continue;
                    r.shots.Add((w.Time, e.otherId, e.flag, (int)e.value, e.flag2));
                    unchecked { r.hash = r.hash * 31 + (long)(w.Time * 1000) * 7 + e.otherId * 3 + (e.flag ? 1 : 0); }
                }
                w.Events.Clear();
            }
            return r;
        }

        /// <summary>The shots outside every reload window (rocket at r: r + after .. r + cycle - before; the ticks' 10 ms slack).</summary>
        static int OutsideWindows(Take r, double cycle)
        {
            int bad = 0;
            foreach (var s in r.shots)
            {
                double last = r.rockets.Where(x => x <= s.t + 1e-9).DefaultIfEmpty(-100).Max();
                if (last < 0 || s.t < last + ZWorld.HandGunAfterRocket - 0.011 || s.t > last + cycle - ZWorld.HandGunBeforeRocket + 0.011) bad++;
            }
            return bad;
        }

        /// <summary>The robot's angle from the gun's line to the look point, measured here on its own (the gun HandGunSidePx to the right).</summary>
        static double Angle(ZObject mech, int lx, int ly, ZObject o)
        {
            double fx = lx - mech.CenterX, fy = ly - mech.CenterY, l = Math.Sqrt(fx * fx + fy * fy); fx /= l; fy /= l;
            double gx = mech.CenterX + (-fy) * ZWorld.HandGunSidePx, gy = mech.CenterY + fx * ZWorld.HandGunSidePx;
            double a1 = Math.Atan2(ly - gy, lx - gx), a2 = Math.Atan2(o.CenterY - gy, o.CenterX - gx);
            double d = (a2 - a1) * 180.0 / Math.PI; while (d > 180) d -= 360; while (d < -180) d += 360;
            return d;
        }

        public static string Run(string settings, int runs = 10)
        {
            var sb = new StringBuilder();
            var st = ZSettings.Parse(settings);
            var ps = st.Unit(ZWorld.HandGunType); var ms = st.Unit("medium");
            bool was = ZWorld.MechHandGunDeviation, wasDrv = ZWorld.DriverFireDeviation;
            try
            {
                ZWorld.DriverFireDeviation = false;                                                  // parts 1-7: only the hand gun's own shots (the hatch driver: part 8)
                ZWorld.MechHandGunDeviation = true;
                sb.AppendLine($"The Psycho's row: damage {ps.Damage} HP, hit chance {ps.attack_damage_chance:0.###}, cycle {ps.attack_speed:0.###} s, range {ps.attack_radius} px. " +
                              $"The mech: rockets every {ms.attack_speed:0.###} s, range {ms.attack_radius} px. Window: {ZWorld.HandGunAfterRocket:0.##} s after a rocket to {ZWorld.HandGunBeforeRocket:0.##} s before the next; arc +{ZWorld.HandGunOutDeg:0} / -{ZWorld.HandGunInDeg:0} deg.");

                // ---------------------------------------------------------------- 1 numbers
                int shots = 0, hits = 0, badDmg = 0, outside = 0, far = 0, rocketsN = 0; double minGap = 9, gapSum = 0; int gaps = 0; long hitHp = 0;
                for (int k = 0; k < runs; k++)
                {
                    var w = World(settings, 100 + k);
                    var m = Mech(w, FX, FY);
                    var sq = Still(w, "grunt", FX + 100, FY);
                    OzV11Kit.PlayerAttack(w, m, sq);
                    var r = Tick(w, m, 20);
                    rocketsN += r.rockets.Count; shots += r.shots.Count; outside += OutsideWindows(r, m.AttackSpeed);
                    for (int i = 0; i < r.shots.Count; i++)
                    {
                        var s = r.shots[i];
                        if (s.hit) { hits++; hitHp += s.hp; if (s.hp != ps.Damage) badDmg++; }
                        var o = w.GetAny(s.target);
                        if (o != null && OzV11Kit.Dist(m, o) > ps.attack_radius) far++;
                        if (i > 0 && r.rockets.All(x => x <= r.shots[i - 1].t || x > s.t)) { double gp = s.t - r.shots[i - 1].t; minGap = Math.Min(minGap, gp); gapSum += gp; gaps++; }
                    }
                }
                double share = shots > 0 ? hits / (double)shots : 0, meanGap = gaps > 0 ? gapSum / gaps : 0;
                sb.AppendLine($"Part 1: {runs} fights of 20 s: {rocketsN} rocket shots, {shots} hand-gun shots ({shots / (double)Math.Max(1, rocketsN):0.0} per reload), {hits} hits ({share * 100:0.0} %), " +
                              $"HP per hit {(hits > 0 ? hitHp / (double)hits : 0):0.0}, seconds between shots in a window: min {minGap:0.000}, mean {meanGap:0.000}; outside a window {outside}; beyond {ps.attack_radius} px {far}");
                sb.AppendLine(OzV11Kit.Verdict(shots > 0 && badDmg == 0 && hits > 0, $"hand-gun damage = the Psycho's {ps.Damage} HP on a grunt: {hits} hits, {badDmg} with another value"));
                sb.AppendLine(OzV11Kit.Verdict(shots > 0 && Math.Abs(share - ps.attack_damage_chance) < 0.08, $"hit share {share * 100:0.0} % (the Psycho's {ps.attack_damage_chance * 100:0} %, Zod's roll)"));
                sb.AppendLine(OzV11Kit.Verdict(gaps > 0 && minGap >= ps.attack_speed - 1e-6 && minGap <= ps.attack_speed + 0.011, $"cycle: shots {minGap:0.000} s apart at least (the Psycho's {ps.attack_speed:0.###} s, the tick 0.01 s)"));
                sb.AppendLine(OzV11Kit.Verdict(shots > 0 && outside == 0, $"only while the rockets reload: {outside} of {shots} shots outside a window"));
                sb.AppendLine(OzV11Kit.Verdict(far == 0, $"range: {far} shots at a robot beyond the Psycho's {ps.attack_radius} px"));

                // ---------------------------------------------------------------- 2 robots only
                int nonRobot = 0, rk2 = 0;
                for (int k = 0; k < runs; k++)
                {
                    var w = World(settings, 200 + k, true);
                    var m = Mech(w, FX, FY);
                    var jeep = Still(w, "jeep", FX + 90, FY);
                    Still(w, "light", FX + 60, FY + 30); Still(w, "gun", FX + 70, FY - 40);
                    OzV11Kit.PlayerAttack(w, m, jeep);
                    var r = Tick(w, m, 15);
                    rk2 += r.rockets.Count; nonRobot += r.shots.Count;
                }
                sb.AppendLine(OzV11Kit.Verdict(rk2 > 0 && nonRobot == 0, $"robots only: a jeep as the rocket target, a light tank, a gun and a red robot factory in range, no robot: {rk2} rocket shots, {nonRobot} hand-gun shots"));

                // ---------------------------------------------------------------- 3 arc
                var arcRows = new List<string>(); int arcBad = 0, arcMiss = 0;
                foreach (double deg in new[] { 0.0, 10, -10, 20, 30, 45, -20, -30, 90, 180 })
                {
                    int n = 0;
                    for (int k = 0; k < Math.Max(2, runs / 3); k++)
                    {
                        var w = World(settings, 300 + k);
                        var m = Mech(w, FX, FY);
                        var jeep = Still(w, "jeep", FX + 110, FY);
                        double gx = FX, gy = FY + ZWorld.HandGunSidePx, ax = jeep.CenterX - gx, ay = jeep.CenterY - gy, al = Math.Sqrt(ax * ax + ay * ay);
                        double c = Math.Cos(deg * Math.PI / 180), s = Math.Sin(deg * Math.PI / 180), dx = (ax * c - ay * s) / al, dy = (ax * s + ay * c) / al;
                        var sq = OzV11Kit.Place(w, "grunt", (int)(gx + dx * 70), (int)(gy + dy * 70), Team.Red, 1);
                        foreach (var q in OzV11Kit.Squad(sq)) { q.MaxHealth = q.Health = Big; q.Damage = 0; q.MoveSpeed = 0; q.RealMoveSpeed = 0; }
                        OzV11Kit.PlayerAttack(w, m, jeep);
                        var r = Tick(w, m, 12);
                        foreach (var sh in r.shots)
                        {
                            n++;
                            var o = w.GetAny(sh.target);
                            double a = o != null ? Angle(m, jeep.CenterX, jeep.CenterY, o) : 999;
                            if (a > ZWorld.HandGunOutDeg + 1e-6 || a < -ZWorld.HandGunInDeg - 1e-6) arcBad++;
                        }
                    }
                    bool inside = deg <= ZWorld.HandGunOutDeg && deg >= -ZWorld.HandGunInDeg;
                    if (inside && n == 0) arcMiss++;
                    arcRows.Add($"{deg:+0;-0;0} deg: {n} shots ({(inside ? "inside" : "outside")})");
                }
                sb.AppendLine("Part 3, one grunt 70 px from the gun at an angle from its line to the rocket target (+ = outward): " + string.Join("; ", arcRows));
                sb.AppendLine(OzV11Kit.Verdict(arcBad == 0, $"the arc: {arcBad} shots at a robot outside -{ZWorld.HandGunInDeg:0} .. +{ZWorld.HandGunOutDeg:0} deg (each shot's angle measured again)"));
                sb.AppendLine(OzV11Kit.Verdict(arcMiss == 0, $"inside the arc it fires: {arcMiss} inside angles without a shot"));

                // ---------------------------------------------------------------- 4 range
                int rangeShots = 0, rk4 = 0;
                for (int k = 0; k < runs; k++)
                {
                    var w = World(settings, 400 + k);
                    var m = Mech(w, FX, FY);
                    var sq = OzV11Kit.Place(w, "grunt", FX + 126, FY, Team.Red, 1);
                    foreach (var q in OzV11Kit.Squad(sq)) { q.MaxHealth = q.Health = Big; q.Damage = 0; q.MoveSpeed = 0; q.RealMoveSpeed = 0; }
                    OzV11Kit.PlayerAttack(w, m, sq);
                    var r = Tick(w, m, 12);
                    rk4 += r.rockets.Count; rangeShots += r.shots.Count;
                }
                sb.AppendLine(OzV11Kit.Verdict(rk4 > 0 && rangeShots == 0, $"range: a grunt about 126 px away (in the rockets' {ms.attack_radius} px): {rk4} rocket shots, {rangeShots} hand-gun shots"));

                // ---------------------------------------------------------------- 5 rule off
                ZWorld.MechHandGunDeviation = false;
                int offShots = 0, rk5 = 0;
                for (int k = 0; k < runs; k++)
                {
                    var w = World(settings, 100 + k);
                    var m = Mech(w, FX, FY);
                    var sq = Still(w, "grunt", FX + 100, FY);
                    OzV11Kit.PlayerAttack(w, m, sq);
                    var r = Tick(w, m, 20);
                    rk5 += r.rockets.Count; offShots += r.shots.Count;
                }
                ZWorld.MechHandGunDeviation = true;
                sb.AppendLine(OzV11Kit.Verdict(rk5 > 0 && offShots == 0, $"the rule off ('Zod exact'): {rk5} rocket shots, {offShots} hand-gun shots"));

                // ---------------------------------------------------------------- 6 the computer's mech, no order
                int cpuShots = 0;
                for (int k = 0; k < runs; k++)
                {
                    var w = World(settings, 600 + k);
                    var m = Mech(w, FX, FY);                                                         // blue = the computer's team (OzV11Kit.World), no order given
                    Still(w, "grunt", FX + 90, FY);
                    var r = Tick(w, m, 15);
                    cpuShots += r.shots.Count;
                }
                sb.AppendLine(OzV11Kit.Verdict(cpuShots > 0, $"the computer's mech without an order (it engages by itself): {cpuShots} hand-gun shots in {runs} fights"));

                // ---------------------------------------------------------------- 7 deterministic
                long h1, h2;
                { var w = World(settings, 777); var m = Mech(w, FX, FY); var sq = Still(w, "grunt", FX + 100, FY); OzV11Kit.PlayerAttack(w, m, sq); h1 = Tick(w, m, 15).hash; }
                { var w = World(settings, 777); var m = Mech(w, FX, FY); var sq = Still(w, "grunt", FX + 100, FY); OzV11Kit.PlayerAttack(w, m, sq); h2 = Tick(w, m, 15).hash; }
                sb.AppendLine(OzV11Kit.Verdict(h1 == h2, $"deterministic: the same seed twice, the same shots ({h1:X} / {h2:X})"));

                OzMechHandGunAimCount.Parts(sb, settings, runs);                                     // part 9: the gun turns first (Editor/OzMechHandGunAimCount.cs)
                // ---------------------------------------------------------------- 8 balance (report)
                ZWorld.DriverFireDeviation = true;                                                   // as in a game: the mech's hatch driver fires too (DV87)
                sb.AppendLine("Part 8, balance (no verdict): one blue mech (real health, its grunt driver) against red robot squads that attack it from 110 px, everybody real; " + runs * 2 + " fights each, 60 s at most.");
                sb.AppendLine("enemies | rule | mech wins | mech lost | undecided | mean seconds to the end | mech health left (wins) | robots killed by the hand gun | hand-gun shots per fight");
                foreach (var foe in new[] { new[] { "grunt", "grunt" }, new[] { "psycho", "psycho" }, new[] { "grunt", "psycho" }, new[] { "grunt", "grunt", "psycho" } })
                    foreach (bool rule in new[] { false, true })
                    {
                        ZWorld.MechHandGunDeviation = rule;
                        int win = 0, lost = 0, open = 0, hgKills = 0, hgShots = 0; double tSum = 0, hpSum = 0; int ended = 0;
                        for (int k = 0; k < runs * 2; k++)
                        {
                            var w = World(settings, 800 + k);
                            var m = Mech(w, FX, FY, false);
                            var squads = new List<ZObject>();
                            for (int j = 0; j < foe.Length; j++) squads.Add(OzV11Kit.Place(w, foe[j], FX + 110, FY - 40 + j * 40, Team.Red));
                            foreach (var sq in squads) OzV11Kit.PlayerAttack(w, sq, m);
                            double endAt = -1;
                            var r = Tick(w, m, 60, ww =>
                            {
                                if (endAt >= 0) return;
                                bool foesDead = squads.All(sq => OzV11Kit.Squad(sq).All(x => !OzV11Kit.Alive(x)));
                                if (!OzV11Kit.Alive(m) || m.Owner != Team.Blue || foesDead) endAt = ww.Time;
                            });
                            hgShots += r.shots.Count; hgKills += r.shots.Count(s => s.killed);
                            bool mechUp = OzV11Kit.Alive(m) && m.Owner == Team.Blue, foesUp = squads.Any(sq => OzV11Kit.Squad(sq).Any(OzV11Kit.Alive));
                            if (mechUp && !foesUp) { win++; hpSum += m.Health / (double)m.MaxHealth; } else if (!mechUp) lost++; else open++;
                            if (endAt >= 0) { tSum += endAt; ended++; }
                        }
                        sb.AppendLine($"{string.Join(" + ", foe)} | {(rule ? "ON" : "off")} | {win} | {lost} | {open} | {(ended > 0 ? tSum / ended : 0):0.0} | {(win > 0 ? hpSum / win * 100 : 0):0} % | {hgKills} | {hgShots / (double)(runs * 2):0.0}");
                    }
            }
            finally { ZWorld.MechHandGunDeviation = was; ZWorld.DriverFireDeviation = wasDrv; }
            return sb.ToString();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("OpenZ/Mech v2/16 Hand gun: counted rules check (headless, DV146)")]
        static void Menu() => OzV11Kit.RunMenu("DV146 mech hand gun", () => Run(OzV11Kit.SettingsText));
#endif
    }
}
