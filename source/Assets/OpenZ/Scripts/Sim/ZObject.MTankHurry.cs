using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// <para>Cause (read-only code checks): nothing slows the medium tank; its numbers do (10 px/s against the light tank's 14, the light
    /// tank's 120 px range so the mech, the heavy and the guns outrange it), and in a fight it only gets Zod's short sidestep runs.</para>
    /// <para>Rule, the medium tank only: while it fights or sidesteps (its first waypoint Dodge, Attack or Agro, never a plain Move) and is
    /// under fire (a shell aimed at it launched, or an enemy hit on it, within MTankHurrySec s), it drives MTankHurryMul (x1.10) as fast,
    /// MTankHurryFarMul (x1.15) when that shell came from beyond its own range; Zod's 1.8 run still wins while it runs. Sim time only, no
    /// random number. Light, heavy, mech, jeep: untouched. ZWorld.MTankHurryDeviation: ZGame turns it on with DV145 (off in 'Zod exact');
    /// headless off.</para>
    /// </summary>
    public partial class ZObject
    {
        /// <summary>DV150: sim time of the last enemy shell aimed at this unit, and of the last one from beyond its own range.</summary>
        public double IncomingAt = -100, IncomingFarAt = -100;

        internal void NoteIncoming(ZObject a, double time)
        {
            IncomingAt = time;
            if (Dist(CenterX, CenterY, a.CenterX, a.CenterY) > RangeTo(a)) IncomingFarAt = time;
        }

        bool MTankHurry(out double s)
        {
            s = 1.0;
            if (!ZWorld.MTankHurryDeviation || Kind != ZKind.Vehicle || Type != ZMTank.Type || Owner == Team.Neutral || Drivers.Count == 0 || Waypoints.Count == 0) return false;
            var m = Waypoints[0].mode;
            if (m != WpMode.Dodge && m != WpMode.Attack && m != WpMode.Agro) return false;     // fighting / sidestepping only, never a plain Move
            double t = World.Time, w = ZWorld.MTankHurrySec;
            if (t - IncomingAt > w && t - LastEnemyHit > w) return false;                        // not under fire
            s = t - IncomingFarAt <= w ? ZWorld.MTankHurryFarMul : ZWorld.MTankHurryMul;
            return true;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV150 switch (code switch; ZGame: with DV145, off in 'Zod exact'; headless default off).</summary>
        public static bool MTankHurryDeviation = false;
        public static double MTankHurryMul = 1.10, MTankHurryFarMul = 1.15, MTankHurrySec = 3.0;

        /// <summary>The parity report and the older counted checks: no DV150 while this lives (a Play session leaves the switch on).</summary>
        public static IDisposable MTankHurryOff() => new MTankHurryOffHold();
        sealed class MTankHurryOffHold : IDisposable
        {
            readonly bool was = MTankHurryDeviation;
            public MTankHurryOffHold() { MTankHurryDeviation = false; }
            public void Dispose() { MTankHurryDeviation = was; }
        }

        /// <summary>DV150: a shell launched at a medium tank of another team is noted on it (ZWorld.AddMissile: every shell source).</summary>
        void NoteIncoming(ZMissile m)
        {
            if (!MTankHurryDeviation || m == null || m.debris) return;
            var t = Get(m.targetId); if (t == null || t.Type != ZMTank.Type) return;
            var a = GetAny(m.attackerId); if (a == null || a.Owner == t.Owner) return;
            t.NoteIncoming(a, Time);
        }
    }
}
