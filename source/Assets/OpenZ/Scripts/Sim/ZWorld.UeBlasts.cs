using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// the playground's entry for a test blast. It adds ONE ordinary missile of the world's own list (neutral: both teams alike, no
    /// attacker, no target), so the damage is the game's normal blast: ProcessMissileDamage with Zod's fall-off (damage x (1 - distance /
    /// radius)), the same cover, knock-down, box-level and platform rules as any shell, and the usual MissileExploded event (the view
    /// draws the kit's own explosion on it, View/OzUeBlasts.cs). No new rule, no switch, no DV row: nothing here runs in a level or a
    /// match (only the Unit editor's tiles call it), and what it queues is a missile the rules already know.
    /// Integers only; the burst time is whole rule ticks from now (the half tick as the NUKE's wave: no float slip). Added between ticks
    /// (the view's drop), straight to the live list like <see cref="PlaygroundNuke"/>.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>Checks: the test blasts queued so far in this world.</summary>
        public int PlaygroundBlasts;

        /// <summary>One neutral blast at (x, y) px: radiusPx (8 px a metre), damage = the HP at its centre, bursting in the
        /// afterTicks-th rule tick from now (1 = the next tick).</summary>
        public void PlaygroundBlast(int x, int y, int radiusPx, int damage, int afterTicks)
        {
            if (radiusPx <= 0 || damage <= 0) return;
            if (afterTicks < 1) afterTicks = 1;
            if (damage > ZConst.MAX_UNIT_HEALTH) damage = ZConst.MAX_UNIT_HEALTH;
            missiles.Add(new ZMissile { x = x, y = y, radius = radiusPx, damage = damage, team = Team.Neutral, explodeTime = Time + (afterTicks - 0.5) * ZConst.TICK });
            PlaygroundBlasts++;
        }
    }
}
