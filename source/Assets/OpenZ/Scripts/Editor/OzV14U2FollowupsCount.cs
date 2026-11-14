using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.4 kit u2_followups: the counted rows of the DV176 follow-ups (Sim/ZWorld.DropEmptiedShots.cs, the DropAttacksOnEmptied calls in
    /// Headless on the Sim alone, nothing saved: menu OpenZ/V1.4/U2 Followups: Emptied Shots Count (headless, DV176), and the DV176 row's probe
    /// (Editor/OpenZParity.V14EmptiedTargets.cs). The counted checks' game switches (OzV11Kit.GameSwitches) with DV171 on; red the player, blue
    /// a CPU that gives no orders of its own. Every row needs its sample (the scene really reached the moment), and each row's rule-off twin
    /// must show the fault (else the row measured nothing).
    ///  F1  FORT: a blue fort's gun (DV7, with its mortar DV25 as in the game) laid and COMMITTED on a red light tank; EXIT DRIVER: shells
    ///      STARTED by the fort at the tank after the EXIT's tick in 8 s = 0 (its MissileFired events with the tank as target).
    ///  F1z the same with DV176 off: at least one shell at the empty tank (the committed shot of Sim/ZObject.FortGun.cs).
    ///  H1  HEAVY: a blue heavy tank (DV30 twin barrels) on the computer's attack order on the tank; right after its FIRST barrel left (the
    ///      second loaded, HeavySecondAt set), EXIT DRIVER: its shells at the tank after the EXIT in 3 s = 0, the loaded one dropped.
    ///  H1z the same with DV176 off: the second barrel fires at the empty hull.
    ///  S1  SHUTTLE: level 16's own ALAN &amp; BRAD take an empty shuttle (Brad the hero pilot, Alan aboard, DV118); a blue jeep shoots it; EXIT
    ///      (ramp, then ShuttleHeroesOut: everyone out, the ship neutral): right after the ship's emptying tick nobody of any team has it as
    ///      target or an attack / agro waypoint on it; the jeep's shots started at it in the next 10 s = 0.
    ///  S1z the same with DV176 off: the jeep keeps it (a target or an attack order left, or shots at it).
    ///  J1  EJECT: a red gatling gun (crewed) shot by a blue jeep and grunt squad; EJECT (Zod's generic EJECT_VEHICLE, ZWorld.Eject): right
    ///      after it nobody has it as target or an attack on it; their shots started at it in 10 s = 0.
    ///  J1z the same with DV176 off: the shots go on.
    ///  D1  F1 and H1 twice on the same seed: the same events bit for bit (deterministic).
    /// </summary>
    public static class OzV14U2FollowupsCount
    {
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);

        const Team Me = Team.Red, Cpu = Team.Blue;
        static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6 };

        sealed class Scene { public ZWorld w; public ulong hash = 1469598103934665603UL; }

        static Scene New(string set, string map, int seed)
        {
            var w = ZWorld.Create(set, map, seed);
            w.NoGameEnd = true;
            w.Bots.Add(new ZBot(w, Cpu, seed + 101) { Ignored = true });
            w.Events.Clear();
            var s = new Scene { w = w };
            Tick(s, 2, null);
            return s;
        }
        static void Tick(Scene s, int n, Action<ZEvent> each)
        {
            for (int i = 0; i < n; i++)
            {
                s.w.Tick(ZConst.TICK);
                foreach (var e in s.w.Events) { Hash(s, e); each?.Invoke(e); }
                s.w.Events.Clear();
            }
        }
        static void Hash(Scene s, ZEvent e)
        {
            unchecked
            {
                ulong h = s.hash;
                foreach (long x in new long[] { (long)e.type, e.objId, e.otherId, e.x, e.y }) { h ^= (ulong)x; h *= 1099511628211UL; }
                s.hash = h;
            }
        }
        static int Ticks(double sec) => (int)Math.Round(sec / ZConst.TICK);
        static bool ShotAt(ZEvent e, ZObject v) => e.otherId == v.Id && (e.type == ZEventType.Hitscan || e.type == ZEventType.MissileFired);
        static void Tough(ZObject o) { foreach (var m in OzV11Kit.Squad(o)) m.MaxHealth = m.Health = 1000000000; }

        /// <summary>A red light tank with its grunt driver (the player's ENTER), very tough.</summary>
        static ZObject Crewed(Scene s, int x, int y)
        {
            var v = s.w.CreateObject("vehicle", "light", x - 16, y - 16, Team.Neutral);
            var d = s.w.CreateRobotGroup("grunt", x - 64 - 8, y - 8, Me, 1);
            OzV11Kit.PlayerEnter(s.w, d, v);
            for (int i = 0; i < 2000 && !(v.Owner == Me && v.Drivers.Count > 0); i++) Tick(s, 1, null);
            v.MaxHealth = v.Health = 1000000000;
            return v;
        }

        /// <summary>Units of any team still on v: as target, or with an attack / agro waypoint on it.</summary>
        static int Holders(ZWorld w, ZObject v, out string who)
        {
            var names = new List<string>();
            foreach (var o in w.Objects)
            {
                if (o == v || o.Destroyed) continue;
                bool on = o.AttackObject == v || o.Waypoints.Any(q => (q.mode == WpMode.Attack || q.mode == WpMode.Agro) && q.refId == v.Id);
                if (on) names.Add($"{o.Type} #{o.Id} {o.Owner}");
            }
            who = names.Count == 0 ? "none" : string.Join(", ", names);
            return names.Count;
        }

        sealed class Res { public bool sample; public int before, after, holders = -1; public double ticks; public string note = "", who = ""; public ulong hash; }

        // ---------------------------------------------------------------- F1: the fort gun committed on the tank
        static Res Fort(string set, int seed, bool rule)
        {
            var r = new Res(); bool was = ZWorld.EmptiedTargetsDeviation; ZWorld.EmptiedTargetsDeviation = rule;
            try
            {
                var s = New(set, OzV11Kit.Plain(96, 96, ("building:fort_front", 40, 10, "blue")), seed);
                var fort = s.w.Objects.FirstOrDefault(o => o.IsFort && o.Owner == Cpu);
                if (fort == null) { r.note = "no blue fort on the map"; return r; }
                OzV11Kit.Dummy(s.w, 88 * 16, 88 * 16);                                                        // blue keeps a unit (a team with none loses its fort)
                var u = s.w.S.Unit("howitzer"); int range = ZObject.FortGunRange(u);
                int px = -1, py = -1;                                                                         // south of the fort_front (its rear arc is north), inside its reach and envelope
                for (int d = fort.HeightPix / 2 + 90; d < range - 24 && px < 0; d += 8)
                {
                    int x = fort.CenterX, y = fort.CenterY + d;
                    if (fort.FortGunCanReach(x, y)) { px = x; py = y; }
                }
                if (px < 0) { r.note = "no spot inside the fort gun's envelope"; return r; }
                var v = Crewed(s, px, py);
                if (v.Owner != Me) { r.note = "the driver did NOT get in"; return r; }
                v.Damage = 0;                                                                                  // it does not shell the fort meanwhile
                for (int i = 0; i < Ticks(30) && !(fort.FortGunTarget == v && fort.FortGunCommitted && s.w.Time < fort.FortGunReadyAt); i++) Tick(s, 1, null);
                if (!(fort.FortGunTarget == v && fort.FortGunCommitted)) { r.note = $"the fort gun never committed on the tank in 30 s (target {(fort.FortGunTarget != null ? fort.FortGunTarget.Type : "none")})"; return r; }
                r.note = $"committed, its shot due in {fort.FortGunReadyAt - s.w.Time:0.00} s";
                if (!s.w.Eject(Me, v)) { r.note += "; the EXIT was refused: " + (v.CanExitDriver(out string why) ? "?" : why); return r; }
                r.sample = true;
                r.holders = fort.FortGunTarget == v ? 1 : 0; r.who = $"fort gun target {(fort.FortGunTarget != null ? fort.FortGunTarget.Type + " #" + fort.FortGunTarget.Id : "none")}, committed {fort.FortGunCommitted}";
                int n = Ticks(8);
                for (int i = 0; i < n; i++) { Tick(s, 1, e => { if (e.type == ZEventType.MissileFired && e.objId == fort.Id && e.otherId == v.Id) r.after++; }); r.ticks++; }
                r.hash = s.hash;
                return r;
            }
            finally { ZWorld.EmptiedTargetsDeviation = was; }
        }

        // ---------------------------------------------------------------- H1: the heavy's loaded second barrel
        static Res Heavy(string set, int seed, bool rule)
        {
            var r = new Res(); bool was = ZWorld.EmptiedTargetsDeviation; ZWorld.EmptiedTargetsDeviation = rule;
            try
            {
                var s = New(set, OzV11Kit.Plain(64, 40), seed);
                int ox = 64 * 8, oy = 40 * 8;
                var v = Crewed(s, ox - 100, oy);
                if (v.Owner != Me) { r.note = "the driver did NOT get in"; return r; }
                var heavy = s.w.CreateObject("vehicle", "heavy", ox + 80 - 16, oy - 16, Cpu); Tough(heavy);
                OzV11Kit.CpuAttack(s.w, heavy, v);
                bool first = false;
                for (int i = 0; i < Ticks(40) && !first; i++)
                    Tick(s, 1, e => { if (e.type == ZEventType.MissileFired && e.objId == heavy.Id && e.otherId == v.Id) { r.before++; if (!e.flag2) first = true; } });
                if (!first || heavy.HeavySecondAt < 0) { r.note = $"no first barrel with a second one loaded in 40 s (shells {r.before})"; return r; }
                r.note = $"second barrel due in {heavy.HeavySecondAt - s.w.Time:0.000} s";
                if (!s.w.Eject(Me, v)) { r.note += "; the EXIT was refused: " + (v.CanExitDriver(out string why) ? "?" : why); return r; }
                r.sample = true;
                r.holders = heavy.HeavySecondAt >= 0 ? 1 : 0; r.who = $"second barrel loaded {heavy.HeavySecondAt >= 0}";
                for (int i = 0; i < Ticks(3); i++) { Tick(s, 1, e => { if (e.type == ZEventType.MissileFired && e.objId == heavy.Id && e.otherId == v.Id) r.after++; }); r.ticks++; }
                r.hash = s.hash;
                return r;
            }
            finally { ZWorld.EmptiedTargetsDeviation = was; }
        }

        // ---------------------------------------------------------------- S1: the hero pilot's shuttle EXIT
        static Res Shuttle(string set, string m16, bool rule)
        {
            var r = new Res(); bool was = ZWorld.EmptiedTargetsDeviation; ZWorld.EmptiedTargetsDeviation = rule;
            bool spd = ZWorld.ShuttlePlayableDeviation, son = ZWorld.ShuttleOn, hero = ZWorld.HeroSquadDeviation; var st = ZWorld.ShuttleTeam;
            try
            {
                ZWorld.ShuttlePlayableDeviation = true; ZWorld.ShuttleOn = true; ZWorld.ShuttleTeam = Me; ZWorld.HeroSquadDeviation = true;
                var w = OzV11Kit.World(set, m16, 11); w.NoGameEnd = true;
                ZWorld.HeroSquadDeviation = hero;
                var s = new Scene { w = w };
                var brad = w.Objects.FirstOrDefault(o => o.IsHero && o.HeroMemberName == "brad" && OzV11Kit.Alive(o) && o.Owner == Me);
                if (brad == null) { r.note = "no ALAN & BRAD on level 16"; return r; }
                ZObject ship = null;
                for (int d = 200; d <= 700 && ship == null; d += 32)
                    for (int a = 0; a < 16 && ship == null; a++)
                    {
                        double g = a * Math.PI / 8;
                        ship = w.SpawnShuttle(brad.CenterX + (int)(Math.Cos(g) * d), brad.CenterY + (int)(Math.Sin(g) * d), Team.Neutral, -90f, out _);
                    }
                if (ship == null) { r.note = "no empty shuttle could be dropped"; return r; }
                OzV11Kit.PlayerEnter(w, brad, ship);
                for (int i = 0; i < Ticks(60) && ship.Drivers.Count == 0; i++) Tick(s, 1, null);
                for (int i = 0; i < Ticks(20) && !(ship.ShuttleRampPhase == ZShuttleRamp.Shut && !ship.ShuttleExitPending && w.Time >= ship.ShuttleRampHoldUntil); i++) Tick(s, 1, null);
                if (!ship.ShuttleHeroPilot || ship.Owner != Me) { r.note = $"the heroes did not take the ship (pilot {ship.Drivers.Count}, hero {ship.ShuttleHeroPilot}, team {ship.Owner})"; return r; }
                ship.MaxHealth = ship.Health = 1000000000;
                var jeep = w.CreateObject("vehicle", "jeep", ship.CenterX + 120 - 16, ship.CenterY + 60 - 16, Cpu); Tough(jeep);
                OzV11Kit.CpuAttack(w, jeep, ship);
                for (int i = 0; i < Ticks(40) && r.before == 0; i++) Tick(s, 1, e => { if (ShotAt(e, ship) && e.objId == jeep.Id) r.before++; });
                if (r.before == 0) { r.note = "the jeep never fired at the ship in 40 s"; return r; }
                if (!w.Eject(Me, ship)) { r.note = "the EXIT was refused"; return r; }
                for (int i = 0; i < Ticks(20) && ship.Owner != Team.Neutral; i++) Tick(s, 1, e => { if (ShotAt(e, ship) && e.objId == jeep.Id) r.before++; });
                if (ship.Owner != Team.Neutral || ship.Drivers.Count > 0) { r.note = $"the ship was not emptied within 20 s of the EXIT (team {ship.Owner}, pilot {ship.Drivers.Count})"; return r; }
                r.sample = true;
                r.holders = Holders(w, ship, out r.who);
                for (int i = 0; i < Ticks(10); i++)
                {
                    if (ship.Owner != Team.Neutral || ship.Destroyed) break;
                    Tick(s, 1, e => { if (ShotAt(e, ship) && e.objId == jeep.Id) r.after++; }); r.ticks++;
                }
                r.note = $"jeep: {(jeep.AttackObject != null ? "target " + jeep.AttackObject.Type + " #" + jeep.AttackObject.Id + " " + jeep.AttackObject.Owner : "no target")}";
                r.hash = s.hash;
                return r;
            }
            finally { ZWorld.EmptiedTargetsDeviation = was; ZWorld.ShuttlePlayableDeviation = spd; ZWorld.ShuttleOn = son; ZWorld.HeroSquadDeviation = hero; ZWorld.ShuttleTeam = st; }
        }

        // ---------------------------------------------------------------- J1: a gun's EJECT
        static Res Eject(string set, int seed, bool rule)
        {
            var r = new Res(); bool was = ZWorld.EmptiedTargetsDeviation; ZWorld.EmptiedTargetsDeviation = rule;
            try
            {
                var s = New(set, OzV11Kit.Plain(64, 40), seed);
                int ox = 64 * 8, oy = 40 * 8;
                ZObject gun = null;
                foreach (var t in new[] { "gatling", "gun", "missile_cannon", "howitzer" })
                {
                    var g = s.w.CreateObject("cannon", t, ox - 120 - 16, oy - 16, Me);
                    if (g != null && g.CanEjectDrivers() && g.Drivers.Count > 0) { gun = g; break; }
                    if (g != null) s.w.RemoveNow(g);
                }
                if (gun == null) { r.note = "no ejectable crewed gun"; return r; }
                gun.MaxHealth = gun.Health = 1000000000; gun.Damage = 0;
                var jeep = s.w.CreateObject("vehicle", "jeep", ox + 40 - 16, oy - 16, Cpu); Tough(jeep);
                var squad = s.w.CreateRobotGroup("grunt", ox - 10 - 8, oy + 70 - 8, Cpu, 3); Tough(squad);
                OzV11Kit.CpuAttack(s.w, jeep, gun); OzV11Kit.CpuAttack(s.w, squad, gun);
                var foes = new HashSet<int>(new[] { jeep.Id }.Concat(OzV11Kit.Squad(squad).Select(o => o.Id)));
                bool j = false, q = false;
                for (int i = 0; i < Ticks(60) && !(j && q); i++) Tick(s, 1, e => { if (!ShotAt(e, gun) || !foes.Contains(e.objId)) return; r.before++; if (e.objId == jeep.Id) j = true; else q = true; });
                if (!(j && q)) { r.note = $"the jeep / the squad did not both fire at the gun in 60 s (shots {r.before})"; return r; }
                if (!s.w.Eject(Me, gun)) { r.note = "the EJECT was refused"; return r; }
                r.sample = true; r.note = gun.Type;
                r.holders = Holders(s.w, gun, out r.who);
                for (int i = 0; i < Ticks(10); i++)
                {
                    if (gun.Owner != Team.Neutral || gun.Destroyed) break;
                    Tick(s, 1, e => { if (ShotAt(e, gun) && foes.Contains(e.objId)) r.after++; }); r.ticks++;
                }
                r.hash = s.hash;
                return r;
            }
            finally { ZWorld.EmptiedTargetsDeviation = was; }
        }

        static Res FirstWithSample(Func<int, Res> run) { Res r = null; foreach (int seed in Seeds) { r = run(seed); if (r.sample) { r.note = $"seed {seed}: " + r.note; return r; } } return r; }

        static string Body(string set, string m16)
        {
            pass = fail = 0; sb = new StringBuilder();
            int f0 = ZWorld.EmptiedFortDropped, fc0 = ZWorld.EmptiedFortCommitDropped, s0 = ZWorld.EmptiedSecondDropped;
            using (OzV11Kit.GameSwitches())
            {
                bool dv171 = ZWorld.VehicleExitDeviation; ZWorld.VehicleExitDeviation = true;
                try
                {
                    int seedF = 1;
                    var f = FirstWithSample(sd => { seedF = sd; return Fort(set, sd, true); });
                    Check(f.sample && f.ticks > 0 && f.holders == 0 && f.after == 0, $"F1 a blue fort's gun committed on our light tank, EXIT DRIVER ({f.note}): right after it {f.who}; its shells started at the empty tank in the next {f.ticks * ZConst.TICK:0.0} s: {f.after} (want 0)");
                    var fz = Fort(set, seedF, false);
                    Check(fz.sample && fz.after > 0, $"F1z the same with DV176 off ({fz.note}): the fort's shells at the empty tank {fz.after} (want > 0: the fault this row looks for is there)");
                    int seedH = 1;
                    var h = FirstWithSample(sd => { seedH = sd; return Heavy(set, sd, true); });
                    Check(h.sample && h.ticks > 0 && h.holders == 0 && h.after == 0, $"H1 a blue heavy tank's first barrel left, its second loaded, EXIT DRIVER ({h.note}): right after it {h.who}; its shells at the empty tank in the next {h.ticks * ZConst.TICK:0.0} s: {h.after} (want 0; before: {h.before})");
                    var hz = Heavy(set, seedH, false);
                    Check(hz.sample && hz.after > 0, $"H1z the same with DV176 off ({hz.note}): its shells at the empty tank {hz.after} (want > 0: the second barrel)");
                    if (string.IsNullOrEmpty(m16)) Check(false, "S1 the shuttle row needs level 16's map");
                    else
                    {
                        var sh = Shuttle(set, m16, true);
                        Check(sh.sample && sh.ticks > 0 && sh.holders == 0 && sh.after == 0, $"S1 ALAN & BRAD's shuttle (Brad the hero pilot) shot by a blue jeep, EXIT: everyone out, the ship neutral; right after its emptying tick units still on it {sh.holders} ({sh.who}); the jeep's shots started at it in the next {sh.ticks * ZConst.TICK:0.0} s: {sh.after} (want 0; before {sh.before}); {sh.note}");
                        var shz = Shuttle(set, m16, false);
                        Check(shz.sample && (shz.holders > 0 || shz.after > 0), $"S1z the same with DV176 off: units still on the empty ship {shz.holders} ({shz.who}), the jeep's shots at it {shz.after} (want > 0 of either: the fault is there); {shz.note}");
                    }
                    int seedJ = 1;
                    var jj = FirstWithSample(sd => { seedJ = sd; return Eject(set, sd, true); });
                    Check(jj.sample && jj.ticks > 0 && jj.holders == 0 && jj.after == 0, $"J1 a red gun shot by a blue jeep and grunt squad, EJECT ({jj.note}): right after it units still on it {jj.holders} ({jj.who}); their shots started at it in the next {jj.ticks * ZConst.TICK:0.0} s: {jj.after} (want 0; before {jj.before})");
                    var jz = Eject(set, seedJ, false);
                    Check(jz.sample && jz.after > 0, $"J1z the same with DV176 off ({jz.note}): their shots at the empty gun {jz.after} (want > 0)");
                    var f2 = Fort(set, seedF, true); var h2 = Heavy(set, seedH, true);
                    Check(f.sample && h.sample && f2.hash == f.hash && h2.hash == h.hash, $"D1 F1 and H1 again on the same seeds: the same events bit for bit ({f.hash:x16} / {f2.hash:x16}, {h.hash:x16} / {h2.hash:x16})");
                }
                finally { ZWorld.VehicleExitDeviation = dv171; }
                Info($"counters in this run: fort guns that dropped an emptied vehicle {ZWorld.EmptiedFortDropped - f0} (committed {ZWorld.EmptiedFortCommitDropped - fc0}), heavy second shells dropped {ZWorld.EmptiedSecondDropped - s0}");
            }
            return $"OzV14U2FollowupsCount (DV176 follow-ups): {(fail == 0 && pass > 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}\n" + sb;
        }

        /// <summary>The whole count (the menu, and the DV176 row's probe). m16 = level 16's map (the shuttle row).</summary>
        public static string Quick(string settings, string m16) => Body(settings, m16);

#if UNITY_EDITOR
        [UnityEditor.MenuItem("OpenZ/V1.4/U2 Followups: Emptied Shots Count (headless, DV176)")]
        static void Menu() => OzV11Kit.RunMenu("emptied shots: count (DV176 follow-ups)", () => Quick(OzV11Kit.SettingsText, OzV11Kit.MapText("L16")));
#endif
    }
}
