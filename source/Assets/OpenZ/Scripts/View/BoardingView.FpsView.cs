// OpenZ (GPL-3.0) - version 1.4 kit fps_view, fix round 2: read-only access to the crew figures BoardingView draws, for the hidden first-person
// view (View/OzFpsView.cs). Nothing here changes what BoardingView does.
using UnityEngine;

namespace OpenZ
{
    public partial class BoardingView
    {
        /// <summary>1.4 FPS VIEW: the crew figure this view draws for vehicle or gun `id` now (the jeep's seated driver, a light / medium / heavy
        /// tank's, the APC's or the crane's hatch commander, also while he is down in the hull and switched off, a gun's gunner), null when none.
        /// Read only: the first-person view holds it off from drawing while it looks out of that vehicle.</summary>
        public static GameObject CrewFigure(int id)
        {
            if (!I || I.game == null || !I.game.Views.TryGetValue(id, out var v) || !v || !I.rigs.TryGetValue(v, out var r)) return null;
            return r.crew ? r.crew : null;
        }

        /// <summary>1.4 FPS VIEW: the root every crew, boarding ghost and body of this view hangs under (OZ_BoardingCrew), null when none. Read only.</summary>
        public static Transform CrewBox => I && I.box ? I.box : null;
    }
}
