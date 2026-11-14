using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Counted check of 1.2 item 9 (rule DV135, View/OzShiftAppend.cs: a Shift queue is added to the order the units already have).
    /// Headless: no scene, no Play mode, nothing saved. The player's clicks are sent exactly as PlayerInput.Flush sends them: the waypoint
    /// of a right-click on a flag (PlayerInput.BuildWaypoint: a move onto the flag, attack-move unless enemies are near), the queue's prefix
    /// (OzShiftAppend.Prefix, what OzShuttleQueueInput.Prefix calls first), then ZWorld.SendWaypoints for each unit of the selection.
    /// Open ground 96 x 64 tiles in three zones: the units start in the west (tile 20, 40), flag 1 stands north-east of them (tile 40, 14),
    /// flag 2 far east (tile 70, 48), so the straight way to flag 2 passes 460 px from flag 1 (nothing takes flag 1 on the way by itself).
    /// Red is the player's team; blue a computer player that gives no orders (a dummy far away keeps the game going). The switches of a
    /// game in Play (OzV11Kit.GameSwitches plus the rules View/ZGame.cs turns on at a start), the calm start off.
    /// <para>A, the case reported: a plain click on flag 1, then, while the units are on their way (<see cref="ShiftAfter"/> s later), a
    /// Shift click on flag 2. For a grunt squad, two jeeps, and a mixed group (a grunt squad, a psycho squad, two jeeps, a light tank), the
    /// rule off (Zod: the cause) and on.</para>
    /// <para>B: two Shift clicks from standing (flag 1, flag 2): the same with the rule off and on. C: two plain clicks: the second replaces
    /// the first, the same with the rule off and on.</para>
    /// <para>D: movement-layer moves in front of the order at the Shift click (a step aside; a back-off carrying the player's mark): never
    /// copied. E: a first waypoint that cannot be overwritten (a forced move): kept once, not doubled. F: a patrol: the queue replaces it
    /// (it never ends). G: the prefix itself: a plain queue, a squad member, the rule off, a destroyed unit: nothing changes.</para>
    /// Deterministic: every row is run twice (the second time must give the same lines).
    /// </summary>
    public static class OzV12ShiftAppendCount
    {
        public const int Runs = 3;
        /// <summary>A: seconds after the plain click on flag 1 that the Shift click on flag 2 comes (the units on their way).</summary>
        public const double ShiftAfter = 2.0;
        /// <summary>Longest run (s of game time) before a flag still not taken counts as never.</summary>
        public const double MaxSec = 180.0;
        /// <summary>A unit "went to flag 1" when, before flag 2 was taken, it came this close (px) to the flag, or <see cref="SpotPx"/> to its
        /// own place there (a group order gives each unit its own spot round the point; a crowd's arrival may end up to 64 px short of it).
        /// The straight way to flag 2 passes 460 px from flag 1.</summary>
        public const int NearPx = 80, SpotPx = 72;

        static double Dist(ZObject u, (int x, int y) p) => Math.Sqrt((double)(u.CenterX - p.x) * (u.CenterX - p.x) + (double)(u.CenterY - p.y) * (u.CenterY - p.y));

        const int MapW = 96, MapH = 64;
        static readonly (int x, int y) StartT = (20, 40), Flag1T = (40, 14), Flag2T = (70, 48), PatrolT = (20, 20);

        /// <summary>The rules View/ZGame.cs turns on at every start that OzV11Kit.GameSwitches leaves off (each only when the build has it).</summary>
        static readonly string[] PlayRules = { "CalmMoveDeviation", "FortRunDeviation", "RockRoundDeviation", "CalmFollowDeviation", "GrenadeBoostDeviation",
            "VeteranHealDeviation", "FortSniperDeviation", "SniperHeadshotDeviation", "SniperSteadyDeviation", "ToughSteadyDeviation", "HeavyFallDeviation",
            "GroundFireDeviation", "ProneTargetDeviation", "HeroBoostDeviation", "JeepChargeDeviation", "DriverFireDeviation", "NoAutoFlagUnderFireDeviation", "SniperBalanceDeviation" };

        enum Kind { ShiftAfterPlain, TwoShift, TwoPlain, Helpers, KeptFirst, Patrol }
        enum Group { Squad, Jeeps, Mixed, SquadJeep, Jeep }

        sealed class Row { public string name; public Kind kind; public Group group; public bool[] rule; }

        /// <summary>What one run measured.</summary>
        sealed class Run
        {
            public string lists = "", wanted = "";
            public bool listOk, onWay = true, patrolEnded = true, helperCopied, exception;
            public int forcedCount = -1, heads, wentTo1;
            public double t1 = -1, t2 = -1, clickAt = -1;
            public string Line() => $"lists after the 2nd click {lists} (wanted {wanted}: {(listOk ? "yes" : "NO")}); on their way at the click {(onWay ? "yes" : "no")}; " +
                                    $"flag 1 taken {T(t1)}, flag 2 taken {T(t2)}; went to flag 1 before flag 2 was taken: {wentTo1} of {heads}" +
                                    (forcedCount >= 0 ? $"; forced first move in the list {forcedCount}x" : "") + (helperCopied ? "; a movement-layer move was COPIED" : "");
        }

        static string F(double v) => v < 0 ? "never" : v.ToString("0.00", CultureInfo.InvariantCulture);
        static string T(double v) => v < 0 ? "never" : F(v) + " s";
        static int Px((int x, int y) t, bool y) => (y ? t.y : t.x) * 16 + 8;

        static Row[] AllRows()
        {
            var both = new[] { false, true }; var on = new[] { true };
            return new[]
            {
                new Row { name = "A grunt squad: plain flag 1, then Shift flag 2", kind = Kind.ShiftAfterPlain, group = Group.Squad, rule = both },
                new Row { name = "A two jeeps: plain flag 1, then Shift flag 2", kind = Kind.ShiftAfterPlain, group = Group.Jeeps, rule = both },
                new Row { name = "A mixed group: plain flag 1, then Shift flag 2", kind = Kind.ShiftAfterPlain, group = Group.Mixed, rule = both },
                new Row { name = "B mixed group: Shift flag 1 + Shift flag 2 from standing", kind = Kind.TwoShift, group = Group.Mixed, rule = both },
                new Row { name = "C mixed group: plain flag 1, then plain flag 2", kind = Kind.TwoPlain, group = Group.Mixed, rule = both },
                new Row { name = "D squad + jeep: movement-layer moves in front at the Shift click", kind = Kind.Helpers, group = Group.SquadJeep, rule = on },
                new Row { name = "E jeep: a first move that cannot be overwritten at the Shift click", kind = Kind.KeptFirst, group = Group.Jeep, rule = on },
                new Row { name = "F grunt squad on patrol, then Shift flag 2", kind = Kind.Patrol, group = Group.Squad, rule = on },
            };
        }

        /// <summary>The whole check: the report, ending in PASS / FAIL lines.</summary>
        public static string Check(string settings, int runs = Runs, Action<string> progress = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"1.2 item 9 (DV135) a Shift queue is added to the units' current order; {runs} runs per row (start spots moved by the seed); " +
                          $"the Shift click {ShiftAfter:0.0} s after the plain one; flag 1 at tile {Flag1T}, flag 2 at tile {Flag2T}, the units from tile {StartT}; " +
                          $"the rules of a game in Play on ({string.Join(", ", PlayRules.Where(n => typeof(ZWorld).GetField(n) != null).Select(n => n.Replace("Deviation", "")))}).");
            var rows = AllRows();
            var res = new Dictionary<(string, bool), List<Run>>();
            var lines = new List<string>();
            bool exception = false;
            foreach (var row in rows)
                foreach (bool rule in row.rule)
                {
                    progress?.Invoke(row.name);
                    var list = new List<Run>();
                    for (int seed = 1; seed <= runs; seed++)
                    {
                        var r = Play(settings, row, seed, rule);
                        if (r.exception) exception = true;
                        list.Add(r);
                        string l = $"{row.name} | rule {(rule ? "on " : "off")} | seed {seed} | {r.Line()}";
                        sb.AppendLine(l); lines.Add(l);
                    }
                    res[(row.name, rule)] = list;
                }
            bool same = true; int k = 0;                                                       // determinism: every row once more
            foreach (var row in rows)
                foreach (bool rule in row.rule)
                    for (int seed = 1; seed <= runs; seed++)
                    {
                        string l = $"{row.name} | rule {(rule ? "on " : "off")} | seed {seed} | {Play(settings, row, seed, rule).Line()}";
                        if (k >= lines.Count || lines[k] != l) same = false;
                        k++;
                    }
            string direct = Direct(settings, out bool directOk);
            sb.AppendLine("G " + direct);
            sb.AppendLine();

            List<Run> R(Kind kind, bool rule) => rows.Where(r => r.kind == kind).SelectMany(r => res.TryGetValue((r.name, rule), out var l) ? l : new List<Run>()).ToList();
            bool InOrder(Run r) => r.t1 >= 0 && r.t2 >= 0 && r.t1 < r.t2;
            var aOff = R(Kind.ShiftAfterPlain, false); var aOn = R(Kind.ShiftAfterPlain, true);
            sb.AppendLine(OzV11Kit.Verdict(aOff.Count > 0 && aOff.All(r => r.listOk && r.onWay && !InOrder(r) && r.wentTo1 == 0),
                $"P1 the cause (rule off, as before): after a plain click on flag 1 and a Shift click on flag 2 while on the way, every unit's list held flag 2 only and flag 1 was dropped: " +
                $"{aOff.Count(r => r.listOk)} of {aOff.Count} runs list [flag2], on the way at the click {aOff.Count(r => r.onWay)}, flag 1 taken before flag 2 in {aOff.Count(InOrder)}, units that went to flag 1 {aOff.Sum(r => r.wentTo1)}"));
            sb.AppendLine(OzV11Kit.Verdict(aOn.Count > 0 && aOn.All(r => r.listOk && r.onWay && InOrder(r) && r.wentTo1 == r.heads),
                $"P2 the fix (rule on): the same clicks send [flag1, flag2] to every unit (no point doubled) and the units take flag 1, then flag 2: " +
                $"lists right {aOn.Count(r => r.listOk)} of {aOn.Count} runs, flag 1 before flag 2 {aOn.Count(InOrder)} of {aOn.Count}, units that went to flag 1 first {aOn.Sum(r => r.wentTo1)} of {aOn.Sum(r => r.heads)}; " +
                $"flag 2 taken after {F(aOn.Where(r => r.t2 >= 0).Select(r => r.t2 - r.clickAt).DefaultIfEmpty(-1).Min())}-{F(aOn.Where(r => r.t2 >= 0).Select(r => r.t2 - r.clickAt).DefaultIfEmpty(-1).Max())} s from the Shift click (a squad, two jeeps, a mixed group)"));
            var bOff = R(Kind.TwoShift, false); var bOn = R(Kind.TwoShift, true);
            sb.AppendLine(OzV11Kit.Verdict(bOff.Count > 0 && bOn.Count == bOff.Count && bOn.Concat(bOff).All(r => r.listOk && InOrder(r) && r.wentTo1 == r.heads) && Same(bOff, bOn),
                $"P3 two Shift clicks from standing: [flag1, flag2], flag 1 then flag 2, the same lines with the rule off and on (unchanged): {bOn.Count(InOrder)} + {bOff.Count(InOrder)} of {bOn.Count + bOff.Count} runs in order"));
            var cOff = R(Kind.TwoPlain, false); var cOn = R(Kind.TwoPlain, true);
            sb.AppendLine(OzV11Kit.Verdict(cOff.Count > 0 && cOn.Count == cOff.Count && cOn.Concat(cOff).All(r => r.listOk && r.t2 >= 0 && !InOrder(r) && r.wentTo1 == 0) && Same(cOff, cOn),
                $"P4 a plain click still replaces the order: [flag2] only, flag 1 dropped, the same lines with the rule off and on: {cOn.Count(r => r.listOk) + cOff.Count(r => r.listOk)} of {cOn.Count + cOff.Count} runs"));
            var d = R(Kind.Helpers, true);
            sb.AppendLine(OzV11Kit.Verdict(d.Count > 0 && d.All(r => r.listOk && !r.helperCopied && InOrder(r)),
                $"P5 movement-layer moves in front of the order (a step aside; a back-off with the player's mark) are not copied: lists [flag1, flag2] in {d.Count(r => r.listOk && !r.helperCopied)} of {d.Count} runs, flag 1 then flag 2 in {d.Count(InOrder)}"));
            var e = R(Kind.KeptFirst, true);
            sb.AppendLine(OzV11Kit.Verdict(e.Count > 0 && e.All(r => r.listOk && r.forcedCount == 1 && InOrder(r)),
                $"P6 a first move that cannot be overwritten stays once (not doubled): [forced, flag1, flag2] in {e.Count(r => r.listOk && r.forcedCount == 1)} of {e.Count} runs, flag 1 then flag 2 in {e.Count(InOrder)}"));
            var f = R(Kind.Patrol, true);
            sb.AppendLine(OzV11Kit.Verdict(f.Count > 0 && f.All(r => r.listOk && r.patrolEnded && r.t2 >= 0 && r.t1 < 0),
                $"P7 a patrol is replaced by the Shift queue, as before (it never ends): [flag2] and the patrol ended in {f.Count(r => r.listOk && r.patrolEnded)} of {f.Count} runs, flag 2 taken {f.Count(r => r.t2 >= 0)}"));
            sb.AppendLine(OzV11Kit.Verdict(directOk, "P8 the prefix leaves alone a plain queue, a squad member, a queue with the rule off and a destroyed unit (G above)"));
            sb.AppendLine(OzV11Kit.Verdict(!exception, "P9 no exception"));
            sb.AppendLine(OzV11Kit.Verdict(same, "P10 deterministic: every row run twice, the same lines"));
            sb.AppendLine(OzV11Kit.Verdict(ZWorld.ShiftAppendDeviation, "P11 the rule's switch is on outside the runs (put back by the check; the game sets it at every start, off in 'Zod exact')"));
            return sb.ToString();
        }

        static bool Same(List<Run> a, List<Run> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i].Line() != b[i].Line()) return false;
            return true;
        }

        // ------------------------------------------------------------------ the ground
        /// <summary>Open ground in three zones (west: the start; middle: flag 1; east: flag 2), both flags free.</summary>
        static string Ground()
        {
            var sb = new StringBuilder("{\"file\":\"v12_shift_append\",\"planet\":\"desert\",\"width\":" + MapW + ",\"height\":" + MapH + ",\"classes\":[");
            sb.Append(string.Join(",", Enumerable.Repeat("0", MapW * MapH))).Append("],\"tiles\":[").Append(string.Join(",", Enumerable.Repeat("0", MapW * MapH)));
            sb.Append("],\"zones\":[{\"x\":0,\"y\":0,\"w\":32,\"h\":" + MapH + "},{\"x\":32,\"y\":0,\"w\":32,\"h\":" + MapH + "},{\"x\":64,\"y\":0,\"w\":32,\"h\":" + MapH + "}],\"objects\":[");
            sb.Append("{\"x\":" + Flag1T.x + ",\"y\":" + Flag1T.y + ",\"owner\":\"null\",\"kind\":\"map_item:flag\",\"ot\":0,\"oid\":0,\"blevel\":0,\"extra_links\":0,\"health_percent\":100},");
            sb.Append("{\"x\":" + Flag2T.x + ",\"y\":" + Flag2T.y + ",\"owner\":\"null\",\"kind\":\"map_item:flag\",\"ot\":0,\"oid\":0,\"blevel\":0,\"extra_links\":0,\"health_percent\":100}");
            return sb.Append("]}").ToString();
        }

        /// <summary>The selection of a row: leaders and vehicles (what PlayerInput.Selection holds), placed round the start spot.</summary>
        static List<ZObject> Place(ZWorld w, Group g, int sx, int sy)
        {
            var l = new List<ZObject>();
            switch (g)
            {
                case Group.Squad: l.Add(OzV11Kit.Place(w, "grunt", sx, sy, Team.Red, 4)); break;
                case Group.Jeeps: l.Add(OzV11Kit.Place(w, "jeep", sx, sy - 24, Team.Red)); l.Add(OzV11Kit.Place(w, "jeep", sx, sy + 24, Team.Red)); break;
                case Group.Jeep: l.Add(OzV11Kit.Place(w, "jeep", sx, sy, Team.Red)); break;
                case Group.SquadJeep: l.Add(OzV11Kit.Place(w, "grunt", sx, sy - 24, Team.Red, 4)); l.Add(OzV11Kit.Place(w, "jeep", sx, sy + 32, Team.Red)); break;
                default:
                    l.Add(OzV11Kit.Place(w, "grunt", sx - 24, sy - 32, Team.Red, 4)); l.Add(OzV11Kit.Place(w, "psycho", sx - 24, sy + 32, Team.Red, 3));
                    l.Add(OzV11Kit.Place(w, "jeep", sx + 24, sy - 40, Team.Red)); l.Add(OzV11Kit.Place(w, "jeep", sx + 24, sy + 40, Team.Red));
                    l.Add(OzV11Kit.Place(w, "light", sx + 56, sy, Team.Red));
                    break;
            }
            return l;
        }

        /// <summary>View/PlayerInput.cs BuildWaypoint for a right-click on a flag (no modifier key): a move onto it, attack-move unless enemies are near.</summary>
        static ZWaypoint FlagClick(ZWorld w, ZObject u, ZObject flag)
        {
            var wp = ZWaypoint.Move(flag.CenterX, flag.CenterY, !NearHostiles(w, u));
            wp.refId = flag.Id;
            return wp;
        }

        /// <summary>View/PlayerInput.cs NearHostiles: an enemy unit within the unit's attack range + agro distance.</summary>
        static bool NearHostiles(ZWorld w, ZObject u)
        {
            double r = u.AttackRadius + w.S.agro_distance;
            foreach (var o in w.Objects)
                if ((o.Kind == ZKind.Robot || o.Kind == ZKind.Vehicle || o.Kind == ZKind.Cannon) && o.Owner != Team.Neutral && o.Owner != u.Owner && !o.Destroyed
                    && (o.CenterX - u.CenterX) * (double)(o.CenterX - u.CenterX) + (o.CenterY - u.CenterY) * (double)(o.CenterY - u.CenterY) <= r * r)
                    return true;
            return false;
        }

        /// <summary>PlayerInput.Flush as it sends a queue: the clicks' points for every unit of the selection, the prefix (shift = a Shift
        /// queue), then SEND_WAYPOINTS for each.</summary>
        static void Send(ZWorld w, List<ZObject> sel, bool shift, params ZObject[] flags)
        {
            var pending = new Dictionary<ZObject, List<ZWaypoint>>();
            foreach (var u in sel) { var l = new List<ZWaypoint>(); foreach (var fl in flags) l.Add(FlagClick(w, u, fl)); pending[u] = l; }
            OzShiftAppend.Prefix(pending, shift);
            foreach (var kv in pending) w.SendWaypoints(Team.Red, kv.Key, kv.Value);
        }

        static string Token(ZWaypoint wp, ZObject f1, ZObject f2) =>
            wp.mode == WpMode.ForceMove ? "forced" : wp.refId < -1 ? "helper" : wp.refId == f1.Id ? "flag1" : wp.refId == f2.Id ? "flag2" : "point";

        static void Tick(ZWorld w) { w.Tick(ZConst.TICK); w.Events.Clear(); }

        // ------------------------------------------------------------------ one run
        static Run Play(string settings, Row row, int seed, bool rule)
        {
            var R = new Run();
            try
            {
                using (OzV11Kit.GameSwitches())
                {
                    foreach (var n in PlayRules) OzV11Kit.TrySet(typeof(ZWorld), n, true);
                    ZWorld.CalmStartDeviation = false;
                    ZWorld.ShiftAppendDeviation = rule;
                    var rng = new Random(seed * 7919 + 31);
                    int sx = Px(StartT, false) + rng.Next(33) - 16, sy = Px(StartT, true) + rng.Next(33) - 16;
                    var w = OzV11Kit.World(settings, Ground(), seed);
                    OzV11Kit.Dummy(w, MapW * 16 - 56, 56);
                    ZObject f1 = w.Flags.First(o => (o.CenterX >> 4) == Flag1T.x || (o.LocX >> 4) == Flag1T.x), f2 = w.Flags.First(o => o != f1);
                    var sel = Place(w, row.group, sx, sy);
                    R.heads = sel.Count;
                    for (int i = 0; i < 50; i++) Tick(w);                                           // 0.5 s: the units stand
                    switch (row.kind)
                    {
                        case Kind.TwoShift: Send(w, sel, true, f1, f2); R.clickAt = w.Time; break;
                        case Kind.Patrol: foreach (var u in sel) w.SendPatrol(Team.Red, u, Px(PatrolT, false), Px(PatrolT, true)); break;
                        default: Send(w, sel, false, f1); break;                                     // the plain click on flag 1
                    }
                    double second = w.Time + ShiftAfter;
                    bool sent = row.kind == Kind.TwoShift;
                    var nearest = sel.ToDictionary(u => u, u => double.MaxValue);
                    var spot = sel.ToDictionary(u => u, u => (x: f1.CenterX, y: f1.CenterY));      // each unit's own place at flag 1 (a group order's spot), read at the click
                    if (sent) foreach (var u in sel) foreach (var q in u.Waypoints) if (q.refId == f1.Id) { spot[u] = (q.x, q.y); break; }
                    while (w.Time < MaxSec && !w.GameOver)
                    {
                        if (!sent && w.Time >= second - 1e-9)
                        {
                            sent = true; R.clickAt = w.Time;
                            foreach (var u in sel)
                            {
                                foreach (var q in u.Waypoints) if (q.refId == f1.Id) { spot[u] = (q.x, q.y); break; }
                                if (row.kind != Kind.Patrol && Dist(u, spot[u]) <= SpotPx) R.onWay = false;   // still on the way to flag 1 at the click
                            }
                            if (row.kind == Kind.Helpers)
                            {   // the movement layer's own moves in front of the order: a step aside (as ZSeparation adds it) and, on the jeep,
                                // a back-off that carries the player's mark (only its refId tells it apart)
                                var a = sel[0]; a.Waypoints.Insert(0, ZWaypoint.Move(a.CenterX + 16, a.CenterY, false, ZPathing.RefStepAside));
                                var b = sel[1]; var bo = ZWaypoint.Move(b.CenterX - 16, b.CenterY, false, ZPathing.RefBackOff); bo.playerGiven = true; b.Waypoints.Insert(0, bo);
                            }
                            if (row.kind == Kind.KeptFirst)
                            {   // a forced move in front (as an exit lane gives it): SEND_WAYPOINTS keeps it by itself
                                var a = sel[0]; a.Waypoints.Insert(0, new ZWaypoint { mode = WpMode.ForceMove, refId = -1, x = a.CenterX + 24, y = a.CenterY - 24, playerGiven = true });
                            }
                            Send(w, sel, row.kind != Kind.TwoPlain, f2);                             // the second click (Shift unless row C)
                        }
                        if (sent && R.lists.Length == 0)
                        {   // the lists right after the second click (row B: after its one send)
                            var parts = new List<string>(); bool ok = true; int forced = 0; string want = "";
                            foreach (var u in sel)
                            {
                                var toks = u.Waypoints.Select(q => Token(q, f1, f2)).ToList();
                                forced += toks.Count(t => t == "forced");
                                if (toks.Contains("helper")) R.helperCopied = true;
                                var expect = row.kind == Kind.KeptFirst ? new[] { "forced", "flag1", "flag2" }
                                           : row.kind == Kind.TwoPlain || row.kind == Kind.Patrol || (row.kind == Kind.ShiftAfterPlain && !rule) ? new[] { "flag2" }
                                           : new[] { "flag1", "flag2" };
                                want = "[" + string.Join(", ", expect) + "]";
                                if (!toks.SequenceEqual(expect)) ok = false;
                                if (row.kind == Kind.Patrol && u.Patrolling) R.patrolEnded = false;
                                parts.Add($"{u.Type} [{string.Join(", ", toks)}]");
                            }
                            R.lists = string.Join(" ", parts); R.wanted = want; R.listOk = ok;
                            if (row.kind == Kind.KeptFirst) R.forcedCount = forced;
                        }
                        Tick(w);
                        if (R.t1 < 0 && f1.Owner == Team.Red) R.t1 = w.Time;
                        if (R.t2 < 0 && f2.Owner == Team.Red) R.t2 = w.Time;
                        if (R.t2 < 0)
                            foreach (var u in sel)
                            {
                                if (!OzV11Kit.Alive(u)) continue;
                                double dd = Math.Min(Dist(u, (f1.CenterX, f1.CenterY)) * SpotPx / NearPx, Dist(u, spot[u]));   // (in SpotPx terms: the flag within NearPx, or its own spot within SpotPx)
                                if (dd < nearest[u]) nearest[u] = dd;
                            }
                        if (R.t2 >= 0 && w.Time > R.t2 + 1.0) break;
                    }
                    R.wentTo1 = sel.Count(u => nearest[u] <= SpotPx);
                }
            }
            catch (Exception ex) { R.exception = true; R.lists = "EXCEPTION " + ex.GetType().Name + ": " + ex.Message; }
            return R;
        }

        // ------------------------------------------------------------------ G: the prefix itself
        static string Direct(string settings, out bool ok)
        {
            ok = false;
            try
            {
                using (OzV11Kit.GameSwitches())
                {
                    ZWorld.CalmStartDeviation = false; ZWorld.ShiftAppendDeviation = true;
                    var w = OzV11Kit.World(settings, Ground(), 1);
                    OzV11Kit.Dummy(w, MapW * 16 - 56, 56);
                    var f1 = w.Flags[0]; var f2 = w.Flags[1];
                    var squad = OzV11Kit.Place(w, "grunt", Px(StartT, false), Px(StartT, true), Team.Red, 3);
                    var dead = OzV11Kit.Place(w, "jeep", Px(StartT, false) + 64, Px(StartT, true), Team.Red);
                    Send(w, new List<ZObject> { squad, dead }, false, f1);
                    var minion = squad.Minions[0];
                    var p = new Dictionary<ZObject, List<ZWaypoint>> { [squad] = new List<ZWaypoint> { FlagClick(w, squad, f2) } };
                    int plain = OzShiftAppend.Prefix(p, false); int plainLen = p[squad].Count;
                    var pm = new Dictionary<ZObject, List<ZWaypoint>> { [minion] = new List<ZWaypoint> { FlagClick(w, minion, f2) } };
                    int mini = OzShiftAppend.Prefix(pm, true); int miniLen = pm[minion].Count;
                    ZWorld.ShiftAppendDeviation = false;
                    var po = new Dictionary<ZObject, List<ZWaypoint>> { [squad] = new List<ZWaypoint> { FlagClick(w, squad, f2) } };
                    int off = OzShiftAppend.Prefix(po, true); int offLen = po[squad].Count;
                    ZWorld.ShiftAppendDeviation = true;
                    dead.Health = 0;
                    var pd = new Dictionary<ZObject, List<ZWaypoint>> { [dead] = new List<ZWaypoint> { FlagClick(w, dead, f2) } };
                    int gone = OzShiftAppend.Prefix(pd, true); int goneLen = pd[dead].Count;
                    var ps = new Dictionary<ZObject, List<ZWaypoint>> { [squad] = new List<ZWaypoint> { FlagClick(w, squad, f2) } };
                    int shifted = OzShiftAppend.Prefix(ps, true); int shiftedLen = ps[squad].Count;
                    bool front = shiftedLen == 2 && ps[squad][0].refId == f1.Id && ps[squad][1].refId == f2.Id;
                    ok = plain == 0 && plainLen == 1 && mini == 0 && miniLen == 1 && off == 0 && offLen == 1 && gone == 0 && goneLen == 1 && shifted == 1 && front;
                    return $"the squad's order flag 1 in front of a queue for flag 2: a plain queue changed {plain} (list {plainLen}), the squad member's queue {mini} (list {miniLen}), the rule off {off} (list {offLen}), " +
                           $"a destroyed jeep {gone} (list {goneLen}); the control, the squad's Shift queue: changed {shifted}, list {shiftedLen} = flag 1 then flag 2 {(front ? "yes" : "NO")}";
                }
            }
            catch (Exception ex) { return "EXCEPTION " + ex.GetType().Name + ": " + ex.Message; }
        }
    }
}
