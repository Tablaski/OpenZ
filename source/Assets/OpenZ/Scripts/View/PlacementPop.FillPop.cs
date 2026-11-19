// OpenZ (GPL-3.0)
// stays';
namespace OpenZ
{
    /// <summary>
    /// 1.5 review R10 (view only): the scale pop of a vehicle or a gun at the START of every colour fill (View/OzVehicleColourFill.cs): a robot
    /// takes it (capture, boarding), the driver gets out, the driver is sniped. It is the snipe's own pop, <see cref="Sniped"/>'s jolt, the same
    /// code and the same numbers: <see cref="JoltStart"/> -> <see cref="JoltPeak"/> -> <see cref="JoltUnder"/> -> 1 over
    /// <see cref="JoltDuration"/> s (ease-out cubic up, smoothstep to the dip, ease-out to 1), uniform, about the vehicle's footprint centre at
    /// its ground height (so tracks and wheels never sink or float), drawn only while the cameras render (the game logic, picking and the crews
    /// see the real scale). Every vehicle and gun that fills (the big tanks too); not the shuttle (its capture has its own flash and ease).
    /// Before: only the snipe popped, because only CombatView's snipe called the pop; the colour fill kit (vehicle_colour_fill) started the
    /// fill alone. A fill whose pop already runs (the snipe's jolt of the same moment, a fill turned back half way) is not popped twice.
    /// Off (<see cref="FillPopOn"/> false) = the fill alone, as before.
    /// </summary>
    public partial class PlacementPop
    {
        public static bool FillPopOn = true;
        /// <summary>Counted proof: pops started with a fill; fills whose pop was already running.</summary>
        public static int FillPops, FillPopsShared;

        /// <summary>OzVehicleColourFill.Begin: a fill has just started on this vehicle / gun.</summary>
        public static void FillPop(UnitView v)
        {
            if (!FillPopOn || !v || v.Dying || v.Obj == null || v.Obj.IsShuttle) return;
            var t = v.transform;
            for (int i = 0; i < jobs.Count; i++) if (jobs[i].t == t) { FillPopsShared++; return; }   // the snipe's jolt (or this fill's) already runs
            if (StartPop(t, JoltStart, JoltPeak, JoltUnder, JoltDuration, false) != null) FillPops++;
        }
    }
}
