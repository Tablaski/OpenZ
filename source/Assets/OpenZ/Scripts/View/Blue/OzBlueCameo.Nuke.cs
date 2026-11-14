// OpenZ (GPL-3.0)
// The red fort fell: OpenZVoiceV3.PlanNuke holds the bomber for the intro (the announcer's explanation, then the commander's nuke line
// with the red button). On the blue side the commander is not there; this scene takes his slot, in the in-game comms window (a little
// bigger than his): EvilZod presents the World-Domination-inator (the doors part, the spotlights snap on, the giant blue bomb), asks for
// the power switch, the sidekick scurries in with the giant blue nuke button, EvilZod takes it, winds up and his fist lands on the cap
// on "fireworks": the clunk, and onPress launches the strike (OpenZNukeStrike.LaunchNow), so the blue bomb drops because he pressed it.
// Never in bot matches, test mode, capture takes or automated checks, never over another blue scene.
using System;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzBlueCameo
    {
        /// <summary>OpenZVoiceV3.PlanNuke: the blue nuke scene can play now (a blue level in play, no other scene up, the data there).</summary>
        public static bool CanPlayNuke => I && OzPlayBlue.Active && I.phase == Phase.Play && ZodBriefing.Enabled && !I.Bots && !GameBoot.TestMode && !I.Automated
                                          && I.show == null && (I.comms == null || I.comms.Closed) && OzBlueScenes.Shared.Ready(OzBlueScenes.WinNuke, out _);
        /// <summary>Seconds from PlayNuke to the fist on the cap at most (the window's preroll + the press beat + the fist's travel).</summary>
        public static float NukeLeadSeconds
        {
            get
            {
                var s = OzBlueScenes.Shared.Scene(OzBlueScenes.WinNuke);
                float press = -1f;
                if (s?.beats != null) foreach (var b in s.beats) if (b.who == "ez" && b.beat == "press") press = b.t;
                return (s != null ? Mathf.Max(0.45f, s.preroll) : 0.45f) + (press >= 0f ? press + OzEvilStage.PressHit : s != null ? s.length : 10f) + 0.1f;
            }
        }
        /// <summary>The longest the nuke intro waits for the press (the whole scene with its tail).</summary>
        public static float NukeMaxSeconds { get { var s = OzBlueScenes.Shared.Scene(OzBlueScenes.WinNuke); return s != null ? s.preroll + s.length + s.tail + 1.5f : 13f; } }
        /// <summary>True while the blue nuke scene is up (the intro's wait uses the blue limit then).</summary>
        public static bool NukeShowing => I && I.show != null && I.show.kind == "nuke" && !I.show.done;
        public static int NukePresses { get; private set; }

        /// <summary>Opens the nuke scene; onPress runs when the fist meets the cap. False = the scene cannot play now.</summary>
        public static bool PlayNuke(Action onPress)
        {
            if (!CanPlayNuke) return false;
            var z = I;
            z.EnsureStage(); z.EnsureComms();
            var lay = NukeLayout();
            bool ok = z.StartShow("nuke", OzBlueScenes.WinNuke, lay, false, 0.45f);
            if (!ok) return false;
            NukeScenesPlayed++;
            z.stage.OnPress = () => { NukePresses++; try { onPress?.Invoke(); } catch (Exception e) { Debug.LogException(e); } Debug.Log($"{Tag} nuke: the fist hit the blue button at {z.show?.t:F2} s: the strike launches"); };
            return true;
        }

        /// <summary>The in-game window, a little bigger than the commander's (the reveal needs the room), at the same place.</summary>
        public static ZodLayout NukeLayout()
        {
            var l = ZodLayout.InGame();
            l.wellW = 400f; l.wellH = 300f; l.eyebrow = "COMMS · EVIL HQ"; l.channel = "CH 02"; l.scale = WindowScale;
            // (top-left, clear of the HUD's centred top bar), not the commander's in-game place 212 u down
            var br = ZodLayout.Briefing(); l.anchor = br.anchor; l.x = br.x; l.y = br.y;
            return l;
        }

        /// <summary>Play check: the nuke scene now (the press only logs; no strike is launched).</summary>
        public static string DebugNuke()
        {
            if (!I) return "no OzBlueCameo in this scene";
            if (I.show != null) return "busy: " + I.show.kind;
            if (!OzBlueScenes.Shared.Ready(OzBlueScenes.WinNuke, out var why)) return why;
            I.EnsureStage(); I.EnsureComms();
            bool ok = I.StartShow("nuke", OzBlueScenes.WinNuke, NukeLayout(), false, 0.45f);
            if (ok) I.stage.OnPress = () => Debug.Log($"{Tag} DebugNuke: the press (no strike launched)");
            return ok ? "playing the blue nuke scene" : "failed";
        }

        /// <summary>Play check: one ending scene now, on the screen, without a fort down (the pictures only).</summary>
        public static string DebugEnding(bool won)
        {
            if (!I) return "no OzBlueCameo in this scene";
            if (I.show != null) return "busy: " + I.show.kind;
            I.StartCoroutine(I.EndingRoutine(won, null));
            return "playing the blue " + (won ? "win" : "lose") + " ending";
        }
    }
}
