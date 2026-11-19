// OpenZ (GPL-3.0) - version 1.5, level 18: the menu of the frame-locked briefing recorders (editor only). Enter Play on the MainMenu scene first.
//  1 Record Blue = the 75b recorder (OzBlueCutsceneProof, job RecordBriefing) with Level "L18" and its level 18 row (OzBriefingRecTimes),
//    plus the per-frame log and the sound log beside the take (OzBriefingFrameLog).
//  2 Record Red  = its red twin (OzRedBriefingRecorder): the red briefing with the level-start flyover under it.
//  3 Skip matrix = SPACE at many moments of both briefings, restart and retry (OzL18SkipMatrix).
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL18BriefingRecMenu
    {
        const string M = "OpenZ/V1.5/L18 Briefings/";

        [MenuItem(M + "1 Record Blue (MainMenu, Play)", false, 7601)]
        static void RecBlue()
        {
            if (!Playing()) return;
            OzBlueCutsceneProof.Level = "L18";
            OzBriefingFrameLog.Begin();
            OzBlueCutsceneProof.Start(OzBlueCutsceneProof.Job.RecordBriefing);
        }

        [MenuItem(M + "2 Record Red (MainMenu, Play)", false, 7602)]
        static void RecRed() { if (Playing()) OzRedBriefingRecorder.Start("L18"); }

        [MenuItem(M + "3 Skip matrix (MainMenu, Play)", false, 7603)]
        static void Skips() { if (Playing()) OzL18SkipMatrix.Begin(); }

        [MenuItem(M + "4 Status (Play)", false, 7610)]
        static void Status()
        {
            Debug.Log("[OzL18BriefingRecMenu] " + ZodFlyover.Status() + "\n" + OzL18BriefingStage.Status() + "\n" + OzL18WatchSet.Status() + "\n"
                      + (OzBlueCameo.I && OzBlueCameo.I.Stage ? OzBlueCameo.I.Stage.L18Status() : "no blue stage") + "\n"
                      + OzRedBriefingRecorder.HandBackState("now"));
        }

        static bool Playing()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OzL18BriefingRecMenu] enter Play on the MainMenu scene first"); return false; }
            return true;
        }
    }
}
