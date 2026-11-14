// OpenZ (GPL-3.0) - version 1.2, owner item 48: the comms window as the blue side's channel card (View/Zod/ZodComms.cs, ZodBriefing.BlueComms.cs).
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// box is ticked, the commander's strip shows the blue side's channel instead of Commander Zod. It is the same comms window (plate,
    /// header band, well, scanlines, sheen, brackets, the opening static, the scan band reveal and the CRT-off fold) with the eyebrow
    /// <see cref="BlueEyebrow"/>, no feed: in the well the game's NO SIGNAL card (the unit portrait's card without its SELECT A UNIT line)
    /// under a light steady static, the live dot dim in the blue side's colour; beside it the speaker <see cref="BlueSpeaker"/> in the blue
    /// side's text colour and <see cref="BlueLine"/>. No portrait, name or voice of the commander. Opened by
    /// <see cref="OpenBlueCard"/>, closed by <see cref="Close"/> as any transmission; the next <see cref="Open"/> builds the window again
    /// without the card.
    /// </summary>
    public sealed partial class ZodComms
    {
        public const string BlueEyebrow = "COMMS · BLUE", BlueChannel = "CH 02", BlueSpeaker = "BLUE COMMAND";
        public const string BlueLine = "No signal from blue command. You lead the blue fort.";
        RectTransform blueRoot; bool blueOn;
        static Sprite blueCardSprite; static bool blueCardFailed;

        /// <summary>The blue channel's card is up (opening or open) in the window as it is now (a later Open builds a new window).</summary>
        public bool BlueCard => blueOn && CardInWindow && (state == State.Opening || state == State.Open);
        bool CardInWindow => blueRoot && win && blueRoot.IsChildOf(win);
        /// <summary>Checks: what the window's speaker and eyebrow read now.</summary>
        public string SpeakerText => subSpeaker != null ? subSpeaker.text : "";
        public string EyebrowText => eyebrow != null ? eyebrow.text : "";
        /// <summary>Checks: a picture (the commander's live feed) is in the well.</summary>
        public bool FeedShown => feed && feed.enabled && feedTex;

        /// <summary>Opens the window at the layout's place as the blue channel's card.</summary>
        public void OpenBlueCard(ZodLayout layout)
        {
            layout.eyebrow = BlueEyebrow; layout.channel = BlueChannel;
            Open(layout, null);                                                                         // the window as always, no feed
            blueOn = true;
            if (noSignal) noSignal.enabled = false;                                                     // the unit portrait's card reads SELECT A UNIT: not here
            blueRoot = Hud.Node("BlueCard", feedRt); blueRoot.Fill();                                    // inside the feed: revealed by the scan band, folded by the CRT-off, jolted by the bursts
            var sp = BlueCardSprite();
            if (sp)
            {
                var img = Hud.Img(blueRoot, "NoSignal", Color.white);
                img.sprite = sp; img.type = Image.Type.Simple; img.preserveAspect = true; img.rectTransform.Fill();
            }
            else Hud.Label(blueRoot, "NoSignal", "NO SIGNAL", 13, OzUi.Ink, 0.3f).FillParent(OzH.C);   // no kit card: the words alone
            if (subSpeaker != null) { subSpeaker.Set(BlueSpeaker); subSpeaker.color = OzUi.BlueText; }
            SetCue(BlueLine);
        }

        /// <summary>The kit's NO SIGNAL card without its bottom line (rows 280-312 of the 312 px card: SELECT A UNIT), made once.</summary>
        static Sprite BlueCardSprite()
        {
            if (blueCardSprite || blueCardFailed) return blueCardSprite;
            var s = OzKit.GetSprite("Portrait/portrait_nosignal");
            if (!s) { blueCardFailed = true; return null; }
            try
            {
                var r = s.textureRect;                                                                  // the card's own pixels (also inside an atlas)
                float cut = r.height * (32f / 312f);
                blueCardSprite = Sprite.Create(s.texture, new Rect(r.x, r.y + cut, r.width, r.height - cut), new Vector2(0.5f, 0.5f), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                blueCardSprite.name = "portrait_nosignal_blue_channel";
            }
            catch (System.Exception) { blueCardFailed = true; blueCardSprite = null; }                  // a tightly packed sprite: the words alone
            return blueCardSprite;
        }

        /// <summary>Tick(): the card's steady static and its dim live dot (after the window's own frame).</summary>
        void TickBlueCard(float now)
        {
            if (!blueOn) return;
            if (!CardInWindow || state == State.Closed) { blueOn = false; return; }                     // built again for a transmission: the card went with the old window
            if (state == State.Open && noise)
            {
                float k = 0.2f + 0.08f * Mathf.PerlinNoise(now * 3.1f, 0.7f);                            // a channel with nothing on it
                if (noise.color.a < k) noise.color = new Color(1f, 1f, 1f, k);
            }
            if (dot) dot.color = OzUi.A(OzUi.BlueHi, 0.55f);                                           // no live signal: steady, dim
        }
    }
}
