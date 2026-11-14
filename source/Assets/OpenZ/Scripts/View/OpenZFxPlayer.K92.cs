using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 owner item 135 for the mech, v1 AND v2: the mech v2 Play proof found its pod rockets
    /// first drawn 0.17-1.42 m out of the mouth (the MLRS's fault before k87b: a rocket started where the mouth was last frame and was first
    /// drawn a whole frame along its own curve). The mech's pods now name their tube the frame a rocket leaves it (View/MechRockets.cs: rocket 1
    /// from the left pod, its twin from the right pod), and k87b's tube ride (View/OpenZFxPlayer.TubeExit.cs) draws it out of that tube's live
    /// mouth along the tube's axis (the sockets fire along their local -z) from its first frame, then onto its own curve; its landing point,
    /// time and blast are unchanged. A tube is named for one weapon: a rocket of another weapon that starts the same frame never takes it.
    /// </summary>
    public partial class OpenZFxPlayer
    {
        static FxWeapon nextTubeW = FxWeapon.Mlrs;

        /// <summary>The tube (its muzzle and the muzzle's firing axis in its own space) that the next rocket of weapon `w` leaves this frame.</summary>
        public static void NoteTube(Transform muzzle, Vector3 dirL, FxWeapon w)
        {
            NoteTube(muzzle, dirL); nextTubeW = w;
        }
    }
}
