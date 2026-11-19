using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit ue_explosions_6 (View/OzUeBlasts.cs): the picture and the sound of one test explosion of the Unit editor. One of these
    /// runs per drop (a component on the shared object OZ_UeBlasts), draws through the pooled particle channels of the game, on
    /// channels of its own (<see cref="EnsureChannels"/>: volume-lit flipbook smoke with fire in its creases and flames in three
    /// palettes through Resources/OpenZ/FX/OzUeBlastFx.shader, streaks, glows, embers; View/OpenZFxPlayer.UeBlasts.cs) and removes
    /// itself when its last beat is over. The six timelines: OzUeBlastFx.Types.cs.
    /// Game time: a paused field holds every clock here (the particles already in the air go on like every effect of the game).
    /// Nothing is left behind: particles die by their lifetime, the scorch marks fade by the game's own rule, the channels (empty
    /// pools) go with the scene's effect player.
    /// </summary>
    public sealed partial class OzUeBlastFx : MonoBehaviour
    {
        /// <summary>Checks: explosions running now, particles emitted so far.</summary>
        public static int Live, Emitted;
        /// <summary>The blast shader and its flipbooks with baked volume light (8 x 8 frames; A = light from the right, above and the
        /// front + coverage, B = light from the left, below and behind + the fire seen through the smoke; kit tools/bake_sixway.py):
        /// billowing heads (4096 px) and fans of thrown earth (2048 px). The detail texture is the one of the game.</summary>
        public const string ShaderPath = "OpenZ/FX/OzUeBlastFx", BillowPath = "OpenZ/FX/Smoke/OzUeBlast_Billow", SprayPath = "OpenZ/FX/Smoke/OzUeBlast_Spray",
            DetailPath = "OpenZ/FX/Smoke/OpenZ_SmokeDetail";
        const float TAU = 6.2831853f;
        static readonly Vector3 Up = Vector3.up;
        static GameObject root;

        struct Tracer
        {
            public Vector3 p, v; public float g, life, age, every, next, size, trailLife, a; public Color col; public string ch; public byte land;
        }

        int type, seed; Vector3 O; System.Random rnd; OzUeBlasts.Stage[] stages; OpenZFxPlayer z;
        float age, t, end = 8f; bool begun; Vector3 lean;
        float c0, c1, c2, c3, c4; int n0, n1; bool f0;
        readonly List<Tracer> tracers = new List<Tracer>(64);
        readonly List<Vector3> sites = new List<Vector3>(12);

        // ------------------------------------------------------------------------------------------------ life
        // smoke: 1.5 A38 (kit air_burst_smoke_fade_r11): FLAK's smoke life (x, 1 = as made; less = its short smoke, View/OzUeBlastFx.Quick.cs)
        internal static OzUeBlastFx Begin(int type, Vector3 at, int seed, OzUeBlasts.Stage[] st, float scale = 1f, float flash = 1f, float smoke = 1f)
        {
            var z = OpenZFxPlayer.I;
            if (!z || !z.isActiveAndEnabled || !EnsureChannels(z)) return null;
            if (!root) root = new GameObject("OZ_UeBlasts");
            var fx = root.AddComponent<OzUeBlastFx>();
            fx.type = type; fx.seed = seed; fx.O = at; fx.stages = st; fx.z = z; fx.rnd = new System.Random(seed); fx.k = scale; fx.sk = Mathf.Sqrt(scale); fx.flashK = flash; All.Add(fx);
            float wa = fx.R(0f, TAU);
            fx.lean = new Vector3(Mathf.Cos(wa), 0f, Mathf.Sin(wa)) * fx.R(0.25f, 0.7f);                 // one light wind per explosion: its smoke leans one way
            Live++;
            if (type == OzUeBlasts.Shock) fx.ShockCharge();
            if (type == OzUeBlasts.Flak && smoke < 1f) fx.q = QuickOn(z, smoke);                            // 1.5 A38: 1 when the short channels cannot be made (the long smoke)
            return fx;
        }

        /// <summary>Makes the channels, loads the flipbooks and draws one unseen particle of each smoke and fire channel at `at`, so the
        /// first explosion of a field does not pay for them in its own frame. Called while a test explosion tile is aimed.</summary>
        public static void Warm(Vector3 at)
        {
            var z = OpenZFxPlayer.I;
            if (!z || !z.isActiveAndEnabled || z.HasChannel("ueb_dirt") || !EnsureChannels(z)) return;
            foreach (var d in Defs())
                if (d.shape == FxShape.Puff || d.shape == FxShape.Fireball) z.EmitChannel(d.name, at + Vector3.up, Vector3.zero, 0.1f, 0.02f, Color.white, 0f);
        }

        internal static void StopAll()
        {
            if (root) Destroy(root);
            root = null; Live = 0;
            billowA = billowB = sprayA = sprayB = null;                                                  // the flipbooks may be unloaded with the scene (the next drop loads them again)
        }

        void OnDestroy() { if (Live > 0) Live--; All.Remove(this); }

        /// <summary>The rules burst beat `stage` of this explosion at `at` (View/OzUeBlasts.cs Take).</summary>
        internal void Beat(int stage, Vector3 at)
        {
            if (!z) return;
            if (!begun) { begun = true; t = 0f; }
            switch (type)
            {
                case OzUeBlasts.Column: ColumnBeat(stage, at); break;
                case OzUeBlasts.Fuel: FuelBeat(stage, at); break;
                case OzUeBlasts.Chain: ChainBeat(stage, at); break;
                case OzUeBlasts.Geyser: GeyserBeat(stage, at); break;
                case OzUeBlasts.Flak: FlakBeat(stage, at); break;
                case OzUeBlasts.Shock: ShockBeat(stage, at); break;
                case OzUeBlasts.Airburst: AirBeat(stage, at); break;
            }
        }

        void Update()
        {
            if (!z) { Destroy(this); return; }
            var g = ZGame.I;
            float dt = g && g.paused ? 0f : Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;
            if (sk != 1f) dt /= sk;   // 1.5 kit explosions_in_game_1_5: k times the size runs sqrt(k) times as long
            age += dt;
            if (!begun) { if (age > 15f) Destroy(this); return; }                                        // the rules never burst it (the field was left)
            t += dt;
            switch (type)
            {
                case OzUeBlasts.Column: ColumnTick(dt); break;
                case OzUeBlasts.Fuel: FuelTick(dt); break;
                case OzUeBlasts.Chain: ChainTick(dt); break;
                case OzUeBlasts.Geyser: GeyserTick(dt); break;
                case OzUeBlasts.Flak: FlakTick(dt); break;
                case OzUeBlasts.Shock: ShockTick(dt); break;
                case OzUeBlasts.Airburst: AirTick(dt); break;
            }
            StepTracers(dt);
            if (t >= end && tracers.Count == 0) Destroy(this);
        }

        // ------------------------------------------------------------------------------------------------ small tools
        float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        static Vector3 Az(float a) => new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        Vector3 Ring() => Az(R(0f, TAU));
        /// <summary>A point in a ball of radius r.</summary>
        Vector3 Ball(float r)
        {
            float a = R(0f, TAU), y = R(-1f, 1f), s = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y)), k = r * Mathf.Pow(R(0f, 1f), 0.3333f);
            return new Vector3(Mathf.Cos(a) * s, y, Mathf.Sin(a) * s) * k;
        }
        /// <summary>A direction on the unit sphere.</summary>
        Vector3 Sphere()
        {
            float a = R(0f, TAU), y = R(-1f, 1f), s = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            return new Vector3(Mathf.Cos(a) * s, y, Mathf.Sin(a) * s);
        }
        /// <summary>How many times a clock of `period` seconds rang in this frame (at most 8).</summary>
        static int Ticks(ref float clock, float dt, float period)
        {
            clock += dt; int n = 0;
            while (clock >= period && n < 8) { clock -= period; n++; }
            if (n == 8) clock = 0f;
            return n;
        }

        void P(string ch, Vector3 p, Vector3 v, float life, float size, Color c, float a = 1f) { EmitL(ch, p, v, life, size, c, a); Emitted++; }
        void P(string ch, Vector3 p, Vector3 v, float life, float size, Color c, float a, float delay) { EmitL(ch, p, v, life, size, c, a, delay); Emitted++; }

        /// <summary>The floor's dust under the Unit editor's backdrop: sand on the dunes, pale on the snow field, grey-brown on the
        /// studio floors; k 0 = light, 1 = dark.</summary>
        static Color Dust(float k)
        {
            int kind = OzUeDust.KindNow;
            if (kind == 2) return Color.Lerp(new Color(0.86f, 0.88f, 0.90f), new Color(0.60f, 0.63f, 0.68f), k);
            if (kind == 1) return Color.Lerp(new Color(0.80f, 0.68f, 0.50f), new Color(0.50f, 0.40f, 0.28f), k);
            return Color.Lerp(new Color(0.62f, 0.57f, 0.50f), new Color(0.35f, 0.32f, 0.29f), k);
        }
        /// <summary>The ground thrown up: darker and damper than its dust.</summary>
        static Color Earth(float k)
        {
            int kind = OzUeDust.KindNow;
            if (kind == 2) return Color.Lerp(new Color(0.46f, 0.43f, 0.40f), new Color(0.24f, 0.21f, 0.19f), k);
            if (kind == 1) return Color.Lerp(new Color(0.40f, 0.30f, 0.19f), new Color(0.21f, 0.15f, 0.10f), k);
            return Color.Lerp(new Color(0.24f, 0.21f, 0.18f), new Color(0.11f, 0.10f, 0.09f), k);
        }
        static Color Soot(float k) => Color.Lerp(new Color(0.105f, 0.098f, 0.092f), new Color(0.27f, 0.25f, 0.23f), k);
        static Color Grey(float k) => Color.Lerp(new Color(0.52f, 0.50f, 0.47f), new Color(0.30f, 0.29f, 0.28f), k);

        void Flash(Vector3 p, float size, float life, Color c) => P("ueb_flash", p, Vector3.zero, life, size, c, 1f);

        /// <summary>The fire's own halo: a soft warm light in the air that goes with the fire for as long as it burns (drawn over the
        /// smoke, so the heads round the fire glow too).</summary>
        static readonly Color FireGlow = new Color(1f, 0.52f, 0.2f), FireDeep = new Color(1f, 0.4f, 0.13f);
        void Glow(Vector3 p, Vector3 v, float size, float life, Color c, float a = 0.7f, float delay = 0f) => P("ueb_flash", p, v, life, size, c, a, delay);

        void Sparks(Vector3 p, int n, float sp0, float sp1, float life0, float life1)
        {
            for (int i = 0; i < n; i++)
            {
                var d = Sphere(); d.y = Mathf.Abs(d.y) * 0.9f + 0.1f;
                P("m12_spark", p, d.normalized * R(sp0, sp1), R(life0, life1), R(0.07f, 0.12f), new Color(1f, 0.85f, 0.55f), 1f);
            }
        }

        /// <summary>A ring of ground-hugging dust rolling out from c (born r0 out, thrown at v0..v1 m/s, stopped by its own drag).</summary>
        void Skirt(Vector3 c, int n, float r0, float v0, float v1, float s0, float s1, float l0, float l1, float alpha)
        {
            float a0 = R(0f, TAU);
            for (int i = 0; i < n; i++)
            {
                var d = Az(a0 + i * TAU / n + R(-0.12f, 0.12f));
                P("ueb_skirt", c + d * (r0 * R(0.7f, 1.3f)) + Up * R(0.25f, 0.7f), d * R(v0, v1) + Up * R(0.15f, 0.9f), R(l0, l1), R(s0, s1), Dust(R(0f, 0.7f)), alpha);
                if (i % 3 == 0) P("ueb_haze", c + d * (r0 * R(0.5f, 1.1f)) + Up * R(0.4f, 1.0f), d * (R(v0, v1) * 0.62f) + Up * R(0.2f, 0.8f), R(l0, l1) * 1.1f, R(s0, s1) * 1.5f, Dust(R(0.1f, 0.6f)), alpha * 0.3f);
            }
        }

        void Embers(Vector3 p, int n, float sp0, float sp1, float life0, float life1)
        {
            for (int i = 0; i < n; i++)
                P("ueb_ember", p + Ball(0.8f), Sphere() * R(sp0, sp1) + Up * R(0f, 3f), R(life0, life1), R(0.14f, 0.3f), new Color(1f, R(0.55f, 0.85f), 0.3f), 1f);
        }

        void Kick(Vector3 at, float px, bool always = true)
        {
            var sh = OpenZFxShake.I;
            if (sh != null && px > 0f && sh.Kick(at, px * k * flashK, "ue_blast_" + OzUeBlasts.Ids[type], always)) OzUeBlasts.Kicks++;
        }

        /// <summary>A sound event of the game's table at `at` (volume and pitch x), now or after `delay` game seconds.</summary>
        void Snd(string ev, Vector3 at, float vol = 1f, float pitch = 1f, float delay = 0f)
        {
            void Go() { var ad = AudioDirector.I; if (ad && ad.Play(ev, at, -1, vol, pitch)) OzUeBlasts.Sounds++; }
            var d = AudioDirector.I;
            if (delay > 0f && d) d.Schedule(delay * sk, Go); else Go();
        }

        /// <summary>A take of one of the game's blast banks, pitched (the deep booms and the satellite pops).</summary>
        void Bank(string key, Vector3 at, float vol, float pitch, string[] bank, float delay = 0f)
        {
            void Go()
            {
                var au = CombatAudio.I; if (!au) return;
                int p0 = au.Played;
                au.PlayPitched(key, at, vol, 0f, -1, pitch, bank);
                if (au.Played > p0) OzUeBlasts.Sounds++;
            }
            var d = AudioDirector.I;
            if (delay > 0f && d) d.Schedule(delay * sk, Go); else Go();
        }

        // ------------------------------------------------------------------------------------------------ thrown pieces with trails
        /// <summary>A piece thrown from p at v under gravity g: its head is one particle of headCh (a channel with the same gravity, so
        /// it flies the same arc), behind it a trail of trailCh puffs every `every` seconds. land: 0 nothing, 1 a dust pop, 2 a clod
        /// (dust, chips, a thud), 3 a white-hot shard (sparks), 4 a burning drop (a small flame).</summary>
        void Launch(Vector3 p, Vector3 v, float g, float life, string headCh, float headSize, Color headCol, string trailCh, float every, float trailSize, float trailLife, Color trailCol, float trailA, byte land)
        {
            if (headCh != null) P(headCh, p, v, life, headSize, headCol, 1f);
            tracers.Add(new Tracer { p = p, v = v, g = g, life = life, every = every, size = trailSize, trailLife = trailLife, col = trailCol, a = trailA, ch = trailCh, land = land });
        }

        void StepTracers(float dt)
        {
            for (int i = tracers.Count - 1; i >= 0; i--)
            {
                var tr = tracers[i];
                var pPrev = tr.p; tr.v.y -= tr.g * dt; tr.p += tr.v * dt; tr.age += dt;
                float k = tr.age / tr.life;
                bool down = tr.v.y < 0f && tr.p.y <= O.y + 1.5f && tr.p.y <= z.GroundAt(tr.p) + 0.05f;
                if (down || k >= 1f)
                {
                    if (down) Land(tr);
                    tracers.RemoveAt(i);
                    continue;
                }
                if (tr.ch != null)
                {
                    tr.next -= dt;
                    int guard = 0;
                    while (tr.next <= 0f && guard++ < 12)
                    {
                        var pAt = Vector3.Lerp(tr.p, pPrev, Mathf.Clamp01(-tr.next / Mathf.Max(1e-4f, dt)));
                        tr.next += tr.every;
                        P(tr.ch, pAt + Ball(0.1f + tr.size * 0.12f), tr.v * 0.06f + Up * R(0.1f, 0.5f) + lean * 0.5f, tr.trailLife * R(0.8f, 1.2f), tr.size * R(0.8f, 1.25f) * (1f - 0.35f * k), tr.col, tr.a * (1f - 0.5f * k));
                    }
                    if (tr.next < 0f) tr.next = 0f;
                }
                tracers[i] = tr;
            }
        }

        void Land(Tracer tr)
        {
            var p = tr.p; p.y = z.GroundAt(p);
            switch (tr.land)
            {
                case 1:
                    P("ueb_skirt", p + Up * 0.4f, Up * R(0.4f, 1f) + lean, R(1.0f, 1.5f), R(0.9f, 1.3f), Dust(R(0.1f, 0.7f)), 0.7f);
                    break;
                case 2:
                    Skirt(p, 4, 0.3f, 2.5f, 4.5f, 0.9f, 1.3f, 1.2f, 1.9f, 0.8f);
                    for (int i = 0; i < 3; i++) EmitL("m12_stone", p + Up * 0.3f, Ring() * R(1f, 3f) + Up * R(2f, 4.5f), R(1.2f, 2f), R(0.2f, 0.4f), Color.white, 1f);
                    if (n1++ < 4) Snd("dst_debris_concrete", p, 0.7f, R(1.0f, 1.25f));
                    break;
                case 3:
                    Sparks(p + Up * 0.15f, 4, 3f, 7f, 0.25f, 0.5f);
                    P("ueb_trail", p + Up * 0.3f, Up * R(0.4f, 1f), R(1.0f, 1.6f), R(0.6f, 0.9f), Dust(R(0.2f, 0.8f)), 0.55f);
                    break;
                case 4:
                    for (int i = 0; i < 4; i++) P("ueb_fire", p + Ball(0.3f) + Up * 0.3f, Up * R(1.2f, 2.6f), R(0.4f, 0.7f), R(0.8f, 1.3f), Color.white, 1f, i * 0.12f);
                    P("ueb_trail", p + Up * 0.5f, Up * R(0.8f, 1.6f) + lean, R(1.2f, 1.9f), R(0.7f, 1.0f), Soot(R(0.1f, 0.7f)), 0.6f, 0.3f);
                    ScorchL(p, 0.5f, 0);
                    break;
                case 5:     // a fragment of an air burst: a kick of dust, chips, now and then sparks, a small burn mark, the hit
                    P("ueb_skirt", p + Up * 0.25f, Ring() * R(0.5f, 1.6f) + Up * R(0.5f, 1.3f) + lean * 0.5f, R(0.7f, 1.1f), R(0.6f, 0.95f), Dust(R(0.1f, 0.7f)), 0.7f);
                    for (int i = 0; i < 2; i++) P("ueb_dirt", p + Up * 0.1f, (Up * R(0.6f, 1f) + Ring() * R(0.2f, 0.7f)).normalized * R(4f, 8f), R(0.3f, 0.5f), R(0.08f, 0.14f), Earth(R(0f, 1f)), 1f);
                    if (n1 % 3 == 0) Sparks(p + Up * 0.1f, 2, 3f, 7f, 0.15f, 0.3f);
                    if (n1 % 4 == 0) ScorchL(p, R(0.3f, 0.5f), 0);
                    if (n1 < 18 && n1 % 2 == 0) Snd("imp_bullet_stone", p, 0.75f, R(0.85f, 1.3f));
                    n1++;
                    break;
                case 6:     // a burning chunk: it hits, throws sparks and dust, bounces on and burns out where it comes to rest
                    Sparks(p + Up * 0.15f, 4, 3f, 8f, 0.2f, 0.4f);
                    P("ueb_skirt", p + Up * 0.3f, Up * R(0.5f, 1.2f) + lean, R(0.9f, 1.3f), R(0.8f, 1.1f), Dust(R(0.1f, 0.7f)), 0.7f);
                    ScorchL(p, 0.4f, 0);
                    Snd("dst_debris_metal", p, 0.7f, R(0.9f, 1.2f));
                    Launch(p + Up * 0.1f, new Vector3(tr.v.x * 0.5f, Mathf.Abs(tr.v.y) * 0.38f, tr.v.z * 0.5f), tr.g, 2.5f, null, 0f, Color.white, "ueb_fire", 0.035f, tr.size * 0.85f, tr.trailLife, Color.white, 1f, 4);
                    break;
            }
        }

        // ------------------------------------------------------------------------------------------------ the channels
        static FxChannelDef PuffDef(string name, int cap, int queue, float gravity, float drag)
        {
            return new FxChannelDef
            {
                name = name, shape = FxShape.Puff, capacity = cap, queue = queue, opaqueness = 1f, tint = Color.white, intensity = 1f, lit = 1f, wrap = 0.3f,
                edgeSoft = 0.3f, softFade = 1.2f, sortByDistance = true, noiseAmt = 0.42f, noiseScale = 1.8f, boil = 0.9f, spin = 0.4f, bump = 0.42f, selfShadow = 0.88f,
                gravity = gravity, drag = drag, sizeCurve = FxSizeCurve.BillowGrow, alphaCurve = FxAlphaCurve.BillowFade,
            };
        }

        static FxChannelDef FireDef(string name, int cap, int queue, float gravity, float drag)
        {
            return new FxChannelDef
            {
                name = name, shape = FxShape.Fireball, capacity = cap, queue = queue, opaqueness = 1f, tint = Color.white, intensity = 1f, edgeSoft = 0.3f, softFade = 0.6f,
                fireRamp = true, fireCore = OpenZFxCatalog.FireV2Core, fireHot = OpenZFxCatalog.FireV2Hot, fireMid = OpenZFxCatalog.FireV2Mid, fireRim = OpenZFxCatalog.FireV2Rim,
                fireSoot = OpenZFxCatalog.FireV2Soot, cooling = 0.8f, heatOpacity = true, opacityRim = OpenZFxCatalog.FireOpacityRim, opacityCore = OpenZFxCatalog.FireOpacityCore,
                noiseAmt = 0.42f, noiseScale = 2.2f, boil = 1.5f, spin = 0.7f, gravity = gravity, drag = drag, sizeCurve = FxSizeCurve.SinShrink, alphaCurve = FxAlphaCurve.One,
            };
        }

        /// <summary>The definitions of the twenty channels (also read by the counted check). Smoke and fire are finished by
        /// <see cref="Dress"/>: their own shader, flipbooks, palettes and curves.</summary>
        public static FxChannelDef[] Defs()
        {
            // volume-lit flipbook smoke. Drawn in this order: ground dust, thrown earth, trails, plain dust heads, the rising heads, balls of fire, soot, the veil of haze
            var skirt = PuffDef("ueb_skirt", 2600, 3008, 0f, 0.93f);
            var wall = PuffDef("ueb_wall", 900, 3008, 0f, 0.9f);
            var spray = PuffDef("ueb_spray", 500, 3009, 7.5f, 0.992f);
            var finger = PuffDef("ueb_finger", 300, 3009, 9.81f, 0.996f); finger.stretch = true; finger.lengthScale = 2.6f;
            var jet = PuffDef("ueb_jet", 1400, 3009, 9.81f, 0.995f);                                         // GEYSER's jets of earth: heads that fly and fall
            var trail = PuffDef("ueb_trail", 9000, 3010, -0.05f, 0.95f);
            var dust = PuffDef("ueb_dust", 900, 3011, -0.2f, 0.98f);
            var cauli = PuffDef("ueb_cauli", 2200, 3012, -0.35f, 0.98f);
            var hot = PuffDef("ueb_hot", 700, 3013, -1.2f, 0.972f);
            var ion = PuffDef("ueb_ion", 300, 3013, -0.4f, 0.97f);
            var pillar = PuffDef("ueb_pillar", 500, 3012, -0.9f, 0.99f);
            var soot = PuffDef("ueb_soot", 900, 3013, -0.55f, 0.978f);
            var haze = PuffDef("ueb_haze", 1600, 3014, -0.12f, 0.97f);
            // flames: the orange tongue, the round red fuel burst, the blue-white plasma
            var fire = FireDef("ueb_fire", 1200, 3020, -1.2f, 0.975f);
            var fuel = FireDef("ueb_fuel", 900, 3021, -2.2f, 0.97f);
            var plasma = FireDef("ueb_plasma", 400, 3022, 0f, 0.96f);
            // additive lights: the flash, the embers, the white-hot shards; opaque lit dirt streaks
            var flash = new FxChannelDef { name = "ueb_flash", shape = FxShape.Glow, capacity = 96, queue = 3040, opaqueness = 0f, tint = Color.white, intensity = 12f, coreColor = Color.white, coreMix = 0.8f,
                sizeCurve = FxSizeCurve.SinShrink, alphaCurve = FxAlphaCurve.One, colorFade = 0.7f };
            var ember = new FxChannelDef { name = "ueb_ember", shape = FxShape.Glow, capacity = 900, queue = 3050, opaqueness = 0f, tint = Color.white, intensity = 3f, coreColor = new Color(1f, 0.86f, 0.55f), coreMix = 0.7f,
                gravity = 3.2f, drag = 0.975f, sizeCurve = FxSizeCurve.Lerp, sizeEnd = 0.25f, alphaCurve = FxAlphaCurve.LegSin };
            var shard = new FxChannelDef { name = "ueb_shard", shape = FxShape.Streak, capacity = 400, queue = 3046, opaqueness = 0f, tint = Color.white, intensity = 9f, coreColor = new Color(1f, 0.97f, 0.85f), coreMix = 0.7f,
                squareness = 0.3f, stretch = true, lengthScale = 16f, gravity = 12f, minWidth = 0.08f, sizeCurve = FxSizeCurve.Constant, alphaCurve = FxAlphaCurve.One };
            var dirt = new FxChannelDef { name = "ueb_dirt", shape = FxShape.Streak, capacity = 1400, queue = 3009, opaqueness = 1f, tint = Color.white, intensity = 1f, lit = 1f, wrap = 0.4f,
                squareness = 0.35f, stretch = true, lengthScale = 7f, gravity = 9.81f, minWidth = 0.1f, sizeCurve = FxSizeCurve.Constant, alphaCurve = FxAlphaCurve.PopFade };
            return new[] { skirt, wall, spray, finger, jet, trail, dust, cauli, hot, ion, pillar, soot, haze, fire, fuel, plasma, flash, ember, shard, dirt };
        }

        static Texture2D billowA, billowB, sprayA, sprayB, detailTex; static Shader blastShader; static bool warned;

        // the three fire palettes (linear HDR: core, hot, body, rim, soot): the game's orange, a fuel red that ends in soot, a blue-white plasma
        static readonly Color[][] Pal =
        {
            new[] { OpenZFxCatalog.FireV2Core, OpenZFxCatalog.FireV2Hot, OpenZFxCatalog.FireV2Mid, OpenZFxCatalog.FireV2Rim, OpenZFxCatalog.FireV2Soot },
            new[] { new Color(7.0f, 2.2f, 0.35f), new Color(4.6f, 0.85f, 0.04f), new Color(2.6f, 0.26f, 0f), new Color(1.1f, 0.07f, 0f), new Color(0.03f, 0.025f, 0.02f) },
            new[] { new Color(4.0f, 7.0f, 9.0f), new Color(1.2f, 3.4f, 6.8f), new Color(0.3f, 1.2f, 4.2f), new Color(0.14f, 0.3f, 1.7f), new Color(0.05f, 0.07f, 0.16f) },
        };

        static void V(Material m, string prop, Color c) => m.SetVector(prop, new Vector4(c.r, c.g, c.b, 1f));

        static void Palette(Material m, int pal)
        {
            var p = Pal[pal];
            V(m, "_FireCore", p[0]); V(m, "_FireHot", p[1]); V(m, "_FireMid", p[2]); V(m, "_FireRim", p[3]); V(m, "_FireSoot", p[4]);
        }

        /// <summary>Alpha over life: pairs (time 0..1, alpha), at most eight.</summary>
        static void Fade(ParticleSystem ps, params float[] ta)
        {
            var ak = new GradientAlphaKey[ta.Length / 2];
            for (int i = 0; i < ak.Length; i++) ak[i] = new GradientAlphaKey(ta[i * 2 + 1], ta[i * 2]);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, ak);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = new ParticleSystem.MinMaxGradient(g);
        }

        /// <summary>Size over life: s0 at birth, a fast push toward s1 (the power), x the emitted size.</summary>
        static void Grow(ParticleSystem ps, float s0, float s1, float pow)
        {
            const int n = 16; var keys = new Keyframe[n + 1]; float mul = Mathf.Max(s0, s1);
            for (int i = 0; i <= n; i++) { float t = i / (float)n; keys[i] = new Keyframe(t, (s0 + (s1 - s0) * (1f - Mathf.Pow(1f - t, pow))) / mul); }
            var c = new AnimationCurve(keys);
            for (int i = 0; i <= n; i++) c.SmoothTangents(i, 0f);
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(mul, c);
        }

        /// <summary>The air brakes a puff by the square of its speed: thrown fast, it slows at once and then creeps (the channel drag of
        /// the game is a weak constant brake). Speed after t seconds: v0 / (1 + c v0 t).</summary>
        static void Brake(ParticleSystem ps, float c)
        {
            var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 1000f; lv.dampen = 0f; lv.drag = c;
            lv.multiplyDragByParticleSize = false; lv.multiplyDragByParticleVelocity = true;
        }

        /// <summary>Thrown fast, then standing: no puff is faster than a limit that falls from vMax to vEnd (m/s) over the first
        /// `until` of its life and stays there. A head thrown up at 20 m/s climbs about 20 m in a second and then only drifts.</summary>
        static void Slow(ParticleSystem ps, float vMax, float until, float vEnd)
        {
            const int n = 12; var keys = new Keyframe[n + 2];
            for (int i = 0; i <= n; i++) { float u = i / (float)n; keys[i] = new Keyframe(u * until, (vEnd + (vMax - vEnd) * (1f - u) * (1f - u)) / vMax); }
            keys[n + 1] = new Keyframe(1f, vEnd / vMax);
            var c = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++) c.SmoothTangents(i, 0f);
            var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.drag = 0f; lv.multiplyDragByParticleVelocity = false;
            lv.limit = new ParticleSystem.MinMaxCurve(vMax, c); lv.dampen = 1f;
        }

        /// <summary>A slow swirl of the air the puffs sit in: they wander and roll instead of sliding on straight lines.</summary>
        static void Swirl(ParticleSystem ps, float strength, float freq)
        {
            var nz = ps.noise; nz.enabled = true; nz.strength = strength; nz.frequency = freq; nz.scrollSpeed = 0.3f; nz.damping = true; nz.octaveCount = 1;
            nz.quality = ParticleSystemNoiseQuality.Medium;
        }

        static void Smoke(Material m, bool spray, float edge, float detail, float contrast, Color shade, Color age, float heatTime, float heatStart, int pal, float turn = 1f, float along = 0f, float spin = 0.35f)
        {
            int queue = m.renderQueue; m.shader = blastShader; m.renderQueue = queue;      // a new shader puts the material back on its own queue
            m.SetFloat("_Shape", 2f); m.SetFloat("_Opaqueness", 1f); m.SetFloat("_UseVertexColor", 1f); m.SetFloat("_Intensity", HeartGain);
            m.SetTexture("_BlastA", spray ? sprayA : billowA); m.SetTexture("_BlastB", spray ? sprayB : billowB); m.SetTexture("_SmokeDetail", detailTex);
            m.SetFloat("_EdgeSoft", edge); m.SetFloat("_DetailAmt", detail); m.SetFloat("_DetailScale", spray ? 2.2f : 3f); m.SetFloat("_Dissolve", 0.5f); m.SetFloat("_FlipRate", 1f);
            m.SetFloat("_Spin", spin); m.SetFloat("_Turn", turn); m.SetFloat("_Along", along); m.SetFloat("_Boil", 0.9f); m.SetFloat("_Warp", 0.014f);
            m.SetFloat("_SunGain", 1f); m.SetFloat("_AmbGain", 1.25f); m.SetFloat("_PointGain", 1.5f); m.SetFloat("_Contrast", contrast); m.SetFloat("_Flat", 0.3f); m.SetFloat("_KeyUp", 0.7f); m.SetFloat("_KeySide", -0.25f); m.SetFloat("_HotShare", 0f);
            V(m, "_AgeColor", age); V(m, "_ShadeColor", shade);
            m.SetFloat("_HeatTime", heatTime); m.SetFloat("_HeatStart", heatStart); m.SetFloat("_CrackGain", 1.6f); m.SetFloat("_HeatFull", 2.3f);
            Palette(m, pal);
        }

        /// <summary>The fire is the hottest thing in the picture: the gain of the fire inside the smoke heads and of the flames (the
        /// orange and the fuel red; the blue-white plasma). Above the picture's bloom line by day and by night: the fire glows.</summary>
        public const float HeartGain = 1.5f, FlameGain = 1.25f, PlasmaGain = 2.0f;

        /// <summary>Soft smoke: the silhouette's feather (the density a puff needs to be fully there) and the fade into what it meets (m).</summary>
        static void Soft(Material m, float edge, float fade) { m.SetFloat("_EdgeSoft", edge); m.SetFloat("_SoftFade", fade); }

        /// <summary>Fast smoke smears: the puff is blurred along its own path while the shutter (the MOTION BLUR option's, x k) is open.</summary>
        static void Path(Material m, float k = 1f) { m.SetFloat("_Smear", k); m.SetFloat("_SmearPath", 1f); m.SetFloat("_SmearCap", 1.4f); }

        static void Flame(Material m, int pal, float teardrop, float cooling, float heatBias, float erode, float scale, float boil, float opRim, float opCore, float gain)
        {
            int queue = m.renderQueue; m.shader = blastShader; m.renderQueue = queue;
            m.SetFloat("_Shape", 3f); m.SetFloat("_Opaqueness", 1f); m.SetFloat("_UseVertexColor", 1f); m.SetFloat("_Intensity", pal == 2 ? PlasmaGain : FlameGain);
            m.SetFloat("_SoftFade", 1.4f);
            m.SetTexture("_SmokeDetail", detailTex);
            m.SetFloat("_EdgeSoft", 0.22f); m.SetFloat("_Teardrop", teardrop); m.SetFloat("_Cooling", cooling); m.SetFloat("_HeatBias", heatBias);
            m.SetFloat("_NoiseAmt", erode); m.SetFloat("_NoiseScale", scale); m.SetFloat("_Boil", boil); m.SetFloat("_HeatGain", gain);
            m.SetVector("_FireOpacity", new Vector4(opRim, opCore, 0f, 0f));
            Palette(m, pal);
        }

        /// <summary>The finish of one smoke or fire channel: the blast shader, its flipbook, palette, curves (the look numbers of the materials).</summary>
        static void Dress(string name, Material m, ParticleSystem ps, ParticleSystemRenderer r)
        {
            var warm = new Color(0.74f, 0.62f, 0.52f); var cold = new Color(0.55f, 0.56f, 0.62f); var one = Color.white;
            switch (name)
            {
                case "ueb_cauli":   // the rising heads: born with fire in their creases, dense for most of their life, then they open into wisps
                    Smoke(m, false, 0.16f, 0.26f, 1.4f, warm, new Color(0.92f, 0.9f, 0.88f), 0.3f, 0.75f, 0); m.SetFloat("_HotShare", 0.4f); Soft(m, 0.55f, 3.2f); Path(m);
                    Fade(ps, 0f, 0f, 0.03f, 1f, 0.58f, 1f, 0.8f, 0.6f, 1f, 0f); Grow(ps, 0.6f, 2.0f, 3.4f); Swirl(ps, 0.3f, 0.22f); Slow(ps, 40f, 0.28f, 0.45f);
                    break;
                case "ueb_dust":    // plain heads of dust and earth: no fire in them
                    Smoke(m, false, 0.16f, 0.26f, 1.4f, warm, new Color(1.1f, 1.08f, 1.05f), 0f, 0f, 0); Soft(m, 0.55f, 3.2f); Path(m);
                    Fade(ps, 0f, 0f, 0.03f, 1f, 0.58f, 1f, 0.8f, 0.6f, 1f, 0f); Grow(ps, 0.55f, 2.0f, 3.0f); Swirl(ps, 0.3f, 0.22f); Slow(ps, 22f, 0.26f, 0.35f);
                    break;
                case "ueb_ion":     // the ball of the shockwave: blue-white fire that rolls, breaks into glowing cracks and is gone
                    Smoke(m, false, 0.14f, 0.2f, 1.2f, cold, one, 1.0f, 1f, 2); m.SetFloat("_PointGain", 1.6f); m.SetFloat("_HeatFull", 3.2f); m.SetFloat("_Intensity", 1.3f); Soft(m, 0.5f, 3.2f); Path(m);
                    Fade(ps, 0f, 0f, 0.05f, 1f, 0.3f, 1f, 0.72f, 0f, 1f, 0f); Grow(ps, 0.4f, 2.1f, 3.0f); Swirl(ps, 0.4f, 0.3f); Slow(ps, 30f, 0.6f, 1.2f);
                    break;
                case "ueb_hot":     // a ball of fire: it rolls, breaks into glowing cracks and goes out (drawn over the heads: the fire in the smoke)
                    Smoke(m, false, 0.14f, 0.22f, 1.35f, warm, new Color(0.8f, 0.76f, 0.72f), 1.0f, 1f, 0); m.SetFloat("_HeatFull", 3.2f); Soft(m, 0.5f, 3.2f); Path(m);
                    Fade(ps, 0f, 0f, 0.04f, 1f, 0.4f, 1f, 0.8f, 0f, 1f, 0f); Grow(ps, 0.5f, 2.0f, 3.0f); Swirl(ps, 0.4f, 0.3f); Slow(ps, 34f, 0.55f, 0.8f);
                    break;
                case "ueb_soot":    // burning fuel: red fire rolling into black smoke with glowing cracks
                    Smoke(m, false, 0.15f, 0.24f, 1.35f, new Color(0.5f, 0.46f, 0.44f), one, 0.46f, 1f, 1); m.SetFloat("_SunGain", 1.7f); m.SetFloat("_AmbGain", 1.6f); m.SetFloat("_HeatFull", 2.8f); Soft(m, 0.55f, 3.2f); Path(m);
                    Fade(ps, 0f, 0f, 0.03f, 1f, 0.6f, 1f, 0.82f, 0.55f, 1f, 0f); Grow(ps, 0.6f, 2.2f, 2.6f); Swirl(ps, 0.4f, 0.24f); Slow(ps, 22f, 0.3f, 0.6f);
                    break;
                case "ueb_pillar":  // the late pillar of the shockwave: pale, a blue glow in its foot
                    Smoke(m, false, 0.18f, 0.26f, 1.45f, cold, new Color(0.8f, 0.8f, 0.82f), 0.2f, 0.7f, 2); Soft(m, 0.55f, 3.2f); Path(m);
                    Fade(ps, 0f, 0f, 0.05f, 1f, 0.5f, 1f, 1f, 0f); Grow(ps, 0.5f, 2.1f, 2.2f); Swirl(ps, 0.3f, 0.25f); Brake(ps, 0.1f);
                    break;
                case "ueb_haze":    // the thin veil that stays and drifts
                    Smoke(m, false, 0.75f, 0.2f, 1.1f, warm, one, 0f, 0f, 0); m.SetFloat("_Intensity", 1f); m.SetFloat("_PointGain", 1f);
                    Fade(ps, 0f, 0f, 0.18f, 1f, 0.45f, 1f, 1f, 0f); Grow(ps, 0.6f, 2.5f, 1.6f); Swirl(ps, 0.3f, 0.18f); Brake(ps, 0.3f);
                    break;
                case "ueb_skirt":   // dust rolling over the ground
                    Smoke(m, false, 0.3f, 0.24f, 1.4f, warm, new Color(1.05f, 1.03f, 1f), 0f, 0f, 0); Soft(m, 0.5f, 2.4f);
                    Fade(ps, 0f, 0f, 0.06f, 1f, 0.38f, 1f, 0.7f, 0.5f, 1f, 0f); Grow(ps, 0.6f, 2.3f, 2.4f); Swirl(ps, 0.25f, 0.3f); Brake(ps, 0.45f);
                    break;
                case "ueb_wall":    // the wall of dust behind the shock ring: dense on the front, gone a moment behind it
                    Smoke(m, false, 0.26f, 0.24f, 1.45f, warm, one, 0f, 0f, 0); m.SetFloat("_Intensity", 1f); m.SetFloat("_PointGain", 1f);
                    Fade(ps, 0f, 0f, 0.1f, 1f, 0.34f, 1f, 1f, 0f); Grow(ps, 0.6f, 1.9f, 1.8f); Swirl(ps, 0.3f, 0.3f); Brake(ps, 0.4f);
                    break;
                case "ueb_trail":   // the thin smoke behind thrown pieces
                    Smoke(m, false, 0.8f, 0.16f, 1.15f, warm, one, 0f, 0f, 0); m.SetFloat("_SoftFade", 2.0f);
                    Fade(ps, 0f, 0.85f, 0.3f, 0.7f, 1f, 0f); Grow(ps, 0.9f, 2.2f, 1.5f); Brake(ps, 0.5f);
                    break;
                case "ueb_spray":   // sheets of thrown earth, upright fans
                    Smoke(m, true, 0.2f, 0.3f, 1.2f, warm, new Color(1.15f, 1.12f, 1.08f), 0f, 0f, 0, 0f, 0f, 0.15f); Soft(m, 0.4f, 2.0f);
                    Fade(ps, 0f, 0f, 0.06f, 1f, 0.5f, 1f, 1f, 0f); Grow(ps, 0.55f, 1.7f, 1.8f);
                    break;
                case "ueb_finger":  // jets of earth and dust thrown out along their flight
                    Smoke(m, true, 0.22f, 0.3f, 1.2f, warm, new Color(1.1f, 1.08f, 1.05f), 0f, 0f, 0, 0f, 1f, 0f); Soft(m, 0.45f, 2.0f);
                    Fade(ps, 0f, 0f, 0.05f, 1f, 0.45f, 1f, 1f, 0f); Grow(ps, 0.5f, 1.6f, 1.6f);
                    break;
                case "ueb_jet":     // GEYSER's jets of earth: dense soft heads, blurred along their flight, thinning into dust
                    Smoke(m, false, 0.2f, 0.3f, 1.3f, warm, new Color(1.12f, 1.1f, 1.06f), 0f, 0f, 0); Soft(m, 0.5f, 2.6f); Path(m);
                    Fade(ps, 0f, 0f, 0.05f, 1f, 0.5f, 1f, 0.8f, 0.55f, 1f, 0f); Grow(ps, 0.6f, 1.7f, 2.0f);
                    break;
                case "ueb_fire": Flame(m, 0, 1f, 0.85f, 0.1f, 0.5f, 2.4f, 2.2f, 0.95f, 0.6f, 0.95f); break;
                case "ueb_fuel": Flame(m, 1, 0.25f, 0.95f, 0.1f, 0.55f, 2.2f, 1.8f, 0.97f, 0.7f, 0.9f); break;
                case "ueb_plasma": Flame(m, 2, 0f, 0.9f, 0.4f, 0.35f, 2.0f, 2.6f, 0.3f, 0f, 1.1f); break;
            }
        }

        /// <summary>The channels of the explosions on the effect player of this scene (made once per player).</summary>
        static bool EnsureChannels(OpenZFxPlayer z)
        {
            if (z.HasChannel("ueb_dirt")) return true;
            if (!blastShader) blastShader = Resources.Load<Shader>(ShaderPath);
            if (!billowA) billowA = Resources.Load<Texture2D>(BillowPath + "A");
            if (!billowB) billowB = Resources.Load<Texture2D>(BillowPath + "B");
            if (!sprayA) sprayA = Resources.Load<Texture2D>(SprayPath + "A");
            if (!sprayB) sprayB = Resources.Load<Texture2D>(SprayPath + "B");
            if (!detailTex) detailTex = Resources.Load<Texture2D>(DetailPath);
            bool assets = blastShader && billowA && billowB && sprayA && sprayB && detailTex;
            if (!assets && !warned) { warned = true; Debug.LogWarning($"{OzUnitEditor.Tag} test explosions: the blast shader or a flipbook is missing ({ShaderPath}, {BillowPath}, {SprayPath}); the plain smoke of the game is used"); }
            bool ok = true;
            foreach (var d in Defs())
            {
                string nm = d.name;
                bool own = assets && (d.shape == FxShape.Puff || d.shape == FxShape.Fireball);
                ok &= own ? z.UeAddChannel(d, (m, ps, r) => Dress(nm, m, ps, r)) : z.UeAddChannel(d);
            }
            if (!ok) Debug.LogWarning($"{OzUnitEditor.Tag} test explosions: a particle channel could not be made");
            return z.HasChannel("ueb_dirt") && z.HasChannel("ueb_cauli") && z.HasChannel("ueb_fire");
        }
    }
}
