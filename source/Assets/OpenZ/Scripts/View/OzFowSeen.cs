// OpenZ (GPL-3.0) - version 1.5 kit los_fog: the MAYHEM fog's line-of-sight map ("seen now"). Plain C#, no Unity, never read by the rules (view only).
using System;

namespace OpenZ
{
    /// <summary>
    /// The fog's reveal map (<see cref="OzFowMask"/>) is explore-once; this
    /// map is what the player's side sees NOW, on the same grid (a byte a metre, <see cref="OzFowMask.CellPx"/> Zod px a cell).
    /// <list type="bullet">
    /// <item>A round (View/OzFogOfWar.Los.cs: about 20 a second, its stamps spread over the frames of the round): <see cref="Begin"/>, every sight
    /// circle of the player's side (its units, buildings and flags with the reveal's SightPx, and each standing radar's 48 m circle at its radius now)
    /// with <see cref="Stamp"/> into a buffer of its own (from the exact Zod px centre with a float radius: no whole-cell steps when a unit moves;
    /// 255 inside, down to 0 over a soft edge of <see cref="RampCells"/> cells whose half way point lies where the reveal's does, the sight radius
    /// - 1.5 m), then <see cref="End"/>: that buffer becomes <see cref="Target"/>.</item>
    /// <item><see cref="Held"/>: the target with a short hold: a cell goes up with it at once, and goes down only after it has stayed lower for
    /// <see cref="HoldRounds"/> rounds (0.3 s), then follows it. A unit that turns, shuffles in a crowd or is missed for a round while the list
    /// changes leaves its edge as it was: no breathing, no blink.</item>
    /// <item><see cref="Shown"/>: what is drawn and what the units' hiding reads. It follows <see cref="Held"/> both ways, linear in time: up over
    /// <c>upSeconds</c> (0 to 255), down over <c>downSeconds</c> (255 to 0); a cell only ever moves toward its goal, never past it. The shader's
    /// smootherstep of the value turns the linear ramp into an eased fade, so the line-of-sight fog comes back softly after a unit died or left.</item>
    /// <item>Bounded work: a whole-radius circle comes from a cached kernel (its centre to the nearest quarter metre: per row the inner run
    /// filled at once and the soft edge's bytes max-blitted, no arithmetic per cell; made once per radius and quarter); a radar circle while it
    /// grows is worked out per cell. The round's hold walks the box of cells that are or were seen, skipping 8 settled cells at once; the easing
    /// walks per row only the span of cells still moving. No allocation after the kernels in use are made.</item>
    /// </list>
    /// </summary>
    public sealed class OzFowSeen
    {
        /// <summary>The soft edge of a line-of-sight circle (cells = metres): from fully seen to not seen over this width.</summary>
        public const float RampCells = 5f;
        /// <summary>The soft edge's outer end lies this far past the sight radius (cells), so its half way point is the reveal's (radius - 1.5 m).</summary>
        public const float OutPadCells = 1f;
        /// <summary>Rounds a cell keeps its seen value after its target fell (6 rounds = 0.3 s at 20 rounds a second).</summary>
        public const int HoldRounds = 6;

        public readonly int W, H;
        /// <summary>The last complete round's sight circles (0 .. 255).</summary>
        public byte[] Target { get; private set; }
        /// <summary>The target with the hold (what Shown eases toward), and Shown (what is drawn).</summary>
        public readonly byte[] Held, Shown;
        /// <summary>Checks: stamps made, rounds ended, ease steps that changed a cell, cells walked by the easing (all rounds).</summary>
        public int Stamps, Rounds, EaseChanges; public long CellsEased;

        byte[] build;                                   // the round being stamped
        readonly byte[] age;                            // rounds a cell's target has stayed under its held value
        readonly int[] lo, hi;                          // per row: the span of cells where Shown may still differ from Held (lo > hi: none)
        int bx0, by0, bx1, by1; bool bAny;              // the box stamped in the round being built
        int tx0, ty0, tx1, ty1; bool tAny;              // the box of Target (the last complete round)
        int hx0, hy0, hx1, hy1; bool hAny;              // the box of cells whose held value is not 0
        double accUp, accDown;

