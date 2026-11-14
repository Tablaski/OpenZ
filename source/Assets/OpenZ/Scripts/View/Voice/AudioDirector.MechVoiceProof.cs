// OpenZ (GPL-3.0) - 1.2 item 15: the mech's own voice pools (sound table vo_select_x_medium, vo_order_x_medium_move and
// vo_order_x_medium_attack). The move line "Walkies!" (a long vowel held on one pitch that sounded like "ahhh") left the move pool;
// "All systems go!" takes its place and 10 new mech lines joined the three pools. The two old "Walkies!" files stay in the project,
// unused. Static entry points for the checks in Play (the editor's code runner):
//   OzMechVoiceProof.Status()       the three pools: lines and clips, clips that do not load, a retired line still in a pool,
//                                   the retired files still in the project; "RESULT: PASS" when all is well
//   OzMechVoiceProof.PlayNew()      the 11 new lines once each through the game's own path (Alan's main channel, its robot filter,
//                                   the never-cut rule and the robot line gap), one console row per line:
//                                   [mech_voice] OK   vo_order_x_medium_move "All systems go!" clip alan_uv_medium_move_4_1 (1.09 s)
//   OzMechVoiceProof.PlayAll()      the same for every line of the three pools
//   OzMechVoiceProof.Running / Last the run is over when Running is false; Last holds its rows (a FAIL row = the line did not start)
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
        /// <summary>Says every (event, line) once with the "=line" filter: waits for Alan's speaking line and the robot line gap
        /// (AlanMainGap) first, so every line really starts, then logs the clip that alan_main plays.</summary>
        internal IEnumerator MechVoiceProofRun(List<KeyValuePair<string, string>> takes, StringBuilder log, Action done)
        {
            foreach (var t in takes)
            {
                string ev = t.Key, line = t.Value;
                float t0 = Time.unscaledTime;
                while ((AlanBusy || Time.unscaledTime < alanMainUntil + AlanMainGap + 0.1f) && Time.unscaledTime - t0 < 12f) yield return null;
                lastLine.Remove(ev);                                                                 // the proof paces itself (no cooldown)
                Say(ev, "=" + line);
                yield return null;
                bool on = alanMain.src && alanMain.src.isPlaying && alanMain.src.clip && alanMain.def != null && alanMain.def.id == ev;
                string clip = on ? alanMain.src.clip.name + " (" + alanMain.src.clip.length.ToString("0.00") + " s)" : "-";
                string row = "[mech_voice] " + (on ? "OK   " : "FAIL ") + ev + " \"" + line + "\" clip " + clip;
                log.AppendLine(row); Debug.Log(row);
            }
            done?.Invoke();
        }
    }

    public static class OzMechVoiceProof
    {
        public static readonly string[] Events = { "vo_select_x_medium", "vo_order_x_medium_move", "vo_order_x_medium_attack" };
        /// <summary>The line that left the move pool, and its two files (kept in the project, unused).</summary>
        public const string RetiredLine = "Walkies!";
        public static readonly string[] RetiredClips = { "OpenZ/Audio/Voice/Units/alan_uv_medium_move_2_1", "OpenZ/Audio/Voice/Units/alan_uv_medium_move_2_2" };
        /// <summary>The replacement and the 10 new lines, in the order PlayNew says them.</summary>
        public static readonly string[,] NewLines =
        {
            { "vo_order_x_medium_move", "All systems go!" },
            { "vo_select_x_medium", "Mech operational!" }, { "vo_select_x_medium", "Hydraulics primed!" },
            { "vo_select_x_medium", "Servos humming, sir!" }, { "vo_select_x_medium", "Fully armed and walking!" },
            { "vo_order_x_medium_move", "Forward march!" }, { "vo_order_x_medium_move", "Striding out!" }, { "vo_order_x_medium_move", "Coming through!" },
            { "vo_order_x_medium_attack", "Weapons free!" }, { "vo_order_x_medium_attack", "Squash them flat!" }, { "vo_order_x_medium_attack", "Firing all rockets!" },
        };
        public static bool Running { get; private set; }
        public static string Last { get; private set; } = "";

        public static string Status()
        {
            var ad = AudioDirector.I;
            if (!ad) return "no AudioDirector (enter Play on a level)";
            var sb = new StringBuilder(); bool ok = true;
            foreach (var ev in Events)
            {
                var d = ad.Def(ev);
                if (d == null || d.clips == null || d.loaded == null) { ok = false; sb.AppendLine("- FAIL " + ev + ": not in the sound table"); continue; }
                var lines = new List<string>();
                foreach (var l in d.lines) if (!string.IsNullOrEmpty(l) && !lines.Contains(l)) lines.Add(l);
                int missing = d.clips.Length - d.loaded.Count;
                bool retired = lines.Contains(RetiredLine);
                if (missing > 0 || retired) ok = false;
                sb.AppendLine("- " + ev + ": " + lines.Count + " lines, " + d.loaded.Count + " of " + d.clips.Length + " clips load"
                              + (missing > 0 ? " (" + missing + " MISSING)" : "") + (retired ? ", FAIL: \"" + RetiredLine + "\" is still in it" : ""));
            }
            for (int i = 0; i < NewLines.GetLength(0); i++)
            {
                var d = ad.Def(NewLines[i, 0]);
                if (d == null || d.lines == null || !d.lines.Contains(NewLines[i, 1])) { ok = false; sb.AppendLine("- FAIL new line missing: " + NewLines[i, 0] + " \"" + NewLines[i, 1] + "\""); }
            }
            int kept = 0;
            foreach (var r in RetiredClips) if (Resources.Load<AudioClip>(r)) kept++;
            sb.AppendLine("- the retired \"" + RetiredLine + "\" files kept in the project: " + kept + " of " + RetiredClips.Length);
            sb.Insert(0, "[mech_voice] RESULT: " + (ok ? "PASS" : "FAIL") + "\n");
            Debug.Log(sb.ToString());
            return sb.ToString();
        }

        public static string PlayNew()
        {
            var takes = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < NewLines.GetLength(0); i++) takes.Add(new KeyValuePair<string, string>(NewLines[i, 0], NewLines[i, 1]));
            return Run(takes);
        }

        public static string PlayAll()
        {
            var ad = AudioDirector.I;
            if (!ad) return "no AudioDirector (enter Play on a level)";
            var takes = new List<KeyValuePair<string, string>>();
            foreach (var ev in Events)
            {
                var d = ad.Def(ev); if (d == null || d.lines == null) continue;
                var seen = new HashSet<string>();
                foreach (var l in d.lines) if (!string.IsNullOrEmpty(l) && seen.Add(l)) takes.Add(new KeyValuePair<string, string>(ev, l));
            }
            return Run(takes);
        }

        static string Run(List<KeyValuePair<string, string>> takes)
        {
            var ad = AudioDirector.I;
            if (!ad) return "no AudioDirector (enter Play on a level)";
            if (Running) return "already running";
            var sb = new StringBuilder(); Running = true;
            ad.StartCoroutine(ad.MechVoiceProofRun(takes, sb, () => { Running = false; Last = sb.ToString(); Debug.Log("[mech_voice] proof done\n" + Last); }));
            return "playing " + takes.Count + " lines (about " + (takes.Count * 5) + " s with the robot line gap); read OzMechVoiceProof.Last when Running is false";
        }
    }
}
