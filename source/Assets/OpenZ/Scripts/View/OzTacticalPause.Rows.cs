// OpenZ (GPL-3.0) - 1.5, kit player_reports_1111, P3: with TACTICAL PAUSE on (the default) its key is the LAST MESSAGE key (SPACE), and
// that key freezes time; the last message is then a click on the message (UI/OpenZHud.cs). The CONTROLS tables named that key "Go to the
// last 5 messages" (main menu) and "Exit crew · cycle the last 5 messages" (pause menu, F1 help) all the same. Now the row says what the
// key does while the switch has it, and goes back to the old words when the switch is off or the two keys differ (the player gave one
// of them another key in CONTROLS). The game speed row already names the tactical pause while it is on (RowText in OzTacticalPause.cs).
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzTacticalPause
    {
        /// <summary>The pause menu's and the F1 help's row of the EXIT CREW and LAST MESSAGE keys, as built (UI/OzControlsTable.cs Compact).</summary>
        public const string CompactMessageRow = "Exit crew · cycle the last 5 messages";
        /// <summary>The same rows while the switch has the LAST MESSAGE key.</summary>
        public const string FullMessageRowOn = "Tactical pause · click a message to go there", CompactMessageRowOn = "Exit crew · tactical pause";

        /// <summary>ControlsRow, every row but the game speed row: the LAST MESSAGE rows get the live text; any other row as it is.</summary>
        static OzText MessageRow(OzText label, string text)
        {
            string onText = text == CompactMessageRow ? CompactMessageRowOn : text == OzBindings.DefOf(OzAction.LastMessage).label ? FullMessageRowOn : null;
            if (onText == null) return label;
            var sw = label.rectTransform.gameObject.AddComponent<MessageRowText>();
            sw.Init(label, text, onText);
            return label;
        }

        /// <summary>The LAST MESSAGE row's text follows the switch and the two keys (checked when the table shows and every frame while it is up).</summary>
        [DisallowMultipleComponent]
        public sealed class MessageRowText : MonoBehaviour
        {
            OzText label; string asBuilt, onText; int shown = -1;
            public bool ShowsTacticalPause => shown == 1;
            internal void Init(OzText l, string built, string on) { label = l; asBuilt = built; onText = on; shown = -1; Refresh(); }
            void OnEnable() { shown = -1; Refresh(); }
            void Update() => Refresh();
            public void Refresh()
            {
                if (label == null) return;
                int want = TakesLastMessageKey ? 1 : 0;
                if (want == shown) return;
                shown = want;
                label.Set(want == 1 ? onText : asBuilt);
            }
        }
    }
}
