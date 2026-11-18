using UnityEngine;

namespace OpenZ
{
    // ============================================================================================================================
    // 1.4 camera controls (UI/OzCamControls.cs): the main menu's OPTIONS rows. ExeOptions calls CamControlRows where it built the
    // FREE CAMERA switch: MIDDLE MOUSE (a value row in that place, the wide value box of RESOLUTION), SCROLL SPEED (a value row with
    // the slider, UI/OzScrollSpeedSlider.cs) and RIGHT-DRAG SCROLL (a switch). Their tab and place (GAME, under EDGE SCROLL) and
    // their tooltips come from OzSettingTips, as for every row. Every value is saved at once and live.
    // ============================================================================================================================
    public partial class OpenZMainMenu
    {
        void CamControlRows(RectTransform p)
        {
            float w = OptW - 2 * OzLayout.OptX;
            AddCycle(p, toggles.Count, OzFreeCam.Label, () => OzCamControls.MiddleName, OzCamControls.MiddleStep);   // MIDDLE MOUSE: TURN VIEW (default) / DRAG MAP / DRAG MAP, ALT TURNS VIEW
            toggles[toggles.Count - 1].value.At(w - 16, OzLayout.OptToggleH * 0.5f, OzH.R, true, OzSettings.ResValueW);   // its longest value is wider than the 170 u of a value row
            AddCycle(p, toggles.Count, OzCamControls.SpeedLabel, () => OzCamControls.SpeedName, OzCamControls.SpeedStep);   // SCROLL SPEED: 50 to 300 %, default 100 %
            var sp = toggles[toggles.Count - 1];
            sp.value.At(w - 16, OzLayout.OptToggleH * 0.5f, OzH.R, true, OzScrollSpeedSlider.ValueW);    // the value with its arrows, right of the bar
            var hot = hots[hots.Count - 1];                                                                 // the row's click: an arrow steps; a press on the bar is the slider's
            hot.sfx = null;
            hot.click = () => { if (OzScrollSpeedSlider.TrackClick) return; Sfx("ui_tab_switch"); OzCamControls.SpeedStep(Shift ? -1 : OzSettings.ArrowDir(sp.value, lastMouse)); };
            OzScrollSpeedSlider.Attach(sp.row, sp.value, w, Sfx);
            AddToggle(p, toggles.Count, OzCamControls.RightLabel, () => OzCamControls.RightDrag, v => OzCamControls.RightDrag = v);   // RIGHT-DRAG SCROLL: default OFF
        }

        // ---------------------------------------------------------------- proof hooks (Play only; Editor/OzV14CamControlsCheck.cs)
        /// <summary>OPTIONS open on a tab (the GAME tab holds the camera rows).</summary>
        public void ProofOpenOptions(int tab) { if (side != Side.Options) OpenSide(Side.Options); ShowOptTab(Mathf.Clamp(tab, 0, optTabBtns.Count - 1)); }
        /// <summary>The open side panel closes.</summary>
        public void ProofCloseSide() { if (side != Side.None) CloseSide(); }
    }
}
