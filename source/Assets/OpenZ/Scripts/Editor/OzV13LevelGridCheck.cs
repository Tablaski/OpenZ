// OpenZ (GPL-3.0) - version 1.3, item 30: the counted check of the battlefield screen's level carousel (edit mode, no Play).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Menu OpenZ/Checks/V1.3/Level select carousel (no Play). Pure numbers, the menu's own functions (UI/OzLevelCarousel.cs):
    ///  G0 the order: the menu lists the levels by number, level 7 after level 2 (L02, L07, L16, L17); the carousel opens on
    ///     L02 L07 L16 and one "&gt;" shows L07 L16 L17;
    ///     below as <see cref="Old12"/>) on every canvas the game runs at; the carousel is off;
    ///  G2 four levels where all fit at today's size (32:9): one row of four at today's card size (the 1.2 row of four), no carousel;
    ///  G3 four levels elsewhere: the carousel; three in view at today's size and in today's three places at every scroll position;
    ///     the others (and their shadows) wholly outside the window; the window inside the screen;
    ///  G4 the arrows and the page bars: inside the screen, clear of the window (no card, no glow), of the commander's window, the
    ///     tooltips' line, the UNIT STATS selector, the deploy bar, BACK, HIGH SCORES and the header;
    ///  G5 the slide: SlideDur 0.25-0.35 s, eased (OutCubic), only forward, ends on the place, no 60 fps frame moves a quarter card;
    ///  G6 keys, arrows and clicks: from every card and row position LEFT / RIGHT reach all four with the selected card in view;
    ///     the arrows step the row and answer ui_disabled at an end; every card can be brought into view and takes the pointer;
    ///     clip come with the package), EvilZod's card line(s) (OzEvilCardLines with their clips; level 7: its voiced exchange);
    ///  G8 the hooks: BuildSelect places by OzLevelCarousel.Place and builds the carousel, FocusCard reveals, TickCards slides,
    ///     Poll's wheel steps; the card line's speaker follows the cue.
    /// </summary>
    public static class OzV13LevelGridCheck
    {
        const string T = "[OzV13LevelCarousel] ";
        /// <summary>Screens (px) of the resolution checks: every canvas width the front end meets (5:4 the narrowest, 32:9 the widest).</summary>
        static readonly (int w, int h)[] Screens =
        {
            (1280, 1024), (1024, 768), (1440, 1080), (1280, 720), (1366, 768), (1600, 900), (1920, 1080), (1920, 1200), (2560, 1440),
            (2560, 1600), (2880, 1800), (2560, 1080), (3440, 1440), (3840, 2160), (5120, 1440)
        };
        const float Glow = OzMenuLayout.CardGlow, Lift = OzMenuLayout.CardLift, HeaderBottom = OzMenuLayout.SelHintY + 12f, BarTop = OzLayout.RefH - OzMenuLayout.DeployBarH;
        static readonly string[] Names = { "L02", "L07", "L16", "L17" };

        [MenuItem("OpenZ/Checks/V1.3/Level select carousel (no Play)", false, 7630)]
        static void Menu() { Run(); }

        /// <summary></summary>
        static OzLevelCarousel.Slot[] Old12(int n, float CW)
        {
            var s = new OzLevelCarousel.Slot[n];
            bool row3 = n > 2;
            float gap = row3 ? OzMenuLayout.CardGapN : OzMenuLayout.CardGap, gutter = row3 ? OzMenuLayout.CardSideN : OzMenuLayout.CardSide;
            float aspect = row3 ? OzMenuLayout.CardAspectN : OzMenuLayout.CardImgH / OzMenuLayout.CardImgW;
            float maxImgH = row3 ? Mathf.Min(OzMenuLayout.CardMaxImgH, OzMenuLayout.CardRowBottomMax - OzMenuLayout.CardTop - OzMenuLayout.CardMeta) : OzMenuLayout.CardMaxImgH;
            float w = Mathf.Min((CW - 2 * gutter - gap * (n - 1)) / n, maxImgH / aspect);
            float ih = w * aspect, x0 = (CW - (n * w + (n - 1) * gap)) * 0.5f;
            float band = OzLayout.RefH - OzMenuLayout.DeployBarH - OzMenuLayout.CardBandPad - OzMenuLayout.CardBandTop;
            float top = row3 ? Mathf.Max(OzMenuLayout.CardTop, Mathf.Min(OzMenuLayout.CardBandTop + (band - (ih + OzMenuLayout.CardMeta)) * 0.5f, OzMenuLayout.CardRowBottomMax - (ih + OzMenuLayout.CardMeta))) : OzMenuLayout.CardTop;
            for (int i = 0; i < n; i++) s[i] = new OzLevelCarousel.Slot { x = x0 + i * (w + gap), y = top, w = w, ih = ih };
            return s;
        }

        static Rect R(float x, float y, float w, float h) => new Rect(x, y, w, h);
        static Rect R(OzRect r) => new Rect(r.x, r.y, r.w, r.h);
        static bool Meet(Rect a, Rect b) => a.xMin < b.xMax && b.xMin < a.xMax && a.yMin < b.yMax && b.yMin < a.yMax;
        static bool Inside(Rect a, Rect b) => a.xMin >= b.xMin - 1e-3f && a.yMin >= b.yMin - 1e-3f && a.xMax <= b.xMax + 1e-3f && a.yMax <= b.yMax + 1e-3f;
        static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        static string S(OzLevelCarousel.Slot s) => $"({F(s.x)}, {F(s.y)}, {F(s.w)} x {F(s.H)})";
        static bool Same(OzLevelCarousel.Slot a, OzLevelCarousel.Slot b) => a.x == b.x && a.y == b.y && a.w == b.w && a.ih == b.ih;

        /// <summary>What the arrows and the page bars must never meet on a canvas cw u wide (canvas units, top-left origin).</summary>
        static List<(string name, Rect r)> Parts(float cw)
        {
            var quip = ZodLayout.Quip(); var br = OzMenuLayout.Back; float bx = br.XOn(cw);
            var L = OzDeployBar.Lay(cw); float selTop = OzLayout.RefH - OzMenuLayout.DeployBarH - OpenZMainMenu.UsGap - OpenZMainMenu.UsH;
            return new List<(string, Rect)>
            {
                ("the header", R(0, 0, cw, HeaderBottom)), ("BACK", R(bx, br.y, br.w, br.h)),
                ("HIGH SCORES", R(bx - OpenZMainMenu.HsBtnGap - OpenZMainMenu.HsBtnW, br.y + (br.h - OpenZMainMenu.HsBtnH) * 0.5f, OpenZMainMenu.HsBtnW, OpenZMainMenu.HsBtnH)),
                ("the commander's window", R(quip.x, quip.y, quip.W * quip.scale, quip.H * quip.scale)),
                ("the tooltips' line", R(0, OzDeployBar.TipBottom - OzDeployBar.TipH, cw, OzDeployBar.TipH)),
                ("the UNIT STATS selector", R(L.deployX, selTop, L.deployW, OpenZMainMenu.UsH)), ("the deploy bar", R(0, BarTop, cw, OzMenuLayout.DeployBarH)),
            };
        }

        public static string Run()
        {
            var sb = new StringBuilder(); int pass = 0, fail = 0;
            void Check(string what, bool ok) { if (ok) pass++; else fail++; sb.AppendLine((ok ? "PASS " : "FAIL ") + what); }
            int carousels = 0, rows4 = 0;

            foreach (var (pw, ph) in Screens)
            {
                float cw = pw * OzLayout.RefH / ph; string at = $"{pw}x{ph} (canvas {F(cw)} u)";
                // G1 three levels = the 1.2 row
                var a = OzLevelCarousel.Place(3, cw); var b = Old12(3, cw); bool same = a.Length == 3 && !OzLevelCarousel.On(3, cw);
                for (int i = 0; i < 3 && same; i++) same = Same(a[i], b[i]);
                Check($"G1 {at}: three levels {string.Join(" ", a.Select(S))} = the 1.2 row {string.Join(" ", b.Select(S))}, no carousel", same);

                var g = OzLevelCarousel.Place(4, cw); int vis = OzLevelCarousel.Visible(4, cw); bool sizeOk = g.All(s => s.w == b[0].w && s.ih == b[0].ih && s.y == b[0].y);
                var screen = R(0, 0, cw, OzLayout.RefH);
                if (!OzLevelCarousel.On(4, cw))
                {
                    // G2 all four fit at today's size
                    rows4++; var bad = new StringBuilder(); var r4 = Old12(4, cw);
                    for (int i = 0; i < 4; i++)
                    {
                        if (!Same(g[i], r4[i])) bad.Append($" card {i} is not the 1.2 row of four");
                        if (g[i].x - Glow < 0f || g[i].x + g[i].w + Glow > cw) bad.Append($" card {i} leaves the screen");
                    }
                    Check($"G2 {at}: four levels fit at today's size: one row {string.Join(" ", g.Select(S))}, today's card ({sizeOk}), no arrows" + bad, sizeOk && bad.Length == 0 && vis == 4);
                    continue;
                }
                carousels++;
                // G3 the carousel: today's places at every scroll position, the others outside the window
                var win = R(OzLevelCarousel.Window(g, vis)); var places = new StringBuilder(); var badW = new StringBuilder();
                if (!Inside(win, screen)) badW.Append(" the window leaves the screen");
                for (int first = 0; first <= OzLevelCarousel.MaxFirst(4, vis); first++)
                {
                    float sc = OzLevelCarousel.ScrollAt(g, first); places.Append($" first {first} (scroll {F(sc)} u):");
                    for (int i = 0; i < 4; i++)
                    {
                        bool drawn = OzLevelCarousel.InWindow(g, vis, i, sc), want = i >= first && i < first + vis;
                        if (drawn != want) badW.Append($" card {i} {(drawn ? "drawn" : "hidden")} at first {first}");
                        if (want && Mathf.Abs(g[i].x - sc - b[i - first].x) > 0.01f) badW.Append($" card {i} not in today's place {i - first}");
                        var shadow = R(g[i].x - sc + OzMenuLayout.CardShadowDX, g[i].y + OzMenuLayout.CardShadowDY, g[i].w, g[i].H);
                        if (!want && (Meet(R(g[i].x - sc, g[i].y, g[i].w, g[i].H), win) || Meet(shadow, win))) badW.Append($" card {i} (or its shadow) inside the window at first {first}");
                        if (want) places.Append(' ').Append(Names[i]);
                    }
                }
                Check($"G3 {at}: four levels, carousel: {vis} in view at today's size ({sizeOk}), window ({F(win.x)}, {F(win.y)}, {F(win.width)} x {F(win.height)});{places}" + badW, sizeOk && vis == 3 && badW.Length == 0);

                // G4 the arrows and page bars
                var bad4 = new StringBuilder(); var parts = Parts(cw);
                foreach (int d in new[] { -1, 1 })
                {
                    var ar = R(OzLevelCarousel.Arrow(g, vis, d)); string nm = d < 0 ? "<" : ">";
                    if (!Inside(ar, screen)) bad4.Append($" {nm} leaves the screen");
                    if (Meet(ar, win)) bad4.Append($" {nm} meets the window (a card or its glow)");
                    foreach (var p in parts) if (Meet(ar, p.r)) bad4.Append($" {nm} meets {p.name}");
                }
                for (int i = 0; i < 4; i++)
                {
                    var pb = R(OzLevelCarousel.PageBar(g, vis, 4, i));
                    if (!Inside(pb, screen) || pb.yMin < g[0].y + g[0].H + OzMenuLayout.CardShadowDY) bad4.Append($" page bar {i} leaves the screen or meets a card's shadow");
                    foreach (var p in parts) if (Meet(pb, p.r)) bad4.Append($" page bar {i} meets {p.name}");
                }
                if (g[0].y - Lift - Glow < HeaderBottom) bad4.Append(" a lifted card's glow reaches the header");
                var l = R(OzLevelCarousel.Arrow(g, vis, -1)); var rr = R(OzLevelCarousel.Arrow(g, vis, 1));
                Check($"G4 {at}: < ({F(l.x)}, {F(l.y)}, {F(l.width)} x {F(l.height)}) and > ({F(rr.x)}, {F(rr.y)}) {F(b[0].x - l.xMax)} u off the cards, the page bars: inside, over no card, glow, comms, tooltip, UNIT STATS, deploy bar, BACK, HIGH SCORES, header" + bad4,
                      bad4.Length == 0 && b[0].x - l.xMax >= Glow + 2f && rr.xMin - (b[2].x + b[2].w) >= Glow + 2f);
            }
            Check($"G2/G3 four levels: the carousel on {carousels} screens, one row of four on {rows4} (32:9)", carousels == Screens.Length - 1 && rows4 == 1);

            // G5 the slide
            {
                var g = OzLevelCarousel.Place(4, 1920f); float pitch = OzLevelCarousel.Pitch(g), prev = 0f, maxStep = 0f; bool mono = true;
                int frames = Mathf.CeilToInt(OzLevelCarousel.SlideDur * 60f);
                for (int k = 1; k <= frames; k++) { float x = OzLevelCarousel.Ease(0f, pitch, k / 60f / OzLevelCarousel.SlideDur); mono &= x >= prev; maxStep = Mathf.Max(maxStep, x - prev); prev = x; }
                float mid = OzLevelCarousel.Ease(0f, pitch, 0.5f);
                Check($"G5 the slide: {F(OzLevelCarousel.SlideDur)} s, OutCubic, one card pitch ({F(pitch)} u at 1920 x 1080): half-way {F(mid)} u, only forward ({mono}), ends on the place ({prev == pitch}), the largest 60 fps frame {F(maxStep)} u; unscaled time (OzMotion)",
                      OzLevelCarousel.SlideDur >= 0.25f && OzLevelCarousel.SlideDur <= 0.35f && mono && prev == pitch && maxStep < pitch * 0.25f && OzLevelCarousel.Ease(0f, pitch, 0f) == 0f && mid > pitch * 0.5f);
            }

            // G6 keys, arrows and clicks (the menu's own rules: FocusCard clamps and reveals, CarouselStep moves the row and keeps the selection in view)
            {
                int n = 4, vis = OzLevelCarousel.Shown, max = OzLevelCarousel.MaxFirst(n, vis); bool all = true, inView = true;
                for (int f0 = 0; f0 < n; f0++)
                    for (int first0 = 0; first0 <= max; first0++)
                    {
                        int f = f0, first = OzLevelCarousel.FirstFor(first0, f0, vis, n); var seen = new HashSet<int> { f };
                        foreach (int d in new[] { 1, 1, 1, 1, -1, -1, -1, -1 })
                        {
                            f = Mathf.Clamp(f + d, 0, n - 1); first = OzLevelCarousel.FirstFor(first, f, vis, n); seen.Add(f);
                            inView &= f >= first && f < first + vis;
                        }
                        all &= seen.Count == n;
                    }
                Check($"G6 keys: from every card and row position LEFT / RIGHT reach all four ({all}), the selected card always in view ({inView})", all && inView);
                int fc = 0, fr = 0; var log = new StringBuilder();
                foreach (int d in new[] { 1, 1, -1, -1 })
                {
                    int to = Mathf.Clamp(fr + d, 0, max); bool moved = to != fr; fr = to; fc = Mathf.Clamp(fc, fr, fr + vis - 1);
                    log.Append($" {(d > 0 ? ">" : "<")}:{(moved ? "ui_click" : "ui_disabled")} first {fr} card {fc}");
                }
                Check($"G6 arrows from the start:{log}", log.ToString() == " >:ui_click first 1 card 1 >:ui_disabled first 1 card 1 <:ui_click first 0 card 1 <:ui_disabled first 0 card 1");
                var g4 = OzLevelCarousel.Place(n, 1920f); var reached = new HashSet<int>();
                for (int first = 0; first <= max; first++) for (int i = first; i < first + vis; i++) if (OzLevelCarousel.InWindow(g4, vis, i, OzLevelCarousel.ScrollAt(g4, first))) reached.Add(i);
                Check($"G6 clicks: with the arrows every card comes into view and takes the pointer ({string.Join(", ", reached.OrderBy(x => x))})", reached.Count == n);
            }

            // With it registered the carousel opens on L02 L07 L16 and one ">" shows L07 L16 L17
            {
                var all = OzLevelDef.All.Select(d => d.Id).ToArray(); var menuIds = OzLevelL07.MenuOrder();
                Check($"G0 the menu order {string.Join(" ", all)} (with level 7 in its place: {string.Join(" ", menuIds)}) rises by level number; the four cards {string.Join(" ", Names)}",
                      OzLevelL07.ByNumber(all) && OzLevelL07.ByNumber(menuIds) && menuIds.SequenceEqual(Names) && (!OzLevelL07.Registered || all.SequenceEqual(Names)));
                int v = OzLevelCarousel.Shown, mf = OzLevelCarousel.MaxFirst(Names.Length, v);
                string atStart = string.Join(" ", Names.Take(v)), atEnd = string.Join(" ", Names.Skip(mf).Take(v));
                Check($"G0 the carousel at the start shows {atStart} (> lit), after one > {atEnd} (< lit, > dimmed)", atStart == "L02 L07 L16" && atEnd == "L07 L16 L17" && mf == 1);
            }

            // G7 the lines of every card
            var levels = OzLevelL07.MenuOrder().ToList();
            var lib = ZodLibrary.Shared; string pkg = PackageLines();
            foreach (var id in levels)
            {
                string zid = "zod_select_" + id; var zl = lib.Line(zid); bool zClip = zl != null && lib.Clip(zid);
                bool zText = zl == null && id == OzLevelL07.Id && pkg.Contains("\"" + zid + "\"") && pkg.Contains("TEXT ONLY, waiting for the owner");
                var ev = OzEvilCardLines.ForLevel(id); bool evOk = ev.Count > 0 && ev.All(x => x.Pending ? id == OzLevelL07.Id : OzEvilCardLines.Clip(x));
                Check($"G7 {id}: Zod {zid} {(zClip ? "in zod_lines.json with its clip" : zText ? "TEXT ONLY in the item 30 package (waiting for the owner)" : "MISSING")}; EvilZod {string.Join(", ", ev.Select(x => x.Id + (x.Pending ? " TEXT ONLY" : "")))}",
                      (zClip || zText) && evOk);
            }

            // G8 the hooks
            string menu = Read("Assets/OpenZ/Scripts/UI/OpenZMainMenu.cs"), car = Read("Assets/OpenZ/Scripts/UI/OpenZMainMenu.Carousel.cs"), lines = Read("Assets/OpenZ/Scripts/View/Zod/ZodBriefing.EvilLines.cs");
            Check("G8 BuildSelect places the cards by OzLevelCarousel.Place into BuildCarousel's parent and builds the arrows; FocusCard reveals, TickCards slides, Poll's wheel steps",
                  menu.Contains("OzLevelCarousel.Place(n, CW); var cardParent = BuildCarousel(p, slots);") && menu.Contains("BuildCarouselControls(p);") && menu.Contains("CarouselReveal(i);")
                  && menu.Contains("TickCarousel(dt);") && menu.Contains("CarouselWheel(wheel, mp);") && car.Contains("h.view = drawn ? View.Select : View.Loading;"));
            Check("G8 EvilZod's card line: the cue's speaker talks (line.WhoAt)", lines.Contains("evilStage.Talk(m > 0f ? line.WhoAt(t) : null, m)"));

            string verdict = fail == 0 ? $"PASS {pass}/{pass + fail}" : $"FAIL {fail} of {pass + fail}";
            sb.Insert(0, "1.3 item 30 level select carousel: counted check " + verdict + (OzLevelL07.Registered ? " (level 7 registered: the screen is the carousel)" : " (three levels registered: the screen is the 1.2 row)") + "\n");
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/v1_3_20261030/item30_level_select_2x2"));
                Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "checks.txt"), sb.ToString());
                sb.AppendLine("report: " + Path.Combine(dir, "checks.txt").Replace('\\', '/'));
            }
            catch (System.Exception e) { sb.AppendLine("report not written: " + e.Message); }
            Debug.Log(T + sb);
            return verdict;
        }

        /// <summary>The item 30 package's lines file (the TEXT ONLY lines), "" when it is not there.</summary>
        static string PackageLines()
        {
            try { return File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_3/solutions/item30_level_select_2x2/lines_item30.json"))); }
            catch (System.Exception) { return ""; }
        }

        static string Read(string assetPath)
        {
            try { return File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath)); } catch (System.Exception) { return ""; }
        }
    }
}
