// OpenZ (GPL-3.0) - version 1.3, item 25 (g): the camera's feel at a low frame rate. Plain arithmetic (no engine call), so the
// counted checks run it on made-up input as well as the game does on the real one.
//  * WheelNotches: the mouse wheel's scroll of one frame in notches. Every notch counts (before: any scroll in one frame was exactly
//    one 15 % step, so at a low frame rate two or three notches in one frame were one step, and a touchpad's many small values were
//    each a full step: the zoom "going in and out"). One notch of a normal wheel is still exactly one step.
//  * PanDt: the frame time the camera's pan moves by, the mean of the last few frame times. At an even frame rate it is the frame
//    time itself; when frame times swing (a frame with 7 rule ticks, the next with 8) the steps on screen stay even, and the
//    distance over a pan is the same (each frame time is counted once in each of the next N means).
namespace OpenZ
{
    public static class OzCamFeel
    {
        /// <summary>Less scroll than this (notches) in one frame is noise (a touchpad finger resting): no zoom.</summary>
        public const float DeadNotch = 0.02f;
        /// <summary>The scroll of one frame in wheel notches, clamped to +-maxNotches; 0 under DeadNotch. perNotch: how much scroll one
        /// notch is (OzTrackpadScroll.PerNotch: 1, or 120 where the raw Windows range is kept).</summary>
        public static float WheelNotches(float wheel, float perNotch, float maxNotches)
        {
            float n = wheel / (perNotch > 0f ? perNotch : 1f);
            if (n > maxNotches) n = maxNotches; else if (n < -maxNotches) n = -maxNotches;
            return System.Math.Abs(n) < DeadNotch ? 0f : n;
        }

        /// <summary>The mean of the last `frames` frame times (1 = the frame time itself, as before).</summary>
        public sealed class PanClock
        {
            readonly float[] dts = new float[16]; int count, at;
            public float Step(float dt, int frames)
            {
                if (frames < 1) frames = 1; else if (frames > dts.Length) frames = dts.Length;
                dts[at] = dt; at = (at + 1) % dts.Length; if (count < dts.Length) count++;
                int n = count < frames ? count : frames; float sum = 0f;
                n = OzCamResponse.PanFrames(dts, at, n);   // 1.4 camera_response: when those frames took over 0.09 s, the last 2 only (View/OzCamResponse.cs)
                for (int i = 1; i <= n; i++) sum += dts[(at - i + dts.Length) % dts.Length];
                return sum / n;
            }
            public void Reset() { count = 0; at = 0; }
        }
    }
}
