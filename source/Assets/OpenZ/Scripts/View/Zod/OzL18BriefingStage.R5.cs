// OpenZ (GPL-3.0)
//  - the low flight after the blasts (aftermath_run) enters ALREADY MOVING: OzDronePath.enterK / enterSeconds (ZodFlyover.L18Drone.cs Share);
//  - a short rush of air at the cut into it, under the voice's duck like every stage sound (OzL18BriefingStage.cs, one row);
//  - OzL18DroneEntry: the per-frame speed of each drone flight's first second, measured from the camera's own place frame to frame (and the
//    path's own speed beside it), one console line per flight ("[L18 drone] entry ...") and Last / All for the Play checks.
// View only (no Sim file).
using System.Text;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzL18BriefingStage
    {
        /// <summary>fixes_r5 (51.6): the rush of air at the cut into the low flight after the blasts: on / off, the stage clock it plays at (the
        /// cut is at tau 3.09: aftermath_run starts there; 0.09 s early so the swell meets the cut), gain share of GainRush, pitch.</summary>
        public static bool FpvRush = true;
        public static float FpvRushTau = 3.0f, FpvRushGain = 0.8f, FpvRushPitch = 1.05f;
    }

    /// <summary></summary>
    public static class OzL18DroneEntry
    {
        /// <summary>On / off, how many seconds of each flight are logged.</summary>
        public static bool On = true; public static float Seconds = 1.0f;
        /// <summary>The last finished log (one flight) and every finished log of this run.</summary>
        public static string Last = "";
        public static readonly StringBuilder All = new StringBuilder();
        /// <summary>The largest frame-to-frame change of the measured speed in the last log (share of the larger of the two), its first frame's
        /// speed as a share of the speed at 0.5 s.</summary>
        public static float LastWorstStep, LastStartShare;

        static string shot; static bool open; static float lastT, v05, vFirst, prevV; static Vector3 lastP; static int frames;
        static readonly StringBuilder sb = new StringBuilder();

        public static void Log(string name, float t, Vector3 p, float pathSpeed)
        {
            if (!On) return;
            if (name != shot || t < lastT - 1e-4f)
            {   // a new flight (or a rewound one): start its log
                if (open) Close();
                shot = name; open = true; sb.Length = 0; frames = 0; lastT = t; lastP = p; v05 = vFirst = prevV = -1f; LastWorstStep = 0f;
                sb.Append("[L18 drone] entry of ").Append(name).Append(" (shot seconds: measured m/s from the camera's place frame to frame | the path's own m/s):");
                return;
            }
            if (!open) return;
            float dt = t - lastT;
            if (dt <= 1e-5f) return;                                                                   // a held frame
            float v = (p - lastP).magnitude / dt;
            frames++;
            if (vFirst < 0f) vFirst = v;
            if (prevV >= 0f) { float m = Mathf.Max(prevV, v); if (m > 0.01f) LastWorstStep = Mathf.Max(LastWorstStep, Mathf.Abs(v - prevV) / m); }
            if (v05 < 0f && t >= 0.5f) v05 = v;
            sb.Append(' ').Append(t.ToString("0.000")).Append(':').Append(v.ToString("0.0")).Append('|').Append(pathSpeed.ToString("0.0"));
            prevV = v; lastT = t; lastP = p;
            if (t >= Seconds) Close();
        }

        static void Close()
        {
            open = false;
            LastStartShare = v05 > 0.01f && vFirst >= 0f ? vFirst / v05 : -1f;
            sb.Append(" | frames ").Append(frames).Append(", first frame ").Append(vFirst.ToString("0.0")).Append(" m/s = ").Append((LastStartShare * 100f).ToString("0")).Append(" % of the speed at 0.5 s, largest frame-to-frame step ").Append((LastWorstStep * 100f).ToString("0.0")).Append(" %");
            Last = sb.ToString(); All.AppendLine(Last);
            Debug.Log(Last);
        }
    }
}
