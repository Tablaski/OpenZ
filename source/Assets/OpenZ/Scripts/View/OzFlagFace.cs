// OpenZ (GPL-3.0)
// Numbers only, no Unity type: FlagView.Face.cs feeds it each frame, and the off-line harness runs this very file on plain .NET.
namespace OpenZ
{
    /// <summary>
    /// Where a flag's cloth flies about its pole so that its face is to the camera: the fly end lies across the view (along the view's right on the ground, or its
    /// left), on the side nearer the wind, so every flag on screen agrees.
    /// <list type="bullet">
    /// <item>Dead band: the side changes only once the wind has crossed the view's line by more than Band (the sine of about 17.5 degrees), so a camera that hovers
    /// round the wind's line never makes the cloth flip back and forth.</item>
    /// <item>A fort's cloth never takes the side toward its central turret (FlagView's 62 degree cone): it takes the other side, which that cone never reaches, and
    /// goes back only once the wanted side is BlockBand clear of the cone.</item>
    /// <item>Eased: the yaw follows on a critically damped spring (Ease s, at most about MaxSpeed deg/s). A side change is one smooth sweep round the side nearer the
    /// wind; a fort's goes round the side away from its turret.</item>
    /// <item>The first step of a flag puts it on its side at once (the level's start, a flag added in the Unit editor).</item>
    /// </list>
    /// Angles in degrees, yaw as FlagView's pivot: the fly end at (cos yaw, 0, -sin yaw).
    /// </summary>
    public static class OzFlagFace
    {
        public const float Ease = 0.3f, MaxSpeed = 300f, Band = 0.3f, BlockBand = 0.12f, SweepFrom = 120f;
        const double Deg = System.Math.PI / 180.0;

        /// <summary>One flag's cloth: its yaw and spring speed, the side it shows (+1 the view's right, -1 its left), set after its first step.</summary>
        public struct State { public float yaw, vel; public int side; public bool set; }

        /// <summary>The side the wind wants, one for every flag (FlagView keeps it, once a frame): +1 the view's right, -1 its left. side: last frame's (0: none yet).
        /// rx, rz: the view's right on the ground (unit). wx, wz: downwind (unit). It changes only once the wind has crossed the view's line by more than Band.</summary>
        public static int WindSide(int side, float rx, float rz, float wx, float wz)
        {
            float d = rx * wx + rz * wz;
            if (side == 0) return d >= 0f ? 1 : -1;
            return side * d < -Band ? -side : side;
        }

        /// <summary>One frame of one flag. windSide: WindSide's of this frame. rx, rz, wx, wz: as WindSide's. bx, bz, bcos: a fort's cone toward its turret (bcos 1 or
        /// more: none). dt: seconds since the last frame (0: the cloth holds). Returns the yaw before the sway.</summary>
        public static float Step(ref State s, int windSide, float rx, float rz, float wx, float wz, float bx, float bz, float bcos, float dt)
        {
            int side = windSide >= 0 ? 1 : -1;
            if (bcos < 1f)
            {   // a fort: the side toward its turret is never taken; the other one is free (the cone is under 90 degrees)
                float into = side * (rx * bx + rz * bz);
                bool other = s.set && s.side == -side;
                if (into > bcos - (other ? BlockBand : 0f)) side = -side;
            }
            float target = Yaw(side * rx, side * rz);
            if (!s.set) { s.set = true; s.side = side; s.yaw = target; s.vel = 0f; return target; }
            s.side = side;
            float delta = DeltaAngle(s.yaw, target);
            if (System.Math.Abs(delta) > SweepFrom)
            {   // a side change: the sweep goes round the side nearer the wind, a fort's round the side away from its turret
                double mid = (s.yaw + delta * 0.5f) * Deg;
                float mx = (float)System.Math.Cos(mid), mz = -(float)System.Math.Sin(mid);
                bool flip = bcos < 1f ? mx * bx + mz * bz > 0f : mx * wx + mz * wz < 0f;
                if (flip) delta -= System.Math.Sign(delta) * 360f;
            }
            if (dt > 0f) s.yaw = Wrap(Damp(s.yaw, s.yaw + delta, ref s.vel, Ease, MaxSpeed, dt));
            return s.yaw;
        }

        /// <summary>The yaw whose fly end points along (dx, 0, dz), as FlagView's windYaw.</summary>
        public static float Yaw(float dx, float dz) => (float)(System.Math.Atan2(-dz, dx) / Deg);

        /// <summary>Mathf.DeltaAngle: the shortest turn from a to b, -180 to 180.</summary>
        public static float DeltaAngle(float a, float b)
        {
            float d = Repeat(b - a, 360f);
            return d > 180f ? d - 360f : d;
        }

        /// <summary>-180 to 180.</summary>
        public static float Wrap(float a) => Repeat(a + 180f, 360f) - 180f;

        static float Repeat(float t, float length)
        {
            float r = t - (float)System.Math.Floor(t / length) * length;
            return r < 0f ? 0f : r > length ? length : r;
        }

        /// <summary>Mathf.SmoothDamp, the same numbers; the caller never passes a dt of 0 (Unity's would divide by it).</summary>
        public static float Damp(float current, float target, ref float vel, float smooth, float maxSpeed, float dt)
        {
            smooth = System.Math.Max(0.0001f, smooth);
            float omega = 2f / smooth, x = omega * dt, exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target, to = target, maxChange = maxSpeed * smooth;
            change = System.Math.Max(-maxChange, System.Math.Min(maxChange, change));
            target = current - change;
            float temp = (vel + omega * change) * dt;
            vel = (vel - omega * temp) * exp;
            float output = target + (change + temp) * exp;
            if (to - current > 0f == output > to) { output = to; vel = 0f; }
            return output;
        }
    }
}
