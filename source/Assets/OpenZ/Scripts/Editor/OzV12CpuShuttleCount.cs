using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Counted check of 1.2 item 12 (rule DV138, Sim/ZBot.CpuShuttle.cs: the computer flies its shuttles). Headless: no scene, no Play mode,
    /// nothing saved; the game's switches (OzV11StrikeCount.Game: OzV11Kit.GameSwitches plus the rules a level start turns on, strike groups
    /// on) and the shuttle's rules on (DV118, DV125, level 16's ship, the exit lanes), the unit mix on (DV115).
    /// <para>The matches (each <see cref="MatchSec"/> s of game time or to its end):</para>
    /// <list type="bullet">
    /// <item>A level 2: the blue computer (Normal) with a crewed ship dropped by its fort, against the strike check's stand-in player on red
    /// (he holds his four nearest territories and his gate, never attacks). Every <see cref="GiftEverySec"/> s the check hands the territory
    /// of blue's that lies farthest from blue's fort to red with nobody in it (an undefended enemy territory appears), and at
    /// <see cref="DecoySec"/> s it hands a far territory to red WITH two squads and a tank holding beside its flag (a defended one: never a
    /// target while they stand).</item>
    /// <item>B level 2: the same with an EMPTY ship by blue's fort instead of the crewed one (blue must take it first).</item>
    /// <item>C level 16: the level's own ship given to blue (ZWorld.ShuttleTeam blue: it stands by the red fort), the stand-in, the gifts.</item>
    /// <item>D level 17: computer against computer (both Normal), the level's own shuttles on (the EMPTY car-park ship on the red side, the
    /// red fort's crewed ship), a crewed blue ship dropped by the blue fort.</item>
    /// <item>E level 2 as A with an EASY blue computer (its clock 1.3 x slower).</item>
    /// <item>F the switch OFF in A's match: no computer ship flies, no computer robot is ever ordered into a ship, the plan logs nothing; twice the
    /// same world hash.</item>
    /// <item>G the switch ON in a match without any shuttle (level 2, computer against computer): the same world hash as with it off.</item>
    /// <item>H the human's ship: level 16, the stand-in owns the level's ship (red); a red squad is ordered in, the ship flown to a free spot,
    /// EXIT: the same world hash and the same shuttle notes with the switch off and on.</item>
    /// <item>I A twice with the switch on: the same world hash and the same plan log.</item>
    /// </list>
    /// The check measures the defence itself (its own code, not the rule's): the other side's robots and vehicles (not shuttles) within
    /// <see cref="UnitPx"/> px, its manned guns whose reach + 32 px covers the point, its forts whose gun's reach (+ 32 px) reaches the point
    /// from the walls. Out of Play the level's plants are not known (the view hands them in during Play): headless landings can succeed where
    /// the game hovers over a tree crown (the plan then takes the next spot).
    /// </summary>
    public static class OzV12CpuShuttleCount
    {
        public const double MatchSec = 900.0, RowMatchSec = 600.0;
        public const double GiftEverySec = 150.0, DecoySec = 60.0;
        public const int UnitPx = 192;
        /// <summary>Other rules (static switches of ZWorld, by name) the matches set on too, e.g. the other computer rules of the game; empty =
        /// the check's own switches only.</summary>
        public static string[] AlsoOn = new string[0];

        /// <summary>The check's own defence measure of point (px) against team me; what = the count by kind.</summary>
        public static int Defended(ZWorld w, Team me, int px, int py, out string what)
        {
            int bodies = 0, veh = 0, guns = 0, forts = 0;
            var hw = w.S.Unit("howitzer");
            long fortR = (hw != null ? hw.attack_radius + 32 : 232) + 32;
            foreach (var o in w.Objects)
            {
                if (o.Destroyed || o.KillAt >= 0 || o.Owner == me || o.Owner == Team.Neutral) continue;
                if (o.IsFort)
                {
                    long ex = Math.Max(0, Math.Max(o.LocX - px, px - (o.LocX + o.WidthPix))), ey = Math.Max(0, Math.Max(o.LocY - py, py - (o.LocY + o.HeightPix)));
                    if (ex * ex + ey * ey <= fortR * fortR) forts++;
                    continue;
                }
                long dx = o.CenterX - px, dy = o.CenterY - py, d2 = dx * dx + dy * dy;
                if (o.Kind == ZKind.Cannon) { long r = o.AttackRadius + 32; if (d2 <= r * r) guns++; continue; }
                if (d2 > (long)UnitPx * UnitPx) continue;
                if (o.Kind == ZKind.Robot) bodies++;
                else if (o.Kind == ZKind.Vehicle && !o.IsShuttle) veh++;
            }
            what = $"{bodies} robots, {veh} vehicles, {guns} guns, {forts} forts";
            return bodies + veh + guns + forts;
        }

        static ulong Mix(ulong h, long v) { h ^= (ulong)v; h *= 1099511628211UL; h ^= h >> 31; return h; }
        /// <summary>The world hash: every object's place, health, owner and orders (OzV11StrikeCount.Fold), the time, the shuttles' flight state.</summary>
        public static ulong Hash(ZWorld w)
        {
            ulong h = OzV11StrikeCount.Fold(1469598103934665603UL, w);
            h = Mix(h, (long)Math.Round(w.Time * 100));
            foreach (var o in w.Objects)
                if (o.IsShuttle) { h = Mix(h, (long)o.ShuttleState); h = Mix(h, (long)Math.Round(o.ShuttleLift * 1000)); h = Mix(h, o.Passengers.Count); h = Mix(h, o.Drivers.Count); h = Mix(h, o.ShuttleGoalX); h = Mix(h, o.ShuttleGoalY); }
            return h;
        }

        sealed class Run
        {
            public string name, level; public bool done;
            public double end; public string endWhy = "";
            public List<ZBot> bots = new List<ZBot>();
            public int takeOffs, diverts, landings, unloads, captures, emptyTaken, boardFails, hovers, lost, noTarget, homeFlights, gifts;
            public int takeOffBad, takeOffNoteBad, landBad, landingsHomeOrTarget, flightsNotByPlan, enterLong, hoverLong, stuck, cooldownBad, firstBad;
            public int repairFlights;                       // take-offs of a ship on a repair trip of its computer (DV137): to the station's deck and back
            public double minGap = double.MaxValue, maxEnterSec, maxHoverSec, maxOpenSec;
            public int decoyId = -1, decoyPicked; public double decoyThreatAt;
            public readonly List<string> rows = new List<string>(), bad = new List<string>();
            public ulong hash; public string plan = "";
            public int cpuEnters, cpuFlights, notesCount;
        }

        /// <summary>
        /// testing panel's drop finds a pad.</summary>
        static ZObject DropShip(ZWorld w, Team owner, Team side)
        {
            var fort = w.Objects.FirstOrDefault(o => o.IsFort && o.Owner == side && ZWorld.StrikeGate(o, out _, out _, out _, out _, out _)) ?? w.Objects.First(o => o.IsFort && o.Owner == side);
            for (int r = 9; r <= 15; r += 3)
                foreach (var (dx, dy) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1), (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int px = fort.CenterX + dx * r * 16, py = fort.CenterY + dy * r * 16;
                    if (px < 64 || py < 64 || px > w.Map.WidthPix - 64 || py > w.Map.HeightPix - 64) continue;
                    var s = w.SpawnShuttle(px, py, owner, -90f, out _);
                    if (s != null) return s;
                }
            return null;
        }

        static double FortDist(ZWorld w, Team t, ZObject f)
        {
            double best = double.MaxValue;
            foreach (var o in w.Objects) if (o.IsFort && o.Owner == t && !o.Destroyed) best = Math.Min(best, Math.Sqrt((double)(o.CenterX - f.CenterX) * (o.CenterX - f.CenterX) + (double)(o.CenterY - f.CenterY) * (o.CenterY - f.CenterY)));
            return best;
        }

        static ZObject Hold(ZWorld w, string type, int cx, int cy)
        {
            for (int r = 0; r <= 6; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int x = cx + dx * 16, y = cy + dy * 16, tx = x >> 4, ty = y >> 4; bool ok = true;
                        for (int j = -1; j <= 1 && ok; j++) for (int i = -1; i <= 1 && ok; i++) { var c = w.Map.ClassAt(tx + i, ty + j); ok = (c == ZClass.Ground || c == ZClass.Road) && w.Path.TilePassable(tx + i, ty + j, false); }
                        if (!ok) continue;
                        foreach (var o in w.Objects) if (!o.Destroyed && o.KillAt < 0 && o.LocX < x + 24 && o.LocX + o.WidthPix > x - 24 && o.LocY < y + 24 && o.LocY + o.HeightPix > y - 24) { ok = false; break; }
                        if (!ok) continue;
                        var u = OzV11Kit.Place(w, type, x, y, Team.Red);
                        w.SendHold(Team.Red, u);
                        return u;
                    }
            return null;
        }

        enum Kind { A, B, C, D, E, F, G, H, I, J }

        /// <summary>One match. on = DV138; returns its counts and verdict data.</summary>
        static Run Match(string settings, string map, string level, Kind k, bool on, double secs, int seed)
        {
            var R = new Run { name = k.ToString(), level = level };
            var team0 = ZWorld.ShuttleTeam;
            using (OzV11StrikeCount.Game(true))
            {
                try
                {
                    ZWorld.BotHeavyDeviation = true; ZWorld.ShuttlePlayableDeviation = true; ZWorld.ShuttleOn = true; ZWorld.L16ShuttleDeviation = true;
                    ZWorld.ShuttleQueueDeviation = true; ZWorld.ExitLanesDeviation = true;
                    ZWorld.L17ShuttlesDeviation = k == Kind.D; ZWorld.L17StartShuttle = k == Kind.D;
                    ZWorld.ShuttleTeam = Team.Red;                                    // level 16's own ship: red's (the stand-in's, or the red computer's in J)
                    ZWorld.CpuShuttleDeviation = on;
                    foreach (var name in AlsoOn) OzV11Kit.TrySet(typeof(ZWorld), name, true);
                    bool cpuRed = k == Kind.D || k == Kind.G || k == Kind.J;
                    var w = ZWorld.Create(settings, map, seed, level == "L17");
                    var blue = new ZBot(w, Team.Blue, seed + 101) { Difficulty = k == Kind.E ? ZBotDifficulty.Easy : ZBotDifficulty.Normal };
                    w.Bots.Add(blue); R.bots.Add(blue);
                    OzV11StrikeCount.StandInState standIn = null;
                    if (cpuRed) { var red = new ZBot(w, Team.Red, seed); w.Bots.Add(red); R.bots.Add(red); }
                    else standIn = OzV11StrikeCount.StandIn(w);
                    if (k == Kind.A || k == Kind.C || k == Kind.D || k == Kind.E || k == Kind.F || k == Kind.I) { if (DropShip(w, Team.Blue, Team.Blue) == null) R.bad.Add("no room for blue's ship"); }
                    if (k == Kind.B) { if (DropShip(w, Team.Neutral, Team.Blue) == null) R.bad.Add("no room for the empty ship"); }
                    ZObject humanShip = null; ZObject humanSquad = null; int humanStep = 0; double humanAt = 0;
                    if (k == Kind.H)
                    {
                        humanShip = w.PlayableShuttle;
                        if (humanShip != null) humanSquad = Hold(w, "grunt", humanShip.CenterX - 64, humanShip.CenterY + 48);
                        if (humanShip == null || humanSquad == null) R.bad.Add("no human ship or squad");
                    }
                    w.Events.Clear();
                    double nextSample = 1.0, nextGift = GiftEverySec;
                    bool gifts = standIn != null && k != Kind.H, decoyDone = !gifts;
                    var evSeen = new Dictionary<ZBot, int>();
                    foreach (var b in R.bots) evSeen[b] = 0;
                    int noteSeen = w.ShuttleNoteSeq;
                    var lastFly = new Dictionary<int, double>();                       // ship id -> time of the plan's last flight order
                    var hoverSince = new Dictionary<int, double>(); var enterSince = new Dictionary<int, (int ship, double t)>();
                    var cpuTeams = new HashSet<Team>(R.bots.Select(b => b.Team));
                    while (w.Time < secs && !w.GameOver)
                    {
                        w.Tick(ZConst.TICK);
                        if (standIn != null) OzV11StrikeCount.StandInKeep(w, standIn);
                        w.Events.Clear();
                        // the human's ship flow (H): board, fly, land, EXIT, the player's own commands
                        if (k == Kind.H && humanShip != null && humanSquad != null && w.Time >= humanAt)
                        {
                            humanAt = w.Time + 1.0;
                            if (humanStep == 0 && w.Time > 5) { OzV11Kit.PlayerEnter(w, humanSquad, humanShip); humanStep = 1; }
                            else if (humanStep == 1 && humanShip.Passengers.Count > 0 && humanShip.ShuttleCrewSettled)
                            {
                                int tx = humanShip.CenterX + (w.Map.WidthPix / 2 - humanShip.CenterX) / 3, ty = humanShip.CenterY + (w.Map.HeightPix / 2 - humanShip.CenterY) / 3;
                                for (int r = 0; r <= 12 && humanStep == 1; r++)
                                    for (int dy = -r; dy <= r && humanStep == 1; dy++)
                                        for (int dx = -r; dx <= r && humanStep == 1; dx++)
                                        {
                                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                                            int x = ((tx >> 4) + dx) * 16 + 8, y = ((ty >> 4) + dy) * 16 + 8;
                                            if (!w.ShuttlePadFree(x, y, humanShip, out _)) continue;
                                            w.SendWaypoints(Team.Red, humanShip, new[] { ZWaypoint.Move(x, y) }); humanStep = 2;
                                        }
                            }
                            else if (humanStep == 2 && humanShip.ShuttleLandings > 0 && humanShip.ShuttleState == ZShuttleState.Landed && humanShip.ShuttleLift <= 0 && w.Time > 30)
                            { if (w.Eject(Team.Red, humanShip)) humanStep = 3; }
                        }
                        // the gifts and the decoy (stand-in matches)
                        if (!decoyDone && w.Time >= DecoySec)
                        {
                            decoyDone = true;
                            var far = w.Flags.Where(f => f.Owner != Team.Red).OrderByDescending(f => Math.Min(FortDist(w, Team.Blue, f), FortDist(w, Team.Red, f))).ThenBy(f => f.Id).FirstOrDefault();
                            if (far != null)
                            {
                                w.AwardZone(far, Team.Red);
                                Hold(w, "psycho", far.CenterX - 32, far.CenterY + 32); Hold(w, "psycho", far.CenterX + 32, far.CenterY + 32); Hold(w, "light", far.CenterX, far.CenterY - 48);
                                R.decoyId = far.Id; R.decoyThreatAt = Defended(w, Team.Blue, far.CenterX, far.CenterY, out _);
                            }
                        }
                        if (gifts && w.Time >= nextGift)
                        {
                            nextGift += GiftEverySec;
                            var give = w.Flags.Where(f => f.Owner == Team.Blue && f.Id != R.decoyId).OrderByDescending(f => FortDist(w, Team.Blue, f)).ThenBy(f => f.Id).FirstOrDefault();
                            if (give != null) { w.AwardZone(give, Team.Red); R.gifts++; }
                        }
                        // the plan's new rows
                        foreach (var b in R.bots)
                        {
                            var evs = b.CpuShuttleEvents;
                            for (int i = evSeen[b]; i < evs.Count; i++)
                            {
                                var e = evs[i];
                                var s = w.GetAny(e.ship);
                                switch (e.kind)
                                {
                                    case CpuShuttleEventKind.TakeOff:
                                    case CpuShuttleEventKind.Divert:
                                    {
                                        if (e.kind == CpuShuttleEventKind.TakeOff) R.takeOffs++; else R.diverts++;
                                        var f = w.Get(e.flag);
                                        int df = f != null ? Defended(w, b.Team, f.CenterX, f.CenterY, out string wf) : 99; int ds = Defended(w, b.Team, e.x, e.y, out string wsp);
                                        string zone = f == null ? "?" : f.Owner == Team.Neutral ? "neutral" : f.Owner == b.Team ? "OWN" : "enemy";
                                        bool ok = f != null && f.Owner != b.Team && df == 0 && ds == 0 && e.threatFlag == 0 && e.threatSpot == 0;
                                        if (!ok) { R.takeOffBad++; R.bad.Add($"{e.time:F0} s {b.Team} {e.kind} to flag #{e.flag} ({zone}): the check's measure flag {df}, spot {ds}, the rule's {e.threatFlag}/{e.threatSpot}"); }
                                        if (e.flag == R.decoyId) R.decoyPicked++;
                                        double fl = s != null ? Math.Sqrt((double)(e.x - s.CenterX) * (e.x - s.CenterX) + (double)(e.y - s.CenterY) * (e.y - s.CenterY)) : 0;
                                        R.rows.Add($"{e.time,6:F0} s {b.Team,-4} {e.kind,-7} ship #{e.ship} ({e.aboard} aboard) -> flag #{e.flag} ({zone}) spot tile {e.x >> 4},{e.y >> 4}, {fl / 8:F0} m{(e.viaX >= 0 ? $" via tile {e.viaX >> 4},{e.viaY >> 4}" : "")}; defence: flag {df}, spot {ds} (rule {e.threatFlag}/{e.threatSpot}){(e.note != null ? " (" + e.note + ")" : "")}");
                                        lastFly[e.ship] = w.Time;
                                        break;
                                    }
                                    case CpuShuttleEventKind.Home: R.homeFlights++; lastFly[e.ship] = w.Time; break;
                                    case CpuShuttleEventKind.Landed:
                                    {
                                        R.landings++;
                                        string why = "";
                                        bool ok = s != null && w.ShuttlePadFreeAt(s.CenterX, s.CenterY, s, s.ShuttleHeadingDeg, out why);
                                        if (s != null) foreach (var o in w.Objects)
                                        {
                                            if (o.Kind == ZKind.MapItem && o.Type == "flag")
                                            {
                                                long ex = Math.Max(0, Math.Max(o.LocX - s.CenterX, s.CenterX - (o.LocX + o.WidthPix))), ey = Math.Max(0, Math.Max(o.LocY - s.CenterY, s.CenterY - (o.LocY + o.HeightPix)));
                                                if (ex * ex + ey * ey < 40 * 40) { ok = false; why += " a flag within 5 m"; }
                                            }
                                            if (o.IsFort && o.Owner != b.Team && o.Owner != Team.Neutral && !o.Destroyed)
                                            {
                                                long ex = Math.Max(0, Math.Max(o.LocX - s.CenterX, s.CenterX - (o.LocX + o.WidthPix))), ey = Math.Max(0, Math.Max(o.LocY - s.CenterY, s.CenterY - (o.LocY + o.HeightPix)));
                                                if (ex * ex + ey * ey < 264L * 264) { ok = false; why += " in an enemy fort's reach"; }
                                            }
                                        }
                                        if (s != null && w.InExitLane(s.CenterX, s.CenterY, 0)) { ok = false; why += " an exit lane"; }
                                        int dl = s != null ? Defended(w, b.Team, s.CenterX, s.CenterY, out _) : 0;
                                        if (!ok) { R.landBad++; R.bad.Add($"{e.time:F0} s {b.Team} landing of ship #{e.ship} at tile {e.x >> 4},{e.y >> 4}: {why}"); }
                                        R.rows.Add($"{e.time,6:F0} s {b.Team,-4} Landed  ship #{e.ship} tile {e.x >> 4},{e.y >> 4}: landing rule {(ok ? "kept" : "BROKEN " + why)}; defence there now {dl}");
                                        break;
                                    }
                                    case CpuShuttleEventKind.Unloaded: R.unloads++; R.rows.Add($"{e.time,6:F0} s {b.Team,-4} Unload  ship #{e.ship}, leader #{e.unit}{(e.note != null ? " (" + e.note + ")" : "")}"); break;
                                    case CpuShuttleEventKind.Captured: R.captures++; R.rows.Add($"{e.time,6:F0} s {b.Team,-4} CAPTURED flag #{e.flag} by the landed squad"); break;
                                    case CpuShuttleEventKind.CaptureFailed: R.rows.Add($"{e.time,6:F0} s {b.Team,-4} capture failed, flag #{e.flag} ({e.note})"); break;
                                    case CpuShuttleEventKind.EmptyTaken: R.emptyTaken++; R.rows.Add($"{e.time,6:F0} s {b.Team,-4} EMPTY ship #{e.ship} taken (pilot {(s != null ? s.Drivers.Count : 0)}, aboard {(s != null ? s.Passengers.Count : 0)}, owner {(s != null ? s.Owner.ToString() : "?")})"); break;
                                    case CpuShuttleEventKind.EmptyOrder: R.rows.Add($"{e.time,6:F0} s {b.Team,-4} squad #{e.unit} ordered into the EMPTY ship #{e.ship}; defence there {Defended(w, b.Team, e.x, e.y, out _)}"); break;
                                    case CpuShuttleEventKind.EmptyFailed: R.rows.Add($"{e.time,6:F0} s {b.Team,-4} empty ship #{e.ship} not taken ({e.note})"); break;
                                    case CpuShuttleEventKind.BoardFailed: R.boardFails++; break;
                                    case CpuShuttleEventKind.Hover: R.hovers++; R.rows.Add($"{e.time,6:F0} s {b.Team,-4} hover over the spot ({e.note}): next spot"); break;
                                    case CpuShuttleEventKind.Lost: R.lost++; R.rows.Add($"{e.time,6:F0} s {b.Team,-4} ship #{e.ship} lost ({e.note})"); break;
                                    case CpuShuttleEventKind.NoTarget: R.noTarget++; break;
                                    case CpuShuttleEventKind.RunEnd: R.rows.Add($"{e.time,6:F0} s {b.Team,-4} run ends ({e.note}); the next take-off from {b.CpuShuttleCooldownUntil:F0} s"); break;
                                }
                            }
                            evSeen[b] = evs.Count;
                        }
                        // the ships' own notes: every take-off of a computer's ship follows an order of the plan
                        if (w.ShuttleNoteSeq != noteSeen)
                        {
                            foreach (var n in w.ShuttleNotes)
                            {
                                if (n.seq <= noteSeen) continue;
                                R.notesCount++;
                                var s = w.GetAny(n.id);
                                if (s == null || !cpuTeams.Contains(s.Owner)) continue;
                                if (n.kind == ZShuttleNoteKind.TakeOff)
                                {
                                    R.cpuFlights++;
                                    if (w.Bots.Any(b => b.Team == s.Owner && b.CpuRepairUnitId == s.Id)) R.repairFlights++;   // the computer's repair rule (DV137) flies it to a station's deck and back
                                    else if (!lastFly.TryGetValue(s.Id, out double t) || w.Time - t > 20.0) { R.flightsNotByPlan++; R.bad.Add($"{w.Time:F0} s a take-off of {s.Owner}'s ship #{s.Id} that the plan did not order"); }
                                }
                                if (n.kind == ZShuttleNoteKind.Landed) R.landingsHomeOrTarget++;
                            }
                            noteSeen = w.ShuttleNoteSeq;
                        }
                        if (w.Time + 1e-6 < nextSample) continue;
                        nextSample += 1.0;
                        // once a second: hovering ships, robots on an ENTER to a ship
                        foreach (var o in w.Objects)
                        {
                            if (o.Destroyed || o.KillAt >= 0) { hoverSince.Remove(o.Id); enterSince.Remove(o.Id); continue; }
                            if (o.IsShuttle && cpuTeams.Contains(o.Owner))
                            {
                                if (o.ShuttleState == ZShuttleState.Hover) { if (!hoverSince.ContainsKey(o.Id)) hoverSince[o.Id] = w.Time; R.maxHoverSec = Math.Max(R.maxHoverSec, w.Time - hoverSince[o.Id]); }
                                else hoverSince.Remove(o.Id);
                            }
                            if (o.IsRobot && !o.IsMinion && cpuTeams.Contains(o.Owner))
                            {
                                int ship = -1;
                                foreach (var wp in o.Waypoints) if (wp.mode == WpMode.Enter) { var t = w.Get(wp.refId); if (t != null && t.IsShuttle) { ship = t.Id; break; } }
                                if (ship >= 0)
                                {
                                    if (!enterSince.TryGetValue(o.Id, out var es) || es.ship != ship) { enterSince[o.Id] = (ship, w.Time); R.cpuEnters++; }
                                    else R.maxEnterSec = Math.Max(R.maxEnterSec, w.Time - es.t);
                                }
                                else enterSince.Remove(o.Id);
                            }
                        }
                    }
                    R.end = w.Time; R.endWhy = w.GameOver ? "won by " + w.Winner : "time";
                    R.hash = Hash(w);
                    var sb = new StringBuilder();
                    foreach (var b in R.bots) foreach (var e in b.CpuShuttleEvents) sb.Append($"{e.time:F2}:{e.kind}:{e.ship}:{e.unit}:{e.flag}:{e.x}:{e.y};");
                    R.plan = sb.ToString();
                    // cooldown and stuck, per computer player
                    foreach (var b in R.bots)
                    {
                        double scale = b.EasySlowScale;
                        double lastEnd = -1; bool first = true;
                        var open = new Dictionary<int, (CpuShuttleEventKind k, double t)>();
                        foreach (var e in b.CpuShuttleEvents)
                        {
                            if (e.kind == CpuShuttleEventKind.TakeOff)
                            {
                                if (first && e.time + 1e-6 < ZWorld.CpuShuttleFirstSec * scale) { R.firstBad++; R.bad.Add($"{b.Team}: the first run took off at {e.time:F0} s"); }
                                if (lastEnd >= 0) { double gap = e.time - lastEnd; R.minGap = Math.Min(R.minGap, gap); if (gap + 1e-6 < ZWorld.CpuShuttleCooldownSec * scale) { R.cooldownBad++; R.bad.Add($"{b.Team}: a run took off {gap:F0} s after the last one ended"); } }
                                first = false;
                            }
                            if (e.kind == CpuShuttleEventKind.RunEnd) lastEnd = e.time;
                            // what is open for the ship, and for how long
                            if (open.TryGetValue(e.ship, out var op)) { R.maxOpenSec = Math.Max(R.maxOpenSec, e.time - op.t); if (e.time - op.t > Limit(op.k, scale)) { R.stuck++; R.bad.Add($"{b.Team} ship #{e.ship}: {op.k} at {op.t:F0} s open for {e.time - op.t:F0} s"); } open.Remove(e.ship); }
                            if (e.kind == CpuShuttleEventKind.Board || e.kind == CpuShuttleEventKind.TakeOff || e.kind == CpuShuttleEventKind.Divert || e.kind == CpuShuttleEventKind.Home) open[e.ship] = (e.kind, e.time);
                        }
                        foreach (var kv in open) { double age = R.end - kv.Value.t; R.maxOpenSec = Math.Max(R.maxOpenSec, age); if (age > Limit(kv.Value.k, scale) && !(w.GameOver && age < 200)) { R.stuck++; R.bad.Add($"{b.Team} ship #{kv.Key}: {kv.Value.k} at {kv.Value.t:F0} s still open at the end"); } }
                    }
                    if (R.maxEnterSec > ZWorld.CpuShuttleCaptureSec + 5) { R.enterLong++; R.bad.Add($"a computer's robot held an ENTER on a ship for {R.maxEnterSec:F0} s"); }
                    if (R.maxHoverSec > 40) { R.hoverLong++; R.bad.Add($"a computer's ship hovered {R.maxHoverSec:F0} s in a row"); }
                    R.done = true;
                }
                catch (Exception ex) { R.bad.Add("the match threw " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace?.Split('\n').FirstOrDefault()); }
                finally { ZWorld.ShuttleTeam = team0; }
            }
            return R;
        }

        /// <summary>The longest a step may stay open (s) before the next row of its ship: boarding, a flight to a target, a flight home.</summary>
        static double Limit(CpuShuttleEventKind k, double scale) =>
            k == CpuShuttleEventKind.Board ? ZWorld.CpuShuttleBoardSec * scale + 3 : k == CpuShuttleEventKind.Home ? ZWorld.CpuShuttleHomeSec + 3 : ZWorld.CpuShuttleFlightSec + ZWorld.CpuShuttleUnloadSec + 3;

        static string Line(Run r) =>
            $"{r.name} {r.level}: to {r.end:F0} s ({r.endWhy}); take-offs {r.takeOffs}, diverts {r.diverts}, landings at a target {r.landings}, unloads {r.unloads}, captures {r.captures}, " +
            $"empty ships taken {r.emptyTaken}, flights home {r.homeFlights}, hovers {r.hovers}, boardings failed {r.boardFails}, ships lost {r.lost}, gifts {r.gifts}; " +
            $"computer ship take-offs (notes) {r.cpuFlights}, ENTER orders on ships {r.cpuEnters}; shortest cooldown {(r.minGap < double.MaxValue ? r.minGap.ToString("F0") + " s" : "-")}, longest open step {r.maxOpenSec:F0} s, longest ENTER {r.maxEnterSec:F0} s, longest hover {r.maxHoverSec:F0} s";

        /// <summary>The whole check (menu OpenZ/V1.2/CPU: 12 Shuttles Check (headless)); secs = each match's length, seeds = how many seeds
        /// of the six rule matches (A to E and J).</summary>
        public static string Check(string settings, string m02, string m16, string m17, double secs = MatchSec, int seeds = 1)
        {
            var sb = new StringBuilder();
            var verdicts = new List<string>();
            var t0 = DateTime.Now;
            sb.AppendLine($"1.2 item 12, the computer flies its shuttles (DV138), headless; matches of {secs:F0} s, {seeds} seed(s); the cooldown {ZWorld.CpuShuttleCooldownSec:F0} s (the Easy computer 1.3 x), the first run from {ZWorld.CpuShuttleFirstSec:F0} s");
            sb.AppendLine("A level 2, blue + its ship vs the stand-in; B level 2, an EMPTY ship by blue's fort; C level 16, blue + its ship vs the stand-in; J level 16, computer vs computer, the level's own ship the red computer's; D level 17, computer vs computer, the level's shuttles + a blue ship; E level 2 as A, blue EASY");
            var runs = new List<Run>();
            for (int s = 1; s <= Math.Max(1, seeds); s++)
            {
                runs.Add(Match(settings, m02, "L02", Kind.A, true, secs, s));
                runs.Add(Match(settings, m02, "L02", Kind.B, true, secs, s + 1));
                runs.Add(Match(settings, m16, "L16", Kind.C, true, secs, s));
                runs.Add(Match(settings, m16, "L16", Kind.J, true, secs, s));
                runs.Add(Match(settings, m17, "L17", Kind.D, true, secs, s));
                runs.Add(Match(settings, m02, "L02", Kind.E, true, secs, s + 2));
            }
            foreach (var r in runs)
            {
                sb.AppendLine(Line(r));
                foreach (var row in r.rows) sb.AppendLine("    " + row);
                foreach (var b in r.bad) sb.AppendLine("    BAD " + b);
            }
            string Per(Func<Run, int> f) => string.Join(", ", runs.GroupBy(r => r.name + " " + r.level).Select(g => g.Key + " " + g.Sum(f)));
            int takeOffs = runs.Sum(r => r.takeOffs + r.diverts), bad = runs.Sum(r => r.takeOffBad);
            verdicts.Add(OzV11Kit.Verdict(takeOffs >= 5 * seeds && bad == 0 && runs.All(r => r.done), $"P1 flies only to undefended targets: {takeOffs} take-offs / turns to a target, every one with the check's own defence measure 0 at the flag and at the landing spot and the flag neutral or the enemy's ({bad} not) [take-offs: {Per(r => r.takeOffs)}]"));
            int lands = runs.Sum(r => r.landings), lbad = runs.Sum(r => r.landBad);
            verdicts.Add(OzV11Kit.Verdict(lands >= 3 * seeds && lbad == 0, $"P2 lands legally: {lands} landings at a target, each kept the landing spot rule on its heading, 5 m and more from every flag, out of every exit lane, out of every enemy fort's reach ({lbad} not)"));
            int unl = runs.Sum(r => r.unloads), cap = runs.Sum(r => r.captures);
            verdicts.Add(OzV11Kit.Verdict(unl >= 3 * seeds && cap >= 2 * seeds && runs.Where(r => r.name == "A" || r.name == "C").All(r => r.captures >= 1), $"P3 unloads and captures: {unl} squads let out at a target, {cap} flags captured by them [{Per(r => r.captures)}]"));
            int cbad = runs.Sum(r => r.cooldownBad + r.firstBad); double gap = runs.Min(r => r.minGap);
            verdicts.Add(OzV11Kit.Verdict(cbad == 0 && runs.Count(r => r.minGap < double.MaxValue) >= 2, $"P4 cooldown: every next run took off {ZWorld.CpuShuttleCooldownSec:F0} s or more after the last one ended (the Easy computer 1.3 x), the first after {ZWorld.CpuShuttleFirstSec:F0} s; the shortest gap seen {(gap < double.MaxValue ? gap.ToString("F0") + " s" : "-")} ({cbad} too soon)"));
            var Bs = runs.Where(r => r.name == "B").ToList();
            verdicts.Add(OzV11Kit.Verdict(Bs.All(r => r.emptyTaken >= 1 && r.takeOffs >= 1), $"P5 empty ships taken: B {Bs.Sum(r => r.emptyTaken)} (then {Bs.Sum(r => r.takeOffs)} runs with them), D {runs.Where(r => r.name == "D").Sum(r => r.emptyTaken)} (the car park's)"));
            int stuck = runs.Sum(r => r.stuck + r.enterLong + r.hoverLong);
            verdicts.Add(OzV11Kit.Verdict(stuck == 0, $"P6 no stuck loops: every boarding, flight and flight home ended in time (longest open step {runs.Max(r => r.maxOpenSec):F0} s), no ENTER on a ship held longer than {ZWorld.CpuShuttleCaptureSec + 5:F0} s (longest {runs.Max(r => r.maxEnterSec):F0} s), no ship hovering 40 s ({stuck} not)"));
            int notPlan = runs.Sum(r => r.flightsNotByPlan);
            verdicts.Add(OzV11Kit.Verdict(notPlan == 0, $"P7 only the plan flies the computer's ships: {runs.Sum(r => r.cpuFlights)} take-offs, each within 20 s of the plan's order or on a repair trip of the computer ({runs.Sum(r => r.repairFlights)}, DV137) ({notPlan} not)"));
            var dec = runs.Where(r => r.decoyId >= 0).ToList();
            verdicts.Add(OzV11Kit.Verdict(dec.Count > 0 && dec.All(r => r.decoyThreatAt > 0) && bad == 0, $"P8 never into a defended territory: the defended decoys ({dec.Count}, defence {string.Join("/", dec.Select(r => r.decoyThreatAt))} when made) picked {runs.Sum(r => r.decoyPicked)} times, each only while undefended (P1)"));
            // the switch off; no ship; the human's ship; determinism
            var F1 = Match(settings, m02, "L02", Kind.F, false, Math.Min(secs, 480), 1); var F2 = Match(settings, m02, "L02", Kind.F, false, Math.Min(secs, 480), 1);
            sb.AppendLine(Line(F1));
            verdicts.Add(OzV11Kit.Verdict(F1.done && F1.cpuFlights == 0 && F1.cpuEnters == 0 && F1.plan.Length == 0 && F1.hash == F2.hash,
                $"P9 the switch off: no computer ship took off ({F1.cpuFlights}), no computer robot was ordered into a ship ({F1.cpuEnters}), the plan logged nothing; the same world hash twice ({F1.hash:x16} / {F2.hash:x16})"));
            var G0 = Match(settings, m02, "L02", Kind.G, false, 360, 4); var G1 = Match(settings, m02, "L02", Kind.G, true, 360, 4);
            verdicts.Add(OzV11Kit.Verdict(G0.done && G1.done && G0.hash == G1.hash && G1.plan.Length == 0, $"P10 a match without a shuttle: the same world hash with the switch off and on ({G0.hash:x16} / {G1.hash:x16}, to {G1.end:F0} s)"));
            var H0 = Match(settings, m16, "L16", Kind.H, false, 240, 1); var H1 = Match(settings, m16, "L16", Kind.H, true, 240, 1);
            verdicts.Add(OzV11Kit.Verdict(H0.done && H1.done && H0.hash == H1.hash && H0.notesCount == H1.notesCount && H0.notesCount >= 3, $"P11 the human's ship: boarded, flown, landed and unloaded by the player's commands, the same world hash and the same {H1.notesCount} shuttle notes with the switch off and on ({H0.hash:x16} / {H1.hash:x16})"));
            var I1 = Match(settings, m02, "L02", Kind.I, true, Math.Min(secs, 600), 1); var I2 = Match(settings, m02, "L02", Kind.I, true, Math.Min(secs, 600), 1);
            verdicts.Add(OzV11Kit.Verdict(I1.done && I1.hash == I2.hash && I1.plan == I2.plan && I1.plan.Length > 0, $"P12 deterministic: A's match twice with the switch on, the same world hash ({I1.hash:x16}) and the same plan log ({I1.plan.Split(';').Length - 1} rows)"));
            foreach (var v in verdicts) sb.AppendLine(v);
            int fails = verdicts.Count(v => v.StartsWith("FAIL"));
            sb.AppendLine($"RESULT {verdicts.Count - fails} PASS, {fails} FAIL ({(DateTime.Now - t0).TotalSeconds:F0} s)");
            return sb.ToString();
        }
    }
}
