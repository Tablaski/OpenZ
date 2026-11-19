// OpenZ (GPL-3.0)
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only: the toss, its landing place, the heavy landing, the break-up,
    /// the trees and the rules are untouched.
    ///
    /// Before: on the landing frame the old small RTS camera shake (RtsCamera.Shake 0.5 = about 3 px at the default camera) through the shared
    /// gate's 2.5 s cooldown, so in a fight the kicks of the shells around it usually held it back: the 6 t cannon came down without a jolt.
    /// Now, once per landing (UpdateToss's landing frame), with the same OpenZFxShake rules as the mega cannon's death blast
    /// (FortGunView.DeathShake.cs):
    /// <list type="bullet">
    /// <item>a kick of <see cref="LandKickPx"/> px (1080 px reference; the death blast's is 26, a fort shell impact about 10.5, the building
    /// collapse 9) at the landing point on the ground: only when it is in the camera view (10 % edge), smaller the further out the camera is
    /// (x 50 m / span, never under 0.4: about 8.6 px at the default RTS camera), SCREEN SHAKE off / CALM SCREEN on = none, not held back by
    /// the cooldown (a landing is its own event);</item>
    /// <item>a short rumble under it (<see cref="LandTailPx"/> px, eased in and out, gone at <see cref="LandTailEnd"/> s), only while the
    /// landing point stays on screen, stopped while the game is paused;</item>
    /// <item>the bounce: one smaller kick of <see cref="LandBounceKickPx"/> px when the cannon comes down again (OzHeavyLanding's fort
    /// cannon bounce, <see cref="LandBounceAt"/> s of game time after the hit);</item>
    /// <item>the private first-person view: one light kick (<see cref="LandFpsKickPx"/> px), no rumble, no bounce kick.</item>
    /// </list>
    /// Over open water (a splash, no ground landing), in the ending (its locked camera and talking scene: the outro blast's cannon lands while
    /// the scene talks, and the death blast's own rumble was kept clear of it) and with the switch <see cref="LandShakeOn"/> off: the old
    /// small shake, as before.
    /// When the new kick played, the old small shake is left out (one jolt, not two).
    /// </summary>
    public partial class FortGunView
    {
        /// <summary>Switch: the landing kick of the thrown mega cannon (on). Off = the old small shake, as before.</summary>
        public static bool LandShakeOn = true;
        /// <summary>The kick, the bounce's kick and the first-person view's light kick (px at the 1080 px reference, at 50 m or nearer).</summary>
        public static float LandKickPx = 12f, LandBounceKickPx = 5f, LandFpsKickPx = 4f;
        /// <summary>The bounce kick's time (s of game time after the hit): OzHeavyLanding.FortGun's bounce comes down at 0.30 s.</summary>
        public static float LandBounceAt = 0.30f;
        /// <summary>The rumble: peak px, start, ease-in, ease-out from, end (s of game time after the hit).</summary>
        public static float LandTailPx = 3.5f, LandTailFrom = 0.04f, LandTailIn = 0.06f, LandTailOutFrom = 0.20f, LandTailEnd = 0.60f;

        // ------------------------------------------------------------------ counts (LandShakeReport)
        public static int LandAsks, LandKicks, LandBounceKicks, LandTails, LandTailFrames, LandTailPaused, LandWater, LandEnding, LandFpsKicks;
        public static float LandTailPeakPx, LastLandKickAskPx, LastLandAt = -1f;
        public static bool LastLandKick;
        public static Vector3 LastLandPoint;
        const int LandTailRumbleId = 94000;                                                            // OpenZFxShake rumble ids: 91000-91999 the fort shockwave, 93000+ the death blast's tail

        /// <summary>UpdateToss, the landing frame (p = the cannon's pivot as it touches down): the landing kick. True = it kicked (the old small
        /// shake is then left out); false = the old shake runs as before (switch off, open water, off screen, the shake settings off).</summary>
        bool LandShake(Vector3 p)
        {
            LastLandKick = false;
            if (!LandShakeOn || !(LandKickPx > 0f)) return false;
            LandAsks++; LastLandAt = Time.time;
            if (OzWaterLanding.OpenWaterAt(p, out _)) { LandWater++; return false; }                   // a splash: as before
            if (RtsCamera.InputLocked) { LandEnding++; return false; }                                     // the ending (its camera, its talking scene): as before
            var sh = OpenZFxShake.I; if (sh == null) return false;
            var at = new Vector3(p.x, game ? game.GroundY(p.x, p.z) : p.y, p.z);                         // the landing point on the ground (p is the pivot, about 3 m up)
            LastLandPoint = at;
            bool fps = OzFpsView.On;
            float px = fps ? Mathf.Min(LandKickPx, LandFpsKickPx) : LandKickPx;
            LastLandKickAskPx = px;
            LastLandKick = sh.Kick(at, px, "fortgun_wreck_land", true);                                  // on screen (10 % edge), distance falloff, SCREEN SHAKE / CALM SCREEN
            if (!LastLandKick) return false;
            LandKicks++;
            if (fps) { LandFpsKicks++; return true; }
            FxRoot().gameObject.AddComponent<LandShakeRun>().Begin(at, LandTailRumbleId + ((Fort != null ? Fort.Id : 0) & 0x3FF));
            LandTails++;
            string line = string.Format("[shake] t={0:0.00} src=fortgun_wreck_land_tail px={1:0.0} -> TAIL {2:0.00}-{3:0.00} s, bounce kick {4:0.0} px at {5:0.00} s",
                                        Time.unscaledTime, LandTailPx, LandTailFrom, LandTailEnd, LandBounceKickPx, LandBounceAt);
            if (OzShakeGate.Log.Count >= 400) OzShakeGate.Log.RemoveAt(0);
            OzShakeGate.Log.Add(line);
            if (OzShakeGate.LogToConsole) Debug.Log(line);
            return true;
        }

        /// <summary>One line of the landing kick's numbers and counts, for the checks.</summary>
        public static string LandShakeReport() =>
            $"mega cannon wreck landing shake: on {LandShakeOn}, kick {LandKickPx:F0} px (last asked {LastLandKickAskPx:F1}, kicked {LastLandKick}), bounce {LandBounceKickPx:F0} px at {LandBounceAt:F2} s, " +
            $"tail {LandTailPx:F1} px {LandTailFrom:F2}-{LandTailEnd:F2} s; landings asked {LandAsks}, kicks {LandKicks}, bounce kicks {LandBounceKicks}, tails {LandTails}, tail frames {LandTailFrames}, " +
            $"peak {LandTailPeakPx:F1} px, paused frames {LandTailPaused}, water {LandWater}, in the ending {LandEnding}, first-person light kicks {LandFpsKicks}; last at {LastLandPoint}; shake setting {(OzShakeGate.SettingOn ? "on" : "off")}";

        // ------------------------------------------------------------------ the rumble and the bounce kick (game time: they wait while paused)
        sealed class LandShakeRun : MonoBehaviour
        {
            Vector3 at; float t; int rid; bool set, bounced;

            internal void Begin(Vector3 point, int id) { at = point; rid = id; }

            void Clear() { if (!set) return; set = false; OpenZFxShake.I?.SetRumble(rid, at, 0f); }

            void Update()
            {
                var sh = OpenZFxShake.I;
                if (sh == null) { set = false; Destroy(this); return; }
                var g = ZGame.I;
                if (g && g.paused) { if (set) LandTailPaused++; Clear(); return; }                        // never a shake held over a pause (or a tactical freeze)
                t += Time.deltaTime;
                if (!LandShakeOn || !OzShakeGate.SettingOn || OzFpsView.On) { Clear(); Destroy(this); return; }
                if (!bounced && t >= LandBounceAt)
                {   // the cannon comes down again: one smaller kick (the same on-screen test, falloff and settings)
                    bounced = true;
                    if (LandBounceKickPx > 0f && sh.Kick(at, LandBounceKickPx, "fortgun_wreck_land_bounce", true)) LandBounceKicks++;
                }
                if (t >= LandTailEnd) { Clear(); if (bounced) Destroy(this); return; }
                if (t < LandTailFrom) return;
                float k = Mathf.SmoothStep(0f, 1f, (t - LandTailFrom) / Mathf.Max(0.01f, LandTailIn))
                          * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(LandTailOutFrom, LandTailEnd, t)));
                float px = LandTailPx * k * DeathShakeDistK(at);                                          // the kick's own distance falloff (FortGunView.DeathShake.cs)
                if (!(px > 0.01f)) { Clear(); return; }
                sh.SetRumble(rid, at, px / OpenZFxCatalog.ShakeRumblePerS); set = true;                  // OpenZFxShake: only while 'at' is on screen
                LandTailFrames++; if (px > LandTailPeakPx) LandTailPeakPx = px;
            }

            void OnDisable() => Clear();
        }
    }
}
