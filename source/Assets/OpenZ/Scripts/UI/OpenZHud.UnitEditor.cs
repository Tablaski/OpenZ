using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (View/OzUnitEditor.cs; ARCHITECTURE D11, D15, Q1): the match HUD in the Unit editor's playground. The always-on match
    /// panels (top strip, territory tab, unit panel, command bar), the mission banner and the HUD's own toasts are hidden through the
    /// HUD's MINIMAL UI mechanism (hook in TickMinimal: OzUeHooks.HideMatchHud; the player's saved option is never written); this part
    /// hides the rest that has no use there: the minimap (no level map) and the MENU button (Esc and the top bar's MAIN MENU belong to the
    /// editor), and closes the factory list. Kept: the unit overlay (bars, name chips, brackets), the cursors, the tooltips, PlayerInput's
    /// gating, F10. The editor's own UI lives on this canvas (node OZ_UnitEditor). On short screens the canvas keeps a floor of 0.85 screen
    /// pixels per unit (times the UI scale): 1366 x 768 shows 12 u text at 10 px, as the prototype the owner judged (Q1).
    /// Called by OzUnitEditor every LateUpdate and once before its UI is built.
    /// </summary>
    public partial class OpenZHud
    {
        bool ueHidden;

        /// <summary>One frame of the playground's HUD: the match parts hidden (once), the pixel floor kept.</summary>
        public void UeFrame()
        {
            if (!Canvas || !Root) return;
            if (!ueHidden)
            {
                ueHidden = true;
                if (Minimap) { if (Minimap.Panel) Minimap.Panel.gameObject.SetActive(false); Minimap.enabled = false; }
                if (menuBtn != null && menuBtn.Rt) menuBtn.Rt.gameObject.SetActive(false);
                factoryListOpen = false;
                Debug.Log("[OpenZ HUD] unit editor: match panels hidden (minimal UI forced, minimap and menu button off), the editor's UI on this canvas");
            }
            if (scaler)
            {
                var want = UeReference(Screen.width, Screen.height, OzText.UiScale);
                if ((scaler.referenceResolution - want).sqrMagnitude > 0.01f) scaler.referenceResolution = want;
            }
        }

        /// <summary>The canvas scaler's reference for the playground: the HUD's own (1920 x 1080 / UI scale) while the screen has at least
        /// 0.85 pixels per unit at 1080 lines, else the reference that keeps 0.85 x the UI scale pixels per unit (match height).</summary>
        public static Vector2 UeReference(float w, float h, float uiScale)
        {
            float s = Mathf.Clamp(uiScale, OzText.ScaleMin, OzText.ScaleMax);
            float k = h / OzLayout.RefH;
            if (k >= OzUnitEditor.PixelFloor || h <= 0f) return new Vector2(OzLayout.RefW, OzLayout.RefH) / s;
            float refH = h / (OzUnitEditor.PixelFloor * s);
            return new Vector2(refH * OzLayout.RefW / OzLayout.RefH, refH);
        }
    }
}
