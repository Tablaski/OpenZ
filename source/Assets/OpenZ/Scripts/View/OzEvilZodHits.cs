using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Every pellet of his that lands on a vehicle, a gun or a fort's tower gun (the Metal pellets of OzEvilZodFx.Spread) now throws far more metal. On top of
    /// the hit as it was (3 fast sparks, the hit-spark burst on the first OzEvilZodFx.MaxImpacts pellets, 4 small sparks on the rest):
    /// <list type="bullet">
    /// <item>bigger warm sparks that arc and drop (m12_spark, the wreck fountain's channel) and white-hot fast ones (wv2_spark), sprayed off the plate about a
    /// normal that faces him, as the hit-spark bursts spray;</item>
    /// <item>a short glint on the plate (wv2_glow, 0.06 s) on every other pellet and on the real impacts, at most <see cref="GlintsPerShot"/> a shot;</item>
    /// <item>ricochet streaks: every <see cref="StreakEvery"/>th pellet a short hot streak flies off the plate (the pellets' own tracer channel), at most
    /// <see cref="StreaksPerShot"/> a shot; the first of a shot is heard (the hit-spark ricochet whines);</item>
    /// <item>a spark shower on the closest hits: the first pellet of a shot that lands within <see cref="ShowerBandM"/> of him (his near band ZEvilZod.FullBandPx,
    /// where his shot is at its full number) adds the big hit-spark burst (sniper profile: its flash, light and whine) and a shower of falling sparks that
    /// bounce on the ground (leg_spark, as the burning wreck's fountain).</item>
    /// </list>
    /// A shot is the pellets that land less than <see cref="ShotGap"/> s apart (his shots are 3 s apart); the extra sparks of one shot are capped at
    /// <see cref="ShotSparks"/>. Off the picture nothing extra is made. Switch <see cref="MoreMetalSparks"/>: off = the metal hits as before.
    /// A pellet that lands on a robot is told to <see cref="OzEvilZodThrow"/>: a body his close shot killed is thrown on the frame the first pellet reaches it.
    /// A file of its own (not a partial of OzEvilZodFx): OzEvilZodFx keeps one added line, so the other EvilZod kits of 3 Nov can change it freely.
    /// </summary>
    public static class OzEvilZodHits
    {
        public static bool MoreMetalSparks = true;
        /// <summary>Per metal pellet: warm and white-hot sparks. Per shot: the cap of the extra sparks, the glints, a streak every StreakEvery pellets up to StreaksPerShot, the shower's sparks.</summary>
        public static int WarmSparks = 4, HotSparks = 2, ShotSparks = 150, GlintsPerShot = 6, StreakEvery = 3, StreaksPerShot = 4, ShowerSparks = 18;
        /// <summary>The closest hits (m, his feet to the hit point, flat): the rules' near band ZEvilZod.FullBandPx (64 px = 8 m). A view number only.</summary>
        public static float ShowerBandM = ZEvilZod.FullBandPx / 8f;
        /// <summary>Sizes (m), speeds (m/s), lives (s); ShowerBurst: the scale of the shower's hit-spark burst; ShotGap: seconds between pellets that start a new shot.</summary>
        public static float WarmSize = 0.1f, WarmMin = 4.5f, WarmMax = 10f, HotMin = 8f, HotMax = 15f, GlintSize = 0.6f, GlintLife = 0.06f, StreakMps = 38f, StreakMetres = 5f, StreakSize = 0.09f, ShowerBurst = 1.5f, ShotGap = 0.5f;
        static readonly Color SparkWarm = new Color(1f, 0.6f, 0.22f), SparkPale = new Color(1f, 0.93f, 0.62f), SparkWhite = new Color(1f, 0.95f, 0.85f), GlintColor = new Color(1f, 0.88f, 0.66f);
        /// <summary>Counters for the Play check: metal pellets, extra sparks, glints, streaks, showers, pellets on a robot.</summary>
        public static int MetalHits, MetalExtra, MetalGlints, MetalStreaks, MetalShowers, BodyHits;

        /// <summary>One shooter's shot: the last metal landing, the sparks left, the pellets, glints and streaks so far, the shower given.</summary>
        sealed class Shot { public UnitView view; public float last = -10f; public int left, n, glints, streaks; public bool shower; }
        static readonly Dictionary<int, Shot> shots = new Dictionary<int, Shot>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { shots.Clear(); MetalHits = MetalExtra = MetalGlints = MetalStreaks = MetalShowers = BodyHits = 0; }

        static Shot Of(OzEvilZodFx fx)
        {
            int key = fx.GetInstanceID();
            if (shots.TryGetValue(key, out var s) && s.view) return s;
            if (shots.Count > 32) shots.Clear();                                                           // views of EvilZods long gone
            s = new Shot { view = fx.GetComponent<UnitView>() }; shots[key] = s;
            return s;
        }

        /// <summary>OzEvilZodFx.Land, its first line: one landing pellet (its Due's fields). The spent shell (not hard) and the ground stay as they were.</summary>
        public static void Land(OzEvilZodFx fx, Vector3 pos, Vector3 dir, FxSurface surf, bool spark, bool hard)
        {
            if (!fx || !hard) return;
            if (surf == FxSurface.Unit)
            {
                BodyHits++;
                var v = Of(fx).view;
                if (v && v.Obj != null) OzEvilZodThrow.PelletOnBody(v.Obj.Id, pos);
                return;
            }
            if (surf != FxSurface.Metal || !MoreMetalSparks) return;
            var s = Of(fx); float now = Time.time;
            if (now - s.last > ShotGap) { s.left = ShotSparks; s.n = 0; s.glints = 0; s.streaks = 0; s.shower = false; }
            s.last = now; s.n++; MetalHits++;
            var z = OpenZFxPlayer.I; if (!z || !OzShakeGate.OnScreen(pos)) return;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.down;
            var h = new Vector3(-dir.x, 0f, -dir.z); if (h.sqrMagnitude < 1e-4f) h = Vector3.forward; h.Normalize();
            var n = (h * 0.85f + Vector3.up * 0.45f).normalized;                                         // the plate faces him, a little up for the RTS camera
            var refl = dir - 2f * Vector3.Dot(dir, n) * n;
            var spray = (refl * 0.7f + n * 0.3f + Vector3.up * 0.15f).normalized;
            var at = pos - dir * 0.06f;
            int warm = Mathf.Min(WarmSparks, s.left); s.left -= warm;
            for (int i = 0; i < warm; i++)
                z.EmitChannel("m12_spark", at, Jitter(spray, 0.55f) * Random.Range(WarmMin, WarmMax), Random.Range(0.3f, 0.6f), WarmSize * Random.Range(0.85f, 1.2f), i % 2 == 0 ? SparkPale : SparkWarm, 1f);
            int hot = Mathf.Min(HotSparks, s.left); s.left -= hot;
            for (int i = 0; i < hot; i++)
                z.EmitChannel("wv2_spark", at, Jitter(spray, 0.4f) * Random.Range(HotMin, HotMax), Random.Range(0.12f, 0.25f), 0.06f, SparkWhite, 1f);
            MetalExtra += warm + hot;
            if ((s.n % 2 == 1 || !spark) && s.glints < GlintsPerShot)
            {
                s.glints++; MetalGlints++;
                z.EmitChannel("wv2_glow", at + n * 0.08f, Vector3.zero, GlintLife, GlintSize * Random.Range(0.8f, 1.1f), GlintColor, 0.85f);
            }
            if (StreakEvery > 0 && s.n % StreakEvery == 1 % StreakEvery && s.streaks < StreaksPerShot)
            {   // a ricochet: off the plate, outward and a little up, never into the ground
                var r = Jitter(refl * 0.8f + n * 0.2f, 0.35f); r.y = Mathf.Abs(r.y) * 0.6f + 0.08f; r.Normalize();
                z.EmitChannel("wv2_mg_tracer", at + r * 0.2f, r * StreakMps, StreakMetres / Mathf.Max(1f, StreakMps), StreakSize, OzEvilZodFx.PelletHot, 0.9f);
                if (s.streaks == 0) CombatAudio.I?.PlayPitched(OpenZHitSparks.SoundKey, at, 0.45f, 0f, -1, Random.Range(0.92f, 1.12f), OpenZHitSparks.Whine);
                s.streaks++; MetalStreaks++;
            }
            if (!s.shower && Flat(pos - fx.transform.position) <= ShowerBandM)
            {   // the closest hits: a big burst with its flash, light and whine, and a shower of falling sparks
                s.shower = true; MetalShowers++;
                OpenZHitSparks.Burst(pos, dir, FxWeapon.Sniper, FxSurface.Metal, ShowerBurst, true);
                int shower = Mathf.Min(ShowerSparks, s.left); s.left -= shower; MetalExtra += shower;          // inside the shot's cap (ShotSparks) as well
                for (int i = 0; i < shower; i++)
                {
                    var v = spray * Random.Range(2.5f, 6f) + Vector3.up * Random.Range(1.5f, 4f) + Random.insideUnitSphere * 1.2f;
                    z.EmitChannel("leg_spark", at + Vector3.up * 0.15f, v, Random.Range(0.9f, 1.4f), Random.Range(0.12f, 0.18f), i % 3 == 0 ? SparkPale : SparkWarm, 1f);
                }
            }
        }

        /// <summary>A unit direction near `axis`; never steeply into the ground.</summary>
        static Vector3 Jitter(Vector3 axis, float jitter)
        {
            var v = axis + Random.insideUnitSphere * jitter;
            if (v.y < -0.25f) v.y = -v.y * 0.5f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.up;
        }

        static float Flat(Vector3 v) => Mathf.Sqrt(v.x * v.x + v.z * v.z);
    }
}
