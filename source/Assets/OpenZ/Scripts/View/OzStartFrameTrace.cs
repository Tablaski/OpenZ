// OpenZ (GPL-3.0) - version 1.2, owner item 86 : the frame-time trace of a
// level's first seconds. Armed before a level loads (the menu OpenZ/V1.2/Blue Flyovers, the command line -oz-frametrace, or Arm from code),
// it records EVERY frame from the level scene's load for Seconds s: the frame's length (ms) and what came on in that frame (the land beyond
// the jungle's rim built, the distance rule's first collect, the HUD built, a briefing / flyover on or off, the jungle's low look, the side).
// The frame rate cap and the vertical sync are taken off for those seconds only (the editor's 30 cap says nothing about frame times) and
// put back. It writes a CSV (frame, t, dt, marks) and a one-page summary (per second: frames, mean, longest; frames over 33 and 50 ms;
// heard or simulated changes.
// Owner items 86 / 142: it also runs until Seconds after the first picture, marks the start work done before the first picture
// (OzStartAhead.cs), and the summary ends with the verdict on the frames the player sees (OzStartFrameTrace.Seen.cs: PASS / FAIL).
// Play check: OzStartFrameTrace.LastSummary, OzStartFrameTrace.LastFile, OzStartFrameTrace.LastSeen / LastPass.
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    public sealed partial class OzStartFrameTrace : MonoBehaviour
    {
        public static OzStartFrameTrace I { get; private set; }
        public static bool Armed { get; private set; }
        public static string Label = "", LastFile = "none yet", LastSummary = "none yet";
        public static float Seconds = 10f;
        public static string Folder = "";

        readonly List<float> dts = new List<float>(2048), ts = new List<float>(2048);
        readonly List<string> marks = new List<string>(2048);
        readonly List<(float t, string mark)> events = new List<(float, string)>();
        float t0; int capWas, vsyncWas; bool done;
        bool landBuilt, collected, hudBuilt, briefing, flying, lowLook, ahead; string level = "?", side = "?";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; Armed = false; Label = ""; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene;
            foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-oz-frametrace") Arm("exe");
        }

        /// <summary>Records the next level that loads (or the level that runs now) for `seconds` s under the name `label`.</summary>
        public static void Arm(string label, float seconds = 10f)
        {
            Label = label ?? ""; Seconds = Mathf.Clamp(seconds, 2f, 60f); Armed = true;
            if (I) { Destroy(I.gameObject); I = null; }
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (g && g.World != null) Begin(g.gameObject.scene);
        }

        static void OnScene(Scene s, LoadSceneMode m)
        {
            if (!Armed || m != LoadSceneMode.Single || !Application.isPlaying) return;
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!g) return;
            Begin(s);
        }

        static void Begin(Scene s)
        {
            Armed = false;
            var go = new GameObject("OzStartFrameTrace");
            if (s.IsValid() && go.scene != s) SceneManager.MoveGameObjectToScene(go, s);
            go.AddComponent<OzStartFrameTrace>();
        }

        void Awake()
        {
            I = this; t0 = Time.realtimeSinceStartup;
            capWas = Application.targetFrameRate; vsyncWas = QualitySettings.vSyncCount;
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            level = g ? g.level : "?"; side = OzPlayBlue.Active ? "blue" : "red";
            if (string.IsNullOrEmpty(Folder)) Folder = DefaultFolder();
        }

        void OnDestroy()
        {
            if (!done) Restore();
            if (I == this) I = null;
        }

        void Restore() { Application.targetFrameRate = capWas; QualitySettings.vSyncCount = vsyncWas; }

        static string DefaultFolder()
        {
            var d = new System.IO.DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
                if (System.IO.Directory.Exists(System.IO.Path.Combine(d.FullName, "work", "records")))
                    return System.IO.Path.Combine(d.FullName, "work", "records", "milestones", "blue_flyovers_k75c");
            return System.IO.Path.Combine(Application.persistentDataPath, "blue_flyovers_k75c");
        }

        void Update()
        {
            if (done) return;
            float t = Time.realtimeSinceStartup - t0, dt = Time.unscaledDeltaTime * 1000f;
            var sb = new StringBuilder();
            Mark(sb, ref landBuilt, OzL16Outskirts.BuildMs > 0f, "land beyond the rim built (" + OzL16Outskirts.BuildMs.ToString("0") + " ms over " + OzL16Outskirts.BuildFrames + " frames)", t);
            Mark(sb, ref collected, DrawAheadCollected(), "distance rule collected " + OzL16DrawAhead.Watched + " groups", t);
            Mark(sb, ref hudBuilt, OpenZHud.I && OpenZHud.I.Canvas, "HUD built", t);
            Flag(sb, ref briefing, ZodBriefing.BriefingActive, "briefing on", "briefing off", t);
            Flag(sb, ref flying, ZodFlyover.Live != null && ZodFlyover.Live.Active, "flyover on", "flyover off", t);
            Flag(sb, ref lowLook, OzL16FlyoverFlora.LowLookOn, "low look on", "low look off", t);
            Mark(sb, ref ahead, OzStartAhead.Done, "the start work done before the first picture (" + OzStartAhead.WorkMs.ToString("0") + " ms)", t);   // owner items 86 / 142 (OzStartAhead.cs)
            TakePictures();                                                                              // the pictures' moments on screen (OzStartFrameTrace.Seen.cs)
            dts.Add(dt); ts.Add(t * 1000f); marks.Add(sb.ToString());
            if (t >= Seconds && SeenLongEnough(t)) Finish();                                            // and Seconds after the first picture (OzStartFrameTrace.Seen.cs)
        }

        /// <summary>This level's distance rule has made its first collect (its own list of groups is there; the Watched counter alone still
        /// holds the last level's number when a second level is traced in the same session).</summary>
        static readonly System.Reflection.FieldInfo DrawAheadGroups = typeof(OzL16DrawAhead).GetField("groups", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        static bool DrawAheadCollected()
        {
            var d = OzL16DrawAhead.I;
            if (!d) return false;
            return DrawAheadGroups != null ? DrawAheadGroups.GetValue(d) != null : OzL16DrawAhead.Watched > 0;
        }

        void Mark(StringBuilder sb, ref bool seen, bool now, string text, float t)
        {
            if (seen || !now) return;
            seen = true; sb.Append(text).Append("; "); events.Add((t, text));
        }

        void Flag(StringBuilder sb, ref bool was, bool now, string on, string off, float t)
        {
            if (was == now) return;
            was = now; string text = now ? on : off; sb.Append(text).Append("; "); events.Add((t, text));
        }

        void Finish()
        {
            done = true; Restore();
            try
            {
                System.IO.Directory.CreateDirectory(Folder);
                string stem = "frametrace_" + level + "_" + side + (string.IsNullOrEmpty(Label) ? "" : "_" + Label);
                int n = 1; while (System.IO.File.Exists(System.IO.Path.Combine(Folder, stem + "_" + n + ".csv"))) n++;
                string csv = System.IO.Path.Combine(Folder, stem + "_" + n + ".csv"), txt = System.IO.Path.Combine(Folder, stem + "_" + n + ".txt");
                var sb = new StringBuilder("frame,t_ms,dt_ms,marks\n");
                for (int i = 0; i < dts.Count; i++) sb.Append(i).Append(',').Append(ts[i].ToString("0.0")).Append(',').Append(dts[i].ToString("0.00")).Append(',').Append(marks[i].Replace(',', ';')).Append('\n');
                System.IO.File.WriteAllText(csv, sb.ToString());
                string summary = Summary(csv);
                System.IO.File.WriteAllText(txt, summary);
                LastFile = txt; LastSummary = summary;
                Debug.Log("[OzStartFrameTrace] " + summary);
            }
            catch (System.Exception e) { LastSummary = "FAILED to write: " + e.Message; Debug.LogWarning("[OzStartFrameTrace] " + LastSummary); }
            Destroy(gameObject);
        }

        string Summary(string csv)
        {
            var sb = new StringBuilder();
            int frames = dts.Count; float total = frames > 0 ? ts[frames - 1] : 0f;
            sb.Append($"frame trace {level} {side}{(string.IsNullOrEmpty(Label) ? "" : " " + Label)}: {frames} frames in {total / 1000f:F2} s ({(total > 0f ? frames * 1000f / total : 0f):F1} fps mean), ")
              .Append($"the first frame {(frames > 0 ? dts[0] : 0f):F0} ms, longest {(frames > 0 ? Max(dts) : 0f):F0} ms; frames over 33 ms: {Over(33.4f)}, over 50 ms: {Over(50f)}, over 100 ms: {Over(100f)}; ")
              .Append($"cap during the trace: none (was {capWas}, vsync {vsyncWas}); csv {csv}\n");
            sb.Append("  per second (frames / mean ms / longest ms / fps):");
            for (int s = 0; s < Mathf.CeilToInt(Mathf.Max(Seconds, total / 1000f)); s++)
            {
                int k = 0; float sum = 0f, mx = 0f;
                for (int i = 0; i < frames; i++) { float t = ts[i] / 1000f; if (t >= s && t < s + 1) { k++; sum += dts[i]; if (dts[i] > mx) mx = dts[i]; } }
                sb.Append($" [{s}-{s + 1}] {k} / {(k > 0 ? sum / k : 0f):F1} / {mx:F0} / {(sum > 0f ? k * 1000f / sum : 0f):F0}");
            }
            sb.Append("\n  the longest frames (t s: ms):");
            var idx = new List<int>(); for (int i = 0; i < frames; i++) idx.Add(i);
            idx.Sort((a, b) => dts[b].CompareTo(dts[a]));
            for (int i = 0; i < Mathf.Min(10, idx.Count); i++) sb.Append($" {ts[idx[i]] / 1000f:F2}: {dts[idx[i]]:F0}{(marks[idx[i]].Length > 0 ? " (" + marks[idx[i]].Trim() + ")" : "")};");
            sb.Append("\n  marks:");
            foreach (var e in events) sb.Append($" {e.t:F2} s {e.mark};");
            if (events.Count == 0) sb.Append(" none");
            sb.Append("\n  ").Append(SeenLine(csv));                                                    // the frames and the pictures the player sees (OzStartFrameTrace.Seen.cs)
            return sb.ToString();
        }

        int Over(float ms) { int k = 0; foreach (var d in dts) if (d > ms) k++; return k; }
        static float Max(List<float> l) { float m = 0f; foreach (var d in l) if (d > m) m = d; return m; }
    }
}
