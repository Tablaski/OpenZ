// 1.2 item 6, View/OzUnitEditor.cs: the hover card of a unit on the field (DESIGN_SPEC E170-E174): after 100 ms over a unit (never while
// dragging or placing, never with the pointer over a panel, never while a card, sheet or dialog is open over the field, which would
// stand over it: UI/OzUeHoverCard.Cover.cs), 22 u right of and 14 u above the mouse; it flips left before the stats box, under the mouse
// before the top bar, and keeps 8 u clear of the unit's own name label (a prototype flaw fixed). Picture, name in the team colour, "red
// vehicle", the HP bar and HP, five cells BUILD (first, amber) / HP / DMG / RANGE / DPS of the set in use with fixed widths, the foot line.
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    internal sealed partial class OzUeHoverCard
    {
        public const float W = 252f, Pad = 10f, Delay = 0.10f, DX = 22f, DY = 14f;
        static readonly UeStat[] Cells = { UeStat.Build, UeStat.Health, UeStat.Damage, UeStat.Range, UeStat.Dps };
        static readonly string[] Caps = { "BUILD", "HP", "DMG", "RANGE", "DPS" };
        static readonly float[] Widths = { 46f, 48f, 44f, 46f, 44f };
        RectTransform card; RawImage art; OzText name, sub, hp, foot; Image barTrack, barFill;
        readonly OzText[] cap = new OzText[5], val = new OzText[5];
        float h, footY; ZObject shown, over; float overSince, nextFill;
        public static int Shows;

        public void Build(RectTransform parent)
        {
            float inner = W - 2 * Pad;
            float y = Pad;
            card = OzUeGfx.Node(parent, "OzUeHoverCard", 0, 0, W, 160f);
            var body = Hud.Plate(card, "sm", 0f, null, null, false, false);
            if (OzKit.Available) body.color = new Color(0.37f, 0.38f, 0.375f, 1f);           // the sm plate darkened to GM950 95 %
            var pic = OzUeGfx.Node(card, "Pic", Pad, y, 40f, 40f);
            var a = OzUeGfx.Node(pic, "Art", 0, 0, 40f, 40f);
            art = a.gameObject.AddComponent<RawImage>(); art.raycastTarget = false;
            OzUeGfx.Frame(pic, OzUi.GM500);
            name = OzUeGfx.Text(card, "Name", "", 13, OzUi.Ink, 0.08f, Pad + 48f, y + 12f, OzH.L, inner - 48f, OzStyle.Underlay);
            sub = OzUeGfx.Text(card, "Sub", "", 12, OzUi.Muted, 0f, Pad + 48f, y + 29f, OzH.L, inner - 48f);
            y += 40f + 8f;
            barTrack = OzUeGfx.Rect(card, "BarTrack", new Color(1f, 1f, 1f, 0.10f), Pad, y, inner, 4f);
            barFill = OzUeGfx.Rect(card, "BarFill", OzUi.Go, Pad, y, inner, 4f);
            y += 4f + 3f;
            hp = OzUeGfx.Text(card, "Hp", "", 13, OzUi.Ink, 0f, Pad, y + 8f, OzH.L, inner);
            y += 17f + 8f;
            // E173: fixed widths (46 / 48 / 44 / 46 / 44 with 4 u gaps, scaled to the card's inner width) so RANGE and DPS never touch
            float sum = 0; foreach (var x in Widths) sum += x;
            float k = (inner - 4f * 4f) / sum, cx = Pad;
            for (int i = 0; i < 5; i++)
            {
                float cw = Widths[i] * k;
                cap[i] = OzUeGfx.Text(card, "Cap" + i, Caps[i], 11, i == 0 ? (Color)OzUi.AmberHi : (Color)OzUi.Muted, 0.08f, cx, y + 7f, OzH.L, cw);
                val[i] = OzUeGfx.Text(card, "Val" + i, "", 13, i == 0 ? (Color)OzUi.AmberHi : (Color)OzUi.Ink, 0f, cx, y + 23f, OzH.L, cw);
                cx += cw + 4f;
            }
            y += 32f + 6f;
            footY = y;                                                                            // E174: wraps to a second line when the set's name is long (as the prototype's)
            foot = Hud.Label(card, "Foot", "", 12, OzUi.Muted, 0f, OzStyle.Plain).Body(Pad, footY, inner);
            h = footY + 16.2f + Pad;
            card.sizeDelta = new Vector2(W, h);
            card.gameObject.SetActive(false);
        }

        static bool IsUnit(ZObject o)
        {
            if (o == null || o.Destroyed || o.KillAt >= 0) return false;
            var id = OzUePlayground.IdOf(o);
            var k = OzUePlayground.KindOf(id);
            return k == UeKind.Robot || k == UeKind.Vehicle || k == UeKind.Gun || k == UeKind.Hero || k == UeKind.Shuttle;
        }

        public void Tick(UeBoxLayout L)
        {
            if (card == null) return;
            var hud = OpenZHud.I;
            ZObject o = OzUePick.Hover;
            bool can = o != null && IsUnit(o) && !OzUePlace.Busy && !OzUeHooks.AnyModal && hud != null && !hud.PointerOverUi && !OzUnitEditor.PanelsHidden
                       && OzUeHoverCard.CoveredBy() == null;                                       // never behind an open card, sheet or dialog (UI/OzUeHoverCard.Cover.cs)
            if (!can) { if (card.gameObject.activeSelf) card.gameObject.SetActive(false); shown = null; over = null; return; }
            if (o != over) { over = o; overSince = OzMotion.Now; }
            if (OzMotion.Now - overSince < Delay) return;
            if (o != shown || OzMotion.Now >= nextFill) { Fill(o); nextFill = OzMotion.Now + 0.1f; }
            Place(o, L);
            if (!card.gameObject.activeSelf) { card.gameObject.SetActive(true); Shows++; }
        }

        void Fill(ZObject o)
        {
            string id = OzUePlayground.IdOf(o);
            bool first = o != shown;
            shown = o;
            var team = o.Owner;
            if (first) OzUeArt.Apply(art, id, team);
            string nm = o.IsHero ? OzHeroSquadView.NameOr(o, OzUePlayground.FullName(id)) : OzUePlayground.FullName(id);   // a hero: ALAN or BRAD
            name.Set(nm); name.color = OzUeUi.TeamText(team);
            sub.Set(OzUeUi.TeamWordSentence(team).ToLowerInvariant() + " " + OzUePlayground.ClassWord(id).ToLowerInvariant());
            float f = o.MaxHealth > 0 ? Mathf.Clamp01(o.Health / (float)o.MaxHealth) : 0f;
            barFill.rectTransform.sizeDelta = new Vector2((W - 2 * Pad) * f, 4f);
            barFill.color = f > 0.5f ? (Color)OzUi.Go : f > 0.25f ? (Color)OzUi.Amber : (Color)OzUi.Alert;
            hp.Set(OzText.Tab(Mathf.Max(0, o.Health) + " / " + o.MaxHealth + " HP"));
            var set = OzUeSets.RunningFor(id, out _);                                                     // the set this unit runs: the one in use, OpenZ for a unit that set does not list
            for (int i = 0; i < 5; i++)
            {
                var c = OzUeSets.Cell(id, Cells[i], set);
                string t = OzUeSets.Text(Cells[i], c);
                if (i == 0 && c.HasNumber) t += " " + OzUeGfx.Col("s", OzUi.Muted);
                val[i].Set(OzText.Tab(t));
                if (i > 0) val[i].color = c.HasNumber ? (Color)OzUi.Ink : c.kind == UeCellKind.NotApplicable || c.kind == UeCellKind.NoRoll ? (Color)OzUi.Dim : (Color)OzUi.Muted;
            }
            foot.Set(OzUeSets.CardFoot(id) + " Click: stats, Delete: remove");                           // "Custom stats: every LASER here runs them." in the set's colour (View/OzUeSets.Reset.cs)
            float nh = footY + Mathf.Max(16.2f, foot.preferredHeight) + Pad;
            if (Mathf.Abs(nh - h) > 0.5f) { h = nh; card.sizeDelta = new Vector2(W, h); }
        }

        /// <summary>22 u right / 14 u above the mouse; flipped left before the stats box, under the mouse before the top bar; never on the
        /// unit's name label (the zone above its screen rect, + 8 u).</summary>
        void Place(ZObject o, UeBoxLayout L)
        {
            var cs = OzUnitEditor.CanvasSize;
            var m = OzUeGfx.PointerCanvas;                                                        // origin bottom-left
            float right = cs.x - OzUeInsets.Get(UeSide.Right) - 8f;
            float topLimit = cs.y - (OzUeInsets.Get(UeSide.Top) > 1f ? OzUeInsets.Get(UeSide.Top) : 40f) - 8f;
            Rect label = LabelZone(o);
            Vector2[] tries =
            {
                new Vector2(m.x + DX, m.y + DY + h), new Vector2(m.x - DX - W, m.y + DY + h),
                new Vector2(m.x + DX, m.y - DY), new Vector2(m.x - DX - W, m.y - DY),
            };
            Vector2 best = tries[0]; bool found = false;
            foreach (var t in tries)
            {
                var r = new Rect(t.x, t.y - h, W, h);
                bool inside = r.xMin >= 8f && r.xMax <= right && r.yMax <= topLimit && r.yMin >= 8f;
                if (!inside) continue;
                if (label.width > 0 && r.Overlaps(label)) continue;
                best = t; found = true; break;
            }
            if (!found)
            {
                best = tries[0];
                if (best.x + W > right) best.x = m.x - DX - W;
                if (best.y > topLimit) best.y = m.y - DY;
            }
            best.x = Mathf.Clamp(best.x, 8f, Mathf.Max(8f, right - W));
            best.y = Mathf.Clamp(best.y, 8f + h, Mathf.Max(8f + h, topLimit));
            card.anchorMin = card.anchorMax = Vector2.zero; card.pivot = new Vector2(0, 1);
            card.anchoredPosition = best;
        }

        /// <summary>The game's overlay draws the unit's name label above it: the band over its screen rect, 8 u wider all round.</summary>
        static Rect LabelZone(ZObject o)
        {
            var ed = OzUnitEditor.I;
            if (ed == null || ed.Input == null) return default;
            Rect s = ed.Input.ScreenRect(o);
            float k = Mathf.Max(0.01f, OzUnitEditor.PxPerUnit);
            float cx = s.center.x / k, top = s.yMax / k;
            return new Rect(cx - 80f - 8f, top - 8f, 160f + 16f, 30f + 16f);
        }
    }
}
