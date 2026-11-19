// top of its flight, to the once-a-minute air burst (View/OzExpInGame.cs HeadApex; hook in UnitView.UpdateToss). View only.
using UnityEngine;

namespace OpenZ
{
    public partial class UnitView
    {
        /// <summary>The thrown head already offered (one offer per throw).</summary>
        Transform tossHeadOffered;
        /// <summary>The share of the flight (0..1) at which the head is offered: just after the top of its arc (0.5).</summary>
        public static float HeadOfferAt = 0.47f;
    }
}
