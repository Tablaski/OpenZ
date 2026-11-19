// OpenZ (GPL-3.0)
using System.Collections;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only: no Sim file, no rule, nothing the rules read.
    ///
    /// Before: when a fort falls and the nuke intro holds the bomber (OpenZVoiceV3.PlanNuke: the announcer's explanation, then the talking
    /// scene: the blue WIN NUKE scene "BEHOLD! My World-Domination-inator!" or, on the red side, the commander's red-button transmission),
    /// OpenZEnding.HoldFortToss keeps the beaten fort's top cannon on the roof until the bomb lands (endings_v1 / owner item 120), and
    /// FortGunView.LateUpdate returns early for a destroyed fort: the cannon stands still, not aiming, not firing, for 8-16 s of talk.
    ///
    /// Now: in OpenZVoiceV3.NukeIntro, after the announcer's line and before the talking scene opens (one inserted line), OutroBlastBeat
    /// blows every cannon still standing on a fallen fort off with the 1.5 megacannon_blast (StartToss: the toss, run 9's blast, the toss
    /// whoosh and DeathBlast: the massive exp2 fireball, the camera kick, the heavy boom and the rumble), then keeps a quiet beat of
    /// <see cref="OutroLeadSeconds"/> s so the boom has rung out before the scene's window opens (its own preroll follows): the blast is
    /// never under a sentence. With no announcer line first (the intro set to Zod only, her clip missing) the beat waits for the ending's
    /// camera glide to reach the fort (OpenZEnding.GlideSeconds, at most <see cref="OutroGlideWaitMax"/> s). PlanNuke adds the beat's
    /// length to the bomber's watchdog hold (<see cref="OutroPlanSeconds"/>), so the watchdog never launches over it.
    ///
    /// A cannon shot off earlier (Fort.FortGunDestroyed) is already gone: nothing happens, no wait. Our own fort falling (no intro, the
    /// bomber flies at once, the gun goes with the impact about 3.7 s later), a strike-off ending (the gun goes with the fort at once) and
    /// the gate battery (no intro) are as before. Switch: <see cref="OutroBlastOn"/> (off = exactly as before this kit).
    /// </summary>
    public partial class FortGunView
    {
        /// <summary>Switch: a beaten fort's standing cannon is blown off before the ending's talking scene (on).</summary>
        public static bool OutroBlastOn = true;
        /// <summary>The quiet beat after the blast before the scene's window opens (s); the longest wait for the camera glide (s, only with
        /// no announcer line first); the gap between two cannons on two fallen forts (s).</summary>
        public static float OutroLeadSeconds = 1.0f, OutroGlideWaitMax = 2.2f, OutroStagger = 0.3f;
        /// <summary>Proof counters: beats run, cannons blown off, beats that found no standing cannon (shot off earlier); the last blast's
        /// time and the seconds from it to the end of the beat (the scene opens after that, plus its preroll).</summary>
        public static int OutroBeats, OutroBlasts, OutroNoneStanding;
        public static float LastOutroBlastAt = -100f, LastOutroLead = -1f;
        /// <summary>The Play proof: a beat ran and it blew a cannon off (or none stood).</summary>
        public static bool OutroBlastOk => OutroBeats > 0 && (OutroBlasts > 0 || OutroNoneStanding > 0) && (OutroBlasts == 0 || LastOutroLead >= OutroLeadSeconds - 0.05f);

        /// <summary>StartToss called by the beat: DeathBlast plays on a fallen fort too (FortGunView.DeathBlast.cs).</summary>
        bool outroBlast;

        static bool StandsOnFallen(FortGunView v) => v && v.Bound && !v.tossed && v.yaw && v.Fort != null && FortFallAtGameOver.Fallen(v.Fort);

        static int StandingOnFallen()
        {
            int n = 0;
            for (int i = 0; i < all.Count; i++) if (StandsOnFallen(all[i])) n++;
            return n;
        }

        /// <summary>OpenZVoiceV3.PlanNuke, on the fort's fall: the seconds the beat will add to the intro (0 = no cannon stands).</summary>
        public static float OutroPlanSeconds(bool rachelFirst)
        {
            if (!OutroBlastOn) return 0f;
            int n = StandingOnFallen();
            if (n == 0) return 0f;
            return OutroLeadSeconds + (rachelFirst ? 0f : OutroGlideWaitMax) + OutroStagger * (n - 1) + 0.1f;
        }

        /// <summary>OpenZVoiceV3.NukeIntro, after the announcer's line, before the talking scene: blow every cannon still standing on a fallen
        /// fort off (the 1.5 megacannon_blast), then the quiet beat.</summary>
        public static IEnumerator OutroBlastBeat(bool rachelFirst)
        {
            OutroBeats++;
            if (!OutroBlastOn) yield break;
            if (StandingOnFallen() == 0) { OutroNoneStanding++; yield break; }
            float t0 = Time.time;
            while (OpenZEnding.Running && OpenZEnding.RealSinceFall < OpenZEnding.GlideSeconds && Time.time - t0 < OutroGlideWaitMax) yield return null;   // the camera on the fort first
            int n = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var v = i < all.Count ? all[i] : null;
                if (!StandsOnFallen(v)) continue;
                if (n > 0) { float s = Time.time; while (Time.time - s < OutroStagger) yield return null; if (!StandsOnFallen(v)) continue; }
                v.outroBlast = true;
                try { v.StartToss(); } finally { v.outroBlast = false; }
                n++; OutroBlasts++; LastOutroBlastAt = Time.time;
                Debug.Log($"[OpenZ] fort #{v.Fort.Id} ({v.Fort.Owner}) fell: its mega cannon blown off before the ending scene (blast {DeathBlasts}, {(rachelFirst ? "after the announcer" : "after the camera glide")})");
            }
            if (n == 0) { OutroNoneStanding++; yield break; }
            float b = Time.time;
            while (Time.time - b < OutroLeadSeconds) yield return null;                                // the boom rings out: the scene's first word never lands on it
            LastOutroLead = Time.time - LastOutroBlastAt;
        }

        /// <summary>One line for the proof.</summary>
        public static string OutroBlastReport() =>
            $"outro blast: on {OutroBlastOn}, beats {OutroBeats}, cannons blown off {OutroBlasts}, none standing {OutroNoneStanding}, last at t {LastOutroBlastAt:F2}, beat after it {LastOutroLead:F2} s (want {OutroLeadSeconds:F2})";
    }
}
