#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// View/OzVoiceOverlapProof.cs (L02 in Play, after the briefing; about 7 minutes) and a one-line state of the voice floor.
    /// Its report: Logs/oz_v14_voice_overlap.txt in the project.</summary>
    public static class OzV14VoiceOverlapMenu
    {
        [MenuItem("OpenZ/V1.4/Voices: No Overlap + Radar Lines Check (L02, Play)")]
        static void Check()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] 1.4 voice check: start L02 in Play first (wait for the briefing to end)"); return; }
            OzVoiceOverlapProof.Run();
            Debug.Log("[OpenZ] 1.4 voice check started (about 7 minutes; keep the Game view in front for the real clicks); its report: " + OzVoiceOverlapProof.LogPath);
        }

        [MenuItem("OpenZ/V1.4/Voices: Floor Status (Play)")]
        static void Status()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] 1.4 voice floor: Play only"); return; }
            Debug.Log($"[OpenZ] voice floor: {OzVoiceFloor.Status(Time.unscaledTime)}; robot lines waited {AudioDirector.FloorHeld}, started after waiting {AudioDirector.FloorReleased}, skipped {AudioDirector.FloorSkipped}; "
                      + $"Rachel lines waited {AudioDirector.FloorRachelWaited}; PlayVoice refused {AudioDirector.FloorGuardBlocked}; Radar lost. paired {AudioDirector.RadarLostPaired} (held behind its partner {AudioDirector.RadarLostPinned}), alone {AudioDirector.RadarLostAloneQueued}, "
                      + $"not said {AudioDirector.RadarLostNot} (last: {AudioDirector.RadarLostWhy}); AlertGate pairs {AlertGate.Paired}; radar rule: {OzRadarLine.Status()}");
        }
    }
}
#endif
