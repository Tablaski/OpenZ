#if !OPENZ_HARNESS
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.2 items 62 and 63 menus (View/Voice/AudioDirector.NoWayLines.cs, AudioDirector.KillLines.cs, OzOrderKillRules.cs):
    /// - Check (edit mode or Play): the no-way pool, "Smokin'!" in the kill quips, "Boom!" in both cheer pools (every clip loads), the
    ///   strength table's answers for the pairs that matter (as Zod's), the four events are not match-state lines (the Unit editor's
    /// </summary>
    public static class OzNoWayKillLinesMenu
    {
        static string Records()
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../..", "work", "records"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        // attacker, target, no-way expected (Zod's zunitrating.cpp: a UCR_WILL_DIE row = no way)
        static readonly (string a, string t, bool noWay)[] Pairs =
        {
            ("grunt", "heavy", true), ("grunt", "gun", true), ("grunt", "howitzer", true), ("grunt", "missile_cannon", true), ("grunt", "gatling", true),
            ("grunt", "jeep", true), ("grunt", "medium", true), ("grunt", "tough", true), ("psycho", "heavy", true), ("sniper", "missile_launcher", true),
            ("jeep", "heavy", true), ("jeep", "gun", true), ("light", "missile_launcher", true), ("medium", "missile_launcher", true), ("missile_launcher", "howitzer", true),
            ("grunt", "grunt", false), ("tough", "heavy", false), ("heavy", "grunt", false), ("psycho", "gun", false), ("jeep", "grunt", false), ("grunt", "fort", false),
        };

        [MenuItem("OpenZ/Audio/no_way_kill_lines Check (pools, table, pacing)")]
        static void Check()
        {
            var sb = new StringBuilder();
            bool ok = true; int rows = 0;
            void Row(bool fine, string s) { if (!fine) ok = false; rows++; sb.AppendLine("- " + (fine ? "ok   " : "FAIL ") + s); }
            var ta = Resources.Load<TextAsset>(AudioDirector.TablePath);
            var table = ta ? JsonUtility.FromJson<SoundTable>(ta.text) : null;
            if (table?.events == null) { Debug.LogError("[no_way_kill] RESULT: FAIL: no sound table at Resources/" + AudioDirector.TablePath); return; }
            var ev = new Dictionary<string, SoundEventDef>();
            foreach (var e in table.events) if (e != null && e.id != null) ev[e.id] = e;
            foreach (var p in new[] { (AudioDirector.NoWayEvent, "alan_main", (string)null, 7), (AudioDirector.KillQuipEvent, "alan_alert", AudioDirector.SmokinLine, 11),
                                      ("vo_boom", "alan_alert", AudioDirector.BoomLine, 12), ("vo_boom_brad", "alan_alert", AudioDirector.BoomLine, 6) })
            {
                if (!ev.TryGetValue(p.Item1, out var e) || e.clips == null) { Row(false, "no " + p.Item1 + " in the sound table"); continue; }
                int load = 0, want = 0, wantLoad = 0; var lines = new HashSet<string>();
                foreach (var c in e.clips)
                {
                    bool l = Resources.Load<AudioClip>(c.res);
                    if (l) load++;
                    if (!string.IsNullOrEmpty(c.line)) lines.Add(c.line);
                    if (p.Item3 != null && c.line == p.Item3) { want++; if (l) wantLoad++; }
                }
                bool fine = e.channel == p.Item2 && load == e.clips.Length && lines.Count >= p.Item4 && (p.Item3 == null || (want >= 1 && wantLoad == want));
                Row(fine, p.Item1 + ": " + e.clips.Length + " clips, " + load + " load, " + lines.Count + " lines, channel " + e.channel
                          + (p.Item3 != null ? "; \"" + p.Item3 + "\" " + want + " take(s), " + wantLoad + " load" : "; lines: " + string.Join(" / ", lines)));
            }
            foreach (var p in Pairs)
            {
                bool got = AudioDirector.NoWayPair(p.a, p.t);
                Row(got == p.noWay, "a " + p.a + " sent alone to attack a " + p.t + ": " + (got ? "NO-WAY answer (it still goes)" : "normal answer") + (got == p.noWay ? "" : " (Zod: " + (p.noWay ? "no way" : "normal") + ")"));
            }
            foreach (var id in new[] { AudioDirector.NoWayEvent, AudioDirector.KillQuipEvent, "vo_boom", "vo_boom_brad" })
                Row(!AudioDirector.MatchStateLines.Contains(id), id + " is not a match-state line: the Unit editor's playground plays it too");
            var gate = new OzNoWayGate();
            Row(gate.Gap >= 20 && gate.Gap <= 30 && gate.Wait <= 3.5 && AudioDirector.BoomLull < AudioDirector.BoomWait,
                "pacing: no-way at most once per " + gate.Gap.ToString("0") + " s, it waits at most " + gate.Wait.ToString("0.0") + " s for quiet voices (+" + gate.After.ToString("0.00")
                + " s breath); cheer: at most one per " + AudioDirector.BoomGap.ToString("0") + " s, " + (AudioDirector.BoomChance * 100f).ToString("0") + " % of good kills on screen, a lull or "
                + AudioDirector.BoomLull.ToString("0.0") + " s after the kill, at most " + AudioDirector.BoomWait.ToString("0") + " s");
            int chatter = AudioDirector.RobotChatter.Level;
            sb.AppendLine("- info ROBOT CHATTER in this editor: " + AudioDirector.RobotChatter.Names[chatter] + (chatter == 0 ? "" : " (LESS and MINIMAL leave the kill quips and the cheers out: that setting's own rule)"));
            if (Application.isPlaying && AudioDirector.NoWayGate != null)
            {
                var g = AudioDirector.NoWayGate;
                sb.AppendLine("- live: no-way asked " + g.Asked + ", said " + g.Said + ", inside the gap " + g.Cooling + ", dropped " + g.Dropped + ", last \"" + g.LastLine + "\"; kill quips asked "
                              + AudioDirector.QuipAsked + ", said " + AudioDirector.QuipSaid + " (\"Smokin'!\" " + AudioDirector.Smokins + "); cheers " + AudioDirector.Booms + " (asked " + AudioDirector.BoomAsked
                              + ", dropped " + AudioDirector.BoomDropped + ", lull " + AudioDirector.CheerLull + ", late " + AudioDirector.CheerLate + ")");
            }
            sb.Insert(0, "# no_way_kill_lines check\n\nRESULT: " + (ok ? "PASS" : "FAIL") + " (" + rows + " rows)\n\n");
            var file = Path.Combine(Records(), "no_way_kill_lines_check.md");
            File.WriteAllText(file, sb.ToString());
            if (ok) Debug.Log("[no_way_kill] RESULT: PASS -> " + file + "\n" + sb);
            else Debug.LogError("[no_way_kill] RESULT: FAIL -> " + file + "\n" + sb);
        }

        [MenuItem("OpenZ/V1.2/Voice: 62 63 No-Way + Kill Lines Proof (any level, Play)")]
        static void Proof()
        {
            if (!Application.isPlaying || !AudioDirector.I) { Debug.LogWarning("[no_way_kill] start a level in Play first (voices on, ROBOT CHATTER FULL)"); return; }
            Debug.Log("[no_way_kill] " + OzNoWayKillProof.Run());
            EditorApplication.CallbackFunction wait = null;
            wait = () =>
            {
                if (!Application.isPlaying) { EditorApplication.update -= wait; return; }
                if (OzNoWayKillProof.Running) return;
                EditorApplication.update -= wait;
                var file = Path.Combine(Records(), "no_way_kill_lines_proof.txt");
                File.WriteAllText(file, OzNoWayKillProof.Last);
                Debug.Log("[no_way_kill] proof " + OzNoWayKillProof.Report + " -> " + file);
            };
            EditorApplication.update += wait;
        }
    }
}
#endif
