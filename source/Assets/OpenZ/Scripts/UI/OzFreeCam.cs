using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.4: this row is MIDDLE MOUSE now (UI/OzCamControls.cs): TURN VIEW = this switch on, DRAG MAP = off, DRAG MAP, ALT TURNS VIEW = on with oz_middle_alt 1
    /// (the middle button moves the map, with Alt it turns). The saved switch carries over. The lines below describe the switch itself.
    /// FREE CAMERA (Options, GAME tab, under EDGE SCROLL; both the main menu and the pause menu). ON by default; a player who saved it OFF keeps OFF.
    /// ON: the camera behaves exactly as with the testing overlay (P) open: holding the middle mouse button turns (mouse left / right)
    /// and tilts (mouse up / down) the view round the point it looks at, instead of dragging the map; released, the view stays; the
    /// minimap's view box turns with it; keys, the screen edge, the wheel, fly-tos and clicks follow the turned view (RtsCamera's free
    /// look, RtsCamera.FreeTurn). OFF again: the view goes back to the fixed north-up view (0.6 s), as when the overlay closes.
    /// The ending's camera lock and the briefing flight end the free look exactly as before. Saved in oz_free_camera (0 / 1), live.
    /// The CONTROLS tables name the free camera's middle-mouse control only while this is on (OzFreeCamRowText).
    /// </summary>
    public static class OzFreeCam
    {
        public const string PrefKey = "oz_free_camera";
        public const string Label = "MIDDLE MOUSE";                                                // 1.4: was FREE CAMERA (UI/OzCamControls.cs)
        public const string Tip = "MIDDLE MOUSE|What the middle mouse button does when you hold it and move the mouse. TURN VIEW (default): it turns and tilts the view, which stays turned; the minimap's view box turns with it. DRAG MAP: it moves the map and the view stays fixed, north up. DRAG MAP, ALT TURNS VIEW: it moves the map; with Alt held it turns and tilts the view.|";

        static int on = -1;
        /// <summary>The player's switch (read once, then kept here; saved at once when set).</summary>
        public static bool On
        {
            get { if (on < 0) on = PlayerPrefs.GetInt(PrefKey, 1) == 1 ? 1 : 0; return on == 1; }
            set
            {
                on = value ? 1 : 0; PlayerPrefs.SetInt(PrefKey, on); PlayerPrefs.Save();
                Debug.Log($"[OpenZ Options] free camera: {(value ? "ON (middle mouse button: turn and tilt)" : "OFF (middle mouse button: drag the map)")} ({PrefKey} {on}, saved, live)");
            }
        }

        // ---- the CONTROLS tables (OzControlsTable): the row that names the middle mouse button reads differently while this is on
        /// <summary>The main menu's CONTROLS table: the camera row, as built (off) and while the free camera is on.</summary>
        public const string FullRowOff = "Zoom · middle drag flies the view", FullRowOn = "Zoom · middle drag turns and tilts the view";   // 1.4: TURN VIEW in plain words
        /// <summary>The pause menu's and the help overlay's short table: the scroll row, as built (off) and while the free camera is on.</summary>
        public const string CompactRowOff = "Scroll (arrows too) · wheel zooms", CompactRowOn = "Scroll · zoom · middle drag turns the view";   // 1.4: TURN VIEW in plain words

        /// <summary>OzControlsTable hook: a row label of the camera row gets a small live text switch; every other row is left as it is.
        /// Returns the label.</summary>
        public static OzText ControlsRow(OzText label, string text)
        {
            if (label == null || label.rectTransform == null) return label;
            string onText = text == FullRowOff ? FullRowOn : text == CompactRowOff ? CompactRowOn : null;
            if (onText == null) return OzTacticalPause.ControlsRow(OzRightDragRowText.Hook(label, text), text);   // 1.5 tactical_pause_megashake: the game speed row's TACTICAL PAUSE key follows the switch;                                // 1.4: an orders row names RIGHT-DRAG SCROLL while it is on (UI/OzRightDragRowText.cs); every other row as built
            var sw = label.rectTransform.gameObject.AddComponent<OzFreeCamRowText>();
            sw.Init(label, text, onText);
            return label;
        }
    }
}
