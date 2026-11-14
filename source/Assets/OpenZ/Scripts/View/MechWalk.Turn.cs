using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// v1 AND v2 (one walk code): the mech turning in place. The drawn hull turns in place at the mech's 140 deg/s
    /// (VehicleSlip "medium"); the stepping walk lifted one foot for a whole slow swing (0.55 s standing) and then kept re-picking that same
    /// foot (a foot that had just landed was free to step again at once, the other one never got its turn), so the other foot stayed locked
    /// while the body turned up to 130 deg away from it: the leg out of reach, the over-stretch guard dragging that foot up to 1.5 m and the
    /// IK leaving its sole 23-30 cm UNDER the ground, every fast turn, both models (measured off the engine with the walk's statement-for-
    ///  * a foot that has just landed is not picked again before the other foot had its turn (<see cref="JustLanded"/>: the feet alternate);
    ///  * turning faster than <see cref="TurnFast"/> deg/s, each step lasts what the turn needs (<see cref="TurnStepDeg"/> deg of turn per
    ///    swing, at least <see cref="TurnSwingMin"/> s; a swing that started slower is quickened), the foot that lags most behind the turn
    ///    steps when its lag passes the lead (the turn's first step at once), and it lands where the turned body will want it, a lead
    ///    (<see cref="TurnLeadShare"/> of one step's turn) ahead: a standing foot stays within about 20 deg of the body, inside the legs'
    ///    reach, never dragged;
    ///  * a swinging sole turns level with the swing's height curve (<see cref="SwingPlate"/>): near lift-off and touch-down it stays along
    ///    the slope (the long v2 sole dipped its toe 3-7 cm into a 12 deg slope).
    /// Measured (the same port, 60 fps, flat, 6 and 12 deg up / down, 8 deg side, a bumpy field; walking, standing and firing with the shot's
    /// dragged; v2 none on flat, 6 deg, side and bumpy ground; on the 12 deg down slope one turn's start leaves a sole 4.2 cm in for 2
    /// frames. Walking straight is as before. Switch: <see cref="TurnSteps"/> (off = the walk as before, statement for statement).
    /// </summary>
    public partial class MechWalk
    {
        /// <summary>The switch: on = the feet keep up with a fast turn in place (and a foot that just landed waits its turn); off = as before.</summary>
        public static bool TurnSteps = true;
        /// <summary>The turn rate (deg/s) from which the turn steps run; the degrees of turn one swing may take; its shortest time (s); the lead
        /// as a share of one step's turn (deg); the turn rate's easing (s).</summary>
        public static float TurnFast = 45f, TurnStepDeg = 24f, TurnSwingMin = 0.16f, TurnLeadShare = 0.5f, TurnRateTau = 0.04f;
        /// <summary>Statistics (the Play checks): steps taken while turning in place fast.</summary>
        public static int TurnStepsTaken;

        float turnRate, lastBodyYaw = float.NaN, turnLead, turnSign = 1f; bool turning; int turnStepsNow;

        /// <summary>Stepping, after the walk's swing time: the body's eased turn rate; turning fast, the swing time the turn needs and the lead.</summary>
        void TurnMode(float bodyYaw, float dt, ref float swing)
        {
            float raw = float.IsNaN(lastBodyYaw) || dt <= 0f ? 0f : Mathf.DeltaAngle(lastBodyYaw, bodyYaw) / dt;
            lastBodyYaw = bodyYaw;
            turnRate += (raw - turnRate) * (1f - Mathf.Exp(-dt / Mathf.Max(1e-3f, TurnRateTau)));
            float w = Mathf.Abs(turnRate);
            turning = TurnSteps && w > TurnFast;
            turnSign = turnRate >= 0f ? 1f : -1f;
            turnLead = 0f;
            if (!turning) { turnStepsNow = 0; return; }
            swing = Mathf.Clamp(TurnStepDeg / w, TurnSwingMin, swing);
            turnLead = TurnLeadShare * w * (swing + 0.06f);
        }

        /// <summary>A foot that has just landed (0.06 s, the other foot's own wait) is not picked again before the other foot had its turn.</summary>
        static bool JustLanded(Leg l) => TurnSteps && l.plantedFor <= 0.06f;

        /// <summary>The pick: turning fast, a foot needs a step when its lag behind the turn passes the lead (the turn's first step at once), or
        /// it is out of reach; the one lagging most steps first.</summary>
        void TurnNeed(Leg l, float bodyYaw, float hipDist, float reachH, bool rollOut, ref bool need, ref float score)
        {
            if (!turning) return;
            float lag = turnSign * Mathf.DeltaAngle(l.plantYaw, bodyYaw);
            need = lag > (turnStepsNow > 0 ? turnLead : 0f) || hipDist > reachH || rollOut;
            score = lag / 90f + (hipDist > reachH ? 2f : 0f) + (rollOut ? 1f : 0f);
        }

        /// <summary>The pick was made: one more step of this turn.</summary>
        void TurnPicked() { if (turning) { turnStepsNow++; TurnStepsTaken++; } }

        /// <summary>A swing that started slower is quickened to the turn's pace (its arc and progress kept).</summary>
        void TurnQuicken(Leg l, float swing) { if (turning && l.swingDur > swing) l.swingDur = swing; }

        /// <summary>Turning fast: the home spot turned about the body to where the body will be at landing, plus the lead; returns that turn (deg,
        /// 0 when not turning) for the foot's yaw.</summary>
        float TurnAhead(Leg l, float s, ref Vector3 home)
        {
            if (!turning) return 0f;
            float ahead = turnRate * (1f - s) * l.swingDur + turnSign * turnLead;
            var c = body.position;
            home = c + Quaternion.AngleAxis(ahead, Vector3.up) * (home - c);
            return ahead;
        }

        /// <summary>Solve: how far a swinging sole has turned from the ground's slope to level, along the swing (0 at lift-off and touch-down).
        /// With the switch: the swing's height curve (4 s (1 - s))^2; off: sin(pi s) as before.</summary>
        static float SwingPlate(float swingT)
        {
            float s = Mathf.Clamp01(swingT);
            if (!TurnSteps) return Mathf.Sin(Mathf.PI * s);
            float u = 4f * s * (1f - s);
            return u * u;
        }
    }
}
