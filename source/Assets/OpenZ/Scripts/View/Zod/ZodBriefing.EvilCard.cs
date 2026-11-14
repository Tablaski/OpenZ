// OpenZ (GPL-3.0) - version 1.2, owner items 65 / 75: the blue side's own channel card on the battlefield screen.
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// While the blue side is picked (OzPlayBlue.BoxOn: the faction screen's pick, the old box), the battlefield screen's comms strip no
    /// longer stays empty (item 65 hid the broken NO SIGNAL card): it shows the blue side's own channel: General EvilZod's live feed from
    /// the evil HQ (OzEvilStage, idling in his stance, his eyes glowing), the eyebrow COMMS · EVIL HQ, the speaker GENERAL EVILZOD and the
    /// company's name as the line. A level card clicked, he says one of its lines (ZodBriefing.EvilLines.cs). Opened by BlueCommsRules when the strip would
    /// otherwise be empty, folded when the side changes or the screen is left; the stage is built on the first need and goes dark with it.
    /// </summary>
    public sealed partial class ZodBriefing
    {
        OzEvilStage evilStage; bool evilCardOn;
        public static int EvilCardOpens { get; private set; }
        public static bool EvilCardShown => I && I.evilCardOn && I.comms != null && I.comms.IsOpen;

        /// <summary>BlueCommsRules, the strip is the blue side's: the card with EvilZod's feed (opened once; kept while it is up).</summary>
        void EvilCardTick(float now)
        {
            if (evilCardOn) { if (comms != null && comms.Closed) evilCardOn = false; else { if (evilStage) evilStage.VoiceGain = VoiceGain(); EvilLinesTick(now); return; } }   // 1.2 items 98c / 99: his lines (ZodBriefing.EvilLines.cs)
            if (comms == null) EnsureComms(1f);
            if (!comms.Closed) return;                                                                 // the commander's window still folding: next frame
            if (!evilStage) { evilStage = OzEvilStage.Create(transform, "L02"); evilStage.SetLive(false); }
            if (!evilStage.HasEvilZod) { if (!evilCardWarned) { evilCardWarned = true; Debug.LogWarning("[ZodCameo] blue channel card: EvilZod's model is missing (Resources/OpenZ/Blue/Characters): the strip stays empty"); } return; }
            var lay = ZodLayout.Quip(); lay.eyebrow = "COMMS · EVIL HQ"; lay.channel = "CH 02";
            var px = comms.WellPixels(lay);
            evilStage.EnsureFeed(px.x, px.y);
            evilStage.SetLive(true, false);
            evilStage.BeginScene(null, false);
            evilStage.VoiceGain = VoiceGain();
            string hostBefore = ZodComms.Host; ZodComms.Host = OzBlueScenes.EvilZodName;
            comms.Open(lay, evilStage.Feed);
            ZodComms.Host = hostBefore; comms.LetterboxSpeaker(null);
            if (comms.NameLabel != null) comms.NameLabel.Set(OzBlueScenes.EvilZodName);
            comms.ApplyRank();
            SetEvilSpeaker();
            comms.SetCue(OzBlueScenes.Company);
            PlayCardStatic("comms_static_open");
            evilCardOn = true; EvilCardOpens++; BlueCardOpens++;
            Debug.Log("[ZodCameo] blue channel card: EvilZod's feed on the battlefield screen (he speaks when a level card is clicked)");
        }
        bool evilCardWarned;
        void SetEvilSpeaker() { string h = ZodComms.Host; ZodComms.Host = OzBlueScenes.EvilZodLabel; comms.ApplyRank(); ZodComms.Host = h; }

        /// <summary>The side changed or the screen was left: the card folds, the stage goes dark.</summary>
        void EvilCardClose(string why)
        {
            if (!evilCardOn) return;
            evilCardOn = false; BlueCardCloses++;
            if (comms != null) comms.Close();
            if (evilStage) evilStage.SetLive(false);
            if (MenuView() != "Loading") PlayCardStatic("comms_static_close");
            Debug.Log("[ZodCameo] blue channel card folds away (" + why + ")");
        }
    }
}
