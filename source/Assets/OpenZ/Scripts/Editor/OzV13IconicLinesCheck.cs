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
    /// 44 new lines ADDED to 41 existing pools (one or two each: select / move / attack / idle per unit type, kill quips, taunts, the
    /// plain radio alert, vehicle enter / exit, win / lose), nothing removed or replaced, no code path changes (the pools' own rules
    /// play them: UnitVoices' type-line share and no-repeat window, the idle running gag, the quip rotation, the radio bag).
    /// Never in item 18's pools (vo_under_attack_repeat, vo_boom, vo_boom_brad), never "click me". Row 28 (vo_order_x_howitzer_attack,
    /// Menus:
    ///  1 Check (edit mode or Play): every pool holds its older clips plus its new ones and keeps its channel, bus and 2D; every new clip
    ///    once, with its line, 0 dB, no stand-in, loads, mono 44.1 kHz, its pool's import (Force To Mono, Normalize off, Decompress On
    ///    Load, Vorbis 0.7, preload), its level (gated RMS) within 2 dB of its pool's older clips, peak under -0.5 dBFS; no new line
    ///    in item 18's pools and none twice in the table.
    ///  2 Set the new clips' import (Normalize off) - once after the wavs are copied in.
    ///  3 Count Start (Play) / 4 Count Write (Play): every VO PLAY of the 41 pools from the in-Play sound log (polled every second) ->
    ///    each new line heard, its share of its pool against a fair share, its shortest repeat gap, its volume against the older clips.
    /// </summary>
    public static class OzV13IconicLinesCheck
    {
        const string M = "OpenZ/V1.3/Voices: 21 Iconic Lines ";
        const string T = "[v13 item21] ";
        const string U = "OpenZ/Audio/Voice/Units/", E = "OpenZ/Audio/Voice/Electro/", V = "OpenZ/Audio/Voice/";
        static readonly string[] Item18Pools = { "vo_under_attack_repeat", "vo_boom", "vo_boom_brad" };
        /// <summary>Pools whose exact clip counts the 1.2 checks pin (OzExtraUnitLinesCheck: pyro and medium tank; OzMediumTankSetup:
        /// medium tank): these lines stay out of them so those checks keep passing.</summary>
        static readonly string[] PinnedPools = { "vo_select_x_pyro", "vo_order_x_pyro_move", "vo_order_x_pyro_attack", "vo_select_x_mtank", "vo_order_x_mtank_move", "vo_order_x_mtank_attack" };
        /// <summary></summary>
        static readonly (string pool, int before, string channel)[] Pools =
        {
            ("vo_select_x_grunt", 20, "alan_main"),
            ("vo_select_x_psycho", 16, "alan_main"),
            ("vo_select_x_tough", 5, "alan_main"),
            ("vo_select_x_sniper", 7, "alan_main"),
            ("vo_select_x_light", 8, "alan_main"),
            ("vo_select_x_jeep", 9, "alan_main"),
            ("vo_select_x_heavy", 6, "alan_main"),
            ("vo_select_x_laser", 10, "alan_main"),
            ("vo_select_x_apc", 8, "alan_main"),
            ("vo_select_x_crane", 8, "alan_main"),
            ("vo_select_x_missile_launcher", 8, "alan_main"),
            ("vo_select_x_medium", 10, "alan_main"),
            ("vo_select_x_electrobot", 9, "alan_main"),
            ("vo_order_x_laser_move", 11, "alan_main"),
            ("vo_order_x_grunt_move", 28, "alan_main"),
            ("vo_order_x_jeep_move", 14, "alan_main"),
            ("vo_order_x_apc_move", 12, "alan_main"),
            ("vo_order_x_heavy_move", 10, "alan_main"),
            ("vo_order_x_medium_move", 15, "alan_main"),
            ("vo_order_x_crane_move", 13, "alan_main"),
            ("vo_order_x_grunt_attack", 15, "alan_main"),
            ("vo_order_x_tough_attack", 14, "alan_main"),
            ("vo_order_x_light_attack", 12, "alan_main"),
            ("vo_order_x_psycho_attack", 13, "alan_main"),
            ("vo_order_x_missile_launcher_attack", 10, "alan_main"),
            ("vo_order_x_gatling_attack", 13, "alan_main"),
            ("vo_idle_x_grunt", 5, "alan_main"),
            ("vo_idle_x_tough", 5, "alan_main"),
            ("vo_idle_x_sniper", 6, "alan_main"),
            ("vo_idle_x_jeep", 6, "alan_main"),
            ("vo_idle_x_heavy", 5, "alan_main"),
            ("vo_idle_x_crane", 7, "alan_main"),
            ("vo_idle_x_electrobot", 6, "alan_main"),
            ("vo_idle_x_gun", 6, "alan_main"),
            ("vo_kill_quip", 22, "alan_alert"),
            ("vo_taunt", 10, "alan_alert"),
            ("vo_under_attack", 16, "alan_alert"),
            ("vo_vehicle_enter", 12, "alan_main"),
            ("vo_vehicle_exit", 10, "alan_main"),
            ("vo_win_lines", 50, "alan_end"),
            ("vo_lose_lines", 48, "alan_end"),
        };
        /// <summary>
        /// </summary>
        static readonly (string pool, int before, string res)[] Dropped =
        {
            ("vo_order_x_howitzer_attack", 13, U + "alan_uv_howitzer_attack_31_1"),
        };
        static readonly string[,] NewClips =
        {
            { "vo_select_x_grunt", "Intelligence? Not detected.", U + "alan_uv_grunt_select_31_1" },
            { "vo_select_x_grunt", "Battle program loading... still loading.", U + "alan_uv_grunt_select_32_1" },
            { "vo_select_x_psycho", "Self-preservation module: missing.", U + "alan_uv_psycho_select_31_1" },
            { "vo_select_x_tough", "Courage at one hundred percent. Brains at twelve percent.", U + "alan_uv_tough_select_31_1" },
            { "vo_select_x_sniper", "Scanning for a plan... none found.", U + "alan_uv_sniper_select_31_1" },
            { "vo_select_x_light", "Small, fast, and slightly scared!", U + "alan_uv_light_select_31_1" },
            { "vo_select_x_jeep", "Wheels first, questions later!", U + "alan_uv_jeep_select_31_1" },
            { "vo_select_x_heavy", "All that build time, and THIS is the job?", U + "alan_uv_heavy_select_31_1" },
            { "vo_select_x_laser", "Running diagnostics... yep, still awesome.", U + "alan_uv_laser_select_31_1" },
            { "vo_select_x_apc", "Seatbelts on, everybody!", U + "alan_uv_apc_select_31_1" },
            { "vo_select_x_crane", "I fix it, you break it. Deal?", U + "alan_uv_crane_select_31_1" },
            { "vo_select_x_missile_launcher", "Aiming is optional. Rockets are not.", U + "alan_uv_missile_launcher_select_31_1" },
            { "vo_select_x_medium", "Who needs wheels? I've got knees!", U + "alan_uv_medium_select_31_1" },
            { "vo_select_x_electrobot", "Battery full. Patience empty!", E + "alan_uv_electrobot_select_31_1" },
            { "vo_order_x_laser_move", "Path finding? More like path guessing.", U + "alan_uv_laser_move_31_1" },
            { "vo_order_x_grunt_move", "Walking around the obvious rock!", U + "alan_uv_grunt_move_31_1" },
            { "vo_order_x_jeep_move", "Hit the gas! Mind the bumps!", U + "alan_uv_jeep_move_31_1" },
            { "vo_order_x_apc_move", "Mind the gap, lads!", U + "alan_uv_apc_move_31_1" },
            { "vo_order_x_heavy_move", "Moving. Slowly. Majestically.", U + "alan_uv_heavy_move_31_1" },
            { "vo_order_x_medium_move", "Medium speed! The best speed!", U + "alan_uv_medium_move_31_1" },
            { "vo_order_x_crane_move", "Coming! Put the kettle on!", U + "alan_uv_crane_move_31_1" },
            { "vo_order_x_grunt_attack", "For the flag! And the boss!", U + "alan_uv_grunt_attack_31_1" },
            { "vo_order_x_tough_attack", "Ding dong! Rocket for you!", U + "alan_uv_tough_attack_31_1" },
            { "vo_order_x_light_attack", "I'll nibble their ankles!", U + "alan_uv_light_attack_31_1" },
            { "vo_order_x_psycho_attack", "WHEEE! CHAOS!", U + "alan_uv_psycho_attack_31_1" },
            { "vo_order_x_missile_launcher_attack", "Light the fuses! Fingers crossed!", U + "alan_uv_missile_launcher_attack_31_1" },
            { "vo_order_x_gatling_attack", "Spin to win!", U + "alan_uv_gatling_attack_31_1" },
            { "vo_idle_x_grunt", "Somewhere a general just zoomed out. I can feel it.", U + "alan_uv_grunt_idle_31_1" },
            { "vo_idle_x_tough", "My warranty does not cover this.", U + "alan_uv_tough_idle_31_1" },
            { "vo_idle_x_sniper", "I was built for this. Literally.", U + "alan_uv_sniper_idle_31_1" },
            { "vo_idle_x_jeep", "Are we there yet? We're not even moving!", U + "alan_uv_jeep_idle_31_1" },
            { "vo_idle_x_heavy", "Zoom in. I'm photogenic.", U + "alan_uv_heavy_idle_31_1" },
            { "vo_idle_x_crane", "Build timer says five more minutes. It always says that.", U + "alan_uv_crane_idle_31_1" },
            { "vo_idle_x_electrobot", "I'm not a phone charger, boss!", E + "alan_uv_electrobot_idle_31_1" },
            { "vo_idle_x_gun", "Guarding this spot since level one.", U + "alan_uv_gun_idle_31_1" },
            { "vo_kill_quip", "Enemy uninstalled!", V + "alan_v13_enemy_uninstalled" },
            { "vo_kill_quip", "Return to sender!", V + "alan_v13_return_to_sender" },
            { "vo_taunt", "Nice aim! Said nobody!", V + "alan_v13_nice_aim_said_nobody" },
            { "vo_taunt", "Is your commander on a rocket fuel break?", V + "alan_v13_rocket_fuel_break" },
            { "vo_under_attack", "Error 404: cover not found!", V + "alan_v13_error_404_cover" },
            { "vo_vehicle_enter", "Mirrors, seat, rockets. Ready!", V + "alan_v13_mirrors_seat_rockets" },
            { "vo_vehicle_exit", "Thanks for the lift!", V + "alan_v13_thanks_for_the_lift" },
            { "vo_win_lines", "Victory installed. Party mode on. Cool.", V + "brad_v13_victory_installed" },
            { "vo_lose_lines", "Lost? We were just... testing their defences.", V + "alan_v13_testing_their_defences" },
        };
        const float LevelTolDb = 2f;

        static Dictionary<string, SoundEventDef> Table()
        {
            var ta = Resources.Load<TextAsset>(AudioDirector.TablePath);
            var table = ta ? JsonUtility.FromJson<SoundTable>(ta.text) : null;
            var ev = new Dictionary<string, SoundEventDef>();
            if (table?.events != null) foreach (var e in table.events) if (e != null && e.id != null) ev[e.id] = e;
            return ev;
        }

        static IEnumerable<int> Rows => Enumerable.Range(0, NewClips.GetLength(0));
        static HashSet<string> NewRes => new HashSet<string>(Rows.Select(i => NewClips[i, 2]));

        // ================================================================================================ 1 the counted check
        [MenuItem(M + "1 Check (pools, clips, import, level)", false, 8210)]
        static void CheckMenu() { Check(); }

        public static string Check()
        {
            var sb = new StringBuilder(); int pass = 0, fail = 0;
            void Row(string what, bool ok) { if (ok) pass++; else fail++; sb.AppendLine((ok ? "- PASS " : "- FAIL ") + what); }
            var ev = Table();
            if (ev.Count == 0) { Debug.LogError(T + "FAIL: no sound table at Resources/" + AudioDirector.TablePath); return "FAIL"; }
            var mineAll = NewRes;
            int n = NewClips.GetLength(0);
            Row($"C0 {n} new lines over {Pools.Length} pools (30-50, spread over many pools; at most 2 a pool)",
                n >= 30 && n <= 50 && Pools.Length >= 20 && Pools.All(p => Rows.Count(i => NewClips[i, 0] == p.pool) <= 2));

            // C1 every pool: older clips + its new ones, all load, its channel, Voice/Robots, 2D
            foreach (var p in Pools)
            {
                if (!ev.TryGetValue(p.pool, out var e) || e.clips == null) { Row("C1 no " + p.pool + " in the sound table", false); continue; }
                int want = Rows.Count(i => NewClips[i, 0] == p.pool), mine = e.clips.Count(c => Rows.Any(i => NewClips[i, 0] == p.pool && NewClips[i, 2] == c.res));
                int load = e.clips.Count(c => Resources.Load<AudioClip>(c.res));
                Row($"C1 {p.pool}: {e.clips.Length} clips (before {p.before}, + {mine} new of {want}), {load} load, {e.channel}, {e.bus}, {(e.is3D ? "3D" : "2D")}",
                    e.clips.Length == p.before + want && mine == want && load == e.clips.Length && e.channel == p.channel && !e.is3D && e.bus == "Voice/Robots");
            }

            // C2 item 18's pools hold none of these lines
            int strays = 0;
            foreach (var id in Item18Pools) if (ev.TryGetValue(id, out var e) && e.clips != null) strays += e.clips.Count(c => mineAll.Contains(c.res));
            int pinned = 0;
            foreach (var id in PinnedPools) if (ev.TryGetValue(id, out var e) && e.clips != null) pinned += e.clips.Count(c => mineAll.Contains(c.res));
            int dropRefs = 0;
            foreach (var e in ev.Values) if (e.clips != null) dropRefs += e.clips.Count(c => Dropped.Any(d => d.res == c.res));
            bool dropPools = Dropped.All(d => ev.TryGetValue(d.pool, out var e) && e.clips != null && e.clips.Length == d.before);
            Row($"C2 item 18's pools (help cries, wipeout cheers) hold {strays} of these clips, the 1.2-pinned pyro / medium tank pools {pinned}, " +
                $"the dropped row 28 clip is referenced {dropRefs} time(s) and its pool keeps its size: {dropPools} (want 0, 0, 0, True)",
                strays == 0 && pinned == 0 && dropRefs == 0 && dropPools);

            // C3 every new clip
            foreach (int i in Rows)
            {
                string pool = NewClips[i, 0], line = NewClips[i, 1], res = NewClips[i, 2];
                int refs = 0, sameLine = 0; SoundClipRef cr = null;
                foreach (var e in ev.Values) if (e.clips != null) foreach (var c in e.clips)
                {
                    if (c.res == res) { refs++; if (e.id == pool) cr = c; }
                    else if (string.Equals(c.line, line, System.StringComparison.OrdinalIgnoreCase)) sameLine++;
                }
                var clip = Resources.Load<AudioClip>(res);
                bool importOk = ImportOk(res, out var importText);
                float lvl = clip ? GatedRmsDb(clip) : float.NaN, poolLvl = PoolLevel(ev, pool, mineAll), peak = clip ? PeakDb(clip) : float.NaN;
                bool levelOk = !float.IsNaN(lvl) && !float.IsNaN(poolLvl) && Mathf.Abs(lvl - poolLvl) <= LevelTolDb && peak <= -0.5f;
                Row($"C3 {pool} \"{line}\" {Path.GetFileName(res)}: {(cr != null ? "in the pool" : "NOT in the pool")}, {refs} reference(s), same words elsewhere {sameLine}, " +
                    $"gain {(cr != null ? cr.gainDb.ToString("0.0") : "-")} dB, " + (clip ? $"{clip.length:0.00} s, {clip.channels} ch, {clip.frequency} Hz" : "does not load") +
                    $"; import: {importText}; level {lvl:0.0} dB vs pool {poolLvl:0.0} dB (tol {LevelTolDb:0.0}), peak {peak:0.0} dBFS",
                    cr != null && refs == 1 && sameLine == 0 && cr.line == line && !cr.standin && Mathf.Abs(cr.gainDb) < 0.001f && clip && clip.channels == 1 && clip.frequency == 44100 && importOk && levelOk);
            }

            if (Application.isPlaying) sb.AppendLine().AppendLine(Recorder.Report(out _));
            string verdict = fail == 0 ? $"PASS {pass}/{pass + fail}" : $"FAIL {fail} of {pass + fail}";
            sb.Insert(0, "# 1.3 item 21 iconic lines: counted check " + verdict + " (" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")\n\n");
            Write("v13_item21_iconic_lines_check.md", sb);
            if (fail == 0) Debug.Log(T + verdict + "\n" + sb); else Debug.LogError(T + verdict + "\n" + sb);
            return verdict;
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
        static float PoolLevel(Dictionary<string, SoundEventDef> ev, string pool, HashSet<string> mine)
        {
            if (!ev.TryGetValue(pool, out var e) || e.clips == null) return float.NaN;
            var l = new List<float>();
            foreach (var c in e.clips)
            {
                if (mine.Contains(c.res)) continue;
                var clip = Resources.Load<AudioClip>(c.res); if (!clip) continue;
                float v = GatedRmsDb(clip); if (!float.IsNaN(v)) l.Add(v);
            }
            if (l.Count == 0) return float.NaN;
            l.Sort(); return l[l.Count / 2];
        }

        // ================================================================================================ 2 import of the new clips
        [MenuItem(M + "2 Set The New Clips' Import (Normalize off)", false, 8211)]
        static void SetImport()
        {
            int done = 0, missing = 0;
            foreach (var res in NewRes)
            {
                var imp = AssetImporter.GetAtPath("Assets/OpenZ/Resources/" + res + ".wav") as AudioImporter;
                if (!imp) { missing++; continue; }
                var so = new SerializedObject(imp); var norm = so.FindProperty("normalize");
                if (norm != null && norm.boolValue) { norm.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
                imp.SaveAndReimport(); done++;                                                         // OpenZAudioImport sets the Voice folder's mono / Vorbis 0.7 / Decompress On Load
            }
            Debug.Log(T + $"import set on {done} new clips (Normalize off), {missing} not in the project yet");
        }

        // ================================================================================================ 3 / 4 the in-game count
        [MenuItem(M + "3 Count Start (Play)", false, 8212)]
        static void CountStart() { if (!Application.isPlaying) { Debug.LogWarning(T + "enter Play first"); return; } Recorder.Start(); Debug.Log(T + "counting the 41 pools' lines from now"); }

        [MenuItem(M + "4 Count Write (Play)", false, 8213)]
        static void CountWrite()
        {
            var sb = new StringBuilder(Recorder.Report(out bool ok));
            sb.Insert(0, "# 1.3 item 21 iconic lines: in-game count " + (ok ? "PASS" : "NOT YET / FAIL") + " (" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")\n\n");
            Write("v13_item21_iconic_lines_count.md", sb);
            if (ok) Debug.Log(T + sb); else Debug.LogWarning(T + sb);
        }

        /// <summary>Polls the in-Play sound log every second and keeps every VO PLAY of the 41 pools (clip, time, volume).</summary>
        static class Recorder
        {
            sealed class Play { public float t, vol; public string clip; }
            static readonly Dictionary<string, List<Play>> plays = new Dictionary<string, List<Play>>();
            static readonly HashSet<string> keys = new HashSet<string>(Pools.Select(p => p.pool));
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
                    if (e.src != "VO" || e.outcome == null || !e.outcome.StartsWith("PLAY") || !keys.Contains(e.key) || string.IsNullOrEmpty(e.clip)) continue;
                    if (!plays.TryGetValue(e.key, out var l)) plays[e.key] = l = new List<Play>();
                    l.Add(new Play { t = e.t, vol = e.vol, clip = e.clip });
                }
            }

            /// <summary>Per new line: heard (once its pool had two rounds of plays), not over twice its even share of the pool + 1, never
            /// twice within 10 s, its volume within 1 dB of the pool's older clips. ok = every line passed and every pool had its two rounds.</summary>
            public static string Report(out bool ok)
            {
                Poll();
                var sb = new StringBuilder(); ok = startedAt >= 0f;
                if (startedAt < 0f) return "- live count: not started (menu 3 in Play)";
                var ev = Table(); int heardLines = 0, rounds = 0;
                sb.AppendLine($"- live count over {(Time.unscaledTime - startedAt) / 60f:0.0} min of play");
                foreach (var p in Pools)
                {
                    var l = plays.TryGetValue(p.pool, out var x) ? x : new List<Play>();
                    var mine = Rows.Where(i => NewClips[i, 0] == p.pool).ToList();
                    var newNames = new HashSet<string>(mine.Select(i => Path.GetFileName(NewClips[i, 2])));
                    var oldVol = l.Where(q => !newNames.Contains(q.clip)).Select(q => q.vol).OrderBy(v => v).ToList();
                    float oldMed = oldVol.Count > 0 ? oldVol[oldVol.Count / 2] : float.NaN;
                    var loaded = ev.TryGetValue(p.pool, out var e) && e.clips != null ? e.clips.Where(c => Resources.Load<AudioClip>(c.res)).ToList() : new List<SoundClipRef>();
                    int distinct = Mathf.Max(1, loaded.Select(c => c.line).Distinct().Count());
                    // among the lines not said lately (UnitVoices), and the count starts somewhere inside a round. One round by LINES could
                    // FAIL a line that was simply due next, and the exact even share +1 FAILs ordinary random spread. So: heard is asked
                    // after two rounds of the pool's clips, and the fair share is twice the even share, plus one.
                    int round = Mathf.Max(1, loaded.Count);
                    int fair = Mathf.CeilToInt(2f * l.Count / distinct) + 1;
                    bool enough = l.Count >= 2 * round; if (enough) rounds++;
                    sb.AppendLine($"  - {p.pool}: {l.Count} plays, {distinct} lines, {loaded.Count} clips{(enough ? "" : $" (not two rounds yet: the heard check needs {2 * round} plays)")}");
                    foreach (int i in mine)
                    {
                        string name = Path.GetFileName(NewClips[i, 2]);
                        var my = l.Where(q => q.clip == name).OrderBy(q => q.t).ToList();
                        float gap = float.PositiveInfinity; for (int k = 1; k < my.Count; k++) gap = Mathf.Min(gap, my[k].t - my[k - 1].t);
                        float vol = my.Count > 0 ? my.Average(q => q.vol) : float.NaN;
                        float dDb = float.IsNaN(vol) || float.IsNaN(oldMed) || oldMed <= 0f ? float.NaN : 20f * Mathf.Log10(vol / oldMed);
                        bool heardOk = my.Count >= 1 || !enough, spamOk = my.Count <= fair && (my.Count < 2 || gap >= 10f), volOk = float.IsNaN(dDb) || Mathf.Abs(dDb) <= 1f;
                        if (my.Count >= 1) heardLines++;
                        if (!(heardOk && spamOk && volOk) || !enough) ok = false;
                        sb.AppendLine($"    - {(heardOk && spamOk && volOk ? "ok  " : "FAIL")} \"{NewClips[i, 1]}\" x{my.Count} (fair share <= {fair}), shortest gap {(my.Count < 2 ? "-" : gap.ToString("0") + " s")}, volume vs older clips {(float.IsNaN(dDb) ? "-" : dDb.ToString("+0.0;-0.0") + " dB")}");
                    }
                }
                sb.AppendLine($"- {heardLines} of {NewClips.GetLength(0)} new lines heard; {rounds} of {Pools.Length} pools had a full round");
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
