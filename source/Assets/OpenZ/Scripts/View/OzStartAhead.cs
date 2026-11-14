// OpenZ (GPL-3.0)
// the level done before its first picture, behind the loading screen.
// What held the first seconds down: three jobs of the level's start ran one slice a frame under the first shot of the start flyover,
//  - the ending's prewarm (OpenZEnding.Prewarm): one building a frame (its ruin, rubble and chunks made ahead, the first of each kind
//    50 to 150 ms), then its warm render;
//  - the nuke's prewarm (OpenZNukePrewarm): about two thousand methods compiled in 4 ms slices, five objects, its warm render;
//  - the land beyond the jungle's rim (OzL16Outskirts.BuildSliced): about a second of work in 6 ms slices over about a hundred frames.
// Each kept single frames short, but together they held seconds 2 to 5 at 18 to 52 frames a second with frames of 100 to 160 ms. The
// distance rule's first collect is not one of them: it runs in the level's first frame, before the first picture (the one long "frame"
// of a start trace is the loading itself: the first frame after a level loads is counted from the loading screen's last frame).
// Now, on a start this is for (Levels, a blue start; RedToo for a red one), each job's first step runs where it ran (its Start) and the
// rest runs in the level's first frame, after the flyover has placed its camera, the low look is on and the distance rule has collected
// (execution order 218), before that frame's picture: the loading screen stays up a little longer and every frame after it is the
// game's own. A job's own slices go on at once; the first frame inside a routine it waits on (a warm render that must be drawn once) and
// any wait go back to the frames from there, as before. Every job does the same work in the same order; the land beyond the rim is ready
// with the first picture (it stands back under the low shots as before), so nothing drawn changes while the flyover runs.
// A red start and the other levels: exactly as before (each job is its own routine over the frames).
// Play check: return OpenZ.OzStartAhead.Status();
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    [DefaultExecutionOrder(218)]
    public sealed class OzStartAhead : MonoBehaviour
    {
        public static OzStartAhead I { get; private set; }
        /// <summary>Off = every start job runs over the first frames as before.</summary>
        public static bool On = true;
        /// <summary>The levels whose start does it (level ids, comma separated), and whether a red start does it too (no: as before).</summary>
        public static string Levels = "L16";
        public static bool RedToo;
        /// <summary>The most the first frame may give the jobs (ms); past it the rest goes back to the frames.</summary>
        public static float MaxMs = 6000f;
        /// <summary>A full garbage collection after the jobs, still before the first picture (the jobs leave their building lists behind).</summary>
        public static bool CollectAfter = true;
        /// <summary>The sounds that began with the level (the jungle, the briefing window's opening crackle) wait while the jobs run, so they
        /// go on with the first picture as they did before (the jobs are one stretch of work: no frame and no other script runs in it).</summary>
        public static bool QuietWhileWorking = true;
        /// <summary>Counters of this level's start: done, the jobs' time in the first frame, the collection's, jobs taken, slices gone on at
        /// once, jobs whose rest went back to the frames, over the time limit, failed.</summary>
        public static bool Done { get; private set; }
        public static float WorkMs, CollectMs;
        public static int Jobs, Slices, HandedBack, OverTime, Failed;
        public static string LastLine = "not run", WhyNot = "no level loaded yet";

        sealed class Job { public MonoBehaviour owner; public string what; public OzStartAheadPump pump; public float ms; }
        readonly List<Job> queue = new List<Job>();
        readonly StringBuilder parts = new StringBuilder();
        bool pumped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            I = null; On = true; Levels = "L16"; RedToo = false; MaxMs = 6000f; CollectAfter = true; QuietWhileWorking = true;
            Clear(); WhyNot = "no level loaded yet";
        }

        static void Clear() { Done = false; WorkMs = CollectMs = 0f; Jobs = Slices = HandedBack = OverTime = Failed = 0; LastLine = "not run"; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; }

        static void OnScene(Scene s, LoadSceneMode m)
        {
            if (m != LoadSceneMode.Single || !Application.isPlaying) return;
            Clear();
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!g) { WhyNot = "not a level"; return; }
            if (!Wanted(g.level, OzPlayBlue.Active, out WhyNot) || I) return;
            var go = new GameObject("OzStartAhead");
            if (s.IsValid() && go.scene != s) SceneManager.MoveGameObjectToScene(go, s);
            go.AddComponent<OzStartAhead>();
        }

        /// <summary>True when a start of `level` with this side does its start work before the first picture; why not otherwise.</summary>
        public static bool Wanted(string level, bool blue, out string why)
        {
            bool listed = false;
            foreach (var l in (Levels ?? "").Split(',')) if (l.Trim().Length > 0 && l.Trim() == level) listed = true;
            bool both = OzLevelWarm.StartAheadBothSides(level); if (both) listed = true;              // 1.4: a level added from 1.4 on (level 7 first), on either side (View/OzLevelWarm.cs); the three 1.2 levels as before
            why = !On ? "off (OzStartAhead.On)" : !listed ? "not this level (" + level + "; OzStartAhead.Levels " + Levels + ")"
                : !blue && !RedToo && !both ? "a red start: as before (OzStartAhead.RedToo)" : "";
            return why.Length == 0;
        }

        /// <summary>A start job (instead of StartCoroutine in its Start): on a start this is for, its first step now and the rest in the
        /// level's first frame before its first picture; otherwise its own routine over the frames, exactly as before.</summary>
        public static void Run(MonoBehaviour owner, IEnumerator work, string what)
        {
            if (!owner || work == null) return;
            var d = I;
            if (!d || d.pumped || !owner.isActiveAndEnabled) { owner.StartCoroutine(work); return; }
            var job = new Job { owner = owner, what = what, pump = new OzStartAheadPump(work, IsWait) };
            float t0 = Time.realtimeSinceStartup;
            var r = job.pump.First();
            job.ms += (Time.realtimeSinceStartup - t0) * 1000f;
            if (r == OzStartAheadPump.Result.Failed) { Failed++; Debug.LogException(job.pump.Error); return; }
            if (r == OzStartAheadPump.Result.Finished) return;
            if (r == OzStartAheadPump.Result.Waits) { d.Hand(job); return; }
            d.queue.Add(job);
        }

        /// <summary>The engine's own waits that are routines too (WaitUntil, WaitWhile, a request): the frames keep them.</summary>
        public static bool IsWait(object o) => o is CustomYieldInstruction;

        void Awake() { I = this; }
        void OnDestroy() { if (I == this) I = null; }

        void LateUpdate()
        {
            if (pumped) return;
            pumped = true;
            bool hush = QuietWhileWorking && queue.Count > 0 && !AudioListener.pause;
            if (hush) AudioListener.pause = true;
            try { Work(); }
            finally { if (hush) AudioListener.pause = false; }
            if (hush) LastLine += "; the level's sounds waited with it";
            Debug.Log("[OzStartAhead] " + LastLine);
        }

        void Work()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            parts.Length = 0;
            foreach (var job in queue)
            {
                if (!job.owner || !job.owner.isActiveAndEnabled) { Note(job.what + ": its object is gone (as its routine would have stopped)"); continue; }
                Jobs++;
                double left = MaxMs - sw.Elapsed.TotalMilliseconds;
                double j0 = sw.Elapsed.TotalMilliseconds;
                var r = left > 0 ? job.pump.Run(left) : OzStartAheadPump.Result.OverTime;
                job.ms += (float)(sw.Elapsed.TotalMilliseconds - j0);
                Slices += job.pump.Slices;
                string tail = job.what + " " + job.ms.ToString("0") + " ms, " + job.pump.Slices + " slices";
                if (r == OzStartAheadPump.Result.Failed) { Failed++; Debug.LogException(job.pump.Error); Note(tail + ", stopped by an error"); continue; }
                if (r == OzStartAheadPump.Result.OverTime) OverTime++;
                if (r != OzStartAheadPump.Result.Finished) { Hand(job); tail += ", the rest in the frames from " + job.pump.StopWhy; }
                Note(tail);
            }
            queue.Clear();
            WorkMs = (float)sw.Elapsed.TotalMilliseconds;
            if (CollectAfter) { var c = System.Diagnostics.Stopwatch.StartNew(); System.GC.Collect(); CollectMs = (float)c.Elapsed.TotalMilliseconds; }
            Done = true;
            var g = ZGame.I;
            LastLine = $"{(g ? g.level : "?")} {(OzPlayBlue.Active ? "blue" : "red")} start: the start work done before the first picture in {WorkMs:0} ms ({parts}); "
                     + $"{HandedBack} went back to the frames, {OverTime} over the time limit, {Failed} failed; garbage collected {(CollectAfter ? CollectMs.ToString("0") + " ms" : "off")}";
        }

        void Note(string s) { if (parts.Length > 0) parts.Append("; "); parts.Append(s); }

        void Hand(Job job)
        {
            if (!job.owner || !job.owner.isActiveAndEnabled) return;                                     // its routine would have stopped with it
            HandedBack++;
            job.owner.StartCoroutine(job.pump.Rest());
        }

        /// <summary>One-line state for the Play check.</summary>
        public static string Status() => Done ? "OzStartAhead: " + LastLine
            : I ? $"OzStartAhead: waiting for the level's first frame ({I.queue.Count} jobs taken)" : "OzStartAhead: not on this start: " + WhyNot;
    }
}
