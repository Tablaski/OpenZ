using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>General-purpose native laboratory fields, using the editor's HUD and keyboard ownership.</summary>
    internal sealed class OzUeLabInput
    {
        public readonly HudButton Button;
        readonly Func<string> read;
        readonly Action<string> write;
        readonly OzText text;
        static OzUeLabInput focused;
        readonly StringBuilder buffer = new StringBuilder();
        bool selected;
        public bool Focused => focused == this;
        public static bool AnyFocused => focused != null;
        public OzUeLabInput(Transform parent, string name, Rect r, Func<string> read, Action<string> write)
        {
            this.read = read; this.write = write;
            Button = OzUeGfx.Hot(parent, name, r.x, r.y, r.width, r.height, 42);
            OzUeGfx.Rect(Button.Rt, "Well", OzUi.GM950, 0, 0, r.width, r.height);
            OzUeGfx.Frame(Button.Rt, OzUi.GM400);
            text = OzUeGfx.Text(Button.Rt, "Value", read(), 13, OzUi.Ink, 0, 7, r.height / 2, OzH.L, r.width - 14);
            Button.OnClick = _ => Focus();
        }
        void Focus()
        {
            if (focused != null && focused != this) focused.End(true);
            if (focused == this) return;
            focused = this; buffer.Clear(); buffer.Append(read()); selected = true;
            if (Keyboard.current != null) Keyboard.current.onTextInput += Character;
            OzUeHooks.SetTyping(this, true);
        }
        void Character(char c)
        {
            if (char.IsControl(c) || buffer.Length >= 80) return;
            if (selected) { buffer.Clear(); selected = false; }
            buffer.Append(c);
        }
        void End(bool apply)
        {
            if (focused != this) return;
            if (Keyboard.current != null) Keyboard.current.onTextInput -= Character;
            focused = null; OzUeHooks.SetTyping(this, false);
            if (apply) write(buffer.ToString());
            text.Set(read());
        }
        public void Tick()
        {
            if (!Focused) { text.Set(read()); return; }
            var k = Keyboard.current; var m = Mouse.current;
            if (!Button.Alive) { End(true); return; }
            if (k != null && k.escapeKey.wasPressedThisFrame) { End(false); return; }
            if (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)) { End(true); return; }
            if (k != null && k.backspaceKey.wasPressedThisFrame)
            { if (selected) buffer.Clear(); else if (buffer.Length > 0) buffer.Length--; selected = false; }
            if (m != null && m.leftButton.wasPressedThisFrame && !OpenZHud.Contains(Button.Rt, m.position.ReadValue())) { End(true); return; }
            text.Set((selected ? "[" : "") + buffer + (selected ? "]" : " |"));
        }
        public void Dispose() { End(true); }
        public static double Number(string s)
        {
            if (!double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double n) || double.IsInfinity(n) || double.IsNaN(n))
                throw new ArgumentException("Enter a finite number. Decimal and scientific notation are accepted.");
            return n;
        }
    }
    internal sealed class OzUeLabSlider
    {
        public readonly HudButton Button;
        readonly Image fill, thumb;
        readonly Func<double> read;
        readonly Action<double> write;
        readonly double min, max;
        bool dragging;
        public OzUeLabSlider(Transform parent, string name, Rect r, double min, double max, Func<double> read, Action<double> write)
        {
            this.read = read; this.write = write; this.min = min; this.max = max;
            Button = OzUeGfx.Hot(parent, name, r.x, r.y, r.width, r.height, 42);
            OzUeGfx.Rect(Button.Rt, "Track", OzUi.GM950, 0, r.height / 2 - 3, r.width, 6);
            fill = OzUeGfx.Rect(Button.Rt, "Fill", OzUi.Amber, 0, r.height / 2 - 3, 1, 6);
            thumb = OzUeGfx.Rect(Button.Rt, "Thumb", OzUi.Ink, 0, 2, 5, r.height - 4);
        }
        public void Tick()
        {
            if (!Button.Alive) { dragging = false; return; }
            var m = Mouse.current;
            if (m != null)
            {
                if (m.leftButton.wasPressedThisFrame && OpenZHud.Contains(Button.Rt, m.position.ReadValue())) dragging = true;
                if (dragging && m.leftButton.isPressed)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(Button.Rt, m.position.ReadValue(), null, out var p);
                    double v = min + (max - min) * Mathf.InverseLerp(Button.Rt.rect.xMin, Button.Rt.rect.xMax, p.x);
                    if (Math.Abs(v - read()) > .0001) write(v);
                }
                if (!m.leftButton.isPressed) dragging = false;
            }
            float t = (float)Math.Max(0, Math.Min(1, (read() - min) / (max - min)));
            fill.rectTransform.sizeDelta = new Vector2(Button.Rt.rect.width * t, 6);
            thumb.rectTransform.anchoredPosition = new Vector2(Button.Rt.rect.width * t - 2, -2);
        }
    }
}
