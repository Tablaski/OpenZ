using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    // ============================================================================================================================
    // 88-90) picked on the FACTION SCREEN before the level selection (UI/OpenZMainMenu.Faction.cs: GENERAL ZOD = red, GENERAL EVILZOD =
    // blue). The faction screen sets the box's own setting (OzPlayBlue.Box, oz_play_blue, saved at once) and the player's own DEPLOY arms
    // the mode from it as before. The level selection's switch and its key B are gone; the OBJECTIVE card's tag line names the side:
    //   [OBJECTIVE ......... GENERAL EVILZOD]        the side's name in the blocks' label style: red text for Zod, blue for EvilZod
    //   [fort] BREAK INTO THE RED FORT                (blue: the red fort glyph and words)
    //   13 FLAGS · CAPTURE FOR TERRITORY               (both sides alike)
    //  * The card's width counts the longer name, so nothing moves when the side or the level changes.
    //  * Not offered in 'Zod exact' (the name hidden there; the faction screen locks the EvilZod poster).
    //  * While the side is blue, the SHUTTLE block of levels 16 and 17 is locked ("NOT AS BLUE" / "RED SIDE ONLY"): the start shuttles are
    //    the red fort's.
    //  * TogglePlayBlue stays as the older proofs' hook (it swaps the side, as a pick on the faction screen does).
    // Hooks: DeployBar.cs (the card's width, the build, the card's words, the refresh), ShuttleStart.cs (the lock), OpenZMainMenu.cs (DEPLOY
    // arms the mode, Awake disarms it).
    // ============================================================================================================================
    public partial class OpenZMainMenu
    {
        /// <summary>The old PLAY AS BLUE key (the level selection no longer reads it: the side is picked on the faction screen).</summary>
        public const Key PlayBlueKey = Key.B;
        /// <summary>The least room between the card's tag OBJECTIVE and the side's name (canvas units).</summary>
        const float PbGapTag = 8f;

        RectTransform pbRoot, pbHot, pbTip; OzText pbLabel; OzText shuttleLockRow;
        float pbLabelW, pbGap; int pbShownState = -1;
        /// <summary>Checks: toggles by click or key, key presses where the box is not offered.</summary>
        public static int PlayBlueToggles { get; private set; }
        public static int PlayBlueKeyRefused { get; private set; }

        /// <summary>BuildObjectiveCard's Need(): the room the tag line takes (OBJECTIVE, the least gap, the longer of the two sides' names),
        /// so the card fits both sides and never changes size.</summary>
        float PlayBlueNeed()
        {
            if (!PbLabelLive) pbLabel = Hud.Label(cardRt, "PlayBlueLabel", OzFaction.SideName(false), 10, OzUi.Ink2, 0.2f, OzStyle.Plain);   // one per build (a new screen size builds the bar again)
            float lw = 0f;
            for (int b = 0; b < 2; b++) { pbLabel.Set(OzFaction.SideName(b == 1)); lw = Mathf.Max(lw, pbLabel.preferredWidth); }
            pbLabelW = lw;
            cardTag.Set(OzDeployBar.CardTag);
            return cardTag.preferredWidth + PbGapTag + pbLabelW;
        }

        /// <summary>The label belongs to this build's card (the old bar of a rebuild is on its way out).</summary>
        bool PbLabelLive => pbLabel != null && pbLabel.rectTransform && cardRt && pbLabel.rectTransform.IsChildOf(cardRt);

        /// <summary>BuildObjectiveCard, after the card's parts are placed (tx: the text's left edge in the card, w: the card's width): the
        /// side's name at the tag line's right end.</summary>
        void BuildPlayBlue(RectTransform p, OzDeployBar.Layout L, float tx, float w)
        {
            if (!PbLabelLive) PlayBlueNeed();
            float right = w - OzDeployBar.CardBoxPadR, ty = OzDeployBar.LabelY - OzDeployBar.CardY;
            pbRoot = Hud.Node("PlayBlue", cardRt).Place(new OzRect(0, 0, w, OzDeployBar.CardH));
            pbLabel.rectTransform.SetParent(pbRoot, false);
            pbLabel.At(right, ty, OzH.R, false, pbLabelW + 4f);
            cardTag.Set(OzDeployBar.CardTag);
            pbGap = right - pbLabelW - (tx + cardTag.preferredWidth);                                          // the room between OBJECTIVE and the name (a check: at least 6 u)
            pbHot = null; pbTip = null;                                                                      // the switch and its tooltip are gone (the older proof hooks read null)
            pbShownState = -1;
            ShowPlayBlue();
        }

        /// <summary>RefreshDeployConsole, every frame on the battlefield screen: the side's name.</summary>
        void RefreshPlayBlue()
        {
            if (!pbRoot) return;
            ShowPlayBlue();
        }

        /// <summary>The name as the side stands: GENERAL ZOD in red text, GENERAL EVILZOD in blue; hidden where the blue side is not offered
        /// ('Zod exact': always red).</summary>
        void ShowPlayBlue()
        {
            bool offered = OzPlayBlue.Offered(), on = OzPlayBlue.Box;
            if (pbRoot.gameObject.activeSelf != offered) pbRoot.gameObject.SetActive(offered);
            if (!offered) return;
            string name = OzFaction.SideName(on);
            if (pbLabel.text != name) pbLabel.Set(name);
            pbLabel.color = on ? (Color)OzUi.BlueText : (Color)OzUi.RedText;
            int st = on ? 1 : 0;
            if (st != pbShownState) { pbShownState = st; cardFor = -1; }                                    // the card's words follow at once (RefreshObjectiveCard)
        }

        /// <summary>A click on the line or the key (where the box is not offered: ui_disabled).</summary>
        void TogglePlayBlue(bool byKey)
        {
            if (!OzPlayBlue.Offered()) { if (byKey) { PlayBlueKeyRefused++; Sfx("ui_disabled"); } return; }
            bool on = !OzPlayBlue.Box;
            OzPlayBlue.Box = on;                                                                              // saved at once
            PlayBlueToggles++;
            Sfx(on ? "ui_toggle_on" : "ui_toggle_off");
            if (pbRoot) ShowPlayBlue();
            RefreshObjectiveCard(true);
            Debug.Log($"[OpenZ Menu] PLAY AS BLUE box: {(on ? "ON (experimental: you command blue, the CPU red)" : "OFF")} ({OzPlayBlue.Pref} {(on ? 1 : 0)}, saved)");
        }

        /// <summary>The player's own DEPLOY (the DEPLOY button, ENTER, a double click on the level's card; hooks in OpenZMainMenu.cs and
        /// DeployBar.cs): only this one arms PLAY AS BLUE (Deploy -> OzPlayBlue.Arm). A proof, the soak or a test that calls Deploy itself
        /// starts a red level, so a box left on can never turn their matches blue.</summary>
        bool pbByPlayer;
        void DeployByPlayer() { pbByPlayer = true; try { Deploy(); } finally { pbByPlayer = false; } }

        /// <summary>RefreshObjectiveCard: the card shows blue's objective while the box is on.</summary>
        bool PlayBlueCard => OzPlayBlue.BoxOn;
        string PlayBlueCardWords(string headline) => OzPlayBlue.CardWords(headline);

        /// <summary>The SHUTTLE block (UI/OpenZMainMenu.ShuttleStart.cs): locked while the box is on (levels 16 and 17).</summary>
        bool PlayBlueLocksShuttle() => OzPlayBlue.LocksShuttle(levels[focusCard].Id);

        /// <summary>ShowShuttle: the locked slot's words name its cause: HARD ("NOT ON HARD"), else the box ("NOT AS BLUE").</summary>
        void PlayBlueShuttleWords()
        {
            bool blue = PlayBlueLocksShuttle() && OpenZDifficulty.Level != GameDifficulty.Hard;
            string row = blue ? OzPlayBlue.ShuttleLockedRow : OzShuttleStart.LockedRowFor(OpenZDifficulty.Choice), hint = blue ? OzPlayBlue.ShuttleLockedHint : OzShuttleStart.LockedHint;
            if (shuttleLockRow != null && shuttleLockRow.text != row) shuttleLockRow.Set(row);
            if (shuttleLockHint != null && shuttleLockHint.text != hint) shuttleLockHint.Set(hint);
        }

        // ---------------------------------------------------------------- proof hooks (OzPlayBlueProof, Play only)
        public RectTransform ProofPlayBlueHot => pbHot;
        public RectTransform ProofPlayBlueLine => pbRoot;
        public RectTransform ProofPlayBlueTip => pbTip;
        /// <summary>The room between the card's tag OBJECTIVE and the PLAY AS BLUE label (canvas units).</summary>
        public float ProofPlayBlueGap => pbGap;
        public void ProofPlayBlueTipHover(bool on) { }                                                         // k75a: the switch's tooltip is gone (the older proof's call does nothing)
        public void ProofTogglePlayBlue(bool byKey) => TogglePlayBlue(byKey);
        public void ProofDeploy() => DeployByPlayer();                                                       // the Play proof deploys as the player does (a click on DEPLOY)
        /// <summary>What the card and the SHUTTLE block say now: "headline | small line | the shuttle block's locked words (or LIVE)".</summary>
        public string ProofPlayBlueText => (cardHead != null ? cardHead.text : "") + " | " + (cardSub != null ? cardSub.text : "") + " | "
                                           + (shuttleLockK > 0.5f ? (shuttleLockRow != null ? shuttleLockRow.text : "") + " / " + (shuttleLockHint != null ? shuttleLockHint.text : "") : "LIVE");
    }
}
