using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// Quality reference refs_1_5/smoke_reference_owner_
    /// The MAMMOTH's and the BIZON's track dust and exhaust, redone. View only: the rules are read, never written.
    /// <para>What was wrong: big_tanks_fx drew the track dust with DustTrails (OpenZ/TrackDust: one round, unlit, hard-edged ball per puff, 1.6-3 m
    /// across, alpha 0.5, rings of them round the hull) and the exhaust with the shared leg_smoke channel (lit only 0.35, 0.6 m balls in a near-black
    /// burst colour at alpha 0.42-0.75): big round beige balls round the tanks and dark balls from the pipes.</para>
    /// <para>Now, per tank, two particle systems of its own (this component on "OZ_BigTankDust_&lt;id&gt;", a child of OZ_BigTanksFx, never of the
    /// tank), drawn with the game's best smoke: the 4096 lit smoke flipbook (8 variants x 8 time steps of 512 px, normal / height / density) with the
    /// 1024 detail erosion (OpenZ/FxAdditive shape 2, copied from the m12_dust and m12_smoke channels: sun + ambient + self shadow, soft particles
    /// against the ground, random variant, mirror and roll per particle, every particle rolled again on emission):</para>
    /// <list type="bullet">
    /// <item>TRACK DUST: low dust kicked up behind and beside each track (never a ring round the hull): many particles (quads born 0.4-1.5 m,
    /// growing to 1.2-4.6 m, the billow inside ~55 % of that), the ground's own colour a little paler (TrackSurface.DustColor; strength TrackSurface.DustStrength: sand full, dirt
    /// less, roads and decks faint, water none), rising 1.5-2.5 m while they grow and dissolve to nothing (eroded flipbook edges, alpha to 0);
    /// the amount follows the speed through a smoothed drive level (0.35 s in, 0.6 s out: zero standing still, never a pop), more when it sets off
    /// (0.8 s) and while it turns (the tracks scrub, also turning on the spot).</item>
    /// <item>EXHAUST: thin dark-grey billowing columns from the Bizon's two pipes (MK_Exhaust_L / _R) and the Mammoth's two rear deck stubs: small
    /// at the mouth (0.3-0.5 m quads), growing to 1.3-2.1 m, rising 3-4 m and leaning with the level's wind, lightening with age; a darker, denser short
    /// burst on setting off and on a hard speed-up (OzBigTankExhaust.Burst, its diesel chuff), a faint wisp while it stands crewed.</item>
    /// <item>THE HULL STAYS READABLE: the shader thins this dust / smoke where the camera sees it in front of the tank's own hull + turret
    /// (a rounded box measured from the hull and turret renderers once; OpenZ/FxAdditive _HullOn: dust keeps <see cref="DustHullKeep"/>, smoke
    /// <see cref="SmokeHullKeep"/>; soft edge, nothing changes anywhere else).</item>
    /// <item>DEATH / REMOVAL (the tank killed, broken up, sniped empty, REMOVE / CLEAR FIELD in the Unit editor, the level unloading): no new
    /// particle from that frame, the live ones fade out over <see cref="FadeSeconds"/> (smoothstep, the whole system at once through _FadeK), then
    /// the GameObject with both systems is destroyed (nothing kept, nothing pooled; the two materials are shared by every tank and built once).</item>
    /// </list>
    /// Cost: two particle systems per big tank (at most <see cref="DustCap"/> + <see cref="SmokeCap"/> particles), emitted by code (EmitParams, no
    /// per-frame allocation), the shared smoke shader (one draw per system on screen). Switch: <see cref="On"/> (false = big_tanks_fx's old dust and
    /// smoke, for a before / after only). Checks: View/OzBigTanksDustCheck.cs (menu OpenZ/V1.5/Big Tanks Dust Redo: ...).
    /// </summary>
    [DefaultExecutionOrder(32060)]
    public sealed class OzBigTanksDust : MonoBehaviour
    {
        public static bool On = true;

        // ------------------------------------------------------------------ numbers (dust)
        /// <summary>Particles at most per tank (dust, exhaust).</summary>
        public const int DustCap = 420, SmokeCap = 260;
        /// <summary>Dust volleys a second at full speed (each volley: a rear and a side particle per track), at a crawl, on setting off, turning.</summary>
        public static float DustRateFull = 18f, DustRateSlow = 6f, DustRateStart = 0f, DustRateTurn = 8f;   // 1.5 last_small_fixes: no setting-off boost (it drew the ring of puffs round the hull; was 16)
        /// <summary>Peak opacity of one dust particle (x the ground's dust strength x the drive level), its final size (m, x the tank's DustK) and life (s).</summary>
        public static float DustAlpha = 0.32f /* 1.5 pass 4: was 0.4 */, DustSizeMin = 2.0f, DustSizeMax = 4.0f, DustLifeMin = 2.6f, DustLifeMax = 3.8f;
        /// <summary>A dust particle is born at this share of its final size (0.5-0.8 m) and grows (ease out).</summary>
        public static float DustGrowFrom = 0.32f;
        /// <summary>Rise: upward speed at birth (m/s), drag (1/s), buoyancy (m/s2): ~1.5-2.5 m over its life.</summary>
        public static float DustRiseMin = 0.55f, DustRiseMax = 0.95f, DustDrag = 1.25f, DustBuoy = 0.35f;
        /// <summary>The ground's dust colour mixed this far to a pale sand (dust is lighter than the ground it comes from).</summary>
        public static float DustPale = 0.22f;
        public static readonly Color DustPaleColour = new Color(0.90f, 0.84f, 0.74f);
        /// <summary>1.5 closing pass 14k (R11 rest: pale round puffs beside a Bizon turning on the spot in the Unit editor by day): traced to the
        /// big tanks' SIDE puffs and the track-end SCRUB puffs of a turn (already on the lit wake channel, OzLevelDust): few, small young puffs,
        /// paled toward cream, each drawn alone on the flat floor = a round disc. Now they follow the rear trail's recipe (pass 6b gate B15):
        /// the ground's own colour, fainter, larger and spread, so they merge into the low soft haze the levels show behind the tracks.</summary>
        public static float SideSizeK = 1.6f, SideAlphaK = 0.4f, SidePale = 0.05f, ScrubSizeK = 2.0f, ScrubAlphaK = 0.3f, ScrubPale = 0.05f, ScrubSpread = 0.6f;   // 14k R11: tried on UE frames (A 1.35 / 0.5 / 1.6 / 0.4 / 0.4 still lobed balls; B = these: one soft haze); before 1 / 1 / -1 (DustPale 0.22) / 1 / 1 / -1 / 0
        /// <summary>1.5 pass 6b (gate B15: small round pale puffs behind each track end at a steady drive): the trail behind the tracks is part of
        /// the low ground-coloured track dust: per volley <see cref="RearPerVolley"/> fainter puffs (x <see cref="RearAlphaK"/>), as large as before
        /// (x <see cref="RearSizeK"/>) and spread round the track end (<see cref="RearSpread"/> m), the ground's own colour (<see cref="RearPale"/>
        /// toward pale sand, not <see cref="DustPale"/>), rising as the other dust (x <see cref="RearRiseK"/>); the dust material keeps more of the flipbook's
        /// lobes (<see cref="DustSmallFade"/>, was 0.8: a small young puff drew as a soft round ball). Only while it travels, as before.</summary>
        public static float RearAlphaK = 0.3f, RearSizeK = 1f, RearSpread = 0.45f, RearPale = 0.05f, RearRiseK = 1f, DustSmallFade = 0.55f;   // 1.5 pass 6b recheck: alpha 0.55 -> 0.3, size 1.25 -> 1 (at the ram / crush speed, 1.18 m/s, the trail still drew pale round clouds); rise back to 1 (the dust should go slightly higher)
        public static int RearPerVolley = 2;
        /// <summary>Drive level: seconds to ramp in / out.</summary>
        public static float LevelIn = 0.35f, LevelOut = 0.6f;
        /// <summary>The share of the dust / smoke kept where it lies in front of the tank's hull at the camera.</summary>
        public static float DustHullKeep = 0.22f, SmokeHullKeep = 0.55f;
        /// <summary>Death / removal: the fade-out of the live particles (s).</summary>
        public static float FadeSeconds = 1f;

        // ------------------------------------------------------------------ numbers (exhaust)
        /// <summary>Exhaust particles a second per mouth: standing crewed, crawling, full speed, extra at a full burst.</summary>
        public static float SmokeRateIdle = 6f, SmokeRateSlow = 11f, SmokeRateFull = 18f, SmokeRateBurst = 20f;   // 1.5 pass 4: about 2 x the puffs at 0.6 x the alpha: one column, not a string of beads (was 3.2 / 6 / 11 / 12)   // 1.5 last_small_fixes: burst 30 -> 12 (stacked small puffs drew round outlines)
        /// <summary>Opacity: standing, driving (slow .. full), a full burst.</summary>
        public static float SmokeAlphaIdle = 0.1f, SmokeAlphaSlow = 0.15f, SmokeAlphaFull = 0.21f, SmokeAlphaBurst = 0.21f;   // 1.5 pass 4: x 0.6 (was 0.16 / 0.24 / 0.34 / 0.34)   // 1.5 last_small_fixes: burst 0.46 -> 0.34
        /// <summary>Final size (m), born at this share, life (s), rise speed (m/s), drag, buoyancy, the wind's share.</summary>
        public static float SmokeSizeMin = 1.3f, SmokeSizeMax = 2.1f, SmokeGrowFrom = 0.24f, SmokeLifeMin = 2.4f, SmokeLifeMax = 3.3f,
                            SmokeRiseMin = 1.6f, SmokeRiseMax = 2.3f, SmokeDrag = 1.1f, SmokeBuoy = 1.3f, SmokeWind = 0.55f, DustWind = 0.25f;
        /// <summary>Exhaust colours (sRGB): standing, driving, the burst (the smoke lightens with age in the shader: _AgeColor).</summary>
        public static readonly Color SmokeIdle = new Color(0.46f, 0.45f, 0.44f), SmokeDrive = new Color(0.33f, 0.32f, 0.31f), SmokeBurst = new Color(0.3f, 0.29f, 0.28f);   // 1.5 last_small_fixes: burst 0.2 -> 0.3 grey

        // ------------------------------------------------------------------ proof counters (the checks read them)
        public static int Made, Releases, Destroyed, Alive, DustEmitted, SmokeEmitted, BurstFrames, HullBoxes, MaterialFails;
        public static float LastFadeSeconds;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Made = Releases = Destroyed = Alive = DustEmitted = SmokeEmitted = BurstFrames = HullBoxes = MaterialFails = 0; LastFadeSeconds = 0f; dustMat = smokeMat = null; }

        // ------------------------------------------------------------------ the two shared materials (the game's lit smoke flipbook)
        static Material dustMat, smokeMat;
        static readonly int FadeKId = Shader.PropertyToID("_FadeK"), HullOnId = Shader.PropertyToID("_HullOn"), HullXId = Shader.PropertyToID("_HullX"),
                            HullYId = Shader.PropertyToID("_HullY"), HullZId = Shader.PropertyToID("_HullZ");

        static bool Materials()
        {
            if (dustMat && smokeMat) return true;
            var fxp = OpenZFxPlayer.I;
            if (!fxp) return false;
            var d = fxp.CloneChannelMaterial("m12_dust"); var s = fxp.CloneChannelMaterial("m12_smoke");
            if (!d || !s || !d.HasProperty("_FadeK")) { MaterialFails++; if (d) Destroy(d); if (s) Destroy(s); return false; }   // the shader without this kit's lines: nothing drawn
            d.name = "OZ_BigTankDust"; s.name = "OZ_BigTankExhaust";
            d.hideFlags = s.hideFlags = HideFlags.DontSave;
            // dust: soft against the ground, sun-wrapped, a little self shadow, eroding into wisps as it dies, lighter (never darker) with age
            d.SetFloat("_Lit", 1f); d.SetFloat("_Wrap", 0.7f); d.SetFloat("_SelfShadow", 0.2f);   /* 1.5 pass 4: flatter light (was wrap 0.42, self shadow 0.55): no lit balls */ d.SetFloat("_SoftFade", 0.9f); d.SetFloat("_EdgeSoft", 0.85f);   /* 1.5 pass 4: edge 0.55 -> 0.85 */
            d.SetFloat("_Spin", 0.35f); d.SetFloat("_Boil", 0.45f); d.SetFloat("_Bump", 0.12f); /* 1.5 pass 4: 0.3 -> 0.12 */ d.SetFloat("_DetailAmt", 0.42f); d.SetFloat("_DetailScale", 2.6f);
            d.SetFloat("_Dissolve", 0.6f); d.SetFloat("_FlipRate", 1f); d.SetVector("_AgeColor", new Vector4(1.04f, 1.03f, 1.02f, 1f));
            d.SetFloat("_SmallFade", DustSmallFade); /* 1.5 pass 4: 0.45 -> 0.8, small puffs fuse into one soft cloud; 1.5 pass 6b: 0.55 (a lone young puff drew as a round ball) */ d.SetFloat("_HullKeep", DustHullKeep); d.SetFloat("_HullOn", 0f); d.SetFloat("_FadeK", 1f);
            d.renderQueue = 3008;
            // exhaust: denser self shadow (volume), dark at the mouth and lighter as it spreads (linear multiplier), boiling, no birth heat
            s.SetFloat("_Lit", 1f); s.SetFloat("_Wrap", 0.42f); s.SetFloat("_SelfShadow", 0.5f); /* 1.5 last_small_fixes: softer rims (was wrap 0.3, self shadow 0.72) */ s.SetFloat("_SoftFade", 0.6f); s.SetFloat("_EdgeSoft", 0.8f);   /* 1.5 pass 4: edge 0.42 -> 0.8 (no round puff outlines in the column) */
            s.SetFloat("_Spin", 0.5f); s.SetFloat("_Boil", 0.7f); s.SetFloat("_Bump", 0.38f); s.SetFloat("_DetailAmt", 0.4f); s.SetFloat("_DetailScale", 2.4f);
            s.SetFloat("_Dissolve", 0.6f); s.SetFloat("_FlipRate", 1f); s.SetVector("_AgeColor", new Vector4(2.6f, 2.55f, 2.5f, 1f)); s.SetVector("_HeatColor", Vector4.zero);
            s.SetFloat("_SmallFade", 0.75f); /* 1.5 pass 4: 0.35 -> 0.75 */ s.SetFloat("_HullKeep", SmokeHullKeep); s.SetFloat("_HullOn", 0f); s.SetFloat("_FadeK", 1f);
            s.renderQueue = 3009;
            dustMat = d; smokeMat = s;
            return true;
        }

        /// <summary>POSITION, NORMAL, COLOR, TEXCOORD0 = UV + AgePercent + StableRandom.x (the puff shader's age and variant), then the motion
        /// smoothing streams (unused here: _Smear 0) so the layout matches the effect player's channels.</summary>
        static readonly List<ParticleSystemVertexStream> Streams = new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV, ParticleSystemVertexStream.AgePercent, ParticleSystemVertexStream.StableRandomX,
            ParticleSystemVertexStream.Velocity, ParticleSystemVertexStream.SizeX, ParticleSystemVertexStream.Center,
        };

        // ------------------------------------------------------------------ one tank's emitter
        UnitView view; float dustK = 1f;
        ParticleSystem dust, smoke; ParticleSystemRenderer dustR, smokeR; MaterialPropertyBlock mpb;
        Bounds hullBox; bool hullOk;
        float level, startT, dustAcc, burstPrev, windNext, yawRate, lastYaw, fadeT;
        float[] smokeAcc = new float[0];
        Vector3 lastPos; bool hasLast, wasMoving, released;

        public bool Released => released;
        public float Level => level;
        public float Fade => released ? 1f - Mathf.SmoothStep(0f, 1f, fadeT / Mathf.Max(0.01f, FadeSeconds)) : 1f;
        public int DustLive => dust ? dust.particleCount : 0;
        public int SmokeLive => smoke ? smoke.particleCount : 0;
        public ParticleSystem DustSystem => dust;
        public ParticleSystem SmokeSystem => smoke;

        /// <summary>The emitter of one big tank's view (null while the effect player's materials are not there yet: asked again later).</summary>
        public static OzBigTanksDust Make(Transform parent, UnitView v, float dustScale, Renderer hull, Renderer turret)
        {
            if (!On || !v || v.Obj == null || !Materials()) return null;
            var go = new GameObject("OZ_BigTankDust_" + v.Obj.Id);
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var e = go.AddComponent<OzBigTanksDust>();
            e.view = v; e.dustK = dustScale; e.mpb = new MaterialPropertyBlock();
            e.dust = e.System("Dust", dustMat, DustCap, DustDrag, DustBuoy, DustGrowFrom, true, out e.dustR);
            e.smoke = e.System("Exhaust", smokeMat, SmokeCap, SmokeDrag, SmokeBuoy, SmokeGrowFrom, false, out e.smokeR);
            e.hullOk = HullBox(v.transform, hull, turret, out e.hullBox);
            if (e.hullOk) HullBoxes++;
            go.SetActive(true);
            e.dust.Play(); e.smoke.Play();
            Made++; Alive++;
            return e;
        }

        ParticleSystem System(string name, Material mat, int cap, float drag, float buoy, float growFrom, bool isDust, out ParticleSystemRenderer r)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>(); r = go.GetComponent<ParticleSystemRenderer>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = cap; main.startSpeed = 0f; main.startLifetime = 1f; main.startSize = 1f;
            float g = Mathf.Max(0.01f, Mathf.Abs(Physics.gravity.y));
            main.gravityModifier = -buoy / g;                                                         // buoyancy: a slow rise that the drag holds
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var sol = ps.sizeOverLifetime; sol.enabled = true;                                        // born small, grows (ease out)
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, growFrom, 0f, 2.6f), new Keyframe(0.35f, Mathf.Lerp(growFrom, 1f, 0.72f)), new Keyframe(1f, 1f, 0.15f, 0f)));
            var col = ps.colorOverLifetime; col.enabled = true;                                       // dust: in fast, out slowly to nothing; exhaust: thin at the pipe (no dark ball at the mouth), fullest once it has spread
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                isDust ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.82f, 0.32f), new GradientAlphaKey(0.42f, 0.62f), new GradientAlphaKey(0.12f, 0.86f), new GradientAlphaKey(0f, 1f) }
                       : new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.35f, 0.05f), new GradientAlphaKey(1f, 0.16f), new GradientAlphaKey(0.74f, 0.38f), new GradientAlphaKey(0.34f, 0.68f), new GradientAlphaKey(0.08f, 0.9f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var lv = ps.limitVelocityOverLifetime; lv.enabled = true;                                 // air drag: puffed out, then slowed
            lv.limit = 1000f; lv.dampen = 0f; lv.drag = drag; lv.multiplyDragByParticleSize = false; lv.multiplyDragByParticleVelocity = false;
            var rol = ps.rotationOverLifetime; rol.enabled = false;
            var f = ps.forceOverLifetime; f.enabled = true; f.space = ParticleSystemSimulationSpace.World;   // the level's wind (set again every second)
            f.x = 0f; f.y = 0f; f.z = 0f;
            r.renderMode = ParticleSystemRenderMode.Billboard; r.alignment = ParticleSystemRenderSpace.View;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.minParticleSize = 0f; r.maxParticleSize = 3f; r.applyActiveColorSpace = true;
            r.sharedMaterial = mat;
            r.SetActiveVertexStreams(Streams);
            return ps;
        }

        /// <summary>The hull + turret box in the view's own space (measured once from the two renderers' mesh boxes).</summary>
        static bool HullBox(Transform frame, Renderer hull, Renderer turret, out Bounds box)
        {
            box = default; bool any = false;
            foreach (var r in new[] { hull, turret })
            {
                if (!r) continue;
                var lb = r.localBounds; var c = lb.center; var e = lb.extents;
                if (e.sqrMagnitude < 1e-6f) continue;
                for (int i = 0; i < 8; i++)
                {
                    var p = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                    var q = frame.InverseTransformPoint(r.transform.TransformPoint(p));
                    if (!any) { box = new Bounds(q, Vector3.zero); any = true; } else box.Encapsulate(q);
                }
            }
            return any && box.extents.x > 0.3f && box.extents.z > 0.3f && box.extents.y > 0.2f;
        }

        // ------------------------------------------------------------------ every frame (OzBigTanksFx.Tank.DustFxStep)
        /// <summary>One frame of a living tank: emit (or not), the hull box follows. Read-only on the rules.</summary>
        public void Step(float dt, bool crewed, bool hullShown, VehicleMotion vm, OzBigTankExhaust exh, Transform[] mouths, string type, float hg, float half)
        {
            if (released || !view) return;
            Hull();
            if (Time.time >= windNext) Wind();
            if (dt <= 0f) return;
            var t = view.transform; var pos = t.position;
            var fw = -t.forward; fw.y = 0f; if (fw.sqrMagnitude < 1e-4f) return; fw.Normalize();      // models face -z
            float yaw = Mathf.Atan2(fw.x, fw.z) * Mathf.Rad2Deg;
            if (!hasLast) { hasLast = true; lastPos = pos; lastYaw = yaw; }
            var mv = pos - lastPos; mv.y = 0f; float md = mv.magnitude; lastPos = pos;
            yawRate = Mathf.Lerp(yawRate, Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)) / dt, 1f - Mathf.Exp(-8f * dt)); lastYaw = yaw;
            bool teleport = md > 4f;
            bool on = crewed && hullShown && !teleport;
            bool moving = on && (vm ? vm.DriveMoving : md > 0.002f);
            float s01 = Mathf.Clamp01((vm ? Mathf.Abs(vm.DriveSpeed) : md / dt) / 1.25f);
            float turnK = on ? Mathf.Clamp01((yawRate - 12f) / 40f) : 0f;
            float target = moving ? Mathf.Max(0.3f, s01) : 0.55f * turnK;
            level = Mathf.MoveTowards(level, target, dt / (target > level ? LevelIn : LevelOut));
            if (moving && !wasMoving) startT = 0.8f;
            wasMoving = moving;
            startT = Mathf.Max(0f, startT - dt);
            if (on) Exhaust(dt, moving, s01, vm, exh, mouths);
            moveK = Mathf.MoveTowards(moveK, Mathf.Clamp01((md / dt - 0.35f) / 0.8f), dt * 3f);   // 1.5 pass 4: how much it really travels (a turn on the spot = 0)
            var travel = md / dt > 0.35f && md > 1e-4f ? mv / md : (vm && vm.DriveSpeed < 0f ? -fw : fw);   // a pivot's tiny steps point every way: the hull's own line then (was mv / md always: the ring round the hull)
            if (level > 0.002f || startT > 0f) DustStep(dt, pos, fw, travel, s01, turnK, hg, half);
        }

        void Hull()
        {
            if (!hullOk) return;
            var t = view.transform;
            var c = t.TransformPoint(hullBox.center);
            var s = Vector3.Scale(hullBox.extents, t.lossyScale) * 1.06f;
            var m = Matrix4x4.TRS(c, t.rotation, new Vector3(Mathf.Max(0.05f, s.x), Mathf.Max(0.05f, s.y), Mathf.Max(0.05f, s.z))).inverse;   // world -> the box's unit space
            Block(m.GetRow(0), m.GetRow(1), m.GetRow(2));
        }

        Vector4 hx, hy, hz;
        void Block(Vector4 x, Vector4 y, Vector4 z)
        {
            hx = x; hy = y; hz = z;
            Apply();
        }

        void Apply()
        {
            float f = Fade;
            mpb.Clear();
            mpb.SetFloat(FadeKId, f);
            mpb.SetFloat(HullOnId, hullOk ? 1f : 0f);
            mpb.SetVector(HullXId, hx); mpb.SetVector(HullYId, hy); mpb.SetVector(HullZId, hz);
            if (dustR) dustR.SetPropertyBlock(mpb);
            if (smokeR) smokeR.SetPropertyBlock(mpb);
        }

        void Wind()
        {
            windNext = Time.time + 1f;
            var w = OzShuttleExhaust.Wind();
            var f = dust.forceOverLifetime; f.x = w.x * DustWind; f.z = w.z * DustWind; f.y = 0f;
            var g = smoke.forceOverLifetime; g.x = w.x * SmokeWind; g.z = w.z * SmokeWind; g.y = 0f;
        }

        // ------------------------------------------------------------------ dust
        void DustStep(float dt, Vector3 pos, Vector3 fw, Vector3 dir, float s01, float turnK, float hg, float half)
        {
            var tm = TrackMarks.I; var dt0 = DustTrails.I;
            if (tm == null || tm.Surface == null || !dt0 || !dt0.isActiveAndEnabled) return;     // the level's dust option (oz_dusttrails) and ground answers, as big_tanks_fx
            var lat = new Vector3(fw.z, 0f, -fw.x);
            float rate = level * Mathf.Lerp(DustRateSlow, DustRateFull, s01) + (startT > 0f ? DustRateStart * (startT / 0.8f) : 0f) + turnK * DustRateTurn;
            dustAcc += rate * dt;
            if (dustAcc > 4f) dustAcc = 4f;                                                          // a long frame: never a clump
            float lk = level;                                                                        // 1.5 last_small_fixes: the dust ramps in with the drive level (was at least 0.7 while setting off)
            while (dustAcc >= 1f)
            {
                dustAcc -= 1f; OzBigTanksFx.DustVolleys++;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    // behind the track's rear: the trail (1.5 pass 4: only while it travels)
                    if (moveK > 0.05f)
                    for (int rp = 0; rp < RearPerVolley; rp++)                                       // 1.5 pass 6b: fainter, larger, spread, ground-coloured, lower (gate B15)
                    {
                    var rear = pos + lat * (sgn * (hg + Random.Range(-0.35f, 0.3f) + Random.Range(-RearSpread, RearSpread))) - dir * (half + Random.Range(0.05f, 0.6f + RearSpread));
                    DustPuff(tm, rear, -dir * (0.3f + 0.8f * s01) * Random.Range(0.7f, 1.2f) + lat * (sgn * Random.Range(0.08f, 0.4f)), Random.Range(0.85f, 1.05f) * (0.75f + 0.35f * s01) * RearSizeK, lk, moveK * RearAlphaK, RearPale, RearRiseK);
                    }
                    // beside the track, thrown outwards (more while it turns or sets off)
                    var side = pos + lat * (sgn * (hg + 0.55f + Random.Range(0f, 0.25f))) + fw * Random.Range(-half * 0.9f, half * 0.35f);
                    float sideK = (0.55f + 0.45f * turnK) * moveK;                                   // 1.5 pass 4: none in a turn on the spot (x moveK)                                         // 1.5 last_small_fixes: the side puffs are not raised on setting off (was max(turnK, startT / 0.8))
                    DustPuff(tm, side, lat * (sgn * Random.Range(0.3f, 0.7f)) - dir * 0.15f, Random.Range(0.6f, 0.8f) * SideSizeK, lk, sideK * SideAlphaK, SidePale);   // 14k R11: the rear trail's recipe
                    // the front of the track scrubs in a turn
                    if (turnK > 0.2f && Random.value < turnK * 0.6f)                                   // 1.5 pass 4: one end of the track a time, thinner (was both front ends, alpha x turnK at >= 0.5 level)
                    {
                        float end = Random.value < 0.5f ? 1f : -1f;
                        DustPuff(tm, pos + lat * (sgn * (hg + 0.25f + Random.Range(-ScrubSpread, ScrubSpread))) + fw * (end * (half + 0.1f + Random.Range(-ScrubSpread, ScrubSpread))), lat * (sgn * Random.Range(0.25f, 0.5f)) + fw * (end * 0.15f), Random.Range(0.5f, 0.7f) * ScrubSizeK, lk, 0.6f * turnK * ScrubAlphaK, ScrubPale);   // 14k R11: the rear trail's recipe
                    }
                }
            }
        }

        void DustPuff(TrackMarks tm, Vector3 at, Vector3 vel, float sizeK, float lk, float alphaK, float pale = -1f, float riseK = 1f)   // 1.5 pass 6b: pale / rise per puff (the rear trail)
        {
            tm.Surface.Sample(at.x, at.z, out float y, out bool deck);
            float ds = deck ? tm.deckDust : tm.Surface.DustStrength(at.x, at.z);
            if (ds < 0.03f) return;
            float size = Random.Range(DustSizeMin, DustSizeMax) * sizeK * dustK;
            var c = Color.Lerp(tm.Surface.DustColor(at.x, at.z), DustPaleColour, pale < 0f ? DustPale : pale);
            c.a = Mathf.Clamp01(DustAlpha * ds * lk * alphaK * Random.Range(0.75f, 1.05f));
            if (c.a < 0.01f) return;
            if (OzLevelDust.BigTankPuff(new Vector3(at.x, y + 0.18f + 0.22f * size * DustGrowFrom, at.z), vel + Vector3.up * Random.Range(DustRiseMin, DustRiseMax) * riseK,
                                        Random.Range(DustLifeMin, DustLifeMax), size, c)) { DustEmitted++; OzBigTanksFx.DustPuffs++; return; }   // 1.5 review R11 (kit review_r8_r12): the levels' lit wake dust (View/OzLevelDust.cs)
            var ep = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false,
                position = new Vector3(at.x, y + 0.18f + 0.22f * size * DustGrowFrom, at.z),
                velocity = vel + Vector3.up * Random.Range(DustRiseMin, DustRiseMax) * riseK,
                startLifetime = Random.Range(DustLifeMin, DustLifeMax),
                startSize = size,
                rotation = Random.Range(0f, 360f),
                angularVelocity = Random.Range(-14f, 14f),
                startColor = c,
            };
            dust.Emit(ep, 1);
            DustEmitted++; OzBigTanksFx.DustPuffs++;
        }

        float moveK;   // 1.5 pass 4

        // ------------------------------------------------------------------ exhaust
        void Exhaust(float dt, bool moving, float s01, VehicleMotion vm, OzBigTankExhaust exh, Transform[] mouths)
        {
            if (mouths == null || mouths.Length == 0) return;
            if (smokeAcc.Length != mouths.Length) smokeAcc = new float[mouths.Length];               // once per tank (the mouths never change)
            if (!DetailKit.OnScreen(Camera.main, view.transform.position + Vector3.up * 2f, 0.25f)) return;
            float burst = exh ? exh.Burst : 0f;
            if (burst > 0.6f && burstPrev <= 0.6f) { OzBigTanksFx.SmokeBursts++; }
            if (burst > 0.05f) BurstFrames++;
            burstPrev = burst;
            float rate = (moving ? Mathf.Lerp(SmokeRateSlow, SmokeRateFull, s01) : SmokeRateIdle) + burst * SmokeRateBurst;
            float alpha = moving ? Mathf.Lerp(SmokeAlphaSlow, SmokeAlphaFull, s01) : SmokeAlphaIdle;
            alpha = Mathf.Lerp(alpha, SmokeAlphaBurst, Mathf.Clamp01(burst));
            var colour = Color.Lerp(moving ? Color.Lerp(SmokeIdle, SmokeDrive, s01) : SmokeIdle, SmokeBurst, Mathf.Clamp01(burst * 1.15f));
            var back = view.transform.forward;                                                       // models face -z: +forward is the rear
            for (int i = 0; i < mouths.Length; i++)
            {
                var m = mouths[i]; if (!m) continue;
                smokeAcc[i] += rate * dt;
                if (smokeAcc[i] > 4f) smokeAcc[i] = 4f;
                while (smokeAcc[i] >= 1f)
                {
                    smokeAcc[i] -= 1f;
                    float size = Random.Range(SmokeSizeMin, SmokeSizeMax) * (1f + 0.4f * burst) * (moving ? 1f : 0.8f);   // 1.5 last_small_fixes: fewer, larger burst puffs merge (was 1 + 0.12 burst)
                    var c = colour; c.a = Mathf.Clamp01(alpha * Random.Range(0.8f, 1.1f));
                    var ep = new ParticleSystem.EmitParams
                    {
                        applyShapeToPosition = false,
                        position = m.position + new Vector3(Random.Range(-0.04f, 0.04f), 0.05f, Random.Range(-0.04f, 0.04f)),
                        velocity = Vector3.up * Random.Range(SmokeRiseMin, SmokeRiseMax) * (1f + 0.3f * burst) + back * (0.15f + 0.4f * s01)
                                   + new Vector3(Random.Range(-0.12f, 0.12f), 0f, Random.Range(-0.12f, 0.12f)),
                        startLifetime = Random.Range(SmokeLifeMin, SmokeLifeMax) * (moving ? 1f : 0.75f),
                        startSize = size,
                        rotation = Random.Range(0f, 360f),
                        angularVelocity = Random.Range(-22f, 22f),
                        startColor = c,
                    };
                    smoke.Emit(ep, 1);
                    SmokeEmitted++; OzBigTanksFx.SmokePuffs++;
                }
            }
        }

        // ------------------------------------------------------------------ death / removal
        /// <summary>No new particle from now on; the live ones fade out over <see cref="FadeSeconds"/>, then this GameObject (both systems) is destroyed.</summary>
        public void Release()
        {
            if (released) return;
            released = true; fadeT = 0f; Releases++;
            if (dust) dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (smoke) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void LateUpdate()
        {
            if (!released) return;
            fadeT += Time.deltaTime;
            Apply();
            bool empty = (!dust || dust.particleCount == 0) && (!smoke || smoke.particleCount == 0);
            if (fadeT >= FadeSeconds || empty) { LastFadeSeconds = fadeT; Destroy(gameObject); }
        }

        void OnDestroy()
        {
            Destroyed++; Alive--;
        }
    }
}
