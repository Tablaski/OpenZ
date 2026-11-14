// OpenZ (GPL-3.0) - 1.3, open_fixes_2nov wreck_toss_landing update 1: the fort cannon's break-up pieces never fly off faster than they can be followed.
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The break-up (item 205) launches each piece with a share of the cannon's touch-down speed along its flight (14.3-15.0 m/s in all, 4-7
    /// m/s of it flat), a scatter out of the impact (up to 5.8 m/s for a plate) and a lift (up to 4.9 m/s), and the upper pieces of a cannon
    /// lying on its side fall 3-5 m to the ground after it: the fastest piece of every break-up reached 9.5-11.0 m/s in its first 1.5 s
    /// (the check's P2, bar 10 m/s; the staged check has no blast wave, so this is the break-up's own throw). <see cref="PieceSpeedCap"/>: each
    /// piece's body may not go faster than 9 m/s for its first <see cref="PieceSpeedCapSeconds"/> s (the physics' own top speed, so the
    /// launch, a fall, a knock and the push out of an overlap are all held to it, in their own direction); after that its top speed is what
    /// it was. The look is the same: only the few fastest pieces lose up to a fifth of their speed. View only.
    /// Switch: <see cref="PieceSpeedCapOn"/> (off = as before).
    /// </summary>
    public partial class OzFortGunBreakup
    {
        public static bool PieceSpeedCapOn = true;
        public static float PieceSpeedCap = 9f, PieceSpeedCapSeconds = 2f;
        /// <summary>Play check: pieces made with the cap.</summary>
        public static int SpeedCapped;
        bool speedCapOn; float speedCapWas;

        /// <summary>Build, once the piece's body is made: its top speed for the first seconds.</summary>
        void SpeedCapBegin()
        {
            if (!PieceSpeedCapOn || !rb) return;
            speedCapWas = rb.maxLinearVelocity; rb.maxLinearVelocity = PieceSpeedCap; speedCapOn = true; SpeedCapped++;
        }

        /// <summary>Update, at PieceSpeedCapSeconds: the body's own top speed back.</summary>
        void SpeedCapEnd()
        {
            speedCapOn = false;
            if (rb) rb.maxLinearVelocity = speedCapWas;
        }
    }
}
