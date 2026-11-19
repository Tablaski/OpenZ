using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only: the rules never read it. What a rocket, missile, shell, grenade or bullet does when it lands in the water:
    /// <list type="bullet">
    /// <item>white water thrown up: a crown that opens outward and a column behind it (soft lit puffs of the water_fx channels of
    /// <see cref="OzWaterSplash"/>), broken into droplets that arc and fall back with gravity, a little mist;</item>
    /// <item>a BLAST STAMP in the water itself (Shaders/OpenZWaterBlast.hlsl, read by both water shaders): a dark smooth blast hole for
    /// half a second, crests that spread outward, move apart and fade (the water's own slope, so its own light and glints; uneven,
    /// broken, never a closed even line), an uneven foam patch that erodes into drifting flecks;</item>
    /// <item>small late stamps where the thrown water comes down again.</item>
    /// </list>
    /// It replaces the flat ring particles (the m12_ripple band: up to three closed pale rings 3 to 8 m in radius per blast) for
    /// everything that goes through OpenZFxPlayer's blast and impact code. Sized by the blast's strength s (a bullet small, a rocket
    /// medium, a missile / heavy shell / MLRS bigger). The flash, sparks, light, camera kick and every sound are the caller's, unchanged.
    /// A stamp is stateless: the water works out its age from the time it was dropped, nothing is updated per frame.
    /// <see cref="On"/> false = every caller draws its rings as before (a test aid for BEFORE pictures, never saved).
    /// </summary>
    public static class OzWaterBlast
    {
        /// <summary>Test aid, never saved: false = the flat rings as before this kit.</summary>
        public static bool On = true;

        // ---- sizes by the blast's strength s (a rocket 1.2, a shell 0.85, a heavy shell 1.65, a missile radius / 4)
        /// <summary>How far the crests run: ReachPerS x s metres, between ReachMin and ReachMax (a rocket 4.3 m; its old outer ring 7.8 m).</summary>
        public static float ReachPerS = 3.6f, ReachMin = 1.2f, ReachMax = 9f;
        /// <summary>How high the water is thrown: HeightPerS x s metres, between HeightMin and HeightMax (a rocket 3.6 m).</summary>
        public static float HeightPerS = 3.0f, HeightMin = 1.0f, HeightMax = 9f;
        /// <summary>A stamp lives LifeBase + LifePerS x s seconds (a rocket 3.4 s).</summary>
        public static float LifeBase = 2.2f, LifePerS = 1.0f;
        /// <summary>The slope of the first crest for a small and a big blast (the water's own small waves: about 0.1 to 0.2).</summary>
        public static float SlopeSmall = 0.5f, SlopeBig = 0.8f;
        /// <summary>Foam of the patch (0..1), the dark of the blast hole (0..1), how fast the foam drifts (m per s).</summary>
        public static float Foam = 0.8f, Hole = 1f, FoamDrift = 0.12f;
        /// <summary>A bullet's stamp: reach = BulletReachBase + BulletReachPerS x s (0.6 to 1.6 m), its slope, its foam.</summary>
        public static float BulletReachBase = 0.3f, BulletReachPerS = 4f, BulletSlope = 0.32f, BulletFoam = 0.45f;
        /// <summary>How many particles a blast of s 1 throws: the crown's puffs, the column's puffs, the droplets.</summary>
        public static float CrownPuffs = 9f, ColumnPuffs = 7f, Droplets = 22f;

        const int Big = 16, Small = 12, Max = Big + Small;                               // = OZ_BLAST_MAX of the shader include
        static readonly Vector4[] gA = new Vector4[Max], gB = new Vector4[Max], gC = new Vector4[Max];
        static readonly float[] dies = new float[Max];
        static readonly int IdA = Shader.PropertyToID("_OZBlastA"), IdB = Shader.PropertyToID("_OZBlastB"), IdC = Shader.PropertyToID("_OZBlastC"),
            IdInfo = Shader.PropertyToID("_OZBlastInfo"), IdTime = Shader.PropertyToID("_Time");
        static OpenZFxPlayer madeOn;
        static Vector3 lastSmallAt; static float lastSmallT = -10f, lastDrop = -10f;

        /// <summary>A piece that lands in the water (debris, a crater's clods, rubble, a wreck part, a turret, a robot falling in): reach =
        /// LandReachBase + LandReachPerS x s between 0.7 m and LandReachMax (s = the strength of the flat ring it replaces: 0.16 a small
        /// piece, 0.45 a medium one, 1.3 a turret), its slope and foam for a small and a big piece.</summary>
        public static float LandReachBase = 0.5f, LandReachPerS = 2.2f, LandReachMax = 3.4f, LandSlopeSmall = 0.3f, LandSlopeBig = 0.5f, LandFoamSmall = 0.3f, LandFoamBig = 0.6f;

        /// <summary>Checks: blasts and bullet stamps drawn, late stamps, landing stamps, stamps that took the place of a living one, bullet stamps left
        /// out (the same spot within a moment), particles emitted, how often the water's clock and the game's clock were more than a
        /// second apart (0 = the stamps age by Time.time).</summary>
        public static int Blasts, Bullets, Late, Landings, CutShort, LeftOut, Emitted, ClockApart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Clear(); madeOn = null; lastSmallT = lastDrop = -10f;
            Blasts = Bullets = Late = Landings = CutShort = LeftOut = Emitted = ClockApart = 0;
        }

        static void Clear()
        {
            for (int i = 0; i < Max; i++) { gA[i] = gB[i] = gC[i] = Vector4.zero; dies[i] = 0f; }
            Shader.SetGlobalVectorArray(IdA, gA); Shader.SetGlobalVectorArray(IdB, gB); Shader.SetGlobalVectorArray(IdC, gC);
            Shader.SetGlobalVector(IdInfo, Vector4.zero);
        }

        /// <summary>The clock the water shaders age a stamp by: _Time.y, which the render pipeline sets from Time.time. When the two are
        /// more than a second apart the water's own clock is taken (and counted).</summary>
        static float Now()
        {
            float t = Time.time, ts = Shader.GetGlobalVector(IdTime).y;
            if (ts > 0f && Mathf.Abs(ts - t) > 1f) { ClockApart++; return ts; }
            return t;
        }

        static bool Ensure(OpenZFxPlayer z)
        {
            if (!z || !z.isActiveAndEnabled) return false;
            if (madeOn == z && z.HasChannel(OzWaterSplash.Column) && z.HasChannel(OzWaterSplash.Drop) && z.HasChannel(OzWaterSplash.Mist)) return true;
            foreach (var d in OzWaterSplash.Defs()) if (!z.HasChannel(d.name) && !z.UeAddChannel(d)) return false;
            madeOn = z;
            return true;
        }

        /// <summary>Drops one stamp. small = a bullet's, a landing piece's or a late one (their own twelve places, so a machine gun never takes a blast's).
        /// A dead place first; else, only when `must`, the place that dies soonest. False = no place.</summary>
        static bool Stamp(Vector3 at, float reach, float life, float slope, float foam, float hole, float delay, bool small, bool must)
        {
            float now = Now();
            if (now < lastDrop - 1f) Clear();                                           // the clock went back (a new scene's clock): nothing old may play again
            lastDrop = now;
            int i0 = small ? Big : 0, i1 = small ? Max : Big, best = -1; float bd = float.MaxValue;
            for (int i = i0; i < i1; i++)
            {
                if (dies[i] <= now) { best = i; bd = -1f; break; }
                if (dies[i] < bd) { bd = dies[i]; best = i; }
            }
            if (bd > 0f) { if (!must) return false; CutShort++; }
            float a = Random.value * 6.2831853f, dr = FoamDrift * Random.Range(0.5f, 1f);
            gA[best] = new Vector4(at.x, at.z, now + delay, reach);
            gB[best] = new Vector4(at.y, life, Random.value, slope);
            gC[best] = new Vector4(Mathf.Clamp01(foam), Mathf.Clamp01(hole), Mathf.Cos(a) * dr, Mathf.Sin(a) * dr);
            dies[best] = now + delay + life;
            float end = 0f; int n = 0;
            for (int i = 0; i < Max; i++) if (dies[i] > now) { n = i + 1; if (dies[i] > end) end = dies[i]; }
            Shader.SetGlobalVectorArray(IdA, gA); Shader.SetGlobalVectorArray(IdB, gB); Shader.SetGlobalVectorArray(IdC, gC);
            Shader.SetGlobalVector(IdInfo, new Vector4(n, end, 0f, 0f));
            return true;
        }

        static Vector3 Ring() { float a = Random.value * 6.2831853f; return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); }

        static void E(OpenZFxPlayer z, string ch, Vector3 p, Vector3 v, float life, float size, Color c, float alpha, float delay)
        {
            if (delay > 0f) z.EmitChannel(ch, p, v, life, size, c, alpha, delay); else z.EmitChannel(ch, p, v, life, size, c, alpha);
            Emitted++;
        }

        /// <summary>A blast on the water at `flat` (the drawn surface), strength s (the blast code's own s), drawn `delay` s later.
        /// False = not drawn (<see cref="On"/> off, or no effect player): the caller draws its rings as before.</summary>
        public static bool Blast(OpenZFxPlayer z, Vector3 flat, float s, float delay = 0f)
        {
            if (!On || !Ensure(z)) return false;
            float sc = Mathf.Clamp(s, 0.25f, 3f), root = Mathf.Sqrt(sc);
            float reach = Mathf.Clamp(ReachPerS * sc, ReachMin, ReachMax), H = Mathf.Clamp(HeightPerS * sc, HeightMin, HeightMax);
            float v0 = Mathf.Sqrt(2f * 9.81f * H), air = 2f * v0 / 9.81f;               // launch speed for that height, seconds up and down again
            Stamp(flat, reach, LifeBase + LifePerS * sc, Mathf.Lerp(SlopeSmall, SlopeBig, Mathf.InverseLerp(0.5f, 2.5f, sc)), Foam, Hole, delay, false, true);

            var water = OpenZFxCatalog.RippleColorFor(ZGame.I ? ZGame.I.level : "");
            // the crown: a wall of water thrown outward and up from a ring round the point; uneven (some of the turn is left out)
            int nc = Mathf.Clamp(Mathf.RoundToInt(CrownPuffs * root), 5, 16);
            float turn = Random.value * 6.2831853f;
            for (int i = 0; i < nc; i++)
            {
                if (Random.value < 0.15f) continue;
                float a = turn + (i + Random.Range(-0.35f, 0.35f)) * 6.2831853f / nc, tilt = Random.Range(0.5f, 0.85f);
                var outw = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                E(z, OzWaterSplash.Column, flat + outw * (H * Random.Range(0.06f, 0.12f)) + Vector3.up * 0.15f, (Vector3.up * Mathf.Cos(tilt) + outw * Mathf.Sin(tilt)) * (v0 * Random.Range(0.45f, 0.7f)),
                  air * Random.Range(0.4f, 0.6f), H * Random.Range(0.14f, 0.22f), OzWaterSplash.Tint(water, OzWaterSplash.BodyWhite + Random.Range(-0.08f, 0.12f)), OzWaterSplash.BodyAlpha, delay);
            }
            // the column: round soft masses in a narrow cone a moment later, rising higher and falling back
            int nj = Mathf.Clamp(Mathf.RoundToInt(ColumnPuffs * root), 4, 14);
            for (int i = 0; i < nj; i++)
            {
                float tilt = Random.Range(0f, 0.16f); var d = Vector3.up * Mathf.Cos(tilt) + Ring() * Mathf.Sin(tilt);
                E(z, OzWaterSplash.Column, flat + Ring() * (H * Random.Range(0f, 0.06f)) + Vector3.up * 0.2f, d * (v0 * Random.Range(0.72f, 1f)),
                  air * Random.Range(0.55f, 0.8f), H * Random.Range(0.16f, 0.26f), OzWaterSplash.Tint(water, OzWaterSplash.BodyWhite + Random.Range(-0.08f, 0.12f)), OzWaterSplash.BodyAlpha,
                  delay + Random.Range(0.02f, 0.07f));
            }
            // droplets off the crown's rim and the column's top, in small clusters: they arc out and fall back
            int nd = Mathf.Clamp(Mathf.RoundToInt(Droplets * root), 10, 44);
            float dsz = Mathf.Lerp(1f, 1.5f, Mathf.InverseLerp(0.5f, 2.5f, sc));
            for (int i = 0; i < nd;)
            {
                float tilt = Random.Range(0.1f, 0.75f); var d = Vector3.up * Mathf.Cos(tilt) + Ring() * Mathf.Sin(tilt);
                var p = flat + Ring() * (H * Random.Range(0.05f, 0.15f)) + Vector3.up * (H * Random.Range(0.06f, 0.35f));
                float sp = v0 * Random.Range(0.5f, 1.05f), dl = delay + Random.Range(0f, 0.08f);
                int cl = 1 + (i % 3);
                for (int k = 0; k < cl && i < nd; k++, i++)
                    E(z, OzWaterSplash.Drop, p + Random.insideUnitSphere * (0.06f * H), d * sp + Random.insideUnitSphere * (0.07f * v0), air * Random.Range(0.55f, 0.95f),
                      Random.Range(0.11f, 0.2f) * dsz, OzWaterSplash.Tint(water, OzWaterSplash.DropWhite), OzWaterSplash.DropAlpha, dl);
            }
            // a little mist drifting off
            int nm = sc > 1f ? 4 : 2;
            for (int i = 0; i < nm; i++)
            {
                var r = Ring();
                E(z, OzWaterSplash.Mist, flat + Vector3.up * (H * Random.Range(0.3f, 0.7f)) + r * (H * Random.Range(0f, 0.12f)), r * Random.Range(0.3f, 0.9f) + Vector3.up * Random.Range(0.2f, 0.6f),
                  Random.Range(1.6f, 2.4f), H * Random.Range(0.45f, 0.7f), OzWaterSplash.Tint(water, OzWaterSplash.MistWhite), OzWaterSplash.MistAlpha, delay + Random.Range(0.1f, 0.4f));
            }
            // where the thrown water comes down again: small late stamps (only in a free place)
            int nl = 2 + (sc > 1f ? 1 : 0) + (sc > 1.6f ? 1 : 0);
            for (int i = 0; i < nl; i++)
            {
                var p = flat + Ring() * (reach * Random.Range(0.25f, 0.6f));
                if (Stamp(p, Random.Range(0.9f, 1.4f) * root, 1.1f, 0.3f, 0.3f, 0f, delay + air * Random.Range(0.55f, 1f), true, false)) Late++;
            }
            Blasts++;
            return true;
        }

        /// <summary>A bullet into the water at `at` (the drawn surface): one small stamp instead of the flat ring of strength s (the
        /// caller's droplets stay). False = not drawn: the caller draws its ring as before.</summary>
        public static bool Bullet(Vector3 at, float s)
        {
            if (!On) return false;
            float now = Time.time;
            if (now - lastSmallT < 0.12f && (at - lastSmallAt).sqrMagnitude < 0.3f * 0.3f) { LeftOut++; return true; }   // the same spot within a moment adds nothing
            lastSmallAt = at; lastSmallT = now;
            if (Stamp(at, Mathf.Clamp(BulletReachBase + BulletReachPerS * s, 0.6f, 1.6f), 0.9f + 0.6f * Mathf.Clamp01(s), BulletSlope, BulletFoam, 0f, 0f, true, true)) Bullets++;
            return true;
        }

        /// <summary>A piece landing in the water at `at` (the drawn surface): one small stamp instead of the flat ring(s) of strength s
        /// (the caller's droplets and sound stay). Several rings asked for at one spot in one moment (a landing drew up to three) = one
        /// stamp, the first. No free place = no mark (a living stamp is never cut for a piece of debris). False = switched off: the
        /// caller draws its ring as before.</summary>
        public static bool Landing(Vector3 at, float s)
        {
            if (!On) return false;
            float now = Time.time;
            if (now - lastSmallT < 0.12f && (at - lastSmallAt).sqrMagnitude < 0.3f * 0.3f) { LeftOut++; return true; }
            lastSmallAt = at; lastSmallT = now;
            float k = Mathf.InverseLerp(0.16f, 1.3f, s);
            if (Stamp(at, Mathf.Clamp(LandReachBase + LandReachPerS * s, 0.7f, LandReachMax), 1.1f + 1.2f * k, Mathf.Lerp(LandSlopeSmall, LandSlopeBig, k), Mathf.Lerp(LandFoamSmall, LandFoamBig, k), 0f, 0f, true, false)) Landings++;
            else LeftOut++;
            return true;
        }

        // ------------------------------------------------------------------------------------------------ 1.5 kit water_rings_rest_r6
        // crests that spread outward as the water's slope and fade, foam flecks that erode, a brief dark hole for a heavy one; sized by the object.
        /// <summary>1.5 kit water_rings_rest_r6: a LAP = a piece, a wreck or a container lying in the water, lapping or breaking the surface, or
        /// a bubble of a sinking hull: reach = LapReachPerM x the object's size (m) between LapReachMin and LapReachMax, faint (LapSlope,
        /// LapFoam), LapLife s. It takes a free small place only while more than LapKeepFree of the twelve stay free and fewer than
        /// LapMaxAlive laps live (landing pieces and bullets come first; a lap left out = no mark, counted in RestLeftOut).</summary>
        public static float LapReachPerM = 0.5f, LapReachMin = 0.6f, LapReachMax = 1.8f, LapLife = 1.6f, LapSlope = 0.22f, LapFoam = 0.25f;
        public static int LapKeepFree = 4, LapMaxAlive = 3;
        /// <summary>1.5 kit water_rings_rest_r6: a HEAVY one = a harbour wreck or an L18 container broken and sinking, a Bizon plunging in:
        /// reach = HeavyReachBase + HeavyReachPerS x s (s = the old splash's strength) between 1.2 m and HeavyReachMax, its slope and foam
        /// growing with s, a brief dark hole (HeavyHole x how heavy). A free blast place first, else a free small one; a living stamp is never cut.</summary>
        public static float HeavyReachBase = 0.8f, HeavyReachPerS = 2.2f, HeavyReachMax = 5.5f, HeavySlopeSmall = 0.35f, HeavySlopeBig = 0.5f,
            HeavyFoamSmall = 0.45f, HeavyFoamBig = 0.7f, HeavyHole = 0.6f;
        /// <summary>Checks of the rest stamps: laps drawn, heavy ones drawn, left out (no free place, or the lap budget full).</summary>
        public static int RestLaps, RestHeavy, RestLeftOut;
        static readonly float[] lapEnds = new float[8];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRest() { RestLaps = RestHeavy = RestLeftOut = 0; for (int i = 0; i < lapEnds.Length; i++) lapEnds[i] = 0f; }

        /// <summary>The drawn water height under a point, taken as OpenZFxPlayer.Splash / Ripple take it (so the SplashStamped right after a
        /// heavy stamp finds the same spot and adds no second stamp).</summary>
        static Vector3 OnWater(Vector3 at)
        {
            var z = OpenZFxPlayer.I;
            if (z && OpenZFxPlayer.IsWaterTile(at) && !float.IsNegativeInfinity(z.WaterY)) at.y = z.WaterYAt(at);
            return at;
        }

        /// <summary>A lap (see LapReachPerM) at `at` of an object `sizeM` metres across. False = switched off (<see cref="On"/>): the caller
        /// draws its ring as before.</summary>
        public static bool Lap(Vector3 at, float sizeM)
        {
            if (!On) return false;
            float now = Now();
            int free = 0; for (int i = Big; i < Max; i++) if (dies[i] <= now) free++;
            int slot = -1;
            for (int i = 0; i < Mathf.Min(LapMaxAlive, lapEnds.Length); i++)
            {
                if (lapEnds[i] > now + 30f) lapEnds[i] = 0f;                                    // the clock went back (a new scene)
                if (lapEnds[i] <= now) { slot = i; break; }
            }
            if (slot < 0 || free <= LapKeepFree || !Stamp(OnWater(at), Mathf.Clamp(LapReachPerM * sizeM, LapReachMin, LapReachMax), LapLife, LapSlope, LapFoam, 0f, 0f, true, false))
            { RestLeftOut++; return true; }
            lapEnds[slot] = now + LapLife; RestLaps++;
            return true;
        }

        /// <summary>A heavy one (see HeavyReachBase) at `at`, `s` = the strength of the old splash it replaces; hole -1 = by how heavy, 0 = none.
        /// It marks the spot, so the SplashStamped right after it (its droplets) adds no second stamp. False = switched off: the caller draws
        /// its splash and rings as before.</summary>
        public static bool Heavy(Vector3 at, float s, float hole = -1f)
        {
            if (!On) return false;
            var w = OnWater(at);
            float k = Mathf.InverseLerp(0.5f, 2f, s), reach = Mathf.Clamp(HeavyReachBase + HeavyReachPerS * s, 1.2f, HeavyReachMax), life = 1.8f + 1.4f * k;
            float slope = Mathf.Lerp(HeavySlopeSmall, HeavySlopeBig, k), foam = Mathf.Lerp(HeavyFoamSmall, HeavyFoamBig, k), h = hole < 0f ? HeavyHole * k : hole;
            if (Stamp(w, reach, life, slope, foam, h, 0f, false, false) || Stamp(w, reach, life, slope, foam, h, 0f, true, false)) RestHeavy++; else RestLeftOut++;
            lastSmallAt = w; lastSmallT = Time.time;
            return true;
        }

        public static string RestStatus()
        {
            float now = Now(); int laps = 0; for (int i = 0; i < lapEnds.Length; i++) if (lapEnds[i] > now) laps++;
            return $"[OzWaterBlast rest] {(On ? "ON" : "OFF (the flat rings as before)")}: laps {RestLaps} (living {laps} of {LapMaxAlive}), hits / sinks / plunges {RestHeavy}, left out {RestLeftOut}";
        }

        public static string Status()
        {
            int live = 0; float now = Now(); for (int i = 0; i < Max; i++) if (dies[i] > now) live++;
            return $"[OzWaterBlast] {(On ? "ON" : "OFF (the flat rings as before)")}: blasts {Blasts}, bullet stamps {Bullets} (left out {LeftOut}), late stamps {Late}, landing stamps {Landings}, cut short {CutShort}, " +
                   $"living now {live} of {Max}, particles emitted {Emitted}, clocks apart {ClockApart} (Time.time {Time.time:F2}, _Time.y {Shader.GetGlobalVector(IdTime).y:F2})";
        }
    }
}
