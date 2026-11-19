using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit shuttle_nearest_landing: the counted check of DV190 (Sim/ZWorld.ShuttleNearestLanding.cs), headless (ZWorld.Create + Tick, no
    /// scene; public members only). Every click goes through ZWorld.ShuttleOrdersFilter exactly as the view's PlayerInput.Flush hands it over,
    /// then what is left is sent with SendWaypoints. Rows:
    ///  A. the search list: every tile offset within ShuttleNearestTiles, nearest first, ties by dy then dx;
    ///  B. level 17 (full: and 16), a red ship: a click on a building / a flag / rocks / a closed tile / water / the map's edge band: the order is
    ///     kept and goes to a tile middle where it can land, at the least distance a brute-force scan of the radius finds (ties as the list);
    ///     sent, it flies there and lands on it (no hover);
    ///  C. nothing within the radius (a click 12 tiles off the map; full: also an in-map place, when the map has one): refused as before (taken
    ///     out of the click, its NO-LAND count +1), the ship stays;
    ///  D. a click where it can land: with DV190 on and off the same order, field by field, and the same world 25 s later (every plain field of
    ///     every object, the waypoints, the next random number);
    ///  E. DV190 off: every click of B refused exactly as before (the order untouched, no DV190 count);
    ///  F. the computer: a computer team's click is left alone; ANYWHERE (no filter), STOP and the repair order untouched; a route's last point
    ///     (DV125) goes to the nearest spot, its middle point as given; (full) level 17 computer against computer with its shuttle rules
    ///     (DV137 / DV138 / DV134 / DV144) on: DV190 off / on / on again: the same fingerprints;
    ///  G. (full) level 17's ground, every 4th tile: the share of clicks refused before (a place where it can never land) and now;
    ///  H. the search's quick sure "never" (ZWorld.ShuttleNearestSureNever) only where the full test says never too (quick: 32 x 32 tiles round
    ///     the ship on level 17; full: also level 17's every 4th tile and level 16 round its ship).
    /// </summary>
    public static class OzV15ShuttleNearestCount
    {
        const double Tick = ZConst.TICK;
        /// <summary>The computer-against-computer match of row F (full): long enough for the computer's shuttle runs (2 min apart).</summary>
        const double CpuSec = 420.0;

        public static string Quick(string settings, Func<string, string> map) => Run(settings, map, false);

        public static string Run(string settings, Func<string, string> map, bool full = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DV190 shuttle nearest landing, counted (Sim/ZWorld.ShuttleNearestLanding.cs)" + (full ? "" : " (quick)"));
            bool was = ZWorld.ShuttlePlayableDeviation, wasOn = ZWorld.ShuttleOn, wasNear = ZWorld.ShuttleNearestLandingDeviation; var team0 = ZWorld.ShuttleTeam;
            try
            {
                ZWorld.ShuttlePlayableDeviation = true; ZWorld.ShuttleOn = true; ZWorld.ShuttleTeam = Team.Red;
                ZWorld.ShuttleNearestLandingDeviation = true;
                Offsets(sb);
                foreach (var lv in full ? new[] { "L17", "L16" } : new[] { "L17" }) Level(sb, settings, map(lv), lv, full);
                Others(sb, settings, map("L17"));
                Sure(sb, settings, map("L17"), "L17", 16, 1);
                if (full) { Sure(sb, settings, map("L16"), "L16", 16, 1); Sure(sb, settings, map("L17"), "L17 (the whole map)", 200, 4); }
                if (full) { Cpu(sb, settings, map("L17")); Share(sb, settings, map("L17")); }
            }
            catch (Exception e) { sb.AppendLine(OzV11Kit.Verdict(false, "the check threw " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace?.Split('\n').FirstOrDefault())); }
            finally { ZWorld.ShuttlePlayableDeviation = was; ZWorld.ShuttleOn = wasOn; ZWorld.ShuttleNearestLandingDeviation = wasNear; ZWorld.ShuttleTeam = team0; }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ helpers
        static void Run(ZWorld w, double secs, Func<bool> until = null)
        {
            double end = w.Time + secs;
            while (w.Time < end) { w.Tick(Tick); w.Events.Clear(); if (until != null && until()) return; }
        }

        static string P(int x, int y) => x + "," + y;
        static string M(double px) => (px / 8.0).ToString("0.0") + " m";

        /// <summary>Until it stands on the ground again or hovers (at most max s), then the rough landing's settle: "landed" / "hover" / "air".</summary>
        static string Settle(ZWorld w, ZObject s, double max = 120)
        {
            double t0 = w.Time; bool left = false;
            Run(w, max, () =>
            {
                if (s.ShuttleState != ZShuttleState.Landed) left = true;
                return s.Destroyed || (left && w.Time - t0 > 0.5 && (s.ShuttleState == ZShuttleState.Landed || s.ShuttleState == ZShuttleState.Hover));
            });
            Run(w, ZWorld.ShuttleRoughSettleSec + 0.1, () => s.ShuttleLevelled && w.Time >= s.ShuttleRampHoldUntil);
            return s.ShuttleState == ZShuttleState.Landed ? "landed" : s.ShuttleState == ZShuttleState.Hover ? "hover" : "air";
        }

        /// <summary>One click as PlayerInput collects it (each unit its own list), through the filter; the lists after it.</summary>
        static Dictionary<ZObject, List<ZWaypoint>> Click(ZWorld w, Team team, ZObject s, IList<ZWaypoint> order, bool landableOnly, out List<ZObject> refused)
        {
            var pending = new Dictionary<ZObject, List<ZWaypoint>> { [s] = new List<ZWaypoint>(order) };
            refused = new List<ZObject>();
            w.ShuttleOrdersFilter(team, pending, landableOnly, refused);
            return pending;
        }

        static ZWaypoint Move(int x, int y, int refId = -1) { var wp = ZWaypoint.Move(x, y, true); wp.refId = refId; return wp; }

        static string W(ZWaypoint p) => $"{p.mode}/{p.refId}@{p.x},{p.y}/{p.attackTo}/{p.playerGiven}/{p.forceFire}/{p.fightOnWay}/{p.leash}";

        /// <summary>The own repair station under (x, y) (ZWorld.ShuttleStationAt's rule, from public members).</summary>
        static bool Station(ZWorld w, ZObject s, int x, int y) =>
            s != null && w.Objects.Any(b => b.Kind == ZKind.Building && b.CanRepairUnit(s.Owner) && x >= b.LocX && x < b.LocX + b.WidthPix && y >= b.LocY && y < b.LocY + b.HeightPix);

        /// <summary>The brute-force answer: every tile within the radius whose middle the ship can land on (ShuttleNeverLands false; not the
        /// edge band, not an own repair station), those with no unit on the pad now first; the least dx*dx + dy*dy, ties by dy then dx. false = none.</summary>
        static bool Brute(ZWorld w, ZObject s, int x, int y, out int bx, out int by, out int bd)
        {
            bx = by = 0; bd = int.MaxValue; int r = ZWorld.ShuttleNearestTiles, tx0 = x >> 4, ty0 = y >> 4;
            var all = new List<(int clear, int d, int dy, int dx, int cx, int cy)>();
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int d = dx * dx + dy * dy;
                    if (d == 0 || d > r * r) continue;
                    int tx = tx0 + dx, ty = ty0 + dy;
                    if (tx < 0 || ty < 0 || tx >= w.Map.width || ty >= w.Map.height) continue;
                    int cx = tx * 16 + 8, cy = ty * 16 + 8;
                    var c = w.ShuttleClamp(cx, cy);
                    if (c.x != cx || c.y != cy || Station(w, s, cx, cy) || w.ShuttleNeverLands(cx, cy, s, out _)) continue;
                    all.Add((w.ShuttleNearestClear(cx, cy, s) ? 0 : 1, d, dy, dx, cx, cy));
                }
            if (all.Count == 0) return false;
            var b = all.OrderBy(a => a.clear).ThenBy(a => a.d).ThenBy(a => a.dy).ThenBy(a => a.dx).First();
            bx = b.cx; by = b.cy; bd = b.d;
            return true;
        }

        static readonly Dictionary<int, (bool never, string why)> memo = new Dictionary<int, (bool, string)>(); static ZWorld memoWorld;

        /// <summary>The nearest tile middle (from (cx, cy), out of the edge band) where the ship can never land for the reason `why`.</summary>
        static bool Never(ZWorld w, ZObject s, string why, int cx, int cy, int maxTiles, out int px, out int py)
        {
            px = py = 0; int e = ZWorld.ShuttleEdgeTiles;
            if (w != memoWorld) { memo.Clear(); memoWorld = w; }
            var cand = new List<(long d, int tx, int ty)>();
            for (int ty = Math.Max(e, (cy >> 4) - maxTiles); ty < Math.Min(w.Map.height - e, (cy >> 4) + maxTiles); ty++)
                for (int tx = Math.Max(e, (cx >> 4) - maxTiles); tx < Math.Min(w.Map.width - e, (cx >> 4) + maxTiles); tx++)
                {
                    int x = tx * 16 + 8, y = ty * 16 + 8;
                    cand.Add(((long)(x - cx) * (x - cx) + (long)(y - cy) * (y - cy), tx, ty));
                }
            foreach (var (d, tx, ty) in cand.OrderBy(c => c.d))
            {
                int x = tx * 16 + 8, y = ty * 16 + 8, key = ty * 65536 + tx;
                if (!memo.TryGetValue(key, out var m)) { bool nv = w.ShuttleNeverLands(x, y, s, out string wy); memo[key] = m = (nv, wy); }
                if (m.never && m.why == why) { px = x; py = y; return true; }
            }
            return false;
        }

        /// <summary>A free 6 x 6 m pad (the landing spot rule now) between minT and maxT tiles from (cx, cy).</summary>
        static bool PadSpot(ZWorld w, ZObject s, int cx, int cy, int minT, int maxT, out int px, out int py)
        {
            for (int r = minT * 16; r <= maxT * 16; r += 16)
                for (int a = 0; a < 32; a++)
                {
                    double g = a * Math.PI / 16;
                    var (x, y) = w.ShuttleClamp(cx + (int)(Math.Cos(g) * r), cy + (int)(Math.Sin(g) * r));
                    if (!Station(w, s, x, y) && w.ShuttlePadFree(x, y, s, out _) && !w.ShuttleNeverLands(x, y, s, out _)) { px = x; py = y; return true; }
                }
            px = py = 0; return false;
        }

        /// <summary>A world and a red ship standing on a free pad near red's first building (or the map's middle).</summary>
        static ZWorld Setup(string settings, string mapJson, out ZObject s)
        {
            var w = OzV11Kit.World(settings, mapJson, 1);
            var home = w.Objects.FirstOrDefault(o => o.Kind == ZKind.Building && o.Owner == Team.Red && !o.Destroyed);
            int hx = home != null ? home.CenterX : w.Map.width * 8, hy = home != null ? home.CenterY : w.Map.height * 8;
            s = null;
            if (PadSpot(w, null, hx, hy, 3, 30, out int px, out int py)) s = w.CreatePlayableShuttle(px, py, Team.Red, 0f);
            if (s != null) s.MaxHealth = s.Health = 1000000000;                                                   // the flights over enemy ground: never shot down (the check is about where it goes)
            return w;
        }

        static readonly FieldInfo[] F = typeof(ZObject).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string)).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();

        static ulong Fnv(ulong h, string s) { foreach (char c in s) { h ^= c; h *= 1099511628211UL; } return h; }

        /// <summary>Every plain field of every object, its waypoints, then the next random number (call last: it draws one).</summary>
        static ulong Print(ZWorld w)
        {
            ulong h = 1469598103934665603UL; var sb = new StringBuilder();
            foreach (var o in w.Objects)
            {
                sb.Clear().Append(o.Id).Append('{');
                foreach (var f in F)
                {
                    var x = f.GetValue(o);
                    sb.Append(x is double dv ? BitConverter.DoubleToInt64Bits(dv).ToString() : x is float fv ? BitConverter.ToInt32(BitConverter.GetBytes(fv), 0).ToString() : Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                }
                foreach (var p in o.Waypoints) sb.Append(W(p)).Append(';');
                h = Fnv(h, sb.Append('}').ToString());
            }
            return Fnv(h, w.Rng.Next(1 << 30).ToString());
        }

        // ------------------------------------------------------------------ A. the search list
        static void Offsets(StringBuilder sb)
        {
            var o = ZWorld.ShuttleNearestOffsets; int n = o.Length / 2, r = ZWorld.ShuttleNearestTiles; bool ok = n > 0; int want = 0;
            for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++) { int d = dx * dx + dy * dy; if (d > 0 && d <= r * r) want++; }
            for (int i = 1; i < n && ok; i++)
            {
                int d0 = o[2 * i - 2] * o[2 * i - 2] + o[2 * i - 1] * o[2 * i - 1], d1 = o[2 * i] * o[2 * i] + o[2 * i + 1] * o[2 * i + 1];
                ok = d0 < d1 || (d0 == d1 && (o[2 * i - 1] < o[2 * i + 1] || (o[2 * i - 1] == o[2 * i + 1] && o[2 * i - 2] < o[2 * i])));
            }
            sb.AppendLine(OzV11Kit.Verdict(ok && n == want, $"A the search list: {n} tile offsets (every one within {r} tiles = {r * 2} m: {want}), nearest first, ties by dy then dx, whole numbers"));
        }

        // ------------------------------------------------------------------ B / C / E on one level
        static void Level(StringBuilder sb, string settings, string mapJson, string lv, bool full)
        {
            var w = Setup(settings, mapJson, out var s);
            if (s == null) { sb.AppendLine(OzV11Kit.Verdict(false, $"{lv}: no free pad for the ship")); return; }
            var wOff = Setup(settings, mapJson, out var sOff);                                                   // E: the same world, DV190 off
            sb.AppendLine($"{lv}: the ship #{s.Id} (red) at {P(s.CenterX, s.CenterY)}");
            var targets = new List<(string what, int x, int y, int refId)>();
            var blds = w.Objects.Where(o => o.Kind == ZKind.Building && !o.Destroyed && !o.CanRepairUnit(Team.Red)).OrderBy(o => (long)(o.CenterX - s.CenterX) * (o.CenterX - s.CenterX) + (long)(o.CenterY - s.CenterY) * (o.CenterY - s.CenterY)).ThenBy(o => o.Id).ToList();
            foreach (var b in blds.Take(full ? blds.Count : 3)) targets.Add(($"building {b.Type} #{b.Id} ({b.Owner})", b.CenterX, b.CenterY, b.Id));
            var flags = w.Objects.Where(o => o.Kind == ZKind.MapItem && o.Type == "flag" && !o.Destroyed).OrderBy(o => (long)(o.CenterX - s.CenterX) * (o.CenterX - s.CenterX) + (long)(o.CenterY - s.CenterY) * (o.CenterY - s.CenterY)).ThenBy(o => o.Id).ToList();
            foreach (var f in flags.Take(full ? flags.Count : 1)) targets.Add(($"flag #{f.Id}", f.CenterX, f.CenterY, f.Id));
            foreach (var why in new[] { "rocks", "blocked", "water", "trees", "item" })
                if (Never(w, s, why, s.CenterX, s.CenterY, full ? 60 : 16, out int x, out int y)) targets.Add(($"{why} (the nearest such tile)", x, y, -1));
            targets.Add(("the map's edge band (tile 1)", 1 * 16 + 8, s.CenterY, -1));
            targets.Add(("the map's corner (tile 0, 0)", 8, 8, -1));
            int kept = 0, valid = 0, nearest = 0, never = 0, landed = 0, flown = 0, offSame = 0, none = 0, unitsHover = 0; double worst = 0; var bad = new List<string>();
            foreach (var (what, x, y, refId) in targets)
            {
                if (!w.ShuttleNeverLands(x, y, s, out string why0)) { sb.AppendLine($"   ({what} at {P(x, y)}: the ship can land there: not a DV190 place)"); continue; }
                never++;
                var order = new[] { Move(x, y, refId) };
                int r0 = ZWorld.ShuttleNearestRedirects, n0 = s.ShuttleNoLandOrders;
                var pend = Click(w, Team.Red, s, order, true, out var refused);
                bool found = Brute(w, s, x, y, out int bx, out int by, out int bd);
                if (!found)
                {   // nothing near: refused as before
                    none++;
                    bool asBefore = refused.Contains(s) && !pend.ContainsKey(s) && s.ShuttleNoLandOrders == n0 + 1 && ZWorld.ShuttleNearestRedirects == r0;
                    sb.AppendLine($"   {what} at {P(x, y)} ({why0}): no landing spot within {ZWorld.ShuttleNearestTiles} tiles: refused as before {asBefore}");
                    if (!asBefore) bad.Add(what + ": not refused as before");
                    continue;
                }
                bool k = pend.TryGetValue(s, out var l) && l.Count == 1 && !refused.Contains(s) && ZWorld.ShuttleNearestRedirects == r0 + 1 && s.ShuttleNoLandOrders == n0;
                if (k) kept++;
                var got = k ? l[0] : default;
                bool v = k && got.mode == order[0].mode && got.refId == -1 && (got.x & 15) == 8 && (got.y & 15) == 8 && !w.ShuttleNeverLands(got.x, got.y, s, out _) && !Station(w, s, got.x, got.y)
                         && w.ShuttleClamp(got.x, got.y) == (got.x, got.y) && got.attackTo == order[0].attackTo && got.playerGiven == order[0].playerGiven;
                if (v) valid++;
                bool nr = k && got.x == bx && got.y == by;
                if (nr) nearest++;
                double dist = Math.Sqrt((double)(got.x - x) * (got.x - x) + (double)(got.y - y) * (got.y - y));
                if (k) worst = Math.Max(worst, Math.Sqrt(bd) * 2.0);
                string line = $"   {what} at {P(x, y)} ({why0}) -> {(k ? P(got.x, got.y) : "REFUSED")}, {Math.Sqrt(bd) * 2.0:0.0} m from the clicked tile ({M(dist)} from the click); brute force {P(bx, by)}";
                // E: DV190 off: refused exactly as before
                if (sOff != null)
                {
                    ZWorld.ShuttleNearestLandingDeviation = false;
                    int ro = ZWorld.ShuttleNearestRedirects, no = ZWorld.ShuttleNearestNone, n1 = sOff.ShuttleNoLandOrders;
                    var po = Click(wOff, Team.Red, sOff, order, true, out var refOff);
                    bool same = refOff.Contains(sOff) && !po.ContainsKey(sOff) && sOff.ShuttleNoLandOrders == n1 + 1 && ZWorld.ShuttleNearestRedirects == ro && ZWorld.ShuttleNearestNone == no && sOff.ShuttleNearestRedirects == 0;
                    ZWorld.ShuttleNearestLandingDeviation = true;
                    if (same) offSame++; else bad.Add(what + ": DV190 off: not refused as before");
                }
                // the flight: the first building, the first flag and every place in full
                if (k && (full || flown < 2) && (what.StartsWith("building") || what.StartsWith("flag") || full))
                {
                    flown++;
                    int f0 = s.ShuttleFlights, h0 = s.ShuttleHovers;
                    bool sent = w.SendWaypoints(Team.Red, s, l);
                    string st = Settle(w, s);
                    bool ok = sent && st == "landed" && Math.Abs(s.CenterX - got.x) <= 8 && Math.Abs(s.CenterY - got.y) <= 8 && s.ShuttleHovers == h0;
                    bool unitsCame = sent && st == "hover" && s.ShuttleNoLand == "units";                                // a unit walked onto the pad on the way: the old hover rule (no tile, building or item)
                    if (ok) landed++; else if (unitsCame) unitsHover++; else bad.Add($"{what}: sent {sent}, {st} at {P(s.CenterX, s.CenterY)}, hovers +{s.ShuttleHovers - h0}");
                    line += $"; flown: {st}{(st == "hover" ? " (" + s.ShuttleNoLand + ", heading " + s.ShuttleHeadingDeg.ToString("0") + ")" : "")} at {P(s.CenterX, s.CenterY)} ({(s.ShuttleFlights > f0 ? "took off" : "already there")}, hovers +{s.ShuttleHovers - h0})";
                }
                sb.AppendLine(line);
                if (!v || !nr) bad.Add($"{what}: kept {k}, valid {v}, nearest {nr}");
            }
            int redirectable = never - none;
            sb.AppendLine(OzV11Kit.Verdict(never >= 4 && kept == redirectable && valid == redirectable && redirectable >= 3,
                $"B {lv}: {never} clicks on places where it can never land (buildings, flags, rocks / closed tiles / water when the map has them, the edge band): {kept} of {redirectable} with a spot near kept (not refused), {valid} sent to a tile middle where it can land (the same test), max {worst:0.0} m from the clicked tile"));
            sb.AppendLine(OzV11Kit.Verdict(nearest == redirectable, $"B {lv}: {nearest} of {redirectable} at the least distance a brute-force scan of the {ZWorld.ShuttleNearestTiles}-tile radius finds (ties: dy, then dx)"));
            sb.AppendLine(OzV11Kit.Verdict(flown >= 2 && landed + unitsHover == flown && landed * 10 >= flown * 9, $"B {lv}: {landed} of {flown} redirected orders flown: it lands on that spot (centre within 1 m), no hover"
                + (unitsHover > 0 ? $"; {unitsHover} hovered because units stood on the pad when it came (enemy ground; the old hover rule, never a tile, building or item)" : "")));
            sb.AppendLine(OzV11Kit.Verdict(offSame == redirectable, $"E {lv}: DV190 off: {offSame} of {redirectable} of those clicks refused exactly as before (taken out of the click, its NO-LAND count +1, no DV190 count)"));
            foreach (var b in bad.Take(12)) sb.AppendLine("   problem: " + b);
            // C: nothing within the radius
            var w2 = Setup(settings, mapJson, out var s2);
            int ox = -12 * 16 - 8, oy = s2.CenterY;
            int r2 = ZWorld.ShuttleNearestRedirects, nn = ZWorld.ShuttleNearestNone, n2 = s2.ShuttleNoLandOrders, f2 = s2.ShuttleFlights, cx2 = s2.CenterX, cy2 = s2.CenterY;
            var p2 = Click(w2, Team.Red, s2, new[] { Move(ox, oy) }, true, out var ref2);
            foreach (var kv in p2) w2.SendWaypoints(Team.Red, kv.Key, kv.Value);
            Run(w2, 8);
            bool cOk = ref2.Contains(s2) && !p2.ContainsKey(s2) && s2.ShuttleNoLandOrders == n2 + 1 && ZWorld.ShuttleNearestRedirects == r2 && ZWorld.ShuttleNearestNone == nn + 1
                       && s2.ShuttleFlights == f2 && s2.CenterX == cx2 && s2.CenterY == cy2;
            sb.AppendLine(OzV11Kit.Verdict(cOk, $"C {lv}: a click 12 tiles off the map ({P(ox, oy)}): nothing within {ZWorld.ShuttleNearestTiles} tiles: refused as before (taken out of the click, NO-LAND +1), 8 s later it stands where it stood ({P(s2.CenterX, s2.CenterY)}, flights +{s2.ShuttleFlights - f2})"
                + (none > 0 ? $"; on the map: {none} such place(s) above, refused as before" : "")));
        }

        // ------------------------------------------------------------------ D + F
        static void Others(StringBuilder sb, string settings, string mapJson)
        {
            // D: a click where it can land: DV190 on = off, field by field
            var wa = Setup(settings, mapJson, out var sa); var wb = Setup(settings, mapJson, out var sb2);
            if (sa == null || sb2 == null || !PadSpot(wa, sa, sa.CenterX, sa.CenterY, 12, 30, out int px, out int py)) { sb.AppendLine(OzV11Kit.Verdict(false, "D: no free pad for the check")); return; }
            var order = new[] { Move(px, py) };
            ZWorld.ShuttleNearestLandingDeviation = true; int ra = ZWorld.ShuttleNearestRedirects;
            var pa = Click(wa, Team.Red, sa, order, true, out var refA); bool noCount = ZWorld.ShuttleNearestRedirects == ra;
            ZWorld.ShuttleNearestLandingDeviation = false;
            var pb = Click(wb, Team.Red, sb2, order, true, out var refB);
            bool sameOrder = pa.Count == 1 && pb.Count == 1 && refA.Count == 0 && refB.Count == 0 && string.Join("|", pa[sa].Select(W)) == string.Join("|", pb[sb2].Select(W)) && W(pa[sa][0]) == W(order[0]);
            ZWorld.ShuttleNearestLandingDeviation = true; foreach (var kv in pa) wa.SendWaypoints(Team.Red, kv.Key, kv.Value); Run(wa, 25);
            ZWorld.ShuttleNearestLandingDeviation = false; foreach (var kv in pb) wb.SendWaypoints(Team.Red, kv.Key, kv.Value); Run(wb, 25);
            ZWorld.ShuttleNearestLandingDeviation = true;
            ulong ha = Print(wa), hb = Print(wb);
            sb.AppendLine(OzV11Kit.Verdict(sameOrder && noCount && ha == hb && sa.ShuttleState == ZShuttleState.Landed,
                $"D a click where it can land ({P(px, py)}): the order with DV190 on = off, field by field ({W(pa[sa][0])}); 25 s later the same world, every plain field of every object + the next random number ({ha:x16} / {hb:x16}); it {sa.ShuttleState} at {P(sa.CenterX, sa.CenterY)}"));
            // F: the computer, ANYWHERE, STOP, repair, a route
            var w = Setup(settings, mapJson, out var s);
            var bld = w.Objects.Where(o => o.Kind == ZKind.Building && !o.Destroyed && !o.CanRepairUnit(Team.Red)).OrderBy(o => (long)(o.CenterX - s.CenterX) * (o.CenterX - s.CenterX) + (long)(o.CenterY - s.CenterY) * (o.CenterY - s.CenterY)).First();
            var blue = w.CreatePlayableShuttle(s.CenterX + 16 * 20, s.CenterY, Team.Blue, 0f);
            int r0 = ZWorld.ShuttleNearestRedirects;
            var oB = new[] { Move(bld.CenterX, bld.CenterY, bld.Id) };
            var pc = Click(w, Team.Blue, blue, oB, true, out var refC);
            bool cpu = !w.IsHumanTeam(Team.Blue) && refC.Count == 0 && pc.ContainsKey(blue) && W(pc[blue][0]) == W(oB[0]) && ZWorld.ShuttleNearestRedirects == r0;
            var pAny = Click(w, Team.Red, s, oB, false, out var refAny);
            bool any = refAny.Count == 0 && W(pAny[s][0]) == W(oB[0]) && ZWorld.ShuttleNearestRedirects == r0;
            var pStop = Click(w, Team.Red, s, new ZWaypoint[0], true, out var refStop);
            bool stop = refStop.Count == 0 && pStop[s].Count == 0 && ZWorld.ShuttleNearestRedirects == r0;
            var rep = new ZWaypoint { mode = WpMode.UnitRepair, refId = bld.Id, x = bld.CenterX, y = bld.CenterY };
            var pRep = Click(w, Team.Red, s, new[] { rep, Move(bld.CenterX, bld.CenterY, bld.Id) }, true, out var refRep);
            bool repair = refRep.Count == 0 && pRep[s].Count == 2 && W(pRep[s][0]) == W(rep) && ZWorld.ShuttleNearestRedirects == r0;
            sb.AppendLine(OzV11Kit.Verdict(cpu && any && stop && repair, $"F a computer team's click (its shuttle onto a building) left alone {cpu}; ANYWHERE (no filter) untouched {any}; STOP untouched {stop}; a repair order first untouched {repair}"));
            PadSpot(w, s, s.CenterX, s.CenterY, 6, 30, out int mx, out int my);
            var route = new[] { Move(mx, my), Move(bld.CenterX, bld.CenterY, bld.Id) };
            w.ShuttleNearestLanding(bld.CenterX, bld.CenterY, s, out int nx, out int ny);
            var pr = Click(w, Team.Red, s, route, true, out var refR);
            bool rOk = refR.Count == 0 && pr[s].Count == 2 && W(pr[s][0]) == W(route[0]) && pr[s][1].x == nx && pr[s][1].y == ny && pr[s][1].refId == -1;
            w.ShuttleNearestLanding(bld.CenterX, bld.CenterY, s, out int nx2, out int ny2);
            sb.AppendLine(OzV11Kit.Verdict(rOk && nx == nx2 && ny == ny2, $"F a route (DV125, Shift): middle point {P(mx, my)} as given, its last point on building #{bld.Id} -> {P(pr[s][1].x, pr[s][1].y)} (the nearest spot); the same search twice: the same spot"));
        }

        // ------------------------------------------------------------------ F (full): the computer's shuttle rules, DV190 off / on / on again
        static void Cpu(StringBuilder sb, string settings, string mapJson)
        {
            var names = new[] { "CpuShuttleDeviation", "CpuRepairDeviation", "L17ShuttlesDeviation", "SideShuttlesDeviation" };
            var keep = names.ToDictionary(n => n, n => { var f = typeof(ZWorld).GetField(n, BindingFlags.Public | BindingFlags.Static); return f != null && (bool)f.GetValue(null); });
            try
            {
                foreach (var n in names) OzV11Kit.TrySet(typeof(ZWorld), n, true);
                string Play(bool on, out int orders)
                {
                    ZWorld.ShuttleNearestLandingDeviation = on;
                    var w = ZWorld.Create(settings, mapJson, 3, true);
                    w.Bots.Add(new ZBot(w, Team.Blue, 104)); w.Bots.Add(new ZBot(w, Team.Red, 3));
                    ulong h = 1469598103934665603UL; int ticks = (int)Math.Round(CpuSec / Tick);
                    for (int i = 0; i < ticks && !w.GameOver; i++) { w.Tick(Tick); w.Events.Clear(); if (i % 100 == 99) h = w.RulesFingerprint(h); }
                    orders = w.Objects.Where(o => o.IsShuttle && o.Owner != Team.Neutral).Sum(o => o.ShuttleFlights);
                    return h.ToString("x16") + " " + w.Time.ToString("0.00") + " s, " + w.Objects.Count + " objects, ships " + w.Objects.Count(o => o.IsShuttle) + ", rand " + w.Rng.Next(1 << 30);
                }
                string a = Play(false, out int fa), b = Play(true, out int fb), c = Play(true, out int fc);
                ZWorld.ShuttleNearestLandingDeviation = true;
                sb.AppendLine(OzV11Kit.Verdict(a == b && b == c, $"F level 17, computer against computer {CpuSec:0} s with its shuttle rules on (DV134 / DV137 / DV138 / DV144; shuttle flights {fa} / {fb} / {fc}): DV190 off / on / on again: {a} | {b} | {c}"));
            }
            finally { foreach (var kv in keep) OzV11Kit.TrySet(typeof(ZWorld), kv.Key, kv.Value); ZWorld.ShuttleNearestLandingDeviation = true; }
        }

        // ------------------------------------------------------------------ H: the quick sure "never" agrees with the full test
        static void Sure(StringBuilder sb, string settings, string mapJson, string lv, int half, int step)
        {
            var w = Setup(settings, mapJson, out var s);
            int tx0 = s.CenterX >> 4, ty0 = s.CenterY >> 4, n = 0, sure = 0, never = 0, wrong = 0; string first = "";
            for (int ty = Math.Max(0, ty0 - half); ty < Math.Min(w.Map.height, ty0 + half); ty += step)
                for (int tx = Math.Max(0, tx0 - half); tx < Math.Min(w.Map.width, tx0 + half); tx += step)
                {
                    int x = tx * 16 + 8, y = ty * 16 + 8; n++;
                    bool nv = w.ShuttleNeverLands(x, y, s, out _); if (nv) never++;
                    if (!w.ShuttleNearestSureNever(x, y, s)) continue;
                    sure++;
                    if (!nv) { wrong++; if (first == "") first = " (first: " + P(x, y) + ")"; }
                }
            sb.AppendLine(OzV11Kit.Verdict(wrong == 0 && sure > 0, $"H {lv}: the quick sure-never test on {n} tile middles: {sure} sure (of {never} where the full test says never), {wrong} where the full test says it can land{first}"));
        }

        // ------------------------------------------------------------------ G (full): level 17's ground before / now
        static void Share(StringBuilder sb, string settings, string mapJson)
        {
            var w = Setup(settings, mapJson, out var s);
            int e = ZWorld.ShuttleEdgeTiles, all = 0, nev = 0, none = 0; int nx = 0, ny = 0; bool inMap = false;
            for (int ty = 0; ty < w.Map.height; ty += 4)
                for (int tx = 0; tx < w.Map.width; tx += 4)
                {
                    int x = tx * 16 + 8, y = ty * 16 + 8; all++;
                    if (!w.ShuttleNeverLands(x, y, s, out _)) continue;
                    nev++;
                    if (!w.ShuttleNearestLanding(x, y, s, out _, out _)) { none++; if (!inMap && tx >= e && ty >= e && tx < w.Map.width - e && ty < w.Map.height - e) { inMap = true; nx = x; ny = y; } }
                }
            string inner = "";
            if (inMap)
            {
                int n0 = s.ShuttleNoLandOrders;
                var p = Click(w, Team.Red, s, new[] { Move(nx, ny) }, true, out var refused);
                inner = $"; an in-map place with none near, {P(nx, ny)}: refused as before {refused.Contains(s) && !p.ContainsKey(s) && s.ShuttleNoLandOrders == n0 + 1}";
            }
            sb.AppendLine(OzV11Kit.Verdict(none * 4 <= nev && (!inMap || inner.EndsWith("True")), $"G level 17, every 4th tile ({all} clicks): refused before {nev} ({100.0 * nev / all:0.0} %), now {none} ({100.0 * none / all:0.0} %: nothing within {ZWorld.ShuttleNearestTiles} tiles){inner}"));
        }
    }
}
