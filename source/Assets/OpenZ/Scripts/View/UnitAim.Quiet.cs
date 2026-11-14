using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.4 vehicle_exit_jump, part B (View/OzVehicleQuiet.cs): a vehicle whose driver is gone (EXIT DRIVER, sniped, killed inside: nobody inside)
    /// stops its turret, gun and torso where they are from the frame the view sees it empty. Before: UnitAim kept slewing onto the shots it still
    /// held (up to 1.5 s for a shell) and onto the target it had predicted (rescanned every 0.25 s), and replayed those held shots as it got on
    /// fired them before the click; fix round 1: the others are dropped, so the turret never snaps and no shell leaves a barrel that points
    /// elsewhere; fix round 2: nor are their impacts), the turret's and the gun's slew stops (no speed carried on), the barrel spin stops (no spin-down), the traverse loop stops; every
    /// frame after: no aim, no idle scan, no recoil kick, the turret keeps its angle on the hull (it turns only with the hull, never on its own),
    /// the recoil slides home and an MLRS rack reloads as before. Crewed again: the normal aim from the next frame. Robots: untouched.
    /// </summary>
    public partial class UnitAim
    {
        int quietFrame = -1;
        /// <summary>Proof: frames this unit's gun stood quiet.</summary>
        public int QuietFrames { get; private set; }

        /// <summary>LateUpdate, right after its dying line: true = the vehicle is empty, its gun stands still this frame (the rest of the aim is skipped).</summary>
        bool QuietStep(float dt)
        {
            if (robot || obj == null || !OzVehicleQuiet.On || OzVehicleQuiet.EmptiedFrame(obj) < 0) { quietFrame = -1; return false; }
            if (quietFrame < 0)
            {
                quietFrame = Time.frameCount; OzVehicleQuiet.TurretsFrozen++;
                if (held.Count > 0)
                {   // fired by the rules before the click (fix round 1): drawn now only when the gun is already on it (no snap, no shell out of a
                    // barrel that points elsewhere); the others are dropped, their impacts too (fix round 2, review)
                    var now = held.ToArray(); held.Clear();
                    foreach (var h in now)
                    {
                        float Tn = Mathf.Max(0.05f, h.T - (Time.time - h.at) * Speed);
                        bool on = ErrorTo(h.point, h.ballistic, Tn, h.g, h.grenade) <= Mathf.Min(slew.tol, OzVehicleQuiet.QuietDrawDeg);
                        OzVehicleQuiet.NoteHeld(obj, on);
                        if (on) Replay(h); else OzVehicleQuiet.DropImpact(obj, h.e);                                // fix round 2 (review): its impact is not drawn either
                    }
                }
                holdUntil = 0f; predicted = null; nextScan = 0.25f; lastShot = -10f;
                if (yawJ != null) yawJ.vel = 0f;
                if (pitchJ != null) pitchJ.vel = 0f;
                spinSpeed = 0f;
                OzSoundV3.BarrelSpin(obj, false, 0f, slew.spinMax > 0 ? slew.spinMax : 900f, spin ? spin.position : transform.position);   // the spin's state off, no spin-down sound
                OzSoundV3.SlewEnd(obj);                                                                         // the traverse loop off
            }
            QuietFrames++;
            var hf = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (hf.sqrMagnitude > 1e-6f) { lastHullYaw = Mathf.Atan2(hf.x, hf.z) * Mathf.Rad2Deg; hullYawInit = true; }   // no world-bearing hold: the turret stays put on the hull
            AnimateGun(dt);                                                                                    // the recoil slides home, an MLRS rack reloads; the spin stays at 0
            if (HasRig) Pose(yawJ != null ? yawJ.ang : 0f, pitchJ != null ? pitchJ.ang : 0f, 0f);
            AimError = 180f;
            return true;
        }
    }
}
