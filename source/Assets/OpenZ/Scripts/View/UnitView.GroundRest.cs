namespace OpenZ
{
    /// <summary>1.3 item 9: what the sunk watch (View/OzSunkWatch.cs) reads of a wreck: sinking away at the end of its life (on purpose),
    /// and whether it is a flipped hull (DV17, a physics body; View/WreckGroundRest.cs).</summary>
    public partial class UnitView
    {
        public bool WreckSinking => fading;
        public bool FlippedWreck => flipWreck;
    }
}
