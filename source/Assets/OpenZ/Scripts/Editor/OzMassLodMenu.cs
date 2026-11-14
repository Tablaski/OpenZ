using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>1.3 "Mass battles faster": the counted Play check of View/OzMassLod.cs (View/OzMassLodCheck.cs), report into the package's proof folder.</summary>
    static class OzMassLodMenu
    {
        [MenuItem("OpenZ/V1.3/Unit Editor/M Mass Battles Check (Play, the unit editor open)")]
        static void Check()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ MassLodCheck] enter Play and open the unit editor first"); return; }
            if (string.IsNullOrEmpty(OzMassLodCheck.Folder))
                OzMassLodCheck.Folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_3/solutions/ue_mass_battles/proof"));
            OzMassLodCheck.Begin();
        }

        [MenuItem("OpenZ/V1.3/Unit Editor/M Mass Battles Check: last report")]
        static void Last() => Debug.Log(OzMassLodCheck.Running ? "[OpenZ MassLodCheck] running" : OzMassLodCheck.Report);
    }
}
