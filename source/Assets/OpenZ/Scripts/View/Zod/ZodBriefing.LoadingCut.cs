// OpenZ (GPL-3.0) - version 1.2, owner item 160: no comms card over the level's loading screen.
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// screen, drawn over its bottom-left). Cause: the card's window has its own overlay canvas (ZodLayout.Quip, sorting 101) above the
    /// menu's canvas (sorting 100) that holds the loading screen, and leaving the battlefield screen only STARTED the window's exit: the
    /// commander's level line faded out for 0.3 s and then his window folded (signal burst, CRT-off, about 0.4 s) with its close static;
    /// the blue side's card (EvilZod's feed) folded the same 0.4 s while his line faded 0.3 s. The loading screen's first frames are long
    /// (the level loads behind it), so the fold was seen over it.
    /// Now: in the frame the menu shows its loading screen (view Loading: DEPLOY, also the Unit editor's), at the end of this component's
    /// frame (LateUpdate: after every Update and coroutine, before the frame is drawn) the window is closed at once (no fold), the
    /// commander's line and EvilZod's line stop, the window's static stops, both stages go dark and their own sounds stop. No close
    /// static plays (as before on the loading screen). Every later frame of the loading screen checks again (anything found is cut and
    /// counted in <see cref="LoadingRecuts"/>, expected 0). The battlefield screen is not touched: there the card opens, speaks and
    /// folds as before.
    /// </summary>
    public sealed partial class ZodBriefing
    {
        bool loadingCut;
        static readonly List<AudioSource> loadingCutSources = new List<AudioSource>();
        /// <summary>Checks: the loading screens on which something of the comms was cut in their first frame, the later frames of a loading
        /// screen on which something was found again (expected 0), and what was cut the last time ("" = nothing was on).</summary>
        public static int LoadingCuts { get; private set; }
        public static int LoadingRecuts { get; private set; }
        public static string LoadingCutWhat { get; private set; } = "";
        /// <summary>Checks: the loading screen is up and nothing of the comms is on (no window, no line, no voice, no static, no stage sound).</summary>
        public static bool LoadingClean => I && I.mode == Mode.Menu && I.menu && I.MenuView() == "Loading" && I.CommsSilent();

        /// <summary>LateUpdate, before the window's own frame: on the menu's loading screen nothing of the comms stays on.</summary>
        void LoadingCutTick()
        {
            if (mode != Mode.Menu || !menu) return;
            if (MenuView() != "Loading") { loadingCut = false; return; }
            bool first = !loadingCut; loadingCut = true;
            string what = LoadingCutNow();
            if (what.Length == 0) return;
            LoadingCutWhat = what;
            if (first) LoadingCuts++; else LoadingRecuts++;
            OzSoundLog.Add("ZodCameo", "comms_loading_cut", what, first ? "STOP (the loading screen is up)" : "STOP again (found on the loading screen)");
            Debug.Log("[ZodCameo] the loading screen is up: cut at once, no fold: " + what + (first ? "" : " (found again on a later frame)"));
        }

        /// <summary>Closes and silences everything of the comms now; returns what was on ("" = nothing).</summary>
        string LoadingCutNow()
        {
            var sb = new StringBuilder();
            if (perf != null)
            {   // the commander's level line (on the air, fading or folding): gone, no close static
                sb.Append("the commander's line ").Append(perf.id).Append(perf.voiceOn ? " (his voice on)" : " (before his voice)").Append("; ");
                if (voice) voice.Stop();
                perf.done = true; perf.closing = true; perf = null;
                if (stage) stage.SetLive(false);
            }
            else if (voice && voice.isPlaying) { voice.Stop(); sb.Append("the commander's voice; "); }
            if (evilCardOn) { EvilCardClose("the loading screen is up"); sb.Append("the blue card; "); }
            if ((evilVoice && evilVoice.isPlaying) || evilTalking)
            {   // EvilZod's card line: his voice stops now (its routine then ends by itself, silent)
                if (evilVoice) evilVoice.Stop();
                evilTalking = false;
                sb.Append("EvilZod's line ").Append(evilLine != null ? evilLine.Id : "").Append("; ");
            }
            if (comms != null && comms.Visible) { comms.CloseNow(); sb.Append("the comms window; "); }
            if (staticEdge && staticEdge.isPlaying) { staticEdge.Stop(); sb.Append("the window's static; "); }
            if (staticMid && staticMid.isPlaying) { staticMid.Stop(); sb.Append("the window's mid static; "); }
            int n = StopSounds(stage) + StopSounds(evilStage);
            if (n > 0) sb.Append(n).Append(" stage sound(s); ");
            return sb.Length > 2 ? sb.ToString(0, sb.Length - 2) : "";
        }

        static int StopSounds(Component owner)
        {
            if (!owner) return 0;
            int n = 0;
            owner.GetComponentsInChildren(true, loadingCutSources);
            foreach (var s in loadingCutSources) if (s && s.isPlaying) { s.Stop(); n++; }
            loadingCutSources.Clear();
            return n;
        }

        static bool AnyPlaying(Component owner)
        {
            if (!owner) return false;
            owner.GetComponentsInChildren(true, loadingCutSources);
            bool on = false;
            foreach (var s in loadingCutSources) if (s && s.isPlaying) { on = true; break; }
            loadingCutSources.Clear();
            return on;
        }

        bool CommsSilent() => perf == null && !evilCardOn && !evilTalking && (comms == null || !comms.Visible)
                              && !(voice && voice.isPlaying) && !(evilVoice && evilVoice.isPlaying)
                              && !(staticEdge && staticEdge.isPlaying) && !(staticMid && staticMid.isPlaying)
                              && !AnyPlaying(stage) && !AnyPlaying(evilStage);

        /// <summary>One-line state of the loading screen's cut (Play checks).</summary>
        public static string LoadingCutStatus()
        {
            if (!I) return "ZodBriefing: none in this scene";
            return $"loading cut: view {(I.menu ? I.MenuView() : "no menu")}, clean {LoadingClean}, cuts {LoadingCuts}, found again {LoadingRecuts}, last \"{LoadingCutWhat}\", "
                 + $"window {(I.comms == null ? "none" : I.comms.Visible ? "VISIBLE" : "closed")}, commander voice {(I.voice && I.voice.isPlaying ? "ON" : "off")}, "
                 + $"EvilZod voice {(I.evilVoice && I.evilVoice.isPlaying ? "ON" : "off")}, static {(I.staticEdge && I.staticEdge.isPlaying ? "ON" : "off")}";
        }
    }
}
