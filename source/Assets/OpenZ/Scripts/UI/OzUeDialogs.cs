using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// editor's LOAD and SHARE windows, game-style (UI/OzUeWindow.cs, their plan UI/OzUeWinPlan.cs) over a 72 % #050706 dim. SAVE has no
    /// window: it saves at once with the toast "SAVED" and the file's name small beside it (SavedToast).
    ///  LOAD A FILE: the .json files of the folder newest first (rows 34 u, six shown, the wheel scrolls more; the file's name 13 u INK, its
    ///   day and time 12 u MUTED on the right; a file that is not a stats file says so in ALERT and cannot be loaded), the picked row
    ///   highlighted (an amber bar and wash; a click picks, a double click loads), the toggle "Also load its units" (off at every open; only
    ///   for a file that saved a field: the field is cleared and built again with the file's units, flags, obstacles and crates), OPEN
    ///   FOLDER (put a friend's file in it) | LOAD (the main button, ENTER). The close box, Esc or a click on the dim closes it.
    ///  SHARE YOUR STATS (SHARE in the top bar saves first): "Your file is ready. Send it to friends yourself; OpenZ sends nothing."
    ///   DISCORD (the game's Discord link, key "discord" of Resources/OpenZ/Text/openz_links.json, through OzLinks.Open as the main menu's
    ///   OPENZ DISCORD), COPY FILE PATH (the path as OzUeFiles.Shown writes it: the user's folder as %USERPROFILE%) | OPEN FOLDER (the main
    ///   button, ENTER). Each keeps the window open; the close box, Esc or a click on the dim closes it.
    /// Nothing is sent: the only outside calls hand the folder to the system's file browser and the Discord link to the browser. While a
    /// window is open the HUD is modal (OzUeHooks.SetModal): only its buttons react (Priority 100), the field takes no input, the wheel
    /// does not zoom, of the keys only Esc (it closes the window first: OzUeKeys.EscDialog) and Enter (the main button) work. Sounds:
    /// ui_panel_open / ui_panel_close, the HUD's click on every button, row and the toggle (ui_disabled on one that cannot be used),
    /// ui_confirm when a file is loaded or the path copied, ui_error when that fails, the click for Enter.
    /// </summary>
    internal sealed class OzUeDialogs
    {
        enum Mode { None, Share, Load }
        internal const int Priority = OzUeWindow.Priority;
        internal const float RowH = OzUeWinPlan.RowH;
        internal const int VisibleRows = OzUeWinPlan.MaxRows;
        internal const string ShareTitle = "SHARE YOUR STATS";
        internal const string ShareLine = "Your file is ready. Send it to friends yourself; OpenZ sends nothing.";
        internal const string LoadTitle = "LOAD A FILE";
        internal const string DiscordLabel = "DISCORD", CopyLabel = "COPY FILE PATH", FolderLabel = "OPEN FOLDER", LoadLabel = "LOAD";
        internal const string WithUnitsLabel = "Also load its units";
        internal const string Empty = "No files yet. Press SAVE, or put a friend's file in the folder.";
        internal const string DiscordTip = "DISCORD|Opens the OpenZ channel of the Discord in your browser|";
        internal const string CopyTip = "COPY FILE PATH|Copies where your file is, to paste it in a chat|";
        internal const string ShareFolderTip = "OPEN FOLDER|Shows your file on this computer|ENTER";
        internal const string LoadFolderTip = "OPEN FOLDER|Put a friend's file in this folder to load it|";
        internal const string LoadTip = "LOAD|Your Custom stats become the picked file's|ENTER";
        internal const string UnitsTip = "ALSO LOAD ITS UNITS|Clears the field, then places the units, flags and obstacles saved in the file|";
        internal const string DiscordFrom = "unit_editor_share";                                                // OzLinks.Open's "from" (its log line, OzLinks.LastFrom)

        readonly object owner = new object();
        Mode mode; int openFrame = -1;
        readonly RectTransform dim;
        readonly OzUeWindow share, load;
        internal readonly HudButton discord, copy, folder, loadBtn, loadFolder;
        /// <summary>LOAD's toggle "Also load its units".</summary>
        internal readonly OzUeCheck withUnits;
        readonly CanvasGroup unitsFade;
        readonly Image listWell;
        sealed class Row { public HudButton b; public Image wash, bar, line; public OzText file, when; public int index = -1; }
        readonly List<Row> rows = new List<Row>();
        readonly OzText emptyText;
        List<UeFileInfo> files = new List<UeFileInfo>();
        int sel = -1, first; float lastClickAt = -10f; int lastClick = -1; bool units;
        readonly OzUeWinPlan.Win sharePlan, loadPlanMax;
        OzUeWinPlan.Win loadPlan;
        /// <summary>Checks: windows opened, files loaded from the list, folders opened, paths copied.</summary>
        internal static int ShareOpens, LoadOpens, PickerLoads, FolderOpens, Copies;
        /// <summary>The Play proof and the popup check: OPEN FOLDER is counted and logged but not handed to the file browser.</summary>
        internal static bool ProofNoFolder;
        internal bool Open => mode != Mode.None;
        internal bool ShareOpen => mode == Mode.Share;
        internal bool LoadOpen => mode == Mode.Load;
        internal RectTransform ShareRoot => share.Root;
        internal RectTransform LoadRoot => load.Root;
        internal int FileCount => files.Count;
        internal int Selected => sel;
        /// <summary>The picked file saved a field (the toggle can be used).</summary>
        internal bool CanUnits => sel >= 0 && sel < files.Count && files[sel].ok && files[sel].onField > 0;
        /// <summary>LOAD brings the file's units too (the toggle on, for a file that has them).</summary>
        internal bool WithUnits => units && CanUnits;
        internal OzUeWinPlan.Win SharePlan => sharePlan;
        internal OzUeWinPlan.Win LoadPlan => loadPlan;
        /// <summary>LOAD with its six rows: the tallest it gets.</summary>
        internal OzUeWinPlan.Win LoadPlanMax => loadPlanMax;
        internal float ShareW => sharePlan.w;
        internal float ShareHeight => sharePlan.h;
        internal float LoadW => loadPlanMax.w;
        internal float LoadHeight => loadPlanMax.h;

        static OzUeWinPlan.Win PlanLoad(int n) => OzUeWinPlan.Plan(LoadTitle, null, Mathf.Max(1, n), true, true, new[] { FolderLabel, LoadLabel }, new[] { null, OzUePlan.KeyEnter }, 1, 1, OzUeShell.TextW);

        internal OzUeDialogs(RectTransform layer, OpenZHud hud)
        {
            dim = Hud.Node("OzUe_DialogDim", layer); dim.Fill();
            var dimImg = Hud.Img(dim, "Dim", new Color(5f / 255f, 7f / 255f, 6f / 255f, 0.72f)); dimImg.rectTransform.Fill();
            if (hud) hud.AddBlocker(dim);

            // ---------------------------------------------------------------- SHARE YOUR STATS
            share = new OzUeWindow(dim, "OzUe_Share", ShareTitle, OzUi.Amber, true, () => Close(true));
            share.SetLine(ShareLine);
            discord = share.Button("OpenDiscord", DiscordLabel, HudButtonStyle.Normal, null, DiscordTip, OpenDiscord);
            copy = share.Button("CopyPath", CopyLabel, HudButtonStyle.Normal, null, CopyTip, CopyPath);
            folder = share.Button("OpenFolder", FolderLabel, HudButtonStyle.Primary, OzUePlan.KeyEnter, ShareFolderTip, OpenFolder);
            sharePlan = OzUeWinPlan.Plan(ShareTitle, ShareLine, 0, false, true, new[] { DiscordLabel, CopyLabel, FolderLabel }, new[] { null, null, OzUePlan.KeyEnter }, 2, 2, OzUeShell.TextW);
            share.Apply(sharePlan, new[] { discord, copy, folder });

            // ---------------------------------------------------------------- LOAD A FILE
            load = new OzUeWindow(dim, "OzUe_Load", LoadTitle, OzUi.Amber, true, () => Close(true));
            loadPlanMax = PlanLoad(VisibleRows);
            float lw = loadPlanMax.list.w;
            listWell = Hud.Img(load.Root, "List", OzUi.A(OzUi.GM950, 0.55f));
            Hud.Border(listWell.rectTransform, OzUi.GM600);
            for (int i = 0; i < VisibleRows; i++) rows.Add(NewRow(load.Root, i, lw));
            emptyText = Hud.Label(load.Root, "Empty", Empty, 13, OzUi.Muted, 0f, OzStyle.Plain);
            withUnits = new OzUeCheck(load.Root, "WithUnits", WithUnitsLabel, lw);
            withUnits.B.Priority = Priority; withUnits.B.Tooltip = UnitsTip;
            withUnits.B.OnClick = _ => ToggleUnits();
            unitsFade = withUnits.B.Rt.gameObject.AddComponent<CanvasGroup>();
            loadFolder = load.Button("OpenFolder", FolderLabel, HudButtonStyle.Normal, null, LoadFolderTip, OpenFolder);
            loadBtn = load.Button("Load", LoadLabel, HudButtonStyle.Primary, OzUePlan.KeyEnter, LoadTip, () => LoadSelected(WithUnits));
            LayLoad(1);

            share.Root.gameObject.SetActive(false); load.Root.gameObject.SetActive(false); dim.gameObject.SetActive(false);
        }

        /// <summary>A path, a file name or a file's own words shown exactly as they are: TextMeshPro reads a backslash followed by n, r,
        /// t, v, u ... as a control code and a pair of angle brackets as a tag, so a path once showed "...\staging" and then "1_2_..." on
        /// the next line (its "\v" eaten), and a user folder starting with n or t after a backslash would break the line. Every path and
        /// file name the Unit editor shows (the LOAD list, the toasts) goes through here.</summary>
        internal static void Literal(OzText t)
        {
            if (t != null && t.graphic is TMPro.TextMeshProUGUI tm) { tm.parseCtrlCharacters = false; tm.richText = false; }
        }

        Row NewRow(RectTransform p, int i, float w)
        {
            var r = new Row();
            r.b = HudButton.Make(p, "Row" + i, null, new Vector2(w, RowH), HudButtonKind.Small, 12f, 0.12f);
            r.b.Rt.Place(new OzRect(0, 0, w, RowH)); r.b.Bg.enabled = false; if (r.b.Hatch) r.b.Hatch.enabled = false; r.b.Priority = Priority;
            r.wash = Hud.Img(r.b.Rt, "Wash", Color.clear); r.wash.rectTransform.Fill();
            r.bar = Hud.Img(r.b.Rt, "Bar", OzUi.Amber); r.bar.rectTransform.Place(new OzRect(0, 0, 3, RowH)); r.bar.enabled = false;
            r.line = Hud.Img(r.b.Rt, "Line", new Color(67f / 255f, 75f / 255f, 69f / 255f, 0.22f));
            r.line.rectTransform.anchorMin = Vector2.zero; r.line.rectTransform.anchorMax = new Vector2(1f, 0f); r.line.rectTransform.pivot = new Vector2(0.5f, 0f);
            r.line.rectTransform.sizeDelta = new Vector2(0f, OzPx.Line(1f)); r.line.rectTransform.anchoredPosition = Vector2.zero;
            r.file = Hud.Label(r.b.Content, "File", "", 13, OzUi.Ink, 0f, OzStyle.Plain).At(14, RowH * 0.5f, OzH.L, true, w * 0.58f);
            r.when = Hud.Label(r.b.Content, "When", "", 12, OzUi.Muted, 0f, OzStyle.Plain).At(w - 12f, RowH * 0.5f, OzH.R, true, w * 0.4f - 26f);
            Literal(r.file); Literal(r.when);                                                               // a file's name and its own words as they are
            int idx = i;
            r.b.OnClick = _ => Click(idx);
            return r;
        }

        /// <summary>LOAD laid out for n rows (1 .. 6): the list as tall as the files need, the toggle and the row of buttons under it.</summary>
        void LayLoad(int n)
        {
            loadPlan = PlanLoad(n);
            load.Apply(loadPlan, new[] { loadFolder, loadBtn });
            var L = loadPlan.list;
            listWell.rectTransform.Place(new OzRect(L.x, L.y, L.w, L.h));
            for (int i = 0; i < rows.Count; i++) rows[i].b.Rt.Place(new OzRect(L.x, L.y + i * RowH, L.w, RowH));
            emptyText.At(L.x + 14f, L.y + L.h * 0.5f, OzH.L, true, L.w - 28f);
            var c = loadPlan.check;
            withUnits.B.Rt.Place(new OzRect(c.x, c.y, c.w, c.h));
        }

        // ------------------------------------------------------------------ open / close
        internal void OpenShare(Vector2 canvas)
        {
            Show(Mode.Share, canvas);
            ShareOpens++;
            Debug.Log("[OpenZ UnitEditor] SHARE: the window (nothing is sent), file " + OzUeFiles.FilePath);
        }

        internal void OpenLoad(Vector2 canvas)
        {
            files = OzUeFiles.List();
            first = 0; sel = files.FindIndex(f => f.ok); units = false;
            LayLoad(Mathf.Clamp(files.Count, 1, VisibleRows));
            Show(Mode.Load, canvas);
            FillRows();
            LoadOpens++;
            Debug.Log("[OpenZ UnitEditor] LOAD: " + files.Count + " file(s) in " + OzUeFiles.Folder);
        }

        void Show(Mode m, Vector2 canvas)
        {
            mode = m; openFrame = Time.frameCount;
            dim.gameObject.SetActive(true); dim.SetAsLastSibling();
            share.Root.gameObject.SetActive(m == Mode.Share); load.Root.gameObject.SetActive(m == Mode.Load);
            (m == Mode.Share ? share : load).Shown();
            Place(canvas);
            OzUeHooks.SetModal(owner, true);
            AudioDirector.UI("ui_panel_open");
        }

        internal void Close(bool sound)
        {
            if (mode == Mode.None) return;
            mode = Mode.None;
            share.Root.gameObject.SetActive(false); load.Root.gameObject.SetActive(false); dim.gameObject.SetActive(false);
            OzUeHooks.SetModal(owner, false);
            if (sound) AudioDirector.UI("ui_panel_close");
        }

        internal bool EscClose() { if (mode == Mode.None) return false; Close(true); return true; }

        /// <summary>Centred on the canvas, the plate never wider than CW - 32.</summary>
        internal static Rect Plan(Vector2 canvas, float w, float h) { w = Mathf.Min(w, canvas.x - 32f); return new Rect((canvas.x - w) * 0.5f, Mathf.Max(16f, (canvas.y - h) * 0.5f), w, h); }

        internal void Place(Vector2 canvas)
        {
            share.PlaceOn(canvas);
            load.PlaceOn(canvas);
        }

        // ------------------------------------------------------------------ actions
        /// <summary>The toast of every save: "SAVED" and the file's name small beside it (SAVE, SHARE, SAVE &amp; LEAVE).</summary>
        internal static void SavedToast() => OzUeToast.Show(OzUePlan.SavedToast, false, OzUeFiles.FileName);

        /// <summary>SAVE in the top bar: no window, the file and the saved Custom profile at once (View/OzUeFiles.cs), the toast and its sound.</summary>
        internal static void SaveNow()
        {
            if (OzUeFiles.Save(out _, out string error)) { SavedToast(); AudioDirector.UI("ui_confirm"); }
            else { OzUeToast.Show(error ?? "COULD NOT WRITE THE FILE.", true); AudioDirector.UI("ui_error"); }
        }

        /// <summary>COPY FILE PATH: the path as the windows write it (no account name) on the clipboard; the toast shows it small.</summary>
        void CopyPath()
        {
            string p = OzUeFiles.Shown(OzUeFiles.FilePath);
            try { GUIUtility.systemCopyBuffer = p; Copies++; OzUeToast.Show("PATH COPIED", false, p); AudioDirector.UI("ui_confirm"); }
            catch (Exception) { OzUeToast.Show("COULD NOT COPY. OPEN FOLDER SHOWS THE FILE", true); AudioDirector.UI("ui_error"); }
        }

        /// <summary>OPEN FOLDER (LOAD and SHARE): the folder in the system's file browser (the HUD plays the click); a check counts it only.</summary>
        void OpenFolder()
        {
            FolderOpens++;
            if (ProofNoFolder) { Debug.Log("[OpenZ UnitEditor] OPEN FOLDER (a check: counted, not handed to the file browser) " + OzUeFiles.Folder); return; }
            OzUeFiles.OpenFolder();
        }

        /// <summary>DISCORD: the game's own link helper opens the Discord link in the browser (the HUD plays the click).</summary>
        void OpenDiscord() { OzLinks.Open(OzLinks.Discord, DiscordFrom); }

        void ToggleUnits()
        {
            if (!CanUnits) return;
            units = !units;
            withUnits.Set(WithUnits);
            Debug.Log("[OpenZ UnitEditor] LOAD: " + WithUnitsLabel + " " + (WithUnits ? "on" : "off"));
        }

        void Click(int rowIndex)
        {
            var r = rows[rowIndex]; if (r.index < 0) return;
            bool dbl = lastClick == r.index && OzMotion.Now - lastClickAt < 0.35f;
            lastClick = r.index; lastClickAt = OzMotion.Now;
            sel = r.index; FillRows(false);
            if (dbl && files[sel].ok) LoadSelected(WithUnits);
        }

        internal void LoadSelected(bool withField)
        {
            if (sel < 0 || sel >= files.Count || !files[sel].ok) { AudioDirector.UI("ui_disabled"); return; }
            var f = files[sel];
            if (OzUeFiles.Load(f.path, withField && f.onField > 0, out string message))
            {
                PickerLoads++;
                Close(false);
                OzUeToast.Show(message);
                AudioDirector.UI("ui_confirm");
            }
            else { OzUeToast.Show(message, true); AudioDirector.UI("ui_error"); }
        }

        internal void Select(int fileIndex) { if (fileIndex >= 0 && fileIndex < files.Count) { sel = fileIndex; FillRows(); } }

        /// <summary>The visible rows from `first`; follow = scroll so the picked file is visible (not while the wheel scrolls).</summary>
        void FillRows(bool follow = true)
        {
            first = Mathf.Clamp(first, 0, Mathf.Max(0, files.Count - VisibleRows));
            if (follow && sel >= 0) { if (sel < first) first = sel; if (sel >= first + VisibleRows) first = sel - VisibleRows + 1; }
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i]; int k = first + i;
                bool has = k < files.Count;
                r.b.Rt.gameObject.SetActiveFast(has);
                r.index = has ? k : -1;
                if (!has) continue;
                var f = files[k];
                r.file.Set(f.file);
                r.file.color = f.ok ? (Color)OzUi.Ink : OzUi.Muted;
                r.when.Set(f.ok ? (f.saved == default ? "" : f.saved.ToString("yyyy-MM-dd  HH:mm", CultureInfo.InvariantCulture)) : (f.error ?? "").TrimEnd('.'));
                r.when.color = f.ok ? (Color)OzUi.Muted : OzUi.AlertText;
                r.bar.enabled = k == sel;
            }
            emptyText.SetActive(files.Count == 0);
            bool okSel = sel >= 0 && sel < files.Count && files[sel].ok;
            loadBtn.Interactable = okSel; loadBtn.Refresh();
            PaintUnits();
        }

        /// <summary>The toggle as it stands: on only for a file that saved a field; faded and refusing (ui_disabled) for one that did not.</summary>
        void PaintUnits()
        {
            bool can = CanUnits;
            if (withUnits.B.Interactable != can) withUnits.B.Interactable = can;
            unitsFade.alpha = can ? 1f : 0.45f;
            withUnits.Set(WithUnits);
        }

        /// <summary>Enter: the main button (LOAD; OPEN FOLDER in SHARE).</summary>
        void Enter()
        {
            if (mode == Mode.Load) LoadSelected(WithUnits);
            else if (mode == Mode.Share) OpenFolder();
        }

        /// <summary>Per frame: the fade, the hover inks, the rows' washes, the toggle, the wheel over the list, Enter (with the click), a
        /// press on the dim closes.</summary>
        internal void Tick()
        {
            if (mode == Mode.None) return;
            var win = mode == Mode.Share ? share : load;
            win.Tick();
            var m = Mouse.current;
            if (mode == Mode.Load)
            {
                foreach (var r in rows)
                {
                    if (r.index < 0) continue;
                    bool selRow = r.index == sel;
                    Color c = r.b.Pressed ? OzUi.A(OzUi.Amber, 0.10f) : selRow ? OzUi.A(OzUi.Amber, 0.08f) : r.b.Hovered ? new Color(1f, 1f, 1f, 0.045f) : Color.clear;
                    if (r.wash.color != c) r.wash.color = c;
                }
                PaintUnits();
                if (m != null && files.Count > VisibleRows && OpenZHud.Contains(load.Root, m.position.ReadValue()))
                {
                    float wheel = m.scroll.ReadValue().y;
                    if (Mathf.Abs(wheel) > 0.01f) { first = Mathf.Clamp(first - (int)Mathf.Sign(wheel), 0, files.Count - VisibleRows); FillRows(false); }
                }
            }
            if (Time.frameCount == openFrame) return;                                                       // the click that opened it
            var kb = Keyboard.current;
            if (kb != null && !OzUeHooks.Typing && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) { Enter(); AudioDirector.UI("ui_click"); return; }
            if (m != null && m.leftButton.wasPressedThisFrame && !OpenZHud.Contains(win.Root, m.position.ReadValue())) Close(true);   // a click on the dim closes
        }

        /// <summary>The buttons of both windows where their plans put them (the plan's rects, as built), the plans holding at 1920 x 1080.
        /// text = the rows in words.</summary>
        internal bool RowsAsPlanned(out string text)
        {
            bool ok = true; var sb = new System.Text.StringBuilder();
            void One(string name, OzUeWinPlan.Win d, HudButton[] bs)
            {
                ok &= OzUeWinPlan.WinOk(d, 1920f, 1080f, out string why);
                sb.Append(name).Append(' ').Append(OzUeWinPlan.Describe(d)).Append(why != null ? " (" + why + ")" : "").Append("; ");
                for (int i = 0; i < bs.Length && i < d.buttons.Length; i++)
                {
                    var r = d.buttons[i]; var a = bs[i].Rt;
                    ok &= Mathf.Abs(a.anchoredPosition.x - r.x) < 0.01f && Mathf.Abs(-a.anchoredPosition.y - r.y) < 0.01f && Mathf.Abs(a.sizeDelta.x - r.w) < 0.01f && Mathf.Abs(a.sizeDelta.y - r.h) < 0.01f;
                }
            }
            One(ShareTitle, sharePlan, new[] { discord, copy, folder });
            One(LoadTitle, loadPlan, new[] { loadFolder, loadBtn });
            text = sb.ToString().TrimEnd(' ', ';');
            return ok;
        }

        /// <summary>SelfCheck: every text of both windows at 11 u or more; both fit the shortest canvas (OzUeWinPlan.MaxH).</summary>
        internal int Audit(System.Text.StringBuilder log)
        {
            int bad = share.Audit(log, ShareTitle) + load.Audit(log, LoadTitle);
            if (sharePlan.h > OzUeWinPlan.MaxH || loadPlanMax.h > OzUeWinPlan.MaxH)
            { bad++; log?.AppendLine("FAIL  P4 a window is taller than the shortest canvas (537 u) less 32: SHARE " + sharePlan.h + ", LOAD with six rows " + loadPlanMax.h); }
            for (int n = 1; n <= VisibleRows; n++)
                if (Mathf.Abs(PlanLoad(n).w - loadPlanMax.w) > 0.01f) { bad++; log?.AppendLine("FAIL  P4 LOAD's width changes with its rows (" + n + " rows: " + PlanLoad(n).w + " u)"); break; }
            return bad;
        }

        internal void Dispose() { OzUeHooks.SetModal(owner, false); }
    }
}
