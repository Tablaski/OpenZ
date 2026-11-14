using OpenZ.Sim;

namespace OpenZ
{
    /// <summary>
    /// as it is at that moment. The radar picture (OzRadarLineRule.cs): while a team owns at least one STANDING radar, the enemy robots,
    /// vehicles and guns are drawn on its minimap (UI/OpenZMinimap.cs, NB41) and the strike-group warning can be heard. So:
    /// <list type="bullet">
    /// <item>"RADAR ACTIVATED" (the HUD toast) and the radar-online lines (first / back online / plain) need a standing radar of the
    /// capturing team in the zone; a ruin gives none (it shows nothing). The announcer's rule is OzRadarLineRule.LineFor, the toast's
    /// <see cref="StandingRadarOf"/>: the same test.</item>
    /// <item>"Radar lost." only when the player's LAST standing radar was taken (the enemy dots leave his minimap): never for a zone whose
    /// radar is a ruin, never while another radar of his still stands ("Territory lost." covers those), never for a radar destroyed (that
    /// one is "Our radar has been destroyed!", sound_v3). And only for a picture that counted in OzRadarLineRule's sense (it was announced,
    /// stood at the level start or for HoldSec s): exactly the pictures after which the next capture says "Radar is back online.", so
    /// "lost" and "back online" always come as a pair, and a contested flag flipping every 0.2 s says neither.</item>
    /// </list>
    /// View only: reads the world, writes nothing into it. No engine types (a headless check can run it). The sim's FlagCaptured event
    /// still marks every zone with a radar (Sim/ZWorld.Rules.cs AwardZone, unchanged): the view decides what is true.
    /// </summary>
    public static class OzRadarTruth
    {
        /// <summary>The zone of flag `flagId` holds a standing radar that `team` owns (the toast and the radar-online lines).</summary>
        public static bool StandingRadarOf(ZWorld w, int flagId, Team team)
        {
            var f = w != null ? w.GetAny(flagId) : null;
            if (f == null) return false;
            foreach (var b in f.LinkedObjects) if (b != null && b.Type == "radar" && b.Owner == team && !b.Destroyed) return true;
            return false;
        }

        /// <summary>Does the loss of flag `flagId`'s zone (taken from `player` a moment ago) take the player's radar picture away?
        /// `why` says what decided it (the log and the Play check).</summary>
        public static bool RadarLostTrue(ZWorld w, int flagId, Team player, OzRadarLineRule rule, out string why)
        {
            var f = w != null ? w.GetAny(flagId) : null;
            if (f == null || rule == null) { why = "no flag / no radar rule"; return false; }
            bool taken = false, any = false;
            foreach (var b in f.LinkedObjects)
            {
                if (b == null || b.Type != "radar") continue;
                any = true;
                if (!b.Destroyed && b.Owner != player) { taken = true; break; }
            }
            if (!any) { why = "the zone has no radar"; return false; }
            if (!taken) { why = "the zone's radar is a ruin (it showed nothing)"; return false; }
            rule.Observe(w);
            if (rule.Covered(player)) { why = "another radar of ours still stands (the enemy dots stay on the minimap)"; return false; }
            if (!rule.Had(player)) { why = "the radar picture was too short to count (never announced, under " + OzRadarLineRule.HoldSec.ToString("0") + " s): the next radar line is the first-radar line"; return false; }
            why = "our last standing radar was taken: the enemy dots leave the minimap";
            return true;
        }
    }
}
