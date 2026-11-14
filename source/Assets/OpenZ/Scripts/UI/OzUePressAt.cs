using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 70 (the unit editor's dock, UI/OzUeDock.cs): where the
    /// left button really went down. The HUD reads the pointer once per frame, so a press made during a long frame (the playground drawing
    /// or loading) is seen where the mouse is AFTER that frame: a press on a tile followed by a quick pull then landed beside the tile and
    /// no drag started. This reads the mouse's own input events: the screen point of the event that took the left button down, and the
    /// frame it was seen in. Hook / Unhook: the dock while it exists.
    /// </summary>
    public static class OzUePressAt
    {
        /// <summary>The screen point (px) of the last left press, read from its input event.</summary>
        public static Vector2 Pos { get; private set; }
        /// <summary>The frame that press was seen in (-1 none).</summary>
        public static int Frame { get; private set; } = -1;
        /// <summary>Checks: left presses read from the events since the hook went on.</summary>
        public static int Seen;
        static bool hooked, merging0;

        // The input system merges a mouse event into the next one when both have the same buttons down: the press and the pull that
        // follows it in the same frame came as one event at the pulled-to point, so the press point was lost (the quick flick). While
        // the dock is up every mouse event is kept (merging off), so the press event is seen where it happened; put back on Unhook.
        public static void Hook() { if (hooked) return; hooked = true; Seen = 0; Frame = -1; merging0 = InputSystem.settings.disableRedundantEventsMerging; InputSystem.settings.disableRedundantEventsMerging = true; InputSystem.onEvent += OnEvent; }
        public static void Unhook() { if (!hooked) return; hooked = false; InputSystem.onEvent -= OnEvent; InputSystem.settings.disableRedundantEventsMerging = merging0; }

        /// <summary>The press seen in this frame's input update: its screen point.</summary>
        public static bool ThisFrame(out Vector2 at) { at = Pos; return hooked && Frame == Time.frameCount; }

        static void OnEvent(InputEventPtr e, InputDevice d)
        {
            if (!(d is Mouse m) || m != Mouse.current) return;
            if (!e.IsA<StateEvent>() && !e.IsA<DeltaStateEvent>()) return;
            try
            {
                // the event is seen before the mouse's state takes it: the button's value now = before this event
                if (!m.leftButton.ReadValueFromEvent(e, out float down) || down < 0.5f || m.leftButton.ReadValue() >= 0.5f) return;
                Pos = m.position.ReadValueFromEvent(e, out Vector2 p) ? p : m.position.ReadValue();
                Frame = Time.frameCount; Seen++;
            }
            catch (System.Exception) { }                                                                   // never let a check break the input update
        }
    }
}
