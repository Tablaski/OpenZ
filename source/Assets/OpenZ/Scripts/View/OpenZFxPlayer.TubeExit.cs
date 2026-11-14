using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 owner item 148 (the MLRS v2): every MLRS rocket leaves the mouth of the tube that just
    /// emptied, along that tube, from its very first drawn frame. The MLRS proofs found 2 of 24 (L02) and 5 of 23 (film 15) drawn v2 rockets
    /// nearer another tube, their first drawn frame up to 1.33 m out of the mouth and 0.46 m off the tube's axis (v1 0.39 m / 0.19 m).
    /// Cause, three parts, all in the drawing (the tube order itself was right: v1's 01..08 round robin, the tube hidden = the tube fired):
    ///  1. ONE FRAME LATE: a rocket starts where the tube's mouth was drawn LAST frame (CombatView 50 and MlrsSalvo 55 read it in Update,
    ///     before UnitView, UnitAim's aim (62) and the launcher kick (66) pose this frame); on v2 the muzzles ride on the kicked box
    ///     (MlrsSideKick puts it back to its rest only after those Updates), so the start point carried last frame's kick and slew;
    ///  2. ITS OWN WAY OUT: a rocket flies its ballistic curve to ITS landing point from that start (OpenZFxPlayer.Launch): the ripple's
    ///     rockets 2-8 land spread round the target (up to 4.5 m), a launcher at its elevation stop or kicked up points elsewhere, so the
    ///     curve leaves up to 30 deg (and at the closest shots more) off the tube's axis;
    ///  3. THE FIRST STEP: it is first drawn a whole frame along that curve (StepProjectiles advances before it draws), more after a slow frame.
    /// Now an MLRS rocket is bound to its tube (UnitAim.OnFire names the tube's muzzle the same frame it hides the tube's rocket; the rocket
    /// takes it when it starts on that muzzle's place) and is DRAWN, after every pose of this frame (this LateUpdate runs after the aim and
    /// the kick):
    ///  * IN THE MOUTH (its first <see cref="TubeHoldM"/>, a quarter of a short flight at most): on the tube's live axis out of its live mouth
    ///    by the distance it has flown (it rides with the launcher's turn and kick); its first drawn frame at most <see cref="TubeFirstMaxM"/> out;
    ///  * FREE: from the point it left the mouth along the line it left by, one smooth turn (a cubic curve: leaving along the tube, arriving
    ///    along its own path) onto its own curve, which it joins exactly after about <see cref="TubeBlendM"/> of flight (half its remaining
    ///    flight at most): never back, never a kink, and a launcher turning on never swings it.
    /// Its landing point and time and the sim's blast are untouched (past the turn it is on its own curve); its body points along the way it is
    /// drawn. View only. Switch: <see cref="TubeExit"/> (off = the drawing before).
    /// </summary>
    public partial class OpenZFxPlayer
    {
        /// <summary>The switch (off = a rocket starts on its ballistic curve from where the mouth was last frame, as before).</summary>
        public static bool TubeExit = true;
        /// <summary>The distance flown on the tube's axis (m), the distance over which it turns onto its own curve (m), and the farthest its
        /// first drawn frame is out of the mouth (m).</summary>
        public static float TubeHoldM = 0.6f, TubeBlendM = 2.4f, TubeFirstMaxM = 0.45f;
        /// <summary>Statistics (the proofs): rockets bound to their tube, rockets named a tube but started elsewhere (not bound), the
        /// farthest first drawn frame out of the mouth (m).</summary>
        public static int TubeBound, TubeMissed;
        public static float TubeFirstAlongMax;

        static Transform nextTube; static Vector3 nextTubeDirL, nextTubeAt; static int nextTubeFrame = -1;
        sealed class TubeRide
        {
            public Proj p; public Transform t; public Vector3 dirL, from, v0, at, axis, drawn, leftAt; public bool first = true, free; public float freeAge, joinAge;
        }
        readonly Dictionary<int, TubeRide> tubeRides = new Dictionary<int, TubeRide>();

        /// <summary>UnitAim.OnFire: the MLRS tube (its muzzle and the muzzle's firing axis in its own space) that the next rocket of this
        /// frame leaves; the rocket takes it when it starts where that muzzle is now.</summary>
        public static void NoteTube(Transform muzzle, Vector3 dirL)
        {
            nextTube = muzzle; nextTubeDirL = dirL; nextTubeAt = muzzle ? muzzle.position : Vector3.zero; nextTubeFrame = Time.frameCount; nextTubeW = FxWeapon.Mlrs;   // k92: an MLRS tube (a mech pod names its own: View/OpenZFxPlayer.K92.cs)
        }

        /// <summary>Launch, once the projectile is set up: an MLRS rocket that starts on the tube named this frame is bound to it.</summary>
        void TubeLaunch(Proj p, Vector3 from)
        {
            var t = nextTube; bool named = t && nextTubeFrame == Time.frameCount;
            if (p.w != (named ? nextTubeW : FxWeapon.Mlrs)) return;   // k92, item 135: a tube named for the mech's pod rockets binds its rocket too (View/OpenZFxPlayer.K92.cs)
            nextTube = null;
            if (!TubeExit || !named) return;
            if ((from - nextTubeAt).sqrMagnitude > 0.05f * 0.05f) { TubeMissed++; return; }
            if (tubeRides.Count > 64)
            {   // rides whose rocket has gone (a pool slot reused or ended before its turn)
                var gone = new List<int>();
                foreach (var kv in tubeRides) if (kv.Value.p == null || !kv.Value.p.active || kv.Value.p.index != kv.Key) gone.Add(kv.Key);
                foreach (var k in gone) tubeRides.Remove(k);
            }
            tubeRides[p.index] = new TubeRide { p = p, t = t, dirL = nextTubeDirL, from = from, v0 = p.vel, at = from, axis = t.TransformDirection(nextTubeDirL).normalized };
            TubeBound++;
        }

        /// <summary>The rocket's own curve at an age: the eased curve (StepProjectiles' closed form) or the plain ballistic one.</summary>
        static Vector3 TubeCurve(Proj p, TubeRide r, float age)
        {
            if (p.ease)
            {
                float u = EaseProgress(age / Mathf.Max(1e-4f, p.life));
                var q = Vector3.LerpUnclamped(p.from, p.to, u); q.y += p.g * p.life * p.life * 0.5f * (u - u * u);
                return q;
            }
            return r.from + r.v0 * age + Vector3.down * (0.5f * p.g * age * age);
        }

        /// <summary>StepProjectiles, once a projectile's place for this frame is worked out (before it is traced and drawn): a rocket bound to
        /// its tube is drawn on the tube's axis out of its mouth, then turns onto its own curve.</summary>
        void TubeStep(Proj p, float dt)
        {
            if (tubeRides.Count == 0 || !tubeRides.TryGetValue(p.index, out var r) || r.p != p) return;
            var b = TubeCurve(p, r, p.age);                                                          // its own curve now
            if (!TubeExit || (r.free && p.age >= r.joinAge))
            {   // on its own curve: the ride ends exactly on it
                p.pos = b; if (!p.ease) p.vel = r.v0 + Vector3.down * (p.g * p.age);
                tubeRides.Remove(p.index); return;
            }
            float s = Vector3.Distance(b, r.from), hold = Mathf.Min(TubeHoldM, 0.25f * Vector3.Distance(r.from, p.to));
            Vector3 d;
            if (!r.free && (r.first || s <= hold))
            {   // in the mouth: on this frame's axis out of this frame's mouth (it rides with the launcher's turn and kick)
                if (r.t) { r.at = r.t.position; var ax = r.t.TransformDirection(r.dirL); if (ax.sqrMagnitude > 1e-8f) r.axis = ax.normalized; }
                float along = r.first ? Mathf.Min(s, Mathf.Min(TubeFirstMaxM, hold)) : s;                           // never past where it leaves the mouth
                d = r.at + r.axis * along;
                if (r.first) { TubeFirstAlongMax = Mathf.Max(TubeFirstAlongMax, along); p.prev = r.at; }
            }
            else
            {
                if (!r.free)
                {   // out of the mouth now: free along the line it left by; it joins its own curve after TubeBlendM of flight (half its rest at most)
                    r.free = true; r.freeAge = p.age; r.leftAt = r.at + r.axis * hold;
                    float sp = (TubeCurve(p, r, p.age + 0.01f) - TubeCurve(p, r, p.age - 0.01f)).magnitude / 0.02f;
                    r.joinAge = p.age + Mathf.Min(0.5f * Mathf.Max(0f, p.life - p.age), Mathf.Max(dt, TubeBlendM / Mathf.Max(0.5f, sp)));
                }
                float t = Mathf.Clamp01((p.age - r.freeAge) / Mathf.Max(1e-4f, r.joinAge - r.freeAge));
                var p1 = TubeCurve(p, r, r.joinAge);
                var t1 = TubeCurve(p, r, r.joinAge + 0.01f) - TubeCurve(p, r, r.joinAge - 0.01f);
                t1 = t1.sqrMagnitude > 1e-10f ? t1.normalized : r.axis;
                float L = Vector3.Distance(r.leftAt, p1), t2 = t * t, t3 = t2 * t;
                d = (2f * t3 - 3f * t2 + 1f) * r.leftAt + (t3 - 2f * t2 + t) * L * r.axis + (3f * t2 - 2f * t3) * p1 + (t3 - t2) * L * t1;   // leaving along the tube, arriving along its own curve
            }
            var prev = r.first ? r.at : r.drawn;
            p.vel = dt > 1e-5f && (d - prev).sqrMagnitude > 1e-10f ? (d - prev) / dt : r.axis;
            p.pos = d; r.drawn = d; r.first = false;
        }
    }
}
