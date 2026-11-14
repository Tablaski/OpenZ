using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (UI/OzUeControlsRoot.cs; spec E020): the empty field's hint, centred in the free field at 44 % of the field's height:
    /// "DRAG UNITS UP FROM THE DOCK" and under it what else to do. It fades out (300 ms) once anything stands on the field and comes
    /// back when the field is empty again. It also fades out while a drop is dragged or armed (OzUePlace.Busy): the arm bar and the
    /// drop's own words under the preview say what to do then, and the preview with its rings and words stands where the hint was.
    /// It never takes the mouse.
    /// </summary>
    public sealed class OzUeFieldHint
    {
        public const string Line1 = "DRAG UNITS UP FROM THE DOCK";
        public const string Line2 = "or click a unit, then click the field. Click units of either team, then right-click to give orders.";
        RectTransform root; CanvasGroup cg; OzText l1, l2;
        bool empty = true; float fadeFrom = 1f, fadeTo = 1f, fadeAt = -10f, shown = 1f;
        bool dirty = true;

        public void Build(OzUnitEditor ed, Transform field)
        {
            root = Hud.Node("UE_FieldHint", field);
            cg = root.gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;
            l1 = Hud.Label(root, "Line1", Line1, 14f, OzUi.A(OzUi.Ink, 0.85f), 0.20f, OzStyle.Underlay);
            l2 = Hud.Label(root, "Line2", Line2, 13f, OzUi.Ink2, 0f, OzStyle.Underlay);
            OzUePlayground.FieldChanged += OnField;
        }

        public void Destroy() { OzUePlayground.FieldChanged -= OnField; }

        void OnField() { dirty = true; }

        /// <summary>The free field (canvas units, bottom-left origin) and the field's top (the top bar) on a canvas of cw x ch.</summary>
        public void Layout(Rect free, float cw, float ch, float topInset)
        {
            float fieldTop = topInset, fieldH = ch - topInset;
            float cx = free.width > 1f ? free.center.x : cw * 0.5f;
            float cy = fieldTop + 0.44f * fieldH;                                                  // top-left canvas coordinates
            root.TL(cx - 400f, cy - 21f, 800f, 42f);
            l1.At(400f, 8.5f, OzH.C, true, 0f);
            l2.At(400f, 33.5f, OzH.C, true, 0f);
        }

        public void Tick()
        {
            if (dirty)
            {
                dirty = false;
                bool any = false;
                foreach (var o in OzUePlayground.Placed()) { if (o != null) { any = true; break; } }
                empty = !any;
            }
            bool want = empty && !OzUePlace.Busy;                                                  // never while a drop is dragged or armed
            if ((fadeTo > 0.5f) != want) { fadeFrom = shown; fadeTo = want ? 1f : 0f; fadeAt = Time.unscaledTime; }
            float d = OzMotion.Dur(0.3f, true);
            float k = d <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - fadeAt) / d);
            shown = Mathf.Lerp(fadeFrom, fadeTo, k);
            cg.alpha = shown;
            root.gameObject.SetActiveFast(shown > 0.001f);
        }

        /// <summary>The hint shows now (for the counted check).</summary>
        public bool Showing => shown > 0.5f;
    }
}
