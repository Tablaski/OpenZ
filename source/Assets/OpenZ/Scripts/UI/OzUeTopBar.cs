using System;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUnitEditor.cs): the top bar, DESIGN_SPEC E001-E009, prototype C round 2d.
    /// E001 the bar: full width, 40 u (36 compact), #1B201D -> #101311, a 1 u GM600 line at the bottom; the field starts under it; H
    ///      never hides it. E002 the OPENZ wordmark (the main menu's own, Wordmarks/wm_openz) at x 16, 26 u tall (bar - 14). E003 the
    ///      crumb "MAIN MENU / UNIT EDITOR / PLAYGROUND" at x 102 (12 u caps, 0.16, INK, the slashes MUTED with 4 u each side;
    ///      "MAIN MENU /" hides under 1700 u or on a short canvas). RESET STATS (it asks first: UI/OzUeLeave.cs) 12 u left of E004 SAVE,
    ///      E005 LOAD, E006 SHARE (primary), 6 u apart, E007 "?"
    ///      (28 x 28, 14 u after the group's 6 u margin), E009 MAIN MENU (1 u GM500 outline, no fill, INK_2; hover AMBER edge, INK) at
    ///      CW - 12 - w. The prototype's "C DOCK + INSPECTOR" chip and ALL PROTOTYPES link are not shipped (MAIN MENU takes the link's
    ///      place). Every number is measured on the canvas the editor has now (OzUnitEditor.CanvasSize), never on 1920: the plan is
    ///      UI/OzUePlan.cs TopBar.
    /// </summary>
    internal sealed class OzUeTopBar
    {
        internal const float Height = 40f, HeightCompact = 36f, LogoX = 16f, LogoGap = 14f, Right = 12f, GroupGap = 6f, GroupMargin = 6f, Gap = 14f, BtnH = 28f;
        const float LogoAspect = 517f / 187f;                                                                 // Wordmarks/wm_openz, size_u (its own transparent margin included, as the prototype's copy)
        internal const string Crumb = "UNIT EDITOR <space=4><color=#8D9286>/</color><space=4> PLAYGROUND";
        internal const string CrumbLong = "MAIN MENU <space=4><color=#8D9286>/</color><space=4> " + Crumb;
        // 1.2 item 109: short tips (the windows they open say the rest)
        internal const string SaveTip = "SAVE|Saves your Custom stats and the units on the field. The levels play them when you pick Custom on the level screen|";
        internal const string ResetTip = "RESET STATS|Every Custom value back to OpenZ's own numbers: the levels play OpenZ again. It asks first|";
        internal const string LoadTip = "LOAD|Load a stats file: yours, or one a friend sent you|";
        internal const string ShareTip = "SHARE|Saves your file and shows how to send it to friends|";
        internal const string HelpTip = "KEYS AND MOUSE||";
        internal const string MenuTip = "MAIN MENU|Leave the Unit editor. It asks first|ESC";

        /// <summary>Where everything of the bar goes on a canvas (u, top-left origin). Pure: also run by the SelfCheck at every size.</summary>
        internal struct Plan
        {
            public float cw, h, logoW, logoH, logoY, crumbX, crumbW, btnY;
            public bool crumbLong;
            public Rect reset, save, load, share, help, menu;
        }

        internal static float MenuWidth => Mathf.Ceil(OzUeShell.TextW("MAIN MENU", 12f, 0.12f) + 2f * 10f + 2f);
        internal static float ButtonWidth(string t) => Mathf.Ceil(OzUeShell.TextW(t, 12f, 0.12f) + 2f * 8f + 2f);

        /// <summary>The bar's plan: UI/OzUePlan.cs TopBar (the same rule as before, measured with the game's font, RESET STATS left of
        /// SAVE; the off-engine layout model runs the same code).</summary>
        internal static Plan Layout(Vector2 canvas, bool compact)
        {
            var q = OzUePlan.TopBar(canvas.x, canvas.y, compact, Crumb, CrumbLong, OzUeShell.TextW);
            return new Plan
            {
                cw = q.cw, h = q.h, logoW = q.logoW, logoH = q.logoH, logoY = q.logoY, crumbX = q.crumbX, crumbW = q.crumbW, btnY = q.btnY, crumbLong = q.crumbLong,
                reset = R(q.reset), save = R(q.save), load = R(q.load), share = R(q.share), help = R(q.help), menu = R(q.menu),
            };
        }

        static Rect R(UeR r) => new Rect(r.x, r.y, r.w, r.h);

        readonly RectTransform root;
        readonly RawImage fill; readonly Image line, logo;
        readonly OzText crumb;
        internal readonly HudButton save, load, share, help, menu, reset;
        readonly Image[] menuEdge;
        Plan plan; bool menuHover = true;
        internal float H => plan.h;
        internal RectTransform Root => root;
        /// <summary>1.5 kit fps_chip_place_r3: the bar's empty stretch, from the crumb's end + Gap to RESET STATS - Gap (canvas units, top-left
        /// origin, the bar's height without its bottom line): the FPS chip's place in the editor (View/OzQuality.cs OzFpsChip.EditorSpot).</summary>
        internal Rect FreeStretch => FreeOf(plan);
        /// <summary>FreeStretch of a plan (pure: the chip's plan check runs it at every size, UI/OzFpsChipProof.cs).</summary>
        internal static Rect FreeOf(Plan p) { float x0 = p.crumbX + p.crumbW + Gap, x1 = p.reset.x - Gap; return new Rect(x0, 0f, Mathf.Max(0f, x1 - x0), Mathf.Max(0f, p.h - 1f)); }

        internal OzUeTopBar(RectTransform parent, OpenZHud hud, Action onSave, Action onLoad, Action onShare, Action onHelp, Action onMenu, Action onReset)
        {
            root = Hud.Node("OzUe_TopBar", parent);
            fill = Hud.Node("Fill", root).gameObject.AddComponent<RawImage>(); fill.raycastTarget = false;
            fill.texture = OzUeShell.VGradient(OzUeUi.TopBarTop, OzUeUi.TopBarLow); fill.rectTransform.Fill();
            line = Hud.Img(root, "Line", OzUi.GM600);
            line.rectTransform.anchorMin = Vector2.zero; line.rectTransform.anchorMax = new Vector2(1f, 0f); line.rectTransform.pivot = new Vector2(0.5f, 0f);
            line.rectTransform.anchoredPosition = Vector2.zero; line.rectTransform.sizeDelta = new Vector2(0f, OzPx.Line(1f));
            logo = Hud.Kit(root, "Wordmark", "Wordmarks/wm_openz_vk_menu", new OzRect(LogoX, 7, 100, 26), null, Image.Type.Simple);
            logo.preserveAspect = true; logo.enabled = logo.sprite != null;
            if (!logo.sprite) Hud.Label(logo.transform, "ForkWordmark", "OpenZ-VK", 14, OzForkOptions.Brand, 0).At(0, 4, OzH.L, false, 100);
            crumb = Hud.Label(root, "Crumb", Crumb, 12, OzUi.Ink, 0.16f);
            reset = OzUeShell.Button(root, "ResetStats", OzUePlan.ResetLabel, 0, 0, BtnH, HudButtonStyle.Normal, ResetTip, onReset);   // RESET STATS: it asks first (UI/OzUeLeave.cs)
            save = OzUeShell.Button(root, "Save", "SAVE", 0, 0, BtnH, HudButtonStyle.Normal, SaveTip, onSave);
            load = OzUeShell.Button(root, "Load", "LOAD", 0, 0, BtnH, HudButtonStyle.Normal, LoadTip, onLoad);
            share = OzUeShell.Button(root, "Share", "SHARE", 0, 0, BtnH, HudButtonStyle.Primary, ShareTip, onShare);
            help = OzUeShell.Button(root, "Help", "?", 0, 0, BtnH, HudButtonStyle.Normal, HelpTip, onHelp, 0, 0f, BtnH);
            help.Label.FontSize = 13;
            menu = OzUeShell.Button(root, "MainMenu", "MAIN MENU", 0, 0, BtnH, HudButtonStyle.Normal, MenuTip, onMenu, 0, 10f);
            menu.Bg.enabled = false;                                                                          // E009: an outline, no fill (the prototype's link look)
            if (menu.Hatch) menu.Hatch.enabled = false;
            menuEdge = Hud.Border(menu.Rt, OzUi.GM500, OzPx.Line(1f));
            if (hud) hud.AddBlocker(root);
        }

        internal void Apply(Vector2 canvas, bool compact)
        {
            plan = Layout(canvas, compact);
            root.Place(new OzRect(0, 0, canvas.x, plan.h));
            logo.rectTransform.Place(new OzRect(LogoX, plan.logoY, plan.logoW, plan.logoH));
            crumb.Set(plan.crumbLong ? CrumbLong : Crumb);
            crumb.At(plan.crumbX, (plan.h - 1f) * 0.5f, OzH.L, true, Mathf.Max(64f, plan.reset.x - Gap - plan.crumbX));
            Put(reset, plan.reset); Put(save, plan.save); Put(load, plan.load); Put(share, plan.share); Put(help, plan.help); Put(menu, plan.menu);
            OzUeInsets.Set(UeSide.Top, "topbar", plan.h);
        }

        static void Put(HudButton b, Rect r) { b.Rt.Place(new OzRect(r.x, r.y, r.width, r.height)); }

        /// <summary>Per frame: the hover looks the kit buttons do not have (INK label on hover; MAIN MENU's amber outline).</summary>
        internal void Tick()
        {
            OzUeShell.HoverInk(reset); OzUeShell.HoverInk(save); OzUeShell.HoverInk(load); OzUeShell.HoverInk(help); OzUeShell.HoverInk(menu);
            bool h = menu.Hovered && menu.Interactable;
            if (h != menuHover)
            {
                menuHover = h;
                foreach (var e in menuEdge) e.color = h ? (Color)OzUi.Amber : OzUi.GM500;
            }
        }

        /// <summary>The "?" button's rect on the canvas (the help card opens under it).</summary>
        internal Rect HelpRect => plan.help;

        /// <summary>The editor closed: the bar's own texture goes with it.</summary>
        internal void Dispose() { if (fill && fill.texture) UnityEngine.Object.Destroy(fill.texture); }
    }
}
