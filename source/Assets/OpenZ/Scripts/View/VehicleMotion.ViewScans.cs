// OpenZ 1.4 speed-up 4 (View/OzViewScans.cs): every VehicleMotion in its registry, so OzTreadSteady finds the vehicles every second without
// a search of the whole scene. VehicleMotion's own Awake (View/VehicleMotion.cs) calls ViewScansAwake first; OnDestroy is here.
namespace OpenZ
{
    public partial class VehicleMotion
    {
        void ViewScansAwake() { OzLiveSet<VehicleMotion>.Add(this); }
        void OnDestroy() { OzLiveSet<VehicleMotion>.Remove(this); }
    }
}
