using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>1.3 item 2: the Unit editor's NUKE, its counted Play check (View/OzUeNukeCheck.cs; set OzUeNukeCheck.Folder first for its
    /// report and sound log in a folder). Its tile picture: Resources/OpenZ/UnitEditor/ue_nuke_tile.png, a 3D nuclear bomb with the owner's radiation sign.</summary>
    static class OzUeNukeMenu
    {
        [MenuItem("OpenZ/V1.3/Unit Editor/2 NUKE Check (Play, the unit editor open)")]
        static void NukeCheck()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ NukeCheck] enter Play and open the unit editor first"); return; }
            if (string.IsNullOrEmpty(OzUeNukeCheck.Folder))
                OzUeNukeCheck.Folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_3/solutions/item02_ue_nuke/proof"));
            OzUeNukeCheck.Begin();
        }

        [MenuItem("OpenZ/V1.3/Unit Editor/2 NUKE Check: last report")]
        static void NukeReport() => Debug.Log(OzUeNukeCheck.Running ? "[OpenZ NukeCheck] running" : OzUeNukeCheck.Report);
    }
}
