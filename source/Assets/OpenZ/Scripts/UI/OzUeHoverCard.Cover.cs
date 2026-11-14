using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (UI/OzUeHoverCard.cs): when the field's hover card may not show. It is the field's tooltip and lives on the panels'
    /// layer, so everything of the sheet, popover and dialog layers draws over it: the unit card, the TABLE, the help card, the background
    /// menu, the balance view's stat list, the share and LOAD dialogs. While any of them is open the hover card stays hidden, wherever the
    /// pointer is (before, a unit under the pointer beside an open unit card got its hover card BEHIND the card, and its words showed
    /// through the card's plate). It also stays hidden while the pointer is over any panel (OpenZHud.PointerOverUi: every panel of the
    /// editor is a blocker of the HUD), while a dialog or the leave question owns the mouse, while placing and while the panels are hidden.
    /// </summary>
    internal sealed partial class OzUeHoverCard
    {
        static readonly UeLayer[] Above = { UeLayer.Sheets, UeLayer.Popovers, UeLayer.Dialogs };

        /// <summary>The open card or sheet over the field that keeps the hover card hidden (the first found), or null when nothing of the
        /// sheet, popover and dialog layers is open. A module's holder (a node with no picture of its own that fills its layer, as
        /// OzUeStatsPopovers or the dialogs' dim) is looked into one level deep; nothing is searched and nothing is allocated.</summary>
        internal static Transform CoveredBy()
        {
            for (int l = 0; l < Above.Length; l++)
            {
                var layer = OzUnitEditor.Layer(Above[l]); if (!layer) continue;
                for (int i = 0; i < layer.childCount; i++)
                {
                    var c = layer.GetChild(i);
                    if (!c.gameObject.activeSelf) continue;
                    if (!Holder(c, layer)) return c;                                                            // a card, a sheet, a popover
                    for (int k = 0; k < c.childCount; k++)
                    {
                        var d = c.GetChild(k);
                        if (d.gameObject.activeSelf) return Fills(d, layer) ? c : d;                            // a holder's full-size picture (the dialogs' dim) stands for the holder
                    }
                }
            }
            return null;
        }

        /// <summary>A node that only holds a module's cards: no picture of its own and the size of its layer.</summary>
        static bool Holder(Transform t, RectTransform layer) => !t.TryGetComponent<Graphic>(out _) && Fills(t, layer);

        static bool Fills(Transform t, RectTransform layer)
        {
            if (!(t is RectTransform r)) return false;
            Vector2 a = r.rect.size, b = layer.rect.size;
            return Mathf.Abs(a.x - b.x) < 2f && Mathf.Abs(a.y - b.y) < 2f;
        }
    }
}
