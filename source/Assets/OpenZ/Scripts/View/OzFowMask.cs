// OpenZ (GPL-3.0) - version 1.4, FOG OF WAR: the reveal map. Plain C#, no Unity, never read by the rules (view only).
using System;
using System.Collections.Generic;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4, the optional FOG OF WAR (View/OzFogOfWar.cs): its reveal map. One byte per cell over the whole level; a cell is
    /// <see cref="CellPx"/> x <see cref="CellPx"/> Zod px (8 px = 1 m, two cells a tile), so level 17 (110 x 128 tiles) is 220 x 256 cells, 56 KB.
    /// <list type="bullet">
    /// <item><see cref="Target"/>: what the player's side has explored. A unit or building stamps its sight circle with a soft edge
    /// (<see cref="RampCells"/> cells from full to nothing) and a cell only ever goes up (explore-once: once opened it is never covered
    /// again, also when every unit has left). Only <see cref="Clear"/> (the testing panel's COVER AGAIN) lowers it.</item>
    /// <item><see cref="Shown"/>: what is drawn. It follows <see cref="Target"/> up by a step per frame (the soft animated reveal) and never
    /// goes down; it is never above <see cref="Target"/>.</item>
    /// </list>
    /// The stamps are integer cells and cached round kernels: a stamp is a max-blit of bytes, no square root per cell.
    /// Version 1.4's radar circle (View/OzFowRadar.cs) stamps with <see cref="StampFractional"/>: a float radius, its soft edge worked out per cell.
    /// </summary>
    public sealed class OzFowMask
    {
        /// <summary>Zod px per cell side: 8 px = 1 m, two cells a 16 px tile.</summary>
        public const int CellPx = 8;
        /// <summary>The soft edge of a sight circle in cells (metres): from fully open to closed over this width.</summary>
        public const int RampCells = 3;
        /// <summary>A cell at or above this shown value counts as explored for hiding, hover and the health bars (half way up the edge).</summary>
        public const byte Revealed = 128;

        public readonly int W, H;
        public readonly byte[] Target, Shown;
        /// <summary>Checks: stamps made, stamps that changed at least one cell, cells raised.</summary>
        public int Stamps, StampsThatOpened; public long CellsRaised;

        int bx0, by0, bx1, by1; bool easing;   // the box of cells where Shown may still be under Target
        readonly Dictionary<int, byte[]> kernels = new Dictionary<int, byte[]>();

        public OzFowMask(int mapWidthPx, int mapHeightPx)
        {
            W = Math.Max(1, (mapWidthPx + CellPx - 1) / CellPx); H = Math.Max(1, (mapHeightPx + CellPx - 1) / CellPx);
            Target = new byte[W * H]; Shown = new byte[W * H];
        }

        /// <summary>The cell of a Zod px coordinate, clamped into the map.</summary>
        public int CellX(int px) => px <= 0 ? 0 : Math.Min(W - 1, px / CellPx);
        public int CellY(int py) => py <= 0 ? 0 : Math.Min(H - 1, py / CellPx);

        /// <summary>The shown value at a Zod px point (0 closed .. 255 open).</summary>
        public byte ShownAtPx(int px, int py) => Shown[CellY(py) * W + CellX(px)];
        /// <summary>The explored value at a Zod px point (0 closed .. 255 open).</summary>
        public byte TargetAtPx(int px, int py) => Target[CellY(py) * W + CellX(px)];

        /// <summary>The round kernel of a sight radius in cells: 255 inside, down to 0 over the last RampCells cells, 0 outside.</summary>
        public byte[] Kernel(int r)
        {
            if (kernels.TryGetValue(r, out var k)) return k;
            int n = 2 * r + 1; k = new byte[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    double d = Math.Sqrt((x - r) * (double)(x - r) + (y - r) * (double)(y - r));
                    double v = (r - d) / RampCells;
                    k[y * n + x] = v >= 1.0 ? (byte)255 : v <= 0.0 ? (byte)0 : (byte)Math.Round(v * 255.0);
                }
            kernels[r] = k;
            return k;
        }

        /// <summary>Stamps a sight circle of radius <paramref name="radiusPx"/> Zod px round the Zod px point (cx, cy) into Target.
        /// Returns true when at least one cell went up.</summary>
        public bool Stamp(int cx, int cy, int radiusPx)
        {
            Stamps++;
            int r = Math.Max(1, (radiusPx + CellPx / 2) / CellPx), n = 2 * r + 1;
            int ox = (cx >= 0 ? cx / CellPx : -1) - r, oy = (cy >= 0 ? cy / CellPx : -1) - r;
            var k = Kernel(r);
            int x0 = Math.Max(0, ox), y0 = Math.Max(0, oy), x1 = Math.Min(W - 1, ox + n - 1), y1 = Math.Min(H - 1, oy + n - 1);
            if (x0 > x1 || y0 > y1) return false;
            bool any = false;
            for (int y = y0; y <= y1; y++)
            {
                int row = y * W, krow = (y - oy) * n - ox;
                for (int x = x0; x <= x1; x++)
                {
                    byte v = k[krow + x];
                    if (v > Target[row + x]) { Target[row + x] = v; any = true; CellsRaised++; }
                }
            }
            if (any) { StampsThatOpened++; Grow(x0, y0, x1, y1); }
            return any;
        }

        /// <summary>1.4 radar circle (View/OzFowRadar.cs): stamps a circle of a FRACTIONAL radius of <paramref name="radiusPx"/> Zod px round
        /// the Zod px point (cx, cy) into Target, with the same soft edge as <see cref="Kernel"/> (255 inside, down to 0 over the last
        /// <see cref="RampCells"/> cells, 0 outside) but measured from the exact point with a float radius, so a circle that grows a little
        /// every frame moves its edge smoothly, never in whole-cell steps. A max-blit like <see cref="Stamp"/> (explore-once); the units keep
        /// the integer kernels. Returns true when at least one cell went up.</summary>
        public bool StampFractional(int cx, int cy, float radiusPx)
        {
            Stamps++;
            double r = Math.Max(1.0, radiusPx / (double)CellPx), fx = cx / (double)CellPx, fy = cy / (double)CellPx;   // in cells; a cell's middle is at its index + 0.5
            int x0 = Math.Max(0, (int)Math.Floor(fx - r)), y0 = Math.Max(0, (int)Math.Floor(fy - r));
            int x1 = Math.Min(W - 1, (int)Math.Floor(fx + r)), y1 = Math.Min(H - 1, (int)Math.Floor(fy + r));
            if (x0 > x1 || y0 > y1) return false;
            double outer2 = r * r, inner = r - RampCells, inner2 = inner > 0.0 ? inner * inner : -1.0;
            bool any = false;
            for (int y = y0; y <= y1; y++)
            {
                double dy = y + 0.5 - fy, dy2 = dy * dy;
                if (dy2 >= outer2) continue;
                int row = y * W;
                for (int x = x0; x <= x1; x++)
                {
                    int i = row + x; byte t = Target[i];
                    if (t == 255) continue;
                    double dx = x + 0.5 - fx, d2 = dx * dx + dy2;
                    if (d2 >= outer2) continue;
                    double v = d2 <= inner2 ? 1.0 : (r - Math.Sqrt(d2)) / RampCells;
                    byte b = v >= 1.0 ? (byte)255 : (byte)Math.Round(v * 255.0);
                    if (b > t) { Target[i] = b; any = true; CellsRaised++; }
                }
            }
            if (any) { StampsThatOpened++; Grow(x0, y0, x1, y1); }
            return any;
        }

        void Grow(int x0, int y0, int x1, int y1)
        {
            if (!easing) { bx0 = x0; by0 = y0; bx1 = x1; by1 = y1; easing = true; return; }
            if (x0 < bx0) bx0 = x0; if (y0 < by0) by0 = y0; if (x1 > bx1) bx1 = x1; if (y1 > by1) by1 = y1;
        }

        /// <summary>Raises Shown toward Target by <paramref name="step"/> (1..255) inside the box still easing. Returns true when a cell changed.</summary>
        public bool Ease(int step)
        {
            if (!easing) return false;
            if (step < 1) step = 1;
            bool changed = false;
            int nx0 = int.MaxValue, ny0 = int.MaxValue, nx1 = -1, ny1 = -1;
            for (int y = by0; y <= by1; y++)
            {
                int row = y * W;
                for (int x = bx0; x <= bx1; x++)
                {
                    int i = row + x; int s = Shown[i], t = Target[i];
                    if (s >= t) continue;
                    s = Math.Min(t, s + step); Shown[i] = (byte)s; changed = true;
                    if (s < t) { if (x < nx0) nx0 = x; if (x > nx1) nx1 = x; if (y < ny0) ny0 = y; if (y > ny1) ny1 = y; }
                }
            }
            if (nx1 < 0) easing = false; else { bx0 = nx0; by0 = ny0; bx1 = nx1; by1 = ny1; }
            return changed;
        }

        /// <summary>True while some cell is still easing up.</summary>
        public bool Easing => easing;

        /// <summary>Shown = Target at once (the match start: the start area is open on the first frame, no reveal animation).</summary>
        public void ShowAll() { Buffer.BlockCopy(Target, 0, Shown, 0, Target.Length); easing = false; }

        /// <summary>Checks only (the Cost Bench): a saved explored and shown map put back, and the easing box made again from every cell whose
        /// shown value is still under its explored value (so a cell that was easing up when the map was saved goes on easing). False when the
        /// sizes do not match.</summary>
        public bool RestoreFrom(byte[] target, byte[] shown)
        {
            if (target == null || shown == null || target.Length != Target.Length || shown.Length != Shown.Length) return false;
            Buffer.BlockCopy(target, 0, Target, 0, Target.Length); Buffer.BlockCopy(shown, 0, Shown, 0, Shown.Length);
            easing = false;
            for (int y = 0; y < H; y++)
            {
                int row = y * W;
                for (int x = 0; x < W; x++) if (Shown[row + x] < Target[row + x]) Grow(x, y, x, y);
            }
            return true;
        }

        /// <summary>Everything covered again (the testing panel's COVER AGAIN only; the game never calls it).</summary>
        public void Clear() { Array.Clear(Target, 0, Target.Length); Array.Clear(Shown, 0, Shown.Length); easing = false; }

        /// <summary>Checks: the share of the map explored (Target at or over <see cref="Revealed"/>), 0..1.</summary>
        public float ExploredShare()
        {
            int n = 0; for (int i = 0; i < Target.Length; i++) if (Target[i] >= Revealed) n++;
            return n / (float)Target.Length;
        }
    }
}
