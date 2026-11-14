using UnityEngine;

namespace OpenZ
{
    // ============================================================================================================================
    // 1.4 camera controls (UI/OzCamControls.cs): the pause menu's OPTIONS rows, as the main menu's (UI/OpenZMainMenu.CamControls.cs).
    // BuildOptions calls CamControlRows where it built the FREE CAMERA switch; RefreshValues asks CamRowValue for the two value rows.
    // MIDDLE MOUSE and SCROLL SPEED are value rows in this menu's form (a click steps on, a right click or Shift steps back, the left
    // arrow steps back); SCROLL SPEED also has its slider; RIGHT-DRAG SCROLL is a switch. Tab, place and tooltip: OzSettingTips.
    // ============================================================================================================================
    public partial class OpenZPauseMenu
    {
        void CamControlRows(RectTransform p)
        {
            float w = OzLayout.PauseW - 2 * OzLayout.OptX;
            AddToggle(p, toggles.Count, OzFreeCam.Label, null, null, true);                                   // MIDDLE MOUSE: TURN VIEW (default) / DRAG MAP / DRAG MAP, ALT TURNS VIEW
            {
                var mm = toggles[toggles.Count - 1];
                mm.value.At(w - 16, OzLayout.OptToggleH * 0.5f, OzH.R, true, OzSettings.ResValueW);          // the wide value box, as RESOLUTION
                mm.row.OnClick = c => { OzCamControls.MiddleStep(c.right || c.shift ? -1 : Arrow(mm)); OzSfx.Ui("ui_tab_switch"); };
                mm.row.OnRightClick = c => OzCamControls.MiddleStep(-1);
            }
            AddToggle(p, toggles.Count, OzCamControls.SpeedLabel, null, null, true);                         // SCROLL SPEED: 50 to 300 %, default 100 %
            {
                var sp = toggles[toggles.Count - 1];
                sp.value.At(w - 16, OzLayout.OptToggleH * 0.5f, OzH.R, true, OzScrollSpeedSlider.ValueW);    // the value with its arrows, right of the bar
                sp.row.OnClick = c => { if (OzScrollSpeedSlider.TrackClick) return; OzCamControls.SpeedStep(c.right || c.shift ? -1 : Arrow(sp)); OzSfx.Ui("ui_tab_switch"); };
                sp.row.OnRightClick = c => OzCamControls.SpeedStep(-1);
                OzScrollSpeedSlider.Attach(sp.row, sp.value, w, id => OzSfx.Ui(id));
            }
            AddToggle(p, toggles.Count, OzCamControls.RightLabel, () => OzCamControls.RightDrag, v => OzCamControls.RightDrag = v);   // RIGHT-DRAG SCROLL: default OFF
        }

        /// <summary>RefreshValues: the value of a camera row (true: written, the row is done).</summary>
        bool CamRowValue(Toggle t)
        {
            if (t.label == OzFreeCam.Label) { t.value.Set(OzText.Tab($"\u25C0  {OzCamControls.MiddleName}  \u25B6")); return true; }
            if (t.label == OzCamControls.SpeedLabel) { t.value.Set(OzScrollSpeedSlider.ValueText(OzCamControls.ScrollPercent)); return true; }
            return false;
        }
    }
}
