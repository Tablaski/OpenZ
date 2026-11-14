// OpenZ (GPL-3.0) - version 1.3, level 7, kit LV5: a fort cannon piece that has melted away stops burning.
namespace OpenZ
{
    public partial class OzFortGunBreakup
    {
        /// <summary>From OzLavaMelt, when the piece has gone under: its fire and its smoke end with it.</summary>
        public void OzDouse() { burnUntil = 0f; }
    }
}
