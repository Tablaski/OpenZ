// OpenZ (GPL-3.0) - 1.3, open_fixes_2nov wreck_toss_landing: the blast wave shoves only what was there when its front came by.
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The wave's shove (ScanBodies every 0.5 s, PushBodies) gave every free body inside its front the full push (up to 13 m/s out + 6 m/s up)
    /// the first time it saw it, for as long as the wave lived (radius under 150 m, 8 s). A body made after the front had passed its place got
    /// that push too: the thrown fort cannon lands 3 s after the blast and breaks up, and 0.1-0.5 s later all its pieces were thrown 12-15 m/s
    /// away from the fort at once (on the level 7 red win toward the scene's camera, so the win scene left them out of its shot in one frame:
    /// the why they disappear instantly). The same for every wreck piece, turret seat or body made after the front.
    /// <see cref="SkipLateBodies"/>: a body seen for the first time, not of the fort's own collapse, whose place the front passed more than
    /// <see cref="LateGrace"/> s before the previous scan (so it was made after the front had gone by) is never shoved. Everything that was there
    /// when the front came (a body of the blast, a robot it kills, the fort's own concrete and its nudge) is shoved exactly as before. View only.
    /// </summary>
    public sealed partial class OpenZFortShockwave
    {
        public static bool SkipLateBodies = true;
        /// <summary>A body counts as made after the front when the front passed its place this long before the previous scan (game s).</summary>
        public static float LateGrace = 1f;
        /// <summary>Play check: bodies left alone as made after the front, the last one.</summary>
        public static int LateSkipped; public static string LastLate = "-";
        float lastScanAge = -1f;

        /// <summary>ScanBodies: true = this body came after the front (it is marked as done and never shoved).</summary>
        bool LateBody(Rigidbody rb)
        {
            if (!SkipLateBodies || lastScanAge < 0f || !rb) return false;
            if (rb.GetComponent<BuildingBreaker.Piece>() || rb.GetComponent<BuildingBreaker.BigPart>()) return false;   // the fort's own collapse: its nudge as before
            var d = rb.worldCenterOfMass - centre; d.y = 0f; float dist = d.magnitude;
            if (dist > radius) return false;                                                                           // the front has not reached it yet
            float after = lastScanAge - TimeAt(dist);
            if (after <= LateGrace) return false;
            pushed.Add(rb); LateSkipped++;
            LastLate = rb.name + " at " + dist.ToString("0") + " m, made at least " + after.ToString("0.0") + " s after the front passed";
            return true;
        }
    }
}
