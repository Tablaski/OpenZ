// OpenZ (GPL-3.0)
// The rule is DV192 (Sim/ZBigTanks.Ground.cs); this is its picture. View only: reads
// the rules and the drawn water / lava, writes nothing to the rules.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The Bizon in water and lava (DV192), view only.
    /// <para>HEIGHT and TILT: UnitView sits a vehicle on five ground samples (the middle, 1.5 m ahead / behind, 1.1 m either side) and its planted
    /// tracks (UnitView.GroundGear) on the ground under each belt. For a Bizon over open water (OzWaterLanding.OpenWaterAt: under a drawn water
    /// surface, the bed at least 8 cm down) every such sample is the WADE surface: the bed, but never higher than <see cref="SinkM"/> under the
    /// water line (so it drives down the bank on the bed and, where the river is deeper, rides with the water at about half its hull height; the
    /// two meet without a step: no pop). Over lava (OzLava.SurfaceAt) the same with <see cref="LavaSinkM"/>, eased in and out over
    /// <see cref="LavaEaseS"/> s as its middle enters / leaves the lava. The samples stay within <see cref="StepM"/> of the middle one, so UnitView's
    /// bank-step rule keeps them and the hull pitches with the bank under its front and rear (UnitView's own eased tilt, 8 / s), its tracks with it.</para>
    /// <para>WATER: entering, a big splash with its rings and sound (OzWaterLanding.Splash, Large); moving, a bow splash every 0.35-0.6 s, round
    /// ripple rings round the hull (OpenZFxPlayer.Ripple: a flat round ring, never a box) and a heavy slosh sound every 1.1-1.7 s (varied pitch);
    /// standing, a slow ring; moving, a soft track churn under it all (veh_wheels_water, a loop). LAVA: entering, a glowing splash, a spray, embers,
    /// the steam hiss and the sizzle; moving, bow sprays, embers, steam, rings and the crust opening round the hull (OzLavaWade's wake); standing, steam
    /// and a slow ring. Effects and sounds only on screen.</para>
    /// <para>LAVA BURN (N12b; the rule burns its HP, ZObject.BizonLavaTicks / BizonLavaTaken): the robots' lava look on the hull. The lower hull and the
    /// tracks glow from the lava line up (TeamLit's legs-in-lava ramp: _OZLegHeat / _OZLegLine / _OZLegTop, as OzLavaWade and OzFireHeat write it),
    /// up over <see cref="GlowUpS"/> s, its band climbing as it stays; the paint chars with the HP lava has taken in all (TeamPaint.Charred, up to
    /// <see cref="CharMax"/>: it stays after it leaves; OpenZDamageStates keeps it as the floor of its own battered paint); smoke columns
    /// (LavaLegSmoke) and embers off the hull, small flames at the track guards once the burn is high (a third of its life in this dip, or under
    /// half its health); a burn crackle loop (dst_fire_loop, low, under the engine) with sizzle ticks (lava_burn_tick, varied) and lava_critical once a
    /// dip. Out of it: lava_steam_out, the glow fades over <see cref="GlowDownS"/> s on the same part of the hull, smoke trails for
    /// <see cref="TrailS"/> s, the char stays. Its heat damage never flashes it as a hit (HeatOnly). A death in it is the usual vehicle death; its
    /// wreck goes under in the lava by the existing melt (OzWaterLanding -> OzLavaMelt, kit LV5). Death: the glow off before any copy of its blocks.</para>
    /// Switch <see cref="On"/> (off = drawn as before: on the bed). Every other unit: untouched (the first test is the type).
    /// </summary>
    public static class OzBizonWade
    {
        public static bool On = true;
        /// <summary>How far under the water line the Bizon's tracks ride where the water is deep (m: about half its hull), the same in lava, the
        /// lava's ease (s), the most a side sample may lie off the middle one (m: under UnitView's 0.7 m bank-step rule).</summary>
        public static float SinkM = 0.95f, LavaSinkM = 0.7f, LavaEaseS = 0.8f, StepM = 0.66f;
        /// <summary>The bow's distance from the middle (m), the hull's ring radius (m).</summary>
        public static float BowM = 3.2f, RingM = 3.4f;
        /// <summary>N12b, the burn: the glow up / down (s), smoke after it left (s), the glow at its peak (_OZLegHeat), the band (m from the lava line:
        /// the glow to ~0.85 of it, soot to ~1.7) cold and after BandFullS s in one dip, the char after all its HP went to lava (TeamPaint.Charred).</summary>
        public static float GlowUpS = 2f, GlowDownS = 3f, TrailS = 4.5f, PeakGlow = 0.95f, BandM = 0.5f, BandHotM = 0.85f, BandFullS = 12f, CharMax = 0.5f;
        /// <summary>The burn is high (flames at the track guards, lava_critical) from this share of its life in one dip, or under this health.</summary>
        public static float HighDipK = 0.33f, HighHealthK = 0.5f;

        /// <summary>Counted proof: frames a Bizon waded, entries (water / lava), splashes, rings, sounds; the burn: frames it glowed, smoke puffs, ember
        /// bursts, flames, burn sounds, crackle loops started, hit flashes held back, char steps, renderers bound.</summary>
        public static int WadeFrames, WaterEntries, LavaEntries, Splashes, Rings, Sounds, ChurnLoops;
        public static int BurnFrames, BurnSmokes, BurnEmbers, BurnFlames, BurnSounds, BurnLoops, FlashesHeld, CharSteps, BurnBound;
        public static float MaxGlow, MaxChar;
        public static string Last = "-";

        sealed class St
        {
            public float lava, bowT, ringT, soundT, steamT, stampT; public int kind = -1; public Vector3 last; public bool init;
            public float burn, lineOff = 0.45f, top, smokeT, emberT, flameT, tickT, trail; public bool hotWas, critical, loop, churn, bound, wrote;
            public UnitView v; public int n; public readonly Renderer[] rs = new Renderer[MaxParts]; public readonly int[] slots = new int[MaxParts], has = new int[MaxParts];
        }
        const int MaxParts = 40;
        static readonly Dictionary<int, St> states = new Dictionary<int, St>();
        static readonly Dictionary<int, int> seenTaken = new Dictionary<int, int>();
        static readonly List<Renderer> parts = new List<Renderer>(64);
        static readonly List<Material> mats = new List<Material>(8);
        static MaterialPropertyBlock block;
        static readonly int LegHeatId = Shader.PropertyToID("_OZLegHeat"), LegLineId = Shader.PropertyToID("_OZLegLine"), LegTopId = Shader.PropertyToID("_OZLegTop");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            states.Clear(); seenTaken.Clear(); block = null;
            WadeFrames = WaterEntries = LavaEntries = Splashes = Rings = Sounds = ChurnLoops = 0; Last = "-";
            BurnFrames = BurnSmokes = BurnEmbers = BurnFlames = BurnSounds = BurnLoops = FlashesHeld = CharSteps = BurnBound = 0; MaxGlow = MaxChar = 0f;
        }

        static bool Mine(UnitView v) => On && v && v.Obj != null && v.Obj.Type == ZBigTanks.Bizon && ZWorld.BizonAmphibDeviation && !v.Obj.Destroyed && !v.Dying;

        static St Of(UnitView v)
        {
            int id = v.GetInstanceID();
            if (!states.TryGetValue(id, out var s))
            {
                if (states.Count > 64) Sweep(true);
                else if (states.Count > 8) Sweep(false);
                s = new St { v = v }; states[id] = s;
            }
            return s;
        }

        /// <summary>0 dry, 1 open water, 2 lava at `at` (bed = the walk surface there); s = the water / lava surface.</summary>
        static int Medium(Vector3 at, float bed, out float s)
        {
            if (OzLava.SurfaceAt(at, out s)) return 2;
            if (OzWaterLanding.OpenWaterAt(at, bed, out s)) return 1;
            return 0;
        }

        static float Wade(Vector3 at, float bed, float lavaShare)
        {
            int k = Medium(at, bed, out float s);
            if (k == 1) return Mathf.Max(bed, s - SinkM);
            if (k == 2) return Mathf.Max(bed, s - LavaSinkM * lavaShare);
            return bed;
        }

        /// <summary>UnitView.GroundGear's ground read for this unit: the wade surface for a wading Bizon, else the ground (ZGame.GroundY).</summary>
        public static float Ground(UnitView v, float x, float z)
        {
            var g = ZGame.I;
            float bed = g ? g.GroundY(x, z) : 0f;
            if (!Mine(v)) return bed;
            var st = Of(v);
            return Wade(new Vector3(x, bed, z), bed, Mathf.SmoothStep(0f, 1f, st.lava));
        }

        /// <summary>UnitView's vehicle branch, in place of OzVehicleWaterDip.Sides: true = a Bizon (its samples are the wade surface; the shore
        /// dip of the other vehicles is not for it), false = any other unit (Sides runs as before).</summary>
        public static bool Samples(UnitView v, Vector3 p, Vector3 fwd, Vector3 right, ref float hc, ref float hf, ref float hb, ref float hr, ref float hl, float dt)
        {
            if (!Mine(v)) return false;
            var st = Of(v);
            int kc = Medium(new Vector3(p.x, hc, p.z), hc, out float sc);
            st.lava = Mathf.MoveTowards(st.lava, kc == 2 ? 1f : 0f, Mathf.Max(0f, dt) / LavaEaseS);
            float ls = Mathf.SmoothStep(0f, 1f, st.lava);
            float c = Wade(new Vector3(p.x, hc, p.z), hc, ls);
            hf = Mathf.Clamp(Wade(p + fwd * 1.5f, hf, ls), c - StepM, c + StepM);
            hb = Mathf.Clamp(Wade(p - fwd * 1.5f, hb, ls), c - StepM, c + StepM);
            hr = Mathf.Clamp(Wade(p + right * 1.1f, hr, ls), c - StepM, c + StepM);
            hl = Mathf.Clamp(Wade(p - right * 1.1f, hl, ls), c - StepM, c + StepM);
            hc = c;
            if (kc != 0) WadeFrames++;
            Fx(v, st, p, fwd, kc, sc, dt);
            Burn(v, st, p, fwd, right, kc, sc, c, dt);
            return true;
        }

        static void Fx(UnitView v, St st, Vector3 p, Vector3 fwd, int kc, float sc, float dt)
        {
            float moved = st.init ? new Vector2(p.x - st.last.x, p.z - st.last.z).magnitude : 0f;
            bool moving = st.init && dt > 0f && moved / dt > 0.25f;
            int was = st.kind; st.kind = kc; st.last = p; st.init = true;
            OzWadeWake.Feed(v, p, fwd, kc, sc, dt, WreckFx.OnScreen(p, 0.1f));   // 1.5 kit big_tank_wake_r3 (A2): the water round the hull (View/OzWadeWake.cs)
            if (dt <= 0f) return;                                                                           // paused: nothing new
            var fxp = OpenZFxPlayer.I; bool fx = fxp && fxp.isActiveAndEnabled;
            bool seen = WreckFx.OnScreen(p, 0.1f);
            var bow = new Vector3(p.x + fwd.x * BowM, sc, p.z + fwd.z * BowM);
            var mid = new Vector3(p.x, sc, p.z);
            int id = v.Obj.Id;
            if (kc != 0 && was == 0)
            {   // entering
                if (kc == 1) { WaterEntries++; if (seen) { if (OzWadeWake.HoldsRings()) { OzWadeWake.Entry(bow, fwd, id); OzWaterBlast.Heavy(bow, 1.2f, 0f); }   // 1.5 kit big_tank_wake_r3 (A2): the plunge sound + a soft spray, no rings; 1.5 kit water_rings_rest_r6: + the plunge in the water itself (3.4 m of broken crests and flecks, no hole)
                    else { OzWaterLanding.Splash(bow, OzWaterSize.Large, "bizon", 3.5f); if (fx) fxp.Splash(bow, 1.6f); } Splashes++; Sounds++; } }
                else
                {
                    LavaEntries++;
                    if (seen && fx) { fxp.LavaHit(bow, 1.3f, true); fxp.LavaSpray(bow, 1.1f, true); fxp.LavaEmber(bow, 10); fxp.LavaSteam(mid, 1.2f); Splashes++; }
                    if (seen) { AudioDirector.Fire("dtl_steam_hiss", bow, id, 1.6f, Random.Range(0.8f, 0.95f)); AudioDirector.Fire("dst_debris_splash", bow, id, 0.8f, Random.Range(0.55f, 0.7f)); Sounds += 2; }
                }
                Last = $"bizon #{id} {(kc == 1 ? "into the water" : "into the lava")} at {p.x:F1}, {p.z:F1}";
            }
            else if (kc == 0 && was > 0 && seen)
            {   // out on the bank: drips and a last slosh
                var rear = new Vector3(p.x - fwd.x * BowM, p.y, p.z - fwd.z * BowM);
                if (was == 1) { if (fx) { if (OzWadeWake.On) OzWadeWake.Exit(rear); else fxp.Splash(rear, 0.7f); } AudioDirector.Fire("rob_water_enter", rear, id, 1.3f, Random.Range(0.7f, 0.85f)); }
                else { if (fx) { fxp.LavaSmoke(rear, 0.9f); fxp.LavaEmber(rear, 4); } AudioDirector.Fire("dtl_steam_hiss", rear, id, 1.2f, Random.Range(0.9f, 1.05f)); }
                Sounds++;
            }
            bool churn = kc == 1 && moving && seen && AudioDirector.I;                                       // N12b: a soft track churn while it drives in water, between the sloshes
            if (churn && !st.churn) { AudioDirector.I.StartLoop("bizonwade:" + id, "veh_wheels_water", mid, v.transform, 0.85f, Random.Range(0.66f, 0.76f)); st.churn = true; ChurnLoops++; }
            else if (!churn && st.churn) { if (AudioDirector.I) AudioDirector.I.StopLoop("bizonwade:" + id, 0.5f); st.churn = false; }
            if (kc == 0) return;
            st.bowT -= dt; st.ringT -= dt; st.soundT -= dt; st.steamT -= dt; st.stampT -= dt;
            if (kc == 2 && st.stampT <= 0f) { st.stampT = 0.1f; var w = OzLavaWade.I ? OzLavaWade.I.Wake : null; w?.Stamp(mid, RingM * 0.8f, moving ? 0.9f : 0.5f); }   // the crust opens round the hull
            if (!seen || !fx) return;
            if (st.ringT <= 0f)
            {   // round rings round the hull (never a box), more often while it moves
                st.ringT = moving ? Random.Range(0.28f, 0.42f) : Random.Range(1.1f, 1.5f);
                if (kc == 1) { if (!OzWadeWake.HoldsRings()) { fxp.Ripple(mid, moving ? Random.Range(1.5f, 1.9f) : 1.3f, moving ? 1.6f : 2.2f); if (moving) fxp.Ripple(bow, Random.Range(0.6f, 0.85f), 1.0f); } }   // 1.5 kit big_tank_wake_r3 (A2): no 6-9 m rings
                else fxp.LavaRing(mid, RingM * (moving ? 1f : 0.85f), moving ? 1.3f : 1.8f);
                Rings++;
            }
            if (moving && st.bowT <= 0f)
            {   // the bow pushes the water / the lava up
                st.bowT = Random.Range(0.35f, 0.6f);
                var side = Vector3.Cross(Vector3.up, fwd) * Random.Range(-0.9f, 0.9f);
                if (kc == 1) { if (!OzWadeWake.On) fxp.Splash(bow + side, Random.Range(0.6f, 0.95f)); }   // 1.5 kit big_tank_wake_r3 (A2): its track spray has no ring
                else { fxp.LavaSpray(bow + side, Random.Range(0.5f, 0.75f), false); fxp.LavaEmber(bow + side, Random.Range(2, 5)); }
                Splashes++;
            }
            if (kc == 2 && st.steamT <= 0f) { st.steamT = moving ? Random.Range(0.45f, 0.7f) : Random.Range(0.8f, 1.2f); fxp.LavaSteam(mid - fwd * 1.2f, moving ? 0.9f : 0.7f); }
            if (st.soundT <= 0f)
            {   // heavy slosh / lava hiss, varied
                st.soundT = moving ? Random.Range(1.1f, 1.7f) : Random.Range(3.5f, 5f);
                if (kc == 1) AudioDirector.Fire("rob_water_enter", bow, id, moving ? 1.6f : 1.0f, Random.Range(0.62f, 0.8f));
                else AudioDirector.Fire("dtl_steam_hiss", mid, id, moving ? 1.3f : 0.9f, Random.Range(0.85f, 1.1f));
                Sounds++;
            }
        }

        // ------------------------------------------------------------------ N12b: the lava burn
        static void Burn(UnitView v, St st, Vector3 p, Vector3 fwd, Vector3 right, int kc, float sc, float bottom, float dt)
        {
            var o = v.Obj; int id = o.Id;
            bool hot = o.BizonLavaTicks > 0;                                                               // the rule burns it now (its centre on a lava tile)
            float dipK = Mathf.Clamp01(o.BizonLavaTicks / (float)ZWorld.BizonLavaDieTicks);               // the share of its life this dip has burnt
            bool high = hot && (dipK >= HighDipK || o.Health < o.MaxHealth * HighHealthK);
            CharUp(v, o);
            if (dt > 0f) st.burn = Mathf.MoveTowards(st.burn, hot ? 1f : 0f, dt / (hot ? GlowUpS : GlowDownS));
            if (hot && kc == 2) st.lineOff = Mathf.Clamp(sc - 0.22f - bottom, 0f, 1.2f);                  // the lava line on the hull, a little under the drawn sheet; it stays there as it drives out
            if (hot) st.top = Mathf.Lerp(BandM, BandHotM, Mathf.Clamp01(o.BizonLavaTicks * (float)ZConst.TICK / BandFullS));
            float g = PeakGlow * Mathf.SmoothStep(0f, 1f, st.burn);
            if (g > 0.002f) { Write(st, g, bottom + st.lineOff, Mathf.Max(0.2f, st.top)); BurnFrames++; if (g > MaxGlow) MaxGlow = g; }
            else if (st.wrote) Cold(st);
            var fxp = OpenZFxPlayer.I; bool fx = fxp && fxp.isActiveAndEnabled;
            bool seen = WreckFx.OnScreen(p, 0.1f);
            var mid = new Vector3(p.x, kc == 2 ? sc : bottom + st.lineOff, p.z);
            if (hot != st.hotWas)
            {
                if (hot) { st.critical = false; if (seen) { AudioDirector.Fire(OzLava.SndEnter, mid, id, 1.2f, Random.Range(0.7f, 0.85f)); BurnSounds++; } }
                else { st.trail = TrailS * Mathf.Max(0.4f, st.burn); if (seen) { AudioDirector.Fire("lava_steam_out", mid, id, 1.3f, Random.Range(0.75f, 0.9f)); BurnSounds++; } }
                st.hotWas = hot;
                Last = $"bizon #{id} {(hot ? "burning in the lava" : "out of the lava")} at {p.x:F1}, {p.z:F1}, health {o.Health}/{o.MaxHealth}, lava took {o.BizonLavaTaken} in all";
            }
            bool loop = hot && seen && AudioDirector.I;                                                   // the crackle, low under the engine, on screen only
            if (loop && !st.loop) { AudioDirector.I.StartLoop("bizonburn:" + id, "dst_fire_loop", mid, v.transform, 0.32f + 0.28f * dipK, Random.Range(0.82f, 0.97f)); st.loop = true; BurnLoops++; }
            else if (loop) AudioDirector.I.SetLoop("bizonburn:" + id, 0.32f + 0.28f * dipK, 0.82f + 0.12f * dipK);
            else if (st.loop) { if (AudioDirector.I) AudioDirector.I.StopLoop("bizonburn:" + id, 0.6f); st.loop = false; }
            if (dt <= 0f) return;                                                                         // paused: nothing new
            st.smokeT -= dt; st.emberT -= dt; st.flameT -= dt; st.tickT -= dt; if (!hot) st.trail -= dt;
            if (!seen) return;
            float topY = bottom + 1.25f;
            if ((hot || st.trail > 0f) && st.smokeT <= 0f && fx)
            {   // smoke columns off the hull, thicker as it burns; after it left a thinning trail
                float k = hot ? 0.55f + 0.6f * dipK : 0.35f + 0.45f * Mathf.Clamp01(st.trail / TrailS);
                st.smokeT = hot ? Random.Range(0.2f, 0.34f) : Random.Range(0.3f, 0.5f);
                var at = new Vector3(p.x, topY, p.z) + fwd * Random.Range(-1.6f, 0.6f) + right * Random.Range(-0.8f, 0.8f);
                fxp.LavaLegSmoke(at, k); BurnSmokes++;
                if (hot && dipK > 0.2f && Random.value < 0.5f) { fxp.LavaLegSmoke(at + Vector3.up * 0.5f, k * 0.8f); BurnSmokes++; }
            }
            if (hot && st.emberT <= 0f && fx)
            {   // embers off the hot steel at the lava line, both sides
                st.emberT = Random.Range(0.25f, 0.5f);
                float side = Random.value < 0.5f ? -1f : 1f;
                fxp.LavaEmber(mid + right * (1.35f * side) + fwd * Random.Range(-1.8f, 1.8f), 2 + Mathf.RoundToInt(4f * dipK)); BurnEmbers++;
            }
            if (high && st.flameT <= 0f && FxManager.I)
            {   // small flames licking at the track guards
                st.flameT = Random.Range(0.18f, 0.32f);
                float side = Random.value < 0.5f ? -1f : 1f;
                FxManager.I.Burn(mid + Vector3.up * 0.18f + right * (1.4f * side) + fwd * Random.Range(-1.7f, 1.7f), Random.Range(0.16f, 0.26f)); BurnFlames++;
            }
            if (hot && st.tickT <= 0f)
            {   // the sizzle, varied
                st.tickT = Random.Range(0.85f, 1.45f);
                AudioDirector.Fire(OzLava.SndTick, mid, id, 0.75f + 0.3f * dipK, Random.Range(0.68f, 0.92f)); BurnSounds++;
            }
            if (high && !st.critical) { st.critical = true; AudioDirector.Fire("lava_critical", mid, id, 1f, Random.Range(0.78f, 0.88f)); BurnSounds++; }
        }

        /// <summary>The char of all the HP lava took (steps of 0.05: TeamPaint.Charred repaints); raised only, never lowered here.</summary>
        static void CharUp(UnitView v, ZObject o)
        {
            float want = CharFloor(o);
            if (want <= 0f) return;
            var paint = v.GetComponent<TeamPaint>();
            if (paint && want > paint.Charred + 0.01f) { paint.Charred = want; CharSteps++; if (want > MaxChar) MaxChar = want; }
        }

        /// <summary>OpenZDamageStates' floor for a living unit's paint: a Bizon keeps the char of the HP lava took from it (0 for every other unit).</summary>
        public static float CharFloor(ZObject o)
        {
            if (!On || o == null || o.BizonLavaTaken <= 0 || o.MaxHealth <= 0 || o.Type != ZBigTanks.Bizon) return 0f;
            float k = Mathf.Clamp01(o.BizonLavaTaken / (float)o.MaxHealth);
            return Mathf.Floor(CharMax * k / 0.05f + 1e-4f) * 0.05f;
        }

        /// <summary>OpenZDamageStates' hit flash: true = this health drop (from lastHp) is the lava's heat alone, no hit (no flash). Only a Bizon.</summary>
        public static bool HeatOnly(ZObject o, int lastHp)
        {
            if (o == null || o.Type != ZBigTanks.Bizon) return false;
            int now = o.BizonLavaTaken;
            bool had = seenTaken.TryGetValue(o.Id, out int was);
            seenTaken[o.Id] = now;
            bool heat = had ? lastHp - o.Health <= now - was : o.BizonLavaTicks > 0;
            if (heat) FlashesHeld++;
            return heat;
        }

        /// <summary>UnitView.Die, before anything copies its blocks: the glow off, the loops out.</summary>
        public static void Death(UnitView v)
        {
            if (!v) return;
            if (!states.TryGetValue(v.GetInstanceID(), out var st)) return;
            Cold(st); StopLoops(v.Obj != null ? v.Obj.Id : -1, st);
            st.burn = 0f; st.trail = 0f; st.n = 0; st.bound = false;
        }

        static void StopLoops(int id, St st)
        {
            var ad = AudioDirector.I;
            if (st.loop && ad) ad.StopLoop("bizonburn:" + id, 0.4f);
            if (st.churn && ad) ad.StopLoop("bizonwade:" + id, 0.3f);
            st.loop = st.churn = false;
        }

        /// <summary>Views gone (removed, a new level): their loops stop, their entries go; all = every entry goes (the table is full).</summary>
        static void Sweep(bool all)
        {
            List<int> dead = null;
            foreach (var kv in states) if (all || !kv.Value.v || !kv.Value.v.isActiveAndEnabled) (dead ??= new List<int>()).Add(kv.Key);
            if (dead == null) return;
            foreach (var k in dead) { var st = states[k]; StopLoops(st.v && st.v.Obj != null ? st.v.Obj.Id : -1, st); if (st.v) Cold(st); states.Remove(k); }
        }

        static bool Draws(Renderer r)
        {
            if (!r || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r is SpriteRenderer) return false;
            var m = r.sharedMaterial; if (m && m.renderQueue >= 2450) return false;                       // light cones, glows, decals
            var n = r.name; return n.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("label", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        /// <summary>The hull's renderers with the TeamLit leg floats (OzFireHeat.Bind's rule): never a robot figure riding on it, nothing far up.</summary>
        static void Bind(St st)
        {
            st.bound = true; st.n = 0; BurnBound++;
            var v = st.v; float y0 = v.transform.position.y;
            var paint = v.GetComponent<TeamPaint>();
            v.GetComponentsInChildren<Renderer>(false, parts);
            for (int i = 0; i < parts.Count; i++)
            {
                var r = parts[i];
                if (!Draws(r)) continue;
                var host = r.GetComponentInParent<TeamPaint>(true);
                if (host && host != paint && host.IsRobotBody) continue;                                  // the driver keeps his own paint
                if (r.bounds.min.y - y0 > 2.4f) continue;
                r.GetSharedMaterials(mats);
                int has = 0;
                for (int s = 0; s < mats.Count && s < 32; s++) if (mats[s] && mats[s].HasProperty(LegHeatId)) has |= 1 << s;
                if (has == 0 || st.n == MaxParts) continue;
                st.rs[st.n] = r; st.slots[st.n] = Mathf.Min(mats.Count, 32); st.has[st.n] = has; st.n++;
            }
            parts.Clear(); mats.Clear();
        }

        /// <summary>OzFireHeat.Write's way: a renderer block serves its slots with no block of their own; each slot block is written too.</summary>
        static void Write(St st, float h, float line, float top)
        {
            if (!st.v) return;
            if (!st.bound) Bind(st);
            block ??= new MaterialPropertyBlock();
            for (int i = 0; i < st.n; i++)
            {
                var r = st.rs[i]; if (!r) continue;
                r.GetPropertyBlock(block);
                bool wide = !block.isEmpty;
                if (wide) { Put(h, line, top); r.SetPropertyBlock(block); }
                int has = st.has[i], n = st.slots[i];
                for (int s = 0; s < n; s++)
                {
                    if ((has & (1 << s)) == 0) continue;
                    r.GetPropertyBlock(block, s);
                    if (wide && block.isEmpty) continue;
                    Put(h, line, top);
                    r.SetPropertyBlock(block, s);
                }
            }
            st.wrote = true;
        }

        static void Put(float h, float line, float top) { block.SetFloat(LegHeatId, h); block.SetFloat(LegLineId, line); block.SetFloat(LegTopId, top); }

        static void Cold(St st) { if (st.wrote) { Write(st, 0f, 0f, 0f); st.wrote = false; } }

        public static string Status() => $"bizon wade {(On ? "on" : "off")} (sink {SinkM:0.00} m, lava {LavaSinkM:0.00} m): frames {WadeFrames}, entries water {WaterEntries} lava {LavaEntries}, splashes {Splashes}, rings {Rings}, sounds {Sounds}, churn loops {ChurnLoops}; "
                                         + $"burn: frames {BurnFrames} (peak glow {MaxGlow:0.00}), smoke {BurnSmokes}, embers {BurnEmbers}, flames {BurnFlames}, sounds {BurnSounds}, crackle loops {BurnLoops}, char steps {CharSteps} (most {MaxChar:0.00}), hit flashes held {FlashesHeld}, binds {BurnBound}; last: {Last}";
    }
}
