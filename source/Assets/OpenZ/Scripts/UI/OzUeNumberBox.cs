// 1.2 item 6, View/OzUnitEditor.cs: a Custom value input (DESIGN_SPEC E096 / E128 / E146, 6.6). The game has no text field: this is a
// polled entry. A click gives it the keyboard (everything typed is selected first, the first key replaces it); Enter applies and leaves,
// Esc goes back to the value it had, a click anywhere else applies; the mouse wheel over it or Up / Down step the value 1 % (Shift 10 %,
// at least one step) and apply at once; a right-click puts the base set's value back. While it has the keyboard every game and editor
// key is silenced (OzUeHooks.SetTyping). A non-number: toast TYPE A NUMBER and the old value comes back. Look (spec 3.2 CUSTOM_EDIT):
// fill SET_CUSTOM 6 % (16 % with the keyboard), edge rgba(150, 160, 150, .35) (hover SET_CUSTOM 55 %; changed or typing: SET_CUSTOM +
// a 1 u inner glow), text INK, changed #BFF2FB.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    internal sealed class OzUeNumberBox
    {
        public RectTransform Rt;
        public HudButton Hot;
        public string Id;
        public UeStat Stat;
        Image fill, glowT, glowB, glowL, glowR, caret, sel;
        Image[] edge;
        OzText label, plain;
        bool editable, changed;
        float w, h;

        static OzUeNumberBox focused;
        static bool selectAll;
        static readonly StringBuilder typed = new StringBuilder(16);
        static readonly List<char> pending = new List<char>(8);
        static string atFocus;
        static bool hooked;
        static readonly object TypingOwner = new object();
        /// <summary>Counts for the checks and the proof.</summary>
        public static int Applied, Rejected, Stepped, Resets;

        public static bool AnyFocused => focused != null;
        public static OzUeNumberBox Focused => focused;

        public static OzUeNumberBox Make(Transform parent, string name, float x, float y, float width, float height, int prio)
        {
            var b = new OzUeNumberBox { w = width, h = height };
            b.Hot = OzUeGfx.Hot(parent, name, x, y, width, height, prio);
            b.Rt = b.Hot.Rt;
            var cc = OzUeSets.Colour(UeSet.Custom);
            b.fill = OzUeGfx.Rect(b.Rt, "Fill", OzUi.A(cc, 0.06f), 0, 0, width, height);
            b.sel = OzUeGfx.Rect(b.Rt, "Sel", OzUi.A(cc, 0.30f), 3, 3, 10, height - 6); b.sel.enabled = false;
            b.edge = OzUeGfx.Frame(b.Rt, OzUeGfx.InputEdge);
            float hl = OzPx.Line(1f);
            b.glowT = OzUeGfx.Rect(b.Rt, "GlowT", OzUi.A(cc, 0.5f), hl, hl, width - 2 * hl, hl);
            b.glowB = OzUeGfx.Rect(b.Rt, "GlowB", OzUi.A(cc, 0.5f), hl, height - 2 * hl, width - 2 * hl, hl);
            b.glowL = OzUeGfx.Rect(b.Rt, "GlowL", OzUi.A(cc, 0.5f), hl, hl, hl, height - 2 * hl);
            b.glowR = OzUeGfx.Rect(b.Rt, "GlowR", OzUi.A(cc, 0.5f), width - 2 * hl, hl, hl, height - 2 * hl);
            b.label = OzUeGfx.Text(b.Rt, "Value", "", 13, OzUi.Ink, 0f, width - 5, height * 0.5f, OzH.R, width - 9);
            b.plain = OzUeGfx.Text(b.Rt, "Plain", "", 13, OzUi.Dim, 0f, width - 6, height * 0.5f, OzH.R, width - 8);
            b.caret = OzUeGfx.Rect(b.Rt, "Caret", OzUeGfx.CustomText, width - 5, 4, Mathf.Max(1f, hl), height - 8); b.caret.enabled = false;
            b.Hot.OnClick = c => b.OnClick();
            b.Hot.OnRightClick = c => b.OnRight();
            return b;
        }

        /// <summary>Shows this unit's and stat's Custom value (editable) or the plain value with its kind (n/a, fixed by a rule, DPS).</summary>
        public void Bind(string id, UeStat st)
        {
            if (focused == this && (id != Id || st != Stat)) Blur(true);
            Id = id; Stat = st;
            Refresh();
        }

        public void Refresh()
        {
            if (Id == null) return;
            editable = OzUeSets.Editable(Id, Stat);
            changed = editable && OzUeSets.IsChanged(Id, Stat);
            var cell = OzUeSets.Cell(Id, Stat, UeSet.Custom);
            bool box = editable;
            Show(fill, box); foreach (var e in edge) Show(e, box);
            label.SetActive(box); plain.SetActive(!box);
            if (Hot.Interactable != editable) { Hot.Interactable = editable; Hot.Refresh(); }   // a value nothing can edit gives the game's disabled click
            if (box)
            {
                if (focused != this) label.Set(OzText.Tab(OzUeSets.Text(Stat, cell)));
            }
            else
            {
                plain.Set(OzText.Tab(OzUeSets.Text(Stat, cell)));
                plain.color = cell.HasNumber ? (Color)OzUi.Ink2 : cell.kind == UeCellKind.NotListed || cell.kind == UeCellKind.NotInSet ? (Color)OzUi.Muted : (Color)OzUi.Dim;
            }
            Look();
        }

        static void Show(Image i, bool on) { if (i && i.enabled != on) i.enabled = on; }

        void Look()
        {
            if (!Rt) return;
            var cc = OzUeSets.Colour(UeSet.Custom);
            bool f = focused == this, glow = editable && (f || changed);
            if (editable)
            {
                fill.color = OzUi.A(cc, f ? 0.16f : 0.06f);
                var ec = f || changed ? cc : Hot.Hovered ? OzUi.A(cc, 0.55f) : OzUeGfx.InputEdge;
                foreach (var e in edge) if (e.color != ec) e.color = ec;
                label.color = changed && !f ? OzUeGfx.CustomText : f ? OzUeGfx.CustomText : (Color)OzUi.Ink;
            }
            Show(glowT, glow); Show(glowB, glow); Show(glowL, glow); Show(glowR, glow);
            Show(caret, f && !selectAll && OzMotion.Blink(0.5f));
            Show(sel, f && selectAll && typed.Length > 0);
            if (f && selectAll && typed.Length > 0)
            {
                float tw = Mathf.Min(w - 6f, label.WidthAt(typed.ToString()) + 4f);
                sel.rectTransform.TL(w - 5f - tw + 2f, 3f, tw, h - 6f);
            }
        }

        void OnClick()
        {
            if (!editable) return;
            if (focused == this) { selectAll = false; return; }
            Focus(this);
        }

        void OnRight()
        {
            if (!editable) return;
            if (focused == this) Blur(false);
            OzUeSets.ResetCustom(Id, Stat);
            Resets++;
            OzUeToast.Show(OzUeSets.EditToast(Id, Stat, true)); OzSfx.Ui("ui_click");                         // every unit of the type, both teams (View/OzUeSets.Reset.cs)
        }

        static void Focus(OzUeNumberBox b)
        {
            if (focused != null) Blur(true);
            focused = b;
            typed.Clear(); typed.Append(OzUeLiveStats.Format(b.Stat, OzUeSets.CustomValue(b.Id, b.Stat)));
            atFocus = typed.ToString();
            selectAll = true; pending.Clear();
            var kb = Keyboard.current;
            if (kb != null && !hooked) { kb.onTextInput += OnChar; hooked = true; }
            OzUeHooks.SetTyping(TypingOwner, true);
            b.label.Set(typed.ToString());
            b.Look();
        }

        static void OnChar(char c) { if (focused != null && c >= ' ' && c != (char)127) pending.Add(c); }

        /// <summary>The box gives the keyboard back; apply = take the typed text (else the value goes back to what it was).</summary>
        public static void Blur(bool apply)
        {
            var b = focused;
            if (b == null) return;
            focused = null;
            if (apply) b.Apply(typed.ToString(), true);
            typed.Clear(); pending.Clear();
            var kb = Keyboard.current;
            if (kb != null && hooked) kb.onTextInput -= OnChar;
            hooked = false;
            OzUeHooks.SetTyping(TypingOwner, false);
            if (b.Rt) b.Refresh();
        }

        void Apply(string text, bool leaving)
        {
            string t = (text ?? "").Trim().Replace(',', '.');
            if (t.Length == 0 || t == atFocus) return;
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || double.IsNaN(v) || double.IsInfinity(v))
            {
                Rejected++;
                OzUeToast.Show("TYPE A NUMBER", true);
                OzSfx.Ui("ui_error");
                return;
            }
            if (OzUeSets.SetCustom(Id, Stat, v)) { Applied++; OzSfx.Ui("ui_click"); OzUeToast.Show(OzUeSets.EditToast(Id, Stat, false)); }   // "EVERY JEEP, BOTH TEAMS: CUSTOM HEALTH 2000 HP"
        }

        void Step(int dir, bool big)
        {
            double v;
            string t = typed.ToString().Trim().Replace(',', '.');
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) v = OzUeSets.CustomValue(Id, Stat);
            v = OzUeLiveStats.Clamp(Id, Stat, v + dir * OzUeLiveStats.StepOf(Stat, v, big));
            if (OzUeSets.SetCustom(Id, Stat, v)) { Stepped++; OzSfx.Ui("ui_click"); OzUeToast.Show(OzUeSets.EditToast(Id, Stat, false)); }   // a wheel / arrow step: the same words, the newest value
            typed.Clear(); typed.Append(OzUeLiveStats.Format(Stat, OzUeSets.CustomValue(Id, Stat)));
            atFocus = typed.ToString();
            selectAll = false;
            label.Set(OzText.Tab(typed.ToString()));
        }

        /// <summary>Every frame (OzUeStatsRoot.Update, order 0: after the editor's keys, so the Enter / Esc that ends typing is never
        /// also an editor key).</summary>
        public static void Tick()
        {
            var b = focused;
            if (b == null) return;
            if (!b.Rt || !b.Rt.gameObject.activeInHierarchy) { Blur(true); return; }
            var kb = Keyboard.current; var m = Mouse.current;
            if (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame) && !OzUeGfx.Over(b.Rt)) { Blur(true); return; }
            if (kb != null)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { Blur(true); return; }
                if (kb.escapeKey.wasPressedThisFrame) { Blur(false); return; }
                bool shift = kb.shiftKey.isPressed;
                if (kb.upArrowKey.wasPressedThisFrame) b.Step(1, shift);
                else if (kb.downArrowKey.wasPressedThisFrame) b.Step(-1, shift);
                if (kb.backspaceKey.wasPressedThisFrame)
                {
                    if (selectAll) typed.Clear(); else if (typed.Length > 0) typed.Length--;
                    selectAll = false;
                }
                if (kb.deleteKey.wasPressedThisFrame && selectAll) { typed.Clear(); selectAll = false; }
            }
            if (pending.Count > 0)
            {
                if (selectAll) { typed.Clear(); selectAll = false; }
                foreach (var c in pending) if (typed.Length < 12) typed.Append(c);
                pending.Clear();
            }
            if (m != null && OzUeGfx.Over(b.Rt))
            {
                float wheel = m.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) b.Step(wheel > 0 ? 1 : -1, kb != null && kb.shiftKey.isPressed);
            }
            b.label.Set(typed.ToString());
            b.Look();
        }

        /// <summary>The editor closes: the keyboard goes back, nothing is kept.</summary>
        public static void Shutdown()
        {
            if (focused != null) Blur(true);
            var kb = Keyboard.current;
            if (kb != null && hooked) kb.onTextInput -= OnChar;
            hooked = false; focused = null; pending.Clear(); typed.Clear();
        }

        public void TickHover() { if (focused != this && editable) Look(); }
    }
}
