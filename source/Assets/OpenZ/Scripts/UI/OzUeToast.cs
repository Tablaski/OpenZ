using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUnitEditor.cs): the editor's toast, DESIGN_SPEC E180 (the HUD's own message plates are hidden
    /// in the playground with the rest of the match HUD). Top centre, top = top bar + 66 u (106 at 1920; + 114 while the arm bar shows),
    /// a small plate (GM900 97 % with a 1 u AMBER_LO edge, ALERT for a refusal), padding 10 16, 12 u caps tracking 0.12 AMBER_HI; one at a
    /// time (a new one replaces the shown one), 2.6 s, in and out in 250 ms (12 u slide), unscaled time, the reduced-motion option
    /// respected. Above every editor layer (Tips), never takes the mouse. Texts: spec 11.3; the caller plays the action's sound.
    /// 1.2 item 109: a toast may carry a small part after its caps, as it is written (11 u INK_2, 12 u after them): the file's name
    /// beside SAVED, the path beside PATH COPIED.
    /// </summary>
    public static class OzUeToast
    {
        public const float Hold = 2.6f, Fade = 0.25f, Slide = 12f, PadX = 16f, H = 37f;
        /// <summary>The small part: 11 u, 12 u after the caps.</summary>
        public const float SubSize = 11f, SubGap = 12f;
        static RectTransform rt; static CanvasGroup cg; static Image edge; static OzText label, small;
        static float shownAt = -100f, textW, subW; static bool danger, pending;
        /// <summary>Checks: toasts shown since the editor opened; the last text and its small part (null when it had none).</summary>
        public static int Shown;
        public static string Last, LastSub;

        /// <summary>Shows a toast (caps in the game's toast), replacing the one on screen.</summary>
        public static void Show(string text, bool danger = false) => Show(text, danger, null);

        /// <summary>A toast with a small part after its caps, shown as it is written (a file's name, a path).</summary>
        public static void Show(string text, bool danger, string sub)
        {
            if (string.IsNullOrEmpty(text)) return;
            Last = text.ToUpperInvariant(); LastSub = string.IsNullOrEmpty(sub) ? null : sub; OzUeToast.danger = danger; Shown++;
            shownAt = OzMotion.Now; pending = true;
            Debug.Log("[OpenZ UnitEditor] toast: " + Last + (LastSub != null ? "  " + LastSub : ""));
            if (rt) Paint();
        }

        /// <summary>The toast is on screen now.</summary>
        public static bool Visible => rt && rt.gameObject.activeSelf;

        internal static void Build(RectTransform layer)
        {
            rt = Hud.Node("OzUe_Toast", layer);
            rt.Place(new OzRect(0, 0, 200, H));
            cg = rt.gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false; cg.alpha = 0f;
            if (OzKit.Available) Hud.Shadow(rt, "Panels/panel_sm_mask");
            edge = Hud.Kit(rt, "Edge", "Panels/panel_sm_mask", new OzRect(0, 0, 10, 10), OzUi.AmberLo); edge.rectTransform.Fill();
            var body = Hud.Kit(rt, "Body", "Panels/panel_sm_mask", new OzRect(0, 0, 10, 10), OzUi.A(OzUi.GM900, 0.97f)); body.rectTransform.Fill(1, 1, 1, 1);
            label = Hud.Label(rt, "Text", "", 12, OzUi.AmberHi, 0.12f).FillParent(OzH.C, PadX, 0, PadX, 0);
            OzUeDialogs.Literal(label);                                                                     // a toast may name a file or quote its own words: shown as they are
            small = Hud.Label(rt, "Sub", "", SubSize, OzUi.Ink2, 0f, OzStyle.Plain);                       // its small part (a file's name, a path), as written
            OzUeDialogs.Literal(small); small.SetActive(false);
            rt.gameObject.SetActive(false);
            if (pending && OzMotion.Now - shownAt < Hold + Fade) Paint();                                    // a toast asked for before the UI was built
        }

        static void Paint()
        {
            label.Set(Last);
            textW = label.WidthAt(Last);
            bool two = LastSub != null && small != null;                                                    // the caps on the left, the small part on the right
            if (small != null) small.SetActive(two);
            if (two) { small.Set(LastSub); subW = small.WidthAt(LastSub); label.FillParent(OzH.L, PadX, 0, PadX, 0); small.FillParent(OzH.R, PadX, 0, PadX, 0); }
            else { subW = 0f; label.FillParent(OzH.C, PadX, 0, PadX, 0); }
            label.color = danger ? (Color)OzUi.AlertText : OzUi.AmberHi;
            edge.color = danger ? (Color)OzUi.Alert : OzUi.AmberLo;
            rt.SetAsLastSibling();
            if (!rt.gameObject.activeSelf) rt.gameObject.SetActive(true);
        }

        /// <summary>Per frame from the shell: place (the canvas may have changed) and fade.</summary>
        internal static void Tick(float topBar, bool armBar, Vector2 canvas)
        {
            if (!rt || !rt.gameObject.activeSelf) return;
            float t = OzMotion.Now - shownAt, fade = Mathf.Max(0.001f, OzMotion.Dur(Fade, true));
            if (t > Hold + fade) { rt.gameObject.SetActive(false); pending = false; return; }
            float k = t < fade ? t / fade : t < Hold ? 1f : 1f - (t - Hold) / fade;
            k = Mathf.Clamp01(k);
            cg.alpha = OzEase.OutCubic(k);
            float w = Mathf.Min(canvas.x - 32f, Mathf.Ceil(textW + (subW > 0f ? SubGap + subW : 0f) + 2f * PadX + 2f));
            float y = topBar + (armBar ? 114f : 66f) - (OzMotion.Reduced ? 0f : Slide * (1f - OzEase.OutCubic(k)));
            rt.Place(new OzRect(Mathf.Round((canvas.x - w) * 0.5f), Mathf.Round(y), w, H));
        }

        /// <summary>The editor closed.</summary>
        internal static void Reset() { rt = null; cg = null; edge = null; label = null; small = null; pending = false; shownAt = -100f; Shown = 0; Last = null; LastSub = null; subW = 0f; }
    }
}
