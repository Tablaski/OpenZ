using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    // ============================================================================================================================
    //
    // Every row shows the side its victory was won with, between the place and the rank: a small chip in the side's team paint with the
    // general's name, ZOD on red, EVILZOD on blue (OzScoreSides; the factions' own colours, the option YOUR TEAM COLOUR does not change
    // them). The chip is the screen's chip (Hud.Chip, the BEST chip's and the end screen's TOP 10 sign's kind), 70 x 16 u, its words
    // 10 u INK centred; every chip of the table is as wide as the longest tag, so the column stands straight. The column's title FACTION
    // stands over the chips, centred. An entry from before the side was kept reads ZOD.
    // The chip is not one of the table's cells (HsCols): the cells keep their numbers (place 0, rank 1, score 2 ... zones 7).
    // Its words join the popup's texts (hsTexts), so every check of the popup's texts sees them.
    // Hooks in OpenZMainMenu.HighScores.cs: BuildHighScoresBlock -> HsSideTitle, HsSideTagMake; FillHighScores -> HsSideFill.
    // ============================================================================================================================
    public partial class OpenZMainMenu
    {
        /// <summary>The tag's chip in a row (view units): left edge, width, height (centred in the 22 u row).</summary>
        public const float HsSideX = 42f, HsSideW = 70f, HsSideH = 16f;

        sealed class HsSideTag { public RectTransform rt; public Image bg; public OzText text; public int shown = -1; }

        /// <summary>BuildHighScoresBlock, the column titles: FACTION over the chips.</summary>
        void HsSideTitle(RectTransform cols)
        {
            HsLabel(cols, "Col_FACTION", "FACTION", 9, OzUi.Muted, 0.2f).At(HsSideX + HsSideW * 0.5f, HsColHead * 0.5f, OzH.C, true, HsSideW);
        }

        /// <summary>BuildHighScoresBlock, every row: the chip (red ZOD until a row is filled).</summary>
        HsSideTag HsSideTagMake(RectTransform row)
        {
            var c = Hud.Chip(row, "Side", new OzRect(HsSideX, (HsRowH - HsSideH) * 0.5f, HsSideW, HsSideH), OzScoreSides.Tag[OzScoreSides.Red],
                             OzScoreSides.Paint(OzScoreSides.Red), OzUi.Ink, 10, 0.16f);
            hsTexts.Add(c.text);
            return new HsSideTag { rt = c.bg.transform.parent as RectTransform, bg = c.bg, text = c.text, shown = OzScoreSides.Red };
        }

        /// <summary>FillHighScores, every shown row: the entry's side.</summary>
        void HsSideFill(HsSideTag t, OzScoreEntry e)
        {
            if (t == null) return;
            int s = OzScoreSides.Side(e);
            if (s == t.shown && t.text.text == OzScoreSides.Tag[s]) return;
            t.shown = s;
            t.text.Set(OzScoreSides.Tag[s]);
            t.bg.color = OzScoreSides.Paint(s);
        }

        // ---------------------------------------------------------------- checks (Play only)
        /// <summary>A row's tag as shown ("ZOD" / "EVILZOD"), null when that row is not shown.</summary>
        public string ProofHighScoresSide(int level, int row)
        {
            if (ProofHighScoresRows(level) <= row || row < 0) return null;
            var t = hsBlocks[level].rows[row].side;
            return t != null ? t.text.text : null;
        }
        /// <summary>A row's chip colour (the side's paint), clear when that row is not shown.</summary>
        public Color ProofHighScoresSidePaint(int level, int row)
        {
            if (ProofHighScoresRows(level) <= row || row < 0) return Color.clear;
            var t = hsBlocks[level].rows[row].side;
            return t != null && t.bg ? t.bg.color : Color.clear;
        }
        /// <summary>A row's chip and its words (null when that row is not shown).</summary>
        public RectTransform ProofHighScoresSideRt(int level, int row) => ProofHighScoresRows(level) > row && row >= 0 && hsBlocks[level].rows[row].side != null ? hsBlocks[level].rows[row].side.rt : null;
        public OzText ProofHighScoresSideText(int level, int row) => ProofHighScoresRows(level) > row && row >= 0 && hsBlocks[level].rows[row].side != null ? hsBlocks[level].rows[row].side.text : null;
    }
}
