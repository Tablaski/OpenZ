#if !OPENZ_HARNESS
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.2 items 187 / 188 menu (ten extra pyro lines and ten extra medium tank lines, ADDED to the type pools, nothing replaced),
    /// - Check (edit mode or Play): the six pools of the sound table hold their older clips plus the new ones (pyro select 12 + 4 + 5,
    ///   move 16 + 2 + 2, attack 15 + 4 + 6 = 21 / 20 / 25 clips; medium tank select 4 + 3, move 4 + 4, attack 4 + 3); every new
    ///   clip is in its pool once, with its line, 0 dB, no stand-in; every clip of the six pools loads and is mono 44.1 kHz; the new
    ///   clips have the import settings of their pool's clips (Force To Mono, Normalize off, Decompress On Load, Vorbis 0.7,
    ///   preload); the pools stay on Alan's main channel (2D, Voice/Robots). In Play mode it also lists the sound log entries that
    ///   played one of the new clips.
    /// </summary>
    public static class OzExtraUnitLinesCheck
    {
        const string U = "OpenZ/Audio/Voice/Units/";
        static readonly string[] Pools = { "vo_select_x_pyro", "vo_order_x_pyro_move", "vo_order_x_pyro_attack",
                                           "vo_select_x_mtank", "vo_order_x_mtank_move", "vo_order_x_mtank_attack" };
        static readonly int[] Before = { 12, 16, 15, 4, 4, 4 };
        static readonly string[,] NewClips =
        {
            { "vo_select_x_pyro", "BBQ time!", U + "alan_uv_pyro_select_10_1" },
            { "vo_select_x_pyro", "Who ordered well done?", U + "alan_uv_pyro_select_11_1" },
            { "vo_select_x_pyro", "Smells like victory... and charcoal.", U + "alan_uv_pyro_select_12_1" },
            { "vo_select_x_pyro", "Nothing left but ashes.", U + "alan_uv_pyro_select_13_1" },
            { "vo_order_x_pyro_move", "Fire up the grill!", U + "alan_uv_pyro_move_7_1" },
            { "vo_order_x_pyro_move", "Extra crispy, coming up!", U + "alan_uv_pyro_move_8_1" },
            { "vo_order_x_pyro_attack", "Let's roast 'em!", U + "alan_uv_pyro_attack_7_1" },
            { "vo_order_x_pyro_attack", "Let them burn!", U + "alan_uv_pyro_attack_8_1" },
            { "vo_order_x_pyro_attack", "Everything burns!", U + "alan_uv_pyro_attack_9_1" },
            { "vo_order_x_pyro_attack", "Feel the heat!", U + "alan_uv_pyro_attack_10_1" },
            { "vo_select_x_pyro", "I'm on fire today!", U + "alan_uv_pyro_select_31_1" },
            { "vo_select_x_pyro", "Baby, I'm on fire!", U + "alan_uv_pyro_select_32_1" },
            { "vo_select_x_pyro", "I set the world on fire.", U + "alan_uv_pyro_select_33_1" },
            { "vo_select_x_pyro", "One spark is all it takes.", U + "alan_uv_pyro_select_34_1" },
            { "vo_select_x_pyro", "Hot enough for you?", U + "alan_uv_pyro_select_35_1" },
            { "vo_order_x_pyro_move", "Smoke 'em out!", U + "alan_uv_pyro_move_31_1" },
            { "vo_order_x_pyro_move", "I'll leave a scorch mark.", U + "alan_uv_pyro_move_32_1" },
            { "vo_order_x_pyro_attack", "Watch me burn!", U + "alan_uv_pyro_attack_31_1" },
            { "vo_order_x_pyro_attack", "Burn, baby, burn!", U + "alan_uv_pyro_attack_32_1" },
            { "vo_order_x_pyro_attack", "You go down in flames!", U + "alan_uv_pyro_attack_33_1" },
            { "vo_order_x_pyro_attack", "Suffocating.", U + "alan_uv_pyro_attack_34_1" },
            { "vo_order_x_pyro_attack", "Into the inferno with you!", U + "alan_uv_pyro_attack_35_1" },
            { "vo_order_x_pyro_attack", "Nowhere to hide from the flames!", U + "alan_uv_pyro_attack_36_1" },
            { "vo_select_x_mtank", "I'm the answer!", U + "alan_uv_mtank_select_5_1" },
            { "vo_select_x_mtank", "All systems go!", U + "alan_uv_mtank_select_6_1" },
            { "vo_select_x_mtank", "Nobody gets past me.", U + "alan_uv_mtank_select_7_1" },
            { "vo_order_x_mtank_move", "Affirmative!", U + "alan_uv_mtank_move_5_1" },
            { "vo_order_x_mtank_move", "Understood.", U + "alan_uv_mtank_move_6_1" },
            { "vo_order_x_mtank_move", "Consider it done.", U + "alan_uv_mtank_move_7_1" },
            { "vo_order_x_mtank_move", "Rolling out!", U + "alan_uv_mtank_move_8_1" },
            { "vo_order_x_mtank_attack", "Consider them dead.", U + "alan_uv_mtank_attack_5_1" },
            { "vo_order_x_mtank_attack", "Behold my wrath!", U + "alan_uv_mtank_attack_6_1" },
            { "vo_order_x_mtank_attack", "Target locked.", U + "alan_uv_mtank_attack_7_1" },
        };

        static string Records()
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../..", "work", "records"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [MenuItem("OpenZ/Audio/extra_unit_lines Check (pyro + medium tank pools)")]
        static void Check()
        {
            var sb = new StringBuilder();
            bool ok = true;
            var ta = Resources.Load<TextAsset>(AudioDirector.TablePath);
            var table = ta ? JsonUtility.FromJson<SoundTable>(ta.text) : null;
            if (table?.events == null) { Debug.LogError("[extra_unit_lines] RESULT: FAIL: no sound table at Resources/" + AudioDirector.TablePath); return; }
            var ev = new Dictionary<string, SoundEventDef>();
            foreach (var e in table.events) if (e != null && e.id != null) ev[e.id] = e;
            int n = NewClips.GetLength(0), total = 0, totalLoad = 0;
            for (int p = 0; p < Pools.Length; p++)
            {
                if (!ev.TryGetValue(Pools[p], out var e) || e.clips == null) { ok = false; sb.AppendLine("- FAIL: no " + Pools[p] + " in the sound table"); continue; }
                int load = 0, fmt = 0, mine = 0, want = 0;
                for (int i = 0; i < n; i++) if (NewClips[i, 0] == Pools[p]) want++;
                foreach (var cr in e.clips)
                {
                    var clip = Resources.Load<AudioClip>(cr.res);
                    if (clip) { load++; if (clip.channels == 1 && clip.frequency == 44100) fmt++; }
                    for (int i = 0; i < n; i++) if (NewClips[i, 0] == Pools[p] && NewClips[i, 2] == cr.res) mine++;
                }
                total += e.clips.Length; totalLoad += load;
                bool fine = e.clips.Length == Before[p] + want && mine == want && load == e.clips.Length && fmt == load
                            && e.channel == "alan_main" && !e.is3D && e.bus == "Voice/Robots";
                if (!fine) ok = false;
                sb.AppendLine("- " + Pools[p] + ": " + e.clips.Length + " clips (before " + Before[p] + ", + " + mine + " new of " + want + "), " + load + " load, " + fmt
                              + " mono 44.1 kHz, " + e.channel + ", " + e.bus + ", " + (e.is3D ? "3D" : "2D") + (fine ? " ok" : " FAIL"));
            }
            sb.AppendLine("- six pools: " + totalLoad + " of " + total + " clips load").AppendLine();
            for (int i = 0; i < n; i++)
            {
                string id = NewClips[i, 0], line = NewClips[i, 1], res = NewClips[i, 2];
                int refs = 0; SoundClipRef cr = null;
                foreach (var e in table.events) if (e?.clips != null) foreach (var c in e.clips) if (c.res == res) { refs++; if (e.id == id) cr = c; }
                var clip = Resources.Load<AudioClip>(res);
                var imp = AssetImporter.GetAtPath("Assets/OpenZ/Resources/" + res + ".wav") as AudioImporter;
                bool importOk = false; string importText = "no importer";
                if (imp)
                {
                    var s = imp.defaultSampleSettings;
                    var so = new SerializedObject(imp); var norm = so.FindProperty("normalize");
                    bool normalize = norm != null && norm.boolValue;
                    importOk = imp.forceToMono && !normalize && s.loadType == AudioClipLoadType.DecompressOnLoad && s.compressionFormat == AudioCompressionFormat.Vorbis
                               && Mathf.Abs(s.quality - 0.7f) < 0.01f && s.preloadAudioData;
                    importText = "mono " + imp.forceToMono + ", normalize " + normalize + ", " + s.loadType + ", " + s.compressionFormat + " " + s.quality.ToString("0.00") + ", preload " + s.preloadAudioData;
                }
                bool fine = cr != null && refs == 1 && cr.line == line && !cr.standin && Mathf.Abs(cr.gainDb) < 0.001f && clip && clip.channels == 1 && clip.frequency == 44100 && importOk;
                if (!fine) ok = false;
                sb.AppendLine("- " + (fine ? "ok   " : "FAIL ") + id + " \"" + line + "\" " + res + ": " + (cr != null ? "in the pool" : "NOT in the pool") + ", " + refs + " table reference(s), "
                              + (clip ? clip.length.ToString("0.00") + " s, " + clip.channels + " ch, " + clip.frequency + " Hz" : "does not load") + "; import: " + importText);
            }
            if (Application.isPlaying)
            {
                var names = new HashSet<string>();
                for (int i = 0; i < n; i++) names.Add(Path.GetFileName(NewClips[i, 2]));
                var heard = new Dictionary<string, int>();
                foreach (var e in OzSoundLog.Since(0f))
                    if (!string.IsNullOrEmpty(e.clip) && names.Contains(e.clip)) heard[e.clip] = (heard.TryGetValue(e.clip, out var c) ? c : 0) + 1;
                sb.AppendLine().AppendLine("- live (Play mode): " + heard.Count + " of " + n + " new clips in the sound log");
                foreach (var kv in heard) sb.AppendLine("  - " + kv.Key + " x" + kv.Value);
            }
            sb.Insert(0, "# extra_unit_lines check\n\nRESULT: " + (ok ? "PASS" : "FAIL") + "\n\n");
            var file = Path.Combine(Records(), "extra_unit_lines_check.md");
            File.WriteAllText(file, sb.ToString());
            if (ok) Debug.Log("[extra_unit_lines] RESULT: PASS -> " + file + "\n" + sb);
            else Debug.LogError("[extra_unit_lines] RESULT: FAIL -> " + file + "\n" + sb);
        }
    }
}
#endif
