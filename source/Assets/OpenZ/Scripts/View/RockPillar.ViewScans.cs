// OpenZ 1.4 speed-up 4 (View/OzViewScans.cs): every RockPillar in its registry, so RockSeeThru counts the rocks every 3 s without a search
// of the whole scene. Awake runs even for a disabled component, as the search counts a disabled one too; OnDestroy runs when it goes.
namespace OpenZ
{
    public partial class RockPillar
    {
        void Awake() { OzLiveSet<RockPillar>.Add(this); }
        void OnDestroy() { OzLiveSet<RockPillar>.Remove(this); }
    }
}