        public OzFowSeen(int w, int h)
        {
            W = Math.Max(1, w); H = Math.Max(1, h);
            Target = new byte[W * H]; build = new byte[W * H]; Held = new byte[W * H]; Shown = new byte[W * H]; age = new byte[W * H];
            lo = new int[H]; hi = new int[H];
            for (int y = 0; y < H; y++) { lo[y] = int.MaxValue; hi[y] = -1; }
        }

        /// <summary>The cell of a Zod px coordinate, clamped into the map.</summary>
        public int CellX(int px) => px <= 0 ? 0 : Math.Min(W - 1, px / OzFowMask.CellPx);
        public int CellY(int py) => py <= 0 ? 0 : Math.Min(H - 1, py / OzFowMask.CellPx);
        /// <summary>The shown value at a Zod px point (0 not seen .. 255 seen).</summary>
        public byte ShownAtPx(int px, int py) => Shown[CellY(py) * W + CellX(px)];
        /// <summary>The target value at a Zod px point (the last complete round).</summary>
        public byte TargetAtPx(int px, int py) => Target[CellY(py) * W + CellX(px)];

        /// <summary>Starts a round (its stamps go into a buffer of their own until <see cref="End"/>).</summary>
        public void Begin() { bAny = false; }

        /// <summary>Stamps a sight circle of a whole radius of <paramref name="radiusPx"/> Zod px round the Zod px point (cx, cy) into the round (a
        /// max-blit): the centre taken to the nearest quarter metre, the circle from a cached kernel of that radius and quarter (per row: its span,
        /// the inner run filled at once, the soft edge's bytes; made once, on first use). The units, buildings, flags and full radar circles.</summary>
        public void Stamp(int cx, int cy, int radiusPx)
        {
            if (cx < 0 || cy < 0) { Stamp(cx, cy, (float)radiusPx); return; }
            Stamps++;
            int qx = (cx + 1) & ~1, qy = (cy + 1) & ~1;                                   // to 2 Zod px (a quarter metre)
            int bxc = qx >> 3, byc = qy >> 3;
            var k = KernelOf(radiusPx, (qx & 7) >> 1, (qy & 7) >> 1);
            int sx0 = int.MaxValue, sx1 = -1, sy0 = int.MaxValue, sy1 = -1;
            var t = build; var v = k.V;
            for (int j = 0; j < k.Rows; j++)
            {
                int y = byc + k.Y0 + j; if (y < 0 || y >= H) continue;
                int ka = k.Xa[j], kb = k.Xb[j]; if (ka > kb) continue;
                int xa = Math.Max(0, bxc + ka), xb = Math.Min(W - 1, bxc + kb); if (xa > xb) continue;
                int row = y * W, ofs = k.Ofs[j] - ka - bxc;                               // the kernel's byte of map column x: V[ofs + x]
                int ia = Math.Max(xa, bxc + k.Ia[j]), ib = Math.Min(xb, bxc + k.Ib[j]);
                if (ia <= ib)
                {
                    for (int x = xa; x < ia; x++) { byte b = v[ofs + x]; if (b > t[row + x]) t[row + x] = b; }
                    new Span<byte>(t, row + ia, ib - ia + 1).Fill(255);                   // the inner run: a memset
                    for (int x = ib + 1; x <= xb; x++) { byte b = v[ofs + x]; if (b > t[row + x]) t[row + x] = b; }
                }
                else for (int x = xa; x <= xb; x++) { byte b = v[ofs + x]; if (b > t[row + x]) t[row + x] = b; }
                if (xa < sx0) sx0 = xa; if (xb > sx1) sx1 = xb; if (y < sy0) sy0 = y; if (y > sy1) sy1 = y;
            }
            if (sx1 >= 0) Join(ref bx0, ref by0, ref bx1, ref by1, ref bAny, sx0, sy0, sx1, sy1);
        }

        /// <summary>A circle's cached rows: for a whole radius and a centre a quarter cell (sx, sy) into its cell, per row (Y0 + j relative to the
        /// centre's cell): the span Xa..Xb, the inner run Ia..Ib (all 255; Ia > Ib: none) and the bytes V[Ofs .. Ofs + Xb - Xa].</summary>
        sealed class Kern { public int Rows, Y0; public int[] Xa, Xb, Ia, Ib, Ofs; public byte[] V; }
        readonly System.Collections.Generic.Dictionary<int, Kern> kerns = new System.Collections.Generic.Dictionary<int, Kern>(64);
        /// <summary>Checks: kernels made (each 1 to 8 KB, the 16 quarters of a radius made together the first time it is stamped).</summary>
        public int Kernels => kerns.Count;

