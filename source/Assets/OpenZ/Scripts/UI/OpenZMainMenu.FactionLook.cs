using TMPro;
using UnityEngine;

namespace OpenZ
{
    // ============================================================================================================================
    // k83 (1.2 look fixes on the faction screen; the rules and the reasons: UI/OzFactionLook.cs). Hooks in UI/OpenZMainMenu.FactionDeck.cs:
    // FcBuildContent (FcWake: the card on before its words are made), FcLabel (FcUiText), TickFactionVs (FcHaloLook), CloseFactionDeck
    // (FcHaloLookClose).
    //  * The DOSSIER card is switched on before its words are made and measured, and its texts are UI texts from the start: the long rows
    //    break after their label (the mock-up), the two-line rows get their real height.
    //  * The VS sign's halo is drawn with the poster shader's halo mode (_Print 3), told the screen's colour behind the VS each frame (the
    //    backdrop with the mood's wash and glow there, as the posters' paper is told), so it reads as drawn, not as a linear-light cloud.
    // ============================================================================================================================
    public partial class OpenZMainMenu
    {
        Material fcHaloMat;

        /// <summary>FcBuildContent: the card is switched on before its words are made. TextMeshPro sets a text's UI flag when it wakes; a
        /// text made on a card that is still off measured at a tenth of its size, so every row seemed to fit on one line.</summary>
        static void FcWake(RectTransform card) { if (card && !card.gameObject.activeSelf) card.gameObject.SetActive(true); }

        /// <summary>FcLabel: a card text is a UI text from its first measure (what its wake-up would set).</summary>
        static void FcUiText(TMP_Text t) { if (t && !t.isOrthographic) t.isOrthographic = true; }

        /// <summary>TickFactionVs, after the halo's colour: the halo drawn with the halo mode, told the screen's colour behind the VS.</summary>
        void FcHaloLook()
        {
            if (!fcVsHalo || !fcVsHalo.enabled) return;
            if (!fcHaloMat)
            {
                if (!fcRig || !fcRig.Figure) return;
                fcHaloMat = new Material(fcRig.Figure) { name = "OzPosterFigure (faction VS halo)", hideFlags = HideFlags.DontSave };
                OzFactionLook.SetHalo(fcHaloMat);
            }
            if (fcVsHalo.material != fcHaloMat) fcVsHalo.material = fcHaloMat;
            fcHaloMat.SetColor(FcPaperBehind, FcVsBehind());
        }

        /// <summary>The screen's colour behind the VS sign (sRGB): the backdrop, the mood's even wash and its round glow there.</summary>
        Color FcVsBehind()
        {
            int gs = fcMood < 0f ? OzFaction.Red : OzFaction.Blue;
            var gp = fc[gs];
            if (gp == null) return OzFaction.PaperBehind(fcMood, 0f);
            var vs = fcLayout.vs;
            float dx = (vs.center.x - (gp.x + gp.w * 0.5f)) * fcLayout.scale, dy = (vs.center.y - (gp.y + gp.h * 0.5f)) * fcLayout.scale;
            return OzFaction.PaperBehind(fcMood, OzFaction.GlowAt(dx, dy));
        }

        /// <summary>CloseFactionDeck: the halo's material goes with the screen.</summary>
        void FcHaloLookClose()
        {
            if (fcVsHalo && fcVsHalo.material == fcHaloMat) fcVsHalo.material = null;
            if (fcHaloMat) { Destroy(fcHaloMat); fcHaloMat = null; }
        }

        // ---------------------------------------------------------------- proof hooks (View/OzLookFixesProof.cs, Play only)
        public Material ProofVsHaloMat => fcHaloMat;
        public Color ProofVsBehind => FcVsBehind();
    }
}
