using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// DV186's counted check (1.5, kit ram_throw_fix; Sim/ZWorld.RamDebris.cs). Headless: the simulation only (ZWorld.Create + Tick), no scene,
    /// nothing saved; the game's switches put back after. Every tank's gun is off (Damage 0). Every line a counted PASS / FAIL:
    ///  A. a Mammoth rams a heavy tank (5 seeds): the heavy's turret debris point lies 3-7 m from its centre, on the far side (along the ram),
    ///     never back toward the Mammoth; DV186 OFF, the same seeds: Zod's own spread (up to 37 m), shown for contrast;
    ///  B. a Bizon crushes a heavy and a field gun: the debris point lies within 3.2 m of the victim's centre;
    ///  C. a heavy tank killed by plain damage (no ram): its debris point is Zod's draw, the same with DV186 on and off;
    ///  D. the random numbers: after a ram's death the world's next random number is the same with DV186 on and off (nothing drawn more or less);
    ///  E. deterministic: case A twice, the same point.
    /// </summary>
    public static class OzV15RamDebrisCount
    {
        public static string Quick(string settings) => Run(settings, false);

        public static string Run(string settings, bool full = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DV186 a rammed / crushed vehicle's turret lands near its wreck, counted (Sim/ZWorld.RamDebris.cs)");
            bool b0 = ZWorld.BizonDeviation, c0 = ZWorld.BizonCrushDeviation, m0 = ZWorld.MammothDeviation, r0 = ZWorld.MammothRamDeviation, d0 = ZWorld.RamDebrisDeviation;
            try
            {
                ZWorld.BizonDeviation = ZWorld.BizonCrushDeviation = ZWorld.MammothDeviation = ZWorld.MammothRamDeviation = true;
                Rammed(sb, settings, full ? 5 : 2);
                Crushed(sb, settings);
                Plain(sb, settings);
                SameDraws(sb, settings);
                if (full) Twice(sb, settings);
            }
            catch (Exception e) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace?.Split('\n').FirstOrDefault())); }
            finally { ZWorld.BizonDeviation = b0; ZWorld.BizonCrushDeviation = c0; ZWorld.MammothDeviation = m0; ZWorld.MammothRamDeviation = r0; ZWorld.RamDebrisDeviation = d0; }
            return sb.ToString();
        }

        sealed class Run1 { public bool died; public int cx, cy, x = int.MinValue, y = int.MinValue, dirX, dirY; public long next; public bool crushed; }

        static ZObject Unit(ZWorld w, string type, int cx, int cy, Team t, float fx, float fy)
        {
            var o = OzV11Kit.Place(w, type, cx, cy, t); o.FaceX = fx; o.FaceY = fy; o.Damage = 0;
            return o;
        }

        /// <summary>A rammer (Mammoth / Bizon) facing east at a victim 20 px off its front; run until the victim's debris missile exists.</summary>
        static Run1 Ram(string settings, int seed, string rammer, string victim, bool dv)
        {
            ZWorld.RamDebrisDeviation = dv;
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(64, 48), seed);
            OzV11Kit.Dummy(w, 980, 730);
            var a = Unit(w, rammer, 320, 384, Team.Red, 9f, 0f);
            int r = ZPathing.VehicleRadiusOf(rammer) + (victim == "gun" ? ZPathing.CannonRadius : ZPathing.VehicleRadiusOf(victim)) + 20;
            var v = Unit(w, victim, 320 + r, 384, Team.Blue, 0f, 0f);
            var res = new Run1();
            for (int i = 0; i < 1500 && res.x == int.MinValue; i++)
            {
                if (OzV11Kit.Alive(v)) { res.cx = v.CenterX; res.cy = v.CenterY; res.dirX = v.RamDirX; res.dirY = v.RamDirY; res.crushed = v.RamCrushed; }
                w.Tick(ZConst.TICK); w.Events.Clear();
                var m = w.PendingMissiles.Concat(w.QueuedMissiles).FirstOrDefault(q => q.debris && q.attackerId == v.Id);
                if (m != null) { res.died = true; res.x = m.x; res.y = m.y; res.next = w.Rng.Next(1 << 30); }
            }
            return res;
        }

        static double M(double px) => px / 8.0;

        static void Rammed(StringBuilder sb, string settings, int seeds)
        {
            double worstOff = 0;
            for (int s = 0; s < seeds; s++)
            {
                var on = Ram(settings, 300 + s, ZBigTanks.Mammoth, "heavy", true);
                var off = Ram(settings, 300 + s, ZBigTanks.Mammoth, "heavy", false);
                double dx = on.x - on.cx, dy = on.y - on.cy, d = Math.Sqrt(dx * dx + dy * dy), along = (dx * on.dirX + dy * on.dirY) / 1024.0;
                double ox = off.x - off.cx, oy = off.y - off.cy; worstOff = Math.Max(worstOff, Math.Sqrt(ox * ox + oy * oy));
                bool ok = on.died && on.dirX != 0 && M(d) >= 3.0 && M(d) <= 7.0 && along > 16;
                sb.AppendLine(OzV11Kit.Verdict(ok, $"seed {300 + s}: a Mammoth rams a heavy: its turret debris {M(d):0.0} m from the hull's centre, {M(along):0.0} m along the ram (beyond it, away from the Mammoth); "
                    + $"DV186 off the same seed: {M(Math.Sqrt(ox * ox + oy * oy)):0.0} m (Zod's spread)"));
            }
            sb.AppendLine($"#   DV186 off, the farthest of those Zod points: {M(worstOff):0.0} m");
        }

        static void Crushed(StringBuilder sb, string settings)
        {
            foreach (var victim in new[] { "heavy", "gun" })
            {
                var on = Ram(settings, 410, ZBigTanks.Bizon, victim, true);
                double dx = on.x - on.cx, dy = on.y - on.cy, d = Math.Sqrt(dx * dx + dy * dy);
                sb.AppendLine(OzV11Kit.Verdict(on.died && on.crushed && M(d) <= 3.2, $"a Bizon crushes a {victim}: crushed {on.crushed}; its debris {M(d):0.0} m from its centre (at most 3.2 m)"));
            }
        }

        static (int x, int y, long next) Shot(string settings, bool dv)
        {
            ZWorld.RamDebrisDeviation = dv;
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(64, 48), 515);
            OzV11Kit.Dummy(w, 980, 730);
            var v = Unit(w, "heavy", 400, 384, Team.Blue, 0f, 0f);
            for (int i = 0; i < 20; i++) { w.Tick(ZConst.TICK); w.Events.Clear(); }
            v.SetHealth(0); w.UpdateObjectHealth(v, -1);
            var m = w.PendingMissiles.Concat(w.QueuedMissiles).FirstOrDefault(q => q.debris && q.attackerId == v.Id);
            return m == null ? (int.MinValue, int.MinValue, -1) : (m.x, m.y, w.Rng.Next(1 << 30));
        }

        static void Plain(StringBuilder sb, string settings)
        {
            var on = Shot(settings, true); var off = Shot(settings, false);
            sb.AppendLine(OzV11Kit.Verdict(on.x != int.MinValue && on.x == off.x && on.y == off.y && on.next == off.next,
                $"a heavy killed by plain damage (no ram): its debris point ({on.x}, {on.y}) with DV186 on = ({off.x}, {off.y}) off; the next random number the same {on.next == off.next}"));
        }

        static void SameDraws(StringBuilder sb, string settings)
        {
            var on = Ram(settings, 302, ZBigTanks.Mammoth, "heavy", true);
            var off = Ram(settings, 302, ZBigTanks.Mammoth, "heavy", false);
            sb.AppendLine(OzV11Kit.Verdict(on.died && off.died && on.next == off.next, $"after a ram's death the world's next random number: DV186 on {on.next}, off {off.next} (nothing drawn more or less)"));
        }

        static void Twice(StringBuilder sb, string settings)
        {
            var a = Ram(settings, 301, ZBigTanks.Mammoth, "heavy", true); var b = Ram(settings, 301, ZBigTanks.Mammoth, "heavy", true);
            sb.AppendLine(OzV11Kit.Verdict(a.died && a.x == b.x && a.y == b.y, $"deterministic: seed 301 twice: ({a.x}, {a.y}) and ({b.x}, {b.y})"));
        }
    }
}