        Kern KernelOf(int radiusPx, int sx, int sy)
        {
            int key = (radiusPx << 4) | (sx << 2) | sy;
            if (kerns.TryGetValue(key, out var k)) return k;
            for (int q = 0; q < 16; q++) { int kq = (radiusPx << 4) | q; if (!kerns.ContainsKey(kq)) kerns[kq] = MakeKernel(radiusPx, q >> 2, q & 3); }   // a new radius: its 16 quarters at once (the match start makes those in use)
            return kerns[key];
        }

        static Kern MakeKernel(int radiusPx, int sx, int sy)
        {
            Kern k;
            float r = Math.Max(1f, radiusPx / (float)OzFowMask.CellPx), fx = sx * 0.25f, fy = sy * 0.25f;
            float outer = r + OutPadCells, inner = outer - RampCells, outer2 = outer * outer, inner2 = inner > 0f ? inner * inner : -1f;
            int y0 = (int)MathF.Floor(fy - outer), y1 = (int)MathF.Floor(fy + outer), n = y1 - y0 + 1;
            k = new Kern { Rows = n, Y0 = y0, Xa = new int[n], Xb = new int[n], Ia = new int[n], Ib = new int[n], Ofs = new int[n] };
            var bytes = new System.Collections.Generic.List<byte>(n * (int)(2 * outer + 2));
            for (int j = 0; j < n; j++)
            {
                float dy = y0 + j + 0.5f - fy, dy2 = dy * dy;
                k.Xa[j] = 0; k.Xb[j] = -1; k.Ia[j] = int.MaxValue / 2; k.Ib[j] = int.MinValue / 2; k.Ofs[j] = bytes.Count;
                if (dy2 >= outer2) continue;
                float xr = MathF.Sqrt(outer2 - dy2);
                int xa = (int)MathF.Ceiling(fx - xr - 0.5f), xb = (int)MathF.Floor(fx + xr - 0.5f);
                if (xa > xb) continue;
                k.Xa[j] = xa; k.Xb[j] = xb;
                for (int x = xa; x <= xb; x++)
                {
                    float dx = x + 0.5f - fx, d2 = dx * dx + dy2;
                    bytes.Add(d2 <= inner2 ? (byte)255 : Ramp(outer, MathF.Sqrt(d2)));
                }
                if (dy2 < inner2)
                {
                    float xi = MathF.Sqrt(inner2 - dy2);
                    int ia = Math.Max(xa, (int)MathF.Ceiling(fx - xi - 0.5f)), ib = Math.Min(xb, (int)MathF.Floor(fx + xi - 0.5f));
                    while (ia <= ib && bytes[k.Ofs[j] + ia - xa] != 255) ia++;            // the run holds only full cells
                    while (ib >= ia && bytes[k.Ofs[j] + ib - xa] != 255) ib--;
                    if (ia <= ib) { k.Ia[j] = ia; k.Ib[j] = ib; }
                }
            }
            k.V = bytes.ToArray();
            return k;
        }

        /// <summary>The soft edge's byte at distance d (cells) from the centre of a circle whose edge ends at <paramref name="outer"/>.</summary>
        static byte Ramp(float outer, float d)
        {
            float v = (outer - d) * (255f / RampCells);
            return v >= 255f ? (byte)255 : v <= 0f ? (byte)0 : (byte)(int)(v + 0.5f);
        }

