#if !OPENZ_HARNESS
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Eleven HELP cries ADDED to vo_under_attack_repeat (the radio's cry for help of a
    /// group under heavy fire off screen; both "For crying out loud, do something!" and "For Christ's sake, do something!", owner
    /// heavy tank, MLRS), nothing removed or replaced. No code path changes: the lines play through the existing rules (RadioDiscipline:
    /// 30-60 s radio gap, max 2 radio lines a minute, a shuffle bag of LINES; the Boom cheer: a medium / big enemy vehicle or a gun
    /// destroyed on camera, 70 %, never two within 32 s).
    /// Menus:
    ///  1 Check (edit mode or Play): the three pools hold their older clips plus the new ones; every new clip once, with its line, 0 dB,
    ///    no stand-in, loads, mono 44.1 kHz, the import of its pool's clips (Force To Mono, Normalize off, Decompress On Load, Vorbis
    ///    0.7, preload); its level (gated RMS) within 2 dB of the pool's older clips; the radio bag over 60 simulated minutes of
    ///    non-stop heavy fire (5 seeds): every help line said, none twice in a row, none again before every line had its turn.
    ///  2 Set the new clips' import (Normalize off, then the project importer does the rest) - run once after the wavs are copied in.
    ///  3 Count Start (Play) / 4 Count Write (Play): counts every VO PLAY of the three pools in the sound log for a long CPU game
    ///    (polled every second, so the 2000-entry ring never loses one) -> how often each new line played, the shortest gap between two
    ///    plays of the same line, its volume against the pool's older clips.
    /// </summary>
    public static class OzV13HelpWipeoutCheck
    {
        const string M = "OpenZ/V1.3/Voices: 18 Help + Wipeout ";
        const string T = "[v13 item18] ";
        const string V = "OpenZ/Audio/Voice/";
        static readonly string[] Pools = { "vo_under_attack_repeat", "vo_boom", "vo_boom_brad" };
        static readonly int[] Before = { 48, 13, 6 };
        /// <summary></summary>
        static readonly string[,] NewClips =
        {
            { "vo_under_attack_repeat", "For crying out loud, do something!", V + "alan_cry_v13_crying_out_loud" },
            { "vo_under_attack_repeat", "For Christ's sake, do something!", V + "alan_cry_v13_christs_sake" },
            { "vo_under_attack_repeat", "Don't just sit there! Help us!", V + "alan_cry_v13_dont_just_sit_there" },
            { "vo_under_attack_repeat", "Is this thing on? We need help down here!", V + "alan_cry_v13_is_this_thing_on" },
            { "vo_under_attack_repeat", "Boss! They're turning us into spare parts!", V + "alan_cry_v13_spare_parts" },
            { "vo_under_attack_repeat", "We're outgunned! Send everything you've got!", V + "alan_cry_v13_outgunned" },
            { "vo_under_attack_repeat", "Help! My armour's made of tin foil!", V + "alan_cry_v13_tin_foil" },
            { "vo_under_attack_repeat", "Calling anyone! We're in big trouble here!", V + "alan_cry_v13_big_trouble" },
            { "vo_under_attack_repeat", "Boss, wake up! We need help!", V + "alan_cry_v13_wake_up" },
            { "vo_under_attack_repeat", "Quick, before we're all scrap!", V + "alan_cry_v13_before_scrap" },
            { "vo_under_attack_repeat", "Mummy! I mean... Boss! Help!", V + "alan_cry_v13_mummy" },
            { "vo_boom", "Wipeout! Bits everywhere!", V + "alan_boom_v13_bits_everywhere_1" },
            { "vo_boom", "Kaboom! Wipeout!", V + "alan_boom_v13_kaboom_wipeout_1" },
            { "vo_boom", "Wiped off the map!", V + "alan_boom_v13_wiped_off_the_map_1" },
            { "vo_boom", "Wipeout! Ten points!", V + "alan_boom_v13_ten_points_1" },
            { "vo_boom", "That's what I call a wipeout!", V + "alan_boom_v13_what_i_call_a_wipeout_1" },
            { "vo_boom", "Big boom, big wipeout!", V + "alan_boom_v13_big_boom_1" },
            { "vo_boom_brad", "Whoa. Wipeout, dude.", V + "brad_boom_v13_wipeout_dude_1" },
            { "vo_boom_brad", "Gnarly wipeout, man!", V + "brad_boom_v13_gnarly_1" },
            { "vo_boom_brad", "Wipeout. Smooth as butter.", V + "brad_boom_v13_smooth_as_butter_1" },
            { "vo_boom_brad", "Wipeout!", V + "brad_boom_v13_wipeout_1" },
        };
        /// <summary>A new clip's gated RMS may sit this far (dB) from the median of its pool's older clips.</summary>
        const float LevelTolDb = 2f;
        const float SimMinutes = 60f;

        /// <summary>The new rows (pool, line, file), all 21 of them.</summary>
        static List<(string pool, string line, string res)> Wired()
        {
            var o = new List<(string, string, string)>();
            for (int i = 0; i < NewClips.GetLength(0); i++) o.Add((NewClips[i, 0], NewClips[i, 1], NewClips[i, 2]));
            return o;
        }

        static Dictionary<string, SoundEventDef> Table()
        {
            var ta = Resources.Load<TextAsset>(AudioDirector.TablePath);
            var table = ta ? JsonUtility.FromJson<SoundTable>(ta.text) : null;
            var ev = new Dictionary<string, SoundEventDef>();
            if (table?.events != null) foreach (var e in table.events) if (e != null && e.id != null) ev[e.id] = e;
            return ev;
        }

        // ================================================================================================ 1 the counted check
        [MenuItem(M + "1 Check (clips, import, level, radio bag)", false, 8180)]
        static void CheckMenu() { Check(); }

        public static string Check()
        {
            var sb = new StringBuilder(); int pass = 0, fail = 0;
            void Row(string what, bool ok) { if (ok) pass++; else fail++; sb.AppendLine((ok ? "- PASS " : "- FAIL ") + what); }
            var ev = Table();
            if (ev.Count == 0) { Debug.LogError(T + "FAIL: no sound table at Resources/" + AudioDirector.TablePath); return "FAIL"; }
            var wired = Wired();

            // C1 the pools: older clips + the new ones, all load, Alan's alert channel, 2D, Voice/Robots
            for (int p = 0; p < Pools.Length; p++)
            {
                if (!ev.TryGetValue(Pools[p], out var e) || e.clips == null) { Row("C1 no " + Pools[p] + " in the sound table", false); continue; }
                int want = wired.Count(w => w.pool == Pools[p]), mine = e.clips.Count(c => wired.Any(w => w.pool == Pools[p] && w.res == c.res));
                int load = e.clips.Count(c => Resources.Load<AudioClip>(c.res));
                Row($"C1 {Pools[p]}: {e.clips.Length} clips (before {Before[p]}, + {mine} new of {want}), {load} load, {e.channel}, {e.bus}, {(e.is3D ? "3D" : "2D")}",
                    e.clips.Length == Before[p] + want && mine == want && load == e.clips.Length && e.channel == "alan_alert" && !e.is3D && e.bus == "Voice/Robots");
            }

            // C2 every new clip: once in its pool with its line, 0 dB, no stand-in, loads, mono 44.1 kHz, the pool's import, its level
            foreach (var w in wired)
            {
                int refs = 0; SoundClipRef cr = null;
                foreach (var e in ev.Values) if (e.clips != null) foreach (var c in e.clips) if (c.res == w.res) { refs++; if (e.id == w.pool) cr = c; }
                var clip = Resources.Load<AudioClip>(w.res);
                bool importOk = ImportOk(w.res, out var importText);
                float lvl = clip ? GatedRmsDb(clip) : float.NaN, poolLvl = PoolLevel(ev, w.pool, wired);
                float peak = clip ? PeakDb(clip) : float.NaN;
                bool levelOk = !float.IsNaN(lvl) && !float.IsNaN(poolLvl) && Mathf.Abs(lvl - poolLvl) <= LevelTolDb && peak <= -0.5f;
                Row($"C2 {w.pool} \"{w.line}\" {Path.GetFileName(w.res)}: {(cr != null ? "in the pool" : "NOT in the pool")}, {refs} table reference(s), line {(cr != null && cr.line == w.line ? "ok" : "WRONG")}, " +
                    $"gain {(cr != null ? cr.gainDb.ToString("0.0") : "-")} dB, " + (clip ? $"{clip.length:0.00} s, {clip.channels} ch, {clip.frequency} Hz" : "does not load") +
                    $"; import: {importText}; level {lvl:0.0} dB vs pool {poolLvl:0.0} dB (tol {LevelTolDb:0.0}), peak {peak:0.0} dBFS",
                    cr != null && refs == 1 && cr.line == w.line && !cr.standin && Mathf.Abs(cr.gainDb) < 0.001f && clip && clip.channels == 1 && clip.frequency == 44100 && importOk && levelOk);
            }

            // C3 the radio bag (RadioDiscipline, the game's own limiter and line bag) over SimMinutes of non-stop heavy fire
            if (ev.TryGetValue("vo_under_attack_repeat", out var cry) && cry.clips != null)
            {
                var lines = cry.clips.Where(c => Resources.Load<AudioClip>(c.res)).Select(c => c.line).Where(l => !string.IsNullOrEmpty(l)).ToList();
                var help = wired.Where(w => w.pool == "vo_under_attack_repeat").Select(w => w.line).ToList();
                for (int seed = 1; seed <= 5; seed++)
                {
                    var r = RadioSim(lines, help, seed, out int cries, out int minNew, out int maxNew, out int sameTwice, out int distinct);
                    Row($"C3 radio seed {seed}: {cries} cries in {SimMinutes:0} min over {distinct} lines; each new help line said {minNew}-{maxNew} x; same line twice in a row {sameTwice}; limiter {(r ? "ok" : "BROKEN")}",
                        r && minNew >= 1 && sameTwice == 0 && maxNew <= minNew + 1);
                }
            }
            else Row("C3 no vo_under_attack_repeat to simulate", false);

            if (Application.isPlaying) sb.AppendLine().AppendLine(Recorder.Report(out _));
            string verdict = fail == 0 ? $"PASS {pass}/{pass + fail}" : $"FAIL {fail} of {pass + fail}";
            sb.Insert(0, "# 1.3 item 18 help + wipeout lines: counted check " + verdict + " (" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")\n\n");
            Write("v13_item18_help_wipeout_check.md", sb);
            if (fail == 0) Debug.Log(T + verdict + "\n" + sb); else Debug.LogError(T + verdict + "\n" + sb);
            return verdict;
        }

        /// <summary>The game's radio rules on a table of lines: a call wanted every 0.5 s (half the episodes get a follow-up), as
        /// RadioDiscipline.Simulate, here counting how often each new help line went out.</summary>
        static bool RadioSim(List<string> lines, List<string> help, int seed, out int cries, out int minNew, out int maxNew, out int sameTwice, out int distinct)
        {
            var r = new RadioDiscipline(seed);
            var said = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            var times = new List<float>(); string last = null; bool followUp = false; var rnd = new System.Random(seed + 7);
            cries = 0; sameTwice = 0;
            for (float t = 0f; t < SimMinutes * 60f; t += 0.5f)
            {
                if (!r.CanCall(t)) continue;
                var line = r.Peek("cry", lines, followUp);
                if (line == null) continue;
                r.Played("cry", line); r.Called(t, true); times.Add(t); cries++;
                if (last != null && string.Equals(last, line, System.StringComparison.OrdinalIgnoreCase)) sameTwice++;
                last = line; said[line] = (said.TryGetValue(line, out var n) ? n : 0) + 1;
                followUp = !followUp && rnd.NextDouble() < 0.5;
            }
            distinct = lines.Distinct(System.StringComparer.OrdinalIgnoreCase).Count();
            minNew = help.Count == 0 ? 0 : help.Min(h => said.TryGetValue(h, out var k) ? k : 0);
            maxNew = help.Count == 0 ? 0 : help.Max(h => said.TryGetValue(h, out var k) ? k : 0);
            bool ok = true;
            for (int i = 0; i < times.Count; i++)
            {
                int inMinute = 0; for (int j = i; j < times.Count && times[j] - times[i] < 60f; j++) inMinute++;
                if (inMinute > r.PerMinute) ok = false;
                if (i > 0 && times[i] - times[i - 1] < Mathf.Min(r.GapMin, r.GapMax) - 1e-3f) ok = false;
            }
            return ok;
        }

        // ================================================================================================ levels and import
        static bool ImportOk(string res, out string text)
        {
            var imp = AssetImporter.GetAtPath("Assets/OpenZ/Resources/" + res + ".wav") as AudioImporter;
            text = "no importer";
            if (!imp) return false;
            var s = imp.defaultSampleSettings;
            var so = new SerializedObject(imp); var norm = so.FindProperty("normalize");
            bool normalize = norm != null && norm.boolValue;
            text = "mono " + imp.forceToMono + ", normalize " + normalize + ", " + s.loadType + ", " + s.compressionFormat + " " + s.quality.ToString("0.00") + ", preload " + s.preloadAudioData;
            return imp.forceToMono && !normalize && s.loadType == AudioClipLoadType.DecompressOnLoad && s.compressionFormat == AudioCompressionFormat.Vorbis
                   && Mathf.Abs(s.quality - 0.7f) < 0.01f && s.preloadAudioData;
        }

        /// <summary>Speech level: the RMS of the 50 ms blocks louder than -50 dBFS (silence and breaths left out), in dBFS.</summary>
        static float GatedRmsDb(AudioClip c)
        {
            var d = Samples(c); if (d == null) return float.NaN;
            int block = Mathf.Max(1, c.frequency / 20); double sum = 0; long n = 0;
            for (int i = 0; i + block <= d.Length; i += block)
            {
                double s = 0; for (int k = 0; k < block; k++) s += d[i + k] * d[i + k];
                if (10.0 * System.Math.Log10(s / block + 1e-12) > -50.0) { sum += s; n += block; }
            }
            return n == 0 ? float.NaN : (float)(10.0 * System.Math.Log10(sum / n + 1e-12));
        }

        static float PeakDb(AudioClip c)
        {
            var d = Samples(c); if (d == null) return float.NaN;
            float p = 0f; foreach (var x in d) p = Mathf.Max(p, Mathf.Abs(x));
            return 20f * Mathf.Log10(p + 1e-9f);
        }

        static float[] Samples(AudioClip c)
        {
            if (!c) return null;
            if (c.loadState != AudioDataLoadState.Loaded) c.LoadAudioData();
            var d = new float[c.samples * c.channels];
            return c.GetData(d, 0) ? d : null;
        }

        /// <summary>The median speech level of the pool's older clips (the new ones left out).</summary>
        static float PoolLevel(Dictionary<string, SoundEventDef> ev, string pool, List<(string pool, string line, string res)> wired)
        {
            if (!ev.TryGetValue(pool, out var e) || e.clips == null) return float.NaN;
            var l = new List<float>();
            foreach (var c in e.clips)
            {
                if (wired.Any(w => w.res == c.res)) continue;
                var clip = Resources.Load<AudioClip>(c.res); if (!clip) continue;
                float v = GatedRmsDb(clip); if (!float.IsNaN(v)) l.Add(v);
            }
            if (l.Count == 0) return float.NaN;
            l.Sort(); return l[l.Count / 2];
        }

        // ================================================================================================ 2 import of the new clips
        [MenuItem(M + "2 Set The New Clips' Import (Normalize off)", false, 8181)]
        static void SetImport()
        {
            var wired = Wired();
            int done = 0, missing = 0;
            var all = wired.Select(w => w.res).Distinct();
            foreach (var res in all)
            {
                var imp = AssetImporter.GetAtPath("Assets/OpenZ/Resources/" + res + ".wav") as AudioImporter;
                if (!imp) { missing++; continue; }
                var so = new SerializedObject(imp); var norm = so.FindProperty("normalize");
                if (norm != null && norm.boolValue) { norm.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
                imp.SaveAndReimport(); done++;                                                         // OpenZAudioImport sets the Voice folder's mono / Vorbis 0.7 / Decompress On Load
            }
            Debug.Log(T + $"import set on {done} new clips (Normalize off), {missing} not in the project yet");
        }

        // ================================================================================================ 3 / 4 the long-game count
        [MenuItem(M + "3 Count Start (Play, a long CPU game)", false, 8182)]
        static void CountStart() { if (!Application.isPlaying) { Debug.LogWarning(T + "enter Play first"); return; } Recorder.Start(); Debug.Log(T + "counting the three pools' lines from now"); }

        [MenuItem(M + "4 Count Write (Play)", false, 8183)]
        static void CountWrite()
        {
            var sb = new StringBuilder(Recorder.Report(out bool ok));
            sb.Insert(0, "# 1.3 item 18 help + wipeout lines: long-game count " + (ok ? "PASS" : "NOT YET / FAIL") + " (" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")\n\n");
            Write("v13_item18_help_wipeout_count.md", sb);
            if (ok) Debug.Log(T + sb); else Debug.LogWarning(T + sb);
        }

        /// <summary>Polls the in-Play sound log every second and keeps every VO PLAY of the three pools (clip, time, volume).</summary>
        static class Recorder
        {
            sealed class Play { public float t, vol; public string clip; }
            static readonly Dictionary<string, List<Play>> plays = new Dictionary<string, List<Play>>();
            static float lastT = -1f, nextPoll, startedAt = -1f; static bool hooked;

            public static void Start()
            {
                plays.Clear(); lastT = Time.unscaledTime; startedAt = lastT; OzSoundLog.On = true;
                if (!hooked) { EditorApplication.update += Poll; hooked = true; }
            }

            static void Poll()
            {
                if (!Application.isPlaying || startedAt < 0f) return;
                if (Time.unscaledTime < nextPoll) return;
                nextPoll = Time.unscaledTime + 1f;
                foreach (var e in OzSoundLog.Since(lastT + 1e-4f))
                {
                    lastT = Mathf.Max(lastT, e.t);
                    if (e.src != "VO" || e.outcome == null || !e.outcome.StartsWith("PLAY") || System.Array.IndexOf(Pools, e.key) < 0 || string.IsNullOrEmpty(e.clip)) continue;
                    if (!plays.TryGetValue(e.key, out var l)) plays[e.key] = l = new List<Play>();
                    l.Add(new Play { t = e.t, vol = e.vol, clip = e.clip });
                }
            }

            public static string Report(out bool ok)
            {
                Poll();
                var sb = new StringBuilder(); ok = startedAt >= 0f;
                if (startedAt < 0f) return "- live count: not started (menu 3 in Play)";
                var ev = Table(); var wired = Wired();
                sb.AppendLine($"- live count over {(Time.unscaledTime - startedAt) / 60f:0.0} min of play");
                foreach (var pool in Pools)
                {
                    var l = plays.TryGetValue(pool, out var x) ? x : new List<Play>();
                    var mine = wired.Where(w => w.pool == pool).ToList();
                    var newNames = new HashSet<string>(mine.Select(w => Path.GetFileName(w.res)));
                    var oldVol = l.Where(p => !newNames.Contains(p.clip)).Select(p => p.vol).OrderBy(v => v).ToList();
                    float oldMed = oldVol.Count > 0 ? oldVol[oldVol.Count / 2] : float.NaN;
                    var loaded = ev.TryGetValue(pool, out var e) && e.clips != null ? e.clips.Where(c => Resources.Load<AudioClip>(c.res)).ToList() : new List<SoundClipRef>();
                    int distinct = Mathf.Max(1, loaded.Select(c => c.line).Distinct().Count());
                    // count starts somewhere inside a round: only two whole rounds since Count Start make sure every line had its turn
                    // (one round by lines could FAIL a line that was simply due next). Fair share: twice the even share, plus one.
                    int round = Mathf.Max(1, pool == "vo_under_attack_repeat" ? distinct : loaded.Count);
                    int fair = Mathf.CeilToInt(2f * l.Count / distinct) + 1;
                    bool enough = l.Count >= 2 * round;
                    sb.AppendLine($"  - {pool}: {l.Count} plays, {distinct} lines, {loaded.Count} clips (the heard check needs two rounds = {2 * round} plays); older clips' volume median {oldMed:0.000}");
                    foreach (var w in mine)
                    {
                        string name = Path.GetFileName(w.res);
                        var my = l.Where(p => p.clip == name).OrderBy(p => p.t).ToList();
                        float gap = float.PositiveInfinity; for (int i = 1; i < my.Count; i++) gap = Mathf.Min(gap, my[i].t - my[i - 1].t);
                        float vol = my.Count > 0 ? my.Average(p => p.vol) : float.NaN;
                        float dDb = float.IsNaN(vol) || float.IsNaN(oldMed) || oldMed <= 0f ? float.NaN : 20f * Mathf.Log10(vol / oldMed);
                        bool heardOk = my.Count >= 1 || !enough, spamOk = my.Count <= fair && (my.Count < 2 || gap >= 30f), volOk = float.IsNaN(dDb) || Mathf.Abs(dDb) <= 1f;
                        if (!(heardOk && spamOk && volOk) || !enough) ok = false;
                        sb.AppendLine($"    - {(heardOk && spamOk && volOk ? "ok  " : "FAIL")} \"{w.line}\" x{my.Count} (fair share <= {fair}), shortest gap {(my.Count < 2 ? "-" : gap.ToString("0") + " s")}, volume vs older clips {(float.IsNaN(dDb) ? "-" : dDb.ToString("+0.0;-0.0") + " dB")}" + (enough ? "" : " (not two rounds yet)"));
                    }
                }
                return sb.ToString();
            }
        }

        static void Write(string file, StringBuilder sb)
        {
            try { File.WriteAllText(Path.Combine(OpenZPaths.Records, file), sb.ToString()); sb.AppendLine("report: work/records/" + file); }
            catch (System.Exception e) { sb.AppendLine("report not written: " + e.Message); }
        }
    }
}
#endif
