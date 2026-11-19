using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit crane_deck_sound_r3: the counted check of DV206 "a crane repairs a bridge from its land end" (Sim/ZObject.CraneLandEnd.cs), no
    /// Unity API (the kit's Mono harness runs it too). Levels 2, 7, 16, 17, 18 with their start units taken off (OzV11Kit.NoUnits; terrain,
    /// bridges, buildings stay), the game's switches (OzV11Kit.GameSwitches, DV61 / DV74 on), one red crane per test, a blue dummy far away.
    /// <para>1 Every bridge, from BOTH ends, destroyed: the crane is put 6 tiles out past that end and ordered to repair it; counted every tick
    /// until it is repaired (150 s at most): where the crane stops to work (its tile), whether its box is ever on a water tile or on the
    /// bridge's own box (the span), its gap to the bridge's end, the moment the work starts (CraneAnim on) and the moment the bridge is whole
    /// (Revived). DV206 on and off (Zod: the drive to the centre over the river, the owner's cause).</para>
    /// <para>2 Every bridge damaged but standing (half its health): the same crane, DV206 on: it mends it from its stop (DV61), never on the deck.</para>
    /// <para>3 One long bridge test twice: the same stop, the same times, the same rules fingerprint (deterministic).</para>
    /// </summary>
    public static class OzV15CraneLandEndCount
    {
        public static readonly string[] Levels = { "L02", "L07", "L16", "L17", "L18" };
        const double Limit = 150;

        internal sealed class Run1
        {
            public string level; public int id, tiles; public string type; public int end; public bool vertical;
            public bool sent, started, repaired; public double tStart = -1, tRepaired = -1;
            public int stopTx = -1, stopTy = -1, gapPx = -1000, toCentrePx = -1;
            public int wetTicks, spanTicks, ticks; public bool stopDry; public ulong sig; public string why = "";
        }

        static bool Wet(ZWorld w, ZObject c)
        {
            int x0 = c.LocX, y0 = c.LocY, x1 = c.LocX + c.WidthPix - 1, y1 = c.LocY + c.HeightPix - 1;
            for (int ty = y0 >> 4; ty <= y1 >> 4; ty++)
                for (int tx = x0 >> 4; tx <= x1 >> 4; tx++) { var k = w.Map.ClassAt(tx, ty); if (k == ZClass.Water || k == ZClass.DeepWater) return true; }
            return false;
        }

        static bool OnSpan(ZObject c, ZObject b) =>
            c.LocX < b.LocX + b.WidthPix && b.LocX < c.LocX + c.WidthPix && c.LocY < b.LocY + b.HeightPix && b.LocY < c.LocY + c.HeightPix;

        /// <summary>px between the crane's box and the bridge's box along the bridge's axis (negative = overlapping).</summary>
        static int Gap(ZObject c, ZObject b, bool vert)
        {
            if (vert) return c.CenterY < b.CenterY ? b.LocY - (c.LocY + c.HeightPix) : c.LocY - (b.LocY + b.HeightPix);
            return c.CenterX < b.CenterX ? b.LocX - (c.LocX + c.WidthPix) : c.LocX - (b.LocX + b.WidthPix);
        }

        static ZObject Dummy(ZWorld w, ZObject b)
        {
            var reach = w.Pathing != null ? w.Pathing.Reach : null; int bestX = -1, bestY = -1; long best = -1;
            for (int ty = 2; ty < w.Map.height - 2; ty += 3)
                for (int tx = 2; tx < w.Map.width - 2; tx += 3)
                {
                    if (!w.Path.TilePassable(tx, ty, true)) continue;
                    long d = (long)(tx * 16 - b.CenterX) * (tx * 16 - b.CenterX) + (long)(ty * 16 - b.CenterY) * (ty * 16 - b.CenterY);
                    if (d > best) { best = d; bestX = tx; bestY = ty; }
                }
            return bestX < 0 ? null : OzV11Kit.Dummy(w, bestX * 16 + 8, bestY * 16 + 8);
        }

        static bool CraneSpot(ZWorld w, ZObject b, int end, out int x, out int y)
        {
            x = y = 0;
            b.GetCraneEntrance(out int ex, out int ey, out int ex2, out int ey2);
            bool vert = b.Type == "bridge_vert";
            int bx = end == 0 ? ex : ex2, by = end == 0 ? ey : ey2, ox = vert ? 0 : (end == 0 ? -1 : 1), oy = vert ? (end == 0 ? -1 : 1) : 0;
            var reach = w.Pathing.Reach;
            for (int k = 96; k <= 192; k += 16)
            {
                int px = bx + ox * k, py = by + oy * k;
                var (tx, ty) = ZReach.FootTileAt(px, py, false);
                int c = reach.Comp(tx, ty, false, false);
                if (c < 0) continue;
                if (reach.Nearest(px, py, false, false, c, out x, out y, 6)) return true;
            }
            return false;
        }

        internal static Run1 Test(string settings, string level, string mapJson, int bridgeId, int end, bool dv206, bool damagedOnly, int seed = 1)
        {
            ZWorld.CraneLandEndDeviation = dv206;
            var w = OzV11Kit.World(settings, OzV11Kit.NoUnits(mapJson), seed);
            var b = w.Objects.FirstOrDefault(o => o.Id == bridgeId);
            var r = new Run1 { level = level, id = bridgeId, end = end };
            if (b == null) { r.why = "no such bridge"; return r; }
            r.type = b.Type; r.vertical = b.Type == "bridge_vert"; r.tiles = Math.Max(b.WidthPix, b.HeightPix) / 16;
            Dummy(w, b);
            if (damagedOnly) { b.SetHealth(Math.Max(1, b.MaxHealth / 2)); w.UpdateObjectHealth(b); }
            else { b.SetHealth(0); w.UpdateObjectHealth(b); }
            if (damagedOnly ? b.Destroyed || b.Health >= b.MaxHealth : !b.Destroyed) { r.why = "could not be " + (damagedOnly ? "damaged" : "destroyed"); return r; }
            if (!CraneSpot(w, b, end, out int cx, out int cy)) { r.why = "no free spot for the crane past this end"; return r; }
            var crane = w.CreateObject("vehicle", "crane", cx - 16, cy - 16, Team.Red);
            w.Events.Clear();
            r.sent = w.SendWaypoints(Team.Red, crane, new[] { new ZWaypoint { mode = WpMode.CraneRepair, refId = b.Id, x = b.CenterX, y = b.CenterY, playerGiven = true } });
            double t0 = w.Time; int maxTicks = (int)Math.Round(Limit / ZConst.TICK);
            for (int i = 0; i < maxTicks && !w.GameOver; i++)
            {
                w.Tick(ZConst.TICK); r.ticks++;
                foreach (var e in w.Events)
                {
                    if (e.type == ZEventType.CraneAnim && e.flag && e.objId == crane.Id && e.otherId == b.Id && !r.started)
                    {
                        r.started = true; r.tStart = w.Time - t0; r.stopTx = crane.CenterX >> 4; r.stopTy = crane.CenterY >> 4;
                        r.gapPx = Gap(crane, b, r.vertical); r.stopDry = !Wet(w, crane) && !OnSpan(crane, b);
                        r.toCentrePx = (int)Math.Round(Math.Sqrt((double)(crane.CenterX - b.CenterX) * (crane.CenterX - b.CenterX) + (double)(crane.CenterY - b.CenterY) * (crane.CenterY - b.CenterY)));
                    }
                    if (!damagedOnly && e.type == ZEventType.Revived && e.objId == b.Id && !r.repaired) { r.repaired = true; r.tRepaired = w.Time - t0; }
                }
                w.Events.Clear();
                if (crane.Destroyed) { r.why = "the crane died"; break; }
                if (r.started && Wet(w, crane)) r.wetTicks++;
                if (r.started && OnSpan(crane, b)) r.spanTicks++;
                if (damagedOnly && r.started && b.Health >= b.MaxHealth && !r.repaired) { r.repaired = true; r.tRepaired = w.Time - t0; }
                if (r.repaired && crane.Waypoints.Count == 0) break;
                if (!r.started && crane.Waypoints.Count == 0) { r.why = "the order ended before the work began"; break; }
            }
            r.sig = w.RulesFingerprint(1469598103934665603UL);
            return r;
        }

        static string M(int px) => (px / 8.0).ToString("0.0");

        public static string Run(string settings, Func<string, string> map, TextWriter progress = null)
        {
            bool was = ZWorld.CraneLandEndDeviation;
            var sb = new StringBuilder();
            try
            {
                sb.AppendLine("DV206 a crane repairs a bridge from its land end: every bridge of levels 2 / 7 / 16 / 17 / 18, from both ends (the crane put 6 tiles out past the end, ordered to repair; 150 s at most)");
                sb.AppendLine("level  bridge         tiles  end | DV206 ON: stop tile  dry  gap to end  to centre  work starts  repaired  ticks wet / on span | ZOD (DV206 off): repaired  ticks wet / on span (the drive to the centre)");
                int n = 0, okOn = 0, zodWet = 0, zodN = 0, bridges = 0; var times = new List<string>(); string longKey = null; var levelsWith = new List<string>();
                var dmg = new List<Run1>();
                foreach (var lv in Levels)
                {
                    string mj; try { mj = map(lv); } catch { sb.AppendLine(lv + "   (map not installed)"); continue; }
                    if (string.IsNullOrEmpty(mj)) { sb.AppendLine(lv + "   (map not installed)"); continue; }
                    ZWorld.CraneLandEndDeviation = false;
                    var probe = OzV11Kit.World(settings, OzV11Kit.NoUnits(mj), 1);
                    var ids = probe.Objects.Where(o => o.IsBridge).OrderBy(o => o.Id).Select(o => o.Id).ToList();
                    if (ids.Count == 0) { sb.AppendLine(lv + "   no bridges"); continue; }
                    levelsWith.Add(lv + " " + ids.Count);
                    foreach (var id in ids)
                    {
                        bridges++;
                        for (int end = 0; end < 2; end++)
                        {
                            var a = Test(settings, lv, mj, id, end, true, false); var z = Test(settings, lv, mj, id, end, false, false);
                            n++;
                            bool ok = a.sent && a.started && a.repaired && a.stopDry && a.wetTicks == 0 && a.spanTicks == 0 && a.why.Length == 0;
                            if (ok) okOn++;
                            if (z.started && z.repaired) { zodN++; if (z.wetTicks > 0 || z.spanTicks > 0) zodWet++; }
                            if (a.repaired && z.repaired) times.Add($"{a.tRepaired:0.0}/{z.tRepaired:0.0}");
                            if (a.tiles >= 10 && longKey == null && ok) longKey = lv + ":" + id + ":" + end;
                            sb.AppendLine(string.Format("{0,-5}  {1,-13}  {2,5}  {3,3} | {4,-14}  {5,-3}  {6,8} m  {7,7} m  {8,9} s  {9,7} s  {10,5} / {11,-7} | {12,9} s  {13,5} / {14}{15}",
                                lv, "#" + id + " " + (a.vertical ? "N-S" : "E-W"), a.tiles, end == 0 ? "A" : "B",
                                a.started ? $"({a.stopTx},{a.stopTy})" : "-", a.started ? (a.stopDry ? "yes" : "NO") : "-", a.started ? M(a.gapPx) : "-", a.started ? M(a.toCentrePx) : "-",
                                a.started ? a.tStart.ToString("0.0") : "-", a.repaired ? a.tRepaired.ToString("0.0") : "NO", a.wetTicks, a.spanTicks,
                                z.repaired ? z.tRepaired.ToString("0.0") : "NO", z.wetTicks, z.spanTicks,
                                (a.why.Length > 0 ? "  [on: " + a.why + "]" : "") + (z.why.Length > 0 ? "  [zod: " + z.why + "]" : "")));
                            progress?.WriteLine($"# {lv} bridge {id} end {end} counted");
                        }
                        var d = Test(settings, lv, mj, id, 0, true, true); d.type = d.type ?? "?"; dmg.Add(d);
                    }
                }
                sb.AppendLine("levels with bridges: " + string.Join(", ", levelsWith));
                sb.AppendLine(OzV11Kit.Verdict(n > 0 && okOn == n, $"DV206 on: every bridge end ({okOn} of {n}, {bridges} bridges): the crane stops on dry land off the span, works there, the bridge is repaired; its box never on water or on the span while it works"));
                sb.AppendLine(OzV11Kit.Verdict(zodN > 0 && zodWet == zodN, $"DV206 off (Zod): the crane drives onto the span / over the water to the centre on every destroyed bridge ({zodWet} of {zodN}): the owner's cause, measured"));
                sb.AppendLine("repair time DV206 / Zod (s, the same end): " + string.Join(" ", times));
                sb.AppendLine("damaged but standing (half health), DV206 on, from end A: " + string.Join("; ", dmg.Select(d => $"{d.level} #{d.id}: stop ({d.stopTx},{d.stopTy}) dry {d.stopDry}, gap {M(d.gapPx)} m, mended {d.repaired} in {d.tRepaired:0.0} s, on span {d.spanTicks}{(d.why.Length > 0 ? " [" + d.why + "]" : "")}")));
                sb.AppendLine(OzV11Kit.Verdict(dmg.Count > 0 && dmg.All(d => d.started && d.repaired && d.stopDry && d.spanTicks == 0 && d.wetTicks == 0),
                    $"DV206 on: a damaged bridge that still stands is mended from the same dry stop ({dmg.Count(d => d.started && d.repaired && d.stopDry && d.spanTicks == 0)} of {dmg.Count})"));
                if (longKey != null)
                {
                    var p = longKey.Split(':'); string mj = map(p[0]); int id = int.Parse(p[1]), end = int.Parse(p[2]);
                    var a = Test(settings, p[0], mj, id, end, true, false); var b = Test(settings, p[0], mj, id, end, true, false);
                    sb.AppendLine(OzV11Kit.Verdict(a.sig == b.sig && a.stopTx == b.stopTx && a.stopTy == b.stopTy && a.tStart == b.tStart && a.tRepaired == b.tRepaired,
                        $"deterministic: {p[0]} long bridge #{id} end {(end == 0 ? "A" : "B")} twice: stop ({a.stopTx},{a.stopTy}) / ({b.stopTx},{b.stopTy}), repaired {a.tRepaired:0.00} / {b.tRepaired:0.00} s, fingerprint {a.sig:x16} / {b.sig:x16}"));
                }
                else sb.AppendLine("FAIL  no long bridge test to repeat");
            }
            finally { ZWorld.CraneLandEndDeviation = was; }
            return sb.ToString();
        }

        /// <summary>The parity row's short form: level 18's first long bridge and level 2's first bridge, from end A, DV206 on and off.</summary>
        public static string Quick(string settings, Func<string, string> map)
        {
            bool was = ZWorld.CraneLandEndDeviation;
            var sb = new StringBuilder();
            try
            {
                foreach (var lv in new[] { "L18", "L02" })
                {
                    string mj; try { mj = map(lv); } catch { continue; }
                    if (string.IsNullOrEmpty(mj)) continue;
                    ZWorld.CraneLandEndDeviation = false;
                    var probe = OzV11Kit.World(settings, OzV11Kit.NoUnits(mj), 1);
                    var br = probe.Objects.Where(o => o.IsBridge).OrderByDescending(o => Math.Max(o.WidthPix, o.HeightPix)).ThenBy(o => o.Id).FirstOrDefault();
                    if (br == null) continue;
                    var a = Test(settings, lv, mj, br.Id, 0, true, false); var z = Test(settings, lv, mj, br.Id, 0, false, false);
                    sb.AppendLine(OzV11Kit.Verdict(a.started && a.repaired && a.stopDry && a.wetTicks == 0 && a.spanTicks == 0 && (z.wetTicks > 0 || z.spanTicks > 0),
                        $"{lv} bridge #{br.Id} ({Math.Max(br.WidthPix, br.HeightPix) / 16} tiles): DV206 stop ({a.stopTx},{a.stopTy}) dry, {M(a.gapPx)} m from its end, repaired in {a.tRepaired:0.0} s; Zod: on the span / water {z.spanTicks} / {z.wetTicks} ticks, repaired in {z.tRepaired:0.0} s"));
                }
            }
            finally { ZWorld.CraneLandEndDeviation = was; }
            return sb.ToString();
        }
    }
}
