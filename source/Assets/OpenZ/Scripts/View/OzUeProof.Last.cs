using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor's Play proof, View/OzUeProof.cs and View/OzUeProof.Polish.cs): the last polish checks, at each picture
    /// size (1920 x 1080, 1366 x 768, 2560 x 1600):
    ///  1 with the field paused and the pointer kept on a blue grunt (its hover card showing), the unit card, the TABLE, the help card,
    ///    the background menu and the LOAD dialog each keep the hover card hidden while open (UI/OzUeHoverCard.Cover.cs) and the hover card
    ///    comes back once each closes; the pointer over each open card and over each panel is over the interface (no hover card); the
    ///    floating cards hide what is behind them (UI/OzUeSolid.cs: an opaque backing right under the 94 % plate);
    ///  2 the empty field's hint shows on the empty field, hides while a drop is armed and shows again after; the drop's words also keep
    ///    clear of the blast ring (View/OzUePlace.Blast.cs, View/OzUeWords.cs);
    ///  3 every quiet preview model is ONE tint: at most 4 % of its bright pixels paler than its own tint (a grey one: brighter);
    ///  4 at 1920 x 1080 the MEDIUM / LOW pictures are taken once the editor's shader compiler is idle (the editor draws a shader variant
    ///    it is still compiling with a flat cyan stand-in; a player build has every variant compiled in), and the floor there is HIGH's.
    /// </summary>
    public static partial class OzUeProof
    {
        /// <summary>A model's share of paler pixels it may have (the gatling's mirrored seat and ammo box were 6.5 .. 7.5 % before).</summary>
        const float PaleLimit = 0.04f;
        static readonly StringBuilder tintWords = new StringBuilder();
        static int tintBad, tintN, tintSkipped;
        static Color32 floorHigh; static bool floorHighSet;
        static float compileWait; static bool compileBusy;

        // ================================================================== 1 the hover card under the open cards
        /// <summary>Right after the hover_card picture (View/OzUeProof.Polish.cs HoverCardPicture): the field paused, the grunt under the
        /// pointer, its hover card showing.</summary>
        static IEnumerator HoverCovered(ZObject unit, RectTransform hover)
        {
            var shell = OzUeShellRoot.I; var hud = OzUnitEditor.I ? OzUnitEditor.I.Hud : null;
            if (unit == null || Gone(unit) || !hover || !shell || !hud) { Check(false, "1 the hover card's cover checks can run (a blue grunt, the hover card, the shell)"); yield break; }
            string[] what = { "the unit card", "the TABLE", "the help card", "the background menu", "the LOAD dialog" };
            var sb = new StringBuilder(); var solid = new StringBuilder();
            int n = 0, hidden = 0, back = 0, over = 0, solidN = 0, solidOk = 0;
            for (int s = 0; s < what.Length; s++)
            {
                RectTransform surf = null;
                if (s == 0) { OzUeStats.OpenCard("grunt", OzUeKeys.ShownBlue); yield return 2; surf = Named("OzUeCard", false); }
                else if (s == 1) { if (!OzUeStats.TableOpen) OzUeStats.ToggleTable(); yield return 2; surf = Named("OzUeTable", false); }
                else if (s == 2) { shell.Help.Show(shell.Top.HelpRect, OzUnitEditor.CanvasSize); yield return 2; surf = shell.Help.Root; }
                else if (s == 3)
                {
                    yield return FindByTip("BACKGROUND", 400f, 200f);
                    if (found.HasValue) { yield return Click(found.Value); yield return 0.3f; }
                    surf = Named("UE_BackgroundPopover", false);
                }
                else { shell.Dialogs.OpenLoad(OzUnitEditor.CanvasSize); yield return 2; surf = shell.Dialogs.LoadRoot; }
                bool open = surf != null && surf.gameObject.activeInHierarchy;
                // the pointer back on the grunt beside the open card: no hover card for half a second
                bool hid = true; float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 0.5f) { Move(UnitScreen(unit)); yield return 2; if (hover.gameObject.activeInHierarchy) hid = false; }
                var by = OzUeHoverCard.CoveredBy(); string byName = by != null ? by.name : "nothing";
                // the pointer on the open card itself: the interface takes it
                bool onIt = false;
                if (open) { Move(Centre(surf)); yield return 3; onIt = hud.PointerOverUi && !hover.gameObject.activeInHierarchy; }
                if (s == 0 || s == 2 || s == 3)
                {
                    solidN++; string d = OzUeSolid.Describe(surf, out bool sok); if (sok) solidOk++;
                    solid.Append(what[s]).Append(": ").Append(d).Append("; ");
                }
                // closed again: the hover card is back over the grunt
                if (s == 0) OzUeStats.CloseCard();
                else if (s == 1) { if (OzUeStats.TableOpen) OzUeStats.ToggleTable(); }
                else if (s == 2) shell.Help.Close(false);
                else if (s == 3) { if (surf != null && surf.gameObject.activeInHierarchy) yield return Tap(Key.Escape); }
                else shell.Dialogs.Close(false);
                yield return 2;
                bool again = false; t0 = Time.realtimeSinceStartup;
                while (!again && Time.realtimeSinceStartup - t0 < 1.5f) { Move(UnitScreen(unit)); yield return 2; again = hover.gameObject.activeInHierarchy; }
                n++;
                if (open && hid && by != null) hidden++;
                if (onIt) over++;
                if (again) back++;
                sb.Append(what[s]).Append(open ? "" : " (NOT OPENED)").Append(": the hover card ").Append(hid ? "hidden" : "SHOWN").Append(" (kept by ").Append(byName).Append("), the pointer on the card ")
                  .Append(onIt ? "over the interface" : "NOT OVER THE INTERFACE").Append(", back after closing ").Append(again ? "yes" : "NO").Append("; ");
            }
            Check(hidden == n && over == n && back == n, "1 the field's hover card stays hidden while a card or sheet is open over the field, the pointer kept on a blue grunt beside it ("
                  + n + " opened): " + sb + "it shows again over the grunt after each one closed");
            var list = Named("OzUeBalanceStats", true);
            if (list != null) { solidN++; string d = OzUeSolid.Describe(list, out bool lok); if (lok) solidOk++; solid.Append("the stat list: ").Append(d).Append("; "); }
            else solid.Append("the stat list: made on its first open, with its backing; ");
            Check(solidN >= 3 && solidOk == solidN, "1 the floating cards hide what is behind them (an opaque backing, the plate's own shape, under the 94 % plate): " + solid.ToString().TrimEnd(' ', ';'));
            // the pointer over every panel along the edges: over the interface, no hover card
            var ps = Panels(); int pOk = 0; var pn = new StringBuilder(); var pb = new StringBuilder();
            foreach (var p in ps)
            {
                Move(Centre(p.rt)); yield return 3;
                bool ok = hud.PointerOverUi && !hover.gameObject.activeInHierarchy;
                if (ok) pOk++; else pb.Append(' ').Append(p.name).Append(';');
                pn.Append(pn.Length > 0 ? ", " : "").Append(p.name);
            }
            Check(ps.Count >= 5 && pOk == ps.Count, "1 the pointer over each panel is over the interface and shows no hover card: " + pOk + " of " + ps.Count + " (" + pn + ")" + (pOk < ps.Count ? ": NOT:" + pb : ""));
            yield return Park();
        }

        /// <summary>Before the hover card's picture: whatever card, sheet or dialog is still open over the field is closed (Esc, at most
        /// 4 taps), as a player would; the hover card never shows under one (UI/OzUeHoverCard.Cover.cs).</summary>
        static IEnumerator Uncover()
        {
            var by = OzUeHoverCard.CoveredBy(); if (by == null) yield break;
            string first = by.name;
            for (int i = 0; i < 4 && OzUeHoverCard.CoveredBy() != null; i++) yield return Tap(Key.Escape);
            var still = OzUeHoverCard.CoveredBy();
            Info("before the hover card's picture " + first + " was still open over the field: closed with Esc" + (still != null ? " (still open: " + still.name + ")" : ""));
        }

        // ================================================================== 2 the empty field's hint while a drop is armed
        /// <summary>On the quiet, empty field (View/OzUeProof.Polish.cs GhostPictures, before its previews).</summary>
        static IEnumerator HintWhileArmed(Vector3 f)
        {
            var hint = Named("UE_FieldHint", true);
            if (!hint) { Check(false, "2 the empty field's hint (UE_FieldHint) is on the field layer"); yield break; }
            yield return Park(); yield return 0.6f;
            bool before = HintVisible(hint);
            yield return ArmGhost("grunt", OzUeKeys.ShownRed, 5, f); yield return 0.3f;
            bool armed = OzUePlace.Armed, during = HintVisible(hint);
            OzUePlace.Stop(); yield return Park(); yield return 0.6f;
            bool after = HintVisible(hint);
            Check(before && armed && !during && after, "2 the empty field's hint (\"" + OzUeFieldHint.Line1 + "\") shows on the empty field (" + before + "), hides while a drop is armed ("
                  + (armed && !during) + ": the arm bar and the drop's own words say what to do) and shows again after (" + after + ")");
        }

        static bool HintVisible(RectTransform hint)
        {
            if (!hint || !hint.gameObject.activeInHierarchy) return false;
            var g = hint.GetComponent<CanvasGroup>();
            return !g || g.alpha > 0.5f;
        }

        /// <summary>The empty field's hint on the screen now (the drop's words never lie on it: it hides while a drop is armed).</summary>
        static bool HintOnScreen() => HintVisible(Named("UE_FieldHint", true));

        /// <summary>The drop's word plates (screen rects) that touch the preview's blast ring; what: the clause for the line ("" = none drawn).</summary>
        static int BlastTouches(List<Rect> plates, out string what)
        {
            what = "";
            float bm = OzUePlaceDriver.WordsBlastM; var cam = OzUeDraw.Cam;
            if (!(bm > 0.1f) || !cam || plates == null) return 0;
            var xs = new float[96]; var ys = new float[96]; int n = 0; var c = OzUePlaceDriver.WordsCentre;
            for (int k = 0; k < 96; k++)
            {
                float a = k * (2f * Mathf.PI / 96f);
                var s = cam.WorldToScreenPoint(new Vector3(c.x + Mathf.Cos(a) * bm, c.y, c.z + Mathf.Sin(a) * bm));
                if (s.z <= cam.nearClipPlane) continue;
                xs[n] = s.x; ys[n] = s.y; n++;
            }
            int touch = 0;
            foreach (var p in plates) if (OzUeWords.SegmentsHit(xs, ys, n, p.xMin - 1f, p.yMin - 1f, p.xMax + 1f, p.yMax + 1f)) touch++;
            what = ", its " + F0(bm) + " m blast ring: " + touch + " touch it";
            return touch;
        }

        // ================================================================== 3 one tint per model
        /// <summary>After a quiet preview's picture: its models' paler share (View/OzUeProof.Polish.cs GhostPictures).</summary>
        static void TintSpread(Texture2D tex, string name)
        {
            var d = Object.FindFirstObjectByType<OzUePlaceDriver>(); var cam = OzUeDraw.Cam;
            tintN++;
            if (!tex || !d || !cam) { tintBad++; tintWords.Append(name).Append(" not read; "); return; }
            d.Shown(shownModels);
            var skip = MouseReadoutScreen();                                       // the Blueprint floor's mouse read-out (pale text by the pointer) is not the model
            float worst = 0f; int models = 0;
            foreach (var s in shownModels)
            {
                var r = ModelScreen(cam, s.go); if (r.width < 2f || r.height < 2f) continue;
                float share = PaleShare(tex, r, s.team == Team.Neutral, skip);
                if (share < 0f) continue;
                models++; if (share > worst) worst = share;
            }
            bool ok = models > 0 && worst <= PaleLimit;
            if (!ok) tintBad++;
            tintWords.Append(name).Append(' ').Append((worst * 100f).ToString("0.0", CultureInfo.InvariantCulture)).Append(" %").Append(models > 0 ? "" : " (no model read)").Append(ok ? "" : " NOT ONE TINT").Append("; ");
        }

        /// <summary>After the six quiet previews: one line.</summary>
        static void TintCheck()
        {
            Check(tintN > 0 && tintBad == 0, "1 every quiet preview model is one tint in every part (at most " + Mathf.RoundToInt(PaleLimit * 100f) + " % of its bright pixels paler than its own tint; "
                  + "a grey one: brighter; the Blueprint floor's mouse read-out left out: " + tintSkipped + " bright pixels under it skipped): the worst model of each preview: " + tintWords.ToString().TrimEnd(' ', ';'));
            tintWords.Clear(); tintBad = tintN = tintSkipped = 0;
        }

        /// <summary>The share of the model's bright pixels (the ghost over the dark floor, value 0.35 and up) that are paler than the model's
        /// own tint: saturation under 60 % of the median (a grey ghost: brighter than 1.3 x the median). -1 = too few pixels.</summary>
        static float PaleShare(Texture2D tex, Rect scr, bool grey, Rect skip)
        {
            float kx = tex.width / (float)Mathf.Max(1, Screen.width), ky = tex.height / (float)Mathf.Max(1, Screen.height);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(scr.xMin * kx), 0, tex.width - 1), x1 = Mathf.Clamp(Mathf.CeilToInt(scr.xMax * kx), 0, tex.width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(scr.yMin * ky), 0, tex.height - 1), y1 = Mathf.Clamp(Mathf.CeilToInt(scr.yMax * ky), 0, tex.height - 1);
            int w = x1 - x0 + 1, h = y1 - y0 + 1; if (w < 2 || h < 2) return -1f;
            var px = tex.GetPixels(x0, y0, w, h);
            var key = new List<float>(px.Length);
            for (int j = 0; j < h; j++) for (int i = 0; i < w; i++)               // texture rows and screen y both run bottom-up (as ModelScreen)
            {
                var c = px[j * w + i];
                float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                if (mx < 0.35f) continue;
                if (skip.width > 0f && skip.Contains(new Vector2((x0 + i) / kx, (y0 + j) / ky))) { tintSkipped++; continue; }
                key.Add(grey ? mx : (mx - mn) / mx);
            }
            if (key.Count < 40) return -1f;
            var sorted = new List<float>(key); sorted.Sort();
            float med = sorted[sorted.Count / 2]; int pale = 0;
            foreach (var k in key) if (grey ? k > med * 1.3f : k < med * 0.6f) pale++;
            return pale / (float)key.Count;
        }

        // ================================================================== 4 the presets: the shader compiler and the floor
        /// <summary>The editor compiles a shader variant the first time it is drawn and shows a flat cyan stand-in meanwhile (its
        /// asynchronous shader compilation): wait until nothing compiles (5 quiet checks in a row, at most 30 s). A player build has
        /// every variant compiled in.</summary>
        static IEnumerator ShadersReady()
        {
#if UNITY_EDITOR
            float t0 = Time.realtimeSinceStartup; int calm = 0;
            yield return 3;                                                                                    // the new preset's first frames ask for their variants
            while (calm < 5 && Time.realtimeSinceStartup - t0 < 30f)
            {
                bool busy = UnityEditor.ShaderUtil.anythingCompiling;
                if (busy) compileBusy = true;
                calm = busy ? 0 : calm + 1;
                yield return 2;
            }
            compileWait += Time.realtimeSinceStartup - t0;
#else
            yield return 1;
#endif
        }

        /// <summary>The x5 picture on HIGH: the floor's colour to compare the presets with.</summary>
        static void FloorOnHigh(Texture2D tex) { floorHigh = FloorColour(tex); floorHighSet = tex != null; }

        /// <summary>The x5 picture on MEDIUM / LOW: the floor as on HIGH.</summary>
        static void FloorOnPreset(Texture2D tex, int lvl)
        {
            var c = FloorColour(tex); int d = floorHighSet ? MaxDiff(c, floorHigh) : 999;
            Check(tex != null && floorHighSet && d <= PresetTolerance, "1 the floor on the " + OzQuality.Names[lvl] + " graphics preset as on HIGH: " + Hex(c) + ", on HIGH " + (floorHighSet ? Hex(floorHigh) : "-")
                  + " (" + d + " of 255 apart at most, " + PresetTolerance + " allowed); the picture taken once the editor's shader compiler was idle ("
                  + compileWait.ToString("0.0", CultureInfo.InvariantCulture) + " s waited, it was compiling: " + compileBusy + ")");
            compileWait = 0f; compileBusy = false;
        }

        /// <summary>The floor's colour in a picture: the per-channel median of a 32 x 18 grid over the free field, the shown preview
        /// models' rects left out.</summary>
        static Color32 FloorColour(Texture2D tex)
        {
            if (!tex) return default;
            var ff = OzUeInsets.FreeFieldScreen;
            if (ff.width < 4f || ff.height < 4f) ff = new Rect(0f, 0f, Screen.width, Screen.height);
            var models = new List<Rect>(); var cam = OzUeDraw.Cam; var d = Object.FindFirstObjectByType<OzUePlaceDriver>();
            if (d && cam) { d.Shown(shownModels); foreach (var s in shownModels) { var r = ModelScreen(cam, s.go); if (r.width > 0f) models.Add(r); } }
            float kx = tex.width / (float)Mathf.Max(1, Screen.width), ky = tex.height / (float)Mathf.Max(1, Screen.height);
            var rs = new List<int>(); var gs = new List<int>(); var bs = new List<int>();
            for (int i = 0; i < 32; i++)
                for (int j = 0; j < 18; j++)
                {
                    var p = new Vector2(ff.x + ff.width * (i + 0.5f) / 32f, ff.y + ff.height * (j + 0.5f) / 18f);
                    bool inModel = false; foreach (var r in models) if (r.Contains(p)) { inModel = true; break; }
                    if (inModel) continue;
                    int x = Mathf.Clamp(Mathf.RoundToInt(p.x * kx), 0, tex.width - 1), y = Mathf.Clamp(Mathf.RoundToInt(p.y * ky), 0, tex.height - 1);
                    Color32 c = tex.GetPixel(x, y);
                    rs.Add(c.r); gs.Add(c.g); bs.Add(c.b);
                }
            if (rs.Count == 0) return default;
            rs.Sort(); gs.Sort(); bs.Sort();
            int m = rs.Count / 2;
            return new Color32((byte)rs[m], (byte)gs[m], (byte)bs[m], 255);
        }
    }
}
