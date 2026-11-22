// OpenZ (GPL-3.0) - version 1.3 item 38 (log only): four more numbers in item 25's once-a-minute "[OpenZ Perf] minute N" line
// (View/OzPerfLine.cs). They decide, with numbers from a player's own PC, whether the hover pre-cull, a cheaper health-bar overlay
// and the garbage work are worth doing. Nothing is drawn, nothing is saved, no rule is read or changed.
//  * garbage: managed memory made per frame. The editor and a development exe have the engine's "GC Allocated In Frame" counter
//    (exact, all threads). A release exe has neither that counter (6000.3.25f1: the name is only in the development UnityPlayer.dll)
//    nor GC.GetAllocatedBytesForCurrentThread (IL2CPP: "not implemented", returns 0; Unity's Boehm Mono returns 0), so it prints the
//    managed heap's growth per frame (GC.GetTotalMemory(false), the rises only): an estimate of the garbage. It counts whole heap
//    blocks taken, so it can read low when memory that a collection freed inside a half-used block is reused, and a little high from
//    object headers (harness on Unity's Boehm Mono: 19.5-25.2 KB for 20.0 KB made). The editor prints both, so they can be compared.
//  * collections: GC.CollectionCount(0) over the minute (Mono and IL2CPP both count every finished collection), and the managed heap
//    in use at the end of the minute.
//  * overlay: the unit overlay's own time a frame (UI/OpenZUnitOverlay.cs LateUpdate: selection frames, health bars, crate marks).
//  * hover: the mouse hover pick's time a frame (View/PlayerInput.cs HoverAt).
// Each is the minute's total divided by the minute's frames ("a frame"), and the worst single frame.
// Cost a frame: 4 Stopwatch reads, 1 heap read, a few additions. No string and no allocation; the text is written once a minute
// into item 25's StringBuilder.
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace OpenZ
{
    /// <summary>1.3 item 38: the garbage, overlay and hover numbers of the per-minute perf line (log only, main thread only).</summary>
    public static class OzPerfNumbers
    {
        static long overlaySum, overlayWorst, hoverSum, hoverWorst;
        static int overlayN, hoverN;
        static long unitViewSum, rockOcclusionSum;
        static long heapLast = -1, heapGrowth, allocSum;
        static bool counterSeen;
        static int gcAtStart;

        /// <summary>A Stopwatch timestamp (the start of a timed part).</summary>
        public static long Now() => Stopwatch.GetTimestamp();

        public static void UnitViewDone(long started) => unitViewSum += Stopwatch.GetTimestamp() - started;
        public static void RockOcclusionDone(long started) => rockOcclusionSum += Stopwatch.GetTimestamp() - started;

        /// <summary>UI/OpenZUnitOverlay.cs: its LateUpdate started at t0 and has ended.</summary>
        public static void OverlayDone(long t0)
        {
            long d = Stopwatch.GetTimestamp() - t0;
            overlaySum += d; overlayN++; if (d > overlayWorst) overlayWorst = d;
        }

        /// <summary>View/PlayerInput.cs: HoverAt started at t0 and has ended.</summary>
        public static void HoverDone(long t0)
        {
            long d = Stopwatch.GetTimestamp() - t0;
            hoverSum += d; hoverN++; if (d > hoverWorst) hoverWorst = d;
        }

        /// <summary>A level started: the heap growth starts again from this level's first frame (not from the loading).</summary>
        public static void LevelStart() { heapLast = -1; }

        /// <summary>A new minute (and a new level): every sum back to zero, the collection count read.</summary>
        public static void Reset()
        {
            overlaySum = overlayWorst = hoverSum = hoverWorst = 0; overlayN = hoverN = 0;
            unitViewSum = rockOcclusionSum = 0;
            heapGrowth = allocSum = 0; counterSeen = false;
            gcAtStart = GC.CollectionCount(0);
        }

        /// <summary>Once a frame while a level runs (View/OzPerfLine.cs LateUpdate). counter: the engine's "GC Allocated In Frame"
        /// recorder is valid; allocLastFrame: its last value (bytes).</summary>
        public static void Frame(bool counter, long allocLastFrame)
        {
            long used = GC.GetTotalMemory(false);
            if (heapLast >= 0 && used > heapLast) heapGrowth += used - heapLast;
            heapLast = used;
            if (counter && allocLastFrame > 0) { allocSum += allocLastFrame; counterSeen = true; }
        }

        /// <summary>The minute's part of the perf line (frames: the minute's frames).</summary>
        public static void AppendMinute(StringBuilder sb, int frames)
        {
            double f = Math.Max(1, frames);
            sb.Append("garbage ");
            if (counterSeen) sb.Append(F(allocSum / f / 1024.0, "0.0")).Append(" KB a frame, heap growth ");
            else sb.Append("n/a (release exe), heap growth ");
            sb.Append(F(heapGrowth / f / 1024.0, "0.0")).Append(" KB a frame, collections ").Append(GC.CollectionCount(0) - gcAtStart)
              .Append(", heap ").Append(F(Math.Max(0, heapLast) / 1048576.0, "0.0")).Append(" MB; ");
            sb.Append("overlay ").Append(Ms(overlaySum, overlayN, f)).Append(" ms a frame (worst ").Append(Ms(overlayWorst, overlayN, 1)).Append("), ");
            sb.Append("hover ").Append(Ms(hoverSum, hoverN, f)).Append(" ms a frame (worst ").Append(Ms(hoverWorst, hoverN, 1)).Append("); ");
            sb.Append("unit views CPU ").Append(Ms(unitViewSum, frames, f)).Append(" ms a frame, rock occlusion ")
              .Append(Ms(rockOcclusionSum, frames, f)).Append(" ms a frame; ");
        }

        static string Ms(long ticks, int n, double per) => n > 0 ? F(ticks * 1000.0 / Stopwatch.Frequency / per, "0.000") : "n/a";
        static string F(double v, string f) => v.ToString(f, CultureInfo.InvariantCulture);
    }
}
