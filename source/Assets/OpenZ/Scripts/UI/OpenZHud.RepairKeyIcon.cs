using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 21: the glyph of the wrench key over the command bar (OpenZHud.RepairKey.cs) is a spanner of its own. The HUD set's
    /// wrench (Kit/Icons/ic_wrench: a small round head on a thin stick) read like a pin on the key; it stays as it is for its other
    /// uses (the bridge / repair-station countdown badge, the crane's weapon mark). The key's spanners are drawn in the set's style
    /// (24-grid, solid white, no outline, cut-outs are holes, tinted Ink) with jaws that read at the key's 28 u:
    /// Kit/Icons/ic_spanner_a / _b / _c at 48 / 96 / 192 px (Hud.IconRel picks the tier by the on-screen size).
    /// </summary>
    public partial class OpenZHud
    {
        /// <summary>The spanner the key shows: 0 = A, a double open-end spanner (two open jaws, a thick shaft); 1 = B, a combination
        /// spanner (an open jaw and a ring); 2 = C, a spanner crossed with a hammer.</summary>
        public const int RepairKeyIconVariant = 0;

        static readonly string[] RepairKeyIcons = { "spanner_a", "spanner_b", "spanner_c" };

        /// <summary>The key's glyph name for Hud.Icon (Kit/Icons/ic_&lt;name&gt;_48 / _96 / _192).</summary>
        public static string RepairKeyIcon => RepairKeyIcons[Mathf.Clamp(RepairKeyIconVariant, 0, RepairKeyIcons.Length - 1)];
    }
}
