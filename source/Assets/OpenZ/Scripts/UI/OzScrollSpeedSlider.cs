using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.4 SCROLL SPEED row (UI/OzCamControls.cs): a slider inside an OPTIONS row of either menu, drawn like the volume sliders (the
    /// same track, amber fill and thumb), between the row's label and its value. The row is a value row of its menu (the value
    /// right-aligned with its two arrows, ValueW wide): a click on an arrow steps 10 %, as the other value rows step. A press on
    /// the bar takes the speed to that place and drags it (steps of 10 %, a tick sound at each step, live), saved when the button
    /// comes up. The menu's own click of that press is no step (TrackClick). Mouse only; the menus build it (OpenZMainMenu.CamControls.cs,
    /// OpenZPauseMenu.CamControls.cs).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OzScrollSpeedSlider : MonoBehaviour
    {
        /// <summary>Row units: where the bar starts (clear of the label), the value box's width, the gap before it, the bar's height.</summary>
        public const float TrackX = 140f, ValueW = 100f, TrackGap = 14f, TrackH = 8f;

        RectTransform track; Image fill, thumb; OzText value; float trackW; Action<string> sfx; int shown = -1;

        static readonly List<OzScrollSpeedSlider> live = new List<OzScrollSpeedSlider>();
        static OzScrollSpeedSlider owner; static int tickFrame = -1, releaseFrame = -10;
        /// <summary>Checks: drags on a bar since the start, the steps they made.</summary>
        public static int Drags, DragSteps;

        /// <summary>Build the slider in a menu's value row (row width rowW, its value text) and keep it showing the saved speed.</summary>
        public static OzScrollSpeedSlider Attach(HudButton row, OzText val, float rowW, Action<string> sound)
        {
            var s = row.Content.gameObject.AddComponent<OzScrollSpeedSlider>();
            s.value = val; s.sfx = sound;
            s.trackW = Mathf.Max(60f, rowW - 16f - ValueW - TrackGap - TrackX);
            s.track = Hud.Node("SpeedTrack", row.Content).Place(new OzRect(TrackX, OzLayout.OptToggleH * 0.5f - TrackH * 0.5f, s.trackW, TrackH));
            var bg = Hud.Kit(s.track, "Bg", "Bars/bar_track", new OzRect(0, 0, s.trackW, TrackH), null); bg.rectTransform.Fill();
            if (!OzKit.Available) bg.color = OzUi.GM950;
            s.fill = OzKit.Available ? OzKit.FillBar(s.track, "Fill", "amber") : Hud.Img(s.track, "Fill", OzUi.Amber);
            s.fill.rectTransform.Fill(1, 1, 1, 1); s.fill.type = Image.Type.Filled; s.fill.fillMethod = Image.FillMethod.Horizontal;
            s.thumb = Hud.Kit(s.track, "Thumb", "Buttons/slider_thumb", new OzRect(0, -6, 14, 20), OzKit.Available ? (Color?)null : OzUi.Ink, Image.Type.Simple);
            s.Show(true);
            return s;
        }

        /// <summary>The value text of the row, as the menus' value rows write it.</summary>
        public static string ValueText(int pct) => OzText.Tab("\u25C0  " + pct + " %  \u25B6");

        void OnEnable() { if (!live.Contains(this)) live.Add(this); shown = -1; Show(true); }
        void OnDisable() { live.Remove(this); if (owner == this) { owner = null; OzCamControls.SetSpeed(OzCamControls.ScrollPercent, true); } }
        void Update() { Tick(); Show(false); }

        void Show(bool force)
        {
            if (!track) return;
            int pct = OzCamControls.ScrollPercent;
            if (!force && pct == shown) return;
            shown = pct;
            float f = OzCamControls.FractionOf(pct);
            fill.fillAmount = f;
            thumb.rectTransform.anchoredPosition = new Vector2(trackW * f - 7f, 6f);
            if (value != null) value.Set(ValueText(pct));
        }

        bool OnTrack(Vector2 mp)
        {
            if (!track || !isActiveAndEnabled) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(track, mp, null, out var lp);
            var r = track.rect;
            return lp.x > r.xMin - 10f && lp.x < r.xMax + 10f && lp.y > r.yMin - 12f && lp.y < r.yMax + 12f;
        }

        void DragTo(Vector2 mp)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(track, mp, null, out var lp);
            var r = track.rect;
            int pct = OzCamControls.SpeedFromFraction((lp.x - r.xMin) / Mathf.Max(1f, r.width));
            if (pct == OzCamControls.ScrollPercent) return;
            OzCamControls.SetSpeed(pct, false); DragSteps++;
            sfx?.Invoke("ui_slider_tick");
            Show(false);
        }

        /// <summary>Once a frame, whichever asks first: a left press on a shown bar takes it; while held it drags; let go, it saves.</summary>
        static void Tick()
        {
            if (tickFrame == Time.frameCount) return;
            tickFrame = Time.frameCount;
            var m = Mouse.current; if (m == null) { owner = null; return; }
            Vector2 mp = m.position.ReadValue();
            if (m.leftButton.wasPressedThisFrame)
            {
                owner = null;
                if (!OzConfirm.Blocks) foreach (var s in live) if (s && s.OnTrack(mp)) { owner = s; Drags++; break; }
            }
            if (!owner) { owner = null; return; }
            owner.DragTo(mp);
            if (!m.leftButton.isPressed)
            {
                owner = null; releaseFrame = Time.frameCount;
                OzCamControls.SetSpeed(OzCamControls.ScrollPercent, true);                                    // saved once, on release
            }
        }

        /// <summary>The left press that is going on (or came up this frame) began on a bar: the menu's click of it is no step.</summary>
        public static bool TrackClick { get { Tick(); return owner != null || releaseFrame == Time.frameCount; } }

        // ---------------------------------------------------------------- checks
        /// <summary>A live slider (a menu's OPTIONS GAME tab is open), or null.</summary>
        public static OzScrollSpeedSlider Shown { get { foreach (var s in live) if (s && s.isActiveAndEnabled) return s; return null; } }
        /// <summary>The value text the row shows now.</summary>
        public string ProofValue => value != null ? value.text : "";
        /// <summary>The bar's fill now (0..1).</summary>
        public float ProofFill => fill ? fill.fillAmount : -1f;
        /// <summary>A drag to a place along the bar (0..1), as a pointer there would set it; saved as on release.</summary>
        public int ProofDragTo(float f)
        {
            if (!track) return -1;
            var r = track.rect;
            DragTo(RectTransformUtility.WorldToScreenPoint(null, track.TransformPoint(new Vector3(r.xMin + Mathf.Clamp01(f) * r.width, r.center.y, 0f))));
            OzCamControls.SetSpeed(OzCamControls.ScrollPercent, true);
            return OzCamControls.ScrollPercent;
        }
        /// <summary>The screen point of a place across the value box (0 = its left edge, 1 = its right edge; the arrows sit at the ends
        /// of the right-aligned text), at the row's middle height.</summary>
        public Vector2 ProofValuePoint(float f)
        {
            if (value == null || !value.rectTransform) return default;
            var r = value.rectTransform.rect;
            return RectTransformUtility.WorldToScreenPoint(null, value.rectTransform.TransformPoint(new Vector3(r.xMin + Mathf.Clamp01(f) * r.width, r.center.y, 0f)));
        }
        /// <summary>The screen point of a place along the bar (for a check that presses the mouse there).</summary>
        public Vector2 ProofPoint(float f)
        {
            if (!track) return default;
            var r = track.rect;
            return RectTransformUtility.WorldToScreenPoint(null, track.TransformPoint(new Vector3(r.xMin + Mathf.Clamp01(f) * r.width, r.center.y, 0f)));
        }
    }
}
