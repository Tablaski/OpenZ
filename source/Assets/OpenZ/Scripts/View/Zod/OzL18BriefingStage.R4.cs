// OpenZ (GPL-3.0) - version 1.5, level 18 "Bridge Game": ROUND 4 of the two level-start briefings (the owner's notes of 12 Nov evening,
// kit level18_briefings/kit_r4; rows of kits/level18/OWNER_NOTES_TRACKER_20261112.md):
//  - 15 / 29  his own march sounds: A from the briefing's first frame, B 1 s later, both once, quiet under the voice, cut dead at the first
//             blast; they replace the boots loop and the track clatter of the parade's bed (the engines under them: OwnerBedEngines).
//  - 33       the blasts' sound goes on about 3 s longer under the fly-by after them: a second rumble, debris, a steel groan and a few smaller
//             secondary bangs (each with a small fire pop on a burning stump), riding their own softer duck under BS-01's line.
//  - 38 / 38b the red defenders of the blue briefing fire short uneven bursts at the parade (more as the columns come nearer), recoil and
//             small aim corrections between shots; from about video 0:07 a few of the parade shoot back (the jeeps' guns, three robots, one
//             tank turret turning and firing once). The game's own shot effects and sounds; nothing is hit for real, nobody dies of it.
//  - 34       the smoke against the rock and the far land: the stage's other smoke (the stump pillars, the steam, the ruins' leg_smoke puffs)
//             takes the same soft fade into what it meets as the blasts' channels, the game camera keeps its depth texture on while a
//             briefing flies, and the red list's flights take the far-land-as-sky rule too (Look.cs FarTick: FarRedAll).
//  - 40       a subtle sun lens flare toward the drawn low sun (URP data-driven lens flare, depth occlusion + the stage's smoke in front of it).
// WRITTEN BY READING ONLY: nothing of this file has run in the editor yet. Every number is a static: tuned in Play, no rebuild.
// STAGING ONLY: no rule, no Sim file, no unit of the game is touched.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ
{
    public sealed partial class OzL18BriefingStage
    {
        // ================================================================================================ the frame (LateUpdate, after TickDefence)
        void R4Tick(float t, bool forward, float duck)
        {
            if (!built) return;
            if (blue && Parades && hasPress) { FirefightTick(t, forward); TailTick(t, forward); }
            SoftTick();
            FlareTick();
        }

        /// <summary>The hand-back, a skip, the scene going: every round 4 thing goes quiet and the borrowed numbers go back.</summary>
        void R4Release(string why)
        {
            OwnerMarchStop(why);
            foreach (var s in tailSrc) if (s.src) { s.src.Stop(); Destroy(s.src); }
            tailSrc.Clear();
            fireRed.Clear(); fireBlue.Clear(); fireBuilt = false; turretCar = null;
            SoftOff(); FlareOff();
        }

        // ================================================================================================ 29: the owner's march
        public const string OwnerMarchA = "brief_l18_army_march_a", OwnerMarchB = "brief_l18_army_march_b";
        /// <summary>
        /// both sit about 13 LU under EvilZod's lines in the open, B about 3 LU under A), B's start after A (s), the fade in (s), the parade's
        /// engines under them (0 = his march alone; 1 = the old engines bed as well). OwnerMarch false = the old bed (boots, tracks, engines).</summary>
        public static bool OwnerMarch = true;
        public static float OwnerMarchGainA = 0.71f, OwnerMarchGainB = 0.72f, OwnerMarchBAfter = 1.0f, OwnerMarchFadeIn = 0.15f, OwnerBedEngines = 0f;
        /// <summary>A's start after the flight's first frame (s). 0 = his words ("A from the first frame"): each file is 7.4 s and fades out by
        /// itself, so A ends 1.5 s and B 0.5 s before the first blast (stage clock -0.21 / 0.79 against 1.30); 1.5 = both run into the blast.</summary>
        public static float OwnerMarchADelay = 0f;
        AudioSource ownA, ownB; float ownATau = float.NaN, ownBTau = float.NaN; bool ownCut, ownWarned;
        /// <summary>Play checks: the owner's clips started (2 wanted a flight), the stage clock of the cut against the first blast's.</summary>
        public static int OwnerMarchPlays; public static float OwnerMarchCutTau = float.NaN, OwnerMarchFirstBlastTau = float.NaN;

        /// <summary>TickLoops, each frame of the blue bed: true = his march plays this flight (the boots and the tracks stay off).</summary>
        bool OwnerMarchTick(bool bed, bool boom, float duckK, float bus)
        {
            if (!bed || !OwnerMarch || !Sounds) return false;
            var a = Clip(OwnerMarchA); var b = Clip(OwnerMarchB);
            if (!a || !b)
            {
                if (!ownWarned) { ownWarned = true; Warn("the briefing march Resources/" + SfxRes + OwnerMarchA + " / " + OwnerMarchB + " is missing: the old bed plays"); OzSoundLog.Add("L18B", "brief_l18_owner_march", null, "DROP clip missing"); }
                return false;
            }
            if (boom)
            {
                if (float.IsNaN(OwnerMarchFirstBlastTau)) { float b0 = float.MaxValue; foreach (var s in spans) if (s.fired) b0 = Mathf.Min(b0, s.blastAt); OwnerMarchFirstBlastTau = b0; }
                if ((ownA || ownB) && duckK <= 0.001f) { OwnerMarchCutTau = tau; OwnerMarchStop("the bridges go: cut dead"); }
                ownCut = true;
            }
            else
            {
                if (!ownA && !ownCut && float.IsNaN(ownATau) && tau >= -(BlueOpen + BluePressClock) + OwnerMarchADelay - 0.001f) { ownA = OwnerOneShot("brief_l18_owner_march_a", a); ownATau = tau; }
                if (!ownB && !ownCut && !float.IsNaN(ownATau) && float.IsNaN(ownBTau) && tau >= ownATau + OwnerMarchBAfter) { ownB = OwnerOneShot("brief_l18_owner_march_b", b); ownBTau = tau; }
            }
            if (ownA) ownA.volume = OwnerMarchGainA * bus * duckK * Mathf.Clamp01((tau - ownATau) / Mathf.Max(0.01f, OwnerMarchFadeIn));
            if (ownB) ownB.volume = OwnerMarchGainB * bus * duckK * Mathf.Clamp01((tau - ownBTau) / Mathf.Max(0.01f, OwnerMarchFadeIn));
            return true;
        }

        AudioSource OwnerOneShot(string id, AudioClip clip)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false; src.spatialBlend = 0f; src.dopplerLevel = 0f; src.bypassReverbZones = true; src.priority = 64; src.loop = false; src.clip = clip; src.volume = 0f; src.Play();
            SoundsStarted++; OwnerMarchPlays++; OzSoundLog.Add("L18B", id, clip.name, "PLAY 2D briefing " + shotNow + " (the briefing march, once)");
            return src;
        }

        void OwnerMarchStop(string why)
        {
            if (ownA) { ownA.Stop(); OzSoundLog.Add("L18B", "brief_l18_owner_march_a", ownA.clip ? ownA.clip.name : null, "STOP (" + why + ")"); Destroy(ownA); }
            if (ownB) { ownB.Stop(); OzSoundLog.Add("L18B", "brief_l18_owner_march_b", ownB.clip ? ownB.clip.name : null, "STOP (" + why + ")"); Destroy(ownB); }
            ownA = ownB = null;
            if (why == "the bridges go: cut dead") return;                                            // a flight that goes on: never again in it
            ownATau = ownBTau = float.NaN; ownCut = false;
        }

        // ================================================================================================ 33: the blasts' tail under the fly-by
        /// <summary>The cues after the LAST bridge's blast (s after it): file, gain, pitch, the stump that pops with it (-1 none; else the span
        /// west to east, its south end). Owner addition 33.</summary>
        public static bool BlastTail = true;
        sealed class TailCue { public float at, gain, pitch; public string file; public int pop; }
        static readonly TailCue[] TailCues =
        {
            new TailCue { at = 1.6f, file = "sd_nuke_rumble_tail", gain = 0.26f, pitch = 0.86f, pop = -1 },
            new TailCue { at = 1.9f, file = "explosion-v3-20260909", gain = 0.20f, pitch = 1.12f, pop = 0 },
            new TailCue { at = 2.3f, file = "sd_debris_rain_b", gain = 0.16f, pitch = 0.95f, pop = -1 },
            new TailCue { at = 2.7f, file = "sd_structure_groan_b", gain = 0.20f, pitch = 0.88f, pop = -1 },
            new TailCue { at = 3.1f, file = "explosion-v1-20260909", gain = 0.16f, pitch = 1.25f, pop = 2 },
            new TailCue { at = 3.5f, file = "v3-tank-impact-b", gain = 0.14f, pitch = 0.92f, pop = 1 },
            new TailCue { at = 3.9f, file = "explosion-v3-20260909", gain = 0.12f, pitch = 1.35f, pop = 0 },
        };
        /// <summary>The tail's own duck: this share of the lines' duck (the stage's other sounds take all of it and the TailLow fall), the stage
        /// clock where it fades out before EvilZod's "Well, EXACTLY!" (tau 7.24), the secondary pops' size (OpenZFxPlayer.BlastLegacy strength).</summary>
        public static float TailDuckShare = 0.55f, TailFadeFrom = 6.3f, TailFadeTo = 7.0f, TailPopStrength = 0.55f;
        /// <summary>Play checks: tail cues played, pops shown.</summary>
        public static int TailCuesPlayed, TailPops;
        sealed class TailSrc { public AudioSource src; public float gain; }
        readonly List<TailSrc> tailSrc = new List<TailSrc>();

        void TailTick(float t, bool forward)
        {
            if (!BlastTail || spans.Count == 0) return;
            float last = float.MinValue; foreach (var s in spans) last = Mathf.Max(last, s.blastAt);
            for (int i = 0; i < TailCues.Length; i++)
            {
                var c = TailCues[i];
                if (!Crossed(last + c.at, forward)) continue;
                if (Sounds)
                {
                    var clip = Clip(c.file);
                    if (!clip) OzSoundLog.Add("L18B", "brief_l18_tail_" + i, null, "DROP clip missing (" + c.file + ")");
                    else
                    {
                        var src = gameObject.AddComponent<AudioSource>();
                        src.playOnAwake = false; src.spatialBlend = 0f; src.dopplerLevel = 0f; src.bypassReverbZones = true; src.priority = 62; src.loop = false; src.clip = clip; src.pitch = c.pitch; src.volume = 0f; src.Play();
                        tailSrc.Add(new TailSrc { src = src, gain = c.gain }); SoundsStarted++; TailCuesPlayed++;
                        OzSoundLog.Add("L18B", "brief_l18_tail_" + i, clip.name, "PLAY 2D briefing " + shotNow + " (the blasts' tail)");
                    }
                }
                if (c.pop >= 0 && c.pop < spans.Count)
                {
                    var z = OpenZFxPlayer.I; uint sd = (uint)(5021 + i * 97);
                    var at = StumpPoint(spans[c.pop], false) + new Vector3((Rnd(ref sd) - 0.5f) * 3f, 0.6f, (Rnd(ref sd) - 0.5f) * 2f);
                    if (z && z.isActiveAndEnabled) { z.BlastLegacy(at, TailPopStrength); TailPops++; }
                }
            }
            float lineDuck = LineDuck(BlueLines, BlueDuck, t + BluePressClock);
            float k = Mathf.Lerp(1f, lineDuck, TailDuckShare) * (1f - Mathf.SmoothStep(0f, 1f, (t - TailFadeFrom) / Mathf.Max(0.05f, TailFadeTo - TailFadeFrom))), bus = Bus();
            for (int i = tailSrc.Count - 1; i >= 0; i--)
            {
                var s = tailSrc[i];
                if (!s.src || !s.src.isPlaying) { if (s.src) Destroy(s.src); tailSrc.RemoveAt(i); continue; }
                s.src.volume = s.gain * bus * k;
            }
        }

        // ================================================================================================ 38 / 38b: the firefight before the blasts
        /// <summary>Owner additions 38 / 38b. The red lines' fire: from this many seconds after the flight's first frame, bursts of 2 to 3 shots
        /// (RedShotGap .. + RedShotSpread apart), the pause after a burst from RedPauseFar (columns far) down to RedPauseNear (columns at the
        /// far end), plus up to RedPauseSpread. The parade's answer from the stage clock BlueFireFrom (about video 0:07) to the first blast.
        /// Gains of the shots (2D, under the voice's duck like every stage sound, thinner with the lens's distance: ShotNear .. ShotFar m),
        /// the least time between two shot sounds (s). Recoil (m back, deg up, s), the aim's life (deg) and its turn after the nearest hull (deg).</summary>
        public static bool Firefight = true;
        public static float RedFireFrom = 6.45f, RedShotGap = 0.13f, RedShotSpread = 0.08f, RedPauseFar = 2.6f, RedPauseNear = 0.9f, RedPauseSpread = 0.9f;
        public static float BlueFireFrom = -1.15f, RedShotGain = 0.10f, BlueShotGain = 0.12f, TankShotGain = 0.16f, ShotNear = 35f, ShotFar = 160f, ShotSoundGap = 0.06f;
        public static float RecoilBack = 0.035f, RecoilUp = 3.5f, RecoilTau = 0.07f, AimLife = 2.2f, AimFollow = 10f, TurretTurnSeconds = 0.6f, TurretMaxTurn = 40f;
        static readonly string[] RifleBank = { "v3-rifle-single-a", "v3-rifle-single-b", "v3-rifle-single-c" };
        /// <summary>Play checks: shots fired by the red lines and by the parade, shot sounds played.</summary>
        public static int RedShots, BlueShots, ShotSounds;
        sealed class FireAt { public float t; public int who; public int kind; }                      // kind: 0 rifle (red), 1 rifle (blue robot), 2 machine gun (jeep), 3 the tank's one shell
        readonly List<FireAt> fireRed = new List<FireAt>(), fireBlue = new List<FireAt>();
        readonly List<float> guardShot = new List<float>(); readonly List<float> guardPhase = new List<float>();
        bool fireBuilt; Car turretCar; float turretFrom = float.NaN, lastShotSound = -99f;

        void FirefightBuild()
        {
            fireBuilt = true; fireRed.Clear(); fireBlue.Clear(); guardShot.Clear(); guardPhase.Clear(); turretCar = null;
            if (!Firefight || spans.Count == 0) return;
            float blast0 = float.MaxValue; foreach (var s in spans) blast0 = Mathf.Min(blast0, s.blastAt);
            float start = -(BlueOpen + BluePressClock);
            for (int i = 0; i < guards.Count; i++)
            {
                var g = guards[i]; uint seed = 7919u + (uint)i * 31u;
                guardShot.Add(-99f); guardPhase.Add(Rnd(ref seed) * 6.28f);
                float t = start + RedFireFrom + Rnd(ref seed) * 1.2f;
                if (g.runner) t = Mathf.Max(t, g.runAt + g.runFor + 0.4f);
                while (t < blast0 - 0.15f)
                {
                    int n = Rnd(ref seed) < 0.45f ? 3 : 2;
                    for (int j = 0; j < n; j++) { float at = t + j * (RedShotGap + Rnd(ref seed) * RedShotSpread); if (at < blast0 - 0.1f) fireRed.Add(new FireAt { t = at, who = i, kind = 0 }); }
                    float near = Mathf.InverseLerp(start, blast0, t);
                    t += n * 0.18f + Mathf.Lerp(RedPauseFar, RedPauseNear, near) + Rnd(ref seed) * RedPauseSpread;
                }
            }
            // the parade answers: each column's jeep (two short bursts), three robots of the squads (one or two shots each), one tank turret
            uint sb = 6151u; int robots = 0;
            for (int ci = 0; ci < cars.Count; ci++)
            {
                var c = cars[ci]; if (!c.root || c.kind == Kind.Banner) continue;
                if (c.kind == Kind.Jeep)
                {
                    float t0 = BlueFireFrom + 0.15f * c.span + Rnd(ref sb) * 0.3f;
                    for (int b = 0; b < 2; b++) for (int j = 0; j < 4; j++) { float at = t0 + b * 0.75f + j * 0.09f; if (at < blast0 - 0.1f) fireBlue.Add(new FireAt { t = at, who = ci, kind = 2 }); }
                }
                else if (c.kind == Kind.Grunt && robots < 3 && c.slot % 2 == 1)
                {
                    robots++; int n = Rnd(ref sb) < 0.5f ? 1 : 2; float t0 = BlueFireFrom + 0.3f + robots * 0.37f + Rnd(ref sb) * 0.25f;
                    for (int j = 0; j < n; j++) { float at = t0 + j * 0.28f; if (at < blast0 - 0.1f) fireBlue.Add(new FireAt { t = at, who = ci, kind = 1 }); }
                }
                else if (turretCar == null && c.turret && c.turretHome && (c.kind == Kind.Light || c.kind == Kind.Medium) && c.span == spans.Count - 1)
                {
                    turretCar = c; turretFrom = BlueFireFrom + 0.2f;
                    float at = turretFrom + TurretTurnSeconds + 0.25f; if (at < blast0 - 0.1f) fireBlue.Add(new FireAt { t = at, who = ci, kind = 3 });
                }
            }
            fireRed.Sort((a, b) => a.t.CompareTo(b.t)); fireBlue.Sort((a, b) => a.t.CompareTo(b.t));
            Debug.Log($"[OpenZ L18 briefing] round 4 firefight: {fireRed.Count} red shots by {guards.Count} defenders, {fireBlue.Count} shots back by the parade (turret: {(turretCar != null ? turretCar.label : "none")})");
        }

        void FirefightTick(float t, bool forward)
        {
            if (!Firefight || guards.Count == 0) return;
            if (!fireBuilt) FirefightBuild();
            float blast0 = float.MaxValue; foreach (var s in spans) blast0 = Mathf.Min(blast0, s.blastAt);
            var fx = OpenZFxPlayer.I; bool fxOk = fx && fx.isActiveAndEnabled;
            // the red lines
            foreach (var f in fireRed)
            {
                if (!Crossed(f.t, forward)) continue;
                var g = f.who < guards.Count ? guards[f.who] : null; if (g == null || !g.root) continue;
                var target = NearestHull(g.root.position, out bool robot);
                if (target == null) continue;
                uint sd = (uint)(9001 + f.who * 13 + (int)(f.t * 100f));
                Vector3 to = target.root.position + Vector3.up * (robot ? 1.0f : target.high * 0.55f) + target.root.right * ((Rnd(ref sd) - 0.5f) * (robot ? 0.6f : target.halfW * 1.2f));
                Vector3 fwd = g.root.forward, from = g.root.position + Vector3.up * (g.kneel ? 0.82f : 1.25f) + fwd * 0.55f;
                if (fxOk) fx.Shot(from, to, FxWeapon.Rifle, robot ? FxSurface.Unit : FxSurface.Metal);
                guardShot[f.who] = f.t; RedShots++;
                ShotSound("brief_l18_shot_red", RifleBank[(f.who + RedShots) % RifleBank.Length], RedShotGain, from);
            }
            // the parade's answer
            foreach (var f in fireBlue)
            {
                if (!Crossed(f.t, forward)) continue;
                var c = f.who < cars.Count ? cars[f.who] : null; if (c == null || !c.root || !c.driving || !c.root.gameObject.activeSelf) continue;
                var gd = NearestGuard(c.root.position); if (gd == null) continue;
                uint sd = (uint)(4409 + f.who * 17 + (int)(f.t * 100f));
                Vector3 front = gd.root.position - gd.root.forward * (1.2f + Rnd(ref sd) * 1.6f) + gd.root.right * ((Rnd(ref sd) - 0.5f) * 3f);   // a miss that kicks dust in front of the line
                front.y = game ? game.GroundY(front.x, front.z) : front.y;
                Vector3 from; FxWeapon w;
                if (f.kind == 3 && c.turret)
                {
                    Vector3 aim = front - c.turret.position; aim.y = 0f; aim = aim.sqrMagnitude > 1e-4f ? aim.normalized : c.root.forward;
                    from = c.turret.position + aim * Mathf.Max(1.5f, c.nose * 0.9f) + Vector3.up * 0.25f; w = FxWeapon.TankShell;
                    front = c.root.position + (front - c.root.position) * 0.8f; front.y = game ? game.GroundY(front.x, front.z) : front.y;   // the shell lands short of the line (a dust kick, no hit)
                    ShotSound("brief_l18_tank_shot", "v3-tank-shot-b", TankShotGain, from);
                }
                else
                {
                    from = c.root.position + Vector3.up * (c.robot ? 1.25f : 1.6f) + c.root.forward * (c.robot ? 0.55f : 0.3f);
                    w = f.kind == 2 ? FxWeapon.MachineGun : FxWeapon.Rifle;
                    ShotSound("brief_l18_shot_blue", RifleBank[(f.who + BlueShots) % RifleBank.Length], BlueShotGain, from);
                }
                if (fxOk) fx.Shot(from, front, w, FxSurface.Ground);
                BlueShots++;
            }
            // alive between shots: recoil on every shot, the aim lives and turns a little after the nearest hull (before the blasts only)
            for (int i = 0; i < guards.Count && i < guardShot.Count; i++)
            {
                var g = guards[i]; if (!g.root) continue;
                float on = 1f - Mathf.Clamp01((t - (blast0 + g.react)) / 0.5f);
                if (g.runner && t < g.runAt + g.runFor + 0.25f) on = 0f;
                if (on <= 0f) continue;
                float yaw = (AimLife * Mathf.Sin(0.9f * t + guardPhase[i]) + AimLife * 0.5f * Mathf.Sin(2.3f * t + guardPhase[i] * 1.7f));
                var h = NearestHull(g.root.position, out _);
                if (h != null)
                {
                    Vector3 d = h.root.position - g.root.position; d.y = 0f;
                    if (d.sqrMagnitude > 1f) yaw += Mathf.Clamp(Vector3.SignedAngle(g.face * Vector3.forward, d, Vector3.up), -AimFollow, AimFollow);
                }
                float since = t - guardShot[i], kick = since >= 0f && since < 0.6f ? Mathf.Exp(-since / Mathf.Max(0.01f, RecoilTau)) : 0f;
                var rot = g.root.rotation * Quaternion.Euler(-RecoilUp * kick * on, yaw * on, 0f);
                g.root.SetPositionAndRotation(g.root.position - g.root.forward * (RecoilBack * kick * on), rot);
            }
            // the tank's turret turns at the red line before its one shell, and stays there until the blasts take the hull
            if (turretCar != null && turretCar.turret && turretCar.turretHome && !turretCar.launched && t < blast0)
            {
                float u = Mathf.SmoothStep(0f, 1f, (t - turretFrom) / Mathf.Max(0.1f, TurretTurnSeconds));
                var gd = NearestGuard(turretCar.root.position); float turn = 0f;
                if (gd != null)
                {
                    Vector3 d = gd.root.position - turretCar.turret.position; d.y = 0f;
                    if (d.sqrMagnitude > 1f) turn = Mathf.Clamp(Vector3.SignedAngle(turretCar.root.forward, d, Vector3.up), -TurretMaxTurn, TurretMaxTurn);
                }
                turretCar.turret.rotation = Quaternion.AngleAxis(turn * u, Vector3.up) * (turretCar.turretHome.rotation * turretCar.turretRot);
            }
        }

        Car NearestHull(Vector3 p, out bool robot)
        {
            Car best = null; float bd = float.MaxValue; robot = false;
            foreach (var c in cars)
            {
                if (!c.root || !c.driving || !c.root.gameObject.activeSelf) continue;
                float d = (c.root.position - p).sqrMagnitude; if (d < bd) { bd = d; best = c; }
            }
            if (best != null) robot = best.robot;
            return best;
        }

        Guard NearestGuard(Vector3 p)
        {
            Guard best = null; float bd = float.MaxValue;
            foreach (var g in guards) { if (!g.root) continue; float d = (g.root.position - p).sqrMagnitude; if (d < bd) { bd = d; best = g; } }
            return best;
        }

        void ShotSound(string id, string file, float gain, Vector3 at)
        {
            if (!Sounds || Time.unscaledTime - lastShotSound < ShotSoundGap) return;
            var cam = Camera.main; float k = 1f;
            if (cam) k = Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(ShotFar, ShotNear, Vector3.Distance(cam.transform.position, at)));
            if (Play(id, file, gain * k, DuckNow) != null) { lastShotSound = Time.unscaledTime; ShotSounds++; }
        }

        // ================================================================================================ 34: smoke soft against the rock and the far land
        /// <summary>Owner addition 34 (his second complaint): the stage's other smoke (the stump pillars, the steam, the ruins' and the shots'
        /// leg_smoke puffs) fades softly into what it meets over this many metres while a briefing flies (the blasts' own channels: Look.cs
        /// SmokeSoftFade); the game camera draws its depth texture while a briefing flies (the soft fade needs it); the red list's flights take
        /// the far-land-as-sky rule (Look.cs FarTick) on every shot.</summary>
        public static bool SoftAll = true, DepthOn = true, FarRedAll = true;
        public static float SoftOtherFade = 2.2f;
        static readonly string[] SoftChannels = { "FxCh_leg_smoke", "FxCh_leg_dust", "FxCh_m12_smoke" };
        sealed class R4SoftWas { public Material m; public float soft; }
        readonly List<R4SoftWas> r4SoftWas = new List<R4SoftWas>(); bool r4SoftOn; float r4SoftAt, r4SoftScanAt; Camera depthCam; CameraOverrideOption depthWas; bool depthSet;
        /// <summary>Play checks: materials softened now, the game camera's depth texture forced on.</summary>
        public static int SoftMats; public static bool DepthForced;

        void SoftTick()
        {
            if (DepthOn && !depthSet)
            {
                var cam = Camera.main;
                if (cam)
                {
                    var cd = cam.GetUniversalAdditionalCameraData();
                    if (cd) { depthCam = cam; depthWas = cd.requiresDepthOption; cd.requiresDepthOption = CameraOverrideOption.On; depthSet = true; DepthForced = true; }
                }
            }
            if (!SoftAll || Time.unscaledTime < r4SoftAt) return;
            r4SoftAt = Time.unscaledTime + 0.5f;                                                          // the pools make their channels late: look again now and then
            var seen = new HashSet<Material>(); foreach (var w in r4SoftWas) if (w.m) seen.Add(w.m);
            void Take(Renderer r)
            {
                if (!r) return; var m = r.sharedMaterial; if (!m || seen.Contains(m) || !m.HasProperty("_SoftFade")) return;
                seen.Add(m); r4SoftWas.Add(new R4SoftWas { m = m, soft = m.GetFloat("_SoftFade") }); m.SetFloat("_SoftFade", Mathf.Max(m.GetFloat("_SoftFade"), SoftOtherFade));
            }
            foreach (var s in spans) if (s.pillar) Take(s.pillar.GetComponent<ParticleSystemRenderer>());
            if (steam) Take(steam.GetComponent<ParticleSystemRenderer>());
            if (!r4SoftOn || Time.unscaledTime >= r4SoftScanAt)
            {   // the effect player makes its channels when first used: looked for again once a second while the briefing flies
                r4SoftScanAt = Time.unscaledTime + 1f;
                foreach (var r in FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (System.Array.IndexOf(SoftChannels, r.gameObject.name) >= 0) Take(r);
                r4SoftOn = true;
            }
            SoftMats = r4SoftWas.Count;
        }

        void SoftOff()
        {
            foreach (var w in r4SoftWas) if (w.m) w.m.SetFloat("_SoftFade", w.soft);
            r4SoftWas.Clear(); r4SoftOn = false; r4SoftAt = r4SoftScanAt = 0f; SoftMats = 0;
            if (depthSet && depthCam) { var cd = depthCam.GetUniversalAdditionalCameraData(); if (cd) cd.requiresDepthOption = depthWas; }
            depthSet = false; depthCam = null; DepthForced = false;
        }

        // ================================================================================================ 40: the sun's lens flare
        /// <summary>Owner addition 40 : a SUBTLE flare on the drawn low sun while a
        /// level 18 briefing flies (the game camera only: the RTS view after the hand-back has none): a soft warm glow on the disc, a gentle
        /// horizontal streak, a few faint hexagon ghosts along the line through the frame's centre. It fades toward the frame's edge and off
        /// it (URP's own attenuation), behind rock and buildings (depth occlusion) and behind the stage's smoke (FlareSmoke: the puffs on the
        /// line to the sun). FlareGain 0 = none.</summary>
        public static bool SunFlare = true;
        public static float FlareGain = 0.55f, FlareGlowSize = 2.4f, FlareGlow = 0.45f, FlareStreak = 0.12f, FlareGhosts = 0.07f, FlareSmokeK = 1.4f, FlareOccRadius = 0.012f;
        public static Color FlareTint = new Color(1f, 0.80f, 0.58f), GhostTint = new Color(0.62f, 0.78f, 1f);
        /// <summary>Play checks: the flare's strength now (0..FlareGain) and the smoke's share in front of the sun.</summary>
        public static float FlareNow, FlareSmoke;
        GameObject flareGo; LensFlareComponentSRP flare; LensFlareDataSRP flareData;

        void FlareTick()
        {
            if (!SunFlare || FlareGain <= 0f) { FlareOff(); return; }
            var cam = Camera.main; var sun = RenderSettings.sun;
            if (!cam || !sun) { FlareOff(); return; }
            if (!flareGo)
            {
                flareData = ScriptableObject.CreateInstance<LensFlareDataSRP>(); flareData.hideFlags = HideFlags.DontSave;
                var glow = new LensFlareDataElementSRP { flareType = SRPLensFlareType.Circle, position = 0f, localIntensity = FlareGlow, uniformScale = FlareGlowSize, tint = FlareTint, fallOff = 0.85f, edgeOffset = 0.2f, modulateByLightColor = false, blendMode = SRPLensFlareBlendMode.Additive };
                var streak = new LensFlareDataElementSRP { flareType = SRPLensFlareType.Circle, position = 0f, localIntensity = FlareStreak, uniformScale = 1f, sizeXY = new Vector2(9f, 0.07f), tint = FlareTint, fallOff = 1f, edgeOffset = 0.3f, modulateByLightColor = false, blendMode = SRPLensFlareBlendMode.Additive };
                var ghosts = new LensFlareDataElementSRP { flareType = SRPLensFlareType.Polygon, sideCount = 6, sdfRoundness = 0.25f, position = 0.55f, localIntensity = FlareGhosts, uniformScale = 0.55f, tint = GhostTint, fallOff = 0.6f, edgeOffset = 0.1f,
                                                           allowMultipleElement = true, count = 4, distribution = SRPLensFlareDistribution.Uniform, lengthSpread = 1.4f, modulateByLightColor = false, blendMode = SRPLensFlareBlendMode.Additive };
                flareData.elements = new[] { glow, streak, ghosts };
                flareGo = new GameObject("L18SunFlare"); flareGo.transform.SetParent(transform, false);
                flare = flareGo.AddComponent<LensFlareComponentSRP>();
                flare.lensFlareData = flareData; flare.useOcclusion = true; flare.occlusionRadius = 1f; flare.sampleCount = 24; flare.allowOffScreen = false;
                flare.attenuationByLightShape = false; flare.maxAttenuationDistance = 100000f; flare.maxAttenuationScale = 100000f;
            }
            if (!flareGo.activeSelf) flareGo.SetActive(true);
            Vector3 dir = FlybySky.VisualSun(-sun.transform.forward).normalized;                        // the drawn disc (the key light's azimuth, at most 1.4 deg up)
            float far = Mathf.Min(cam.farClipPlane * 0.8f, 1500f);
            flareGo.transform.position = cam.transform.position + dir * far;
            flare.occlusionRadius = far * FlareOccRadius;
            FlareSmoke = Mathf.Clamp01(SmokeOnLine(cam.transform.position, dir) * FlareSmokeK);
            FlareNow = FlareGain * (1f - FlareSmoke);
            flare.intensity = FlareNow;
        }

        void FlareOff()
        {
            if (flareGo && flareGo.activeSelf) flareGo.SetActive(false);
            FlareNow = 0f;
        }

        /// <summary>How much of the stage's smoke stands on the line from the lens to the sun (0 clear .. 1 and more: thick).</summary>
        float SmokeOnLine(Vector3 from, Vector3 dir)
        {
            float cover = 0f;
            void Scan(ParticleSystem ps)
            {
                if (!ps) return;
                int max = ps.main.maxParticles; if (colBuf == null || colBuf.Length < max) colBuf = new ParticleSystem.Particle[Mathf.Max(max, 512)];
                int n = ps.GetParticles(colBuf); bool world = ps.main.simulationSpace == ParticleSystemSimulationSpace.World; var tr = ps.transform;
                for (int i = 0; i < n; i++)
                {
                    Vector3 w = world ? colBuf[i].position : tr.TransformPoint(colBuf[i].position);
                    Vector3 v = w - from; float s = Vector3.Dot(v, dir); if (s < 1f) continue;
                    float r = colBuf[i].GetCurrentSize(ps) * 0.4f, d = (v - dir * s).magnitude; if (d >= r) continue;
                    float life = colBuf[i].startLifetime > 0f ? colBuf[i].remainingLifetime / colBuf[i].startLifetime : 0f;
                    cover += (1f - d / r) * Mathf.Clamp01(life * 1.5f) * (colBuf[i].GetCurrentColor(ps).a / 255f);
                    if (cover >= 2f) return;
                }
            }
            foreach (var ps in colSys) { Scan(ps); if (cover >= 2f) return cover; }
            foreach (var s in spans) { Scan(s.pillar); if (cover >= 2f) return cover; }
            return cover;
        }

        // ================================================================================================ the stage's robots: never the old one
        /// <summary>Play check: how often the parade had to draw a robot with the OLD Z_robot prefab because the level scene's unit list has no
        /// </summary>
        public static int OldRobotFallbacks;
        GameObject OldRobot(string what)
        {
            OldRobotFallbacks++;
            Debug.LogError("[OpenZ L18 briefing] PLACEHOLDER: the level scene's unit list has no current robot prefab for the " + what + ": the OLD Z_robot is drawn (fix the L18 scene's ZGame unit prefabs)");
            return game.Prefab("Z_robot");
        }

        /// <summary>One line of round 4's numbers for the Play check (OzL18BriefingStage.R4Status()).</summary>
        public static string R4Status()
            => $"L18 round 4: briefing march plays {OwnerMarchPlays} (2 wanted a blue flight), cut at clock {OwnerMarchCutTau:F2} vs first blast {OwnerMarchFirstBlastTau:F2}; tail cues {TailCuesPlayed} ({TailCues.Length} a flight), pops {TailPops}; "
             + $"shots red {RedShots} / blue {BlueShots}, shot sounds {ShotSounds}; soft materials {SoftMats}, depth forced {DepthForced}; flare {FlareNow:F2} (smoke {FlareSmoke:F2}); old robot fallbacks {OldRobotFallbacks} (0 wanted)";
    }
}
