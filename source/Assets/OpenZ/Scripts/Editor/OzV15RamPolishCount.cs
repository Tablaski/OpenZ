using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit ram_polish: the counted check of DV188 (Sim/ZObject.RamPolish.cs), headless. Rows: no shot of the big tank at its ram / crush
    /// target from the start until it is dead, while it fires at a second enemy beside; no shot FROM the target after its fate is sealed; the
    /// drawn gap at the contact on every target type (head-on and side-on) for the Mammoth and the Bizon; the push (px) and the heave -> kill
    /// time; the sweep: enemy robots behind the target squashed, own robots there and robots beside / behind the Mammoth unharmed; the same
    /// run twice = the same fingerprint; DV188 off = no push, no drop.
    /// </summary>
    public static class OzV15RamPolishCount
    {
        public static string Quick(string settings) => Run(settings, false);

        public static readonly string[] Targets = { "light", "medium", ZMTank.Type, "heavy", "jeep", "apc", "missile_launcher", ZBigTanks.Bizon, ZBigTanks.Mammoth };

        public static string Run(string settings, bool full = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DV188 the ram polish, counted (Sim/ZObject.RamPolish.cs)");
            bool b0 = ZWorld.BizonDeviation, c0 = ZWorld.BizonCrushDeviation, m0 = ZWorld.MammothDeviation, r0 = ZWorld.MammothRamDeviation, p0 = ZWorld.RamPolishDeviation;
            try
            {
                ZWorld.BizonDeviation = ZWorld.BizonCrushDeviation = ZWorld.MammothDeviation = ZWorld.MammothRamDeviation = true;
                ZWorld.RamPolishDeviation = true;
                Fire(sb, settings, ZBigTanks.Mammoth); Fire(sb, settings, ZBigTanks.Bizon);
                foreach (var rammer in new[] { ZBigTanks.Mammoth, ZBigTanks.Bizon })
                    foreach (var t in Targets) foreach (bool side in new[] { false, true }) Gap(sb, settings, rammer, t, side);
                Sweep(sb, settings);
                string a = Print(settings, 41), b = Print(settings, 41);
                sb.AppendLine(OzV11Kit.Verdict(a == b, "the same ram scene twice (DV188 on): the same fingerprint " + a.GetHashCode().ToString("x8")));
                ZWorld.RamPolishDeviation = false;
                var off = Scene(settings, 41, ZBigTanks.Mammoth, "heavy", false, out _, out _, out _);
                sb.AppendLine(OzV11Kit.Verdict(off.RamPushes == 0 && off.RamFireDrops == 0 && off.RamDrawnTouches == 0, $"DV188 off: no push ({off.RamPushes}), no drop ({off.RamFireDrops}), the spacing contact ({off.RamDrawnTouches} drawn touches)"));
            }
            catch (Exception e) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace?.Split('\n').FirstOrDefault())); }
            finally { ZWorld.BizonDeviation = b0; ZWorld.BizonCrushDeviation = c0; ZWorld.MammothDeviation = m0; ZWorld.MammothRamDeviation = r0; ZWorld.RamPolishDeviation = p0; }
            return sb.ToString();
        }

        static ZObject Unit(ZWorld w, string type, int cx, int cy, Team t, float fx, float fy, bool armed)
        {
            var o = OzV11Kit.Place(w, type, cx, cy, t); o.FaceX = fx; o.FaceY = fy; if (!armed) o.Damage = 0;
            return o;
        }

        static bool Shot(ZEvent e) => e.type == ZEventType.MissileFired || e.type == ZEventType.Hitscan || e.type == ZEventType.DriverShot || e.type == ZEventType.MechHandGun;

        /// <summary>One ram scene: the rammer at (320, 384) facing +x, the target ahead (side = turned across), optional second enemy and robots.</summary>
        static ZWorld Scene(string settings, int seed, string rammer, string victim, bool side, out ZObject a, out ZObject v, out List<ZEvent> shots, Action<ZWorld, ZObject, ZObject> extra = null, int ticks = 900, bool armed = false)
        {
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(64, 48), seed);
            OzV11Kit.Dummy(w, 980, 730);
            a = Unit(w, rammer, 320, 384, Team.Red, 9f, 0f, armed);
            int r = ZPathing.VehicleRadiusOf(rammer) + ZPathing.VehicleRadiusOf(victim) + 20;
            v = Unit(w, victim, 320 + r, 384, Team.Blue, side ? 0f : -9f, side ? 9f : 0f, armed);
            extra?.Invoke(w, a, v);
            shots = new List<ZEvent>();
            for (int i = 0; i < ticks; i++)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events) if (Shot(e)) shots.Add(e);
                w.Events.Clear();
                if (!OzV11Kit.Alive(v) && !a.Ramming && i > 200) break;
            }
            return w;
        }

        static void Fire(StringBuilder sb, string settings, string rammer)
        {
            ZObject second = null; long startTick = -1, doomTick = -1, deadTick = -1; int atTarget = 0, atSecond = 0, fromTarget = 0;
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(64, 48), 51);
            OzV11Kit.Dummy(w, 980, 730);
            var a = Unit(w, rammer, 320, 384, Team.Red, 9f, 0f, true);
            int r = ZPathing.VehicleRadiusOf(rammer) + ZPathing.VehicleRadiusOf("heavy") + 20;
            var v = Unit(w, "heavy", 320 + r, 384, Team.Blue, -9f, 0f, true);
            second = Unit(w, "light", 320 + 30, 384 - 96, Team.Blue, 0f, 9f, false);   // beside (12 m), in range, harmless
            a.Engage(v);
            for (int i = 0; i < 1200; i++)
            {
                w.Tick(ZConst.TICK);
                long k = w.TickIndex;
                if (startTick < 0 && a.Ramming) startTick = k;
                if (doomTick < 0 && v.RamDoomBy >= 0) doomTick = k;
                foreach (var e in w.Events)
                {
                    if (!Shot(e)) continue;
                    if (e.objId == a.Id && e.otherId == v.Id && startTick >= 0 && deadTick < 0) atTarget++;
                    if (e.objId == a.Id && e.otherId == second.Id && startTick >= 0) atSecond++;
                    if (e.objId == v.Id && doomTick >= 0) fromTarget++;
                }
                w.Events.Clear();
                if (deadTick < 0 && !OzV11Kit.Alive(v)) deadTick = k;
                if (deadTick >= 0 && k > deadTick + 50) break;
            }
            sb.AppendLine(OzV11Kit.Verdict(startTick >= 0 && deadTick > 0 && atTarget == 0, $"{rammer} rams a heavy it was firing at: shots at it from the start (tick {startTick}) to its death (tick {deadTick}): {atTarget} (must be 0)"));
            sb.AppendLine(OzV11Kit.Verdict(atSecond > 0, $"{rammer}: the turret takes the next enemy (a light tank beside, in range) at once: {atSecond} shots at it after the start (drops {w.RamFireDrops})"));
            sb.AppendLine(OzV11Kit.Verdict(doomTick >= 0 && fromTarget == 0, $"{rammer}: shots FROM the rammed heavy after its fate is sealed (tick {doomTick}): {fromTarget} (must be 0)"));
        }

        static void Gap(StringBuilder sb, string settings, string rammer, string victim, bool side)
        {
            int gapAt = int.MinValue, push = -1, lift = -1, plan = -1; long heave = -1, kill = -1, hit = -1;
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(64, 48), 61);
            OzV11Kit.Dummy(w, 980, 730);
            var a = Unit(w, rammer, 320, 384, Team.Red, 9f, 0f, false);
            int r = ZPathing.VehicleRadiusOf(rammer) + ZPathing.VehicleRadiusOf(victim) + 20;
            var v = Unit(w, victim, 320 + r, 384, Team.Blue, side ? 0f : 9f, side ? 9f : 0f, false);   // head-on: turned away (no counter-ram duel)
            for (int i = 0; i < 1200; i++)
            {
                w.Tick(ZConst.TICK); w.Events.Clear();
                if (hit < 0 && v.RamDoomBy >= 0) { hit = w.TickIndex; gapAt = a.RamDrawnGap(v); }
                if (heave < 0 && v.RamHeaveTick >= 0) { heave = v.RamHeaveTick; push = v.RamPushDone; kill = v.RamDoomTick; lift = v.RamLiftDone; plan = v.RamLiftPlan; }
                if (hit >= 0 && !OzV11Kit.Alive(v)) break;
            }
            bool ok = hit >= 0 && gapAt <= 0 && gapAt >= -1;
            string what = $"{rammer} -> {victim} {(side ? "side-on" : "head-on")}: drawn gap at the contact {gapAt / 8.0:0.00} m (<= 0.10, >= -0.13)";
            if (rammer == ZBigTanks.Mammoth)
            {
                ok &= heave >= 0 && push == 0 && plan >= ZObject.RamLiftMinPx && plan <= ZObject.RamLiftMaxPx && lift >= plan && lift <= plan + 1 && kill - heave == ZObject.RamFlipTicks;   // 1.5 rework 14i: the lift
                what += $"; the hull never shoved ({push} px); the Mammoth's lift under it {lift / 8.0:0.00} m (plan {plan / 8.0:0.00} m = its drawn length along the ram {(ZObject.RamLiftOverPx >= 0 ? "+" : "")}{ZObject.RamLiftOverPx} px); heave -> kill {kill - heave} ticks";
            }
            sb.AppendLine(OzV11Kit.Verdict(ok, what));
        }

        static void Sweep(StringBuilder sb, string settings)
        {
            var robots = new List<(ZObject o, string where)>();
            var w = Scene(settings, 71, ZBigTanks.Mammoth, "heavy", true, out var a, out var v, out _, (w0, a0, v0) =>
            {
                int far = v0.CenterX + 17 + 10;                                          // side-on heavy: its far face 17 px beyond its centre, + 1.25 m
                robots.Add((OzV11Kit.Place(w0, "grunt", far, 384, Team.Blue, 1), "enemy behind"));
                robots.Add((OzV11Kit.Place(w0, "grunt", far + 8, 392, Team.Blue, 1), "enemy behind 2"));
                robots.Add((OzV11Kit.Place(w0, "grunt", far + 4, 372, Team.Red, 1), "own behind"));
                robots.Add((OzV11Kit.Place(w0, "grunt", v0.CenterX, 384 + 80, Team.Blue, 1), "enemy beside"));
                robots.Add((OzV11Kit.Place(w0, "grunt", 320 - 60, 384, Team.Blue, 1), "enemy behind the Mammoth"));
                foreach (var x in robots) x.o.Damage = 0;
            }, 1200);
            foreach (var (o, where) in robots)
            {
                bool dead = !OzV11Kit.Alive(o), sq = o.Squashed;
                bool want = where.StartsWith("enemy behind") && !where.Contains("Mammoth");
                sb.AppendLine(OzV11Kit.Verdict(want ? dead && sq : !dead, $"sweep: {where}: {(dead ? (sq ? "squashed" : "dead") : "alive")} (want {(want ? "squashed" : "alive")})"));
            }
            sb.AppendLine($"#   sweep kills {w.RamSweepKills}, pushes {w.RamPushes}, heaves {w.RamHeaves}");
        }

        static string Print(string settings, int seed)
        {
            var w = Scene(settings, seed, ZBigTanks.Mammoth, "heavy", true, out var a, out var v, out var shots, (w0, a0, v0) => { OzV11Kit.Place(w0, "grunt", v0.CenterX + 30, 384, Team.Blue, 1); }, 900, true);
            var s = new StringBuilder();
            foreach (var o in w.Objects) s.Append(o.Id).Append(':').Append(o.CenterX).Append(',').Append(o.CenterY).Append(',').Append(o.Health).Append(';');
            s.Append(shots.Count).Append('|').Append(w.Rng.Next(1 << 30));
            return s.ToString();
        }
    }
}
