using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit mammoth_ram_fight_n26c: the counted check of DV196 "the Mammoth fights on while it rams" (Sim/ZObject.RamFight.cs), headless, on
    /// plain maps, the big-tank switches on as in a Play session (DV180-DV195, RAM-FIRST DV189 included). Every line a counted PASS / FAIL. "The
    /// ram" = from the Mammoth's ram start to the death of the vehicle it rammed (its flip and landing included).
    ///  A. A Mammoth (player's ATTACK order on it, and idle) rams an enemy heavy tank while two more enemy tanks stand in its gun range beside it
    ///     (outside the ram's arc), at four start gaps: during the ram it fires at them (shots counted, every first-barrel shot one reload after the
    ///     last or later: its normal rate), never at the rammed heavy (0 shots, 0 ticks with the heavy as its target), and it is on a target every
    ///     tick from the first look on (0 ticks without one while an enemy stands in range).
    ///  B. The target it fights during the ram is destroyed: it is on the other one within <see cref="ZObject.RamLookTicks"/> ticks (DV196 off:
    ///     Zod's 0.5 s passive look: up to 50 ticks without a target, measured).
    ///  C. No other enemy in range: during the ram 0 shots, 0 ticks with the rammed heavy as its target (its fire held).
    ///  D. A column of three enemy medium tanks (DV195's chain) with an enemy heavy beside in range: all three rammed, 0 shots and 0 target ticks at
    ///     any rammed one, shots at the heavy.
    ///  E. Deterministic: scene A twice: the same shots, ticks and spots.
    /// Menu: OpenZ/V1.5/Ram Fight: Count (headless, DV196) and (short, DV196); the parity row DV196 runs <see cref="Quick"/>.
    /// </summary>
    public static class OzV15RamFightCount
    {
        public static string Quick(string settings) => Run(settings, false);

        static readonly string[] Big = { "MammothDeviation", "BizonDeviation", "MammothRamDeviation", "BizonCrushDeviation", "RamDuelDeviation", "CrushRollOffDeviation", "RamDebrisDeviation", "RamPolishDeviation", "BigTankRamFirstDeviation", "BizonAmphibDeviation", "BigTankWreckDeviation", "RamChainDeviation" };
        const int Seed = 196;

        static bool Get(string n) { var f = typeof(ZWorld).GetField(n); return f != null && (bool)f.GetValue(null); }
        static void Set(string n, bool v) => OzV11Kit.TrySet(typeof(ZWorld), n, v);

        public static string Run(string settings, bool full = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DV196 the Mammoth fights on while it rams, counted (Sim/ZObject.RamFight.cs)");
            var keep = new Dictionary<string, bool>();
            foreach (var n in Big.Concat(new[] { "RamFightDeviation", "CalmStartDeviation" })) keep[n] = Get(n);
            try
            {
                foreach (var n in Big) Set(n, true);
                Set("CalmStartDeviation", false); Set("RamFightDeviation", true);
                // A
                var gaps = full ? new[] { 60, 90, 120, 135 } : new[] { 60, 120 };
                int shots = 0, atRam = 0, tgtRam = 0, gapTicks = 0, fast = 0, rams = 0, scenes = 0;
                var lines = new List<string>();
                foreach (bool order in full ? new[] { true, false } : new[] { true })
                    foreach (int g in gaps)
                    {
                        var r = Scene(settings, g, 2, order, -1);
                        scenes++; if (r.rammed) rams++;
                        shots += r.shots; atRam += r.atRam; tgtRam += r.tgtRam; gapTicks += r.noTarget; fast += r.fast;
                        lines.Add($"{(order ? "ATTACK" : "idle")} gap {g}: ram {r.start}-{r.end}, shots {r.shots} (at a rammed one {r.atRam}), a rammed one its target {r.tgtRam} ticks, no target with one in range {r.noTarget} ticks");
                    }
                sb.AppendLine(OzV11Kit.Verdict(rams == scenes && shots > 0 && atRam == 0 && tgtRam == 0 && gapTicks == 0 && fast == 0,
                    $"A two more enemies in range: {rams} of {scenes} rams hit; during the rams {shots} shots at the other two, {atRam} at a rammed vehicle (want 0), a rammed vehicle its target {tgtRam} ticks (want 0), "
                    + $"no target while one stood in range {gapTicks} ticks (want 0), shots faster than its reload {fast} (want 0)"));
                foreach (var l in lines) sb.AppendLine("    " + l);
                // B
                var on = Scene(settings, 120, 2, true, 40);
                Set("RamFightDeviation", false); var off = Scene(settings, 120, 2, true, 40); Set("RamFightDeviation", true);
                sb.AppendLine(OzV11Kit.Verdict(on.killDone && on.switchTicks >= 0 && on.switchTicks <= ZObject.RamLookTicks && on.atRam == 0 && on.tgtRam == 0,
                    $"B its target destroyed during the ram: on the other one {on.switchTicks} ticks later (want <= {ZObject.RamLookTicks}); DV196 off: {off.switchTicks} ticks (Zod's 0.5 s passive look, up to 50); shots at a rammed one {on.atRam}, a rammed one its target {on.tgtRam} ticks"));
                // C
                var c = Scene(settings, 120, 0, true, -1);
                sb.AppendLine(OzV11Kit.Verdict(c.rammed && c.shots == 0 && c.tgtRam == 0 && c.withTarget == 0,
                    $"C no other enemy in range: ram {c.start}-{c.end}, shots {c.shots} (want 0), the rammed heavy its target {c.tgtRam} ticks (want 0), ticks with any target {c.withTarget} (want 0: its fire held)"));
                // D
                var d = Column(settings);
                sb.AppendLine(OzV11Kit.Verdict(d.rams == 3 && d.atRam == 0 && d.tgtRam == 0 && d.shotsOther > 0,
                    $"D a column of three (DV195's chain) and an enemy heavy beside: rammed {d.rams} of 3, shots at a rammed one {d.atRam} (want 0), a rammed one its target {d.tgtRam} ticks (want 0), shots at the heavy {d.shotsOther}"));
                // E
                if (full)
                {
                    var a1 = Scene(settings, 120, 2, true, -1); var a2 = Scene(settings, 120, 2, true, -1);
                    sb.AppendLine(OzV11Kit.Verdict(a1.print == a2.print, $"E deterministic: scene A twice: {a1.print:x16} / {a2.print:x16}"));
                }
            }
            catch (Exception e) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace?.Split('\n').FirstOrDefault())); }
            finally { foreach (var kv in keep) Set(kv.Key, kv.Value); }
            return sb.ToString();
        }

        static ZWorld World(string settings)
        {
            var wd = ZWorld.Create(settings, OzV11Kit.Plain(64, 48), Seed);
            wd.Bots.Add(new ZBot(wd, Team.Blue, Seed + 101) { Ignored = true });
            wd.Events.Clear();
            OzV11Kit.Dummy(wd, 64 * 16 - 40, 48 * 16 - 40);
            return wd;
        }

        static ZObject Tough(ZObject o) { o.Damage = 0; o.MaxHealth = o.Health = 1000000000; return o; }

        sealed class R { public long start = -1, end = -1; public int shots, atRam, tgtRam, noTarget, withTarget, fast, switchTicks = -1; public bool rammed, killDone; public ulong print = 1469598103934665603UL; }

        /// <summary>o is the vehicle the Mammoth rams now or one it doomed (any vehicle, not only the heavy: RAM-FIRST may pick another).</summary>
        static bool Rammed(ZWorld w, ZObject m, ZObject o) => o != null && (o.RamDoomBy == m.Id || (m.Ramming && m.RamTargetId == o.Id));

        static bool Victim(ZWorld w, ZObject m) { foreach (var o in w.Objects) if (o.RamDoomBy == m.Id && !o.Destroyed) return true; return false; }

        static bool InRange(ZWorld w, ZObject m)
        {
            foreach (var o in w.Objects)
                if (o.IsUnit && !o.Destroyed && o.KillAt < 0 && o.Owner != Team.Neutral && o.Owner != m.Owner && m.CanAttackObject(o) && m.WithinAttackRadius(o)) return true;
            return false;
        }

        /// <summary>A Mammoth at (320, 384) facing east, an enemy heavy `gap` px ahead (centre to centre), `others` enemy medium tanks beside in
        /// range (outside the ram's arc). order: a player's ATTACK on the heavy, else idle (RAM-FIRST picks it). killAt: that many ticks into the
        /// ram the Mammoth's target is destroyed.</summary>
        static R Scene(string settings, int gap, int others, bool order, int killAt)
        {
            var w = World(settings);
            var m = OzV11Kit.Place(w, ZBigTanks.Mammoth, 320, 384, Team.Red); m.FaceX = 9f; m.FaceY = 0f; m.MaxHealth = m.Health = 1000000000;
            var h = Tough(OzV11Kit.Place(w, "heavy", 320 + gap, 384, Team.Blue)); h.FaceX = -9f; h.FaceY = 0f;
            for (int i = 0; i < others; i++) { var o = Tough(OzV11Kit.Place(w, "medium", 320 + 100, 384 + (i == 0 ? -95 : 95), Team.Blue)); o.FaceX = 0f; o.FaceY = 9f; }
            if (order) OzV11Kit.PlayerAttack(w, m, h);
            var r = new R();
            long lastShot = -100000, killTick = -1; int reload = (int)Math.Round(m.AttackSpeed / ZConst.TICK);
            for (int i = 0; i < 2000; i++)
            {
                w.Tick(ZConst.TICK);
                long now = w.TickIndex;
                bool ram = m.Ramming || Victim(w, m);
                if (ram && r.start < 0) r.start = now;
                if (Victim(w, m)) r.rammed = true;
                foreach (var e in w.Events)
                {
                    if (e.objId != m.Id || e.type != ZEventType.MissileFired) continue;
                    if (ram) { r.shots++; if (Rammed(w, m, w.Get(e.otherId))) r.atRam++; }
                    r.print = (r.print ^ (ulong)(now * 31 + e.otherId)) * 1099511628211UL;
                }
                foreach (var e in w.Events)
                    if (e.objId == m.Id && e.type == ZEventType.MissileFired && ram && now - lastShot < reload - 1 && now - lastShot > 60) r.fast++;   // first barrels only: the second barrel follows 36 ticks after the first
                foreach (var e in w.Events) if (e.objId == m.Id && e.type == ZEventType.MissileFired && now - lastShot > 60) lastShot = now;
                w.Events.Clear();
                if (ram)
                {
                    var a = m.AttackObject;
                    if (Rammed(w, m, a)) r.tgtRam++;
                    if (a != null) r.withTarget++;
                    else if (now - r.start > ZObject.RamLookTicks && InRange(w, m)) r.noTarget++;
                    if (killAt >= 0 && killTick < 0 && now - r.start >= killAt && a != null && !Rammed(w, m, a))
                    {
                        a.SetHealth(0); w.UpdateObjectHealth(a, -1); killTick = now; r.killDone = true;
                    }
                    if (killTick >= 0 && r.switchTicks < 0 && m.AttackObject != null && !m.AttackObject.Destroyed && !Rammed(w, m, m.AttackObject)) r.switchTicks = (int)(now - killTick);
                }
                if (r.start >= 0 && r.end < 0 && !ram) { r.end = now; break; }
                r.print = (r.print ^ (ulong)(m.CenterX * 7919 + m.CenterY)) * 1099511628211UL;
            }
            return r;
        }

        sealed class Col { public int rams, atRam, tgtRam, shotsOther; }

        static Col Column(string settings)
        {
            var w = World(settings);
            var m = OzV11Kit.Place(w, ZBigTanks.Mammoth, 200, 384, Team.Red); m.FaceX = 9f; m.FaceY = 0f; m.MaxHealth = m.Health = 1000000000;
            var col = new List<ZObject>();
            for (int i = 0; i < 3; i++) { var t = OzV11Kit.Place(w, "medium", 290 + i * 40, 384, Team.Blue); t.FaceX = -9f; t.FaceY = 0f; t.Damage = 0; col.Add(t); }
            var hv = Tough(OzV11Kit.Place(w, "heavy", 300, 384 - 110, Team.Blue)); hv.FaceX = 0f; hv.FaceY = 9f;
            OzV11Kit.PlayerMove(w, m, 900, 384, false);
            var r = new Col();
            for (int i = 0; i < 2500; i++)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                    if (e.objId == m.Id && e.type == ZEventType.MissileFired)
                    {
                        var t = w.Get(e.otherId);
                        if (t != null && t.RamDoomBy == m.Id || col.Exists(c => c.Id == e.otherId && (c.RamDoomBy == m.Id || m.RamTargetId == c.Id))) r.atRam++;
                        else if (e.otherId == hv.Id) r.shotsOther++;
                    }
                w.Events.Clear();
                var a = m.AttackObject;
                if (a != null && (a.RamDoomBy == m.Id || (m.Ramming && m.RamTargetId == a.Id))) r.tgtRam++;
                if (col.All(c => c.Destroyed) && i > 10) break;
            }
            r.rams = col.Count(c => c.RamDoomBy == m.Id);
            return r;
        }
    }
}
