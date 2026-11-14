using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// tracks under its suspension node, the light tank (v1 and v2) hangs its belts (Track_Left / Track_Right) and its road wheels
    /// (TankWheel_N) on the model root BESIDE the suspension node. So VehicleMotion's suspension bounce never moved them, but the whole
    /// model's drive wobble and the shot's rock did (they turn the root). The planted gear (View/UnitView.GroundGear.cs) now takes the
    /// running gear wherever it hangs under the vehicle: under the suspension node (the heavy) or beside it (the light tank); never a node
    /// the suspension itself hangs under (the model root, the "Model" node). The light tank v2's dark side plates behind the road wheels
    /// ride on their belts (the v2 build puts them under the belt nodes), so they stay planted too.
    /// </summary>
    public partial class VehicleMotion
    {
        /// <summary>GearNodes: a track or a wheel node that belongs to the running gear: under the suspension node, or under the vehicle beside
        /// it (not one of the suspension's own parents).</summary>
        bool GearUnder(Transform t) => t && body && (t.IsChildOf(body) || (t.IsChildOf(transform) && !body.IsChildOf(t)));
    }
}
