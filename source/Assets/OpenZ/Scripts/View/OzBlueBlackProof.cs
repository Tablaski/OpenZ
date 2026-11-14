#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// Version 1.2 Play proof of owner items 49 (the black world of PLAY AS BLUE), 48 (no Commander Zod on the battlefield screen while
    /// the box is ticked) and 56 (no match-state lines in the Unit editor); menu OpenZ/V1.2/Play As Blue: 11b Black World, Comms And Lines
    /// Proof (Play), Editor/OzBlueBlackMenu.cs. Started on the MainMenu scene in Play it walks by itself (about five minutes):
    ///  0  right after boot: FlybySky wrote the glow shape (BootShape.x above 0) and the live global is above 0.
    ///  B  the battlefield screen: the box OFF, a level card clicked: the commander's level line plays in his window (his feed, CMDR ZOD);
    ///     the box ticked while he talks: his line fades out and his window folds, then no comms window at all (1.2 item 65: no blue
    ///     channel card, no static of it); another card clicked with the box on: no line of his and no window for 3 s; the box
    ///     unticked: his line for that card comes. Pictures.
    ///  A  the flyby globals zeroed as a new process has them, then PLAY AS BLUE deployed from the main menu on L02, L16 and L17: the
    ///     world drawn (Camera.main into a picture: mean light over 30 of 255, 0 NaN pixels) at 1 s and at 6 s after the level loaded,
    ///     the globals still 0 (the shader guards draw it); then the boot globals written as FlybySky's boot does and blue L16 again
    ///     (the state of a new game: the same); then a red L02 start with the globals zeroed and the briefing skipped at once (the
    ///     earliest the SPACE key allows): drawn at 1 s and 6 s. Pictures at 6 s.
    ///  D  in that red level: every match-state line passes the playground rule; "They're gonna win!" (ann_youre_losing) plays when
    ///     asked; the push watcher itself ("Let's take the fort!" ..., vo_push) speaks with the player's units next to the enemy fort;
    ///     the help cries' losing takes are asked as they are (not swapped).
    ///  F  the frame rate bench (a short smoke run) on that level: with the world made black on purpose (one value of the atmosphere
    ///     pass not a number, as pow(0, 0) was) the result is NOT VALID with "the world drew black"; with the world drawn, not for that reason.
    ///  U  the Unit editor (through the menu's own UNIT EDITOR action): a sample fight, then the other side than the player's wiped out, the status
    ///     watchers made eligible: 0 match-state lines played (the sound log), the watcher's push line held, every match-state line asked
    ///     directly held, the help cries' losing takes swapped and none of them played, a unit voice line still plays. Back to the main menu.
    /// The player's settings are put back at the end (the box, the announcer and robot chatter levels, the flyby globals as found, the
    /// status line's earliest time, the bench's waits), also when Play stops first. Counted PASS / FAIL lines and the pictures in
    /// <see cref="Folder"/>. Test only (editor).
    /// </summary>
    public sealed class OzBlueBlackProof : MonoBehaviour
    {
        public static string Report = "idle";
        public static OzBlueBlackProof I { get; private set; }
        const string T = "[OzBlueBlackProof] ";

        /// <summary></summary>
        public static string Folder()
        {
            var d = new System.IO.DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var staging = System.IO.Path.Combine(d.FullName, "work", "staging");
                if (!System.IO.Directory.Exists(staging)) continue;
                foreach (var stage in System.IO.Directory.GetDirectories(staging))
                {
                    var kit = System.IO.Path.Combine(stage, "k11b_blue_black_and_small_fixes");
                    if (System.IO.Directory.Exists(kit)) return System.IO.Path.Combine(kit, "proof");
                }
            }
            return System.IO.Path.Combine(Application.persistentDataPath, "k11b_blue_black_and_small_fixes");
        }

        public static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning(T + "enter Play on the MainMenu scene first"); return; }
            if (I) { Debug.LogWarning(T + "already running"); return; }
            if (!FindFirstObjectByType<OpenZMainMenu>()) { Debug.LogWarning(T + "start it on the main menu (the MainMenu scene in Play)"); return; }
            Report = "running";
            var go = new GameObject("OzBlueBlackProof"); DontDestroyOnLoad(go); go.AddComponent<OzBlueBlackProof>();
        }

        // ------------------------------------------------------------------------------------------------ state, put back at the end
        static readonly string[] FlybyVec = { "_FlybySunDir", "_FlybySunGlow", "_FlybySunShape", "_FlybySunDisc" };
        static readonly string[] FlybyFloat = { "_FlybySkyW", "_FlybySeaY", "_FlybyHorizonBlend", "_FlybySkyClear", "_FlybyHazeBandK", "_FlybySunThrough" };
        readonly Dictionary<string, Vector4> vecFound = new Dictionary<string, Vector4>();
        readonly Dictionary<string, float> floatFound = new Dictionary<string, float>();
        bool boxWas, started, putBack; int annWas = -1, robotWas = -1; float earliestWas, settleWas, warmWas, settleViewWas;
        readonly StringBuilder sb = new StringBuilder(); int fails, passes; string dir;
        float levelLoadedAt = -1f, levelLoadedLog = -1f; string levelLoaded = "";
        /// <summary>The sound log's clock (OzSoundLog stamps its entries with the unscaled time; the waits here run on the real time,
        /// which in the editor counts from the editor's start, not from Play).</summary>
        static float LogNow => Time.unscaledTime;

        void Awake() { I = this; }
        void Start()
        {
            dir = Folder(); System.IO.Directory.CreateDirectory(dir);
            boxWas = OzPlayBlue.Box;
            foreach (var n in FlybyVec) vecFound[n] = Shader.GetGlobalVector(n);
            foreach (var n in FlybyFloat) floatFound[n] = Shader.GetGlobalFloat(n);
            earliestWas = AudioDirector.StatusLineEarliest;
            settleWas = OzFpsBench.LevelSettleS; warmWas = OzFpsBench.WarmS; settleViewWas = OzFpsBench.SettleS;
            SceneManager.sceneLoaded += OnLoaded;
            started = true;
            StartCoroutine(All());
        }
        void OnLoaded(Scene s, LoadSceneMode m) { if (m == LoadSceneMode.Single) { levelLoadedAt = Time.realtimeSinceStartup; levelLoadedLog = LogNow; levelLoaded = s.name; } }
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            if (I == this) I = null;
            if (started && !putBack) { PutBack(); Debug.Log(T + "stopped before its end: the player's settings put back"); }
        }

        void PutBack()
        {
            putBack = true;
            OzPlayBlue.Box = boxWas;
            foreach (var kv in vecFound) Shader.SetGlobalVector(kv.Key, kv.Value);
            foreach (var kv in floatFound) Shader.SetGlobalFloat(kv.Key, kv.Value);
            AudioDirector.StatusLineEarliest = earliestWas;
            if (annWas >= 0) AudioDirector.Chatter.Level = annWas;
            if (robotWas >= 0) AudioDirector.RobotChatter.Level = robotWas;
            OzFpsBench.LevelSettleS = settleWas; OzFpsBench.WarmS = warmWas; OzFpsBench.SettleS = settleViewWas;
        }

        void Log(string t) { sb.AppendLine(t); Debug.Log(T + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }
        static IEnumerator Until(System.Func<bool> c, float max) { float t = Time.realtimeSinceStartup + max; while (Time.realtimeSinceStartup < t && !c()) yield return null; }
        IEnumerator Shot(string name)
        {
            string p = System.IO.Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(p); Log("   picture " + name + ".png");
            yield return OzProofPictures.AfterGrab(); yield return OzProofPictures.Written(p);
        }
        static OpenZMainMenu Menu() => FindFirstObjectByType<OpenZMainMenu>();
        static bool InMenu => Menu() && SceneManager.GetActiveScene().name == OzPlayBlue.MenuScene;
        static int Card(string lv) => System.Array.FindIndex(OzLevelDef.All, d => d.Id == lv);
        /// <summary>Sound log entries of key whose outcome starts so, since the log time l0 (<see cref="LogNow"/>).</summary>
        static int SoundCount(float l0, string key, string outcomeStart) => OzSoundLog.Since(l0).Count(e => e.key == key && e.outcome != null && e.outcome.StartsWith(outcomeStart));

        /// <summary>A click on a level card as the commander's line rule sees it (the menu's own last card click and its time).</summary>
        static void CardClicked(OpenZMainMenu m, int card)
        {
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(OpenZMainMenu).GetField("lastCardClick", F)?.SetValue(m, card);
            typeof(OpenZMainMenu).GetField("lastCardClickAt", F)?.SetValue(m, OzMotion.Now);
        }

        void ZeroFlyby()
        {
            foreach (var n in FlybyVec) Shader.SetGlobalVector(n, Vector4.zero);
            foreach (var n in FlybyFloat) Shader.SetGlobalFloat(n, 0f);
        }

        IEnumerator All()
        {
            // ---------------------------------------------------------------- 0: right after boot
            var live = Shader.GetGlobalVector("_FlybySunShape");
            Check($"0 right after boot FlybySky wrote the glow shape: BootShape {FlybySky.BootShape} ({FlybySky.RestWrites} write(s)), the live _FlybySunShape.x {live.x:0.###} above 0",
                  FlybySky.BootShape.x > 0f && FlybySky.RestWrites >= 1 && live.x > 0f);
            yield return Real(1f);
            yield return Comms();
            foreach (var lv in new[] { "L02", "L16", "L17" }) yield return BlueStart(lv, true);
            FlybySky.WriteRestGlobals();
            var sh = Shader.GetGlobalVector("_FlybySunShape");
            Check($"A the boot's globals written again (as a new game has them): _FlybySunShape ({sh.x:0}, {sh.y:0}, {sh.z:0.000000}, {sh.w:0.000000}), _FlybySunDir.w {Shader.GetGlobalVector("_FlybySunDir").w:0}",
                  Mathf.Approximately(sh.x, FlybySky.GlowTightExp) && Mathf.Approximately(sh.y, FlybySky.GlowWideExp) && Shader.GetGlobalVector("_FlybySunDir").w == 0f);
            yield return BlueStart("L16", false);
            yield return RedStart();
            if (ZGame.I && ZGame.I.World != null && !OzPlayBlue.Active)
            {
                yield return LevelLines();
                yield return Bench();
            }
            yield return ToMenu();
            yield return UnitEditor();
            yield return ToMenu();
            PutBack();
            Log("the player's settings put back: the box " + (boxWas ? "ON" : "OFF") + ", the flyby globals as found, the status line's earliest time " + earliestWas.ToString("0") + " s");
            Report = $"{T}{(fails == 0 ? "PASS" : "FAIL")} {passes}/{passes + fails} checks; pictures in {dir}";
            Log(Report);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "checks.txt"), sb.ToString());
            Destroy(gameObject);
        }

        IEnumerator ToMenu()
        {
            if (!InMenu) { OzScenes.MainMenu(); yield return Until(() => InMenu, 60f); }
            yield return Real(1.5f);
            if (!InMenu) Check("back on the main menu", false);
        }

        // ---------------------------------------------------------------- B: the comms window on the battlefield screen
        IEnumerator Comms()
        {
            var m = Menu();
            m.ProofSelect(); yield return Real(0.8f);
            if (OzPlayBlue.Box) { m.ProofTogglePlayBlue(false); yield return Real(0.4f); }
            yield return Until(() => ZodBriefing.DebugLineId == null && !ZodBriefing.CommsOpen, 12f);
            int c02 = Card("L02"), c17 = Card("L17");
            m.ProofFocus(c02); yield return Real(0.3f);
            ZodBriefing.ProofForgetQuips();
            CardClicked(m, c02);
            yield return Until(() => ZodBriefing.DebugLineId != null, 5f);
            string zl = ZodBriefing.DebugLineId;
            yield return Real(0.8f);                                                                       // inside his line (preroll 0.55 s + about 2.5 s of words): the box is ticked mid-line below
            string spk = ZodBriefing.CommsSpeaker;
            Check($"B box OFF, the L02 card clicked: the commander's line {zl ?? "(none)"} in his window, speaker {spk}, his feed {(ZodBriefing.CommsFeedShown ? "shown" : "MISSING")}",
                  zl == "zod_select_L02" && spk.Contains("ZOD") && ZodBriefing.CommsFeedShown && !ZodBriefing.BlueCardShown);
            yield return Shot("B1_menu_box_off_zod_line");

            float t0 = Time.realtimeSinceStartup, l0 = LogNow; int faded = ZodBriefing.BlueQuipsFaded, opens = ZodBriefing.BlueCardOpens;
            m.ProofTogglePlayBlue(false);
            yield return Until(() => ZodBriefing.DebugLineId == null, 1.5f);
            float off = Time.realtimeSinceStartup - t0;
            Check($"B the box ticked while he talks: his line ends in {off:0.00} s (faded {ZodBriefing.BlueQuipsFaded - faded}), not speaking {!ZodBriefing.Speaking}",
                  OzPlayBlue.Box && ZodBriefing.DebugLineId == null && off <= 1.5f && ZodBriefing.BlueQuipsFaded == faded + 1 && !ZodBriefing.Speaking);
            yield return Until(() => !ZodBriefing.CommsVisible, 2.5f);                                     // his faded line's window folds by itself (1.2 item 65)
            float gone = Time.realtimeSinceStartup - t0;
            int winFrames0 = 0, frames0 = 0; float tq = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tq < 1.5f) { frames0++; if (ZodBriefing.CommsVisible || ZodBriefing.BlueCardShown) winFrames0++; yield return null; }
            int hides = ZodBriefing.BlueHides;                                                              // the rule hid the window (counted since the game started)
            Check($"B no comms window while the box is ticked: his window gone {gone:0.00} s after the tick, then a window in {winFrames0} of {frames0} frames, the blue card opened {ZodBriefing.BlueCardOpens - opens} x, its static {SoundCount(l0, "comms_blue_open", "PLAY")} x, hidden {hides} x",
                  !ZodBriefing.CommsVisible && gone <= 2.5f && winFrames0 == 0 && frames0 > 0 && ZodBriefing.BlueCardOpens == opens && SoundCount(l0, "comms_blue_open", "PLAY") == 0 && hides > 0);
            yield return Shot("B2_menu_box_on_no_comms");

            m.ProofFocus(c17); yield return Real(0.2f); CardClicked(m, c17);
            int lineFrames = 0, winFrames = 0, frames = 0; float t1 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t1 < 3f) { frames++; if (ZodBriefing.DebugLineId != null || ZodBriefing.Speaking) lineFrames++; if (ZodBriefing.CommsVisible) winFrames++; yield return null; }
            Check($"B the box on, the L17 card clicked: for 3 s no line of the commander ({lineFrames} of {frames} frames) and no comms window ({winFrames} of {frames})",
                  lineFrames == 0 && winFrames == 0 && frames > 0);
            yield return Shot("B3_menu_box_on_card_clicked");

            float t2 = Time.realtimeSinceStartup, l2 = LogNow;
            m.ProofTogglePlayBlue(false);
            yield return Real(0.3f);
            Check($"B the box unticked: no blue card to fold ({!ZodBriefing.BlueCardShown}), no closing static of it ({SoundCount(l2, "comms_blue_close", "PLAY")} x)",
                  !OzPlayBlue.Box && !ZodBriefing.BlueCardShown && SoundCount(l2, "comms_blue_close", "PLAY") == 0);
            yield return Until(() => ZodBriefing.DebugLineId != null, 4f);
            zl = ZodBriefing.DebugLineId; float back = Time.realtimeSinceStartup - t2;
            yield return Real(1.2f);
            Check($"B the commander is back at once: {zl ?? "(none)"} {back:0.0} s after the untick, speaker {ZodBriefing.CommsSpeaker}, his feed {ZodBriefing.CommsFeedShown}",
                  zl == "zod_select_L17" && back <= 4f && ZodBriefing.CommsSpeaker.Contains("ZOD") && ZodBriefing.CommsFeedShown);
            yield return Shot("B4_menu_box_off_zod_back");
            yield return Until(() => ZodBriefing.DebugLineId == null && !ZodBriefing.CommsOpen, 12f);
        }

        // ---------------------------------------------------------------- A: the blue starts and the red start
        IEnumerator Deploy(string lv, bool blue, bool zero)
        {
            yield return ToMenu();
            var m = Menu(); if (!m) { Check(lv + ": the main menu", false); yield break; }
            m.ProofSelect(); yield return Real(0.8f);
            m.ProofFocus(Card(lv)); yield return Real(0.6f);
            if (OzPlayBlue.Box != blue) { m.ProofTogglePlayBlue(false); yield return Real(0.5f); }
            if (zero) ZeroFlyby();
            levelLoadedAt = -1f;
            m.ProofDeploy();
            Log($"{lv}: DEPLOY {(blue ? "with PLAY AS BLUE" : "red (the box off)")}{(zero ? ", every flyby global 0 (a new process)" : "")}");
            yield return Until(() => levelLoadedAt > 0f && ZGame.I && ZGame.I.World != null && SceneManager.GetActiveScene().name != OzPlayBlue.MenuScene, 120f);
        }

        IEnumerator Light(string what, float at)
        {
            while (levelLoadedAt > 0f && Time.realtimeSinceStartup - levelLoadedAt < at) yield return null;
            var r = OzWorldLight.Now(Camera.main);
            float t = levelLoadedAt > 0f ? Time.realtimeSinceStartup - levelLoadedAt : -1f;
            Check($"{what} at {t:0.00} s after {levelLoaded} loaded: Camera.main drawn ({r}), mean over {OzWorldLight.BlackBelow:0} of 255 and no NaN pixel", r.ok && r.mean > OzWorldLight.BlackBelow && r.nan == 0);
        }

        IEnumerator BlueStart(string lv, bool zero)
        {
            yield return Deploy(lv, true, zero);
            if (!ZGame.I || ZGame.I.World == null) { Check(lv + " blue: the level loaded", false); yield break; }
            string tag = lv + " blue" + (zero ? " (flyby globals 0)" : " (the boot's globals)");
            Check($"{tag}: played as blue (human {ZGame.I.player}, PLAY AS BLUE {OzPlayBlue.Active}), no briefing {!ZodBriefing.BriefingActive}",
                  OzPlayBlue.Active && ZGame.I.player == Team.Blue && !ZodBriefing.BriefingActive);
            yield return Light(tag, 1f);
            var sh = Shader.GetGlobalVector("_FlybySunShape");
            if (zero) Check($"{tag}: the glow shape is still 0 ({sh.x:0}) at 1 s: the shader's own guards draw this world", sh.x == 0f);
            else Check($"{tag}: the glow shape as the boot wrote it ({sh.x:0})", Mathf.Approximately(sh.x, FlybySky.GlowTightExp));
            yield return Light(tag, 6f);
            yield return Shot($"A_{lv}_blue_{(zero ? "globals0" : "boot")}_6s");
        }

        IEnumerator RedStart()
        {
            yield return Deploy("L02", false, true);
            if (!ZGame.I || ZGame.I.World == null) { Check("L02 red: the level loaded", false); yield break; }
            Check($"L02 red: played as red (human {ZGame.I.player}, PLAY AS BLUE {OzPlayBlue.Active})", !OzPlayBlue.Active && ZGame.I.player == Team.Red);
            float early = -1f; OzWorldLight.Reading first = default;
            while (Time.realtimeSinceStartup - levelLoadedAt < 0.15f) yield return null;
            first = OzWorldLight.Now(Camera.main); early = Time.realtimeSinceStartup - levelLoadedAt;
            Log($"   L02 red, the first moment ({early:0.00} s, under the briefing window): Camera.main {first} (seen only)");
            yield return Until(() => ZodBriefing.BriefingActive, 10f);
            float bAt = Time.realtimeSinceStartup;
            bool skipped = false;
            while (!skipped && Time.realtimeSinceStartup - bAt < 4f)
            {
                if (Time.realtimeSinceStartup - bAt >= 0.36f) skipped = ZodBriefing.ProofSkipBriefing();
                yield return null;
            }
            Check($"L02 red: the briefing skipped at {Time.realtimeSinceStartup - levelLoadedAt:0.00} s after the load (as the SPACE key at its earliest)", skipped);
            yield return Light("L02 red, briefing skipped", 1f);
            yield return Light("L02 red, briefing skipped", 6f);
            yield return Shot("A_L02_red_skip_globals0_6s");
            yield return Until(() => !ZodBriefing.BriefingActive, 10f);
        }

        // ---------------------------------------------------------------- D: the lines in a normal level
        static readonly BindingFlags IF = BindingFlags.NonPublic | BindingFlags.Instance;
        static void ReadyStatusLines()
        {
            var ad = AudioDirector.I; if (!ad) return;
            foreach (var f in new[] { "lastPush", "lastLosing", "lastStatusLine" }) typeof(AudioDirector).GetField(f, IF)?.SetValue(ad, -1000f);
            foreach (var f in new[] { "nextPushCheck", "nextLosingCheck" }) typeof(AudioDirector).GetField(f, IF)?.SetValue(ad, 0f);
        }

        void SettleChatter()
        {
            if (annWas < 0) annWas = AudioDirector.Chatter.Level;
            if (robotWas < 0) robotWas = AudioDirector.RobotChatter.Level;
            if (AudioDirector.Chatter.Level != 0) AudioDirector.Chatter.Level = 0;                        // FULL for the proof (put back at the end)
            if (AudioDirector.RobotChatter.Level != 0) AudioDirector.RobotChatter.Level = 0;
        }

        IEnumerator AskUntilPlayed(string id, bool announce, float l0, float each, int tries)
        {
            for (int k = 0; k < tries && SoundCount(l0, id, "PLAY") == 0; k++)
            {
                if (announce) AudioDirector.Announce(id); else AudioDirector.Say(id);
                float t = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t < each && SoundCount(l0, id, "PLAY") == 0) yield return null;
            }
        }

        IEnumerator LevelLines()
        {
            yield return Real(1.5f);
            SettleChatter();
            AudioDirector.StatusLineEarliest = 0f;
            int held0 = AudioDirector.MatchLinesHeld; float l0 = LogNow;
            int passed = AudioDirector.MatchStateLines.Count(id => !AudioDirector.HoldMatchLine(id));
            Check($"D a level ({ZGame.I.level}): the playground rule lets every match-state line through ({passed} of {AudioDirector.MatchStateLines.Count}; in the playground: {AudioDirector.NoMatchHere})",
                  passed == AudioDirector.MatchStateLines.Count && !AudioDirector.NoMatchHere && AudioDirector.MatchLinesHeld == held0);
            yield return AskUntilPlayed("ann_youre_losing", true, l0, 4f, 3);                             // before any fight: no alert line keeps its gap shut
            Check($"D a level: the losing line asked (\"They're gonna win!\"): played {SoundCount(l0, "ann_youre_losing", "PLAY")} x", SoundCount(l0, "ann_youre_losing", "PLAY") >= 1);
            // the push watcher itself (vo_push, "Let's take the fort!" ...): red units next to the blue fort (its 'close' rule), its gaps opened
            var g = ZGame.I; var W = g.World;
            var fort = W.Objects.FirstOrDefault(o => o.IsFort && o.Owner != g.player && o.Owner != Team.Neutral && !o.Destroyed);
            if (fort == null) { Check("D a level: an enemy fort for the push watcher", false); yield break; }
            var spots = FreeTilesNear(W, fort, 3);
            foreach (var s in spots)
            {
                var o = W.CreateRobotGroup("grunt", s.x * 16, s.y * 16, g.player);
                if (o != null) { g.SpawnUnitView(o); foreach (var mm in o.Minions) g.SpawnUnitView(mm); }
            }
            Log($"   {spots.Count} grunt squad(s) of the player's dropped on free ground 7-12 tiles from the enemy fort #{fort.Id} ({string.Join(" ", spots.Select(s => s.x + "," + s.y))})");
            if (spots.Count == 0) { Check("D a level: free ground next to the enemy fort for the push watcher", false); yield break; }
            float t1 = Time.realtimeSinceStartup, l1 = LogNow;
            while (Time.realtimeSinceStartup - t1 < 20f && SoundCount(l1, "vo_push", "PLAY") == 0)
            {
                ReadyStatusLines();
                float w = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - w < 2f && SoundCount(l1, "vo_push", "PLAY") == 0) yield return null;
            }
            Check($"D a level: the push watcher speaks with the player's units next to the enemy fort: vo_push played {SoundCount(l1, "vo_push", "PLAY")} x; held by the playground rule {AudioDirector.MatchLinesHeld - held0}",
                  SoundCount(l1, "vo_push", "PLAY") >= 1 && AudioDirector.MatchLinesHeld == held0);
            int sw0 = AudioDirector.LosingTakesSwapped;
            foreach (var take in AudioDirector.LosingTakes) AudioDirector.Say("vo_under_attack_repeat", "=" + take);
            Check($"D a level: the help cries' losing takes are asked as they are (swapped {AudioDirector.LosingTakesSwapped - sw0}, want 0)", AudioDirector.LosingTakesSwapped == sw0);
            AudioDirector.StatusLineEarliest = earliestWas;
        }

        /// <summary>The clip names of the help cries' takes that speak of losing (AudioDirector.LosingTakes).</summary>
        static HashSet<string> LosingClips()
        {
            var s = new HashSet<string>(); var d = AudioDirector.I ? AudioDirector.I.Def("vo_under_attack_repeat") : null;
            if (d != null && d.loaded != null && d.lines != null)
                for (int i = 0; i < d.loaded.Count && i < d.lines.Count; i++) if (d.loaded[i] && AudioDirector.LosingTakes.Contains(d.lines[i])) s.Add(d.loaded[i].name);
            return s;
        }

        /// <summary>Up to n free robot tiles (3 x 3 passable, no object centre within 2 tiles) 7 to 12 tiles from the object's centre:
        /// inside the push watcher's 250 px 'close' rule, outside a fort's footprint.</summary>
        static List<Vector2Int> FreeTilesNear(ZWorld W, ZObject at, int n)
        {
            var got = new List<Vector2Int>(); int cx = at.CenterX >> 4, cy = at.CenterY >> 4;
            for (int r = 7; r <= 12 && got.Count < n; r++)
                for (int k = 0; k < 8 * r && got.Count < n; k++)
                {
                    float a = k * Mathf.PI * 2f / (8 * r);
                    int tx = cx + Mathf.RoundToInt(Mathf.Cos(a) * r), ty = cy + Mathf.RoundToInt(Mathf.Sin(a) * r);
                    bool ok = true;
                    for (int j = -1; j <= 1 && ok; j++) for (int i = -1; i <= 1 && ok; i++) ok = W.Path.TilePassable(tx + i, ty + j, true);
                    if (!ok) continue;
                    foreach (var o in W.Objects) if (!o.Destroyed && Mathf.Abs((o.CenterX >> 4) - tx) < 3 && Mathf.Abs((o.CenterY >> 4) - ty) < 3) { ok = false; break; }
                    foreach (var q in got) if (Mathf.Abs(q.x - tx) < 3 && Mathf.Abs(q.y - ty) < 3) ok = false;
                    if (ok) got.Add(new Vector2Int(tx, ty));
                }
            return got;
        }

        // ---------------------------------------------------------------- F: the bench's black-world rule
        IEnumerator Bench()
        {
            OzFpsBench.LevelSettleS = 1f; OzFpsBench.WarmS = 0.3f; OzFpsBench.SettleS = 0.6f;
            // the black world of item 49 made on purpose, through the same pass: one value of the atmosphere pass that is not a number
            // (as pow(0, 0) was) and NaN x _FlybySkyW (0) is still NaN: every pixel of the world black, the HUD drawn
            var glowWas = Shader.GetGlobalVector("_FlybySunGlow");
            Shader.SetGlobalVector("_FlybySunGlow", new Vector4(float.NaN, float.NaN, float.NaN, float.NaN));
            yield return Real(0.3f);
            var dark = OzWorldLight.Now(Camera.main);
            Log($"   _FlybySunGlow set to NaN (a black world on purpose): Camera.main {dark}");
            string a = OzFpsBench.Smoke(5f); Log("   bench smoke with the black world: " + a);
            yield return Real(1f);
            yield return Until(() => !OzFpsBench.I, 90f);
            string r1 = OzFpsBench.LastResult;
            Shader.SetGlobalVector("_FlybySunGlow", glowWas);
            Check($"F the bench on a black world is NOT VALID because the world drew black (the world before the run: {dark}): {Short(r1)}", r1.Contains("NOT VALID") && r1.Contains("the world drew black") && dark.Black);
            yield return Real(1f);
            string b = OzFpsBench.Smoke(5f); Log("   bench smoke with the world drawn: " + b);
            yield return Real(1f);
            yield return Until(() => !OzFpsBench.I, 90f);
            string r2 = OzFpsBench.LastResult;
            Check($"F the bench with the world drawn: not for that reason (an editor smoke run is never VALID): {Short(r2)}",
                  r2.Length > 0 && r2 != r1 && !r2.Contains("the world drew black") && !r2.Contains("could not be read") && !r2.Contains("FAILED"));
            OzFpsBench.LevelSettleS = settleWas; OzFpsBench.WarmS = warmWas; OzFpsBench.SettleS = settleViewWas;
        }
        static string Short(string s) { int i = s.IndexOf("NOT VALID", System.StringComparison.Ordinal); return i >= 0 ? s.Substring(i) : s.Length > 200 ? s.Substring(s.Length - 200) : s; }

        // ---------------------------------------------------------------- U: the Unit editor's playground
        IEnumerator UnitEditor()
        {
            var m = Menu(); if (!m) { Check("U the main menu", false); yield break; }
            var go = typeof(OpenZMainMenu).GetMethod("GoUnitEditor", IF);
            if (go == null) { Check("U the menu's UNIT EDITOR action", false); yield break; }
            go.Invoke(m, null);
            yield return Until(() => OzUnitEditor.UiBuilt && SceneManager.GetActiveScene().name == OzUnitEditor.SceneName, 90f);
            yield return Real(2f);
            var ed = OzUnitEditor.I; var W = ed ? ed.World : null;
            if (!ed || W == null) { Check("U the Unit editor's playground open", false); yield break; }
            SettleChatter();
            AudioDirector.StatusLineEarliest = 0f;
            float l0 = levelLoadedLog > 0f ? levelLoadedLog : LogNow; int held0 = AudioDirector.MatchLinesHeld;   // from the playground's load (its start lines too)
            Team me = ZGame.I ? ZGame.I.player : Team.Red, other = me == Team.Red ? Team.Blue : Team.Red;
            OzUePlayground.ClearField(); yield return Real(0.3f);
            int groups = OzUePlayground.SampleFight();
            OzUeControls.SetSpeed(2f); OzUeControls.SetPlaying(true);
            yield return Real(4f);                                                                         // the fight under way, both sides still standing
            yield return Shot("U1_playground_sample_fight");
            int killed = 0;
            foreach (var o in W.Objects.Where(x => x.IsUnit && x.Owner == other && !x.Destroyed).ToList())
            {
                foreach (var mm in new List<ZObject>(o.Minions)) if (!mm.Destroyed) { mm.DamageHealth(mm.Health + 1); W.UpdateObjectHealth(mm, -1); }
                if (!o.Destroyed) { o.DamageHealth(o.Health + 1); W.UpdateObjectHealth(o, -1); killed++; }
            }
            yield return Real(2f);
            int left = W.Objects.Count(x => x.IsUnit && x.Owner == other && !x.Destroyed), mine = W.Objects.Count(x => x.IsUnit && x.Owner == me && !x.Destroyed);
            Check($"U a sample fight ({groups} groups) at x2, then the {other} side wiped out ({killed} units destroyed): {other} {left}, {me} {mine} left, no game end {!W.GameOver}",
                  groups > 0 && left == 0 && mine > 0 && !W.GameOver);
            float t1 = Time.realtimeSinceStartup, l1 = LogNow;
            while (Time.realtimeSinceStartup - t1 < 12f) { ReadyStatusLines(); yield return Real(2f); }
            int heldWatch = SoundCount(l1, "vo_push", "HELD");
            yield return Shot("U2_playground_one_side_wiped");
            int asked0 = AudioDirector.MatchLinesHeld;
            foreach (var id in AudioDirector.MatchStateLines) { if (id.StartsWith("ann_")) AudioDirector.Announce(id); else AudioDirector.Say(id); }
            int heldAsked = AudioDirector.MatchLinesHeld - asked0;
            int sw1 = AudioDirector.LosingTakesSwapped;
            foreach (var take in AudioDirector.LosingTakes) AudioDirector.Say("vo_under_attack_repeat", "=" + take);
            int swapped = AudioDirector.LosingTakesSwapped - sw1;
            yield return Real(3f);
            var losing = LosingClips();
            int losingPlays = OzSoundLog.Since(l0).Count(e => e.key == "vo_under_attack_repeat" && e.outcome != null && e.outcome.StartsWith("PLAY") && e.clip != null && losing.Contains(e.clip));
            Check($"U the help cries' losing takes ({string.Join(" / ", AudioDirector.LosingTakes)}, {losing.Count} clip(s)) asked directly: swapped {swapped} of {AudioDirector.LosingTakes.Count}; played since the playground opened: {losingPlays}",
                  swapped == AudioDirector.LosingTakes.Count && losing.Count >= 1 && losingPlays == 0);
            var plays = OzSoundLog.Since(l0).Where(e => e.outcome != null && e.outcome.StartsWith("PLAY") && AudioDirector.MatchStateLines.Contains(e.key)).Select(e => e.key).ToList();
            Check($"U the push watcher with one side wiped out: its line held {heldWatch} x (the watcher ran, nothing spoke)", heldWatch >= 1);
            Check($"U every match-state line asked directly: held {heldAsked} of {AudioDirector.MatchStateLines.Count}", heldAsked == AudioDirector.MatchStateLines.Count);
            Check($"U the sound log since the playground opened (its start lines, the fight, the watchers, the asked lines): {plays.Count} match-state line(s) played{(plays.Count > 0 ? " (" + string.Join(", ", plays) + ")" : "")}, {AudioDirector.MatchLinesHeld - held0} held",
                  plays.Count == 0 && AudioDirector.MatchLinesHeld - held0 >= AudioDirector.MatchStateLines.Count + 1);
            float l2 = LogNow;
            yield return AskUntilPlayed("vo_select_generic", false, l2, 2f, 4);
            Check($"U a unit voice line still plays in the playground: vo_select_generic played {SoundCount(l2, "vo_select_generic", "PLAY")} x", SoundCount(l2, "vo_select_generic", "PLAY") >= 1);
            OzUeControls.SetPlaying(false); OzUeControls.SetSpeed(1f);
            OzUePlayground.ClearField(); yield return Real(0.5f);
            AudioDirector.StatusLineEarliest = earliestWas;
        }
    }
}
#endif
