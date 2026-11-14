using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// DV161 EvilZod: the small filling circle of his power dash, left of his health bar (a child of the bar, so it shows and hides with it; the bar's marks are pooled, so it
    /// switches off for anyone else). The production ring's art, clockwise from the top: amber while it fills, the map's ink colour when ready, with a short pop on the moment.
    /// </summary>
    public static class OzEvilZodCircle
    {
        public const string Node = "EvilDashRing";
        public static float Size = 12f, Gap = 3f, PopSeconds = 0.3f, PopScale = 1.35f;
        /// <summary>Checks: the last fill drawn, how many frames drew it.</summary>
        public static float LastFill; public static int Drawn;
        static int popId = -1; static float popAt = -9f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { popId = -1; popAt = -9f; LastFill = 0f; Drawn = 0; }

        /// <summary>The rules' EvilDashReady (OzEvilZodFx).</summary>
        public static void Ready(int id) { popId = id; popAt = Time.unscaledTime; }

        /// <summary>OpenZUnitOverlay.Draw, beside the veteran star. o may be anyone: for all but him the ring is off.</summary>
        public static void Draw(RectTransform bar, ZObject o, float alpha)
        {
            if (!bar) return;
            bool him = o != null && o.IsEvilZod;
            var t = bar.Find(Node);
            if (!t)
            {
                if (!him) return;
                t = Make(bar);
            }
            bool wasOn = t.gameObject.activeSelf;
            if (wasOn != him) t.gameObject.SetActive(him);
            if (!him) return;
            var arc = t.Find("Arc")?.GetComponent<Image>(); var track = t.Find("Track")?.GetComponent<Image>();
            if (!arc) return;
            float f = Mathf.Clamp01((float)o.EvilDashCharge01);
            arc.fillAmount = f; LastFill = f; Drawn++;
            Color c = f >= 0.999f ? OzUi.Ink : OzUi.Amber; c.a = alpha;
            if (arc.color != c) arc.color = c;
            if (track) { var tc = track.color; if (Mathf.Abs(tc.a - 0.8f * alpha) > 0.01f) { tc.a = 0.8f * alpha; track.color = tc; } }
            float age = o.Id == popId ? Time.unscaledTime - popAt : 9f;
            float s = age < PopSeconds ? Mathf.Lerp(PopScale, 1f, Mathf.SmoothStep(0f, 1f, age / PopSeconds)) : 1f;
            t.localScale = Vector3.one * s;
        }

        static Transform Make(RectTransform bar)
        {
            var rt = Hud.Node(Node, bar);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(1f, 0.5f); rt.anchoredPosition = new Vector2(-Gap, 0f); rt.sizeDelta = new Vector2(Size, Size);
            var track = Hud.Kit(rt, "Track", "Production/ring_sm_track", new OzRect(0, 0, Size, Size), null, Image.Type.Simple);
            track.raycastTarget = false;
            Image arc;
            if (OzKit.Available) arc = OzKit.RingFill(rt, "Arc", true);
            else { arc = Hud.Img(rt, "Arc", OzUi.Amber); arc.type = Image.Type.Filled; arc.fillMethod = Image.FillMethod.Radial360; arc.fillOrigin = (int)Image.Origin360.Top; arc.fillClockwise = true; }
            arc.rectTransform.Place(new OzRect(0, 0, Size, Size));
            arc.raycastTarget = false;
            return rt;
        }
    }
}
