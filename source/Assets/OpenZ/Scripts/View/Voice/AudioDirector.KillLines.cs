// OpenZ (GPL-3.0) - version 1.2, item 63: the robots' kill lines ("Smokin'!" among the kill quips, the "boom" cheers after a good kill)
// heard at their moment again (View/AudioDirector.cs OnDeath calls KillQuip, BoomTick asks CheerMoment; the rules: OzLineBag and
// OzCheerRule in View/Voice/OzOrderKillRules.cs).
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Version 1.2 item 63.
    /// - "Smokin'!" (alan_smokin_1 / _2) is one of the 11 lines of the kill quip (vo_kill_quip, Zod's A-portrait kill lines: "Good hit!",
    ///   "Nice one!", "Oh yeah!", "Gotcha!", "Smokin'!", "Cool!", "Wipe out!" and four more), said when one of our units kills an enemy unit
    ///   that it was not ordered to attack ("Target destroyed." then), the robots' channels quiet. The quip drew its 22 TAKES in a
    ///   shuffled round (Pick's bag): "Smokin'!" came as often as now (2 takes of 22 = 1 quip in 11), but a line could stay away for up
    ///   to about 40 quips and two takes of one line could come back to back. Now each LINE of the pool comes once before any comes again
    ///   (a random take of it), so two "Smokin'!" are at most 21 quips apart; a quip that could not be said (a voice speaks) keeps its
    ///   line for the next kill.
    /// - The "boom" lines (vo_boom: "Boom!", "Knockout!", "Lights out!", "Rest in pieces!", "Gotcha!" ...; vo_boom_brad for the second
    ///   voice) are the cheer after a good kill: an enemy vehicle or gun destroyed (or its driver shot out) on screen, at most one cheer
    ///   per BoomGap s, BoomChance of those kills. Such a kill skips its quip and waits for the cheer, but the cheer came only once the
    ///   fight there was over (no armed enemy near the killer, the killer attacking nothing) within BoomWait s, else it was dropped: in
    ///   a battle the next enemy is always near, so the kill got no line at all. Now the cheer takes a lull as before, or else comes
    ///   BoomLull s after the kill whatever the fight, at the first moment the voices are quiet (still at most BoomWait s after the kill,
    ///   the killer alive and on screen).
    /// Pacing stays as it was: the quip's cooldown, never over another robot line, the cheer's gap and chance; ROBOT CHATTER LESS and
    /// MINIMAL still leave both out (that setting's own rule). View only: the simulation is untouched.
    /// </summary>
    public partial class AudioDirector
    {
        public const string KillQuipEvent = "vo_kill_quip", SmokinLine = "Smokin'!", BoomLine = "Boom!";
        /// <summary>Seconds after the kill when the cheer comes (run 10 1d: 0.6 s, the blast first; was 2.5 s, waiting for a lull).</summary>
        public static float BoomLull = 0.6f;
        readonly OzLineBag quipBag = new OzLineBag();
        /// <summary>Checks: kill quips asked / said / not said (a voice spoke, the cooldown, the chatter setting), of them "Smokin'!";
        /// cheer moments found in a lull / after BoomLull s while the fight went on.</summary>
        public static int QuipAsked, QuipSaid, QuipNotSaid, Smokins, CheerLull, CheerLate;
        /// <summary>Checks: the last kill quip lines said, oldest first (at most 32).</summary>
        public static readonly System.Collections.Generic.List<string> QuipLines = new System.Collections.Generic.List<string>();
        float cheerMomentFor = -1f;

        /// <summary>OnDeath: the kill quip, its line the next one of the pool's line bag.</summary>
        void KillQuip()
        {
            var d = Def(KillQuipEvent); QuipAsked++;
            string line = d != null && d.lines != null ? quipBag.Peek(d.lines, () => Random.value) : null;
            int before = Plays.TryGetValue(KillQuipEvent, out var n0) ? n0 : 0;
            Say(KillQuipEvent, line != null ? "=" + line : null);
            if ((Plays.TryGetValue(KillQuipEvent, out var n1) ? n1 : 0) <= before) { if (line != null && KillQuipWaits(line)) return; QuipNotSaid++; return; }   // 1.4 radar_voice_fix: a quip that waits for the voice floor is taken from the bag when it starts
            QuipSaid++;
            if (line == null) return;
            quipBag.Take(line);
            if (line == SmokinLine) Smokins++;
            QuipLines.Add(line); if (QuipLines.Count > 32) QuipLines.RemoveAt(0);
        }

        /// <summary>BoomTick: the cheer may come now (a lull in the fight, or BoomLull s after the kill); the voices are asked after.</summary>
        bool CheerMoment(float now, bool fight)
        {
            bool lull = !fight && boomQuietSince >= 0f && now - boomQuietSince >= BoomQuiet;
            if (!OzCheerRule.Moment(now, boomAt, fight, boomQuietSince, BoomQuiet, BoomLull)) return false;
            if (cheerMomentFor != boomAt)
            {
                cheerMomentFor = boomAt;
                if (lull) CheerLull++; else CheerLate++;
                OzSoundLog.Add("dir", "vo_boom", null, lull ? "cheer moment: a lull in the fight" : "cheer moment: " + BoomLull.ToString("0.0") + " s after the kill, the fight goes on");
            }
            return true;
        }
    }
}
