using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>1.2 item 146: what the planted running gear (View/UnitView.GroundGear.cs) needs from the vehicle feel: the suspension node
    /// and its rest pose, the running gear under it (each track renderer and each spun wheel) and this frame's suspension motion scaled.</summary>
    public partial class VehicleMotion
    {
        /// <summary>The suspension node (TankSuspension and the like) or null.</summary>
        public Transform Body => body;

        /// <summary>The suspension node's rest pose (its local place and turn before any motion). False when the model has none.</summary>
        public bool BodyRest(out Vector3 pos, out Quaternion rot) { pos = bodyPos; rot = bodyRot; return body != null; }

        /// <summary>The running gear under the suspension node: every track renderer's node (spun = false) and every wheel this component
        /// spins (spun = true). A node under another listed node is left out (it rides with that one).</summary>
        public void GearNodes(List<(Transform t, bool spun)> into)
        {
            into.Clear();
            if (!body) return;
            foreach (var (r, _) in tracks) if (r && r.transform != body && GearUnder(r.transform)) into.Add((r.transform, false));   // 1.2 v2 units: under the suspension or beside it (View/VehicleMotion.GearOutside.cs)
            foreach (var w in wheels) if (w.t && w.t != body && GearUnder(w.t)) into.Add((w.t, true));
            LinkGearNodes(into);                                                                       // 1.5 DV180 / DV181: the link-track side roots (View/VehicleMotion.Links.cs)
            for (int i = into.Count - 1; i >= 0; i--)
                for (int j = 0; j < into.Count; j++)
                    if (j != i && into[i].t != into[j].t && into[i].t.IsChildOf(into[j].t)) { into.RemoveAt(i); break; }
        }

        /// <summary>This frame's suspension motion (bounce, roll, squat, lean, the barrels' kick: Step writes it whole every frame) scaled by
        /// k about the rest pose.</summary>
        public void ScaleBody(float k)
        {
            if (!body) return;
            body.localPosition = bodyPos + (body.localPosition - bodyPos) * k;
            body.localRotation = bodyRot * Quaternion.SlerpUnclamped(Quaternion.identity, Quaternion.Inverse(bodyRot) * body.localRotation, k);
        }
    }
}
