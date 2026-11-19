using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (UI/OzUeControlsRoot.cs; owner 6k "zoom scale fit take too much space could be smaller"; spec E030-E036): the small
    /// zoom control in the bottom-left corner (next to the dock when there is room, else at the play bar's height): a ruler of the TRUE
    /// length on the ground at the free field's bottom row (the largest of 1, 2, 5, 10, 25, 50, 100, 250 m that fits in 64 u), the zoom
    /// against the start, zoom out, fit (Home), zoom in, and the compass (its needle turns with the view; amber while turned or tilted;
    /// a click glides back to north). The ruler keeps a 64 u box, so the buttons never move while the zoom changes.
    /// in the bottom-left corner: the row at the right end of the BACKGROUND popover's VIEW header (UI/OzUeBackdropMenu.cs ViewHead; no plate
    /// of its own, the popover blocks the field's clicks); the same buttons, tooltips and keys.
    /// </summary>
    public sealed class OzUeZoomBar
    {
        public static readonly float[] RulerSteps = { 1f, 2f, 5f, 10f, 25f, 50f, 100f, 250f };
        /// <summary>Checks: clicks since the editor opened; the ruler's last length (m) and bar width (u).</summary>
        public static int OutClicks, FitClicks, InClicks, CompassClicks;
        public static float RulerMetres, RulerUnits;

        public RectTransform Plate;
        RectTransform rulerBox; Image barL, barR, barB; OzText rulerText, pct;
        OzUeIconButton zOut, zFit, zIn, compass;
        OzUeBottomLayout.Zoom lay;
        float nextInfo; string pctShown = "", rulerShown = "";

        public UeBox PlateBox => lay != null ? lay.plate : default;

        public void Build(OzUnitEditor ed, Transform panels)
        {
            OutClicks = FitClicks = InClicks = CompassClicks = 0;
            Plate = Hud.Node("UE_ZoomRow", panels);                                                       // 1.5 kit ue_bottom_left_r10: `panels` = the popover's VIEW header (no plate, no blocker of its own)

            var rulerHot = OzUeW.Bare(Plate, "Ruler", OzUeBottomLayout.RulerW, 17f);
            rulerHot.Tooltip = OzUeW.Tip("Scale on the ground");
            rulerHot.OnClick = c => { };                                                                    // a click answers with the HUD's UI click like every control (a read-out: no action)
            rulerBox = rulerHot.Rt;
            barL = Hud.Img(rulerBox, "L", OzUi.Ink2); barR = Hud.Img(rulerBox, "R", OzUi.Ink2); barB = Hud.Img(rulerBox, "B", OzUi.Ink2);
            rulerText = Hud.Label(rulerBox, "M", "5 M", OzUeBottomLayout.ZoomText, OzUi.Ink, OzUeBottomLayout.ZoomTrack, OzStyle.Plain);
            var pctHot = OzUeW.Bare(Plate, "Pct", 36f, 15f);
            pctHot.Tooltip = OzUeW.Tip("Zoom", null, "Mouse wheel zooms to the cursor");
            pctHot.OnClick = c => { };                                                                      // the same: the UI click, no action
            pct = OzUeW.Centred(pctHot.Rt, "Pct", "100 %", OzUeBottomLayout.ZoomText, OzUi.Muted, OzUeBottomLayout.ZoomTrack);
            pct.FillParent(OzH.R);

            zOut = new OzUeIconButton(Plate, "ZoomOut", "minus", OzUeBottomLayout.ZoomBtn, 14f);
            zOut.B.Tooltip = OzUeW.Tip("Zoom out (mouse wheel)");
            zOut.B.OnClick = c => { OutClicks++; OzUeControls.ZoomStep(-1); };
            zFit = new OzUeIconButton(Plate, "Fit", "fit", OzUeBottomLayout.ZoomBtn, 14f);
            zFit.B.Tooltip = OzUeW.Tip("Fit all units", "HOME");
            zFit.B.OnClick = c => { FitClicks++; OzUeControls.Fit(); };
            zIn = new OzUeIconButton(Plate, "ZoomIn", "plus", OzUeBottomLayout.ZoomBtn, 14f);
            zIn.B.Tooltip = OzUeW.Tip("Zoom in (mouse wheel)");
            zIn.B.OnClick = c => { InClicks++; OzUeControls.ZoomStep(1); };
            compass = new OzUeIconButton(Plate, "Compass", "compass", OzUeBottomLayout.ZoomBtn, 14f);
            compass.B.Tooltip = OzUeW.Tip("Camera turn", null, "Hold the middle mouse button and drag to turn or tilt the view", "Click: back to north");
            compass.B.OnClick = c => { CompassClicks++; OzUeControls.CompassHome(); };
            compass.Icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            compass.Icon.rectTransform.anchoredPosition += new Vector2(7f, -7f);                           // turn round its middle
        }

        public void Layout(OzUeBottomLayout.Zoom z)
        {
            lay = z;
            // 1.5 kit ue_bottom_left_r10 (A36): at the right end of its parent (the VIEW header), its middle on the header's middle
            Plate.anchorMin = Plate.anchorMax = new Vector2(1f, 0.5f); Plate.pivot = new Vector2(1f, 0.5f);
            Plate.sizeDelta = new Vector2(z.rowW, OzUeBottomLayout.ZoomH); Plate.anchoredPosition = new Vector2(-OzUeBackdropMenu.ViewRowRight, 0f);
            float rx = OzUeBottomLayout.Border + OzUeBottomLayout.ZoomPadL, ry = (OzUeBottomLayout.ZoomH - 17f) * 0.5f;
            rulerBox.TL(rx, ry, OzUeBottomLayout.RulerW, 17f);
            rulerText.At(0f, 6f, OzH.L, false, 0f);
            var pctRt = pct.rectTransform.parent as RectTransform;
            pctRt.TL(rx + OzUeBottomLayout.RulerW + OzUeBottomLayout.ZoomGapIn, (OzUeBottomLayout.ZoomH - 15f) * 0.5f, z.pctW, 15f);
            float by = (OzUeBottomLayout.ZoomH - OzUeBottomLayout.ZoomBtn) * 0.5f;
            zOut.B.Rt.TL(z.buttonX[0], by, OzUeBottomLayout.ZoomBtn, OzUeBottomLayout.ZoomBtn);
            zFit.B.Rt.TL(z.buttonX[1], by, OzUeBottomLayout.ZoomBtn, OzUeBottomLayout.ZoomBtn);
            zIn.B.Rt.TL(z.buttonX[2], by, OzUeBottomLayout.ZoomBtn, OzUeBottomLayout.ZoomBtn);
            compass.B.Rt.TL(z.buttonX[3], by, OzUeBottomLayout.ZoomBtn, OzUeBottomLayout.ZoomBtn);
            SetBar(RulerUnits > 0f ? RulerUnits : 64f);
        }

        void SetBar(float w)
        {
            w = Mathf.Clamp(w, 2f, OzUeBottomLayout.RulerW);
            barL.rectTransform.TL(0f, 0f, 1f, 5f);
            barR.rectTransform.TL(w - 1f, 0f, 1f, 5f);
            barB.rectTransform.TL(0f, 4f, w, 1f);
        }

        /// <summary>E031: the largest step that fits in 64 u for this many units per metre (1 m when even that is longer).</summary>
        public static float RulerFor(float unitsPerMetre, out float widthU)
        {
            float m = RulerSteps[0];
            foreach (var s in RulerSteps) if (s * unitsPerMetre <= OzUeBottomLayout.RulerW + 0.01f) m = s;
            widthU = Mathf.Min(OzUeBottomLayout.RulerW, m * unitsPerMetre);
            return m;
        }

        public void Tick(bool hidden)
        {
            Plate.gameObject.SetActiveFast(!hidden);
            if (hidden) return;
            zOut.Tick(); zFit.Tick(); zIn.Tick();
            var cam = OzUeCamera.I;
            if (cam)
            {
                compass.Lit = cam.Turned;
                compass.Icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, cam.Yaw);              // the needle shows where north is on screen
            }
            compass.Tick();
            float now = Time.unscaledTime;
            if (now < nextInfo) return;
            nextInfo = now + 0.25f;
            if (!cam) return;
            float upm = cam.UnitsPerMetreAtBottom();
            if (upm > 0f)
            {
                float m = RulerFor(upm, out float w);
                RulerMetres = m; RulerUnits = w;
                SetBar(w);
                string rs = OzText.Tab(m.ToString("0") + " M");
                if (rs != rulerShown) { rulerShown = rs; rulerText.Set(rs); }
            }
            string ps = OzText.Tab(Mathf.RoundToInt(OzUeControls.ZoomPercent) + " %");
            if (ps != pctShown) { pctShown = ps; pct.Set(ps); }
        }
    }
}
