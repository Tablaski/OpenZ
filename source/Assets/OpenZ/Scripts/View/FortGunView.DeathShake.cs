// OpenZ (GPL-3.0)
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Stacks on megacannon_blast (FortGunView.DeathBlast.cs) and outro_megacannon_blast (the same blast in the ending's beat).
    /// <list type="bullet">
    /// <item>the kick <see cref="DeathKickPx"/> 12 -> 26 px and the after-kick <see cref="DeathAfterKickPx"/> 6 -> 13 px (1080 px reference,
    /// about 2.2 x; OpenZFxShake.Kick as before: only when the blast is in the view (10 % edge), smaller the further out the camera is
    /// (x 50 m / span, never under 0.4), the SCREEN SHAKE and CALM SCREEN settings and the shake scale decide);</item>
    /// <item>a short rumble tail after them (<see cref="DeathTailPx"/> px, from <see cref="DeathTailFrom"/> s, eased in over
    /// <see cref="DeathTailIn"/> s, eased out from <see cref="DeathTailOutFrom"/> s to nothing at <see cref="DeathTailEnd"/> s), only when the
    /// kick itself played; the same distance falloff as the kick; it stops at once while the blast is off screen (OpenZFxShake's rumble test),
    /// with SCREEN SHAKE off / CALM SCREEN on, and while the game is paused;</item>
    /// <item>the private first-person view: one light kick of <see cref="DeathFpsKickPx"/> px only (no after-kick, no tail).</item>
    /// </list>
    /// All of it is gone in about 1.05 s (the nuke: 14 px over 3.6 s; the fort's death: 8.6 px over 3.4 s).
    /// Switch <see cref="DeathShakeStrongOn"/>: off = the 12 / 6 px kicks exactly as megacannon_blast made them, no tail.
    /// </summary>
    public partial class FortGunView
    {
        /// <summary>Switch: the stronger shake of the mega cannon's death blast (on). Off = the kicks of megacannon_blast (12 / 6 px), no tail.</summary>
        public static bool DeathShakeStrongOn = true;
        /// <summary>The kicks before this kit (used when the switch is off).</summary>
        public static float DeathKickPxOld = 12f, DeathAfterKickPxOld = 6f;
        /// <summary>The rumble tail: peak px (1080 reference), start (s after the blast), ease-in (s), ease-out from (s), end (s).</summary>
        public static float DeathTailPx = 6f, DeathTailFrom = 0.12f, DeathTailIn = 0.08f, DeathTailOutFrom = 0.35f, DeathTailEnd = 1.05f;
        /// <summary>The private first-person view: one light kick (px), nothing more.</summary>
        public static float DeathFpsKickPx = 6f;

        // ------------------------------------------------------------------ counts (DeathShakeReport)
        public static int DeathTails, DeathTailFrames, DeathTailPaused, DeathFpsKickAsks;
        public static float DeathTailPeakPx, LastDeathKickAskPx, LastDeathAfterKickAskPx;
        const int DeathTailRumbleId = 93000;                                                           // OpenZFxShake rumble ids: the fort shockwave uses 91000-91999

        /// <summary>The px the kick (or the after-kick) asks for now.</summary>
        static float DeathKickFor(float px, bool after)
        {
            float v;
            if (!DeathShakeStrongOn) v = after ? DeathAfterKickPxOld : DeathKickPxOld;                // as megacannon_blast made it
            else if (OzFpsView.On) { v = after ? 0f : Mathf.Min(px, DeathFpsKickPx); if (!after) DeathFpsKickAsks++; }   // the first-person view: one light kick
            else v = px;
            if (after) LastDeathAfterKickAskPx = v; else LastDeathKickAskPx = v;
            return v;
        }

        /// <summary>DeathBlast, after the after-effects runner started: the rumble tail, when the kick played.</summary>
        void DeathShakeTail(Vector3 at)
        {
            if (!DeathShakeStrongOn || !LastDeathBlastKick || OzFpsView.On || !(DeathTailPx > 0f) || OpenZFxShake.I == null || !OzShakeGate.SettingOn) return;
            FxRoot().gameObject.AddComponent<DeathShakeTailRun>().Begin(at, DeathTailRumbleId + ((Fort != null ? Fort.Id : 0) & 0x3FF));
            DeathTails++;
            string line = string.Format("[shake] t={0:0.00} src=fortgun_megablast_tail px={1:0.0} -> TAIL {2:0.00}-{3:0.00} s", Time.unscaledTime, DeathTailPx, DeathTailFrom, DeathTailEnd);
            if (OzShakeGate.Log.Count >= 400) OzShakeGate.Log.RemoveAt(0);
            OzShakeGate.Log.Add(line);
            if (OzShakeGate.LogToConsole) Debug.Log(line);
        }

        /// <summary>One line of this kit's numbers and counts, for the proof.</summary>
        public static string DeathShakeReport() =>
            $"fort gun death shake: on {DeathShakeStrongOn}, kick {DeathKickPx:F0} px (last asked {LastDeathKickAskPx:F1}), after-kick {DeathAfterKickPx:F0} px at {DeathAfterKickAt:F2} s " +
            $"(last asked {LastDeathAfterKickAskPx:F1}), tail {DeathTailPx:F1} px {DeathTailFrom:F2}-{DeathTailEnd:F2} s: tails {DeathTails}, frames {DeathTailFrames}, peak {DeathTailPeakPx:F1} px, " +
            $"paused frames {DeathTailPaused}; first-person light kicks asked {DeathFpsKickAsks} ({DeathFpsKickPx:F0} px); shake setting {(OzShakeGate.SettingOn ? "on" : "off")}; {DeathBlastReport()}";

        /// <summary>The on-screen px of a px value at <paramref name="at"/>: the kick's own distance falloff (x 50 m / span, 0.4..1).</summary>
        static float DeathShakeDistK(Vector3 at)
        {
            var c = Camera.main; if (!c) return 0f;
            float span = c.orthographic ? 2f * c.orthographicSize : 2f * Vector3.Distance(c.transform.position, at) * Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return Mathf.Clamp(OpenZFxCatalog.ShakeKickRefSpan / Mathf.Max(1f, span), OpenZFxCatalog.ShakeKickFarK, 1f);
        }

        // ------------------------------------------------------------------ the rumble tail (game time: it pauses with the game)
        sealed class DeathShakeTailRun : MonoBehaviour
        {
            Vector3 at; float t; int rid; bool set;

            internal void Begin(Vector3 point, int id) { at = point; rid = id; }

            void Clear() { if (!set) return; set = false; OpenZFxShake.I?.SetRumble(rid, at, 0f); }

            void Update()
            {
                var sh = OpenZFxShake.I;
                if (sh == null) { set = false; Destroy(this); return; }
                var g = ZGame.I;
                if (g && g.paused) { if (set) DeathTailPaused++; Clear(); return; }                     // never a shake held over the pause
                t += Time.deltaTime;
                if (t >= DeathTailEnd || !DeathShakeStrongOn || !OzShakeGate.SettingOn || OzFpsView.On) { Clear(); Destroy(this); return; }
                if (t < DeathTailFrom) return;
                float k = Mathf.SmoothStep(0f, 1f, (t - DeathTailFrom) / Mathf.Max(0.01f, DeathTailIn))
                          * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DeathTailOutFrom, DeathTailEnd, t)));
                float px = DeathTailPx * k * DeathShakeDistK(at);
                if (!(px > 0.01f)) { Clear(); return; }
                sh.SetRumble(rid, at, px / OpenZFxCatalog.ShakeRumblePerS); set = true;               // OpenZFxShake: s x ShakeRumblePerS px, only while 'at' is on screen
                DeathTailFrames++; if (px > DeathTailPeakPx) DeathTailPeakPx = px;
            }

            void OnDisable() => Clear();
        }
    }
}
