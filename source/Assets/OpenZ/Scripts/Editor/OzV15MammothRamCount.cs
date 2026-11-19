using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The MAMMOTH RAM's counted check (1.5, DV182; Sim/ZObject.MammothRam.cs). Headless: the simulation only (ZWorld.Create + Tick), no scene,
    /// no Play mode, nothing saved; the game's switches (OzV11Kit.GameSwitches) put back after. The Mammoth's own gun and its targets' guns are
    /// off in the ram rows (Damage 0), so what happens is the ram's alone. Every line a counted PASS / FAIL:
    ///  A. WHEN: a ram starts only with an enemy crewed vehicle in the near front arc: ahead and near (yes); beside, behind, far, at 45 degrees,
    ///     our own, a neutral empty one, a robot squad, a cannon (no); the switch off, DV180 off, a Mammoth that never moved (no);
    ///  B. the ram: the boost x 1.35 only while it rams (never outside), the target rammed on front contact, held 1.3 s, then destroyed with
    ///     the kill the Mammoth's (one Death, otherId = the Mammoth); the Mammoth then stands (no order) or goes on with its order (a MOVE: the
    ///     same waypoint first again and it arrives; an ATTACK on a far tank: that attack first again); a new order ends a ram at once; the
    ///     cool-down (no new start within 3 s of a hit);
    ///  C. TWO enemy vehicles close together in front: side by side and one behind the other: ONE is picked (the nearest, ties by object order),
    ///     one hit, the other never rammed by that ram, no second Death, no change of target while a ram lasts, no drive through the rammed
    ///     hull while it is held, nothing stuck (the Mammoth ends with no ram waypoint);
    ///  D. two Mammoths at one target (one hit, one Death), a target that drives away (no hit, it ends, no chase), water in the way (refused),
    ///     water beside the target (rammed, the Mammoth never on water), a target on a bridge deck or a one-lane chokepoint of level 2 (refused),
    ///     a Mammoth and a Bizon as targets (rammed), a side contact (nothing), robots in its way (run over as before, no ram);
    ///  E. deterministic: the two-vehicle scene twice, a Mammoth fight of two computer teams twice (the same world hash), and a fight without
    ///     Mammoths the same with DV182 on or off.
    /// </summary>
    public static class OzV15MammothRamCount
    {
        const int Tick100 = 100;

        /// <summary>The parity report's probe: parts A to D on plain maps (no level map needed).</summary>
        public static string Quick(string settings) => Run(settings, null, false);

        public static string Run(string settings, Func<string, string> map, bool full = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# the MAMMOTH RAM, counted (DV182, Sim/ZObject.MammothRam.cs)");
            bool m0 = ZWorld.MammothDeviation, r0 = ZWorld.MammothRamDeviation, ro = ZWorld.RunOverDeviation;
            try
            {
                ZWorld.MammothDeviation = true; ZWorld.MammothRamDeviation = true;
                When(sb, settings);
                Ram(sb, settings);
                Two(sb, settings);
                Cases(sb, settings, map);
                if (full) Same(sb, settings);
            }
            catch (Exception e) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace?.Split('\n').FirstOrDefault())); }
            finally { ZWorld.MammothDeviation = m0; ZWorld.MammothRamDeviation = r0; ZWorld.RunOverDeviation = ro; }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ the scene kit
        sealed class Scene
        {
            public ZWorld w; public ZObject m; public readonly List<ZEvent> ev = new List<ZEvent>();
            public int boostTicks, boostOutside, boostWrong, targetSwitches, rampDown, firstPct = -1, fullAfter = -1; public long lastHitTick = -1, minStartAfterHit = long.MaxValue;
            int lastTarget = -1, lastPct = -1; bool wasRamming; long rampStart = -1;   // 1.5 rework 14i: the boost's ramp
            public readonly List<string> notes = new List<string>();
            public void Tick(int n = 1, Action each = null)
            {
                for (int i = 0; i < n; i++)
                {
                    double x0 = m.PosX, y0 = m.PosY;
                    w.Tick(ZConst.TICK);
                    foreach (var e in w.Events)
                    {
                        ev.Add(e);
                        if (e.type == ZEventType.MammothRam && e.objId == m.Id)
                        {
                            if (e.flag) lastHitTick = w.TickIndex;
                            else if (lastHitTick >= 0) minStartAfterHit = Math.Min(minStartAfterHit, w.TickIndex - lastHitTick);
                        }
                    }
                    w.Events.Clear();
                    bool moved = m.PosX != x0 || m.PosY != y0;
                    if (m.Ramming)
                    {   // 1.5 rework 14i: the boost accelerates (ZObject.RamSpeedPct): never down while it rams, the full boost RamRampTicks after the start
                        int pct = m.RamSpeedPct;
                        if (!wasRamming) { rampStart = w.TickIndex; if (firstPct < 0) firstPct = pct; lastPct = -1; }
                        if (lastPct >= 0 && pct < lastPct) rampDown++;
                        if (pct >= ZObject.RamBoostPct && fullAfter < 0 && rampStart >= 0) fullAfter = (int)(w.TickIndex - rampStart);
                        lastPct = pct;
                    }
                    if (moved && m.MoveSpeed > 0 && OzV11Kit.Alive(m))
                    {
                        double k = m.RealMoveSpeed / (m.MoveSpeed * w.Map.TileWalkSpeed(m.CenterX, m.CenterY));
                        bool boosted = m.Ramming ? Math.Abs(k - m.RamSpeedPct / 100.0) < 0.005 : k > 1.005;   // 1.5 rework 14i: the ramp's speed this tick
                        if (m.Ramming) { boostTicks++; if (!boosted) boostWrong++; }
                        else if (boosted && !wasRamming) boostOutside++;
                    }
                    if (m.Ramming) { if (wasRamming && m.RamTargetId != lastTarget) targetSwitches++; lastTarget = m.RamTargetId; }
                    wasRamming = m.Ramming;
                    each?.Invoke();
                }
            }
            public int Starts(ZObject by = null) => ev.Count(e => e.type == ZEventType.MammothRam && !e.flag && e.objId == (by ?? m).Id);
            public int Hits(ZObject by = null) => ev.Count(e => e.type == ZEventType.MammothRam && e.flag && e.objId == (by ?? m).Id);
            public int HitsOn(ZObject t) => ev.Count(e => e.type == ZEventType.MammothRam && e.flag && e.otherId == t.Id);
            public int Deaths(ZObject t) => ev.Count(e => e.type == ZEventType.Death && e.objId == t.Id);
            public bool DeathBy(ZObject t, ZObject by) => ev.Any(e => e.type == ZEventType.Death && e.objId == t.Id && e.otherId == by.Id);
            public bool RamWpLeft(ZObject o = null) => (o ?? m).Waypoints.Any(x => x.refId == ZWorld.RefRam);
        }

        static string Plain() => OzV11Kit.Plain(64, 48);

        /// <summary>A plain map with tile classes set by cls(x, y) (ZClass as int).</summary>
        static string MapWith(int w, int h, Func<int, int, int> cls)
        {
            var sb = new StringBuilder("{\"file\":\"v15_ram\",\"planet\":\"desert\",\"width\":" + w + ",\"height\":" + h + ",\"classes\":[");
            var c = new List<string>(); for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) c.Add(cls(x, y).ToString());
            sb.Append(string.Join(",", c)).Append("],\"tiles\":[").Append(string.Join(",", Enumerable.Repeat("0", w * h)));
            sb.Append("],\"zones\":[{\"x\":0,\"y\":0,\"w\":" + w + ",\"h\":" + h + "}],\"objects\":[]}");
            return sb.ToString();
        }

        static Scene New(string settings, int seed, string map = null)
        {
            var w = OzV11Kit.World(settings, map ?? Plain(), seed);
            OzV11Kit.Dummy(w, 980, 730);
            return new Scene { w = w };
        }

        /// <summary>A Mammoth at (cx, cy) with its hull heading (fx, fy) (as if it had driven that way), its gun off.</summary>
        static ZObject Mammoth(Scene s, int cx, int cy, Team t, float fx, float fy, bool gun = false)
        {
            var m = OzV11Kit.Place(s.w, ZBigTanks.Mammoth, cx, cy, t);
            m.FaceX = fx; m.FaceY = fy;
            if (!gun) m.Damage = 0;
            if (s.m == null) s.m = m;
            return m;
        }

        /// <summary>A target whose centre is gap px (hull to hull) from the Mammoth's, at deg degrees off its heading (east = +x; the sim's y grows south).</summary>
        static ZObject At(Scene s, string type, Team t, int gap, double deg, bool gun = false)
        {
            int r = ZPathing.Radius(s.m) + ZPathing.VehicleRadiusOf(type) + gap;
            if (ZBuildLists.IsRobot(type)) r = ZPathing.Radius(s.m) + ZPathing.RobotRadius + gap;
            double a = deg * Math.PI / 180.0;
            int cx = s.m.CenterX + (int)Math.Round(r * Math.Cos(a)), cy = s.m.CenterY + (int)Math.Round(r * Math.Sin(a));
            var o = OzV11Kit.Place(s.w, type, cx, cy, t);
            if (!gun) { o.Damage = 0; foreach (var mm in o.Minions) mm.Damage = 0; }
            return o;
        }

        // ------------------------------------------------------------------ A. when a ram starts
        static void When(StringBuilder sb, string settings)
        {
            var yes = new List<string>(); var no = new List<string>(); bool okYes = true, okNo = true;
            void Case(string what, bool want, Action<Scene> make, Action<Scene> before = null)
            {
                var s = New(settings, 11);
                Mammoth(s, 320, 384, Team.Red, 9f, 0f);
                make(s);
                before?.Invoke(s);
                s.Tick(3 * Tick100);
                int st = s.Starts();
                bool got = st > 0;
                if (want) { okYes &= got; yes.Add($"{what} {(got ? "rammed" : "NOT rammed")}"); }
                else { okNo &= !got; no.Add($"{what} {(got ? "RAMMED" : "no")}"); }
            }
            Case("an enemy heavy straight ahead, 24 px", true, s => At(s, "heavy", Team.Blue, 24, 0));
            Case("an enemy light tank 25 deg off, 20 px", true, s => At(s, "light", Team.Blue, 20, 25));
            Case("an enemy jeep ahead, 30 px", true, s => At(s, "jeep", Team.Blue, 30, 0));
            sb.AppendLine(OzV11Kit.Verdict(okYes, "a ram starts with an enemy crewed vehicle in the near front arc: " + string.Join("; ", yes)));
            Case("beside (90 deg)", false, s => At(s, "heavy", Team.Blue, 20, 90));
            Case("behind (180 deg)", false, s => At(s, "heavy", Team.Blue, 20, 180));
            Case("at 45 deg", false, s => At(s, "heavy", Team.Blue, 20, 45));
            Case("far (60 px ahead)", false, s => At(s, "heavy", Team.Blue, 60, 0));
            Case("our own heavy ahead", false, s => At(s, "heavy", Team.Red, 20, 0));
            Case("a neutral empty heavy ahead", false, s => At(s, "heavy", Team.Neutral, 20, 0));
            Case("an enemy grunt squad ahead", false, s => At(s, "grunt", Team.Blue, 14, 0));
            Case("an enemy cannon (gun) ahead", false, s => At(s, "gun", Team.Blue, 20, 0));
            Case("the switch DV182 off", false, s => At(s, "heavy", Team.Blue, 20, 0), s => ZWorld.MammothRamDeviation = false);
            ZWorld.MammothRamDeviation = true;
            Case("DV180 off (no Mammoth rules)", false, s => At(s, "heavy", Team.Blue, 20, 0), s => ZWorld.MammothDeviation = false);
            ZWorld.MammothDeviation = true;
            Case("a Mammoth that never moved (no front known)", false, s => At(s, "heavy", Team.Blue, 20, 0), s => { s.m.FaceX = 0f; s.m.FaceY = 0f; });
            sb.AppendLine(OzV11Kit.Verdict(okNo, "no ram for: " + string.Join("; ", no)));
        }

        // ------------------------------------------------------------------ B. the ram itself
        static void Ram(StringBuilder sb, string settings)
        {
            {   // standing, a heavy ahead: start, boost, contact, the hold, the kill, it stands
                var s = New(settings, 21);
                Mammoth(s, 320, 384, Team.Red, 9f, 0f);
                var t = At(s, "heavy", Team.Blue, 24, 0);
                long hitTick = -1, doomTick = -1, deadTick = -1; bool movedWhileHeld = false; int tx = t.LocX, ty = t.LocY; bool tShot = false;
                s.Tick(5 * Tick100, () =>
                {
                    if (hitTick < 0 && t.RamDoomed) { hitTick = s.w.TickIndex; doomTick = t.RamDoomTick; tx = t.LocX; ty = t.LocY; }
                    if (hitTick >= 0 && deadTick < 0 && OzV11Kit.Alive(t) && (t.LocX != tx || t.LocY != ty)) movedWhileHeld = true;
                    if (hitTick >= 0 && deadTick < 0 && OzV11Kit.Alive(t) && t.AttackObject != null) tShot = true;
                    if (deadTick < 0 && t.Destroyed) deadTick = s.w.TickIndex;
                });
                sb.AppendLine(OzV11Kit.Verdict(s.Starts() == 1 && s.Hits() == 1 && hitTick > 0 && s.boostTicks > 0 && s.boostWrong == 0 && s.boostOutside == 0 && s.firstPct == 0 && s.rampDown == 0,
                    $"standing, an enemy heavy 24 px ahead: 1 start, 1 hit at tick {hitTick}; the boost ACCELERATES from standing (first ram tick {s.firstPct} %, want 0) toward x {ZObject.RamBoostPct / 100.0:0.00} over {ZObject.RamRampTicks} ticks, never down while it rams ({s.rampDown}); the ramp's speed on all {s.boostTicks} ram ticks it moved (wrong {s.boostWrong}), never outside a ram ({s.boostOutside})"));
                sb.AppendLine(OzV11Kit.Verdict(deadTick - hitTick == ZObject.RamFlipTicks && !movedWhileHeld && !tShot && s.Deaths(t) == 1 && s.DeathBy(t, s.m),
                    $"the rammed heavy is held {deadTick - hitTick} ticks (want {ZObject.RamFlipTicks}: the flip), no step ({!movedWhileHeld}), no shot ({!tShot}), then destroyed: {s.Deaths(t)} Death, the kill the Mammoth's ({s.DeathBy(t, s.m)})"));
                sb.AppendLine(OzV11Kit.Verdict(!s.m.Ramming && s.m.Waypoints.Count == 0 && !s.m.IsMoving && !s.RamWpLeft(),
                    $"after the ram a Mammoth without an order stands (ramming {s.m.Ramming}, waypoints {s.m.Waypoints.Count}, moving {s.m.IsMoving})"));
                sb.AppendLine(OzV11Kit.Verdict(s.minStartAfterHit == long.MaxValue || s.minStartAfterHit >= ZObject.RamCoolTicks, $"no new ram within {ZObject.RamCoolTicks} ticks of a hit (next start {(s.minStartAfterHit == long.MaxValue ? "none" : s.minStartAfterHit + " ticks later")})"));
            }
            {   // a MOVE order through a jeep: rammed, then the same order goes on and arrives
                var s = New(settings, 22);
                Mammoth(s, 160, 384, Team.Red, 9f, 0f);
                var t = OzV11Kit.Place(s.w, "jeep", 160 + 120, 384, Team.Blue); t.Damage = 0;
                OzV11Kit.PlayerMove(s.w, s.m, 640, 384, false);
                var order = s.m.Waypoints[0]; bool resumed = false, sawRam = false;
                s.Tick(80 * Tick100, () =>
                {
                    if (s.m.Ramming) sawRam = true;
                    if (sawRam && !resumed && !s.m.Ramming && s.m.Waypoints.Count > 0 && s.m.Waypoints[0].Equals(order)) resumed = true;
                });
                bool arrived = Math.Abs(s.m.CenterX - 640) <= 4 && Math.Abs(s.m.CenterY - 384) <= 4;
                sb.AppendLine(OzV11Kit.Verdict(s.Hits() == 1 && resumed && arrived && s.m.Waypoints.Count == 0 && !s.RamWpLeft() && s.boostOutside == 0 && s.boostWrong == 0 && s.firstPct >= 100 && s.rampDown == 0 && (s.fullAfter < 0 || s.fullAfter == ZObject.RamRampTicks),
                    $"a MOVE order through an enemy jeep: rammed ({s.Hits()} hit), the same order first again after it ({resumed}), arrived at its point ({arrived}: {s.m.CenterX},{s.m.CenterY}); the boost on {s.boostTicks} ram ticks, never on the order's own steps ({s.boostOutside}); "
                    + $"moving at the start: the ramp from its speed (first {s.firstPct} %, want >= 100), the full x {ZObject.RamBoostPct / 100.0:0.00} {(s.fullAfter < 0 ? "not reached before the hit" : s.fullAfter + " ticks after the start")} (never sooner than {ZObject.RamRampTicks}), never down ({s.rampDown})"));
            }
            {   // an ATTACK order on a far tank, a jeep close in front: rammed, then the attack goes on
                var s = New(settings, 23);
                Mammoth(s, 160, 384, Team.Red, 9f, 0f, true);   // its gun on: an attack order of a tank that cannot hurt is dropped by the rules
                var far = OzV11Kit.Place(s.w, "heavy", 760, 384, Team.Blue); far.Damage = 0; far.MaxHealth = far.Health = 100000000;
                var jeep = At(s, "jeep", Team.Blue, 20, 0); jeep.MaxHealth = jeep.Health = 100000000;   // no shell kills it before the ram
                OzV11Kit.PlayerAttack(s.w, s.m, far);
                bool back = false;
                s.Tick(4 * Tick100, () => { if (s.Hits() > 0 && !s.m.Ramming && (s.m.Waypoints.Any(x => x.mode == WpMode.Attack && x.refId == far.Id) || s.m.AttackObject == far)) back = true; });   // the attack goes on (its own path step may stand first)
                sb.AppendLine(OzV11Kit.Verdict(s.HitsOn(jeep) == 1 && back && !s.RamWpLeft(), $"an ATTACK order on a far heavy, an enemy jeep close in front: the jeep rammed ({s.HitsOn(jeep)}), the attack first again after it ({back})"));
            }
            {   // a new order ends a ram at once
                var s = New(settings, 24);
                Mammoth(s, 320, 384, Team.Red, 9f, 0f);
                var t = At(s, "heavy", Team.Blue, 30, 0);
                bool was = false, endedAtOnce = false;
                for (int i = 0; i < 2 * Tick100 && !was; i++) { s.Tick(); if (s.m.Ramming) was = true; }
                if (was) { OzV11Kit.PlayerMove(s.w, s.m, 320, 100, false); endedAtOnce = !s.m.Ramming && !s.RamWpLeft(); }
                s.Tick(3 * Tick100);
                sb.AppendLine(OzV11Kit.Verdict(was && endedAtOnce && !t.RamDoomed && OzV11Kit.Alive(t), $"a new order in the middle of a ram ends it at once ({endedAtOnce}); the target untouched ({!t.RamDoomed})"));
            }
        }

        // ------------------------------------------------------------------ C. two enemy vehicles close together in front
        static void Two(StringBuilder sb, string settings)
        {
            foreach (var layout in new[] { "side by side", "one behind the other" })
            {
                var s = New(settings, 31);
                Mammoth(s, 300, 384, Team.Red, 9f, 0f);
                int rr = ZPathing.Radius(s.m) + ZPathing.VehicleRadiusOf("heavy") + 22;
                ZObject a, b;
                if (layout == "side by side")
                {   // both 22 px ahead of the hull, 21 px either side of its line (their gap 2 px over the spacing): the same distance, a tie
                    a = OzV11Kit.Place(s.w, "heavy", 300 + rr, 384 - 21, Team.Blue); b = OzV11Kit.Place(s.w, "heavy", 300 + rr, 384 + 21, Team.Blue);
                }
                else { a = OzV11Kit.Place(s.w, "heavy", 300 + rr, 384, Team.Blue); b = OzV11Kit.Place(s.w, "heavy", 300 + rr + 42, 384, Team.Blue); }
                a.Damage = b.Damage = 0;
                ZObject first = null; int minGapToHeld = int.MaxValue; long aHit = -1, bHit = -1;
                s.Tick(8 * Tick100, () =>
                {
                    if (first == null && s.m.Ramming) first = s.w.Get(s.m.RamTargetId);
                    if (a.RamDoomed && aHit < 0) aHit = s.w.TickIndex;
                    if (b.RamDoomed && bHit < 0) bHit = s.w.TickIndex;
                    foreach (var h in new[] { a, b })
                        if (h.RamDoomed && OzV11Kit.Alive(h))
                        {   // no drive through the rammed hull while it is held
                            int d = (int)OzV11Kit.Dist(s.m, h) - ZPathing.MinSpacing(s.m, h);
                            if (d < minGapToHeld) minGapToHeld = d;
                        }
                });
                bool firstOk = first == a;                                                // the nearest (a tie side by side: object order; in a line: the front one)
                int otherDuringFirst = layout == "side by side" ? (bHit >= 0 && aHit >= 0 && bHit - aHit < ZObject.RamCoolTicks ? 1 : 0) : (bHit >= 0 && bHit - aHit < ZObject.RamCoolTicks ? 1 : 0);
                sb.AppendLine(OzV11Kit.Verdict(firstOk && aHit > 0 && otherDuringFirst == 0 && s.Deaths(a) == 1 && s.Deaths(b) <= 1 && s.HitsOn(a) == 1 && s.HitsOn(b) <= 1
                    && s.targetSwitches == 0 && minGapToHeld >= -ZObject.RamContactPx - 1 && !s.m.Ramming && !s.RamWpLeft(),
                    $"TWO enemy heavies {layout}: picked #{first?.Id} (want the first, #{a.Id}), it rammed at tick {aHit}; the other rammed by that ram: {otherDuringFirst} (a later ram of its own: {(bHit > 0 ? "at tick " + bHit : "none")}); " +
                    $"Deaths {s.Deaths(a)} / {s.Deaths(b)}; target changes while ramming {s.targetSwitches}; closest to a held hull {minGapToHeld} px past the spacing (never through it); ends clean ({!s.m.Ramming && !s.RamWpLeft()})"));
            }
        }

        // ------------------------------------------------------------------ D. more cases
        static void Cases(StringBuilder sb, string settings, Func<string, string> map)
        {
            {   // two Mammoths at one target
                var s = New(settings, 41);
                var t = OzV11Kit.Place(s.w, "heavy", 520, 384, Team.Blue); t.Damage = 0;
                int r = ZPathing.VehicleRadiusOf(ZBigTanks.Mammoth) + 20 + 22;
                var m1 = Mammoth(s, 520 - r, 384, Team.Red, 9f, 0f);
                var m2 = Mammoth(s, 520, 384 - r, Team.Red, 0f, 9f);
                s.Tick(5 * Tick100);
                sb.AppendLine(OzV11Kit.Verdict(s.Hits(m1) + s.Hits(m2) == 1 && s.HitsOn(t) == 1 && s.Deaths(t) == 1 && !m1.Ramming && !m2.Ramming && !s.RamWpLeft(m1) && !s.RamWpLeft(m2),
                    $"two Mammoths at one heavy: starts {s.Starts(m1)} + {s.Starts(m2)}, hits {s.Hits(m1)} + {s.Hits(m2)} (want 1), Deaths {s.Deaths(t)}; both end clean"));
            }
            {   // a target that drives away
                var s = New(settings, 42);
                Mammoth(s, 300, 384, Team.Red, 9f, 0f);
                var j = At(s, "jeep", Team.Blue, 28, 0);
                bool started = false;
                for (int i = 0; i < Tick100 && !started; i++) { s.Tick(); started = s.m.Ramming; }
                j.MoveSpeed = 80;                                                        // 1.5 rework 14i: the ram now reaches 6 m/s (x 6.00): a target that escapes must be faster (a test-only 10 m/s jeep)
                s.w.SendWaypoints(Team.Blue, j, new[] { ZWaypoint.Move(980, 384) });
                double far = 0;
                s.Tick(6 * Tick100, () => far = Math.Max(far, s.m.CenterX));
                sb.AppendLine(OzV11Kit.Verdict(started && s.Hits() == 0 && !s.m.Ramming && s.m.Waypoints.Count == 0 && far < 300 + 120,
                    $"an enemy jeep that drives away mid ram (faster than the ram's boost): no hit ({s.Hits()}), the ram ended ({s.w.MammothRamEndsAway} away, {s.w.MammothRamEndsTime} time), no chase (it got to x {far:0}, started at 300), it stands"));
            }
            {   // water in the way / water beside the target
                string m1 = MapWith(64, 48, (x, y) => x == 22 && y >= 20 && y <= 28 ? (int)ZClass.Water : 0);   // the tile column between the two hulls
                var s = New(settings, 43, m1);
                Mammoth(s, 320, 384, Team.Red, 9f, 0f);
                var t = OzV11Kit.Place(s.w, "heavy", 320 + 24 + 20 + 28, 384, Team.Blue); t.Damage = 0;   // its box (376-407 px) dry, east of the water column (352-367 px)
                s.Tick(3 * Tick100);
                sb.AppendLine(OzV11Kit.Verdict(s.Starts() == 0 && s.w.MammothRamRefusedWay > 0, $"water between it and an enemy heavy: no ram ({s.Starts()} starts, refused for the way {s.w.MammothRamRefusedWay})"));
                string m2 = MapWith(64, 48, (x, y) => x >= 26 && x <= 28 && y >= 18 && y <= 30 ? (int)ZClass.Water : 0);
                var s2 = New(settings, 44, m2);
                Mammoth(s2, 320, 384, Team.Red, 9f, 0f);
                var t2 = OzV11Kit.Place(s2.w, "heavy", 320 + 24 + 20 + 24, 384, Team.Blue); t2.Damage = 0;   // its far side on the bank
                bool wet = false;
                s2.Tick(5 * Tick100, () => { for (int y = s2.m.LocY + 1; y < s2.m.LocY + s2.m.HeightPix - 1; y += 4) for (int x = s2.m.LocX + 1; x < s2.m.LocX + s2.m.WidthPix - 1; x += 4) { var k = s2.w.Map.ClassAt(x >> 4, y >> 4); if (k == ZClass.Water || k == ZClass.DeepWater) wet = true; } });
                sb.AppendLine(OzV11Kit.Verdict(s2.Hits() == 1 && !wet, $"an enemy heavy on the bank of water, the way dry: rammed ({s2.Hits()}), the Mammoth never on water ({!wet})"));
            }
            {   // a Mammoth and a Bizon as targets
                var got = new List<string>(); bool ok = true;
                foreach (var tt in ZBigTanks.Types)
                {
                    var s = New(settings, 45);
                    Mammoth(s, 320, 384, Team.Red, 9f, 0f);
                    var t = At(s, tt, Team.Blue, 22, 0);
                    s.Tick(5 * Tick100);
                    ok &= s.Hits() == 1 && s.Deaths(t) == 1; got.Add($"{tt} {s.Hits()} hit / {s.Deaths(t)} Death");
                }
                sb.AppendLine(OzV11Kit.Verdict(ok, "a big tank is an enemy vehicle like any: " + string.Join(", ", got)));
            }
            {   // a side contact does nothing special
                var s = New(settings, 46);
                Mammoth(s, 320, 300, Team.Red, 9f, 0f);
                var j = OzV11Kit.Place(s.w, "jeep", 320 + 50, 300 + 24 + 16 + 1, Team.Blue); j.Damage = 0;   // 39 deg off its heading, its hull touching the Mammoth's spacing
                s.w.SendWaypoints(Team.Blue, j, new[] { ZWaypoint.Move(100, 300 + 24 + 16 + 1) });   // it drives west along the Mammoth's right flank
                s.Tick(6 * Tick100);
                sb.AppendLine(OzV11Kit.Verdict(s.Starts() == 0 && !j.RamDoomed, $"an enemy jeep passing along its side: no ram ({s.Starts()} starts), nothing rammed"));
            }
            {   // robots in its way: run over as before, no ram
                ZWorld.RunOverDeviation = true;
                var s = New(settings, 47);
                Mammoth(s, 160, 384, Team.Red, 9f, 0f);
                var g = OzV11Kit.Place(s.w, "grunt", 360, 384, Team.Blue, 3); foreach (var r in OzV11Kit.Squad(g)) { r.Damage = 0; r.MoveSpeed = 0; }
                int hits0 = s.w.RunOverHits;
                OzV11Kit.PlayerMove(s.w, s.m, 640, 384, false);
                s.Tick(40 * Tick100);
                // "as before" = as a heavy tank in the same scene (a standing squad: the pathing routes a vehicle round it there, the heavy too)
                var h = New(settings, 47);
                var hv = OzV11Kit.Place(h.w, "heavy", 160, 384, Team.Red); hv.FaceX = 9f; hv.FaceY = 0f; hv.Damage = 0; h.m = hv;
                var hg = OzV11Kit.Place(h.w, "grunt", 360, 384, Team.Blue, 3); foreach (var r in OzV11Kit.Squad(hg)) { r.Damage = 0; r.MoveSpeed = 0; }
                OzV11Kit.PlayerMove(h.w, hv, 640, 384, false);
                h.Tick(40 * Tick100);
                sb.AppendLine(OzV11Kit.Verdict(s.Starts() == 0 && s.w.RunOverHits - hits0 >= h.w.RunOverHits, $"a grunt squad in its way: no ram ({s.Starts()}), run over as before: {s.w.RunOverHits - hits0} run-over hits (a heavy tank in the same scene: {h.w.RunOverHits})"));
            }
            if (map == null) return;
            {   // a target on a bridge deck or a one-lane chokepoint of level 2: refused
                string text = null; try { text = map("L02"); } catch { }
                if (string.IsNullOrEmpty(text)) { sb.AppendLine("SKIP  no L02 map: the bridge row needs it"); return; }
                var w = OzV11Kit.World(settings, text, 48);                              // the level as it starts (its lanes need its bridges); 2 s a lane, far from the forts' reach is not needed: the refusal is the first look
                w.Tick(ZConst.TICK); w.Events.Clear();
                var lanes = w.Pathing != null ? w.Pathing.Lanes : null;
                int tried = 0, refused = 0, hits = 0;
                if (lanes != null)
                    foreach (var L in lanes)
                    {
                        if (tried >= 4) break;
                        int zx = (L.ZX0 + L.ZX1) / 2, zy = (L.ZY0 + L.ZY1) / 2;
                        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            int r = ZPathing.VehicleRadiusOf(ZBigTanks.Mammoth) + ZPathing.VehicleRadiusOf("light") + 24;
                            int mx = zx - dx * r, my = zy - dy * r;
                            if (!Free(w, mx, my) || !Free(w, zx, zy)) continue;
                            var s = new Scene { w = w };
                            var m = Mammoth(s, mx, my, Team.Red, dx * 9f, dy * 9f);
                            var t = OzV11Kit.Place(w, "light", zx, zy, Team.Blue); t.Damage = 0;
                            int ref0 = w.MammothRamRefusedWay;
                            s.Tick(2 * Tick100);
                            tried++; if (w.MammothRamRefusedWay > ref0) refused++; hits += s.Hits();
                            Kill(w, m); Kill(w, t); s.Tick(2);
                            break;
                        }
                    }
                if (tried == 0) sb.AppendLine("SKIP  no lane zone of L02 with free ground for the two hulls was found");
                else sb.AppendLine(OzV11Kit.Verdict(hits == 0 && refused == tried, $"an enemy light tank on a bridge deck / one-lane chokepoint of L02 ({tried} lanes): never rammed ({hits} hits), refused for the way {refused} of {tried}"));
            }
        }

        static bool Free(ZWorld w, int cx, int cy)
        {
            for (int ty = (cy - 16) >> 4; ty <= (cy + 15) >> 4; ty++)
                for (int tx = (cx - 16) >> 4; tx <= (cx + 15) >> 4; tx++)
                {
                    if (!w.Path.TilePassableGrid(tx, ty, false, false)) return false;
                    var k = w.Map.ClassAt(tx, ty); if (k == ZClass.Water || k == ZClass.DeepWater) return false;
                }
            foreach (var o in w.Objects) if (ZPathing.Separable(o) && Math.Abs(o.CenterX - cx) < 48 && Math.Abs(o.CenterY - cy) < 48) return false;
            return true;
        }

        static void Kill(ZWorld w, ZObject o)
        {
            if (o == null || o.Destroyed) return;
            o.SetHealth(0); w.UpdateObjectHealth(o, -1);
        }

        // ------------------------------------------------------------------ E. deterministic
        static string Hash(ZWorld w)
        {
            unchecked
            {
                long h = 17;
                foreach (var o in w.Objects.OrderBy(q => q.Id))
                    h = h * 31 + o.Id * 7919 + o.LocX * 131 + o.LocY * 137 + o.Health + (o.Destroyed ? 1 : 0) + o.RamDoomBy * 3 + (int)o.RamDoomTick * 5 + o.Waypoints.Count * 11;
                h = h * 31 + w.MammothRamStarts * 13 + w.MammothRamHits * 17 + w.MammothRamKills * 19 + w.MammothRamBoostTicks;
                return h.ToString("X");
            }
        }

        static string TwoScene(string settings)
        {
            var s = New(settings, 7);
            Mammoth(s, 300, 384, Team.Red, 9f, 0f, true);
            int rr = ZPathing.Radius(s.m) + ZPathing.VehicleRadiusOf("heavy") + 22;
            OzV11Kit.Place(s.w, "heavy", 300 + rr, 384 - 21, Team.Blue); OzV11Kit.Place(s.w, "heavy", 300 + rr, 384 + 21, Team.Blue);
            s.Tick(20 * Tick100);
            return Hash(s.w);
        }

        /// <summary>Two computer teams: each its Mammoth and a light tank, attack orders on each other (the CPU's order form), real guns.</summary>
        static string BotFight(string settings, bool mammoths, int seed)
        {
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(56, 32), seed);
            w.Bots.Add(new ZBot(w, Team.Red, seed + 202) { Ignored = true });
            OzV11Kit.Dummy(w, 860, 480);
            var a = OzV11Kit.Place(w, mammoths ? ZBigTanks.Mammoth : "heavy", 160, 200, Team.Red);
            var b = OzV11Kit.Place(w, "light", 160, 330, Team.Red);
            var c = OzV11Kit.Place(w, mammoths ? ZBigTanks.Mammoth : "heavy", 560, 260, Team.Blue);
            var d = OzV11Kit.Place(w, "jeep", 600, 330, Team.Blue);
            OzV11Kit.CpuAttack(w, a, c); OzV11Kit.CpuAttack(w, b, d); OzV11Kit.CpuAttack(w, c, a); OzV11Kit.CpuAttack(w, d, b);
            while (w.Time < 60) { w.Tick(ZConst.TICK); w.Events.Clear(); }
            return Hash(w);
        }

        static void Same(StringBuilder sb, string settings)
        {
            string a = TwoScene(settings), b = TwoScene(settings);
            sb.AppendLine(OzV11Kit.Verdict(a == b, $"the two-vehicle scene twice (seed 7, 20 s, guns on): the same world hash ({a} / {b})"));
            string f1 = BotFight(settings, true, 9), f2 = BotFight(settings, true, 9);
            sb.AppendLine(OzV11Kit.Verdict(f1 == f2, $"a Mammoth fight of two computer teams twice (seed 9, 60 s): the same world hash ({f1} / {f2})"));
            string on = BotFight(settings, false, 5);
            ZWorld.MammothRamDeviation = false;
            string off = BotFight(settings, false, 5);
            ZWorld.MammothRamDeviation = true;
            sb.AppendLine(OzV11Kit.Verdict(on == off, $"a fight without Mammoths is the same with DV182 on or off ({on} / {off}): no other unit changes"));
        }
    }
}
