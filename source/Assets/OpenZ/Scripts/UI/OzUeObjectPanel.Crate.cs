// 1.2 item 73, UI/OzUeObjectPanel.cs: the STATS box's panel of a GRENADES crate picked on the field, as the flag's and the obstacle's. Head
// (back, the crate's picture, GRENADE CRATE, "Grenade crate, always neutral", the live line: its grenades and its HP with the bar), REMOVE
// (Delete); what it does in three plain lines; four rows (grenades, health, who takes it, who never does).
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    internal sealed partial class OzUeObjectPanel
    {
        static readonly string[] CrateWords =
        {
            "A robot that walks over it, or is sent onto it, picks it up: its squad gets the grenades and the crate is gone.",
            "Toughs and electrobots never take grenades, nor a squad that still has some.",
            "Shots and blasts can blow it up: its grenades fly out and burst around it.",
        };
        /// <summary>Checks: crate panels set up, the live line shown last ("20 grenades, 300 / 300 HP").</summary>
        internal static int CrateSetups;
        internal static string LastCrateLive;

        void SetupCrate()
        {
            OzUeArt.Apply(head.Art, OzUePlayground.Grenades, Team.Neutral);
            head.ShowRemove(true, "Remove it from the field (Delete)");
            head.SetName("GRENADE CRATE", false, "Grenade crate, always neutral");
            float y = 0f;
            for (int i = 0; i < 3; i++)
            {
                line[i].rectTransform.anchoredPosition = new Vector2(12f, -y);
                line[i].rectTransform.sizeDelta = new Vector2(L.w - 24f, 60f);
                line[i].Set(CrateWords[i]);
                y += Mathf.Max(13f * 1.36f, line[i].preferredHeight);
            }
            float wh = y + 8f;
            words.TL(0, L.head, L.w, wh);
            wordsLine.rectTransform.TL(0, wh - OzPx.Line(1f), L.w, OzPx.Line(1f));
            rows.TL(0, L.head + wh, L.w, 4 * RowH + 8f);
            string[] labs = { "Grenades", "Health", "Takes it", "Never" };
            for (int i = 0; i < 4; i++) rowLab[i].Set(labs[i]);
            rowVal[2].Set("A robot walking over it");
            rowVal[3].Set("Toughs, electrobots");
            lastLive = null;
            CrateSetups++;
            RefreshCrate();
        }

        /// <summary>The live parts: the grenades in the crate and its HP.</summary>
        void RefreshCrate()
        {
            int n = Mathf.Max(0, obj.Grenades), hp = Mathf.Max(0, obj.Health), max = obj.MaxHealth > 0 ? obj.MaxHealth : 1;
            string key = n + "|" + hp + "|" + max;
            if (key == lastLive) return;
            lastLive = key;
            float f = Mathf.Clamp01(hp / (float)max);
            string count = n + (n == 1 ? " grenade" : " grenades"), hpText = hp + " / " + max + " HP";
            head.LeftBig = false;
            head.SetLive(count, OzUi.Khaki, hpText, OzUi.Ink, f, f > 0.4f ? (Color)OzUi.Khaki : f > 0.2f ? (Color)OzUi.Amber : (Color)OzUi.Alert, null);
            rowVal[0].Set(OzText.Tab(count));
            rowVal[1].Set(OzText.Tab(hpText));
            LastCrateLive = count + ", " + hpText;
        }
    }
}
