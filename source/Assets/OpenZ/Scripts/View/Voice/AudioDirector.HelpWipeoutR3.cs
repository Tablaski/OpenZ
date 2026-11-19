// OpenZ (GPL-3.0)
// The 1.3 item 18 help cries and wipeout cheers heard in a match.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Version 1.5 voice_lines_check_r3. The 1.3 item 18 lines were wired (clips in Resources, keys in the table, import right) but almost never
    /// spoke: counted on real two-bot matches (kit proof/voice_counts.md), "For Christ's sake / For crying out loud, do something!" came about
    /// once in 77 minutes. Why: (1) the help cries share AlertGate's "help" group with the on-screen hurt yelps (vo_hurt, vo_low_health): every
    /// "Ow!" the player sees shut the radio's cries for 30 s; (2) the cries only came from fights OFF screen, and the owner watches his fights;
    /// (3) the 11 new lines sit in a bag of 46 lines, so one line comes about once per 46 cries; (4) the wipeout lines are 6 of 19 / 4 of 10
    /// takes of the boom cheer, picked at random.
    /// Now: (1) AlertGate gives the yelps their own group "hurt" (View/AlertGate.cs); (2) a group the player WATCHES under heavy fire that is
    /// losing people (a member lost in the last HelpLossWindow s) may cry once, at most one such cry per HelpWatchedGap s, the radio's own
    /// limits on top (30-60 s shut after any cry, at most 2 radio lines a minute, 8 s apart); (3) the FIRST cry of an episode under heavy fire
    /// while losing (or watched) takes the next line of the 1.3 bag; each match's first round of that bag opens with the owner's pair (item 18:
    /// The cheer stays at its own moment (a medium / big vehicle or gun kill on camera, its gap and
    /// chance unchanged); the 1st, 3rd, 5th ... cheer of a match says the next wipeout line of the killer's voice (Alan's or Brad's bag),
    /// the 2nd, 4th ... one of the older takes ("Boom!", "Gotcha!" ...): half and half.
    /// View only: the simulation is untouched. Sound log: "HELP watched", "HELP 1.3 line asked", "WIPEOUT cheer".
    /// </summary>
    public partial class AudioDirector
    {
        public static float HelpWatchedGap = 50f, HelpLossWindow = 10f;
        /// <summary>(5) item 21's win line, never asked before (the end sequence asks only filtered win lines): the closing robot line of about
        /// one victory in VictoryInstalledChance (FxV2/OpenZVictory.cs); item 21's loss line joined the second loss line's rotation there.</summary>
        public const string VictoryInstalledLine = "Victory installed. Party mode on. Cool.";
        public static float VictoryInstalledChance = 0.3f;
        /// <summary>The owner's two lines: each match's first round of the 1.3 bag opens with them.</summary>
        public static readonly string[] HelpOwnerPair = { "For Christ's sake, do something!", "For crying out loud, do something!" };
        /// <summary>Checks: watched cries let through, 1.3 lines asked, wipeout cheers said.</summary>
        public static int HelpWatchedCount, HelpV13Asked, WipeSaid;
        const string HelpV13Key = "vo_under_attack_repeat#v13";
        readonly List<string> helpOwnerFirst = new List<string>();                                  // per match (a new director per level)
        bool helpOwnerDealt;
        float lastWatchedHelp = -1000f;
        const string BoomOlderFilter = "!wipe";                                                    // LineMatch: no "wipe" in the line = an older cheer
        int cheersR3; string wipeLineR3;                                                            // cheers said this match; the wipeout line asked now
        readonly Dictionary<string, List<string>> wipeBags = new Dictionary<string, List<string>>();

        /// <summary>The group lost a member in the last HelpLossWindow s (NoteLoss).</summary>
        bool HelpLosing(int id) => pressure.TryGetValue(id, out var p) && Time.unscaledTime - p.lastLoss < HelpLossWindow;

        /// <summary>1.5 help_line_r5 (FAILS A4, 14 Nov): the episode's own state at its start (DistressEp.heavyLoss): under heavy fire AND losing
        /// people. Its cry may wait for the radio (shut 30-60 s after any cry, other voices first); its line goes by this, not by the fire then.</summary>
        bool HelpHeavyLossR5(ZObject group) => group != null && Heavy(group.Id) && HelpLosing(group.Id);

        /// <summary>Distress: the player watches this group; true = it may still cry (heavy fire, losing people, the watched gap free).</summary>
        bool HelpWatchedOk(ZObject group, float now, ZObject victim = null)
        {
            if (group == null || group.Kind == ZKind.Building || now - lastWatchedHelp < HelpWatchedGap || !Heavy(group.Id) || !(HelpLosing(group.Id) || HelpLow(victim))) return false;   // 1.5 final_fixes_r12 (A41): losing people OR the hit unit low on health (losing a duel)
            lastWatchedHelp = now; HelpWatchedCount++;
            OzSoundLog.Add("dir", "vo_under_attack_repeat", null, "HELP watched: group #" + group.Id + " under heavy fire and " + (HelpLosing(group.Id) ? "losing people" : "low on health") + " on screen");
            return true;
        }

        /// <summary>UpdateDistress: the line of this call from the 1.3 bag, or null (the older bag of all lines then). 1.5 help_line_r5: the first cry of an
        /// episode that BEGAN under heavy fire while losing people (or is so now) takes the 1.3 bag, however long it waited for the radio.</summary>
        string HelpLineR3(string id, SoundEventDef def, DistressEp d, bool heavy)
        {
            if (id != "vo_under_attack_repeat" || def == null || def.clips == null || d.level > 0 || !(d.heavyLoss || d.watched || (heavy && HelpLosing(d.id)))) return null;   // 1.5 help_line_r5 (FAILS A4): the episode's own state, not the fire when the radio opens
            var lines = new List<string>();
            foreach (var c in def.clips) if (c != null && c.res != null && c.res.Contains("_v13_") && !string.IsNullOrEmpty(c.line) && !lines.Contains(c.line)) lines.Add(c.line);
            if (lines.Count == 0) return null;
            if (!helpOwnerDealt)
            {
                helpOwnerDealt = true;
                foreach (var l in HelpOwnerPair) if (lines.Contains(l)) helpOwnerFirst.Insert(UnityEngine.Random.Range(0, helpOwnerFirst.Count + 1), l);
            }
            string line = helpOwnerFirst.Count > 0 ? helpOwnerFirst[0] : Radio.Peek(HelpV13Key, lines, false);
            if (line != null) { HelpV13Asked++; OzSoundLog.Add("dir", id, null, "HELP 1.3 line asked: " + line); }
            return line;
        }

        /// <summary>UpdateDistress: a cry went out; its line leaves the 1.3 bag too.</summary>
        void HelpLinePlayedR3(string line)
        {
            if (line == null) return;
            if (helpOwnerFirst.Remove(line)) return;
            Radio.Played(HelpV13Key, line);
        }

        /// <summary>BoomTick: the filter of the cheer about to be said: the next wipeout line on the 1st, 3rd, 5th ... cheer of the match,
        /// else one of the older takes ("!wipe": no older take says "wipe"), so both kinds keep half of the cheers.</summary>
        string BoomFilterR3(string id)
        {
            wipeLineR3 = null;
            if (cheersR3 % 2 != 0) return BoomOlderFilter;
            var d = Def(id);
            if (d == null || d.clips == null) return null;
            if (!wipeBags.TryGetValue(id, out var bag)) wipeBags[id] = bag = new List<string>();
            if (bag.Count == 0)
            {
                foreach (var c in d.clips) if (c != null && c.res != null && c.res.Contains("_v13_") && !string.IsNullOrEmpty(c.line) && !bag.Contains(c.line)) bag.Insert(UnityEngine.Random.Range(0, bag.Count + 1), c.line);
                if (bag.Count == 0) return null;
            }
            wipeLineR3 = bag[0];
            return "=" + wipeLineR3;
        }

        /// <summary>BoomTick: the cheer was said: the next cheer takes the other turn; a wipeout line leaves its bag.</summary>
        void BoomSaidR3(string id)
        {
            cheersR3++;
            if (wipeLineR3 == null) return;
            WipeSaid++;
            if (wipeBags.TryGetValue(id, out var bag)) bag.Remove(wipeLineR3);
            OzSoundLog.Add("dir", id, null, "WIPEOUT cheer: " + wipeLineR3);
            wipeLineR3 = null;
        }
    }
}
