using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>1.2 item 109: the Unit editor's simple windows (the leave question, the reset confirm, LOAD, SHARE; SAVE without a window):
    /// the counted Play check View/OzUePopupCheck.cs (set OzUePopupCheck.Folder first for its report and pictures in a folder).</summary>
    static class OzV12PopupsMenu
    {
        [MenuItem("OpenZ/V1.2/Unit Editor/80 Popups Check (Play, the unit editor open)")]
        static void PopupsCheck()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ PopupCheck] enter Play and open the unit editor first"); return; }
            OzUePopupCheck.Begin();
        }

        [MenuItem("OpenZ/V1.2/Unit Editor/80 Popups Check: last report")]
        static void PopupsReport() => Debug.Log(OzUePopupCheck.Running ? "[OpenZ PopupCheck] running" : OzUePopupCheck.Report);
    }
}
