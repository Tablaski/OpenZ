// OpenZ (GPL-3.0) - version 1.3, owner item 3, part 2 (29 Oct 2026, a player on Discord: units
// The counted check of DV153 (Sim/ZObject.TowerAnswer.cs), headless: the simulation only (no scene, no
// Play mode, nothing saved). Levels 2, 7, 16 and 17 as shipped (their robots and vehicles taken out; forts, tower guns, buildings, rocks and every
// other cannon kept), per level the enemy tower gun of each side nearest the map's middle, and per weapon (the 13 robot and vehicle types, the
// grunt with grenades, and the 4 guns in A) one shooter of the other side:
//   A  placed idle inside its own range of the gun with a clear line: fired within 2 s?
//   B  placed 80 px out of its range, the player's ATTACK order on the gun: reached its range, and fired within 2 s of being in range?
//   C  placed idle out of its own range but inside the GUN's (the gun shells it): within 40 s it fired back, or is on its way to a tower gun of
//      that fort (the squad's help call, a slow tank that steps aside from every shell), or stands out of every tower gun's reach of that fort,
//      gone on a flag or vehicle run of its own that began before the first shell hit it (a move in progress: the help call never touches one, DV53);
//      a unit that stays under the fire, or walks off after it was hit, without an answer fails
//   D  placed 80 px out of its range, an attack-move to the fort's centre : once in range of a tower gun of that
//      fort, fired within 2 s?
//   E  (INFO) the same with a plain move (Zod's no-fight move): vehicles and the light robots (DV57) still fire on the way
// "Within 2 s" counts the time the unit is in range and on its feet (no robot of the squad knocked down): a robot knocked down by a heavy blast
// (DV51) may not fire until it is up, and a Tough then settles its tube again (DV11) before the rocket.
// The rules of a normal game (View/ZGame.cs with the default options: high ground +10 % on, lava and the 1.3 rules on); DV69's calm start (the
// level's first 20 s) is off, it is not what is measured. The shooter's side plays as the human player (no bot: the player's rules b49 / DV130 as
// in a game), the gun's side as a computer that gives no orders. The gun, the fort's other guns and its roof defender fire as in the game. Every
// shooter is staged with a health no shell can take in one hit (2 Nov: one howitzer shell killed a whole grunt squad through the old check's
// healing, and those cells read "never in range" / "NO SHOT" though the unit was dead), the gun is healed every tick. A shot counts when it is
// aimed at an enemy fort tower gun (the gun itself or another one of the same fort). C counts only trials in which a tower gun of that fort
// really fired at the shooter. Each row with DV153 off (as 1.2, INFO) and on (the verdicts). Every switch touched is put back afterwards.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace OpenZ.EditorTools
{
    public static class OzV13TowerFireCount
    {
        public static string[] Levels = { "L02", "L07", "L16", "L17" };
        public static int Seed = 1;
        /// <summary>Also play every row with DV153 off (as 1.2), INFO only.</summary>
        public static bool WithOff = true;
        /// <summary>The shooters: the 13 robot and vehicle types, the grunt with grenades, the 4 guns (guns only in A: they never move).</summary>
        public static readonly (string type, string kind)[] Shooters =
        {
            ("grunt", "robot"), ("psycho", "robot"), ("sniper", "robot"), ("tough", "robot"), ("pyro", "robot"), ("laser", "robot"), ("electrobot", "robot"), ("grunt", "grenades"),
            ("jeep", "vehicle"), ("light", "vehicle"), ("medium", "vehicle"), (ZMTank.Type, "vehicle"), ("heavy", "vehicle"), ("missile_launcher", "vehicle"),
            ("gatling", "cannon"), ("gun", "cannon"), ("howitzer", "cannon"), ("missile_cannon", "cannon"),
        };
        public const double FireWindow = 2.0, OrderWindow = 40.0;
        const int OutPx = 80;
        /// <summary>The staged shooter's health: no shell of the game takes it in one hit (the ratio Health / MaxHealth stays 1, as healed).</summary>
        public const int StagedHp = 1000000000;
        /// <summary>The 1.3 rules View/ZGame.cs turns on with the default options that OzV12MechHpCount's list (1.2) does not hold.</summary>
        static readonly string[] GameRules13 = { "HighGroundDeviation", "OrderMoveFallbackDeviation", "HeavyRockDeviation", "LavaDeviation", "DragonDeviation", "MTankAimDeviation", "MTankHurryDeviation", "CpuGunManDeviation" };

#if UNITY_EDITOR
        [MenuItem("OpenZ/V1.3/Tower guns: every unit in range fires, counted (headless, DV153)")]
        static void Menu()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.3] tower guns: leave Play mode first (the check sets the simulation's static switches)."); return; }
            Debug.Log("[OpenZ V1.3] tower guns fired at (DV153)\n" + Run(OzV11Kit.SettingsText, OzV11Kit.MapText));
        }

        [MenuItem("OpenZ/V1.3/Tower guns: every unit in range fires, quick (level 17 only)")]
        static void MenuQuick()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.3] tower guns: leave Play mode first."); return; }
            var keep = Levels; var off = WithOff; Levels = new[] { "L17" }; WithOff = false;
            try { Debug.Log("[OpenZ V1.3] tower guns fired at (DV153), quick\n" + Run(OzV11Kit.SettingsText, OzV11Kit.MapText)); } finally { Levels = keep; WithOff = off; }
        }