        /// <summary>Stamps a sight circle of a FRACTIONAL radius of <paramref name="radiusPx"/> Zod px round the Zod px point (cx, cy) into the
        /// round (a max-blit), worked out per cell from the exact centre: a radar circle while it grows (its edge moves smoothly, never in steps).</summary>
        public void Stamp(int cx, int cy, float radiusPx)
        {
            Stamps++;
            float r = Math.Max(1f, radiusPx / OzFowMask.CellPx), fx = cx / (float)OzFowMask.CellPx, fy = cy / (float)OzFowMask.CellPx;
            float outer = r + OutPadCells, inner = outer - RampCells, outer2 = outer * outer, inner2 = inner > 0f ? inner * inner : -1f;
            int y0 = Math.Max(0, (int)Math.Floor(fy - outer)), y1 = Math.Min(H - 1, (int)Math.Floor(fy + outer));
            int sx0 = int.MaxValue, sx1 = -1, sy0 = int.MaxValue, sy1 = -1;
            var t = build;
            for (int y = y0; y <= y1; y++)
            {
                float dy = y + 0.5f - fy, dy2 = dy * dy;
                if (dy2 >= outer2) continue;
                float xr = MathF.Sqrt(outer2 - dy2);
                int xa = Math.Max(0, (int)MathF.Ceiling(fx - xr - 0.5f)), xb = Math.Min(W - 1, (int)MathF.Floor(fx + xr - 0.5f));
                if (xa > xb) continue;
                int ia = int.MaxValue, ib = -1;                                       // the run inside the inner disc: all 255
                if (dy2 < inner2)
                {
                    float xi = MathF.Sqrt(inner2 - dy2);
                    ia = Math.Max(xa, (int)MathF.Ceiling(fx - xi - 0.5f)); ib = Math.Min(xb, (int)MathF.Floor(fx + xi - 0.5f));
                }
                int row = y * W;
                for (int x = xa; x <= xb; x++)
                {
                    if (x >= ia && x <= ib) { new Span<byte>(t, row + ia, ib - ia + 1).Fill(255); x = ib; continue; }   // a memset
                    int k = row + x; int old = t[k];
                    if (old == 255) continue;
                    float dx = x + 0.5f - fx; int b = Ramp(outer, MathF.Sqrt(dx * dx + dy2));
                    if (b > old) t[k] = (byte)b;
                }
                if (xa < sx0) sx0 = xa; if (xb > sx1) sx1 = xb; if (y < sy0) sy0 = y; if (y > sy1) sy1 = y;
            }
            if (sx1 >= 0) Join(ref bx0, ref by0, ref bx1, ref by1, ref bAny, sx0, sy0, sx1, sy1);
        }

        /// <summary>Ends a round: its stamps become <see cref="Target"/>, the hold steps once, the cells whose held value moved away from Shown
        /// join the easing.</summary>
        public void End()
        {
            Rounds++;
            var old = Target; Target = build; build = old;                            // the old target is cleared for the next round
            if (tAny) for (int y = ty0; y <= ty1; y++) Array.Clear(build, y * W + tx0, tx1 - tx0 + 1);
            tx0 = bx0; ty0 = by0; tx1 = bx1; ty1 = by1; tAny = bAny; bAny = false;
            if (!tAny && !hAny) return;
            int ux0, uy0, ux1, uy1; bool u = false; ux0 = uy0 = ux1 = uy1 = 0;
            if (tAny) Join(ref ux0, ref uy0, ref ux1, ref uy1, ref u, tx0, ty0, tx1, ty1);
            if (hAny) Join(ref ux0, ref uy0, ref ux1, ref uy1, ref u, hx0, hy0, hx1, hy1);
            int nx0 = int.MaxValue, ny0 = int.MaxValue, nx1 = -1, ny1 = -1;
            var t = Target; var h = Held; var s = Shown; var a = age;
            for (int y = uy0; y <= uy1; y++)
            {
                int row = y * W, l = lo[y], r = hi[y];
                for (int x = ux0; x <= ux1; x++)
                {
                    int i = row + x;
                    if (x + 8 <= ux1 + 1)                                                      // 8 settled cells at once (target = held = shown, no hold running)
                    {
                        ulong hw = Word(h, i);
                        if (Word(t, i) == hw && Word(s, i) == hw && Word(a, i) == 0UL)
                        {
                            if (hw != 0UL) { if (x < nx0) nx0 = x; if (x + 7 > nx1) nx1 = x + 7; if (y < ny0) ny0 = y; if (y > ny1) ny1 = y; }
                            x += 7; continue;
                        }
                    }
                    int tv = t[i], hv = h[i];
                    if ((tv | hv) == 0) continue;
                    if (tv >= hv) { h[i] = (byte)tv; a[i] = 0; hv = tv; }
                    else if (a[i] < HoldRounds) a[i]++;
                    else { h[i] = (byte)tv; hv = tv; }
                    if (hv != s[i]) { if (x < l) l = x; if (x > r) r = x; }
                    if (hv != 0) { if (x < nx0) nx0 = x; if (x > nx1) nx1 = x; if (y < ny0) ny0 = y; if (y > ny1) ny1 = y; }
                }
                lo[y] = l; hi[y] = r;
            }
            hAny = nx1 >= 0; if (hAny) { hx0 = nx0; hy0 = ny0; hx1 = nx1; hy1 = ny1; }
        }

