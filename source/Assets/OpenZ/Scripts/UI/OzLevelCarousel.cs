// OpenZ (GPL-3.0) - version 1.3, item 30: the battlefield screen's level cards as a carousel once a fourth level is registered.
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Where the level cards of the battlefield
    /// screen stand (OpenZMainMenu.BuildSelect) and how the row scrolls (OpenZMainMenu.Carousel.cs).
    ///  * One, two or three levels: the 1.2 row, its numbers moved here unchanged (<see cref="Row"/>), so today's three cards stand
    ///    exactly where they stood. No window, no arrows, no scroll.
    ///  * Four or more levels where they all fit at the size of today's three (only 32:9: the 1.2 card is then held by its height,
    ///    696 x 582 u): the 1.2 row of all of them, every card at today's size, the row centred (<see cref="AllFit"/>).
    ///  * Elsewhere a carousel: every card keeps the size, the row place, the gap and the side of today's row of three; three show at
    ///    once in today's three places (<see cref="Shown"/>); the others wait beside them, one gap apart, outside a window
    ///    (<see cref="Window"/>: the three places plus the 24 u glow, a RectMask2D with a soft edge) and are not drawn and not
    ///    clickable. A "&lt;" and a "&gt;" button stand in the side room at the row's two ends (<see cref="Arrow"/>), a short bar per
    ///    level under the row shows which are in view (<see cref="PageBar"/>). Moving the selection past the window's edge, an arrow,
    ///    or the wheel over the row slides the row by one card (<see cref="SlideDur"/>, eased, unscaled time); the selected card is
    ///    always one of the three in view (<see cref="FirstFor"/>).
    /// Pure numbers: the editor check (Editor/OzV13LevelGridCheck.cs) and the package's offline check use the same functions.
    /// </summary>
    public static class OzLevelCarousel
    {
        /// <summary>One card's place: top-left x, y, width and picture height (canvas units, 1080 u tall, top-left origin). In the
        /// carousel x is the place in the row at scroll 0 (card i: the first place + i x <see cref="Pitch"/>).</summary>
        public struct Slot
        {
            public float x, y, w, ih;
            /// <summary>The whole card: picture + meta bar.</summary>
            public float H => ih + OzMenuLayout.CardMeta;
        }

        /// <summary>Cards in view at once in the carousel: today's three.</summary>
        public const int Shown = 3;
        /// <summary>The slide by one card (s, unscaled time, OutCubic; the reduced-motion option makes it a cut, as every menu motion).
        /// The wheel steps at most once per <see cref="WheelGap"/> s.</summary>
        public const float SlideDur = 0.3f, WheelGap = 0.15f;
        /// <summary>The arrow buttons: size, and the room between an arrow and the card beside it (more than the 24 u glow; at the 96 u
        /// side of 16:9 and narrower the arrow stands centred in the side room).</summary>
        public const float ArrowW = 44f, ArrowH = 88f, ArrowGap = 26f;
        /// <summary>The window: the glow's room left and right of the three places, room above / below (lift, glow, the 24 u entrance
        /// rise), and the soft edge of the mask (u, inside the glow's room, so a card at rest is never faded).</summary>
        public const float WinPadX = OzMenuLayout.CardGlow, WinPadY = 48f;
        public const int Soft = 12;
        /// <summary>The page bars under the row: one per level, the progress bar's 3 u line, amber while that card is in view.</summary>
        public const float PageW = 16f, PageH = 3f, PageGap = 6f, PageDY = 14f;

        /// <summary>The cards' places for n levels on a canvas cw u wide: the 1.2 row, or the carousel row.</summary>
        public static Slot[] Place(int n, float cw)
        {
            if (!On(n, cw)) return Row(n, cw);
            var r = Row(Shown, cw); float gap = OzMenuLayout.CardGapN, x0 = r[0].x, w = r[0].w;
            var s = new Slot[n];
            for (int i = 0; i < n; i++) s[i] = new Slot { x = x0 + i * (w + gap), y = r[0].y, w = w, ih = r[0].ih };   // card i < 3: the very expression of Row (the same x)
            return s;
        }

        /// <summary>
        /// picture, the row centred in the free band; a row of three never reaches under the commander's strip.</summary>
        public static Slot[] Row(int n, float CW)
        {
            var s = new Slot[n];
            bool row3 = n > 2;
            float gap = row3 ? OzMenuLayout.CardGapN : OzMenuLayout.CardGap, gutter = row3 ? OzMenuLayout.CardSideN : OzMenuLayout.CardSide;
            float aspect = row3 ? OzMenuLayout.CardAspectN : OzMenuLayout.CardImgH / OzMenuLayout.CardImgW;      // picture height / width
            float maxImgH = row3 ? Mathf.Min(OzMenuLayout.CardMaxImgH, OzMenuLayout.CardRowBottomMax - OzMenuLayout.CardTop - OzMenuLayout.CardMeta) : OzMenuLayout.CardMaxImgH;
            float w = Mathf.Min((CW - 2 * gutter - gap * (n - 1)) / n, maxImgH / aspect);
            float ih = w * aspect, x0 = (CW - (n * w + (n - 1) * gap)) * 0.5f;
            float band = OzLayout.RefH - OzMenuLayout.DeployBarH - OzMenuLayout.CardBandPad - OzMenuLayout.CardBandTop;
            float top = row3 ? Mathf.Max(OzMenuLayout.CardTop, Mathf.Min(OzMenuLayout.CardBandTop + (band - (ih + OzMenuLayout.CardMeta)) * 0.5f, OzMenuLayout.CardRowBottomMax - (ih + OzMenuLayout.CardMeta))) : OzMenuLayout.CardTop;
            for (int i = 0; i < n; i++) s[i] = new Slot { x = x0 + i * (w + gap), y = top, w = w, ih = ih };
            return s;
        }

        /// <summary>All n cards fit in one row at the size of today's three (three or fewer: always).</summary>
        public static bool AllFit(int n, float cw) => n <= Shown || Row(n, cw)[0].w >= Row(Shown, cw)[0].w;
        /// <summary>The screen is a carousel: more levels than fit at today's size.</summary>
        public static bool On(int n, float cw) => !AllFit(n, cw);
        /// <summary>Cards in view: three in the carousel, else all.</summary>
        public static int Visible(int n, float cw) => On(n, cw) ? Shown : n;
        /// <summary>The step between two cards of the row (card width + the 1.2 gap).</summary>
        public static float Pitch(Slot[] s) => s[0].w + OzMenuLayout.CardGapN;
        /// <summary>The row's scroll (u) with card `first` in the first place.</summary>
        public static float ScrollAt(Slot[] s, int first) => first * Pitch(s);
        /// <summary>The last possible first card.</summary>
        public static int MaxFirst(int n, int visible) => Mathf.Max(0, n - visible);
        /// <summary>The first card in view so that `card` is in view: unchanged when it already is, else the least move.</summary>
        public static int FirstFor(int first, int card, int visible, int n)
        {
            if (card < first) first = card;
            else if (card > first + visible - 1) first = card - visible + 1;
            return Mathf.Clamp(first, 0, MaxFirst(n, visible));
        }
        /// <summary>The slide: from `from` to `to` (u), t01 = the share of <see cref="SlideDur"/> gone.</summary>
        public static float Ease(float from, float to, float t01) => from + (to - from) * OzEase.OutCubic(Mathf.Clamp01(t01));

        /// <summary>The window (canvas rect): the places of the cards in view, plus the glow's room left and right and
        /// <see cref="WinPadY"/> above and below.</summary>
        public static OzRect Window(Slot[] s, int visible)
        {
            float left = s[0].x - WinPadX, right = s[visible - 1].x + s[0].w + WinPadX;
            return new OzRect(left, s[0].y - WinPadY, right - left, s[0].H + 2f * WinPadY);
        }
        /// <summary>The "&lt;" (dir -1) or "&gt;" (+1) button: in the side room, <see cref="ArrowGap"/> off the card beside it, centred on
        /// the card's height.</summary>
        public static OzRect Arrow(Slot[] s, int visible, int dir)
        {
            float y = s[0].y + (s[0].H - ArrowH) * 0.5f;
            float x = dir < 0 ? s[0].x - ArrowGap - ArrowW : s[visible - 1].x + s[0].w + ArrowGap;
            return new OzRect(x, y, ArrowW, ArrowH);
        }
        /// <summary>Level i's page bar: the n bars centred under the window, <see cref="PageDY"/> under the cards.</summary>
        public static OzRect PageBar(Slot[] s, int visible, int n, int i)
        {
            float all = n * PageW + (n - 1) * PageGap, mid = (s[0].x + s[visible - 1].x + s[0].w) * 0.5f;
            return new OzRect(mid - all * 0.5f + i * (PageW + PageGap), s[0].y + s[0].H + PageDY, PageW, PageH);
        }
        /// <summary>The share (0..1) of card i's width inside the places in view, at scroll `scroll` (the page bar's light).</summary>
        public static float InView(Slot[] s, int visible, int i, float scroll)
        {
            float a = s[0].x, b = s[visible - 1].x + s[0].w, l = s[i].x - scroll, r = l + s[i].w;
            return Mathf.Clamp01((Mathf.Min(b, r) - Mathf.Max(a, l)) / s[i].w);
        }
        /// <summary>Card i is drawn (any part inside the window) at scroll `scroll`: only such a card takes the pointer.</summary>
        public static bool InWindow(Slot[] s, int visible, int i, float scroll)
        {
            var w = Window(s, visible); float l = s[i].x - scroll, r = l + s[i].w;
            return l < w.x + w.w && r > w.x;
        }
    }

    /// <summary>
    /// 1.3 item 30: the fourth card, level 7 "Pyro Technics" (item 12, the lava level), ready to register. NOT in OzLevelDef.All
    /// until item 12's level exists (its scene L07_Volcanic in the build, part B6, and its per-level switches, part B7): then
    /// tools/register_l07.py of the item 30 package inserts <see cref="Def"/> into OzLevelDef.All (UI/OzLevelPreview.cs) in its place
    /// <see cref="After"/>
    /// the menu order is L02, L07, L16, L17, and the battlefield screen becomes the carousel by itself. The card's art is item 12's: the still Resources/OpenZ/Loading/L07_loading_1 (or
    /// NO PREVIEW and the loading screen the plain name until they are made). The facts come from the map (L07_grid, item 12); the
    /// chips below are the fallback, counted from the Zod map by item 12 (check/L07_lava_stats.json).
    /// </summary>
    public static class OzLevelL07
    {
        public const string Id = "L07", Scene = "L07_Volcanic", Wordmark = "Wordmarks/mask_volcanic";
        public static readonly OzLevelDef Def = new OzLevelDef(Id, Scene, "07", "VOLCANO", "DAY · ASH HAZE", Wordmark,
            "CAPTURE THE FLAGS TO TAKE TERRITORY. BREAK INTO THE BLUE FORT TO WIN.", "Capture the flags. Break into the blue fort.",
            new[] { "10 ZONES", "8 FLAGS", "2 FORTS", "84×110 TILES" });

        /// <summary>Level 7 is one of the menu's levels (registered).</summary>
        public static bool Registered => System.Array.FindIndex(OzLevelDef.All, d => d.Id == Id) >= 0;
        /// <summary>The level that level 7 follows in the menu: the menu lists the levels by number (L02, L07, L16, L17).</summary>
        public const string After = "L02";

        /// <summary>The menu's level ids in menu order, with level 7 in its place (after <see cref="After"/>) while it is not registered
        /// yet: L02, L07, L16, L17 both before and after register_l07.py (the checks walk the levels in this order).</summary>
        public static string[] MenuOrder()
        {
            var ids = new System.Collections.Generic.List<string>();
            foreach (var d in OzLevelDef.All) ids.Add(d.Id);
            if (!Registered) ids.Insert(System.Array.FindIndex(OzLevelDef.All, d => d.Id == After) + 1, Id);
            return ids.ToArray();
        }

        /// <summary>The ids rise by level number (L02, L07, L16, L17): the menu's order rule.</summary>
        public static bool ByNumber(string[] ids)
        {
            for (int i = 1; i < ids.Length; i++)
                if (!int.TryParse(ids[i - 1].Substring(1), out int a) || !int.TryParse(ids[i].Substring(1), out int b) || a >= b) return false;
            return ids.Length > 0;
        }
    }
}
