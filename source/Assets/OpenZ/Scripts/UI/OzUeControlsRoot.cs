using System;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (View/OzUnitEditor.cs; ARCHITECTURE module P2; spec E010-E049, E213, E215, sections 8 and 9.6): the Unit editor's
    /// bottom controls, camera and floor. Added by OzUnitEditor.Awake (the camera and the floor start at once, before the first frame);
    /// Build makes the HUD parts once the HUD has its Root: the counts plate with the BACKGROUND gear and its popover (top left), the
    /// empty field's hint, the play bar, the zoom / compass control and the ONE-row dock (bottom). The layout follows the canvas size,
    /// the other panels' insets, H and the dock's state (UI/OzUeBottomLayout.cs); the dock reports the bottom inset of the free field.
    /// </summary>
    public sealed class OzUeControlsRoot : MonoBehaviour
    {
        public static OzUeControlsRoot I { get; private set; }
        OzUnitEditor ed;
        OzUeDock dock; OzUePlayBar play; OzUeZoomBar zoom; OzUeCounts counts; OzUeBackdropMenu menu; OzUeFieldHint hint;
        OzUeMeasure measure;
        bool built, dirty = true; Vector2 lastCanvas; float nextMute;
        OzUeBottomLayout.Out lay;
        /// <summary>The last layout (for the counted check).</summary>
        public OzUeBottomLayout.Out Laid => lay;

        void Awake()
        {
            I = this;
            ed = GetComponent<OzUnitEditor>(); if (!ed) ed = OzUnitEditor.I;
            OzUeControls.Begin();
            if (ed != null)
            {
                var c = gameObject.AddComponent<OzUeCamera>(); c.Init(ed);
                var f = gameObject.AddComponent<OzUeFloor>(); f.Init(ed);
            }
            OzUeControls.ApplySound();
        }

        public void Build(OzUnitEditor editor)
        {
            ed = editor;
            var panels = OzUnitEditor.Layer(UeLayer.Panels); var field = OzUnitEditor.Layer(UeLayer.Field); var pops = OzUnitEditor.Layer(UeLayer.Popovers);
            if (!panels || !field || !pops) throw new InvalidOperationException("the editor's UI layers are missing");
            measure = new OzUeMeasure(field);
            hint = new OzUeFieldHint(); hint.Build(editor, field);
            if (OzUeFloor.I) OzUeFloor.I.BuildLabels(field);
            counts = new OzUeCounts(); counts.Build(editor, panels);
            play = new OzUePlayBar(); play.Build(editor, panels);
            zoom = new OzUeZoomBar(); zoom.Build(editor, panels);
            dock = new OzUeDock(); dock.Build(editor, panels);
            menu = new OzUeBackdropMenu(); menu.Build(editor, pops, counts.Gear.Rt);
            counts.OnGear = menu.Toggle;
            OzUeInsets.Changed += OnDirty;
            OzUnitEditor.PanelsHiddenChanged += OnHidden;
            OzUeControls.Changed += OnDirty;
            OzUeControls.DockChanged += OnDirty;
            built = true;
            Relayout();
            Debug.Log($"[OpenZ UnitEditor] bottom controls built: dock {lay.dock.plate}, tiles {lay.dock.tile:0} u, play bar {lay.play.plate}, zoom {lay.zoom.plate} ({lay.zoom.Where}), bottom inset {lay.insetB:0} u");
        }

        void OnDirty() { dirty = true; }
        void OnHidden(bool h) { dirty = true; }

        float TopInset() { float t = OzUeInsets.Get(UeSide.Top); return t > 0.5f ? t : 40f; }

        void Relayout()
        {
            var cs = OzUnitEditor.CanvasSize; lastCanvas = cs;
            var i = new OzUeBottomLayout.In
            {
                cw = cs.x, ch = cs.y, insetL = OzUeInsets.Get(UeSide.Left), insetR = OzUeInsets.Get(UeSide.Right), insetT = TopInset(),
                dockOpen = OzUeControls.DockOpen, hidden = OzUnitEditor.PanelsHidden,
                teamWords = new[] { OzUeUi.TeamWord(Team.Red), OzUeUi.TeamWord(Team.Blue), "NEUTRAL" },
                tiles = OzUeDock.TileLabels(), flagLabel = OzUePlayground.ShortName(OzUePlayground.Flag), obstacleLabel = OzUePlayground.ShortName(OzUePlayground.Obstacle),
                measure = measure.Width,
            };
            lay = OzUeBottomLayout.Lay(i);
            OzUeInsets.Set(UeSide.Bottom, "P2 dock", lay.insetB);
            dock.Layout(lay.dock);
            play.Layout(lay.play);
            zoom.Layout(lay.zoom);
            counts.Layout(i.insetT);
            hint.Layout(OzUeInsets.FreeFieldCanvas, cs.x, cs.y, i.insetT);
            if (menu.IsOpen) menu.Layout(cs.x, cs.y);
        }

        void Update()
        {
            if (!built) return;
            var cs = OzUnitEditor.CanvasSize;
            if ((cs - lastCanvas).sqrMagnitude > 0.25f) dirty = true;
            if (dirty) { dirty = false; Relayout(); }
            bool hidden = OzUnitEditor.PanelsHidden;
            dock.Tick(hidden); play.Tick(hidden); zoom.Tick(hidden); counts.Tick(hidden); menu.Tick(hidden); hint.Tick();
            float now = Time.unscaledTime;
            if (now >= nextMute) { nextMute = now + 1f; OzUeControls.KeepMute(); }   // the audio track may load its volumes after the editor muted them
        }

        void OnDestroy()
        {
            OzUeInsets.Changed -= OnDirty;
            OzUnitEditor.PanelsHiddenChanged -= OnHidden;
            OzUeControls.Changed -= OnDirty;
            OzUeControls.DockChanged -= OnDirty;
            dock?.Destroy(); hint?.Destroy();
            OzUeControls.ResetEvents(); OzUeBackdrop.ResetEvents();
            if (I == this) I = null;
        }

        // ------------------------------------------------------------------------------------------------ counted checks
        /// <summary>The canvas sizes of spec 2.1 (the screens they come from).</summary>
        public static readonly (float cw, float ch, string screen)[] CheckSizes =
        {
            (1920f, 1080f, "1920x1080"), (1607f, 904f, "1366x768 with the 0.85 pixel floor"), (1807f, 859f, "1536x730 (a 1920 screen at 125 %)"),
            (1920f, 1080f, "1600x900"), (1920f, 1080f, "2560x1440"), (1728f, 1080f, OzProofWords.Screen1610),
        };

        /// <summary>P2's counted check (menu 2 Counted Check in edit mode, and in Play): the bottom band laid out at the six canvas sizes of
        /// spec 2.1 with the stats box open (one dock row, the same dock rect in every tab, the spec's numbers at 1920 x 1080 within 2 u, no
        /// overlap, nothing under the stats box), the shaders, the new icons, and in Play the live parts.</summary>
        public static int SelfCheck(StringBuilder log)
        {
            if (log == null) log = new StringBuilder();
            int f = 0;
            Func<StringBuilder, bool, string, int> line = (l, ok, what) => { l.AppendLine((ok ? "PASS " : "FAIL ") + "P2 " + what); return ok ? 0 : 1; };
            GameObject temp = null; OzUeMeasure m = I != null ? I.measure : null;
            try
            {
                if (m == null) { temp = new GameObject("UE_MeasureCheck", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave }; m = new OzUeMeasure(temp.transform); }
                var labels = OzUeDock.TileLabels();
                foreach (var s in CheckSizes)
                {
                    float box = Mathf.Clamp(0.22f * s.cw, 360f, 470f);
                    UeBox first = default; bool same = true;
                    OzUeBottomLayout.Out o = null;
                    for (int tab = 0; tab < 4; tab++)
                    {
                        var i = new OzUeBottomLayout.In { cw = s.cw, ch = s.ch, insetR = OzUeBottomLayout.Gap + box, insetT = 40f, tiles = labels, measure = m.Width,
                                                          flagLabel = OzUePlayground.ShortName(OzUePlayground.Flag), obstacleLabel = OzUePlayground.ShortName(OzUePlayground.Obstacle) };
                        o = OzUeBottomLayout.Lay(i);
                        if (tab == 0) first = o.dock.plate;
                        else same &= Mathf.Abs(o.dock.plate.x - first.x) < 0.01f && Mathf.Abs(o.dock.plate.w - first.w) < 0.01f && Mathf.Abs(o.dock.plate.h - first.h) < 0.01f;
                    }
                    var d = o.dock; string at = $"at {s.cw:0} x {s.ch:0} u ({s.screen})";
                    bool oneRow = d.plate.w <= s.cw - 2f * OzUeBottomLayout.Gap + 0.01f && (Mathf.Abs(d.plate.h - 72f) < 0.01f || Mathf.Abs(d.plate.h - 80f) < 0.01f) && d.tile >= OzUeBottomLayout.TileMin;
                    f += line(log, oneRow, $"dock one row {at}: {d.plate} (tiles {d.tile:0} u, tab padding {d.tabPad:0})");
                    f += line(log, same, $"dock rect the same in all four tabs {at}");
                    var p = o.play.plate; var z = o.zoom.plate;
                    f += line(log, p.Right <= s.cw - (OzUeBottomLayout.Gap + box) - OzUeBottomLayout.Gap + 0.01f && p.x >= OzUeBottomLayout.Gap - 0.01f, $"play bar inside the free field {at}: {p}");
                    f += line(log, !p.Overlaps(d.plate) && !p.Overlaps(z) && !z.Overlaps(d.plate), $"play bar, zoom and dock do not overlap {at} (zoom {z}, {o.zoom.Where})");
                    f += line(log, Mathf.Abs(p.Bottom + OzUeBottomLayout.PlayAbove - d.plate.y) < 0.01f, $"play bar 8 u above the dock {at}");
                    if (s.cw == 1920f && s.ch == 1080f && s.screen == "1920x1080")
                    {
                        float platCells = d.platMidTile.w + d.platHighTile.w + 2f * d.tileGap;                     // 1.3: the two ramp platforms' cells, as their labels measure
                        f += line(log, Near(d.plate, 350.5f - platCells * 0.5f, 996f, 1219f + platCells, 72f), $"E040 dock at 1920 x 1080: {d.plate} (spec 437,996 1046x72 + the medium tank's tile, DV145 + the GRENADES and NUKE extras' cells + the two ramp platforms' {platCells:0} u, the extras' labels inside their cells: {350.5f - platCells * 0.5f:0.#},996 {1219f + platCells:0}x72)");   // 1.3 item 2: + the NUKE cell (5 + 48 u)
                        f += line(log, Near(p, 424f, 950f, 638f, 38f), $"E021 play bar at 1920 x 1080: {p} (spec 424,950 638x38)");
                        f += line(log, Near(z, 12f, 1038f, 232f, 30f) && o.zoom.low, $"E030 zoom control at 1920 x 1080: {z} (spec 12,1038 232x30, beside the dock)");
                        f += line(log, Mathf.Abs(o.insetB - 84f) < 0.01f, $"bottom inset 12 + the dock's 72 = {o.insetB:0} u (spec 2.2)");
                    }
                }
                f += line(log, Mathf.Min(OzUeBottomLayout.TileLabelText, Mathf.Min(OzUeBottomLayout.ZoomText, OzUeBottomLayout.KeyText)) >= OzText.MinSize, $"no text under {OzText.MinSize:0} u (the smallest: 11 u tile labels, zoom read-out, key caps, captions)");
                f += line(log, Mathf.Abs(OzUeBackdropMenu.Height(2) - 828.9f) < 1f, $"E013 background popover 300 x {OzUeBackdropMenu.Height(2):0.#} u with 2 columns (spec 722 with 10 styles; 1.3 item 26: 12 styles = one more row of 98.8 + 8 u = 828.9)");
                f += OzUeFloor.SelfCheckAssets(log, line);
                foreach (var g in new[] { "gear", "compass", "plus", "minus", "fit", "cube" })
                    foreach (var tier in new[] { 48, 96 })
                    {
                        var sp = OzKit.GetSprite("Icons/ic_" + g + "_" + tier);
                        f += line(log, sp != null && Mathf.RoundToInt(sp.rect.width) == tier, $"icon Kit/Icons/ic_{g}_{tier}: {(sp == null ? "missing" : "a sprite " + sp.rect.width + " px")}");
                    }
                var fp = OzUeArt.FlagPicture(); var op = OzUeArt.ObstaclePicture();
                f += line(log, fp && op && fp.width == 128 && op.width == 128 && fp.mipmapCount > 1 && op.mipmapCount > 1,
                          $"E047 the FLAG and OBSTACLE tile pictures drawn in code: {(fp ? fp.width + " px, " + fp.mipmapCount + " mip levels" : "missing")} / {(op ? op.width + " px, " + op.mipmapCount + " mip levels" : "missing")}");
                if (I != null && I.built)
                {
                    var live = I.lay; var cs = OzUnitEditor.CanvasSize;
                    f += line(log, live != null && OzUeFloor.Made, $"Play: built on the canvas {cs.x:0} x {cs.y:0} u, the floor {(OzUeFloor.Made ? "drawn" : "MISSING")} ({OzUeFloor.ShaderState})");
                    if (live != null)
                    {
                        f += line(log, Mathf.Abs(OzUeInsets.Get(UeSide.Bottom) - (OzUnitEditor.PanelsHidden ? 0f : live.insetB)) < 0.01f, $"Play: the bottom inset reported ({live.insetB:0} u)");
                        f += line(log, Mathf.Abs(I.dock.PlateBox.w - live.dock.plate.w) < 0.01f, $"Play: the dock's rect {live.dock.plate}");
                    }
                    // D18: SOUND OFF mutes the world's buses (effects + ambience, robot voices; never the interface sounds) and never pauses
                    // the listener, so nothing held back bursts out on SOUND ON; the player's own state is put back after the check
                    bool was = OzUeControls.SoundOn;
                    OzUeControls.SetSound(false); bool offOk = OzUeControls.WorldMuted && !AudioListener.pause;
                    OzUeControls.SetSound(true); bool onOk = !OzUeControls.WorldMuted && !AudioListener.pause;
                    OzUeControls.SetSound(was);
                    f += line(log, offOk && onOk && OzUeControls.WorldMuted == !was, $"Play: SOUND OFF mutes the world without pausing the listener, SOUND ON brings it back (nothing bursts out); back to SOUND {(was ? "ON" : "OFF")}");
                }
            }
            catch (Exception e) { f += line(log, false, "the check threw " + e.GetType().Name + ": " + e.Message); }
            finally { if (temp) { if (Application.isPlaying) Destroy(temp); else DestroyImmediate(temp); } }
            return f;
        }

        static bool Near(UeBox b, float x, float y, float w, float h) => Mathf.Abs(b.x - x) <= 2f && Mathf.Abs(b.y - y) <= 2f && Mathf.Abs(b.w - w) <= 2f && Mathf.Abs(b.h - h) <= 2f;
    }

    /// <summary>Text widths in the game's font (TextMeshPro Inter Tight ExtraBold), canvas units, cached: one hidden probe label per size
    /// and tracking.</summary>
    public sealed class OzUeMeasure
    {
        readonly Transform host;
        readonly Dictionary<string, float> cache = new Dictionary<string, float>();
        readonly Dictionary<string, OzText> probes = new Dictionary<string, OzText>();
        public OzUeMeasure(Transform parent) { host = parent; }

        public float Width(string text, float size, float tracking)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            string key = text + "|" + size.ToString("0.###") + "|" + tracking.ToString("0.####");
            if (cache.TryGetValue(key, out var w)) return w;
            string pk = size.ToString("0.###") + "|" + tracking.ToString("0.####");
            if (!probes.TryGetValue(pk, out var p) || p == null)
            {
                p = Hud.Label(host, "UE_Probe_" + pk, "", size, Color.clear, tracking, OzStyle.Plain);
                probes[pk] = p;
            }
            w = p.WidthAt(text);
            cache[key] = w;
            return w;
        }
    }
}
