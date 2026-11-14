using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Version 1.3 item 12, level 7 "Pyro Technics": the counted checks of the TODO. Headless simulation (ZWorld.Create + Tick): no scene,
    /// no Play mode, nothing saved; only public members of the simulation and of OzLavaPops.
    ///   M  the level matches the Zod map p02_bb_orig07.map: size, every tile, zones, every object (the file itself when it is next to the
    ///      project, refs/zod_engine; else the facts table below), 332 lava tiles all of class Impassable, start units of both sides
    ///   R  a red squad (grunt, psycho, tough) ordered onto the deepest lava tile: every member walks in, loses LavaBiteHp after each
    ///      whole second inside, its heat is HP lost / (lost + left) after every bite, it melts at the 12th bite = 12.0 s after it stepped
    ///      in (the death a fire death, LavaKilled), walks at 0.7 of its speed in lava, never stands in a rock, building or cliff tile
    ///   O  the same squad ordered back out after the 5th bite: it walks out, loses nothing more, its heat is 0 within 4 s, nobody stays in
    ///   V  every vehicle type ordered onto the same lava tile for 30 s: ticks with a vehicle on lava = 0
    ///   Z  the rule off: the squad never sets foot on lava (Zod), full health after 30 s
    ///   L1 a red squad on the near bank of the two-tile river sent onto its far half: every member in within 8 s, nobody walks more than 8 tiles
    ///   L2 the same squad left in lava with no order: each member walks out by itself 5 s after its last order (or sooner, pushed out with its squad), nobody stays in
    ///   C  a whole match of the two computer players on level 7 with the rule on (CpuSeconds of game time): units on lava = 0, lava
    ///      orders = 0
    ///   S  levels 2, 16 and 17: SameSeconds of computer against computer with the rule on and off end in the same state, tick for tick
    ///   P  the pops (View/OzLavaPops.cs, the game's own seed and four more): per minute over PopMinutes min: mean PopMeanMin..PopMeanMax,
    ///      the quietest minute PopQuietMin or more, the busiest at least PopSwing times the quietest, minute counts vary (CV PopCvMin or
    ///      more) and the gaps are not a rhythm (gap CV PopGapCvMin or more; a metronome is 0, plain chance 1)
    /// Menu: OpenZ/L07 Lava/Checks/Count lava rule (all) and the short form without C. The last line reads "PASS n/n" when every line passed.
    /// </summary>
    public static class OzL07LavaCount
    {
        public const string Level = "L07", MapFile = "p02_bb_orig07.map";
        public const double CpuSeconds = 900, SameSeconds = 300, OutAfterBite = 5, CoolTol = 0.05;
        public const int PopMinutes = 30, PopQuietMin = 4, PopMeanMin = 25, PopMeanMax = 70;
        public const double PopSwing = 3.0, PopCvMin = 0.30, PopGapCvMin = 1.2;
        static readonly string[] SquadTypes = { "grunt", "psycho", "tough" };
        static readonly string[] VehicleTypes = { "jeep", "light", "medium", "heavy", "apc", "missile_launcher", "crane" };

        // ------------------------------------------------------------------ the facts of p02_bb_orig07.map (decoded 29 Oct 2026)
        const int MapW = 84, MapH = 110, Zones = 10, Flags = 8, Rocks = 877, Grenades = 12, Huts = 12, Guns = 6, LavaTiles = 332, HotTiles = 22;

        public static string Run(string settings, Func<string, string> mapText, bool cpuMatch = true, string zodMapPath = null)
        {
            var sb = new StringBuilder(); int pass = 0, total = 0;
            void V(bool ok, string what) { total++; if (ok) pass++; sb.AppendLine(OzV11Kit.Verdict(ok, what)); }
            string json = mapText(Level);
            var map = ZMapData.FromJson(json);

            // M: the map
            {
                int Count(string kind) => map.objects.Count(o => o.kind == kind);
                bool size = map.width == MapW && map.height == MapH && map.file == MapFile && map.planet == "volcanic";
                bool objs = map.zones.Count == Zones && Count("map_item:flag") == Flags && Count("map_item:rock") == Rocks && Count("map_item:grenades") == Grenades
                            && Count("map_item:hut") == Huts && Count("cannon:gun") == Guns && Count("building:vehicle_factory") == 4 && Count("building:robot_factory") == 3
                            && Count("building:radar") == 1 && map.objects.Any(o => o.kind == "building:fort_front" && o.owner == "blue" && o.x == 37 && o.y == 6)
                            && map.objects.Any(o => o.kind == "building:fort_back" && o.owner == "red" && o.x == 37 && o.y == 93)
                            && map.objects.Any(o => o.kind == "building:robot_factory" && o.x == 73 && o.y == 40 && o.blevel == 3)
                            && map.objects.Any(o => o.kind == "building:radar" && o.x == 48 && o.y == 56);
                string Start(string side) => string.Join(" ", map.objects.Where(o => o.owner == side && (o.kind.StartsWith("robot:") || o.kind.StartsWith("vehicle:") || o.kind.StartsWith("cannon:")))
                                                         .GroupBy(o => o.kind).OrderBy(g => g.Key).Select(g => g.Count() + " " + g.Key));
                string blue = Start("blue"), red = Start("red");
                bool starts = blue == "2 cannon:gun 3 robot:psycho 1 robot:tough 1 vehicle:light" && red == "2 cannon:gun 2 robot:psycho 1 robot:tough 1 vehicle:light";
                int lava = 0, hot = 0, notImp = 0;
                for (int ty = 0; ty < map.height; ty++) for (int tx = 0; tx < map.width; tx++)
                    {
                        int k = map.LavaKind(tx, ty); if (k == 0) continue; lava++; if (k == 2) hot++;
                        if (map.ClassAt(tx, ty) != ZClass.Impassable) notImp++;
                    }
                V(size && objs, $"M1 level 7 = {map.file} ({map.planet}) {map.width} x {map.height}, {map.zones.Count} zones, {Count("map_item:flag")} flags, forts blue (37,6) red (37,93), " +
                                $"4 vehicle + 3 robot factories (level 3 at 73,40), radar (48,56), {Count("cannon:gun")} guns, {Count("map_item:rock")} rocks, {Count("map_item:grenades")} grenade boxes, {Count("map_item:hut")} huts");
                V(starts, $"M2 start units: blue {blue} | red {red}");
                V(lava == LavaTiles && hot == HotTiles && notImp == 0 && map.HasLava, $"M3 lava: {lava} tiles ({hot} hot pools), {notImp} of them not Impassable (want {LavaTiles} / {HotTiles} / 0)");
                if (zodMapPath != null && File.Exists(zodMapPath))
                {
                    string diff = CompareZod(File.ReadAllBytes(zodMapPath), map);
                    V(diff == "", "M4 every tile, zone (six widened to the side strips) and object equals the Zod file " + MapFile + (diff == "" ? "" : ": " + diff));
                }
                else sb.AppendLine("info  M4 skipped: " + MapFile + " not found next to the project (refs/zod_engine)");
            }

            // the deepest lava tile and a dry start spot 3 to 6 tiles from it with a straight line in
            var probe = ZWorld.Create(settings, OzV11Kit.NoUnits(json), 7);
            var (lx, ly) = Deepest(map);
            int lavaX = lx * 16 + 8, lavaY = ly * 16 + 8;
            if (!StartSpot(probe, lavaX, lavaY, out int sx, out int sy)) { V(false, "no dry start spot near the deepest lava tile"); return Finish(sb, pass, total); }
            sb.AppendLine($"info  deepest lava tile ({lx},{ly}); squads start at ({sx >> 4},{sy >> 4})");

            using (OzV11Kit.GameSwitches())
            {
                ZWorld.LavaDeviation = true;
                double idle0 = ZWorld.LavaIdleSeconds; ZWorld.LavaIdleSeconds = 1e9;                      // R and O want a robot that stays: no walk out of a forgotten robot here (lines L1, L2 count it)
                // R: in and melt
                foreach (var type in SquadTypes)
                {
                    var r = Squad(settings, json, type, sx, sy, lavaX, lavaY, -1);
                    V(r.ok, $"R {type} squad into lava: {r.text}");
                }
                // O: in, then out after the 5th bite
                {
                    var r = Squad(settings, json, "grunt", sx, sy, lavaX, lavaY, OutAfterBite);
                    V(r.ok, $"O grunt squad in, out after bite {OutAfterBite}: {r.text}");
                }
                // V: vehicles
                {
                    var bad = new List<string>(); int onLava = 0;
                    foreach (var vt in VehicleTypes)
                    {
                        var w = OzV11Kit.World(settings, OzV11Kit.NoUnits(json), 11);
                        OzV11Kit.Dummy(w, 16 * 4, 16 * 4); RedDummy(w);
                        if (!VehicleSpot(w, lavaX, lavaY, out int vx, out int vy)) { bad.Add(vt + ": no spot"); continue; }
                        var v = OzV11Kit.Place(w, vt, vx, vy, Team.Red);
                        OzV11Kit.PlayerMove(w, v, lavaX, lavaY);
                        int n = 0; double end = w.Time + 30;
                        while (w.Time < end) { w.Tick(ZConst.TICK); w.Events.Clear(); if (OnLava(w, v)) n++; }
                        onLava += n; if (n > 0) bad.Add(vt + " " + n + " ticks");
                    }
                    V(onLava == 0 && bad.Count == 0, $"V vehicles ordered onto lava 30 s each ({string.Join(", ", VehicleTypes)}): ticks on lava {onLava}{(bad.Count > 0 ? " (" + string.Join("; ", bad) + ")" : "")}");
                }
                // Z: the rule off
                {
                    ZWorld.LavaDeviation = false;
                    var w = OzV11Kit.World(settings, OzV11Kit.NoUnits(json), 13);
                    OzV11Kit.Dummy(w, 16 * 4, 16 * 4); RedDummy(w);
                    var lead = OzV11Kit.Place(w, "grunt", sx, sy, Team.Red); var sq = OzV11Kit.Squad(lead).ToList();
                    OzV11Kit.PlayerMove(w, lead, lavaX, lavaY);
                    int n = 0; double end = w.Time + 30;
                    while (w.Time < end) { w.Tick(ZConst.TICK); w.Events.Clear(); foreach (var m in sq) if (OzV11Kit.Alive(m) && w.InLavaNow(m)) n++; }
                    bool full = sq.All(m => OzV11Kit.Alive(m) && m.Health == m.MaxHealth);
                    V(n == 0 && full && w.LavaOrders == 0, $"Z rule off: robot ticks on lava {n}, full health {full}, lava orders {w.LavaOrders}");
                    ZWorld.LavaDeviation = true;
                }
                ZWorld.LavaIdleSeconds = idle0;
                foreach (var line in OzV13LavaOrdersCount.Short(settings, json)) V(line.ok, line.text);   // L1, L2: a click on the far half of a river, a squad forgotten in lava (Editor/OzV13LavaOrdersCount.cs)
                // C: computer against computer
                if (cpuMatch)
                {
                    var w = ZWorld.Create(settings, json, 1);
                    w.Bots.Add(new ZBot(w, Team.Blue, 102)); w.Bots.Add(new ZBot(w, Team.Red, 1));
                    int robots = 0, vehicles = 0; double end = w.Time + CpuSeconds;
                    while (w.Time < end && !w.GameOver)
                    {
                        w.Tick(ZConst.TICK); w.Events.Clear();
                        foreach (var o in w.Objects)
                        {
                            if (o.Destroyed || o.KillAt >= 0) continue;
                            if (o.IsRobot && w.InLavaNow(o)) robots++;
                            else if (o.Kind == ZKind.Vehicle && OnLava(w, o)) vehicles++;
                        }
                    }
                    V(robots == 0 && vehicles == 0 && w.LavaOrders == 0 && w.LavaSteps == 0,
                      $"C computer vs computer {w.Time:F0} s of game time (game over {w.GameOver}): robot ticks on lava {robots}, vehicle ticks {vehicles}, lava orders {w.LavaOrders}, steps in {w.LavaSteps}");
                }
                // S: the other levels play the same game with the rule on and off, tick for tick (no lava there)
                if (cpuMatch)
                    foreach (var lv in new[] { "L02", "L16", "L17" })
                    {
                        string mj; try { mj = mapText(lv); } catch { mj = null; }
                        if (mj == null) { sb.AppendLine("info  S " + lv + " not installed"); continue; }
                        string Sig(bool rule)
                        {
                            ZWorld.LavaDeviation = rule;
                            var w = ZWorld.Create(settings, mj, 3);
                            w.Bots.Add(new ZBot(w, Team.Blue, 102)); w.Bots.Add(new ZBot(w, Team.Red, 1));
                            double end = w.Time + SameSeconds; while (w.Time < end && !w.GameOver) { w.Tick(ZConst.TICK); w.Events.Clear(); }
                            var sg = new StringBuilder(); foreach (var o in w.Objects) sg.Append(o.Id).Append(':').Append(o.LocX).Append(',').Append(o.LocY).Append(',').Append(o.Health).Append(';');
                            return w.Time.ToString("F2") + "|" + w.Objects.Count + "|" + sg;
                        }
                        string on = Sig(true), off = Sig(false); ZWorld.LavaDeviation = true;
                        V(on == off, $"S {lv}: {SameSeconds:F0} s computer vs computer with the lava rule on = off, tick for tick ({(on == off ? "same" : "DIFFERENT")} end state)");
                    }
            }

            // P: the pops
            {
                var tiles = new List<(int, int, int)>();
                for (int ty = 0; ty < map.height; ty++) for (int tx = 0; tx < map.width; tx++) { int k = map.LavaKind(tx, ty); if (k > 0) tiles.Add((tx, ty, k)); }
                foreach (int seed in new[] { 1907, 1, 2, 3, 4 })
                {
                    var p = new OzLavaPops(tiles, seed); var got = new List<OzLavaPops.Pop>();
                    for (int f = 0; f < PopMinutes * 60 * 60; f++) p.Step(1.0 / 60.0, got);             // 60 frames a second
                    var perMin = new int[PopMinutes]; foreach (var q in got) perMin[Math.Min(PopMinutes - 1, (int)(q.t / 60))]++;
                    double mean = perMin.Average(), sd = Math.Sqrt(perMin.Select(c => (c - mean) * (c - mean)).Average());
                    var gaps = got.Zip(got.Skip(1), (a, b) => b.t - a.t).ToList();
                    double gm = gaps.Count > 0 ? gaps.Average() : 0, gcv = gm > 0 ? Math.Sqrt(gaps.Select(g => (g - gm) * (g - gm)).Average()) / gm : 0;
                    int quiet = perMin.Min(), busy = perMin.Max();
                    bool ok = mean >= PopMeanMin && mean <= PopMeanMax && quiet >= PopQuietMin && busy >= PopSwing * quiet && sd / mean >= PopCvMin && gcv >= PopGapCvMin;
                    V(ok, $"P pops seed {seed}: {got.Count} in {PopMinutes} min, {mean:F1} a minute (quietest {quiet}, busiest {busy}), minute CV {sd / mean:F2}, gap CV {gcv:F2}, big {got.Count(q => q.big)}");
                }
            }
            return Finish(sb, pass, total);
        }

        static string Finish(StringBuilder sb, int pass, int total) { sb.AppendLine((pass == total ? "PASS " : "FAIL ") + pass + "/" + total); return sb.ToString(); }

        // ------------------------------------------------------------------ the squad cases
        sealed class SqResult { public bool ok; public string text; }

        /// <summary>A red squad of <paramref name="type"/> at (sx, sy) ordered onto (lx, ly); outAfter &lt; 0: it stays until it melts (or 60 s),
        /// else after the leader's bite number outAfter it is ordered back to (sx, sy).</summary>
        static SqResult Squad(string settings, string json, string type, int sx, int sy, int lx, int ly, double outAfter)
        {
            var w = OzV11Kit.World(settings, OzV11Kit.NoUnits(json), 5);
            OzV11Kit.Dummy(w, 16 * 4, 16 * 4); RedDummy(w);
            var lead = OzV11Kit.Place(w, type, sx, sy, Team.Red); var sq = OzV11Kit.Squad(lead).ToList();
            OzV11Kit.PlayerMove(w, lead, lx, ly, false);
            var tIn = new Dictionary<int, double>(); var tOut = new Dictionary<int, double>(); var bites = sq.ToDictionary(m => m.Id, m => new List<(double t, int hp, double heat)>());
            var hp = sq.ToDictionary(m => m.Id, m => m.Health); var deathT = new Dictionary<int, double>(); var fireDeath = new HashSet<int>();
            var coolAt = new Dictionary<int, double>();                                                  // O: the first tick each member stands out of the lava with heat 0 (its own cooling time)
            int inWall = 0, speedBad = 0, speedSeen = 0, lostAfterOut = 0; bool orderedOut = false; double coolBy = -1;
            double end = w.Time + 60;
            while (w.Time < end)
            {
                w.Tick(ZConst.TICK);
                foreach (var e in w.Events) if (e.type == ZEventType.Death && sq.Any(m => m.Id == e.objId)) { deathT[e.objId] = w.Time; if (e.flag) fireDeath.Add(e.objId); }
                w.Events.Clear();
                foreach (var m in sq)
                {
                    if (m.Health < hp[m.Id]) { if (tOut.ContainsKey(m.Id)) lostAfterOut += hp[m.Id] - m.Health; else bites[m.Id].Add((w.Time, hp[m.Id] - m.Health, m.LavaHeat)); }   // also the bite that melts it
                    hp[m.Id] = m.Health;
                    if (!OzV11Kit.Alive(m)) continue;
                    if (w.LavaHardTiles(m, m.LocX, m.LocY) > 0) inWall++;
                    bool now = w.InLavaNow(m);
                    if (now && !tIn.ContainsKey(m.Id)) tIn[m.Id] = w.Time;
                    if (!now && tIn.ContainsKey(m.Id) && !tOut.ContainsKey(m.Id)) tOut[m.Id] = w.Time;
                    if (!now && tOut.ContainsKey(m.Id) && m.LavaHeat == 0 && !coolAt.ContainsKey(m.Id)) coolAt[m.Id] = w.Time;
                    if (now && m.IsMoving && !m.IsRunning) { speedSeen++; if (Math.Abs(m.RealMoveSpeed - m.MoveSpeed * ZWorld.LavaWalkSpeed) > 1e-6) speedBad++; }
                }
                if (outAfter >= 0 && !orderedOut && OzV11Kit.Alive(lead) && bites[lead.Id].Count >= outAfter) { orderedOut = true; OzV11Kit.PlayerMove(w, lead, sx, sy, false); }
                if (outAfter >= 0 && orderedOut && sq.All(m => !OzV11Kit.Alive(m) || (!w.InLavaNow(m) && m.LavaHeat == 0))) { if (coolBy < 0) coolBy = w.Time; if (w.Time > coolBy + 2) break; }
                if (outAfter < 0 && sq.All(m => !OzV11Kit.Alive(m))) break;
            }
            int bite0 = ZWorld.LavaBiteHp(lead);
            var lines = new List<string>(); bool ok = inWall == 0 && speedBad == 0 && speedSeen > 0;
            foreach (var m in sq)
            {
                bool entered = tIn.ContainsKey(m.Id); var b = bites[m.Id];
                bool rate = entered && b.Count > 0 && b.Select((x, k) => Math.Abs(x.t - (tIn[m.Id] + ZWorld.LavaBiteEvery * (k + 1))) < ZConst.TICK * 1.5).All(x => x)
                            && b.Take(b.Count - 1).All(x => x.hp == bite0);
                bool heat = Heats(b, m.MaxHealth);
                if (outAfter < 0)
                {
                    bool died = deathT.ContainsKey(m.Id) && entered, melt = died && fireDeath.Contains(m.Id) && m.LavaKilled;
                    double life = died ? deathT[m.Id] - tIn[m.Id] : -1;
                    bool atTime = died && Math.Abs(life - ZWorld.LavaMeltBites * ZWorld.LavaBiteEvery) < ZConst.TICK * 1.5 && b.Count == ZWorld.LavaMeltBites;
                    ok &= entered && rate && heat && melt && atTime;
                    lines.Add($"#{m.MemberIndex} in {(entered ? tIn[m.Id].ToString("F2") : "never")} s, {b.Count} bites of {bite0} HP, melted {(died ? life.ToString("F2") + " s after stepping in" : "no")}, fire death {fireDeath.Contains(m.Id)}, LavaKilled {m.LavaKilled}");
                }
                else
                {
                    bool outOk = entered && tOut.ContainsKey(m.Id) && OzV11Kit.Alive(m) && !w.InLavaNow(m) && m.LavaHeat == 0;
                    ok &= entered && rate && heat && outOk && lostAfterOut == 0;
                    lines.Add($"#{m.MemberIndex} in {(entered ? tIn[m.Id].ToString("F2") : "never")} s, out {(tOut.ContainsKey(m.Id) ? tOut[m.Id].ToString("F2") : "never")} s, {b.Count} bites, alive {OzV11Kit.Alive(m)}, heat now {m.LavaHeat:F2}");
                }
            }
            if (outAfter >= 0)
            {   // each member's own cooling: heat 0 at most LavaCoolSeconds after IT stepped out (the members step out at different ticks)
                double worst = sq.Where(OzV11Kit.Alive).Select(m => tOut.ContainsKey(m.Id) && coolAt.ContainsKey(m.Id) ? coolAt[m.Id] - tOut[m.Id] : 999).DefaultIfEmpty(999).Max();
                ok &= w.LavaWalkOuts > 0 && coolBy >= 0 && worst <= ZWorld.LavaCoolSeconds + CoolTol;
                lines.Add($"HP lost after stepping out {lostAfterOut}, walks out {w.LavaWalkOuts}, slowest cooling {worst:F2} s (want <= {ZWorld.LavaCoolSeconds + CoolTol:F2})");
            }
            lines.Add($"ticks inside a rock / building / cliff tile {inWall}, speed in lava = x{ZWorld.LavaWalkSpeed} on {speedSeen - speedBad}/{speedSeen} ticks, lava orders {w.LavaOrders}, minions sent on in {w.LavaFollowIns}");
            return new SqResult { ok = ok, text = string.Join(" | ", lines) };
        }

        /// <summary>Heat after the k-th bite = lava HP lost / (lost + HP left), rising, 1 at the melt.</summary>
        static bool Heats(List<(double t, int hp, double heat)> b, int max)
        {
            int lost = 0; double last = 0;
            foreach (var x in b)
            {
                lost += x.hp; int left = Math.Max(0, max - lost);
                double want = lost / (double)(lost + left);
                if (Math.Abs(x.heat - want) > 1e-9 || x.heat + 1e-12 < last) return false;
                last = x.heat;
            }
            return true;
        }

        // ------------------------------------------------------------------ places
        static (int tx, int ty) Deepest(ZMapData m)
        {
            int W = m.width, H = m.height; var d = new int[W * H]; var q = new Queue<int>();
            for (int i = 0; i < W * H; i++) { d[i] = m.lava[i] > 0 ? -1 : 0; if (d[i] == 0) q.Enqueue(i); }
            while (q.Count > 0)
            {
                int c = q.Dequeue(), cx = c % W, cy = c / W;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = cx + dx, ny = cy + dy; if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                        int n = ny * W + nx; if (d[n] != -1) continue; d[n] = d[c] + 1; q.Enqueue(n);
                    }
            }
            int best = -1, bi = 0; for (int i = 0; i < W * H; i++) if (d[i] > best) { best = d[i]; bi = i; }   // first in row order on a tie
            return (bi % W, bi / W);
        }

        /// <summary>A dry robot tile 3 to 6 tiles from (lx, ly) with a clean straight line in (row order: the same spot every run).</summary>
        static bool StartSpot(ZWorld w, int lx, int ly, out int sx, out int sy)
        {
            int tx0 = lx >> 4, ty0 = ly >> 4;
            for (int r = 3; r <= 6; r++)
                for (int ty = ty0 - r; ty <= ty0 + r; ty++)
                    for (int tx = tx0 - r; tx <= tx0 + r; tx++)
                    {
                        if (Math.Max(Math.Abs(tx - tx0), Math.Abs(ty - ty0)) != r || !w.Path.TilePassable(tx, ty, true) || w.Map.LavaAt(tx, ty)) continue;
                        if (!w.LavaLineClean(tx * 16 + 8, ty * 16 + 8, lx, ly)) continue;
                        sx = tx * 16 + 8; sy = ty * 16 + 8; return true;
                    }
            sx = sy = 0; return false;
        }

        /// <summary>A free 2 x 2 vehicle spot (centre on a tile corner) 3 to 8 tiles from (lx, ly).</summary>
        static bool VehicleSpot(ZWorld w, int lx, int ly, out int vx, out int vy)
        {
            int tx0 = lx >> 4, ty0 = ly >> 4;
            for (int r = 3; r <= 8; r++)
                for (int ty = ty0 - r; ty <= ty0 + r; ty++)
                    for (int tx = tx0 - r; tx <= tx0 + r; tx++)
                    {
                        if (Math.Max(Math.Abs(tx - tx0), Math.Abs(ty - ty0)) != r) continue;
                        if (!w.Path.TilePassable(tx, ty, false) || !w.Path.TilePassable(tx + 1, ty, false) || !w.Path.TilePassable(tx, ty + 1, false) || !w.Path.TilePassable(tx + 1, ty + 1, false)) continue;
                        vx = tx * 16 + 16; vy = ty * 16 + 16; return true;
                    }
            vx = vy = 0; return false;
        }

        /// <summary>A red grunt that cannot move, shoot or die, in the far south-west corner: red never runs out of units in a case.</summary>
        static void RedDummy(ZWorld w)
        {
            var d = w.CreateRobotGroup("grunt", 16 * 4 - 8, (w.Map.height - 5) * 16 - 8, Team.Red, 1);
            d.MoveSpeed = 0; d.Damage = 0; d.MaxHealth = d.Health = 1000000000;
        }

        /// <summary>Any tile under the unit's box (shrunk by 1 px) is lava.</summary>
        static bool OnLava(ZWorld w, ZObject o)
        {
            int x = o.LocX + 1, y = o.LocY + 1, x1 = o.LocX + o.WidthPix - 2, y1 = o.LocY + o.HeightPix - 2;
            for (int j = y >> 4; j <= y1 >> 4; j++) for (int i = x >> 4; i <= x1 >> 4; i++) if (w.Map.LavaAt(i, j)) return true;
            return false;
        }

        static string CompareZod(byte[] d, ZMapData m)
        {
            int U16(int o) => d[o] | (d[o + 1] << 8);
            int w = U16(0), h = U16(2), objs = U16(56), zones = U16(60), off = 62;
            if (w != m.width || h != m.height) return $"size {w} x {h}";
            if (zones != m.zones.Count) return $"zones {zones}";
            for (int i = 0; i < zones; i++, off += 8)
            {
                var z = m.zones[i];
                int zx = z.x, zw = z.w; if (i == 0 || i == 3 || i == 7) { zx += 3; zw -= 3; } else if (i == 2 || i == 6 || i == 8) zw -= 5;   /* 1.3 note 5: L07's side strips are walkable, these six zones reach the new edge */ if (U16(off) != zx || U16(off + 2) != z.y || U16(off + 4) != zw || U16(off + 6) != z.h) return "zone " + i;
            }
            if (objs != m.objects.Count) return $"objects {objs} (json {m.objects.Count})";
            for (int i = 0; i < objs; i++, off += 16)
            {
                var o = m.objects[i];
                if (U16(off) != o.x || U16(off + 2) != o.y || d[off + 5] != o.ot || d[off + 6] != o.oid) return $"object {i} ({o.kind} at {o.x},{o.y})";
            }
            for (int i = 0; i < w * h; i++, off += 2) if (U16(off) != m.tiles[i]) return $"tile {i % w},{i / w}";
            return off == d.Length ? "" : $"file length {d.Length} (read {off})";
        }

