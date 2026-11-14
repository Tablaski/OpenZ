// OpenZ (GPL-3.0) - version 1.3, level 7, kit LV5: a shuttle piece that has melted away stops burning.
namespace OpenZ
{
    public sealed partial class OzShuttleBreakup
    {
        /// <summary>From OzLavaMelt, when the piece has gone under: its fire, its smoke and its smoulder end with it. The fire's crackle ends with the last flame, as ever.</summary>
        public void OzDouse() { burnUntil = 0f; smoulderUntil = 0f; }
    }
}
