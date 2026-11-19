using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// (a building, a flag, rocks, a closed tile, water, the map's edge band, or ground too near one of them for its 6 x 6 m pad), and there the
    /// click only gave the red NO cursor and the refusal blip.
    ///  * The rule (switch <see cref="ShuttleNearestLandingDeviation"/>: View/ZGame.cs on at every start, off in 'Zod exact'; headless default
    ///    off): a HUMAN team's move order for its shuttle (one click, handed to <see cref="ShuttleOrdersFilter"/> by the view's PlayerInput, the
    ///    option SHUTTLE ORDERS = LANDABLE SPOTS ONLY) whose landing point is a place where it can never land (<see cref="ShuttleNeverLands"/>,
    ///    unchanged) goes to the NEAREST tile middle where it can (the same test) within <see cref="ShuttleNearestTiles"/> tiles of the clicked
    ///    tile: whole tile offsets, nearest by dx*dx + dy*dy, ties by a fixed order (dy, then dx, both from low to high); the map's edge band
    ///    and an own repair station are no such place. A place where no unit stands on its pad now (<see cref="ShuttleNearestClear"/>: no
    ///    ground unit but the ship itself within the ship's outline radius + the pad's clearance + the unit's own radius) comes first; only
    ///    when every such place within the radius has units on it, the nearest of those (the units walk away, as at any landing spot). That
    ///    point replaces the order's landing point (the first move, or a route's last one, DV125); the rest of the order is as given. Nothing
    ///    within the radius: refused as before (taken out of the click).
    ///  * Unchanged: a click on a place where it can land (the order exactly as before); STOP, the repair order, ANYWHERE (no filter); the
    ///    computer's orders (they never go through the filter: ZBot sends them with SendWaypoints; DV137 / DV138 untouched); the flight, the
    ///    landing and the hover rules. Off: <see cref="ShuttleNearestRedirect"/> returns at its first line (not one field changes).
    /// Deterministic: positions and a fixed list only, no random draw, no frame time.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV190 switch (off = the click filter refuses as before). The game sets it at every start (off in 'Zod exact'); headless default off.</summary>
        public static bool ShuttleNearestLandingDeviation;
        /// <summary>DV190: the search radius in tiles (8 tiles = 16 m) round the clicked tile.</summary>
        public const int ShuttleNearestTiles = 8;
        /// <summary>DV190 check counters (every ship): clicks sent to the nearest landing spot instead, clicks with none within the radius
        /// (refused), and the last one: the clicked point, the point it went to, why the clicked place was refused.</summary>
        public static int ShuttleNearestRedirects, ShuttleNearestNone;
        public static int ShuttleNearestLastFromX, ShuttleNearestLastFromY, ShuttleNearestLastToX, ShuttleNearestLastToY;
        public static string ShuttleNearestLastWhy = "";

        static int[] shuttleNearestOffsets;

        /// <summary>DV190: the tile offsets tried, as pairs (dx, dy): every one with 0 &lt; dx*dx + dy*dy &lt;= ShuttleNearestTiles^2, nearest
        /// first, ties by dy then dx (from low to high).</summary>
        public static int[] ShuttleNearestOffsets
        {
            get
            {
                if (shuttleNearestOffsets != null) return shuttleNearestOffsets;
                int r = ShuttleNearestTiles; var l = new List<(int d, int dy, int dx)>();
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int d = dx * dx + dy * dy;
                        if (d > 0 && d <= r * r) l.Add((d, dy, dx));
                    }
                l.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : a.dy != b.dy ? a.dy.CompareTo(b.dy) : a.dx.CompareTo(b.dx));
                var o = new int[l.Count * 2];
                for (int i = 0; i < l.Count; i++) { o[2 * i] = l[i].dx; o[2 * i + 1] = l[i].dy; }
                return shuttleNearestOffsets = o;
            }
        }

        /// <summary>DV190: the nearest tile middle (nx, ny) round the clicked point (x, y) (px) where shuttle s can land (ShuttleNeverLands false),
        /// within ShuttleNearestTiles; false = none.</summary>
        public bool ShuttleNearestLanding(int x, int y, ZObject s, out int nx, out int ny)
        {
            int k = 0, held = -1;
            return ShuttleNearestStep(x, y, s, true, ref k, ref held, int.MaxValue, out nx, out ny) == 1;
        }

        /// <summary>DV190, the search in parts (the view's cursor spreads it over frames): tries at most `count` offsets from index k on (k moves
        /// on). clearNow true (the click): a place with no unit on its pad now ends it; the first place where it can land but units stand is
        /// kept in `held` (an offset index, -1 = none) and taken when the list ends without a clear one. clearNow false (the cursor): the first
        /// place where it can land at all ends it. 1 = found (nx, ny), 2 = none within the radius, 0 = not done yet (ask again with k, held).</summary>
        public int ShuttleNearestStep(int x, int y, ZObject s, bool clearNow, ref int k, ref int held, int count, out int nx, out int ny)
        {
            nx = ny = 0;
            if (Map == null || s == null || !s.IsShuttle) return 2;
            var off = ShuttleNearestOffsets; int n = off.Length / 2, tx0 = x >> 4, ty0 = y >> 4;
            while (k < n && count > 0)
            {
                int i = k++; count--;
                int tx = tx0 + off[2 * i], ty = ty0 + off[2 * i + 1];
                if (tx < 0 || ty < 0 || tx >= Map.width || ty >= Map.height) continue;
                int cx = tx * 16 + 8, cy = ty * 16 + 8;
                var (qx, qy) = ShuttleClamp(cx, cy);
                if (qx != cx || qy != cy) continue;                                                           // the edge band: its middle may never stand there
                if (ShuttleStationAt(cx, cy, s) != null) continue;                                             // an own repair station: no landing by surprise (its own order)
                if (s.ShuttleRepairTarget < 0 && ShuttleNearestSureNever(cx, cy, s)) continue;                 // the quick sure "never" (the full test would refuse it too)
                if (ShuttleNeverLands(cx, cy, s, out _)) continue;
                if (clearNow && !ShuttleNearestClear(cx, cy, s)) { if (held < 0) held = i; continue; }          // units on its pad now: the first such place kept
                nx = cx; ny = cy; return 1;
            }
            if (k < n) return 0;
            if (held < 0) return 2;
            nx = (tx0 + off[2 * held]) * 16 + 8; ny = (ty0 + off[2 * held + 1]) * 16 + 8;
            return 1;
        }

        /// <summary>DV190, a quick sure "never" before the full test (ShuttleNeverLands), the same answer, only faster: a sample point of the landing
        /// rule's own 4 px grid round (cx, cy) nearer than the outline's inscribed circle + the pad's clearance (ShuttlePlanInM + ShuttleSpotClearM:
        /// inside the outline + ShuttleSpotClearM at EVERY heading, so in the ship's part of the rule) on a rock or closed tile or off the map, or a
        /// sample point of a building's or map item's box there (with DV128 a flag by its pole: the pole nearer than that + ShuttleFlagPoleM), makes the rule refuse every heading
        /// (ZWorld.ShuttleSpotPart / ShuttleFairSpotPart, the ship's part). Not on a repair trip (its deck has its own rule: the caller).</summary>
        public bool ShuttleNearestSureNever(int cx, int cy, ZObject s)
        {
            double reachM = ZObject.ShuttlePlanRadiusM + ShuttleSpotClearM + 0.5, inM = ShuttlePlanInM + ShuttleSpotClearM, inR2 = inM * inM, inPx = inM * 8.0;
            int bx0 = (int)Math.Floor(cx - reachM * 8), by0 = (int)Math.Floor(cy - reachM * 8), bx1 = (int)Math.Ceiling(cx + reachM * 8), by1 = (int)Math.Ceiling(cy + reachM * 8);
            for (int py = by0; py <= by1; py += 4)
                for (int px = bx0; px <= bx1; px += 4)
                {
                    double dx = (px - cx) / 8.0, dy = (py - cy) / 8.0;
                    if (dx * dx + dy * dy >= inR2) continue;
                    int tx = px >> 4, ty = py >> 4;
                    if (px < 0 || py < 0 || tx >= Map.width || ty >= Map.height) return true;
                    var c = Map.ClassAt(tx, ty);
                    if (c == ZClass.Water || c == ZClass.DeepWater) continue;                                       // water: only under the gear (DV128): the full test
                    if (c == ZClass.Impassable || !Path.TilePassable(tx, ty, false)) return true;
                }
            foreach (var o in Objects)
            {
                if (o == s || o.Destroyed || o.KillAt >= 0 || (o.Kind != ZKind.Building && o.Kind != ZKind.MapItem)) continue;
                if (o.LocX > cx + inPx + 4 || o.LocX + o.WidthPix < cx - inPx - 4 || o.LocY > cy + inPx + 4 || o.LocY + o.HeightPix < cy - inPx - 4) continue;
                if (o.Type == "flag" && ShuttleFairSpotOn)
                {   // DV128: a flag by its pole (ShuttleFairSpotPart)
                    double fx = (o.LocX + o.WidthPix * 0.5 - cx) / 8.0, fy = (o.LocY + o.HeightPix * 0.5 - cy) / 8.0, fr = inM + ShuttleFlagPoleM;
                    if (fx * fx + fy * fy < fr * fr) return true;
                    continue;
                }
                if (o.Contains(cx, cy)) return true;
                for (int yy = o.LocY; yy <= o.LocY + o.HeightPix; yy += 4)
                    for (int xx = o.LocX; xx <= o.LocX + o.WidthPix; xx += 4)
                    {
                        double qx = (Math.Min(xx, o.LocX + o.WidthPix - 0.01) - cx) / 8.0, qy = (Math.Min(yy, o.LocY + o.HeightPix - 0.01) - cy) / 8.0;
                        if (qx * qx + qy * qy < inR2) return true;
                    }
            }
            return false;
        }

        /// <summary>DV190: no ground unit (but ship s itself; a ship up in the air does not count) stands on the pad round (cx, cy) now: its middle
        /// farther than the ship's outline radius + ShuttleSpotClearM + its own pathing radius (whole px, rounded up) from the pad's middle.</summary>
        public bool ShuttleNearestClear(int cx, int cy, ZObject s)
        {
            int reach = (int)Math.Ceiling((ZObject.ShuttlePlanRadiusM + ShuttleSpotClearM) * 8.0);
            foreach (var o in Objects)
            {
                if (o == s || !o.IsUnit || o.Destroyed || o.KillAt >= 0 || o.ShuttleAirborne) continue;
                int r = reach + ZPathing.Radius(o), dx = o.CenterX - cx, dy = o.CenterY - cy;
                if (dx * dx + dy * dy < r * r) return false;
            }
            return true;
        }

        /// <summary>ShuttleOrdersFilter hook (the order's landing move `land` is a place where ship o can never land, `why`): with the switch on
        /// the landing move in `list` goes to the nearest landing spot (true: the order stays in the click); false = refused as before.</summary>
        bool ShuttleNearestRedirect(ZObject o, List<ZWaypoint> list, ZWaypoint land, string why)
        {
            if (!ShuttleNearestLandingDeviation) return false;
            int i = ShuttleLandingIndex(list);
            if (i < 0) return false;
            if (!ShuttleNearestLanding(land.x, land.y, o, out int nx, out int ny)) { ShuttleNearestNone++; return false; }
            var wp = list[i]; wp.x = nx; wp.y = ny; wp.refId = -1; list[i] = wp;                                // a plain move to that ground
            o.ShuttleNearestRedirects++; ShuttleNearestRedirects++;
            ShuttleNearestLastFromX = land.x; ShuttleNearestLastFromY = land.y; ShuttleNearestLastToX = nx; ShuttleNearestLastToY = ny; ShuttleNearestLastWhy = why ?? "";
            return true;
        }

        /// <summary>The index in `list` of the move ShuttleLandingMove returns (a route's last move, else the first move); -1 = none.</summary>
        int ShuttleLandingIndex(List<ZWaypoint> list)
        {
            if (list == null) return -1;
            bool route = ShuttleRoutePoints(list, out _, out _) != null;
            int found = -1;
            for (int i = 0; i < list.Count; i++)
            {
                var wp = list[i];
                if (wp.mode == WpMode.UnitRepair) { if (found < 0) return -1; continue; }
                if (wp.mode != WpMode.Move && wp.mode != WpMode.ForceMove) continue;
                found = i;
                if (!route) break;
            }
            return found;
        }

        /// <summary>DV190 off for a while (the parity report's Zod rows and probes); Dispose puts it back.</summary>
        public static IDisposable ShuttleNearestOff() => new ShuttleNearestOffHold();
        sealed class ShuttleNearestOffHold : IDisposable
        {
            readonly bool was = ShuttleNearestLandingDeviation;
            public ShuttleNearestOffHold() { ShuttleNearestLandingDeviation = false; }
            public void Dispose() { ShuttleNearestLandingDeviation = was; }
        }
    }

    public partial class ZObject
    {
        /// <summary>DV190: this shuttle's clicks sent to the nearest landing spot instead (check counter).</summary>
        public int ShuttleNearestRedirects;
    }
}
