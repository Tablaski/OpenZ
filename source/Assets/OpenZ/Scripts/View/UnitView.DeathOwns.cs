using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only. The death owns the body from
    /// the death event; a robot that lives reacts exactly as before. The owner's NB3 rule stays (BlastWave.cs
    /// every death keeps its hold and is thrown when its drawn round / rocket arrives.
    /// What really met on one body (read from the code; kit vehicle_exit_gun's freeze in Sync never runs then: Update / Sync / LateUpdate all
    /// return for a robot the rules have dead, View/UnitView.cs Update, LateUpdate's hold line and Sync's first line):
    /// <list type="bullet">
    /// <item>a knockdown still in its blast arc when a later rocket kills him, with a hold for that rocket's drawn landing (an MLRS ripple:
    /// BlastWave, up to 2.5 s + 0.5 s): nothing moved the body any more, so he HUNG IN THE AIR for the whole hold and was then thrown from
    /// there. vehicle_exit_gun's DeathWinsNow already set the freeze point at the arc's landing (reactFreezeT), but only Sync advanced to it,
    /// and Sync does not run in a held death. Now (<see cref="DeathHoldLands"/>, on) LateUpdate's hold line calls <see cref="DeathHoldLand"/>:
    /// the frozen knockdown runs its own arc on to reactFreezeT (ApplyReaction's kind-3 arc from reactAnchor to reactLand and the lie pose: no
    /// sim position used, no new push) with its clips (ReactAnim), then lies still. Die then runs at the rocket's moment, exactly as for a robot
    /// that was already lying: the rag doll starts from the ground with only the death's own push.</item>
    /// <item>Die called EndReaction after it had switched the gun grip off (hold.Suspended = true); for a knockdown EndReaction switched it ON
    /// again, so WeaponHold (order 80, LateUpdate) posed the gun and the left arm's IK on every dying body whose Animator stays on (Zod's
    /// flight when the rag doll cap is full, the death clip, the melt): the arms pulled to the rifle while the death clip / flight played.
    /// Now Die switches the grip off again right after EndReaction (<see cref="DeathWinsGripOff"/>).</item>
    /// </list>
    /// </summary>
    public partial class UnitView
    {
        /// <summary>Off = a knockdown frozen in its arc hangs in the air through his death's hold (as before this kit).</summary>
        public static bool DeathHoldLands = true;
        /// <summary>Proof counters: frames a held knockdown's arc was run on during his hold; of them the frame it reached the ground (the freeze
        /// point); grips switched off again after Die's EndReaction.</summary>
        public static int DeathHoldArcFrames, DeathHoldLanded, DeathGripOffs;

        /// <summary>LateUpdate, while a death is held (its hold line, before its return): a knockdown frozen in its blast arc at his death event
        /// (vehicle_exit_gun's DeathWinsNow) runs the arc on to its freeze point, the landing (reactFreezeT), at the frame's own step, with its
        /// clips. Nothing else: a frozen stagger / flinch / lying knockdown is already at its freeze point (it stays still); a frame Sync ran
        /// (it then took the step itself through ReactStep) is skipped; the get-up is never reached (reactFreezeT &lt; lieEnd).</summary>
        void DeathHoldLand(float dt)
        {
            if (!DeathWinsOn || !DeathHoldLands || !reactFrozen || reactKind != 3 || death != Death.None || syncedFrame == Time.frameCount) return;
            if (reactT < 0f || reactT >= reactFreezeT - 1e-4f || dt <= 0f) return;
            float step = Mathf.Min(dt, reactFreezeT - reactT);
            ApplyReaction(step);                                                                         // kind 3 before lieEnd: reactAnchor -> reactLand and the lie pose only
            ReactAnim(step);                                                                             // blast_airborne, then death_back as the body lands (the grip stays suspended)
            DeathHoldArcFrames++; if (reactT >= reactFreezeT - 1e-4f) DeathHoldLanded++;
        }

        /// <summary>Die, right after EndReaction: no gun grip / left-arm IK on a dying body (EndReaction of a knockdown had switched it on).</summary>
        void DeathWinsGripOff()
        {
            if (!DeathWinsOn || !hold || hold.Suspended) return;
            hold.Suspended = true; DeathGripOffs++;
        }

        /// <summary>Proof: in a knockdown's blast arc now (in the air); the arc's time left (s, 0 when not in it); the gun grip on (WeaponHold
        /// would pose the arms this frame); the Animator on (a dying body it still poses: the death clip, the melt, Zod's flight).</summary>
        public bool InKnockdownArc => reactKind == 3 && reactT >= 0f && reactT < flyEnd;
        public float KnockArcLeft => InKnockdownArc ? flyEnd - reactT : 0f;
        public bool GripPosing => hold && hold.enabled && !hold.Suspended && (!anim || anim.enabled);
        public bool AnimOn => anim && anim.enabled;

        /// <summary>Proof: every counter of this file back to 0.</summary>
        public static void DeathOwnsStatsReset() { DeathHoldArcFrames = DeathHoldLanded = DeathGripOffs = 0; }
    }
}
