using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The RAM DUEL's counted check (1.5, DV185, kit big_tanks_fx; Sim/ZObject.RamDuel.cs on DV182 / DV183's machinery). Headless: the simulation
    /// only (ZWorld.Create + Tick), no scene, nothing saved; the game's switches (OzV11Kit.GameSwitches) put back after. Every tank's gun is off
    /// (Damage 0): what happens is the rams' alone. Every line a counted PASS / FAIL:
    ///  A. MUTUAL, the same tick (both see each other at once): Mammoth vs Mammoth, Mammoth vs Bizon (both id orders), Bizon vs Bizon: exactly one
    ///     ram (or crush) happens, the lower object id's; the other never starts one on it; exactly one of the pair destroyed (by the other),
    ///     never both, never a flip and a crush on the pair; nothing stuck (no ram waypoint left, nobody ramming 12 s on: a crush drive lasts at most 6.5 s after its contact);
    ///  B. MUTUAL, one started FIRST: the other one (the lower id this time) turns to face it 0.3 s later: it never starts, the first wins;
    ///  C. a Bizon crushes a Mammoth that is ramming a third vehicle: the Mammoth's ram ends at once, the third vehicle untouched;
    ///  D. a Mammoth rams a Bizon that is rolling over a vehicle it crushed: the crushed vehicle is destroyed the next tick (the kill the Bizon's),
    ///     not up to 3 s later under a dead Bizon; with DV185 off it waits for its own fallback (the old way, shown for contrast);
    ///  E. DV185 OFF: the same-tick mutual Mammoth pair starts TWO rams (the old way: first to contact in object order), ON: one;
    ///  F. deterministic: case A (Mammoth vs Bizon) twice: the same ticks, the same winner.
    /// </summary>
    public static class OzV15RamDuelCount
    {
        const int Tick100 = 100;

        public static string Quick(string settings) => Run(settings, null, false);

        public static string Run(string settings, Func<string, string> map, bool full = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# the RAM DUEL, counted (DV185, Sim/ZObject.RamDuel.cs)");
            bool b0 = ZWorld.BizonDeviation, c0 = ZWorld.BizonCrushDeviation, m0 = ZWorld.MammothDeviation, r0 = ZWorld.MammothRamDeviation, d0 = ZWorld.RamDuelDeviation;
            try
            {
                ZWorld.BizonDeviation = ZWorld.BizonCrushDeviation = ZWorld.MammothDeviation = ZWorld.MammothRamDeviation = ZWorld.RamDuelDeviation = true;
                Mutual(sb, settings);
                FirstWins(sb, settings);
                RammerCrushed(sb, settings);
                CrusherRammed(sb, settings);
                Off(sb, settings);
                if (full) Same(sb, settings);
            }
            catch (Exception e) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace?.Split('\n').FirstOrDefault())); }
            finally { ZWorld.BizonDeviation = b0; ZWorld.BizonCrushDeviation = c0; ZWorld.MammothDeviation = m0; ZWorld.MammothRamDeviation = r0; ZWorld.RamDuelDeviation = d0; }
            return sb.ToString();
        }

        sealed class Scene
        {
            public ZWorld w; public readonly List<ZEvent> ev = new List<ZEvent>();
            public void Tick(int n = 1, Action each = null)
            {
                for (int i = 0; i < n; i++) { w.Tick(ZConst.TICK); ev.AddRange(w.Events); w.Events.Clear(); each?.Invoke(); }
            }
            public int Starts(ZObject a) => ev.Count(e => (e.type == ZEventType.MammothRam || e.type == ZEventType.BizonCrush) && !e.flag && e.objId == a.Id);
            public int Hits(ZObject a, ZObject t) => ev.Count(e => (e.type == ZEventType.MammothRam || e.type == ZEventType.BizonCrush) && e.flag && e.objId == a.Id && e.otherId == t.Id);
            public bool DeathBy(ZObject t, ZObject by) => ev.Any(e => e.type == ZEventType.Death && e.objId == t.Id && e.otherId == by.Id);
            public int Deaths(ZObject t) => ev.Count(e => e.type == ZEventType.Death && e.objId == t.Id);
        }

        static Scene New(string settings, int seed)
        {
            var w = OzV11Kit.World(settings, OzV11Kit.Plain(64, 48), seed);
            OzV11Kit.Dummy(w, 980, 730);
            return new Scene { w = w };
        }

        static ZObject Tank(Scene s, string type, int cx, int cy, Team t, float fx, float fy)
        {
            var o = OzV11Kit.Place(s.w, type, cx, cy, t); o.FaceX = fx; o.FaceY = fy; o.Damage = 0;
            return o;
        }

        static bool WpLeft(ZObject o) => o.Waypoints.Any(x => x.refId == ZWorld.RefRam);

        /// <summary>Two tanks facing each other, gap px hull to hull, the first placed first (the lower id).</summary>
        static (Scene s, ZObject a, ZObject b) Pair(string settings, int seed, string ta, string tb, int gap)
        {
            var s = New(settings, seed);
            var a = Tank(s, ta, 320, 384, Team.Red, 9f, 0f);
            int r = ZPathing.VehicleRadiusOf(ta) + ZPathing.VehicleRadiusOf(tb) + gap;
            var b = Tank(s, tb, 320 + r, 384, Team.Blue, -9f, 0f);
            return (s, a, b);
        }

        static string Name(ZObject o) => (o.Type == ZBigTanks.Mammoth ? "Mammoth" : "Bizon") + " #" + o.Id;

        // ------------------------------------------------------------------ A
        static void Mutual(StringBuilder sb, string settings)
        {
            var cases = new[] { (ZBigTanks.Mammoth, ZBigTanks.Mammoth), (ZBigTanks.Mammoth, ZBigTanks.Bizon), (ZBigTanks.Bizon, ZBigTanks.Mammoth), (ZBigTanks.Bizon, ZBigTanks.Bizon) };
            int seed = 81;
            foreach (var (ta, tb) in cases)
            {
                var (s, a, b) = Pair(settings, seed++, ta, tb, 24);
                bool bothRamming = false; int stuckTicks = 0;
                s.Tick(12 * Tick100, () => { if (a.Ramming && b.Ramming) bothRamming = true; });   // a Bizon's drive over its target lasts up to CrushMaxTicks (6.5 s) after the contact
                for (int i = 0; i < Tick100; i++) { s.Tick(); if ((OzV11Kit.Alive(a) && a.Ramming) || (OzV11Kit.Alive(b) && b.Ramming)) stuckTicks++; }
                bool aWon = !OzV11Kit.Alive(b) && OzV11Kit.Alive(a), bWon = !OzV11Kit.Alive(a) && OzV11Kit.Alive(b);
                bool pairOnce = (a.RamDoomBy == -1) != (b.RamDoomBy == -1);
                bool kindRight = !bWon && aWon && (a.Type == ZBigTanks.Mammoth ? !b.RamCrushed : b.RamCrushed);
                sb.AppendLine(OzV11Kit.Verdict(aWon && s.Starts(b) == 0 && s.Starts(a) == 1 && s.Hits(a, b) == 1 && s.DeathBy(b, a) && pairOnce && kindRight && !bothRamming && stuckTicks == 0 && !WpLeft(a) && !WpLeft(b),
                    $"{Name(a)} and {Name(b)} facing each other 24 px apart, the same tick: the lower id {Name(a)} {(a.Type == ZBigTanks.Mammoth ? "rams" : "crushes")} (starts {s.Starts(a)}, contacts {s.Hits(a, b)}), "
                    + $"{Name(b)} never starts ({s.Starts(b)}); destroyed: {Name(b)} {!OzV11Kit.Alive(b)} (by {Name(a)} {s.DeathBy(b, a)}, {(b.RamCrushed ? "crushed" : "flipped")}), {Name(a)} {!OzV11Kit.Alive(a)}; "
                    + $"one doom on the pair {pairOnce}; both ramming at once ever {bothRamming}; ramming 12-13 s on {stuckTicks} ticks; ram waypoints left {WpLeft(a) || WpLeft(b)}; "
                    + $"duel counters: met {s.w.RamDuelDuels}, refused looks {s.w.RamDuelYields}, cancels {s.w.RamDuelCancels}"));
            }
        }

        // ------------------------------------------------------------------ B
        static void FirstWins(StringBuilder sb, string settings)
        {
            foreach (var (ta, tb) in new[] { (ZBigTanks.Bizon, ZBigTanks.Mammoth), (ZBigTanks.Mammoth, ZBigTanks.Mammoth) })
            {
                var s = New(settings, 91);
                // a = the LOWER id, facing away at first; b faces a from the start and begins its ram at once
                var a = Tank(s, ta, 320, 384, Team.Red, -9f, 0f);
                int r = ZPathing.VehicleRadiusOf(ta) + ZPathing.VehicleRadiusOf(tb) + 26;
                var b = Tank(s, tb, 320 + r, 384, Team.Blue, -9f, 0f);
                long bStart = -1, aStart = -1; bool turned = false;
                s.Tick(8 * Tick100, () =>
                {
                    if (bStart < 0 && b.Ramming) bStart = s.w.TickIndex;
                    if (aStart < 0 && a.Ramming) aStart = s.w.TickIndex;
                    if (!turned && s.w.TickIndex >= 30) { turned = true; a.FaceX = 9f; a.FaceY = 0f; }   // a now faces b, whose ram started first
                });
                bool ok = bStart >= 0 && aStart < 0 && !OzV11Kit.Alive(a) && OzV11Kit.Alive(b) && s.DeathBy(a, b) && !WpLeft(a) && !WpLeft(b) && !b.Ramming;
                sb.AppendLine(OzV11Kit.Verdict(ok, $"{Name(b)} starts its ram first (tick {bStart}); {Name(a)} (the lower id) faces it 0.3 s later: it never starts (its start {(aStart < 0 ? "none" : aStart.ToString())}); "
                    + $"{Name(b)} wins ({Name(a)} destroyed by it {s.DeathBy(a, b)}, {Name(b)} alive {OzV11Kit.Alive(b)}); nothing left ramming; refused looks {s.w.RamDuelYields}"));
            }
        }

        // ------------------------------------------------------------------ C
        static void RammerCrushed(StringBuilder sb, string settings)
        {
            var s = New(settings, 101);
            var m = Tank(s, ZBigTanks.Mammoth, 320, 384, Team.Red, 9f, 0f);                                     // rams east
            var h = Tank(s, "heavy", 320 + ZPathing.VehicleRadiusOf(ZBigTanks.Mammoth) + 20 + 30, 384, Team.Blue, 0f, 0f);
            var z = Tank(s, ZBigTanks.Bizon, 320, 384 + ZPathing.VehicleRadiusOf(ZBigTanks.Mammoth) + ZPathing.VehicleRadiusOf(ZBigTanks.Bizon) + 16, Team.Blue, 0f, -9f);   // south of it, facing north
            bool mRammed = false; long mDoom = -1; bool rammingAfterDoom = false;
            s.Tick(8 * Tick100, () =>
            {
                if (m.Ramming) mRammed = true;
                if (mDoom < 0 && m.RamDoomed) mDoom = s.w.TickIndex;
                if (mDoom >= 0 && m.Ramming) rammingAfterDoom = true;
            });
            bool ok = mRammed && mDoom >= 0 && m.RamCrushed && m.RamDoomBy == z.Id && !rammingAfterDoom && !h.RamDoomed && OzV11Kit.Alive(h) && OzV11Kit.Alive(z) && !WpLeft(m) && s.Hits(m, h) == 0;
            sb.AppendLine(OzV11Kit.Verdict(ok, $"a Bizon crushes a Mammoth that is ramming a heavy: the Mammoth rammed first ({mRammed}), crushed at tick {mDoom} by the Bizon ({m.RamDoomBy == z.Id}); "
                + $"its ram ended at once (ramming after {rammingAfterDoom}, waypoint left {WpLeft(m)}); the heavy untouched (doomed {h.RamDoomed}, alive {OzV11Kit.Alive(h)}, contacts {s.Hits(m, h)}); the Bizon alive {OzV11Kit.Alive(z)}"));
        }

        // ------------------------------------------------------------------ D
        static (bool ok, string what) CrusherRammedCase(string settings, bool duel)
        {
            ZWorld.RamDuelDeviation = duel;
            try
            {
                var s = New(settings, 111);
                var z = Tank(s, ZBigTanks.Bizon, 320, 384, Team.Blue, 9f, 0f);                                    // crushes east
                var v = Tank(s, "heavy", 320 + ZPathing.VehicleRadiusOf(ZBigTanks.Bizon) + 20 + 6, 384, Team.Red, 0f, 0f);
                v.MaxHealth = v.Health = 100000000;
                var m = Tank(s, ZBigTanks.Mammoth, 320, 384 + ZPathing.VehicleRadiusOf(ZBigTanks.Bizon) + ZPathing.VehicleRadiusOf(ZBigTanks.Mammoth) + 4, Team.Red, 0f, 0f);   // south; 1.5 rework 14i: 4 px (was 20) and no front known until the Bizon's contact, then facing north: the ram now accelerates from standing and the crush runs at x 8.00, so the Mammoth must start close and late to meet the Bizon while the heavy is under it
                long zDoom = -1, vDead = -1, vHit = -1; bool crushingAtDoom = false;
                s.Tick(10 * Tick100, () =>
                {
                    if (vHit < 0 && v.RamDoomed) { vHit = s.w.TickIndex; m.FaceX = 0f; m.FaceY = -9f; }
                    if (zDoom < 0 && z.RamDoomed) { zDoom = s.w.TickIndex; crushingAtDoom = vHit >= 0 && OzV11Kit.Alive(v); }
                    if (vDead < 0 && v.Destroyed) vDead = s.w.TickIndex;
                });
                bool ok = vHit >= 0 && zDoom > vHit && crushingAtDoom && z.RamDoomBy == m.Id && vDead >= 0 && s.DeathBy(v, z) && OzV11Kit.Alive(m) && !z.Crushing;
                if (duel) ok &= vDead - zDoom <= 2;
                else ok &= vDead - zDoom > 2;
                return (ok, $"DV185 {(duel ? "ON" : "off")}: the Bizon crushed the heavy at tick {vHit}; the Mammoth rammed the Bizon at tick {zDoom} while it rolled over the heavy ({crushingAtDoom}); "
                    + $"the heavy destroyed at tick {vDead} ({vDead - zDoom} ticks after the Bizon's doom; {(duel ? "want <= 2" : "the old way: at its own fallback")}), the kill the Bizon's ({s.DeathBy(v, z)}); the Mammoth alive {OzV11Kit.Alive(m)}");
            }
            finally { ZWorld.RamDuelDeviation = true; }
        }

        static void CrusherRammed(StringBuilder sb, string settings)
        {
            var on = CrusherRammedCase(settings, true); var off = CrusherRammedCase(settings, false);
            sb.AppendLine(OzV11Kit.Verdict(on.ok, "a Mammoth rams a Bizon rolling over a vehicle it crushed: " + on.what));
            sb.AppendLine(OzV11Kit.Verdict(off.ok, "the same with the switch off (contrast): " + off.what));
        }

        // ------------------------------------------------------------------ E
        static void Off(StringBuilder sb, string settings)
        {
            int Starts(bool duel)
            {
                ZWorld.RamDuelDeviation = duel;
                try { var (s, a, b) = Pair(settings, 121, ZBigTanks.Mammoth, ZBigTanks.Mammoth, 24); s.Tick(Tick100); return s.w.MammothRamStarts; }
                finally { ZWorld.RamDuelDeviation = true; }
            }
            int off = Starts(false), on = Starts(true);
            sb.AppendLine(OzV11Kit.Verdict(off == 2 && on == 1, $"the switch: two Mammoths facing each other, the same tick: DV185 off {off} rams started (the old way), on {on}"));
        }

        // ------------------------------------------------------------------ F
        static void Same(StringBuilder sb, string settings)
        {
            string Trace()
            {
                var (s, a, b) = Pair(settings, 131, ZBigTanks.Mammoth, ZBigTanks.Bizon, 24);
                var t = new StringBuilder();
                s.Tick(6 * Tick100, () => { if (s.w.TickIndex % 25 == 0) t.Append(a.LocX).Append(',').Append(a.LocY).Append(';').Append(b.LocX).Append(',').Append(b.LocY).Append(';').Append(a.Ramming ? 1 : 0).Append(b.Ramming ? 1 : 0).Append('|'); });
                t.Append(a.Destroyed).Append(b.Destroyed).Append(b.RamDoomTick).Append(a.RamDoomTick);
                return t.ToString();
            }
            string x = Trace(), y = Trace();
            sb.AppendLine(OzV11Kit.Verdict(x == y && x.Length > 40, $"deterministic: Mammoth vs Bizon twice: the same positions, ram states and death ticks every 0.25 s ({x.Length} chars, equal {x == y})"));
        }
    }
}
