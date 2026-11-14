using UnityEngine;

namespace OpenZ
{
    public partial class UnitView
    {
        /// <summary>Proof (k92, item 146: "standing" is the DRAWN tank standing): the place and heading (deg) the planted gear sampled its ground
        /// under this frame (the root as UnitView placed it from the sim, before the shot's rock and slide); false when nothing is planted.</summary>
        public bool ProofGearPlace(out Vector3 place, out float yawDeg)
        {
            place = gearLastP; yawDeg = gearLastYaw;
            return gearInit && gearValid;
        }
    }
}
