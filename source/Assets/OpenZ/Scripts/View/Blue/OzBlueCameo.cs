// OpenZ (GPL-3.0)
// sidekick BS-01 in a level played as blue. The commander's cameo (ZodBriefing) is not installed in a blue level (OzPlayBlue.NoStoryIn);
// this component takes its place there and runs the same way:
//  1. The level-start BRIEFING: the sim waits (ZGame disabled: no head start), the camera flies the level's shot list (ZodFlyover: the
//     blue lists while the side is blue) under the dialog, EvilZod and the sidekick talk from the evil HQ in the comms window under the
//     letterbox bars with subtitles and speaker labels; SPACE / ENTER / ESC / a click skips; then the bars retract, the camera lands on
//     the blue start view and the normal start runs (the HUD, the start sting and the start lines are held until then, exactly as the
//     red briefing holds them: AudioDirector.HoldStartLines is chained).
//  2. The WIN NUKE scene (OzBlueCameo.Nuke.cs): at the fallen red fort, in the in-game comms window, in the commander's own slot of the
//     nuke intro (OpenZVoiceV3): the World-Domination-inator, the giant blue button, the press launches the strike.
//     scene's slot (OpenZVictory.Run), then the end screen.
// Voice: the approved joined takes (Resources/OpenZ/Blue/Voice), one 2D source on the commander's voice bus (Voice/Commander, voiceDb),
// ducking music / ambience / the robots' bus like the commander; the other voices wait (AudioDirector.ExternalVoiceBusy chained).
// Data: Resources/OpenZ/Blue/blue_scenes.json (OzBlueScenes). Stage: OzEvilStage. Never in bot matches, test mode, capture takes or
// automated checks; PlayerPrefs oz_zod_cameo 0 and oz_zod_briefing 0 switch it off like the commander's.
using System;
using System.Collections;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    [DefaultExecutionOrder(-54)]
    public sealed partial class OzBlueCameo : MonoBehaviour
    {
        public static OzBlueCameo I { get; private set; }
        public const string Tag = "[EvilZod]";
        [Header("Voice (AudioDirector bus Voice/Commander)")]
        [Tooltip("The blue voices' level on the voice bus (dB); the takes are -18 LUFS like the commander's")] public float voiceDb = -2f;
        public float duckMusicDb = -8f, duckAmbienceDb = -5f, duckAlanBusDb = -8f;
        [Tooltip("The window's static, dB relative to the voice gain")] public float staticEdgeDb = -1f, staticMidDb = -5f;
        public float briefingPreroll = 0.55f;
        /// <summary>
        /// EvilZod's words in the blue scenes (briefing, nuke scene, WIN,
        /// LOSE) play this many dB over the joined take; BS-01, the static, the stage sounds and the ducking stay at voiceDb. Eased in and
        /// out over <see cref="EvilZodRamp"/> s in the middle of the pause before / after each of his stretches (OzBlueScenes words,
        /// <see cref="EvilRuns"/>
        /// win / lose / nuke) before, +1.7 / +1.7 / -0.3 / +2.2 LU with 1 dB. 0 = the 1.2 mix; never more than <see cref="EvilZodDbMax"/>.</summary>
        [Tooltip("EvilZod's lines in the blue scenes, dB over the take (0 = as in 1.2; at most 1.5)")] public float evilZodDb = 1.0f;
        public const float EvilZodDbMax = 1.5f, EvilZodRamp = 0.1f, EvilZodEdgePad = 0.35f;
        /// <summary>
        /// scenes' comms window (briefing, nuke scene, WIN, LOSE) at this scale of the red window's size, same corner and margins (ZodLayout.scale:
        /// the whole window and its feed). The red windows and the battlefield card stay as they are.</summary>
        public static float WindowScale = 0.85f;

        enum Phase { Pending, Briefing, Handing, Play }
        sealed class Show
        {
            public string kind; public OzBlueScene scene; public AudioClip clip; public ZodLayout layout; public bool onScreen;
            public float t, preroll, end, voiceEnd, fade = 1f, fadeSpeed; public bool voiceOn, done, closing, pendingOpen; public int bi; public string speaker;
            /// <summary>1.3 item 19: EvilZod's stretches of the take (start / end pairs, s from the voice start).</summary>
            public float[] evilRuns;
            public void Abort(float seconds) { if (fadeSpeed <= 0f) fadeSpeed = 1f / Mathf.Max(0.05f, seconds); }
        }

        static bool hooked, pendingFromFlow;
        static readonly HashSet<string> briefed = new HashSet<string>();
        Phase phase = Phase.Play; bool fromFlow;
        ZGame game; OpenZHud hud; PlayerInput input; RtsCamera rts; ZodComms comms; OzEvilStage stage; ZodFlyover fly;
        AudioSource voice, staticEdge, staticMid; readonly Dictionary<string, AudioClip> statics = new Dictionary<string, AudioClip>();
        Show show; bool skipped; float nextDuck, lastBusyAt = -100f; readonly float[] rmsBuf = new float[1024]; float mouth;
        string hostBefore; bool hostSet;
        /// <summary>Checks: scenes started by kind this session; the briefing's skip; the last scene's clock.</summary>
        public static int BriefingsPlayed { get; private set; }
        public static int NukeScenesPlayed { get; private set; }
        public static int EndingsPlayed { get; private set; }
        public static string LastSceneKind { get; private set; } = "";
        public static float LastSceneT => I != null && I.show != null ? I.show.t : -99f;

        // ================================================================================================ boot
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            briefed.Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded;
            if (!hooked)
            {   // chained, never replaced: the commander's rules keep working on the red side
                hooked = true;
                var prevHold = AudioDirector.HoldStartLines;
                AudioDirector.HoldStartLines = () => HoldingStart() || (prevHold != null && prevHold());
                var prevBusy = AudioDirector.ExternalVoiceBusy;
                AudioDirector.ExternalVoiceBusy = prevBusy == null ? (Func<bool>)BlueSpeaking : () => BlueSpeaking() || prevBusy();
                // scene-load hook runs first (FlybySky's preload asks this at the level load)
                var prevExpect = FlybySky.ExpectStartFlyover;
                FlybySky.ExpectStartFlyover = () => BlueBriefingExpected() || (prevExpect != null && prevExpect());
            }
            if (!I) Install(SceneManager.GetActiveScene(), false);
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode m) { if (m == LoadSceneMode.Single) Install(s, true); }

        /// <summary>FlybySky's level-load question: a blue level whose briefing will play (asked after every Awake of the new scene).</summary>
        static bool BlueBriefingExpected()
        {
            if (OpenZ.Multiplayer.DuelRuntime.Active) return false;
            if (I) return I.phase == Phase.Pending || I.phase == Phase.Briefing;
            var g = ZGame.I;
            if (!g || !Application.isPlaying || !ZodBriefing.Enabled || !OzPlayBlue.NoStoryIn(g.gameObject.scene.name)) return false;
            if (PlayerPrefs.GetInt(ZodBriefing.PrefBriefing, 1) <= 0 || GameBoot.TestMode || CaptureKit.Enabled || GateBattery.Running) return false;
            if ((g.blueBot && g.redBot) || PlayerPrefs.GetInt("oz_botmatch", 0) == 1) return false;
            return OzBlueScenes.Shared.Ready(OzBlueScenes.Briefing, out _);
        }

        static void Install(Scene s, bool fromFlow)
        {
            if (OpenZ.Multiplayer.DuelRuntime.Active) return;
            if (!Application.isPlaying || !s.IsValid() || !ZodBriefing.Enabled || !OzPlayBlue.NoStoryIn(s.name)) return;
            if (I && I.gameObject.scene == s) return;
            if (OzUeHooks.NoCameo(s)) return;
            var go = new GameObject("EvilZodCameo");
            if (go.scene != s) SceneManager.MoveGameObjectToScene(go, s);
            pendingFromFlow = fromFlow;
            go.AddComponent<OzBlueCameo>();
        }

        /// <summary>The frame step of the scenes' own clocks (the window's opening and tail, the stage's moves, the flyover): the capture step
        /// while a take records at a fixed step (Time.captureFramerate: Time.unscaledDeltaTime stays real time then, as the game's capture
        /// code knows, while the voice runs on the take's clock), else the real frame time.</summary>
        public static float Step => Time.captureFramerate > 0 ? 1f / Time.captureFramerate : Time.unscaledDeltaTime;

        // frame and the next are seconds long): the briefing's clocks (the window's opening, the voice's preroll, the flyover's shot clock)
        // wait until the game draws normally, so the first picture is the flyover's first frame with the window just opening
        bool settling; int settleFrames; float settleReal;
        /// <summary>A frame counts as normal when it is no longer than this (real s); the settle ends after SettleFrames of them in a row.</summary>
        public static float SettleFrameMax = 0.2f; public static int SettleFrames = 2; public static float SettleMaxSeconds = 15f;
        public static int SettledAfterFrames { get; private set; } = -1;
        void TickSettle()
        {
            if (!settling) return;
            float real = Time.unscaledDeltaTime; settleReal += real;
            bool normal = Time.captureFramerate > 0 || real <= SettleFrameMax;
            settleFrames = normal ? settleFrames + 1 : 0;
            if (settleFrames >= SettleFrames || settleReal > SettleMaxSeconds)
            {
                settling = false; SettledAfterFrames = Time.frameCount;
                if (show != null && show.pendingOpen) { show.pendingOpen = false; comms.Open(show.layout, stage ? stage.Feed : null); }
                Debug.Log($"{Tag} the start settled ({settleReal:F2} s of real time at the level's start, frame {Time.frameCount}): the briefing's clocks run from here");
            }
        }

        static bool HoldingStart() => I && I.phase != Phase.Play;
        static bool BlueSpeaking() => I && I.show != null && !I.show.done && I.show.t < I.show.voiceEnd + 0.15f && !AudioListener.pause;
        /// <summary>True while a blue voice line is on (the other voices wait).</summary>
        public static bool Speaking => BlueSpeaking();
        /// <summary>True while the blue briefing owns the game (sim waiting, camera flying).</summary>
        public static bool BriefingActive => I && (I.phase == Phase.Briefing || I.phase == Phase.Handing);
        public static bool SceneShowing => I && I.show != null && !I.show.done;

        void Awake()
        {
            if (I && I != this && I.gameObject.scene == gameObject.scene) { Destroy(gameObject); return; }
            I = this; fromFlow = pendingFromFlow; pendingFromFlow = false;
            game = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!game) { Destroy(gameObject); return; }
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false; voice.loop = false; voice.spatialBlend = 0f; voice.priority = 8; voice.dopplerLevel = 0f; voice.bypassReverbZones = true; voice.ignoreListenerPause = false;
            AudioDirector.RegisterExternalVoice(voice);
            staticEdge = NewStaticSource(); staticMid = NewStaticSource();
            if (WantsBriefing(out var why)) { phase = Phase.Pending; ZodFlyover.ExpectBlueStart("blue briefing"); }
            else Debug.Log($"{Tag} level {game.level} as blue: no briefing ({why})");
        }

        AudioSource NewStaticSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = false; s.spatialBlend = 0f; s.priority = 40; s.dopplerLevel = 0f; s.bypassReverbZones = true; s.ignoreListenerPause = false;
            return s;
        }

        bool Bots => game && ((game.blueBot && game.redBot) || PlayerPrefs.GetInt("oz_botmatch", 0) == 1);
        bool Automated
        {
            get
            {
                if (Time.unscaledTime < nextAutoCheck) return automated;
                nextAutoCheck = Time.unscaledTime + 1f;
                automated = CaptureKit.Enabled || GateBattery.Running || NightLumaCheck.Running || OzFpsBench.Running || FindFirstObjectByType<OzCombatSoundCheck>() || FindFirstObjectByType<IdleLegsCheck>() || FindFirstObjectByType<CombatViewCheck>()
#if UNITY_EDITOR
                            || OzBlueFlyoverProof.I                                                     // the blue flyover proof (editor only, as its class) flies its own briefing (its runner): a second flight would take its camera's home pose and its drive's counts
#endif
                            ;                                                                           // players compile the same expression without the editor-only term
                return automated;
            }
        }
        float nextAutoCheck = -1f; bool automated;

        bool WantsBriefing(out string why)
        {
            int pref = PlayerPrefs.GetInt(ZodBriefing.PrefBriefing, 1);
            why = pref <= 0 ? ZodBriefing.PrefBriefing + " = 0" : Bots ? "bot match" : GameBoot.TestMode ? "test mode" : CaptureKit.Enabled ? "capture kit" : Automated ? "an automated check is running"
                : !OzBlueScenes.Shared.Ready(OzBlueScenes.Briefing, out var w) ? w : null;
            return why == null;
        }

        void Start()
        {
            hud = OpenZHud.I ? OpenZHud.I : FindFirstObjectByType<OpenZHud>();
            input = FindFirstObjectByType<PlayerInput>();
            rts = FindFirstObjectByType<RtsCamera>();
            if (phase == Phase.Pending) StartCoroutine(BriefingRoutine());
        }

        /// <summary>The owner's weight rule (load where shown, unload after): the briefing's stage is destroyed once its window has folded
        /// and the robots' models and textures are swept 1.5 s later; the nuke scene and the endings build it again when they begin
        /// (EnsureStage, the match decided by then). The red ZodStage keeps its stage for the level (SetLive(false) between its scenes);
        /// this one is heavier (two 2k-textured robots, about 60 MB), so it goes.</summary>
        void UnloadStage()
        {
            if (!stage) return;
            var go = stage.gameObject; stage = null;
            Destroy(go);
            StartCoroutine(SweepSoon(1.5f));
            Debug.Log($"{Tag} stage unloaded after its window folded (built again by the next scene that needs it)");
        }
        IEnumerator SweepSoon(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (stage || show != null) yield break;                                                     // a new scene already built it: nothing to sweep
            yield return Resources.UnloadUnusedAssets();
        }

        void EnsureStage()
        {
            if (!stage) { stage = OzEvilStage.Create(transform, game ? game.level : "L02"); stage.SetLive(false); }
            else stage.SetBoardLevel(game ? game.level : "L02");
        }
        void EnsureComms() { comms ??= new ZodComms(transform); comms.SetUiScale(1f); }

        void OnDestroy()
        {
            if (I == this) I = null;
            if (phase != Phase.Play) Cursor.visible = true;
            RestoreHost();
            comms?.Destroy();
        }

        // ================================================================================================ the briefing
        IEnumerator BriefingRoutine()
        {
            phase = Phase.Briefing;
            settling = true; settleFrames = 0; settleReal = 0f;                                         // the clocks wait for the start's long frames (TickSettle)
            bool handed = false, hudStarted = false;
            try
            {
                EnsureStage(); EnsureComms();
                game.enabled = false;                                                                   // the sim waits: nobody gets a head start
                if (input) input.enabled = false;
                if (hud && hud.Canvas == null) hud.Prebuild();                                          // under the letterbox, not as a hitch on the hand-over frame
                hudStarted = hud && hud.Canvas != null;
                if (hud) { if (hudStarted) hud.SetVisible(false); hud.enabled = false; }
                fly = rts ? new ZodFlyover(rts, game) : null;
                if (fly != null) fly.VoiceOn = () => voice && voice.isPlaying;
                // its first Update, after this Start: shot 1 was aimed at the scene's saved sun, 40-50 deg off the real sunrise: no sun disc)
                var dn = FindFirstObjectByType<DayNight>(); if (dn) dn.Apply(dn.timeOfDay);
                bool flying = fly != null && fly.Begin("overview");                                     // the blue shot list while the side is blue
                Cursor.visible = false;
                var def = Array.Find(OzLevelDef.All, d => d.Id == game.level);
                comms.Letterbox(true, "MISSION BRIEFING", "OPERATION " + game.level + " · " + (def != null ? def.Name : game.level));
                var lay = ZodLayout.Briefing(); lay.eyebrow = "INCOMING TRANSMISSION"; lay.channel = "EVIL HQ · CH 02"; lay.scale = WindowScale;
                bool ok = StartShow("briefing", OzVolcanic.BlueBriefingScene(game ? game.level : null), lay, false, briefingPreroll);
                if (ok && flying && show != null)
                {
                    var line = FlyLine(show.scene);
                    float pre = show.preroll, end = show.end;
                    ZodShotList.BriefingRoom(game.level, line, ref pre, show.voiceEnd, ref end);        // the blue list's quiet opening and its last shot's room
                    show.preroll = pre; show.end = Mathf.Max(show.end, end); show.t = -pre;
                    fly.PlanCine(show.preroll, show.end, line, () => show != null && !show.done ? show.t + show.preroll : float.NaN);
                }
                if (ok) BriefingsPlayed++;
                Debug.Log($"{Tag} blue briefing on {game.level}: {(ok ? "playing" : "SCENE MISSING")}, fly-over {(flying ? "on" : "off (no RtsCamera)")}, sim waits, HUD {(hudStarted ? "hidden" : "deferred")}");
                float started = Time.unscaledTime, alive = 0f, budget = show != null ? show.preroll + show.end + 6f : 0f;
                while (show != null && !show.done)
                {
                    if (!Paused) alive += Mathf.Min(Step, 0.1f);
                    if (alive > budget) { Debug.LogWarning($"{Tag} briefing watchdog: still running after {alive:F0} s, handing the level back"); if (voice) voice.Stop(); show.done = true; skipped = true; break; }
                    if (!skipped && Time.unscaledTime - started > 0.35f && SkipPressed()) { skipped = true; show.Abort(0.22f); AudioDirector.UI("ui_cancel"); Debug.Log($"{Tag} briefing skipped at {show.t:F1} s"); }
                    yield return null;
                }
                phase = Phase.Handing;
                comms.Letterbox(false);
                if (flying)
                {
                    fly.HandBack(skipped);
                    float waited = 0f;
                    while (fly.HomeError > 0.04f && waited < 1.6f) { waited += Mathf.Min(Step, 0.1f); yield return null; }
                    fly.End();
                }
                game.enabled = true;
                Cursor.visible = true;
                yield return null;                                                                      // the skip press never reaches the game
                if (input) input.enabled = true;
                if (hud) { hud.enabled = true; if (hudStarted) hud.SetVisible(true); }
                briefed.Add(game.level);
                phase = Phase.Play;                                                                     // AudioDirector: the start sting, the start lines
                handed = true;
                Debug.Log($"{Tag} blue briefing handed over ({(skipped ? "skipped" : "complete")}): sim running, input + HUD back");
            }
            finally { if (!handed) EmergencyHandBack(hudStarted); }
        }

        void EmergencyHandBack(bool hudStarted)
        {
            Debug.LogWarning($"{Tag} briefing aborted by an error: handing the level back to the player");
            try { if (voice) voice.Stop(); if (show != null) show.done = true; show = null; } catch (Exception) { }
            try { comms?.Letterbox(false); comms?.Close(); } catch (Exception) { }
            try { if (stage) stage.SetLive(false); } catch (Exception) { }
            try { fly?.End(); } catch (Exception) { }
            if (game) game.enabled = true;
            Cursor.visible = true;
            if (input) input.enabled = true;
            try { if (hud) { hud.enabled = true; if (hudStarted) hud.SetVisible(true); } } catch (Exception) { }
            phase = Phase.Play;
        }

        /// <summary>The dialog as a commander line for the flyover's cuts: five cues (the fourth line split before "Now move") and the
        /// words with their times, so the blue shot list cuts on "Same", "Where" and "Now" (k75c's entry point).</summary>
        static ZodLine FlyLine(OzBlueScene s)
        {
            var cues = new List<ZodCue>(); var words = new List<ZodWord>();
            foreach (var l in s.lines) cues.Add(new ZodCue { text = l.text, start = l.start, end = l.end });
            float now = -1f;
            if (s.words != null) foreach (var w in s.words) { words.Add(new ZodWord { word = w.w, start = w.s, end = w.e }); if (now < 0f && ZodLibrary.Norm(w.w) == "now") now = w.s; }
            if (cues.Count == 4 && now > cues[3].start + 0.5f)
            {
                var last = cues[3]; int cut = last.text.IndexOf("Now", StringComparison.Ordinal);
                cues[3] = new ZodCue { text = cut > 0 ? last.text.Substring(0, cut).Trim() : last.text, start = last.start, end = now - 0.02f };
                cues.Add(new ZodCue { text = cut > 0 ? last.text.Substring(cut) : "", start = now, end = last.end });
            }
            return new ZodLine { id = "blue_" + s.id, group = "briefing", text = "", duration = s.length, cues = cues.ToArray(), words = words.ToArray() };
        }

        static bool SkipPressed()
        {
            var kb = Keyboard.current; var m = Mouse.current;
            return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                || (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame));
        }

        // ================================================================================================ a scene
        /// <summary>Starts a scene: in the comms window (layout) or on the screen (onScreen: the letterbox bars draw the subtitles).</summary>
        bool StartShow(string kind, string id, ZodLayout layout, bool onScreen, float preroll)
        {
            var lib = OzBlueScenes.Shared;
            if (!lib.Ready(id, out var why)) { Debug.LogWarning($"{Tag} scene {id} not playable: {why}"); return false; }
            var sc = lib.Scene(id); var clip = lib.Clip(sc);
            EnsureStage(); EnsureComms();
            var p = new Show { kind = kind, scene = sc, clip = clip, layout = layout, onScreen = onScreen, preroll = Mathf.Max(preroll, sc.preroll) };
            p.voiceEnd = Mathf.Min(clip.length, sc.length > 0f ? sc.length : clip.length);
            p.end = p.voiceEnd + Mathf.Max(0.3f, sc.tail);
            p.t = -p.preroll;
            p.evilRuns = EvilRuns(sc);                                                                      // 1.3 item 19
            show = p; skipped = false; mouth = 0f;
            SetHost(OzBlueScenes.EvilZodName);
            stage.VoiceGain = VoiceGain();
            if (onScreen) { stage.SetLive(true, true); comms.LetterboxCue = true; }
            else
            {
                var px = comms.WellPixels(layout);
                stage.EnsureFeed(px.x, px.y);
                stage.SetLive(true, false);
                if (settling && kind == "briefing") p.pendingOpen = true;                                   // opened when the start has settled (TickSettle)
                else comms.Open(layout, stage.Feed);
            }
            stage.BeginScene(sc, onScreen);
            stage.Frozen = false;
            PlayStatic(staticEdge, "open", staticEdgeDb);
            voice.Stop(); voice.clip = clip; voice.volume = VoiceGain();
            LastSceneKind = kind;
            Debug.Log($"{Tag} {kind}: scene {id} ({clip.length:F2} s, {sc.beats.Length} beats, {sc.lines.Length} lines, preroll {p.preroll:F2}, end {p.end:F2}, {(onScreen ? "full screen" : "window")})");
            return true;
        }

        void SetHost(string name) { if (!hostSet) { hostBefore = ZodComms.Host; hostSet = true; } ZodComms.Host = name; }
        void RestoreHost() { if (hostSet) { ZodComms.Host = hostBefore; hostSet = false; } }

        float VoiceGain()
        {
            float bus = AudioDirector.I ? AudioDirector.I.VoiceBusLinear("Voice/Commander") : PlayerPrefs.GetFloat("oz_vol_voice", 1f);
            return Mathf.Clamp01(bus * Mathf.Pow(10f, voiceDb / 20f));
        }

        /// <summary>1.3 item 19: EvilZod's stretches of a scene as start / end pairs: the runs of his consecutive words (or his line
        /// windows when a scene has no words), each edge moved out into the MIDDLE of the pause to the other speaker (at most
        /// <see cref="EvilZodEdgePad"/>
        /// the audible word does (LOSE "everyone!" is loudest 5.78-5.92 s but its word ends at 5.72; "myself!" sounds 13.22-13.36, ends
        /// at 13.2), so a stretch that stopped at the last word's end left his loudest syllables at the old level. The pauses between the
        /// two speakers are digital silence, so their middle is the clean place for the ease. Where the take overlaps the two voices (the
        /// nuke scene: BS-01 starts 0.08 s before EvilZod's last word ends) the edge stays on his word's end, as before.</summary>
        public static float[] EvilRuns(OzBlueScene s)
        {
            var seg = new List<Vector3>();                                                                  // x start, y end, z 1 = EvilZod
            if (s?.words != null && s.words.Length > 0)
            { foreach (var w in s.words) if (w != null) seg.Add(new Vector3(w.s, w.e, w.who == OzBlueScenes.EvilZod ? 1f : 0f)); }
            else if (s?.lines != null)
                foreach (var l in s.lines) if (l != null) seg.Add(new Vector3(l.start, l.end, l.who == OzBlueScenes.EvilZod ? 1f : 0f));
            var r = new List<float>();
            for (int i = 0; i < seg.Count; i++)
            {
                if (seg[i].z < 0.5f) continue;
                int j = i; float a = seg[i].x, b = seg[i].y;
                while (j + 1 < seg.Count && seg[j + 1].z > 0.5f) { j++; b = Mathf.Max(b, seg[j].y); }
                float before = i > 0 ? seg[i - 1].y : float.NegativeInfinity, after = j + 1 < seg.Count ? seg[j + 1].x : float.PositiveInfinity;
                r.Add(a - EdgePad(before, a)); r.Add(b + EdgePad(b, after));
                i = j;
            }
            return r.ToArray();
        }

        /// <summary>1.3 item 19: how far an edge of EvilZod's stretch moves out into the pause from..to (s): to the middle of the pause
        /// less half the ramp, at most EvilZodEdgePad (the full pad when there is no other speaker on that side); 0 when the pause is
        /// shorter than the ramp (the voices overlap or nearly).</summary>
        static float EdgePad(float from, float to)
        {
            float gap = to - from;
            if (float.IsInfinity(gap)) return EvilZodEdgePad;
            return gap < EvilZodRamp ? 0f : Mathf.Min(EvilZodEdgePad, gap * 0.5f - EvilZodRamp * 0.5f);
        }

        /// <summary>1.3 item 19: the voice multiplier at time t of the take: 10^(db/20) inside EvilZod's stretches, 1 elsewhere, eased
        /// linearly over EvilZodRamp s just outside each stretch (in the pause between the speakers).</summary>
        public static float EvilGainAt(float[] runs, float t, float db)
        {
            db = Mathf.Clamp(db, 0f, EvilZodDbMax);
            if (db <= 0f || runs == null) return 1f;
            float w = 0f;
            for (int i = 0; i + 1 < runs.Length; i += 2)
                w = Mathf.Max(w, Mathf.Clamp01(Mathf.Min(t - (runs[i] - EvilZodRamp), runs[i + 1] + EvilZodRamp - t) / EvilZodRamp));
            return Mathf.Lerp(1f, Mathf.Pow(10f, db / 20f), w);
        }

        float EvilGain(Show p) => p != null && p.voiceOn ? EvilGainAt(p.evilRuns, p.t, evilZodDb) : 1f;
        bool Paused => AudioListener.pause || (hud && hud.Pause && hud.Pause.IsOpen) || (game && game.paused && phase == Phase.Play);

        void PlayStatic(AudioSource src, string name, float db)
        {
            if (!src) return;
            if (!statics.TryGetValue(name, out var c)) statics[name] = c = Resources.Load<AudioClip>("OpenZ/Zod/Sfx/comms_static_" + name);
            if (!c) return;
            src.Stop(); src.clip = c; src.pitch = UnityEngine.Random.Range(0.93f, 1.07f);
            src.volume = Mathf.Clamp01(VoiceGain() * Mathf.Pow(10f, db / 20f)); src.Play();
            OzSoundLog.Add("EvilZod", "comms_static_" + name, c.name, "played", src.volume);
        }

        float LiveMouth()
        {
            if (!voice || !voice.isPlaying) return 0f;
            voice.GetOutputData(rmsBuf, 0);
            double s = 0; for (int i = 0; i < rmsBuf.Length; i++) s += rmsBuf[i] * rmsBuf[i];
            float db = 10f * Mathf.Log10((float)(s / rmsBuf.Length) + 1e-10f) - 20f * Mathf.Log10(Mathf.Max(0.05f, voice.volume));
            db += OzEvilStage.LavaMouthDb(voice.time);                                                   // level 7: the jaw follows the approved take, not this file's per-line gain
            float k = Mathf.Clamp01((db + 36f) / 20f);
            return k * k;
        }

        void TickShow(float dt)
        {
            var p = show; if (p == null) return;
            if (settling) dt = 0f;                                                                       // the preroll waits for the start's long frames
            bool paused = Paused;
            if (stage) stage.Frozen = paused;
            if (!paused)
            {
                if (!p.voiceOn)
                {
                    p.t += dt;
                    if (p.t >= 0f && p.fadeSpeed <= 0f) { voice.volume = VoiceGain(); voice.Play(); p.voiceOn = true; p.t = 0f; }
                }
                else if (voice.isPlaying) p.t = voice.timeSamples / (float)Mathf.Max(1, p.clip.frequency);
                else p.t += dt;
                if (staticEdge && staticEdge.isPlaying && p.voiceOn && !p.closing)
                {
                    staticEdge.volume = Mathf.MoveTowards(staticEdge.volume, 0f, dt / 0.12f * Mathf.Max(0.05f, VoiceGain()));
                    if (staticEdge.volume <= 0.001f) staticEdge.Stop();
                }
                while (p.bi < p.scene.beats.Length && p.scene.beats[p.bi].t <= p.t) { var b = p.scene.beats[p.bi++]; if (stage) stage.Beat(b); if (b.who == "cam" && b.beat != "kick" && b.beat != "lock" && b.a <= 0f && comms != null && !p.onScreen) comms.Glitch(0.3f, 0.07f); }
                if (p.fadeSpeed > 0f) { p.fade = Mathf.MoveTowards(p.fade, 0f, dt * p.fadeSpeed); if (p.fade <= 0f) { voice.Stop(); p.done = true; } }
                if (p.t >= p.end) p.done = true;
            }
            voice.volume = Mathf.Clamp01(VoiceGain() * p.fade * EvilGain(p));                            // 1.3 item 19: EvilZod's words a tiny step up
            if (stage) stage.VoiceGain = VoiceGain();
            // who talks: the line windows gate the live level
            string who = p.voiceOn && p.fadeSpeed <= 0f ? OzBlueScenes.SpeakerAt(p.scene, p.t) : null;
            float m = who != null ? LiveMouth() : 0f;
            mouth += (m - mouth) * (1f - Mathf.Exp(-Mathf.Max(dt, 0.0001f) / (m > mouth ? 0.025f : 0.045f)));
            if (stage) stage.Talk(who, m);
            // subtitles and the speaker label
            var line = p.voiceOn && p.fadeSpeed <= 0f ? OzBlueScenes.LineAt(p.scene, p.t) : null;
            string label = line == null ? null : line.who == OzBlueScenes.EvilZod ? OzBlueScenes.EvilZodLabel : OzBlueScenes.SidekickLabel;
            if (label != null && label != p.speaker)
            {
                p.speaker = label;
                if (p.onScreen || p.layout.sub == ZodSubMode.None) comms.LetterboxSpeaker(label);
                else { ZodComms.Host = label; comms.ApplyRank(); }
            }
            comms.SetCue(line != null ? line.text : null);
            comms.SetVu(mouth);
            if (p.kind == "briefing") comms.SetProgress((p.t + p.preroll) / Mathf.Max(1f, p.end + p.preroll));
            if (voice.isPlaying && !paused && Time.unscaledTime >= nextDuck)
            {
                nextDuck = Time.unscaledTime + 0.3f;
                if (AudioDirector.I) AudioDirector.I.AddDuck(duckMusicDb, 0f, duckAmbienceDb, duckAlanBusDb, 0.55f, 0.2f, 1.0f);
                if (CombatAudio.I) CombatAudio.I.DuckFor(0.55f, -6f, -2f);
            }
            if (p.done && !p.closing)
            {
                p.closing = true;
                comms.SetCue(null); comms.SetVu(0f);
                if (!p.onScreen) { comms.Close(); PlayStatic(staticEdge, "close", staticEdgeDb); }
                OnShowDone(p);
            }
        }

        /// <summary>A window scene closed: the stage goes dark when the window has folded (after the briefing it is unloaded: UnloadStage);
        /// a screen scene stays on its last picture.</summary>
        void OnShowDone(Show p) { if (p.onScreen) { show = null; RestoreHost(); } }

        void Update()
        {
            if (AudioDirector.VoiceBusy) lastBusyAt = Time.unscaledTime;
            TickSettle();
            TickShow(Mathf.Min(Step, 0.1f));
            if (show != null && show.closing && !show.onScreen && comms != null && comms.Closed)
            {
                bool briefing = show.kind == "briefing"; string kindWas = show.kind;
                show = null; RestoreHost();
                if (stage) { if (briefing || kindWas == "ending") UnloadStage(); else stage.SetLive(false); }
            }
        }

        void LateUpdate()
        {
            if (fly != null && fly.Active && !AudioListener.pause) fly.Tick(settling ? 0f : Mathf.Min(Step, 0.1f));   // shot 1's first pose until the start settled
            comms?.Tick();
        }

        /// <summary>One-line state for the Play checks.</summary>
        public static string Status()
        {
            if (!I) return "OzBlueCameo: none in this scene";
            var s = I.show;
            return $"OzBlueCameo: level {(I.game ? I.game.level : "?")}, phase {I.phase}, show {(s != null ? s.kind + " t=" + s.t.ToString("F2") + (s.done ? " done" : "") : "none")}, "
                 + $"stage {(I.stage ? (I.stage.HasEvilZod ? "EvilZod" : "NO EVILZOD") + (I.stage.HasSidekick ? " + sidekick" : " NO SIDEKICK") + ", live " + I.stage.Live : "not built")}, "
                 + $"briefings {BriefingsPlayed}, nuke scenes {NukeScenesPlayed}, endings {EndingsPlayed}, voice gain {I.VoiceGain():F2}" + (I.stage ? "; " + I.stage.Status() : "");
        }
        public string StageStatus => stage ? stage.Status() : "no stage";
        public OzEvilStage Stage => stage;
        public ZodComms Comms => comms;
        public bool Skipped => skipped;

        /// <summary>Play check: the blue briefing again, now (the level in play).</summary>
        public static string DebugBriefing()
        {
            if (!I) return "no OzBlueCameo in this scene";
            if (I.phase != Phase.Play || I.show != null) return "busy: " + I.phase + (I.show != null ? " " + I.show.kind : "");
            I.skipped = false; I.StartCoroutine(I.BriefingRoutine());
            return "blue briefing started: " + I.game.level;
        }
#if UNITY_EDITOR
        /// <summary>Play proofs: the briefing skipped as the SPACE key does.</summary>
        public static bool ProofSkipBriefing()
        {
            if (!I || I.phase != Phase.Briefing || I.show == null || I.show.done || I.skipped) return false;
            I.skipped = true; I.show.Abort(0.22f); AudioDirector.UI("ui_cancel");
            return true;
        }
#endif
    }
}
