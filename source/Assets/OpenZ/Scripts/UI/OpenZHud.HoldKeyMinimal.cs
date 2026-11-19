// OpenZ (GPL-3.0)
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    /// <summary>
    /// Why H did nothing in a match: H has never been an interface key there. Since 1.0 the match's H is HOLD POSITION (View/OzBindings.cs: an
    /// order for the selected units; with nothing selected it did nothing at all, not even a sound). A match hides its panels with the MINIMAL
    /// UI key (U) and the whole HUD with F10. The H that hides the panels is the Unit editor's own key (View/OzUeKeys.cs HideAll: "PANELS
    /// HIDDEN. H SHOWS THEM AGAIN."), the same code in 1.4 and 1.5; no key was saved over (the game's saved settings hold no oz_key_ rebinding).
    /// Now a match does what the Unit editor does: the HOLD POSITION key (H) pressed with NOTHING selected flips MINIMAL UI, exactly as its
    /// own key does (MinimalUiKey: the OPTIONS switch, saved at once, its sounds and its note "MINIMAL UI ON · KEY U"). With units selected
    /// it stays HOLD POSITION, as before. Not in the Unit editor (its own H hides its panels), not with Ctrl / Alt / Cmd held, not while
    /// the two actions share one key, never under a menu (MinimalUiKey refuses there). HoldKeyMinimalOn off = as before this kit.
    /// </summary>
    public partial class OpenZHud
    {
        public static bool HoldKeyMinimalOn = true;
        /// <summary>Checks: MINIMAL UI flips made by the HOLD POSITION key with nothing selected since the game started.</summary>
        public static int HoldKeyMinimalFlips { get; private set; }

        /// <summary>OpenZHud.Update, a frame the MINIMAL UI key itself was not pressed.</summary>
        void HoldKeyMinimalTick(Keyboard kb)
        {
            if (!HoldKeyMinimalOn || kb == null || OzUnitEditor.Active || input == null || input.Selection.Count > 0) return;
            if (kb.ctrlKey.isPressed || kb.altKey.isPressed || OzTrackpadScroll.CmdHeld(kb)) return;
            if (OzBindings.KeyOf(OzAction.HoldPosition) == OzBindings.KeyOf(OzAction.MinimalUi) || !OzBindings.Pressed(OzAction.HoldPosition)) return;
            if (!MinimalUiKey()) return;
            HoldKeyMinimalFlips++;
            Debug.Log($"[OpenZ HUD] {OzBindings.Cap(OzAction.HoldPosition)} with nothing selected: MINIMAL UI {(OzSettingTips.MinimalUi ? "ON" : "OFF")}");
        }
    }
}
