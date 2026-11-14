#if !OPENZ_HARNESS
// OpenZ (GPL-3.0) - version 1.3, owner item 32 (DV160, Sim/ZWorld.GrenadeBoost.cs):
// The counted check, headless (the simulation only: no scene, no Play mode, nothing saved), on open flat ground, the game's switches
// (OzV12MechHpCount.SetPlaySwitches: DV104 on, the medium tank's own rules DV145 / DV150 / DV151 on, as ZGame sets them). Every row is run at
// DV160's step x1.00 (= the 1.2 grenade), x1.10, x1.15 (shipped) and x1.20, on the same worlds (the same seeds, so only the grenade differs).
//   1  the rules: the shipped step, a real thrown grenade (direct / area / radius / time between throws / flight), Zod's grenade with DV104 off
//   2  every landing point of a throw (48 x 48) on a robot and on a tank: none does less than Zod's grenade; one grenade on a fresh robot;
//   3  tanks: (a) grunt squads with a box against a tank of the CPU that stands and fires back; (b) the PLAYER's tank on an attack order (it is
//      not told to step aside, DV16) against grunt squads of the CPU with a box; (c) grunt squads with a box against a CPU tank free to move
//      (it steps aside, Zod's DodgeMissile): light tank, medium tank, heavy tank
//   4  robots: one grunt squad with a box against a grunt, a psycho and a laser squad without
//   5  a tank push: (a) the CPU's light, medium and heavy tank on an attack-move through three grunt squads of the player with a box each;
//      (b) the player's three tanks on attack orders against three grunt squads of the CPU with a box each
//   6  the same fight twice gives the same result
// Counted per row: fights won by the grenadiers / by the other side / draws, the win share, the average time to kill (first grenade to the
// kill), grenades thrown and landed per fight, grenades landed per kill (the tank line over the worlds both steps won), tanks destroyed in
// the push rows. Every switch touched is put back afterwards.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzV13GrenadeCount
    {
        const int CX = 1024, CY = 1024, Big = 1000000000;
        /// <summary>Worlds per distance and row (OzV11GrenadeCount.Seed spreads them); the quick menu uses 4.</summary>
        public static int Seeds = 60;
        /// <summary>Start distance (px) between the grenadiers and the tank / squad: inside every gun's range (110) and from further out (220).</summary>
        public static int[] Distances = { 110, 220 };
        public static double MaxSeconds = 120;
        /// <summary>The rows: DV160's per cent. 100 = the 1.2 grenade (the "before" row), ZWorld.ShippedGrenadeStrongerPct the verdict row.</summary>
        public static int[] Steps = { 100, 110, 115, 120 };
        public static readonly string[] Tanks = { "light", ZMTank.Type, "heavy" };
        public static readonly string[] Robots = { "grunt", "psycho", "laser" };

        // ---- pass lines (SOLUTION.md section 3 says why each is where it is)
        /// <summary>"Clearly better against tanks": a tank that stands takes at least this share fewer grenades to destroy than with the 1.2
        /// grenade, counted EXACTLY over every landing point (part 2, the game's own rule, the tanks' own boxes and health; the line the step
        /// was chosen on, tools/step_scan.py: 11.3 / 12.2 / 12.3 %). 10 % = the counted checks' own "clearly" tolerance (runs / 10, OzV11GrenadeCount).</summary>
        public const double MinFewerGrenadesPerKill = 0.10;
        /// <summary>
        /// seeds), so the 1.2 row's lucky wins are not set against the step's harder ones. Half the exact line: the exact cut is 11-12 %, its
        /// sampling error over ~48 paired worlds about 1.5 points (a world needs the same or one grenade fewer), so 5 % is more than 4 errors
        /// under it; a cut under 5 % would mean the fights do not follow the landing-point count. At least MinPairedWorlds such worlds.</summary>
        public const double MinFewerGrenadesPerKillFights = 0.05;
        public const int MinPairedWorlds = 10;
        /// <summary>The player's tank on an attack order against the CPU's grenadiers: the grenadiers' win share, summed over the three tanks, rises
        /// by at least this (share points, the same worlds before and after).</summary>
        public const double MinTankShareGain = 0.05;
        /// <summary>"Not overpowered against robots": no robot row's win share rises by more than this, and the robot rows together rise no more
        /// than the tank rows of 3b together.</summary>
        public const double MaxRobotRowGain = 0.15;
        /// <summary>One grenade (every landing point of a throw aimed at it) fells a fresh grunt at most this share of the time, a fresh psycho /
        /// sniper at most PsychoOneShotCap (Zod 15.1 % / 0 %, the 1.2 grenade 14.0 % / 1.3 %, the shipped step 24.7 % / 7.7 % in tools/step_scan.py).</summary>
        public const double GruntOneShotCap = 0.25, PsychoOneShotCap = 0.10;

        static string settings, flat;
        static string Settings => settings ??= OzV11Kit.SettingsText;
        static string Flat => flat ??= OzV11Kit.Plain(128, 128);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static string P(double share) => (100.0 * share).ToString("0.0", Inv) + " %";
        static string F(double v, string f = "0.0") => v.ToString(f, Inv);

        [MenuItem("OpenZ/V1.3/Grenades stronger: counted (headless, DV160)")]
        static void Menu()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.3] grenades: leave Play mode first (the check sets the simulation's static switches)."); return; }
            Debug.Log("[OpenZ V1.3] grenades stronger (DV160)\n" + Run(true));
        }

        [MenuItem("OpenZ/V1.3/Grenades stronger: quick (4 worlds a row)")]
        static void MenuQuick()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.3] grenades: leave Play mode first."); return; }
            int keep = Seeds; Seeds = 4;
            try { Debug.Log("[OpenZ V1.3] grenades stronger, quick (DV160)\n" + Run(true)); } finally { Seeds = keep; }
        }

        // ================================================================== worlds
        /// <summary>Blue = a CPU team that gives no orders of its own, red = the player; two far keepers that cannot move, shoot or die, so the
        /// game is never over.</summary>
        static ZWorld NewWorld(int seed)
        {
            var w = OzV11Kit.World(Settings, Flat, seed);
            var red0 = w.CreateRobotGroup("grunt", 48, 48, Team.Red, 1);
            red0.MoveSpeed = 0; red0.RealMoveSpeed = 0; red0.Damage = 0; red0.MaxHealth = red0.Health = Big;
            OzV11Kit.Dummy(w, 2000, 2000);
            return w;
        }

        static bool Out(ZObject o) => !OzV11Kit.Alive(o) || o.Owner == Team.Neutral;
        static bool InAir(ZWorld w) => w.PendingMissiles.Any(m => m.grenade) || w.QueuedMissiles.Any(m => m.grenade);
        static ZObject Nearest(ZObject from, List<ZObject> all)
        {
            ZObject best = null; double bd = double.MaxValue;
            foreach (var o in all) { if (Out(o)) continue; double d = OzV11Kit.Dist(from, o); if (d < bd) { bd = d; best = o; } }
            return best;
        }

        /// <summary>A grunt squad with a box of 20 grenades.</summary>
        static ZObject Grenadiers(ZWorld w, int cx, int cy, Team team, List<ZObject> bodies)
        {
            var s = OzV11Kit.Place(w, "grunt", cx, cy, team); s.Grenades = 20;
            bodies.AddRange(OzV11Kit.Squad(s));
            return s;
        }

        // ================================================================== one fight
        public struct Fight { public bool decided, nadesWon; public double ttk; public int thrown, landed, tanksDown; }

        /// <summary>kind: "stand" (3a), "ordered" (3b), "free" (3c), "robot" (4), "pushCpu" (5a), "pushPlayer" (5b).
        /// enemy: the tank (3) or the robot type (4); squads: grenadier squads (3) / ignored (4, 5).</summary>
        public static Fight One(string kind, string enemy, int squads, int dist, int seed)
        {
            var w = NewWorld(seed);
            var nades = new List<ZObject>(); var others = new List<ZObject>();
            bool nadesRed = kind != "ordered" && kind != "pushPlayer";                          // the grenadiers' side: the player's, but the CPU's in 3b / 5b
            Team nt = nadesRed ? Team.Red : Team.Blue, ot = nadesRed ? Team.Blue : Team.Red;
            var leaders = new List<ZObject>(); var tanks = new List<ZObject>();
            if (kind == "pushCpu" || kind == "pushPlayer")
            {
                for (int i = 0; i < 3; i++) leaders.Add(Grenadiers(w, CX, CY + (i - 1) * 64, nt, nades));
                for (int i = 0; i < Tanks.Length; i++) { var t = OzV11Kit.Place(w, Tanks[i], CX + (kind == "pushCpu" ? 360 : 220), CY + (i - 1) * 64, ot); tanks.Add(t); others.Add(t); }
            }
            else if (kind == "robot")
            {
                leaders.Add(Grenadiers(w, CX, CY, nt, nades));
                var r = OzV11Kit.Place(w, enemy, CX + dist, CY, ot); others.AddRange(OzV11Kit.Squad(r));
            }
            else
            {
                for (int q = 0; q < squads; q++) leaders.Add(Grenadiers(w, CX, CY + (q - (squads - 1) / 2) * 48, nt, nades));
                var t = OzV11Kit.Place(w, enemy, CX + dist, CY, ot); tanks.Add(t); others.Add(t);
                if (kind == "stand") { t.MoveSpeed = 0; t.RealMoveSpeed = 0; }                  // it fires back but cannot move: never told to step aside
            }
            // orders
            if (kind == "stand" || kind == "free" || kind == "robot") foreach (var l in leaders) OzV11Kit.PlayerAttack(w, l, others[0]);
            if (kind == "ordered" || kind == "pushPlayer")
            {
                foreach (var l in leaders) OzV11Kit.CpuAttack(w, l, Nearest(l, tanks));
                foreach (var t in tanks) OzV11Kit.PlayerAttack(w, t, Nearest(t, leaders));
            }
            if (kind == "pushCpu") foreach (var t in tanks) OzV11Kit.PlayerMove(w, t, CX - 300, t.CenterY, true);   // an attack-move through the squads (it steps aside)
            var nadeIds = new HashSet<int>(nades.Select(o => o.Id));
            w.Events.Clear();
            var f = new Fight(); double first = -1, end = w.Time + MaxSeconds, overAt = -1; int guard = 0;
            Fight Done(Fight x) { x.tanksDown = tanks.Count(Out); return x; }
            while (w.Time < end)
            {
                if ((kind == "ordered" || kind == "pushPlayer") && ++guard % 10 == 0)          // an attentive player orders an idle tank onto the next squad
                    foreach (var t in tanks)
                        if (!Out(t) && !(t.Waypoints.Count > 0 && t.Waypoints[0].mode == WpMode.Attack)) { var n = Nearest(t, nades); if (n != null) OzV11Kit.PlayerAttack(w, t, n); }
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                {
                    if (!e.flag || !nadeIds.Contains(e.objId)) continue;
                    if (e.type == ZEventType.MissileFired) { f.thrown++; if (first < 0) first = w.Time; }
                    else if (e.type == ZEventType.MissileExploded) f.landed++;
                }
                w.Events.Clear();
                bool othersOut = others.All(Out), nadesOut = nades.All(Out);
                if (othersOut) { f.decided = true; f.nadesWon = true; f.ttk = first >= 0 ? w.Time - first : 0; return Done(f); }
                if (nadesOut) { if (overAt < 0) overAt = w.Time; if (!InAir(w) || w.Time - overAt > 6) { f.decided = true; return Done(f); } continue; }   // the grenades already thrown still land
                if (kind != "robot" && f.thrown > 0 && leaders.Where(l => !Out(l)).Sum(l => l.Grenades) == 0 && nades.Where(o => !Out(o)).All(o => o.Grenades == 0) && !InAir(w))
                { f.decided = true; return Done(f); }                                            // the boxes are empty and the tank(s) still stand: the tanks' fight
            }
            return Done(f);                                                                      // nobody done after MaxSeconds: a draw
        }

        public sealed class Row
        {
            public string name; public int step, fights, won, lost, draws; public long thrown, landed, landedAtKill, tanksDown; public double ttk;
            /// <summary>Per world, in order: grenades landed when the grenadiers won it, -1 when they did not (for the paired line).</summary>
            public readonly List<int> perFight = new List<int>();
            public double Share => won + lost > 0 ? (double)won / (won + lost) : 0;
            public double PerKill => won > 0 ? (double)landedAtKill / won : 0;
            public string Text() =>
                $"  x{step / 100.0:0.00}: grenadiers win {won,3}, the other side {lost,3}, draws {draws,3} of {fights} | grenadiers' win share {P(Share)} | " +
                $"time to kill {(won > 0 ? F(ttk / won) + " s" : "-")} | per fight {F((double)thrown / Math.Max(1, fights))} thrown, {F((double)landed / Math.Max(1, fights))} landed | grenades landed per kill {(won > 0 ? F(PerKill, "0.00") : "-")}" +
                (name != null && name.StartsWith("push") ? $" | tanks destroyed {tanksDown} of {3 * fights} ({F((double)tanksDown / Math.Max(1, fights), "0.00")} a fight)" : "");
        }

        /// <summary>Grenades landed per kill over the worlds BOTH rows won (the same seeds, the same order): (before, after, worlds).</summary>
        static (double before, double after, int worlds) Paired(Row before, Row after)
        {
            long b = 0, a = 0; int n = 0;
            for (int i = 0; i < before.perFight.Count && i < after.perFight.Count; i++)
                if (before.perFight[i] >= 0 && after.perFight[i] >= 0) { b += before.perFight[i]; a += after.perFight[i]; n++; }
            return n > 0 ? ((double)b / n, (double)a / n, n) : (0, 0, 0);
        }

        public static Row Many(string name, string kind, string enemy, int squads, int step, int[] dists)
        {
            var r = new Row { name = name, step = step };
            int keep = ZWorld.GrenadeStrongerPct; ZWorld.GrenadeStrongerPct = step;
            try
            {
                foreach (int d in dists)
                    for (int n = 1; n <= Seeds; n++)
                    {
                        var f = One(kind, enemy, squads, d, OzV11GrenadeCount.Seed(n));
                        r.fights++; r.thrown += f.thrown; r.landed += f.landed; r.tanksDown += f.tanksDown;
                        r.perFight.Add(f.decided && f.nadesWon ? f.landed : -1);
                        if (!f.decided) { r.draws++; continue; }
                        if (f.nadesWon) { r.won++; r.ttk += f.ttk; r.landedAtKill += f.landed; } else r.lost++;
                    }
            }
            finally { ZWorld.GrenadeStrongerPct = keep; }
            return r;
        }

        // ================================================================== 1 + 2: the rules and the landing points
        sealed class Throw { public int damage = -1, area = -1, radius = -1; public double gap = -1, flight = -1; }

        /// <summary>One grunt with grenades at a light tank of the CPU that cannot move, shoot or die, 100 px away: the first grenade's numbers
        /// (the missile itself), the flight and the time between his first two throws.</summary>
        static Throw RealThrow(int seed)
        {
            var w = NewWorld(seed);
            var t = OzV11Kit.Place(w, "light", CX + 100, CY, Team.Blue);
            t.MoveSpeed = 0; t.RealMoveSpeed = 0; t.Damage = 0; t.MaxHealth = Big; t.SetHealth(Big);
            var g = OzV11Kit.Place(w, "grunt", CX, CY, Team.Red, 1); g.Grenades = 10;
            OzV11Kit.PlayerAttack(w, g, t);
            w.Events.Clear();
            var r = new Throw(); double t1 = -1;
            for (int i = 0; i < 3000 && r.gap < 0; i++)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                {
                    if (e.type != ZEventType.MissileFired || !e.flag || e.objId != g.Id) continue;
                    if (t1 < 0)
                    {
                        t1 = w.Time; r.flight = e.value;
                        var m = w.PendingMissiles.Concat(w.QueuedMissiles).FirstOrDefault(x => x.grenade && x.attackerId == g.Id);
                        if (m != null) { r.damage = m.damage; r.radius = m.radius; r.area = m.areaDamage > 0 ? m.areaDamage : w.ThrownGrenadeAreaDamage; }
                    }
                    else { r.gap = w.Time - t1; break; }
                }
                w.Events.Clear();
            }
            return r;
        }

        /// <summary>Every landing point of a throw aimed at o's centre (Rng.Next(48) - 24 on both axes, Sim/ZObject.cs ProcessAttackDamage):
        /// HP by the game's rule (ZWorld.GrenadeHit, ProcessMissileDamage's float ratio). zod = Zod's grenade at the same point.</summary>
        static int At(ZObject o, int dx, int dy, int direct, int area, int radius, bool zod)
        {
            if (Math.Abs(dx) > radius || Math.Abs(dy) > radius) return 0;
            float mag = (float)Math.Sqrt((float)(dx * dx + dy * dy));
            if (mag >= radius) return 0;
            float ratio = (float)(mag / radius);
            return zod ? (int)(direct * (1.0 - ratio)) : ZWorld.GrenadeHit(direct, area, o.CenterX + dx, o.CenterY + dy, o, ratio);
        }

        static void Landings(StringBuilder sb, int zodD, int zodR)
        {
            var w = NewWorld(1);
            var bodies = new (string what, ZObject o)[] { ("a robot (grunt)", OzV11Kit.Place(w, "grunt", CX, CY, Team.Blue, 1)), ("a tank (light)", OzV11Kit.Place(w, "light", CX + 300, CY, Team.Blue)) };
            int psyHp = OzV11Kit.Place(w, "psycho", CX - 300, CY, Team.Blue, 1).MaxHealth, gruntHp = bodies[0].o.MaxHealth;
            bool was = ZWorld.GrenadeBoostDeviation; ZWorld.GrenadeBoostDeviation = true;
            int keep = ZWorld.GrenadeStrongerPct;
            try
            {
                foreach (int step in Steps)
                {
                    ZWorld.GrenadeStrongerPct = step;
                    int d = w.ThrownGrenadeDamage, a = w.ThrownGrenadeAreaDamage, r = w.ThrownGrenadeRadius;
                    var parts = new List<string>(); int belowAll = 0; double kg = 0, kp = 0;
                    foreach (var (what, o) in bodies)
                    {
                        int below = 0, worst = 0; long sum = 0, zsum = 0;
                        for (int dx = -24; dx < 24; dx++)
                            for (int dy = -24; dy < 24; dy++)
                            {
                                int k = At(o, dx, dy, d, a, r, false), z = At(o, dx, dy, zodD, zodD, zodR, true);
                                sum += k; zsum += z; if (k < z) { below++; worst = Math.Max(worst, z - k); }
                                if (o == bodies[0].o) { if (k >= gruntHp) kg++; if (k >= psyHp) kp++; }
                            }
                        belowAll += below;
                        parts.Add($"{what}: {F(sum / 2304.0)} HP a grenade (Zod's {F(zsum / 2304.0)}), {below} of 2304 landing points below Zod's{(below > 0 ? " (at most " + worst + " HP)" : "")}");
                    }
                    kg /= 2304.0; kp /= 2304.0;
                    string line = $"x{step / 100.0:0.00} (direct {d}, area {a}, {r} px): {string.Join("; ", parts)}; one grenade fells a fresh grunt {P(kg)}, a fresh psycho {P(kp)}";
                    if (step == ZWorld.ShippedGrenadeStrongerPct)
                    {
                        sb.AppendLine(OzV11Kit.Verdict(belowAll == 0, "no landing point of the shipped grenade does less than Zod's grenade: " + line));
                        sb.AppendLine(OzV11Kit.Verdict(kg <= GruntOneShotCap && kp <= PsychoOneShotCap, $"not overpowered against robots, one grenade: fells a fresh grunt {P(kg)} (at most {P(GruntOneShotCap)}), a fresh psycho / sniper {P(kp)} (at most {P(PsychoOneShotCap)})"));
                    }
                    else sb.AppendLine("INFO  " + line);
                }
                // a tank that stands, over every landing point, by the game's own rule (ZWorld.GrenadeHit) on the tanks' own boxes and health
                var tankObjs = Tanks.Select((t, i) => OzV11Kit.Place(w, t, CX + 300, CY + 200 + 100 * i, Team.Blue)).ToList();
                double[] MeanAt(int step)
                {
                    ZWorld.GrenadeStrongerPct = step;
                    int dd = w.ThrownGrenadeDamage, aa = w.ThrownGrenadeAreaDamage, rr = w.ThrownGrenadeRadius;
                    return tankObjs.Select(tk =>
                    {
                        var v = new int[2304]; int n = 0;
                        for (int dx = -24; dx < 24; dx++) for (int dy = -24; dy < 24; dy++) v[n++] = At(tk, dx, dy, dd, aa, rr, false);
                        return MeanToKill(v, tk.MaxHealth);
                    }).ToArray();
                }
                double[] mb = MeanAt(100), ms = MeanAt(ZWorld.ShippedGrenadeStrongerPct);
                bool aOk = Enumerable.Range(0, Tanks.Length).All(i => mb[i] > 0 && ms[i] > 0 && ms[i] <= mb[i] * (1 - MinFewerGrenadesPerKill));
                sb.AppendLine(OzV11Kit.Verdict(aOk, $"clearly better against tanks (exact, every landing point): grenades to destroy a tank that stands at x{ZWorld.ShippedGrenadeStrongerPct / 100.0:0.00}: " +
                    string.Join(", ", Enumerable.Range(0, Tanks.Length).Select(i => $"{Tanks[i]} ({tankObjs[i].MaxHealth} HP) {F(ms[i], "0.00")} (1.2: {F(mb[i], "0.00")}, {(mb[i] > 0 ? F(100 * (1 - ms[i] / mb[i])) : "-")} % fewer)")) +
                    $" (at least {P(MinFewerGrenadesPerKill)} fewer for each; tools/step_scan.py 11.3 / 12.2 / 12.3 %)"));
            }
            finally { ZWorld.GrenadeStrongerPct = keep; ZWorld.GrenadeBoostDeviation = was; }
        }

        /// <summary>Exact mean number of grenades until the health hp is gone, every landing in vals equally likely (E[N] = the sum over k of
        /// P(N > k), by convolution of the capped damage; the same sum as tools/grenade_model.py grenades_to_kill).</summary>
        static double MeanToKill(int[] vals, int hp)
        {
            if (hp <= 0 || vals.Length == 0 || vals.All(x => x <= 0)) return 0;
            var hist = new Dictionary<int, int>();
            foreach (int x in vals) { int c = Math.Min(x, hp); hist[c] = hist.TryGetValue(c, out int k) ? k + 1 : 1; }
            int[] vs = hist.Keys.ToArray(); double[] ps = vs.Select(x => hist[x] / (double)vals.Length).ToArray();
            var dist = new double[hp]; dist[0] = 1; double alive = 1, mean = 0;
            for (int g = 0; g < 1000 && alive > 1e-9; g++)
            {
                mean += alive;                                                                   // P(N > g)
                var nd = new double[hp]; alive = 0;
                for (int s = 0; s < hp; s++)
                {
                    double p = dist[s]; if (p == 0) continue;
                    for (int j = 0; j < vs.Length; j++) { int t = s + vs[j]; if (t < hp) nd[t] += p * ps[j]; }
                }
                for (int s = 0; s < hp; s++) alive += nd[s];
                dist = nd;
            }
            return mean;
        }

        static void Rules(StringBuilder sb)
        {
            var s = ZSettings.Parse(Settings);
            int zd = (int)(s.grenade_damage * ZConst.MAX_UNIT_HEALTH), zr = s.grenade_damage_radius, shipped = ZWorld.ShippedGrenadeStrongerPct;
            int wantD = ZWorld.GrenadeStronger((zd * ZWorld.GrenadeBoostDamagePct + 50) / 100), wantA = ZWorld.GrenadeStronger((zd * ZWorld.GrenadeAreaDamagePct + 50) / 100);
            int wantR = (zr * ZWorld.GrenadeBoostRadiusPct + 50) / 100;
            sb.AppendLine(OzV11Kit.Verdict(ZWorld.GrenadeStrongerPct == shipped && shipped >= 110 && shipped <= 120 && zd == 1666 && zr == 30 && Math.Abs(s.grenade_attack_speed - 2.254) < 1e-9 && s.grenade_missile_speed == 40 && s.grenades_per_box == 20,
                $"rules: DV160 = x{shipped / 100.0:0.00} on DV104 (in use x{ZWorld.GrenadeStrongerPct / 100.0:0.00}; 'slightly' = at most x1.20); the settings keep Zod's grenade: {zd} HP, {zr} px, {F(s.grenade_attack_speed, "0.000")} s between throws, {s.grenade_missile_speed} px/s, {s.grenades_per_box} a box"));
            Throw on, before, zod;
            bool was = ZWorld.GrenadeBoostDeviation; int keep = ZWorld.GrenadeStrongerPct;
            try
            {
                ZWorld.GrenadeBoostDeviation = true; on = RealThrow(3);
                ZWorld.GrenadeStrongerPct = 100; before = RealThrow(3);
                ZWorld.GrenadeBoostDeviation = false; ZWorld.GrenadeStrongerPct = shipped; zod = RealThrow(3);
            }
            finally { ZWorld.GrenadeBoostDeviation = was; ZWorld.GrenadeStrongerPct = keep; }
            sb.AppendLine(OzV11Kit.Verdict(on.damage == wantD && on.area == wantA && on.radius == wantR && before.damage == (zd * ZWorld.GrenadeBoostDamagePct + 50) / 100 && before.radius == wantR,
                $"a real thrown grenade: direct {on.damage} HP, area {on.area} HP, radius {on.radius} px (wanted {wantD} / {wantA} / {wantR}; the 1.2 grenade {before.damage} / {before.area} / {before.radius})"));
            sb.AppendLine(OzV11Kit.Verdict(on.gap > 0 && Math.Abs(on.gap - before.gap) < 1e-9 && Math.Abs(on.flight - before.flight) < 1e-6,
                $"the throw is unchanged: {F(on.gap, "0.000")} s between throws (1.2: {F(before.gap, "0.000")} s), flight {F(on.flight, "0.00")} s at 100 px (1.2: {F(before.flight, "0.00")} s)"));
            sb.AppendLine(OzV11Kit.Verdict(zod.damage == zd && zod.area == zd && zod.radius == zr,
                $"DV104 off ('Zod exact', oz_grenade_boost = 0): Zod's grenade, {zod.damage} / {zod.area} HP, {zod.radius} px, at any DV160 step"));
            Landings(sb, zd, zr);
        }

        // ================================================================== the whole check
        /// <summary>fights = false: only parts 1 and 2 (seconds; the parity row). true: everything (minutes).</summary>
        public static string Run(bool fights)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DV160 thrown grenades x1.15 on top of DV104, counted headless");
            using (OzV11Kit.GameSwitches())
            {
                try
                {
                    OzV12MechHpCount.SetPlaySwitches();
                    ZWorld.MTankAimDeviation = true; ZWorld.MTankHurryDeviation = true;           // the medium tank's own rules, as ZGame sets them with DV145
                    ZWorld.GrenadeBoostDeviation = true;
                    sb.AppendLine("## 1-2 the rules and the landing points");
                    Rules(sb);
                    if (!fights) return sb.ToString();
                    int shipped = ZWorld.ShippedGrenadeStrongerPct;
                    sb.AppendLine($"## 3-5 fights on open flat ground, {Seeds * Distances.Length} a row (start {string.Join(" / ", Distances)} px, worlds 1..{Seeds}), at most {MaxSeconds:0} s each; the same worlds in every row");
                    var all = new Dictionary<(string, int), Row>();
                    void Table(string title, string key, string kind, string enemy, int squads, int[] dists)
                    {
                        sb.AppendLine(title);
                        foreach (int st in Steps) { var r = Many(key, kind, enemy, squads, st, dists); all[(key, st)] = r; sb.AppendLine(r.Text()); }
                    }
                    foreach (var t in Tanks) Table($"3a. two grunt squads with a box -> a {t} of the CPU that stands and fires back:", "stand " + t, "stand", t, 2, Distances);
                    foreach (var t in Tanks) Table($"3b. the PLAYER's {t} on an attack order <- two grunt squads of the CPU with a box:", "ordered " + t, "ordered", t, 2, Distances);
                    foreach (var t in Tanks) Table($"3c. three grunt squads with a box -> a {t} of the CPU free to move (it steps aside):", "free " + t, "free", t, 3, Distances);
                    foreach (var r in Robots) Table($"4. one grunt squad with a box -> a {r} squad without (it fires back, free to move):", "robot " + r, "robot", r, 1, Distances);
                    Table("5a. the CPU's light, medium and heavy tank on an attack-move through three grunt squads of the player with a box each (win = all three destroyed):", "pushCpu", "pushCpu", null, 3, new[] { 360 });
                    Table("5b. the player's light, medium and heavy tank on attack orders <- three grunt squads of the CPU with a box each (win = all three destroyed):", "pushPlayer", "pushPlayer", null, 3, new[] { 220 });

                    Row R(string k, int st) => all[(k, st)];
                    // 3: clearly better against tanks (the exact line is in part 2; here the fights, paired over the worlds both steps won)
                    var cut = Tanks.Select(t => { var p = Paired(R("stand " + t, 100), R("stand " + t, shipped)); return (t, b: p.before, a: p.after, n: p.worlds); }).ToList();
                    bool cutOk = cut.All(c => c.n >= MinPairedWorlds && c.b > 0 && c.a > 0 && c.a <= c.b * (1 - MinFewerGrenadesPerKillFights));
                    sb.AppendLine(OzV11Kit.Verdict(cutOk, "against a tank that stands and fires back, grenades landed per kill at x" + F(shipped / 100.0, "0.00") + " vs the 1.2 grenade, over the worlds both won: " +
                        string.Join(", ", cut.Select(c => $"{c.t} {F(c.a, "0.00")} (1.2: {F(c.b, "0.00")}, {(c.b > 0 ? F(100 * (1 - c.a / c.b)) : "-")} % fewer, {c.n} worlds)")) +
                        $" (at least {P(MinFewerGrenadesPerKillFights)} fewer for each, at least {MinPairedWorlds} worlds; all wins, unpaired: " +
                        string.Join(", ", Tanks.Select(t => $"{t} {F(R("stand " + t, shipped).PerKill, "0.00")} / 1.2 {F(R("stand " + t, 100).PerKill, "0.00")}")) + ")"));
                    double gain = Tanks.Sum(t => R("ordered " + t, shipped).Share - R("ordered " + t, 100).Share);
                    int noise = Math.Max(3, Seeds * Distances.Length / 10);
                    bool noneWorse = Tanks.All(t => R("ordered " + t, shipped).won >= R("ordered " + t, 100).won - noise) && Tanks.All(t => R("stand " + t, shipped).won >= R("stand " + t, 100).won - noise);
                    sb.AppendLine(OzV11Kit.Verdict(gain >= MinTankShareGain && noneWorse,
                        $"the player's tank on an attack order against the CPU's grenadiers: their win share rises by {F(100 * gain)} points over the three tanks (at least {F(100 * MinTankShareGain)}): " +
                        string.Join(", ", Tanks.Select(t => $"{t} {P(R("ordered " + t, shipped).Share)} (1.2: {P(R("ordered " + t, 100).Share)})")) + $"; no tank row lost by more than {noise} fights"));
                    // 4: not overpowered against robots
                    double rgain = Robots.Sum(r => R("robot " + r, shipped).Share - R("robot " + r, 100).Share);
                    double worst = Robots.Max(r => R("robot " + r, shipped).Share - R("robot " + r, 100).Share);
                    sb.AppendLine(OzV11Kit.Verdict(worst <= MaxRobotRowGain && rgain <= Math.Max(gain, 0) + 1e-9,
                        $"not overpowered against robots: a grunt squad with a box wins " + string.Join(", ", Robots.Select(r => $"vs {r}s {P(R("robot " + r, shipped).Share)} (1.2: {P(R("robot " + r, 100).Share)})")) +
                        $"; the most one row rises {F(100 * worst)} points (at most {F(100 * MaxRobotRowGain)}), the robot rows together {F(100 * rgain)} points, the tank rows of 3b {F(100 * gain)}"));
                    // the tanks destroyed (a push slowed by one or two lost tanks), so the row says something even when no push is halted at either step
                    foreach (var k in new[] { "pushCpu", "pushPlayer" })
                    {
                        Row a5 = R(k, shipped), b5 = R(k, 100);
                        sb.AppendLine(OzV11Kit.Verdict(a5.won >= b5.won - noise && a5.tanksDown >= b5.tanksDown - noise,
                            $"{(k == "pushCpu" ? "the CPU's tank push (attack-move, it steps aside)" : "the player's tank push (attack orders, no stepping aside)")} halted in {a5.won} of {a5.fights} (1.2: {b5.won}); tanks destroyed {a5.tanksDown} of {3 * a5.fights} (1.2: {b5.tanksDown}); " +
                            $"grenades landed per fight {F((double)a5.landed / Math.Max(1, a5.fights))} of {F((double)a5.thrown / Math.Max(1, a5.fights))} thrown"));
                        if (a5.won == 0 && b5.won == 0) sb.AppendLine($"INFO  {k}: no push halted at x1.00 or x{shipped / 100.0:0.00}; the row's verdict rests on the tanks destroyed");
                    }
                    // INFO: the other steps
                    var hold = Steps.Where(st => st != 100).Where(st =>
                        Tanks.All(t => { var p = Paired(R("stand " + t, 100), R("stand " + t, st)); return p.worlds >= MinPairedWorlds && p.before > 0 && p.after <= p.before * (1 - MinFewerGrenadesPerKillFights); })
                        && Robots.Max(r => R("robot " + r, st).Share - R("robot " + r, 100).Share) <= MaxRobotRowGain).ToList();
                    sb.AppendLine("INFO  steps that hold the tank line (3a) and the robot line (4): " + (hold.Count > 0 ? "x" + string.Join(", x", hold.Select(st => F(st / 100.0, "0.00"))) : "none"));
                    // 6: deterministic
                    int keep = ZWorld.GrenadeStrongerPct; ZWorld.GrenadeStrongerPct = shipped;
                    try
                    {
                        var a1 = One("ordered", "light", 2, 110, OzV11GrenadeCount.Seed(7)); var a2 = One("ordered", "light", 2, 110, OzV11GrenadeCount.Seed(7));
                        sb.AppendLine(OzV11Kit.Verdict(a1.decided == a2.decided && a1.nadesWon == a2.nadesWon && a1.thrown == a2.thrown && a1.landed == a2.landed && Math.Abs(a1.ttk - a2.ttk) < 1e-9,
                            $"the same fight twice gives the same result (world 7: {(a1.decided ? a1.nadesWon ? "grenadiers" : "tank" : "draw")}, {a1.thrown} thrown, {a1.landed} landed, {F(a1.ttk, "0.00")} s)"));
                    }
                    finally { ZWorld.GrenadeStrongerPct = keep; }
                }
                catch (Exception ex) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + ex.GetType().Name + ": " + ex.Message)); }
            }
            return sb.ToString();
        }
    }
}
#endif
