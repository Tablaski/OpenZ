#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.3 item 9 menus (no vehicle or wreck in the ground, CLEAR FIELD takes every wreck; View/WreckGroundRest.cs): the Unit editor
    /// clear check (View/OzUeClearCheck.cs) and the sunk watch for levels and the editor (View/OzSunkWatch.cs). Play only.</summary>
    public static class OzV13WreckMenu
    {
        [MenuItem("OpenZ/V1.3/Wrecks: Item 9 Unit Editor Clear Check (Play, unit editor open)")]
        static void ClearCheck()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] item 9 clear check: enter Play and open the unit editor first"); return; }
            OzUeClearCheck.Begin();
            Debug.Log("[OpenZ] item 9 clear check started (about 3 minutes; hands off); its report: OzUeClearCheck.Report and " + OzUeClearCheck.Folder);
        }

        [MenuItem("OpenZ/V1.3/Wrecks: Item 9 Sunk Watch Start or Stop (Play, any level)")]
        static void Watch()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] item 9 sunk watch: start a level (best a computer-vs-computer match at 4x) in Play first"); return; }
            OzSunkWatch.Toggle();
            Debug.Log("[OpenZ] item 9 sunk watch: " + OzSunkWatch.Report + "; its log in " + OzSunkWatch.Folder);
        }
    }
}
#endif
