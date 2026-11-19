// OpenZ (GPL-3.0) - version 1.5, level 18 "Bridge Game": Zod, Alan and Brad hold the mission-start briefing together, on their fort's north parapet.
// A line of zod_lines.json with "host": "trio" on a level that has a watch set (OzL18WatchSet, level L18 only, red side) is held by the three:
//  - StartPerf calls TrioHostBegin after the two robots' rule (BoysHost answers false for this host): the window shows the watch set
//    through its own camera for the whole line (ZodStage.LoungerFeed), the plate under the window stays the commander's (he is in the
//    picture), each robot's jaw moves inside his own subtitle cues only ("Alan: ..." / "Brad: ..."), Zod's on every other cue, and the
//    set plays its beats on the line's clock. The opening on the two soldiers' backs in the HQ and the cut to Alan on his cue are not
//    played (they belong to the HQ picture).
//  - Every other line, and every other level, runs exactly as before: TrioHostBegin answers false.
// The name-plate joke is not played on this briefing: level 18's shot list asks for a quiet opening (ZodShotList.QuietOpening).
// WRITTEN BY READING ONLY: compiled off-line with the project's code, never run in the engine. Remove this line once it runs.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class ZodBriefing
    {
        public const string TrioHost = "trio";
        /// <summary>
        /// its seconds of the uncut line (cues, jaws, the window's shots, takes and poses, the flights' cuts on the cues): the line's clock is the
        /// clip's second plus what was cut before it, and each jump lands on a cut of both pictures. Pairs: the clip's second, seconds cut there.</summary>
        public static readonly float[] L18RedCuts = { 5.87f, 0.8f, 11.72f, 0.78f };
        public static float LineWarp(string id, float clipT)
        {
            if (id != "zod_brief_L18") return clipT;
            float t = clipT;
            for (int i = 0; i + 1 < L18RedCuts.Length; i += 2) if (clipT >= L18RedCuts[i]) t += L18RedCuts[i + 1];
            return t;
        }
        /// <summary>Play check: how many lines the three have held on the parapet in this session.</summary>
        public static int TrioHostLines { get; private set; }

        /// <summary>StartPerf: the three hold this line (a level-start briefing whose line says so, on a level whose watch set is ready).
        /// False = not such a line, or the set could not start (the caller opens in the HQ as on every level).</summary>
        bool TrioHostBegin(Kind kind, Perf p)
        {
            if (kind != Kind.Briefing || mode != Mode.Level || p?.line == null || p.line.host != TrioHost || !game) return false;
            var set = OzL18WatchSet.For(game.level);
            if (!set || !set.Ready) return false;
            var a = new List<Vector2>(); var b = new List<Vector2>(); var z = new List<Vector2>();
            if (p.line.cues != null)
                foreach (var c in p.line.cues)
                {
                    if (c == null) continue;
                    if (IsAlanCue(c)) a.Add(new Vector2(c.start, c.end));
                    else if (IsBradCue(c)) b.Add(new Vector2(c.start, c.end));
                    else z.Add(new Vector2(c.start, c.end));
                }
            // the line's clock: seconds on the voice clip (negative before it); +infinity while the window closes; NaN when the line is gone
            System.Func<float> lineClock = () => perf == p ? (p.done ? float.PositiveInfinity : p.t) : float.NaN;
            if (!set.BriefBegin(stage, voice, a, b, z, lineClock, VoiceGain())) return false;
            ZodComms.Host = null; comms?.ApplyRank();                                                   // the commander's own plate
            stage.GazeBoys = false;
            TrioHostLines++;
            Debug.Log($"[ZodCameo] {p.id}: held by Zod, Alan and Brad on their fort's parapet ({z.Count} cues of the commander, {a.Count} of the smaller robot, {b.Count} of the bigger one)");
            return true;
        }

        /// <summary>The window's signal meter also follows the three on the parapet.</summary>
        static float WatchMouth01 => OzL18WatchSet.I && OzL18WatchSet.I.BriefingOn ? OzL18WatchSet.I.Mouth01 : 0f;
    }
}
