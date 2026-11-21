using System;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>The direct graphics preset selector shared by the main and pause menus.</summary>
    sealed class OzGraphicsQualityButtons
    {
        static readonly int[] DisplayOrder = { 2, 1, 0, OzQuality.CustomSelection };
        readonly HudButton[] buttons = new HudButton[DisplayOrder.Length];
        readonly Image[] customOutline;
        public HudButton Row { get; }

        public OzGraphicsQualityButtons(RectTransform panel, float width, Action<HudButton, int> bind)
        {
            Row = HudButton.Make(panel, "GraphicsQuality", null, new Vector2(width, OzLayout.OptToggleH), HudButtonKind.Small);
            Row.Tooltip = OzQuality.Tip;
            Row.Priority = 100;
            Hud.Label(Row.Content, "Label", OzQuality.Label, OzLayout.OptRowText, OzUi.Ink, 0.1f, OzStyle.Plain)
                .At(16, OzLayout.OptToggleH * 0.5f, OzH.L, true, 170);

            const float gap = 4f, right = 12f;
            float buttonWidth = Mathf.Min(76f, (width - 164f - right - 3 * gap) / 4f);
            float left = width - right - 4 * buttonWidth - 3 * gap;
            for (int i = 0; i < DisplayOrder.Length; i++)
            {
                int selection = DisplayOrder[i];
                string label = OzQuality.SelectionName(selection);
                var button = HudButton.Make(Row.Rt, label, label, new Vector2(buttonWidth, 22f), HudButtonKind.Small, 11f, 0.04f);
                float x = left + i * (buttonWidth + gap);
                button.Rt.anchorMin = Vector2.zero;
                button.Rt.anchorMax = Vector2.up;
                button.Rt.offsetMin = new Vector2(x, 2f);
                button.Rt.offsetMax = new Vector2(x + buttonWidth, -2f);
                button.Priority = 101;
                button.Tooltip = OzQuality.Tip;
                if (selection == OzQuality.CustomSelection)
                {
                    button.LabelColor = OzForkOptions.Accent;
                    customOutline = Hud.Border(button.Rt, OzForkOptions.Accent);
                    button.Refresh();
                }
                buttons[i] = button;
                bind(button, selection);
            }
            Refresh();
        }

        public void Refresh()
        {
            int selection = OzQuality.Selection;
            for (int i = 0; i < DisplayOrder.Length - 1; i++)
            {
                bool selected = DisplayOrder[i] == selection;
                if (buttons[i].Selected == selected) continue;
                buttons[i].Selected = selected;
                buttons[i].Refresh();
            }
            foreach (var line in customOutline) line.enabled = selection == OzQuality.CustomSelection;
        }
    }
}
