// OpenZ (GPL-3.0)
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Version 1.5 final_fixes_r12: WHEN a call for help (the cry vo_under_attack_repeat, or the plain vo_under_attack of the same episode)
    /// may go out on Alan's radio voice.
    /// Cause: round 5 (help_line_r5, A4) let a watched episode keep its one cry up to 25 s, also after its leader fell, waiting for the radio
    /// (shut 30-60 s after any call, other voices first), and let it cry by the episode's START state (heavy fire and losing people). So the
    /// cry often came seconds after the squad was dead or the enemy was already beaten.
    /// Now a call goes out only while ALL of this holds, looked at every HelpLookEvery s and at the moment it is said:
    /// <list type="bullet">
    /// <item>the group still has a living member (the members known at the trouble + every member hit since; a squad whose leader fell counts);</item>
    /// <item>it was hit in the last HelpHitFresh s, and one of the enemies that hit it in the last HelpAttackerWindow s is still alive (the
    /// duel is not won);</item>
    /// <item>it goes out within HelpMaxWait s of the trouble (first look HelpFirstDelay s after the hit), else the episode ends unsaid (a watched
    /// one gives its gap back; the next hit may start a new one);</item>
    /// <item>a cry only while the fire is heavy NOW (not by the start state).</item>
    /// </list>
    /// A watched trouble = under heavy fire AND (losing people OR the hit unit under HelpLowHealth of its health: losing a duel). A watched call
    /// skips the radio's long shut gap (it has its own HelpWatchedGap) but never talks over another voice. One call per episode, then the
    /// group is quiet HelpGroupQuiet s (never twice in a row for the same fight). If the group falls while its call plays, the line fades out
    /// in HelpCutFade s.
    /// Counters, HelpRecords and the sound log lines "HELP said" / "HELP dropped: ..." / "HELP cut" are what the Play check counts. View only:
    /// the simulation is untouched.
    /// </summary>
    public partial class AudioDirector
    {
        public static float HelpFirstDelay = 0.25f, HelpLookEvery = 0.1f, HelpMaxWait = 1.5f, HelpHitFresh = 2.5f, HelpAttackerWindow = 3f,
                            HelpLowHealth = 0.35f, HelpGroupQuiet = 60f, HelpCutFade = 0.15f;
        /// <summary>Checks (since the game started): calls said; episodes ended unsaid, by reason (the group fell / every enemy on it fell / no
        /// more hits / the radio not free in time / other); lines cut because the group fell while they played; the last and the longest
        /// delay from the trouble to the call (s); calls said while the group or every enemy on it was already down (0 by construction).</summary>
        public static int HelpSaidR12, HelpDropGone, HelpDropWon, HelpDropQuiet, HelpDropLate, HelpDropOther, HelpCutR12, HelpSaidDead, HelpSaidWon;
        public static float HelpLastDelay = -1f, HelpMaxDelay;
        /// <summary>Checks: one line per call said: "t=..s group #id delay ..s members alive N enemies alive N watched ...", the newest last (200 kept).</summary>
        public static readonly List<string> HelpRecords = new List<string>();

        sealed class HelpSpeaking { public DistressEp ep; public AudioClip clip; public bool cutting; }
        HelpSpeaking helpSpeaking;

        static bool HelpGone(ZObject o) => o == null || o.Destroyed || o.KillAt >= 0;
        static bool HelpLow(ZObject v) => v != null && v.MaxHealth > 0 && v.Health < v.MaxHealth * HelpLowHealth;

        /// <summary>Distress: the group's members at the trouble (the hit unit, the leader, every unit that follows the leader).</summary>
        HashSet<int> HelpMembers(ZObject group, ZObject victim)
        {
            var m = new HashSet<int>();
            if (group != null) m.Add(group.Id);
            if (victim != null) m.Add(victim.Id);
            if (group != null && group.Kind != ZKind.Building && W != null)
                foreach (var o in W.Objects) if (o != null && o.Leader == group && !HelpGone(o)) m.Add(o.Id);
            return m;
        }

        int HelpMembersAlive(DistressEp d)
        {
            if (d == null || W == null) return 0;
            if (d.members == null) return HelpGone(W.GetAny(d.id)) ? 0 : 1;
            int n = 0;
            foreach (var id in d.members) if (!HelpGone(W.GetAny(id))) n++;
            return n;
        }

        int HelpAttackersAlive(int id, float now)
        {
            if (W == null || !pressure.TryGetValue(id, out var p)) return 0;
            var seen = new HashSet<int>();
            foreach (var h in p.hits) if (now - h.t <= HelpAttackerWindow && seen.Add(h.by) && !HelpGone(W.GetAny(h.by))) return 1;
            return 0;
        }

        /// <summary>UpdateDistress: why the episode ends now, or null (it goes on).</summary>
        string HelpEndWhy(DistressEp d, bool ordered, float now)
        {
            if (ordered) return "ordered: the player gave the group an order";
            if (!d.watched && Watching(d.pos)) return "camera: the player looks at the fight";
            if (HelpMembersAlive(d) == 0) return "gone: the group has fallen";
            if (now - d.lastHit > HelpHitFresh) return "quiet: no hit for " + (now - d.lastHit).ToString("0.0") + " s";
            if (HelpAttackersAlive(d.id, now) == 0) return "won: every enemy that hit the group is down";
            if (d.level == 0 && now - d.first > HelpMaxWait) return "late: the radio was not free within " + HelpMaxWait.ToString("0.0") + " s";
            return null;
        }

        /// <summary>UpdateDistress: the episode ends (after its one call, or unsaid with the reason).</summary>
        void HelpEpisodeEnd(DistressEp d, string why, float now)
        {
            bool said = d.level > 0;
            distressQuiet[d.id] = now + (said ? HelpGroupQuiet : d.watched ? 0f : 5f);
            distressCalmUntil = now + (said ? 12f : 0.5f);
            if (said) return;
            if (d.watched && !float.IsNaN(d.gapFrom)) lastWatchedHelp = d.gapFrom;                       // a watched call that never went out gives its gap back
            why = why ?? "other";
            if (why.StartsWith("gone")) HelpDropGone++; else if (why.StartsWith("won")) HelpDropWon++; else if (why.StartsWith("quiet")) HelpDropQuiet++;
            else if (why.StartsWith("late")) HelpDropLate++; else HelpDropOther++;
            OzSoundLog.Add("dir", "vo_under_attack", null, "HELP dropped: group #" + d.id + (d.watched ? " (watched)" : "") + ", " + why);
        }

        /// <summary>UpdateDistress: the call went out on Alan's alert voice: counted, and watched while it plays (cut if the group falls).</summary>
        void HelpSaid(DistressEp d, string id, float now)
        {
            int alive = HelpMembersAlive(d), enemies = HelpAttackersAlive(d.id, now);
            HelpSaidR12++; if (alive == 0) HelpSaidDead++; if (enemies == 0) HelpSaidWon++;
            HelpLastDelay = now - d.first; if (HelpLastDelay > HelpMaxDelay) HelpMaxDelay = HelpLastDelay;
            string rec = "t=" + now.ToString("0.00") + "s group #" + d.id + " " + id + " delay " + HelpLastDelay.ToString("0.00") + "s members alive " + alive
                       + " enemies alive " + enemies + " last hit " + (now - d.lastHit).ToString("0.00") + "s ago" + (d.watched ? " watched" : "");
            HelpRecords.Add(rec); if (HelpRecords.Count > 200) HelpRecords.RemoveAt(0);
            OzSoundLog.Add("dir", id, null, "HELP said: " + rec);
            helpSpeaking = alanAlert != null && alanAlert.src && alanAlert.src.isPlaying ? new HelpSpeaking { ep = d, clip = alanAlert.src.clip } : null;
        }

        /// <summary>Every frame (UpdateDistress): the group of the call speaking now fell: the line fades out in HelpCutFade s.</summary>
        void HelpCutTick()
        {
            var c = helpSpeaking; if (c == null) return;
            var v = alanAlert;
            if (v == null || !v.src || !v.busy || !v.src.isPlaying || v.src.clip != c.clip)
            {
                if (c.cutting && v != null) v.fade = v.fadeTarget = 1f;
                helpSpeaking = null; return;
            }
            if (!c.cutting)
            {
                if (HelpMembersAlive(c.ep) > 0) return;
                c.cutting = true; HelpCutR12++;
                OzSoundLog.Add("dir", "vo_under_attack", null, "HELP cut: group #" + c.ep.id + " fell while its call played (fade " + HelpCutFade.ToString("0.00") + " s)");
            }
            v.fade = Mathf.MoveTowards(v.fade, 0f, Time.unscaledDeltaTime / Mathf.Max(0.02f, HelpCutFade));
            Apply(v);
            if (v.fade <= 0.001f) { v.src.Stop(); v.busy = false; v.fade = v.fadeTarget = 1f; helpSpeaking = null; }
        }
    }
}
