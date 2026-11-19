// OpenZ (GPL-3.0)
// The flight itself is the level's shot list (ZodFlyover.L18.cs: B18 as blue, L18 as red); this class watches which shot is on screen and
// how far into it (ZodFlyover.CineShown) and puts the story there. VIEW ONLY: the rules hold the three centre bridges at health 0 from
// tick 0 (the map file's own start state); nothing here is a unit, a collider of the game, a rule or a save.
//   BLUE (PLAY AS BLUE): while EvilZod counts his bridges the three long decks stand whole (the stage's own copies of the level's bridge
//     model over the hidden destroyed ones) and three columns of blue Mammoths and heavy tanks drive over them toward the red fort
//     (OzL18BriefingStage.Convoy.cs). On the cut into the shot `three_blasts` his palm has met the wrong button: PressToBlast later the
//     west bridge goes, then the middle, then the east one (BlastGap apart; three charges a span), with the game's own explosion, its
//     bridge break-up, the bridge kit's own chunks, water columns and the ruin fire; every tank on a deck is thrown, its turret flies off
//     spinning (classic Z), it hits the water and goes up in it (OzL18BriefingStage.Blast.cs); the tanks behind a deck brake too late and
//     go over the broken end one after another (Convoy.cs). Each blast kicks the camera inside the flight's picture (FlightKick). From
//     its blast on a span shows the level's real destroyed model: at the hand-back the picture IS the rules' start state.
//   RED: the briefing opens a moment after the blasts: three fresh fireballs on the broken spans, the cloud rising, the last hulls going
//     under, three smoke pillars. No decks, no convoy.
// EVERYTHING IS A PURE FUNCTION OF THE CLOCK: tau = seconds from the cut into `three_blasts` (blue) or from the first frame of the flight
// (red), read from the shot on screen and the seconds into it. A held frame (ZodFlyover.HoldShot) shows the running flight's picture, a
// paused briefing stands still. Only the one-shot effects (fireballs, chunks, splashes, sounds) are fired when the clock passes their
// second, and only when it passes it going forward.
// NOTHING OF THE STAGE SURVIVES THE HAND-BACK: at the hand-back, on a skip (any second) or when the scene goes, every deck copy, tank,
// turret and chunk is destroyed, the real destroyed bridges are shown and the rubble the blasts left is retired. Only the three smoke
// pillars and a quiet fire crackle stay and fade out over AfterglowSeconds of play (they are smoke: nothing a unit could touch).
// NOTHING IS BUILT IN THE FRAME IT FIRST SHOWS: decks, tanks, pillars and sound clips are made ready (hidden) in the first frame of the
// flight, which in the game is the level's first frame, under the load tail and the letterbox.
// SOUNDS: 2D on the stage's own sources at the world level, ducked by ZodFlyover.DipDuck under a voice, every start in the sound log
// (source "L18B"): engines under the columns, each blast (explosion, steel collapse, water column, groan, debris rain), each hull and
// turret hitting the water, steam, the fire. The camera's own whooshes are the shot list's (its `whoosh` flags).
// It exists on level 18 only and does nothing while no shot list flies. Play checks: return OpenZ.OzL18BriefingStage.Status();
using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    [DefaultExecutionOrder(200)]                                                                      // LateUpdate after the briefing has flown the camera
    public sealed partial class OzL18BriefingStage : MonoBehaviour
    {
        public const string Level = OzCity.BridgeGame;
        /// <summary>The blue list's shot whose cut IS the press (tau = 0 there). The blue story needs it; a list without it shows the aftermath only.</summary>
        public const string PressShot = "three_blasts";
        public const string SfxRes = "OpenZ/Audio/Sfx/";
        public static OzL18BriefingStage I { get; private set; }
        // <numbers: kit_tools/voices_build.py wrote the same ones into the voice clip's pause (clock.json numbers); change both or neither>
        /// <summary>Blue: seconds from the press to the west bridge's first charge, from one bridge to the next, from one charge of a span to the next.</summary>
        public static float PressToBlast = 1.30f, BlastGap = 0.45f, ChargeGap = 0.12f;
        // </numbers>
        /// <summary>Red: seconds from the first frame of the flight to the first of the three fresh fireballs, and between them.</summary>
        public static float RedBlastAt = 0.30f, RedBlastGap = 0.34f;
        /// <summary>Red, round 2: the flight opens on the blue parades crossing the three whole bridges and the blasts go off under them in its first
        /// second (the blue side's own stage, seen from the red bank). False = the aftermath only, as round 1.</summary>
        public static bool RedParade = true;
        /// <summary>True while this flight is staged with the parades and the blue side's blasts (blue with its press shot, or red with RedParade).</summary>
        bool Parades => (blue && hasPress) || (!blue && RedParade && cars.Count > 0);
        /// <summary>The smoke and the fire crackle that stay after the hand-back fade out over this many seconds of play.</summary>
        public static float AfterglowSeconds = 90f;
        /// <summary>False = silent stage (a check that wants pictures only).</summary>
        public static bool Sounds = true;
        /// <summary>Play checks: flights staged, span blasts fired, hulls gone under, actors alive now, releases, the last warning.</summary>
        public static int Flights, BlastsFired, HullsSunk, Released, SoundsStarted;
        /// <summary>The blue list's shot that flies through the falling wreckage, and what was still in the air at its cut out in the last flight (-1 = not flown yet; a count, no rule).</summary>
        public const string AirShot = "aftermath_run"; public static int AirAtCut = -1;
        /// <summary>Under a line every sound of the stage goes down to that line's share (BS-01 speaks 7 dB under EvilZod: his lines duck deeper).
        /// The lines are the voice clips' own (start, end pairs on the clip's clock, clock.json); the duck is a formula of the clock: down over
        /// DuckIn s, there DuckLead s before the first word, back up over DuckOut s from DuckTail s after the last one.</summary>
        public static float VoiceDuck = 0.15f;
        public static float[] BlueLines = { 0.00f, 4.68f, 4.73f, 6.24f, 6.29f, 7.35f, 10.41f, 13.53f, 13.75f, 17.74f, 18.13f, 19.82f }, BlueDuck = { 0.34f, 0.34f, 0.5f, 0.13f, 0.26f, 0.13f };   // the third: BS-01's scream at the press (its duck lets go before the first blast: the blast cuts it)
        public static float[] RedLines = { 0.30f, 3.43f, 3.71f, 6.27f, 6.67f, 9.02f, 9.30f, 12.81f, 13.26f, 15.86f, 16.16f, 18.94f }, RedDuck = { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f };
        /// <summary>The voice clock at the press (blue: the stage clock's zero), and the seconds the window is open before the first word.</summary>
        public static float BluePressClock = 6.51f, BlueOpen = 1.10f, RedOpen = 1.17f, DuckLead = 0.15f, DuckIn = 0.15f, DuckTail = 0.25f, DuckOut = 0.40f;
        /// <summary>Play check: the duck now (1 = no line).</summary>
        public static float DuckNow = 1f;
        /// <summary>The blasts' tail: from this stage second it falls to TailLow of itself by TailTo (and stays there: what comes later is the
        /// aftermath). (The game's own ambience bed is left alone: its ducks pump the briefing's voices, measured on two takes.)</summary>
        public static float TailFrom = 2.6f, TailTo = 3.6f, TailLow = 0.42f;
        /// <summary>The level's ambience bus while a briefing flies (dB, 0 = untouched): the flights run low along the harbour water, whose loops stand
        /// at the voices' own level there. Only the ambience bus: a duck of the world bus takes the briefing's voices down with it (measured on three takes).</summary>
        public static float RedAmbienceDb = -12f, BlueAmbienceDb = -8f;
        float ambAt;

        static float LineDuck(float[] lines, float[] to, float c)
        {
            float k = 1f;
            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                float s = lines[i] - DuckLead, e = lines[i + 1] + DuckTail, low = to != null && i / 2 < to.Length ? to[i / 2] : VoiceDuck, d;
                if (c < s - DuckIn || c > e + DuckOut) continue;
                if (c < s) d = Mathf.Lerp(1f, low, Mathf.SmoothStep(0f, 1f, (c - (s - DuckIn)) / Mathf.Max(0.01f, DuckIn)));
                else if (c <= e) d = low;
                else d = Mathf.Lerp(low, 1f, Mathf.SmoothStep(0f, 1f, (c - e) / Mathf.Max(0.01f, DuckOut)));
                k = Mathf.Min(k, d);
            }
            return k;
        }
        /// <summary>The fire crackle by where the game camera looks: full within FireNear metres of a burning bridge, silent beyond FireFar.</summary>
        public static float FireNear = 45f, FireFar = 190f;
        public static string LastWarning = "";

        ZGame game; bool staged, blue, built, buildFailed, hasPress, afterglow, softWas, softSet; float swellWas;
        /// <summary>The strength of the soft swells on the water while the stage plays (the level's own is the game's).</summary>
        public static float StageSwellAlpha = 0f;
        float tau = -99f, prevTau = -99f, glowT; string shotNow;
        Transform root;
        readonly Dictionary<string, AudioClip> sfx = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> voices = new List<AudioSource>(); readonly List<float> voiceGain = new List<float>();
        AudioSource engines, fire; float enginesK, fireK;

        /// <summary>The stage's clock now (s): from the press as blue, from the first frame of the flight as red; -99 while nothing is staged.</summary>
        public static float Tau => I && I.staged ? I.tau : -99f;
        public static bool Staged => I && I.staged;

        // ------------------------------------------------------------------------------------------------ attach (level 18, both sides)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; DuckNow = 1f; Flights = BlastsFired = HullsSunk = Released = SoundsStarted = RolledOff = DustPuffs = SmokePuffs = FarFrames = FarDrawn = KickFrames = StretchedDecks = LookChannels = 0; AirAtCut = -1; KickNow = 0f; LastWarning = ""; ModelSizes = ""; UnitTable = ""; BannerState = "none"; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterFirstScene() { TryAttach(SceneManager.GetActiveScene()); }
        static void OnScene(Scene s, LoadSceneMode m) { if (m == LoadSceneMode.Single) TryAttach(s); }

        static void TryAttach(Scene s)
        {
            if (!Application.isPlaying || I) return;
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!g || g.level != Level) return;
            var go = new GameObject("OzL18BriefingStage");
            if (s.IsValid() && go.scene != s) SceneManager.MoveGameObjectToScene(go, s);
            go.AddComponent<OzL18BriefingStage>();
        }

        void Awake() { I = this; game = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>(); }

        void OnDestroy()
        {
            if (I == this) I = null;
            try { if (staged) Release("the scene goes"); ShowReal(true); RestoreMushroomLife(); LookOff(); FarOff(); DisposeBanner(); } catch (Exception e) { Debug.LogException(e); }
        }

        static void Warn(string s) { if (LastWarning == s) return; LastWarning = s; Debug.LogWarning("[OpenZ L18 briefing] " + s); }

        // ------------------------------------------------------------------------------------------------ frame
        void LateUpdate()
        {
            TickAfterglow(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
            TickMushroomLife();
            var fly = ZodFlyover.Live;
            string shot = null; float into = 0f, dur = 0f;
            bool on = fly != null && fly.Active && fly.Cine && fly.CineShown(out shot, out into, out dur) && game && game.level == Level;
            if (!on) { KickOff(); if (staged) Release("the flight handed the camera back"); return; }
            if (!staged)
            {
                staged = true; Flights++; blue = OzPlayBlue.Active; prevTau = -99f; shotNow = null; afterglow = false;
                ZodFlyover.DipHook = blue ? null : (Func<string, float>)RedDip;                           // round 3 R3.2: red's camera whooshes
                softWas = OpenZFxPlayer.SoftWater; swellWas = OpenZFxPlayer.SoftSwellAlpha; softSet = true;
                OpenZFxPlayer.SoftWater = true; OpenZFxPlayer.SoftSwellAlpha = Mathf.Min(swellWas, StageSwellAlpha);   // while the stage plays, what falls into the river draws faint soft swells and foam, never line circles (given back at the release)
                Prepare(fly);
            }
            if (buildFailed) return;
            string shotWas = shotNow; shotNow = shot;
            // the clock: the briefing's own seconds at the shot on screen, counted from the press (blue) or the first frame (red)
            float t = into, press = float.NaN;
            foreach (var s in fly.CineShots) { if (s.name == shot) t = s.start + into; if (s.name == PressShot) press = s.start; }
            hasPress = blue && !float.IsNaN(press);
            tau = hasPress ? t - press : t;
            float duck = blue ? LineDuck(BlueLines, BlueDuck, hasPress ? tau + BluePressClock : t - BlueOpen) : LineDuck(RedLines, RedDuck, t - RedOpen);
            if (ZodBriefing.StartLinePlaying) duck = Mathf.Min(duck, VoiceDuck);
            float amb = blue ? BlueAmbienceDb : RedAmbienceDb;
            if (amb < 0f && Time.unscaledTime >= ambAt && AudioDirector.I)
            {   // the level's own ambience (the harbour's water under a low flight) held down evenly while the briefing flies: one steady level, no pumping
                ambAt = Time.unscaledTime + 0.2f; AudioDirector.I.AddDuck(0f, 0f, amb, 0f, 0.6f, 0.3f, 1.0f);
            }
            if (blue && hasPress && tau > TailFrom) duck *= Mathf.Lerp(1f, TailLow, Mathf.SmoothStep(0f, 1f, (tau - TailFrom) / Mathf.Max(0.1f, TailTo - TailFrom)));   // the three blasts' wall of sound decays before BS-01 speaks
            DuckNow = duck;
            bool forward = tau >= prevTau && tau - prevTau < 0.5f;                                      // a running clock: one-shot effects may fire
            if (Parades) TickBlue(tau, forward, duck); else TickRed(tau, forward, duck, blue);
            TickDefence(tau);
            R4Tick(tau, forward, duck);                                                                 // round 4: firefight, the blasts' tail, soft smoke, the sun flare (R4.cs)
            TickLoops(duck);
            LookTick();                                                                                 // the blasts' fire glows, their smoke is soft (this stage only: Look.cs)
            FarTick();                                                                                  // smoke in front of the far land is hazed as in front of the sky (Look.cs)
            PillarNearTick();                                                                           // red_r6_puff (row 19.6): a stump pillar's puff near the lens at the frame edge fades (R6.cs)
            if (blue && hasPress && forward)
            {   // the drone's rush of air: the dive at the opening, the pass round the banner
                if (!rushed && tau < -(BlueOpen + BluePressClock) + 0.6f) { rushed = true; Play("brief_l18_dive_rush", "demo-flyby-whoosh", GainRush, 1f, false, 0.9f); }
                if (Crossed(2.2f - BluePressClock, forward)) Play("brief_l18_pass_rush", "sd_body_whoosh_a", GainRush * 0.7f, duck, false, 0.7f);
                if (FpvRush && Crossed(FpvRushTau, forward)) Play("brief_l18_fpv_rush", "demo-flyby-whoosh", GainRush * FpvRushGain, duck, false, FpvRushPitch);
            }
            if (blue && hasPress && forward && shotWas == AirShot && shot != AirShot) AirAtCut = InAir(tau);   // counted for the Play check
            FlightKick(tau);                                                                            // after the flight's pose of this frame
            prevTau = tau;
        }

        /// <summary>True on the frame the clock passes `at` going forward (never for a held or a jumped frame).</summary>
        bool Crossed(float at, bool forward) => forward && prevTau < at && tau >= at;

        // ------------------------------------------------------------------------------------------------ build (hidden, in the flight's first frame)
        void Prepare(ZodFlyover fly)
        {
            if (built || buildFailed) { if (built) Rewind(); return; }
            try
            {
                root = new GameObject("L18BriefingActors").transform; root.SetParent(transform, false);
                BuildSpans();
                if (spans.Count == 0) { buildFailed = true; Warn("no long centre bridge among the level's objects: nothing is staged"); return; }
                bool press = false;
                foreach (var s in fly.CineShots) if (s.name == PressShot) press = true;
                if ((blue && press) || (!blue && RedParade)) BuildConvoy();
                else if (blue) Warn("the blue shot list has no shot '" + PressShot + "': the aftermath only (no decks, no columns, no blasts)");
                BuildPillars();
                if (blue && press) BuildDefence();                                                         // the red side's firing lines at the bridge heads (Defence.cs)
                OzUeBlastFx.Warm(spans[0].c + Vector3.down * 40f);                                       // the blasts' fire, smoke and water channels, made ready under the ground (nothing is built in the frame it first shows)
                foreach (var f in WarmClips) Clip(f);
                built = true;
                Debug.Log($"[OpenZ L18 briefing] staged as {(blue ? "blue" : "red")}: {spans.Count} long bridges at x {SpanXs()}, {cars.Count} tanks in {(cars.Count > 0 ? spans.Count : 0)} columns, {WarmClips.Length} sound clips ready");
            }
            catch (Exception e) { Debug.LogException(e); buildFailed = true; Scrap(); Warn("the stage could not be built: nothing of it stays in the level"); }
        }

        string SpanXs() { var a = new List<string>(); foreach (var s in spans) a.Add(s.c.x.ToString("F0")); return string.Join(" / ", a); }

        /// <summary>A second flight in one visit (the Play check's DebugBriefing): the actors are made again from nothing.</summary>
        void Rewind() { Scrap(); built = false; buildFailed = false; Prepare(ZodFlyover.Live); }

        /// <summary>Every actor of the stage is destroyed and the real bridges are shown (a failed build, a release, a rewind).</summary>
        void Scrap()
        {
            ShowReal(true); DisposeBanner(); DefenceOff();
            foreach (var c in cars) { if (c.turret && c.turretHome) { c.turret.SetParent(c.turretHome, false); } if (c.root) Destroy(c.root.gameObject); }
            cars.Clear();
            foreach (var g in chunks) if (g) Destroy(g);
            chunks.Clear();
            foreach (var s in spans) { if (s.intact) Destroy(s.intact); s.intact = null; if (s.pillar) Destroy(s.pillar.gameObject); s.pillar = null; s.blown = true; }
            if (steam) { Destroy(steam.gameObject); steam = null; }
            if (root) { Destroy(root.gameObject); root = null; }
            spans.Clear();
        }

        // ------------------------------------------------------------------------------------------------ hand-back / skip
        void Release(string why)
        {
            staged = false; shotNow = null; Released++; KickOff(); FarOff(); ZodFlyover.DipHook = null;
            if (softSet) { OpenZFxPlayer.SoftWater = softWas; OpenZFxPlayer.SoftSwellAlpha = swellWas; softSet = false; }
            if (!built) { tau = prevTau = -99f; return; }
            // the picture of the rules' start state: the real destroyed bridges, no deck copy, no tank, no turret, no chunk, no rubble
            ShowReal(true); DisposeBanner(); DefenceOff(); lookOffIn = 8f;                                              // the smoke still in the air keeps its soft look until it is gone
            foreach (var c in cars) { if (c.turret && c.turretHome) c.turret.SetParent(c.turretHome, false); if (c.root) Destroy(c.root.gameObject); }
            int had = cars.Count + chunks.Count; cars.Clear();
            foreach (var g in chunks) if (g) Destroy(g);
            chunks.Clear();
            foreach (var s in spans)
            {
                if (s.intact) { Destroy(s.intact); s.intact = null; }
                s.blown = true;
                var fx = FxManager.I; if (fx) fx.ClearRubble(s.c, s.half + 14f);
                if (s.pillar && !s.pillar.isPlaying) { s.pillar.Simulate(6f, true, true); s.pillar.Play(); }   // a skip before the blasts: the ruins smoke all the same (the bridges ARE down)
                if (!s.fired) { s.fired = true; OzRuinFires.Bridge(s.o); }                              // and burn at their broken ends, as after every bridge death
            }
            if (engines) { engines.Stop(); OzSoundLog.Add("L18B", "brief_l18_engines", engines.clip ? engines.clip.name : null, "STOP loop (the stage is released)"); engines = null; }
            foreach (var v in voices) if (v && v.isPlaying && v != fire) v.Stop();
            StopLoop(ref march, "brief_l18_march", "the stage is released"); StopLoop(ref flap, "brief_l18_banner_cloth", "the stage is released"); StopLoop(ref tracks, "brief_l18_tracks", "the stage is released"); marchK = flapK = 0f; bedCutK = 1f; rushed = false; R4Release(why);
            afterglow = true; glowT = 0f;
            tau = prevTau = -99f;
            Debug.Log($"[OpenZ L18 briefing] released ({why}): {had} actors destroyed, the real bridges shown, the smoke fades over {AfterglowSeconds:F0} s");
        }

        /// <summary>After the hand-back: the pillars thin out and the crackle goes quiet, then both are gone.</summary>
        float lookOffIn = -1f;
        void TickAfterglow(float dt)
        {
            if (lookOffIn > 0f && !staged) { lookOffIn -= dt; if (lookOffIn <= 0f) LookOff(); }
            if (!afterglow || !built) return;
            if (game && game.paused) return;
            glowT += dt;
            float k = 1f - Mathf.Clamp01(glowT / Mathf.Max(1f, AfterglowSeconds));
            foreach (var s in spans) if (s.pillar) { var em = s.pillar.emission; em.rateOverTime = PillarRate * k * k; }
            if (fire) { fire.volume = FireGain * Bus() * k * 0.6f * FireNearK(out float pan); fire.panStereo = pan; if (k <= 0f && fire.isPlaying) { fire.Stop(); OzSoundLog.Add("L18B", "brief_l18_fire", fire.clip ? fire.clip.name : null, "STOP loop (the afterglow is over)"); } }
            if (k <= 0f && glowT > AfterglowSeconds + PillarLife * 1.3f)
            {
                afterglow = false;
                foreach (var s in spans) { if (s.pillar) Destroy(s.pillar.gameObject); s.pillar = null; }
            }
        }

        /// <summary>The fire's loudness by where the game camera looks (the point its lens meets the ground): full within FireNear metres of the
        /// nearest burning bridge, none beyond FireFar; panned to that bridge's side of the picture. A fire is heard where it burns.</summary>
        float FireNearK(out float pan)
        {
            pan = 0f; var cam = Camera.main; if (!cam || spans.Count == 0) return 1f;
            var ct = cam.transform; Vector3 f = ct.forward, focus = ct.position;
            if (f.y < -0.05f) focus += f * (ct.position.y / -f.y);
            float best = float.MaxValue; Vector3 at = spans[0].c;
            foreach (var s in spans) { float d = new Vector2(s.c.x - focus.x, s.c.z - focus.z).magnitude; if (d < best) { best = d; at = s.c; } }
            var vp = cam.WorldToViewportPoint(at); if (vp.z > 0f) pan = Mathf.Clamp((vp.x - 0.5f) * 1.4f, -0.7f, 0.7f);
            return Mathf.Clamp01(Mathf.InverseLerp(FireFar, FireNear, best));
        }

        // ------------------------------------------------------------------------------------------------ sounds
        public static float GainEngines = 0.30f, GainBlast = 0.46f, GainCollapse = 0.38f, GainWater = 0.4f, GainGroan = 0.4f, GainRain = 0.34f, GainRumble = 0.34f,
                            GainHullSplash = 0.3f, GainSizzle = 0.2f, GainTurret = 0.28f, FireGain = 0.2f, GainBrake = 0.24f, GainSinkBlast = 0.25f, GainBannerLand = 0.42f, GainLipK = 1.3f;
        public static readonly string[] WarmClips =
        {
            "sd_engine_heavy_drive", "explosion-heavy-2", "explosion-v2-20260909", "sd_bridge_collapse_steel_a", "sd_bridge_collapse_steel_b", "sd_explosion_water_x_a", "sd_explosion_water_x_b",
            "sd_structure_groan_a", "sd_structure_groan_b", "sd_debris_rain_a", "sd_debris_rain_b", "sd_nuke_rumble_tail", "sd_body_splash_x_a", "sd_body_splash_x_b",
            "sd_robot_death_fire_sizzle", "sd_explosion_water_small_a", "sd_explosion_water_small_b", "sd_fire_crackle_loop", "sd_tracks_squeal_a", "sd_tracks_squeal_b",
            "sd_squad_march_concrete_loop", "sd_flag_flap_loop", "demo-flyby-whoosh", "sd_body_whoosh_a", "sd_tracks_loop",
            OwnerMarchA, OwnerMarchB, "explosion-v1-20260909", "explosion-v3-20260909", "v3-tank-impact-b", "v3-rifle-single-a", "v3-rifle-single-b", "v3-rifle-single-c", "v3-tank-shot-b",   // round 4 (R4.cs)
        };
        /// <summary>The robots' march (while a squad or the banner Psycho walks), the banner's cloth (carried and in the air), the drone's rush of air.</summary>
        public static float GainMarch = 0.2f, GainFlap = 0.22f, GainRush = 0.3f;
        AudioSource march, flap; float marchK, flapK; bool rushed;
        /// <summary>
        /// and engines, plainly heard from the first frame (about 10 LU under the voice in the open, a little lower under a line: BedDuckShare of
        /// the lines' duck), fuller when the lens is near a column (BedFar .. BedNear m, down to BedThin), and CUT DEAD by the first blast.</summary>
        public static float BedMarch = 0.85f, BedTracks = 0.45f, BedEngines = 0.42f, BedDuckShare = 0.35f, BedNear = 45f, BedFar = 150f, BedThin = 0.72f, BedCutSeconds = 0.06f;
        AudioSource tracks; float bedCutK = 1f;
        float BedNearK()
        {
            var cam = Camera.main; if (!cam) return 1f;
            float best = 1e9f; Vector3 p = cam.transform.position;
            foreach (var c in cars) if (c.driving && c.root) { float d = (c.root.position - p).sqrMagnitude; if (d < best) best = d; }
            return best > 1e8f ? 1f : Mathf.Lerp(BedThin, 1f, Mathf.InverseLerp(BedFar, BedNear, Mathf.Sqrt(best)));
        }

        AudioSource Loop(string id, string file)
        {
            if (!Sounds) return null;
            var clip = Clip(file);
            if (!clip) { OzSoundLog.Add("L18B", id, null, "DROP clip missing (" + file + ")"); Warn("silent loop " + id + ": Resources/" + SfxRes + file + " is missing"); return null; }
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false; src.spatialBlend = 0f; src.dopplerLevel = 0f; src.bypassReverbZones = true; src.priority = 70; src.loop = true; src.clip = clip; src.volume = 0f; src.Play();
            SoundsStarted++; OzSoundLog.Add("L18B", id, clip.name, "LOOP 2D briefing " + shotNow);
            return src;
        }
        void StopLoop(ref AudioSource src, string id, string why)
        {
            if (!src) return;
            src.Stop(); OzSoundLog.Add("L18B", id, src.clip ? src.clip.name : null, "STOP loop (" + why + ")"); Destroy(src); src = null;
        }

        static float Bus() => CombatAudio.I ? CombatAudio.I.volume * CombatAudio.BusGain(CombatAudio.Bus.World) : 0.55f;

        AudioClip Clip(string file)
        {
            if (!sfx.TryGetValue(file, out var clip)) sfx[file] = clip = Resources.Load<AudioClip>(file.IndexOf('/') >= 0 ? file : SfxRes + file);
            return clip;
        }

        /// <summary>
        /// "Actually, sir, so are we!") gets the drone rush of the other flights instead of the wind-swell dip; every other red camera
        /// whoosh stays in its place, RedDipGain lower (0.5 = -6 dB).</summary>
        public static float RedDipGain = 0.5f, RedCutRushGain = 0.8f;
        public static string RedRushShot = "red_cut_off";
        public static int RedRushes, RedDipsLowered;
        float RedDip(string shot)
        {
            if (shot == RedRushShot)
            {
                if (Play("brief_l18_red_cut_rush", "demo-flyby-whoosh", GainRush * RedCutRushGain, 1f, false, FpvRushPitch) != null) { RedRushes++; return 0f; }
                return RedDipGain;                                                                            // the clip missing: the lowered dip as at the other cuts
            }
            if (shot != null && shot.StartsWith("red_")) { RedDipsLowered++; return RedDipGain; }
            return 1f;
        }

        AudioSource Play(string id, string file, float gain, float duck, bool loop = false, float pitch = 0f)
        {
            if (!Sounds) return null;
            var clip = Clip(file);
            if (!clip) { OzSoundLog.Add("L18B", id, null, "DROP clip missing (" + file + ")"); Warn("silent cue " + id + ": Resources/" + SfxRes + file + " is missing"); return null; }
            AudioSource src = null; int at = -1;
            for (int i = 0; i < voices.Count; i++) if (voices[i] && !voices[i].isPlaying && voices[i] != engines && voices[i] != fire) { src = voices[i]; at = i; break; }
            if (!src)
            {
                src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false; src.spatialBlend = 0f; src.dopplerLevel = 0f; src.bypassReverbZones = true; src.priority = 60;
                voices.Add(src); voiceGain.Add(gain); at = voices.Count - 1;
            }
            voiceGain[at] = gain;
            src.clip = clip; src.loop = loop; src.pitch = pitch > 0f ? pitch : loop ? 1f : UnityEngine.Random.Range(0.96f, 1.04f); src.panStereo = 0f;
            src.volume = gain * Bus() * DuckNow; src.Play();                                              // every sound of the stage rides the lines' duck, each frame (TickLoops)
            SoundsStarted++;
            OzSoundLog.Add("L18B", id, clip.name, (loop ? "LOOP" : "PLAY") + " 2D briefing " + shotNow + (DuckNow < 0.999f ? " (under the voice)" : ""), src.volume);
            return src;
        }

        /// <summary>The two loops: the columns' engines (as many as still drive) and the fire on the broken spans.</summary>
        void TickLoops(float duck)
        {
            float bus = Bus();
            for (int i = 0; i < voices.Count; i++) { var v = voices[i]; if (v && v.isPlaying && v != engines && v != fire) v.volume = voiceGain[i] * bus * duck; }   // a one-shot that began in a pause goes down under the next line
            float wantE = 0f;
            if (cars.Count > 0) { int n = 0; foreach (var c in cars) if (c.driving) n++; wantE = n / (float)cars.Count; }
            enginesK = Mathf.MoveTowards(enginesK, wantE, Time.unscaledDeltaTime / 0.35f);
            if (wantE > 0f && !engines) engines = Play("brief_l18_engines", "sd_engine_heavy_drive", GainEngines, duck, true);
            // the blue briefing's bed (see BedMarch): its own softer duck, the lens's distance, dead at the first blast
            bool bed = blue && Parades, boom = false; if (bed) foreach (var s in spans) if (s.fired) boom = true;
            bedCutK = !bed ? 1f : Mathf.MoveTowards(bedCutK, boom ? 0f : 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, boom ? BedCutSeconds : 0.25f));
            float bedK = bed ? Mathf.Lerp(1f, duck, BedDuckShare) * BedNearK() * bedCutK : duck;
            bool own = OwnerMarchTick(bed, boom, bed ? Mathf.Lerp(1f, duck, BedDuckShare) * bedCutK : duck, bus);
            if (bed && wantE > 0f && !tracks && !boom && !own) tracks = Loop("brief_l18_tracks", "sd_tracks_loop");
            if (tracks) { tracks.volume = BedTracks * bus * bedK * enginesK; tracks.pitch = 0.9f; if (boom && bedCutK <= 0.001f) StopLoop(ref tracks, "brief_l18_tracks", "the bridges go: the parade is cut dead"); }
            if (engines) { engines.volume = (bed ? BedEngines * bedK * (own ? OwnerBedEngines : 1f) : GainEngines * duck) * Bus() * enginesK; engines.pitch = 0.92f + 0.1f * enginesK; if (enginesK <= 0.001f && wantE <= 0f && engines.isPlaying) { engines.Stop(); OzSoundLog.Add("L18B", "brief_l18_engines", engines.clip ? engines.clip.name : null, "STOP loop (no tank drives)"); engines = null; } }
            // the march of the robots' boots and the banner's cloth
            float wantM = 0f, wantF = 0f;
            foreach (var c in cars) if (c.robot && c.driving && c.root && c.root.gameObject.activeSelf) { wantM = 1f; break; }
            if (banner != null && (BannerState == "carried" || BannerState == "in the air")) wantF = BannerState == "carried" ? 0.7f : 1f;
            marchK = Mathf.MoveTowards(marchK, wantM, Time.unscaledDeltaTime / 0.3f); flapK = Mathf.MoveTowards(flapK, wantF, Time.unscaledDeltaTime / 0.25f);
            if (wantM > 0f && !march && !own) march = Loop("brief_l18_march", "sd_squad_march_concrete_loop");
            if (wantF > 0f && !flap) flap = Loop("brief_l18_banner_cloth", "sd_flag_flap_loop");
            if (march) { march.volume = (bed ? BedMarch * bedK : GainMarch * duck) * bus * marchK; if (marchK <= 0.001f && wantM <= 0f) StopLoop(ref march, "brief_l18_march", "nobody marches"); }
            if (flap) { flap.volume = GainFlap * bus * duck * flapK; flap.pitch = BannerState == "in the air" ? 1.25f : 1f; if (flapK <= 0.001f && wantF <= 0f) StopLoop(ref flap, "brief_l18_banner_cloth", "the banner lies"); }
            bool burning = false; foreach (var s in spans) if (s.fired) burning = true;
            if (burning && !fire) { fire = Play("brief_l18_fire", "sd_fire_crackle_loop", FireGain, duck, true); fireK = 0f; }
            if (fire && !afterglow) { fireK = Mathf.MoveTowards(fireK, 1f, Time.unscaledDeltaTime / 1.2f); fire.volume = FireGain * Bus() * duck * fireK * FireNearK(out float pan); fire.panStereo = pan; }
        }

        // ------------------------------------------------------------------------------------------------ check
        /// <summary>One-line state for the Play check.</summary>
        public static string Status()
        {
            if (!I) return "L18 briefing stage: none in this scene (level 18 only)";
            int live = 0, drive = 0, sunk = 0, air = I.staged ? I.InAir(I.tau) : 0; foreach (var c in I.cars) { if (c.root && c.root.gameObject.activeSelf) live++; if (c.driving) drive++; if (c.sunk) sunk++; }
            int down = 0; foreach (var s in I.spans) if (s.blown) down++;
            string pose = "";
            if (I.staged)
            {   // the game camera's pose as this frame was drawn (the recorder writes it into frame_log.csv: the flights' angle proof)
                var cm = Camera.main;
                if (cm)
                {
                    var ct = cm.transform; Vector3 e = ct.eulerAngles, p = ct.position; var inv = System.Globalization.CultureInfo.InvariantCulture;
                    pose = string.Format(inv, "; cam {0:F3} {1:F3} {2:F3} ypr {3:F3} {4:F3} {5:F3} fov {6:F2}", p.x, p.y, p.z, e.y, Mathf.DeltaAngle(0f, e.x), Mathf.DeltaAngle(0f, e.z), cm.fieldOfView);
                }
            }
            return $"L18 briefing stage: {(I.staged ? (I.blue ? "blue" : "red") + " flight, shot " + I.shotNow + ", clock " + I.tau.ToString("F2") + " s" + pose : "not staged")}, built {I.built}{(I.buildFailed ? " (FAILED: " + LastWarning + ")" : "")}, "
                 + $"bridges shown destroyed {down} of {I.spans.Count}, tanks {live} shown / {drive} driving / {air} hulls and turrets in the air / {sunk} gone, over a broken end {RolledOff}, in the air at the cut out of {AirShot} {AirAtCut} (0 wanted), chunks {I.chunks.Count}, blasts fired {BlastsFired}, hulls gone {HullsSunk}, "
                 + $"shake {KickNow:F2} deg now ({KickFrames} frames), look channels {LookChannels}, far land as sky {FarFrames} frames ({FarDrawn} pictures of the game camera), banner {BannerState}, dust puffs {DustPuffs}, exhaust puffs {SmokePuffs}, stretched decks {StretchedDecks} (0 wanted for an owner picture), models [{ModelSizes}], "
                 + $"afterglow {(I.afterglow ? I.glowT.ToString("F0") + " s" : "off")}, sounds {SoundsStarted}, flights {Flights}, releases {Released}";
        }

        /// <summary>Play check, at every moment of play after the hand-back: stage actors a unit could meet (deck copies, tanks, chunks). 0 is the only good answer.</summary>
        public static int Leftovers()
        {
            if (!I || I.staged) return 0;
            int n = I.chunks.Count; foreach (var c in I.cars) if (c.root) n++;
            foreach (var s in I.spans) if (s.intact) n++;
            return n;
        }
    }
}
