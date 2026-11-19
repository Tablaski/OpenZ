// 1.2 item 6, View/OzUnitEditor.cs: the STATS box's panel of a picked flag or obstacle (DESIGN_SPEC E110-E112; owner 6c: a neutral flag to
// test capturing, 6j: the holographic obstacle). Head as the inspector's (back, picture, FLAG / OBSTACLE, its kind, the live line: the
// flag's owner and how often it was taken, the obstacle's HP and bar), REMOVE; then what it does in plain lines and four rows.
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    internal sealed partial class OzUeObjectPanel                                                         // + UI/OzUeObjectPanel.Crate.cs (the GRENADES crate)
    {
        UeBoxShell shell; UeBoxLayout L;
        RectTransform root, words, rows;
        UeHead head;
        readonly OzText[] line = new OzText[3];
        readonly OzText[] rowLab = new OzText[4], rowVal = new OzText[4];
        Image wordsLine;
        UeInspect kind = UeInspect.None; ZObject obj;
        string lastLive;
        public bool Shown { get; private set; }
        public const float RowH = 28f, LabelW = 96f;

        static readonly string[] FlagWords =
        {
            "A robot or vehicle of another team that touches it takes it for its team.",
            "It can be taken back; it holds 1 s after a change.",
            "Pick units, right-click the flag: they all run onto it.",                             // the game's own flag order (ARCHITECTURE D5), not the prototype's "closest one"
        };
        static readonly string[] ObstacleWords =
        {
            "Blocks every shot of both teams: a unit with the box in its line holds its fire and walks round to see past it.",
            "Destructible by explosives only (Toughs, tanks, mechs, the MLRS, cannons): pick them, then Ctrl + right-click it. Their splash hurts it too.",
            "Several can stand side by side, never on units, the flag or each other.",
        };

        public void Build(UeBoxShell shell)
        {
            this.shell = shell; L = shell.L;
            root = OzUeGfx.Node(shell.Body, "Object", 0, 0, L.w, L.h - L.bodyTop);
            head = UeHead.Make(root, L, shell.Box, UeBoxShell.PFix);
            head.Remove.OnClick = cl => { var o = OzUePick.Inspected; if (o != null && OzUePlayground.Remove(o) > 0) OzSfx.Ui("ui_cancel"); };
            words = OzUeGfx.Node(root, "Words", 0, L.head, L.w, 60f);
            for (int i = 0; i < 3; i++)
                line[i] = Hud.Label(words, "Line" + i, "", 13, OzUi.Ink2, 0f, OzStyle.Plain).Body(12f, 0f, L.w - 24f);
            wordsLine = OzUeGfx.HLine(words, "Line", OzUeGfx.FootLine, 0, 59f, L.w);
            rows = OzUeGfx.Node(root, "Rows", 0, 150f, L.w, 4 * RowH + 8f);
            for (int i = 0; i < 4; i++)
            {
                float y = 2f + i * RowH;
                OzUeGfx.HLine(rows, "Line" + i, OzUeGfx.GridLine, 12f, y + RowH - OzPx.Line(1f), L.w - 24f);
                rowLab[i] = OzUeGfx.Text(rows, "Label" + i, "", 13, OzUi.Muted, 0f, 12f, y + RowH * 0.5f, OzH.L, LabelW - 4f);
                rowVal[i] = OzUeGfx.Text(rows, "Value" + i, "", 13, OzUi.Ink, 0f, 12f + LabelW, y + RowH * 0.5f, OzH.L, L.w - 24f - LabelW);
            }
            root.gameObject.SetActive(false);
        }

        /// <summary>The box's new height (UeBoxShell.Resize); the next Show places the panel with it.</summary>
        public void Resize(UeBoxLayout l) => L = l;

        public void Show(bool on, float top)
        {
            Shown = on;
            root.gameObject.SetActiveFast(on);
            if (!on) { kind = UeInspect.None; obj = null; return; }
            root.TL(0, top, L.w, L.h - L.bodyTop - top);
            var k = OzUePick.Kind; var o = OzUePick.Inspected;
            if (k != kind || o != obj) { kind = k; obj = o; Setup(); }
        }

        void Setup()
        {
            if (kind == UeInspect.Platform) { SetupPlatform(); return; }                              // UI/OzUeObjectPanel.Platform.cs
            if (kind == UeInspect.Crate) { SetupCrate(); return; }                                    // UI/OzUeObjectPanel.Crate.cs
            bool flag = kind == UeInspect.Flag;
            bool box = !flag && OzUePlayground.IsBox(obj);                                               // 1.5 ue_boxes: the BOX's own panel words
            string id = flag ? OzUePlayground.Flag : box ? OzUePlayground.Box : OzUePlayground.Obstacle;
            OzUeArt.Apply(head.Art, id, Team.Neutral);
            head.ShowRemove(true, "Remove it from the field (Delete)");
            head.SetName(flag ? "FLAG" : box ? "BOX" : "OBSTACLE", false, flag ? "Capture flag" : box ? "Holo box, always neutral" : "Holo obstacle, always neutral");
            var w = flag ? FlagWords : box ? OzUeBoxes.PanelWords : ObstacleWords;
            float y = 0f;
            for (int i = 0; i < 3; i++)
            {
                line[i].rectTransform.anchoredPosition = new Vector2(12f, -y);
                line[i].rectTransform.sizeDelta = new Vector2(L.w - 24f, 60f);
                line[i].Set(w[i]);
                y += Mathf.Max(13f * 1.36f, line[i].preferredHeight);
            }
            float wh = y + 8f;
            words.TL(0, L.head, L.w, wh);
            wordsLine.rectTransform.TL(0, wh - OzPx.Line(1f), L.w, OzPx.Line(1f));
            rows.TL(0, L.head + wh, L.w, 4 * RowH + 8f);
            string[] labs = flag ? new[] { "Owner", "Taken", "Takes it", "Never" } : new[] { "Health", "Size", "Shots", "Paths" };
            for (int i = 0; i < 4; i++) rowLab[i].Set(labs[i]);
            float side = OzUePlayground.ObstacleTiles * 2f;
            rowVal[2].Set(flag ? "A robot or vehicle that touches it" : "Blocks every shot");
            rowVal[3].Set(flag ? "The shuttle or a gun" : "Units walk round it");
            if (!flag) rowVal[1].Set(side.ToString("0") + " x " + side.ToString("0") + " m, " + OzUePlayground.ObstacleHeightM.ToString("0") + " m tall");
            if (box) rowVal[1].Set(OzUeBoxes.SizeWords(obj));                                              // 1.5 ue_boxes: 4 x 4 m, 4 m tall, its level
            lastLive = null;
            Refresh();
        }

        /// <summary>The live parts (2.5 times a second): owner and takes of the flag, HP of the obstacle.</summary>
        public void Refresh()
        {
            if (!Shown || obj == null) return;
            if (kind == UeInspect.Platform) { RefreshPlatform(); return; }                            // its HP
            if (kind == UeInspect.Crate) { RefreshCrate(); return; }                                  // its grenades and HP
            bool flag = kind == UeInspect.Flag;
            string key;
            if (flag)
            {
                int n = OzUePlayground.FlagTakes(obj);
                var t = obj.Owner;
                key = t + "|" + n;
                if (key == lastLive) return;
                lastLive = key;
                head.LeftBig = false;
                string owner = t == Team.Neutral ? "Neutral" : OzUeUi.TeamWordSentence(t) + " flag";
                head.SetLive(owner, t == Team.Neutral ? (Color)OzUi.Khaki : OzUeUi.TeamText(t), n == 0 ? "not taken yet" : "taken " + n + " x", OzUi.Ink, -1f, default, null);
                rowVal[0].Set(t == Team.Neutral ? "Neutral" : OzUeUi.TeamWordSentence(t));
                rowVal[1].Set(OzText.Tab(n + (n == 1 ? " time" : " times")));
            }
            else
            {
                int hp = Mathf.Max(0, obj.Health), max = obj.MaxHealth > 0 ? obj.MaxHealth : OzUePlayground.ObstacleHp;
                key = hp + "|" + max;
                if (key == lastLive) return;
                lastLive = key;
                float f = Mathf.Clamp01(hp / (float)max);
                string hpText = hp + " / " + max + " HP";
                head.LeftBig = true;
                head.SetLive(hpText, OzUi.Khaki, "", OzUi.Khaki, f, f > 0.4f ? (Color)OzUi.Khaki : f > 0.2f ? (Color)OzUi.Amber : (Color)OzUi.Alert, null);
                rowVal[0].Set(OzText.Tab(hpText));
            }
        }

        public void Tick() { }
    }
}
