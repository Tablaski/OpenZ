using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// DV146 (the mech's hand gun), the drawn aim and the muzzle flash, for View/MechHandGunView.cs (its hooks call these; the rule of the aim:
    /// Sim/ZObject.MechHandGunAim.cs).
    /// <list type="bullet">
    /// <item>THE GUN TURNS FIRST: the arm aims at the robot the SIM chose (<see cref="ZObject.HandGunTarget"/>) from the frame it is chosen,
    /// while the sim waits for its arm (the turn at <see cref="ZWorld.HandGunTurnDegS"/> + the settle); a running burst at another robot
    /// (the last one fell, or left the arc) stops at once, its clip with it, so the arm turns in silence and then fires; with no robot
    /// chosen (the window closed) a running burst ends on its robot (its last point when it fell); after the last shot the arm holds
    /// <see cref="ZWorld.HandGunHoldS"/> (sim time, as the sim's arm), then goes back to rest. The drawn turn is a little faster than the
    /// sim's (<see cref="ViewRateMul"/>) and eased (<see cref="ViewTau"/>), both on the game speed, so the arm is on the robot when the sim
    /// fires.</item>
    /// <item>NO ROUND OFF THE ARM: a burst starts only when the barrel points at its robot within <see cref="GateDeg"/> (and the robot is the
    /// one the sim shot last); a round due while the barrel is off (the arm held at its reach) is not drawn; the killing round is drawn when
    /// the barrel points at the fallen robot within <see cref="KillGateDeg"/>, else not at all.</item>
    /// <item>THE MUZZLE FLASH, the Psycho's (WeaponsV2Runner.MgRound: hot core, cone, the brake's side flares, sparks every 2nd round, a
    /// strobing light every 3rd, smoke every 4th) at the mech gun's size: the Psycho's flash is several times his gun's bore; the mech's gun
    /// mouth is about 0.6 m wide, as wide as the Psycho's whole flash, and the flash centred ON the mouth sat half inside the barrel (hidden by
    /// it) and no bigger than the mouth: no flash to see. Now it is sized from the drawn mouth (<see cref="FlashMouthMul"/> x the mouth's width,
    /// within <see cref="FlashMin"/> .. <see cref="FlashMax"/> x the Psycho's), its core in front of the mouth (<see cref="FlashAhead"/> of
    /// its radius) and the side flares outside the barrel; a core whose pool is full goes to the glow channel, so every round has its flash.</item>
    /// </list>
    /// View only: nothing here changes the sim.
    /// </summary>
    public static class MechHandGunAim
    {
        /// <summary>The drawn turn: x the sim's turn rate, its smooth time (s at game speed 1), the pitch's top speed (deg/s); the gate (deg)
        /// a round needs; the killing round's gate (deg; the arm may have left by a frame's turn).</summary>
        public static float ViewRateMul = 1.5f, ViewTau = 0.05f, PitchDegS = 400f, GateDeg = 3f, KillGateDeg = 6f;
        /// <summary>The flash: its core = FlashMouthMul x the mouth's width, in Psycho flashes (0.5 m) between FlashMin and FlashMax; the core's
        /// centre FlashAhead x its radius in front of the mouth; the mouth's half-width when the model gives none (m).</summary>
        public static float FlashMouthMul = 1.6f, FlashMin = 1.2f, FlashMax = 2.6f, FlashAhead = 0.6f, MouthFallbackR = 0.3f;

        /// <summary>Proof counters (since the last reset): bursts held until the arm was on (frames), rounds not drawn off the arm, killing
        /// rounds not drawn (the arm off the fallen robot), bursts stopped for a new robot (of those with their clip stopped), first rounds of a burst, of those more than 3 deg off the barrel, the largest such
        /// angle; flashes (rounds with a core really emitted), first rounds with a flash, cores sent to the glow channel (the core's pool was
        /// full), cores lost (no room anywhere); new robots the arm turned to, the largest turn (deg) and the mean time (s) from the new robot to
        /// its first drawn round.</summary>
        public static int HeldFrames, RoundsOff, KillOff, BurstsCut, ClipsStopped, FirstRounds, FirstOver3, Flashes, FlashBursts, FlashToGlow, FlashLost, Switches, SwitchRounds;
        public static float FirstMaxDeg, SwitchTurnMaxDeg, SwitchDelaySum, FlashScaleSeen;

        sealed class St
        {
            public float off = 180f, s = -1f, mouthR, switchAt = -1f, switchDeg;
            public int aimId = -1; public Vector3 aimP; public bool switching; public Transform muzzle;
            public AudioSource src; public string clip;
        }
        static readonly Dictionary<int, St> st = new Dictionary<int, St>();
        static St S(int id) { if (!st.TryGetValue(id, out var s)) { s = new St(); st[id] = s; } return s; }

        static Vector3 AimPos(ZGame game, ZObject t)
        {
            if (game.Views.TryGetValue(t.Id, out var v) && v && v.isActiveAndEnabled) return v.AimPos();
            return game.CenterWorld(t) + Vector3.up;
        }

        static bool Alive(ZObject t) => t != null && !t.Destroyed && t.KillAt < 0;

        /// <summary>Pose: the point the arm aims at this frame (false = none: back to rest). The robot the sim chose; with none, a running
        /// burst's robot (its last point when it fell), else the robot shot last while the sim's arm holds on it.</summary>
        public static bool AimPoint(ZGame game, int id, ZObject mech, int lastTarget, bool inBurst, Vector3 lastTo, float now, out Vector3 p)
        {
            var s = S(id); p = Vector3.zero;
            if (game == null || game.World == null || mech == null) { s.aimId = -1; return false; }
            var t = mech.HandGunTarget;
            if (inBurst && lastTarget >= 0 && (t == null || t.Id == lastTarget))
            {
                var b = game.World.GetAny(lastTarget);
                p = Alive(b) ? AimPos(game, b) : lastTo;
                Note(s, lastTarget, now); s.aimP = p;
                return true;
            }
            if (t == null)
            {
                var last = game.World.GetAny(lastTarget);
                if (Alive(last) && game.World.Time - mech.HandGunLastShot < ZWorld.HandGunHoldS) t = last;
            }
            if (t == null) { s.aimId = -1; return false; }
            p = AimPos(game, t);
            Note(s, t.Id, now); s.aimP = p;
            return true;
        }

        static void Note(St s, int id, float now)
        {
            if (id == s.aimId) return;
            if (s.aimId >= 0) { s.switching = true; s.switchAt = now; s.switchDeg = s.off < 180f ? s.off : 0f; Switches++; }   // from one robot to another
            s.aimId = id;
        }

        /// <summary>Pose: the turn (deg, + outward = clockwise from above) and the climb (deg) from rest that put the BARREL'S LINE through p.
        /// The arm turns about the elbow (pivot) and the muzzle sits 1.2 .. 1.8 m in front of it, so the angle seen from the resting muzzle
        /// leaves the turned barrel off a near robot (v2: 9 deg at 7.5 m and 25 deg, more than the gate: no round would be drawn); solved on
        /// the pivot instead, exact for the pose's turn-then-climb (the climb about the rest line's side axis, the turn about up). f0 / m0:
        /// the barrel's direction and the muzzle at rest. A point too close to the pivot to solve: the angle from the resting muzzle.</summary>
        public static void Need(Vector3 pivot, Vector3 f0, Vector3 m0, Vector3 p, out float yaw, out float pitch)
        {
            var d = p - m0; var fa = new Vector3(f0.x, 0f, f0.z); var fb = new Vector3(d.x, 0f, d.z);
            yaw = fa.sqrMagnitude > 1e-6f && fb.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(fa, fb, Vector3.up) : 0f;   // the fallback: from the resting muzzle
            pitch = (90f - Vector3.Angle(Vector3.up, d)) - (90f - Vector3.Angle(Vector3.up, f0));
            if (fa.sqrMagnitude < 1e-6f) return;
            var F = fa.normalized; var H = Vector3.Cross(Vector3.up, F).normalized;
            Vector3 v = m0 - pivot, q = p - pivot;
            float qF = Vector3.Dot(q, F), qH = Vector3.Dot(q, H), r = Mathf.Sqrt(qF * qF + qH * qH);
            float vF = Vector3.Dot(v, F), vH = Vector3.Dot(v, H), vU = v.y;
            if (r < Mathf.Abs(vH) + 0.05f) return;
            float wF = Mathf.Sqrt(r * r - vH * vH), wU = q.y, wl = Mathf.Sqrt(wF * wF + wU * wU);   // the point in the turned arm's plane
            float a0 = Mathf.Atan2(f0.y, Vector3.Dot(f0, F)), k = Mathf.Cos(a0) * vU - Mathf.Sin(a0) * vF;   // the rest line's climb and its height off the pivot
            if (wl < Mathf.Abs(k) + 0.05f) return;
            yaw = (Mathf.Atan2(qH, qF) - Mathf.Asin(vH / r)) * Mathf.Rad2Deg;
            pitch = (Mathf.Atan2(wU, wF) - Mathf.Asin(k / wl) - a0) * Mathf.Rad2Deg;
        }

        /// <summary>Pose: the drawn turn toward (wantY, wantP) (deg from the arm's rest), on the game speed: a little faster than the sim's arm.</summary>
        public static void Turn(ref float yaw, ref float yawVel, ref float pitch, ref float pitchVel, float wantY, float wantP, float dt, float speed)
        {
            float tau = ViewTau / Mathf.Max(0.25f, speed);
            yaw = Mathf.SmoothDamp(yaw, wantY, ref yawVel, tau, (float)ZWorld.HandGunTurnDegS * ViewRateMul * speed, dt);
            pitch = Mathf.SmoothDamp(pitch, wantP, ref pitchVel, tau, PitchDegS * speed, dt);
        }

        /// <summary>Pose, after the arm is drawn: how far (deg) the barrel points off the aim point.</summary>
        public static void NoteOff(int id, Vector3 muzzle, Vector3 dir, bool aim, Vector3 aimP)
        {
            var s = S(id);
            var d = aimP - muzzle;
            s.off = aim && d.sqrMagnitude > 1e-4f ? Vector3.Angle(dir, d) : 180f;
            if (s.switching && s.off > s.switchDeg) s.switchDeg = s.off;                                      // the turn this new robot needs (the largest seen)
        }

        /// <summary>LateUpdate: may the burst clock run this frame? A running burst at the robot the arm aims at: yes (its rounds are gated
        /// one by one); one at another robot stops (the clip too). A new burst only with the barrel on the robot the sim shot last; while the
        /// arm turns to another robot the arming left from the last one is dropped.</summary>
        public static bool MayStep(int id, WeaponsV2Logic.MgBurst mg, int lastTarget, float now)
        {
            var s = S(id);
            if (mg.InBurst)
            {
                if (s.aimId < 0 || s.aimId == lastTarget) return true;
                mg.LastClip = mg.Clip; mg.Clip = -1; mg.ArmedUntil = -1f; mg.NextBurstAt = now + WeaponsV2Logic.MgPauseMin;   // the burst ends here
                if (s.src && s.src.isPlaying && s.src.clip && s.src.clip.name == s.clip) { s.src.Stop(); ClipsStopped++; }
                s.src = null; BurstsCut++;
                return false;
            }
            if (s.aimId == lastTarget && s.off <= GateDeg) return true;
            if (s.aimId >= 0 && s.aimId != lastTarget) mg.ArmedUntil = -1f;
            if (mg.ArmedUntil > 0f) HeldFrames++;
            return false;
        }

        /// <summary>LateUpdate: is this due round drawn (the barrel on its robot)?</summary>
        public static bool RoundOn(int id)
        {
            if (S(id).off <= GateDeg) return true;
            RoundsOff++;
            return false;
        }

        /// <summary>LateUpdate: draw the killing round (to the fallen robot's point `to`)? Only with the barrel on it within KillGateDeg.</summary>
        public static bool KillOn(Vector3 muzzle, Vector3 dir, Vector3 to)
        {
            var d = to - muzzle;
            if (d.sqrMagnitude < 1e-4f || Vector3.Angle(dir, d) <= KillGateDeg) return true;
            KillOff++;
            return false;
        }

        /// <summary>vehicle_exit_jump part B (fix round 1, View/MechHandGunView.Quiet.cs): the voice BurstSound recorded for this gun stops now
        /// (also a burst heard from off screen at CombatAudio's distant spot); true = it still played that burst's clip.</summary>
        public static bool StopBurstVoice(int id)
        {
            if (!st.TryGetValue(id, out var s) || !s.src) return false;
            bool on = s.src.isPlaying && s.src.clip && s.src.clip.name == s.clip;
            if (on) s.src.Stop();
            s.src = null;
            return on;
        }

        /// <summary>LateUpdate, right after a burst's clip was played at `pos`: the voice that plays it (so a burst cut for a new robot stops
        /// its sound too); none found = the clip plays out.</summary>
        public static void BurstSound(int id, string clip, Vector3 pos)
        {
            var s = S(id); s.src = null; s.clip = clip;
            var au = CombatAudio.I; if (!au || clip == null) return;
            AudioSource best = null; float bd = float.MaxValue;
            foreach (var a in au.GetComponentsInChildren<AudioSource>())
            {
                if (!a.isPlaying || !a.clip || a.clip.name != clip || a.time > 0.05f) continue;
                float d = (a.transform.position - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = a; }
            }
            s.src = best;
        }

        /// <summary>The drawn mouth's half-width (m): the barrel's meshes (under the muzzle node's parent: the spinning ring, else the gun)
        /// measured across the shot axis from their mesh bounds.</summary>
        static float MouthRadius(Transform muzzle, Transform recoil)
        {
            var root = muzzle && muzzle.parent ? muzzle.parent : recoil;
            if (!root || !muzzle) return MouthFallbackR;
            Vector3 o = muzzle.position, ax = muzzle.right, ay = muzzle.up;
            float rx = 0f, ry = 0f; int n = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh; if (!m) continue;
                var b = m.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    var w = mf.transform.TransformPoint(c) - o;
                    rx = Mathf.Max(rx, Mathf.Abs(Vector3.Dot(w, ax))); ry = Mathf.Max(ry, Mathf.Abs(Vector3.Dot(w, ay)));
                }
                n++;
            }
            float r = n > 0 ? 0.5f * (rx + ry) : MouthFallbackR;
            return r > 0.02f && r < 3f ? r : MouthFallbackR;
        }

        /// <summary>Round: the Psycho's muzzle flash at the mech gun's mouth (`round` already counts this round: 1 = a burst's first).
        /// Also the first-round check: the angle between the barrel and the line to the aim point.</summary>
        public static void Flash(int id, int round, OpenZFxPlayer z, Vector3 from, Vector3 dir, Vector3 aimAt, Transform muzzle, Transform recoil, float now)
        {
            var s = S(id);
            if (s.s < 0f || s.muzzle != muzzle)
            {
                s.muzzle = muzzle;
                s.mouthR = MouthRadius(muzzle, recoil);
                s.s = Mathf.Clamp(FlashMouthMul * 2f * s.mouthR / 0.5f, FlashMin, FlashMax);
                FlashScaleSeen = Mathf.Max(FlashScaleSeen, s.s);
            }
            if (round == 1)
            {
                FirstRounds++;
                var d = aimAt - from;
                float a = d.sqrMagnitude > 1e-4f ? Vector3.Angle(dir, d) : 0f;
                if (a > 3f) FirstOver3++;
                if (a > FirstMaxDeg) FirstMaxDeg = a;
                if (s.switching)
                {
                    s.switching = false; SwitchRounds++;
                    if (s.switchDeg > SwitchTurnMaxDeg) SwitchTurnMaxDeg = s.switchDeg;
                    SwitchDelaySum += Mathf.Max(0f, now - s.switchAt);
                }
            }
            float k = s.s, flick = Random.Range(0.85f, 1.15f), core = 0.5f * k * flick;
            var c = from + dir * (0.5f * core * FlashAhead);                                                  // the core's centre in front of the mouth
            bool emitted;
            if (z.ChannelRoom("m12_core"))
            {
                int n0 = z.ChannelCount("m12_core");
                z.EmitChannel("m12_core", c, Vector3.zero, 0.05f, core, OpenZFxCatalog.M12Core.color, 1f);
                emitted = z.ChannelCount("m12_core") > n0;
            }
            else emitted = false;
            if (!emitted && z.ChannelRoom("wv2_glow")) { z.EmitChannel("wv2_glow", c, Vector3.zero, 0.05f, core * 1.15f, OpenZFxCatalog.M12Core.color, 1f); emitted = true; FlashToGlow++; }
            if (emitted) { Flashes++; if (round == 1) FlashBursts++; } else FlashLost++;
            z.EmitChannel("leg_cone", from + dir * 0.35f * k, dir * 0.05f, Random.Range(0.035f, 0.07f), 0.3f * k * flick, OpenZFxCatalog.MuzzleConeColor, 0.9f);
            var brake = Vector3.Cross(Vector3.up, dir); brake = brake.sqrMagnitude > 1e-6f ? brake.normalized : Vector3.right;
            float side = Mathf.Max(0.14f * k, s.mouthR + 0.06f);                                             // outside the barrel
            z.EmitChannel("leg_cone", from + dir * 0.05f + brake * side, brake * 0.05f, 0.04f, 0.14f * k, OpenZFxCatalog.MuzzleConeColor, 0.8f);
            z.EmitChannel("leg_cone", from + dir * 0.05f - brake * side, -brake * 0.05f, 0.04f, 0.14f * k, OpenZFxCatalog.MuzzleConeColor, 0.8f);
            if ((round & 1) == 0)
                for (int i = 0; i < 2; i++)
                    z.EmitChannel("leg_spark_light", c, dir * Random.Range(1.5f, 3.2f) + Random.insideUnitSphere * 1.1f + Vector3.up * 0.6f, Random.Range(0.12f, 0.24f), 0.2f, OpenZFxCatalog.MuzzleSparkA, 1f);
            if (round % 3 == 0)
            {
                z.AddLight(c + dir * 0.2f, OpenZFxCatalog.LegLightColor, 4.2f, 4f, 0.07f);
                z.GroundLight(new Vector3(c.x, z.SurfaceY(c), c.z), 0.3f);
            }
            if (round % 4 == 0) z.EmitChannel("leg_smoke", c, -dir * 0.3f + Vector3.up * 0.5f, 0.5f, 0.14f * k, OpenZFxCatalog.MuzzleSmoke, 0.6f);
        }

        public static string Stats =>
            $"[MechHandGunAim] flash scale {FlashScaleSeen:0.00} x the Psycho's; flashes {Flashes} (to the glow channel {FlashToGlow}, lost {FlashLost}); first rounds of a burst {FirstRounds}, with a flash {FlashBursts}, " +
            $"more than 3 deg off the barrel {FirstOver3} (largest {FirstMaxDeg:0.0} deg); rounds not drawn off the arm {RoundsOff}; bursts held for the arm {HeldFrames} frames; killing rounds not drawn {KillOff}; " +
            $"bursts stopped for a new robot {BurstsCut} (their clip stopped {ClipsStopped}); " +
            $"new robots {Switches}, first rounds after one {SwitchRounds}, the arm turned up to {SwitchTurnMaxDeg:0.0} deg first, {(SwitchRounds > 0 ? SwitchDelaySum / SwitchRounds : 0f):0.00} s to the first round";

        public static void ResetStats()
        {
            HeldFrames = RoundsOff = KillOff = BurstsCut = ClipsStopped = FirstRounds = FirstOver3 = Flashes = FlashBursts = FlashToGlow = FlashLost = Switches = SwitchRounds = 0;
            FirstMaxDeg = SwitchTurnMaxDeg = SwitchDelaySum = FlashScaleSeen = 0f;
            foreach (var s in st.Values) s.s = -1f;
        }
    }
}
