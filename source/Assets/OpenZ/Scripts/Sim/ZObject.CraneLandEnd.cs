// OpenZ (GPL-3.0) - version 1.5 kit crane_deck_sound_r3: DV206 "a crane repairs a bridge from its land end".
using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// destroyed bridge, it drove to the bridge's middle over the water; it only needs to come to the side, close enough, and repair).
    /// <para>Zod (zobject.cpp:2915-3091, ProcessCraneRepairWP): the crane paths to the NEAREST crane entrance by straight distance (a bridge
    /// has two, 2 tiles past each end on its axis, bbridge.cpp:454-481), drives from there straight to the bridge's centre (over the river
    /// when the deck is down), straight back out to the entrance, and the bridge is repaired on the next tick.</para>
    /// <para>DV206, bridges only (other buildings unchanged): (1) the stop is the nearest REACHABLE land end: of the two entrances (and,
    /// when one is blocked, up to 2 more tiles out along the axis) the crane takes the one it can drive to whose 2 x 2 box lies on dry ground
    /// (no water tile, off the bridge's own box), the nearer by Zod's own measure; (2) at that stop it never drives onto the span: it stays,
    /// does its work for as long as Zod's drive in and out would have taken (the same straight line, the same tile speeds, 4 s at least,
    /// 40 s at most), and the bridge is repaired at the same moment of the job as in Zod (the next tick after the job's end). The damaged
    /// bridge that still stands is mended from the same stop (DV61's 4 s, unchanged). A crane order to a bridge with no reachable dry stop
    /// is Zod's (the nearest entrance; the pathing layer gives such an order up as before).</para>
    /// <para>Switch <see cref="ZWorld.CraneLandEndDeviation"/>: View/ZGame.cs turns it on at every level start, off in 'Zod exact'; static
    /// default off (headless worlds, the parity report's Zod rows and probes, every older counted check). Positions and times are ints and
    /// doubles from the map and the crane's own speed: no float, no random draw, no frame time. Parity row: Editor/OpenZParity.V15CraneLandEnd.cs,
    /// counted check Editor/OzV15CraneLandEndCount.cs (menu OpenZ/Checks/V1.5/Crane repairs a bridge from its land end (no Play)).</para>
    /// </summary>
    public partial class ZObject
    {
        double craneWorkFrom = -1;

        /// <summary>DV206 in force for this crane on building t (a bridge).</summary>
        internal bool CraneLandEndOn(ZObject t) => ZWorld.CraneLandEndDeviation && t != null && t.IsBridge && Kind == ZKind.Vehicle && Type == "crane" && World != null;

        /// <summary>Sim time this crane's work at its stop began / ends (DV61's mend of a standing building, DV206's work at a bridge's
        /// land end); -1 = none. The view reads them (the repair's progress).</summary>
        public double CraneWorkFrom => craneFixUntil >= 0 ? craneWorkFrom : -1;
        public double CraneWorkUntil => craneFixUntil;

        /// <summary>DV206: the crane's stop for a repair of bridge t: the nearest reachable dry spot at either end (Zod's entrance, else up to
        /// 2 tiles further out along the axis). False: none (Zod's pick then).</summary>
        internal bool CraneLandEndPick(ZObject t, out int x, out int y)
        {
            x = y = 0;
            if (!t.GetCraneEntrance(out int ex, out int ey, out int ex2, out int ey2)) return false;
            var pa = World.Pathing; var reach = pa != null && pa.Enabled ? pa.Reach : null;
            if (reach == null) return false;
            int c = reach.UnitComp(this, false);                                                     // a crane carries no explosives: the grid with the rocks
            if (c < 0) return false;
            bool vert = t.Type == "bridge_vert";
            bool found = false; double best = double.MaxValue;
            for (int e = 0; e < 2; e++)
            {
                int bx = e == 0 ? ex : ex2, by = e == 0 ? ey : ey2, ox = vert ? 0 : (e == 0 ? -1 : 1), oy = vert ? (e == 0 ? -1 : 1) : 0;
                for (int k = 0; k <= ZWorld.CraneLandEndSearch; k += 8)
                {
                    int px = bx + ox * k, py = by + oy * k;
                    if (!reach.PointOk(px, py, false, false, c) || !CraneLandEndDry(t, px, py)) continue;
                    double d = Math.Sqrt(Math.Pow(LocX - px, 2) + Math.Pow(LocY - py, 2));           // Zod measures from loc (top-left)
                    if (d < best) { best = d; x = px; y = py; found = true; }
                    break;                                                                           // the first dry spot of this end
                }
            }
            return found;
        }

        /// <summary>DV206: a crane's 2 x 2 box with its centre on (px, py) is on dry land: no Water / DeepWater tile under it and off the
        /// bridge's own box.</summary>
        internal bool CraneLandEndDry(ZObject t, int px, int py)
        {
            int hw = WidthPix >> 1, hh = HeightPix >> 1, x0 = px - hw, y0 = py - hh, x1 = px + hw - 1, y1 = py + hh - 1;
            if (x0 < t.LocX + t.WidthPix && t.LocX < x1 + 1 && y0 < t.LocY + t.HeightPix && t.LocY < y1 + 1) return false;
            var m = World.Map;
            for (int ty = y0 >> 4; ty <= y1 >> 4; ty++)
                for (int tx = x0 >> 4; tx <= x1 >> 4; tx++)
                {
                    var k = m.ClassAt(tx, ty);
                    if (k == ZClass.Water || k == ZClass.DeepWater || k == ZClass.Impassable) return false;
                }
            return true;
        }

        /// <summary>DV206: how long the crane works at its stop: Zod's drive from here straight to the bridge's centre and back out (the
        /// same tile speeds as ProcessMove: road 1.689, water 0.7, ground 1, never 0), at least ZWorld.CraneFixSeconds, at most
        /// ZWorld.CraneLandEndMaxSeconds.</summary>
        internal double CraneLandEndHold(ZObject t)
        {
            if (!t.GetCraneCenter(out int cx, out int cy)) return ZWorld.CraneFixSeconds;
            double sp = MoveSpeed * DamagedSpeed();
            int dx = cx - CenterX, dy = cy - CenterY, n = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (sp <= 0 || n == 0) return ZWorld.CraneFixSeconds;
            double len = Math.Sqrt((double)dx * dx + (double)dy * dy), step = len / n, sum = 0;
            var pa = World.Pathing;
            for (int i = 0; i < n; i++)
            {
                int px = CenterX + (int)Math.Floor(dx * (i + 0.5) / n), py = CenterY + (int)Math.Floor(dy * (i + 0.5) / n);
                double tile = World.Map.TileWalkSpeed(px, py);
                if (pa != null && pa.Enabled) tile = pa.SafeTileSpeed(this, tile);
                if (tile <= 0) tile = ZConst.WATER_SPEED;
                sum += step / (sp * tile);
            }
            sum *= 2;                                                                                // in to the centre and back out
            return Math.Max(ZWorld.CraneFixSeconds, Math.Min(ZWorld.CraneLandEndMaxSeconds, sum));
        }

        /// <summary>DV206, ProcessCraneRepairWP at the stop (stage GotoEntrance reached, bridge t destroyed): the work starts here; true = done.</summary>
        internal bool CraneLandEndStart(ZObject t)
        {
            if (!CraneLandEndOn(t) || !t.Destroyed) return false;
            cur.stage = Stage.ExitBuilding; SetTarget(CenterX, CenterY); SetVelocity();
            craneWorkFrom = World.Time; craneFixUntil = World.Time + CraneLandEndHold(t);
            ZWorld.CraneLandEndJobs++;
            World.Emit(ZEventType.CraneAnim, this, t, 0, 0, 0, 0, true);
            return true;
        }

        /// <summary>DV61 / DV206: the work at the stop begins now (the view's progress).</summary>
        internal void CraneWorkBegins() { craneWorkFrom = World != null ? World.Time : -1; }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV206 on: a crane repairs a bridge from its nearest reachable dry land end and never drives onto the span.</summary>
        public static bool CraneLandEndDeviation = false;
        /// <summary>DV206: px searched past Zod's entrance along the bridge's axis when the entrance itself is not dry or not reachable.</summary>
        public const int CraneLandEndSearch = 32;
        /// <summary>DV206: the longest work at a bridge's land end (s).</summary>
        public const double CraneLandEndMaxSeconds = 40.0;
        /// <summary>DV206 statistics (counted checks, all worlds): repairs started at a land end.</summary>
        public static int CraneLandEndJobs;
        /// <summary>Holds DV206 off (the parity report's Zod rows and probes), puts the value back on Dispose.</summary>
        public static IDisposable CraneLandEndOff() => new CraneLandEndHoldScope(false);
        /// <summary>Holds DV206 on (its counted check), puts the value back on Dispose.</summary>
        public static IDisposable CraneLandEndOnScope() => new CraneLandEndHoldScope(true);
        sealed class CraneLandEndHoldScope : IDisposable
        {
            readonly bool was = CraneLandEndDeviation;
            public CraneLandEndHoldScope(bool on) { CraneLandEndDeviation = on; }
            public void Dispose() { CraneLandEndDeviation = was; }
        }
    }
}
