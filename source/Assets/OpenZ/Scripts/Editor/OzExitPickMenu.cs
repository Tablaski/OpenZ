#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 item 2f menu (robots walking out of the shuttle can be selected at once, View/PlayerInput.ExitPick.cs): the Play proof
    /// View/OzExitPickProof.cs on level 16 (a grunt squad, then ALAN &amp; BRAD: click, box and an order while they walk out). Its output
    /// folder: OzExitPickProof.Folder() (built at run time from the project's place).</summary>
    public static class OzExitPickMenu
    {
        [MenuItem("OpenZ/V1.2/Shuttle: 2f Walk-out Select Proof (Level 16, Play)")]
        static void Proof()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] 1.2 item 2f walk-out select proof: start level 16 in Play first"); return; }
            OzExitPickProof.Run();
            Debug.Log("[OpenZ] 1.2 item 2f walk-out select proof started; its report: OzExitPickProof.Report, the log and pictures in " + OzExitPickProof.Folder());
        }
    }
}
#endif
