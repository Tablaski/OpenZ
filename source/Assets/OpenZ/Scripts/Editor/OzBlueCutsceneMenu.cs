// OpenZ (GPL-3.0) - version 1.2: the menu of the blue cutscene proofs (kit k75b). Edit mode: the counted check. Play (MainMenu scene):
// the Play proofs deploy level 02 as blue and run the scene; the Record jobs also write the MP4s into blue_team/videos/.
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzBlueCutsceneMenu
    {
        const string M = "OpenZ/V1.2/Blue Cutscenes: 75b ";

        [MenuItem(M + "1 Counted Check (no Play)", false, 7510)]
        static void Counted() { OzBlueCutsceneProof.CountedCheck(); }

        [MenuItem(M + "2 Play Proof Briefing (from the MainMenu)", false, 7511)]
        static void PlayBriefing() { if (Playing()) OzBlueCutsceneProof.Start(OzBlueCutsceneProof.Job.Briefing); }
        [MenuItem(M + "2 Play Proof Win + Nuke scene", false, 7512)]
        static void PlayWin() { if (Playing()) OzBlueCutsceneProof.Start(OzBlueCutsceneProof.Job.Win); }
        [MenuItem(M + "2 Play Proof Lose", false, 7513)]
        static void PlayLose() { if (Playing()) OzBlueCutsceneProof.Start(OzBlueCutsceneProof.Job.Lose); }

        [MenuItem(M + "3 Record MP4 Briefing", false, 7521)]
        static void RecBriefing() { if (Playing()) OzBlueCutsceneProof.Start(OzBlueCutsceneProof.Job.RecordBriefing); }
        [MenuItem(M + "3 Record MP4 Win Nuke + Win", false, 7522)]
        static void RecWin() { if (Playing()) OzBlueCutsceneProof.Start(OzBlueCutsceneProof.Job.RecordWin); }
        [MenuItem(M + "3 Record MP4 Lose", false, 7523)]
        static void RecLose() { if (Playing()) OzBlueCutsceneProof.Start(OzBlueCutsceneProof.Job.RecordLose); }

        [MenuItem(M + "4 Status (Play)", false, 7530)]
        static void Status() { Debug.Log(OzBlueCameo.Status()); }

        static bool Playing()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OzBlueCutsceneMenu] enter Play on the MainMenu scene first"); return false; }
            return true;
        }
    }
}
