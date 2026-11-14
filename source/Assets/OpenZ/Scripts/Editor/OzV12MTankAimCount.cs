#if !OPENZ_HARNESS
// OpenZ (GPL-3.0) - version 1.2, run 10 item 1e: the counted check of DV151 (Sim/ZObject.MTankAim.cs: the medium tank misses more often
// until it becomes a veteran). Headless sim on a flat map, no scene, no Play mode. One shooter (held in place, cannot die) fires at an enemy
// LIGHT TANK that drives up and down 90 px in front of it (medium range: 90-108 px of the 120 px gun range; it cannot die and does not
// fire back), Seconds of game time per run, Runs runs (seeds 1..Runs). Counted: shells fired by the shooter, hits (the target's health
// dropped). Medium tank: switch off (as before), switch on + not a veteran, switch on + a veteran; light and heavy tanks: switch off / on.
// Every switch touched is restored afterwards.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzV12MTankAimCount
    {
        const int CX = 640, CY = 640, Big = 1_000_000_000, Gap = 90, Swing = 60;
        public static double Seconds = 60;
        public static int Runs = 10;

        [MenuItem("OpenZ/V1.2/Count Medium Tank Aim (DV151)")]
        static void Menu() => Debug.Log(Report());

        static string settings, flat;
        static string Settings => settings ??= Resources.Load<TextAsset>("OpenZ/default_settings").text;
        static string Flat => flat ??= BuildFlat(96, 96);

        static string BuildFlat(int w, int h)
        {
            var sb = new StringBuilder("{\"file\":\"aim_flat\",\"planet\":\"desert\",\"width\":" + w + ",\"height\":" + h + ",\"classes\":[");
            sb.Append(string.Join(",", Enumerable.Repeat("0", w * h))).Append("],\"tiles\":[").Append(string.Join(",", Enumerable.Repeat("0", w * h)));
            sb.Append("],\"zones\":[{\"x\":0,\"y\":0,\"w\":" + w + ",\"h\":" + h + "}],\"objects\":[]}");
            return sb.ToString();
        }

        static ZObject Vehicle(ZWorld w, string t, int cx, int cy, Team team) => w.CreateObject("vehicle", t, cx - 16, cy - 16, team);

        static void Go(ZWorld w, ZObject o, int x, int y) =>
            w.SendWaypoints(o.Owner, o, new List<ZWaypoint> { new ZWaypoint { mode = WpMode.Move, x = x, y = y } });

        /// <summary>One run: (shells fired, hits).</summary>
        public static (int shells, int hits) One(string shooter, bool veteran, int seed)
        {
            var w = ZWorld.Create(Settings, Flat, seed);
            var keeper = w.CreateRobotGroup("grunt", 1400, 1400, Team.Red);               // a far, still red squad: the game stays on
            keeper.MoveSpeed = 0; keeper.RealMoveSpeed = 0; foreach (var m in keeper.Minions) { m.MoveSpeed = 0; m.RealMoveSpeed = 0; }
            var a = Vehicle(w, shooter, CX, CY, Team.Red);
            a.MaxHealth = a.Health = Big; a.MoveSpeed = 0; a.RealMoveSpeed = 0; a.Veteran = veteran;
            var t = Vehicle(w, "light", CX + Gap, CY - Swing, Team.Blue);
            t.MaxHealth = t.Health = Big; t.Damage = 0;
            w.Events.Clear();
            w.SendWaypoints(a.Owner, a, new List<ZWaypoint> { new ZWaypoint { mode = WpMode.Attack, refId = t.Id, x = t.CenterX, y = t.CenterY } });
            int goalY = CY + Swing; Go(w, t, CX + Gap, goalY);
            int shells = 0, hits = 0, hp = t.Health;
            double end = w.Time + Seconds;
            while (w.Time < end)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events) if (e.type == ZEventType.MissileFired && e.objId == a.Id) shells++;
                w.Events.Clear();
                if (t.Health < hp) hits++;
                t.Health = hp = Big;
                a.Health = Big;
                if (Mathf.Abs(t.CenterY - goalY) < 6 || t.Waypoints.Count == 0) { goalY = goalY > CY ? CY - Swing : CY + Swing; Go(w, t, CX + Gap, goalY); }
            }
            return (shells, hits);
        }

        static (int shells, int hits) Many(string shooter, bool aimRule, bool veteran)
        {
            bool keep = ZWorld.MTankAimDeviation;
            try
            {
                ZWorld.MTankAimDeviation = aimRule;
                int s = 0, h = 0;
                for (int seed = 1; seed <= Runs; seed++) { var r = One(shooter, veteran, seed); s += r.shells; h += r.hits; }
                return (s, h);
            }
            finally { ZWorld.MTankAimDeviation = keep; }
        }

        static string Row(string what, (int shells, int hits) r) =>
            $"{what,-44} shells {r.shells,5}  hits {r.hits,5}  hit rate {(r.shells > 0 ? 100.0 * r.hits / r.shells : 0):0.0} %";

        public static string Report(string factors = "")
        {
            var sb = new StringBuilder();
            double keepF = ZWorld.MTankGreenScatter;
            sb.AppendLine($"DV151 medium tank aim, headless: shooter held in place, an enemy light tank driving up and down {Gap} px in front ({Gap}-{(int)System.Math.Sqrt(Gap * Gap + Swing * Swing)} px; gun range 120 px), {Seconds:0} s x {Runs} runs (seeds 1..{Runs}); scatter factor x{ZWorld.MTankGreenScatter:0.00} for a medium tank that is not a veteran");
            try
            {
                var off = Many(ZMTank.Type, false, false); sb.AppendLine(Row("medium tank, not veteran, switch OFF (before)", off));
                var on = Many(ZMTank.Type, true, false); sb.AppendLine(Row("medium tank, not veteran, switch ON (now)", on));
                double rateOff = off.shells > 0 ? (double)off.hits / off.shells : 0, rateOn = on.shells > 0 ? (double)on.hits / on.shells : 0;
                sb.AppendLine($"  -> hits per shell {(rateOff > 0 ? 100.0 * (1 - rateOn / rateOff) : 0):0.0} % fewer than before");
                var vOff = Many(ZMTank.Type, false, true); sb.AppendLine(Row("medium tank, VETERAN, switch OFF (before)", vOff));
                var vOn = Many(ZMTank.Type, true, true); sb.AppendLine(Row("medium tank, VETERAN, switch ON (now)", vOn));
                sb.AppendLine($"  -> veteran identical: {(vOff == vOn ? "YES" : "NO")}");
                // one 60 s run each (the asked format)
                var one = One(ZMTank.Type, false, 1); bool k = ZWorld.MTankAimDeviation;
                ZWorld.MTankAimDeviation = true; var oneOn = One(ZMTank.Type, false, 1); var oneVet = One(ZMTank.Type, true, 1); ZWorld.MTankAimDeviation = k;
                sb.AppendLine($"  one {Seconds:0} s run (seed 1): not veteran before {one.shells} shells / {one.hits} hits, now {oneOn.shells} / {oneOn.hits}; veteran now {oneVet.shells} / {oneVet.hits}");
                foreach (var ty in new[] { "light", "heavy" })
                {
                    var a0 = Many(ty, false, false); var a1 = Many(ty, true, false);
                    sb.AppendLine(Row(ty + " tank, switch OFF", a0)); sb.AppendLine(Row(ty + " tank, switch ON", a1));
                    sb.AppendLine($"  -> {ty} identical: {(a0 == a1 ? "YES" : "NO")}");
                }
                foreach (var fs in (factors ?? "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    ZWorld.MTankGreenScatter = double.Parse(fs, System.Globalization.CultureInfo.InvariantCulture);
                    var r = Many(ZMTank.Type, true, false);
                    double rr = r.shells > 0 ? (double)r.hits / r.shells : 0;
                    sb.AppendLine(Row($"  sweep x{ZWorld.MTankGreenScatter:0.00}, not veteran", r) + $"  ({(rateOff > 0 ? 100.0 * (1 - rr / rateOff) : 0):0.0} % fewer)");
                }
            }
            finally { ZWorld.MTankGreenScatter = keepF; }
            return sb.ToString();
        }

        /// <summary>Run 11: one run with the shooter held, an enemy light tank STANDING or MOVING (up and down +-swing px) gap px in front.
        /// Counted: shells fired, shells that burst on the target's hull (its 32 x 32 px box), damage done (the blast's own falloff), and the
        /// older count (ticks its health dropped).</summary>
        public static long[] Run11One(bool veteran, int seed, int gap, int swing, bool moving)
        {
            var w = ZWorld.Create(Settings, Flat, seed);
            var keeper = w.CreateRobotGroup("grunt", 1400, 1400, Team.Red);
            keeper.MoveSpeed = 0; keeper.RealMoveSpeed = 0; foreach (var m in keeper.Minions) { m.MoveSpeed = 0; m.RealMoveSpeed = 0; }
            var a = Vehicle(w, ZMTank.Type, CX, CY, Team.Red);
            a.MaxHealth = a.Health = Big; a.MoveSpeed = 0; a.RealMoveSpeed = 0; a.Veteran = veteran;
            var t = Vehicle(w, "light", CX + gap, moving ? CY - swing : CY, Team.Blue);
            t.MaxHealth = t.Health = Big; t.Damage = 0;
            if (!moving) { t.MoveSpeed = 0; t.RealMoveSpeed = 0; }
            w.Events.Clear();
            w.SendWaypoints(a.Owner, a, new List<ZWaypoint> { new ZWaypoint { mode = WpMode.Attack, refId = t.Id, x = t.CenterX, y = t.CenterY } });
            int goalY = CY + swing; if (moving) Go(w, t, CX + gap, goalY);
            long shells = 0, hull = 0, dmg = 0, hpHits = 0; int hp = t.Health;
            double end = w.Time + Seconds;
            while (w.Time < end)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                {
                    if (e.type == ZEventType.MissileFired && e.objId == a.Id) shells++;
                    if (e.type == ZEventType.MissileExploded && e.objId == a.Id && System.Math.Abs(e.x - t.CenterX) <= t.WidthPix / 2 && System.Math.Abs(e.y - t.CenterY) <= t.HeightPix / 2) hull++;
                }
                w.Events.Clear();
                if (t.Health < hp) { hpHits++; dmg += hp - t.Health; }
                t.Health = hp = Big; a.Health = Big;
                if (moving && (Mathf.Abs(t.CenterY - goalY) < 6 || t.Waypoints.Count == 0)) { goalY = goalY > CY ? CY - swing : CY + swing; Go(w, t, CX + gap, goalY); }
            }
            return new[] { shells, hull, dmg, hpHits };
        }

        static long[] Run11Many(bool veteran, int gap, int swing, bool moving)
        {
            var s = new long[4];
            for (int seed = 1; seed <= Runs; seed++) { var r = Run11One(veteran, seed, gap, swing, moving); for (int j = 0; j < 4; j++) s[j] += r[j]; }
            return s;
        }

        [MenuItem("OpenZ/V1.2/Count Medium Tank Aim run 11 (DV151 slight)")]
        static void Run11Menu() => Debug.Log(Run11Report());

        /// <summary>Run 11 report: veteran against not veteran, standing and moving enemy light tank at 60 and 100 px, the game's switches
        /// (DV12 veterans and DV151 on, as ZGame sets them), Seconds x Runs each; the not-veteran also at x1.00 (before DV151) and x2.40 (run 10).</summary>
        public static string Run11Report()
        {
            var sb = new StringBuilder();
            bool keepA = ZWorld.MTankAimDeviation, keepV = ZWorld.VeteranDeviation; double keepF = ZWorld.MTankGreenScatter;
            double now = ZWorld.MTankGreenScatter;
            sb.AppendLine($"DV151 run 11, headless, the game's switches (veterans DV12 on, DV151 on): medium tank held in place, enemy light tank STANDING or MOVING (+-40 px up and down) at 60 / 100 px, {Seconds:0} s x {Runs} runs (seeds 1..{Runs}) each; scatter factor now x{now:0.00} for a medium tank that is not a veteran");
            sb.AppendLine("hull = shells that burst on the light tank's 32 x 32 px hull; damage = health its shells took (a near miss takes less); old count = ticks its health dropped (run 10's count)");
            bool all = true;
            try
            {
                ZWorld.MTankAimDeviation = true; ZWorld.VeteranDeviation = true;
                foreach (var (gap, moving) in new[] { (60, false), (100, false), (60, true), (100, true) })
                {
                    ZWorld.MTankGreenScatter = now;
                    var v = Run11Many(true, gap, 40, moving);
                    var g = Run11Many(false, gap, 40, moving);
                    ZWorld.MTankGreenScatter = 1.0; var g0 = Run11Many(false, gap, 40, moving);
                    ZWorld.MTankGreenScatter = 2.4; var g24 = Run11Many(false, gap, 40, moving);
                    ZWorld.MTankGreenScatter = now;
                    double P(long x, long y) => y > 0 ? 100.0 * x / y : 0;
                    double dm = P(g[2], v[2]), hh = P(g[1], v[1]); bool ok = moving ? hh < P(g0[1], v[1]) : hh >= 85.0 && hh <= 92.0; all &= ok;   // run 11 fix: the measure = shells on the hull (standing: 85-92 % of the veteran's; moving: below x1.00, which is already under 85 %)
                    sb.AppendLine($"{(moving ? "MOVING" : "STANDING")} light tank at {gap} px:");
                    sb.AppendLine($"  veteran          shells {v[0],4}  on hull {v[1],4}  damage {v[2],8}  old count {v[3],4}");
                    sb.AppendLine($"  not veteran x{now:0.00} shells {g[0],4}  on hull {g[1],4} ({hh:0.0} % of the veteran's) {(ok ? "PASS" : "FAIL")}  damage {g[2],8} ({dm:0.0} %)  old count {g[3],4} ({P(g[3], v[3]):0.0} %)");
                    sb.AppendLine($"  not veteran x1.00 (before DV151): on hull {P(g0[1], v[1]):0.0} %, damage {P(g0[2], v[2]):0.0} %;  x2.40 (run 10's factor): on hull {P(g24[1], v[1]):0.0} %, damage {P(g24[2], v[2]):0.0} %");
                }
                var l0 = Many("light", false, false); var l1 = Many("light", true, false);
                var h0 = Many("heavy", false, false); var h1 = Many("heavy", true, false);
                sb.AppendLine($"light tank identical with DV151 off / on: {(l0 == l1 ? "YES" : "NO")}; heavy tank identical: {(h0 == h1 ? "YES" : "NO")}");
                sb.AppendLine($"RESULT: {(all ? "PASS" : "FAIL")} (shells on the hull of a not-veteran: STANDING target 85-92 % of a veteran's; MOVING target fewer than at x1.00, which is already under 85 %)");
            }
            finally { ZWorld.MTankAimDeviation = keepA; ZWorld.VeteranDeviation = keepV; ZWorld.MTankGreenScatter = keepF; }
            return sb.ToString();
        }
    }
}
#endif
