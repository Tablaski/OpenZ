#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 items 67 / 68 menu: the headless counted check of the R key's repair trip on both sides (Editor/OzV12BlueRepairCount.cs,
    /// outside Play, a few minutes) and the Play proofs (View/OzBlueRepairProof.cs): the repair trip on level 16 or 17, and the end lock after
    /// a win or a loss (each lock run ends the match: start the level again for the next one).</summary>
    public static class OzBlueRepairMenu
    {
        [MenuItem("OpenZ/V1.2/Repair: 67 Blue Repair Check (headless)")]
        static void Count() => OzV11Kit.RunMenu("1.2 item 67 the R key's repair trip, both sides", () => OzV12BlueRepairCount.Check(OzV11Kit.SettingsText, OzV11Kit.MapText));

        [MenuItem("OpenZ/V1.2/Repair: 67 Repair Trip Proof (Play)")]
        static void Trip() => Start(OzBlueRepairProof.Part.Trip);

        [MenuItem("OpenZ/V1.2/Repair: 68 End Lock Proof, win (Play)")]
        static void Win() => Start(OzBlueRepairProof.Part.Win);

        [MenuItem("OpenZ/V1.2/Repair: 68 End Lock Proof, loss (Play)")]
        static void Loss() => Start(OzBlueRepairProof.Part.Loss);

        static void Start(OzBlueRepairProof.Part p)
        {
            if (!Application.isPlaying || ZGame.I == null || ZGame.I.World == null) { Debug.LogWarning("[OpenZ] 1.2 items 67 / 68 proof: start a level in Play first"); return; }
            OzBlueRepairProof.Run(p);
        }
    }
}
#endif
