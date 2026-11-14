using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.3 item 40, the counted check of the four lighter GRAPHICS rows (View/OzLightRows.cs: SHADOWS, AMBIENT OCCLUSION, TREES AND
    /// PLANTS, WRECKS AND DEBRIS, and the LIGHTEST FOR SLOW PCS helper). Two menus:
    /// <list type="bullet">
    /// <item>1 (no Play, no scene): R1 nothing under Scripts/Sim names the rows or their keys; R2 a short headless computer match on L02,
    /// L16 and L17 (90 s of game time, seed 1, the switches of a normal game) gives the same world fingerprint after EVERY tick and the
    /// same end with every row on every value and with the helper on, as with every row on its default.</item>
    /// <item>2 (Play, in a level; level 16 for the tree counts): the game is held (paused, time 0, the camera's input off) and, at the
    /// held view: D1 with every row on its default (set through the rows' own code) the settings this item can touch, the state of every
    /// renderer of the scene (on, held off, shadow mode, active) and the counts drawn (renderers seen, shadow casters seen, the plant
    /// cells' renderers seen, and the engine's draw calls / batches / SetPass calls / shadow casters of the frame) are byte for byte those
    /// read with no saved value; L1-L5 each lighter value lowers what it should and the drop is printed; P1-P5 set back, everything
    /// is again byte for byte the first read; H1-H2 the helper sets all four and puts all four back. A picture per value goes beside the
    /// report.</item>
    /// </list>
    /// Draw counts come from the engine's render counters, which count every camera: close the Scene view (or keep it still) during menu 2.
    /// </summary>
    public static class OzV13LightRowsCheck
    {
        const string Tag = "[OpenZ lighter rows]";
        public static string Report = "not run";
        static readonly string[] Keys = { OzLightRows.ShadowsPref, OzLightRows.AoPref, OzLightRows.TreesPref, OzLightRows.WrecksPref };
        /// <summary>Every lighter value, one at a time: the key, its value, its name.</summary>
        static readonly (string key, int v, string name)[] Values =
        {
            (OzLightRows.ShadowsPref, 1, "SHADOWS OFF"), (OzLightRows.AoPref, 1, "AMBIENT OCCLUSION OFF"),
            (OzLightRows.TreesPref, 1, "TREES AND PLANTS LIGHT"), (OzLightRows.TreesPref, 2, "TREES AND PLANTS LIGHTEST"),
            (OzLightRows.WrecksPref, 1, "WRECKS AND DEBRIS LIGHT"),
        };
        public static readonly string[] Levels = { "L02", "L16", "L17" };
        public const double MatchSeconds = 90.0;

        static int ok, bad; static readonly StringBuilder fails = new StringBuilder(), lines = new StringBuilder();

        static void Check(bool pass, string what)
        {
            if (pass) ok++; else { bad++; fails.Append(" | ").Append(what); }
            string l = $"{Tag} {(pass ? "ok  " : "FAIL")} {what}"; lines.AppendLine(l); Debug.Log(l);
        }
        static void Note(string what) { string l = $"{Tag}      {what}"; lines.AppendLine(l); Debug.Log(l); }

        static string Dir()
        {
            var d = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/v1_3_20261030/item40_light_rows"));
            Directory.CreateDirectory(d); return d;
        }

        // ------------------------------------------------------------------ the player's own saved values
        static int[] keptV; static bool[] keptHas;
        static void Keep()
        {
            keptV = new int[Keys.Length]; keptHas = new bool[Keys.Length];
            for (int i = 0; i < Keys.Length; i++) { keptHas[i] = PlayerPrefs.HasKey(Keys[i]); keptV[i] = PlayerPrefs.GetInt(Keys[i], 0); }
        }
        static void PutBack()
        {
            if (keptV == null) return;
            for (int i = 0; i < Keys.Length; i++) { if (keptHas[i]) PlayerPrefs.SetInt(Keys[i], keptV[i]); else PlayerPrefs.DeleteKey(Keys[i]); }
            PlayerPrefs.Save(); keptV = null;
            OzLightRows.Reload();
        }
        /// <summary>No saved value for any row, then one key set (null = none), and the rows read them again.</summary>
        static void Only(string key, int v)
        {
            foreach (var k in Keys) PlayerPrefs.DeleteKey(k);
            if (key != null) PlayerPrefs.SetInt(key, v);
            PlayerPrefs.Save(); OzLightRows.Reload();
        }

        // ================================================================== menu 1: the rules at every value (no Play)
        [MenuItem("OpenZ/Checks/V1.3/Lighter rows 1 Rules the same at every value (no Play)")]
        public static void RulesMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning(Tag + " leave Play mode first (the matches set the simulation's static switches)"); return; }
            ok = bad = 0; fails.Clear(); lines.Clear();
            Keep();
            try
            {
                // R1: nothing of the rules names the rows
                var sim = Path.Combine(Application.dataPath, "OpenZ", "Scripts", "Sim"); int files = 0, named = 0; var who = new List<string>();
                foreach (var f in Directory.GetFiles(sim, "*.cs", SearchOption.AllDirectories))
                {
                    files++; var t = File.ReadAllText(f);
                    if (t.Contains("OzLightRows") || t.Contains("oz_lite_")) { named++; who.Add(Path.GetFileName(f)); }
                }
                Check(files > 0 && named == 0, $"R1 {files} rule files under Scripts/Sim, {named} name OzLightRows or an oz_lite_ key{(named > 0 ? ": " + string.Join(", ", who) : "")}");

                // R2: one match per level and value, every tick's fingerprint against the default's
                var configs = new List<(string name, System.Action set)> { ("every row on its default", () => Only(null, 0)) };
                foreach (var v in Values) { var c = v; configs.Add((c.name, () => Only(c.key, c.v))); }
                configs.Add(("LIGHTEST FOR SLOW PCS (all four)", () => { Only(null, 0); foreach (var c in Values) if (!(c.key == OzLightRows.TreesPref && c.v == 1)) PlayerPrefs.SetInt(c.key, c.v); PlayerPrefs.Save(); OzLightRows.Reload(); }));
                using (OzV11Kit.GameSwitches())
                    foreach (var level in Levels)
                    {
                        List<ulong> first = null; double end0 = 0; bool over0 = false; Team win0 = Team.Neutral;
                        foreach (var cfg in configs)
                        {
                            cfg.set();
                            var r = Match(level, 1, MatchSeconds, out double end, out bool over, out Team win, out string err);
                            if (err != null) { Check(false, $"R2 {level} {cfg.name}: the match failed ({err})"); continue; }
                            if (first == null) { first = r; end0 = end; over0 = over; win0 = win; Note($"R2 {level} default: {r.Count} ticks, ended at {end:0.00} s{(over ? ", won by " + win : "")}, last fingerprint {r[r.Count - 1]:x16}"); continue; }
                            int diff = -1; for (int i = 0; i < Mathf.Min(r.Count, first.Count) && diff < 0; i++) if (r[i] != first[i]) diff = i;
                            bool same = diff < 0 && r.Count == first.Count && System.Math.Abs(end - end0) < 1e-9 && over == over0 && win == win0;
                            Check(same, $"R2 {level} {cfg.name}: {r.Count} ticks, every fingerprint {(same ? "the same as with the defaults" : diff >= 0 ? "differs from tick " + diff : "the same but the length or the end differs")}");
                        }
                    }
            }
            finally { PutBack(); }
            Finish("menu 1 (rules)", "rules");
        }

        /// <summary>One headless computer match (no scene): the world's fingerprint after every tick (OzV11StrikeCount.Fold plus the tick's events).</summary>
        static List<ulong> Match(string level, int seed, double seconds, out double end, out bool over, out Team winner, out string error)
        {
            var hashes = new List<ulong>(); end = 0; over = false; winner = Team.Neutral; error = null;
            try
            {
                using (OzV11StrikeCount.Game(true))
                {
                    var w = ZWorld.Create(OzV11Kit.SettingsText, OzV11Kit.MapText(level), seed, level == "L17");
                    w.Bots.Add(new ZBot(w, Team.Blue, seed + 101)); w.Bots.Add(new ZBot(w, Team.Red, seed));
                    w.Events.Clear();
                    ulong h = 1469598103934665603UL;
                    while (w.Time < seconds && !w.GameOver)
                    {
                        w.Tick(ZConst.TICK);
                        foreach (var e in w.Events) { h = Mix(h, (long)e.type); h = Mix(h, e.objId); h = Mix(h, e.otherId); }
                        w.Events.Clear();
                        h = OzV11StrikeCount.Fold(h, w);
                        hashes.Add(h);
                    }
                    end = w.Time; over = w.GameOver; winner = w.Winner;
                }
            }
            catch (System.Exception e) { error = e.GetType().Name + ": " + e.Message; }
            return hashes;
        }
        static ulong Mix(ulong h, long v) { h ^= (ulong)v; h *= 1099511628211UL; h ^= h >> 31; return h; }

        // ================================================================== menu 2: draw counts and the put-back (Play, a level)
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static int waitFrame; static float waitUntil;
        static bool held, paused0, rtsOn0; static float scale0; static RtsCamera rts;
        static ProfilerRecorder rDraws, rBatches, rSetPass, rCasters;

        [MenuItem("OpenZ/Checks/V1.3/Lighter rows 2 Draw counts and put-back (Play, in a level)")]
        public static void DrawMenu()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning(Tag + " enter Play mode in a level first (level 16 for the tree counts)"); return; }
            if (stack.Count > 0) { Debug.LogWarning(Tag + " a run is still going"); return; }
            var g = ZGame.I;
            if (!g || g.World == null || g.World.GameOver) { Report = Tag + " FAIL: a running level is needed"; Debug.LogError(Report); return; }
            ok = bad = 0; fails.Clear(); lines.Clear(); Report = "running";
            stack.Push(Draws(g));
            waitFrame = 0; waitUntil = 0f; EditorApplication.update += Step;
        }

        static void Step()
        {
            if (stack.Count == 0 || !EditorApplication.isPlaying) { stack.Clear(); Release(); EditorApplication.update -= Step; return; }
            if (Time.frameCount < waitFrame || Time.realtimeSinceStartup < waitUntil) return;
            var top = stack.Peek(); bool more;
            try { more = top.MoveNext(); }
            catch (System.Exception ex) { Debug.LogException(ex); stack.Clear(); Release(); Report = Tag + " FAIL: exception " + ex.Message; Debug.LogError(Report); EditorApplication.update -= Step; return; }
            if (!more) { stack.Pop(); if (stack.Count == 0) EditorApplication.update -= Step; return; }
            if (top.Current is IEnumerator child) stack.Push(child);
            else if (top.Current is float s) waitUntil = Time.realtimeSinceStartup + s;
            else waitFrame = Time.frameCount + (top.Current is int n ? n : 1);
        }

        static void Hold(ZGame g)
        {
            paused0 = g.paused; scale0 = Time.timeScale; rts = Object.FindFirstObjectByType<RtsCamera>(); rtsOn0 = rts && rts.enabled;
            g.paused = true; Time.timeScale = 0f; if (rts) rts.enabled = false; held = true;
        }
        static void Release()
        {
            PutBack();
            if (held) { var g = ZGame.I; if (g) g.paused = paused0; Time.timeScale = scale0; if (rts) rts.enabled = rtsOn0; held = false; }
            if (rDraws.Valid) rDraws.Dispose(); if (rBatches.Valid) rBatches.Dispose(); if (rSetPass.Valid) rSetPass.Dispose(); if (rCasters.Valid) rCasters.Dispose();
        }

        /// <summary>What one read holds: the settings line, the renderer state hash and the counts.</summary>
        sealed class Read
        {
            public string settings; public ulong state; public int renderers, seen, seenCasters, floraSeen;
            public long draws, batches, setpass, casters;
            public string Counts => $"seen {seen} renderers ({seenCasters} casting), plant cells {floraSeen}; draw calls {draws}, batches {batches}, SetPass {setpass}, shadow casters {casters}";
            public bool SameAs(Read o) => settings == o.settings && state == o.state && renderers == o.renderers && seen == o.seen && seenCasters == o.seenCasters
                                          && floraSeen == o.floraSeen && draws == o.draws && batches == o.batches && setpass == o.setpass && casters == o.casters;
        }

        static string Settings()
        {
            var sb = new StringBuilder();
            var cam = Camera.main; var d = cam ? cam.GetComponent<UniversalAdditionalCameraData>() : null;
            sb.Append($"camera shadows {(d ? d.renderShadows.ToString() : "-")}; ssao");
            var assets = new List<UniversalRenderPipelineAsset> { GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset,
                Resources.Load<UniversalRenderPipelineAsset>(OzQuality.ResDir + OzQuality.MediumAsset), Resources.Load<UniversalRenderPipelineAsset>(OzQuality.ResDir + OzQuality.LowAsset) };
            var fi = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var a in assets)
            {
                if (!a || fi == null) continue;
                if (fi.GetValue(a) is ScriptableRendererData[] list)
                    foreach (var rd in list) if (rd) foreach (var f in rd.rendererFeatures) if (f) sb.Append($" {rd.name}/{f.name}:{(f.isActive ? 1 : 0)}");
            }
            sb.Append($"; plant cells margin {OzL16DrawAhead.Margin:0.##} far above {OzL16DrawAhead.FarAbove:0.##} band {OzL16DrawAhead.FarBand:0.##}");
            sb.Append($"; wreck gate {WreckSinkGate.CapWrecks} / {WreckSinkGate.CapRenderers}; wreck pieces {WreckPieces.MaxLive}");
            sb.Append($"; held by the rows: '{OzLightRows.Touched()}'; driver {(GameObject.Find("OZ_LightRows") ? "yes" : "no")}");
            return sb.ToString();
        }

        static ulong Hash(ulong h, long v) => Mix(h, v);

        static IEnumerator Sample(Read r, int frames)
        {
            var dr = new List<long>(); var ba = new List<long>(); var sp = new List<long>(); var ca = new List<long>();
            for (int i = 0; i < frames; i++)
            {
                yield return 1;
                dr.Add(rDraws.LastValue); ba.Add(rBatches.LastValue); sp.Add(rSetPass.LastValue); ca.Add(rCasters.LastValue);
            }
            r.draws = Median(dr); r.batches = Median(ba); r.setpass = Median(sp); r.casters = Median(ca);
        }
        static long Median(List<long> l) { l.Sort(); return l.Count == 0 ? -1 : l[l.Count / 2]; }

        static IEnumerator Take(Read r)
        {
            yield return 10;                                                                                // the plant cells switch in LateUpdate; the renderers' seen flags need a drawn frame
            r.settings = Settings();
            ulong h = 1469598103934665603UL; int n = 0, seen = 0, seenC = 0, flora = 0;
            var froot = GameObject.Find(OzL16DrawAhead.FloraRoot); var ft = froot ? froot.transform : null;
            foreach (var x in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
            {
                if (!x) continue; n++;
                h = Hash(h, x.GetInstanceID()); h = Hash(h, x.enabled ? 1 : 0); h = Hash(h, x.forceRenderingOff ? 1 : 0); h = Hash(h, (long)x.shadowCastingMode); h = Hash(h, x.gameObject.activeInHierarchy ? 1 : 0);
                if (!x.isVisible) continue;
                seen++; if (x.shadowCastingMode != ShadowCastingMode.Off) seenC++;
                if (ft && x.transform.IsChildOf(ft)) flora++;
            }
            r.state = h; r.renderers = n; r.seen = seen; r.seenCasters = seenC; r.floraSeen = flora;
            yield return Sample(r, 30);
        }

        static IEnumerator Shot(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir(), $"{ZGame.I.level}_{name}.png"));
            yield return 3;
        }

        /// <summary>Puts one row on one value through the row's own code (what the Options row calls).</summary>
        static void Set(string key, int v)
        {
            if (key == OzLightRows.ShadowsPref) OzLightRows.ShadowsOn = v == 0;
            else if (key == OzLightRows.AoPref) OzLightRows.AoOn = v == 0;
            else if (key == OzLightRows.TreesPref) OzLightRows.Trees = v;
            else if (key == OzLightRows.WrecksPref) OzLightRows.WrecksLight = v == 1;
        }

        static IEnumerator Draws(ZGame g)
        {
            Keep(); Only(null, 0);                                                                          // the player's own lighter values go out first (put back at the end)
            Hold(g);
            rDraws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            rBatches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            rSetPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            rCasters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
            bool l16 = g.level == OzL16DrawAhead.Level, low = OzQuality.Level == 2;
            if (!rDraws.Valid || !rCasters.Valid) Note("the engine's render counters are not available here: the draw-call parts of L1-L5 / H1 cannot pass (run it in the editor's Play mode)");
            Note($"level {g.level}, graphics {OzQuality.Name}, camera {(rts ? rts.height : -1f):0} m; {(l16 ? "the plant cells are counted" : "no plant cells on this level: TREES AND PLANTS must change nothing here")}");

            var r0 = new Read(); yield return Take(r0);
            Note("no saved value (the rows' own code put back whatever they held): " + r0.Counts); Note("   " + r0.settings);
            yield return Shot("0_default");

            // D1: every row on its default through its own code = the first read, byte for byte
            OzLightRows.ShadowsOn = true; OzLightRows.AoOn = true; OzLightRows.Trees = 0; OzLightRows.WrecksLight = false;
            var r1 = new Read(); yield return Take(r1);
            Check(r1.SameAs(r0) && OzLightRows.Touched() == "", $"D1 every row on its default: settings, {r1.renderers} renderers' state and the counts the same as with no saved value ({r1.Counts})");

            int k = 0;
            foreach (var v in Values)
            {
                k++;
                Set(v.key, v.v);
                var r = new Read(); yield return Take(r);
                long dDraw = r0.draws - r.draws, dCast = r0.casters - r.casters; int dSeen = r0.seen - r.seen, dFlora = r0.floraSeen - r.floraSeen;
                string drop = $"draw calls {r0.draws} -> {r.draws} (-{dDraw}), batches {r0.batches} -> {r.batches}, shadow casters {r0.casters} -> {r.casters} (-{dCast}), renderers seen {r0.seen} -> {r.seen} (-{dSeen}), plant cells {r0.floraSeen} -> {r.floraSeen} (-{dFlora})";
                bool pass;
                if (v.key == OzLightRows.ShadowsPref)
                {
                    var d = Camera.main ? Camera.main.GetComponent<UniversalAdditionalCameraData>() : null;
                    pass = d && !d.renderShadows && (r0.casters == 0 || (r.casters < r0.casters && r.draws < r0.draws));   // other cameras (the Scene view) may still count a few
                }
                else if (v.key == OzLightRows.AoPref) pass = low ? r.draws == r0.draws : r.draws < r0.draws;          // LOW has no SSAO: nothing to take
                else if (v.key == OzLightRows.TreesPref)
                    pass = OzL16DrawAhead.FarAbove == (v.v == 1 ? OzLightRows.TreesLightFarAbove : OzLightRows.TreesLightestFarAbove)
                           && OzL16DrawAhead.Margin == (v.v == 1 ? OzLightRows.TreesLightMargin : OzLightRows.TreesLightestMargin)
                           && (!l16 ? r.draws == r0.draws : v.v == 2 ? r.floraSeen < r0.floraSeen && r.draws < r0.draws : r.floraSeen <= r0.floraSeen && r.draws <= r0.draws);
                else pass = WreckSinkGate.CapWrecks == OzLightRows.WrecksLightWrecks && WreckSinkGate.CapRenderers == OzLightRows.WrecksLightParts && WreckPieces.MaxLive == WreckPieces.MaxLiveLight
                            && r.draws <= r0.draws;
                Check(pass, $"L{k} {v.name}: {drop}");
                yield return Shot($"{k}_{v.name.Replace(' ', '_')}");
                Set(v.key, 0);
                var rb = new Read(); yield return Take(rb);
                Check(rb.SameAs(r0) && OzLightRows.Touched() == "", $"P{k} {v.name} set back: settings, renderers' state and counts the same as before ({rb.Counts})");
                if (!rb.SameAs(r0)) Note("   now: " + rb.settings);
            }

            // H1-H2: the helper
            OzLightRows.Lightest = true;
            var rh = new Read(); yield return Take(rh);
            Check(OzLightRows.Lightest && !OzLightRows.ShadowsOn && !OzLightRows.AoOn && OzLightRows.Trees == 2 && OzLightRows.WrecksLight && rh.draws < r0.draws,
                  $"H1 LIGHTEST FOR SLOW PCS on: the four rows on their lightest; draw calls {r0.draws} -> {rh.draws} (-{r0.draws - rh.draws}), shadow casters {r0.casters} -> {rh.casters}, plant cells {r0.floraSeen} -> {rh.floraSeen}");
            yield return Shot("6_LIGHTEST");
            OzLightRows.Lightest = false;
            var rh2 = new Read(); yield return Take(rh2);
            Check(!OzLightRows.Lightest && OzLightRows.AllDefault && rh2.SameAs(r0) && OzLightRows.Touched() == "", $"H2 the helper off: the four rows on their defaults, everything the same as before ({rh2.Counts})");

            Release();
            Finish("menu 2 (draw counts, " + g.level + ")", "draws_" + g.level);
        }

        static void Finish(string what, string file)
        {
            Report = $"{Tag} {(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad} {what}{fails}";
            lines.AppendLine(Report);
            File.WriteAllText(Path.Combine(Dir(), $"{file}_{System.DateTime.Now:yyyyMMdd_HHmm}.txt"), lines.ToString());
            if (bad == 0) Debug.Log(Report); else Debug.LogError(Report);
        }
    }
}
