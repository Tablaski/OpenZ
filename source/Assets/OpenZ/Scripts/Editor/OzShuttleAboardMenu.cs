#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 item 2 menu (who sits in a shuttle and how many): the Play proof View/OzShuttleAboardProof.cs on level 16 (an empty
    /// neutral ship, ALAN &amp; BRAD aboard, a pilot-only ship, a grunt squad aboard landed and flying, by day and at night: the card, the
    /// pill over the ship). Its output folder: OzShuttleAboardProof.Folder() (built at run time from the project's place).</summary>
    public static class OzShuttleAboardMenu
    {
        [MenuItem("OpenZ/V1.2/Shuttle: Who Is Aboard Proof (Level 16, Play)")]
        static void Proof()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] 1.2 item 2 who-is-aboard proof: start level 16 in Play first"); return; }
            OzShuttleAboardProof.Run();
            Debug.Log("[OpenZ] 1.2 item 2 who-is-aboard proof started; its report: OzShuttleAboardProof.Report, the log and pictures in " + OzShuttleAboardProof.Folder());
        }
    }
}
#endif
