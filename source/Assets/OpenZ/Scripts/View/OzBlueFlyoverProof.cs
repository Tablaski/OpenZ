#if UNITY_EDITOR
// OpenZ (GPL-3.0) - version 1.2, the blue flyovers (ZodFlyover.Blue.cs): the Play proof, the MP4 recording and the start frame trace, driven
// from the menu OpenZ/V1.2/Blue Flyovers (Editor/OzBlueFlyoverMenu.cs). Started on the MainMenu scene in Play it deploys the level as the player
// does with the side set to blue (the PLAY AS BLUE box on, or whatever arms the blue side: ArmBlue), survives the scene load, freezes the
// simulation on the level's first frame (as the briefing does), hides the HUD, opens the briefing letterbox, plays the approved blue
// briefing take and flies the level's blue shot list under it with the cuts on the dialog's breaks (ZodShotList.BlueBriefingLine), then hands
// the camera back to the blue start view and gives the level back. Started inside a level that runs as blue it does the same there.
// Until the blue briefing kit drives the flight itself, this is the way to see it.
//  Proof     counted PASS / FAIL lines: the shots in order, the cuts on the line breaks, the sun disc in the frame (and the painted sky full) in
//            shot 1, our fort in shots 1 and 4, the red fort in shots 2 and 3, the painted sky and the jungle's low look on while it flies,
//            the hand-back exactly on the blue start view (the RTS camera's own pose, the focus the level placed), SPACE / ESC / a click
//            skip (dip and cut home); pictures of every shot (its middle), the first frame and the landing.
//  Record    the same flight recorded as an MP4 with the game's sound (the voices, the wind swell, the level's ambience): the Game view
//            set to 1920 x 1080, fixed 30 steps a second (Time.captureFramerate), every finished frame to a JPG and the mixed sound rendered
//            in step (AudioRenderer, as the game's own capture kit does), then ffmpeg joins them into
//            blue_team/videos/flyover_<level>.mp4 (x264 CRF 17, AAC) and a frame sheet (2 frames a second) beside it. A GPU at 80 C or more
//            holds the take until it is under 75 C.
//  Skip test the flight skipped at 2.1 s of the dialog: the voice fades, the dip to black, the cut home within 1.6 s.
//  Trace     owner item 86: OzStartFrameTrace armed, the level deployed from the menu as blue or as red; the trace writes the first 10 s.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    public sealed partial class OzBlueFlyoverProof : MonoBehaviour                                   // + OzBlueFlyoverProof.Checks2.cs: the start lines, the ground rays, the robots alive
    {
        public enum Job { Proof, Record, SkipTest, TraceBlue, TraceRed }
        public static OzBlueFlyoverProof I { get; private set; }
        public static string Report = "idle";
        /// <summary>The take: fixed steps a second and the JPG quality of the recording.</summary>
        public static int Fps = 30, Jpg = 95;
        /// <summary>The briefing's window-open to first-word time before the shot list's own quiet opening (ZodBriefing.briefingPreroll).</summary>
        public static float Preroll = 0.55f, Tail = 0.35f, SkipAt = 2.1f;

        string lv; Job job; OzLevelDef def;
        readonly StringBuilder sb = new StringBuilder(); int fails, passes; string dir; bool boxWas, boxChanged;
        ZGame game; RtsCamera rts; Camera cam; OpenZHud hud; PlayerInput input; ZodComms comms; ZodFlyover fly; AudioSource voice; AudioClip clip; ZodLine line;
        float preroll, end, pt; bool voiceOn, skipped, flightDone, fading; float fade = 1f;
        // per shot: frames, the subjects inside the frame, the cut time seen
        readonly int[] shotFrames = new int[8], blueIn = new int[8], redIn = new int[8], sunIn = new int[8]; readonly float[] cutSeen = new float[8]; readonly bool[] shotPictured = new bool[8];
        int lastShot = -1; bool skyLive, lowLookSeen, dipSeen, firstPictured;
        // the painted sky that draws the sun disc loads with the flight and fades in (FlybySky): when it is full, the shot 1 frames that show the drawn disc
        float skyFullAt = -1f, cut2At = -1f; int sunDrawn;
        // recording
        bool recording, audioStarted, flightBegun, holding; int frameNo, ch = 2, rate = 48000, per = 1600, holdFrames, renderFails, dropped; readonly List<float> samples = new List<float>(); string recDir; float peak;

        /// <summary>The flight's own clock step: one capture step a frame while the take runs at fixed steps (Time.unscaledDeltaTime stays real
        /// time under Time.captureFramerate, as the game's capture code knows), else the real frame time.</summary>
        static float Step() => Time.captureFramerate > 0 ? 1f / Time.captureFramerate : Mathf.Min(Time.unscaledDeltaTime, 0.1f);

        public static string Folder(string level)
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
                if (Directory.Exists(Path.Combine(d.FullName, "work", "records")))
                    return Path.Combine(d.FullName, "work", "records", "milestones", "blue_flyovers_k75c", level ?? "");
            return Path.Combine(Application.persistentDataPath, "blue_flyovers_k75c", level ?? "");
        }

        /// <summary>The staging folder of this version (the one with the blue team's approved voices), found from the project's place; "" = none.</summary>
        public static string Staging()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                string st = Path.Combine(d.FullName, "work", "staging");
                if (!Directory.Exists(st)) continue;
                foreach (var v in Directory.GetDirectories(st, "v1_2_*"))
                    if (File.Exists(Path.Combine(v, "blue_team", "voice_final", "scene_briefing.wav"))) return v.Replace('\\', '/');
                return "";
            }
            return "";
        }

        public static void Run(string level, Job job)
        {
            if (I) { Debug.LogWarning("[OzBlueFlyoverProof] already running (" + I.lv + " " + I.job + ")"); return; }
            Report = "running"; var go = new GameObject("OzBlueFlyoverProof"); DontDestroyOnLoad(go);
            var p = go.AddComponent<OzBlueFlyoverProof>(); p.lv = level; p.job = job;
        }

        void Awake() { I = this; }
        void Start() { def = Array.Find(OzLevelDef.All, d => d.Id == lv); dir = Folder(lv); Directory.CreateDirectory(dir); StartCoroutine(All()); }
        void OnDestroy()
        {
            if (I == this) I = null;
            if (boxChanged) { OzPlayBlue.Box = boxWas; Debug.Log("[OzBlueFlyoverProof] the side box put back " + (boxWas ? "ON" : "OFF")); }
            if (recording) StopRecording(false);
            if (Time.captureFramerate != 0) Time.captureFramerate = 0;
            if (comms != null) { comms.Destroy(); comms = null; }
            if (fly != null && fly.Active) fly.End();
            if (game) { game.enabled = true; }
            if (input) input.enabled = true;
            if (hud) { hud.enabled = true; hud.SetVisible(true); }
            Cursor.visible = true;
        }

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzBlueFlyoverProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }

        /// <summary>The blue side for the next DEPLOY: the PLAY AS BLUE box (saved); a later faction screen that arms the side another way
        /// changes this one line.</summary>
        void ArmSide(bool blue)
        {
            boxWas = OzPlayBlue.Box;
            if (OzPlayBlue.Box != blue) { OzPlayBlue.Box = blue; boxChanged = true; }
        }

        // ================================================================================================================ the whole run
        IEnumerator All()
        {
            if (def == null) { Check("level " + lv + " is a level of the game", false); Finish(); yield break; }
            bool blue = job != Job.TraceRed;
            if (!ZGame.I)
            {   // from the main menu: deploy as the player does
                var m = FindFirstObjectByType<OpenZMainMenu>();
                if (!m) { Check("the MainMenu scene (or a blue level) is in Play", false); Finish(); yield break; }
                ArmSide(blue);
                if (job == Job.TraceBlue || job == Job.TraceRed) OzStartFrameTrace.Arm(job == Job.TraceBlue ? "menu_start" : "menu_start", 10f);
                yield return Real(0.8f);
                m.ProofSelect(); yield return Real(0.8f);
                m.ProofFocus(Array.FindIndex(OzLevelDef.All, d => d.Id == lv)); yield return Real(0.6f);
                if (blue && job != Job.TraceBlue) ZodFlyover.ExpectBlueStart("the blue flyover runner");    // the start lines wait for the flight's hand-back (ZodFlyover.BlueHold.cs); the trace flies none
                m.ProofDeploy();
                Log(lv + ": DEPLOY as the player with the side " + (blue ? "BLUE" : "RED"));
                float t0 = Time.realtimeSinceStartup;
                while ((!ZGame.I || ZGame.I.World == null || SceneManager.GetActiveScene().name != def.Scene) && Time.realtimeSinceStartup - t0 < 120f) yield return null;
                Check(lv + ": the level loaded (" + SceneManager.GetActiveScene().name + ")", ZGame.I && ZGame.I.World != null && SceneManager.GetActiveScene().name == def.Scene);
                if (!ZGame.I) { Finish(); yield break; }
            }
            else if (job == Job.TraceBlue || job == Job.TraceRed) { Check("a trace starts from the main menu (the level's first frames are the measure)", false); Finish(); yield break; }
            game = ZGame.I;
            Begin2();                                                                                  // the sound log's clock, the counters (OzBlueFlyoverProof.Checks2.cs)
            Begin3();                                                                                  // the pose watch, the drive's counters (OzBlueFlyoverProof.Checks3.cs)
            if (job == Job.TraceBlue || job == Job.TraceRed) { yield return Trace(); Finish(); yield break; }
            if (game.level != lv) { Check("the level in Play is " + lv + " (it is " + game.level + ")", false); Finish(); yield break; }
            Check(lv + ": the level runs as BLUE (" + OzPlayBlueLevel.Status() + ")", OzPlayBlue.Active && game.player == Team.Blue);
            if (!OzPlayBlue.Active) { Finish(); yield break; }
            game.enabled = false;                                                                      // the simulation waits, as under the briefing
            rts = FindFirstObjectByType<RtsCamera>(); cam = rts ? rts.GetComponent<Camera>() : Camera.main; if (!cam) cam = Camera.main;
            float tp = Time.realtimeSinceStartup;
            while ((!OzPlayBlueLevel.I || !OzPlayBlueLevel.I.Placed) && Time.realtimeSinceStartup - tp < 3f) yield return null;
            Check(lv + ": the blue level placed its camera over the blue fort before the flight", OzPlayBlueLevel.I && OzPlayBlueLevel.I.Placed);
            hud = OpenZHud.I ? OpenZHud.I : FindFirstObjectByType<OpenZHud>(); input = FindFirstObjectByType<PlayerInput>();
            tp = Time.realtimeSinceStartup;
            while (hud && hud.Canvas == null && Time.realtimeSinceStartup - tp < 3f) yield return null;
            if (hud) { hud.SetVisible(false); hud.enabled = false; }
            if (input) input.enabled = false;
            Cursor.visible = false;
            yield return Real(0.6f);
            yield return LoadClip();
            Check(lv + ": the approved blue briefing take is loaded (" + (clip ? clip.length.ToString("F2") + " s" : "none") + ")", clip);
            if (job == Job.Record) yield return StartRecording();
            yield return Flight();
            if (job == Job.Record) { yield return Tail2(); StopRecording(true); }                          // the take runs on until the start lines have played
            yield return Checks2();                                                                    // the start lines, the framing, the units alive
            yield return Checks3();                                                                    // every robot's pose, the antics on everyone, the leftovers, the cost, the sounds
            Finish();
        }

        // ================================================================================================================ the clip
        IEnumerator LoadClip()
        {
            clip = Resources.Load<AudioClip>("OpenZ/Blue/Voice/scene_briefing");                           // the briefing kit's own copy, when it is in
            if (!clip)
            {
                string st = Staging();
                string path = st.Length > 0 ? st + "/blue_team/voice_final/scene_briefing.wav" : "";
                if (path.Length > 0 && File.Exists(path))
                {
                    using (var req = UnityWebRequestMultimedia.GetAudioClip("file:///" + path, AudioType.WAV))
                    {
                        yield return req.SendWebRequest();
                        if (req.result == UnityWebRequest.Result.Success) { clip = DownloadHandlerAudioClip.GetContent(req); clip.name = "scene_briefing (take)"; }
                        else Log("the take could not be read: " + req.error + " (" + path + ")");
                    }
                }
                else Log("no approved take found under the staging folder (" + (st.Length > 0 ? st : "no staging folder") + ")");
            }
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false; voice.loop = false; voice.spatialBlend = 0f; voice.priority = 8; voice.dopplerLevel = 0f; voice.bypassReverbZones = true;
            voice.clip = clip; voice.volume = 1f;
            AudioDirector.RegisterExternalVoice(voice);
        }

        // ================================================================================================================ the flight
        IEnumerator Flight()
        {
            line = ZodShotList.BlueBriefingLine();
            comms = new ZodComms(transform); comms.SetUiScale(1f);
            comms.Letterbox(true, "MISSION BRIEFING", "OPERATION " + lv + " · " + def.Name);
            fly = new ZodFlyover(rts, game);
            fly.VoiceOn = () => voice && voice.isPlaying;
            bool flying = fly.Begin("overview"); flightBegun = flying;
            Check(lv + ": the blue shot list flies (" + (flying ? (fly.Cine ? "shot list" : "ORBIT FLIGHT") : "no flight") + ")", flying && fly.Cine);
            if (!flying) { comms.Letterbox(false); yield break; }
            preroll = Preroll; float voiceEnd = clip ? Mathf.Min(clip.length, line.duration) : line.duration; end = voiceEnd + Tail;
            ZodShotList.BriefingRoom(lv, line, ref preroll, voiceEnd, ref end);
            fly.PlanCine(preroll, end, line, () => flightDone ? float.NaN : pt + preroll);
            ZodFlyover.SecondsToHandBack = () => fly == null || ZodFlyover.Live != fly ? float.NaN : flightDone ? 0f : end - pt;   // the antics end just before the hand-back (ZodFlyover.Antics.cs); only while this flight is the live one (a run that stopped early never speaks for a later flight); cleared at the hand-back
            Log("plan: " + ZodFlyover.LastPlan);
            Log("room: " + ZodShotList.LastRoom);
            var names = new List<string>(); foreach (var s in fly.CineShots) { names.Add(s.name); if (names.Count == 2) cut2At = s.start; }
            Check(lv + ": four shots in order " + string.Join(" > ", names), string.Join(">", names) == string.Join(">", ZodShotList.BlueShotNames));
            var want = new[] { float.NaN, ZodShotList.BlueCueStarts[1] - ZodFlyover.CutLead, ZodShotList.BlueWordWhere - ZodFlyover.CutLead, ZodShotList.BlueWordNow - ZodFlyover.CutLead };
            var an = ZodFlyover.LastAnchors; bool anchorsOk = an != null && an.Length == 4;
            var at = new StringBuilder();
            for (int i = 1; i < 4 && anchorsOk; i++) { float a = an[i] - preroll; at.Append($" {ZodShotList.BlueShotNames[i]} {a:F2} (want {want[i]:F2})"); if (float.IsNaN(a) || Mathf.Abs(a - want[i]) > 0.03f) anchorsOk = false; }
            Check(lv + ": the cuts sit on the dialog's breaks (s from the voice start):" + at, anchorsOk);
            Check(lv + ": the plan ends at least 2 s after the cut home and after the last word (end " + end.ToString("F2") + " s)", end >= ZodShotList.BlueWordNow - ZodFlyover.CutLead + 2f - 0.01f && end >= voiceEnd + Tail - 0.01f);
            pt = -preroll; voiceOn = false; skipped = false; flightDone = false; fading = false; fade = 1f;
            float flown = 0f;
            while (!flightDone)
            {
                if (holding) { yield return null; continue; }                                          // a GPU heat hold of the take: the flight and the voice wait with it
                float dt = Step(); flown += dt;
                if (!voiceOn) { pt += dt; if (pt >= 0f && !fading) { if (clip) voice.Play(); voiceOn = true; pt = 0f; } }
                else if (voice.isPlaying) pt = voice.timeSamples / (float)Mathf.Max(1, clip.frequency);
                else pt += dt;
                if (fading) { fade = Mathf.MoveTowards(fade, 0f, dt / 0.22f); voice.volume = fade; if (fade <= 0f) { voice.Stop(); flightDone = true; } }
                if (!skipped && flown > 0.35f && (SkipPressed() || (job == Job.SkipTest && pt >= SkipAt)))
                {
                    skipped = true; fading = true; AudioDirector.UI("ui_cancel");
                    Log($"briefing skipped at {pt:F2} s ({(job == Job.SkipTest ? "the skip test" : "a key or click")})");
                }
                if (pt >= end) flightDone = true;
                comms.SetCue(voiceOn && !fading ? CueAt(pt) : null);
                yield return null;
            }
            comms.Letterbox(false);
            fly.HandBack(skipped);
            float handS = 0f; bool dipNow = false;                                                      // on the flight's own clock (capture steps in a take)
            while (fly.HomeError > 0.04f && handS < 1.6f) { if (!dipNow) dipNow = DipShown(); yield return null; if (!holding) handS += Step(); }
            Check(lv + $": the hand-back lands on the RTS camera (error {fly.HomeError:F3} after {handS:F2} s{(skipped ? ", dip + cut" : "")})", fly.HomeError <= 0.04f);
            if (skipped) Check(lv + ": the skip dips to black before the cut home", dipNow || dipSeen);
            var focus = rts ? rts.focus : Vector3.zero; var want2 = OzPlayBlueLevel.I ? OzPlayBlueLevel.I.StartFocus : focus;
            Check(lv + $": the landing is the blue start view (focus {focus.x:0.0} / {focus.z:0.0}, the level placed {want2.x:0.0} / {want2.z:0.0}, height {(rts ? rts.height : 0f):0} m)", Vector3.Distance(focus, want2) < 0.1f);
            fly.End();
            game.enabled = true; Cursor.visible = true;
            HandedBack2();                                                                             // the hand-back's clock: the start lines are judged against it
            HandedBack3();                                                                             // the leftovers are counted a moment later (OzBlueFlyoverProof.Checks3.cs)
            yield return null;
            if (input) input.enabled = true;
            if (hud) { hud.enabled = true; hud.SetVisible(true); }
            yield return Real(0.3f);
            yield return Shot("landed_start_view");
            // the per-shot counts
            if (!skipped)
            {
                Check(lv + $": shot 1 holds the sun's place in the frame ({Pct(sunIn[0], shotFrames[0])} of its {shotFrames[0]} frames) and our blue fort ({Pct(blueIn[0], shotFrames[0])})", shotFrames[0] > 0 && sunIn[0] >= shotFrames[0] * 0.9f && blueIn[0] >= shotFrames[0] * 0.8f);
                Check(lv + $": shot 1: the painted sky with the sun disc is full at {skyFullAt:F2} s of the flight (it loads and fades in with the flight, as on every flyover), before the cut to shot 2 at {cut2At:F2} s; the drawn disc in the frame in {Pct(sunDrawn, shotFrames[0])} of shot 1",
                    skyFullAt >= 0f && cut2At > 0f && skyFullAt < cut2At);
                bool red2 = Shot2RedFort(out string red2Says);                                          // the glide on the map brings the red fort in by its end (OzBlueFlyoverProof.Checks2.cs)
                Check(lv + $": shot 2 shows the map with the red fort in the frame ({Pct(redIn[1], shotFrames[1])} of {shotFrames[1]} frames; {red2Says})", shotFrames[1] > 0 && red2);
                Check(lv + $": shot 3 holds the red fort ({Pct(redIn[2], shotFrames[2])} of {shotFrames[2]} frames)", shotFrames[2] > 0 && redIn[2] >= shotFrames[2] * 0.95f);
                Check(lv + $": shot 4 starts on our blue fort and eases home ({Pct(blueIn[3], shotFrames[3])} of {shotFrames[3]} frames)", shotFrames[3] > 0 && blueIn[3] >= shotFrames[3] * 0.8f);
                Check(lv + ": the painted flyby sky was live under the flight", skyLive);
                if (lv == "L16") Check("L16: the jungle's low look held the plants and its far land under the flight", lowLookSeen);
                Check(lv + ": the dip to black came with the cut into the red fort shot", dipSeen);
            }
            Check(lv + ": the simulation waited (no game time passed under the flight) and runs again", game.enabled && !(input && !input.enabled));
        }

        string CueAt(float t)
        {
            if (line?.cues == null) return null;
            foreach (var c in line.cues) if (c != null && t >= c.start && t < c.end) return c.text;
            return null;
        }

        static bool SkipPressed()
        {
            var kb = Keyboard.current; var m = Mouse.current;
            return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                || (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame));
        }

        static bool DipShown()
        {
            var go = GameObject.Find("ZodBriefingDip"); if (!go) return false;
            var img = go.GetComponentInChildren<UnityEngine.UI.Image>(); return img && img.enabled && img.color.a > 0.5f;
        }

        static string Pct(int a, int n) => n > 0 ? (100f * a / n).ToString("0") + " %" : "-";

        void LateUpdate()
        {
            if (comms != null) comms.Tick();
            if (fly == null || !fly.Active || (flightDone && fly.CineShotIndex < 0)) return;
            if (!AudioListener.pause && !holding) fly.Tick(Step());
            if (!flightDone) Watch();
        }

        /// <summary>Every frame of the flight: the shot on screen, the subjects inside the frame, the sky, the dip, the pictures.</summary>
        void Watch()
        {
            int i = fly.CineShotIndex; if (i < 0 || i >= 8 || !cam) return;
            if (i != lastShot) { lastShot = i; cutSeen[i] = pt; }
            shotFrames[i]++; Watch2(i); Watch3(i);                                                         // the picture's ground rays and the robots' animation, every frame; the pose of every robot in the picture
            var W = game.World; Vector3 blue = Vector3.zero, red = Vector3.zero; int nb = 0, nr = 0;
            foreach (var o in W.Objects)
            {
                if (!o.IsFort) continue;
                var p = ZGame.PxToWorld(o.CenterX, o.CenterY); p.y = game.TerrainY(p.x, p.z) + 3f;
                if (o.Owner == Team.Blue) { blue += p; nb++; } else if (o.Owner == Team.Red) { red += p; nr++; }
            }
            if (nb > 0 && InFrame(blue / nb)) blueIn[i]++;
            if (nr > 0 && InFrame(red / nr)) redIn[i]++;
            var sun = RenderSettings.sun;
            var vs = FlybySky.VisualSun(sun ? -sun.transform.forward : new Vector3(0.94f, 0.16f, -0.3f).normalized);
            bool sunNow = InFrame(cam.transform.position + vs * 1000f); if (sunNow) sunIn[i]++;
            float skyW = FlybySky.I ? FlybySky.I.Weight : 0f;
            if (i == 0 && sunNow && skyW >= 0.5f) sunDrawn++;
            if (skyFullAt < 0f && skyW >= 0.99f) skyFullAt = pt + preroll;
            if (FlybySky.I && ZodFlyover.Live == fly) skyLive = true;
            if (OzL16FlyoverFlora.LowLookOn) lowLookSeen = true;
            if (!dipSeen && i == 2 && DipShown()) dipSeen = true;
            if (!firstPictured && pt + preroll > 0.25f) { firstPictured = true; StartCoroutine(Shot("first_frame")); }
            foreach (var s in fly.CineShots)
            {
                if (s.name != names4(i)) continue;
                if (!shotPictured[i] && pt + preroll >= s.start + 0.5f * s.dur) { shotPictured[i] = true; StartCoroutine(Shot($"shot{i + 1}_{s.name}")); }
            }
        }

        string names4(int i) => i < ZodShotList.BlueShotNames.Length ? ZodShotList.BlueShotNames[i] : "";

        /// <summary>The picture between the briefing letterbox's bars (ZodComms: 112 u at the bottom, 64 u at the top of 1080), a small margin in.</summary>
        public static float BarBottom = 112f / 1080f, BarTop = 64f / 1080f;

        bool InFrame(Vector3 p)
        {
            var v = cam.WorldToViewportPoint(p);
            return v.z > 0f && v.x > 0.03f && v.x < 0.97f && v.y > BarBottom + 0.01f && v.y < 1f - BarTop - 0.01f;
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(dir, name + ".png");
            if (recording)
            {   // the take's own frame: the Game view at the end of this frame
                var tex = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(path, tex.EncodeToPNG()); Destroy(tex);
            }
            else ScreenCapture.CaptureScreenshot(path);
            Log("picture " + path);
        }

        // ================================================================================================================ the recording
        IEnumerator StartRecording()
        {
            OzFinalGateView.SetFixed(1920, 1080, "OpenZ blue flyover 1920x1080", out var info); Log("Game view: " + info);
            float tw = Time.realtimeSinceStartup;
            while ((Screen.width != 1920 || Screen.height != 1080) && Time.realtimeSinceStartup - tw < 6f) yield return null;
            Check(lv + ": the Game view is 1920 x 1080 for the take (it is " + Screen.width + " x " + Screen.height + ")", Screen.width == 1920 && Screen.height == 1080);
            recDir = Path.Combine(dir, "take_frames");
            Directory.CreateDirectory(recDir);
            foreach (var f in Directory.GetFiles(recDir, "*.jpg")) File.Delete(f);                     // the last take's frames of this level (its MP4 stays)
            rate = AudioSettings.outputSampleRate; ch = Channels(AudioSettings.speakerMode); per = rate / Fps;
            samples.Clear(); frameNo = 0; holdFrames = 0; renderFails = 0; peak = 0f;
            Time.captureFramerate = Fps;
            audioStarted = AudioRenderer.Start();
            Check(lv + ": the sound capture started (" + rate + " Hz, " + ch + " channels, " + per + " samples a frame)", audioStarted);
            recording = true;
            StartCoroutine(Grabber());
            yield return null;
        }

        static int Channels(AudioSpeakerMode m)
        {
            switch (m)
            {
                case AudioSpeakerMode.Mono: return 1;
                case AudioSpeakerMode.Quad: return 4;
                case AudioSpeakerMode.Surround: return 5;
                case AudioSpeakerMode.Mode5point1: return 6;
                case AudioSpeakerMode.Mode7point1: return 8;
                default: return 2;
            }
        }

        IEnumerator Grabber()
        {
            while (recording)
            {
                yield return new WaitForEndOfFrame();
                if (!recording) break;
                if (Time.captureFramerate <= 0) { holdFrames++; continue; }
                if (!flightBegun)
                {   // the take starts on the flight's first frame (no frame of the plain start view before it); the sound so far is dropped
                    if (audioStarted)
                    {
                        int pre = AudioRenderer.GetSampleCountForCaptureFrame();
                        using (var nb = new NativeArray<float>(Mathf.Max(0, pre) * ch, Allocator.Temp)) AudioRenderer.Render(nb);
                    }
                    dropped++; continue;
                }
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(recDir, $"f_{frameNo:00000}.jpg"), tex.EncodeToJPG(Jpg)); Destroy(tex);
                if (audioStarted)
                {
                    int avail = AudioRenderer.GetSampleCountForCaptureFrame();
                    using (var nb = new NativeArray<float>(Mathf.Max(0, avail) * ch, Allocator.Temp))
                    {
                        if (!AudioRenderer.Render(nb)) renderFails++;
                        int from = avail > per + per / 2 && frameNo > 0 ? (avail - per) * ch : 0;      // after a heat hold: the frame's own share only
                        for (int k = from; k < nb.Length; k++) { float s = nb[k]; samples.Add(s); if (Mathf.Abs(s) > peak) peak = Mathf.Abs(s); }
                    }
                }
                frameNo++;
                if (frameNo % 30 == 0 && GpuTemp() >= 80)
                {   // the GPU rule: hold the take until it is cool again (no frame, no sound)
                    int t = GpuTemp(); Log($"GPU {t} C: the take holds until it is under 75 C (the flight and the voice wait)");
                    holding = true; bool paused = voice && voice.isPlaying; if (paused) voice.Pause();
                    Time.captureFramerate = 0; float w = 0f;
                    while (GpuTemp() >= 75 && w < 300f) { w += Time.unscaledDeltaTime; yield return null; }
                    Time.captureFramerate = Fps; if (paused && voice) voice.UnPause(); holding = false;
                    Log($"GPU {GpuTemp()} C: the take goes on after {w:0} s");
                }
            }
        }

        static int GpuTemp()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("OpenZ.EditorTools.GpuGuard"); if (t == null) continue;
                var p = t.GetProperty("LastTemp"); return p != null ? (int)p.GetValue(null) : -1;
            }
            return -1;
        }

        void StopRecording(bool write)
        {
            recording = false;
            if (audioStarted) { AudioRenderer.Stop(); audioStarted = false; }
            Time.captureFramerate = 0;
            if (!write) return;
            string wav = Path.Combine(dir, "take.wav");
            int want = frameNo * per * ch;
            while (samples.Count < want) samples.Add(0f);
            if (samples.Count > want) samples.RemoveRange(want, samples.Count - want);
            using (var fs = new FileStream(wav, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                int bytes = samples.Count * 4;
                bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + bytes); bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(Encoding.ASCII.GetBytes("fmt ")); bw.Write(16); bw.Write((short)3); bw.Write((short)ch); bw.Write(rate); bw.Write(rate * ch * 4); bw.Write((short)(ch * 4)); bw.Write((short)32);
                bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write(bytes);
                foreach (var s in samples) bw.Write(s);
            }
            Log($"take: {frameNo} frames at {Fps} a second ({frameNo / (float)Fps:F1} s) in {recDir}, sound {wav} (peak {peak:F3}, render fails {renderFails}, held frames {holdFrames}, frames before the flight left out {dropped})");
            Check(lv + ": the take has frames and sound", frameNo > Fps * 8 && peak > 0.01f && renderFails == 0);
            string st = Staging();
            string outDir = st.Length > 0 ? st + "/blue_team/videos" : dir;
            Directory.CreateDirectory(outDir);
            string mp4 = Path.Combine(outDir, "flyover_" + lv + ".mp4").Replace('\\', '/'), sheet = Path.Combine(outDir, "flyover_" + lv + "_sheet.jpg").Replace('\\', '/');
            string ff = FfmpegExe();
            bool muxed = false;
            if (ff != null)
            {
                muxed = Ffmpeg(ff, $"-y -framerate {Fps} -i \"{recDir.Replace('\\', '/')}/f_%05d.jpg\" -i \"{wav.Replace('\\', '/')}\" -c:v libx264 -preset medium -crf 17 -pix_fmt yuv420p -vf scale=1920:1080:flags=lanczos -c:a aac -b:a 192k -movflags +faststart -shortest \"{mp4}\"", 240000)
                        && File.Exists(mp4) && new FileInfo(mp4).Length > 100000;
                if (muxed) Ffmpeg(ff, $"-y -i \"{mp4}\" -vf \"fps=2,scale=480:-1,tile=4x12\" -frames:v 1 -q:v 3 \"{sheet}\"", 120000);   // bigger tiles: 1920 wide, 2 a second, up to 24 s
            }
            Check(lv + ": the MP4 is written " + mp4 + (File.Exists(sheet) ? " (frame sheet " + sheet + ")" : ""), muxed);
            if (!muxed) Log("join the take by hand: python <kit>/tools/mux_mp4.py \"" + recDir + "\" \"" + wav + "\" \"" + mp4 + "\"");
        }

        static string FfmpegExe()
        {
            var env = Environment.GetEnvironmentVariable("OPENZ_FFMPEG"); if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
            foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            { try { var p = Path.Combine(d.Trim('"'), "ffmpeg.exe"); if (File.Exists(p)) return p; } catch (Exception) { } }
            var la = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var link = Path.Combine(la, "Microsoft", "WinGet", "Links", "ffmpeg.exe"); if (File.Exists(link)) return link;
            var pk = Path.Combine(la, "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(pk))
                foreach (var d in Directory.GetDirectories(pk, "*FFmpeg*"))
                    foreach (var f in Directory.GetFiles(d, "ffmpeg.exe", SearchOption.AllDirectories)) return f;
            return null;
        }

        bool Ffmpeg(string exe, string args, int waitMs)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = false };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    string err = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(waitMs)) { p.Kill(); Log("ffmpeg timed out: " + args); return false; }
                    if (p.ExitCode != 0) { Log("ffmpeg exit " + p.ExitCode + ": " + LastOf(err)); return false; }
                    return true;
                }
            }
            catch (Exception e) { Log("ffmpeg failed: " + e.Message); return false; }
        }

        static string LastOf(string s) => string.IsNullOrEmpty(s) ? "" : s.Length > 600 ? s.Substring(s.Length - 600) : s;

        // ================================================================================================================ the frame trace
        IEnumerator Trace()
        {
            string before = OzStartFrameTrace.LastFile;
            float t0 = Time.realtimeSinceStartup;
            while (OzStartFrameTrace.LastFile == before && Time.realtimeSinceStartup - t0 < 40f) yield return null;
            Check(lv + " " + (job == Job.TraceBlue ? "BLUE" : "RED") + " start: the frame trace of the first 10 s is written (" + OzStartFrameTrace.LastFile + ")", OzStartFrameTrace.LastFile != before);
            Log(OzStartFrameTrace.LastSummary);
            TraceLines2();                                                                             // the start lines of this start (red: held by the briefing)
            TraceLines3();                                                                             // a red start's robots play the flyover antics too
        }

        // ================================================================================================================ the end
        void Finish()
        {
            string head = $"{(fails == 0 ? "PASS" : "FAIL")} {passes}/{passes + fails} checks ({lv} {job}, {Part2}, {Part3}); pictures and reports in {dir}";
            Report = head + "\n" + sb;
            try { File.WriteAllText(Path.Combine(dir, "report_" + job + ".txt"), Report); } catch (Exception) { }
            Debug.Log("[OzBlueFlyoverProof] " + head);
            Destroy(gameObject);
        }
    }
}
#endif
