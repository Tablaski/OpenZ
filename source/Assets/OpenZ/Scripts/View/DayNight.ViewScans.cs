// OpenZ 1.4 speed-up 4 (View/OzViewScans.cs): every DayNight in its registry, so AudioDirector's ambience finds it, or finds none, without
// a search of the whole scene (the Unit editor has none and asked for it every frame). Awake runs even for a disabled component, as the
// search finds a disabled one too; OnDestroy runs when the object goes.
namespace OpenZ
{
    public partial class DayNight
    {
        void Awake() { OzLiveSet<DayNight>.Add(this); }
        void OnDestroy() { OzLiveSet<DayNight>.Remove(this); }
    }
}
