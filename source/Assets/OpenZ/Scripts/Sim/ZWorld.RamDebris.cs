using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ 1.5 deviation DV186 "a rammed or crushed vehicle's turret lands near its wreck" (kit ram_throw_fix, on DV182 the Mammoth's ram and
    /// DV183 the Bizon's crush). Zod throws a dead tank's / gun's turret debris to a random point up to max_turrent_horizontal_distance (300 px,
    /// about 37 m) away on each axis (vlight.cpp:261-278, ZWorld.DeathDebris); after a Mammoth ram the view flew the rammed tank's turret that far
    /// <para>With DV186 on, a vehicle or gun that dies while doomed by a ram or a crush (RamDoomBy set: the ram's kill, or anything that kills it
    /// during the hold) has its debris point moved near the wreck: for a RAM <see cref="RamDebrisAheadPx"/> px beyond the hull's centre along the
    /// ram (the side it is flipped to, never back onto the Mammoth) +- <see cref="RamDebrisSpreadPx"/> px; for a CRUSH (no turret is thrown, the
    /// view squashes it) <see cref="CrushDebrisBackPx"/> px behind the centre (where the Bizon has already passed) +- <see cref="CrushDebrisSpreadPx"/>
    /// px. The spread is Zod's own draw scaled down (the same two random numbers, nothing drawn more or less: every later random number of the
    /// match is the one it was). The debris' damage, radius and time are Zod's. Whole numbers only (deterministic).</para>
    /// <para>Switch <see cref="RamDebrisDeviation"/>: View/ZGame.cs on at every start (off in 'Zod exact'); headless default off; the parity
    /// report's Zod rows and the counted checks hold it off. Off, or a death that is not a ram's / crush's: not one number changes.</para>
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV186 (kit ram_throw_fix). ZGame turns it on at every start (off in 'Zod exact'); headless default off.</summary>
        public static bool RamDebrisDeviation = false;
        /// <summary>A rammed hull's turret debris: this far beyond its centre along the ram (5 m), spread +- 2 m.</summary>
        public const int RamDebrisAheadPx = 40, RamDebrisSpreadPx = 16;
        /// <summary>A crushed one's: this far behind its centre (2 m, under the Bizon's passed track), spread +- 1 m.</summary>
        public const int CrushDebrisBackPx = 16, CrushDebrisSpreadPx = 8;

        /// <summary>The parity report and the older counted checks: off while this lives.</summary>
        public static IDisposable RamDebrisOff() => new RamDebrisOffHold();
        sealed class RamDebrisOffHold : IDisposable
        {
            readonly bool was = RamDebrisDeviation;
            public RamDebrisOffHold() { RamDebrisDeviation = false; }
            public void Dispose() { RamDebrisDeviation = was; }
        }

        /// <summary>Statistics (the counted check): debris points moved near a rammed / crushed wreck.</summary>
        public int RamDebrisMoved;

        /// <summary>DeathDebris, right after Zod's draw of `m` for `o` (One(mh, mv, ..)): a ram's / crush's victim gets the same draw scaled
        /// down round a point next to its wreck. No random draw here.</summary>
        void RamDebrisNear(ZObject o, ZMissile m, int mh, int mv)
        {
            if (!RamDebrisDeviation || o == null || m == null || o.RamDoomBy < 0) return;
            bool crush = o.RamCrushed;
            int along = crush ? -CrushDebrisBackPx : RamDebrisAheadPx, h = crush ? CrushDebrisSpreadPx : RamDebrisSpreadPx;
            long ox = m.x - (o.LocX + 16), oy = m.y - (o.LocY + 16);                       // Zod's draw: -mh .. +mh, -mv .. +mv
            int cx = o.CenterX + (int)((long)o.RamDirX * along / 1024), cy = o.CenterY + (int)((long)o.RamDirY * along / 1024);
            m.x = cx + (mh > 0 ? (int)(ox * h / mh) : 0);
            m.y = cy + (mv > 0 ? (int)(oy * h / mv) : 0);
            RamDebrisMoved++;
        }
    }
}
