#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 item 17 menu (the grenade crate's cursor is Z's hand-grab cursor, the flag keeps its flag-capture cursor,
    /// UI/OpenZCursors.cs): the Play proof View/OzGrabCursorProof.cs on any level (a new crate beside our fort, grunts / a Tough squad /
    /// grunts holding grenades, a flag; hover, right-click, the cursor row). Its output folder: OzGrabCursorProof.Folder() (built at run
    /// time from the project's place).</summary>
    public static class OzGrabCursorMenu
    {
        [MenuItem("OpenZ/V1.2/Cursors: 17 Grenade Hand Cursor Proof (any level, Play)")]
        static void Proof()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] 1.2 item 17 grenade hand cursor proof: start a level in Play first"); return; }
            OzGrabCursorProof.Run();
            Debug.Log("[OpenZ] 1.2 item 17 grenade hand cursor proof started; its report: OzGrabCursorProof.Report, the log and pictures in " + OzGrabCursorProof.Folder());
        }
    }
}
#endif
