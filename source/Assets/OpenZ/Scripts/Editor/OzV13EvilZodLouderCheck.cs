#if !OPENZ_HARNESS
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The counted check of OzBlueCameo.evilZodDb (+1 dB on EvilZod's words in the four blue
    /// scenes; BS-01, the music, the sounds and the red side's Zod unchanged). Edit mode, no Play:
    ///  C1 the setting: a fresh OzBlueCameo has 0 &lt; evilZodDb &lt;= 1.5 (EvilZodDbMax).
    ///  C2 per scene, the envelope (OzBlueCameo.EvilRuns / EvilGainAt) on the measured words of blue_scenes.json: every EvilZod word
    ///     at the full step over its whole span, every BS-01 word at 0 dB over its whole span except where the take itself overlaps the
    ///     two voices (the nuke scene: BS-01 cuts in 0.08 s before EvilZod's last word ends) - those words are listed with their largest
    ///     step, which must stay under the full step.
    ///  C3 per scene, the speech level (gated RMS, 50 ms blocks over -50 dBFS in the take as it is, the same blocks before and after)
    ///     of each voice's lines in the take before and after the envelope: EvilZod + evilZodDb (+-0.15 dB), BS-01 +-0.15 dB (+-0.4 dB
    ///     in the overlapping nuke scene). The EBU R128 table
    ///     (LUFS / dBTP) of the same simulation is check/measure_blue_voices.py in the 1.3 package (ffmpeg).
    ///  C4 per scene, the loudest sample after the envelope, at the scene's playback level (voiceDb, the VOICE slider at 100 %): under
    ///     -0.5 dBFS (no clipping at the source).
    /// </summary>
    public static class OzV13EvilZodLouderCheck
    {
        const string T = "[v13 item19] ";

        [MenuItem("OpenZ/V1.3/Voices: 19 EvilZod Louder Check (no Play)", false, 8190)]
        static void CheckMenu() { Check(); }

        public static string Check()
        {
            var sb = new StringBuilder(); int pass = 0, fail = 0;
            void Row(string what, bool ok) { if (ok) pass++; else fail++; sb.AppendLine((ok ? "- PASS " : "- FAIL ") + what); }

            // C1 the setting as a new component gets it (the cameo is added at run time: OzBlueCameo.Install)
            float db, voiceDb;
            var go = new GameObject("v13 item19 probe") { hideFlags = HideFlags.HideAndDontSave };
            try { var c = go.AddComponent<OzBlueCameo>(); db = c.evilZodDb; voiceDb = c.voiceDb; }
            finally { Object.DestroyImmediate(go); }
            Row($"C1 evilZodDb {db:0.00} dB (0 < step <= {OzBlueCameo.EvilZodDbMax:0.0}), voiceDb {voiceDb:0.0} dB, ramp {OzBlueCameo.EvilZodRamp:0.00} s", db > 0f && db <= OzBlueCameo.EvilZodDbMax);
            float full = Mathf.Pow(10f, Mathf.Min(db, OzBlueCameo.EvilZodDbMax) / 20f);

            var lib = OzBlueScenes.Shared;
            foreach (var id in new[] { OzBlueScenes.Briefing, OzBlueScenes.Win, OzBlueScenes.Lose, OzBlueScenes.WinNuke })
            {
                var s = lib.Scene(id);
                if (s == null || s.words == null || s.lines == null) { Row($"C2 {id}: no scene / words in blue_scenes.json", false); continue; }
                var runs = OzBlueCameo.EvilRuns(s);
                // C2 the envelope on the words
                int ezOk = 0, ezAll = 0, skOk = 0, skAll = 0; var overlaps = new List<string>();
                foreach (var w in s.words)
                {
                    if (w == null) continue;
                    float lo = float.MaxValue, hi = 0f;
                    for (float t = w.s; t <= w.e + 1e-4f; t += 0.005f) { float g = OzBlueCameo.EvilGainAt(runs, t, db); lo = Mathf.Min(lo, g); hi = Mathf.Max(hi, g); }
                    if (w.who == OzBlueScenes.EvilZod) { ezAll++; if (Mathf.Abs(lo - full) < 1e-4f) ezOk++; }
                    else
                    {
                        skAll++;
                        if (hi - 1f < 1e-4f) skOk++;
                        else if (Overlaps(w, s.words)) overlaps.Add($"\"{w.w}\" up to {20f * Mathf.Log10(hi):+0.00} dB");
                    }
                }
                bool overlapOk = skOk + overlaps.Count == skAll;                                           // a touched BS-01 word is only ever one the take overlaps
                Row($"C2 {id}: {runs.Length / 2} EvilZod stretches; EvilZod words at the full step {ezOk}/{ezAll}; BS-01 words untouched {skOk}/{skAll}" +
                    (overlaps.Count > 0 ? "; in the take's own overlap: " + string.Join(", ", overlaps) : ""), ezOk == ezAll && overlapOk);

                // C3 / C4 the take itself
                var clip = lib.Clip(s);
                var d = Samples(clip);
                if (d == null) { Row($"C3 {id}: the take does not load", false); continue; }
                int sr = clip.frequency, ch = Mathf.Max(1, clip.channels);
                float ezB = Level(d, sr, ch, s, OzBlueScenes.EvilZod, null, 0f), ezA = Level(d, sr, ch, s, OzBlueScenes.EvilZod, runs, db);
                float skB = Level(d, sr, ch, s, OzBlueScenes.Sidekick, null, 0f), skA = Level(d, sr, ch, s, OzBlueScenes.Sidekick, runs, db);
                float skTol = overlaps.Count > 0 ? 0.4f : 0.15f;
                Row($"C3 {id}: EvilZod {ezB:0.0} -> {ezA:0.0} dB ({ezA - ezB:+0.00}), BS-01 {skB:0.0} -> {skA:0.0} dB ({skA - skB:+0.00}); EvilZod - BS-01 {ezB - skB:+0.0} -> {ezA - skA:+0.0} dB",
                    Mathf.Abs(ezA - ezB - db) <= 0.15f && Mathf.Abs(skA - skB) <= skTol);
                float peak = 0f;
                for (int i = 0; i < d.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(d[i]) * OzBlueCameo.EvilGainAt(runs, (i / ch) / (float)sr, db));
                float outDb = 20f * Mathf.Log10(peak + 1e-9f) + voiceDb;
                Row($"C4 {id}: loudest sample after the step {20f * Mathf.Log10(peak + 1e-9f):0.0} dBFS in the take, {outDb:0.0} dBFS at the source (voiceDb {voiceDb:0.0})", outDb < -0.5f);
            }

            string verdict = fail == 0 ? $"PASS {pass}/{pass + fail}" : $"FAIL {fail} of {pass + fail}";
            sb.Insert(0, "# 1.3 item 19 EvilZod a tiny bit louder: counted check " + verdict + " (" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")\n\n");
            try { var path = Path.Combine(OpenZPaths.Records, "v13_item19_evilzod_louder_check.md"); File.WriteAllText(path, sb.ToString()); sb.AppendLine("report: " + path.Replace('\\', '/')); }
            catch (System.Exception e) { sb.AppendLine("report not written: " + e.Message); }
            if (fail == 0) Debug.Log(T + verdict + "\n" + sb); else Debug.LogError(T + verdict + "\n" + sb);
            return verdict;
        }

        /// <summary>A BS-01 word that overlaps one of EvilZod's words in the take (the two voices at the same time).</summary>
        static bool Overlaps(OzBlueWord w, OzBlueWord[] all)
        {
            foreach (var o in all) if (o != null && o.who == OzBlueScenes.EvilZod && o.s < w.e + OzBlueCameo.EvilZodRamp && o.e > w.s - OzBlueCameo.EvilZodRamp) return true;
            return false;
        }

        /// <summary>The gated RMS (dB) of one speaker's line windows in the take, with the envelope applied when runs is given. The gate
        /// over -50 dBFS no longer joins the "after" sum and pulls the difference under the step (a false FAIL).</summary>
        static float Level(float[] d, int sr, int ch, OzBlueScene s, string who, float[] runs, float db)
        {
            int block = Mathf.Max(1, sr / 20); double sum = 0; long n = 0;
            foreach (var l in s.lines)
            {
                if (l == null || l.who != who) continue;
                int a = Mathf.Clamp(Mathf.FloorToInt(l.start * sr), 0, d.Length / ch), b = Mathf.Clamp(Mathf.CeilToInt(l.end * sr), 0, d.Length / ch);
                for (int i = a; i + block <= b; i += block)
                {
                    double e = 0, e0 = 0;
                    for (int k = 0; k < block; k++)
                    {
                        float g = runs != null ? OzBlueCameo.EvilGainAt(runs, (i + k) / (float)sr, db) : 1f;
                        float x0 = d[(i + k) * ch], x = x0 * g; e += x * x; e0 += x0 * x0;
                    }
                    if (10.0 * System.Math.Log10(e0 / block + 1e-12) > -50.0) { sum += e; n += block; }
                }
            }
            return n == 0 ? float.NaN : (float)(10.0 * System.Math.Log10(sum / n + 1e-12));
        }

        static float[] Samples(AudioClip c)
        {
            if (!c) return null;
            if (c.loadState != AudioDataLoadState.Loaded) c.LoadAudioData();
            var d = new float[c.samples * c.channels];
            return c.GetData(d, 0) ? d : null;
        }
    }
}
#endif
