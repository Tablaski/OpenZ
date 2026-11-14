// OpenZ (GPL-3.0) - version 1.2 Play proof of EvilZod's lines on the blue card (kit k75b2; editor only, menu OpenZ/V1.2/Blue Cutscenes:
// 75b2 2 Card Lines Play Proof). Started on the MainMenu scene in Play it walks by itself (about 90 s), hands off:
//  P1  the main menu's own PLAY opens the faction screen, the posters load.
//  P2  GENERAL EVILZOD picked (RIGHT, ENTER, as the keys do): Zod's "You chose... poorly." on its own source, then the blue battlefield
//      screen; the blue card with EvilZod's feed opens, the speaker GEN EVILZOD, the company's name; no line without a click (2 s).
//  P3  each level's card clicked (the menu's own click): EvilZod says ONE of that level's lines (two; three for level 16) (the sound
//      log: one PLAY of key evil_card_line, the clip one of that level's), after the 0.8 s focus and his 0.45 s push-in; the subtitle on
//      the card is a cue of that line, his close-up, his jaw / eyes on his voice (the stage's mouth level); after it the company's name,
//      the two-shot, the mouth shut; no second line (never two); the same card clicked again in the same visit: silent. A picture of the
//      card while he speaks for each level (full screen + the card cut out).
//  P4  a new visit (BACK, EvilZod picked again): Zod's joke source played again and level 16's card clicked at once: EvilZod waits until
//      that voice has ended (+0.3 s), then says one of level 16's three lines (a new draw).
//  P5  the screen left mid-line (BACK): his line fades out (gone in 0.5 s, the sound log's FADE), the card folds, no line after.
//  ALL frames: EvilZod's voice never plays together with another voice (the faction joke's source, the commander's, the announcer /
//      robots of the director).
// The player's settings are put back (the side, 'Zod exact', the Game view's size). Lines, sound log and pictures in
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace OpenZ
{
    public sealed class OzEvilCardLinesProof : MonoBehaviour
    {
        public static string Report = "idle";
        public static OzEvilCardLinesProof I { get; private set; }
        const string T = "[OzEvilCardLinesProof] ";
        const BindingFlags IF = BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly string[] Levels = { "L02", "L16", "L17" };

        string dir; readonly StringBuilder sb = new StringBuilder(); int passes, fails, pics;
        bool started, putBack, boxWas, hadExact, watching; int exactWas, viewWas = -1; int[] heardWas;   // 1.3 item 6: Zod's pick-line marks, put back at the end
        int evilFrames, overlapFrames; string overlapWhat = ""; float runL0;

        public static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning(T + "enter Play on the MainMenu scene first"); return; }
            if (I) { Debug.LogWarning(T + "already running"); return; }
            if (!FindFirstObjectByType<OpenZMainMenu>()) { Debug.LogWarning(T + "start it on the main menu (the MainMenu scene in Play)"); return; }
            Report = "running";
            var go = new GameObject("OzEvilCardLinesProof"); DontDestroyOnLoad(go);
            go.AddComponent<OzEvilCardLinesProof>();
        }

        void Awake() { I = this; }

        void Start()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/blue_evil_card_lines_k75b2"));
            int n = 1;
            while (Directory.Exists(Path.Combine(root, "play_" + n))) n++;
            dir = Path.Combine(root, "play_" + n); Directory.CreateDirectory(dir);
            boxWas = OzPlayBlue.Box; hadExact = PlayerPrefs.HasKey("oz_zod_exact"); exactWas = PlayerPrefs.GetInt("oz_zod_exact", 0);
            heardWas = OzFaction.ProofHeardKeep();
            viewWas = OzFinalGateView.SelectedIndex();
            started = true; runL0 = LogNow;
            Log("k75b2 EvilZod card lines Play proof, Game view " + Screen.width + " x " + Screen.height + ", into " + dir);
            StartCoroutine(All());
        }

        void OnDestroy()
        {
            if (started && !putBack) { PutBack(); Debug.Log(T + "stopped before its end: the player's settings put back"); }
            if (I == this) I = null;
        }

        void PutBack()
        {
            putBack = true; watching = false;
            OzPlayBlue.Box = boxWas;
            if (hadExact) PlayerPrefs.SetInt("oz_zod_exact", exactWas); else PlayerPrefs.DeleteKey("oz_zod_exact");
            OzFaction.ProofHeardPutBack(heardWas);                                                           // 1.3 item 6
            PlayerPrefs.Save();
            if (viewWas >= 0) OzFinalGateView.Select(viewWas);
        }

        void Finish()
        {
            watching = false;
            Check($"ALL EvilZod's voice never together with another voice: {overlapFrames} of {evilFrames} frames of his voice{(overlapFrames > 0 ? " (" + overlapWhat + ")" : "")}",
                  overlapFrames == 0 && evilFrames > 0);
            var log = OzSoundLog.Since(runL0).Where(e => e.src == "EvilZod" || (e.key != null && (e.key.StartsWith("comms_blue") || e.key.StartsWith("evil_card")))).ToList();
            File.WriteAllText(Path.Combine(dir, "soundlog.txt"), OzSoundLog.Format(log));
            Log("   sound log of the run (EvilZod + the card's static): " + log.Count + " entries in soundlog.txt");
            if (!putBack) PutBack();
            var m = Menu(); if (m && m.ProofView == "Faction") m.ProofFactionBack();
            Report = $"{T}{(fails == 0 ? "PASS" : "FAIL")} {passes}/{passes + fails} checks, {pics} pictures; {dir.Replace('\\', '/')}";
            Log(Report);
            File.WriteAllText(Path.Combine(dir, "checks.txt"), sb.ToString());
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------------------------------------ helpers
        void Log(string t) { sb.AppendLine(t); Debug.Log(T + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }
        static IEnumerator Until(System.Func<bool> c, float max) { float t = Time.realtimeSinceStartup + max; while (Time.realtimeSinceStartup < t && !SafeTrue(c)) yield return null; }
        static bool SafeTrue(System.Func<bool> c) { try { return c(); } catch (System.Exception) { return false; } }
        static OpenZMainMenu Menu() => FindFirstObjectByType<OpenZMainMenu>();
        static float LogNow => Time.unscaledTime;
        static List<OzSoundLog.Entry> Lines(float l0, string outcome) => OzSoundLog.Since(l0).Where(e => e.key == "evil_card_line" && e.outcome != null && e.outcome.StartsWith(outcome)).ToList();
        static int Card(string lv) => System.Array.FindIndex(OzLevelDef.All, d => d.Id == lv);
        static string Ids(string lv) => string.Join(" / ", OzEvilCardLines.ForLevel(lv).Select(x => x.Id));

        /// <summary>A click on a level card as the player's (the menu's own click: focus, the select sound, the click time).</summary>
        static void Click(OpenZMainMenu m, int card)
        {
            var mi = typeof(OpenZMainMenu).GetMethod("ClickCard", IF);
            if (mi != null) { mi.Invoke(m, new object[] { card }); return; }
            m.ProofFocus(card);                                                                                 // no click method: focus + the click's time, as the older proofs
            typeof(OpenZMainMenu).GetField("lastCardClick", IF)?.SetValue(m, card);
            typeof(OpenZMainMenu).GetField("lastCardClickAt", IF)?.SetValue(m, OzMotion.Now);
        }

        /// <summary>The main menu's own PLAY action (the item a click on PLAY runs).</summary>
        static bool Play(OpenZMainMenu m)
        {
            var items = typeof(OpenZMainMenu).GetField("items", IF)?.GetValue(m) as IList;
            if (items == null) return false;
            foreach (var it in items)
            {
                var ft = it.GetType();
                if (ft.GetField("label")?.GetValue(it) as string != "PLAY") continue;
                var act = ft.GetField("act")?.GetValue(it) as System.Action;
                if (act == null) return false;
                act(); return true;
            }
            return false;
        }

        IEnumerator Shot(string name)
        {
            string p = Path.Combine(dir, name + "_" + Screen.width + "x" + Screen.height + ".png");
            ScreenCapture.CaptureScreenshot(p); pics++; Log("   picture " + Path.GetFileName(p));
            yield return OzProofPictures.AfterGrab(); yield return OzProofPictures.Written(p);
        }

        /// <summary>The card cut out of the screen (its window's rectangle + a margin), at the screen's own pixels.</summary>
        IEnumerator CardShot(string name)
        {
            yield return new WaitForEndOfFrame();
            var w = ZodBriefing.EvilCardWindow;
            if (!w) { Log("   no card window to cut out"); yield break; }
            var c = new Vector3[4]; w.GetWorldCorners(c);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(c[0].x) - 12, 0, Screen.width - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(c[0].y) - 12, 0, Screen.height - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(c[2].x) + 12, x0 + 1, Screen.width), y1 = Mathf.Clamp(Mathf.CeilToInt(c[2].y) + 12, y0 + 1, Screen.height);
            var full = ScreenCapture.CaptureScreenshotAsTexture();
            var crop = new Texture2D(x1 - x0, y1 - y0, TextureFormat.RGB24, false);
            crop.SetPixels(full.GetPixels(x0, y0, x1 - x0, y1 - y0)); crop.Apply(false);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), crop.EncodeToPNG());
            Destroy(full); Destroy(crop); pics++;
            Log("   picture " + name + ".png (the card, " + (x1 - x0) + " x " + (y1 - y0) + " px)");
        }

        IEnumerator SetSize(int w, int h)
        {
            OzFinalGateView.SetFixed(w, h, "OpenZ " + w + "x" + h, out string info);
            yield return Until(() => Screen.width == w && Screen.height == h, 5f);
            yield return Real(0.8f);
            if (Screen.width != w || Screen.height != h) Check($"the Game view shows {w} x {h} ({info}; it shows {Screen.width} x {Screen.height})", false);
            else Log("Game view " + w + " x " + h + " (" + info + ")");
        }

        /// <summary>Every frame of the run: EvilZod's voice and any other voice at the same time.</summary>
        IEnumerator Watch()
        {
            while (watching)
            {
                var ev = ZodBriefing.EvilVoice; bool evil = ev && ev.isPlaying;
                if (evil)
                {
                    evilFrames++;
                    var m = Menu(); var fv = m ? m.ProofFactionVoice : null; var cv = ZodBriefing.CommanderVoice;
                    string other = fv && fv != ev && fv.isPlaying ? "the faction joke " + (fv.clip ? fv.clip.name : "") : cv && cv != ev && cv.isPlaying ? "the commander's source"
                                 : AudioDirector.VoiceBusy ? "the announcer / robots" : null;
                    if (other != null) { overlapFrames++; if (overlapWhat.Length < 200) overlapWhat += (overlapWhat.Length > 0 ? "; " : "") + other + " at " + Time.realtimeSinceStartup.ToString("0.00"); }
                }
                yield return null;
            }
        }

        // ------------------------------------------------------------------------------------------------ the walk
        IEnumerator All()
        {
            yield return Real(1f);
            if (!ZodBriefing.Enabled || GameBoot.TestMode || !ZodBriefing.I) { Check($"the cameo is on in the main menu (oz_zod_cameo {(ZodBriefing.Enabled ? 1 : 0)}, test mode {GameBoot.TestMode}, ZodBriefing {(ZodBriefing.I ? "there" : "MISSING")})", false); Finish(); yield break; }
            if (PlayerPrefs.GetInt("oz_zod_exact", 0) == 1) { PlayerPrefs.SetInt("oz_zod_exact", 0); Log("'Zod exact' off for the run (the EvilZod poster is locked there); put back at the end"); }
            yield return SetSize(1920, 1080);
            watching = true; StartCoroutine(Watch());
            var m = Menu();
            yield return OpenFaction("P1");
            yield return PickEvil("P2", true);
            foreach (var lv in Levels) yield return SpeakOn(m, lv, "P3");
            yield return OtherVoiceFirst(m);
            yield return LeaveMidLine(m);
            Finish();
        }

        IEnumerator OpenFaction(string tag)
        {
            var m = Menu();
            if (m.ProofView != "Main") { m.ProofMain(); yield return Real(0.8f); }
            bool ran = Play(m);
            if (!ran) m.ProofFaction();
            yield return Until(() => m.ProofFactionShown, 3f);
            yield return Until(() => m.ProofRig && m.ProofRig.Ready && m.ProofPrintAlpha(1) > 0.99f, 12f);
            Check($"{tag} the main menu's PLAY opens the faction screen (by its own action {ran}), view {m.ProofView}, the posters {(m.ProofRig && m.ProofRig.Ready ? "loaded" : "NOT LOADED")}",
                  m.ProofView == "Faction" && m.ProofRig && m.ProofRig.Ready);
            yield return Real(0.5f);
        }

        IEnumerator PickEvil(string tag, bool first)
        {
            var m = Menu();
            m.ProofFactionKeys(false, true, false, false); yield return Real(0.5f);                          // RIGHT: EvilZod's poster
            OzFaction.ProofForgetLine();                                                                      // 1.3 item 6: every pick here is a first pick (P2 / P4 wait on Zod's joke)
            m.ProofFactionKeys(false, false, true, false);                                                    // ENTER: the pick
            var fv = m.ProofFactionVoice;
            bool joke = fv && fv.isPlaying && fv.clip && fv.clip.name == "zod_pick_blue";
            float len = fv && fv.clip ? fv.clip.length : 0f;
            yield return Until(() => m.ProofView == "Select", len + 3f);
            Check($"{tag} GENERAL EVILZOD picked: Zod's joke {(fv && fv.clip ? fv.clip.name : "none")} on its own source {joke}, then the blue battlefield screen (view {m.ProofView}, tag \"{m.ProofSideTag}\", the side {(OzPlayBlue.BoxOn ? "BLUE" : "RED")})",
                  joke && m.ProofView == "Select" && OzPlayBlue.BoxOn && m.ProofSideTag == "GENERAL EVILZOD");
            yield return Until(() => ZodBriefing.EvilCardShown, 3f);
            yield return Real(0.6f);
            Check($"{tag} the blue card opens with EvilZod's feed: shown {ZodBriefing.EvilCardShown}, the feed {ZodBriefing.CommsFeedShown}, speaker \"{ZodBriefing.CommsSpeaker}\", the line \"{ZodBriefing.EvilCardCue}\"",
                  ZodBriefing.EvilCardShown && ZodBriefing.CommsFeedShown && ZodBriefing.CommsSpeaker == OzBlueScenes.EvilZodLabel && ZodBriefing.EvilCardCue == OzBlueScenes.Company);
            if (!first) yield break;
            int s0 = ZodBriefing.EvilLinesStarted;
            yield return Real(2f);
            Check($"{tag} no line without a click: {ZodBriefing.EvilLinesStarted - s0} line(s) in 2 s on the card (as the commander's rule: only a clicked card speaks)", ZodBriefing.EvilLinesStarted == s0);
            yield return Shot(tag + "_card_idle");
            yield return CardShot(tag + "_card_idle_crop");
        }

        IEnumerator SpeakOn(OpenZMainMenu m, string lv, string tag)
        {
            int card = Card(lv);
            var lines = OzEvilCardLines.ForLevel(lv);
            bool wasFocused = m.ProofFocusCard == card;
            float l0 = LogNow; int started0 = ZodBriefing.EvilLinesStarted, played0 = ZodBriefing.EvilLinesPlayed;
            float t0 = Time.realtimeSinceStartup;
            Click(m, card);
            yield return Until(() => ZodBriefing.EvilLinesPlayed > played0, 6f);
            float delay = Time.realtimeSinceStartup - t0;
            string id = ZodBriefing.EvilLastLineId;
            var line = lines.FirstOrDefault(x => x.Id == id);
            var plays = Lines(l0, "PLAY");
            float minDelay = wasFocused ? 0.4f : 1.2f;
            Check($"{tag} {lv}'s card clicked: EvilZod says {id} ({delay:0.00} s after the click, {minDelay:0.0} s at least: {(wasFocused ? "the card was focused already, " : "the 0.8 s focus + ")}the 0.45 s push-in), one of [{Ids(lv)}]; the sound log: {plays.Count} PLAY ({string.Join(", ", plays.Select(e => e.clip))})",
                  line != null && delay >= minDelay && delay <= 4f && plays.Count == 1 && plays[0].clip == id);
            if (line == null) yield break;
            var ev = ZodBriefing.EvilVoice;
            yield return Real(0.6f);
            float t = ev && ev.clip ? ev.timeSamples / (float)ev.clip.frequency : -1f;
            string cue = ZodBriefing.EvilCardCue;
            Check($"{tag} {lv} his words on the card at {t:0.00} s: \"{cue}\" (a cue of \"{line.Text}\"), speaker \"{ZodBriefing.CommsSpeaker}\", the camera on his close-up ({ZodBriefing.EvilCardCamera}), his voice at {(ev ? ev.volume : 0f):0.00}",
                  line.Cues.Any(c => c.Text == cue) && ZodBriefing.CommsSpeaker == OzBlueScenes.EvilZodLabel && ZodBriefing.EvilCardCamera == (OzEvilCardLines.StaysOnHim(line) ? "ez_med" : "ez_close") && ev && ev.isPlaying && ev.volume > 0.05f);
            yield return Shot($"{tag}_{lv}_speaking");
            yield return CardShot($"{tag}_{lv}_speaking_crop");
            // his jaw and eyes on his voice until the line ends
            float mMax = 0f; int frames = 0, open = 0, shutFrames = 0, voiceFrames = 0;
            float end = Time.realtimeSinceStartup + line.Duration + 3f;
            while (ZodBriefing.EvilLineOn && Time.realtimeSinceStartup < end)
            {
                frames++; float mo = ZodBriefing.EvilMouth;
                if (ev && ev.isPlaying) { voiceFrames++; mMax = Mathf.Max(mMax, mo); if (mo > 0.15f) open++; else shutFrames++; }
                yield return null;
            }
            Check($"{tag} {lv} his jaw / eyes follow his voice: mouth up to {mMax:0.00}, open (> 0.15) in {open} of {voiceFrames} voiced frames, shut in {shutFrames} (the pauses)",
                  mMax >= 0.3f && open >= voiceFrames / 6 && shutFrames > 0 && voiceFrames > 0);
            yield return Real(1.2f);                                                                          // the camera eases back (0.8 s)
            Check($"{tag} {lv} after his line: the line \"{ZodBriefing.EvilCardCue}\", the camera {ZodBriefing.EvilCardCamera}, the mouth {ZodBriefing.EvilMouth:0.00}",
                  ZodBriefing.EvilCardCue == OzBlueScenes.Company && ZodBriefing.EvilCardCamera == (OzEvilCardLines.StaysOnHim(line) ? "ez_med" : "two") && ZodBriefing.EvilMouth < 0.05f && !ZodBriefing.EvilLineOn);
            yield return Real(2f);
            plays = Lines(l0, "PLAY");
            Check($"{tag} {lv} never two: {plays.Count} line(s) heard since the click ({string.Join(", ", plays.Select(e => e.clip))}), {ZodBriefing.EvilLinesStarted - started0} started",
                  plays.Count == 1 && ZodBriefing.EvilLinesStarted == started0 + 1);
            int s1 = ZodBriefing.EvilLinesStarted; float l1 = LogNow;
            Click(m, card);
            yield return Real(3f);
            Check($"{tag} {lv}'s card clicked again in the same visit: {ZodBriefing.EvilLinesStarted - s1} line(s), {Lines(l1, "PLAY").Count} heard (a level speaks once per visit)",
                  ZodBriefing.EvilLinesStarted == s1 && Lines(l1, "PLAY").Count == 0);
        }

        IEnumerator OtherVoiceFirst(OpenZMainMenu m)
        {
            m.ProofBackFromSelect();
            yield return Until(() => m.ProofFactionShown, 3f);
            yield return Until(() => m.ProofRig && m.ProofRig.Ready && m.ProofPrintAlpha(1) > 0.99f, 12f);
            yield return Real(0.5f);
            yield return PickEvil("P4", false);
            yield return Real(0.5f);
            var fv = m.ProofFactionVoice;
            if (!fv || !fv.clip) { Check("P4 the faction joke's source for the test", false); yield break; }
            int played0 = ZodBriefing.EvilLinesPlayed; float l0 = LogNow;
            fv.Stop(); fv.Play();                                                                             // another voice on the air
            Click(m, Card("L16"));
            float fvEnd = -1f, evStart = -1f, lim = Time.realtimeSinceStartup + fv.clip.length + 6f;
            while (Time.realtimeSinceStartup < lim && evStart < 0f)
            {
                if (fvEnd < 0f && !fv.isPlaying) fvEnd = Time.realtimeSinceStartup;
                if (ZodBriefing.EvilLinesPlayed > played0) evStart = Time.realtimeSinceStartup;
                yield return null;
            }
            string id = ZodBriefing.EvilLastLineId;
            bool inPair = OzEvilCardLines.ForLevel("L16").Any(x => x.Id == id);
            Check($"P4 a new visit, level 16 clicked while Zod's joke plays ({fv.clip.length:0.00} s): EvilZod's voice starts {(evStart > 0f && fvEnd > 0f ? (evStart - fvEnd).ToString("0.00") : "?")} s after the joke ended (0.3 s quiet wanted), the line {id} (one of [{Ids("L16")}]), {Lines(l0, "PLAY").Count} heard",
                  fvEnd > 0f && evStart > 0f && evStart - fvEnd >= 0.25f && inPair && Lines(l0, "PLAY").Count == 1);
            yield return Until(() => !ZodBriefing.EvilLineOn, 10f);
            yield return Real(1f);
        }

        IEnumerator LeaveMidLine(OpenZMainMenu m)
        {
            int played0 = ZodBriefing.EvilLinesPlayed, faded0 = ZodBriefing.EvilLinesFaded; float l0 = LogNow;
            Click(m, Card("L17"));
            yield return Until(() => ZodBriefing.EvilLinesPlayed > played0, 6f);
            yield return Real(1f);
            var ev = ZodBriefing.EvilVoice;
            bool was = ev && ev.isPlaying;
            yield return Shot("P5_L17_speaking_before_back");
            float t0 = Time.realtimeSinceStartup;
            m.ProofBackFromSelect();
            yield return Until(() => !(ev && ev.isPlaying), 2f);
            float gone = Time.realtimeSinceStartup - t0;
            yield return Real(1f);
            int fades = Lines(l0, "FADE").Count;
            Check($"P5 the screen left mid-line (BACK): his voice was on {was}, gone {gone:0.00} s after (0.3 s fade), faded {ZodBriefing.EvilLinesFaded - faded0} (sound log FADE {fades}), the card {(ZodBriefing.EvilCardShown ? "STILL UP" : "folded")}, view {m.ProofView}",
                  was && gone <= 0.5f && ZodBriefing.EvilLinesFaded == faded0 + 1 && fades == 1 && !ZodBriefing.EvilCardShown && m.ProofView == "Faction");
            int s1 = ZodBriefing.EvilLinesStarted;
            yield return Real(1.5f);
            Check($"P5 no line after the screen was left: {ZodBriefing.EvilLinesStarted - s1} started, the line on air {ZodBriefing.EvilLineOn}", ZodBriefing.EvilLinesStarted == s1 && !ZodBriefing.EvilLineOn);
        }
    }
}
#endif
