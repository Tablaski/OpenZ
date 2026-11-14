using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// DV146 (version 1.2), the view of the mech's hand gun (the rule: Sim/ZObject.MechHandGun.cs). Both models (v1, v2), one code. Every
    /// sim shot (ZEventType.MechHandGun) arms the PSYCHO's machine gun on the mech's right hand, drawn with the Psycho's own parts:
    /// <list type="bullet">
    /// <item>bursts: WeaponsV2Logic.MgBurst (the Psycho's burst clock, 6 burst clips, one tracer on each round onset of the clip heard), the
    /// Psycho's "mg" clips at WeaponsV2.GainMg (one per burst: the clip IS the burst's rounds), its tracer and sleeve, its muzzle flash
    /// (hot core, cone, the brake's side flares, sparks every 2nd round, a strobing light every 3rd, smoke every 4th), its impacts
    /// (hitsparks on the robot, dust / chips / ground sound on a miss) and its kill timing (BlastWave);</item>
    /// <item>the rounds leave the gun's muzzle: the node HandGunMuzzle (k86b puts it on the new v2 gun arm, under MechGunRecoil), else
    /// MechMuzzle (both models have it), read every frame after the arm is posed;</item>
    /// <item>SHELL CASINGS: one brass casing per round out of the ejection port (HandGunCasingEject, else a point on MechGunRecoil's outer
    /// side), on the game's pooled casing channel (wv2_casing: lit brass, gravity, bounce, as the Psycho's and the gatling's), with the
    /// game's casing tinkle (AudioDirector wpn_casings) on the first round of every burst and on 30 % of the others (as the Psycho's);</item>
    /// <item>the ARM: after UnitAim (which turns the upper body and the shoulder onto the rocket target) the gun (MechGunElbow) turns toward
    /// the hand gun's robot inside the sim's forward arc (<see cref="ZWorld.HandGunOutDeg"/> outward, <see cref="ZWorld.HandGunInDeg"/>
    /// inward, + <see cref="ArcSlackDeg"/> for the upper body's slew; pitch within <see cref="PitchMaxDeg"/>), eased in and back to rest;
    /// a slight eased wobble while a burst runs (<see cref="WobbleYawDeg"/> / <see cref="WobblePitchDeg"/>) and a small slide back per round.</item>
    /// </list>
    /// View only: the sim decides every shot, its target and its hit; nothing here changes them. With the rule off ('Zod exact') no event
    /// arrives and nothing is drawn. Switch: <see cref="Enabled"/> (off = the shots stay in the sim, undrawn).
    /// </summary>
    [DefaultExecutionOrder(64)]   // after UnitAim (62) posed the upper body and the shoulder; before BoardingView (70) and MechWalk (79)
    public sealed partial class MechHandGunView : MonoBehaviour                                    // k112, item 158: + View/MechHandGunView.Reach.cs (each model's own reach)
    {
        public static bool Enabled = true;
        public static MechHandGunView I { get; private set; }
        /// <summary>Degrees the drawn gun may turn past the sim's arc (the upper body slews onto its target, it does not snap); pitch limit
        /// (deg) from the shoulder's aim; the arm's easing (s) toward a target and back to rest; the burst wobble (deg) and its rate (Hz);
        /// the slide back per round (model units) and the arm's hold after the last shot (s).</summary>
        public static float ArcSlackDeg = 3f, PitchMaxDeg = 30f, AimTau = 0.07f, RestTau = 0.30f, WobbleYawDeg = 1.2f, WobblePitchDeg = 0.9f,
                            WobbleHz = 7.3f, SlideM = 0.035f, HoldS = 0.6f, FlashScale = 1.2f, CasingScale = 1.25f;
        /// <summary>The ejection port when the model has no HandGunCasingEject node: in MechGunRecoil's space (model units), the outer side
        /// (-x: the mech's right), a little up, part of the way to the muzzle.</summary>
        public static Vector3 EjectFallback = new Vector3(-0.18f, 0.06f, 0f);
        public const string MuzzleNode = "HandGunMuzzle", EjectNode = "HandGunCasingEject";
        const int Emitter = 1 << 28;

        /// <summary>Proof counters (since load): sim shots seen, bursts started, bursts with their MG clip played, rounds (tracers) drawn,
        /// casings thrown, casing tinkles played, killing rounds drawn at once, shots whose target is no robot (must stay 0), frames the arm was held at an arc limit, shots seen whose
        /// robot lay outside the drawn arm's reach (must stay 0), mechs bound, of those with their own HandGunMuzzle / HandGunCasingEject.</summary>
        public static int ShotsSeen, ShotsNotAtRobot, Bursts, BurstSounds, Rounds, Casings, Tinkles, KillRounds, ClampFrames, OutOfReach, Bound, OwnMuzzle, OwnEject, ReachHeld, ReachCut;   // k102: ReachHeld = sim shots not drawn while the arm could not reach the robot, ReachCut = bursts stopped for it
        /// <summary>k102: the switch: a sim shot at a robot the drawn arm cannot reach yet (the upper body still turning) is not drawn (off = drawn as before).</summary>
        public static bool ReachHold = true;
        /// <summary>Proof hook: every sim shot the view reads (the event, its target).</summary>
        public static event System.Action<ZEvent, ZObject> Seen;
        /// <summary>Proof: the largest drawn arm turn seen (deg, + outward / - inward) and the largest pitch (deg).</summary>
        public static float YawMaxOut, YawMaxIn, PitchMaxSeen;

        sealed class Gun
        {
            public int id; public UnitView view; public Transform elbow, recoil, muzzle, eject; public Quaternion elbowRest;
            public Vector3 dirE, muzzleE, backL, slideAdded, slideWritten; public bool ok;
            public float yaw, pitch, yawVel, pitchVel, wob, phase, lastShotAt = -10f, lastRoundAt = -10f;
            public WeaponsV2Logic.MgBurst mg; public int lastTarget = -1, round; public Vector3 lastTo, lastOffset; public bool kill;
            public float reachOut, reachIn, pitchMax; public bool ownArm;                                 // k112, item 158: this model's reach (View/MechHandGunView.Reach.cs)
        }
        struct Hit { public float at; public Vector3 pos, dir; public bool hit, anchored; public FxSurface surf; public HitSurface.Anchor anc; }
        readonly Dictionary<int, Gun> guns = new Dictionary<int, Gun>();
        readonly List<Hit> hits = new List<Hit>(128);
        readonly List<int> drop = new List<int>();
        ZGame game; bool tracerCh, casingCh;
        static readonly string[][] MgClipNames = BuildMgNames();
        static string[][] BuildMgNames() { var a = new string[WeaponsV2Logic.MgClips.Length][]; for (int i = 0; i < a.Length; i++) a[i] = new[] { WeaponsV2Logic.MgClips[i].name }; return a; }

        // ------------------------------------------------------------------ bootstrap
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded; }
        static void OnSceneLoaded(Scene s, LoadSceneMode m) { if (ZGame.I || FindFirstObjectByType<ZGame>()) Ensure(); }

        /// <summary>The scene's hand-gun view (created on demand).</summary>
        public static MechHandGunView Ensure()
        {
            if (I && I.isActiveAndEnabled) return I;
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!g) return null;
            var go = new GameObject("MechHandGunView");
            if (g.gameObject.scene.IsValid() && go.scene != g.gameObject.scene) SceneManager.MoveGameObjectToScene(go, g.gameObject.scene);
            var v = go.AddComponent<MechHandGunView>(); v.game = g;
            return v;
        }

        void Awake()
        {
            if (I && I != this) { Destroy(gameObject); return; }
            I = this;
            tracerCh = System.Array.Exists(OpenZFxCatalog.Channels, c => c.name == "wv2_mg_tracer");
            casingCh = System.Array.Exists(OpenZFxCatalog.Channels, c => c.name == "wv2_casing");
        }

        void OnDestroy() { if (I == this) I = null; }

        float Speed => game ? Mathf.Max(0.25f, game.gameSpeed) : 1f;
        static float Recoil(float age) => age < 0 || age > 1 ? 0 : (1 - Mathf.Exp(-age / 0.018f)) * Mathf.Exp(-age / 0.23f) * 1.5f;   // UnitAim / UnitView's recoil envelope

        static Transform Deep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Deep(c, name); if (f) return f; }
            return null;
        }

        Gun GunFor(int id)
        {
            if (guns.TryGetValue(id, out var g) && g.view) return g;
            if (!game || !game.Views.TryGetValue(id, out var v) || !v) return null;
            g = new Gun { id = id, view = v, mg = new WeaponsV2Logic.MgBurst(id * 97 + 11), phase = (id * 0.6180339f) % 1f * 6.2831853f };
            g.elbow = Deep(v.transform, "MechGunElbow"); g.recoil = Deep(v.transform, "MechGunRecoil");
            var own = Deep(v.transform, MuzzleNode); g.muzzle = own ? own : Deep(v.transform, "MechMuzzle");
            g.eject = Deep(v.transform, EjectNode);
            g.ok = g.elbow && g.elbow.parent && g.muzzle;
            if (g.ok)
            {   // the arm at rest (nothing but this file turns MechGunElbow): the barrel's direction and the muzzle in the elbow's space
                g.elbowRest = g.elbow.localRotation;
                g.dirE = g.elbow.InverseTransformDirection(g.muzzle.TransformDirection(Vector3.back)).normalized;   // sockets fire along local -z
                g.muzzleE = g.elbow.InverseTransformPoint(g.muzzle.position);
                if (g.recoil && g.recoil.parent) g.backL = g.recoil.parent.InverseTransformDirection(-g.muzzle.TransformDirection(Vector3.back)).normalized;
                Bound++; if (own) OwnMuzzle++; if (g.eject) OwnEject++;
                SetReach(g, own);                                                                          // k112, item 158: v1's arm its own measured reach, an own gun arm the rule's arc (View/MechHandGunView.Reach.cs)
            }
            guns[id] = g;
            return g;
        }

        // ------------------------------------------------------------------ sim events (Update: CombatView clears them in its LateUpdate)
        void Update()
        {
            if (!game) game = ZGame.I;
            if (!game || game.World == null) return;
            foreach (var kv in guns)
            {   // the arm back at rest before UnitAim (62) solves the upper body and the shoulder on the real muzzle this frame (LateUpdate):
                // its solve must never see last frame's hand-gun turn (it would turn the upper body against it); LateUpdate turns it again
                var gg = kv.Value;
                if (gg.ok && gg.elbow) gg.elbow.localRotation = gg.elbowRest;
                if (gg.recoil && gg.recoil.localPosition == gg.slideWritten) gg.recoil.localPosition -= gg.slideAdded;   // and the slide off (UnitAim writes the node anew)
                gg.slideAdded = Vector3.zero; if (gg.recoil) gg.slideWritten = gg.recoil.localPosition;
            }
            if (!Enabled) return;
            var ev = game.World.Events;
            float now = Time.time;
            for (int i = 0; i < ev.Count; i++)
            {
                var e = ev[i];
                if (e.type != ZEventType.MechHandGun) continue;
                var g = GunFor(e.objId); if (g == null || !g.ok) continue;
                var t = game.World.GetAny(e.otherId);
                ShotsSeen++; if (t == null || t.Kind != ZKind.Robot) ShotsNotAtRobot++;
                Seen?.Invoke(e, t);
                bool newTarget = g.lastTarget != e.otherId;
                g.lastShotAt = now; g.lastTarget = e.otherId;                                               // the arm turns toward it in any case
                g.lastTo = AimPosOf(t);
                if (newTarget) g.lastOffset = Vector3.zero;
                bool reach = t == null || InReach(g, t);
                if (!reach && ReachHold) { ReachHeld++; if (e.flag2) ReachKillHeld++; continue; }          // k102: the arm cannot point at it: no round from a gun that points elsewhere (the sim's shot counts all the same); k112, item 158: the killing round too (the robot falls on its own time)
                g.mg.Arm(now);
                if (e.flag2) g.kill = true;                                                                 // the killing round shows at once, as a hit
                if (!reach) OutOfReach++;
            }
        }

        Vector3 AimPosOf(ZObject t)
        {
            if (t == null) return Vector3.zero;
            if (game.Views.TryGetValue(t.Id, out var v) && v && v.isActiveAndEnabled) return v.AimPos();
            return game.CenterWorld(t) + Vector3.up;
        }

        /// <summary>The drawn barrel's direction with the arm at rest (UnitAim's upper body and shoulder this frame) and the muzzle there.</summary>
        void Rest(Gun g, out Quaternion restRot, out Vector3 f0, out Vector3 m0)
        {
            restRot = g.elbow.parent.rotation * g.elbowRest;
            f0 = restRot * g.dirE;
            m0 = g.elbow.position + restRot * Vector3.Scale(g.muzzleE, g.elbow.lossyScale);
        }

        /// <summary>The turn (deg, + outward = clockwise from above = the mech's right) and the climb (deg) the gun needs from rest to aim at p.</summary>
        static void Need(Vector3 f0, Vector3 m0, Vector3 p, out float yaw, out float pitch)
        {
            var d = p - m0;
            var a = new Vector3(f0.x, 0f, f0.z); var b = new Vector3(d.x, 0f, d.z);
            yaw = a.sqrMagnitude > 1e-6f && b.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(a, b, Vector3.up) : 0f;
            pitch = (90f - Vector3.Angle(Vector3.up, d)) - (90f - Vector3.Angle(Vector3.up, f0));
        }

        bool InReach(Gun g, ZObject t)
        {
            if (!g.ok || !g.elbow) return true;
            return ReachesK112(g, AimPosOf(t));                                                      // k112, item 158: this model's own reach, turn and climb, solved on the elbow as the pose is (View/MechHandGunView.Reach.cs)
        }

        // ------------------------------------------------------------------ per frame: pose, bursts, rounds, impacts
        void LateUpdate()
        {
            if (!game) { game = ZGame.I; if (!game) return; }
            if (game.World == null) return;
            float now = Time.time, dt = Time.deltaTime;
            drop.Clear();
            foreach (var kv in guns)
            {
                var g = kv.Value;
                if (!g.view || g.view.Obj == null) { drop.Add(kv.Key); continue; }
                if (!g.ok || !g.view.isActiveAndEnabled || g.view.Dying || g.view.Reacting) { g.mg.ArmedUntil = -1f; g.mg.Clip = -1; g.kill = false; continue; }
                if (QuietGun(g)) continue;                                                                    // vehicle_exit_jump part B: nobody inside the mech: its burst and clip stop, the arm stays put (View/MechHandGunView.Quiet.cs)
                Pose(g, now, dt);
                if (!Enabled) continue;
                var t = game.World.GetAny(g.lastTarget);
                if (ReachHold && !g.kill && g.mg.InBurst && t != null && !InReach(g, t)) { g.mg.ArmedUntil = -1f; g.mg.Clip = -1; ReachCut++; continue; }   // k102: the robot left the arm's reach mid-burst: the burst stops
                if (g.kill && !MechHandGunAim.KillOn(g.muzzle.position, (g.elbow.rotation * g.dirE).normalized, g.lastTo)) g.kill = false;   // the killing round only along the arm (View/MechHandGunAim.cs)
                if (g.kill) { g.kill = false; Round(g, now, t, true); KillRounds++; }
                int started = -1, due = 0;
                if (MechHandGunAim.MayStep(g.id, g.mg, g.lastTarget, now)) started = g.mg.Step(now, out due);   // a burst only with the barrel on its robot (View/MechHandGunAim.cs)
                if (started >= 0)
                {
                    Bursts++; g.round = 0;
                    var au = CombatAudio.I;
                    if (au) { au.PlayPitched("mg", g.muzzle.position, WeaponsV2.GainMg, 0f, Emitter + g.id, 1f, MgClipNames[started]); BurstSounds++; }
                    MechHandGunAim.BurstSound(g.id, MgClipNames[started][0], g.muzzle.position);                  // its voice (a burst stopped for a new robot stops its clip)
                }
                for (int i = 0; i < due; i++) if (MechHandGunAim.RoundOn(g.id)) Round(g, now, t, false);      // no round off the arm
            }
            foreach (var id in drop) guns.Remove(id);
            StepHits(now);
        }

        void Pose(Gun g, float now, float dt)
        {
            Rest(g, out var restRot, out var f0, out var m0);
            var t = game.World.GetAny(g.lastTarget);
            bool aim = MechHandGunAim.AimPoint(game, g.id, g.view.Obj, g.lastTarget, g.mg.InBurst, g.lastTo, now, out var aimP) && Enabled;   // the robot the sim chose (View/MechHandGunAim.cs)
            float wantY = 0f, wantP = 0f, outL = g.reachOut, inL = g.reachIn, pm = g.pitchMax;              // k112, item 158: this model's own reach (before: the rule's arc + ArcSlackDeg and PitchMaxDeg for every model)
            if (aim)
            {
                MechHandGunAim.Need(g.elbow.position, f0, m0, aimP, out wantY, out wantP);   // the barrel's line through it, solved on the elbow (View/MechHandGunAim.cs)
                float cy = Mathf.Clamp(wantY, -inL, outL), cp = Mathf.Clamp(wantP, -pm, pm);
                if (cy != wantY || cp != wantP) ClampFrames++;
                wantY = cy; wantP = cp;
            }
            MechHandGunAim.Turn(ref g.yaw, ref g.yawVel, ref g.pitch, ref g.pitchVel, wantY, wantP, dt, Speed);   // at the sim arm's turn rate, a little faster (View/MechHandGunAim.cs)
            g.wob = Mathf.MoveTowards(g.wob, g.mg.InBurst ? 1f : 0f, dt / (g.mg.InBurst ? 0.06f : 0.15f));   // eased in with the burst, out after it
            float w = Smooth(g.wob), ph = now * WobbleHz * 6.2831853f + g.phase;
            float ya = Mathf.Clamp(g.yaw + w * WobbleYawDeg * Mathf.Sin(ph), -inL, outL);
            float pa = Mathf.Clamp(g.pitch + w * WobblePitchDeg * Mathf.Sin(ph * 0.71f + 1.3f), -pm, pm);
            if (ya > YawMaxOut) YawMaxOut = ya; if (-ya > YawMaxIn) YawMaxIn = -ya; if (Mathf.Abs(pa) > PitchMaxSeen) PitchMaxSeen = Mathf.Abs(pa);
            var flat = new Vector3(f0.x, 0f, f0.z); var h = Vector3.Cross(Vector3.up, flat.sqrMagnitude > 1e-6f ? flat.normalized : g.view.transform.forward);
            var R = Quaternion.AngleAxis(ya, Vector3.up) * Quaternion.AngleAxis(-pa, h.sqrMagnitude > 1e-6f ? h.normalized : Vector3.right);   // + pitch raises the barrel
            g.elbow.rotation = R * restRot;
            MechHandGunAim.NoteOff(g.id, g.muzzle.position, (g.elbow.rotation * g.dirE).normalized, aim, aimP);   // how far the barrel points off its robot
            // the slide back per round, on top of UnitAim's own recoil of this node (it writes the node absolute every frame; taken off
            // again here when nobody else wrote it since, so it never adds up)
            if (g.recoil)
            {
                if (g.recoil.localPosition == g.slideWritten) g.recoil.localPosition -= g.slideAdded;
                g.slideAdded = g.backL * (Recoil(now - g.lastRoundAt) * SlideM);
                g.recoil.localPosition += g.slideAdded; g.slideWritten = g.recoil.localPosition;
            }
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        void Round(Gun g, float now, ZObject t, bool kill)
        {
            var z = OpenZFxPlayer.I; if (!z || !z.isActiveAndEnabled) return;
            bool alive = t != null && !t.Destroyed && t.KillAt < 0;
            var from = g.muzzle.position;
            var dir = (g.elbow.rotation * g.dirE).normalized;
            var aimAt = alive ? AimPosOf(t) + g.lastOffset : g.lastTo;
            float chance = game.World.S.Unit(ZWorld.HandGunType) is ZUnitSettings u ? (float)u.attack_damage_chance : 0.65f;
            bool hit = kill || (alive && Random.value < chance);                                           // cosmetic rounds on the Psycho's hit chance; the sim's roll decides the damage
            Vector3 to;
            if (kill) to = g.lastTo;
            else if (hit)
            {
                to = aimAt + Random.insideUnitSphere * 0.28f;
                if (game.Views.TryGetValue(t.Id, out var tv) && tv && HitSurface.OnBody(tv.transform, from, to, out var hp, out _)) to = hp;
            }
            else
            {
                var off = Random.insideUnitCircle.normalized * Random.Range(0.7f, 2.2f);
                to = HitSurface.MissGround(from, aimAt + new Vector3(off.x, 0f, off.y));
            }
            if (ShotLine.Stop(from, to, t, out var wall)) { to = wall; hit = false; }                  // bullets_straight: it ends on what its line meets (View/ShotLine.cs)
            var d = to - from; float dist = d.magnitude; if (dist < 0.1f) return;
            var sd = d / dist;
            float speed = LegacyMatch.ShotSpeed(Speed) * WeaponsV2Runner.MgTracerMul;
            float start = Mathf.Min(1.1f * 0.5f + 0.1f, dist * 0.5f), life = WeaponsV2Runner.HeadTime(ZWorld.HandGunType, dist, Speed);
            life = z.EmitRound(tracerCh ? "wv2_mg_tracer" : "leg_tracer", "leg_tracer_sleeve", from, to, start, speed, life, 0.075f, WeaponsV2Runner.MgTracer, 1f, 0.2f, WeaponsV2Runner.MgSleeve, 0.22f);
            if (kill) BlastWave.NoteDrawn(g.id, t, life);
            // the Psycho's muzzle flash (WeaponsV2Runner.MgRound) at the mech gun's mouth, sized to the mouth, in front of it (View/MechHandGunAim.cs)
            g.round++;
            MechHandGunAim.Flash(g.id, g.round, z, from, dir, aimAt, g.muzzle, g.recoil, now);
            Casing(g, z, from, dir);
            g.lastRoundAt = now;
            hits.Add(Anc(new Hit { at = now + life, pos = to, dir = sd, hit = hit, surf = hit && t != null ? FxSurface.Unit : z.SurfaceAt(to) }));
            Rounds++;
        }

        static Hit Anc(Hit h) { if (HitSurface.Claim(h.pos, out var a)) { h.anchored = true; h.anc = a; } return h; }

        /// <summary>One brass casing out of the port on the side of the bore it really sits on (as the Psycho's), and the game's tinkle.</summary>
        void Casing(Gun g, OpenZFxPlayer z, Vector3 bore, Vector3 dir)
        {
            Vector3 port;
            if (g.eject) port = g.eject.position;
            else if (g.recoil) { var e = EjectFallback; e.z = g.recoil.InverseTransformPoint(g.muzzle.position).z * 0.5f; port = g.recoil.TransformPoint(e); }
            else port = bore - dir * 0.5f;
            var gunRight = Vector3.Cross(Vector3.up, dir); gunRight = gunRight.sqrMagnitude > 1e-6f ? gunRight.normalized : g.view.transform.right;
            float side = Vector3.Dot(port - bore, gunRight) < -0.01f ? -1f : 1f;
            var right = gunRight * side;
            var v = right * Random.Range(1.6f, 2.6f) + Vector3.up * Random.Range(1.4f, 2.4f) - dir * Random.Range(0.1f, 0.6f) + Random.insideUnitSphere * 0.3f;
            z.EmitChannel(casingCh ? "wv2_casing" : "m12_metal", port, v, Random.Range(1.8f, 2.6f), 0.26f * CasingScale, Color.white, 1f);
            Casings++;
            var ad = AudioDirector.I;
            if (ad && (g.round == 1 || Random.value < 0.3f) && ad.Play("wpn_casings", port, Emitter + g.id)) Tinkles++;   // AudioDirector's psycho / gatling brass (the first of each burst always)
        }

        void StepHits(float now)
        {
            if (hits.Count == 0) return;
            var z = OpenZFxPlayer.I;
            for (int i = 0; i < hits.Count; i++)
            {
                var h = hits[i]; if (now < h.at) continue;
                hits.RemoveAt(i--);
                if (!z || !z.isActiveAndEnabled) continue;
                if (h.anchored && HitSurface.Now(h.anc, out var np, out _)) h.pos = np;
                if (h.surf == FxSurface.Water) { var w = new Vector3(h.pos.x, z.SurfaceY(h.pos), h.pos.z); z.Droplets(w, 0.15f, 3); z.Ripple(w, 0.16f); continue; }
                if (OpenZHitSparks.Active)
                {   // as the Psycho's rounds (WeaponsV2Runner.MgImpact)
                    if (h.hit && h.surf == FxSurface.Unit) { OpenZHitSparks.Burst(h.pos, h.dir, FxWeapon.Bolt, h.surf, 0.8f, Random.value < 0.35f); continue; }
                    if (!h.hit) OzSoundV3.GroundImpact(h.pos, h.surf);
                    if (h.surf == FxSurface.Road || h.surf == FxSurface.Rock) OpenZHitSparks.Chips(h.pos, h.surf);
                    z.Dust(h.pos, 0.12f);
                    continue;
                }
                int n = h.hit ? 3 : 2;
                for (int k = 0; k < n; k++)
                {
                    var v = Random.onUnitSphere * Random.Range(1.5f, 3.8f); v.y = Mathf.Abs(v.y) + 0.7f;
                    z.EmitChannel("leg_impact_spark", h.pos, v, Random.Range(0.12f, 0.26f), 0.1f, k == 0 ? OpenZFxCatalog.ImpactSparkHot : new Color(1f, 0.7f, 0.31f), 1f);
                }
                if (h.hit) z.EmitChannel("m12_core", h.pos, Vector3.zero, 0.05f, 0.2f, OpenZFxCatalog.M12Core.color, 1f);
                else z.Dust(h.pos, 0.12f);
            }
        }

        /// <summary>Proof: one line of the counters.</summary>
        public static string Stats =>
            $"[MechHandGunView] mechs bound {Bound} (own HandGunMuzzle {OwnMuzzle}, own HandGunCasingEject {OwnEject}); sim shots seen {ShotsSeen} (at no robot {ShotsNotAtRobot}); bursts {Bursts}, with their MG clip {BurstSounds}; " +
            $"rounds drawn {Rounds}, casings {Casings}, tinkles {Tinkles}, killing rounds {KillRounds}; arm turned up to {YawMaxOut:0.0} deg outward / {YawMaxIn:0.0} inward, pitch {PitchMaxSeen:0.0}; " +
            $"frames held at a limit {ClampFrames}; shots at a robot out of the arm's reach {OutOfReach} (drawn; not drawn while the arm could not point at the robot {ReachHeld}, of them killing rounds {ReachKillHeld}, bursts stopped for it {ReachCut}); {ReachText}";

        public static void ResetStats() { ShotsSeen = ShotsNotAtRobot = Bursts = BurstSounds = Rounds = Casings = Tinkles = KillRounds = ClampFrames = OutOfReach = ReachHeld = ReachKillHeld = ReachCut = 0; YawMaxOut = YawMaxIn = PitchMaxSeen = 0f; }
    }
}
