using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.3 row 83 (rule DV167, Sim/ZEvilZod.Cone.cs), view only: the robots EvilZod's shot hits besides its target. The volley is OzEvilZodFx's own (its pellets
    /// already fly in that cone and on past the target); here each robot the rules hit in the cone gets what the target gets from a pellet that lands on it, at the
    /// moment a pellet reaches it: the hit sparks on its body (OpenZHitSparks, the robot surface) and, for a close kill, the throw (OzEvilZodThrow, row 71: the body
    /// the rules' death holds flies back from him when the pellet arrives). The moment: the volley's own rule (OzEvilZodFx.Shoot: once his gun is up,
    /// OzEvilZodBody.ShotReadyAt, at most OzEvilZodBody.ShotWaitMax after the rules' shot), then the pellet's flight at OzEvilZodFx.PelletMps.
    /// The first <see cref="LogLines"/> cone hits of a run are written to the log: in a built exe that is Player.log, the proof outside the editor.
    /// Switch <see cref="On"/>: off = no sparks here and no pellet timing (a close kill is still thrown, by OzEvilZodThrow's late clock).
    /// </summary>
    [DefaultExecutionOrder(84)]   // after OzEvilZodFx (82, draws the volley) and OzEvilZodThrow (83)
    public sealed class OzEvilZodCone : MonoBehaviour
    {
        public static bool On = true;
        /// <summary>How many cone hits of a run go to the log.</summary>
        public static int LogLines = 6;
        /// <summary>Play check: cone hits seen, kills among them, bodies a pellet reached here, lines logged.</summary>
        public static int Hits, Kills, Reached, Logged;
        public static string Status => "cone hits " + Hits + " (killed " + Kills + "), reached " + Reached + ", waiting " + (waiting.Count + flying.Count);

        struct Wait { public int shooter, victim; public float at; }
        struct Fly { public int shooter, victim; public Vector3 pos, dir; public float at; }
        static readonly List<Wait> waiting = new List<Wait>();
        static readonly List<Fly> flying = new List<Fly>();
        static OzEvilZodCone inst;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { waiting.Clear(); flying.Clear(); inst = null; Hits = Kills = Reached = Logged = 0; }

        /// <summary>CombatView: the rules' cone hit.</summary>
        public static bool Wants(ZEventType t) => t == ZEventType.EvilConeHit;

        /// <summary>CombatView, one line per event: a = EvilZod, b = the robot his shot hit in its cone.</summary>
        public static void Handle(ZEvent e, ZObject a, ZObject b)
        {
            Hits++; if (e.flag2) Kills++;
            if (Logged < LogLines)
            {
                Logged++;
                Debug.Log("[OpenZ EvilZod] cone hit (DV167): " + (b != null ? b.Type : "robot") + " #" + e.otherId + " at " + e.radius + " px took " + (int)e.value + " HP"
                          + (e.flag2 ? ", killed" : "") + (e.flag ? ", dash shot" : "") + " (cone hits so far " + Hits + ")");
            }
            if (!On || a == null) return;
            waiting.Add(new Wait { shooter = a.Id, victim = e.otherId, at = Time.time });
            if (!inst) inst = new GameObject("OzEvilZodCone").AddComponent<OzEvilZodCone>();
        }

        void LateUpdate()
        {
            var g = ZGame.I;
            if (!g) { waiting.Clear(); flying.Clear(); return; }
            float gs = Mathf.Max(0.25f, g.gameSpeed), now = Time.time;
            for (int i = 0; i < waiting.Count; i++)
            {
                var w = waiting[i];
                if (!g.Views.TryGetValue(w.shooter, out var sv) || !sv) { waiting.RemoveAt(i--); continue; }
                var body = sv.GetComponentInChildren<OzEvilZodBody>();
                float late = (now - w.at) * gs;
                if (body && body.FireWeight < OzEvilZodBody.ShotReadyAt && late < OzEvilZodBody.ShotWaitMax && !sv.Dying) continue;   // the volley's own moment (OzEvilZodFx.Shoot)
                if (OzEvilZodFx.ShotWaiting(sv) && late < 1f && !sv.Dying) continue;           // 1.4 DV174: the volley still waits for his drawn gun to face the target
                waiting.RemoveAt(i--);
                if (!g.Views.TryGetValue(w.victim, out var vv) || !vv) continue;
                Vector3 from = body ? body.MuzzlePos : sv.MuzzlePos(), to = vv.AimPos(), d = to - from;
                float dist = d.magnitude;
                flying.Add(new Fly { shooter = w.shooter, victim = w.victim, pos = to, dir = dist > 0.01f ? d / dist : sv.transform.forward, at = now + dist / Mathf.Max(1f, OzEvilZodFx.PelletMps * gs) });
            }
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                if (now < flying[i].at) continue;
                var f = flying[i]; flying.RemoveAt(i);
                Reach(f, g);
            }
        }

        /// <summary>A pellet reaches a robot of the cone: the held close kill flies (OzEvilZodThrow), the body sparks as the target's does.</summary>
        static void Reach(Fly f, ZGame g)
        {
            Reached++;
            var pos = f.pos;
            if (g.Views.TryGetValue(f.victim, out var vv) && vv) pos = vv.AimPos();                // where the body is drawn now
            OzEvilZodThrow.PelletOnBody(f.shooter, pos);
            if (!OzShakeGate.OnScreen(pos)) return;
            if (OpenZHitSparks.Burst(pos, f.dir, FxWeapon.Rifle, FxSurface.Unit, 1.2f, true)) return;
            var z = OpenZFxPlayer.I; if (!z) return;
            for (int i = 0; i < 4; i++) z.EmitChannel("wv2_spark", pos, (-f.dir + Random.insideUnitSphere) * Random.Range(2f, 5f), Random.Range(0.15f, 0.3f), 0.05f, new Color(1f, 0.8f, 0.5f), 1f);
        }

        void OnDestroy() { if (inst == this) { inst = null; waiting.Clear(); flying.Clear(); } }
    }
}
