// OpenZ (GPL-3.0) - version 1.5 kit crane_repair_look_r6: a one-shot event held under a gain (the bridge restore's bed: fade in, fade out).
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// event played as a HELD voice: it starts at the clip's start, silent, its level is the event's own x the gain the caller sets each
    /// frame (<see cref="HeldGain"/>), and it ends when the caller lets it go (<see cref="HeldStop"/>) or the clip is over. It uses the
    /// loop voices' own fade (the crane's construction loop fades the same way), with the one-shot's screen rule (off screen = not played,
    /// just off screen = 9 dB down, AudioDirector.Play). Logged in OzSoundLog like every loop start.
    /// </summary>
    public partial class AudioDirector
    {
        /// <summary>Starts event `id` as the held voice `key` at `pos`. False = not played (no event, no clip, off screen).</summary>
        public bool HeldStart(string key, string id, Vector3 pos, float volMul, float gain)
        {
            id = Canon(id);
            var d = Def(id);
            if (d == null || d.loaded.Count == 0 || Silent.Contains(id)) { OzSoundLog.Add("AD", id, null, "DROP held one-shot: no event / no clip", 0f, pos); return false; }
            float off = 1f;
            if (d.is3D && d.inViewOnly && cam)
            {
                var vp = cam.WorldToViewportPoint(pos);
                if (OzFpsView.Own(pos)) vp = new Vector3(0.5f, 0.5f, 1f);
                bool inView = vp.z > 0 && vp.x > -0.35f && vp.x < 1.35f && vp.y > -0.35f && vp.y < 1.35f;
                if (!inView)
                {
                    bool near = vp.z > 0 && vp.x > -1.5f && vp.x < 2.5f && vp.y > -1.5f && vp.y < 2.5f;
                    if (!(d.offscreen && near)) { OzSoundLog.Add("AD", id, null, "DROP off screen", 0f, pos); return false; }
                    off = Db(-9f);
                }
            }
            float pitch = 1f + (d.pitchVar > 0 ? UnityEngine.Random.Range(-d.pitchVar, d.pitchVar) : 0f);
            if (StartLoop(key, id, pos, null, volMul * off, pitch) != 1 || !loops.TryGetValue(key, out var v) || !v.busy) return false;
            v.src.loop = false; v.src.time = 0f;                                                          // a one-shot: from its start, once
            v.fade = v.fadeTarget = Mathf.Max(HeldFloor, Mathf.Clamp01(gain)); v.fadeSpeed = 1000f;
            OzSoundLog.Add("AD", id, v.src.clip ? v.src.clip.name : null, $"HELD one-shot (gain set by its owner, starts at {Mathf.Clamp01(gain):0.00})", v.baseLin, pos);
            return true;
        }

        /// <summary>The loop voices stop a voice whose fade reaches 0: a held voice at gain 0 stays just above it (-56 dB) until it is let go.</summary>
        const float HeldFloor = 0.0015f;

        /// <summary>The held voice's gain now (0..1 on top of the event's own level).</summary>
        public void HeldGain(string key, float gain)
        {
            if (key == null || !loops.TryGetValue(key, out var v) || !v.busy) return;
            v.fade = v.fadeTarget = Mathf.Max(HeldFloor, Mathf.Clamp01(gain)); v.fadeSpeed = 1000f;
        }

        /// <summary>Lets the held voice go (30 ms to silence, then its source is freed).</summary>
        public void HeldStop(string key) { if (key != null) StopLoop(key, 0.03f); }
    }
}
