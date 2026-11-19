#if UNITY_EDITOR
// OpenZ (GPL-3.0) - version 1.5, level 18: the per-level data of the two frame-locked briefing recorders (editor only):
//  blue = OzBlueCutsceneProof (kit k75b, menu OpenZ/V1.2/Blue Cutscenes: 75b 3 Record MP4 Briefing, or OpenZ/V1.5/L18 Briefings/1 Record Blue),
//  red  = OzRedBriefingRecorder (its red twin, menu OpenZ/V1.5/L18 Briefings/2 Record Red).
// A level without a row here (level 2) keeps the 75b recorder's own picture times, scene watch, folders and clip names, unchanged.
// Picture times are seconds on the briefing's voice clip (the beats of the level's clock), the scene watch ends after the level's last beat,
// the tail keeps the hand-back and the first start line. Frames and take files go into a new folder per take (nothing older is touched).
using System;
using System.IO;
using UnityEngine;

namespace OpenZ
{
    public sealed class OzBriefingRecTimes
    {
        public string Level, Side, ClipName, ClipDir, FramesRoot;
        public float WatchSeconds, TailSeconds;
        public (float t, string name)[] Shots;
        /// <summary>The frames folder of the last take started (the frame log writes its files there).</summary>
        public static string LastFramesDir = "";

        static string JobRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../..")).Replace('\\', '/');

        /// <summary>A new folder for one take: FramesRoot/side_job_time (never an existing one).</summary>
        public string NewFramesDir(string job)
        {
            string d = FramesRoot + "/" + Side + "_" + job + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(d); LastFramesDir = d; return d;
        }

        /// <summary>The blue row of a level (null: level 2's own values in the 75b recorder).</summary>
        public static OzBriefingRecTimes Blue(string level)
        {
            if (level != "L18") return null;
            return new OzBriefingRecTimes
            {
                Level = level, Side = "blue", ClipName = "L18_briefing_blue_take",
                ClipDir = JobRoot + "/work/staging/v1_5/owner_frames/level18/briefings/_work_takes",
                FramesRoot = JobRoot + "/work/tmp/l18_brief_rec",
                // clip scene_L18_start 19.82 s (v2), preroll 1.10, hand-back 20.72 s on the voice clock: the watch ends 4.3 s after it at the latest
                WatchSeconds = 27f, TailSeconds = 2.5f,
                Shots = new (float, string)[]
                {
                    (-1.00f, "window_opens"), (0.25f, "three_bridges"), (1.31f, "three_armies_crash"), (3.47f, "victory"), (4.75f, "now"),
                    (5.69f, "console_insert"), (6.31f, "scream_no"), (6.51f, "press"), (6.61f, "alarm"), (7.81f, "blast_west"), (8.26f, "blast_middle"), (8.71f, "blast_east"),
                    (9.70f, "river_chase"), (10.41f, "sidekick_sir"), (11.60f, "all_three"), (13.60f, "tear"), (14.03f, "exactly"), (16.19f, "can_not"),
                    (16.85f, "cross"), (18.13f, "neither"), (19.80f, "voice_end"), (20.50f, "hand_back")
                }
            };
        }

        /// <summary>The red row of a level (null: the red recorder does not know the level).</summary>
        public static OzBriefingRecTimes Red(string level)
        {
            if (level != "L18") return null;
            return new OzBriefingRecTimes
            {
                Level = level, Side = "red", ClipName = "L18_briefing_red_take",
                ClipDir = JobRoot + "/work/staging/v1_5/owner_frames/level18/briefings/_work_takes",
                FramesRoot = JobRoot + "/work/tmp/l18_brief_rec",
                // line zod_brief_L18 18.94 s (clip 19.34 s): the watch ends 9 s after the last word at the latest (the flight lands after the line)
                WatchSeconds = 28f, TailSeconds = 2.5f,
                Shots = new (float, string)[]
                {
                    (0.00f, "line_start"), (0.32f, "alan_sir"), (1.40f, "alan_blew_up"), (3.71f, "zod_excellent"), (5.00f, "trapped"), (6.67f, "alan_actually"),
                    (8.00f, "so_are_we"), (9.30f, "even_better"), (11.00f, "never_see_us"), (13.26f, "brad_but_sir"), (14.60f, "how_across"),
                    (16.16f, "zod_swim"), (17.80f, "get_on_with_it"), (18.90f, "line_end")
                }
            };
        }
    }
}
#endif
