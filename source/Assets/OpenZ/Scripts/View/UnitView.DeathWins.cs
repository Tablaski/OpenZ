using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only.
    /// The two systems: the hit reaction (DV19, UnitView.React / ApplyReaction: a flinch, a stagger or a knockdown that offsets and turns the
    /// drawn root every frame, started by HitReactions.OnBlast) and the death (UnitView.Die: the rag doll, Zod's flight, a pop or a melt). The
    /// death is thrown on the frame its DRAWN round / rocket arrives (H0 round 3, GP3; an MLRS ripple rocket's landing up to 2.5 s later:
    /// BlastWave.RobotDelay), held until then (HoldForDeath, CombatView.RobotDeathLater). Where they met: (1) a robot still wobbling from an
    /// earlier hit (typical in an MLRS ripple) and killed by a later one went on wobbling through that hold (Update's Sync still runs
    /// ApplyReaction; a reaction still waiting for its blast front even STARTED then), and the rag doll was then thrown from a moving pose;
    /// (2) React could start a reaction on a robot the rules have killed (only death != None was asked).
    /// Now the death wins (<see cref="DeathWinsOn"/>, on):
    /// <list type="bullet">
    /// <item>no reaction is started on a robot the rules have dead (KillAt / Destroyed) or whose death is held for its drawn moment;</item>
    /// <item>at his death event (CombatView.Death, <see cref="DeathWinsNow"/>) a reaction still waiting for its blast front is dropped; one being
    /// drawn is FROZEN where it is (<see cref="ReactStep"/>: it no longer advances, his drawn root keeps that pose and stays still; a knockdown
    /// still in its blast arc first finishes the arc to the ground, so nobody hangs in the air for the hold); his death keeps its hold and is
    /// thrown at its drawn moment as before (review 2: never earlier), and Die ends the frozen reaction (EndReaction) before the death's own
    /// push: the rag doll gets only the death's impulse (no wobble velocity is added: one push on the body, never two);</item>
    /// <item>a robot that survives the hit reacts exactly as before.</item>
    /// </list>
    /// </summary>
    public partial class UnitView
    {
        /// <summary>Off = as before (a reaction runs on through a held death and may start on a robot dead in the rules).</summary>
        public static bool DeathWinsOn = true;
        /// <summary>Proof counters: reactions refused on a robot dead in the rules / held for its death; waiting reactions dropped at the death;
        /// running reactions frozen at the death event; of them thrown (Die), of those with a hold for a drawn round / ripple rocket, of those
        /// thrown IN the death event's own frame although a hold was asked (must stay 0: the early throw of review 2); frames a frozen reaction
        /// was drawn still (its step 0); reactions started.</summary>
        public static int DeathWinsRefused, DeathWinsDropped, DeathWinsFrozen, DeathWinsFrozenThrown, DeathWinsFrozenHeld, DeathWinsFrozenEarly, ReactFrozenFrames, ReactsStarted;

        /// <summary>Proof: one frozen reaction and its death (the last 64): the robot, the reaction's kind, the death event's frame and time, the
        /// hold its event asked (s: the drawn round's / ripple rocket's wait; 0 = none), the throw's frame and time.</summary>
        public struct FrozenDeath { public int id, kind, eventFrame, thrownFrame; public float eventAt, wave, thrownAt; }
        public static readonly System.Collections.Generic.List<FrozenDeath> DeathWinsLog = new System.Collections.Generic.List<FrozenDeath>();

        bool reactFrozen; float reactFreezeT, frozenEventAt; int frozenEventFrame;

        /// <summary>The rules have him dead, or his death waits for its drawn moment.</summary>
        bool DeadForReaction => Obj != null && (Obj.Destroyed || Obj.KillAt >= 0) || deathHoldUntil > Time.time;

        /// <summary>React's guard: true = no reaction starts (the death wins). Counts the started ones otherwise (the check's "a survivor still
        /// reacts").</summary>
        bool DeathWinsRefuses()
        {
            if (DeathWinsOn && DeadForReaction) { DeathWinsRefused++; return true; }
            reactFrozen = false;
            ReactsStarted++;
            return false;
        }

        /// <summary>Sync: the step the hit reaction takes this frame (ApplyReaction's dt): dt as before; once frozen at his death event 0 (the
        /// reaction's own offset and turn kept, nothing advances: his body still); a knockdown frozen in its blast arc runs on to its landing
        /// first (never held in the air). Safety: frozen but no longer dead nor held (never seen): it runs on.</summary>
        float ReactStep(float dt)
        {
            if (!reactFrozen) return dt;
            if (!DeadForReaction && death == Death.None) { reactFrozen = false; return dt; }
            if (reactT < reactFreezeT - 1e-4f) return Mathf.Min(dt, reactFreezeT - reactT);
            ReactFrozenFrames++;
            return 0f;
        }

        /// <summary>CombatView.Death, for this robot's death event, BEFORE its hold (which stays): a reaction still waiting for its front is
        /// dropped; one being drawn is frozen where it is (a knockdown in its blast arc: at its landing).</summary>
        public void DeathWinsNow()
        {
            if (!DeathWinsOn || reactKind == 0 || death != Death.None) return;
            if (reactT < 0f) { EndReaction(); DeathWinsDropped++; return; }
            if (reactFrozen) return;
            reactFrozen = true; DeathWinsFrozen++;
            reactFreezeT = reactKind == 3 && reactT < flyEnd ? Mathf.Min(flyEnd + 0.001f, lieEnd - 0.001f) : reactT;
            frozenEventAt = Time.time; frozenEventFrame = Time.frameCount;
        }

        /// <summary>Die, before EndReaction (which ends the frozen reaction, as Die always did): the proof's record of the throw against its
        /// hold. Nothing is added to the death's own push.</summary>
        void DeathWinsEnd()
        {
            if (!reactFrozen) return;
            reactFrozen = false; DeathWinsFrozenThrown++;
            float held = deathHoldUntil - frozenEventAt;                                                  // the hold this death event asked: its wait + 0.5 s (CombatView.Death)
            float wave = held > 0.5f + 0.015f ? held - 0.5f : 0f;
            if (wave > 0f) { DeathWinsFrozenHeld++; if (Time.frameCount == frozenEventFrame) DeathWinsFrozenEarly++; }
            if (DeathWinsLog.Count >= 64) DeathWinsLog.RemoveAt(0);
            DeathWinsLog.Add(new FrozenDeath { id = Obj != null ? Obj.Id : -1, kind = reactKind, eventFrame = frozenEventFrame, eventAt = frozenEventAt, wave = wave, thrownFrame = Time.frameCount, thrownAt = Time.time });
        }

        /// <summary>Proof: a reaction is being drawn now; frozen at his death event and at its freeze point (his body must be still); a reaction
        /// and a death on this robot at once (must never be: Die ends the reaction).</summary>
        public bool ReactingAny => reactKind != 0 && reactT >= 0f;
        public bool ReactFrozenNow => reactFrozen && reactKind != 0 && reactT >= reactFreezeT - 1e-4f;
        public bool ReactAndDeathBoth => reactKind != 0 && death != Death.None;

        /// <summary>Proof: every counter back to 0.</summary>
        public static void DeathWinsStatsReset()
        {
            DeathWinsRefused = DeathWinsDropped = DeathWinsFrozen = DeathWinsFrozenThrown = DeathWinsFrozenHeld = DeathWinsFrozenEarly = ReactFrozenFrames = ReactsStarted = 0;
            DeathWinsLog.Clear();
        }
    }
}
