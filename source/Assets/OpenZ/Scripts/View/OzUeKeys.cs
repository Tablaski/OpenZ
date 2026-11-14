using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUnitEditor.cs): the editor's keys (DESIGN_SPEC 11.1, ARCHITECTURE D13). Space play / pause,
    /// T table, B balance tab, I stats box, P dock, H hide all panels, Home fit, Delete remove the picked, 1 / 2 / 3 new units blue /
    /// red / neutral (as the player SEES the colours: 1 = the side drawn blue), [ / ] the balance stat, Esc = the chain below. Letters
    /// are read by the letter the keyboard layout prints on the key (OzKeyNames.LetterPressed, as the confirm dialog's Y / N), the
    /// digits and the brackets by place. Ignored while a Custom number box has the keyboard (OzUeHooks.Typing: Esc and Enter are the
    /// box's), with Ctrl / Alt / Cmd held, under the confirm dialog, and while an editor dialog is open (Esc closes it). A key that the
    /// player gave to a game action the playground keeps (stop, patrol, exit crew, nearest only, the camera keys) stays that action.
    /// Esc walks the closers the modules register, highest priority first, until one closes something; with nothing else to close it
    /// asks GO TO THE MAIN MENU? (UI/OzUeLeave.cs: the field paused while it shows; Esc again = STAY). Every key action is answered by a
    /// sound: the action's own, else the HUD click (AudioDirector keeps one per frame).
    /// </summary>
    public static class OzUeKeys
    {
        public const int EscDialog = 120, EscPopover = 110, EscCard = 100, EscTable = 90, EscPlacing = 80, EscUnpick = 70,
                         EscObject = 60, EscBalance = 50, EscInspector = 40;
        /// <summary>The leave question and its confirms (over every other closer), and the last of all: nothing else was open.</summary>
        public const int EscLeave = 130, EscNothingOpen = 0;

        struct Closer { public int priority, order; public Func<bool> tryClose; }
        static readonly List<Closer> closers = new List<Closer>();
        static int registered;
        /// <summary>Checks: editor keys handled and Esc presses that closed something since the editor opened.</summary>
        public static int Handled, EscClosed;
        /// <summary>Checks: the last key the editor acted on ("SPACE", "T", "ESC" ...).</summary>
        public static string LastKey;

        /// <summary>Registers an Esc closer (once, when a module builds). tryClose returns true when it closed something.</summary>
        public static void OnEsc(int priority, Func<bool> tryClose)
        {
            if (tryClose == null) return;
            closers.Add(new Closer { priority = priority, order = registered++, tryClose = tryClose });
            closers.Sort((a, b) => a.priority != b.priority ? b.priority.CompareTo(a.priority) : a.order.CompareTo(b.order));
        }

        /// <summary>Esc: the first closer (highest priority) that closes something wins. True when one did.</summary>
        public static bool Escape()
        {
            for (int i = 0; i < closers.Count; i++)
            {
                bool closed = false;
                try { closed = closers[i].tryClose(); }
                catch (Exception e) { Debug.LogException(e); }
                if (closed) { EscClosed++; return true; }
            }
            return false;
        }

        /// <summary>The closers registered now (the proof counts them).</summary>
        public static int CloserCount => closers.Count;

        /// <summary>The editor closed: no closer of this session may be called by the next one.</summary>
        internal static void Reset() { closers.Clear(); registered = 0; Handled = 0; EscClosed = 0; LastKey = null; }

        /// <summary>The side drawn blue / red (TeamColors.Shown: the YOUR TEAM COLOUR option can draw the red side blue).</summary>
        public static Team ShownBlue => TeamColors.Shown(Team.Blue) == Team.Blue ? Team.Blue : Team.Red;
        public static Team ShownRed => ShownBlue == Team.Blue ? Team.Red : Team.Blue;
    }

    /// <summary>Reads the editor's keys once a frame (order -35: before the number boxes at 0, so a box that ends typing this frame
    /// cannot also trigger an editor key; after the HUD at -45).</summary>
    [DefaultExecutionOrder(-35)]
    internal sealed class OzUeKeysDriver : MonoBehaviour
    {
        static readonly OzAction[] Kept = { OzAction.NearestOnly, OzAction.ExitCrew, OzAction.Patrol, OzAction.StopUnits, OzAction.HideHud,
                                            OzAction.ScrollUp, OzAction.ScrollDown, OzAction.ScrollLeft, OzAction.ScrollRight };
        bool typedLast;

        void Update()
        {
            if (!OzUnitEditor.UiBuilt) return;
            var kb = Keyboard.current; if (kb == null) return;
            bool typing = OzUeHooks.Typing;
            if (typing || typedLast) { typedLast = typing; return; }                                     // the frame a box lets go of the keyboard is still the box's (its Enter / Esc)
            if (OzConfirm.Blocks || OzControlsTable.Capturing) return;
            var hud = OzUnitEditor.I.Hud;
            if (kb.escapeKey.wasPressedThisFrame) { Esc(); return; }
            if (OzUeHooks.AnyModal) return;                                                              // a dialog: only Esc
            if (hud && hud.Modal) return;                                                                // anything else in front (pause, arrange)
            if (kb.ctrlKey.isPressed || kb.altKey.isPressed || OzTrackpadScroll.CmdHeld(kb)) return;
            if (kb.spaceKey.wasPressedThisFrame && Free(Key.Space)) Act("SPACE", () => OzUeControls.TogglePlay());
            if (Letter(kb, 'T')) Act("T", () => OzUeStats.ToggleTable());
            if (Letter(kb, 'B')) Act("B", () => OzUeStats.ToggleBalance());
            if (Letter(kb, 'I')) Act("I", () => OzUeStats.ToggleBox());
            if (Letter(kb, 'P')) Act("P", () => OzUeControls.ToggleDock());
            if (Letter(kb, 'H')) Act("H", HideAll);
            if (kb.homeKey.wasPressedThisFrame && Free(Key.Home)) Act("HOME", () => OzUeControls.Fit());
            if (kb.deleteKey.wasPressedThisFrame && Free(Key.Delete)) Act("DELETE", Remove, false);
            if (Digit(kb, Key.Digit1, Key.Numpad1)) Act("1", () => OzUeControls.SetTeam(OzUeKeys.ShownBlue));
            if (Digit(kb, Key.Digit2, Key.Numpad2)) Act("2", () => OzUeControls.SetTeam(OzUeKeys.ShownRed));
            if (Digit(kb, Key.Digit3, Key.Numpad3)) Act("3", () => OzUeControls.SetTeam(Team.Neutral));
            if (Bracket(kb, Key.LeftBracket) && OzUeStats.BalanceOpen) Act("[", () => OzUeStats.StepBalanceStat(-1));
            if (Bracket(kb, Key.RightBracket) && OzUeStats.BalanceOpen) Act("]", () => OzUeStats.StepBalanceStat(1));
        }

        void LateUpdate() { typedLast = OzUeHooks.Typing; }

        static void Act(string key, Action a, bool click = true)
        {
            try { a(); }
            catch (Exception e) { Debug.LogException(e); }
            if (click) OzSfx.Ui("ui_click");                                                            // the action's own cue wins over this click (AudioDirector: one UI sound per frame)
            OzUeKeys.Handled++; OzUeKeys.LastKey = key;
        }

        static void Esc()
        {
            if (OzUeKeys.Escape()) { OzSfx.Ui("ui_click"); OzUeKeys.Handled++; OzUeKeys.LastKey = "ESC"; }
        }

        static void HideAll()
        {
            bool hide = !OzUnitEditor.PanelsHidden;
            OzUnitEditor.SetPanelsHidden(hide);
            OzUeToast.Show(hide ? "PANELS HIDDEN. H SHOWS THEM AGAIN." : "PANELS SHOWN");
            Debug.Log("[OpenZ UnitEditor] H: panels " + (hide ? "hidden" : "shown"));
        }

        static void Remove()
        {
            int n = 0;
            try { n = OzUePlayground.RemovePicked(); }
            catch (Exception e) { Debug.LogException(e); }
            if (n <= 0) OzSfx.Ui("ui_disabled");                                                         // nothing picked: the key answers that nothing happened
            else OzSfx.Ui("ui_cancel");                                                                 // spec 11.4: remove = ui_cancel (a second call in the same frame is one sound)
        }

        /// <summary>The key that shows `letter` on this layout went down, and no kept game action is on that key.</summary>
        static bool Letter(Keyboard kb, char letter)
        {
            if (!OzKeyNames.LetterPressed(kb, letter)) return false;
            for (Key k = Key.A; k <= Key.Z; k++)
            {
                var c = kb[k];
                if (c != null && c.wasPressedThisFrame && OzKeyNames.Name(k) == letter.ToString()) return Free(k);
            }
            return true;                                                                                 // a sign key that shows the letter (Turkish, French layouts)
        }

        static bool Digit(Keyboard kb, Key main, Key pad)
        {
            var a = kb[main]; var b = kb[pad];
            return ((a != null && a.wasPressedThisFrame) || (b != null && b.wasPressedThisFrame)) && Free(main);
        }

        /// <summary>The bracket keys by place, unless this layout prints + / = / - on them (then they are the game's speed keys).</summary>
        static bool Bracket(Keyboard kb, Key k)
        {
            var c = kb[k];
            if (c == null || !c.wasPressedThisFrame || !Free(k)) return false;
            return OzKeyNames.SpeedDir(k, c.displayName) == 0;
        }

        /// <summary>No game action the playground keeps is bound to this key.</summary>
        static bool Free(Key k)
        {
            for (int i = 0; i < Kept.Length; i++) if (OzBindings.KeyOf(Kept[i]) == k) return false;
            return true;
        }
    }
}
