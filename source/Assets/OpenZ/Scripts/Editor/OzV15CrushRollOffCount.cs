using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// DV187's counted check (1.5, kit bizon_crush_climb; Sim/ZObject.CrushRollOff.cs on DV183). Headless: the simulation only, nothing saved; the
    /// game's switches put back after. Guns off (Damage 0). Every line a counted PASS / FAIL:
    ///  A. a Bizon crushes a heavy, a jeep and a ground gun with DV187 ON: the drive ends "over" with the Bizon's centre at least Radius(target) +
    ///     CrushRollOffPx - 2 px beyond the target's centre along the crush (its middle past the far edge); the kill tick is the same as with DV187 OFF
    ///     (the rule's timing unchanged); the drive lasts less than CrushMaxTicks + CrushRollOffTicks; nothing left ramming 13 s on;
    ///  B. a friendly tank standing 6 px beyond the target's far side: the drive ends there, never through it (the two never closer than their
    ///     rooms), the friend alive, the target still destroyed;
    ///  C. DV187 OFF: the drive ends on the target's spot (DV183 as it was);
    ///  D. deterministic: case A (heavy) twice: the same ticks, the same end point.
    /// </summary>
    public static class OzV15CrushRollOffCount
    {
        public static string Quick(string settings) => Run(settings, false);

        public static string Run(string settings, bool full = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DV187 the Bizon rolls off the far side, counted (Sim/ZObject.CrushRollOff.cs)");
            bool b0 = ZWorld.BizonDeviation, c0 = ZWorld.BizonCrushDeviation, r0 = ZWorld.CrushRollOffDeviation;
            try
            {
                ZWorld.BizonDeviation = ZWorld.BizonCrushDeviation = true;
                foreach (var v in full ? new[] { "heavy", "jeep", "gun" } : new[] { "heavy", "gun" }) Over(sb, settings, v);
                Blocked(sb, settings);
                Off(sb, settings);
                if (full) Twice(sb, settings);
            }
            catch (Exception e) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace?.Split('\n').FirstOrDefault())); }
            finally { ZWorld.BizonDeviation = b0; ZWorld.BizonCrushDeviation = c0; ZWorld.CrushRollOffDeviation = r0; }
            return sb.ToString();
        }

        sealed class R { public long hit = -1, dead = -1, endTick = -1; public int tx, ty, dirX, dirY, bx, by, ra, rt, clear, deadX, deadY; public string end = ""; public bool stuck, friendAlive = true; public int friendGap = int.MaxValue; public ZWorld w; }

        static int Rad(string type) => type == "gun" ? ZPathing.CannonRadius : ZPathing.VehicleRadiusOf(type);

        static R Crush(string settings, string victim, bool dv, bool friend)
        {
            ZWorld.CrushRollOffDeviation = dv;
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(64, 48), 707);
            OzV11Kit.Dummy(w, 980, 730);
            var b = OzV11Kit.Place(w, ZBigTanks.Bizon, 320, 384, Team.Red); b.FaceX = 9f; b.FaceY = 0f; b.Damage = 0;
            int ra = ZPathing.VehicleRadiusOf(ZBigTanks.Bizon), rt = Rad(victim);
            var t = OzV11Kit.Place(w, victim, 320 + ra + rt + 20, 384, Team.Blue); t.FaceX = 0f; t.FaceY = 0f; t.Damage = 0;
            ZObject f = null;
            if (friend) { f = OzV11Kit.Place(w, "medium", 320 + ra + rt + 20 + rt + 6 + ZPathing.VehicleRadiusOf("medium"), 384, Team.Red); f.Damage = 0; }
            var r = new R { ra = ra, rt = rt, w = w };
            int e0 = 0;
            for (int i = 0; i < 14 * 100; i++)
            {
                if (OzV11Kit.Alive(t)) { r.tx = t.CenterX; r.ty = t.CenterY; r.dirX = t.RamDirX; r.dirY = t.RamDirY; if (b.CrushClearPlan > 0) r.clear = b.CrushClearPlan; }
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                {
                    if (e.type == ZEventType.BizonCrush && e.flag && e.objId == b.Id && r.hit < 0) r.hit = w.TickIndex;
                    if (e.type == ZEventType.Death && e.objId == t.Id && r.dead < 0) { r.dead = w.TickIndex; r.deadX = b.CenterX; r.deadY = b.CenterY; }
                }
                w.Events.Clear();
                if (f != null) { long dx = f.CenterX - b.CenterX, dy = f.CenterY - b.CenterY; r.friendGap = Math.Min(r.friendGap, (int)Math.Sqrt(dx * dx + dy * dy)); }
                int ends = w.BizonCrushEndsOver + w.BizonCrushEndsBlocked + w.BizonCrushEndsHeld + w.BizonCrushEndsTime;
                if (ends > e0 && r.endTick < 0)
                {
                    r.endTick = w.TickIndex; r.bx = b.CenterX; r.by = b.CenterY;
                    r.end = w.BizonCrushEndsOver > 0 ? "over" : w.BizonCrushEndsBlocked > 0 ? "blocked" : w.BizonCrushEndsHeld > 0 ? "held" : "time";
                }
                e0 = ends;
                if (i == 13 * 100 && b.Ramming) r.stuck = true;
            }
            if (f != null) r.friendAlive = OzV11Kit.Alive(f);
            return r;
        }

        static double Past(R r) => ((r.bx - r.tx) * (double)r.dirX + (r.by - r.ty) * (double)r.dirY) / 1024.0;

        static void Over(StringBuilder sb, string settings, string victim)
        {
            var on = Crush(settings, victim, true, false); var off = Crush(settings, victim, false, false);
            double past = Past(on); int want = on.clear + ZObject.CrushBrakePx - 2;
            double deadPast = ((on.deadX - on.tx) * (double)on.dirX + (on.deadY - on.ty) * (double)on.dirY) / 1024.0;
            // 1.5 rework 14i: the rear off the wreck: the kill CrushBlastTicks after the Bizon's centre passed the clear point (later than DV183's kill
            // under its front), never past the fallback; the drive's end the brake run beyond it
            bool ok = on.hit > 0 && on.dead > 0 && on.end == "over" && on.clear > 0 && past >= want && on.dead > off.dead && on.dead - on.hit <= ZObject.CrushFallbackTicks
                      && deadPast >= on.clear && on.endTick - on.hit < ZObject.CrushMaxTicks + ZObject.CrushRollOffTicks && !on.stuck && on.w.CrushRollOffs == 1 && on.w.CrushRearClears == 1;
            sb.AppendLine(OzV11Kit.Verdict(ok, $"a Bizon crushes a {victim}: the drive ends '{on.end}' {past:0} px beyond the target's centre (want >= {want}: the rear off the wreck {on.clear} px + the brake run {ZObject.CrushBrakePx}), "
                + $"{on.endTick - on.hit} ticks after the contact (limit {ZObject.CrushMaxTicks + ZObject.CrushRollOffTicks}); killed {on.dead - on.hit} ticks after the contact with the Bizon {deadPast:0} px past it (want >= {on.clear}: its rear off the wreck; DV187 off: under its front {off.dead - off.hit} ticks); still ramming 13 s on {on.stuck}"));
        }

        static void Blocked(StringBuilder sb, string settings)
        {
            var on = Crush(settings, "heavy", true, true);
            int minGap = on.ra + ZPathing.VehicleRadiusOf("medium");
            bool ok = on.dead > 0 && on.friendAlive && on.friendGap >= minGap - 1 && !on.stuck;
            sb.AppendLine(OzV11Kit.Verdict(ok, $"a friendly medium tank 6 px beyond the heavy's far side: the drive ends '{on.end}', the two never closer than {on.friendGap} px (rooms {minGap}), the friend alive {on.friendAlive}, the heavy destroyed {on.dead > 0}"));
        }

        static void Off(StringBuilder sb, string settings)
        {
            var off = Crush(settings, "heavy", false, false);
            double past = Past(off);
            sb.AppendLine(OzV11Kit.Verdict(off.end == "over" && Math.Abs(past) <= 2 && off.w.CrushRollOffs == 0, $"DV187 off: the drive ends '{off.end}' on the target's spot ({past:0} px from it), as DV183 made it"));
        }

        static void Twice(StringBuilder sb, string settings)
        {
            var a = Crush(settings, "heavy", true, false); var b = Crush(settings, "heavy", true, false);
            sb.AppendLine(OzV11Kit.Verdict(a.endTick == b.endTick && a.bx == b.bx && a.by == b.by && a.dead == b.dead, $"deterministic: twice: end tick {a.endTick} / {b.endTick}, end ({a.bx}, {a.by}) / ({b.bx}, {b.by}), kill {a.dead} / {b.dead}"));
        }
    }
}
