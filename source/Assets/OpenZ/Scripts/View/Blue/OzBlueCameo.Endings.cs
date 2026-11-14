// OpenZ (GPL-3.0)
// How they sit in the ending the game already plays (nothing of that is changed): fort falls -> (win: the announcer, the WIN NUKE scene,
// the blue bomb) -> bomber -> impact, slow motion, mushroom, death wave -> OpenZVictory.Run waits PostImpactDelay -> THIS SCENE in the
// top-left under the letterbox bars, the subtitles and the speaker labels on the bars, the game's own view of the ruin behind it) ->
// the window folds -> OpenZVictory.Release -> the end screen. WIN: "The Red fort is ours, sir! Now what?" ... "NOW MOVE TO THE NEXT
// MISSION!", the sidekick salutes and scurries off. LOSE: "Mission failed, sir!" ... "FINE, I'LL DO IT MYSELF!": the cut finds EvilZod
// appears only here. Enter / Space (OpenZVictory's skip) ends the scene early (the voice fades 0.15 s).
// Any other case (a bot match, test mode, an automated check, no fort down, a missing scene) answers false and the level ends like the others.
using System;
using System.Collections;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzBlueCameo
    {
        public static string EndingStatus = "no blue ending scene yet";
        public static bool EndingSceneEnabled = true;

        /// <summary>OpenZVictory.Run: a blue level, this match and the data allow the scene.</summary>
        public static bool EndingSceneWanted(bool won)
        {
            var z = I;
            if (!EndingSceneEnabled || !z || !z.game || !OzPlayBlue.Active) return false;
            if (z.Bots || GameBoot.TestMode || z.Automated) return false;
            if (!OpenZEnding.Running && !FortFallAtGameOver.AnyFell) { Debug.Log($"{Tag} ending scene: no fort fell (one side lost all its units): the level ends like the others"); return false; }
            if (!OzBlueScenes.Shared.Ready(won ? OzBlueScenes.Win : OzBlueScenes.Lose, out var why)) { Debug.LogWarning($"{Tag} ending scene: {why}: the level ends like the others"); return false; }
            return true;
        }

        /// <summary>The endings' window: the briefing's place and size (top-left under the letterbox bar, 400 x 300), the evil HQ's channel.</summary>
        public static ZodLayout EndingLayout()
        {
            var l = ZodLayout.Briefing(); l.eyebrow = "INCOMING TRANSMISSION"; l.channel = "EVIL HQ · CH 02"; l.scale = WindowScale;
            return l;
        }

        /// <summary>OpenZVictory.Run: plays the scene; ends when it is over or when skipped() turns true (the end screen is up).</summary>
        public static IEnumerator EndingScene(bool won, Func<bool> skipped) { return I.EndingRoutine(won, skipped); }

        /// <summary>OpenZVictory.Run, before the scene: its whole length (the opening, the voice, the tail and 2 s of room) is added to the
        /// end sequence's safety hold, so the end screen never comes in the middle of it (the LOSE scene ends about 23.8 game seconds after
        /// the fall when the enemy bomber flies at once; the safety release came 24 s after the game over).</summary>
        public static float EndingSeconds(bool won)
        {
            var s = OzBlueScenes.Shared.Scene(won ? OzBlueScenes.Win : OzBlueScenes.Lose);
            return s != null ? Mathf.Max(0.6f, s.preroll) + s.length + Mathf.Max(0.3f, s.tail) + 2f : 0f;
        }

        IEnumerator EndingRoutine(bool won, Func<bool> skipped)
        {
            if (skipped != null && skipped()) { Debug.Log($"{Tag} ending scene: skipped before the cut (the end screen is up): the level ends like the others"); yield break; }
            if (show != null && !show.done) { show.Abort(0.15f); float w = 0f; while (show != null && w < 1f) { w += Step; yield return null; } }
            EnsureStage(); EnsureComms();
            if (hud) hud.SetVisible(false);
            comms.Letterbox(true, "", ""); comms.LetterboxCue = true;
            string id = won ? OzBlueScenes.Win : OzBlueScenes.Lose;
            bool ok = StartShow("ending", id, EndingLayout(), false, 0.6f);
            if (!ok) { comms.Letterbox(false); yield break; }
            EndingsPlayed++;
            OzFortInferno.DuckAll(-24f, 2.5f, "blue ending scene");
            EndingStatus = $"blue ending {id}: cut {Time.time - OpenZNukeStrike.LastImpactTime:F2} s after the impact, clip {show.clip.name} {show.clip.length:F2} s, end {show.end:F2} s";
            Debug.Log($"{Tag} {EndingStatus}");
            bool wasSkipped = false; int frames = 0;
            while (show != null && !show.done)
            {
                if (skipped != null && skipped()) { wasSkipped = true; break; }
                if (frames++ == 1) Debug.Log($"{Tag} ending first frame: stage camera {(stage && stage.Live && !stage.OnScreen ? "live in the comms window" : "NOT in the window")}, {stage?.Status()}");
                if (!Paused && Time.unscaledTime >= nextDuck - 0.05f && CombatAudio.I) CombatAudio.I.DuckFor(0.55f, -9f, -4f);
                yield return null;
            }
            if (wasSkipped && show != null && !show.done) { show.Abort(0.15f); float w = 0f; while (show != null && !show.done && w < 0.5f) { w += Step; yield return null; } }
            // the window folds (Update clears the show and unloads the stage when it has), then the bars go and the end screen comes
            float fold = 0f; while (show != null && fold < 1.2f) { fold += Mathf.Max(Step, 0.001f); yield return null; }
            comms.SetCue(null); comms.SetVu(0f); comms.Letterbox(false);
            if (show != null) { show.done = true; show = null; RestoreHost(); UnloadStage(); }
            Debug.Log($"{Tag} ending scene over ({(wasSkipped ? "skipped" : "complete")}); the window folded, the end screen comes");
        }
    }
}
