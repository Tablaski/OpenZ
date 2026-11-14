namespace OpenZ
{
    /// <summary>
    /// 1.4 range_rings_option: the clock of one selection ring (View/AttackRangeRings.cs LateUpdate), plain C# so the kit's harness runs this very
    /// file off Unity. The ring is drawn at an age: its burst, hold and fade (AttackRangeRings.Timing) follow it, and the ring ends at
    /// expandTime + holdTime + fadeTime.
    ///  * Not kept (OPTIONS RANGE RINGS ON SELECTED UNITS = FADE, the default): the age is now - t0, as before, the same float sum.
    ///  * Kept (ALWAYS, the player's own ring): the age stops at the end of the hold (settled = expandTime + holdTime): full size, full strength,
    ///    for as long as the ring lives (its units leaving the selection still fade it out in dropFade, as before).
    ///  * A ring that was held when the player switched back to FADE: its fade starts now (t0 moved to now - settled), so it fades in fadeTime
    ///    as at the end of a normal hold, instead of vanishing at once.
    /// </summary>
    public static class OzRingHold
    {
        /// <summary>The age the ring is drawn at this frame; may move t0 (the switch back to FADE) and sets held.</summary>
        public static float Age(ref float t0, ref bool held, float now, bool keep, float settled)
        {
            if (keep)
            {
                float a = now - t0;
                if (a > settled) { held = true; return settled; }
                return a;
            }
            if (held) { held = false; t0 = now - settled; return settled; }
            return now - t0;
        }
    }
}
