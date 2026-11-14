// OpenZ (GPL-3.0) - version 1.2: the checks of General EvilZod's lines on the blue card (kit k75b2). Edit mode: the counted check (the seven
// scene): the Play proof walks the faction screen to the blue battlefield screen and clicks each level's card (View/OzEvilCardLinesProof.cs).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzEvilCardLinesMenu
    {
        const string M = "OpenZ/V1.2/Blue Cutscenes: 75b2 ";
        const string T = "[OzEvilCardLines] ";

        [MenuItem(M + "1 Card Lines Counted Check (no Play)", false, 7540)]
        static void Counted() { CountedCheck(); }

        [MenuItem(M + "2 Card Lines Play Proof (from the MainMenu)", false, 7541)]
        static void PlayProof()
        {
            if (!Application.isPlaying) { Debug.LogWarning(T + "enter Play on the MainMenu scene first"); return; }
            OzEvilCardLinesProof.Run();
        }

        [MenuItem(M + "3 Card Lines Status (Play)", false, 7542)]
        static void Status() { Debug.Log(T + ZodBriefing.EvilLinesStatus()); }

        /// <summary>
        /// approved subtitle).</summary>
        static readonly (string level, string words)[] Owner =
        {
            ("L02", "SAND, SUN, AND SCRAP METAL! THE WAY I LIKE IT!"), ("L02", "THE DESERT, WHERE RED ROBOTS COME TO RUST!"),
            ("L07", "Sir! The floor is lava! Oh, cut the crap and go win it. And bring me a burger!"),
            ("L16", "WELCOME TO THE JUNGLE! PAINT IT BLUE!"), ("L16", "Clear that jungle! I want a parking lot by lunch!"),
            ("L16", "Jungle with crocs? I prefer sharks with lasers!"),
            ("L17", "THIS CITY NEEDS A NEW MAYOR! ME! AND YOU FOUND ONE!"), ("L17", "ONE CITY, TWO ARMIES, ZERO PARKING SPACES! LET'S FIX THAT!"),
        };
        const string Sibling = "Assets/OpenZ/Resources/OpenZ/Zod/Voice/zod_select_L02.wav";
        /// <summary>How many lines each level of the battlefield screen has (the jungle three, the others two); seven in all.</summary>
        static readonly Dictionary<string, int> Want = new Dictionary<string, int> { { "L02", 2 }, { "L07", 1 }, { "L16", 3 }, { "L17", 2 } };   // 1.3 item 30: level 7 one exchange
        const int AllLines = 8;
        /// <summary>1.3 item 30: the levels the table serves, in menu order: the menu's levels, and level 7 in its place (after L02)
        /// before item 12 registers it.</summary>
        static IEnumerable<string> TableLevels() => OzLevelL07.MenuOrder();

        /// <summary></summary>
        public static string CountedCheck()
        {
            var sb = new StringBuilder(); int pass = 0, fail = 0;
            void Check(string what, bool ok) { if (ok) pass++; else fail++; sb.AppendLine((ok ? "PASS " : "FAIL ") + what); }
            var all = OzEvilCardLines.All;

            // C1 the table: two lines for every level of the battlefield screen, three for the jungle
            foreach (var id in TableLevels())
            {
                var lines = OzEvilCardLines.ForLevel(id); int want = Want.TryGetValue(id, out var w) ? w : 2;
                Check($"C1 {id}: {lines.Count} lines ({string.Join(", ", lines.Select(x => x.Id + (x.Pending ? " TEXT ONLY" : "")))}), {want} wanted", lines.Count == want && lines.All(x => x.Id.StartsWith("evil_card_" + id + "_")));
            }
            Check($"C1 {all.Length} lines in all ({AllLines} wanted), ids unique", all.Length == AllLines && all.Select(x => x.Id).Distinct().Count() == AllLines);

            foreach (var (level, words) in Owner)
            {
                var hit = all.FirstOrDefault(x => x.Level == level && string.Equals(x.Text, words, System.StringComparison.OrdinalIgnoreCase));
                Check($"C2 {level} \"{words}\": {(hit != null ? hit.Id + " \"" + hit.Text + "\"" : "NOT IN THE TABLE")}", hit != null);
            }

            // C3 the cues: together exactly the words, in order, from the voice's start to the take's end; the jolt inside the take
            foreach (var l in all)
            {
                if (l.Pending)
                {   // 1.3 item 30: no take yet: the words and the speakers only (BS-01 first, EvilZod cuts in); the times come with the take
                    string said = string.Join(" ", l.Cues.Select(c => c.Text));
                    Check($"C3 {l.Id}: TEXT ONLY, waiting for the owner: {l.Cues.Length} cues \"{said}\" = the words, speakers {string.Join(" > ", l.Cues.Select(c => c.Who))}, never picked ({OzEvilCardLines.Pick(l.Level, 0.5f)?.Id ?? "none"} picked for {l.Level})",
                          said == l.Text && l.Cues.Length == 2 && l.Cues[0].Who == OzBlueScenes.Sidekick && l.Cues[1].Who == OzBlueScenes.EvilZod && l.Exchange && OzEvilCardLines.Pick(l.Level, 0.5f) != l);
                    continue;
                }
                bool order = l.Cues.Length > 0 && Mathf.Approximately(l.Cues[0].Start, 0f) && Mathf.Abs(l.Cues[l.Cues.Length - 1].End - l.Duration) < 0.01f;
                for (int i = 0; i < l.Cues.Length; i++) { order &= l.Cues[i].End > l.Cues[i].Start; if (i > 0) order &= Mathf.Approximately(l.Cues[i].Start, l.Cues[i - 1].End); }
                string joined = string.Join(" ", l.Cues.Select(c => c.Text));
                Check($"C3 {l.Id}: {l.Cues.Length} cue(s) \"{joined}\" = the words, {l.Cues[0].Start:0.00}-{l.Cues[l.Cues.Length - 1].End:0.00} s of {l.Duration:0.000} s, the jolt at {l.KickAt:0.00} s",
                      joined == l.Text && order && l.KickAt > 0f && l.KickAt < l.Duration);
            }

            // C4 the seven clips: found, their length as the table's, mono 44.1 kHz, never clipped; C5 imported as the commander's level line
            var sib = AssetImporter.GetAtPath(Sibling) as AudioImporter;
            int found = 0;
            int pending = 0;
            foreach (var l in all)
            {
                if (l.Pending) { pending++; sb.AppendLine($"WAIT C4 {l.Id}: TEXT ONLY, waiting for the owner (no take yet; not counted)"); continue; }   // 1.3 item 30
                var clip = OzEvilCardLines.Clip(l);
                string path = "Assets/OpenZ/Resources/" + OzEvilCardLines.VoicePath + l.Id + ".wav";
                if (!clip) { Check($"C4 {l.Id}: the clip Resources/{OzEvilCardLines.VoicePath}{l.Id} is MISSING", false); continue; }
                found++;
                float peak = 0f; double sq = 0; int n = 0;
                var data = new float[clip.samples * clip.channels];
                bool read = clip.GetData(data, 0);
                if (read) { foreach (var s in data) { float a = Mathf.Abs(s); if (a > peak) peak = a; sq += s * s; } n = data.Length; }
                float rmsDb = n > 0 ? 10f * Mathf.Log10((float)(sq / n) + 1e-12f) : -99f, peakDb = 20f * Mathf.Log10(peak + 1e-9f);
                Check($"C4 {l.Id}: {clip.length:0.000} s (table {l.Duration:0.000}), {clip.channels} channel(s), {clip.frequency} Hz, peak {peakDb:0.0} dBFS, mean {rmsDb:0.0} dBFS (read {read})",
                      Mathf.Abs(clip.length - l.Duration) < 0.03f && clip.channels == 1 && clip.frequency == 44100 && read && peak < 0.999f && peak > 0.2f);
                var imp = AssetImporter.GetAtPath(path) as AudioImporter;
                if (!imp || !sib) { Check($"C5 {l.Id}: the importer of {path} {(imp ? "found" : "MISSING")}, the sibling {Sibling} {(sib ? "found" : "MISSING")}", false); continue; }
                var a1 = imp.defaultSampleSettings; var a2 = sib.defaultSampleSettings;
                bool same = a1.loadType == a2.loadType && a1.compressionFormat == a2.compressionFormat && Mathf.Approximately(a1.quality, a2.quality) && a1.sampleRateSetting == a2.sampleRateSetting
                            && imp.forceToMono == sib.forceToMono && imp.loadInBackground == sib.loadInBackground;
                Check($"C5 {l.Id}: imported as the commander's level line ({a1.loadType}, {a1.compressionFormat} {a1.quality:0.00}, mono {imp.forceToMono})", same);
            }
            Check($"C4 {found} of {AllLines - pending} clips found ({pending} line(s) TEXT ONLY)", found == AllLines - pending && all.Length == AllLines);

            // C6 the pick: 100 draws per line of a level with the game's own Pick (a new random draw each time; 200 for two lines, 300 for
            //    the jungle's three): always ONE of that level's lines, every line comes, each 60..140 times (100 expected; two lines: 100 +- 35
            //    is five standard deviations, three lines: 100 +- 40 is five), the same line twice in a row 60..140 of the picks - 1 (not a
            //    fixed order either way)
            foreach (var d in OzLevelDef.All)
            {
                var lines = OzEvilCardLines.ForLevel(d.Id); if (lines.Count < 2) continue;
                int picks = 100 * lines.Count, wrong = 0, repeats = 0; var got = new int[lines.Count]; string prev = null;
                for (int i = 0; i < picks; i++)
                {
                    var p = OzEvilCardLines.Pick(d.Id);
                    int k = p != null ? lines.IndexOf(p) : -1;
                    if (k >= 0) got[k]++; else wrong++;
                    if (p != null && p.Id == prev) repeats++;
                    prev = p?.Id;
                }
                Check($"C6 {d.Id}: {picks} picks: {string.Join(", ", lines.Select((x, i) => x.Id + " " + got[i] + " x"))}, anything else {wrong} x; the same line twice in a row {repeats} of {picks - 1}",
                      wrong == 0 && got.All(x => x >= 60 && x <= 140) && got.Sum() == picks && repeats >= 60 && repeats <= 140);
            }
            //    never two at once: 200 visits of the screen, every level clicked twice a visit, the rule of ZodBriefing.EvilLinesTick (a level
            //    speaks once a visit, one Pick each time it speaks): lines a level a visit
            int visits = 200, over = 0, under = 0;
            for (int v = 0; v < visits; v++)
            {
                var spoken = new HashSet<string>(); var said = new Dictionary<string, int>();
                foreach (var d in OzLevelDef.All)
                    for (int click = 0; click < 2; click++)
                    {
                        if (spoken.Contains(d.Id)) continue;
                        var p = OzEvilCardLines.Pick(d.Id); spoken.Add(d.Id);
                        if (p != null) said[d.Id] = (said.TryGetValue(d.Id, out var k) ? k : 0) + 1;
                    }
                foreach (var d in OzLevelDef.All) { int k = said.TryGetValue(d.Id, out var x) ? x : 0; if (k > 1) over++; if (k < 1 && OzEvilCardLines.ForLevel(d.Id).Any(l => !l.Pending)) under++; }   // 1.3 item 30: a level whose line waits for its take stays silent
            }
            Check($"C6 {visits} visits x {OzLevelDef.All.Length} levels, each card clicked twice: a level said more than one line {over} x, none {under} x (one line a level a visit, never two)", over == 0 && under == 0);

            // C7 the hooks in place (the apply's edits): the card calls the rule, Speaking covers his line
            string cardSrc = Read("Assets/OpenZ/Scripts/View/Zod/ZodBriefing.EvilCard.cs"), zbSrc = Read("Assets/OpenZ/Scripts/View/Zod/ZodBriefing.cs");
            Check("C7 the card calls his rule every frame it is up (EvilLinesTick in ZodBriefing.EvilCard.cs)", cardSrc.Contains("EvilLinesTick(now);"));
            Check("C7 ZodBriefing.Speaking covers his line (the director's voices wait, the music dips; ZodBriefing.cs)", zbSrc.Contains("|| EvilLineSpeaking();"));

            string verdict = (fail == 0 ? $"PASS {pass}/{pass + fail}" : $"FAIL {fail} of {pass + fail}") + (pending > 0 ? $" ({pending} line(s) TEXT ONLY, waiting for the owner)" : "");
            sb.Insert(0, "k75b2 EvilZod card lines: counted check " + verdict + "\n");
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/blue_evil_card_lines_k75b2"));
                Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "counted_check.txt"), sb.ToString());
                sb.AppendLine("report: " + Path.Combine(dir, "counted_check.txt").Replace('\\', '/'));
            }
            catch (System.Exception e) { sb.AppendLine("report not written: " + e.Message); }
            Debug.Log(T + sb);
            return verdict;
        }

        static string Read(string assetPath)
        {
            try { return File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath)); } catch (System.Exception) { return ""; }
        }
    }
}
