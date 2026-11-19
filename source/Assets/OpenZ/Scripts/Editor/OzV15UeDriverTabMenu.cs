#if !OPENZ_HARNESS && !OZ_RELEASE
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The menus of the 1.5 kits ue_exit_button + ue_exit_placement (the Unit editor's EXIT DRIVER button in the picked-unit card,
    /// UI/OzUeDriverTab.cs, UI/OzUeInspector.ExitDriver.cs): its Play check (View/OzUeDriverTabCheck.cs, about 70 s with the six tanks and
    /// the UI SCALE steps) and its last report. The report and the frames go to kit ue_exit_placement's proof/play folder.</summary>
    public static class OzV15UeDriverTabMenu
    {
        static string PlayFolder => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/kits/ue_exit_placement/proof/play"));

        [MenuItem("OpenZ/V1.5/UE Exit Driver Tab: Play Check (the Unit editor open)")]
        static void Run()
        {
            if (!Application.isPlaying || !OzUnitEditor.Active) { Debug.LogWarning("[OpenZ] UE exit driver check: open the Unit editor in Play first (main menu, UNIT EDITOR; the normal mode, not 'Zod exact')"); return; }
            if (OzUeDriverTabCheck.Running) { Debug.LogWarning("[OpenZ] UE exit driver check: already running"); return; }
            OzUeDriverTabCheck.Folder = PlayFolder;
            OzUeDriverTabCheck.Begin();
            Debug.Log("[OpenZ] UE exit driver check started (each hatch vehicle dropped, picked, its EXIT DRIVER button in the card hovered and clicked; a blue tank by the key; an empty tank; a gun and a tank; every UI SCALE step; the box closed); report in " + PlayFolder);
        }

        [MenuItem("OpenZ/V1.5/UE Exit Driver Tab: last report")]
        static void Last() => Debug.Log(OzUeDriverTabCheck.Report);
    }
}
#endif
