using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 owner item 158 (the hand gun fires only inside the arm's forward arc): each model's OWN reach for the drawn arm and its gate.
    /// Before, the drawn arm stopped at the rule's arc (<see cref="Sim.ZWorld.HandGunOutDeg"/> 35 outward / <see cref="Sim.ZWorld.HandGunInDeg"/>
    /// 15 inward, + <see cref="ArcSlackDeg"/>) and its gate (a sim shot at a robot further off than that + 12 deg was not drawn) was the
    /// same numbers, measured on the old mech v2 arm (its own gun arm with its own HandGunMuzzle, which reached 38 / 18). Both mechs now
    /// fire from v1's gun arm (MechMuzzle under MechGunRecoil; the mech v2 is v1's model with its own texture), and the rule measures its
    /// arc at a point 1.77 m to the mech's right of its centre, while v1's muzzle sits 0.54 m further forward and the drawn upper body
    /// still slews onto a new rocket target for a few tenths of a second after the rule's look has jumped to it. So a robot well inside
    /// the rule's arc needed a drawn turn past 38 deg (a near robot, a fresh rocket target): the arm stopped at its limit (frames held
    /// at a limit 341 on L17) and the gate counted such shots out of its reach (9, killing rounds, which passed the gate before).
    /// v1's arm really reaches much further without touching the body: measured on the model (k86c tools/arm_reach.py, yaw at five
    /// shoulder pitches: 42 deg inward, 77 deg outward; this kit's tools/arm_reach_grid.py, the turn and the climb together at the same
    /// shoulder pitches: with the climb within +-30 deg it stays clear up to 29 deg inward (42 with no climb) and at least 60 deg outward,
    /// the end of that sweep: <see cref="V1ArmOutDeg"/> / <see cref="V1ArmInDeg"/> / <see cref="V1ArmPitchDeg"/>).
    /// Now, view only (the rule, its arc and every shot unchanged; deterministic):
    ///  * a model that fires from v1's arm turns its arm up to v1's measured reach; a model with its own gun arm (an own HandGunMuzzle)
    ///    keeps the rule's arc + <see cref="ArcSlackDeg"/> and <see cref="PitchMaxDeg"/>, as before;
    ///  * the gate is the same reach, solved on the elbow as the drawn pose is (the barrel's line through the robot), turn and climb:
    ///    a robot the drawn arm cannot point at is never shot at by a drawn round, the killing round included (the robot falls on its
    ///    own time, as a killing round off the barrel already did); the arm still turns toward it.
    /// Counters: <see cref="OutOfReach"/> = drawn shots at a robot out of the drawn arm's reach (0 while <see cref="ReachHold"/> is on),
    /// <see cref="ReachHeld"/> = sim shots not drawn for it, of them <see cref="ReachKillHeld"/> killing rounds.
    /// </summary>
    public sealed partial class MechHandGunView
    {
        /// <summary>v1's gun arm (both mechs fire from it): its measured reach about the elbow from the arm's rest (deg; outward = the mech's
        /// right) and its climb (deg up or down) at which that turn stays clear of the body.</summary>
        public static float V1ArmOutDeg = 60f, V1ArmInDeg = 29f, V1ArmPitchDeg = 30f;
        /// <summary>k112: sim shots not drawn because their robot was out of the drawn arm's reach that were killing rounds.</summary>
        public static int ReachKillHeld;

        /// <summary>GunFor, once per mech: this model's reach (an own gun arm: the rule's arc + the slack, as before; v1's arm: its own).</summary>
        static void SetReach(Gun g, bool ownArm)
        {
            g.ownArm = ownArm;
            g.reachOut = ownArm ? (float)Sim.ZWorld.HandGunOutDeg + ArcSlackDeg : V1ArmOutDeg;
            g.reachIn = ownArm ? (float)Sim.ZWorld.HandGunInDeg + ArcSlackDeg : V1ArmInDeg;
            g.pitchMax = ownArm ? PitchMaxDeg : V1ArmPitchDeg;
        }

        /// <summary>Can the drawn arm point at p now (the turn and the climb from this frame's rest, solved on the elbow as Pose does)?</summary>
        bool ReachesK112(Gun g, Vector3 p)
        {
            Rest(g, out _, out var f0, out var m0);
            MechHandGunAim.Need(g.elbow.position, f0, m0, p, out float yaw, out float pitch);
            return yaw <= g.reachOut && yaw >= -g.reachIn && Mathf.Abs(pitch) <= g.pitchMax;
        }

        /// <summary>The reach numbers for the counter line.</summary>
        static string ReachText => $"reach: v1's arm {V1ArmOutDeg:0} out / {V1ArmInDeg:0} in, climb {V1ArmPitchDeg:0}; an own arm {(float)Sim.ZWorld.HandGunOutDeg + ArcSlackDeg:0} / {(float)Sim.ZWorld.HandGunInDeg + ArcSlackDeg:0}, {PitchMaxDeg:0}";
    }
}
