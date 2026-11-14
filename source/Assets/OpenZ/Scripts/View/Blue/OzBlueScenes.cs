// OpenZ (GPL-3.0)
// Resources/OpenZ/Blue/blue_scenes.json holds the four scenes (briefing, win, lose, win_nuke): the subtitle lines with their speaker
// and times, the words of the approved voice join with their times, and the beats (what the stage does at which second: the camera
// framings, EvilZod's gestures, the sidekick's moods and moves, the set). Times are seconds from the voice start (negative = in the
// window's opening); every beat was placed on the words of the approved take. Plain data; OzBlueCameo plays it on OzEvilStage.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    [Serializable] public class OzBlueLine { public string who, text; public float start, end; }
    [Serializable] public class OzBlueWord { public string w, who; public float s, e; }
    /// <summary>One beat: at t, for `who` ("ez" EvilZod, "sk" the sidekick, "cam" the camera, "set" the room), the beat's name with
    /// two numbers (a, b: strength / seconds / a sign, as the beat needs), a hold (s) and a word (a mood name for "mood").</summary>
    [Serializable] public class OzBlueBeat { public float t; public string who, beat, word; public float a, b, hold; }
    [Serializable]
    public class OzBlueScene
    {
        public string id, voice; public float length, preroll, tail;
        public OzBlueLine[] lines; public OzBlueWord[] words; public OzBlueBeat[] beats;
    }
    [Serializable] public class OzBlueSceneFile { public int version; public float pumpLen; public OzBlueScene[] scenes; }

    /// <summary>The scenes, read once per session; the voice joins loaded on demand (Resources/OpenZ/Blue/Voice/&lt;voice&gt;).</summary>
    public sealed class OzBlueScenes
    {
        public const string Path = "OpenZ/Blue/blue_scenes", VoicePath = "OpenZ/Blue/Voice/";
        public const string Briefing = "briefing", Win = "win", Lose = "lose", WinNuke = "win_nuke";
        public const string EvilZod = "evilzod", Sidekick = "sidekick";
        /// <summary>Speaker labels on the subtitle bars and the window (the sidekick's name is his hull marking).</summary>
        public const string EvilZodLabel = "GEN EVILZOD", SidekickLabel = "BS-01", EvilZodName = "GENERAL EVILZOD", Company = "EVIL TAKEOVER INDUSTRIES";
        static OzBlueScenes shared;
        public static OzBlueScenes Shared => shared ??= new OzBlueScenes();

        readonly Dictionary<string, OzBlueScene> scenes = new Dictionary<string, OzBlueScene>();
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        public float PumpLen { get; private set; } = 1.0f;
        public bool Ok => scenes.Count > 0;
        public string Problem { get; private set; } = "";

        OzBlueScenes()
        {
            try
            {
                var ta = Resources.Load<TextAsset>(Path);
                if (!ta) { Problem = "no Resources/" + Path + ".json"; return; }
                var f = JsonUtility.FromJson<OzBlueSceneFile>(ta.text);
                if (f?.scenes != null) foreach (var s in f.scenes) if (s != null && !string.IsNullOrEmpty(s.id)) scenes[s.id] = s;
                if (f != null && f.pumpLen > 0.2f) PumpLen = f.pumpLen;
                foreach (var s in scenes.Values) if (s.beats != null) Array.Sort(s.beats, (a, b) => a.t.CompareTo(b.t));
            }
            catch (Exception e) { Problem = e.Message; }
            if (scenes.Count == 0) Debug.LogWarning("[EvilZod] blue scenes not readable (" + Problem + ")");
        }

        public OzBlueScene Scene(string id) => id != null && scenes.TryGetValue(id, out var s) ? s : null;

        public AudioClip Clip(OzBlueScene s)
        {
            if (s == null || string.IsNullOrEmpty(s.voice)) return null;
            if (clips.TryGetValue(s.voice, out var c) && c) return c;
            c = Resources.Load<AudioClip>(VoicePath + s.voice);
            if (c && c.loadState == AudioDataLoadState.Unloaded) c.LoadAudioData();
            clips[s.voice] = c;
            return c;
        }

        /// <summary>The scene, its clip and its data are there (a missing piece names itself in `why`).</summary>
        public bool Ready(string id, out string why)
        {
            var s = Scene(id);
            why = s == null ? "no scene " + id + (Problem.Length > 0 ? " (" + Problem + ")" : "") : !Clip(s) ? "no voice clip " + VoicePath + s.voice
                : s.lines == null || s.lines.Length == 0 ? "no lines in " + id : s.beats == null || s.beats.Length == 0 ? "no beats in " + id : null;
            return why == null;
        }

        /// <summary>The subtitle line at time t (lines hold a little past their end), null in the gaps.</summary>
        public static OzBlueLine LineAt(OzBlueScene s, float t, float linger = 0.3f)
        {
            if (s?.lines == null) return null;
            for (int i = 0; i < s.lines.Length; i++)
            {
                var l = s.lines[i];
                bool last = i == s.lines.Length - 1;
                if (t >= l.start - 0.05f && t < l.end + (last ? linger : 0.05f)) return l;
            }
            return null;
        }

        /// <summary>Who is speaking at t (the lines' windows, a little widened so the mouth closes after the last word), or null.</summary>
        public static string SpeakerAt(OzBlueScene s, float t)
        {
            if (s?.lines == null) return null;
            foreach (var l in s.lines) if (t >= l.start - 0.08f && t <= l.end + 0.05f) return l.who;
            return null;
        }

        /// <summary>Checks: the beats of a scene that name a word, resolved against its words (a beat's time is already absolute; this
        /// only tells whether its named mood / word exists where it should).</summary>
        public static int CountBeats(OzBlueScene s, string who) { int n = 0; if (s?.beats != null) foreach (var b in s.beats) if (b.who == who) n++; return n; }
    }
}
