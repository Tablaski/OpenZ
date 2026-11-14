using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.3 item 15: the pieces of a destroyed shuttle tumble and spin as they fly, from their own push and size, then land and settle as
    /// before. View only (rule DV118's break-up; the kill, the damage and the crew are the rules' as before).
    /// <para>Why they flew stiff (read in OzShuttleBreakup.cs): every piece's body has angular damping 0.9 (its spin loses 60 % a second),
    /// and the launch gave the big pieces, which are what the eye follows, little spin: the body chunks 1-2 rad/s (1.6-3.2 in an air
    /// burst, x0.7-1.3 by ThrowVary), the domes, engines and ramp 3-5 rad/s; a ground death throws them 0.4 x as high, so they barely
    /// turned before they lay still.</para>
    /// <para>Now: (1) the launch spin is at least <see cref="TumbleK"/> x the piece's throw speed (out of the blast, the ship's own way
    /// taken off) over its half size, a little more for a light piece and less for a heavy one (x(<see cref="TumbleRefKg"/> / mass)^0.1,
    /// 0.8-1.2), never under <see cref="TumbleMinSpin"/>, capped at the body's 14 rad/s; the drawn spins already higher stay; ThrowVary's
    /// own variation still applies; no random draw is added, so every other draw of the break is as before. (2) While it flies the
    /// piece's spin damping is <see cref="AirAngularDamping"/>; at its first landing contact (the ground, the water, a rock or building,
    /// a piece already at rest) or when it rests it goes back to <see cref="LandAngularDamping"/> (the old 0.9), so the landing, the
    /// rolling out and the rest are as before.</para>
    /// Switch: <see cref="TumbleOn"/> (off = the old spin and damping).
    /// </summary>
    public sealed partial class OzShuttleBreakup
    {
        public static bool TumbleOn = true;
        /// <summary>Spin per (m/s of throw / m of half size), the least spin (rad/s), the mass of an average piece (kg).</summary>
        public static float TumbleK = 0.9f, TumbleMinSpin = 3f, TumbleRefKg = 600f;
        /// <summary>Spin damping in the air and after the first landing contact (the old value).</summary>
        public static float AirAngularDamping = 0.1f, LandAngularDamping = 0.9f;
        /// <summary>Proof: launches whose spin the rule raised, pieces that came down.</summary>
        public static int TumbleRaised, Touchdowns;

        bool tumbleDown, tumbleInit; float tumbleTurned, tumblePeak, tumbleAir; Quaternion tumbleLast;
        /// <summary>The check (View/OzShuttleTumbleWatch.cs): degrees it turned in the air, its fastest spin there (rad/s), its time in the
        /// air (s), down yet.</summary>
        public float AirTurnDeg => tumbleTurned;
        public float AirPeakSpin => tumblePeak;
        public float AirSeconds => tumbleAir;
        public bool Touched => tumbleDown;

        static float TumbleSpin(OzShuttleBreakup pc, Vector3 throwRel, float sp)
        {
            if (!TumbleOn || !pc) return sp;
            float half = Mathf.Max(0.4f, pc.Size * 0.5f);
            float massK = Mathf.Clamp(Mathf.Pow(TumbleRefKg / Mathf.Max(1f, pc.MassKg), 0.1f), 0.8f, 1.2f);
            float w = Mathf.Max(TumbleMinSpin, TumbleK * throwRel.magnitude / half * massK);
            if (w <= sp) return sp;
            TumbleRaised++;
            return Mathf.Min(w, 14f);
        }

        void TumbleAir() { if (TumbleOn && rb && !tumbleDown) rb.angularDamping = AirAngularDamping; }

        void Touchdown()
        {
            if (tumbleDown) return;
            tumbleDown = true; Touchdowns++;
            if (TumbleOn && rb) rb.angularDamping = LandAngularDamping;
            TumbleLetGo();                                                                                      // 1.3 item 15b: its held pairs by the old depth rule now
        }

        /// <summary>A contact: the ground, the water bed, scenery or a piece that is down = it has landed; a piece still flying = not yet.</summary>
        void TumbleContact(Collision c)
        {
            if (tumbleDown || c == null || !c.collider) return;
            var other = c.collider.GetComponentInParent<OzShuttleBreakup>();
            if (other && !other.keeper && !other.tumbleDown && !other.frozen) return;                          // two pieces knocking in the air
            if (!other && c.rigidbody && !c.rigidbody.isKinematic) return;
            Touchdown();
        }

        // ------------------------------------------------------------------ 1.3 item 15b: no grinding against a neighbour in the air
        /// <summary>1.3 item 15b. In an air break (burst, split, crack) two pieces that can still reach each other (the hull's neighbours:
        /// the eight body panels share their cut faces, the domes and engines sit on the body) do not collide while both are still in the
        /// air. They collide again once they are clear of each other (their reach spheres apart), or as soon as one of them is down (then by
        /// the old depth rule: more than <see cref="HoldDepth"/> inside each other = held apart until they are not, as at the start).
        /// <para>
        /// each is launched spinning; a pair less than 3 cm inside each other (every cut face) collided from the first physics step, so the
        /// spinning edges drove into the neighbours and the heavy metal contact (bounce 0.08, friction 0.85) took the spin. Pieces with no
        /// neighbour kept 96-99 % of their launch spin over the flight; the body panels in the crack, whose slow throw keeps them together
        /// longest, kept 20-88 % (median 42 %): Body_R_Low_B, 3.8 rad/s for 1.78 s, turned 70 deg, not the 355 of its free flight.</para>
        /// View only, no random draw. Switch: <see cref="TumbleHold"/> (off = the old start hold: only pairs more than 3 cm inside each
        /// other, let go at 3 cm).</summary>
        public static bool TumbleHold = true;
        /// <summary>The margin (m) over the two reach spheres for "clear of each other".</summary>
        public static float TumbleClearM = 0.1f;
        /// <summary>Proof: pairs the air rule held at a break (beyond the old deep pairs); held pairs let go when one of the two came down.</summary>
        public static int TumbleHeldPairs, TumbleLetGoDown;

        float tumbleReach = -1f;
        /// <summary>The farthest vertex of the piece from its body's origin (m): no turn takes any of it farther out.</summary>
        float TumbleReach()
        {
            if (tumbleReach >= 0f) return tumbleReach;
            World(); var o = transform.position; float r = 0f;
            foreach (var q in wpts) r = Mathf.Max(r, (q - o).sqrMagnitude);
            return tumbleReach = Mathf.Sqrt(r);
        }

        static bool TumbleClear(OzShuttleBreakup a, OzShuttleBreakup b)
        {
            float r = a.TumbleReach() + b.TumbleReach() + TumbleClearM;
            return (a.transform.position - b.transform.position).sqrMagnitude > r * r;
        }

        static bool TumbleAirBreak(Group g) => g != null && (g.how == OzShuttleDeathPlan.Burst || g.how == OzShuttleDeathPlan.Split || g.how == OzShuttleDeathPlan.Crack);

        /// <summary>Break's start hold: in an air break a pair that is not clear is held too (the old rule: only pairs more than 3 cm inside each other).</summary>
        static bool TumbleHoldAtBreak(Group g, OzShuttleBreakup a, OzShuttleBreakup b)
        {
            if (!TumbleOn || !TumbleHold || !TumbleAirBreak(g) || !a || !b || TumbleClear(a, b)) return false;
            TumbleHeldPairs++;
            return true;
        }

        /// <summary>The keeper's check of a held pair (every 0.2 s): in an air break it stays held while both are in the air and not clear.</summary>
        static bool TumbleKeep(OzShuttleBreakup a, OzShuttleBreakup b) => TumbleOn && TumbleHold && a && b && TumbleAirBreak(a.grp) && !a.tumbleDown && !b.tumbleDown && !TumbleClear(a, b);

        /// <summary>Touchdown: this piece is down, so its held pairs go by the old rule at once: no longer more than 3 cm inside each other =
        /// they collide again (a piece still flying lands on it).</summary>
        void TumbleLetGo()
        {
            if (!TumbleOn || !TumbleHold || grp == null || !TumbleAirBreak(grp)) return;
            for (int i = grp.held.Count - 1; i >= 0; i--)
            {
                var q = grp.held[i]; if (q.pa != this && q.pb != this) continue;
                if (!q.a || !q.b || Depth(q.a, q.b) > HoldDepth) continue;
                Physics.IgnoreCollision(q.a, q.b, false); grp.held.RemoveAt(i); ReleasedPairs++; TumbleLetGoDown++;
            }
        }

        void LateUpdate()
        {
            if (keeper || !rb || tumbleDown || retiring) return;
            if (OzWaterLanding.InWater(transform)) { Touchdown(); return; }
            var q = transform.rotation;
            if (tumbleInit) tumbleTurned += Quaternion.Angle(tumbleLast, q);
            tumbleLast = q; tumbleInit = true;
            tumbleAir += Time.deltaTime;
            tumblePeak = Mathf.Max(tumblePeak, rb.angularVelocity.magnitude);
        }
    }
}
