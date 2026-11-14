#if !OPENZ_HARNESS
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 item 11 menus (EXPERIMENTAL PLAY AS BLUE): the headless counted check (no Play; whole matches on levels 2, 16 and 17,
    /// a few minutes) and the Play proof (View/OzPlayBlueProof.cs: in a level started from the battlefield screen with the box ON, or on
    /// the battlefield screen itself, which then deploys level by level). Its output folder: OzPlayBlueProof.Folder().</summary>
    public static class OzPlayBlueMenu
    {
        /// <summary>The editor keeps static values after Play: Play stopped inside a blue level would leave the mode on in edit mode (the
        /// team colour option, the start shuttle rule and the words of edit-mode tools would follow it). Back in edit mode it is ended.</summary>
        [InitializeOnLoadMethod]
        static void EndWithPlay() => EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredEditMode) OzPlayBlue.Disarm("Play stopped in the editor"); };

        [MenuItem("OpenZ/V1.2/Play As Blue: 11 Sides Check (headless, no Play)")]
        static void Check()
        {
            bool heads = ZGame.HeadStartTypes.SequenceEqual(OzV12PlayBlueCount.HeadL02) && ZGame.HeadStartL16Types.SequenceEqual(OzV12PlayBlueCount.HeadL16)
                         && ZGame.EasyL17Types.SequenceEqual(OzV12PlayBlueCount.HeadL17);
            OzV11Kit.RunMenu("1.2 item 11 Play As Blue: sides check", () =>
                (heads ? "PASS  the check's head start lists are ZGame's own\n" : "FAIL  the check's head start lists differ from ZGame's (View/ZGame.cs): update Editor/OzV12PlayBlueCount.cs\n")
                + OzV12PlayBlueCount.Check(OzV11Kit.SettingsText, OzV11Kit.MapText, false, s => EditorUtility.DisplayProgressBar("Play As Blue check", "part " + s, 0.5f)));
            EditorUtility.ClearProgressBar();
        }

        [MenuItem("OpenZ/V1.2/Play As Blue: 11 Play Proof (battlefield screen or a blue level, Play)")]
        static void Proof()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] 1.2 item 11 Play As Blue proof: start the MainMenu scene (or a level deployed with the box ON) in Play first"); return; }
            OzPlayBlueProof.Run();
            Debug.Log("[OpenZ] 1.2 item 11 Play As Blue proof started; its report: OzPlayBlueProof.Report, the log and pictures in " + OzPlayBlueProof.Folder());
        }
    }
}
#endif
