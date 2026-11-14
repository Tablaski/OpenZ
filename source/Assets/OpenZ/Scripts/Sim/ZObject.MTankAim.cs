using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// There are no ranks in between).
    /// <para>Rule, the MEDIUM TANK only (<see cref="ZMTank.Type"/>, never the mech's "medium"): a shell's scatter (Zod's one draw per axis,
    /// rand()%32 - 16, zobject.cpp:2155) is widened x <see cref="ZWorld.MTankGreenScatter"/> while the tank is NOT a veteran (no gold star);
    /// a veteran medium tank keeps its scatter exactly as before (DV12). Two states only: veteran or not. The same random draws as before
    /// (only their size changes): no draw of its own, sim only, deterministic. Light tank, heavy, mech and every other unit: untouched.</para>
    /// <para>Switch <see cref="ZWorld.MTankAimDeviation"/>: ZGame turns it on with DV145 (off in 'Zod exact'); headless default off.</para>
    /// </summary>
    public partial class ZObject
    {
        /// <summary>DV151: the scatter offset of this shooter's shell after DV12 (veterans) and DV151 (a medium tank that is not a veteran).</summary>
        internal int AimScatter(int offset)
        {
            int s = Scatter(offset);
            double f = MTankScatterFactor();
            return f == 1.0 ? s : (int)Math.Floor(s * f);   // run 11 fix: rounded down, so the x1.10 widening takes 2 of the 32 draws per axis off the hull (not 1 or 3)
        }

        /// <summary>DV151: the medium tank's scatter factor: x MTankGreenScatter when it is not a veteran; 1.0 for a veteran medium tank,
        /// any other unit, or the switch off.</summary>
        public double MTankScatterFactor()
        {
            if (!ZWorld.MTankAimDeviation || Kind != ZKind.Vehicle || Type != ZMTank.Type || Veteran) return 1.0;
            return ZWorld.MTankGreenScatter;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV151 switch (code switch; ZGame: with DV145, off in 'Zod exact'; headless default off).</summary>
        public static bool MTankAimDeviation = false;
        /// <summary>
        /// Run 11 fix: x1.10,
        /// rounded down = 2 of Zod's 32 draws per axis now land off a standing target's hull: shells on the hull of a standing enemy light
        /// tank 87.9 % of a veteran medium tank's at 60 / 100 px (x1.04 left it at 95.8 %); a moving target see W/RESULTS_V12.md run 11
        /// (Editor/OzV12MTankAimCount.cs Run11Report).</summary>
        public static double MTankGreenScatter = 1.10;

        /// <summary>The parity report and the older counted checks: no DV151 while this lives (a Play session leaves the switch on).</summary>
        public static IDisposable MTankAimOff() => new MTankAimOffHold();
        sealed class MTankAimOffHold : IDisposable
        {
            readonly bool was = MTankAimDeviation;
            public MTankAimOffHold() { MTankAimDeviation = false; }
            public void Dispose() { MTankAimDeviation = was; }
        }
    }
}
