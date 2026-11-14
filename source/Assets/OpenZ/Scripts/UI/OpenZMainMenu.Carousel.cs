// OpenZ (GPL-3.0) - version 1.3, item 30: the battlefield screen's level carousel (more levels than fit at today's card size).
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    // ============================================================================================================================
    //
    // Off (one to three levels, or all of them fit at today's size: 32:9) nothing of this file builds anything: the cards stand in
    // the screen's root as in 1.2. On (four or more levels, every other canvas): the numbers are OzLevelCarousel's (UI/OzLevelCarousel.cs).
    //  * The cards are built into a strip (CardStrip, canvas-sized, so every card keeps its 1.2 coordinates and TickCards is
    //    unchanged) inside a window (CardWindow: today's three places plus the glow's room, a RectMask2D with a soft edge). The strip
    //    slides by one card pitch per step: OzLevelCarousel.Ease over SlideDur, unscaled time (OzMotion), a cut with reduced motion.
    //  * A card outside the window is not drawn (the mask culls it) and does not take the pointer: its hot area waits on another view.
    //  * "<" and ">" (CarouselPrev / CarouselNext): the menu's large button (hover, press, the disabled look with its hatch at the
    //    ends), a click steps the row by one card with ui_click; at an end ui_disabled. The selection stays on a card in view (it
    //    moves to the nearest one when the row leaves it, silently: the step's own sound answers).
    //  * The keys LEFT / RIGHT (and the gamepad's D-pad, which taps them) and a click on a card move the selection as in 1.2
    //    (FocusCard): the row follows so the selected card is in view (CarouselReveal).
    //  * The wheel over the row (or an arrow) steps it too, once per WheelGap, with ui_slider_tick (ui_disabled at an end); not while
    //    the HIGH SCORES popup is open (its own wheel).
    //  * One short bar per level under the row (CarouselPage0..): amber while that card is in view, following the slide.
    // Hooks in OpenZMainMenu.cs: BuildSelect -> BuildCarousel / BuildCarouselControls, FocusCard -> CarouselReveal, TickCards ->
    // TickCarousel, Poll -> CarouselWheel.
    // ============================================================================================================================
    public partial class OpenZMainMenu
    {
        OzLevelCarousel.Slot[] carSlots; RectTransform carWindowRt, carStrip; OzRect carWin;
        HudButton carPrev, carNext; Hot carPrevHot, carNextHot; Hot[] carCardHots; Image[] carPage;
        bool carOn; int carVis, carFirst, carMax; float carScroll, carFrom, carTo, carAt = -10f, carWheelAt = -10f;
        static readonly Color CarPageOn = OzUi.A(OzUi.Amber, 0.9f), CarPageOff = OzUi.A(OzUi.Ink, 0.22f);

        /// <summary>Called by BuildSelect before the cards: the parent for the cards (the screen's root while the carousel is off).</summary>
        RectTransform BuildCarousel(RectTransform p, OzLevelCarousel.Slot[] slots)
        {
            int n = slots.Length;
            carSlots = slots; carOn = OzLevelCarousel.On(n, CW);
            carWindowRt = carStrip = null; carPrev = carNext = null; carPrevHot = carNextHot = null; carCardHots = null; carPage = null;
            if (!carOn) return p;
            carVis = OzLevelCarousel.Visible(n, CW); carMax = OzLevelCarousel.MaxFirst(n, carVis);
            carFirst = OzLevelCarousel.FirstFor(Mathf.Clamp(carFirst, 0, carMax), focusCard, carVis, n);    // a rebuild keeps the row where it was; the selected card in view, at once (no slide)
            carScroll = carFrom = carTo = OzLevelCarousel.ScrollAt(slots, carFirst); carAt = -10f;
            carWin = OzLevelCarousel.Window(slots, carVis);
            carWindowRt = Hud.Node("CardWindow", p).Place(carWin);
            var mask = carWindowRt.gameObject.AddComponent<RectMask2D>(); mask.softness = new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(OzLevelCarousel.Soft * Screen.height / OzLayout.RefH)), 0);   // a soft edge, as the CHANGES view's (softness is in screen px: scaled to the canvas)
            carStrip = Hud.Node("CardStrip", carWindowRt).Place(new OzRect(-carWin.x - carScroll, -carWin.y, CW, OzLayout.RefH));
            return carStrip;
        }

        /// <summary>Called by BuildSelect after the cards (before the deploy bar and HIGH SCORES): the arrows and the page bars.</summary>
        void BuildCarouselControls(RectTransform p)
        {
            if (!carOn) return;
            int n = carSlots.Length; carCardHots = new Hot[n];
            for (int i = 0; i < n; i++) { var rt = cards[i].rt; carCardHots[i] = hots.Find(h => h.rt == rt); }
            carPrev = CarouselArrow(p, "CarouselPrev", "◀", -1, out carPrevHot);
            carNext = CarouselArrow(p, "CarouselNext", "▶", 1, out carNextHot);
            carPage = new Image[n];
            for (int i = 0; i < n; i++) { carPage[i] = Hud.Img(p, "CarouselPage" + i, CarPageOff); carPage[i].rectTransform.Place(OzLevelCarousel.PageBar(carSlots, carVis, n, i)); }
            TickCarousel(0f);
        }

        HudButton CarouselArrow(RectTransform p, string name, string glyph, int dir, out Hot hot)
        {
            var r = OzLevelCarousel.Arrow(carSlots, carVis, dir);
            var b = HudButton.Make(p, name, glyph, new Vector2(r.w, r.h), HudButtonKind.Large, 14, 0f, HudButtonStyle.Normal, true);
            b.Rt.Place(r);
            AddHot(b.Rt, View.Select, () => CarouselStep(dir, "ui_click"), null, null, null);   // no button in the hot: TickCarousel draws hover / press / disabled itself, so a click on a dimmed arrow still answers (ui_disabled)
            hot = hots[hots.Count - 1];
            return b;
        }

        /// <summary>One card left (dir -1) or right (+1): `sfx` when the row moves, ui_disabled at an end.</summary>
        void CarouselStep(int dir, string sfx)
        {
            if (!carOn || carSlots == null) return;
            int to = Mathf.Clamp(carFirst + dir, 0, carMax);
            if (to == carFirst) { Sfx("ui_disabled"); return; }
            Sfx(sfx);
            CarouselScrollTo(to);
            int f = Mathf.Clamp(focusCard, carFirst, carFirst + carVis - 1);
            if (f != focusCard) FocusCard(f, false, true);                                                  // the selection stays on a card in view (deploy bar, flyby and commander follow as for a key)
        }

        /// <summary>FocusCard's hook: the row follows the selection so the selected card is in view.</summary>
        void CarouselReveal(int i)
        {
            if (!carOn || carSlots == null) return;
            int to = OzLevelCarousel.FirstFor(carFirst, i, carVis, carSlots.Length);
            if (to != carFirst) CarouselScrollTo(to);
        }

        void CarouselScrollTo(int first)
        {
            carFirst = Mathf.Clamp(first, 0, carMax);
            carFrom = carScroll; carTo = OzLevelCarousel.ScrollAt(carSlots, carFirst); carAt = OzMotion.Now;   // from where the row is now: a step during a slide goes on smoothly
        }

        /// <summary>Poll's hook: the wheel over the row or an arrow steps the row (not while the HIGH SCORES popup has the wheel).</summary>
        void CarouselWheel(float wheel, Vector2 mp)
        {
            if (!carOn || !carWindowRt || hsOpen) return;
            bool over = RectTransformUtility.RectangleContainsScreenPoint(carWindowRt, mp, null)
                        || (carPrev != null && RectTransformUtility.RectangleContainsScreenPoint(carPrev.Rt, mp, null))
                        || (carNext != null && RectTransformUtility.RectangleContainsScreenPoint(carNext.Rt, mp, null));
            if (!over || OzMotion.Now - carWheelAt < OzLevelCarousel.WheelGap) return;
            carWheelAt = OzMotion.Now;
            CarouselStep(wheel < 0f ? 1 : -1, "ui_slider_tick");
        }

        /// <summary>TickCards' hook, every frame on this screen: the slide, which cards take the pointer, the arrows' look, the page bars.</summary>
        void TickCarousel(float dt)
        {
            if (!carOn || !carStrip) return;
            carScroll = OzLevelCarousel.Ease(carFrom, carTo, OzMotion.T(carAt, OzMotion.Dur(OzLevelCarousel.SlideDur)));
            var ap = new Vector2(-carWin.x - carScroll, carWin.y);
            if (carStrip.anchoredPosition != ap) carStrip.anchoredPosition = ap;
            for (int i = 0; i < carSlots.Length; i++)
            {
                bool drawn = OzLevelCarousel.InWindow(carSlots, carVis, i, carScroll);
                var h = carCardHots != null ? carCardHots[i] : null;
                if (h != null) h.view = drawn ? View.Select : View.Loading;                                    // outside the window: its hot area waits on a view the pointer is never polled in
                if (!drawn && hoverCard == i) hoverCard = -1;
                if (carPage != null && carPage[i]) { var c = Color.Lerp(CarPageOff, CarPageOn, OzLevelCarousel.InView(carSlots, carVis, i, carScroll)); if (carPage[i].color != c) carPage[i].color = c; }
            }
            CarouselArrowLook(carPrev, carPrevHot, carFirst > 0);
            CarouselArrowLook(carNext, carNextHot, carFirst < carMax);
        }

        void CarouselArrowLook(HudButton b, Hot h, bool on)
        {
            if (b == null || h == null) return;
            h.sfx = on ? null : "";                                                                          // null: Poll's hover sound (the click's sound is CarouselStep's); "": a dimmed arrow is silent under the pointer
            bool hov = hotOver == h, prs = on && hov && hotDown == h;
            if (b.Interactable == on && b.Hovered == hov && b.Pressed == prs) return;
            b.Interactable = on; b.Hovered = hov; b.Pressed = prs; b.Refresh();
        }

        // ---------------------------------------------------------------- checks (Play only)
        public bool ProofCarouselOn => carOn;
        public int ProofCarouselFirst => carFirst;
        public float ProofCarouselScroll => carScroll;
        public RectTransform ProofCarouselArrow(int dir) => dir < 0 ? (carPrev != null ? carPrev.Rt : null) : (carNext != null ? carNext.Rt : null);
        /// <summary>Card i is drawn and takes the pointer (always true while the carousel is off).</summary>
        public bool ProofCardShown(int i) => !carOn || carCardHots == null || (i >= 0 && i < carCardHots.Length && carCardHots[i] != null && carCardHots[i].view == View.Select);
    }
}
