#if !OPENZ_HARNESS
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.2 item 61 menus (the original game's lines OpenZ did not have yet, View/Voice/AudioDirector.ZVoiceLines.cs):
    /// - Check (edit mode or Play): the three pools in the sound table (ann_radar_online, ann_fort_under_attack, vo_vehicle_captured)
    ///   keep their settings and every older clip, and hold the two new clips of their line once each (no stand-in, 0 dB); every new
    ///   clip loads, is mono 44.1 kHz and has the import settings of its pool's clips (Force To Mono, Normalize off, Decompress On Load,
    ///   Vorbis 0.7, preload); the fort alarm stays a match-state line (held in the Unit editor's playground)
    /// - Proof (Play, any level): OzZVoiceLinesProof.Status + PlayNew (the six clips through the game's own path)
    /// </summary>
    public static class OzZVoiceLinesMenu
    {
        static string Records()
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../..", "work", "records"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [MenuItem("OpenZ/Audio/z_voice_lines Check (pools, clips, import)")]
        static void Check()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# z_voice_lines check (" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")").AppendLine();
            bool ok = true;
            var ta = Resources.Load<TextAsset>(AudioDirector.TablePath);
            var table = ta ? JsonUtility.FromJson<SoundTable>(ta.text) : null;
            if (table?.events == null) { Debug.LogError("[z_voice_lines] RESULT: FAIL: no sound table at Resources/" + AudioDirector.TablePath); return; }
            var ev = new Dictionary<string, SoundEventDef>();
            foreach (var e in table.events) if (e != null && e.id != null) ev[e.id] = e;
            var P = OzZVoiceLinesProof.Pools; var before = OzZVoiceLinesProof.ClipsBefore; var nc = OzZVoiceLinesProof.NewClips;
            for (int p = 0; p < P.Length; p++)
            {
                if (!ev.TryGetValue(P[p], out var e) || e.clips == null) { ok = false; sb.AppendLine("- FAIL: no " + P[p] + " in the sound table"); continue; }
                int load = 0, mine = 0;
                foreach (var cr in e.clips)
                {
                    if (Resources.Load<AudioClip>(cr.res)) load++;
                    for (int i = 0; i < nc.GetLength(0); i++) if (nc[i, 0] == P[p] && nc[i, 2] == cr.res) mine++;
                }
                bool fine = e.clips.Length == before[p] + 2 && mine == 2 && load == e.clips.Length;
                if (!fine) ok = false;
                sb.AppendLine("- " + P[p] + ": " + e.clips.Length + " clips (before " + before[p] + ", + " + mine + " new), " + load + " of " + e.clips.Length + " load, "
                              + "channel " + e.channel + ", " + e.bus + ", " + (e.is3D ? "3D" : "2D") + ", cooldown " + e.cooldown.ToString("0.0") + " s" + (fine ? " ok" : " FAIL"));
            }
            for (int i = 0; i < nc.GetLength(0); i++)
            {
                string id = nc[i, 0], line = nc[i, 1], res = nc[i, 2];
                int refs = 0; SoundClipRef cr = null;
                foreach (var e in table.events) if (e?.clips != null) foreach (var c in e.clips) if (c.res == res) { refs++; if (e.id == id) cr = c; }
                var clip = Resources.Load<AudioClip>(res);
                string path = "Assets/OpenZ/Resources/" + res + ".wav";
                var imp = AssetImporter.GetAtPath(path) as AudioImporter;
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
            bool held = AudioDirector.MatchStateLines.Contains("ann_fort_under_attack");
            if (!held) ok = false;
            sb.AppendLine("- ann_fort_under_attack is a match-state line (the Unit editor's playground holds it, the new takes too): " + (held ? "yes" : "NO"));
            if (Application.isPlaying && AudioDirector.I) sb.AppendLine().AppendLine("- live (Play mode):").Append(OzZVoiceLinesProof.Status());
            sb.Insert(0, "RESULT: " + (ok ? "PASS" : "FAIL") + "\n\n");
            var file = Path.Combine(Records(), "z_voice_lines_check.md");
            File.WriteAllText(file, sb.ToString());
            if (ok) Debug.Log("[z_voice_lines] RESULT: PASS -> " + file + "\n" + sb);
            else Debug.LogError("[z_voice_lines] RESULT: FAIL -> " + file + "\n" + sb);
        }

        [MenuItem("OpenZ/V1.2/Voice: 61 Z Voice Lines Proof (any level, Play)")]
        static void Proof()
        {
            if (!Application.isPlaying || !AudioDirector.I) { Debug.LogWarning("[z_voice_lines] start a level in Play first (ANNOUNCER FULL, voices on)"); return; }
            string status = OzZVoiceLinesProof.Status();
            Debug.Log("[z_voice_lines] " + OzZVoiceLinesProof.PlayNew());
            EditorApplication.CallbackFunction wait = null;
            wait = () =>
            {
                if (!Application.isPlaying) { EditorApplication.update -= wait; return; }
                if (OzZVoiceLinesProof.Running) return;
                EditorApplication.update -= wait;
                var file = Path.Combine(Records(), "z_voice_lines_proof.txt");
                File.WriteAllText(file, status + "\n" + OzZVoiceLinesProof.Last);
                Debug.Log("[z_voice_lines] proof " + OzZVoiceLinesProof.Report + " -> " + file);
            };
            EditorApplication.update += wait;
        }

        [MenuItem("OpenZ/V1.2/Voice: 61 Z Voice Lines Held Check (Unit editor, Play)")]
        static void Held()
        {
            string r = OzZVoiceLinesProof.HeldCheck();
            var file = Path.Combine(Records(), "z_voice_lines_held.txt");
            File.WriteAllText(file, r);
            Debug.Log(r + " -> " + file);
        }
    }
}
#endif
