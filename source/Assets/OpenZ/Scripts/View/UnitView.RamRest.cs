// OpenZ (GPL-3.0)
using UnityEngine;

namespace OpenZ
{
    /// <summary>1.5 kit rammed_wreck_rest_n30: what OzRammedWreckRest reads of a vehicle wreck, and its hook at the end of BeginWreck
    /// (View/UnitView.Deaths.cs): the death is drawn, the wreck's lights go off and a rammed wreck is laid on the ground (View/OzRammedWreckRest.cs).</summary>
    public partial class UnitView
    {
        /// <summary>The turret thrown off at the death (Zod's turret toss), while it flies or lies on its own; null when none.</summary>
        internal Transform RamRestTossed => tossed;
        /// <summary>A physics body flies this wreck now (the DV17 flip, a fallen platform): the rest rule leaves it to PhysX.</summary>
        internal bool RamRestPhysics => wreckBody && !wreckBody.isKinematic;
        /// <summary>It is a wreck that is still drawn (not sinking away at the end of its life, not hidden).</summary>
        internal bool RamRestWreck => death == Death.Wreck && !fading && gameObject.activeInHierarchy;

        void RamRestAtDeath() => OzRammedWreckRest.AtDeath(this);
    }
}
