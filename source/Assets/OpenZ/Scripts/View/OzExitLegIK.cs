using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only.
    /// <para>
    /// humanoid leg muscles are degenerate (RobotLegIK's root cause: a bent knee is stored as a large thigh twist with a shin counter-twist), so
    /// whatever interpolates them swings the leg through that twist. A live robot never shows it: RobotLegIK pins its feet to the clips' own
    /// smooth foot goals and points its knees to its front. The hatch-exit figure is a clone without a UnitView, so it had no leg IK: the
    /// landing's squat blending into the armed idle as the gun comes back (HatchGunBlend), and the walk's cross-fade after the stand-up, swung
    /// its knees inward and across (knee spacing jumping 0.30 -> 0.71 -> 0.47 m between single frames, the boots crossing by 5 cm).</para>
    /// <para>Now, from the touch to the hand-over to the live robot (the landing, the gun coming up, the walk on to his spot), the figure's
    /// feet go to the clips' own foot goals (blended goals during a cross-fade: a straight glide) and its knees are hinted
    /// <see cref="KneeAhead"/> in front of the hip-foot middle, toward the figure's front (it faces its -z), exactly as the live robots' legs.
    /// Nothing else of the jump changes (the take-off, the flight and the somersault are as before). <see cref="On"/> off = as before.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OzExitLegIK : MonoBehaviour
    {
        /// <summary>N2: the exit figure's legs held by IK from the touch on (on).</summary>
        public static bool On = true;
        /// <summary>The knee hint in front of the hip-foot middle (human scale units, as RobotLegIK.KneeAhead).</summary>
        public static float KneeAhead = 0.5f;
        /// <summary>Proof: IK passes that held an exit figure's legs.</summary>
        public static int Passes;

        Animator anim; Transform front; Transform hipL, hipR; int markFrame = -10;
        static readonly AvatarIKGoal[] Goals = { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot };
        static readonly AvatarIKHint[] Hints = { AvatarIKHint.LeftKnee, AvatarIKHint.RightKnee };

        /// <summary>BoardingView (the hatch exit from the touch on, every frame, before the figure's Animator is evaluated): hold this figure's
        /// legs this frame and the next (the walk's own Animator update comes before the next mark). frontOf = the figure's root (its front = -z).</summary>
        public static void Mark(Animator a, Transform frontOf)
        {
            if (!On || !a || !a.avatar || !a.avatar.isHuman || !a.isActiveAndEnabled) return;
            var k = a.GetComponent<OzExitLegIK>(); if (!k) k = a.gameObject.AddComponent<OzExitLegIK>();
            k.anim = a; k.front = frontOf ? frontOf : a.transform; k.markFrame = Time.frameCount;
            if (!k.hipL) { k.hipL = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg); k.hipR = a.GetBoneTransform(HumanBodyBones.RightUpperLeg); }
        }

        void OnAnimatorIK(int layer)
        {
            if (layer != 0 || !anim) return;
            bool on = On && Time.frameCount - markFrame <= 1 && anim.isHuman && hipL && hipR;
            if (!on) { for (int i = 0; i < 2; i++) { anim.SetIKPositionWeight(Goals[i], 0f); anim.SetIKRotationWeight(Goals[i], 0f); anim.SetIKHintPositionWeight(Hints[i], 0f); } return; }
            var fwd = front ? -front.forward : -transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            fwd.Normalize();
            float ahead = KneeAhead * Mathf.Max(0.1f, anim.humanScale);
            for (int i = 0; i < 2; i++)
            {
                var goal = Goals[i];
                var p = anim.GetIKPosition(goal); var q = anim.GetIKRotation(goal);                       // the clips' own (blended) foot goal
                anim.SetIKPositionWeight(goal, 1f); anim.SetIKRotationWeight(goal, 1f);
                anim.SetIKPosition(goal, p); anim.SetIKRotation(goal, q);
                var hip = (i == 0 ? hipL : hipR).position;
                anim.SetIKHintPositionWeight(Hints[i], 1f);
                anim.SetIKHintPosition(Hints[i], (hip + p) * 0.5f + fwd * ahead);                         // the knee toward his front: never flipped by the thigh twist
            }
            Passes++;
        }
    }
}
