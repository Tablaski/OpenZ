using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Counted check of DV149, the mech loses its hand gun (Sim/ZObject.MechGunLoss.cs). Headless simulation on plain ground (no scene, no
    /// Play mode, nothing saved; only the public members of the simulation, so the off-engine harness compiles it as it is):
    ///  1 the rule, blast by blast: blue mechs that cannot die (10 000 000 HP, so the 20 % line is exact and no hit kills) shelled by red
    ///    light tanks whose shell is the game's (2083 HP) or 1200 / 700 / 520 / 300 HP; before each shell lands the mech's health is set to
    ///    5 .. 100 % of full; every blast: a draw exactly when the blast took the heavy share or more and left the mech under 20 % (and the
    ///    gun still there); the share of draws that lost the gun (~50 %); no draw after the loss;
    ///  2 small arms (grunt, psycho, sniper, pyro with burning ground, laser, electrobot zaps) on a mech held at 15 %: hits, never a draw
    ///    (a grenade a grunt throws is a blast and is counted apart);
    ///  3 the explosive weapons (light tank, heavy tank, gun, missile launcher, an enemy mech, a Tough squad) on a mech held at 15 %: draws;
    ///  4 the gate: a mech that lost its gun never fires it (ordered, and the computer's mech by itself), its rockets still fire; a mech
    ///    with its gun fires it in the same fight;
    ///  5 repair on the station's pad (DV62): full health, the gun still lost, no shot after; Zod's repair swap (DV62 off): the fresh
    ///    vehicle has lost it too;
    ///  6 capture: the driver sniped, a red grunt takes the empty mech: still no gun;
    ///  7 the switch off (and DV146 off, 'Zod exact'): no draw; with no qualifying hit the world is the same with the rule on and off (no
    ///    extra draw on the rand() stream); the same seed twice gives the same draws and losses;
    ///  8 balance (a report, no verdict): real mechs (6418 HP) fighting real light tanks / Tough squads: how often the gun is lost first.
    /// </summary>
    public static class OzMechGunLossCount
    {
        const int Big = 1000000000, Huge = 10000000;
        const int FX = 700, FY = 700;
        static readonly double[] Fractions = { 0.05, 0.12, 0.18, 0.199, 0.21, 0.25, 0.30, 0.50, 0.80, 1.0 };
        static readonly int[] ShellHp = { 0, 1200, 700, 520, 300 };                                  // 0 = the light tank's own shell

        static ZWorld World(string settings, int seed, string map = null)
        {
            var w = OzV11Kit.World(settings, map ?? OzV11Kit.Plain(96, 96), seed);
            var d = w.CreateRobotGroup("grunt", 40, 1480, Team.Red, 1);
            d.MoveSpeed = 0; d.Damage = 0; d.MaxHealth = d.Health = Big;
            OzV11Kit.Dummy(w, 80, 1480);
            return w;
        }

        /// <summary>A blue mech: its driver cannot be sniped, `hp` full health, standing still unless `walks`; its rockets without splash.</summary>
        static ZObject Mech(ZWorld w, int cx, int cy, int hp = Huge, bool walks = false, Team team = Team.Blue)
        {
            var m = OzV11Kit.Place(w, "medium", cx, cy, team);
            m.Drivers[0] = Big; m.MaxHealth = hp; m.SetHealth(hp); m.DamageRadius = 0;
            if (!walks) { m.MoveSpeed = 0; m.RealMoveSpeed = 0; }
            m.ResetDamageInfo();
            return m;
        }

        /// <summary>A red unit that cannot die or move; `dmg` > 0 replaces its weapon's HP.</summary>
        static ZObject Shooter(ZWorld w, string type, int cx, int cy, int dmg = 0, Team team = Team.Red)
        {
            var o = OzV11Kit.Place(w, type, cx, cy, team);
            foreach (var u in OzV11Kit.Squad(o)) { u.MaxHealth = u.Health = Big; u.MoveSpeed = 0; u.RealMoveSpeed = 0; if (u.Drivers.Count > 0) u.Drivers[0] = Big; if (dmg > 0) u.Damage = dmg; }
            return o;
        }

        static ZObject Still(ZWorld w, string type, int cx, int cy, Team team)
        {
            var o = OzV11Kit.Place(w, type, cx, cy, team);
            foreach (var u in OzV11Kit.Squad(o)) { u.MaxHealth = u.Health = Big; u.Damage = 0; u.MoveSpeed = 0; u.RealMoveSpeed = 0; if (u.Drivers.Count > 0) u.Drivers[0] = Big; }
            return o;
        }

        static void Run(ZWorld w, double until, Action perTickBefore, Action<ZEvent> perEvent)
        {
            while (w.Time < until && !w.GameOver)
            {
                perTickBefore?.Invoke();
                w.Tick(ZConst.TICK);
                if (perEvent != null) foreach (var e in w.Events) perEvent(e);
                w.Events.Clear();
            }
        }

        static void Set(ZObject m, double share) => m.SetHealth(Math.Max(1, (int)(m.MaxHealth * share)));

        /// <summary>Knocks the mech's gun off for real: a red light tank 100 px south shells it, the mech set to 15 % before each shell lands.
        /// The tank is removed after. False = no loss in 120 s.</summary>
        static bool LoseGun(ZWorld w, ZObject m)
        {
            var t = Shooter(w, "light", m.CenterX, m.CenterY + 100);
            OzV11Kit.PlayerAttack(w, t, m);
            double end = w.Time + 120;
            while (!m.HandGunLost && w.Time < end && !w.GameOver)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events) if (e.type == ZEventType.MissileFired && e.objId == t.Id) Set(m, 0.15);
                w.Events.Clear();
            }
            t.Waypoints.Clear(); t.Damage = 0; t.SetHealth(0);
            Run(w, w.Time + 0.5, null, null);
            return m.HandGunLost;
        }

        /// <summary>The gun's line test as DV146's check: a red jeep as the rocket target 110 px east, a red grunt 70 px from the gun on that
        /// line; `order` = the mech attacks the jeep, else no order (the computer's passive engage, a grunt 90 px east). Returns (hand-gun
        /// shots, rocket shots) in 12 s.</summary>
        static (int shots, int rockets) GateFight(ZWorld w, ZObject m, bool order)
        {
            Team foe = m.Owner == Team.Red ? Team.Blue : Team.Red;
            int shots = 0, rockets = 0;
            if (order)
            {
                var jeep = Still(w, "jeep", m.CenterX + 110, m.CenterY, foe);
                Still(w, "grunt", m.CenterX + 70, m.CenterY + (int)ZWorld.HandGunSidePx, foe);
                OzV11Kit.PlayerAttack(w, m, jeep);
            }
            else Still(w, "grunt", m.CenterX + 90, m.CenterY, foe);
            Run(w, w.Time + 12, null, e =>
            {
                if (e.objId != m.Id) return;
                if (e.type == ZEventType.MechHandGun) shots++;
                if (e.type == ZEventType.MissileFired) rockets++;
            });
            return (shots, rockets);
        }

        sealed class Pair { public ZObject mech, tank; public int k, hpPrev, rollsPrev, shellHp; public bool lostPrev; }
        sealed class Tally
        {
            public int blasts, heavyUnder, rolls, wrongDraw, missedDraw, lost, rollsAfterLoss, mechs, mechsLost;
            public int heavyOver, lightUnder, lightOver;
            public long hash = 17;
        }

        /// <summary>Part 1's world: 10 mechs, each shelled by its own tank (two of each shell), health set before each shell lands.</summary>
        static Tally RuleWorld(string settings, int seed, double secs, double[] fractions, Tally t)
        {
            var w = World(settings, seed);
            int heavy = w.GunLossHeavyHp;
            var pairs = new List<Pair>();
            for (int col = 0; col < 2; col++)
                for (int i = 0; i < ShellHp.Length; i++)
                {
                    int x = 380 + col * 560, y = 180 + i * 260;
                    var m = Mech(w, x, y);
                    var tank = Shooter(w, "light", x + 100, y, ShellHp[i]);
                    OzV11Kit.PlayerAttack(w, tank, m);
                    pairs.Add(new Pair { mech = m, tank = tank, k = (seed * 7 + col * 3 + i) % fractions.Length, shellHp = ShellHp[i] });
                }
            t.mechs += pairs.Count;
            var byTank = pairs.ToDictionary(p => p.tank.Id);
            Run(w, secs, () =>
            {
                foreach (var p in pairs) { p.hpPrev = p.mech.Health; p.rollsPrev = p.mech.GunLossRolls; p.lostPrev = p.mech.HandGunLost; }
            }, e =>
            {
                if (!byTank.TryGetValue(e.objId, out var p)) return;
                var m = p.mech;
                if (e.type == ZEventType.MissileFired) { Set(m, fractions[p.k++ % fractions.Length]); return; }
                if (e.type != ZEventType.MissileExploded) return;
                int took = p.hpPrev - m.Health, drew = m.GunLossRolls - p.rollsPrev;
                bool under = m.Health < m.MaxHealth * ZWorld.GunLossHpShare, isHeavy = took >= heavy;
                bool want = ZWorld.MechGunLossDeviation && ZWorld.MechHandGunDeviation && !p.lostPrev && isHeavy && under && !m.Destroyed;
                t.blasts++;
                if (isHeavy && under) t.heavyUnder++; else if (isHeavy) t.heavyOver++; else if (under) t.lightUnder++; else t.lightOver++;
                t.rolls += drew;
                if (p.lostPrev) t.rollsAfterLoss += drew;
                if (drew > 0 && !want) t.wrongDraw++;
                if (drew == 0 && want) t.missedDraw++;
                if (m.HandGunLost && !p.lostPrev) { t.lost++; t.mechsLost++; }
                unchecked { t.hash = t.hash * 31 + (long)(w.Time * 1000) * 7 + m.Id * 13 + took * 3 + drew * 5 + (m.HandGunLost ? 1 : 0); }
            });
            return t;
        }

        /// <summary>A hash of every event (rule on vs off with no qualifying hit: the same world).</summary>
        static long EventHash(string settings, int seed, double secs)
        {
            var w = World(settings, seed);
            var pairs = new List<Pair>();
            for (int i = 0; i < ShellHp.Length; i++)
            {
                var m = Mech(w, 380, 180 + i * 260); var tank = Shooter(w, "light", 480, 180 + i * 260, ShellHp[i]);
                OzV11Kit.PlayerAttack(w, tank, m); pairs.Add(new Pair { mech = m, tank = tank });
            }
            var byTank = pairs.ToDictionary(p => p.tank.Id);
            long h = 17; int k = 0;
            double[] high = { 0.30, 0.45, 0.60, 0.80, 1.0 };
            Run(w, secs, null, e =>
            {
                if (e.type == ZEventType.MissileFired && byTank.TryGetValue(e.objId, out var p)) Set(p.mech, high[k++ % high.Length]);
                unchecked { h = h * 31 + (int)e.type * 7 + e.objId * 3 + e.x * 5 + e.y * 11 + (long)(e.value * 10); }
            });
            foreach (var p in pairs) unchecked { h = h * 31 + p.mech.Health; }
            return h;
        }

        public static string Run(string settings, int runs = 10)
        {
            var sb = new StringBuilder();
            bool was = ZWorld.MechGunLossDeviation, wasHg = ZWorld.MechHandGunDeviation, wasDrv = ZWorld.DriverFireDeviation, wasPad = ZWorld.RepairInPlaceDeviation,
                 wasFire = ZWorld.GroundFireDeviation, wasHp = ZWorld.MechHpDeviation;
            try
            {
                ZWorld.MechHandGunDeviation = true; ZWorld.MechGunLossDeviation = true; ZWorld.DriverFireDeviation = false;
                var st = ZSettings.Parse(settings); var light = st.Unit(ZWorld.GunLossHeavyRef);
                int heavy; { var w0 = World(settings, 1); heavy = w0.GunLossHeavyHp; }
                sb.AppendLine($"DV149: a blast that takes {heavy} HP or more (the {ZWorld.GunLossHeavyRef} tank's shell {light?.Damage} HP x {ZWorld.GunLossHeavyShare:0.##}) and leaves the mech alive under " +
                              $"{ZWorld.GunLossHpShare * 100:0} % of its health: one draw, {ZWorld.GunLossChance * 100:0} % to lose the gun.");
                sb.AppendLine(OzV11Kit.Verdict(light != null && heavy == (int)Math.Round(light.Damage * ZWorld.GunLossHeavyShare) && heavy > 0, $"the heavy hit's HP from the rules: {heavy}"));

                // ---------------------------------------------------------------- 1 the rule, blast by blast
                var t = new Tally();
                int worlds = Math.Max(4, runs * 4);
                for (int k = 0; k < worlds; k++) RuleWorld(settings, 1000 + k, 40, Fractions, t);
                double share = t.rolls > 0 ? t.lost / (double)t.rolls : 0, tol = Math.Max(0.07, 2.6 * Math.Sqrt(0.25 / Math.Max(1, t.rolls)));
                sb.AppendLine($"Part 1: {worlds} worlds of 40 s, {t.mechs} mechs shelled ({string.Join(" / ", ShellHp.Select(h => h == 0 ? (light?.Damage ?? 0) + " (the game's)" : h.ToString()))} HP shells, two mechs each), " +
                              $"health before each shell {string.Join(" / ", Fractions.Select(f => (f * 100).ToString("0.#")))} %: {t.blasts} blasts on a mech: heavy + under 20 % after {t.heavyUnder}, " +
                              $"heavy + 20 % or more {t.heavyOver}, light + under {t.lightUnder}, light + 20 % or more {t.lightOver}; draws {t.rolls}, guns lost {t.lost} ({share * 100:0.0} %), mechs that lost it {t.mechsLost} of {t.mechs}");
                sb.AppendLine(OzV11Kit.Verdict(t.rolls > 0 && t.wrongDraw == 0, $"a draw only on a heavy blast that leaves the mech under 20 % with its gun: {t.wrongDraw} draws elsewhere (of {t.rolls}; {t.heavyOver} heavy blasts above the line, {t.lightUnder + t.lightOver} light blasts)"));
                sb.AppendLine(OzV11Kit.Verdict(t.heavyUnder > 0 && t.missedDraw == 0, $"every such blast draws: {t.missedDraw} without a draw"));
                sb.AppendLine(OzV11Kit.Verdict(t.rolls >= 40 && Math.Abs(share - ZWorld.GunLossChance) <= tol, $"the chance: {t.lost} of {t.rolls} draws lost the gun = {share * 100:0.0} % (want {ZWorld.GunLossChance * 100:0} +- {tol * 100:0.0})"));
                sb.AppendLine(OzV11Kit.Verdict(t.rollsAfterLoss == 0, $"no draw once the gun is gone: {t.rollsAfterLoss} (the shelling went on)"));

                // ---------------------------------------------------------------- 2 small arms
                ZWorld.GroundFireDeviation = true;                                                        // the pyro's burning ground too (DV117)
                var rows2 = new List<string>(); int smallDraws = 0, quiet = 0;
                foreach (var type in new[] { "grunt", "psycho", "sniper", "pyro", "laser", "electrobot" })
                {
                    var w = World(settings, 2000 + type.Length);
                    var m = Mech(w, FX, FY); Set(m, 0.15);
                    var sq = Shooter(w, type, FX + 80, FY);
                    var ids = new HashSet<int>(OzV11Kit.Squad(sq).Select(u => u.Id));
                    foreach (var u in OzV11Kit.Squad(sq)) OzV11Kit.PlayerAttack(w, u, m);
                    long hpLost = 0; int drawsNoBlast = 0, grenades = 0;
                    while (w.Time < 20 && !w.GameOver)
                    {   // held at 15 % every tick: every hit lands under the line
                        Set(m, 0.15); int hp0 = m.Health, r0 = m.GunLossRolls; bool blast = false;
                        w.Tick(ZConst.TICK);
                        foreach (var e in w.Events) if (e.type == ZEventType.MissileExploded && ids.Contains(e.objId)) { blast = true; grenades++; }
                        w.Events.Clear();
                        hpLost += Math.Max(0, hp0 - m.Health);
                        if (!blast) drawsNoBlast += m.GunLossRolls - r0;
                    }
                    smallDraws += drawsNoBlast; if (hpLost == 0) quiet++;
                    rows2.Add($"{type}: HP taken {hpLost}, draws without a blast {drawsNoBlast}{(grenades > 0 ? $", grenade blasts {grenades} (blasts: counted as part 3)" : "")}");
                }
                ZWorld.GroundFireDeviation = wasFire;
                sb.AppendLine("Part 2, small arms on a mech held at 15 % for 20 s: " + string.Join("; ", rows2));
                sb.AppendLine(OzV11Kit.Verdict(smallDraws == 0, $"small arms never draw: {smallDraws} draws"));
                sb.AppendLine(OzV11Kit.Verdict(quiet == 0, $"each small arm did hit the mech: {quiet} of 6 without a hit"));

                // ---------------------------------------------------------------- 3 explosive weapons
                var rows3 = new List<string>(); int noDraw = 0, overDraw = 0, lost3 = 0;
                foreach (var type in new[] { "light", "heavy", "gun", "missile_launcher", "medium", "tough" })
                {
                    var w = World(settings, 3000 + type.Length);
                    var m = Mech(w, FX, FY); Set(m, 0.15);
                    var sq = Shooter(w, type, FX + 100, FY);
                    var ids = new HashSet<int>(OzV11Kit.Squad(sq).Select(u => u.Id));
                    foreach (var u in OzV11Kit.Squad(sq)) OzV11Kit.PlayerAttack(w, u, m);
                    int blasts = 0;
                    while (w.Time < 40 && !w.GameOver && !m.HandGunLost)
                    {
                        Set(m, 0.15);
                        w.Tick(ZConst.TICK);
                        foreach (var e in w.Events) if (e.type == ZEventType.MissileExploded && ids.Contains(e.objId)) blasts++;
                        w.Events.Clear();
                    }
                    if (m.GunLossRolls == 0) noDraw++;
                    if (m.GunLossRolls > blasts) overDraw++;
                    if (m.HandGunLost) lost3++;
                    rows3.Add($"{type}: {blasts} blasts, {m.GunLossRolls} draws, gun {(m.HandGunLost ? $"lost at {m.HandGunLostAt:0.00} s" : "kept")}");
                }
                sb.AppendLine("Part 3, explosive weapons on a mech held at 15 % (until the gun is lost, 40 s at most): " + string.Join("; ", rows3));
                sb.AppendLine(OzV11Kit.Verdict(noDraw == 0 && overDraw == 0, $"every explosive weapon's heavy hit draws: {noDraw} weapons without a draw, {overDraw} with more draws than blasts"));
                sb.AppendLine(OzV11Kit.Verdict(lost3 > 0, $"guns lost to them: {lost3} of 6"));

                // ---------------------------------------------------------------- 4 the gate
                int ctrlShots = 0, lostShots = 0, lostRockets = 0, ctrlCpu = 0, lostCpu = 0, notLost = 0;
                for (int k = 0; k < Math.Max(2, runs / 2); k++)
                {
                    { var w = World(settings, 4000 + k); var m = Mech(w, FX, FY); ctrlShots += GateFight(w, m, true).shots; }
                    { var w = World(settings, 4000 + k); var m = Mech(w, FX, FY); if (!LoseGun(w, m)) notLost++; var r = GateFight(w, m, true); lostShots += r.shots; lostRockets += r.rockets; }
                    { var w = World(settings, 4100 + k); var m = Mech(w, FX, FY); ctrlCpu += GateFight(w, m, false).shots; }
                    { var w = World(settings, 4100 + k); var m = Mech(w, FX, FY); if (!LoseGun(w, m)) notLost++; lostCpu += GateFight(w, m, false).shots; }
                }
                sb.AppendLine($"Part 4: ordered fights: with the gun {ctrlShots} hand-gun shots, without it {lostShots} (its rockets {lostRockets}); the computer's mech without an order: with the gun {ctrlCpu}, without it {lostCpu}");
                sb.AppendLine(OzV11Kit.Verdict(notLost == 0 && ctrlShots > 0 && lostShots == 0 && lostRockets > 0, $"a mech without its gun never fires it, its rockets do: {lostShots} shots, {lostRockets} rockets (with the gun: {ctrlShots} shots)"));
                sb.AppendLine(OzV11Kit.Verdict(notLost == 0 && ctrlCpu > 0 && lostCpu == 0, $"the computer's mech knows it: {lostCpu} shots by itself without the gun (with it: {ctrlCpu})"));

                // ---------------------------------------------------------------- 5 repair
                string repMap = OzV11Kit.Plain(96, 96, ("building:repair", 40, 40, "blue"));
                foreach (bool pad in new[] { true, false })
                {
                    ZWorld.RepairInPlaceDeviation = pad;
                    var w = World(settings, pad ? 5001 : 5002, repMap);
                    var stn = w.Objects.FirstOrDefault(o => o.Kind == ZKind.Building && o.Type == "repair");
                    if (stn == null) { sb.AppendLine(OzV11Kit.Verdict(false, "repair: no station on the test map")); continue; }
                    stn.GetRepairEntrance(out int ex, out int ey);
                    var m = Mech(w, ex, ey + 130, Huge, true);
                    bool gone = LoseGun(w, m);
                    double lostAt = m.HandGunLostAt; int before = m.Health;
                    var wp = ZWaypoint.Move(stn.CenterX, stn.CenterY, true); wp.refId = stn.Id; wp.mode = WpMode.UnitRepair; wp.attackTo = false; wp.x = stn.CenterX - 8; wp.y = stn.CenterY;
                    bool sent = w.SendWaypoints(Team.Blue, m, new[] { wp });
                    ZObject fresh = null; bool onPad = false, full = false;
                    double end = w.Time + 120;
                    while (w.Time < end && !w.GameOver && (pad ? !(full && m.Waypoints.Count == 0) : fresh == null || fresh.Waypoints.Count > 0))
                    {   // the trip, tick by tick: on the pad, full health, back off it (the pad) / the fresh vehicle out of the station (the swap)
                        w.Tick(ZConst.TICK);
                        foreach (var e in w.Events) if (e.type == ZEventType.RepairOut && fresh == null) fresh = w.GetAny(e.objId);
                        w.Events.Clear();
                        if (m.OnRepairPad(stn.Id)) onPad = true;
                        if (onPad && m.Health >= m.MaxHealth) full = true;
                    }
                    ZObject u = pad ? m : fresh;
                    bool ok = gone && sent && u != null && u.HandGunLost && !u.HasHandGun && (pad ? onPad && full && u.Health >= u.MaxHealth : u != m && u.HandGunLostAt == lostAt && u.Health >= u.MaxHealth);
                    int after = -1;
                    if (u != null && OzV11Kit.Alive(u)) { u.Waypoints.Clear(); after = GateFight(w, u, true).shots; }
                    sb.AppendLine($"Part 5{(pad ? "a, the pad (DV62)" : "b, Zod's repair swap (DV62 off)")}: gun lost {gone} at {lostAt:0.00} s with {before * 100L / Math.Max(1, m.MaxHealth)} % health; sent {sent}; " +
                                  (pad ? $"on the pad {onPad}, full health {full} ({u?.Health * 100L / Math.Max(1, u?.MaxHealth ?? 1)} %)" : $"the fresh vehicle #{(fresh != null ? fresh.Id : -1)} (was #{m.Id}), its health {(fresh != null ? fresh.Health * 100L / Math.Max(1, fresh.MaxHealth) : 0)} %, lost at {(fresh != null ? fresh.HandGunLostAt : -1):0.00} s") +
                                  $"; gun lost after it {u?.HandGunLost}; hand-gun shots in a fight after it {after}");
                    sb.AppendLine(OzV11Kit.Verdict(ok && after == 0, pad ? "repaired on the pad to full health, the gun stays lost, no shot after" : "Zod's repair swap: the fresh vehicle has lost the gun too, no shot after"));
                }
                ZWorld.RepairInPlaceDeviation = wasPad;

                // ---------------------------------------------------------------- 6 capture
                {
                    var w = World(settings, 6001);
                    var m = Mech(w, FX, FY);
                    bool gone = LoseGun(w, m);
                    m.DamageDriverHealth(m.Drivers.Count > 0 ? m.Drivers[0] : 0);
                    bool empty = m.Owner == Team.Neutral && m.Drivers.Count == 0;
                    var g = w.CreateRobotGroup("grunt", FX - 8, FY + 40, Team.Red, 1);
                    OzV11Kit.PlayerEnter(w, g, m);
                    Run(w, w.Time + 40, null, null);
                    bool taken = m.Owner == Team.Red && m.Drivers.Count > 0;
                    if (taken) m.Drivers[0] = Big;
                    int shots = taken ? GateFight(w, m, true).shots : -1;
                    sb.AppendLine($"Part 6: gun lost {gone}, the driver sniped (empty {empty}), a red grunt took it ({taken}); gun lost after {m.HandGunLost}; hand-gun shots in a fight for red {shots}");
                    sb.AppendLine(OzV11Kit.Verdict(gone && empty && taken && m.HandGunLost && shots == 0, "a captured mech keeps the loss"));
                }

                // ---------------------------------------------------------------- 7 switches, the rand() stream, determinism
                {
                    var off = new Tally(); ZWorld.MechGunLossDeviation = false; RuleWorld(settings, 1000, 40, Fractions, off); ZWorld.MechGunLossDeviation = true;
                    var zod = new Tally(); ZWorld.MechHandGunDeviation = false; RuleWorld(settings, 1000, 40, Fractions, zod); ZWorld.MechHandGunDeviation = true;
                    sb.AppendLine(OzV11Kit.Verdict(off.rolls == 0 && off.lost == 0 && off.heavyUnder > 0, $"the switch off: {off.rolls} draws, {off.lost} guns lost ({off.heavyUnder} blasts that would qualify)"));
                    sb.AppendLine(OzV11Kit.Verdict(zod.rolls == 0 && zod.lost == 0, $"DV146 off ('Zod exact', no hand gun): {zod.rolls} draws, {zod.lost} guns lost"));
                    ZWorld.MechGunLossDeviation = false; long h0 = EventHash(settings, 1100, 30); ZWorld.MechGunLossDeviation = true; long h1 = EventHash(settings, 1100, 30);
                    sb.AppendLine(OzV11Kit.Verdict(h0 == h1, $"no qualifying hit (health 30 % or more before every shell): the world is the same with the rule on and off ({h0:X} / {h1:X}): no extra draw"));
                    var a = new Tally(); RuleWorld(settings, 1003, 40, Fractions, a);
                    var b = new Tally(); RuleWorld(settings, 1003, 40, Fractions, b);
                    sb.AppendLine(OzV11Kit.Verdict(a.hash == b.hash && a.rolls == b.rolls && a.lost == b.lost && a.rolls > 0, $"deterministic: the same seed twice, the same blasts, draws ({a.rolls} / {b.rolls}) and losses ({a.lost} / {b.lost}) ({a.hash:X} / {b.hash:X})"));
                }

                // ---------------------------------------------------------------- 8 balance (report)
                ZWorld.DriverFireDeviation = true; ZWorld.MechHpDeviation = true;
                sb.AppendLine("Part 8, balance (no verdict): one blue mech (the game's health, it fights back, standing) against red units 100 px away that attack it, everybody real, 90 s at most:");
                sb.AppendLine("enemies | fights | mech killed | gun lost (of those, the mech still standing at the end) | mean health when lost | hand-gun shots before the loss (mean)");
                foreach (var foe in new[] { new[] { "light", "light" }, new[] { "tough", "tough" }, new[] { "light", "grunt" } })
                {
                    int fights = Math.Max(4, runs * 2), killed = 0, lostN = 0, lostAlive = 0; double hpAt = 0; long shotsBefore = 0;
                    for (int k = 0; k < fights; k++)
                    {
                        var w = World(settings, 8000 + k * 13 + foe[0].Length);
                        var m = OzV11Kit.Place(w, "medium", FX, FY, Team.Blue); m.MoveSpeed = 0; m.RealMoveSpeed = 0;
                        var foes = new List<ZObject>();
                        for (int j = 0; j < foe.Length; j++) foes.Add(OzV11Kit.Place(w, foe[j], FX + 100, FY - 30 + j * 60, Team.Red));
                        foreach (var f in foes) foreach (var u in OzV11Kit.Squad(f)) OzV11Kit.PlayerAttack(w, u, m);
                        OzV11Kit.PlayerAttack(w, m, foes[0]);
                        bool seen = false; int shots0 = 0;
                        while (w.Time < 90 && !w.GameOver && OzV11Kit.Alive(m) && foes.Any(f => OzV11Kit.Squad(f).Any(OzV11Kit.Alive)))
                        {
                            w.Tick(ZConst.TICK); w.Events.Clear();
                            if (!seen && m.HandGunLost) { seen = true; hpAt += m.Health / (double)m.MaxHealth; shots0 = m.HandGunShots; }
                        }
                        if (!OzV11Kit.Alive(m)) killed++;
                        if (m.HandGunLost) { lostN++; shotsBefore += shots0; if (OzV11Kit.Alive(m)) lostAlive++; }
                    }
                    sb.AppendLine($"{string.Join(" + ", foe)} | {fights} | {killed} | {lostN} ({lostAlive}) | {(lostN > 0 ? hpAt / lostN * 100 : 0):0} % | {(lostN > 0 ? shotsBefore / (double)lostN : 0):0.0}");
                }
            }
            finally
            {
                ZWorld.MechGunLossDeviation = was; ZWorld.MechHandGunDeviation = wasHg; ZWorld.DriverFireDeviation = wasDrv; ZWorld.RepairInPlaceDeviation = wasPad;
                ZWorld.GroundFireDeviation = wasFire; ZWorld.MechHpDeviation = wasHp;
            }
            return sb.ToString();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("OpenZ/Mech v2/22 Gun loss: counted rules check (headless, DV149)", false, 22)]
        static void Menu() => OzV11Kit.RunMenu("DV149 mech loses its hand gun", () => Run(OzV11Kit.SettingsText));
#endif
    }
}
