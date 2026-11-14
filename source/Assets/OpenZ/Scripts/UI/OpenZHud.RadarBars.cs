using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.4 radar_enemy_bars, DV172 (View/OzRadarBars.cs): the unit panel's side of "no enemy health bars without a radar". A guard: in a match the
    /// panel only ever shows the player's own units (the selection keeps own units only, View/PlayerInput.cs; both teams only in the Unit editor,
    /// where the rule is off). Should it show an enemy while the player owns no standing radar: the health bar (track and fill) and the health
    /// number fade out with the overlay's enemy bars (the same shared factor, the same frame) and are switched off at 0, and the small label over
    /// the bar reads "HEALTH · NEEDS RADAR" in its own style (as UI/OpenZHud.HeroRegen.cs shows SELF-REPAIR; "NEEDS RADAR" where the long one
    /// would reach the health value, measured once like the hero line). The label goes with the bar, in the same frame: NEEDS RADAR once the bar
    /// has faded out and is off, HEALTH again on the first frame the bar fades back in (round 2: it no longer switches at the radar change while
    /// the bar still fades). Nothing moves: every part keeps its place (UI/OzLayout.cs). With a radar, and for every own or neutral unit: exactly
    /// as before (the bar's CanvasGroup is only added the first time a factor under 1 is needed, and then stays at 1).
    /// </summary>
    public partial class OpenZHud
    {
        CanvasGroup radarHpCg;
        bool radarLabelOn;
        string radarLabelText;

        /// <summary>AnimateUnit (every frame a unit is shown, right after the health bar's value): the bar and the number with the radar rule's
        /// factor for the unit <paramref name="hu"/> whose health the panel shows, and the label in the frame the bar goes off or comes back.</summary>
        void RadarHpFade(ZObject hu)
        {
            OzRadarBars.Tick(game);                                                                       // the HUD runs before the overlay: this frame's state for both
            if (uHealth == null || !uHealth.Root) return;
            float k = OzRadarBars.BarAlpha(hu);
            bool on = k > OzRadarBars.Hidden;
            var root = uHealth.Root.gameObject;
            if (root.activeSelf != on) root.SetActive(on);
            if (k < 1f && !radarHpCg)
            {
                radarHpCg = root.GetComponent<CanvasGroup>();
                if (!radarHpCg) { radarHpCg = root.AddComponent<CanvasGroup>(); radarHpCg.blocksRaycasts = false; radarHpCg.interactable = false; }
            }
            if (radarHpCg && Mathf.Abs(radarHpCg.alpha - k) > 0.001f) radarHpCg.alpha = k;
            if (uHp != null) { uHp.SetActive(on); if (on) uHp.Alpha = k; }                                // the 10 Hz refresh sets the number's colour (alpha 1) earlier in the frame
            if (on == radarLabelOn) HeroRegenLine(hu);                                                    // the label does not match the bar: switch it now (not at the next 10 Hz refresh)
        }

        /// <summary>HeroRegenLine's first step (the panel's 10 Hz unit refresh, and RadarHpFade in the frame the bar goes off or comes back): true =
        /// the label reads NEEDS RADAR (the hero line waits); false = the label is HeroRegenLine's again ("HEALTH", or a hero's self-repair, which it
        /// sets in the same call). The test is the bar's own (OzRadarBars.BarOff: factor at or under Hidden), this frame's state.</summary>
        bool RadarHpLabel(ZObject hu)
        {
            OzRadarBars.Tick(game);                                                                       // the refresh runs before AnimateUnit: this frame's state, not the last one's
            if (!OzRadarBars.BarOff(hu))
            {
                if (radarLabelOn) { radarLabelOn = false; uHpLabel.Set(OzRadarBarsRule.LabelHealth); uHpLabel.color = OzUi.Muted; heroRegenShown = null; }
                return false;
            }
            if (radarLabelText == null)                                                                   // measured once: the long label, or the short one where it would reach the health value
            {
                uHpLabel.Set(OzRadarBarsRule.LabelNeedsRadar);
                radarLabelText = uHpLabel.preferredWidth > HeroRegenLabelMaxW ? OzRadarBarsRule.LabelNeedsRadarShort : OzRadarBarsRule.LabelNeedsRadar;
            }
            radarLabelOn = true;
            uHpLabel.Set(radarLabelText);
            uHpLabel.color = OzUi.Muted;
            return true;
        }
    }
}
