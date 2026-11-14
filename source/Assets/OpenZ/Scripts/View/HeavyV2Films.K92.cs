#if UNITY_EDITOR
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// k92, the v2 films (View/HeavyV2Films.cs) after the medium tank's and the mech's Play checks (editor only):
    ///  * THE TURRET FLUTTER RULE: a flutter needs a swing of at least <see cref="FlutterMinSwingK92"/> between its two turn reversals (the
    ///    world-heading slew's own dead band, UnitAim.CombatDeadDeg). The medium's four "re-obtaining" flutters (menu 12, its second tank) were
    ///    0.11-0.28 deg swings while the slew held still on its target: the hull's eased rock after each shot (HeavyRecoil, after the aim)
    ///    tips the pitched-down barrel. The same goals replayed through the slew give none. Every real flutter in the stage's recorded takes is
    ///    kept (each control line still shows its shake): the kit's proof/flutter_swing.txt.
    ///  * THE TRACK TESTS' "standing": also the drawn tank still (ProofStageK92.GearStill), not only the sim's IsMoving.
    ///  * THE MECH'S BOARD FILM: the level's own units of another side near the empty mech stand still without a weapon before the red grunt is
    ///    ordered in (a blue robot boarded it in k86's take).
    /// </summary>
    public partial class HeavyV2Films
    {
        static float FlutterMinSwingK92 => UnitAim.CombatDeadDeg;

        /// <summary>BeltSampler: the drawn tank moved or turned since the last sampled frame.</summary>
        static bool DrawnMovedK92(UnitView v) => !ProofStageK92.GearStill(v, 0f);

        /// <summary>Board(): the mech's take only (the other subjects' takes as before).</summary>
        void BoardStageK92(ZObject empty)
        {
            if (S.Type != OzMech.Type || empty == null) return;
            ProofStageK92.HoldLevelUnits(W, new[] { empty }, Team.Red, Note, "the mech's board film");
        }
    }
}
#endif
