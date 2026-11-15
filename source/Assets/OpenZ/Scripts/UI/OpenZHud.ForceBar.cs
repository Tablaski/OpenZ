using System;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    public partial class OpenZHud
    {
        Image forceRedShare, forceBlueShare, forceCursor, forceRedGlow, forceBlueGlow;
        OzText forceRedBonus, forceBlueBonus;
        HudButton forceRedHot, forceBlueHot;
        double forceRed, forceBlue, forceRedBoost, forceBlueBoost;
        bool forceValueMode;
        float forceShare = .5f;

        void BuildForceComparison()
        {
            var rail = Hud.Node("ForceBalance", top); rail.TL(79, 57, 622, 4);
            Hud.Img(rail, "Track", OzUi.GM950).rectTransform.Fill();
            forceRedShare = Hud.Img(rail, "RedShare", OzUi.TeamPaint(Team.Red));
            forceBlueShare = Hud.Img(rail, "BlueShare", OzUi.TeamPaint(Team.Blue));
            forceCursor = Hud.Img(rail, "ForceBalanceMarker", OzUi.Ink);
            forceCursor.rectTransform.TL(310, -2, 2, 8);
            forceRedGlow = Hud.Img(top, "RedBonusGlow", Color.clear); forceRedGlow.rectTransform.Place(new OzRect(77, 40, 220, 14));
            forceBlueGlow = Hud.Img(top, "BlueBonusGlow", Color.clear); forceBlueGlow.rectTransform.Place(new OzRect(483, 40, 220, 14));
            forceRedBonus = Hud.Label(top, "RedFortBonus", "", 9, OzUi.Ink).At(187, 47, OzH.C, true, 216);
            forceBlueBonus = Hud.Label(top, "BlueFortBonus", "", 9, OzUi.Ink).At(593, 47, OzH.C, true, 216);
            forceRedBonus.rectTransform.gameObject.SetActive(false); forceBlueBonus.rectTransform.gameObject.SetActive(false);
        }

        void RefreshForceMetrics(int redUnits, int blueUnits)
        {
            if (OpenZ.Multiplayer.DuelRuntime.Active)
            {
                SetFortProductionBoost(Team.Red, W.DuelFortLoserReduction(Team.Red));
                SetFortProductionBoost(Team.Blue, W.DuelFortLoserReduction(Team.Blue));
            }
            bool valueMode = OzForceBarOption.MilitaryValue;
            if (forceValueMode != valueMode)
            {
                forceValueMode = valueMode; shownRed = shownBlue = int.MinValue;
                lastRedW = lastBlueW = -1;
            }
            forceRed = valueMode ? W.MilitaryValue(Team.Red) : redUnits;
            forceBlue = valueMode ? W.MilitaryValue(Team.Blue) : blueUnits;
            redTick.Set((int)Math.Round(forceRed)); blueTick.Set((int)Math.Round(forceBlue));
            forceShare = (float)ZForceComparison.Share(forceRed, forceBlue);
            redBar.Set(valueMode ? forceShare : redUnits / (float)ZWorld.MAX_UNITS_PER_TEAM);
            blueBar.Set(valueMode ? 1 - forceShare : blueUnits / (float)ZWorld.MAX_UNITS_PER_TEAM);
            redCount.rectTransform.sizeDelta = new Vector2(120, redCount.rectTransform.sizeDelta.y);
            blueCount.rectTransform.sizeDelta = new Vector2(120, blueCount.rectTransform.sizeDelta.y);
            string delta = ForcePercent(ZForceComparison.RelativePercent(forceRed, forceBlue));
            string otherDelta = ForcePercent(ZForceComparison.RelativePercent(forceBlue, forceRed));
            redOf.Set(valueMode ? delta : "/ 70 · " + delta);
            blueOf.Set(valueMode ? otherDelta : otherDelta + " · / 70");
            redOf.rectTransform.sizeDelta = new Vector2(120, redOf.rectTransform.sizeDelta.y);
            blueOf.rectTransform.sizeDelta = new Vector2(120, blueOf.rectTransform.sizeDelta.y);
            string description = valueMode ? "Normal construction time of offensive mobile units. Robot squads count once; cranes, unarmed units, cannons and buildings do not count." : "Units on the field; robots count individually. At 70 the factories pause.";
            string reference = "The signed gap is relative to the smaller army; the cursor marks each team's share of the combined total.";
            forceRedHot.Tooltip = TeamColors.Name(Team.Red) + " FORCE|" + description + "\n" + reference + "\nUnits: " + redUnits + "/70." + BoostTip(forceRedBoost) + "|";
            forceBlueHot.Tooltip = TeamColors.Name(Team.Blue) + " FORCE|" + description + "\n" + reference + "\nUnits: " + blueUnits + "/70." + BoostTip(forceBlueBoost) + "|";
        }
        static string BoostTip(double reduction) => reduction > 0 ? "\nLooser bonus applied: fort construction time −" + (reduction * 100).ToString("0.#") + "%." : "";

        static string ForcePercent(double percent)
        {
            if (double.IsPositiveInfinity(percent)) return "+∞";
            if (double.IsNegativeInfinity(percent)) return "−∞";
            if (Math.Abs(percent) < .05) return "0%";
            return (percent > 0 ? "+" : "−") + Math.Abs(percent).ToString("0.#") + "%";
        }

        void RefreshForceNumbers()
        {
            SetNum(redCount, ref shownRed, redTick.Value);
            SetNum(blueCount, ref shownBlue, blueTick.Value);
        }

        /// <summary>Display the actual fort-production reduction supplied by the match policy; never changes production.</summary>
        public void SetFortProductionBoost(Team team, double reduction)
        {
            if (double.IsNaN(reduction) || reduction < 0 || reduction > 1) throw new ArgumentOutOfRangeException(nameof(reduction));
            if (team == Team.Red) forceRedBoost = reduction;
            else if (team == Team.Blue) forceBlueBoost = reduction;
        }

        void RefreshForceAppearance()
        {
            float share = forceCursor ? Mathf.Lerp((forceCursor.rectTransform.anchoredPosition.x + 1) / 622f, forceShare, OzMotion.Reduced ? 1 : 1 - Mathf.Exp(-Time.unscaledDeltaTime * 12)) : forceShare;
            forceRedShare.rectTransform.TL(0, 0, 622 * share, 4);
            forceBlueShare.rectTransform.TL(622 * share, 0, 622 * (1 - share), 4);
            forceCursor.rectTransform.TL(622 * share - 1, -2, 2, 8);
            ShowFortBoost(forceRedBoost, forceRedGlow, forceRedBonus);
            ShowFortBoost(forceBlueBoost, forceBlueGlow, forceBlueBonus);
        }
        static void ShowFortBoost(double reduction, Image glow, OzText label)
        {
            bool active = reduction > 0;
            label.rectTransform.gameObject.SetActiveFast(active);
            if (!active) { glow.color = Color.clear; return; }
            float pulse = OzMotion.Reduced ? .25f : .2f + .08f * Mathf.Sin(Time.unscaledTime * 3);
            glow.color = OzUi.A(OzUi.Amber, pulse);
            label.Set("LOOSER BONUS −" + (reduction * 100).ToString("0.#") + "%");
        }
    }
}