#endif

        // ------------------------------------------------------------------ the map: the level without its robots and vehicles
        static string Map(Func<string, string> map, string level)
        {
            var d = ZMapData.FromJson(map(level));
            d.objects.RemoveAll(o => { string c = o.kind.Split(':')[0]; return c == "robot" || c == "vehicle"; });
            return UnityEngine.JsonUtility.ToJson(d);
        }

        // ------------------------------------------------------------------ one trial
        public sealed class Trial
        {
            public bool placed, inRange, fired, firedAtGun, shelled, died, left, going;
            public double tIn = -1, tFirst = -1, onFeetInRange, downInRange, tHit = -1, tGrab = -1;
            /// <summary>C: out of the guns' reach at the end, and either never hit or its own flag / vehicle run began before the first hit
            /// (a move in progress, which the help call never touches: DV53). Left after a hit without an answer = walked off under fire.</summary>
            public bool LeftBeforeFire => left && (tHit < 0 || (tGrab >= 0 && tGrab <= tHit + 1e-9));
            public string last = "";
        }

        static bool Alive(ZObject o) => o != null && !o.Destroyed && o.KillAt < 0;
        /// <summary>A robot that left the map by boarding a vehicle or a gun (the rules take it off with its health whole): not a death.</summary>
        static bool Boarded(ZObject o) => o != null && o.KillAt >= 0 && o.Health > 0;
        static IEnumerable<ZObject> Members(ZObject o) { if (o == null) yield break; yield return o; foreach (var m in o.Minions) yield return m; }

        /// <summary>The range of a tower gun at a unit on the ground (DV162: +10 % from high ground when the rule is on).</summary>
        static int GunReach(ZObject gun) => ZWorld.HighGroundDeviation ? ZWorld.HighGroundRange(gun.AttackRadius) : gun.AttackRadius;

        /// <summary>A free spot for the shooter at `dist` px from the gun's centre, bearings from the fort's centre out through the gun, +-160 deg in
        /// 20 deg steps; needLine = a clear line (DV23 tiles + Zod's barrier rule) from the spot to the gun.</summary>
        static bool Spot(ZWorld w, ZObject gun, ZObject fort, int dist, bool robot, bool needLine, out int px, out int py)
        {
            px = py = 0;
            double b0 = Math.Atan2(gun.CenterY - fort.CenterY, gun.CenterX - fort.CenterX);
            for (int k = 0; k <= 16; k++)
            {
                int step = (k + 1) / 2 * ((k & 1) == 1 ? 1 : -1);
                double b = b0 + step * 20 * Math.PI / 180;
                int x = gun.CenterX + (int)Math.Round(Math.Cos(b) * dist), y = gun.CenterY + (int)Math.Round(Math.Sin(b) * dist);
                if (!Free(w, x, y, robot)) continue;
                if (needLine && (!w.Sight.Clear(x, y, 0, gun.CenterX, gun.CenterY, w.Sight.ExemptOf(gun)) || w.EngageBarrierBetween(x, y, gun.CenterX, gun.CenterY))) continue;
                px = x; py = y; return true;
            }
            return false;
        }

        static bool Free(ZWorld w, int cx, int cy, bool robot)
        {
            int tx = (cx - (robot ? 8 : 16)) >> 4, ty = (cy - (robot ? 8 : 16)) >> 4, n = robot ? 1 : 2;
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++) if (!w.Path.TilePassable(tx + i, ty + j, robot)) return false;
            if (w.Clearance != null) for (int j = 0; j < n; j++) for (int i = 0; i < n; i++) if (w.Clearance.MeshOwner(tx + i, ty + j) != 0) return false;
            foreach (var o in w.Objects)
                if (o.Kind != ZKind.MapItem && !o.Destroyed && o.Kind != ZKind.Building && Math.Abs(o.CenterX - cx) < 40 && Math.Abs(o.CenterY - cy) < 40) return false;
            return true;
        }

        static ZObject Make(ZWorld w, string type, string kind, int cx, int cy, Team team)
        {
            switch (kind)
            {
                case "robot": return w.CreateRobotGroup(type, cx - 8, cy - 8, team);
                case "grenades": { var s = w.CreateRobotGroup(type, cx - 8, cy - 8, team); if (s != null) s.Grenades = 4; return s; }
                case "cannon": return w.CreateObject("cannon", type, cx - 16, cy - 16, team);
                default: return w.CreateObject("vehicle", type, cx - 16, cy - 16, team);
            }
        }

        /// <summary>One shooter against one tower gun. scenario A..E as in the file's head.</summary>
        public static Trial One(string settings, Func<string, string> map, string level, int gunIndex, string type, string kind, char scenario)
        {
            var r = new Trial();
            var w = OzV11Kit.World(settings, Map(map, level), Seed);
            var gun = Guns(w).ElementAtOrDefault(gunIndex); if (gun == null) return r;
            var fort = FortOf(w, gun); if (fort == null) return r;
            Team team = gun.Owner == Team.Red ? Team.Blue : Team.Red;
            w.Bots.Clear(); w.Bots.Add(new ZBot(w, gun.Owner, Seed + 101) { Ignored = true });   // the shooter's side = the human player, the gun's side = a computer that gives no orders
            var u0 = w.S.Unit(type); if (u0 == null) return r;
            bool robot = kind == "robot" || kind == "grenades";
            int range = u0.attack_radius, gunReach = GunReach(gun);
            int dist; bool line;
            switch (scenario)
            {
                case 'A': dist = range - (robot ? 12 : 20); line = true; break;
                case 'C': if (gunReach - 16 <= range + 16) return r; dist = gunReach - 16; line = false; break;
                default: dist = range + OutPx; line = false; break;
            }
            if (!Spot(w, gun, fort, dist, robot, line, out int x, out int y)) return r;
            var u = Make(w, type, kind, x, y, team); if (u == null) return r;
            r.placed = true;
            var squad = Members(u).ToList();
            foreach (var m in squad) { m.MaxHealth = StagedHp; m.Health = StagedHp; }
            var fortGunList = w.Objects.Where(o => o.IsTowerGun && o.Owner == gun.Owner && FortOf(w, o) == fort).ToList();
            var fortGuns = new HashSet<int>(fortGunList.Select(o => o.Id));
            var ids = new HashSet<int>(squad.Select(m => m.Id));
            if (scenario == 'B') OzV11Kit.PlayerAttack(w, u, gun);
            if (scenario == 'D') OzV11Kit.PlayerMove(w, u, fort.CenterX, fort.CenterY, true);
            if (scenario == 'E') OzV11Kit.PlayerMove(w, u, fort.CenterX, fort.CenterY, false);
            w.Events.Clear();
            double t0 = w.Time, span = scenario == 'A' ? FireWindow + 0.5 : OrderWindow;
            while (w.Time < t0 + span)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                {
                    if (fortGuns.Contains(e.objId) && ids.Contains(e.otherId) && (e.type == ZEventType.MissileFired || e.type == ZEventType.Hitscan)) r.shelled = true;   // C: a tower gun fires at the shooter
                    if (!ids.Contains(e.objId) || !fortGuns.Contains(e.otherId)) continue;
                    if (e.type != ZEventType.MissileFired && e.type != ZEventType.Hitscan && e.type != ZEventType.ElectroCharge && e.type != ZEventType.ElectroBolt) continue;
                    if (r.tFirst < 0) { r.tFirst = w.Time - t0; r.fired = true; }
                    if (e.otherId == gun.Id) r.firedAtGun = true;
                }
                w.Events.Clear();
                foreach (var m in squad) { if (Alive(m)) m.Health = m.MaxHealth; else if (!Boarded(m)) r.died = true; }
                if (Alive(gun)) gun.Health = gun.MaxHealth;
                if (r.tHit < 0 && squad.Any(m => Alive(m) && m.LastEnemyHit > t0 + 1e-9)) r.tHit = w.Time - t0;          // the first hit on the squad
                var ld = squad.FirstOrDefault(Alive);
                if (r.tGrab < 0 && ld != null && ld.Waypoints.Count > 0)
                {                                                                               // the first run of its own: a flag, an empty vehicle or gun, a crate
                    var q = ld.Waypoints[0];
                    if (!q.playerGiven && (q.mode == WpMode.Enter || q.mode == WpMode.PickupGrenades || (q.mode == WpMode.Move && q.attackTo && q.refId >= 0))) r.tGrab = w.Time - t0;
                }
                bool inNow = scenario == 'B' || scenario == 'A' ? squad.Any(m => Alive(m) && m.WithinAttackRadius(gun))
                                                               : squad.Any(m => Alive(m) && fortGunList.Any(g => Alive(g) && m.WithinAttackRadius(g)));
                if (inNow && r.tIn < 0) { r.tIn = w.Time - t0; r.inRange = true; }
                if (r.tIn >= 0 && !r.fired)
                {
                    if (squad.All(m => !Alive(m) || !m.KnockedDown(w.Time))) r.onFeetInRange += ZConst.TICK; else r.downInRange += ZConst.TICK;   // on its feet: no robot of the squad knocked down (DV51)
                }
                if (r.fired && (scenario == 'C' || r.tIn >= 0)) break;
                if (r.died) break;
            }
            var lead = squad.FirstOrDefault(Alive) ?? u;
            var lw = lead.Waypoints.Count > 0 ? lead.Waypoints[0].mode.ToString() : Boarded(u) ? "boarded" : "idle";
            r.going = lead.Waypoints.Any(q => (q.mode == WpMode.Attack || q.mode == WpMode.Agro) && fortGuns.Contains(q.refId));   // C: on its way to a tower gun of that fort (the answer under way)
            r.left = squad.Where(Alive).All(m => fortGunList.All(g => !Alive(g) || OzV11Kit.Dist(g, m) > GunReach(g)));   // C: at the end out of every tower gun's reach of that fort (no longer under its fire)
            r.last = $"{lw}, {Math.Sqrt((double)(lead.CenterX - gun.CenterX) * (lead.CenterX - gun.CenterX) + (double)(lead.CenterY - gun.CenterY) * (lead.CenterY - gun.CenterY)):0} px, sees {(lead.CanSee(gun) ? 1 : 0)}";
            return r;
        }

        /// <summary>Per level: of each side's tower guns the one nearest the map's middle (0 = red's, 1 = blue's when both have one).</summary>
        static List<ZObject> Guns(ZWorld w)
        {
            int mx = w.Map.width * 8, my = w.Map.height * 8;
            var l = new List<ZObject>();
            foreach (var team in new[] { Team.Red, Team.Blue })
            {
                var g = w.Objects.Where(o => o.IsTowerGun && o.Owner == team && !o.Destroyed && FortOf(w, o) != null)
                                 .OrderBy(o => (long)(o.CenterX - mx) * (o.CenterX - mx) + (long)(o.CenterY - my) * (o.CenterY - my)).ThenBy(o => o.Id).FirstOrDefault();
                if (g != null) l.Add(g);
            }
            return l;
        }

        static ZObject FortOf(ZWorld w, ZObject c) =>
            w.Objects.FirstOrDefault(f => f.IsFort && c.CenterX >= f.LocX && c.CenterX < f.LocX + f.WidthPix && c.CenterY >= f.LocY && c.CenterY < f.LocY + f.HeightPix);

        /// <summary>The switches of a normal game with the default options, inside OzV11Kit.GameSwitches (which puts every one back).</summary>
        public static void SetGameRules()
        {
            OzV12MechHpCount.SetPlaySwitches();
            foreach (var n in GameRules13) OzV11Kit.TrySet(typeof(ZWorld), n, true);
            ZWorld.CalmStartDeviation = false;                                                    // DV69's first 20 s are not measured (the shooter's side is human)
        }

        // ------------------------------------------------------------------ the report
        public static string Run(string settings, Func<string, string> map)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DV153 fort tower guns are answered, counted headless; levels " + string.Join(", ", Levels));
            sb.AppendLine("A idle in range: fired within 2 s | B the player's attack order: in range, fired within 2 s on its feet | C idle under the gun's fire, out of its own range: within 40 s answered, on its way, or gone on a run of its own begun before the first hit | D attack-move to the fort: in range of a tower gun, fired within 2 s on its feet | E plain move (INFO)");
            bool allA = true, allB = true, allC = true, allD = true, staged = true; int cells = 0, chances0 = ZWorld.TowerChases, fails0 = ZWorld.TowerPlansFailed, steps0 = OzV11Kit.Counter(typeof(ZWorld), "TowerStepHelps");
            int cFired = 0, cGoing = 0, cLeft = 0, cNone = 0, cNot = 0;                           // C's outcomes with DV153 on
            var perType = new SortedDictionary<string, int[]>();                                // type -> cells judged, cells passed (DV153 on)
            using (OzV11Kit.GameSwitches())
            {
                try
                {
                    SetGameRules();
                    sb.AppendLine($"rules: high ground {(ZWorld.HighGroundDeviation ? "on" : "off")}, lava {(ZWorld.LavaDeviation ? "on" : "off")}, item 29 {(ZWorld.OrderMoveFallbackDeviation ? "on" : "off")}, calm start off; shooters staged with {StagedHp} HP, the gun healed");
                    foreach (bool on in WithOff ? new[] { false, true } : new[] { true })
                    {
                        ZWorld.TowerAnswerDeviation = on;
                        sb.AppendLine(on ? "## DV153 ON (1.3)" : "## DV153 OFF (as 1.2, INFO)");
                        foreach (var level in Levels)
                        {
                            var w0 = OzV11Kit.World(settings, Map(map, level), Seed);
                            var guns = Guns(w0);
                            if (guns.Count == 0) { sb.AppendLine($"{level}: no tower gun"); continue; }
                            for (int gi = 0; gi < guns.Count; gi++)
                            {
                                var g = guns[gi];
                                sb.AppendLine($"{level}: {g.Owner} {g.Type} on the fort tower at tile ({g.LocX >> 4},{g.LocY >> 4}), its range {g.AttackRadius} px ({GunReach(g)} px at a unit on the ground)");
                                foreach (var (type, kind) in Shooters)
                                {
                                    string name = kind == "grenades" ? "grunt+grenades" : type;
                                    var row = new StringBuilder($"  {name,-16}");
                                    foreach (char sc in "ABCDE")
                                    {
                                        if (kind == "cannon" && sc != 'A') { row.Append(" | " + sc + " -"); continue; }
                                        if (sc == 'C' && GunReach(g) - 16 <= (w0.S.Unit(type)?.attack_radius ?? 0) + 16) { row.Append(" | C - (the gun does not outrange it)"); continue; }
                                        var t = One(settings, map, level, gi, type, kind, sc);
                                        if (on) cells++;
                                        if (!t.placed) { row.Append(" | " + sc + " no spot"); if (on && sc != 'E') staged = false; continue; }
                                        if (t.died) { row.Append(" | " + sc + " DIED (staging)"); if (on) staged = false; continue; }
                                        if (sc == 'C' && !t.shelled && !t.fired) { row.Append(" | C not shelled (the gun never fired at it)"); if (on) cNot++; continue; }
                                        if (sc == 'C' && on) { if (t.fired) cFired++; else if (t.going) cGoing++; else if (t.LeftBeforeFire) cLeft++; else cNone++; }
                                        double feet = t.onFeetInRange;
                                        bool ok2 = sc == 'A' ? t.fired && t.tFirst <= FireWindow
                                                 : sc == 'C' ? t.fired || t.going || t.LeftBeforeFire
                                                 : sc == 'B' ? t.inRange && t.fired && feet <= FireWindow + 1e-9
                                                 : !t.inRange || (t.fired && feet <= FireWindow + 1e-9);
                                        string down = t.downInRange > 0.05 ? $", down {t.downInRange:0.0} s" : "";
                                        string cell = sc == 'A' ? (t.fired ? $"fired {t.tFirst:0.0} s" : "NO SHOT [" + t.last + "]")
                                                    : sc == 'C' ? (t.fired ? $"answered {t.tFirst:0.0} s" : t.going ? "on its way to the gun [" + t.last + "]" : t.LeftBeforeFire ? $"left the guns' reach, its own run at {t.tGrab:0.0} s, first hit {(t.tHit < 0 ? "none" : t.tHit.ToString("0.0") + " s")} [" + t.last + "]" : t.left ? $"WALKED OFF under fire (first hit {t.tHit:0.0} s) [" + t.last + "]" : "NO ANSWER [" + t.last + "]")
                                                    : !t.inRange ? (t.fired ? $"fired {t.tFirst:0.0} s before in range" : "never in range [" + t.last + "]")
                                                    : t.fired ? $"in range {t.tIn:0.0} s, fired {t.tFirst:0.0} s{down}" : $"in range {t.tIn:0.0} s, NO SHOT{down} [" + t.last + "]";
                                        row.Append(" | " + sc + " " + cell + (sc != 'E' && !ok2 ? " (!)" : ""));
                                        if (on && sc != 'E')
                                        {
                                            if (!perType.TryGetValue(name, out var pt)) perType[name] = pt = new int[2];
                                            pt[0]++; if (ok2) pt[1]++;
                                            if (sc == 'A' && !ok2) allA = false;
                                            if (sc == 'B' && !ok2) allB = false;
                                            if (sc == 'C' && !ok2) allC = false;
                                            if (sc == 'D' && !ok2) allD = false;
                                        }
                                    }
                                    sb.AppendLine(row.ToString());
                                }
                            }
                        }
                    }
                    sb.AppendLine($"C outcomes (DV153 on): fired back {cFired}, on its way {cGoing}, left on a run of its own begun before the fire {cLeft}, walked off under fire or no answer {cNone}, not shelled {cNot}");
                    sb.AppendLine("per weapon (DV153 on, A-D cells passed / judged): " + string.Join(", ", perType.Select(kv => $"{kv.Key} {kv.Value[1]}/{kv.Value[0]}")));
                    sb.AppendLine(OzV11Kit.Verdict(staged, $"S: the staging holds: every shooter placed and alive to the end ({cells} trials with DV153 on)"));
                    sb.AppendLine(OzV11Kit.Verdict(allA, "A: every unit placed idle in range of a tower gun fired at it within 2 s (DV153 on)"));
                    sb.AppendLine(OzV11Kit.Verdict(allB, "B: on the player's attack order every unit reached its range and fired within 2 s of being in range on its feet (DV153 on)"));
                    sb.AppendLine(OzV11Kit.Verdict(allC, "C: every unit shelled by a tower gun from beyond its own range answered it within 40 s (fired, or on its way to it), or was already on a run of its own when the fire hit it (DV153 on)"));
                    sb.AppendLine(OzV11Kit.Verdict(allD, "D: on an attack-move to the fort every unit that came in range of a tower gun fired within 2 s on its feet (DV153 on)"));
                    sb.AppendLine($"INFO  DV153 chases given as an ATTACK {ZWorld.TowerChases - chances0}, attacks with no shot spot remembered {ZWorld.TowerPlansFailed - fails0}, help-call chases put in behind a step aside {OzV11Kit.Counter(typeof(ZWorld), "TowerStepHelps") - steps0}");
                }
                catch (Exception ex) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + ex.GetType().Name + ": " + ex.Message)); }
            }
            return sb.ToString();
        }
    }
}
