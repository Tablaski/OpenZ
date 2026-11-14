using UnityEngine;

namespace OpenZ
{
    public partial class RoundHatch
    {
        /// <summary>Heavy tank v2 (1.2 item 110): its two round cupolas are part of the model. The real one (left, the ladder side) has its
        /// lid on heavy_HatchHinge, a dark well inside its collar and the frame node heavy_v2_HatchFrame 0.36 m under the collar top (the
        /// heavy spec's top), so RimWorld is the collar rim and BoardingView's commander stands in that hatch. The other cupola is
        /// decorative and stays shut. Nothing is built and nothing is removed; the hinge's rest is kept as for v1.</summary>
        bool UseModelled(Transform root)
        {
            string frameNode = spec == null ? null : spec.prefix == "heavy_" ? OzHeavyTank.FrameNode : OzV2Units.FrameNodeFor(spec.prefix);   // 1.2 v2 units: the light tank v2's modelled hatch too (View/OzV2Units.cs)
            if (frameNode == null || !root) return false;
            Transform frame = null, hinge = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!frame && t.name == frameNode) frame = t;
                else if (!hinge && t.name == spec.prefix + "HatchHinge") hinge = t;
            }
            if (!frame || !hinge) return false;
            Frame = frame; Hinge = hinge; hingeRest = hinge.localRotation;
            return true;
        }
    }
}
