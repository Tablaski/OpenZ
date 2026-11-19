using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// the Mammoth's twin barrels are taken clear of the vehicle it rams. View only (the rules never read a turret).
    /// <list type="bullet">
    /// <item>PRE-CLEAR: while an enemy crewed vehicle stands inside the ram's start zone in front of a crewed Mammoth (View/OzRamBarrels.cs: the
    /// sim's own arc and gap with a small margin, so it begins before the ram can), the barrels rise to their top stop; the turret keeps its
    /// aim.</item>
    /// <item>RAM: from the ram's start (the sim's Ramming) until the rammed vehicle's flip is over and its death drawn (or the ram ended without a
    /// hit), the barrels stay at the top stop and the turret turns <see cref="RamClearTurnDeg"/> to the side it is nearer to, so the barrels lie
    /// over the Mammoth's own track, far from the vehicle lifted on its spikes.</item>
    /// <item>Both moves use the gun's own eased slew (SmoothDamp), hurried <see cref="RamClearHurry"/> x (the clear is quicker than an aim;
    /// with the Mammoth's 120 deg/s turret: 300 deg/s at most, a 90 deg turn in about 0.4 s).</item>
    /// </list>
    /// kit_tools/barrels_proof.py (proof/barrels_frames.txt) and in Play by View/OzBigTanksFxPlayCheck.cs (renderer boxes of the target).
    /// </summary>
    public partial class UnitAim
    {
        /// <summary>Switch (test aid): false = the barrels stay on their aim during a ram (1.5 before this kit).</summary>
        public static bool RamClearOn = true;
        /// <summary>The clear's slew is the gun's own, this many times faster (speed x, smoothing time / ).</summary>
        public const float RamClearHurry = 2.5f;
        /// <summary>How far the turret turns aside during a ram (deg, to the side it is nearer to).</summary>
        public const float RamClearTurnDeg = 90f;
        /// <summary>The clear takes at most this long from the ram's start (s, at game speed 1): a ram that reaches its contact sooner (it started
        /// close) holds the rammed vehicle on the spikes until then before the heave (OzRamFlip's wait; the rules' timing is unchanged: the flip
        /// is shortened by the wait and still ends when the rules destroy it).</summary>
        public const float RamClearSeconds = 0.45f;
        /// <summary>Proof: frames drawn in the pre-clear, frames drawn in the ram clear.</summary>
        public static int PreClearFrames, RamClearFrames;
        float ramClearSide;

        /// <summary>SolveGun, after the aim's goal: the clear pose of a Mammoth near or in a ram (true = it holds the goal; the gun is not on a
        /// target meanwhile).</summary>
        bool RamClear(ref float gy, ref float gp)
        {
            if (ZWorld.RamPolishDeviation && obj != null && obj.RamDoomed) { if (yawJ != null) gy = yawJ.ang; if (pitchJ != null) gp = pitchJ.ang; ramClearSide = 0f; return true; }   // 1.5 DV188 (kit ram_polish): a doomed vehicle's turret stops tracking
            if (RamFightView(ref gy, ref gp, out bool rfRet)) return rfRet;   // 1.5 DV196 (kit mammoth_ram_fight_n26c): no swing aside, no top stop: the next target or held where it is, the barrels only as high as the rammed hull needs (View/UnitAim.RamFight.cs)
            if (ZWorld.RamPolishDeviation && obj != null && obj.Type == OzBigTanks.Mammoth && obj.AttackObject != null && !obj.RamFireBarred(obj.AttackObject)) { ramClearSide = 0f; return false; }   // 1.5 DV188: a next target during the ram: the turret turns to it
            if (!RamClearOn || obj == null || obj.Kind != ZKind.Vehicle || obj.Type != OzBigTanks.Mammoth || pitchJ == null) { ramClearSide = 0f; return false; }
            var st = OzRamBarrels.StateOf(obj);
            if (st == OzRamBarrels.Clear.None) { ramClearSide = 0f; return false; }
            gp = ClampPitch(1000f * pitchSign);                                                      // the top stop
            if (st == OzRamBarrels.Clear.Ram && yawJ != null)
            {
                if (ramClearSide == 0f) ramClearSide = Mathf.DeltaAngle(0f, yawJ.ang) < 0f ? -1f : 1f;    // the side it is nearer to (straight ahead: +)
                gy = ramClearSide * RamClearTurnDeg;
                RamClearFrames++;
            }
            else PreClearFrames++;
            return true;
        }
    }
}
