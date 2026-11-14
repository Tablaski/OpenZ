using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// that are on forts. the projectlies either fly to tower floor on ground or they fly up to sky and then down.. they should be attacking
    /// View only: the rules are untouched.
    /// <para>Cause: the rules aim every shell, rocket and grenade at a point of the map (ZMissile x, y: the target's centre + Zod's lead and
    /// scatter), and CombatView draws it to that point ON THE TERRAIN (PxGround). A fort tower gun is drawn on its tower top (TowerMounts:
    /// about 9 m up and up to 3.7 m from its sim footprint), so the drawn round flew to the ground inside the fort / at the tower's foot, and
    /// the arcing ones (shells with a drop, rockets, grenades) climbed and fell onto that ground point; the blast was drawn there too. A
    /// missed rifle / laser round was bent down to the ground (HitSurface.MissGround). The flying shuttle had the same problem and is solved
    /// by OzShuttleAim (the aim lifted to the drawn hull, misses past it into the sky): this class is the same for tower guns, hooked into the
    /// same calls, so every weapon gets it (CombatView MissileFired / MissileExploded / DrawShot misses, UnitAim and RobotWeaponPose through
    /// OzShuttleAim.AirAim, the salvo extras through OzShuttleAim.SalvoEndY).</para>
    /// <para>The rule: the rules' aim point's offset from the gun's own sim centre (lead + scatter), halved and at most <see cref="SpreadM"/>,
    /// is carried over to the drawn gun's middle (<see cref="Middle"/>): every round at a standing tower gun bursts inside Zod's blast radius
    /// of it (scatter +-16 px = +-2 m against a 40-50 px radius), so every round is drawn ending on or at the gun. A missed bullet / bolt
    /// passes the gun <see cref="MissNear"/> .. <see cref="MissFar"/> m from its middle and flies on into the sky (OzShuttleAim.MissPast;
    /// IsSkyMiss: no puff in mid-air). Salvo extras burst on the fort's top under them, else in the air beside the gun.</para>
    /// <para>Counts for the Play proof (<see cref="Recording"/>): per weapon the drawn rounds, their end height over the ground, their end's
    /// distance from the gun's middle, misses into the sky, blasts and low blasts (View/OzTowerShotProof.cs).</para>
    /// </summary>
    public static class OzTowerAim
    {
        /// <summary>The gun's middle over its seat (m, before the tower draw scale): UnitView.AimPos's 1.0 m.</summary>
        public const float MidM = 1.0f;
        /// <summary>The rules' aim offset carried over: x SpreadK, at most SpreadM m from the gun's middle.</summary>
        public const float SpreadK = 0.5f, SpreadM = 0.9f;
        /// <summary>A missed bullet / bolt passes the gun's middle this far (m) and flies on MissRun m.</summary>
        public const float MissNear = 2.0f, MissFar = 2.8f, MissRun = 14f;
        /// <summary>A salvo's main round ended within this distance (m) of a drawn tower gun: its extras burst up there too.</summary>
        public const float SalvoNearM = 4f;

        /// <summary>t is a cannon drawn on a fort tower (UnitView seated it on its pad).</summary>
        public static bool On(ZObject t, out UnitView v)
        {
            v = null; var g = ZGame.I;
            if (t == null || t.Kind != ZKind.Cannon || !g) return false;
            return g.Views.TryGetValue(t.Id, out v) && v && v.OnTower && v.TowerFort;
        }
        public static bool On(ZObject t) => On(t, out _);

        /// <summary>The drawn gun's middle: its pad seat (TowerMounts.SeatWorld: the base plate's centre on the mount) + MidM x its draw scale.</summary>
        public static Vector3 Middle(ZObject t, UnitView v) =>
            TowerMounts.SeatWorld(v.TowerFort, v.TowerPad) + v.TowerFort.up * (MidM * TowerMounts.CannonScaleFor(t.Type));

        /// <summary>OzShuttleAim.AirAim for a target that is not the flying shuttle: a tower gun's aim point goes up onto the drawn gun (the
        /// rules' offset kept, halved, at most SpreadM); anything else: `ground` unchanged.</summary>
        public static Vector3 Aim(ZObject t, Vector3 ground)
        {
            if (!On(t, out var v)) return OzUePlatformViews.Aim(t, ground);                          // 1.3 the Unit editor: a round at a unit on a ramp platform's deck never ends at the wall's foot (View/OzUePlatformView.cs); `ground` in every level
            var g = ZGame.I; var c = Middle(t, v); var sim = g.CenterWorld(t, false);
            var off = new Vector3(ground.x - sim.x, 0f, ground.z - sim.z) * SpreadK;
            if (off.sqrMagnitude > SpreadM * SpreadM) off = off.normalized * SpreadM;
            return c + off;
        }

        /// <summary>CombatView.DrawShot, a missed bullet / bolt at a tower gun: past the gun up there and on into the sky.</summary>
        public static Vector3 Miss(Vector3 from, ZObject t)
        {
            if (!On(t, out var v)) return from;
            var end = OzShuttleAim.MissPast(from, Middle(t, v), MissNear, MissFar, MissRun);
            Missed++;
            return end;
        }
        public static int Missed;

        /// <summary>OpenZPyroFire, a missed flame at a tower gun: the stream goes up past the gun's side and ends 1 m beyond it, never past the
        /// flame's reach (as OzShuttleAim.FlameMiss does at the flying ship).</summary>
        public static Vector3 FlameMiss(Vector3 from, ZObject t, float reach)
        {
            if (!On(t, out var v)) return from;
            var c = Middle(t, v);
            var d = c - from; float len = d.magnitude; if (len < 0.5f) return c;
            var n = d / len;
            var a = Vector3.Cross(n, Mathf.Abs(n.y) > 0.95f ? Vector3.right : Vector3.up).normalized; var b = Vector3.Cross(n, a);
            float ang = Random.value * Mathf.PI * 2f;
            var aim = c + (a * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * Random.Range(MissNear * 0.6f, MissNear);
            var dir = (aim - from).normalized;
            Missed++;
            return from + dir * Mathf.Min((aim - from).magnitude + 1f, Mathf.Max(reach, len));
        }

        /// <summary>OzShuttleAim.SalvoEndY: an extra rocket of a salvo whose own round ended at a drawn tower gun bursts on the fort's top under
        /// it, else in the air beside the gun (not on the ground at the tower's foot). False = not at a tower gun (the caller goes on).</summary>
        public static bool SalvoY(Vector3 to, Vector3 p, float groundY, out float y)
        {
            y = groundY;
            var g = ZGame.I; if (!g || g.World == null) return false;
            UnitView near = null; float best = SalvoNearM * SalvoNearM;
            foreach (var kv in g.Views)
            {
                var v = kv.Value; if (!v || !v.OnTower || !v.TowerFort || v.Obj == null) continue;
                float d = (Middle(v.Obj, v) - to).sqrMagnitude; if (d <= best) { best = d; near = v; }
            }
            if (!near) return false;
            if (TowerMounts.FortSurface(near.TowerFort, p, to.y + 1f, out float top) && top > groundY + 1f) y = top + 0.1f;
            else y = Mathf.Max(groundY, to.y + Random.Range(-0.6f, 0.4f));
            SalvoUp++;
            return true;
        }
        public static int SalvoUp;

        // ------------------------------------------------------------------ the Play proof's counts
        public sealed class Probe
        {
            public string key; public int shots, hits, misses, inSky, ends, endsOnGun, endsLow, blasts, blastsOnGun, blastsLow;
            public float minH = 999f, maxH = -999f, sumH, worstGun;
        }
        /// <summary>Proof switch: count every drawn round at a tower gun (test only).</summary>
        public static bool Recording;
        public static readonly Dictionary<string, Probe> Probes = new Dictionary<string, Probe>();
        public static readonly StringBuilder Lines = new StringBuilder();
        static int lineCount;
        /// <summary>A drawn end this near the gun's middle (m) is "on the gun"; this low over the ground (m) is "low" (the tower top is ~9 m up).</summary>
        public const float OnGunM = 2.5f, LowM = 4f;
        public static void ProbeReset() { Probes.Clear(); Lines.Clear(); lineCount = 0; Missed = 0; SalvoUp = 0; }
        static Probe P(string key) { if (!Probes.TryGetValue(key, out var p)) Probes[key] = p = new Probe { key = key }; return p; }
        static string KeyOf(ZObject a, bool grenade)
        {
            if (a == null) return "?";
            string k = a.IsFort ? "fort gun" : a.Kind == ZKind.Vehicle && a.Type == "apc" ? "apc crew " + a.CrewWeaponType : a.Type;
            return grenade ? "grenade " + k : k;
        }
        static float Over(Vector3 p) { var g = ZGame.I; return g ? p.y - g.GroundY(p.x, p.z) : p.y; }
        static void Line(string s) { if (lineCount++ < 3000) Lines.AppendLine(s); }

        /// <summary>OzShuttleAim.NoteShot (CombatView.DrawShot): a drawn bullet / bolt / flame at t.</summary>
        public static void NoteShot(ZEvent e, ZObject a, ZObject t, Vector3 from, Vector3 to)
        {
            if (!Recording || !On(t, out var v)) return;
            var p = P(KeyOf(a, false)); p.shots++;
            float h = Over(to), dg = (to - Middle(t, v)).magnitude; bool sky = !e.flag && OzShuttleAim.IsSkyMiss(to);
            if (e.flag) { p.hits++; End(p, h, dg); } else { p.misses++; if (sky) p.inSky++; else End(p, h, dg); }
            Line($"{Time.time:F2} {p.key}#{(a != null ? a.Id : -1)} {(e.flag ? "HIT " : "MISS")} from {Over(from):F1} m up; end {h:F1} m over the ground, {dg:F2} m from the gun{(sky ? ", in the sky" : "")}");
        }

        /// <summary></summary>
        public static void NoteBolt(ZObject a, ZObject t, Vector3 from, Vector3 end)
        {
            if (!Recording || !On(t, out var v)) return;
            var p = P(KeyOf(a, false)); p.shots++; p.hits++;
            float h = Over(end), dg = (end - Middle(t, v)).magnitude;
            End(p, h, dg);
            Line($"{Time.time:F2} {p.key}#{(a != null ? a.Id : -1)} BOLT from {Over(from):F1} m up; end {h:F1} m over the ground, {dg:F2} m from the gun");
        }

        /// <summary>OzShuttleAim.NoteFired (CombatView.MissileFired): a drawn shell / rocket / grenade leaving for t.</summary>
        public static void NoteFired(ZEvent e, ZObject a, ZObject t, Vector3 from, Vector3 to)
        {
            if (!Recording || !On(t, out var v)) return;
            var p = P(KeyOf(a, e.flag)); p.shots++;
            float h = Over(to), dg = (to - Middle(t, v)).magnitude;
            End(p, h, dg);
            Line($"{Time.time:F2} {p.key}#{(a != null ? a.Id : -1)} FIRED from {Over(from):F1} m up; aimed {h:F1} m over the ground, {dg:F2} m from the gun; flight {e.value:F2} s");
        }

        /// <summary>OzShuttleAim.NoteBlast (CombatView.MissileExploded): the drawn blast of a round the rules aimed at t.</summary>
        public static void NoteBlast(ZEvent e, ZObject a, ZObject t, Vector3 at)
        {
            if (!Recording || !On(t, out var v)) return;
            var p = P(KeyOf(a, e.flag)); p.blasts++;
            float h = Over(at), dg = (at - Middle(t, v)).magnitude;
            if (dg <= OnGunM) p.blastsOnGun++;
            if (h < LowM) p.blastsLow++;
            Line($"{Time.time:F2} {p.key}#{(a != null ? a.Id : -1)} BLAST {h:F1} m over the ground, {dg:F2} m from the gun");
        }

        static void End(Probe p, float h, float dg)
        {
            p.ends++; p.sumH += h; p.minH = Mathf.Min(p.minH, h); p.maxH = Mathf.Max(p.maxH, h); p.worstGun = Mathf.Max(p.worstGun, dg);
            if (dg <= OnGunM) p.endsOnGun++;
            if (h < LowM) p.endsLow++;
        }

        /// <summary>The proof's table: one line per weapon.</summary>
        public static string ProbeText()
        {
            var sb = new StringBuilder();
            foreach (var p in Probes.Values)
                sb.AppendLine($"  {p.key,-22} rounds {p.shots,3} (hits {p.hits}, misses {p.misses}, into the sky {p.inSky}) | drawn ends {p.ends}: on the gun {p.endsOnGun}, low {p.endsLow}, " +
                              $"over the ground {(p.ends > 0 ? p.minH : 0f):F1} .. {(p.ends > 0 ? p.maxH : 0f):F1} m (mean {(p.ends > 0 ? p.sumH / p.ends : 0f):F1}), worst {p.worstGun:F2} m from the gun | blasts {p.blasts}: on the gun {p.blastsOnGun}, low {p.blastsLow}");
            return sb.ToString();
        }
    }
}
