// OpenZ (GPL-3.0)
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 review R9: the TACTICAL PAUSE's freeze / unfreeze takes (View/OzTacticalPause.cs) on a source of their own instead of the shared
    /// one-shot pool.
    /// <para>Why the unfreeze could go unheard in the exe (the Unity editor's clip has it: tactical_pause_freeze_orders_L02.mp4, take a / b at
    /// 8.38 s, correlation 0.85): it is the only one of these sounds started from the pool at the very frame the freeze ends. The game's pause
    /// flag goes off in the same call, so in the next frames AudioListener.pause goes false and every world voice it held resumes at once,
    /// and the battle's first sim events ask the pool for voices. The pool hands out a voice whose source does not report isPlaying
    /// (AudioDirector.cs Grab, and the frame loop's release, which waits only while the listener is paused), steals by priority (a UI take is
    /// priority 16, the same as every other UI sound) and caps each event: a take started at that moment can be taken over or cut by the resume
    /// before it is heard, and its quiet first 0.1 s (the speed-up sweep swells) sits under the wall of resumed sound. The freeze take plays
    /// while the world still runs normally and was heard.</para>
    /// <para>Now: one 2D source of the director's own (ignores the listener pause, priority 0, no reverb zones, no Doppler), PlayOneShot, so a
    /// take can never be stolen, capped or released by anything else; the take is picked by the event's own shuffled bag (one of the 4, random,
    /// never the same twice in a row) and mixed exactly as the pool mixes a UI event (event dB + clip gain + mix trim, the master, the UI bus with
    /// the player's volumes), the unfreeze <see cref="OzTacticalPause.UnfreezeLiftDb"/> louder so it stands over the resumed battle. One sound
    /// log line per take (AD, "PLAY own UI source").</para>
    /// </summary>
    public partial class AudioDirector
    {
        AudioSource timeFreezeSrc;
        /// <summary>Counted proof: takes played on the own source; the last one.</summary>
        public static int OwnUiPlays;
        public static string LastOwnUi = "-";

        /// <summary>One take of a 2D UI event on the director's own source. False = no such event / no clips (the caller falls back to UI()).</summary>
        public static bool PlayOwnUi(string id, float extraDb = 0f)
        {
            if (!I && !EnsureLive()) return false;
            var me = I; if (!me || !me.Loaded || id == null) return false;
            id = Canon(id);
            var d = me.Def(id);
            if (d == null || Silent.Contains(id) || d.loaded == null || d.loaded.Count == 0) return false;
            int idx = me.Pick(d, null);
            if (idx < 0 || idx >= d.loaded.Count || !d.loaded[idx]) return false;
            var clip = d.loaded[idx];
            if (!me.timeFreezeSrc)
            {
                var go = new GameObject("AudioDirector_TimeFreeze"); go.transform.SetParent(me.transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false; s.loop = false; s.spatialBlend = 0f; s.ignoreListenerPause = true; s.priority = 0; s.bypassReverbZones = true; s.dopplerLevel = 0f;
                me.timeFreezeSrc = s;
            }
            float gain = idx < d.gains.Count ? d.gains[idx] : 0f;
            float vol = Mathf.Clamp01(Db(d.volDb + gain + OzMixTrim.AdDb(d.id) + extraDb) * me.masterVolume * Db(me.BusGainDb(d.bus)));
            var src = me.timeFreezeSrc;
            src.ignoreListenerPause = true; src.volume = 1f;
            src.pitch = d.pitchMul * (1f + (d.pitchVar > 0f ? Random.Range(-d.pitchVar, d.pitchVar) : 0f));
            src.PlayOneShot(clip, vol);
            me.lastUi[id] = Time.unscaledTime;
            OzSoundLog.Add("AD", id, OzGcCache.NameOf(clip), "PLAY own UI source (tactical pause)", vol, me.ListenerPos);
            OwnUiPlays++; LastOwnUi = id + " " + clip.name + " " + vol.ToString("0.000");
            return true;
        }
    }
}
