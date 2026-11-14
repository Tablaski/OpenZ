#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 item 9 menus (rule DV135, View/OzShiftAppend.cs: a Shift queue is added to the units' current order): the headless
    /// counted check Editor/OzV12ShiftAppendCount.cs (outside Play) and the Play proof View/OzShiftAppendProof.cs (a level in Play with free
    /// flags, level 2 recommended). The proof's output folder: OzShiftAppendProof.Folder() (built at run time from the project's place).</summary>
    public static class OzShiftAppendMenu
    {
        [MenuItem("OpenZ/V1.2/Orders: 9 Shift Adds Check (headless)")]
        static void Count() => OzV11Kit.RunMenu("1.2 item 9 Shift adds to the current order (DV135)", () => OzV12ShiftAppendCount.Check(OzV11Kit.SettingsText));

        [MenuItem("OpenZ/V1.2/Orders: 9 Shift Adds Proof (Play)")]
        static void Proof()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] 1.2 item 9 Shift adds proof: start a level in Play first (level 2 recommended)"); return; }
            OzShiftAppendProof.Run();
            Debug.Log("[OpenZ] 1.2 item 9 Shift adds proof started; its report: OzShiftAppendProof.Report, the log and pictures in " + OzShiftAppendProof.Folder());
        }
    }
}
#endif
