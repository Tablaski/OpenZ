using System;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    internal sealed class OzAiGenerationToggle
    {
        internal readonly RectTransform Root;
        internal readonly Image Classic, Modern;
        readonly OzText classicText, modernText;
        readonly Color modernAccent;

        internal OzAiGenerationToggle(Transform parent, OzRect rect, float fontSize, Color modernAccent)
        {
            this.modernAccent = modernAccent;
            var border = Hud.Img(parent, "AI Generation", OzUi.Muted);
            border.raycastTarget = false;
            Root = border.rectTransform.Place(rect);
            float cellWidth = (rect.w - 2) * .5f;
            Classic = Segment("1996", 1);
            Modern = Segment("2026", 1 + cellWidth);
            classicText = Hud.Label(Classic.transform, "Year", "1996", fontSize, OzUi.Muted, 0, OzStyle.Plain).FillParent(OzH.C);
            modernText = Hud.Label(Modern.transform, "Year", "2026", fontSize, OzUi.Muted, 0, OzStyle.Plain).FillParent(OzH.C);
            classicText.graphic.raycastTarget = modernText.graphic.raycastTarget = false;

            Image Segment(string year, float x)
            {
                var segment = Hud.Img(Root, year, OzUi.GM950);
                segment.rectTransform.TL(x, 1, cellWidth, rect.h - 2);
                return segment;
            }
        }

        internal void Refresh(bool modern)
        {
            Classic.color = modern ? OzUi.GM950 : OzUi.Amber;
            Modern.color = modern ? modernAccent : (Color)OzUi.GM950;
            classicText.color = modern ? OzUi.Muted : OzUi.GM950;
            modernText.color = modern ? OzUi.GM950 : OzUi.Muted;
        }
    }

    public partial class OpenZMainMenu
    {
        static readonly string[] ModernCpuHints = {
            "SLOW OPENINGS", "QUICK CAPTURES", "COORDINATED ATTACKS", "MULTIPLE FRONTS"
        };
        static readonly float[] ModernCpuWidths = { .22f, .30f, .30f, .18f };
        readonly HudButton[] cpuModern = new HudButton[ZAiDifficulty.Modern.Length];
        readonly HudButton[] optCpuModern = new HudButton[ZAiDifficulty.Modern.Length];
        OzAiGenerationToggle cpuGeneration, optCpuGeneration;

        OzAiGenerationToggle BuildSoloAiGeneration(RectTransform parent, OzRect rect, View v, Side side, float fontSize)
        {
            var toggle = new OzAiGenerationToggle(parent, rect, fontSize, OzForkOptions.Accent);
            AddHot(toggle.Classic.rectTransform, v, () => OzAiSoloChoice.SelectGeneration(false), null, null, "ui_click", side);
            AddHot(toggle.Modern.rectTransform, v, () => OzAiSoloChoice.SelectGeneration(true), null, null, "ui_click", side);
            optTips[toggle.Modern.rectTransform] = "OpenZ-VK AI generation. Campaign rules use your 1996 difficulty setting.";
            return toggle;
        }

        void BuildModernCpuRow(RectTransform parent, OzRect row, View v, Side side, HudButton[] buttons, bool compact)
        {
            float gap = compact ? OzDeployBar.CpuGap : 4;
            float room = row.w - gap * (buttons.Length - 1);
            float x = row.x;
            for (int i = 0; i < buttons.Length; i++)
            {
                var level = ZAiDifficulty.Modern[i];
                float width = compact ? room * ModernCpuWidths[i] : room / buttons.Length;
                var button = SmallButton(parent, (compact ? "Cpu" : "OptCpu") + level, level.ToString().ToUpperInvariant(),
                    new OzRect(x, row.y, width, row.h), v, side, () => OzAiSoloChoice.Level = level,
                    compact ? OzDeployBar.CpuText : OzLayout.OptRowText, compact ? OzDeployBar.CpuTrack : .14f);
                button.LabelColor = OzForkOptions.Accent;
                button.Refresh();
                buttons[i] = button;
                optTips[button.Rt] = ZAiDifficulty.Hint(level);
                if (compact) SlidePart(button.Rt);
                x += width + gap;
            }
        }

        static void RefreshAiDifficulty(OzAiGenerationToggle toggle, OzText hint, HudButton[] modernButtons,
            HudButton easy, HudButton medium, HudButton hard, HudButton mayhem, bool compact)
        {
            var level = OzAiSoloChoice.Level;
            var difficulty = OpenZDifficulty.Choice;
            int modernIndex = Array.IndexOf(ZAiDifficulty.Modern, level);
            bool modern = modernIndex >= 0;
            toggle.Refresh(modern);
            Refresh(easy, !modern, difficulty == GameDifficulty.Easy);
            Refresh(medium, !modern, difficulty == GameDifficulty.Medium);
            Refresh(hard, !modern, difficulty == GameDifficulty.Hard);
            Refresh(mayhem, !modern, difficulty == GameDifficulty.Mayhem);
            for (int i = 0; i < modernButtons.Length; i++)
                Refresh(modernButtons[i], modern, i == modernIndex);

            if (modern)
            {
                string rules = OpenZDifficulty.Label(difficulty);
                hint.Set(compact ? ModernCpuHints[modernIndex] + " · " + rules
                    : ZAiDifficulty.Hint(level).ToUpperInvariant() + "  ·  " + rules + " RULES");
            }
            else hint.Set(compact ? OzDeployBar.CpuHint(difficulty) : OpenZDifficulty.Hint(difficulty));

            static void Refresh(HudButton button, bool visible, bool selected)
            {
                if (button.Rt.gameObject.activeSelf != visible) button.Rt.gameObject.SetActive(visible);
                SetSelected(button, visible && selected);
            }
        }
    }
}
