// OpenZ (GPL-3.0)
// the same file is also built and run on its own by the kit's off-line test).
// A start job is a routine written for the frames: its plain yields (yield return null at its own level) are its slices, "the next
// piece of work in the next frame". Run goes on through those at once. It steps into a routine the job waits on (yield return
// SomeRoutine()), and it stops, keeping the rest for the frames, at:
//  - a plain yield inside such a routine (a warm render: its objects must be drawn once in a frame, then moved, then removed),
//  - any other yield (a wait for time, for the end of a frame, for a load; isWait tells which routines are waits),
//  - the time limit.
// Rest() is that rest as a routine for the frames: the yield it stopped at first, then on from there, exactly as before.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenZ
{
    public sealed class OzStartAheadPump
    {
        public enum Result { Finished, Queued, Waits, OverTime, Failed }

        readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        readonly Func<object, bool> isWait;
        object held; bool hasHeld;

        /// <summary>Counters: plain yields gone on at once (slices), routines stepped into; why it stopped; the error that ended it.</summary>
        public int Slices { get; private set; }
        public int Nested { get; private set; }
        public string StopWhy { get; private set; } = "";
        public Exception Error { get; private set; }
        public bool Empty => stack.Count == 0;

        public OzStartAheadPump(IEnumerator work, Func<object, bool> isWait)
        {
            if (work != null) stack.Push(work);
            this.isWait = isWait;
        }

        /// <summary>The job's first step only, as a routine's start runs it: Finished (nothing left), Queued (a plain yield: the rest can go
        /// on later in one go), Waits (it yielded something else first: the rest belongs to the frames), Failed.</summary>
        public Result First()
        {
            if (stack.Count == 0) return Result.Finished;
            var top = stack.Peek();
            bool more;
            try { more = top.MoveNext(); }
            catch (Exception e) { Error = e; stack.Clear(); StopWhy = "an error in its first step"; return Result.Failed; }
            if (!more) { stack.Clear(); return Result.Finished; }
            if (top.Current == null) return Result.Queued;
            Hold(top.Current, "its first step yields " + Kind(top.Current));
            return Result.Waits;
        }

        /// <summary>On until the job is done (Finished), a yield needs the frames (Waits: Rest() goes on from there), maxMs passed
        /// (OverTime: Rest() goes on with the next step) or it threw (Failed: dropped, as a routine that throws stops).</summary>
        public Result Run(double maxMs)
        {
            var sw = Stopwatch.StartNew();
            while (stack.Count > 0)
            {
                if (sw.Elapsed.TotalMilliseconds >= maxMs) { StopWhy = "the time limit"; return Result.OverTime; }
                var top = stack.Peek();
                bool more;
                try { more = top.MoveNext(); }
                catch (Exception e) { Error = e; stack.Clear(); StopWhy = "an error"; return Result.Failed; }
                if (!more) { stack.Pop(); continue; }
                var cur = top.Current;
                if (cur == null)
                {
                    if (stack.Count == 1) { Slices++; continue; }                                 // the job's own slice: the next piece now
                    Hold(null, "a frame inside a routine it waits on (" + Kind(top) + ")");     // a warm render: drawn once in a frame first
                    return Result.Waits;
                }
                if (cur is IEnumerator inner && !(isWait != null && isWait(cur))) { stack.Push(inner); Nested++; continue; }
                Hold(cur, "a wait (" + Kind(cur) + ")");
                return Result.Waits;
            }
            StopWhy = "done";
            return Result.Finished;
        }

        /// <summary>The rest of the job as a routine for the frames: the yield it stopped at, then on from where it was.</summary>
        public IEnumerator Rest()
        {
            if (hasHeld) { hasHeld = false; var h = held; held = null; yield return h; }
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                yield return top.Current;
            }
        }

        void Hold(object o, string why) { held = o; hasHeld = true; StopWhy = why; }

        static string Kind(object o)
        {
            if (o == null) return "null";
            var n = o.GetType().Name;
            int i = n.IndexOf('>');                                                                       // a compiler-made routine: <WarmRender>d__12
            return i > 1 && n[0] == '<' ? n.Substring(1, i - 1) : n;
        }
    }
}
