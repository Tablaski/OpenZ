using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// Level 7: a small thermometer beside the health bar's place over each robot lava is heating, selected or not. It fills with the rules' own
    /// heat (ZObject.LavaHeat, the same that melts it), turns red as it fills and is gone once the robot has cooled. Drawn only; no lava level, or
    /// nobody in lava yet, costs nothing.
    /// </summary>
    public partial class OpenZUnitOverlay
    {
        sealed class Thermo { public RectTransform rt, fill; public Image fillImg, bulb; }
        readonly List<Thermo> thermos = new List<Thermo>();
        int thermoUsed;
        const float ThermoW = 6f, ThermoH = 16f, ThermoBulb = 8f, ThermoOff = 28f;
        static readonly Color ThermoCool = new Color(1f, 0.72f, 0.2f), ThermoHot = new Color(1f, 0.1f, 0.05f);
        /// <summary>Checks: thermometers drawn in the last frame, and the highest fill among them.</summary>
        public static int ThermoShown; public static float ThermoTop;

        Thermo NextThermo()
        {
            if (thermoUsed >= thermos.Count)
            {
                var t = new Thermo { rt = Hud.Node("Thermo", root) };
                t.rt.anchorMin = t.rt.anchorMax = Vector2.zero; t.rt.pivot = new Vector2(0.5f, 0f); t.rt.sizeDelta = new Vector2(ThermoBulb, ThermoH + ThermoBulb);
                var tube = Hud.Img(t.rt, "Tube", OzUi.A(Color.black, 0.8f)); tube.raycastTarget = false;
                tube.rectTransform.anchorMin = tube.rectTransform.anchorMax = new Vector2(0.5f, 0f); tube.rectTransform.pivot = new Vector2(0.5f, 0f);
                tube.rectTransform.anchoredPosition = new Vector2(0f, ThermoBulb - 2f); tube.rectTransform.sizeDelta = new Vector2(ThermoW, ThermoH);
                t.bulb = Hud.Img(t.rt, "Bulb", ThermoCool); t.bulb.raycastTarget = false;
                t.bulb.rectTransform.anchorMin = t.bulb.rectTransform.anchorMax = new Vector2(0.5f, 0f); t.bulb.rectTransform.pivot = new Vector2(0.5f, 0f);
                t.bulb.rectTransform.anchoredPosition = Vector2.zero; t.bulb.rectTransform.sizeDelta = new Vector2(ThermoBulb, ThermoBulb);
                t.fillImg = Hud.Img(t.rt, "Fill", ThermoCool); t.fillImg.raycastTarget = false; t.fill = t.fillImg.rectTransform;
                t.fill.anchorMin = t.fill.anchorMax = new Vector2(0.5f, 0f); t.fill.pivot = new Vector2(0.5f, 0f); t.fill.anchoredPosition = new Vector2(0f, ThermoBulb - 2f);
                thermos.Add(t);
            }
            var r = thermos[thermoUsed++]; r.rt.gameObject.SetActiveFast(true); return r;
        }

        void DrawLavaThermos(Camera cam, float sf, ZWorld W)
        {
            thermoUsed = 0; ThermoShown = 0; ThermoTop = 0f;
            if (W.Map.HasLava && W.LavaSteps > 0)
                foreach (var o in W.Objects)
                {
                    if (!o.IsRobot || o.Destroyed || o.KillAt >= 0 || o.LavaHeat <= 0 || !UnitRect(cam, o, out var r)) continue;
                    var t = NextThermo();
                    float h = Mathf.Clamp01((float)o.LavaHeat); var c = Color.Lerp(ThermoCool, ThermoHot, h);
                    t.rt.anchoredPosition = SnapU(new Vector2(r.center.x / sf + ThermoOff, r.yMax / sf + OzLayout.HpBarGap - 8f), sf);
                    t.fill.sizeDelta = new Vector2(ThermoW - 2f, Mathf.Max(1f, (ThermoH - 1f) * h));
                    t.fillImg.color = c; t.bulb.color = c;
                    ThermoShown++; if (h > ThermoTop) ThermoTop = h;
                }
            for (int i = thermoUsed; i < thermos.Count; i++) thermos[i].rt.gameObject.SetActiveFast(false);
        }
    }
}
