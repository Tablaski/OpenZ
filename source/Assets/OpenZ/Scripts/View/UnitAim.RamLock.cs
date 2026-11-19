using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 quick fix round 14d (gate B10-turret): a vehicle a Mammoth has rammed (the rules' RamDoomed, from the contact) keeps its turret
    /// and gun locked on its hull for the whole lift, flip and landing, until its death is drawn. View only (the rules never read a turret).
    /// Before: DV188 held the aim goal, but the gun's world-bearing hold (motion_v1) and the fight layer (UnitAim.Combat.cs) still worked in
    /// world heading, measured from the hull's and the barrels' forward projected on the ground; while the hull turns over, those projections
    /// swing through 180 deg and vanish, so the turret's heading jumped 90-180 deg from one frame to the next and the barrel swung into the
    /// the others dropped (no snap; as UnitAim.Quiet.cs), the slews stop (no speed carried on), the traverse loop stops; every frame after:
    /// no aim, no world-bearing hold, no fight layer, the turret and the gun keep their angles on the hull (they turn only with it), the
    /// recoil slides home. Robots: untouched. Switch <see cref="RamLockOn"/> (proof only).
    /// </summary>
    public partial class UnitAim
    {
        public static bool RamLockOn = true;
        /// <summary>Proof: frames a doomed vehicle's turret stood locked on its hull; doomed vehicles locked.</summary>
        public static int RamLockFrames, RamLocks;
        bool ramLocked;

        /// <summary>LateUpdate, right after the quiet step: true = this vehicle is doomed by a ram, its turret stays locked this frame (the rest of
        /// the aim is skipped).</summary>
        bool RamLockStep(float dt)
        {
            if (!RamLockOn || robot || obj == null || (obj.Kind != ZKind.Vehicle && !OzRamStill.Holds(obj) /* 1.5 DV195: a crushed gun too */) || !obj.RamDoomed) { ramLocked = false; return false; }
            if (!ramLocked)
            {
                ramLocked = true; RamLocks++;
                if (held.Count > 0)
                {   // fired by the rules before the doom: drawn now only when the gun is already on it (no snap); the others are dropped, their impacts too
                    var now = held.ToArray(); held.Clear();
                    foreach (var h in now)
                    {
                        float Tn = Mathf.Max(0.05f, h.T - (Time.time - h.at) * Speed);
                        if (ErrorTo(h.point, h.ballistic, Tn, h.g, h.grenade) <= slew.tol) Replay(h); else OzVehicleQuiet.DropImpact(obj, h.e);
                    }
                }
                holdUntil = 0f; predicted = null; nextScan = 0.25f;
                if (yawJ != null) yawJ.vel = 0f;
                if (pitchJ != null) pitchJ.vel = 0f;
                OzSoundV3.SlewEnd(obj);                                                                            // the traverse loop off
            }
            RamLockFrames++;
            var hf = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (hf.sqrMagnitude > 1e-6f) { lastHullYaw = Mathf.Atan2(hf.x, hf.z) * Mathf.Rad2Deg; hullYawInit = true; }   // no world-bearing hold
            AnimateGun(dt);                                                                                        // the recoil slides home
            if (HasRig) Pose(yawJ != null ? yawJ.ang : 0f, pitchJ != null ? pitchJ.ang : 0f, 0f);
            AimError = 180f;
            return true;
        }
    }
}
