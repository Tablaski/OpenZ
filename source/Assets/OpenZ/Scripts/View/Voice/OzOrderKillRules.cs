// OpenZ (GPL-3.0) - version 1.2, items 62 and 63: the pacing rules of the no-way answer and of the kill lines, free of the engine
// (View/Voice/AudioDirector.NoWayLines.cs and View/Voice/AudioDirector.KillLines.cs use them; the off-engine checks run this file as it is).
using System;
using System.Collections.Generic;

namespace OpenZ
{
    /// <summary>
    /// Version 1.2 item 62: the pacing of the no-way answer. Zod: a unit sent alone to attack a unit it cannot beat answers with a
    /// no-way line instead of its acknowledgement, and still goes (zplayer.cpp DevWayPointsNoWay, zobject.cpp PlayAcknowledgeAnim).
    /// Here: at most one no-way answer per <see cref="Gap"/> s (an order inside that gap gets its normal answer); the line is never the
    /// one said last while the pool has another; it never starts over another voice: it waits until the voices have been quiet for
    /// <see cref="After"/> s, at most <see cref="Wait"/> s after the order. A line that could not start in time is dropped and the gap
    /// does not start (the next such order may say one).
    /// </summary>
    public sealed class OzNoWayGate
    {
        /// <summary>Seconds between two no-way answers, the longest wait for quiet voices after the order, the breath after them.</summary>
        public double Gap = 25.0, Wait = 3.0, After = 0.25;
        double last = -1e9, askedAt, quietSince = -1.0;
        string lastLine, pending;
        /// <summary>Checks: lines asked (an order that may answer), said, orders inside the gap (normal answer), lines dropped.</summary>
        public int Asked, Said, Cooling, Dropped;
        public string LastLine => lastLine;
        public string Pending => pending;
        public bool Waiting => pending != null;
        /// <summary>When the last no-way line started (the clock of the caller).</summary>
        public double Last => last;

        /// <summary>An order Zod answers with a no-way line: true = this one may; false = the normal answer (inside the gap, or a line waits).</summary>
        public bool Due(double now)
        {
            if (pending != null || now - last < Gap) { Cooling++; return false; }
            return true;
        }

        /// <summary>Picks the line (each distinct line of the pool alike, never the last one said while another exists) and starts the
        /// wait. null = the pool has no line.</summary>
        public string Ask(IList<string> lines, double now, Func<double> rnd)
        {
            var c = new List<string>();
            if (lines != null) foreach (var l in lines) if (!string.IsNullOrEmpty(l) && !c.Contains(l)) c.Add(l);
            if (c.Count == 0) return null;
            if (c.Count > 1 && lastLine != null) c.Remove(lastLine);
            int i = (int)(rnd() * c.Count);
            if (i >= c.Count) i = c.Count - 1;
            if (i < 0) i = 0;
            pending = c[i]; askedAt = now; quietSince = -1.0; Asked++;
            return pending;
        }

        public enum Step { Idle, Wait, Say, Drop }

        /// <summary>Once a frame while a line waits. quiet = no voice the robots must not talk over plays now.</summary>
        public Step Tick(double now, bool quiet)
        {
            if (pending == null) return Step.Idle;
            if (!quiet) quietSince = -1.0;
            else if (quietSince < 0.0) quietSince = now;
            if (quiet && now - quietSince >= After) return Step.Say;
            if (now - askedAt > Wait) { pending = null; Dropped++; return Step.Drop; }
            return Step.Wait;
        }

        /// <summary>The waiting line was handed to the voice channel: played = it started (the gap starts now), else another rule of the
        /// channel held it (counted as dropped, no gap).</summary>
        public void Spoke(double now, bool played)
        {
            if (pending == null) return;
            if (played) { last = now; lastLine = pending; Said++; }
            else Dropped++;
            pending = null;
        }
    }

    /// <summary>
    /// Version 1.2 item 63: a shuffled bag of the LINES of a pool (not of its takes): every line once before any line comes again,
    /// never the same line twice in a row (also where two bags meet). <see cref="Peek"/> gives the next line; <see cref="Take"/> marks
    /// it said. A line that could not be said stays first for the next try, so no line is skipped by a busy moment.
    /// </summary>
    public sealed class OzLineBag
    {
        readonly List<string> bag = new List<string>();
        string last, key;

        public string Peek(IList<string> lines, Func<double> rnd)
        {
            var d = new List<string>();
            if (lines != null) foreach (var l in lines) if (!string.IsNullOrEmpty(l) && !d.Contains(l)) d.Add(l);
            string k = string.Join("|", d);
            if (k != key) { bag.Clear(); key = k; }                                   // the pool changed (a reload): a new bag
            if (bag.Count == 0 && d.Count > 0)
            {
                bag.AddRange(d);
                for (int i = bag.Count - 1; i > 0; i--)
                {
                    int j = (int)(rnd() * (i + 1));
                    if (j > i) j = i;
                    if (j < 0) j = 0;
                    var t = bag[i]; bag[i] = bag[j]; bag[j] = t;
                }
                if (bag.Count > 1 && bag[0] == last) { var t = bag[0]; bag[0] = bag[bag.Count - 1]; bag[bag.Count - 1] = t; }
            }
            return bag.Count > 0 ? bag[0] : null;
        }

        public void Take(string line)
        {
            if (bag.Count > 0 && bag[0] == line) bag.RemoveAt(0);
            last = line;
        }
    }

    /// <summary>
    /// Version 1.2 item 63: when the cheer after a good kill (the "boom" lines) may come. Before, only once the fight there was over
    /// (no armed enemy near the killer and the killer attacking nothing for quiet s) within the wait after the kill, else it was dropped:
    /// in a battle the next enemy is always near, so it almost never came. Now: such a lull as before, or else lull s after the kill
    /// whatever the fight; the caller then still waits for quiet voices, at most its wait after the kill.
    /// </summary>
    public static class OzCheerRule
    {
        public static bool Moment(double now, double killAt, bool fight, double quietSince, double quiet, double lull) =>
            (!fight && quietSince >= 0.0 && now - quietSince >= quiet) || now - killAt >= lull;
    }
}
