using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 (the k86c leftover, v1 AND v2 and any later mech model: one walk code, every number read from the model's own legs): a standing
    /// foot must stay where it landed (under 5 cm / 5 deg) also when the drawn body walks fast while it turns. The Play proof after k86c
    /// (mech v2 menu 3, L02) still measured standing feet moving 10-16 cm, and the stance monitor named the two causes: (a) "over-stretch
    /// drag" at 2.8-4.3 m/s: the body outran the standing leg before the other foot had landed, and the walk's guards act only at 0.95 / 0.98
    /// of the reach: at 30 fps one frame of a 4 m/s body is 13 cm, so the far foot was dragged for the last frame or two; (b) "hip roll
    /// limit": turning, the standing foot ended 34-48 deg out of its leg's plane (the hip rolls 30 at most), so the drawn ankle missed its
    /// spot by 6-35 cm, also in a slow turn on the spot with the other foot in a long 0.55 s swing. Both are a TIME problem: a guard must act
    /// before the limit, by how fast the standing leg is closing on it. Now (<see cref="Ahead"/>):
    ///  * THE BUDGET of a standing foot: the time until its hip passes the reach the drag guard keeps (1.05 x the walk's horizontal reach)
    ///    at the hip's speed away from the foot (the body's move, raw and smoothed, plus the body's turn about its centre), or until its leg's
    ///    roll passes <see cref="AheadRoll"/> x MaxRoll at the rate it grows; minus <see cref="AheadFrames"/> frames (a foot lifts the frame
    ///    after the other one landed);
    ///  * WHO STEPS: with both feet down, a foot whose budget is shorter than one swing of the other foot steps now (the one that runs out
    ///    first), without the short landing rest;
    ///  * THE SWING lands within the budget of the foot that carries the body: its arc and its aim kept, only faster (never under the walk's
    ///    own 0.05 s floor);
    ///  * A SWINGING SOLE never goes under the ground under it (<see cref="SwingSoleClear"/>): its sole is lifted to stand on the ground at
    ///    the place and tilt it is drawn at, not only at the two ends of the swing.
    /// Walking at normal speed and standing are as before (the budgets are long: nothing changes). View only. Switches: <see cref="Ahead"/>,
    /// <see cref="SwingSoleClear"/> (off = the walk as before, statement for statement).
    /// </summary>
    public partial class MechWalk
    {
        public static bool Ahead = true, SwingSoleClear = true;
        /// <summary>The share of the drag guard's reach a budget runs to (1 = the guard's own 1.05 x reach), the share of MaxRoll, the frames
        /// kept in hand (a foot lifts one frame after the other landed; a slow frame counts as 1/30 s at least).</summary>
        public static float AheadReach = 1f, AheadRoll = 0.97f, AheadFrames = 1.5f, AheadMinFrameS = 1f / 30f;
        /// <summary>Statistics (the Play checks): steps taken early by their budget, swings landed sooner by a budget, swinging soles lifted.</summary>
        public static int AheadSteps, AheadHurries, SwingSoleLifts;

        float[] k102RollPrev, k102RollRate, k102RollAt; Vector3[] k102Stance;

        float K102Frame(float dt) => AheadFrames * Mathf.Max(dt, AheadMinFrameS);

        /// <summary>The time (s) a standing foot can still carry the body: until its hip passes the drag guard's reach or its leg's roll passes
        /// the limit (infinity = not closing). Reads last frame's solve (the roll) and the body's move this frame.</summary>
        float K102Budget(Leg l, Vector3 rawVel, float reachH)
        {
            if (!l.planted || !body) return float.PositiveInfinity;
            int i = Ix(l);
            if (k102RollPrev == null) { k102RollPrev = new float[2]; k102RollRate = new float[2]; k102RollAt = new float[2]; k102Stance = new Vector3[2]; k102RollAt[0] = k102RollAt[1] = -1f; }
            float roll = Mathf.Abs(l.rollNeed), now = Time.time;
            if (k102Stance[i] != l.stanceStart) { k102Stance[i] = l.stanceStart; k102RollPrev[i] = roll; k102RollRate[i] = 0f; k102RollAt[i] = now; }   // a new stance: no rate yet
            else if (k102RollAt[i] != now)
            {
                float span = now - k102RollAt[i];
                k102RollRate[i] = span > 1e-4f && span < 0.25f ? Mathf.Lerp(k102RollRate[i], (roll - k102RollPrev[i]) / span, 0.5f) : 0f;
                k102RollPrev[i] = roll; k102RollAt[i] = now;
            }
            var hp = l.hip.position; var d = new Vector2(hp.x - l.plant.x, hp.z - l.plant.z); float dist = d.magnitude;
            var r = hp - body.position; float w = turnRate * Mathf.Deg2Rad;
            var spin = new Vector2(w * r.z, -w * r.x);                                                       // the hip's move from the body's turn (yaw + = clockwise from above)
            float close = dist > 1e-4f ? Mathf.Max(Vector2.Dot(new Vector2(rawVel.x, rawVel.z) + spin, d), Vector2.Dot(new Vector2(vel.x, vel.z) + spin, d)) / dist : 0f;
            float lim = AheadReach * 1.05f * reachH;
            float tReach = close > 1e-3f ? (lim - dist) / close : float.PositiveInfinity;
            float tRoll = k102RollRate[i] > 1e-2f ? (AheadRoll * MaxRoll - roll) / k102RollRate[i] : float.PositiveInfinity;   // a roll that does not grow (a side slope) is the walk's own rule (rollOut): no step dance
            return Mathf.Max(0f, Mathf.Min(tReach, tRoll));
        }

        /// <summary>The pick, after the walk's and the turn's own rules: with both feet down, a foot that would run out before the other foot
        /// could make one whole step steps now (the one that runs out first; no landing rest).</summary>
        void K102Order(Leg l, Leg o, Vector3 rawVel, float dt, float swing, float reachH, ref bool need, ref bool otherOk, ref float score)
        {
            if (!Ahead || !l.planted || !o.planted) return;
            float span = swing + K102Frame(dt), bl = K102Budget(l, rawVel, reachH);
            if (bl >= span || K102Budget(o, rawVel, reachH) < bl) return;
            need = true; otherOk = true; score += 3f + (span - bl) / Mathf.Max(1e-3f, span);
            AheadSteps++;
        }

        /// <summary>The hurry pass, for a standing foot l while o swings: o lands within l's budget (its arc kept, only faster).</summary>
        void K102Hurry(Leg l, Leg o, Vector3 rawVel, float dt, float reachH)
        {
            if (!Ahead || !l.planted || o.planted || o.swingT >= 1f) return;
            float budget = K102Budget(l, rawVel, reachH) - K102Frame(dt);
            float rest = 1f - Mathf.Clamp01(o.swingT), left = rest * Mathf.Max(0.05f, o.swingDur);
            if (left <= budget || rest < 1e-3f) return;
            o.swingDur = Mathf.Max(0.05f, budget) / rest;
            AheadHurries++;
        }

        /// <summary>The swing: the lift that stands the sole on the ground under it where and as it is drawn now (its plate turned as the solve
        /// turns it, its yaw eased), when that is more than the lift eased between the two ends.</summary>
        float K102SwingSole(ZGame g, Leg l, float sl, Vector3 xz, float baseY, float e, float yaw)
        {
            if (!SwingSoleClear || !SoleOnGround || soleOff == null || g == null) return sl;
            var n = Vector3.Slerp(l.normal, Vector3.up, SwingPlate(l.swingT));
            float need = LiftAt(Ix(l), g, new Vector3(xz.x, baseY, xz.z), n, Mathf.LerpAngle(l.fromYaw, yaw, e));
            if (need <= sl) return sl;
            SwingSoleLifts++;
            return need;
        }
    }
}
