// OpenZ (GPL-3.0) - version 1.3, level 7: the counted Play check of the three small fixes. Every line starts [SMALL]; the last line reads "[SMALL] PASS n/n" when all passed.
//   OpenZ/L07 Lava/Checks/Small fixes (Play, level 7)   a fresh match from the main menu, about 60 s, camera moved by the check. One hut and one crystal are destroyed
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL07SmallCheck
    {
        sealed class Watch
        {
            public int stage, pass, n, cone = -1, tries, rim0; public float t0, waitTo, bombUntil;
            public L16Hut hut; public OzL07Crystal cry; public ZObject hutSim, crySim; public readonly List<string> o = new List<string>();
        }
        static Watch w;
        static void Line(bool ok, string s) { w.n++; if (ok) w.pass++; w.o.Add((ok ? "[SMALL] ok   " : "[SMALL] FAIL ") + s); }
        static int Played(string key) => OzSoundLog.Since(w.t0).Count(e => e.key == key && (e.outcome == null || !e.outcome.StartsWith("DROP")));
        static int Dropped(string key) => OzSoundLog.Since(w.t0).Count(e => e.key == key && e.outcome != null && e.outcome.StartsWith("DROP"));
        static void Cam(Vector3 at, float h) { var rts = Object.FindFirstObjectByType<RtsCamera>(); if (rts) { rts.focus = new Vector3(at.x, 0f, at.z); rts.SetHeightImmediate(h); } }
        static ZObject SimOf(ZGame g, GameObject go) { foreach (var kv in g.StaticViews) if (kv.Value == go) return g.World.Get(kv.Key); return null; }

        [MenuItem("OpenZ/L07 Lava/Checks/Small fixes (Play, level 7)")]
        public static void Run()
        {
            var g = ZGame.I; var v = OzVolcano.I;
            if (!Application.isPlaying || !g || g.World == null || !v || !OzVolcanic.Is(g.level)) { Debug.LogError("[SMALL] FAIL enter Play on level 7 first"); return; }
            w = new Watch { t0 = Time.unscaledTime };
            w.hut = L16Hut.All.FirstOrDefault(h => !h.Broken && !h.Leaning); w.cry = OzL07Crystal.All.FirstOrDefault(c => !c.Broken);
            for (int i = 0; i < v.Count && w.cone < 0; i++) if (v.ModelOf(i) && v.CanBig(i)) w.cone = i;
            if (w.hut) w.hutSim = SimOf(g, w.hut.gameObject);
            if (w.cry) w.crySim = SimOf(g, w.cry.gameObject);
            if (w.hutSim == null || w.crySim == null || w.cone < 0) { Debug.LogError("[SMALL] FAIL no whole hut, crystal or big eruptor bound: start a fresh match"); return; }
            Line(L16Hut.All.Count == 12, $"{L16Hut.All.Count} huts (12)");
            Line(OzL07Crystal.All.Count == 29, $"{OzL07Crystal.All.Count} crystals (29)");
            Line(OzL07Crystal.All.All(c => c.ruin && !c.ruin.activeSelf && c.loose != null && c.loose.Length == 3 && c.body != null && c.body.Length > 0), "every crystal: body up, stump and 3 shards put away");
            Line(L16Hut.All.All(h => h.chunks == "stone" && h.breakEvent != "dst_debris_wood" && h.leanEvent != "dst_debris_wood"), "every hut is set to stone");
            Cam(w.hut.transform.position, 30f);
            w.hutSim.DamageHealth(w.hutSim.MaxHealth / 2 + 1);
            w.waitTo = Time.unscaledTime + 1.5f;
            EditorApplication.update -= Pump; EditorApplication.update += Pump;
        }

        static void Pump()
        {
            var g = ZGame.I; var v = OzVolcano.I;
            if (!Application.isPlaying || !g || !v) { EditorApplication.update -= Pump; Debug.LogError("[SMALL] FAIL Play ended during the check"); return; }
            float now = Time.unscaledTime;
            if (now < w.waitTo) return;
            switch (w.stage)
            {
                case 0:
                    Line(w.hut.Leaning, "hut leans at half hit points");
                    Line(Played("dst_l07_hut_lean") >= 1, $"lean sound dst_l07_hut_lean: {Played("dst_l07_hut_lean")} played, {Dropped("dst_l07_hut_lean")} dropped");
                    w.hutSim.DamageHealth(w.hutSim.Health); g.World.UpdateObjectHealth(w.hutSim, -1);
                    w.stage = 1; w.waitTo = now + 2.5f; return;
                case 1:
                    Line(w.hut.Broken, "hut " + L16Hut.LastBreak);
                    Line(Played("dst_l07_hut_break") >= 1, $"break sound dst_l07_hut_break: {Played("dst_l07_hut_break")} played, {Dropped("dst_l07_hut_break")} dropped");
                    Line(Played("dst_l07_hut_collapse") >= 1, $"collapse take: {Played("dst_l07_hut_collapse")} played, {Dropped("dst_l07_hut_collapse")} dropped");
                    Line(Played("dst_debris_wood") == 0, $"no wood sound: dst_debris_wood {Played("dst_debris_wood")}");
                    Cam(w.cry.transform.position, 30f);
                    w.waitTo = now + 1f; w.stage = 2; return;
                case 2:
                    w.crySim.DamageHealth(w.crySim.Health); g.World.UpdateObjectHealth(w.crySim, -1);
                    w.stage = 3; w.waitTo = now + 1.5f; return;
                case 3:
                    Line(w.cry.Broken, "crystal " + OzL07Crystal.LastBreak);
                    Line(w.cry.ruin.activeInHierarchy && w.cry.body.All(b => !b.activeSelf), "body gone, stump on the map");
                    Line(w.cry.loose.All(t => t && t.parent == null), "3 shards in the air or on the ground");
                    Line(Played(OzL07Crystal.BreakEvent) >= 1, $"break sound {OzL07Crystal.BreakEvent}: {Played(OzL07Crystal.BreakEvent)} played, {Dropped(OzL07Crystal.BreakEvent)} dropped");
                    Cam(v.CraterOf(w.cone), 70f); w.rim0 = OzVolcano.BombsOnRim;
                    w.stage = 4; return;
                case 4:
                    if (v.Trigger(w.cone, 1)) { w.bombUntil = now + 40f; w.stage = 5; w.waitTo = now + 1f; return; }
                    if (w.tries++ > 40) { w.bombUntil = now; w.stage = 5; return; }
                    w.waitTo = now + 1f; return;
                default:
                    if (OzVolcano.BombsOnRim == w.rim0 && now < w.bombUntil) return;
                    if (now < w.waitTo + 0.5f) return;
                    EditorApplication.update -= Pump;
                    Line(OzVolcano.BombsOnRim > w.rim0, $"rocks landed on the band: {OzVolcano.BombsOnRim - w.rim0}");
                    Line(Played("lava_bomb_land_rock") >= 1, $"rock on rock lava_bomb_land_rock: {Played("lava_bomb_land_rock")} played, {Dropped("lava_bomb_land_rock")} dropped (a drop must say off screen)");
                    w.o.Add($"[SMALL] info lava_bomb_land {Played("lava_bomb_land")} played, {Dropped("lava_bomb_land")} dropped");
                    var path = Path.Combine(OpenZPaths.RepoRoot, "work", "records", "l07_small_sound_log.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, OzSoundLog.Format(OzSoundLog.Since(w.t0)));
                    w.o.Add("[SMALL] info sound log " + path);
                    w.o.Add($"[SMALL] {(w.pass == w.n ? "PASS" : "FAIL")} {w.pass}/{w.n}");
                    Debug.Log(string.Join("\n", w.o));
                    return;
            }
        }
    }
}
