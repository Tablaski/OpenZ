using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>1.4 ue_nuke_mushroom: the Play stage of the Unit editor's NUKE mushroom (View/OzUeMushroomCheck.cs). Its report and the frames
    /// (frames_new: the new cloud, frames_old: the levels' look as in 1.3, at 0 / 1 / 2 / 4 / 7 / 10 s after the impact) go to the kit's
    /// proof_play folder unless OzUeMushroomCheck.Folder was set first.</summary>
    static class OzUeMushroomMenu
    {
        [MenuItem("OpenZ/V1.4/Unit Editor/Nuke Mushroom: Frames + Check (Play, the unit editor open)")]
        static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ NukeMushroom] enter Play and open the unit editor first"); return; }
            if (string.IsNullOrEmpty(OzUeMushroomCheck.Folder))
                OzUeMushroomCheck.Folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_4/kits/ue_nuke_mushroom/proof_play"));
            OzUeMushroomCheck.Begin();
        }

        [MenuItem("OpenZ/V1.4/Unit Editor/Nuke Mushroom: last report")]
        static void Last() => Debug.Log(OzUeMushroomCheck.Running ? "[OpenZ NukeMushroom] running" : OzUeMushroomCheck.Report);
    }
}
