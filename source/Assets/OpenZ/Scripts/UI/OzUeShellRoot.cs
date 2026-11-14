using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUnitEditor.cs): module P4's root component (added by OzUnitEditor, built once the HUD has its
    /// Root). The shell of prototype C round 2d: the top bar (SAVE / LOAD / SHARE / ? / MAIN MENU), the toast, the arm bar, the help
    /// card, the share and LOAD dialogs, and the drivers of the keys (View/OzUeKeys.cs), the placing (View/OzUePlace.cs), the world
    /// marks (View/OzUeMarks.cs) and their drawing (View/OzUeDraw.cs). MAIN MENU, and Esc when nothing else is open, ask LEAVE THE UNIT
    /// EDITOR? (1.2 item 109: STAY | LEAVE, and SAVE &amp; LEAVE while Custom changes are not saved; the field paused while it shows) and
    /// RESET STATS asks before it puts every Custom value back to OpenZ (UI/OzUeLeave.cs). Layout follows the canvas the editor has now
    /// (OzUnitEditor.CanvasSize) every time it changes; nothing is measured on 1920.
    /// </summary>
    public sealed class OzUeShellRoot : MonoBehaviour
    {
        public static OzUeShellRoot I { get; private set; }
        OpenZHud hud; bool built;
        internal OzUeTopBar Top { get; private set; }
        internal OzUeArmBar Arm { get; private set; }
        internal OzUeHelpCard Help { get; private set; }
        internal OzUeDialogs Dialogs { get; private set; }
        /// <summary>The leave question and its two confirms.</summary>
        internal OzUeLeave Ask { get; private set; }
        Vector2 lastCanvas; bool lastCompact;
        /// <summary>Checks: MAIN MENU asked to leave; the editor left through it.</summary>
        public static int LeaveAsked, Left;

        void Awake()
        {
            I = this;
            gameObject.AddComponent<OzUeKeysDriver>();
            gameObject.AddComponent<OzUePlaceDriver>();
            gameObject.AddComponent<OzUeMarks>();
            gameObject.AddComponent<OzUeDrawDriver>();
        }

        /// <summary>Builds the shell on the editor's layers (once).</summary>
        public void Build(OzUnitEditor ed)
        {
            if (built || ed == null) return;
            hud = ed.Hud;
            var panels = OzUnitEditor.Layer(UeLayer.Panels);
            Top = new OzUeTopBar(panels, hud, OzUeDialogs.SaveNow, OpenLoad, Share, ToggleHelp, AskLeave, AskReset);
            Arm = new OzUeArmBar(panels, hud);
            Help = new OzUeHelpCard(OzUnitEditor.Layer(UeLayer.Popovers), hud);
            Dialogs = new OzUeDialogs(OzUnitEditor.Layer(UeLayer.Dialogs), hud);
            Ask = new OzUeLeave(OzUnitEditor.Layer(UeLayer.Dialogs), hud, Leave);                            // the leave question and its two confirms (UI/OzUeLeave.cs)
            OzUeToast.Build(OzUnitEditor.Layer(UeLayer.Tips));
            OzUeKeys.OnEsc(OzUeKeys.EscLeave, Ask.EscClose);                                                 // Esc in the question = STAY, in a confirm = CANCEL / BACK
            OzUeKeys.OnEsc(OzUeKeys.EscNothingOpen, EscAsksLeave);                                           // Esc with nothing else to close asks GO TO THE MAIN MENU?
            OzUeKeys.OnEsc(OzUeKeys.EscDialog, Dialogs.EscClose);
            OzUeKeys.OnEsc(OzUeKeys.EscPopover, Help.EscClose);
            OzUeKeys.OnEsc(OzUeKeys.EscPlacing, OzUePlace.EscClose);
            OzUePlace.Changed += Arm.Refresh;
            OzUeControls.Changed += Arm.Refresh;
            OzUnitEditor.PanelsHiddenChanged += PanelsHidden;
            built = true;
            Relayout(OzUnitEditor.CanvasSize, Compact(OzUnitEditor.CanvasSize));
            Debug.Log("[OpenZ UnitEditor] P4 shell built: top bar " + Top.H + " u, " + OzUeKeys.CloserCount + " Esc closers, folder " + OzUeFiles.Folder);
        }

        void OnDestroy()
        {
            if (Arm != null) { OzUePlace.Changed -= Arm.Refresh; OzUeControls.Changed -= Arm.Refresh; }
            OzUnitEditor.PanelsHiddenChanged -= PanelsHidden;
            Dialogs?.Dispose(); Ask?.Dispose();
            Top?.Dispose();
            OzUeKeys.Reset(); OzUePlace.Reset(); OzUeToast.Reset();
            OzUeShell.DisposeMeasure();
            if (I == this) I = null;
        }

        void Update()
        {
            if (!built) return;
            var cs = OzUnitEditor.CanvasSize; bool compact = Compact(cs);
            if ((cs - lastCanvas).sqrMagnitude > 0.25f || compact != lastCompact) Relayout(cs, compact);
            Top.Tick();
            Arm.Tick(Top.H, cs);
            Help.Tick(Top.help.Rt);
            Dialogs.Tick();
            Ask.Tick();
            OzUeToast.Tick(Top.H, Arm.Visible, cs);
        }

        void Relayout(Vector2 cs, bool compact)
        {
            lastCanvas = cs; lastCompact = compact;
            Top.Apply(cs, compact);
            if (Help.Open) Help.Place(Top.HelpRect, cs);
            if (Dialogs.Open) Dialogs.Place(cs);
            if (Ask.Open) Ask.Place(cs);
        }

        /// <summary>DESIGN_SPEC 2.3 COMPACT: the stats box would be shorter than 705 u (box = min(CH - 40 - 24 - insetB, 860)).</summary>
        internal static bool Compact(Vector2 cs) { float ib = OzUeInsets.Get(UeSide.Bottom); if (ib <= 0f) ib = 12f + 72f; return Mathf.Min(cs.y - 40f - 24f - ib, 860f) < 705f; }

        void PanelsHidden(bool hidden) { if (hidden) Help.Close(false); Arm.Refresh(); }

        // ------------------------------------------------------------------ the top bar's actions
        void OpenLoad() { Help.Close(false); Dialogs.OpenLoad(OzUnitEditor.CanvasSize); }

        void Share()
        {
            Help.Close(false);
            if (OzUeFiles.Save(out _, out string error)) { OzUeDialogs.SavedToast(); AudioDirector.UI("ui_confirm"); Dialogs.OpenShare(OzUnitEditor.CanvasSize); }   // the file and the saved Custom profile; the toast SAVED with the file's name
            else { OzUeToast.Show(error ?? "COULD NOT WRITE THE FILE.", true); AudioDirector.UI("ui_error"); }
        }

        void ToggleHelp() { Help.Toggle(Top.HelpRect, OzUnitEditor.CanvasSize); }

        /// <summary>MAIN MENU (and Esc with nothing else open): LEAVE THE UNIT EDITOR? (UI/OzUeLeave.cs); the field stands still while it
        /// shows.</summary>
        void AskLeave()
        {
            LeaveAsked++;
            Help.Close(false);
            Ask.OpenLeave(OzUnitEditor.CanvasSize);
        }

        bool EscAsksLeave() { AskLeave(); return true; }

        /// <summary>RESET STATS: the confirm (or a toast when the stats are the defaults already).</summary>
        void AskReset() { Help.Close(false); Ask.OpenReset(OzUnitEditor.CanvasSize); }

        /// <summary>Back to the main menu (the leave question's go buttons, UI/OzUeLeave.cs).</summary>
        internal void Leave()
        {
            Left++;
            OzUePlace.Stop(); Dialogs.Close(false); Help.Close(false);
            OzSfx.Ui("ui_menu_back");
            Debug.Log("[OpenZ UnitEditor] MAIN MENU: leaving the playground");
            OzScenes.MainMenu();
        }

        // ------------------------------------------------------------------ SelfCheck (edit mode or Play: pure layouts at the 6 canvas sizes)
        static readonly Vector2[] Sizes = { new Vector2(1920, 1080), new Vector2(1607, 904), new Vector2(1807, 859), new Vector2(1920, 1080), new Vector2(1920, 1080), new Vector2(1728, 1080) };
        static readonly string[] SizeNames = { "1920x1080", "1366x768 (floor 0.85)", "1536x730 (floor 0.85)", "1600x900", "2560x1440", OzProofWords.Screen1610 };

        /// <summary>Module P4's counted layout check: appends PASS / FAIL lines, returns the number of FAILs (0 = all passed).</summary>
        public static int SelfCheck(StringBuilder log)
        {
            if (log == null) log = new StringBuilder();
            int bad = 0, ok = 0;
            void Check(bool pass, string what) { if (pass) ok++; else bad++; log.AppendLine((pass ? "PASS  P4 " : "FAIL  P4 ") + what); }
            GameObject tmp = null;
            try
            {
                tmp = new GameObject("OzUe_P4SelfCheck", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                var rt = (RectTransform)tmp.transform; rt.sizeDelta = new Vector2(1920, 1080);
                var help = new OzUeHelpCard(rt, null);
                var dialogs = new OzUeDialogs(rt, null);
                int a = help.Audit(log, "texts") + dialogs.Audit(log);
                Check(a == 0, "help card, SHARE and LOAD windows' texts: none under 11 u; both windows " + OzUeWinPlan.MaxH.ToString("0", CultureInfo.InvariantCulture)
                              + " u tall at most, LOAD as wide with 1 .. 6 rows (" + a + " problems)");
                // 1.2 item 109: the SHARE window's row (DISCORD, COPY FILE PATH | OPEN FOLDER, the main button) and LOAD's (OPEN FOLDER | LOAD),
                // each button where its plan puts it (TextMeshPro's widths), inside the plate's margins, none over another, one row
                bool rowOk = dialogs.RowsAsPlanned(out string rowText);
                Check(rowOk, "the SHARE and LOAD windows' buttons where their plans put them, one row inside the plate's margins, none over another: " + rowText);
                // both windows (LOAD with six rows) at 1366 x 768 .. 3840 x 2160 with UI SCALE 100 % and 160 % (OzUnitEditor.CanvasFor): full size
                // (never scaled down) and inside the canvas with 16 u to spare
                int[,] screens = { { 1366, 768 }, { 1600, 900 }, { 1920, 1080 }, { 2560, 1440 }, { 2560, 1600 }, { 3840, 2160 } };
                bool fits = true; var tight = new StringBuilder(); float least = float.MaxValue;
                for (int k = 0; k < screens.GetLength(0); k++)
                    foreach (float s in new[] { 1f, 1.6f })
                    {
                        var cs = OzUnitEditor.CanvasFor(screens[k, 0], screens[k, 1], s);
                        bool inside = OzUeWinPlan.WinOk(dialogs.SharePlan, cs.x, cs.y, out string why1) & OzUeWinPlan.WinOk(dialogs.LoadPlanMax, cs.x, cs.y, out string why2);
                        var sr = OzUeDialogs.Plan(cs, dialogs.ShareW, dialogs.ShareHeight); var lr = OzUeDialogs.Plan(cs, dialogs.LoadW, dialogs.LoadHeight);
                        float spare = Mathf.Min(Mathf.Min(sr.x, cs.y - sr.yMax), Mathf.Min(lr.x, cs.y - lr.yMax));
                        fits &= inside;
                        string at = screens[k, 0] + "x" + screens[k, 1] + " at " + Mathf.RoundToInt(s * 100f) + " % (canvas " + cs.x.ToString("0", CultureInfo.InvariantCulture) + " x " + cs.y.ToString("0", CultureInfo.InvariantCulture) + ")";
                        if (spare < least) { least = spare; tight.Clear().Append(at); }
                        if (!inside) log.AppendLine("INFO  P4 a window does not hold at " + at + ": " + (why1 ?? why2));
                    }
                Check(fits, "the SHARE window (" + dialogs.ShareW.ToString("0", CultureInfo.InvariantCulture) + " x " + dialogs.ShareHeight.ToString("0", CultureInfo.InvariantCulture)
                            + " u) and the LOAD window with six rows (" + dialogs.LoadW.ToString("0", CultureInfo.InvariantCulture) + " x " + dialogs.LoadHeight.ToString("0", CultureInfo.InvariantCulture)
                            + " u) full size and inside the canvas at 1366x768 .. 3840x2160, UI SCALE 100 % and 160 % (12 sizes); the tightest " + tight + ", "
                            + least.ToString("0", CultureInfo.InvariantCulture) + " u to spare");
                for (int i = 0; i < Sizes.Length; i++)
                {
                    var cs = Sizes[i]; string at = SizeNames[i] + " (canvas " + cs.x + " x " + cs.y + ")";
                    bool compact = Mathf.Min(cs.y - 40f - 24f - 84f, 860f) < 705f;
                    var p = OzUeTopBar.Layout(cs, compact);
                    Check(Mathf.Approximately(p.h, compact ? 36f : 40f) && p.cw == cs.x, "E001 top bar 0,0 " + cs.x + " x " + p.h + " at " + at);
                    Check(Mathf.Abs(p.menu.xMax - (cs.x - 12f)) < 0.01f && Mathf.Abs(p.help.xMax + 14f - p.menu.x) < 0.01f && Mathf.Abs(p.share.xMax + 20f - p.help.x) < 0.01f
                          && Mathf.Abs(p.load.xMax + 6f - p.share.x) < 0.01f && Mathf.Abs(p.save.xMax + 6f - p.load.x) < 0.01f,
                          "E004-E009 right group at " + at + ": SAVE " + F(p.save) + ", LOAD " + F(p.load) + ", SHARE " + F(p.share) + ", ? " + F(p.help) + ", MAIN MENU " + F(p.menu));
                    Check(p.crumbX + p.crumbW + OzUeTopBar.Gap <= p.save.x, "E003 crumb (" + (p.crumbLong ? "MAIN MENU / UNIT EDITOR / PLAYGROUND" : "UNIT EDITOR / PLAYGROUND") + ") ends at " + (p.crumbX + p.crumbW).ToString("0.0", CultureInfo.InvariantCulture) + ", before SAVE at " + p.save.x.ToString("0.0", CultureInfo.InvariantCulture) + " at " + at);
                    Check(p.crumbLong == (cs.x >= 1700f && cs.y >= 800f), "E003 \"MAIN MENU /\" " + (p.crumbLong ? "shown" : "hidden") + " at " + at);
                    Check(p.save.y >= 0f && p.save.yMax <= p.h - 1f && p.save.x >= 0f, "E004-E009 buttons inside the bar at " + at);
                    var hr = OzUeHelpCard.Plan(p.help, cs, help.Height);
                    Check(hr.x >= 12f - 0.01f && hr.xMax <= cs.x - 12f + 0.01f && hr.yMax <= cs.y - 12f, "E190 help card " + F(hr) + " inside the canvas at " + at);
                    var sr = OzUeDialogs.Plan(cs, dialogs.ShareW, dialogs.ShareHeight);
                    Check(sr.width <= cs.x - 32f + 0.01f && sr.yMax <= cs.y - 16f && Mathf.Abs(sr.center.x - cs.x * 0.5f) < 0.6f, "the SHARE window " + F(sr) + " centred, inside at " + at);
                    var lr = OzUeDialogs.Plan(cs, dialogs.LoadW, dialogs.LoadHeight);
                    Check(lr.width <= cs.x - 32f + 0.01f && lr.yMax <= cs.y - 16f, "the LOAD window (six rows) " + F(lr) + " inside at " + at);
                    var ar = OzUeArmBar.Plan(cs, p.h, 620f);
                    Check(Mathf.Abs(ar.y - (p.h + 66f)) < 0.01f && ar.x >= 12f, "E183 arm bar " + F(ar) + " at top bar + 66 at " + at);
                    float toastY = p.h + 66f;
                    Check(Mathf.Abs(toastY - (compact ? 102f : 106f)) < 0.01f, "E180 toast top " + toastY + " (top bar + 66) at " + at);
                    if (i == 0)
                    {
                        Check(Mathf.Abs(p.logoH - 26f) < 0.01f && Mathf.Abs(p.crumbX - 102f) < 2f, "E002 wordmark at x 16, 26 u tall; E003 crumb at x " + p.crumbX.ToString("0.0", CultureInfo.InvariantCulture) + " (spec 102)");
                        Check(Mathf.Abs(p.help.y - 5.5f) < 0.01f && Mathf.Abs(p.menu.height - 28f) < 0.01f, "E004-E009 28 u tall at y 5.5 (spec)");
                        Check(Mathf.Abs(hr.xMax - p.help.xMax) < 0.01f && Mathf.Abs(hr.y - 41.5f) < 0.01f && Mathf.Abs(hr.width - 490f) < 0.01f, "E190 help card 490 u wide at y 41.5, right edge on \"?\" (spec 1259,41.5 with the prototype's wider link)");
                        Check(Mathf.Abs(sr.width - dialogs.ShareW) < 0.01f && dialogs.SharePlan.hasLine && dialogs.ShareHeight <= 200f, "the SHARE window " + dialogs.ShareW + " x " + dialogs.ShareHeight
                              + " u at 1920 x 1080: its title, one line and one row of buttons (200 u tall at most)");
                        log.AppendLine("INFO  P4 MAIN MENU is " + p.menu.width.ToString("0.0", CultureInfo.InvariantCulture) + " u wide (the prototype's ALL PROTOTYPES link was 145.0): SAVE / LOAD / SHARE / ? sit that much further right than the prototype's 1489.4 / 1558.2 / 1628.1 / 1721.1");
                    }
                }
            }
            catch (Exception e) { bad++; log.AppendLine("FAIL  P4 SelfCheck threw " + e.GetType().Name + ": " + e.Message); }
            finally
            {
                if (tmp) { if (Application.isPlaying) Destroy(tmp); else DestroyImmediate(tmp); }
                OzUeShell.DisposeMeasure();
            }
            log.AppendLine("P4 SelfCheck: " + ok + " PASS, " + bad + " FAIL");
            return bad;
        }

        static string F(Rect r) => r.x.ToString("0.0", CultureInfo.InvariantCulture) + "," + r.y.ToString("0.0", CultureInfo.InvariantCulture) + " " + r.width.ToString("0.0", CultureInfo.InvariantCulture) + "x" + r.height.ToString("0.0", CultureInfo.InvariantCulture);
    }

    /// <summary>Module P4's small UI helpers on the game's kit (no new look): measured buttons, the kit keycap, edged plates, textures.</summary>
    internal static class OzUeShell
    {
        static GameObject measGo; static OzText meas;

        /// <summary>Width of a text at a size and tracking, measured with the game's font (TMP), in canvas units.</summary>
        internal static float TextW(string text, float size, float tracking)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            if (meas == null || !measGo)
            {
                measGo = new GameObject("OzUe_Measure", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                meas = OzText.Make(measGo.transform, "M", "", size, Color.white, tracking, OzStyle.Plain);
            }
            meas.FontSize = size; meas.Tracking = tracking;
            return meas.WidthAt(text);
        }

        internal static void DisposeMeasure()
        {
            if (measGo) { if (Application.isPlaying) UnityEngine.Object.Destroy(measGo); else UnityEngine.Object.DestroyImmediate(measGo); }
            measGo = null; meas = null;
        }

        /// <summary>A kit button (HudButtonKind.Small, 12 u caps 0.12, INK_2) as wide as its text + padding, at (x, y) in its parent.</summary>
        internal static HudButton Button(Transform parent, string name, string text, float x, float y, float h, HudButtonStyle style, string tip, Action onClick,
                                         int priority = 0, float padX = 8f, float minW = 0f)
        {
            float w = Mathf.Max(minW, Mathf.Ceil(TextW(text, 12f, 0.12f) + 2f * padX + 2f));
            var b = HudButton.Make(parent, name, text, new Vector2(w, h), HudButtonKind.Small, 12f, 0.12f, style);
            b.Rt.Place(new OzRect(x, y, w, h));
            b.Tooltip = tip; b.Priority = priority;
            if (style == HudButtonStyle.Normal) b.LabelColor = OzUi.Ink2;
            b.Refresh();
            if (onClick != null) b.OnClick = _ => { try { onClick(); } catch (Exception e) { Debug.LogException(e); } };
            return b;
        }

        /// <summary>Guide 3.3: button text INK_2, INK while hovered (the kit's states keep the plate).</summary>
        internal static void HoverInk(HudButton b)
        {
            if (b == null || b.Style != HudButtonStyle.Normal || b.Selected) return;
            Color want = b.Hovered && b.Interactable ? (Color)OzUi.Ink : OzUi.Ink2;
            if (b.LabelColor != want) { b.LabelColor = want; b.Refresh(); }
        }

        /// <summary>The kit keycap (Panels/keycap), 20 u tall, a 12 u INK key name, at least square.</summary>
        internal static RectTransform Keycap(Transform parent, string name, string key, float x, float y, float h = 20f, float size = 12f)
        {
            float w = Mathf.Max(h, Mathf.Ceil(TextW(key, size, 0f) + 10f));
            var root = Hud.Node(name, parent); root.Place(new OzRect(x, y, w, h));
            var bg = Hud.Kit(root, "Bg", "Panels/keycap", new OzRect(0, 0, w, h)); bg.rectTransform.Fill();
            Hud.Label(root, "Key", key, size, OzUi.Ink, 0f, OzStyle.Plain).FillParent(OzH.C, 0, 1, 0, 0);
            return root;
        }

        /// <summary>A small plate with a 1 u coloured edge (the toast, the arm bar): the kit's panel_sm silhouette twice, hard shadow.</summary>
        internal static void EdgePlate(RectTransform rt, Color edge, Color fill, bool shadow)
        {
            if (shadow && OzKit.Available) Hud.Shadow(rt, "Panels/panel_sm_mask");
            var e = Hud.Kit(rt, "Edge", "Panels/panel_sm_mask", new OzRect(0, 0, 10, 10), edge); e.rectTransform.Fill();
            var f = Hud.Kit(rt, "Fill", "Panels/panel_sm_mask", new OzRect(0, 0, 10, 10), fill); f.rectTransform.Fill(1, 1, 1, 1);
        }

        /// <summary>A 1 x 32 vertical gradient (sRGB), top colour at the top.</summary>
        internal static Texture2D VGradient(Color32 top, Color32 low)
        {
            var t = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "OzUe_TopBarFill", hideFlags = HideFlags.DontSave };
            var px = new Color32[32];
            for (int i = 0; i < 32; i++) px[i] = Color32.Lerp(low, top, i / 31f);                                // row 0 is the bottom
            t.SetPixels32(px); t.Apply(false, true); return t;
        }

        /// <summary>A soft square glow: opaque inside, fading over `margin` (a share of the side) to the edge.</summary>
        internal static Texture2D SoftSquare(int n, float margin)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "OzUe_SoftSquare", hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n]; float m = Mathf.Max(1f, margin * n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(m - (x + 0.5f), (x + 0.5f) - (n - m))), dy = Mathf.Max(0f, Mathf.Max(m - (y + 0.5f), (y + 0.5f) - (n - m)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / m;
                    float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * a * 255f));
                }
            t.SetPixels32(px); t.Apply(false, true); return t;
        }
    }
}
