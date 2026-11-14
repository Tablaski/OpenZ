#if !OPENZ_HARNESS && !OZ_RELEASE
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// DV161 EvilZod's sounds: the check (any mode: every row in the sound table, its clips load, mono 44.1 kHz, no clip silent, voices on the robot channel) and the Play proof
    /// (View/Voice/OzEvilZodSoundProof.cs).
    /// </summary>
    public static class OzV13EvilZodSoundMenu
    {
        [MenuItem("OpenZ/V1.3/EvilZod: Sound Check (rows, clips)")]
        static void Check()
        {
            var ta = Resources.Load<TextAsset>(AudioDirector.TablePath);
            var table = ta ? JsonUtility.FromJson<SoundTable>(ta.text) : null;
            if (table?.events == null) { Debug.LogError("[OpenZ EvilZod sound] FAIL: no sound table"); return; }
            var rows = table.events.Where(e => e != null && e.id != null && e.id.Contains("evilzod")).ToList();
            var sb = new StringBuilder(); int bad = 0, clips = 0;
            foreach (var e in rows)
            {
                bool voice = e.id.StartsWith("vo_");
                if (voice && (e.channel != "alan_main" || e.is3D || e.bus != "Voice/Robots")) { bad++; sb.AppendLine("FAIL channel: " + e.id); }
                if (!voice && (!e.is3D || !e.bus.StartsWith("SFX"))) { bad++; sb.AppendLine("FAIL bus: " + e.id); }
                foreach (var c in e.clips)
                {
                    clips++;
                    var a = Resources.Load<AudioClip>(c.res);
                    if (!a) { bad++; sb.AppendLine($"FAIL does not load: {e.id} {c.res}"); continue; }
                    if (a.frequency != 44100 || a.length <= 0f) { bad++; sb.AppendLine($"FAIL format: {e.id} {c.res} {a.frequency} Hz"); }
                    if (voice && string.IsNullOrEmpty(c.line)) { bad++; sb.AppendLine($"FAIL no line text: {e.id} {c.res}"); }
                }
            }
            string want = "wpn_evilzod_shot wpn_evilzod_reload wpn_evilzod_shell wpn_evilzod_dash_kick wpn_evilzod_dash_whoosh wpn_evilzod_dash_stop wpn_evilzod_dash_skid wpn_evilzod_dash_land_sand wpn_evilzod_dash_land_hard rob_evilzod_stomp rob_evilzod_armour vo_select_hero_evilzod vo_order_hero_evilzod_move vo_order_hero_evilzod_attack vo_order_hero_evilzod_dash vo_hero_evilzod_kill vo_hero_evilzod_low vo_hero_evilzod_board";
            var have = new HashSet<string>(rows.Select(r => r.id));
            foreach (var id in want.Split(' ')) if (!have.Contains(id)) { bad++; sb.AppendLine("FAIL no row: " + id); }
            string res = $"[OpenZ EvilZod sound] {(bad == 0 ? "PASS" : "FAIL")}: {rows.Count} rows, {clips} clips, {bad} faults";
            if (bad == 0) Debug.Log(res); else Debug.LogError(res + "\n" + sb);
        }

        [MenuItem("OpenZ/V1.3/EvilZod: Sound Proof (Play, a level, DV161)")]
        static void Proof()
        {
            if (!Application.isPlaying) { Debug.LogError("[OpenZ EvilZod sound] start a level in Play first"); return; }
            OzEvilZodSoundProof.Run();
            Debug.Log("[OpenZ EvilZod sound] proof started, about 6 minutes: report in " + OzEvilZodSoundProof.Folder());
        }
    }
}
#endif
