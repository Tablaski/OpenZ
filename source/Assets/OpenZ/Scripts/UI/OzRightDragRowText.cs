using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.4 RIGHT-DRAG SCROLL in the CONTROLS tables (UI/OzCamControls.cs): the orders row (the right click's row, in the main menu's
    /// table and in the short table of the pause menu and the help overlay) also names the right drag while the row is ON, and reads
    /// as built while it is OFF (the default). OzFreeCam.ControlsRow hands it every row that is not the camera row; only the two
    /// orders rows get it. Checked when the table is shown and every frame while it stays open (one bool compare).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OzRightDragRowText : MonoBehaviour
    {
        OzText label; string asBuilt; int shown = -1;

        /// <summary>The text on the row now (for the checks).</summary>
        public string Text => label != null ? label.text : "";
        public bool ShowsRightDrag => shown == 1;

        /// <summary>OzFreeCam.ControlsRow: an orders row gets the switch; every other row is left as it is. Returns the label.</summary>
        public static OzText Hook(OzText label, string text)
        {
            if (label == null || label.rectTransform == null || OzCamControls.OrdersRowText(text, false) == null) return label;
            var sw = label.rectTransform.gameObject.AddComponent<OzRightDragRowText>();
            sw.label = label; sw.asBuilt = text; sw.shown = -1; sw.Refresh();
            return label;
        }

        void OnEnable() { shown = -1; Refresh(); }
        void Update() => Refresh();

        /// <summary>Sets the text for the row as it is now (also called by the checks while the table is hidden).</summary>
        public void Refresh()
        {
            if (label == null) return;
            int want = OzCamControls.RightDrag ? 1 : 0;
            if (want == shown) return;
            shown = want; label.Set(OzCamControls.OrdersRowText(asBuilt, want == 1));
        }
    }
}
