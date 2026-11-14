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
    /// 1.2 items 25 / 25a menus (the repair lines, View/Voice/AudioDirector.RepairLines.cs):
    /// - Check (edit mode or Play): every repairable vehicle type has its repair event vo_order_x_&lt;type&gt;_repair in the sound table, on
    ///   Alan's main channel (2D, Voice/Robots: the robot filter and the never-cut rule), with the 15 lines of OzRepairLines.Lines and 15
    ///   clips of the type's own names (alan_uv_&lt;type&gt;_repair_*) that load; the clips' format (mono, 44.1 kHz) and length; and the picker
    ///   (OzRepairLines.Simulate: never the same line twice in a row, no recent line while a fresher one is free, every line comes up),
    /// - Proof (Play, a level with a repair station: 16 or 17): View/Voice/OzRepairLinesProof.cs.
    /// </summary>
    public static class OzRepairLinesMenu
    {
        [MenuItem("OpenZ/Audio/repair_lines Check (pools, clips, picker)")]
        static void Check()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# repair_lines check (" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")").AppendLine();
            bool ok = true;
            var ta = Resources.Load<TextAsset>(AudioDirector.TablePath);
            var table = ta ? JsonUtility.FromJson<SoundTable>(ta.text) : null;
            if (table?.events == null) { Debug.LogError("[repair_lines] RESULT: FAIL: no sound table at Resources/" + AudioDirector.TablePath); return; }
            var ev = new Dictionary<string, SoundEventDef>();
            foreach (var e in table.events) if (e != null && e.id != null) ev[e.id] = e;
            int clips = 0, missing = 0;
            foreach (var t in OzRepairLines.Types)
            {
                string id = OzRepairLines.EventFor(t);
                if (!ev.TryGetValue(id, out var e) || e.clips == null || e.clips.Length == 0) { ok = false; sb.AppendLine($"- FAIL: {t}: no {id} in the sound table"); continue; }
                var names = new List<string>(); var lens = new List<float>(); int bad = 0, wrongName = 0, wrongFmt = 0;
                foreach (var cr in e.clips)
                {
                    clips++;
                    var clip = Resources.Load<AudioClip>(cr.res);
                    if (!clip) { missing++; bad++; ok = false; sb.AppendLine($"- FAIL: {id}: clip does not load: {cr.res}"); continue; }
                    lens.Add(clip.length);
                    if (!clip.name.StartsWith("alan_uv_" + t + "_repair_")) wrongName++;
                    if (clip.channels != 1 || clip.frequency != 44100) wrongFmt++;
                    if (!string.IsNullOrEmpty(cr.line) && !names.Contains(cr.line)) names.Add(cr.line);
                }
                bool same = names.Count == OzRepairLines.Lines.Length && OzRepairLines.Lines.All(names.Contains);
                bool chan = e.channel == "alan_main" && !e.is3D && e.bus == "Voice/Robots";
                bool fine = same && chan && bad == 0 && wrongName == 0 && wrongFmt == 0 && e.clips.Length == OzRepairLines.Lines.Length;
                if (!fine) ok = false;
                sb.AppendLine($"- {t}: {id}: {names.Count} lines{(same ? "" : " (NOT the 15 repair lines)")}, {e.clips.Length - bad} of {e.clips.Length} clips load, "
                              + $"{(lens.Count > 0 ? lens.Min().ToString("0.00") + " .. " + lens.Max().ToString("0.00") + " s" : "-")}, {e.channel} {e.bus} {(e.is3D ? "3D" : "2D")}, cooldown {e.cooldown:0.00}"
                              + (wrongName > 0 ? $", {wrongName} clips not named alan_uv_{t}_repair_*" : "") + (wrongFmt > 0 ? $", {wrongFmt} clips not mono 44.1 kHz" : "")
                              + (fine ? " ok" : " FAIL"));
            }
            sb.AppendLine($"- {OzRepairLines.Types.Length} vehicle types, {clips} clip references, {missing} missing").AppendLine();
            for (int seed = 1; seed <= 3; seed++)
            {
                var r = OzRepairLines.Simulate(OzRepairLines.Lines, seed, 5000, out bool pass);
                ok &= pass;
                sb.AppendLine($"- picker {(pass ? "PASS" : "FAIL")}: {r}");
            }
            foreach (var t in OzRepairLines.Types)
            {   // AudioDirector.RobotChatter.Allows: MINIMAL keeps every vo_order* line, LESS drops only idle chatter (vo_idle_x_*, its list)
                string id = OzRepairLines.EventFor(t);
                if (!id.StartsWith("vo_order")) { ok = false; sb.AppendLine($"- FAIL: {id} is not an order line: ROBOT CHATTER MINIMAL would drop it"); }
            }
            sb.AppendLine("- every repair event is an order line (vo_order*): ROBOT CHATTER keeps it on FULL, LESS and MINIMAL; the voice volume and VOICES switches apply as to every robot line");
            if (Application.isPlaying && AudioDirector.I) sb.AppendLine().AppendLine("- live pools (Play mode):").Append(AudioDirector.RepairLinesLiveReport());
            sb.Insert(0, $"RESULT: {(ok ? "PASS" : "FAIL")}\n\n");
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../..", "work", "records"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "repair_lines_check.md");
            File.WriteAllText(path, sb.ToString());
            if (ok) Debug.Log("[repair_lines] RESULT: PASS -> " + path + "\n" + sb);
            else Debug.LogError("[repair_lines] RESULT: FAIL -> " + path + "\n" + sb);
        }

        [MenuItem("OpenZ/V1.2/Voice: 25 Repair Lines Proof (Level 16 or 17, Play)")]
        static void Proof()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] 1.2 item 25 repair lines proof: start level 16 or 17 in Play first"); return; }
            OzRepairLinesProof.Run();
            Debug.Log("[OpenZ] 1.2 item 25 repair lines proof started; its report: OzRepairLinesProof.Report, the log and pictures in " + OzRepairLinesProof.Folder());
        }
    }
}
#endif
