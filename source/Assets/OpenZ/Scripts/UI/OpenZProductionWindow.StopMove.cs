using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.4, the build menu's STOP beside the unit in production (the same player's second wish, sent on by the owner with REPEAT QUEUE):
    /// pure layout numbers (window units, the menu's top-left; the frame of OzLayout.Prod*).
    ///  - STOP: 70 x 24 (the height the 1.4 queue row's toggles had; the REPEAT switch replaced them) at the right end of the progress row, its right edge at 436 (the time's and
    ///    the bar's right edge), y 108..132 round the bar (117..127): 4 u under the next-unit line's caps (they end at 104) and 4 u over the
    ///    formula line's caps (136..144), so every text of the header keeps its size and its full width;
    ///  - the progress bar ends 6 u before STOP (111..360; it was 111..435): the frame's baked track runs on under the button;
    ///  - the bottom row (SET RALLY 15..223, the clear-rally X at 229..261) is OzRepeatSwitchLayout's, placed by UI/OpenZProductionWindow.RepeatQueue.cs
    ///    (the 1.4 queue REPEAT switch; before it this file put CLEAR at 15..116 and SET RALLY at 122..330).
    /// </summary>
    public static class OzStopMoveLayout
    {
        public static readonly OzRect Stop = new OzRect(366, 108, 70, 24);
        public static readonly OzRect Progress = new OzRect(OzLayout.ProdProgress.x, OzLayout.ProdProgress.y, 249, OzLayout.ProdProgress.h);
    }

    /// <summary>
    /// 1.4: STOP moves from the bottom row to the right end of the progress row, beside the unit it stops (numbers: OzStopMoveLayout). Its
    /// style (Danger), label, tooltip, click (StopBuilding, ui_production_cancel, the announcement, the toast) and its rule (live only while
    /// BUILDING, Refresh's SetB) are the same button's, untouched: only its place changes. The progress bar is shorter by STOP's width
    /// (Refresh reads <see cref="ProgressW"/> for the bar's moving edge and sheen); the bottom row: OpenZProductionWindow.BottomRowLayout.
    /// Needed since the queue REPEAT switch: its bottom row puts SET RALLY at 15..223, where STOP stood (OzBuildMenuLayout.Stop 15..116), so STOP
    /// must sit beside the bar (the switch kit's apply_kit.py requires this file; before it, the repeat_queue kit's --no-stop-move could leave it out).
    /// </summary>
    public partial class OpenZProductionWindow
    {
        RectTransform progRoot;
        bool stopMoved;

        /// <summary>The progress bar's width in window units (Refresh: the moving edge and the sheen).</summary>
        float ProgressW => stopMoved ? OzStopMoveLayout.Progress.w : OzLayout.ProdProgress.w;

        partial void StopMoveBuild()
        {
            progRoot = progFill.rectTransform.parent as RectTransform;
            StopMoveLayout(true);
        }

        /// <summary>kit = STOP in the header (1.4), else the 1.3 places (the layout check's before picture).</summary>
        partial void StopMoveLayout(bool kit)
        {
            if (progRoot == null) return;
            stopMoved = kit;
            var s = kit ? OzStopMoveLayout.Stop : OzBuildMenuLayout.Stop;
            bStop.Rt.SetParent(kit ? body : actionsRoot, false);
            bStop.Rt.Place(s); Rehatch(bStop, s.Size);
            progRoot.Place(kit ? OzStopMoveLayout.Progress : OzLayout.ProdProgress);
        }
    }
}
