// OpenZ (GPL-3.0) - version 1.4: the counted checks of level 7's card and first start, and of the rule for every new level.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Video;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Two menus. Nothing is saved; every switch a check sets is put back.
    /// OpenZ/Checks/V1.4/Level warm lists (edit mode or Play):
    ///  W1 every level of the menu has its list Resources/OpenZ/Shaders/Levels/&lt;id&gt;_warm with variants in it;
    ///  W2 each list holds every shader of its scene that the shared list holds; W3 and none that only other scenes use;
    ///  W4 the start rule: levels of 1.4 on (L07, a made-up L08) start ahead on both sides, L02 / L16 / L17 exactly as before
    ///     (OzStartAhead.Wanted with its own defaults: L16 blue only), and with OzLevelWarm.On off nothing changes;
    ///  W5 the build hook is in place (a new map gets its list at every build).
    /// OpenZ/Checks/V1.4/Level cards and loading warm-up (Play, main menu):
    ///     15-30 s long (OzPreviewStandard numbers in the release check, tools/release_check_levels.py); L07: the owner's one exception, OwnerKeep;
    ///  C2 every card, selected in the level select for <see cref="CardSeconds"/> s: its picture is live within 3 s, the picture's texture
    ///     is the clip's own size; C3 per card: new pictures a second at least 0.85 x the clip's rate (or the screen's lower cap), worst frame
    ///     at most 1.2 x the cards' median worst + 5 ms, frames a second at least 0.9 x the cards' median: every card plays the same way;
    ///  C4 the loading screen's warm-up with a cold driver emulated (40 variants of 57 ms, OzShaderWarm's emulation): it runs one variant a
    ///     frame within the budget, the shared list waits meanwhile, it ends at 1 and a second call is done at once.
    /// </summary>
    public static class OzV14LevelCheck
    {
        const string Tag = "[OpenZ V1.4 level check]";
        public const float CardSeconds = 8f, WarmUpSeconds = 3f;
        // (size and frame rate are still checked, C2-C4 as for every card). Same pin as tools/release_check_levels.py OWNER_KEEP (kits/l07_preview_warm):
        const string KeepId = "L07", KeepFile = "Assets/OpenZ/Resources/OpenZ/Menu/L07_preview.mp4", KeepMd5 = "185eab6a80384cdc09154894824f3d21"; const float KeepSeconds = 12f;
        static bool OwnerKeep(string id, int clips, float len)
        {
            if (id != KeepId || clips != 1 || Mathf.Abs(len - KeepSeconds) > 0.05f || !File.Exists(KeepFile)) return false;
            using (var h = System.Security.Cryptography.MD5.Create()) return string.Concat(h.ComputeHash(File.ReadAllBytes(KeepFile)).Select(b => b.ToString("x2"))) == KeepMd5;
        }
        public static string Report = "not run";
        static int ok, bad;
        static readonly StringBuilder lines = new StringBuilder();

        static void Check(string what, bool pass) { if (pass) ok++; else bad++; lines.AppendLine((pass ? "PASS " : "FAIL ") + what); }

        static string Dir()
        {
            var d = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/v1_4/l07_preview_warm"));
            Directory.CreateDirectory(d); return d;
        }

        static void Finish(string name)
        {
            Report = $"{Tag} {name}: {(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad}";
            lines.Insert(0, Report + "\n");
            File.WriteAllText(Path.Combine(Dir(), name.Replace(' ', '_') + ".txt"), lines.ToString());
            if (bad == 0) Debug.Log(lines.ToString()); else Debug.LogError(lines.ToString());
        }

        // ================================================================== the warm lists and the start rule
        [MenuItem("OpenZ/Checks/V1.4/Level warm lists (edit mode or Play)")]
        public static void RunLists()
        {
            ok = bad = 0; lines.Clear();
            var plans = OzLevelWarmLists.Plans(out var why);
            Check("W0 the shared list is there (" + (why ?? OzShaderWarmup.ListPath) + ")", why == null);
            var shared = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(OzShaderWarmup.ListPath);
            var sharedShaders = new HashSet<Shader>(OzLevelWarmLists.Entries(shared).Select(e => e.s));
            foreach (var p in plans)
            {
                var list = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(OzLevelWarmLists.PathOf(p.Id));
                var have = new HashSet<Shader>(OzLevelWarmLists.Entries(list).Select(e => e.s));
                Check($"W1 {p.Id}: list {OzLevelWarmLists.PathOf(p.Id)} {(list ? list.variantCount + " variants in " + list.shaderCount + " shaders" : "MISSING")} (the plan: {p.Entries.Count})", list && list.variantCount > 0);
                var missing = p.SceneShaders.Where(s => sharedShaders.Contains(s) && !have.Contains(s)).Select(s => s.name).ToList();
                Check($"W2 {p.Id}: every shader of {p.Scene} that the shared list holds is in it ({p.SceneShaders.Count(s => sharedShaders.Contains(s))}; missing: {(missing.Count == 0 ? "none" : string.Join(", ", missing))})", list && missing.Count == 0);
                var extra = have.Where(s => p.OnlyOthers.Contains(s)).Select(s => s.name).ToList();
                Check($"W3 {p.Id}: no shader that only other scenes use ({p.OnlyOthers.Count} left out; in the list anyway: {(extra.Count == 0 ? "none" : string.Join(", ", extra))})", list && extra.Count == 0);
            }
            bool on0 = OzLevelWarm.On, aOn = OzStartAhead.On, red0 = OzStartAhead.RedToo; string lv0 = OzStartAhead.Levels;
            try
            {
                OzLevelWarm.On = true; OzStartAhead.On = true; OzStartAhead.RedToo = false; OzStartAhead.Levels = "L16";
                bool a7 = OzLevelWarm.StartAheadBothSides("L07"), a8 = OzLevelWarm.StartAheadBothSides("L08"), a2 = OzLevelWarm.StartAheadBothSides("L02"), a16 = OzLevelWarm.StartAheadBothSides("L16"), a17 = OzLevelWarm.StartAheadBothSides("L17");
                bool r7 = OzStartAhead.Wanted("L07", false, out _), b7 = OzStartAhead.Wanted("L07", true, out _), r16 = OzStartAhead.Wanted("L16", false, out var w16), b16 = OzStartAhead.Wanted("L16", true, out _),
                     b2 = OzStartAhead.Wanted("L02", true, out _), r17 = OzStartAhead.Wanted("L17", false, out _);
                Check($"W4 the start rule: both sides L07 {a7}, L08 {a8}; as before L02 {a2}, L16 {a16}, L17 {a17}. Wanted: L07 red {r7}, L07 blue {b7}, L16 red {r16} ({w16}), L16 blue {b16}, L02 blue {b2}, L17 red {r17}",
                      a7 && a8 && !a2 && !a16 && !a17 && r7 && b7 && !r16 && b16 && !b2 && !r17);
                OzLevelWarm.On = false;
                bool off7 = OzStartAhead.Wanted("L07", false, out var wo);
                Check($"W4b OzLevelWarm.On off: L07 red start {off7} ({wo}), the loading step {OzLevelWarm.Step("L07"):0} (1 = no wait), the shared list waits {OzLevelWarm.HoldsGlobal}", !off7 && !OzLevelWarm.HoldsGlobal);
            }
            finally { OzLevelWarm.On = on0; OzStartAhead.On = aOn; OzStartAhead.RedToo = red0; OzStartAhead.Levels = lv0; }
            Check("W5 the build hook makes the lists before every player build (OzLevelWarmListsBuild)", typeof(IPreprocessBuildWithReport).IsAssignableFrom(typeof(OzLevelWarmListsBuild)));
            Finish("level warm lists");
        }

        // ================================================================== the cards and the loading warm-up (Play)
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static int waitFrame; static float waitUntil;

        [MenuItem("OpenZ/Checks/V1.4/Level cards and loading warm-up (Play, main menu)")]
        public static void RunCards()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning(Tag + " enter Play on the MainMenu scene first"); return; }
            if (stack.Count > 0) { Debug.LogWarning(Tag + " a run is still going"); return; }
            var m = Object.FindFirstObjectByType<OpenZMainMenu>();
            if (!m) { Debug.LogWarning(Tag + " start it on the main menu (the MainMenu scene in Play)"); return; }
            ok = bad = 0; lines.Clear(); Report = "running";
            stack.Push(Cards(m)); waitFrame = 0; waitUntil = 0f; EditorApplication.update += Step;
        }

        static void Step()
        {
            if (stack.Count == 0 || !EditorApplication.isPlaying) { stack.Clear(); PutBack(); EditorApplication.update -= Step; return; }
            if (Time.frameCount < waitFrame || Time.realtimeSinceStartup < waitUntil) return;
            var top = stack.Peek(); bool more;
            try { more = top.MoveNext(); }
            catch (System.Exception ex) { Debug.LogException(ex); stack.Clear(); PutBack(); Report = Tag + " FAIL: exception " + ex.Message; Debug.LogError(Report); EditorApplication.update -= Step; return; }
            if (!more) { stack.Pop(); if (stack.Count == 0) EditorApplication.update -= Step; return; }
            if (top.Current is IEnumerator child) stack.Push(child);
            else if (top.Current is float s) waitUntil = Time.realtimeSinceStartup + s;
            else waitFrame = Time.frameCount + (top.Current is int n ? n : 1);
        }

        static void PutBack() { OzShaderWarm.ProofColdStepMs = 0f; OzShaderWarm.ProofColdVariants = 0; OzLevelWarm.ProofReset(); }

        sealed class Row { public string id, clip; public int clips, w, h, rtW, rtH, frames, pics, dropped; public float fps, len, worst, sec, firstPic = -1f; }
        static int droppedNow;
        static void OnDropped(VideoPlayer v) => droppedNow++;

        static IEnumerator Cards(OpenZMainMenu m)
        {
            bool light = OzMenuVideo.UseLight; int wantW = light ? 1920 : 2560, wantH = light ? 1080 : 1440; float wantFps = light ? 30f : 60f;
            var rows = new List<Row>();
            float q0 = Time.realtimeSinceStartup;
            while (OzLevelPreview.PreparesPending && Time.realtimeSinceStartup - q0 < 40f) yield return 5;          // every card's first prepare done (1.3 item 23's queue), so each card starts from the same state
            foreach (var id in OzLevelL07.MenuOrder())
            {
                int i = m.ProofLevelIndex(id);
                if (i < 0) { Check($"C1 {id}: not on the menu", false); continue; }
                m.ProofGoSelect(i);
                OzLevelPreview p = null;
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 12f)
                {
                    p = OzLevelPreview.Live.Find(x => x.Level == id);
                    if (p != null && p.ShowingVideo) break;
                    yield return 1;
                }
                var r = new Row { id = id };
                if (p != null && p.ShowingVideo) r.firstPic = Time.realtimeSinceStartup - t0;
                if (p != null && p.Player)
                {
                    var c = p.Player.clip; r.clips = p.ClipCount;
                    if (c) { r.clip = c.name; r.w = (int)c.width; r.h = (int)c.height; r.fps = (float)c.frameRate; r.len = (float)c.length; }
                    if (p.Frame) { r.rtW = p.Frame.width; r.rtH = p.Frame.height; }
                    yield return WarmUpSeconds;
                    droppedNow = 0; p.Player.frameDropped += OnDropped;
                    long last = p.Player.frame; float s0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - s0 < CardSeconds)
                    {
                        r.frames++; float d = Time.unscaledDeltaTime * 1000f; if (r.frames > 1 && d > r.worst) r.worst = d;
                        if (p.Player.frame != last) { r.pics++; last = p.Player.frame; }
                        yield return 1;
                    }
                    r.sec = Time.realtimeSinceStartup - s0; r.dropped = droppedNow; p.Player.frameDropped -= OnDropped;
                }
                rows.Add(r);
            }
            float Median(IEnumerable<float> v) { var a = v.OrderBy(x => x).ToList(); return a.Count == 0 ? 0f : a[a.Count / 2]; }
            float mWorst = Median(rows.Select(x => x.worst)), mFps = Median(rows.Select(x => x.frames / Mathf.Max(0.01f, x.sec)));
            float cap = OzMenuVideo.ScreenCap();
            foreach (var r in rows)
            {
                bool keep = OwnerKeep(r.id, r.clips, r.len);                                                   // 1.4: L07's owner exception (count and length only)
                Check($"C1 {r.id}: {r.clips} clips, on screen {r.clip} {r.w}x{r.h} {r.fps:0.##} fps {r.len:0.0} s (standard: 2-3 clips, {wantW}x{wantH} {wantFps:0} fps, 15-30 s{(keep ? "; owner exception 5 Nov 2026: L07 keeps his chosen 12 s shot, 1 clip, this exact file" : "")})",
                      ((r.clips >= 2 && r.clips <= 3 && r.len >= 15f && r.len <= 30f) || keep) && r.w == wantW && r.h == wantH && Mathf.Abs(r.fps - wantFps) < 0.5f);
                Check($"C2 {r.id}: live picture after {r.firstPic:0.00} s (3 s at most), its texture {r.rtW}x{r.rtH} = the clip's {r.w}x{r.h}", r.firstPic >= 0f && r.firstPic <= 3f && r.rtW == r.w && r.rtH == r.h);
                float exp = cap > 0f ? Mathf.Min(r.fps, cap) : r.fps, pps = r.pics / Mathf.Max(0.01f, r.sec), fps = r.frames / Mathf.Max(0.01f, r.sec);
                Check($"C3 {r.id}: {pps:0.0} new pictures a second (line {exp * 0.85f:0.0}), {r.dropped} dropped, worst frame {r.worst:0.0} ms (line {mWorst * 1.2f + 5f:0.0}), {fps:0.0} fps (line {mFps * 0.9f:0.0})",
                      pps >= exp * 0.85f && r.worst <= mWorst * 1.2f + 5f && fps >= mFps * 0.9f);
            }
            m.ProofGoMain();
            // ---- C4 the loading screen's warm-up, a cold driver emulated (no level is loaded)
            OzLevelWarm.ProofReset(); OzShaderWarm.ProofColdStepMs = OzShaderWarm.ColdStepMs; OzShaderWarm.ProofColdVariants = 40;
            int frames = 0, held = 0; float last01 = 0f, prog = 0f; bool rising = true; float w0 = Time.realtimeSinceStartup;
            while (prog < 1f && Time.realtimeSinceStartup - w0 < 20f)
            {
                prog = OzLevelWarm.Step("L07"); frames++;
                if (prog < last01) rising = false; last01 = prog;
                if (prog < 1f && OzLevelWarm.HoldsGlobal) held++;
                yield return 1;
            }
            bool missing = OzLevelWarm.ProofMissing; int steps = OzLevelWarm.ProofSteps, wf = OzLevelWarm.ProofFrames; double longest = OzLevelWarm.ProofLongestMs;
            float again = OzLevelWarm.Step("L07");
            Check($"C4 the loading warm-up, cold driver emulated: {(missing ? "NO LIST for L07" : steps + " variants over " + wf + " work frames")} ({frames} calls, the bar only rose {rising}, the shared list waited {held} frames, longest step {longest:0.0} ms); "
                  + $"done {prog:0.00}, a second call {again:0.00}; {OzLevelWarm.LastLine}",
                  !missing && prog >= 1f && again >= 1f && rising && steps == 40 && wf >= 40 && held >= wf - 1 && longest < OzShaderWarm.ColdStepMs + OzLevelWarm.LoadingBudgetMs);
            PutBack();
            Finish("level cards and loading warm-up");
        }
    }
}
