// MEDIUM PLATFORM or HIGH PLATFORM, its class, the live line: HP and bar), REMOVE (Delete); what it does in three plain lines; four rows.
using System.Globalization;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    internal sealed partial class OzUeObjectPanel
    {
        static readonly string[] PlatformWords =
        {
            "Units drive and walk up its ramp and fight from the deck. Beside it, it blocks direct fire; arcing fire goes over.",
            "A unit on the deck sees and fires over it. Range and hit chance stay the same, as on a fort tower. Splash stays on its own level.",
            "Explosives only: pick them, then Ctrl + right-click it. Destroyed, everything on it dies with it.",
        };
        /// <summary>Checks: platform panels set up.</summary>
        internal static int PlatformSetups;

        void SetupPlatform()
        {
            string id = OzUePlayground.PlatformIdOf(obj);
            var p = obj.World != null ? obj.World.PlatformOf(obj.Id) : null;
            OzUeArt.Apply(head.Art, id, Team.Neutral);
            head.ShowRemove(true, "Remove it and what stands on it from the field (Delete)");
            head.SetName(OzUePlayground.FullName(id), false, OzUePlayground.ClassWord(id) + ", always neutral");
            float y = 0f;
            for (int i = 0; i < 3; i++)
            {
                line[i].rectTransform.anchoredPosition = new Vector2(12f, -y);
                line[i].rectTransform.sizeDelta = new Vector2(L.w - 24f, 60f);
                line[i].Set(PlatformWords[i]);
                y += Mathf.Max(13f * 1.36f, line[i].preferredHeight);
            }
            float wh = y + 8f;
            words.TL(0, L.head, L.w, wh);
            wordsLine.rectTransform.TL(0, wh - OzPx.Line(1f), L.w, OzPx.Line(1f));
            rows.TL(0, L.head + wh, L.w, 4 * RowH + 8f);
            string[] labs = { "Health", "Size", "Deck", "Ramp" };
            for (int i = 0; i < 4; i++) rowLab[i].Set(labs[i]);
            if (p != null)
            {
                string M(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
                rowVal[1].Set(M(p.Wide * 2) + " x " + M(p.Deep * 2) + " m");
                rowVal[2].Set(M(ZWorld.PlatformDeckRows * 2) + " x " + M(ZWorld.PlatformDeckRows * 2) + " m, " + M(p.DeckM) + " m high");
                rowVal[3].Set(M(ZWorld.PlatformDeckRows * 2) + " m wide, " + M(p.RampRows * 2) + " m long");
            }
            lastLive = null;
            PlatformSetups++;
            RefreshPlatform();
        }

        void RefreshPlatform()
        {
            int hp = Mathf.Max(0, obj.Health), max = obj.MaxHealth > 0 ? obj.MaxHealth : 1;
            string key = hp + "|" + max;
            if (key == lastLive) return;
            lastLive = key;
            float f = Mathf.Clamp01(hp / (float)max);
            string hpText = hp + " / " + max + " HP";
            head.LeftBig = true;
            head.SetLive(hpText, OzUi.Khaki, "", OzUi.Khaki, f, f > 0.4f ? (Color)OzUi.Khaki : f > 0.2f ? (Color)OzUi.Amber : (Color)OzUi.Alert, null);
            rowVal[0].Set(OzText.Tab(hpText));
        }
    }
}
