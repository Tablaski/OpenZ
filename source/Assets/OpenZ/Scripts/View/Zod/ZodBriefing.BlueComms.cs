// OpenZ (GPL-3.0) - version 1.2, owner item 48: no Commander Zod on the battlefield screen while PLAY AS BLUE is ticked.
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// COMMS · HQ window with CMDR ZOD and his portrait while PLAY AS BLUE was ticked). On the main menu scene MenuRules asks
    /// <see cref="BlueCommsRules"/> before the commander's own rules:
    ///  * the battlefield screen (view Select) with the PLAY AS BLUE box ticked and offered: a level line of the commander that is on the
    ///    air fades out at once (0.3 s, as when the screen is left) and his window folds after it, no line of his starts and NO comms
    ///    window is shown at all (1.2 item 65: an empty channel card read as a bug): the strip under the level cards stays empty, as it
    ///    is between his lines, and nothing else on the screen moves (the tooltips keep their places, so nothing jumps when the box is
    ///    clicked). The blue channel's card (ZodComms.OpenBlueCard, ZodComms.BlueCard.cs) is kept for blue's own scenes later, unused;
    ///  * the box unticked, or the screen left: the commander's rules run again at once: his line for the level card the player clicked
    ///    comes as it always did (a level's line is not used up while the box was ticked).
    /// Not in a capture take, test mode or an automated check (as the commander's own lines). The main menu's greeting is not touched.
    /// </summary>
    public sealed partial class ZodBriefing
    {
        bool blueCardOpen, blueHidden;
        /// <summary>Checks (1.2 item 65): the times the box hid the comms window, the windows it folded itself (none expected: a faded
        /// line's window folds by itself), and whether any comms window is on the screen now (opening, open or folding).</summary>
        public static int BlueHides { get; private set; }
        public static int BlueWindowsFolded { get; private set; }
        public static bool CommsVisible => I && I.comms != null && I.comms.Visible;
        /// <summary>Checks: the blue card's openings and closings, and the commander's lines it faded out, since the game started.</summary>
        public static int BlueCardOpens { get; private set; }
        public static int BlueCardCloses { get; private set; }
        public static int BlueQuipsFaded { get; private set; }
        /// <summary>Checks: the blue channel's card is on the screen (opening or open).</summary>
        public static bool BlueCardShown => I && I.blueCardOpen && I.comms != null && I.comms.BlueCard;
        /// <summary>Checks: the comms window is up, its speaker and eyebrow labels, and whether its well shows the commander's feed.</summary>
        public static bool CommsOpen => I && I.comms != null && I.comms.IsOpen;
        public static string CommsSpeaker => I && I.comms != null ? I.comms.SpeakerText : "";
        public static string CommsEyebrow => I && I.comms != null ? I.comms.EyebrowText : "";
        public static bool CommsFeedShown => I && I.comms != null && I.comms.FeedShown;

        /// <summary>MenuRules, before the commander's own rules. True = the blue card owns the window this frame (his rules wait).</summary>
        bool BlueCommsRules(string view, float now)
        {
            bool want = view == "Select" && !menu.ShowingAiMaps && OzPlayBlue.BoxOn && !GameBoot.TestMode && !Automated;
            if (want)
            {
                if (perf != null)
                {   // he was on the air when the box was ticked: his line fades out now, the card follows when his window has folded
                    if (perf.fadeSpeed <= 0f) { perf.Abort(0.3f); BlueQuipsFaded++; Debug.Log("[ZodCameo] PLAY AS BLUE ticked: " + perf.id + " fades out (no commander on the blue side)"); }
                    return true;
                }
                if (comms != null && !comms.Closed && !evilCardOn)
                {   // 1.2 item 65: no comms window of any kind while the box is ticked; one still up without a line of his folds now (item 75: except the blue side's own card)
                    if (comms.IsOpen) { comms.Close(); BlueWindowsFolded++; Debug.Log("[ZodCameo] PLAY AS BLUE ticked: the comms window folds away (no comms window on the blue side)"); }
                    return true;
                }
                if (!blueHidden)
                {
                    blueHidden = true; BlueHides++;
                    Debug.Log("[ZodCameo] PLAY AS BLUE ticked: no comms window on the battlefield screen (no commander, no channel card; the blue side's own characters come later)");
                }
                EvilCardTick(now);                                                                     // 1.2 item 75: the blue side's own channel card, EvilZod's feed (ZodBriefing.EvilCard.cs)
                return true;
            }
            if (blueHidden)
            {
                EvilCardClose(view == "Select" ? "the side changed" : "the battlefield screen left");   // 1.2 item 75 (ZodBriefing.EvilCard.cs)
                blueHidden = false;
                Debug.Log("[ZodCameo] the comms window may show again (" + (view == "Select" ? "the box unticked" : "the battlefield screen left") + "): the commander's rules again");
            }
            if (blueCardOpen)
            {
                blueCardOpen = false; BlueCardCloses++;
                if (comms != null) comms.Close();
                if (view != "Loading") PlayCardStatic("comms_static_close");
                Debug.Log("[ZodCameo] the blue channel's card folds away (" + (view == "Select" ? "the box unticked" : "the battlefield screen left") + "): the commander's rules again");
            }
            return false;
        }

        /// <summary>The window's plain static clip on the window's own source and level (as PlayStatic, without the radio calls' draw).</summary>
        void PlayCardStatic(string clip)
        {
            if (!staticEdge) return;
            if (!statics.TryGetValue(clip, out var c)) statics[clip] = c = Resources.Load<AudioClip>("OpenZ/Zod/Sfx/" + clip);
            if (!c) return;
            staticEdge.Stop(); staticEdge.clip = c; staticEdge.pitch = Random.Range(0.93f, 1.07f);
            staticEdge.volume = Mathf.Clamp01(VoiceGain() * Mathf.Pow(10f, staticEdgeDb / 20f)); staticEdge.Play();
            OzSoundLog.Add("ZodCameo", clip == "comms_static_open" ? "comms_blue_open" : "comms_blue_close", c.name, "PLAY", staticEdge.volume);
        }

#if UNITY_EDITOR
        /// <summary>Play proofs: the level lines may come again this session (as after a new start of the game).</summary>
        public static void ProofForgetQuips() => quipped.Clear();

        /// <summary>Play proofs: the level briefing skipped as the SPACE key does (the proof waits the 0.35 s the key needs first). False: no
        /// briefing line runs that could be skipped.</summary>
        public static bool ProofSkipBriefing()
        {
            if (!I || I.mode != Mode.Level || I.phase != Phase.Briefing || I.perf == null || I.perf.done || I.skipped) return false;
            I.skipped = true; I.perf.Abort(0.22f); AudioDirector.UI("ui_cancel");
            Debug.Log($"[ZodCameo] briefing skipped at {I.perf.t:F1} s (a Play proof, as the SPACE key)");
            return true;
        }
#endif
    }
}
