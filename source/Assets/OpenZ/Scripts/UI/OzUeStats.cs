// 1.2 item 6, View/OzUnitEditor.cs: the STATS box (DESIGN_SPEC E050: one box at the right, place AND size never change between its tabs,
// owner 6j: its BALANCE tab, the TABLE sheet and the unit card: the state
// other modules switch (keys I, B, T, [ ], every "i" badge) and the box's layout at any canvas size (pure: the checks lay it out at the
// six canvas sizes of spec 2.1 in edit mode).
using System;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    /// <summary>The STATS box, its BALANCE tab, the TABLE sheet and the unit card.</summary>
    public static class OzUeStats
    {
        internal enum Tab { Stats = 0, Balance = 1, Dynamics = 2, Testing = 3 }
        internal static bool LaboratoryTab => tab == Tab.Dynamics || tab == Tab.Testing;

        static bool boxOpen = true, tableOpen;
        static Tab tab = Tab.Stats;
        static string cardId; static Team cardTeam = Team.Blue;
        /// <summary>The panels redraw on this (raised from the editor's own calls only).</summary>
        internal static event Action StateChanged;

        public static bool BoxOpen => boxOpen;
        /// <summary>I: the box or its rail (with a click sound: the key path; the box's own buttons sound by themselves).</summary>
        public static void ToggleBox() { SetBox(!boxOpen); OzSfx.Ui("ui_click"); }
        internal static void SetBox(bool open) { if (boxOpen == open) return; if (!open) OzBehaviorPanel.I?.CommitInputs(); boxOpen = open; Raise(); }

        public static bool BalanceOpen => tab == Tab.Balance;
        /// <summary>B: the Balance tab of the same box (no layout jump); the box opens when it was hidden.</summary>
        public static void ToggleBalance() { SetTab(tab == Tab.Balance ? Tab.Stats : Tab.Balance); OzSfx.Ui("ui_click"); }
        internal static Tab CurrentTab => tab;
        internal static void SetTab(Tab t) { bool ch = tab != t || !boxOpen; if (ch) OzBehaviorPanel.I?.CommitInputs(); tab = t; boxOpen = true; if (ch) Raise(); }

        /// <summary>[ and ]: the previous / next stat of the Balance tab (only while it shows).</summary>
        public static void StepBalanceStat(int dir)
        {
            if (tab != Tab.Balance || !boxOpen) return;
            var r = OzUeStatsRoot.I;
            if (r != null && r.Step(dir)) OzSfx.Ui("ui_click");
        }

        public static bool TableOpen => tableOpen;
        /// <summary>T: the table over the field (nothing else moves).</summary>
        public static void ToggleTable() { SetTable(!tableOpen); OzSfx.Ui("ui_click"); }
        internal static void SetTable(bool open) { if (tableOpen == open) return; tableOpen = open; Raise(); }

        public static bool CardOpen => cardId != null;
        internal static string CardId => cardId;
        internal static Team CardTeam => cardTeam;
        /// <summary>Every "i" badge (dock tiles, list, balance and table rows): the unit card of that unit.</summary>
        public static void OpenCard(string id, Team team)
        {
            if (id == null || !OzUeLiveStats.Known(id)) return;
            cardId = id; cardTeam = team == Team.Red ? Team.Red : Team.Blue;
            Raise();
        }
        public static void CloseCard() { if (cardId == null) return; cardId = null; Raise(); }

        /// <summary>The box's width now (canvas units; 0 while hidden).</summary>
        public static float BoxWidth
        {
            get
            {
                if (!boxOpen || OzUnitEditor.PanelsHidden || !OzUnitEditor.UiBuilt) return 0f;
                return UeBoxLayout.ForTab(OzUnitEditor.CanvasSize, OzUeInsets.Get(UeSide.Top), OzUeInsets.Get(UeSide.Bottom), tab).w;
            }
        }

        static void Raise()
        {
            var h = StateChanged;
            if (h == null) return;
            try { h(); } catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>A new editor session: every switch back to its start (box open on STATS, no table, no card).</summary>
        internal static void Reset() { boxOpen = true; tableOpen = false; tab = Tab.Stats; cardId = null; StateChanged = null; }
    }

    /// <summary>The box's place and inner sizes at one canvas size (DESIGN_SPEC 2.2 / 2.3 / 4.8-4.12). Pure.</summary>
    internal struct UeBoxLayout
    {
        public Vector2 canvas;
        public float x, y, w, h;
        public bool compact, hideVs, narrowList, narrowBal, narrowWindow;
        public float header, setTop, setH, bodyTop, strip;
        public float head, portrait, ctl, words, wordsLine, wordsPad, gridHead, gridRow, input, foot;

        public const float Gap = 12f, MaxH = 860f, RailW = 40f, RailH = 105.5f;
        /// <summary>The inspector at normal rows (spec 2.3): under this the box uses the compact rows.</summary>
        public const float NormalNeed = 705f;

        internal static UeBoxLayout ForTab(Vector2 canvas, float insetTop, float insetBottom, OzUeStats.Tab tab)
        {
            var l = For(canvas, insetTop, insetBottom);
            float desired = tab == OzUeStats.Tab.Dynamics ? Mathf.Clamp(canvas.x * .36f, 640, 760) :
                tab == OzUeStats.Tab.Testing ? Mathf.Clamp(canvas.x * .29f, 520, 620) : Mathf.Max(500, l.w);
            l.w = Mathf.Min(desired, Mathf.Max(500, canvas.x - 2 * Gap));
            l.x = canvas.x - Gap - l.w;
            l.hideVs = l.w < 419; l.narrowList = l.w < 400; l.narrowBal = l.w < 380;
            return l;
        }

        public static UeBoxLayout For(Vector2 canvas, float insetTop, float insetBottom)
        {
            var L = new UeBoxLayout { canvas = canvas };
            float top = insetTop > 1f ? insetTop : 40f;
            L.w = Mathf.Clamp(0.22f * canvas.x, 360f, 470f);
            L.y = top + Gap;
            L.h = Mathf.Max(240f, Mathf.Min(canvas.y - L.y - Gap - Mathf.Max(0f, insetBottom), MaxH));
            L.x = canvas.x - Gap - L.w;
            L.compact = L.h < NormalNeed;
            L.hideVs = L.w < 419f; L.narrowList = L.w < 400f; L.narrowBal = L.w < 380f;
            L.narrowWindow = canvas.x < 1500f || canvas.y < 800f;
            bool c = L.compact;
            L.header = c ? 32f : 36f;
            L.setTop = L.header + (c ? 4f : 8f) + 1f;
            L.setH = 32f;
            L.bodyTop = L.setTop + L.setH + (c ? 4f : 8f);
            L.strip = c ? 26f : 30f;
            L.head = c ? 54f : 88f; L.portrait = c ? 42f : 64f; L.ctl = c ? 30f : 32f;
            L.words = c ? 12f : 13f; L.wordsLine = (c ? 1.30f : 1.36f) * L.words; L.wordsPad = c ? 5f : 8f;
            L.gridHead = c ? 23f : 28f; L.gridRow = c ? 21f : 26f; L.input = c ? 20f : 22f; L.foot = c ? 34f : 44f;
            return L;
        }

        /// <summary>The box's rect on the canvas, top-left origin.</summary>
        public Rect Box => new Rect(x, y, w, h);
        public Rect Rail => new Rect(canvas.x - Gap - RailW, y, RailW, RailH);

        // ---- the inspector grid (E089): name 1fr | OpenZ 50 | ZodEngine 66 | Z-Wiki 50 | Custom 60 | vs OpenZ 56 (0 under 419 u), padding 0 10
        public const float GridPad = 10f;
        public float VsW => hideVs ? 0f : 56f;
        public float GridW => w - 2f * GridPad;
        /// <summary>Right edges of the columns, grid-local: [0] name, [1] OpenZ, [2] ZodEngine, [3] Z-Wiki, [4] Custom, [5] vs OpenZ.</summary>
        public float ColRight(int i)
        {
            float r = GridW; if (i == 5) return r;
            r -= VsW; if (i == 4) return r;
            r -= 60f; if (i == 3) return r;
            r -= 50f; if (i == 2) return r;
            r -= 66f; if (i == 1) return r;
            return r - 50f;
        }
        public float NameW => ColRight(0);
    }

    /// <summary>A left press on a unit picture (row, portrait, table, card) that travels more than 6 u starts the drag onto the field.</summary>
    internal static class UeDrag
    {
        static string id; static Team team; static Vector2 from; static bool armed;
        public const float Travel = 6f;
        public static int Started;

        public static void Arm(string unitId, Team t) { id = unitId; team = t; from = OzUeGfx.PointerCanvas; armed = unitId != null; }

        public static void Tick()
        {
            if (!armed) return;
            var m = Mouse.current;
            if (m == null || !m.leftButton.isPressed) { armed = false; return; }
            if ((OzUeGfx.PointerCanvas - from).sqrMagnitude > Travel * Travel)
            {
                armed = false;
                Started++;
                OzUePlace.BeginDrag(id, team, OzUeGfx.PointerScreen);
            }
        }

        public static void Cancel() => armed = false;
    }
}
