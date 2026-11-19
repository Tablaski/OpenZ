#if !OZ_RELEASE
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit loading_bar_real_r8 (FAILS A24): the Play check of the honest loading bar (UI/OpenZMainMenu.cs TickLoadingHonest; the log
    /// L18 red, L18 blue, the Unit editor) is started from the menu (the menu's own GoSelect / Deploy, as the soak does; GoUnitEditor) four times:
    ///  warmup  not counted: the entry's first start of this Play session (cold caches, the editor's own shader compiles) so the three
    ///          compared runs all start warm (critic round: 'before' first was the coldest and skewed FIRST PICTURE);
    ///  before  OpenZMainMenu.LoadBarHonest off AND OzStartSplit.On off (r8b, critic round): P as it is, without any part of the kit (the old bar,
    ///          no split host, no early flyby release, no hold words), so FIRST PICTURE compares P with the whole kit; expected FULL FAIL (the
    ///          owner's case: 100 % while the level still loads);
    ///  after   the kit: every bar rule must PASS (BACK, FULL, HOLD at most 80 % with its words and the wait words, STILL no other stretch over
    ///          1.0 s, WARM, SOUND); FREEZE (r8b: the bar still at most 1.0 s while the loading picture shows, the level's first frame included;
    ///          the owner's 15 Nov case) is expected to FAIL until step 2: then the headline is KNOWN-FAIL, never PASS;
    ///  after2  the kit again: the run-to-run noise of the level's first picture.
    /// The level's warm list runs on every start as in the exe (critic round: in the editor OzLevelWarm.Step returned 1 at once, so the bar's
    /// warm share 45-72 % was never tested here): before each start OzLevelWarm.ProofReset, ProofInEditor on and a cold driver emulated with
    /// WarmStepMs x WarmVariants (15 ms x 64 = about 1 s over about 32 frames: exe 15d's 988-1050 ms over 31-33 frames). Put back at the end.
    /// Each run records every frame from the click to 3 s after the level's first picture, saves the hold's picture (hold_*.png: what the long
    /// first frame stands on), the level's first picture (first_*.png) and the rows (rows_*.csv), then goes back to the menu.
    /// FIRST PICTURE: the level's first picture before vs after must differ no more than after vs after2 (the noise): mean difference at most
    /// max(1.5 x noise, 1.0 grey level of 255) and the share of pixels off by more than 24 grey levels at most max(1.5 x noise, 0.5 %).
    /// Errors and exceptions logged during the run fail it. Report: &lt;folder&gt;/report_loading_bar.txt. Test only: the switches it set are put back.
    /// </summary>
    public sealed class OzLoadBarPlayCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ loading bar check]";
        public static string Report { get; private set; } = "not run";
        public static bool Running { get; private set; }
        public static string LastFolder { get; private set; } = "";
        public static readonly string[] AllEntries = { "L02", "L07", "L16", "L17", "L18", "L18blue", "UE" };
        public static readonly string[] AllModes = { "warmup", "before", "after", "after2" };
        /// <summary>The level's warm list on every start: a cold driver emulated (ms per variant, variants).</summary>
        public static float WarmStepMs = 15f; public static int WarmVariants = 64;

        string folder; string[] entries, modes;
        int errors; readonly List<string> errorLines = new List<string>();
        readonly StringBuilder rep = new StringBuilder();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Report = "not run"; Running = false; LastFolder = ""; }

        public static void Run(string folder, string[] entries = null, string[] modes = null)
        {
            if (Running) { Debug.LogWarning(Tag + " already running"); return; }
            var go = new GameObject("OzLoadBarPlayCheck");
            DontDestroyOnLoad(go);
            var c = go.AddComponent<OzLoadBarPlayCheck>();
            c.folder = folder; c.entries = entries ?? AllEntries; c.modes = modes ?? AllModes;
            Running = true; LastFolder = folder; Report = "running";
        }

        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }
        void OnDestroy() { Running = false; }

        void OnLog(string msg, string stack, LogType t)
        {
            if (t != LogType.Error && t != LogType.Exception && t != LogType.Assert) return;
            errors++;
            if (errorLines.Count < 12) errorLines.Add(t + ": " + (msg.Length > 200 ? msg.Substring(0, 200) : msg));
        }

        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static OpenZMainMenu Menu() => FindFirstObjectByType<OpenZMainMenu>();

        static string ViewOf(OpenZMainMenu m) => typeof(OpenZMainMenu).GetField("view", F)?.GetValue(m)?.ToString() ?? "?";

        /// <summary>The menu's own start of one entry; false with the reason when the menu did not start a loading screen.</summary>
        static bool StartEntry(OpenZMainMenu m, string entry, out string why)
        {
            var t = typeof(OpenZMainMenu);
            int run0 = OpenZMainMenu.LoadBarRun;
            why = "";
            if (entry == "UE")
            {
                if (ViewOf(m) != "Main") t.GetMethod("GoMain", F, null, System.Type.EmptyTypes, null)?.Invoke(m, null);
                var ue = t.GetMethod("GoUnitEditor", F, null, System.Type.EmptyTypes, null);
                if (ue == null) { why = "no GoUnitEditor"; return false; }
                ue.Invoke(m, null);
            }
            else
            {
                string id = entry.Replace("blue", "");
                var goSelect = t.GetMethod("GoSelect", F, null, System.Type.EmptyTypes, null);
                var deploy = t.GetMethod("Deploy", F, null, System.Type.EmptyTypes, null);
                var focus = t.GetField("focusCard", F);
                if (goSelect == null || deploy == null || focus == null) { why = "no GoSelect / Deploy / focusCard"; return false; }
                int idx = -1;
                if (t.GetField("levels", F)?.GetValue(m) is OzLevelDef[] arr) for (int i = 0; i < arr.Length; i++) if (arr[i].Id == id) idx = i;
                if (idx < 0) { why = "no level card " + id; return false; }
                goSelect.Invoke(m, null);
                focus.SetValue(m, idx);
                deploy.Invoke(m, null);
                if (entry.EndsWith("blue")) OzPlayBlue.ProofArm(id);                                          // before the held scene is let in: ZGame.Awake reads it
            }
            if (OpenZMainMenu.LoadBarRun == run0) { why = "the menu did not start a loading screen (view " + ViewOf(m) + ")"; return false; }
            return true;
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            bool armedBefore = OzLoadBarLog.Armed;
            string capWas = OzLoadBarLog.CaptureDir;
            bool inEditorWas = OzLevelWarm.ProofInEditor; float coldMsWas = OzShaderWarm.ProofColdStepMs; int coldNWas = OzShaderWarm.ProofColdVariants;
            int passes = 0, fails = 0, targetMet = 0, targetAll = 0; var targetLines = new StringBuilder();   // kit loading_bar_real_r8: MAXFRAME
            int freezeFails = 0, freezeAll = 0; bool splitOnWas = OzStartSplit.On;
            rep.Append("1.5 kit loading_bar_real_r8 Play check, ").Append(System.DateTime.Now.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture)).Append('\n');
            rep.Append("entries ").Append(string.Join(" ", entries)).Append("; modes ").Append(string.Join(" ", modes)).Append("; editor ").Append(Application.isEditor).Append('\n');
            rep.Append("rules (after, after2): BACK the bar never goes back; FULL never 100 % before the level's first picture; HOLD the long first frame stands on at most 80 % with its words; STILL no other stretch over 1.0 s without a bar change; ")
               .Append("WARM (levels) the warm list ran (at least ").Append(OzLoadBarLog.WarmFramesMin).Append(" frames) and the bar went through its share 46-71 % (at least ").Append(OzLoadBarLog.BandFramesMin).Append(" frames); ")
               .Append("SOUND the done sound once, on the let-in frame, not on a step tick's frame; ")
               .Append("FREEZE (r8b, the owner's 15 Nov case) the bar never still over ").Append(OzLoadBarLog.FreezeMaxS.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s while the loading picture shows, the level's first frame included\n")
               .Append("before = P as it is (old bar, OzStartSplit off, no early flyby release, no hold words): FIRST PICTURE compares P with the whole kit\n");
            rep.Append("warm list on every start: cold driver emulated, ").Append(WarmVariants).Append(" variants x ").Append(WarmStepMs.ToString("0", CultureInfo.InvariantCulture)).Append(" ms; 'warmup' runs are not counted\n\n");
            var results = new Dictionary<string, bool>();
            foreach (var entry in entries)
                foreach (var mode in modes)
                {
                    float w0 = Time.realtimeSinceStartup; OpenZMainMenu m = null;
                    while (Time.realtimeSinceStartup - w0 < 90f && !(m = Menu())) yield return null;
                    if (!m) { rep.Append(entry).Append(' ').Append(mode).Append(": FAIL no main menu in 90 s\n"); fails++; continue; }
                    yield return new WaitForSecondsRealtime(4f);                                                // the menu built, its previews going
                    m = Menu();
                    if (!m) { rep.Append(entry).Append(' ').Append(mode).Append(": FAIL the main menu went away\n"); fails++; continue; }
                    OpenZMainMenu.LoadBarHonest = mode != "before";
                    OzStartSplit.On = mode == "before" ? false : splitOnWas;                                             // r8b: 'before' without the kit's always-on host
                    OzLevelWarm.ProofReset(); OzLevelWarm.ProofInEditor = true;                                        // the warm list as in the exe, every start the same
                    OzShaderWarm.ProofColdStepMs = WarmStepMs; OzShaderWarm.ProofColdVariants = WarmVariants;
                    OzLoadBarLog.RequireWarm = entry != "UE";                                                           // the Unit editor has no warm list (as before)
                    OzLoadBarLog.CaptureDir = folder; OzLoadBarLog.CaptureName = entry + "_" + mode;
                    OzLoadBarLog.Arm();
                    int runs0 = OzLoadBarLog.Runs;
                    if (!StartEntry(m, entry, out var why)) { rep.Append(entry).Append(' ').Append(mode).Append(": FAIL ").Append(why).Append('\n'); fails++; continue; }   // a warmup that cannot start fails too: the menu is broken
                    float s0 = Time.realtimeSinceStartup;
                    while (OzLoadBarLog.Runs == runs0 && Time.realtimeSinceStartup - s0 < 240f) yield return null;
                    if (OzLoadBarLog.Runs == runs0) { rep.Append(entry).Append(' ').Append(mode).Append(": FAIL no level picture in 240 s\n"); fails++; }
                    else
                    {
                        bool p = OzLoadBarLog.LastPass;
                        results[entry + "_" + mode] = p;
                        string what = mode == "before" ? (p ? "  (before: the old bar passed: the owner's case NOT reproduced)" : "  (before: expected FAIL, the owner's case)")
                                    : mode == "warmup" ? "  (warmup: not counted)" : "";
                        rep.Append(entry).Append(' ').Append(mode).Append(": ").Append(OzLoadBarLog.LastSummary).Append(what).Append('\n');
                        if (mode == "after" || mode == "after2")                                                     // r8b: the bar rules count; FREEZE apart (KNOWN-FAIL)
                        {
                            if (OzLoadBarLog.LastBarPass) passes++; else fails++;
                            freezeAll++; if (!OzLoadBarLog.LastFreezeOk) freezeFails++;
                        }
                        if (mode == "after" || mode == "after2")                                                     // kit loading_bar_real_r8: the A24 target, apart from PASS
                        {
                            targetAll++; if (OzLoadBarLog.LastTargetMet) targetMet++;
                            targetLines.Append(entry).Append(' ').Append(mode).Append(": longest frame while the loading picture shows ").Append(OzLoadBarLog.LastMaxFrameMs.ToString("0", CultureInfo.InvariantCulture))
                                       .Append(" ms (").Append(OzLoadBarLog.LastTargetMet ? "met" : "NOT MET").Append("); bar still ").Append(OzLoadBarLog.LastFreezeS.ToString("0.00", CultureInfo.InvariantCulture))
                                       .Append(" s (FREEZE ").Append(OzLoadBarLog.LastFreezeOk ? "ok" : "FAIL").Append("); hidden flybys released before the let-in ").Append(OpenZMainMenu.LoadBarPreviewsReleased).Append("; split: ").Append(OzStartSplit.LastLevel == OzLoadBarLog.LastLevel ? OzStartSplit.LastLine : "none").Append('\n');
                        }
                    }
                    OpenZMainMenu.LoadBarHonest = true; OzStartSplit.On = splitOnWas;
                    OzScenes.MainMenu();
                    yield return new WaitForSecondsRealtime(1f);
                }
            OpenZMainMenu.LoadBarHonest = true; OzStartSplit.On = splitOnWas;
            OzLoadBarLog.CaptureDir = capWas; OzLoadBarLog.CaptureName = ""; OzLoadBarLog.RequireWarm = false;
            OzLevelWarm.ProofReset(); OzLevelWarm.ProofInEditor = inEditorWas; OzShaderWarm.ProofColdStepMs = coldMsWas; OzShaderWarm.ProofColdVariants = coldNWas;   // put back
            if (!armedBefore) OzLoadBarLog.Disarm();
            yield return new WaitForSecondsRealtime(1f);                                                        // the last pictures written
            rep.Append("\nFIRST PICTURE (before vs after against after vs after2: mean grey-level difference, share of pixels off by more than 24)\n");
            foreach (var entry in entries)
            {
                string a = Path.Combine(folder, "first_" + entry + "_before.png"), b = Path.Combine(folder, "first_" + entry + "_after.png"), c = Path.Combine(folder, "first_" + entry + "_after2.png");
                if (!File.Exists(a) || !File.Exists(b)) { rep.Append(entry).Append(": no first pictures to compare (").Append(File.Exists(a) ? "" : "before ").Append(File.Exists(b) ? "" : "after").Append(")\n"); if (System.Array.IndexOf(modes, "before") >= 0) fails++; continue; }
                (float mean, float share, string why) d = Diff(a, b);
                (float mean, float share, string why) n = (0f, 0f, null);                                     // no after2: the noise counts as 0
                if (File.Exists(c)) n = Diff(b, c);
                if (d.why != null || n.why != null) { rep.Append(entry).Append(": FAIL ").Append(d.why ?? n.why).Append('\n'); fails++; continue; }
                bool ok = d.mean <= Mathf.Max(1.5f * n.mean, 1.0f) && d.share <= Mathf.Max(1.5f * n.share, 0.005f);
                rep.Append(entry).Append(": ").Append(ok ? "PASS" : "FAIL").Append(" before/after mean ").Append(d.mean.ToString("0.00", CultureInfo.InvariantCulture))
                   .Append(", off ").Append((d.share * 100f).ToString("0.00", CultureInfo.InvariantCulture)).Append(" %; noise (after/after2) mean ")
                   .Append(n.mean.ToString("0.00", CultureInfo.InvariantCulture)).Append(", off ").Append((n.share * 100f).ToString("0.00", CultureInfo.InvariantCulture)).Append(" %\n");
                if (ok) passes++; else fails++;
            }
            if (errors > 0) { fails++; rep.Append("\nERRORS logged during the run: ").Append(errors).Append('\n'); foreach (var l in errorLines) rep.Append("  ").Append(l).Append('\n'); }
            rep.Append("\nA24 TARGET MAXFRAME (kit loading_bar_real_r8; editor numbers are editor numbers, the exe check decides): met on ").Append(targetMet).Append(" of ").Append(targetAll)
               .Append(" after / after2 starts (target: every frame while the loading picture shows <= ").Append(OzLoadBarLog.MaxFrameTargetMs.ToString("0", CultureInfo.InvariantCulture)).Append(" ms)\n").Append(targetLines);
            rep.Append("FREEZE (owner 15 Nov case; the bar still over ").Append(OzLoadBarLog.FreezeMaxS.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s while the loading picture shows): failed on ")
               .Append(freezeFails).Append(" of ").Append(freezeAll).Append(" after / after2 starts").Append(freezeFails > 0 ? " = KNOWN-FAIL (A24 open until step 2, the start spread)" : "").Append('\n');
            bool pass = fails == 0 && passes > 0;
            string word = !pass ? "FAIL" : freezeFails > 0 ? "KNOWN-FAIL" : "PASS";                              // r8b: never PASS while the owner's freeze stands
            rep.Append('\n').Append(word).Append(' ').Append(passes).Append(" / ").Append(fails)
               .Append(word == "KNOWN-FAIL" ? " (every bar rule, FIRST PICTURE and the error count PASS; only FREEZE fails: the owner's case is NOT fixed by step 1)" : "").Append('\n');
            Report = word + " " + passes + " / " + fails + "; FREEZE failed " + freezeFails + " of " + freezeAll + "; A24 target MAXFRAME met " + targetMet + " of " + targetAll + " (" + folder + ")";
            try { File.WriteAllText(Path.Combine(folder, "report_loading_bar.txt"), rep.ToString()); } catch (System.Exception e) { Debug.LogWarning(Tag + " report not saved: " + e.Message); }
            Debug.Log(Tag + " " + Report + "\n" + rep);
            Running = false;
            Destroy(gameObject);
        }

        /// <summary>Mean grey-level difference (0..255, all channels) and the share of pixels with a channel off by more than 24.</summary>
        static (float mean, float share, string why) Diff(string pa, string pb)
        {
            var ta = new Texture2D(2, 2, TextureFormat.RGBA32, false); var tb = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ta.LoadImage(File.ReadAllBytes(pa)) || !tb.LoadImage(File.ReadAllBytes(pb))) return (0f, 0f, "a picture did not load");
                if (ta.width != tb.width || ta.height != tb.height) return (0f, 0f, $"sizes differ {ta.width}x{ta.height} / {tb.width}x{tb.height}");
                var a = ta.GetPixels32(); var b = tb.GetPixels32();
                double sum = 0; int off = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    int dr = Mathf.Abs(a[i].r - b[i].r), dg = Mathf.Abs(a[i].g - b[i].g), db = Mathf.Abs(a[i].b - b[i].b);
                    sum += dr + dg + db;
                    if (dr > 24 || dg > 24 || db > 24) off++;
                }
                return ((float)(sum / (3.0 * a.Length)), off / (float)a.Length, null);
            }
            finally { Destroy(ta); Destroy(tb); }
        }
    }
}
#endif
