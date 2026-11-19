using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit ram_chain_n24_n25: the counted check of DV195 "the rammed stay still, the big tanks ram on" (Sim/ZObject.RamChain.cs), headless, on
    /// plain maps, the big-tank switches on as in a Play session (DV180-DV188, DV192 / DV193), RAM-FIRST (DV189) off so the rows measure DV195 alone.
    /// Every line a counted PASS / FAIL:
    ///  A (N24). A Bizon crushes every crushable kind (the ten vehicle types, the crane among them, driving at it; the four ground guns) and a
    ///     Mammoth rams every vehicle type (driving at it): from the contact to its death the target's position is unchanged (0 moved ticks, its
    ///     heading too), 0 shots, its owner's order sent every tick is refused (0 taken, 0 waypoints after any tick); the crane also standing and
    ///     driving across; DV195 off: the orders are taken (as before) and the Mammoth's rammed target is pushed (as before).
    ///  B (N25). A Bizon and a Mammoth, each facing a column of three enemy medium tanks and of three light tanks (nose to tail, 4 px apart), no
    ///     order: all three rammed / crushed in sequence, the next ram begun in the tick the last one ended (0 ticks without a ram from the first
    ///     start to the third contact), the speed at each hand-over within the ram's ramp (no stop: it moves on the hand-over ticks); the whole
    ///     column in less time than three single rams one after another (each single from its start to the kill, measured alone).
    ///  C (N25 limits). After a crush / ram on land the next one in front is never chained when it stands in water (DV192: RamWet), when it is a
    ///     friendly (it stays alive), when it stands outside the front cone (50 deg off the heading, within reach); nothing in front: the MOVE
    ///     order behind goes on (it arrives).
    ///  D. DV195 off: the column gets one ram / crush only (the old stop); deterministic: the Mammoth column twice: the same ticks and spots.
    /// Menu: OpenZ/V1.5/Ram Chain: Count (headless, DV195); the parity row DV195 runs <see cref="Quick"/>.
    /// </summary>
    public static class OzV15RamChainCount
    {
        public static string Quick(string settings) => Run(settings, false);

        static readonly string[] Big = { "MammothDeviation", "BizonDeviation", "MammothRamDeviation", "BizonCrushDeviation", "RamDuelDeviation", "CrushRollOffDeviation", "RamDebrisDeviation", "RamPolishDeviation", "BizonAmphibDeviation", "BigTankWreckDeviation" };
        static readonly string[] Vehicles = { "light", "medium", ZMTank.Type, "heavy", "missile_launcher", "jeep", "apc", "crane", ZBigTanks.Mammoth, ZBigTanks.Bizon };
        static readonly string[] Guns = { "gatling", "gun", "howitzer", "missile_cannon" };
        const int Seed = 707;

        static ulong Fnv(string s) { ulong h = 1469598103934665603UL; foreach (char c in s) { h ^= c; h *= 1099511628211UL; } return h; }
        static bool Get(string n) { var f = typeof(ZWorld).GetField(n); return f != null && (bool)f.GetValue(null); }
        static void Set(string n, bool v) => OzV11Kit.TrySet(typeof(ZWorld), n, v);

        public static string Run(string settings, bool full = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DV195 the rammed stay still, the big tanks ram on, counted (Sim/ZObject.RamChain.cs)");
            var keep = new Dictionary<string, bool>();
            foreach (var n in Big.Concat(new[] { "RamChainDeviation", "CalmStartDeviation", "BigTankRamFirstDeviation" })) keep[n] = Get(n);
            try
            {
                foreach (var n in Big) Set(n, true);
                Set("CalmStartDeviation", false); Set("BigTankRamFirstDeviation", false); Set("RamChainDeviation", true);
                // A
                foreach (var k in full ? Vehicles : new[] { "crane", "medium", ZBigTanks.Mammoth }) StillRow(sb, settings, true, k, "drive_at");
                foreach (var k in full ? Guns : new[] { "gun" }) StillRow(sb, settings, true, k, "still");
                if (full) { StillRow(sb, settings, true, "crane", "still"); StillRow(sb, settings, true, "crane", "drive_across"); }
                foreach (var k in full ? Vehicles : new[] { "crane", "heavy" }) StillRow(sb, settings, false, k, "drive_at");
                StillOff(sb, settings);
                // B
                foreach (var big in new[] { ZBigTanks.Bizon, ZBigTanks.Mammoth })
                    foreach (var kind in full ? new[] { "medium", "light" } : new[] { "medium" }) ChainRow(sb, settings, big, kind);
                // C
                foreach (var big in full ? new[] { ZBigTanks.Bizon, ZBigTanks.Mammoth } : new[] { ZBigTanks.Bizon })
                {
                    if (big == ZBigTanks.Bizon) LimitRow(sb, settings, big, "water");
                    LimitRow(sb, settings, big, "friend");
                    LimitRow(sb, settings, big, "cone");
                    LimitRow(sb, settings, big, "nothing");
                }
                // D
                ChainOff(sb, settings);
                if (full)
                {
                    var a = Column(settings, ZBigTanks.Mammoth, "medium", 3, 4, false); var b = Column(settings, ZBigTanks.Mammoth, "medium", 3, 4, false);
                    sb.AppendLine(OzV11Kit.Verdict(a.Print == b.Print, $"deterministic: the Mammoth column twice: {a.Print} / {b.Print}"));
                }
            }
            catch (Exception e) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace?.Split('\n').FirstOrDefault())); }
            finally { foreach (var kv in keep) Set(kv.Key, kv.Value); }
            return sb.ToString();
        }

        static ZWorld World(string settings, int w = 64, int h = 48, int waterX0 = -1, int waterX1 = -1)
        {
            var wd = ZWorld.Create(settings, waterX0 < 0 ? OzV11Kit.Plain(w, h) : PlainWater(w, h, waterX0, waterX1), Seed);
            wd.Bots.Add(new ZBot(wd, Team.Blue, Seed + 101) { Ignored = true });
            wd.Events.Clear();
            OzV11Kit.Dummy(wd, w * 16 - 40, h * 16 - 40);
            return wd;
        }

        /// <summary>OzV11Kit.Plain with the columns x0..x1 of water tiles (class 2), for the RamWet limit.</summary>
        static string PlainWater(int w, int h, int x0, int x1)
        {
            var cls = new string[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) cls[y * w + x] = x >= x0 && x <= x1 ? "2" : "0";
            var sb = new StringBuilder("{\"file\":\"v15_ramchain_water\",\"planet\":\"desert\",\"width\":" + w + ",\"height\":" + h + ",\"classes\":[");
            sb.Append(string.Join(",", cls)).Append("],\"tiles\":[").Append(string.Join(",", Enumerable.Repeat("0", w * h)));
            sb.Append("],\"zones\":[{\"x\":0,\"y\":0,\"w\":" + w + ",\"h\":" + h + "}],\"objects\":[]}");
            return sb.ToString();
        }

        static ZObject Big1(ZWorld w, string type, int cx, int cy)
        {
            var b = OzV11Kit.Place(w, type, cx, cy, Team.Red); b.FaceX = 9f; b.FaceY = 0f; b.MaxHealth = b.Health = 1000000000; b.Damage = 0;
            return b;
        }

        static ZObject PlaceAt(ZWorld w, string kind, int cx, int cy, Team team)
        {
            var t = OzV11Kit.Place(w, kind, cx, cy, team);
            t.SetCords(cx - (t.WidthPix >> 1), cy - (t.HeightPix >> 1));
            return t;
        }

        // ------------------------------------------------------------------ A (N24)
        sealed class Still { public long doom = -1, dead = -1; public int moved, turned, shots, wps, taken, tries, maxDrift; }

        static Still StillScene(string settings, bool crush, string kind, string how)
        {
            var w = World(settings);
            var b = Big1(w, crush ? ZBigTanks.Bizon : ZBigTanks.Mammoth, 320, 384);
            int ra = ZPathing.Radius(b);
            var t = PlaceAt(w, kind, 320, 384, Team.Blue);
            int rt = ZPathing.Radius(t);
            t.SetCords(320 + ra + rt + 20 - (t.WidthPix >> 1), 384 - (t.HeightPix >> 1));
            t.FaceX = how == "drive_across" ? 0f : -9f; t.FaceY = how == "drive_across" ? 9f : 0f;
            if (how == "drive_at") OzV11Kit.PlayerMove(w, t, 100, 384, false);
            if (how == "drive_across") OzV11Kit.PlayerMove(w, t, t.CenterX, 700, false);
            var r = new Still();
            double lx = 0, ly = 0; float fx = 0, fy = 0; int dx0 = 0, dy0 = 0;
            for (int i = 0; i < 1500; i++)
            {
                if (t.RamDoomBy >= 0 && OzV11Kit.Alive(t)) { r.tries++; if (w.SendWaypoints(Team.Blue, t, new[] { ZWaypoint.Move(t.CenterX, t.CenterY + 200, false) })) r.taken++; }
                w.Tick(ZConst.TICK);
                if (t.RamDoomBy >= 0 && r.doom < 0) { r.doom = w.TickIndex; lx = t.PosX; ly = t.PosY; fx = t.FaceX; fy = t.FaceY; dx0 = t.CenterX; dy0 = t.CenterY; }
                else if (r.doom >= 0 && r.dead < 0)
                {
                    foreach (var e in w.Events)
                        if (e.objId == t.Id && (e.type == ZEventType.MissileFired || e.type == ZEventType.Hitscan || e.type == ZEventType.DriverShot || e.type == ZEventType.MechHandGun)) r.shots++;
                    if (!t.Destroyed)
                    {
                        if (t.PosX != lx || t.PosY != ly) r.moved++;
                        if (t.FaceX != fx || t.FaceY != fy) r.turned++;
                        lx = t.PosX; ly = t.PosY; fx = t.FaceX; fy = t.FaceY;
                        r.maxDrift = Math.Max(r.maxDrift, Math.Abs(t.CenterX - dx0) + Math.Abs(t.CenterY - dy0));
                        if (t.Waypoints.Count > 0) r.wps++;
                    }
                }
                foreach (var e in w.Events) if (e.type == ZEventType.Death && e.objId == t.Id && r.dead < 0) r.dead = w.TickIndex;
                w.Events.Clear();
                if (r.dead >= 0) break;
            }
            return r;
        }

        static void StillRow(StringBuilder sb, string settings, bool crush, string kind, string how)
        {
            var r = StillScene(settings, crush, kind, how);
            bool ok = r.doom > 0 && r.dead > r.doom && r.moved == 0 && r.turned == 0 && r.shots == 0 && r.taken == 0 && r.tries > 0 && r.wps == 0;
            sb.AppendLine(OzV11Kit.Verdict(ok, $"N24: a {(crush ? "Bizon crushes" : "Mammoth rams")} a {kind} ({how.Replace('_', ' ')}): from the contact to its death ({r.dead - r.doom} ticks): moved {r.moved} ticks (drift {r.maxDrift} px), turned {r.turned}, shots {r.shots}, its owner's orders taken {r.taken} of {r.tries}, ticks with a waypoint {r.wps}"));
        }

        static void StillOff(StringBuilder sb, string settings)
        {
            Set("RamChainDeviation", false);
            Still c, m;
            try { c = StillScene(settings, true, "crane", "drive_at"); m = StillScene(settings, false, "heavy", "drive_at"); }
            finally { Set("RamChainDeviation", true); }
            sb.AppendLine(OzV11Kit.Verdict(c.taken == c.tries && c.tries > 0 && m.maxDrift > 0, $"N24, DV195 off (as before): the crushed crane's orders taken {c.taken} of {c.tries}; the Mammoth's rammed heavy pushed {m.maxDrift} px ({m.moved} moved ticks) before its death"));
        }

        // ------------------------------------------------------------------ B (N25)
        sealed class Col
        {
            public List<long> starts = new List<long>(), hits = new List<long>(), deaths = new List<long>();
            public long first = -1, last = -1; public int noRam, noMove, chains, sameTick, early, handStill, n; public double maxHandDrop;
            public string Print = "";
        }

        /// <summary>A big tank at (200, 384) facing +x, n enemy tanks of `kind` in a column in front of it (the first 20 px from its hull, then gap px
        /// apart), no order (or a MOVE order through them).</summary>
        static Col Column(string settings, string big, string kind, int n, int gap, bool order)
        {
            var w = World(settings, 80, 48);
            var b = Big1(w, big, 200, 384);
            int x = 200 + ZPathing.Radius(b);
            var ts = new List<ZObject>();
            for (int k = 0; k < n; k++)
            {
                var t = PlaceAt(w, kind, 0, 384, Team.Blue); int rt = ZPathing.Radius(t);
                x += (k == 0 ? 20 : gap) + rt;
                t.SetCords(x - (t.WidthPix >> 1), 384 - (t.HeightPix >> 1)); t.FaceX = -9f; t.FaceY = 0f; t.Damage = 0;
                x += rt; ts.Add(t);
            }
            if (order) OzV11Kit.PlayerMove(w, b, 1100, 384, false);
            var r = new Col { n = n };
            double px = b.PosX, py = b.PosY, spd = 0;
            var pr = new StringBuilder();
            for (int i = 0; i < 3000; i++)
            {
                int s0 = r.starts.Count;
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events)
                {
                    if ((e.type == ZEventType.BizonCrush || e.type == ZEventType.MammothRam) && e.objId == b.Id) { if (e.flag) r.hits.Add(w.TickIndex); else r.starts.Add(w.TickIndex); }
                    if (e.type == ZEventType.Death && ts.Any(t => t.Id == e.objId)) r.deaths.Add(w.TickIndex);
                }
                w.Events.Clear();
                double dx = b.PosX - px, dy = b.PosY - py, sp = Math.Sqrt(dx * dx + dy * dy); px = b.PosX; py = b.PosY;
                if (r.starts.Count > 0 && r.first < 0) r.first = w.TickIndex;
                if (r.first >= 0 && r.hits.Count < n)
                {
                    if (!b.Ramming) r.noRam++;
                    if (sp <= 0) r.noMove++;
                }
                if (r.starts.Count > s0 && s0 > 0)
                {   // a hand-over: its speed the tick before and in this tick (px / tick)
                    if (sp <= 0) r.handStill++;
                    r.maxHandDrop = Math.Max(r.maxHandDrop, spd - sp);
                }
                spd = sp;
                if (i % 10 == 9) pr.Append(b.CenterX).Append(',').Append(b.CenterY).Append(';');
                if (r.deaths.Count >= n && r.last < 0) r.last = w.TickIndex;
                if (r.last >= 0 && w.TickIndex > r.last + 50) break;
            }
            r.chains = w.RamChains; r.sameTick = w.RamChainSameTick; r.early = w.RamChainEarlyEnds;
            r.Print = $"starts {string.Join(",", r.starts)} hits {string.Join(",", r.hits)} deaths {string.Join(",", r.deaths)} way {Fnv(pr.ToString()):x16}";
            return r;
        }

        static void ChainRow(StringBuilder sb, string settings, string big, string kind)
        {
            var c = Column(settings, big, kind, 3, 4, false);
            long single = 0; bool singles = true;
            for (int k = 0; k < 3; k++) { var s = Column(settings, big, kind, 1, 4, false); if (s.deaths.Count < 1 || s.first < 0) singles = false; else single += s.deaths[0] - s.first; }
            long total = c.first >= 0 && c.last >= 0 ? c.last - c.first : -1;
            string rel(List<long> l) => string.Join(",", l.Select(v => v - c.first));
            bool ok = c.starts.Count == 3 && c.hits.Count == 3 && c.deaths.Count == 3 && c.noRam == 0 && c.handStill == 0 && c.chains == 2 && singles && total > 0 && total < single;
            sb.AppendLine(OzV11Kit.Verdict(ok, $"N25: a {big} and 3 enemy {kind} tanks in a column (4 px apart), no order: starts {c.starts.Count} [{rel(c.starts)}], contacts {c.hits.Count} [{rel(c.hits)}], deaths {c.deaths.Count} [{rel(c.deaths)}] (ticks from the first start); chains {c.chains} (same tick {c.sameTick}, held drives ended early {c.early}); ticks without a ram from the first start to the third contact {c.noRam}, standing ticks {c.noMove}, standing at a hand-over {c.handStill}, largest speed drop at a hand-over {c.maxHandDrop:0.00} px/tick; the whole column {total} ticks vs three single rams {single} ticks"));
        }

        // ------------------------------------------------------------------ C (N25 limits)
        static void LimitRow(StringBuilder sb, string settings, string big, string what)
        {
            ZWorld w; int x0 = 200;
            if (what == "water") w = World(settings, 80, 48, 22, 30); else w = World(settings, 80, 48);
            var b = Big1(w, big, x0, 384);
            int ra = ZPathing.Radius(b);
            var t1 = PlaceAt(w, "medium", 0, 384, Team.Blue); int r1 = ZPathing.Radius(t1);
            int c1 = x0 + ra + 20 + r1; t1.SetCords(c1 - (t1.WidthPix >> 1), 384 - (t1.HeightPix >> 1)); t1.FaceX = -9f; t1.FaceY = 0f; t1.Damage = 0;
            ZObject t2 = null;
            if (what == "water") { t2 = PlaceAt(w, "medium", 23 * 16 + 8, 384, Team.Blue); }
            if (what == "friend") { t2 = PlaceAt(w, "medium", c1 + r1 + 4 + 18, 384, Team.Red); }
            if (what == "cone") { int d = r1 + 4 + 18 + 30; t2 = PlaceAt(w, "medium", c1 + d * 64 / 100, 384 + d * 77 / 100, Team.Blue); }   // 50 deg off the heading (cos 0.64, sin 0.77)
            if (t2 != null) { t2.FaceX = -9f; t2.FaceY = 0f; t2.Damage = 0; t2.MaxHealth = t2.Health = 1000000000; }
            const int goalX = 520;
            if (what == "nothing") OzV11Kit.PlayerMove(w, b, goalX, 384, false);
            int hits = 0; bool t1Dead = false, arrived = false;
            for (int i = 0; i < (what == "nothing" ? 8000 : 1500); i++)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events) { if ((e.type == ZEventType.BizonCrush || e.type == ZEventType.MammothRam) && e.flag && e.objId == b.Id) hits++; if (e.type == ZEventType.Death && e.objId == t1.Id) t1Dead = true; }
                w.Events.Clear();
                if (what == "nothing" && t1Dead && b.Waypoints.Count == 0 && Math.Abs(b.CenterX - goalX) <= 24 && Math.Abs(b.CenterY - 384) <= 24) { arrived = true; break; }
            }
            bool t2Alive = t2 != null && OzV11Kit.Alive(t2) && t2.RamDoomBy < 0;
            bool ok = what == "nothing" ? t1Dead && hits == 1 && w.RamChains == 0 && arrived : t1Dead && hits == 1 && w.RamChains == 0 && t2Alive;
            string desc = what == "water" ? "the next enemy medium tank stands in water (DV192: never rammed in water)" : what == "friend" ? "a friendly medium tank right behind it" :
                          what == "cone" ? "the next enemy medium tank 50 deg off its heading within reach (outside the front cone)" : "nothing in front, a MOVE order behind the ram";
            sb.AppendLine(OzV11Kit.Verdict(ok, $"N25 limits: a {big} rams / crushes a medium tank, {desc}: contacts {hits}, chains {w.RamChains}, the first destroyed {t1Dead}" + (t2 != null ? $", the second untouched {t2Alive}" : $", the order went on: arrived {arrived}")));
        }

        // ------------------------------------------------------------------ D
        static void ChainOff(StringBuilder sb, string settings)
        {
            Set("RamChainDeviation", false);
            Col c;
            try { c = Column(settings, ZBigTanks.Bizon, "medium", 3, 4, false); }
            finally { Set("RamChainDeviation", true); }
            sb.AppendLine(OzV11Kit.Verdict(c.chains == 0 && c.hits.Count < 3 && c.noRam > ZObject.RamLookTicks, $"DV195 off (as before): the Bizon and the column, no order: contacts {c.hits.Count} of 3 in 30 s, chains {c.chains}, ticks without a ram between the contacts {c.noRam} (the old stop and the slide round the second tank)"));
        }
    }
}
