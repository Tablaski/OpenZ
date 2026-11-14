// OpenZ (GPL-3.0) - version 1.2, item 61: the lines of the original game's voice list that OpenZ did not have yet at their moment,
// added to their pools in the voice of each pool (nothing replaced, every older take stays):
//   ann_radar_online       Rachel  "Radar activated."             rachel_radar_activated_1 / _2
//   ann_fort_under_attack  Rachel  "Your fort is under attack!"   rachel_your_fort_is_under_attack_1 / _2
//   vo_vehicle_captured    robots  "Vehicle captured!"            alan_vehicle_captured_1 / _2 (the shared robot voice of that pool)
// The game's rules stay as they are: the radar kind gap, the fort alarm's alert limiter and its hold in the Unit editor's playground
// (MatchStateLines holds the whole event), the capture quip's chance and Rachel's follow-up. Static entry points for the checks in
// Play (the editor's code runner, or the menus of Editor/OzZVoiceLinesMenu.cs):
//   OzZVoiceLinesProof.Status()     the three pools: lines, clips, the new clips load and sit in their pool, the old clips still load
//   OzZVoiceLinesProof.PlayNew()    the six new clips through the game's own path (Announce for Rachel, Say for the robots; every
//                                   gate of the game applies: the proof waits for quiet and retries a dropped request), one row per clip
//   OzZVoiceLinesProof.HeldCheck()  in the Unit editor's playground: the new fort alarm is held like the old ones
//   OzZVoiceLinesProof.Running / Last / Report
// View only: it never touches the simulation.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OpenZ
{
    public partial class AudioDirector
    {
        /// <summary>The proof's request: says one line ("=line") through the game's own path and waits until its channel plays a
        /// clip of that event (5 s), else retries every 2 s (a request a rule dropped: the radar kind gap, the alert limiter, Alan
        /// speaking), at most <paramref name="timeout"/> s. got = the clip name and length, or null.</summary>
        internal IEnumerator ZVoiceLineSay(string ev, string line, bool rachelLine, float timeout, Action<string, float> got)
        {
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < timeout)
            {
                while (VoiceBusyImpl() && Time.unscaledTime - t0 < timeout) yield return null;
                if (rachelLine) Announce(ev, "=" + line); else Say(ev, "=" + line);              // the game's own entry points
                var ch = rachelLine ? rachel : alanAlert;
                float t1 = Time.unscaledTime;
                while (Time.unscaledTime - t1 < 5f)
                {
                    if (ch != null && ch.src && ch.src.isPlaying && ch.src.clip && ch.def != null && ch.def.id == ev)
                    {
                        string name = ch.src.clip.name; float len = ch.src.clip.length;
                        while (ch.src && ch.src.isPlaying) yield return null;                       // the whole line, never cut
                        got?.Invoke(name, len);
                        yield break;
                    }
                    yield return null;
                }
                float t2 = Time.unscaledTime;
                while (Time.unscaledTime - t2 < 2f) yield return null;
            }
            got?.Invoke(null, 0f);
        }
    }

    public static class OzZVoiceLinesProof
    {
        /// <summary>(event, line, clip resource) of the six new clips.</summary>
        public static readonly string[,] NewClips =
        {
            { "ann_radar_online", "Radar activated.", "OpenZ/Audio/Voice/rachel_radar_activated_1" },
            { "ann_radar_online", "Radar activated.", "OpenZ/Audio/Voice/rachel_radar_activated_2" },
            { "ann_fort_under_attack", "Your fort is under attack!", "OpenZ/Audio/Voice/rachel_your_fort_is_under_attack_1" },
            { "ann_fort_under_attack", "Your fort is under attack!", "OpenZ/Audio/Voice/rachel_your_fort_is_under_attack_2" },
            { "vo_vehicle_captured", "Vehicle captured!", "OpenZ/Audio/Voice/alan_vehicle_captured_1" },
            { "vo_vehicle_captured", "Vehicle captured!", "OpenZ/Audio/Voice/alan_vehicle_captured_2" },
        };
        /// <summary>(event, clips before this addition): every pool keeps all of them.</summary>
        public static readonly string[] Pools = { "ann_radar_online", "ann_fort_under_attack", "vo_vehicle_captured" };
        public static readonly int[] ClipsBefore = { 3, 12, 16 };
        public static bool Running { get; private set; }
        public static string Last { get; private set; } = "";
        public static string Report { get; private set; } = "not run";

        static bool IsRachel(string ev) => ev.StartsWith("ann_", StringComparison.Ordinal);
        static string Leaf(string res) => res.Substring(res.LastIndexOf('/') + 1);

        public static string Status()
        {
            var ad = AudioDirector.I;
            if (!ad) return "no AudioDirector (enter Play on a level)";
            var sb = new StringBuilder(); bool ok = true;
            for (int p = 0; p < Pools.Length; p++)
            {
                var d = ad.Def(Pools[p]);
                if (d == null || d.clips == null || d.loaded == null) { ok = false; sb.AppendLine("- FAIL " + Pools[p] + ": not in the sound table"); continue; }
                var lines = new List<string>();
                foreach (var l in d.lines) if (!string.IsNullOrEmpty(l) && !lines.Contains(l)) lines.Add(l);
                int missing = d.clips.Length - d.loaded.Count;
                bool grew = d.clips.Length >= ClipsBefore[p] + 2;
                if (missing > 0 || !grew) ok = false;
                sb.AppendLine("- " + Pools[p] + ": " + lines.Count + " lines, " + d.loaded.Count + " of " + d.clips.Length + " clips load (before: "
                              + ClipsBefore[p] + ")" + (missing > 0 ? " (" + missing + " MISSING)" : "") + (grew ? "" : " FAIL: the two new clips are not in it")
                              + ", channel " + d.channel + ", cooldown " + d.cooldown.ToString("0.0") + " s");
            }
            for (int i = 0; i < NewClips.GetLength(0); i++)
            {
                string ev = NewClips[i, 0], line = NewClips[i, 1], leaf = Leaf(NewClips[i, 2]);
                var d = ad.Def(ev); int at = -1;
                if (d != null && d.loaded != null) for (int k = 0; k < d.loaded.Count; k++) if (d.loaded[k] && d.loaded[k].name == leaf) { at = k; break; }
                bool fine = at >= 0 && d.lines[at] == line && !d.standins[at];
                var clip = Resources.Load<AudioClip>(NewClips[i, 2]);
                if (!fine || !clip) ok = false;
                sb.AppendLine("- " + (fine && clip ? "ok   " : "FAIL ") + ev + " \"" + line + "\" " + leaf + (clip ? " (" + clip.length.ToString("0.00") + " s, " + clip.channels + " ch, " + clip.frequency + " Hz)" : " does not load")
                              + (at < 0 ? ": not in the pool" : ""));
            }
            bool held = AudioDirector.MatchStateLines.Contains("ann_fort_under_attack");
            if (!held) ok = false;
            sb.AppendLine("- the Unit editor's playground holds ann_fort_under_attack (a match-state line), so the new fort takes too: " + (held ? "yes" : "NO"));
            sb.AppendLine("- ann_radar_online and vo_vehicle_captured are not match-state lines (as before): " +
                          (!AudioDirector.MatchStateLines.Contains("ann_radar_online") && !AudioDirector.MatchStateLines.Contains("vo_vehicle_captured") ? "yes" : "NO"));
            sb.Insert(0, "[z_voice_lines] RESULT: " + (ok ? "PASS" : "FAIL") + "\n");
            Debug.Log(sb.ToString());
            return sb.ToString();
        }

        /// <summary>Every new clip once through the game's own path: each line is asked until both of its takes have played (the
        /// "=line" pick never takes the same clip twice in a row), at most 4 requests per line. Waits for quiet before each.</summary>
        public static string PlayNew()
        {
            var ad = AudioDirector.I;
            if (!ad) return "no AudioDirector (enter Play on a level)";
            if (Running) return "already running";
            Running = true; Report = "running";
            ad.StartCoroutine(Run(ad));
            return "playing the 6 new clips (about 2 minutes: the fort alarm's alert limiter keeps 30 s between two alarms); read OzZVoiceLinesProof.Report / Last when Running is false";
        }

        static IEnumerator Run(AudioDirector ad)
        {
            var sb = new StringBuilder(); var heard = new HashSet<string>(); int fails = 0;
            var lines = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < NewClips.GetLength(0); i++)
            {
                var kv = new KeyValuePair<string, string>(NewClips[i, 0], NewClips[i, 1]);
                if (!lines.Contains(kv)) lines.Add(kv);
            }
            foreach (var kv in lines)
            {
                var want = new List<string>();
                for (int i = 0; i < NewClips.GetLength(0); i++) if (NewClips[i, 0] == kv.Key && NewClips[i, 1] == kv.Value) want.Add(Leaf(NewClips[i, 2]));
                for (int ask = 0; ask < 4 && !want.TrueForAll(heard.Contains); ask++)
                {
                    string name = null; float len = 0f;
                    yield return ad.ZVoiceLineSay(kv.Key, kv.Value, IsRachel(kv.Key), 45f, (n, l) => { name = n; len = l; });
                    string row = "[z_voice_lines] " + (name != null ? "OK   " : "FAIL ") + kv.Key + " \"" + kv.Value + "\" clip " + (name ?? "-")
                                 + (name != null ? " (" + len.ToString("0.00") + " s) on " + (IsRachel(kv.Key) ? "Rachel's channel" : "Alan's alert channel (robot filter)") : " (nothing played in 45 s)");
                    sb.AppendLine(row); Debug.Log(row);
                    if (name == null) { fails++; break; }
                    heard.Add(name);
                }
            }
            int got = 0;
            for (int i = 0; i < NewClips.GetLength(0); i++) if (heard.Contains(Leaf(NewClips[i, 2]))) got++;
            bool pass = fails == 0 && got == NewClips.GetLength(0);
            Report = (pass ? "PASS" : "FAIL") + " (" + got + " of " + NewClips.GetLength(0) + " new clips played through the game's path)";
            sb.AppendLine("[z_voice_lines] " + Report);
            Last = sb.ToString(); Running = false;
            Debug.Log("[z_voice_lines] proof done\n" + Last);
        }

        /// <summary>In the Unit editor's playground (Play): asks the new fort alarm and checks that the match-state rule holds it,
        /// as it holds the older fort takes. Elsewhere it says where to run it.</summary>
        public static string HeldCheck()
        {
            var sb = new StringBuilder();
            if (!AudioDirector.I) return "no AudioDirector (enter Play)";
            if (!AudioDirector.NoMatchHere) return "[z_voice_lines] HELD CHECK: open the Unit editor (Play) and run it there (in a level nothing is held, as before)";
            int before = AudioDirector.MatchLinesHeld;
            AudioDirector.Announce("ann_fort_under_attack", "=Your fort is under attack!");
            bool held = AudioDirector.MatchLinesHeld == before + 1;
            sb.AppendLine("[z_voice_lines] HELD CHECK " + (held ? "PASS" : "FAIL") + ": ann_fort_under_attack \"Your fort is under attack!\" in the Unit editor's playground: "
                          + (held ? "held (the sound log: HELD match-state line)" : "NOT held"));
            Debug.Log(sb.ToString());
            return sb.ToString();
        }
    }
}
