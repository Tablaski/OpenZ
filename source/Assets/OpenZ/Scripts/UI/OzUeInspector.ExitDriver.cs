// OpenZ (GPL-3.0) - 1.5 kit ue_exit_placement: the EXIT DRIVER button in the Unit editor's picked-unit card (the STATS box's inspector
// head, under REMOVE). The rules stay the game's (UI/OzUeDriverTab.cs mirrors OpenZHud's own EXIT DRIVER tab); the column's numbers are in
// UI/OzUeExitColumn.cs.
using UnityEngine;

namespace OpenZ
{
    internal sealed partial class UeHead
    {
        /// <summary>The card's EXIT DRIVER button (the inspector's head only; null in the flag / obstacle panel).</summary>
        public HudButton Exit;
        public bool ExitOn { get; private set; }
        /// <summary>Its side tooltip (left of the STATS box, level with the button): the game's tab tooltip without its keycap part.</summary>
        public string ExitTip = "";
        float colW, colH, exitW = OzUeExitColumn.ExitW; bool colCompact; UeBox exitBox;

        /// <summary>Built once with the inspector (OzUeInspector.Build), hidden. Same kit as REMOVE (OzUeGfx.Button: HudButton.Make, the
        /// HUD's states and sounds, the box's priority), the normal style (REMOVE is the red one), 12 u / 0.12 like REMOVE.</summary>
        public void MakeExit(UeBoxLayout L, RectTransform box, int prio)
        {
            colW = L.w; colH = L.head; colCompact = L.compact;
            Exit = OzUeGfx.Button(Rt, "UE_DriverExit", OzUeDriverTab.Label, 0f, 0f, exitW, OzUeExitColumn.BtnH, HudButtonStyle.Normal, prio);
            if (Exit.Label != null)
            {
                float need = Exit.Label.WidthAt(OzUeDriverTab.Label) + 2f * OzUeExitColumn.MinPadX;
                if (need > exitW) exitW = Mathf.Ceil(need);                                                  // never a cut label
            }
            Exit.Tooltip = null;                                                                             // no HUD tooltip (it covered the dock): the editor's side tip only
            Exit.OnClick = c => OzUeDriverTab.Click();
            OzUeSideTip.Add(Exit.Rt, () => ExitTip, OzUeSideTip.Place.Side, prio, box);
            Exit.Rt.gameObject.SetActive(false);
            OzUeDriverTab.Builds++;
            Debug.Log($"[OpenZ UnitEditor] EXIT DRIVER button built in the picked-unit card, under REMOVE ({exitW:0.#} x "
                      + $"{(L.compact ? OzUeExitColumn.BtnHCompact : OzUeExitColumn.BtnH):0} u, card {L.w:0.#} u wide, {(L.compact ? "compact" : "normal")} head {L.head:0} u)");
        }

        /// <summary>The column: REMOVE alone as before, or REMOVE with EXIT DRIVER right under it (UI/OzUeExitColumn.cs).</summary>
        public void ShowExit(bool on)
        {
            if (Exit == null || !Exit.Rt) return;
            ExitOn = on;
            OzUeExitColumn.Lay(colW, colH, colCompact, on, exitW, out var r, out var e);
            Remove.Rt.TL(r.x, r.y, r.w, r.h);
            exitBox = e;
            if (on) Exit.Rt.TL(e.x, e.y, e.w, e.h);
            Exit.Rt.SetActiveFast(on);
        }

        /// <summary>The HP text's box on the live line: as before (40 u kept for the team word); beside EXIT DRIVER the team word's own
        /// width + 8 u is kept free instead.</summary>
        float LiveHpBox(float r) => Mathf.Max(40f, r - textX - (ExitOn && !LeftBig ? LiveL.WidthAt(LiveL.text) + 8f : 40f));

        /// <summary>Beside EXIT DRIVER, an "n / m HP" that does not fit its box at full size is shown as "n / m" (the bar under it still shows
        /// the fraction; a narrow card, e.g. 1366 x 768 at UI SCALE 100 %). Otherwise the words as given.</summary>
        string LiveHpWords(string right, float r)
        {
            if (!ExitOn || LeftBig || string.IsNullOrEmpty(right) || !right.EndsWith(" HP")) return right;
            return LiveR.WidthAt(OzText.Tab(right)) <= LiveHpBox(r) ? right : right.Substring(0, right.Length - 3);
        }

        float RightBeside(float mid, float half) => OzUeExitColumn.RowRight(Right, mid, half, ExitOn, exitBox);
        /// <summary>The right edges the head's rows use now (the checks read them too).</summary>
        internal float NameRight => Right;
        internal float MetaRight => RightBeside(metaMid, 7f);
        internal float LiveRight => Mathf.Min(RightBeside(liveMid, 8f), RightBeside(barY + 2f, 2f));
        internal float TextX => textX;
        internal UeBox ExitBox => exitBox;
    }

    internal sealed partial class OzUeInspector
    {
        internal HudButton ExitButton => head != null ? head.Exit : null;
        internal UeHead Head => head;

        /// <summary>Every frame after the HUD's Update (OzUeDriverTab.LateUpdate): the button shows while the game's own tab would show AND
        /// this card shows one of those picked tanks; live / greyed and its tooltip as the game's tab. A change lays the head out again.</summary>
        internal void TickExit(OpenZHud hud)
        {
            if (head == null || head.Exit == null || !head.Exit.Rt) return;
            bool want = Shown && head.Rt.gameObject.activeInHierarchy && OzUeDriverTab.CardWants(hud);
            if (want != head.ExitOn)
            {
                head.ShowExit(want);
                if (id != null)
                {
                    head.SetName(OzUePlayground.FullName(id), OzUePlayground.OpenZOnly(id), OzUePlayground.ClassWord(id) + ", " + OzUePlayground.OnField(id) + " on field");
                    TickLive();
                }
                if (want) OzUeDriverTab.Shows++;
            }
            if (!want) return;
            bool live = hud.DriverTabLive;
            if (head.Exit.Interactable != live) { head.Exit.Interactable = live; head.Exit.Refresh(); }
            head.ExitTip = OzUeDriverTab.SideTip(hud.DriverTabTip);
        }
    }
}
