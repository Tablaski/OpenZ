// OpenZ (GPL-3.0) - version 1.4, camera_response: the camera answers the player sooner; nothing on screen looks different.
//  * 5 Nov Play check: the frame queue part is OFF (On = false, the engine's own 2 frames): queue 1 cost frame rate. What follows describes it when on.
//  * Frames queued: QualitySettings.maxQueuedFrames 1 (the engine's own is 2). When the graphics chip is the slower part of a frame,
//    the processor no longer runs two frames ahead of it, so a key, the mouse or the wheel reaches the screen one frame sooner
//    (17 ms at 60 fps, 25 ms at 40, 40 ms at 25). The same on every FRAME RATE LIMIT row: 30 / 40 / 60 keep their cap with vsync
//    off, UNLIMITED (VSYNC) keeps vsync on (then at most one finished frame waits for the screen's refresh), and the heat guard's
//    30 is as before (OzFpsCap.ApplyLimit decides the cap and vsync; this only decides the queue). OzCamResponseKeeper puts the
//    value back each frame (one property read), so a row change, the heat guard or the engine cannot undo it.
//  * Pan window: the scroll's pan steps by the mean of the last 4 frame times (1.3 item 25 g, OzCamFeel.PanClock), even steps when
//    frame times swing. 4 frames last 67 ms at 60 fps but 160 ms at 25, so a change of the frame time took that long to reach the
//    pan. Now, while the last 4 frames took longer than PanWindowSeconds (0.09 s: slower than about 45 fps, or a long frame among
//    them), the mean is over the last 2: the same distance, still even steps when frame times swing in turn (7 / 8 rule ticks), and
//    a window of at most about 90 ms down to 22 fps. At 45 fps and faster nothing changes: 4 frames, the same steps bit for bit.
//  * Where the camera reads its input: the input system reads the keyboard and the mouse once a frame, before any script's Update.
//    RtsCamera.Update reads that state and moves the camera in the same frame (0 frames between the input and the moved camera,
//    counted by Editor/OzV14CamResponseCheck.cs for the key scroll, the screen edge, the drag, the wheel and the turn). There is no
//    later reading inside a frame, so the camera keeps its place in the script order (order 0, where many scripts read its pose).
// Switches for the before / after check: On (the queue) and PanWindowOn. View only: the rules never read any of it.
using UnityEngine;

namespace OpenZ
{
    public static class OzCamResponse
    {
        public const string Tag = "[OpenZ Camera]";
        /// <summary>The frame queue at its least (1). Off = the engine's own value (EngineQueued), as before 1.4. 1.4 Play check (5 Nov):
        /// OFF, because queue 1 cost frames (60 cap: 60.0 -> 50.1 fps; graphics chip loaded: 33.9 -> 23.9) and 1.4 never trades frame
        /// rate. Only the pan window below ships. The delay check's after pass keeps this value.</summary>
        public static bool On = ShipOn;
        /// <summary>The shipped value of On: the start value and the one ResetStatics puts back at every Play start and exe start, so the two
        /// can never drift apart.</summary>
        const bool ShipOn = false;
        /// <summary>The pan window in seconds (PanFrames). Off = always the last 4 frames, as in 1.3.</summary>
        public static bool PanWindowOn = true;
        /// <summary>Frames the driver may queue while On.</summary>
        public const int QueuedFrames = 1;
        /// <summary>The longest time the 4-frame mean of the pan may span (s); longer = the mean of the last 2 frames.</summary>
        public static float PanWindowSeconds = 0.09f;
        /// <summary>The engine's own frame queue, read at the start before this changes it (-1 = not read yet).</summary>
        public static int EngineQueued = -1;
        /// <summary>Checks: times the queue was put back to the wanted value; frames whose pan mean was taken over 2 frames.</summary>
        public static int Sets, PanNarrowed;
        static int seenVsync = int.MinValue, seenTarget = int.MinValue, seenQueue = int.MinValue;
        static bool booted;

        /// <summary>The frame queue wanted now.</summary>
        public static int Want => On ? QueuedFrames : (EngineQueued > 0 ? EngineQueued : 2);

        /// <summary>OzCamFeel.PanClock: how many of the last frame times the pan's mean takes. n = the count the clock would take
        /// (PanAverageFrames, fewer at the very start); dts = its ring of frame times, the newest at at - 1. Unchanged while those n
        /// frames lasted PanWindowSeconds or less, else 2 (n of 2 or less is never changed). Plain arithmetic, no engine call.</summary>
        public static int PanFrames(float[] dts, int at, int n)
        {
            if (!PanWindowOn || n <= 2 || dts == null) return n;
            int len = dts.Length; float s = 0f;
            for (int i = 1; i <= n; i++) s += dts[((at - i) % len + len) % len];
            if (s <= PanWindowSeconds) return n;
            PanNarrowed++;
            return 2;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            On = ShipOn; PanWindowOn = true; PanWindowSeconds = 0.09f; Sets = 0; PanNarrowed = 0; booted = false;
            seenVsync = seenTarget = seenQueue = int.MinValue;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (booted) return;
            booted = true;
            if (EngineQueued < 0) EngineQueued = QualitySettings.maxQueuedFrames;
            Queue();                                                                                      // the first frame already runs with it; the keeper's first frame writes the log line (after GameBoot set the frame limit)
            var go = new GameObject("OzCamResponse"); Object.DontDestroyOnLoad(go); go.AddComponent<OzCamResponseKeeper>();
            Application.quitting -= PutBack; Application.quitting += PutBack;                            // the editor: Play stop leaves the editor's own queue
        }

        /// <summary>Puts the frame queue at Want if anything changed it; logs the queue with the frame limit and vsync whenever one of
        /// the three changes (the start, a FRAME RATE LIMIT row, the heat guard).</summary>
        public static void Keep()
        {
            Queue();
            int v = QualitySettings.vSyncCount, t = Application.targetFrameRate, q = QualitySettings.maxQueuedFrames;
            if (v == seenVsync && t == seenTarget && q == seenQueue) return;
            bool first = seenQueue == int.MinValue;
            seenVsync = v; seenTarget = t; seenQueue = q;
            Debug.Log(first
                ? $"{Tag} response (1.4): frames queued {q} (the engine's own {EngineQueued}), vsync {v}, frame limit {(t > 0 ? t.ToString() : "none")}; pan window {(PanWindowOn ? PanWindowSeconds.ToString("0.00") + " s" : "off")}"
                : $"{Tag} frames queued {q}, vsync {v}, frame limit {(t > 0 ? t.ToString() : "none")}");
        }

        /// <summary>The frame queue to Want, when it is not.</summary>
        static void Queue()
        {
            int want = Want;
            if (QualitySettings.maxQueuedFrames != want) { QualitySettings.maxQueuedFrames = want; Sets++; }
        }

        static void PutBack()
        {
            if (Application.isEditor && EngineQueued > 0 && QualitySettings.maxQueuedFrames != EngineQueued) QualitySettings.maxQueuedFrames = EngineQueued;
        }
    }

    /// <summary>Holds OzCamResponse's frame queue (one hidden object for the whole run, made at the start).</summary>
    public sealed class OzCamResponseKeeper : MonoBehaviour
    {
        void Update() { OzCamResponse.Keep(); }
    }
}
