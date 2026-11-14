// OpenZ (GPL-3.0) - version 1.4, camera_response: the Play probe of Editor/OzV14CamResponseCheck.cs. Only that check adds it; the game
// never does.
// Each frame: when the frame's scripts start (OzCamResponseProbe, order -32050: before every other script, after the input system read
// the devices) the device state the camera reads this frame and the clock; after every script (OzCamResponseProbeLate, order 32050)
// the camera as it is drawn this frame, and the frame timing figures of finished frames (FrameTimingManager: when the graphics chip
// finished each frame). A staged action is sent as input events at the end of a frame (a coroutine resumes after the frame's Update),
// so the input system reads it at the start of the next frame: that frame is the input sample, and the delay is the frames from it to
// the first frame whose drawn camera moved, plus the milliseconds from the sample to the graphics chip's end of that frame.
// Actions: key scroll (an arrow key), screen edge (the mouse 1 px from the left or right edge), middle drag (FREE CAMERA off), wheel
// zoom (one notch), free turn (FREE CAMERA on). The mouse stays in the middle of the screen between them.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    [DefaultExecutionOrder(-32050)]
    public sealed class OzCamResponseProbe : MonoBehaviour
    {
        public enum Act { Key, Edge, Drag, Wheel, Turn }
        public static readonly string[] ActNames = { "key scroll", "screen edge", "middle drag", "wheel zoom", "free turn" };
        /// <summary>Frames an action is held (the wheel: one notch); frames waited after it for the timing figures.</summary>
        public static int HoldFrames = 8, TailFrames = 8;
        /// <summary>Mouse travel per frame of the drag and the turn (px).</summary>
        public static float DeltaPx = 12f;

        /// <summary>One frame: the device state at its start and the camera after every script.</summary>
        public struct Frame
        {
            public int frame, key; public long t0; public float dt; public bool mid; public Vector2 pos, delta; public float scroll;
            public bool late, testing; public Vector3 focus; public float height, yaw, tilt;
        }
        /// <summary>One staged action and what it gave.</summary>
        public struct Rep
        {
            public Act act; public int fps; public bool after; public int inject, sample, moved; public string note;
            public double ms; public int inFlight; public float frameMs;
        }
        /// <summary>A finished frame's timing figures (FrameTimingManager), clock ticks.</summary>
        public struct Timing { public ulong start, complete, present; public double cpuMs, gpuMs; }
        /// <summary>One stage (a frame rate in one mode): its frames a second and the queue the driver held.</summary>
        public struct Stage { public int fps; public bool after, gpuLoad; public float measuredFps; public int queued, firstFrame, lastFrame; }

        public static OzCamResponseProbe I { get; private set; }
        public readonly Dictionary<int, Frame> frames = new Dictionary<int, Frame>();
        public readonly List<Rep> reps = new List<Rep>();
        public readonly List<Stage> stages = new List<Stage>();
        public readonly Dictionary<ulong, Timing> timings = new Dictionary<ulong, Timing>();
        public RtsCamera cam;
        public bool Busy { get; private set; }
        public string Status = "";
        /// <summary>The clock of the timing figures is the one this probe stamps frames with (same tick rate).</summary>
        public bool SameClock;
        public double TickHz = System.Diagnostics.Stopwatch.Frequency;
        System.Reflection.PropertyInfo freeCamOn;
        bool freeCam0, freeCamKnown;
        Vector3 focus0; float height0;
        int cap0, target0, vsync0; bool queue0, pan0;
        Camera loadCam; RenderTexture loadRt;

        public static OzCamResponseProbe Attach(RtsCamera cam)
        {
            if (I) return I;
            var go = new GameObject("OzCamResponseProbe");
            var p = go.AddComponent<OzCamResponseProbe>(); go.AddComponent<OzCamResponseProbeLate>();
            p.cam = cam; I = p;
            ulong hz = FrameTimingManager.GetCpuTimerFrequency();
            p.SameClock = hz > 0 && (double)hz == System.Diagnostics.Stopwatch.Frequency;
            if (hz > 0) p.TickHz = hz;
            return p;
        }

        void OnDestroy() { if (Busy) { Busy = false; PutBack(); } if (I == this) I = null; }                  // a run cut short still puts everything back

        void Update()
        {
            var kb = Keyboard.current; var m = Mouse.current;
            var f = new Frame
            {
                frame = Time.frameCount, t0 = System.Diagnostics.Stopwatch.GetTimestamp(), dt = Time.unscaledDeltaTime,
                key = kb == null ? 0 : (kb.rightArrowKey.isPressed ? 1 : 0) - (kb.leftArrowKey.isPressed ? 1 : 0),
                mid = m != null && m.middleButton.isPressed, pos = m != null ? m.position.ReadValue() : Vector2.zero,
                delta = m != null ? m.delta.ReadValue() : Vector2.zero, scroll = m != null ? m.scroll.ReadValue().y : 0f,
                testing = FreeCamOn() || OzCheatSpawner.Visible || OzUeHooks.Active,                          // RtsCamera's own test for the middle button's turn
            };
            frames[f.frame] = f;
        }

        /// <summary>OzCamResponseProbeLate: the camera after every script of this frame, and the finished frames' timing figures.</summary>
        internal void Late(FrameTiming[] buf)
        {
            if (frames.TryGetValue(Time.frameCount, out var f) && cam)
            {
                f.late = true; f.focus = cam.focus; f.height = cam.height; f.yaw = cam.yaw; f.tilt = cam.Tilt;
                frames[f.frame] = f;
            }
            FrameTimingManager.CaptureFrameTimings();
            uint n = FrameTimingManager.GetLatestTimings((uint)buf.Length, buf);
            for (int i = 0; i < n; i++)
            {
                var t = buf[i];
                if (t.frameStartTimestamp == 0 || timings.ContainsKey(t.frameStartTimestamp)) continue;
                timings[t.frameStartTimestamp] = new Timing { start = t.frameStartTimestamp, complete = t.cpuTimeFrameComplete, present = t.cpuTimePresentCalled, cpuMs = t.cpuFrameTime, gpuMs = t.gpuFrameTime };
            }
        }

        // ------------------------------------------------------------------ switches the check stages
        bool FreeCamOn()
        {
            if (freeCamOn == null) { var t = System.Type.GetType("OpenZ.OzFreeCam"); freeCamOn = t != null ? t.GetProperty("On") : null; if (freeCamOn == null) return false; }
            return (bool)freeCamOn.GetValue(null);
        }
        bool SetFreeCam(bool on)
        {
            FreeCamOn();
            if (freeCamOn == null || !freeCamOn.CanWrite) return false;
            if (!freeCamKnown) { freeCam0 = (bool)freeCamOn.GetValue(null); freeCamKnown = true; }
            if ((bool)freeCamOn.GetValue(null) != on) freeCamOn.SetValue(null, on);
            return true;
        }

        static void Keys(params Key[] k) { if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k)); }
        static void MouseAt(Vector2 p, Vector2 delta, float scroll, bool middle)
        {
            var m = Mouse.current; if (m == null) return;
            var s = new MouseState { position = p, delta = delta, scroll = new Vector2(0f, scroll) };
            if (middle) s = s.WithButton(MouseButton.Middle);
            InputSystem.QueueStateEvent(m, s);
        }
        static Vector2 Mid => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // ------------------------------------------------------------------ the run
        /// <summary>The whole run: for each frame rate, before / after / before / after (before = the engine's own frame queue and
        /// the 1.3 pan window), each action `repsPerAct` times. gpuLoad: a hidden second camera draws the view at `gpuScale` times
        /// the screen's size into a texture nobody sees, so the graphics chip is the slower part (the queue only shows there).</summary>
        public IEnumerator Run(int[] fpsList, int repsPerAct, bool gpuLoad, float gpuScale)
        {
            Busy = true; Status = "running";
            cap0 = GameBoot.CapFps; target0 = Application.targetFrameRate; vsync0 = QualitySettings.vSyncCount; queue0 = OzCamResponse.On; pan0 = OzCamResponse.PanWindowOn;
            focus0 = cam.focus; height0 = cam.height;
            try
            {
                if (gpuLoad) MakeLoad(gpuScale);
                MouseAt(Mid, Vector2.zero, 0f, false); Keys();
                cam.focus = new Vector3(cam.mapSizeMeters.x * 0.5f, 0f, -cam.mapSizeMeters.y * 0.5f); cam.SetHeightImmediate(70f);
                foreach (int fps in fpsList)
                    for (int round = 0; round < 4; round++)
                        yield return StageRun(fps, round % 2 == 1, repsPerAct, gpuLoad);
            }
            finally { PutBack(); }
            Status = "done"; Busy = false;
        }

        void PutBack()
        {
            GameBoot.CapFps = cap0; Application.targetFrameRate = target0; QualitySettings.vSyncCount = vsync0;
            OzCamResponse.On = queue0; OzCamResponse.PanWindowOn = pan0; OzCamResponse.Keep();
            if (freeCamKnown) { SetFreeCam(freeCam0); freeCamKnown = false; }
            Keys(); MouseAt(Mid, Vector2.zero, 0f, false);
            if (cam) { cam.FreeLookOff(); cam.focus = focus0; cam.SetHeightImmediate(height0); }
            if (loadCam) Destroy(loadCam.gameObject);
            if (loadRt) { loadRt.Release(); Destroy(loadRt); }
            loadCam = null; loadRt = null;
        }

        void MakeLoad(float scale)
        {
            var src = cam ? cam.GetComponent<Camera>() : Camera.main; if (!src) return;
            int w = Mathf.Clamp(Mathf.RoundToInt(Screen.width * scale), 64, 8192), h = Mathf.Clamp(Mathf.RoundToInt(Screen.height * scale), 64, 8192);
            loadRt = new RenderTexture(w, h, 24) { name = "OzCamResponseLoad" };
            var go = new GameObject("OzCamResponseLoadCam"); go.transform.SetParent(src.transform, false);
            loadCam = go.AddComponent<Camera>(); loadCam.CopyFrom(src); loadCam.targetTexture = loadRt; loadCam.depth = src.depth - 1;
        }

        IEnumerator StageRun(int fps, bool after, int repsPerAct, bool gpuLoad)
        {
            OzCamResponse.On = after && queue0; OzCamResponse.PanWindowOn = after; OzCamResponse.Keep();   // after = as shipped (the queue switch as the game has it)
            GameBoot.CapFps = fps; QualitySettings.vSyncCount = 0; Application.targetFrameRate = fps;
            Status = $"{fps} fps {(after ? "after" : "before")}";
            for (int i = 0; i < 30; i++) yield return null;
            int f0 = Time.frameCount; float r0 = Time.realtimeSinceStartup;
            foreach (Act a in System.Enum.GetValues(typeof(Act)))
                for (int k = 0; k < repsPerAct; k++)
                    yield return RepRun(a, k % 2 == 0 ? 1 : -1, fps, after);
            float dr = Time.realtimeSinceStartup - r0;
            stages.Add(new Stage { fps = fps, after = after, gpuLoad = gpuLoad, measuredFps = dr > 0f ? (Time.frameCount - f0) / dr : 0f, queued = QualitySettings.maxQueuedFrames, firstFrame = f0, lastFrame = Time.frameCount });
        }

        bool Moved(int k)
        {
            if (!frames.TryGetValue(k, out var a) || !frames.TryGetValue(k - 1, out var b) || !a.late || !b.late) return false;
            return (a.focus - b.focus).sqrMagnitude > 1e-8f || Mathf.Abs(a.height - b.height) > 1e-4f || Mathf.Abs(Mathf.DeltaAngle(a.yaw, b.yaw)) > 1e-3f || Mathf.Abs(a.tilt - b.tilt) > 1e-3f;
        }

        bool Turned(int k)
        {
            return frames.TryGetValue(k, out var a) && frames.TryGetValue(k - 1, out var b) && a.late && b.late
                && (Mathf.Abs(Mathf.DeltaAngle(a.yaw, b.yaw)) > 1e-3f || Mathf.Abs(a.tilt - b.tilt) > 1e-3f);
        }

        IEnumerator RepRun(Act a, int sign, int fps, bool after)
        {
            var rep = new Rep { act = a, fps = fps, after = after, inject = -1, sample = -1, moved = -1, ms = double.NaN, inFlight = -1 };
            bool staged = true;
            if (a == Act.Drag) staged = SetFreeCam(false);
            if (a == Act.Turn) staged = SetFreeCam(true);
            if (!staged) { rep.note = "FREE CAMERA switch not found (OzFreeCam.On)"; reps.Add(rep); yield break; }
            // still: the camera did not move for 4 frames and the zoom has settled (at most 150 frames)
            int still = 0;
            for (int i = 0; i < 150 && still < 4; i++)
            {
                yield return null;
                bool zoomDone = Mathf.Abs(cam.height - cam.TargetHeight) < 0.005f;
                still = !Moved(Time.frameCount - 1) && zoomDone ? still + 1 : 0;                           // the frame before this one is complete (a coroutine resumes before LateUpdate)
            }
            rep.inject = Time.frameCount;
            var mid = Mid;
            switch (a)
            {
                case Act.Key:
                    Keys(sign > 0 ? Key.RightArrow : Key.LeftArrow);
                    for (int i = 0; i < HoldFrames; i++) yield return null;
                    Keys();
                    break;
                case Act.Edge:
                    MouseAt(new Vector2(sign > 0 ? Screen.width - 1f : 1f, Screen.height * 0.5f), Vector2.zero, 0f, false);
                    for (int i = 0; i < HoldFrames; i++) yield return null;
                    MouseAt(mid, Vector2.zero, 0f, false);
                    break;
                case Act.Drag:
                case Act.Turn:
                    for (int i = 0; i < HoldFrames; i++) { MouseAt(mid, new Vector2(sign * DeltaPx, 0f), 0f, true); yield return null; }
                    MouseAt(mid, Vector2.zero, 0f, false);
                    break;
                case Act.Wheel:
                    MouseAt(mid, Vector2.zero, sign * OzTrackpadScroll.PerNotch(), false);
                    yield return null;
                    MouseAt(mid, Vector2.zero, 0f, false);
                    for (int i = 1; i < HoldFrames; i++) yield return null;
                    break;
            }
            for (int i = 0; i < TailFrames; i++) yield return null;
            reps.Add(rep);
        }

        // ------------------------------------------------------------------ after the run
        bool Seen(Act a, Frame f)
        {
            switch (a)
            {
                case Act.Key: return f.key != 0;
                case Act.Edge: return cam && (f.pos.x < cam.edgePx || f.pos.x > Screen.width - cam.edgePx);
                case Act.Drag: return f.mid && f.delta.sqrMagnitude > 0f && !f.testing;
                case Act.Turn: return f.mid && f.delta.sqrMagnitude > 0f && f.testing;
                default: return Mathf.Abs(f.scroll) > 0f;
            }
        }

        /// <summary>The timing figures of frame k: the finished frame that started last before the probe's stamp of frame k and after
        /// the stamp of frame k - 1 (needs the same clock).</summary>
        bool TimingOf(int k, out Timing t)
        {
            t = default;
            if (!SameClock || !frames.TryGetValue(k, out var f)) return false;
            long prev = frames.TryGetValue(k - 1, out var p) ? p.t0 : long.MinValue;
            bool found = false;
            foreach (var x in timings.Values)
                if ((long)x.start <= f.t0 && (long)x.start > prev && (!found || x.start > t.start)) { t = x; found = true; }
            return found;
        }

        /// <summary>Fills each rep's sample frame, moved frame, milliseconds and frames in flight from the frames and timings kept.</summary>
        public void Analyse()
        {
            for (int i = 0; i < reps.Count; i++)
            {
                var r = reps[i]; if (r.inject < 0) continue;
                for (int k = r.inject + 1; k <= r.inject + 12; k++) if (frames.TryGetValue(k, out var f) && Seen(r.act, f)) { r.sample = k; break; }
                for (int k = r.inject + 1; k <= r.inject + 12; k++) if (Moved(k)) { r.moved = k; break; }
                if (r.sample < 0) r.note = "the input was not seen (the game window lost focus, or the real mouse or keyboard was used)";
                else if (r.moved < 0) r.note = "the camera did not move (map edge, a menu, EDGE SCROLL off, or TRACKPAD SCROLL on MOVE MAP)";
                else if (r.moved < r.sample) r.note = "the camera moved before the input (something else moved it): left out";
                else if (r.act == Act.Drag && Turned(r.moved)) r.note = "the middle button turned the view instead of dragging (the middle mouse setting decides)";
                else if (r.act == Act.Turn && !Turned(r.moved)) r.note = "the middle button dragged instead of turning (the middle mouse setting decides)";
                if (r.sample > 0 && r.moved >= r.sample && frames.TryGetValue(r.sample, out var s))
                {
                    r.frameMs = s.dt * 1000f;
                    if (TimingOf(r.moved, out var t) && t.complete > 0)
                    {
                        r.ms = ((long)t.complete - s.t0) * 1000.0 / TickHz;
                        int n = 0; for (int k = r.moved + 1; k <= r.moved + 10; k++) if (frames.TryGetValue(k, out var q) && q.t0 < (long)t.complete) n++;
                        r.inFlight = n;
                    }
                }
                reps[i] = r;
            }
        }

        /// <summary>The frames of a stage as replay input (Script form): what RtsCamera.Update read in each of them.</summary>
        public List<OzCamResponseReplay.In> TraceOf(Stage st)
        {
            var l = new List<OzCamResponseReplay.In>();
            for (int k = st.firstFrame; k < st.lastFrame; k++)
            {
                if (!frames.TryGetValue(k, out var f)) continue;
                var x = new OzCamResponseReplay.In { dt = f.dt, wheel = f.scroll };
                x.dir = new Vector2(f.key, 0f);
                if (cam && f.pos.x < cam.edgePx) x.dir += Vector2.left; else if (cam && f.pos.x > Screen.width - cam.edgePx) x.dir += Vector2.right;
                if (f.mid && f.delta != Vector2.zero) { if (f.testing) x.turn = f.delta; else x.drag = f.delta; }
                l.Add(x);
            }
            return l;
        }
    }

    /// <summary>The probe's end of frame: after every other script.</summary>
    [DefaultExecutionOrder(32050)]
    public sealed class OzCamResponseProbeLate : MonoBehaviour
    {
        readonly FrameTiming[] buf = new FrameTiming[8];
        void LateUpdate() { var p = OzCamResponseProbe.I; if (p) p.Late(buf); }
    }
}
