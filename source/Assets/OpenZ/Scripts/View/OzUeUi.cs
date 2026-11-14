using System.Text;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (View/OzUnitEditor.cs): the editor's own colour tokens (design spec 3.2; the set colours are P3's OzUeSets.Colour) and
    /// the team words and colours as the player SEES them. The YOUR TEAM COLOUR option can draw the red side blue (TeamColors.Shown):
    /// every RED / BLUE word and team colour in the editor goes through these, never a hard-coded "RED".
    /// </summary>
    public static class OzUeUi
    {
        public static readonly Color PlayGreenTop = new Color32(0x8A, 0xF0, 0xA8, 255), PlayGreenMid = new Color32(0x3F, 0xCF, 0x6C, 255),
                                     PlayGreenLow = new Color32(0x26, 0xA6, 0x50, 255), PlayGreenEdge = new Color32(0x1D, 0x7A, 0x3B, 255),
                                     PlayGreenInk = new Color32(0x06, 0x21, 0x0F, 255);
        public static readonly Color PauseTop = new Color32(0x25, 0x2A, 0x27, 255), PauseLow = new Color32(0x17, 0x1A, 0x18, 255);
        public static readonly Color DeltaUp = new Color32(0x9B, 0xE3, 0x8A, 255), DeltaDown = new Color32(0xFF, 0x9C, 0x86, 255);
        public static readonly Color CustomText = new Color32(0xBF, 0xF2, 0xFB, 255);
        public static readonly Color HoloBase = new Color32(0xE6, 0xEF, 0xE9, 255), NoRoom = new Color32(0xFF, 0x7A, 0x5E, 255);
        public static readonly Color TopBarTop = new Color32(0x1B, 0x20, 0x1D, 255), TopBarLow = new Color32(0x10, 0x13, 0x11, 255);
        public static readonly Color SheetFill = new Color32(0x0D, 0x0F, 0x0E, 255), GridHead = new Color32(0x16, 0x1A, 0x18, 255),
                                     CardHead = new Color32(0x15, 0x19, 0x17, 255), UnitCell = new Color32(0x14, 0x18, 0x16, 255),
                                     DarkInk = new Color32(0x17, 0x13, 0x0A, 255);

        static readonly Color RingRed = new Color32(0xFF, 0x7A, 0x5E, 255), RingBlue = new Color32(0x7F, 0xA4, 0xFF, 255), RingNeutral = new Color32(0xCF, 0xCD, 0xBF, 255);
        static readonly Color GlowRed = new Color32(0xFF, 0x6A, 0x4D, 255), GlowBlue = new Color32(0x6F, 0x9B, 0xFF, 255);

        /// <summary>TEAM_RING: range rings, order lines, drop previews (red #FF7A5E, blue #7FA4FF, neutral #CFCDBF), by the SHOWN colour.</summary>
        public static Color Ring(Team t) { t = TeamColors.Shown(t); return t == Team.Red ? RingRed : t == Team.Blue ? RingBlue : RingNeutral; }
        /// <summary>The team glow (red #FF6A4D, blue #6F9BFF; neutral = its ring colour), by the SHOWN colour.</summary>
        public static Color Glow(Team t) { t = TeamColors.Shown(t); return t == Team.Red ? GlowRed : t == Team.Blue ? GlowBlue : RingNeutral; }
        /// <summary>"RED", "BLUE" or "NEUTRAL" as drawn (TeamColors.Shown).</summary>
        public static string TeamWord(Team t) { t = TeamColors.Shown(t); return t == Team.Red ? "RED" : t == Team.Blue ? "BLUE" : "NEUTRAL"; }
        /// <summary>"Red", "Blue", "Neutral" (sentence case) as drawn.</summary>
        public static string TeamWordSentence(Team t) { t = TeamColors.Shown(t); return t == Team.Red ? "Red" : t == Team.Blue ? "Blue" : "Neutral"; }
        /// <summary>The team's UI mark colour (OzUi.Mark), as drawn.</summary>
        public static Color TeamColor(Team t) => OzUi.Mark(t);
        /// <summary>The team's text colour (OzUi.TeamText), as drawn.</summary>
        public static Color TeamText(Team t) => OzUi.TeamText(t);

        /// <summary>Counted check: the team words follow the shown colours (both ways of the YOUR TEAM COLOUR option).</summary>
        internal static int SelfCheck(StringBuilder log)
        {
            int f = 0;
            bool swap = TeamColors.Shown(Team.Red) == Team.Blue;
            f += OzUnitEditor.Line(log, TeamWord(Team.Red) == (swap ? "BLUE" : "RED") && TeamWord(Team.Blue) == (swap ? "RED" : "BLUE") && TeamWord(Team.Neutral) == "NEUTRAL",
                                   $"team words as drawn (your team colour {(swap ? "swapped" : "as played")}): red side {TeamWord(Team.Red)}, blue side {TeamWord(Team.Blue)}");
            f += OzUnitEditor.Line(log, Ring(Team.Red) != Ring(Team.Blue) && Ring(Team.Neutral) == RingNeutral, "team ring colours: red, blue and neutral differ");
            return f;
        }
    }
}
