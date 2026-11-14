using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// <list type="bullet">
    /// <item>Zod's rules never read a direction (zobject.cpp:2102-2213); OpenZ's view turns a robot onto its target and holds the DRAWN shot until its gun points
    /// there (View/UnitAim.cs: robots within <see cref="TolCenti"/> / 100 deg, at <see cref="TurnCentiPerTick"/> x 100 / 100 deg/s). EvilZod's shot (DV161) left the rules
    /// on the tick a target was picked, and his view drew it within 0.1 s whatever way he faced, so a target behind him got the pellets before he had turned.</item>
    /// <item>Now the rules keep his heading, in hundredths of a degree on the map (0 = east, 9000 = south): every tick it turns toward his target (the dash line in a
    /// dash, the attack target, else the way he walks) by at most <see cref="TurnCentiPerTick"/>, the robots' body turn of the view (720 deg/s). His shot waits until
    /// that heading is within <see cref="TolCenti"/> of the target (10 deg, the robots' aim tolerance of the view). The reload, the damage, the range, the cone and the
    /// dash are DV161's and DV167's: only the moment of a shot that needs a turn moves, by the turn itself.</item>
    /// <item>He starts facing south (<see cref="StartCenti"/>), as his drawn body does when it is made (every unit view is made unturned: its front is the map's +y).</item>
    /// </list>
    /// Determinism: whole numbers only (an integer arctangent from a 65-entry table of whole hundredths of a degree), sim ticks, no random draw, no float. Switch
    /// <see cref="ZWorld.EvilFaceFireDeviation"/> (on; the game never sets it, as the auto dash's): off = the 1.4 rules before this kit, bit for bit (the heading
    /// is still kept, it decides nothing). Without an EvilZod nothing here runs.
    /// </summary>
    public static class ZEvilFace
    {
        /// <summary>A whole turn, in hundredths of a degree.</summary>
        public const int Full = 36000;
        /// <summary>The robots' aim tolerance of the view (View/UnitAim.cs SlewFor: robots tol = 10 deg).</summary>
        public const int TolCenti = 1000;
        /// <summary>The robots' body turn of the view (View/UnitAim.cs SlewFor: robots yawSpeed = 720 deg/s), per 10 ms tick.</summary>
        public const int TurnCentiPerTick = 720;
        /// <summary>His heading when he is made: south (map +y), the front of an unturned unit view.</summary>
        public const int StartCenti = 9000;
        /// <summary>Walking: a new heading from every StepPx of his walk; a jump farther than JumpPx in one tick (out of the shuttle, a teleport) is no walk.</summary>
        public const int StepPx = 3, JumpPx = 24;

        /// <summary>atan(i / 64) for i = 0..64, whole hundredths of a degree (0 .. 4500).</summary>
        static readonly int[] Atan64 =
        {
            0, 90, 179, 268, 358, 447, 536, 624, 713, 800, 888, 975, 1062, 1148, 1234, 1319, 1404, 1488, 1571, 1653, 1735, 1817, 1897, 1977, 2056, 2134,
            2211, 2287, 2363, 2438, 2511, 2584, 2657, 2728, 2798, 2867, 2936, 3003, 3070, 3136, 3201, 3264, 3327, 3390, 3451, 3511, 3571, 3629, 3687, 3744,
            3800, 3855, 3909, 3963, 4016, 4067, 4119, 4169, 4218, 4267, 4315, 4363, 4409, 4455, 4500,
        };

        /// <summary>atan(small / big) for 0 &lt;= small &lt;= big, big &gt; 0: whole hundredths of a degree, 0 .. 4500.</summary>
        static int AtanRatio(long small, long big)
        {
            long num = small * 64, i = num / big, r = num % big;
            if (i >= 64) return Atan64[64];
            return (int)(Atan64[i] + (Atan64[i + 1] - Atan64[i]) * r / big);
        }

        /// <summary>The bearing of (dx, dy) on the map, 0 .. 35999 (0 = east, 9000 = south); -1 for (0, 0). Within 0.02 deg of the true angle.</summary>
        public static int Bearing(long dx, long dy)
        {
            if (dx == 0 && dy == 0) return -1;
            long ax = Math.Abs(dx), ay = Math.Abs(dy);
            int a = ax >= ay ? AtanRatio(ay, ax) : 9000 - AtanRatio(ax, ay);
            if (dx >= 0 && dy >= 0) return a;
            if (dx < 0 && dy >= 0) return 18000 - a;
            if (dx < 0) return 18000 + a;
            return a == 0 ? 0 : Full - a;
        }

        /// <summary>The signed shortest turn from a to b, -18000 .. 17999.</summary>
        public static int Diff(int a, int b) => ((b - a) % Full + Full + 18000) % Full - 18000;
    }

    public partial class ZObject
    {
        int evilFace = ZEvilFace.StartCenti, evilFaceAtX, evilFaceAtY, evilFaceWait, evilFaceShotWait; bool evilFaceAt;
        int evilFaceShots, evilFaceShotHead, evilFaceShotBear = -1, evilFaceShotOff;                    // fix round 1: the rules' own note of his last shot (the Play check reads it)

        /// <summary>DV174: his heading now, hundredths of a degree on the map (0 = east, 9000 = south).</summary>
        public int EvilFacing => evilFace;
        /// <summary>DV174: ticks his last shot waited for his turn (0: he faced the target when the shot was ready).</summary>
        public int EvilFaceShotWaitTicks => evilFaceShotWait;
        /// <summary>DV174 (fix round 1), checks only: his shots so far, and at the last one (noted on its own tick, before anything else moves) his heading, the
        /// target's bearing (-1: right on top of him) and the angle between, all in hundredths of a degree. Noted with the rule on and off; it decides nothing.</summary>
        public int EvilFaceShotCount => evilFaceShots;
        public int EvilFaceShotHeading => evilFaceShotHead;
        public int EvilFaceShotBearing => evilFaceShotBear;
        public int EvilFaceShotOff => evilFaceShotOff;

        /// <summary>DV174, checks only: he faces this way now (hundredths of a degree on the map), as a check that stands him facing its targets.</summary>
        public void EvilFaceTo(int centi) { evilFace = (centi % ZEvilFace.Full + ZEvilFace.Full) % ZEvilFace.Full; }

        /// <summary>EvilStep, first thing every tick of his: the heading turns toward the dash line, the attack target, else the way he walks.</summary>
        void EvilFaceStep()
        {
            int want = -1;
            if (AttackObject == null) evilFaceWait = 0;
            if (evilPhase != ZEvilPhase.None) want = ZEvilFace.Bearing(EvilDashToX - EvilDashFromX, EvilDashToY - EvilDashFromY);
            else if (AttackObject != null && !AttackObject.Destroyed) want = ZEvilFace.Bearing(AttackObject.CenterX - CenterX, AttackObject.CenterY - CenterY);
            long mx = CenterX - evilFaceAtX, my = CenterY - evilFaceAtY, m2 = mx * mx + my * my;
            if (!evilFaceAt || !IsMoving || m2 > (long)ZEvilFace.JumpPx * ZEvilFace.JumpPx) { evilFaceAt = true; evilFaceAtX = CenterX; evilFaceAtY = CenterY; }
            else if (m2 >= (long)ZEvilFace.StepPx * ZEvilFace.StepPx) { if (want < 0) want = ZEvilFace.Bearing(mx, my); evilFaceAtX = CenterX; evilFaceAtY = CenterY; }
            if (want < 0) return;
            int d = ZEvilFace.Diff(evilFace, want);
            if (d > ZEvilFace.TurnCentiPerTick) d = ZEvilFace.TurnCentiPerTick; else if (d < -ZEvilFace.TurnCentiPerTick) d = -ZEvilFace.TurnCentiPerTick;
            evilFace = (evilFace + d + ZEvilFace.Full) % ZEvilFace.Full;
        }

        /// <summary>EvilFire, the shot ready and the target in range: true = he faces it (within the tolerance) and fires; false = the shot waits for his turn.</summary>
        bool EvilFaceReady(ZObject t)
        {
            int b = ZEvilFace.Bearing(t.CenterX - CenterX, t.CenterY - CenterY);
            int off = b < 0 ? 0 : Math.Abs(ZEvilFace.Diff(evilFace, b));
            if (!ZWorld.EvilFaceFireDeviation || off <= ZEvilFace.TolCenti)
            {
                evilFaceShotWait = evilFaceWait; evilFaceWait = 0;
                evilFaceShots++; evilFaceShotHead = evilFace; evilFaceShotBear = b; evilFaceShotOff = off;   // EvilFire fires on every true: this is the shot's own note
                return true;
            }
            evilFaceWait++; World.EvilFaceWaits++;
            return false;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV174 switch (Sim/ZEvilZod.Face.cs): his shot waits for his turn onto the target. On; the game never sets it. Off: the shot as before this kit.</summary>
        public static bool EvilFaceFireDeviation = true;
        /// <summary>DV174 statistics of this world (checks): ticks a ready shot of his waited for his turn.</summary>
        public int EvilFaceWaits;
    }
}
