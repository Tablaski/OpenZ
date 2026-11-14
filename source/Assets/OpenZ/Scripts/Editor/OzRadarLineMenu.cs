#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 item 16 menu (the first radar of a game gets its own announcer line, "back online" only after a loss): the Play
    /// proof View/OzRadarLineProof.cs on any level with two radars, started at the level start. Its output folder:
    /// OzRadarLineProof.Folder() (built at run time from the project's place).</summary>
    public static class OzRadarLineMenu
    {
        [MenuItem("OpenZ/V1.2/Radar: First vs Back Online Line Proof (any level, Play)")]
        static void Proof()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] 1.2 item 16 radar line proof: start a level in Play first"); return; }
            OzRadarLineProof.Run();
            Debug.Log("[OpenZ] 1.2 item 16 radar line proof started (about 2 minutes); its report: OzRadarLineProof.Report, the log and pictures in " + OzRadarLineProof.Folder());
        }

        [MenuItem("OpenZ/V1.2/Radar: Line Rule Status (Play)")]
        static void Status() => Debug.Log("[OpenZ] radar line rule: " + OzRadarLine.Status());
    }
}
#endif
