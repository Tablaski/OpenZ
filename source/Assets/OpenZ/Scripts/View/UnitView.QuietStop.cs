using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.4 DV171 vehicle_exit_jump2,
    /// the first jump kit's Play check: the DRAWN hull still moved 0.56-0.62 s after the EXIT or the snipe, bar 0.3 s, while the rules had it
    /// stopped in that tick): what moved it was the shot's recoil, not a drive: the vehicle had fired just before the click, and its recoil
    /// springs (View/HeavyRecoil.cs: the body slides back and rocks at 2.3 Hz, damping 0.42, about 0.6 s to settle; the head rocks too; the
    /// envelope kick of UnitView.ApplyRecoil for the others) and VehicleMotion's squat and kick springs went on swinging the drawn hull and
    /// turret. The drawn position and turn themselves already stop with the rules in that frame (UnitView.NoCrab: VehicleSlip.Hold on
    /// UnmannedHeld; the turret: UnitAim.QuietStep, UnitView's turretAim).
    /// Now, in the frame the view first sees the vehicle empty (OzVehicleQuiet.EmptiedFrame: EXIT, sniped, killed inside), before this frame's
    /// recoil is drawn, every one of those springs of THAT vehicle is set to rest: the hull and the turret stand at their place on the hull in
    /// that frame, still from then on; a held shot drawn in that frame (UnitAim.QuietStep) slides its barrel but kicks no hull. Every other unit,
    /// and the same vehicle crewed again, is untouched (a hit's rock on the empty hull, OzVehicleRock, still shows).
    /// </summary>
    public partial class UnitView
    {
        bool quietStopped;
        /// <summary>Proof: vehicles set to rest in their emptied frame (since start).</summary>
        public static int QuietStops;

        /// <summary>Sync, right before ApplyRecoil (one anchored line in View/UnitView.cs): a vehicle emptied this frame: its recoil and suspension
        /// springs to rest, once.</summary>
        void QuietStop()
        {
            if (Obj == null || Obj.Kind != OpenZ.Sim.ZKind.Vehicle) return;
            if (OzVehicleQuiet.EmptiedFrame(Obj) < 0) { quietStopped = false; return; }
            if (quietStopped) return;
            quietStopped = true; QuietStops++;
            shotAge = 2f;                                                                                // ApplyRecoil's envelope kick over (Recoil(age > 1) = 0)
            if (heavyRecoil) heavyRecoil.Still();
            if (motion) motion.Still();
        }

        /// <summary>OnFire (its first line, one anchored line in View/UnitView.cs): a shot of a vehicle nobody is in any more (a held shot drawn in
        /// its emptied frame): the barrel's own slide only, no hull jolt or rock. True = done.</summary>
        bool QuietStopFire()
        {
            if (Obj == null || Obj.Kind != OpenZ.Sim.ZKind.Vehicle || !OzVehicleQuiet.Quiet(Obj)) return false;
            LastFireAt = Time.time;
            if (gunAim) gunAim.OnFire();
            return true;
        }
    }

    public partial class HeavyRecoil
    {
        /// <summary>vehicle_exit_jump2 part A: every spring at rest now (the body's slide and rock, the head's slide and rock): the next
        /// StepAndPlace draws no offset, LateUpdate puts the head back on its pose.</summary>
        public void Still()
        {
            pushX = pushZ = rockX = rockZ = headS = headQ = default;
            BodyPushNow = 0f; BodyRockNow = 0f;
        }
    }

    public partial class VehicleMotion
    {
        /// <summary>vehicle_exit_jump2 part A: the squat (from the speed's change), the lean into turns and the kick springs at rest now.</summary>
        public void Still()
        {
            speedSmooth = prevSpeed = accel = lean = 0f;
            kRoll = kRollV = kPitch = kPitchV = 0f;
        }
    }
}
