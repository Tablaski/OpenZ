// OpenZ (GPL-3.0) - version 1.3, item 12 part B: the counted check of level 7's hut animals. Editor only, never in a build.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Files (no Play): every model, picture, bone, take and sound of the hut animals loads, and the lava dragon's model. A missing model is a FAIL.
    /// Play on L07, from the main menu, fresh match: counts the animals, sees that none has a collider or a sim object, then for each kind flies
    /// the camera to it and makes it walk, look round, run and die, and reads the sound log: every event must show a PLAY line, the hut
    /// ambience too (AudioDirector's level 7 branch, it may take 40 s). Every line in the Console starts [HUT]; the report and the log lines
    /// go to Logs/L07_hut_animals_check.txt. The third menu runs on any other level: nothing of this may exist there.
    /// </summary>
    public static class OzL07HutAnimalsCheck
    {
        static readonly string[] Events = { "critter_l07_walk", "critter_l07_run", "critter_l07_worm_crawl", "critter_l07_raptor_look", "critter_l07_pig_look", "critter_l07_worm_look", "critter_l07_raptor_die", "critter_l07_pig_die", "critter_l07_worm_die", "amb_l07_hut_critters" };
        static readonly L07HutAnimals.Kind[] Kinds = { L07HutAnimals.Kind.Raptor, L07HutAnimals.Kind.MiniRaptor, L07HutAnimals.Kind.PigDino, L07HutAnimals.Kind.YellowWorm };
        static readonly string[] Clips = { "Sfx/sd_l07_critter_step_a", "Sfx/sd_l07_critter_step_b", "Sfx/sd_l07_critter_run_a", "Sfx/sd_l07_raptor_look_a", "Sfx/sd_l07_pig_look_a", "Sfx/sd_l07_raptor_die_a", "Sfx/sd_l07_pig_die_a", "Sfx/sd_l07_worm_crawl_a", "Sfx/sd_l07_worm_die_a", "Ambience/sd_amb_l07_hut_critters_a" };
        const int HutsWanted = 12;
        static readonly float[] Beats = { 2.5f, 3f, 2.2f, 2.2f, 2.5f };                                    // fly there, walk, look, run, die
        static readonly List<string> report = new List<string>();
        static int pass, fail, stage, kind, beat, objects0, home0; static float t0, log0;

        static void V(bool ok, string what) { if (ok) pass++; else fail++; string l = (ok ? "PASS  " : "FAIL  ") + what; report.Add(l); Debug.Log("[HUT] " + l); }

        static void Save(string name, string extra)
        {
            var sb = new StringBuilder(); foreach (var l in report) sb.AppendLine(l);
            sb.AppendLine((fail == 0 ? "PASS " : "FAIL ") + pass + "/" + (pass + fail)); sb.Append(extra);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", name));
            File.WriteAllText(path, sb.ToString());
            Debug.Log("[HUT] " + (fail == 0 ? "PASS " : "FAIL ") + pass + "/" + (pass + fail) + "  " + path);
        }

        static int Played(string id, List<OzSoundLog.Entry> log) => log.Count(e => e.key == id && e.outcome != null && e.outcome.StartsWith("PLAY"));

        [MenuItem("OpenZ/L07 Lava/Checks/Hut animal files (no Play)")]
        static void Files()
        {
            pass = fail = 0; report.Clear();
            var bad = L07HutAnimals.FileFaults();
            foreach (var l in bad) V(false, "not in the game: " + l);
            V(bad.Count == 0, "models, pictures, bones and takes of the hut animals: " + bad.Count + " faults");
            var dragon = Resources.Load<GameObject>(L07HutAnimals.Res + "LavaDragon");
            V(dragon, "model loads: Resources/" + L07HutAnimals.Res + "LavaDragon.fbx");
            if (dragon) foreach (var b in new[] { "hips", "head", "tail_2", "wing_1_L", "wing_1_R", "wing_tip_L", "wing_tip_R" }) V(L16AnimalKit.Find(dragon.transform, b), "dragon bone " + b);
            var table = Resources.Load<TextAsset>("OpenZ/Audio/openz_sound_events");
            foreach (var id in Events) V(table && table.text.Contains("\"id\": \"" + id + "\""), "event in the table: " + id);
            foreach (var res in Clips) V(Resources.Load<AudioClip>("OpenZ/Audio/" + res), "clip loads: " + res);
            Save("L07_hut_animal_files.txt", "");
        }

        [MenuItem("OpenZ/L07 Lava/Checks/Hut animals (Play, on L07)")]
        static void Run()
        {
            var g = ZGame.I; var h = L07HutAnimals.I;
            if (!Application.isPlaying || !g || g.World == null || !OzVolcanic.Is(g.level) || !h) { Debug.LogWarning("[HUT] enter Play on L07 first, from the main menu"); return; }
            pass = fail = 0; report.Clear(); stage = 0; kind = 0; beat = 0;
            foreach (var l in L07HutAnimals.Missing) V(false, "not in the game: " + l);
            V(h.Huts == HutsWanted, "huts: " + h.Huts + " (" + HutsWanted + " wanted)");
            V(h.Animals == h.Huts * 2, "animals: " + h.Animals + " (2 per hut)");
            foreach (var k in Kinds) V(h.OfKind(k) == h.Huts / 2, k + ": " + h.OfKind(k) + " (" + h.Huts / 2 + " wanted)");
            V(h.RootNode.GetComponentsInChildren<Collider>(true).Length == 0, "no collider on any animal");
            objects0 = g.World.Objects.Count(o => o.Kind == OpenZ.Sim.ZKind.MapItem); h.ProofAllOut(); home0 = L07HutAnimals.WentHome;
            log0 = Time.unscaledTime; t0 = Time.time;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Fly(L07HutAnimals h, L07HutAnimals.Kind k)
        {
            var rts = Object.FindFirstObjectByType<RtsCamera>();
            if (rts && h.ProofPlace(k, out var at)) rts.FlyTo(at);
        }

        static void Tick()
        {
            var g = ZGame.I; var h = L07HutAnimals.I;
            if (!Application.isPlaying || !g || !h) { EditorApplication.update -= Tick; return; }
            float now = Time.time;
            if (stage == 0)
            {
                if (now - t0 < 1f) return;
                V(L07HutAnimals.Out + L07HutAnimals.WentHome - home0 >= h.Animals - h.InBrokenHuts, "out of their huts at once: " + L07HutAnimals.Out + " of " + h.Animals + ", gone home again in that second " + (L07HutAnimals.WentHome - home0) + ", in broken huts " + h.InBrokenHuts);
                stage = 1; t0 = now; Fly(h, Kinds[0]); return;
            }
            if (kind < Kinds.Length)
            {
                if (now - t0 < Beats[beat]) return;
                t0 = now;
                if (beat < 4) { V(h.ProofDo(Kinds[kind], beat), Kinds[kind] + " does step " + beat + " (0 walk, 1 look, 2 run, 3 die)"); beat++; return; }
                beat = 0; kind++; if (kind < Kinds.Length) Fly(h, Kinds[kind]);
                return;
            }
            bool amb = Played("amb_l07_hut_critters", OzSoundLog.Since(log0)) > 0;
            if (!amb && now - t0 < 45f) return;                                                            // the ambience comes by its own 20-40 s clock, the camera beside a hut
            var log = OzSoundLog.Since(log0);
            foreach (var id in Events) { int n = Played(id, log); V(n > 0, "sound log PLAY " + id + ": " + n); }
            V(g.World.Objects.Count(o => o.Kind == OpenZ.Sim.ZKind.MapItem) == objects0, "map objects unchanged (a live match builds units): " + objects0);
            V(L07HutAnimals.Killed >= Kinds.Length, "killed " + L07HutAnimals.Killed + ", came out " + L07HutAnimals.CameOut + ", looked " + L07HutAnimals.Looked + ", ran " + L07HutAnimals.Ran);
            EditorApplication.update -= Tick;
            Debug.Log("[HUT] " + L07HutAnimals.Status());
            Save("L07_hut_animals_check.txt", L07HutAnimals.Status() + "\n" + OzSoundLog.Format(log.Where(e => Events.Contains(e.key)).ToList()));
        }

        [MenuItem("OpenZ/L07 Lava/Checks/Hut animals no trace (Play, on L02 L16 L17)")]
        static void NoTrace()
        {
            var g = ZGame.I;
            if (!Application.isPlaying || !g) { Debug.LogWarning("[HUT] enter Play on another level first"); return; }
            pass = fail = 0; report.Clear();
            V(!OzVolcanic.Is(g.level), "level " + g.level);
            V(!L07HutAnimals.I && !Object.FindFirstObjectByType<L07HutAnimals>(), "no hut animal object on this level");
            V(!OzSoundLog.Since(0f).Any(e => Events.Contains(e.key)), "no hut animal sound in the log");
            Save("L07_hut_animals_no_trace.txt", "");
        }
    }
}
