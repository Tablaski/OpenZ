using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Kit LV2, the counted checks (LV2/README.md). Every line starts [LV2], the last reads PASS n/n.
    ///   heat numbers (no Play)   OzLavaHeatProof.Report: no steps, no rhythm, on the game's own classes
    ///   alive (Play, L07)        CheckSeconds of a running level with the camera over the biggest lake at the default zoom: temperature, globals,
    ///                            surge, bubbles, droplets, embers, wisps, lights, beds, model glow; then a pause and the HoldHeat switch
    ///   no lava here (Play, L02, L16, L17)   no OzLava object, no lava channel, globals at rest
    /// Reads the view only; the one thing it sets is game.paused and OzLavaFx.HoldHeat for a few seconds, both put back.
    /// </summary>
    public static class OzLavaCheckLV2
    {
        public const float CheckSeconds = 100f, PauseAt = 80f, HoldAt = 90f;
        const string Root = "OpenZ/L07 Lava/Checks/";

        [MenuItem(Root + "LV2 heat numbers (no Play)")]
        public static void Numbers()
        {
            foreach (var l in OzLavaHeatProof.Report().Split('\n')) if (l.Trim().Length > 0) Debug.Log(l.Trim());
        }

        [MenuItem(Root + "LV2 no lava here (Play on L02, L16, L17)")]
        public static void NoLava()
        {
            if (!Application.isPlaying || !ZGame.I) { Debug.LogError("[LV2] Play a level first"); return; }
            bool none = !Object.FindFirstObjectByType<OzLava>() && !OzLava.I, ch = true;
            var fx = OpenZFxPlayer.I; if (fx) for (int i = 0; i < 4; i++) ch &= fx.LavaLive(i) == 0;
            bool at = !OzLava.SurfaceAt(Vector3.zero, out _);
            bool flat = Shader.GetGlobalFloat(OzLavaIds.Mood) == 0f && Shader.GetGlobalVector(OzLavaIds.Wave) == Vector4.zero;
            var names = new List<string>(); if (fx) foreach (Transform t in fx.transform) if (t.name.StartsWith("FxCh_lava_")) names.Add(t.name);
            int pass = (none ? 1 : 0) + (ch && names.Count == 0 ? 1 : 0) + (at ? 1 : 0) + (flat ? 1 : 0);
            Debug.Log($"[LV2] {(none ? "PASS" : "FAIL")} {ZGame.I.level}: no OzLava object");
            Debug.Log($"[LV2] {(ch && names.Count == 0 ? "PASS" : "FAIL")} {ZGame.I.level}: no lava channel built, none live");
            Debug.Log($"[LV2] {(at ? "PASS" : "FAIL")} {ZGame.I.level}: OzLava.SurfaceAt false at once");
            Debug.Log($"[LV2] {(flat ? "PASS" : "FAIL")} {ZGame.I.level}: lava globals at rest");
            Debug.Log($"[LV2] no lava here {(pass == 4 ? "PASS" : "FAIL")} {pass}/4");
        }

        // ------------------------------------------------------------------ alive
        static float started, lastFrameAt; static int lastFrame; static bool running, paused0;
        static float[] lo, hi, prev; static float fastest, mapLo, mapHi, glowLo, glowHi, pauseHeat, pauseClock; static int pauseBursts;
        static int pops0, extra0, emberMost, wispMost, dropMost, domesSeen; static int surge0, globalsBad, globalsSeen; static bool pausedTest, holdTest, pauseOk, holdOk;

        [MenuItem(Root + "LV2 alive (Play on L07, about 100 s)")]
        public static void Alive()
        {
            var lava = OzLava.I;
            if (!Application.isPlaying || !lava || !ZGame.I) { Debug.LogError("[LV2] Play level 7 first"); return; }
            if (running) return;
            lo = new float[OzLavaIds.FieldSlots]; hi = new float[OzLavaIds.FieldSlots]; prev = new float[OzLavaIds.FieldSlots];
            for (int i = 0; i < lo.Length; i++) { lo[i] = 9f; hi[i] = -9f; prev[i] = lava.HeatOf(i); }
            fastest = 0f; mapLo = glowLo = 9f; mapHi = glowHi = -9f; emberMost = wispMost = dropMost = domesSeen = 0; globalsBad = globalsSeen = 0;
            pausedTest = holdTest = false; pauseOk = holdOk = false;
            pops0 = OzLava.PopsThisSession; extra0 = OzLava.ExtraPops; surge0 = lava.HeatMachine != null ? lava.HeatMachine.SurgesStarted : 0;
            var cam = Object.FindFirstObjectByType<RtsCamera>();
            if (cam) { cam.FlyTo(lava.FieldCentre(0)); cam.ZoomTo(70f); }
            started = Time.realtimeSinceStartup; lastFrame = Time.frameCount; running = true;
            EditorApplication.update += Step;
            Debug.Log($"[LV2] alive check started, {CheckSeconds:F0} s, keep the game running");
        }

        static void Step()
        {
            var lava = OzLava.I;
            if (!Application.isPlaying || !lava) { EditorApplication.update -= Step; running = false; Debug.LogError("[LV2] alive check stopped: left Play"); return; }
            if (Time.frameCount == lastFrame) return;
            float dt = ZGame.I && ZGame.I.paused ? 0f : Time.deltaTime * (Time.frameCount - lastFrame); lastFrame = Time.frameCount;   // (an editor tick can span more than one game frame)
            float t = Time.realtimeSinceStartup - started;
            for (int i = 0; i < lo.Length; i++)
            {
                float h = lava.HeatOf(i); lo[i] = Mathf.Min(lo[i], h); hi[i] = Mathf.Max(hi[i], h);
                if (dt > 0f && !holdTest) fastest = Mathf.Max(fastest, Mathf.Abs(h - prev[i]) / dt); prev[i] = h;   // HoldHeat puts heat at 1.0 at once on purpose: the limit is measured before it
            }
            mapLo = Mathf.Min(mapLo, lava.HeatNow); mapHi = Mathf.Max(mapHi, lava.HeatNow);
            if (lava.GlowMaterials > 0) { float g = lava.GlowNow; glowLo = Mathf.Min(glowLo, g); glowHi = Mathf.Max(glowHi, g); }
            var fx = OpenZFxPlayer.I;
            if (fx) { dropMost = Mathf.Max(dropMost, fx.LavaLive(0)); wispMost = Mathf.Max(wispMost, fx.LavaLive(1)); emberMost = Mathf.Max(emberMost, fx.LavaLive(3)); }
            if (lava.Domes != null) domesSeen = Mathf.Max(domesSeen, lava.Domes.MostDomes);
            if (Time.frameCount % 30 == 0)
            {
                var f = Shader.GetGlobalVectorArray(OzLavaIds.Field); globalsSeen++;
                if (f == null || f.Length != OzLavaIds.FieldSlots || Mathf.Abs(f[0].x - lava.HeatOf(0)) > 0.02f) globalsBad++;
            }
            if (!pausedTest && t >= PauseAt)
            {
                pausedTest = true; paused0 = ZGame.I.paused; ZGame.I.paused = true; pauseHeat = -1f;
            }
            if (pausedTest && pauseHeat < 0f && t >= PauseAt + 0.5f) { pauseHeat = lava.HeatOf(0); pauseClock = lava.Clock; pauseBursts = lava.Domes != null ? lava.Domes.Bursts : 0; }
            if (pausedTest && !pauseOk && t >= PauseAt + 3f && ZGame.I.paused)
            {
                pauseOk = lava.HeatOf(0) == pauseHeat && lava.Clock == pauseClock && (lava.Domes == null || lava.Domes.Bursts == pauseBursts);
                ZGame.I.paused = paused0;
            }
            if (!holdTest && t >= HoldAt) { holdTest = true; OzLavaFx.HoldHeat = true; }
            if (holdTest && !holdOk && t >= HoldAt + 2.5f)
            {
                holdOk = lava.HeatNow == 1f; for (int i = 0; i < lo.Length; i++) holdOk &= lava.HeatOf(i) == 1f;
                OzLavaFx.HoldHeat = false;
            }
            if (t < CheckSeconds) return;
            EditorApplication.update -= Step; running = false; OzLavaFx.HoldHeat = false;
            Report(lava);
        }

        static void Report(OzLava lava)
        {
            int pass = 0, total = 0; var sb = new StringBuilder();
            void V(bool ok, string s) { total++; if (ok) pass++; sb.AppendLine((ok ? "[LV2] PASS " : "[LV2] FAIL ") + s); }
            float lowest = 9f, top = -9f, wide = 0f;
            for (int i = 0; i < 6; i++) { lowest = Mathf.Min(lowest, lo[i]); top = Mathf.Max(top, hi[i]); wide = Mathf.Max(wide, (hi[i] - lo[i]) / lo[i]); }
            var h = lava.HeatMachine; var air = lava.Air; var d = lava.Domes;
            V(lowest >= 0.72f && top <= 1.35f, $"heat range on the fields {lowest:F3} .. {top:F3} (0.72-1.35)");
            V(fastest <= 0.1505f, $"fastest change on a game frame {fastest:F4} a second (limit 0.15)");
            V(wide >= 0.15f, $"a lake swung {wide * 100:F0} % in {CheckSeconds:F0} s (gate 2's 25 % is measured on the 60 s capture)");
            V(globalsSeen > 0 && globalsBad == 0, $"_OZLavaField matches HeatOf, {globalsSeen} reads, {globalsBad} off");
            V(h != null && h.SurgesStarted - surge0 >= 2, $"surges in {CheckSeconds:F0} s: {(h != null ? h.SurgesStarted - surge0 : 0)}");
            int pops = OzLava.PopsThisSession - pops0, extra = OzLava.ExtraPops - extra0;
            V(pops >= 15, $"stream pops {pops}, extra pops {extra}");
            V(extra > 0, "clusters and border streams add pops");
            V(d != null && d.Bursts > 0 && d.MostDomes <= OzLavaDomes.MaxDomes && d.Refused == 0, $"domes: {(d != null ? d.Bursts : 0)} bursts, at most {domesSeen} at once (32), {(d != null ? d.Refused : 0)} refused");
            V(dropMost > 0, $"droplets live at most {dropMost} of {OpenZFxPlayer.LavaDropCapacity}");
            V(emberMost >= 40 && emberMost <= 110, $"embers live at most {emberMost} (40-80, plus the bursts')");
            V(wispMost > 0, $"wisps live at most {wispMost}; emitted {(air != null ? air.Wisps : 0)}");
            V(lava.LightCount >= 6 && lava.LightCount <= OzLava.MaxLights, $"lights {lava.LightCount} (10 at most)");
            V(lava.LightReachMax <= 4, $"at most {lava.LightReachMax} lights reach a ground point at night range (4)");
            V(lava.BedCount >= 1 && lava.BedCount <= 6, $"bed loops {lava.BedCount}, one per field");
            V(lava.GlowMaterials >= 1 && glowHi - glowLo > 0f, $"model glow: {lava.GlowMaterials} shared materials breathe, {glowLo:F3} .. {glowHi:F3}");
            V(pauseOk, "pause: heat, clock and bursts stood still for 2.5 s");
            V(holdOk, "HoldHeat: every field and the map at exactly 1.0");
            sb.Append($"[LV2] alive {(pass == total ? "PASS" : "FAIL")} {pass}/{total}");
            foreach (var l in sb.ToString().Split('\n')) Debug.Log(l.Trim());
        }
    }
}
