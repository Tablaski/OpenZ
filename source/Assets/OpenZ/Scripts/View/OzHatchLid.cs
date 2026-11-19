// OpenZ (GPL-3.0) - version 1.5 kit hatch_lid: a tank's hatch lid never disappears.
using System;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// either closed or open with its lid). View only: no rule, no sim state, no sim random number is touched.
    /// The cause: HC1's heavy-hit knock-off (View/HeavyChips.cs OnBlast, called by CombatView for every MissileExploded of a heavy shooter on
    /// a vehicle -> View/RobotPopDeath.cs KnockOff / DoKnock) takes the nearest small real part of a LIVING vehicle within 2.2 m of the burst,
    /// switches it off for good (SetActive(false)) and throws a copy. Its name filter (NoKnock) leaves out the BUILT hatch parts (their names
    /// start with "OZ_": the light tank v1's and the mech's round hatch, RoundHatch.Build) but not the MODELLED lids of the v2 models:
    /// tank_v2_HatchLid (the light tank v2 and the medium tank) and HeavyTankV2_hatch_lid (the heavy tank v2). Each is 0.8 - 1.4 m across
    /// (inside the knock-off's 0.25 - 1.8 m size band), has no child and sits under no gun joint, so a tank shell or a rocket bursting on the
    /// turret took the lid first; the well under it (part of the turret mesh) stayed open: the owner's black hole, for the rest of the match.
    /// The fix: <see cref="KeepOn"/> leaves every part of the hatch (a renderer with a node on its way up to the unit whose name holds "hatch":
    /// the lid, its hinge, a handle, a rim) out of the knock-off's candidates on the four hatch vehicles (ZObject.IsHatchVehicle: the light,
    /// medium and heavy tank and the mech). Such a heavy hit then knocks the next nearest small part off exactly as before (the flying copy,
    /// the sparks, the tear and its clang), or only throws chips when no other part is near. Every other vehicle and cannon is unchanged.
    /// The net (View/OzHatchLidGuard.cs, View/BoardingView.HatchLidGuard.cs): every frame each shown hatch vehicle's lid is drawn and on its
    /// hinge between closed and open; anything else that would switch it off, scale it away or turn it off its arc is put back and logged.
    /// </summary>
    public static class OzHatchLid
    {
        /// <summary>On = the hatch parts of the four hatch vehicles are never knocked off (the fix). Off = the 1.4 knock-off (the check's before run).</summary>
        public static bool KeepLidOn = true;
        /// <summary>On = the guard keeps each shown hatch vehicle's lid drawn and on its hinge (closed .. open). Off = no guard (the check's before run).</summary>
        public static bool GuardOn = true;
        /// <summary>Proof: hatch parts the knock-off passed over (each part, each heavy hit on a hatch vehicle).</summary>
        public static int PassedOver;

        static Transform lastRoot; static bool lastHatch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { KeepLidOn = true; GuardOn = true; PassedOver = 0; lastRoot = null; lastHatch = false; }   // a check's before run never carries into the next Play

        /// <summary>RobotPopDeath.KnockOff, for each candidate part: true = keep this part on the model (a part of the hatch of a hatch vehicle).
        /// `root` is the unit's view (HeavyChips: the UnitView's transform).</summary>
        public static bool KeepOn(Transform part, Transform root)
        {
            if (!KeepLidOn || !part || !root) return false;
            if (root != lastRoot)
            {
                lastRoot = root;
                var v = root.GetComponent<UnitView>();
                lastHatch = v && v.Obj != null && v.Obj.IsHatchVehicle;
            }
            if (!lastHatch) return false;
            for (var t = part; t && t != root; t = t.parent)
                if (t.name.IndexOf("hatch", StringComparison.OrdinalIgnoreCase) >= 0) { PassedOver++; return true; }
            return false;
        }
    }
}
