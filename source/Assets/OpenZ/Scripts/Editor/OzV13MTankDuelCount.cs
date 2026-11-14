#if !OPENZ_HARNESS
// OpenZ (GPL-3.0) - version 1.3, owner item 14 (DV156, Sim/ZMTank.cs)
// The counted check, headless (the simulation only: no scene, no Play mode, nothing saved). One medium tank against one heavy tank (and, the
// second table, against one light tank) on open flat ground, many fights: start distance 100 / 140 / 180 / 240 px, the line between them at
// 0 / 45 / 90 / 135 degrees, the medium tank red or blue (teams swapped), both on the player's ATTACK order at each other or both on an
// attack-move at each other, seeds 1..3 = 192 fights per row. The game's switches (OzV12MechHpCount.SetPlaySwitches, the medium tank's own
// rules DV150 / DV151 on, as ZGame sets them). The medium tank's reload is set per row: the light tank's x 1.00 (1.2), 1.10, 1.15 (the shipped
// DV156 value) and, for information, x 1.20 / 1.25. Counted per row: medium wins / other wins / draws (nobody dead after MaxSeconds), the win
// share, the average time to kill (first shell of the fight to the kill), the winner's health left and the medium tank's shells per fight.
// Every switch touched is put back afterwards.
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
    public static class OzV13MTankDuelCount
    {
        const int CX = 1024, CY = 1024;
        public static int[] Seeds = { 1, 2, 3 };
        /// <summary>Start distance between the two centres (px): inside both guns' range (100), between the medium's 120 and the heavy's 144
        /// (140), and two approaches from further out.</summary>
        public static int[] Distances = { 100, 140, 180, 240 };
        public static int[] Angles = { 0, 45, 90, 135 };
        public static double MaxSeconds = 90;
        /// <summary>The rows: the medium tank's reload as a multiple of the light tank's. The first three are the verdict rows.</summary>
        public static double[] Factors = { 1.00, 1.10, 1.15, 1.20, 1.25 };
        /// <summary>Pass lines: against the heavy tank the medium tank wins at most this share of the decided fights (the heavy's clear
        /// majority = at least 65 %); against the light tank it still wins at least this share.</summary>
        public const double MaxShareVsHeavy = 0.35, MinShareVsLight = 0.60;

        static string settings, flat;
        static string Settings => settings ??= OzV11Kit.SettingsText;
        static string Flat => flat ??= OzV11Kit.Plain(128, 128);

        [MenuItem("OpenZ/V1.3/Medium Tank reload: 1 vs 1 duels, counted (headless, DV156)")]
        static void Menu()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.3] medium tank duels: leave Play mode first (the check sets the simulation's static switches)."); return; }
            Debug.Log("[OpenZ V1.3] medium tank duels (DV156)\n" + Run());
        }

        [MenuItem("OpenZ/V1.3/Medium Tank reload: 1 vs 1 duels, quick (seed 1 only)")]
        static void MenuQuick()
        {
            if (Application.isPlaying) { Debug.LogWarning("[OpenZ V1.3] medium tank duels: leave Play mode first."); return; }
            var keep = Seeds; Seeds = new[] { 1 };
            try { Debug.Log("[OpenZ V1.3] medium tank duels, quick (DV156)\n" + Run()); } finally { Seeds = keep; }
        }

        public struct Fight { public bool decided, mWon; public double ttk, hpLeftPct; public int mShells; }

        /// <summary>One fight: the medium tank (reload = the light tank's x factor) against one `enemy` vehicle.</summary>
        public static Fight One(string enemy, double factor, int seed, int dist, int angleDeg, bool mediumRed, bool attackMove)
        {
            var w = OzV11Kit.World(Settings, Flat, seed);                                   // blue: a CPU team that gives no orders of its own
            var red0 = w.CreateRobotGroup("grunt", 48, 48, Team.Red, 1);                    // two far, still, harmless keepers: the game is never over
            red0.MoveSpeed = 0; red0.RealMoveSpeed = 0; red0.Damage = 0; red0.MaxHealth = red0.Health = 1000000000;
            OzV11Kit.Dummy(w, 2000, 2000);
            double a = angleDeg * Math.PI / 180.0, hx = Math.Cos(a) * dist * 0.5, hy = Math.Sin(a) * dist * 0.5;
            Team mt = mediumRed ? Team.Red : Team.Blue, et = mediumRed ? Team.Blue : Team.Red;
            var m = OzV11Kit.Place(w, ZMTank.Type, (int)Math.Round(CX - hx), (int)Math.Round(CY - hy), mt);
            var e = OzV11Kit.Place(w, enemy, (int)Math.Round(CX + hx), (int)Math.Round(CY + hy), et);
            var light = w.S.Unit(ZMTank.Gun);
            m.AttackSpeed = Math.Round(light.attack_speed * factor, 6);                     // the row under test (the shipped row is checked on its own)
            if (attackMove) { OzV11Kit.PlayerMove(w, m, e.CenterX, e.CenterY, true); OzV11Kit.PlayerMove(w, e, m.CenterX, m.CenterY, true); }
            else { OzV11Kit.PlayerAttack(w, m, e); OzV11Kit.PlayerAttack(w, e, m); }
            w.Events.Clear();
            var f = new Fight();
            double first = -1, end = w.Time + MaxSeconds;
            while (w.Time < end)
            {
                w.Tick(ZConst.TICK);
                foreach (var ev in w.Events)
                {
                    if (ev.type != ZEventType.MissileFired && ev.type != ZEventType.Hitscan) continue;
                    if (ev.objId != m.Id && ev.objId != e.Id) continue;
                    if (first < 0) first = w.Time;
                    if (ev.objId == m.Id && ev.type == ZEventType.MissileFired) f.mShells++;
                }
                w.Events.Clear();
                bool mOut = Out(m), eOut = Out(e);
                if (!mOut && !eOut) continue;
                if (mOut && eOut) return f;                                                  // both in the same tick: not decided
                f.decided = true; f.mWon = eOut;
                var winner = f.mWon ? m : e;
                f.ttk = first >= 0 ? w.Time - first : 0;
                f.hpLeftPct = winner.MaxHealth > 0 ? 100.0 * winner.Health / winner.MaxHealth : 0;
                return f;
            }
            return f;
        }

        static bool Out(ZObject o) => !OzV11Kit.Alive(o) || o.Owner == Team.Neutral;

        public sealed class Row { public double factor; public int fights, mWins, oWins, draws, shells; public double ttk, hpLeft; public double Share => mWins + oWins > 0 ? (double)mWins / (mWins + oWins) : 0; }

        public static Row Many(string enemy, double factor)
        {
            var r = new Row { factor = factor };
            foreach (int seed in Seeds)
                foreach (int d in Distances)
                    foreach (int ang in Angles)
                        foreach (bool red in new[] { true, false })
                            foreach (bool move in new[] { false, true })
                            {
                                var f = One(enemy, factor, seed, d, ang, red, move);
                                r.fights++; r.shells += f.mShells;
                                if (!f.decided) { r.draws++; continue; }
                                if (f.mWon) r.mWins++; else r.oWins++;
                                r.ttk += f.ttk; r.hpLeft += f.hpLeftPct;
                            }
            int dec = r.mWins + r.oWins;
            if (dec > 0) { r.ttk /= dec; r.hpLeft /= dec; }
            return r;
        }

        static string N(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
        static string Line(string enemy, Row r, double light) =>
            $"  x{r.factor:0.00} (reload {N(Math.Round(light * r.factor, 6))} s): medium wins {r.mWins,3}, {enemy} wins {r.oWins,3}, draws {r.draws,3} of {r.fights} | medium win share {100.0 * r.Share:0.0} % | " +
            $"average time to kill {r.ttk:0.0} s | the winner's health left {r.hpLeft:0.0} % | medium tank shells per fight {(r.fights > 0 ? (double)r.shells / r.fights : 0):0.0}";

        public static string Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DV156 the medium tank's own reload, counted headless");
            using (OzV11Kit.GameSwitches())
            {
                try
                {
                    OzV12MechHpCount.SetPlaySwitches();
                    ZWorld.MTankAimDeviation = true; ZWorld.MTankHurryDeviation = true;           // the medium tank's own rules, as ZGame sets them with DV145
                    // ---- 1. the rules row
                    var s = ZSettings.Parse(Settings);
                    var u = s.Unit(ZMTank.Type); var light = s.Unit(ZMTank.Gun); var heavy = s.Unit("heavy"); var mech = s.Unit(ZMTank.Mech);
                    double want = ZMTank.Reload(light.attack_speed);
                    sb.AppendLine(OzV11Kit.Verdict(Math.Abs(u.attack_speed - want) < 1e-9 && ZMTank.ReloadFactor >= 1.10 - 1e-9 && ZMTank.ReloadFactor <= 1.15 + 1e-9,
                        $"rules row: unit.mtank.attack_speed {N(u.attack_speed)} s = the light tank's {N(light.attack_speed)} s x {N(ZMTank.ReloadFactor)} (the owner's band 1.10 .. 1.15); {100.0 * (1 - light.attack_speed / u.attack_speed):0.0} % fewer shots a minute"));
                    sb.AppendLine(OzV11Kit.Verdict(Math.Abs(light.attack_speed - 1.128) < 1e-9 && Math.Abs(heavy.attack_speed - 4.088) < 1e-9 && Math.Abs(mech.attack_speed - 2.336) < 1e-9,
                        $"the other tanks keep their reload: light {N(light.attack_speed)} s, heavy {N(heavy.attack_speed)} s, mech {N(mech.attack_speed)} s (Zod: 1.128 / 4.088 / 2.336)"));
                    var w0 = OzV11Kit.World(Settings, Flat, 1);
                    var placed = OzV11Kit.Place(w0, ZMTank.Type, CX, CY, Team.Red);
                    var placedLight = OzV11Kit.Place(w0, ZMTank.Gun, CX + 200, CY, Team.Red);
                    sb.AppendLine(OzV11Kit.Verdict(Math.Abs(placed.AttackSpeed - u.attack_speed) < 1e-9 && Math.Abs(placedLight.AttackSpeed - light.attack_speed) < 1e-9,
                        $"a placed medium tank fires every {N(placed.AttackSpeed)} s, a placed light tank every {N(placedLight.AttackSpeed)} s"));
                    // ---- 2. the duels
                    int perRow = Seeds.Length * Distances.Length * Angles.Length * 4;
                    sb.AppendLine($"## 1 vs 1 duels on open flat ground, {perRow} fights per row (start {string.Join(" / ", Distances)} px, angles {string.Join(" / ", Angles)} deg, teams swapped, attack order or attack-move, seeds {string.Join(",", Seeds)}), at most {MaxSeconds:0} s each");
                    var heavyRows = new List<Row>(); var lightRows = new List<Row>();
                    sb.AppendLine("medium tank vs HEAVY tank:");
                    foreach (var fct in Factors) { var r = Many("heavy", fct); heavyRows.Add(r); sb.AppendLine(Line("heavy", r, light.attack_speed)); }
                    sb.AppendLine("medium tank vs LIGHT tank:");
                    foreach (var fct in Factors.Where(x => x <= 1.15 + 1e-9)) { var r = Many("light", fct); lightRows.Add(r); sb.AppendLine(Line("light", r, light.attack_speed)); }
                    var hNow = heavyRows.First(r => Math.Abs(r.factor - ZMTank.ReloadFactor) < 1e-9);
                    var lNow = lightRows.First(r => Math.Abs(r.factor - ZMTank.ReloadFactor) < 1e-9);
                    var h0 = heavyRows.First(r => Math.Abs(r.factor - 1.0) < 1e-9);
                    sb.AppendLine(OzV11Kit.Verdict(hNow.Share <= MaxShareVsHeavy && hNow.mWins + hNow.oWins > 0,
                        $"against the heavy tank the medium tank wins {100.0 * hNow.Share:0.0} % of the decided fights at x{N(ZMTank.ReloadFactor)} (at most {100 * MaxShareVsHeavy:0} %: the heavy's clear majority; 1.2's x1.00: {100.0 * h0.Share:0.0} %)"));
                    sb.AppendLine(OzV11Kit.Verdict(lNow.Share >= MinShareVsLight,
                        $"against the light tank the medium tank still wins {100.0 * lNow.Share:0.0} % at x{N(ZMTank.ReloadFactor)} (at least {100 * MinShareVsLight:0} %)"));
                    var ok = heavyRows.Where(r => r.factor >= 1.10 - 1e-9 && r.factor <= 1.15 + 1e-9 && r.Share <= MaxShareVsHeavy)
                                      .Where(r => { var l = lightRows.FirstOrDefault(x => Math.Abs(x.factor - r.factor) < 1e-9); return l != null && l.Share >= MinShareVsLight; })
                                      .Select(r => r.factor).ToList();
                    sb.AppendLine(ok.Count > 0 ? $"INFO inside the band these hold both lines: x{string.Join(", x", ok.Select(N))} (the smallest is the gentlest change)"
                                               : "INFO no factor inside the band (x1.10 .. x1.15) holds both lines: ask the owner (the x1.20 / x1.25 rows above show what a bigger step does)");
                    // ---- 3. deterministic
                    var a1 = One("heavy", ZMTank.ReloadFactor, 7, 140, 45, true, false); var a2 = One("heavy", ZMTank.ReloadFactor, 7, 140, 45, true, false);
                    sb.AppendLine(OzV11Kit.Verdict(a1.decided == a2.decided && a1.mWon == a2.mWon && a1.mShells == a2.mShells && Math.Abs(a1.ttk - a2.ttk) < 1e-9,
                        $"the same fight twice gives the same result (seed 7: {(a1.decided ? a1.mWon ? "medium" : "heavy" : "draw")}, {a1.mShells} medium shells, {a1.ttk:0.00} s)"));
                }
                catch (Exception ex) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + ex.GetType().Name + ": " + ex.Message)); }
            }
            return sb.ToString();
        }
    }
}
#endif
