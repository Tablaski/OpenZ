using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (UI/OzUeControlsRoot.cs; owner 6g "view should not be called view but background and in different place", 6j "count
    /// of blue and red on left top smaller its unimportant info", 6k "background just settings icon not huge useless button"; spec
    /// E010-E012): the small plate top-left: the BACKGROUND gear (opens the floor styles' popover) and the live units per team ("BLUE n
    /// RED n": every robot of a squad counted, as the HUD's top strip counts), updated twice a second; the team drawn blue first.
    /// </summary>
    public sealed class OzUeCounts
    {
        public const float PlateH = 36f, PadL = 4f, PadR = 8f, CellGap = 6f, CellH = 22f, CellPad = 2f, CapNumGap = 4f, GearSize = 28f, GearGap = 4f;
        /// <summary>Checks: gear clicks.</summary>
        public static int GearClicks;

        public RectTransform Plate;
        public HudButton Gear;
        readonly HudButton[] cells = new HudButton[2];
        readonly OzText[] caps = new OzText[2], nums = new OzText[2];
        readonly Team[] order = new Team[2];
        readonly int[] shown = { -1, -1 };
        float nextCount, top = 40f;
        string w0 = "", w1 = "";
        public System.Action OnGear;

        public UeBox PlateBox { get; private set; }

        public void Build(OzUnitEditor ed, Transform panels)
        {
            GearClicks = 0;
            Plate = Hud.Node("UE_Counts", panels);
            Hud.Plate(Plate, "sm");
            ed.Hud.AddBlocker(Plate);
            Gear = HudButton.Make(Plate, "Gear", null, new Vector2(GearSize, GearSize), HudButtonKind.Small, 12, 0f);
            Hud.Icon(Gear.Content, "Icon", "gear", (GearSize - 14f) * 0.5f, (GearSize - 14f) * 0.5f, 14f, OzUi.Ink2);
            Gear.Tooltip = OzUeW.Tip("Background", null, "Floor style, floor lines, name labels, motion");
            Gear.OnClick = c => { GearClicks++; OnGear?.Invoke(); };
            for (int k = 0; k < 2; k++)
            {
                cells[k] = OzUeW.Bare(Plate, "Count" + k, 40f, CellH);
                cells[k].OnClick = c => { };                                                                // a click answers with the HUD's UI click like every control (the count has no action)
                caps[k] = Hud.Label(cells[k].Rt, "Team", "BLUE", 11f, OzUi.BlueText, 0.10f, OzStyle.Plain);
                nums[k] = Hud.Label(cells[k].Rt, "N", "0", 12f, OzUi.BlueText, 0f, OzStyle.Plain);
            }
        }

        public void Layout(float topInset)
        {
            top = topInset;
            Relayout();
        }

        void Relayout()
        {
            // the team drawn blue first (YOUR TEAM COLOUR, D16)
            order[0] = TeamColors.Shown(Team.Blue) == Team.Blue ? Team.Blue : Team.Red;
            order[1] = order[0] == Team.Blue ? Team.Red : Team.Blue;
            float x = OzUeBottomLayout.Border + PadL;
            Gear.Rt.TL(x, (PlateH - GearSize) * 0.5f, GearSize, GearSize);
            x += GearSize + GearGap + CellGap;
            for (int k = 0; k < 2; k++)
            {
                var t = order[k];
                caps[k].Set(OzUeUi.TeamWord(t)); caps[k].color = OzUeUi.TeamText(t); nums[k].color = OzUeCap.CountColour(t, Mathf.Max(0, shown[k]));
                cells[k].Tooltip = OzUeW.Tip(OzUeUi.TeamWordSentence(t) + " units on the field", null, OzUeCap.CountTip);
                string n = OzUeCap.CountText(Mathf.Max(0, shown[k]));                                            // "245/300" near the unit cap (View/OzUeCap.cs)
                float cw = caps[k].WidthAt(OzUeUi.TeamWord(t)), nw = nums[k].WidthAt(OzText.Tab(n));
                float w = CellPad + cw + CapNumGap + nw + CellPad;
                cells[k].Rt.TL(x, (PlateH - CellH) * 0.5f, w, CellH);
                caps[k].At(CellPad, CellH * 0.5f, OzH.L, true, 0f);
                nums[k].At(CellPad + cw + CapNumGap, CellH * 0.5f, OzH.L, true, 0f);
                x += w + (k == 0 ? CellGap : 0f);
            }
            float pw = x + PadR + OzUeBottomLayout.Border;
            PlateBox = new UeBox(OzUeBottomLayout.Gap, top + OzUeBottomLayout.Gap, pw, PlateH);
            Plate.TL(PlateBox.x, PlateBox.y, PlateBox.w, PlateBox.h);
            w0 = OzUeUi.TeamWord(order[0]); w1 = OzUeUi.TeamWord(order[1]);
        }

        public void Tick(bool hidden)
        {
            Plate.gameObject.SetActiveFast(!hidden);
            if (hidden) return;
            float now = Time.unscaledTime;
            if (now < nextCount) return;
            nextCount = now + 0.5f;
            bool again = OzUeUi.TeamWord(order[0]) != w0 || OzUeUi.TeamWord(order[1]) != w1;
            for (int k = 0; k < 2; k++)
            {
                int n = OzUePlayground.CountUnits(order[k]);
                if (n != shown[k]) { shown[k] = n; nums[k].Set(OzText.Tab(OzUeCap.CountText(n))); again = true; }
            }
            if (again) Relayout();
        }
    }
}
