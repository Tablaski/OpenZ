// OpenZ (GPL-3.0) - 1.5 kit ue_exit_placement: the button column of the Unit editor's picked-unit card (REMOVE, EXIT DRIVER). Pure (System
// only): the card (UI/OzUeInspector.ExitDriver.cs) and the off-engine layout model (the kit's tools/layout_model) use the same numbers.
using System;

namespace OpenZ
{
    /// <summary>
    /// The EXIT DRIVER button stands in the picked-unit card of the STATS box, directly under REMOVE, both right aligned to the
    /// card's right edge (12 u in, compact head 10 u, as REMOVE always stood). REMOVE keeps its 76.7 u width; EXIT DRIVER is 106 u wide (the
    /// game's tab width, OpenZHud.DriverTabRect.w: "EXIT DRIVER" at REMOVE's 12 u / 0.12 is about 86 u, so it keeps REMOVE's ~10 u side room).
    /// <list type="bullet">
    /// <item>Normal head (88 u): both 28 u high (REMOVE's height), 4 u apart, REMOVE from 21 u (so the OPENZ ONLY chip still fits over it,
    /// UeHead.SetName needs 21 u), EXIT DRIVER 53-81 u (7 u above the card's words).</item>
    /// <item>Compact head (54 u): both 23 u high, 3 u apart, centred (2.5-25.5, 28.5-51.5).</item>
    /// <item>No EXIT DRIVER: REMOVE exactly as before (28 u, centred in the head).</item>
    /// </list>
    /// The rows of the head (name, "class, n on field", the live team / HP line and its bar) stop 10 u before every button beside them:
    /// before REMOVE as before (UeHead.Right), and before EXIT DRIVER where a row stands beside it (<see cref="RowRight"/>).
    /// </summary>
    public static class OzUeExitColumn
    {
        public const float RemoveW = 76.7f, ExitW = 106f, BtnH = 28f, BtnHCompact = 23f, Gap = 4f, GapCompact = 3f, TopNormal = 21f;
        /// <summary>The least room each side of the words EXIT DRIVER (REMOVE has 9.5 u): a wider font widens the button, never cuts it.</summary>
        public const float MinPadX = 8f;
        /// <summary>A row of the head stops this far before a button beside it (UeHead.Right: REMOVE's x - 10).</summary>
        public const float RowGap = 10f;

        /// <summary>REMOVE and EXIT DRIVER in a head of w x h (head-local, top-left origin). exit is empty when exitOn is false.</summary>
        public static void Lay(float w, float h, bool compact, bool exitOn, float exitW, out UeBox remove, out UeBox exit)
        {
            float right = w - (compact ? 10f : 12f);
            exit = default;
            if (!exitOn) { remove = new UeBox(right - RemoveW, (h - BtnH) * 0.5f, RemoveW, BtnH); return; }
            float bh = compact ? BtnHCompact : BtnH, gap = compact ? GapCompact : Gap;
            float top = compact ? (h - 2f * bh - gap) * 0.5f : TopNormal;
            remove = new UeBox(right - RemoveW, top, RemoveW, bh);
            exit = new UeBox(right - exitW, top + bh + gap, exitW, bh);
        }

        /// <summary>The right edge of a head row (centre mid, half its height half): right0 (UeHead.Right, REMOVE's rule), and 10 u before
        /// EXIT DRIVER when the row stands beside it.</summary>
        public static float RowRight(float right0, float mid, float half, bool exitOn, UeBox exit) =>
            exitOn && mid + half > exit.y && mid - half < exit.Bottom ? Math.Min(right0, exit.x - RowGap) : right0;
    }
}
