// OpenZ (GPL-3.0) - version 1.5 kit crane_deck_sound_r3: the crane's construction sound while it repairs (view only).
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// fade out). The event <see cref="CraneConstructId"/> is made here from the table's veh_crane_work (its bus, curve and distances), with
    /// start, so the 5.65 s clip loops without a click), a loop at <see cref="CraneConstructDb"/> dB (his take is -15.9 LUFS: about -22 LUFS
    /// before the bus and the distance curve). It starts at the clip's start with a <see cref="CraneFadeIn"/> s fade in and stops with a
    /// <see cref="CraneFadeOut"/> s fade out (StopLoop). Logged in OzSoundLog as every loop start.
    /// </summary>
    public partial class AudioDirector
    {
        public const string CraneConstructId = "veh_crane_construct";
        public const string CraneConstructRes = "OpenZ/Audio/Sfx/sd_crane_construct_owner_loop";
        public const float CraneConstructDb = -6.0f, CraneFadeIn = 0.3f, CraneFadeOut = 0.5f;

        SoundEventDef CraneConstructDef()
        {
            if (defs.TryGetValue(CraneConstructId, out var have)) return have.loaded != null && have.loaded.Count > 0 ? have : null;
            var b = Def("veh_crane_work");
            var d = new SoundEventDef
            {
                id = CraneConstructId, bus = b != null ? b.bus : "SFX/World", channel = "", status = "final",
                is3D = true, noRepeat = false, loop = true, inViewOnly = false, offscreen = true, coverage = false,
                blend = b != null ? b.blend : 0.8f, minDist = b != null ? b.minDist : 6f, maxDist = b != null ? b.maxDist : 60f, spread = 0f,
                volDb = CraneConstructDb, volVarDb = 0f, pitchVar = 0f, pitchMul = 1f, cooldown = 0f, gap = 0f, doppler = 0f, maxCamHeight = 0f,
                maxVoices = 4, priority = b != null ? b.priority : 60, rank = 0, routes = new string[0],
                clips = new[] { new SoundClipRef { res = CraneConstructRes, gainDb = 0f, standin = false, line = "" } },
            };
            d.loaded = new List<AudioClip>(); d.gains = new List<float>(); d.lines = new List<string>(); d.standins = new List<bool>();
            LoadClips(d);
            defs[CraneConstructId] = d;
            if (d.loaded.Count == 0) { Debug.LogWarning("[AudioDirector] " + CraneConstructRes + " is missing: the crane works without its construction sound"); return null; }
            return d;
        }

        /// <summary>The construction loop of crane job `key` at `pos` (following `follow`), from the clip's start, faded in. 1 = playing.</summary>
        public int CraneConstructStart(string key, Vector3 pos, Transform follow)
        {
            if (CraneConstructDef() == null) return 0;
            bool fresh = !(loops.TryGetValue(key, out var was) && was.busy);
            int r = StartLoop(key, CraneConstructId, pos, follow);
            if (r == 1 && loops.TryGetValue(key, out var v) && v.busy)
            {
                if (fresh) v.src.time = 0f;                                                              // his take from its start
                v.fadeSpeed = 1f / Mathf.Max(0.02f, CraneFadeIn);
            }
            return r;
        }

        /// <summary>Fades crane job `key`'s construction loop out (the repair ended or was called off).</summary>
        public void CraneConstructStop(string key) => StopLoop(key, CraneFadeOut);

        /// <summary>The owner's construction clip is in the build (OzCraneLandEnd.OwnsWorkSound: the old repair-work noise stays out).</summary>
        public bool CraneConstructReady => CraneConstructDef() != null;
    }
}
