using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    /// <summary>
    /// The leave question and the RESET
    /// STATS confirm, two game-style windows (UI/OzUeWindow.cs, their plan UI/OzUeWinPlan.cs) on the editor's dialog layer over a 72 % dim.
    ///  LEAVE THE UNIT EDITOR? (Esc when nothing else is open, or MAIN MENU at the top right). While Custom changes are not saved: the one
    ///   line "Your Custom stats are not saved." and STAY (ESC) | LEAVE, SAVE &amp; LEAVE (the main button, ENTER). With nothing to save: no
    ///   line, STAY (ESC) | LEAVE (the main button, ENTER). STAY (Esc, or a click on the dim) gives back exactly what ran: the field stands
    ///   still while the question shows, a running fight runs on at its speed, a paused one stays paused. LEAVE drops the changes not
    ///   saved (the Custom stats go back to the last save: View/OzUeSets.Reset.cs DropUnsaved) and goes at once, no second question (the
    ///   line said it). SAVE &amp; LEAVE keeps the Custom stats as the profile the levels can play and writes openz_custom_stats.json with
    ///   the field (View/OzUeFiles.cs), then goes; a failed write stays here with its toast. No reset here: RESET STATS is in the top bar.
    ///  RESET ALL CUSTOM STATS? (RESET STATS in the top bar): the line "Every unit goes back to OpenZ's own numbers." and CANCEL (ESC, a
    ///   click on the dim) | RESET (the main button, ENTER): every Custom value back to OpenZ's own numbers, the field on OpenZ, the saved
    ///   Custom profile removed (the file is not changed: LOAD brings its values back). With nothing to reset, RESET STATS only says so.
    /// While one shows the HUD is modal (OzUeHooks.SetModal: only its buttons react, the field takes no input). Sounds: ui_panel_open and
    /// ui_panel_close, the HUD's click on every button, ui_confirm when the stats are saved or reset, ui_error when the file cannot be
    /// written, ui_disabled when there is nothing to reset, the click for Enter; leaving plays the shell's ui_menu_back.
    /// </summary>
    internal sealed class OzUeLeave
    {
        enum Mode { None, Leave, Reset }
        internal const int Priority = OzUeWindow.Priority;
        internal const string StayTip = "STAY|Back to the fight|ESC";
        internal const string NoSaveTip = "LEAVE|To the main menu. Changes you did not save are dropped|";
        internal const string SaveGoTip = "SAVE & LEAVE|Saves your Custom stats and the field, then the main menu|ENTER";
        internal const string ResetTip = "RESET|Every Custom value back to OpenZ's own numbers; your saved Custom stats are removed (the file stays)|ENTER";
        /// <summary>The Play proof: the leave buttons do everything but leave, so the proof stays in the playground (only while it runs).</summary>
        internal static bool ProofHold;
        /// <summary>The popup check (View/OzUePopupCheck.cs, editor only) holds the leaves the same way while it runs.</summary>
        internal static bool CheckHold;
        static bool HoldNow => (ProofHold && OzUeProof.Running) || CheckHold;
        /// <summary>Checks: questions and confirms shown, choices made, saves, resets and drops done, leaves held, cancels; the last choice;
        /// the line the question showed last ("" when it had none).</summary>
        internal static int LeaveOpens, ResetOpens, Stays, Goes, Saves, ResetsDone, DropsDone, Held, Cancels;
        internal static string LastChoice, LastStatus;

        readonly object owner = new object();
        readonly Action leaveNow;
        Mode mode; int openFrame = -1; bool pausedHere, unsaved;
        readonly RectTransform dim;
        readonly OzUeWindow leave, reset;
        internal readonly HudButton stay, noSave, saveGo, cancel, resetNow;
        readonly OzUeWinPlan.Win planUnsaved, planSaved, planReset;

        internal bool Open => mode != Mode.None;
        internal bool LeaveOpen => mode == Mode.Leave;
        internal bool ResetOpen => mode == Mode.Reset;
        /// <summary>The question shows its changes-not-saved form (the line and SAVE &amp; LEAVE).</summary>
        internal bool Unsaved => unsaved;
        /// <summary>The leave question paused the field (STAY lets it run again).</summary>
        internal bool PausedHere => pausedHere;
        internal RectTransform LeaveRoot => leave.Root;
        internal RectTransform ResetRoot => reset.Root;
        /// <summary>The plans: the question with changes not saved, with none, the reset confirm.</summary>
        internal OzUeWinPlan.Win LeavePlan => planUnsaved;
        internal OzUeWinPlan.Win LeaveSavedPlan => planSaved;
        internal OzUeWinPlan.Win ResetPlan => planReset;

        internal OzUeLeave(RectTransform layer, OpenZHud hud, Action leaveNow)
        {
            this.leaveNow = leaveNow;
            ProofHold = false; CheckHold = false;
            dim = Hud.Node("OzUe_LeaveDim", layer); dim.Fill();
            var dimImg = Hud.Img(dim, "Dim", new Color(5f / 255f, 7f / 255f, 6f / 255f, 0.72f)); dimImg.rectTransform.Fill();
            if (hud) hud.AddBlocker(dim);
            OzUePlan.Measure m = OzUeShell.TextW;

            // ---------------------------------------------------------------- LEAVE THE UNIT EDITOR?
            leave = new OzUeWindow(dim, "OzUe_Leave", OzUePlan.LeaveTitle, OzUi.Amber, false, null);
            stay = leave.Button("Stay", OzUePlan.Stay, HudButtonStyle.Normal, OzUePlan.KeyEsc, StayTip, Stay);
            noSave = leave.Button("Leave", OzUePlan.NoSave, HudButtonStyle.Normal, OzUePlan.KeyEnter, NoSaveTip, GoWithoutSaving);
            saveGo = leave.Button("SaveAndLeave", OzUePlan.SaveAndGo, HudButtonStyle.Primary, OzUePlan.KeyEnter, SaveGoTip, SaveAndGo);
            planUnsaved = OzUeWinPlan.Plan(OzUePlan.LeaveTitle, OzUePlan.NotSaved, 0, false, false, new[] { OzUePlan.Stay, OzUePlan.NoSave, OzUePlan.SaveAndGo },
                                           new[] { OzUePlan.KeyEsc, null, OzUePlan.KeyEnter }, 1, 2, m);
            planSaved = OzUeWinPlan.Plan(OzUePlan.LeaveTitle, null, 0, false, false, new[] { OzUePlan.Stay, OzUePlan.NoSave }, new[] { OzUePlan.KeyEsc, OzUePlan.KeyEnter }, 1, 1, m);
            Form(false);

            // ---------------------------------------------------------------- RESET ALL CUSTOM STATS?
            reset = new OzUeWindow(dim, "OzUe_ResetStats", OzUePlan.ResetTitle, OzUi.Alert, false, null);
            cancel = reset.Button("Cancel", OzUePlan.Cancel, HudButtonStyle.Normal, OzUePlan.KeyEsc, null, Cancel);
            resetNow = reset.Button("Reset", OzUePlan.ResetNow, HudButtonStyle.Danger, OzUePlan.KeyEnter, ResetTip, Confirm);
            planReset = OzUeWinPlan.Plan(OzUePlan.ResetTitle, OzUePlan.ResetLine, 0, false, false, new[] { OzUePlan.Cancel, OzUePlan.ResetNow }, new[] { OzUePlan.KeyEsc, OzUePlan.KeyEnter }, 1, 1, m);
            reset.SetLine(OzUePlan.ResetLine);
            reset.Apply(planReset, new[] { cancel, resetNow });

            leave.Root.gameObject.SetActive(false); reset.Root.gameObject.SetActive(false); dim.gameObject.SetActive(false);
        }

        /// <summary>The question's form: with Custom changes not saved (the line, SAVE &amp; LEAVE the main button) or with none (no line,
        /// LEAVE the main button with ENTER on its cap).</summary>
        void Form(bool u)
        {
            unsaved = u;
            leave.SetLine(u ? OzUePlan.NotSaved : "");
            leave.Style(noSave, u ? HudButtonStyle.Normal : HudButtonStyle.Primary, !u);
            leave.Apply(u ? planUnsaved : planSaved, u ? new[] { stay, noSave, saveGo } : new[] { stay, noSave });
            LastStatus = u ? OzUePlan.NotSaved : "";
        }

        // ------------------------------------------------------------------ open / close
        /// <summary>LEAVE THE UNIT EDITOR? (MAIN MENU, or Esc with nothing else open). The field stands still while it shows.</summary>
        internal void OpenLeave(Vector2 canvas)
        {
            if (mode == Mode.Leave) return;
            if (mode == Mode.None)
            {
                pausedHere = OzUeControls.Playing;
                if (pausedHere) OzUeControls.SetPlaying(false);
            }
            LeaveOpens++;
            var hud = OpenZHud.I;
            if (hud && !hud.hudVisible) hud.SetVisible(true);                                               // the interface hidden with F10: the question shows with it
            int n = OzUeSets.UnsavedCount;
            Form(n > 0);
            Show(Mode.Leave, canvas);
            Debug.Log("[OpenZ UnitEditor] " + OzUePlan.LeaveTitle + " asked (" + (pausedHere ? "the field paused while it shows" : "the field was paused already") + "): "
                      + (n > 0 ? n + " Custom value(s) not saved" : "nothing to save"));
        }

        /// <summary>RESET STATS in the top bar: the confirm, or a toast when the stats are the defaults already.</summary>
        internal void OpenReset(Vector2 canvas)
        {
            if (mode != Mode.None) return;
            if (!OzUeSets.AnythingToReset)
            {
                OzUeToast.Show(OzUePlan.NothingToReset);
                AudioDirector.UI("ui_disabled");
                Debug.Log("[OpenZ UnitEditor] RESET STATS: nothing to reset (the field uses OpenZ, every Custom value is OpenZ's, no Custom profile saved)");
                return;
            }
            ResetOpens++;
            Show(Mode.Reset, canvas);
        }

        void Show(Mode m, Vector2 canvas)
        {
            mode = m; openFrame = Time.frameCount;
            dim.gameObject.SetActive(true); dim.SetAsLastSibling();
            leave.Root.gameObject.SetActive(m == Mode.Leave);
            reset.Root.gameObject.SetActive(m == Mode.Reset);
            (m == Mode.Leave ? leave : reset).Shown();
            Place(canvas);
            OzUeHooks.SetModal(owner, true);
            AudioDirector.UI("ui_panel_open");
        }

        /// <summary>Closed; unpause = the field runs again when the leave question paused it.</summary>
        void Close(bool sound, bool unpause)
        {
            if (mode == Mode.None) return;
            mode = Mode.None;
            leave.Root.gameObject.SetActive(false); reset.Root.gameObject.SetActive(false); dim.gameObject.SetActive(false);
            OzUeHooks.SetModal(owner, false);
            if (sound) AudioDirector.UI("ui_panel_close");
            if (unpause && pausedHere && !OzUeControls.Playing) OzUeControls.SetPlaying(true);
            pausedHere = false;
        }

        /// <summary>Esc while one shows: STAY in the question, CANCEL in the reset confirm.</summary>
        internal bool EscClose()
        {
            if (mode == Mode.None) return false;
            if (mode == Mode.Leave) Stay(); else Cancel();
            return true;
        }

        internal void Place(Vector2 canvas)
        {
            leave.PlaceOn(canvas);
            reset.PlaceOn(canvas);
        }

        // ------------------------------------------------------------------ the choices
        void Choice(string what) { LastChoice = what; Debug.Log("[OpenZ UnitEditor] " + OzUePlan.LeaveTitle + " " + what); }

        void Stay()
        {
            if (mode != Mode.Leave) return;
            Stays++; Choice(OzUePlan.Stay);
            Close(true, true);
        }

        /// <summary>SAVE &amp; LEAVE: the file and the saved Custom profile (OzUeFiles.Save writes both), then the main menu.</summary>
        void SaveAndGo()
        {
            if (mode != Mode.Leave) return;
            Choice(OzUePlan.SaveAndGo);
            if (OzUeFiles.Save(out _, out string error))
            {
                Saves++;
                OzUeDialogs.SavedToast();
                AudioDirector.UI("ui_confirm");
                LeaveNow();
            }
            else { OzUeToast.Show(error ?? "COULD NOT WRITE THE FILE.", true); AudioDirector.UI("ui_error"); }   // stays: nothing is lost
        }

        /// <summary>LEAVE: the changes not saved are dropped (the Custom stats are the last save again), then the main menu. No second
        /// question: the window's line says the stats are not saved, and SAVE &amp; LEAVE stands beside it.</summary>
        void GoWithoutSaving()
        {
            if (mode != Mode.Leave) return;
            int n = OzUeSets.UnsavedCount;
            if (n > 0) { OzUeSets.DropUnsaved(); DropsDone++; Choice(OzUePlan.NoSave + " (" + n + " unsaved value(s) dropped)"); }
            else { Goes++; Choice(OzUePlan.NoSave); }
            LeaveNow();
        }

        void Cancel()
        {
            if (mode != Mode.Reset) return;
            Cancels++;
            Close(true, false);
        }

        void Confirm()
        {
            if (mode != Mode.Reset) return;
            int n = OzUeSets.ResetAll();
            ResetsDone++;
            AudioDirector.UI("ui_confirm");
            Debug.Log("[OpenZ UnitEditor] RESET STATS: " + n + " Custom value(s) back to OpenZ's own numbers, the field uses OpenZ, no saved Custom profile");
            OzUeToast.Show(OzUePlan.ResetDone);
            Close(false, false);
        }

        /// <summary>Closes and goes to the main menu (the shell's Leave); while a proof or the popup check holds it, the field runs again
        /// instead.</summary>
        void LeaveNow()
        {
            bool unpause = HoldNow;
            Close(false, unpause);
            if (HoldNow) { Held++; Debug.Log("[OpenZ UnitEditor] a check holds the leave: the playground stays"); return; }
            leaveNow?.Invoke();
        }

        /// <summary>The Play proof's answer when its Enter key did not arrive: the same as Enter.</summary>
        internal void ProofEnter() => Enter();

        /// <summary>Enter: the main button (SAVE &amp; LEAVE with changes not saved, else LEAVE; RESET in the confirm).</summary>
        void Enter()
        {
            if (mode == Mode.Leave) { if (unsaved) SaveAndGo(); else GoWithoutSaving(); }
            else if (mode == Mode.Reset) Confirm();
        }

        /// <summary>Per frame: the fade and the hover inks, Enter (with the click: a choice's own sound wins), a press on the dim (= STAY /
        /// CANCEL).</summary>
        internal void Tick()
        {
            if (mode == Mode.None) return;
            (mode == Mode.Leave ? leave : reset).Tick();
            if (Time.frameCount == openFrame) return;                                                       // the click or the key that opened it
            var kb = Keyboard.current;
            if (kb != null && !OzUeHooks.Typing && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) { Enter(); AudioDirector.UI("ui_click"); return; }
            var m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame)
            {
                var plate = mode == Mode.Leave ? leave.Root : reset.Root;
                if (!OpenZHud.Contains(plate, m.position.ReadValue())) { if (mode == Mode.Leave) Stay(); else Cancel(); }
            }
        }

        internal void Dispose() { OzUeHooks.SetModal(owner, false); }
    }
}
