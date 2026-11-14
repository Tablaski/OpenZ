using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>1.2 item 73 follow-up: the Unit editor's GRENADES crate with the levels' sounds, and Delete on a picked crate (the counted Play
    /// check View/OzUeCrateCheck.cs; set OzUeCrateCheck.Folder first for its report and pictures in a folder).</summary>
    static class OzV12CrateSoundsMenu
    {
        [MenuItem("OpenZ/V1.2/Unit Editor/74c Crate Sounds And Delete Check (Play, the unit editor open)")]
        static void CrateCheck()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ CrateCheck] enter Play and open the unit editor first"); return; }
            OzUeCrateCheck.Begin();
        }

        [MenuItem("OpenZ/V1.2/Unit Editor/74c Crate Check: last report")]
        static void CrateReport() => Debug.Log(OzUeCrateCheck.Running ? "[OpenZ CrateCheck] running" : OzUeCrateCheck.Report);
    }
}