        /// <summary>Moves Shown toward Held for a frame of <paramref name="dt"/> seconds: up 255 over <paramref name="upSeconds"/>, down 255 over
        /// <paramref name="downSeconds"/> (whole bytes, the rest carried to the next frame, so the speed is exact at any frame rate). Returns true
        /// when a cell changed.</summary>
        public bool Ease(float dt, float upSeconds, float downSeconds)
        {
            if (dt > 0f)
            {
                accUp += 255.0 * dt / Math.Max(1e-3, upSeconds);
                accDown += 255.0 * dt / Math.Max(1e-3, downSeconds);
            }
            int up = (int)Math.Min(255.0, accUp), down = (int)Math.Min(255.0, accDown);
            if (up == 0 && down == 0) return false;
            accUp -= up; accDown -= down;
            bool changed = false, any = false;
            var s = Shown; var h = Held;
            for (int y = 0; y < H; y++)
            {
                int l = lo[y], r = hi[y];
                if (r < l) continue;
                int row = y * W, nl = int.MaxValue, nr = -1;
                CellsEased += r - l + 1;
                for (int x = l; x <= r; x++)
                {
                    int i = row + x;
                    if (x + 8 <= r + 1 && Word(s, i) == Word(h, i)) { x += 7; continue; }    // 8 cells at their goal at once
                    int a = s[i], b = h[i];
                    if (a == b) continue;
                    int n = a < b ? Math.Min(b, a + up) : Math.Max(b, a - down);   // toward the goal, never past it
                    if (n != a) { s[i] = (byte)n; changed = true; }
                    if (n == b) continue;
                    if (x < nl) nl = x; if (x > nr) nr = x;
                }
                lo[y] = nl; hi[y] = nr; if (nr >= 0) any = true;
            }
            if (!any) { accUp = accDown = 0.0; }                                       // nothing moving: no step saved up for later (no jump when it starts)
            if (changed) EaseChanges++;
            return changed;
        }

        /// <summary>True while some cell is still easing.</summary>
        public bool Easing { get { for (int y = 0; y < H; y++) if (hi[y] >= lo[y]) return true; return false; } }

        /// <summary>Held = Shown = Target at once (the match start: no fade on the first frame).</summary>
        public void ShowAll()
        {
            Buffer.BlockCopy(Target, 0, Held, 0, Target.Length); Buffer.BlockCopy(Target, 0, Shown, 0, Target.Length); Array.Clear(age, 0, age.Length);
            for (int y = 0; y < H; y++) { lo[y] = int.MaxValue; hi[y] = -1; }
            hAny = tAny; hx0 = tx0; hy0 = ty0; hx1 = tx1; hy1 = ty1;
            accUp = accDown = 0.0;
        }

        /// <summary>Checks: the share of the map seen now (Shown at or over half), 0..1.</summary>
        public float SeenShare()
        {
            int n = 0; for (int i = 0; i < Shown.Length; i++) if (Shown[i] >= 128) n++;
            return n / (float)Shown.Length;
        }

        /// <summary>8 cells as one number (any byte offset; the bytes' order does not matter for an equality test).</summary>
        static ulong Word(byte[] b, int i) => BitConverter.ToUInt64(b, i);

        static void Join(ref int x0, ref int y0, ref int x1, ref int y1, ref bool any, int ax0, int ay0, int ax1, int ay1)
        {
            if (!any) { x0 = ax0; y0 = ay0; x1 = ax1; y1 = ay1; any = true; return; }
            if (ax0 < x0) x0 = ax0; if (ay0 < y0) y0 = ay0; if (ax1 > x1) x1 = ax1; if (ay1 > y1) y1 = ay1;
        }
    }
}
