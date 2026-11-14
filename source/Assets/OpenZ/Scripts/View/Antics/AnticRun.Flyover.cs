// OpenZ (GPL-3.0)
// standing robot into the phone or the rocket-fuel drink (AnticDirector.FlyoverAll.cs). Three things a run does for it:
//  * PRE-ROLL (FlyoverPreroll): a robot's first antic under the hold begins part-way through, at a random point of a random clip segment
//    before the last ones (never inside the can's crush and throw, never in the phone's put-away): the weapon is simply away and the prop
//    simply in the hand, no pop and no sound (nothing "happens": the camera finds a robot already at it), the events up to that point are
//    done; from there the antic runs as always, its beeps, glugs, crush, throw and put-away with their pops and sounds. So a hundred
//    robots start in the same third of a second without one pose in step and without a burst of pops.
//  * CHAIN (FlyoverChain / InheritStow / FlyoverKeep): the next antic of a robot follows on the frame the last one ends; the weapon the
//    last one stowed stays away and the next one takes it over (no rifle popping back for a tenth of a second between a phone and a can).
//    A run that keeps the weapon and gets no successor (a refusal, the end of the hold) gives it back (FlyoverUnstow).
//  * The end: the director cancels the flyover antics before the hand-back through the antic's own cancel (props off, weapon back, as on
//    an order); FlyoverKeep keeps nothing once the end has come (AnticDirector.FlyoverChainsNow).
// View only.
using UnityEngine;

namespace OpenZ
{
    public sealed partial class AnticRun
    {
        bool preroll; int preStep; float preT;
        /// <summary>Started by the flyover drive (AnticDirector.FlyoverAll.cs).</summary>
        public bool Flyover { get; private set; }
        /// <summary>This run holds its robot's stowed weapon: after its end for the next antic of the chain, or before its start (inherited).</summary>
        public bool KeepsWeapon { get; private set; }
        /// <summary>Checks: where the pre-roll began (segment, seconds into it); -1 = not pre-rolled.</summary>
        public int PrerollStep => preroll ? preStep : -1;
        public float PrerollSeconds => preroll ? preT : -1f;

        /// <summary>The first antic of a robot under the hold: begins up to `depth` (0..1) of the way into a random segment before the last ones.</summary>
        public void FlyoverPreroll(float depth)
        {
            Flyover = true;
            int last = Kind == AnticKind.Drink ? steps.Count - 3 : Kind == AnticKind.Phone ? steps.Count - 2 : -1;   // the last sip / the last use-only segment
            if (last < 0) return;
            preStep = Random.Range(0, last + 1);
            preT = Random.value * Mathf.Clamp01(depth) * steps[preStep].len;
            preroll = true;
        }

        /// <summary>A later antic of a robot's chain under the hold: begins at its start, with its pops and sounds.</summary>
        public void FlyoverChain() { Flyover = true; }

        /// <summary>Tick's first step: enters the pre-rolled clip; true when this run is pre-rolled (else the normal first clip is entered).</summary>
        bool PrerollStart(Animator anim)
        {
            if (!preroll || preStep < 0 || preStep >= steps.Count) return false;
            var kit = AnticDirector.Kit;
            quiet = true;
            try
            {
                Stow();                                                                              // the weapon simply away (quiet: no pop)
                if (Kind == AnticKind.Drink && kit && !can) can = HandProp(kit.can, "AnticFuelCan", true, AnticSockets.CanScale, false);
                if (Kind == AnticKind.Phone && kit && !phone && (preStep > 0 || preT >= F(2))) phone = HandProp(kit.phone, "AnticPhone", true, AnticSockets.PhoneScale, true);
            }
            finally { quiet = false; }
            while (evNext < evs.Count && (evs[evNext].step < preStep || (evs[evNext].step == preStep && evs[evNext].t <= preT))) evNext++;   // done quietly
            step = preStep; stepT = preT;
            if (anim) anim.CrossFadeInFixedTime(steps[preStep].state, FirstFade(0.15f), 0, steps[preStep].off + preT);
            Note($"flyover: begins {preT:F2} s into {steps[preStep].state} (segment {preStep + 1} of {steps.Count}), the prop in the hand without a pop");
            return true;
        }

        /// <summary>The chain: the weapon the last antic left stowed is this one's (its own Stow finds it in place).</summary>
        public void InheritStow(AnticRun prev)
        {
            if (prev == null || !prev.KeepsWeapon) return;
            if (!prev.weapon) { prev.KeepsWeapon = false; return; }
            weapon = prev.weapon; weaponScale = prev.weaponScale; weaponHidden = prev.weaponHidden;
            weaponOff.Clear(); weaponOff.AddRange(prev.weaponOff);
            prev.weapon = null; prev.weaponHidden = false; prev.weaponOff.Clear(); prev.KeepsWeapon = false;
            KeepsWeapon = true;
            Note("flyover: the weapon stays stowed from the last antic");
        }

        /// <summary>Finish: a flyover antic that ends while the hold still chains leaves the weapon stowed (true); else the weapon comes back as always.</summary>
        bool FlyoverKeep()
        {
            bool keep = Flyover && AnticDirector.FlyoverChainsNow && weapon && weaponHidden;
            KeepsWeapon = keep;
            return keep;
        }

        /// <summary>No next antic came (a refusal, the end of the hold): the weapon this run kept comes back now.</summary>
        public void FlyoverUnstow()
        {
            if (!KeepsWeapon) return;
            KeepsWeapon = false;
            Unstow(false);
        }
    }
}
