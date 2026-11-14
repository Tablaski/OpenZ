// OpenZ (GPL-3.0) - version 1.2: General EvilZod's lines on the blue side's channel card (the battlefield screen with the blue side picked).
// Two lines for each level, three for the jungle (L16); each time he speaks for a level he says ONE of its lines, picked at random (never
// two). The words are the owner's, as written; the cues split them sentence by sentence on the measured words of the approved takes; the
// voices are the takes at the game's voice level (Resources/OpenZ/Blue/Voice/evil_card_<level>_<n>, -18 LUFS like the commander's level
// lines).
// 1.3 item 30: level 7 (the lava level) gets ONE card line, a two-voice exchange: the sidekick BS-01 starts, EvilZod cuts him off
// Resources/OpenZ/Blue/Voice/evil_card_L07_1). A line without its take (Duration 0) is never picked (the guard stays for any later
// line; the package's lines_item30.json holds the words, the voices and the play rule).
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>One subtitle cue of a card line: its words, when they start / end (s from the voice start) and who says them
    /// (OzBlueScenes.EvilZod, or OzBlueScenes.Sidekick in a 1.3 exchange).</summary>
    public readonly struct OzEvilCue
    {
        public readonly string Text; public readonly float Start, End; public readonly string Who;
        public OzEvilCue(string text, float start, float end) : this(OzBlueScenes.EvilZod, text, start, end) { }
        public OzEvilCue(string who, string text, float start, float end) { Who = who; Text = text; Start = start; End = end; }
    }

    /// <summary>One of EvilZod's card lines: its level, its voice file (= <see cref="Id"/>), the words, the take's length, the cues and the
    /// second the camera's jolt lands on (one stressed word, as the commander's punch on his own level lines).</summary>
    public sealed class OzEvilCardLine
    {
        public readonly string Id, Level, Text; public readonly float Duration, KickAt; public readonly OzEvilCue[] Cues;
        public OzEvilCardLine(string id, string level, string text, float duration, float kickAt, params OzEvilCue[] cues)
        { Id = id; Level = level; Text = text; Duration = duration; KickAt = kickAt; Cues = cues; }

        /// <summary>The cue on screen at t: the last one started (the first from the voice's start; the last holds to the end).</summary>
        public string CueAt(float t)
        {
            string s = Cues[0].Text;
            for (int i = 1; i < Cues.Length; i++) if (t >= Cues[i].Start) s = Cues[i].Text;
            return s;
        }

        /// <summary>1.3 item 30: who speaks the cue on screen at t (the same rule as <see cref="CueAt"/>; EvilZod on every 1.2 line).</summary>
        public string WhoAt(float t)
        {
            string w = Cues[0].Who;
            for (int i = 1; i < Cues.Length; i++) if (t >= Cues[i].Start) w = Cues[i].Who;
            return string.IsNullOrEmpty(w) ? OzBlueScenes.EvilZod : w;
        }

        /// <summary>1.3 item 30: the line's words exist but its take does not yet (TEXT ONLY, waiting for the owner): never picked.</summary>
        public bool Pending => Duration <= 0f;
        /// <summary>1.3 item 30: more than one speaker (an exchange: BS-01, then EvilZod).</summary>
        public bool Exchange { get { foreach (var c in Cues) if (c.Who != OzBlueScenes.EvilZod) return true; return false; } }
    }

    public static class OzEvilCardLines
    {
        public const string VoicePath = "OpenZ/Blue/Voice/";
        /// <summary>The camera eases in to his close-up before his first word (s), and holds it this long after his last.</summary>
        public const float Preroll = 0.45f, Tail = 0.6f;

        /// <summary>The seven lines (the cue times: 0.12 s before the measured first word of each sentence).</summary>
        public static readonly OzEvilCardLine[] All =
        {
            new OzEvilCardLine("evil_card_L02_1", "L02", "Sand, sun, and scrap metal! The way I like it!", 4.417f, 1.53f,
                new OzEvilCue("Sand, sun, and scrap metal!", 0f, 2.78f), new OzEvilCue("The way I like it!", 2.78f, 4.417f)),
            new OzEvilCardLine("evil_card_L02_2", "L02", "The desert, where red robots come to rust!", 3.937f, 2.95f,
                new OzEvilCue("The desert, where red robots come to rust!", 0f, 3.937f)),
            // (BS-01 from 0, EvilZod from 0.12 s before his first word) from the measured words, as for the lines above.
            new OzEvilCardLine("evil_card_L07_1", "L07", "Sir! The floor is lava! Oh, cut the crap and go win it. And bring me a burger!", 6.007f, 3.45f,
                new OzEvilCue(OzBlueScenes.Sidekick, "Sir! The floor is lava!", 0f, 1.96f), new OzEvilCue(OzBlueScenes.EvilZod, "Oh, cut the crap and go win it. And bring me a burger!", 1.96f, 6.007f)),
            new OzEvilCardLine("evil_card_L16_1", "L16", "Welcome to the jungle! Paint it blue!", 3.377f, 2.43f,
                new OzEvilCue("Welcome to the jungle!", 0f, 2.06f), new OzEvilCue("Paint it blue!", 2.06f, 3.377f)),
            new OzEvilCardLine("evil_card_L16_2", "L16", "Clear that jungle! I want a parking lot by lunch!", 3.617f, 2.67f,
                new OzEvilCue("Clear that jungle!", 0f, 1.30f), new OzEvilCue("I want a parking lot by lunch!", 1.30f, 3.617f)),
            new OzEvilCardLine("evil_card_L16_3", "L16", "Jungle with crocs? I prefer sharks with lasers!", 4.657f, 3.49f,
                new OzEvilCue("Jungle with crocs?", 0f, 2.40f), new OzEvilCue("I prefer sharks with lasers!", 2.40f, 4.657f)),
            new OzEvilCardLine("evil_card_L17_1", "L17", "This city needs a new mayor! Me! And you found one!", 4.977f, 2.57f,
                new OzEvilCue("This city needs a new mayor!", 0f, 2.50f), new OzEvilCue("Me! And you found one!", 2.50f, 4.977f)),
            new OzEvilCardLine("evil_card_L17_2", "L17", "One city, two armies, zero parking spaces! Let's fix that!", 5.137f, 4.27f,
                new OzEvilCue("One city, two armies, zero parking spaces!", 0f, 3.86f), new OzEvilCue("Let's fix that!", 3.86f, 5.137f)),
        };

        /// <summary>
        /// The level's SECOND line (L02_2, L16_2, L17_2: one in each level's
        /// draw) is filmed on EvilZod alone: the card eases to his mid shot, crash-zooms into his face on the stressed word (KickAt) and
        /// eases back to the mid shot, never to the two-shot. The other lines as before (his close-up, back to the two-shot after).</summary>
        public static bool StaysOnHim(OzEvilCardLine l) => l != null && l.Id.EndsWith("_2");

        /// <summary>The lines of a level, in the table's order (two, three for the jungle; none for an unknown level).</summary>
        public static List<OzEvilCardLine> ForLevel(string level)
        {
            var l = new List<OzEvilCardLine>(3);
            foreach (var x in All) if (x.Level == level) l.Add(x);
            return l;
        }

        /// <summary>ONE of the level's lines, picked at random (a new draw every time, each line as likely; null for a level without lines;
        /// 1.3 item 30: a line still waiting for its take is never picked).</summary>
        public static OzEvilCardLine Pick(string level) => Pick(level, Random.value);
        /// <summary>The pick for a draw r in [0, 1] among the level's N lines: r split into N equal parts, one a line (two lines: the
        /// halves; three: the thirds); r = 1 is the last line.</summary>
        public static OzEvilCardLine Pick(string level, float r)
        {
            var lines = ForLevel(level); lines.RemoveAll(x => x.Pending);                                       // 1.3 item 30
            if (lines.Count == 0) return null;
            return lines[Mathf.Clamp((int)(r * lines.Count), 0, lines.Count - 1)];
        }

        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        /// <summary>The line's voice clip (loaded once).</summary>
        public static AudioClip Clip(OzEvilCardLine l)
        {
            if (l == null) return null;
            if (clips.TryGetValue(l.Id, out var c) && c) return c;
            c = Resources.Load<AudioClip>(VoicePath + l.Id);
            if (c && c.loadState == AudioDataLoadState.Unloaded) c.LoadAudioData();
            clips[l.Id] = c;
            return c;
        }
    }
}
