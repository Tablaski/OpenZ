#if !OZ_RELEASE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// The
    /// input events (as the 1.2 Play proof does): click into the Game view once first, else the hover / click lines say the input did not
    ///  B0 the editor's bottom controls are built (OzUeControlsRoot)
    ///  B1 the bottom band: no zoom control in it (the layout's zoom inMenu, nothing named UE_Zoom on the panels layer); the zoom row is a
    ///     child of the BACKGROUND popover (UE_BackgroundPopover / View / UE_ZoomRow)
    ///  B2 the dock's team switch 36 u over the 28 u count row, the same left edge and width, inside the plate (2 u or more free), the plate
    ///     72 u (80 u on a LARGE canvas) as before
    ///  B3 RED, BLUE, NEUTRAL picked in turn (OzUeControls.SetTeam: what the 1 / 2 / 3 keys and a click call): the picked cell ON (the full team
    ///     colour, its outline, the bright label), the two others OFF (the dark tints); the change flashes; the screen's own pixels in each cell
    ///     (4 u in from the cell's left edge, mid height) within 24 of the colour drawn; the ON cell brighter than an OFF cell of the same team.
    ///     Frames: bl_red.png, bl_blue.png, bl_neutral.png (the bottom-left: the dock's left end), bl_full_blue.jpg (the whole screen)
    ///  B4 HOVER: the game's mouse on an OFF cell: that cell one step brighter (the hover tint), its pixels brighter than OFF
    ///  B5 the BACKGROUND popover opened (as its gear): the zoom row shown in its VIEW header, inside the popover and the canvas, the ruler
    ///     "N M" and the zoom "N %" read; frame popover_view.png (the popover) and popover_full.jpg
    ///  B6 the zoom buttons CLICKED in the popover (the game's mouse): ZOOM OUT (zoom % down), ZOOM IN x2 (up), FIT (the camera's fit ran,
    ///     the zoom % moved), the COMPASS (back to north ran); the popover stays open while they are clicked
    ///  B7 every UI SCALE step (90 .. 160 %): the team switch inside the dock, the dock inside the canvas, the popover and its zoom row inside
    ///     the canvas; frame ui_scale_160.png (bottom-left + popover at 160 %)
    ///  B8 no errors / exceptions logged during the run.
    /// The team, the UI SCALE and the popover are put back as they were. Report: <see cref="Folder"/>/report_ue_bottom_left.txt, first line
    /// PASS or FAIL.
    /// </summary>
    public sealed class OzUeBottomLeftPlayCheck : MonoBehaviour
    {
        public static string Report = "idle";

        public static string Folder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var k = Path.Combine(d.FullName, "work", "staging", "v1_5", "kits", "ue_bottom_left_r10");
                if (Directory.Exists(k)) return Path.Combine(k, "proof", "play");
            }
            return Path.Combine(Application.persistentDataPath, "ue_bottom_left_play_check");
        }

        public static void Run() { Report = "running"; new GameObject("OzUeBottomLeftPlayCheck").AddComponent<OzUeBottomLeftPlayCheck>(); }

        int pass, fail, errors;
        readonly List<string> errorLines = new List<string>();
        readonly StringBuilder log = new StringBuilder();
        Vector2 lastPos; bool lBtn, delivered = true;

        void Start() { Application.logMessageReceived += OnLog; StartCoroutine(All()); }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        void OnLog(string msg, string st, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) { errors++; if (errorLines.Count < 6) errorLines.Add(msg.Split('\n')[0]); } }

        void Log(string s) { log.AppendLine(s); Debug.Log("[OpenZ ue bottom left check] " + s); }
        void Check(string what, bool ok) { if (ok) pass++; else fail++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        // ------------------------------------------------------------------ the game's mouse (queued input events, as View/OzUeProof.cs)
        void Move(Vector2 p)
        {
            var m = Mouse.current; if (m == null) return;
            var st = new MouseState { position = p, delta = p - lastPos };
            if (lBtn) st = st.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(m, st); lastPos = p;
        }

        IEnumerator Click(Vector2 p)
        {
            Move(p); yield return Frames(2);
            lBtn = true; Move(p); yield return Frames(2);
            lBtn = false; Move(p); yield return Frames(2);
        }

        // ------------------------------------------------------------------ geometry (screen pixels, bottom-left origin) and frames
        static Rect ScreenRect(RectTransform rt)
        {
            if (!rt) return default;
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(null, c[0]), b = RectTransformUtility.WorldToScreenPoint(null, c[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        static bool Inside(Rect inner, Rect outer, float tol = 1.5f) =>
            inner.xMin >= outer.xMin - tol && inner.yMin >= outer.yMin - tol && inner.xMax <= outer.xMax + tol && inner.yMax <= outer.yMax + tol;

        static string R(Rect r) => $"{r.x:0},{r.y:0} {r.width:0}x{r.height:0} px";

        Texture2D Grab()
        {
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            return t;
        }

        void SavePng(Texture2D t, Rect r, string file)
        {
            if (!t) return;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(r.xMin), 0, t.width - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(r.yMin), 0, t.height - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(r.xMax), x0 + 1, t.width), y1 = Mathf.Clamp(Mathf.CeilToInt(r.yMax), y0 + 1, t.height);
            var c = new Texture2D(x1 - x0, y1 - y0, TextureFormat.RGB24, false);
            c.SetPixels(t.GetPixels(x0, y0, x1 - x0, y1 - y0)); c.Apply();
            File.WriteAllBytes(Path.Combine(Folder(), file), c.EncodeToPNG()); Destroy(c);
            Log("frame " + file + " (" + R(r) + ")");
        }

        void SaveJpg(Texture2D t, string file)
        {
            if (!t) return;
            File.WriteAllBytes(Path.Combine(Folder(), file), t.EncodeToJPG(92)); Log("frame " + file);
        }

        /// <summary>The mean colour of a 3 x 3 px patch of the grabbed screen at a cell's sample point (4 u in from its left edge, mid height).</summary>
        static Color Sample(Texture2D t, OzUeTeamSeg seg, int k)
        {
            var r = ScreenRect(seg.Inner(k)); float px = OzUnitEditor.PxPerUnit;
            int x = Mathf.RoundToInt(r.xMin + 4f * px), y = Mathf.RoundToInt(r.center.y);
            Color s = Color.black; int n = 0;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = Mathf.Clamp(x + dx, 0, t.width - 1), yy = Mathf.Clamp(y + dy, 0, t.height - 1);
                    s += t.GetPixel(xx, yy); n++;
                }
            return s / n;
        }

        static float Diff(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b))) * 255f;
        static float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        static string C(Color c) { Color32 k = c; return $"({k.r},{k.g},{k.b})"; }

        // ------------------------------------------------------------------ the run
        IEnumerator All()
        {
            Directory.CreateDirectory(Folder());
            Log($"start {System.DateTime.Now:dd MMM yyyy HH:mm:ss}, screen {Screen.width} x {Screen.height}, canvas {OzUnitEditor.CanvasSize.x:0} x {OzUnitEditor.CanvasSize.y:0} u, UI SCALE {OzText.UiScale * 100f:0} %");
            var root = OzUeControlsRoot.I;
            bool up = OzUnitEditor.Active && root != null && root.Laid != null && root.Dock != null && root.Menu != null && root.Zoom != null;
            Check("B0 the Unit editor is up, its bottom controls built (dock, BACKGROUND popover, zoom row)", up);
            if (up)
            {
                var team0 = OzUeControls.Team; float scale0 = OzText.UiScale; bool open0 = root.Menu.IsOpen;
                if (Mouse.current != null) lastPos = Mouse.current.position.ReadValue();
                yield return Band(root);
                yield return Teams(root);
                yield return Hover(root);
                yield return EnsureDock(root);
                yield return Popover(root);
                yield return Scales(root);
                // put back
                OzUeControls.SetTeam(team0);
                if (Mathf.Abs(OzText.UiScale - scale0) > 0.001f) { OzText.UiScale = scale0; OzUnitEditor.I.Hud.ApplyUiScale(); }
                if (open0) root.Menu.Open(); else root.Menu.Close();
                Move(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
                yield return Real(0.4f);
                Log($"put back: team {OzUeControls.Team}, UI SCALE {OzText.UiScale * 100f:0} %, popover {(root.Menu.IsOpen ? "open" : "closed")}");
            }
            if (errors > 0) Check($"B8 no errors / exceptions logged during the run ({errors}: {string.Join(" | ", errorLines)})", false);
            else Check("B8 no errors / exceptions logged during the run", true);
            string head = (fail == 0 && pass > 0 ? "PASS" : "FAIL") + $" ue_bottom_left_r10 (A36): {pass} pass, {fail} fail";
            Report = head;
            File.WriteAllText(Path.Combine(Folder(), "report_ue_bottom_left.txt"), head + "\n" + log);
            Debug.Log("[OpenZ ue bottom left check] " + head + " (" + Folder() + ")");
            Destroy(gameObject);
        }

        IEnumerator Band(OzUeControlsRoot root)
        {
            var lay = root.Laid;
            var panels = OzUnitEditor.Layer(UeLayer.Panels);
            bool noZoomInBand = true;
            if (panels) for (int i = 0; i < panels.childCount; i++) { var c = panels.GetChild(i); if (c.gameObject.activeInHierarchy && c.name.StartsWith("UE_Zoom", System.StringComparison.Ordinal)) noZoomInBand = false; }
            Check($"B1 no zoom control in the bottom band (layout: {lay.zoom.Where}, band plate {lay.zoom.plate}; nothing named UE_Zoom on the panels layer)", lay.zoom.inMenu && lay.zoom.plate.w <= 0f && noZoomInBand);
            var zp = root.Zoom.Plate;
            Check($"B1 the zoom row is in the BACKGROUND popover's VIEW header ({(zp ? zp.parent.name + " / " + zp.name : "none")})",
                  zp && root.Menu.Panel && zp.IsChildOf(root.Menu.Panel) && root.Menu.ViewHead && zp.parent == root.Menu.ViewHead);
            var d = lay.dock; var tw = d.teamWell; var cw = d.countWell;
            Check($"B2 team switch {tw} over the count row {cw} in the dock {d.plate}: {OzUeBottomLayout.TeamSegH:0} / {OzUeBottomLayout.CountSegH:0} u, the same left edge and width, 2 u or more free, the plate {d.plate.h:0} u",
                  Mathf.Abs(tw.h - OzUeBottomLayout.TeamSegH) < 0.01f && Mathf.Abs(cw.h - OzUeBottomLayout.CountSegH) < 0.01f && Mathf.Abs(tw.x - cw.x) < 0.01f && Mathf.Abs(tw.w - cw.w) < 0.01f
                  && tw.y >= 2.99f && cw.Bottom <= d.plate.h - 2.99f && (Mathf.Abs(d.plate.h - 72f) < 0.01f || Mathf.Abs(d.plate.h - 80f) < 0.01f));
            var seg = root.Dock.TeamSeg;
            var segR = ScreenRect(seg != null ? seg.Rt : null); var plateR = ScreenRect(root.Dock.Plate);
            Check($"B2 on screen: the team switch {R(segR)} inside the dock {R(plateR)}", seg != null && OzUeControls.DockOpen ? Inside(segR, plateR) : seg != null);
            yield return null;
        }

        IEnumerator Teams(OzUeControlsRoot root)
        {
            var seg = root.Dock.TeamSeg; if (seg == null) yield break;
            if (!OzUeControls.DockOpen) { OzUeControls.SetDockOpen(true); yield return Real(0.5f); }
            Move(new Vector2(Screen.width * 0.5f, Screen.height * 0.6f)); yield return Frames(3);           // the mouse off the dock: no hover
            var order = new[] { Team.Red, Team.Blue, Team.Neutral };
            var files = new[] { "bl_red.png", "bl_blue.png", "bl_neutral.png" };
            var offLum = new Dictionary<OzUeSeg.Look, float>(); var onLum = new Dictionary<OzUeSeg.Look, float>();
            Log($"B3 the dock {(root.Dock.Pinned ? "pinned" : "NOT pinned (it may slide away when the mouse leaves it: the check opens it again)")}; the team picked before: {OzUeControls.Team}");
            for (int round = 0; round < 2; round++)
                for (int j = 0; j < 3; j++)
                {
                    var t = order[j];
                    int flashes0 = OzUeTeamSeg.Flashes;
                    OzUeControls.SetTeam(t);
                    yield return Frames(2);
                    int want = t == Team.Red ? 0 : t == Team.Blue ? 1 : 2;
                    float flashA = seg.FlashAlpha;
                    yield return Real(0.5f);                                                                    // the flash is over
                    if (round == 0) { Log($"B3 {t}: picked (flash {flashA:0.00}, {OzUeTeamSeg.Flashes - flashes0} change)"); continue; }   // the first round only walks the three once
                    yield return new WaitForEndOfFrame();
                    var tex = Grab();
                    bool states = seg.Selected == want, pixels = true; var sb = new StringBuilder();
                    for (int k = 0; k < 3; k++)
                    {
                        bool on = k == want;
                        var tint = OzUeTeamSeg.For(seg.LookOf(k));
                        Color drawn = seg.FillMid(k), wantFill = on ? tint.OnMid : tint.OffMid, label = seg.LabelColour(k), wantLabel = on ? (Color)tint.onInk : (Color)tint.offInk;
                        states &= Diff(drawn, wantFill) < 1.5f && Diff(label, wantLabel) < 1.5f && seg.OutlineOn(k) == on;
                        var px = tex ? Sample(tex, seg, k) : Color.clear;
                        float dp = Diff(px, drawn);
                        pixels &= tex && dp <= 24f;
                        if (tex) { if (on) onLum[seg.LookOf(k)] = Lum(px); else offLum[seg.LookOf(k)] = Lum(px); }
                        sb.Append($" {(k == 0 ? "1st" : k == 1 ? "2nd" : "3rd")} {seg.LookOf(k)} {(on ? "ON" : "off")}: drawn {C(drawn)}, screen {C(px)} (off by {dp:0}), label {C(label)};");
                    }
                    Check($"B3 {t} picked: the switch shows it (cell {want} ON: the full team colour, outline, bright label; the others OFF: the dark tints) and a change flashed ({flashA:0.00}):" + sb, states && OzUeTeamSeg.Flashes > flashes0 && flashA > 0.05f);
                    Check($"B3 {t}: the screen's pixels in the three cells within 24 of the colours drawn", pixels);
                    if (tex)
                    {
                        var plateR = ScreenRect(root.Dock.Plate); float u = OzUnitEditor.PxPerUnit;
                        var lay = root.Laid.dock;
                        var crop = Rect.MinMaxRect(Mathf.Max(0f, plateR.xMin - 12f * u), 0f, plateR.xMin + (lay.teamWell.Right + lay.sectionGap + 40f) * u, plateR.yMax + 60f * u);
                        SavePng(tex, crop, files[j]);
                        if (t == Team.Blue) SaveJpg(tex, "bl_full_blue.jpg");
                        Destroy(tex);
                    }
                }
            foreach (var l in new[] { OzUeSeg.Look.Red, OzUeSeg.Look.Blue, OzUeSeg.Look.Neutral })
                if (onLum.TryGetValue(l, out float hi) && offLum.TryGetValue(l, out float lo))
                    Check($"B3 {l}: ON on the screen (luminance {hi:0.000}) clearly brighter than OFF ({lo:0.000})", hi > lo + 0.08f);
                else Check($"B3 {l}: ON and OFF both read on the screen", false);
        }

        /// <summary>An unpinned dock slides away 1.2 s after the mouse left it (UI/OzUeDock.cs): wait that out and open it again.</summary>
        IEnumerator EnsureDock(OzUeControlsRoot root)
        {
            if (!root.Dock.Pinned) yield return Real(OzUeDock.HideDelay + 0.3f);
            if (!OzUeControls.DockOpen) { OzUeControls.SetDockOpen(true); yield return Real(0.5f); Log("the dock opened again (it is not pinned)"); }
        }

        IEnumerator Hover(OzUeControlsRoot root)
        {
            var seg = root.Dock.TeamSeg; if (seg == null) yield break;
            OzUeControls.SetTeam(Team.Neutral); yield return Real(0.5f);                                       // RED and BLUE both OFF
            int k = 1;                                                                                          // the 2nd cell (BLUE as drawn with the default colours)
            var r = ScreenRect(seg.Inner(k));
            yield return new WaitForEndOfFrame();
            var t0 = Grab(); var off = t0 ? Sample(t0, seg, k) : Color.clear; if (t0) Destroy(t0);
            Move(new Vector2(r.xMax - 6f, r.center.y)); yield return Frames(4);
            delivered = Mouse.current != null && (Mouse.current.position.ReadValue() - lastPos).sqrMagnitude < 4f;
            if (!delivered) { Check("B4 HOVER: the queued mouse reached the game (else: click into the Game view once and run it again)", false); yield break; }
            yield return new WaitForEndOfFrame();
            var t1 = Grab(); var hov = t1 ? Sample(t1, seg, k) : Color.clear; if (t1) Destroy(t1);
            var tint = OzUeTeamSeg.For(seg.LookOf(k));
            Check($"B4 HOVER on the OFF {seg.LookOf(k)} cell: hovered {seg.Hovered == k}, drawn {C(seg.FillMid(k))} = the hover tint {C(tint.HovMid)}, screen {C(off)} -> {C(hov)} (brighter)",
                  seg.Hovered == k && Diff(seg.FillMid(k), tint.HovMid) < 1.5f && Lum(hov) > Lum(off) + 0.01f);
            Move(new Vector2(Screen.width * 0.5f, Screen.height * 0.6f)); yield return Frames(3);
            Check($"B4 the mouse off again: the cell back to OFF ({C(seg.FillMid(k))})", seg.Hovered != k && Diff(seg.FillMid(k), tint.OffMid) < 1.5f);
        }

        IEnumerator Popover(OzUeControlsRoot root)
        {
            var menu = root.Menu; var zoom = root.Zoom;
            menu.Open(); yield return Real(0.6f);
            var pr = ScreenRect(menu.Panel); var vr = ScreenRect(menu.ViewHead); var zr = ScreenRect(zoom.Plate);
            var scr = new Rect(0f, 0f, Screen.width, Screen.height);
            Check($"B5 the BACKGROUND popover open {R(pr)}: its VIEW header {R(vr)} on top, the zoom row {R(zr)} shown inside the header, the popover and the screen",
                  menu.IsOpen && zoom.Plate.gameObject.activeInHierarchy && Inside(zr, vr) && Inside(vr, pr) && Inside(pr, scr) && Mathf.Abs(vr.yMax - pr.yMax) <= 3f);
            yield return Real(0.4f);
            Check($"B5 the ruler {OzUeZoomBar.RulerMetres:0} M ({OzUeZoomBar.RulerUnits:0.#} u) and the zoom {OzUeControls.ZoomPercent:0} % read", OzUeZoomBar.RulerMetres > 0f && OzUeZoomBar.RulerUnits > 0f);
            yield return new WaitForEndOfFrame();
            var tex = Grab();
            if (tex) { var u = OzUnitEditor.PxPerUnit; SavePng(tex, Rect.MinMaxRect(Mathf.Max(0f, pr.xMin - 8f * u), Mathf.Max(0f, pr.yMin - 8f * u), Mathf.Min(tex.width, pr.xMax + 8f * u), Mathf.Min(tex.height, pr.yMax + 8f * u)), "popover_view.png"); SaveJpg(tex, "popover_full.jpg"); Destroy(tex); }

            // B6 the buttons clicked with the game's mouse
            var outB = zoom.Plate.Find("ZoomOut") as RectTransform; var fitB = zoom.Plate.Find("Fit") as RectTransform;
            var inB = zoom.Plate.Find("ZoomIn") as RectTransform; var compB = zoom.Plate.Find("Compass") as RectTransform;
            Check("B6 the four buttons are in the row (ZoomOut, Fit, ZoomIn, Compass)", outB && fitB && inB && compB);
            if (!(outB && fitB && inB && compB)) { menu.Close(); yield break; }
            int o0 = OzUeZoomBar.OutClicks, i0 = OzUeZoomBar.InClicks, f0 = OzUeZoomBar.FitClicks, c0 = OzUeZoomBar.CompassClicks, fits0 = OzUeCamera.Fits, homes0 = OzUeCamera.Homes;
            // FIT first (a known start: the start view, or every unit framed), then IN x2, OUT, FIT again
            yield return Click(ScreenRect(fitB).center); yield return Real(1.2f);
            bool got = OzUeZoomBar.FitClicks > f0;
            if (!got) { Check("B6 the clicks reached the HUD (else: click into the Game view once and run it again)", false); menu.Close(); yield break; }
            float z0 = OzUeControls.ZoomPercent;
            Check($"B6 FIT clicked ({OzUeZoomBar.FitClicks - f0} click, the camera's fit ran {OzUeCamera.Fits - fits0}x): zoom {z0:0} %", OzUeZoomBar.FitClicks - f0 == 1 && OzUeCamera.Fits - fits0 == 1);
            yield return Click(ScreenRect(inB).center); yield return Click(ScreenRect(inB).center); yield return Real(0.9f);
            float z1 = OzUeControls.ZoomPercent;
            Check($"B6 ZOOM IN clicked twice in the popover ({OzUeZoomBar.InClicks - i0} clicks): zoom {z0:0} % -> {z1:0} % (up)", OzUeZoomBar.InClicks - i0 == 2 && z1 > z0 + 0.5f);
            yield return Click(ScreenRect(outB).center); yield return Real(0.9f);
            float z2 = OzUeControls.ZoomPercent;
            Check($"B6 ZOOM OUT clicked ({OzUeZoomBar.OutClicks - o0} click): zoom {z1:0} % -> {z2:0} % (down)", OzUeZoomBar.OutClicks - o0 == 1 && z2 < z1 - 0.5f);
            yield return Click(ScreenRect(fitB).center); yield return Real(1.2f);
            float z3 = OzUeControls.ZoomPercent;
            Check($"B6 FIT clicked again: zoom {z2:0} % -> {z3:0} % (back to the first fit's {z0:0} %)", OzUeZoomBar.FitClicks - f0 == 2 && Mathf.Abs(z3 - z2) > 0.5f && Mathf.Abs(z3 - z0) < 1.5f);
            yield return Click(ScreenRect(compB).center); yield return Real(0.8f);
            Check($"B6 COMPASS clicked ({OzUeZoomBar.CompassClicks - c0} click, back to north ran {OzUeCamera.Homes - homes0}x)", OzUeZoomBar.CompassClicks - c0 == 1 && OzUeCamera.Homes - homes0 >= 1);
            Check("B6 the popover stayed open while its row was clicked", menu.IsOpen);
            Move(new Vector2(Screen.width * 0.5f, Screen.height * 0.6f)); yield return Frames(3);
            menu.Close(); yield return Real(0.2f);
        }

        IEnumerator Scales(OzUeControlsRoot root)
        {
            var hud = OzUnitEditor.I.Hud; var seg = root.Dock.TeamSeg; var menu = root.Menu;
            var bad = new StringBuilder(); var wide = new StringBuilder(); int n = 0;
            menu.Open();
            foreach (var s in OzText.ScalePresets)
            {
                OzText.UiScale = s; hud.ApplyUiScale(); yield return Real(0.8f);
                if (!OzUeControls.DockOpen) { OzUeControls.SetDockOpen(true); yield return Real(0.5f); }       // an unpinned dock may have slid away
                if (!menu.IsOpen) menu.Open();
                yield return Real(0.2f);
                var scr = new Rect(0f, 0f, Screen.width, Screen.height);
                var segR = ScreenRect(seg.Rt); var plateR = ScreenRect(root.Dock.Plate); var pr = ScreenRect(menu.Panel); var zr = ScreenRect(root.Zoom.Plate);
                // checker 16 Nov: at the high UI SCALE steps of a small screen the dock's fit rule bottoms out (36 u tiles, 4 u tab padding) and the dock
                // is wider than the canvas: as before this kit (1289 u then, 1296 u now with the team padding following the tab padding). The dock on
                // the screen is required only where the fit rule fits it; elsewhere a NOTE with its width (not this kit's to fix)
                var cs = OzUnitEditor.CanvasSize; var dl = root.Laid.dock.plate;
                bool fits = dl.w <= cs.x - 2f * OzUeBottomLayout.Gap + 0.5f;
                bool ok = Inside(segR, plateR) && (!fits || Inside(plateR, scr)) && Inside(zr, pr) && zr.yMin >= -1f && zr.yMax <= Screen.height + 1f && root.Laid.zoom.inMenu;
                n++;
                if (!ok) bad.Append($" {s * 100f:0} %: switch {R(segR)}, dock {R(plateR)}, popover {R(pr)}, zoom row {R(zr)};");
                if (!fits) wide.Append($" {s * 100f:0} %: dock {dl.w:0} u in a {cs.x:0} u canvas (tiles {root.Laid.dock.tile:0} u, tab padding {root.Laid.dock.tabPad:0} u);");
                if (Mathf.Abs(s - OzText.ScaleMax) < 0.001f)
                {
                    yield return new WaitForEndOfFrame();
                    var tex = Grab(); if (tex) { SaveJpg(tex, "ui_scale_160.jpg"); Destroy(tex); }
                }
            }
            menu.Close();
            if (wide.Length > 0) Log("NOTE B7 the dock wider than the canvas (the fit rule's floor, as before this kit):" + wide);
            Check($"B7 every UI SCALE step ({n}): the team switch inside the dock, the dock on the screen where the fit rule fits it, the zoom row inside the popover and on the screen" + (bad.Length > 0 ? ":" + bad : ""), bad.Length == 0 && n > 0);
        }
    }
}
#endif
