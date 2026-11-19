#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 fort_damage, PART 1:
    /// the fort's time-to-kill measured headless on the Sim alone, nothing saved, no rule changed. Menu OpenZ/V1.5/Fort Damage/Fort health count
    /// (headless). The level 2 map without its robots, vehicles and guns (the forts, buildings and terrain kept); a red and a blue grunt that
    /// cannot die stand in far corners (nobody runs out of units); the blue fort is the target; its roof defender is held off (he would snipe
    /// the drivers) and the attackers cannot die (the fort's big gun still fires and still shares the fort's hits). Each case: the attackers
    /// placed about 12 tiles from the fort's middle on free ground, one attack order the way the player's input sends it (SEND_WAYPOINTS; Ctrl
    /// = the force-fire mark), then the world runs until the fort falls or MaxSeconds. One line per case: the order taken, the first hit, the
    /// health at 75 / 50 / 25 / 0 %, any rise of the fort's health (regeneration), the end. The game's switches (OzV11Kit.GameSwitches, as a
    /// level start sets them) plus the ones that touch a fort's damage as the game sets them (DV26 wall reach, DV113 Pyro burn, DV117 burning
    /// ground, DV104 / DV160 grenades); then the same cases with those off (Zod's rules) where they differ.
    /// </summary>
    public static class OzV15FortHealthCount
    {
        public const double MaxSeconds = 600;
        static int pass, fail; static StringBuilder sb;
        static void Check(bool ok, string what) { if (ok) pass++; else fail++; sb.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static void Info(string what) => sb.Append("INFO  ").AppendLine(what);

        sealed class Result
        {
            public string name; public bool accepted; public int hp0, hp1, rises, hits; public double first = -1, at75 = -1, at50 = -1, at25 = -1, fell = -1;
            public string Line()
            {
                string T(double t) => t < 0 ? "-" : $"{t:0.0} s";
                return $"{name}: {(accepted ? "order taken" : "order REFUSED")}, first damage {T(first)}, 75 % {T(at75)}, 50 % {T(at50)}, 25 % {T(at25)}, " +
                       $"fell {T(fell)}; health {hp0} -> {hp1}; rises {rises}";
            }
        }

        static string NoUnits(string mapJson)
        {
            var m = ZMapData.FromJson(mapJson);
            m.objects.RemoveAll(o => { string c = o.kind.Split(':')[0]; return c == "robot" || c == "vehicle" || c == "cannon"; });
            return UnityEngine.JsonUtility.ToJson(m);
        }

        static bool Free(ZWorld w, int tx, int ty, int r)
        {
            for (int i = -r; i <= r; i++)
                for (int j = -r; j <= r; j++)
                {
                    if (!w.Path.TilePassable(tx + i, ty + j, false)) return false;
                    int x = (tx + i) * 16 + 8, y = (ty + j) * 16 + 8;
                    foreach (var o in w.Objects) if (o.Kind == ZKind.Building && o.Contains(x, y)) return false;
                }
            return true;
        }

        static List<(int x, int y)> Spots(ZWorld w, ZObject t, int n)
        {
            var l = new List<(int, int)>(); int cx = t.CenterX >> 4, cy = t.CenterY >> 4;
            for (int r = 11; r <= 18 && l.Count < n; r++)
                for (int k = 0; k < 24 && l.Count < n; k++)
                {
                    double a = k * Math.PI / 12;
                    int tx = cx + (int)Math.Round(r * Math.Cos(a)), ty = cy + (int)Math.Round(r * Math.Sin(a));
                    if (!Free(w, tx, ty, 2) || l.Any(p => Math.Abs(p.Item1 - tx * 16) < 48 && Math.Abs(p.Item2 - ty * 16) < 48)) continue;
                    l.Add((tx * 16, ty * 16));
                }
            return l;
        }

        /// <summary>One case: `count` units of `type` (robots: squads), `ctrl` = the force-fire mark, `grenades` = each squad leader carries a box.</summary>
        static Result Case(string name, string type, int count, bool ctrl, bool grenades, int seed)
        {
            var r = new Result { name = name };
            var w = ZWorld.Create(OzV11Kit.SettingsText, NoUnits(OzV11Kit.MapText("L02")), seed);
            w.Bots.Clear();
            foreach (var (kt, kx) in new[] { (Team.Red, 16), (Team.Blue, (w.Map.width - 2) * 16) })
            { var d = w.CreateObject("robot", "grunt", kx, 16, kt); d.MaxHealth = d.Health = 1000000000; d.Damage = 0; }
            var fort = w.Objects.First(o => o.IsFort && o.Owner == Team.Blue);
            var spots = Spots(w, fort, count);
            var units = new List<ZObject>();
            foreach (var (x, y) in spots)
            {
                bool robot = ZBuildLists.IsRobot(type);
                var u = robot ? w.CreateRobotGroup(type, x, y, Team.Red) : w.CreateObject("vehicle", type, x, y, Team.Red);
                if (robot && grenades) u.Grenades = 20;
                units.Add(u);
                foreach (var m in OzV11Kit.Squad(u)) { m.MaxHealth = m.Health = 1000000000; }
            }
            r.hp0 = fort.Health; int last = fort.Health;
            foreach (var u in units)
            {
                var wp = ZWaypoint.Move(fort.CenterX, fort.CenterY, ctrl, fort.Id);
                wp.mode = WpMode.Attack;
                if (ctrl) { wp.fightOnWay = true; wp.forceFire = true; }                                   // as View/PlayerInput.cs BuildWaypoint with Ctrl held
                w.SendWaypoints(Team.Red, u, new[] { wp });
                r.accepted |= u.Waypoints.Count > 0 && u.Waypoints[0].mode == WpMode.Attack && u.Waypoints[0].refId == fort.Id;
            }
            w.Events.Clear();
            int max = fort.MaxHealth;
            while (w.Time < MaxSeconds)
            {
                w.Tick(ZConst.TICK);
                w.Events.Clear();
                int hp = fort.Health;
                if (hp > last) r.rises++;
                if (hp < last && r.first < 0) r.first = w.Time;
                last = hp;
                if (r.at75 < 0 && hp <= max * 3 / 4) r.at75 = w.Time;
                if (r.at50 < 0 && hp <= max / 2) r.at50 = w.Time;
                if (r.at25 < 0 && hp <= max / 4) r.at25 = w.Time;
                if (fort.Destroyed) { r.fell = w.Time; break; }
                if (!r.accepted && w.Time > 30) break;
            }
            r.hp1 = fort.Health;
            return r;
        }

        sealed class Rules : IDisposable
        {
            readonly bool reach = ZWorld.BuildingReachDeviation, burn = ZWorld.PyroForceBurnDeviation, ground = ZWorld.GroundFireDeviation, nade = ZWorld.GrenadeBoostDeviation, def = ZWorld.FortDefenderDeviation;
            public Rules(bool game)
            {
                ZWorld.BuildingReachDeviation = game; ZWorld.PyroForceBurnDeviation = game; ZWorld.GroundFireDeviation = game; ZWorld.GrenadeBoostDeviation = game;
                ZWorld.FortDefenderDeviation = false;                                                              // the roof sniper would empty the vehicles
            }
            public void Dispose() { ZWorld.BuildingReachDeviation = reach; ZWorld.PyroForceBurnDeviation = burn; ZWorld.GroundFireDeviation = ground; ZWorld.GrenadeBoostDeviation = nade; ZWorld.FortDefenderDeviation = def; }
        }

        /// <summary>The report (one line per fact, PASS / FAIL / INFO).</summary>
        public static string Run()
        {
            pass = fail = 0; sb = new StringBuilder();
            var set = ZSettings.Parse(OzV11Kit.SettingsText);
            var probe = ZWorld.Create(OzV11Kit.SettingsText, NoUnits(OzV11Kit.MapText("L02")), 1);
            var f0 = probe.Objects.First(o => o.IsFort);
            Check(f0.MaxHealth == (int)(set.fort_building_health * ZConst.MAX_UNIT_HEALTH) && f0.MaxHealth == 416666,
                  $"H1 a fort's hit points: {f0.MaxHealth} = building.fort.health {set.fort_building_health:0.000000} x {ZConst.MAX_UNIT_HEALTH} (Zod's default 10000 / 240, zsettings.cpp:256; zobject.cpp:136): not higher than Zod's");
            Check(f0.AttackedByExplosives && !f0.CanBeDestroyed, "H2 a fort takes explosives only (Zod zbuilding.cpp:17-19); it is not removed at 0 HP, its team is eliminated (CheckDestroyedFort)");
            var results = new List<Result>();
            using (new Rules(true))
            {
                results.Add(Case("4 pyros (one squad), Ctrl (DV113 burn)", "pyro", 1, true, false, 11));
                results.Add(Case("4 pyros (one squad), no Ctrl", "pyro", 1, false, false, 12));
                results.Add(Case("4 toughs (two squads)", "tough", 2, false, false, 13));
                results.Add(Case("4 lasers (one squad), Ctrl", "laser", 1, true, false, 14));
                results.Add(Case("4 lasers (one squad) with a grenade box", "laser", 1, false, true, 15));
                results.Add(Case("1 light tank", "light", 1, false, false, 16));
                results.Add(Case("1 medium tank", ZMTank.Type, 1, false, false, 17));
                results.Add(Case("1 mech", "medium", 1, false, false, 18));
                results.Add(Case("1 heavy tank", "heavy", 1, false, false, 19));
                results.Add(Case("3 light tanks", "light", 3, false, false, 20));
            }
            foreach (var r in results) Info(r.Line());
            var p = results[0]; var pn = results[1]; var tg = results[2]; var lz = results[3]; var lg = results[4]; var lt = results[5];
            Check(p.accepted && p.first >= 0 && p.hp1 < p.hp0, $"B1 burning works: 4 pyros on a Ctrl order take the fort from {p.hp0} to {p.hp1} ({(p.fell >= 0 ? $"down in {p.fell:0} s" : $"{100.0 * (p.hp0 - p.hp1) / p.hp0:0} % in {MaxSeconds:0} s")})");
            Check(!pn.accepted && pn.hp1 == pn.hp0, "B2 without Ctrl a pyro's attack order on the fort is refused (explosives only, Zod), the fort untouched");
            Check(!lz.accepted && lz.hp1 == lz.hp0, "B3 lasers without grenades cannot attack a fort (Ctrl or not)");
            Check(lg.accepted && lg.hp1 < lg.hp0, $"B4 a laser squad with a grenade box damages it ({lg.hp0} -> {lg.hp1}: 20 grenades, then no explosives left)");
            Check(results.All(r => r.rises == 0), "R1 nothing regenerates: the fort's health never rose in any case (no auto repair of a fort, no crane repair of a fort, Zod zobject.cpp:4489-4499)");
            Check(tg.accepted && tg.hp1 < tg.hp0 && lt.accepted && lt.hp1 < lt.hp0, "D1 every explosive attacker's shots take health off (toughs, tanks)");
            using (new Rules(false))
            {
                var zl = Case("1 light tank, Zod's rules (no DV26)", "light", 1, false, false, 16);
                var zp = Case("4 pyros, Ctrl, Zod's rules (no DV113)", "pyro", 1, true, false, 11);
                Info(zl.Line()); Info(zp.Line());
                Check(!zp.accepted && zp.hp1 == zp.hp0, "Z1 Zod: a pyro can never attack a fort");
                if (zl.fell > 0 && lt.fell > 0) Info($"Z2 one light tank: {lt.fell:0} s with DV26 against {zl.fell:0} s by Zod's rules (DV26 aims at the walls: more of each blast lands)");
            }
            sb.Insert(0, $"[OpenZ V1.5 fort health] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail} (no rule changed; the expected numbers are in the fort_damage kit README, PART 1)\n");
            return sb.ToString();
        }
    }
}
#endif
