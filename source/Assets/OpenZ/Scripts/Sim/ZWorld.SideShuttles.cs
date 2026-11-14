using System;
using System.Collections.Generic;
using System.Text;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ deviation DV144 (1.2 item 111, on top of DV118's playable shuttle, K256f's EMPTY shuttle, DV134's level 17 shuttles and
    /// DV140's PLAY AS BLUE): the battlefield screen's SHUTTLE box works for whichever side the player commands, level 17's car park has
    /// an empty shuttle on each side, and the HEAD START vehicles never stand on or beside a shuttle. Made at map load, deterministic
    /// (fixed places from this file's data, no random draw, no clock), the same on every machine.
    ///  * THE BLUE SIDE'S START SHUTTLE (the player commands blue, the box asks for a shuttle: level 16 on EASY / MEDIUM with the box on,
    ///    level 17 on EASY / MEDIUM with the box on): the player's crewed shuttle (its pilot, team blue) by the BLUE fort, on a spot of its
    ///    own per level (the map is not mirrored): level 16 <see cref="L16BlueFortShuttleSpot"/> (by the blue fort's south-east corner,
    ///    between the termite mounds and the road, on the flattest ground there, nose east, the ramp west toward the gate), level 17
    ///    <see cref="L17BlueFortShuttleSpot"/> (the quiet pocket west of the blue fort, between its west wall, the repair station, the huts
    ///    and the west cliffs, its ramp south-east toward the gate side). It is <see cref="ZWorld.PlayableShuttle"/>. The red fort's own
    ///    spots stay the red side's: with the player blue, level 16's pocket by the red fort stays empty (as with the box off: no ship, no
    ///    decoration; Sim/ZWorld.Shuttle.cs) and level 17's fort spot by the red fort is not used (Sim/ZWorld.L17Shuttles.cs). The computer
    ///    gets no start shuttle on either side (as before: the box is the player's).
    ///  * LEVEL 17, THE SECOND CAR-PARK SHUTTLE, every difficulty, whichever side is played: one more EMPTY shuttle (no pilot, the null team,
    ///    K256f's rules) in the free bay of the WEST car park's first row (the blue side), nose south like the row's vehicles
    ///    (<see cref="L17BlueCarParkShuttleSpot"/>): the mirror of DV134's ship in the east car park (the red side), the same room round it.
    ///  * THE HEAD START VEHICLES (ZWorld.AddHeadStart, both sides): a parking place is refused when its 2 x 2 tile box comes nearer than
    ///    <see cref="HeadStartShuttleClearM"/> to a shuttle's outline or nearer than <see cref="HeadStartLaneClearM"/> to its ramp lane
    ///    (<see cref="HeadStartNearShuttle"/>); every shuttle stands before the head start is placed, so the head start goes round them,
    ///    whatever options are on (the box, HEAD START, the difficulty, the parked start).
    ///  * Each spot is checked with the landing spot rule on its exact heading (ZWorld.ShuttlePadFreeAt) and kept off the level's drawn
    ///    props the rules know at map load (ZWorld.ParkingKeepOut: street lamps, bollards, boulders) by <see cref="StartPropClearM"/> (its
    ///    ramp lane by <see cref="HeadStartLaneClearM"/>); if the map ever changed under it, the nearest tile within
    ///    <see cref="SideSpotSearchTiles"/> tiles that takes the ship is used (counted, <see cref="SideShuttlesMoved"/>), else none is made.
    ///  * The places were chosen by numbers (the kit's measure of every tile and heading round each fort: the landing rule, every vehicle,
    ///    gun, building, flag, item, drawn prop, plant crown, exit lane and gate face, the walk from the fort's gate to the ramp foot, the
    ///    ground under the four leg pads: the drawn ship follows the ground on its pads only up to an 8 degree slope).
    /// Switch: <see cref="SideShuttlesDeviation"/> (the game: on, off in 'Zod exact'; headless default off, so every older check keeps its
    /// numbers). Needs DV118 on (ShuttlePlayableDeviation) and the shuttle rule on (ShuttleOn); the car-park ship also DV134's switch.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV144 switch (off = as before: no start shuttle for a blue player, one empty shuttle in level 17's car park, the head
        /// start parked without looking at shuttles).</summary>
        public static bool SideShuttlesDeviation = false;
        /// <summary>The blue side's start shuttle on level 16: tile x, tile y (its middle on that tile's middle), heading (deg; 0 = east, 90 = south).</summary>
        public static readonly int[] L16BlueFortShuttleSpot = { 64, 20, 0 };
        /// <summary>The blue side's start shuttle on level 17 (west of the blue fort, the ramp toward the gate side).</summary>
        public static readonly int[] L17BlueFortShuttleSpot = { 13, 27, -135 };
        /// <summary>Level 17's second empty car-park shuttle (the west car park's first row, nose south).</summary>
        public static readonly int[] L17BlueCarParkShuttleSpot = { 27, 92, 90 };
        /// <summary>How far (tiles) a spot is looked for when its own place does not take the ship (the map changed).</summary>
        public const int SideSpotSearchTiles = 3;
        /// <summary>A head start parking box keeps this far (m) from every shuttle's outline, and this far from its ramp lane.</summary>
        public const double HeadStartShuttleClearM = 4.0, HeadStartLaneClearM = 0.5;
        /// <summary>A start shuttle keeps this far (m) from the level's drawn props (ParkingKeepOut discs) with its outline.</summary>
        public const double StartPropClearM = 1.5;

        /// <summary>The blue side's start shuttle made at map load (null: the player is red, another level, the box did not ask, the switch off).</summary>
        public ZObject BlueStartShuttle { get; private set; }
        /// <summary>Level 17's second empty car-park shuttle (the blue side's bay; null: another level or a switch off).</summary>
        public ZObject L17BlueCarParkShuttle { get; private set; }
        /// <summary>Shuttles this rule made at map load, and those that could not stand on their own place (0 on the maps as they are).</summary>
        public int SideShuttlesMade { get; private set; }
        public int SideShuttlesMoved { get; private set; }
        /// <summary>Why a spot made nothing or moved ("" = as wanted).</summary>
        public string SideShuttleWhy { get; private set; } = "";
        /// <summary>Head start parking places refused because a shuttle stands there or beside it (the checks).</summary>
        public int HeadStartShuttleRefusals { get; private set; }

        /// <summary>The blue side's start shuttle spot of this world's map (null: a level without one).</summary>
        public static int[] BlueFortSpotOf(ZWorld w) =>
            w == null || w.Map == null ? null : w.IsL16Map ? L16BlueFortShuttleSpot : w.CarParkMap ? L17BlueFortShuttleSpot : null;

        bool IsL16Map => Map != null && string.Equals(Map.file, L16MapFile, StringComparison.OrdinalIgnoreCase);

        /// <summary>ZWorld.L16ShuttleReplaced hook: the player commands blue, so level 16's pocket by the red fort stays empty (no ship, no
        /// decoration, as with the box off); the player's ship stands by the blue fort (<see cref="AddSideShuttles"/>).</summary>
        bool L16PocketForBlue() => SideShuttlesDeviation && ShuttleTeam == Team.Blue;

        /// <summary>ZWorld.Create hook, right after DV134's level 17 shuttles: level 17's second empty car-park shuttle, then the blue side's
        /// start shuttle when the player commands blue and the box asks.</summary>
        void AddSideShuttles()
        {
            BlueStartShuttle = L17BlueCarParkShuttle = null; SideShuttlesMade = SideShuttlesMoved = 0; SideShuttleWhy = ""; HeadStartShuttleRefusals = 0;
            if (!SideShuttlesDeviation || !ShuttlePlayableDeviation || !ShuttleOn || Map == null) return;
            if (CarParkMap && L17ShuttlesDeviation)
            {
                var keep = PlayableShuttle;
                L17BlueCarParkShuttle = SidePlace(L17BlueCarParkShuttleSpot, Team.Neutral, "blue car park");
                if (L17BlueCarParkShuttle != null && PlayableShuttle == L17BlueCarParkShuttle) PlayableShuttle = keep;   // an empty one is never the level's own playable ship
            }
            if (ShuttleTeam != Team.Blue) return;
            var spot = BlueFortSpotOf(this);
            if (spot == null) return;
            bool wanted = IsL16Map ? L16ShuttleDeviation && L16ShuttleOptionOff : L17ShuttlesDeviation && L17StartShuttle;   // level 16: the box on (ShuttleOn) and the pocket emptied; level 17: the box's answer
            if (!wanted) { SideShuttleWhy += "blue fort: the box did not ask for one; "; return; }
            BlueStartShuttle = SidePlace(spot, Team.Blue, "blue fort");
            if (BlueStartShuttle != null) PlayableShuttle = BlueStartShuttle;
        }

        /// <summary>One shuttle on its spot (tile x, y, heading): the landing spot rule on that exact heading and the drawn props kept clear;
        /// else the nearest tile ring by ring within SideSpotSearchTiles (the heading turned to a free side, ZWorld.ShuttleLandSpot), else
        /// none (why kept).</summary>
        ZObject SidePlace(int[] spot, Team team, string what)
        {
            int tx0 = spot[0], ty0 = spot[1]; double hd = spot[2];
            int cx = tx0 * 16 + 8, cy = ty0 * 16 + 8;
            if (!StartShuttleSpotFree(cx, cy, hd, out string why))
            {
                bool found = false;
                for (int r = 1; r <= SideSpotSearchTiles && !found; r++)
                    for (int dy = -r; dy <= r && !found; dy++)
                        for (int dx = -r; dx <= r && !found; dx++)
                        {
                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                            int tx = tx0 + dx, ty = ty0 + dy;
                            if (tx < ShuttleEdgeTiles || ty < ShuttleEdgeTiles || tx >= Map.width - ShuttleEdgeTiles || ty >= Map.height - ShuttleEdgeTiles) continue;
                            int x = tx * 16 + 8, y = ty * 16 + 8;
                            if (!ShuttleLandSpot(x, y, null, spot[2], out double h2, out _) || !StartShuttleSpotFree(x, y, h2, out _)) continue;
                            cx = x; cy = y; hd = h2; found = true;
                        }
                if (!found) { SideShuttleWhy += what + ": " + why + "; "; return null; }
                SideShuttlesMoved++;
                SideShuttleWhy += what + ": its spot took no ship (" + why + "), the nearest free tile is used; ";
            }
            var s = CreatePlayableShuttle(cx, cy, team, (float)ZObject.ShuttleNormDeg(hd));
            SideShuttlesMade++;
            return s;
        }

        /// <summary>A start shuttle may stand with its middle at (cx, cy) px on heading hd: the landing spot rule on that heading, and the
        /// level's drawn props the rules know (ParkingKeepOut, this map's) at least StartPropClearM from its outline and HeadStartLaneClearM
        /// from its ramp lane. why = the first thing in the way.</summary>
        public bool StartShuttleSpotFree(int cx, int cy, double hd, out string why)
        {
            if (!ShuttlePadFreeAt(cx, cy, null, hd, out why)) return false;
            if (ParkingKeepOut.Count == 0 || ParkingKeepOutMap != Map.file) return true;
            foreach (var k in ParkingKeepOut)
            {
                if (Math.Abs(k.x - cx) > 160 || Math.Abs(k.y - cy) > 160) continue;
                if (ZObject.ShuttlePlanGap(cx, cy, hd, k.x, k.y) - k.r / 8.0 < StartPropClearM || ShuttleLaneGapM(cx, cy, hd, k.x, k.y) - k.r / 8.0 < HeadStartLaneClearM)
                { why = "prop"; return false; }
            }
            return true;
        }

        /// <summary>Metres from the point (x, y) px to the ramp lane of a ship standing with its middle at (cx, cy) on heading hdDeg (the
        /// lane of the landing rule: ShuttleLaneHalfW either side, from the hull's back to ShuttleBackM + ShuttleLaneBehindM behind the middle).</summary>
        public static double ShuttleLaneGapM(double cx, double cy, double hdDeg, double x, double y)
        {
            double hd = hdDeg * Math.PI / 180.0, nx = Math.Cos(hd), ny = Math.Sin(hd);
            double dx = (x - cx) / 8.0, dy = (y - cy) / 8.0;
            double rz = dx * nx + dy * ny, rx = -dx * ny + dy * nx;
            double z0 = -(ShuttleBackM + ShuttleLaneBehindM), z1 = -ZObject.ShuttleHullBackM;
            double qx = Math.Max(0.0, Math.Abs(rx) - ShuttleLaneHalfW), qz = Math.Max(0.0, Math.Max(z0 - rz, rz - z1));
            return Math.Sqrt(qx * qx + qz * qz);
        }

        static double ShipMidX(ZObject s) => s.LocX + s.XOver + s.WidthPix * 0.5;
        static double ShipMidY(ZObject s) => s.LocY + s.YOver + s.HeightPix * 0.5;

        /// <summary>Metres from a box (px, half-open) to a ship's outline and to its ramp lane (negative = they overlap), sampled every 2 px.</summary>
        public static void ShuttleBoxGaps(ZObject s, double x0, double y0, double x1, double y1, out double outline, out double lane)
        {
            outline = lane = double.MaxValue;
            double mx = ShipMidX(s), my = ShipMidY(s), hd = s.ShuttleHeadingDeg;
            for (double y = y0; y <= y1; y += 2)
                for (double x = x0; x <= x1; x += 2)
                {
                    double px = Math.Min(x, x1 - 0.01), py = Math.Min(y, y1 - 0.01);
                    outline = Math.Min(outline, ZObject.ShuttlePlanGap(mx, my, hd, px, py));
                    lane = Math.Min(lane, ShuttleLaneGapM(mx, my, hd, px, py));
                }
        }

        /// <summary>ZWorld.AddHeadStart hook (its parking test of a 2 x 2 tile box at tile tx, ty): true = a shuttle stands on or beside it
        /// (the box nearer than HeadStartShuttleClearM to its outline or HeadStartLaneClearM to its ramp lane). Every standing shuttle of the
        /// map, any team. The switch off: false.</summary>
        public bool HeadStartNearShuttle(int tx, int ty)
        {
            if (!SideShuttlesDeviation || ShuttlesMadeCount == 0) return false;
            double x0 = tx * 16, y0 = ty * 16, x1 = x0 + 32, y1 = y0 + 32;
            foreach (var s in Objects)
            {
                if (!s.IsShuttle || s.Destroyed || s.KillAt >= 0 || s.ShuttleAirborne) continue;
                if (Math.Abs(s.CenterX - (x0 + 16)) > 160 || Math.Abs(s.CenterY - (y0 + 16)) > 160) continue;
                ShuttleBoxGaps(s, x0, y0, x1, y1, out double g, out double lg);
                if (g < HeadStartShuttleClearM || lg < HeadStartLaneClearM) { HeadStartShuttleRefusals++; return true; }
            }
            return false;
        }

        /// <summary>The checks (counted and Play): the room round a standing shuttle, "" = every rule kept, else what is too near. From its
        /// outline: every vehicle and gun 4 m, every robot 3 m, every building 2 m, every flag 4 m, every hut / item / detail / wreck / rock
        /// 1.5 m, every drawn prop the rules know (ParkingKeepOut) 1.5 m; its ramp lane clear of buildings, items, vehicles, guns and props by
        /// 0.5 m; no exit-lane point under its outline + 0.5 m; not within 2 tiles of a fort's gate face; the landing spot rule takes it where
        /// it stands (with the plant crowns the view handed in); no unit within 2 m of its outline. near = the nearest of each kind.</summary>
        public string ShuttleStartRoom(ZObject s, out string near)
        {
            near = "";
            if (s == null || !s.IsShuttle) return " not a shuttle;";
            var bad = new StringBuilder(); double nv = 99, nb = 99, nf = 99, ni = 99, np = 99, nl = 99; string wv = "", wb = "", wp = "", wl = "";
            double mx = ShipMidX(s), my = ShipMidY(s), hd = s.ShuttleHeadingDeg;
            foreach (var o in Objects)
            {
                if (o == s || o.Destroyed || o.KillAt >= 0) continue;
                if (Math.Abs(o.CenterX - s.CenterX) > 200 || Math.Abs(o.CenterY - s.CenterY) > 200) continue;
                ShuttleBoxGaps(s, o.LocX, o.LocY, o.LocX + o.WidthPix, o.LocY + o.HeightPix, out double g, out double lg);
                double lim; string what = o.Type + "#" + o.Id + "@" + (o.LocX >> 4) + "," + (o.LocY >> 4);
                if (o.Kind == ZKind.Vehicle || o.Kind == ZKind.Cannon) { lim = 4.0; if (g < nv) { nv = g; wv = what; } }
                else if (o.Kind == ZKind.Robot) lim = 3.0;
                else if (o.Kind == ZKind.Building) { lim = 2.0; if (g < nb) { nb = g; wb = what; } }
                else if (o.Type == "flag") { lim = 4.0; nf = Math.Min(nf, g); }
                else { lim = 1.5; ni = Math.Min(ni, g); }
                if (g < lim) bad.Append(" " + what + " " + g.ToString("0.0") + " m (< " + lim.ToString("0.0") + ");");
                if (o.Kind == ZKind.Robot) continue;
                if (lg < nl) { nl = lg; wl = what; }
                if (lg < 0.5) bad.Append(" ramp lane: " + what + " " + lg.ToString("0.0") + " m;");
            }
            if (ParkingKeepOut.Count > 0 && ParkingKeepOutMap == Map.file)
                foreach (var k in ParkingKeepOut)
                {
                    if (Math.Abs(k.x - s.CenterX) > 200 || Math.Abs(k.y - s.CenterY) > 200) continue;
                    double g = ZObject.ShuttlePlanGap(mx, my, hd, k.x, k.y) - k.r / 8.0, lg = ShuttleLaneGapM(mx, my, hd, k.x, k.y) - k.r / 8.0;
                    string what = "prop @" + (k.x / 16.0).ToString("0.0") + "," + (k.y / 16.0).ToString("0.0");
                    if (g < np) { np = g; wp = what; }
                    if (g < 1.5) bad.Append(" drawn " + what + " " + g.ToString("0.0") + " m;");
                    if (lg < nl) { nl = lg; wl = "prop"; }
                    if (lg < 0.5) bad.Append(" ramp lane: drawn " + what + " " + lg.ToString("0.0") + " m;");
                }
            int lanePts = 0, all = 0;
            for (int y = s.CenterY - 48; y <= s.CenterY + 48; y += 4)
                for (int x = s.CenterX - 48; x <= s.CenterX + 48; x += 4)
                {
                    if (s.ShuttleOutlineGapM(x, y) >= 0.5) continue;
                    all++; if (InExitLane(x, y, 0)) lanePts++;
                }
            if (lanePts > 0) bad.Append(" " + lanePts + " of " + all + " outline points in an exit lane;");
            if (NearGateFace(s.CenterX - 32, s.CenterY - 32, s.CenterX + 32, s.CenterY + 32, 32)) bad.Append(" within 2 tiles of a fort's gate face;");
            if (!ShuttlePadFreeAt(s.CenterX, s.CenterY, s, hd, out string why)) bad.Append(" the landing spot rule refuses it where it stands (" + why + ");");
            int inside = ShuttleUnitsInside(s, 2.0);
            if (inside > 0) bad.Append(" " + inside + " unit(s) within 2 m of its outline;");
            near = "vehicle " + nv.ToString("0.0") + " m (" + wv + "), building " + nb.ToString("0.0") + " m (" + wb + "), flag " + (nf < 90 ? nf.ToString("0.0") + " m" : "none near")
                   + ", item " + (ni < 90 ? ni.ToString("0.0") + " m" : "none near") + ", prop " + (np < 90 ? np.ToString("0.0") + " m (" + wp + ")" : "none near") + ", lane " + nl.ToString("0.0") + " m (" + wl + ")"
                   + ", plant crowns " + ShuttleKeepOutCount;
            return bad.ToString();
        }

        /// <summary>The checks and the parity report: DV144 held off until disposed (a Play session leaves the switch on).</summary>
        public static IDisposable SideShuttlesOff() => new SideShuttlesOffHold();
        sealed class SideShuttlesOffHold : IDisposable
        {
            readonly bool was = SideShuttlesDeviation;
            public SideShuttlesOffHold() { SideShuttlesDeviation = false; }
            public void Dispose() { SideShuttlesDeviation = was; }
        }
    }
}
