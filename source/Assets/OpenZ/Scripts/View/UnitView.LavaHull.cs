namespace OpenZ
{
    /// <summary>Level 7, kit LV5: a whole vehicle or gun wreck that stands on lava melts as one (OzLavaMelt). From that moment it no longer cooks off into parts and throws no spark
    /// fountain: its renderers are melting. Its fire burns on until it is under, then the view ends as a faded wreck ends. A turret still out on its own keeps the view alive until its own end.</summary>
    public partial class UnitView
    {
        public void LavaHullMelts()
        {
            if (death != Death.Wreck) return;
            breakAt = -1f; cookOff = false; sparked = true;
        }

        /// <summary>Its top went under.</summary>
        public void LavaHullGone()
        {
            OzDouseWreck();
            if (death == Death.Wreck && !tossed) Hide();
        }
    }
}
