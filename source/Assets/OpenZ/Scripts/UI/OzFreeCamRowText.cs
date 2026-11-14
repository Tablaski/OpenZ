using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The camera row of a CONTROLS table (OzFreeCam.ControlsRow): shows the free camera's middle-mouse control while FREE CAMERA is
    /// on, the row as built while it is off. Checked when the table is shown and every frame while it stays open (one bool compare).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OzFreeCamRowText : MonoBehaviour
    {
        OzText label; string offText, onText; int shown = -1;

        /// <summary>The text on the row now (for the checks).</summary>
        public string Text => label != null ? label.text : "";
        public bool ShowsFreeCamera => shown == (int)OzCamControls.Middle.TurnView || shown == (int)OzCamControls.Middle.DragMapAltTurns;   // 1.4: the free look may turn the view

        public void Init(OzText l, string off, string on) { label = l; offText = off; onText = on; shown = -1; Refresh(); }

        void OnEnable() { shown = -1; Refresh(); }
        void Update() => Refresh();

        /// <summary>Sets the text for the switch as it is now (also called by the checks while the table is hidden).</summary>
        public void Refresh()
        {
            if (label == null) return;
            int want = (int)OzCamControls.MiddleMode;                                                        // 1.4: TURN VIEW 0, DRAG MAP 1, the Alt way 2 (UI/OzCamControls.cs)
            if (want == shown) return;
            shown = want; label.Set(OzCamControls.CameraRowText(offText, onText, want));
        }
    }
}
