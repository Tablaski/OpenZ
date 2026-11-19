// 1.5 kit fps_chip_place_r3 (FAILS_LIST A6; owner, exe 13c): the FPS chip's
// counted checks (View/OzQuality.cs OzFpsChip). Menus: Editor/OzV15FpsChipCheck.cs.
// PLAN (pure, edit mode or Play; batch OpenZ.EditorTools.OzV15FpsChipCheck.Batch): at 13 screen sizes x the 6 interface scales x ON / DETAILED,
// (1) the Unit editor's top bar: the rect the chip's bar rule gives (OzFpsChip.BarSpot on OzUeTopBar.FreeOf, the same calls the game makes)
// against every piece of the bar (wordmark, crumb, RESET STATS, SAVE, LOAD, SHARE, ?, MAIN MENU, the eye; each taken the bar's full height),
// inside the bar and the screen; the sizes where the stretch is too narrow are listed (there the chip takes the free field's corner: Play);
// (2) a match's HUD in its standard layout (MENU, the top strip): the band under MENU holds the chip clear of both within BandDown.
// SCREEN (Play): the chip's rect against every visible picture and text of every canvas on the screen now, ON and DETAILED at the 6
// interface scales; appended to Logs/fps_chip_screens.txt.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenZ
{
    public static class OzFpsChipProof
    {
        /// <summary>The usual screens: 16:9, 16:10, 5:4, from 1280 x 720 to 3840 x 2160.</summary>
        public static readonly Vector2Int[] Sizes =
        {
            new Vector2Int(1920, 1080), new Vector2Int(1366, 768), new Vector2Int(1280, 720), new Vector2Int(1536, 864), new Vector2Int(1600, 900),
            new Vector2Int(2560, 1440), new Vector2Int(3840, 2160), new Vector2Int(1920, 1200), new Vector2Int(1680, 1050), new Vector2Int(1440, 900),
            new Vector2Int(1280, 800), new Vector2Int(2560, 1600), new Vector2Int(1280, 1024),
        };

        static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        static string R(Rect r) => F(r.x) + "," + F(r.y) + " " + F(r.width) + "x" + F(r.height);
        static bool Over(Rect a, Rect b) => Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > 0.01f && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > 0.01f;

        /// <summary>The plan check: appends PASS / FAIL lines, returns the FAILs (0 = passed).</summary>
        public static int PlanCheck(StringBuilder log)
        {
            if (log == null) log = new StringBuilder();
            int bad = 0, ok = 0;
            void Check(bool pass, string what) { if (pass) ok++; else bad++; log.AppendLine((pass ? "PASS  " : "FAIL  ") + what); }

            // (1) the Unit editor's top bar
            int cases = 0, inBar = 0, hits = 0, outside = 0; string first = null; var field = new List<string>(); var spots = new StringBuilder();
            foreach (var sz in Sizes)
                foreach (float s in OzText.ScalePresets)
                    for (int d = 0; d < 2; d++)
                    {
                        cases++;
                        Vector2 canvas = OzUnitEditor.CanvasFor(sz.x, sz.y, s);
                        bool compact = OzUeShellRoot.Compact(canvas);
                        var plan = OzUeTopBar.Layout(canvas, compact);
                        float ppu = sz.y / canvas.y, k = sz.y / 1080f, cw = d == 0 ? OzFpsChip.W : OzFpsChip.DetailW;
                        string what = sz.x + "x" + sz.y + " " + F(s * 100f) + " % " + (d == 0 ? "ON" : "DETAILED");
                        var free = OzUeTopBar.FreeOf(plan);
                        if (!OzFpsChip.BarSpot(free, cw * k / ppu, OzFpsChip.H * k / ppu, out Rect r))
                        {
                            field.Add(what + " (stretch " + F(free.width) + " u, chip " + F(cw * k / ppu) + " u)");
                            continue;
                        }
                        inBar++;
                        float bh = plan.h;
                        var pieces = new List<KeyValuePair<string, Rect>>
                        {
                            new KeyValuePair<string, Rect>("wordmark", new Rect(OzUeTopBar.LogoX, 0f, plan.logoW, bh)),
                            new KeyValuePair<string, Rect>("crumb", new Rect(plan.crumbX, 0f, plan.crumbW, bh)),
                            new KeyValuePair<string, Rect>("RESET STATS", new Rect(plan.reset.x, 0f, plan.reset.width, bh)),
                            new KeyValuePair<string, Rect>("SAVE", new Rect(plan.save.x, 0f, plan.save.width, bh)),
                            new KeyValuePair<string, Rect>("LOAD", new Rect(plan.load.x, 0f, plan.load.width, bh)),
                            new KeyValuePair<string, Rect>("SHARE", new Rect(plan.share.x, 0f, plan.share.width, bh)),
                            new KeyValuePair<string, Rect>("?", new Rect(plan.help.x, 0f, plan.help.width, bh)),
                            new KeyValuePair<string, Rect>("MAIN MENU", new Rect(plan.menu.x, 0f, plan.menu.width, bh)),
                        };
                        var eye = OzUePlan.Eye(canvas.x, compact); pieces.Add(new KeyValuePair<string, Rect>("the eye", new Rect(eye.x, 0f, eye.w, bh)));
                        string hit = null; foreach (var p in pieces) if (Over(r, p.Value)) { hits++; hit = hit ?? p.Key; }
                        var bar = new Rect(0f, 0f, plan.cw, plan.h - 1f);
                        bool inside = r.xMin >= bar.xMin - 0.01f && r.xMax <= bar.xMax + 0.01f && r.yMin >= bar.yMin - 0.01f && r.yMax <= bar.yMax + 0.01f
                                      && r.xMin * ppu >= -0.5f && r.xMax * ppu <= sz.x + 0.5f;
                        if (!inside) outside++;
                        if ((hit != null || !inside) && first == null) first = what + ": chip " + R(r) + (hit != null ? " over " + hit : " not inside the bar " + R(bar));
                        if (d == 0 && (s == 1f || s == 1.6f) && (sz.x == 1920 && sz.y == 1080 || sz.x == 1366 && sz.y == 768))
                            spots.Append("  " + what + ": chip " + R(r) + " u, crumb ends " + F(plan.crumbX + plan.crumbW) + ", RESET STATS at " + F(plan.reset.x) + ";");
                    }
            Check(inBar > 0 && hits == 0 && outside == 0, "E1 the Unit editor's top bar, " + cases + " cases (" + Sizes.Length + " screens x " + OzText.ScalePresets.Length
                  + " scales x ON / DETAILED): the chip in the bar's empty stretch in " + inBar + ", over a piece of the bar in " + hits + ", outside the bar / screen in " + outside
                  + (first != null ? "; first: " + first : "") + "." + spots);
            Check(true, "E2 too narrow for the chip (it takes the free field's top-right corner, counted in Play): " + field.Count + " of " + cases
                  + (field.Count > 0 ? ": " + string.Join("; ", field) : ""));

            // (2) a match's HUD in its standard layout: MENU (OzLayout.MenuButton) and the top strip (OzLayout.TopStrip) on the HUD canvas
            // (H / 1080 x the interface scale, OpenZHud.CanvasWidth); the chip Right u from the right, from Y down in 6 u steps, clear with 5 u of air
            int gCases = 0, gHome = 0, gBand = 0, gOut = 0; float gMax = 0f; string gFirst = null;
            foreach (var sz in Sizes)
                foreach (float s in OzText.ScalePresets)
                    for (int d = 0; d < 2; d++)
                    {
                        gCases++;
                        float k = sz.y / 1080f, kh = k * s, cwHud = sz.x / kh, cw = d == 0 ? OzFpsChip.W : OzFpsChip.DetailW;
                        var mb = OzLayout.MenuButton; var ts = OzLayout.TopStrip;
                        var menu = new Rect(mb.XOn(cwHud) * kh, mb.y * kh, mb.w * kh, mb.h * kh);                     // pixels, top-left origin
                        var strip = new Rect(ts.XOn(cwHud) * kh, ts.y * kh, ts.w * kh, ts.h * kh);
                        float found = -1f;
                        for (float t = OzFpsChip.Y; t <= OzFpsChip.Y + OzFpsChip.BandDown + 0.01f; t += 6f)
                        {
                            var c = new Rect(sz.x - (OzFpsChip.Right + cw) * k, (t - 5f) * k, cw * k, (OzFpsChip.H + 5f) * k);
                            if (!Over(c, menu) && !Over(c, strip)) { found = t; break; }
                        }
                        if (found < 0f) { gOut++; if (gFirst == null) gFirst = sz.x + "x" + sz.y + " " + F(s * 100f) + " %"; continue; }
                        if (found <= OzFpsChip.Y + 0.01f) gHome++; else gBand++;
                        gMax = Mathf.Max(gMax, found);
                    }
            Check(gOut == 0, "M1 a match's HUD, standard layout, " + gCases + " cases: the chip at home under MENU (y " + F(OzFpsChip.Y) + ") in " + gHome + ", lower in the band in "
                  + gBand + " (lowest y " + F(gMax) + ", band ends " + F(OzFpsChip.Y + OzFpsChip.BandDown) + "), no clear step in the band in " + gOut + (gFirst != null ? " (first " + gFirst + ")" : ""));

            log.AppendLine("[OpenZ V1.5 A6 FPS chip] plan: " + (bad == 0 ? "PASS" : "FAIL") + " " + ok + "/" + (ok + bad));
            return bad;
        }

        // ------------------------------------------------------------------------------------------------ the screen check (Play)
        /// <summary>A rect's screen pixels (origin bottom-left) for any canvas mode.</summary>
        static readonly Vector3[] corners = new Vector3[4];
        static Rect ScreenRect(RectTransform rt, Canvas cv)
        {
            rt.GetWorldCorners(corners);
            var root = cv ? cv.rootCanvas : null;
            Camera cam = root && root.renderMode != RenderMode.ScreenSpaceOverlay ? (root.worldCamera ? root.worldCamera : Camera.main) : null;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = cam ? RectTransformUtility.WorldToScreenPoint(cam, corners[i]) : (Vector2)corners[i];
                x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y);
            }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        static string NodePath(Transform t) { var s = t.name; for (int i = 0; i < 5 && t.parent; i++) { t = t.parent; s = t.name + "/" + s; } return s; }

        /// <summary>The chip's rect (screen pixels) against every visible picture and text of every other canvas: the hits (hits listed).
        /// Not counted: a full-screen layer (90 % of the screen each way: a background, a dim, a click catcher), a bar background along the
        /// whole top edge (90 % wide, from the screen's top, 12 % of its height at most: the Unit editor's top bar fill), nothing under 2 %
        /// alpha, an empty text, a picture culled by its mask. -1 = no chip drawn (counter off, a take, a cinematic).</summary>
        public static int CountScreen(out string chip, List<string> hits)
        {
            hits.Clear(); chip = "not drawn";
            var go = GameObject.Find("OpenZ_FpsCanvas"); var cv = go ? go.GetComponent<Canvas>() : null;
            var rt = cv ? cv.transform.Find("Root/FpsChip") as RectTransform : null;
            if (!cv || !cv.enabled || !rt || !rt.gameObject.activeInHierarchy) return -1;
            Rect c = ScreenRect(rt, cv); float sw = Screen.width, sh = Screen.height;
            chip = "px " + R(c) + " (right " + F(sw - c.xMax) + ", top " + F(sh - c.yMax) + ")";
            int n = 0;
            if (c.xMin < -0.5f || c.yMin < -0.5f || c.xMax > sw + 0.5f || c.yMax > sh + 0.5f) { n++; hits.Add("the chip is not inside the screen"); }
            var inner = new Rect(c.x + 1f, c.y + 1f, Mathf.Max(0f, c.width - 2f), Mathf.Max(0f, c.height - 2f));
            foreach (var g in UnityEngine.Object.FindObjectsByType<Graphic>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!g || !g.enabled || !g.gameObject.activeInHierarchy || !g.canvas || !g.canvas.isActiveAndEnabled) continue;
                if (g.canvas.rootCanvas == cv || g.transform.IsChildOf(cv.transform)) continue;
                if (g.canvasRenderer.cull || g.color.a * g.canvasRenderer.GetInheritedAlpha() < 0.02f) continue;
                if (g is TMP_Text tt && string.IsNullOrWhiteSpace(tt.text)) continue;
                if (g is Text lt && string.IsNullOrWhiteSpace(lt.text)) continue;
                var r = ScreenRect(g.rectTransform, g.canvas);
                if (r.width < 0.5f || r.height < 0.5f) continue;
                if (r.width >= 0.9f * sw && r.height >= 0.9f * sh) continue;
                if (r.width >= 0.9f * sw && r.yMax >= sh - 2f && r.height <= 0.12f * sh) continue;
                if (!Over(r, inner)) continue;
                n++; hits.Add(NodePath(g.transform) + " (" + g.GetType().Name + ", px " + R(r) + ")");
            }
            return n;
        }

        /// <summary>What is open now, for the report line.</summary>
        public static string Where()
        {
            var sb = new StringBuilder(SceneManager.GetActiveScene().name);
            var hud = OpenZHud.I;
            if (OzUnitEditor.Active) sb.Append(" | Unit editor" + (OzUnitEditor.PanelsHidden ? ", panels hidden (H)" : "") + (OzUeHooks.AnyModal ? ", a window open" : ""));
            if (hud)
            {
                if (hud.Pause && hud.Pause.IsOpen) sb.Append(" | pause menu");
                if (hud.EndScreen && hud.EndScreen.IsShown) sb.Append(" | end screen");
                if (hud.Modal) sb.Append(" | HUD modal");
                if (!hud.hudVisible) sb.Append(" | HUD hidden");
            }
            else sb.Append(" | no HUD");
            return sb.ToString();
        }

        public const string ScreenLog = "Logs/fps_chip_screens.txt";
    }

    /// <summary>The Play sweep (menu OpenZ/Checks/V1.5/A6 FPS chip: count this screen): on the screen as it is now, the chip ON and DETAILED at
    /// the 6 interface scales (OpenZHud.ApplyUiScale; the Unit editor lays itself out again by itself), each counted after 1.6 s (the chip
    /// looks for its place twice a second); the player's own counter mode and scale put back after; appended to Logs/fps_chip_screens.txt.</summary>
    public sealed class OzFpsChipProofRun : MonoBehaviour
    {
        public static bool Running { get; private set; }
        /// <summary>The last sweep: counted cases with a hit (-1 = none run yet).</summary>
        public static int LastBad = -1;
        string label;

        public static void Begin(string label)
        {
            if (Running) { Debug.LogWarning("[OpenZ V1.5 A6 FPS chip] a sweep is running: wait for its line"); return; }
            var go = new GameObject("OzFpsChipProofRun"); DontDestroyOnLoad(go);
            go.AddComponent<OzFpsChipProofRun>().label = label;
        }

        IEnumerator Start()
        {
            Running = true;
            int mode0 = OzQuality.FpsMode; float s0 = OzText.UiScale;
            var sb = new StringBuilder(); var hits = new List<string>(); int bad = 0, runs = 0, none = 0;
            string where = OzFpsChipProof.Where();
            sb.AppendLine("[OpenZ V1.5 A6 FPS chip] screen " + DateTime.Now.ToString("dd MMM yyyy HH:mm:ss", CultureInfo.InvariantCulture) + ": " + (string.IsNullOrEmpty(label) ? "" : label + " | ")
                          + where + " | " + Screen.width + "x" + Screen.height);
            try
            {
                foreach (int mode in new[] { 1, 2 })
                    foreach (float s in OzText.ScalePresets)
                    {
                        OzQuality.FpsMode = mode; OzText.UiScale = s; if (OpenZHud.I) OpenZHud.I.ApplyUiScale();
                        yield return new WaitForSecondsRealtime(1.6f);
                        int n = OzFpsChipProof.CountScreen(out string chip, hits); runs++;
                        if (n > 0) bad++; if (n < 0) none++;
                        sb.AppendLine("  " + (n > 0 ? "FAIL" : "PASS") + "  " + (mode == 2 ? "DETAILED" : "ON      ") + " scale " + (s * 100f).ToString("0", CultureInfo.InvariantCulture) + " %: chip " + chip
                                      + ", spot " + OzFpsChip.PlacedSpot + ", " + (n < 0 ? "NOT DRAWN here (counter hidden: a take / a cinematic)" : n + " hits"));
                        foreach (var h in hits) sb.AppendLine("          " + h);
                    }
            }
            finally
            {
                OzQuality.FpsMode = mode0; OzText.UiScale = s0; if (OpenZHud.I) OpenZHud.I.ApplyUiScale();
                Running = false;
            }
            LastBad = bad;
            sb.AppendLine("  => " + (bad == 0 ? "PASS" : "FAIL") + " " + (runs - bad) + "/" + runs + (none > 0 ? " (" + none + " not drawn)" : ""));
            try { Directory.CreateDirectory("Logs"); File.AppendAllText(OzFpsChipProof.ScreenLog, sb.ToString()); } catch (Exception e) { Debug.LogWarning("[OpenZ V1.5 A6 FPS chip] log: " + e.Message); }
            Debug.Log(sb.ToString());
            Destroy(gameObject);
        }
    }
}
