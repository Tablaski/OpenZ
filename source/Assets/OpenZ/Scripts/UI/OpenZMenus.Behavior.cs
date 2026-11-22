using System;
using UnityEngine;

namespace OpenZ
{
    public partial class OpenZMainMenu
    {
        void AddBehaviorOptions(RectTransform p)
        {
            AddCycle(p, toggles.Count, OzForkOptions.ChatLabel, () => OzForkOptions.ChatName, OzForkOptions.BindChat);
            AddMapReadabilityChoices(p, OzMapReadabilityOptions.FlagLabel, OzMapReadabilityOptions.FlagSizes, "x",
                () => OzMapReadabilityOptions.FlagSize, value => OzMapReadabilityOptions.FlagSize = value);
            AddCycle(p, toggles.Count, OzMapReadabilityOptions.BorderLabel, () => OzMapReadabilityOptions.BorderName, OzMapReadabilityOptions.StepBorder);
            AddToggle(p, toggles.Count, OzMapReadabilityOptions.RadarLabel, () => OzMapReadabilityOptions.Radars, value => OzMapReadabilityOptions.Radars = value);
            AddMapReadabilityChoices(p, OzMapReadabilityOptions.RadarSweepLabel, OzMapReadabilityOptions.RadarSweepIntervals, " s",
                () => OzMapReadabilityOptions.RadarSweepSeconds, value => OzMapReadabilityOptions.RadarSweepSeconds = value);
        }

        void AddMapReadabilityChoices(RectTransform panel, string label, float[] values, string suffix, Func<float> get, Action<float> set)
        {
            int rowIndex = hots.Count;
            var choices = new MapReadabilityChoices(panel, OptW - 2 * OzLayout.OptX, label, values, suffix, get, (button, value) =>
            {
                AddHot(button.Rt, View.Main, () => set(value), null, button, "ui_tab_switch", Side.Options);
                optTips[button.Rt] = button.Tooltip;
            });
            hots.Insert(rowIndex, new Hot { rt = choices.Row.Rt, view = View.Main, side = Side.Options, sfx = "" });
            optTips[choices.Row.Rt] = choices.Row.Tooltip;
            toggles.Add(new Toggle { label = label, row = choices.Row, tab = OzSettingTips.TabGame, choices = choices });
        }
    }
    public partial class OpenZPauseMenu
    {
        void AddBehaviorOptions(RectTransform p)
        {
            if (Multiplayer.DuelRuntime.Active)
            {
                AddToggle(p, toggles.Count, OzForkOptions.ChatLabel, null, null, true);
                var chatKey = toggles[toggles.Count - 1];
                chatKey.row.OnClick = c => OzForkOptions.BindChat();
                chatKey.row.OnRightClick = c => OzForkOptions.BindChat();
            }
            AddMapReadabilityChoices(p, OzMapReadabilityOptions.FlagLabel, OzMapReadabilityOptions.FlagSizes, "x",
                () => OzMapReadabilityOptions.FlagSize, value => OzMapReadabilityOptions.FlagSize = value);
            AddToggle(p, toggles.Count, OzMapReadabilityOptions.BorderLabel, null, null, true);
            var borderSize = toggles[toggles.Count - 1];
            borderSize.row.OnClick = c => OzMapReadabilityOptions.StepBorder(c.right || c.shift ? -1 : Arrow(borderSize));
            borderSize.row.OnRightClick = c => OzMapReadabilityOptions.StepBorder(-1);
            AddToggle(p, toggles.Count, OzMapReadabilityOptions.RadarLabel, () => OzMapReadabilityOptions.Radars, value => OzMapReadabilityOptions.Radars = value);
            AddMapReadabilityChoices(p, OzMapReadabilityOptions.RadarSweepLabel, OzMapReadabilityOptions.RadarSweepIntervals, " s",
                () => OzMapReadabilityOptions.RadarSweepSeconds, value => OzMapReadabilityOptions.RadarSweepSeconds = value);
        }

        void AddMapReadabilityChoices(RectTransform panel, string label, float[] values, string suffix, Func<float> get, Action<float> set)
        {
            var choices = new MapReadabilityChoices(panel, OzLayout.PauseW - 2 * OzLayout.OptX, label, values, suffix, get, (button, value) =>
            {
                button.OnClick = _ => { set(value); OzSfx.Ui("ui_tab_switch"); };
            });
            toggles.Add(new Toggle { label = label, row = choices.Row, tab = OzSettingTips.TabGame, choices = choices });
        }

        internal HudButton ProofMapReadabilityChoice(string label, float value) =>
            toggles.Find(toggle => toggle.label == label)?.choices?.ButtonFor(value);
    }

    sealed class MapReadabilityChoices
    {
        readonly HudButton[] buttons;
        readonly float[] values;
        readonly Func<float> get;
        public HudButton Row { get; }

        public MapReadabilityChoices(RectTransform panel, float width, string label, float[] values, string suffix,
            Func<float> get, Action<HudButton, float> bind)
        {
            this.values = values;
            this.get = get;
            buttons = new HudButton[values.Length];
            Row = HudButton.Make(panel, label, null, new Vector2(width, OzLayout.OptToggleH), HudButtonKind.Small);
            Row.Priority = 100;
            Row.Tooltip = OzSettingTips.For(label);
            const float gap = 4f, right = 12f, buttonWidth = 42f;
            float left = width - right - values.Length * buttonWidth - (values.Length - 1) * gap;
            Hud.Label(Row.Content, "Label", label, OzLayout.OptRowText, OzUi.Ink, 0.1f, OzStyle.Plain)
                .At(16f, OzLayout.OptToggleH * 0.5f, OzH.L, true, left - 24f);
            OzForkOptions.Highlight(Row, label);
            for (int i = 0; i < values.Length; i++)
            {
                string name = OzMapReadabilityOptions.FormatChoice(values[i]) + suffix;
                var button = HudButton.Make(Row.Rt, name, name, new Vector2(buttonWidth, 22f), HudButtonKind.Small, 11f, 0.04f);
                float x = left + i * (buttonWidth + gap);
                button.Rt.anchorMin = Vector2.zero;
                button.Rt.anchorMax = Vector2.up;
                button.Rt.offsetMin = new Vector2(x, 2f);
                button.Rt.offsetMax = new Vector2(x + buttonWidth, -2f);
                button.Priority = 101;
                button.Tooltip = Row.Tooltip;
                button.LabelColor = OzForkOptions.Accent;
                buttons[i] = button;
                bind(button, values[i]);
            }
            Refresh();
        }

        public void Refresh()
        {
            float selected = get();
            for (int i = 0; i < values.Length; i++)
            {
                bool active = values[i] == selected;
                if (buttons[i].Selected == active) continue;
                buttons[i].Selected = active;
                buttons[i].Refresh();
            }
        }

        public HudButton ButtonFor(float value)
        {
            int index = Array.IndexOf(values, value);
            return index >= 0 ? buttons[index] : null;
        }
    }
}
