using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The counted check of the Unit editor's two light floor styles.
    /// Menu: OpenZ/Checks/V1.3/Light floors 2 - counted check. Edit mode runs S1-S8; in Play mode with the Unit editor playground open it
    /// also runs P1-P3 on the live field. Run menu 1 (OzV13LightFloorsBefore) on the 1.2 code BEFORE the package is applied.
    ///  S1 the style count: 12 styles (names, notes, looks), the ten dark ones first, 11 Snow field and 12 Desert dunes light.
    ///  S2 the ten old styles' table is the 1.2 table (every colour, the live flag, name and note), the default style is still 1.
    ///  S3 the ten old styles' popover pictures, drawn again by the thumbnail shader, equal the BEFORE pictures (133 x 75 and 399 x 225).
    ///  S4 the light styles' pictures: the floor is light (L* 60..80: 80 keeps the bloom off it) and its lines stand out from the floor
    ///     round them at least as far as its dark twin's lines (Micro-hex for Snow field, Topographic for Desert dunes) and 3 L* or more.
    ///  S5 the ink on the light floors, measured on their rendered pictures: every field mark (range rings red / blue, picked and hovered,
    ///     blast ring, flag / empty vehicle ring, the INK rings and obstacle box, the blocked line of sight), the game's selection and
    ///     squad member rings, order marker and path dots, attack range dots and selection brackets (red and blue), the health bar frame
    ///     and the label plate: contrast ratio 3:1 or more against the floor (the style's picture drawn with its lines off: its median,
    ///     the field's middle; the ratio at its darkest 2 %, the vignette's corner, is printed too, not judged); the label text on its
    ///     plate 4.5:1 or more (the plate over the median floor).
    ///  S6 a saved pick after a restart: oz_ue_backdrop 0..11 gives that style again (a 1.2 save 0..9: the same style and name as in 1.2),
    ///     out-of-range values fall back inside 0..11; SetStyle wraps.
    ///  S7 the BACKGROUND popover fits 12 pictures: 2 columns 828.9 u (one more row), 3 columns 601 u (4 rows, as with 10); at the canvas
    ///     sizes of spec 2.1 it stays inside the canvas.
    ///  S8 the dark styles keep the old ink path (no style 1..10 is light; OzUeHooks.DarkMarks false outside the playground).
    ///  P1 (playground) on each light style: the selection rings in their dark ink, the field labels on their plates, a screenshot.
    ///  P2 back on a dark style: the rings as in the levels again (HDR green), unit labels without plates.
    ///  P3 the player's style and the name label option are put back.
    /// </summary>
    public static class OzV13LightFloorsCheck
    {
        const string Tag = OzV13LightFloorsBefore.Tag;
        public static string Report = "not run";
        static int ok, bad;
        static readonly StringBuilder lines = new StringBuilder(), fails = new StringBuilder();
        static string dir;

        // the 1.2 table (View/OzUeBackdrop.cs of 1.2), copied here so a change to it shows
        static readonly string[] OldNames = { "Survey grid", "Dot lattice", "Micro-hex", "Topographic", "Range rings", "Scan lines", "Triangle mesh", "Blueprint", "Tactical map", "Carbon hatch" };
        static readonly string[] OldNotes =
        {
            "Steel blue square grid 1 / 10 / 50 m with small + marks",
            "Amber dots every 1 m, larger every 10 m, crosses every 50 m",
            "Violet 1 m hex cells, hex rings every 10 m, outline every 50 m",
            "Gold contour lines of a flat floor, every 5th an index line",
            "Radar green rings every 5 m from the field centre, spokes every 10 degrees",
            "Teal cross lines every 0.5 m, brighter every 10 m, lengthwise every 10 m",
            "Cold grey 2 m triangle mesh with 20 m triangles",
            "Deep blue sheet, white 1 / 5 / 25 m lines, edge ticks every 5 m",
            "Olive 10 m squares, 1 m dots, 100 m blocks with corner brackets, hatched bands",
            "Graphite cross hatch every 0.75 m, ember squares every 15 m and 75 m",
        };
        static readonly string[][] OldLooks =
        {
            new[] { "#05090F", "#7FB0EA", "#A8D4FF", "#5F8FC8", "#7FB0EA", "#A8D4FF" }, new[] { "#0A0805", "#C9A25A", "#E2C07A", "#C9A25A", "#C9A25A", "#C9A25A" },
            new[] { "#08060F", "#8A76D6", "#B3A2FF", "#8A76D6", "#B3A2FF", "#B3A2FF" }, new[] { "#0A0905", "#B89A52", "#F0D488", "#8F7A44", "#D7B25E", "#D7B25E" },
            new[] { "#040A06", "#4F9A63", "#9BE0AD", "#4F9A63", "#74C48A", "#74C48A" }, new[] { "#040A0A", "#3FA6A0", "#7FE0D8", "#3FA6A0", "#5CC4BD", "#5CC4BD" },
            new[] { "#07080A", "#9AA4B0", "#D4DCE6", "#9AA4B0", "#B8C2CE", "#B8C2CE" }, new[] { "#061226", "#6F8FD0", "#DBE7FF", "#DBE7FF", "#DBE7FF", "#DBE7FF" },
            new[] { "#0B0C08", "#8A8F62", "#C8CD94", "#8A8F62", "#A9AE7C", "#A9AE7C" }, new[] { "#0A0807", "#7A6A62", "#FFB070", "#7A6A62", "#C0703A", "#C0703A" },
        };

        [MenuItem("OpenZ/Checks/V1.3/Light floors 2 - counted check (Play mode in the Unit editor adds the field part)")]
        public static void Run()
        {
            ok = bad = 0; lines.Clear(); fails.Clear(); Report = "running";
            dir = OzV13LightFloorsBefore.Dir("");
            Directory.CreateDirectory(OzV13LightFloorsBefore.Dir("after"));
            try { EditPart(); }
            catch (System.Exception ex) { Debug.LogException(ex); Check(false, "exception in the edit-mode part: " + ex.Message); }
            if (EditorApplication.isPlaying && OzUeHooks.Active) { play = Field(); EditorApplication.update += Step; }
            else
            {
                Line("(the field part P1-P3 needs Play mode with the Unit editor playground open: "
                     + (EditorApplication.isPlaying ? "Play mode but no playground" : "edit mode") + ")");
                Finish();
            }
        }

        static void Check(bool pass, string what)
        {
            if (pass) ok++; else { bad++; fails.Append(" | ").Append(what); }
            Line($"{(pass ? "ok  " : "FAIL")} {what}");
        }
        static void Line(string s) { string l = Tag + " " + s; lines.AppendLine(l); Debug.Log(l); }

        static void Finish()
        {
            Report = $"{Tag} {(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad}" + (bad > 0 ? fails.ToString() : "");
            lines.AppendLine(Report);
            File.WriteAllText(Path.Combine(dir, "checks.txt"), lines.ToString());
            if (bad == 0) Debug.Log(Report); else Debug.LogError(Report);
        }

        // ------------------------------------------------------------------------------------------------ edit mode
        static void EditPart()
        {
            int n = OzUeBackdrop.StyleCount;
            // S1
            Check(n == 12 && OzUeBackdrop.Names.Length == n && OzUeBackdrop.Notes.Length == n && OzUeBackdrop.Looks.Length == n && OzUeBackdrop.FirstLight == 10,
                  $"S1 style count {n} (names {OzUeBackdrop.Names.Length}, notes {OzUeBackdrop.Notes.Length}, looks {OzUeBackdrop.Looks.Length}), first light style {OzUeBackdrop.FirstLight + 1}");
            bool lightOk = n == 12 && OzUeBackdrop.Looks[10].light && OzUeBackdrop.Looks[11].light && OzUeBackdrop.Names[10] == "Snow field" && OzUeBackdrop.Names[11] == "Desert dunes";
            for (int i = 0; i < 10 && i < n; i++) lightOk &= !OzUeBackdrop.Looks[i].light;
            Check(lightOk, $"S1 styles 1..10 dark, 11 {Nm(10)} and 12 {Nm(11)} light");
            // S2
            for (int i = 0; i < 10; i++)
            {
                var L = OzUeBackdrop.Looks[i]; var o = OldLooks[i];
                bool same = Eq(L.floor, o[0]) && Eq(L.tint, o[1]) && Eq(L.hi, o[2]) && Eq(L.c1, o[3]) && Eq(L.c2, o[4]) && Eq(L.c3, o[5]) && L.live == (i == 7) && !L.light
                            && OzUeBackdrop.Names[i] == OldNames[i] && OzUeBackdrop.Notes[i] == OldNotes[i];
                Check(same, $"S2 style {i + 1} {OldNames[i]}: colours {Hex(L.floor)} {Hex(L.tint)} {Hex(L.hi)} {Hex(L.c1)} {Hex(L.c2)} {Hex(L.c3)}, name and note as in 1.2");
            }
            Check(DefaultStyle() == 0, "S2 the default style (no saved value) is still 1. Survey grid");
            // S3 + S4 + the pictures
            string before = OzV13LightFloorsBefore.Dir("before"), after = OzV13LightFloorsBefore.Dir("after");
            var big = new Texture2D[n];
            for (int i = 0; i < n; i++)
                foreach (var s in OzV13LightFloorsBefore.Sizes)
                {
                    var t = OzV13LightFloorsBefore.Render(i, s.x, s.y);
                    if (t == null) { Check(false, $"S3 style {i + 1}: the thumbnail shader is missing"); continue; }
                    File.WriteAllBytes(Path.Combine(after, OzV13LightFloorsBefore.Name(i, s)), t.EncodeToPNG());
                    if (i < 10)
                    {
                        string bp = Path.Combine(before, OzV13LightFloorsBefore.Name(i, s));
                        if (!File.Exists(bp)) Check(false, $"S3 style {i + 1} {s.x} x {s.y}: no BEFORE picture ({bp}); run menu 1 on the 1.2 code first");
                        else
                        {
                            var b = new Texture2D(2, 2, TextureFormat.RGBA32, false, false); b.LoadImage(File.ReadAllBytes(bp));
                            Compare(b, t, out int differ, out int maxD);
                            Check(b.width == t.width && b.height == t.height && maxD <= 1,
                                  $"S3 style {i + 1} {OldNames[i]} {s.x} x {s.y}: {differ} of {t.width * t.height} pixels differ from 1.2, the largest by {maxD} of 255 (0 expected; 1 allowed for a driver's rounding)");
                            Object.DestroyImmediate(b);
                        }
                    }
                    if (s.x == 399) big[i] = t; else Object.DestroyImmediate(t);
                }
            Sheet(big, Path.Combine(after, "popover_pictures_2_columns.png"));
            var depthOf = new float[n];
            for (int i = 0; i < OzUeBackdrop.FirstLight && i < n; i++) if (big[i] != null) depthOf[i] = LineDepth(big[i], false);
            for (int i = OzUeBackdrop.FirstLight; i < n; i++)
            {
                if (big[i] == null) continue;
                var px = big[i].GetPixels32();
                var Ls = px.Select(p => Lstar(p)).OrderBy(v => v).ToArray();
                float med = Ls[Ls.Length / 2], depth = LineDepth(big[i], true);
                Check(med >= 60f && med <= 80f, $"S4 {Nm(i)}: the floor's lightness L* {med:0.0} (light: 60 .. 80; over 80 the floor itself would start to bloom)");
                int twin = i == 10 ? 2 : 3;                                                                 // the same lines: 11 = Micro-hex's, 12 = Topographic's
                Check(depth >= 3f && depth >= depthOf[twin], $"S4 {Nm(i)}: its lines stand L* {depth:0.0} under the floor round them; its dark twin {Nm(twin)}'s {depthOf[twin]:0.0} over its floor (3 or more, and at least the twin's)");
                // line pixels, and the vignette corner is shown, not judged: this display-space mix understates the scene-space marks)
                var bare = OzV13LightFloorsBefore.Render(i, 399, 225, false);
                bool drew = bare != null;
                var fpx = drew ? bare.GetPixels32() : px;
                if (drew) Object.DestroyImmediate(bare);
                string fw = drew ? "the floor" : "the picture WITH lines (the bare floor did not draw)";
                Color32 floorMed = MedianColour(fpx), floorDark = DarkColour(fpx, 0.02f);
                foreach (var (name, c, inked) in Marks())
                {
                    Color ink = inked ? OzUeBackdrop.Ink(c) : c;
                    float r = Ratio(Over(floorMed, ink), floorMed), rv = Ratio(Over(floorDark, ink), floorDark);
                    Check(r >= 3f, $"S5 {Nm(i)}: {name} {Hex(ink)} at {ink.a * 100f:0} %: {r:0.00} : 1 against {fw} {Hex(floorMed)} (median; at the vignette's darkest 2 % {Hex(floorDark)}: {rv:0.00} : 1, shown only)");
                }
                Color plate = OzUi.A(OzUi.GM950, 0.88f);
                Color onPlate = Over(Over(floorMed, plate), (Color)OzUi.Ink);
                float rp = Ratio(onPlate, Over(floorMed, plate));
                Check(rp >= 4.5f, $"S5 {Nm(i)}: label text INK on its plate {rp:0.00} : 1 (4.5 or more)");
            }
            foreach (var t in big) if (t) Object.DestroyImmediate(t);
            // S6
            SavedIndex();
            // S7
            float h2 = OzUeBackdropMenu.Height(2), h3 = OzUeBackdropMenu.Height(3);
            Check(Mathf.Abs(h2 - 828.875f) < 0.01f && Mathf.Abs(h3 - 601f) < 0.01f, $"S7 popover heights: 2 columns {h2:0.###} u (828.875), 3 columns {h3:0.###} u (601, as with 10 styles)");
            foreach (var cs in UI_OzUeControlsRoot_CheckSizes())
            {
                const float topY = 56f + 28f + 8f;                                                         // under the gear (OzUeBackdropMenu: the default gear rect 17,56 28x28 + 8 u)
                int cols = topY + h2 + 12f > cs.y ? 3 : 2; float ph = cols == 3 ? h3 : h2, pw = cols == 3 ? OzUeBackdropMenu.Width3 : OzUeBackdropMenu.Width2;
                float py = Mathf.Min(topY, Mathf.Max(OzUeBottomLayout.Gap, cs.y - ph - OzUeBottomLayout.Gap));
                Check(py + ph <= cs.y - OzUeBottomLayout.Gap + 0.01f && pw <= cs.x - 2f * OzUeBottomLayout.Gap,
                      $"S7 canvas {cs.x:0} x {cs.y:0} u: {cols} columns, {pw:0} x {ph:0.#} u at y {py:0.#}, inside the canvas");
            }
            // S8
            Check(!OzUeHooks.DarkMarks || EditorApplication.isPlaying, $"S8 OzUeHooks.DarkMarks {OzUeHooks.DarkMarks} outside the playground (false: the levels' rings and dots stay as they are)");
            var ink0 = OzUeBackdrop.Ink(OzUi.Ink);
            Check(Mathf.Abs(Lum(ink0) - OzUeBackdrop.InkLum) < 0.01f && ink0.a >= OzUeBackdrop.InkAlphaMin,
                  $"S8 the ink rule: INK -> {Hex(ink0)} (luminance {Lum(ink0):0.000}, {OzUeBackdrop.InkLum:0.000} wanted), alpha {ink0.a:0.00}; the keyline pale {Hex(OzUeBackdrop.InkKeyline(1f))}");
        }

        static string Nm(int i) => i < OzUeBackdrop.Names.Length ? $"{i + 1}. {OzUeBackdrop.Names[i]}" : "(none)";

        /// <summary>The marks drawn over the floor: (name, colour as in a level / on a dark floor, drawn through OzUeBackdrop.Ink on a light floor).</summary>
        static IEnumerable<(string, Color, bool)> Marks()
        {
            yield return ("range ring picked, red (75 %)", OzUi.A((Color32)OzUeUi.Ring(Team.Red), 0.75f), true);
            yield return ("range ring picked, blue (75 %)", OzUi.A((Color32)OzUeUi.Ring(Team.Blue), 0.75f), true);
            yield return ("range ring hovered, red (45 %)", OzUi.A((Color32)OzUeUi.Ring(Team.Red), 0.45f), true);
            yield return ("range ring hovered, blue (45 %)", OzUi.A((Color32)OzUeUi.Ring(Team.Blue), 0.45f), true);
            yield return ("blast ring (INK 30 %)", OzUi.A(OzUi.Ink, 0.3f), true);
            yield return ("flag / empty vehicle ring (90 %)", OzUi.A((Color32)OzUeUi.Ring(Team.Neutral), 0.9f), true);
            yield return ("hovered / picked ring, obstacle box (INK)", (Color)OzUi.Ink, true);
            yield return ("blocked line of sight (55 %)", new Color(1f, 140f / 255f, 110f / 255f, 0.55f), true);   // View/OzUeMarks.cs Blocked
            foreach (var f in new[] { "SelInk", "MinionInk", "MarkerInk", "PathInk" })
                yield return ("game " + f + " (View/SelectionOverlay.cs)", Static<Color>(typeof(SelectionOverlay), f), false);
            foreach (var t in new[] { Team.Red, Team.Blue })
            {
                yield return ("game attack range dots, " + t + " (View/AttackRangeRings.cs, OzUeHooks.DeepMark)", OzUeHooks.DeepMark(t), false);
                yield return ("game selection brackets 85 %, " + t + " (UI/OpenZUnitOverlay.cs, OzUeHooks.DeepMark)", OzUi.A((Color32)OzUeHooks.DeepMark(t), 0.85f), false);
            }
            yield return ("health bar frame (85 %)", new Color(8f / 255f, 10f / 255f, 9f / 255f, 0.85f), false);      // View/OzUeDraw.cs NewBar
            yield return ("label plate (GM950 88 %)", OzUi.A(OzUi.GM950, 0.88f), false);                             // View/OzUeDraw.cs PlateFill
        }

        static (float x, float y)[] UI_OzUeControlsRoot_CheckSizes() => OzUeControlsRoot.CheckSizes.Select(s => (s.cw, s.ch)).ToArray();

        static int DefaultStyle()
        {
            var fl = typeof(OzUeBackdrop).GetField("loaded", BindingFlags.NonPublic | BindingFlags.Static);
            const string key = "oz_ue_backdrop";
            bool had = PlayerPrefs.HasKey(key); int keep = PlayerPrefs.GetInt(key, 0);
            PlayerPrefs.DeleteKey(key); fl.SetValue(null, false);
            int s = OzUeBackdrop.Style;
            if (had) PlayerPrefs.SetInt(key, keep); fl.SetValue(null, false);
            return s;
        }

        static void SavedIndex()
        {
            var fl = typeof(OzUeBackdrop).GetField("loaded", BindingFlags.NonPublic | BindingFlags.Static);
            const string key = "oz_ue_backdrop";
            bool had = PlayerPrefs.HasKey(key); int keep = PlayerPrefs.GetInt(key, 0);
            try
            {
                var got = new StringBuilder(); bool all = true;
                foreach (int v in new[] { -3, 0, 3, 7, 9, 10, 11, 12, 99 })
                {
                    PlayerPrefs.SetInt(key, v); fl.SetValue(null, false);                                    // a restart: the next read loads the saved value
                    int s = OzUeBackdrop.Style, want = Mathf.Clamp(v, 0, OzUeBackdrop.StyleCount - 1);
                    bool same = s == want && (v < 0 || v > 9 || OzUeBackdrop.Names[s] == OldNames[v]);
                    all &= same; got.Append($" {v}->{s + 1}{(same ? "" : " (WRONG)")}");
                }
                Check(all, "S6 saved oz_ue_backdrop after a restart (saved -> style):" + got + "; a 1.2 save 0..9 keeps its style and name");
                PlayerPrefs.SetInt(key, 4); fl.SetValue(null, false);
                OzUeBackdrop.SetStyle(10); int a = OzUeBackdrop.Style; fl.SetValue(null, false); int a2 = OzUeBackdrop.Style;
                OzUeBackdrop.SetStyle(12); int b = OzUeBackdrop.Style;
                OzUeBackdrop.SetStyle(-1); int c = OzUeBackdrop.Style;
                Check(a == 10 && a2 == 10 && b == 0 && c == 11, $"S6 a pick of 11 is saved and read back after a restart ({a + 1}, {a2 + 1}); SetStyle(12) -> {b + 1}, SetStyle(-1) -> {c + 1} (wraps)");
            }
            finally
            {
                if (had) PlayerPrefs.SetInt(key, keep); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save(); fl.SetValue(null, false);
            }
        }

        // ------------------------------------------------------------------------------------------------ Play mode, the playground
        static IEnumerator play; static float waitUntil;
        static void Step()
        {
            if (play == null || !EditorApplication.isPlaying) { EditorApplication.update -= Step; if (play != null) { play = null; Check(false, "P Play mode ended during the run"); Finish(); } return; }
            if (Time.realtimeSinceStartup < waitUntil) return;
            bool more;
            try { more = play.MoveNext(); }
            catch (System.Exception ex) { Debug.LogException(ex); more = false; Check(false, "P exception: " + ex.Message); }
            if (!more) { play = null; EditorApplication.update -= Step; Finish(); return; }
            if (play.Current is float s) waitUntil = Time.realtimeSinceStartup + s;
        }

        static IEnumerator Field()
        {
            int style0 = OzUeBackdrop.Style; bool labels0 = OzUeBackdrop.NameLabels;
            var so = Object.FindFirstObjectByType<SelectionOverlay>();
            var fDark = typeof(SelectionOverlay).GetField("darkInk", BindingFlags.NonPublic | BindingFlags.Instance);
            var fSel = typeof(SelectionOverlay).GetField("selMat", BindingFlags.NonPublic | BindingFlags.Instance);
            Color selInk = Static<Color>(typeof(SelectionOverlay), "SelInk"), selLevel = Static<Color>(typeof(SelectionOverlay), "SelColor");
            selLevel = new Color(selLevel.r * 1.9f, selLevel.g * 1.9f, selLevel.b * 1.9f, selLevel.a);
            if (OzUePlayground.Placed().Count(o => o != null && o.IsUnit) < 2) { OzUePlayground.SampleFight(); yield return 3f; }
            OzUeBackdrop.SetNameLabels(true);
            for (int i = OzUeBackdrop.FirstLight; i < OzUeBackdrop.StyleCount; i++)
            {
                OzUeBackdrop.SetStyle(i); yield return 1.5f;
                bool dark = so && (bool)fDark.GetValue(so);
                var m = so ? fSel.GetValue(so) as Material : null;
                Color c = m ? m.GetColor("_Color") : Color.clear;
                Check(OzUeHooks.DarkMarks && dark && Near(c, selInk), $"P1 {Nm(i)}: DarkMarks {OzUeHooks.DarkMarks}, the selection rings in their dark ink {dark} ({c})");
                Plates(out int on, out int all);
                Check(all > 0 && on == all, $"P1 {Nm(i)}: field labels on their dark plates: {on} of {all} (name labels on every unit)");
                Check(OzUeDraw.LastLabels > 0, $"P1 {Nm(i)}: marks drawn in the last frame: {OzUeDraw.LastStrokes} strokes, {OzUeDraw.LastLabels} labels, {OzUeDraw.LastBars} bars");
                string shot = Path.Combine(OzV13LightFloorsBefore.Dir("after"), $"field_{i + 1:00}.png");
                ScreenCapture.CaptureScreenshot(shot); yield return 0.5f;
                Line($"P1 {Nm(i)}: screenshot {shot} (look: red and blue units, rings, labels, bars on the light floor)");
            }
            int back = style0 < OzUeBackdrop.FirstLight ? style0 : 0;
            OzUeBackdrop.SetStyle(back); yield return 1.5f;
            {
                bool dark = so && (bool)fDark.GetValue(so);
                var m = so ? fSel.GetValue(so) as Material : null;
                Color c = m ? m.GetColor("_Color") : Color.clear;
                Check(!OzUeHooks.DarkMarks && !dark && Near(c, selLevel), $"P2 {Nm(back)}: the selection rings as in the levels again (HDR green {c})");
                Plates(out int on, out int all);
                Check(all > 0 && on == 0, $"P2 {Nm(back)}: unit labels without plates: {on} of {all} plated");
            }
            OzUeBackdrop.SetStyle(style0); OzUeBackdrop.SetNameLabels(labels0); yield return 0.3f;
            Check(OzUeBackdrop.Style == style0 && OzUeBackdrop.NameLabels == labels0, $"P3 put back: style {Nm(style0)}, name labels {(labels0 ? "on" : "off")}");
        }

        /// <summary>The active world labels (View/OzUeDraw.cs OzUe_WorldLabel) and how many show their plate.</summary>
        static void Plates(out int on, out int all)
        {
            on = all = 0;
            foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
            {
                if (rt.name != "OzUe_WorldLabel" || !rt.gameObject.activeInHierarchy) continue;
                all++;
                var p = rt.Find("Plate");
                if (p && p.gameObject.activeSelf) on++;
            }
        }

        // ------------------------------------------------------------------------------------------------ colour
        static T Static<T>(System.Type t, string name)
        {
            var f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            return f != null ? (T)f.GetValue(null) : default;
        }
        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;
        static bool Eq(Color32 c, string hex) { var h = OzUeBackdrop.Hex(hex); return c.r == h.r && c.g == h.g && c.b == h.b && c.a == h.a; }
        static string Hex(Color32 c) => $"#{c.r:X2}{c.g:X2}{c.b:X2}";
        static string Hex(Color c) => Hex((Color32)c);
        static float Lum(Color c) { var l = c.linear; return 0.2126f * l.r + 0.7152f * l.g + 0.0722f * l.b; }
        static float Ratio(Color a, Color b) { float x = Lum(a), y = Lum(b); return (Mathf.Max(x, y) + 0.05f) / (Mathf.Min(x, y) + 0.05f); }
        static float Lstar(Color32 p) { float y = Lum(p); return y > 0.008856f ? 116f * Mathf.Pow(y, 1f / 3f) - 16f : 903.3f * y; }
        /// <summary>c over the floor colour, blended in linear light (as the screen mixes it), opaque result.</summary>
        static Color Over(Color under, Color c)
        {
            Color u = under.linear, k = c.linear; float a = c.a;
            var m = new Color(u.r * (1f - a) + k.r * a, u.g * (1f - a) + k.g * a, u.b * (1f - a) + k.b * a, 1f);
            return m.gamma;
        }
        static Color32 MedianColour(Color32[] px)
        {
            var r = px.Select(p => p.r).OrderBy(v => v).ToArray(); var g = px.Select(p => p.g).OrderBy(v => v).ToArray(); var b = px.Select(p => p.b).OrderBy(v => v).ToArray();
            int k = px.Length / 2; return new Color32(r[k], g[k], b[k], 255);
        }
        /// <summary>The mean colour of the darkest share of the pixels (the vignette's corners and the lines).</summary>
        static Color32 DarkColour(Color32[] px, float share)
        {
            var d = px.OrderBy(p => Lum(p)).Take(Mathf.Max(1, (int)(px.Length * share))).ToArray();
            return new Color32((byte)d.Average(p => p.r), (byte)d.Average(p => p.g), (byte)d.Average(p => p.b), 255);
        }
        /// <summary>How far the lines stand from the floor round them: the 99.5th percentile of (the 9 x 9 mean - the pixel) in L*, for a
        /// light floor (dark lines), or (the pixel - the mean) for a dark one. The vignette and the middle light change too slowly to count.</summary>
        static float LineDepth(Texture2D t, bool light)
        {
            int w = t.width, h = t.height; var px = t.GetPixels32();
            var S = new double[(w + 1) * (h + 1)];
            for (int y = 0; y < h; y++) { double row = 0; for (int x = 0; x < w; x++) { row += Lstar(px[y * w + x]); S[(y + 1) * (w + 1) + x + 1] = S[y * (w + 1) + x + 1] + row; } }
            var d = new List<float>(w * h); const int R = 4;
            for (int y = R; y < h - R; y++)
                for (int x = R; x < w - R; x++)
                {
                    int x0 = x - R, x1 = x + R + 1, y0 = y - R, y1 = y + R + 1;
                    double sum = S[y1 * (w + 1) + x1] - S[y0 * (w + 1) + x1] - S[y1 * (w + 1) + x0] + S[y0 * (w + 1) + x0];
                    float mean = (float)(sum / ((2 * R + 1) * (2 * R + 1))), L = Lstar(px[y * w + x]);
                    d.Add(light ? mean - L : L - mean);
                }
            d.Sort();
            return d.Count > 0 ? d[(int)(d.Count * 0.995f)] : 0f;
        }

        static void Compare(Texture2D a, Texture2D b, out int differ, out int maxD)
        {
            differ = 0; maxD = 0;
            if (a.width != b.width || a.height != b.height) { differ = -1; maxD = 255; return; }
            var x = a.GetPixels32(); var y = b.GetPixels32();
            for (int i = 0; i < x.Length; i++)
            {
                int d = Mathf.Max(Mathf.Abs(x[i].r - y[i].r), Mathf.Max(Mathf.Abs(x[i].g - y[i].g), Mathf.Abs(x[i].b - y[i].b)));
                if (d > 0) differ++;
                if (d > maxD) maxD = d;
            }
        }
        /// <summary>All the pictures in the popover's 2-column order, on the popover's dark plate.</summary>
        static void Sheet(Texture2D[] pics, string path)
        {
            if (pics.Length == 0 || pics[0] == null) return;
            int w = pics[0].width, h = pics[0].height, gap = 12, rows = (pics.Length + 1) / 2;
            var s = new Texture2D(2 * w + 3 * gap, rows * h + (rows + 1) * gap, TextureFormat.RGBA32, false, false);
            var fill = Enumerable.Repeat((Color32)OzUi.GM900, s.width * s.height).ToArray(); s.SetPixels32(fill);
            for (int i = 0; i < pics.Length; i++)
                if (pics[i] != null) s.SetPixels32(gap + (i % 2) * (w + gap), s.height - (gap + (i / 2) * (h + gap)) - h, w, h, pics[i].GetPixels32());
            s.Apply(); File.WriteAllBytes(path, s.EncodeToPNG()); Object.DestroyImmediate(s);
        }
    }
}
