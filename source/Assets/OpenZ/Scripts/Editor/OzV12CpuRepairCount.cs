using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Counted check of 1.2 item 13, rule DV137 (Sim/ZBot.CpuRepair.cs): the computer sends its strongest damaged vehicle to its repair
    /// station and back, once every 5 minutes. Headless whole matches with the game's switches (the strike groups and the unit mix on, as
    /// in the game) on the levels whose maps hold repair stations (the Jungle and the Car Park):
    ///   A "computer match": both sides the game's computer (Normal);
    ///   B "player holds": blue is the computer, red the strike check's stand-in player who holds and never attacks (the matches run long);
    /// (the order the check expects for a vehicle: UNIT_REPAIR at the station's centre - 8, attack_to off, then a MOVE back to the point it stood on)
    ///   C the same on Easy (its period x 1.3);
    ///   D the switch on and off: the same world tick for tick until the first trip is sent;
    ///   E the same match twice with the switch on: the same world, the same trips;
    ///   F the switch off: no trip, nobody held;
    ///   G the computer's own stations destroyed at the start: no trip while it has no standing station;
    ///   H a ship of the computer, the only damaged one of the rule's first kinds at 5:00: while the computer may not fly ships (DV118 refuses
    ///     a computer's ship orders) it is tried once and skipped for a period (the next one of the order is sent); once it may, it flies there
    ///     and back like the other vehicles.
    /// Everything is judged from the objects themselves, independently of the rule's code: at every send the check ranks the computer's
    /// vehicles itself (heavy tank, missile launcher, shuttle, medium tank; then hit points lost; then the lower id; the same exclusions)
    /// and finds the nearest station it reaches; every tick of a trip it watches the vehicle's waypoints (a player-given waypoint other
    /// than the repair order, a hold or a patrol = another order), the computer's order record of that tick, the pad, the health.
    /// Menu: OpenZ/V1.2/CPU: 13 Repairs Check (headless). The quick form (the Deviations row): the Car Park only, one seed.
    /// </summary>
    public static class OzV12CpuRepairCount
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static string F(double v, string f = "0") => v.ToString(f, Inv);
        static string Clock(double t) => t < 0 ? "-" : ((int)(t / 60)).ToString(Inv) + ":" + ((int)(t % 60)).ToString("00", Inv);
        /// <summary>The levels whose maps hold repair stations.</summary>
        public static readonly string[] Levels = { "L16", "L17" };
        public const int Seeds = 3;
        public const double MatchSeconds = 1500;
        /// <summary>px: a trip is "back" when it ended this close to where the vehicle was when it was sent.</summary>
        public const int BackPx = 64;

        // ------------------------------------------------------------------ what the check sees
        public sealed class Trip
        {
            public Team team; public int unitId, stationId, hpSend, maxHp; public string type; public double sentAt = -1, padAt = -1, healedAt = -1, endAt = -1;
            public string end = "running", why = ""; public int backPx = -1; public bool ruleUnit, ruleStation, ruleOrder, hadStation;
            public int foreign; public string foreignWhat = ""; public bool stationLost, shuttle; public int fromX, fromY;
        }
        public sealed class Run
        {
            public string level, mode; public int seed; public bool on; public ZBotDifficulty difficulty; public double end; public string winner;
            public readonly List<Trip> trips = new List<Trip>();
            public double minGap = double.MaxValue, firstAt = -1; public int overlaps, noStationSends, heldOutside, tripsByBot, refused;
            public ulong hash; public long ticks; public int dueNoStation, refusedSeen, stationsLeft; public readonly List<double> refusedAt = new List<double>();
            public readonly Dictionary<Team, double> lastSend = new Dictionary<Team, double>();
            public string log = "";
        }

        static int Rank(ZObject o) => o.Kind != ZKind.Vehicle ? -1 : o.IsShuttle ? 2 : o.Type == "heavy" ? 0 : o.Type == "missile_launcher" ? 1 : o.Type == "medium" ? 3 : -1;
        static bool Alive(ZObject o) => o != null && !o.Destroyed && o.KillAt < 0;

        static bool OwnStation(ZWorld w, Team t) => w.Objects.Any(b => b.Kind == ZKind.Building && b.CanRepairUnit(t));

        /// <summary>G: every repair station of the map taken out of the world before the first tick (deleted, no ruin a crane could rebuild).</summary>
        static void KillStations(ZWorld w)
        {
            foreach (var b in w.Objects)
                if (b.Kind == ZKind.Building && b.Type == "repair") b.KillAt = w.Time;
        }

        /// <summary>The station the rule should pick for u: the nearest own one it reaches and whose waiting spot it can stand on (squared centre
        /// distance, then the lower id); the one whose bay the last trip could not get into (skip) only when there is no other.</summary>
        public static ZObject NearestStation(ZWorld w, ZBot bot, ZObject u, int skip = -1) =>
            NearestStationBut(w, bot, u, skip) ?? (skip >= 0 ? NearestStationBut(w, bot, u, -1) : null);

        static ZObject NearestStationBut(ZWorld w, ZBot bot, ZObject u, int skip)
        {
            ZObject best = null; double bd = 0;
            foreach (var st in w.Objects)
            {
                if (st.Kind != ZKind.Building || !st.CanRepairUnit(bot.Team) || st.Id == skip) continue;
                if (u.IsShuttle) { if (!u.ShuttleDeckPoint(st, out _, out _)) continue; }
                else if (!st.GetRepairEntrance(out int ex, out int ey) || !bot.InSameRegion(u.LocX + 8, u.LocY + 8, ex, ey, false) || SpotBlocked(w, u, ex, ey)) continue;
                double dx = st.CenterX - u.CenterX, dy = st.CenterY - u.CenterY, d = dx * dx + dy * dy;
                if (best == null || d < bd || (d == bd && st.Id < best.Id)) { best = st; bd = d; }
            }
            return best;
        }

        /// <summary>The check's own test of DV62's waiting spot (40 px out of the entrance when its tiles are free, else the entrance): a cannon
        /// nearer to it than the two bodies' spacing (ZPathing.MinSpacing) means no vehicle can stand there.</summary>
        static bool SpotBlocked(ZWorld w, ZObject u, int ex, int ey)
        {
            int sy = w.Path.WithinImpassable(ex - u.WidthPix / 2 + 1, ey + 40 - u.HeightPix / 2 + 1, u.WidthPix - 2, u.HeightPix - 2, out _, out _, false) ? ey : ey + 40;
            return w.Objects.Any(c => c.Kind == ZKind.Cannon && !c.Destroyed && c.KillAt < 0
                                      && (long)(c.CenterX - ex) * (c.CenterX - ex) + (long)(c.CenterY - sy) * (c.CenterY - sy) < (long)ZPathing.MinSpacing(u, c) * ZPathing.MinSpacing(u, c));
        }

        /// <summary>The rule's choice as the check sees it right after the tick of the send (the computer decides at the end of that tick;
        /// sent = the vehicle that got the order, whose own waypoint is now the repair; a vehicle its own planner sent to repair in that same
        /// tick still counts as free then). guardsBefore = the vehicles on guard (DV136) before that tick: the defend rule runs after the
        /// repair rule in the computer's tick and may take or free a guard in that same tick.</summary>
        static ZObject RuleChoice(ZWorld w, ZBot bot, ZObject sent, HashSet<int> guardsBefore, out int eligible)
        {
            var list = new List<ZObject>();
            foreach (var o in w.Objects)
            {
                if (o.Kind != ZKind.Vehicle || o.Owner != bot.Team || !Alive(o) || o.IsMinion || o.IsFortDefender || Rank(o) < 0) continue;
                if (o.Health >= o.MaxHealth || o.MaxHealth - o.Health < o.MaxHealth * ZWorld.CpuRepairMinLost || o.StrikeHeld) continue;
                if (w.Time + 1e-6 < o.LastEnemyHit + ZWorld.CpuRepairCalmSec || (o.AttackObject != null && Alive(o.AttackObject))) continue;   // in a fight: hit in the last 4 s, or firing at a target
                if (o != sent)
                {
                    if (guardsBefore.Contains(o.Id) || o.CpuShuttleHeld) continue;                  // a guard at its frontier flag (DV136), a ship / squad of the shuttle plan (DV138)
                    bool repairWp = o.Waypoints.Count > 0 && o.Waypoints[0].mode == WpMode.UnitRepair;
                    bool plannerNow = repairWp && bot.LastRoundOrders.Any(x => x.unitId == o.Id && x.mode == WpMode.UnitRepair && Math.Abs(x.time - w.Time) < 1e-9);
                    if (repairWp && !plannerNow) continue;
                    if (o.IsShuttle) { if (o.ShuttleRepairTarget >= 0 || bot.CpuRepairRefused > 0) continue; }   // a ship the computer may not fly is skipped by the rule
                    else if (!o.CanMove || !o.CanOverwriteWP()) continue;
                }
                if (NearestStation(w, bot, o) == null) continue;
                list.Add(o);
            }
            eligible = list.Count;
            return list.OrderBy(Rank).ThenByDescending(o => o.MaxHealth - o.Health).ThenBy(o => o.Id).FirstOrDefault();
        }

        // ------------------------------------------------------------------ one match
        public static Run Match(string settings, string map, string level, int seed, bool cpuVsCpu, bool on, ZBotDifficulty difficulty, double seconds,
                                List<ulong> trace = null, int traceUntilTick = -1, bool killStations = false, Action<ZWorld> onWorld = null, Action<ZWorld> everyTick = null)
        {
            var R = new Run { level = level, seed = seed, mode = cpuVsCpu ? "computer match" : "player holds", on = on, difficulty = difficulty };
            using (OzV11StrikeCount.Game(true))
            {
                ZWorld.BotHeavyDeviation = true;                                                    // the game's unit mix (ZGame), headless default off
                ZWorld.CpuRepairDeviation = on;
                var w = ZWorld.Create(settings, map, seed, level == "L17");
                var blue = new ZBot(w, Team.Blue, seed + 101) { Difficulty = difficulty };
                w.Bots.Add(blue);
                ZBot red = null;
                if (cpuVsCpu) { red = new ZBot(w, Team.Red, seed) { Difficulty = difficulty }; w.Bots.Add(red); }
                var standIn = cpuVsCpu ? null : OzV11StrikeCount.StandIn(w);
                if (killStations) KillStations(w);
                w.Events.Clear();
                onWorld?.Invoke(w);
                var bots = w.Bots.ToList();
                var seen = bots.ToDictionary(b => b, b => 0);
                var open = new Dictionary<ZBot, Trip>();
                var noBay = new Dictionary<Team, (int id, double until)>();
                double period = ZWorld.CpuRepairEverySec * (difficulty == ZBotDifficulty.Easy && ZWorld.EasyCpuSlowerOn ? ZWorld.EasyDecisionSlow : 1.0);
                ulong h = 1469598103934665603UL;
                var guards = new HashSet<int>();
                while (w.Time < seconds && !w.GameOver)
                {
                    everyTick?.Invoke(w);
                    guards.Clear();
                    foreach (var o in w.Objects) if (o.Kind == ZKind.Vehicle && o.CpuDefendHeld) guards.Add(o.Id);   // DV136 guards as the repair rule sees them in this tick
                    w.Tick(ZConst.TICK);
                    R.ticks++;
                    if (killStations)
                    {
                        if (R.ticks % 100 == 0 && w.Time + 1e-6 >= period)
                            foreach (var bot in w.Bots)
                                if (!OwnStation(w, bot.Team) && w.Objects.Any(o => o.Owner == bot.Team && Alive(o) && Rank(o) >= 0 && o.MaxHealth - o.Health >= o.MaxHealth * ZWorld.CpuRepairMinLost)) R.dueNoStation++;
                    }
                    int refusedNow = 0;
                    for (int bi = 0; bi < w.Bots.Count; bi++) refusedNow += w.Bots[bi].CpuRepairRefused;
                    if (refusedNow > R.refusedSeen) { R.refusedSeen = refusedNow; R.refusedAt.Add(w.Time); }
                    if (standIn != null) OzV11StrikeCount.StandInKeep(w, standIn);
                    if (trace != null && (traceUntilTick < 0 || R.ticks <= traceUntilTick)) trace.Add(OzV11StrikeCount.Fold(1469598103934665603UL, w));
                    foreach (var e in w.Events)
                    {
                        if (e.type != ZEventType.RepairIn && e.type != ZEventType.RepairOut) continue;
                        foreach (var kv in open)
                            if (kv.Value.unitId == e.objId)
                            {
                                if (e.type == ZEventType.RepairIn && kv.Value.padAt < 0) kv.Value.padAt = w.Time;
                                if (e.type == ZEventType.RepairOut && kv.Value.healedAt < 0) kv.Value.healedAt = w.Time;
                            }
                    }
                    w.Events.Clear();
                    foreach (var bot in bots)
                    {
                        // a new trip (the computer decided at the end of this tick)
                        if (bot.CpuRepairTrips > seen[bot])
                        {
                            seen[bot] = bot.CpuRepairTrips;
                            var now = bot.CpuRepairNow;
                            var u = w.Get(now.unitId); var st = w.Get(now.stationId);
                            var T = new Trip { team = bot.Team, unitId = now.unitId, stationId = now.stationId, type = now.type, hpSend = now.hpAtSend, maxHp = now.maxHp, sentAt = w.Time, fromX = now.fromX, fromY = now.fromY, shuttle = now.shuttle };
                            if (open.ContainsKey(bot)) { R.overlaps++; open.Remove(bot); }
                            T.hadStation = st != null && st.CanRepairUnit(bot.Team);
                            if (!OwnStation(w, bot.Team)) R.noStationSends++;
                            var choice = u != null ? RuleChoice(w, bot, u, guards, out int n) : null;
                            T.ruleUnit = choice == u && u != null;
                            if (!T.ruleUnit) T.why = $"the check's choice {(choice == null ? "none" : choice.Type + "#" + choice.Id + " " + choice.Health + "/" + choice.MaxHealth)}";
                            int skip = noBay.TryGetValue(bot.Team, out var nb) && w.Time + 1e-6 < nb.until ? nb.id : -1;
                            var nst = u != null ? NearestStation(w, bot, u, skip) : null;
                            T.ruleStation = nst != null && nst == st;
                            if (u != null && !u.IsShuttle)
                            {
                                var wp = u.Waypoints.Count > 0 ? u.Waypoints[0] : ZWaypoint.Cleared;
                                var mv = u.Waypoints.Count > 1 ? u.Waypoints[1] : ZWaypoint.Cleared;
                                T.ruleOrder = st != null && u.Waypoints.Count == 2 && wp.mode == WpMode.UnitRepair && wp.refId == st.Id && wp.x == st.CenterX - 8 && wp.y == st.CenterY && !wp.attackTo && wp.playerGiven
                                              && mv.mode == WpMode.Move && mv.refId == -1 && mv.x == now.fromX && mv.y == now.fromY && !mv.attackTo && mv.playerGiven;
                            }
                            else T.ruleOrder = u != null && st != null && u.ShuttleRepairTarget == st.Id;
                            if (R.lastSend.TryGetValue(bot.Team, out double last)) R.minGap = Math.Min(R.minGap, w.Time - last);
                            else if (R.firstAt < 0 || w.Time < R.firstAt) R.firstAt = w.Time;
                            R.lastSend[bot.Team] = w.Time;
                            R.trips.Add(T); open[bot] = T;
                        }
                        // the trip under way: nothing else orders it
                        if (open.TryGetValue(bot, out var cur))
                        {
                            var u = w.Get(cur.unitId);
                            if (bot.CpuRepairUnitId == cur.unitId && u != null && !u.Destroyed)
                            {
                                foreach (var p in u.Waypoints)
                                {
                                    if (!p.playerGiven || p.refId <= ZPathing.RefStepAside) continue;
                                    if (p.mode == WpMode.UnitRepair && p.refId == cur.stationId && !p.attackTo) continue;
                                    if (p.mode == WpMode.Move && p.refId == -1 && p.x == cur.fromX && p.y == cur.fromY && !p.attackTo) continue;   // its own way back
                                    if (p.mode == WpMode.Move && p.refId == -1 && !p.attackTo && Math.Abs(p.x - cur.fromX) <= BackPx && Math.Abs(p.y - cur.fromY) <= BackPx) continue;   // the same, its goal moved aside by the movement layer (settle / slots, as for every move); no order
                                    cur.foreign++; cur.foreignWhat = $"{p.mode} {p.refId} at {Clock(w.Time)}"; break;
                                }
                                if (u.HoldPosition || u.Patrolling) { cur.foreign++; cur.foreignWhat = "hold / patrol at " + Clock(w.Time); }
                                if (bot.LastRoundOrders.Any(x => x.unitId == u.Id && Math.Abs(x.time - w.Time) < 1e-9)) { cur.foreign++; cur.foreignWhat = "an order of the computer's round at " + Clock(w.Time); }
                            }
                            if (bot.CpuRepairUnitId != cur.unitId)                                  // the rule ended it this tick
                            {
                                int ri = bot.CpuRepairLog.FindLastIndex(x => x.unitId == cur.unitId && Math.Abs(x.sentAt - cur.sentAt) < 1e-9);
                                cur.endAt = w.Time;
                                if (ri >= 0)
                                {
                                    var rec = bot.CpuRepairLog[ri];
                                    cur.end = rec.end.ToString(); cur.backPx = rec.BackPx;
                                    if (cur.padAt < 0 && rec.padAt >= 0) cur.padAt = rec.padAt;
                                    if (cur.healedAt < 0 && rec.hpBest >= rec.maxHp && rec.maxHp > 0) cur.healedAt = rec.leftAt;
                                }
                                else cur.end = "?";
                                var sNow = w.Get(cur.stationId);
                                cur.stationLost = sNow == null || !sNow.CanRepairUnit(bot.Team);
                                if (cur.end == "NoRepair" && !cur.stationLost && !cur.shuttle) noBay[bot.Team] = (cur.stationId, w.Time + period);
                                open.Remove(bot);
                            }
                        }
                    }
                    // nobody of a computer is held unless it is that computer's trip unit
                    if (R.ticks % 100 == 0)
                        foreach (var o in w.Objects)
                            if (o.CpuRepairHeld && !bots.Any(b => b.Team == o.Owner && b.CpuRepairUnitId == o.Id)) R.heldOutside++;
                }
                foreach (var kv in open) { kv.Value.end = "match over"; kv.Value.endAt = w.Time; }
                R.end = w.Time; R.winner = w.GameOver ? w.Winner.ToString() : "-";
                R.stationsLeft = w.Objects.Count(b => b.Kind == ZKind.Building && b.Type == "repair");
                R.hash = OzV11StrikeCount.Fold(h, w);
                R.tripsByBot = bots.Sum(b => b.CpuRepairTrips); R.refused = bots.Sum(b => b.CpuRepairRefused);
                R.log = string.Join("\n", bots.SelectMany(b => b.Log.Where(l => l.Contains("DV137"))));
            }
            return R;
        }

        public static string TripLine(Trip t)
        {
            string dur = t.endAt >= 0 ? F(t.endAt - t.sentAt) + " s" : "-";
            return $"    {Clock(t.sentAt)} {t.team} {t.type}#{t.unitId} {t.hpSend}/{t.maxHp} -> repair#{t.stationId}: pad {Clock(t.padAt)}, healed {Clock(t.healedAt)}, end {t.end} at {Clock(t.endAt)} ({dur}), back {(t.backPx < 0 ? "-" : t.backPx + " px")}" +
                   $"{(t.ruleUnit ? "" : " | NOT THE RULE'S UNIT: " + t.why)}{(t.ruleStation ? "" : " | NOT THE NEAREST STATION")}{(t.ruleOrder ? "" : " | NOT THE R KEY'S ORDER")}{(t.foreign == 0 ? "" : " | OTHER ORDERS " + t.foreign + ": " + t.foreignWhat)}{(t.end == "NoRepair" ? (t.stationLost ? " (the station was lost)" : " (its approach gave up: the bay's waiting spot blocked)") : "")}";
        }

        public static string RunLine(Run r) =>
            $"  {r.level} seed {r.seed} {r.mode}{(r.difficulty == ZBotDifficulty.Easy ? " (Easy)" : "")}: {(r.winner == "-" ? "time" : r.winner + " won")} at {Clock(r.end)}; trips {r.trips.Count} (blue {r.trips.Count(t => t.team == Team.Blue)}, red {r.trips.Count(t => t.team == Team.Red)}), orders refused {r.refused}";

        // ------------------------------------------------------------------ the check
        /// <summary>The whole check (quick = the Car Park only, one seed, for the Deviations row).</summary>
        public static string Check(string settings, Func<string, string> map, bool quick = false, int seeds = Seeds, Action<string> progress = null)
        {
            var sb = new StringBuilder(); var v = new List<string>();
            bool was = ZWorld.CpuRepairDeviation;
            try
            {
                sb.AppendLine($"# 1.2 item 13, DV137: the computer sends its strongest damaged vehicle to its repair station and back, once every {F(ZWorld.CpuRepairEverySec / 60)} minutes (headless){OzV11StrikeCount.RulesNote()}");
                sb.AppendLine($"rule: heavy tank, missile launcher, shuttle, else a medium tank; then the most hit points lost (at least {F(ZWorld.CpuRepairMinLost * 100)} %); not a vehicle in a fight (hit in the last {F(ZWorld.CpuRepairCalmSec)} s, or firing at a target) or on guard (DV136); the R key's order to the nearest own station it reaches (not one whose waiting spot a cannon blocks); held until back; a trip ends after {F(ZWorld.CpuRepairMaxTripSec)} s at the latest ({F(ZWorld.CpuRepairTripSlackSec)} s + {F(ZWorld.CpuRepairTripDriveFactor)} x the straight drive there and back when that is longer)");
                var levels = quick ? new[] { "L17" } : Levels; int n = quick ? 1 : seeds;
                var all = new List<Run>();
                foreach (var cpu in new[] { true, false })
                {
                    sb.AppendLine(cpu ? "-- A the computer against the computer (Normal), up to " + F(MatchSeconds / 60) + " minutes" : "-- B the computer (blue, Normal) against the stand-in player who holds, " + F((quick ? 900 : MatchSeconds) / 60) + " minutes");
                    foreach (var lv in levels)
                        for (int s = 1; s <= n; s++)
                        {
                            progress?.Invoke($"{lv} seed {s} {(cpu ? "cpu" : "hold")}");
                            var r = Match(settings, map(lv), lv, s, cpu, true, ZBotDifficulty.Normal, quick && !cpu ? 900 : MatchSeconds);
                            all.Add(r); sb.AppendLine(RunLine(r)); foreach (var t in r.trips) sb.AppendLine(TripLine(t));
                        }
                }
                Run easy = null;
                if (!quick)
                {
                    sb.AppendLine("-- C Easy: the computer (blue, Easy) against the stand-in player, " + F(MatchSeconds / 60) + " minutes (period x " + F(ZWorld.EasyDecisionSlow, "0.0") + ")");
                    progress?.Invoke("easy");
                    easy = Match(settings, map("L17"), "L17", 1, false, true, ZBotDifficulty.Easy, MatchSeconds);
                    sb.AppendLine(RunLine(easy)); foreach (var t in easy.trips) sb.AppendLine(TripLine(t));
                }
                // D: on = off until the first trip
                progress?.Invoke("on / off");
                var traceOn = new List<ulong>();
                var dOn = Match(settings, map("L17"), "L17", 1, true, true, ZBotDifficulty.Normal, Math.Min(MatchSeconds, 720), traceOn);
                int firstTick = dOn.trips.Count > 0 ? (int)Math.Round(dOn.trips[0].sentAt / ZConst.TICK) : traceOn.Count + 1;
                var traceOff = new List<ulong>();
                var dOff = Match(settings, map("L17"), "L17", 1, true, false, ZBotDifficulty.Normal, (firstTick + 1) * ZConst.TICK, traceOff);
                int same = 0; while (same < traceOn.Count && same < traceOff.Count && traceOn[same] == traceOff[same]) same++;
                bool dOk = dOn.trips.Count > 0 && same >= firstTick - 1;
                sb.AppendLine($"-- D the switch on and off, L17 computer match seed 1: the first trip at {Clock(dOn.trips.Count > 0 ? dOn.trips[0].sentAt : -1)} (tick {firstTick}); the world identical tick for tick for {same} ticks ({Clock(same * ZConst.TICK)}), first difference at tick {(same < Math.Min(traceOn.Count, traceOff.Count) ? (same + 1).ToString(Inv) : "-")}");
                // E: the same match twice
                Run e1 = null, e2 = null;
                if (!quick)
                {
                    progress?.Invoke("twice");
                    e1 = Match(settings, map("L16"), "L16", 2, false, true, ZBotDifficulty.Normal, 900);
                    e2 = Match(settings, map("L16"), "L16", 2, false, true, ZBotDifficulty.Normal, 900);
                    sb.AppendLine($"-- E the same match twice (L16 seed 2, player holds, 15 minutes, the switch on): hash {(e1.hash == e2.hash ? "equal" : "DIFFERENT")}, trips {e1.trips.Count} / {e2.trips.Count}, the same units and times {SameTrips(e1, e2)}");
                }
                // F: the switch off
                progress?.Invoke("off");
                var off = Match(settings, map("L17"), "L17", 2, false, false, ZBotDifficulty.Normal, quick ? 600 : 900);
                sb.AppendLine($"-- F the switch off (L17 seed 2, player holds, {F(off.end / 60)} minutes): trips {off.trips.Count}, sent by the rule {off.tripsByBot}, held vehicles seen {off.heldOutside}");
                // G: no station
                Run g = null;
                if (!quick)
                {
                    progress?.Invoke("no station");
                    g = Match(settings, map("L16"), "L16", 1, false, true, ZBotDifficulty.Normal, 900, killStations: true);
                    sb.AppendLine($"-- G every repair station of the map taken away at the start (L16 seed 1, player holds, 15 minutes): stations left {g.stationsLeft}, trips {g.trips.Count}, sent while it had no standing station {g.noStationSends}; looks (once a second from 5:00) at which the computer had a damaged vehicle of the rule's kinds and no station: {g.dueNoStation}");
                    foreach (var t in g.trips) sb.AppendLine(TripLine(t));
                }

                // H: a ship of the computer
                Run hr = null; ZObject ship = null; string shipNote = "";
                if (!quick)
                {
                    progress?.Invoke("ship");
                    hr = Match(settings, map("L17"), "L17", 1, false, true, ZBotDifficulty.Normal, 590, onWorld: w => ship = ShipFor(w, out shipNote), everyTick: w => ShipOnly(w, ship));
                    var shipTrips = hr.trips.Where(t => t.unitId == (ship?.Id ?? -2)).ToList();
                    sb.AppendLine($"-- H a ship of the computer (L17 seed 1, player holds, to 9:50, before a second period; {shipNote}): orders the command layer refused {hr.refused}, ship trips {shipTrips.Count}, other trips {hr.trips.Count - shipTrips.Count}");
                    foreach (var t in hr.trips) sb.AppendLine(TripLine(t));
                }

                // ------------------------------------------------------------------ verdicts
                var trips = all.SelectMany(r => r.trips).Concat(easy != null ? easy.trips : new List<Trip>()).ToList();
                var done = trips.Where(t => t.end != "match over").ToList();
                v.Add(OzV11Kit.Verdict(trips.Count >= (quick ? 1 : 6) && levels.All(lv => all.Any(r => r.level == lv && r.trips.Count > 0)),
                    $"V1 trips sent: {trips.Count} in {all.Count + (easy != null ? 1 : 0)} matches ({string.Join(", ", levels.Select(lv => lv + " " + all.Where(r => r.level == lv).Sum(r => r.trips.Count)))})"));
                double gap = all.Min(r => r.minGap), first = all.Where(r => r.firstAt >= 0).Select(r => r.firstAt).DefaultIfEmpty(-1).Min();
                bool easyOk = easy == null || (easy.minGap >= ZWorld.CpuRepairEverySec * ZWorld.EasyDecisionSlow - 1e-6 && (easy.firstAt < 0 || easy.firstAt >= ZWorld.CpuRepairEverySec * ZWorld.EasyDecisionSlow - 1e-6));
                v.Add(OzV11Kit.Verdict(gap >= ZWorld.CpuRepairEverySec - 1e-6 && (first < 0 || first >= ZWorld.CpuRepairEverySec - 1e-6) && all.Sum(r => r.overlaps) == 0 && easyOk,
                    $"V2 at most one trip per {F(ZWorld.CpuRepairEverySec)} s per computer: the first at {Clock(first)}, the shortest gap between two sends of one computer {(gap == double.MaxValue ? "-" : F(gap, "0.0") + " s")}, two at once {all.Sum(r => r.overlaps)}" +
                    (easy != null ? $"; Easy: the first at {Clock(easy.firstAt)}, the shortest gap {(easy.minGap == double.MaxValue ? "-" : F(easy.minGap, "0.0") + " s")} (period {F(ZWorld.CpuRepairEverySec * ZWorld.EasyDecisionSlow)} s)" : "")));
                int noSt = all.Sum(r => r.noStationSends) + (easy?.noStationSends ?? 0) + (g?.noStationSends ?? 0);
                v.Add(OzV11Kit.Verdict(noSt == 0 && trips.All(t => t.hadStation) && (g == null || (g.trips.Count == 0 && g.dueNoStation > 0 && g.stationsLeft == 0)),
                    $"V3 only with a station: sends while the computer had no standing station of its own {noSt}; the order's station its own and standing {trips.Count(t => t.hadStation)} of {trips.Count}" + (g != null ? $"; no station on the map: {g.trips.Count} trips in {g.dueNoStation} looks with a damaged vehicle waiting" : "")));
                v.Add(OzV11Kit.Verdict(trips.All(t => t.ruleUnit && t.ruleStation && t.ruleOrder),
                    $"V4 the rule's unit: the check's own choice (kind, hit points lost, id) {trips.Count(t => t.ruleUnit)} of {trips.Count}; the nearest station it reaches {trips.Count(t => t.ruleStation)}; the R key's order (UNIT_REPAIR at the station's centre - 8, attack_to off; then a MOVE back to where it stood) {trips.Count(t => t.ruleOrder)}"));
                int back = done.Count(t => t.end == "Back" && t.padAt >= 0 && t.healedAt >= 0 && t.backPx >= 0 && t.backPx <= BackPx);
                int died = done.Count(t => t.end == "Died" || t.end == "Lost");
                int noRep = done.Count(t => t.end == "NoRepair"), noRepLost = done.Count(t => t.end == "NoRepair" && t.stationLost), noRepBay = noRep - noRepLost, timedOut = done.Count(t => t.end == "TimedOut"), backFar = done.Count(t => t.end == "Back" && !(t.padAt >= 0 && t.healedAt >= 0 && t.backPx >= 0 && t.backPx <= BackPx));
                int maxBack = done.Where(t => t.end == "Back").Select(t => t.backPx).DefaultIfEmpty(-1).Max();
                v.Add(OzV11Kit.Verdict(done.Count > 0 && back + died + noRepLost + noRepBay == done.Count && back > 0 && timedOut == 0 && backFar == 0,
                    $"V5 the trip completes: of {done.Count} finished trips {back} reached the pad, were healed to full and came back (within {BackPx} px of where they were; farthest {maxBack} px), {died} ended destroyed or lost to the other side, " +
                    $"{noRepLost} ended because the station was lost on the way, {noRepBay} because the repair trip's own approach gave up at a blocked bay and drove back (the R key's trip gives up the same way; the next trip then prefers another station); back but not healed / too far {backFar}, too long {timedOut}; still going when a match ended {trips.Count - done.Count}"));
                int foreign = trips.Sum(t => t.foreign), outside = all.Sum(r => r.heldOutside) + (easy?.heldOutside ?? 0);
                v.Add(OzV11Kit.Verdict(foreign == 0 && outside == 0,
                    $"V6 no other order on the way: other orders seen on the trip vehicles {foreign} (a player-given waypoint other than the repair order, a hold or a patrol, an order of the computer's own round, its idle rules); vehicles held that were on no trip {outside}"));
                bool eOk = e1 == null || (e1.hash == e2.hash && SameTrips(e1, e2) == "yes");
                v.Add(OzV11Kit.Verdict(dOk && eOk && off.trips.Count == 0 && off.tripsByBot == 0 && off.heldOutside == 0,
                    $"V7 the world: the switch on = the switch off tick for tick until the first trip ({same} ticks, the first trip at tick {firstTick}){(e1 != null ? "; the same match twice: " + (eOk ? "the same world and trips" : "DIFFERENT") : "")}; the switch off: {off.tripsByBot} trips, {off.heldOutside} held"));
                if (hr != null)
                {
                    var st = hr.trips.Where(t => ship != null && t.unitId == ship.Id).ToList();
                    bool refusedWay = ship != null && hr.refused == 1 && hr.refusedAt.Count == 1 && hr.refusedAt[0] + 1e-6 >= ZWorld.CpuRepairEverySec && st.Count == 0 && hr.trips.All(t => t.ruleUnit);
                    bool flownWay = ship != null && hr.refused == 0 && st.Count >= 1 && st.All(t => t.end == "Back" && t.padAt >= 0 && t.healedAt >= 0 && t.backPx >= 0 && t.backPx <= BackPx && t.foreign == 0);
                    v.Add(OzV11Kit.Verdict(refusedWay || flownWay,
                        $"V8 a ship of the computer: {(ship == null ? "NO SHIP PLACED" : refusedWay ? "the ship's repair order refused (the shuttle rule DV138 off in this check): tried once at " + Clock(hr.refusedAt[0]) + ", refused, not tried again in the period; then the next one of the order: " + (hr.trips.Count > 0 ? hr.trips[0].type + "#" + hr.trips[0].unitId + " at " + Clock(hr.trips[0].sentAt) : "none damaged") : flownWay ? "flown to the station's deck, repaired, flown back: " + string.Join(", ", st.Select(t => Clock(t.sentAt) + " " + t.backPx + " px")) : "neither: refused " + hr.refused + ", ship trips " + st.Count)}"));
                }
                sb.AppendLine();
                foreach (var line in v) sb.AppendLine(line);
                int fails = v.Count(x => x.StartsWith("FAIL", StringComparison.Ordinal));
                sb.AppendLine($"{v.Count - fails} PASS, {fails} FAIL");
            }
            catch (Exception ex) { sb.AppendLine("FAIL  the check threw " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace); }
            finally { ZWorld.CpuRepairDeviation = was; }
            return sb.ToString();
        }

        /// <summary>H: a ship of the computer (blue) at half its hit points on a free pad beside its fort (the playable shuttle rule on).</summary>
        public static ZObject ShipFor(ZWorld w, out string note)
        {
            ZWorld.ShuttlePlayableDeviation = true; ZWorld.ShuttleOn = true;
            var fort = w.Objects.FirstOrDefault(o => o.IsFort && o.Owner == Team.Blue);
            ZObject s = null; string why = "no fort";
            if (fort != null)
                foreach (var (dx, dy) in new[] { (0, 12), (12, 0), (-12, 0), (0, -12), (12, 12), (-12, 12) })
                {
                    s = w.SpawnShuttle(fort.CenterX + dx * 16, fort.CenterY + dy * 16, Team.Blue, 0f, out why);
                    if (s != null) break;
                }
            if (s == null) { note = "no ship: " + why; return null; }
            s.SetHealth(s.MaxHealth / 2);
            note = $"shuttle#{s.Id} {s.Health}/{s.MaxHealth} at ({s.CenterX / 16},{s.CenterY / 16})";
            return s;
        }

        /// <summary>H: just before the first look at 5:00 the computer's heavy tanks and missile launchers are mended, so the ship is the first of the order.</summary>
        public static void ShipOnly(ZWorld w, ZObject ship)
        {
            if (ship == null || w.Time < ZWorld.CpuRepairEverySec - 1.0 || w.Time >= ZWorld.CpuRepairEverySec - 0.5) return;
            foreach (var o in w.Objects)
                if (o != ship && o.Owner == Team.Blue && Alive(o) && (Rank(o) == 0 || Rank(o) == 1) && o.Health < o.MaxHealth) o.SetHealth(o.MaxHealth);
        }

        static string SameTrips(Run a, Run b)
        {
            if (a.trips.Count != b.trips.Count) return "no";
            for (int i = 0; i < a.trips.Count; i++)
                if (a.trips[i].unitId != b.trips[i].unitId || Math.Abs(a.trips[i].sentAt - b.trips[i].sentAt) > 1e-9 || a.trips[i].end != b.trips[i].end) return "no";
            return "yes";
        }
    }
}
