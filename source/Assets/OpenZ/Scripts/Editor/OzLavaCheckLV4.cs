// OpenZ (GPL-3.0) - version 1.3, level 7: LV4's materials and counted checks. Every pass line starts [LV4]; the last line reads "[LV4] PASS n/n" when all passed.
//   OpenZ/L07 Lava/LV4 Make volcano materials            no Play, safe to run twice: import settings of the flow maps, M_L07_Flow, M_L07_Bomb
//   OpenZ/L07 Lava/Checks/LV4 clock count (no Play)      the clock alone: 5 seeds x 10 minutes
//   OpenZ/L07 Lava/Checks/LV4 eruptions (Play, level 7)  3 big + 2 spits on real volcanoes, about 80 s, camera moved by the check
//   OpenZ/L07 Lava/Checks/LV4 other level (Play)         L02, L16, L17: no OzLava, no OzVolcano, no volcano channel
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzLavaCheckLV4
    {
        const string Lava = "Assets/OpenZ/Resources/OpenZ/Lava", Noise = "Assets/OpenZ/Resources/OpenZ/FX/Pyro/legacy-noise.png";

        static void Flow(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!ti) { Debug.LogError("[LV4] FAIL flow map missing: " + path); return; }
            ti.textureType = TextureImporterType.Default; ti.sRGBTexture = false; ti.mipmapEnabled = true; ti.isReadable = false; ti.npotScale = TextureImporterNPOTScale.None;
            ti.alphaSource = TextureImporterAlphaSource.None; ti.wrapMode = TextureWrapMode.Clamp; ti.filterMode = FilterMode.Bilinear; ti.anisoLevel = 1; ti.maxTextureSize = 512;
            ti.textureCompression = TextureImporterCompression.Uncompressed;                                    // the time ramp bands under block compression
            ti.SaveAndReimport();
        }

        [MenuItem("OpenZ/L07 Lava/LV4 Make volcano materials")]
        public static string MakeMaterials()
        {
            Flow(Lava + "/T_L07_VolFlow.png"); Flow(Lava + "/T_L07_DecorFlow.png");
            var noise = AssetDatabase.LoadAssetAtPath<Texture>(Noise);
            Material Make(string file, string shader)
            {
                var p = Lava + "/" + file + ".mat"; var sh = Shader.Find(shader);
                if (!sh) { Debug.LogError("[LV4] FAIL shader " + shader + " missing or broken"); return null; }
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
                m.shader = sh; m.SetTexture("_NoiseTex", noise); EditorUtility.SetDirty(m); return m;
            }
            var flow = Make("M_L07_Flow", "OpenZ/VolcanoFlow"); var bomb = Make("M_L07_Bomb", "OpenZ/LavaBomb");
            if (bomb) bomb.enableInstancing = true;
            AssetDatabase.SaveAssets();
            string s = $"[LV4] materials: flow {(flow ? "ok" : "MISSING")}, bomb {(bomb ? "ok" : "MISSING")}, noise {(noise ? "ok" : "MISSING")}";
            Debug.Log(s); return s;
        }

        // ------------------------------------------------------------------ the clock
        [MenuItem("OpenZ/L07 Lava/Checks/LV4 clock count (no Play)")]
        public static string ClockCount()
        {
            var o = new List<string>(); int pass = 0, n = 0;
            void Line(bool ok, string s) { n++; if (ok) pass++; o.Add((ok ? "[LV4] ok   " : "[LV4] FAIL ") + s); }
            const float Dt = 1f / 30f, Length = 600f;
            for (int seed = 0; seed < 5; seed++)
            {
                var plan = new OzVolcanoPlan(1907 * 31 + 7 + seed * 101);
                float t = 0f, spitEnd = 0f, bigEnd = 0f, first = -1f, last = -1f; int spits = 0, bigs = 0, both = 0; var gaps = new List<float>();
                while (t < Length)
                {
                    t += Dt; bool sf = t >= spitEnd, bf = t >= bigEnd;
                    if (!sf && !bf) both++;
                    int d = plan.Step(Dt, sf, bf);
                    if (d == OzVolcanoPlan.None) continue;
                    plan.Started(d); if (first < 0f) first = t;
                    if (d == OzVolcanoPlan.Big) { bigs++; bigEnd = t + 15f; }
                    else { spits++; spitEnd = t + 4f; if (last >= 0f) gaps.Add(t - last); last = t; }
                }
                double mean = 0, sq = 0, lo = 1e9, hi = 0; foreach (var g in gaps) { mean += g; lo = Math.Min(lo, g); hi = Math.Max(hi, g); }
                mean /= Math.Max(gaps.Count, 1); foreach (var g in gaps) sq += (g - mean) * (g - mean);
                double cv = Math.Sqrt(sq / Math.Max(gaps.Count, 1)) / Math.Max(mean, 1e-6);
                Line(first >= OzVolcanoPlan.QuietStart, $"seed {seed}: first start at {first:F1} s (30 or later)");
                Line(mean >= 20 && mean <= 40, $"seed {seed}: spits {spits}, mean gap {mean:F1} s (20-40), gaps {lo:F0}-{hi:F0} s");
                Line(cv >= 0.2, $"seed {seed}: spit gap CV {cv:F2} (0.2 or more: a metronome is 0)");
                Line(bigs >= 2 && bigs <= 5, $"seed {seed}: bigs {bigs} in 10 min (one per 3-6 min, a first one early)");
            }
            o.Add($"[LV4] {(pass == n ? "PASS" : "FAIL")} {pass}/{n}");
            var s2 = string.Join("\n", o); Debug.Log(s2); return s2;
        }

        // ------------------------------------------------------------------ eruptions, in Play
        sealed class Watch { public int stage, nextSite, cy, tries; public float waitTo, worstMs, cover70, cover35; public int clots, puffs, lightsMost, bigs, spits; public List<string> o = new List<string>(); public int pass, n; public List<int> cones = new List<int>(); public int vent = -1; }
        static Watch w;

        static void Line(bool ok, string s) { w.n++; if (ok) w.pass++; w.o.Add((ok ? "[LV4] ok   " : "[LV4] FAIL ") + s); }

        [MenuItem("OpenZ/L07 Lava/Checks/LV4 eruptions (Play, level 7)")]
        public static void PlayCheck()
        {
            var v = OzVolcano.I;
            if (!Application.isPlaying || !v) { Debug.LogError("[LV4] FAIL enter Play on level 7 first"); return; }
            w = new Watch();
            int models = 0, flows = 0, spots = 0;
            for (int i = 0; i < v.Count; i++)
            {
                if (v.ModelOf(i)) { models++; if (v.FlowRenderersOf(i) > 0) flows++; if (v.CanBig(i) && v.LandingsOf(i) > 0) spots++; if (v.CanBig(i)) w.cones.Add(i); }
                else if (w.vent < 0) w.vent = i;
            }
            Line(v.Count >= 6 && v.Count <= 10, $"{v.Count} eruptors (6-10)");
            Line(models == v.Count - 2, $"{models} on their models (all but the 2 vents)");
            Line(flows == models, $"{flows} with flow overlays");
            Line(spots == models, $"{spots} with landing spots");
            OzVolcano.SpitsStarted = OzVolcano.BigsStarted = OzVolcano.BombsLaunched = OzVolcano.BombsOnLava = OzVolcano.BombsOnRim = OzVolcano.BombsBad = 0; OzVolcano.HighestAsh = 0f;
            EditorApplication.update -= Pump; EditorApplication.update += Pump;
        }

        static void Pump()
        {
            var v = OzVolcano.I; var cam = Camera.main; var fx = OpenZFxPlayer.I;
            if (!Application.isPlaying || !v || !fx) { EditorApplication.update -= Pump; Debug.LogError("[LV4] FAIL Play ended during the check"); return; }
            float now = Time.unscaledTime;
            fx.VolcanoLive(cam, out int clots, out int puffs, out float screens);
            w.clots = Mathf.Max(w.clots, clots); w.puffs = Mathf.Max(w.puffs, puffs); w.worstMs = Mathf.Max(w.worstMs, Time.unscaledDeltaTime * 1000f);
            var rc = cam ? cam.GetComponent<RtsCamera>() : null;
            if (rc && rc.TargetHeight < 50f) w.cover35 = Mathf.Max(w.cover35, screens); else w.cover70 = Mathf.Max(w.cover70, screens);
            int lit = 0; foreach (var l in v.GetComponentsInChildren<Light>()) if (l.enabled) lit++;
            w.lightsMost = Mathf.Max(w.lightsMost, lit);
            if (now < w.waitTo || v.Busy) return;
            if (w.stage < 3 && w.cones.Count > 0)                                                               // three bigs: the band volcano, then two cones, the second at the near zoom
            {
                int site = w.cones[w.stage % w.cones.Count == 0 ? 0 : (w.stage * w.cones.Count / 3) % w.cones.Count];
                if (rc) { rc.FlyTo(v.CraterOf(site)); rc.SetHeightImmediate(w.stage == 2 ? 35f : 70f); }
                if (!v.Trigger(site, 1) && w.tries++ < 40) { w.waitTo = now + 1f; return; }                     // a natural eruption's tail holds this site: ask again
                else if (v.Busy) w.bigs++;
                w.stage++; w.tries = 0; w.waitTo = now + 3f; return;
            }
            if (w.stage == 3)
            {
                if (w.cones.Count > 1 && !v.Trigger(w.cones[1], 0) && w.tries++ < 40) { w.waitTo = now + 1f; return; }
                if (v.Busy) w.spits++;
                w.stage++; w.tries = 0; w.waitTo = now + 1.5f; return;
            }
            if (w.stage == 4)
            {
                if (w.vent >= 0 && !v.Trigger(w.vent, 0) && w.tries++ < 40) { w.waitTo = now + 1f; return; }
                if (v.Busy) w.spits++;
                w.stage++; w.tries = 0; w.waitTo = now + 9f; return;
            }
            if (now < w.waitTo + 12f && w.stage == 5) { w.stage = 6; w.waitTo = now + 12f; return; }              // the last rocks land, the tails run
            EditorApplication.update -= Pump;
            Line(w.bigs == 3, $"{w.bigs} big eruptions ran");
            Line(w.spits == 2, $"{w.spits} spits ran, one of them a vent");
            Line(OzVolcano.BombsLaunched >= 18, $"{OzVolcano.BombsLaunched} rocks thrown (18 or more)");
            Line(OzVolcano.BombsBad == 0 && OzVolcano.BombsOnLava + OzVolcano.BombsOnRim == OzVolcano.BombsLaunched, $"landed: lava {OzVolcano.BombsOnLava}, band {OzVolcano.BombsOnRim}, walkable ground {OzVolcano.BombsBad}");
            Line(w.clots <= 640, $"most clots at once {w.clots} (pool 640)");
            Line(w.puffs <= 170, $"most ash puffs at once {w.puffs} (column 40 + ring 12 + 14 rocks' trails 95 + smoke, 170 or less, pool 260)");
            Line(OzVolcano.HighestAsh <= OzVolcano.AshCeiling + 1f, $"ash top {OzVolcano.HighestAsh:F1} m over the crater (ceiling {OzVolcano.AshCeiling:F0})");
            Line(w.cover70 < 1f, $"ash cover at 70 m {w.cover70:F2} screens (under 1)");
            Line(w.cover35 < 2.5f, $"ash cover at 35 m {w.cover35:F2} screens (under 2.5)");
            Line(w.lightsMost <= 2, $"lights on at once {w.lightsMost} (pool 2)");
            w.o.Add($"[LV4] info worst frame in the check {w.worstMs:F1} ms, status: {OzVolcano.Status()}");
            w.o.Add($"[LV4] {(w.pass == w.n ? "PASS" : "FAIL")} {w.pass}/{w.n}");
            Debug.Log(string.Join("\n", w.o));
        }

        [MenuItem("OpenZ/L07 Lava/Checks/LV4 other level (Play)")]
        public static string OtherLevel()
        {
            if (!Application.isPlaying || !ZGame.I) { Debug.LogError("[LV4] FAIL enter Play on L02, L16 or L17 first"); return "[LV4] FAIL not in a level"; }
            var s = $"[LV4] other level: OzLava {(UnityEngine.Object.FindAnyObjectByType<OzLava>() ? "PRESENT FAIL" : "none")}, OzVolcano {(OzVolcano.I ? "PRESENT FAIL" : "none")}, FxCh_lava_fountain {(GameObject.Find("FxCh_lava_fountain") ? "PRESENT FAIL" : "none")}";
            Debug.Log(s); return s;
        }
    }
}
