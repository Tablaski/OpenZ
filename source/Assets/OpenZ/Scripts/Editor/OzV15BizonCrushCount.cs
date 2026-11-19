using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The BIZON CRUSH's counted check (1.5, DV183; Sim/ZObject.BizonCrush.cs on DV182's machinery). Headless: the simulation only (ZWorld.Create
    /// + Tick), no scene, no Play mode, nothing saved; the game's switches (OzV11Kit.GameSwitches) put back after. The Bizon's own gun and its
    /// targets' guns are off (Damage 0), so what happens is the crush's alone. Every line a counted PASS / FAIL:
    ///  A. WHEN: a crush starts only with an enemy crewed vehicle or ground gun in the near front arc (ahead and near: yes; beside, behind, far,
    ///     our own, a neutral empty one, a robot squad, a fort tower gun, the switch off, DV181 off: no);
    ///  B. the crush: the boost x 1.35 only while it lasts; on front contact the target is held (no step, no shot) and the Bizon drives ON over
    ///     it (its centre comes within the kill gap of the target's: the two hulls overlap, the spacing lets it), the target destroyed under its
    ///     front (one Death, the kill the Bizon's) whatever its type and health (a heavy, a jeep, a Mammoth with 100 000 000 HP, a gun, a
    ///     howitzer), never later than 3 s after the contact; then the Bizon stands (no order) or its order goes on and arrives (a MOVE);
    ///  C. TWO enemies close together in front (side by side, one behind the other): one crushed per crush, the other never doomed by it, no
    ///     second Death within the cool-down, nothing stuck (no crush waypoint left), nobody driven through (the one behind keeps its room);
    ///  D. the Mammoth unchanged with DV183 on (it rams a heavy, holds it its 130 flip ticks, never a gun), a new order ends a crush at once
    ///     (before the contact: the target untouched; after it: the target still destroyed);
    ///  E. deterministic: the two-vehicle scene twice: the same positions, ticks and kills.
    /// </summary>
    public static class OzV15BizonCrushCount
    {
        const int Tick100 = 100;

        public static string Quick(string settings) => Run(settings, null, false);

        public static string Run(string settings, Func<string, string> map, bool full = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# the BIZON CRUSH, counted (DV183, Sim/ZObject.BizonCrush.cs)");
            bool b0 = ZWorld.BizonDeviation, c0 = ZWorld.BizonCrushDeviation, m0 = ZWorld.MammothDeviation, r0 = ZWorld.MammothRamDeviation;
            try
            {
                ZWorld.BizonDeviation = true; ZWorld.BizonCrushDeviation = true; ZWorld.MammothDeviation = true; ZWorld.MammothRamDeviation = true;
                When(sb, settings);
                Crush(sb, settings);
                Two(sb, settings);
                Others(sb, settings);
                if (full) Same(sb, settings);
            }
            catch (Exception e) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace?.Split('\n').FirstOrDefault())); }
            finally { ZWorld.BizonDeviation = b0; ZWorld.BizonCrushDeviation = c0; ZWorld.MammothDeviation = m0; ZWorld.MammothRamDeviation = r0; }
            return sb.ToString();
        }

        sealed class Scene
        {
            public ZWorld w; public ZObject b; public readonly List<ZEvent> ev = new List<ZEvent>();
            public int boostTicks, boostOutside, boostWrong, rampDown, firstPct = -1; bool was; int lastPct = -1;   // 1.5 rework 14i: the ramp
            public void Tick(int n = 1, Action each = null)
            {
                for (int i = 0; i < n; i++)
                {
                    double x0 = b.PosX, y0 = b.PosY;
                    w.Tick(ZConst.TICK);
                    ev.AddRange(w.Events); w.Events.Clear();
                    bool moved = b.PosX != x0 || b.PosY != y0;
                    if (b.Ramming) { int pct = b.RamSpeedPct; if (!was) { if (firstPct < 0) firstPct = pct; lastPct = -1; } if (lastPct >= 0 && pct < lastPct) rampDown++; lastPct = pct; }
                    if (moved && b.MoveSpeed > 0 && OzV11Kit.Alive(b))
                    {
                        double k = b.RealMoveSpeed / (b.MoveSpeed * w.Map.TileWalkSpeed(b.CenterX, b.CenterY));
                        bool boosted = b.Ramming ? Math.Abs(k - b.RamSpeedPct / 100.0) < 0.005 : k > 1.005;   // 1.5 rework 14i: the ramp's speed this tick
                        if (b.Ramming) { boostTicks++; if (!boosted) boostWrong++; } else if (boosted && !was) boostOutside++;
                    }
                    was = b.Ramming;
                    each?.Invoke();
                }
            }
            public int Starts() => ev.Count(e => e.type == ZEventType.BizonCrush && !e.flag && e.objId == b.Id);
            public int Contacts(ZObject t = null) => ev.Count(e => e.type == ZEventType.BizonCrush && e.flag && e.objId == b.Id && (t == null || e.otherId == t.Id));
            public int Deaths(ZObject t) => ev.Count(e => e.type == ZEventType.Death && e.objId == t.Id);
            public bool DeathBy(ZObject t, ZObject by) => ev.Any(e => e.type == ZEventType.Death && e.objId == t.Id && e.otherId == by.Id);
            public bool WpLeft() => b.Waypoints.Any(x => x.refId == ZWorld.RefRam);
        }

        static Scene New(string settings, int seed)
        {
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(64, 48), seed);
            OzV11Kit.Dummy(w, 980, 730);
            return new Scene { w = w };
        }

        static ZObject Bizon(Scene s, int cx, int cy, Team t, float fx, float fy)
        {
            var b = OzV11Kit.Place(s.w, ZBigTanks.Bizon, cx, cy, t);
            b.FaceX = fx; b.FaceY = fy; b.Damage = 0;
            if (s.b == null) s.b = b;
            return b;
        }

        static int R(string type) => ZBuildLists.IsRobot(type) ? ZPathing.RobotRadius : ZBuildLists.IsCannon(type) ? ZPathing.CannonRadius : ZPathing.VehicleRadiusOf(type);

        /// <summary>A target gap px (hull to hull) from the Bizon, deg degrees off its heading (east = +x; the sim's y grows south).</summary>
        static ZObject At(Scene s, string type, Team t, int gap, double deg)
        {
            int r = ZPathing.Radius(s.b) + R(type) + gap;
            double a = deg * Math.PI / 180.0;
            var o = OzV11Kit.Place(s.w, type, s.b.CenterX + (int)Math.Round(r * Math.Cos(a)), s.b.CenterY + (int)Math.Round(r * Math.Sin(a)), t);
            o.Damage = 0; foreach (var mm in o.Minions) mm.Damage = 0;
            return o;
        }

        // ------------------------------------------------------------------ A
        static void When(StringBuilder sb, string settings)
        {
            var yes = new List<string>(); var no = new List<string>(); bool okYes = true, okNo = true;
            void Case(string what, bool want, Func<Scene, ZObject> make, Action<Scene> before = null)
            {
                var s = New(settings, 41);
                Bizon(s, 320, 384, Team.Red, 9f, 0f);
                var t = make(s);
                before?.Invoke(s);
                s.Tick(3 * Tick100);
                bool got = s.Starts() > 0;
                string crew = t != null && t.Kind == ZKind.Cannon ? $" (crew {t.Drivers.Count})" : "";
                if (want) { okYes &= got; yes.Add($"{what}{crew} {(got ? "crushed" : "NOT crushed")}"); }
                else { okNo &= !got; no.Add($"{what}{crew} {(got ? "CRUSHED" : "no")}"); }
            }
            Case("an enemy heavy ahead, 24 px", true, s => At(s, "heavy", Team.Blue, 24, 0));
            Case("an enemy jeep 25 deg off, 20 px", true, s => At(s, "jeep", Team.Blue, 20, 25));
            Case("an enemy ground gun ahead, 20 px", true, s => At(s, "gun", Team.Blue, 20, 0));
            Case("an enemy howitzer ahead, 20 px", true, s => At(s, "howitzer", Team.Blue, 20, 0));
            Case("an enemy missile cannon ahead, 20 px", true, s => At(s, "missile_cannon", Team.Blue, 20, 0));
            sb.AppendLine(OzV11Kit.Verdict(okYes, "a crush starts with an enemy crewed vehicle or ground gun in the near front arc: " + string.Join("; ", yes)));
            Case("beside (90 deg)", false, s => At(s, "heavy", Team.Blue, 20, 90));
            Case("behind (180 deg)", false, s => At(s, "gun", Team.Blue, 20, 180));
            Case("far (60 px ahead)", false, s => At(s, "gun", Team.Blue, 60, 0));
            Case("our own gun ahead", false, s => At(s, "gun", Team.Red, 20, 0));
            Case("a neutral empty gun ahead", false, s => At(s, "gun", Team.Neutral, 20, 0));
            Case("an enemy grunt squad ahead", false, s => At(s, "grunt", Team.Blue, 14, 0));
            Case("an enemy fort tower gun (not ejectable) ahead", false, s => { var g = At(s, "gun", Team.Blue, 20, 0); g.Ejectable = false; return g; });
            Case("the switch DV183 off", false, s => At(s, "gun", Team.Blue, 20, 0), s => ZWorld.BizonCrushDeviation = false);
            ZWorld.BizonCrushDeviation = true;
            Case("DV181 off (no Bizon rules)", false, s => At(s, "heavy", Team.Blue, 20, 0), s => ZWorld.BizonDeviation = false);
            ZWorld.BizonDeviation = true;
            sb.AppendLine(OzV11Kit.Verdict(okNo, "no crush for: " + string.Join("; ", no)));
        }

        // ------------------------------------------------------------------ B
        static void Crush(StringBuilder sb, string settings)
        {
            foreach (var type in new[] { "heavy", "jeep", ZBigTanks.Mammoth, "gun", "howitzer" })
            {
                var s = New(settings, 51);
                Bizon(s, 320, 384, Team.Red, 9f, 0f);
                var t = At(s, type, Team.Blue, 24, 0);
                if (type == ZBigTanks.Mammoth) { t.MaxHealth = t.Health = 100000000; }
                long hit = -1, dead = -1; bool moved = false, shot = false; int tx = t.LocX, ty = t.LocY; long minD2 = long.MaxValue;
                int kg = ZPathing.Radius(s.b) + ZPathing.Radius(t) / 3;
                s.Tick(8 * Tick100, () =>
                {
                    if (hit < 0 && t.RamDoomed) { hit = s.w.TickIndex; tx = t.LocX; ty = t.LocY; }
                    if (hit >= 0 && dead < 0 && OzV11Kit.Alive(t))
                    {
                        if (t.LocX != tx || t.LocY != ty) moved = true;
                        if (t.AttackObject != null) shot = true;
                        long dx = t.CenterX - s.b.CenterX, dy = t.CenterY - s.b.CenterY; minD2 = Math.Min(minD2, dx * dx + dy * dy);
                    }
                    if (dead < 0 && t.Destroyed) dead = s.w.TickIndex;
                });
                double minD = minD2 == long.MaxValue ? -1 : Math.Sqrt(minD2);
                bool over = minD >= 0 && minD <= kg + 1;
                sb.AppendLine(OzV11Kit.Verdict(s.Starts() == 1 && s.Contacts(t) == 1 && hit > 0 && dead > hit && dead - hit <= ZObject.CrushFallbackTicks && !moved && !shot && s.Deaths(t) == 1 && s.DeathBy(t, s.b) && over,
                    $"an enemy {type}{(type == ZBigTanks.Mammoth ? " (100 000 000 HP)" : "")} 24 px ahead: 1 start, 1 contact (tick {hit}), held (no step {!moved}, no shot {!shot}), the Bizon on over it (centres {minD:0} px apart at the closest, the kill gap {kg}), "
                    + $"destroyed {dead - hit} ticks after the contact (at most {ZObject.CrushFallbackTicks}): {s.Deaths(t)} Death, the kill the Bizon's ({s.DeathBy(t, s.b)})"));
                if (type == "heavy")
                {
                    sb.AppendLine(OzV11Kit.Verdict(s.boostTicks > 0 && s.boostWrong == 0 && s.boostOutside == 0 && s.firstPct == 0 && s.rampDown == 0,
                        $"the boost ACCELERATES from standing (first crush tick {s.firstPct} %, want 0) toward x {ZObject.CrushBoostPct / 100.0:0.00} over {ZObject.RamRampTicks} ticks, never down ({s.rampDown}); the ramp's speed on all {s.boostTicks} crush ticks it moved (wrong {s.boostWrong}), never outside ({s.boostOutside})"));
                    sb.AppendLine(OzV11Kit.Verdict(!s.b.Ramming && !s.WpLeft() && s.b.Waypoints.Count == 0 && s.w.BizonCrushOnTime >= 1,
                        $"after it a Bizon without an order stands (crushing {s.b.Crushing}, waypoints {s.b.Waypoints.Count}); killed as the front passed (not the fallback): {s.w.BizonCrushOnTime}; ends over {s.w.BizonCrushEndsOver} / held {s.w.BizonCrushEndsHeld} / blocked {s.w.BizonCrushEndsBlocked} / time {s.w.BizonCrushEndsTime}"));
                }
            }
            {   // a MOVE through an enemy gun: crushed, the order goes on and arrives
                var s = New(settings, 52);
                Bizon(s, 160, 384, Team.Red, 9f, 0f);
                var g = OzV11Kit.Place(s.w, "gun", 160 + 110, 384, Team.Blue); g.Damage = 0;
                OzV11Kit.PlayerMove(s.w, s.b, 640, 384, false);
                var order = s.b.Waypoints[0]; bool saw = false, resumed = false;
                s.Tick(90 * Tick100, () => { if (s.b.Ramming) saw = true; if (saw && !resumed && !s.b.Ramming && s.b.Waypoints.Count > 0 && s.b.Waypoints[0].Equals(order)) resumed = true; });
                bool arrived = Math.Abs(s.b.CenterX - 640) <= 4 && Math.Abs(s.b.CenterY - 384) <= 4;
                sb.AppendLine(OzV11Kit.Verdict(s.Contacts(g) == 1 && s.Deaths(g) == 1 && resumed && arrived && !s.WpLeft(),
                    $"a MOVE order through an enemy gun: crushed ({s.Contacts(g)} contact, {s.Deaths(g)} Death), the same order first again after it ({resumed}), arrived ({arrived}: {s.b.CenterX},{s.b.CenterY})"));
            }
        }

        // ------------------------------------------------------------------ C
        static void Two(StringBuilder sb, string settings)
        {
            foreach (var layout in new[] { "side by side", "one behind the other" })
            {
                var s = New(settings, 61);
                Bizon(s, 300, 384, Team.Red, 9f, 0f);
                int rr = ZPathing.Radius(s.b) + ZPathing.VehicleRadiusOf("heavy") + 22;
                ZObject a, c;
                if (layout == "side by side") { a = OzV11Kit.Place(s.w, "heavy", 300 + rr, 384 - 21, Team.Blue); c = OzV11Kit.Place(s.w, "heavy", 300 + rr, 384 + 21, Team.Blue); }
                else { a = OzV11Kit.Place(s.w, "heavy", 300 + rr, 384, Team.Blue); c = OzV11Kit.Place(s.w, "heavy", 300 + rr + 42, 384, Team.Blue); }
                a.Damage = c.Damage = 0;
                ZObject first = null; long firstHit = -1, firstDead = -1, secondDoom = -1; int minGap = int.MaxValue; bool stuck = false;
                s.Tick(20 * Tick100, () =>
                {
                    if (first == null && (a.RamDoomed || c.RamDoomed)) { first = a.RamDoomed ? a : c; firstHit = s.w.TickIndex; }
                    var other = first == a ? c : first == c ? a : null;
                    if (first != null && firstDead < 0 && first.Destroyed) firstDead = s.w.TickIndex;
                    if (other != null && secondDoom < 0 && other.RamDoomed) secondDoom = s.w.TickIndex;
                    if (other != null && OzV11Kit.Alive(other) && !other.RamDoomed && OzV11Kit.Alive(s.b))   // its own later crush may overlap it (that pair is exempt)
                    {
                        long dx = other.CenterX - s.b.CenterX, dy = other.CenterY - s.b.CenterY;
                        minGap = Math.Min(minGap, (int)Math.Sqrt(dx * dx + dy * dy) - ZPathing.MinSpacing(s.b, other));
                    }
                });
                if (s.b.Ramming) stuck = true;
                var second = first == a ? c : a;
                bool oneAtATime = secondDoom < 0 || secondDoom - firstHit >= ZObject.RamCoolTicks;
                sb.AppendLine(OzV11Kit.Verdict(first != null && s.Deaths(first) == 1 && (secondDoom < 0 || s.Contacts(second) <= 1) && oneAtATime && !stuck && !s.WpLeft() && minGap >= -2,
                    $"two enemy heavies {layout} in front: the first crushed and destroyed ({(first != null ? s.Deaths(first) : 0)} Death), the other doomed {(secondDoom < 0 ? "never" : (secondDoom - firstHit) + " ticks after the first contact (its own crush after the cool-down, want >= " + ZObject.RamCoolTicks + ")")}; "
                    + $"never driven into (closest gap to its room {minGap} px); nothing stuck (crushing at the end {stuck}, crush waypoint left {s.WpLeft()})"));
            }
        }

        // ------------------------------------------------------------------ D
        static void Others(StringBuilder sb, string settings)
        {
            {   // the Mammoth as before with DV183 on
                var s = New(settings, 71);
                var m = OzV11Kit.Place(s.w, ZBigTanks.Mammoth, 320, 384, Team.Red); m.FaceX = 9f; m.FaceY = 0f; m.Damage = 0;
                int r = ZPathing.Radius(m) + ZPathing.VehicleRadiusOf("heavy") + 24;
                var t = OzV11Kit.Place(s.w, "heavy", 320 + r, 384, Team.Blue); t.Damage = 0;
                var s2 = New(settings, 72);
                var m2 = OzV11Kit.Place(s2.w, ZBigTanks.Mammoth, 320, 384, Team.Red); m2.FaceX = 9f; m2.FaceY = 0f; m2.Damage = 0;
                var g = OzV11Kit.Place(s2.w, "gun", 320 + ZPathing.Radius(m2) + ZPathing.CannonRadius + 20, 384, Team.Blue); g.Damage = 0;
                long hit = -1, dead = -1;
                for (int i = 0; i < 5 * Tick100; i++)
                {
                    s.w.Tick(ZConst.TICK); s.w.Events.Clear(); s2.w.Tick(ZConst.TICK); s2.w.Events.Clear();
                    if (hit < 0 && t.RamDoomed) hit = s.w.TickIndex;
                    if (dead < 0 && t.Destroyed) dead = s.w.TickIndex;
                }
                sb.AppendLine(OzV11Kit.Verdict(hit > 0 && dead - hit == ZObject.RamFlipTicks && !t.RamCrushed && !g.RamDoomed && s2.w.MammothRamStarts == 0 && s.w.BizonCrushStarts == 0,
                    $"the Mammoth with DV183 on: rams a heavy and holds it {dead - hit} ticks (want {ZObject.RamFlipTicks}, not crushed {!t.RamCrushed}); never goes for a gun (starts {s2.w.MammothRamStarts}, the gun doomed {g.RamDoomed}); no Bizon counts ({s.w.BizonCrushStarts})"));
            }
            {   // a new order before the contact: the target untouched
                var s = New(settings, 73);
                Bizon(s, 320, 384, Team.Red, 9f, 0f);
                var t = At(s, "heavy", Team.Blue, 30, 0);
                bool was = false;
                for (int i = 0; i < 2 * Tick100 && !was; i++) { s.Tick(); if (s.b.Ramming && !s.b.Crushing) was = true; }
                bool atOnce = false;
                if (was) { OzV11Kit.PlayerMove(s.w, s.b, 320, 100, false); atOnce = !s.b.Ramming && !s.WpLeft(); }
                s.Tick(3 * Tick100);
                sb.AppendLine(OzV11Kit.Verdict(was && atOnce && !t.RamDoomed && OzV11Kit.Alive(t), $"a new order before the contact ends the crush at once ({atOnce}); the target untouched ({!t.RamDoomed})"));
            }
            {   // a new order after the contact: the target still destroyed (never left alive)
                var s = New(settings, 74);
                Bizon(s, 320, 384, Team.Red, 9f, 0f);
                var t = At(s, "heavy", Team.Blue, 24, 0);
                bool ordered = false;
                s.Tick(8 * Tick100, () => { if (!ordered && s.b.Crushing) { ordered = true; OzV11Kit.PlayerMove(s.w, s.b, 320, 100, false); } });
                sb.AppendLine(OzV11Kit.Verdict(ordered && t.Destroyed && s.Deaths(t) == 1 && !s.WpLeft(), $"a new order during the drive over the target: the target still destroyed ({t.Destroyed}, {s.Deaths(t)} Death); no crush waypoint left"));
            }
        }

        // ------------------------------------------------------------------ E
        static void Same(StringBuilder sb, string settings)
        {
            string Run1()
            {
                var s = New(settings, 81);
                Bizon(s, 300, 384, Team.Red, 9f, 0f);
                int rr = ZPathing.Radius(s.b) + ZPathing.VehicleRadiusOf("heavy") + 22;
                var a = OzV11Kit.Place(s.w, "heavy", 300 + rr, 384 - 21, Team.Blue); var c = OzV11Kit.Place(s.w, "gun", 300 + rr + 30, 384 + 30, Team.Blue);
                a.Damage = c.Damage = 0;
                var log = new StringBuilder();
                s.Tick(12 * Tick100, () => { if (s.w.TickIndex % 50 == 0) log.Append(s.b.CenterX).Append(',').Append(s.b.CenterY).Append(';'); });
                return log + "|" + string.Join(",", s.ev.Where(e => e.type == ZEventType.BizonCrush || e.type == ZEventType.Death).Select(e => e.type + ":" + e.objId + ">" + e.otherId));
            }
            string x = Run1(), y = Run1();
            sb.AppendLine(OzV11Kit.Verdict(x == y && x.Length > 10, $"deterministic: the two-target scene twice gives the same positions and events ({x.Length} characters of trace, the same {x == y})"));
        }
    }
}
