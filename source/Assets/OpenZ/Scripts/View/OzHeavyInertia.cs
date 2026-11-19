using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only; the sim is never read for
    /// it beyond what UnitView already draws, never written.
    /// <para>The heavy tank (v1 and v2), the Mammoth and the Bizon: the hull with its turret (NOT the tracks, links, wheels, sponsons or side
    /// plates: the planted running gear, View/UnitView.GroundGear.cs, puts them on the ground after this) rocks on a damped spring about a
    /// pivot at the hull's middle, at the height of the track tops:</para>
    /// <list type="bullet">
    /// <item>START from a standstill (stood at least <see cref="StandS"/> s): every bit of drawn speed gained in the first <see cref="StartWindowS"/>
    /// s pushes the spring (a force from the acceleration, as inertia does): the nose lifts, the hull pitches back, then settles with one small
    /// opposite swing (<see cref="Hz"/> Hz, damping <see cref="Damp"/>: peak about 0.17 s after the push, under a tenth of it about 0.6 s later).</item>
    /// <item>FULL STOP (the drawn speed down to rest after it drove at least <see cref="MinDriveS"/> s): the speed it still carried a moment
    /// before (its top speed, let down by <see cref="RecentDecay"/> m/s every second: a hard stop throws it, a long slow roll-out hardly) pushes
    /// it the other way at once: the nose dips, the rear lifts, then it settles.</item>
    /// <item>Size: <see cref="DegPerMps"/> degrees per m/s of speed gained or lost (the drawn speed at 1x game speed), at most <see cref="MaxDeg"/>.
    /// Backing up: the same, mirrored (a reverse start dips the nose, a reverse stop lifts it).</item>
    /// <item>Nothing while it cruises, turns in place, slows without stopping or speeds up again without having stood (no input then); the
    /// drawn speed is low-passed (<see cref="SpeedTau"/> for the push, <see cref="FastTau"/> for telling moving from standing: the stop's push
    /// lands as the hull comes to rest, not after) and both ends have hysteresis, so it never jitters.</item>
    /// <item>Nothing while the ram / crush owns the tank (the rules' ram state, the drawn lunge, the Bizon's crush ride) or while it is the one
    /// rammed: what swings eases out over <see cref="OwnedFadeS"/> s and no start / stop counts until <see cref="AfterRamS"/> s after the ram.
    /// A vehicle emptied this frame (EXIT, sniped) stands still at once (as its other springs: View/UnitView.QuietStop.cs). No wreck (UnitView
    /// does not sync a dying unit).</item>
    /// </list>
    /// <para>Where: UnitView.Sync, right after ApplyRecoil and before GroundGearAfter (one line in View/UnitView.cs): the root (hull + turret)
    /// is turned like the shot's rock (View/HeavyRecoil.cs); GroundGearAfter then keeps the hull's bottom from going below where it rests on its
    /// tracks (it rocks about the end that touches, never into the tracks or the ground) and puts every gear node back on the ground, so the
    /// tracks stay exactly where they were. Planted gear only (TrackGround on): with it off nothing is drawn (the tracks would tip too).</para>
    /// Switch <see cref="On"/> (off = as before, nothing of it runs).
    /// </summary>
    public static class OzHeavyInertia
    {
        /// <summary>The switch.</summary>
        public static bool On = true;
        /// <summary>Peak pitch (deg) per m/s of drawn speed gained from a standstill or lost into a full stop, and the largest.</summary>
        public static float DegPerMps = 1.5f, MaxDeg = 1.8f;
        /// <summary>The spring: natural frequency (Hz) and damping ratio (0.45: one opposite swing of about 20 %).</summary>
        public static float Hz = 1.5f, Damp = 0.45f;
        /// <summary>The drawn speed's low-pass (s) and the moving / standing hysteresis (m/s at 1x game speed).</summary>
        public static float SpeedTau = 0.08f, FastTau = 0.03f, MoveOnMps = 0.25f, MoveOffMps = 0.08f;
        /// <summary>A start counts after standing this long (s); its push lasts this long (s); a stop counts after driving this long (s); the
        /// speed a stop throws forward: the top speed it drove at, let down by RecentDecay m/s every second (a gentle roll-out throws nothing).</summary>
        public static float StandS = 0.4f, StartWindowS = 0.8f, MinDriveS = 0.4f, RecentDecay = 0.8f;
        /// <summary>The ram / crush owning the tank: the swing eases out over this (s); starts / stops count again this long after (s).</summary>
        public static float OwnedFadeS = 0.25f, AfterRamS = 1.0f;
        /// <summary>Statistics (the Play check / sheets): starts and stops pushed, frames drawn with a rock, the largest rock drawn (deg).</summary>
        public static int Starts, Stops, Frames, OwnedFrames, QuietSnaps;
        public static float MaxDegSeen;

        /// <summary>The heavy vehicles that rock: the heavy tank (v1 and v2), the Mammoth, the Bizon.</summary>
        public static bool Is(string type) => type == OzHeavyTank.Type || type == OzBigTanks.Mammoth || type == OzBigTanks.Bizon;

        public static void ResetStats() { Starts = Stops = Frames = OwnedFrames = QuietSnaps = 0; MaxDegSeen = 0f; }

        /// <summary>Exact damped oscillator step (any dt, no blow-up), the HeavyRecoil spring.</summary>
        internal static void Step(ref float x, ref float v, float omega, float zeta, float dt)
        {
            if (dt <= 0f || (x == 0f && v == 0f)) return;
            float a = zeta * omega, wd = omega * Mathf.Sqrt(1f - zeta * zeta);
            float e = Mathf.Exp(-a * dt), c = Mathf.Cos(wd * dt), s = Mathf.Sin(wd * dt);
            float A = x, B = (v + a * x) / wd;
            x = e * (A * c + B * s);
            v = e * (c * (B * wd - a * A) - s * (A * wd + a * B));
            if (Mathf.Abs(x) < 1e-5f && Mathf.Abs(v) < 1e-4f) { x = 0f; v = 0f; }
        }

        internal static float Omega => 2f * Mathf.PI * Mathf.Max(0.1f, Hz);

        /// <summary>The start speed (deg/s) a spring at rest needs for its first peak to be `peak` deg.</summary>
        internal static float Impulse(float peak)
        {
            float z = Mathf.Clamp(Damp, 0.05f, 0.95f), r = Mathf.Sqrt(1f - z * z);
            return peak * Omega / Mathf.Exp(-(z / r) * Mathf.Atan2(r, z));
        }
    }

    public partial class VehicleMotion
    {
        /// <summary>R21 (View/OzHeavyInertia.cs): the drawn speed along the hull this frame's Step rolled the tracks with (m/s, - = backing up);
        /// 0 when Step did not run this frame.</summary>
        public float InertiaSpeed => Time.frameCount - lastStepFrame <= 0 ? lastSpeed : 0f;
        /// <summary>R21: the Bizon's crush ride holds the hull (its target or its eased share), 0 otherwise.</summary>
        public float InertiaRideHold => Mathf.Max(RideHoldTarget, rideHold);
    }

    public partial class UnitView
    {
        float hiX, hiV, hiSpeed, hiFast, hiStand, hiDrive, hiStartAge = -1f, hiStartSign, hiStartSum, hiRecent, hiRecentSign = 1f, hiGate = 1f, hiAfter;
        bool hiMoving, hiInit, hiPivotOk; Vector3 hiPivotL;

        /// <summary>Proof (sheets / Play check): the inertia pitch drawn this frame (deg, + = nose up).</summary>
        public float HeavyInertiaNow { get; private set; }

        /// <summary>UnitView.Sync, right after ApplyRecoil, before GroundGearAfter (one line in View/UnitView.cs): the spring moves on and its
        /// pitch goes onto the root (hull + turret) about the pivot.</summary>
        void HeavyInertiaStep(float dt)
        {
            HeavyInertiaNow = 0f;
            if (!OzHeavyInertia.On || Obj == null || Obj.Destroyed || Obj.Kind != ZKind.Vehicle || !OzHeavyInertia.Is(Obj.Type) || !motion || !gearValid || game == null)
            { HeavyInertiaRest(); return; }
            if (!hiInit) HeavyInertiaInit();
            if (!hiPivotOk) return;
            if (OzVehicleQuiet.Empty(Obj))
            {   // emptied (EXIT, sniped): still at once, as every other spring of the vehicle (UnitView.QuietStop)
                if (hiX != 0f || hiV != 0f) OzHeavyInertia.QuietSnaps++;
                HeavyInertiaRest(); return;
            }
            bool owned = Obj.Ramming || Obj.RamDoomed || motion.InertiaRideHold > 0f || TryGetComponent<OzRamLunge>(out _);
            if (owned) { hiAfter = OzHeavyInertia.AfterRamS; OzHeavyInertia.OwnedFrames++; }
            else if (dt > 0f) hiAfter = Mathf.Max(0f, hiAfter - dt);
            hiGate = Mathf.MoveTowards(hiGate, owned ? 0f : 1f, dt / Mathf.Max(0.01f, OzHeavyInertia.OwnedFadeS));
            if (dt > 0f) HeavyInertiaFeed(dt, hiAfter > 0f);
            if (hiGate <= 0f) { hiX = hiV = 0f; }
            OzHeavyInertia.Step(ref hiX, ref hiV, OzHeavyInertia.Omega, Mathf.Clamp(OzHeavyInertia.Damp, 0.05f, 0.95f), dt);
            float cap = OzHeavyInertia.MaxDeg * 1.15f;
            if (hiX > cap) { hiX = cap; if (hiV > 0f) hiV = 0f; } else if (hiX < -cap) { hiX = -cap; if (hiV < 0f) hiV = 0f; }
            float deg = hiX * Mathf.SmoothStep(0f, 1f, hiGate);
            if (Mathf.Abs(deg) < 1e-4f) return;
            // about the pivot (the hull's middle at the track tops, the root's rest space): the front (the way it drives) up for + deg
            var up = gearRot * Vector3.up;
            var front = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward; front -= up * Vector3.Dot(front, up);
            if (front.sqrMagnitude < 1e-6f) return;
            var axis = Vector3.Cross(front.normalized, up);
            var pivot = transform.TransformPoint(hiPivotL);
            var q = Quaternion.AngleAxis(deg, axis);
            transform.SetPositionAndRotation(pivot + q * (transform.position - pivot), q * transform.rotation);
            HeavyInertiaNow = deg; OzHeavyInertia.Frames++;
            if (Mathf.Abs(deg) > OzHeavyInertia.MaxDegSeen) OzHeavyInertia.MaxDegSeen = Mathf.Abs(deg);
        }

        /// <summary>The drawn speed (low-passed, at 1x game speed) and its start / stop events: the pushes into the spring.</summary>
        void HeavyInertiaFeed(float dt, bool blocked)
        {
            float g = Mathf.Max(0.25f, game.gameSpeed);
            float prev = hiSpeed, raw = motion.InertiaSpeed / g;
            hiSpeed = Mathf.Lerp(hiSpeed, raw, 1f - Mathf.Exp(-dt / Mathf.Max(0.005f, OzHeavyInertia.SpeedTau)));
            hiFast = Mathf.Lerp(hiFast, raw, 1f - Mathf.Exp(-dt / Mathf.Max(0.005f, OzHeavyInertia.FastTau)));
            float a = Mathf.Abs(hiFast), dv = hiSpeed - prev;
            bool was = hiMoving;
            if (hiMoving ? a < OzHeavyInertia.MoveOffMps : a > OzHeavyInertia.MoveOnMps) hiMoving = !hiMoving;
            float k = OzHeavyInertia.DegPerMps, cap = OzHeavyInertia.MaxDeg;
            if (blocked) { hiStartAge = -1f; hiStand = 0f; hiDrive = 0f; hiRecent = 0f; return; }   // the ram / crush owns it: nothing counts, a fresh standstill first
            if (hiMoving && !was && hiStand >= OzHeavyInertia.StandS)
            {   // a start from a standstill: the speed already gained pushes now, the rest of the window follows
                hiStartAge = 0f; hiStartSign = Mathf.Sign(hiFast); hiStartSum = 0f; OzHeavyInertia.Starts++;
                dv = hiSpeed;
            }
            if (hiStartAge >= 0f)
            {
                if (hiStartAge > OzHeavyInertia.StartWindowS || !hiMoving) hiStartAge = -1f;
                else
                {
                    float push = dv * hiStartSign;                                                    // speed gained the way it set off
                    if (push > 0f && hiStartSum * k < cap)
                    {
                        float use = Mathf.Min(push, cap / Mathf.Max(1e-4f, k) - hiStartSum); hiStartSum += use;
                        hiV += OzHeavyInertia.Impulse(k * use) * hiStartSign;                          // forward start: the nose up (+)
                    }
                    hiStartAge += dt;
                }
            }
            if (hiMoving)
            {
                hiStand = 0f; hiDrive += dt;
                hiRecent = Mathf.Max(a, hiRecent - OzHeavyInertia.RecentDecay * dt); if (a > 0.05f) hiRecentSign = Mathf.Sign(hiFast);
            }
            else
            {
                if (was && hiDrive >= OzHeavyInertia.MinDriveS)
                {   // a full stop: the speed it carried throws it forward (the nose down for a forward stop)
                    float lost = Mathf.Min(hiRecent, cap / Mathf.Max(1e-4f, k));
                    hiV -= OzHeavyInertia.Impulse(k * lost) * hiRecentSign;
                    OzHeavyInertia.Stops++; hiStartAge = -1f;
                }
                hiDrive = 0f; hiStand += dt; hiRecent = 0f;
            }
        }

        /// <summary>Nothing drawn (switched off, not a heavy vehicle, no planted gear, emptied): every value at rest; the next frame starts afresh.</summary>
        void HeavyInertiaRest()
        {
            hiX = hiV = 0f; hiStartAge = -1f; hiStand = 0f; hiDrive = 0f; hiRecent = 0f; hiMoving = false; hiSpeed = 0f; hiFast = 0f; hiGate = 1f; hiAfter = 0f;
        }

        /// <summary>The pivot, once (the root's rest space, from the planted gear's own rest boxes: no mesh reading): the middle of the hull's
        /// length (its bottom box, UnitView.GroundGear) at the height of the highest track top (a track's mesh box, or a link track's baked box).</summary>
        void HeavyInertiaInit()
        {
            hiInit = true; hiPivotOk = false;
            if (gearNodes == null || gearHullRoot == null || gearHullRoot.Length == 0) return;
            float zlo = float.MaxValue, zhi = float.MinValue, top = float.MinValue, bottom = float.MaxValue;
            foreach (var c in gearHullRoot) { zlo = Mathf.Min(zlo, c.z); zhi = Mathf.Max(zhi, c.z); bottom = Mathf.Min(bottom, c.y); }
            foreach (var g in gearNodes)
            {
                if (g == null || !g.t || g.spun) continue;
                var mf = g.t.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh)
                {
                    var bb = mf.sharedMesh.bounds; var m = g.chain * Matrix4x4.TRS(g.restPos, g.restRot, g.scale);
                    for (int i = 0; i < 8; i++)
                        top = Mathf.Max(top, m.MultiplyPoint3x4(bb.center + Vector3.Scale(bb.extents, new Vector3((i & 1) * 2 - 1, ((i >> 1) & 1) * 2 - 1, ((i >> 2) & 1) * 2 - 1))).y);
                }
                else { var (lo, hi) = LinkGearBox(g); if (hi.x > lo.x) top = Mathf.Max(top, hi.y); }
            }
            if (top == float.MinValue || top < bottom) top = bottom;
            hiPivotL = new Vector3(0f, top, (zlo + zhi) * 0.5f);
            hiPivotOk = zhi > zlo;
        }
    }
}