#if UNITY_EDITOR
        static string ZodPath => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../../../refs/zod_engine/" + MapFile));
        [UnityEditor.MenuItem("OpenZ/L07 Lava/Checks/Count lava rule (all)")]
        static void Menu() => OzV11Kit.RunMenu("L07 lava rule", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText, true, ZodPath));
        [UnityEditor.MenuItem("OpenZ/L07 Lava/Checks/Count lava rule, short (no computer match)")]
        static void MenuShort() => OzV11Kit.RunMenu("L07 lava rule, short", () => Run(OzV11Kit.SettingsText, OzV11Kit.MapText, false, ZodPath));

        /// <summary>Makes Resources/OpenZ/Lava/M_L07_Lava.mat on the shader OpenZ/Lava (so the build keeps the shader; View/OzLava.cs loads it), sets the import of LV1's maps and binds them. Safe to run twice.</summary>
        [UnityEditor.MenuItem("OpenZ/L07 Lava/Make lava material")]
        static void MakeMaterial()
        {
            const string dir = "Assets/OpenZ/Resources/OpenZ/Lava", path = dir + "/M_L07_Lava.mat";
            var sh = UnityEngine.Shader.Find("OpenZ/Lava");
            if (!sh) { UnityEngine.Debug.LogError("[OpenZ L07] shader OpenZ/Lava not found (Assets/OpenZ/Shaders/OpenZLava.shader)"); return; }
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets/OpenZ/Resources/OpenZ", "Lava");
            var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(path);
            if (!mat) { mat = new UnityEngine.Material(sh) { name = "M_L07_Lava" }; UnityEditor.AssetDatabase.CreateAsset(mat, path); }
            else mat.shader = sh;
            mat.SetTexture("_LumpMap", LavaMap(dir + "/T_Lava_Lumps.png", true));
            mat.SetTexture("_LumpNrm", LavaMap(dir + "/T_Lava_LumpsN.png", true));
            mat.SetTexture("_FieldMap", LavaMap(dir + "/T_L07_Field.png", false));
            UnityEditor.EditorUtility.SetDirty(mat);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("[OpenZ L07] lava material: " + path);
        }

        /// <summary>Data maps, linear. Lumps tile with mips and aniso 2 (fix 17); the field is clamped, no mips, never compressed: its numbers are read as numbers.</summary>
        static UnityEngine.Texture2D LavaMap(string path, bool lumps)
        {
            var ti = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
            if (!ti) { UnityEngine.Debug.LogError("[OpenZ L07] lava map missing: " + path); return null; }
            ti.textureType = UnityEditor.TextureImporterType.Default; ti.sRGBTexture = false; ti.alphaIsTransparency = false; ti.npotScale = UnityEditor.TextureImporterNPOTScale.None;
            ti.alphaSource = UnityEditor.TextureImporterAlphaSource.FromInput; ti.maxTextureSize = 2048; ti.mipmapEnabled = lumps;
            ti.wrapMode = lumps ? UnityEngine.TextureWrapMode.Repeat : UnityEngine.TextureWrapMode.Clamp;
            ti.filterMode = lumps ? UnityEngine.FilterMode.Trilinear : UnityEngine.FilterMode.Bilinear; ti.anisoLevel = lumps ? 2 : 0;
            ti.textureCompression = lumps ? UnityEditor.TextureImporterCompression.CompressedHQ : UnityEditor.TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(path);
        }
#endif
    }
}
