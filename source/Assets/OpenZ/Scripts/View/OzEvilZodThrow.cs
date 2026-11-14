using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only. A robot EvilZod kills with a close shot is thrown back, away from him, the way the nuke's wave throws robots (OpenZEnding.Release:
    /// UnitView.Die with the blast flag, thrown from a point on the blast's side), instead of the pop into parts Zod's rule gives every bullet kill.
    /// <list type="bullet">
    /// <item>Close: the shot was fired from inside <see cref="ThrowBandPx"/> = ZEvilZod.FullBandPx (64 px, 8 m centre to centre), the band where his shot is at its full number and kills
    /// any robot in one; and the dash shot, which always takes the full number. Every farther kill of his, and every other killer: today's death, untouched.</item>
    /// <item>The throw is the game's blast throw: a real ragdoll (RobotRagdoll.Launch: up and away with a tumble, RagdollFlail's limbs). It arcs, lands by
    /// physics (no snap: the ragdoll settles, then stays as a corpse) and lies there as every blast-killed robot does, then pops away as they do. Its sounds
    /// are theirs: the whoosh as it leaves and the metal thud as it lands (OzBodySounds), the blast death's dust and cry (LegacyMatch.RobotDeath).
    /// The power is the ragdoll's blast proximity: <see cref="PowerNear"/> (the nuke's own 1) point blank, down to <see cref="PowerEdge"/> at the band's
    /// edge; a dash shot x <see cref="DashPower"/>.</item>
    /// <item>When: on the frame his first pellet reaches the body (OzEvilZodFx.Land through OzEvilZodHits.Land calls <see cref="PelletOnBody"/>); the body keeps its pose until then,
    /// as it does for every bullet kill (UnitView.HoldForDeath). With no pellet drawn (no effects player), at the latest when one would have arrived.</item>
    /// <item>No free ragdoll (RobotRagdoll.MaxActive at once), in lava, EvilZod himself: today's death. The scripted "airborne" flight is never used here.</item>
    /// </list>
    /// The rules are not touched: the death tick, the damage and the kill credit are the sim's; this only reads them. Switch <see cref="On"/>: off = today's
    /// death for every kill.
    /// </summary>
    [DefaultExecutionOrder(83)]   // after OzEvilZodFx (82): its pellets land in its LateUpdate and throw at once; this LateUpdate only throws the late ones
    public sealed class OzEvilZodThrow : MonoBehaviour
    {
        public static bool On = true;
        /// <summary>The band (px, centre to centre) a kill is thrown from: the rules' ZEvilZod.FullBandPx (64), where his shot kills any robot in one. A view number only.</summary>
        public static int ThrowBandPx = ZEvilZod.FullBandPx;
        /// <summary>The ragdoll's blast proximity point blank and at the edge of the band, the dash shot's factor; LateMargin: seconds after the latest the
        /// pellet could arrive; PelletReach: metres from the body a pellet may land and still count; RecentHit: seconds a pellet waits for its death to come through.</summary>
        public static float PowerNear = 1f, PowerEdge = 0.75f, DashPower = 1.1f, LateMargin = 0.25f, PelletReach = 2.5f, RecentHit = 0.4f;
        /// <summary>Play check: thrown (by a pellet, by the late clock), kept as today because far, because no ragdoll was free; held bodies whose view went.</summary>
        public static int Thrown, ByPellet, ByLate, KeptFar, KeptBusy, Gone;
        /// <summary>Play check: the last body taken, the range of its shot (px, centre to centre), a dash shot, its power.</summary>
        public static int LastVictim = -1, LastRangePx;
        public static bool LastDash;
        public static float LastPower;
        public static string Status => "thrown " + Thrown + " (pellet " + ByPellet + ", late " + ByLate + "), kept far " + KeptFar + ", kept busy " + KeptBusy + ", gone " + Gone + ", waiting " + pending.Count;

        struct Pending { public UnitView v; public ZObject o; public ZEvent e; public int killer; public Vector3 from; public float prox, due; }
        struct Hit { public int shooter; public Vector3 at; public float t; }
        static readonly List<Pending> pending = new List<Pending>();
        static readonly List<Hit> recent = new List<Hit>();
        static OzEvilZodThrow inst;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { pending.Clear(); recent.Clear(); inst = null; Thrown = ByPellet = ByLate = KeptFar = KeptBusy = Gone = 0; LastVictim = -1; LastRangePx = 0; LastDash = false; LastPower = 0f; }

        /// <summary>CombatView.Death, a robot: true = EvilZod's close shot killed it and it is held here until his pellet arrives (CombatView does nothing more).</summary>
        public static bool TakesDeath(ZGame game, ZEvent e, ZObject o, UnitView v)
        {
            if (!On || game == null || game.World == null || o == null || !v || !v.isActiveAndEnabled || v.Dying) return false;
            if (o.Kind != ZKind.Robot || o.IsEvilZod || e.flag || e.flag2 || e.otherId < 0) return false;   // a flame or a blast is not his shot
            var k = game.World.GetAny(e.otherId);
            if (k == null || !k.IsEvilZod) return false;
            if (o.LavaKilled || o.InLava) return false;                                                  // lava melts it, as before
            long dx = k.CenterX - o.CenterX, dy = k.CenterY - o.CenterY;
            int d = (int)System.Math.Sqrt((double)(dx * dx + dy * dy));                                   // = (int)DistanceFrom in ZObject.EvilFire
            long shot = k.EvilShotTick - ZEvilZod.CycleTicks;                                             // the tick of his last shot
            bool dash = k.EvilDashTargetId == o.Id && k.EvilArrivedAt >= 0 && shot >= k.EvilArrivedAt && shot - k.EvilArrivedAt <= ZEvilZod.DashShotHoldTicks;
            if (d > ThrowBandPx && !dash) { KeptFar++; return false; }
            if (!RobotRagdoll.CanStart()) { KeptBusy++; return false; }
            var p = v.transform.position;
            HitReactions.ShooterPos(game, k, out var kp);
            var from = kp; from.y = p.y - 2.5f;                                                          // as OpenZEnding.Release: a point on his side, under the body: out and up, away from him
            float prox = Mathf.Lerp(PowerNear, PowerEdge, Mathf.Clamp01(d / (float)Mathf.Max(1, ThrowBandPx))) * (dash ? DashPower : 1f);
            float gs = Mathf.Max(0.25f, game.gameSpeed), metres = Vector3.Distance(kp, p) + 1f;
            float wait = (OzEvilZodBody.ShotWaitMax + metres / Mathf.Max(1f, OzEvilZodFx.PelletMps * 0.75f)) / gs + LateMargin;   // the gun's hold, then the slowest pellet
            HitReactions.NoteImpulse(o, "death_shot", p, from, from, kp);
            v.HoldForDeath(wait + 0.5f);
            pending.Add(new Pending { v = v, o = o, e = e, killer = k.Id, from = from, prox = prox, due = Time.time + wait });
            LastVictim = o.Id; LastRangePx = d; LastDash = dash; LastPower = prox;
            if (!inst) inst = new GameObject("OzEvilZodThrow").AddComponent<OzEvilZodThrow>();
            for (int i = recent.Count - 1; i >= 0; i--)                                                  // his pellet was drawn before the rules' death came through
                if (recent[i].shooter == k.Id && Time.time - recent[i].t <= RecentHit && Flat(recent[i].at, p) <= PelletReach) { recent.RemoveAt(i); Throw(pending.Count - 1, true); break; }
            return true;
        }

        /// <summary>OzEvilZodHits.Land: a pellet of `shooter` reached a body at `at`: the held body it hit flies now.</summary>
        public static void PelletOnBody(int shooter, Vector3 at)
        {
            if (!On) return;
            int best = -1; float bd = PelletReach;
            for (int i = 0; i < pending.Count; i++)
            {
                var q = pending[i]; if (q.killer != shooter || !q.v) continue;
                float m = Flat(q.v.transform.position, at); if (m <= bd) { bd = m; best = i; }
            }
            if (best >= 0) { Throw(best, true); return; }
            recent.Add(new Hit { shooter = shooter, at = at, t = Time.time });
            if (recent.Count > 16) recent.RemoveAt(0);
        }

        static void Throw(int i, bool byPellet)
        {
            var q = pending[i]; pending.RemoveAt(i);
            if (!q.v || !q.v.isActiveAndEnabled || q.v.Dying) { Gone++; return; }
            var p = q.v.transform.position; var au = CombatAudio.I;
            OzDeathTrace.DeathEvent(q.e, q.o, q.from);
            if (RobotRagdoll.CanStart())
            {
                q.v.Die(false, true, q.from, q.killer, q.prox);                                          // the blast death: a ragdoll, up and away from him
                if (!q.v.Popped) LegacyMatch.RobotDeath(p, q.from, true, au);                           // as CombatView: a pop (the DV20 Shares mix) plays its own crunch + smoke
                Thrown++; if (byPellet) ByPellet++; else ByLate++;
            }
            else
            {   // every ragdoll is busy this frame: today's death after all (never the scripted flight)
                KeptBusy++;
                q.v.Die(false, false, q.from, q.killer, 1f);
                if (!q.v.Popped) LegacyMatch.RobotDeath(p, q.from, false, au);
            }
        }

        static float Flat(Vector3 a, Vector3 b) { float x = a.x - b.x, z = a.z - b.z; return Mathf.Sqrt(x * x + z * z); }

        void LateUpdate()
        {
            float now = Time.time;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (!pending[i].v) { pending.RemoveAt(i); Gone++; continue; }
                if (now >= pending[i].due) Throw(i, false);
            }
            for (int i = recent.Count - 1; i >= 0; i--) if (now - recent[i].t > RecentHit) recent.RemoveAt(i);
        }

        void OnDestroy() { if (inst == this) { inst = null; pending.Clear(); recent.Clear(); } }
    }
}
